using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Planning;
using Xunit;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8NonliteralRuntimeFactoryAdmissionTests
{
    private static Utf8PreparedRegex Plan(string pattern) => (Utf8PreparedRegex)typeof(Utf8Regex)
        .GetProperty("PreparedRegex", BindingFlags.Instance | BindingFlags.NonPublic)!
        .GetValue(new Utf8Regex(pattern, RegexOptions.CultureInvariant))!;

    public static IEnumerable<object[]> Roots() => from kind in Enum.GetValues<Utf8CompiledEngineKind>()
        where kind is not (Utf8CompiledEngineKind.ExactLiteral or Utf8CompiledEngineKind.LiteralFamily)
        from limit in new[] { "bytes", "work", "depth" } select new object[] { (int)kind, limit };

    [Fact]
    public void RemainingPayloadStatesCoverStoredFieldsAndNoDiscardedImplementationSurvives()
    {
        Type[] types = [typeof(Utf8CompiledSymmetricLiteralWindowCounter),
            typeof(Utf8EmittedTokenFamilyMatcher)];
        foreach (var type in types)
        {
            var state = type.GetNestedType("ConstructionState", BindingFlags.NonPublic)!;
            var fields = type.GetFields(BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly);
            var stateFields = state.GetFields(BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly);
            Assert.Equal(fields.Select(f => f.FieldType.FullName).Order(), stateFields.Select(f => f.FieldType.FullName).Order());
            Assert.Empty(type.GetConstructors(BindingFlags.Instance | BindingFlags.Public));
        }
        Assert.Null(typeof(Utf8Regex).Assembly.GetType("Lokad.Utf8Regex.Internal.Execution.Utf8NonLiteralCompiledEngineRuntime"));
        Assert.Single(typeof(Utf8StructuralLinearRuntime).GetFields(BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly));
        foreach (var type in new[] { typeof(Utf8NoStructuralLinearRuntime), typeof(Utf8AsciiInstructionLinearRuntime), typeof(Utf8AsciiStructuralFamilyLinearRuntime) })
            Assert.Empty(type.GetFields(BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly));
    }

    private static Utf8CompiledEngineRuntime Build(int kind, Utf8PreparedRegex plan, Utf8VerifierRuntime verifier) =>
        Utf8CompiledRuntimeFactory.Create(new Utf8CompiledEngine((Utf8CompiledEngineKind)kind), plan, verifier, RegexOptions.None);

    [Theory]
    [MemberData(nameof(Roots))]
    public void EveryStorageWorkAndDepthCutoffUnwindsChildrenWithoutRefundingWork(int kind, string limit)
    {
        const string pattern = "aaaaa|bbbbb|ccccc";
        var plan = Plan(pattern);
        var verifier = Utf8VerifierRuntime.Create(plan);
        ulong peak, work, depth, loan;
        using (var funded = new Utf8ConstructionScope(0, ulong.MaxValue, ulong.MaxValue, ulong.MaxValue))
        {
            _ = funded.Budget.AllocateObject(17, static v => new Box(v));
            _ = Build(kind, plan, verifier);
            peak = funded.Budget.PeakBytes - 128; work = funded.Budget.WorkSteps - 2;
            depth = funded.Budget.PeakDepth; loan = funded.Budget.ReservedBytes;
        }
        var maximum = limit == "bytes" ? peak : limit == "work" ? work : depth;
        var successes = 0;
        for (var allowance = 0ul; allowance <= maximum; allowance++)
        {
            using var owner = new Utf8ConstructionScope(0, limit == "bytes" ? 128 + allowance : ulong.MaxValue,
                limit == "work" ? 2 + allowance : ulong.MaxValue, limit == "depth" ? allowance : ulong.MaxValue);
            var parent = owner.Budget.AllocateObject(17, static v => new Box(v));
            var checkpoint = owner.Budget.ReservedBytes;
            var error = Record.Exception(() => _ = Build(kind, plan, verifier));
            if (error is null)
            {
                successes++; Assert.Equal(loan, owner.Budget.ReservedBytes);
            }
            else
            {
                var expected = limit == "bytes" ? Utf8ResourceLimitKind.ConstructionBytes :
                    limit == "work" ? Utf8ResourceLimitKind.ConstructionWorkSteps : Utf8ResourceLimitKind.ConstructionDepth;
                Assert.Equal(expected, Assert.IsType<Utf8ResourceLimitExceededException>(error).ResourceKind);
                Assert.Equal(128ul, owner.Budget.ReservedBytes); Assert.Equal(checkpoint, owner.Budget.ReservedBytes);
            }
            Assert.Equal(17, parent.Value); Assert.Equal(0ul, owner.Budget.Depth); Assert.Equal(0ul, owner.Budget.Reserved);
            Assert.InRange(owner.Budget.WorkSteps, 2ul, limit == "work" ? 2 + allowance : 2 + work);
            if (allowance == 0 && limit == "depth")
            { Assert.Equal(128ul, owner.Budget.PeakBytes); Assert.Equal(2ul, owner.Budget.WorkSteps); }
        }
        Assert.Equal(1, successes);
    }


    private static Utf8PreparedRegex WithSearch(Utf8PreparedRegex p, Utf8SearchPlan search,
        Utf8StructuralLinearProgram program) =>
        Utf8PreparedRegex.Create(p.SemanticRegex, p.Features, p.ExecutionPattern, NativeExecutionKind.FallbackRegex, p.ExecutionBackend,
            p.CompiledPatternCategory, p.ExecutionTree, p.ScalarProgram,
            Utf8OperationMatchCursorPlans.Create(search, program, p.SimplePatternPlan, p.StructuralSearchPlan, p.StructuralVerifier.AsciiProgram),
            p.StructuralSearchPlan, p.StructuralVerifier, p.StructuralIdentifierFamilyPlan, p.StructuralTokenWindowPlan,
            p.StructuralRepeatedSegmentPlan, p.StructuralQuotedRelationPlan, p.OrderedLiteralWindowPlan,
            p.LiteralUtf8, "unsupported_loop", p.FallbackDirectFamily);

    public static IEnumerable<object[]> Helpers() => from kind in Enumerable.Range(0, 6)
        from bytes in new[] { false, true } select new object[] { kind, bytes };

    [Theory]
    [MemberData(nameof(Helpers))]
    public void PlainHelperAdmissionBorrowsPayloadsAndUnwindsEveryCutoff(int kind, bool bytes)
    {
        var originalPlan = Plan("abc");
        var program = originalPlan.StructuralLinearProgram;
        if (kind is 1 or 2)
        {
            object boxed = program;
            typeof(Utf8StructuralLinearProgram).GetField("<Kind>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance)!
                .SetValue(boxed, kind == 1 ? Utf8StructuralLinearProgramKind.AsciiCharClassRun : Utf8StructuralLinearProgramKind.AsciiStructuralFamily);
            program = (Utf8StructuralLinearProgram)boxed;
        }
        var holderPlan = WithSearch(originalPlan, originalPlan.SearchPlan, program);
        byte[] first = "ab"u8.ToArray(), second = "cd"u8.ToArray();
        var symmetric = new AsciiSimplePatternSymmetricLiteralWindowPlan(first, second, default,
            0, (byte)'a', (byte)'c', 0, 8, false, 1, (byte)'b', (byte)'d', 0, (byte)'a', (byte)'c');
        // Prepare primitive inputs explicitly. Public scalar admission may bypass
        // these legacy optimization shapes; this test qualifies holder accounting.
        var tokenPlan = kind == 4
            ? Utf8FallbackDirectFamilyPlan.ForBoundedDateToken(1, 2, 1, 2, 2, 4, (byte)'/', (byte)'/', true, true)
            : Utf8FallbackDirectFamilyPlan.ForKind(Utf8FallbackDirectFamilyKind.Utf8UriToken);
        object Acquire()
        {
            if (kind < 3) return Utf8StructuralLinearRuntime.Create(holderPlan);
            if (kind == 3) return Utf8CompiledSymmetricLiteralWindowCounter.Create(symmetric);
            Assert.True(Utf8EmittedTokenFamilyMatcher.TryCreate(tokenPlan, out var result));
            return result!;
        }
        ulong loan;
        using (var funded = new Utf8ConstructionScope(0, ulong.MaxValue, ulong.MaxValue, 0))
        {
            var result = Acquire(); loan = funded.Budget.ReservedBytes;
            Assert.Equal(2ul, funded.Budget.WorkSteps); Assert.Equal(0ul, funded.Budget.PeakDepth);
            if (kind < 3)
            {
                Assert.Equal(new[] { typeof(Utf8NoStructuralLinearRuntime), typeof(Utf8AsciiInstructionLinearRuntime), typeof(Utf8AsciiStructuralFamilyLinearRuntime) }[kind], result.GetType());
                Assert.Same(holderPlan, typeof(Utf8StructuralLinearRuntime).GetField("_regexPlan", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(result));
                Assert.Equal(128ul, loan);
            }
            if (kind == 3)
            {
                Assert.Same(first, result.GetType().GetField("_firstLiteral", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(result));
                Assert.Same(second, result.GetType().GetField("_secondLiteral", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(result));
            }
        }
        var maximum = bytes ? loan : 2ul;
        for (var allowance = 0ul; allowance <= maximum; allowance++)
        {
            using var owner = new Utf8ConstructionScope(0, bytes ? 128 + allowance : ulong.MaxValue,
                bytes ? ulong.MaxValue : 2 + allowance, 0);
            var parent = owner.Budget.AllocateObject(17, static v => new Box(v)); var checkpoint = owner.Budget.ReservedBytes;
            var error = Record.Exception(() => _ = Acquire());
            if (allowance < maximum)
            {
                Assert.Equal(bytes ? Utf8ResourceLimitKind.ConstructionBytes : Utf8ResourceLimitKind.ConstructionWorkSteps,
                    Assert.IsType<Utf8ResourceLimitExceededException>(error).ResourceKind);
                Assert.Equal(128ul, owner.Budget.ReservedBytes); Assert.Equal(checkpoint, owner.Budget.ReservedBytes);
            }
            else { Assert.Null(error); Assert.Equal(128 + loan, owner.Budget.ReservedBytes); }
            Assert.Equal(17, parent.Value); Assert.Equal(0ul, owner.Budget.Depth);
            Assert.Equal(allowance == maximum ? 4ul : 2ul, owner.Budget.WorkSteps);
            Assert.Equal("ab"u8.ToArray(), first); Assert.Equal("cd"u8.ToArray(), second);
        }
    }

    private sealed class Box(int value) { internal readonly int Value = value; }
}
