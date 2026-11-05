using System.Diagnostics;
using System.Reflection;
using KiteWin.Core.Services;
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

        // Theme options
        foreach (var (id, name) in Theme.Modes) ThemeBox.Items.Add(new ComboBoxItem { Content = name, Tag = id });
        ThemeBox.SelectedIndex = Math.Max(0, Array.FindIndex(Theme.Modes, m => m.Id == Prefs.Current.Theme));

        // Accent options
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

        // Sync settings
        MacEndpointBox.Text = Prefs.Current.MacEndpoint;
        AutoSyncSwitch.IsOn = Prefs.Current.AutoSync;

        // AI Provider
        GeminiApiKeyBox.Password = Prefs.Current.GeminiApiKey;
        var providerIndex = Prefs.Current.AiProvider switch
        {
            "ollama" => 1,
            "offline" => 2,
            _ => 0
        };
        AiProviderBox.SelectedIndex = providerIndex;

        GeminiModelBox.SelectedIndex = Prefs.Current.GeminiModel switch
        {
            "gemini-3.1-flash-lite" => 1,
            "gemini-flash-latest" => 2,
            _ => 0
        };
        InterviewToggleSwitch.IsOn = Prefs.Current.TranslatorInterview;
        OllamaEndpointBox.Text = Prefs.Current.OllamaEndpoint;

        // Version & Info
        var version = Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        AboutCard.Description = $"Версия {version?.Split('+')[0] ?? "1.0.0"}. Все данные хранятся локально в SQLite WAL.";
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
        App.MainWindow.ReapplyAccent();
    }

    private void OnMacEndpointChanged(object sender, TextChangedEventArgs e)
    {
        if (_loading) return;
        Prefs.Current.MacEndpoint = MacEndpointBox.Text.Trim();
        if (!AppPaths.IsOverridden) Prefs.Current.Save();
        App.MainWindow.Model.MacEndpoint = Prefs.Current.MacEndpoint;
    }

    private void OnAutoSyncChanged(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        Prefs.Current.AutoSync = AutoSyncSwitch.IsOn;
        if (!AppPaths.IsOverridden) Prefs.Current.Save();
    }

    private void OnAiProviderChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading || AiProviderBox.SelectedItem is not ComboBoxItem item) return;
        Prefs.Current.AiProvider = item.Tag as string ?? "gemini";
        if (!AppPaths.IsOverridden) Prefs.Current.Save();
    }

    private void OnGeminiKeyChanged(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        Prefs.Current.GeminiApiKey = GeminiApiKeyBox.Password.Trim();
        if (!AppPaths.IsOverridden) Prefs.Current.Save();
    }

    private void OnGeminiModelChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading || GeminiModelBox.SelectedItem is not ComboBoxItem item) return;
        Prefs.Current.GeminiModel = item.Tag as string ?? "gemini-3.5-flash";
        if (!AppPaths.IsOverridden) Prefs.Current.Save();
    }

    private void OnInterviewToggled(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        Prefs.Current.TranslatorInterview = InterviewToggleSwitch.IsOn;
        if (!AppPaths.IsOverridden) Prefs.Current.Save();
    }

    private void OnOllamaEndpointChanged(object sender, TextChangedEventArgs e)
    {
        if (_loading) return;
        Prefs.Current.OllamaEndpoint = OllamaEndpointBox.Text.Trim();
        if (!AppPaths.IsOverridden) Prefs.Current.Save();
    }

    private async void OnTestAiConnection(object sender, RoutedEventArgs e)
    {
        TestAiBtn.IsEnabled = false;
        AiTestStatusText.Text = "Проверка...";
        AiTestStatusText.Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"];

        try
        {
            var provider = Prefs.Current.AiProvider switch
            {
                "ollama" => AiProvider.Ollama,
                "offline" => AiProvider.Offline,
                _ => AiProvider.Gemini
            };

            var settings = new AiSettings
            {
                Provider = provider,
                GeminiApiKey = Prefs.Current.GeminiApiKey,
                GeminiModel = Prefs.Current.GeminiModel,
                OllamaEndpoint = Prefs.Current.OllamaEndpoint,
                OllamaModel = Prefs.Current.OllamaModel
            };

            using var aiService = new AiService();
            var success = await aiService.TestConnectionAsync(settings);

            if (success)
            {
                AiTestStatusText.Text = "Связь установлена (OK)";
                AiTestStatusText.Foreground = (Brush)Application.Current.Resources["SystemFillColorSuccessBrush"];
            }
            else
            {
                AiTestStatusText.Text = "Не удалось подключиться";
                AiTestStatusText.Foreground = (Brush)Application.Current.Resources["SystemFillColorCriticalBrush"];
            }
        }
        catch (Exception ex)
        {
            AiTestStatusText.Text = $"Ошибка: {ex.Message}";
            AiTestStatusText.Foreground = (Brush)Application.Current.Resources["SystemFillColorCriticalBrush"];
        }
        finally
        {
            TestAiBtn.IsEnabled = true;
        }
    }

    private void OnOpenDataFolder(object sender, RoutedEventArgs e)
    {
        Process.Start(new ProcessStartInfo("explorer.exe", AppPaths.Folder) { UseShellExecute = true });
    }
}
