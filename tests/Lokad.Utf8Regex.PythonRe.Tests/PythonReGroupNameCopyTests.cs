using System.Buffers;
using System.Text;

namespace Lokad.Utf8Regex.PythonRe.Tests;

public sealed class PythonReGroupNameCopyTests
{
    [Fact]
    public void OpeningOrderKeepsOriginalUnicodeNamesAndNumericSlots()
    {
        var regex = new Utf8PythonRegex("(?P<α>a)(b)(?P<g1>c)(?P<𐐀>d)"u8);
        string[] expected = ["0", "α", "2", "g1", "𐐀"];
        Assert.Equal(expected.Length, regex.CaptureGroupCount + 1);
        var destination = new byte[8];
        for (var slot = 0; slot < expected.Length; slot++)
        {
            Assert.Equal(OperationStatus.Done, regex.TryCopyGroupNameUtf8(slot, destination, out var required));
            Assert.Equal(Encoding.UTF8.GetBytes(expected[slot]), destination.AsSpan(0, required).ToArray());
        }
        Assert.True(regex.TryGetGroupNumber("α"u8, out var number));
        Assert.Equal(1, number);
        Assert.False(regex.TryGetGroupNumber("2"u8, out _));
    }

    [Fact]
    public void SupplementaryNameAcrossCopyCheckpointKeepsSizeAndBoundaries()
    {
        var name = new string('α', 2047) + "𐐀";
        var expected = Encoding.UTF8.GetBytes(name);
        var regex = new Utf8PythonRegex("(?P<" + name + ">a)");
        var destination = Enumerable.Repeat((byte)0xa5, expected.Length + 2).ToArray();
        Assert.Equal(OperationStatus.DestinationTooSmall,
            regex.TryCopyGroupNameUtf8(1, destination.AsSpan(1, expected.Length - 1), out var required));
        Assert.Equal(expected.Length, required);
        Assert.All(destination, value => Assert.Equal(0xa5, value));
        Assert.Equal(OperationStatus.Done,
            regex.TryCopyGroupNameUtf8(1, destination.AsSpan(1, expected.Length), out required));
        Assert.Equal(expected, destination.AsSpan(1, required).ToArray());
        Assert.Equal(0xa5, destination[0]);
        Assert.Equal(0xa5, destination[^1]);
    }

    [Fact]
    public void OriginalCancellationPrecedesInvalidSlotAndWrites()
    {
        var regex = new Utf8PythonRegex("(?P<名>a)");
        var destination = Enumerable.Repeat((byte)0xa5, 8).ToArray();
        Assert.Throws<ArgumentOutOfRangeException>(() => regex.TryCopyGroupNameUtf8(-1, destination, out _));
        Assert.Throws<ArgumentOutOfRangeException>(() => regex.TryCopyGroupNameUtf8(2, destination, out _));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        Assert.Equal(cancellation.Token, Assert.Throws<OperationCanceledException>(() =>
            regex.TryCopyGroupNameUtf8(-1, destination, out _, cancellation.Token)).CancellationToken);
        Assert.All(destination, value => Assert.Equal(0xa5, value));
    }

    [Fact]
    public void RepeatedNameCopiesAllocateNoIntermediateMetadata()
    {
        var regex = new Utf8PythonRegex("(?P<名>a)");
        var destination = new byte[3];
        for (var i = 0; i < 16; i++) regex.TryCopyGroupNameUtf8(1, destination, out _);
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 64; i++) regex.TryCopyGroupNameUtf8(1, destination, out _);
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
        Assert.Equal("名"u8.ToArray(), destination);
    }
}
