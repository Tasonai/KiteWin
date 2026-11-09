using KiteWin.Core.Models;

namespace KiteWin.Core;

public enum RemediationAction
{
    InjectCacheLayer,
    AddRateLimiter,
    ScaleHorizontal,
    DecoupleCyclicDependency
}

public sealed class BenchmarkProfile
{
    public double TargetRps { get; set; }
    public double AchievedRps { get; set; }
    public double P99LatencyMs { get; set; }
    public double ErrorRate { get; set; }
    public string? BottleneckNodeId { get; set; }

    public BenchmarkProfile() { }

    public BenchmarkProfile(double targetRps, double achievedRps, double p99LatencyMs, double errorRate, string? bottleneckNodeId = null)
    {
        TargetRps = targetRps;
        AchievedRps = achievedRps;
        P99LatencyMs = p99LatencyMs;
        ErrorRate = errorRate;
        BottleneckNodeId = bottleneckNodeId;
    }
}

public sealed class ArchitecturalRemediationPatch
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string TargetNodeId { get; set; } = string.Empty;
    public RemediationAction Action { get; set; }
    public string PatchDescription { get; set; } = string.Empty;
    public string DiffSnippet { get; set; } = string.Empty;

    public ArchitecturalRemediationPatch() { }

    public ArchitecturalRemediationPatch(string targetNodeId, RemediationAction action, string patchDescription, string diffSnippet)
    {
        TargetNodeId = targetNodeId;
        Action = action;
        PatchDescription = patchDescription;
        DiffSnippet = diffSnippet;
    }
}

public static class SelfHealingEngine
{
    public static List<ArchitecturalRemediationPatch> DiagnoseBottleneck(
        BenchmarkProfile benchmark,
        List<CanvasFrame> frames)
    {
        var patches = new List<ArchitecturalRemediationPatch>();
        var targetId = benchmark.BottleneckNodeId ?? frames.FirstOrDefault()?.Id.ToString() ?? "primary-service";

        if (benchmark.P99LatencyMs > 300.0)
        {
            patches.Add(new ArchitecturalRemediationPatch(
                targetId,
                RemediationAction.InjectCacheLayer,
                $"High latency ({(int)benchmark.P99LatencyMs} ms) detected. Inject Redis LRU cache layer ahead of database persistence.",
                """
                + [CanvasFrame: Redis Cache Layer (6379)]
                +   Connection: [API Service] -> [Redis Cache] (Cache-Aside, TTL=300s)
                +   Connection: [Redis Cache] -> [Database] (On-Miss Fallback)
                """
            ));
        }

        if (benchmark.ErrorRate > 0.01)
        {
            patches.Add(new ArchitecturalRemediationPatch(
                targetId,
                RemediationAction.AddRateLimiter,
                $"Elevated error rate ({(benchmark.ErrorRate * 100):0.1f}%) detected. Inject Token Bucket Rate Limiter and Circuit Breaker.",
                """
                + [CanvasFrame: Envoy / Traefik Rate Limiter (8080)]
                +   Policy: 500 RPS Token Bucket with burst capacity 1000
                +   Circuit Breaker: Trip on 5 consecutive 5xx errors (cooldown 15s)
                """
            ));
        }

        if (benchmark.AchievedRps < (benchmark.TargetRps * 0.8))
        {
            patches.Add(new ArchitecturalRemediationPatch(
                targetId,
                RemediationAction.ScaleHorizontal,
                $"Throughput shortfall ({(int)benchmark.AchievedRps} / {(int)benchmark.TargetRps} RPS). Scale cluster replicas from 1 to 3 with round-robin load balancer.",
                $"""
                ~ services:
                ~   {targetId}:
                +     deploy:
                +       replicas: 3
                +       resources:
                +         limits:
                +           cpus: '1.5'
                +           memory: 1024M
                """
            ));
        }

        return patches;
    }
}
