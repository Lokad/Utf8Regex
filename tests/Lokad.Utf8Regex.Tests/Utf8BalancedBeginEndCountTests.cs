using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.FrontEnd;
using Lokad.Utf8Regex.Internal.Diagnostics;
using Lokad.Utf8Regex.Internal.Input;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8BalancedBeginEndCountTests
{
    private const string Pattern =
        "BEGIN(?:(?<open>BEGIN)|(?<-open>END)|(?:(?!BEGIN|END)[\\s\\S]))*END(?(open)(?!))";

    [Theory]
    [InlineData(RegexOptions.CultureInvariant)]
    [InlineData(RegexOptions.CultureInvariant | RegexOptions.Compiled)]
    public void CountMatchesDotNetAcrossExhaustiveTokenSequences(RegexOptions options)
    {
        var oracle = new Regex(Pattern, options, Regex.InfiniteMatchTimeout);
        var regex = new Utf8Regex(Pattern, options);

        for (var tokenCount = 0; tokenCount <= 10; tokenCount++)
        {
            var combinationCount = 1 << tokenCount;
            for (var combination = 0; combination < combinationCount; combination++)
            {
                var input = BuildTokenSequence(tokenCount, combination);
                Assert.Equal(oracle.Count(input), regex.Count(Encoding.UTF8.GetBytes(input)));
            }
        }
    }

    [Theory]
    [InlineData(RegexOptions.None)]
    [InlineData(RegexOptions.Compiled)]
    public void CountMatchesDotNetAcrossTextAndUnmatchedTokens(RegexOptions options)
    {
        string[] inputs =
        [
            "",
            "BEGINEND",
            "BEGINBEGINENDEND",
            "BEGINBEGINEND",
            "BEGINENDEND",
            "ENDBEGINEND",
            "BEGINENDBEGINEND",
            "BEGINBEGINENDBEGINEND",
            "BBEGIN x END",
            "BEGEND BEGIN payload END EEND",
            "é BEGIN раздел BEGIN данные END хвост END 終",
            "BEGIN outer BEGIN complete END BEGIN incomplete",
        ];
        var oracle = new Regex(Pattern, options, Regex.InfiniteMatchTimeout);
        var regex = new Utf8Regex(Pattern, options);

        foreach (var input in inputs)
        {
            Assert.Equal(oracle.Count(input), regex.Count(Encoding.UTF8.GetBytes(input)));
        }
    }

    [Fact]
    public void CountFallsBackBeyondBoundedNativeNesting()
    {
        var input = string.Concat(Enumerable.Repeat("BEGIN", 129)) +
            string.Concat(Enumerable.Repeat("END", 129));
        var bytes = Encoding.UTF8.GetBytes(input);
        var regex = new Utf8Regex(Pattern, RegexOptions.CultureInvariant);

        Assert.False(Utf8BalancedBeginEndExecutor.TryCount(bytes, out _));
        Assert.Equal(Regex.Count(input, Pattern), regex.Count(bytes));
    }

    [Theory]
    [InlineData(RegexOptions.CultureInvariant)]
    [InlineData(RegexOptions.CultureInvariant | RegexOptions.Compiled)]
    public void CountRejectsMalformedUtf8(RegexOptions options)
    {
        var regex = new Utf8Regex(Pattern, options);
        byte[] malformed = [.. "BEGIN payload END"u8, 0xFF];

        Assert.Throws<ArgumentException>(() => regex.Count(malformed));
    }

    [Fact]
    public void AnalyzerAdmitsOnlyCaseSensitiveCompatibleOptions()
    {
        Assert.True(ClassifiesBalanced(Pattern, RegexOptions.None));
        Assert.True(ClassifiesBalanced(
            Pattern,
            RegexOptions.CultureInvariant | RegexOptions.Compiled));
        Assert.False(ClassifiesBalanced(Pattern, RegexOptions.IgnoreCase));
        Assert.False(ClassifiesBalanced(Pattern + "x", RegexOptions.None));
    }

    [Fact]
    public void FiniteCountsReuseTheProofAcrossScalarTailsAndWindowBoundaries()
    {
        var alias = Pattern.Replace("open", "stack").Replace("BEGIN", @"(?:\x42)EGIN");
        using var token = new CancellationTokenSource();
        foreach (var pattern in new[] { Pattern, alias })
        foreach (var mode in new[] { RegexOptions.None, RegexOptions.Compiled })
        {
            var regex = new Utf8Regex(pattern, mode, Timeout.InfiniteTimeSpan,
                new() { MaxWorkSteps = 100000, MaxDepth = 128, MaxWorkingBytes = 512, MaxResultBytes = 1 });
            foreach (var prefix in new[] { 0, 2046, 2047, 2048, 2049 })
            {
                var text = new string('x', prefix) + "BEGIN😀\n\0BEGINéEND";
                var padded = Encoding.UTF8.GetBytes("pad" + text + "tail");
                var input = padded.AsSpan(3, Encoding.UTF8.GetByteCount(text));
                var session = Utf8SearchDiagnosticsSession.Start();
                try
                {
                    Assert.Equal(1, regex.Count(input, token.Token));
                    Assert.Equal(Utf8ExecutionRoute.FallbackDirectUtf8BalancedBeginEnd, session.ExecutionRoute);
                }
                finally { session.Complete(); }
            }
        }
    }

    [Fact]
    public void WorkDepthAndStackDenialsReleaseOnlyTheLocalReservation()
    {
        var input = Encoding.UTF8.GetBytes(new string('x', 2049) + "BEGINBEGIN😀END");
        var complete = new Utf8SliceInvocation(Utf8ExecutionLimits.Unlimited, Timeout.InfiniteTimeSpan, default);
        Assert.Equal(Utf8ExecutionLimitKind.None, Utf8BalancedBeginEndExecutor.TryCountWellFormed(
            input, ref complete, out var count, out var handled));
        Assert.True(handled); Assert.Equal(1, count); Assert.Equal(2U, complete.Budget.PeakDepth);
        Assert.Equal(0UL, complete.Ledger.ReservedBytes);
        for (ulong cap = 1; cap <= complete.Budget.Steps; cap++)
        {
            var invocation = new Utf8SliceInvocation(new(cap, 128, 525, 1), Timeout.InfiniteTimeSpan, default);
            Assert.True(invocation.Ledger.TryReserve(13));
            var breach = Utf8BalancedBeginEndExecutor.TryCountWellFormed(input, ref invocation, out count, out handled);
            Assert.Equal(cap < complete.Budget.Steps ? Utf8ExecutionLimitKind.WorkSteps : Utf8ExecutionLimitKind.None, breach);
            Assert.Equal(cap == complete.Budget.Steps, handled);
            Assert.Equal(handled ? 1 : 0, count);
            Assert.Equal(13UL, invocation.Ledger.ReservedBytes);
            Assert.Equal(525UL, invocation.Budget.PeakWorkingBytes);
        }
        foreach (var limits in new[] { new Utf8ExecutionLimits(100000, 1, 525, 1), new(100000, 128, 524, 1) })
        {
            var invocation = new Utf8SliceInvocation(limits, Timeout.InfiniteTimeSpan, default);
            Assert.True(invocation.Ledger.TryReserve(13));
            Assert.Equal(limits.MaxDepth == 1 ? Utf8ExecutionLimitKind.Depth : Utf8ExecutionLimitKind.WorkingBytes,
                Utf8BalancedBeginEndExecutor.TryCountWellFormed(input, ref invocation, out count, out handled));
            Assert.False(handled); Assert.Equal(0, count); Assert.Equal(13UL, invocation.Ledger.ReservedBytes);
        }
        foreach (var policy in new[] { new Utf8ResourcePolicy { MaxDepth = 1 }, new() { MaxWorkingBytes = 511 } })
        {
            var regex = new Utf8Regex(Pattern, RegexOptions.None, Timeout.InfiniteTimeSpan, policy);
            var failure = Assert.Throws<Utf8ResourceLimitExceededException>(() => regex.Count(input));
            Assert.Equal(policy.MaxDepth == 1 ? Utf8ResourceLimitKind.ExecutionDepth : Utf8ResourceLimitKind.ExecutionWorkingBytes, failure.ResourceKind);
        }
    }

    [Fact]
    public void DeclinedDepthRetainsWorkAndOriginalScalarCount()
    {
        var text = "BEGINEND" + string.Concat(Enumerable.Repeat("BEGIN", 129)) +
            string.Concat(Enumerable.Repeat("END", 129));
        var input = Encoding.UTF8.GetBytes(text);
        var invocation = new Utf8SliceInvocation(Utf8ExecutionLimits.Unlimited, Timeout.InfiniteTimeSpan, default);
        Assert.True(invocation.Ledger.TryReserve(13));
        Assert.Equal(Utf8ExecutionLimitKind.None, Utf8BalancedBeginEndExecutor.TryCountWellFormed(
            input, ref invocation, out var count, out var handled));
        Assert.False(handled); Assert.Equal(0, count); Assert.Equal(13UL, invocation.Ledger.ReservedBytes);
        Assert.True(invocation.Budget.Steps >= 130);
        var regex = new Utf8Regex(Pattern);
        var program = regex.Inspection.AvailableOwnedSliceProgram;
        var slots = Utf8SliceRunner.RentSlots(program.GroupCount, ref invocation);
        try
        {
            var outcome = Utf8SliceRunner.CountFromBytes(program, input, 0, slots.Starts, slots.Ends, ref invocation);
            Assert.Equal(Utf8ExecutionLimitKind.None, outcome.Breach); Assert.Equal(2, outcome.Count);
        }
        finally { Utf8SliceRunner.ReleaseSlots(slots, ref invocation); }
        Assert.Equal(2, regex.Count(input));
        Assert.Equal(13UL, invocation.Ledger.ReservedBytes);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void CountInterruptsAfterValidationUsingOriginalAuthority(bool expire)
    {
        long ticks = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
        var input = Encoding.UTF8.GetBytes(new string('x', 320000) + "BEGIN😀END");
        var validation = new Utf8SliceInvocation(Utf8ExecutionLimits.Unlimited, Timeout.InfiniteTimeSpan, default);
        Utf8Validation.ThrowIfInvalidOnly(input, ref validation);
        var entered = false;
        using var token = new CancellationTokenSource();
        var timeout = expire ? TimeSpan.FromMilliseconds(20) : Timeout.InfiniteTimeSpan;
        var regex = new Utf8Regex(Pattern, RegexOptions.None, timeout,
            new() { MaxWorkSteps = 10000000, MaxDepth = 128, MaxWorkingBytes = 512, MaxResultBytes = 1 });
        var session = Utf8SearchDiagnosticsSession.Start(steps =>
        {
            if (entered || steps <= validation.Budget.Steps) return;
            entered = true;
            if (expire) ticks += 50; else token.Cancel();
        });
        try
        {
            if (expire)
            {
                var failure = Assert.Throws<RegexMatchTimeoutException>(() => regex.Count(input, token.Token));
                Assert.Equal(Pattern, failure.Pattern); Assert.Equal(timeout, failure.MatchTimeout);
                Assert.Empty(failure.Input);
            }
            else Assert.Equal(token.Token, Assert.Throws<OperationCanceledException>(() => regex.Count(input, token.Token)).CancellationToken);
            Assert.True(entered);
        }
        finally { session.Complete(); }
        Assert.Equal(1, regex.Count("BEGINEND"u8));
    }

    [Fact]
    public void FinalPublicationPollReleasesStackAfterCancellation()
    {
        var reads = 0;
        var cancelAt = int.MaxValue;
        using var token = new CancellationTokenSource();
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() =>
        {
            if (++reads == cancelAt) token.Cancel();
            return 0;
        });
        var invocation = new Utf8SliceInvocation(new(100000, 128, 525, 1), TimeSpan.FromSeconds(1), token.Token);
        Assert.True(invocation.Ledger.TryReserve(13));
        reads = 0;
        Assert.Equal(Utf8ExecutionLimitKind.None, Utf8BalancedBeginEndExecutor.TryCountWellFormed(
            "BEGINEND"u8, ref invocation, out _, out _));
        cancelAt = reads; reads = 0;
        Assert.Equal(token.Token, Assert.Throws<OperationCanceledException>(() =>
            Utf8BalancedBeginEndExecutor.TryCountWellFormed("BEGINEND"u8, ref invocation, out _, out _)).CancellationToken);
        Assert.Equal(cancelAt, reads); Assert.Equal(13UL, invocation.Ledger.ReservedBytes);
    }

    [Fact]
    public void CapturesRetainedResultsOffsetsAndExcludedShapesRemainScalar()
    {
        var input = "BEGIN😀BEGINéEND tail END BEGINxEND"u8.ToArray();
        foreach (var pattern in new[] { Pattern, Pattern.Replace("*END", "*?END"), @"(?<open>x?)" + Pattern })
        foreach (var mode in new[] { RegexOptions.None, RegexOptions.Compiled, RegexOptions.RightToLeft, RegexOptions.IgnoreCase })
        {
            var regex = new Utf8Regex(pattern, mode);
            var oracle = new Regex(pattern, mode);
            var text = Encoding.UTF8.GetString(input);
            Assert.Equal(oracle.Count(text), regex.Count(input));
            Assert.Equal(oracle.Count(text, 5), regex.CountFromUtf16Offset(input, 5));
        }
        var owner = new Utf8Regex(Pattern);
        var retained = owner.MatchDetailed(input);
        Assert.Equal("BEGIN😀BEGINéEND tail END", retained.GetValueString());
        Assert.False(retained.GetGroup("open").Success);
        Assert.Equal(2, owner.Count(input));
        Assert.Equal("BEGIN😀BEGINéEND tail END", retained.GetValueString());
        byte[] malformed = [.. input, 0xff];
        Assert.Throws<ArgumentException>(() => owner.Count(malformed));
        using var token = new CancellationTokenSource(); token.Cancel();
        Assert.Equal(token.Token, Assert.Throws<OperationCanceledException>(() => owner.Count(malformed, token.Token)).CancellationToken);
    }

    private static bool ClassifiesBalanced(string pattern, RegexOptions options) =>
        Utf8FallbackRegexFamilyAnalyzer.Classify(Utf8FrontEnd.Analyze(pattern, options)).Kind ==
            Utf8FallbackDirectFamilyKind.Utf8BalancedBeginEndCount;

    private static string BuildTokenSequence(int tokenCount, int combination)
    {
        var builder = new StringBuilder(tokenCount * 7);
        for (var tokenIndex = 0; tokenIndex < tokenCount; tokenIndex++)
        {
            builder.Append((combination & (1 << tokenIndex)) == 0 ? "BEGIN" : "END");
            builder.Append('x');
        }

        return builder.ToString();
    }
}
