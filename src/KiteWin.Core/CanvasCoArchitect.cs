using KiteWin.Core.Models;

namespace KiteWin.Core;

public sealed record ProposedAddition(
    string FrameTitle,
    string StickyText,
    string ColorHex,
    string ConnectionFrom,
    string ConnectionLabel);

public static class CanvasCoArchitect
{
    public static List<ProposedAddition> SuggestGaps(IReadOnlyList<CanvasFrame> frames, IReadOnlyList<CanvasSticky> stickies)
    {
        var additions = new List<ProposedAddition>();
        var allText = string.Join(" ", frames.Select(f => f.Title).Concat(stickies.Select(s => s.Text))).ToLowerInvariant();

        if (!allText.Contains("auth") && !allText.Contains("identity") && !allText.Contains("jwt"))
        {
            additions.Add(new ProposedAddition(
                "Security & Identity",
                "Token Auth & Access Boundary",
                "#FEE2E2",
                "UI",
                "Authenticates"
            ));
        }

        if (!allText.Contains("cache") && !allText.Contains("redis") && !allText.Contains("buffer"))
        {
            additions.Add(new ProposedAddition(
                "Performance & Caching",
                "In-Memory LRU Cache & WAL",
                "#FEF3C7",
                "Service",
                "Cached By"
            ));
        }

        if (!allText.Contains("log") && !allText.Contains("trace") && !allText.Contains("metric") && !allText.Contains("telemetry"))
        {
            additions.Add(new ProposedAddition(
                "Observability & Audit",
                "Structured Logs & Tracing Span",
                "#E0E7FF",
                "Storage",
                "Monitors"
            ));
        }

        return additions;
    }

    public static int ApplyGaps(CanvasGraphSnapshot snapshot)
    {
        var suggestions = SuggestGaps(snapshot.Frames, snapshot.Stickies);
        if (suggestions.Count == 0) return 0;

        double startX = 1300;
        double startY = 100;

        foreach (var s in suggestions)
        {
            snapshot.Frames.Add(new CanvasFrame
            {
                Title = s.FrameTitle,
                ColorHex = "#EC4899",
                X = startX,
                Y = startY,
                Width = 320,
                Height = 300
            });

            var stickyId = Guid.NewGuid().ToString();
            snapshot.Stickies.Add(new CanvasSticky
            {
                Id = stickyId,
                Text = s.StickyText,
                ColorHex = s.ColorHex,
                X = startX + 20,
                Y = startY + 60
            });

            var existing = snapshot.Stickies.FirstOrDefault(st => st.Text.Contains(s.ConnectionFrom, StringComparison.OrdinalIgnoreCase) && st.Id != stickyId);
            if (existing != null)
            {
                snapshot.Connections.Add(new CanvasConnection
                {
                    FromId = existing.Id,
                    ToId = stickyId,
                    Label = s.ConnectionLabel,
                    ColorHex = "#EC4899"
                });
            }

            startX += 360;
        }

        return suggestions.Count;
    }
}
