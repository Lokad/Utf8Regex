namespace Lokad.Utf8Regex.Pcre2.Tests;

[Collection(Pcre2AllocationTestCollection.Name)]
public sealed class Pcre2NameCopyTests
{
    [Theory]
    [InlineData(0, "n")]
    [InlineData(1, "n")]
    [InlineData(2, "n")]
    [InlineData(3, "n")]
    [InlineData(0, "missing")]
    [InlineData(3, "missing")]
    [InlineData(3, null)]
    public void Utf8NameCopyPreservesOrderTruncationAndUnusedDestination(int capacity, string? name)
    {
        var regex = new Utf8Pcre2Regex("(?J)(?<n>a)|(?<n>b)");
        var match = regex.Probe("b"u8, Pcre2PartialMode.Soft).GetMatch();
        var nameUtf8 = System.Text.Encoding.UTF8.GetBytes(name ?? string.Empty);
        Span<int> storage = stackalloc int[3];
        storage.Fill(-1);
        var copied = regex.CopyNumbersForUtf8Name(nameUtf8, storage[..capacity], out var more);
        var expected = name == "n" ? 2 : 0;
        Assert.Equal(Math.Min(capacity, expected), copied);
        Assert.Equal(expected > capacity, more);
        for (var index = 0; index < storage.Length; index++)
            Assert.Equal(index < copied ? index + 1 : -1, storage[index]);
        Assert.Equal("b", System.Text.Encoding.UTF8.GetString(match.GetGroup(2).GetValueBytes()));
    }

    [Fact]
    public void NameCopiesNeedNoTemporaryCollections()
    {
        var regex = new Utf8Pcre2Regex("(?J)" + string.Join('|', Enumerable.Repeat("(?<n>a)", 64)));
        Span<int> numbers = stackalloc int[1];
        for (var i = 0; i < 1000; i++)
        {
            regex.CopyNumbersForUtf8Name("n"u8, numbers, out _);
        }
        // Warm the measured batch itself as well as its production callees.
        // Keep setup/assertions outside the exact allocation window.
        MeasureCopies(regex, numbers, out _);
        var allocated = MeasureCopies(regex, numbers, out var copied);
        Assert.Equal(100, copied);
        Assert.Equal(0, allocated);
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static long MeasureCopies(Utf8Pcre2Regex regex, Span<int> numbers, out int copied)
    {
        copied = 0;
        var allocated = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 100; i++)
        {
            copied += regex.CopyNumbersForUtf8Name("n"u8, numbers, out _);
        }
        return GC.GetAllocatedBytesForCurrentThread() - allocated;
    }
}
