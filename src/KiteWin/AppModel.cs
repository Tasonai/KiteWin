using System.Collections.ObjectModel;
using System.ComponentModel;
using KiteWin.Core;
using KiteWin.Core.Models;
using KiteWin.Core.Sync;
using CommunityToolkit.Mvvm.ComponentModel;

namespace KiteWin;

/// <summary>
/// Navigation items in the NavigationView sidebar.
/// Snapshot mode walks through every case, then Settings.
/// Zero emojis: uses Segoe Fluent Icons code points.
/// </summary>
public enum SidebarPage
{
    All,
    InProgress,
    Completed,
    Starred,
    Canvas,
    Translator,
    Skills,
    Sync
}

public static class SidebarPages
{
    public static string Id(this SidebarPage page) => page.ToString().ToLowerInvariant();

    public static string Title(this SidebarPage page) => page switch
    {
        SidebarPage.All => "Все идеи",
        SidebarPage.InProgress => "В разработке",
        SidebarPage.Completed => "Готово",
        SidebarPage.Starred => "Избранное",
        SidebarPage.Canvas => "Холст идей",
        SidebarPage.Translator => "Промпт-переводчик",
        SidebarPage.Skills => "База скиллов",
        SidebarPage.Sync => "Синхронизация P2P",
        _ => page.ToString(),
    };

    /// <summary>
    /// Segoe Fluent Icons glyphs as UTF-32 code points. Zero emojis.
    /// </summary>
    public static string Glyph(this SidebarPage page) => char.ConvertFromUtf32(page switch
    {
        SidebarPage.All => 0xE793,          // Lightbulb
        SidebarPage.InProgress => 0xE945,   // LightningBolt
        SidebarPage.Completed => 0xE73E,    // Checkmark
        SidebarPage.Starred => 0xE735,      // FavoriteStarFill
        SidebarPage.Canvas => 0xE746,       // ViewAll / Canvas Grid
        SidebarPage.Translator => 0xE70F,   // Edit
        SidebarPage.Skills => 0xE950,       // DeveloperTools
        SidebarPage.Sync => 0xE895,         // Sync
        _ => 0xE8A5,                        // Document
    });

    public static SidebarPage? FromId(string id) =>
        Enum.GetValues<SidebarPage>().Cast<SidebarPage?>().FirstOrDefault(p => p!.Value.Id().Equals(id, StringComparison.OrdinalIgnoreCase));
}

/// <summary>
/// App-wide observable state that pages, commands, and P2P sync share.
/// </summary>
public partial class AppModel : ObservableObject, IDisposable
{
    private readonly List<Item> _items;

    public AppModel(ItemStore store)
    {
        Store = store;
        CanvasStore = new CanvasStore();
        _items = store.All();
        foreach (var item in _items) item.PropertyChanged += OnItemChanged;

        SyncClient = new KiteSyncClient();
        SyncServer = new KiteSyncServer(store, CanvasStore);
        SyncServer.Start();

        MacEndpoint = Prefs.Current.MacEndpoint;
        Page = SidebarPages.FromId(Prefs.Current.LastPage) ?? SidebarPage.All;
        Refresh();

        if (Prefs.Current.AutoSync)
        {
            _ = Task.Run(async () =>
            {
                await Task.Delay(1000);
                await TriggerSyncAsync();
            });
        }
    }

    public ItemStore Store { get; }
    public CanvasStore CanvasStore { get; }
    public KiteSyncClient SyncClient { get; }
    public KiteSyncServer SyncServer { get; }

    /// <summary>
    /// Visible items filtered by current page and search text.
    /// </summary>
    public ObservableCollection<Item> Visible { get; } = [];

    public IReadOnlyList<Item> Items => _items;

    [ObservableProperty]
    public partial SidebarPage Page { get; set; }

    [ObservableProperty]
    public partial Item? Selection { get; set; }

    [ObservableProperty]
    public partial string SearchText { get; set; } = "";

    [ObservableProperty]
    public partial string MacEndpoint { get; set; }

    [ObservableProperty]
    public partial bool IsSyncing { get; set; }

    [ObservableProperty]
    public partial string SyncStatusText { get; set; } = "Готов к синхронизации";

    [ObservableProperty]
    public partial KiteSyncResult? LastSyncResult { get; set; }

    partial void OnPageChanged(SidebarPage value)
    {
        if (!AppPaths.IsOverridden)
        {
            Prefs.Current.LastPage = value.Id();
            Prefs.Current.Save();
        }
        Refresh();
    }

    partial void OnSearchTextChanged(string value)
    {
        Refresh();
    }

    partial void OnMacEndpointChanged(string value)
    {
        Prefs.Current.MacEndpoint = value;
        Prefs.Current.Save();
    }

    public void Refresh()
    {
        var keep = Selection;
        Visible.Clear();

        var query = SearchText?.Trim() ?? "";
        IEnumerable<Item> source = _items;

        if (!string.IsNullOrEmpty(query))
        {
            // FTS5 ranks title above tags, concept and style; unsearchable input shows everything.
            var rank = Store.Search(query)
                .Select((id, position) => (id, position))
                .ToDictionary(x => x.id, x => x.position);
            if (ItemStore.BuildMatchQuery(query) is not null)
            {
                source = _items
                    .Where(item => rank.ContainsKey(item.Id))
                    .OrderBy(item => rank[item.Id]);
            }
        }

        foreach (var item in source)
        {
            if (Shows(item)) Visible.Add(item);
        }

        Selection = keep is not null && Visible.Contains(keep) ? keep : Visible.FirstOrDefault();
    }

    private bool Shows(Item item) => Page switch
    {
        SidebarPage.Starred => item.IsStarred,
        SidebarPage.InProgress => item.Status == "in_progress",
        SidebarPage.Completed => item.Status == "completed",
        _ => true
    };

    public Item AddItem(string title = "", string concept = "")
    {
        var item = new Item(ItemTitle.Clean(title))
        {
            Concept = concept,
            Status = Page == SidebarPage.InProgress ? "in_progress" : (Page == SidebarPage.Completed ? "completed" : "backlog"),
            IsStarred = Page == SidebarPage.Starred
        };

        item.PropertyChanged += OnItemChanged;
        _items.Insert(0, item);
        Store.Save(item);
        Visible.Insert(0, item);
        Selection = item;
        return item;
    }

    public void Delete(Item item)
    {
        var index = Visible.IndexOf(item);
        item.PropertyChanged -= OnItemChanged;
        _items.Remove(item);
        Visible.Remove(item);
        Store.Delete(item.Id);
        Selection = Visible.Count == 0 ? null : Visible[Math.Clamp(index, 0, Visible.Count - 1)];
    }

    public async Task<KiteSyncResult> TriggerSyncAsync()
    {
        if (IsSyncing) return new KiteSyncResult { Success = false, Message = "Синхронизация уже выполняется." };

        IsSyncing = true;
        SyncStatusText = "Синхронизация с Kite macOS...";

        try
        {
            var res = await SyncClient.SyncBidirectionalAsync(MacEndpoint, Store);
            LastSyncResult = res;

            if (res.Success)
            {
                // Reload items from database after sync
                _items.Clear();
                _items.AddRange(Store.All());
                foreach (var it in _items) it.PropertyChanged += OnItemChanged;
                Refresh();
                SyncStatusText = $"Успешно: {res.Message}";
            }
            else
            {
                SyncStatusText = $"Ошибка: {res.Message}";
            }

            return res;
        }
        catch (Exception ex)
        {
            var fail = new KiteSyncResult { Success = false, Message = ex.Message };
            LastSyncResult = fail;
            SyncStatusText = $"Сбой: {ex.Message}";
            return fail;
        }
        finally
        {
            IsSyncing = false;
        }
    }

    private void OnItemChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not Item item) return;
        if (e.PropertyName is nameof(Item.DisplayTitle) or nameof(Item.StarGlyph) or nameof(Item.StatusGlyph) or nameof(Item.PlatformGlyph)) return;
        Store.Save(item);
    }

    public void Dispose()
    {
        SyncClient.Dispose();
        SyncServer.Dispose();
    }
}
