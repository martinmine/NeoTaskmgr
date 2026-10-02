namespace NeoTaskmgr.Core.Startup;

public static class CommandLineParser
{
    /// <summary>
    /// Finds the program a command line runs, such as <c>"C:\Program Files\App\app.exe" --minimized</c>, an unquoted
    /// path with spaces, or a program in the system folder (<c>rundll32.exe ...</c>). Environment variables are expanded.
    /// </summary>
    /// <returns>The full path of an existing file, or null if it cannot be found.</returns>
    public static string? GetExecutablePath(string commandLine)
    {
        var command = Environment.ExpandEnvironmentVariables(commandLine).Trim();

        // Some installers store the whole command quoted, with escaped inner quotes: "\"C:\app.exe\" --startup".
        if (command.StartsWith("\"\\\"", StringComparison.Ordinal) && command.EndsWith('"'))
        {
            command = command[1..^1].Replace("\\\"", "\"", StringComparison.Ordinal);
        }

        if (command.StartsWith('"'))
        {
            var end = command.IndexOf('"', 1);
            return Find(end < 0 ? command.Trim('"') : command[1..end]);
        }

        // Without quotes the path may contain spaces, so try the longest prefix that is an existing file first.
        var parts = command.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        for (var count = parts.Length; count > 0; count--)
        {
            if (Find(string.Join(' ', parts[..count])) is { } path)
            {
                return path;
            }
        }

        return null;
    }

    private static string? Find(string file)
    {
        string[] candidates = Path.HasExtension(file) ? [file] : [file, file + ".exe"];
        foreach (var candidate in candidates)
        {
            if (Path.IsPathFullyQualified(candidate))
            {
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }
            else if (!candidate.Contains(Path.DirectorySeparatorChar))
            {
                var inSystemFolder = Path.Combine(Environment.SystemDirectory, candidate);
                if (File.Exists(inSystemFolder))
                {
                    return inSystemFolder;
                }
            }
        }

        return null;
    }
}
