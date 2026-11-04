using KiteWin.Core;
using Xunit;

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
}

public class ItemStoreTests : IDisposable
{
    // Every test gets its own file in %TEMP%. Never the real database.
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"KiteWin-test-{Guid.NewGuid():N}.db");

    [Fact]
    public void SavedItemsSurviveReopening()
    {
        var item = new Item("Renew passport") { IsStarred = true, Notes = "Before June" };
        using (var store = new ItemStore(_path)) store.Save(item);

        using var reopened = new ItemStore(_path);
        var loaded = Assert.Single(reopened.All());
        Assert.Equal(item.Id, loaded.Id);
        Assert.Equal("Renew passport", loaded.Title);
        Assert.True(loaded.IsStarred);
        Assert.Equal("Before June", loaded.Notes);
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
