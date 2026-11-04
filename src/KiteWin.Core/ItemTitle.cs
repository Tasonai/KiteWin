namespace KiteWin.Core;

/// Rules for item titles. Pure functions, unit tested in tests/KiteWin.Core.Tests.
public static class ItemTitle
{
    /// Trims the title and collapses runs of whitespace to one space.
    public static string Clean(string raw) =>
        string.Join(' ', raw.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    /// What a row shows for an item with no title yet.
    public static string Display(string title) => string.IsNullOrWhiteSpace(title) ? "Untitled" : title;
}
