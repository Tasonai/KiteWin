using System.Linq;
using System.Text;
using Windows.ApplicationModel.DataTransfer;
using KiteWin.Core;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace KiteWin.Pages;

public sealed partial class HyperscalePage : Page
{
    public HyperscalePage()
    {
        InitializeComponent();
        Loaded += (_, _) => RefreshDashboard();
    }

    private void RefreshDashboard()
    {
        var snap = HyperscaleCockpitAggregator.Instance.CaptureSnapshot();

        // 1. Spatial 3D
        var scene = Spatial3DTopologyEngine.Instance.GetScene();
        SpatialSummaryText.Text = $"Страт: {scene.StrataCount} | 3D-узлов: {scene.Nodes.Count} | Связей: {scene.Edges.Count}";
        var sb3d = new StringBuilder();
        foreach (var n in scene.Nodes)
        {
            sb3d.AppendLine($"[{n.Stratum,-18}] {n.Label,-26} Pos=({n.X:0.0}, {n.Y:0.0}, {n.Z:0.0})");
        }
        SpatialNodesListText.Text = sb3d.ToString().TrimEnd();

        // 2. eBPF & WASI Edge
        var flows = EbpfTelemetryEngine.Instance.GetKernelFlows();
        var pops = WasmEdgeMeshEngine.Instance.GetGlobalPops();
        EbpfEdgeSummaryText.Text = $"Потоков L4/L7: {flows.Count} | Теневых API: {snap.ShadowApiCount} | Активных узлов WASI PoP: {snap.HealthyEdgePopsCount}/{pops.Count}";
        var sbEbpf = new StringBuilder();
        foreach (var f in flows)
        {
            sbEbpf.AppendLine($"{f.SourceWorkload} -> {f.DestinationWorkload} [{f.WireProtocol}] RTT={f.TcpRttMs:0.00}ms");
        }
        sbEbpf.AppendLine($"PoP-локации: {string.Join(", ", pops.Select(p => $"{p.IataCode} ({p.ColdStartMs:0.00}ms)"))}");
        EbpfFlowsListText.Text = sbEbpf.ToString().TrimEnd();

        // 3. DR & FinOps/Carbon
        var regions = DisasterRecoveryEngine.Instance.GetRegions();
        var arb = FinOpsCarbonEngine.Instance.ComputeArbitrage(true);
        DrFinopsSummaryText.Text = $"Регионов Active-Active: {regions.Count} | Экономия ARM64/Spot: ${arb.TotalMonthlySavingsUsd:N0}/мес ({arb.SavingsPercent:0.0}%) | Зеленый регион: {snap.OptimalGreenRegion}";
        var sbDr = new StringBuilder();
        foreach (var r in regions)
        {
            sbDr.AppendLine($"Регион {r.RegionId,-14} [{r.Role,-9}] Lag={r.ReplicationLagMs:0.0}ms | QuorumVoters={r.VotingVoters}");
        }
        DrFinopsDetailText.Text = sbDr.ToString().TrimEnd();

        // 4. Formal TLA+, PQC, SLO & Federation
        var budgets = SloBurnRateEngine.Instance.GetBudgets();
        GovernanceSummaryText.Text = $"TLA+ состояний: {snap.FormalStatesExplored} ({(snap.FormalVerified ? "VERIFIED" : "VIOLATION")}) | PQ-готовность: {snap.PqcReadinessPercent:0.0}% | SLO Freeze: {snap.FrozenSloServicesCount} | SPIFFE доменов: {snap.SpiffeTrustDomainsCount}";
        var sbGov = new StringBuilder();
        foreach (var b in budgets)
        {
            var gate = SloBurnRateEngine.Instance.EvaluateReleaseGate(b.ServiceId);
            sbGov.AppendLine($"SLO {b.ServiceId,-16} Budget={b.RemainingBudgetPercent:0.0}% | 1h={b.BurnRate1h:0.0}x -> {gate.Verdict}");
        }
        GovernanceDetailText.Text = sbGov.ToString().TrimEnd();
    }

    private void OnRefreshAllClicked(object sender, RoutedEventArgs e)
    {
        RefreshDashboard();
        ActionStatusBar.Severity = InfoBarSeverity.Success;
        ActionStatusBar.Message = "Метрики всех 10 движков гиперскейла и формальной верификации обновлены.";
    }

    private void OnRelax3DLayoutClicked(object sender, RoutedEventArgs e)
    {
        Spatial3DTopologyEngine.Instance.ComputeForceDirectedLayout(12);
        RefreshDashboard();
        ActionStatusBar.Severity = InfoBarSeverity.Success;
        ActionStatusBar.Message = "Выполнена 3D релаксация пружинно-отталкивающей раскладки по 5 Z-стратам.";
    }

    private void OnCopyGltfClicked(object sender, RoutedEventArgs e)
    {
        var gltf = Spatial3DTopologyEngine.Instance.ExportGltfScene();
        var pkg = new DataPackage();
        pkg.SetText(gltf.GltfJson);
        Clipboard.SetContent(pkg);
        ActionStatusBar.Severity = InfoBarSeverity.Success;
        ActionStatusBar.Message = $"Сцена {gltf.FormatVersion} ({gltf.NodeCount} узлов) скопирована в буфер обмена.";
    }

    private void OnCopyEbpfPolicyClicked(object sender, RoutedEventArgs e)
    {
        var pol = EbpfTelemetryEngine.Instance.GenerateEbpfPolicy("kite-prod", true);
        var pkg = new DataPackage();
        pkg.SetText(pol.CiliumNetworkPolicyYaml);
        Clipboard.SetContent(pkg);
        ActionStatusBar.Severity = InfoBarSeverity.Success;
        ActionStatusBar.Message = "Манифест CiliumNetworkPolicy (Zero-Sidecar eBPF) скопирован в буфер обмена.";
    }

    private void OnSimulateFailoverClicked(object sender, RoutedEventArgs e)
    {
        var sim = DisasterRecoveryEngine.Instance.SimulateRegionalBlackout("eu-central-1", 1.0, 30.0);
        ActionStatusBar.Severity = sim.SlaCompliant ? InfoBarSeverity.Success : InfoBarSeverity.Warning;
        ActionStatusBar.Message = $"DR Failover: новый лидер {sim.PromotedPrimaryRegionId} (кворум={sim.SurvivorQuorumVotes}, RPO={sim.AchievedRpoSeconds:0.00}s, RTO={sim.AchievedRtoSeconds:0.0}s).";
    }

    private void OnRotatePqcClicked(object sender, RoutedEventArgs e)
    {
        var rot = PostQuantumCryptoEngine.Instance.RotateToHybridPqc();
        RefreshDashboard();
        ActionStatusBar.Severity = InfoBarSeverity.Success;
        ActionStatusBar.Message = $"Эпоха {rot.NewEpoch}: гибридные алгоритмы {rot.KemSuite} и {rot.SignatureSuite} активированы (100% PQ-Safe).";
    }

    private void OnRunMapeKClicked(object sender, RoutedEventArgs e)
    {
        var exec = AutoRemediationEngine.Instance.ExecutePlaybook("ErrorBudgetFastBurn", "ledger-writer", 1, false);
        ActionStatusBar.Severity = exec.Executed ? InfoBarSeverity.Success : InfoBarSeverity.Warning;
        ActionStatusBar.Message = exec.ActionSummary;
    }
}
