using NeoTaskmgr.Core.Services;

namespace NeoTaskmgr.Core.Tests.Services;

public sealed class ServiceTests
{
    [Theory]
    [InlineData(@"C:\Windows\system32\svchost.exe -k netsvcs -p", "netsvcs")]
    [InlineData(@"C:\Windows\System32\svchost.exe -K LocalServiceNoNetwork", "LocalServiceNoNetwork")]
    [InlineData(@"""C:\Program Files\App\service.exe"" --start", null)]
    [InlineData(@"C:\Windows\system32\lsass.exe", null)]
    [InlineData(null, null)]
    public void ParseGroup(string? binaryPath, string? expected) => Assert.Equal(expected, ServiceGroup.Parse(binaryPath));

    /// <summary>Integration test against the real service list.</summary>
    [Fact]
    public void Sample_ListsServices()
    {
        using var provider = new ServiceProvider();

        var services = provider.Sample();

        // The RPC endpoint mapper runs on every Windows system, in a svchost group.
        var rpc = Assert.Single(services, s => s.Name.Equals("RpcEptMapper", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(ServiceState.Running, rpc.State);
        Assert.NotEqual(0, rpc.Pid);
        Assert.NotNull(rpc.Group);
        Assert.NotEmpty(rpc.DisplayName);
        Assert.Contains(services, s => s.State == ServiceState.Stopped && s.Pid == 0);
    }
}
