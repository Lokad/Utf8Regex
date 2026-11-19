using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.FrontEnd;

namespace Lokad.Utf8Regex.Tests;

// R03 class slice: ASCII-only positive and negated classes (literals with
// `]`-first/`-`-edge rules, ascending ranges with escape bounds,
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
        yield return new object?[] { "[]-a]+" };
        yield return new object?[] { "[abc]+" };
        yield return new object?[] { "[a-c-e]+" };
        yield return new object?[] { "[A-z]+" };
        yield return new object?[] { "[\\b]+" };
        yield return new object?[] { "[\\n]+" };
        yield return new object?[] { "([a-z]+)" };
        yield return new object?[] { "[^a-z]+b" };
        yield return new object?[] { "[a-[b]]" };
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
            var regex = new Utf8Regex(pattern, options: options);
            Assert.True(regex.Inspection.DebugUsesOwnedSliceProgram, options.ToString());
        }
    }

    public static IEnumerable<object?[]> DeclinedClassShapes()
    {
        yield return new object?[] { "[\\w]" };
        yield return new object?[] { "[\\x41]" };
        // The native plan remains qualified after the VM admits this grammar.
        yield return new object?[] { "[[:alpha:]]" };
        yield return new object?[] { "[\\cA]" };
    }

    [Theory]
    [MemberData(nameof(DeclinedClassShapes))]
    public void UnsupportedClassShapesPreservePublicSemantics(string pattern)
{ var regex = new Utf8Regex(pattern, options: RegexOptions.CultureInvariant); var oracle = new Regex(pattern, RegexOptions.CultureInvariant); foreach(var text in new[] { "", "a", "1", "a1b", "word", ":", "é", "abc", "aabc", "aaab" }) { var bytes = System.Text.Encoding.UTF8.GetBytes(text); Assert.Equal(oracle.IsMatch(text), regex.IsMatch(bytes)); Assert.Equal(oracle.Count(text), regex.Count(bytes)); } }
    public static IEnumerable<object?[]> ReversedClassShapes()
    {
        yield return new object?[] { "[z-a]" };
        yield return new object?[] { "[a-\\-z]" };
    }

    [Theory]
    [MemberData(nameof(ReversedClassShapes))]
    public void ReversedClassShapesFailConstruction(string pattern)
    {
        Utf8PatternFailureAssert.Throws(
            () => new Utf8Regex(pattern, options: RegexOptions.CultureInvariant));
    }

    public static IEnumerable<object?[]> UnrecognizedClassEscapes()
    {
        // BCL rejects anchor escapes inside classes at construction; the owned parser
        // declines them, so the flip surfaces the same RegexParseException eagerly at
        // construction instead of the old fork serving them to a lazy first-use failure.
        yield return new object?[] { "[\\A]+" };
        yield return new object?[] { "[\\z]+" };
        yield return new object?[] { "[\\B]+" };
    }

    [Theory]
    [MemberData(nameof(UnrecognizedClassEscapes))]
    public void UnrecognizedClassEscapesFailLikeRuntime(string pattern)
    {
        Assert.Null(Utf8OwnedTreeParser.TryParse(pattern, RegexOptions.CultureInvariant));
        var expected = Assert.Throws<RegexParseException>(() => new Regex(pattern, RegexOptions.CultureInvariant));
        var actual = Utf8PatternFailureAssert.Throws(() => new Utf8Regex(pattern, options: RegexOptions.CultureInvariant));
        Assert.Equal("pattern", actual.ParamName);
    }

    [Theory]
    [InlineData("([a-z]+)", "XXabbYY", "abb", "abb")]
    [InlineData("([^a-z]+)", "xxAByy", "AB", "AB")]
    public void OwnedMatchProjectsCoordinates(
        string pattern, string inputText, string expectedValue, string expectedGroup)
    {
        var input = Encoding.UTF8.GetBytes(inputText);
        var oracle = new Regex(pattern, RegexOptions.CultureInvariant, Regex.InfiniteMatchTimeout).Match(inputText);
        var regex = new Utf8Regex(pattern, options: RegexOptions.CultureInvariant);
        var actual = regex.Match(input);
        Assert.Equal(oracle.Success, actual.Success);
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
    [InlineData("[a-z]+", "xxab", 2, 4)]
    [InlineData("[^a-z]+", "abUG", 2, 4)]
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
    [InlineData("[a-z]+", "xa\U0001F600by", 2)]
    [InlineData("[abc]+", "xax\U0001F600", 1)]
    public void AstralPositiveClassStaysOwned(
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
    [InlineData("[^a-z]+", "Aé!", 1)]
    [InlineData("[^abc]+", "abcDé", 1)]
    public void BmpNegatedClassStaysOwned(
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
    [InlineData("([a-z]+)", "xxabbyy", "<$1>")]
    [InlineData("[^a-z]+", "a1B2c", "<$&>")]
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
