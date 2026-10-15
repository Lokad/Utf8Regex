using System.Text;
using Lokad.Utf8Regex.PythonRe;

namespace Lokad.Utf8Regex.PythonRe.Tests;

public sealed class PythonReIdentifierVersionTests
{
    // CPython 3.13.2 accepts XID identifiers, rather than a union of general
    // categories. NFKC exclusions and Other_ID_Start/Continue matter as well as
    // the Unicode version; the group spelling itself is preserved.
    [Theory]
    [InlineData("\u037A", false)]
    [InlineData("\u1C89", false)]
    [InlineData("\U0001E5D0", false)]
    [InlineData("a\U0001E5F1", false)]
    [InlineData("a\u00B7", true)]
    [InlineData("\u2118", true)]
    [InlineData("a\u200C", true)]
    [InlineData("\U00031350", true)]
    [InlineData("a\u0301", true)]
    [InlineData("_a5", true)]
    public void DeclarationReferenceAndReplacementUsePinnedIdentifiers(string name, bool valid)
    {
        var pattern = "(?P<" + name + ">a)(?P=" + name + ")";
        if (!valid)
        {
            Assert.Throws<PythonRePatternException>(() => new Utf8PythonRegex(pattern));
            Assert.Throws<PythonRePatternException>(() => new Utf8PythonRegex(Encoding.UTF8.GetBytes(pattern)));
            return;
        }

        var policy = new Utf8ResourcePolicy { MaxWorkSteps = 100000, MaxDepth = 128,
            MaxWorkingBytes = 1_048_576, MaxResultBytes = 1_048_576 };
        var regex = new Utf8PythonRegex(pattern, PythonReCompileOptions.None, Timeout.InfiniteTimeSpan, policy);
        var match = regex.SearchDetailedData("😀aa"u8);
        Assert.True(match.Success);
        Assert.Equal(name, match.LastGroup);
        Assert.Equal("a", match.Groups[1].ValueText);
        Assert.Equal("😀a", regex.SubnToString("😀aa"u8, "\\g<" + name + ">").ResultText);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        Assert.Throws<OperationCanceledException>(() => regex.IsMatch("aa"u8, 0, cancellation.Token));
    }

    [Fact]
    public void IdentifierValidationDoesNotNormalizeStoredNames()
    {
        var regex = new Utf8PythonRegex("(?P<é>a)(?P<e\u0301>b)");
        var match = regex.SearchDetailedData("ab"u8);
        Assert.Equal("e\u0301", match.LastGroup);
        Assert.Equal("a|b", regex.SubnToString("ab"u8, "\\g<é>|\\g<e\u0301>").ResultText);
    }

    [Theory]
    [InlineData("\u037A")]
    [InlineData("\u1C89")]
    [InlineData("a\U0001E5F1")]
    public void InvalidReferencesAndTemplatesUseTheSameIdentifierPolicy(string name)
    {
        Assert.Throws<PythonRePatternException>(() => new Utf8PythonRegex("(?P=" + name + ")"));
        Assert.Throws<PythonRePatternException>(() => new Utf8PythonRegex("(?(" + name + ")a|b)"));
        Assert.Throws<PythonRePatternException>(() =>
            new Utf8PythonRegex("a").SubnToString("a"u8, "\\g<" + name + ">"));
    }
}
