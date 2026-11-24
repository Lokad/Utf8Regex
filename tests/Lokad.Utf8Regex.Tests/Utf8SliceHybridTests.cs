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
// categories across all scalar planes. Negation complements the scalar union;
// PCRE2 applies its own profile admission to the shared program.
public sealed class Utf8SliceHybridTests
{
    [Theory]
    [InlineData("[\\da]", "a1", 2)]
    public void BareHybridShapesKeepAsciiSimpleRoute(string pattern, string inputText, int expected)
    {
        // Simple shapes stay native; the native route already carries full
        // hybrid semantics including Unicode members.
        var input = Encoding.UTF8.GetBytes(inputText);
        var oracle = new Regex(pattern, RegexOptions.CultureInvariant, Regex.InfiniteMatchTimeout);
        Assert.Equal(expected, oracle.Matches(inputText).Count);
        var regex = new Utf8Regex(pattern, options: RegexOptions.CultureInvariant);
        Assert.Equal(expected, regex.Count(input));
        Assert.True(regex.Inspection.PreparedRegex.ScalarProgram is not null, "pattern=" + pattern);
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
        var regex = new Utf8Regex(pattern, options: RegexOptions.CultureInvariant);
        Assert.True(regex.Inspection.DebugUsesOwnedSliceProgram, "pattern=" + pattern);
        Assert.Equal(expected, regex.IsMatch(input));
        Assert.True(regex.Inspection.PreparedRegex.ScalarProgram is not null, "pattern=" + pattern);
    }

    public static IEnumerable<object?[]> AstralOwnedCases()
    {
        var astralNd = char.ConvertFromUtf32(0x1D7CE);
        yield return new object?[] { "[\\da]+", astralNd, true };
        yield return new object?[] { "[\\da]+", "a" + astralNd, true };
    }

    [Theory]
    [MemberData(nameof(AstralOwnedCases))]
    public void OwnedAstralPositiveStaysOwned(string pattern, string inputText, bool expected)
    {
        // The digit is one complete Nd scalar; its surrogate halves have no
        // role in shorthand membership.
        var input = Encoding.UTF8.GetBytes(inputText);
        var regex = new Utf8Regex(pattern, options: RegexOptions.CultureInvariant);
        Assert.True(regex.Inspection.DebugUsesOwnedSliceProgram, "pattern=" + pattern);
        Assert.Equal(expected, regex.IsMatch(input));
        Assert.True(regex.Inspection.PreparedRegex.ScalarProgram is not null, "pattern=" + pattern);
    }

    [Theory]
    [InlineData("([a\\d]+)", "xxa1byy", "a1", "a1")]
    [InlineData("([\\D\\s]+)", "ab cd", "ab cd", "ab cd")]
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
    [InlineData("[\\da]+", "xa1", 1, 3)]
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
    [InlineData("[\\da]+", "a1b", "X")]
    [InlineData("[\\w-]+", "a-b c", "X")]
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

    [Theory]
    [InlineData("~a3", RegexOptions.None, 1)]
    [InlineData("~\0!", RegexOptions.None, 1)]
    [InlineData("~😀٤", RegexOptions.None, 4)]
    [InlineData("~K3", RegexOptions.None, 0)]
    [InlineData("\u00a0K!", RegexOptions.None, 3)]
    [InlineData("\u00a0K!", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, 0)]
    public void StandaloneMixedClassesPreserveCaptureWidthsInBothDirections(string text, RegexOptions options, int width)
    {
        const string pattern = @"\A[\s~](?<value>[^\sK])[\d!]\z";
        var input = Encoding.UTF8.GetBytes(text);
        var start = Encoding.UTF8.GetByteCount(text.AsSpan(0, 1));
        Span<Utf8CaptureRange> captures = stackalloc Utf8CaptureRange[2];
        foreach (var direction in new[] { RegexOptions.None, RegexOptions.RightToLeft })
        {
            using var regex = new Utf8Regex(pattern, options | direction);
            var match = regex.Match(input, captures: captures);
            Assert.Equal(width != 0, match.Success);
            Assert.Equal(match.Success, captures[1].Success);
            if (match.Success)
                Assert.Equal((start, start + width), (captures[1].StartOffsetInBytes, captures[1].EndOffsetInBytes));
        }
    }

    [Fact]
    public void OwnedCatastrophicHybridIsMatchTimesOut()
    {
        var input = Encoding.UTF8.GetBytes("b" + new string('1', 25));
        var regex = new Utf8Regex("([\\da]+)+b", options: RegexOptions.CultureInvariant, policy: new Utf8RegexPolicy
        {
            MatchTimeout = TimeSpan.FromMilliseconds(1)
        });
        Assert.True(regex.Inspection.DebugUsesOwnedSliceProgram);
        Assert.Throws<RegexMatchTimeoutException>(() => regex.IsMatch(input));
        Assert.True(regex.Inspection.PreparedRegex.ScalarProgram is not null, "pattern=([\\da]+)+b");
    }
}
