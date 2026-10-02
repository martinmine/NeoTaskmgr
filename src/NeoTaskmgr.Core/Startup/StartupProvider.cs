using System.Diagnostics;
using Microsoft.Win32;

namespace NeoTaskmgr.Core.Startup;

/// <summary>
/// Reads and changes the programs that start when the user signs in: the Run registry keys and the Startup folders.
/// The roots are parameters so tests can use a scratch registry key and folders.
/// </summary>
public sealed class StartupProvider(RegistryKey currentUser, RegistryKey localMachine, string userStartupFolder, string commonStartupFolder)
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string Run32Key = @"Software\WOW6432Node\Microsoft\Windows\CurrentVersion\Run";
    private const string ApprovedKey = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved";

    /// <summary>The provider for the current user and machine.</summary>
    public static StartupProvider CreateDefault() => new(
        Registry.CurrentUser,
        RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64),
        Environment.GetFolderPath(Environment.SpecialFolder.Startup),
        Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup));

    public List<StartupEntry> Read()
    {
        var entries = new List<StartupEntry>();
        ReadRunKey(entries, currentUser, RunKey, StartupSource.CurrentUserRun);
        ReadRunKey(entries, localMachine, RunKey, StartupSource.LocalMachineRun);
        ReadRunKey(entries, localMachine, Run32Key, StartupSource.LocalMachineRun32);
        ReadFolder(entries, userStartupFolder, StartupSource.CurrentUserStartupFolder);
        ReadFolder(entries, commonStartupFolder, StartupSource.CommonStartupFolder);
        return entries;
    }

    /// <summary>Enables or disables the entry the way Task Manager does, without removing it.</summary>
    /// <exception cref="UnauthorizedAccessException">The entry is for all users and the app is not running as administrator.</exception>
    public void SetEnabled(StartupEntry entry, bool enabled)
    {
        var (root, approvedSubkey) = GetApproval(entry.Source);
        using var key = root.CreateSubKey($@"{ApprovedKey}\{approvedSubkey}", writable: true);
        key.SetValue(entry.ApprovalName, StartupApproval.Create(enabled, DateTime.UtcNow), RegistryValueKind.Binary);
    }

    private void ReadRunKey(List<StartupEntry> entries, RegistryKey root, string keyPath, StartupSource source)
    {
        using var key = root.OpenSubKey(keyPath);
        if (key is null)
        {
            return;
        }

        foreach (var name in key.GetValueNames().Where(n => n.Length > 0))
        {
            if (key.GetValue(name) is string command)
            {
                var target = CommandLineParser.GetExecutablePath(command);
                entries.Add(CreateEntry(name, command, target, source, $@"{root.Name}\{keyPath}", name));
            }
        }
    }

    private void ReadFolder(List<StartupEntry> entries, string folder, StartupSource source)
    {
        if (!Directory.Exists(folder))
        {
            return;
        }

        foreach (var file in Directory.EnumerateFiles(folder))
        {
            var fileName = Path.GetFileName(file);
            if (fileName.Equals("desktop.ini", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var isShortcut = Path.GetExtension(file).Equals(".lnk", StringComparison.OrdinalIgnoreCase);
            var (target, arguments) = isShortcut ? ShortcutReader.Read(file) : (file, null);
            var command = target is null ? file : arguments is null ? $"\"{target}\"" : $"\"{target}\" {arguments}";
            var name = isShortcut ? Path.GetFileNameWithoutExtension(file) : fileName;
            entries.Add(CreateEntry(name, command, target, source, folder, fileName));
        }
    }

    private StartupEntry CreateEntry(string name, string command, string? target, StartupSource source, string location, string approvalName)
    {
        var (root, approvedSubkey) = GetApproval(source);
        using var approved = root.OpenSubKey($@"{ApprovedKey}\{approvedSubkey}");
        var isEnabled = StartupApproval.IsEnabled(approved?.GetValue(approvalName) as byte[]);
        var (description, publisher) = ReadVersionInfo(target);
        return new StartupEntry(name, command, target, source, location, approvalName, isEnabled, description, publisher);
    }

    /// <summary>The hive and StartupApproved subkey that store the enabled state of entries from a source.</summary>
    private (RegistryKey Root, string Subkey) GetApproval(StartupSource source) => source switch
    {
        StartupSource.CurrentUserRun => (currentUser, "Run"),
        StartupSource.LocalMachineRun => (localMachine, "Run"),
        StartupSource.LocalMachineRun32 => (localMachine, "Run32"),
        StartupSource.CurrentUserStartupFolder => (currentUser, "StartupFolder"),
        StartupSource.CommonStartupFolder => (localMachine, "StartupFolder"),
        _ => throw new ArgumentOutOfRangeException(nameof(source)),
    };

    private static (string? Description, string? Publisher) ReadVersionInfo(string? path)
    {
        if (path is null || !File.Exists(path))
        {
            return default;
        }

        var info = FileVersionInfo.GetVersionInfo(path);
        return (NullIfEmpty(info.FileDescription), NullIfEmpty(info.CompanyName));
    }

    private static string? NullIfEmpty(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
