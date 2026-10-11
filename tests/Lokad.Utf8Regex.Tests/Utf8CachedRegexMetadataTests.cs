using Lokad.Utf8Regex.Internal;
using Lokad.Utf8Regex.Internal.Caching;

namespace Lokad.Utf8Regex.Tests;

[Collection(Utf8RegexCacheCollection.Name)]
public sealed class Utf8CachedRegexMetadataTests
{
    [Fact]
    public void CachedFacadeBelongsToTheConstructionOwner()
    {
        using var owner = Utf8ConstructionScope.Enter();
        var regex = Utf8RegexCache.GetOrAdd("cache-root-" + Guid.NewGuid().ToString("N"), System.Text.RegularExpressions.RegexOptions.CultureInvariant);
        var found = false;
        for (var allocation = owner.Budget.ObjectCheckpoint; allocation is not null; allocation = allocation.Previous)
            found |= ReferenceEquals(regex, allocation.Value);
        Assert.True(found);
    }

    [Fact]
    public void FacadeStateCoversAllTwentyOneFields()
    {
        var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.DeclaredOnly;
        var fields = typeof(Utf8Regex).GetFields(flags);
        var state = typeof(Utf8Regex).GetNestedType("CachedConstructionState", System.Reflection.BindingFlags.NonPublic);
        Assert.NotNull(state); Assert.Equal(21, fields.Length); Assert.Equal(21, state.GetFields(flags).Length);
        Assert.Equal(fields.Select(static field => field.FieldType.ToString()).Order(), state.GetFields(flags).Select(static field => field.FieldType.ToString()).Order());
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void AdmissionDenialPrecedesFacadeAllocationAndMalformedPatternParsing(bool work)
    {
        var descriptor = Utf8ConstructionBudget.ObjectBytes<(object, Utf8ConstructionBudget.ObjectAllocation?, ulong)>();
        var loan = Utf8Regex.CachedObjectBytes + descriptor;
        using var owner = new Utf8ConstructionScope(ulong.MaxValue, work ? ulong.MaxValue : loan - 1,
            work ? 1ul : ulong.MaxValue, ulong.MaxValue);
        var failure = Assert.Throws<Utf8ResourceLimitExceededException>(() =>
            Utf8Regex.CreateAdmittedForCache("[", System.Text.RegularExpressions.RegexOptions.CultureInvariant, System.Text.RegularExpressions.Regex.InfiniteMatchTimeout));
        Assert.Equal(work ? Utf8ResourceLimitKind.ConstructionWorkSteps : Utf8ResourceLimitKind.ConstructionBytes, failure.ResourceKind);
        Assert.Equal(0ul, owner.Budget.ReservedBytes); Assert.Null(owner.Budget.ObjectCheckpoint); Assert.Equal(0ul, owner.Budget.WorkSteps);
    }

    [Fact]
    public void FailedPreparationRestoresTheParentAndKeepsSpentWork()
    {
        using var owner = Utf8ConstructionScope.Enter();
        _ = owner.Budget.AllocateObject(17, static _ => new object());
        var checkpoint = owner.Budget.ObjectCheckpoint;
        Assert.ThrowsAny<ArgumentException>(() => Utf8Regex.CreateAdmittedForCache("[",
            System.Text.RegularExpressions.RegexOptions.CultureInvariant, System.Text.RegularExpressions.Regex.InfiniteMatchTimeout));
        Assert.Equal(112ul, owner.Budget.ReservedBytes); Assert.Same(checkpoint, owner.Budget.ObjectCheckpoint);
        Assert.True(owner.Budget.WorkSteps > 2);
    }
}
