using System.Diagnostics;
using NeoTaskmgr.Core.Processes;

namespace NeoTaskmgr.Core.Tests.Processes;

/// <summary>Integration tests against the real process list.</summary>
public sealed class ProcessProviderTests
{
    [Fact]
    public void Sample_IncludesCurrentProcessWithDetails()
    {
        using var provider = new ProcessProvider();

        var snapshot = provider.Sample(TimeSpan.Zero);

        var self = Assert.Single(snapshot.Processes, p => p.Counters.Pid == Environment.ProcessId);
        Assert.Equal(Path.GetFileName(Environment.ProcessPath), self.Counters.Name, ignoreCase: true);
        Assert.Equal(Environment.ProcessPath, self.Details.ImagePath, ignoreCase: true);
        Assert.Equal(Environment.UserName, self.Details.UserName, ignoreCase: true);
        Assert.Contains(Environment.ProcessPath!, self.Details.CommandLine, StringComparison.OrdinalIgnoreCase);
        Assert.NotNull(self.Details.Architecture);
        Assert.True(self.Counters.ThreadCount > 0);
        Assert.True(self.Counters.PrivateWorkingSet > 0);
        Assert.False(self.Counters.IsSuspended);

        using var current = Process.GetCurrentProcess();
        Assert.Equal(current.StartTime.ToUniversalTime(), DateTime.FromFileTimeUtc(self.Counters.CreateTime));
    }

    [Fact]
    public void Sample_IncludesIdleAndSystemProcesses()
    {
        using var provider = new ProcessProvider();

        var processes = provider.Sample(TimeSpan.Zero).Processes;

        Assert.Equal("System Idle Process", Assert.Single(processes, p => p.Counters.Pid == 0).Counters.Name);
        Assert.Equal("System", Assert.Single(processes, p => p.Counters.Pid == 4).Counters.Name);
    }

    [Fact]
    public void SecondSample_ReportsCpuUsage()
    {
        using var provider = new ProcessProvider();
        provider.Sample(TimeSpan.Zero);
        Thread.Sleep(200);

        var processes = provider.Sample(TimeSpan.FromMilliseconds(200)).Processes;

        // Across all processes (including idle) the usage adds up to roughly 100%.
        Assert.InRange(processes.Sum(p => p.CpuPercent), 50, 150);
    }
}
