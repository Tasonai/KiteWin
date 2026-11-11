using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json.Serialization;

namespace KiteWin.Core;

public sealed class SloServiceBudget
{
    [JsonPropertyName("serviceId")]
    public string ServiceId { get; init; } = "";

    [JsonPropertyName("sloTargetPercent")]
    public double SloTargetPercent { get; init; }

    [JsonPropertyName("burnRate1h")]
    public double BurnRate1h { get; init; }

    [JsonPropertyName("burnRate6h")]
    public double BurnRate6h { get; init; }

    [JsonPropertyName("remainingBudgetPercent")]
    public double RemainingBudgetPercent { get; init; }

    [JsonPropertyName("remainingMinutes30d")]
    public double RemainingMinutes30d { get; init; }
}

public sealed class ReleaseGateVerdict
{
    [JsonPropertyName("serviceId")]
    public string ServiceId { get; init; } = "";

    [JsonPropertyName("verdict")]
    public string Verdict { get; init; } = "";

    [JsonPropertyName("fastBurnTriggered")]
    public bool FastBurnTriggered { get; init; }

    [JsonPropertyName("mediumBurnTriggered")]
    public bool MediumBurnTriggered { get; init; }

    [JsonPropertyName("reason")]
    public string Reason { get; init; } = "";
}

public sealed class OpenSloBundle
{
    [JsonPropertyName("serviceId")]
    public string ServiceId { get; init; } = "";

    [JsonPropertyName("sloTargetPercent")]
    public double SloTargetPercent { get; init; }

    [JsonPropertyName("openSloYaml")]
    public string OpenSloYaml { get; init; } = "";

    [JsonPropertyName("prometheusRuleYaml")]
    public string PrometheusRuleYaml { get; init; } = "";
}

public sealed class SloBurnRateEngine
{
    public static SloBurnRateEngine Instance { get; } = new();

    private readonly object _sync = new();
    private readonly List<SloServiceBudget> _budgets;

    public SloBurnRateEngine()
    {
        _budgets = new List<SloServiceBudget>
        {
            new()
            {
                ServiceId = "checkout-api",
                SloTargetPercent = 99.95,
                BurnRate1h = 1.2,
                BurnRate6h = 0.9,
                RemainingBudgetPercent = 84.5,
                RemainingMinutes30d = 18.25
            },
            new()
            {
                ServiceId = "ledger-writer",
                SloTargetPercent = 99.99,
                BurnRate1h = 16.8,
                BurnRate6h = 7.4,
                RemainingBudgetPercent = 12.4,
                RemainingMinutes30d = 0.54
            },
            new()
            {
                ServiceId = "search-indexer",
                SloTargetPercent = 99.90,
                BurnRate1h = 4.5,
                BurnRate6h = 6.2,
                RemainingBudgetPercent = 41.0,
                RemainingMinutes30d = 17.71
            }
        };
    }

    public List<SloServiceBudget> GetBudgets()
    {
        lock (_sync)
        {
            return _budgets.ToList();
        }
    }

    public ReleaseGateVerdict EvaluateReleaseGate(string serviceId = "checkout-api")
    {
        SloServiceBudget target;
        lock (_sync)
        {
            target = _budgets.FirstOrDefault(b => string.Equals(b.ServiceId, serviceId, StringComparison.OrdinalIgnoreCase))
                     ?? _budgets[0];
        }

        bool fastBurn = target.BurnRate1h >= 14.4;
        bool medBurn = target.BurnRate6h >= 6.0;

        if (fastBurn || target.RemainingBudgetPercent < 15.0)
        {
            return new ReleaseGateVerdict
            {
                ServiceId = target.ServiceId,
                Verdict = "FREEZE_RELEASE",
                FastBurnTriggered = fastBurn,
                MediumBurnTriggered = medBurn,
                Reason = $"Critical 1h burn rate ({target.BurnRate1h.ToString("0.0", CultureInfo.InvariantCulture)}x >= 14.4x) or depleted error budget ({target.RemainingBudgetPercent.ToString("0.0", CultureInfo.InvariantCulture)}%)."
            };
        }

        if (medBurn || target.RemainingBudgetPercent < 50.0)
        {
            return new ReleaseGateVerdict
            {
                ServiceId = target.ServiceId,
                Verdict = "CANARY_ONLY",
                FastBurnTriggered = false,
                MediumBurnTriggered = medBurn,
                Reason = $"Elevated 6h burn rate ({target.BurnRate6h.ToString("0.0", CultureInfo.InvariantCulture)}x >= 6.0x); progressive 5% canary rollout required."
            };
        }

        return new ReleaseGateVerdict
        {
            ServiceId = target.ServiceId,
            Verdict = "ALLOW_DEPLOY",
            FastBurnTriggered = false,
            MediumBurnTriggered = false,
            Reason = $"Healthy error budget ({target.RemainingBudgetPercent.ToString("0.0", CultureInfo.InvariantCulture)}% remaining); multi-window burn within SRE bounds."
        };
    }

    public OpenSloBundle GenerateOpenSloManifest(string serviceId = "checkout-api", double sloTargetPercent = 99.95)
    {
        string safeSvc = string.IsNullOrWhiteSpace(serviceId) ? "checkout-api" : serviceId.Trim();
        double clampedTarget = Math.Clamp(sloTargetPercent, 95.0, 99.999);
        double errorFraction = Math.Round((100.0 - clampedTarget) / 100.0, 6);
        string targetStr = clampedTarget.ToString("0.00", CultureInfo.InvariantCulture);
        string errStr = errorFraction.ToString("0.00000", CultureInfo.InvariantCulture);

        string openSlo = $@"apiVersion: openslo/v1
kind: SLO
metadata:
  name: {safeSvc}-availability-slo
spec:
  service: {safeSvc}
  budgetingMethod: Occurrences
  timeWindow:
    - duration: 30d
      isRolling: true
  objectives:
    - displayName: Availability {targetStr}%
      target: {(clampedTarget / 100.0).ToString("0.00000", CultureInfo.InvariantCulture)}";

        string promRule = $@"apiVersion: monitoring.coreos.com/v1
kind: PrometheusRule
metadata:
  name: {safeSvc}-multiwindow-burn-alerts
spec:
  groups:
    - name: {safeSvc}.slo.burn
      rules:
        - alert: SloFastBurnPage
          expr: |
            (job:http_errors:ratio_rate1h{{service=""{safeSvc}""}} > (14.4 * {errStr}))
            and
            (job:http_errors:ratio_rate5m{{service=""{safeSvc}""}} > (14.4 * {errStr}))
          for: 2m
          labels:
            severity: critical";

        return new OpenSloBundle
        {
            ServiceId = safeSvc,
            SloTargetPercent = clampedTarget,
            OpenSloYaml = openSlo,
            PrometheusRuleYaml = promRule
        };
    }
}
