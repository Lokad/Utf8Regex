using Lokad.Utf8Regex.Internal;

namespace Lokad.Utf8Regex.Pcre2.Tests;

public sealed class Pcre2PartialEndAnchoredTests
{
    [Theory]
    [InlineData("a")] // literal driver
    [InlineData("(a)b+")] // specialized partial helper
    [InlineData(@"(?<v>\x41)(😀)")] // normal detailed matcher
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
    [InlineData(Pcre2PartialMode.Soft)]
    [InlineData(Pcre2PartialMode.Hard)]
    public void CompiledEndAnchoredAlsoRejectsPartial(Pcre2PartialMode mode)
    {
        var regex = new Utf8Pcre2Regex("(a)b+", options: Pcre2CompileOptions.EndAnchored);
        var error = Assert.Throws<Pcre2MatchException>(() => { _ = regex.Probe("abbb"u8, mode); });
        Assert.Equal("BadOption", error.ErrorKind.ToString());
    }

    [Theory]
    [InlineData("(a)b+", "abxabbb", 3, 7)]
    [InlineData("dog(sbody)??", "dogsbody", 0, 8)]
    public void ValidNonPartialCompiledEndAnchoredFindsLaterCandidatesAndAlternatives(
        string pattern, string text, int start, int end)
    {
        var regex = new Utf8Pcre2Regex(pattern, options: Pcre2CompileOptions.EndAnchored);
        var input = System.Text.Encoding.UTF8.GetBytes(text);
        ReadOnlySpan<byte> expected1Input = input;
        var expected1Captures = new Utf8Pcre2CaptureRange[regex.CaptureGroupCount + 1];
        var expected = regex.Match(expected1Input, captures: expected1Captures);
        Assert.Equal((start, end), (expected1Captures[0].StartOffsetInBytes, expected1Captures[0].EndOffsetInBytes));
        var actual = regex.Probe(input, Pcre2PartialMode.None).GetMatch();
        Assert.Equal((start, end), (actual.GetGroup(0).StartOffsetInBytes, actual.GetGroup(0).EndOffsetInBytes));
        Assert.Equal((expected.Success ? expected1Captures.Length : 0), actual.CaptureSlotCount);
    }

    [Fact]
    public void ValidEndAnchoredCapturePublicationRetainsFiniteAuthorityAndSnapshots()
    {
        var capacity = Utf8ConstructionBudget.ArrayBytes<Pcre2GroupData>(2);
        Utf8Pcre2Regex Create(ulong resultBytes) => new Utf8Pcre2Regex("(?<head>a)b+", options: Pcre2CompileOptions.EndAnchored, compileSettings: default, policy: new Utf8RegexPolicy
        {
            ResourcePolicy = new()
            {
                MaxWorkSteps = 100000,
                MaxDepth = 256,
                MaxWorkingBytes = 1 << 20,
                MaxResultBytes = resultBytes
            },
            MatchTimeout = Timeout.InfiniteTimeSpan
        });
        Assert.Equal(Pcre2ErrorKind.ResultLimit, Assert.Throws<Pcre2MatchException>(() =>
        { _ = Create(capacity - 1).Probe("😀abxabbb"u8, Pcre2PartialMode.None); }).ErrorKind);
        var regex = Create(capacity);
        var retained = regex.Probe("😀abxabbb"u8, Pcre2PartialMode.None).GetMatch();
        Assert.Equal((7, 11), (retained.GetGroup(0).StartOffsetInBytes, retained.GetGroup(0).EndOffsetInBytes));
        Assert.True(retained.TryGetFirstSetGroup("head", out var alias));
        Assert.Equal(Utf8Pcre2ProbeKind.NoMatch, regex.Probe("abx"u8, Pcre2PartialMode.None).Kind);
        Assert.Equal("abbb", System.Text.Encoding.UTF8.GetString(retained.Value.GetValueBytes()));
        Assert.Equal("a", System.Text.Encoding.UTF8.GetString(alias.GetValueBytes()));
        Assert.Equal("a", System.Text.Encoding.UTF8.GetString(regex.Probe("😀abxabbb"u8, Pcre2PartialMode.None).GetMatch().GetGroup(1).GetValueBytes()));
    }

    [Fact]
    public void InvalidCombinationIsStructuredAndPreCancellationKeepsItsToken()
    {
        var regex = new Utf8Pcre2Regex("(a)b+", options: Pcre2CompileOptions.EndAnchored, compileSettings: default, policy: new Utf8RegexPolicy
        {
            ResourcePolicy = new Utf8ResourcePolicy
            {
                MaxWorkSteps = 1,
                MaxWorkingBytes = 1,
                MaxResultBytes = 1
            },
            MatchTimeout = Timeout.InfiniteTimeSpan
        });
        Assert.Equal("BadOption", Assert.Throws<Pcre2MatchException>(() =>
        { _ = regex.Probe("abbb"u8, Pcre2PartialMode.Soft); }).ErrorKind.ToString());
        using var source = new CancellationTokenSource();
        source.Cancel();
        var error = Assert.Throws<OperationCanceledException>(() =>
        { _ = regex.Probe("abbb"u8, Pcre2PartialMode.Hard, 0, Pcre2MatchOptions.EndAnchored, source.Token); });
        Assert.Equal(source.Token, error.CancellationToken);
    }
}
