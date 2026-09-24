using System.Text;
using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex.Tests;

// R03 IgnorePatternWhitespace on the slice: insignificant pattern whitespace
// (space/tab/LF/FF/CR, never vertical tab) and #-to-newline comments strip
// before parsing, while escapes, classes, and (?#) comments pass through
// verbatim. Unterminated comments decline to legacy-exact construction
// failures. Bare shapes keep native precedence.
public sealed class Utf8SliceVerboseTests
{
    public static IEnumerable<object?[]> SliceVerboseShapes()
    {
        yield return new object?[] { "a +b" };
        yield return new object?[] { "( a | b ) +c" };
        yield return new object?[] { "a # middle" + "\n" + "b" };
        yield return new object?[] { "[a-z] +" };
    }

    [Theory]
    [MemberData(nameof(SliceVerboseShapes))]
    public void VerboseShapesRouteOwned(string pattern)
    {
        foreach (var options in new[]
        {
            RegexOptions.IgnorePatternWhitespace | RegexOptions.CultureInvariant,
            RegexOptions.IgnorePatternWhitespace | RegexOptions.CultureInvariant | RegexOptions.Compiled,
        })
        {
            var regex = new Utf8Regex(pattern, options);
            Assert.True(regex.Inspection.DebugUsesOwnedSliceProgram, options.ToString());
        }
    }

    public static IEnumerable<object?[]> IsMatchCases()
    {
        yield return new object?[] { "a b", "ab", true };
        yield return new object?[] { "a b", "a b", false };
        yield return new object?[] { "a\tb", "ab", true };
        yield return new object?[] { "a\nb", "ab", true };
        yield return new object?[] { "a\fb", "ab", true };
        yield return new object?[] { "a\rb", "ab", true };
        yield return new object?[] { "a\vb", "ab", false };
        yield return new object?[] { "a\vb", "a\vb", true };
        yield return new object?[] { "a # comment", "a", true };
        yield return new object?[] { "a # comment", "b", false };
        yield return new object?[] { "a # trailing", "a", true };
        yield return new object?[] { "[a b]", " ", true };
        yield return new object?[] { "[a b]", "ab", true };
        yield return new object?[] { "[a#b]", "#", true };
        yield return new object?[] { "a\\ b", "a b", true };
        yield return new object?[] { "a\\#b", "a#b", true };
        yield return new object?[] { "( a | b ) +c", "abac", true };
        yield return new object?[] { "a #c1\nb #c2", "ab", true };
        yield return new object?[] { "a(?#x) b", "ab", true };
    }

    [Theory]
    [MemberData(nameof(IsMatchCases))]
    public void OwnedIsMatchMatchesOracle(string pattern, string inputText, bool expected)
    {
        var input = Encoding.UTF8.GetBytes(inputText);
        var oracle = new Regex(pattern, RegexOptions.IgnorePatternWhitespace | RegexOptions.CultureInvariant, Regex.InfiniteMatchTimeout);
        Assert.Equal(expected, oracle.IsMatch(inputText));
        foreach (var options in new[]
        {
            RegexOptions.IgnorePatternWhitespace | RegexOptions.CultureInvariant,
            RegexOptions.IgnorePatternWhitespace | RegexOptions.CultureInvariant | RegexOptions.Compiled,
        })
        {
            var regex = new Utf8Regex(pattern, options);
            Assert.Equal(expected, regex.IsMatch(input));
            if (regex.Inspection.DebugUsesOwnedSliceProgram)
            {
                Assert.False(regex.Inspection.DebugFallbackRegexIsCreated, "pattern=" + pattern);
            }
        }
    }

    [Theory]
    [InlineData("a # unterminated")]
    public void UnterminatedCommentMatchesOracle(string pattern)
    {
        var oracle = new Regex(pattern, RegexOptions.IgnorePatternWhitespace | RegexOptions.CultureInvariant, Regex.InfiniteMatchTimeout);
        var regex = new Utf8Regex(pattern, RegexOptions.IgnorePatternWhitespace | RegexOptions.CultureInvariant);
        Assert.Equal(oracle.IsMatch("a"), regex.IsMatch(Encoding.UTF8.GetBytes("a")));
    }

    [Theory]
    [InlineData("(?:a ")]
    public void UnterminatedShapesFailConstruction(string pattern)
    {
        Assert.Throws<RegexParseException>(
            () => new Utf8Regex(pattern, RegexOptions.IgnorePatternWhitespace | RegexOptions.CultureInvariant));
    }
}