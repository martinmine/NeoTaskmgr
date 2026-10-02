using NeoTaskmgr.Core.Processes;
using NeoTaskmgr.Core.Users;
using static NeoTaskmgr.Core.Tests.Processes.TestProcesses;

namespace NeoTaskmgr.Core.Tests.Users;

public sealed class UserProcessesTests
{
    [Fact]
    public void GroupsProcessesBySessionAndUser()
    {
        UserSession[] sessions = [Session(1, "alice"), Session(2, "bob")];
        ProcessInfo[] processes =
        [
            Process(10, sessionId: 1, userName: "alice"),
            Process(11, sessionId: 2, userName: "BOB"), // Account names are case-insensitive.
            Process(12, sessionId: 1, userName: "DWM-1"), // Another account in the user's session.
            Process(13, sessionId: 1, userName: null), // Unknown user (access denied).
            Process(14, sessionId: 2, userName: "alice"), // The user, but in another session.
            Process(15, sessionId: 0, userName: "SYSTEM"),
        ];

        var groups = UserProcesses.Group(sessions, processes);

        Assert.Equal([10], groups[1].Select(p => p.Counters.Pid));
        Assert.Equal([11], groups[2].Select(p => p.Counters.Pid));
    }

    [Fact]
    public void SessionWithoutProcessesHasEmptyList()
    {
        var groups = UserProcesses.Group([Session(3, "carol")], []);

        Assert.Empty(groups[3]);
    }

    private static UserSession Session(int id, string userName) =>
        new(id, userName, "PC", userName, UserSessionState.Active, "Console", string.Empty, null);

    private static ProcessInfo Process(int pid, int sessionId, string? userName) =>
        new(Counters(pid) with { SessionId = sessionId }, 0, new ProcessDetails { UserName = userName });
}
