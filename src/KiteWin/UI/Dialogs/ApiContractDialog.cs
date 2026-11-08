using KiteWin.Core;
using KiteWin.Core.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.ApplicationModel.DataTransfer;

namespace KiteWin.UI.Dialogs;

public sealed class ApiContractDialog : ContentDialog
{
    private readonly Item _item;
    private readonly IReadOnlyList<ApiRoute> _routes;
    private readonly string _tsClient;
    private readonly string _markdownContract;
    private readonly string _curlSnippets;
    private readonly TextBox _contentBox;
    private readonly ComboBox _viewSelector;

    public ApiContractDialog(XamlRoot xamlRoot, Item item)
    {
        XamlRoot = xamlRoot;
        _item = item;
        _routes = ApiContractBuilder.StarterPack;
        _tsClient = ApiContractBuilder.GenerateTypeScriptClient(_routes);
        _markdownContract = ApiContractBuilder.GenerateMarkdownContract(item.Title, _routes);
        _curlSnippets = ApiContractBuilder.GenerateCurlSnippets(_routes);

        Title = "API Контракт и TypeScript SDK";
        PrimaryButtonText = "Скопировать TS-клиент";
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

        // Header summary
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
            Text = "Типизированный HTTP-клиент, контракт эндпоинтов и готовые cURL вызовы",
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
        _viewSelector.Items.Add(new ComboBoxItem { Content = "TypeScript клиент (api/client.ts)", Tag = "ts" });
        _viewSelector.Items.Add(new ComboBoxItem { Content = "Спецификация API (API_CONTRACT.md)", Tag = "contract" });
        _viewSelector.Items.Add(new ComboBoxItem { Content = "Интерактивные сниппеты cURL", Tag = "curl" });
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
            Text = _tsClient
        };

        Grid.SetRow(_contentBox, 2);
        root.Children.Add(_contentBox);

        Content = root;

        PrimaryButtonClick += (s, e) => CopyToClipboard(_tsClient);
        SecondaryButtonClick += (s, e) => CopyToClipboard(_contentBox.Text);
    }

    private void OnViewSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_viewSelector.SelectedItem is ComboBoxItem item && item.Tag is string tag)
        {
            _contentBox.Text = tag switch
            {
                "contract" => _markdownContract,
                "curl" => _curlSnippets,
                _ => _tsClient
            };
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
