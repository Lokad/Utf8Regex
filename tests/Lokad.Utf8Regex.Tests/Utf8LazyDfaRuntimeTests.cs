using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.FrontEnd;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8LazyDfaRuntimeTests
{
    public static IEnumerable<object[]> AnchorCases()
    {
        foreach (var pattern in new[] { @"^ab[0-9]{2}$", @"\Aab[0-9]{2}\Z", @"\Aab[0-9]{2}\z",
            @"(?m)^ab[0-9]{2}$", @"(?m)\Aab[0-9]{2}\Z", @"(?m)\Aab[0-9]{2}\z", @"\Gab[0-9]{2}",
            @"ab(?:cd|ef){1,3}", @"^ab(?:cd|ef){1,3}$", @"(?m)^ab(?:cd|ef){1,3}$", @"a(?:\nb)?(?m:$)" })
        foreach (var compiled in new[] { false, true }) yield return [pattern,
            RegexOptions.CultureInvariant | (compiled ? RegexOptions.Compiled : RegexOptions.None)];
    }

    [Theory]
    [MemberData(nameof(AnchorCases))]
    public void BothByteSafeProgramsRetainActualAnchorKindsAndOracleRanges(string pattern, RegexOptions options)
    {
        var prepared = Utf8FrontEnd.Compile(pattern, options);
        var linear = Utf8ByteSafeLinearVerifierProgram.Compile(prepared.ExecutionTree);
        // Starting the optional arm with LF can cross a multiline end position.
        // The first-arm-only kernel cannot skip that arm after the end assertion
        // fails. Keep this original grammar row and qualify its scalar route.
        var needsAssertionRewind = pattern == @"a(?:\nb)?(?m:$)";
        Assert.Equal(!needsAssertionRewind, linear.Succeeded);
        var dfa = Utf8ByteSafeLazyDfaVerifierProgram.Compile(linear.Program);
        Assert.Equal(!needsAssertionRewind, dfa.Succeeded);
        var tree = Utf8OwnedTreeParser.TryParse(pattern, options); Assert.NotNull(tree);
        var owned = Utf8OwnedTreeSliceLowerer.TryLower(tree, options); Assert.NotNull(owned);
        var starts = new int[owned.GroupCount]; var ends = new int[owned.GroupCount];
        var oracle = new Regex(pattern, options);
        foreach (var text in new[] { "", "ab12", "ab12\n", "ab12\n\n", "ab12\nrest", "\nab12\n", "zab12", "ab1x",
            "abcd", "abcdefef", "abcdefef\n", "\nabef\nrest", "abcdcdefcd", "abcxef", "ab", "a\nx", "a\nb\nrest", "a\nb", "\na\nx", "a\nbx" })
        foreach (var start in Enumerable.Range(0, text.Length + 1))
        {
            var expected = oracle.Match(text, start);
            var input = Encoding.ASCII.GetBytes(text);
            var expectedHit = expected.Success && expected.Index == start;
            var invocation = new Utf8SliceInvocation(Utf8ExecutionLimits.Unlimited, Regex.InfiniteMatchTimeout, CancellationToken.None) { AnchorOriginByte = start };
            var scalar = Utf8SliceRunner.TryMatchAt(owned, input, start, starts, ends, true, ref invocation);
            Assert.Equal(Utf8ExecutionLimitKind.None, scalar.Breach);
            Assert.Equal(expectedHit, scalar.IsMatch);
            if (expectedHit) Assert.Equal(expected.Length, scalar.EndIndex - start);
            if (needsAssertionRewind) continue;
            Assert.Equal(expectedHit, linear.Program.TryMatch(input, start, out var linearLength, out var fallback));
            Assert.False(fallback);
            Assert.Equal(expectedHit, dfa.Program.TryMatch(input, start, out var dfaLength));
            Assert.Equal(expectedHit ? expected.Length : 0, linearLength);
            Assert.Equal(expectedHit ? expected.Length : 0, dfaLength);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ContiguousAnchorReadsTheExplicitOuterInvocationOrigin(bool compiled)
    {
        var prepared = Utf8FrontEnd.Compile(@"\Gab[0-9]{2}", RegexOptions.CultureInvariant | (compiled ? RegexOptions.Compiled : RegexOptions.None));
        var linear = Utf8ByteSafeLinearVerifierProgram.Compile(prepared.ExecutionTree).Program;
        var dfa = Utf8ByteSafeLazyDfaVerifierProgram.Compile(linear).Program;
        foreach (var origin in new[] { 0, 1 })
        {
            var invocation = new Utf8SliceInvocation(Utf8ExecutionLimits.Unlimited, Regex.InfiniteMatchTimeout, CancellationToken.None) { AnchorOriginByte = origin };
            Assert.Equal(origin == 1, linear.TryMatch("xab12"u8, 1, ref invocation, out var length, out _, out _));
            Assert.Equal(origin == 1 ? 4 : 0, length);
            Assert.Equal(origin == 1, dfa.TryMatch("xab12"u8, 1, ref invocation, out length, out _));
            Assert.Equal(origin == 1 ? 4 : 0, length);
        }
    }

    [Fact]
    public void AnchorsInsideTheBodyStayOnTheOwnedLinearProgram()
    {
        var prepared = Utf8FrontEnd.Compile(@"(?m)^a$b", RegexOptions.CultureInvariant);
        var linear = Utf8ByteSafeLinearVerifierProgram.Compile(prepared.ExecutionTree);
        Assert.True(linear.Succeeded);
        var dfa = Utf8ByteSafeLazyDfaVerifierProgram.Compile(linear.Program);
        Assert.False(dfa.Succeeded);
        Assert.Equal(Utf8ByteSafeLazyDfaCompileFailureKind.UnsupportedAnchoring, dfa.FailureKind);
        Assert.False(linear.Program.TryMatch("ab"u8, 0, out _));
        Assert.False(linear.Program.TryMatch("a\nb"u8, 0, out _));
    }

    private static Utf8ByteSafeLazyDfaVerifierProgram RepeatedByte()
    {
        var outcome = Utf8ByteSafeLazyDfaVerifierProgram.Compile(new([
            Utf8ByteSafeLinearVerifierStep.LoopByte((byte)'a', 1, -1), Utf8ByteSafeLinearVerifierStep.Accept()]));
        Assert.True(outcome.Succeeded);
        return outcome.Program;
    }

    [Fact]
    public void EveryRuntimeWorkCutoffWithholdsTheResultAndKeepsCallerLeases()
    {
        var program = RepeatedByte();
        var input = Enumerable.Repeat((byte)'a', 8192).ToArray();
        var measured = new Utf8SliceInvocation(Utf8ExecutionLimits.Unlimited, Regex.InfiniteMatchTimeout, CancellationToken.None);
        Assert.True(program.TryMatch(input, 0, ref measured, out var length, out var breach));
        Assert.Equal(input.Length, length);
        Assert.Equal(1U, measured.Budget.PeakDepth);
        Assert.Equal(Utf8ExecutionLimitKind.None, breach);
        for (var limit = 1UL; limit < measured.Budget.Steps; limit++)
        {
            var denied = new Utf8SliceInvocation(new Utf8ExecutionLimits(limit, 0, 0), Regex.InfiniteMatchTimeout, CancellationToken.None);
            Assert.False(program.TryMatch(input, 0, ref denied, out length, out breach));
            Assert.Equal(0, length);
            Assert.Equal(Utf8ExecutionLimitKind.WorkSteps, breach);
            Assert.Equal(0UL, denied.Ledger.ReservedBytes);
        }
        var recovered = new Utf8SliceInvocation(new Utf8ExecutionLimits(0, 1, 8), Regex.InfiniteMatchTimeout, CancellationToken.None);
        Assert.True(recovered.Ledger.TryReserve(7));
        Assert.True(program.TryMatch(input, 0, ref recovered, out length, out breach));
        Assert.Equal(input.Length, length);
        Assert.Equal(7UL, recovered.Ledger.ReservedBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void LongRuntimeChecksTokenAndDeadlineBeforePublishing(bool expire)
    {
        var program = RepeatedByte();
        var input = Enumerable.Repeat((byte)'a', 256 * 1024).ToArray();
        using var source = new CancellationTokenSource();
        var invocation = new Utf8SliceInvocation(Utf8ExecutionLimits.Unlimited,
            expire ? TimeSpan.FromMilliseconds(200) : Regex.InfiniteMatchTimeout, source.Token);
        var entered = false;
        invocation.CheckpointHook = _ => { entered = true; if (expire) Thread.Sleep(250); else source.Cancel(); };
        if (expire) Assert.Throws<Utf8ExecutionDeadlineExpiredException>(() => program.TryMatch(input, 0, ref invocation, out _, out _));
        else Assert.Equal(source.Token, Assert.Throws<OperationCanceledException>(() => program.TryMatch(input, 0, ref invocation, out _, out _)).CancellationToken);
        Assert.True(entered);
        Assert.Equal(0UL, invocation.Ledger.ReservedBytes);
        Assert.True(program.TryMatch(input, 0, out _));
    }

    [Fact]
    public void StructuralRuntimeRetainsTheExistingDeadline()
    {
        var program = RepeatedByte();
        var plan = new Utf8StructuralVerifierPlan(Utf8StructuralVerifierKind.ByteSafeLazyDfaProgram, default, default,
            new(program, Utf8ByteSafeLazyDfaCompileFailureKind.None), null, default);
        var runtime = Utf8StructuralVerifierRuntimeFactory.Create(Utf8VerifierTestPlans.WithStructuralVerifier(plan));
        var deadline = Utf8ExecutionDeadline.Start(TimeSpan.FromMilliseconds(10));
        Thread.Sleep(30);
        Assert.Throws<Utf8ExecutionDeadlineExpiredException>(() => runtime.TryMatch("a"u8, 0, 0, deadline, out _));
    }

    [Fact]
    public void WarmRuntimeHasNoTemporaryAllocation()
    {
        var program = RepeatedByte();
        var input = "aaaa"u8.ToArray();
        for (var index = 0; index < 100; index++) Assert.True(program.TryMatch(input, 0, out _));
        var before = GC.GetAllocatedBytesForCurrentThread();
        var hits = 0;
        for (var index = 0; index < 1000; index++) if (program.TryMatch(input, 0, out _)) hits++;
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(1000, hits);
        Assert.Equal(0, allocated);
    }

    public static IEnumerable<object[]> PublicCases()
    {
        foreach (var pattern in new[] { @"(?m)\Aab[0-9]{2}\z", @"(?m)\Aab[0-9]{2}\Z", @"\Aab[0-9]{260}x\z", @"^ab(?:cd|ef){1,3}$", @"\Gab[0-9]{2}" })
        foreach (var compiled in new[] { false, true })
        foreach (var rtl in new[] { false, true }) yield return [pattern, RegexOptions.CultureInvariant |
            (compiled ? RegexOptions.Compiled : RegexOptions.None) | (rtl ? RegexOptions.RightToLeft : RegexOptions.None)];
    }

    [Theory]
    [MemberData(nameof(PublicCases))]
    public void PublicConformanceKeepsCompiledRtlOversizedAndFinalNewlineInputs(string pattern, RegexOptions options)
        => Utf8SliceSparseGroupTests.AssertOwnedOperations(pattern, options,
            ["", "ab12", "ab12\n", "ab12\nrest", "\nab12", "xab12", "abcd", "abcdefef\n", "\U0001f600ab12",
             "ab" + new string('1', 260) + "x", "ab" + new string('1', 260) + "x\n"], options);
}
