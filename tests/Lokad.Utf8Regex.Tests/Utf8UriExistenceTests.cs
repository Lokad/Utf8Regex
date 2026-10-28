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
        var regex = new Utf8Regex(Pattern, options);
        var captured = new Utf8Regex("(?<uri>" + Pattern + ")", options);
        foreach (var text in new[] { "", "x://a?query", "x://😀", "𐐀://😀😀", "é://ab", "١://ab",
            "x://😀/", "x:///😀😀", "😀://ab", "x://a x://ab", "x://ab?#tail x://cd" })
        {
            var input = Encoding.UTF8.GetBytes(text);
            Assert.Equal(captured.IsMatch(input), regex.IsMatch(input));
            Assert.Equal(captured.Count(input), regex.Count(input));
            var match = regex.MatchDetailed(input);
            Assert.Equal(match.Success, regex.IsMatch(input));
            if (match.Success)
                Assert.Equal(captured.MatchDetailed(input).GetGroup("uri").GetValueString(), match.GetGroup(0).GetValueString());
        }
        Assert.False(regex.IsMatch("x://😀"u8));
        Assert.True(regex.IsMatch("𐐀://😀😀"u8));
        Assert.True(regex.IsMatchFromUtf16Offset("😀x://ab"u8, 2));
        Assert.False(regex.IsMatchFromUtf16Offset("😀x://ab"u8, 3));
        Assert.Throws<ArgumentOutOfRangeException>(() => regex.IsMatchFromUtf16Offset("😀x://ab"u8, 1));
    }

    [Fact]
    public void FiniteExistenceNeedsNoVmScratchButRetainsAdmissionAndStrictValidation()
    {
        Assert.True(Limited(100000).IsMatch("𐐀://😀😀"u8));
        Assert.False(Limited(100000).IsMatch("x://😀"u8));
        ulong firstSuccess = 0;
        for (ulong cap = 1; cap <= 32; cap++)
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
        Assert.InRange(firstSuccess, 2UL, 32UL);
        var malformed = "x://ab😀"u8.ToArray().Append((byte)255).ToArray();
        Assert.Throws<ArgumentException>(() => Limited(100000).IsMatch(malformed));
        var lateMalformed = Encoding.UTF8.GetBytes(new string('x', 200000)).Append((byte)255).ToArray();
        Assert.Equal(Utf8ResourceLimitKind.ExecutionWorkSteps,
            Assert.Throws<Utf8ResourceLimitExceededException>(() => Limited(1).IsMatch(lateMalformed)).ResourceKind);
        foreach (var options in new[] { RegexOptions.RightToLeft, RegexOptions.NonBacktracking, RegexOptions.IgnoreCase })
            Assert.Throws<Utf8ResourceLimitExceededException>(() => Limited(100000, options).IsMatch("x://ab"u8));
    }

    [Theory]
    [InlineData(0, false)] [InlineData(0, true)]
    [InlineData(1, false)] [InlineData(1, true)]
    [InlineData(2, false)] [InlineData(2, true)]
    [InlineData(3, false)] [InlineData(3, true)]
    public void OriginalInterruptionReachesDelimiterSchemeBodyAndFailedCandidateLoops(int phase, bool expire)
    {
        var text = phase switch
        {
            0 => new string('x', 200000),
            1 => new string('x', 200000) + "://ab",
            2 => "x://" + new string('a', 200000),
            _ => string.Concat(Enumerable.Repeat("x://a ", 40000)),
        };
        var input = Encoding.UTF8.GetBytes(text);
        var calibration = new Utf8SliceInvocation(new(1000000, 1, 1, 1), Regex.InfiniteMatchTimeout, default);
        Utf8Validation.ThrowIfInvalidOnly(input, ref calibration);
        var threshold = ((calibration.Budget.Steps / 64) + 1) * 64;
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
        try { regex.IsMatch(input, cancellation.Token); }
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

    private static Utf8Regex Limited(ulong work, RegexOptions options = RegexOptions.None, TimeSpan? timeout = null)
        => new(Pattern, options, timeout ?? Regex.InfiniteMatchTimeout,
            new() { MaxWorkSteps = work, MaxDepth = 1, MaxWorkingBytes = 1, MaxResultBytes = 1 });
}
