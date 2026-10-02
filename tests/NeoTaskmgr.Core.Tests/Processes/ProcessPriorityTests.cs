using System.Diagnostics;
using NeoTaskmgr.Core.Processes;

namespace NeoTaskmgr.Core.Tests.Processes;

public sealed class ProcessPriorityTests
{
    [Theory]
    [InlineData(4, ProcessPriorityClass.Idle)]
    [InlineData(6, ProcessPriorityClass.BelowNormal)]
    [InlineData(8, ProcessPriorityClass.Normal)]
    [InlineData(10, ProcessPriorityClass.AboveNormal)]
    [InlineData(13, ProcessPriorityClass.High)]
    [InlineData(24, ProcessPriorityClass.RealTime)]
    public void FromBasePriority_MapsKnownValues(int basePriority, ProcessPriorityClass expected) =>
        Assert.Equal(expected, ProcessPriority.FromBasePriority(basePriority));

    [Fact]
    public void FromBasePriority_UnknownValue_ReturnsNull() => Assert.Null(ProcessPriority.FromBasePriority(0));
}
