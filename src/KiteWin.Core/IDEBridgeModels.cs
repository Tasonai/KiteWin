using System.Text.Json.Serialization;

namespace KiteWin.Core;

public enum TelemetryHealthStatus
{
    Healthy,
    Degraded,
    Failing
}

public sealed class NodeTelemetry
{
    [JsonPropertyName("nodeId")]
    public string NodeId { get; set; } = string.Empty;

    [JsonPropertyName("rps")]
    public double Rps { get; set; }

    [JsonPropertyName("p99LatencyMs")]
    public double P99LatencyMs { get; set; }

    [JsonPropertyName("errorRate")]
    public double ErrorRate { get; set; }

    [JsonPropertyName("status")]
    public string Status => HealthStatus switch
    {
        TelemetryHealthStatus.Healthy => "healthy",
        TelemetryHealthStatus.Degraded => "degraded",
        _ => "failing"
    };

    [JsonIgnore]
    public TelemetryHealthStatus HealthStatus
    {
        get
        {
            if (ErrorRate >= 0.05 || P99LatencyMs >= 1000.0)
                return TelemetryHealthStatus.Failing;
            if (ErrorRate >= 0.01 || P99LatencyMs >= 300.0)
                return TelemetryHealthStatus.Degraded;
            return TelemetryHealthStatus.Healthy;
        }
    }

    [JsonPropertyName("colorHex")]
    public string ColorHex => HealthStatus switch
    {
        TelemetryHealthStatus.Healthy => "#2ECC71",
        TelemetryHealthStatus.Degraded => "#F39C12",
        _ => "#E74C3C"
    };

    [JsonPropertyName("lastUpdated")]
    public DateTimeOffset LastUpdated { get; set; } = DateTimeOffset.UtcNow;

    public NodeTelemetry() { }

    public NodeTelemetry(string nodeId, double rps, double p99LatencyMs, double errorRate)
    {
        NodeId = nodeId;
        Rps = Math.Max(0.0, rps);
        P99LatencyMs = Math.Max(0.0, p99LatencyMs);
        ErrorRate = Math.Clamp(errorRate, 0.0, 1.0);
        LastUpdated = DateTimeOffset.UtcNow;
    }
}

public sealed class IDEBridgeMessage
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("filePath")]
    public string? FilePath { get; set; }

    [JsonPropertyName("line")]
    public int? Line { get; set; }

    [JsonPropertyName("column")]
    public int? Column { get; set; }

    [JsonPropertyName("nodeId")]
    public string? NodeId { get; set; }

    [JsonPropertyName("nodeName")]
    public string? NodeName { get; set; }

    [JsonPropertyName("editor")]
    public string? Editor { get; set; }

    [JsonPropertyName("telemetry")]
    public NodeTelemetry? Telemetry { get; set; }
}

public sealed class IDEBridgeHub
{
    public static IDEBridgeHub Shared { get; } = new();

    private readonly object _lock = new();
    private string? _activeFilePath;
    private int? _activeLine;
    private readonly Dictionary<string, NodeTelemetry> _telemetries = [];

    private IDEBridgeHub() { }

    public void UpdateActiveFile(string path, int? line = null)
    {
        lock (_lock)
        {
            _activeFilePath = path;
            _activeLine = line;
        }
    }

    public (string? path, int? line) CurrentActiveFile
    {
        get
        {
            lock (_lock)
            {
                return (_activeFilePath, _activeLine);
            }
        }
    }

    public void SetTelemetry(NodeTelemetry telemetry)
    {
        lock (_lock)
        {
            _telemetries[telemetry.NodeId] = telemetry;
        }
    }

    public NodeTelemetry? GetTelemetry(string nodeId)
    {
        lock (_lock)
        {
            return _telemetries.GetValueOrDefault(nodeId);
        }
    }

    public List<NodeTelemetry> AllTelemetries()
    {
        lock (_lock)
        {
            return _telemetries.Values.ToList();
        }
    }
}
