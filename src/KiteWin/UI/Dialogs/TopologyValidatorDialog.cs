using KiteWin.Core.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.ApplicationModel.DataTransfer;

namespace KiteWin.UI.Dialogs;

public sealed class TopologyValidatorDialog : ContentDialog
{
    private readonly TopologyReport _report;
    private readonly string _markdownContent;
    private readonly TextBox _contentBox;

    public TopologyValidatorDialog(XamlRoot xamlRoot, string title, TopologyReport report)
    {
        XamlRoot = xamlRoot;
        _report = report;
        _markdownContent = CanvasTopologyValidator.GenerateTopologyMarkdown(title, report);

        Title = "Валидатор топологии архитектуры";
        PrimaryButtonText = "Скопировать TOPOLOGY.md";
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

        // Header score banner
        var statusBadge = new Border
        {
            Background = Application.Current.Resources["CardBackgroundFillColorDefaultBrush"] as Brush,
            BorderBrush = Application.Current.Resources["CardStrokeColorDefaultBrush"] as Brush,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(12, 8, 12, 8)
        };
        var statusText = _report.IsSecure ? "SECURE TOPOLOGY" : "CRITICAL REMEDIATION REQUIRED";
        statusBadge.Child = new TextBlock
        {
            Text = $"Индекс здоровья архитектуры: {_report.HealthScore}/100 ({statusText})",
            FontSize = 12
        };
        Grid.SetRow(statusBadge, 0);
        root.Children.Add(statusBadge);

        // Metrics strip
        var metricsPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 12
        };

        metricsPanel.Children.Add(CreateMetricPill("Узлов", _report.TotalNodes.ToString()));
        metricsPanel.Children.Add(CreateMetricPill("Связей", _report.TotalConnections.ToString()));
        metricsPanel.Children.Add(CreateMetricPill("Замечаний", _report.Issues.Count.ToString()));

        Grid.SetRow(metricsPanel, 1);
        root.Children.Add(metricsPanel);

        // Report Preview
        _contentBox = new TextBox
        {
            AcceptsReturn = true,
            IsReadOnly = true,
            TextWrapping = TextWrapping.Wrap,
            FontFamily = new FontFamily("Cascadia Code, Consolas"),
            FontSize = 12,
            Text = _markdownContent
        };

        Grid.SetRow(_contentBox, 2);
        root.Children.Add(_contentBox);

        Content = root;

        PrimaryButtonClick += (s, e) => CopyToClipboard(_markdownContent);
    }

    private static Border CreateMetricPill(string label, string val)
    {
        var border = new Border
        {
            Background = Application.Current.Resources["CardBackgroundFillColorDefaultBrush"] as Brush,
            BorderBrush = Application.Current.Resources["CardStrokeColorDefaultBrush"] as Brush,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(8, 4, 8, 4)
        };
        border.Child = new TextBlock
        {
            Text = $"{label}: {val}",
            FontSize = 12
        };
        return border;
    }

    private static void CopyToClipboard(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return;
        var package = new DataPackage();
        package.SetText(text);
        Clipboard.SetContent(package);
    }
}
