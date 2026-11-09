using System.Text.RegularExpressions;

namespace KiteWin.Core;

public enum ViolationSeverity
{
    Warning,
    Error
}

public sealed class ArchitectureRule
{
    public string Name { get; set; } = string.Empty;
    public string SourceLayer { get; set; } = string.Empty;
    public List<string> ForbiddenTargets { get; set; } = [];
    public ViolationSeverity Severity { get; set; } = ViolationSeverity.Error;
    public string Description { get; set; } = string.Empty;

    public ArchitectureRule() { }

    public ArchitectureRule(string name, string sourceLayer, List<string> forbiddenTargets, ViolationSeverity severity, string description)
    {
        Name = name;
        SourceLayer = sourceLayer;
        ForbiddenTargets = forbiddenTargets;
        Severity = severity;
        Description = description;
    }
}

public sealed class DriftViolation
{
    public string Id { get; set; } = string.Empty;
    public string RuleName { get; set; } = string.Empty;
    public string SourceFile { get; set; } = string.Empty;
    public string SourceLayer { get; set; } = string.Empty;
    public string ImportedModule { get; set; } = string.Empty;
    public string TargetLayer { get; set; } = string.Empty;
    public ViolationSeverity Severity { get; set; } = ViolationSeverity.Error;
    public string Message { get; set; } = string.Empty;

    public DriftViolation() { }

    public DriftViolation(string ruleName, string sourceFile, string sourceLayer, string importedModule, string targetLayer, ViolationSeverity severity, string message)
    {
        Id = $"{sourceFile}->{importedModule}";
        RuleName = ruleName;
        SourceFile = sourceFile;
        SourceLayer = sourceLayer;
        ImportedModule = importedModule;
        TargetLayer = targetLayer;
        Severity = severity;
        Message = message;
    }
}

public static class ArchitecturalDriftGuard
{
    public static List<ArchitectureRule> DefaultRules =>
    [
        new ArchitectureRule(
            "PresentationToDataBypass",
            "Presentation",
            ["Data", "Persistence", "Storage"],
            ViolationSeverity.Error,
            "Presentation layer components must access data via Domain/Services and cannot import database or persistence layers directly."
        ),
        new ArchitectureRule(
            "DomainReverseDependency",
            "Domain",
            ["Presentation", "UI", "Infrastructure"],
            ViolationSeverity.Error,
            "Domain logic must remain pure and cannot depend on UI, presentation, or external driver infrastructure."
        ),
        new ArchitectureRule(
            "DataReverseDependency",
            "Data",
            ["Presentation", "UI"],
            ViolationSeverity.Error,
            "Data layer must not hold references or dependencies to presentation views or UI components."
        )
    ];

    public static string InferLayer(string filePath)
    {
        var normalized = filePath.ToLowerInvariant();
        var clean = "/" + normalized.Replace('\\', '/').Trim('/', '.') + "/";

        bool Matches(string[] keywords)
        {
            foreach (var kw in keywords)
            {
                if (clean.Contains($"/{kw}/") || clean.Contains($"/{kw}") || clean.Contains($"{kw}/") || normalized == kw || normalized.Contains($".{kw}"))
                {
                    return true;
                }
            }
            return false;
        }

        if (Matches(["views", "pages", "ui", "components", "controllers", "viewmodels"]))
            return "Presentation";
        if (Matches(["domain", "services", "models", "usecases", "core"]))
            return "Domain";
        if (Matches(["data", "repositories", "storage", "persistence", "db", "database"]))
            return "Data";
        if (Matches(["infrastructure", "network", "drivers", "gateways"]))
            return "Infrastructure";

        return "Common";
    }

    public static List<string> ExtractImports(string sourceCode)
    {
        var imports = new HashSet<string>();
        var lines = sourceCode.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);

        foreach (var rawLine in lines)
        {
            var line = rawLine.Trim();

            // C#: using Namespace;
            if (line.StartsWith("using ") && line.EndsWith(';'))
            {
                var trimmed = line[6..^1].Trim();
                if (!string.IsNullOrEmpty(trimmed) && !trimmed.Contains('='))
                {
                    imports.Add(trimmed);
                }
            }
            // Swift: import Module
            else if (line.StartsWith("import "))
            {
                var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 2)
                {
                    var mod = parts[1].TrimEnd(';', ',');
                    if (!string.IsNullOrEmpty(mod)) imports.Add(mod);
                }
            }
            // TypeScript/JS: from 'module' or require('module')
            else if (line.Contains("from \"") || line.Contains("from '"))
            {
                var quoteSplit = line.Split(['\"', '\''], StringSplitOptions.RemoveEmptyEntries);
                if (quoteSplit.Length >= 2)
                {
                    var mod = quoteSplit[1];
                    if (!string.IsNullOrEmpty(mod)) imports.Add(mod);
                }
            }
            // Python: from x import y OR import x
            else if (line.StartsWith("from "))
            {
                var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 2 && !string.IsNullOrEmpty(parts[1]))
                {
                    imports.Add(parts[1]);
                }
            }
        }

        var sortedList = imports.ToList();
        sortedList.Sort();
        return sortedList;
    }

    public static List<DriftViolation> Evaluate(
        Dictionary<string, string> files,
        List<ArchitectureRule>? rules = null)
    {
        var activeRules = rules ?? DefaultRules;
        var violations = new List<DriftViolation>();

        foreach (var (filePath, content) in files)
        {
            var sourceLayer = InferLayer(filePath);
            if (sourceLayer == "Common") continue;

            var importedModules = ExtractImports(content);

            foreach (var module in importedModules)
            {
                var targetLayer = InferLayer(module);
                if (targetLayer == "Common" || targetLayer == sourceLayer) continue;

                foreach (var rule in activeRules.Where(r => r.SourceLayer == sourceLayer))
                {
                    if (rule.ForbiddenTargets.Contains(targetLayer))
                    {
                        violations.Add(new DriftViolation(
                            rule.Name,
                            filePath,
                            sourceLayer,
                            module,
                            targetLayer,
                            rule.Severity,
                            $"Architecture Drift: [{sourceLayer}] '{filePath}' forbidden from importing [{targetLayer}] '{module}'. {rule.Description}"
                        ));
                    }
                }
            }
        }

        return violations;
    }

    public static string GenerateMermaidTopology(List<DriftViolation>? violations = null)
    {
        var lines = new List<string>
        {
            "graph TD",
            "    subgraph Presentation [Presentation Layer]",
            "        Views[Views and UI Components]",
            "        Controllers[Controllers and ViewModels]",
            "    end",
            "    subgraph Domain [Domain Layer]",
            "        Services[Application Services]",
            "        Entities[Domain Entities and UseCases]",
            "    end",
            "    subgraph Data [Data Layer]",
            "        Repos[Repositories and Storage]",
            "        Database[Database and Persistence]",
            "    end",
            "    subgraph Infrastructure [Infrastructure Layer]",
            "        Network[Network and Gateways]",
            "        Drivers[External Drivers]",
            "    end",
            "",
            "    %% Permitted architectural flow",
            "    Presentation --> Domain",
            "    Domain --> Repos",
            "    Repos --> Database",
            "    Infrastructure --> Domain"
        };

        if (violations != null && violations.Count > 0)
        {
            lines.Add("");
            lines.Add("    %% Architectural Drift Violations Detected");
            foreach (var v in violations.Take(5))
            {
                lines.Add($"    {v.SourceLayer} -.-|\"VIOLATION: {v.ImportedModule}\"| {v.TargetLayer}");
                lines.Add($"    style {v.SourceLayer} stroke:#e74c3c,stroke-width:2px");
                lines.Add($"    style {v.TargetLayer} stroke:#e74c3c,stroke-width:2px");
            }
        }

        return string.Join("\n", lines);
    }
}
