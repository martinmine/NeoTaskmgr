using NeoTaskmgr.Core.Processes;
using static NeoTaskmgr.Core.Tests.Processes.TestProcesses;

namespace NeoTaskmgr.Core.Tests.Processes;

public sealed class ProcessGrouperTests
{
    private const string WindowsDirectory = @"C:\Windows";

    [Fact]
    public void AppAdoptsDescendantsWithoutWindows()
    {
        ProcessInfo[] processes =
        [
            Process(10, parentPid: 1, createTime: 100, hasWindow: true),
            Process(11, parentPid: 10, createTime: 200),
            Process(12, parentPid: 11, createTime: 300),
            Process(13, parentPid: 10, createTime: 150),
        ];

        var group = Assert.Single(ProcessGrouper.Group(processes, WindowsDirectory));

        Assert.Equal(ProcessCategory.App, group.Category);
        Assert.Equal([10, 13, 11, 12], group.Processes.Select(p => p.Counters.Pid)); // Root, then by creation time.
    }

    [Fact]
    public void ChildWithOwnWindowIsSeparateApp()
    {
        ProcessInfo[] processes =
        [
            Process(10, createTime: 100, hasWindow: true),
            Process(11, parentPid: 10, createTime: 200, hasWindow: true),
            Process(12, parentPid: 11, createTime: 300),
        ];

        var groups = ProcessGrouper.Group(processes, WindowsDirectory);

        Assert.Equal(2, groups.Count);
        Assert.Equal([10], Assert.Single(groups, g => g.Root.Counters.Pid == 10).Processes.Select(p => p.Counters.Pid));
        Assert.Equal([11, 12], Assert.Single(groups, g => g.Root.Counters.Pid == 11).Processes.Select(p => p.Counters.Pid));
    }

    [Fact]
    public void ExplorerDoesNotAdoptProcesses()
    {
        ProcessInfo[] processes =
        [
            Process(10, createTime: 100, hasWindow: true, name: "explorer.exe", imagePath: @"C:\Windows\explorer.exe"),
            Process(11, parentPid: 10, createTime: 200, imagePath: @"C:\Program Files\Tray\tray.exe"),
        ];

        var groups = ProcessGrouper.Group(processes, WindowsDirectory);

        Assert.Equal(ProcessCategory.App, Assert.Single(groups, g => g.Root.Counters.Pid == 10).Category);
        Assert.Equal(ProcessCategory.Background, Assert.Single(groups, g => g.Root.Counters.Pid == 11).Category);
    }

    [Fact]
    public void ReusedParentPidIsNotAParent()
    {
        // Process 11 claims parent 10, but the current process 10 was created later.
        ProcessInfo[] processes =
        [
            Process(10, createTime: 500, hasWindow: true),
            Process(11, parentPid: 10, createTime: 100),
        ];

        Assert.Equal(2, ProcessGrouper.Group(processes, WindowsDirectory).Count);
    }

    [Theory]
    [InlineData(@"C:\Windows\System32\svchost.exe", ProcessCategory.Windows)]
    [InlineData(@"c:\windows\explorer.exe", ProcessCategory.Windows)]
    [InlineData(@"C:\WindowsApps\app.exe", ProcessCategory.Background)]
    [InlineData(@"C:\Program Files\App\service.exe", ProcessCategory.Background)]
    [InlineData(null, ProcessCategory.Windows)] // Protected system processes cannot be queried.
    public void CategorizesProcessesWithoutWindows(string? imagePath, ProcessCategory expected) =>
        Assert.Equal(expected, Assert.Single(ProcessGrouper.Group([Process(10, imagePath: imagePath)], WindowsDirectory)).Category);

    [Fact]
    public void BackgroundProcessesAreGroupedUnderTopmostAncestorWithSameImage()
    {
        ProcessInfo[] processes =
        [
            Process(1, createTime: 50, name: "launcher.exe", imagePath: @"C:\Apps\launcher.exe"),
            Process(10, parentPid: 1, createTime: 100, name: "browser.exe", imagePath: @"C:\Apps\browser.exe"),
            Process(11, parentPid: 10, createTime: 200, name: "browser.exe", imagePath: @"C:\Apps\browser.exe"),
            Process(12, parentPid: 11, createTime: 300, name: "BROWSER.EXE", imagePath: @"C:\Apps\browser.exe"),
            Process(13, parentPid: 10, createTime: 400, name: "helper.exe", imagePath: @"C:\Apps\helper.exe"),
        ];

        var groups = ProcessGrouper.Group(processes, WindowsDirectory);

        Assert.Equal(3, groups.Count);
        var browser = Assert.Single(groups, g => g.Root.Counters.Pid == 10);
        Assert.Equal(ProcessCategory.Background, browser.Category);
        Assert.Equal([10, 11, 12], browser.Processes.Select(p => p.Counters.Pid));
    }

    [Fact]
    public void LeavesOutIdleProcess()
    {
        Assert.Empty(ProcessGrouper.Group([Process(0, parentPid: 0, createTime: 0)], WindowsDirectory));
    }

    private static ProcessInfo Process(int pid, int parentPid = 0, long createTime = 100, bool hasWindow = false, string? name = null, string? imagePath = null) =>
        new(Counters(pid, parentPid, createTime) with { Name = name ?? $"process{pid}.exe" }, 0, new ProcessDetails { ImagePath = imagePath })
        {
            Window = hasWindow ? new ProcessWindow(pid, IsHung: false) : null,
        };
}
