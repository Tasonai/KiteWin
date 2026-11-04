using System.Text.Json;

namespace KiteWin.Core.Models;

public sealed class CanvasStore
{
    private readonly string _filePath;
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public CanvasGraphSnapshot Snapshot { get; private set; }

    public CanvasStore(string? filePath = null)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var dir = Path.Combine(appData, "KiteWin");
            Directory.CreateDirectory(dir);
            _filePath = Path.Combine(dir, "canvas.json");
        }
        else
        {
            _filePath = filePath;
        }

        Snapshot = LoadOrSeed();
    }

    public void Save()
    {
        try
        {
            Snapshot.ExportedAt = DateTimeOffset.Now.ToString("O");
            var json = JsonSerializer.Serialize(Snapshot, JsonOpts);
            var dir = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(_filePath, json);
        }
        catch
        {
            // Transient file write failure fallback
        }
    }

    public string GetSnapshotJson()
    {
        Snapshot.ExportedAt = DateTimeOffset.Now.ToString("O");
        return JsonSerializer.Serialize(Snapshot, JsonOpts);
    }

    public bool ImportSnapshot(string json)
    {
        try
        {
            var imported = JsonSerializer.Deserialize<CanvasGraphSnapshot>(json, JsonOpts);
            if (imported != null)
            {
                Snapshot = imported;
                Save();
                return true;
            }
        }
        catch
        {
            // Parse error
        }
        return false;
    }

    private CanvasGraphSnapshot LoadOrSeed()
    {
        try
        {
            if (File.Exists(_filePath))
            {
                var text = File.ReadAllText(_filePath);
                var loaded = JsonSerializer.Deserialize<CanvasGraphSnapshot>(text, JsonOpts);
                if (loaded != null) return loaded;
            }
        }
        catch
        {
            // Fall through to seed
        }

        return CreateDefaultSeed();
    }

    public static CanvasGraphSnapshot CreateDefaultSeed()
    {
        return new CanvasGraphSnapshot
        {
            Version = 1,
            ExportedAt = DateTimeOffset.Now.ToString("O"),
            Frames =
            [
                new CanvasFrame
                {
                    Title = "Архитектурные принципы и UI/UX",
                    ColorHex = "#3B82F6",
                    X = 480,
                    Y = 40,
                    Width = 440,
                    Height = 520
                }
            ],
            ReferenceCards =
            [
                new CanvasReferenceCard
                {
                    Title = "Linear Navigation & Command HUD",
                    Source = "Mobbin",
                    Url = "https://mobbin.com",
                    ColorHex = "#10B981",
                    X = 180,
                    Y = 160,
                    Tags = ["Shortcuts", "Ergonomics", "Dark Mode"],
                    Description = "Референс компактной навигации и быстрого доступа через клавиши."
                }
            ],
            Stickies =
            [
                new CanvasSticky
                {
                    Text = "Архитектурный контраст: отказ от серого слопа",
                    ColorHex = "#FEF08A",
                    X = 560,
                    Y = 100
                },
                new CanvasSticky
                {
                    Text = "44px Touch Targets & Apple HIG / Fluent 4px Grid",
                    ColorHex = "#BBF7D0",
                    X = 560,
                    Y = 320
                },
                new CanvasSticky
                {
                    Text = "Zero-latency нативный отклик SQLite WAL",
                    ColorHex = "#DDD6FE",
                    X = 180,
                    Y = 400
                }
            ],
            Connections =
            [
                new CanvasConnection
                {
                    FromId = "sticky-1",
                    ToId = "sticky-2",
                    Label = "HIG 4px",
                    ColorHex = "#10B981"
                }
            ],
            ItemPositions = new Dictionary<string, CanvasPoint>
            {
                ["default-idea"] = new CanvasPoint { X = 560, Y = 200 }
            }
        };
    }
}
