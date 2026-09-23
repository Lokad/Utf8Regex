using System.Text;
using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex.Tests;

// R03 class slice: ASCII-only positive and negated classes (literals with
// `]`-first/`-`-edge/POSIX-guard rules, ascending ranges with escape bounds,
// and supported single-char escapes) lower to owned Set programs with BCL
// oracle parity and zero fallback-engine construction. Negated classes
// consume whole non-ASCII scalars, so astral inputs decline to fallback
// (unit-exact halves); positive classes and BMP inputs stay owned.
public sealed class Utf8SliceClassTests
{
    public static IEnumerable<object?[]> SliceClassShapes()
    {
        yield return new object?[] { "[a-z]+" };
        yield return new object?[] { "[^a-z]+" };
        yield return new object?[] { "[abc]+" };
        yield return new object?[] { "[a-c-e]+" };
        yield return new object?[] { "[A-z]+" };
        yield return new object?[] { "[\\b]+" };
        yield return new object?[] { "[\\n]+" };
        yield return new object?[] { "([a-z]+)" };
        yield return new object?[] { "[^a-z]+b" };
    }

    [Theory]
    [MemberData(nameof(SliceClassShapes))]
    public void ClassShapesRouteOwned(string pattern)
    {
        foreach (var options in new[]
        {
            RegexOptions.None,
            RegexOptions.CultureInvariant,
            RegexOptions.Compiled | RegexOptions.CultureInvariant,
            RegexOptions.Singleline | RegexOptions.CultureInvariant,
            RegexOptions.Multiline | RegexOptions.CultureInvariant,
        })
        {
            var regex = new Utf8Regex(pattern, options);
            Assert.True(regex.Inspection.DebugUsesOwnedSliceProgram, options.ToString());
        }
    }

    public static IEnumerable<object?[]> DeclinedClassShapes()
    {
        yield return new object?[] { "[\\w]" };
        yield return new object?[] { "[\\p{L}]" };
        yield return new object?[] { "[\\x41]" };
        yield return new object?[] { "[a-[b]]" };
        yield return new object?[] { "[[:alpha:]]" };
        yield return new object?[] { "[\\cA]" };
    }

    [Theory]
    [MemberData(nameof(DeclinedClassShapes))]
    public void UnsupportedClassShapesDecline(string pattern)
    {
        var regex = new Utf8Regex(pattern, RegexOptions.CultureInvariant);
        Assert.False(regex.Inspection.DebugUsesOwnedSliceProgram, "pattern=" + pattern);
    }

    public static IEnumerable<object?[]> ReversedClassShapes()
    {
        yield return new object?[] { "[z-a]" };
        yield return new object?[] { "[a-\\-z]" };
    }

    [Theory]
    [MemberData(nameof(ReversedClassShapes))]
    public void ReversedClassShapesFailConstruction(string pattern)
    {
        Assert.Throws<RegexParseException>(
            () => new Utf8Regex(pattern, RegexOptions.CultureInvariant));
    }

    public static IEnumerable<object?[]> IsMatchCases()
    {
        yield return new object?[] { "[a-z]+", "xxabyy", true };
        yield return new object?[] { "[a-z]+", "XXABYY", false };
        yield return new object?[] { "[^a-z]+", "xxAYyy", true };
        yield return new object?[] { "[abc]+", "xxcabbayy", true };
        yield return new object?[] { "[a-c-e]+", "xxdyy", false };
        yield return new object?[] { "[a-c-e]+", "xxeyy", true };
        yield return new object?[] { "[a-c-e]+", "xx-yy", true };
        yield return new object?[] { "[A-z]+", "xx[\\]^_`yy", true };
        yield return new object?[] { "[]a]+", "xx]yy", true };
        yield return new object?[] { "[-a]+", "xx-yy", true };
        yield return new object?[] { "[a-]+", "xx-yy", true };
        yield return new object?[] { "[a\\-z]+", "xx-yy", true };
        yield return new object?[] { "[a\\-z]+", "xxmyy", false };
        yield return new object?[] { "[[a]+", "xx[yy", true };
        yield return new object?[] { "[a[]+", "xx[yy", true };
        yield return new object?[] { "[+--.]+", "xx,yy", true };
        yield return new object?[] { "[+--.]+", "xx*yy", false };
        yield return new object?[] { "[--a]+", "xx/yy", true };
        yield return new object?[] { "[--0]+", "xx/yy", true };
        yield return new object?[] { "[--0]+", "xxayy", false };
        yield return new object?[] { "[a-b-c]+", "xx-yy", true };
        yield return new object?[] { "[\\n-\\r]+", "x\ny\rz", true };
        yield return new object?[] { "[\\b]+", "x\by", true };
        yield return new object?[] { "[\\0]+", "x\0y", true };
        yield return new object?[] { "[\\e]+", "x\ey", true };
        yield return new object?[] { "[\\--0]+", "xx/yy", false };
        yield return new object?[] { "[\\--0]+", "xx-yy", true };
        yield return new object?[] { "[^]a]+", "xxyy", true };
        yield return new object?[] { "[^]a]+", "]]aa", false };
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
        yield return new object?[] { "[a-z]+", "abUG12cd", 2 };
        yield return new object?[] { "[^a-z]+", "abUG12", 1 };
        yield return new object?[] { "[a-c-e]+", "abcdef-", 3 };
        yield return new object?[] { "[abc]+", "xxcabbayy", 1 };
        yield return new object?[] { "[\\n-\\r]+", "a\n\rb", 1 };
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
    [InlineData("([a-z]+)", "XXabbYY", "abb", "abb")]
    [InlineData("([^a-z]+)", "xxAByy", "AB", "AB")]
    public void OwnedMatchProjectsCoordinates(
        string pattern, string inputText, string expectedValue, string expectedGroup)
    {
        var input = Encoding.UTF8.GetBytes(inputText);
        var oracle = new Regex(pattern, RegexOptions.CultureInvariant, Regex.InfiniteMatchTimeout).Match(inputText);
        var regex = new Utf8Regex(pattern, RegexOptions.CultureInvariant);
        var actual = regex.Match(input);
        Assert.Equal(oracle.Success, actual.Success);
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
    [InlineData("[a-z]+", "xxab", 2, 4)]
    [InlineData("[^a-z]+", "abUG", 2, 4)]
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
    [InlineData("[a-z]+", "xa\U0001F600by", 2)]
    [InlineData("[abc]+", "xax\U0001F600", 1)]
    public void AstralPositiveClassStaysOwned(
        string pattern, string inputText, int expectedCount)
    {
        var input = Encoding.UTF8.GetBytes(inputText);
        var oracle = new Regex(pattern, RegexOptions.CultureInvariant, Regex.InfiniteMatchTimeout);
        Assert.Equal(expectedCount, oracle.Matches(inputText).Count);
        var regex = new Utf8Regex(pattern, RegexOptions.CultureInvariant);
        Assert.True(regex.Inspection.DebugUsesOwnedSliceProgram, "pattern=" + pattern);
        Assert.Equal(expectedCount, regex.Count(input));
        Assert.False(regex.Inspection.DebugFallbackRegexIsCreated, "pattern=" + pattern);
    }

    [Theory]
    [InlineData("[^a-z]+", "x\U0001F600y", 1)]
    [InlineData("[^a-z]+", "a\U0001F600b", 1)]
    public void AstralNegatedClassDeclinesToFallback(
        string pattern, string inputText, int expectedCount)
    {
        var input = Encoding.UTF8.GetBytes(inputText);
        var oracle = new Regex(pattern, RegexOptions.CultureInvariant, Regex.InfiniteMatchTimeout);
        Assert.Equal(expectedCount, oracle.Matches(inputText).Count);
        var regex = new Utf8Regex(pattern, RegexOptions.CultureInvariant);
        Assert.True(regex.Inspection.DebugUsesOwnedSliceProgram, "pattern=" + pattern);
        Assert.False(regex.Inspection.DebugFallbackRegexIsCreated, "construction must stay lazy");
        Assert.Equal(expectedCount, regex.Count(input));
        Assert.True(regex.Inspection.DebugFallbackRegexIsCreated, "pattern=" + pattern);
    }

    [Theory]
    [InlineData("[^a-z]+", "Aé!", 1)]
    [InlineData("[^abc]+", "abcDé", 1)]
    public void BmpNegatedClassStaysOwned(
        string pattern, string inputText, int expectedCount)
    {
        var input = Encoding.UTF8.GetBytes(inputText);
        var oracle = new Regex(pattern, RegexOptions.CultureInvariant, Regex.InfiniteMatchTimeout);
        Assert.Equal(expectedCount, oracle.Matches(inputText).Count);
        var regex = new Utf8Regex(pattern, RegexOptions.CultureInvariant);
        Assert.True(regex.Inspection.DebugUsesOwnedSliceProgram, "pattern=" + pattern);
        Assert.Equal(expectedCount, regex.Count(input));
        Assert.False(regex.Inspection.DebugFallbackRegexIsCreated, "pattern=" + pattern);
    }

    [Theory]
    [InlineData("([a-z]+)", "xxabbyy", "<$1>")]
    [InlineData("[^a-z]+", "a1B2c", "<$&>")]
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
