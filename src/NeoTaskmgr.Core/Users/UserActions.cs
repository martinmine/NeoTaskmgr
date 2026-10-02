using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;

namespace NeoTaskmgr.Core.Users;

/// <summary>Actions on users' sessions. Other users' sessions require administrator rights; failures throw <see cref="Win32Exception"/>.</summary>
public static class UserActions
{
    /// <summary>Disconnects the session; the user's apps keep running and the user can sign in to it again.</summary>
    public static void Disconnect(int sessionId)
    {
        if (!PInvoke.WTSDisconnectSession(HANDLE.Null, (uint)sessionId, false))
        {
            throw new Win32Exception(Marshal.GetLastPInvokeError());
        }
    }

    /// <summary>Signs the user out, closing their apps.</summary>
    public static void SignOut(int sessionId)
    {
        if (!PInvoke.WTSLogoffSession(HANDLE.Null, (uint)sessionId, false))
        {
            throw new Win32Exception(Marshal.GetLastPInvokeError());
        }
    }

    /// <summary>Opens the accounts page of the Settings app.</summary>
    public static void ManageUserAccounts() =>
        Process.Start(new ProcessStartInfo("ms-settings:otherusers") { UseShellExecute = true })?.Dispose();
}
