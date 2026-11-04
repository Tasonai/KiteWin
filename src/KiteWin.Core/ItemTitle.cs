using System.Text;
using System.Text.RegularExpressions;

namespace KiteWin.Core;

/// <summary>
/// Design tokens and prompt compilation engine for Antigravity skills (enhance-prompt, design-md, emil-design-eng).
/// Formats prompts for Google Stitch, Targeted Edit, and Master Plan without emojis.
/// </summary>
public static partial class ItemTitle
{
    public static string Display(string raw)
    {
        var trimmed = CleanSpaces().Replace(raw ?? "", " ").Trim();
        return string.IsNullOrWhiteSpace(trimmed) ? "Новая идея" : trimmed;
    }

    public static string Clean(string raw) => Display(raw);

    [GeneratedRegex(@"\s+")]
    private static partial Regex CleanSpaces();

    public record DesignTokens(
        string Platform,
        string Theme,
        string Background,
        string Surface,
        string PrimaryAccent,
        string AccentRole,
        string TextPrimary,
        string TextSecondary,
        string Typography,
        string Buttons,
        string Cards
    );

    public static DesignTokens GetTokens(string style, string platform = "Web")
    {
        var p = string.IsNullOrWhiteSpace(platform) ? "Web" : platform;
        return style switch
        {
            "Claude Parchment" or "Claude Paper" => new DesignTokens(
                Platform: p,
                Theme: "warm parchment light mode (never pure white)",
                Background: "#FBF9F4",
                Surface: "#F3EFE6",
                PrimaryAccent: "#D97757",
                AccentRole: "Terracotta warmth on primary actions and focus states",
                TextPrimary: "#1F1E1B",
                TextSecondary: "#6E6B63",
                Typography: "Editorial serif display (Instrument Serif / Georgia), crisp geometric sans body (Inter / Helvetica Neue)",
                Buttons: "Subtle 6px rounded, warm terracotta fill (#D97757) with off-white text (#FBF9F4)",
                Cards: "Clean 8px rounded, 1px border (#E5DEC9), whisper-soft diffused shadow"
            ),
            "Codex Minimal" => new DesignTokens(
                Platform: p,
                Theme: "edge-to-edge pure monochrome canvas",
                Background: "#FFFFFF",
                Surface: "#F7F7F7",
                PrimaryAccent: "#000000",
                AccentRole: "Ink-black contrast dividers and high-impact CTA fills",
                TextPrimary: "#000000",
                TextSecondary: "#525252",
                Typography: "System monospace headers (JetBrains Mono / SF Mono), ultra-clean sans body (Inter)",
                Buttons: "Sharp 0px or 4px minimal borders, solid black fill with pure white text",
                Cards: "Flat 4px border (#E5E5E5), zero drop shadows, 1px hairline dividers"
            ),
            "Terracotta Journal" or "Terracotta Clay" => new DesignTokens(
                Platform: p,
                Theme: "sun-baked terracotta clay on ivory canvas",
                Background: "#F7F4EE",
                Surface: "#ECE6DA",
                PrimaryAccent: "#C85A32",
                AccentRole: "Earthy terracotta warmth on hero metrics and interactive pills",
                TextPrimary: "#2A2421",
                TextSecondary: "#7A6F68",
                Typography: "Warm editorial display (Newsreader / Playfair), humanist sans body (Source Sans 3)",
                Buttons: "Generously rounded 12px pill buttons with terracotta fill (#C85A32)",
                Cards: "Rounded 16px cards, soft clay ambient shadow, 1px warm border (#E7DFD5)"
            ),
            "Risograph Print" or "Risograph Two-Color" => new DesignTokens(
                Platform: p,
                Theme: "two-color riso art print (Teal and Fluorescent Pink)",
                Background: "#F5F2EB",
                Surface: "#EAE5DA",
                PrimaryAccent: "#008080",
                AccentRole: "Duotone ink contrast: Teal structure and Pink highlights",
                TextPrimary: "#1B2A2A",
                TextSecondary: "#4A6060",
                Typography: "Stark industrial sans (Space Grotesk), tactile display accents (Syne)",
                Buttons: "Sharp 0px edges, 2px solid teal border with 3px offset press shadow",
                Cards: "Zero border-radius, 2px solid riso ink stroke (#008080)"
            ),
            "Linear & DevTool Cockpit" => new DesignTokens(
                Platform: p,
                Theme: "deep obsidian developer cockpit with laser neon accents",
                Background: "#0D0E11",
                Surface: "#16181D",
                PrimaryAccent: "#5E6AD2",
                AccentRole: "Precision violet laser accents on active states and tabs",
                TextPrimary: "#F2F3F5",
                TextSecondary: "#8A8F98",
                Typography: "Inter / Geist Sans body with JetBrains Mono tabular numeric metrics",
                Buttons: "Precision 6px rounded, 1px stroke (#2B303B), violet glow on focus",
                Cards: "Subtle 8px rounded, 1px perimeter border (#232730), zero ambient blur"
            ),
            "Neo-Brutalism & Tactical" => new DesignTokens(
                Platform: p,
                Theme: "high-contrast brutalist canvas with stark black outlines",
                Background: "#FEF08A",
                Surface: "#FFFFFF",
                PrimaryAccent: "#000000",
                AccentRole: "Thick 2px-3px architectural ink strokes and hard offset shadows",
                TextPrimary: "#000000",
                TextSecondary: "#1F2937",
                Typography: "Heavyweight grotesk headers (Public Sans Black), mono labels",
                Buttons: "Sharp 0px corners, 2px solid black border, 3px offset drop shadow (#000000)",
                Cards: "Sharp 0px corners, 2px solid black border, 4px offset shadow (#000000)"
            ),
            "Voxel & 3D Isometric" => new DesignTokens(
                Platform: p,
                Theme: "isometric 3D depth with ambient occlusion surfaces",
                Background: "#181824",
                Surface: "#222233",
                PrimaryAccent: "#A78BFA",
                AccentRole: "Soft lilac diffuse light on dimensional edges",
                TextPrimary: "#F5F3FF",
                TextSecondary: "#9CA3AF",
                Typography: "Rounded sans (Plus Jakarta Sans) with geometric display accents",
                Buttons: "Chunky 12px pill with 3D bottom bevel (border-b-4 border-black/40)",
                Cards: "20px rounded isometric cards with ambient occlusion shadow"
            ),
            "Dark Glassmorphism" => new DesignTokens(
                Platform: p,
                Theme: "frosted dark glass with specular edge reflections",
                Background: "#090D16",
                Surface: "rgba(255, 255, 255, 0.05)",
                PrimaryAccent: "#38BDF8",
                AccentRole: "Cyan glow highlights on interactive borders",
                TextPrimary: "#F0F9FF",
                TextSecondary: "#94A3B8",
                Typography: "Modern geometric sans (Inter / SF Pro Display)",
                Buttons: "Frosted capsule pill (backdrop-blur-md, border 1px solid white/20)",
                Cards: "16px rounded, backdrop-blur-xl, 1px specular gradient highlight border"
            ),
            _ => new DesignTokens( // Editorial & Swiss Modern
                Platform: p,
                Theme: "rigorous Swiss typographic grid with high-contrast hierarchy",
                Background: "#F8F7F4",
                Surface: "#FFFFFF",
                PrimaryAccent: "#1A1A1A",
                AccentRole: "Architectural jet-black focus states and crisp rule dividers",
                TextPrimary: "#111111",
                TextSecondary: "#555555",
                Typography: "Display serif (Instrument Serif), body neutral sans (Inter)",
                Buttons: "Geometric 6px rounded, jet black fill, off-white text, sub-250ms press",
                Cards: "Modular rounded 12px, 1px border (#DDD9D0), whisper-soft diffused shadow"
            )
        };
    }

    /// <summary>
    /// Compiles an authoritative, production-grade Google Stitch prompt.
    /// Follows the 3-part layout: Atmosphere Header + DESIGN SYSTEM + numbered Page Structure.
    /// Strictly zero emojis.
    /// </summary>
    public static string CompileStitchPrompt(Item item)
    {
        var title = Display(item.Title);
        var concept = string.IsNullOrWhiteSpace(item.Concept)
            ? "Разработай высокопроизводительный, отзывчивый интерфейс с чистой архитектурой компонентов."
            : item.Concept.Trim();
        var style = string.IsNullOrWhiteSpace(item.Style) ? "Editorial & Swiss Modern" : item.Style;
        var platform = string.IsNullOrWhiteSpace(item.Platform) ? "Web" : item.Platform;
        var tokens = GetTokens(style, platform);

        var sb = new StringBuilder();

        // Part 1: Atmosphere Header
        sb.AppendLine($"A professional, modern {platform.ToLowerInvariant()} application interface for {title}, featuring {tokens.Theme}.");
        sb.AppendLine();

        // Part 2: Design System (Required)
        sb.AppendLine("**DESIGN SYSTEM (REQUIRED):**");
        sb.AppendLine($"- Platform: {tokens.Platform}");
        sb.AppendLine($"- Theme: {tokens.Theme}");
        sb.AppendLine($"- Background: {tokens.Background}");
        sb.AppendLine($"- Surface: {tokens.Surface}");
        sb.AppendLine($"- Primary Accent: {tokens.PrimaryAccent} ({tokens.AccentRole})");
        sb.AppendLine($"- Text Primary: {tokens.TextPrimary}");
        sb.AppendLine($"- Text Secondary: {tokens.TextSecondary}");
        sb.AppendLine($"- Buttons: {tokens.Buttons}");
        sb.AppendLine($"- Cards: {tokens.Cards}");
        sb.AppendLine($"- Typography: {tokens.Typography}");
        sb.AppendLine();

        // Part 3: Numbered Page Structure
        sb.AppendLine("**Page Structure:**");
        sb.AppendLine($"1. Navigation Header: Top app bar with {title} logo, breadcrumbs, search input, and profile/status indicators.");
        sb.AppendLine($"2. Executive Overview: Key metrics display, status highlights, and quick action bar.");
        sb.AppendLine($"3. Main Workspace: Primary interactive section addressing: {concept}");
        sb.AppendLine($"4. Details & Inspector Panel: Contextual sidebar showing entity properties, metadata, and export tools.");
        sb.AppendLine("5. System Toast & Feedback: Sub-250ms spring animations, keyboard shortcuts (Esc, Cmd/Ctrl+K), and accessible focus rings.");
        sb.AppendLine();
        sb.AppendLine("Tip: Do not add any extra sections not requested above. Use realistic data, never placeholder text.");

        return sb.ToString();
    }

    /// <summary>
    /// Compiles a surgical, targeted edit prompt for existing UI without regressions.
    /// Follows enhance-prompt Step 4 Ex 2.
    /// </summary>
    public static string CompileTargetedEditPrompt(Item item, string targetElement = "", string editDetails = "")
    {
        var title = Display(item.Title);
        var elem = string.IsNullOrWhiteSpace(targetElement) ? "компонент действий" : targetElement.Trim();
        var details = string.IsNullOrWhiteSpace(editDetails)
            ? (string.IsNullOrWhiteSpace(item.Concept) ? "Оптимизируй геометрию, добавь тактильный отклик sub-250ms и кольца фокуса." : item.Concept.Trim())
            : editDetails.Trim();
        var style = string.IsNullOrWhiteSpace(item.Style) ? "Editorial & Swiss Modern" : item.Style;
        var platform = string.IsNullOrWhiteSpace(item.Platform) ? "Web" : item.Platform;
        var tokens = GetTokens(style, platform);

        var sb = new StringBuilder();
        sb.AppendLine($"Targeted UI edit for {title}: Modify {elem} strictly following the existing design system.");
        sb.AppendLine();
        sb.AppendLine("**SURGICAL MODIFICATION DIRECTIVES:**");
        sb.AppendLine($"- Target Component: {elem}");
        sb.AppendLine($"- Scope of Change: {details}");
        sb.AppendLine($"- Design System Tokens: Surface {tokens.Surface}, Accent {tokens.PrimaryAccent}, Buttons: {tokens.Buttons}");
        sb.AppendLine("- Layout & Geometry: Preserve existing layout grid (4px scale), avoid shifts (Zero Cumulative Layout Shift).");
        sb.AppendLine("- Zero Regressions: Do NOT rewrite, restyle, or remove any surrounding sections, headers, or sibling components.");
        sb.AppendLine("- Accessibility & State: Ensure WCAG AA contrast, :focus-visible ring, active touch state, and disabled state.");

        return sb.ToString();
    }

    /// <summary>
    /// Compiles an engineering Master Plan prompt with technical stack, Zod validation, and blind spot mitigations.
    /// </summary>
    public static string CompilePlanPrompt(Item item)
    {
        var title = Display(item.Title);
        var concept = string.IsNullOrWhiteSpace(item.Concept) ? "Архитектурная реализация проекта." : item.Concept.Trim();
        var style = string.IsNullOrWhiteSpace(item.Style) ? "Editorial & Swiss Modern" : item.Style;
        var platform = string.IsNullOrWhiteSpace(item.Platform) ? "Web" : item.Platform;
        var tokens = GetTokens(style, platform);

        var sb = new StringBuilder();
        sb.AppendLine($"# Инженерный план реализации: {title}");
        sb.AppendLine();
        sb.AppendLine("## 1. Архитектурный контекст и JTBD");
        sb.AppendLine($"- Платформа: {platform}");
        sb.AppendLine($"- Целевая концепция: {concept}");
        sb.AppendLine($"- Визуальная дизайн-система: {style}");
        sb.AppendLine();
        sb.AppendLine("## 2. Дизайн-система и токены (REQUIRED)");
        sb.AppendLine($"- Фоновый цвет: {tokens.Background}");
        sb.AppendLine($"- Поверхность карточек: {tokens.Surface}");
        sb.AppendLine($"- Основной акцент: {tokens.PrimaryAccent} ({tokens.AccentRole})");
        sb.AppendLine($"- Типографика: {tokens.Typography}");
        sb.AppendLine($"- Сетка: строгая 4px / 4 epx сетка");
        sb.AppendLine();
        sb.AppendLine("## 3. Фазовый план разработки");
        sb.AppendLine("1. Модели данных, валидация схем и локальное хранилище SQLite/WAL.");
        sb.AppendLine("2. Компонентный каркас UI: NavigationRail/Sidebar, Header, WorkArea.");
        sb.AppendLine("3. Интерактивная логика, тактильные пружины (sub-250ms) и скелетоны.");
        sb.AppendLine("4. P2P-синхронизация по REST API и обработка конфликтов.");
        sb.AppendLine("5. Стресс-тест данных, доступность с клавиатуры (a11y) и финализация.");

        return sb.ToString();
    }

    public static string CompileConcisePrompt(Item item)
    {
        var title = Display(item.Title);
        var concept = string.IsNullOrWhiteSpace(item.Concept) ? "Реализуй проект." : item.Concept.Trim();
        var style = string.IsNullOrWhiteSpace(item.Style) ? "Editorial & Swiss Modern" : item.Style;
        var tokens = GetTokens(style, item.Platform);

        return $"{title}: {concept}. Платформа: {item.Platform}. Стиль: {style} (фон {tokens.Background}, акцент {tokens.PrimaryAccent}). Без эмодзи, семантическая верстка, строгая 4px сетка.";
    }

    public static string CompileActionPrompt(Item item)
    {
        var title = Display(item.Title);
        var sb = new StringBuilder();
        sb.AppendLine($"Чек-лист задач для {title}:");
        sb.AppendLine("- [ ] Инициализировать модели данных и локальный репозиторий");
        sb.AppendLine("- [ ] Подключить дизайн-систему со стилем " + (item.Style ?? "Editorial & Swiss Modern"));
        sb.AppendLine("- [ ] Реализовать основной сценарий: " + (string.IsNullOrWhiteSpace(item.Concept) ? title : item.Concept));
        sb.AppendLine("- [ ] Настроить тактильный отклик и шорткаты клавиатуры");
        sb.AppendLine("- [ ] Проверить отсутствие эмодзи и доступность WCAG AA");
        return sb.ToString();
    }
}
