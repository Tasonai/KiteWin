using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Windows.Graphics;

namespace KiteWin.UI;

/// AppWindow sizes are physical pixels. These helpers take effective pixels (epx), the unit
/// XAML uses, and scale them by the window's DPI, so a 1100 epx window is 1100 epx at 150% too.
public static class WindowBehavior
{
    public static nint Handle(this Window window) => WinRT.Interop.WindowNative.GetWindowHandle(window);

    public static double Scale(this Window window) => Win32.GetDpiForWindow(window.Handle()) / 96.0;

    /// Sizes the window in epx and centers it in the work area (no taskbar) of its display.
    public static void SizeAndCenter(this Window window, int width, int height)
    {
        var scale = window.Scale();
        var size = new SizeInt32((int)Math.Round(width * scale), (int)Math.Round(height * scale));
        var area = DisplayArea.GetFromWindowId(window.AppWindow.Id, DisplayAreaFallback.Nearest).WorkArea;
        size.Width = Math.Min(size.Width, area.Width);
        size.Height = Math.Min(size.Height, area.Height);
        window.AppWindow.MoveAndResize(new RectInt32(
            area.X + (area.Width - size.Width) / 2,
            area.Y + (area.Height - size.Height) / 2,
            size.Width, size.Height));
    }

    /// The smallest size the layout still works at, in epx.
    public static void MinimumSize(this Window window, int width, int height)
    {
        if (window.AppWindow.Presenter is not OverlappedPresenter presenter) return;
        var scale = window.Scale();
        presenter.PreferredMinimumWidth = (int)Math.Round(width * scale);
        presenter.PreferredMinimumHeight = (int)Math.Round(height * scale);
    }
}
