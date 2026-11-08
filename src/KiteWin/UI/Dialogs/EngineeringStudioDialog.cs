using KiteWin.Core.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.ApplicationModel.DataTransfer;

namespace KiteWin.UI.Dialogs;

public sealed class EngineeringStudioDialog : ContentDialog
{
    private readonly StudioPayload _payload;
    private readonly TextBox _contentBox;
    private readonly ComboBox _viewSelector;

    public EngineeringStudioDialog(XamlRoot xamlRoot, StudioPayload payload)
    {
        XamlRoot = xamlRoot;
        _payload = payload;

        Title = payload.Title;
        PrimaryButtonText = "Скопировать текущий вид";
        SecondaryButtonText = "Скопировать все";
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
            Text = payload.Subtitle,
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
        for (int i = 0; i < payload.Views.Count; i++)
        {
            _viewSelector.Items.Add(new ComboBoxItem
            {
                Content = payload.Views[i].Name,
                Tag = i
            });
        }
        _viewSelector.SelectedIndex = 0;
        _viewSelector.SelectionChanged += OnSelectionChanged;
        Grid.SetRow(_viewSelector, 1);
        root.Children.Add(_viewSelector);

        // Content box
        _contentBox = new TextBox
        {
            AcceptsReturn = true,
            IsReadOnly = true,
            TextWrapping = TextWrapping.Wrap,
            FontFamily = new FontFamily("Cascadia Code, Consolas"),
            FontSize = 12,
            Text = payload.Views.Count > 0 ? payload.Views[0].Content : string.Empty
        };
        Grid.SetRow(_contentBox, 2);
        root.Children.Add(_contentBox);

        Content = root;

        PrimaryButtonClick += (s, e) =>
        {
            var dataPackage = new DataPackage();
            dataPackage.SetText(_contentBox.Text);
            Clipboard.SetContent(dataPackage);
        };

        SecondaryButtonClick += (s, e) =>
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"# {_payload.Title}");
            sb.AppendLine(_payload.Subtitle);
            sb.AppendLine();
            foreach (var view in _payload.Views)
            {
                sb.AppendLine($"## {view.Name}");
                sb.AppendLine(view.Content);
                sb.AppendLine();
            }
            var dataPackage = new DataPackage();
            dataPackage.SetText(sb.ToString());
            Clipboard.SetContent(dataPackage);
        };
    }

    private void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_viewSelector.SelectedItem is ComboBoxItem item && item.Tag is int index && index >= 0 && index < _payload.Views.Count)
        {
            _contentBox.Text = _payload.Views[index].Content;
        }
    }
}
