using KiteWin.Core;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.DataTransfer;

namespace KiteWin.Pages;

public sealed partial class PromptTranslatorPage : Page
{
    private string _activeMode = "stitch";

    public PromptTranslatorPage()
    {
        InitializeComponent();
        InputTitleBox.Text = "SaaS Analytics & DevTool Cockpit";
        InputConceptBox.Text = "Производительный SaaS дашборд с метриками, Cmd+K командной строкой, фильтрами в URL и защитой от падений.";
        Compile();
    }

    public AppModel Model => ((App)Application.Current).Model;

    private void OnModeChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ModeSegmented?.SelectedItem is SegmentedItem item && item.Tag is string tag)
        {
            _activeMode = tag;
            TargetedEditPanel.Visibility = _activeMode == "targeted" ? Visibility.Visible : Visibility.Collapsed;
            Compile();
        }
    }

    private void OnInputChanged(object sender, object e)
    {
        UpdateDensityGauge();
    }

    private void OnCompileClick(object sender, RoutedEventArgs e)
    {
        Compile();
        ShowBanner("Промпт успешно скомпилирован по выбранному стандарту.", InfoBarSeverity.Success);
    }

    private void Compile()
    {
        var item = new Item(InputTitleBox?.Text ?? "Новая идея")
        {
            Concept = InputConceptBox?.Text ?? "",
            Platform = (PlatformBox?.SelectedItem as ComboBoxItem)?.Content as string ?? "Web",
            Style = (StyleBox?.SelectedItem as ComboBoxItem)?.Content as string ?? "Editorial & Swiss Modern"
        };

        var output = _activeMode switch
        {
            "plan" => ItemTitle.CompilePlanPrompt(item),
            "targeted" => ItemTitle.CompileTargetedEditPrompt(item, TargetElementBox?.Text ?? "", TargetScopeBox?.Text ?? ""),
            "brief" => ItemTitle.CompileConcisePrompt(item),
            "actions" => ItemTitle.CompileActionPrompt(item),
            _ => ItemTitle.CompileStitchPrompt(item) // default: stitch
        };

        if (OutputTextBox != null)
        {
            OutputTextBox.Text = output;
        }

        UpdateDensityGauge();
    }

    private void UpdateDensityGauge()
    {
        if (QualityBar == null || QualityText == null) return;
        var text = (InputTitleBox?.Text ?? "") + " " + (InputConceptBox?.Text ?? "");
        var len = text.Trim().Length;

        // Base 50, +25 if >50 chars, +15 if >100 chars, +10 for design tokens
        var score = 50;
        if (len > 30) score += 20;
        if (len > 80) score += 15;
        if (len > 150) score += 10;
        if (_activeMode == "stitch" || _activeMode == "plan") score += 5;

        score = Math.Clamp(score, 0, 100);
        QualityBar.Value = score;
        QualityText.Text = $"{score}%";
    }

    private void OnCopyOutput(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(OutputTextBox.Text)) return;
        var package = new DataPackage();
        package.SetText(OutputTextBox.Text);
        Clipboard.SetContent(package);
        ShowBanner("Скомпилированный промпт скопирован в буфер обмена.", InfoBarSeverity.Informational);
    }

    private void OnSaveToIdeas(object sender, RoutedEventArgs e)
    {
        var title = InputTitleBox.Text.Trim();
        var concept = InputConceptBox.Text.Trim();
        var item = Model.AddItem(title, concept);
        item.Platform = (PlatformBox.SelectedItem as ComboBoxItem)?.Content as string ?? "Web";
        item.Style = (StyleBox.SelectedItem as ComboBoxItem)?.Content as string ?? "Editorial & Swiss Modern";
        item.GeneratedPrompt = OutputTextBox.Text;
        Model.Store.Save(item);

        ShowBanner($"Идея «{item.Title}» сохранена в локальный банк идей.", InfoBarSeverity.Success);
    }

    private void OnPresetStitchLanding(object sender, RoutedEventArgs e)
    {
        InputTitleBox.Text = "Landing Page & Product Showcase";
        InputConceptBox.Text = "Современный посадочный экран SaaS продукта с hero-секцией, интерактивным калькулятором выгоды, bento-сеткой преимуществ и плавной анимацией.";
        SetPlatformAndStyle("Web", "Editorial & Swiss Modern");
        Compile();
    }

    private void OnPresetClaudePaper(object sender, RoutedEventArgs e)
    {
        InputTitleBox.Text = "Editorial Research Reader";
        InputConceptBox.Text = "Академический ридер научных статей с теплой пергаментной поверхностью, антиквой Instrument Serif и плавной сменой глав.";
        SetPlatformAndStyle("Web", "Claude Paper");
        Compile();
    }

    private void OnPresetCodexMinimal(object sender, RoutedEventArgs e)
    {
        InputTitleBox.Text = "Codex Code Architecture Inspector";
        InputConceptBox.Text = "Монохромный минималистичный интерфейс аудита исходного кода с моноширинными метриками и древовидным графом AST.";
        SetPlatformAndStyle("Desktop", "Codex Minimal");
        Compile();
    }

    private void OnPresetTerracotta(object sender, RoutedEventArgs e)
    {
        InputTitleBox.Text = "Terracotta Habit Journal";
        InputConceptBox.Text = "Теплый трекер привычек с глиняными акцентами, мягкими тенями, тактильным откликом нажатий и прогресс-барами.";
        SetPlatformAndStyle("Mobile", "Terracotta Journal");
        Compile();
    }

    private void OnPresetLinear(object sender, RoutedEventArgs e)
    {
        InputTitleBox.Text = "Linear Agent Cockpit";
        InputConceptBox.Text = "Темная кабина управления ИИ-агентами с живыми счетчиками токенов, логом выполнения задач и горячими клавишами.";
        SetPlatformAndStyle("Desktop", "Linear & DevTool Cockpit");
        Compile();
    }

    private void SetPlatformAndStyle(string platform, string style)
    {
        for (var i = 0; i < PlatformBox.Items.Count; i++)
        {
            if (PlatformBox.Items[i] is ComboBoxItem cbi && (string)cbi.Content == platform)
            {
                PlatformBox.SelectedIndex = i;
                break;
            }
        }
        for (var i = 0; i < StyleBox.Items.Count; i++)
        {
            if (StyleBox.Items[i] is ComboBoxItem cbi && (string)cbi.Content == style)
            {
                StyleBox.SelectedIndex = i;
                break;
            }
        }
    }

    private void ShowBanner(string message, InfoBarSeverity severity)
    {
        TranslatorBanner.Message = message;
        TranslatorBanner.Severity = severity;
        TranslatorBanner.IsOpen = true;
    }
}
