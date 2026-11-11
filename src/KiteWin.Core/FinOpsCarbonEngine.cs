//
// FinOpsCarbonEngine.cs
// KiteWin.Core
//
// FinOps Cloud Cost Arbitrage & Carbon-Aware Green Scheduler Engine.
// Analyzes multi-cloud unit economics across AWS, GCP, Azure, and bare-metal providers,
// synthesizes ARM64 Graviton/Axion & Spot savings plans, profiles regional grid carbon intensity
// (gCO2eq/kWh), and generates Karpenter NodePool + KEDA carbon-aware batch/AI scalers.
//

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace KiteWin.Core
{
    public class CloudWorkloadSpend
    {
        [JsonPropertyName("workloadId")]
        public string WorkloadId { get; set; } = string.Empty;

        [JsonPropertyName("provider")]
        public string Provider { get; set; } = string.Empty;

        [JsonPropertyName("region")]
        public string Region { get; set; } = string.Empty;

        [JsonPropertyName("instanceFamily")]
        public string InstanceFamily { get; set; } = string.Empty;

        [JsonPropertyName("architecture")]
        public string Architecture { get; set; } = "x86_64";

        [JsonPropertyName("monthlyCostUsd")]
        public double MonthlyCostUsd { get; set; }

        [JsonPropertyName("cpuUtilizationPercent")]
        public double CpuUtilizationPercent { get; set; }

        [JsonPropertyName("gridCarbonGco2PerKwh")]
        public int GridCarbonGco2PerKwh { get; set; }
    }

    public class ArbitrageRecommendation
    {
        [JsonPropertyName("workloadId")]
        public string WorkloadId { get; set; } = string.Empty;

        [JsonPropertyName("currentMonthlyUsd")]
        public double CurrentMonthlyUsd { get; set; }

        [JsonPropertyName("projectedMonthlyUsd")]
        public double ProjectedMonthlyUsd { get; set; }

        [JsonPropertyName("monthlySavingsUsd")]
        public double MonthlySavingsUsd { get; set; }

        [JsonPropertyName("targetArchitecture")]
        public string TargetArchitecture { get; set; } = "arm64-graviton4";

        [JsonPropertyName("strategy")]
        public string Strategy { get; set; } = string.Empty;
    }

    public class FinOpsArbitrageReport
    {
        [JsonPropertyName("totalCurrentMonthlyUsd")]
        public double TotalCurrentMonthlyUsd { get; set; }

        [JsonPropertyName("totalProjectedMonthlyUsd")]
        public double TotalProjectedMonthlyUsd { get; set; }

        [JsonPropertyName("totalMonthlySavingsUsd")]
        public double TotalMonthlySavingsUsd { get; set; }

        [JsonPropertyName("savingsPercent")]
        public double SavingsPercent { get; set; }

        [JsonPropertyName("recommendations")]
        public List<ArbitrageRecommendation> Recommendations { get; set; } = new();
    }

    public class GreenScheduleBundle
    {
        [JsonPropertyName("optimalLowCarbonRegion")]
        public string OptimalLowCarbonRegion { get; set; } = string.Empty;

        [JsonPropertyName("optimalCarbonGco2PerKwh")]
        public int OptimalCarbonGco2PerKwh { get; set; }

        [JsonPropertyName("carbonReductionPercent")]
        public double CarbonReductionPercent { get; set; }

        [JsonPropertyName("karpenterNodePoolYaml")]
        public string KarpenterNodePoolYaml { get; set; } = string.Empty;

        [JsonPropertyName("kedaCarbonScalerYaml")]
        public string KedaCarbonScalerYaml { get; set; } = string.Empty;
    }

    public class FinOpsCarbonEngine
    {
        private static readonly Lazy<FinOpsCarbonEngine> _instance = new(() => new FinOpsCarbonEngine());
        public static FinOpsCarbonEngine Instance => _instance.Value;

        private readonly object _lock = new();
        private readonly List<CloudWorkloadSpend> _workloads = new();

        public FinOpsCarbonEngine()
        {
            SeedWorkloads();
        }

        private void SeedWorkloads()
        {
            _workloads.Add(new CloudWorkloadSpend
            {
                WorkloadId = "api-core-fleet",
                Provider = "aws",
                Region = "eu-central-1",
                InstanceFamily = "m6i.4xlarge",
                Architecture = "x86_64",
                MonthlyCostUsd = 4800.0,
                CpuUtilizationPercent = 28.5,
                GridCarbonGco2PerKwh = 315
            });
            _workloads.Add(new CloudWorkloadSpend
            {
                WorkloadId = "ml-embedding-batch",
                Provider = "aws",
                Region = "us-east-1",
                InstanceFamily = "g5.2xlarge",
                Architecture = "x86_64",
                MonthlyCostUsd = 6200.0,
                CpuUtilizationPercent = 41.0,
                GridCarbonGco2PerKwh = 365
            });
            _workloads.Add(new CloudWorkloadSpend
            {
                WorkloadId = "analytics-warehouse-workers",
                Provider = "gcp",
                Region = "eu-north-1",
                InstanceFamily = "t2a-standard-8",
                Architecture = "arm64",
                MonthlyCostUsd = 1900.0,
                CpuUtilizationPercent = 72.0,
                GridCarbonGco2PerKwh = 28
            });
        }

        public List<CloudWorkloadSpend> GetWorkloads()
        {
            lock (_lock)
            {
                return new List<CloudWorkloadSpend>(_workloads);
            }
        }

        public FinOpsArbitrageReport ComputeArbitrage(bool allowSpotForStateless = true)
        {
            List<CloudWorkloadSpend> items;
            lock (_lock)
            {
                items = new List<CloudWorkloadSpend>(_workloads);
            }

            var recs = new List<ArbitrageRecommendation>();
            double totalCur = 0.0;
            double totalProj = 0.0;

            foreach (var w in items)
            {
                totalCur += w.MonthlyCostUsd;
                double discountFactor = 1.0;
                var reasons = new List<string>();

                if (w.Architecture == "x86_64")
                {
                    discountFactor *= 0.72; // 28% Graviton4/Axion ARM64 price-performance improvement
                    reasons.Add("Migrate x86_64 to ARM64 Graviton4/Axion (-28%)");
                }
                if (w.CpuUtilizationPercent < 40.0)
                {
                    discountFactor *= 0.75; // Right-size overprovisioned vCPU allocation
                    reasons.Add("Right-size underutilized vCPUs (<40% avg)");
                }
                if (allowSpotForStateless && w.WorkloadId.Contains("batch"))
                {
                    discountFactor *= 0.55; // Spot/Preemptible capacity pool arbitrage
                    reasons.Add("Shift fault-tolerant batch pool to Spot capacity (-45%)");
                }

                double projected = Math.Round(w.MonthlyCostUsd * discountFactor, 2);
                double saved = Math.Round(w.MonthlyCostUsd - projected, 2);
                totalProj += projected;

                if (saved > 0.0)
                {
                    recs.Add(new ArbitrageRecommendation
                    {
                        WorkloadId = w.WorkloadId,
                        CurrentMonthlyUsd = w.MonthlyCostUsd,
                        ProjectedMonthlyUsd = projected,
                        MonthlySavingsUsd = saved,
                        TargetArchitecture = "arm64",
                        Strategy = string.Join("; ", reasons)
                    });
                }
            }

            double totalSaved = Math.Round(totalCur - totalProj, 2);
            double pct = totalCur > 0 ? Math.Round((totalSaved / totalCur) * 100.0, 1) : 0.0;

            return new FinOpsArbitrageReport
            {
                TotalCurrentMonthlyUsd = Math.Round(totalCur, 2),
                TotalProjectedMonthlyUsd = Math.Round(totalProj, 2),
                TotalMonthlySavingsUsd = totalSaved,
                SavingsPercent = pct,
                Recommendations = recs
            };
        }

        public GreenScheduleBundle GenerateGreenSchedule(int maxCarbonThresholdGco2 = 80)
        {
            List<CloudWorkloadSpend> items;
            lock (_lock)
            {
                items = new List<CloudWorkloadSpend>(_workloads);
            }

            var greenest = items.OrderBy(w => w.GridCarbonGco2PerKwh).FirstOrDefault()
                           ?? new CloudWorkloadSpend { Region = "eu-north-1", GridCarbonGco2PerKwh = 28 };
            double avgCarbon = items.Count > 0 ? items.Average(w => w.GridCarbonGco2PerKwh) : 300.0;
            double reduction = avgCarbon > 0 ? Math.Round(((avgCarbon - greenest.GridCarbonGco2PerKwh) / avgCarbon) * 100.0, 1) : 0.0;

            var karpenterYaml = $@"apiVersion: karpenter.sh/v1
kind: NodePool
metadata:
  name: kite-arm64-finops-pool
spec:
  template:
    spec:
      requirements:
        - key: kubernetes.io/arch
          operator: In
          values: [""arm64""]
        - key: karpenter.sh/capacity-type
          operator: In
          values: [""spot"", ""on-demand""]
  disruption:
    consolidationPolicy: WhenEmptyOrUnderutilized
    consolidateAfter: 30s";

            var kedaYaml = $@"apiVersion: keda.sh/v1alpha1
kind: ScaledObject
metadata:
  name: kite-carbon-aware-batch-scaler
spec:
  scaleTargetRef:
    name: ml-embedding-batch
  minReplicaCount: 0
  maxReplicaCount: 32
  triggers:
    - type: carbon-aware-grid
      metadata:
        targetRegion: ""{greenest.Region}""
        maxCarbonIntensityGco2Kwh: ""{maxCarbonThresholdGco2}""";

            return new GreenScheduleBundle
            {
                OptimalLowCarbonRegion = greenest.Region,
                OptimalCarbonGco2PerKwh = greenest.GridCarbonGco2PerKwh,
                CarbonReductionPercent = reduction,
                KarpenterNodePoolYaml = karpenterYaml,
                KedaCarbonScalerYaml = kedaYaml
            };
        }
    }
}
