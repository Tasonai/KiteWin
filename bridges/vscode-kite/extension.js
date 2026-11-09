const vscode = require('vscode');
const http = require('http');
const path = require('path');

let statusBarItem;
let eventSourceReq = null;
let reconnectTimer = null;
let isConnected = false;

function activate(context) {
    const config = vscode.workspace.getConfiguration('kite');
    const port = config.get('port') || 24242;

    statusBarItem = vscode.window.createStatusBarItem(vscode.StatusBarAlignment.Right, 100);
    statusBarItem.command = 'kite.openCanvas';
    updateStatusBar(false);
    statusBarItem.show();
    context.subscriptions.push(statusBarItem);

    // Register commands
    context.subscriptions.push(
        vscode.commands.registerCommand('kite.focusNode', () => {
            const editor = vscode.window.activeTextEditor;
            if (editor) {
                notifyActiveFile(editor.document.fileName, editor.selection.active.line + 1, port);
            }
        }),
        vscode.commands.registerCommand('kite.syncArchitecture', () => {
            triggerSync(port);
        }),
        vscode.commands.registerCommand('kite.openCanvas', () => {
            vscode.env.openExternal(vscode.Uri.parse(`http://127.0.0.1:${port}/`));
        })
    );

    // Track active file change
    context.subscriptions.push(
        vscode.window.onDidChangeActiveTextEditor((editor) => {
            if (!editor) return;
            const autoFocus = vscode.workspace.getConfiguration('kite').get('autoFocus') ?? true;
            if (autoFocus) {
                notifyActiveFile(editor.document.fileName, editor.selection.active.line + 1, port);
            }
        })
    );

    // Start event stream connection
    connectEvents(port);
}

function updateStatusBar(connected) {
    isConnected = connected;
    if (connected) {
        statusBarItem.text = '$(pulse) Kite: Connected';
        statusBarItem.tooltip = 'Kite Architecture Canvas Bridge: Connected';
    } else {
        statusBarItem.text = '$(circle-slash) Kite: Standby';
        statusBarItem.tooltip = 'Kite Architecture Canvas Bridge: Searching for local Kite Station...';
    }
}

function notifyActiveFile(fullPath, line, port) {
    const workspaceFolders = vscode.workspace.workspaceFolders;
    let relativePath = fullPath;
    if (workspaceFolders && workspaceFolders.length > 0) {
        relativePath = path.relative(workspaceFolders[0].uri.fsPath, fullPath);
    }

    const payload = JSON.stringify({
        filePath: relativePath,
        line: line,
        editor: 'vscode'
    });

    const options = {
        hostname: '127.0.0.1',
        port: port,
        path: '/api/ide/active',
        method: 'POST',
        headers: {
            'Content-Type': 'application/json',
            'Content-Length': Buffer.byteLength(payload)
        },
        timeout: 1000
    };

    const req = http.request(options, (res) => {
        if (res.statusCode >= 200 && res.statusCode < 300) {
            if (!isConnected) updateStatusBar(true);
        }
    });

    req.on('error', () => {
        // Try fallback port if 24242 failed
        if (port === 24242) {
            notifyActiveFile(fullPath, line, 24243);
        } else if (isConnected) {
            updateStatusBar(false);
        }
    });

    req.write(payload);
    req.end();
}

function triggerSync(port) {
    const options = {
        hostname: '127.0.0.1',
        port: port,
        path: '/api/status',
        method: 'GET',
        timeout: 1500
    };

    const req = http.request(options, (res) => {
        if (res.statusCode === 200) {
            updateStatusBar(true);
            vscode.window.showInformationMessage('Kite Architecture Canvas synchronised successfully.');
        } else {
            vscode.window.showWarningMessage(`Kite station responded with status code ${res.statusCode}.`);
        }
    });

    req.on('error', () => {
        vscode.window.showErrorMessage('Unable to connect to Kite Station at 127.0.0.1:' + port);
    });

    req.end();
}

function connectEvents(port) {
    if (eventSourceReq) {
        eventSourceReq.destroy();
        eventSourceReq = null;
    }

    const options = {
        hostname: '127.0.0.1',
        port: port,
        path: '/api/events',
        method: 'GET',
        headers: {
            'Accept': 'text/event-stream',
            'Cache-Control': 'no-cache'
        }
    };

    eventSourceReq = http.request(options, (res) => {
        if (res.statusCode === 200) {
            updateStatusBar(true);
        }

        res.setEncoding('utf8');
        let buffer = '';

        res.on('data', (chunk) => {
            buffer += chunk;
            const lines = buffer.split('\n\n');
            buffer = lines.pop() || '';

            for (const block of lines) {
                for (const line of block.split('\n')) {
                    if (line.startsWith('data: ')) {
                        const jsonStr = line.substring(6).trim();
                        handleKiteEvent(jsonStr);
                    }
                }
            }
        });

        res.on('end', () => {
            updateStatusBar(false);
            scheduleReconnect(port);
        });
    });

    eventSourceReq.on('error', () => {
        updateStatusBar(false);
        scheduleReconnect(port);
    });

    eventSourceReq.end();
}

function scheduleReconnect(port) {
    if (reconnectTimer) clearTimeout(reconnectTimer);
    reconnectTimer = setTimeout(() => {
        connectEvents(port);
    }, 4000);
}

function handleKiteEvent(rawJson) {
    try {
        const event = JSON.parse(rawJson);
        if (event.type === 'ide_navigate' && event.payload) {
            const p = event.payload;
            const targetPath = p.filePath;
            const line = Math.max(0, (p.line || 1) - 1);
            const column = Math.max(0, (p.column || 1) - 1);

            let fileUri;
            if (path.isAbsolute(targetPath)) {
                fileUri = vscode.Uri.file(targetPath);
            } else {
                const root = vscode.workspace.workspaceFolders?.[0]?.uri.fsPath || '';
                fileUri = vscode.Uri.file(path.join(root, targetPath));
            }

            vscode.workspace.openTextDocument(fileUri).then((doc) => {
                vscode.window.showTextDocument(doc, {
                    selection: new vscode.Range(line, column, line, column),
                    preview: false
                });
            }, (err) => {
                vscode.window.showWarningMessage(`Kite: Could not open file ${targetPath}: ${err.message}`);
            });
        }
    } catch {
        // Ignore unparseable frames
    }
}

function deactivate() {
    if (eventSourceReq) eventSourceReq.destroy();
    if (reconnectTimer) clearTimeout(reconnectTimer);
}

module.exports = {
    activate,
    deactivate
};
