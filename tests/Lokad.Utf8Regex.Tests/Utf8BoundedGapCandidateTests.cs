using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Diagnostics;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Input;

namespace Lokad.Utf8Regex.Tests;

[Collection(Utf8RuntimeAllocationCollection.Name)]
public sealed class Utf8BoundedGapCandidateTests
{
    private const string Pattern = @"[A-Za-z]{10}\s+[\s\S]{0,100}Result[\s\S]{0,100}\s+[A-Za-z]{10}";
    private static Utf8ResourcePolicy Funded => new() { MaxWorkSteps = 4000000,
        MaxDepth = 4096, MaxWorkingBytes = 16 << 20, MaxResultBytes = 16 << 20 };

    private static int ScalarCount(Utf8Regex regex, byte[] input, int fromByte)
    {
        var program = regex.Inspection.AvailableOwnedSliceProgram!;
        var invocation = new Utf8SliceInvocation(Utf8ExecutionLimits.Unlimited, Timeout.InfiniteTimeSpan, default);
        Utf8Validation.ThrowIfInvalidOnly(input, ref invocation);
        var slots = Utf8SliceRunner.RentSlots(program.GroupCount, ref invocation);
        try
        {
            // No candidate plan: the changed class/whitespace window cannot run.
            var result = Utf8SliceRunner.CountFromBytes(program, input, fromByte, slots.Starts, slots.Ends, ref invocation);
            Assert.Equal(Utf8ExecutionLimitKind.None, result.Breach);
            return result.Count;
        }
        finally { Utf8SliceRunner.ReleaseSlots(slots, ref invocation); }
    }

    [Theory]
    [InlineData(@"[A-Za-z]{3}\s+[\s\S]{0,3}Result[\s\S]{0,3}\s+[A-Za-z]{3}")]
    [InlineData(@"(?<w>\w{2,4})\s+[\s\S]{1,3}Result\k<w>")]
    [InlineData(@"(?<w>[a ]{1,4})\s+[\s\S]{0,3}R\k<w>")]
    [InlineData(@"[é😀a]{2,4}\s+[\s\S]{0,2}Result")]
    [InlineData(@"(?<w>\p{L}{2,4})\s+[\s\S]{0,4}Result(?<=Result)")]
    [InlineData(@"\w{2,4}\s+.{0,3}Result")]
    [InlineData(@"\w{2,4}\s+[\s\S]{0,3}?Result")]
    [InlineData(@"\w{2,4}\s+[\s\S]*Result")]
    [InlineData(@"\w{2,4}\s+[\s\S]{0,3}Result|x")]
    public void WindowsKeepEveryScalarFloorAndVmDecision(string pattern)
    {
        var text = new string('!', 260) + "abc \u2003😀Result def! ab abc Rabc! aa a Raa! aé😀\n😀Result! word Resultxx Resultword!";
        var input = Encoding.UTF8.GetBytes(text);
        byte[] storage = [0xff, .. input, 0xff];
        foreach (var mode in new[] { RegexOptions.None, RegexOptions.Compiled, RegexOptions.RightToLeft, RegexOptions.IgnoreCase })
        {
            var regex = new Utf8Regex(pattern, mode, Timeout.InfiniteTimeSpan, Funded);
            using var token = new CancellationTokenSource();
            for (var start = 255; start <= text.Length; start++)
            {
                if (start < text.Length && char.IsLowSurrogate(text[start])) continue;
                var fromByte = Encoding.UTF8.GetByteCount(text.AsSpan(0, start));
                var expected = ScalarCount(regex, input, fromByte);
                Assert.Equal(expected, regex.CountFromUtf16Offset(storage.AsSpan(1, input.Length), start));
                Assert.Equal(expected, regex.CountFromUtf16Offset(input, start, token.Token));
            }
        }
    }

    [Theory]
    [InlineData(0, 1)] [InlineData(1, 1)] [InlineData(99, 1)] [InlineData(100, 1)] [InlineData(101, 0)]
    public void BothGapBoundsCountScalarsRatherThanBytes(int length, int expected)
    {
        foreach (var scalar in new[] { "!", "é", "😀", "\n" })
        foreach (var side in new[] { false, true })
        {
            var gap = string.Concat(Enumerable.Repeat(scalar, length));
            var text = "abcdefghij \u2003" + (side ? "" : gap) + "Result" + (side ? gap : "") + "\u00a0 klmnopqrst";
            var input = Encoding.UTF8.GetBytes(new string('!', 260) + text);
            foreach (var mode in new[] { RegexOptions.None, RegexOptions.Compiled, RegexOptions.NonBacktracking })
            {
                var regex = new Utf8Regex(Pattern, mode, Timeout.InfiniteTimeSpan, Funded);
                // Neighboring whitespace runs may absorb the newline gap.
                var expectedCount = scalar == "\n" ? 1 : expected;
                Assert.Equal(expectedCount, ScalarCount(regex, input, 0));
                Assert.Equal(expectedCount, regex.Count(input));
                using var token = new CancellationTokenSource();
                Assert.Equal(expectedCount, regex.Count(input, token.Token));
            }
        }
    }

    [Fact]
    public void EarlyLiteralsInsideThePrefixDoNotHideALaterAnchor()
    {
        const string pattern = @"(?<w>\w{3,30})\s+[\s\S]{0,8}Result\k<w>";
        foreach (var tail in new[] { "aResultb ResultaResultb", "abc Result?Resultabc", "abc Resultabc Resultabc" })
        {
            var input = Encoding.UTF8.GetBytes(new string('!', 260) + tail);
            var regex = new Utf8Regex(pattern, RegexOptions.Compiled, Timeout.InfiniteTimeSpan, Funded);
            var expected = ScalarCount(regex, input, 0);
            Assert.True(expected > 0);
            var retained = regex.MatchDetailed(input);
            Assert.Equal(expected, regex.Count(input));
            Assert.True(retained.Success);
            Assert.False(string.IsNullOrEmpty(retained.GetGroup("w").GetValueString()));
        }
    }

    [Fact]
    public void DiscoveryRespectsFiniteAdmissionAndEveryWorkCutoff()
    {
        var input = Encoding.UTF8.GetBytes(new string('!', 100000) + "abcdefghij 😀Result klmnopqrst");
        var efficient = new Utf8Regex(Pattern, RegexOptions.Compiled, Timeout.InfiniteTimeSpan,
            Funded with { MaxWorkSteps = 4096 });
        using var token = new CancellationTokenSource();
        Assert.Equal(1, efficient.Count(input, token.Token));
        Assert.Equal(Utf8ResourceLimitKind.ExecutionWorkingBytes,
            Assert.Throws<Utf8ResourceLimitExceededException>(() => new Utf8Regex(Pattern,
                RegexOptions.None, Timeout.InfiniteTimeSpan, Funded with { MaxWorkingBytes = 1 }).Count(input)).ResourceKind);
        byte[] malformed = [.. input, 0xff];
        Assert.Throws<ArgumentException>(() => efficient.Count(malformed));
        var longClass = Encoding.UTF8.GetBytes(new string('a', 100000) + " Result klmnopqrst");
        Assert.Equal(1, new Utf8Regex(Pattern, RegexOptions.None, Timeout.InfiniteTimeSpan,
            Funded with { MaxWorkSteps = 16384 }).Count(longClass, token.Token));

        var regex = new Utf8Regex(@"(?<w>[a-z]{2,4})\s+[\s\S]{0,3}Result\k<w>");
        var program = regex.Inspection.AvailableOwnedSliceProgram!;
        var witness = Encoding.UTF8.GetBytes(new string('!', 260) + "abc 😀Resultbc");
        var complete = new Utf8SliceInvocation(new(100000, 0, 0), Timeout.InfiniteTimeSpan, default);
        Assert.True(complete.Ledger.TryReserve(13));
        var result = Utf8SliceRunner.CountFromBytes(program, witness, 0, new int[program.GroupCount],
            new int[program.GroupCount], ref complete, regex.Inspection.PreparedRegex.SearchPlan);
        Assert.Equal((1, Utf8ExecutionLimitKind.None), (result.Count, result.Breach));
        for (ulong cap = 1; cap <= complete.Budget.Steps; cap++)
        {
            var invocation = new Utf8SliceInvocation(new(cap, 0, 0), Timeout.InfiniteTimeSpan, default);
            Assert.True(invocation.Ledger.TryReserve(13));
            result = Utf8SliceRunner.CountFromBytes(program, witness, 0, new int[program.GroupCount],
                new int[program.GroupCount], ref invocation, regex.Inspection.PreparedRegex.SearchPlan);
            Assert.Equal(cap < complete.Budget.Steps ? Utf8ExecutionLimitKind.WorkSteps : Utf8ExecutionLimitKind.None, result.Breach);
            Assert.Equal(13UL, invocation.Ledger.ReservedBytes);
        }
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void OriginalInterruptionCoversDiscoveryAfterValidation(bool expire)
    {
        var input = Encoding.UTF8.GetBytes(new string('!', 200000) + "abcdefghij 😀Result klmnopqrst");
        var calibration = new Utf8SliceInvocation(new(4000000, 0, 0), Timeout.InfiniteTimeSpan, default);
        Utf8Validation.ThrowIfInvalidOnly(input, ref calibration);
        var threshold = (calibration.Budget.Steps / 64 + 1) * 64;
        using var token = new CancellationTokenSource();
        long ticks = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
        var regex = new Utf8Regex(Pattern, RegexOptions.Compiled, TimeSpan.FromMilliseconds(20), Funded);
        var entered = false;
        var session = Utf8SearchDiagnosticsSession.Start(steps =>
        {
            if (steps < threshold) return;
            entered = true;
            if (expire) ticks = 50; else token.Cancel();
        });
        try
        {
            if (expire) Assert.Equal(regex.MatchTimeout, Assert.Throws<RegexMatchTimeoutException>(() => regex.Count(input, token.Token)).MatchTimeout);
            else Assert.Equal(token.Token, Assert.Throws<OperationCanceledException>(() => regex.Count(input, token.Token)).CancellationToken);
            Assert.True(entered);
        }
        finally { session.Complete(); }
    }
}
