using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;

namespace KiteWin.Core;

/// <summary>
/// Core model for architectural ideas and specifications.
/// Matches Kite macOS data contract for seamless P2P synchronization.
/// </summary>
public partial class Item : ObservableObject
{
    public Item(string title = "")
    {
        Title = title;
        Concept = "";
        Notes = "";
        Platform = "Web";
        Style = "Editorial & Swiss Modern";
        Status = "backlog";
        Tags = "";
        GeneratedPrompt = "";
        StitchPrompt = "";
        TargetedEditPrompt = "";
        CreatedAt = DateTimeOffset.Now;
        UpdatedAt = DateTimeOffset.Now;
    }

    /// <summary>
    /// Stable UUID matching macOS UUID string and web client ID.
    /// </summary>
    public Guid Id { get; init; } = Guid.NewGuid();

    public DateTimeOffset CreatedAt { get; init; }

    [ObservableProperty]
    public partial DateTimeOffset UpdatedAt { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisplayTitle))]
    public partial string Title { get; set; }

    [ObservableProperty]
    public partial string Concept { get; set; }

    [ObservableProperty]
    public partial string Notes { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PlatformGlyph))]
    public partial string Platform { get; set; }

    [ObservableProperty]
    public partial string Style { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusGlyph))]
    [NotifyPropertyChangedFor(nameof(StatusLabel))]
    public partial string Status { get; set; }

    [ObservableProperty]
    public partial string Tags { get; set; }

    [ObservableProperty]
    public partial string GeneratedPrompt { get; set; }

    [ObservableProperty]
    public partial string StitchPrompt { get; set; }

    [ObservableProperty]
    public partial string TargetedEditPrompt { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StarGlyph))]
    public partial bool IsStarred { get; set; }

    public string DisplayTitle => string.IsNullOrWhiteSpace(Title) ? "Безымянная идея" : Title.Trim();

    /// <summary>
    /// Segoe Fluent Icons: FavoriteStarFill (0xE735) or FavoriteStar (0xE734).
    /// </summary>
    public string StarGlyph => char.ConvertFromUtf32(IsStarred ? 0xE735 : 0xE734);

    /// <summary>
    /// Segoe Fluent Icons: Checkmark (0xE73E), LightningBolt (0xE945), or Pin (0xE718).
    /// Zero emojis.
    /// </summary>
    public string StatusGlyph => Status switch
    {
        "completed" => char.ConvertFromUtf32(0xE73E),
        "in_progress" => char.ConvertFromUtf32(0xE945),
        _ => char.ConvertFromUtf32(0xE718)
    };

    public string StatusLabel => Status switch
    {
        "completed" => "Готово",
        "in_progress" => "В разработке",
        _ => "В планах"
    };

    /// <summary>
    /// Segoe Fluent Icons: Globe (0xE774), CellPhone (0xE8EA), or Devices (0xE7F4).
    /// </summary>
    public string PlatformGlyph => Platform?.ToLowerInvariant() switch
    {
        "mobile" => char.ConvertFromUtf32(0xE8EA),
        "desktop" => char.ConvertFromUtf32(0xE7F4),
        _ => char.ConvertFromUtf32(0xE774)
    };

    public override string ToString() => DisplayTitle;
}
