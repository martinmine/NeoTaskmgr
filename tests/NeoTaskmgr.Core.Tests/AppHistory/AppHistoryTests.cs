using NeoTaskmgr.Core.AppHistory;
using NeoTaskmgr.Core.Interop;

namespace NeoTaskmgr.Core.Tests.AppHistory;

public sealed class AppHistoryTests
{
    private const string User = "S-1-5-21-1-2-3-1001";
    private static readonly DateTime Time = new(2026, 9, 1, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void SumsProvidersPerApp()
    {
        AppUsageRecord[] records =
        [
            Program(@"\Device\HarddiskVolume3\Tools\tool.exe", Time) with { CycleTime = 3_000_000_000 },
            Program(@"\Device\HarddiskVolume3\Tools\tool.exe", Time.AddHours(1)) with { CycleTime = 1_000_000_000 },
            Program(@"\Device\HarddiskVolume3\Tools\tool.exe", Time) with { BytesSent = 10, BytesReceived = 20 },
            new(Time, SruAppKind.Package, "Contoso.Mail_1.0.0.0_x64__abc123", User, NotificationBytes: 5),
        ];

        var entries = Aggregate(records);

        var tool = Assert.Single(entries, e => e.Kind == AppHistoryKind.Program);
        Assert.Equal("tool.exe", tool.Name);
        Assert.Equal(TimeSpan.FromSeconds(4), tool.CpuTime); // 4 billion cycles at 1 GHz.
        Assert.Equal(30, tool.NetworkBytes);
        Assert.Equal(@"C:\Tools\tool.exe", tool.ImagePath);

        var mail = Assert.Single(entries, e => e.Kind == AppHistoryKind.Package);
        Assert.Equal("Contoso.Mail", mail.Name);
        Assert.Equal("CONTOSO.MAIL_ABC123", mail.PackageFamilyName);
        Assert.Equal(5, mail.NotificationBytes);
    }

    [Fact]
    public void MergesVersionsOfAProgram()
    {
        AppUsageRecord[] records =
        [
            Program(@"\Device\HarddiskVolume3\Users\me\AppData\Local\App\app-1.2.3\app.exe", Time) with { CycleTime = 1_000_000_000 },
            Program(@"\device\harddiskvolume3\users\me\appdata\local\app\app-1.3.0\app.exe", Time.AddDays(1)) with { BytesSent = 1 },
            Program(@"\Device\HarddiskVolume3\Code\04c0d99f4f\helper.exe", Time),
            Program(@"\Device\HarddiskVolume3\Code\07f806f999\helper.exe", Time),
        ];

        var entries = Aggregate(records);

        Assert.Equal(2, entries.Count);
        var app = Assert.Single(entries, e => e.Name == "app.exe"); // The original casing is kept.
        Assert.Equal(TimeSpan.FromSeconds(1), app.CpuTime);
        Assert.Equal(@"C:\users\me\appdata\local\app\app-1.3.0\app.exe", app.ImagePath); // The newest version.
    }

    [Fact]
    public void KeepsProgramsInDifferentFoldersApart()
    {
        AppUsageRecord[] records =
        [
            Program(@"\Device\HarddiskVolume3\Windows\System32\bash.exe", Time),
            Program(@"\Device\HarddiskVolume3\Program Files\Git\usr\bin\bash.exe", Time),
        ];

        Assert.Equal(2, Aggregate(records).Count);
    }

    [Fact]
    public void PackagesAreKeyedByFamily()
    {
        Assert.Equal(
            AppHistoryAggregator.GetKey(SruAppKind.Package, "Contoso.Mail_1.0.0.0_x64__abc123"),
            AppHistoryAggregator.GetKey(SruAppKind.Package, "Contoso.Mail_2.0.0.0_neutral_~_abc123"));
        Assert.Equal("Contoso.Mail_abc123", AppHistoryAggregator.GetPackageFamilyName("Contoso.Mail_1.0.0.0_x64__abc123"));
    }

    [Fact]
    public void SkipsOtherUsers()
    {
        AppUsageRecord[] records =
        [
            Program(@"\Device\HarddiskVolume3\a.exe", Time, sid: User),
            Program(@"\Device\HarddiskVolume3\b.exe", Time, sid: "S-1-5-19"),
            Program(@"\Device\HarddiskVolume3\c.exe", Time, sid: "S-1-5-21-1-2-3-1002"),
        ];

        var entries = AppHistoryAggregator.Aggregate(
            records, 1e9, sid => AppHistoryProvider.IsIncluded(sid, User), _ => null);

        Assert.Equal(["a.exe", "b.exe"], entries.Select(e => e.Name).Order());
    }

    [Fact]
    public void ConvertsDevicePaths()
    {
        var converter = new DevicePathConverter(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [@"\Device\HarddiskVolume3"] = "C:",
        });

        Assert.Equal(@"C:\Windows\explorer.exe", converter.ToDosPath(@"\Device\HarddiskVolume3\Windows\explorer.exe"));
        Assert.Null(converter.ToDosPath(@"\Device\HarddiskVolume30\x.exe"));
        Assert.Null(converter.ToDosPath("System"));
    }

    private static AppUsageRecord Program(string path, DateTime time, string sid = User) => new(time, SruAppKind.Program, path, sid);

    private static List<AppHistoryEntry> Aggregate(IEnumerable<AppUsageRecord> records) =>
        AppHistoryAggregator.Aggregate(records, 1e9, _ => true,
            path => path.StartsWith(@"\Device\HarddiskVolume3", StringComparison.OrdinalIgnoreCase) ? "C:" + path[@"\Device\HarddiskVolume3".Length..] : null);
}
