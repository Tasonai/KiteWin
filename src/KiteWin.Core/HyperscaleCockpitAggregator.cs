using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace KiteWin.Core;

public sealed class HyperscaleCockpitSnapshot
{
    [JsonPropertyName("strataCount")]
    public int StrataCount { get; init; }

    [JsonPropertyName("spatialNodesCount")]
    public int SpatialNodesCount { get; init; }

    [JsonPropertyName("ebpfFlowsCount")]
    public int EbpfFlowsCount { get; init; }

    [JsonPropertyName("shadowApiCount")]
    public int ShadowApiCount { get; init; }

    [JsonPropertyName("healthyEdgePopsCount")]
    public int HealthyEdgePopsCount { get; init; }

    [JsonPropertyName("drRegionsCount")]
    public int DrRegionsCount { get; init; }

    [JsonPropertyName("monthlyArbitrageSavingsUsd")]
    public double MonthlyArbitrageSavingsUsd { get; init; }

    [JsonPropertyName("optimalGreenRegion")]
    public string OptimalGreenRegion { get; init; } = "";

    [JsonPropertyName("formalVerified")]
    public bool FormalVerified { get; init; }

    [JsonPropertyName("formalStatesExplored")]
    public int FormalStatesExplored { get; init; }

    [JsonPropertyName("pqcReadinessPercent")]
    public double PqcReadinessPercent { get; init; }

    [JsonPropertyName("frozenSloServicesCount")]
    public int FrozenSloServicesCount { get; init; }

    [JsonPropertyName("remediationPlaybooksCount")]
    public int RemediationPlaybooksCount { get; init; }

    [JsonPropertyName("spiffeTrustDomainsCount")]
    public int SpiffeTrustDomainsCount { get; init; }
}

public sealed class HyperscaleCockpitAggregator
{
    public static HyperscaleCockpitAggregator Instance { get; } = new();

    public HyperscaleCockpitSnapshot CaptureSnapshot()
    {
        var scene = Spatial3DTopologyEngine.Instance.GetScene();
        var flows = EbpfTelemetryEngine.Instance.GetKernelFlows();
        var shadow = EbpfTelemetryEngine.Instance.AuditShadowDependencies();
        var pops = WasmEdgeMeshEngine.Instance.GetGlobalPops();
        var regions = DisasterRecoveryEngine.Instance.GetRegions();
        var arb = FinOpsCarbonEngine.Instance.ComputeArbitrage(true);
        var green = FinOpsCarbonEngine.Instance.GenerateGreenSchedule(80);
        var formal = FormalVerificationEngine.Instance.RunBoundedModelCheck(8, false);
        var cbom = PostQuantumCryptoEngine.Instance.AuditCbom();
        var budgets = SloBurnRateEngine.Instance.GetBudgets();
        int frozen = budgets.Count(b => SloBurnRateEngine.Instance.EvaluateReleaseGate(b.ServiceId).Verdict == "FREEZE_RELEASE");
        var playbooks = AutoRemediationEngine.Instance.GetPlaybooks();
        var domains = FederationCatalogEngine.Instance.GetTrustDomains();

        return new HyperscaleCockpitSnapshot
        {
            StrataCount = scene.StrataCount,
            SpatialNodesCount = scene.Nodes.Count,
            EbpfFlowsCount = flows.Count,
            ShadowApiCount = shadow.UndocumentedEdgesCount + shadow.RogueEgressCount,
            HealthyEdgePopsCount = pops.Count(p => p.IsHealthy),
            DrRegionsCount = regions.Count,
            MonthlyArbitrageSavingsUsd = arb.TotalMonthlySavingsUsd,
            OptimalGreenRegion = green.OptimalLowCarbonRegion,
            FormalVerified = formal.Verified,
            FormalStatesExplored = formal.DistinctStatesExplored,
            PqcReadinessPercent = cbom.ReadinessScorePercent,
            FrozenSloServicesCount = frozen,
            RemediationPlaybooksCount = playbooks.Count,
            SpiffeTrustDomainsCount = domains.Count
        };
    }
}
