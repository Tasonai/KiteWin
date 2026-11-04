namespace KiteWin.Core;

public record AntigravitySkillInfo(
    string Name,
    string Category,
    string Summary,
    string Description,
    string? PluginId = null,
    bool IsCore = false
);

public record AntigravityPluginInfo(
    string Id,
    string Name,
    string Category,
    string Author,
    string Version,
    string Summary,
    int SkillsCount,
    string[] Skills,
    string? McpServer = null,
    string? CliCommand = null,
    string IconGlyph = "\uE7B8" // Package icon in Segoe Fluent Icons
);

public static class SkillsRepository
{
    public static readonly AntigravityPluginInfo[] Plugins =
    [
        new("bama", "Build Any Mac App (BAMA)", "Apple & macOS", "bama", "1.2.0",
            "Комплексный плагин для проектирования и сборки нативных macOS приложений на SwiftUI и SwiftData.", 5,
            ["bama", "mac-design", "new-mac-app", "swift-swiftui", "test-mac-app"], null, "bama", "\uE7F4"),

        new("bawa", "Build Any Windows App (BAWA)", "Windows & WinUI", "bawa", "1.0.0",
            "Разработка современных нативных приложений под Windows 11 с использованием C#, WinUI 3 и Fluent Design.", 22,
            ["agent-cli", "app-brief", "app-icon", "bawa", "build-and-verify", "clone", "csharp-winui", "design-from-app", "design-kit", "design-system", "distribute", "improve-architecture", "improve-loop", "layout-grid", "new-windows-app", "os-integrations", "performance", "research", "test-windows-app", "user-stories", "ux-design", "windows-design"], null, "bawa", "\uE7F4"),

        new("canva", "Canva Design & Export MCP", "Дизайн-платформы", "Canva", "2.1.0",
            "Интеграция с платформой Canva через MCP: создание макетов, ресайз для соцсетей, бренд-киты и экспорт.", 11,
            ["canva-brand-check", "canva-bulk-create", "canva-edit-design", "canva-resize-for-social-media"], "canva_canva", null, "\uE790"),

        new("chrome-devtools-plugin", "Chrome DevTools & a11y", "Тестирование и DevTools", "Google Chrome", "1.4.0",
            "Профилировщик Chrome через MCP: замеры Core Web Vitals, аудит доступности WCAG, поиск утечек памяти.", 6,
            ["a11y-debugging", "chrome-devtools", "cookie-debugging", "memory-leak-debugging", "troubleshooting"], "chrome-devtools", null, "\uE774"),

        new("coderabbit", "CodeRabbit AI Review & Autofix", "Код-ревью и аудит", "CodeRabbit", "0.9.5",
            "Глубокий анализ исходного кода, аудит безопасности пул-реквестов и автоматическое применение исправлений.", 2,
            ["autofix", "code-review"], null, "coderabbit", "\uE896"),

        new("figma", "Figma Design to Code & Connect", "Дизайн-системы", "Figma", "3.0.0",
            "Двусторонняя интеграция с Figma: конвертация фреймов в код (React, WinUI, SwiftUI), FigJam диаграммы и токены.", 18,
            ["figma-code-connect", "figma-create-new-file", "figma-design-to-code", "figma-generate-diagram", "figma-shaders", "figma-swiftui", "figma-use"], "figma_figma", null, "\uE790"),

        new("firebase", "Google Firebase Full-Stack", "Бэкенд и облачные сервисы", "Google Firebase", "2.2.0",
            "Облачный бэкенд Google Firebase: Firebase Auth, Firestore Security Rules, Gemini AI Logic, App Hosting.", 13,
            ["firebase-ai-logic-basics", "firebase-auth-basics", "firebase-firestore", "firebase-security-rules-auditor", "firestore-rules-creation"], null, "firebase", "\uE753"),

        new("flutter", "Flutter & Dart Mobile SDK", "Мобильная разработка", "Google Flutter Team", "3.24.0",
            "Инструменты разработки кроссплатформенных приложений на Flutter и Dart: адаптивная верстка, превью виджетов.", 25,
            ["flutter-add-widget-preview", "flutter-apply-architecture-best-practices", "flutter-build-responsive-layout", "flutter-fix-layout-issues"], null, "flutter", "\uE8EA"),

        new("gemini-api", "Google Gemini API & Docs", "Искусственный интеллект", "Google Gemini Team", "1.5.0",
            "Официальная интеграция с семейством моделей Google Gemini 2.5 Flash, Pro и Live Audio API через SDK.", 3,
            ["gemini-api-dev", "gemini-live-api-dev", "gemini-omni-flash-api"], "gemini-api_gemini-api-docs", null, "\uE945"),

        new("mobbin", "Mobbin UI/UX Real-World References", "Дизайн-референсы", "Mobbin", "2.0.0",
            "Интеллектуальный поиск по каталогу 600,000+ скриншотов и пользовательских сценариев реальных топовых продуктов.", 4,
            ["best-practices", "plain-writing", "redesign-screen", "search"], "mobbin_mobbin", null, "\uEB9F"),

        new("stitch-build", "Google Stitch Code Generation", "Инструменты Stitch AI", "Google Stitch Team", "1.0.0",
            "Генерация чистого TypeScript/React и WinUI C# кода, дашбордов TanStack Query и Remotion видео из Stitch-макетов.", 5,
            ["react-vite-dashboard", "remotion", "shadcn-ui", "stitch::react-components", "stitch::react-native"], "stitch", null, "\uE70F"),

        new("stitch-design", "Google Stitch Design System", "Инструменты Stitch AI", "Google Stitch Team", "1.0.0",
            "Управление дизайн-системами в Stitch: конвертация кода в макеты, извлечение static HTML и синхронизация экранов.", 6,
            ["stitch::code-to-design", "stitch::extract-design-md", "stitch::generate-design", "stitch::manage-design-system"], "stitch", null, "\uE790"),

        new("stitch-utilities", "Google Stitch Utilities & Prompts", "Инструменты Stitch AI", "Google Stitch Team", "1.0.0",
            "Утилиты для авторов Stitch: улучшение UI-промптов, синтез конституции сайта SITE.md и автономный цикл генерации.", 5,
            ["design-md", "enhance-prompt", "site-md", "stitch-loop", "taste-design"], "stitch", null, "\uE713"),

        new("supabase", "Supabase PostgreSQL & Edge MCP", "Бэкенд и базы данных", "Supabase", "2.0.0",
            "Полное управление Supabase из среды Antigravity: SQL-запросы, RLS политики безопасности, миграции, Edge Functions.", 2,
            ["supabase", "supabase-postgres-best-practices"], "supabase_mcp", null, "\uE753"),

        new("superpowers", "Superpowers Engineering System", "Инженерные суперсилы", "Antigravity Superpowers", "2.0.0",
            "Дисциплинированная методология: дивергентный брейншторминг, Test-Driven Development (TDD), дебаггинг и субагенты.", 15,
            ["brainstorming", "dispatching-parallel-agents", "executing-plans", "subagent-driven-development", "systematic-debugging", "test-driven-development", "writing-plans"], null, null, "\uE9D9"),

        new("vercel-plugin", "Vercel Deployment & AI Cloud", "Vercel, Cloud & Edge", "Vercel", "3.2.0",
            "Облачный стек Vercel: Vercel AI SDK, развертывание проектов, превью-URL, KV/Blob хранилище, MicroVM Sandbox.", 38,
            ["ai-sdk", "build-agents", "deployments-cicd", "domains", "env-vars", "runtime-cache", "vercel-agent", "vercel-sandbox", "workflow"], null, "vercel", "\uE753")
    ];

    public static readonly AntigravitySkillInfo[] CuratedSkills =
    [
        new("enhance-prompt", "Промпты и спецификации", "Превращает идеи в эталонные Stitch-промпты с 3-частной структурой.", "Промпт-транслятор: Atmosphere Header + DESIGN SYSTEM + numbered Page Structure.", "stitch-utilities", true),
        new("design-md", "Дизайн-системы", "Синтез семантической дизайн-системы в DESIGN.md для Google Stitch.", "Извлекает цветовую палитру, геометрию, typography и токены компонентов.", "stitch-utilities", true),
        new("emil-design-eng", "UI/UX полишинг", "Философия Emil Kowalski: пружинные анимации sub-250ms и тактильный отклик.", "Детальный гайдлайн по микроинтеракциям, отсутствию layout shift и кольцам фокуса.", null, true),
        new("taste-design", "Эстетика и дизайн", "Курированные визуальные системы: Claude Paper, Codex Minimal, Terracotta, Risograph.", "Предотвращает шаблонный дизайн и шаблонные фиолетовые градиенты.", "stitch-utilities", true),
        new("csharp-winui", "Windows & WinUI 3", "Правила разработки для Windows 11: MVVM, x:Bind, SQLite WAL, Mica, DPI.", "Гайдлайны создания высокопроизводительных нативных приложений под Windows на C# и .NET 10.", "bawa", true),
        new("windows-design", "Windows & WinUI 3", "Fluent Design гайдлайны: 4 epx сетка, NavigationView, Mica, Segoe Fluent Icons.", "Рекомендации Microsoft по созданию нативного интерфейса Windows 11.", "bawa", true),
        new("a11y-debugging", "Качество и тестирование", "Аудит доступности WCAG AA, фокус с клавиатуры и контрастность.", "Проверка навигации с клавиатуры, скринридеров и доступных меток.", "chrome-devtools-plugin", true),
        new("subagent-driven-development", "Инженерные суперсилы", "Параллельное исполнение планов с изолированными воркспейсами.", "Автономная координация субагентов для крупных архитектурных задач.", "superpowers", true),
        new("test-driven-development", "Инженерные суперсилы", "Разработка через тестирование (TDD) до написания бизнес-кода.", "Гарантия отсутствия регрессий в критических сценариях.", "superpowers", true),
        new("systematic-debugging", "Инженерные суперсилы", "Систематический научный поиск причин багов без предположений.", "Логирование, воспроизведение дефекта и атомарное исправление.", "superpowers", true)
    ];
}
