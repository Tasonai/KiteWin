using System.Diagnostics;
using System.Reflection;
using KiteWin.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Windows.UI.ViewManagement;

namespace KiteWin.Pages;

public sealed partial class SettingsPage : Page
{
    private bool _loading = true;

    public SettingsPage()
    {
        InitializeComponent();

        foreach (var (id, name) in Theme.Modes) ThemeBox.Items.Add(new ComboBoxItem { Content = name, Tag = id });
        ThemeBox.SelectedIndex = Math.Max(0, Array.FindIndex(Theme.Modes, m => m.Id == Prefs.Current.Theme));

        var windowsAccent = new UISettings().GetColorValue(UIColorType.Accent);
        foreach (var accent in Theme.All)
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = Space.Sm };
            row.Children.Add(new Ellipse
            {
                Width = Space.Lg,
                Height = Space.Lg,
                VerticalAlignment = VerticalAlignment.Center,
                Fill = new SolidColorBrush(accent.Base ?? windowsAccent),
            });
            row.Children.Add(new TextBlock { Text = accent.Name, VerticalAlignment = VerticalAlignment.Center });
            AccentBox.Items.Add(new ComboBoxItem { Content = row, Tag = accent.Id });
        }
        AccentBox.SelectedIndex = Math.Max(0, Array.FindIndex(Theme.All, a => a.Id == Prefs.Current.Accent));

        var version = Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        AboutCard.Description = $"Version {version?.Split('+')[0]}. Your data stays on this PC.";
        _loading = false;
    }

    private void OnThemeChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading || ThemeBox.SelectedItem is not ComboBoxItem { Tag: string id }) return;
        Prefs.Current.Theme = id;
        if (!AppPaths.IsOverridden) Prefs.Current.Save();
        App.MainWindow.ApplyTheme(id);
    }

    private void OnAccentChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading || AccentBox.SelectedItem is not ComboBoxItem { Tag: string id }) return;
        Prefs.Current.Accent = id;
        if (!AppPaths.IsOverridden) Prefs.Current.Save();
        Theme.ApplyAccent(id);
        App.MainWindow.ReapplyAccent();
    }

    private void OnOpenDataFolder(object sender, RoutedEventArgs e) =>
        Process.Start(new ProcessStartInfo("explorer.exe", System.IO.Path.GetDirectoryName(AppPaths.Store)!) { UseShellExecute = true });
}
