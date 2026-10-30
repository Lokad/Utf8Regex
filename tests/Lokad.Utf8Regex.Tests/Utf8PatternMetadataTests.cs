using System.Buffers;
using System.Text;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8PatternMetadataTests
{
    [Theory]
    [InlineData("(a)(b)", "01")]
    [InlineData("(a)(b)", "0000000000000000000000000000000000000000002")]
    [InlineData("(a)(b)", "4294967296")]
    [InlineData("(a)(b)", "+1")]
    [InlineData("(a)(b)", "")]
    [InlineData("(?<名>a)(?<é>b)", "名")]
    [InlineData("(?<名>a)(?<é>b)", "é")]
    [InlineData("(?<名>a)(?<é>b)", "e\u0301")]
    [InlineData("(?<名>a)(?<é>b)", "01")]
    [InlineData("(?<12>a)(b)", "12")]
    [InlineData("(?<12>a)(b)", "012")]
    [InlineData("(?<12>a)(b)", "1")]
    [InlineData("(?<same>a)(?<same>b)", "same")]
    public void NativeLookupPreservesTheExistingNameAndNumericContract(string pattern, string name)
    {
        var regex = Utf8Regex.Compile(Encoding.UTF8.GetBytes(pattern));
        Assert.Equal(regex.GroupNumberFromName(name), regex.GroupNumberFromUtf8Name(Encoding.UTF8.GetBytes(name)));
    }

    [Fact]
    public void NativeLookupValidatesTheWholeNameAndDoesNotAllocateAKeyString()
    {
        var regex = Utf8Regex.Compile("(?<名>a)"u8);
        var malformed = new byte[] { (byte)'z', 0xff };
        var failure = Assert.Throws<ArgumentException>(() => regex.GroupNumberFromUtf8Name(malformed));
        Assert.Equal("byteOffset", failure.ParamName); Assert.Contains("byte offset 1", failure.Message);
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        Assert.Equal(cancellation.Token, Assert.Throws<OperationCanceledException>(() =>
            regex.GroupNumberFromUtf8Name(malformed, cancellation.Token)).CancellationToken);
        for (var i = 0; i < 16; i++) regex.GroupNumberFromUtf8Name("名"u8);
        var allocated = GC.GetAllocatedBytesForCurrentThread();
        var checksum = 0;
        for (var i = 0; i < 64; i++) checksum += regex.GroupNumberFromUtf8Name("名"u8);
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - allocated); Assert.Equal(64, checksum);
    }

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
