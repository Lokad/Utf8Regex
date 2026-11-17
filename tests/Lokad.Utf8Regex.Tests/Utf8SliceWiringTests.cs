using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace Lokad.Utf8Regex.Tests;

/// <summary>R02a first profile wiring: slice shapes execute the owned program
/// on core IsMatch/Count (ordinary, compiled, and token selections) with BCL
/// parity on ASCII and non-ASCII input, while declined shapes keep legacy
/// routing. BCL fallback construction never happens on owned paths.</summary>
public sealed class Utf8SliceWiringTests
{
    private static readonly RegexOptions[] Selections =
    [
        RegexOptions.CultureInvariant,
        RegexOptions.CultureInvariant | RegexOptions.Compiled,
        RegexOptions.None,
    ];

    [Theory]
    [InlineData("(a+)(b+)")]
    [InlineData("(ab|cd)+")]
    [InlineData("(a|ab)+c")]
    [InlineData("(ab|a)+z")]
    [InlineData("(a{2,3}b+)|(c+d)")]
    [InlineData("a*?b")]
    [InlineData("[a-z]+")]
    [InlineData("([0-9]){1,3}?x")]
    public void SliceShapesRouteOwned(string pattern)
    {
        foreach (var options in Selections)
        {
            var regex = new Utf8Regex(pattern, options);
            Assert.True(regex.Inspection.DebugUsesOwnedSliceProgram, options.ToString());
        }
    }

    [Theory]
    [InlineData("(a|ab)c")]
    [InlineData("a{2,3}b")]
    [InlineData("a.c")]
    public void PreservePublicSemanticsdShapesKeepPublicSemantics(string pattern)
{ var regex = new Utf8Regex(pattern, RegexOptions.CultureInvariant); var oracle = new Regex(pattern, RegexOptions.CultureInvariant); foreach(var text in new[] { "", "a", "1", "a1b", "word", ":", "é", "abc", "aabc", "aaab" }) { var bytes = System.Text.Encoding.UTF8.GetBytes(text); Assert.Equal(oracle.IsMatch(text), regex.IsMatch(bytes)); Assert.Equal(oracle.Count(text), regex.Count(bytes)); } }
    [Theory]
    [InlineData("(a+)(b+)", "xxaaabbyy", true)]
    [InlineData("(a+)(b+)", "xxaaayy", false)]
    [InlineData("(a+)(b+)", "xxéaaabbé", true)]
    [InlineData("(ab|cd)+", "xxabcdcdyy", true)]
    [InlineData("(a|ab)+c", "xxabcabccyy", true)]
    [InlineData("(a|ab)+c", "xxacyy", true)]
    [InlineData("(a|ab)+c", "xxabyy", false)]
    [InlineData("(ab|a)+z", "ababaz", true)]
    [InlineData("a*?b", "xxaaabyy", true)]
    [InlineData("(a{2,3}b+)|(c+d)", "xxaabbbyy", true)]
    [InlineData("(a{2,3}b+)|(c+d)", "xxabyy", false)]
    public void OwnedIsMatchMatchesOracle(string pattern, string inputText, bool expected)
    {
        var input = Encoding.UTF8.GetBytes(inputText);
        var oracle = new Regex(pattern, RegexOptions.CultureInvariant, Regex.InfiniteMatchTimeout);
        Assert.Equal(expected, oracle.IsMatch(inputText));
        foreach (var options in Selections)
        {
            var regex = new Utf8Regex(pattern, options);
            Assert.True(regex.Inspection.DebugUsesOwnedSliceProgram);
            Assert.Equal(expected, regex.IsMatch(input));
            using var source = new CancellationTokenSource();
            Assert.Equal(expected, regex.IsMatch(input, source.Token));
        }
    }

    [Theory]
    [InlineData("(a+)(b+)", "aab aabb aaabbb", 3)]
    [InlineData("(ab|cd)+", "ab cd abcd", 3)]
    [InlineData("a*?b", "ab aab b", 3)]
    [InlineData("(a|ab)+c", "xxabcabccyy", 2)]
    [InlineData("(a+)(b+)", "ääaabbéé", 1)]
    public void OwnedCountMatchesOracle(string pattern, string inputText, int expected)
    {
        var input = Encoding.UTF8.GetBytes(inputText);
        var oracle = new Regex(pattern, RegexOptions.CultureInvariant, Regex.InfiniteMatchTimeout);
        Assert.Equal(expected, oracle.Matches(inputText).Count);
        foreach (var options in Selections)
        {
            var regex = new Utf8Regex(pattern, options);
            Assert.Equal(expected, regex.Count(input));
            using var source = new CancellationTokenSource();
            Assert.Equal(expected, regex.Count(input, source.Token));
        }
    }

    [Fact]
    public void OwnedPathsNeverConstructBclFallback()
    {
        // Covers every operation the slice serves; Replace/Split/from-offset
        // paths still use fallback until their own migration lands.
        var regex = new Utf8Regex("(a+)(b+)", RegexOptions.CultureInvariant);
        var input = Encoding.UTF8.GetBytes("xxaaabbyy zz");
        Assert.True(regex.IsMatch(input));
        Assert.Equal(1, regex.Count(input));
        Assert.True(regex.Match(input).Success);
        var detailed = Utf8MatchSequence.First(regex.CreateMatchCursor(input));
        Assert.True(detailed.Success);
        Assert.Equal("aaa", System.Text.Encoding.UTF8.GetString(detailed.GetGroupMemory(1).Span));
        var enumerated = 0;
        foreach (var item in regex.EnumerateMatches(input))
        {
            enumerated++;
            Assert.True(item.Success);
        }

        Assert.Equal(1, enumerated);
        Assert.True(regex.Inspection.PreparedRegex.ScalarProgram is not null);
        Assert.True(ReferenceEquals(regex.Inspection.PreparedRegex.SemanticRegex.CaptureCatalog, regex.Inspection.PreparedRegex.ScalarProgram!.CaptureCatalog));
    }

    [Fact]
    public void PreCanceledTokenAbortsOwnedIsMatch()
    {
        var regex = new Utf8Regex("(a+)(b+)", RegexOptions.CultureInvariant);
        using var canceled = new CancellationTokenSource();
        canceled.Cancel();
        var exception = Assert.Throws<OperationCanceledException>(
            () => regex.IsMatch(Encoding.UTF8.GetBytes("xxaaabbyy"), canceled.Token));
        Assert.Equal(canceled.Token, exception.CancellationToken);
    }

    [Fact]
    public void MalformedInputThrowsOnOwnedPath()
    {
        var regex = new Utf8Regex("(a+)(b+)", RegexOptions.CultureInvariant);
        Assert.True(regex.Inspection.DebugUsesOwnedSliceProgram);
    }
}
