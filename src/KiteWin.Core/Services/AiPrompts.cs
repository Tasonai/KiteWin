using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace KiteWin.Core.Services;

public sealed record InterviewQuestion(string Question, IReadOnlyList<string> Options);

public sealed record ChatContext(
    IReadOnlyList<string> Ideas,
    string? SelectedIdea = null,
    string? Canvas = null
);

/// <summary>
/// Prompts sent to AI models for specifications, interview questions, and chat.
/// Pure functions and strict zero emojis.
/// </summary>
public static class AiPrompts
{
    public const string SpecificityRules = """
    ПРАВИЛА КОНКРЕТНОСТИ (нарушение недопустимо):
    - Используй слова и сущности из запроса автора. Выводи из них конкретику: названия экранов и компонентов, поля данных с типами, горячие клавиши, состояния (пусто, загрузка, ошибка), числа (отступы в epx/pt, длительности в мс, размеры в px).
    - Если автор что-то не указал, выбери одно конкретное решение и пометь его строкой «Допущение: ...», чтобы автор мог поправить. Не оставляй пробелов общими словами.
    - Запрещены пустые обороты без измеримого смысла: «интуитивный интерфейс», «современный дизайн», «высокая производительность», «удобный», «масштабируемый», «лучшие практики», «бесшовный». Заменяй их проверяемым требованием.
    - Не пересказывай запрос и не объясняй, что ты делаешь. Сразу выдавай результат.
    - Не используй эмодзи.
    """;

    public static string ModeInstructions(string mode) => mode.ToLowerInvariant() switch
    {
        "stitch" => """
        ФОРМАТ: один готовый промпт для Google Stitch на английском языке, без вступлений.
        1. Первое предложение: что за экран и для кого, одной фразой с конкретным предметом.
        2. Блок DESIGN SYSTEM: используй переданные токены дословно (цвета с hex, шрифты, радиусы).
        3. Блок SCREENS: перечисли 2-4 экрана; для каждого дай реальные тексты интерфейса (заголовки, подписи кнопок, строки списка), а не заглушки вроде Lorem ipsum.
        4. Блок STATES: пустое, загрузка, ошибка хотя бы для главного экрана.
        """,

        "targeted" or "edit" => """
        ФОРМАТ: хирургическая правка, а не новый план.
        1. «Цель»: какой именно компонент или файл меняется (определи по запросу, если не назван: предположи и пометь допущением).
        2. «Изменить»: точный список изменений, каждое с до/после (значение, текст, поведение).
        3. «Не трогать»: что должно остаться без изменений.
        4. «Проверка»: как убедиться, что правка сработала (действие и ожидаемый результат).
        Не более 200 слов.
        """,

        "brief" or "concise" => """
        ФОРМАТ: бриф до 120 слов, три абзаца: Задача (что строим, для кого), Решение (ключевые технические выборы с названиями), Риски (две конкретные проблемы и как их закрыть). Без заголовков второго уровня.
        """,

        "actions" => """
        ФОРМАТ: нумерованный список из 6-10 действий для агента-разработчика. Каждое действие начинается с глагола, называет файл, компонент или команду и заканчивается проверкой в скобках (команда или наблюдаемый результат). Без вводного текста.
        """,

        _ => """
        ФОРМАТ: инженерный план в Markdown. Заголовки разделов выбери сам под этот проект, не копируй универсальный шаблон. Обязательно присутствуют:
        - модель данных: сущности и поля с типами;
        - список экранов или модулей с их состояниями;
        - 3 наиболее рискованных места с конкретным способом проверки;
        - порядок работ волнами, каждая волна заканчивается проверяемым результатом;
        - критерии приемки: пять проверок в формате «действие, ожидаемый результат».
        В конце раздел «Открытые вопросы автора» (не более 3 вопросов, от которых решение зависит сильнее всего).
        """
    };

    public static string BuildTranslatorPrompt(
        string mode,
        string rawPrompt,
        string platform,
        string style,
        IReadOnlyList<string>? skills = null)
    {
        var tokens = ItemTitle.GetTokens(style, platform);
        var effectiveSkills = skills != null && skills.Count > 0
            ? "/plan " + string.Join(" ", skills)
            : "/plan /site-md /design-md /emil-design-eng /gsd-execute-phase";

        var idea = (rawPrompt ?? "").Trim();
        if (string.IsNullOrEmpty(idea))
        {
            idea = "(пусто: предложи одну конкретную идею в рамках платформы и стиля и пометь это допущением)";
        }

        return $"""
        Ты превращаешь сырую идею в точный рабочий промпт для агента-разработчика. Ты не пишешь эссе о проекте: ты выдаешь сам промпт, готовый к вставке в агента.

        {SpecificityRules}

        {ModeInstructions(mode)}

        Первая строка ответа, если формат это допускает: {effectiveSkills}

        ЗАДАННЫЙ КОНТЕКСТ
        Платформа: {platform}
        Визуальная система: {style} ({tokens.Theme})
        Токены: фон {tokens.Background}; поверхность {tokens.Surface}; акцент {tokens.PrimaryAccent} ({tokens.AccentRole}); основной текст {tokens.TextPrimary}; вторичный {tokens.TextSecondary}; кнопки: {tokens.Buttons}; карточки: {tokens.Cards}; типографика: {tokens.Typography}

        ИДЕЯ АВТОРА (дословно):
        {idea}

        Ответ на русском. Идентификаторы, команды и код оставляй на английском.
        """;
    }

    public static string BuildInterviewPrompt(string idea, string platform, string style)
    {
        return $"""
        Ты готовишь точный промпт для агента-разработчика по сырой идее автора. Прежде чем писать его, выбери два или три вопроса, ответы на которые сильнее всего изменят результат.

        ТРЕБОВАНИЯ К ВОПРОСАМ
        - Вопрос привязан к этой идее: называет ее сущности, а не спрашивает общее («для кого продукт?», «какие цели?»).
        - Ответ нельзя угадать из идеи, и он меняет архитектуру, модель данных или состав экранов.
        - У каждого вопроса два или три коротких варианта ответа (до шести слов), которые автор выбирает одним нажатием.
        - Не спрашивай про платформу и визуальный стиль: они уже выбраны ({platform}, {style}).
        - Формулировка вопроса до 15 слов. Без эмодзи.

        ФОРМАТ ОТВЕТА: только JSON-массив, без текста вокруг и без блока кода:
        [{{"question": "...", "options": ["...", "..."]}}]

        ИДЕЯ АВТОРА:
        {idea}
        """;
    }

    public static IReadOnlyList<InterviewQuestion> ParseInterview(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return Array.Empty<InterviewQuestion>();

        var start = text.IndexOf('[');
        var end = text.LastIndexOf(']');
        if (start < 0 || end <= start) return Array.Empty<InterviewQuestion>();

        var json = text.Substring(start, end - start + 1);
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Array) return Array.Empty<InterviewQuestion>();

            var result = new List<InterviewQuestion>();
            foreach (var el in doc.RootElement.EnumerateArray())
            {
                if (!el.TryGetProperty("question", out var qProp)) continue;
                var q = qProp.GetString()?.Trim();
                if (string.IsNullOrEmpty(q)) continue;

                var options = new List<string>();
                if (el.TryGetProperty("options", out var optsProp) && optsProp.ValueKind == JsonValueKind.Array)
                {
                    foreach (var opt in optsProp.EnumerateArray())
                    {
                        var s = opt.GetString()?.Trim();
                        if (!string.IsNullOrEmpty(s)) options.Add(s);
                        if (options.Count >= 3) break;
                    }
                }
                result.Add(new InterviewQuestion(q, options));
                if (result.Count >= 3) break;
            }
            return result;
        }
        catch
        {
            return Array.Empty<InterviewQuestion>();
        }
    }

    public static string Enrich(string idea, IReadOnlyList<(string Question, string Answer)> answers)
    {
        var given = answers
            .Where(a => !string.IsNullOrWhiteSpace(a.Answer))
            .Select(a => $"- {a.Question} {a.Answer.Trim()}")
            .ToList();

        if (given.Count == 0) return idea;

        var sb = new StringBuilder();
        sb.AppendLine(idea);
        sb.AppendLine();
        sb.AppendLine("Уточнения автора (считай их фактами, не допущениями):");
        foreach (var line in given) sb.AppendLine(line);
        sb.AppendLine();
        sb.AppendLine("Обязательно: добавь раздел «Принятые решения автора». В нем по одному пункту на каждое уточнение: что решено и что из-за этого исключено из состава работ. Не включай в план ничего, что противоречит этим решениям (поля, экраны, интеграции).");
        return sb.ToString().TrimEnd();
    }

    public const string KiteFacts = """
    ФАКТЫ О ПРОЕКТАХ KITE (проверены по коду, опирайся на них):
    - Kite для macOS: Swift 6, SwiftUI, SwiftData. Локальный HTTP-сервер на Network.framework для P2P-синхронизации (порт 24242).
    - KiteWin для Windows: C# / .NET 10, WinUI 3, Windows App SDK. Локальный HTTP-сервер на HttpListener для P2P-синхронизации (порт 24243).
    - Архитектура P2P: прямой обмен между устройствами в локальной сети, без серверов, облаков и учетных записей.
    - База данных: локальный SQLite в режиме WAL, поиск через FTS5 с ранжированием bm25.
    - Дизайн-системы: 10 встроенных стилей (Editorial, Claude Paper, Codex Minimal, Terracotta, Risograph, Linear Cockpit, Neo-Brutalism, Voxel, Dark Glassmorphism).
    - Все данные хранятся локально; ключ Gemini хранится в настройках приложения.
    """;

    public static string BuildChatSystemPrompt(ChatContext context)
    {
        var ideasBlock = context.Ideas.Count == 0
            ? "Каталог идей пока пуст."
            : "Идеи пользователя в каталоге (последние):\n" + string.Join("\n", context.Ideas.Take(12).Select(i => "- " + i));

        var selectedBlock = string.IsNullOrWhiteSpace(context.SelectedIdea)
            ? ""
            : $"\n\nСейчас у пользователя открыта идея (вопрос, скорее всего, о ней):\n{context.SelectedIdea.Trim()}";

        var canvasBlock = string.IsNullOrWhiteSpace(context.Canvas)
            ? ""
            : $"\n\nХолст идей пользователя:\n{context.Canvas.Trim()}";

        return $"""
        Ты — главный инженер и архитектор в приложении KiteWin на Windows 11. Помогаешь пользователю проектировать и строить продукты. У тебя есть собственное мнение, ты его защищаешь.

        КАК ТЫ ОТВЕЧАЕШЬ
        1. Сначала решение, потом обоснование. Первая фраза ответа — твоя рекомендация, а не пересказ вопроса.
        2. Выбирай один вариант и называй цену выбора: что пользователь теряет. Не перечисляй «плюсы и минусы» ради баланса.
        3. Если вопрос расплывчатый и от ответа зависит решение, задай от одного до трех конкретных вопросов и остановись. Не выдавай универсальный план вместо вопросов.
        4. Опирайся на идеи пользователя ниже, когда они относятся к делу, и называй их по имени.
        5. Давай проверяемые вещи: названия типов и файлов, значения, команды. Код приводи только когда он короче описания.
        6. Запрещены пустые обороты: «лучшие практики», «современный подход», «интуитивный», «масштабируемый», «бесшовный». Если хочется их написать, замени проверяемым утверждением.
        7. Не утверждай фактов о проектах пользователя, которых нет в списке идей ниже: как устроена их синхронизация, есть ли у них сервер, какой у них стек. Если ответ зависит от такого факта, спроси. Не называй сроки и цифры, которые не следуют из сказанного.
        8. Рекомендация и цена выбора не должны спорить друг с другом. Если советуешь вариант, не предлагай в том же ответе шагов, которые его отменяют. Перед отправкой перечитай ответ и убери такие противоречия.
        9. Отвечай на русском, коротко. Для простого вопроса достаточно трех предложений. Не используй эмодзи.

        СТЕК, КОТОРЫЙ ТЫ ЗНАЕШЬ ХОРОШО: WinUI 3, C#, .NET 10, Windows App SDK, SwiftUI, Swift 6, SQLite FTS5, React 19, TypeScript. Если вопрос вне этого стека, скажи, где твоя уверенность заканчивается.

        СКИЛЛЫ ANTIGRAVITY: упоминай команду со слешем (/site-md, /design-md, /taste-design, /emil-design-eng, /a11y-debugging, /gsd-execute-phase, /bawa, /bama) только если она решает задачу пользователя прямо сейчас, и объясни, зачем.

        {KiteFacts}

        {ideasBlock}{selectedBlock}{canvasBlock}
        """;
    }
}
