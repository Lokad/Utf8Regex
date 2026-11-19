using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Diagnostics;

namespace Lokad.Utf8Regex.Tests;

// Boundary programs test Unicode word membership at complete scalar boundaries.
// Emoji is non-word; its four bytes have no interior matching position. Joiners
// retain their boundary word membership. BMP cases overlap the BCL comparator.
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
            var regex = new Utf8Regex(pattern, options: options);
            Assert.True(regex.Inspection.DebugUsesOwnedSliceProgram, options.ToString());
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
        var regex = new Utf8Regex(pattern, options: options);
        Assert.True(regex.Inspection.DebugUsesOwnedSliceProgram, "pattern=" + pattern);
        Assert.Equal(expected, regex.IsMatch(input));
        Assert.True(regex.Inspection.PreparedRegex.ScalarProgram is not null, "pattern=" + pattern);
    }

    public static IEnumerable<object?[]> CountCases()
    {
        yield return new object?[] { "\\b", "a word here", 6 };
        yield return new object?[] { "\\b", "a\U0001F600b", 4 };
        yield return new object?[] { "\\B", "a\U0001F600b", 0 };
        yield return new object?[] { "\\B", "\U0001F600", 2 };
        yield return new object?[] { "\\b", "\U0001F600", 0 };
        yield return new object?[] { "\\b", "é", 2 };
        yield return new object?[] { "\\b", "a\u200Db", 2 };
        yield return new object?[] { "\\B", "", 1 };
        yield return new object?[] { "\\b", "", 0 };
        yield return new object?[] { "x\\b!", "ax! axb", 1 };
    }

    [Theory]
    [MemberData(nameof(CountCases))]
    public void OwnedCountMatchesScalarContract(string pattern, string inputText, int expected)
    {
        var input = Encoding.UTF8.GetBytes(inputText);
        var regex = new Utf8Regex(pattern, options: RegexOptions.CultureInvariant);
        Assert.True(regex.Inspection.DebugUsesOwnedSliceProgram, "pattern=" + pattern);
        Assert.Equal(expected, regex.Count(input));
        Utf8AsciiScalarOperationAssertions.Operations(pattern, RegexOptions.CultureInvariant, inputText, requireSlice: true);
        Assert.True(regex.Inspection.PreparedRegex.ScalarProgram is not null, "pattern=" + pattern);
    }

    [Theory]
    [InlineData(@"\b\w{10,}\b", RegexOptions.None)]
    [InlineData(@"\b\w{10,}\b", RegexOptions.ECMAScript)]
    [InlineData(@"\b\w{10,12}?\b", RegexOptions.None)]
    [InlineData(@"\b[!,]", RegexOptions.None)]
    [InlineData(@"\B\w{4}(?=\w)", RegexOptions.None)]
    public void LeadingBoundaryCountKeepsBothAsciiAndUnicodeNeighbors(string pattern, RegexOptions options)
    {
        const string part = "abc, abcdefghij! é! a\u200Dabcdefghij! a\u0301bcdefghijk! İabcdefghij! ";
        var text = string.Concat(Enumerable.Repeat(part, 8));
        var expected = new Regex(pattern, options).Count(text);
        Assert.True(expected > 0);
        using var token = new CancellationTokenSource();
        using var regex = Utf8Regex.Compile(pattern, options, new()
        {
            ResourcePolicy = new() { MaxWorkSteps = 20_000, MaxWorkingBytes = 4096 }
        });
        Assert.Equal(expected, regex.Count(Encoding.UTF8.GetBytes(text), token.Token));
    }

    [Theory]
    [InlineData(" ", 256, 320UL)]
    [InlineData("abc ", 64, 100UL)]
    public void RejectedAsciiStartsUseBoundedWorkAndObserveOriginalCancellation(string segment, int copies, ulong maxWork)
    {
        var input = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat(segment, copies)));
        using var token = new CancellationTokenSource();
        using var regex = Utf8Regex.Compile(@"\b\w{10,}\b", policy: new()
        {
            ResourcePolicy = new() { MaxWorkSteps = maxWork, MaxWorkingBytes = 1 }
        });
        Assert.Equal(0, regex.Count(input, token.Token));
        var diagnostics = Utf8SearchDiagnosticsSession.Start(steps => { if (steps >= 64) token.Cancel(); });
        try
        {
            var error = Assert.Throws<OperationCanceledException>(() => regex.Count(input, token.Token));
            Assert.Equal(token.Token, error.CancellationToken);
        }
        finally { diagnostics.Complete(); }
        Assert.Equal(0, regex.Count(input));
    }

    [Theory]
    [InlineData("(\\B.+\\B)", "xaybzc", "aybz", "aybz")]
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
    [InlineData("\\b", "ab", 1, true)]
    [InlineData("\\b", "ab", 2, true)]
    [InlineData("x\\b!", "ax!by", 1, true)]
    public void OwnedFromOffsetMatchesOracle(
        string pattern, string inputText, int offset, bool expected)
    {
        var input = Encoding.UTF8.GetBytes(inputText);
        var oracle = new Regex(pattern, RegexOptions.CultureInvariant, Regex.InfiniteMatchTimeout);
        Assert.Equal(expected, oracle.Match(inputText, offset).Success);
        var regex = new Utf8Regex(pattern, options: RegexOptions.CultureInvariant);
        Assert.Equal(expected, regex.Match(input, Encoding.UTF8.GetByteCount(inputText.AsSpan(0, offset)), []).Success);
        Assert.Equal(expected, regex.Match(input, Encoding.UTF8.GetByteCount(inputText.AsSpan(0, offset)), []).Success);
        Assert.True(regex.Inspection.PreparedRegex.ScalarProgram is not null, "pattern=" + pattern);
    }

    [Theory]
    [InlineData("\\A.*\\z", "a\U0001F600b", true)]
    public void AstralDotAbsoluteAnchorServesOwned(
        string pattern, string inputText, bool expectedSuccess)
    {
        var input = Encoding.UTF8.GetBytes(inputText);
        var oracle = new Regex(pattern, RegexOptions.CultureInvariant, Regex.InfiniteMatchTimeout);
        Assert.Equal(expectedSuccess, oracle.IsMatch(inputText));
        var regex = new Utf8Regex(pattern, options: RegexOptions.CultureInvariant);
        Assert.True(regex.Inspection.DebugUsesOwnedSliceProgram, "pattern=" + pattern);
        Assert.True(regex.Inspection.PreparedRegex.ScalarProgram is not null, "construction must stay lazy");
        Assert.Equal(expectedSuccess, regex.IsMatch(input));
        Assert.True(regex.Inspection.PreparedRegex.ScalarProgram is not null, "pattern=" + pattern);
    }

    [Theory]
    [InlineData("(x\\b!)", "ax!by", "<$1>")]
    [InlineData("\\b", "ab cd", "Z")]
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
