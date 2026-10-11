using System.Reflection;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.FrontEnd;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8SimplePatternProducerAdmissionTests
{
    private const RegexOptions Options = RegexOptions.CultureInvariant;
    private sealed class ParentValue(int value) { internal readonly int Value = value; }

    public static IEnumerable<object[]> Shapes()
    {
        yield return ["[0-9]{2}", Options];
        yield return ["ab[cd]ef", Options];
        yield return ["(?=abc)ab[cd]", Options];
        yield return ["(?:ab|cd){1,2}[xy]", Options];
        yield return ["(?=1234)12[CD]", Options | RegexOptions.IgnoreCase];
        yield return ["\\Aab[0-9]{1,2}\\Z", Options];
        yield return ["^[0-9]{1,2}/[0-9]{1,2}/[0-9]{2,3}$", Options];
        yield return ["^([0-9]{4}[- ]){3}[0-9]{3,4}$", Options];
        yield return ["^[a-zA-Z]{1,2}[0-9][0-9A-Za-z]? ?[0-9][A-Za-z]{2}$", Options];
        yield return ["\\s[a-zA-Z]{0,3}ing\\s", Options];
        yield return ["Holmes.{0,2}Watson|Watson.{0,2}Holmes", Options];
        yield return ["^(?:[0-9]x|[0-9]{3}x)$", Options];
        yield return ["^(?:[0-9]x[A-Z]|[0-9]{2}x[A-Z]{2})$", Options];
    }

    private static AsciiSimplePatternPlan Create(Utf8RegexAnalysis prepared, RegexOptions options)
    {
        Assert.True(Utf8AsciiSimplePatternLowerer.TryCreatePlan(prepared.SemanticRegex, options, out var plan, out _));
        return plan;
    }

    [Fact]
    public void ReturnedTokenTablesKeepTheirConstructionLoans()
    {
        var prepared = Utf8FrontEnd.Analyze("[0-9]{2}", Options);
        using var owner = Utf8ConstructionScope.Enter();
        var plan = Create(prepared, Options);
        Assert.NotEmpty(plan.Branches);
        var retained = Utf8ConstructionBudget.ArrayBytes<AsciiSimplePatternToken[]>(plan.Branches.Length);
        foreach (var branch in plan.Branches)
            retained += Utf8ConstructionBudget.ArrayBytes<AsciiSimplePatternToken>(branch.Length);
        Assert.True(owner.Budget.ReservedBytes >= retained + 128ul);
        Assert.Equal(0ul, owner.Budget.Depth);
    }

    [Theory]
    [MemberData(nameof(Shapes))]
    public void ReturnedMajorArraysFitNumericAdmissionAlongsideConservativeHelpers(string pattern, RegexOptions options)
    {
        var prepared = Utf8FrontEnd.Analyze(pattern, options);
        using var owner = Utf8ConstructionScope.Enter();
        Assert.True(Utf8AsciiSimplePatternLowerer.TryCreatePlan(prepared.SemanticRegex, options, out var plan, out var facts));
        var visited = new HashSet<object>(ReferenceEqualityComparer.Instance);
        var bytes = 0ul;
        Visit(plan); Visit(facts);
        Assert.True(owner.Budget.ReservedBytes >= bytes + 128ul);
        Assert.True(owner.Budget.PeakBytes > owner.Budget.ReservedBytes);
        Assert.Equal(0ul, owner.Budget.Depth);
        Assert.Equal(0ul, owner.Budget.Reserved);

        void Visit(object? value)
        {
            if (value is null || !visited.Add(value)) return;
            var type = value.GetType();
            if (value is Array array)
            {
                var measure = typeof(Utf8ConstructionBudget).GetMethod("ArrayBytes", BindingFlags.Static | BindingFlags.NonPublic)
                    ?? throw new InvalidOperationException();
                bytes += (ulong)(measure.MakeGenericMethod(type.GetElementType() ?? throw new InvalidOperationException())
                    .Invoke(null, [array.Length]) ?? throw new InvalidOperationException());
                foreach (var element in array) Visit(element);
                return;
            }
            if (type.Namespace?.StartsWith("Lokad.Utf8Regex.Internal", StringComparison.Ordinal) != true || type.IsEnum) return;
            foreach (var field in type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
                Visit(field.GetValue(value));
        }
    }

    [Theory]
    [MemberData(nameof(Shapes))]
    public void EveryWorkCutoffRestoresParentAndNeverRefundsSpentWork(string pattern, RegexOptions options)
    {
        var prepared = Utf8FrontEnd.Analyze(pattern, options);
        ulong work;
        using (var measured = Utf8ConstructionScope.Enter())
        {
            Create(prepared, options);
            work = measured.Budget.WorkSteps;
        }
        for (var cutoff = 0ul; cutoff <= work; cutoff++)
        {
            using var owner = new Utf8ConstructionScope(0, ulong.MaxValue, cutoff + 2, ulong.MaxValue);
            var parent = owner.Budget.AllocateObject(17, static value => new ParentValue(value));
            var checkpoint = owner.Budget.ReservedBytes;
            using (var discarded = Utf8ConstructionScope.Enter())
            {
                var failure = Record.Exception(() => Create(prepared, options));
                if (cutoff < work)
                {
                    var breach = Assert.IsType<Utf8ResourceLimitExceededException>(failure);
                    Assert.Equal(Utf8ResourceLimitKind.ConstructionWorkSteps, breach.ResourceKind);
                    Assert.Equal(cutoff + 2, breach.Limit);
                    Assert.True(breach.Requested > breach.Limit);
                }
                else Assert.Null(failure);
            }
            AssertParent(owner.Budget, checkpoint, parent);
            Assert.InRange(owner.Budget.WorkSteps, 2ul, cutoff + 2);
        }
    }

    [Theory]
    [MemberData(nameof(Shapes))]
    public void CapacityAndDepthCutoffsRecoverWithoutLosingParentLoans(string pattern, RegexOptions options)
    {
        var prepared = Utf8FrontEnd.Analyze(pattern, options);
        var recovery = Utf8FrontEnd.Analyze("ab", Options);
        ulong peak, depth, recoveryPeak;
        using (var measured = Utf8ConstructionScope.Enter())
        {
            measured.Budget.AllocateObject(17, static value => new ParentValue(value));
            Create(prepared, options);
            peak = measured.Budget.PeakBytes;
            depth = measured.Budget.PeakDepth;
        }
        using (var measured = Utf8ConstructionScope.Enter())
        {
            measured.Budget.AllocateObject(17, static value => new ParentValue(value));
            Create(recovery, Options);
            recoveryPeak = measured.Budget.PeakBytes;
        }
        foreach (var cap in new[] { 128ul, 224ul, 512ul, 1024ul, peak / 2, peak - 1, peak }.Distinct().Order())
        {
            using var owner = new Utf8ConstructionScope(0, cap, ulong.MaxValue, ulong.MaxValue);
            var parent = owner.Budget.AllocateObject(17, static value => new ParentValue(value));
            var checkpoint = owner.Budget.ReservedBytes;
            using (var discarded = Utf8ConstructionScope.Enter())
            {
                var failure = Record.Exception(() => Create(prepared, options));
                if (failure is not null)
                {
                    var breach = Assert.IsType<Utf8ResourceLimitExceededException>(failure);
                    Assert.Equal(Utf8ResourceLimitKind.ConstructionBytes, breach.ResourceKind);
                    Assert.Equal(cap, breach.Limit);
                    Assert.True(breach.Requested > cap);
                }
                if (cap == peak) Assert.Null(failure);
            }
            AssertParent(owner.Budget, checkpoint, parent);
            Assert.True(owner.Budget.PeakBytes <= cap);
            if (cap >= recoveryPeak)
            {
                var spent = owner.Budget.WorkSteps;
                using (var discarded = Utf8ConstructionScope.Enter()) Create(recovery, Options);
                Assert.True(owner.Budget.WorkSteps > spent);
                AssertParent(owner.Budget, checkpoint, parent);
            }
        }
        for (var cap = 0ul; cap < depth; cap++)
        {
            using var owner = new Utf8ConstructionScope(0, ulong.MaxValue, ulong.MaxValue, cap);
            var parent = owner.Budget.AllocateObject(17, static value => new ParentValue(value));
            var checkpoint = owner.Budget.ReservedBytes;
            var breach = Assert.Throws<Utf8ResourceLimitExceededException>(() => Create(prepared, options));
            Assert.Equal(Utf8ResourceLimitKind.ConstructionDepth, breach.ResourceKind);
            Assert.Equal(cap, breach.Limit);
            Assert.True(breach.Requested > cap);
            AssertParent(owner.Budget, checkpoint, parent);
        }
    }

    public static IEnumerable<object[]> Interruptions()
    {
        foreach (var shape in Shapes())
        {
            yield return [shape[0], shape[1], false];
            yield return [shape[0], shape[1], true];
        }
    }

    [Theory]
    [MemberData(nameof(Interruptions))]
    public void EveryCheckpointKeepsInheritedTokenAndDeadlineAuthority(string pattern, RegexOptions options, bool timeout)
    {
        var prepared = Utf8FrontEnd.Analyze(pattern, options);
        var count = 0;
        using (var measured = Utf8ConstructionScope.Enter())
        {
            measured.Budget.CheckpointHook = _ => count++;
            Create(prepared, options);
        }
        Assert.True(count > 0);
        for (var target = 1; target <= count; target++)
        {
            using var owner = Utf8ConstructionScope.Enter();
            var parent = owner.Budget.AllocateObject(17, static value => new ParentValue(value));
            var checkpoint = owner.Budget.ReservedBytes;
            using var cancellation = new CancellationTokenSource();
            long ticks = 0;
            using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
            var deadline = Utf8ExecutionDeadline.Start(TimeSpan.FromSeconds(30));
            owner.Budget.Deadline = deadline;
            owner.Budget.CancellationToken = cancellation.Token;
            var current = 0;
            owner.Budget.CheckpointHook = _ =>
            {
                if (++current != target) return;
                if (timeout) ticks = 30000;
                else cancellation.Cancel();
            };
            var failure = Record.Exception(() => Create(prepared, options));
            if (timeout) Assert.IsType<Utf8ExecutionDeadlineExpiredException>(failure);
            else Assert.Equal(cancellation.Token, Assert.IsType<OperationCanceledException>(failure).CancellationToken);
            Assert.Same(deadline, owner.Budget.Deadline);
            AssertParent(owner.Budget, checkpoint, parent);
            owner.Budget.CheckpointHook = null;
            owner.Budget.CancellationToken = default;
            owner.Budget.Deadline = Utf8ExecutionDeadline.Infinite;
            using (var discarded = Utf8ConstructionScope.Enter()) Create(prepared, options);
            AssertParent(owner.Budget, checkpoint, parent);
        }
    }

    private static void AssertParent(Utf8ConstructionBudget budget,
        ulong checkpoint, ParentValue parent)
    {
        Assert.Equal(128ul, budget.ReservedBytes);
        Assert.Equal(0ul, budget.Reserved);
        Assert.Equal(0ul, budget.Depth);
        Assert.Equal(checkpoint, budget.ReservedBytes);
        Assert.Equal(17, parent.Value);
    }

    [Theory]
    [InlineData("(?=AB)A[CD]", Options | RegexOptions.IgnoreCase)]
    [InlineData("[ab]{96}", Options)]
    [InlineData("é", Options)]
    [InlineData("(?<x>😀)", Options)]
    [InlineData("(?:a|b|c|d|e){3}", Options)]
    [InlineData("(?-i:ab)[cd]", Options | RegexOptions.IgnoreCase)]
    public void SpeculativeDeclinesReleaseAllScratchWithoutRefundingWork(string pattern, RegexOptions options)
    {
        var prepared = Utf8FrontEnd.Analyze(pattern, options);
        var recovery = Utf8FrontEnd.Analyze("ab", Options);
        using var owner = Utf8ConstructionScope.Enter();
        var parent = owner.Budget.AllocateObject(17, static value => new ParentValue(value));
        var checkpoint = owner.Budget.ReservedBytes;
        Assert.False(Utf8AsciiSimplePatternLowerer.TryCreatePlan(prepared.SemanticRegex, options, out _, out _));
        Assert.True(owner.Budget.WorkSteps > 2);
        AssertParent(owner.Budget, checkpoint, parent);
        var spent = owner.Budget.WorkSteps;
        using (var discarded = Utf8ConstructionScope.Enter()) Create(recovery, Options);
        Assert.True(owner.Budget.WorkSteps > spent);
        AssertParent(owner.Budget, checkpoint, parent);
    }
}
