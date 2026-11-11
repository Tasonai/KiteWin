//
// Spatial3DTopologyEngine.cs
// KiteWin.Core
//
// 3D Spatial Architecture Topology & Immersive Scene Graph Engine.
// Projects 2D architecture canvases onto 5 semantic Z-elevation strata (Edge, Presentation,
// Domain, Event Streaming, Stateful Storage), executes 3D force-directed spring-repulsion
// relaxation, performs analytic ray-sphere picking, and exports glTF 2.0 JSON scene graphs.
//

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace KiteWin.Core
{
    public class Spatial3DNode
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("label")]
        public string Label { get; set; } = string.Empty;

        [JsonPropertyName("stratum")]
        public string Stratum { get; set; } = string.Empty;

        [JsonPropertyName("x")]
        public double X { get; set; }

        [JsonPropertyName("y")]
        public double Y { get; set; }

        [JsonPropertyName("z")]
        public double Z { get; set; }

        [JsonPropertyName("boundingRadius")]
        public double BoundingRadius { get; set; } = 12.0;
    }

    public class Spatial3DEdge
    {
        [JsonPropertyName("sourceId")]
        public string SourceId { get; set; } = string.Empty;

        [JsonPropertyName("targetId")]
        public string TargetId { get; set; } = string.Empty;

        [JsonPropertyName("protocol")]
        public string Protocol { get; set; } = "gRPC";
    }

    public class Spatial3DScene
    {
        [JsonPropertyName("sceneId")]
        public string SceneId { get; set; } = "kite-spatial3d-scene-01";

        [JsonPropertyName("strataCount")]
        public int StrataCount { get; set; } = 5;

        [JsonPropertyName("nodes")]
        public List<Spatial3DNode> Nodes { get; set; } = new();

        [JsonPropertyName("edges")]
        public List<Spatial3DEdge> Edges { get; set; } = new();
    }

    public class RayPickHit
    {
        [JsonPropertyName("hit")]
        public bool Hit { get; set; }

        [JsonPropertyName("nodeId")]
        public string? NodeId { get; set; }

        [JsonPropertyName("distance")]
        public double Distance { get; set; }
    }

    public class GltfExportBundle
    {
        [JsonPropertyName("formatVersion")]
        public string FormatVersion { get; set; } = "glTF-2.0";

        [JsonPropertyName("nodeCount")]
        public int NodeCount { get; set; }

        [JsonPropertyName("gltfJson")]
        public string GltfJson { get; set; } = string.Empty;
    }

    public class Spatial3DTopologyEngine
    {
        private static readonly Lazy<Spatial3DTopologyEngine> _instance = new(() => new Spatial3DTopologyEngine());
        public static Spatial3DTopologyEngine Instance => _instance.Value;

        private readonly object _lock = new();
        private Spatial3DScene _scene = new();

        public Spatial3DTopologyEngine()
        {
            SeedScene();
        }

        private void SeedScene()
        {
            _scene = new Spatial3DScene
            {
                SceneId = "kite-spatial3d-scene-01",
                StrataCount = 5,
                Nodes = new List<Spatial3DNode>
                {
                    new Spatial3DNode { Id = "node-cdn", Label = "Global Edge WASI CDN", Stratum = "Z0-Edge", X = 0.0, Y = 40.0, Z = 0.0, BoundingRadius = 12.0 },
                    new Spatial3DNode { Id = "node-bff", Label = "Web & Mobile BFF", Stratum = "Z1-Presentation", X = -24.0, Y = 20.0, Z = -40.0, BoundingRadius = 12.0 },
                    new Spatial3DNode { Id = "node-orders", Label = "Orders Domain Service", Stratum = "Z2-Domain", X = 0.0, Y = 0.0, Z = -80.0, BoundingRadius = 14.0 },
                    new Spatial3DNode { Id = "node-kafka", Label = "Kafka Event Backbone", Stratum = "Z3-Streaming", X = 28.0, Y = -16.0, Z = -120.0, BoundingRadius = 12.0 },
                    new Spatial3DNode { Id = "node-pg", Label = "CockroachDB Ledger", Stratum = "Z4-StatefulStorage", X = 0.0, Y = -36.0, Z = -160.0, BoundingRadius = 16.0 }
                },
                Edges = new List<Spatial3DEdge>
                {
                    new Spatial3DEdge { SourceId = "node-cdn", TargetId = "node-bff", Protocol = "HTTP/3" },
                    new Spatial3DEdge { SourceId = "node-bff", TargetId = "node-orders", Protocol = "gRPC" },
                    new Spatial3DEdge { SourceId = "node-orders", TargetId = "node-kafka", Protocol = "Kafka-TLS" },
                    new Spatial3DEdge { SourceId = "node-orders", TargetId = "node-pg", Protocol = "PostgreSQL-Wire" }
                }
            };
        }

        public Spatial3DScene GetScene()
        {
            lock (_lock)
            {
                return _scene;
            }
        }

        public Spatial3DScene ComputeForceDirectedLayout(int iterations = 12)
        {
            lock (_lock)
            {
                var nodes = _scene.Nodes.Select(n => new Spatial3DNode
                {
                    Id = n.Id,
                    Label = n.Label,
                    Stratum = n.Stratum,
                    X = n.X,
                    Y = n.Y,
                    Z = StratumElevationZ(n.Stratum),
                    BoundingRadius = n.BoundingRadius
                }).ToList();

                int steps = Math.Clamp(iterations, 1, 50);
                const double repulsionK = 180.0;

                for (int iter = 0; iter < steps; iter++)
                {
                    for (int i = 0; i < nodes.Count; i++)
                    {
                        double fx = 0.0;
                        double fy = 0.0;
                        for (int j = 0; j < nodes.Count; j++)
                        {
                            if (i == j) continue;
                            double dx = nodes[i].X - nodes[j].X;
                            double dy = nodes[i].Y - nodes[j].Y;
                            double distSq = (dx * dx) + (dy * dy) + 16.0;
                            double dist = Math.Sqrt(distSq);
                            double force = repulsionK / distSq;
                            fx += (dx / dist) * force;
                            fy += (dy / dist) * force;
                        }
                        nodes[i].X = Math.Round(nodes[i].X + (fx * 0.25), 2);
                        nodes[i].Y = Math.Round(nodes[i].Y + (fy * 0.25), 2);
                    }
                }

                _scene.Nodes = nodes;
                return _scene;
            }
        }

        public RayPickHit RaycastPick(double originX, double originY, double originZ, double dirX, double dirY, double dirZ)
        {
            List<Spatial3DNode> nodes;
            lock (_lock)
            {
                nodes = new List<Spatial3DNode>(_scene.Nodes);
            }

            double len = Math.Sqrt((dirX * dirX) + (dirY * dirY) + (dirZ * dirZ));
            if (len < 1e-9)
                return new RayPickHit { Hit = false, NodeId = null, Distance = -1.0 };

            double dx = dirX / len;
            double dy = dirY / len;
            double dz = dirZ / len;

            string? closestNode = null;
            double closestT = double.MaxValue;

            foreach (var n in nodes)
            {
                double ocX = originX - n.X;
                double ocY = originY - n.Y;
                double ocZ = originZ - n.Z;

                double b = (ocX * dx) + (ocY * dy) + (ocZ * dz);
                double c = (ocX * ocX) + (ocY * ocY) + (ocZ * ocZ) - (n.BoundingRadius * n.BoundingRadius);
                double discriminant = (b * b) - c;

                if (discriminant >= 0.0)
                {
                    double t = -b - Math.Sqrt(discriminant);
                    if (t > 0.0 && t < closestT)
                    {
                        closestT = t;
                        closestNode = n.Id;
                    }
                }
            }

            if (closestNode != null)
            {
                return new RayPickHit
                {
                    Hit = true,
                    NodeId = closestNode,
                    Distance = Math.Round(closestT, 2)
                };
            }

            return new RayPickHit { Hit = false, NodeId = null, Distance = -1.0 };
        }

        public GltfExportBundle ExportGltfScene()
        {
            Spatial3DScene snap;
            lock (_lock)
            {
                snap = _scene;
            }

            var gltfNodes = string.Join(",\n    ", snap.Nodes.Select(n =>
                $"{{\"name\": \"{n.Id}\", \"translation\": [{n.X:F2}, {n.Y:F2}, {n.Z:F2}], \"extras\": {{\"label\": \"{n.Label}\", \"stratum\": \"{n.Stratum}\"}}}}"
            ));

            var indices = string.Join(", ", Enumerable.Range(0, snap.Nodes.Count));

            var gltf = $@"{{
  ""asset"": {{
    ""version"": ""2.0"",
    ""generator"": ""Kite Spatial3D Topology Exporter""
  }},
  ""scene"": 0,
  ""scenes"": [
    {{
      ""name"": ""{snap.SceneId}"",
      ""nodes"": [{indices}]
    }}
  ],
  ""nodes"": [
    {gltfNodes}
  ]
}}";

            return new GltfExportBundle
            {
                FormatVersion = "glTF-2.0",
                NodeCount = snap.Nodes.Count,
                GltfJson = gltf
            };
        }

        private static double StratumElevationZ(string stratum)
        {
            if (stratum.StartsWith("Z0")) return 0.0;
            if (stratum.StartsWith("Z1")) return -40.0;
            if (stratum.StartsWith("Z2")) return -80.0;
            if (stratum.StartsWith("Z3")) return -120.0;
            return -160.0;
        }
    }
}
