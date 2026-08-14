using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Pcre2.Tests;

public sealed class Pcre2AsciiByteSetFactoryTests
{
    [Fact]
    public void Pcre2BuildsSharedCarrierWithoutDotNetEncoding()
    {
        var range = Pcre2AsciiByteSetFactory.CreateRange((byte)'a', (byte)'z');
        var explicitSet = Pcre2AsciiByteSetFactory.Create("abc"u8);
        var whitespace = Pcre2AsciiByteSetFactory.CreatePredicate(Utf8AsciiBytePredicates.IsSixByteWhitespace);

        Assert.True(range.Contains((byte)'q'));
        Assert.False(range.Contains((byte)'Q'));
        Assert.True(explicitSet.Contains((byte)'b'));
        Assert.False(explicitSet.Contains((byte)'z'));
        Assert.True(whitespace.Contains((byte)'\v'));
    }

    [Fact]
    public void Pcre2FactoryDoesNotReferenceDotNetRegexCharClass()
    {
        var source = File.ReadAllText(FindRepositoryFile("src", "Lokad.Utf8Regex.Pcre2", "Pcre2AsciiByteSetFactory.cs"));
        Assert.DoesNotContain("RegexCharClass", source, StringComparison.Ordinal);
    }

    private static string FindRepositoryFile(params string[] segments)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine([directory.FullName, .. segments]);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException(string.Join(Path.DirectorySeparatorChar, segments));
    }
}
