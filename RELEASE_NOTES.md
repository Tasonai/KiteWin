# Kite & KiteWin — Release Notes v1.0.0
**The Sovereign Studio for Ideas, Architecture, and Antigravity Skills**

Release Tag: `v1.0.0`  
Build Date: `2026-11-04`  
Architecture Standard: Apple HIG & BAMA (macOS) / Windows 11 & BAWA (Windows)  
Zero Emojis Standard: 100% System Glyphs (SF Symbols, Segoe Fluent Icons, Lucide Icons)

---

## 1. Executive Summary

Kite is a native, zero-latency desktop studio and idea banking system designed for engineers and AI agents. It connects macOS and Windows 11 through a peer-to-peer (P2P) local synchronization bridge, giving teams an offline-first workspace for capturing ideas, exploring design architectures on an infinite canvas, and commanding 355 specialized Antigravity engineering skills.

---

## 2. Key Modules & Capabilities

### 2.1. Dual-Platform Native Architecture
- **macOS (`Kite`)**:
  - Swift 6, SwiftUI, SwiftData, and AppKit.
  - Native 8pt grid with sub-250ms spring transitions.
  - Menu Bar daemon (`MenuBarExtra`) that remains active even when windows are closed.
- **Windows 11 (`KiteWin`)**:
  - WinUI 3, C# / .NET 10, SQLite WAL persistence.
  - Strict 4 epx layout grid, Mica Alt backdrop material.
  - Background tray daemon mode with seamless window lifecycle management.

### 2.2. Antigravity Skills & Plugins Engine (355 Skills & 18 Plugins)
- Comprehensive catalog covering Frontend, Cloud Infrastructure, Mobile, Accessibility, and Design Craft.
- All commands, activation triggers, and example prompts strictly formatted with slash syntax:
  - `/site-md` — Project constitution and screen mapping
  - `/design-md` — Semantic design system tokens in Markdown
  - `/emil-design-eng` — Physical spring micro-interactions and tactile feedback
  - `/taste-design` — Intentional typography and aesthetic curation
  - `/a11y-debugging` — WCAG AA compliance and focus state auditing
  - `/gsd-execute-phase` — Disciplined wave-based execution with atomic commits
- In-depth Russian language documentation for each skill detailing architectural missions, activation triggers, and deliverables.

### 2.3. Infinite Visual Canvas & Design Tokens Export
- Freeform canvas supporting Pan & Zoom (`60%` - `160%`).
- Draggable Idea nodes, Sticky notes (`#FEF08A`, `#BBF7D0`, `#DDD6FE`), and Reference cards (Mobbin, Figma).
- Curved Bezier connector lines with semantic relationship pills.
- One-click export directly to:
  - **Google Stitch**: Standard `DESIGN.md` blueprint.
  - **Figma**: W3C Design Tokens JSON format.

### 2.4. Global Quick Capture HUD (Global Hotkeys)
- **macOS**: `Cmd+Shift+K` (Carbon Events API) invokes a floating Spotlight-style panel.
- **Windows 11**: `Ctrl+Shift+K` (Win32 `RegisterHotKey` with window subclassing) displays an acrylic fast-entry popup.
- Accelerators: `Ctrl+Enter` / `Cmd+Enter` to commit, `Esc` to dismiss.

### 2.5. AI Architecture Engine (Gemini 2.5 & Local Ollama)
- **Google Gemini API**: Full integration with `gemini-2.5-flash` and `gemini-2.5-pro` via direct HTTP REST protocol.
- **Local Ollama**: Offline model support (`http://localhost:11434/api/generate`) with `llama3`, `mistral`, and `deepseek-r1`.
- **Offline Blueprint Compiler**: Fallback heuristic generator producing full architectural specifications without network access.

### 2.6. Agent CLI & MCP Server (`agent-cli` / MCP)
- Autonomous command line utility (`kite`) installed to `/usr/local/bin/kite`.
- Model Context Protocol (MCP) server running over standard input/output (`kite mcp`):
  - `kite_status` — Inspect running station status
  - `kite_list_ideas` — Query idea repository
  - `kite_add_idea` — Add ideas live into the running app
  - `kite_export_canvas` — Stream canvas graph and tokens
- Compatible with Claude Desktop, Cursor, Antigravity, and custom agent scripts.

---

## 3. P2P Local Synchronization Protocol

Kite features automatic bidirectional synchronization without external cloud dependencies:

| Endpoint | Method | Payload / Response | Description |
| :--- | :--- | :--- | :--- |
| `/api/status` | GET | `{"status":"online", "port": 24242}` | Station health check and capability probe |
| `/api/ideas` | GET | `{"version": 1, "ideas": [...]}` | Pulls all ideas and architectural metadata |
| `/api/ideas` | POST | `{"ideas": [...]}` | Atomic merge and deduplication of ideas |
| `/api/canvas` | GET | `{"frames": [...], "stickies": [...]}` | Exports infinite canvas graph snapshot |
| `/api/canvas` | POST | `{"frames": [...], "stickies": [...]}` | Imports and renders remote canvas nodes |

- macOS Station listens on TCP port `24242` (`Network.framework`).
- Windows 11 Station listens on TCP port `24243` (`HttpListener`).
- Web Studio connects directly to both via `kiteSyncBridge.ts`.

---

## 4. Packaging & Distribution Artifacts

### 4.1. macOS (`/Users/tasonai/Developer/Kite/dist`)
- **`Kite-1.0.0-macOS.dmg`**: Compressed Apple Disk Image (UDZO) with Drag-and-Drop symlink to `/Applications`.
- **`Kite-1.0.0-macOS.zip`**: Standalone release archive.

### 4.2. Windows 11 (`/Users/tasonai/Developer/KiteWindows/dist`)
- **`KiteWin-1.0.0-Setup.exe`**: Native PE32+ GUI x86-64 installer.
  - Zero admin rights required: installs to `%LOCALAPPDATA%\Programs\KiteWin`.
  - Creates Start Menu and Desktop shortcuts with `AppIcon.ico`.
  - Registers uninstaller in Windows Registry (`Uninstall\KiteWin`).
- **`KiteWin-1.0.0-win-x64.zip`**: Portable release archive.

---

## 5. Verification Checklist & Checksums

### SHA256 Checksums
```text
2e8ddd56f9066c96f1c49ac3e2a1bc9cffa29171173db72e354fc8f1baadbc5f  Kite-1.0.0-macOS.dmg
42def6372226e766207084d6ac3949b3b039d2ea1bfe51c13e5336e49f95b441  Kite-1.0.0-macOS.zip
ecf68dccabfdf55b33c0ffc991579f342814ad28c370f1b72c4874b74ac5e99d  KiteWin-1.0.0-Setup.exe
b15f287f1349df624f8a508800ff8c57acea24f32d97d86add276b0c9d9ec5ab  KiteWin-1.0.0-win-x64.zip
```

### Quality Assurance Verification
- In-App Self-Test (`./tools/selftest.sh`): **PASS (5/5 checks passed)**.
- Automated Emoji Audit: **0 violations detected across all .swift, .cs, .xaml files**.
- P2P Roundtrip Test: **HTTP 200 OK across :24242 and :24243**.
- Agent CLI Execution: **`kite status` & `kite list` confirmed operational**.
