//
// TimeTravelReplayEngine.cs
// KiteWin.Core
//
// 4D Time-Travel Canvas Replay & Incident Post-Mortem Engine.
// Reconstructs architecture topology snapshots across historical git commits and Merkle audit blocks,
// computes visual and semantic temporal diffs, and correlates production incidents with topology mutations.
//

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace KiteWin.Core
{
    public class ReplayCanvasNode
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("type")]
        public string Type { get; set; } = "service";

        [JsonPropertyName("x")]
        public double X { get; set; } = 0.0;

        [JsonPropertyName("y")]
        public double Y { get; set; } = 0.0;
    }

    public class ReplayCanvasConnector
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        [JsonPropertyName("fromId")]
        public string FromId { get; set; } = string.Empty;

        [JsonPropertyName("toId")]
        public string ToId { get; set; } = string.Empty;

        [JsonPropertyName("label")]
        public string Label { get; set; } = string.Empty;

        [JsonPropertyName("protocolType")]
        public string ProtocolType { get; set; } = "direct";
    }

    public class TimelineSnapshot
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("timestamp")]
        public string Timestamp { get; set; } = string.Empty;

        [JsonPropertyName("author")]
        public string Author { get; set; } = string.Empty;

        [JsonPropertyName("message")]
        public string Message { get; set; } = string.Empty;

        [JsonPropertyName("nodeCount")]
        public int NodeCount { get; set; }

        [JsonPropertyName("connectorCount")]
        public int ConnectorCount { get; set; }

        [JsonPropertyName("nodes")]
        public List<ReplayCanvasNode> Nodes { get; set; } = new();

        [JsonPropertyName("connectors")]
        public List<ReplayCanvasConnector> Connectors { get; set; } = new();

        [JsonPropertyName("incidentIds")]
        public List<string> IncidentIds { get; set; } = new();
    }

    public class CanvasNodeDiff
    {
        [JsonPropertyName("id")]
        public string Id => NodeId;

        [JsonPropertyName("nodeId")]
        public string NodeId { get; set; } = string.Empty;

        [JsonPropertyName("nodeName")]
        public string NodeName { get; set; } = string.Empty;

        [JsonPropertyName("changeType")]
        public string ChangeType { get; set; } = "modified"; // "added", "removed", "modified"

        [JsonPropertyName("details")]
        public string Details { get; set; } = string.Empty;
    }

    public class TemporalDeltaDiff
    {
        [JsonPropertyName("fromSnapshotId")]
        public string FromSnapshotId { get; set; } = string.Empty;

        [JsonPropertyName("toSnapshotId")]
        public string ToSnapshotId { get; set; } = string.Empty;

        [JsonPropertyName("addedNodes")]
        public List<ReplayCanvasNode> AddedNodes { get; set; } = new();

        [JsonPropertyName("removedNodes")]
        public List<ReplayCanvasNode> RemovedNodes { get; set; } = new();

        [JsonPropertyName("modifiedNodes")]
        public List<CanvasNodeDiff> ModifiedNodes { get; set; } = new();

        [JsonPropertyName("addedConnectors")]
        public List<ReplayCanvasConnector> AddedConnectors { get; set; } = new();

        [JsonPropertyName("removedConnectors")]
        public List<ReplayCanvasConnector> RemovedConnectors { get; set; } = new();

        [JsonPropertyName("summary")]
        public string Summary { get; set; } = string.Empty;
    }

    public class IncidentEvent
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("title")]
        public string Title { get; set; } = string.Empty;

        [JsonPropertyName("severity")]
        public string Severity { get; set; } = "P2"; // "P1", "P2", "P3"

        [JsonPropertyName("service")]
        public string Service { get; set; } = string.Empty;

        [JsonPropertyName("timestamp")]
        public string Timestamp { get; set; } = string.Empty;

        [JsonPropertyName("status")]
        public string Status { get; set; } = "resolved"; // "resolved", "investigating"

        [JsonPropertyName("mttrMinutes")]
        public int MttrMinutes { get; set; }

        [JsonPropertyName("suspectedSnapshotId")]
        public string SuspectedSnapshotId { get; set; } = string.Empty;
    }

    public class IncidentCorrelationResult
    {
        [JsonPropertyName("incident")]
        public IncidentEvent Incident { get; set; } = new();

        [JsonPropertyName("suspectedSnapshot")]
        public TimelineSnapshot SuspectedSnapshot { get; set; }

        [JsonPropertyName("probableCauses")]
        public List<string> ProbableCauses { get; set; } = new();

        [JsonPropertyName("confidenceScore")]
        public double ConfidenceScore { get; set; }

        [JsonPropertyName("recommendations")]
        public List<string> Recommendations { get; set; } = new();
    }

    public class TimeTravelReplayEngine
    {
        private static readonly Lazy<TimeTravelReplayEngine> _instance = new(() => new TimeTravelReplayEngine());
        public static TimeTravelReplayEngine Instance => _instance.Value;

        private readonly object _lock = new();
        private readonly List<TimelineSnapshot> _snapshots = new();
        private readonly List<IncidentEvent> _incidents = new();

        public TimeTravelReplayEngine()
        {
            SeedInitialHistory();
        }

        private void SeedInitialHistory()
        {
            var nodeGateway = new ReplayCanvasNode { Id = "node-gw", Name = "API Gateway", Type = "api-gateway", X = 100, Y = 100 };
            var nodeAuth = new ReplayCanvasNode { Id = "node-auth", Name = "Auth Service", Type = "api-service", X = 300, Y = 100 };
            var nodeDb = new ReplayCanvasNode { Id = "node-db", Name = "Primary Postgres", Type = "postgres-database", X = 500, Y = 100 };
            var nodePayment = new ReplayCanvasNode { Id = "node-pay", Name = "Payment Service", Type = "api-service", X = 300, Y = 250 };
            var nodeLegacyBilling = new ReplayCanvasNode { Id = "node-bill", Name = "Billing Service v1", Type = "api-service", X = 500, Y = 250 };

            var conn1 = new ReplayCanvasConnector { Id = "c1", FromId = "node-gw", ToId = "node-auth", Label = "JWT Auth", ProtocolType = "https" };
            var conn2 = new ReplayCanvasConnector { Id = "c2", FromId = "node-auth", ToId = "node-db", Label = "Read/Write", ProtocolType = "postgresql" };
            var conn3 = new ReplayCanvasConnector { Id = "c3", FromId = "node-gw", ToId = "node-pay", Label = "Process Checkout", ProtocolType = "https" };
            var conn4 = new ReplayCanvasConnector { Id = "c4", FromId = "node-pay", ToId = "node-bill", Label = "Legacy RPC", ProtocolType = "grpc" };

            var snap1 = new TimelineSnapshot
            {
                Id = "snap-v1.0.0",
                Timestamp = "2026-11-01T08:00:00Z",
                Author = "devops@kite.internal",
                Message = "Initial production microservice deployment",
                NodeCount = 3,
                ConnectorCount = 2,
                Nodes = new List<ReplayCanvasNode> { nodeGateway, nodeAuth, nodeDb },
                Connectors = new List<ReplayCanvasConnector> { conn1, conn2 }
            };

            var snap2 = new TimelineSnapshot
            {
                Id = "snap-v1.2.0",
                Timestamp = "2026-11-05T14:30:00Z",
                Author = "payments-team@kite.internal",
                Message = "Add payment and legacy synchronous billing integration",
                NodeCount = 5,
                ConnectorCount = 4,
                Nodes = new List<ReplayCanvasNode> { nodeGateway, nodeAuth, nodeDb, nodePayment, nodeLegacyBilling },
                Connectors = new List<ReplayCanvasConnector> { conn1, conn2, conn3, conn4 },
                IncidentIds = new List<string> { "INC-7102" }
            };

            var nodeModernBilling = new ReplayCanvasNode { Id = "node-bill-v2", Name = "Billing Service v2", Type = "api-service", X = 500, Y = 250 };
            var nodeKafka = new ReplayCanvasNode { Id = "node-mq", Name = "Payment Events MQ", Type = "kafka-queue", X = 400, Y = 350 };
            var conn5 = new ReplayCanvasConnector { Id = "c5", FromId = "node-pay", ToId = "node-mq", Label = "Publish Invoice", ProtocolType = "kafka" };
            var conn6 = new ReplayCanvasConnector { Id = "c6", FromId = "node-mq", ToId = "node-bill-v2", Label = "Subscribe", ProtocolType = "kafka" };

            var snap3 = new TimelineSnapshot
            {
                Id = "snap-v1.3.0",
                Timestamp = "2026-11-09T18:00:00Z",
                Author = "sre-core@kite.internal",
                Message = "Decouple payment pipeline with async message queue",
                NodeCount = 6,
                ConnectorCount = 5,
                Nodes = new List<ReplayCanvasNode> { nodeGateway, nodeAuth, nodeDb, nodePayment, nodeModernBilling, nodeKafka },
                Connectors = new List<ReplayCanvasConnector> { conn1, conn2, conn3, conn5, conn6 }
            };

            _snapshots.Add(snap1);
            _snapshots.Add(snap2);
            _snapshots.Add(snap3);

            var inc1 = new IncidentEvent
            {
                Id = "INC-7102",
                Title = "Cascading Timeout in Checkout Path during Flash Sale",
                Severity = "P1",
                Service = "Payment Service",
                Timestamp = "2026-11-05T16:15:00Z",
                Status = "resolved",
                MttrMinutes = 42,
                SuspectedSnapshotId = "snap-v1.2.0"
            };
            _incidents.Add(inc1);
        }

        public List<TimelineSnapshot> GetTimeline()
        {
            lock (_lock)
            {
                return new List<TimelineSnapshot>(_snapshots);
            }
        }

        public TimelineSnapshot GetSnapshot(string id)
        {
            lock (_lock)
            {
                return _snapshots.FirstOrDefault(s => s.Id == id);
            }
        }

        public TimelineSnapshot RecordSnapshot(string id, string author, string message, List<ReplayCanvasNode> nodes, List<ReplayCanvasConnector> connectors, List<string> incidentIds = null)
        {
            lock (_lock)
            {
                var snapId = string.IsNullOrEmpty(id) ? $"snap-{Guid.NewGuid().ToString().Substring(0, 8)}" : id;
                var snapshot = new TimelineSnapshot
                {
                    Id = snapId,
                    Timestamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                    Author = author,
                    Message = message,
                    NodeCount = nodes?.Count ?? 0,
                    ConnectorCount = connectors?.Count ?? 0,
                    Nodes = nodes ?? new List<ReplayCanvasNode>(),
                    Connectors = connectors ?? new List<ReplayCanvasConnector>(),
                    IncidentIds = incidentIds ?? new List<string>()
                };
                _snapshots.Add(snapshot);
                return snapshot;
            }
        }

        public TemporalDeltaDiff ComputeDiff(string fromId, string toId)
        {
            lock (_lock)
            {
                var fromSnap = _snapshots.FirstOrDefault(s => s.Id == fromId);
                var toSnap = _snapshots.FirstOrDefault(s => s.Id == toId);

                if (fromSnap == null || toSnap == null)
                {
                    return new TemporalDeltaDiff
                    {
                        FromSnapshotId = fromId,
                        ToSnapshotId = toId,
                        Summary = "Invalid snapshot IDs provided."
                    };
                }

                var fromDict = fromSnap.Nodes.ToDictionary(n => n.Id, n => n);
                var toDict = toSnap.Nodes.ToDictionary(n => n.Id, n => n);

                var addedNodes = new List<ReplayCanvasNode>();
                var removedNodes = new List<ReplayCanvasNode>();
                var modifiedNodes = new List<CanvasNodeDiff>();

                foreach (var kvp in toDict)
                {
                    if (fromDict.TryGetValue(kvp.Key, out var oldNode))
                    {
                        if (oldNode.Name != kvp.Value.Name || oldNode.Type != kvp.Value.Type)
                        {
                            modifiedNodes.Add(new CanvasNodeDiff
                            {
                                NodeId = kvp.Key,
                                NodeName = kvp.Value.Name,
                                ChangeType = "modified",
                                Details = $"Name '{oldNode.Name}' -> '{kvp.Value.Name}', Type '{oldNode.Type}' -> '{kvp.Value.Type}'"
                            });
                        }
                    }
                    else
                    {
                        addedNodes.Add(kvp.Value);
                    }
                }

                foreach (var kvp in fromDict)
                {
                    if (!toDict.ContainsKey(kvp.Key))
                    {
                        removedNodes.Add(kvp.Value);
                    }
                }

                var fromConnSet = new HashSet<string>(fromSnap.Connectors.Select(c => $"{c.FromId}->{c.ToId}:{c.Label ?? ""}"));
                var toConnSet = new HashSet<string>(toSnap.Connectors.Select(c => $"{c.FromId}->{c.ToId}:{c.Label ?? ""}"));

                var addedConnectors = toSnap.Connectors.Where(c => !fromConnSet.Contains($"{c.FromId}->{c.ToId}:{c.Label ?? ""}")).ToList();
                var removedConnectors = fromSnap.Connectors.Where(c => !toConnSet.Contains($"{c.FromId}->{c.ToId}:{c.Label ?? ""}")).ToList();

                var summary = $"Temporal diff from {fromId} to {toId}: +{addedNodes.Count} nodes, -{removedNodes.Count} nodes, ~{modifiedNodes.Count} modified nodes, +{addedConnectors.Count} edges, -{removedConnectors.Count} edges.";

                return new TemporalDeltaDiff
                {
                    FromSnapshotId = fromId,
                    ToSnapshotId = toId,
                    AddedNodes = addedNodes,
                    RemovedNodes = removedNodes,
                    ModifiedNodes = modifiedNodes,
                    AddedConnectors = addedConnectors,
                    RemovedConnectors = removedConnectors,
                    Summary = summary
                };
            }
        }

        public List<IncidentEvent> GetIncidents()
        {
            lock (_lock)
            {
                return new List<IncidentEvent>(_incidents);
            }
        }

        public IncidentEvent RegisterIncident(string id, string title, string severity, string service, string timestamp, string status, int mttrMinutes, string suspectedSnapshotId = null)
        {
            lock (_lock)
            {
                var inc = new IncidentEvent
                {
                    Id = id,
                    Title = title,
                    Severity = severity,
                    Service = service,
                    Timestamp = timestamp,
                    Status = status,
                    MttrMinutes = mttrMinutes,
                    SuspectedSnapshotId = suspectedSnapshotId ?? string.Empty
                };
                _incidents.Add(inc);
                return inc;
            }
        }

        public IncidentCorrelationResult CorrelateIncident(string incidentId)
        {
            lock (_lock)
            {
                var inc = _incidents.FirstOrDefault(i => i.Id == incidentId);
                if (inc == null)
                {
                    return new IncidentCorrelationResult
                    {
                        Incident = new IncidentEvent { Id = incidentId, Title = "Unknown Incident", Severity = "P3", Status = "investigating" },
                        SuspectedSnapshot = null,
                        ProbableCauses = new List<string> { "Incident record not found in telemetry registry." },
                        ConfidenceScore = 0.0,
                        Recommendations = new List<string> { "Register incident details via telemetry API." }
                    };
                }

                TimelineSnapshot matchedSnapshot = null;
                if (!string.IsNullOrEmpty(inc.SuspectedSnapshotId))
                {
                    matchedSnapshot = _snapshots.FirstOrDefault(s => s.Id == inc.SuspectedSnapshotId);
                }

                if (matchedSnapshot == null)
                {
                    matchedSnapshot = _snapshots.FirstOrDefault(s => s.IncidentIds.Contains(inc.Id));
                }

                var causes = new List<string>();
                var recommendations = new List<string>();
                var confidence = 0.85;

                if (matchedSnapshot != null)
                {
                    causes.Add($"Mutation introduced in snapshot '{matchedSnapshot.Id}' by {matchedSnapshot.Author}: '{matchedSnapshot.Message}'");
                    if (matchedSnapshot.Connectors.Any(c => (c.Label?.ToLowerInvariant().Contains("sync") == true) || (c.Label?.ToLowerInvariant().Contains("rpc") == true)))
                    {
                        causes.Add("Synchronous tight coupling detected on critical transaction path without circuit breaker.");
                        recommendations.Add("Decouple synchronous dependency with asynchronous Kafka/RabbitMQ message queue.");
                        confidence = 0.94;
                    }
                    recommendations.Add("Introduce resilient retry backoff and circuit breaker pattern on upstream callers.");
                }
                else
                {
                    causes.Add("Incident occurred during active runtime without an immediately preceding tagged topology deployment.");
                    recommendations.Add("Enable high-resolution audit tracking to automatically checkpoint canvas mutations.");
                    confidence = 0.50;
                }

                return new IncidentCorrelationResult
                {
                    Incident = inc,
                    SuspectedSnapshot = matchedSnapshot,
                    ProbableCauses = causes,
                    ConfidenceScore = confidence,
                    Recommendations = recommendations
                };
            }
        }
    }
}
