using KiteWin.Core;
using KiteWin.Core.Services;
using KiteWin.UI.Dialogs;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.ApplicationModel.DataTransfer;

namespace KiteWin.Pages;

public sealed partial class ItemsPage : Page
{
    public ItemsPage()
    {
        InitializeComponent();
    }

    public AppModel Model => ((App)Application.Current).Model;

    protected override void OnNavigatedTo(Microsoft.UI.Xaml.Navigation.NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        PageTitle.Text = Model.Page.Title();
        UpdateSelectionUi();
    }

    private void UpdateSelectionUi()
    {
        var item = Model.Selection;
        var hasSelection = item is not null;
        Detail.Visibility = hasSelection ? Visibility.Visible : Visibility.Collapsed;
        NoSelection.Visibility = hasSelection ? Visibility.Collapsed : Visibility.Visible;
        DeleteButton.IsEnabled = hasSelection;
        StarButton.IsEnabled = hasSelection;

        if (item is not null)
        {
            // Sync status combo
            for (var i = 0; i < StatusCombo.Items.Count; i++)
            {
                if (StatusCombo.Items[i] is ComboBoxItem cbi && (string)cbi.Tag == item.Status)
                {
                    StatusCombo.SelectedIndex = i;
                    break;
                }
            }

            // Sync platform combo
            for (var i = 0; i < PlatformCombo.Items.Count; i++)
            {
                if (PlatformCombo.Items[i] is ComboBoxItem cbi && (string)cbi.Content == item.Platform)
                {
                    PlatformCombo.SelectedIndex = i;
                    break;
                }
            }

            // Sync style combo
            for (var i = 0; i < StyleCombo.Items.Count; i++)
            {
                if (StyleCombo.Items[i] is ComboBoxItem cbi && (string)cbi.Content == item.Style)
                {
                    StyleCombo.SelectedIndex = i;
                    break;
                }
            }
        }
    }

    private void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (List.SelectedItem is Item item)
        {
            Model.Selection = item;
        }
        UpdateSelectionUi();
    }

    private void OnNew(object sender, RoutedEventArgs e)
    {
        var item = Model.AddItem("Новый проект", "Опишите здесь ключевой сценарий...");
        List.SelectedItem = item;
        TitleBox.Focus(FocusState.Programmatic);
        TitleBox.SelectAll();
    }

    private void OnQuickCapture(object sender, RoutedEventArgs e)
    {
        App.MainWindow.QuickCapture.ShowAndFocus();
    }

    private void OnDelete(object sender, RoutedEventArgs e)
    {
        if (Model.Selection is { } item) Model.Delete(item);
        UpdateSelectionUi();
    }

    private void OnStar(object sender, RoutedEventArgs e)
    {
        if (Model.Selection is { } item) item.IsStarred = !item.IsStarred;
    }

    private void OnDeleteKey(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        if (Model.Selection is { } item)
        {
            Model.Delete(item);
            args.Handled = true;
        }
    }

    private void OnStarRow(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: Item item }) item.IsStarred = !item.IsStarred;
    }

    private void OnDeleteRow(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: Item item }) Model.Delete(item);
        UpdateSelectionUi();
    }

    private void OnStatusChanged(object sender, SelectionChangedEventArgs e)
    {
        if (Model.Selection is { } item && StatusCombo.SelectedItem is ComboBoxItem cbi && cbi.Tag is string tag)
        {
            item.Status = tag;
        }
    }

    private void OnPlatformChanged(object sender, SelectionChangedEventArgs e)
    {
        if (Model.Selection is { } item && PlatformCombo.SelectedItem is ComboBoxItem cbi && cbi.Content is string plat)
        {
            item.Platform = plat;
        }
    }

    private void OnStyleChanged(object sender, SelectionChangedEventArgs e)
    {
        if (Model.Selection is { } item && StyleCombo.SelectedItem is ComboBoxItem cbi && cbi.Content is string style)
        {
            item.Style = style;
        }
    }

    private async void OnGenerateAiSpec(object sender, RoutedEventArgs e)
    {
        if (Model.Selection is not { } item) return;

        GenerateAiBtn.IsEnabled = false;
        AiProgressRing.IsActive = true;
        AiProgressRing.Visibility = Visibility.Visible;
        AiIcon.Visibility = Visibility.Collapsed;
        AiBtnText.Text = "Генерация...";

        try
        {
            var provider = Prefs.Current.AiProvider.ToLowerInvariant() switch
            {
                "openai" or "openai compatible" => AiProvider.OpenAI,
                "ollama" or "local ollama" => AiProvider.Ollama,
                "offline" or "local blueprint (offline)" => AiProvider.Offline,
                _ => AiProvider.Gemini
            };

            var settings = new AiSettings
            {
                Provider = provider,
                GeminiApiKey = Prefs.Current.GeminiApiKey,
                GeminiModel = Prefs.Current.GeminiModel,
                OpenAIEndpoint = Prefs.Current.OpenAIEndpoint,
                OpenAIApiKey = Prefs.Current.OpenAIApiKey,
                OpenAIModel = Prefs.Current.OpenAIModel,
                OllamaEndpoint = Prefs.Current.OllamaEndpoint,
                OllamaModel = Prefs.Current.OllamaModel
            };

            using var aiService = new AiService();
            var spec = await aiService.GenerateSpecificationAsync(item, settings);
            item.GeneratedPrompt = spec;
            item.StitchPrompt = spec;
            Model.Store.Save(item);

            ShowBanner("AI-спецификация успешно сформирована!", InfoBarSeverity.Success);
        }
        catch (Exception ex)
        {
            ShowBanner($"Ошибка генерации: {ex.Message}", InfoBarSeverity.Error);
        }
        finally
        {
            GenerateAiBtn.IsEnabled = true;
            AiProgressRing.IsActive = false;
            AiProgressRing.Visibility = Visibility.Collapsed;
            AiIcon.Visibility = Visibility.Visible;
            AiBtnText.Text = "Сгенерировать AI-спецификацию";
        }
    }

    private void OnCompilePrompt(object sender, RoutedEventArgs e)
    {
        if (Model.Selection is not { } item) return;
        var prompt = ItemTitle.CompileStitchPrompt(item);
        item.GeneratedPrompt = prompt;
        item.StitchPrompt = prompt;
        Model.Store.Save(item);

        ShowBanner("Промпт скомпилирован по стандарту Google Stitch (3-частный лэйаут, дизайн-система).", InfoBarSeverity.Success);
    }

    private void OnCopyPrompt(object sender, RoutedEventArgs e)
    {
        if (Model.Selection is not { } item || string.IsNullOrWhiteSpace(item.GeneratedPrompt)) return;
        var package = new DataPackage();
        package.SetText(item.GeneratedPrompt);
        Clipboard.SetContent(package);

        ShowBanner("Промпт скопирован в буфер обмена.", InfoBarSeverity.Informational);
    }

    private async void OnSyncQuick(object sender, RoutedEventArgs e)
    {
        ShowBanner("Запуск P2P-синхронизации с Kite macOS...", InfoBarSeverity.Informational);
        var result = await Model.TriggerSyncAsync();
        ShowBanner(result.Message, result.Success ? InfoBarSeverity.Success : InfoBarSeverity.Error);
    }

    private void OnExportStitch(object sender, RoutedEventArgs e)
    {
        if (Model.Selection is not { } item) return;
        var md = DesignExporter.GenerateStitchDesignMd(item);
        var package = new DataPackage();
        package.SetText(md);
        Clipboard.SetContent(package);
        ShowBanner("Спецификация Google Stitch (DESIGN.md) скопирована в буфер обмена.", InfoBarSeverity.Success);
    }

    private void OnExportFigma(object sender, RoutedEventArgs e)
    {
        if (Model.Selection is not { } item) return;
        var json = DesignExporter.GenerateFigmaTokensJson(item);
        var package = new DataPackage();
        package.SetText(json);
        Clipboard.SetContent(package);
        ShowBanner("Дизайн-токены Figma (W3C JSON) скопированы в буфер обмена.", InfoBarSeverity.Success);
    }

    private void OnExportFigJam(object sender, RoutedEventArgs e)
    {
        if (Model.Selection is not { } item) return;
        var diag = DesignExporter.GenerateFigJamDiagram(item);
        var package = new DataPackage();
        package.SetText(diag);
        Clipboard.SetContent(package);
        ShowBanner("Схема Mermaid для FigJam скопирована в буфер обмена.", InfoBarSeverity.Success);
    }

    private async void OnOpenAntigravityPack(object sender, RoutedEventArgs e)
    {
        if (Model.Selection is not { } item) return;
        var dialog = new AntigravityPackDialog(XamlRoot, item);
        await dialog.ShowAsync();
    }

    private async void OnOpenModelRouter(object sender, RoutedEventArgs e)
    {
        if (Model.Selection is not { } item) return;
        var dialog = new ModelRouterDialog(XamlRoot, item);
        await dialog.ShowAsync();
    }

    private async void OnOpenDataSchema(object sender, RoutedEventArgs e)
    {
        if (Model.Selection is not { } item) return;
        var dialog = new DataSchemaDialog(XamlRoot, item);
        await dialog.ShowAsync();
    }

    private async void OnOpenApiContract(object sender, RoutedEventArgs e)
    {
        if (Model.Selection is not { } item) return;
        var dialog = new ApiContractDialog(XamlRoot, item);
        await dialog.ShowAsync();
    }

    private async void OnOpenTopologyValidator(object sender, RoutedEventArgs e)
    {
        if (Model.Selection is not { } item) return;
        var dialog = new TopologyValidatorDialog(XamlRoot, item);
        await dialog.ShowAsync();
    }

    private void ShowBanner(string text, InfoBarSeverity severity)
    {
        StatusBanner.Message = text;
        StatusBanner.Severity = severity;
        StatusBanner.IsOpen = true;
    }
}
