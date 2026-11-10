//
// SecurityChaosEngine.cs
// KiteWin.Core
//
// Architectural Security Red Team & Chaos Injection Engine.
// Evaluates STRIDE threat surfaces, computes dynamic blast radii,
// and synthesizes executable Chaos Mesh manifests and rollback runbooks.
//

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace KiteWin.Core
{
    public enum ThreatCategory
    {
        [JsonPropertyName("spoofing")]
        Spoofing,
        [JsonPropertyName("tampering")]
        Tampering,
        [JsonPropertyName("repudiation")]
        Repudiation,
        [JsonPropertyName("informationDisclosure")]
        InformationDisclosure,
        [JsonPropertyName("denialOfService")]
        DenialOfService,
        [JsonPropertyName("elevationOfPrivilege")]
        ElevationOfPrivilege
    }

    public enum ThreatSeverity
    {
        [JsonPropertyName("critical")]
        Critical,
        [JsonPropertyName("high")]
        High,
        [JsonPropertyName("medium")]
        Medium,
        [JsonPropertyName("low")]
        Low
    }

    public class IdentifiedThreat
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        [JsonPropertyName("category")]
        public string Category { get; set; } = "spoofing";

        [JsonPropertyName("severity")]
        public string Severity { get; set; } = "medium";

        [JsonPropertyName("targetNodeId")]
        public string TargetNodeId { get; set; } = string.Empty;

        [JsonPropertyName("targetNodeName")]
        public string TargetNodeName { get; set; } = string.Empty;

        [JsonPropertyName("description")]
        public string Description { get; set; } = string.Empty;

        [JsonPropertyName("mitigationGuide")]
        public string MitigationGuide { get; set; } = string.Empty;

        [JsonPropertyName("isMitigated")]
        public bool IsMitigated { get; set; }
    }

    public class BlastRadiusReport
    {
        [JsonPropertyName("originNodeId")]
        public string OriginNodeId { get; set; } = string.Empty;

        [JsonPropertyName("originNodeName")]
        public string OriginNodeName { get; set; } = string.Empty;

        [JsonPropertyName("affectedUpstreamNodes")]
        public List<string> AffectedUpstreamNodes { get; set; } = new();

        [JsonPropertyName("affectedDownstreamNodes")]
        public List<string> AffectedDownstreamNodes { get; set; } = new();

        [JsonPropertyName("totalImpactPercentage")]
        public double TotalImpactPercentage { get; set; }

        [JsonPropertyName("criticalPathRisk")]
        public string CriticalPathRisk { get; set; } = "Low";
    }

    public class ChaosExperimentSpec
    {
        [JsonPropertyName("id")]
        public string Id => ExperimentId;

        [JsonPropertyName("experimentId")]
        public string ExperimentId { get; set; } = "chaos-" + Guid.NewGuid().ToString().Substring(0, 8);

        [JsonPropertyName("type")]
        public string Type { get; set; } = "podKill";

        [JsonPropertyName("targetNodeName")]
        public string TargetNodeName { get; set; } = string.Empty;

        [JsonPropertyName("durationSec")]
        public int DurationSec { get; set; } = 60;

        [JsonPropertyName("blastRadiusPct")]
        public double BlastRadiusPct { get; set; } = 25.0;

        [JsonPropertyName("yamlManifest")]
        public string YamlManifest { get; set; } = string.Empty;

        [JsonPropertyName("rollbackRunbook")]
        public string RollbackRunbook { get; set; } = string.Empty;
    }

    public class SecurityScorecard
    {
        [JsonPropertyName("healthScore")]
        public int HealthScore { get; set; } = 100;

        [JsonPropertyName("totalThreatsCount")]
        public int TotalThreatsCount { get; set; }

        [JsonPropertyName("criticalCount")]
        public int CriticalCount { get; set; }

        [JsonPropertyName("highCount")]
        public int HighCount { get; set; }

        [JsonPropertyName("threats")]
        public List<IdentifiedThreat> Threats { get; set; } = new();

        [JsonPropertyName("strideBreakdown")]
        public Dictionary<string, int> StrideBreakdown { get; set; } = new();
    }

    public class SecurityChaosEngine
    {
        private static readonly Lazy<SecurityChaosEngine> _instance = new(() => new SecurityChaosEngine());
        public static SecurityChaosEngine Instance => _instance.Value;

        private readonly object _lock = new();

        public SecurityScorecard AuditTopology(
            IEnumerable<(string Id, string Name, string Type)> nodes,
            IEnumerable<(string FromId, string ToId, string ProtocolType)> connections)
        {
            lock (_lock)
            {
                var nodeList = nodes.ToList();
                var connList = connections.ToList();
                var threats = new List<IdentifiedThreat>();
                var breakdown = new Dictionary<string, int>
                {
                    { "spoofing", 0 },
                    { "tampering", 0 },
                    { "repudiation", 0 },
                    { "informationDisclosure", 0 },
                    { "denialOfService", 0 },
                    { "elevationOfPrivilege", 0 }
                };

                bool hasSecurityGate = nodeList.Any(n =>
                {
                    var l = n.Type.ToLowerInvariant();
                    return l.Contains("waf") || l.Contains("firewall") || l.Contains("gateway") || l.Contains("auth");
                });

                if (!hasSecurityGate && nodeList.Count > 0)
                {
                    threats.Add(new IdentifiedThreat
                    {
                        Category = "elevationOfPrivilege",
                        Severity = "critical",
                        TargetNodeId = nodeList.First().Id,
                        TargetNodeName = nodeList.First().Name,
                        Description = "Ingress points lack unified API Gateway / WAF authentication boundary",
                        MitigationGuide = "Insert an API Gateway or WAF frame in front of application services."
                    });
                    breakdown["elevationOfPrivilege"]++;
                }

                foreach (var node in nodeList)
                {
                    var l = node.Type.ToLowerInvariant();
                    if (l.Contains("db") || l.Contains("database") || l.Contains("postgres"))
                    {
                        var incoming = connList.Where(c => c.ToId == node.Id).ToList();
                        bool directFrontend = incoming.Any(conn =>
                        {
                            var caller = nodeList.FirstOrDefault(n => n.Id == conn.FromId);
                            var ct = caller.Type?.ToLowerInvariant() ?? "";
                            return ct.Contains("web") || ct.Contains("frontend") || ct.Contains("client");
                        });

                        if (directFrontend)
                        {
                            threats.Add(new IdentifiedThreat
                            {
                                Category = "tampering",
                                Severity = "critical",
                                TargetNodeId = node.Id,
                                TargetNodeName = node.Name,
                                Description = "Direct client connection to database bypasses business logic validation",
                                MitigationGuide = "Route client requests through an authenticated backend service layer."
                            });
                            breakdown["tampering"]++;
                        }
                    }
                    else if (l.Contains("queue") || l.Contains("stream"))
                    {
                        threats.Add(new IdentifiedThreat
                        {
                            Category = "denialOfService",
                            Severity = "medium",
                            TargetNodeId = node.Id,
                            TargetNodeName = node.Name,
                            Description = "Message stream lacks backpressure buffer and Dead Letter Queue (DLQ)",
                            MitigationGuide = "Configure Dead Letter Queue and exponential retry limits."
                        });
                        breakdown["denialOfService"]++;
                    }
                }

                int crit = threats.Count(t => t.Severity == "critical");
                int high = threats.Count(t => t.Severity == "high");
                int med = threats.Count(t => t.Severity == "medium");
                int penalty = (crit * 30) + (high * 15) + (med * 5);
                int score = Math.Max(10, Math.Min(100, 100 - penalty));

                return new SecurityScorecard
                {
                    HealthScore = score,
                    TotalThreatsCount = threats.Count,
                    CriticalCount = crit,
                    HighCount = high,
                    Threats = threats,
                    StrideBreakdown = breakdown
                };
            }
        }

        public BlastRadiusReport CalculateBlastRadius(
            string targetNodeId,
            IEnumerable<(string Id, string Name, string Type)> nodes,
            IEnumerable<(string FromId, string ToId, string ProtocolType)> connections)
        {
            lock (_lock)
            {
                var nodeList = nodes.ToList();
                var connList = connections.ToList();
                var target = nodeList.FirstOrDefault(n => n.Id == targetNodeId);

                if (string.IsNullOrEmpty(target.Id))
                {
                    return new BlastRadiusReport
                    {
                        OriginNodeId = targetNodeId,
                        OriginNodeName = "Unknown",
                        TotalImpactPercentage = 0.0,
                        CriticalPathRisk = "Low"
                    };
                }

                var upstream = new HashSet<string>();
                foreach (var c in connList.Where(x => x.ToId == targetNodeId))
                {
                    var n = nodeList.FirstOrDefault(x => x.Id == c.FromId);
                    if (!string.IsNullOrEmpty(n.Name)) upstream.Add(n.Name);
                }

                var downstream = new HashSet<string>();
                foreach (var c in connList.Where(x => x.FromId == targetNodeId))
                {
                    var n = nodeList.FirstOrDefault(x => x.Id == c.ToId);
                    if (!string.IsNullOrEmpty(n.Name)) downstream.Add(n.Name);
                }

                int total = Math.Max(1, nodeList.Count);
                int impacted = upstream.Count + downstream.Count + 1;
                double pct = Math.Min(100.0, ((double)impacted / total) * 100.0);

                string risk = pct >= 66.0 ? "Critical (Tier-0 Core Failure)" :
                              pct >= 33.0 ? "High (Cross-Service Degradation)" :
                                            "Moderate (Isolated Component)";

                return new BlastRadiusReport
                {
                    OriginNodeId = target.Id,
                    OriginNodeName = target.Name,
                    AffectedUpstreamNodes = upstream.OrderBy(x => x).ToList(),
                    AffectedDownstreamNodes = downstream.OrderBy(x => x).ToList(),
                    TotalImpactPercentage = pct,
                    CriticalPathRisk = risk
                };
            }
        }

        public ChaosExperimentSpec GenerateChaosExperiment(string type, string targetNodeName, int durationSec = 60)
        {
            lock (_lock)
            {
                var safeName = targetNodeName.ToLowerInvariant().Replace(" ", "-");
                var experimentId = $"chaos-{safeName}-{Guid.NewGuid().ToString().Substring(0, 6)}";

                string yaml;
                if (type.Equals("networkLatency", StringComparison.OrdinalIgnoreCase))
                {
                    yaml = $@"apiVersion: chaos-mesh.org/v1alpha1
kind: NetworkChaos
metadata:
  name: {experimentId}
  namespace: default
spec:
  action: delay
  mode: all
  selector:
    labelSelectors:
      app.kubernetes.io/name: {safeName}
  delay:
    latency: ""250ms""
    jitter: ""50ms""
  duration: ""{durationSec}s""";
                }
                else if (type.Equals("cpuSpike", StringComparison.OrdinalIgnoreCase))
                {
                    yaml = $@"apiVersion: chaos-mesh.org/v1alpha1
kind: StressChaos
metadata:
  name: {experimentId}
  namespace: default
spec:
  mode: one
  selector:
    labelSelectors:
      app.kubernetes.io/name: {safeName}
  stressors:
    cpu:
      workers: 4
      load: 90
  duration: ""{durationSec}s""";
                }
                else
                {
                    // default podKill
                    yaml = $@"apiVersion: chaos-mesh.org/v1alpha1
kind: PodChaos
metadata:
  name: {experimentId}
  namespace: default
spec:
  action: pod-kill
  mode: one
  selector:
    namespaces:
      - default
    labelSelectors:
      app.kubernetes.io/name: {safeName}
  duration: ""{durationSec}s""
  scheduler:
    cron: ""@every 5m""";
                }

                string rollback = $@"# Rollback Runbook for {experimentId}
kubectl delete -f {experimentId}.yaml --ignore-not-found=true
kubectl rollout status deployment/{safeName} --timeout=60s";

                return new ChaosExperimentSpec
                {
                    ExperimentId = experimentId,
                    Type = type,
                    TargetNodeName = targetNodeName,
                    DurationSec = durationSec,
                    BlastRadiusPct = 35.0,
                    YamlManifest = yaml,
                    RollbackRunbook = rollback
                };
            }
        }
    }
}
