using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Diagnostics;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.FrontEnd;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8DeterministicCaptureContinuationTests
{
    private static readonly string[] Patterns =
    [
        "(a)(b)", "((ab)(c))", "(?<x>a(?<x>b)c)(d)", "^(?<x>ab)[0-9]$",
        "(?m:^(a)(b)$)", "(?i:(a)(bc))", "(a)([A-Z])", "(a)(b$)",
    ];

    private static Utf8ExecutionNode Deep(Utf8ExecutionNode node, int depth, int shape)
    {
        for (var index = 0; index < depth; index++)
            node = new Utf8ExecutionNode(shape switch { 0 => Utf8ExecutionNodeKind.Concatenate, 1 => Utf8ExecutionNodeKind.Group, _ => Utf8ExecutionNodeKind.Capture },
                RegexOptions.CultureInvariant, [node], "", '\0', 0, 1, 1);
        return node;
    }

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
    public void DeepCachedLayoutsAndIterativeMatchingRetainFinalGroups(int depth, int shape, int family, bool compiled)
    {
        var options = RegexOptions.CultureInvariant | (compiled ? RegexOptions.Compiled : RegexOptions.None);
        var prepared = Utf8FrontEnd.Compile(Patterns[family], options);
        Assert.NotNull(prepared.ExecutionTree);
        Assert.NotNull(prepared.ExecutionProgram);
        var program = Utf8ExecutionProgramLowerer.Lower(new Utf8ExecutionTree(Deep(prepared.ExecutionTree.Root, depth, shape)));
        Assert.NotNull(program);
        var layout = Assert.IsType<Utf8DeterministicCaptureLayout>(program.DeterministicCaptureLayout);
        var cached = Assert.IsType<Utf8DeterministicCaptureLayout>(program.DeterministicCaptureLayout);
        Assert.Same(layout.Captures, cached.Captures);
        var baseline = Assert.IsType<Utf8DeterministicCaptureLayout>(prepared.ExecutionProgram.DeterministicCaptureLayout);
        Assert.Equal(baseline.MatchLength, layout.MatchLength);
        Assert.Equal(baseline.Captures.OrderBy(static pair => pair.Key), layout.Captures.OrderBy(static pair => pair.Key));
        var oracle = new Regex(Patterns[family], options);
        var numbers = oracle.GetGroupNumbers();
        var captures = new Utf8CaptureSlots(numbers.Max() + 1);
        foreach (var subject in new[] { "", "ab", "abc", "abcd", "xxabyy", "aZ", "ABc", "\nab\n", "ab\n", "ab\r\n", "ab!\n", "ab1" })
        for (var start = 0; start <= subject.Length; start++)
        {
            var input = Encoding.ASCII.GetBytes(subject);
            var expected = oracle.Match(subject, start);
            var invocation = new Utf8SliceInvocation(Utf8ExecutionLimits.Unlimited, Regex.InfiniteMatchTimeout, CancellationToken.None);
            var success = Utf8ExecutionInterpreter.TryMatchDeterministicProgram(input, program, start, captures, ref invocation, out var length, out var breach);
            Assert.Equal(Utf8ExecutionLimitKind.None, breach);
            Assert.Equal(expected.Success && expected.Index == start, success);
            Assert.Equal(0UL, invocation.Ledger.ReservedBytes);
            foreach (var number in numbers)
            {
                var present = captures.TryGet(number, out var actualStart, out var actualLength);
                Assert.Equal(success && expected.Groups[number].Success, present);
                if (!present) continue;
                Assert.Equal(expected.Groups[number].Index, actualStart);
                Assert.Equal(expected.Groups[number].Length, actualLength);
            }
            if (success) Assert.Equal(expected.Length, length);
            else Assert.Equal(0, length);
        }
    }

    [Theory]
    [InlineData(65, 0)]
    [InlineData(65, 1)]
    [InlineData(65, 2)]
    [InlineData(4097, 0)]
    [InlineData(4097, 1)]
    [InlineData(4097, 2)]
    public void DeepMatchingUsesConstantDepthAndBorrowedStorageAndClearsEveryFailedAttempt(int depth, int shape)
    {
        var prepared = Utf8FrontEnd.Compile("(a)(b)", RegexOptions.CultureInvariant);
        Assert.NotNull(prepared.ExecutionTree);
        var program = Utf8ExecutionProgramLowerer.Lower(new Utf8ExecutionTree(Deep(prepared.ExecutionTree.Root, depth, shape)));
        Assert.NotNull(program);
        var captures = new Utf8CaptureSlots(3);
        var input = "ab"u8.ToArray();
        var baseline = new Utf8SliceInvocation(new Utf8ExecutionLimits(0, 1, 1), Regex.InfiniteMatchTimeout, CancellationToken.None);
        Assert.True(Utf8ExecutionInterpreter.TryMatchDeterministicProgram(input, program, 0, captures, ref baseline, out var length, out var breach));
        Assert.Equal(2, length);
        Assert.Equal(Utf8ExecutionLimitKind.None, breach);
        Assert.Equal(1U, baseline.Budget.PeakDepth);
        Assert.Equal(0UL, baseline.Ledger.ReservedBytes);
        Assert.Equal(0UL, baseline.Budget.PeakWorkingBytes);
        var cutoffs = depth == 65 ? Enumerable.Range(1, checked((int)baseline.Budget.Steps - 1)).Select(static value => (ulong)value)
            : new[] { 1UL, 2UL, 63UL, 64UL, 127UL, 128UL, 4095UL, 4096UL, baseline.Budget.Steps - 1 };
        foreach (var work in cutoffs)
        {
            var limited = new Utf8SliceInvocation(new Utf8ExecutionLimits(work, 1, 1), Regex.InfiniteMatchTimeout, CancellationToken.None);
            Assert.False(Utf8ExecutionInterpreter.TryMatchDeterministicProgram(input, program, 0, captures, ref limited, out length, out breach));
            Assert.Equal(Utf8ExecutionLimitKind.WorkSteps, breach);
            Assert.Equal(0, length);
            foreach (var slot in Enumerable.Range(0, 3)) Assert.False(captures.TryGet(slot, out _, out _));
            Assert.Equal(0UL, limited.Ledger.ReservedBytes);
        }
        using var source = new CancellationTokenSource();
        var canceled = new Utf8SliceInvocation(Utf8ExecutionLimits.Unlimited, Regex.InfiniteMatchTimeout, source.Token);
        canceled.CheckpointHook = steps => { if (steps >= 64) source.Cancel(); };
        var failure = Assert.Throws<OperationCanceledException>(() =>
            Utf8ExecutionInterpreter.TryMatchDeterministicProgram(input, program, 0, captures, ref canceled, out _, out _));
        Assert.Equal(source.Token, failure.CancellationToken);
        foreach (var slot in Enumerable.Range(0, 3)) Assert.False(captures.TryGet(slot, out _, out _));
        Assert.Equal(0UL, canceled.Ledger.ReservedBytes);
        var recovered = new Utf8SliceInvocation(new Utf8ExecutionLimits(0, 1, 7), Regex.InfiniteMatchTimeout, CancellationToken.None);
        Assert.True(recovered.Ledger.TryReserve(7)); // An unrelated caller-owned lease remains untouched.
        Assert.True(Utf8ExecutionInterpreter.TryMatchDeterministicProgram(input, program, 0, captures, ref recovered, out _, out _));
        Assert.Equal(7UL, recovered.Ledger.ReservedBytes);
        recovered.Ledger.Release(7);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void LongLiteralBlocksPollCancellationAndDeadlineAndClearUnpublishedCaptures(bool expire)
    {
        var text = new string('a', 256 * 1024);
        var prepared = Utf8FrontEnd.Compile("(" + text + ")", RegexOptions.CultureInvariant);
        Assert.NotNull(prepared.ExecutionProgram);
        var input = Encoding.ASCII.GetBytes(text);
        var captures = new Utf8CaptureSlots(2);
        using var source = new CancellationTokenSource();
        var invocation = new Utf8SliceInvocation(Utf8ExecutionLimits.Unlimited,
            expire ? TimeSpan.FromMilliseconds(20) : Regex.InfiniteMatchTimeout, source.Token);
        var entered = false;
        invocation.CheckpointHook = _ => { entered = true; if (expire) Thread.Sleep(50); else source.Cancel(); };
        if (expire)
            Assert.Throws<Utf8ExecutionDeadlineExpiredException>(() =>
                Utf8ExecutionInterpreter.TryMatchDeterministicProgram(input, prepared.ExecutionProgram, 0, captures, ref invocation, out _, out _));
        else
        {
            var failure = Assert.Throws<OperationCanceledException>(() =>
                Utf8ExecutionInterpreter.TryMatchDeterministicProgram(input, prepared.ExecutionProgram, 0, captures, ref invocation, out _, out _));
            Assert.Equal(source.Token, failure.CancellationToken);
        }
        Assert.True(entered);
        Assert.False(captures.TryGet(0, out _, out _));
        Assert.False(captures.TryGet(1, out _, out _));
        Assert.Equal(0UL, invocation.Ledger.ReservedBytes);
    }

    [Fact]
    public void LegacyDeterministicEntrySharesTheCallersExpiredDeadlineAndRecovers()
    {
        var prepared = Utf8FrontEnd.Compile("(a)(b)", RegexOptions.CultureInvariant);
        Assert.True(Utf8AsciiSimplePatternLowerer.TryCreatePlan(Utf8FrontEnd.Analyze("(a)(b)", RegexOptions.CultureInvariant).SemanticRegex,
            RegexOptions.CultureInvariant, out var plan, out _));
        var captures = new Utf8CaptureSlots(3);
        var deadline = Utf8ExecutionDeadline.Start(TimeSpan.FromMilliseconds(1));
        // TickCount64 can advance in roughly 16 ms quanta on Windows. Wait
        // beyond that resolution before testing the existing authority.
        Thread.Sleep(50);
        Assert.Throws<Utf8ExecutionDeadlineExpiredException>(() => Utf8ExecutionInterpreter.TryMatchSimplePatternAt(
            "ab"u8, prepared.ExecutionProgram, plan, 0, captures, deadline, out _));
        Assert.True(Utf8ExecutionInterpreter.TryMatchSimplePatternAt("ab"u8, prepared.ExecutionProgram,
            plan, 0, captures, Utf8ExecutionDeadline.Infinite, out var length));
        Assert.Equal(2, length);
    }

    [Fact]
    public void WarmDeterministicDiscoveryAndCaptureProjectionDoNotAllocate()
    {
        var prepared = Utf8FrontEnd.Compile("(?<x>a(?<x>b)c)(d)", RegexOptions.CultureInvariant);
        Assert.True(Utf8AsciiSimplePatternLowerer.TryCreatePlan(Utf8FrontEnd.Analyze("(?<x>a(?<x>b)c)(d)", RegexOptions.CultureInvariant).SemanticRegex,
            RegexOptions.CultureInvariant, out var plan, out _));
        var captures = new Utf8CaptureSlots(3);
        for (var index = 0; index < 32; index++)
            Assert.True(Utf8ExecutionInterpreter.TryMatchSimplePatternAt("abcd"u8, prepared.ExecutionProgram,
                plan, 0, captures, Utf8ExecutionDeadline.Infinite, out _));
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var index = 0; index < 256; index++)
            Assert.True(Utf8ExecutionInterpreter.TryMatchSimplePatternAt("abcd"u8, prepared.ExecutionProgram,
                plan, 0, captures, Utf8ExecutionDeadline.Infinite, out _));
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
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
            if (expire) Thread.Sleep(150);
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
        Assert.True(ReferenceEquals(regex.Inspection.PreparedRegex.SemanticRegex.RuntimeTree!.CaptureCatalog, regex.Inspection.PreparedRegex.ScalarProgram!.CaptureCatalog));
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
        Assert.True(ReferenceEquals(regex.Inspection.PreparedRegex.SemanticRegex.RuntimeTree!.CaptureCatalog, regex.Inspection.PreparedRegex.ScalarProgram!.CaptureCatalog));
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
