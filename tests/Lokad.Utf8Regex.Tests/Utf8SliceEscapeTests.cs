using System.Text;
using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex.Tests;

// R03 numeric escapes: \xHH (exactly two hex), \uHHHH (exactly four hex),
// and octal (\0-led in literals, bare in classes, up to three digits) with
// ASCII results lower to literals, members, and range bounds. Malformed
// spellings decline to legacy-exact construction throws, and non-ASCII
// results decline with legacy-exact behavior. Bare shapes keep native
// precedence; quantified shapes route owned.
public sealed class Utf8SliceEscapeTests
{
    public static IEnumerable<object?[]> SliceEscapeShapes()
    {
        yield return new object?[] { "\\x41+" };
        yield return new object?[] { "\\u0041+" };
        yield return new object?[] { "\\01+" };
        yield return new object?[] { "[\\x41-\\x5A]+" };
        yield return new object?[] { "[\\101-\\132]+" };
        yield return new object?[] { "[\\u0041]+" };
    }

    [Theory]
    [MemberData(nameof(SliceEscapeShapes))]
    public void EscapeShapesRouteOwned(string pattern)
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

    public static IEnumerable<object?[]> IsMatchCases()
    {
        yield return new object?[] { "\\x41", "A", true };
        yield return new object?[] { "\\x41", "B", false };
        yield return new object?[] { "\\x41+", "AAB", true };
        yield return new object?[] { "\\u0041", "A", true };
        yield return new object?[] { "\\u004A", "J", true };
        yield return new object?[] { "\\0", "\u0000", true };
        yield return new object?[] { "\\00", "\u0000", true };
        yield return new object?[] { "\\000", "\u0000", true };
        yield return new object?[] { "\\07", "\u0007", true };
        yield return new object?[] { "\\12", "\n", true };
        yield return new object?[] { "\\12B", "\nB", true };
        yield return new object?[] { "\\08", "\u0000" + "8", true };
        yield return new object?[] { "a\\0", "a\u0000", true };
        yield return new object?[] { "[\\x41]", "A", true };
        yield return new object?[] { "[\\x41]", "B", false };
        yield return new object?[] { "[\\x41-\\x5A]", "M", true };
        yield return new object?[] { "[\\x41-\\x5A]", "a", false };
        yield return new object?[] { "[\\101-\\132]", "S", true };
        yield return new object?[] { "[\\u0041]", "A", true };
        yield return new object?[] { "[\\0]", "\u0000", true };
        yield return new object?[] { "[\\7]", "\u0007", true };
        yield return new object?[] { "[\\12]", "\n", true };
        yield return new object?[] { "[\\77]", "?", true };
        yield return new object?[] { "(\\x41)(\\x42)", "AB", true };
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
            Assert.Equal(expected, regex.IsMatch(input));
            using var source = new CancellationTokenSource();
            Assert.Equal(expected, regex.IsMatch(input, source.Token));
            if (regex.Inspection.DebugUsesOwnedSliceProgram)
            {
                Assert.False(regex.Inspection.DebugFallbackRegexIsCreated, "pattern=" + pattern);
            }
        }
    }

    public static IEnumerable<object?[]> ThrowCases()
    {
        yield return new object?[] { "\\x4" };
        yield return new object?[] { "\\xG1" };
        yield return new object?[] { "\\x" };
        yield return new object?[] { "\\u41" };
        yield return new object?[] { "\\u004" };
        yield return new object?[] { "\\8" };
        yield return new object?[] { "\\9" };
        yield return new object?[] { "[\\x4]" };
        yield return new object?[] { "[\\u41]" };
        yield return new object?[] { "[\\8]" };
    }

    [Theory]
    [MemberData(nameof(ThrowCases))]
    public void MalformedEscapesFailConstruction(string pattern)
    {
        Assert.Throws<RegexParseException>(
            () => new Utf8Regex(pattern, RegexOptions.CultureInvariant));
    }

    public static IEnumerable<object?[]> NonAsciiCases()
    {
        yield return new object?[] { "\\xFF", "ÿ" };
        yield return new object?[] { "\\u00E9", "é" };
        yield return new object?[] { "\\400", "Ā" };
        yield return new object?[] { "[\\xFF]", "ÿ" };
        yield return new object?[] { "[\\400]", "Ā" };
    }

    [Theory]
    [MemberData(nameof(NonAsciiCases))]
    public void NonAsciiResultsDeclineToLegacy(string pattern, string inputText)
    {
        var input = Encoding.UTF8.GetBytes(inputText);
        var oracle = new Regex(pattern, RegexOptions.CultureInvariant, Regex.InfiniteMatchTimeout);
        var regex = new Utf8Regex(pattern, RegexOptions.CultureInvariant);
        Assert.False(regex.Inspection.DebugUsesOwnedSliceProgram, "pattern=" + pattern);
        Assert.Equal(oracle.IsMatch(inputText), regex.IsMatch(input));
    }

    [Theory]
    [InlineData("\\x41+", "aAb", 1)]
    [InlineData("[\\x41-\\x5A]+", "ABc", 1)]
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
    [InlineData("\\x41+", "aa", "aa")]
    public void OwnedIgnoreCaseFoldsEscapeLiterals(string pattern, string inputText, string expected)
    {
        var input = Encoding.UTF8.GetBytes(inputText);
        var oracle = new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, Regex.InfiniteMatchTimeout);
        Assert.Equal(expected, oracle.Match(inputText).Value);
        var regex = new Utf8Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        Assert.True(regex.Inspection.DebugUsesOwnedSliceProgram, "pattern=" + pattern);
        Assert.Equal(expected, regex.MatchDetailed(input).GetGroup(0).GetValueString());
        Assert.False(regex.Inspection.DebugFallbackRegexIsCreated, "pattern=" + pattern);
    }
}