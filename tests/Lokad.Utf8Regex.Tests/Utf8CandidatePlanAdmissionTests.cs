using Lokad.Utf8Regex.Internal;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Planning;
using Lokad.Utf8Regex.Internal.Search;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8CandidatePlanAdmissionTests
{
    private sealed class ParentValue(int value) { internal readonly int Value = value; }

    [Fact]
    public void SearchPreparationBorrowsFixedDistanceSetsWithoutAllocatingAnotherPlan()
    {
        var set = new Utf8FixedDistanceSet(0, AsciiCharClass.FromBytes("ab"u8)).PrepareAnchor();
        var facts = Utf8SearchFacts.Create(Utf8SearchKind.FixedDistanceAsciiSets, new() { FixedDistanceSets = [set] });
        using var owner = new Utf8ConstructionScope(0, 0, ulong.MaxValue, ulong.MaxValue);
        var plan = Utf8SearchPlan.Prepare(facts);
        Assert.True(plan.HasStructuralCandidates);
        Assert.Same(facts.FixedDistanceSets, plan.FixedDistanceSets);
        Assert.Equal(0ul, owner.Budget.PeakBytes);
        var state = default(PreparedSearchScanState);
        Assert.True(plan.TryFindNextCandidate("😀xb"u8, ref state, out var candidate));
        Assert.Equal(5, candidate.Index);
        Assert.Equal(0ul, owner.Budget.Depth);
    }

    [Fact]
    public void SearchPreparationHonorsDepthAndRecoversInTheSameOwner()
    {
        var facts = Utf8SearchFacts.Create(Utf8SearchKind.ExactAsciiLiteral, new() { LiteralUtf8 = "a"u8.ToArray() });
        using var owner = new Utf8ConstructionScope(0, ulong.MaxValue, ulong.MaxValue, 1);
        var parent = owner.Budget.AllocateObject(17, static value => new ParentValue(value));
        var checkpoint = owner.Budget.ReservedBytes;
        owner.Budget.EnterDepth();
        try
        {
            var failure = Assert.Throws<Utf8ResourceLimitExceededException>(() => Utf8SearchPlan.Prepare(facts));
            Assert.Equal(Utf8ResourceLimitKind.ConstructionDepth, failure.ResourceKind);
            Assert.Equal(2ul, failure.Requested);
            Assert.Equal(checkpoint, owner.Budget.ReservedBytes);
            Assert.Equal(1ul, owner.Budget.Depth);
        }
        finally { owner.Budget.LeaveDepth(); }
        Assert.True(Utf8SearchPlan.Prepare(facts).HasStructuralCandidates);
        Assert.Equal(0ul, owner.Budget.Depth);
        Assert.Equal(17, parent.Value);
    }
}
