using System.Reflection;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Caching;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.FrontEnd;
using Lokad.Utf8Regex.Internal.Planning;
using Lokad.Utf8Regex.Internal.Replacement;
using Lokad.Utf8Regex.Internal.Search;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8ProgramCacheChargeTests
{
    [Theory]
    [InlineData("", false)] [InlineData("abc", false)] [InlineData("abc", true)]
    [InlineData(@"[\p{L}-[AE]]+", true)] [InlineData(@"(?<n>😀)\k<n>", false)]
    [InlineData("foo|bar", true)] [InlineData("Holmes.{0,25}Watson|Watson.{0,25}Holmes", true)]
    [InlineData("\\b(?:record|struct|class)\\s+[A-Z][A-Za-z0-9_]+", true)]
    public void CompletedCertificateCoversPhysicalGraphsAndExactPublicationBoundary(string pattern, bool compiled)
    {
        using var owner = Utf8ConstructionScope.Enter(); owner.Budget.ReserveBytes(7);
        var before = owner.Budget.ReservedBytes;
        var value = new Utf8Regex(pattern, RegexOptions.CultureInvariant | (compiled ? RegexOptions.Compiled : RegexOptions.None));
        var completed = owner.Budget.ReservedBytes - before;
        var child = (Utf8ReplacementPlanCache)typeof(Utf8Regex).GetField("_replacementCache", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(value)!;
        Assert.Equal(checked(completed + Utf8Regex.CachedObjectBytes + Utf8ConstructionBudget.StringBytes(pattern.Length) + child.MaximumRetainedBytes), value.CacheProgramBytes);
        Assert.True(value.CacheProgramBytes >= Utf8GraphQualification.Measure(value));
        var provider = default(Utf8RegexRootAndLazyStorage);
        Assert.True(provider.TryGetValueCharge(value, value.CacheProgramBytes, default(Utf8ConstructionTextNoControl), out var exact));
        Assert.Equal(value.CacheProgramBytes, exact);
        Assert.False(provider.TryGetValueCharge(value, exact - 1, default(Utf8ConstructionTextNoControl), out var denied));
        Assert.Equal(exact, denied);
        var field = typeof(Utf8Regex).GetField("_cacheProgramBytes", BindingFlags.Instance | BindingFlags.NonPublic)!;
        Assert.True(field.IsInitOnly); Assert.Equal(typeof(ulong), field.FieldType);
    }

    [Fact]
    public void NumericProviderAcquiresNothingAndRetainsTheSharedWorkCounter()
    {
        var value = new Utf8Regex(@"(?<n>😀)+", RegexOptions.Compiled | RegexOptions.CultureInvariant);
        var provider = default(Utf8RegexRootAndLazyStorage);
        using var owner = new Utf8ConstructionScope(0, 7, ulong.MaxValue, 0); owner.Budget.ReserveBytes(7);
        for (var i = 0; i < 1000; i++) Assert.True(provider.TryGetValueCharge(value, ulong.MaxValue, default(Utf8ConstructionTextNoControl), out _));
        var work = owner.Budget.WorkSteps;
        var correct = true;
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 100; i++) correct &= provider.TryGetValueCharge(value, ulong.MaxValue, default(Utf8ConstructionTextNoControl), out var charge) && charge == value.CacheProgramBytes;
        var acquired = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.True(correct); Assert.Equal(0, acquired);
        Assert.Equal(work + 100, owner.Budget.WorkSteps);
        Assert.Equal(7UL, owner.Budget.ReservedBytes); Assert.Equal(7UL, owner.Budget.PeakBytes);
        Assert.Equal(0UL, owner.Budget.Reserved); Assert.Equal(0UL, owner.Budget.Depth);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void PublicationInheritsOriginalInterruptionBeforeOversizeDecline(bool timeout)
    {
        var value = new Utf8Regex("abc");
        using var owner = Utf8ConstructionScope.Enter(); owner.Budget.ReserveBytes(7);
        using var token = new CancellationTokenSource(); long ticks = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
        var deadline = Utf8ExecutionDeadline.Start(TimeSpan.FromMilliseconds(50));
        owner.Budget.Deadline = deadline; owner.Budget.CancellationToken = token.Token;
        owner.Budget.CheckpointHook = _ => { if (timeout) ticks = 50; else token.Cancel(); };
        var failure = Record.Exception(() => default(Utf8RegexRootAndLazyStorage).TryGetValueCharge(value, 0, default(Utf8ConstructionTextNoControl), out _));
        if (timeout) Assert.IsType<Utf8ExecutionDeadlineExpiredException>(failure);
        else Assert.Equal(token.Token, Assert.IsType<OperationCanceledException>(failure).CancellationToken);
        Assert.Same(deadline, owner.Budget.Deadline); Assert.Equal(7UL, owner.Budget.ReservedBytes);
        Assert.Equal(0UL, owner.Budget.WorkSteps);
    }

    [Fact]
    public void OneCoarseEnvelopeCoversTheClosedBorrowedAndFixedMetadataFamilies()
    {
        var state = typeof(Utf8OperationMatchCursorPlans).GetNestedType("ConstructionState", BindingFlags.NonPublic)!;
        var shared = (ulong)typeof(Utf8ConstructionBudget).GetMethod("ObjectBytes", BindingFlags.Static | BindingFlags.NonPublic)!.MakeGenericMethod(state).Invoke(null, null)!
            + Utf8ConstructionBudget.ObjectBytes<PreparedByteSetState>() + 2 * Utf8ConstructionBudget.ObjectBytes<(Array, object, int)>()
            + Utf8ConstructionBudget.StringBytes(1);
        var fixedArrays = Utf8ConstructionBudget.ArrayBytes<Utf8WindowSearchFacts>(2)
            + Utf8ConstructionBudget.ArrayBytes<byte>(1) + Utf8ConstructionBudget.ArrayBytes<byte[]>(1)
            + Utf8ConstructionBudget.ArrayBytes<AsciiSimplePatternAnchoredValidatorSegment>(2);
        Assert.InRange(shared + fixedArrays, 1UL, Utf8Regex.ProgramFixedMetadataBytes);
        // Static empties have no nonempty child payload; the stored schema witnesses
        // and semantic acquisition audit separately cover all reference fields.
        Assert.Equal(0, Utf8ConstructionReadOnlyMap<string, int>.Empty.Count);
        Assert.Equal(0, Utf8ConstructionReadOnlyMap<int, int>.Empty.Count);
        Assert.False(PreparedByteSet.Empty.Contains((byte)'a'));
    }
}
