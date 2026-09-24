using System.Text;
using Lokad.Utf8Regex.Pcre2;

namespace Lokad.Utf8Regex.Pcre2.Tests;

// R03 case-insensitive slice for the PCRE2 profile: native caseless folds
// UCP-wide while the shared runner applies BCL-invariant per-scalar folding,
// so every IgnoreCase program declines slice routing. ASCII caseless shapes
// keep their native programs with BCL-agreeing behavior.
public sealed class Pcre2SliceIgnoreCaseTests
{
    private static byte[] Utf8(string value) => Encoding.UTF8.GetBytes(value);

    [Theory]
    [InlineData("a+")]
    [InlineData("[a-z]+")]
    [InlineData("(a)\\1")]
    [InlineData("(a)(b)")]
    public void IgnoreCaseShapesDeclineSliceRouting(string pattern)
    {
        var regex = new Utf8Pcre2Regex(pattern, Pcre2CompileOptions.Caseless);
        Assert.DoesNotContain("Pcre2Slice", regex.DebugDescribeExecutionPlan());
    }

    [Fact]
    public void NativeCaselessFoldsUcpWide()
    {
        // Pinned divergence that forces the decline above: native caseless
        // matches sigma against final sigma while BCL-invariant folding
        // (and the owned slice) reports a miss.
        var regex = new Utf8Pcre2Regex("(?P<x>Σ)+", Pcre2CompileOptions.Caseless);
        Assert.DoesNotContain("Pcre2Slice", regex.DebugDescribeExecutionPlan());
        Assert.True(regex.IsMatch(Utf8("ς")));
        var bcl = new System.Text.RegularExpressions.Regex(
            "(Σ)+",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.CultureInvariant,
            System.Text.RegularExpressions.Regex.InfiniteMatchTimeout);
        Assert.False(bcl.IsMatch("ς"));
    }

    public static IEnumerable<object?[]> IsMatchCases()
    {
        yield return new object?[] { "a+", "xAAx", true };
        yield return new object?[] { "abc", "xABCx", true };
        yield return new object?[] { "[a-z]+", "ABC", true };
        yield return new object?[] { "(a)\\1", "aA", true };
        yield return new object?[] { "a+", "bbb", false };
    }

    [Theory]
    [MemberData(nameof(IsMatchCases))]
    public void CaselessIsMatchMatchesOracle(string pattern, string inputText, bool expected)
    {
        var oracle = new System.Text.RegularExpressions.Regex(
            pattern,
            System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.CultureInvariant,
            System.Text.RegularExpressions.Regex.InfiniteMatchTimeout);
        Assert.Equal(expected, oracle.IsMatch(inputText));
        var regex = new Utf8Pcre2Regex(pattern, Pcre2CompileOptions.Caseless);
        Assert.Equal(expected, regex.IsMatch(Utf8(inputText)));
        Assert.False(regex.DebugFoundationCoreFallbackRegexIsCreated, "pattern=" + pattern);
    }
}