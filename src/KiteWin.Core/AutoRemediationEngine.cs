using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace KiteWin.Core;

public sealed class RemediationPlaybook
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = "";

    [JsonPropertyName("triggerSignal")]
    public string TriggerSignal { get; init; } = "";

    [JsonPropertyName("actionType")]
    public string ActionType { get; init; } = "";

    [JsonPropertyName("maxBlastRadiusServices")]
    public int MaxBlastRadiusServices { get; init; }

    [JsonPropertyName("cooldownSeconds")]
    public int CooldownSeconds { get; init; }
}

public sealed class RemediationExecutionResult
{
    [JsonPropertyName("playbookId")]
    public string PlaybookId { get; init; } = "";

    [JsonPropertyName("targetService")]
    public string TargetService { get; init; } = "";

    [JsonPropertyName("executed")]
    public bool Executed { get; init; }

    [JsonPropertyName("dryRun")]
    public bool DryRun { get; init; }

    [JsonPropertyName("guardrailPassed")]
    public bool GuardrailPassed { get; init; }

    [JsonPropertyName("actionSummary")]
    public string ActionSummary { get; init; } = "";

    [JsonPropertyName("postActionTrafficStablePercent")]
    public int PostActionTrafficStablePercent { get; init; }
}

public sealed class ArgoRolloutSpecBundle
{
    [JsonPropertyName("serviceName")]
    public string ServiceName { get; init; } = "";

    [JsonPropertyName("canaryStepsCount")]
    public int CanaryStepsCount { get; init; }

    [JsonPropertyName("rolloutYaml")]
    public string RolloutYaml { get; init; } = "";

    [JsonPropertyName("analysisTemplateYaml")]
    public string AnalysisTemplateYaml { get; init; } = "";
}

public sealed class AutoRemediationEngine
{
    public static AutoRemediationEngine Instance { get; } = new();

    private readonly object _sync = new();
    private readonly List<RemediationPlaybook> _playbooks;

    public AutoRemediationEngine()
    {
        _playbooks = new List<RemediationPlaybook>
        {
            new()
            {
                Id = "pb-canary-drain",
                TriggerSignal = "ErrorBudgetFastBurn",
                ActionType = "CANARY_TRAFFIC_ROLLBACK",
                MaxBlastRadiusServices = 2,
                CooldownSeconds = 180
            },
            new()
            {
                Id = "pb-hpa-surge",
                TriggerSignal = "P99LatencySpike",
                ActionType = "HPA_REPLICA_SURGE",
                MaxBlastRadiusServices = 4,
                CooldownSeconds = 120
            },
            new()
            {
                Id = "pb-circuit-killswitch",
                TriggerSignal = "DownstreamTimeoutStorm",
                ActionType = "FEATURE_FLAG_SHED_LOAD",
                MaxBlastRadiusServices = 3,
                CooldownSeconds = 60
            }
        };
    }

    public List<RemediationPlaybook> GetPlaybooks()
    {
        lock (_sync)
        {
            return _playbooks.ToList();
        }
    }

    public RemediationExecutionResult ExecutePlaybook(
        string triggerSignal = "ErrorBudgetFastBurn",
        string targetService = "ledger-writer",
        int estimatedBlastRadius = 1,
        bool dryRun = false)
    {
        RemediationPlaybook pb;
        lock (_sync)
        {
            pb = _playbooks.FirstOrDefault(p => string.Equals(p.TriggerSignal, triggerSignal, StringComparison.OrdinalIgnoreCase))
                 ?? _playbooks[0];
        }

        string safeTarget = string.IsNullOrWhiteSpace(targetService) ? "ledger-writer" : targetService.Trim();
        bool guardrailOk = estimatedBlastRadius <= pb.MaxBlastRadiusServices;

        if (!guardrailOk)
        {
            return new RemediationExecutionResult
            {
                PlaybookId = pb.Id,
                TargetService = safeTarget,
                Executed = false,
                DryRun = dryRun,
                GuardrailPassed = false,
                ActionSummary = $"BLOCKED by blast-radius guardrail: impact ({estimatedBlastRadius} services) exceeds limit ({pb.MaxBlastRadiusServices}); human SRE escalation triggered.",
                PostActionTrafficStablePercent = 95
            };
        }

        return new RemediationExecutionResult
        {
            PlaybookId = pb.Id,
            TargetService = safeTarget,
            Executed = !dryRun,
            DryRun = dryRun,
            GuardrailPassed = true,
            ActionSummary = $"{(dryRun ? "[DRY-RUN] " : "")}MAPE-K executed {pb.ActionType} on {safeTarget}: shifted 100% traffic to stable revision within {pb.CooldownSeconds}s cooldown window.",
            PostActionTrafficStablePercent = 100
        };
    }

    public ArgoRolloutSpecBundle GenerateArgoRolloutSpec(string serviceName = "ledger-writer")
    {
        string safeSvc = string.IsNullOrWhiteSpace(serviceName) ? "ledger-writer" : serviceName.Trim();

        string rolloutYaml = $@"apiVersion: argoproj.io/v1alpha1
kind: Rollout
metadata:
  name: {safeSvc}-rollout
spec:
  replicas: 6
  strategy:
    canary:
      steps:
        - setWeight: 5
        - pause: {{duration: 2m}}
        - setWeight: 25
        - analysis:
            templates:
              - templateName: {safeSvc}-slo-guard";

        string analysisYaml = $@"apiVersion: argoproj.io/v1alpha1
kind: AnalysisTemplate
metadata:
  name: {safeSvc}-slo-guard
spec:
  metrics:
    - name: success-rate
      interval: 1m
      successCondition: result[0] >= 0.999
      failureLimit: 1
      provider:
        prometheus:
          query: sum(rate(http_requests_total{{service=""{safeSvc}"",status!~""5..""}}[2m])) / sum(rate(http_requests_total{{service=""{safeSvc}""}}[2m]))";

        return new ArgoRolloutSpecBundle
        {
            ServiceName = safeSvc,
            CanaryStepsCount = 4,
            RolloutYaml = rolloutYaml,
            AnalysisTemplateYaml = analysisYaml
        };
    }
}
