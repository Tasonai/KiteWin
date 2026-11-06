using KiteWin.Core.Models;

namespace KiteWin.Core;

public sealed record PhaseTask(string Title, string Status);
public sealed record PhaseWave(int PhaseNumber, string Name, IReadOnlyList<PhaseTask> Tasks);

public static class RoadmapTracker
{
    public static List<PhaseWave> ParseOrCreateRoadmap(string? markdown = null)
    {
        if (!string.IsNullOrWhiteSpace(markdown))
        {
            var waves = new List<PhaseWave>();
            var currentPhaseName = "";
            var currentTasks = new List<PhaseTask>();
            var phaseCounter = 1;

            var lines = markdown.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                var trimmed = line.Trim();
                if (trimmed.StartsWith("## Phase") || trimmed.StartsWith("## Фаза"))
                {
                    if (!string.IsNullOrEmpty(currentPhaseName))
                    {
                        waves.Add(new PhaseWave(phaseCounter++, currentPhaseName, currentTasks));
                        currentTasks = [];
                    }
                    currentPhaseName = trimmed.Replace("#", "").Trim();
                }
                else if (trimmed.StartsWith("- [x]"))
                {
                    currentTasks.Add(new PhaseTask(trimmed.Replace("- [x]", "").Trim(), "verified"));
                }
                else if (trimmed.StartsWith("- [/]"))
                {
                    currentTasks.Add(new PhaseTask(trimmed.Replace("- [/]", "").Trim(), "in_progress"));
                }
                else if (trimmed.StartsWith("- [ ]"))
                {
                    currentTasks.Add(new PhaseTask(trimmed.Replace("- [ ]]", "").Trim(), "pending"));
                }
            }

            if (!string.IsNullOrEmpty(currentPhaseName))
            {
                waves.Add(new PhaseWave(phaseCounter, currentPhaseName, currentTasks));
            }

            if (waves.Count > 0) return waves;
        }

        return [
            new PhaseWave(1, "Phase 1: Foundation & Data", [
                new PhaseTask("Local Schema & SQLite/SwiftData", "verified"),
                new PhaseTask("P2P REST Handshake (Port 24242/3)", "verified"),
                new PhaseTask("Zero Emojis Lint & Audit", "verified")
            ]),
            new PhaseWave(2, "Phase 2: Core Architecture", [
                new PhaseTask("Interactive Canvas & Connectors", "in_progress"),
                new PhaseTask("AI Co-Architect & Gap Engine", "in_progress"),
                new PhaseTask("Repo Ingestion Parser", "pending")
            ]),
            new PhaseWave(3, "Phase 3: Polish & Distribution", [
                new PhaseTask("Apple HIG & BAWA Grid Verification", "pending"),
                new PhaseTask("Homebrew Cask & Setup.exe", "verified"),
                new PhaseTask("End-to-End P2P Sync Test", "verified")
            ])
        ];
    }

    public static (List<CanvasFrame> Frames, List<CanvasSticky> Stickies, List<CanvasConnection> Connections) GenerateWaves(string content = "")
    {
        var waves = ParseRoadmap(content);
        return GenerateCanvasLanes(waves);
    }

    public static (List<CanvasFrame> Frames, List<CanvasSticky> Stickies, List<CanvasConnection> Connections) GenerateCanvasLanes(IReadOnlyList<PhaseWave> waves)
    {
        var frames = new List<CanvasFrame>();
        var stickies = new List<CanvasSticky>();
        var connections = new List<CanvasConnection>();

        double startX = 80;
        string previousPhaseLastStickyId = "";

        foreach (var wave in waves)
        {
            var color = wave.PhaseNumber == 1 ? "#10B981" : (wave.PhaseNumber == 2 ? "#3B82F6" : "#8B5CF6");
            frames.Add(new CanvasFrame
            {
                Title = $"{wave.Name} [Wave {wave.PhaseNumber}]",
                ColorHex = color,
                X = startX,
                Y = 80,
                Width = 360,
                Height = 480
            });

            string firstStickyId = "";
            for (var idx = 0; idx < Math.Min(3, wave.Tasks.Count); idx++)
            {
                var task = wave.Tasks[idx];
                var badge = task.Status == "verified" ? "[DONE]" : (task.Status == "in_progress" ? "[WIP]" : "[TODO]");
                var stickyColor = task.Status == "verified" ? "#D1FAE5" : (task.Status == "in_progress" ? "#FEF3C7" : "#F3F4F6");
                var stickyId = Guid.NewGuid().ToString();

                stickies.Add(new CanvasSticky
                {
                    Id = stickyId,
                    Text = $"{badge} {task.Title}",
                    ColorHex = stickyColor,
                    X = startX + 24,
                    Y = 150 + (idx * 100)
                });

                if (idx == 0) firstStickyId = stickyId;
                if (idx == wave.Tasks.Count - 1)
                {
                    if (!string.IsNullOrEmpty(previousPhaseLastStickyId))
                    {
                        connections.Add(new CanvasConnection
                        {
                            FromId = previousPhaseLastStickyId,
                            ToId = firstStickyId,
                            Label = "Depends On",
                            ColorHex = color
                        });
                    }
                    previousPhaseLastStickyId = stickyId;
                }
            }

            startX += 420;
        }

        return (frames, stickies, connections);
    }
}
