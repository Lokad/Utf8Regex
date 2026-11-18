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

        var policy = new Utf8ResourcePolicy
        {
            MaxWorkSteps = 100000,
            MaxDepth = 128,
            MaxWorkingBytes = 1_048_576,
            MaxResultBytes = 1_048_576
        };
        var regex = new Utf8PythonRegex(pattern, options: PythonReCompileOptions.None, policy: new Utf8RegexPolicy()
        {
            MatchTimeout = Timeout.InfiniteTimeSpan,
            ResourcePolicy = policy
        });
        Utf8PythonRangeMatch match = default;
        using (var captureCursor1 = regex.CreateMatchCursor(("😀aa"u8).ToArray(), mode: Utf8PythonMatchMode.Search))
        {
            captureCursor1.MoveNext(out match);
        }
        Assert.True(match.Success);
        Assert.Equal(1, match.LastIndex);
        Assert.Equal("a", System.Text.Encoding.UTF8.GetString((match.Groups[1].Success ? match.GetGroupMemory(match.Groups[1].Number).Span : ReadOnlySpan<byte>.Empty)));
        Assert.Equal("😀a", PythonReSubstitutionAssert.Decode(regex.Subn("😀aa"u8, Encoding.UTF8.GetBytes("\\g<" + name + ">"))).ResultText);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        Assert.Throws<OperationCanceledException>(() => regex.IsMatch("aa"u8, 0, cancellation.Token));
    }

    [Fact]
    public void IdentifierValidationDoesNotNormalizeStoredNames()
    {
        var regex = new Utf8PythonRegex("(?P<é>a)(?P<e\u0301>b)");
        Utf8PythonRangeMatch match = default;
        using (var captureCursor2 = regex.CreateMatchCursor(("ab"u8).ToArray(), mode: Utf8PythonMatchMode.Search))
        {
            captureCursor2.MoveNext(out match);
        }
        Assert.Equal(2, match.LastIndex);
        Assert.Equal("a|b", PythonReSubstitutionAssert.Decode(regex.Subn("ab"u8, "\\g<é>|\\g<e\u0301>"u8)).ResultText);
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
            PythonReSubstitutionAssert.Decode(new Utf8PythonRegex("a").Subn("a"u8, Encoding.UTF8.GetBytes("\\g<" + name + ">"))));
    }
}
