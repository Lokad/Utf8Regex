using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Tests;

[Collection(Utf8RuntimeAllocationCollection.Name)]
public sealed class Utf8AsciiStructuralRuntimeTests
{
    private const string ASet = "\0\u0002\0ab";
    private const string AbSet = "\0\u0002\0ac";

    private static (AsciiStructuralLinearVerifierProgram Program, byte[] Input) Case(int family, int length)
    {
        var a = AsciiCharClass.ForByte((byte)'a');
        var ab = new AsciiCharClass(0, (1UL << 33) | (1UL << 34));
        var input = Enumerable.Repeat((byte)'a', length).ToArray();
        AsciiStructuralLinearVerifierInstruction[] instructions;
        switch (family)
        {
            case 0:
                instructions = [AsciiStructuralLinearVerifierInstruction.ConsumeSetLoop(ASet, a, 1)];
                break;
            case 1:
                instructions = [AsciiStructuralLinearVerifierInstruction.RequireSetByte(ASet, a),
                    AsciiStructuralLinearVerifierInstruction.ConsumeSetTail(ASet, a, 0, int.MaxValue)];
                break;
            case 2:
                instructions = [AsciiStructuralLinearVerifierInstruction.MatchSuffixAtCurrent(
                    [AsciiStructuralCompiledSuffixPart.CreateLiteral(input)])];
                break;
            case 3:
                input = [.. input, (byte)'x'];
                instructions = [AsciiStructuralLinearVerifierInstruction.MatchSuffixAtCurrent(
                    [AsciiStructuralCompiledSuffixPart.CreateSeparator(ASet, a, 1),
                     AsciiStructuralCompiledSuffixPart.CreateLiteral("x"u8.ToArray())])];
                break;
            default:
                input = [.. input, (byte)'b', (byte)'y'];
                instructions = [AsciiStructuralLinearVerifierInstruction.RequireSetByte(ASet, a),
                    AsciiStructuralLinearVerifierInstruction.ConsumeSetTail(AbSet, ab, 0, int.MaxValue),
                    AsciiStructuralLinearVerifierInstruction.MatchSuffixAfterTail(
                        [AsciiStructuralCompiledSuffixPart.CreateLiteral("abx"u8.ToArray())], 0)];
                break;
        }
        return (new([.. instructions, AsciiStructuralLinearVerifierInstruction.Accept()]), input);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void EveryWorkCutoffPreservesLengthsLoansAndRecovery(int family)
    {
        var (program, input) = Case(family, 64);
        var baseline = new Utf8SliceInvocation(Utf8ExecutionLimits.Unlimited, Regex.InfiniteMatchTimeout, CancellationToken.None);
        var found = program.TryMatch(input, 0, 0, ref baseline, out var length, out var breach);
        Assert.Equal(family != 4, found);
        Assert.Equal(found ? input.Length : 0, length);
        Assert.Equal(Utf8ExecutionLimitKind.None, breach);
        Assert.Equal(1U, baseline.Budget.PeakDepth);
        Assert.Equal(0UL, baseline.Ledger.ReservedBytes);
        for (ulong step = 1; step < baseline.Budget.Steps; step++)
        {
            var denied = new Utf8SliceInvocation(new Utf8ExecutionLimits(step, 0, 0), Regex.InfiniteMatchTimeout, CancellationToken.None);
            Assert.False(program.TryMatch(input, 0, 0, ref denied, out length, out breach));
            Assert.Equal(0, length);
            Assert.Equal(Utf8ExecutionLimitKind.WorkSteps, breach);
            Assert.Equal(0UL, denied.Ledger.ReservedBytes);
        }
        var recovery = new Utf8SliceInvocation(new Utf8ExecutionLimits(0, 1, 8), Regex.InfiniteMatchTimeout, CancellationToken.None);
        Assert.True(recovery.Ledger.TryReserve(7));
        Assert.Equal(found, program.TryMatch(input, 0, 0, ref recovery, out length, out breach));
        Assert.Equal(7UL, recovery.Ledger.ReservedBytes);
        Assert.Equal(0UL, recovery.Budget.PeakWorkingBytes);
        recovery.Ledger.Release(7);
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, false)]
    [InlineData(2, false)]
    [InlineData(3, false)]
    [InlineData(4, false)]
    [InlineData(0, true)]
    [InlineData(1, true)]
    [InlineData(2, true)]
    [InlineData(3, true)]
    [InlineData(4, true)]
    public void LongAtomsAndSuffixCandidateScanningObserveTokenAndDeadline(int family, bool expire)
    {
        var (program, input) = Case(family, 256 * 1024);
        using var source = new CancellationTokenSource();
        var invocation = new Utf8SliceInvocation(Utf8ExecutionLimits.Unlimited,
            expire ? TimeSpan.FromMilliseconds(200) : Regex.InfiniteMatchTimeout, source.Token);
        var entered = false;
        invocation.CheckpointHook = step =>
        {
            // For the candidate row, wait beyond the tail's bounded block
            // checks so the injected event occurs in suffix verification.
            if (family == 4 && step < (ulong)input.Length / 2048 + 64) return;
            if (!entered)
            {
                entered = true;
                if (expire) Thread.Sleep(250);
                else source.Cancel();
            }
        };
        if (expire)
            Assert.Throws<Utf8ExecutionDeadlineExpiredException>(() =>
                program.TryMatch(input, 0, 0, ref invocation, out _, out _));
        else
        {
            var failure = Assert.Throws<OperationCanceledException>(() =>
                program.TryMatch(input, 0, 0, ref invocation, out _, out _));
            Assert.Equal(source.Token, failure.CancellationToken);
        }
        Assert.True(entered);
        Assert.Equal(0UL, invocation.Ledger.ReservedBytes);
    }

    [Fact]
    public void StructuralWrapperRetainsTheAlreadyExpiredDeadline()
    {
        var program = new AsciiStructuralVerifierProgram(
            [AsciiStructuralVerifierStep.ConsumeSeparator(ASet, 1), AsciiStructuralVerifierStep.Accept()], []);
        var plan = new Utf8StructuralVerifierPlan(Utf8StructuralVerifierKind.AsciiStructuralProgram,
            program, default, default, null, default);
        var runtime = Utf8StructuralVerifierRuntimeFactory.Create(plan);
        var deadline = Utf8ExecutionDeadline.Start(TimeSpan.FromMilliseconds(10));
        Thread.Sleep(50);
        Assert.Throws<Utf8ExecutionDeadlineExpiredException>(() => runtime.TryMatch("aaa"u8, 0, 0, deadline, out _));
        Assert.True(runtime.TryMatch("aaa"u8, 0, 0, Utf8ExecutionDeadline.Infinite, out var length));
        Assert.Equal(3, length);
    }

    [Fact]
    public void WarmAsciiRuntimeBorrowsAllStorageWithoutTemporaryAllocation()
    {
        var (program, input) = Case(3, 64);
        for (var index = 0; index < 100; index++) Assert.True(program.TryMatch(input, 0, 0, out _));
        var before = GC.GetAllocatedBytesForCurrentThread();
        var hits = 0;
        for (var index = 0; index < 1000; index++) if (program.TryMatch(input, 0, 0, out _)) hits++;
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(1000, hits);
        Assert.Equal(0, allocated);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void PrefixOffsetsBoundariesAndGreedySuffixChoicesKeepOracleRanges(int prefixLength)
    {
        var ab = new AsciiCharClass(0, (1UL << 33) | (1UL << 34));
        var program = new AsciiStructuralLinearVerifierProgram([
            AsciiStructuralLinearVerifierInstruction.RequireSetByte(ASet, AsciiCharClass.ForByte((byte)'a')),
            AsciiStructuralLinearVerifierInstruction.ConsumeSetTail(AbSet, ab, 0, int.MaxValue),
            AsciiStructuralLinearVerifierInstruction.MatchSuffixAfterTail(
                [AsciiStructuralCompiledSuffixPart.CreateLiteral("ab"u8.ToArray()),
                 AsciiStructuralCompiledSuffixPart.CreateLiteral("x"u8.ToArray())], 0),
            AsciiStructuralLinearVerifierInstruction.Accept()]);
        var oracle = new Regex((prefixLength == 1 ? "p" : "") + "a[ab]*abx", RegexOptions.CultureInvariant);
        foreach (var body in new[] { "", "a", "aabx", "aaabx", "ababx", "aaaby", "aaabxx", "aaabx中" })
        {
            var text = "zz" + (prefixLength == 1 ? "p" : "") + body;
            var expected = oracle.Match(text, 2);
            var found = program.TryMatch(Encoding.UTF8.GetBytes(text), 2, prefixLength, out var length);
            Assert.Equal(expected.Success && expected.Index == 2, found);
            Assert.Equal(found ? expected.Length : 0, length);
        }
    }

    public static IEnumerable<object[]> PublicCases()
    {
        foreach (var pattern in new[] { @"call[ \t]+[a-z]+\(", @"abc[0-9]{2}end", @"a[ab]*abx", @"a+suffix" })
        foreach (var compiled in new[] { false, true })
        foreach (var rtl in new[] { false, true }) yield return [pattern, RegexOptions.CultureInvariant |
            (compiled ? RegexOptions.Compiled : RegexOptions.None) | (rtl ? RegexOptions.RightToLeft : RegexOptions.None)];
    }

    [Theory]
    [MemberData(nameof(PublicCases))]
    public void PublicOperationsPreserveOrdinaryCompiledRtlAndUnicode(string pattern, RegexOptions options)
        => Utf8SliceSparseGroupTests.AssertOwnedOperations(pattern, options,
            ["", "call name(", "call\tname(", "xcall name(中", "abc12end", "abc1end", "aaabx", "aaaby",
             "aasuffix", "aaabx\ncall name(", "\U0001f600abc12end\U0001f600"], options);
}
