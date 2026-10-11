using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.FrontEnd;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8LinearRuntimeContinuationTests
{
    internal static Utf8ByteSafeLinearVerifierProgram Nested(int depth, bool failedTrial)
    {
        Utf8ByteSafeLinearVerifierStep[] steps = [Utf8ByteSafeLinearVerifierStep.MatchText("ab"u8.ToArray())];
        if (failedTrial) steps = [Utf8ByteSafeLinearVerifierStep.LoopProgram(steps, 1, -1), Utf8ByteSafeLinearVerifierStep.MatchText("ac"u8.ToArray())];
        for (var index = 0; index < depth; index++)
            steps = failedTrial
                ? [Utf8ByteSafeLinearVerifierStep.LoopProgram(steps, 1, 1), Utf8ByteSafeLinearVerifierStep.MatchByte((byte)'c')]
                : [Utf8ByteSafeLinearVerifierStep.LoopProgram(steps, 1, 1)];
        return new([.. steps, Utf8ByteSafeLinearVerifierStep.MatchByte((byte)'x'), Utf8ByteSafeLinearVerifierStep.Accept()]);
    }

    [Theory]
    [InlineData(65, false)]
    [InlineData(65, true)]
    [InlineData(16385, false)]
    [InlineData(16385, true)]
    public void DeepBodiesCommitSuccessfulIterationsAndDiscardFailedTrialBytes(int depth, bool failedTrial)
    {
        var program = Nested(depth, failedTrial);
        var text = (failedTrial ? "ababac" + new string('c', depth) : "ab") + "x";
        foreach (var prefix in new[] { "", "zz" })
        {
            Assert.True(program.TryMatch(Encoding.ASCII.GetBytes(prefix + text), prefix.Length, out var length, out var fallback));
            Assert.Equal(text.Length, length);
            Assert.False(fallback);
            Assert.False(program.TryMatch(Encoding.ASCII.GetBytes(prefix + text[..^1] + "y"), prefix.Length, out length, out fallback));
            Assert.Equal(0, length);
            Assert.False(fallback);
        }
    }

    public static IEnumerable<object[]> OracleCases()
    {
        foreach (var pattern in new[] { @"^ab(?:c[ab]){1,3}x$", @"^ab(?:ca|cb){0,3}x$",
            @"^ab(?:ca|cb)?x$", @"^ab[ab]+x$", @"\bab[ab]{2}\b", @"^ab(?:c[ab]){2}$" })
        foreach (var compiled in new[] { false, true }) yield return [pattern, compiled];
    }

    [Theory]
    [MemberData(nameof(OracleCases))]
    public void AdmittedProgramsKeepOrderedChoicesRangesAnchorsAndRepetition(string pattern, bool compiled)
    {
        var options = RegexOptions.CultureInvariant | (compiled ? RegexOptions.Compiled : RegexOptions.None);
        var prepared = Utf8FrontEnd.Compile(pattern, options);
        var outcome = Utf8ByteSafeLinearVerifierProgram.Compile(prepared.ExecutionTree);
        Assert.True(outcome.Succeeded);
        var oracle = new Regex(pattern, options);
        var texts = new List<string> { "abca", "abcacbx", "abcacbcax", "abcbx", "abx", "abca\n", "ababx", "zabab z" };
        var frontier = new List<string> { "" };
        for (var size = 0; size <= 4; size++)
        {
            texts.AddRange(frontier);
            frontier = frontier.SelectMany(static value => "abcx".Select(c => value + c)).ToList();
        }
        foreach (var text in texts)
        foreach (var start in Enumerable.Range(0, text.Length + 1))
        {
            var expected = oracle.Match(text, start);
            var found = outcome.Program.TryMatch(Encoding.ASCII.GetBytes(text), start, out var length, out var fallback);
            Assert.False(fallback);
            Assert.Equal(expected.Success && expected.Index == start, found);
            Assert.Equal(found ? expected.Length : 0, length);
        }
    }

    [Theory]
    [InlineData(65)]
    [InlineData(4097)]
    public void EveryWorkDepthAndStorageCutoffReturnsLoansAndPreservesCallerLeases(int depth)
    {
        var program = Nested(depth, false);
        var input = "abx"u8.ToArray();
        var baseline = new Utf8SliceInvocation(Utf8ExecutionLimits.Unlimited, Regex.InfiniteMatchTimeout, CancellationToken.None);
        Assert.True(program.TryMatch(input, 0, ref baseline, out var length, out _, out var breach));
        Assert.Equal(3, length);
        Assert.Equal(Utf8ExecutionLimitKind.None, breach);
        Assert.Equal((uint)depth + 1, baseline.Budget.PeakDepth);
        Assert.Equal(0UL, baseline.Ledger.ReservedBytes);
        var work = depth == 65 ? Enumerable.Range(1, checked((int)baseline.Budget.Steps - 1)).Select(static x => (ulong)x)
            : new[] { 1UL, 63UL, 64UL, 4095UL, 4096UL, baseline.Budget.Steps / 2, baseline.Budget.Steps - 1 };
        var depths = depth == 65 ? Enumerable.Range(1, depth).Select(static x => (uint)x)
            : new[] { 1U, 63U, 64U, 4096U, (uint)depth };
        var limits = work.Select(static x => new Utf8ExecutionLimits(x, 0, 0))
            .Concat(depths.Select(static x => new Utf8ExecutionLimits(0, x, 0)))
            .Concat(new[] { new Utf8ExecutionLimits(0, 0, 1), new Utf8ExecutionLimits(0, 0, baseline.Budget.PeakWorkingBytes - 1) });
        foreach (var limit in limits)
        {
            var denied = new Utf8SliceInvocation(limit, Regex.InfiniteMatchTimeout, CancellationToken.None);
            Assert.False(program.TryMatch(input, 0, ref denied, out length, out var fallback, out breach));
            Assert.Equal(0, length);
            Assert.False(fallback);
            Assert.Equal(limit.MaxWorkSteps != 0 ? Utf8ExecutionLimitKind.WorkSteps : limit.MaxDepth != 0
                ? Utf8ExecutionLimitKind.Depth : Utf8ExecutionLimitKind.WorkingBytes, breach);
            Assert.Equal(0UL, denied.Ledger.ReservedBytes);
        }
        var recovery = new Utf8SliceInvocation(Utf8ExecutionLimits.Unlimited, Regex.InfiniteMatchTimeout, CancellationToken.None);
        Assert.True(recovery.Ledger.TryReserve(7));
        Assert.True(program.TryMatch(input, 0, ref recovery, out _, out _, out _));
        Assert.Equal(7UL, recovery.Ledger.ReservedBytes);
        recovery.Ledger.Release(7);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CancellationDuringDescentOrUnwindKeepsTokenIdentityAndReturnsState(bool unwind)
    {
        var program = Nested(4097, false);
        var input = "abx"u8.ToArray();
        var baseline = new Utf8SliceInvocation(Utf8ExecutionLimits.Unlimited, Regex.InfiniteMatchTimeout, CancellationToken.None);
        Assert.True(program.TryMatch(input, 0, ref baseline, out _, out _, out _));
        using var source = new CancellationTokenSource();
        var canceled = new Utf8SliceInvocation(Utf8ExecutionLimits.Unlimited, Regex.InfiniteMatchTimeout, source.Token);
        canceled.CheckpointHook = step => { if (!unwind || step > baseline.Budget.Steps * 3 / 4) source.Cancel(); };
        var failure = Assert.Throws<OperationCanceledException>(() => program.TryMatch(input, 0, ref canceled, out _, out _, out _));
        Assert.Equal(source.Token, failure.CancellationToken);
        Assert.Equal(0UL, canceled.Ledger.ReservedBytes);
        Assert.True(program.TryMatch(input, 0, out _));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    public void LongTextAlternativesAndByteLoopsPollInsideAtoms(int family)
    {
        var input = Enumerable.Repeat((byte)'a', 256 * 1024).ToArray();
        var step = family switch
        {
            0 => Utf8ByteSafeLinearVerifierStep.MatchText(input),
            1 => Utf8ByteSafeLinearVerifierStep.LoopByte((byte)'a', 1, -1),
            2 => Utf8ByteSafeLinearVerifierStep.MatchAnyText([input]),
            3 => Utf8ByteSafeLinearVerifierStep.LoopText("a"u8.ToArray(), 1, -1),
            4 => Utf8ByteSafeLinearVerifierStep.LoopAsciiClass(AsciiCharClass.ForByte((byte)'a'), 1, -1),
            5 => Utf8ByteSafeLinearVerifierStep.LoopAnyText(["a"u8.ToArray()], 1, -1),
            _ => Utf8ByteSafeLinearVerifierStep.LoopAsciiClass(new AsciiCharClass(ulong.MaxValue, ulong.MaxValue), 1, -1),
        };
        var program = new Utf8ByteSafeLinearVerifierProgram([step, Utf8ByteSafeLinearVerifierStep.Accept()]);
        using var source = new CancellationTokenSource();
        var invocation = new Utf8SliceInvocation(Utf8ExecutionLimits.Unlimited, Regex.InfiniteMatchTimeout, source.Token);
        invocation.CheckpointHook = _ => source.Cancel();
        var failure = Assert.Throws<OperationCanceledException>(() => program.TryMatch(input, 0, ref invocation, out _, out _, out _));
        Assert.Equal(source.Token, failure.CancellationToken);
        Assert.Equal(0UL, invocation.Ledger.ReservedBytes);
    }

    [Fact]
    public void DeadlineWrapperRetainsTheCallersAlreadyExpiredDeadline()
    {
        var program = Nested(65, false);
        var deadline = Utf8ExecutionDeadline.Start(TimeSpan.FromMilliseconds(10));
        Thread.Sleep(30);
        Assert.Throws<Utf8ExecutionDeadlineExpiredException>(() => program.TryMatch("abx"u8, 0, deadline, out _, out _));
        var plan = new Utf8StructuralVerifierPlan(Utf8StructuralVerifierKind.ByteSafeLinearProgram, default,
            new(program, Utf8ByteSafeLinearCompileFailureKind.None), default, null, default);
        var runtime = Utf8StructuralVerifierRuntimeFactory.Create(Utf8VerifierTestPlans.WithStructuralVerifier(plan));
        Assert.Throws<Utf8ExecutionDeadlineExpiredException>(() => runtime.TryMatch("abx"u8, 0, 0, deadline, out _));
        Assert.True(program.TryMatch("abx"u8, 0, out _));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void LongAtomsCheckDeadlineWhileTheyAreExecuting(bool loop)
    {
        using var clock = Utf8DeadlineTestClock.Start();
        var input = Enumerable.Repeat((byte)'a', 256 * 1024).ToArray();
        var step = loop ? Utf8ByteSafeLinearVerifierStep.LoopByte((byte)'a', 1, -1)
            : Utf8ByteSafeLinearVerifierStep.MatchText(input);
        var program = new Utf8ByteSafeLinearVerifierProgram([step, Utf8ByteSafeLinearVerifierStep.Accept()]);
        var invocation = new Utf8SliceInvocation(Utf8ExecutionLimits.Unlimited, TimeSpan.FromMilliseconds(200), CancellationToken.None);
        var entered = false;
        invocation.CheckpointHook = _ => { entered = true; clock.Advance(250); };
        Assert.Throws<Utf8ExecutionDeadlineExpiredException>(() => program.TryMatch(input, 0, ref invocation, out _, out _, out _));
        Assert.True(entered);
        Assert.Equal(0UL, invocation.Ledger.ReservedBytes);
    }

    public static IEnumerable<object[]> PublicCases()
    {
        foreach (var pattern in new[] { @"^ab(?:c[ab]){1,3}x$", @"\w+\s+Holmes" })
        foreach (var compiled in new[] { false, true })
        foreach (var rtl in new[] { false, true }) yield return [pattern, RegexOptions.CultureInvariant |
            (compiled ? RegexOptions.Compiled : RegexOptions.None) | (rtl ? RegexOptions.RightToLeft : RegexOptions.None)];
    }

    [Theory]
    [MemberData(nameof(PublicCases))]
    public void FullPublicOperationsKeepOrdinaryCompiledRtlAndUnicodeFallbackSemantics(string pattern, RegexOptions options)
    {
        Utf8SliceSparseGroupTests.AssertOwnedOperations(pattern, options,
            ["", "abx", "abcacbx", "abcacbcax", "abcacbcacax", "abcacbx\n", "John Holmes", "é Holmes", "中 Holmes",
             "John Holmes é Holmes", "\U0001f600é Holmes\U0001f600"], options);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(16385)]
    public void ProjectedUnicodeFallbackPropagatesAcrossEveryOptionalParent(int depth)
    {
        Utf8ByteSafeLinearVerifierStep[] body = [Utf8ByteSafeLinearVerifierStep.MatchAsciiClass(AsciiCharClass.ForByte((byte)'a'))];
        for (var index = 0; index < depth; index++) body = [Utf8ByteSafeLinearVerifierStep.LoopProgram(body, 0, 1)];
        var program = new Utf8ByteSafeLinearVerifierProgram([.. body, Utf8ByteSafeLinearVerifierStep.Accept()]);
        Assert.False(program.TryMatch("é"u8, 0, out var length, out var fallback));
        Assert.Equal(0, length);
        Assert.True(fallback);
        Assert.False(program.TryMatch("😀"u8, 0, out length, out fallback));
        Assert.Equal(0, length);
        Assert.True(fallback);
        Assert.True(program.TryMatch("a"u8, 0, out length, out fallback));
        Assert.Equal(1, length);
        Assert.False(fallback);
    }

    [Fact]
    public void EmptyAndEmbeddedAcceptBodiesDoNotPublishNestedResults()
    {
        foreach (var body in new Utf8ByteSafeLinearVerifierStep[][] { [], [Utf8ByteSafeLinearVerifierStep.Accept()] })
        {
            var program = new Utf8ByteSafeLinearVerifierProgram([Utf8ByteSafeLinearVerifierStep.LoopProgram(body, 0, -1), Utf8ByteSafeLinearVerifierStep.Accept()]);
            var found = program.TryMatch(""u8, 0, out var length);
            Assert.Equal(body.Length != 0, found); // Empty completion rejects non-progress; embedded Accept fails an optional trial.
            Assert.Equal(0, length);
        }
        Assert.False(default(Utf8ByteSafeLinearVerifierProgram).TryMatch("a"u8, 0, out _));
        Assert.False(new Utf8ByteSafeLinearVerifierProgram([Utf8ByteSafeLinearVerifierStep.MatchByte((byte)'a')]).TryMatch("a"u8, 0, out _));
    }

    private readonly record struct ReferenceState(object? Graph, int Value);

    public static IEnumerable<object[]> NativeEndAnchorCases()
    {
        foreach (var pattern in new[] { @"^ab(?:c[ab]){1,3}x$", @"\Aab(?:c[ab]){1,3}x\Z", @"\Aab(?:c[ab]){1,3}x\z",
            @"(?m)^ab(?:c[ab]){1,3}x$", @"(?m)\Aab(?:c[ab]){1,3}x\Z", @"(?m)\Aab(?:c[ab]){1,3}x\z" })
        foreach (var compiled in new[] { false, true }) yield return [pattern,
            RegexOptions.CultureInvariant | (compiled ? RegexOptions.Compiled : RegexOptions.None)];
    }

    [Theory]
    [MemberData(nameof(NativeEndAnchorCases))]
    public void NativeExpandedBranchesDistinguishAbsoluteFinalNewlineAndLineEnds(string pattern, RegexOptions options)
    {
        var oracle = new Regex(pattern, options);
        var regex = new Utf8Regex(pattern, options);
        foreach (var text in new[] { "abcacbx", "abcacbx\n", "abcacbx\n\n", "abcacbx\nrest", "\nabcacbx\n", "\r\nabcacbx\n" })
        {
            var input = Encoding.ASCII.GetBytes(text);
            Assert.Equal(oracle.Replace(text, "Q"), regex.ReplaceToString(input, "Q"));
            Assert.Equal(Encoding.ASCII.GetBytes(oracle.Replace(text, "Q")), regex.Replace(input, "Q"));
            var expected = oracle.EnumerateMatches(text);
            var actual = regex.EnumerateMatches(input);
            while (expected.MoveNext())
            {
                Assert.True(actual.MoveNext());
                Assert.Equal(expected.Current.Index, actual.Current.IndexInUtf16);
                Assert.Equal(expected.Current.Length, actual.Current.LengthInUtf16);
            }
            Assert.False(actual.MoveNext());
            Assert.True(regex.Inspection.PreparedRegex.ScalarProgram is not null);
        }
    }

    [Fact]
    public void ReferenceLoansHaveExactAdmissionBoundedRetentionAndClearGraphSlots()
    {
        var denied = new Utf8SliceInvocation(new Utf8ExecutionLimits(0, 0, 1), Regex.InfiniteMatchTimeout, CancellationToken.None);
        var buffer = new Utf8InvocationStateBuffer<ReferenceState>();
        var value = new ReferenceState(new object(), 1);
        var before = GC.GetAllocatedBytesForCurrentThread();
        var breach = buffer.TryPush(value, ref denied);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(Utf8ExecutionLimitKind.WorkingBytes, breach);
        Assert.Equal(0, allocated);
        var retained = Utf8ExactStatePool<ReferenceState>.RetainedBytes;
        var finite = new Utf8SliceInvocation(new Utf8ExecutionLimits(0, 0, 4096), Regex.InfiniteMatchTimeout, CancellationToken.None);
        Assert.Equal(Utf8ExecutionLimitKind.None, buffer.TryPush(value, ref finite));
        Assert.Equal(Utf8ConstructionBudget.ArrayBytes<ReferenceState>(16), finite.Ledger.ReservedBytes);
        buffer.Pop();
        buffer.Release(ref finite);
        Assert.Equal(retained, Utf8ExactStatePool<ReferenceState>.RetainedBytes);
        foreach (var capacity in new[] { 16, 32, 64, 128, 256 })
        {
            var items = Utf8ExactStatePool<ReferenceState>.Rent(capacity, true);
            Array.Fill(items, value);
            Utf8ExactStatePool<ReferenceState>.Return(items, true);
            items = Utf8ExactStatePool<ReferenceState>.Rent(capacity, false);
            Assert.All(items, static item => Assert.Equal(default, item));
            Utf8ExactStatePool<ReferenceState>.Return(items, true);
            Assert.InRange(Utf8ExactStatePool<ReferenceState>.RetainedBytes, 0, Utf8ExactStatePool<ReferenceState>.MaximumRetainedBytes);
        }
    }
}
