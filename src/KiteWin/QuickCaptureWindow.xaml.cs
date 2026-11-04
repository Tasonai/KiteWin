using KiteWin.Core;
using KiteWin.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.System;

namespace KiteWin;

public sealed partial class QuickCaptureWindow : Window
{
    private readonly AppModel _model;

    public QuickCaptureWindow(AppModel model)
    {
        _model = model;
        InitializeComponent();

        ExtendsContentIntoTitleBar = true;
        this.SizeAndCenter(540, 360);
        AppWindow.IsShownInSwitchers = false;

        Title = "Быстрый захват - KiteWin";
        AppWindow.SetIcon(Path.Combine(AppContext.BaseDirectory, "Assets", "AppIcon.ico"));
        ApplyTheme(Prefs.Current.Theme);

        Activated += OnWindowActivated;
    }

    private void OnWindowActivated(object sender, WindowActivatedEventArgs args)
    {
        if (args.WindowActivationState != WindowActivationState.Deactivated)
        {
            TitleInput.Focus(FocusState.Programmatic);
        }
    }

    public void ShowAndFocus()
    {
        TitleInput.Text = "";
        ConceptInput.Text = "";
        PlatformPicker.SelectedIndex = 0;
        StylePicker.SelectedIndex = 0;

        AppWindow.Show();
        this.Activate();
        TitleInput.Focus(FocusState.Programmatic);
    }

    private void OnSaveClicked(object sender, RoutedEventArgs e)
    {
        SaveAndClose();
    }

    private void OnCancelClicked(object sender, RoutedEventArgs e)
    {
        AppWindow.Hide();
    }

    private void OnSnipClicked(object sender, RoutedEventArgs e)
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "ms-screenclip:",
                UseShellExecute = true
            });
        }
        catch
        {
            // Snipping tool fallback
        }
    }

    private void SaveAndClose()
    {
        var title = TitleInput.Text.Trim();
        if (string.IsNullOrEmpty(title))
        {
            title = "Новая идея";
        }

        var platform = (PlatformPicker.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Web";
        var style = (StylePicker.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Editorial & Swiss Modern";
        var concept = ConceptInput.Text.Trim();

        var item = new Item
        {
            Title = ItemTitle.Clean(title),
            Concept = concept,
            Platform = platform,
            Style = style,
            Status = "backlog"
        };

        _model.Store.Save(item);
        _model.Refresh();

        AppWindow.Hide();
    }

    private void OnTitleKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Enter)
        {
            ConceptInput.Focus(FocusState.Programmatic);
            e.Handled = true;
        }
        else if (e.Key == VirtualKey.Escape)
        {
            AppWindow.Hide();
            e.Handled = true;
        }
    }

    private void OnConceptKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Escape)
        {
            AppWindow.Hide();
            e.Handled = true;
        }
    }

    public void ApplyTheme(string mode)
    {
        if (Content is FrameworkElement root)
        {
            root.RequestedTheme = Theme.Element(mode);
        }
    }
}
