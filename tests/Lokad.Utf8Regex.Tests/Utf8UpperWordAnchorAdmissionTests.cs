using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.FrontEnd;
using Lokad.Utf8Regex.Internal.Planning;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8UpperWordAnchorAdmissionTests
{
    private static AsciiStructuralIdentifierFamilyPlan Family(byte[][] prefixes)
        => new(prefixes, AsciiCharClass.FromBytes(" \t\r\n\f\v"u8), 1,
            AsciiCharClass.FromBytes("ABCDEFGHIJKLMNOPQRSTUVWXYZ"u8),
            AsciiCharClass.FromBytes("ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789_"u8),
            1, int.MaxValue, null, [], Utf8BoundaryRequirement.Boundary, Utf8BoundaryRequirement.None);

    private static ulong Bytes<T>(int count)
        => count == 0 ? 0 : 32UL + (((ulong)Unsafe.SizeOf<T>() * (ulong)count + 15) & ~15UL);

    private static (int Offset, byte[] Bytes)? Oracle(byte[][] prefixes)
    {
        if (prefixes.Length == 0 || prefixes.Any(p => p.Length == 0)) return null;
        return Enumerable.Range(0, prefixes.Min(p => p.Length))
            .Select(offset => (Offset: offset, Column: prefixes.Select(p => p[offset]).ToArray()))
            .Where(c => c.Column.All(b => char.IsAsciiLetterOrDigit((char)b) || b == (byte)'_'))
            .Select(c => (c.Offset, Bytes: c.Column.Distinct().Order().ToArray(), Score: c.Column.Sum(b => Utf8AsciiSearchHeuristics.GetAnchorRarityScore(b))))
            .Where(c => c.Bytes.Length >= 2)
            .OrderByDescending(c => c.Bytes.Length).ThenByDescending(c => c.Score).ThenByDescending(c => c.Offset)
            .Select(c => ((int Offset, byte[] Bytes)?)(c.Offset, c.Bytes)).FirstOrDefault();
    }

    [Fact]
    public void SelectorMatchesIndependentColumnRankingAcrossByteDomainAndRandomFamilies()
    {
        List<byte[][]> rows =
        [
            [], [[]], ["aa"u8.ToArray(), "bb"u8.ToArray()],
            ["a!"u8.ToArray(), "b?"u8.ToArray()], ["xa"u8.ToArray(), "ybzz"u8.ToArray(), "ybzz"u8.ToArray()],
            ["aaa"u8.ToArray(), "aaa"u8.ToArray()], ["x"u8.ToArray(), "y"u8.ToArray(), []]
        ];
        for (var b = 0; b <= byte.MaxValue; b++) rows.Add([[(byte)b, (byte)'b'], [(byte)'Z', (byte)'c']]);
        var random = new Random(841903);
        for (var trial = 0; trial < 100; trial++)
            rows.Add(Enumerable.Range(0, random.Next(1, 9))
                .Select(_ => Enumerable.Range(0, random.Next(1, 9)).Select(_ => (byte)random.Next(32, 140)).ToArray()).ToArray());
        Span<byte> scratch = stackalloc byte[128];
        foreach (var prefixes in rows)
        {
            var family = Family(prefixes); var expected = Oracle(prefixes);
            var found = Utf8AsciiStructuralIdentifierFamilyExecutor.TryGetUpperWordIdentifierKernelSpec(family, Utf8ExecutionDeadline.Infinite, out var offset, out var anchors);
            Assert.Equal(expected.HasValue, found);
            if (!found) { Assert.True(anchors.IsEmpty); continue; }
            Assert.Equal(expected!.Value.Offset, offset);
            Assert.Equal(expected.Value.Bytes, scratch[..anchors.CopyMatchBytes(scratch)].ToArray());
            for (var b = 0; b <= byte.MaxValue; b++) Assert.Equal(expected.Value.Bytes.Contains((byte)b), anchors.Contains((byte)b));
        }
        Assert.Equal(1, Oracle(rows[2])!.Value.Offset); // Equal column ranks keep the later offset.
    }

    [Fact]
    public void WarmPredicateSelectionAndDirectMatchingAcquireNoHeapOrConstructionStorage()
    {
        var family = Family(["record"u8.ToArray(), "struct"u8.ToArray(), "class"u8.ToArray()]);
        byte[] input = "record Foo struct Bar class Worker_State"u8.ToArray();
        bool Run()
        {
            if (!Utf8AsciiStructuralIdentifierFamilyExecutor.CanUseUpperWordIdentifierKernel(family, Utf8ExecutionDeadline.Infinite) ||
                !Utf8AsciiStructuralIdentifierFamilyExecutor.TryGetUpperWordIdentifierKernelSpec(family, Utf8ExecutionDeadline.Infinite, out _, out _)) return false;
            var count = Utf8AsciiStructuralIdentifierFamilyExecutor.CountUpperWordIdentifier(input, family, Utf8ExecutionDeadline.Infinite);
            var index = Utf8AsciiStructuralIdentifierFamilyExecutor.FindNextUpperWordIdentifierDirect(input, family, 0, Utf8ExecutionDeadline.Infinite, out var length);
            return count == 3 && index == 0 && length == 10;
        }
        for (var call = 0; call < 1000; call++) Assert.True(Run());
        // Keep xUnit's comparison allocations outside the observed production calls.
        var correct = true;
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var call = 0; call < 100; call++) correct &= Run();
        var acquired = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.True(correct); Assert.Equal(0, acquired); Assert.False(Utf8ConstructionScope.IsActive);
        using var owner = new Utf8ConstructionScope(0, 7, ulong.MaxValue, 0);
        owner.Budget.ReserveBytes(7); Assert.True(Run());
        Assert.Equal(7UL, owner.Budget.PeakBytes); Assert.Equal(7UL, owner.Budget.ReservedBytes);
        Assert.Equal(0UL, owner.Budget.Reserved); Assert.Equal(0UL, owner.Budget.Depth);
        Assert.True(owner.Budget.WorkSteps > 0);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void EmittedPlanAdmitsOnlyItsFinalOwnedArraysAndRollsBackEveryWorkCutoff(bool bytes)
    {
        var family = Family(["aa"u8.ToArray(), "bb"u8.ToArray()]);
        var extent = Bytes<byte>(2) + Bytes<byte[]>(256) + Bytes<Utf8EmittedKernelBlock>(6);
        ulong work;
        using (var probe = Utf8ConstructionScope.Enter())
        {
            Assert.True(Utf8EmittedKernelLowerer.TryLower(family, default, out var lowered));
            Assert.Equal(extent, probe.Budget.ReservedBytes); work = probe.Budget.WorkSteps;
            Assert.Equal(new byte[] { (byte)'a', (byte)'b' }, lowered.FindOptimization.AnchorBytes);
            Assert.Equal(1, lowered.FindOptimization.AnchorOffset); Assert.Same(family.Prefixes, lowered.Prefixes);
            Assert.Same(family.Prefixes[1], lowered.FindOptimization.AnchorPrefixesByByte![(byte)'b']);
        }
        var cutoffs = bytes ? new[] { extent - 1, extent } : Enumerable.Range(0, checked((int)work + 1)).Select(i => (ulong)i);
        foreach (var cutoff in cutoffs)
        {
            using var owner = new Utf8ConstructionScope(0, bytes ? 7 + cutoff : ulong.MaxValue,
                bytes ? ulong.MaxValue : cutoff, ulong.MaxValue);
            owner.Budget.ReserveBytes(7);
            Utf8EmittedKernelPlan result = default;
            var failure = Record.Exception(() => Utf8EmittedKernelLowerer.TryLower(family, default, out result));
            if (cutoff == (bytes ? extent : work))
            {
                Assert.Null(failure); Assert.True(result.HasValue); Assert.Equal(7 + extent, owner.Budget.ReservedBytes);
                Assert.Equal(Utf8EmittedKernelKind.UpperWordIdentifierFamily, result.Kind);
            }
            else
            {
                Assert.Equal(bytes ? Utf8ResourceLimitKind.ConstructionBytes : Utf8ResourceLimitKind.ConstructionWorkSteps,
                    Assert.IsType<Utf8ResourceLimitExceededException>(failure).ResourceKind);
                Assert.False(result.HasValue); Assert.Equal(7UL, owner.Budget.ReservedBytes);
            }
            Assert.Equal(0UL, owner.Budget.Reserved); Assert.Equal(0UL, owner.Budget.Depth);
        }
    }

    [Fact]
    public void LongBorrowedPrefixesDenySelectionBeforeAnyLengthSizedAllocation()
    {
        var family = Family([Enumerable.Repeat((byte)'a', 100000).ToArray(), Enumerable.Repeat((byte)'b', 100000).ToArray()]);
        using var owner = new Utf8ConstructionScope(0, 7, 100, 0);
        owner.Budget.ReserveBytes(7);
        var before = GC.GetAllocatedBytesForCurrentThread();
        var failure = Record.Exception(() => Utf8AsciiStructuralIdentifierFamilyExecutor.TryGetUpperWordIdentifierKernelSpec(family, Utf8ExecutionDeadline.Infinite, out _, out _));
        var acquired = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(Utf8ResourceLimitKind.ConstructionWorkSteps, Assert.IsType<Utf8ResourceLimitExceededException>(failure).ResourceKind);
        Assert.True(acquired < 32768, $"Selector acquired a length-sized array: {acquired} bytes.");
        Assert.Equal(7UL, owner.Budget.ReservedBytes); Assert.Equal(7UL, owner.Budget.PeakBytes);
        Assert.Equal(100UL, owner.Budget.WorkSteps);
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)]
    public void OriginalRuntimeDeadlineInterruptsLongSelection(int operation)
    {
        var family = Family([Enumerable.Repeat((byte)'a', 100000).ToArray(), Enumerable.Repeat((byte)'b', 100000).ToArray()]);
        var reads = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ++reads < 3 ? 0 : 50);
        var deadline = Utf8ExecutionDeadline.Start(TimeSpan.FromMilliseconds(50));
        Assert.Throws<Utf8ExecutionDeadlineExpiredException>(() =>
        {
            switch (operation)
            {
                case 0: Utf8AsciiStructuralIdentifierFamilyExecutor.TryGetUpperWordIdentifierKernelSpec(family, deadline, out _, out _); break;
                case 1: Utf8AsciiStructuralIdentifierFamilyExecutor.CanUseUpperWordIdentifierKernel(family, deadline); break;
                case 2: Utf8AsciiStructuralIdentifierFamilyExecutor.CountUpperWordIdentifier([], family, deadline); break;
                default: Utf8AsciiStructuralIdentifierFamilyExecutor.FindNextUpperWordIdentifierDirect([], family, 0, deadline, out _); break;
            }
        });
        Assert.Equal(3, reads); Assert.False(Utf8ConstructionScope.IsActive);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void EveryEmissionCheckpointInheritsTokenAndDeadlineAndRestoresParent(bool timeout)
    {
        var family = Family(["aa"u8.ToArray(), "bb"u8.ToArray()]);
        var count = 0;
        using (var probe = Utf8ConstructionScope.Enter())
        {
            probe.Budget.CheckpointHook = _ => count++;
            Assert.True(Utf8EmittedKernelLowerer.TryLower(family, default, out _));
        }
        for (var target = 1; target <= count; target++)
        {
            using var owner = Utf8ConstructionScope.Enter(); owner.Budget.ReserveBytes(7);
            using var token = new CancellationTokenSource(); long ticks = 0;
            using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
            var deadline = Utf8ExecutionDeadline.Start(TimeSpan.FromMilliseconds(50));
            owner.Budget.CancellationToken = token.Token; owner.Budget.Deadline = deadline;
            var checkpoints = 0;
            owner.Budget.CheckpointHook = _ => { if (++checkpoints == target) { if (timeout) ticks = 50; else token.Cancel(); } };
            var failure = Record.Exception(() => Utf8EmittedKernelLowerer.TryLower(family, default, out _));
            if (timeout) Assert.IsType<Utf8ExecutionDeadlineExpiredException>(failure);
            else Assert.Equal(token.Token, Assert.IsType<OperationCanceledException>(failure).CancellationToken);
            Assert.Same(deadline, owner.Budget.Deadline); Assert.Equal(7UL, owner.Budget.ReservedBytes);
            Assert.Equal(0UL, owner.Budget.Depth); Assert.Equal(0UL, owner.Budget.Reserved);
            owner.Budget.CheckpointHook = null; owner.Budget.CancellationToken = default; owner.Budget.Deadline = Utf8ExecutionDeadline.Infinite;
            using (var recovered = Utf8ConstructionScope.Enter()) Assert.True(Utf8EmittedKernelLowerer.TryLower(family, default, out _));
            Assert.Equal(7UL, owner.Budget.ReservedBytes);
        }
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void ActualOrdinaryAndCompiledConsumersKeepUnicodeBoundaryRanges(bool compiled)
    {
        const string pattern = @"\b(?:record|struct|class)\s+[A-Z][A-Za-z0-9_]+";
        byte[] input = Encoding.UTF8.GetBytes("😀 class Worker_State; érecord Bogus; record CustomerExportJob; struct worker_state");
        var regex = new Utf8Regex(pattern, RegexOptions.CultureInvariant | (compiled ? RegexOptions.Compiled : RegexOptions.None));
        Assert.Equal(2, regex.Count(input));
        var first = regex.Match(input); Assert.Equal(5, first.IndexInBytes);
        Assert.Equal("class Worker_State"u8.ToArray(), input.AsSpan(first.IndexInBytes, first.LengthInBytes).ToArray());
        var prepared = Utf8FrontEnd.Compile(pattern, RegexOptions.CultureInvariant);
        Assert.True(Utf8EmittedKernelLowerer.TryLower(prepared, out var kernel));
        Assert.Equal(Utf8EmittedKernelKind.UpperWordIdentifierFamily, kernel.Kind);
        var expected = Oracle(prepared.StructuralIdentifierFamilyPlan.Prefixes)!.Value;
        Assert.Equal(expected.Offset, kernel.FindOptimization.AnchorOffset); Assert.Equal(expected.Bytes, kernel.FindOptimization.AnchorBytes);
    }
}
