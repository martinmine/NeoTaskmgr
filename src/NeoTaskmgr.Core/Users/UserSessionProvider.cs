using System.Security.Principal;
using Microsoft.Win32;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.NetworkManagement.NetManagement;
using Windows.Win32.System.RemoteDesktop;

namespace NeoTaskmgr.Core.Users;

/// <summary>
/// Reads the sessions of signed-in users from the Remote Desktop Services API (which also covers the local console).
/// Account names and pictures are looked up once per account.
/// </summary>
public sealed unsafe class UserSessionProvider
{
    private const string AccountPicturesKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\AccountPicture\Users";

    private readonly Dictionary<(string Domain, string UserName), (string DisplayName, string? PicturePath)> _accounts = [];

    public IReadOnlyList<UserSession> Sample()
    {
        var level = 1u;
        WTS_SESSION_INFO_1W* sessions;
        uint count;
        if (!PInvoke.WTSEnumerateSessionsEx(HANDLE.Null, &level, 0, &sessions, &count))
        {
            return [];
        }

        try
        {
            var result = new List<UserSession>();
            for (var i = 0; i < count; i++)
            {
                var session = sessions[i];
                var userName = session.pUserName.ToString();
                var state = session.State switch
                {
                    WTS_CONNECTSTATE_CLASS.WTSActive => UserSessionState.Active,
                    WTS_CONNECTSTATE_CLASS.WTSDisconnected => UserSessionState.Disconnected,
                    _ => (UserSessionState?)null, // Listeners, and sessions being set up or torn down.
                };
                if (string.IsNullOrEmpty(userName) || state is null)
                {
                    continue;
                }

                var domain = session.pDomainName.ToString() ?? string.Empty;
                var (displayName, picturePath) = GetAccount(domain, userName);
                result.Add(new UserSession(
                    (int)session.SessionId,
                    userName,
                    domain,
                    displayName,
                    state.Value,
                    session.pSessionName.ToString() ?? string.Empty,
                    QueryString(session.SessionId, WTS_INFO_CLASS.WTSClientName),
                    picturePath));
            }

            return result;
        }
        finally
        {
            PInvoke.WTSFreeMemoryEx(WTS_TYPE_CLASS.WTSTypeSessionInfoLevel1, sessions, count);
        }
    }

    private static string QueryString(uint sessionId, WTS_INFO_CLASS info)
    {
        PWSTR buffer;
        uint size;
        if (!PInvoke.WTSQuerySessionInformation(HANDLE.Null, sessionId, info, &buffer, &size))
        {
            return string.Empty;
        }

        try
        {
            return buffer.ToString() ?? string.Empty;
        }
        finally
        {
            PInvoke.WTSFreeMemory(buffer);
        }
    }

    private (string DisplayName, string? PicturePath) GetAccount(string domain, string userName)
    {
        if (!_accounts.TryGetValue((domain, userName), out var account))
        {
            // Only local accounts (including Microsoft accounts) are described by this computer.
            var isLocal = domain.Equals(Environment.MachineName, StringComparison.OrdinalIgnoreCase);
            account = ((isLocal ? GetLocalDisplayName(userName) : null) ?? userName, GetPicturePath(domain, userName));
            _accounts[(domain, userName)] = account;
        }

        return account;
    }

    /// <summary>The e-mail address of a Microsoft account, else the full name if one is set.</summary>
    private static string? GetLocalDisplayName(string userName)
    {
        byte* buffer;
        if (PInvoke.NetUserGetInfo(null, userName, 24, out buffer) == 0)
        {
            try
            {
                var info = (USER_INFO_24*)buffer;
                if (info->usri24_internet_identity && NullIfEmpty(info->usri24_internet_principal_name.ToString()) is { } principalName)
                {
                    return principalName;
                }
            }
            finally
            {
                PInvoke.NetApiBufferFree(buffer);
            }
        }

        if (PInvoke.NetUserGetInfo(null, userName, 10, out buffer) == 0)
        {
            try
            {
                return NullIfEmpty(((USER_INFO_10*)buffer)->usri10_full_name.ToString());
            }
            finally
            {
                PInvoke.NetApiBufferFree(buffer);
            }
        }

        return null;
    }

    /// <summary>The account picture Windows keeps for the sign-in screen, if the account has one.</summary>
    private static string? GetPicturePath(string domain, string userName)
    {
        try
        {
            var sid = (SecurityIdentifier)new NTAccount(domain, userName).Translate(typeof(SecurityIdentifier));
            using var key = Registry.LocalMachine.OpenSubKey($@"{AccountPicturesKey}\{sid.Value}");
            // 48 px stays sharp when the 16 px picture is shown at up to 300% scaling.
            return new[] { "Image48", "Image32", "Image64", "Image96" }
                .Select(name => key?.GetValue(name) as string)
                .FirstOrDefault(path => path is not null && File.Exists(path));
        }
        catch (Exception e) when (e is IdentityNotMappedException or SystemException)
        {
            return null;
        }
    }

    private static string? NullIfEmpty(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
