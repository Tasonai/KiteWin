//
// PolicyAsCodeEngine.cs
// KiteWin.Core
//
// Policy-as-Code Authorization Engine (Phase 28).
// Evaluates ABAC/ReBAC fine-grained access decisions with explicit forbid-overrides-permit
// semantics across canvas service boundaries, and synthesizes Open Policy Agent (OPA Rego v1)
// modules alongside AWS Cedar schema-backed policy sets.
//

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace KiteWin.Core;

public sealed class AuthorizationPolicyRule
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = "";

    [JsonPropertyName("effect")]
    public string Effect { get; init; } = "permit";

    [JsonPropertyName("principalRole")]
    public string PrincipalRole { get; init; } = "";

    [JsonPropertyName("action")]
    public string Action { get; init; } = "";

    [JsonPropertyName("resourceType")]
    public string ResourceType { get; init; } = "";

    [JsonPropertyName("conditionSummary")]
    public string ConditionSummary { get; init; } = "";
}

public sealed class PolicyDecisionVerdict
{
    [JsonPropertyName("allowed")]
    public bool Allowed { get; init; }

    [JsonPropertyName("matchedRuleId")]
    public string MatchedRuleId { get; init; } = "";

    [JsonPropertyName("denyOverridesTriggered")]
    public bool DenyOverridesTriggered { get; init; }

    [JsonPropertyName("reason")]
    public string Reason { get; init; } = "";
}

public sealed class PolicyAsCodeBundle
{
    [JsonPropertyName("namespace")]
    public string Namespace { get; init; } = "";

    [JsonPropertyName("opaRegoModule")]
    public string OpaRegoModule { get; init; } = "";

    [JsonPropertyName("cedarPolicySet")]
    public string CedarPolicySet { get; init; } = "";
}

public sealed class PolicyAsCodeEngine
{
    public static PolicyAsCodeEngine Instance { get; } = new();

    private readonly object _lock = new();
    private readonly List<AuthorizationPolicyRule> _rules = new();

    public PolicyAsCodeEngine()
    {
        _rules.AddRange(new[]
        {
            new AuthorizationPolicyRule
            {
                Id = "pol-forbid-unverified-mfa",
                Effect = "forbid",
                PrincipalRole = "*",
                Action = "ledger:ApproveSettlement",
                ResourceType = "SettlementBatch",
                ConditionSummary = "context.mfaVerified == false"
            },
            new AuthorizationPolicyRule
            {
                Id = "pol-permit-treasury-settle",
                Effect = "permit",
                PrincipalRole = "treasury-operator",
                Action = "ledger:ApproveSettlement",
                ResourceType = "SettlementBatch",
                ConditionSummary = "principal.role == treasury-operator && context.mfaVerified == true"
            },
            new AuthorizationPolicyRule
            {
                Id = "pol-permit-sre-rollout",
                Effect = "permit",
                PrincipalRole = "sre-oncall",
                Action = "mesh:DrainTraffic",
                ResourceType = "CanaryService",
                ConditionSummary = "principal.role == sre-oncall"
            },
            new AuthorizationPolicyRule
            {
                Id = "pol-forbid-auditor-mutate",
                Effect = "forbid",
                PrincipalRole = "compliance-auditor",
                Action = "ledger:ApproveSettlement",
                ResourceType = "SettlementBatch",
                ConditionSummary = "read-only auditor separation of duties"
            }
        });
    }

    public List<AuthorizationPolicyRule> GetRules()
    {
        lock (_lock)
        {
            return _rules.ToList();
        }
    }

    public PolicyDecisionVerdict EvaluateAccess(
        string principalRole = "treasury-operator",
        string action = "ledger:ApproveSettlement",
        string resourceType = "SettlementBatch",
        bool mfaVerified = true)
    {
        List<AuthorizationPolicyRule> snapshot;
        lock (_lock)
        {
            snapshot = _rules.ToList();
        }

        if (action == "ledger:ApproveSettlement" && !mfaVerified)
        {
            return new PolicyDecisionVerdict
            {
                Allowed = false,
                MatchedRuleId = "pol-forbid-unverified-mfa",
                DenyOverridesTriggered = true,
                Reason = "Explicit FORBID: ledger:ApproveSettlement requires hardware WebAuthn/MFA verification."
            };
        }

        var explicitForbid = snapshot.FirstOrDefault(r =>
            r.Effect == "forbid"
            && r.PrincipalRole == principalRole
            && r.Action == action
            && r.ResourceType == resourceType);

        if (explicitForbid != null)
        {
            return new PolicyDecisionVerdict
            {
                Allowed = false,
                MatchedRuleId = explicitForbid.Id,
                DenyOverridesTriggered = true,
                Reason = $"Explicit FORBID matched rule {explicitForbid.Id} ({explicitForbid.ConditionSummary})."
            };
        }

        var permitRule = snapshot.FirstOrDefault(r =>
            r.Effect == "permit"
            && (r.PrincipalRole == principalRole || r.PrincipalRole == "*")
            && r.Action == action
            && r.ResourceType == resourceType);

        if (permitRule != null)
        {
            return new PolicyDecisionVerdict
            {
                Allowed = true,
                MatchedRuleId = permitRule.Id,
                DenyOverridesTriggered = false,
                Reason = $"PERMIT granted via rule {permitRule.Id}."
            };
        }

        return new PolicyDecisionVerdict
        {
            Allowed = false,
            MatchedRuleId = "default-zero-trust-deny",
            DenyOverridesTriggered = false,
            Reason = $"Default zero-trust DENY: no matching permit policy for {principalRole} on {action}."
        };
    }

    public PolicyAsCodeBundle SynthesizePolicyBundle(string ns = "kite.authz.v1")
    {
        string pkg = ns.Replace("-", "_");

        string rego = $@"package {pkg}
import rego.v1

default allow := false

explicit_deny if {{
    input.action == ""ledger:ApproveSettlement""
    not input.context.mfa_verified
}}

explicit_deny if {{
    input.principal.role == ""compliance-auditor""
    startswith(input.action, ""ledger:Approve"")
}}

allow if {{
    not explicit_deny
    input.principal.role == ""treasury-operator""
    input.action == ""ledger:ApproveSettlement""
    input.resource.type == ""SettlementBatch""
}}

allow if {{
    not explicit_deny
    input.principal.role == ""sre-oncall""
    input.action == ""mesh:DrainTraffic""
    input.resource.type == ""CanaryService""
}}";

        string cedar = @"@id(""pol-forbid-unverified-mfa"")
forbid (
    principal,
    action == Kite::Action::""ledger:ApproveSettlement"",
    resource is Kite::SettlementBatch
)
unless {
    context.mfaVerified == true
};

@id(""pol-permit-treasury-settle"")
permit (
    principal in Kite::Role::""treasury-operator"",
    action == Kite::Action::""ledger:ApproveSettlement"",
    resource is Kite::SettlementBatch
)
when {
    context.mfaVerified == true
};

@id(""pol-permit-sre-rollout"")
permit (
    principal in Kite::Role::""sre-oncall"",
    action == Kite::Action::""mesh:DrainTraffic"",
    resource is Kite::CanaryService
);";

        return new PolicyAsCodeBundle
        {
            Namespace = pkg,
            OpaRegoModule = rego,
            CedarPolicySet = cedar
        };
    }
}
