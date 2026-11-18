namespace Lokad.Utf8Regex.Pcre2.Tests;

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
}
