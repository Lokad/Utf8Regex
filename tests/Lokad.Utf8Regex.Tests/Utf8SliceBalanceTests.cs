using System.Text;
using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex.Tests;

// R03 balancing captures: successful body close pops n2 (fail if empty),
// including sources established by the body, and captures its gap/intersection
// interval into n1; (?<-n2>sub) pops without capturing. Stacks use per-group
// heads journaled through the trail, so backtracking undoes pops exactly
// like capture writes, and detailed histories drain popped captures.
// Targets bind against the complete capture catalog, including forward,
// explicitly numbered and shared declarations. Undefined targets and malformed
// spellings fail construction.
public sealed class Utf8SliceBalanceTests
{
    [Theory]
    [InlineData("(?=(?=(?<p>😀)(?<-p>é)))😀é")]
    [InlineData("(?:(?=(?<p>😀)(?<-p>é))Z|😀é)")]
    public void AssertionBalancingKeepsItsHeadsAndRollback(string pattern)
    {
        using var cancellation = new CancellationTokenSource();
        foreach (var options in new[] { RegexOptions.None, RegexOptions.Compiled })
        {
            var regex = new Utf8Regex(pattern, options: options, policy: new Utf8RegexPolicy
            {
                MatchTimeout = Regex.InfiniteMatchTimeout,
                ResourcePolicy = new()
                {
                    MaxWorkSteps = 100000,
                    MaxDepth = 128,
                    MaxWorkingBytes = 1000000,
                    MaxResultBytes = 1000000
                }
            });
            var retained = Utf8MatchSequence.First(regex.CreateMatchCursor("z😀é😀é"u8.ToArray(), cancellationToken: cancellation.Token));
            Assert.True(retained.Success);
            Assert.Equal(1, retained.Value.StartOffsetInBytes);
            Assert.Equal(6, (retained.Value.EndOffsetInBytes - retained.Value.StartOffsetInBytes));
            Assert.True(retained.TryGetGroup(1, out var range1));
            Assert.False(range1.Success);
            Assert.Equal(2, regex.Count("z😀é😀é"u8, cancellation.Token));
            Assert.Equal("😀é", System.Text.Encoding.UTF8.GetString(retained.GetValueMemory().Span));
            Assert.True(retained.TryGetGroup(1, out var range2));
            Assert.False(range2.Success);
        }
    }

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
            var regex = new Utf8Regex(pattern, options: options);
            Assert.True(regex.Inspection.DebugUsesOwnedSliceProgram, options.ToString());
        }
    }

    [Theory]
    [InlineData("(?<-b>a)(?<b>c)")]
    [InlineData("(?<a-a>x)")]
    [InlineData("(?<a-1>x)")]
    public void ForwardAndSelfBalanceShapesRouteOwned(string pattern)
    {
        foreach (var options in new[] { RegexOptions.CultureInvariant,
            RegexOptions.CultureInvariant | RegexOptions.Compiled,
            RegexOptions.CultureInvariant | RegexOptions.RightToLeft,
            RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.RightToLeft })
        {
            var oracle = new Regex(pattern, options);
            var regex = new Utf8Regex(pattern, options: options);
            foreach (var text in new[] { "", "x", "abc", "xxx" })
            {
                var input = Encoding.UTF8.GetBytes(text);
                Assert.Equal(oracle.IsMatch(text), regex.IsMatch(input));
                Assert.Equal(oracle.Count(text), regex.Count(input));
            }
            Assert.True(regex.Inspection.DebugUsesOwnedSliceProgram, "pattern=" + pattern);
            Assert.True(regex.Inspection.PreparedRegex.ScalarProgram is not null);
        }
    }

    [Theory]
    [InlineData("(?<-open>b)")]
    [InlineData("(?<a-b>x)")]
    [InlineData("(?<b-a>x)(?<b-c>y)")]
    [InlineData("(?<1-a>x)")]
    [InlineData("(?'b-a'y)")]
    public void UndefinedBalanceShapesFailConstruction(string pattern)
    {
        Utf8PatternFailureAssert.Throws(
            () => new Utf8Regex(pattern, options: RegexOptions.CultureInvariant));
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
            var regex = new Utf8Regex(pattern, options: options);
            Assert.True(regex.Inspection.DebugUsesOwnedSliceProgram, options.ToString());
            Assert.Equal(expected, regex.IsMatch(input));
            using var source = new CancellationTokenSource();
            Assert.Equal(expected, regex.IsMatch(input, source.Token));
            Assert.True(regex.Inspection.PreparedRegex.ScalarProgram is not null, "pattern=" + pattern);
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
        var regex = new Utf8Regex(pattern, options: RegexOptions.CultureInvariant);
        Assert.True(regex.Inspection.DebugUsesOwnedSliceProgram, "pattern=" + pattern);
        Assert.Equal(expected, regex.Count(input));
        Assert.True(regex.Inspection.PreparedRegex.ScalarProgram is not null, "pattern=" + pattern);
    }

    [Fact]
    public void OwnedDetailedDrainsPoppedCaptures()
    {
        var regex = new Utf8Regex("(?<open>a)+(?<-open>b)+", options: RegexOptions.CultureInvariant);
        Assert.True(regex.Inspection.DebugUsesOwnedSliceProgram);
        var oracle = new Regex("(?<open>a)+(?<-open>b)+", RegexOptions.CultureInvariant, Regex.InfiniteMatchTimeout).Match("aab");
        var detailed = Utf8MatchSequence.First(regex.CreateMatchCursor(Encoding.UTF8.GetBytes("aab")));
        Assert.True(detailed.Success);
        Assert.Equal(oracle.Groups[1].Value, System.Text.Encoding.UTF8.GetString(detailed.GetGroupMemory(1).Span));
        Assert.Equal("a", System.Text.Encoding.UTF8.GetString(detailed.GetGroupMemory(1).Span));
        Assert.Equal(oracle.Groups[1].Captures.Count, detailed.GetCaptures(1).Length);
        Assert.Equal(1, detailed.GetCaptures(1).Length);
        Assert.Equal("a", System.Text.Encoding.UTF8.GetString(((global::System.ReadOnlyMemory<byte>)Encoding.UTF8.GetBytes("aab")).Span.Slice(detailed.GetCaptures(1)[0].StartOffsetInBytes, detailed.GetCaptures(1)[0].EndOffsetInBytes - detailed.GetCaptures(1)[0].StartOffsetInBytes)));
        Assert.True(regex.Inspection.PreparedRegex.ScalarProgram is not null);
    }

    [Fact]
    public void OwnedDetailedProjectsIntervalCaptures()
    {
        var regex = new Utf8Regex("(?<a>x)(?<b-a>y)(?<c-b>z)", options: RegexOptions.CultureInvariant);
        Assert.True(regex.Inspection.DebugUsesOwnedSliceProgram);
        var detailed = Utf8MatchSequence.First(regex.CreateMatchCursor(Encoding.UTF8.GetBytes("xyz")));
        Assert.True(detailed.Success);
        Assert.True(detailed.TryGetGroup(1, out var range3));
        Assert.False(range3.Success);
        Assert.True(detailed.TryGetGroup(2, out var range4));
        Assert.False(range4.Success);
        Assert.Equal("y", System.Text.Encoding.UTF8.GetString(detailed.GetGroupMemory(3).Span));
        Assert.True(regex.Inspection.PreparedRegex.ScalarProgram is not null);
    }

    [Theory]
    [InlineData("(?<o>a)(?<-o>b)", "xab", 1, 3)]
    public void OwnedFromOffsetMatchesOracle(
        string pattern, string inputText, int offset, int expectedEnd)
    {
        var input = Encoding.UTF8.GetBytes(inputText);
        var oracle = new Regex(pattern, RegexOptions.CultureInvariant, Regex.InfiniteMatchTimeout);
        var expected = oracle.Match(inputText, offset);
        var regex = new Utf8Regex(pattern, options: RegexOptions.CultureInvariant);
        Assert.Equal(expected.Success, regex.Match(input, Encoding.UTF8.GetByteCount(inputText.AsSpan(0, offset)), []).Success);
        var actual = regex.Match(input, Encoding.UTF8.GetByteCount(inputText.AsSpan(0, offset)), []);
        Assert.Equal(expected.Success, actual.Success);
        if (expected.Success)
        {
            Assert.Equal(Encoding.UTF8.GetByteCount(inputText.AsSpan(0, expectedEnd)), actual.EndOffsetInBytes);
        }

        Assert.True(regex.Inspection.PreparedRegex.ScalarProgram is not null, "pattern=" + pattern);
    }

    [Theory]
    [InlineData("(?<o>a)(?<-o>b)", "ab", "<$1>")]
    [InlineData("(?<x>a)+(?<-x>b)+", "xaabb\U0001F600", "Q")]
    public void OwnedReplaceMatchesOracle(
        string pattern, string inputText, string replacement)
    {
        var input = Encoding.UTF8.GetBytes(inputText);
        var oracle = new Regex(pattern, RegexOptions.CultureInvariant, Regex.InfiniteMatchTimeout).Replace(inputText, replacement);
        var regex = new Utf8Regex(pattern, options: RegexOptions.CultureInvariant);
        Assert.True(regex.Inspection.DebugUsesOwnedSliceProgram, "pattern=" + pattern);
        Assert.Equal(oracle, Encoding.UTF8.GetString(regex.Replace(input, replacement)));
        Assert.True(regex.Inspection.PreparedRegex.ScalarProgram is not null, "pattern=" + pattern);
    }

    [Fact]
    public void OwnedCatastrophicBalanceIsMatchTimesOut()
    {
        var input = Encoding.UTF8.GetBytes("bc" + new string('a', 25));
        var regex = new Utf8Regex("(?<o>(a+)+)(?<-o>b)c", options: RegexOptions.CultureInvariant, policy: new Utf8RegexPolicy
        {
            MatchTimeout = TimeSpan.FromMilliseconds(1)
        });
        Assert.True(regex.Inspection.DebugUsesOwnedSliceProgram);
        Assert.Throws<RegexMatchTimeoutException>(() => regex.IsMatch(input));
        Assert.True(regex.Inspection.PreparedRegex.ScalarProgram is not null, "pattern=(?<o>(a+)+)(?<-o>b)c");
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
        var regex = new Utf8Regex(pattern, options: RegexOptions.CultureInvariant);
        Assert.True(regex.Inspection.DebugUsesOwnedSliceProgram, "pattern=" + pattern);
        Assert.Equal(expected, regex.IsMatch(input));
        Assert.True(regex.Inspection.PreparedRegex.ScalarProgram is not null, "pattern=" + pattern);
    }

    public static IEnumerable<object?[]> LookbehindCaptureDeclineCases()
    {
        // Assertion bodies share the caller's balance nodes, head mutations
        // and capture openings; negative or failed probes restore their mark.
        yield return new object?[] { "(?<=(a))(?<o>x)+(?<-o>y)+", "axy" };
        yield return new object?[] { "(?<!a(?<q>z))(?<o>x)+(?<-o>y)+", "xy" };
        yield return new object?[] { "(?<o>a)+(?<=b(?<-o>))c", "abc" };
    }

    [Theory]
    [MemberData(nameof(LookbehindCaptureDeclineCases))]
    public void LookbehindGroupWritesExecuteOwned(string pattern, string inputText)
    {
        var input = Encoding.UTF8.GetBytes(inputText);
        var oracle = new Regex(pattern, RegexOptions.CultureInvariant, Regex.InfiniteMatchTimeout);
        var regex = new Utf8Regex(pattern, options: RegexOptions.CultureInvariant);
        Assert.True(regex.Inspection.DebugUsesOwnedSliceProgram, "pattern=" + pattern);
        Assert.Equal(oracle.IsMatch(inputText), regex.IsMatch(input));
        Assert.True(regex.Inspection.PreparedRegex.ScalarProgram is not null, "pattern=" + pattern);
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
        var regex = new Utf8Regex(pattern, options: RegexOptions.CultureInvariant);
        Assert.True(regex.Inspection.DebugUsesOwnedSliceProgram, "pattern=" + pattern);
        Assert.Equal(expected, regex.IsMatch(input));
        Assert.True(regex.Inspection.PreparedRegex.ScalarProgram is not null, "pattern=" + pattern);
    }
}
