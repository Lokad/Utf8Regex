using System.Reflection;
using Lokad.Utf8Regex.Internal;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Replacement;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8ReplacementPreparationAdmissionTests
{
    private sealed class ParentValue(int value) { internal readonly int Value = value; }

    public static IEnumerable<object[]> Shapes()
    {
        yield return ["", false];
        yield return ["café😀", false];
        yield return ["a$$z", false];
        yield return ["$&$`$'$+$_", false];
        yield return ["$001$1${1}z", false];
        yield return ["a${missing}$0012z", false];
        yield return ["${seven}$7${one}$1", false];
        yield return ["${_é}${😀}${}", false];
        yield return ["$2147483648${2147483648}${unclosed", false];
        yield return ["$123$701$001", true];
        yield return ["${missing}${absent}x$0012", true];
    }

    private static Utf8AnalyzedReplacement Create(string template, bool ecma) =>
        Utf8FrontEndReplacementAnalyzer.Analyze(System.Text.Encoding.UTF8.GetBytes(template), [0, 1, 7, 12], ["zero", "one", "seven", "twelve"], ecma);

    [Fact]
    public void PublishedReplacementHoldersAndMajorArraysFitAdmission()
    {
        using var owner = Utf8ConstructionScope.Enter();
        var result = Utf8FrontEndReplacementAnalyzer.Analyze("a${missing}$1$1z"u8, [0, 1], ["zero", "one"]);
        Assert.False(result.Plan.Instructions.IsEmpty);
        Assert.True(owner.Budget.ReservedBytes >= 256ul +
            Utf8ConstructionBudget.ArrayBytes<Utf8ReplacementInstruction>(result.Plan.Instructions.Length));
    }


    [Theory]
    [MemberData(nameof(Shapes))]
    public void ReturnedMajorArraysFitNumericAdmissionAlongsideConservativeHelpers(string template, bool ecma)
    {
        using var owner = Utf8ConstructionScope.Enter();
        var result = Create(template, ecma);
        var visited = new HashSet<object>(ReferenceEqualityComparer.Instance);
        var bytes = 0ul;
        Visit(result);
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
    public void CapacityAndDepthCutoffsRecoverWithoutLosingParentLoans(string template, bool ecma)
    {
        using var checkpointObservation = global::Lokad.Utf8Regex.Internal.Diagnostics.Utf8SearchDiagnosticsSession.Start();
        ulong peak, depth, recoveryPeak;
        using (var measured = Utf8ConstructionScope.Enter())
        {
            measured.Budget.AllocateObject(17, static value => new ParentValue(value));
            Create(template, ecma);
            peak = measured.Budget.PeakBytes;
            depth = measured.Budget.PeakDepth;
        }
        using (var measured = Utf8ConstructionScope.Enter())
        {
            measured.Budget.AllocateObject(17, static value => new ParentValue(value));
            Create("a", false);
            recoveryPeak = measured.Budget.PeakBytes;
        }
        foreach (var cap in new[] { 128ul, 224ul, 512ul, 1024ul, peak / 2, peak - 1, peak }.Distinct().Order())
        {
            using var owner = new Utf8ConstructionScope(0, cap, ulong.MaxValue);
            var parent = owner.Budget.AllocateObject(17, static value => new ParentValue(value));
            var checkpoint = owner.Budget.ReservedBytes;
            using (var discarded = Utf8ConstructionScope.Enter())
            {
                var failure = Record.Exception(() => Create(template, ecma));
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
                var spent = owner.Budget.DiagnosticCheckpoints;
                using (var discarded = Utf8ConstructionScope.Enter()) Create("a", false);
                Assert.True(owner.Budget.DiagnosticCheckpoints > spent);
                AssertParent(owner.Budget, checkpoint, parent);
            }
        }
        for (var cap = 0ul; cap < depth; cap++)
        {
            using var owner = new Utf8ConstructionScope(0, ulong.MaxValue, cap);
            var parent = owner.Budget.AllocateObject(17, static value => new ParentValue(value));
            var checkpoint = owner.Budget.ReservedBytes;
            var breach = Assert.Throws<Utf8ResourceLimitExceededException>(() => Create(template, ecma));
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
    public void EveryCheckpointKeepsInheritedTokenAndDeadlineAuthority(string template, bool ecma, bool timeout)
    {
        var count = 0;
        using (var measured = Utf8ConstructionScope.Enter())
        {
            measured.Budget.CheckpointHook = () => count++;
            Create(template, ecma);
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
            owner.Budget.CheckpointHook = () =>
            {
                if (++current != target) return;
                if (timeout) ticks = 30000;
                else cancellation.Cancel();
            };
            var failure = Record.Exception(() => Create(template, ecma));
            if (timeout) Assert.IsType<Utf8ExecutionDeadlineExpiredException>(failure);
            else Assert.Equal(cancellation.Token, Assert.IsType<OperationCanceledException>(failure).CancellationToken);
            Assert.Same(deadline, owner.Budget.Deadline);
            AssertParent(owner.Budget, checkpoint, parent);
            owner.Budget.CheckpointHook = null;
            owner.Budget.CancellationToken = default;
            owner.Budget.Deadline = Utf8ExecutionDeadline.Infinite;
            using (var discarded = Utf8ConstructionScope.Enter()) Create(template, ecma);
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


    [Fact]
    public void UnknownReferencesKeepOriginalFlagsAndLiteralBytesAreSharedWithThePlan()
    {
        var result = Create("${missing}$00123", false);
        Assert.True(result.IsLiteral);
        Assert.True(result.ContainsSubstitutions);
        Assert.True(result.ContainsGroupReferences);
        Assert.True(result.ContainsNamedGroups);
        Assert.False(result.ContainsSpecialSubstitutions);
        Assert.Equal(1, result.Plan.Instructions.Length);
        Assert.Same(result.Plan.Instructions[0].LiteralUtf8, result.LiteralUtf8);
        Assert.Equal("${missing}$00123", System.Text.Encoding.UTF8.GetString(result.LiteralUtf8));
        var regex = new Utf8Regex("(a)(b)(c)(d)(e)(f)(g)(h)(i)(j)(k)(l)");
        Assert.Equal("gaglabcdefghijkl", System.Text.Encoding.UTF8.GetString(regex.Replace("abcdefghijkl"u8, "$7$1$7$12$0"u8)));
    }

    [Theory]
    [InlineData("parse")]
    [InlineData("named")]
    [InlineData("unknown-literal")]
    [InlineData("numeric")]
    [InlineData("literal-text")]
    public void StandaloneFactoriesKeepEveryWorkCutoffAndInterruptionInTheParentOwner(string stage)
    {
        using var checkpointObservation = global::Lokad.Utf8Regex.Internal.Diagnostics.Utf8SearchDiagnosticsSession.Start();
        var operation = Stage(stage);
        ulong work, peak, depth;
        var checkpoints = 0;
        using (var measured = Utf8ConstructionScope.Enter())
        {
            measured.Budget.AllocateObject(17, static value => new ParentValue(value));
            measured.Budget.CheckpointHook = () => checkpoints++;
            operation();
            work = measured.Budget.DiagnosticCheckpoints - 2;
            peak = measured.Budget.PeakBytes;
            depth = measured.Budget.PeakDepth;
        }
        Assert.True(work > 0);
        foreach (var cap in new[] { 128ul, 224ul, 512ul, peak / 2, peak - 1, peak }.Distinct().Order())
        {
            using var owner = new Utf8ConstructionScope(0, cap, ulong.MaxValue);
            var parent = owner.Budget.AllocateObject(17, static value => new ParentValue(value));
            var checkpoint = owner.Budget.ReservedBytes;
            using (var discarded = Utf8ConstructionScope.Enter())
            {
                var failure = Record.Exception(() => operation());
                if (failure is not null)
                    Assert.Equal(Utf8ResourceLimitKind.ConstructionBytes,
                        Assert.IsType<Utf8ResourceLimitExceededException>(failure).ResourceKind);
                if (cap == peak) Assert.Null(failure);
            }
            AssertParent(owner.Budget, checkpoint, parent);
            Assert.True(owner.Budget.PeakBytes <= cap);
        }
        for (var cap = 0ul; cap < depth; cap++)
        {
            using var owner = new Utf8ConstructionScope(0, ulong.MaxValue, cap);
            var parent = owner.Budget.AllocateObject(17, static value => new ParentValue(value));
            var checkpoint = owner.Budget.ReservedBytes;
            Assert.Equal(Utf8ResourceLimitKind.ConstructionDepth,
                Assert.Throws<Utf8ResourceLimitExceededException>(() => operation()).ResourceKind);
            AssertParent(owner.Budget, checkpoint, parent);
        }
        foreach (var timeout in new[] { false, true })
        for (var target = 1; target <= checkpoints; target++)
        {
            using var owner = Utf8ConstructionScope.Enter();
            var parent = owner.Budget.AllocateObject(17, static value => new ParentValue(value));
            var checkpoint = owner.Budget.ReservedBytes;
            using var cancellation = new CancellationTokenSource();
            long ticks = 0;
            using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
            owner.Budget.Deadline = Utf8ExecutionDeadline.Start(TimeSpan.FromSeconds(30));
            owner.Budget.CancellationToken = cancellation.Token;
            var current = 0;
            owner.Budget.CheckpointHook = () =>
            {
                if (++current != target) return;
                if (timeout) ticks = 30000;
                else cancellation.Cancel();
            };
            var failure = Record.Exception(() => operation());
            if (timeout) Assert.IsType<Utf8ExecutionDeadlineExpiredException>(failure);
            else Assert.Equal(cancellation.Token, Assert.IsType<OperationCanceledException>(failure).CancellationToken);
            AssertParent(owner.Budget, checkpoint, parent);
        }
    }

    private static Func<object> Stage(string stage)
    {
        switch (stage)
        {
            case "parse": return () =>
            {
                using var child = Utf8ConstructionScope.Enter();
                using var storage = Utf8ConstructionSession.Create(child.Budget);
                var parsed = Utf8ReplacementPattern.Parse(storage, "é$$${missing}$0012"u8, [], false);
                storage.Complete();
                child.Complete();
                return parsed;
            };
            case "named": return () => Utf8FrontEndReplacementAnalyzer.Analyze("a${x}$0012z"u8, [0, 1, 7], ["zero", "one", "x"]);
            case "unknown-literal": return () => Utf8FrontEndReplacementAnalyzer.Analyze("a${x}$0012z"u8);
            case "numeric": return () => Utf8FrontEndReplacementAnalyzer.Analyze("a$1z"u8, [0, 1], ["zero", "one"]);
            case "literal-text": return () => Utf8FrontEndReplacementAnalyzer.Analyze("é😀$literalé"u8);
            default: throw new ArgumentOutOfRangeException(nameof(stage));
        }
    }
}
