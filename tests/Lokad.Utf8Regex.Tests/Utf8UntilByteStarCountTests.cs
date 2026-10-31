using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Diagnostics;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Tests;

[Collection(Utf8RuntimeAllocationCollection.Name)]
public sealed class Utf8UntilByteStarCountTests
{
    private static readonly Utf8ResourcePolicy Policy = new()
    { MaxWorkSteps = 1_000_000, MaxDepth = 1, MaxWorkingBytes = 1, MaxResultBytes = 1 };

    [Theory]
    [InlineData(RegexOptions.None)] [InlineData(RegexOptions.Compiled)]
    public void PublicCountUsesTheProvedCounterWithOneByteStorageBudgets(RegexOptions options)
    {
        const string text = "alpha\n\nété😀\n\0tail";
        var regex = new Utf8Regex(@"[^\n]*", options, Timeout.InfiniteTimeSpan, Policy);
        using var live = new CancellationTokenSource();
        var diagnostics = Utf8SearchDiagnosticsSession.Start();
        try
        {
            Assert.Equal(Regex.Count(text, @"[^\n]*"), regex.Count(Encoding.UTF8.GetBytes(text), live.Token));
            Assert.Equal(Utf8ExecutionRoute.FallbackDirectAsciiUntilByteStar, diagnostics.ExecutionRoute);
        }
        finally { diagnostics.Complete(); }
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(254)] [InlineData(255)]
    [InlineData(256)] [InlineData(257)] [InlineData(511)] [InlineData(512)]
    public void RunAndDelimiterBoundariesDoNotAddChunkMatches(int length)
    {
        foreach (var terminator in new[] { '\n', '\0', 'x', '\x7f' })
        {
            var pattern = "[^\\u" + ((int)terminator).ToString("X4") + "]*";
            foreach (var text in new[] { "", new string(terminator, length), new string('a', length),
                new string('a', length) + terminator, new string('a', length) + "😀é" + terminator + "été😀",
                terminator + new string('a', length) + terminator + terminator })
            {
                var bytes = Encoding.UTF8.GetBytes(text);
                byte[] backing = [0xff, .. bytes, 0xff];
                foreach (var options in new[] { RegexOptions.None, RegexOptions.Compiled })
                {
                    var regex = new Utf8Regex(pattern, options, Timeout.InfiniteTimeSpan, Policy);
                    Assert.Equal(Regex.Count(text, pattern), regex.Count(backing.AsSpan(1, bytes.Length)));
                }
            }
        }
    }

    [Fact]
    public void SeededMixedScalarCountsMatchIndependentRunOracle()
    {
        var random = new Random(71821);
        var regex = new Utf8Regex(@"[^\n]*", RegexOptions.Compiled);
        string[] scalars = ["a", "é", "😀", "\n", "\0", "夏", "\r"];
        for (var sample = 0; sample < 80; sample++)
        {
            var text = string.Concat(Enumerable.Range(0, random.Next(1100)).Select(_ => scalars[random.Next(scalars.Length)]));
            var expected = 1; var run = false;
            foreach (var scalar in text.EnumerateRunes())
            {
                if (scalar.Value == '\n') { if (run) expected++; expected++; run = false; }
                else run = true;
            }
            if (run) expected++;
            Assert.Equal(expected, regex.Count(Encoding.UTF8.GetBytes(text)));
        }
    }

    [Theory]
    [InlineData(RegexOptions.RightToLeft)] [InlineData(RegexOptions.NonBacktracking)]
    [InlineData(RegexOptions.IgnoreCase)]
    public void ExcludedModesRetainTheirResults(RegexOptions options)
    {
        const string text = "a😀\n\nété\nx";
        var regex = new Utf8Regex(@"[^\n]*", options);
        Assert.Equal(Regex.Count(text, @"[^\n]*", options), regex.Count(Encoding.UTF8.GetBytes(text)));
    }

    [Fact]
    public void DetailedAndCapturedRangesRetainScalarProgression()
    {
        var input = Encoding.UTF8.GetBytes("😀\nété\n");
        var regex = new Utf8Regex(@"(?<line>[^\n]*)");
        Assert.Equal(5, regex.Count(input));
        var match = regex.MatchDetailed(input);
        Assert.Equal(4, match.LengthInBytes);
        Assert.Equal(4, match.GetGroup("line").LengthInBytes);
        Assert.Equal(5, regex.Count(input));
        Assert.Equal(4, match.GetGroup("line").LengthInBytes);
    }

    [Theory]
    [InlineData("")] [InlineData("abc")] [InlineData("\n\n")]
    public void FiniteWorkRefusalCannotPublishPartialCount(string text)
    {
        var input = Encoding.UTF8.GetBytes(text); var denied = false; var admitted = false;
        for (ulong work = 1; work < 100; work++)
        {
            var regex = new Utf8Regex(@"[^\n]*", RegexOptions.None, Timeout.InfiniteTimeSpan,
                Policy with { MaxWorkSteps = work });
            try { Assert.Equal(Regex.Count(text, @"[^\n]*"), regex.Count(input)); admitted = true; break; }
            catch (Utf8ResourceLimitExceededException error)
            { Assert.Equal(Utf8ResourceLimitKind.ExecutionWorkSteps, error.ResourceKind); denied = true; }
        }
        Assert.True(denied); Assert.True(admitted);
    }

    [Fact]
    public void InvalidTailAndOriginalEntryCancellationRemainStrict()
    {
        var regex = new Utf8Regex(@"[^\n]*");
        byte[][] invalid = [[.. "hit\n"u8, 0xff], [.. "\n\n"u8, 0xf0, 0x9f, 0x98]];
        foreach (var input in invalid)
        {
            Assert.Throws<ArgumentException>(() => regex.Count(input));
            using var token = new CancellationTokenSource(); token.Cancel();
            Assert.Equal(token.Token, Assert.Throws<OperationCanceledException>(() => regex.Count(input, token.Token)).CancellationToken);
        }
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void BoundedCounterObservesInterruptionAfterValidation(bool expire)
    {
        long ticks = 0; var entered = false;
        using var token = new CancellationTokenSource();
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
        var regex = new Utf8Regex(@"[^\n]*", RegexOptions.None, TimeSpan.FromMilliseconds(50), Policy);
        var diagnostics = Utf8SearchDiagnosticsSession.Start(steps =>
        {
            if (steps != 64) return;
            Assert.Equal(Utf8ExecutionRoute.FallbackDirectAsciiUntilByteStar, Utf8SearchDiagnosticsSession.Current!.ExecutionRoute);
            entered = true;
            if (expire) ticks = 100; else token.Cancel();
        });
        try
        {
            var input = Encoding.ASCII.GetBytes(new string('a', 32768));
            if (expire) Assert.Throws<RegexMatchTimeoutException>(() => regex.Count(input, token.Token));
            else Assert.Equal(token.Token, Assert.Throws<OperationCanceledException>(() => regex.Count(input, token.Token)).CancellationToken);
            Assert.True(entered);
        }
        finally { diagnostics.Complete(); }
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void FinalPublicationObservesOriginalTokenAndDeadline(bool expire)
    {
        long ticks = 0; var reads = 0; var trigger = int.MaxValue;
        using var token = new CancellationTokenSource();
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() =>
        { if (++reads == trigger) { if (expire) ticks = 100; else token.Cancel(); } return ticks; });
        var regex = new Utf8Regex(@"[^\n]*", RegexOptions.Compiled, TimeSpan.FromMilliseconds(50), Policy);
        Assert.Equal(4, regex.Count("a\nb"u8, token.Token));
        trigger = reads; reads = 0;
        if (expire) Assert.Equal(regex.MatchTimeout, Assert.Throws<RegexMatchTimeoutException>(() => regex.Count("a\nb"u8, token.Token)).MatchTimeout);
        else Assert.Equal(token.Token, Assert.Throws<OperationCanceledException>(() => regex.Count("a\nb"u8, token.Token)).CancellationToken);
    }
}
