using System.Text;
using Lokad.Utf8Regex.Pcre2;

namespace Lokad.Utf8Regex.Pcre2.Tests;

// R03 lookaround slice for the PCRE2 profile: fixed-shape lookahead and
// fixed-length lookbehind run every operation on the shared slice runner
// (atomic first-success lookahead, rightmost-start lookbehind, shared-slot
// captures with snapshot restore). Single-group backrefs keep the
// specialized literal-family IsMatch/Count/Enumerate where one exists;
// the SameStartLiteral Count/Enumerate keeps precedence for its shapes.
// Variable-length lookbehind declines to the generic backtracking VM, as do
// named references and Caseless shapes. Expectations are BCL-computed
// ground truths on case-sensitive ASCII shapes where BCL and native PCRE2
// agree.
public sealed class Pcre2SliceLookaroundTests
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
    public void LookaroundShapesRouteThroughSlice()
    {
        foreach (var pattern in new[] { "(?=a)a", "a(?!b)", "(?<=a)b", "(?<!a)b", "foo(?!bar)", "(?=(?=ab))ab", "(?<=(a|b))\\1" })
        {
            var regex = new Utf8Pcre2Regex(pattern, Pcre2CompileOptions.None);
            Assert.Equal(
                "IsMatch=Pcre2Slice, Count=Pcre2Slice, Enumerate=Pcre2Slice, Match=Pcre2Slice, Replace=Pcre2Slice",
                regex.DebugDescribeExecutionPlan());
        }

        var sameStart = new Utf8Pcre2Regex("(?<=abc)(|def)", Pcre2CompileOptions.None);
        Assert.Equal(
            "IsMatch=Pcre2Slice, Count=Pcre2SameStartLiteral, Enumerate=Pcre2SameStartLiteral, Match=Pcre2Slice, Replace=Pcre2Slice",
            sameStart.DebugDescribeExecutionPlan());
    }

    [Theory]
    [InlineData("(?<=a+)b")]
    [InlineData("(?<!a+)b")]
    [InlineData("(?<=(a|b)+)c")]
    [InlineData("(?=(?<w>a))\\k<w>")]
    public void NonSliceLookaroundShapesDeclineSliceRouting(string pattern)
    {
        var regex = new Utf8Pcre2Regex(pattern, Pcre2CompileOptions.None);
        Assert.DoesNotContain("Pcre2Slice", regex.DebugDescribeExecutionPlan());
    }

    public static IEnumerable<object?[]> IsMatchCases()
    {
        yield return new object?[] { "(?=a)a", "xaa", true };
        yield return new object?[] { "a(?!b)", "ac", true };
        yield return new object?[] { "a(?!b)", "ab", false };
        yield return new object?[] { "(?<=a)b", "xab", true };
        yield return new object?[] { "(?<=a)b", "xcb", false };
        yield return new object?[] { "(?<!a)b", "xcb", true };
        yield return new object?[] { "foo(?!bar)", "xxfoobaz", true };
        yield return new object?[] { "(?=(?=ab))ab", "xxab", true };
        yield return new object?[] { "(?<=abc)def", "xxabcdef", true };
        yield return new object?[] { "(?<!abc)def", "xxzzzdef", true };
        yield return new object?[] { "(?<=(a|ab))", "xab", true };
        yield return new object?[] { "(?<=a|ab)c", "xabc", true };
    }

    [Theory]
    [MemberData(nameof(IsMatchCases))]
    public void OwnedIsMatchMatchesNativeSemantics(
        string pattern, string inputText, bool expected)
    {
        Assert.Equal(expected, Bcl(pattern).IsMatch(inputText));
        var reuse = new Utf8Pcre2Regex(pattern, Pcre2CompileOptions.None);
        Assert.Equal(expected, reuse.IsMatch(Utf8(inputText)));
        Assert.False(reuse.DebugFoundationCoreFallbackRegexIsCreated, "pattern=" + pattern);
        var explicitStart = new Utf8Pcre2Regex(pattern, Pcre2CompileOptions.None);
        Assert.Equal(expected, explicitStart.IsMatch(Utf8(inputText), 0));
    }

    [Theory]
    [InlineData("(?=a)a", "aaa", 0, 3)]
    [InlineData("(?<=a)b", "aab ab", 0, 2)]
    [InlineData("(?<=abc)(|def)", "abcdefabc", 0, 3)]
    public void OwnedCountMatchesNativeSemantics(
        string pattern, string inputText, int startOffsetInBytes, int expected)
    {
        // Expectations are native PCRE2 global counts: the same-position
        // non-empty retry adds retried matches BCL would not count (the
        // empty-first-alternative row counts 3 natively, 2 under BCL).
        var regex = new Utf8Pcre2Regex(pattern, Pcre2CompileOptions.None);
        Assert.Equal(expected, regex.Count(Utf8(inputText), startOffsetInBytes));
        Assert.False(regex.DebugFoundationCoreFallbackRegexIsCreated, "pattern=" + pattern);
    }

    [Theory]
    [InlineData("foo(?!bar)", "xxfoobaz", 0, true, 2, 5)]
    [InlineData("(?<=(a|ab))", "xab", 0, true, 2, 2)]
    [InlineData("(?<=abc)(|def)", "abcdefabc", 0, true, 3, 3)]
    public void OwnedFirstMatchProjectsBytes(
        string pattern, string inputText, int startOffsetInBytes, bool expectedSuccess, int expectedStart, int expectedEnd)
    {
        var expected = Bcl(pattern).Match(inputText);
        Assert.Equal(expectedSuccess, expected.Success);
        var regex = new Utf8Pcre2Regex(pattern, Pcre2CompileOptions.None);
        var found = regex.Match(Utf8(inputText), startOffsetInBytes);
        Assert.Equal(expectedSuccess, found.Success);
        if (expectedSuccess)
        {
            Assert.Equal(expected.Index, found.StartOffsetInBytes);
            Assert.Equal(expected.Index + expected.Length, found.EndOffsetInBytes);
            Assert.Equal(expectedStart, found.StartOffsetInBytes);
            Assert.Equal(expectedEnd, found.EndOffsetInBytes);
        }

        Assert.False(regex.DebugFoundationCoreFallbackRegexIsCreated, "pattern=" + pattern);
    }

    [Fact]
    public void OwnedEnumerationAdvancesThroughLookarounds()
    {
        var regex = new Utf8Pcre2Regex("(?=a)a", Pcre2CompileOptions.None);
        var enumerator = regex.EnumerateMatches(Utf8("aaa"), 0);
        var ranges = new List<string>();
        while (enumerator.MoveNext())
        {
            ranges.Add(
                enumerator.Current.StartOffsetInBytes
                + "-" + enumerator.Current.EndOffsetInBytes);
        }

        Assert.Equal(["0-1", "1-2", "2-3"], ranges);
        Assert.False(regex.DebugFoundationCoreFallbackRegexIsCreated);
    }

    [Fact]
    public void OwnedDetailedProjectsLookaroundGroups()
    {
        var regex = new Utf8Pcre2Regex("(?<=(a|ab))", Pcre2CompileOptions.None);
        var detailed = regex.MatchDetailed(Utf8("xab"), 0);
        Assert.True(detailed.Success);
        Assert.Equal("a", detailed.GetGroup(1).GetValueString());
        Assert.False(regex.DebugFoundationCoreFallbackRegexIsCreated);
    }

    [Fact]
    public void OwnedReplaceExpandsLookaroundMatch()
    {
        var regex = new Utf8Pcre2Regex("(?<=a)b", Pcre2CompileOptions.None);
        Assert.Equal("aa<b> a<b>", regex.ReplaceToString(Utf8("aab ab"), "<$0>"));
        Assert.False(regex.DebugFoundationCoreFallbackRegexIsCreated);
    }

    [Fact]
    public void OwnedReplaceEvaluatorSeesRetriedNonEmptyMatch()
    {
        // Empty-then-nonempty yields project the retried captures, not the
        // empty first success.
        var regex = new Utf8Pcre2Regex("(?<=abc)(|def)", Pcre2CompileOptions.None);
        var actual = regex.ReplaceToString(
            Utf8("abcdefabc"),
            0,
            static (in Utf8Pcre2MatchContext match, ref int state) =>
            {
                state++;
                return "[" + match.Value.StartOffsetInBytes + ":" + match.GetValueString() + "]";
            });
        Assert.Equal("abc[3:][3:def]abc[9:]", actual);
        Assert.False(regex.DebugFoundationCoreFallbackRegexIsCreated);
    }
}