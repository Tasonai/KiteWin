using System.ComponentModel;
using KiteWin.Core;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;

namespace KiteWin.Pages;

/// One page for every SidebarPage: the list on the left, the selected item on the right.
public sealed partial class ItemsPage : Page
{
    public ItemsPage()
    {
        InitializeComponent();
        Model.PropertyChanged += OnModelChanged;
        Model.Visible.CollectionChanged += (_, _) => UpdateStates();
        Loaded += (_, _) => { ShowPage(); UpdateStates(); };
    }

    public AppModel Model => App.Model;

    private void OnModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(AppModel.Page):
                ShowPage();
                break;
            case nameof(AppModel.Selection):
                if (List.SelectedItem != Model.Selection) List.SelectedItem = Model.Selection;
                if (Model.Selection is { } item) List.ScrollIntoView(item);
                Bindings.Update();
                UpdateStates();
                break;
        }
    }

    private void ShowPage()
    {
        PageTitle.Text = Model.Page.Title();
        EmptyGlyph.Glyph = Model.Page.Glyph();
        EmptyTitle.Text = Model.Page == SidebarPage.Starred ? "No starred items" : "No items";
    }

    private void UpdateStates()
    {
        var hasSelection = Model.Selection is not null;
        EmptyList.Visibility = Model.Visible.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        Detail.Visibility = hasSelection ? Visibility.Visible : Visibility.Collapsed;
        NoSelection.Visibility = hasSelection ? Visibility.Collapsed : Visibility.Visible;
        StarButton.IsEnabled = hasSelection;
        DeleteButton.IsEnabled = hasSelection;
        StarButton.Label = Model.Selection?.IsStarred == true ? "Unstar" : "Star";
    }

    private void OnSelectionChanged(object sender, SelectionChangedEventArgs e) =>
        Model.Selection = List.SelectedItem as Item;

    private void OnNew(object sender, RoutedEventArgs e)
    {
        Model.AddItem();
        // A new item gets focus, so the user can type its title at once.
        DispatcherQueue.TryEnqueue(() => TitleBox.Focus(FocusState.Programmatic));
    }

    private void OnStar(object sender, RoutedEventArgs e)
    {
        if (Model.Selection is not { } item) return;
        item.IsStarred = !item.IsStarred;
        UpdateStates();
    }

    private void OnDelete(object sender, RoutedEventArgs e)
    {
        if (Model.Selection is { } item) Model.Delete(item);
    }

    private void OnDeleteKey(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        args.Handled = true;
        OnDelete(sender, new RoutedEventArgs());
    }

    private void OnStarRow(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is Item item) item.IsStarred = !item.IsStarred;
        UpdateStates();
    }

    private void OnDeleteRow(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is Item item) Model.Delete(item);
    }
}
