using System.Collections.ObjectModel;
using System.ComponentModel;
using KiteWin.Core;
using CommunityToolkit.Mvvm.ComponentModel;

namespace KiteWin;

/// The navigation pages. Snapshot mode walks through every case, then Settings.
public enum SidebarPage { All, Starred }

public static class SidebarPages
{
    public static string Id(this SidebarPage page) => page.ToString().ToLowerInvariant();

    public static string Title(this SidebarPage page) => page switch
    {
        SidebarPage.All => "All items",
        SidebarPage.Starred => "Starred",
        _ => page.ToString(),
    };

    /// Segoe Fluent Icons glyphs, written as code points so they stay readable in source.
    public static string Glyph(this SidebarPage page) => char.ConvertFromUtf32(page switch
    {
        SidebarPage.All => 0xE8FD,       // BulletedList
        SidebarPage.Starred => 0xE734,   // FavoriteStar
        _ => 0xE8A5,                     // Document
    });

    public static SidebarPage? FromId(string id) =>
        Enum.GetValues<SidebarPage>().Cast<SidebarPage?>().FirstOrDefault(p => p!.Value.Id() == id);
}

/// App-wide state that pages, commands, and the self-test share.
public partial class AppModel : ObservableObject
{
    private readonly List<Item> _items;

    public AppModel(ItemStore store)
    {
        Store = store;
        _items = store.All();
        foreach (var item in _items) item.PropertyChanged += OnItemChanged;
        Page = SidebarPages.FromId(Prefs.Current.LastPage) ?? SidebarPage.All;
        // Setting Page to its default value fires no change, so fill the first page here.
        Refresh();
    }

    public ItemStore Store { get; }

    /// The items the current page shows, newest first. Rebuilt by Refresh.
    public ObservableCollection<Item> Visible { get; } = [];

    public IReadOnlyList<Item> Items => _items;

    [ObservableProperty]
    public partial SidebarPage Page { get; set; }

    [ObservableProperty]
    public partial Item? Selection { get; set; }

    partial void OnPageChanged(SidebarPage value)
    {
        if (!AppPaths.IsOverridden) { Prefs.Current.LastPage = value.Id(); Prefs.Current.Save(); }
        Refresh();
    }

    public void Refresh()
    {
        var keep = Selection;
        Visible.Clear();
        foreach (var item in _items.Where(Shows)) Visible.Add(item);
        Selection = keep is not null && Visible.Contains(keep) ? keep : null;
    }

    private bool Shows(Item item) => Page != SidebarPage.Starred || item.IsStarred;

    public Item AddItem(string title = "")
    {
        var item = new Item(ItemTitle.Clean(title)) { IsStarred = Page == SidebarPage.Starred };
        item.PropertyChanged += OnItemChanged;
        _items.Insert(0, item);
        Store.Save(item);
        Visible.Insert(0, item);
        Selection = item;
        return item;
    }

    /// Removes the item and moves the selection to the next row.
    public void Delete(Item item)
    {
        var index = Visible.IndexOf(item);
        item.PropertyChanged -= OnItemChanged;
        _items.Remove(item);
        Visible.Remove(item);
        Store.Delete(item.Id);
        Selection = Visible.Count == 0 ? null : Visible[Math.Clamp(index, 0, Visible.Count - 1)];
    }

    private void OnItemChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not Item item || e.PropertyName is nameof(Item.DisplayTitle) or nameof(Item.StarGlyph)) return;
        Store.Save(item);
    }
}
