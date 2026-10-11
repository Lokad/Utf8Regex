using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Diagnostics;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.FrontEnd;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8ScalarCaptureContinuationTests
{
    private static readonly string[] Patterns =
    [
        "(a)(b)", "((ab)(c))", "(?<x>a(?<x>b)c)(d)", "^(?<x>ab)[0-9]$",
        "(?m:^(a)(b)$)", "(?i:(a)(bc))", "(a)([A-Z])", "(a)(b$)",
    ];

    public static IEnumerable<object[]> Cases()
    {
        foreach (var depth in new[] { 65, 16385 })
        foreach (var shape in Enumerable.Range(0, 3))
        foreach (var family in Enumerable.Range(0, Patterns.Length))
        foreach (var compiled in new[] { false, true })
            yield return [depth, shape, family, compiled];
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void DeepOwnedLoweringAndMatchingRetainAllCaptureHistories(int depth, int shape, int family, bool compiled)
    {
        var options = RegexOptions.CultureInvariant | (compiled ? RegexOptions.Compiled : RegexOptions.None);
        var program = Utf8ScalarCaptureWitness.Lower(Patterns[family], options, depth, shape);
        var oracle = new Regex(Patterns[family], RegexOptions.CultureInvariant);
        foreach (var subject in new[] { "", "ab", "abc", "abcd", "xxabyy", "aZ", "ABc", "\nab\n", "ab\n", "ab\r\n", "ab!\n", "ab1" })
        for (var start = 0; start <= subject.Length; start++)
            Utf8ScalarCaptureWitness.AssertAsciiAt(program, oracle, subject, start);
    }

    [Theory]
    [InlineData(65, 0)] [InlineData(65, 1)] [InlineData(65, 2)]
    [InlineData(4097, 0)] [InlineData(4097, 1)] [InlineData(4097, 2)]
    public void ScalarCaptureWorkAndStorageDenialRestoreBorrowedLoansAndRecover(int depth, int shape)
    {
        var program = Utf8ScalarCaptureWitness.Lower("(a)(b)", RegexOptions.CultureInvariant, depth, shape);
        var starts = new int[program.GroupCount]; var ends = new int[program.GroupCount];
        var baseline = new Utf8SliceInvocation(Utf8ExecutionLimits.Unlimited, Regex.InfiniteMatchTimeout, CancellationToken.None);
        Assert.True(baseline.Ledger.TryReserve(7));
        var outcome = Utf8SliceRunner.TryMatchAt(program, "ab"u8, 0, starts, ends, true, true, ref baseline);
        Assert.True(outcome.IsMatch);
        Assert.Equal(2, outcome.EndIndex);
        Assert.Equal(new[] { 0 }, outcome.CaptureStarts?[0]);
        Assert.Equal(new[] { 1 }, outcome.CaptureStarts?[1]);
        Utf8SliceRunner.ReleaseHistory(outcome.CaptureStarts, outcome.CaptureLengths, ref baseline);
        Assert.Equal(7UL, baseline.Ledger.ReservedBytes);
        baseline.Ledger.Release(7);
        var limits = Enumerable.Range(1, checked((int)baseline.Budget.Steps - 1))
            .Select(static work => new Utf8ExecutionLimits((ulong)work, 0, 0))
            .Concat(new[] { new Utf8ExecutionLimits(0, 0, baseline.Budget.PeakWorkingBytes - 1) });
        foreach (var limit in limits)
        {
            var denied = new Utf8SliceInvocation(limit, Regex.InfiniteMatchTimeout, CancellationToken.None);
            Assert.True(denied.Ledger.TryReserve(7));
            var failed = Utf8SliceRunner.TryMatchAt(program, "ab"u8, 0, starts, ends, true, true, ref denied);
            Assert.False(failed.IsMatch);
            Assert.Equal(limit.MaxWorkSteps == 0 ? Utf8ExecutionLimitKind.WorkingBytes : Utf8ExecutionLimitKind.WorkSteps, failed.Breach);
            Assert.Null(failed.CaptureStarts);
            Assert.Null(failed.CaptureLengths);
            Assert.Equal(7UL, denied.Ledger.ReservedBytes);
            denied.Ledger.Release(7);
        }
        Utf8ScalarCaptureWitness.AssertAsciiAt(program, new Regex("(a)(b)", RegexOptions.CultureInvariant), "ab", 0);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void ScalarCaptureCancellationAndDeadlineReturnEveryLoan(bool expire)
    {
        var text = new string('a', 256 * 1024);
        var program = Utf8ScalarCaptureWitness.Lower("(" + text + ")", RegexOptions.CultureInvariant, 0, 0);
        var input = Encoding.ASCII.GetBytes(text);
        var starts = new int[program.GroupCount]; var ends = new int[program.GroupCount];
        using var source = new CancellationTokenSource();
        long ticks = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
        var invocation = new Utf8SliceInvocation(Utf8ExecutionLimits.Unlimited,
            expire ? TimeSpan.FromMilliseconds(20) : Regex.InfiniteMatchTimeout, source.Token);
        Assert.True(invocation.Ledger.TryReserve(7));
        var entered = false;
        invocation.CheckpointHook = _ => { entered = true; if (expire) ticks = 50; else source.Cancel(); };
        if (expire)
            Assert.Throws<Utf8ExecutionDeadlineExpiredException>(() =>
                Utf8SliceRunner.TryMatchAt(program, input, 0, starts, ends, true, true, ref invocation));
        else
        {
            var failure = Assert.Throws<OperationCanceledException>(() =>
                Utf8SliceRunner.TryMatchAt(program, input, 0, starts, ends, true, true, ref invocation));
            Assert.Equal(source.Token, failure.CancellationToken);
        }
        Assert.True(entered);
        Assert.Equal(7UL, invocation.Ledger.ReservedBytes);
        invocation.Ledger.Release(7);
        Utf8ScalarCaptureWitness.AssertAsciiAt(program, new Regex("(" + text + ")"), text, 0);
    }

    [Fact]
    public void ByteVerifierSharesTheCallersExpiredDeadlineAndRecovers()
    {
        var prepared = Utf8FrontEnd.Compile("(a)(b)", RegexOptions.CultureInvariant);
        long ticks = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
        var deadline = Utf8ExecutionDeadline.Start(TimeSpan.FromMilliseconds(1));
        ticks = 50;
        Assert.Throws<Utf8ExecutionDeadlineExpiredException>(() => Utf8ExecutionInterpreter.TryMatchPrefix(
            "ab"u8, prepared.ExecutionProgram, 0, deadline, out _));
        Assert.True(Utf8ExecutionInterpreter.TryMatchPrefix("ab"u8, prepared.ExecutionProgram,
            0, Utf8ExecutionDeadline.Infinite, out var length));
        Assert.Equal(2, length);
    }

    [Fact]
    public void WarmByteSimplePatternDiscoveryDoesNotAllocate()
    {
        var prepared = Utf8FrontEnd.Compile("(?<x>a(?<x>b)c)(d)", RegexOptions.CultureInvariant);
        Assert.True(Utf8AsciiSimplePatternLowerer.TryCreatePlan(Utf8FrontEnd.Analyze("(?<x>a(?<x>b)c)(d)", RegexOptions.CultureInvariant).SemanticRegex,
            RegexOptions.CultureInvariant, out var plan, out _));
        for (var index = 0; index < 32; index++)
            Assert.True(Utf8ExecutionInterpreter.TryMatchSimplePatternAt("abcd"u8, prepared.ExecutionProgram,
                plan, 0, Utf8ExecutionDeadline.Infinite, out _));
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var index = 0; index < 256; index++)
            Assert.True(Utf8ExecutionInterpreter.TryMatchSimplePatternAt("abcd"u8, prepared.ExecutionProgram,
                plan, 0, Utf8ExecutionDeadline.Infinite, out _));
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }

    [Theory]
    [InlineData(false, false)] [InlineData(false, true)]
    [InlineData(true, false)] [InlineData(true, true)]
    public void SupplementaryCapturesQuantifyAndReplaceWholeScalars(bool compiled, bool rtl)
    {
        var options = RegexOptions.CultureInvariant | (compiled ? RegexOptions.Compiled : RegexOptions.None) |
            (rtl ? RegexOptions.RightToLeft : RegexOptions.None);
        var regex = new Utf8Regex("(?<x>.)+", options);
        var input = "a😀𐐨"u8;
        var match = regex.MatchDetailed(input);
        Assert.True(match.Success);
        Assert.Equal((0, 9), (match.IndexInBytes, match.LengthInBytes));
        var group = match.GetGroup("x");
        Assert.Equal(3, group.CaptureCount);
        Assert.Equal(rtl ? "a" : "𐐨", group.GetValueString());
        Assert.Equal(rtl ? (0, 1) : (5, 4), (group.IndexInBytes, group.LengthInBytes));
        Assert.Equal(rtl ? "<a>" : "<𐐨>", Encoding.UTF8.GetString(regex.Replace(input, "<${x}>")));
    }

    public static IEnumerable<object[]> PublicCases()
    {
        foreach (var pattern in new[] { "(a)(b)", "(?<x>a(?<x>b)c)d", "ab.c", "ab" })
        foreach (var compiled in new[] { false, true })
        foreach (var rtl in new[] { false, true })
            yield return [pattern, RegexOptions.CultureInvariant |
                (compiled ? RegexOptions.Compiled : RegexOptions.None) |
                (rtl ? RegexOptions.RightToLeft : RegexOptions.None)];
    }

    [Theory]
    [MemberData(nameof(PublicCases))]
    public void LiveTokenSelectionRetainsCompleteOperations(string pattern, RegexOptions options)
        => Utf8SliceSparseGroupTests.AssertOwnedOperations(pattern, options,
            ["", "ab", "abxc", "abc", "abcd", "AB", "zzabxcabxc", "\U0001f600abxcab\U0001f600"], options);

    public static IEnumerable<object[]> CheckpointCases()
    {
        foreach (var operation in new[] { "bool", "count", "value", "detailed", "enumerate" })
        foreach (var compiled in new[] { false, true })
        foreach (var expire in new[] { false, true })
            yield return [operation, compiled, expire];
    }

    [Theory]
    [MemberData(nameof(CheckpointCases))]
    public void LiveTokensSelectTheOwnedLoopAndPreserveFailureIdentityAndRecovery(string operation, bool compiled, bool expire)
    {
        using var clock = Utf8DeadlineTestClock.Start();
        const string pattern = "ab.c";
        // This is normally a native fixed-pattern plan. The small ASCII subject
        // reaches step 64 in candidate matching, after validation and mapping.
        var text = string.Concat(Enumerable.Repeat("abxz", 5000)) + "abxc";
        var input = Encoding.ASCII.GetBytes(text);
        var timeout = expire ? TimeSpan.FromMilliseconds(100) : Regex.InfiniteMatchTimeout;
        var options = RegexOptions.CultureInvariant | (compiled ? RegexOptions.Compiled : RegexOptions.None);
        var regex = new Utf8Regex(pattern, options, timeout);
        using var source = new CancellationTokenSource();
        var entered = false;
        var session = Utf8SearchDiagnosticsSession.Start(steps =>
        {
            // Deadline checks run every 2048 VM instructions. Allow that
            // bounded cadence, while detecting a lost authority at 4096.
            if (entered)
            {
                if (!expire || steps >= 4096) throw new InvalidOperationException("The matching loop lost its cancellation or deadline authority.");
                return;
            }
            entered = true;
            if (expire) clock.Advance(150);
            else source.Cancel();
        });
        try
        {
            if (expire)
            {
                var failure = Assert.Throws<RegexMatchTimeoutException>(() => Invoke(regex, input, operation, source.Token));
                Assert.Equal(text, failure.Input);
                Assert.Equal(pattern, failure.Pattern);
                Assert.Equal(timeout, failure.MatchTimeout);
            }
            else
            {
                var failure = Assert.Throws<OperationCanceledException>(() => Invoke(regex, input, operation, source.Token));
                Assert.Equal(source.Token, failure.CancellationToken);
            }
            Assert.True(entered);
        }
        finally { session.Complete(); }
        Assert.True(regex.Inspection.PreparedRegex.ScalarProgram is not null);
        Assert.True(ReferenceEquals(regex.Inspection.PreparedRegex.SemanticRegex.CaptureCatalog, regex.Inspection.PreparedRegex.ScalarProgram!.CaptureCatalog));
        using var recovery = new CancellationTokenSource();
        Assert.True(regex.IsMatch("abxc"u8, recovery.Token));
        Assert.Equal(1, regex.Count("abxc"u8, recovery.Token));
        Assert.True(regex.Match("abxc"u8, recovery.Token).Success);
        Assert.True(regex.MatchDetailed("abxc"u8, recovery.Token).Success);
        Assert.True(regex.EnumerateMatches("abxc"u8, recovery.Token).MoveNext());
    }

    [Theory]
    [InlineData("bool", false)]
    [InlineData("bool", true)]
    [InlineData("count", false)]
    [InlineData("count", true)]
    [InlineData("value", false)]
    [InlineData("value", true)]
    public void CallsWithoutTokensRetainTheirNativeSelection(string operation, bool compiled)
    {
        var input = Encoding.ASCII.GetBytes(string.Concat(Enumerable.Repeat("abxz", 5000)) + "abxc");
        var regex = new Utf8Regex("ab.c", RegexOptions.CultureInvariant | (compiled ? RegexOptions.Compiled : RegexOptions.None));
        var session = Utf8SearchDiagnosticsSession.Start(_ => throw new InvalidOperationException("A no-token native call entered the unit VM."));
        try
        {
            switch (operation)
            {
                case "bool": Assert.True(regex.IsMatch(input)); break;
                case "count": Assert.Equal(1, regex.Count(input)); break;
                case "value": Assert.True(regex.Match(input).Success); break;
            }
        }
        finally { session.Complete(); }
        Assert.True(regex.Inspection.PreparedRegex.ScalarProgram is not null);
        Assert.True(ReferenceEquals(regex.Inspection.PreparedRegex.SemanticRegex.CaptureCatalog, regex.Inspection.PreparedRegex.ScalarProgram!.CaptureCatalog));
    }

    private static void Invoke(Utf8Regex regex, byte[] input, string operation, CancellationToken token)
    {
        switch (operation)
        {
            case "bool": _ = regex.IsMatch(input, token); break;
            case "count": _ = regex.Count(input, token); break;
            case "value": _ = regex.Match(input, token); break;
            case "detailed": _ = regex.MatchDetailed(input, token); break;
            case "enumerate": _ = regex.EnumerateMatches(input, token).MoveNext(); break;
            default: throw new ArgumentOutOfRangeException(nameof(operation));
        }
    }
}
