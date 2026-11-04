using KiteWin.Core;
using KiteWin.Pages;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace KiteWin;

/// Self-test for agents: runs the app's real functions on a test store,
/// prints PASS or FAIL per check, and exits with code 0 or 1.
///   $env:APP_STORE="$env:TEMP\KiteWin-self.db"; KiteWin.exe --selftest
/// (tools/selftest.ps1 does this.) Add a check for every story with logic.
public static class SelfTest
{
    public static bool Requested => Environment.GetCommandLineArgs().Contains("--selftest");

    private static int _failures, _count;

    public static async Task RunIfRequestedAsync(MainWindow window, AppModel model)
    {
        if (!Requested) return;
        if (!AppPaths.IsTestStore)
        {
            AgentLog.Line("SELFTEST refused: set APP_STORE to a file in %TEMP%");
            AgentLog.Exit(1);
        }
        await Task.Delay(500);

        // Before anything else: the page open at launch shows what the store holds.
        Check("the first page shows the stored items at launch",
            model.Visible.Count == model.Items.Count(i => model.Page != SidebarPage.Starred || i.IsStarred),
            $"{model.Visible.Count} shown, {model.Items.Count} stored");

        window.Show(SidebarPage.All.Id());
        var before = model.Items.Count;
        var item = model.AddItem("  Buy   milk ");
        Check("New item adds one item", model.Items.Count == before + 1);
        Check("the new item is selected", model.Selection == item);
        Check("titles are cleaned", item.Title == "Buy milk");

        item.IsStarred = true;
        using (var reopened = new ItemStore(AppPaths.Store))
            Check("starring is saved to the database", reopened.All().Any(i => i.Id == item.Id && i.IsStarred));

        window.Show(SidebarPage.Starred.Id());
        await Task.Delay(100);
        var starred = model.AddItem("Starred from the Starred page");
        Check("New item on Starred is starred and visible", starred.IsStarred && model.Visible.Contains(starred));

        model.Delete(starred);
        model.Delete(item);
        Check("deleting removes the items", model.Items.Count == before);

        // Every page opens, and every command in a command bar has a label and a shortcut.
        foreach (var page in Enum.GetValues<SidebarPage>())
        {
            window.Show(page.Id());
            await Task.Delay(200);
            Check($"page {page.Id()} shows its title", Find<TextBlock>(window.RootElement).Any(t => t.Text == page.Title()));
        }
        foreach (var button in Find<AppBarButton>(window.RootElement))
        {
            var shortcut = button.KeyboardAccelerators.Count > 0 || !string.IsNullOrEmpty(button.KeyboardAcceleratorTextOverride);
            Check($"command '{button.Label}' has a label and a shortcut", !string.IsNullOrEmpty(button.Label) && shortcut);
        }
        window.Show("settings");
        await Task.Delay(300);
        Check("Settings opens", Find<SettingsPage>(window.RootElement).Any());

        AgentLog.Line(_failures == 0 ? $"SELFTEST PASS {_count} checks" : $"SELFTEST FAIL {_failures} of {_count} checks");
        AgentLog.Exit(_failures == 0 ? 0 : 1);
    }

    private static void Check(string name, bool ok, string detail = "")
    {
        _count++;
        if (!ok) _failures++;
        AgentLog.Line(ok ? $"PASS {name}" : $"FAIL {name}{(detail.Length > 0 ? ": " + detail : "")}");
    }

    public static IEnumerable<T> Find<T>(DependencyObject parent) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T match) yield return match;
            foreach (var d in Find<T>(child)) yield return d;
        }
    }
}
