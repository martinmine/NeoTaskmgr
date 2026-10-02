using NeoTaskmgr.Core.Settings;

namespace NeoTaskmgr.Core.Tests.Settings;

public sealed class SettingsStoreTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "NeoTaskmgr.Tests", Guid.NewGuid().ToString("N"));

    private string FilePath => Path.Combine(_directory, "settings.json");

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Fact]
    public void Load_MissingFile_ReturnsDefaults()
    {
        var settings = new SettingsStore(FilePath).Load();

        Assert.Equal(new AppSettings(), settings);
        Assert.Equal(AppPage.Processes, settings.StartPage);
        Assert.Equal(UpdateSpeed.Normal, settings.UpdateSpeed);
        Assert.Equal(AppTheme.System, settings.Theme);
        Assert.False(settings.AlwaysOnTop);
    }

    [Fact]
    public void Save_ThenLoad_RoundTrips()
    {
        var store = new SettingsStore(FilePath);
        var saved = new AppSettings
        {
            StartPage = AppPage.Services,
            UpdateSpeed = UpdateSpeed.Paused,
            AlwaysOnTop = true,
            Theme = AppTheme.Dark,
        };

        Assert.True(store.Save(saved));

        Assert.Equal(saved, store.Load());
    }

    [Fact]
    public void Save_ThenLoad_RoundTripsWindowAndTables()
    {
        var store = new SettingsStore(FilePath);
        var saved = new AppSettings(
            MinimizeOnUse: true,
            Window: new WindowPlacement(10, 20, 1200, 800, IsMaximized: true),
            Tables: new Dictionary<string, TableLayout>
            {
                ["Details"] = new([new ColumnLayout("Name", 250, true), new ColumnLayout("Pid", 70, false)], "Pid", SortDescending: true),
            });

        store.Save(saved);
        var loaded = store.Load();

        // Records compare collections by reference, so compare the parts.
        Assert.True(loaded.MinimizeOnUse);
        Assert.Equal(saved.Window, loaded.Window);
        var table = loaded.Tables!["Details"];
        Assert.Equal(saved.Tables!["Details"].Columns, table.Columns);
        Assert.Equal(("Pid", true), (table.SortColumn, table.SortDescending));
    }

    [Fact]
    public void Save_WritesEnumsAsStrings()
    {
        var store = new SettingsStore(FilePath);

        store.Save(new AppSettings { StartPage = AppPage.Details });

        Assert.Contains("\"Details\"", File.ReadAllText(FilePath));
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("{ \"StartPage\": \"NoSuchPage\" }")]
    public void Load_CorruptFile_ReturnsDefaults(string content)
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllText(FilePath, content);

        Assert.Equal(new AppSettings(), new SettingsStore(FilePath).Load());
    }

    [Fact]
    public void Load_PartialFile_UsesDefaultsForMissingValues()
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllText(FilePath, "{ \"AlwaysOnTop\": true }");

        Assert.Equal(new AppSettings { AlwaysOnTop = true }, new SettingsStore(FilePath).Load());
    }
}
