using KiteWin.Core;
using KiteWin.UI;
using Microsoft.UI.Xaml;

namespace KiteWin;

// KiteWin
//   KiteWin.exe                         the app
//   APP_STORE=<file> KiteWin.exe        use another database (tests, experiments); must be in %TEMP%
//   APP_SNAPSHOT=<dir> KiteWin.exe      save a PNG of every page and Settings in light and dark, then quit
//   APP_SNAPSHOT_PAGES=all,settings          limit the snapshot pages
//   APP_SEED=1 KiteWin.exe              fill an empty %TEMP% store with sample items (tools/snap.ps1)
//   KiteWin.exe --selftest              run the self-test on a %TEMP% store, exit 0 or 1 (tools/selftest.ps1)
//   APP_ACCENT=<id> KiteWin.exe         run with another accent from Theme.All (UI/Theme.cs)
//   APP_GRID=1 KiteWin.exe              draw the 8 epx grid and the layout probes over the window
public partial class App : Application
{
    public static AppModel Model { get; private set; } = null!;
    public static MainWindow MainWindow { get; private set; } = null!;

    public App()
    {
        InitializeComponent();
        UnhandledException += (_, e) => AgentLog.Line($"CRASH {e.Exception}");
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        // The accent must be in the resources before the first control loads.
        // Not in the constructor: Application.Current.Resources throws E_UNEXPECTED there.
        Theme.ApplyAccent(Environment.GetEnvironmentVariable("APP_ACCENT") ?? Prefs.Current.Accent);
        var store = new ItemStore(AppPaths.Store);
        SeedData.InsertIfRequested(store);
        Model = new AppModel(store);
        MainWindow = new MainWindow(Model);
        MainWindow.Activate();
        // Agent modes run once the window has drawn its first frame.
        MainWindow.DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, async () =>
        {
            await SelfTest.RunIfRequestedAsync(MainWindow, Model);
            await Snapshot.RunIfRequestedAsync(MainWindow, Model);
        });
    }
}
