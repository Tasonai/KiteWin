#!/usr/bin/env python3
"""
Kite Agent CLI & MCP Server
Provides command line automation and Model Context Protocol (MCP) server
for the Kite ecosystem (macOS SwiftData & Windows 11 WinUI 3).

Standard: BAMA / BAWA agent-cli specification.
Zero Emojis: Strictly clean typography and standard symbols.
"""

import sys
import os
import json
import urllib.request
import urllib.error
import argparse
from typing import Any, Dict, List, Optional

MAC_ENDPOINT = "http://127.0.0.1:24242"
WIN_ENDPOINT = "http://127.0.0.1:24243"

def get_active_endpoint() -> str:
    # Try Mac first, fallback to Windows
    for ep in [MAC_ENDPOINT, WIN_ENDPOINT]:
        try:
            req = urllib.request.Request(f"{ep}/api/status", headers={"Accept": "application/json"})
            with urllib.request.urlopen(req, timeout=1.0) as resp:
                if resp.status == 200:
                    return ep
        except Exception:
            continue
    return MAC_ENDPOINT

def api_get(endpoint: str, path: str) -> Dict[str, Any]:
    url = f"{endpoint}{path}"
    req = urllib.request.Request(url, headers={"Accept": "application/json"})
    try:
        with urllib.request.urlopen(req, timeout=3.0) as resp:
            data = resp.read()
            return json.loads(data.decode("utf-8"))
    except urllib.error.URLError as e:
        sys.stderr.write(f"Error connecting to Kite ({url}): {e}\n")
        sys.stderr.write("Make sure Kite (macOS) or KiteWin (Windows) is running.\n")
        sys.exit(1)

def api_post(endpoint: str, path: str, payload: Dict[str, Any]) -> Dict[str, Any]:
    url = f"{endpoint}{path}"
    body = json.dumps(payload).encode("utf-8")
    req = urllib.request.Request(url, data=body, headers={"Content-Type": "application/json", "Accept": "application/json"}, method="POST")
    try:
        with urllib.request.urlopen(req, timeout=5.0) as resp:
            data = resp.read()
            return json.loads(data.decode("utf-8"))
    except urllib.error.URLError as e:
        sys.stderr.write(f"Error posting to Kite ({url}): {e}\n")
        sys.exit(1)

# ==================== CLI COMMANDS ====================

def cmd_status(args: argparse.Namespace) -> None:
    ep = get_active_endpoint()
    status_data = api_get(ep, "/api/status")
    ideas_data = api_get(ep, "/api/ideas")
    canvas_data = api_get(ep, "/api/canvas")

    ideas = ideas_data.get("ideas", [])
    frames = canvas_data.get("frames", [])
    stickies = canvas_data.get("stickies", [])

    print(f"Station:        {status_data.get('app', 'Kite')}")
    print(f"Status:         {status_data.get('status', 'online').upper()}")
    print(f"Endpoint:       {ep}")
    print(f"Version:        {status_data.get('version', '1.0')}")
    print(f"Total Ideas:    {len(ideas)}")
    print(f"Canvas Frames:  {len(frames)}")
    print(f"Canvas Stickies:{len(stickies)}")

def cmd_list(args: argparse.Namespace) -> None:
    ep = get_active_endpoint()
    data = api_get(ep, "/api/ideas")
    ideas = data.get("ideas", [])

    if args.status:
        ideas = [i for i in ideas if i.get("status") == args.status]
    if args.platform:
        ideas = [i for i in ideas if i.get("platform", "").lower() == args.platform.lower()]
    if args.starred:
        ideas = [i for i in ideas if i.get("isStarred") is True]

    if args.json:
        print(json.dumps(ideas, indent=2, ensure_ascii=False))
        return

    if not ideas:
        print("No ideas found matching criteria.")
        return

    print(f"{'STAR':<5} {'ID':<38} {'STATUS':<12} {'PLATFORM':<10} {'TITLE'}")
    print("-" * 80)
    for i in ideas:
        star = "[*]" if i.get("isStarred") else "   "
        iid = i.get("id", "")
        status = i.get("status", "backlog")
        platform = i.get("platform", "web").capitalize()
        title = i.get("title", "Untitled")
        print(f"{star:<5} {iid:<38} {status:<12} {platform:<10} {title}")

def cmd_add(args: argparse.Namespace) -> None:
    ep = get_active_endpoint()
    new_idea = {
        "title": args.title,
        "description": args.concept or "",
        "platform": args.platform or "web",
        "preferredStyle": args.style or "Editorial & Swiss Modern",
        "status": args.status or "backlog",
        "isStarred": args.starred or False,
        "tags": [t.strip() for t in args.tags.split(",")] if args.tags else []
    }

    res = api_post(ep, "/api/ideas", {"ideas": [new_idea]})
    print(f"Added idea: \"{args.title}\" (Imported: {res.get('imported', 1)})")

def cmd_show(args: argparse.Namespace) -> None:
    ep = get_active_endpoint()
    data = api_get(ep, "/api/ideas")
    ideas = data.get("ideas", [])

    query = args.query.lower()
    matched = [i for i in ideas if query in i.get("id", "").lower() or query in i.get("title", "").lower()]

    if not matched:
        print(f"Idea not found matching '{args.query}'.")
        return

    item = matched[0]
    print(f"Title:       {item.get('title')}")
    print(f"ID:          {item.get('id')}")
    print(f"Platform:    {item.get('platform')}")
    print(f"Style:       {item.get('preferredStyle')}")
    print(f"Status:      {item.get('status')}")
    print(f"Starred:     {item.get('isStarred')}")
    print(f"Tags:        {', '.join(item.get('tags', []))}")
    print(f"Created:     {item.get('createdAt')}")
    print("\nConcept:")
    print(item.get("description") or "(none)")

def cmd_canvas_export(args: argparse.Namespace) -> None:
    ep = get_active_endpoint()
    data = api_get(ep, "/api/canvas")

    if args.format == "json":
        print(json.dumps(data, indent=2, ensure_ascii=False))
    elif args.format == "stitch":
        print("# Google Stitch — DESIGN.md")
        print("\n## Canvas Architecture Blueprint")
        print(f"- Exported At: {data.get('exportedAt', '')}")
        print(f"- Total Frames: {len(data.get('frames', []))}")
        print(f"- Total Stickies: {len(data.get('stickies', []))}")
        print("\n### Frames:")
        for f in data.get("frames", []):
            print(f"- {f.get('title')} ({f.get('width')}x{f.get('height')})")
        print("\n### Stickies & Architectural Directives:")
        for s in data.get("stickies", []):
            print(f"- {s.get('text')}")
    else:
        print(json.dumps(data, indent=2, ensure_ascii=False))

# ==================== MCP SERVER (JSON-RPC) ====================

def run_mcp_server() -> None:
    """
    Standard Model Context Protocol (MCP) server running over stdio.
    Exposes Kite tools to Claude Desktop, Cursor, Antigravity, and other MCP clients.
    """
    def send_response(response: Dict[str, Any]) -> None:
        raw = json.dumps(response, ensure_ascii=False)
        sys.stdout.write(raw + "\n")
        sys.stdout.flush()

    while True:
        try:
            line = sys.stdin.readline()
            if not line:
                break
            line = line.strip()
            if not line:
                continue

            msg = json.loads(line)
            msg_id = msg.get("id")
            method = msg.get("method")
            params = msg.get("params", {})

            if method == "initialize":
                send_response({
                    "jsonrpc": "2.0",
                    "id": msg_id,
                    "result": {
                        "protocolVersion": "2024-11-05",
                        "serverInfo": {
                            "name": "kite-mcp-server",
                            "version": "1.0.0"
                        },
                        "capabilities": {
                            "tools": {}
                        }
                    }
                })

            elif method == "notifications/initialized":
                # Client acknowledges init
                continue

            elif method == "tools/list":
                send_response({
                    "jsonrpc": "2.0",
                    "id": msg_id,
                    "result": {
                        "tools": [
                            {
                                "name": "kite_status",
                                "description": "Returns status, active endpoint, version, and item counts of the running Kite app.",
                                "inputSchema": {
                                    "type": "object",
                                    "properties": {}
                                }
                            },
                            {
                                "name": "kite_list_ideas",
                                "description": "Lists ideas and projects stored in the local Kite SwiftData/SQLite bank.",
                                "inputSchema": {
                                    "type": "object",
                                    "properties": {
                                        "status": {"type": "string", "enum": ["backlog", "in_progress", "completed"]},
                                        "platform": {"type": "string", "enum": ["web", "mobile", "desktop"]},
                                        "starredOnly": {"type": "boolean"}
                                    }
                                }
                            },
                            {
                                "name": "kite_add_idea",
                                "description": "Creates a new project idea or architecture blueprint in the running Kite app live.",
                                "inputSchema": {
                                    "type": "object",
                                    "properties": {
                                        "title": {"type": "string", "description": "Project title"},
                                        "concept": {"type": "string", "description": "JTBD scenario and requirements"},
                                        "platform": {"type": "string", "enum": ["web", "mobile", "desktop"], "default": "web"},
                                        "style": {"type": "string", "description": "Design system preset", "default": "Editorial & Swiss Modern"},
                                        "starred": {"type": "boolean", "default": false}
                                    },
                                    "required": ["title"]
                                }
                            },
                            {
                                "name": "kite_export_canvas",
                                "description": "Exports the full interactive canvas graph including frames, stickies, and architecture nodes.",
                                "inputSchema": {
                                    "type": "object",
                                    "properties": {
                                        "format": {"type": "string", "enum": ["stitch", "json"], "default": "stitch"}
                                    }
                                }
                            }
                        ]
                    }
                })

            elif method == "tools/call":
                tool_name = params.get("name")
                args = params.get("arguments", {})
                ep = get_active_endpoint()

                if tool_name == "kite_status":
                    status = api_get(ep, "/api/status")
                    ideas = api_get(ep, "/api/ideas").get("ideas", [])
                    canvas = api_get(ep, "/api/canvas")
                    text = f"Kite Station: {status.get('app')}\nEndpoint: {ep}\nIdeas: {len(ideas)}\nFrames: {len(canvas.get('frames', []))}\nStickies: {len(canvas.get('stickies', []))}"
                    send_response({
                        "jsonrpc": "2.0",
                        "id": msg_id,
                        "result": {
                            "content": [{"type": "text", "text": text}]
                        }
                    })

                elif tool_name == "kite_list_ideas":
                    data = api_get(ep, "/api/ideas")
                    ideas = data.get("ideas", [])
                    if args.get("status"):
                        ideas = [i for i in ideas if i.get("status") == args["status"]]
                    if args.get("platform"):
                        ideas = [i for i in ideas if i.get("platform", "").lower() == args["platform"].lower()]
                    if args.get("starredOnly"):
                        ideas = [i for i in ideas if i.get("isStarred") is True]

                    send_response({
                        "jsonrpc": "2.0",
                        "id": msg_id,
                        "result": {
                            "content": [{"type": "text", "text": json.dumps(ideas, indent=2, ensure_ascii=False)}]
                        }
                    })

                elif tool_name == "kite_add_idea":
                    new_idea = {
                        "title": args.get("title"),
                        "description": args.get("concept", ""),
                        "platform": args.get("platform", "web"),
                        "preferredStyle": args.get("style", "Editorial & Swiss Modern"),
                        "status": "backlog",
                        "isStarred": args.get("starred", False),
                        "tags": ["mcp", "agent-cli"]
                    }
                    res = api_post(ep, "/api/ideas", {"ideas": [new_idea]})
                    send_response({
                        "jsonrpc": "2.0",
                        "id": msg_id,
                        "result": {
                            "content": [{"type": "text", "text": f"Successfully created idea '{args.get('title')}' in Kite (Imported: {res.get('imported', 1)})."}]
                        }
                    })

                elif tool_name == "kite_export_canvas":
                    canvas = api_get(ep, "/api/canvas")
                    fmt = args.get("format", "stitch")
                    if fmt == "stitch":
                        lines = [
                            "# Google Stitch — DESIGN.md",
                            f"Exported At: {canvas.get('exportedAt', '')}",
                            f"Frames: {len(canvas.get('frames', []))}",
                            f"Stickies: {len(canvas.get('stickies', []))}\n",
                            "Directives:"
                        ]
                        for s in canvas.get("stickies", []):
                            lines.append(f"- {s.get('text')}")
                        text = "\n".join(lines)
                    else:
                        text = json.dumps(canvas, indent=2, ensure_ascii=False)

                    send_response({
                        "jsonrpc": "2.0",
                        "id": msg_id,
                        "result": {
                            "content": [{"type": "text", "text": text}]
                        }
                    })

                else:
                    send_response({
                        "jsonrpc": "2.0",
                        "id": msg_id,
                        "error": {
                            "code": -32601,
                            "message": f"Method {tool_name} not found"
                        }
                    })

            elif msg_id is not None:
                send_response({
                    "jsonrpc": "2.0",
                    "id": msg_id,
                    "error": {
                        "code": -32601,
                        "message": f"Unrecognized method {method}"
                    }
                })

        except Exception as ex:
            sys.stderr.write(f"MCP Server error: {ex}\n")

# ==================== MAIN DISPATCHER ====================

def main() -> None:
    parser = argparse.ArgumentParser(
        prog="kite",
        description="Kite Agent CLI & MCP Server — drives the running Kite macOS / Windows app live."
    )
    subparsers = parser.add_subparsers(dest="subcommand", help="Subcommand to execute")

    # status
    subparsers.add_parser("status", help="Show connection status and statistics")

    # list
    p_list = subparsers.add_parser("list", help="List project ideas")
    p_list.add_argument("--status", choices=["backlog", "in_progress", "completed"], help="Filter by status")
    p_list.add_argument("--platform", choices=["web", "mobile", "desktop"], help="Filter by platform")
    p_list.add_argument("--starred", action="store_true", help="Only starred items")
    p_list.add_argument("--json", action="store_true", help="Output raw JSON")

    # add
    p_add = subparsers.add_parser("add", help="Add a new project idea")
    p_add.add_argument("title", help="Title of the project")
    p_add.add_argument("--concept", help="JTBD concept and user scenario")
    p_add.add_argument("--platform", choices=["web", "mobile", "desktop"], default="web", help="Platform")
    p_add.add_argument("--style", default="Editorial & Swiss Modern", help="Design system preset")
    p_add.add_argument("--status", choices=["backlog", "in_progress", "completed"], default="backlog", help="Status")
    p_add.add_argument("--tags", help="Comma-separated tags")
    p_add.add_argument("--starred", action="store_true", help="Mark as starred")

    # show
    p_show = subparsers.add_parser("show", help="Show details of an idea")
    p_show.add_argument("query", help="ID or title substring to find")

    # canvas
    p_canvas = subparsers.add_parser("canvas", help="Export canvas layout and tokens")
    p_canvas.add_argument("--format", choices=["stitch", "json"], default="stitch", help="Output format")

    # mcp
    subparsers.add_parser("mcp", help="Run MCP server over stdio for Claude Desktop / Cursor / Antigravity")

    args = parser.parse_args()

    if args.subcommand == "status":
        cmd_status(args)
    elif args.subcommand == "list":
        cmd_list(args)
    elif args.subcommand == "add":
        cmd_add(args)
    elif args.subcommand == "show":
        cmd_show(args)
    elif args.subcommand == "canvas":
        cmd_canvas_export(args)
    elif args.subcommand == "mcp":
        run_mcp_server()
    else:
        parser.print_help()

if __name__ == "__main__":
    main()
