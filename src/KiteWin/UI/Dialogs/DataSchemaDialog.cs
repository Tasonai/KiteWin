using KiteWin.Core;
using KiteWin.Core.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.ApplicationModel.DataTransfer;

namespace KiteWin.UI.Dialogs;

public sealed class DataSchemaDialog : ContentDialog
{
    private readonly Item _item;
    private readonly IReadOnlyList<SchemaEntity> _entities;
    private readonly string _ddl;
    private readonly string _prisma;
    private readonly string _indexing;
    private readonly TextBox _contentBox;
    private readonly ComboBox _viewSelector;

    public DataSchemaDialog(XamlRoot xamlRoot, Item item)
    {
        XamlRoot = xamlRoot;
        _item = item;
        _entities = DataSchemaDesigner.StarterPack;
        _ddl = DataSchemaDesigner.GeneratePostgreSqlDdl(_entities);
        _prisma = DataSchemaDesigner.GeneratePrismaSchema(_entities);
        _indexing = DataSchemaDesigner.GenerateCompositeIndexes(item.Title, _entities);

        Title = "Схема данных и индексы (PostgreSQL / Prisma)";
        PrimaryButtonText = "Скопировать DDL";
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
            Text = "Строгая реляционная схема с первичными ключами UUID v4 и B-Tree индексами",
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
        _viewSelector.Items.Add(new ComboBoxItem { Content = "PostgreSQL DDL (schema.sql)", Tag = "ddl" });
        _viewSelector.Items.Add(new ComboBoxItem { Content = "Prisma ORM (schema.prisma)", Tag = "prisma" });
        _viewSelector.Items.Add(new ComboBoxItem { Content = "Составные индексы БД (INDEXING.md)", Tag = "indexing" });
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
            Text = _ddl
        };

        Grid.SetRow(_contentBox, 2);
        root.Children.Add(_contentBox);

        Content = root;

        PrimaryButtonClick += (s, e) => CopyToClipboard(_ddl);
        SecondaryButtonClick += (s, e) => CopyToClipboard(_contentBox.Text);
    }

    private void OnViewSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_viewSelector.SelectedItem is ComboBoxItem item && item.Tag is string tag)
        {
            _contentBox.Text = tag switch
            {
                "prisma" => _prisma,
                "indexing" => _indexing,
                _ => _ddl
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
