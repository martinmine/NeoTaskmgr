using System.Diagnostics;
using NeoTaskmgr.Core.Processes;

namespace NeoTaskmgr.Core.Tests.Processes;

/// <summary>Integration tests that start and manipulate real child processes.</summary>
public sealed class ProcessActionsTests : IDisposable
{
    private readonly List<Process> _started = [];

    public void Dispose()
    {
        foreach (var process in _started)
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }

            process.Dispose();
        }
    }

    [Fact]
    public void End_EndsProcess()
    {
        var process = StartWaitingProcess();

        ProcessActions.End(KeyOf(process));

        Assert.True(process.WaitForExit(5000));
    }

    [Fact]
    public void End_WithWrongCreateTime_DoesNothing()
    {
        var process = StartWaitingProcess();

        ProcessActions.End(KeyOf(process) with { CreateTime = 1 });

        Assert.False(process.WaitForExit(500));
    }

    [Fact]
    public void End_ExitedProcess_DoesNothing()
    {
        var process = StartWaitingProcess();
        var key = KeyOf(process);
        process.Kill();
        process.WaitForExit();

        ProcessActions.End(key);
    }

    [Fact]
    public void EndTree_EndsChildren()
    {
        // cmd.exe starts a child cmd.exe that waits.
        var parent = Start("cmd.exe", "/c cmd.exe /k");
        using var provider = new ProcessProvider();
        var counters = WaitForChild(provider, parent.Id);

        ProcessActions.EndTree(KeyOf(parent), counters);

        Assert.True(parent.WaitForExit(5000));
        var child = counters.Single(p => p.ParentPid == parent.Id);
        Assert.DoesNotContain(provider.Sample(TimeSpan.Zero).Processes, p => p.Key == child.Key);
    }

    [Fact]
    public void SetPriority_ChangesPriorityClass()
    {
        var process = StartWaitingProcess();

        ProcessActions.SetPriority(KeyOf(process), ProcessPriorityClass.BelowNormal);

        process.Refresh();
        Assert.Equal(ProcessPriorityClass.BelowNormal, process.PriorityClass);
    }

    [Fact]
    public void SetAffinity_ChangesAffinity()
    {
        var process = StartWaitingProcess();

        ProcessActions.SetAffinity(KeyOf(process), 1);

        Assert.Equal((1ul, ProcessActions.GetAffinity(KeyOf(process))!.Value.SystemMask), ProcessActions.GetAffinity(KeyOf(process)));
    }

    [Fact]
    public void SetEfficiencyMode_TurnsOnAndOff()
    {
        var process = StartWaitingProcess();
        var key = KeyOf(process);

        ProcessActions.SetEfficiencyMode(key, enabled: true);
        process.Refresh();
        Assert.True(ProcessActions.IsEfficiencyModeEnabled(key));
        Assert.Equal(ProcessPriorityClass.Idle, process.PriorityClass);

        ProcessActions.SetEfficiencyMode(key, enabled: false);
        process.Refresh();
        Assert.False(ProcessActions.IsEfficiencyModeEnabled(key));
        Assert.Equal(ProcessPriorityClass.Normal, process.PriorityClass);
    }

    [Fact]
    public void SuspendAndResume()
    {
        var process = StartWaitingProcess();
        using var provider = new ProcessProvider();
        bool IsSuspended() => provider.Sample(TimeSpan.Zero).Processes.Single(p => p.Key == KeyOf(process)).Counters.IsSuspended;

        ProcessActions.Suspend(KeyOf(process));
        Assert.True(IsSuspended());

        ProcessActions.Resume(KeyOf(process));
        Assert.False(IsSuspended());
    }

    [Fact]
    public void CreateDumpFile_WritesUniqueFiles()
    {
        var process = StartWaitingProcess();
        var directory = Directory.CreateTempSubdirectory("NeoTaskmgr.Tests").FullName;
        try
        {
            var first = ProcessActions.CreateDumpFile(KeyOf(process), "cmd.exe", directory);
            var second = ProcessActions.CreateDumpFile(KeyOf(process), "cmd.exe", directory);

            Assert.Equal(Path.Combine(directory, "cmd.DMP"), first);
            Assert.Equal(Path.Combine(directory, "cmd (2).DMP"), second);
            Assert.True(new FileInfo(first!).Length > 1024 * 1024);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private Process StartWaitingProcess() => Start("cmd.exe", "/k");

    private Process Start(string fileName, string arguments)
    {
        // Redirected stdin keeps cmd.exe waiting for input until it is ended.
        var process = Process.Start(new ProcessStartInfo(fileName, arguments)
        {
            RedirectStandardInput = true,
            CreateNoWindow = true,
            UseShellExecute = false,
        })!;
        _started.Add(process);
        return process;
    }

    private static ProcessKey KeyOf(Process process) => new(process.Id, process.StartTime.ToUniversalTime().ToFileTimeUtc());

    private static IReadOnlyList<ProcessCounters> WaitForChild(ProcessProvider provider, int parentPid)
    {
        for (var attempt = 0; attempt < 50; attempt++)
        {
            var counters = provider.Sample(TimeSpan.Zero).Processes.Select(p => p.Counters).ToList();
            if (counters.Any(p => p.ParentPid == parentPid))
            {
                return counters;
            }

            Thread.Sleep(100);
        }

        throw new TimeoutException("The child process did not start.");
    }
}
