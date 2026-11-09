# Kite Architecture Bridge for VS Code and Cursor

Bidirectional companion extension connecting Visual Studio Code and Cursor to the Kite Architecture Canvas Station.

## Features

- **Active File Highlighting:** Automatically highlights the corresponding architecture frame/node on the Kite infinite canvas whenever you switch active editor tabs.
- **Canvas-to-Code Navigation:** Clicking a node in the Kite desktop app immediately opens the corresponding file, line, and column in your editor.
- **Zero Runtime Dependencies:** Built strictly using native Node.js APIs without external npm package overhead.
- **Cross-Platform:** Connects seamlessly to Kite on macOS (port 24242) and Kite on Windows (port 24243).

## Commands

- `Kite: Focus Node in Architecture Canvas`: Sends active editor file path to Kite.
- `Kite: Sync Architecture Canvas`: Verifies connection and synchronises project topology.
- `Kite: Open Canvas Studio`: Launches the interactive local canvas in your default browser.

## Configuration

- `kite.port`: Local Kite Station port (default: 24242).
- `kite.autoFocus`: Automatically focus architecture node on active editor change (default: true).

## License

MIT
