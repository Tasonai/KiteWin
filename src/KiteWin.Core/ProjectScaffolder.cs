using System.Text;
using System.Text.RegularExpressions;

namespace KiteWin.Core;

public sealed record ScaffoldResult(string Directory, IReadOnlyList<string> Files, string GitMessage = "");

/// <summary>
/// Scaffolds a complete project constitution ready for Antigravity agents,
/// creating README.md, SPEC.md, PRD.md, PLAN.md, DESIGN.md, CLAUDE.md, and .planning/.
/// Zero emojis.
/// </summary>
public static class ProjectScaffolder
{
    public static string Slugify(string text)
    {
        var cleaned = Regex.Replace((text ?? "").ToLowerInvariant(), @"[^a-z0-9]+", "-").Trim('-');
        return string.IsNullOrEmpty(cleaned) ? "kite-project" : cleaned;
    }

    public static ScaffoldResult Scaffold(
        string parentFolder,
        string title,
        string concept,
        string platform,
        string style,
        string planPrompt,
        bool createGitBranch = false)
    {
        var folderName = Slugify(title);
        var projectDir = Path.Combine(parentFolder, folderName);
        Directory.CreateDirectory(projectDir);

        var createdFiles = new List<string>();
        var tokens = ItemTitle.GetTokens(style, platform);

        // 1. README.md
        var readme = $"""
        # {title}

        > **Platform:** {platform}
        > **Visual System:** {style}
        > **Scaffolded by:** [Kite Studio](https://github.com/Tasonai/Kite)

        ## Концепция
        {(string.IsNullOrWhiteSpace(concept) ? "Описание концепции и ключевого сценария использования." : concept.Trim())}

        ## Быстрый старт
        Ознакомьтесь с [SPEC.md](./SPEC.md), [PRD.md](./PRD.md) и [PLAN.md](./PLAN.md) для реализации через Antigravity / Cursor / Claude Code.

        ## Чек-лист разработки
        - [ ] Первичная инициализация компонентов и токенов
        - [ ] Локальное хранилище данных SQLite / Local-first
        - [ ] Реализация основного пользовательского сценария
        - [ ] Аудит дизайна, доступности и сборка инсталляторов
        """;
        File.WriteAllText(Path.Combine(projectDir, "README.md"), readme, Encoding.UTF8);
        createdFiles.Add("README.md");

        // 2. SPEC.md and PRD.md
        var spec = $"""
        # Product Requirements Document (PRD): {title}

        ## 1. Problem & JTBD
        - **Target Platform:** {platform}
        - **Style Aesthetic:** {style}
        - **Core Value:** Решение ключевой задачи пользователя с наивысшей скоростью отклика и нулевым визуальным шумом.

        ## 2. Functional Requirements
        - Быстродействие и отсутствие сетевых блокировок UI.
        - Локальное сохранение состояния.
        - Поддержка клавиатурных шорткатов.

        ## 3. Architecture & Tech Stack
        - **UI/Layout:** Модульная сетка 4 epx / 4pt, типографика в духе {style}.
        - **State:** Однонаправленный поток данных.
        """;
        File.WriteAllText(Path.Combine(projectDir, "SPEC.md"), spec, Encoding.UTF8);
        File.WriteAllText(Path.Combine(projectDir, "PRD.md"), spec, Encoding.UTF8);
        createdFiles.Add("SPEC.md");
        createdFiles.Add("PRD.md");

        // 3. PLAN.md
        var plan = !string.IsNullOrWhiteSpace(planPrompt)
            ? planPrompt.Trim()
            : ItemTitle.CompilePlanPrompt(new Item(title) { Concept = concept, Platform = platform, Style = style });
        File.WriteAllText(Path.Combine(projectDir, "PLAN.md"), plan, Encoding.UTF8);
        createdFiles.Add("PLAN.md");

        // 4. DESIGN.md
        var designMd = $"""
        # Design System Specification — DESIGN.md
        > **Project:** {title}
        > **Aesthetic:** {style} ({tokens.Theme})

        ## Color Tokens
        - **Background:** {tokens.Background}
        - **Surface:** {tokens.Surface}
        - **Primary Accent:** {tokens.PrimaryAccent} ({tokens.AccentRole})
        - **Text Primary:** {tokens.TextPrimary}
        - **Text Secondary:** {tokens.TextSecondary}

        ## Components & Elevation
        - **Buttons:** {tokens.Buttons}
        - **Cards & Panels:** {tokens.Cards}
        - **Typography:** {tokens.Typography}
        - **Layout Grid:** Strict 4 epx / 4pt modular grid (touch targets >= 44px).
        """;
        File.WriteAllText(Path.Combine(projectDir, "DESIGN.md"), designMd, Encoding.UTF8);
        createdFiles.Add("DESIGN.md");

        // 5. .planning/ directory for Antigravity GSD agents
        var planningDir = Path.Combine(projectDir, ".planning");
        var phasesDir = Path.Combine(planningDir, "phases", "01-foundation");
        Directory.CreateDirectory(phasesDir);

        var projectMd = $"""
        # Project Charter: {title}

        ## Vision & JTBD
        {(string.IsNullOrWhiteSpace(concept) ? "Реализация высокопроизводительного нативного решения." : concept.Trim())}

        ## Platform & Visual System
        - **Platform:** {platform}
        - **Design System:** {style} ({tokens.Theme})
        - **Grid Standard:** 4 epx / 4pt modular grid, sub-250ms feedback, zero emojis

        ## Required Antigravity Skills
        - `/site-md` — Architecture mapping and screen catalog
        - `/design-md` — Visual system and DESIGN.md token sync
        - `/emil-design-eng` — Micro-interactions and spring physics
        - `/gsd-execute-phase` — Wave-based execution with atomic commits
        """;
        File.WriteAllText(Path.Combine(planningDir, "PROJECT.md"), projectMd, Encoding.UTF8);
        createdFiles.Add(".planning/PROJECT.md");

        var roadmapMd = $"""
        # Roadmap: {title}

        ## Milestone 1: MVP Core
        - [ ] Phase 1: Foundation & Data Schema
        - [ ] Phase 2: Core Domain Logic & State Management
        - [ ] Phase 3: High-Fidelity UI & Token Alignment
        - [ ] Phase 4: Verification, A11y & Packaging
        """;
        File.WriteAllText(Path.Combine(planningDir, "ROADMAP.md"), roadmapMd, Encoding.UTF8);
        createdFiles.Add(".planning/ROADMAP.md");

        var stateMd = $"""
        # Project State: {title}

        - **Current Milestone:** 1.0.0
        - **Current Phase:** Phase 1 (Foundation & Data Schema)
        - **Status:** Initialized and ready for agent execution
        """;
        File.WriteAllText(Path.Combine(planningDir, "STATE.md"), stateMd, Encoding.UTF8);
        createdFiles.Add(".planning/STATE.md");

        var phase1Plan = """
        # Phase 1 Plan: Foundation & Data Schema

        ## Goal
        Establish data models, storage layer, and baseline project structure.

        ## Tasks
        1. Initialize repository and configuration.
        2. Setup data models and schema validation.
        3. Verify local read/write persistence.
        """;
        File.WriteAllText(Path.Combine(phasesDir, "PLAN.md"), phase1Plan, Encoding.UTF8);
        createdFiles.Add(".planning/phases/01-foundation/PLAN.md");

        // 6. CLAUDE.md
        var claudeMd = $"""
        # Engineering Constitution for {title}

        ## Core Rules
        - **Zero AI Slop:** No generic template filler, no unstyled placeholders, no emojis.
        - **Craftsmanship:** Strict 4 epx / 4pt modular grid. Every touch target >= 44px.
        - **Architecture:** Local-first, sub-250ms spring transitions, offline resilience.

        ## Antigravity Skills
        Execute work with slash syntax:
        - `/site-md` — Architecture mapping and project constitution
        - `/design-md` — Visual system and design token sync
        - `/taste-design` — Typography curation and visual hierarchy
        - `/impeccable` — Interface polish and high-density details
        - `/emil-design-eng` — Physical spring micro-interactions
        - `/a11y-debugging` — WCAG AA compliance and keyboard accessibility
        - `/gsd-execute-phase` — Wave-based execution with atomic commits
        """;
        File.WriteAllText(Path.Combine(projectDir, "CLAUDE.md"), claudeMd, Encoding.UTF8);
        createdFiles.Add("CLAUDE.md");

        // 7. .gitignore
        var gitignore = """
        .DS_Store
        node_modules/
        bin/
        obj/
        dist/
        *.log
        .env.local
        """;
        File.WriteAllText(Path.Combine(projectDir, ".gitignore"), gitignore, Encoding.UTF8);
        createdFiles.Add(".gitignore");

        var gitMsg = "";
        if (createGitBranch)
        {
            var res = InitializeGitFeature(projectDir, $"feature/kite-{folderName}", $"feat: scaffold planning specs for {title}");
            gitMsg = res.Message;
        }

        return new ScaffoldResult(projectDir, createdFiles, gitMsg);
    }

    public static (bool Success, string Message) InitializeGitFeature(
        string projectDir,
        string? branchName = null,
        string? commitMessage = null)
    {
        var folderName = Path.GetFileName(projectDir);
        var branch = branchName ?? $"feature/kite-{folderName}";
        var commitMsg = commitMessage ?? $"feat: scaffold planning specs for {folderName}";

        string RunGit(string args)
        {
            try
            {
                var psi = new System.Diagnostics.ProcessStartInfo("git", args)
                {
                    WorkingDirectory = projectDir,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                using var proc = System.Diagnostics.Process.Start(psi);
                if (proc == null) return "error";
                var output = proc.StandardOutput.ReadToEnd() + proc.StandardError.ReadToEnd();
                proc.WaitForExit(3000);
                return output;
            }
            catch (Exception ex)
            {
                return $"error: {ex.Message}";
            }
        }

        RunGit("init");
        RunGit($"checkout -B {branch}");
        RunGit("add .");
        RunGit($"commit -m \"{commitMsg}\"");
        return (true, $"Git branch '{branch}' ready with initial planning specs");
    }
}
