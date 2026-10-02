using NeoTaskmgr.Core.Performance;

namespace NeoTaskmgr.Core.Tests.Performance;

/// <summary>Integration tests against the real system.</summary>
public sealed class PerformanceProviderTests
{
    [Fact]
    public void Sample_ReportsPlausibleValues()
    {
        using var provider = new PerformanceProvider();
        provider.Sample(TimeSpan.Zero);
        Thread.Sleep(500);

        var (snapshot, _) = provider.Sample(TimeSpan.FromMilliseconds(500));

        var cpu = snapshot.Cpu;
        Assert.NotEmpty(cpu.Info.Name);
        Assert.Equal(Environment.ProcessorCount, cpu.Info.LogicalProcessors);
        Assert.InRange(cpu.Info.Cores, 1, cpu.Info.LogicalProcessors);
        Assert.True(cpu.Info.Sockets >= 1);
        Assert.True(cpu.Info.L2CacheBytes > 0);
        Assert.Equal(cpu.Info.LogicalProcessors, cpu.ProcessorUsagePercent.Count);
        Assert.InRange(cpu.UsagePercent, 0, 100);
        Assert.True(cpu.SpeedMhz > 0);

        var memory = snapshot.Memory;
        Assert.InRange(memory.AvailablePhysical, 1, memory.TotalPhysical);
        Assert.True(memory.Info.InstalledBytes >= memory.TotalPhysical);
        Assert.InRange(memory.Committed, 1, memory.CommitLimit);
        Assert.True(memory.Standby + memory.Free + memory.Modified > 0);

        var systemDisk = Assert.Single(snapshot.Disks, d => d.Info.IsSystemDisk);
        Assert.InRange(systemDisk.ActivePercent, 0, 100);
        Assert.True(systemDisk.Info.FormattedBytes > 0);
        Assert.NotEqual(DiskKind.Unknown, systemDisk.Info.Kind);

        Assert.All(snapshot.Gpus, gpu => Assert.InRange(gpu.UtilizationPercent, 0, 100));
    }
}
