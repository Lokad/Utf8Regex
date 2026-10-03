using System.Text;
using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex.Tests;

// R03 boundary slice: `\b`/`\B` lower to owned zero-width Boundary programs
// with Unicode word semantics (BCL-exact, including zero-width joiners);
// `\A`, `\Z`, `\z` ride the anchor program. Every row runs oracle parity
// with zero fallback-engine construction. Astral scalars count as non-word
// (the oracle sees non-word halves), so no astral routing gate is needed;
// low-surrogate attempts reproduce the oracle halves positions.
public sealed class Utf8SliceBoundaryTests
{
    public static IEnumerable<object?[]> SliceBoundaryShapes()
    {
        yield return new object?[] { "\\b" };
        yield return new object?[] { "\\B" };
        yield return new object?[] { "x\\b!" };
        yield return new object?[] { "x\\Bx" };
        yield return new object?[] { "\\b\\w+\\b" };
        yield return new object?[] { "\\A.*\\z" };
        yield return new object?[] { "\\A.*\\Z" };
        yield return new object?[] { "(\\by\\b)" };
        yield return new object?[] { "(\\B.+\\B)" };
        yield return new object?[] { "\\b\\p{L}+\\b" };
    }

    [Theory]
    [MemberData(nameof(SliceBoundaryShapes))]
    public void BoundaryShapesRouteOwned(string pattern)
    {
        foreach (var options in new[]
        {
            RegexOptions.None,
            RegexOptions.CultureInvariant,
            RegexOptions.Compiled | RegexOptions.CultureInvariant,
            RegexOptions.Singleline | RegexOptions.CultureInvariant,
            RegexOptions.Multiline | RegexOptions.CultureInvariant,
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
        })
        {
            var regex = new Utf8Regex(pattern, options);
            Assert.True(regex.Inspection.DebugUsesOwnedSliceProgram, options.ToString());
        }
    }


    public static IEnumerable<object?[]> IsMatchCases()
    {
        yield return new object?[] { "x\\b!", "ax!by", true };
        yield return new object?[] { "x\\b!", "axby", false };
        yield return new object?[] { "x\\Bx", "axxb", true };
        yield return new object?[] { "x\\Bx", "ax bx", false };
        yield return new object?[] { "\\A.*\\z", "ab", true };
        yield return new object?[] { "\\A.*\\z", "xab\n", false };
        yield return new object?[] { "\\A.*\\Z", "ab", true };
        yield return new object?[] { "\\A.*\\Z", "ab\n", true };
        yield return new object?[] { "\\b", "a\u2060b", true };
        yield return new object?[] { "\\b", "a\u200Db", true };
        yield return new object?[] { "\\b\\p{L}+\\b", "\u00E9x", true };
        yield return new object?[] { "\\b\\w+\\b", "caf\u00E9", true };
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

    public static IEnumerable<object?[]> IgnoreCaseBoundaryCases()
    {
        // Word boundaries are case-transparent; the folded category inside
        // still matches through the cased union (Ll/Lu/Lt) or directly.
        yield return new object?[] { "\\b\\p{Ll}+\\b", "AB", true };
        yield return new object?[] { "(?i:\\b\\p{Ll}+\\b)", "AB", true };
        yield return new object?[] { "\\b\\p{L}+\\b", "\u00C9X", true };
    }

    [Theory]
    [MemberData(nameof(IgnoreCaseBoundaryCases))]
    public void OwnedIgnoreCaseBoundaryMatchesOracle(string pattern, string inputText, bool expected)
    {
        var options = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;
        var input = Encoding.UTF8.GetBytes(inputText);
        var oracle = new Regex(pattern, options, Regex.InfiniteMatchTimeout);
        Assert.Equal(expected, oracle.IsMatch(inputText));
        var regex = new Utf8Regex(pattern, options);
        Assert.True(regex.Inspection.DebugUsesOwnedSliceProgram, "pattern=" + pattern);
        Assert.Equal(expected, regex.IsMatch(input));
        Assert.False(regex.Inspection.DebugFallbackRegexIsCreated, "pattern=" + pattern);
    }

    public static IEnumerable<object?[]> CountCases()
    {
        yield return new object?[] { "\\b", "a word here", 6 };
        yield return new object?[] { "\\b", "a\U0001F600b", 4 };
        yield return new object?[] { "\\B", "a\U0001F600b", 1 };
        yield return new object?[] { "\\B", "\U0001F600", 3 };
        yield return new object?[] { "\\b", "\U0001F600", 0 };
        yield return new object?[] { "\\b", "é", 2 };
        yield return new object?[] { "\\b", "a\u200Db", 2 };
        yield return new object?[] { "\\B", "", 1 };
        yield return new object?[] { "\\b", "", 0 };
        yield return new object?[] { "x\\b!", "ax! axb", 1 };
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
    [InlineData("(\\B.+\\B)", "xaybzc", "aybz", "aybz")]
    public void OwnedMatchProjectsCoordinates(
        string pattern, string inputText, string expectedValue, string expectedGroup)
    {
        var input = Encoding.UTF8.GetBytes(inputText);
        var oracle = new Regex(pattern, RegexOptions.CultureInvariant, Regex.InfiniteMatchTimeout).Match(inputText);
        var regex = new Utf8Regex(pattern, RegexOptions.CultureInvariant);
        var actual = regex.Match(input);
        Assert.Equal(oracle.Success, actual.Success);
        Assert.True(actual.Success);
        Assert.Equal(expectedValue, inputText.Substring(actual.IndexInUtf16, actual.LengthInUtf16));
        var detailed = regex.MatchDetailed(input);
        Assert.True(detailed.Success);
        Assert.Equal(expectedGroup, detailed.GetGroup(1).GetValueString());
        foreach (var item in regex.EnumerateMatches(input))
        {
            Assert.Equal(expectedValue, inputText.Substring(item.IndexInUtf16, item.LengthInUtf16));
        }

        Assert.False(regex.Inspection.DebugFallbackRegexIsCreated, "pattern=" + pattern);
    }

    [Theory]
    [InlineData("\\b", "ab", 1, true)]
    [InlineData("\\b", "ab", 2, true)]
    [InlineData("x\\b!", "ax!by", 1, true)]
    public void OwnedFromOffsetMatchesOracle(
        string pattern, string inputText, int offset, bool expected)
    {
        var input = Encoding.UTF8.GetBytes(inputText);
        var oracle = new Regex(pattern, RegexOptions.CultureInvariant, Regex.InfiniteMatchTimeout);
        Assert.Equal(expected, oracle.Match(inputText, offset).Success);
        var regex = new Utf8Regex(pattern, RegexOptions.CultureInvariant);
        Assert.Equal(expected, regex.IsMatchFromUtf16Offset(input, offset));
        Assert.Equal(expected, regex.MatchFromUtf16Offset(input, offset).Success);
        Assert.False(regex.Inspection.DebugFallbackRegexIsCreated, "pattern=" + pattern);
    }

    [Theory]
    [InlineData("\\A.*\\z", "a\U0001F600b", true)]
    public void AstralDotAbsoluteAnchorServesOwned(
        string pattern, string inputText, bool expectedSuccess)
    {
        var input = Encoding.UTF8.GetBytes(inputText);
        var oracle = new Regex(pattern, RegexOptions.CultureInvariant, Regex.InfiniteMatchTimeout);
        Assert.Equal(expectedSuccess, oracle.IsMatch(inputText));
        var regex = new Utf8Regex(pattern, RegexOptions.CultureInvariant);
        Assert.True(regex.Inspection.DebugUsesOwnedSliceProgram, "pattern=" + pattern);
        Assert.False(regex.Inspection.DebugFallbackRegexIsCreated, "construction must stay lazy");
        Assert.Equal(expectedSuccess, regex.IsMatch(input));
        Assert.False(regex.Inspection.DebugFallbackRegexIsCreated, "pattern=" + pattern);
    }

    [Theory]
    [InlineData("(x\\b!)", "ax!by", "<$1>")]
    [InlineData("\\b", "ab cd", "Z")]
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
}
