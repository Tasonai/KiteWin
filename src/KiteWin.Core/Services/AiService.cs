using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace KiteWin.Core.Services;

public enum AiProvider
{
    Gemini,
    OpenAI,
    Ollama,
    Offline
}

public sealed class AiSettings
{
    public AiProvider Provider { get; set; } = AiProvider.Gemini;
    public string GeminiApiKey { get; set; } = "";
    public string GeminiModel { get; set; } = "gemini-3.5-flash";
    public string OpenAIEndpoint { get; set; } = "https://api.deepseek.com/v1/chat/completions";
    public string OpenAIApiKey { get; set; } = "";
    public string OpenAIModel { get; set; } = "deepseek-chat";
    public string OllamaEndpoint { get; set; } = "http://localhost:11434/api/generate";
    public string OllamaModel { get; set; } = "llama3";
    public bool InterviewEnabled { get; set; } = true;
}

public interface IAiService
{
    Task<string> GenerateSpecificationAsync(Item item, AiSettings settings, CancellationToken ct = default);
    Task<string> GenerateSpecificationAsync(Item item, AiSettings settings, string mode, IReadOnlyList<string>? skills = null, IReadOnlyList<(string Question, string Answer)>? answers = null, CancellationToken ct = default);
    Task<IReadOnlyList<InterviewQuestion>> GenerateInterviewQuestionsAsync(Item item, AiSettings settings, CancellationToken ct = default);
    Task<string> ChatAsync(IReadOnlyList<(string Role, string Content)> messages, string systemInstruction, AiSettings settings, CancellationToken ct = default);
    Task<bool> TestConnectionAsync(AiSettings settings, CancellationToken ct = default);
}

public sealed class AiService : IAiService, IDisposable
{
    public const string DefaultGeminiModel = "gemini-3.5-flash";

    private static readonly string[] FallbackModels =
    [
        "gemini-3.5-flash",
        "gemini-3.1-flash-lite",
        "gemini-flash-latest"
    ];

    private static readonly HashSet<string> RetiredModels = new(StringComparer.OrdinalIgnoreCase)
    {
        "gemini-2.5-flash",
        "gemini-2.5-pro",
        "gemini-2.0-flash",
        "gemini-1.5-flash",
        "gemini-1.5-pro"
    };

    private readonly HttpClient _http;
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public AiService()
    {
        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(35) };
        _http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }

    public static IReadOnlyList<string> GetModelChain(string? requested)
    {
        var trimmed = (requested ?? "").Trim();
        var first = string.IsNullOrEmpty(trimmed) || RetiredModels.Contains(trimmed)
            ? DefaultGeminiModel
            : trimmed;

        var chain = new List<string> { first };
        foreach (var m in FallbackModels)
        {
            if (!chain.Contains(m, StringComparer.OrdinalIgnoreCase))
            {
                chain.Add(m);
            }
        }
        return chain;
    }

    public Task<string> GenerateSpecificationAsync(Item item, AiSettings settings, CancellationToken ct = default)
    {
        return GenerateSpecificationAsync(item, settings, "plan", null, null, ct);
    }

    public async Task<string> GenerateSpecificationAsync(
        Item item,
        AiSettings settings,
        string mode,
        IReadOnlyList<string>? skills = null,
        IReadOnlyList<(string Question, string Answer)>? answers = null,
        CancellationToken ct = default)
    {
        var cleanKey = settings.GeminiApiKey.Trim();

        var promptBase = AiPrompts.BuildTranslatorPrompt(
            mode: mode,
            rawPrompt: string.IsNullOrWhiteSpace(item.Concept) ? item.Title : $"{item.Title}. {item.Concept}",
            platform: item.Platform,
            style: item.Style,
            skills: skills
        );

        var finalPrompt = answers != null && answers.Count > 0
            ? AiPrompts.Enrich(promptBase, answers)
            : promptBase;

        if (settings.Provider == AiProvider.Gemini && !string.IsNullOrEmpty(cleanKey))
        {
            try
            {
                var thinking = (mode == "brief" || mode == "concise" || mode == "actions" || mode == "targeted" || mode == "edit")
                    ? "low"
                    : null;

                return await CallGeminiGenerateAsync(finalPrompt, settings, cleanKey, thinking, ct);
            }
            catch (Exception ex)
            {
                var offline = GenerateOfflineBlueprint(item);
                return $"<!-- Предупреждение API: {ex.Message} (использован локальный компилятор) -->\n\n" + offline;
            }
        }

        if (settings.Provider == AiProvider.OpenAI)
        {
            try
            {
                return await CallOpenAIGenerateAsync(finalPrompt, settings, ct);
            }
            catch (Exception ex)
            {
                var offline = GenerateOfflineBlueprint(item);
                return $"<!-- Предупреждение OpenAI/DeepSeek: {ex.Message} (использован локальный компилятор) -->\n\n" + offline;
            }
        }

        if (settings.Provider == AiProvider.Ollama)
        {
            try
            {
                return await CallOllamaGenerateAsync(finalPrompt, settings, ct);
            }
            catch (Exception ex)
            {
                var offline = GenerateOfflineBlueprint(item);
                return $"<!-- Предупреждение Ollama: {ex.Message} (использован локальный компилятор) -->\n\n" + offline;
            }
        }

        return GenerateOfflineBlueprint(item);
    }

    public async Task<IReadOnlyList<InterviewQuestion>> GenerateInterviewQuestionsAsync(
        Item item,
        AiSettings settings,
        CancellationToken ct = default)
    {
        var cleanKey = settings.GeminiApiKey.Trim();
        if (settings.Provider != AiProvider.Gemini || string.IsNullOrEmpty(cleanKey))
        {
            return Array.Empty<InterviewQuestion>();
        }

        var idea = string.IsNullOrWhiteSpace(item.Concept) ? item.Title : $"{item.Title}. {item.Concept}";
        var prompt = AiPrompts.BuildInterviewPrompt(idea, item.Platform, item.Style);

        try
        {
            var rawJson = await CallGeminiGenerateAsync(prompt, settings, cleanKey, "low", ct);
            return AiPrompts.ParseInterview(rawJson);
        }
        catch
        {
            return Array.Empty<InterviewQuestion>();
        }
    }

    public async Task<string> ChatAsync(
        IReadOnlyList<(string Role, string Content)> messages,
        string systemInstruction,
        AiSettings settings,
        CancellationToken ct = default)
    {
        var cleanKey = settings.GeminiApiKey.Trim();

        if (settings.Provider == AiProvider.Gemini && !string.IsNullOrEmpty(cleanKey))
        {
            return await CallGeminiChatAsync(messages, systemInstruction, settings, cleanKey, ct);
        }

        if (settings.Provider == AiProvider.OpenAI)
        {
            return await CallOpenAIChatAsync(messages, systemInstruction, settings, ct);
        }

        if (settings.Provider == AiProvider.Ollama)
        {
            return await CallOllamaChatAsync(messages, systemInstruction, settings, ct);
        }

        return "Офлайн-режим: укажите ключ Gemini или OpenAI в настройках приложения для общения с AI-Архитектором.";
    }

    public async Task<bool> TestConnectionAsync(AiSettings settings, CancellationToken ct = default)
    {
        try
        {
            if (settings.Provider == AiProvider.Gemini)
            {
                var cleanKey = settings.GeminiApiKey.Trim();
                if (string.IsNullOrEmpty(cleanKey)) return false;

                var chain = GetModelChain(settings.GeminiModel);
                foreach (var model in chain)
                {
                    var url = $"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent";
                    var body = new
                    {
                        contents = new[]
                        {
                            new { parts = new[] { new { text = "Ping. Answer with 'PONG'." } } }
                        }
                    };

                    using var request = new HttpRequestMessage(HttpMethod.Post, url);
                    request.Headers.Add("x-goog-api-key", cleanKey);
                    request.Content = JsonContent.Create(body);

                    using var response = await _http.SendAsync(request, ct);
                    if (response.IsSuccessStatusCode) return true;

                    var status = (int)response.StatusCode;
                    if (status == 400 || status == 401 || status == 403) return false;
                }
                return false;
            }

            if (settings.Provider == AiProvider.OpenAI)
            {
                var body = new
                {
                    model = string.IsNullOrWhiteSpace(settings.OpenAIModel) ? "deepseek-chat" : settings.OpenAIModel,
                    messages = new[] { new { role = "user", content = "Ping" } },
                    max_tokens = 5
                };
                using var req = new HttpRequestMessage(HttpMethod.Post, settings.OpenAIEndpoint);
                if (!string.IsNullOrWhiteSpace(settings.OpenAIApiKey))
                {
                    req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", settings.OpenAIApiKey.Trim());
                }
                req.Content = JsonContent.Create(body);
                using var res = await _http.SendAsync(req, ct);
                return res.IsSuccessStatusCode;
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

    private async Task<string> CallGeminiGenerateAsync(
        string prompt,
        AiSettings settings,
        string apiKey,
        string? thinkingLevel,
        CancellationToken ct)
    {
        var chain = GetModelChain(settings.GeminiModel);
        var lastStatus = 0;
        var lastMessage = "";

        foreach (var model in chain)
        {
            var url = $"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent";

            for (var attempt = 1; attempt <= 2; attempt++)
            {
                object body = thinkingLevel != null
                    ? new
                    {
                        contents = new[] { new { parts = new[] { new { text = prompt } } } },
                        generationConfig = new
                        {
                            temperature = 0.8,
                            topP = 0.95,
                            thinkingConfig = new { thinkingLevel }
                        }
                    }
                    : new
                    {
                        contents = new[] { new { parts = new[] { new { text = prompt } } } },
                        generationConfig = new
                        {
                            temperature = 0.8,
                            topP = 0.95
                        }
                    };

                using var request = new HttpRequestMessage(HttpMethod.Post, url);
                request.Headers.Add("x-goog-api-key", apiKey);
                request.Content = JsonContent.Create(body);

                HttpResponseMessage response;
                try
                {
                    response = await _http.SendAsync(request, ct);
                }
                catch (HttpRequestException) when (attempt == 1)
                {
                    await Task.Delay(1500, ct);
                    continue;
                }

                if (response.IsSuccessStatusCode)
                {
                    using var stream = await response.Content.ReadAsStreamAsync(ct);
                    return ParseGeminiResponse(stream);
                }

                lastStatus = (int)response.StatusCode;
                var errBytes = await response.Content.ReadAsByteArrayAsync(ct);
                lastMessage = ExtractErrorMessage(errBytes);

                // If thinkingLevel rejected with 400, retry without thinkingLevel
                if (lastStatus == 400 && thinkingLevel != null)
                {
                    thinkingLevel = null;
                    continue;
                }

                if (lastStatus == 400 || lastStatus == 401 || lastStatus == 403)
                {
                    throw new InvalidOperationException($"Gemini отклонил запрос ({lastStatus}): {lastMessage}. Проверьте ключ API в настройках.");
                }

                if ((lastStatus == 429 || lastStatus == 500 || lastStatus == 503) && attempt == 1)
                {
                    await Task.Delay(1500, ct);
                    continue;
                }

                if (lastStatus == 404 || lastStatus == 429 || lastStatus == 500 || lastStatus == 503)
                {
                    break; // Move to next model in chain
                }

                throw new InvalidOperationException($"Ошибка Gemini ({lastStatus}): {lastMessage}");
            }
        }

        throw new InvalidOperationException($"Ни одна модель Gemini не ответила (статус {lastStatus}): {lastMessage}");
    }

    private async Task<string> CallGeminiChatAsync(
        IReadOnlyList<(string Role, string Content)> messages,
        string systemInstruction,
        AiSettings settings,
        string apiKey,
        CancellationToken ct)
    {
        var chain = GetModelChain(settings.GeminiModel);
        var lastStatus = 0;
        var lastMessage = "";

        var contents = messages.Select(m => new
        {
            role = m.Role == "user" ? "user" : "model",
            parts = new[] { new { text = m.Content } }
        }).ToArray();

        foreach (var model in chain)
        {
            var url = $"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent";

            for (var attempt = 1; attempt <= 2; attempt++)
            {
                var body = new
                {
                    system_instruction = new { parts = new[] { new { text = systemInstruction } } },
                    contents,
                    generationConfig = new
                    {
                        temperature = 0.8,
                        topP = 0.95,
                        thinkingConfig = new { thinkingLevel = "low" }
                    }
                };

                using var request = new HttpRequestMessage(HttpMethod.Post, url);
                request.Headers.Add("x-goog-api-key", apiKey);
                request.Content = JsonContent.Create(body);

                HttpResponseMessage response;
                try
                {
                    response = await _http.SendAsync(request, ct);
                }
                catch (HttpRequestException) when (attempt == 1)
                {
                    await Task.Delay(1500, ct);
                    continue;
                }

                if (response.IsSuccessStatusCode)
                {
                    using var stream = await response.Content.ReadAsStreamAsync(ct);
                    return ParseGeminiResponse(stream);
                }

                lastStatus = (int)response.StatusCode;
                var errBytes = await response.Content.ReadAsByteArrayAsync(ct);
                lastMessage = ExtractErrorMessage(errBytes);

                if ((lastStatus == 429 || lastStatus == 500 || lastStatus == 503) && attempt == 1)
                {
                    await Task.Delay(1500, ct);
                    continue;
                }

                if (lastStatus == 404 || lastStatus == 429 || lastStatus == 500 || lastStatus == 503)
                {
                    break;
                }

                throw new InvalidOperationException($"Ошибка Gemini ({lastStatus}): {lastMessage}");
            }
        }

        throw new InvalidOperationException($"Ни одна модель Gemini не ответила: {lastMessage}");
    }

    private static string ParseGeminiResponse(Stream stream)
    {
        using var doc = JsonDocument.Parse(stream);
        var root = doc.RootElement;

        if (!root.TryGetProperty("candidates", out var candidates) || candidates.GetArrayLength() == 0)
        {
            throw new InvalidOperationException("Gemini вернул пустой список кандидатов.");
        }

        var candidate = candidates[0];
        if (!candidate.TryGetProperty("content", out var content) ||
            !content.TryGetProperty("parts", out var parts))
        {
            throw new InvalidOperationException("Структура ответа Gemini не содержит частей текста.");
        }

        var sb = new StringBuilder();
        foreach (var part in parts.EnumerateArray())
        {
            if (part.TryGetProperty("thought", out var thoughtProp) && thoughtProp.GetBoolean())
            {
                continue; // Skip thinking scratchpad
            }
            if (part.TryGetProperty("text", out var textProp))
            {
                sb.Append(textProp.GetString());
            }
        }

        var text = sb.ToString();
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new InvalidOperationException("Gemini вернул ответ без текстового содержимого.");
        }

        return text;
    }

    private static string ExtractErrorMessage(byte[] data)
    {
        try
        {
            using var doc = JsonDocument.Parse(data);
            if (doc.RootElement.TryGetProperty("error", out var errorProp) &&
                errorProp.TryGetProperty("message", out var msgProp))
            {
                return msgProp.GetString() ?? "Неизвестная ошибка";
            }
        }
        catch
        {
        }
        var raw = Encoding.UTF8.GetString(data);
        return raw.Length > 240 ? raw.Substring(0, 240) : raw;
    }

    private async Task<string> CallOllamaGenerateAsync(string prompt, AiSettings settings, CancellationToken ct)
    {
        var body = new
        {
            model = string.IsNullOrWhiteSpace(settings.OllamaModel) ? "llama3" : settings.OllamaModel,
            prompt,
            stream = false,
            options = new { temperature = 0.8 }
        };

        var response = await _http.PostAsJsonAsync(settings.OllamaEndpoint, body, JsonOpts, ct);
        response.EnsureSuccessStatusCode();

        using var doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
        if (doc.RootElement.TryGetProperty("response", out var respProp))
        {
            return respProp.GetString() ?? "";
        }
        return "";
    }

    private async Task<string> CallOllamaChatAsync(
        IReadOnlyList<(string Role, string Content)> messages,
        string systemInstruction,
        AiSettings settings,
        CancellationToken ct)
    {
        var ollamaMsgs = new List<object>
        {
            new { role = "system", content = systemInstruction }
        };

        foreach (var m in messages)
        {
            ollamaMsgs.Add(new
            {
                role = m.Role == "user" ? "user" : "assistant",
                content = m.Content
            });
        }

        var body = new
        {
            model = string.IsNullOrWhiteSpace(settings.OllamaModel) ? "llama3" : settings.OllamaModel,
            messages = ollamaMsgs,
            stream = false
        };

        var response = await _http.PostAsJsonAsync(settings.OllamaEndpoint, body, JsonOpts, ct);
        response.EnsureSuccessStatusCode();

        using var doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
        if (doc.RootElement.TryGetProperty("message", out var msgProp) &&
            msgProp.TryGetProperty("content", out var contentProp))
        {
            return contentProp.GetString() ?? "";
        }
        return "";
    }

    private async Task<string> CallOpenAIGenerateAsync(string prompt, AiSettings settings, CancellationToken ct)
    {
        var body = new
        {
            model = string.IsNullOrWhiteSpace(settings.OpenAIModel) ? "deepseek-chat" : settings.OpenAIModel,
            messages = new[] { new { role = "user", content = prompt } },
            temperature = 0.7
        };

        using var req = new HttpRequestMessage(HttpMethod.Post, settings.OpenAIEndpoint);
        if (!string.IsNullOrWhiteSpace(settings.OpenAIApiKey))
        {
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", settings.OpenAIApiKey.Trim());
        }
        req.Content = JsonContent.Create(body);

        using var res = await _http.SendAsync(req, ct);
        res.EnsureSuccessStatusCode();

        using var doc = await JsonDocument.ParseAsync(await res.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
        if (doc.RootElement.TryGetProperty("choices", out var choices) && choices.GetArrayLength() > 0)
        {
            var choice = choices[0];
            if (choice.TryGetProperty("message", out var msg) && msg.TryGetProperty("content", out var content))
            {
                return content.GetString() ?? "";
            }
        }
        return "";
    }

    private async Task<string> CallOpenAIChatAsync(
        IReadOnlyList<(string Role, string Content)> messages,
        string systemInstruction,
        AiSettings settings,
        CancellationToken ct)
    {
        var apiMessages = new List<object>
        {
            new { role = "system", content = systemInstruction }
        };
        foreach (var m in messages)
        {
            apiMessages.Add(new { role = m.Role, content = m.Content });
        }

        var body = new
        {
            model = string.IsNullOrWhiteSpace(settings.OpenAIModel) ? "deepseek-chat" : settings.OpenAIModel,
            messages = apiMessages,
            temperature = 0.7
        };

        using var req = new HttpRequestMessage(HttpMethod.Post, settings.OpenAIEndpoint);
        if (!string.IsNullOrWhiteSpace(settings.OpenAIApiKey))
        {
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", settings.OpenAIApiKey.Trim());
        }
        req.Content = JsonContent.Create(body);

        using var res = await _http.SendAsync(req, ct);
        res.EnsureSuccessStatusCode();

        using var doc = await JsonDocument.ParseAsync(await res.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
        if (doc.RootElement.TryGetProperty("choices", out var choices) && choices.GetArrayLength() > 0)
        {
            var choice = choices[0];
            if (choice.TryGetProperty("message", out var msg) && msg.TryGetProperty("content", out var content))
            {
                return content.GetString() ?? "";
            }
        }
        return "";
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
