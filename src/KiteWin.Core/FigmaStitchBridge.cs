using System.Text.RegularExpressions;
using KiteWin.Core.Models;

namespace KiteWin.Core;

public sealed record ExtractedDesignSystem(
    string Name,
    string PrimaryColor,
    string SurfaceColor,
    string TextColor,
    string Typography,
    IReadOnlyList<CanvasReferenceCard> ReferenceScreens);

public static class FigmaStitchBridge
{
    private static readonly Regex HexRegex = new(@"#([A-Fa-f0-9]{6}|[A-Fa-f0-9]{3})", RegexOptions.Compiled);

    public static ExtractedDesignSystem ParseDesignTokens(string text)
    {
        var primary = "#3B82F6";
        var surface = "#1E293B";
        var textColor = "#F8FAFC";
        var typography = "Segoe UI / Cascadia";
        var screens = new List<CanvasReferenceCard>();

        var lines = (text ?? "").Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        foreach (var line in lines)
        {
            var lower = line.ToLowerInvariant();
            if (lower.Contains("primary") || lower.Contains("accent"))
            {
                var match = HexRegex.Match(line);
                if (match.Success) primary = match.Value;
            }
            else if (lower.Contains("surface") || lower.Contains("card"))
            {
                var match = HexRegex.Match(line);
                if (match.Success) surface = match.Value;
            }
            else if (lower.Contains("text") || lower.Contains("foreground"))
            {
                var match = HexRegex.Match(line);
                if (match.Success) textColor = match.Value;
            }
            else if (lower.Contains("font") || lower.Contains("typography"))
            {
                typography = line.Replace("#", "").Trim();
            }
            else if (lower.Contains("screen") || lower.Contains("frame") || lower.Contains("view"))
            {
                screens.Add(new CanvasReferenceCard
                {
                    Title = line.Trim('#', '*', '-', ' '),
                    Source = "Figma / Stitch",
                    Url = "https://stitch.design",
                    ColorHex = primary,
                    X = 150,
                    Y = 150 + (screens.Count * 140),
                    Tags = ["UI", "Token", "Figma"],
                    Description = "Imported from design system specification."
                });
            }
        }

        if (screens.Count == 0)
        {
            screens.Add(new CanvasReferenceCard
            {
                Title = "Primary Dashboard Screen",
                Source = "Figma Spec",
                Url = "https://figma.com",
                ColorHex = primary,
                X = 150,
                Y = 150,
                Tags = ["Figma", "Design System"],
                Description = "Design system screen layout with extracted color tokens."
            });
        }

        return new ExtractedDesignSystem(
            "Imported Design System",
            primary,
            surface,
            textColor,
            typography,
            screens
        );
    }
}
