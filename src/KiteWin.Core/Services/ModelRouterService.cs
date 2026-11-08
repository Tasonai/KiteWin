using System.Text;

namespace KiteWin.Core.Services;

public record ModelRouteRule(
    string TaskType,
    string PrimaryModel,
    string FallbackModel,
    int MaxTokens,
    double Temperature,
    int TargetLatencyMs
);

public static class ModelRouter
{
    public static IReadOnlyList<ModelRouteRule> StarterRoutes => new[]
    {
        new ModelRouteRule(
            "Fast Autocomplete & Micro-edits",
            "gemini-3.8-flash",
            "gemini-3.5-flash",
            2048,
            0.1,
            250
        ),
        new ModelRouteRule(
            "Architectural Reasoning & Specs",
            "gemini-3.8-pro",
            "gemini-3.8-flash",
            8192,
            0.2,
            800
        ),
        new ModelRouteRule(
            "Deep Audits & Frontier Verification",
            "gemini-3.8-ultra",
            "gemini-3.8-pro",
            16384,
            0.0,
            2000
        )
    };

    public static string GenerateRoutingMarkdown(string title, IReadOnlyList<ModelRouteRule> routes)
    {
        var cleanTitle = string.IsNullOrWhiteSpace(title) ? "Project" : title.Trim();
        var sb = new StringBuilder();
        sb.AppendLine($"# MULTI-MODEL ORCHESTRATION & LLM ROUTER: {cleanTitle.ToUpperInvariant()}");
        sb.AppendLine("Configured for the Google DeepMind Gemini 3.8 model family.");
        sb.AppendLine("This specification guides Google DeepMind Antigravity in intelligently routing AI calls by task complexity.");
        sb.AppendLine();

        var idx = 1;
        foreach (var r in routes)
        {
            sb.AppendLine($"## {idx}. {r.TaskType}");
            sb.AppendLine($"- Primary Model: `{r.PrimaryModel}`");
            sb.AppendLine($"- Fallback Chain: `{r.FallbackModel}`");
            sb.AppendLine($"- Token Budget: {r.MaxTokens} tokens");
            sb.AppendLine($"- Temperature: {r.Temperature:F1}");
            sb.AppendLine($"- SLA Target Latency: < {r.TargetLatencyMs}ms");
            sb.AppendLine();
            idx++;
        }

        sb.AppendLine("## Gemini 3.8 Selection Principles");
        sb.AppendLine("- `gemini-3.8-flash`: Default for high-frequency interactive canvas updates, micro-edits, and token-budget conscious loops.");
        sb.AppendLine("- `gemini-3.8-pro`: Complex multi-file planning, state machine synthesis, and full-stack architecture specs.");
        sb.AppendLine("- `gemini-3.8-ultra`: Mathematical proofs, deep security auditing, and comprehensive adversarial verification.");
        sb.AppendLine();
        sb.AppendLine("## Automatic Fallback Protocol");
        sb.AppendLine("1. If Primary Model encounters rate limits (429) or transient 503 errors, retry once after 500ms jittered backoff.");
        sb.AppendLine("2. If the retry fails, instantly switch to Fallback Model with logged telemetry.");
        sb.AppendLine("3. Preserve structured schema constraints (JSON Schema / Zod) identically across both models.");

        return sb.ToString();
    }

    public static string GenerateTypeScriptRouter(IReadOnlyList<ModelRouteRule> routes)
    {
        var sb = new StringBuilder();
        sb.AppendLine("// lib/modelRouter.ts");
        sb.AppendLine("// High-performance LLM Router for Gemini 3.8 and Antigravity Workflows");
        sb.AppendLine();
        sb.AppendLine("export type TaskComplexity = 'fast' | 'reasoning' | 'audit';");
        sb.AppendLine();
        sb.AppendLine("export interface ModelRouteConfig {");
        sb.AppendLine("  model: string;");
        sb.AppendLine("  fallback: string;");
        sb.AppendLine("  maxTokens: number;");
        sb.AppendLine("  temperature: number;");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("export const ROUTE_MATRIX: Record<TaskComplexity, ModelRouteConfig> = {");

        var mapping = new[] { "fast", "reasoning", "audit" };
        for (var i = 0; i < Math.Min(mapping.Length, routes.Count); i++)
        {
            var key = mapping[i];
            var r = routes[i];
            sb.AppendLine($"  {key}: {{");
            sb.AppendLine($"    model: '{r.PrimaryModel}',");
            sb.AppendLine($"    fallback: '{r.FallbackModel}',");
            sb.AppendLine($"    maxTokens: {r.MaxTokens},");
            sb.AppendLine($"    temperature: {r.Temperature:F1},");
            sb.AppendLine("  },");
        }

        sb.AppendLine("};");
        sb.AppendLine();
        sb.AppendLine("export async function callModelWithRouting<T>(");
        sb.AppendLine("  complexity: TaskComplexity,");
        sb.AppendLine("  prompt: string,");
        sb.AppendLine("  caller: (model: string, temp: number, maxTokens: number) => Promise<T>");
        sb.AppendLine("): Promise<T> {");
        sb.AppendLine("  const cfg = ROUTE_MATRIX[complexity];");
        sb.AppendLine("  try {");
        sb.AppendLine("    return await caller(cfg.model, cfg.temperature, cfg.maxTokens);");
        sb.AppendLine("  } catch (error) {");
        sb.AppendLine("    console.warn(`[ModelRouter] Primary model ${cfg.model} failed, switching to fallback ${cfg.fallback}:`, error);");
        sb.AppendLine("    return await caller(cfg.fallback, cfg.temperature, cfg.maxTokens);");
        sb.AppendLine("  }");
        sb.AppendLine("}");

        return sb.ToString();
    }
}
