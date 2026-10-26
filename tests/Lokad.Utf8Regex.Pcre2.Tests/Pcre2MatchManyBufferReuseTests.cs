using System.Text;
using System.Runtime.InteropServices;
using Lokad.Utf8Regex.Pcre2;

namespace Lokad.Utf8Regex.Pcre2.Tests;

// Caller-owned destination reuse (the managed ovector analog): only the
// [0..written) prefix is ever valid, and every call reports its own prefix
// exactly, regardless of what previous calls left in the buffer. Unlike the
// native PCRE.NET baseline buffer, no match storage lives on the instance,
// so reuse across calls and patterns cannot leak state. See the R08 pcre-net
// disposition in PLAN.md.
public sealed class Pcre2MatchManyBufferReuseTests
{
    [Theory]
    [InlineData("a", 0, 0)] [InlineData("a", 0, 32)] [InlineData("a", 32, 0)]
    [InlineData("(?:ab|cd)+", 0, 0)] [InlineData("(?:ab|cd)+", 0, 32)] [InlineData("(?:ab|cd)+", 32, 0)]
    [InlineData("z", 0, 0)] [InlineData("(?:xy|zw)+", 0, 0)]
    public void OverlapIsRejectedBeforeAnyWriteIncludingOnAMiss(string pattern, int inputOffset, int outputOffset)
    {
        var regex = Utf8Pcre2Regex.Create(pattern, Pcre2CompileOptions.None, default,
            new() { MaxWorkSteps = 1_000_000, MaxDepth = 1024, MaxWorkingBytes = 1_048_576, MaxResultBytes = 1_048_576 },
            Timeout.InfiniteTimeSpan);
        var storage = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat("ab", 96)));
        var original = storage.ToArray();
        ArgumentException? failure = null;
        try
        {
            regex.MatchMany(storage.AsSpan(inputOffset, 96),
                MemoryMarshal.Cast<byte, Utf8Pcre2MatchData>(storage.AsSpan(outputOffset))[..2], out _);
        }
        catch (ArgumentException error) { failure = error; }
        Assert.NotNull(failure);
        Assert.Equal("destination", failure.ParamName);
        Assert.Equal(original, storage);
    }

    [Fact]
    public void AdjacentStorageAndEmptyDestinationRemainUsable()
    {
        var regex = new Utf8Pcre2Regex("a");
        var storage = Enumerable.Repeat((byte)'a', 128).ToArray();
        var destination = MemoryMarshal.Cast<byte, Utf8Pcre2MatchData>(storage.AsSpan(0, 96));
        Assert.Equal(4, regex.MatchMany(storage.AsSpan(96), destination, out var more));
        Assert.True(more);
        Assert.Equal(new[] { 0, 1, 2, 3 }, destination.ToArray().Select(m => m.StartOffsetInBytes));
        var before = storage.ToArray();
        Assert.Equal(0, regex.MatchMany(storage.AsSpan(96), destination[..0], out more));
        Assert.True(more);
        Assert.Equal(before, storage);
    }

    [Fact]
    public void PreCancellationPrecedesOverlapAndMalformedInput()
    {
        using var source = new CancellationTokenSource();
        source.Cancel();
        var regex = new Utf8Pcre2Regex("a");
        var storage = Enumerable.Repeat((byte)'a', 128).ToArray();
        storage[0] = 0xFF;
        var original = storage.ToArray();
        var failure = Assert.Throws<OperationCanceledException>(() =>
            regex.MatchMany(storage, MemoryMarshal.Cast<byte, Utf8Pcre2MatchData>(storage.AsSpan()), out _,
                0, Pcre2MatchOptions.None, source.Token));
        Assert.Equal(source.Token, failure.CancellationToken);
        Assert.Equal(original, storage);
    }

    [Fact]
    public void ReusedDestinationReportsExactPrefixPerCall()
    {
        var regex = new Utf8Pcre2Regex("ab");
        var destination = new Utf8Pcre2MatchData[4];

        var written = regex.MatchMany(Encoding.UTF8.GetBytes("ab ab ab"), destination, out var isMore);
        Assert.Equal(3, written);
        Assert.False(isMore);
        Assert.Equal(new[] { 0, 3, 6 }, new[] { destination[0].StartOffsetInBytes, destination[1].StartOffsetInBytes, destination[2].StartOffsetInBytes });

        written = regex.MatchMany(Encoding.UTF8.GetBytes("xxab"), destination, out isMore);
        Assert.Equal(1, written);
        Assert.False(isMore);
        Assert.True(destination[0].Success);
        Assert.Equal(2, destination[0].StartOffsetInBytes);
        Assert.Equal(4, destination[0].EndOffsetInBytes);
    }

    [Fact]
    public void UndersizedDestinationReportsPartialPrefixAndMore()
    {
        var regex = new Utf8Pcre2Regex("ab");
        var destination = new Utf8Pcre2MatchData[2];

        var written = regex.MatchMany(Encoding.UTF8.GetBytes("ab ab ab"), destination, out var isMore);
        Assert.Equal(2, written);
        Assert.True(isMore);
        Assert.Equal(0, destination[0].StartOffsetInBytes);
        Assert.Equal(3, destination[1].StartOffsetInBytes);
    }

    [Fact]
    public void MissLeavesDestinationUntouched()
    {
        var regex = new Utf8Pcre2Regex("ab");
        var destination = new Utf8Pcre2MatchData[2];

        var written = regex.MatchMany(Encoding.UTF8.GetBytes("zz zz"), destination, out var isMore);
        Assert.Equal(0, written);
        Assert.False(isMore);
        Assert.False(destination[0].Success);
        Assert.False(destination[1].Success);
    }
}
