using KiteWin.UI;
using Microsoft.UI.Xaml;
using WinRT.Interop;

namespace KiteWin.Services;

public sealed class QuickCaptureService : IDisposable
{
    private const int HotkeyId = 0x4B49; // 'KI'
    private readonly MainWindow _mainWindow;
    private readonly QuickCaptureWindow _quickWindow;
    private readonly nint _hwnd;
    private readonly Win32.SUBCLASSPROC _subclassProc;
    private bool _registered;

    public QuickCaptureService(MainWindow mainWindow, QuickCaptureWindow quickWindow)
    {
        _mainWindow = mainWindow;
        _quickWindow = quickWindow;
        _hwnd = WindowNative.GetWindowHandle(mainWindow);

        _subclassProc = new Win32.SUBCLASSPROC(WndProc);
        Win32.SetWindowSubclass(_hwnd, _subclassProc, 1, 0);

        RegisterHotkey();
    }

    private void RegisterHotkey()
    {
        if (_registered) return;
        // Register Ctrl + Shift + K (0x4B)
        _registered = Win32.RegisterHotKey(_hwnd, HotkeyId, Win32.MOD_CONTROL | Win32.MOD_SHIFT | Win32.MOD_NOREPEAT, 0x4B);
    }

    private nint WndProc(nint hWnd, uint uMsg, nuint wParam, nint lParam, nuint uIdSubclass, nuint dwRefData)
    {
        if (uMsg == Win32.WM_HOTKEY && (int)wParam == HotkeyId)
        {
            ToggleQuickCapture();
            return 0;
        }

        return Win32.DefSubclassProc(hWnd, uMsg, wParam, lParam);
    }

    public void ToggleQuickCapture()
    {
        _mainWindow.DispatcherQueue.TryEnqueue(() =>
        {
            if (_quickWindow.Visible)
            {
                _quickWindow.AppWindow.Hide();
            }
            else
            {
                _quickWindow.ShowAndFocus();
            }
        });
    }

    public void Dispose()
    {
        if (_registered)
        {
            Win32.UnregisterHotKey(_hwnd, HotkeyId);
            _registered = false;
        }
        Win32.RemoveWindowSubclass(_hwnd, _subclassProc, 1);
    }
}
