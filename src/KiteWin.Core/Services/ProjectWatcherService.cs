namespace KiteWin.Core.Services;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

public sealed class ProjectArtifactSummary
{
    public string RelativePath { get; }
    public string Category { get; }
    public int WaveIndex { get; }

    public ProjectArtifactSummary(string relativePath, string category, int waveIndex)
    {
        RelativePath = relativePath;
        Category = category;
        WaveIndex = waveIndex;
    }
}

public sealed class ProjectProgressSnapshot
{
    public string WatchedPath { get; }
    public HashSet<string> CompletedTaskKeys { get; }
    public IReadOnlyList<ProjectArtifactSummary> DetectedArtifacts { get; }
    public IReadOnlyDictionary<int, int> WavePercentages { get; }
    public int OverallPercentage { get; }

    public ProjectProgressSnapshot(
        string watchedPath,
        HashSet<string> completedTaskKeys,
        IReadOnlyList<ProjectArtifactSummary> detectedArtifacts,
        IReadOnlyDictionary<int, int> wavePercentages,
        int overallPercentage)
    {
        WatchedPath = watchedPath;
        CompletedTaskKeys = completedTaskKeys;
        DetectedArtifacts = detectedArtifacts;
        WavePercentages = wavePercentages;
        OverallPercentage = overallPercentage;
    }
}

public static class ProjectWatcherService
{
    public static ProjectProgressSnapshot AnalyzeDirectory(
        string path,
        IReadOnlyList<AntigravityWave> waves)
    {
        if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
        {
            return new ProjectProgressSnapshot(
                path ?? "",
                new HashSet<string>(StringComparer.OrdinalIgnoreCase),
                Array.Empty<ProjectArtifactSummary>(),
                new Dictionary<int, int> { [1] = 0, [2] = 0, [3] = 0, [4] = 0 },
                0
            );
        }

        var detectedArtifacts = new List<ProjectArtifactSummary>();
        var detectedRelativePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            var baseDir = new DirectoryInfo(path);
            ScanDirectory(baseDir, baseDir.FullName, detectedArtifacts, detectedRelativePaths, 0);
        }
        catch
        {
            // Defensive IO handling
        }

        var completedKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var wavePercentages = new Dictionary<int, int>();

        foreach (var wave in waves)
        {
            var totalTasks = wave.Scope.Count;
            if (totalTasks == 0)
            {
                wavePercentages[wave.Id] = 0;
                continue;
            }

            var count = 0;
            foreach (var task in wave.Scope)
            {
                var key = $"{task}_{wave.Id}";
                if (MatchesTask(task, wave.Id, detectedRelativePaths))
                {
                    completedKeys.Add(key);
                    count++;
                }
            }

            var pct = (int)((double)count / totalTasks * 100.0);
            wavePercentages[wave.Id] = Math.Clamp(pct, 0, 100);
        }

        var totalExpected = waves.Sum(w => w.Scope.Count);
        var overall = totalExpected > 0 ? (int)((double)completedKeys.Count / totalExpected * 100.0) : 0;

        return new ProjectProgressSnapshot(
            path,
            completedKeys,
            detectedArtifacts,
            wavePercentages,
            Math.Clamp(overall, 0, 100)
        );
    }

    private static void ScanDirectory(
        DirectoryInfo dir,
        string rootFullPath,
        List<ProjectArtifactSummary> artifacts,
        HashSet<string> detectedPaths,
        int currentDepth)
    {
        if (currentDepth > 4) return;

        var name = dir.Name.ToLowerInvariant();
        if (name == ".git" || name == "node_modules" || name == "bin" || name == "obj" || name == ".build" || name == "target" || name == "dist")
        {
            return;
        }

        FileInfo[] files;
        DirectoryInfo[] subDirs;
        try
        {
            files = dir.GetFiles();
            subDirs = dir.GetDirectories();
        }
        catch
        {
            return;
        }

        foreach (var file in files)
        {
            var rel = Path.GetRelativePath(rootFullPath, file.FullName).Replace('\\', '/');
            var lower = rel.ToLowerInvariant();
            detectedPaths.Add(lower);

            if (lower.EndsWith(".prisma") || lower.EndsWith("schema.sql") || lower.EndsWith("package.json") || lower.EndsWith("cargo.toml") || lower.EndsWith(".csproj"))
            {
                artifacts.Add(new ProjectArtifactSummary(rel, "Foundation & Schema", 1));
            }
            else if (lower.Contains("/api/") || lower.Contains("/services/") || lower.Contains("/server/") || lower.Contains("/routes/"))
            {
                artifacts.Add(new ProjectArtifactSummary(rel, "Backend Services", 2));
            }
            else if (lower.Contains("/components/") || lower.Contains("/pages/") || lower.Contains("/views/") || lower.EndsWith(".xaml"))
            {
                artifacts.Add(new ProjectArtifactSummary(rel, "UI Components", 3));
            }
            else if (lower.Contains("/tests/") || lower.Contains("/test/") || lower.Contains("__tests__") || lower.Contains(".github/workflows"))
            {
                artifacts.Add(new ProjectArtifactSummary(rel, "Tests & QA", 4));
            }
        }

        foreach (var sub in subDirs)
        {
            ScanDirectory(sub, rootFullPath, artifacts, detectedPaths, currentDepth + 1);
        }
    }

    private static bool MatchesTask(string taskText, int waveId, HashSet<string> detectedPaths)
    {
        var lower = (taskText ?? "").ToLowerInvariant();

        switch (waveId)
        {
            case 1:
                if (lower.Contains("schema") || lower.Contains("prisma") || lower.Contains("бд") || lower.Contains("баз"))
                {
                    return detectedPaths.Any(p => p.Contains("schema") || p.Contains("prisma") || p.Contains("sql") || p.Contains("migrations"));
                }
                if (lower.Contains("типы") || lower.Contains("модели") || lower.Contains("model") || lower.Contains("type"))
                {
                    return detectedPaths.Any(p => p.Contains("types") || p.Contains("models") || p.Contains("model"));
                }
                if (lower.Contains("проект") || lower.Contains("каркас") || lower.Contains("конфиг") || lower.Contains("config"))
                {
                    return detectedPaths.Any(p => p.Contains("package.json") || p.Contains("tsconfig") || p.Contains("cargo.toml") || p.Contains(".csproj"));
                }
                return detectedPaths.Any(p => p.Contains("package.json") || p.Contains("schema") || p.Contains("src") || p.Contains(".csproj"));

            case 2:
                if (lower.Contains("auth") || lower.Contains("авториз") || lower.Contains("безопасн") || lower.Contains("токен"))
                {
                    return detectedPaths.Any(p => p.Contains("auth") || p.Contains("jwt") || p.Contains("guard") || p.Contains("session"));
                }
                if (lower.Contains("api") || lower.Contains("роут") || lower.Contains("route") || lower.Contains("endpoint"))
                {
                    return detectedPaths.Any(p => p.Contains("api") || p.Contains("routes") || p.Contains("controllers") || p.Contains("endpoints"));
                }
                if (lower.Contains("сервис") || lower.Contains("service") || lower.Contains("логик"))
                {
                    return detectedPaths.Any(p => p.Contains("services") || p.Contains("server") || p.Contains("core"));
                }
                return detectedPaths.Any(p => p.Contains("api") || p.Contains("services") || p.Contains("controllers") || p.Contains("server"));

            case 3:
                if (lower.Contains("компонент") || lower.Contains("ui") || lower.Contains("кнопк") || lower.Contains("виджет"))
                {
                    return detectedPaths.Any(p => p.Contains("components") || p.Contains("widgets") || p.Contains("ui"));
                }
                if (lower.Contains("экран") || lower.Contains("страниц") || lower.Contains("page") || lower.Contains("view"))
                {
                    return detectedPaths.Any(p => p.Contains("pages") || p.Contains("views") || p.Contains("app") || p.EndsWith(".xaml"));
                }
                if (lower.Contains("состояни") || lower.Contains("state") || lower.Contains("стил") || lower.Contains("theme"))
                {
                    return detectedPaths.Any(p => p.Contains("hooks") || p.Contains("store") || p.Contains("styles") || p.Contains("css"));
                }
                return detectedPaths.Any(p => p.Contains("components") || p.Contains("pages") || p.Contains("views") || p.Contains("src") || p.EndsWith(".xaml"));

            case 4:
                if (lower.Contains("тест") || lower.Contains("test") || lower.Contains("unit") || lower.Contains("e2e"))
                {
                    return detectedPaths.Any(p => p.Contains("test") || p.Contains("spec") || p.Contains("e2e"));
                }
                if (lower.Contains("ci") || lower.Contains("deploy") || lower.Contains("docker") || lower.Contains("workflow"))
                {
                    return detectedPaths.Any(p => p.Contains(".github") || p.Contains("docker") || p.Contains("deploy") || p.Contains("workflows"));
                }
                if (lower.Contains("документ") || lower.Contains("readme") || lower.Contains("инструкц"))
                {
                    return detectedPaths.Any(p => p.Contains("readme") || p.Contains("antigravity.md") || p.Contains("docs"));
                }
                return detectedPaths.Any(p => p.Contains("test") || p.Contains(".github") || p.Contains("docker") || p.Contains("readme"));

            default:
                return false;
        }
    }
}
