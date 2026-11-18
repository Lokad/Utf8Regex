using System.Buffers;
using System.Text;
using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8NativeTemplateNoMatchTests
{
    [Fact]
    public void NativeGroupedMissAllocatesOnlyFinalBytes()
    {
        var text = string.Concat(Enumerable.Repeat("dog fox bird ", 512));
        var input = Encoding.UTF8.GetBytes(text);
        var regex = new Utf8Regex("^((cat))$", options: RegexOptions.Compiled | RegexOptions.CultureInvariant);
        for (var i = 0; i < 16; i++) Assert.Equal(input, regex.Replace(input, "$1-$2"u8));
        var before = GC.GetAllocatedBytesForCurrentThread();
        var output = regex.Replace(input, "$1-$2"u8);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(input, output);
        // Caller text materialization is outside this byte-output allocation check.
        Assert.InRange(allocated, input.Length, input.Length + 1024L);
    }

    [Theory]
    [InlineData("^((cat))$", "dog", "dog")]
    [InlineData("^((cat))$", "cat", "cat-cat")]
    [InlineData("(?m:^((cat))$)", "dog\ncat\ncat\ndog", "dog\ncat-cat\ncat-cat\ndog")]
    public void FirstAndSubsequentMatchesKeepTheirCaptures(string pattern, string text, string expected)
    {
        var regex = new Utf8Regex(pattern, options: RegexOptions.Compiled | RegexOptions.CultureInvariant);
        var input = Encoding.UTF8.GetBytes(text);
        var expectedBytes = Encoding.UTF8.GetBytes(expected);
        Assert.Equal(expectedBytes, regex.Replace(input, "$1-$2"u8));
        Assert.Equal(expectedBytes, regex.Replace(input, "$1-$2"u8));
        var destination = new byte[expectedBytes.Length];
        Assert.Equal(OperationStatus.Done, regex.TryReplace(input, "$1-$2"u8, destination, out var written));
        Assert.Equal(expectedBytes.Length, written);
        Assert.Equal(expectedBytes, destination);
        if (expectedBytes.Length > 0)
        {
            var shortDestination = Enumerable.Repeat((byte)0xa5, expectedBytes.Length - 1).ToArray();
            Assert.Equal(OperationStatus.DestinationTooSmall,
                regex.TryReplace(input, "$1-$2"u8, shortDestination, out written));
            Assert.Equal(0, written);
            Assert.All(shortDestination, value => Assert.Equal((byte)0xa5, value));
        }
    }
}
