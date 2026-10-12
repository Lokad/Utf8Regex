using Lokad.Utf8Regex.Internal;

namespace Lokad.Utf8Regex.Pcre2.Tests;

public sealed class Pcre2PartialEndAnchoredTests
{
    [Theory]
    [InlineData("a")] // literal driver
    [InlineData("(a)b+")] // specialized partial helper
    [InlineData(@"(?<v>\u0041)(😀)")] // normal detailed matcher
    public void PartialEndAnchoredFailsBeforeAnyResult(string pattern)
    {
        var regex = new Utf8Pcre2Regex(pattern);
        foreach (var mode in new[] { Pcre2PartialMode.Soft, Pcre2PartialMode.Hard })
        {
            var error = Assert.Throws<Pcre2MatchException>(() =>
            { _ = regex.Probe("abbb"u8, mode, 0, Pcre2MatchOptions.EndAnchored); });
            Assert.Equal("BadOption", error.ErrorKind.ToString());
        }
    }

    [Theory]
    [InlineData(Pcre2PartialMode.Soft)] [InlineData(Pcre2PartialMode.Hard)]
    public void CompiledEndAnchoredAlsoRejectsPartial(Pcre2PartialMode mode)
    {
        var regex = new Utf8Pcre2Regex("(a)b+", Pcre2CompileOptions.EndAnchored);
        var error = Assert.Throws<Pcre2MatchException>(() => { _ = regex.Probe("abbb"u8, mode); });
        Assert.Equal("BadOption", error.ErrorKind.ToString());
    }

    [Theory]
    [InlineData("(a)b+", "abxabbb", 3, 7)]
    [InlineData("dog(sbody)??", "dogsbody", 0, 8)]
    public void ValidNonPartialCompiledEndAnchoredFindsLaterCandidatesAndAlternatives(
        string pattern, string text, int start, int end)
    {
        var regex = new Utf8Pcre2Regex(pattern, Pcre2CompileOptions.EndAnchored);
        var input = System.Text.Encoding.UTF8.GetBytes(text);
        var expected = regex.MatchDetailed(input);
        Assert.Equal((start, end), (expected.GetGroup(0).StartOffsetInBytes, expected.GetGroup(0).EndOffsetInBytes));
        var actual = regex.Probe(input, Pcre2PartialMode.None).GetMatch();
        Assert.Equal((start, end), (actual.GetGroup(0).StartOffsetInBytes, actual.GetGroup(0).EndOffsetInBytes));
        Assert.Equal(expected.CaptureSlotCount, actual.CaptureSlotCount);
    }

    [Fact]
    public void ValidEndAnchoredCapturePublicationRetainsFiniteAuthorityAndSnapshots()
    {
        var capacity = Utf8ConstructionBudget.ArrayBytes<Pcre2GroupData>(2);
        Utf8Pcre2Regex Create(ulong resultBytes) => Utf8Pcre2Regex.Create("(?<head>a)b+", Pcre2CompileOptions.EndAnchored, default,
            new() { MaxWorkSteps = 100000, MaxDepth = 256, MaxWorkingBytes = 1 << 20, MaxResultBytes = resultBytes }, Timeout.InfiniteTimeSpan);
        Assert.Equal(Pcre2ErrorKind.ResultLimit, Assert.Throws<Pcre2MatchException>(() =>
        { _ = Create(capacity - 1).Probe("😀abxabbb"u8, Pcre2PartialMode.None); }).ErrorKind);
        var regex = Create(capacity);
        var retained = regex.Probe("😀abxabbb"u8, Pcre2PartialMode.None).GetMatch();
        Assert.Equal((7, 11), (retained.GetGroup(0).StartOffsetInBytes, retained.GetGroup(0).EndOffsetInBytes));
        Assert.True(retained.TryGetFirstSetGroup("head", out var alias));
        Assert.Equal(Utf8Pcre2ProbeKind.NoMatch, regex.Probe("abx"u8, Pcre2PartialMode.None).Kind);
        Assert.Equal("abbb", retained.GetValueString());
        Assert.Equal("a", alias.GetValueString());
        Assert.Equal("a", regex.Probe("😀abxabbb"u8, Pcre2PartialMode.None).GetMatch().GetGroup(1).GetValueString());
    }

    [Fact]
    public void InvalidCombinationIsStructuredAndPreCancellationKeepsItsToken()
    {
        var regex = new Utf8Pcre2Regex("(a)b+", Pcre2CompileOptions.EndAnchored, default,
            new() { MatchLimit = 1, HeapLimitInBytes = 1, ResultLimitInBytes = 1 }, Timeout.InfiniteTimeSpan);
        Assert.Equal("BadOption", Assert.Throws<Pcre2MatchException>(() =>
        { _ = regex.Probe("abbb"u8, Pcre2PartialMode.Soft); }).ErrorKind.ToString());
        using var source = new CancellationTokenSource();
        source.Cancel();
        var error = Assert.Throws<OperationCanceledException>(() =>
        { _ = regex.Probe("abbb"u8, Pcre2PartialMode.Hard, 0, Pcre2MatchOptions.EndAnchored, source.Token); });
        Assert.Equal(source.Token, error.CancellationToken);
    }
}
