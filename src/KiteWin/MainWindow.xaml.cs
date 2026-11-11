using KiteWin.Pages;
using KiteWin.Services;
using KiteWin.UI;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;
using Windows.UI;

namespace KiteWin;

public sealed partial class MainWindow : Window
{
    public MainWindow(AppModel model)
    {
        Model = model;
        InitializeComponent();

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);
        AppWindow.SetIcon(Path.Combine(AppContext.BaseDirectory, "Assets", "AppIcon.ico"));
        AppWindow.TitleBar.PreferredHeightOption = TitleBarHeightOption.Tall;
        this.SizeAndCenter(1140, 740);
        this.MinimumSize(780, 500);
        ApplyTheme(Prefs.Current.Theme);

        BuildNavigationMenu();
        Show(model.Page.Id());

        QuickCapture = new QuickCaptureWindow(model);
        QuickCaptureService = new QuickCaptureService(this, QuickCapture);

        AppWindow.Closing += (sender, args) =>
        {
            // Minimize to background tray daemon mode
            args.Cancel = true;
            AppWindow.Hide();
        };
    }

    public AppModel Model { get; }
    public QuickCaptureWindow QuickCapture { get; }
    public QuickCaptureService QuickCaptureService { get; }

    private void BuildNavigationMenu()
    {
        Nav.MenuItems.Clear();

        // 1. Проекты и бэклог
        Nav.MenuItems.Add(new NavigationViewItemHeader { Content = "Проекты и бэклог" });
        AddMenuItem(SidebarPage.All);
        AddMenuItem(SidebarPage.InProgress);
        AddMenuItem(SidebarPage.Completed);
        AddMenuItem(SidebarPage.Starred);

        // 2. Архитектура и синтез
        Nav.MenuItems.Add(new NavigationViewItemSeparator());
        Nav.MenuItems.Add(new NavigationViewItemHeader { Content = "Архитектура и синтез" });
        AddMenuItem(SidebarPage.Canvas);
        AddMenuItem(SidebarPage.Hyperscale);
        AddMenuItem(SidebarPage.Translator);
        AddMenuItem(SidebarPage.Skills);

        // 3. Дизайн и интеграции
        Nav.MenuItems.Add(new NavigationViewItemSeparator());
        Nav.MenuItems.Add(new NavigationViewItemHeader { Content = "Дизайн и интеграции" });
        AddMenuItem(SidebarPage.Sync);
    }

    private void AddMenuItem(SidebarPage page)
    {
        Nav.MenuItems.Add(new NavigationViewItem
        {
            Content = page.Title(),
            Icon = new FontIcon { Glyph = page.Glyph() },
            Tag = page.Id(),
        });
    }

    /// <summary>
    /// Opens a page by id: a SidebarPage id, or "settings".
    /// </summary>
    public void Show(string id)
    {
        var target = id == "settings"
            ? Nav.SettingsItem
            : Nav.MenuItems.OfType<NavigationViewItem>().FirstOrDefault(i => (string)i.Tag == id);
        if (target is not null && Nav.SelectedItem != target) Nav.SelectedItem = target;
    }

    /// <summary>
    /// "system", "light", or "dark". Also sets the caption buttons on the right to match the monochrome theme.
    /// </summary>
    public void ApplyTheme(string mode)
    {
        Root.RequestedTheme = Theme.Element(mode);
        AppWindow.TitleBar.PreferredTheme = mode switch
        {
            "light" => TitleBarTheme.Light,
            "dark" => TitleBarTheme.Dark,
            _ => TitleBarTheme.UseDefaultAppMode,
        };
        UpdateTitleBarButtons(mode);
    }

    private void UpdateTitleBarButtons(string mode)
    {
        var isDark = mode == "dark" || (mode == "system" && Root.ActualTheme == ElementTheme.Dark);
        var titleBar = AppWindow.TitleBar;
        titleBar.ButtonBackgroundColor = Colors.Transparent;
        titleBar.ButtonInactiveBackgroundColor = Colors.Transparent;

        if (isDark)
        {
            titleBar.ButtonForegroundColor = Color.FromArgb(255, 240, 240, 240);
            titleBar.ButtonHoverForegroundColor = Colors.White;
            titleBar.ButtonHoverBackgroundColor = Color.FromArgb(32, 255, 255, 255);
            titleBar.ButtonInactiveForegroundColor = Color.FromArgb(160, 160, 160, 160);
        }
        else
        {
            titleBar.ButtonForegroundColor = Color.FromArgb(255, 28, 28, 30);
            titleBar.ButtonHoverForegroundColor = Colors.Black;
            titleBar.ButtonHoverBackgroundColor = Color.FromArgb(20, 0, 0, 0);
            titleBar.ButtonInactiveForegroundColor = Color.FromArgb(160, 120, 120, 120);
        }
    }

    public void ReapplyAccent() => Theme.Reapply(Root);

    public FrameworkElement RootElement => Root;

    public void DrawGridOverlay()
    {
        if (GridOverlay.Enabled) GridOverlay.Draw(GridCanvas, Root);
    }

    private void OnNavigationChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        var transition = new SuppressNavigationTransitionInfo();
        if (args.IsSettingsSelected)
        {
            sender.Header = null;
            Title = "Настройки - KiteWin";
            ContentFrame.Navigate(typeof(SettingsPage), null, transition);
            return;
        }

        if (args.SelectedItem is NavigationViewItem { Tag: string id } && SidebarPages.FromId(id) is { } page)
        {
            Model.Page = page;
            Title = $"{page.Title()} - KiteWin";

            switch (page)
            {
                case SidebarPage.Canvas:
                    if (ContentFrame.Content is not CanvasPage)
                        ContentFrame.Navigate(typeof(CanvasPage), null, transition);
                    break;

                case SidebarPage.Hyperscale:
                    if (ContentFrame.Content is not HyperscalePage)
                        ContentFrame.Navigate(typeof(HyperscalePage), null, transition);
                    break;

                case SidebarPage.Translator:
                    if (ContentFrame.Content is not PromptTranslatorPage)
                        ContentFrame.Navigate(typeof(PromptTranslatorPage), null, transition);
                    break;

                case SidebarPage.Skills:
                    if (ContentFrame.Content is not SkillsCatalogPage)
                        ContentFrame.Navigate(typeof(SkillsCatalogPage), null, transition);
                    break;

                case SidebarPage.Sync:
                    if (ContentFrame.Content is not SyncPage)
                        ContentFrame.Navigate(typeof(SyncPage), null, transition);
                    break;

                case SidebarPage.All:
                case SidebarPage.InProgress:
                case SidebarPage.Completed:
                case SidebarPage.Starred:
                default:
                    if (ContentFrame.Content is not ItemsPage)
                        ContentFrame.Navigate(typeof(ItemsPage), null, transition);
                    break;
            }
        }
    }

    private void OnPaneToggleRequested(TitleBar sender, object args) => Nav.IsPaneOpen = !Nav.IsPaneOpen;
}
