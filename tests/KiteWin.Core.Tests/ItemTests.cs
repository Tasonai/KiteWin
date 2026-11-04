using System.Text.RegularExpressions;
using KiteWin.Core;
using Xunit;

namespace KiteWin.Core.Tests;

public class ItemTitleTests
{
    [Theory]
    [InlineData("  Buy   milk ", "Buy milk")]
    [InlineData("Call\tAnna", "Call Anna")]
    [InlineData("", "")]
    public void CleansWhitespace(string raw, string expected) => Assert.Equal(expected, ItemTitle.Clean(raw));

    [Fact]
    public void EmptyTitleDisplaysUntitled() => Assert.Equal("Untitled", new Item().DisplayTitle);

    // Narrator reads a list row through ToString. A type name there is a screen reader bug.
    [Fact]
    public void ScreenReadersHearTheTitle() => Assert.Equal("Renew passport", new Item("Renew passport").ToString());

    [Fact]
    public void CompileStitchPrompt_ContainsThreePartStructure()
    {
        var item = new Item("Kite AI Studio")
        {
            Concept = "Студия для дизайнеров интерфейсов и архитекторов промптов",
            Platform = "Web",
            Style = "Claude Paper"
        };

        var prompt = ItemTitle.CompileStitchPrompt(item);

        Assert.Contains("Atmosphere Header", prompt);
        Assert.Contains("**DESIGN SYSTEM (REQUIRED):**", prompt);
        Assert.Contains("**Page Structure:**", prompt);
        Assert.Contains("Claude Paper", prompt);
    }

    [Fact]
    public void CompileTargetedEditPrompt_ContainsSurgicalDirectives()
    {
        var item = new Item("Analytics Card");
        var prompt = ItemTitle.CompileTargetedEditPrompt(item, "MetricValue counter", "Make font tabular-nums and add subtle pulse");

        Assert.Contains("Surgical modification request", prompt);
        Assert.Contains("MetricValue counter", prompt);
        Assert.Contains("Make font tabular-nums", prompt);
        Assert.Contains("CRITICAL CONSTRAINTS", prompt);
    }

    [Fact]
    public void DesignExporter_OutputsValidStitchMd()
    {
        var item = new Item("Design Tokens")
        {
            Concept = "Swiss minimal design tokens",
            Platform = "Desktop",
            Style = "Codex Minimal"
        };

        var md = DesignExporter.ToStitchDesignMd(item);
        Assert.Contains("# DESIGN.md", md);
        Assert.Contains("Color Palette", md);
        Assert.Contains("Typography Tokens", md);
    }

    [Fact]
    public void DesignExporter_OutputsValidFigmaJson()
    {
        var item = new Item("Design Tokens")
        {
            Style = "Terracotta Journal"
        };

        var json = DesignExporter.ToFigmaTokensJson(item);
        Assert.Contains("\"version\": \"1.0.0\"", json);
        Assert.Contains("\"color\":", json);
        Assert.Contains("\"space\":", json);
    }

    [Fact]
    public void DesignExporter_OutputsValidMermaidFlowchart()
    {
        var item = new Item("Flowchart Idea");
        var mermaid = DesignExporter.ToFigJamMermaid(item);
        Assert.Contains("flowchart TD", mermaid);
    }

    [Fact]
    public void AllCompiledPrompts_ZeroEmojis()
    {
        var item = new Item("Zero Emoji Test")
        {
            Concept = "Checking for absolute zero emojis",
            Style = "Risograph Print"
        };

        var stitch = ItemTitle.CompileStitchPrompt(item);
        var plan = ItemTitle.CompilePlanPrompt(item);
        var targeted = ItemTitle.CompileTargetedEditPrompt(item, "header", "change color");
        var brief = ItemTitle.CompileConcisePrompt(item);
        var actions = ItemTitle.CompileActionPrompt(item);

        var emojiRegex = new Regex(@"[\uD83C-\uDBFF\uDC00-\uDFFF]|[\u2600-\u27BF]", RegexOptions.Compiled);

        Assert.DoesNotMatch(emojiRegex, stitch);
        Assert.DoesNotMatch(emojiRegex, plan);
        Assert.DoesNotMatch(emojiRegex, targeted);
        Assert.DoesNotMatch(emojiRegex, brief);
        Assert.DoesNotMatch(emojiRegex, actions);
    }
}

public class ItemStoreTests : IDisposable
{
    // Every test gets its own file in %TEMP%. Never the real database.
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"KiteWin-test-{Guid.NewGuid():N}.db");

    [Fact]
    public void SavedItemsSurviveReopening()
    {
        var item = new Item("Renew passport")
        {
            IsStarred = true,
            Notes = "Before June",
            Concept = "Check with embassy",
            Platform = "Desktop",
            Style = "Editorial & Swiss Modern",
            Status = "in_progress",
            Tags = ["travel", "official"]
        };

        using (var store = new ItemStore(_path)) store.Save(item);

        using var reopened = new ItemStore(_path);
        var loaded = Assert.Single(reopened.All());
        Assert.Equal(item.Id, loaded.Id);
        Assert.Equal("Renew passport", loaded.Title);
        Assert.True(loaded.IsStarred);
        Assert.Equal("Before June", loaded.Notes);
        Assert.Equal("Check with embassy", loaded.Concept);
        Assert.Equal("Desktop", loaded.Platform);
        Assert.Equal("Editorial & Swiss Modern", loaded.Style);
        Assert.Equal("in_progress", loaded.Status);
        Assert.Contains("travel", loaded.Tags);
    }

    [Fact]
    public void SaveTwiceUpdatesInPlace()
    {
        using var store = new ItemStore(_path);
        var item = new Item("Draft");
        store.Save(item);
        item.Title = "Final";
        store.Save(item);
        Assert.Equal("Final", Assert.Single(store.All()).Title);
    }

    [Fact]
    public void DeleteRemovesTheItem()
    {
        using var store = new ItemStore(_path);
        var item = new Item("Groceries");
        store.Save(item);
        store.Delete(item.Id);
        Assert.Equal(0, store.Count());
    }

    public void Dispose()
    {
        foreach (var suffix in new[] { "", "-wal", "-shm" }) File.Delete(_path + suffix);
    }
}
