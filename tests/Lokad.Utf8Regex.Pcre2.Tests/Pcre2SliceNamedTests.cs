using System.Text;
using Lokad.Utf8Regex.Pcre2;

namespace Lokad.Utf8Regex.Pcre2.Tests;

// R03 named captures for the PCRE2 profile: all-named shapes run on the
// shared slice runner (with literal-prefix IsMatch/Count/Enumerate where
// a specialized program exists); mixed named/unnamed shapes decline
// slice routing everywhere because native PCRE2 numbers captures
// positionally while BCL numbers unnamed groups first. Duplicate names
// fail construction natively; (?P<>) forms stay on native programs
// (the core slice declines Python syntax).
public sealed class Pcre2SliceNamedTests
{
    private static byte[] Utf8(string value) => Encoding.UTF8.GetBytes(value);

    private static System.Text.RegularExpressions.Regex Bcl(string pattern)
    {
        return new System.Text.RegularExpressions.Regex(
            pattern,
            System.Text.RegularExpressions.RegexOptions.CultureInvariant,
            System.Text.RegularExpressions.Regex.InfiniteMatchTimeout);
    }

    [Fact]
    public void NamedShapesRouteThroughSlice()
    {
        foreach (var pattern in new[] { "(?<word>a)+", "(?<x>a|b)+" })
        {
            var regex = new Utf8Pcre2Regex(pattern, options: Pcre2CompileOptions.None);
            Assert.Equal(
                "IsMatch=Pcre2Slice, Count=Pcre2Slice, Enumerate=Pcre2Slice, Match=Pcre2Slice, Replace=Pcre2Slice",
                regex.DebugDescribeExecutionPlan());
        }

        var prefixed = new Utf8Pcre2Regex("(?<a>x)(?<b>y)+", options: Pcre2CompileOptions.None);
    }

    [Theory]
    [InlineData("(?<b>a)(c)+")]
    [InlineData("(?<a>x)(b)(?<c>y)")]
    [InlineData("(?P<x>a)")]
    public void NonSliceNamedShapesDeclineSliceRouting(string pattern)
    {
        var regex = new Utf8Pcre2Regex(pattern, options: Pcre2CompileOptions.None);
        Assert.DoesNotContain("Pcre2Slice", regex.DebugDescribeExecutionPlan());
    }

    [Fact]
    public void DuplicateNamesFailConstruction()
    {
        Assert.Throws<Pcre2CompileException>(() => new Utf8Pcre2Regex("(?<x>a)(?<x>b)", options: Pcre2CompileOptions.None));
    }

    [Fact]
    public void EnabledDuplicateNamesKeepDistinctProfileCaptureSlots()
    {
        var regex = new Utf8Pcre2Regex("(?<x>a)(?<x>b)", options: Pcre2CompileOptions.None, compileSettings: new Utf8Pcre2CompileSettings { AllowDuplicateNames = true }, policy: new Utf8RegexPolicy
        {
            MatchTimeout = System.Text.RegularExpressions.Regex.InfiniteMatchTimeout
        });
        Assert.DoesNotContain("Pcre2Slice", regex.DebugDescribeExecutionPlan());
        ReadOnlySpan<byte> context1Input = "ab"u8;
        var context1Captures = new Utf8Pcre2CaptureRange[regex.CaptureGroupCount + 1];
        var context = regex.Match(context1Input, captures: context1Captures);
        Assert.True(context.Success);
        Assert.Equal("a", System.Text.Encoding.UTF8.GetString((context1Captures[1].Success ? context1Input.Slice(context1Captures[1].StartOffsetInBytes, context1Captures[1].EndOffsetInBytes - context1Captures[1].StartOffsetInBytes) : ReadOnlySpan<byte>.Empty)));
        Assert.Equal("b", System.Text.Encoding.UTF8.GetString((context1Captures[2].Success ? context1Input.Slice(context1Captures[2].StartOffsetInBytes, context1Captures[2].EndOffsetInBytes - context1Captures[2].StartOffsetInBytes) : ReadOnlySpan<byte>.Empty)));
        Span<int> numbers = stackalloc int[2];
        Assert.Equal(2, regex.CopyNumbersForUtf8Name("x"u8, numbers, out var isMore));
        Assert.False(isMore);
        Assert.Equal(new[] { 1, 2 }, numbers.ToArray());
    }

    [Fact]
    public void MixedNumberingFollowsNativePositionalRule()
    {
        // Native PCRE2 numbers left-to-right: group 1 is (b) here, while
        // BCL would number the unnamed (c) first. The slice decline above
        // keeps this shape on native numbering.
        var regex = new Utf8Pcre2Regex("(?<b>a)(c)+", options: Pcre2CompileOptions.None);
        ReadOnlyMemory<byte> detailed2Input = Utf8("acc");
        using var detailed2Cursor = regex.CreateMatchCursor(detailed2Input);
        detailed2Cursor.MoveNext(out var detailed2Result);
        detailed2Cursor.Dispose();
        var detailed = detailed2Result;
        Assert.True(detailed.Success);
        Assert.Equal("a", System.Text.Encoding.UTF8.GetString((detailed.Groups[1].Success ? detailed2Input.Span.Slice(detailed.Groups[1].StartOffsetInBytes, detailed.Groups[1].EndOffsetInBytes - detailed.Groups[1].StartOffsetInBytes) : ReadOnlySpan<byte>.Empty)));
        Assert.Equal("c", System.Text.Encoding.UTF8.GetString((detailed.Groups[2].Success ? detailed2Input.Span.Slice(detailed.Groups[2].StartOffsetInBytes, detailed.Groups[2].EndOffsetInBytes - detailed.Groups[2].StartOffsetInBytes) : ReadOnlySpan<byte>.Empty)));
        Assert.True(detailed.TryGetGroup(1, out var bee)); Assert.True(bee.Success);
        Assert.Equal("a", System.Text.Encoding.UTF8.GetString((bee.Success ? detailed2Input.Span.Slice(bee.StartOffsetInBytes, bee.EndOffsetInBytes - bee.StartOffsetInBytes) : ReadOnlySpan<byte>.Empty)));
    }

    public static IEnumerable<object?[]> IsMatchCases()
    {
        yield return new object?[] { "(?<word>a)+", "xaa", true };
        yield return new object?[] { "(?<word>a)+", "xb", false };
        yield return new object?[] { "(?<x>a|b)+", "abba", true };
        yield return new object?[] { "(?<a>x)(?<b>y)+", "xyy", true };
    }

    [Theory]
    [MemberData(nameof(IsMatchCases))]
    public void OwnedIsMatchMatchesNativeSemantics(
        string pattern, string inputText, bool expected)
    {
        Assert.Equal(expected, Bcl(pattern).IsMatch(inputText));
        using var regex = new Utf8Pcre2Regex(pattern);
        Assert.Equal(expected, regex.IsMatch(Utf8(inputText)));
    }

    [Theory]
    [InlineData("(?<word>a)+", "aa aa", 0, 2)]
    [InlineData("(?<x>a|b)+", "abba ab", 0, 2)]
    public void OwnedCountMatchesNativeSemantics(
        string pattern, string inputText, int startOffsetInBytes, int expected)
    {
        var regex = new Utf8Pcre2Regex(pattern, options: Pcre2CompileOptions.None);
        Assert.Equal(expected, regex.Count(Utf8(inputText), startOffsetInBytes));
    }

    [Theory]
    [InlineData("(?<word>a)+", "xaa", 0, true, 1, 3)]
    [InlineData("(?<a>x)(?<b>y)+", "xxyy", 0, true, 1, 4)]
    public void OwnedFirstMatchProjectsBytes(
        string pattern, string inputText, int startOffsetInBytes, bool expectedSuccess, int expectedStart, int expectedEnd)
    {
        var expected = Bcl(pattern).Match(inputText);
        Assert.Equal(expectedSuccess, expected.Success);
        var regex = new Utf8Pcre2Regex(pattern, options: Pcre2CompileOptions.None);
        var found = regex.Match(Utf8(inputText), startOffsetInBytes: startOffsetInBytes);
        Assert.Equal(expectedSuccess, found.Success);
        if (expectedSuccess)
        {
            Assert.Equal(expected.Index, found.StartOffsetInBytes);
            Assert.Equal(expected.Index + expected.Length, found.EndOffsetInBytes);
            Assert.Equal(expectedStart, found.StartOffsetInBytes);
            Assert.Equal(expectedEnd, found.EndOffsetInBytes);
        }
    }

    [Fact]
    public void OwnedEnumerationAdvancesThroughNamedGroups()
    {
        var regex = new Utf8Pcre2Regex("(?<word>a)+", options: Pcre2CompileOptions.None);
        var enumerator = regex.EnumerateMatches(Utf8("aa aa"), 0);
        var ranges = new List<string>();
        while (enumerator.MoveNext())
        {
            ranges.Add(
                enumerator.Current.StartOffsetInBytes
                + "-" + enumerator.Current.EndOffsetInBytes);
        }

        Assert.Equal(["0-2", "3-5"], ranges);
    }

    [Fact]
    public void OwnedDetailedProjectsNamedGroups()
    {
        var regex = new Utf8Pcre2Regex("(?<word>a)+", options: Pcre2CompileOptions.None);
        ReadOnlyMemory<byte> detailed3Input = Utf8("xaa");
        using var detailed3Cursor = regex.CreateMatchCursor(detailed3Input);
        detailed3Cursor.MoveNext(out var detailed3Result);
        detailed3Cursor.Dispose();
        var detailed = detailed3Result;
        Assert.True(detailed.Success);
        Assert.Equal("a", System.Text.Encoding.UTF8.GetString((detailed.Groups[1].Success ? detailed3Input.Span.Slice(detailed.Groups[1].StartOffsetInBytes, detailed.Groups[1].EndOffsetInBytes - detailed.Groups[1].StartOffsetInBytes) : ReadOnlySpan<byte>.Empty)));
        Assert.True(detailed.TryGetGroup(1, out var word)); Assert.True(word.Success);
        Assert.Equal("a", System.Text.Encoding.UTF8.GetString((word.Success ? detailed3Input.Span.Slice(word.StartOffsetInBytes, word.EndOffsetInBytes - word.StartOffsetInBytes) : ReadOnlySpan<byte>.Empty)));
    }

    [Fact]
    public void OwnedReplaceExpandsNamedGroup()
    {
        var regex = new Utf8Pcre2Regex("(?<word>a)+", options: Pcre2CompileOptions.None);
        Assert.Equal("x<a>", System.Text.Encoding.UTF8.GetString(regex.Replace(Utf8("xaa"), "<$1>"u8)));
    }
}
