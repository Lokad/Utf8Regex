using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Diagnostics;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Input;

namespace Lokad.Utf8Regex.Tests;

[Collection(Utf8RuntimeAllocationCollection.Name)]
public sealed class Utf8LeadingAsciiCandidateTests
{
    [Theory]
    [InlineData(@"(?:[A-Z][a-z]+\s*){2,4}")]
    [InlineData(@"(?:(?<w>[A-Z][a-z]+)\s*){2,4}")]
    [InlineData(@"(?:(?:[A-Z][a-z]+){1,2}\s*){2,4}")]
    [InlineData(@"(?:A[0-9]+\s*){2,4}")]
    [InlineData(@"[A-Z]+(?<last>[a-z])")]
    [InlineData(@"(?<w>[A-Z]+)[a-z]\k<w>")]
    [InlineData(@"[A-CX-Z]+[a-z]")]
    [InlineData(@"[^A-Z]+[a-z]")]
    [InlineData(@"[\p{Lu}]+[a-z]")]
    [InlineData(@"[A-Z]*[a-z]")]
    [InlineData(@"(?:[A-Z]?a){1,4}")]
    [InlineData(@"(?<=x)[A-Z]+a")]
    [InlineData(@"[A-Z][a-z]")]
    [InlineData(@"(?:[A-Z][a-z]?){2,4}")]
    [InlineData(@"(?:[A-Z]{1,2}[a-z]){2,4}")]
    [InlineData(@"(?:[A-Z](?:[a-z]|[0-9])){2,4}")]
    [InlineData(@"(?<x>[A-Z])(?<y>[a-z]+)\k<y>")]
    [InlineData(@"(?:(?i:[A-Z])[a-z]+){2,4}")]
    public void CountsPreserveScalarEnumerationCapturesAndNonzeroFloors(string pattern)
    {
        foreach (var options in new[] { RegexOptions.None, RegexOptions.Compiled })
        {
            var regex = new Utf8Regex(pattern, options, Regex.InfiniteMatchTimeout, Funded);
            foreach (var tail in new[] { "", "Ab", "Ab Cd", "Ab Cd Ef Gh Ij Kl", "Ab\u2003Cd", "Éa Βa",
                "ABaB", "xAa", "A1 A2 A3", "aab", "DAa XBza", "Aa😀Bb Cc\u00a0Dd" })
            {
                var text = "é😀x" + tail + "😀é";
                var input = Encoding.UTF8.GetBytes(text);
                byte[] storage = [0xff, .. input, 0xff];
                using var live = new CancellationTokenSource();
                foreach (var start in new[] { 0, 1, 3, 4, text.Length })
                {
                    var cursor = regex.EnumerateMatchesFromUtf16Offset(input, start);
                    var expected = 0;
                    while (cursor.MoveNext()) expected++;
                    Assert.Equal(expected, regex.CountFromUtf16Offset(storage.AsSpan(1, input.Length), start));
                    Assert.Equal(expected, regex.CountFromUtf16Offset(input, start, live.Token));
                }
            }
        }
        var captured = new Utf8Regex(@"(?<w>[A-Z]+)[a-z]\k<w>", RegexOptions.None, Regex.InfiniteMatchTimeout, Funded);
        Assert.Equal(1, captured.Count("éABaB😀"u8));
        Assert.Equal("B", captured.MatchDetailed("éABaB😀"u8).GetGroup("w").GetValueString());
    }

    [Theory]
    [InlineData(2046)] [InlineData(2047)] [InlineData(2048)] [InlineData(2049)] [InlineData(4095)]
    public void WindowTailsAndUnicodeSeparatorsRemainScalar(int prefixLength)
    {
        var regex = new Utf8Regex(@"(?:[A-Z][a-z]+\s*){2,4}", RegexOptions.Compiled, Regex.InfiniteMatchTimeout, Funded);
        var input = Encoding.UTF8.GetBytes(new string('x', prefixLength) + "😀Ab\u2003Cd😀Ef Gh");
        using var live = new CancellationTokenSource();
        Assert.Equal(2, regex.Count(input));
        Assert.Equal(2, regex.Count(input, live.Token));
        Assert.Equal(1, regex.Count("Ab\u2003Cd"u8));
        Assert.Equal(1, regex.Count(Encoding.UTF8.GetBytes("😀" + new string('x', prefixLength - 4) + "Ab Cd"), live.Token));
    }

    [Fact]
    public void RejectedSecondScalarsAvoidVmStorageButStillConsumeBoundedWork()
    {
        const string pattern = @"(?:[A-Z][a-z]+\s*){2,4}";
        var input = Encoding.UTF8.GetBytes(new string('A', 200000) + "😀A");
        Assert.Equal(0, new Utf8Regex(pattern, RegexOptions.None, Regex.InfiniteMatchTimeout,
            Funded with { MaxWorkingBytes = 1 }).Count(input));
        Assert.Equal(Utf8ResourceLimitKind.ExecutionWorkSteps,
            Assert.Throws<Utf8ResourceLimitExceededException>(() => new Utf8Regex(pattern,
                RegexOptions.None, Regex.InfiniteMatchTimeout, Funded with { MaxWorkSteps = 4096 }).Count(input)).ResourceKind);
    }

    [Theory]
    [InlineData(RegexOptions.RightToLeft)]
    [InlineData(RegexOptions.NonBacktracking)]
    [InlineData(RegexOptions.IgnoreCase)]
    [InlineData(RegexOptions.ECMAScript)]
    public void OtherOptionsKeepTheirOwnScalarExecution(RegexOptions options)
    {
        var regex = new Utf8Regex(@"(?:[A-Z][a-z]+\s*){2,4}", options, Regex.InfiniteMatchTimeout, Funded);
        var input = "Ab\u2003Cd😀ab cd Ef Gh Ij Kl"u8;
        var cursor = regex.EnumerateMatches(input);
        var expected = 0;
        while (cursor.MoveNext()) expected++;
        Assert.Equal(expected, regex.Count(input));
    }

    [Fact]
    public void SkippedStartsStillRequireValidationWorkAndRealVmStorage()
    {
        const string pattern = @"(?:[A-Z][a-z]+\s*){2,4}";
        var input = Encoding.UTF8.GetBytes(new string('x', 200000) + "😀Ab Cd");
        Assert.Equal(1, new Utf8Regex(pattern, RegexOptions.None, Regex.InfiniteMatchTimeout,
            Funded with { MaxWorkSteps = 4096 }).Count(input));
        Assert.Equal(Utf8ResourceLimitKind.ExecutionWorkingBytes,
            Assert.Throws<Utf8ResourceLimitExceededException>(() => new Utf8Regex(pattern,
                RegexOptions.None, Regex.InfiniteMatchTimeout, Funded with { MaxWorkingBytes = 1 }).Count(input)).ResourceKind);
        var invalid = input.Append((byte)255).ToArray();
        Assert.Throws<ArgumentException>(() => new Utf8Regex(pattern).Count(invalid));
        Assert.Equal(Utf8ResourceLimitKind.ExecutionWorkSteps,
            Assert.Throws<Utf8ResourceLimitExceededException>(() => new Utf8Regex(pattern,
                RegexOptions.None, Regex.InfiniteMatchTimeout, Funded with { MaxWorkSteps = 1 }).Count(invalid)).ResourceKind);
    }

    [Fact]
    public void EveryWorkCutoffPreservesTheOriginalScratchLoan()
    {
        var regex = new Utf8Regex(@"(?<w>[A-Z]+)[a-z]\k<w>");
        var program = regex.Inspection.AvailableOwnedSliceProgram!;
        var input = "éABaB😀"u8.ToArray();
        var complete = new Utf8SliceInvocation(new(100000, 0, 0), Regex.InfiniteMatchTimeout, default);
        Assert.True(complete.Ledger.TryReserve(13));
        var outcome = Utf8SliceRunner.CountFromBytes(program, input, 0, new int[program.GroupCount],
            new int[program.GroupCount], ref complete);
        Assert.Equal(Utf8ExecutionLimitKind.None, outcome.Breach);
        Assert.Equal(1, outcome.Count);
        Assert.Equal(13UL, complete.Ledger.ReservedBytes);
        for (ulong cap = 1; cap <= complete.Budget.Steps; cap++)
        {
            var invocation = new Utf8SliceInvocation(new(cap, 0, 0), Regex.InfiniteMatchTimeout, default);
            Assert.True(invocation.Ledger.TryReserve(13));
            outcome = Utf8SliceRunner.CountFromBytes(program, input, 0, new int[program.GroupCount],
                new int[program.GroupCount], ref invocation);
            Assert.Equal(cap < complete.Budget.Steps ? Utf8ExecutionLimitKind.WorkSteps : Utf8ExecutionLimitKind.None, outcome.Breach);
            Assert.Equal(13UL, invocation.Ledger.ReservedBytes);
        }
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void BoundedRangeSearchKeepsOriginalCancellationAndDeadline(bool expire)
    {
        var input = Encoding.UTF8.GetBytes(new string('x', 200000) + "😀Ab Cd");
        var calibration = new Utf8SliceInvocation(new(4000000, 0, 0), Regex.InfiniteMatchTimeout, default);
        Utf8Validation.ThrowIfInvalidOnly(input, ref calibration);
        var threshold = ((calibration.Budget.Steps / 64) + 1) * 64;
        using var cancellation = new CancellationTokenSource();
        long ticks = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
        var regex = new Utf8Regex(@"(?:[A-Z][a-z]+\s*){2,4}", RegexOptions.Compiled,
            expire ? TimeSpan.FromMilliseconds(20) : Regex.InfiniteMatchTimeout, Funded);
        var entered = false;
        var session = Utf8SearchDiagnosticsSession.Start(steps =>
        {
            if (steps < threshold) return;
            entered = true;
            if (expire) ticks = 50; else cancellation.Cancel();
        });
        Exception? failure = null;
        try { regex.Count(input, cancellation.Token); }
        catch (Exception error) { failure = error; }
        finally { session.Complete(); }
        Assert.True(entered);
        if (expire) Assert.IsType<RegexMatchTimeoutException>(failure);
        else Assert.Equal(cancellation.Token, Assert.IsType<OperationCanceledException>(failure).CancellationToken);
    }

    [Fact]
    public void FinalRangeMissCannotPublishAfterTheLastDeadlinePollCancelsTheToken()
    {
        var reads = 0;
        var cancelAt = int.MaxValue;
        using var cancellation = new CancellationTokenSource();
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() =>
        {
            if (++reads == cancelAt) cancellation.Cancel();
            return 0;
        });
        var regex = new Utf8Regex(@"(?:[A-Z][a-z]+\s*){2,4}", RegexOptions.None, TimeSpan.FromSeconds(1), Funded);
        var input = Encoding.UTF8.GetBytes("😀Ab Cd" + new string('!', 8192));
        Assert.Equal(1, regex.Count(input, cancellation.Token));
        Assert.True(reads > 1);
        cancelAt = reads;
        reads = 0;
        Assert.Equal(cancellation.Token,
            Assert.Throws<OperationCanceledException>(() => regex.Count(input, cancellation.Token)).CancellationToken);
        Assert.Equal(cancelAt, reads);
    }

    private static Utf8ResourcePolicy Funded => new() { MaxWorkSteps = 4000000, MaxDepth = 4096,
        MaxWorkingBytes = 16 << 20, MaxResultBytes = 16 << 20 };
}
