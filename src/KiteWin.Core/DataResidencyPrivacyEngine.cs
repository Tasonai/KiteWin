//
// DataResidencyPrivacyEngine.cs
// KiteWin.Core
//
// Sovereign Data Residency, PII Lineage & Differential Privacy Engine (Phase 31).
// Traces GDPR/PCI data-classification lineage across cloud jurisdictions, enforces
// Schrems II / GDPR Art. 44 cross-border transfer guardrails, and calibrates
// (epsilon, delta)-Differential Privacy noise scales alongside PostgreSQL RLS DDL.
//

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace KiteWin.Core;

public sealed class DataLineageFlow
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = "";

    [JsonPropertyName("datasetName")]
    public string DatasetName { get; init; } = "";

    [JsonPropertyName("classification")]
    public string Classification { get; init; } = "";

    [JsonPropertyName("sourceJurisdiction")]
    public string SourceJurisdiction { get; init; } = "";

    [JsonPropertyName("destinationRegion")]
    public string DestinationRegion { get; init; } = "";

    [JsonPropertyName("pseudonymized")]
    public bool Pseudonymized { get; init; }
}

public sealed class ResidencyComplianceReport
{
    [JsonPropertyName("inspectedFlows")]
    public int InspectedFlows { get; init; }

    [JsonPropertyName("violationsCount")]
    public int ViolationsCount { get; init; }

    [JsonPropertyName("gdprCompliant")]
    public bool GdprCompliant { get; init; }

    [JsonPropertyName("violations")]
    public List<string> Violations { get; init; } = new();
}

public sealed class DifferentialPrivacyBundle
{
    [JsonPropertyName("epsilon")]
    public double Epsilon { get; init; }

    [JsonPropertyName("delta")]
    public double Delta { get; init; }

    [JsonPropertyName("l2Sensitivity")]
    public double L2Sensitivity { get; init; }

    [JsonPropertyName("laplaceScaleB")]
    public double LaplaceScaleB { get; init; }

    [JsonPropertyName("gaussianSigma")]
    public double GaussianSigma { get; init; }

    [JsonPropertyName("postgresRlsMaskingSql")]
    public string PostgresRlsMaskingSql { get; init; } = "";
}

public sealed class DataResidencyPrivacyEngine
{
    public static DataResidencyPrivacyEngine Instance { get; } = new();

    private readonly object _lock = new();
    private readonly List<DataLineageFlow> _lineageFlows = new();

    public DataResidencyPrivacyEngine()
    {
        _lineageFlows.AddRange(new[]
        {
            new DataLineageFlow
            {
                Id = "flow-pii-eu-local",
                DatasetName = "customer_identity_vault",
                Classification = "GDPR_PII",
                SourceJurisdiction = "EU-EEA",
                DestinationRegion = "eu-central-1",
                Pseudonymized = false
            },
            new DataLineageFlow
            {
                Id = "flow-pci-ch-vault",
                DatasetName = "cardholder_pan_tokens",
                Classification = "PCI_CHD",
                SourceJurisdiction = "CH-SOVEREIGN",
                DestinationRegion = "ch-zurich-1",
                Pseudonymized = true
            },
            new DataLineageFlow
            {
                Id = "flow-pii-us-export",
                DatasetName = "marketing_clickstream_raw",
                Classification = "GDPR_PII",
                SourceJurisdiction = "EU-EEA",
                DestinationRegion = "us-east-1",
                Pseudonymized = false
            },
            new DataLineageFlow
            {
                Id = "flow-telem-global",
                DatasetName = "edge_pop_latency_histograms",
                Classification = "PUBLIC_TELEMETRY",
                SourceJurisdiction = "EU-EEA",
                DestinationRegion = "us-east-1",
                Pseudonymized = true
            }
        });
    }

    public List<DataLineageFlow> GetLineageFlows()
    {
        lock (_lock)
        {
            return _lineageFlows.ToList();
        }
    }

    public ResidencyComplianceReport AuditResidencyCompliance(bool enforceStrictSchremsII = true)
    {
        List<DataLineageFlow> flows;
        lock (_lock)
        {
            flows = _lineageFlows.ToList();
        }

        var violations = new List<string>();
        foreach (var flow in flows)
        {
            bool isRegulated = flow.Classification == "GDPR_PII" || flow.Classification == "PCI_CHD";
            if (!isRegulated)
                continue;

            bool isSovereignSource = flow.SourceJurisdiction == "EU-EEA" || flow.SourceJurisdiction == "CH-SOVEREIGN";
            bool isAdequateDest = flow.DestinationRegion.StartsWith("eu-", StringComparison.Ordinal)
                || flow.DestinationRegion.StartsWith("ch-", StringComparison.Ordinal);

            if (isSovereignSource && !isAdequateDest && !flow.Pseudonymized)
            {
                if (enforceStrictSchremsII || flow.Classification == "PCI_CHD")
                {
                    violations.Add(
                        $"GDPR Art. 44 / Schrems II violation on {flow.Id} ({flow.DatasetName}): unpseudonymized {flow.Classification} exported from {flow.SourceJurisdiction} to {flow.DestinationRegion}.");
                }
            }
        }

        return new ResidencyComplianceReport
        {
            InspectedFlows = flows.Count,
            ViolationsCount = violations.Count,
            GdprCompliant = violations.Count == 0,
            Violations = violations
        };
    }

    public DifferentialPrivacyBundle CalibrateDifferentialPrivacy(
        double epsilon = 0.5,
        double delta = 1e-5,
        double sensitivity = 1.0)
    {
        double eps = Math.Max(epsilon, 0.0001);
        double del = Math.Clamp(delta, 1e-12, 0.1);
        double sens = Math.Max(sensitivity, 0.0001);

        double rawLaplace = sens / eps;
        double rawGaussian = (sens * Math.Sqrt(2.0 * Math.Log(1.25 / del))) / eps;

        double laplaceB = Math.Round(rawLaplace, 4);
        double gaussianSig = Math.Round(rawGaussian, 4);

        string sqlDdl = $@"-- Sovereign Row-Level Security & Differential Privacy Rollup (epsilon={eps}, delta={del})
ALTER TABLE customer_identity_vault ENABLE ROW LEVEL SECURITY;
ALTER TABLE customer_identity_vault FORCE ROW LEVEL SECURITY;

CREATE POLICY sovereign_jurisdiction_isolation
    ON customer_identity_vault
    FOR ALL
    USING (source_jurisdiction = current_setting('app.sovereign_jurisdiction', true));

CREATE OR REPLACE VIEW analytics_dp_customer_rollup AS
SELECT
    source_jurisdiction,
    encode(digest(customer_email, 'sha256'), 'hex') AS pseudonymized_subject_key,
    COUNT(*) + dp_gaussian_noise(0.0, {gaussianSig}) AS dp_cohort_count
FROM customer_identity_vault
GROUP BY source_jurisdiction, encode(digest(customer_email, 'sha256'), 'hex');";

        return new DifferentialPrivacyBundle
        {
            Epsilon = eps,
            Delta = del,
            L2Sensitivity = sens,
            LaplaceScaleB = laplaceB,
            GaussianSigma = gaussianSig,
            PostgresRlsMaskingSql = sqlDdl
        };
    }
}
