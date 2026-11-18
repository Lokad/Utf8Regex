using System.Buffers;
using System.Text;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8GroupNameCopyTests
{
    public static IEnumerable<object[]> Cases()
    {
        yield return ["", new[] { "0" }];
        yield return ["(a)(b)", new[] { "0", "1", "2" }];
        yield return ["(?<12>a)(b)(?<名>c)", new[] { "0", "1", "名", "12" }];
        yield return ["(?<名>a)(?<名>b)", new[] { "0", "名" }];
        yield return ["(?<é>a)(?<e\u0301>b)", new[] { "0", "é", "e\u0301" }];
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void CompactNamesPreserveSpellingAndCallerDestination(string pattern, string[] names)
    {
        var regex = new Utf8Regex(pattern);
        Assert.Equal(names.Length, regex.CaptureGroupCount + 1);
        for (var slot = 0; slot < names.Length; slot++)
        {
            var expected = Encoding.UTF8.GetBytes(names[slot]);
            foreach (var capacity in new[] { 0, expected.Length - 1, expected.Length, expected.Length + 1 }.Distinct())
            {
                var destination = Enumerable.Repeat((byte)0xa5, expected.Length + 3).ToArray();
                var unchanged = destination.ToArray();
                var status = regex.TryCopyGroupNameUtf8(slot, destination.AsSpan(1, capacity), out var required);
                Assert.Equal(expected.Length, required);
                Assert.Equal(capacity < required ? OperationStatus.DestinationTooSmall : OperationStatus.Done, status);
                if (status == OperationStatus.Done) expected.CopyTo(unchanged.AsSpan(1));
                Assert.Equal(unchanged, destination);
            }
        }
    }

    [Fact]
    public void BadSlotsAndPreCancellationPreserveDestination()
    {
        var regex = new Utf8Regex("(?<名>a)");
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
    public void CopyingNamesAllocatesNoIntermediateStorageAndOwnsNoOutputAlias()
    {
        var regex = new Utf8Regex("(?<名>a)");
        var destination = new byte[3];
        for (var i = 0; i < 16; i++) regex.TryCopyGroupNameUtf8(1, destination, out _);
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 64; i++) regex.TryCopyGroupNameUtf8(1, destination, out _);
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
        Assert.Equal("名"u8.ToArray(), destination);
        destination.AsSpan().Fill((byte)'x');
        Assert.Equal(OperationStatus.Done, regex.TryCopyGroupNameUtf8(1, destination, out var required));
        Assert.Equal(3, required);
        Assert.Equal("名"u8.ToArray(), destination);
    }
}
