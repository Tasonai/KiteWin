using KiteWin.Core;
using KiteWin.Core.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.ApplicationModel.DataTransfer;

namespace KiteWin.UI.Dialogs;

public sealed class AntigravityPackDialog : ContentDialog
{
    private readonly Item _item;
    private readonly IReadOnlyList<AntigravityWave> _waves;
    private readonly AntigravityReadinessReport _readiness;
    private readonly string _fullMarkdown;
    private readonly TextBox _contentBox;
    private readonly ComboBox _viewSelector;

    public AntigravityPackDialog(XamlRoot xamlRoot, Item item)
    {
        XamlRoot = xamlRoot;
        _item = item;
        _waves = AntigravityPackGenerator.BuildWaves(item.Title, item.Concept, item.Platform, item.Style);
        _readiness = AntigravityReadinessLinter.Evaluate(item.Title, item.Concept, item.Platform, item.Style);
        _fullMarkdown = AntigravityPackGenerator.GenerateAntigravityMd(item.Title, item.Concept, item.Platform, item.Style);

        Title = "Пакет для Antigravity";
        PrimaryButtonText = "Скопировать ANTIGRAVITY.md";
        SecondaryButtonText = "Скопировать текущий вид";
        CloseButtonText = "Закрыть";
        DefaultButton = ContentDialogButton.Primary;

        var root = new Grid
        {
            Width = 720,
            Height = 520,
            RowSpacing = 12
        };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        // Header info banner
        var headerPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 12
        };

        var flightStatus = _readiness.IsFlightReady ? "FLIGHT READY" : "REFINEMENT REQUIRED";
        var statusBadge = new Border
        {
            Background = Application.Current.Resources["CardBackgroundFillColorDefaultBrush"] as Brush,
            BorderBrush = Application.Current.Resources["CardStrokeColorDefaultBrush"] as Brush,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(8, 4, 8, 4)
        };
        statusBadge.Child = new TextBlock
        {
            Text = $"Готовность к полету: {_readiness.Score}% ({flightStatus})",
            FontSize = 12
        };
        headerPanel.Children.Add(statusBadge);

        var watcherBtn = new Button
        {
            Content = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 8,
                Children =
                {
                    new FontIcon { Glyph = "\uE9D2", FontSize = 12 },
                    new TextBlock { Text = "Live Watcher (Авто-прогресс)" }
                }
            }
        };
        watcherBtn.Click += OnLiveWatcherClicked;
        headerPanel.Children.Add(watcherBtn);

        Grid.SetRow(headerPanel, 0);
        root.Children.Add(headerPanel);

        // Wave / Document Selector
        _viewSelector = new ComboBox
        {
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        _viewSelector.Items.Add(new ComboBoxItem { Content = "Полный манифест (ANTIGRAVITY.md)", Tag = "full" });
        _viewSelector.Items.Add(new ComboBoxItem { Content = "Волна 1: Схема и Модели данных", Tag = "1" });
        _viewSelector.Items.Add(new ComboBoxItem { Content = "Волна 2: Бизнес-логика и Сервисы", Tag = "2" });
        _viewSelector.Items.Add(new ComboBoxItem { Content = "Волна 3: Верстка и Токены интерфейса", Tag = "3" });
        _viewSelector.Items.Add(new ComboBoxItem { Content = "Волна 4: Тесты, a11y и Quality Gate", Tag = "4" });
        _viewSelector.SelectedIndex = 0;
        _viewSelector.SelectionChanged += OnViewSelectionChanged;

        Grid.SetRow(_viewSelector, 1);
        root.Children.Add(_viewSelector);

        // Code Viewer
        _contentBox = new TextBox
        {
            AcceptsReturn = true,
            IsReadOnly = true,
            TextWrapping = TextWrapping.Wrap,
            FontFamily = new FontFamily("Cascadia Code, Consolas"),
            FontSize = 12,
            Text = _fullMarkdown
        };

        Grid.SetRow(_contentBox, 2);
        root.Children.Add(_contentBox);

        Content = root;

        PrimaryButtonClick += (s, e) => CopyToClipboard(_fullMarkdown);
        SecondaryButtonClick += (s, e) => CopyToClipboard(_contentBox.Text);
    }

    private void OnViewSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_viewSelector.SelectedItem is ComboBoxItem item && item.Tag is string tag)
        {
            if (tag == "full")
            {
                _contentBox.Text = _fullMarkdown;
            }
            else if (int.TryParse(tag, out var waveId) && waveId >= 1 && waveId <= _waves.Count)
            {
                _contentBox.Text = _waves[waveId - 1].Prompt;
            }
        }
    }

    private void OnLiveWatcherClicked(object sender, RoutedEventArgs e)
    {
        var targetDir = AppDomain.CurrentDomain.BaseDirectory;
        var snapshot = ProjectWatcherService.AnalyzeDirectory(targetDir, _waves);
        _contentBox.Text = $"=== LIVE WATCHER: ПРОГРЕСС ПРОЕКТА ===\n" +
            $"Отслеживаемый путь: {snapshot.WatchedPath}\n" +
            $"Общий прогресс: {snapshot.OverallPercentage}%\n" +
            $"Волна 1 (Схема и Модели): {snapshot.WavePercentages[1]}%\n" +
            $"Волна 2 (Сервисы и API): {snapshot.WavePercentages[2]}%\n" +
            $"Волна 3 (UI Компоненты): {snapshot.WavePercentages[3]}%\n" +
            $"Волна 4 (Тесты и CI/CD): {snapshot.WavePercentages[4]}%\n\n" +
            $"Обнаруженные артефакты ({snapshot.DetectedArtifacts.Count}):\n" +
            string.Join("\n", snapshot.DetectedArtifacts.Select(a => $"[{a.Category}] {a.RelativePath}")) + "\n\n" +
            $"Выполненные задачи ({snapshot.CompletedTaskKeys.Count}):\n" +
            string.Join("\n", snapshot.CompletedTaskKeys.Select(k => $"[x] {k}"));
    }

    private static void CopyToClipboard(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return;
        var package = new DataPackage();
        package.SetText(text);
        Clipboard.SetContent(package);
    }
}
