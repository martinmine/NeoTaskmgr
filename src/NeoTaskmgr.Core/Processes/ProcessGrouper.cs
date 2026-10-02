namespace NeoTaskmgr.Core.Processes;

/// <summary>The sections of the Processes page.</summary>
public enum ProcessCategory
{
    App,
    Background,
    Windows,
}

/// <summary>One row of the Processes page: an app or background program with the processes it started, or a single process.</summary>
public sealed record ProcessGroup(ProcessCategory Category, IReadOnlyList<ProcessInfo> Processes)
{
    /// <summary>The group's main process.</summary>
    public ProcessInfo Root => Processes[0];
}

public static class ProcessGrouper
{
    /// <summary>
    /// Groups processes like Task Manager's Processes page:
    /// <list type="bullet">
    /// <item>A process with an app window is an app. Processes it started (directly or indirectly) that have no window
    /// of their own are grouped under it. Explorer is an exception: it starts most apps, which are not part of it.</item>
    /// <item>Other processes are grouped under their topmost ancestor with the same image, like a browser's helper processes.</item>
    /// <item>Groups without an app are Windows processes when the main image is in the Windows directory or cannot be
    /// read (protected system processes), and background processes otherwise.</item>
    /// <item>The System Idle Process is left out.</item>
    /// </list>
    /// </summary>
    public static IReadOnlyList<ProcessGroup> Group(IReadOnlyList<ProcessInfo> processes, string windowsDirectory)
    {
        var byPid = processes.ToDictionary(p => p.Counters.Pid);
        var membersByRoot = new Dictionary<ProcessKey, List<ProcessInfo>>();
        foreach (var process in processes.Where(p => p.Counters.Pid != 0))
        {
            var root = FindRoot(process, byPid);
            if (!membersByRoot.TryGetValue(root.Key, out var members))
            {
                membersByRoot[root.Key] = members = [];
            }

            members.Add(process);
        }

        return membersByRoot.Values.Select(members =>
        {
            // Root first (it was created first), then the processes it started in creation order.
            members.Sort((a, b) => a.Counters.CreateTime.CompareTo(b.Counters.CreateTime));
            var root = members[0];
            var category = root.Window is not null ? ProcessCategory.App
                : IsWindowsProcess(root, windowsDirectory) ? ProcessCategory.Windows
                : ProcessCategory.Background;
            return new ProcessGroup(category, members);
        }).ToList();
    }

    private static ProcessInfo FindRoot(ProcessInfo process, Dictionary<int, ProcessInfo> byPid)
    {
        if (process.Window is not null)
        {
            return process;
        }

        // The nearest ancestor with a window, unless that is Explorer.
        for (var ancestor = GetParent(process, byPid); ancestor is not null; ancestor = GetParent(ancestor, byPid))
        {
            if (ancestor.Window is not null)
            {
                if (!IsExplorer(ancestor))
                {
                    return ancestor;
                }

                break;
            }
        }

        // Otherwise the topmost ancestor running the same program.
        var root = process;
        while (GetParent(root, byPid) is { Window: null } parent && parent.Counters.Name.Equals(root.Counters.Name, StringComparison.OrdinalIgnoreCase))
        {
            root = parent;
        }

        return root;
    }

    /// <summary>The parent process, or null if it exited (or its PID was reused, so it was created after the child).</summary>
    private static ProcessInfo? GetParent(ProcessInfo process, Dictionary<int, ProcessInfo> byPid) =>
        byPid.TryGetValue(process.Counters.ParentPid, out var parent) &&
        parent.Counters.Pid != process.Counters.Pid &&
        parent.Counters.Pid != 0 &&
        parent.Counters.CreateTime <= process.Counters.CreateTime
            ? parent
            : null;

    private static bool IsExplorer(ProcessInfo process) => process.Counters.Name.Equals("explorer.exe", StringComparison.OrdinalIgnoreCase);

    private static bool IsWindowsProcess(ProcessInfo process, string windowsDirectory) =>
        process.Details.ImagePath is not { } path ||
        path.StartsWith(Path.TrimEndingDirectorySeparator(windowsDirectory) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
}
