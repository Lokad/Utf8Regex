using System.Text;
using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex.Tests;

// Unicode shorthands classify complete scalars, including supplementary letters
// and digits. BMP cases retain the overlapping BCL comparator and route checks.
public sealed class Utf8SliceShorthandTests
{
    public static IEnumerable<object?[]> SliceShorthandShapes()
    {
        yield return new object?[] { "\\d+" };
        yield return new object?[] { "\\D+" };
        yield return new object?[] { "\\w+" };
        yield return new object?[] { "\\W+" };
        yield return new object?[] { "\\s+" };
        yield return new object?[] { "\\S+" };
        yield return new object?[] { "(\\d+)" };
        yield return new object?[] { "\\d+:\\w+" };
        yield return new object?[] { "[\\d]+" };
        yield return new object?[] { "[^\\d]+" };
        yield return new object?[] { "\\d+\\s*\\w+" };
        yield return new object?[] { "[a-[\\b]]" };
    }

    [Theory]
    [MemberData(nameof(SliceShorthandShapes))]
    public void ShorthandShapesRouteOwned(string pattern)
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

    public static IEnumerable<object?[]> DeclinedShorthandShapes()
    {
        yield return new object?[] { "a\\db" };
        yield return new object?[] { "\\w" };
        yield return new object?[] { "\\x41" };
        yield return new object?[] { "[[:alpha:]]" };
        yield return new object?[] { "\\cA" };
    }

    [Theory]
    [MemberData(nameof(DeclinedShorthandShapes))]
    public void UnsupportedShorthandShapesDecline(string pattern)
    {
        var regex = new Utf8Regex(pattern, RegexOptions.CultureInvariant);
        Assert.False(regex.Inspection.DebugUsesOwnedSliceProgram, "pattern=" + pattern);
    }

    public static IEnumerable<object?[]> ReversedShorthandShapes()
    {
        yield return new object?[] { "[z-a]" };
        yield return new object?[] { "[a-\\-z]" };
    }

    [Theory]
    [MemberData(nameof(ReversedShorthandShapes))]
    public void ReversedShorthandShapesFailConstruction(string pattern)
    {
        Utf8PatternFailureAssert.Throws(
            () => new Utf8Regex(pattern, RegexOptions.CultureInvariant));
    }

    public static IEnumerable<object?[]> IsMatchCases()
    {
        yield return new object?[] { "\\d+", "a1b", true };
        yield return new object?[] { "\\d+", "aab", false };
        yield return new object?[] { "\\d+", "a\u0661b", true };
        yield return new object?[] { "\\D+", "a b", true };
        yield return new object?[] { "\\w+", "a_b1", true };
        yield return new object?[] { "\\w+", "a\u0301b", true };
        yield return new object?[] { "\\w+", "a\u203Fb", true };
        yield return new object?[] { "\\w+", "a\u200Db", true };
        yield return new object?[] { "\\W+", "a b", true };
        yield return new object?[] { "\\W+", "ab", false };
        yield return new object?[] { "\\s+", "a\t b", true };
        yield return new object?[] { "\\s+", "a\u0085b", true };
        yield return new object?[] { "\\s+", "a\u00A0b", true };
        yield return new object?[] { "\\s+", "a\u2000b", true };
        yield return new object?[] { "\\s+", "a\u001Cb", false };
        yield return new object?[] { "\\s+", "a\u202Fb", true };
        yield return new object?[] { "\\s+", "a\uFEFFb", false };
        yield return new object?[] { "\\S+", "a b", true };
        yield return new object?[] { "[\\d]+", "a1b", true };
        yield return new object?[] { "[^\\d]+", "a1b", true };
        yield return new object?[] { "[^\\d]+", "123", false };
        yield return new object?[] { "\\d+:\\w+", "12:ab", true };
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
            Assert.True(regex.Inspection.PreparedRegex.ScalarProgram is not null, "pattern=" + pattern);
        }
    }

    public static IEnumerable<object?[]> CountCases()
    {
        yield return new object?[] { "\\d+", "a12b34", 2 };
        yield return new object?[] { "\\d+", "a\u0661\u0662b", 1 };
        yield return new object?[] { "\\D+", "a1b2", 2 };
        yield return new object?[] { "\\w+", "a b_c1", 2 };
        yield return new object?[] { "\\s+", "a\t b\nc", 2 };
        yield return new object?[] { "\\S+", "a b", 2 };
        yield return new object?[] { "(\\d+)", "a12b", 1 };
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
        Assert.True(regex.Inspection.PreparedRegex.ScalarProgram is not null, "pattern=" + pattern);
    }

    [Theory]
    [InlineData("(\\d+)", "ab12cd", "12", "12")]
    [InlineData("(\\w+)", "a!", "a", "a")]
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

        Assert.True(regex.Inspection.PreparedRegex.ScalarProgram is not null, "pattern=" + pattern);
    }

    [Theory]
    [InlineData("\\d+", "ab12", 2, 4)]
    [InlineData("\\D+", "12ab", 2, 4)]
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

        Assert.True(regex.Inspection.PreparedRegex.ScalarProgram is not null, "pattern=" + pattern);
    }

    public static IEnumerable<object?[]> AstralPositiveCases()
    {
        yield return new object?[] { "\\d+", "\U000104A0", 1 };
        yield return new object?[] { "\\w+", "\U00010400", 1 };
        yield return new object?[] { "\\s+", "\U00010400", 0 };
        yield return new object?[] { "\\d+", "1\U000104A02", 1 };
    }

    [Theory]
    [MemberData(nameof(AstralPositiveCases))]
    public void AstralPositiveShorthandStaysOwned(
        string pattern, string inputText, int expectedCount)
    {
        var input = Encoding.UTF8.GetBytes(inputText);
        var regex = new Utf8Regex(pattern, RegexOptions.CultureInvariant);
        Assert.True(regex.Inspection.DebugUsesOwnedSliceProgram, "pattern=" + pattern);
        Assert.Equal(expectedCount, regex.Count(input));
        Utf8AsciiScalarOperationAssertions.Operations(pattern, RegexOptions.CultureInvariant, inputText, requireSlice: true);
        Assert.True(regex.Inspection.PreparedRegex.ScalarProgram is not null, "pattern=" + pattern);
    }

    [Theory]
    [InlineData("[^\\W]+", "Aé!", 1)]
    [InlineData("[^\\S]+", "a b\u007Cc", 1)]
    public void BmpNegatedShorthandStaysOwned(
        string pattern, string inputText, int expectedCount)
    {
        var input = Encoding.UTF8.GetBytes(inputText);
        var oracle = new Regex(pattern, RegexOptions.CultureInvariant, Regex.InfiniteMatchTimeout);
        Assert.Equal(expectedCount, oracle.Matches(inputText).Count);
        var regex = new Utf8Regex(pattern, RegexOptions.CultureInvariant);
        Assert.True(regex.Inspection.DebugUsesOwnedSliceProgram, "pattern=" + pattern);
        Assert.Equal(expectedCount, regex.Count(input));
        Assert.True(regex.Inspection.PreparedRegex.ScalarProgram is not null, "pattern=" + pattern);
    }

    [Theory]
    [InlineData("(\\d+)", "ab12cd", "<$1>")]
    [InlineData("\\s+", "a\tb", "<$&>")]
    public void OwnedReplaceMatchesOracle(
        string pattern, string inputText, string replacement)
    {
        var input = Encoding.UTF8.GetBytes(inputText);
        var oracle = new Regex(pattern, RegexOptions.CultureInvariant, Regex.InfiniteMatchTimeout).Replace(inputText, replacement);
        var regex = new Utf8Regex(pattern, RegexOptions.CultureInvariant);
        Assert.True(regex.Inspection.DebugUsesOwnedSliceProgram, "pattern=" + pattern);
        Assert.Equal(oracle, Encoding.UTF8.GetString(regex.Replace(input, replacement)));
        Assert.True(regex.Inspection.PreparedRegex.ScalarProgram is not null, "pattern=" + pattern);
    }
}
