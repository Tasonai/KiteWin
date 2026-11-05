using KiteWin.Core;
using KiteWin.Core.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.ApplicationModel.DataTransfer;

namespace KiteWin.Pages;

public sealed partial class PromptTranslatorPage : Page
{
    private string _activeMode = "stitch";
    private bool _isGenerating;
    private IReadOnlyList<InterviewQuestion> _currentQuestions = Array.Empty<InterviewQuestion>();
    private readonly Dictionary<string, string> _interviewAnswers = new(StringComparer.OrdinalIgnoreCase);

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
            InterviewPanel.Visibility = Visibility.Collapsed;
            _currentQuestions = Array.Empty<InterviewQuestion>();
            Compile();
        }
    }

    private void OnInputChanged(object sender, object e)
    {
        UpdateDensityGauge();
    }

    private async void OnCompileClick(object sender, RoutedEventArgs e)
    {
        if (_isGenerating) return;

        var item = CreateCurrentItem();
        var key = Prefs.Current.GeminiApiKey.Trim();

        var canAsk = Prefs.Current.TranslatorInterview
            && (_activeMode == "plan" || _activeMode == "stitch")
            && !string.IsNullOrEmpty(key)
            && item.Concept.Trim().Length >= 8;

        if (canAsk && _currentQuestions.Count == 0)
        {
            SetLoadingState(true, "Формулирую вопросы...");
            try
            {
                using var ai = new AiService();
                var settings = CreateAiSettings();
                var questions = await ai.GenerateInterviewQuestionsAsync(item, settings);

                if (questions.Count > 0)
                {
                    _currentQuestions = questions;
                    RenderInterviewQuestions(questions);
                    InterviewPanel.Visibility = Visibility.Visible;
                    SetLoadingState(false);
                    return;
                }
            }
            catch
            {
                // Fall through to standard generation if question generation fails
            }
        }

        await ExecuteAiCompilationAsync(withAnswers: false);
    }

    private async void OnSubmitInterviewClick(object sender, RoutedEventArgs e)
    {
        await ExecuteAiCompilationAsync(withAnswers: true);
    }

    private async void OnSkipInterviewClick(object sender, RoutedEventArgs e)
    {
        await ExecuteAiCompilationAsync(withAnswers: false);
    }

    private void OnOfflineCompileClick(object sender, RoutedEventArgs e)
    {
        InterviewPanel.Visibility = Visibility.Collapsed;
        Compile();
        ShowBanner("Скомпилировано локально по офлайн-шаблону (без API).", InfoBarSeverity.Informational);
    }

    private async Task ExecuteAiCompilationAsync(bool withAnswers)
    {
        if (_isGenerating) return;

        InterviewPanel.Visibility = Visibility.Collapsed;
        SetLoadingState(true, "Трансляция архитектуры...");

        var item = CreateCurrentItem();
        var key = Prefs.Current.GeminiApiKey.Trim();

        if (string.IsNullOrEmpty(key) && Prefs.Current.AiProvider != "ollama")
        {
            Compile();
            SetLoadingState(false);
            ShowBanner("Ключ Gemini API не задан. Использован офлайн-шаблон.", InfoBarSeverity.Warning);
            return;
        }

        try
        {
            using var ai = new AiService();
            var settings = CreateAiSettings();

            var skills = new[]
            {
                "/enhance-prompt",
                "/design-md",
                "/taste-design",
                "/emil-design-eng",
                "/a11y-debugging",
                "/gsd-execute-phase"
            };

            IReadOnlyList<(string Question, string Answer)>? answers = null;
            if (withAnswers && _interviewAnswers.Count > 0)
            {
                answers = _interviewAnswers
                    .Where(kv => !string.IsNullOrWhiteSpace(kv.Value))
                    .Select(kv => (kv.Key, kv.Value))
                    .ToList();
            }

            var output = await ai.GenerateSpecificationAsync(item, settings, _activeMode, skills, answers);
            OutputTextBox.Text = output;
            UpdateDensityGauge();
            ShowBanner("Инженерная спецификация успешно сформирована AI-моделью.", InfoBarSeverity.Success);
        }
        catch (Exception ex)
        {
            Compile();
            ShowBanner($"Ошибка обращения к AI: {ex.Message}. Скомпилирован локальный шаблон.", InfoBarSeverity.Error);
        }
        finally
        {
            _currentQuestions = Array.Empty<InterviewQuestion>();
            _interviewAnswers.Clear();
            SetLoadingState(false);
        }
    }

    private void RenderInterviewQuestions(IReadOnlyList<InterviewQuestion> questions)
    {
        InterviewQuestionsList.Children.Clear();
        _interviewAnswers.Clear();

        foreach (var q in questions)
        {
            var card = new StackPanel { Spacing = 8 };

            var title = new TextBlock
            {
                Text = q.Question,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                TextWrapping = TextWrapping.Wrap
            };
            card.Children.Add(title);

            if (q.Options.Count > 0)
            {
                var optionsPanel = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 8
                };

                var customBox = new TextBox
                {
                    PlaceholderText = "Свой ответ...",
                    FontSize = 12
                };

                var optionButtons = new List<Button>();
                foreach (var opt in q.Options)
                {
                    var optText = opt;
                    var btn = new Button
                    {
                        Content = optText,
                        FontSize = 12,
                        Padding = new Thickness(8, 4, 8, 4)
                    };

                    btn.Click += (_, _) =>
                    {
                        var isSelected = _interviewAnswers.TryGetValue(q.Question, out var current) && current == optText;
                        if (isSelected)
                        {
                            _interviewAnswers.Remove(q.Question);
                            btn.Style = null;
                        }
                        else
                        {
                            _interviewAnswers[q.Question] = optText;
                            customBox.Text = "";
                            foreach (var b in optionButtons) b.Style = null;
                            btn.Style = (Style)Application.Current.Resources["AccentButtonStyle"];
                        }
                    };

                    optionButtons.Add(btn);
                    optionsPanel.Children.Add(btn);
                }

                card.Children.Add(optionsPanel);

                customBox.TextChanged += (_, _) =>
                {
                    var val = customBox.Text.Trim();
                    if (!string.IsNullOrEmpty(val))
                    {
                        _interviewAnswers[q.Question] = val;
                        foreach (var b in optionButtons) b.Style = null;
                    }
                };

                card.Children.Add(customBox);
            }

            InterviewQuestionsList.Children.Add(card);
        }
    }

    private void SetLoadingState(bool loading, string text = "")
    {
        _isGenerating = loading;
        CompileBtn.IsEnabled = !loading;
        CompileProgressRing.IsActive = loading;
        CompileProgressRing.Visibility = loading ? Visibility.Visible : Visibility.Collapsed;
        CompileIcon.Visibility = loading ? Visibility.Collapsed : Visibility.Visible;
        CompileBtnText.Text = loading ? text : "Скомпилировать спецификацию";
    }

    private Item CreateCurrentItem()
    {
        return new Item(InputTitleBox?.Text ?? "Новая идея")
        {
            Concept = InputConceptBox?.Text ?? "",
            Platform = (PlatformBox?.SelectedItem as ComboBoxItem)?.Content as string ?? "Web",
            Style = (StyleBox?.SelectedItem as ComboBoxItem)?.Content as string ?? "Editorial & Swiss Modern"
        };
    }

    private static AiSettings CreateAiSettings()
    {
        var provider = Prefs.Current.AiProvider.ToLowerInvariant() switch
        {
            "openai" or "openai compatible" => AiProvider.OpenAI,
            "ollama" => AiProvider.Ollama,
            "offline" => AiProvider.Offline,
            _ => AiProvider.Gemini
        };

        return new AiSettings
        {
            Provider = provider,
            GeminiApiKey = Prefs.Current.GeminiApiKey,
            GeminiModel = Prefs.Current.GeminiModel,
            OpenAIEndpoint = Prefs.Current.OpenAIEndpoint,
            OpenAIApiKey = Prefs.Current.OpenAIApiKey,
            OpenAIModel = Prefs.Current.OpenAIModel,
            OllamaEndpoint = Prefs.Current.OllamaEndpoint,
            OllamaModel = Prefs.Current.OllamaModel,
            InterviewEnabled = Prefs.Current.TranslatorInterview
        };
    }

    private void Compile()
    {
        var item = CreateCurrentItem();

        var output = _activeMode switch
        {
            "plan" => ItemTitle.CompilePlanPrompt(item),
            "targeted" => ItemTitle.CompileTargetedEditPrompt(item, TargetElementBox?.Text ?? "", TargetScopeBox?.Text ?? ""),
            "brief" => ItemTitle.CompileConcisePrompt(item),
            "actions" => ItemTitle.CompileActionPrompt(item),
            _ => ItemTitle.CompileStitchPrompt(item)
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

    private async void OnScaffoldProjectClick(object sender, RoutedEventArgs e)
    {
        try
        {
            var picker = new Windows.Storage.Pickers.FolderPicker();
            picker.FileTypeFilter.Add("*");

            var hwnd = Win32.GetActiveWindow();
            if (hwnd == 0) hwnd = Win32.GetForegroundWindow();
            if (hwnd != 0) WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

            var folder = await picker.PickSingleFolderAsync();
            if (folder != null)
            {
                var item = CreateCurrentItem();
                var result = ProjectScaffolder.Scaffold(
                    parentFolder: folder.Path,
                    title: item.Title,
                    concept: item.Concept,
                    platform: item.Platform,
                    style: item.Style,
                    planPrompt: OutputTextBox.Text
                );
                ShowBanner($"Проект успешно создан в {result.Directory} ({result.Files.Count} файлов, включая .planning/)", InfoBarSeverity.Success);
            }
        }
        catch (Exception ex)
        {
            var baseDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Projects");
            var item = CreateCurrentItem();
            var result = ProjectScaffolder.Scaffold(
                parentFolder: baseDir,
                title: item.Title,
                concept: item.Concept,
                platform: item.Platform,
                style: item.Style,
                planPrompt: OutputTextBox.Text
            );
            ShowBanner($"Проект создан в {result.Directory} ({result.Files.Count} файлов): {ex.Message}", InfoBarSeverity.Informational);
        }
    }

    private void ShowBanner(string message, InfoBarSeverity severity)
    {
        TranslatorBanner.Message = message;
        TranslatorBanner.Severity = severity;
        TranslatorBanner.IsOpen = true;
    }
}
