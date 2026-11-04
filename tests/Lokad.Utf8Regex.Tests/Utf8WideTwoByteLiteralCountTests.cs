using System.Text;
using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8WideTwoByteLiteralCountTests
{
    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(31)] [InlineData(32)]
    [InlineData(33)] [InlineData(63)] [InlineData(64)] [InlineData(65)]
    public void ThresholdOffsetsAndTailsPreserveExactCounts(int offset)
    {
        const string literal = "Шерлок Холмс";
        foreach (var tail in new[] { -1, 0, 1, 31, 32, 63, 64, 65 })
        {
            var padding = 128 * 1024 - 2 * Encoding.UTF8.GetByteCount(literal) + tail;
            var input = Encoding.UTF8.GetBytes(new string('x', offset) + literal + new string('x', padding) + literal);
            byte[] storage = [0xff, .. input, 0xff];
            foreach (var option in new[] { RegexOptions.None, RegexOptions.Compiled })
            {
                var regex = new Utf8Regex(literal, option);
                Assert.Equal(2, regex.Count(storage.AsSpan(1, input.Length)));
                Assert.Equal(1, regex.Count(input.AsSpan(0, input.Length - 2)));
                Assert.Throws<ArgumentException>(() => regex.Count(input.AsSpan(0, input.Length - 1)));
            }
        }
    }

    [Theory]
    [InlineData("Шяя", "Шыя Шяя ")]
    [InlineData("éé", "éééé ")]
    [InlineData("éabc", "éabd éabc ")]
    [InlineData("Шa😀", "Шa😁 Шa😀 ")]
    [InlineData("Шабв", "Шабг Шабв ")]
    public void DenseAndFalseCorrelationsKeepCompleteLiteralVerification(string literal, string unit)
    {
        var text = string.Concat(Enumerable.Repeat(unit, 32768));
        var input = Encoding.UTF8.GetBytes(text);
        var expected = new Regex(Regex.Escape(literal)).Count(text);
        foreach (var option in new[] { RegexOptions.None, RegexOptions.Compiled, RegexOptions.NonBacktracking })
            Assert.Equal(expected, new Utf8Regex(Regex.Escape(literal), option).Count(input));
    }

    [Fact]
    public void LongNeedleConsumesMultipleCandidateBlocksWithoutRecounting()
    {
        var literal = string.Concat(Enumerable.Repeat("Ша", 40)) + "x";
        var text = string.Concat(Enumerable.Repeat(literal, 1024)) + literal[..^1];
        var input = Encoding.UTF8.GetBytes(text);
        foreach (var option in new[] { RegexOptions.None, RegexOptions.Compiled })
            Assert.Equal(1024, new Utf8Regex(literal, option).Count(input));
    }

    [Fact]
    public void ControlledCallsRetainWholeValidationAndOriginalCancellation()
    {
        var input = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat("Шя ", 32768)));
        var regex = new Utf8Regex("Шя", RegexOptions.None, Timeout.InfiniteTimeSpan,
            new Utf8ResourcePolicy { MaxWorkSteps = 50_000_000, MaxDepth = 256,
                MaxWorkingBytes = 1 << 20, MaxResultBytes = 1024 });
        using var live = new CancellationTokenSource();
        Assert.Equal(32768, regex.Count(input, live.Token));
        byte[] malformed = [.. input, 0xff];
        Assert.Throws<ArgumentException>(() => regex.Count(malformed, live.Token));
        live.Cancel();
        Assert.Equal(live.Token,
            Assert.Throws<OperationCanceledException>(() => regex.Count(input, live.Token)).CancellationToken);
        var unlimited = new Utf8Regex("Шя");
        Assert.Equal(live.Token,
            Assert.Throws<OperationCanceledException>(() => unlimited.Count(input, live.Token)).CancellationToken);
    }
}
