using System.Text;
using KiteWin.Core.Models;

namespace KiteWin.Core.Services;

public enum TopologySeverity
{
    Critical,
    Warning,
    Info
}

public record TopologyNodeDescriptor(
    string Id,
    string Title,
    string Role = "service"
);

public record TopologyIssue(
    string Id,
    TopologySeverity Severity,
    string Title,
    string Message,
    string Recommendation,
    IReadOnlyList<string> InvolvedNodeIds
);

public record TopologyReport(
    int HealthScore,
    IReadOnlyList<TopologyIssue> Issues,
    int TotalConnections,
    int TotalNodes
)
{
    public bool IsSecure => Issues.All(i => i.Severity != TopologySeverity.Critical);
}

public static class CanvasTopologyValidator
{
    public static string InferRole(string title)
    {
        var lower = (title ?? "").ToLowerInvariant();
        if (lower.Contains("db") || lower.Contains("database") || lower.Contains("postgres") ||
            lower.Contains("redis") || lower.Contains("mongo") || lower.Contains("sql") || lower.Contains("storage"))
        {
            return "database";
        }
        if (lower.Contains("ui") || lower.Contains("frontend") || lower.Contains("client") ||
            lower.Contains("mobile") || lower.Contains("web") || lower.Contains("app") || lower.Contains("view"))
        {
            return "client";
        }
        if (lower.Contains("gateway") || lower.Contains("proxy") || lower.Contains("ingress") || lower.Contains("router"))
        {
            return "gateway";
        }
        if (lower.Contains("queue") || lower.Contains("kafka") || lower.Contains("bus") || lower.Contains("event"))
        {
            return "eventbus";
        }
        return "service";
    }

    public static TopologyReport Validate(
        IReadOnlyList<TopologyNodeDescriptor> nodes,
        IReadOnlyList<CanvasConnection> connections)
    {
        var issues = new List<TopologyIssue>();
        var nodeMap = nodes.ToDictionary(n => n.Id, n => n);

        // 1. Direct Client-to-Database trap
        foreach (var conn in connections)
        {
            if (nodeMap.TryGetValue(conn.FromId, out var from) && nodeMap.TryGetValue(conn.ToId, out var to))
            {
                if (from.Role == "client" && to.Role == "database")
                {
                    issues.Add(new TopologyIssue(
                        Guid.NewGuid().ToString(),
                        TopologySeverity.Critical,
                        "Прямой доступ клиента к базе данных",
                        $"Компонент '{from.Title}' обращается напрямую к хранилищу '{to.Title}'.",
                        "Добавьте защищенный backend-сервис или API Gateway с обязательной JWT/Session проверкой.",
                        new[] { from.Id, to.Id }
                    ));
                }
            }
        }

        // 2. Missing Rate Limiter on public client endpoints
        foreach (var conn in connections)
        {
            if (nodeMap.TryGetValue(conn.FromId, out var from) && nodeMap.TryGetValue(conn.ToId, out var to))
            {
                if (from.Role == "client" && to.Role == "service")
                {
                    var lbl = (conn.Label ?? "").ToLowerInvariant();
                    if (!lbl.Contains("limit") && !lbl.Contains("guard") && !lbl.Contains("auth") && !lbl.Contains("gateway"))
                    {
                        issues.Add(new TopologyIssue(
                            Guid.NewGuid().ToString(),
                            TopologySeverity.Warning,
                            "Отсутствует Rate Limiting на публичном маршруте",
                            $"Запросы от '{from.Title}' к '{to.Title}' не защищены лимитированием частоты.",
                            "Настройте Upstash Redis или API Gateway Rate Limiter для защиты от DDoS.",
                            new[] { from.Id, to.Id }
                        ));
                    }
                }
            }
        }

        // 3. Cyclic Synchronous Dependencies (Deadlock Risk)
        var adj = new Dictionary<string, List<string>>();
        foreach (var conn in connections)
        {
            var lbl = (conn.Label ?? "").ToLowerInvariant();
            if (!lbl.Contains("event") && !lbl.Contains("queue") && !lbl.Contains("kafka"))
            {
                if (!adj.TryGetValue(conn.FromId, out var list))
                {
                    list = new List<string>();
                    adj[conn.FromId] = list;
                }
                list.Add(conn.ToId);
            }
        }

        var visited = new HashSet<string>();
        var recStack = new HashSet<string>();
        var cycleNodes = new List<string>();

        bool DfsCycle(string u)
        {
            visited.Add(u);
            recStack.Add(u);

            if (adj.TryGetValue(u, out var neighbors))
            {
                foreach (var v in neighbors)
                {
                    if (!visited.Contains(v))
                    {
                        if (DfsCycle(v)) return true;
                    }
                    else if (recStack.Contains(v))
                    {
                        cycleNodes.Add(u);
                        cycleNodes.Add(v);
                        return true;
                    }
                }
            }

            recStack.Remove(u);
            return false;
        }

        foreach (var n in nodes)
        {
            if (!visited.Contains(n.Id))
            {
                if (DfsCycle(n.Id))
                {
                    var names = string.Join(" -> ", cycleNodes.Where(id => nodeMap.ContainsKey(id)).Select(id => nodeMap[id].Title));
                    issues.Add(new TopologyIssue(
                        Guid.NewGuid().ToString(),
                        TopologySeverity.Critical,
                        "Синхронная циклическая зависимость",
                        $"Обнаружен цикл синхронных вызовов между сервисами: {names}.",
                        "Разорвите цикл, заменив один из синхронных RPC вызовов на асинхронный EventBus или фоновую очередь.",
                        cycleNodes
                    ));
                    break;
                }
            }
        }

        // 4. Isolated / Orphaned components (in systems with 3+ components)
        if (nodes.Count >= 3)
        {
            var connectedIds = new HashSet<string>();
            foreach (var c in connections)
            {
                connectedIds.Add(c.FromId);
                connectedIds.Add(c.ToId);
            }
            foreach (var n in nodes.Where(n => !connectedIds.Contains(n.Id)))
            {
                issues.Add(new TopologyIssue(
                    Guid.NewGuid().ToString(),
                    TopologySeverity.Info,
                    "Изолированный компонент",
                    $"Компонент '{n.Title}' не имеет активных связей на холсте.",
                    "Соедините компонент с целевым сервисом или удалите, если он не входит в MVP.",
                    new[] { n.Id }
                ));
            }
        }

        // Health Score calculation
        var deductions = 0;
        foreach (var issue in issues)
        {
            deductions += issue.Severity switch
            {
                TopologySeverity.Critical => 25,
                TopologySeverity.Warning => 10,
                TopologySeverity.Info => 5,
                _ => 0
            };
        }
        var score = Math.Max(0, Math.Min(100, 100 - deductions));

        return new TopologyReport(score, issues, connections.Count, nodes.Count);
    }

    public static string GenerateTopologyMarkdown(string title, TopologyReport report)
    {
        var cleanTitle = string.IsNullOrWhiteSpace(title) ? "System Architecture" : title.Trim();
        var sb = new StringBuilder();
        sb.AppendLine($"# ARCHITECTURE TOPOLOGY & AUDIT REPORT: {cleanTitle.ToUpperInvariant()}");
        sb.AppendLine("Generated by KiteWin Architecture Studio for Google DeepMind Antigravity.");
        sb.AppendLine();
        sb.AppendLine("## 1. Executive Summary");
        sb.AppendLine($"- Architecture Health Score: **{report.HealthScore}/100** ({(report.IsSecure ? "SECURE TOPOLOGY" : "CRITICAL REMEDIATION REQUIRED")})");
        sb.AppendLine($"- Total Components: {report.TotalNodes}");
        sb.AppendLine($"- Total Protocol Connections: {report.TotalConnections}");
        sb.AppendLine($"- Total Identified Issues: {report.Issues.Count}");
        sb.AppendLine();
        sb.AppendLine("## 2. Identified Vulnerabilities & Smells");

        if (report.Issues.Count == 0)
        {
            sb.AppendLine("No architectural smells detected. All components and protocols adhere to production security standards.");
        }
        else
        {
            var idx = 1;
            foreach (var issue in report.Issues)
            {
                var sevName = issue.Severity switch
                {
                    TopologySeverity.Critical => "CRITICAL",
                    TopologySeverity.Warning => "WARNING",
                    TopologySeverity.Info => "INFO",
                    _ => "NOTICE"
                };
                sb.AppendLine($"### {idx}. [{sevName}] {issue.Title}");
                sb.AppendLine($"- **Diagnosis**: {issue.Message}");
                sb.AppendLine($"- **Recommendation**: {issue.Recommendation}");
                sb.AppendLine();
                idx++;
            }
        }

        sb.AppendLine("## 3. Directives for Antigravity Vibe-Coding");
        sb.AppendLine("1. Never allow direct database access from frontend/client code.");
        sb.AppendLine("2. Implement edge rate limiting on all public API endpoints.");
        sb.AppendLine("3. Decouple synchronous circular service calls using event queues.");

        return sb.ToString();
    }
}
