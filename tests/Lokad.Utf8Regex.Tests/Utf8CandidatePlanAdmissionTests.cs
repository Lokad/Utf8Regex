using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Planning;
using Lokad.Utf8Regex.Internal.Search;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8CandidatePlanAdmissionTests
{
    private sealed class ParentValue(int value) { internal readonly int Value = value; }
    private static PreparedSearcher Searcher(string text) => new(new PreparedSubstringSearch(Encoding.UTF8.GetBytes(text), false), false);
    private static ulong StageBytes(Utf8StructuralSearchPlan plan) => Utf8ConstructionBudget.ArrayBytes<Utf8StructuralSearchStage>(plan.Stages.Length);

    [Fact]
    public void StartAndWindowFactoriesTransferOnlyFinalStageArrays()
    {
        var leading = Searcher("a"); var trailing = Searcher("z");
        foreach (var window in new[] { false, true })
        {
            using var owner = new Utf8ConstructionScope(0, ulong.MaxValue, ulong.MaxValue, ulong.MaxValue);
            var plan = window
                ? Utf8StructuralSearchPlan.CreateTransformedWindowPlanWithLineSpan(new(leading, trailing, 32, true), 5, new(1, Utf8FallbackStartTransformKind.None))
                : Utf8StructuralSearchPlan.CreateStartPlan(leading);
            Assert.True(plan.HasValue); Assert.Equal(StageBytes(plan), owner.Budget.ReservedBytes);
            Assert.Equal(owner.Budget.ReservedBytes, owner.Budget.PeakBytes);
            Assert.Equal(0ul, owner.Budget.Reserved); Assert.Equal(0ul, owner.Budget.Depth);
            Assert.True(owner.Budget.WorkSteps > 0);
            Assert.Equal(window ? 6 : 2, plan.Stages.Length);
        }
    }

    [Fact]
    public void EnrichmentBorrowsTheOriginalAndFundsOneCombinedGuardArray()
    {
        var searcher = Searcher("a");
        Utf8DeterministicByteGuard[] prefix = [new(0, (byte)'a'), new(1, AsciiCharClass.FromBytes("xy"u8))];
        var literal = "!"u8.ToArray();
        Utf8FixedDistanceSet[] sets = [Utf8FixedDistanceSet.FromMask(0, AsciiCharClass.ForByte((byte)'a')),
            Utf8FixedDistanceSet.FromMask(1, AsciiCharClass.FromBytes("xy"u8)),
            Utf8FixedDistanceSet.FromMask(2, AsciiCharClass.Empty),
            Utf8FixedDistanceSet.FromMask(2, new AsciiCharClass(1, 0, true))];
        using var owner = new Utf8ConstructionScope(0, ulong.MaxValue, ulong.MaxValue, ulong.MaxValue);
        var original = Utf8StructuralSearchPlan.CreateStartPlan(searcher);
        var enriched = original.WithVerificationGuards(new(prefix, literal, 2, sets, 3, false));
        Assert.Equal(StageBytes(original) + StageBytes(enriched), owner.Budget.ReservedBytes);
        Assert.Equal(owner.Budget.ReservedBytes, owner.Budget.PeakBytes);
        Assert.Equal(2, original.Stages.Length); Assert.Equal(8, enriched.Stages.Length);
        Assert.Same(literal, enriched.Stages[3].LiteralUtf8);
        Assert.Equal((byte)'a', enriched.Stages[4].LiteralByte); Assert.Equal(sets[1].ByteSet, enriched.Stages[5].Set);
        Assert.Equal(Utf8StructuralSearchStageKind.RequireMinLength, enriched.Stages[6].Kind);
        var state = default(Utf8StructuralSearchState);
        Assert.True(enriched.TryFindNextCandidate("ax!"u8, ref state, out var candidate)); Assert.Equal(0, candidate.StartIndex);
        state = default; Assert.False(enriched.TryFindNextCandidate("az!"u8, ref state, out _));
        Assert.Equal(0ul, owner.Budget.Depth); Assert.Equal(0ul, owner.Budget.Reserved);
    }

    [Fact]
    public void EveryByteAndWorkCutoffFundsTheWholeWindowOrRestoresItsParent()
    {
        var window = new PreparedWindowSearch(Searcher("a"), Searcher("z"), 32, true);
        Utf8StructuralSearchPlan Build() => Utf8StructuralSearchPlan.CreateTransformedWindowPlanWithLineSpan(window, 5, new(1, Utf8FallbackStartTransformKind.None));
        ulong peak, work;
        using (var funded = new Utf8ConstructionScope(0, ulong.MaxValue, ulong.MaxValue, ulong.MaxValue))
        { Assert.True(Build().HasValue); peak = funded.Budget.PeakBytes; work = funded.Budget.WorkSteps; }
        Assert.InRange(peak, 1ul, 20000ul); Assert.InRange(work, 1ul, 1000ul);
        foreach (var bytes in new[] { true, false })
        {
            var limit = bytes ? peak : work; var successes = 0;
            for (var cutoff = 0ul; cutoff <= limit; cutoff++)
            {
                using var owner = new Utf8ConstructionScope(0, bytes ? 112 + cutoff : ulong.MaxValue, bytes ? ulong.MaxValue : 2 + cutoff, ulong.MaxValue);
                var parent = owner.Budget.AllocateObject(17, static value => new ParentValue(value)); var checkpoint = owner.Budget.ObjectCheckpoint;
                var plan = default(Utf8StructuralSearchPlan); var failure = Record.Exception(() => plan = Build());
                if (failure is null) { successes++; Assert.True(plan.HasValue); Assert.Equal(112 + StageBytes(plan), owner.Budget.ReservedBytes); }
                else
                {
                    Assert.Equal(bytes ? Utf8ResourceLimitKind.ConstructionBytes : Utf8ResourceLimitKind.ConstructionWorkSteps,
                        Assert.IsType<Utf8ResourceLimitExceededException>(failure).ResourceKind);
                    Assert.Equal(112ul, owner.Budget.ReservedBytes); Assert.Same(checkpoint, owner.Budget.ObjectCheckpoint);
                }
                Assert.Equal(17, parent.Value); Assert.Equal(0ul, owner.Budget.Depth); Assert.Equal(0ul, owner.Budget.Reserved);
                Assert.True(bytes ? owner.Budget.PeakBytes <= 112 + cutoff : owner.Budget.WorkSteps <= 2 + cutoff);
            }
            Assert.Equal(1, successes);
        }
    }

    [Fact]
    public void SearchPreparationFundsItsCandidateVectorWithoutAcquiringRejectedWindows()
    {
        var set = Utf8FixedDistanceSet.FromMask(0, AsciiCharClass.FromBytes("ab"u8));
        var invalid = Utf8WindowSearchFacts.WithinLines(Utf8PreparedSearcherInfo.LiteralFamily(["a"u8.ToArray()]),
            Utf8PreparedSearcherInfo.QuotedAsciiRun(default, 3), 5);
        var facts = Utf8SearchFacts.Create(Utf8SearchKind.FixedDistanceAsciiSets, new() { FixedDistanceSets = [set], RequiredWindowPrefilters = [invalid] });
        using var owner = new Utf8ConstructionScope(0, ulong.MaxValue, ulong.MaxValue, ulong.MaxValue);
        var plan = Utf8SearchPlan.Prepare(facts);
        Assert.True(plan.StructuralSearchPlan.HasValue);
        Assert.Equal(StageBytes(plan.StructuralSearchPlan) + Utf8ConstructionBudget.ArrayBytes<Utf8StructuralSearchPlan>(1), owner.Budget.ReservedBytes);
        Assert.Null(owner.Budget.ObjectCheckpoint); Assert.Equal(owner.Budget.ReservedBytes, owner.Budget.PeakBytes);
        var state = default(Utf8StructuralSearchState);
        Assert.True(plan.StructuralSearchPlan.TryFindNextCandidate("😀xb"u8, ref state, out var candidate)); Assert.Equal(5, candidate.StartIndex);
        Assert.Equal(0ul, owner.Budget.Depth);

        // Close the measured owner before the next independent finite owners.
        owner.Dispose();
        var literalFacts = Utf8SearchFacts.Create(Utf8SearchKind.ExactAsciiLiteral, new() { LiteralUtf8 = "a"u8.ToArray() });
        ulong peak, work;
        using (var funded = new Utf8ConstructionScope(0, ulong.MaxValue, ulong.MaxValue, ulong.MaxValue))
        { Assert.True(Utf8SearchPlan.Prepare(literalFacts).StructuralSearchPlan.HasValue); peak = funded.Budget.PeakBytes; work = funded.Budget.WorkSteps; }
        Assert.InRange(peak, 1ul, 20000ul); Assert.InRange(work, 1ul, 1000ul);
        foreach (var bytes in new[] { true, false })
        {
            var limit = bytes ? peak : work; var successes = 0;
            for (var cutoff = 0ul; cutoff <= limit; cutoff++)
            {
                using var limited = new Utf8ConstructionScope(0, bytes ? 112 + cutoff : ulong.MaxValue, bytes ? ulong.MaxValue : 2 + cutoff, ulong.MaxValue);
                var parent = limited.Budget.AllocateObject(17, static value => new ParentValue(value)); var checkpoint = limited.Budget.ObjectCheckpoint;
                var result = default(Utf8SearchPlan); var failure = Record.Exception(() => result = Utf8SearchPlan.Prepare(literalFacts));
                if (failure is null) { successes++; Assert.True(result.StructuralSearchPlan.HasValue); Assert.True(limited.Budget.ReservedBytes > 112); }
                else
                {
                    Assert.Equal(bytes ? Utf8ResourceLimitKind.ConstructionBytes : Utf8ResourceLimitKind.ConstructionWorkSteps,
                        Assert.IsType<Utf8ResourceLimitExceededException>(failure).ResourceKind);
                    Assert.Equal(112ul, limited.Budget.ReservedBytes); Assert.Same(checkpoint, limited.Budget.ObjectCheckpoint);
                }
                Assert.Equal(17, parent.Value); Assert.Equal(0ul, limited.Budget.Depth); Assert.Equal(0ul, limited.Budget.Reserved);
                Assert.True(bytes ? limited.Budget.PeakBytes <= 112 + cutoff : limited.Budget.WorkSteps <= 2 + cutoff);
            }
            Assert.Equal(1, successes);
        }
    }

    [Fact]
    public void DerivedUtf16LengthsAreAdmittedBeforeReadingBorrowedScalarLiterals()
    {
        var helper = typeof(Utf8SearchPlan).GetMethod("CreateAlternateLiteralUtf16Lengths", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(helper);
        int[] Invoke(byte[][] literals)
        {
            try { return (int[])helper.Invoke(null, [literals])!; }
            catch (TargetInvocationException error) when (error.InnerException is not null) { ExceptionDispatchInfo.Capture(error.InnerException).Throw(); throw; }
        }
        byte[][] literals = ["é"u8.ToArray(), "😀"u8.ToArray(), "中"u8.ToArray()];
        using (var owner = new Utf8ConstructionScope(0, ulong.MaxValue, ulong.MaxValue, ulong.MaxValue))
        {
            Assert.Equal(new[] { 1, 2, 1 }, Invoke(literals));
            Assert.Equal(Utf8ConstructionBudget.ArrayBytes<int>(3), owner.Budget.ReservedBytes); Assert.Equal(0ul, owner.Budget.Depth);
        }
        using (var denied = new Utf8ConstructionScope(0, 0, ulong.MaxValue, ulong.MaxValue))
        {
            var failure = Assert.Throws<Utf8ResourceLimitExceededException>(() => Invoke([[0xff]]));
            Assert.Equal(Utf8ResourceLimitKind.ConstructionBytes, failure.ResourceKind); Assert.Equal(0ul, denied.Budget.ReservedBytes);
        }
        foreach (var options in new[] { RegexOptions.None, RegexOptions.Compiled, RegexOptions.RightToLeft, RegexOptions.RightToLeft | RegexOptions.Compiled })
        {
            var regex = new Utf8Regex("é|😀|中", options | RegexOptions.CultureInvariant);
            var match = regex.MatchDetailed("x😀y"u8);
            Assert.Equal((1, 4, 1, 2), (match.IndexInBytes, match.LengthInBytes, match.IndexInUtf16, match.LengthInUtf16));
        }
    }

    [Fact]
    public void PlanRootsHonorDepthBeforeAcquisitionAndRecoverInTheSameOwner()
    {
        var searcher = Searcher("a"); var emptyFacts = Utf8SearchFacts.Create(Utf8SearchKind.None);
        using var owner = new Utf8ConstructionScope(0, ulong.MaxValue, ulong.MaxValue, 1);
        var parent = owner.Budget.AllocateObject(17, static value => new ParentValue(value)); var checkpoint = owner.Budget.ObjectCheckpoint;
        owner.Budget.EnterDepth();
        try
        {
            foreach (var root in new Action[] { () => Utf8StructuralSearchPlan.CreateStartPlan(searcher), () => Utf8SearchPlan.Prepare(emptyFacts) })
            {
                var failure = Assert.Throws<Utf8ResourceLimitExceededException>(root);
                Assert.Equal(Utf8ResourceLimitKind.ConstructionDepth, failure.ResourceKind); Assert.Equal(2ul, failure.Requested);
                Assert.Equal(112ul, owner.Budget.ReservedBytes); Assert.Same(checkpoint, owner.Budget.ObjectCheckpoint); Assert.Equal(1ul, owner.Budget.Depth);
            }
        }
        finally { owner.Budget.LeaveDepth(); }
        Assert.True(Utf8StructuralSearchPlan.CreateStartPlan(searcher).HasValue); Assert.Equal(0ul, owner.Budget.Depth); Assert.Equal(17, parent.Value);
    }
}
