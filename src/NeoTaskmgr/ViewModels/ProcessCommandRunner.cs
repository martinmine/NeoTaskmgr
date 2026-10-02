using System.ComponentModel;
using NeoTaskmgr.Core.Processes;
using NeoTaskmgr.Core.Shell;
using NeoTaskmgr.Services;

namespace NeoTaskmgr.ViewModels;

/// <summary>Runs process and shell actions for the Processes and Details pages: confirms, reports failures, and refreshes data.</summary>
public sealed class ProcessCommandRunner(DialogService dialogs, Action requestRefresh)
{
    private const int ErrorCancelled = 1223; // The user declined the UAC prompt.

    public DialogService Dialogs => dialogs;

    /// <summary>Runs an action, shows any failure, and refreshes the data.</summary>
    public Task RunAsync(Action action) => RunCoreAsync(() =>
    {
        action();
        return Task.CompletedTask;
    });

    /// <summary>Like <see cref="RunAsync"/>, for slow actions that would block the UI.</summary>
    public Task RunInBackgroundAsync(Action action) => RunCoreAsync(() => Task.Run(action));

    private async Task RunCoreAsync(Func<Task> action)
    {
        try
        {
            await action();
        }
        catch (Win32Exception e) when (e.NativeErrorCode != ErrorCancelled)
        {
            await dialogs.ShowErrorAsync(Strings.Get("ActionFailedTitle"), e.Message);
        }
        catch (Win32Exception)
        {
            // Cancelled by the user.
        }

        requestRefresh();
    }

    /// <summary>Runs an action for each process; all are attempted, and the first failure is reported.</summary>
    public Task RunForEachAsync(IEnumerable<ProcessKey> processes, Action<ProcessKey> action) => RunAsync(() =>
    {
        Win32Exception? firstError = null;
        foreach (var process in processes)
        {
            try
            {
                action(process);
            }
            catch (Win32Exception e)
            {
                firstError ??= e;
            }
        }

        if (firstError is not null)
        {
            throw firstError;
        }
    });

    public async Task SetEfficiencyModeAsync(string name, IEnumerable<ProcessKey> processes, bool enabled)
    {
        if (!enabled || await dialogs.ConfirmAsync(
                Strings.Format("EfficiencyConfirmTitle", name), Strings.Get("EfficiencyConfirmMessage"), Strings.Get("EfficiencyConfirmButton")))
        {
            await RunForEachAsync(processes, p => ProcessActions.SetEfficiencyMode(p, enabled));
        }
        else
        {
            requestRefresh(); // Resets toggle buttons that were switched before the dialog was cancelled.
        }
    }

    public async Task CreateDumpFileAsync(ProcessKey process, string processName)
    {
        string? path;
        try
        {
            // A full dump of a large process takes a while; keep the UI responsive.
            path = await Task.Run(() => ProcessActions.CreateDumpFile(process, processName, Path.GetTempPath()));
        }
        catch (Exception e) when (e is Win32Exception or IOException or UnauthorizedAccessException)
        {
            await dialogs.ShowErrorAsync(Strings.Get("ActionFailedTitle"), e.Message);
            return;
        }

        if (path is not null &&
            await dialogs.ConfirmAsync(Strings.Get("DumpCreatedTitle"), Strings.Format("DumpCreatedMessage", path), Strings.Get("OpenFileLocation"), Strings.Get("DialogOk")))
        {
            await RunAsync(() => ShellActions.OpenFileLocation(path));
        }
    }

    public async Task RunNewTaskAsync()
    {
        if (await dialogs.RunNewTaskAsync() is { } task)
        {
            await RunAsync(() => ShellActions.Run(task.Command, task.AsAdministrator));
        }
    }

    public Task OpenFileLocationAsync(string path) => RunAsync(() => ShellActions.OpenFileLocation(path));

    public Task SearchOnlineAsync(ProcessInfo process) =>
        RunAsync(() => ShellActions.SearchOnline(string.Join(' ', process.Counters.Name, process.Details.Description).Trim()));

    public Task ShowPropertiesAsync(string path) => RunAsync(() => ShellActions.ShowFileProperties(dialogs.WindowHandle, path));
}
