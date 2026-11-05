using System.Text;
using System.Security;

namespace KiteWin.Core.Models;

/// <summary>
/// Exports the Infinite Canvas to vector SVG, interactive HTML with pan/zoom, and Mermaid diagrams.
/// Zero emojis.
/// </summary>
public static class CanvasExporter
{
    public static string ExportSvg(CanvasGraphSnapshot snapshot, IReadOnlyList<Item> items)
    {
        var minX = double.MaxValue;
        var minY = double.MaxValue;
        var maxX = double.MinValue;
        var maxY = double.MinValue;

        foreach (var frame in snapshot.Frames)
        {
            minX = Math.Min(minX, frame.X);
            minY = Math.Min(minY, frame.Y);
            maxX = Math.Max(maxX, frame.X + frame.Width);
            maxY = Math.Max(maxY, frame.Y + frame.Height);
        }

        foreach (var sticky in snapshot.Stickies)
        {
            minX = Math.Min(minX, sticky.X - 100);
            minY = Math.Min(minY, sticky.Y - 80);
            maxX = Math.Max(maxX, sticky.X + 100);
            maxY = Math.Max(maxY, sticky.Y + 80);
        }

        for (var i = 0; i < items.Count; i++)
        {
            var posX = 160.0 + (i % 3) * 320.0;
            var posY = 160.0 + (i / 3) * 240.0;
            if (snapshot.ItemPositions.TryGetValue(items[i].Id, out var pt))
            {
                posX = pt.X;
                posY = pt.Y;
            }
            minX = Math.Min(minX, posX - 140);
            minY = Math.Min(minY, posY - 70);
            maxX = Math.Max(maxX, posX + 140);
            maxY = Math.Max(maxY, posY + 70);
        }

        if (minX > maxX)
        {
            minX = 0; minY = 0; maxX = 1200; maxY = 800;
        }

        const double pad = 64.0;
        var originX = (int)(minX - pad);
        var originY = (int)(minY - pad);
        var totalW = (int)Math.Max(800.0, maxX - minX + pad * 2);
        var totalH = (int)Math.Max(600.0, maxY - minY + pad * 2);

        var sb = new StringBuilder();
        sb.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
        sb.AppendLine($"<svg width=\"{totalW}\" height=\"{totalH}\" viewBox=\"{originX} {originY} {totalW} {totalH}\" xmlns=\"http://www.w3.org/2000/svg\">");
        sb.AppendLine("  <defs>");
        sb.AppendLine("    <marker id=\"arrow\" viewBox=\"0 0 10 10\" refX=\"8\" refY=\"5\" markerWidth=\"6\" markerHeight=\"6\" orient=\"auto-start-reverse\">");
        sb.AppendLine("      <path d=\"M 0 1 L 9 5 L 0 9 z\" fill=\"#3B82F6\"/>");
        sb.AppendLine("    </marker>");
        sb.AppendLine("    <filter id=\"shadow\" x=\"-10%\" y=\"-10%\" width=\"120%\" height=\"120%\">");
        sb.AppendLine("      <feDropShadow dx=\"0\" dy=\"4\" stdDeviation=\"6\" flood-opacity=\"0.25\"/>");
        sb.AppendLine("    </filter>");
        sb.AppendLine("  </defs>");
        sb.AppendLine("  <style>");
        sb.AppendLine("    text { font-family: Segoe UI, -apple-system, sans-serif; }");
        sb.AppendLine("    .card-title { font-weight: 700; font-size: 15px; fill: #FFFFFF; }");
        sb.AppendLine("    .card-desc { font-size: 12px; fill: #A09D95; }");
        sb.AppendLine("    .sticky-text { font-weight: 600; font-size: 13px; fill: #1C1B18; }");
        sb.AppendLine("    .tag { font-family: Cascadia Code, Consolas, monospace; font-size: 10px; fill: #8C8980; }");
        sb.AppendLine("    .frame-title { font-weight: 700; font-size: 12px; fill: #FAF9F5; }");
        sb.AppendLine("  </style>");
        sb.AppendLine($"  <rect x=\"{originX}\" y=\"{originY}\" width=\"{totalW}\" height=\"{totalH}\" fill=\"#141413\" />");

        // Frames
        sb.AppendLine("  <g id=\"frames\">");
        foreach (var frame in snapshot.Frames)
        {
            var fx = (int)frame.X;
            var fy = (int)frame.Y;
            var fw = (int)frame.Width;
            var fh = (int)frame.Height;
            sb.AppendLine($"    <rect x=\"{fx}\" y=\"{fy}\" width=\"{fw}\" height=\"{fh}\" rx=\"16\" fill=\"{frame.ColorHex}\" fill-opacity=\"0.06\" stroke=\"{frame.ColorHex}\" stroke-width=\"1.5\" stroke-dasharray=\"6,4\" />");
            sb.AppendLine($"    <rect x=\"{fx}\" y=\"{fy}\" width=\"{Math.Min(fw, 240)}\" height=\"28\" rx=\"8\" fill=\"{frame.ColorHex}\" />");
            sb.AppendLine($"    <text x=\"{fx + 12}\" y=\"{fy + 18}\" class=\"frame-title\">{SecurityElement.Escape(frame.Title)}</text>");
        }
        sb.AppendLine("  </g>");

        // Connections
        sb.AppendLine("  <g id=\"connections\">");
        foreach (var conn in snapshot.Connections)
        {
            var fromPt = GetElementCenter(conn.FromId, snapshot, items);
            var toPt = GetElementCenter(conn.ToId, snapshot, items);
            if (fromPt != null && toPt != null)
            {
                var x1 = (int)fromPt.Value.X;
                var y1 = (int)fromPt.Value.Y;
                var x2 = (int)toPt.Value.X;
                var y2 = (int)toPt.Value.Y;
                sb.AppendLine($"    <line x1=\"{x1}\" y1=\"{y1}\" x2=\"{x2}\" y2=\"{y2}\" stroke=\"{conn.ColorHex}\" stroke-width=\"2\" marker-end=\"url(#arrow)\" />");
            }
        }
        sb.AppendLine("  </g>");

        // Stickies
        sb.AppendLine("  <g id=\"stickies\">");
        foreach (var sticky in snapshot.Stickies)
        {
            var sx = (int)(sticky.X - 100);
            var sy = (int)(sticky.Y - 80);
            sb.AppendLine($"    <g transform=\"translate({sx}, {sy})\" filter=\"url(#shadow)\">");
            sb.AppendLine($"      <rect width=\"200\" height=\"160\" rx=\"8\" fill=\"{sticky.ColorHex}\" />");
            sb.AppendLine($"      <text x=\"16\" y=\"32\" class=\"sticky-text\">{SecurityElement.Escape(sticky.Text)}</text>");
            sb.AppendLine("    </g>");
        }
        sb.AppendLine("  </g>");

        // Idea Cards
        sb.AppendLine("  <g id=\"ideas\">");
        for (var i = 0; i < items.Count; i++)
        {
            var item = items[i];
            var posX = 160.0 + (i % 3) * 320.0;
            var posY = 160.0 + (i / 3) * 240.0;
            if (snapshot.ItemPositions.TryGetValue(item.Id, out var pt))
            {
                posX = pt.X;
                posY = pt.Y;
            }
            var cardX = (int)(posX - 140);
            var cardY = (int)(posY - 70);
            var statusColor = item.Status == "completed" ? "#10B981" : (item.Status == "in_progress" ? "#3B82F6" : "#8C8980");

            sb.AppendLine($"    <g transform=\"translate({cardX}, {cardY})\" filter=\"url(#shadow)\">");
            sb.AppendLine("      <rect width=\"280\" height=\"140\" rx=\"12\" fill=\"#1C1B18\" stroke=\"#33322E\" stroke-width=\"1\" />");
            sb.AppendLine($"      <circle cx=\"24\" cy=\"28\" r=\"5\" fill=\"{statusColor}\" />");
            sb.AppendLine($"      <text x=\"40\" y=\"32\" class=\"card-title\">{SecurityElement.Escape(item.Title)}</text>");
            var desc = (item.Concept ?? "").Length > 70 ? item.Concept.Substring(0, 70) : (item.Concept ?? "");
            sb.AppendLine($"      <text x=\"24\" y=\"64\" class=\"card-desc\">{SecurityElement.Escape(desc)}</text>");
            sb.AppendLine("      <rect x=\"24\" y=\"104\" width=\"60\" height=\"20\" rx=\"10\" fill=\"#292824\" />");
            sb.AppendLine($"      <text x=\"54\" y=\"118\" class=\"tag\" text-anchor=\"middle\">{SecurityElement.Escape(item.Platform)}</text>");
            sb.AppendLine($"      <text x=\"100\" y=\"118\" class=\"tag\">{SecurityElement.Escape(item.Style)}</text>");
            sb.AppendLine("    </g>");
        }
        sb.AppendLine("  </g>");
        sb.AppendLine("</svg>");

        return sb.ToString();
    }

    public static string ExportHtml(CanvasGraphSnapshot snapshot, IReadOnlyList<Item> items)
    {
        var svg = ExportSvg(snapshot, items);

        return $"""
        <!DOCTYPE html>
        <html lang="ru">
        <head>
          <meta charset="utf-8">
          <title>KiteWin Infinite Canvas</title>
          <style>
            * {{ box-sizing: border-box; margin: 0; padding: 0; }}
            body {{ background: #141413; color: #FAF9F5; font-family: Segoe UI, -apple-system, sans-serif; overflow: hidden; height: 100vh; width: 100vw; }}
            #toolbar {{ position: fixed; top: 16px; left: 16px; z-index: 100; display: flex; gap: 8px; background: rgba(28,27,24,0.85); backdrop-filter: blur(12px); border: 1px solid rgba(255,255,255,0.1); border-radius: 8px; padding: 8px 12px; }}
            #search {{ background: #292824; border: 1px solid #3D3B35; color: #FAF9F5; padding: 6px 12px; border-radius: 6px; font-size: 13px; outline: none; }}
            button {{ background: #292824; border: 1px solid #3D3B35; color: #FAF9F5; padding: 6px 12px; border-radius: 6px; cursor: pointer; font-size: 13px; }}
            button:hover {{ background: #383631; }}
            #viewport {{ width: 100vw; height: 100vh; cursor: grab; }}
            #viewport:active {{ cursor: grabbing; }}
            .highlight {{ stroke: #3B82F6 !important; stroke-width: 3px !important; }}
          </style>
        </head>
        <body>
          <div id="toolbar">
            <input type="text" id="search" placeholder="Поиск карточек..." oninput="onSearch(this.value)">
            <button onclick="resetZoom()">Сброс масштаба</button>
          </div>
          <div id="viewport">
            {svg}
          </div>
          <script>
            let scale = 1, panX = 0, panY = 0, isPanning = false, startX = 0, startY = 0;
            const vp = document.getElementById('viewport');
            const svg = vp.querySelector('svg');
            
            vp.addEventListener('wheel', e => {{
              e.preventDefault();
              const delta = e.deltaY < 0 ? 1.1 : 0.9;
              scale = Math.min(Math.max(0.2, scale * delta), 4);
              updateTransform();
            }}, {{ passive: false }});

            vp.addEventListener('mousedown', e => {{
              if (e.target.tagName === 'INPUT' || e.target.tagName === 'BUTTON') return;
              isPanning = true;
              startX = e.clientX - panX;
              startY = e.clientY - panY;
            }});

            window.addEventListener('mousemove', e => {{
              if (!isPanning) return;
              panX = e.clientX - startX;
              panY = e.clientY - startY;
              updateTransform();
            }});

            window.addEventListener('mouseup', () => {{ isPanning = false; }});

            function updateTransform() {{
              svg.style.transformOrigin = '0 0';
              svg.style.transform = `translate(${{panX}}px, ${{panY}}px) scale(${{scale}})`;
            }}

            function resetZoom() {{
              scale = 1; panX = 0; panY = 0;
              updateTransform();
            }}

            function onSearch(query) {{
              const q = query.toLowerCase().trim();
              document.querySelectorAll('#ideas g, #stickies g').forEach(node => {{
                const text = node.textContent.toLowerCase();
                const rect = node.querySelector('rect');
                if (rect) {{
                  if (q && text.includes(q)) {{
                    rect.classList.add('highlight');
                  }} else {{
                    rect.classList.remove('highlight');
                  }}
                }}
              }});
            }}
          </script>
        </body>
        </html>
        """;
    }

    public static string ExportMermaid(CanvasGraphSnapshot snapshot, IReadOnlyList<Item> items)
    {
        var sb = new StringBuilder();
        sb.AppendLine("flowchart TD");
        for (var i = 0; i < items.Count; i++)
        {
            var item = items[i];
            var safeTitle = (item.Title ?? "").Replace("\"", "'");
            var safeId = "node_" + item.Id.Replace("-", "_");
            sb.AppendLine($"    {safeId}[\"{safeTitle}\"]");
        }
        foreach (var conn in snapshot.Connections)
        {
            var fromId = conn.FromId.Replace("item-", "node_").Replace("-", "_");
            var toId = conn.ToId.Replace("item-", "node_").Replace("-", "_");
            sb.AppendLine($"    {fromId} --> {toId}");
        }
        return sb.ToString();
    }

    private static (double X, double Y)? GetElementCenter(string id, CanvasGraphSnapshot snapshot, IReadOnlyList<Item> items)
    {
        if (id.StartsWith("sticky-", StringComparison.OrdinalIgnoreCase))
        {
            var raw = id.Substring("sticky-".Length);
            var sticky = snapshot.Stickies.FirstOrDefault(s => s.Id == raw);
            if (sticky != null) return (sticky.X, sticky.Y);
        }
        else if (id.StartsWith("item-", StringComparison.OrdinalIgnoreCase))
        {
            var raw = id.Substring("item-".Length);
            var item = items.FirstOrDefault(i => i.Id == raw);
            if (item != null)
            {
                if (snapshot.ItemPositions.TryGetValue(item.Id, out var pt))
                {
                    return (pt.X, pt.Y);
                }
                var idx = items.IndexOf(item);
                return (160.0 + (idx % 3) * 320.0, 160.0 + (idx / 3) * 240.0);
            }
        }
        return null;
    }
}
