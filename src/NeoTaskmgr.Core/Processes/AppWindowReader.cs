using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Dwm;
using Windows.Win32.UI.WindowsAndMessaging;

namespace NeoTaskmgr.Core.Processes;

/// <summary>Finds the processes that have an app window, i.e. a window that would show on the taskbar.</summary>
internal static unsafe class AppWindowReader
{
    /// <summary>Returns the first app window per process ID.</summary>
    public static Dictionary<int, ProcessWindow> Read()
    {
        var windows = new Dictionary<int, ProcessWindow>();
        PInvoke.EnumWindows((window, _) =>
        {
            Add(window, windows);
            return true;
        }, 0);
        return windows;
    }

    private static void Add(HWND window, Dictionary<int, ProcessWindow> windows)
    {
        if (!PInvoke.IsWindowVisible(window) ||
            PInvoke.GetWindow(window, GET_WINDOW_CMD.GW_OWNER) != HWND.Null ||
            PInvoke.GetWindowTextLength(window) == 0 ||
            ((WINDOW_EX_STYLE)PInvoke.GetWindowLong(window, WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE)).HasFlag(WINDOW_EX_STYLE.WS_EX_TOOLWINDOW) ||
            IsCloaked(window))
        {
            return;
        }

        // Store (UWP) apps are hosted in a frame owned by ApplicationFrameHost; the app's process owns the CoreWindow inside it.
        var owner = window;
        if (GetClassName(window) == "ApplicationFrameWindow")
        {
            owner = PInvoke.FindWindowEx(window, HWND.Null, "Windows.UI.Core.CoreWindow", null);
            if (owner == HWND.Null)
            {
                return;
            }
        }

        uint pid;
        if (PInvoke.GetWindowThreadProcessId(owner, &pid) != 0)
        {
            windows.TryAdd((int)pid, new ProcessWindow(window, PInvoke.IsHungAppWindow(window)));
        }
    }

    /// <summary>
    /// Cloaked windows are not shown, e.g. suspended Store apps and background shell UI. This also excludes windows on
    /// other virtual desktops, which the shell cloaks too.
    /// </summary>
    private static bool IsCloaked(HWND window)
    {
        uint cloaked;
        return PInvoke.DwmGetWindowAttribute(window, DWMWINDOWATTRIBUTE.DWMWA_CLOAKED, &cloaked, sizeof(uint)).Succeeded && cloaked != 0;
    }

    private static string GetClassName(HWND window)
    {
        var buffer = stackalloc char[256];
        var length = PInvoke.GetClassName(window, buffer, 256);
        return new string(buffer, 0, length);
    }
}
