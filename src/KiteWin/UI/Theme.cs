using Microsoft.UI;
using Microsoft.UI.Xaml;
using Windows.UI;

namespace KiteWin.UI;

/// One accent choice. Base is null for "Windows default", which follows the accent
/// the person picked in Windows Settings > Personalization > Colors.
public sealed record AccentChoice(string Id, string Name, Color? Base);

/// The accent and the app theme. Views never hold a color: they use theme resources
/// (AccentFillColorDefaultBrush, AccentTextFillColorPrimaryBrush, TextOnAccentFillColorPrimaryBrush),
/// which this file feeds.
public static class Theme
{
    public const string DefaultId = "system";

    public static readonly AccentChoice[] All =
    [
        new("system", "Windows default", null),
        new("blue", "Blue", Hex(0x0067C0)),
        new("purple", "Purple", Hex(0x8764B8)),
        new("pink", "Pink", Hex(0xBF0077)),
        new("red", "Red", Hex(0xC42B1C)),
        new("orange", "Orange", Hex(0xCA5010)),
        new("green", "Green", Hex(0x0F7B0F)),
        new("teal", "Teal", Hex(0x038387)),
        new("graphite", "Graphite", Hex(0x5D5A58)),
    ];

    public static readonly (string Id, string Name)[] Modes =
        [("system", "Use system setting"), ("light", "Light"), ("dark", "Dark")];

    /// Overrides the seven SystemAccentColor shades WinUI derives every accent brush from.
    /// Light mode fills with Dark1 and writes text with Dark2; dark mode fills with Light2
    /// and writes with Light3. Call before the first window, or follow with Reapply.
    public static void ApplyAccent(string id)
    {
        var resources = Application.Current.Resources;
        var choice = All.FirstOrDefault(a => a.Id == id);
        string[] keys = ["SystemAccentColor", "SystemAccentColorLight1", "SystemAccentColorLight2", "SystemAccentColorLight3",
                         "SystemAccentColorDark1", "SystemAccentColorDark2", "SystemAccentColorDark3"];
        if (choice?.Base is not Color c)
        {
            // Remove throws on a key that is not there, and an exception this early kills the app.
            foreach (var key in keys) if (resources.ContainsKey(key)) resources.Remove(key);
            return;
        }
        resources["SystemAccentColor"] = c;
        resources["SystemAccentColorLight1"] = Mix(c, Colors.White, 0.25);
        resources["SystemAccentColorLight2"] = Mix(c, Colors.White, 0.5);
        resources["SystemAccentColorLight3"] = Mix(c, Colors.White, 0.7);
        resources["SystemAccentColorDark1"] = Mix(c, Colors.Black, 0.15);
        resources["SystemAccentColorDark2"] = Mix(c, Colors.Black, 0.35);
        resources["SystemAccentColorDark3"] = Mix(c, Colors.Black, 0.55);
    }

    public static ElementTheme Element(string mode) => mode switch
    {
        "light" => ElementTheme.Light,
        "dark" => ElementTheme.Dark,
        _ => ElementTheme.Default,
    };

    /// Theme resources resolve when an element loads. Flipping the theme makes every
    /// element resolve them again, so a new accent shows at once.
    public static void Reapply(FrameworkElement root)
    {
        var wanted = root.RequestedTheme;
        root.RequestedTheme = root.ActualTheme == ElementTheme.Dark ? ElementTheme.Light : ElementTheme.Dark;
        root.RequestedTheme = wanted;
    }

    private static Color Hex(uint rgb) => Color.FromArgb(255, (byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb);

    private static Color Mix(Color a, Color b, double t) => Color.FromArgb(255,
        (byte)Math.Round(a.R + (b.R - a.R) * t),
        (byte)Math.Round(a.G + (b.G - a.G) * t),
        (byte)Math.Round(a.B + (b.B - a.B) * t));
}
