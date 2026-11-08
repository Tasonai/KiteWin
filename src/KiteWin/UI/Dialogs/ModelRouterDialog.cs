using KiteWin.Core;
using KiteWin.Core.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.ApplicationModel.DataTransfer;

namespace KiteWin.UI.Dialogs;

public sealed class ModelRouterDialog : ContentDialog
{
    private readonly Item _item;
    private readonly IReadOnlyList<ModelRouteRule> _routes;
    private readonly string _markdownSpec;
    private readonly string _tsCode;
    private readonly TextBox _contentBox;
    private readonly ComboBox _viewSelector;

    public ModelRouterDialog(XamlRoot xamlRoot, Item item)
    {
        XamlRoot = xamlRoot;
        _item = item;
        _routes = ModelRouter.StarterRoutes;
        _markdownSpec = ModelRouter.GenerateRoutingMarkdown(item.Title, _routes);
        _tsCode = ModelRouter.GenerateTypeScriptRouter(_routes);

        Title = "LLM Маршрутизатор (Gemini 3.8)";
        PrimaryButtonText = "Скопировать TS-клиент";
        SecondaryButtonText = "Скопировать Markdown";
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

        // Header matrix summary
        var summaryBorder = new Border
        {
            Background = Application.Current.Resources["CardBackgroundFillColorDefaultBrush"] as Brush,
            BorderBrush = Application.Current.Resources["CardStrokeColorDefaultBrush"] as Brush,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(12, 8, 12, 8)
        };
        var summaryText = new TextBlock
        {
            Text = "Маршрутизация Gemini 3.8: Flash (микро-правки) | Pro (архитектура) | Ultra (аудит безопасности)",
            FontSize = 12
        };
        summaryBorder.Child = summaryText;
        Grid.SetRow(summaryBorder, 0);
        root.Children.Add(summaryBorder);

        // Selector
        _viewSelector = new ComboBox
        {
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        _viewSelector.Items.Add(new ComboBoxItem { Content = "TypeScript клиент (lib/modelRouter.ts)", Tag = "ts" });
        _viewSelector.Items.Add(new ComboBoxItem { Content = "Спецификация маршрутизации (MODEL_ROUTING.md)", Tag = "md" });
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
            Text = _tsCode
        };

        Grid.SetRow(_contentBox, 2);
        root.Children.Add(_contentBox);

        Content = root;

        PrimaryButtonClick += (s, e) => CopyToClipboard(_tsCode);
        SecondaryButtonClick += (s, e) => CopyToClipboard(_markdownSpec);
    }

    private void OnViewSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_viewSelector.SelectedItem is ComboBoxItem item && item.Tag is string tag)
        {
            _contentBox.Text = tag == "ts" ? _tsCode : _markdownSpec;
        }
    }

    private static void CopyToClipboard(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return;
        var package = new DataPackage();
        package.SetText(text);
        Clipboard.SetContent(package);
    }
}
