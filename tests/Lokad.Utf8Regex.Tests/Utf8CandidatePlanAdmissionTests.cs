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
    private static ulong PlanBytes => Utf8ConstructionBudget.ObjectBytes<Utf8StructuralSearchPlan.Data>();

    private static Utf8StructuralSearchPlan CreatePlan(PreparedSearcher searcher) =>
        Utf8StructuralSearchPlan.Create(Utf8SearchKind.ExactAsciiLiteral, 0, false, searcher, null, default);

    [Fact]
    public void StartFactoryTransfersOnlyItsPlanPayload()
    {
        var searcher = Searcher("a");
        using var owner = new Utf8ConstructionScope(0, ulong.MaxValue, ulong.MaxValue, ulong.MaxValue);
        var plan = CreatePlan(searcher);
        Assert.True(plan.HasValue);
        Assert.Equal(PlanBytes, owner.Budget.ReservedBytes);
        Assert.Equal(owner.Budget.ReservedBytes, owner.Budget.PeakBytes);
        Assert.Equal(0ul, owner.Budget.Reserved);
        Assert.Equal(0ul, owner.Budget.Depth);
        Assert.True(owner.Budget.WorkSteps > 0);
    }

    [Fact]
    public void SearchPreparationFundsItsStructuralPlan()
    {
        var set = new Utf8FixedDistanceSet(0, AsciiCharClass.FromBytes("ab"u8)).PrepareAnchor();
        var facts = Utf8SearchFacts.Create(Utf8SearchKind.FixedDistanceAsciiSets, new() { FixedDistanceSets = [set] });
        using var owner = new Utf8ConstructionScope(0, ulong.MaxValue, ulong.MaxValue, ulong.MaxValue);
        var plan = Utf8SearchPlan.Prepare(facts);
        Assert.True(plan.StructuralSearchPlan.HasValue);
        Assert.Equal(PlanBytes, owner.Budget.ReservedBytes);
        Assert.Equal(owner.Budget.ReservedBytes, owner.Budget.PeakBytes);
        var state = default(PreparedSearchScanState);
        Assert.True(plan.StructuralSearchPlan.TryFindNextCandidate("😀xb"u8, ref state, out var candidate)); Assert.Equal(5, candidate.Index);
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
                using var limited = new Utf8ConstructionScope(0, bytes ? 128 + cutoff : ulong.MaxValue, bytes ? ulong.MaxValue : 2 + cutoff, ulong.MaxValue);
                var parent = limited.Budget.AllocateObject(17, static value => new ParentValue(value)); var checkpoint = limited.Budget.ReservedBytes;
                var result = default(Utf8SearchPlan); var failure = Record.Exception(() => result = Utf8SearchPlan.Prepare(literalFacts));
                if (failure is null) { successes++; Assert.True(result.StructuralSearchPlan.HasValue); Assert.True(limited.Budget.ReservedBytes > 128); }
                else
                {
                    Assert.Equal(bytes ? Utf8ResourceLimitKind.ConstructionBytes : Utf8ResourceLimitKind.ConstructionWorkSteps,
                        Assert.IsType<Utf8ResourceLimitExceededException>(failure).ResourceKind);
                    Assert.Equal(128ul, limited.Budget.ReservedBytes); Assert.Equal(checkpoint, limited.Budget.ReservedBytes);
                }
                Assert.Equal(17, parent.Value); Assert.Equal(0ul, limited.Budget.Depth); Assert.Equal(0ul, limited.Budget.Reserved);
                Assert.True(bytes ? limited.Budget.PeakBytes <= 128 + cutoff : limited.Budget.WorkSteps <= 2 + cutoff);
            }
            Assert.Equal(1, successes);
        }
    }

    [Fact]
    public void PlanRootsHonorDepthBeforeAcquisitionAndRecoverInTheSameOwner()
    {
        var searcher = Searcher("a"); var emptyFacts = Utf8SearchFacts.Create(Utf8SearchKind.None);
        using var owner = new Utf8ConstructionScope(0, ulong.MaxValue, ulong.MaxValue, 1);
        var parent = owner.Budget.AllocateObject(17, static value => new ParentValue(value)); var checkpoint = owner.Budget.ReservedBytes;
        owner.Budget.EnterDepth();
        try
        {
            foreach (var root in new Action[] { () => CreatePlan(searcher), () => Utf8SearchPlan.Prepare(emptyFacts) })
            {
                var failure = Assert.Throws<Utf8ResourceLimitExceededException>(root);
                Assert.Equal(Utf8ResourceLimitKind.ConstructionDepth, failure.ResourceKind); Assert.Equal(2ul, failure.Requested);
                Assert.Equal(128ul, owner.Budget.ReservedBytes); Assert.Equal(checkpoint, owner.Budget.ReservedBytes); Assert.Equal(1ul, owner.Budget.Depth);
            }
        }
        finally { owner.Budget.LeaveDepth(); }
        Assert.True(CreatePlan(searcher).HasValue); Assert.Equal(0ul, owner.Budget.Depth); Assert.Equal(17, parent.Value);
    }
}
