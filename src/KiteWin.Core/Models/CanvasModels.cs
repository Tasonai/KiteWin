using System.Text.Json;
using System.Text.Json.Serialization;

namespace KiteWin.Core.Models;

public sealed class CanvasSticky
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [JsonPropertyName("text")]
    public string Text { get; set; } = "";

    [JsonPropertyName("colorHex")]
    public string ColorHex { get; set; } = "#FEF08A";

    [JsonPropertyName("x")]
    public double X { get; set; }

    [JsonPropertyName("y")]
    public double Y { get; set; }
}

public static class CanvasConnectionProtocol
{
    public const string Rest = "REST";
    public const string Trpc = "tRPC";
    public const string Grpc = "gRPC";
    public const string WebSocket = "WebSocket";
    public const string EventBus = "EventBus";
    public const string GraphQl = "GraphQL";
    public const string Sql = "SQL";
    public const string Queue = "Queue";

    public static readonly IReadOnlyList<string> All = new[]
    {
        Rest, Trpc, Grpc, WebSocket, EventBus, GraphQl, Sql, Queue
    };

    public static string Color(string label)
    {
        var lower = (label ?? "").ToLowerInvariant();
        if (lower.Contains("trpc")) return "#8B5CF6";
        if (lower.Contains("grpc")) return "#10B981";
        if (lower.Contains("ws") || lower.Contains("socket") || lower.Contains("stream")) return "#EC4899";
        if (lower.Contains("event") || lower.Contains("bus") || lower.Contains("kafka")) return "#F59E0B";
        if (lower.Contains("graphql") || lower.Contains("gql")) return "#E11D48";
        if (lower.Contains("sql") || lower.Contains("db") || lower.Contains("query")) return "#64748B";
        if (lower.Contains("queue") || lower.Contains("inngest") || lower.Contains("job")) return "#14B8A6";
        return "#3B82F6";
    }
}

public sealed class CanvasConnection
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [JsonPropertyName("fromId")]
    public string FromId { get; set; } = "";

    [JsonPropertyName("toId")]
    public string ToId { get; set; } = "";

    [JsonPropertyName("label")]
    public string Label { get; set; } = "";

    [JsonPropertyName("colorHex")]
    public string ColorHex { get; set; } = "#3B82F6";
}

public sealed class CanvasFrame
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [JsonPropertyName("title")]
    public string Title { get; set; } = "";

    [JsonPropertyName("colorHex")]
    public string ColorHex { get; set; } = "#3B82F6";

    [JsonPropertyName("x")]
    public double X { get; set; }

    [JsonPropertyName("y")]
    public double Y { get; set; }

    [JsonPropertyName("width")]
    public double Width { get; set; } = 440;

    [JsonPropertyName("height")]
    public double Height { get; set; } = 520;
}

public sealed class CanvasReferenceCard
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [JsonPropertyName("title")]
    public string Title { get; set; } = "";

    [JsonPropertyName("source")]
    public string Source { get; set; } = "Mobbin";

    [JsonPropertyName("url")]
    public string Url { get; set; } = "";

    [JsonPropertyName("colorHex")]
    public string ColorHex { get; set; } = "#10B981";

    [JsonPropertyName("x")]
    public double X { get; set; }

    [JsonPropertyName("y")]
    public double Y { get; set; }

    [JsonPropertyName("tags")]
    public List<string> Tags { get; set; } = [];

    [JsonPropertyName("description")]
    public string Description { get; set; } = "";
}

public sealed class CanvasPoint
{
    [JsonPropertyName("x")]
    public double X { get; set; }

    [JsonPropertyName("y")]
    public double Y { get; set; }
}

public sealed class CanvasGraphSnapshot
{
    [JsonPropertyName("version")]
    public int Version { get; set; } = 1;

    [JsonPropertyName("exportedAt")]
    public string ExportedAt { get; set; } = DateTimeOffset.Now.ToString("O");

    [JsonPropertyName("frames")]
    public List<CanvasFrame> Frames { get; set; } = [];

    [JsonPropertyName("stickies")]
    public List<CanvasSticky> Stickies { get; set; } = [];

    [JsonPropertyName("referenceCards")]
    public List<CanvasReferenceCard> ReferenceCards { get; set; } = [];

    [JsonPropertyName("connections")]
    public List<CanvasConnection> Connections { get; set; } = [];

    [JsonPropertyName("replace")]
    public bool? Replace { get; set; }

    [JsonPropertyName("itemPositions")]
    public Dictionary<string, CanvasPoint> ItemPositions { get; set; } = [];
}
