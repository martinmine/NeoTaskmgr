using NeoTaskmgr.Core.Processes;

namespace NeoTaskmgr.Core.Users;

public enum UserSessionState
{
    Active,
    Disconnected,
}

/// <summary>A signed-in user's session, a row of the Users page.</summary>
/// <param name="UserName">The account name without domain, as <see cref="ProcessDetails.UserName"/> has it.</param>
/// <param name="DisplayName">The Microsoft account's e-mail address, else the full name, else the account name.</param>
/// <param name="SessionName">The window station, such as "Console" or "RDP-Tcp#0"; empty while disconnected.</param>
/// <param name="ClientName">The computer a remote session is connected from; empty for local sessions.</param>
/// <param name="PicturePath">The account picture, or null if there is none.</param>
public sealed record UserSession(
    int SessionId,
    string UserName,
    string Domain,
    string DisplayName,
    UserSessionState State,
    string SessionName,
    string ClientName,
    string? PicturePath);

public static class UserProcesses
{
    /// <summary>
    /// The processes each session's user runs in that session, by session ID. Processes of other accounts in the
    /// session (such as the Desktop Window Manager) are left out, and so are processes whose user is unknown
    /// (other users' processes, without administrator rights).
    /// </summary>
    public static Dictionary<int, List<ProcessInfo>> Group(IReadOnlyList<UserSession> sessions, IEnumerable<ProcessInfo> processes)
    {
        var result = sessions.ToDictionary(s => s.SessionId, _ => new List<ProcessInfo>());
        var userNames = sessions.ToDictionary(s => s.SessionId, s => s.UserName);
        foreach (var process in processes)
        {
            if (userNames.TryGetValue(process.Counters.SessionId, out var userName) &&
                string.Equals(process.Details.UserName, userName, StringComparison.OrdinalIgnoreCase))
            {
                result[process.Counters.SessionId].Add(process);
            }
        }

        return result;
    }
}
