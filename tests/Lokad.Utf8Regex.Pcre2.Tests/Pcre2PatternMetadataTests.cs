using System.Buffers;
using System.Text;

namespace Lokad.Utf8Regex.Pcre2.Tests;

public sealed class Pcre2PatternMetadataTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void NameEntryBytesPreserveOrderedPairsAndCallerCapacity(bool native)
    {
        (string Pattern, string[] Names, int[] Numbers)[] cases =
        [
            ("a", [], []),
            ("(?<z>a)(b)(?<名>c)", ["z", "名"], [1, 3]),
            ("(?J)(?<x>a)(?<y>b)(?<x>c)", ["x", "y", "x"], [1, 2, 3]),
            ("(?|(?<x>a)|(?<x>b))", ["x"], [1]),
            ("(?<é>a)(?<É>b)", ["é", "É"], [1, 2])
        ];
        foreach (var item in cases)
        {
            using var regex = native ? new Utf8Pcre2Regex(Encoding.UTF8.GetBytes(item.Pattern))
                : new Utf8Pcre2Regex(item.Pattern);
            Assert.Equal(item.Names.Length, regex.NameEntryCount);
            for (var index = 0; index < item.Names.Length; index++)
            {
                var expected = Encoding.UTF8.GetBytes(item.Names[index]);
                var storage = Enumerable.Repeat((byte)0xa5, expected.Length + 5).ToArray();
                for (var capacity = 0; capacity < expected.Length; capacity++)
                {
                    Assert.Equal(OperationStatus.DestinationTooSmall,
                        regex.TryCopyNameEntryUtf8(index, storage.AsSpan(2, capacity), out var required, out var number));
                    Assert.Equal(expected.Length, required); Assert.Equal(item.Numbers[index], number);
                    Assert.All(storage, value => Assert.Equal(0xa5, value));
                }
                Assert.Equal(OperationStatus.Done,
                    regex.TryCopyNameEntryUtf8(index, storage.AsSpan(2, expected.Length + 1), out var bytesRequired, out var groupNumber));
                Assert.Equal(expected.Length, bytesRequired); Assert.Equal(item.Numbers[index], groupNumber);
                Assert.Equal(expected, storage.AsSpan(2, expected.Length).ToArray());
                Assert.Equal(new byte[] { 0xa5, 0xa5 }, storage[..2]);
                Assert.All(storage[(2 + expected.Length)..], value => Assert.Equal(0xa5, value));
                storage.AsSpan().Fill(0xff);
                Assert.Equal(OperationStatus.Done, regex.TryCopyNameEntryUtf8(index, storage, out _, out _));
                Assert.Equal(expected, storage.AsSpan(0, expected.Length).ToArray());
            }
            foreach (var badIndex in new[] { -1, item.Names.Length, int.MaxValue })
                Assert.Equal("nameEntryIndex", Assert.Throws<ArgumentOutOfRangeException>(() =>
                    regex.TryCopyNameEntryUtf8(badIndex, new byte[128], out _, out _)).ParamName);
        }
        using var allocationRegex = new Utf8Pcre2Regex("(?<名>a)");
        var destination = new byte[3];
        for (var i = 0; i < 16; i++) allocationRegex.TryCopyNameEntryUtf8(0, destination, out _, out _);
        var allocated = GC.GetAllocatedBytesForCurrentThread();
        var checksum = 0;
        for (var i = 0; i < 64; i++)
        {
            allocationRegex.TryCopyNameEntryUtf8(0, destination, out var length, out var number);
            checksum += length + number;
        }
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - allocated); Assert.Equal(256, checksum);
    }

    [Fact]
    public void NativeLookupPreservesDuplicateNumbersAndCallerCapacity()
    {
        var regex = new Utf8Pcre2Regex("(?J)(?<名>a)|(?<名>b)|(?<é>c)"u8);
        var numbers = new int[3];
        Assert.Equal(1, regex.CopyNumbersForUtf8Name("名"u8, numbers.AsSpan(1, 1), out var more));
        Assert.True(more); Assert.Equal(new[] { 0, 1, 0 }, numbers);
        Assert.Equal(2, regex.CopyNumbersForUtf8Name("名"u8, numbers, out more));
        Assert.False(more); Assert.Equal(new[] { 1, 2, 0 }, numbers);
        Assert.Equal(1, regex.CopyNumbersForUtf8Name("é"u8, numbers, out more)); Assert.Equal(3, numbers[0]);
        Assert.Equal(0, regex.CopyNumbersForUtf8Name("e\u0301"u8, numbers, out more)); Assert.False(more);
        Assert.Equal(0, regex.CopyNumbersForUtf8Name("0"u8, numbers, out more)); Assert.False(more);
        Assert.Equal(0, regex.CopyNumbersForUtf8Name("名"u8, [], out more)); Assert.True(more);
        var snapshot = (int[])numbers.Clone();
        Assert.Throws<ArgumentException>(() => regex.CopyNumbersForUtf8Name(new byte[] { (byte)'z', 0xff }, numbers, out _));
        Assert.Equal(snapshot, numbers);
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        Assert.Equal(cancellation.Token, Assert.Throws<OperationCanceledException>(() =>
            regex.CopyNumbersForUtf8Name("名"u8, numbers, out _, cancellation.Token)).CancellationToken);
        for (var i = 0; i < 16; i++) regex.CopyNumbersForUtf8Name("名"u8, numbers, out _);
        var allocated = GC.GetAllocatedBytesForCurrentThread();
        var checksum = 0;
        for (var i = 0; i < 64; i++) checksum += regex.CopyNumbersForUtf8Name("名"u8, numbers, out _);
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - allocated); Assert.Equal(128, checksum);
    }

    [Theory]
    [InlineData("literal", 0)]
    [InlineData("[abc]+", 0)]
    [InlineData("(a)(b)", 2)]
    [InlineData("(?J)(?<same>a)(?<same>b)", 2)]
    [InlineData("(?|(a)|(b))(c)", 2)]
    [InlineData("(?=(?<x>a))a", 1)]
    [InlineData("(?<x>a)(?&x)", 1)]
    [InlineData("(?n)(a)(?<x>b)", 1)]
    [InlineData("(?(DEFINE)(?<d>a))(?&d)", 1)]
    public void CaptureCountSurvivesExecutionSelectionAndBranchReset(string source, int count)
    {
        Assert.Equal(count, new Utf8Pcre2Regex(source).CaptureGroupCount);
        Assert.Equal(count, new Utf8Pcre2Regex(Encoding.UTF8.GetBytes(source)).CaptureGroupCount);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void CopiesOriginalSourceIntoCallerStorageAndPreservesCancellation(bool native)
    {
        var source = "(?#" + new string('x', 2043) + ")é😀\0";
        var encoded = Encoding.UTF8.GetBytes(source);
        var regex = native ? new Utf8Pcre2Regex(encoded): new Utf8Pcre2Regex(source);
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
}
