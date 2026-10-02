using System.Diagnostics;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Shell;
using Windows.Win32.UI.WindowsAndMessaging;

namespace NeoTaskmgr.Core.Shell;

/// <summary>Actions that hand off to the Windows shell. Failures throw <see cref="System.ComponentModel.Win32Exception"/>.</summary>
public static class ShellActions
{
    /// <summary>Opens File Explorer with the file selected.</summary>
    public static void OpenFileLocation(string path) => Start("explorer.exe", $"/select,\"{path}\"");

    public static void SearchOnline(string query) => Start($"https://www.bing.com/search?q={Uri.EscapeDataString(query)}");

    /// <summary>Shows the file's Properties dialog.</summary>
    public static void ShowFileProperties(nint ownerWindow, string path) =>
        PInvoke.SHObjectProperties((HWND)ownerWindow, SHOP_TYPE.SHOP_FILEPATH, path, null);

    /// <summary>Runs a command like the Run dialog: a program, document, folder or URL, optionally followed by arguments.</summary>
    public static void Run(string commandLine, bool asAdministrator)
    {
        var (file, arguments) = SplitCommandLine(commandLine.Trim());
        var startInfo = new ProcessStartInfo(file, arguments) { UseShellExecute = true };
        if (asAdministrator)
        {
            startInfo.Verb = "runas";
        }

        Process.Start(startInfo)?.Dispose();
    }

    /// <summary>Brings a window to the foreground, restoring it if it is minimized.</summary>
    public static void SwitchTo(nint window)
    {
        var handle = (HWND)window;
        if (PInvoke.IsIconic(handle))
        {
            PInvoke.ShowWindow(handle, SHOW_WINDOW_CMD.SW_RESTORE);
        }

        PInvoke.SetForegroundWindow(handle);
    }

    /// <summary>Splits a command line into the file to start and its arguments.</summary>
    internal static (string File, string Arguments) SplitCommandLine(string commandLine)
    {
        if (commandLine.StartsWith('"'))
        {
            var end = commandLine.IndexOf('"', 1);
            return end < 0 ? (commandLine.Trim('"'), string.Empty) : (commandLine[1..end], commandLine[(end + 1)..].Trim());
        }

        // An unquoted path with spaces, such as C:\Program Files\App\app.exe.
        if (File.Exists(commandLine) || Directory.Exists(commandLine))
        {
            return (commandLine, string.Empty);
        }

        var space = commandLine.IndexOf(' ');
        return space < 0 ? (commandLine, string.Empty) : (commandLine[..space], commandLine[(space + 1)..].Trim());
    }

    private static void Start(string file, string arguments = "") =>
        Process.Start(new ProcessStartInfo(file, arguments) { UseShellExecute = true })?.Dispose();
}
