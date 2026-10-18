using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Diagnostics;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Input;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8ScalarCountPrefixTests
{
    [Theory]
    [InlineData("(ab|cd)+", "xxabcd yy cdcd ab xx")]
    [InlineData("(a|ab)+b", "aaaab abab aaac ab")]
    [InlineData("(ab|a)+b", "aabab ab aaac")]
    [InlineData("(ab|cd)+?", "abcdabcd cd")]
    [InlineData("abc[0-9]+", "abcX abc12 abc3 abc")]
    [InlineData("(abc)\\1", "abcX abcabc abc")]
    [InlineData("(?=abc)abc", "xxabc ab abc")]
    [InlineData("(?<=x)(abc|def)", "abc xabc xdef def")]
    [InlineData("^abc[0-9]+", "xxabc1\nabc2\nabcX\nabc3")]
    [InlineData("(ab|cd)*", "xxabcd yy")]
    [InlineData("(?i:ab|cd)+", "ABcd xx abCD")]
    [InlineData("\\G(ab|cd)+", "xxabcd ab")]
    [InlineData("a|\\Gb", "a xa")]
    [InlineData("a\\G|b", "b xb")]
    [InlineData("(?:a\\G)+|b", "b xb")]
    [InlineData("\\G|b", "b xb")]
    [InlineData("😀|\\Gb", "😀 x😀")]
    [InlineData("a\\G|😀", "😀 x😀")]
    public void PublicCountKeepsPrefixVerificationAndDeclines(string pattern, string subject)
    {
        foreach (var options in new[] { RegexOptions.CultureInvariant, RegexOptions.CultureInvariant | RegexOptions.Compiled,
            RegexOptions.CultureInvariant | RegexOptions.Multiline, RegexOptions.CultureInvariant | RegexOptions.RightToLeft,
            RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.RightToLeft })
        {
            var expected = new Regex(pattern, options).Count(subject);
            var unlimited = new Utf8Regex(pattern, options);
            var finite = new Utf8Regex(pattern, options, Timeout.InfiniteTimeSpan,
                new() { MaxWorkSteps = 100000, MaxDepth = 8192, MaxWorkingBytes = 1048576, MaxResultBytes = 1048576 });
            var storage = Encoding.UTF8.GetBytes("unused" + subject + "unused");
            var input = storage.AsSpan(6, Encoding.UTF8.GetByteCount(subject));
            Assert.Equal(expected, unlimited.Count(input));
            Assert.Equal(expected, finite.Count(input));
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RtlUnanchoredAlternativeRetainsScalarCapturesAndValidation(bool compiled)
    {
        var options = RegexOptions.RightToLeft | (compiled ? RegexOptions.Compiled : 0);
        var regex = new Utf8Regex("a\\G|(?<x>😀)", options, Timeout.InfiniteTimeSpan,
            new() { MaxWorkSteps = 100000, MaxDepth = 128, MaxWorkingBytes = 1048576, MaxResultBytes = 1048576 });
        var storage = "prefix😀 x😀tail"u8.ToArray();
        var input = storage.AsSpan(6, 10);
        using var source = new CancellationTokenSource();
        var retained = regex.MatchDetailed(input, source.Token);
        Assert.True(retained.Success);
        Assert.Equal(6, retained.IndexInBytes);
        Assert.Equal(4, retained.LengthInBytes);
        Assert.Equal("😀", retained.GetGroup("x").GetValueString());
        Assert.Equal(2, regex.Count(input, source.Token));
        Assert.Equal("😀", retained.GetGroup("x").GetValueString());
        var malformed = input.ToArray().Concat(new byte[] { 0xff }).ToArray();
        Assert.Throws<ArgumentException>(() => regex.Count(malformed));
        source.Cancel();
        Assert.Equal(source.Token, Assert.Throws<OperationCanceledException>(() =>
            regex.Count("😀 x😀"u8, source.Token)).CancellationToken);
        Assert.Equal(1, regex.Count("😀"u8));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MandatoryInitialRtlContiguousAnchorKeepsBoundedStop(bool compiled)
    {
        var regex = new Utf8Regex("a\\G", RegexOptions.RightToLeft | (compiled ? RegexOptions.Compiled : 0));
        var program = regex.Inspection.AvailableOwnedSliceProgram;
        var input = Encoding.UTF8.GetBytes(new string('x', 32768));
        var invocation = new Utf8SliceInvocation(new(16, 64, 8192, 1), Timeout.InfiniteTimeSpan, default);
        var result = Utf8SliceRunner.CountFromBytes(program, input, input.Length,
            new int[program.GroupCount], new int[program.GroupCount], ref invocation);
        Assert.Equal(Utf8ExecutionLimitKind.None, result.Breach);
        Assert.Equal(0, result.Count);
        Assert.Equal(0UL, invocation.Ledger.ReservedBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RtlGapUsesOriginalCancellationAndDeadline(bool expire)
    {
        const string pattern = "a\\G|b";
        var input = Encoding.UTF8.GetBytes("b" + new string('x', 32768) + "b");
        var validation = new Utf8SliceInvocation(new(10000000, 0, 0), Timeout.InfiniteTimeSpan, default);
        _ = Utf8Validation.Validate(input, ref validation);
        var validationSteps = validation.Budget.Steps;
        var regex = new Utf8Regex(pattern, RegexOptions.RightToLeft, TimeSpan.FromMilliseconds(20),
            new() { MaxWorkSteps = 10000000, MaxDepth = 128, MaxWorkingBytes = 1048576, MaxResultBytes = 1048576 });
        long ticks = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
        using var source = new CancellationTokenSource();
        var entered = false;
        var session = Utf8SearchDiagnosticsSession.Start(steps =>
        {
            if (steps <= validationSteps + 64) return;
            entered = true;
            if (expire) ticks = 50;
            else source.Cancel();
        });
        try
        {
            if (expire)
                Assert.Equal(pattern, Assert.Throws<RegexMatchTimeoutException>(() => regex.Count(input)).Pattern);
            else
                Assert.Equal(source.Token, Assert.Throws<OperationCanceledException>(() => regex.Count(input, source.Token)).CancellationToken);
        }
        finally { session.Complete(); }
        Assert.True(entered);
        ticks = 0;
        Assert.Equal(2, regex.Count(input));
        var limited = new Utf8Regex(pattern, RegexOptions.RightToLeft, Timeout.InfiniteTimeSpan,
            new() { MaxWorkSteps = 10000, MaxDepth = 128, MaxWorkingBytes = 1048576, MaxResultBytes = 1048576 });
        Assert.Equal(Utf8ResourceLimitKind.ExecutionWorkSteps,
            Assert.Throws<Utf8ResourceLimitExceededException>(() => limited.Count(input)).ResourceKind);
    }

    [Theory]
    [InlineData("(😀|ab)+", "é😀ab xx 😀😀", 2)]
    [InlineData("(é|😀)+?", "é😀 xx 😀", 3)]
    [InlineData("😀x+", "é😀xxx 😀x 😀", 2)]
    public void UnicodePrefixesKeepWholeScalarCoordinates(string pattern, string subject, int expected)
    {
        var regex = new Utf8Regex(pattern, RegexOptions.CultureInvariant);
        Assert.Equal(expected, regex.Count(Encoding.UTF8.GetBytes(subject)));
        Assert.Throws<ArgumentException>(() => regex.Count(Encoding.UTF8.GetBytes(subject).Concat(new byte[] { 0xff }).ToArray()));
    }

    [Fact]
    public void CandidateSearchReducesImpossibleVmAttemptsWithEveryCutoffPreserved()
    {
        var regex = new Utf8Regex("(ab|cd)+", RegexOptions.CultureInvariant);
        var program = regex.Inspection.AvailableOwnedSliceProgram;
        var plan = regex.Inspection.SearchPlan;
        var input = Encoding.UTF8.GetBytes(new string('x', 4097) + "abcd xx cd");
        var starts = new int[program.GroupCount]; var ends = new int[program.GroupCount];
        var plain = new Utf8SliceInvocation(new(ulong.MaxValue, 0, 0), Timeout.InfiniteTimeSpan, default);
        Assert.Equal(2, Utf8SliceRunner.CountFromBytes(program, input, 0, starts, ends, ref plain).Count);
        var guided = new Utf8SliceInvocation(new(ulong.MaxValue, 0, 0), Timeout.InfiniteTimeSpan, default);
        Assert.Equal(2, Utf8SliceRunner.CountFromBytes(program, input, 0, starts, ends, ref guided, plan).Count);
        Assert.True(guided.Budget.Steps < plain.Budget.Steps / 4);
        for (ulong limit = 1; limit <= guided.Budget.Steps; limit++)
        {
            var invocation = new Utf8SliceInvocation(new(limit, 0, 0), Timeout.InfiniteTimeSpan, default);
            Assert.True(invocation.Ledger.TryReserve(13));
            var result = Utf8SliceRunner.CountFromBytes(program, input, 0, starts, ends, ref invocation, plan);
            Assert.Equal(limit == guided.Budget.Steps ? Utf8ExecutionLimitKind.None : Utf8ExecutionLimitKind.WorkSteps, result.Breach);
            if (result.Breach == Utf8ExecutionLimitKind.None) Assert.Equal(2, result.Count);
            Assert.Equal(13ul, invocation.Ledger.ReservedBytes);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void LongCandidateMissPollsOriginalInvocationAndReleasesScratch(bool expire)
    {
        using var clock = Utf8DeadlineTestClock.Start();
        var regex = new Utf8Regex("(ab|cd)+", RegexOptions.CultureInvariant);
        var program = regex.Inspection.AvailableOwnedSliceProgram;
        var input = Encoding.UTF8.GetBytes(new string('x', 200000));
        using var source = new CancellationTokenSource();
        var invocation = new Utf8SliceInvocation(Utf8ExecutionLimits.Unlimited,
            expire ? TimeSpan.FromMilliseconds(200) : Timeout.InfiniteTimeSpan, source.Token);
        var entered = false;
        invocation.CheckpointHook = _ => { entered = true; if (expire) clock.Advance(250); else source.Cancel(); };
        Assert.True(invocation.Ledger.TryReserve(13));
        void Run() =>
            Utf8SliceRunner.CountFromBytes(program, input, 0, new int[program.GroupCount], new int[program.GroupCount], ref invocation, regex.Inspection.SearchPlan);
        if (expire) Assert.Throws<Utf8ExecutionDeadlineExpiredException>(Run);
        else Assert.Equal(source.Token, Assert.Throws<OperationCanceledException>(Run).CancellationToken);
        Assert.True(entered);
        Assert.Equal(13ul, invocation.Ledger.ReservedBytes);
        Assert.Equal(0, regex.Count(input));
    }
}
