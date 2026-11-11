//
// LoadTestingEngine.cs
// KiteWin.Core
//
// Synthetic Load & Performance Engineering Engine (Phase 27).
// Computes Gunther's Universal Scalability Law (USL) retrograde contention knees and
// Little's Law concurrency depth across architecture services, and synthesizes executable
// Grafana k6 and Python Locust distributed load-testing suites with SLA thresholds.
//

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace KiteWin.Core;

public sealed class ServiceLoadProfile
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = "";

    [JsonPropertyName("serviceId")]
    public string ServiceId { get; init; } = "";

    [JsonPropertyName("endpointPath")]
    public string EndpointPath { get; init; } = "";

    [JsonPropertyName("baselineSingleWorkerRps")]
    public double BaselineSingleWorkerRps { get; init; }

    [JsonPropertyName("contentionSigma")]
    public double ContentionSigma { get; init; }

    [JsonPropertyName("coherencyKappa")]
    public double CoherencyKappa { get; init; }

    [JsonPropertyName("targetP99LatencyMs")]
    public double TargetP99LatencyMs { get; init; }
}

public sealed class UslScalabilityForecast
{
    [JsonPropertyName("serviceId")]
    public string ServiceId { get; init; } = "";

    [JsonPropertyName("concurrencyN")]
    public int ConcurrencyN { get; init; }

    [JsonPropertyName("relativeCapacityFactor")]
    public double RelativeCapacityFactor { get; init; }

    [JsonPropertyName("projectedThroughputRps")]
    public double ProjectedThroughputRps { get; init; }

    [JsonPropertyName("optimalConcurrencyKnee")]
    public int OptimalConcurrencyKnee { get; init; }

    [JsonPropertyName("littlesLawInFlightRequests")]
    public double LittlesLawInFlightRequests { get; init; }

    [JsonPropertyName("isRetrogradeRegion")]
    public bool IsRetrogradeRegion { get; init; }
}

public sealed class SyntheticLoadBundle
{
    [JsonPropertyName("serviceId")]
    public string ServiceId { get; init; } = "";

    [JsonPropertyName("targetVus")]
    public int TargetVus { get; init; }

    [JsonPropertyName("durationSec")]
    public int DurationSec { get; init; }

    [JsonPropertyName("k6ScriptJs")]
    public string K6ScriptJs { get; init; } = "";

    [JsonPropertyName("locustFilePy")]
    public string LocustFilePy { get; init; } = "";
}

public sealed class LoadTestingEngine
{
    public static LoadTestingEngine Instance { get; } = new();

    private readonly object _lock = new();
    private readonly List<ServiceLoadProfile> _profiles = new();

    public LoadTestingEngine()
    {
        _profiles.AddRange(new[]
        {
            new ServiceLoadProfile
            {
                Id = "load-checkout",
                ServiceId = "checkout-api",
                EndpointPath = "/v1/orders/checkout",
                BaselineSingleWorkerRps = 420.0,
                ContentionSigma = 0.04,
                CoherencyKappa = 0.0015,
                TargetP99LatencyMs = 95.0
            },
            new ServiceLoadProfile
            {
                Id = "load-ledger",
                ServiceId = "ledger-writer",
                EndpointPath = "/v1/ledger/commit",
                BaselineSingleWorkerRps = 180.0,
                ContentionSigma = 0.08,
                CoherencyKappa = 0.0040,
                TargetP99LatencyMs = 60.0
            },
            new ServiceLoadProfile
            {
                Id = "load-search",
                ServiceId = "search-indexer",
                EndpointPath = "/v1/catalog/query",
                BaselineSingleWorkerRps = 950.0,
                ContentionSigma = 0.02,
                CoherencyKappa = 0.0005,
                TargetP99LatencyMs = 45.0
            }
        });
    }

    public List<ServiceLoadProfile> GetProfiles()
    {
        lock (_lock)
        {
            return _profiles.ToList();
        }
    }

    public UslScalabilityForecast EvaluateUslModel(string serviceId = "checkout-api", int concurrencyN = 16)
    {
        ServiceLoadProfile prof;
        lock (_lock)
        {
            prof = _profiles.FirstOrDefault(p => p.ServiceId == serviceId) ?? _profiles[0];
        }

        int n = Math.Max(1, concurrencyN);
        double nDouble = n;
        double sigma = prof.ContentionSigma;
        double kappa = Math.Max(0.000001, prof.CoherencyKappa);

        double denom = 1.0 + sigma * (nDouble - 1.0) + kappa * nDouble * (nDouble - 1.0);
        double cFactor = Math.Round(nDouble / denom, 2);
        double throughput = Math.Round(prof.BaselineSingleWorkerRps * (nDouble / denom), 1);

        int knee = Math.Max(1, (int)Math.Floor(Math.Sqrt(Math.Max(0.0, 1.0 - sigma) / kappa)));
        double latencySec = prof.TargetP99LatencyMs / 1000.0;
        double inFlight = Math.Round(throughput * latencySec, 2);

        return new UslScalabilityForecast
        {
            ServiceId = prof.ServiceId,
            ConcurrencyN = n,
            RelativeCapacityFactor = cFactor,
            ProjectedThroughputRps = throughput,
            OptimalConcurrencyKnee = knee,
            LittlesLawInFlightRequests = inFlight,
            IsRetrogradeRegion = n > knee
        };
    }

    public SyntheticLoadBundle SynthesizeLoadScripts(string serviceId = "checkout-api", int targetVus = 200, int durationSec = 120)
    {
        ServiceLoadProfile prof;
        lock (_lock)
        {
            prof = _profiles.FirstOrDefault(p => p.ServiceId == serviceId) ?? _profiles[0];
        }

        int vus = Math.Max(10, targetVus);
        int dur = Math.Max(30, durationSec);
        int p99Int = (int)prof.TargetP99LatencyMs;
        int rampUp = Math.Max(10, dur / 4);
        int steady = Math.Max(10, dur - (2 * rampUp));
        string safeFn = prof.ServiceId.Replace("-", "_");

        string k6Js = $@"import http from 'k6/http';
import {{ check, sleep }} from 'k6';

export const options = {{
  scenarios: {{
    kite_usl_profile: {{
      executor: 'ramping-vus',
      startVUs: 1,
      stages: [
        {{ duration: '{rampUp}s', target: {vus} }},
        {{ duration: '{steady}s', target: {vus} }},
        {{ duration: '{rampUp}s', target: 0 }},
      ],
    }},
  }},
  thresholds: {{
    http_req_failed: ['rate<0.005'],
    http_req_duration: ['p(99)<{p99Int}'],
  }},
}};

export default function () {{
  const baseUrl = __ENV.KITE_TARGET_URL || 'https://api.kite.internal';
  const res = http.post(`${{baseUrl}}{prof.EndpointPath}`, JSON.stringify({{ syntheticProbe: true }}), {{
    headers: {{ 'Content-Type': 'application/json' }},
  }});
  check(res, {{ 'status is 200': (r) => r.status === 200 }});
  sleep(0.1);
}}";

        string locustPy = $@"from locust import HttpUser, task, between

class KiteSyntheticSwarmUser(HttpUser):
    wait_time = between(0.05, 0.25)

    @task(1)
    def exercise_{safeFn}(self):
        with self.client.post(
            ""{prof.EndpointPath}"",
            json={{""syntheticProbe"": True, ""service"": ""{prof.ServiceId}""}},
            catch_response=True,
        ) as resp:
            if resp.status_code != 200:
                resp.failure(f""Unexpected HTTP {{resp.status_code}}"")
            elif resp.elapsed.total_seconds() * 1000.0 > {p99Int}:
                resp.failure(""SLA P99 threshold breached"")";

        return new SyntheticLoadBundle
        {
            ServiceId = prof.ServiceId,
            TargetVus = vus,
            DurationSec = dur,
            K6ScriptJs = k6Js,
            LocustFilePy = locustPy
        };
    }
}
