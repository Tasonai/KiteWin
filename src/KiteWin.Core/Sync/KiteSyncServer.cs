using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using KiteWin.Core.Models;

namespace KiteWin.Core.Sync;

/// <summary>
/// Embedded HTTP Server for Windows that serves Kite REST API on port 24243.
/// Allows Kite macOS, Web Studio, and other agents to query and sync ideas over LAN.
/// </summary>
public sealed class KiteSyncServer : IDisposable
{
    private HttpListener? _listener;
    private CancellationTokenSource? _cts;
    private Task? _listenTask;
    private readonly ItemStore _store;
    private readonly CanvasStore? _canvasStore;
    private readonly List<HttpListenerResponse> _sseClients = [];
    private readonly object _sseLock = new();

    public int Port { get; private set; }
    public bool IsRunning => _listener?.IsListening == true;

    public KiteSyncServer(ItemStore store, CanvasStore? canvasStore = null, int port = 24243)
    {
        _store = store;
        _canvasStore = canvasStore;
        Port = port;
    }

    public void Start()
    {
        if (IsRunning) return;

        try
        {
            _listener = new HttpListener();
            _listener.Prefixes.Add($"http://*:{Port}/");
            _listener.Start();

            _cts = new CancellationTokenSource();
            _listenTask = Task.Run(() => ListenLoopAsync(_cts.Token));
            KiteLANBeacon.Shared.Start(Port, "Kite Windows", "Windows");
        }
        catch (HttpListenerException)
        {
            // Port might be restricted without admin; fallback to localhost
            try
            {
                _listener = new HttpListener();
                _listener.Prefixes.Add($"http://localhost:{Port}/");
                _listener.Prefixes.Add($"http://127.0.0.1:{Port}/");
                _listener.Start();

                _cts = new CancellationTokenSource();
                _listenTask = Task.Run(() => ListenLoopAsync(_cts.Token));
                KiteLANBeacon.Shared.Start(Port, "Kite Windows", "Windows");
            }
            catch
            {
                // Fallback failed
                _listener = null;
            }
        }
    }

    public void Stop()
    {
        if (!IsRunning) return;
        KiteLANBeacon.Shared.Stop();

        try
        {
            lock (_sseLock)
            {
                foreach (var client in _sseClients)
                {
                    try { client.Close(); } catch { }
                }
                _sseClients.Clear();
            }

            _cts?.Cancel();
            _listener?.Stop();
            _listener?.Close();
        }
        catch
        {
            // Ignore shutdown errors
        }
        finally
        {
            _listener = null;
            _cts = null;
        }
    }

    public void Broadcast(string eventType, object? payload = null)
    {
        var json = JsonSerializer.Serialize(new { type = eventType, payload });
        var msg = $"event: message\ndata: {json}\n\n";
        var bytes = Encoding.UTF8.GetBytes(msg);

        lock (_sseLock)
        {
            for (var i = _sseClients.Count - 1; i >= 0; i--)
            {
                var client = _sseClients[i];
                try
                {
                    client.OutputStream.Write(bytes);
                    client.OutputStream.Flush();
                }
                catch
                {
                    _sseClients.RemoveAt(i);
                }
            }
        }
    }

    private async Task ListenLoopAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested && _listener?.IsListening == true)
        {
            try
            {
                var context = await _listener.GetContextAsync();
                _ = Task.Run(() => HandleRequestAsync(context), token);
            }
            catch (HttpListenerException)
            {
                break;
            }
            catch (ObjectDisposedException)
            {
                break;
            }
            catch
            {
                // Ignore transient errors
            }
        }
    }

    private async Task HandleRequestAsync(HttpListenerContext context)
    {
        var req = context.Request;
        var res = context.Response;

        // Apply CORS headers for Web Studio / Cross-origin callers
        res.Headers.Add("Access-Control-Allow-Origin", "*");
        res.Headers.Add("Access-Control-Allow-Methods", "GET, POST, OPTIONS");
        res.Headers.Add("Access-Control-Allow-Headers", "Content-Type, Accept");

        if (req.HttpMethod.Equals("OPTIONS", StringComparison.OrdinalIgnoreCase))
        {
            res.StatusCode = (int)HttpStatusCode.NoContent;
            res.Close();
            return;
        }

        try
        {
            var path = req.Url?.AbsolutePath?.ToLowerInvariant() ?? "";

            switch (path)
            {
                case "" or "/" or "/index.html":
                    var html = WebStudio.GetHtml(Port, "Kite Windows (BAWA)");
                    res.ContentType = "text/html; charset=utf-8";
                    res.StatusCode = (int)HttpStatusCode.OK;
                    var htmlBytes = Encoding.UTF8.GetBytes(html);
                    await res.OutputStream.WriteAsync(htmlBytes);
                    res.Close();
                    break;

                case "/api/events":
                    res.ContentType = "text/event-stream; charset=utf-8";
                    res.Headers.Add("Cache-Control", "no-cache");
                    res.Headers.Add("Connection", "keep-alive");
                    res.StatusCode = (int)HttpStatusCode.OK;
                    lock (_sseLock) { _sseClients.Add(res); }
                    var welcomeBytes = Encoding.UTF8.GetBytes($"data: {{\"type\":\"connected\",\"app\":\"Kite Windows\",\"port\":{Port}}}\n\n");
                    await res.OutputStream.WriteAsync(welcomeBytes);
                    await res.OutputStream.FlushAsync();
                    return;

                case "/api/status":
                    var status = new KiteStatusResponse
                    {
                        Status = "online",
                        App = "Kite Windows (BAWA)",
                        Version = "1.0",
                        Port = Port
                    };
                    await WriteJsonResponseAsync(res, status);
                    break;

                case "/api/peers":
                    var peers = KiteLANBeacon.Shared.ActivePeers;
                    await WriteJsonResponseAsync(res, peers);
                    break;

                case "/api/ideas":
                    if (req.HttpMethod.Equals("GET", StringComparison.OrdinalIgnoreCase))
                    {
                        var items = _store.All();
                        var payload = new KiteIdeasPayload
                        {
                            Version = 1,
                            ExportedAt = DateTimeOffset.Now.ToString("O", CultureInfo.InvariantCulture),
                            Ideas = items.Select(KiteSyncClient.ItemToDto).ToList()
                        };
                        await WriteJsonResponseAsync(res, payload);
                    }
                    else if (req.HttpMethod.Equals("POST", StringComparison.OrdinalIgnoreCase))
                    {
                        using var reader = new StreamReader(req.InputStream, req.ContentEncoding ?? Encoding.UTF8);
                        var json = await reader.ReadToEndAsync();
                        var payload = JsonSerializer.Deserialize<KiteIdeasPayload>(json, new JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true
                        });

                        var count = 0;
                        if (payload?.Ideas != null)
                        {
                            foreach (var dto in payload.Ideas)
                            {
                                var item = KiteSyncClient.DtoToItem(dto);
                                _store.Save(item);
                                count++;
                            }
                        }

                        var responseObj = new { status = "ok", imported = count };
                        Broadcast("ideas_updated", new { imported = count });
                        await WriteJsonResponseAsync(res, responseObj);
                    }
                    else
                    {
                        res.StatusCode = (int)HttpStatusCode.MethodNotAllowed;
                        res.Close();
                    }
                    break;

                case "/api/canvas":
                    if (req.HttpMethod.Equals("GET", StringComparison.OrdinalIgnoreCase))
                    {
                        var json = _canvasStore?.GetSnapshotJson() ?? "{}";
                        res.ContentType = "application/json; charset=utf-8";
                        res.StatusCode = (int)HttpStatusCode.OK;
                        var bytes = Encoding.UTF8.GetBytes(json);
                        await res.OutputStream.WriteAsync(bytes);
                        res.Close();
                    }
                    else if (req.HttpMethod.Equals("POST", StringComparison.OrdinalIgnoreCase))
                    {
                        using var reader = new StreamReader(req.InputStream, req.ContentEncoding ?? Encoding.UTF8);
                        var json = await reader.ReadToEndAsync();
                        var success = _canvasStore?.ImportSnapshot(json) ?? false;
                        if (success) Broadcast("canvas_updated", new { success = true });
                        var responseObj = new { status = success ? "ok" : "error" };
                        await WriteJsonResponseAsync(res, responseObj);
                    }
                    else
                    {
                        res.StatusCode = (int)HttpStatusCode.MethodNotAllowed;
                        res.Close();
                    }
                    break;

                case "/api/ide/active":
                    if (req.HttpMethod.Equals("POST", StringComparison.OrdinalIgnoreCase))
                    {
                        using var reader = new StreamReader(req.InputStream, req.ContentEncoding ?? Encoding.UTF8);
                        var json = await reader.ReadToEndAsync();
                        using var doc = JsonDocument.Parse(json);
                        var root = doc.RootElement;
                        var filePath = root.TryGetProperty("filePath", out var fp) ? fp.GetString() ?? "" : "";
                        int? line = root.TryGetProperty("line", out var l) ? l.GetInt32() : null;
                        var editor = root.TryGetProperty("editor", out var ed) ? ed.GetString() ?? "ide" : "ide";

                        IDEBridgeHub.Shared.UpdateActiveFile(filePath, line);
                        Broadcast("ide_active_file", new { filePath, line = line ?? 0, editor });
                        await WriteJsonResponseAsync(res, new { status = "ok", filePath });
                    }
                    else
                    {
                        res.StatusCode = (int)HttpStatusCode.MethodNotAllowed;
                        res.Close();
                    }
                    break;

                case "/api/ide/navigate":
                    if (req.HttpMethod.Equals("POST", StringComparison.OrdinalIgnoreCase))
                    {
                        using var reader = new StreamReader(req.InputStream, req.ContentEncoding ?? Encoding.UTF8);
                        var json = await reader.ReadToEndAsync();
                        using var doc = JsonDocument.Parse(json);
                        var root = doc.RootElement;
                        var filePath = root.TryGetProperty("filePath", out var fp) ? fp.GetString() ?? "" : "";
                        var line = root.TryGetProperty("line", out var l) ? l.GetInt32() : 1;
                        var column = root.TryGetProperty("column", out var c) ? c.GetInt32() : 1;
                        var editor = root.TryGetProperty("editor", out var ed) ? ed.GetString() ?? "vscode" : "vscode";

                        Broadcast("ide_navigate", new { filePath, line, column, editor });
                        await WriteJsonResponseAsync(res, new { status = "ok", navigated = true });
                    }
                    else
                    {
                        res.StatusCode = (int)HttpStatusCode.MethodNotAllowed;
                        res.Close();
                    }
                    break;

                case "/api/ide/status":
                    var (activePath, activeLine) = IDEBridgeHub.Shared.CurrentActiveFile;
                    await WriteJsonResponseAsync(res, new
                    {
                        status = "online",
                        activeFilePath = activePath ?? "",
                        activeLine = activeLine ?? 0
                    });
                    break;

                case "/api/telemetry":
                    if (req.HttpMethod.Equals("GET", StringComparison.OrdinalIgnoreCase))
                    {
                        var all = IDEBridgeHub.Shared.AllTelemetries();
                        await WriteJsonResponseAsync(res, all);
                    }
                    else if (req.HttpMethod.Equals("POST", StringComparison.OrdinalIgnoreCase))
                    {
                        using var reader = new StreamReader(req.InputStream, req.ContentEncoding ?? Encoding.UTF8);
                        var json = await reader.ReadToEndAsync();
                        var telemetry = JsonSerializer.Deserialize<NodeTelemetry>(json, new JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true
                        });

                        if (telemetry != null && !string.IsNullOrEmpty(telemetry.NodeId))
                        {
                            IDEBridgeHub.Shared.SetTelemetry(telemetry);
                            Broadcast("telemetry_updated", new
                            {
                                nodeId = telemetry.NodeId,
                                status = telemetry.Status,
                                rps = telemetry.Rps,
                                p99LatencyMs = telemetry.P99LatencyMs,
                                errorRate = telemetry.ErrorRate
                            });
                            await WriteJsonResponseAsync(res, telemetry);
                        }
                        else
                        {
                            res.StatusCode = (int)HttpStatusCode.BadRequest;
                            res.Close();
                        }
                    }
                    else
                    {
                        res.StatusCode = (int)HttpStatusCode.MethodNotAllowed;
                        res.Close();
                    }
                    break;

                case "/api/relay/create":
                    if (req.HttpMethod.Equals("POST", StringComparison.OrdinalIgnoreCase))
                    {
                        var roomToken = RoomSecurityKeyDeriver.GenerateRoomToken();
                        var resPayload = new
                        {
                            status = "ok",
                            roomId = roomToken,
                            joinUrl = $"kite://join?room={roomToken}",
                            isEncrypted = true
                        };
                        await WriteJsonResponseAsync(res, resPayload);
                    }
                    else
                    {
                        res.StatusCode = (int)HttpStatusCode.MethodNotAllowed;
                        res.Close();
                    }
                    break;

                case "/api/relay/status":
                    var stats = WebRTCMeshManager.Shared.Statistics();
                    var meshPeers = WebRTCMeshManager.Shared.ActivePeers();
                    await WriteJsonResponseAsync(res, new
                    {
                        stats,
                        peers = meshPeers,
                        isOnline = true
                    });
                    break;

                case "/api/relay/signal":
                    if (req.HttpMethod.Equals("POST", StringComparison.OrdinalIgnoreCase))
                    {
                        using var reader = new StreamReader(req.InputStream, req.ContentEncoding ?? Encoding.UTF8);
                        var json = await reader.ReadToEndAsync();
                        var sigMsg = JsonSerializer.Deserialize<SignalingMessage>(json, new JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true
                        });
                        if (sigMsg != null)
                        {
                            RelayStationHub.Shared.RouteMessage(sigMsg);
                            Broadcast("relay_signal_routed", new { type = sigMsg.Type, roomId = sigMsg.RoomId, sender = sigMsg.SenderPeerId });
                            await WriteJsonResponseAsync(res, new { status = "ok", routed = true });
                        }
                        else
                        {
                            res.StatusCode = (int)HttpStatusCode.BadRequest;
                            res.Close();
                        }
                    }
                    else
                    {
                        res.StatusCode = (int)HttpStatusCode.MethodNotAllowed;
                        res.Close();
                    }
                    break;

                case string s when s.StartsWith("/api/relay/messages"):
                    var queryPeerId = req.QueryString["peerId"] ?? "";
                    var msgs = RelayStationHub.Shared.PopMessages(queryPeerId);
                    await WriteJsonResponseAsync(res, msgs);
                    break;

                case "/api/plugins":
                    var plugins = WasmPluginEngine.Shared.AllPlugins();
                    await WriteJsonResponseAsync(res, plugins);
                    break;

                case "/api/plugins/execute":
                    if (req.HttpMethod.Equals("POST", StringComparison.OrdinalIgnoreCase))
                    {
                        using var reader = new StreamReader(req.InputStream, req.ContentEncoding ?? Encoding.UTF8);
                        var json = await reader.ReadToEndAsync();
                        using var doc = JsonDocument.Parse(json);
                        var root = doc.RootElement;
                        var pluginId = root.TryGetProperty("pluginId", out var pId) ? pId.GetString() ?? "" : "";
                        var context = new Dictionary<string, object>();
                        if (root.TryGetProperty("context", out var ctxElem) && ctxElem.ValueKind == JsonValueKind.Object)
                        {
                            foreach (var prop in ctxElem.EnumerateObject())
                            {
                                if (prop.Value.ValueKind == JsonValueKind.String)
                                    context[prop.Name] = prop.Value.GetString() ?? "";
                                else if (prop.Value.ValueKind == JsonValueKind.Object)
                                {
                                    var inner = new Dictionary<string, string>();
                                    foreach (var sub in prop.Value.EnumerateObject())
                                        inner[sub.Name] = sub.Value.GetString() ?? "";
                                    context[prop.Name] = inner;
                                }
                            }
                        }
                        var execResult = WasmPluginEngine.Shared.ExecutePlugin(pluginId, context);
                        await WriteJsonResponseAsync(res, execResult);
                    }
                    else
                    {
                        res.StatusCode = (int)HttpStatusCode.MethodNotAllowed;
                        res.Close();
                    }
                    break;

                case "/api/worktree/status":
                    var session = WorktreeSyncEngine.Instance.GetSession();
                    await WriteJsonResponseAsync(res, session ?? new WorktreeSession());
                    break;

                case "/api/worktree/sync":
                    if (req.HttpMethod.Equals("POST", StringComparison.OrdinalIgnoreCase))
                    {
                        string? branch = null;
                        var defaultNodes = new List<(string Id, string Name, string Type)>
                        {
                            ("node-1", "Orders API", "api-service"),
                            ("node-2", "Orders DB", "postgres-database"),
                            ("node-3", "Order Events", "kafka-stream")
                        };
                        try
                        {
                            using var reader = new StreamReader(req.InputStream, req.ContentEncoding ?? Encoding.UTF8);
                            var json = await reader.ReadToEndAsync();
                            if (!string.IsNullOrWhiteSpace(json))
                            {
                                using var doc = JsonDocument.Parse(json);
                                if (doc.RootElement.TryGetProperty("branch", out var bProp))
                                {
                                    branch = bProp.GetString();
                                }
                            }
                        }
                        catch { }

                        var syncRes = WorktreeSyncEngine.Instance.ExecuteSync(branchName: branch, nodes: defaultNodes);
                        await WriteJsonResponseAsync(res, syncRes);
                    }
                    else
                    {
                        res.StatusCode = (int)HttpStatusCode.MethodNotAllowed;
                        res.Close();
                    }
                    break;

                case "/api/worktree/pr":
                    if (req.HttpMethod.Equals("POST", StringComparison.OrdinalIgnoreCase))
                    {
                        string? customTitle = null;
                        try
                        {
                            using var reader = new StreamReader(req.InputStream, req.ContentEncoding ?? Encoding.UTF8);
                            var json = await reader.ReadToEndAsync();
                            if (!string.IsNullOrWhiteSpace(json))
                            {
                                using var doc = JsonDocument.Parse(json);
                                if (doc.RootElement.TryGetProperty("title", out var tProp))
                                {
                                    customTitle = tProp.GetString();
                                }
                            }
                        }
                        catch { }

                        var prRes = WorktreeSyncEngine.Instance.SynthesizePullRequest(customTitle: customTitle);
                        await WriteJsonResponseAsync(res, prRes);
                    }
                    else
                    {
                        res.StatusCode = (int)HttpStatusCode.MethodNotAllowed;
                        res.Close();
                    }
                    break;

                default:
                    res.StatusCode = (int)HttpStatusCode.NotFound;
                    res.Close();
                    break;
            }
        }
        catch (Exception ex)
        {
            res.StatusCode = (int)HttpStatusCode.InternalServerError;
            var errBytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { error = ex.Message }));
            res.ContentType = "application/json; charset=utf-8";
            await res.OutputStream.WriteAsync(errBytes);
            res.Close();
        }
    }

    private static async Task WriteJsonResponseAsync<T>(HttpListenerResponse res, T obj)
    {
        res.ContentType = "application/json; charset=utf-8";
        res.StatusCode = (int)HttpStatusCode.OK;
        var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(obj, new JsonSerializerOptions
        {
            WriteIndented = true
        }));
        await res.OutputStream.WriteAsync(bytes);
        res.Close();
    }

    public void Dispose()
    {
        Stop();
    }
}
