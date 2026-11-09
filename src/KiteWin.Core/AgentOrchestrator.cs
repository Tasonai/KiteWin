namespace KiteWin.Core;

public enum AgentRole
{
    Architect,
    BackendBuilder,
    FrontendBuilder,
    QATester,
    SecurityAuditor,
    SiteReliability
}

public enum AgentTaskStatus
{
    Pending,
    Running,
    Completed,
    Failed
}

public sealed class AgentTask
{
    public string Id { get; set; } = string.Empty;
    public AgentRole Role { get; set; }
    public string Title { get; set; } = string.Empty;
    public List<string> Dependencies { get; set; } = [];
    public AgentTaskStatus Status { get; set; } = AgentTaskStatus.Pending;
    public int EstimatedDurationSec { get; set; } = 10;

    public AgentTask() { }

    public AgentTask(string id, AgentRole role, string title, List<string>? dependencies = null, AgentTaskStatus status = AgentTaskStatus.Pending, int estimatedDurationSec = 10)
    {
        Id = id;
        Role = role;
        Title = title;
        Dependencies = dependencies ?? [];
        Status = status;
        EstimatedDurationSec = estimatedDurationSec;
    }
}

public sealed class MultiAgentWave
{
    public int WaveIndex { get; set; }
    public List<AgentTask> Tasks { get; set; } = [];

    public MultiAgentWave() { }

    public MultiAgentWave(int waveIndex, List<AgentTask> tasks)
    {
        WaveIndex = waveIndex;
        Tasks = tasks;
    }
}

public static class MultiAgentOrchestrator
{
    public static List<AgentTask> SynthesizeDefaultWorkflow(string projectName)
    {
        return
        [
            new AgentTask("task-1", AgentRole.Architect, "Synthesize System Topology and ERD Schema", []),
            new AgentTask("task-2", AgentRole.SecurityAuditor, "Threat Model & Negative Scope Audit", ["task-1"]),
            new AgentTask("task-3", AgentRole.BackendBuilder, "Scaffold Core API and PostgreSQL Migrations", ["task-1"]),
            new AgentTask("task-4", AgentRole.FrontendBuilder, "Synthesize Responsive Design Token UI", ["task-1"]),
            new AgentTask("task-5", AgentRole.QATester, "Generate Playwright E2E Integration Suite", ["task-3", "task-4"]),
            new AgentTask("task-6", AgentRole.SiteReliability, "Configure Prometheus Telemetry and Docker Compose", ["task-3", "task-5"])
        ];
    }

    public static List<MultiAgentWave> PartitionIntoWaves(List<AgentTask> tasks)
    {
        var completed = new HashSet<string>();
        var remaining = tasks.ToList();
        var waves = new List<MultiAgentWave>();
        var waveIdx = 1;

        while (remaining.Count > 0)
        {
            var readyTasks = remaining.Where(t => t.Dependencies.All(completed.Contains)).ToList();

            if (readyTasks.Count == 0)
            {
                waves.Add(new MultiAgentWave(waveIdx, remaining));
                break;
            }

            waves.Add(new MultiAgentWave(waveIdx, readyTasks));
            var readyIds = readyTasks.Select(t => t.Id).ToHashSet();
            foreach (var id in readyIds) completed.Add(id);
            remaining.RemoveAll(t => readyIds.Contains(t.Id));
            waveIdx++;
        }

        return waves;
    }
}
