using KiteWin.Core.Models;

namespace KiteWin.Core;

public enum SandboxRuntime
{
    Docker,
    AppleVirtualization,
    ProcessIsolation,
    Wasm
}

public enum SandboxStatus
{
    Idle,
    Provisioning,
    Running,
    Degraded,
    Terminated
}

public sealed class ServiceContainerSpec
{
    public string Name { get; set; } = string.Empty;
    public string Image { get; set; } = string.Empty;
    public int InternalPort { get; set; }
    public int HostPort { get; set; }
    public Dictionary<string, string> Environment { get; set; } = [];
    public string? HealthCheckCommand { get; set; }
    public List<string> DependsOn { get; set; } = [];

    public ServiceContainerSpec() { }

    public ServiceContainerSpec(string name, string image, int internalPort, int hostPort, Dictionary<string, string>? env = null, string? healthCheck = null, List<string>? dependsOn = null)
    {
        Name = name;
        Image = image;
        InternalPort = internalPort;
        HostPort = hostPort;
        Environment = env ?? [];
        HealthCheckCommand = healthCheck;
        DependsOn = dependsOn ?? [];
    }
}

public static class SandboxEngine
{
    public static ServiceContainerSpec InferContainerSpec(string frameTitle, int index)
    {
        var lower = frameTitle.ToLowerInvariant();
        var cleanName = new string(frameTitle.ToLowerInvariant()
            .Select(c => char.IsLetterOrDigit(c) ? c : '-')
            .ToArray())
            .Trim('-');

        var svcName = string.IsNullOrEmpty(cleanName) ? $"service-{index}" : cleanName;

        if (lower.Contains("postgres") || lower.Contains("db") || lower.Contains("database"))
        {
            return new ServiceContainerSpec(
                svcName,
                "postgres:16-alpine",
                5432,
                5432 + index,
                new Dictionary<string, string>
                {
                    ["POSTGRES_DB"] = "kitedb",
                    ["POSTGRES_USER"] = "kite",
                    ["POSTGRES_PASSWORD"] = "kite_secret_dev_pass"
                },
                "pg_isready -U kite -d kitedb",
                []
            );
        }
        if (lower.Contains("redis") || lower.Contains("cache"))
        {
            return new ServiceContainerSpec(
                svcName,
                "redis:7-alpine",
                6379,
                6379 + index,
                null,
                "redis-cli ping",
                []
            );
        }
        if (lower.Contains("gateway") || lower.Contains("ingress") || lower.Contains("proxy"))
        {
            return new ServiceContainerSpec(
                svcName,
                "traefik:v3.0",
                80,
                8080 + index,
                null,
                null,
                []
            );
        }
        if (lower.Contains("web") || lower.Contains("front") || lower.Contains("ui"))
        {
            return new ServiceContainerSpec(
                svcName,
                "node:20-alpine",
                3000,
                3000 + index,
                new Dictionary<string, string> { ["NODE_ENV"] = "development", ["PORT"] = "3000" },
                null,
                []
            );
        }

        return new ServiceContainerSpec(
            svcName,
            "golang:1.22-alpine",
            8000,
            8000 + index,
            new Dictionary<string, string> { ["APP_ENV"] = "development" },
            null,
            []
        );
    }

    public static string SynthesizeDockerCompose(List<CanvasFrame> frames)
    {
        var lines = new List<string>
        {
            "# Generated automatically by Kite Sandbox Engine (BAMA/BAWA Specification)",
            "# Zero Emojis, production isolated micro-environment topology",
            "version: '3.8'",
            "",
            "services:"
        };

        var specs = frames.Select((f, idx) => InferContainerSpec(f.Title, idx)).ToList();
        var dbNames = specs.Where(s => s.Image.Contains("postgres") || s.Image.Contains("redis")).Select(s => s.Name).ToHashSet();

        foreach (var spec in specs)
        {
            if (!dbNames.Contains(spec.Name) && dbNames.Count > 0)
            {
                spec.DependsOn = dbNames.ToList();
            }

            lines.Add($"  {spec.Name}:");
            lines.Add($"    image: {spec.Image}");
            lines.Add($"    container_name: kite-{spec.Name}");
            lines.Add("    ports:");
            lines.Add($"      - \"{spec.HostPort}:{spec.InternalPort}\"");

            if (spec.Environment.Count > 0)
            {
                lines.Add("    environment:");
                foreach (var (k, v) in spec.Environment.OrderBy(e => e.Key))
                {
                    lines.Add($"      {k}: {v}");
                }
            }

            if (!string.IsNullOrEmpty(spec.HealthCheckCommand))
            {
                lines.Add("    healthcheck:");
                lines.Add($"      test: [\"CMD-SHELL\", \"{spec.HealthCheckCommand}\"]");
                lines.Add("      interval: 10s");
                lines.Add("      timeout: 5s");
                lines.Add("      retries: 5");
            }

            if (spec.DependsOn.Count > 0)
            {
                lines.Add("    depends_on:");
                foreach (var dep in spec.DependsOn)
                {
                    lines.Add($"      - {dep}");
                }
            }

            lines.Add("    networks:");
            lines.Add("      - kite-mesh-net");
            lines.Add("    restart: unless-stopped");
            lines.Add("");
        }

        lines.Add("networks:");
        lines.Add("  kite-mesh-net:");
        lines.Add("    driver: bridge");

        return string.Join("\n", lines);
    }
}
