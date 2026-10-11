using System.Reflection;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.FrontEnd;
using Lokad.Utf8Regex.Internal.Planning;
using Xunit;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8VerifierProgramAdmissionTests
{
    public static IEnumerable<object[]> Roots() => from kind in Enumerable.Range(0, 9)
        from limit in new[] { "bytes", "work", "depth" } select new object[] { kind, limit };

    private static object Build(int kind, Utf8PreparedRegex prepared)
    {
        if (kind < 4)
            return Utf8StructuralVerifierRuntimeFactory.Create(prepared);
        if (kind < 8) return Utf8VerifierRuntime.Create(prepared);
        return Utf8RegexProgram.Compile("a", RegexOptions.CultureInvariant, Regex.InfiniteMatchTimeout);
    }

    [Theory]
    [MemberData(nameof(Roots))]
    public void RootCutoffsPreserveParentOwnershipAndSpentWork(int kind, string limit)
    {
        var prepared = Utf8VerifierTestPlans.WithStructuralVerifier(new((Utf8StructuralVerifierKind)(kind < 4 ? kind : Math.Min(kind - 4, 3)),
            default, default, default, null, default));
        ulong peak, work, depth, loan, slots;
        using (var funded = new Utf8ConstructionScope(ulong.MaxValue, ulong.MaxValue, ulong.MaxValue, ulong.MaxValue))
        {
            _ = funded.Budget.AllocateObject(17, static value => new Box(value));
            _ = Build(kind, prepared);
            peak = funded.Budget.PeakBytes - 112; work = funded.Budget.WorkSteps - 2;
            depth = funded.Budget.PeakDepth; loan = funded.Budget.ReservedBytes; slots = funded.Budget.Reserved;
        }
        Assert.True(peak > 0); Assert.True(work > 0); Assert.True(depth > 0);
        var maximum = kind == 8 ? limit switch
        {
            "bytes" => Utf8ConstructionScope.DefaultBufferBytes - 112,
            "work" => Utf8ConstructionScope.DefaultWorkSteps - 2,
            _ => Utf8ConstructionScope.DefaultDepth,
        } : limit == "bytes" ? peak : limit == "work" ? work : depth;
        // Primitive roots have few acquisitions; exhaust every cutoff. The full
        // preparer has cache-dependent work and adaptive scratch; its own suites
        // cover those producers. Exercise aggregate denial and the funded defaults.
        var allowances = kind == 8
            ? new[] { 0ul, 1ul, maximum }.Distinct().Order()
            : Enumerable.Range(0, checked((int)maximum + 1)).Select(value => (ulong)value);
        var successes = 0;
        foreach (var allowance in allowances)
        {
            using var owner = new Utf8ConstructionScope(ulong.MaxValue,
                limit == "bytes" ? 112 + allowance : ulong.MaxValue,
                limit == "work" ? 2 + allowance : ulong.MaxValue,
                limit == "depth" ? allowance : ulong.MaxValue);
            var parent = owner.Budget.AllocateObject(17, static value => new Box(value));
            var checkpoint = owner.Budget.ObjectCheckpoint;
            var error = Record.Exception(() => _ = Build(kind, prepared));
            if (kind == 8 && allowance <= 1) Assert.NotNull(error);
            if (error is null)
            {
                successes++; Assert.Equal(loan, owner.Budget.ReservedBytes);
                Assert.Equal(slots, owner.Budget.Reserved);
            }
            else
            {
                var expected = limit == "bytes" ? Utf8ResourceLimitKind.ConstructionBytes :
                    limit == "work" ? Utf8ResourceLimitKind.ConstructionWorkSteps : Utf8ResourceLimitKind.ConstructionDepth;
                Assert.Equal(expected, Assert.IsType<Utf8ResourceLimitExceededException>(error).ResourceKind);
                Assert.Equal(112ul, owner.Budget.ReservedBytes); Assert.Same(checkpoint, owner.Budget.ObjectCheckpoint);
                Assert.Equal(0ul, owner.Budget.Reserved);
            }
            Assert.Equal(17, parent.Value); Assert.Equal(0ul, owner.Budget.Depth);
            // Byte pressure can resize preparer scratch and spend additional work.
            // Only an explicit work limit supplies an upper bound for that lane.
            Assert.InRange(owner.Budget.WorkSteps, 2ul, limit == "work" ? 2 + allowance : ulong.MaxValue);
            if (limit == "depth" && allowance == 0)
            {
                Assert.Equal(112ul, owner.Budget.PeakBytes); Assert.Equal(2ul, owner.Budget.WorkSteps);
            }
        }
        Assert.Equal(1, successes);
    }

    [Fact]
    public void CompleteFieldStateIncludesSourcesAndBorrowsStructuralPayload()
    {
        Type[] types = [typeof(Utf8VerifierRuntime), typeof(Utf8StructuralVerifierRuntime), typeof(Utf8RegexProgram)];
        foreach (var type in types)
        {
            Assert.NotNull(type);
            var state = type.GetNestedType("ConstructionState", BindingFlags.NonPublic)!;
            Assert.NotNull(state);
            var fields = type.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
            var stateFields = state.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
            Assert.Equal(fields.Select(field => field.FieldType.FullName).Order(), stateFields.Select(field => field.FieldType.FullName).Order());
            Assert.Empty(type.GetConstructors(BindingFlags.Instance | BindingFlags.Public));
        }
        Assert.Equal(2, typeof(Utf8VerifierRuntime).GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly).Length);
        Assert.Single(typeof(Utf8StructuralVerifierRuntime).GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly));
        foreach (var type in new[] { typeof(Utf8NoStructuralVerifierRuntime), typeof(Utf8AsciiStructuralVerifierRuntime),
            typeof(Utf8ByteSafeLinearVerifierRuntime), typeof(Utf8ByteSafeLazyDfaVerifierRuntime) })
            Assert.Empty(type.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly));
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)]
    public void StructuralRootRetainsItsPreparedOwnerWithoutCopyingThePlan(int kind)
    {
        var prepared = Utf8VerifierTestPlans.WithStructuralVerifier(new((Utf8StructuralVerifierKind)kind,
            default, default, default, null, default));
        var runtime = Assert.IsAssignableFrom<Utf8StructuralVerifierRuntime>(Build(kind, prepared));
        var field = Assert.Single(typeof(Utf8StructuralVerifierRuntime)
            .GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly));
        Assert.Equal(typeof(Utf8PreparedRegex), field.FieldType); Assert.Same(prepared, field.GetValue(runtime));
        Assert.Equal(prepared.StructuralVerifier.Kind, runtime.Plan.Kind);
    }

    [Theory]
    [InlineData(false, false)] [InlineData(false, true)]
    [InlineData(true, false)] [InlineData(true, true)]
    public void BorrowedVerifierRootsAdmitFieldsAndRetainersBeforeAcquisition(bool finiteTimeout, bool bytes)
    {
        var timeout = finiteTimeout ? TimeSpan.FromSeconds(1) : Regex.InfiniteMatchTimeout;
        var regex = new Utf8Regex("a", RegexOptions.None, timeout);
        var prepared = regex.Inspection.PreparedRegex;
        object BuildSource() => Utf8VerifierRuntime.Create(prepared);
        ulong capacity, work;
        using (var funded = new Utf8ConstructionScope(ulong.MaxValue, ulong.MaxValue, ulong.MaxValue, ulong.MaxValue))
        {
            var result = Assert.IsType<Utf8VerifierRuntime>(BuildSource());
            Utf8OwnedVerifierAssertions.RetainsPreparedOwner(result);
            Assert.Same(prepared, typeof(Utf8VerifierRuntime).GetField("_preparedRegex", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(result));
            Assert.Equal(timeout, regex.MatchTimeout);
            Assert.Equal(RegexOptions.None, regex.Options);
            capacity = funded.Budget.ReservedBytes; work = funded.Budget.WorkSteps;
        }
        Assert.True(capacity > 0); Assert.Equal(4ul, work);
        var maximum = bytes ? capacity : work;
        var successes = 0;
        for (var allowance = 0ul; allowance <= maximum; allowance++)
        {
            using var owner = new Utf8ConstructionScope(ulong.MaxValue, bytes ? 112 + allowance : ulong.MaxValue,
                bytes ? ulong.MaxValue : 2 + allowance, ulong.MaxValue);
            var parent = owner.Budget.AllocateObject(17, static value => new Box(value));
            var checkpoint = owner.Budget.ObjectCheckpoint;
            var error = Record.Exception(() => _ = BuildSource());
            if (error is null)
            { successes++; Assert.Equal(112 + capacity, owner.Budget.ReservedBytes); }
            else
            {
                Assert.Equal(bytes ? Utf8ResourceLimitKind.ConstructionBytes : Utf8ResourceLimitKind.ConstructionWorkSteps,
                    Assert.IsType<Utf8ResourceLimitExceededException>(error).ResourceKind);
                Assert.Equal(112ul, owner.Budget.ReservedBytes); Assert.Same(checkpoint, owner.Budget.ObjectCheckpoint);
            }
            Assert.Equal(17, parent.Value); Assert.Equal(0ul, owner.Budget.Depth);
            Assert.InRange(owner.Budget.WorkSteps, 2ul, bytes ? 2 + work : 2 + allowance);
        }
        Assert.Equal(1, successes);
    }

    [Theory]
    [InlineData(0, false)] [InlineData(0, true)] [InlineData(1, false)]
    [InlineData(1, true)] [InlineData(2, false)] [InlineData(2, true)]
    public void OwnedPublicationKeepsConfigurationOrFailsEagerlyAndRecovers(int mode, bool valid)
    {
        var options = RegexOptions.CultureInvariant | (mode == 1 ? RegexOptions.Compiled :
            mode == 2 ? RegexOptions.NonBacktracking : RegexOptions.None);
        if (!valid)
        {
            using var owner = new Utf8ConstructionScope(ulong.MaxValue, ulong.MaxValue, ulong.MaxValue, ulong.MaxValue);
            var parent = owner.Budget.AllocateObject(17, static value => new Box(value));
            var checkpoint = owner.Budget.ObjectCheckpoint;
            ArgumentException? first = null;
            for (var attempt = 0; attempt < 2; attempt++)
            {
                Utf8Regex? unpublished = null;
                var workBefore = owner.Budget.WorkSteps;
                var error = Assert.ThrowsAny<ArgumentException>(() => unpublished = new Utf8Regex("(", options));
                Assert.Null(unpublished);
                if (first is not null) { Assert.Equal(first.Message, error.Message); Assert.Equal(first.ParamName, error.ParamName); }
                first = error;
                Assert.Equal(112ul, owner.Budget.ReservedBytes); Assert.Same(checkpoint, owner.Budget.ObjectCheckpoint);
                Assert.Equal(0ul, owner.Budget.Depth); Assert.True(owner.Budget.WorkSteps > workBefore);
            }
            Assert.Equal(17, parent.Value);
            var recovered = new Utf8Regex("(?<x>a)", options);
            Assert.Equal(1, recovered.Count("😀a"u8));
            Assert.Equal("a", recovered.MatchDetailed("😀a"u8).GetGroup("x").GetValueString());
            return;
        }
        var regex = new Utf8Regex("(?<x>a)", options);
        var prepared = regex.Inspection.PreparedRegex;
        var scalar = prepared.ScalarProgram;
        Assert.NotNull(scalar);
        Assert.Same(prepared.SemanticRegex.CaptureCatalog, scalar.CaptureCatalog);
        Assert.Equal("(?<x>a)", regex.Pattern); Assert.Equal(options, regex.Options);
        Assert.Equal(Regex.InfiniteMatchTimeout, regex.MatchTimeout);
        Parallel.For(0, 32, _ =>
        {
            Assert.Same(prepared, regex.Inspection.PreparedRegex);
            Assert.Same(scalar, regex.Inspection.AvailableOwnedSliceProgram);
            Assert.True(regex.IsMatch("😀a"u8));
            Assert.Equal("a", regex.MatchDetailed("😀a"u8).GetGroup("x").GetValueString());
        });
    }

    private sealed class Box(int value) { internal readonly int Value = value; }
}

internal static class Utf8VerifierTestPlans
{
    internal static Utf8PreparedRegex WithDirectFamily(Utf8PreparedRegex p, Utf8FallbackDirectFamilyPlan family)
        => Utf8PreparedRegex.Create(p.SemanticRegex, p.Features, p.ExecutionPattern, p.ExecutionKind, p.ExecutionBackend,
            p.CompiledPatternCategory, p.ExecutionTree, p.ExecutionProgram, p.ScalarProgram, p.OperationMatchCursorPlans,
            p.StructuralSearchPlan, p.DeterministicAnchor, p.DeterministicGuards, p.ScalarVerifier, p.StructuralVerifier,
            p.StructuralIdentifierFamilyPlan, p.StructuralTokenWindowPlan, p.StructuralRepeatedSegmentPlan,
            p.StructuralQuotedRelationPlan, p.OrderedLiteralWindowPlan, p.LiteralUtf8, p.FallbackReason, family);

    internal static Utf8PreparedRegex WithSearchPlan(Utf8PreparedRegex p, Utf8SearchPlan search)
        => Utf8PreparedRegex.Create(p.SemanticRegex, p.Features, p.ExecutionPattern, p.ExecutionKind, p.ExecutionBackend,
            p.CompiledPatternCategory, p.ExecutionTree, p.ExecutionProgram, p.ScalarProgram,
            Utf8OperationMatchCursorPlans.Create(p.ExecutionProgram, search, p.StructuralLinearProgram, p.SimplePatternPlan,
                search.StructuralSearchPlan, p.StructuralVerifier.AsciiProgram),
            search.StructuralSearchPlan, p.DeterministicAnchor, p.DeterministicGuards, p.ScalarVerifier, p.StructuralVerifier,
            p.StructuralIdentifierFamilyPlan, p.StructuralTokenWindowPlan, p.StructuralRepeatedSegmentPlan,
            p.StructuralQuotedRelationPlan, p.OrderedLiteralWindowPlan, p.LiteralUtf8, p.FallbackReason, p.FallbackDirectFamily);

    internal static Utf8PreparedRegex WithScalarVerifier(Utf8PreparedRegex p, Utf8ScalarVerifierPlan fallback)
        => Utf8PreparedRegex.Create(p.SemanticRegex, p.Features, p.ExecutionPattern, p.ExecutionKind, p.ExecutionBackend,
            p.CompiledPatternCategory, p.ExecutionTree, p.ExecutionProgram, p.ScalarProgram, p.OperationMatchCursorPlans,
            p.StructuralSearchPlan, p.DeterministicAnchor, p.DeterministicGuards, fallback, p.StructuralVerifier,
            p.StructuralIdentifierFamilyPlan, p.StructuralTokenWindowPlan, p.StructuralRepeatedSegmentPlan,
            p.StructuralQuotedRelationPlan, p.OrderedLiteralWindowPlan, p.LiteralUtf8, p.FallbackReason, p.FallbackDirectFamily);

    internal static Utf8PreparedRegex WithStructuralVerifier(Utf8StructuralVerifierPlan structuralVerifier)
    {
        var p = Utf8FrontEnd.Compile("a", RegexOptions.CultureInvariant);
        return Utf8PreparedRegex.Create(p.SemanticRegex, p.Features, p.ExecutionPattern, p.ExecutionKind, p.ExecutionBackend,
            p.CompiledPatternCategory, p.ExecutionTree, p.ExecutionProgram, p.ScalarProgram,
            Utf8OperationMatchCursorPlans.Create(p.ExecutionProgram, p.SearchPlan, p.StructuralLinearProgram, p.SimplePatternPlan, p.StructuralSearchPlan, structuralVerifier.AsciiProgram),
            p.StructuralSearchPlan, p.DeterministicAnchor, p.DeterministicGuards, p.ScalarVerifier, structuralVerifier, p.StructuralIdentifierFamilyPlan, p.StructuralTokenWindowPlan,
            p.StructuralRepeatedSegmentPlan, p.StructuralQuotedRelationPlan, p.OrderedLiteralWindowPlan,
            p.LiteralUtf8, p.FallbackReason, p.FallbackDirectFamily);
    }
}
