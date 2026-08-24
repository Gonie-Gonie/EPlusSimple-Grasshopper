using System.Security;

namespace GonieGonie.EnergyPlus.Runtime.Tests;

public sealed class RuntimeSafetyTests
{
    [Fact]
    public void RunDirectoriesAreUniqueMarkedChildrenOfCallerRoot()
    {
        using var directory = new TestDirectory();
        var root = System.IO.Path.Combine(directory.Path, "runs");
        var first = SafeRunDirectory.Create(root);
        var second = SafeRunDirectory.Create(root);

        Assert.NotEqual(first.Path, second.Path);
        Assert.True(RuntimeFileSystem.IsDescendantOf(root, first.Path));
        Assert.True(RuntimeFileSystem.IsDescendantOf(root, second.Path));

        first.Delete();
        second.Delete();
        Assert.False(Directory.Exists(first.Path));
        Assert.False(Directory.Exists(second.Path));
        Assert.True(Directory.Exists(root));
    }

    [Fact]
    public void CombiningTraversalOutsideRootIsRejected()
    {
        using var directory = new TestDirectory();
        var root = System.IO.Path.Combine(directory.Path, "runs");
        Directory.CreateDirectory(root);

        Assert.Throws<SecurityException>(() =>
            RuntimeFileSystem.CombineUnder(root, "..", "escaped.txt"));
        Assert.False(File.Exists(System.IO.Path.Combine(directory.Path, "escaped.txt")));
    }

    [Theory]
    [InlineData("")]
    [InlineData("plain")]
    [InlineData("with space")]
    [InlineData("trailing\\")]
    [InlineData("embedded\"quote")]
    public void NetFrameworkCommandLineQuotingRoundTripsImportantShapes(string argument)
    {
        var quoted = WindowsCommandLine.Quote(argument);

        if (argument.Length != 0
            && argument.All(character => !char.IsWhiteSpace(character) && character != '"'))
        {
            Assert.Equal(argument, quoted);
        }
        else
        {
            Assert.StartsWith("\"", quoted, StringComparison.Ordinal);
            Assert.EndsWith("\"", quoted, StringComparison.Ordinal);
        }
    }
}
