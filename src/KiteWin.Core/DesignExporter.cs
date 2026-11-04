using System.Text;
using System.Text.Json;

namespace KiteWin.Core;

/// <summary>
/// Exporter for design artifacts: Google Stitch DESIGN.md, Figma W3C tokens JSON, and FigJam Mermaid diagrams.
/// Strictly zero emojis.
/// </summary>
public static class DesignExporter
{
    public static string GenerateStitchDesignMd(Item item)
    {
        var title = ItemTitle.Display(item.Title);
        var style = string.IsNullOrWhiteSpace(item.Style) ? "Editorial & Swiss Modern" : item.Style;
        var platform = string.IsNullOrWhiteSpace(item.Platform) ? "Web" : item.Platform;
        var tokens = ItemTitle.GetTokens(style, platform);

        var sb = new StringBuilder();
        sb.AppendLine($"# Design System: {title}");
        sb.AppendLine();
        sb.AppendLine("## Overview");
        sb.AppendLine($"Autonomous design system specification generated for **{title}** on {platform}.");
        sb.AppendLine($"Visual archetype: **{style}** ({tokens.Theme}).");
        sb.AppendLine();
        sb.AppendLine("## Color Palette");
        sb.AppendLine($"- **Canvas Background**: `{tokens.Background}`");
        sb.AppendLine($"- **Surface**: `{tokens.Surface}`");
        sb.AppendLine($"- **Primary Accent**: `{tokens.PrimaryAccent}` ({tokens.AccentRole})");
        sb.AppendLine($"- **Primary Text**: `{tokens.TextPrimary}`");
        sb.AppendLine($"- **Secondary Text**: `{tokens.TextSecondary}`");
        sb.AppendLine();
        sb.AppendLine("## Typography & Layout");
        sb.AppendLine($"- **Typography**: {tokens.Typography}");
        sb.AppendLine("- **Grid**: 4px / 4 epx base scale (Space.xaml / Tailwind 4px).");
        sb.AppendLine("- **Container Padding**: 16px mobile, 24px tablet, 32px desktop.");
        sb.AppendLine();
        sb.AppendLine("## Component Tokens");
        sb.AppendLine($"- **Buttons**: {tokens.Buttons}");
        sb.AppendLine($"- **Cards**: {tokens.Cards}");
        sb.AppendLine("- **Motion**: Sub-250ms spring curves, zero unnecessary layout shift.");
        sb.AppendLine();
        sb.AppendLine("## Implementation Notes");
        sb.AppendLine("- No emojis in production code, buttons, or technical logs.");
        sb.AppendLine("- WCAG 2.1 AA compliant contrast across all surfaces.");
        sb.AppendLine("- Keyboard accessible focus rings on all interactive elements.");

        return sb.ToString();
    }

    public static string GenerateFigmaTokensJson(Item item)
    {
        var title = ItemTitle.Display(item.Title);
        var style = string.IsNullOrWhiteSpace(item.Style) ? "Editorial & Swiss Modern" : item.Style;
        var tokens = ItemTitle.GetTokens(style, item.Platform);

        var jsonModel = new
        {
            name = title,
            version = "1.0.0",
            style,
            color = new
            {
                background = new { value = tokens.Background, type = "color" },
                surface = new { value = tokens.Surface, type = "color" },
                accent = new { value = tokens.PrimaryAccent, type = "color" },
                textPrimary = new { value = tokens.TextPrimary, type = "color" },
                textSecondary = new { value = tokens.TextSecondary, type = "color" }
            },
            spacing = new
            {
                unit = new { value = "4px", type = "spacing" },
                sm = new { value = "8px", type = "spacing" },
                md = new { value = "16px", type = "spacing" },
                lg = new { value = "24px", type = "spacing" },
                xl = new { value = "32px", type = "spacing" }
            },
            radii = new
            {
                card = new { value = "8px", type = "borderRadius" },
                button = new { value = "6px", type = "borderRadius" }
            }
        };

        return JsonSerializer.Serialize(jsonModel, new JsonSerializerOptions { WriteIndented = true });
    }

    public static string GenerateFigJamDiagram(Item item)
    {
        var title = ItemTitle.Display(item.Title).Replace("\"", "'");
        var concept = string.IsNullOrWhiteSpace(item.Concept) ? "Основной сценарий" : item.Concept.Replace("\"", "'").Trim();
        var style = string.IsNullOrWhiteSpace(item.Style) ? "Swiss Modern" : item.Style;

        var sb = new StringBuilder();
        sb.AppendLine("flowchart TD");
        sb.AppendLine($"    A[\"{title}\"] --> B[\"Слой данных (SQLite WAL)\"]");
        sb.AppendLine($"    A --> C[\"UI Компоненты ({style})\"]");
        sb.AppendLine("    A --> D[\"P2P Синхронизация (REST 24242 / 24243)\"]");
        sb.AppendLine($"    B --> E[\"{concept}\"]");
        sb.AppendLine("    C --> F[\"Prompt Translator & Stitch Engine\"]");
        sb.AppendLine("    D --> G[\"Kite macOS & Web Studio\"]");
        sb.AppendLine("    classDef accent fill:#5E6AD2,stroke:#2B303B,stroke-width:2px,color:#FFFFFF;");
        sb.AppendLine("    class A,D,F accent;");

        return sb.ToString();
    }
}
