using System.Text;
using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex.Tests;

// R03 mixed-class shorthands: classes combining shorthand escapes with
// other members lower to owned hybrid programs (BCL uniform rule: a
// shorthand is always a member, never a bound; a following dash stays
// literal; shorthand upper bounds throw). Bare shapes keep the native
// AsciiSimplePattern route; quantified and captured shapes route owned.
// The ASCII mask (with shorthand ASCII parts folded in) decides ASCII
// bytes; recorded members decide non-ASCII scalars with full Unicode
// categories (BMP units agree; astral follows the halves mirror).
// Negated hybrids and hybrids with negated members decline astral inputs
// at routing; PCRE2 declines every hybrid (native ASCII scope).
public sealed class Utf8SliceHybridTests
{
    public static IEnumerable<object?[]> SliceHybridShapes()
    {
        yield return new object?[] { "[\\da]+" };
        yield return new object?[] { "([\\da]+)" };
        yield return new object?[] { "[^\\da]+" };
        yield return new object?[] { "[\\d\\s]+" };
        yield return new object?[] { "[\\w-]+" };
        yield return new object?[] { "[\\d-]+" };
        yield return new object?[] { "[-\\d]+" };
        yield return new object?[] { "[a\\d]+" };
        yield return new object?[] { "[\\d-e]+" };
        yield return new object?[] { "[\\Da]+" };
        yield return new object?[] { "[^\\D\\s]+" };
    }

    [Theory]
    [MemberData(nameof(SliceHybridShapes))]
    public void HybridShapesRouteOwned(string pattern)
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
    [InlineData("[\\da]", "a1", 2)]
    public void BareHybridShapesKeepAsciiSimpleRoute(string pattern, string inputText, int expected)
    {
        // Simple shapes stay native; the native route already carries full
        // hybrid semantics including Unicode members.
        var input = Encoding.UTF8.GetBytes(inputText);
        var oracle = new Regex(pattern, RegexOptions.CultureInvariant, Regex.InfiniteMatchTimeout);
        Assert.Equal(expected, oracle.Matches(inputText).Count);
        var regex = new Utf8Regex(pattern, RegexOptions.CultureInvariant);
        Assert.Equal("AsciiSimplePattern", regex.Inspection.ExecutionKind.ToString());
        Assert.Equal(expected, regex.Count(input));
        Assert.False(regex.Inspection.DebugFallbackRegexIsCreated, "pattern=" + pattern);
    }



    public static IEnumerable<object?[]> IsMatchCases()
    {
        yield return new object?[] { "[\\da]+", "a1", true };
        yield return new object?[] { "[\\da]+", "xy", false };
        yield return new object?[] { "[\\da]+", "5", true };
        yield return new object?[] { "[\\da]+", "b", false };
        yield return new object?[] { "[a\\d]+", "b", false };
        yield return new object?[] { "[a\\d]+", "5", true };
        yield return new object?[] { "[\\Da]+", "5", false };
        yield return new object?[] { "[\\Da]+", "x", true };
        yield return new object?[] { "[^\\da]+", "5", false };
        yield return new object?[] { "[^\\da]+", "-", true };
        yield return new object?[] { "[\\w-]+", "-", true };
        yield return new object?[] { "[\\w-]+", " ", false };
        yield return new object?[] { "[^\\w\\s]+", "-", true };
        yield return new object?[] { "[^\\w\\s]+", "_", false };
        yield return new object?[] { "[^\\D\\s]+", "5", true };
        yield return new object?[] { "[^\\D\\s]+", " ", false };
        yield return new object?[] { "[\\d-e]+", "5", true };
        yield return new object?[] { "[\\d-e]+", "-", true };
        yield return new object?[] { "[\\d-e]+", "e", true };
        yield return new object?[] { "[\\d-e]+", "d", false };
        yield return new object?[] { "[\\d-x]+", "5", true };
        yield return new object?[] { "[\\d-x]+", "e", false };
        yield return new object?[] { "[\\d-x]+", "-", true };
        yield return new object?[] { "[-\\d]+", "-", true };
        yield return new object?[] { "[-\\d]+", "7", true };
        yield return new object?[] { "[\\s\\S]+", "x", true };
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

    public static IEnumerable<object?[]> BmpMemberCases()
    {
        var arabicDigit = char.ConvertFromUtf32(0x660);
        var eAcute = char.ConvertFromUtf32(0xE9);
        yield return new object?[] { "[\\da]+", arabicDigit, true };
        yield return new object?[] { "[\\da]+", eAcute, false };
        yield return new object?[] { "[^\\da]+", arabicDigit, false };
        yield return new object?[] { "[^\\da]+", eAcute, true };
        yield return new object?[] { "[\\w-]+", eAcute, true };
        yield return new object?[] { "[^\\w\\s]+", eAcute, false };
        yield return new object?[] { "[\\Da]+", eAcute, true };
        yield return new object?[] { "[^\\D\\s]+", arabicDigit, true };
    }

    [Theory]
    [MemberData(nameof(BmpMemberCases))]
    public void OwnedBmpMemberMatchesOracle(string pattern, string inputText, bool expected)
    {
        var input = Encoding.UTF8.GetBytes(inputText);
        var oracle = new Regex(pattern, RegexOptions.CultureInvariant, Regex.InfiniteMatchTimeout);
        Assert.Equal(expected, oracle.IsMatch(inputText));
        var regex = new Utf8Regex(pattern, RegexOptions.CultureInvariant);
        Assert.True(regex.Inspection.DebugUsesOwnedSliceProgram, "pattern=" + pattern);
        Assert.Equal(expected, regex.IsMatch(input));
        Assert.False(regex.Inspection.DebugFallbackRegexIsCreated, "pattern=" + pattern);
    }

    public static IEnumerable<object?[]> AstralOwnedCases()
    {
        var astralNd = char.ConvertFromUtf32(0x1D7CE);
        yield return new object?[] { "[\\da]+", astralNd, false };
        yield return new object?[] { "[\\da]+", "a" + astralNd, true };
    }

    [Theory]
    [MemberData(nameof(AstralOwnedCases))]
    public void OwnedAstralPositiveStaysOwned(string pattern, string inputText, bool expected)
    {
        // Positive hybrids with positive-only members miss astral scalars
        // exactly like the oracle halves, so astral inputs stay owned.
        var input = Encoding.UTF8.GetBytes(inputText);
        var oracle = new Regex(pattern, RegexOptions.CultureInvariant, Regex.InfiniteMatchTimeout);
        Assert.Equal(expected, oracle.IsMatch(inputText));
        var regex = new Utf8Regex(pattern, RegexOptions.CultureInvariant);
        Assert.True(regex.Inspection.DebugUsesOwnedSliceProgram, "pattern=" + pattern);
        Assert.Equal(expected, regex.IsMatch(input));
        Assert.False(regex.Inspection.DebugFallbackRegexIsCreated, "pattern=" + pattern);
    }

    public static IEnumerable<object?[]> CountCases()
    {
        yield return new object?[] { "[\\da]+", "a1b22", 2 };
        yield return new object?[] { "[^\\da]+", "a-b", 1 };
        yield return new object?[] { "[\\w-]+", "a-b c", 2 };
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
    [InlineData("([a\\d]+)", "xxa1byy", "a1", "a1")]
    [InlineData("([\\D\\s]+)", "ab cd", "ab cd", "ab cd")]
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
    [InlineData("[\\da]+", "xa1", 1, 3)]
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
    [InlineData("[\\da]+", "a1b", "X")]
    [InlineData("[\\w-]+", "a-b c", "X")]
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
    public void OwnedCatastrophicHybridIsMatchTimesOut()
    {
        var input = Encoding.UTF8.GetBytes(new string('1', 25));
        var regex = new Utf8Regex("([\\da]+)+b", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(1));
        Assert.True(regex.Inspection.DebugUsesOwnedSliceProgram);
        Assert.Throws<RegexMatchTimeoutException>(() => regex.IsMatch(input));
        Assert.False(regex.Inspection.DebugFallbackRegexIsCreated, "pattern=([\\da]+)+b");
    }
}