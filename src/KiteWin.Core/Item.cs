using CommunityToolkit.Mvvm.ComponentModel;

namespace KiteWin.Core;

/// The app's main type. Rename it to the brief's main noun.
public partial class Item : ObservableObject
{
    public Item(string title = "")
    {
        Title = title;
        Notes = "";
    }

    /// A stable id for links, the agent CLI, and selection. Never the row number.
    public Guid Id { get; init; } = Guid.NewGuid();

    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.Now;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisplayTitle))]
    public partial string Title { get; set; }

    [ObservableProperty]
    public partial string Notes { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StarGlyph))]
    public partial bool IsStarred { get; set; }

    public string DisplayTitle => ItemTitle.Display(Title);

    /// Segoe Fluent Icons: FavoriteStarFill or FavoriteStar, as code points so they stay readable in source.
    public string StarGlyph => char.ConvertFromUtf32(IsStarred ? 0xE735 : 0xE734);

    /// What Narrator reads for a list row. Without it, every row is read as the type name.
    public override string ToString() => DisplayTitle;
}
