using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace KiteWin.Core;

public sealed class CryptoPrimitiveEntry
{
    [JsonPropertyName("componentId")]
    public string ComponentId { get; init; } = "";

    [JsonPropertyName("usage")]
    public string Usage { get; init; } = "";

    [JsonPropertyName("algorithm")]
    public string Algorithm { get; set; } = "";

    [JsonPropertyName("nistStandard")]
    public string NistStandard { get; set; } = "";

    [JsonPropertyName("quantumSafe")]
    public bool QuantumSafe { get; set; }

    [JsonPropertyName("keyEpoch")]
    public int KeyEpoch { get; set; }
}

public sealed class CbomAuditReport
{
    [JsonPropertyName("bomFormat")]
    public string BomFormat { get; init; } = "CycloneDX-CBOM-1.6";

    [JsonPropertyName("totalPrimitives")]
    public int TotalPrimitives { get; init; }

    [JsonPropertyName("shorVulnerableCount")]
    public int ShorVulnerableCount { get; init; }

    [JsonPropertyName("readinessScorePercent")]
    public double ReadinessScorePercent { get; init; }

    [JsonPropertyName("entries")]
    public List<CryptoPrimitiveEntry> Entries { get; init; } = new();
}

public sealed class PqcRotationResult
{
    [JsonPropertyName("newEpoch")]
    public int NewEpoch { get; init; }

    [JsonPropertyName("upgradedCount")]
    public int UpgradedCount { get; init; }

    [JsonPropertyName("kemSuite")]
    public string KemSuite { get; init; } = "X25519MLKEM768";

    [JsonPropertyName("signatureSuite")]
    public string SignatureSuite { get; init; } = "ML-DSA-65";

    [JsonPropertyName("allQuantumSafe")]
    public bool AllQuantumSafe { get; init; }
}

public sealed class PqcTlsConfigBundle
{
    [JsonPropertyName("clusterName")]
    public string ClusterName { get; init; } = "";

    [JsonPropertyName("hybridKemGroup")]
    public string HybridKemGroup { get; init; } = "X25519MLKEM768";

    [JsonPropertyName("signatureAlgorithm")]
    public string SignatureAlgorithm { get; init; } = "ML-DSA-65";

    [JsonPropertyName("envoySdsYaml")]
    public string EnvoySdsYaml { get; init; } = "";

    [JsonPropertyName("cbomJson")]
    public string CbomJson { get; init; } = "";
}

public sealed class PostQuantumCryptoEngine
{
    public static PostQuantumCryptoEngine Instance { get; } = new();

    private readonly object _sync = new();
    private readonly List<CryptoPrimitiveEntry> _entries;
    private int _epoch = 1;

    public PostQuantumCryptoEngine()
    {
        _entries = new List<CryptoPrimitiveEntry>
        {
            new()
            {
                ComponentId = "edge-ingress-gateway",
                Usage = "mTLS Key Exchange",
                Algorithm = "X25519MLKEM768",
                NistStandard = "FIPS-203",
                QuantumSafe = true,
                KeyEpoch = 1
            },
            new()
            {
                ComponentId = "auth-token-signer",
                Usage = "JWT / SVID Digital Signature",
                Algorithm = "ECDSA-P256",
                NistStandard = "Legacy-FIPS-186",
                QuantumSafe = false,
                KeyEpoch = 1
            },
            new()
            {
                ComponentId = "orders-ledger-AtRest",
                Usage = "Envelope Encryption",
                Algorithm = "AES-256-GCM",
                NistStandard = "Grover-128bit-PQ",
                QuantumSafe = true,
                KeyEpoch = 1
            },
            new()
            {
                ComponentId = "partner-webhook-bridge",
                Usage = "TLS Handshake Auth",
                Algorithm = "RSA-2048",
                NistStandard = "Legacy-PKCS1",
                QuantumSafe = false,
                KeyEpoch = 1
            }
        };
    }

    public CbomAuditReport AuditCbom()
    {
        lock (_sync)
        {
            var snap = _entries.Select(e => new CryptoPrimitiveEntry
            {
                ComponentId = e.ComponentId,
                Usage = e.Usage,
                Algorithm = e.Algorithm,
                NistStandard = e.NistStandard,
                QuantumSafe = e.QuantumSafe,
                KeyEpoch = e.KeyEpoch
            }).ToList();

            int total = snap.Count;
            int vuln = snap.Count(e => !e.QuantumSafe);
            double score = total == 0 ? 100.0 : Math.Round(((double)(total - vuln) / total) * 100.0, 1);

            return new CbomAuditReport
            {
                BomFormat = "CycloneDX-CBOM-1.6",
                TotalPrimitives = total,
                ShorVulnerableCount = vuln,
                ReadinessScorePercent = score,
                Entries = snap
            };
        }
    }

    public PqcRotationResult RotateToHybridPqc()
    {
        lock (_sync)
        {
            _epoch += 1;
            int upgraded = 0;
            foreach (var entry in _entries)
            {
                entry.KeyEpoch = _epoch;
                if (!entry.QuantumSafe)
                {
                    upgraded += 1;
                    if (entry.Usage.Contains("Signature", StringComparison.OrdinalIgnoreCase))
                    {
                        entry.Algorithm = "ML-DSA-65";
                        entry.NistStandard = "FIPS-204";
                    }
                    else
                    {
                        entry.Algorithm = "X25519MLKEM768";
                        entry.NistStandard = "FIPS-203";
                    }
                    entry.QuantumSafe = true;
                }
            }

            bool allSafe = _entries.All(e => e.QuantumSafe);
            return new PqcRotationResult
            {
                NewEpoch = _epoch,
                UpgradedCount = upgraded,
                KemSuite = "X25519MLKEM768",
                SignatureSuite = "ML-DSA-65",
                AllQuantumSafe = allSafe
            };
        }
    }

    public PqcTlsConfigBundle GenerateEnvoyPqcConfig(string clusterName = "kite-mesh-upstream")
    {
        string safeCluster = string.IsNullOrWhiteSpace(clusterName) ? "kite-mesh-upstream" : clusterName.Trim();
        var audit = AuditCbom();

        string envoy = $@"transport_socket:
  name: envoy.transport_sockets.tls
  typed_config:
    ""@type"": type.googleapis.com/envoy.extensions.transport_sockets.tls.v3.UpstreamTlsContext
    sni: {safeCluster}.internal
    common_tls_context:
      tls_params:
        tls_minimum_protocol_version: TLSv1_3
        tls_maximum_protocol_version: TLSv1_3
        ecdh_curves:
          - X25519MLKEM768
          - X25519
        signature_algorithms:
          - mldsa65
          - ecdsa_secp256r1_sha256";

        string cbomJson = $@"{{
  ""bomFormat"": ""CycloneDX"",
  ""specVersion"": ""1.6"",
  ""cryptoProperties"": {{
    ""hybridKem"": ""X25519MLKEM768"",
    ""signature"": ""ML-DSA-65"",
    ""readinessPercent"": {audit.ReadinessScorePercent.ToString(System.Globalization.CultureInfo.InvariantCulture)}
  }}
}}";

        return new PqcTlsConfigBundle
        {
            ClusterName = safeCluster,
            HybridKemGroup = "X25519MLKEM768",
            SignatureAlgorithm = "ML-DSA-65",
            EnvoySdsYaml = envoy,
            CbomJson = cbomJson
        };
    }
}
