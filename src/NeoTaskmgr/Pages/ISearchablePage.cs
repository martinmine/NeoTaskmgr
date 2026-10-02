namespace NeoTaskmgr.Pages;

/// <summary>A page that filters its content by the text in the title bar search box.</summary>
public interface ISearchablePage
{
    void ApplySearch(string text);
}
