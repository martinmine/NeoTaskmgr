using Microsoft.Windows.ApplicationModel.Resources;

namespace NeoTaskmgr;

/// <summary>Localized strings from Strings/*/Resources.resw for use in code.</summary>
internal static class Strings
{
    private static readonly ResourceLoader Loader = new();

    public static string Get(string key) => Loader.GetString(key);

    public static string Format(string key, params object?[] args) => string.Format(Get(key), args);
}
