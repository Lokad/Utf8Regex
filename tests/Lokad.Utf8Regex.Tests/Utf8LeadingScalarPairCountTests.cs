using System.Text;
using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8LeadingScalarPairCountTests
{
    [Theory]
    [InlineData("夏a", "夏a夏a")]
    [InlineData("夏a", "䤏a夏a䤏a")]
    [InlineData("夏·", "夏·夏夏·")]
    [InlineData("夏夏·", "夏夏夏·夏夏·")]
    [InlineData("夏a夏a", "夏a夏a夏a")]
    [InlineData("ࠀa", "ࠀaࠀa")]
    [InlineData("한a", "한a한a")]
    [InlineData("😀a", "😀a😀a")]
    [InlineData("Шa", "ШaШa")]
    [InlineData("夏洛", "夏洛夏洛")]
    public void MixedLiteralsPreserveExactScalarNonoverlapAndModes(string literal, string text)
    {
        foreach (var option in new[] { RegexOptions.None, RegexOptions.Compiled,
            RegexOptions.RightToLeft, RegexOptions.NonBacktracking })
        {
            var pattern = Regex.Escape(literal);
            var expected = new Regex(pattern, option).Count(text);
            var actual = new Utf8Regex(pattern, option).Count(Encoding.UTF8.GetBytes(text));
            Assert.Equal(expected, actual);
        }
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(31)] [InlineData(32)]
    [InlineData(63)] [InlineData(64)] [InlineData(65)]
    public void OffsetsAndTailsKeepFullVerificationAndStrictWholeSubjectValidation(int offset)
    {
        const string literal = "夏a夏·";
        var regex = new Utf8Regex(literal);
        var input = Encoding.UTF8.GetBytes(new string('x', offset) + literal + literal);
        byte[] storage = [0xff, .. input, 0xff];
        Assert.Equal(2, regex.Count(storage.AsSpan(1, input.Length)));
        Assert.Equal(1, regex.Count(input.AsSpan(0, input.Length - 2)));
        Assert.Throws<ArgumentException>(() => regex.Count(input.AsSpan(0, input.Length - 1)));
        byte[] malformed = [.. input, 0xff];
        Assert.Throws<ArgumentException>(() => regex.Count(malformed));
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        Assert.Equal(cancelled.Token,
            Assert.Throws<OperationCanceledException>(() => regex.Count(input, cancelled.Token)).CancellationToken);
    }

    [Fact]
    public void FrequentFalseContinuationPairsDoNotHideLaterOrOverlappingMatches()
    {
        var text = string.Concat(Enumerable.Repeat("䤏a", 4096)) + "夏a夏a";
        var input = Encoding.UTF8.GetBytes(text);
        var regex = new Utf8Regex("夏a");
        Assert.Equal(2, regex.Count(input));
        var finite = new Utf8Regex("夏a", RegexOptions.None, Timeout.InfiniteTimeSpan,
            new Utf8ResourcePolicy { MaxWorkSteps = 1_000_000, MaxDepth = 256,
                MaxWorkingBytes = 1 << 20, MaxResultBytes = 1 << 20 });
        using var live = new CancellationTokenSource();
        Assert.Equal(2, finite.Count(input, live.Token));
        Assert.Equal(2, regex.Count(input, live.Token));
    }
}
