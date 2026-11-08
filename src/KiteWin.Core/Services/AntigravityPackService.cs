using System.Text;

namespace KiteWin.Core.Services;

public record AntigravityWave(
    int Id,
    string Title,
    string Subtitle,
    string EstimatedTokens,
    IReadOnlyList<string> Scope,
    IReadOnlyList<string> NegativeScope,
    string Prompt
);

public record ReadinessCheckItem(
    string Id,
    string Title,
    bool IsPassed,
    string Suggestion
);

public record AntigravityReadinessReport(
    int Score,
    IReadOnlyList<ReadinessCheckItem> Items
)
{
    public bool IsFlightReady => Score >= 80;
}

public static class AntigravityReadinessLinter
{
    public static AntigravityReadinessReport Evaluate(
        string title,
        string concept,
        string platform,
        string style)
    {
        var items = new List<ReadinessCheckItem>();
        var passedCount = 0;

        var cleanConcept = (concept ?? string.Empty).Trim();
        var conceptOk = cleanConcept.Length >= 20;
        if (conceptOk) passedCount++;
        items.Add(new ReadinessCheckItem(
            "concept-clarity",
            "Конкретность описания идеи",
            conceptOk,
            conceptOk ? "Идея четко сформулирована" : "Добавьте больше деталей ключевого пользовательского сценария"
        ));

        var platformOk = !string.IsNullOrWhiteSpace(platform);
        if (platformOk) passedCount++;
        items.Add(new ReadinessCheckItem(
            "platform-bound",
            "Целевая платформа определена",
            platformOk,
            platformOk ? $"Платформа: {platform}" : "Выберите целевую платформу (Web, Desktop, Mobile)"
        ));

        var styleOk = !string.IsNullOrWhiteSpace(style);
        if (styleOk) passedCount++;
        items.Add(new ReadinessCheckItem(
            "style-tokens",
            "Визуальная система и дизайн-система",
            styleOk,
            styleOk ? $"Стиль: {style}" : "Задайте эстетику для исключения бесформенного UI"
        ));

        var lowerConcept = cleanConcept.ToLowerInvariant();
        var hasPitfalls = lowerConcept.Contains("auth") ||
                          lowerConcept.Contains("payment") ||
                          lowerConcept.Contains("stripe") ||
                          lowerConcept.Contains("pay") ||
                          lowerConcept.Contains("login");
        var pitfallResolved = !hasPitfalls || cleanConcept.Length > 80;
        if (pitfallResolved) passedCount++;
        items.Add(new ReadinessCheckItem(
            "pitfall-shield",
            "Защита от критических ловушек (Auth/Billing)",
            pitfallResolved,
            pitfallResolved ? "Архитектура защищена от преждевременного усложнения" : "Детализируйте модель авторизации/оплаты для снижения рисков"
        ));

        var totalChecks = 4;
        var score = (int)Math.Round((double)passedCount / totalChecks * 100);
        return new AntigravityReadinessReport(score, items);
    }
}

public static class AntigravityPackGenerator
{
    public static IReadOnlyList<AntigravityWave> BuildWaves(
        string title,
        string concept,
        string platform,
        string style)
    {
        var cleanTitle = string.IsNullOrWhiteSpace(title) ? "Проект" : title.Trim();
        var cleanConcept = string.IsNullOrWhiteSpace(concept) ? "Реализация проекта" : concept.Trim();

        var wave1Prompt = $"""
        [ANTIGRAVITY WAVE 1 / 4] АРХИТЕКТУРНЫЙ КАРКАС И МОДЕЛИ
        Проект: {cleanTitle}
        Платформа: {platform}
        Концепция: {cleanConcept}

        ЗАДАЧА ЭТОЙ ВОЛНЫ:
        1. Создать базовую структуру папок проекта и файл конфигурации сборки.
        2. Определить строгие модели предметной области с типами полей и первичными ключами.
        3. Настроить локальное персистентное хранилище (JSON/SQLite/KV) без тяжелых зависимостей.
        4. Написать проверочный тест компиляции моделей.

        СТРОГИЕ ОГРАНИЧЕНИЯ:
        - НЕ верстай пользовательский интерфейс на этом шаге.
        - НЕ подключай сторонние облачные сервисы и тяжелые ORM, если не требовалось явно.
        - Выполни компиляцию и проверь корректность типов.

        ПОСЛЕ ВЫПОЛНЕНИЯ:
        Выведи структуру созданных файлов и точную команду сборки/теста. ОСТАНОВИСЬ и жди команды для перехода к Волне 2.
        """;

        var wave1 = new AntigravityWave(
            1,
            "Волна 1: Схема и Модели данных",
            "Инициализация каркаса, строгая типизация и хранилище",
            "~1.2k токенов (высокая экономия)",
            new[]
            {
                "Файловая структура и конфигурация сборки",
                "Модели сущностей и типы полей",
                "Персистентность (локальное хранилище данных)",
                "Проверочный юнит-тест записи/чтения"
            },
            new[]
            {
                "Запрещено верстать сложные экраны",
                "Запрещено устанавливать избыточные библиотеки",
                "Запрещено настраивать внешние облачные API"
            },
            wave1Prompt
        );

        var wave2Prompt = $"""
        [ANTIGRAVITY WAVE 2 / 4] БИЗНЕС-ЛОГИКА И СЕРВИСЫ
        Проект: {cleanTitle}
        Стек: {platform}

        ЗАДАЧА ЭТОЙ ВОЛНЫ:
        1. Реализовать сервисный слой для обработки ключевого пользовательского сценария:
           {cleanConcept}
        2. Сконфигурировать однонаправленный поток данных (State Store / Observable ViewModel).
        3. Обработать граничные случаи: пустое состояние, состояние загрузки, валидация ввода.

        СТРОГИЕ ОГРАНИЧЕНИЯ:
        - НЕ переписывай модели данных из Волны 1, расширяй их только при необходимости.
        - Сохраняй чистоту функций: бизнес-логика должна тестироваться автономно от графического интерфейса.

        ПОСЛЕ ВЫПОЛНЕНИЯ:
        Покажи пример вызова логики и подтверди отсутствие ошибок типизации. ОСТАНОВИСЬ и жди перехода к Волне 3.
        """;

        var wave2 = new AntigravityWave(
            2,
            "Волна 2: Логика и Состояние",
            "Сервисы, мутации данных, валидация и бизнес-правила",
            "~1.5k токенов (точный фокус)",
            new[]
            {
                "Сервисы для сценария пользователя",
                "Менеджер состояния приложения",
                "Обработка ошибок и валидация",
                "Реактивные потоки обновления"
            },
            new[]
            {
                "Запрещено ломать контракты моделей из Волны 1",
                "Запрещено усложнять стейт-менеджмент внешними фреймворками",
                "Не приступать к финальной верстке до завершения"
            },
            wave2Prompt
        );

        var wave3Prompt = $"""
        [ANTIGRAVITY WAVE 3 / 4] ВЕРСТКА И ИНТЕРФЕЙС
        Проект: {cleanTitle} ({platform})
        Эстетика: {style}

        ДИЗАЙН-СТАНДАРТЫ:
        - Сетка: строгая модульная сетка 4pt/8pt. Touch targets >= 44px.
        - Микро-взаимодействия (Emil Kowalski): физические пружины (:active scale-[0.98]), четкие кольца фокуса.
        - Доступность: контрастность по WCAG AA, навигация с клавиатуры.

        ЗАДАЧА ЭТОЙ ВОЛНЫ:
        1. Сверстать главный экран и вспомогательные панели.
        2. Привязать UI к сервисам и стейту из Волны 2.
        3. Обеспечить корректное отображение состояний: загрузка, данные, пустой список, ошибка.
        4. Добавить клавиатурные шорткаты (Ctrl+K/Cmd+K быстрый ввод, Escape для закрытия).

        СТРОГИЕ ОГРАНИЧЕНИЯ:
        - Никакого «AI-слопа»: не оставляй серых плейсхолдеров, используй точные цвета и выверенные отступы.
        - Не используй эмодзи в интерфейсе: только векторные или нативные глифы.

        ПОСЛЕ ВЫПОЛНЕНИЯ:
        Дай команду для запуска приложения и покажи скриншот или инструкцию для просмотра. ОСТАНОВИСЬ.
        """;

        var wave3 = new AntigravityWave(
            3,
            "Волна 3: Интерфейс и Дизайн-токены",
            "Верстка экранов, сетка 4pt/8pt, анимации и привязка к стейту",
            "~2.0k токенов (высокая точность)",
            new[]
            {
                "Главный экран и навигационная структура",
                "Применение токенов дизайн-системы",
                "Шорткаты и доступность",
                "Связка UI со стейтом Волны 2"
            },
            new[]
            {
                "Запрещены эмодзи в элементах интерфейса",
                "Запрещены случайные не-кратные отступы (только 4pt/8pt)",
                "Запрещены заглушки без обработки действий"
            },
            wave3Prompt
        );

        var wave4Prompt = $"""
        [ANTIGRAVITY WAVE 4 / 4] АУДИТ КАЧЕСТВА И ПОЛИРОВКА
        Проект: {cleanTitle} ({platform})

        ЗАДАЧА ЭТОЙ ВОЛНЫ:
        1. Провести полный цикл проверки сборки и тестов:
           - Проверка типов и линтинг (0 ошибок, 0 предупреждений).
           - Запуск существующих юнит-тестов.
        2. Аудит доступности (a11y):
           - Проверка контрастности цветов по стандарту WCAG AA.
           - Навигация с клавиатуры (Tab-focus, Escape).
        3. Очистить проект от неиспользуемых импортов, временных файлов и логов.
        4. Сформировать итоговый README с инструкцией по запуску и тестированию.

        ПОСЛЕ ВЫПОЛНЕНИЯ:
        Выведи статус прохождения Quality Gate: «ALL CHECKS PASSED» и финальные команды для сборки.
        """;

        var wave4 = new AntigravityWave(
            4,
            "Волна 4: Тесты, Проверка и Доступность",
            "Quality Gate, аудит a11y, очистка от мусора и финальный билд",
            "~1.0k токенов (быстрый аудит)",
            new[]
            {
                "Запуск тестов и линтера",
                "Аудит контрастности и доступности",
                "Очистка неиспользуемого кода",
                "Инструкция по развертыванию"
            },
            new[]
            {
                "Запрещено добавлять новые неоговоренные функции",
                "Запрещено игнорировать ошибки компилятора"
            },
            wave4Prompt
        );

        return new[] { wave1, wave2, wave3, wave4 };
    }

    public static string GenerateAntigravityMd(
        string title,
        string concept,
        string platform,
        string style)
    {
        var cleanTitle = string.IsNullOrWhiteSpace(title) ? "Проект" : title.Trim();
        var cleanConcept = string.IsNullOrWhiteSpace(concept) ? "Реализация проекта" : concept.Trim();
        var waves = BuildWaves(title, concept, platform, style);
        var readiness = AntigravityReadinessLinter.Evaluate(title, concept, platform, style);

        var sb = new StringBuilder();
        sb.AppendLine($"# ANTIGRAVITY EXECUTION PLAN: {cleanTitle.ToUpperInvariant()}");
        sb.AppendLine($"Target Platform: {platform}");
        sb.AppendLine($"Aesthetic & Style: {style}");
        sb.AppendLine($"Flight Readiness Score: {readiness.Score}% (Status: {(readiness.IsFlightReady ? "FLIGHT READY" : "REFINEMENT RECOMMENDED")})");
        sb.AppendLine();
        sb.AppendLine("## 1. Project Concept & Objectives");
        sb.AppendLine(cleanConcept);
        sb.AppendLine();
        sb.AppendLine("## 2. Readiness Check Matrix");
        foreach (var item in readiness.Items)
        {
            var mark = item.IsPassed ? "[X]" : "[ ]";
            sb.AppendLine($"- {mark} **{item.Title}**: {item.Suggestion}");
        }
        sb.AppendLine();
        sb.AppendLine("## 3. Strict Execution Waves (Progressive Token-Optimal Waves)");
        sb.AppendLine("Execute each wave strictly in sequence. Do NOT jump ahead to future waves until the current wave passes tests.");
        sb.AppendLine();

        foreach (var wave in waves)
        {
            sb.AppendLine($"### {wave.Title} ({wave.EstimatedTokens})");
            sb.AppendLine($"*Purpose*: {wave.Subtitle}");
            sb.AppendLine();
            sb.AppendLine("**Scope (Must Build)**:");
            foreach (var s in wave.Scope)
            {
                sb.AppendLine($"- [ ] {s}");
            }
            sb.AppendLine();
            sb.AppendLine("**Negative Scope Firewall (Forbidden)**:");
            foreach (var ns in wave.NegativeScope)
            {
                sb.AppendLine($"- {ns}");
            }
            sb.AppendLine();
            sb.AppendLine("```markdown");
            sb.AppendLine(wave.Prompt.Trim());
            sb.AppendLine("```");
            sb.AppendLine();
        }

        sb.AppendLine("## 4. Master Prompt for Google DeepMind Antigravity");
        sb.AppendLine("Feed this starter prompt into Antigravity or your AI agent terminal:");
        sb.AppendLine();
        sb.AppendLine("```markdown");
        sb.AppendLine($"You are executing the project '{cleanTitle}' on {platform} following the Apple HIG / BAWA Craftsmanship Standard.");
        sb.AppendLine("Start with WAVE 1 only. Read ANTIGRAVITY.md in full. Commit each wave atomically with conventional commits.");
        sb.AppendLine("```");

        return sb.ToString();
    }
}
