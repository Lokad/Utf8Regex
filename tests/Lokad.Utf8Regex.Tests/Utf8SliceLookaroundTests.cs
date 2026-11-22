using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Diagnostics;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Tests;

// R03 lookaround slice: positive and negative lookahead lower to owned
// fork/jump/fail shapes (atomic first-success on the shared trail) while
// lookbehind lowers to backward assertion programs at the exact current edge.
// Both directions share capture openings and balancing nodes with their caller;
// positive bodies retain their trail and other exits restore its entry mark.
// Unsupported or malformed grammar retains its construction behavior.
public sealed class Utf8SliceLookaroundTests
{
    public static IEnumerable<object?[]> VariableLookbehindOwnedShapes()
    {
        // Variable-length bodies follow backward greedy/lazy choice order
        // from the assertion edge; they stay owned on every lane.
        yield return new object?[] { "(?<=(a|ab))", "xab" };
        yield return new object?[] { "(?<=a+)b", "aaab" };
        yield return new object?[] { "(?<=a+)b", "aaac" };
        yield return new object?[] { "(?<=x*)x", "xx" };
        yield return new object?[] { "(?<=[a-z]*)x", "axb" };
        yield return new object?[] { "(?<=\\p{L}*)x", "axb" };
        yield return new object?[] { "(?<=\\p{L}+)x", "axb" };
        yield return new object?[] { "(?<!x*)x", "xx" };
        yield return new object?[] { "(?<![a-z]*)x", "axb" };
        yield return new object?[] { "(?<!\\p{L}+)x", "axb" };
        yield return new object?[] { "(?<=\\p{L}{2,3})x", "abcx" };
        yield return new object?[] { "(?<=a{2,3})x", "aaax" };
        yield return new object?[] { "(?<![a-z]{1,2})x", "12x" };
    }

    public static IEnumerable<object?[]> IgnoreCaseLookbehindCases()
    {
        // IgnoreCase folds inside lookbehind bodies exactly like the main
        // stream (cased-union masks, invariant block equivalents).
        yield return new object?[] { "(?<=\\p{Ll})X", "aX", true };
        yield return new object?[] { "(?<=\\p{Ll})X", "AXB", true };
        yield return new object?[] { "(?<=\\p{IsBasicLatin})X", "aX", true };
        yield return new object?[] { "(?<!\\p{Lu})x", "ax", false };
        yield return new object?[] { "(?<=[\\p{L}a])X", "1X", false };
    }

    [Theory]
    [MemberData(nameof(IgnoreCaseLookbehindCases))]
    public void OwnedIgnoreCaseLookbehindMatchesOracle(string pattern, string inputText, bool expected)
    {
        var options = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;
        var input = Encoding.UTF8.GetBytes(inputText);
        var oracle = new Regex(pattern, options, Regex.InfiniteMatchTimeout);
        Assert.Equal(expected, oracle.IsMatch(inputText));
        var regex = new Utf8Regex(pattern, options: options);
        Assert.True(regex.Inspection.DebugUsesOwnedSliceProgram, "pattern=" + pattern);
        Assert.Equal(expected, regex.IsMatch(input));
        Assert.True(regex.Inspection.PreparedRegex.ScalarProgram is not null, "pattern=" + pattern);
    }

    [Theory]
    [MemberData(nameof(VariableLookbehindOwnedShapes))]
    public void VariableWidthLookbehindServesOwned(string pattern, string inputText)
    {
        var input = Encoding.UTF8.GetBytes(inputText);
        var oracle = new Regex(pattern, RegexOptions.CultureInvariant, Regex.InfiniteMatchTimeout);
        var regex = new Utf8Regex(pattern, options: RegexOptions.CultureInvariant);
        Assert.True(regex.Inspection.DebugUsesOwnedSliceProgram, "pattern=" + pattern);
        Assert.Equal(oracle.IsMatch(inputText), regex.IsMatch(input));
        Assert.Equal(oracle.Matches(inputText).Count, regex.Count(input));
        Assert.True(regex.Inspection.PreparedRegex.ScalarProgram is not null, "pattern=" + pattern);
    }
    [Theory]
    [InlineData("(?:a)")]
    [InlineData("(?<name>a)")]
    [InlineData("(?'name'a)")]
    public void NonLookaroundGroupShapesDecline(string pattern)
    {
        var regex = new Utf8Regex(pattern, options: RegexOptions.CultureInvariant);
        Assert.False(regex.Inspection.DebugUsesOwnedSliceProgram, "pattern=" + pattern);
    }

    [Fact]
    public void InvalidLookaroundFailsConstruction()
    {
        Utf8PatternFailureAssert.Throws(
            () => new Utf8Regex("(?P=name)", options: RegexOptions.CultureInvariant));
    }

    [Theory]
    [InlineData("(?<=(a))b", "xxab", 3, 1, "a")]
    [InlineData("(?<=(a))b", "xaab\U0001F600", 3, 1, "a")]
    [InlineData("((?=a)a)\\1", "xaaayy", 1, 2, "a")]
    public void OwnedMatchProjectsCoordinates(
        string pattern, string inputText, int expectedIndex, int expectedLength, string expectedGroup)
    {
        var input = Encoding.UTF8.GetBytes(inputText);
        var oracle = new Regex(pattern, RegexOptions.CultureInvariant, Regex.InfiniteMatchTimeout).Match(inputText);
        var regex = new Utf8Regex(pattern, options: RegexOptions.CultureInvariant);
        var actual = regex.Match(input);
        Assert.Equal(oracle.Success, actual.Success);
        Assert.True(actual.Success);
        Assert.Equal(System.Text.Encoding.UTF8.GetByteCount(inputText.AsSpan(0, expectedIndex)), actual.StartOffsetInBytes);
        Assert.Equal(System.Text.Encoding.UTF8.GetByteCount(inputText.AsSpan(expectedIndex, expectedLength)), (actual.EndOffsetInBytes - actual.StartOffsetInBytes));
        var detailed = Utf8MatchSequence.First(regex.CreateMatchCursor(input));
        Assert.True(detailed.Success);
        Assert.Equal(expectedGroup, System.Text.Encoding.UTF8.GetString(detailed.GetGroupMemory(1).Span));
        foreach (var item in regex.EnumerateMatches(input))
        {
            Assert.Equal(System.Text.Encoding.UTF8.GetByteCount(inputText.AsSpan(0, expectedIndex)), item.StartOffsetInBytes);
            Assert.Equal(System.Text.Encoding.UTF8.GetByteCount(inputText.AsSpan(expectedIndex, expectedLength)), (item.EndOffsetInBytes - item.StartOffsetInBytes));
        }

        Assert.True(regex.Inspection.PreparedRegex.ScalarProgram is not null, "pattern=" + pattern);
    }

    [Theory]
    [InlineData("(?!(a)b)c", "ac", 1, 1, 1)]
    [InlineData("((?=a)a)(b)", "ab", 0, 2, 2)]
    [InlineData("(?<=(a)|(b))", "cb", 2, 0, 2)]
    public void OwnedDetailedProjectsGroups(
        string pattern, string inputText, int expectedIndex, int expectedLength, int groups)
    {
        var input = Encoding.UTF8.GetBytes(inputText);
        var oracle = new Regex(pattern, RegexOptions.CultureInvariant, Regex.InfiniteMatchTimeout).Match(inputText);
        var regex = new Utf8Regex(pattern, options: RegexOptions.CultureInvariant);
        var detailed = Utf8MatchSequence.First(regex.CreateMatchCursor(input));
        Assert.Equal(oracle.Success, detailed.Success);
        Assert.True(detailed.Success);
        Utf8MatchSequence.AssertRange(detailed.Value, inputText, expectedIndex, expectedLength);
        Utf8MatchSequence.AssertCaptures(oracle, detailed, input, inputText);
        for (var group = 1; group <= groups; group++)
        {
            Assert.Equal(oracle.Groups[group].Success, (detailed.TryGetGroup(group, out var range1) ? range1.Success : default));
            if (oracle.Groups[group].Success)
            {
                Assert.Equal(oracle.Groups[group].Value, System.Text.Encoding.UTF8.GetString(detailed.GetGroupMemory(group).Span));
            }
        }

        Assert.True(regex.Inspection.PreparedRegex.ScalarProgram is not null, "pattern=" + pattern);
    }

    [Theory]
    [InlineData("(?<=a)b", "xab", 1, 3)]
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
    [InlineData("(?<=a)b", "aab ab", "X")]
    [InlineData("(?<=(a))b", "xa\U0001F600b", "Q")]
    [InlineData("(?=a)a", "aaa", "X")]
    public void OwnedReplaceMatchesOracle(
        string pattern, string inputText, string replacement)
    {
        var input = Encoding.UTF8.GetBytes(inputText);
        var oracle = new Regex(pattern, RegexOptions.CultureInvariant, Regex.InfiniteMatchTimeout).Replace(inputText, replacement);
        var regex = new Utf8Regex(pattern, options: RegexOptions.CultureInvariant);
        Assert.True(regex.Inspection.DebugUsesOwnedSliceProgram, "pattern=" + pattern);
        Assert.Equal(oracle, Encoding.UTF8.GetString(regex.Replace(input, Encoding.UTF8.GetBytes(replacement))));
        Assert.True(regex.Inspection.PreparedRegex.ScalarProgram is not null, "pattern=" + pattern);
    }

    [Fact]
    public void OwnedCatastrophicLookaheadIsMatchTimesOut()
    {
        // The required b occurs in the subject, separated from the candidate run.
        var input = Encoding.UTF8.GetBytes(new string('a', 30) + "!b");
        using var regex = new Utf8Regex("(?=(a+)+b)a", options: RegexOptions.CultureInvariant, policy: new Utf8RegexPolicy
        {
            MatchTimeout = TimeSpan.FromMilliseconds(20)
        });
        Assert.True(regex.Inspection.DebugUsesOwnedSliceProgram);
        long ticks = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
        var session = Utf8SearchDiagnosticsSession.Start(_ => ticks = 50);
        try
        {
            Assert.Throws<RegexMatchTimeoutException>(() => regex.IsMatch(input));
            Assert.Equal(50, ticks);
        }
        finally { session.Complete(); }
    }

    [Fact]
    public void PositiveLookaheadSeeksALateDelimiterWithinAFiniteWorkLimit()
    {
        var text = string.Concat(Enumerable.Repeat("lengthy-word Ω documentable ", 16)) + "été:";
        var input = Encoding.UTF8.GetBytes(text);
        using var regex = new Utf8Regex(@"\p{L}+(?=:)", RegexOptions.Compiled, new()
        {
            ResourcePolicy = new() { MaxWorkSteps = 128 }
        });

        Assert.True(regex.IsMatch(input));
        Assert.Equal(1, regex.Count(input));
        Utf8MatchSequence.AssertRange(regex.Match(input), text, text.Length - 4, 3);
    }

    [Theory]
    [InlineData(@"\p{L}+(?=::)", ":::étude:::abc::")]
    [InlineData(@"\p{L}+(?=aaa)", "aaa éxaaa!zabaaa")]
    [InlineData(@"\b\d{1,2}(?=::)", "98::x١٢:: ٤::")]
    [InlineData(@"(?<word>\p{L}+)(?=::)::(?<value>\d+)", "::absent😀étude::19!end::03")]
    public void PositiveLookaheadWindowsKeepOverlapOriginsAndCaptures(string pattern, string tail)
    {
        const RegexOptions options = RegexOptions.Compiled | RegexOptions.CultureInvariant;
        var text = new string('!', 288) + tail;
        var input = Encoding.UTF8.GetBytes(text);
        var oracle = new Regex(pattern, options);
        using var regex = new Utf8Regex(pattern, options);
        Assert.Equal(oracle.IsMatch(text), regex.IsMatch(input));
        Assert.Equal(oracle.Count(text), regex.Count(input));

        var first = oracle.Match(text);
        Assert.True(first.Success);
        Utf8MatchSequence.AssertRange(regex.Match(input), text, first.Index, first.Length);
        var origin = first.Index + first.Length;
        var next = oracle.Match(text, origin);
        Assert.True(next.Success);
        var slots = new Utf8CaptureRange[regex.CaptureGroupCount + 1];
        var actual = regex.Match(input, Encoding.UTF8.GetByteCount(text.AsSpan(0, origin)), slots);
        Utf8MatchSequence.AssertRange(actual, text, next.Index, next.Length);
        for (var group = 1; group < next.Groups.Count; group++)
            Utf8MatchSequence.AssertRange(slots[group], text, next.Groups[group].Index, next.Groups[group].Length);
    }

    public static IEnumerable<object?[]> AstralIsMatchCases()
    {
        var astral = char.ConvertFromUtf32(0x1F600);
        yield return new object?[] { "(?<=a)b", astral + "ab", true };
        yield return new object?[] { "(?=a)a", astral + "aa", true };
    }

    [Theory]
    [MemberData(nameof(AstralIsMatchCases))]
    public void OwnedAstralInputIsMatchMatchesOracle(string pattern, string inputText, bool expected)
    {
        var input = Encoding.UTF8.GetBytes(inputText);
        var oracle = new Regex(pattern, RegexOptions.CultureInvariant, Regex.InfiniteMatchTimeout);
        Assert.Equal(expected, oracle.IsMatch(inputText));
        var regex = new Utf8Regex(pattern, options: RegexOptions.CultureInvariant);
        Assert.True(regex.Inspection.DebugUsesOwnedSliceProgram, "pattern=" + pattern);
        Assert.Equal(expected, regex.IsMatch(input));
        Assert.True(regex.Inspection.PreparedRegex.ScalarProgram is not null, "pattern=" + pattern);
    }

    public static IEnumerable<object?[]> AstralDotBodyCases()
    {
        var astral = char.ConvertFromUtf32(0x1F600);
        yield return new object?[] { "(?<=.)b", astral + "b", 1 };
    }

    [Theory]
    [MemberData(nameof(AstralDotBodyCases))]
    public void AstralDotBodyUsesOwnedUnits(string pattern, string inputText, int expected)
    {
        var input = Encoding.UTF8.GetBytes(inputText);
        var oracle = new Regex(pattern, RegexOptions.CultureInvariant, Regex.InfiniteMatchTimeout);
        Assert.Equal(expected, oracle.Matches(inputText).Count);
        var regex = new Utf8Regex(pattern, options: RegexOptions.CultureInvariant);
        Assert.True(regex.Inspection.DebugUsesOwnedSliceProgram, "pattern=" + pattern);
        Assert.True(regex.Inspection.PreparedRegex.ScalarProgram is not null, "construction must stay lazy");
        Assert.Equal(expected, regex.Count(input));
        Assert.True(regex.Inspection.PreparedRegex.ScalarProgram is not null, "pattern=" + pattern);
    }
}
