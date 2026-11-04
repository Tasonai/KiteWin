using KiteWin.Pages;
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
        this.SizeAndCenter(1100, 720);
        this.MinimumSize(760, 480);
        ApplyTheme(Prefs.Current.Theme);

        foreach (var page in Enum.GetValues<SidebarPage>())
        {
            Nav.MenuItems.Add(new NavigationViewItem
            {
                Content = page.Title(),
                Icon = new FontIcon { Glyph = page.Glyph() },
                Tag = page.Id(),
            });
        }
        Show(model.Page.Id());
    }

    public AppModel Model { get; }

    /// Opens a page by id: a SidebarPage id, or "settings".
    public void Show(string id)
    {
        var target = id == "settings"
            ? Nav.SettingsItem
            : Nav.MenuItems.OfType<NavigationViewItem>().FirstOrDefault(i => (string)i.Tag == id);
        if (target is not null && Nav.SelectedItem != target) Nav.SelectedItem = target;
    }

    /// "system", "light", or "dark". Also sets the caption buttons to match.
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
            ContentFrame.Navigate(typeof(SettingsPage), null, transition);
            return;
        }
        if (args.SelectedItem is NavigationViewItem { Tag: string id } && SidebarPages.FromId(id) is { } page)
        {
            Model.Page = page;
            Title = $"{page.Title()} - KiteWin";
            if (ContentFrame.Content is not ItemsPage) ContentFrame.Navigate(typeof(ItemsPage), null, transition);
        }
    }

    private void OnPaneToggleRequested(TitleBar sender, object args) => Nav.IsPaneOpen = !Nav.IsPaneOpen;
}
