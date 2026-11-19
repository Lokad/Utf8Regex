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
            var regex = new Utf8Regex(pattern, options: options);
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
    public void UnsupportedShorthandShapesPreservePublicSemantics(string pattern)
{ var regex = new Utf8Regex(pattern, options: RegexOptions.CultureInvariant); var oracle = new Regex(pattern, RegexOptions.CultureInvariant); foreach(var text in new[] { "", "a", "1", "a1b", "word", ":", "é", "abc", "aabc", "aaab" }) { var bytes = System.Text.Encoding.UTF8.GetBytes(text); Assert.Equal(oracle.IsMatch(text), regex.IsMatch(bytes)); Assert.Equal(oracle.Count(text), regex.Count(bytes)); } }
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
            () => new Utf8Regex(pattern, options: RegexOptions.CultureInvariant));
    }

    [Theory]
    [InlineData("(\\d+)", "ab12cd", "12", "12")]
    [InlineData("(\\w+)", "a!", "a", "a")]
    public void OwnedMatchProjectsCoordinates(
        string pattern, string inputText, string expectedValue, string expectedGroup)
    {
        var input = Encoding.UTF8.GetBytes(inputText);
        var oracle = new Regex(pattern, RegexOptions.CultureInvariant, Regex.InfiniteMatchTimeout).Match(inputText);
        var regex = new Utf8Regex(pattern, options: RegexOptions.CultureInvariant);
        var actual = regex.Match(input);
        Assert.Equal(oracle.Success, actual.Success);
        Assert.True(actual.Success);
        Assert.Equal(expectedValue, inputText.Substring(actual.StartOffsetInBytes, (actual.EndOffsetInBytes - actual.StartOffsetInBytes)));
        var detailed = Utf8MatchSequence.First(regex.CreateMatchCursor(input));
        Assert.True(detailed.Success);
        Assert.Equal(expectedGroup, System.Text.Encoding.UTF8.GetString(detailed.GetGroupMemory(1).Span));
        foreach (var item in regex.EnumerateMatches(input))
        {
            Assert.Equal(expectedValue, inputText.Substring(item.StartOffsetInBytes, (item.EndOffsetInBytes - item.StartOffsetInBytes)));
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
        var regex = new Utf8Regex(pattern, options: RegexOptions.CultureInvariant);
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
        var regex = new Utf8Regex(pattern, options: RegexOptions.CultureInvariant);
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
        var regex = new Utf8Regex(pattern, options: RegexOptions.CultureInvariant);
        Assert.True(regex.Inspection.DebugUsesOwnedSliceProgram, "pattern=" + pattern);
        Assert.Equal(oracle, Encoding.UTF8.GetString(regex.Replace(input, Encoding.UTF8.GetBytes(replacement))));
        Assert.True(regex.Inspection.PreparedRegex.ScalarProgram is not null, "pattern=" + pattern);
    }
}
