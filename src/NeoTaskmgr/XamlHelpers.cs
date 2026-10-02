using Microsoft.UI.Xaml;

namespace NeoTaskmgr;

/// <summary>Small conversion functions for x:Bind.</summary>
public static class XamlHelpers
{
    /// <summary>Indents child rows of a tree-like table.</summary>
    public static Thickness Indent(bool isChild) => new(isChild ? 24 : 0, 0, 0, 0);
}
