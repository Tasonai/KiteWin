using KiteWin.Core.Models;

namespace KiteWin.Core;

public sealed record IngestedArchitecture(
    string ProjectType,
    string Name,
    IReadOnlyList<CanvasFrame> Frames,
    IReadOnlyList<CanvasSticky> Stickies,
    IReadOnlyList<CanvasConnection> Connections);

public static class CodebaseIngestion
{
    public static IngestedArchitecture Analyze(string folderPath)
    {
        var folderName = Path.GetFileName(folderPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        if (string.IsNullOrEmpty(folderName)) folderName = "Project";

        var projectType = "Generic Project";
        var uiNodes = new List<string>();
        var serviceNodes = new List<string>();
        var dataNodes = new List<string>();

        if (Directory.Exists(folderPath))
        {
            if (File.Exists(Path.Combine(folderPath, "Package.swift")))
            {
                projectType = "Swift SPM Package";
            }
            else if (Directory.GetFiles(folderPath, "*.csproj").Length > 0 || Directory.GetFiles(folderPath, "*.sln").Length > 0)
            {
                projectType = ".NET 10 / WinUI 3";
            }
            else if (File.Exists(Path.Combine(folderPath, "package.json")))
            {
                projectType = "TypeScript / Modern Web";
            }

            var entries = Directory.GetFileSystemEntries(folderPath);
            foreach (var entry in entries)
            {
                var name = Path.GetFileName(entry);
                var lower = name.ToLowerInvariant();

                if (lower.Contains("view") || lower.Contains("page") || lower.Contains("ui") || lower.Contains("component"))
                {
                    uiNodes.Add(name);
                }
                else if (lower.Contains("service") || lower.Contains("sync") || lower.Contains("controller") || lower.Contains("api") || lower.Contains("kit"))
                {
                    serviceNodes.Add(name);
                }
                else if (lower.Contains("model") || lower.Contains("data") || lower.Contains("store") || lower.Contains("db"))
                {
                    dataNodes.Add(name);
                }
            }
        }

        if (uiNodes.Count == 0) uiNodes.AddRange(["Views & Pages", "Design Tokens", "Navigation HUD"]);
        if (serviceNodes.Count == 0) serviceNodes.AddRange(["P2P Sync Server", "AI Generator Service", "REST Client"]);
        if (dataNodes.Count == 0) dataNodes.AddRange(["Local Store (WAL)", "Cache Index", "Schema Models"]);

        var frameUI = new CanvasFrame
        {
            Title = $"UI & Presentation ({projectType})",
            ColorHex = "#3B82F6",
            X = 100, Y = 100, Width = 340, Height = 420
        };
        var frameService = new CanvasFrame
        {
            Title = "Domain Services & P2P",
            ColorHex = "#10B981",
            X = 500, Y = 100, Width = 340, Height = 420
        };
        var frameData = new CanvasFrame
        {
            Title = "Data Persistence & State",
            ColorHex = "#8B5CF6",
            X = 900, Y = 100, Width = 340, Height = 420
        };

        var stickies = new List<CanvasSticky>();
        var connections = new List<CanvasConnection>();

        string lastUIId = "";
        for (var i = 0; i < Math.Min(3, uiNodes.Count); i++)
        {
            var id = Guid.NewGuid().ToString();
            stickies.Add(new CanvasSticky
            {
                Id = id,
                Text = $"UI: {uiNodes[i]}",
                ColorHex = "#DBEAFE",
                X = 120,
                Y = 160 + (i * 110)
            });
            lastUIId = id;
        }

        string lastServiceId = "";
        for (var i = 0; i < Math.Min(3, serviceNodes.Count); i++)
        {
            var id = Guid.NewGuid().ToString();
            stickies.Add(new CanvasSticky
            {
                Id = id,
                Text = $"Service: {serviceNodes[i]}",
                ColorHex = "#D1FAE5",
                X = 520,
                Y = 160 + (i * 110)
            });
            lastServiceId = id;
        }

        string lastDataId = "";
        for (var i = 0; i < Math.Min(3, dataNodes.Count); i++)
        {
            var id = Guid.NewGuid().ToString();
            stickies.Add(new CanvasSticky
            {
                Id = id,
                Text = $"Storage: {dataNodes[i]}",
                ColorHex = "#EDE9FE",
                X = 920,
                Y = 160 + (i * 110)
            });
            lastDataId = id;
        }

        if (!string.IsNullOrEmpty(lastUIId) && !string.IsNullOrEmpty(lastServiceId))
        {
            connections.Add(new CanvasConnection { FromId = lastUIId, ToId = lastServiceId, Label = "Calls", ColorHex = "#3B82F6" });
        }
        if (!string.IsNullOrEmpty(lastServiceId) && !string.IsNullOrEmpty(lastDataId))
        {
            connections.Add(new CanvasConnection { FromId = lastServiceId, ToId = lastDataId, Label = "Persists", ColorHex = "#10B981" });
        }

        return new IngestedArchitecture(
            projectType,
            folderName,
            [frameUI, frameService, frameData],
            stickies,
            connections
        );
    }
}
