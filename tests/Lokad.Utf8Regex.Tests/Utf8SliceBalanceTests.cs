using System.Text;
using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex.Tests;

// R03 balancing captures: (?<n1-n2>sub) pops n2 (fail if empty) and
// captures [popped end, pop-time position) into n1; (?<-n2>sub) pops
// without capturing. Stacks live in the balance chunk with per-group
// heads journaled through the trail, so backtracking undoes pops exactly
// like capture writes, and detailed histories drain popped captures.
// n2 must be backward-defined; forward/undefined targets, duplicates,
// digit starts, mismatched closers, and self-pops decline to
// legacy-exact routing.
public sealed class Utf8SliceBalanceTests
{
    public static IEnumerable<object?[]> SliceBalanceShapes()
    {
        yield return new object?[] { "(?<open>a)+(?<-open>b)+" };
        yield return new object?[] { "(?<open>a)(?<-open>b)" };
        yield return new object?[] { "(?<a>x)(?<b-a>y)" };
        yield return new object?[] { "(?<a>x)(?<b-a>y)(?<c-b>z)" };
        yield return new object?[] { "((?<o>a)+(?<-o>b)+)" };
        yield return new object?[] { "(?<o>a)(?<-o>b)?b" };
        yield return new object?[] { "(?<a>x)(?'b-a'y)" };
        yield return new object?[] { "(?<o>\\p{L}+)(?<-o>\\d+)" };
        yield return new object?[] { "(?<o>\\p{L}+)(?<-o>\\p{Nd}+)" };
    }

    [Theory]
    [MemberData(nameof(SliceBalanceShapes))]
    public void BalanceShapesRouteOwned(string pattern)
    {
        foreach (var options in new[]
        {
            RegexOptions.None,
            RegexOptions.CultureInvariant,
            RegexOptions.Compiled | RegexOptions.CultureInvariant,
        })
        {
            var regex = new Utf8Regex(pattern, options);
            Assert.True(regex.Inspection.DebugUsesOwnedSliceProgram, options.ToString());
        }
    }

    [Theory]
    [InlineData("(?<-b>a)(?<b>c)")]
    [InlineData("(?<a-a>x)")]
    [InlineData("(?<a-1>x)")]
    public void NonBalanceShapesDecline(string pattern)
    {
        var regex = new Utf8Regex(pattern, RegexOptions.CultureInvariant);
        Assert.False(regex.Inspection.DebugUsesOwnedSliceProgram, "pattern=" + pattern);
    }

    [Theory]
    [InlineData("(?<-open>b)")]
    [InlineData("(?<a-b>x)")]
    [InlineData("(?<b-a>x)(?<b-c>y)")]
    [InlineData("(?<1-a>x)")]
    [InlineData("(?'b-a'y)")]
    public void UndefinedBalanceShapesFailConstruction(string pattern)
    {
        Assert.Throws<RegexParseException>(
            () => new Utf8Regex(pattern, RegexOptions.CultureInvariant));
    }

    public static IEnumerable<object?[]> IsMatchCases()
    {
        yield return new object?[] { "(?<open>a)+(?<-open>b)+", "aab", true };
        yield return new object?[] { "(?<open>a)+(?<-open>b)+", "bbb", false };
        yield return new object?[] { "(?<open>a)(?<-open>b)", "ab", true };
        yield return new object?[] { "(?<open>a)(?<-open>b)", "ac", false };
        yield return new object?[] { "(?<a>x)(?<b-a>y)", "xy", true };
        yield return new object?[] { "(?<a>x)(?<b-a>y)", "xx", false };
        yield return new object?[] { "(?<a>x)(?<b-a>y)(?<c-b>z)", "xyz", true };
        yield return new object?[] { "(?<o>a)(?<-o>b)?b", "ab", true };
        yield return new object?[] { "(?<o>a)+(?<-o>b)+(?<-o>c)", "aabc", true };
        yield return new object?[] { "((?<o>a)+(?<-o>b)+)", "aab", true };
        yield return new object?[] { "(?<o>a)+(?<-o>b)+\\1", "aabaa", true };
        yield return new object?[] { "(?<a>x)(?'b-a'y)", "xy", true };
        yield return new object?[] { "(?<o>\\p{L}+)(?<-o>\\d+)", "ab12", true };
        yield return new object?[] { "(?<o>\\p{L}+)(?<-o>\\d+)", "ab1", true };
        yield return new object?[] { "(?<o>\\p{L}+)(?<-o>\\d+)", "ab", false };
        yield return new object?[] { "(?<o>\\p{L}+)(?<-o>\\p{Nd}+)", "ab12", true };
        yield return new object?[] { "(?<o>\\p{L}+)(?<-o>\\p{Nd}+)", "ab", false };
    }

    [Theory]
    [MemberData(nameof(IsMatchCases))]
    public void OwnedIsMatchMatchesOracle(string pattern, string inputText, bool expected)
    {
        var input = Encoding.UTF8.GetBytes(inputText);
        var oracle = new Regex(pattern, RegexOptions.CultureInvariant, Regex.InfiniteMatchTimeout);
        Assert.Equal(expected, oracle.IsMatch(inputText));
        foreach (var options in new[]
        {
            RegexOptions.None,
            RegexOptions.CultureInvariant,
            RegexOptions.Compiled | RegexOptions.CultureInvariant,
        })
        {
            var regex = new Utf8Regex(pattern, options);
            Assert.True(regex.Inspection.DebugUsesOwnedSliceProgram, options.ToString());
            Assert.Equal(expected, regex.IsMatch(input));
            using var source = new CancellationTokenSource();
            Assert.Equal(expected, regex.IsMatch(input, source.Token));
            Assert.False(regex.Inspection.DebugFallbackRegexIsCreated, "pattern=" + pattern);
        }
    }

    public static IEnumerable<object?[]> CountCases()
    {
        yield return new object?[] { "(?<open>a)+(?<-open>b)+", "aab aab", 2 };
        yield return new object?[] { "(?<o>a)(?<-o>b)", "ab ab", 2 };
        yield return new object?[] { "(?<o>\\p{L}+)(?<-o>\\d+)", "ab12 cd34", 2 };
    }

    [Theory]
    [MemberData(nameof(CountCases))]
    public void OwnedCountMatchesOracle(string pattern, string inputText, int expected)
    {
        var input = Encoding.UTF8.GetBytes(inputText);
        var oracle = new Regex(pattern, RegexOptions.CultureInvariant, Regex.InfiniteMatchTimeout);
        Assert.Equal(expected, oracle.Matches(inputText).Count);
        var regex = new Utf8Regex(pattern, RegexOptions.CultureInvariant);
        Assert.True(regex.Inspection.DebugUsesOwnedSliceProgram, "pattern=" + pattern);
        Assert.Equal(expected, regex.Count(input));
        Assert.False(regex.Inspection.DebugFallbackRegexIsCreated, "pattern=" + pattern);
    }

    [Fact]
    public void OwnedDetailedDrainsPoppedCaptures()
    {
        var regex = new Utf8Regex("(?<open>a)+(?<-open>b)+", RegexOptions.CultureInvariant);
        Assert.True(regex.Inspection.DebugUsesOwnedSliceProgram);
        var oracle = new Regex("(?<open>a)+(?<-open>b)+", RegexOptions.CultureInvariant, Regex.InfiniteMatchTimeout).Match("aab");
        var detailed = regex.MatchDetailed(Encoding.UTF8.GetBytes("aab"));
        Assert.True(detailed.Success);
        Assert.Equal(oracle.Groups[1].Value, detailed.GetGroup(1).GetValueString());
        Assert.Equal("a", detailed.GetGroup(1).GetValueString());
        Assert.Equal(oracle.Groups[1].Captures.Count, detailed.GetGroup(1).CaptureCount);
        Assert.Equal(1, detailed.GetGroup(1).CaptureCount);
        Assert.Equal("a", detailed.GetGroup(1).GetCapture(0).GetValueString());
        Assert.False(regex.Inspection.DebugFallbackRegexIsCreated);
    }

    [Fact]
    public void OwnedDetailedProjectsIntervalCaptures()
    {
        var regex = new Utf8Regex("(?<a>x)(?<b-a>y)(?<c-b>z)", RegexOptions.CultureInvariant);
        Assert.True(regex.Inspection.DebugUsesOwnedSliceProgram);
        var detailed = regex.MatchDetailed(Encoding.UTF8.GetBytes("xyz"));
        Assert.True(detailed.Success);
        Assert.False(detailed.GetGroup(1).Success);
        Assert.False(detailed.GetGroup(2).Success);
        Assert.Equal("y", detailed.GetGroup(3).GetValueString());
        Assert.False(regex.Inspection.DebugFallbackRegexIsCreated);
    }

    [Theory]
    [InlineData("(?<o>a)(?<-o>b)", "xab", 1, 3)]
    public void OwnedFromOffsetMatchesOracle(
        string pattern, string inputText, int offset, int expectedEnd)
    {
        var input = Encoding.UTF8.GetBytes(inputText);
        var oracle = new Regex(pattern, RegexOptions.CultureInvariant, Regex.InfiniteMatchTimeout);
        var expected = oracle.Match(inputText, offset);
        var regex = new Utf8Regex(pattern, RegexOptions.CultureInvariant);
        Assert.Equal(expected.Success, regex.IsMatchFromUtf16Offset(input, offset));
        var actual = regex.MatchFromUtf16Offset(input, offset);
        Assert.Equal(expected.Success, actual.Success);
        if (expected.Success)
        {
            Assert.Equal(expectedEnd, actual.IndexInUtf16 + actual.LengthInUtf16);
        }

        Assert.False(regex.Inspection.DebugFallbackRegexIsCreated, "pattern=" + pattern);
    }

    [Theory]
    [InlineData("(?<o>a)(?<-o>b)", "ab", "<$1>")]
    [InlineData("(?<x>a)+(?<-x>b)+", "xaabb\U0001F600", "Q")]
    public void OwnedReplaceMatchesOracle(
        string pattern, string inputText, string replacement)
    {
        var input = Encoding.UTF8.GetBytes(inputText);
        var oracle = new Regex(pattern, RegexOptions.CultureInvariant, Regex.InfiniteMatchTimeout).Replace(inputText, replacement);
        var regex = new Utf8Regex(pattern, RegexOptions.CultureInvariant);
        Assert.True(regex.Inspection.DebugUsesOwnedSliceProgram, "pattern=" + pattern);
        Assert.Equal(oracle, Encoding.UTF8.GetString(regex.Replace(input, replacement)));
        Assert.False(regex.Inspection.DebugFallbackRegexIsCreated, "pattern=" + pattern);
    }

    [Fact]
    public void OwnedCatastrophicBalanceIsMatchTimesOut()
    {
        var input = Encoding.UTF8.GetBytes(new string('a', 25));
        var regex = new Utf8Regex("(?<o>(a+)+)(?<-o>b)c", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(1));
        Assert.True(regex.Inspection.DebugUsesOwnedSliceProgram);
        Assert.Throws<RegexMatchTimeoutException>(() => regex.IsMatch(input));
        Assert.False(regex.Inspection.DebugFallbackRegexIsCreated, "pattern=(?<o>(a+)+)(?<-o>b)c");
    }

    public static IEnumerable<object?[]> AstralInputCases()
    {
        var astral = char.ConvertFromUtf32(0x1F600);
        yield return new object?[] { "(?<o>a)+(?<-o>b)+", astral + "aab", true };
        yield return new object?[] { "(?<o>a)+(?<-o>b)+", astral + "bbb", false };
        yield return new object?[] { "(?<x>a)+(?<-x>b)+", "xaabb\U0001F600", true };
    }

    [Theory]
    [MemberData(nameof(AstralInputCases))]
    public void OwnedAstralInputMatchesOracle(string pattern, string inputText, bool expected)
    {
        var input = Encoding.UTF8.GetBytes(inputText);
        var oracle = new Regex(pattern, RegexOptions.CultureInvariant, Regex.InfiniteMatchTimeout);
        Assert.Equal(expected, oracle.IsMatch(inputText));
        var regex = new Utf8Regex(pattern, RegexOptions.CultureInvariant);
        Assert.True(regex.Inspection.DebugUsesOwnedSliceProgram, "pattern=" + pattern);
        Assert.Equal(expected, regex.IsMatch(input));
        Assert.False(regex.Inspection.DebugFallbackRegexIsCreated, "pattern=" + pattern);
    }

    public static IEnumerable<object?[]> LookbehindCaptureDeclineCases()
    {
        // Captures or pops inside a lookbehind body would alias the shared
        // balance heads through nested tables, so balancing mixes with
        // lookbehind group writes decline to legacy-exact routing.
        yield return new object?[] { "(?<=(a))(?<o>x)+(?<-o>y)+", "axy" };
        yield return new object?[] { "(?<!a(?<q>z))(?<o>x)+(?<-o>y)+", "xy" };
        yield return new object?[] { "(?<o>a)+(?<=b(?<-o>))c", "abc" };
    }

    [Theory]
    [MemberData(nameof(LookbehindCaptureDeclineCases))]
    public void LookbehindGroupWritesDecline(string pattern, string inputText)
    {
        var input = Encoding.UTF8.GetBytes(inputText);
        var oracle = new Regex(pattern, RegexOptions.CultureInvariant, Regex.InfiniteMatchTimeout);
        var regex = new Utf8Regex(pattern, RegexOptions.CultureInvariant);
        Assert.False(regex.Inspection.DebugUsesOwnedSliceProgram, "pattern=" + pattern);
        Assert.Equal(oracle.IsMatch(inputText), regex.IsMatch(input));
        Assert.True(regex.Inspection.DebugFallbackRegexIsCreated, "pattern=" + pattern);
    }

    public static IEnumerable<object?[]> LookaheadBalanceCases()
    {
        // Inline lookahead shares the attempt tables, so balancing around
        // and inside lookahead stays owned with BCL parity.
        yield return new object?[] { "(?<o>a)+(?=b)(?<-o>b)+", "aabb", true };
        yield return new object?[] { "(?<o>a)+(?=b)(?<-o>b)+", "aab", true };
        yield return new object?[] { "(?<o>a)+(?=b)(?<-o>b)+", "aacc", false };
        yield return new object?[] { "(?<o>a)+(?=a(?<-o>))a+", "aaa", true };
    }

    [Theory]
    [MemberData(nameof(LookaheadBalanceCases))]
    public void OwnedLookaheadBalanceMatchesOracle(string pattern, string inputText, bool expected)
    {
        var input = Encoding.UTF8.GetBytes(inputText);
        var oracle = new Regex(pattern, RegexOptions.CultureInvariant, Regex.InfiniteMatchTimeout);
        Assert.Equal(expected, oracle.IsMatch(inputText));
        var regex = new Utf8Regex(pattern, RegexOptions.CultureInvariant);
        Assert.True(regex.Inspection.DebugUsesOwnedSliceProgram, "pattern=" + pattern);
        Assert.Equal(expected, regex.IsMatch(input));
        Assert.False(regex.Inspection.DebugFallbackRegexIsCreated, "pattern=" + pattern);
    }
}