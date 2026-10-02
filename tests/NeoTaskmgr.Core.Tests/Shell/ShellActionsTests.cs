using NeoTaskmgr.Core.Shell;

namespace NeoTaskmgr.Core.Tests.Shell;

public sealed class ShellActionsTests
{
    [Theory]
    [InlineData("notepad", "notepad", "")]
    [InlineData("notepad file.txt", "notepad", "file.txt")]
    [InlineData("\"C:\\Program Files\\App\\app.exe\" --flag  x", "C:\\Program Files\\App\\app.exe", "--flag  x")]
    [InlineData("\"C:\\Program Files\\App\\app.exe\"", "C:\\Program Files\\App\\app.exe", "")]
    [InlineData("\"unterminated", "unterminated", "")]
    [InlineData("https://example.com", "https://example.com", "")]
    public void SplitCommandLine(string commandLine, string expectedFile, string expectedArguments) =>
        Assert.Equal((expectedFile, expectedArguments), ShellActions.SplitCommandLine(commandLine));

    [Fact]
    public void SplitCommandLine_UnquotedExistingPathWithSpaces_IsOneFile()
    {
        var directory = Path.Combine(Path.GetTempPath(), "NeoTaskmgr Tests " + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            Assert.Equal((directory, ""), ShellActions.SplitCommandLine(directory));
        }
        finally
        {
            Directory.Delete(directory);
        }
    }
}
