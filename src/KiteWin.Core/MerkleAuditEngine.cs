//
// MerkleAuditEngine.cs
// KiteWin.Core
//
// Enterprise Identity & Access Management (IAM) and Cryptographic Merkle Audit Trail.
// Provides append-only hash chains, binary Merkle tree root calculation,
// verifiable inclusion proofs, and RFC architectural sign-off workflows.
//

using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;

namespace KiteWin.Core
{
    public enum AuditActionType
    {
        [JsonPropertyName("nodeCreated")]
        NodeCreated,
        [JsonPropertyName("nodeUpdated")]
        NodeUpdated,
        [JsonPropertyName("nodeDeleted")]
        NodeDeleted,
        [JsonPropertyName("connectorLinked")]
        ConnectorLinked,
        [JsonPropertyName("connectorUnlinked")]
        ConnectorUnlinked,
        [JsonPropertyName("rfcApproved")]
        RfcApproved,
        [JsonPropertyName("rfcRejected")]
        RfcRejected,
        [JsonPropertyName("policyEnforced")]
        PolicyEnforced
    }

    public enum EnterpriseRole
    {
        [JsonPropertyName("architectureOwner")]
        ArchitectureOwner,
        [JsonPropertyName("contributor")]
        Contributor,
        [JsonPropertyName("reviewer")]
        Reviewer,
        [JsonPropertyName("auditor")]
        Auditor
    }

    public enum IdentityProvider
    {
        [JsonPropertyName("githubEnterprise")]
        GitHubEnterprise,
        [JsonPropertyName("okta")]
        Okta,
        [JsonPropertyName("googleWorkspace")]
        GoogleWorkspace,
        [JsonPropertyName("keycloak")]
        Keycloak,
        [JsonPropertyName("local")]
        Local
    }

    public class AuditActor
    {
        [JsonPropertyName("userId")]
        public string UserId { get; set; } = string.Empty;

        [JsonPropertyName("email")]
        public string Email { get; set; } = string.Empty;

        [JsonPropertyName("role")]
        public string Role { get; set; } = "architectureOwner";

        [JsonPropertyName("tenantId")]
        public string TenantId { get; set; } = "enterprise-default";

        [JsonPropertyName("provider")]
        public string Provider { get; set; } = "githubEnterprise";

        public AuditActor() { }

        public AuditActor(string userId, string email, string role, string tenantId = "enterprise-default", string provider = "githubEnterprise")
        {
            UserId = userId;
            Email = email;
            Role = role;
            TenantId = tenantId;
            Provider = provider;
        }
    }

    public class MerkleAuditLeaf
    {
        [JsonPropertyName("id")]
        public string Id => LeafHash;

        [JsonPropertyName("index")]
        public int Index { get; set; }

        [JsonPropertyName("timestampIso")]
        public string TimestampIso { get; set; } = DateTime.UtcNow.ToString("o");

        [JsonPropertyName("action")]
        public string Action { get; set; } = "policyEnforced";

        [JsonPropertyName("actor")]
        public AuditActor Actor { get; set; } = new();

        [JsonPropertyName("entityId")]
        public string EntityId { get; set; } = string.Empty;

        [JsonPropertyName("entityType")]
        public string EntityType { get; set; } = string.Empty;

        [JsonPropertyName("previousHash")]
        public string PreviousHash { get; set; } = string.Empty;

        [JsonPropertyName("payloadHash")]
        public string PayloadHash { get; set; } = string.Empty;

        [JsonPropertyName("leafHash")]
        public string LeafHash { get; set; } = string.Empty;

        public MerkleAuditLeaf() { }

        public MerkleAuditLeaf(int index, string action, AuditActor actor, string entityId, string entityType, string previousHash, string payloadJson)
        {
            Index = index;
            TimestampIso = DateTime.UtcNow.ToString("o");
            Action = action;
            Actor = actor;
            EntityId = entityId;
            EntityType = entityType;
            PreviousHash = previousHash;

            using var sha = SHA256.Create();
            var pBytes = sha.ComputeHash(Encoding.UTF8.GetBytes(payloadJson));
            PayloadHash = BitConverter.ToString(pBytes).Replace("-", "").ToLowerInvariant();

            var combined = $"{index}|{TimestampIso}|{action}|{actor.Email}|{entityId}|{previousHash}|{PayloadHash}";
            var lBytes = sha.ComputeHash(Encoding.UTF8.GetBytes(combined));
            LeafHash = BitConverter.ToString(lBytes).Replace("-", "").ToLowerInvariant();
        }
    }

    public class MerkleTreeRoot
    {
        [JsonPropertyName("rootHash")]
        public string RootHash { get; set; } = string.Empty;

        [JsonPropertyName("leafCount")]
        public int LeafCount { get; set; }

        [JsonPropertyName("height")]
        public int Height { get; set; }

        [JsonPropertyName("generatedAtIso")]
        public string GeneratedAtIso { get; set; } = DateTime.UtcNow.ToString("o");
    }

    public class MerkleInclusionProof
    {
        [JsonPropertyName("leafHash")]
        public string LeafHash { get; set; } = string.Empty;

        [JsonPropertyName("leafIndex")]
        public int LeafIndex { get; set; }

        [JsonPropertyName("auditPath")]
        public List<string> AuditPath { get; set; } = new();

        [JsonPropertyName("rootHash")]
        public string RootHash { get; set; } = string.Empty;

        [JsonPropertyName("isVerified")]
        public bool IsVerified { get; set; }
    }

    public class RFCWorkflowItem
    {
        [JsonPropertyName("id")]
        public string Id => RfcId;

        [JsonPropertyName("rfcId")]
        public string RfcId { get; set; } = Guid.NewGuid().ToString();

        [JsonPropertyName("title")]
        public string Title { get; set; } = string.Empty;

        [JsonPropertyName("authorEmail")]
        public string AuthorEmail { get; set; } = string.Empty;

        [JsonPropertyName("status")]
        public string Status { get; set; } = "inReview";

        [JsonPropertyName("signers")]
        public List<AuditActor> Signers { get; set; } = new();

        [JsonPropertyName("merkleLeafHash")]
        public string? MerkleLeafHash { get; set; }
    }

    public class MerkleAuditEngine
    {
        private static readonly Lazy<MerkleAuditEngine> _instance = new(() => new MerkleAuditEngine());
        public static MerkleAuditEngine Instance => _instance.Value;

        private readonly object _lock = new();
        private readonly List<MerkleAuditLeaf> _auditLeaves = new();
        private readonly Dictionary<string, RFCWorkflowItem> _rfcItems = new();
        private AuditActor _currentActor;

        public MerkleAuditEngine()
        {
            _currentActor = new AuditActor(
                "usr-arch-lead-01",
                "chief-architect@enterprise.corp",
                "architectureOwner",
                "corp-global",
                "githubEnterprise"
            );

            var genesis = new MerkleAuditLeaf(
                0,
                "policyEnforced",
                _currentActor,
                "system-genesis",
                "security-perimeter",
                "0000000000000000000000000000000000000000000000000000000000000000",
                "{\"policy\": \"zero-trust-architecture\", \"version\": \"1.0\"}"
            );
            _auditLeaves.Add(genesis);

            var rfc1 = new RFCWorkflowItem
            {
                RfcId = "RFC-2026-08",
                Title = "Migrate Payment Gateway from Monolith to Event-Driven Stream",
                AuthorEmail = "lead-dev@enterprise.corp",
                Status = "inReview",
                Signers = new List<AuditActor>()
            };
            _rfcItems[rfc1.RfcId] = rfc1;
        }

        public AuditActor GetCurrentActor()
        {
            lock (_lock)
            {
                return _currentActor;
            }
        }

        public List<MerkleAuditLeaf> AllLeaves()
        {
            lock (_lock)
            {
                return _auditLeaves.ToList();
            }
        }

        public MerkleAuditLeaf RecordAction(string action, string entityId, string entityType, string payloadJson, AuditActor? actor = null)
        {
            lock (_lock)
            {
                var activeActor = actor ?? _currentActor;
                var prevHash = _auditLeaves.LastOrDefault()?.LeafHash ?? "0000000000000000000000000000000000000000000000000000000000000000";
                var nextIndex = _auditLeaves.Count;

                var leaf = new MerkleAuditLeaf(nextIndex, action, activeActor, entityId, entityType, prevHash, payloadJson);
                _auditLeaves.Add(leaf);
                return leaf;
            }
        }

        public MerkleTreeRoot ComputeMerkleRoot()
        {
            lock (_lock)
            {
                if (_auditLeaves.Count == 0)
                {
                    return new MerkleTreeRoot
                    {
                        RootHash = "0000000000000000000000000000000000000000000000000000000000000000",
                        LeafCount = 0,
                        Height = 0
                    };
                }

                var currentLayer = _auditLeaves.Select(l => l.LeafHash).ToList();
                int height = 0;
                using var sha = SHA256.Create();

                while (currentLayer.Count > 1)
                {
                    var nextLayer = new List<string>();
                    for (int i = 0; i < currentLayer.Count; i += 2)
                    {
                        var left = currentLayer[i];
                        var right = (i + 1 < currentLayer.Count) ? currentLayer[i + 1] : left;
                        var combined = left + right;
                        var digest = sha.ComputeHash(Encoding.UTF8.GetBytes(combined));
                        nextLayer.Add(BitConverter.ToString(digest).Replace("-", "").ToLowerInvariant());
                    }
                    currentLayer = nextLayer;
                    height++;
                }

                return new MerkleTreeRoot
                {
                    RootHash = currentLayer.FirstOrDefault() ?? "",
                    LeafCount = _auditLeaves.Count,
                    Height = height,
                    GeneratedAtIso = DateTime.UtcNow.ToString("o")
                };
            }
        }

        public MerkleInclusionProof? GenerateInclusionProof(int index)
        {
            lock (_lock)
            {
                if (index < 0 || index >= _auditLeaves.Count) return null;

                var targetLeafHash = _auditLeaves[index].LeafHash;
                var currentLayer = _auditLeaves.Select(l => l.LeafHash).ToList();
                var auditPath = new List<string>();
                int currentIndex = index;
                using var sha = SHA256.Create();

                while (currentLayer.Count > 1)
                {
                    int siblingIndex = (currentIndex % 2 == 0) ? (currentIndex + 1) : (currentIndex - 1);
                    string siblingHash = (siblingIndex < currentLayer.Count) ? currentLayer[siblingIndex] : currentLayer[currentIndex];
                    auditPath.Add(siblingHash);

                    var nextLayer = new List<string>();
                    for (int i = 0; i < currentLayer.Count; i += 2)
                    {
                        var left = currentLayer[i];
                        var right = (i + 1 < currentLayer.Count) ? currentLayer[i + 1] : left;
                        var combined = left + right;
                        var digest = sha.ComputeHash(Encoding.UTF8.GetBytes(combined));
                        nextLayer.Add(BitConverter.ToString(digest).Replace("-", "").ToLowerInvariant());
                    }
                    currentLayer = nextLayer;
                    currentIndex /= 2;
                }

                var calculatedRoot = currentLayer.FirstOrDefault() ?? "";

                // Verify proof locally
                var proofHash = targetLeafHash;
                var verifyIndex = index;
                foreach (var sibling in auditPath)
                {
                    var combined = (verifyIndex % 2 == 0) ? (proofHash + sibling) : (sibling + proofHash);
                    var digest = sha.ComputeHash(Encoding.UTF8.GetBytes(combined));
                    proofHash = BitConverter.ToString(digest).Replace("-", "").ToLowerInvariant();
                    verifyIndex /= 2;
                }

                bool isValid = (proofHash == calculatedRoot);

                return new MerkleInclusionProof
                {
                    LeafHash = targetLeafHash,
                    LeafIndex = index,
                    AuditPath = auditPath,
                    RootHash = calculatedRoot,
                    IsVerified = isValid
                };
            }
        }

        public (bool Success, string? Error, RFCWorkflowItem? Item) SignOffRFC(string rfcId, bool approve, AuditActor? actor = null)
        {
            lock (_lock)
            {
                var activeActor = actor ?? _currentActor;
                if (activeActor.Role != "architectureOwner" && activeActor.Role != "reviewer")
                {
                    return (false, $"Forbidden: Actor role '{activeActor.Role}' is not authorized to sign off architectural RFCs", null);
                }

                if (!_rfcItems.TryGetValue(rfcId, out var item))
                {
                    return (false, $"Not Found: RFC '{rfcId}' does not exist", null);
                }

                item.Status = approve ? "approved" : "rejected";
                if (!item.Signers.Any(s => s.UserId == activeActor.UserId))
                {
                    item.Signers.Add(activeActor);
                }

                var prevHash = _auditLeaves.LastOrDefault()?.LeafHash ?? "0000000000000000000000000000000000000000000000000000000000000000";
                var leaf = new MerkleAuditLeaf(
                    _auditLeaves.Count,
                    approve ? "rfcApproved" : "rfcRejected",
                    activeActor,
                    rfcId,
                    "architectural-rfc",
                    prevHash,
                    $"{{\"rfcId\": \"{rfcId}\", \"title\": \"{item.Title}\", \"approved\": {approve.ToString().ToLowerInvariant()}}}"
                );
                _auditLeaves.Add(leaf);
                item.MerkleLeafHash = leaf.LeafHash;
                _rfcItems[rfcId] = item;

                return (true, null, item);
            }
        }

        public List<RFCWorkflowItem> AllRFCs()
        {
            lock (_lock)
            {
                return _rfcItems.Values.ToList();
            }
        }
    }
}
