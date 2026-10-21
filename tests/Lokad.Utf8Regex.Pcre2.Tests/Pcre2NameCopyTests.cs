namespace Lokad.Utf8Regex.Pcre2.Tests;

[Collection(Pcre2AllocationTestCollection.Name)]
public sealed class Pcre2NameCopyTests
{
    [Theory]
    [InlineData(0, "n")] [InlineData(1, "n")] [InlineData(2, "n")] [InlineData(3, "n")]
    [InlineData(0, "missing")] [InlineData(3, "missing")] [InlineData(3, null)]
    public void BothNameCopiesPreserveOrderTruncationAndUnusedDestination(int capacity, string? name)
    {
        var regex = new Utf8Pcre2Regex("(?J)(?<n>a)|(?<n>b)");
        var match = regex.MatchDetailed("b"u8);
        Span<int> storage = stackalloc int[3];
        for (var context = 0; context < 2; context++)
        {
            storage.Fill(-1);
            bool more;
            var copied = context == 0
                ? regex.CopyNumbersForName(name!, storage[..capacity], out more)
                : match.CopyNumbersForName(name!, storage[..capacity], out more);
            var expected = name == "n" ? 2 : 0;
            Assert.Equal(Math.Min(capacity, expected), copied);
            Assert.Equal(expected > capacity, more);
            for (var index = 0; index < storage.Length; index++)
                Assert.Equal(index < copied ? index + 1 : -1, storage[index]);
        }
        Assert.Equal("b", match.GetGroup(2).GetValueString());
    }

    [Fact]
    public void NameCopiesNeedNoTemporaryCollections()
    {
        var regex = new Utf8Pcre2Regex("(?J)" + string.Join('|', Enumerable.Repeat("(?<n>a)", 64)));
        var match = regex.MatchDetailed("a"u8);
        Span<int> numbers = stackalloc int[1];
        for (var i = 0; i < 1000; i++)
        {
            regex.CopyNumbersForName("n", numbers, out _);
            match.CopyNumbersForName("n", numbers, out _);
        }
        var allocated = GC.GetAllocatedBytesForCurrentThread();
        var copied = 0;
        for (var i = 0; i < 100; i++)
        {
            copied += regex.CopyNumbersForName("n", numbers, out _);
            copied += match.CopyNumbersForName("n", numbers, out _);
        }
        allocated = GC.GetAllocatedBytesForCurrentThread() - allocated;
        Assert.Equal(200, copied);
        Assert.Equal(0, allocated);
    }
}
