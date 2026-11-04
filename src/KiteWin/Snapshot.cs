using KiteWin.UI;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;

namespace KiteWin;

/// Screenshot mode for agents.
///   $env:APP_STORE="$env:TEMP\test.db"; $env:APP_SNAPSHOT="$env:TEMP\shots"; KiteWin.exe
/// writes <page>-light.png and <page>-dark.png for every SidebarPage and for Settings, then quits.
/// Each PNG gets a <page>-<mode>-layout.json with the grid probes (tools/check-layout.ps1).
/// APP_SNAPSHOT_PAGES=all,settings limits the pages. APP_ACCENT=<id> shows another accent.
public static class Snapshot
{
    public static async Task RunIfRequestedAsync(MainWindow window, AppModel model)
    {
        var dir = Environment.GetEnvironmentVariable("APP_SNAPSHOT");
        if (string.IsNullOrEmpty(dir)) return;
        // Snapshots never run against the real database.
        if (!AppPaths.IsTestStore)
        {
            AgentLog.Line("SNAPSHOT refused: set APP_STORE to a file in %TEMP%");
            AgentLog.Exit(1);
        }
        var only = Environment.GetEnvironmentVariable("APP_SNAPSHOT_PAGES")?.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        var pages = Enum.GetValues<SidebarPage>().Select(p => p.Id()).Append("settings")
            .Where(p => only is null || only.Contains(p)).ToList();

        // A sleeping display renders nothing. Keep it on until the run quits.
        Win32.SetThreadExecutionState(Win32.ES_CONTINUOUS | Win32.ES_DISPLAY_REQUIRED | Win32.ES_SYSTEM_REQUIRED);
        Directory.CreateDirectory(dir);
        await Task.Delay(1500);
        foreach (var mode in new[] { "light", "dark" })
        {
            window.ApplyTheme(mode);
            foreach (var page in pages)
            {
                window.Show(page);
                // Selected rows show the detail pane; the first row is a fair sample.
                if (page != "settings" && model.Selection is null && model.Visible.Count > 0) model.Selection = model.Visible[0];
                await Task.Delay(800);
                window.DrawGridOverlay();
                await Task.Delay(100);
                await CaptureAsync(window, Path.Combine(dir, $"{page}-{mode}.png"));
            }
        }
        AgentLog.Line($"SNAPSHOT done {dir}");
        AgentLog.Exit(0);
    }

    /// Captures the real window with PrintWindow(PW_RENDERFULLCONTENT), so Mica, the title bar,
    /// and every control look as on screen, even when another window covers this one.
    /// Crops the invisible resize border with the DWM frame bounds.
    public static async Task CaptureAsync(MainWindow window, string path)
    {
        var hwnd = window.Handle();
        // An active window, like one someone is using: a bright title bar and full Mica.
        Win32.ForceForeground(hwnd);
        await Task.Delay(150);

        GridProbes.Write(window.RootElement, path.Replace(".png", "-layout.json"));

        Win32.GetWindowRect(hwnd, out var outer);
        Win32.DwmGetWindowAttribute(hwnd, Win32.DWMWA_EXTENDED_FRAME_BOUNDS, out var frame, System.Runtime.InteropServices.Marshal.SizeOf<Win32.RECT>());
        int width = outer.Width, height = outer.Height;
        var header = new Win32.BITMAPINFOHEADER
        {
            biSize = (uint)System.Runtime.InteropServices.Marshal.SizeOf<Win32.BITMAPINFOHEADER>(),
            biWidth = width,
            biHeight = -height, // top-down rows
            biPlanes = 1,
            biBitCount = 32,
        };
        var screen = Win32.GetDC(0);
        var dc = Win32.CreateCompatibleDC(screen);
        var bitmap = Win32.CreateDIBSection(dc, ref header, 0, out var bits, 0, 0);
        var old = Win32.SelectObject(dc, bitmap);
        byte[] pixels;
        int cropX = frame.Left - outer.Left, cropY = frame.Top - outer.Top;
        int cropW = Math.Min(frame.Width, width - cropX), cropH = Math.Min(frame.Height, height - cropY);
        try
        {
            if (!Win32.PrintWindow(hwnd, dc, Win32.PW_RENDERFULLCONTENT))
            {
                AgentLog.Line($"SNAPSHOT {path} failed: PrintWindow returned false");
                return;
            }
            pixels = new byte[cropW * cropH * 4];
            unsafe
            {
                var source = (byte*)bits;
                for (var y = 0; y < cropH; y++)
                    new ReadOnlySpan<byte>(source + ((y + cropY) * width + cropX) * 4, cropW * 4).CopyTo(pixels.AsSpan(y * cropW * 4));
            }
        }
        finally
        {
            Win32.SelectObject(dc, old);
            Win32.DeleteObject(bitmap);
            Win32.DeleteDC(dc);
            Win32.ReleaseDC(0, screen);
        }

        using var stream = new InMemoryRandomAccessStream();
        var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, stream);
        encoder.SetPixelData(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Ignore, (uint)cropW, (uint)cropH, 96, 96, pixels);
        await encoder.FlushAsync();
        var png = new byte[stream.Size];
        using var reader = new DataReader(stream.GetInputStreamAt(0));
        await reader.LoadAsync((uint)stream.Size);
        reader.ReadBytes(png);
        await File.WriteAllBytesAsync(path, png);
        AgentLog.Line($"SNAPSHOT {path}");
    }
}
