using System.Reflection;
using System.Runtime.CompilerServices;
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
        yield return ["\ud83d$1\ude00", false];
    }

    private static Utf8AnalyzedReplacement Create(string template, bool ecma) =>
        Utf8FrontEndReplacementAnalyzer.Analyze(template, [0, 1, 7, 12], ["zero", "one", "seven", "twelve"], ecma);

    [Fact]
    public void PublishedReplacementPatternAndPlanKeepTheirConstructionLoans()
    {
        using var owner = Utf8ConstructionScope.Enter();
        var result = Utf8FrontEndReplacementAnalyzer.Analyze("a${missing}$1$1z", [0, 1], ["zero", "one"]);
        Assert.NotEmpty(result.Pattern.Tokens);
        Assert.NotEmpty(result.Plan.Instructions);
        foreach (var value in new object[] { result.Pattern, result.Plan })
        {
            var tracked = false;
            for (var loan = owner.Budget.ObjectCheckpoint; loan is not null; loan = loan.Previous)
                tracked |= ReferenceEquals(loan.Value, value);
            Assert.True(tracked, "Every published replacement holder requires its complete live construction state.");
        }
        var descriptors = 0ul;
        for (var loan = owner.Budget.ObjectCheckpoint; loan is not null; loan = loan.Previous)
            descriptors += loan.Bytes;
        Assert.True(owner.Budget.ReservedBytes > descriptors,
            "Published replacement token, instruction, literal and capture-reference arrays keep their capacity loans.");
    }

    [Theory]
    [MemberData(nameof(Shapes))]
    public void CompleteReturnedGraphKeepsExactlyItsArrayAndDescriptorLoans(string template, bool ecma)
    {
        using var owner = Utf8ConstructionScope.Enter();
        var result = Create(template, ecma);
        var descriptors = 0ul;
        for (var loan = owner.Budget.ObjectCheckpoint; loan is not null; loan = loan.Previous)
        {
            descriptors += loan.Bytes;
            if (loan.Value is string text)
            {
                Assert.Equal(Utf8ConstructionBudget.StringBytes(text.Length) + 64, loan.Bytes);
                continue;
            }
            var fields = loan.Value.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            var extent = fields.Aggregate(0ul, (size, field) => size + Size(field.FieldType));
            Assert.True(loan.Bytes >= 32 + ((extent + 15) & ~15ul) + 64,
                $"Complete instance extent for {loan.Value.GetType()} must remain admitted.");
        }
        var visited = new HashSet<object>(ReferenceEqualityComparer.Instance);
        var bytes = 0ul;
        Visit(result);
        Assert.Equal(descriptors + bytes, owner.Budget.ReservedBytes);
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

    private static ulong Size(Type type) => type.IsValueType
        ? (ulong)(typeof(Utf8ReplacementPreparationAdmissionTests).GetMethod(nameof(ValueSize), BindingFlags.Static | BindingFlags.NonPublic)
            ?.MakeGenericMethod(type).Invoke(null, null) ?? throw new InvalidOperationException()) : 8ul;
    private static ulong ValueSize<T>() => (ulong)Unsafe.SizeOf<T>();

    [Theory]
    [MemberData(nameof(Shapes))]
    public void EveryWorkCutoffRestoresParentAndNeverRefundsSpentWork(string template, bool ecma)
    {
        ulong work;
        using (var measured = Utf8ConstructionScope.Enter())
        {
            Create(template, ecma);
            work = measured.Budget.WorkSteps;
        }
        for (var cutoff = 0ul; cutoff <= work; cutoff++)
        {
            using var owner = new Utf8ConstructionScope(0, ulong.MaxValue, cutoff + 2, ulong.MaxValue);
            var parent = owner.Budget.AllocateObject(17, static value => new ParentValue(value));
            var checkpoint = owner.Budget.ObjectCheckpoint;
            using (var discarded = Utf8ConstructionScope.Enter())
            {
                var failure = Record.Exception(() => Create(template, ecma));
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
    public void CapacityAndDepthCutoffsRecoverWithoutLosingParentLoans(string template, bool ecma)
    {
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
        foreach (var cap in new[] { 112ul, 224ul, 512ul, 1024ul, peak / 2, peak - 1, peak }.Distinct().Order())
        {
            using var owner = new Utf8ConstructionScope(0, cap, ulong.MaxValue, ulong.MaxValue);
            var parent = owner.Budget.AllocateObject(17, static value => new ParentValue(value));
            var checkpoint = owner.Budget.ObjectCheckpoint;
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
                var spent = owner.Budget.WorkSteps;
                using (var discarded = Utf8ConstructionScope.Enter()) Create("a", false);
                Assert.True(owner.Budget.WorkSteps > spent);
                AssertParent(owner.Budget, checkpoint, parent);
            }
        }
        for (var cap = 0ul; cap < depth; cap++)
        {
            using var owner = new Utf8ConstructionScope(0, ulong.MaxValue, ulong.MaxValue, cap);
            var parent = owner.Budget.AllocateObject(17, static value => new ParentValue(value));
            var checkpoint = owner.Budget.ObjectCheckpoint;
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
            measured.Budget.CheckpointHook = _ => count++;
            Create(template, ecma);
        }
        Assert.True(count > 0);
        for (var target = 1; target <= count; target++)
        {
            using var owner = Utf8ConstructionScope.Enter();
            var parent = owner.Budget.AllocateObject(17, static value => new ParentValue(value));
            var checkpoint = owner.Budget.ObjectCheckpoint;
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
        Utf8ConstructionBudget.ObjectAllocation? checkpoint, ParentValue parent)
    {
        Assert.Equal(112ul, budget.ReservedBytes);
        Assert.Equal(0ul, budget.Reserved);
        Assert.Equal(0ul, budget.Depth);
        Assert.Same(checkpoint, budget.ObjectCheckpoint);
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
        Assert.Single(result.Plan.Instructions);
        Assert.Same(result.Plan.Instructions[0].LiteralUtf8, result.LiteralUtf8);
        Assert.Equal("${missing}$00123", System.Text.Encoding.UTF8.GetString(result.LiteralUtf8));
        var referenced = Create("$7$1$7$12$0", false).Plan.ReferencedCaptureGroups.ToArray();
        Assert.Equal(new[] { 7, 1, 12 }, referenced);
    }

    [Theory]
    [InlineData("parse")]
    [InlineData("resolve-changed")]
    [InlineData("resolve-borrowed")]
    [InlineData("literal-extract")]
    [InlineData("lower-named")]
    [InlineData("literal-text")]
    [InlineData("literal-bytes")]
    [InlineData("rewrite-simple")]
    [InlineData("rewrite-tree")]
    [InlineData("rewrite-borrowed")]
    public void StandaloneFactoriesKeepEveryWorkCutoffAndInterruptionInTheParentOwner(string stage)
    {
        var operation = Stage(stage);
        ulong work, peak, depth;
        var checkpoints = 0;
        using (var measured = Utf8ConstructionScope.Enter())
        {
            measured.Budget.AllocateObject(17, static value => new ParentValue(value));
            measured.Budget.CheckpointHook = _ => checkpoints++;
            operation();
            work = measured.Budget.WorkSteps - 2;
            peak = measured.Budget.PeakBytes;
            depth = measured.Budget.PeakDepth;
        }
        Assert.True(work > 0);
        for (var cap = 0ul; cap <= work; cap++)
        {
            using var owner = new Utf8ConstructionScope(0, ulong.MaxValue, cap + 2, ulong.MaxValue);
            var parent = owner.Budget.AllocateObject(17, static value => new ParentValue(value));
            var checkpoint = owner.Budget.ObjectCheckpoint;
            using (var discarded = Utf8ConstructionScope.Enter())
            {
                var failure = Record.Exception(() => operation());
                if (cap < work)
                    Assert.Equal(Utf8ResourceLimitKind.ConstructionWorkSteps,
                        Assert.IsType<Utf8ResourceLimitExceededException>(failure).ResourceKind);
                else Assert.Null(failure);
            }
            AssertParent(owner.Budget, checkpoint, parent);
            Assert.InRange(owner.Budget.WorkSteps, 2ul, cap + 2);
        }
        foreach (var cap in new[] { 112ul, 224ul, 512ul, peak / 2, peak - 1, peak }.Distinct().Order())
        {
            using var owner = new Utf8ConstructionScope(0, cap, ulong.MaxValue, ulong.MaxValue);
            var parent = owner.Budget.AllocateObject(17, static value => new ParentValue(value));
            var checkpoint = owner.Budget.ObjectCheckpoint;
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
            using var owner = new Utf8ConstructionScope(0, ulong.MaxValue, ulong.MaxValue, cap);
            var parent = owner.Budget.AllocateObject(17, static value => new ParentValue(value));
            var checkpoint = owner.Budget.ObjectCheckpoint;
            Assert.Equal(Utf8ResourceLimitKind.ConstructionDepth,
                Assert.Throws<Utf8ResourceLimitExceededException>(() => operation()).ResourceKind);
            AssertParent(owner.Budget, checkpoint, parent);
        }
        foreach (var timeout in new[] { false, true })
        for (var target = 1; target <= checkpoints; target++)
        {
            using var owner = Utf8ConstructionScope.Enter();
            var parent = owner.Budget.AllocateObject(17, static value => new ParentValue(value));
            var checkpoint = owner.Budget.ObjectCheckpoint;
            using var cancellation = new CancellationTokenSource();
            long ticks = 0;
            using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
            owner.Budget.Deadline = Utf8ExecutionDeadline.Start(TimeSpan.FromSeconds(30));
            owner.Budget.CancellationToken = cancellation.Token;
            var current = 0;
            owner.Budget.CheckpointHook = _ =>
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
        var pattern = Utf8ReplacementPattern.Parse(stage == "resolve-borrowed" ? "a$1z" : "a${x}$0012z");
        switch (stage)
        {
            case "parse": return () => Utf8ReplacementPattern.Parse("é$$${missing}$0012");
            case "resolve-changed": return () => pattern.ResolveGroupReferences([0, 1, 7], ["zero", "one", "x"]);
            case "resolve-borrowed": return () => pattern.ResolveGroupReferences([0, 1], ["zero", "one"]);
            case "literal-extract": return () =>
            {
                Assert.True(pattern.TryGetLiteralText([], [], out var value)); return value;
            };
            case "lower-named": return () => Utf8ReplacementPlanLowerer.Lower(pattern, [0, 1, 7], ["zero", "one", "x"]);
            case "literal-text": return () => Utf8ReplacementInstruction.Literal("é\ud83d$literal\ude00");
            case "literal-bytes":
                byte[] bytes = [0x61, 0xff, 0xf0, 0x9f, 0x98, 0x80];
                return () => Utf8ReplacementInstruction.Literal(bytes);
            default:
                var regex = new Utf8Regex(stage == "rewrite-simple" ? "(abc)" : stage == "rewrite-tree" ? "((?:ab|cd))" : "(a+)b");
                var plan = Utf8FrontEndReplacementAnalyzer.Analyze("x$1$1z", [0, 1], ["zero", "one"]).Plan;
                var method = typeof(Utf8Regex).GetMethod("RewriteWholeLiteralCapturePlan", BindingFlags.Instance | BindingFlags.NonPublic)
                    ?? throw new InvalidOperationException();
                return () =>
                {
                    try { return method.Invoke(regex, [plan]) ?? throw new InvalidOperationException(); }
                    catch (TargetInvocationException failure) when (failure.InnerException is not null)
                    { System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure.InnerException).Throw(); throw; }
                };
        }
    }
}
