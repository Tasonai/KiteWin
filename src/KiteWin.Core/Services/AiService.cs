using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace KiteWin.Core.Services;

public enum AiProvider
{
    Gemini,
    Ollama,
    Offline
}

public sealed class AiSettings
{
    public AiProvider Provider { get; set; } = AiProvider.Gemini;
    public string GeminiApiKey { get; set; } = "";
    public string GeminiModel { get; set; } = "gemini-2.5-flash";
    public string OllamaEndpoint { get; set; } = "http://localhost:11434/api/generate";
    public string OllamaModel { get; set; } = "llama3";
}

public interface IAiService
{
    Task<string> GenerateSpecificationAsync(Item item, AiSettings settings, CancellationToken ct = default);
    Task<bool> TestConnectionAsync(AiSettings settings, CancellationToken ct = default);
}

public sealed class AiService : IAiService, IDisposable
{
    private readonly HttpClient _http;
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public AiService()
    {
        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(25) };
        _http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }

    public async Task<string> GenerateSpecificationAsync(Item item, AiSettings settings, CancellationToken ct = default)
    {
        var cleanKey = settings.GeminiApiKey.Trim();

        if (settings.Provider == AiProvider.Gemini && !string.IsNullOrEmpty(cleanKey))
        {
            try
            {
                return await CallGeminiAsync(item, settings, cleanKey, ct);
            }
            catch (Exception ex)
            {
                // Fall back to offline compiler if API error occurs
                var offline = GenerateOfflineBlueprint(item);
                return $"<!-- Предупреждение API: {ex.Message} (использован локальный компилятор) -->\n\n" + offline;
            }
        }

        if (settings.Provider == AiProvider.Ollama)
        {
            try
            {
                return await CallOllamaAsync(item, settings, ct);
            }
            catch (Exception ex)
            {
                var offline = GenerateOfflineBlueprint(item);
                return $"<!-- Предупреждение Ollama: {ex.Message} (использован локальный компилятор) -->\n\n" + offline;
            }
        }

        return GenerateOfflineBlueprint(item);
    }

    public async Task<bool> TestConnectionAsync(AiSettings settings, CancellationToken ct = default)
    {
        try
        {
            if (settings.Provider == AiProvider.Gemini)
            {
                var cleanKey = settings.GeminiApiKey.Trim();
                if (string.IsNullOrEmpty(cleanKey)) return false;

                var url = $"https://generativelanguage.googleapis.com/v1beta/models/{settings.GeminiModel}:generateContent?key={cleanKey}";
                var body = new
                {
                    contents = new[]
                    {
                        new { parts = new[] { new { text = "Ping. Answer with 'PONG'." } } }
                    }
                };
                var response = await _http.PostAsJsonAsync(url, body, JsonOpts, ct);
                return response.IsSuccessStatusCode;
            }

            if (settings.Provider == AiProvider.Ollama)
            {
                var body = new
                {
                    model = string.IsNullOrWhiteSpace(settings.OllamaModel) ? "llama3" : settings.OllamaModel,
                    prompt = "Ping",
                    stream = false
                };
                var response = await _http.PostAsJsonAsync(settings.OllamaEndpoint, body, JsonOpts, ct);
                return response.IsSuccessStatusCode;
            }

            return true;
        }
        catch
        {
            return false;
        }
    }

    private async Task<string> CallGeminiAsync(Item item, AiSettings settings, string apiKey, CancellationToken ct)
    {
        var model = string.IsNullOrWhiteSpace(settings.GeminiModel) ? "gemini-2.5-flash" : settings.GeminiModel;
        var url = $"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent?key={apiKey}";

        var prompt = BuildPrompt(item);

        var body = new
        {
            contents = new[]
            {
                new { parts = new[] { new { text = prompt } } }
            }
        };

        var response = await _http.PostAsJsonAsync(url, body, JsonOpts, ct);
        response.EnsureSuccessStatusCode();

        using var doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
        var text = doc.RootElement
            .GetProperty("candidates")[0]
            .GetProperty("content")
            .GetProperty("parts")[0]
            .GetProperty("text")
            .GetString();

        return text ?? GenerateOfflineBlueprint(item);
    }

    private async Task<string> CallOllamaAsync(Item item, AiSettings settings, CancellationToken ct)
    {
        var prompt = BuildPrompt(item);
        var body = new
        {
            model = string.IsNullOrWhiteSpace(settings.OllamaModel) ? "llama3" : settings.OllamaModel,
            prompt,
            stream = false
        };

        var response = await _http.PostAsJsonAsync(settings.OllamaEndpoint, body, JsonOpts, ct);
        response.EnsureSuccessStatusCode();

        using var doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
        if (doc.RootElement.TryGetProperty("response", out var respProp))
        {
            return respProp.GetString() ?? GenerateOfflineBlueprint(item);
        }

        return GenerateOfflineBlueprint(item);
    }

    private static string BuildPrompt(Item item)
    {
        return $"""
        Ты — Principal AI-Архитектор экосистемы Antigravity.
        Сформируй исчерпывающую техническую спецификацию и пошаговый план реализации для следующей идеи:

        Название: {item.Title}
        Платформа: {item.Platform}
        Стиль: {item.Style}
        Концепция: {item.Concept}

        Требования к архитектуре:
        1. Исключи любой AI-слоп (Zero AI Slop). Только практичные, рабочие технические решения.
        2. Рекомендуй профильные системные скиллы через префикс слэша: /site-md, /design-md, /taste-design, /emil-design-eng, /gsd-execute-phase, /bawa, /bama.
        3. Структура вывода:
           - Архитектурная миссия и JTBD
           - Стек технологий и дизайн-токены
           - Компонентная иерархия
           - Пошаговый план выполнения (Phases 1-4)
           - Чеклист верификации качества (WCAG AA, sub-250ms отклик, 4px grid)
        """;
    }

    public static string GenerateOfflineBlueprint(Item item)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# Архитектурная спецификация: {item.Title}");
        sb.AppendLine();
        sb.AppendLine($"- **Платформа**: {item.Platform}");
        sb.AppendLine($"- **Стиль**: {item.Style}");
        sb.AppendLine($"- **Дата формирования**: {DateTimeOffset.Now:yyyy-MM-dd HH:mm}");
        sb.AppendLine();
        sb.AppendLine("## 1. Миссия и сценарий (JTBD)");
        sb.AppendLine(string.IsNullOrWhiteSpace(item.Concept) ? "Реализация высокопроизводительного нативного решения с нулевыми задержками." : item.Concept);
        sb.AppendLine();
        sb.AppendLine("## 2. Рекомендуемые Antigravity Скиллы");
        sb.AppendLine("- `/site-md` — синтез конституции проекта и карты экранов");
        sb.AppendLine("- `/design-md` — извлечение дизайн-системы и токенов в DESIGN.md");
        sb.AppendLine("- `/emil-design-eng` — полировка тактильных микровзаимодействий и пружинных анимаций");
        sb.AppendLine("- `/gsd-execute-phase` — дисциплинированное волновое исполнение с атомарными коммитами");
        if (item.Platform.Equals("Windows", StringComparison.OrdinalIgnoreCase))
        {
            sb.AppendLine("- `/new-windows-app` — стандарт BAWA для Windows 11 / WinUI 3");
        }
        else if (item.Platform.Equals("Desktop", StringComparison.OrdinalIgnoreCase) || item.Platform.Equals("Mac", StringComparison.OrdinalIgnoreCase))
        {
            sb.AppendLine("- `/new-mac-app` — стандарт BAMA для macOS SwiftUI");
        }
        sb.AppendLine();
        sb.AppendLine("## 3. Этапы разработки (Phases)");
        sb.AppendLine("1. **Phase 1 (Foundation)**: Настройка хранилища данных и базовой разметки.");
        sb.AppendLine("2. **Phase 2 (Core Logic)**: Реактивные контроллеры и локальный кэш.");
        sb.AppendLine("3. **Phase 3 (UI Polish)**: Внедрение дизайн-токенов, сетки 4px и отклика.");
        sb.AppendLine("4. **Phase 4 (Verification)**: Запуск тестов, проверка контраста и сборка инсталляторов.");
        return sb.ToString();
    }

    public void Dispose()
    {
        _http.Dispose();
    }
}
