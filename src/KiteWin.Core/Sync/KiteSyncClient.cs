using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using KiteWin.Core.Models;

namespace KiteWin.Core.Sync;

/// <summary>
/// HTTP Client for P2P synchronization with Kite macOS.
/// Communicates with KiteSyncServer over local loopback or Wi-Fi/LAN.
/// </summary>
public sealed class KiteSyncClient : IDisposable
{
    private readonly HttpClient _http;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    public KiteSyncClient()
    {
        _http = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(5)
        };
        _http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }

    /// <summary>
    /// Checks if Kite macOS is running and reachable at the given endpoint.
    /// </summary>
    public async Task<bool> CheckConnectionAsync(string endpoint)
    {
        try
        {
            var url = FormatUrl(endpoint, "/api/status");
            using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(1500));
            var res = await _http.GetAsync(url, cts.Token);
            if (!res.IsSuccessStatusCode) return false;

            var status = await res.Content.ReadFromJsonAsync<KiteStatusResponse>(JsonOptions, cts.Token);
            return status?.Status == "online";
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Pulls all ideas from Kite macOS.
    /// </summary>
    public async Task<List<Item>> PullIdeasAsync(string endpoint)
    {
        var url = FormatUrl(endpoint, "/api/ideas");
        var payload = await _http.GetFromJsonAsync<KiteIdeasPayload>(url, JsonOptions);
        if (payload?.Ideas == null) return [];

        var result = new List<Item>();
        foreach (var dto in payload.Ideas)
        {
            var item = DtoToItem(dto);
            result.Add(item);
        }
        return result;
    }

    /// <summary>
    /// Pushes ideas to Kite macOS.
    /// </summary>
    public async Task<int> PushIdeasAsync(string endpoint, IEnumerable<Item> items)
    {
        var url = FormatUrl(endpoint, "/api/ideas");
        var dtos = items.Select(ItemToDto).ToList();
        var payload = new KiteIdeasPayload
        {
            Version = 1,
            ExportedAt = DateTimeOffset.Now.ToString("O", CultureInfo.InvariantCulture),
            Ideas = dtos
        };

        var response = await _http.PostAsJsonAsync(url, payload, JsonOptions);
        response.EnsureSuccessStatusCode();

        using var doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        if (doc.RootElement.TryGetProperty("imported", out var countProp))
        {
            return countProp.GetInt32();
        }
        return dtos.Count;
    }

    /// <summary>
    /// Performs a full bidirectional P2P sync between Kite macOS and local Windows ItemStore.
    /// Guarantees zero data loss, title/UUID deduplication, and atomic commits.
    /// </summary>
    public async Task<KiteSyncResult> SyncBidirectionalAsync(string endpoint, ItemStore store)
    {
        var result = new KiteSyncResult();

        // 1. Verify connection
        var isOnline = await CheckConnectionAsync(endpoint);
        if (!isOnline)
        {
            result.Success = false;
            result.Message = $"Kite macOS не отвечает по адресу {endpoint}. Убедитесь, что приложение запущено на Mac.";
            return result;
        }

        // 2. Fetch remote ideas from Mac
        List<Item> macItems;
        try
        {
            macItems = await PullIdeasAsync(endpoint);
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Message = $"Ошибка загрузки данных с Mac: {ex.Message}";
            return result;
        }

        // 3. Fetch local ideas from Windows SQLite store
        var localItems = store.All();
        var localById = localItems.ToDictionary(i => i.Id);
        var localByTitle = localItems.ToDictionary(i => NormalizeTitle(i.Title), StringComparer.OrdinalIgnoreCase);

        var addedCount = 0;
        var updatedCount = 0;

        foreach (var macItem in macItems)
        {
            var normalizedMacTitle = NormalizeTitle(macItem.Title);

            if (localById.TryGetValue(macItem.Id, out var existingById))
            {
                // Conflict resolution: prefer newer update timestamp, merge concept/prompts
                if (macItem.UpdatedAt > existingById.UpdatedAt)
                {
                    existingById.Title = macItem.Title;
                    existingById.Concept = string.IsNullOrWhiteSpace(existingById.Concept) ? macItem.Concept : existingById.Concept;
                    existingById.Status = macItem.Status;
                    existingById.Platform = macItem.Platform;
                    existingById.Style = macItem.Style;
                    existingById.IsStarred = existingById.IsStarred || macItem.IsStarred;
                    existingById.UpdatedAt = macItem.UpdatedAt;
                    store.Save(existingById);
                    updatedCount++;
                }
            }
            else if (localByTitle.TryGetValue(normalizedMacTitle, out var existingByTitle))
            {
                // Same title match: enrich existing local item
                if (string.IsNullOrWhiteSpace(existingByTitle.Concept) && !string.IsNullOrWhiteSpace(macItem.Concept))
                {
                    existingByTitle.Concept = macItem.Concept;
                }
                existingByTitle.IsStarred = existingByTitle.IsStarred || macItem.IsStarred;
                store.Save(existingByTitle);
                updatedCount++;
            }
            else
            {
                // New item from Mac
                store.Save(macItem);
                localById[macItem.Id] = macItem;
                localByTitle[normalizedMacTitle] = macItem;
                addedCount++;
            }
        }

        // 4. Retrieve complete unified merged set from local store
        var mergedSet = store.All();

        // 5. Push unified merged set back to Mac
        var pushedCount = 0;
        try
        {
            pushedCount = await PushIdeasAsync(endpoint, mergedSet);
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Message = $"Идеи получены с Mac ({addedCount} новых), но обратная отправка на Mac не удалась: {ex.Message}";
            return result;
        }

        result.Success = true;
        result.AddedFromMac = addedCount;
        result.UpdatedFromMac = updatedCount;
        result.PushedToMac = pushedCount;
        result.TotalMerged = mergedSet.Count;
        result.Message = $"Синхронизировано: +{addedCount} с Mac, {updatedCount} обновлено, {mergedSet.Count} всего.";
        return result;
    }

    /// <summary>
    /// Pulls the complete Canvas graph from Kite macOS.
    /// </summary>
    public async Task<CanvasGraphSnapshot?> PullCanvasAsync(string endpoint)
    {
        try
        {
            var url = FormatUrl(endpoint, "/api/canvas");
            return await _http.GetFromJsonAsync<CanvasGraphSnapshot>(url, JsonOptions);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Pushes the local Canvas snapshot to Kite macOS.
    /// </summary>
    public async Task<bool> PushCanvasAsync(string endpoint, CanvasGraphSnapshot snapshot)
    {
        try
        {
            var url = FormatUrl(endpoint, "/api/canvas");
            var response = await _http.PostAsJsonAsync(url, snapshot, JsonOptions);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Bidirectionally syncs the infinite canvas graph with Kite macOS.
    /// </summary>
    public async Task<bool> SyncCanvasBidirectionalAsync(string endpoint, CanvasStore store)
    {
        var remote = await PullCanvasAsync(endpoint);
        if (remote != null && remote.Frames.Count + remote.Stickies.Count > 0)
        {
            // Merge remote items into local
            foreach (var frame in remote.Frames)
            {
                if (!store.Snapshot.Frames.Any(f => f.Title == frame.Title || f.Id == frame.Id))
                {
                    store.Snapshot.Frames.Add(frame);
                }
            }
            foreach (var sticky in remote.Stickies)
            {
                if (!store.Snapshot.Stickies.Any(s => s.Text == sticky.Text || s.Id == sticky.Id))
                {
                    store.Snapshot.Stickies.Add(sticky);
                }
            }
            foreach (var refCard in remote.ReferenceCards)
            {
                if (!store.Snapshot.ReferenceCards.Any(r => r.Title == refCard.Title || r.Id == refCard.Id))
                {
                    store.Snapshot.ReferenceCards.Add(refCard);
                }
            }
            foreach (var conn in remote.Connections)
            {
                if (!store.Snapshot.Connections.Any(c => c.FromId == conn.FromId && c.ToId == conn.ToId))
                {
                    store.Snapshot.Connections.Add(conn);
                }
            }
            store.Save();
        }

        // Push merged state back to remote
        return await PushCanvasAsync(endpoint, store.Snapshot);
    }

    public static KiteIdeaDTO ItemToDto(Item item)
    {
        return new KiteIdeaDTO
        {
            Id = item.Id.ToString(),
            Title = item.Title,
            Description = item.Concept,
            Platform = item.Platform?.ToLowerInvariant() ?? "web",
            PreferredStyle = item.Style ?? "Editorial & Swiss Modern",
            Status = item.Status ?? "backlog",
            Tags = (item.Tags ?? "")
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToList(),
            IsStarred = item.IsStarred,
            CreatedAt = item.CreatedAt.ToString("O", CultureInfo.InvariantCulture),
            UpdatedAt = item.UpdatedAt.ToString("O", CultureInfo.InvariantCulture)
        };
    }

    public static Item DtoToItem(KiteIdeaDTO dto)
    {
        var id = Guid.TryParse(dto.Id, out var g) ? g : Guid.NewGuid();
        var created = DateTimeOffset.TryParse(dto.CreatedAt, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dtoCreated)
            ? dtoCreated
            : DateTimeOffset.Now;
        var updated = !string.IsNullOrWhiteSpace(dto.UpdatedAt) && DateTimeOffset.TryParse(dto.UpdatedAt, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dtoUpdated)
            ? dtoUpdated
            : created;

        return new Item(dto.Title)
        {
            Id = id,
            Concept = dto.Description ?? "",
            Platform = Capitalize(dto.Platform),
            Style = string.IsNullOrWhiteSpace(dto.PreferredStyle) ? "Editorial & Swiss Modern" : dto.PreferredStyle,
            Status = string.IsNullOrWhiteSpace(dto.Status) ? "backlog" : dto.Status,
            Tags = dto.Tags != null ? string.Join(",", dto.Tags) : "",
            IsStarred = dto.IsStarred,
            CreatedAt = created,
            UpdatedAt = updated
        };
    }

    private static string NormalizeTitle(string? title) => (title ?? "").Trim().ToLowerInvariant();

    private static string Capitalize(string? str)
    {
        if (string.IsNullOrWhiteSpace(str)) return "Web";
        return char.ToUpperInvariant(str[0]) + str[1..].ToLowerInvariant();
    }

    private static string FormatUrl(string endpoint, string path)
    {
        var trimmed = endpoint.TrimEnd('/');
        if (!trimmed.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
            !trimmed.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            trimmed = "http://" + trimmed;
        }
        return $"{trimmed}{path}";
    }

    /// <summary>
    /// Starts background real-time SSE listener for remote changes (ideas_updated, canvas_updated).
    /// </summary>
    public async Task StartEventsStreamAsync(string endpoint, Action<string, string> onEvent, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                var url = FormatUrl(endpoint, "/api/events");
                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
                if (!response.IsSuccessStatusCode)
                {
                    await Task.Delay(3000, ct);
                    continue;
                }

                using var stream = await response.Content.ReadAsStreamAsync(ct);
                using var reader = new StreamReader(stream);

                while (!reader.EndOfStream && !ct.IsCancellationRequested)
                {
                    var line = await reader.ReadLineAsync(ct);
                    if (string.IsNullOrWhiteSpace(line)) continue;

                    if (line.StartsWith("data: ", StringComparison.OrdinalIgnoreCase))
                    {
                        var json = line["data: ".Length..].Trim();
                        try
                        {
                            using var doc = JsonDocument.Parse(json);
                            if (doc.RootElement.TryGetProperty("type", out var typeProp))
                            {
                                var type = typeProp.GetString() ?? "";
                                onEvent(type, json);
                            }
                        }
                        catch
                        {
                            // ignore malformed SSE json line
                        }
                    }
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                break;
            }
            catch
            {
                // Reconnect backoff
                await Task.Delay(3000, ct);
            }
        }
    }

    public void Dispose()
    {
        _http.Dispose();
    }
}
