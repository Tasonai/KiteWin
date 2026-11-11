using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace KiteWin.Core;

public sealed class FederatedTrustDomain
{
    [JsonPropertyName("spiffeTrustDomain")]
    public string SpiffeTrustDomain { get; init; } = "";

    [JsonPropertyName("bundleEndpointUrl")]
    public string BundleEndpointUrl { get; init; } = "";

    [JsonPropertyName("svidRotationMinutes")]
    public int SvidRotationMinutes { get; init; }

    [JsonPropertyName("mtlsVerified")]
    public bool MtlsVerified { get; init; }
}

public sealed class B2bContractVerificationResult
{
    [JsonPropertyName("contractId")]
    public string ContractId { get; init; } = "";

    [JsonPropertyName("partnerTrustDomain")]
    public string PartnerTrustDomain { get; init; } = "";

    [JsonPropertyName("backwardCompatible")]
    public bool BackwardCompatible { get; init; }

    [JsonPropertyName("breakingChanges")]
    public List<string> BreakingChanges { get; init; } = new();
}

public sealed class BackstageCatalogBundle
{
    [JsonPropertyName("systemName")]
    public string SystemName { get; init; } = "";

    [JsonPropertyName("federatedDomainsCount")]
    public int FederatedDomainsCount { get; init; }

    [JsonPropertyName("catalogInfoYaml")]
    public string CatalogInfoYaml { get; init; } = "";

    [JsonPropertyName("spireFederationYaml")]
    public string SpireFederationYaml { get; init; } = "";
}

public sealed class FederationCatalogEngine
{
    public static FederationCatalogEngine Instance { get; } = new();

    private readonly object _sync = new();
    private readonly List<FederatedTrustDomain> _domains;

    public FederationCatalogEngine()
    {
        _domains = new List<FederatedTrustDomain>
        {
            new()
            {
                SpiffeTrustDomain = "spiffe://kite.internal",
                BundleEndpointUrl = "https://spire-bundle.kite.internal:8443/bundle",
                SvidRotationMinutes = 60,
                MtlsVerified = true
            },
            new()
            {
                SpiffeTrustDomain = "spiffe://clearing-partner.eu",
                BundleEndpointUrl = "https://federation.clearing-partner.eu:8443/spiffe-bundle",
                SvidRotationMinutes = 30,
                MtlsVerified = true
            },
            new()
            {
                SpiffeTrustDomain = "spiffe://custody-vault.ch",
                BundleEndpointUrl = "https://spire.custody-vault.ch:8443/bundle",
                SvidRotationMinutes = 15,
                MtlsVerified = true
            }
        };
    }

    public List<FederatedTrustDomain> GetTrustDomains()
    {
        lock (_sync)
        {
            return _domains.ToList();
        }
    }

    public B2bContractVerificationResult VerifyBoundaryContract(
        string contractId = "settlement-events-v2",
        string partnerTrustDomain = "spiffe://clearing-partner.eu",
        bool removeRequiredField = false)
    {
        string safeContract = string.IsNullOrWhiteSpace(contractId) ? "settlement-events-v2" : contractId.Trim();
        string safePartner = string.IsNullOrWhiteSpace(partnerTrustDomain) ? "spiffe://clearing-partner.eu" : partnerTrustDomain.Trim();

        if (removeRequiredField)
        {
            return new B2bContractVerificationResult
            {
                ContractId = safeContract,
                PartnerTrustDomain = safePartner,
                BackwardCompatible = false,
                BreakingChanges = new List<string>
                {
                    $"BREAKING: Removed required property 'idempotencyKey' in {safeContract} consumed by {safePartner}.",
                    "BREAKING: Narrowed enum settlementCurrency without major API version bump."
                }
            };
        }

        return new B2bContractVerificationResult
        {
            ContractId = safeContract,
            PartnerTrustDomain = safePartner,
            BackwardCompatible = true,
            BreakingChanges = new List<string>()
        };
    }

    public BackstageCatalogBundle GenerateFederationCatalog(string systemName = "kite-enterprise-platform")
    {
        string safeSys = string.IsNullOrWhiteSpace(systemName) ? "kite-enterprise-platform" : systemName.Trim();
        var doms = GetTrustDomains();

        string catalogYaml = $@"apiVersion: backstage.io/v1alpha1
kind: System
metadata:
  name: {safeSys}
  annotations:
    kite.io/spiffe-root: spiffe://kite.internal
spec:
  owner: platform-architecture
  domain: enterprise-core
---
apiVersion: backstage.io/v1alpha1
kind: API
metadata:
  name: {safeSys}-b2b-settlement-api
spec:
  type: asyncapi
  lifecycle: production
  owner: platform-architecture
  system: {safeSys}";

        string spireYaml = @"apiVersion: spire.spiffe.io/v1alpha1
kind: ClusterFederatedTrustDomain
metadata:
  name: clearing-partner-eu
spec:
  trustDomain: clearing-partner.eu
  bundleEndpointURL: https://federation.clearing-partner.eu:8443/spiffe-bundle
  bundleEndpointProfile:
    type: https_spiffe
    endpointSPIFFEID: spiffe://clearing-partner.eu/ns/spire/sa/spire-server";

        return new BackstageCatalogBundle
        {
            SystemName = safeSys,
            FederatedDomainsCount = doms.Count,
            CatalogInfoYaml = catalogYaml,
            SpireFederationYaml = spireYaml
        };
    }
}
