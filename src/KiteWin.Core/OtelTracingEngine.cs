//
// OtelTracingEngine.cs
// KiteWin.Core
//
// Distributed OpenTelemetry Span Waterfall & Tail-Sampling Pipeline Engine (Phase 29).
// Reconstructs W3C traceparent span trees across architecture services, computes Critical Path
// self-time attribution, detects N+1 query cascades, and synthesizes OpenTelemetry Collector
// tail-sampling and PII-redaction YAML pipelines.
//

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace KiteWin.Core;

public sealed class DistributedTraceSpan
{
    [JsonPropertyName("spanId")]
    public string SpanId { get; init; } = "";

    [JsonPropertyName("parentSpanId")]
    public string? ParentSpanId { get; init; }

    [JsonPropertyName("traceId")]
    public string TraceId { get; init; } = "";

    [JsonPropertyName("serviceName")]
    public string ServiceName { get; init; } = "";

    [JsonPropertyName("operationName")]
    public string OperationName { get; init; } = "";

    [JsonPropertyName("durationMs")]
    public double DurationMs { get; init; }

    [JsonPropertyName("dbStatementCount")]
    public int DbStatementCount { get; init; }

    [JsonPropertyName("hasError")]
    public bool HasError { get; init; }
}

public sealed class CriticalPathAttribution
{
    [JsonPropertyName("traceId")]
    public string TraceId { get; init; } = "";

    [JsonPropertyName("totalDurationMs")]
    public double TotalDurationMs { get; init; }

    [JsonPropertyName("bottleneckService")]
    public string BottleneckService { get; init; } = "";

    [JsonPropertyName("bottleneckSelfTimeMs")]
    public double BottleneckSelfTimeMs { get; init; }

    [JsonPropertyName("nPlusOneDetected")]
    public bool NPlusOneDetected { get; init; }

    [JsonPropertyName("waterfallSummary")]
    public List<string> WaterfallSummary { get; init; } = new();
}

public sealed class OtelCollectorPipelineBundle
{
    [JsonPropertyName("clusterEnv")]
    public string ClusterEnv { get; init; } = "";

    [JsonPropertyName("tailSampleLatencyThresholdMs")]
    public int TailSampleLatencyThresholdMs { get; init; }

    [JsonPropertyName("otelCollectorYaml")]
    public string OtelCollectorYaml { get; init; } = "";
}

public sealed class OtelTracingEngine
{
    public const string DefaultTraceId = "4bf92f3577b34da6a3ce929d0e0e4736";

    public static OtelTracingEngine Instance { get; } = new();

    private readonly object _lock = new();
    private readonly List<DistributedTraceSpan> _spans = new();

    public OtelTracingEngine()
    {
        _spans.AddRange(new[]
        {
            new DistributedTraceSpan
            {
                SpanId = "span-root-01",
                ParentSpanId = null,
                TraceId = DefaultTraceId,
                ServiceName = "api-gateway",
                OperationName = "POST /v1/orders/checkout",
                DurationMs = 210.0,
                DbStatementCount = 0,
                HasError = false
            },
            new DistributedTraceSpan
            {
                SpanId = "span-checkout-02",
                ParentSpanId = "span-root-01",
                TraceId = DefaultTraceId,
                ServiceName = "checkout-api",
                OperationName = "GrpcOrderOrchestrator.Execute",
                DurationMs = 186.0,
                DbStatementCount = 1,
                HasError = false
            },
            new DistributedTraceSpan
            {
                SpanId = "span-inv-03",
                ParentSpanId = "span-checkout-02",
                TraceId = DefaultTraceId,
                ServiceName = "inventory-service",
                OperationName = "ReserveLineItemsLoop",
                DurationMs = 164.0,
                DbStatementCount = 0,
                HasError = false
            },
            new DistributedTraceSpan
            {
                SpanId = "span-pg-04",
                ParentSpanId = "span-inv-03",
                TraceId = DefaultTraceId,
                ServiceName = "inventory-postgres",
                OperationName = "SELECT stock FOR UPDATE (x24)",
                DurationMs = 148.0,
                DbStatementCount = 24,
                HasError = false
            }
        });
    }

    public List<DistributedTraceSpan> GetTraceSpans(string traceId = DefaultTraceId)
    {
        lock (_lock)
        {
            var matched = _spans.Where(s => s.TraceId == traceId).ToList();
            return matched.Count > 0 ? matched : _spans.ToList();
        }
    }

    public CriticalPathAttribution AnalyzeCriticalPath(string traceId = DefaultTraceId)
    {
        var traceSpans = GetTraceSpans(traceId);
        var root = traceSpans.FirstOrDefault(s => s.ParentSpanId == null) ?? traceSpans[0];

        string maxSelfService = root.ServiceName;
        double maxSelfMs = 0.0;
        bool nPlusOne = false;
        var summary = new List<string>();

        foreach (var sp in traceSpans)
        {
            double childrenDur = traceSpans
                .Where(c => c.ParentSpanId == sp.SpanId)
                .Sum(c => c.DurationMs);
            double selfTime = Math.Max(0.0, Math.Round(sp.DurationMs - childrenDur, 2));
            if (selfTime > maxSelfMs)
            {
                maxSelfMs = selfTime;
                maxSelfService = sp.ServiceName;
            }
            if (sp.DbStatementCount >= 10)
            {
                nPlusOne = true;
            }
            summary.Add($"{sp.ServiceName} [{sp.OperationName}]: total={sp.DurationMs}ms, self={selfTime}ms");
        }

        return new CriticalPathAttribution
        {
            TraceId = root.TraceId,
            TotalDurationMs = root.DurationMs,
            BottleneckService = maxSelfService,
            BottleneckSelfTimeMs = maxSelfMs,
            NPlusOneDetected = nPlusOne,
            WaterfallSummary = summary
        };
    }

    public OtelCollectorPipelineBundle SynthesizeCollectorConfig(
        string clusterEnv = "prod-eu-central",
        int tailSampleLatencyThresholdMs = 150)
    {
        int threshold = Math.Max(25, tailSampleLatencyThresholdMs);

        string yaml = $@"receivers:
  otlp:
    protocols:
      grpc:
        endpoint: 0.0.0.0:4317
      http:
        endpoint: 0.0.0.0:4318

processors:
  attributes/pii_redact:
    actions:
      - key: http.request.header.authorization
        action: delete
      - key: user.email
        action: hash
  tail_sampling:
    decision_wait: 10s
    num_traces: 50000
    policies:
      - name: retain-errors-100pct
        type: status_code
        status_code: {{ status_codes: [ERROR] }}
      - name: retain-high-latency-spans
        type: latency
        latency: {{ threshold_ms: {threshold} }}
      - name: baseline-probabilistic-5pct
        type: probabilistic
        probabilistic: {{ sampling_percentage: 5.0 }}
  batch:
    send_batch_size: 2048
    timeout: 2s

exporters:
  otlp/tempo:
    endpoint: tempo-distributor.{clusterEnv}.svc.cluster.local:4317
    tls:
      insecure: false

service:
  pipelines:
    traces:
      receivers: [otlp]
      processors: [attributes/pii_redact, tail_sampling, batch]
      exporters: [otlp/tempo]";

        return new OtelCollectorPipelineBundle
        {
            ClusterEnv = clusterEnv,
            TailSampleLatencyThresholdMs = threshold,
            OtelCollectorYaml = yaml
        };
    }
}
