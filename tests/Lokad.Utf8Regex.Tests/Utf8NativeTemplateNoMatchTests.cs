using System.Buffers;
using System.Text;
using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8NativeTemplateNoMatchTests
{
    [Fact]
    public void NativeGroupedMissAvoidsUnusedSubjectSizedTextStaging()
    {
        var text = string.Concat(Enumerable.Repeat("dog fox bird ", 512));
        var input = Encoding.UTF8.GetBytes(text);
        var regex = new Utf8Regex("^((cat))$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
        for (var i = 0; i < 16; i++) Assert.Equal(text, regex.ReplaceToString(input, "$1-$2"));
        var before = GC.GetAllocatedBytesForCurrentThread();
        var output = regex.ReplaceToString(input, "$1-$2");
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(text, output);
        // One owned UTF16 result is necessary; a decoded subject plus an unused
        // subject-sized StringBuilder exceeds this deliberately loose guard.
        Assert.InRange(allocated, 0, 3L * input.Length + 1024);
    }

    [Theory]
    [InlineData("^((cat))$", "dog", "dog")]
    [InlineData("^((cat))$", "cat", "cat-cat")]
    [InlineData("(?m:^((cat))$)", "dog\ncat\ncat\ndog", "dog\ncat-cat\ncat-cat\ndog")]
    public void FirstAndSubsequentMatchesKeepTheirCaptures(string pattern, string text, string expected)
    {
        var regex = new Utf8Regex(pattern, RegexOptions.Compiled | RegexOptions.CultureInvariant);
        var input = Encoding.UTF8.GetBytes(text);
        var expectedBytes = Encoding.UTF8.GetBytes(expected);
        Assert.Equal(expected, regex.ReplaceToString(input, "$1-$2"));
        Assert.Equal(expectedBytes, regex.Replace(input, "$1-$2"));
        Assert.Equal(expectedBytes, regex.Replace(input, "$1-$2"u8));
        var destination = new byte[expectedBytes.Length];
        Assert.Equal(OperationStatus.Done, regex.TryReplace(input, "$1-$2", destination, out var written));
        Assert.Equal(expectedBytes.Length, written);
        Assert.Equal(expectedBytes, destination);
        if (expectedBytes.Length > 0)
        {
            var shortDestination = Enumerable.Repeat((byte)0xa5, expectedBytes.Length - 1).ToArray();
            Assert.Equal(OperationStatus.DestinationTooSmall,
                regex.TryReplace(input, "$1-$2", shortDestination, out written));
            Assert.Equal(0, written);
            Assert.All(shortDestination, value => Assert.Equal((byte)0xa5, value));
        }
    }

    [Fact]
    public void InvalidUtf8CannotReturnThroughTheNoMatchArm()
    {
        var regex = new Utf8Regex("^((cat))$", RegexOptions.Compiled);
        var input = new byte[] { (byte)'d', (byte)'o', (byte)'g', 0xff };
        Assert.Throws<ArgumentException>(() => regex.ReplaceToString(input, "$1-$2"));
        Assert.Throws<ArgumentException>(() => regex.Replace(input, "$1-$2"));
    }
}
