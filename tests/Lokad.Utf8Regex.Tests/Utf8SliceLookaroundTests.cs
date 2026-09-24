using System.Text;
using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex.Tests;

// R03 lookaround slice: positive and negative lookahead lower to owned
// fork/jump/fail shapes (atomic first-success on the shared trail) while
// lookbehind lowers to owned assertion programs searched from the current
// position down to the body length bound (BCL rightmost-start order).
// Captures run on the shared slots with entry snapshots, so surviving
// positive bodies keep their groups and every other path restores them.
// Atomic, comment, branch-reset, conditional, named, and unclosed group
// forms decline to legacy-exact routing, as do variable-scalar-width
// lookbehind bodies (greedy forward evaluation would overshoot the edge).
public sealed class Utf8SliceLookaroundTests
{
    public static IEnumerable<object?[]> SliceLookaroundShapes()
    {
        yield return new object?[] { "(?=a)a" };
        yield return new object?[] { "(?!a)b" };
        yield return new object?[] { "(?<=a)b" };
        yield return new object?[] { "(?<!a)b" };
        yield return new object?[] { "((?=a)a)" };
        yield return new object?[] { "(?=(a)(b))" };
    }

    [Theory]
    [MemberData(nameof(SliceLookaroundShapes))]
    public void LookaroundShapesRouteOwned(string pattern)
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

    public static IEnumerable<object?[]> VariableLookbehindDeclines()
    {
        // Greedy forward evaluation overshoots the lookbehind edge when the
        // body admits several scalar widths on a byte-variable span, while a
        // shorter end would hit (BCL evaluates the body backward and finds
        // it): those bodies ride legacy-exact instead of staying owned.
        yield return new object?[] { "(?<=(a|ab))", "xab" };
        yield return new object?[] { "(?<=a+)b", "aaab" };
        yield return new object?[] { "(?<=a+)b", "aaac" };
        yield return new object?[] { "(?<=x*)x", "xx" };
        yield return new object?[] { "(?<=[a-z]*)x", "axb" };
        yield return new object?[] { "(?<=\\p{L}*)x", "axb" };
        yield return new object?[] { "(?<=\\p{L}+)x", "axb" };
    }

    [Theory]
    [MemberData(nameof(VariableLookbehindDeclines))]
    public void VariableWidthLookbehindDeclinesToLegacy(string pattern, string inputText)
    {
        var input = Encoding.UTF8.GetBytes(inputText);
        var oracle = new Regex(pattern, RegexOptions.CultureInvariant, Regex.InfiniteMatchTimeout);
        var regex = new Utf8Regex(pattern, RegexOptions.CultureInvariant);
        Assert.False(regex.Inspection.DebugUsesOwnedSliceProgram, "pattern=" + pattern);
        Assert.Equal(oracle.IsMatch(inputText), regex.IsMatch(input));
        Assert.Equal(oracle.Matches(inputText).Count, regex.Count(input));
    }

    [Theory]
    [InlineData("(?:a)")]
    [InlineData("(?<name>a)")]
    [InlineData("(?'name'a)")]
    public void NonLookaroundGroupShapesDecline(string pattern)
    {
        var regex = new Utf8Regex(pattern, RegexOptions.CultureInvariant);
        Assert.False(regex.Inspection.DebugUsesOwnedSliceProgram, "pattern=" + pattern);
    }

    [Fact]
    public void InvalidLookaroundFailsConstruction()
    {
        Assert.Throws<RegexParseException>(
            () => new Utf8Regex("(?P=name)", RegexOptions.CultureInvariant));
    }

    public static IEnumerable<object?[]> IsMatchCases()
    {
        yield return new object?[] { "(?=a)a", "ba", true };
        yield return new object?[] { "(?=a)b", "ab", false };
        yield return new object?[] { "(?!b)a", "ba", true };
        yield return new object?[] { "(?!b)a", "b", false };
        yield return new object?[] { "(?!a)a", "a", false };
        yield return new object?[] { "(?!)a", "a", false };
        yield return new object?[] { "(?<=a)b", "ab", true };
        yield return new object?[] { "(?<=a)b", "cb", false };
        yield return new object?[] { "(?<!a)b", "cb", true };
        yield return new object?[] { "(?<!a)b", "ab", false };
        yield return new object?[] { "((?=a)a)\\1", "aaa", true };
        yield return new object?[] { "a(?<=a)b", "ab", true };
        yield return new object?[] { "(?<=^a)b", "ab", true };
        yield return new object?[] { "(?<=a$)b", "ab", false };
        yield return new object?[] { "(?=(a)|ab)b", "ab", false };
        yield return new object?[] { "(?=a)b|ab", "ab", true };
        yield return new object?[] { "(?=a(?=b))", "ab", true };
        yield return new object?[] { "(?<=(?<=a)a)b", "aab", true };
        yield return new object?[] { "(?<=a)+b", "ab", true };
        yield return new object?[] { "((?=a)a)(b)", "ab", true };
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
        yield return new object?[] { "(?=a)a", "aaa", 3 };
        yield return new object?[] { "(?<=a)b", "aab ab", 2 };
        yield return new object?[] { "(?!b)a", "ba ba", 2 };
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

    [Theory]
    [InlineData("(?<=(a))b", "xxab", 3, 1, "a")]
    [InlineData("((?=a)a)\\1", "xaaayy", 1, 2, "a")]
    public void OwnedMatchProjectsCoordinates(
        string pattern, string inputText, int expectedIndex, int expectedLength, string expectedGroup)
    {
        var input = Encoding.UTF8.GetBytes(inputText);
        var oracle = new Regex(pattern, RegexOptions.CultureInvariant, Regex.InfiniteMatchTimeout).Match(inputText);
        var regex = new Utf8Regex(pattern, RegexOptions.CultureInvariant);
        var actual = regex.Match(input);
        Assert.Equal(oracle.Success, actual.Success);
        Assert.True(actual.Success);
        Assert.Equal(expectedIndex, actual.IndexInUtf16);
        Assert.Equal(expectedLength, actual.LengthInUtf16);
        var detailed = regex.MatchDetailed(input);
        Assert.True(detailed.Success);
        Assert.Equal(expectedGroup, detailed.GetGroup(1).GetValueString());
        foreach (var item in regex.EnumerateMatches(input))
        {
            Assert.Equal(expectedIndex, item.IndexInUtf16);
            Assert.Equal(expectedLength, item.LengthInUtf16);
        }

        Assert.False(regex.Inspection.DebugFallbackRegexIsCreated, "pattern=" + pattern);
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
        var regex = new Utf8Regex(pattern, RegexOptions.CultureInvariant);
        var detailed = regex.MatchDetailed(input);
        Assert.Equal(oracle.Success, detailed.Success);
        Assert.True(detailed.Success);
        for (var group = 1; group <= groups; group++)
        {
            Assert.Equal(oracle.Groups[group].Success, detailed.GetGroup(group).Success);
            if (oracle.Groups[group].Success)
            {
                Assert.Equal(oracle.Groups[group].Value, detailed.GetGroup(group).GetValueString());
            }
        }

        Assert.False(regex.Inspection.DebugFallbackRegexIsCreated, "pattern=" + pattern);
    }

    [Theory]
    [InlineData("(?<=a)b", "xab", 1, 3)]
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
    [InlineData("(?<=a)b", "aab ab", "X")]
    [InlineData("(?=a)a", "aaa", "X")]
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
    public void OwnedCatastrophicLookaheadIsMatchTimesOut()
    {
        var input = Encoding.UTF8.GetBytes(new string('a', 30));
        var regex = new Utf8Regex("(?=(a+)+b)a", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(1));
        Assert.True(regex.Inspection.DebugUsesOwnedSliceProgram);
        Assert.Throws<RegexMatchTimeoutException>(() => regex.IsMatch(input));
        Assert.False(regex.Inspection.DebugFallbackRegexIsCreated, "pattern=(?=(a+)+b)a");
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
        var regex = new Utf8Regex(pattern, RegexOptions.CultureInvariant);
        Assert.True(regex.Inspection.DebugUsesOwnedSliceProgram, "pattern=" + pattern);
        Assert.Equal(expected, regex.IsMatch(input));
        Assert.False(regex.Inspection.DebugFallbackRegexIsCreated, "pattern=" + pattern);
    }

    public static IEnumerable<object?[]> AstralDotBodyCases()
    {
        var astral = char.ConvertFromUtf32(0x1F600);
        yield return new object?[] { "(?<=.)b", astral + "b", 1 };
    }

    [Theory]
    [MemberData(nameof(AstralDotBodyCases))]
    public void AstralDotBodyDeclinesToFallback(string pattern, string inputText, int expected)
    {
        var input = Encoding.UTF8.GetBytes(inputText);
        var oracle = new Regex(pattern, RegexOptions.CultureInvariant, Regex.InfiniteMatchTimeout);
        Assert.Equal(expected, oracle.Matches(inputText).Count);
        var regex = new Utf8Regex(pattern, RegexOptions.CultureInvariant);
        Assert.True(regex.Inspection.DebugUsesOwnedSliceProgram, "pattern=" + pattern);
        Assert.False(regex.Inspection.DebugFallbackRegexIsCreated, "construction must stay lazy");
        Assert.Equal(expected, regex.Count(input));
        Assert.True(regex.Inspection.DebugFallbackRegexIsCreated, "pattern=" + pattern);
    }
}