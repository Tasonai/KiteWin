using KiteWin.Core.Models;
using KiteWin.Core.Services;
using Xunit;

namespace KiteWin.Core.Tests;

public class EngineeringServiceTests
{
    [Fact]
    public void AntigravityReadinessLinter_ScoresCorrectly()
    {
        var report = AntigravityReadinessLinter.Evaluate(
            "Kite Studio",
            "Студия архитектуры и генерации спецификаций для вайбкодинга",
            "Web (Next.js 15)",
            "Apple HIG Monochrome"
        );

        Assert.True(report.Score >= 75);
        Assert.Equal(4, report.Items.Count);
        Assert.All(report.Items, i => Assert.False(string.IsNullOrWhiteSpace(i.Title)));
    }

    [Fact]
    public void AntigravityPackGenerator_BuildsFourProgressiveWaves()
    {
        var waves = AntigravityPackGenerator.BuildWaves(
            "Kite Studio",
            "Полнофункциональный редактор",
            "Windows 11",
            "Monochrome"
        );

        Assert.Equal(4, waves.Count);
        Assert.Equal(1, waves[0].Id);
        Assert.Equal(2, waves[1].Id);
        Assert.Equal(3, waves[2].Id);
        Assert.Equal(4, waves[3].Id);

        Assert.Contains("WAVE 1", waves[0].Prompt);
        Assert.Contains("WAVE 2", waves[1].Prompt);
        Assert.Contains("WAVE 3", waves[2].Prompt);
        Assert.Contains("WAVE 4", waves[3].Prompt);
    }

    [Fact]
    public void AntigravityPackGenerator_GeneratesValidMarkdown()
    {
        var md = AntigravityPackGenerator.GenerateAntigravityMd(
            "Fintech App",
            "Безопасный кошелек с поддержкой карт",
            "Mobile",
            "Apple Minimal"
        );

        Assert.Contains("# ANTIGRAVITY EXECUTION PLAN: FINTECH APP", md);
        Assert.Contains("Flight Readiness Score", md);
        Assert.Contains("WAVE 1", md);
        Assert.Contains("WAVE 4", md);
    }

    [Fact]
    public void ModelRouter_GeneratesTypeScriptAndMarkdown()
    {
        var routes = ModelRouter.StarterRoutes;
        Assert.NotEmpty(routes);
        Assert.Contains(routes, r => r.PrimaryModel.Contains("gemini-3.8"));

        var md = ModelRouter.GenerateRoutingMarkdown("AI Platform", routes);
        Assert.Contains("gemini-3.8-flash", md);
        Assert.Contains("gemini-3.8-pro", md);
        Assert.Contains("gemini-3.8-ultra", md);

        var ts = ModelRouter.GenerateTypeScriptRouter(routes);
        Assert.Contains("ROUTE_MATRIX", ts);
        Assert.Contains("callModelWithRouting", ts);
    }

    [Fact]
    public void DataSchemaDesigner_GeneratesDdlAndIndexes()
    {
        var entities = DataSchemaDesigner.StarterPack;
        Assert.NotEmpty(entities);

        var ddl = DataSchemaDesigner.GeneratePostgreSqlDdl(entities);
        Assert.Contains("CREATE TABLE IF NOT EXISTS users", ddl);
        Assert.Contains("CREATE TABLE IF NOT EXISTS projects", ddl);

        var prisma = DataSchemaDesigner.GeneratePrismaSchema(entities);
        Assert.Contains("model User", prisma);
        Assert.Contains("model Project", prisma);

        var idx = DataSchemaDesigner.GenerateCompositeIndexes("TestApp", entities);
        Assert.Contains("CREATE INDEX CONCURRENTLY", idx);
    }

    [Fact]
    public void ApiContractBuilder_GeneratesTypedClientAndCurl()
    {
        var routes = ApiContractBuilder.StarterPack;
        Assert.NotEmpty(routes);

        var ts = ApiContractBuilder.GenerateTypeScriptClient(routes);
        Assert.Contains("export const api =", ts);
        Assert.Contains("/api/projects", ts);

        var curl = ApiContractBuilder.GenerateCurlSnippets(routes);
        Assert.Contains("curl -s -X GET", curl);
        Assert.Contains("curl -s -X POST", curl);
    }

    [Fact]
    public void CanvasTopologyValidator_DetectsDirectDbAccessAndCycles()
    {
        var nodes = new[]
        {
            new TopologyNodeDescriptor("ui", "Web Client UI", "client"),
            new TopologyNodeDescriptor("db", "Postgres Storage", "database"),
            new TopologyNodeDescriptor("svc_a", "Service A", "service"),
            new TopologyNodeDescriptor("svc_b", "Service B", "service")
        };

        var conns = new[]
        {
            new CanvasConnection { FromId = "ui", ToId = "db", Label = "Direct SQL" },
            new CanvasConnection { FromId = "svc_a", ToId = "svc_b", Label = "REST" },
            new CanvasConnection { FromId = "svc_b", ToId = "svc_a", Label = "REST" }
        };

        var report = CanvasTopologyValidator.Validate(nodes, conns);
        Assert.False(report.IsSecure);
        Assert.Contains(report.Issues, i => i.Severity == TopologySeverity.Critical);

        var md = CanvasTopologyValidator.GenerateTopologyMarkdown("Test System", report);
        Assert.Contains("ARCHITECTURE TOPOLOGY & AUDIT REPORT", md);
        Assert.Contains("CRITICAL", md);
    }
}
