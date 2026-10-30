using System.Buffers;
using System.Text;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8PatternMetadataTests
{
    [Theory]
    [InlineData("", 0)]
    [InlineData("(?:a)", 0)]
    [InlineData("(a)(b)", 2)]
    [InlineData("(?<name>a)(?<name>b)", 1)]
    [InlineData("(?<12>a)(b)", 2)]
    [InlineData("(a)(?<12>b)(?<name>c)", 3)]
    [InlineData("(?n)(a)(?<x>b)", 1)]
    [InlineData("(?<=(a))b", 1)]
    public void CaptureCountUsesDefinedIdentitiesRatherThanHighestNumber(string pattern, int count)
    {
        Assert.Equal(count, new Utf8Regex(pattern).CaptureGroupCount);
        Assert.Equal(count, Utf8Regex.Compile(Encoding.UTF8.GetBytes(pattern)).CaptureGroupCount);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void SourceCopyReturnsRequiredCapacityAndOwnsNoAliases(bool native)
    {
        const string source = "(?#trivia)é😀\0";
        var encoded = Encoding.UTF8.GetBytes(source);
        var regex = native ? Utf8Regex.Compile(encoded) : new Utf8Regex(source);
        var destination = Enumerable.Repeat((byte)0xa5, encoded.Length + 4).ToArray();
        Assert.Equal(OperationStatus.DestinationTooSmall,
            regex.TryCopyPatternUtf8(destination.AsSpan(2, encoded.Length - 1), out var required));
        Assert.Equal(encoded.Length, required);
        Assert.All(destination, value => Assert.Equal(0xa5, value));
        Assert.Equal(OperationStatus.Done,
            regex.TryCopyPatternUtf8(destination.AsSpan(2, encoded.Length), out required));
        Assert.Equal(encoded.Length, required);
        Assert.Equal(encoded, destination.AsSpan(2, required).ToArray());
        Assert.Equal(new byte[] { 0xa5, 0xa5 }, destination[..2]);
        Assert.Equal(new byte[] { 0xa5, 0xa5 }, destination[^2..]);
        destination.AsSpan().Clear();
        Assert.Equal(source, regex.Pattern);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void UnpairedTextSurrogatesCannotBeSilentlyChangedByMetadataExport(bool high)
    {
        var source = high ? new string((char)0xd800, 1) : "a" + (char)0xdc00 + "b";
        var regex = new Utf8Regex(source);
        var destination = Enumerable.Repeat((byte)0xa5, 16).ToArray();
        Assert.Equal(OperationStatus.InvalidData, regex.TryCopyPatternUtf8(destination, out var required));
        Assert.Equal(0, required);
        Assert.All(destination, value => Assert.Equal(0xa5, value));
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void ScalarCrossingCopyCheckpointsIsExactAndPreCancellationKeepsItsToken(bool native)
    {
        var source = new string('a', 2047) + "😀é\0";
        var encoded = Encoding.UTF8.GetBytes(source);
        var regex = native ? Utf8Regex.Compile(encoded) : new Utf8Regex(source);
        var destination = new byte[encoded.Length];
        Assert.Equal(OperationStatus.Done, regex.TryCopyPatternUtf8(destination, out var required));
        Assert.Equal(encoded.Length, required); Assert.Equal(encoded, destination);
        using var canceled = new CancellationTokenSource(); canceled.Cancel();
        var failure = Assert.Throws<OperationCanceledException>(() => regex.TryCopyPatternUtf8(destination, out _, canceled.Token));
        Assert.Equal(canceled.Token, failure.CancellationToken);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void RepeatedCopiesAllocateNoIntermediateSourceOrStorage(bool native)
    {
        var encoded = Encoding.UTF8.GetBytes(new string('a', 128) + "😀é");
        var regex = native ? Utf8Regex.Compile(encoded) : new Utf8Regex(Encoding.UTF8.GetString(encoded));
        var destination = new byte[encoded.Length];
        for (var i = 0; i < 16; i++) regex.TryCopyPatternUtf8(destination, out _);
        var allocated = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 64; i++) regex.TryCopyPatternUtf8(destination, out _);
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - allocated);
        Assert.Equal(encoded, destination);
    }
}
