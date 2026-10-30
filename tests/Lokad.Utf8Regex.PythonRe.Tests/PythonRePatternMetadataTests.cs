using System.Buffers;
using System.Text;

namespace Lokad.Utf8Regex.PythonRe.Tests;

public sealed class PythonRePatternMetadataTests
{
    [Fact]
    public void NativeLookupUsesValidatedNamesWithoutNumericOrNormalizationAliases()
    {
        var regex = new Utf8PythonRegex("(?P<名>a)(b)(?P<é>c)"u8);
        Assert.True(regex.TryGetGroupNumber("名"u8, out var number)); Assert.Equal(1, number);
        Assert.True(regex.TryGetGroupNumber("é"u8, out number)); Assert.Equal(3, number);
        Assert.False(regex.TryGetGroupNumber("e\u0301"u8, out number)); Assert.Equal(0, number);
        Assert.False(regex.TryGetGroupNumber("1"u8, out _));
        Assert.False(regex.TryGetGroupNumber("0"u8, out _));
        Assert.False(regex.TryGetGroupNumber([], out _));
        Assert.Throws<ArgumentException>(() => regex.TryGetGroupNumber(new byte[] { (byte)'z', 0xff }, out _));
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        Assert.Equal(cancellation.Token, Assert.Throws<OperationCanceledException>(() =>
            regex.TryGetGroupNumber("名"u8, out _, cancellation.Token)).CancellationToken);
        for (var i = 0; i < 16; i++) regex.TryGetGroupNumber("名"u8, out _);
        var allocated = GC.GetAllocatedBytesForCurrentThread();
        var checksum = 0;
        for (var i = 0; i < 64; i++) { regex.TryGetGroupNumber("名"u8, out number); checksum += number; }
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - allocated); Assert.Equal(64, checksum);
    }

    [Theory]
    [InlineData("", 0)]
    [InlineData("(?:a)", 0)]
    [InlineData("(a)(b)", 2)]
    [InlineData("(?P<name>a)(b)", 2)]
    [InlineData("(?=(a))a", 1)]
    [InlineData("(?i)(a)", 1)]
    public void CaptureCountComesFromValidatedGrammar(string source, int count)
    {
        Assert.Equal(count, new Utf8PythonRegex(source).CaptureGroupCount);
        Assert.Equal(count, new Utf8PythonRegex(Encoding.UTF8.GetBytes(source)).CaptureGroupCount);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void CopiesOriginalSourceIntoCallerStorageAndPreservesCancellation(bool native)
    {
        var source = "(?#" + new string('x', 2043) + ")é😀\0";
        var encoded = Encoding.UTF8.GetBytes(source);
        var regex = native ? new Utf8PythonRegex(encoded) : new Utf8PythonRegex(source);
        var destination = Enumerable.Repeat((byte)0xa5, encoded.Length + 2).ToArray();
        Assert.Equal(OperationStatus.DestinationTooSmall,
            regex.TryCopyPatternUtf8(destination.AsSpan(1, encoded.Length - 1), out var required));
        Assert.Equal(encoded.Length, required); Assert.All(destination, value => Assert.Equal(0xa5, value));
        Assert.Equal(OperationStatus.Done, regex.TryCopyPatternUtf8(destination.AsSpan(1, encoded.Length), out required));
        Assert.Equal(encoded.Length, required); Assert.Equal(encoded, destination.AsSpan(1, required).ToArray());
        Assert.Equal(0xa5, destination[0]); Assert.Equal(0xa5, destination[^1]);
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        var failure = Assert.Throws<OperationCanceledException>(() => regex.TryCopyPatternUtf8(destination, out _, cancellation.Token));
        Assert.Equal(cancellation.Token, failure.CancellationToken);
        destination.AsSpan().Clear(); Assert.Equal(source, regex.Pattern);
    }

    [Fact]
    public void InvalidLegacySurrogatesDoNotBecomeReplacementCharacters()
    {
        var regex = new Utf8PythonRegex("a\ud800b");
        var destination = Enumerable.Repeat((byte)0xa5, 32).ToArray();
        Assert.Equal(OperationStatus.InvalidData, regex.TryCopyPatternUtf8(destination, out var required));
        Assert.Equal(0, required); Assert.All(destination, value => Assert.Equal(0xa5, value));
    }
}
