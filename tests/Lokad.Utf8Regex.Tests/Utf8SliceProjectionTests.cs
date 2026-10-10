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
            Assert.Equal(expectedIndex, actual.IndexInUtf16);
            Assert.Equal(expectedLength, actual.LengthInUtf16);
            Assert.Equal(oracle.Index, actual.IndexInUtf16);
            Assert.Equal(oracle.Length, actual.LengthInUtf16);
            Assert.True(actual.IsByteAligned);
            Assert.Equal(Encoding.UTF8.GetString(input[actual.IndexInBytes..(actual.IndexInBytes + actual.LengthInBytes)]), oracle.Value);
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
        var actual = regex.MatchDetailed(input);
        var oracle = new Regex("(a+)(b+)", RegexOptions.CultureInvariant, Regex.InfiniteMatchTimeout).Match("xxaaabbyy");
        Assert.True(actual.Success);
        Assert.Equal(3, actual.GroupCount);
        Assert.Equal(oracle.Groups[0].Value, actual.GetGroup(0).GetValueString());
        Assert.Equal(oracle.Groups[1].Value, actual.GetGroup(1).GetValueString());
        Assert.Equal(oracle.Groups[2].Value, actual.GetGroup(2).GetValueString());
        Assert.Equal(oracle.Groups[1].Index, actual.GetGroup(1).IndexInUtf16);
        Assert.True(regex.Inspection.PreparedRegex.ScalarProgram is not null);
    }

    [Fact]
    public void OwnedDetailedReportsUnmatchedGroups()
    {
        var regex = new Utf8Regex("(a)|(b)", RegexOptions.CultureInvariant);
        var actual = regex.MatchDetailed(Encoding.UTF8.GetBytes("xb"));
        var oracle = new Regex("(a)|(b)", RegexOptions.CultureInvariant, Regex.InfiniteMatchTimeout).Match("xb");
        Assert.True(actual.Success);
        Assert.False(actual.GetGroup(1).Success);
        Assert.True(actual.GetGroup(2).Success);
        Assert.Equal(oracle.Groups[2].Value, actual.GetGroup(2).GetValueString());
        Assert.Equal("b", actual.GetGroup("2").GetValueString());
    }

    [Fact]
    public void OwnedDetailedMissIsUnsuccessful()
    {
        var regex = new Utf8Regex("(a+)(b+)", RegexOptions.CultureInvariant);
        Assert.False(regex.MatchDetailed(Encoding.UTF8.GetBytes("xyz")).Success);
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
        Assert.Equal((0, 3), (enumerator.Current.IndexInUtf16, enumerator.Current.LengthInUtf16));
        var second = new List<(int Index, int Length)>();
        foreach (var match in regex.EnumerateMatches(input))
        {
            second.Add((match.IndexInUtf16, match.LengthInUtf16));
        }

        Assert.Equal([(0, 3), (4, 4), (9, 6)], second);
    }

    [Fact]
    public void MidEnumerationCancellationAborts()
    {
        var regex = new Utf8Regex("(a+)(b+)", RegexOptions.CultureInvariant);
        using var source = new CancellationTokenSource();
        var enumerator = regex.EnumerateMatches(Encoding.UTF8.GetBytes("aab aabb aaabbb"), source.Token);
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
        Assert.Throws<OperationCanceledException>(() => regex.EnumerateMatches(Encoding.UTF8.GetBytes("aab"), canceled.Token));
    }
}
