using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Diagnostics;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Input;

namespace Lokad.Utf8Regex.Tests;

[Collection(Utf8RuntimeAllocationCollection.Name)]
public sealed class Utf8UriExistenceTests
{
    private const string Pattern = @"[\w]+://[^/\s?#]+[^\s?#]+(?:\?[^\s#]*)?(?:#[^\s]*)?";

    [Theory]
    [InlineData(RegexOptions.None)]
    [InlineData(RegexOptions.Compiled)]
    public void ExistenceRetainsScalarResultsAndOffsetContext(RegexOptions options)
    {
        var regex = new Utf8Regex(Pattern, options: options);
        var captured = new Utf8Regex("(?<uri>" + Pattern + ")", options: options);
        foreach (var text in new[] { "", "x://a?query", "x://host?query", "x://😀", "𐐀://😀😀", "é://ab", "١://ab",
            "x://😀/", "x:///😀😀", "😀://ab", "://ab é://ab", "x://a x://ab", "x:///b x://?b", "x:///ab x://a/",
            "://a" + new string(' ', 2041) + "𐐀://ab", "x://ab?#tail x://cd" })
        {
            var input = Encoding.UTF8.GetBytes(text);
            Assert.Equal(captured.IsMatch(input), regex.IsMatch(input));
            Assert.Equal(captured.Count(input), regex.Count(input));
            var match = Utf8MatchSequence.First(regex.CreateMatchCursor(input));
            Assert.Equal(match.Success, regex.IsMatch(input));
            if (match.Success)
                Assert.Equal(System.Text.Encoding.UTF8.GetString(Utf8MatchSequence.First(captured.CreateMatchCursor(input)).GetGroupMemory(captured.GroupNumberFromUtf8Name("uri"u8)).Span), System.Text.Encoding.UTF8.GetString(match.GetGroupMemory(0).Span));
        }
        Assert.False(regex.IsMatch("x://😀"u8));
        Assert.True(regex.IsMatch("𐐀://😀😀"u8));
        Assert.True(regex.Match("😀x://ab"u8, 4, []).Success);
        Assert.False(regex.Match("😀x://ab"u8, 5, []).Success);
        Assert.Throws<ArgumentOutOfRangeException>(() => regex.Match("😀x://ab"u8, 1, []).Success);
    }

    [Theory]
    [InlineData(@"[\w]{2,}://ab", "x://ab", "𐐀y://ab")]
    [InlineData(@"[\w:]+://ab", "://ab", "x:://ab")]
    [InlineData(@"(?<scheme>[\w]+)://\k<scheme>", "xy://x", "xy://xy")]
    [InlineData(Pattern + "!$", "x://ab", "x://ab!")]
    public void DelimiterSearchPreservesUnprovedRepeatAndCaptureRequirements(string pattern, string miss, string hit)
    {
        using var regex = new Utf8Regex(pattern, RegexOptions.Compiled);
        Assert.False(regex.IsMatch(Encoding.UTF8.GetBytes(miss)));
        Assert.True(regex.IsMatch(Encoding.UTF8.GetBytes(hit)));
    }

    [Fact]
    public void GreedyQueryAndFragmentRangesNeedNoRetryStorage()
    {
        using var source = new CancellationTokenSource();
        using var regex = Limited(4096, working: 1);
        var input = "https://écho/?id=😀#début fin"u8;
        var expected = new Utf8CaptureRange(0, "https://écho/?id=😀#début"u8.Length);
        Assert.Equal(expected, regex.Match(input, cancellationToken: source.Token));
        Assert.Equal(1, regex.Count(input, source.Token));

        const string record = "://path x://a?empty 𐐀://é/b?query=😀#tag x://ab y://cd#frag\n";
        var document = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat(record, 20)));
        Assert.Equal(60, regex.Count(document, source.Token));
    }

    [Fact]
    public void FiniteExistenceRetainsFundedAdmissionAndScalarSemantics()
    {
        using var cancellation = new CancellationTokenSource();
        using var tightWorking = Limited(128, working: 1);
        Assert.True(tightWorking.IsMatch("https://atlas.example.org/reports/export?id=42"u8, cancellation.Token));
        Assert.True(tightWorking.IsMatch("é://hôte/résumé#section"u8, cancellation.Token));
        Assert.False(tightWorking.IsMatch("x://😀"u8, cancellation.Token));
        Assert.True(Limited(100000).IsMatch("𐐀://😀😀"u8));
        Assert.False(Limited(100000).IsMatch("x://😀"u8));
        ulong firstSuccess = 0;
        for (ulong cap = 1; cap <= 512; cap++)
        {
            try
            {
                Assert.True(Limited(cap).IsMatch("x://ab"u8));
                if (firstSuccess == 0) firstSuccess = cap;
            }
            catch (Utf8ResourceLimitExceededException error)
            {
                Assert.Equal(0UL, firstSuccess);
                Assert.Equal(Utf8ResourceLimitKind.ExecutionWorkSteps, error.ResourceKind);
                Assert.Equal(cap, error.Limit);
                Assert.True(error.Requested > error.Limit);
            }
        }
        Assert.InRange(firstSuccess, 2UL, 512UL);
        var lateMalformed = Encoding.UTF8.GetBytes(new string('x', (1 << 20))).Append((byte)255).ToArray();
        Assert.Equal(Utf8ResourceLimitKind.ExecutionWorkSteps,
            Assert.Throws<Utf8ResourceLimitExceededException>(() => Limited(1).IsMatch(lateMalformed)).ResourceKind);
        foreach (var options in new[] { RegexOptions.RightToLeft, RegexOptions.NonBacktracking, RegexOptions.IgnoreCase })
            Assert.True(Limited(100000, options).IsMatch("x://ab"u8));
    }

    [Fact]
    public void LongUriSchemeNeedsOnlyItsLastScalarForExistence()
    {
        using var source = new CancellationTokenSource();
        using var limited = Limited(32, RegexOptions.Compiled, working: 1);
        using var ordinary = new Utf8Regex(Pattern, RegexOptions.Compiled);
        foreach (var tip in new[] { "", "𐐀" })
        {
            var input = Encoding.UTF8.GetBytes(new string('w', 4096) + tip + "://é/");
            Assert.True(limited.IsMatch(input, source.Token));
            Assert.Equal(new Utf8CaptureRange(0, input.Length), ordinary.Match(input));
        }
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(0, true)]
    [InlineData(1, false)]
    [InlineData(1, true)]
    [InlineData(2, false)]
    [InlineData(2, true)]
    [InlineData(3, false)]
    [InlineData(3, true)]
    public void OriginalInterruptionReachesDelimiterSchemeBodyAndFailedCandidateLoops(int phase, bool expire)
    {
        var text = phase switch
        {
            0 => new string('x', (1 << 20)),
            1 => new string('x', (1 << 20)) + "://ab",
            2 => "x://" + new string('a', (1 << 20)),
            _ => string.Concat(Enumerable.Repeat("x://a ", 200000)),
        };
        var input = Encoding.UTF8.GetBytes(text);
        const ulong threshold = 64;
        using var cancellation = new CancellationTokenSource();
        long ticks = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
        var regex = Limited(1000000, timeout: expire ? TimeSpan.FromMilliseconds(20) : null);
        var entered = false;
        var session = Utf8SearchDiagnosticsSession.Start(steps =>
        {
            if (steps < threshold) return;
            entered = true;
            if (expire) ticks = 50; else cancellation.Cancel();
        });
        Exception? failure = null;
        try
        {
            // The body only needs two scalars for existence. A range must
            // still find its greedy end and observe interruption during the run.
            if (phase == 2) regex.Match(input, cancellationToken: cancellation.Token);
            else regex.IsMatch(input, cancellation.Token);
        }
        catch (Exception error) { failure = error; }
        finally { session.Complete(); }
        Assert.True(entered);
        if (expire) Assert.IsType<RegexMatchTimeoutException>(failure);
        else Assert.Equal(cancellation.Token, Assert.IsType<OperationCanceledException>(failure).CancellationToken);
        using var canceled = new CancellationTokenSource();
        canceled.Cancel();
        Assert.Equal(canceled.Token,
            Assert.Throws<OperationCanceledException>(() => regex.IsMatch(input, canceled.Token)).CancellationToken);
    }

    [Fact]
    public void LastDeadlinePollCannotPublishAfterOriginalTokenCancellation()
    {
        var reads = 0;
        var cancelAt = int.MaxValue;
        using var cancellation = new CancellationTokenSource();
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() =>
        {
            if (++reads == cancelAt) cancellation.Cancel();
            return 0;
        });
        var regex = Limited(100000, timeout: TimeSpan.FromSeconds(1));
        Assert.True(regex.IsMatch("x://ab"u8, cancellation.Token));
        Assert.True(reads > 1);
        cancelAt = reads;
        reads = 0;
        Assert.Equal(cancellation.Token,
            Assert.Throws<OperationCanceledException>(() => regex.IsMatch("x://ab"u8, cancellation.Token)).CancellationToken);
        Assert.Equal(cancelAt, reads);
    }

    private static Utf8Regex Limited(ulong work, RegexOptions options = RegexOptions.None, TimeSpan? timeout = null,
        ulong working = 8 << 20)
        => new(Pattern, options: options, policy: new Utf8RegexPolicy
        {
            MatchTimeout = timeout ?? Regex.InfiniteMatchTimeout,
            ResourcePolicy = new()
            {
                MaxWorkSteps = work,
                MaxDepth = 8192,
                MaxWorkingBytes = working,
                MaxResultBytes = 1
            }
        });
}
