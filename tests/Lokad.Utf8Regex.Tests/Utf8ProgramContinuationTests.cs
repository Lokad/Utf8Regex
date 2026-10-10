using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.FrontEnd;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8ProgramContinuationTests
{
    private static readonly string[] Patterns = [
        "(a|ab)c", "(a+?)b", "([ab]{1,3})(b)", "(a)+?b", "(?<x>a){0,3}?b",
        "(?:(a)c|(b))d", "(a|ab)+c", "((ab|a)+?)b", "((a?){2})b", "(a?)*b",
        "((a?)*)b", "(a?){0,2}?a", "(?i:(a|ab)c)", "(?m:^(a+?)b$)",
        "(?<x>a(?<x>b)c)(d)", "", "(?m:\\A(a+)b\\Z)", "\\G(a+)b", "(?m:(a+)b\\Z)",
    ];

    public static IEnumerable<object[]> Cases()
    {
        foreach (var family in Enumerable.Range(0, Patterns.Length))
        foreach (var compiled in new[] { false, true }) yield return [family, compiled];
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void EnclosingSuffixesRetainOrderedChoicesAndFinalCaptures(int family, bool compiled)
    {
        var options = RegexOptions.CultureInvariant | (compiled ? RegexOptions.Compiled : RegexOptions.None);
        var prepared = Utf8FrontEnd.Compile(Patterns[family], options);
        Assert.NotNull(prepared.ExecutionProgram);
        // Keep the standing stable interpreted oracle for diagnosed .NET 10.0.12
        // compiled finite-lazy capture defects; compiled owned preparation is tested.
        var oracle = new Regex(Patterns[family], RegexOptions.CultureInvariant);
        var captures = new Utf8CaptureSlots(oracle.GetGroupNumbers().Max() + 1);
        var texts = new List<string> { "", "aab", "aaab", "aaaab", "abc", "ab", "abcd", "bd", "ABc", "\naab\n", "aab\r\n", "aab\nx", "\naab\nx" };
        var layer = new List<string> { "" };
        for (var length = 1; length <= 4; length++)
        {
            layer = layer.SelectMany(static prefix => "abc".Select(ch => prefix + ch)).ToList();
            texts.AddRange(layer);
        }
        foreach (var text in texts)
        for (var start = 0; start <= text.Length; start++)
            AssertAt(prepared.ExecutionProgram, oracle, captures, text, start);
    }

    private static void AssertAt(Utf8ExecutionProgram program, Regex oracle, Utf8CaptureSlots captures, string text, int start)
    {
        var expected = oracle.Match(text, start);
        var invocation = new Utf8SliceInvocation(Utf8ExecutionLimits.Unlimited, Regex.InfiniteMatchTimeout, CancellationToken.None);
        var success = Utf8ExecutionInterpreter.TryMatchProgram(Encoding.ASCII.GetBytes(text), program, 0, start,
            captures, ref invocation, out var end, out var breach);
        Assert.Equal(Utf8ExecutionLimitKind.None, breach);
        Assert.Equal(expected.Success && expected.Index == start, success);
        Assert.Equal(success ? expected.Index + expected.Length : 0, end);
        Assert.Equal(0UL, invocation.Ledger.ReservedBytes);
        foreach (var number in oracle.GetGroupNumbers())
        {
            var present = captures.TryGet(number, out var actualStart, out var actualLength);
            Assert.Equal(success && expected.Groups[number].Success, present);
            if (!present) continue;
            Assert.Equal(expected.Groups[number].Index, actualStart);
            Assert.Equal(expected.Groups[number].Length, actualLength);
        }
    }

    private static Utf8ExecutionNode Deep(Utf8ExecutionNode node, int depth, int shape)
    {
        for (var index = 0; index < depth; index++)
            node = new Utf8ExecutionNode(shape switch { 0 => Utf8ExecutionNodeKind.Concatenate, 1 => Utf8ExecutionNodeKind.Group, _ => Utf8ExecutionNodeKind.Capture },
                RegexOptions.CultureInvariant, [node], "", '\0', 0, 1, 1);
        return node;
    }

    public static IEnumerable<object[]> DeepCases()
    {
        foreach (var depth in new[] { 65, 16385 })
        foreach (var shape in Enumerable.Range(0, 3))
        foreach (var family in new[] { 0, 1, 3, 8 })
        foreach (var compiled in new[] { false, true }) yield return [depth, shape, family, compiled];
    }

    [Theory]
    [MemberData(nameof(DeepCases))]
    public void DeepRuntimeContinuationsMatchWithoutClrRecursion(int depth, int shape, int family, bool compiled)
    {
        var prepared = Utf8FrontEnd.Compile(Patterns[family], RegexOptions.CultureInvariant | (compiled ? RegexOptions.Compiled : RegexOptions.None));
        Assert.NotNull(prepared.ExecutionTree);
        var program = Utf8ExecutionProgramLowerer.Lower(new Utf8ExecutionTree(Deep(prepared.ExecutionTree.Root, depth, shape)));
        Assert.NotNull(program);
        var oracle = new Regex(Patterns[family], RegexOptions.CultureInvariant);
        var captures = new Utf8CaptureSlots(oracle.GetGroupNumbers().Max() + 1);
        AssertAt(program, oracle, captures, family switch { 0 => "abc", 8 => "b", _ => "aab" }, 0);
        AssertAt(program, oracle, captures, "aaad", 0);
    }

    public static IEnumerable<object[]> ResourceCases()
    {
        foreach (var depth in new[] { 65, 4097 })
        foreach (var shape in Enumerable.Range(0, 3))
        foreach (var family in new[] { 0, 3 }) yield return [depth, shape, family];
    }

    [Theory]
    [MemberData(nameof(ResourceCases))]
    public void EveryDeniedContinuationOrMutationReleasesStorageAndRecovers(int depth, int shape, int family)
    {
        var prepared = Utf8FrontEnd.Compile(Patterns[family], RegexOptions.CultureInvariant);
        Assert.NotNull(prepared.ExecutionTree);
        var program = Utf8ExecutionProgramLowerer.Lower(new Utf8ExecutionTree(Deep(prepared.ExecutionTree.Root, depth, shape)));
        Assert.NotNull(program);
        var input = Encoding.ASCII.GetBytes(family == 0 ? "abc" : "aab");
        var captures = new Utf8CaptureSlots(2);
        var baseline = new Utf8SliceInvocation(Utf8ExecutionLimits.Unlimited, Regex.InfiniteMatchTimeout, CancellationToken.None);
        Assert.True(Utf8ExecutionInterpreter.TryMatchProgram(input, program, 0, 0, captures, ref baseline, out _, out _));
        Assert.True(baseline.Budget.PeakDepth > depth);
        Assert.True(baseline.Budget.PeakWorkingBytes > 0);
        Assert.Equal(0UL, baseline.Ledger.ReservedBytes);
        var work = depth == 65 ? Enumerable.Range(1, checked((int)baseline.Budget.Steps - 1)).Select(static value => (ulong)value)
            : new[] { 1UL, 2UL, 63UL, 64UL, 127UL, 512UL, 4095UL, 4096UL, baseline.Budget.Steps - 1 };
        var depths = depth == 65 ? Enumerable.Range(1, checked((int)baseline.Budget.PeakDepth - 1)).Select(static value => (uint)value)
            : new[] { 1U, 2U, 63U, 64U, (uint)depth, baseline.Budget.PeakDepth - 1 };
        foreach (var limits in work.Select(static limit => new Utf8ExecutionLimits(limit, 0, 0))
            .Concat(depths.Select(static limit => new Utf8ExecutionLimits(0, limit, 0)))
            .Concat(new[] { new Utf8ExecutionLimits(0, 0, 1), new Utf8ExecutionLimits(0, 0, baseline.Budget.PeakWorkingBytes - 1) }))
        {
            var denied = new Utf8SliceInvocation(limits, Regex.InfiniteMatchTimeout, CancellationToken.None);
            Assert.False(Utf8ExecutionInterpreter.TryMatchProgram(input, program, 0, 0, captures, ref denied, out var end, out var breach));
            Assert.Equal(limits.MaxWorkSteps != 0 ? Utf8ExecutionLimitKind.WorkSteps : limits.MaxDepth != 0 ? Utf8ExecutionLimitKind.Depth : Utf8ExecutionLimitKind.WorkingBytes, breach);
            Assert.Equal(0, end);
            Assert.Equal(0UL, denied.Ledger.ReservedBytes);
            foreach (var slot in Enumerable.Range(0, 2)) Assert.False(captures.TryGet(slot, out _, out _));
        }
        using var source = new CancellationTokenSource();
        var canceled = new Utf8SliceInvocation(Utf8ExecutionLimits.Unlimited, Regex.InfiniteMatchTimeout, source.Token);
        canceled.CheckpointHook = _ => source.Cancel();
        var failure = Assert.Throws<OperationCanceledException>(() =>
            Utf8ExecutionInterpreter.TryMatchProgram(input, program, 0, 0, captures, ref canceled, out _, out _));
        Assert.Equal(source.Token, failure.CancellationToken);
        Assert.Equal(0UL, canceled.Ledger.ReservedBytes);
        foreach (var slot in Enumerable.Range(0, 2)) Assert.False(captures.TryGet(slot, out _, out _));
        var recovery = new Utf8SliceInvocation(Utf8ExecutionLimits.Unlimited, Regex.InfiniteMatchTimeout, CancellationToken.None);
        Assert.True(recovery.Ledger.TryReserve(7));
        Assert.True(Utf8ExecutionInterpreter.TryMatchProgram(input, program, 0, 0, captures, ref recovery, out _, out _));
        Assert.Equal(7UL, recovery.Ledger.ReservedBytes);
        recovery.Ledger.Release(7);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void LongLiteralChecksClearCapturesAndReturnEveryLoan(bool expire)
    {
        var text = new string('a', 256 * 1024);
        var prepared = Utf8FrontEnd.Compile("(" + text + ")", RegexOptions.CultureInvariant);
        Assert.NotNull(prepared.ExecutionProgram);
        var input = Encoding.ASCII.GetBytes(text);
        var captures = new Utf8CaptureSlots(2);
        using var source = new CancellationTokenSource();
        // Expire at the admitted checkpoint itself. A wall clock can expire
        // during JIT or scheduling before the hook, losing this witness's entry.
        long ticks = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
        var invocation = new Utf8SliceInvocation(Utf8ExecutionLimits.Unlimited,
            expire ? TimeSpan.FromMilliseconds(20) : Regex.InfiniteMatchTimeout, source.Token);
        var entered = false;
        invocation.CheckpointHook = _ => { entered = true; if (expire) ticks = 50; else source.Cancel(); };
        if (expire)
            Assert.Throws<Utf8ExecutionDeadlineExpiredException>(() =>
                Utf8ExecutionInterpreter.TryMatchProgram(input, prepared.ExecutionProgram, 0, 0, captures, ref invocation, out _, out _));
        else
        {
            var failure = Assert.Throws<OperationCanceledException>(() =>
                Utf8ExecutionInterpreter.TryMatchProgram(input, prepared.ExecutionProgram, 0, 0, captures, ref invocation, out _, out _));
            Assert.Equal(source.Token, failure.CancellationToken);
        }
        Assert.True(entered);
        Assert.Equal(0UL, invocation.Ledger.ReservedBytes);
        foreach (var slot in Enumerable.Range(0, 2)) Assert.False(captures.TryGet(slot, out _, out _));
    }

    [Fact]
    public void AggregateDenialSkipsAllocationAndFiniteStorageDoesNotGrowRetainedPools()
    {
        var invocation = new Utf8SliceInvocation(new Utf8ExecutionLimits(0, 0, 1), Regex.InfiniteMatchTimeout, CancellationToken.None);
        var buffer = new Utf8InvocationStateBuffer<long>();
        var before = GC.GetAllocatedBytesForCurrentThread();
        var breach = buffer.TryPush(1, ref invocation);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(Utf8ExecutionLimitKind.WorkingBytes, breach);
        Assert.Equal(0, allocated);
        Assert.Equal(0UL, invocation.Ledger.ReservedBytes);
        var retained = Utf8ExactStatePool<WideState>.RetainedBytes;
        foreach (var capacity in new[] { 16, 32, 64, 128, 256, 512 })
        {
            var items = Utf8ExactStatePool<WideState>.Rent(capacity, false);
            Assert.Equal(capacity, items.Length);
            Utf8ExactStatePool<WideState>.Return(items, false);
        }
        Assert.Equal(retained, Utf8ExactStatePool<WideState>.RetainedBytes);
        foreach (var capacity in new[] { 16, 32, 64, 128, 256, 512 })
        {
            var items = Utf8ExactStatePool<WideState>.Rent(capacity, true);
            Assert.Equal(capacity, items.Length);
            Utf8ExactStatePool<WideState>.Return(items, true);
            Assert.InRange(Utf8ExactStatePool<WideState>.RetainedBytes, 0, Utf8ExactStatePool<WideState>.MaximumRetainedBytes);
        }
        Assert.Equal(64, Unsafe.SizeOf<WideState>());
    }

    private readonly record struct WideState(long A, long B, long C, long D, long E, long F, long G, long H);

    public static IEnumerable<object[]> PublicCases()
    {
        foreach (var pattern in new[] { "(a|ab)c", "(a+?)b", "(a)+?b", "(?<x>a){0,3}?b" })
        foreach (var compiled in new[] { false, true })
        foreach (var rtl in new[] { false, true })
            yield return [pattern, RegexOptions.CultureInvariant | (compiled ? RegexOptions.Compiled : RegexOptions.None) |
                (rtl ? RegexOptions.RightToLeft : RegexOptions.None)];
    }

    [Theory]
    [MemberData(nameof(PublicCases))]
    public void PublicOperationsRetainHistoriesAndBothTokenSelections(string pattern, RegexOptions options)
    {
        var texts = new[] { "", "ab", "abc", "aab", "aaab", "aaaab", "abc aab abc", "\U0001f600aab\U0001f600abc" };
        var oracleOptions = options & ~RegexOptions.Compiled; // Standing interpreted finite-lazy bug disposition.
        Utf8SliceSparseGroupTests.AssertOwnedOperations(pattern, options, texts, oracleOptions);
        var oracle = new Regex(pattern, oracleOptions);
        var regex = new Utf8Regex(pattern, options);
        foreach (var text in texts)
        {
            var input = Encoding.UTF8.GetBytes(text);
            var expected = oracle.Match(text);
            Assert.Equal(oracle.IsMatch(text), regex.IsMatch(input));
            Assert.Equal(oracle.Count(text), regex.Count(input));
            var actual = regex.Match(input);
            Assert.Equal(expected.Success, actual.Success);
            if (actual.Success)
            {
                Assert.Equal(expected.Index, actual.IndexInUtf16);
                Assert.Equal(expected.Length, actual.LengthInUtf16);
            }
            var detailed = regex.MatchDetailed(input);
            Assert.Equal(expected.Success, detailed.Success);
            foreach (var number in oracle.GetGroupNumbers())
            {
                var group = detailed.GetGroup(number);
                Assert.Equal(expected.Groups[number].Success, group.Success);
                Assert.Equal(expected.Groups[number].Value, group.GetValueString());
                Assert.Equal(expected.Groups[number].Captures.Count, group.CaptureCount);
            }
        }
        Assert.True(regex.Inspection.PreparedRegex.ScalarProgram is not null);
        Assert.True(ReferenceEquals(regex.Inspection.PreparedRegex.SemanticRegex.RuntimeTree!.CaptureCatalog, regex.Inspection.PreparedRegex.ScalarProgram!.CaptureCatalog));
    }

    [Theory]
    [InlineData("\\G(a)(b)", false)]
    [InlineData("\\G(a)(b)", true)]
    [InlineData("(?m:\\A(a)(b))", false)]
    [InlineData("(?m:\\A(a)(b))", true)]
    [InlineData("(?m:(a)(b)\\Z)", false)]
    [InlineData("(?m:(a)(b)\\Z)", true)]
    public void DeterministicAndGeneralAnchorsAgreeWithAbsoluteAndCallerOriginSemantics(string pattern, bool compiled)
    {
        var prepared = Utf8FrontEnd.Compile(pattern, RegexOptions.CultureInvariant | (compiled ? RegexOptions.Compiled : RegexOptions.None));
        Assert.NotNull(prepared.ExecutionProgram);
        Assert.NotNull(prepared.ExecutionProgram.DeterministicCaptureLayout);
        var oracle = new Regex(pattern, RegexOptions.CultureInvariant);
        var captures = new Utf8CaptureSlots(3);
        foreach (var text in new[] { "", "ab", "xab", "\nab", "ab\n", "ab\nx", "\nab\nx" })
        for (var start = 0; start <= text.Length; start++)
        {
            AssertAt(prepared.ExecutionProgram, oracle, captures, text, start);
            var invocation = new Utf8SliceInvocation(Utf8ExecutionLimits.Unlimited, Regex.InfiniteMatchTimeout, CancellationToken.None);
            var expected = oracle.Match(text, start);
            var hit = Utf8ExecutionInterpreter.TryMatchDeterministicProgram(Encoding.ASCII.GetBytes(text), prepared.ExecutionProgram,
                start, captures, ref invocation, out var length, out var breach);
            Assert.Equal(Utf8ExecutionLimitKind.None, breach);
            Assert.Equal(expected.Success && expected.Index == start, hit);
            Assert.Equal(hit ? expected.Length : 0, length);
            foreach (var number in oracle.GetGroupNumbers())
            {
                var present = captures.TryGet(number, out var index, out var width);
                Assert.Equal(hit && expected.Groups[number].Success, present);
                if (!present) continue;
                Assert.Equal(expected.Groups[number].Index, index);
                Assert.Equal(expected.Groups[number].Length, width);
            }
        }
        if (!pattern.StartsWith("\\G", StringComparison.Ordinal)) return;
        var origin = new Utf8SliceInvocation(Utf8ExecutionLimits.Unlimited, Regex.InfiniteMatchTimeout, CancellationToken.None);
        origin.AnchorOriginByte = 0;
        Assert.False(Utf8ExecutionInterpreter.TryMatchProgram("xab"u8, prepared.ExecutionProgram, 0, 1, captures, ref origin, out _, out _));
        Assert.False(Utf8ExecutionInterpreter.TryMatchDeterministicProgram("xab"u8, prepared.ExecutionProgram, 1, captures, ref origin, out _, out _));
        origin.AnchorOriginByte = 1;
        Assert.True(Utf8ExecutionInterpreter.TryMatchProgram("xab"u8, prepared.ExecutionProgram, 0, 1, captures, ref origin, out _, out _));
        Assert.True(Utf8ExecutionInterpreter.TryMatchDeterministicProgram("xab"u8, prepared.ExecutionProgram, 1, captures, ref origin, out _, out _));
        Assert.Equal(0UL, origin.Ledger.ReservedBytes);
    }
}
