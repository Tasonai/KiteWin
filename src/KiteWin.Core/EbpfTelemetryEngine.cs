//
// EbpfTelemetryEngine.cs
// KiteWin.Core
//
// Zero-Instrumentation eBPF Kernel Telemetry & Shadow Service Map Engine.
// Captures L4/L7 socket flows (TCP RTT, TLS handshake, HTTP/2 & gRPC wire latency),
// diffs kernel socket edges against declared architecture connectors to uncover shadow APIs
// and unauthorized WAN egress, and synthesizes CiliumNetworkPolicy + Tetragon TracingPolicy CRDs.
//

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace KiteWin.Core
{
    public enum EbpfWireProtocol
    {
        [JsonPropertyName("HTTP/2")]
        Http2,
        [JsonPropertyName("gRPC")]
        Grpc,
        [JsonPropertyName("PostgreSQL-Wire")]
        PostgresWire,
        [JsonPropertyName("Redis-RESP3")]
        RedisResp,
        [JsonPropertyName("TCP-TLS1.3")]
        RawTcp
    }

    public class KernelSocketFlow
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("sourceWorkload")]
        public string SourceWorkload { get; set; } = string.Empty;

        [JsonPropertyName("destinationWorkload")]
        public string DestinationWorkload { get; set; } = string.Empty;

        [JsonPropertyName("destinationPort")]
        public int DestinationPort { get; set; }

        [JsonPropertyName("wireProtocol")]
        public string WireProtocol { get; set; } = "gRPC";

        [JsonPropertyName("tcpRttMs")]
        public double TcpRttMs { get; set; }

        [JsonPropertyName("tlsHandshakeMs")]
        public double TlsHandshakeMs { get; set; }

        [JsonPropertyName("p99WireLatencyMs")]
        public double P99WireLatencyMs { get; set; }

        [JsonPropertyName("packetRetransmitRate")]
        public double PacketRetransmitRate { get; set; }

        [JsonPropertyName("bytesPerSecond")]
        public int BytesPerSecond { get; set; }

        [JsonPropertyName("isExternalEgress")]
        public bool IsExternalEgress { get; set; }
    }

    public class DeclaredCanvasEdge
    {
        [JsonPropertyName("source")]
        public string Source { get; set; } = string.Empty;

        [JsonPropertyName("destination")]
        public string Destination { get; set; } = string.Empty;
    }

    public class ShadowDependencyFinding
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("severity")]
        public string Severity { get; set; } = string.Empty;

        [JsonPropertyName("sourceWorkload")]
        public string SourceWorkload { get; set; } = string.Empty;

        [JsonPropertyName("destinationWorkload")]
        public string DestinationWorkload { get; set; } = string.Empty;

        [JsonPropertyName("destinationPort")]
        public int DestinationPort { get; set; }

        [JsonPropertyName("category")]
        public string Category { get; set; } = string.Empty;

        [JsonPropertyName("recommendation")]
        public string Recommendation { get; set; } = string.Empty;
    }

    public class ShadowAuditReport
    {
        [JsonPropertyName("inspectedFlows")]
        public int InspectedFlows { get; set; }

        [JsonPropertyName("declaredEdgesCount")]
        public int DeclaredEdgesCount { get; set; }

        [JsonPropertyName("undocumentedEdgesCount")]
        public int UndocumentedEdgesCount { get; set; }

        [JsonPropertyName("rogueEgressCount")]
        public int RogueEgressCount { get; set; }

        [JsonPropertyName("complianceScore")]
        public int ComplianceScore { get; set; }

        [JsonPropertyName("findings")]
        public List<ShadowDependencyFinding> Findings { get; set; } = new();
    }

    public class EbpfPolicyBundle
    {
        [JsonPropertyName("namespace")]
        public string Namespace { get; set; } = string.Empty;

        [JsonPropertyName("tetragonTracingPolicyYaml")]
        public string TetragonTracingPolicyYaml { get; set; } = string.Empty;

        [JsonPropertyName("ciliumNetworkPolicyYaml")]
        public string CiliumNetworkPolicyYaml { get; set; } = string.Empty;

        [JsonPropertyName("ebpfSockopsProbeC")]
        public string EbpfSockopsProbeC { get; set; } = string.Empty;
    }

    public class EbpfTelemetryEngine
    {
        private static readonly Lazy<EbpfTelemetryEngine> _instance = new(() => new EbpfTelemetryEngine());
        public static EbpfTelemetryEngine Instance => _instance.Value;

        private readonly object _lock = new();
        private readonly List<KernelSocketFlow> _flows = new();

        public EbpfTelemetryEngine()
        {
            SeedKernelFlows();
        }

        private void SeedKernelFlows()
        {
            _flows.Add(new KernelSocketFlow
            {
                Id = "flow-kprobe-01",
                SourceWorkload = "api-gateway",
                DestinationWorkload = "auth-service",
                DestinationPort = 8443,
                WireProtocol = "gRPC",
                TcpRttMs = 0.42,
                TlsHandshakeMs = 1.85,
                P99WireLatencyMs = 3.10,
                PacketRetransmitRate = 0.0001,
                BytesPerSecond = 1420000,
                IsExternalEgress = false
            });
            _flows.Add(new KernelSocketFlow
            {
                Id = "flow-kprobe-02",
                SourceWorkload = "auth-service",
                DestinationWorkload = "users-postgres",
                DestinationPort = 5432,
                WireProtocol = "PostgreSQL-Wire",
                TcpRttMs = 0.31,
                TlsHandshakeMs = 1.20,
                P99WireLatencyMs = 2.45,
                PacketRetransmitRate = 0.0,
                BytesPerSecond = 890000,
                IsExternalEgress = false
            });
            _flows.Add(new KernelSocketFlow
            {
                Id = "flow-kprobe-03",
                SourceWorkload = "checkout-worker",
                DestinationWorkload = "legacy-Shadow-Billing",
                DestinationPort = 9090,
                WireProtocol = "HTTP/2",
                TcpRttMs = 1.95,
                TlsHandshakeMs = 0.0,
                P99WireLatencyMs = 28.40,
                PacketRetransmitRate = 0.012,
                BytesPerSecond = 310000,
                IsExternalEgress = false
            });
            _flows.Add(new KernelSocketFlow
            {
                Id = "flow-kprobe-04",
                SourceWorkload = "checkout-worker",
                DestinationWorkload = "telemetry-collector.unverified-wan.io",
                DestinationPort = 443,
                WireProtocol = "TCP-TLS1.3",
                TcpRttMs = 34.80,
                TlsHandshakeMs = 48.20,
                P99WireLatencyMs = 92.10,
                PacketRetransmitRate = 0.004,
                BytesPerSecond = 64000,
                IsExternalEgress = true
            });
        }

        public List<KernelSocketFlow> GetKernelFlows()
        {
            lock (_lock)
            {
                return new List<KernelSocketFlow>(_flows);
            }
        }

        public void RecordKernelFlow(KernelSocketFlow flow)
        {
            lock (_lock)
            {
                _flows.RemoveAll(f => f.Id == flow.Id);
                _flows.Add(flow);
            }
        }

        public ShadowAuditReport AuditShadowDependencies(List<DeclaredCanvasEdge>? declaredEdges = null)
        {
            List<KernelSocketFlow> activeFlows;
            lock (_lock)
            {
                activeFlows = new List<KernelSocketFlow>(_flows);
            }

            var baselineEdges = declaredEdges ?? new List<DeclaredCanvasEdge>
            {
                new DeclaredCanvasEdge { Source = "api-gateway", Destination = "auth-service" },
                new DeclaredCanvasEdge { Source = "auth-service", Destination = "users-postgres" }
            };

            var declaredSet = new HashSet<string>(
                baselineEdges.Select(e => $"{e.Source.ToLowerInvariant()}->{e.Destination.ToLowerInvariant()}")
            );

            var findings = new List<ShadowDependencyFinding>();
            int undocumentedCount = 0;
            int rogueEgressCount = 0;

            foreach (var flow in activeFlows)
            {
                var key = $"{flow.SourceWorkload.ToLowerInvariant()}->{flow.DestinationWorkload.ToLowerInvariant()}";
                if (flow.IsExternalEgress && !declaredSet.Contains(key))
                {
                    rogueEgressCount++;
                    findings.Add(new ShadowDependencyFinding
                    {
                        Id = $"SHADOW-EGRESS-{flow.Id}",
                        Severity = "critical",
                        SourceWorkload = flow.SourceWorkload,
                        DestinationWorkload = flow.DestinationWorkload,
                        DestinationPort = flow.DestinationPort,
                        Category = "rogue-wan-egress",
                        Recommendation = $"Block unauthorized outbound WAN socket to {flow.DestinationWorkload}:{flow.DestinationPort} via CiliumNetworkPolicy egress deny rule."
                    });
                }
                else if (!declaredSet.Contains(key))
                {
                    undocumentedCount++;
                    var unencryptedNote = flow.TlsHandshakeMs == 0.0 ? " (unencrypted plaintext socket)" : "";
                    findings.Add(new ShadowDependencyFinding
                    {
                        Id = $"SHADOW-SVC-{flow.Id}",
                        Severity = "high",
                        SourceWorkload = flow.SourceWorkload,
                        DestinationWorkload = flow.DestinationWorkload,
                        DestinationPort = flow.DestinationPort,
                        Category = "undocumented-shadow-api",
                        Recommendation = $"Promote {flow.SourceWorkload} -> {flow.DestinationWorkload}:{flow.DestinationPort}{unencryptedNote} onto architecture canvas or enforce mTLS SPIFFE identity."
                    });
                }
            }

            int penalty = (rogueEgressCount * 30) + (undocumentedCount * 15);
            int score = Math.Max(0, 100 - penalty);

            return new ShadowAuditReport
            {
                InspectedFlows = activeFlows.Count,
                DeclaredEdgesCount = baselineEdges.Count,
                UndocumentedEdgesCount = undocumentedCount,
                RogueEgressCount = rogueEgressCount,
                ComplianceScore = score,
                Findings = findings
            };
        }

        public EbpfPolicyBundle GenerateEbpfPolicy(string ns = "kite-prod", bool blockRogueEgress = true)
        {
            var tetragonYaml = $@"apiVersion: cilium.io/v1alpha1
kind: TracingPolicyNamespaced
metadata:
  name: kite-zero-trust-socket-monitor
  namespace: {ns}
spec:
  kprobes:
    - call: ""tcp_connect""
      syscall: false
      args:
        - index: 0
          type: ""sock""
      selectors:
        - matchNamespaces:
            - namespace: Net
              operator: NotIn
              values:
                - ""host_ns""
    - call: ""tcp_close""
      syscall: false
      args:
        - index: 0
          type: ""sock""";

            var egressDirective = blockRogueEgress
                ? $@"  egress:
    - toEndpoints:
        - matchLabels:
            k8s:io.kubernetes.pod.namespace: {ns}
  egressDeny:
    - toCIDRSet:
        - cidr: 0.0.0.0/0
          except:
            - 10.0.0.0/8
            - 172.16.0.0/12"
                : @"  egress:
    - toEntities:
        - cluster
        - world";

            var ciliumYaml = $@"apiVersion: cilium.io/v2
kind: CiliumNetworkPolicy
metadata:
  name: kite-canvas-enforced-topology
  namespace: {ns}
spec:
  endpointSelector:
    matchLabels:
      app.kubernetes.io/managed-by: kite-architect
{egressDirective}";

            var probeC = @"#include <linux/bpf.h>
#include <bpf/bpf_helpers.h>
#include <bpf/bpf_endian.h>

struct kite_flow_metric {
    __u32 src_ip;
    __u32 dst_ip;
    __u16 dst_port;
    __u32 srtt_us;
};

struct {
    __uint(type, BPF_MAP_TYPE_RINGBUF);
    __uint(max_entries, 262144);
} kite_socket_events SEC("".maps"");

SEC(""sockops"")
int kite_trace_sockops(struct bpf_sock_ops *skops) {
    if (skops->op == BPF_SOCK_OPS_STATE_CB || skops->op == BPF_SOCK_OPS_RTT_CB) {
        struct kite_flow_metric *evt = bpf_ringbuf_reserve(&kite_socket_events, sizeof(*evt), 0);
        if (!evt) return 1;
        evt->src_ip = skops->local_ip4;
        evt->dst_ip = skops->remote_ip4;
        evt->dst_port = bpf_ntohl(skops->remote_port) >> 16;
        evt->srtt_us = skops->srtt_us >> 3;
        bpf_ringbuf_submit(evt, 0);
    }
    return 1;
}

char LICENSE[] SEC(""license"") = ""Dual BSD/GPL"";";

            return new EbpfPolicyBundle
            {
                Namespace = ns,
                TetragonTracingPolicyYaml = tetragonYaml,
                CiliumNetworkPolicyYaml = ciliumYaml,
                EbpfSockopsProbeC = probeC
            };
        }
    }
}
