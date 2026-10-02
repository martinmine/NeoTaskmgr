using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Principal;
using Microsoft.Win32.SafeHandles;
using NeoTaskmgr.Core.Interop;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Security;
using Windows.Win32.System.SystemInformation;
using Windows.Win32.System.Threading;

namespace NeoTaskmgr.Core.Processes;

/// <summary>Reads the static <see cref="ProcessDetails"/> of a process. Caches lookups shared between processes.</summary>
internal sealed unsafe class ProcessDetailsReader
{
    private readonly Dictionary<string, (string? Description, string? Publisher)> _versionInfoByPath = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<SecurityIdentifier, string> _userNamesBySid = [];

    public ProcessDetails Read(ProcessCounters process)
    {
        // The idle and kernel processes cannot be opened, but are known to run as SYSTEM.
        if (process.Pid is 0 or 4)
        {
            return new ProcessDetails { UserName = "SYSTEM" };
        }

        using var handle = PInvoke.OpenProcess_SafeHandle(PROCESS_ACCESS_RIGHTS.PROCESS_QUERY_LIMITED_INFORMATION, false, (uint)process.Pid);
        if (handle.IsInvalid)
        {
            return ProcessDetails.Empty;
        }

        var imagePath = GetImagePath(handle);
        var (userName, isElevated) = GetTokenDetails(handle);
        var (description, publisher) = imagePath is null ? default : GetVersionInfo(imagePath);
        return new ProcessDetails
        {
            ImagePath = imagePath,
            UserName = userName,
            IsElevated = isElevated,
            Description = description,
            Publisher = publisher,
            Architecture = GetArchitecture(handle),
            CommandLine = GetCommandLine(handle),
        };
    }

    private static string? GetImagePath(SafeFileHandle handle)
    {
        Span<char> buffer = stackalloc char[1024];
        var length = (uint)buffer.Length;
        return PInvoke.QueryFullProcessImageName(handle, PROCESS_NAME_FORMAT.PROCESS_NAME_WIN32, buffer, ref length)
            ? new string(buffer[..(int)length])
            : null;
    }

    private (string? Description, string? Publisher) GetVersionInfo(string imagePath)
    {
        if (!_versionInfoByPath.TryGetValue(imagePath, out var info))
        {
            try
            {
                var versionInfo = FileVersionInfo.GetVersionInfo(imagePath);
                info = (NullIfEmpty(versionInfo.FileDescription), NullIfEmpty(versionInfo.CompanyName));
            }
            catch (FileNotFoundException)
            {
                info = default;
            }

            _versionInfoByPath[imagePath] = info;
        }

        return info;
    }

    private static string? NullIfEmpty(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private (string? UserName, bool? IsElevated) GetTokenDetails(SafeFileHandle process)
    {
        if (!PInvoke.OpenProcessToken(process, TOKEN_ACCESS_MASK.TOKEN_QUERY, out var token))
        {
            return (null, null);
        }

        using (token)
        {
            string? userName = null;
            Span<byte> buffer = stackalloc byte[256];
            if (PInvoke.GetTokenInformation(token, TOKEN_INFORMATION_CLASS.TokenUser, buffer, out _))
            {
                fixed (byte* tokenUser = buffer)
                {
                    userName = GetUserName(new SecurityIdentifier((nint)((TOKEN_USER*)tokenUser)->User.Sid.Value));
                }
            }

            bool? isElevated = null;
            var elevation = default(TOKEN_ELEVATION);
            if (PInvoke.GetTokenInformation(token, TOKEN_INFORMATION_CLASS.TokenElevation, MemoryMarshal.AsBytes(new Span<TOKEN_ELEVATION>(ref elevation)), out _))
            {
                isElevated = elevation.TokenIsElevated != 0;
            }

            return (userName, isElevated);
        }
    }

    /// <summary>The account name without domain, like Task Manager shows it.</summary>
    private string GetUserName(SecurityIdentifier sid)
    {
        if (!_userNamesBySid.TryGetValue(sid, out var name))
        {
            try
            {
                var account = sid.Translate(typeof(NTAccount)).Value;
                name = account[(account.IndexOf('\\') + 1)..];
            }
            catch (IdentityNotMappedException)
            {
                name = sid.Value;
            }

            _userNamesBySid[sid] = name;
        }

        return name;
    }

    private static string? GetArchitecture(SafeFileHandle handle)
    {
        var info = default(PROCESS_MACHINE_INFORMATION);
        if (!PInvoke.GetProcessInformation(handle, PROCESS_INFORMATION_CLASS.ProcessMachineTypeInfo, MemoryMarshal.AsBytes(new Span<PROCESS_MACHINE_INFORMATION>(ref info))))
        {
            return null;
        }

        return info.ProcessMachine switch
        {
            IMAGE_FILE_MACHINE.IMAGE_FILE_MACHINE_AMD64 => "x64",
            IMAGE_FILE_MACHINE.IMAGE_FILE_MACHINE_I386 => "x86",
            IMAGE_FILE_MACHINE.IMAGE_FILE_MACHINE_ARM64 => "ARM64",
            IMAGE_FILE_MACHINE.IMAGE_FILE_MACHINE_ARMNT => "ARM",
            _ => null,
        };
    }

    private static string? GetCommandLine(SafeFileHandle handle)
    {
        // The result is a UNICODE_STRING header followed by the characters.
        var size = 1024u;
        while (true)
        {
            var buffer = NativeMemory.Alloc(size);
            try
            {
                var status = Ntdll.NtQueryInformationProcess(handle, Ntdll.ProcessCommandLineInformationClass, buffer, size, out var needed);
                if (status == Ntdll.StatusInfoLengthMismatch && needed > size)
                {
                    size = needed;
                    continue;
                }

                return status < 0 ? null : ((UNICODE_STRING*)buffer)->ToStringOrNull();
            }
            finally
            {
                NativeMemory.Free(buffer);
            }
        }
    }
}
