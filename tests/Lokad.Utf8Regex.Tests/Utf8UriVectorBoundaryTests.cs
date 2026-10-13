using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8UriVectorBoundaryTests
{
    private const string Pattern = @"[\w]+://[^/\s?#]+[^\s?#]+(?:\?[^\s#]*)?(?:#[^\s]*)?";

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(17)] [InlineData(31)] [InlineData(32)] [InlineData(63)]
    public void UriVectorsAndTailsKeepWholeAndCountRanges(int offset)
    {
        var oracle = new Regex(Pattern, RegexOptions.CultureInvariant);
        var regex = new Utf8Regex(Pattern, RegexOptions.CultureInvariant);
        var compiled = new Utf8Regex(Pattern, RegexOptions.CultureInvariant | RegexOptions.Compiled);
        for (var length = 0; length <= 97; length++)
        {
            var text = "x://" + new string('a', length) + "?q#f";
            var bytes = Encoding.UTF8.GetBytes("padding" + text + "trailing-padding");
            var input = bytes.AsSpan(7, text.Length);
            // Vary the actual borrowed storage offset independently of URI length.
            var storage = new byte[offset + input.Length + 11];
            input.CopyTo(storage.AsSpan(offset));
            input = storage.AsSpan(offset, input.Length);
            var expected = oracle.Count(text);
            Assert.Equal(expected, Utf8AsciiUriTokenExecutor.CountAsciiUriTokens(input));
            Assert.Equal(expected, regex.Count(input));
            Assert.Equal(expected, compiled.Count(input));
            var whole = Utf8AsciiUriTokenExecutor.TryMatchWhole(input, out var matchedLength);
            Assert.Equal(expected == 1, whole);
            if (whole) Assert.Equal(input.Length, matchedLength);
        }
    }

    [Theory]
    [InlineData(0)] [InlineData(31)] [InlineData(32)] [InlineData(33)] [InlineData(63)] [InlineData(64)]
    public void EveryAsciiStopByteKeepsContinuationAndScalarFallback(int position)
    {
        var oracle = new Regex(Pattern, RegexOptions.CultureInvariant);
        var regex = new Utf8Regex(Pattern, RegexOptions.CultureInvariant);
        var finite = new Utf8Regex(Pattern, RegexOptions.CultureInvariant, Timeout.InfiniteTimeSpan,
            new() { MaxWorkSteps = 100000, MaxDepth = 8192, MaxWorkingBytes = 1048576, MaxResultBytes = 1048576 });
        for (var value = 0; value < 128; value++)
        {
            var text = "x://aa" + new string('b', position) + (char)value + "tail?query#fragment y://zz";
            var input = Encoding.UTF8.GetBytes(text);
            var expected = oracle.Count(text);
            Assert.Equal(expected, Utf8AsciiUriTokenExecutor.CountAsciiUriTokens(input));
            Assert.Equal(expected, regex.Count(input));
            Assert.Equal(expected, finite.Count(input));
        }
        Assert.Equal(1, regex.Count(Encoding.UTF8.GetBytes("x://aa" + new string('b', position) + "😀tail?é#😀")));
    }
}
