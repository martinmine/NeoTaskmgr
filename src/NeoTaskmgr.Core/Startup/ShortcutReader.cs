using System.Buffers.Binary;
using System.Text;

namespace NeoTaskmgr.Core.Startup;

/// <summary>
/// Reads the target of a shell link (.lnk) file by parsing the [MS-SHLLINK] format, so no COM is needed.
/// Handles shortcuts to local files; returns null for other targets (such as network or Store app shortcuts).
/// </summary>
internal static class ShortcutReader
{
    private const int HeaderSize = 0x4C;
    private const uint HasLinkTargetIdList = 0x01;
    private const uint HasLinkInfo = 0x02;
    private const uint HasName = 0x04;
    private const uint HasRelativePath = 0x08;
    private const uint HasWorkingDir = 0x10;
    private const uint HasArguments = 0x20;
    private const uint IsUnicode = 0x80;
    private const uint VolumeIdAndLocalBasePath = 0x01;

    public static (string? TargetPath, string? Arguments) Read(string path)
    {
        try
        {
            return Parse(File.ReadAllBytes(path), Path.GetDirectoryName(path)!);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return default;
        }
    }

    /// <param name="linkDirectory">Used to resolve a relative target path.</param>
    internal static (string? TargetPath, string? Arguments) Parse(byte[] data, string linkDirectory)
    {
        try
        {
            if (data.Length < HeaderSize || BinaryPrimitives.ReadInt32LittleEndian(data) != HeaderSize)
            {
                return default;
            }

            var flags = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(0x14));
            var offset = HeaderSize;
            if ((flags & HasLinkTargetIdList) != 0)
            {
                offset += 2 + BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(offset));
            }

            string? target = null;
            if ((flags & HasLinkInfo) != 0)
            {
                var linkInfo = data.AsSpan(offset);
                var linkInfoSize = BinaryPrimitives.ReadInt32LittleEndian(linkInfo);
                target = ReadLocalBasePath(linkInfo[..linkInfoSize]);
                offset += linkInfoSize;
            }

            // The optional strings follow, each a character count and the characters.
            var unicode = (flags & IsUnicode) != 0;
            string? ReadString()
            {
                var length = BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(offset));
                var byteLength = unicode ? length * 2 : length;
                var text = (unicode ? Encoding.Unicode : Encoding.Latin1).GetString(data, offset + 2, byteLength);
                offset += 2 + byteLength;
                return text;
            }

            string? relativePath = null, arguments = null;
            if ((flags & HasName) != 0)
            {
                ReadString(); // The description; not needed.
            }

            if ((flags & HasRelativePath) != 0)
            {
                relativePath = ReadString();
            }

            if ((flags & HasWorkingDir) != 0)
            {
                ReadString();
            }

            if ((flags & HasArguments) != 0)
            {
                arguments = ReadString();
            }

            target ??= relativePath is null ? null : Path.GetFullPath(Path.Combine(linkDirectory, relativePath));
            return (target, string.IsNullOrEmpty(arguments) ? null : arguments);
        }
        catch (ArgumentException)
        {
            return default; // Truncated or malformed file.
        }
    }

    private static string? ReadLocalBasePath(ReadOnlySpan<byte> linkInfo)
    {
        var headerSize = BinaryPrimitives.ReadInt32LittleEndian(linkInfo[4..]);
        var linkInfoFlags = BinaryPrimitives.ReadUInt32LittleEndian(linkInfo[8..]);
        if ((linkInfoFlags & VolumeIdAndLocalBasePath) == 0)
        {
            return null;
        }

        // Newer links also store the path as Unicode (when the header is at least 0x24 bytes).
        if (headerSize >= 0x24)
        {
            var unicodeBase = BinaryPrimitives.ReadInt32LittleEndian(linkInfo[0x1C..]);
            var unicodeSuffix = BinaryPrimitives.ReadInt32LittleEndian(linkInfo[0x20..]);
            if (unicodeBase > 0)
            {
                return ReadUnicodeString(linkInfo, unicodeBase) + (unicodeSuffix > 0 ? ReadUnicodeString(linkInfo, unicodeSuffix) : string.Empty);
            }
        }

        var basePath = ReadAnsiString(linkInfo, BinaryPrimitives.ReadInt32LittleEndian(linkInfo[0x10..]));
        var suffixOffset = BinaryPrimitives.ReadInt32LittleEndian(linkInfo[0x18..]);
        return basePath + (suffixOffset > 0 ? ReadAnsiString(linkInfo, suffixOffset) : string.Empty);
    }

    private static string ReadAnsiString(ReadOnlySpan<byte> data, int offset)
    {
        var text = data[offset..];
        return Encoding.Latin1.GetString(text[..text.IndexOf((byte)0)]); // The ANSI code page is not available by default; Latin-1 is close for paths.
    }

    private static string ReadUnicodeString(ReadOnlySpan<byte> data, int offset)
    {
        var text = data[offset..];
        var length = 0;
        while (length + 1 < text.Length && (text[length] != 0 || text[length + 1] != 0))
        {
            length += 2;
        }

        return Encoding.Unicode.GetString(text[..length]);
    }
}
