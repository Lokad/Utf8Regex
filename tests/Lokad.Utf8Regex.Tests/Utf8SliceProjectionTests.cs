using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace Lokad.Utf8Regex.Tests;

/// <summary>R02a projection: owned Match values, detailed groups, and lazy
/// enumeration match the BCL oracle on coordinates, values, empty advancement,
/// and astral input; mid-enumeration cancellation aborts through the retained
/// token and abandoned enumerations leave no shared corruption.</summary>
public sealed class Utf8SliceProjectionTests
{
    [Theory]
    [InlineData("(a+)(b+)", "xxaaabbyy", 2, 5)]
    [InlineData("(a+)(b+)", "xxaaayy", -1, 0)]
    [InlineData("(a+)(b+)", "éaaabbé", 1, 5)]
    [InlineData("(ab|cd)+", "xxabcdcdyy", 2, 6)]
    public void OwnedMatchProjectsCoordinates(string pattern, string inputText, int expectedIndex, int expectedLength)
    {
        var input = Encoding.UTF8.GetBytes(inputText);
        var oracle = new Regex(pattern, RegexOptions.CultureInvariant, Regex.InfiniteMatchTimeout).Match(inputText);
        var regex = new Utf8Regex(pattern, RegexOptions.CultureInvariant);
        Assert.True(regex.Inspection.DebugUsesOwnedSliceProgram);
        var actual = regex.Match(input);
        Assert.Equal(oracle.Success, actual.Success);
        if (!expectedIndex.Equals(-1))
        {
            Assert.True(actual.Success);
            Assert.Equal(System.Text.Encoding.UTF8.GetByteCount(inputText.AsSpan(0, expectedIndex)), actual.StartOffsetInBytes);
            Assert.Equal(System.Text.Encoding.UTF8.GetByteCount(inputText.AsSpan(expectedIndex, expectedLength)), (actual.EndOffsetInBytes - actual.StartOffsetInBytes));
            Assert.Equal(System.Text.Encoding.UTF8.GetByteCount(inputText.AsSpan(0, oracle.Index)), actual.StartOffsetInBytes);
            Assert.Equal(System.Text.Encoding.UTF8.GetByteCount(inputText.AsSpan(oracle.Index, oracle.Length)), (actual.EndOffsetInBytes - actual.StartOffsetInBytes));
            Assert.True(actual.Success);
            Assert.Equal(Encoding.UTF8.GetString(input[actual.StartOffsetInBytes..(actual.StartOffsetInBytes + (actual.EndOffsetInBytes - actual.StartOffsetInBytes))]), oracle.Value);
        }
        else
        {
            Assert.False(actual.Success);
        }
    }

    [Fact]
    public void OwnedDetailedProjectsGroups()
    {
        var regex = new Utf8Regex("(a+)(b+)", RegexOptions.CultureInvariant);
        var input = Encoding.UTF8.GetBytes("xxaaabbyy");
        var actual = Utf8MatchSequence.First(regex.CreateMatchCursor(input));
        var oracle = new Regex("(a+)(b+)", RegexOptions.CultureInvariant, Regex.InfiniteMatchTimeout).Match("xxaaabbyy");
        Assert.True(actual.Success);
        Assert.Equal(3, actual.GroupNumbers.Length);
        Utf8MatchSequence.AssertCaptures(oracle, actual, input, "xxaaabbyy");
        Assert.Equal(oracle.Groups[0].Value, System.Text.Encoding.UTF8.GetString(actual.GetGroupMemory(0).Span));
        Assert.Equal(oracle.Groups[1].Value, System.Text.Encoding.UTF8.GetString(actual.GetGroupMemory(1).Span));
        Assert.Equal(oracle.Groups[2].Value, System.Text.Encoding.UTF8.GetString(actual.GetGroupMemory(2).Span));
        Assert.True(regex.Inspection.PreparedRegex.ScalarProgram is not null);
    }

    [Fact]
    public void OwnedDetailedReportsUnmatchedGroups()
    {
        var regex = new Utf8Regex("(a)|(b)", RegexOptions.CultureInvariant);
        var actual = Utf8MatchSequence.First(regex.CreateMatchCursor(Encoding.UTF8.GetBytes("xb")));
        var oracle = new Regex("(a)|(b)", RegexOptions.CultureInvariant, Regex.InfiniteMatchTimeout).Match("xb");
        Assert.True(actual.Success);
        Assert.False((actual.TryGetGroup(1, out var range1) ? range1.Success : default));
        Assert.True((actual.TryGetGroup(2, out var range2) ? range2.Success : default));
        Assert.Equal(oracle.Groups[2].Value, System.Text.Encoding.UTF8.GetString(actual.GetGroupMemory(2).Span));
        Assert.Equal("b", System.Text.Encoding.UTF8.GetString(actual.GetGroupMemory(regex.GroupNumberFromName("2")).Span));
    }

    [Fact]
    public void OwnedDetailedMissIsUnsuccessful()
    {
        var regex = new Utf8Regex("(a+)(b+)", RegexOptions.CultureInvariant);
        Assert.False(Utf8MatchSequence.First(regex.CreateMatchCursor(Encoding.UTF8.GetBytes("xyz"))).Success);
    }

    [Theory]
    [InlineData("(a+)(b+)", "aab aabb aaabbb")]
    [InlineData("(ab|cd)+", "ab cd abcd")]
    [InlineData("a*", "b")]
    [InlineData("a*", "😀")]
    [InlineData("(a+)(b+)", "éaaabbé")]
    public void OwnedEnumerationMatchesScalarContract(string pattern, string inputText)
    {
        Utf8AsciiScalarOperationAssertions.Operations(pattern, RegexOptions.CultureInvariant, inputText, requireSlice: true);
    }

    [Fact]
    public void AbandonedEnumerationLeavesNoCorruption()
    {
        var regex = new Utf8Regex("(a+)(b+)", RegexOptions.CultureInvariant);
        var input = Encoding.UTF8.GetBytes("aab aabb aaabbb");
        var enumerator = regex.EnumerateMatches(input);
        Assert.True(enumerator.MoveNext());
        Assert.Equal((0, 3), (enumerator.Current.StartOffsetInBytes, (enumerator.Current.EndOffsetInBytes - enumerator.Current.StartOffsetInBytes)));
        var second = new List<(int Index, int Length)>();
        foreach (var match in regex.EnumerateMatches(input))
        {
            second.Add((match.StartOffsetInBytes, (match.EndOffsetInBytes - match.StartOffsetInBytes)));
        }

        Assert.Equal([(0, 3), (4, 4), (9, 6)], second);
    }

    [Fact]
    public void MidEnumerationCancellationAborts()
    {
        var regex = new Utf8Regex("(a+)(b+)", RegexOptions.CultureInvariant);
        using var source = new CancellationTokenSource();
        var enumerator = regex.EnumerateMatches(Encoding.UTF8.GetBytes("aab aabb aaabbb"), cancellationToken: source.Token);
        Assert.True(enumerator.MoveNext());
        source.Cancel();
        try
        {
            enumerator.MoveNext();
            Assert.Fail("Expected OperationCanceledException.");
        }
        catch (OperationCanceledException exception)
        {
            Assert.Equal(source.Token, exception.CancellationToken);
        }
    }

    [Fact]
    public void PreCanceledEnumerationThrowsAtBegin()
    {
        var regex = new Utf8Regex("(a+)(b+)", RegexOptions.CultureInvariant);
        using var canceled = new CancellationTokenSource();
        canceled.Cancel();
        Assert.Throws<OperationCanceledException>(() => regex.EnumerateMatches(Encoding.UTF8.GetBytes("aab"), cancellationToken: canceled.Token));
    }
}
