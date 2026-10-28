using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Diagnostics;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Input;

namespace Lokad.Utf8Regex.Tests;

[Collection(Utf8RuntimeAllocationCollection.Name)]
public sealed class Utf8LiteralExistenceTests
{
    [Theory]
    [InlineData(RegexOptions.None)]
    [InlineData(RegexOptions.Compiled)]
    public void CompleteSuffixExistencePreservesScalarsOffsetsAndOtherOperations(RegexOptions options)
    {
        foreach (var pattern in new[] { ".*(?<x>ss)", @"[^\n]*(?<x>\x73s)", ".*(?<x>a\nb)", ".*(a)(b)", ".*(?<x>ab)(?<y>cd)" })
        {
            var regex = new Utf8Regex(pattern, options);
            var oracle = new Regex(pattern, options);
            foreach (var text in new[] { "", "é😀", "prefixss\nss", "éss😀", "a\nb", "a", "b", "ab", "cd", "abcd" })
            {
                var padded = Encoding.UTF8.GetBytes("pad" + text + "pad");
                var input = padded.AsSpan(3, Encoding.UTF8.GetByteCount(text));
                Assert.Equal(oracle.IsMatch(text), regex.IsMatch(input));
                Assert.Equal(oracle.Count(text), regex.Count(input));
                var expected = oracle.Match(text);
                var actual = regex.Match(input);
                Assert.Equal(expected.Success, actual.Success);
                if (expected.Success)
                    Assert.Equal(expected.Value, Encoding.UTF8.GetString(input.Slice(actual.IndexInBytes, actual.LengthInBytes)));
            }
        }
        var suffix = new Utf8Regex(".*(ss)", options);
        Assert.Equal(1, suffix.Count("ssxxss"u8));
        Assert.False(suffix.IsMatchFromUtf16Offset("éss😀x"u8, 3));
        Assert.True(suffix.IsMatchFromUtf16Offset("éss😀xss"u8, 3));
        Assert.Throws<ArgumentOutOfRangeException>(() => suffix.IsMatchFromUtf16Offset("😀ss"u8, 1));
    }

    [Fact]
    public void FiniteBooleanSearchNeedsNoVmScratchButStillSpendsWork()
    {
        var regex = Limited(100000);
        Assert.True(regex.IsMatch("é😀ss"u8));
        Assert.False(regex.IsMatch("é😀no"u8));
        var firstSuccess = 0UL;
        for (ulong cap = 1; cap <= 32; cap++)
        {
            try
            {
                Assert.True(Limited(cap).IsMatch("ss"u8));
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
        foreach (var options in new[] { RegexOptions.RightToLeft, RegexOptions.NonBacktracking, RegexOptions.IgnoreCase })
            Assert.Throws<Utf8ResourceLimitExceededException>(() => Limited(100000, options).IsMatch("ss"u8));
    }

    [Fact]
    public void MatchDoesNotPermitSkippingMalformedTailValidationOrEarlierWorkDenial()
    {
        var regex = new Utf8Regex(".*(ss)");
        var malformed = "ss😀"u8.ToArray().Append((byte)255).ToArray();
        Assert.Throws<ArgumentException>(() => regex.IsMatch(malformed));
        var lateMalformed = Encoding.UTF8.GetBytes(new string('x', 200000)).Append((byte)255).ToArray();
        Assert.Equal(Utf8ResourceLimitKind.ExecutionWorkSteps,
            Assert.Throws<Utf8ResourceLimitExceededException>(() => Limited(1).IsMatch(lateMalformed)).ResourceKind);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void BoundedSearchPollsOriginalCancellationAndDeadlineAfterValidation(bool expire)
    {
        var input = Encoding.UTF8.GetBytes(new string('x', 200000) + "ss");
        var calibration = new Utf8SliceInvocation(new(4096, 1, 1, 1), Regex.InfiniteMatchTimeout, default);
        Utf8Validation.ThrowIfInvalidOnly(input, ref calibration);
        var threshold = ((calibration.Budget.Steps / 64) + 1) * 64;
        long ticks = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
        using var source = new CancellationTokenSource();
        var regex = Limited(4096, timeout: expire ? TimeSpan.FromMilliseconds(20) : null);
        var retained = new Utf8Regex(".*(?<x>ss)").MatchDetailed("éss"u8);
        var entered = false;
        var session = Utf8SearchDiagnosticsSession.Start(steps =>
        {
            if (steps < threshold) return;
            entered = true;
            if (expire) ticks = 50; else source.Cancel();
        });
        Exception? failure = null;
        try { regex.IsMatch(input, source.Token); }
        catch (Exception error) { failure = error; }
        finally { session.Complete(); }
        Assert.True(entered);
        if (expire) Assert.IsType<RegexMatchTimeoutException>(failure);
        else Assert.Equal(source.Token, Assert.IsType<OperationCanceledException>(failure).CancellationToken);
        Assert.Equal("ss", retained.GetGroup("x").GetValueString());
        using var canceled = new CancellationTokenSource();
        canceled.Cancel();
        Assert.Equal(canceled.Token,
            Assert.Throws<OperationCanceledException>(() => regex.IsMatch(input, canceled.Token)).CancellationToken);
    }

    private static Utf8Regex Limited(ulong work, RegexOptions options = RegexOptions.None, TimeSpan? timeout = null) =>
        new(".*(ss)", options, timeout ?? Regex.InfiniteMatchTimeout,
            new() { MaxWorkSteps = work, MaxDepth = 1, MaxWorkingBytes = 1, MaxResultBytes = 1 });

    [Fact]
    public void CancellationFromTheLastObservedDeadlinePollCannotPublishSuccess()
    {
        var reads = 0;
        var cancelAt = int.MaxValue;
        using var source = new CancellationTokenSource();
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() =>
        {
            if (++reads == cancelAt) source.Cancel();
            return 0;
        });
        var regex = Limited(100000, timeout: TimeSpan.FromSeconds(1));
        Assert.True(regex.IsMatch("ss"u8, source.Token));
        Assert.True(reads > 1);
        cancelAt = reads;
        reads = 0;
        Assert.Equal(source.Token,
            Assert.Throws<OperationCanceledException>(() => regex.IsMatch("ss"u8, source.Token)).CancellationToken);
        Assert.Equal(cancelAt, reads);
    }
}
