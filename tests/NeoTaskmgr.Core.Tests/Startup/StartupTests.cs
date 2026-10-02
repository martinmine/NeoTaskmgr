using System.Diagnostics;
using Microsoft.Win32;
using NeoTaskmgr.Core.Startup;

namespace NeoTaskmgr.Core.Tests.Startup;

public sealed class StartupApprovalTests
{
    [Theory]
    [InlineData(null, true)]
    [InlineData(new byte[0], true)]
    [InlineData(new byte[] { 0x02, 0, 0, 0 }, true)]
    [InlineData(new byte[] { 0x06, 0, 0, 0 }, true)]
    [InlineData(new byte[] { 0x03, 0, 0, 0 }, false)]
    [InlineData(new byte[] { 0x07, 0, 0, 0 }, false)]
    public void IsEnabled(byte[]? value, bool expected) => Assert.Equal(expected, StartupApproval.IsEnabled(value));

    [Fact]
    public void Create_Enabled() => Assert.Equal([0x02, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0], StartupApproval.Create(true, DateTime.UtcNow));

    [Fact]
    public void Create_Disabled_StoresTime()
    {
        var now = new DateTime(2026, 10, 2, 12, 0, 0, DateTimeKind.Utc);

        var value = StartupApproval.Create(false, now);

        Assert.Equal(0x03, value[0]);
        Assert.Equal(now.ToFileTimeUtc(), BitConverter.ToInt64(value, 4));
    }
}

public sealed class CommandLineParserTests : IDisposable
{
    private readonly string _directory = Directory.CreateTempSubdirectory("NeoTaskmgr Tests ").FullName; // Contains a space.

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    private static string Cmd => Path.Combine(Environment.SystemDirectory, "cmd.exe");

    [Fact]
    public void QuotedPath() => Assert.Equal(Cmd, CommandLineParser.GetExecutablePath($"\"{Cmd}\" /c exit"), ignoreCase: true);

    [Fact]
    public void WholeCommandQuotedWithEscapedQuotes() =>
        Assert.Equal(Cmd, CommandLineParser.GetExecutablePath($"\"\\\"{Cmd}\\\" --startup\""), ignoreCase: true);

    [Fact]
    public void UnquotedPathWithSpaces()
    {
        var program = Path.Combine(_directory, "my app.exe");
        File.WriteAllText(program, string.Empty);

        Assert.Equal(program, CommandLineParser.GetExecutablePath($"{program} --minimized"));
    }

    [Theory]
    [InlineData("rundll32.exe shell32.dll,Control_RunDLL")]
    [InlineData("cmd /c exit")]
    [InlineData("%SystemRoot%\\System32\\cmd.exe /c exit")]
    public void FindsSystemPrograms(string commandLine)
    {
        var expected = Path.Combine(Environment.SystemDirectory, commandLine.Split(' ')[0].Replace("%SystemRoot%\\System32\\", string.Empty));

        Assert.Equal(Path.HasExtension(expected) ? expected : expected + ".exe", CommandLineParser.GetExecutablePath(commandLine), ignoreCase: true);
    }

    [Theory]
    [InlineData("C:\\does not exist\\app.exe --x")]
    [InlineData("nosuchprogram123 /arg")]
    [InlineData("")]
    public void UnknownProgram_ReturnsNull(string commandLine) => Assert.Null(CommandLineParser.GetExecutablePath(commandLine));
}

/// <summary>Uses a scratch registry key (under HKCU) and temp folders in place of the real hives and Startup folders.</summary>
public sealed class StartupProviderTests : IDisposable
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ApprovedKey = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved";

    private readonly string _scratchKey = $@"Software\NeoTaskmgr.Tests\{Guid.NewGuid():N}";
    private readonly RegistryKey _currentUser;
    private readonly RegistryKey _localMachine;
    private readonly string _userFolder = Directory.CreateTempSubdirectory("NeoTaskmgr.Tests").FullName;
    private readonly string _commonFolder = Directory.CreateTempSubdirectory("NeoTaskmgr.Tests").FullName;
    private readonly StartupProvider _provider;

    public StartupProviderTests()
    {
        _currentUser = Registry.CurrentUser.CreateSubKey($@"{_scratchKey}\HKCU");
        _localMachine = Registry.CurrentUser.CreateSubKey($@"{_scratchKey}\HKLM");
        _provider = new StartupProvider(_currentUser, _localMachine, _userFolder, _commonFolder);
    }

    private static string Cmd => Path.Combine(Environment.SystemDirectory, "cmd.exe");

    public void Dispose()
    {
        _currentUser.Dispose();
        _localMachine.Dispose();
        Registry.CurrentUser.DeleteSubKeyTree(_scratchKey);
        Directory.Delete(_userFolder, recursive: true);
        Directory.Delete(_commonFolder, recursive: true);
    }

    [Fact]
    public void Read_FindsRunKeysAndStartupFolders()
    {
        SetValue(_currentUser, RunKey, "UserApp", $"\"{Cmd}\" /k");
        SetValue(_localMachine, RunKey, "MachineApp", $"{Cmd} /k");
        SetValue(_localMachine, @"Software\WOW6432Node\Microsoft\Windows\CurrentVersion\Run", "Machine32App", "cmd.exe");
        CreateShortcut(Path.Combine(_userFolder, "Folder App.lnk"), Cmd, "/k echo hi");
        File.WriteAllText(Path.Combine(_userFolder, "desktop.ini"), string.Empty);

        var entries = _provider.Read();

        Assert.Equal(
            ["UserApp", "MachineApp", "Machine32App", "Folder App"],
            entries.Select(e => e.Name));
        Assert.All(entries, e => Assert.Equal(Cmd, e.TargetPath, ignoreCase: true));
        Assert.All(entries, e => Assert.True(e.IsEnabled));
        Assert.All(entries, e => Assert.Equal("Windows Command Processor", e.Description));
        Assert.All(entries, e => Assert.Equal("Microsoft Corporation", e.Publisher));

        var shortcut = entries.Single(e => e.Name == "Folder App");
        Assert.Equal(StartupSource.CurrentUserStartupFolder, shortcut.Source);
        Assert.Equal("Folder App.lnk", shortcut.ApprovalName);
        Assert.EndsWith("/k echo hi", shortcut.Command);
    }

    [Fact]
    public void Read_UsesStartupApprovedState()
    {
        SetValue(_currentUser, RunKey, "Enabled", Cmd);
        SetValue(_currentUser, RunKey, "Disabled", Cmd);
        using (var approved = _currentUser.CreateSubKey($@"{ApprovedKey}\Run"))
        {
            approved.SetValue("Disabled", new byte[] { 0x03, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 }, RegistryValueKind.Binary);
        }

        var entries = _provider.Read();

        Assert.True(entries.Single(e => e.Name == "Enabled").IsEnabled);
        Assert.False(entries.Single(e => e.Name == "Disabled").IsEnabled);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SetEnabled_WritesStartupApproved(bool isFolderEntry)
    {
        if (isFolderEntry)
        {
            CreateShortcut(Path.Combine(_commonFolder, "App.lnk"), Cmd, null);
        }
        else
        {
            SetValue(_localMachine, @"Software\WOW6432Node\Microsoft\Windows\CurrentVersion\Run", "App", Cmd);
        }

        var entry = Assert.Single(_provider.Read());

        _provider.SetEnabled(entry, enabled: false);
        Assert.False(Assert.Single(_provider.Read()).IsEnabled);

        _provider.SetEnabled(entry, enabled: true);
        Assert.True(Assert.Single(_provider.Read()).IsEnabled);
    }

    [Fact]
    public void ParseShortcut_MalformedData_ReturnsNothing() =>
        Assert.Equal(default, ShortcutReader.Parse([0x4C, 0, 0, 0, 1, 2, 3], _userFolder));

    private static void SetValue(RegistryKey root, string keyPath, string name, string value)
    {
        using var key = root.CreateSubKey(keyPath);
        key.SetValue(name, value);
    }

    /// <summary>Creates a real shell link using the Windows Script Host.</summary>
    private static void CreateShortcut(string path, string target, string? arguments)
    {
        var script = $"$s = (New-Object -ComObject WScript.Shell).CreateShortcut('{path}'); $s.TargetPath = '{target}'; $s.Arguments = '{arguments}'; $s.Save()";
        using var process = Process.Start(new ProcessStartInfo("powershell.exe", ["-NoProfile", "-Command", script]) { CreateNoWindow = true })!;
        process.WaitForExit();
        Assert.True(File.Exists(path), "The shortcut was not created.");
    }
}
