using KiteWin.Pages;
using KiteWin.Services;
using KiteWin.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;

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

        // Project Items
        AddMenuItem(SidebarPage.All);
        AddMenuItem(SidebarPage.InProgress);
        AddMenuItem(SidebarPage.Completed);
        AddMenuItem(SidebarPage.Starred);

        // Divider
        Nav.MenuItems.Add(new NavigationViewItemSeparator());

        // Studio & Tools
        AddMenuItem(SidebarPage.Canvas);
        AddMenuItem(SidebarPage.Translator);
        AddMenuItem(SidebarPage.Skills);
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
    /// "system", "light", or "dark". Also sets the caption buttons to match.
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
