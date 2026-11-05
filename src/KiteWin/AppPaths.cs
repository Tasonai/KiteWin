using System.Text.Json;
using System.Text.Json.Serialization;

namespace KiteWin;

/// Where the data lives: %LOCALAPPDATA%\KiteWin\KiteWin.db, next to settings.json.
public static class AppPaths
{
    /// True when APP_STORE points somewhere else (tests, snapshots, seed data).
    public static bool IsOverridden => !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("APP_STORE"));

    /// Test runs only ever touch files in %TEMP%.
    public static bool IsTestStore =>
        IsOverridden && Path.GetFullPath(Store).StartsWith(Path.GetFullPath(Path.GetTempPath()), StringComparison.OrdinalIgnoreCase);

    public static string Folder
    {
        get
        {
            var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "KiteWin");
            Directory.CreateDirectory(dir);
            return dir;
        }
    }

    public static string Store =>
        IsOverridden ? Environment.GetEnvironmentVariable("APP_STORE")! : Path.Combine(Folder, "KiteWin.db");

    /// Test runs keep their preferences next to the test store, so they never change the real ones.
    public static string Prefs => IsOverridden ? Store + ".settings.json" : Path.Combine(Folder, "settings.json");
}

/// Small preferences, saved as a JSON file next to the data, so a test run keeps its own copy.
/// (ApplicationData.GetForUnpackaged() exists since Windows App SDK 2.2, but a file is easier to test.)
public sealed class Prefs
{
    public string Theme { get; set; } = "system";
    public string Accent { get; set; } = UI.Theme.DefaultId;
    public string LastPage { get; set; } = "all";
    public string MacEndpoint { get; set; } = "http://127.0.0.1:24242";
    public bool AutoSync { get; set; } = true;
    public string AiProvider { get; set; } = "Gemini API";
    public string GeminiApiKey { get; set; } = "";
    public string GeminiModel { get; set; } = "gemini-3.5-flash";
    public bool TranslatorInterview { get; set; } = true;
    public string OllamaEndpoint { get; set; } = "http://localhost:11434/api/generate";
    public string OllamaModel { get; set; } = "llama3";

    private static Prefs? _current;
    public static Prefs Current => _current ??= Load();

    private static Prefs Load()
    {
        try { return JsonSerializer.Deserialize(File.ReadAllText(AppPaths.Prefs), AppJson.Default.Prefs) ?? new(); }
        catch (Exception e) when (e is IOException or JsonException or UnauthorizedAccessException) { return new(); }
    }

    public void Save()
    {
        try { File.WriteAllText(AppPaths.Prefs, JsonSerializer.Serialize(this, AppJson.Default.Prefs)); }
        catch (IOException e) { AgentLog.Line($"Prefs not saved: {e.Message}"); }
    }
}

/// Source-generated JSON: no reflection, so it survives trimming and NativeAOT (tools/publish.ps1),
/// and starts faster. Add every type the app saves as JSON here.
[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(Prefs))]
[JsonSerializable(typeof(UI.LayoutFile))]
internal sealed partial class AppJson : JsonSerializerContext;
