using System.Text;
using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex.Tests;

// R03 backreference slice: single-digit backward references lower to owned
// ordinal Backref programs (unmatched groups fail, last iteration wins,
// byte comparison is encoding-indifferent) with BCL parity and zero
// fallback-engine construction. Multi-digit, forward, undefined, and octal
// escapes decline to legacy-exact routing.
public sealed class Utf8SliceBackrefTests
{
    public static IEnumerable<object?[]> SliceBackrefShapes()
    {
        yield return new object?[] { "(a)\\1" };
        yield return new object?[] { "(a|b)+\\1" };
        yield return new object?[] { "(a+)\\1" };
        yield return new object?[] { "(a)(b)\\2\\1" };
        yield return new object?[] { "((a)b)\\1" };
        yield return new object?[] { "(a*)\\1" };
        yield return new object?[] { "(x)?b\\1" };
        yield return new object?[] { "([a-z]+)\\1" };
        yield return new object?[] { "(a)(b)(c)(d)(e)(f)(g)(h)\\8" };
        yield return new object?[] { "(a)(b)(c)(d)(e)(f)(g)(h)(i)\\9" };
    }

    [Theory]
    [MemberData(nameof(SliceBackrefShapes))]
    public void BackrefShapesRouteOwned(string pattern)
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
    [InlineData("\\1(a)")]
    [InlineData("a\\12b")]
    [InlineData("(a)\\10")]
    [InlineData("(\\2a|(b))+")]
    public void NonBackrefEscapesDecline(string pattern)
    {
        var regex = new Utf8Regex(pattern, RegexOptions.CultureInvariant);
        Assert.False(regex.Inspection.DebugUsesOwnedSliceProgram, "pattern=" + pattern);
    }

    [Fact]
    public void UndefinedBackrefFailsConstruction()
    {
        Assert.Throws<RegexParseException>(
            () => new Utf8Regex("a\\1b", RegexOptions.CultureInvariant));
    }

    public static IEnumerable<object?[]> IsMatchCases()
    {
        yield return new object?[] { "(a)\\1", "aa", true };
        yield return new object?[] { "(a)\\1", "ab", false };
        yield return new object?[] { "(a|b)+\\1", "abb", true };
        yield return new object?[] { "(a|b)+\\1", "aba", false };
        yield return new object?[] { "(a*)\\1", "aaa", true };
        yield return new object?[] { "(x)?b\\1", "b", false };
        yield return new object?[] { "(x)?b\\1", "bxb", false };
        yield return new object?[] { "(a)(b)\\2\\1", "abba", true };
        yield return new object?[] { "((a)b)\\1", "abb", false };
        yield return new object?[] { "((a)b)\\1", "abab", true };
        yield return new object?[] { "([a-z]+)\\1", "abcabc", true };
        yield return new object?[] { "(a+)\\1", "aaabaaa", true };
        yield return new object?[] { "(a?)\\1b", "b", true };
        yield return new object?[] { "(\\p{L}+)\\1", "abab", true };
        yield return new object?[] { "(\\p{L}+)\\1", "ab ab", false };
        yield return new object?[] { "(\\p{IsBasicLatin}+)\\1", "HiHi", true };
        yield return new object?[] { "([^\\p{L}]+)\\1", "11", true };
        yield return new object?[] { "([\\p{L}a]+)\\1", "aBaB", true };
        yield return new object?[] { "(a)(b)(c)(d)(e)(f)(g)(h)\\8", "abcdefghh", true };
        yield return new object?[] { "(a)(b)(c)(d)(e)(f)(g)(h)\\8", "abcdefgha", false };
        yield return new object?[] { "(a)(b)(c)(d)(e)(f)(g)(h)(i)\\9", "abcdefghii", true };
        yield return new object?[] { "(a)(b)(c)(d)(e)(f)(g)(h)(i)\\9", "abcdefghia", false };
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
        yield return new object?[] { "(a)\\1", "aaabaaa", 2 };
        yield return new object?[] { "(a|b)+\\1", "abb abb", 2 };
        yield return new object?[] { "(x)?b\\1", "bbb", 0 };
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
    [InlineData("([a-z]+)\\1", "xx", "xx", "x")]
    [InlineData("(a)(b)\\2\\1", "xxabbayy", "abba", "a")]
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
    [InlineData("(a)\\1", "xaa", 1, 3)]
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
    [InlineData("(([a-z]+))\\2", "xxabcabcyy", "<$2>")]
    [InlineData("(a+)\\1", "xaaxaa", "<$1>")]
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

    [Theory]
    [InlineData("(a|b\\1)+", "aba", true)]
    [InlineData("(a|b\\1)+", "abba", true)]
    [InlineData("(a|b\\1)+", "b", false)]
    [InlineData("((a)|\\1b)+", "aab", true)]
    public void OwnedSelfReferenceIsMatchMatchesOracle(string pattern, string inputText, bool expected)
    {
        var input = Encoding.UTF8.GetBytes(inputText);
        var oracle = new Regex(pattern, RegexOptions.CultureInvariant, Regex.InfiniteMatchTimeout);
        Assert.Equal(expected, oracle.IsMatch(inputText));
        var regex = new Utf8Regex(pattern, RegexOptions.CultureInvariant);
        Assert.True(regex.Inspection.DebugUsesOwnedSliceProgram, "pattern=" + pattern);
        Assert.Equal(expected, regex.IsMatch(input));
        Assert.False(regex.Inspection.DebugFallbackRegexIsCreated, "pattern=" + pattern);
    }

    [Fact]
    public void OwnedCatastrophicBackrefIsMatchTimesOut()
    {
        var input = Encoding.UTF8.GetBytes(new string('a', 30));
        var regex = new Utf8Regex("(a+)+\\1b", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(1));
        Assert.True(regex.Inspection.DebugUsesOwnedSliceProgram);
        Assert.Throws<RegexMatchTimeoutException>(() => regex.IsMatch(input));
        Assert.False(regex.Inspection.DebugFallbackRegexIsCreated, "pattern=(a+)\\1b");
    }

    public static IEnumerable<object?[]> AstralIsMatchCases()
    {
        var astral = char.ConvertFromUtf32(0x1F600);
        yield return new object?[] { "(a)\\1", astral + "aa", true };
        yield return new object?[] { "(a)\\1", astral + "ab", false };
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

    [Fact]
    public void OwnedAstralPrefixMatchProjectsUtf16Coordinates()
    {
        var inputText = char.ConvertFromUtf32(0x1F600) + "aa";
        var input = Encoding.UTF8.GetBytes(inputText);
        var oracle = new Regex("(a)\\1", RegexOptions.CultureInvariant, Regex.InfiniteMatchTimeout).Match(inputText);
        var regex = new Utf8Regex("(a)\\1", RegexOptions.CultureInvariant);
        var actual = regex.Match(input);
        Assert.Equal(oracle.Success, actual.Success);
        Assert.True(actual.Success);
        Assert.Equal(2, actual.IndexInUtf16);
        Assert.Equal(2, actual.LengthInUtf16);
        Assert.False(regex.Inspection.DebugFallbackRegexIsCreated);
    }

    public static IEnumerable<object?[]> AstralCountCases()
    {
        var astral = char.ConvertFromUtf32(0x1F600);
        yield return new object?[] { "(a)\\1", "aa" + astral + "aa", 2 };
    }

    [Theory]
    [MemberData(nameof(AstralCountCases))]
    public void OwnedAstralInputCountMatchesOracle(string pattern, string inputText, int expected)
    {
        var input = Encoding.UTF8.GetBytes(inputText);
        var oracle = new Regex(pattern, RegexOptions.CultureInvariant, Regex.InfiniteMatchTimeout);
        Assert.Equal(expected, oracle.Matches(inputText).Count);
        var regex = new Utf8Regex(pattern, RegexOptions.CultureInvariant);
        Assert.Equal(expected, regex.Count(input));
        Assert.False(regex.Inspection.DebugFallbackRegexIsCreated, "pattern=" + pattern);
    }

    public static IEnumerable<object?[]> MultiDigitDigitEscapeCases()
    {
        yield return new object?[] { "(a)\\10", "a\x08", true };
        yield return new object?[] { "(a)\\10", "a0", false };
        yield return new object?[] { "(a)(b)\\12", "ab\n", true };
        yield return new object?[] { "(a)(b)(c)(d)(e)(f)(g)(h)(i)(j)\\10", "abcdefghijj", true };
        yield return new object?[] { "(a)(b)(c)(d)(e)(f)(g)(h)(i)(j)\\10", "abcdefghijk", false };
    }

    [Theory]
    [MemberData(nameof(MultiDigitDigitEscapeCases))]
    public void MultiDigitDigitEscapesDeclineWithLegacyAgreement(string pattern, string inputText, bool expected)
    {
        var input = Encoding.UTF8.GetBytes(inputText);
        var regex = new Utf8Regex(pattern, RegexOptions.CultureInvariant);
        Assert.False(regex.Inspection.DebugUsesOwnedSliceProgram, "pattern=" + pattern);
        var oracle = new Regex(pattern, RegexOptions.CultureInvariant, Regex.InfiniteMatchTimeout);
        Assert.Equal(expected, oracle.IsMatch(inputText));
        Assert.Equal(expected, regex.IsMatch(input));
    }

    public static IEnumerable<object?[]> InsufficientGroupDigitCases()
    {
        yield return new object?[] { "(a)\\8" };
        yield return new object?[] { "(a)\\9" };
    }

    [Theory]
    [MemberData(nameof(InsufficientGroupDigitCases))]
    public void InsufficientGroupDigitsFailConstruction(string pattern)
    {
        Assert.Throws<RegexParseException>(() => new Regex(pattern, RegexOptions.CultureInvariant));
        Assert.Throws<RegexParseException>(() => new Utf8Regex(pattern, RegexOptions.CultureInvariant));
    }
}