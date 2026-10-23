using System.Text;

namespace Lokad.Utf8Regex.PythonRe.Tests;

[Collection(PythonReGlobalSettingsCollection.Name)]
public sealed class PythonReByteOffsetCollectionTests
{
    [Theory]
    [InlineData("foo", "é😀")]
    [InlineData("foo|bar", "é😀")]
    [InlineData("foo", "ascii ")]
    [InlineData("foo|bar", "ascii ")]
    public void NativeCollectionsPreserveByteAndDerivedCoordinates(string pattern, string prefix)
    {
        var input = Encoding.UTF8.GetBytes(prefix + " foo bar");
        var start = Encoding.UTF8.GetByteCount(prefix);
        var expected = pattern == "foo" ? new[] { "foo" } : new[] { "foo", "bar" };
        using var live = new CancellationTokenSource();
        foreach (var mode in Enumerable.Range(0, 3))
        {
            var regex = Create(pattern, mode == 2);
            Assert.True(regex.DebugUsesUtf8RegexBackend);
            var token = mode == 1 ? live.Token : default;
            var matches = regex.FindAll(input, start, token);
            Assert.Equal(expected, matches.Select(static match => match.ValueText));
            Assert.Equal(start + 1, matches[0].StartOffsetInBytes);
            Assert.Equal(prefix.Length + 1, matches[0].StartOffsetInUtf16);
            Assert.Equal(start + 4, matches[0].EndOffsetInBytes);
            Assert.Equal(prefix.Length + 4, matches[0].EndOffsetInUtf16);
            Assert.Equal(expected, regex.FindAllToStrings(input, start, token).ScalarValues);
            Assert.Equal(expected, regex.FindAllToUtf8(input, start, token).ScalarValues.Select(Encoding.UTF8.GetString));
            _ = regex.FindAll("later foo"u8);
            Assert.Equal("foo", matches[0].ValueText);
        }
    }

    [Theory]
    [InlineData("foo")]
    [InlineData("foo|bar")]
    public void NativeCollectionStillValidatesBoundariesAndEntireSubject(string pattern)
    {
        using var canceled = new CancellationTokenSource();
        canceled.Cancel();
        foreach (var finite in new[] { false, true })
        {
            var regex = Create(pattern, finite);
            for (var interior = 1; interior < 4; interior++)
            {
                var offset = interior;
                Assert.Throws<ArgumentOutOfRangeException>(() => regex.FindAllToUtf8("😀 foo"u8, offset));
            }
            Assert.Empty(regex.FindAllToUtf8("😀 foo"u8, "😀 foo"u8.Length).ScalarValues);
            Assert.Throws<ArgumentException>(() => regex.FindAllToUtf8(new byte[] { 0xc3, 0xa9, 32, 102, 111, 111, 0xff }, 2));
            var failure = Assert.Throws<OperationCanceledException>(() => regex.FindAllToUtf8("😀 foo"u8, 4, canceled.Token));
            Assert.Equal(canceled.Token, failure.CancellationToken);
        }
        var limited = new Utf8PythonRegex(pattern, default, Timeout.InfiniteTimeSpan,
            new Utf8ResourcePolicy { MaxWorkSteps = 1 });
        Assert.Throws<Utf8ResourceLimitExceededException>(() => limited.FindAllToUtf8("😀 foo"u8, 4));
    }

    [Theory]
    [InlineData("foo")]
    [InlineData("foo|bar")]
    public void NativeByteOffsetCollectionDoesNotAllocateASubjectSizedMap(string pattern)
    {
        var regex = Create(pattern, false);
        var small = Encoding.UTF8.GetBytes("é" + new string('a', 63) + " foo");
        var large = Encoding.UTF8.GetBytes("é" + new string('a', 65_535) + " foo");
        for (var i = 0; i < 16; i++)
        {
            _ = regex.FindAllToUtf8(small, small.Length - 4);
            _ = regex.FindAllToUtf8(large, large.Length - 4);
        }
        var smallBytes = Measure(small);
        var largeBytes = Measure(large);
        // One result has the same payload; allow incidental metadata variation,
        // while rejecting a UTF-16 boundary map proportional to the whole input.
        Assert.InRange(largeBytes, 0, smallBytes + 4096);

        long Measure(byte[] input)
        {
            var before = GC.GetAllocatedBytesForCurrentThread();
            var values = regex.FindAllToUtf8(input, input.Length - 4);
            var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.Equal("foo"u8.ToArray(), Assert.Single(values.ScalarValues));
            return allocated;
        }
    }

    private static Utf8PythonRegex Create(string pattern, bool finite) => new(pattern, default,
        Timeout.InfiniteTimeSpan, finite ? new Utf8ResourcePolicy
        {
            MaxWorkSteps = 1_000_000, MaxDepth = 1024,
            MaxWorkingBytes = 16 << 20, MaxResultBytes = 16 << 20,
        } : default);
}
