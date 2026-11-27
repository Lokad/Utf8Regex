using Lokad.Utf8Regex.Internal;
using Lokad.Utf8Regex.Internal.Planning;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8CandidatePlanAdmissionTests
{
    private sealed class ParentValue(int value) { internal readonly int Value = value; }

    [Fact]
    public void SearchPreparationHonorsDepthAndRecoversInTheSameOwner()
    {
        var facts = new Utf8SearchFacts(Utf8SearchKind.ExactAsciiLiteral) { LiteralUtf8 = "a"u8.ToArray() };
        using var owner = new Utf8ConstructionScope(0, ulong.MaxValue, 1);
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
        Assert.True(Utf8SearchPlan.Prepare(facts).HasPreparedSearcher);
        Assert.Equal(0ul, owner.Budget.Depth);
        Assert.Equal(17, parent.Value);
    }
}
