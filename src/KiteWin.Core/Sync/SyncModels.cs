using System.Text.Json.Serialization;

namespace KiteWin.Core.Sync;

public sealed class KiteStatusResponse
{
    [JsonPropertyName("status")]
    public string Status { get; set; } = "online";

    [JsonPropertyName("app")]
    public string App { get; set; } = "Kite Windows";

    [JsonPropertyName("version")]
    public string Version { get; set; } = "1.0";

    [JsonPropertyName("port")]
    public int Port { get; set; } = 24243;
}

public sealed class KiteIdeaDTO
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("title")]
    public string Title { get; set; } = "";

    [JsonPropertyName("description")]
    public string Description { get; set; } = "";

    [JsonPropertyName("platform")]
    public string Platform { get; set; } = "web";

    [JsonPropertyName("preferredStyle")]
    public string PreferredStyle { get; set; } = "Editorial & Swiss Modern";

    [JsonPropertyName("status")]
    public string Status { get; set; } = "backlog";

    [JsonPropertyName("tags")]
    public List<string> Tags { get; set; } = [];

    [JsonPropertyName("isStarred")]
    public bool IsStarred { get; set; }

    [JsonPropertyName("createdAt")]
    public string CreatedAt { get; set; } = "";

    [JsonPropertyName("updatedAt")]
    public string? UpdatedAt { get; set; }
}

public sealed class KiteIdeasPayload
{
    [JsonPropertyName("version")]
    public int Version { get; set; } = 1;

    [JsonPropertyName("exportedAt")]
    public string ExportedAt { get; set; } = "";

    [JsonPropertyName("ideas")]
    public List<KiteIdeaDTO> Ideas { get; set; } = [];
}

public sealed class KiteSyncResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = "";
    public int AddedFromMac { get; set; }
    public int UpdatedFromMac { get; set; }
    public int PushedToMac { get; set; }
    public int TotalMerged { get; set; }
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.Now;
}
