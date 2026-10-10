using System.Text.RegularExpressions;
using System.Reflection;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.FrontEnd;
using Lokad.Utf8Regex.Internal.Planning;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8CaptureMapAdmissionTests
{
    private sealed class ParentValue(int value) { internal readonly int Value = value; }

    public static IEnumerable<object[]> Shapes()
    {
        yield return [0, "(?<outer>é|😀)"];
        yield return [0, "(?:(?<left>é)|(?<right>😀))"];
        yield return [1, "(?<outer>a(?<inner>😀)b)"];
        yield return [1, "(?<outer>a(?<inner>😀)?b)"];
        yield return [1, "(?<outer>(?<inner>ab|cd)e)"];
        yield return [2, "(?<outer>a(?<inner>é|😀)b)"];
        yield return [2, "(?<outer>a(?<left>é)?(?<right>😀)??b)"];
        yield return [2, "(?<outer>a(?<left>é|😀)(?<right>b|cc)d)"];
        yield return [2, "(?<piece>é|😀){2}"];
    }

    private static object? Create(int producer, Utf8PreparedRegex prepared) => producer switch
    {
        0 => Utf8LiteralFamilyCaptureMap.TryCreate(prepared.ExecutionTree),
        1 => Utf8GroupedLiteralSpanTable.TryCreate(prepared.ExecutionTree, prepared.ScalarProgram!),
        _ => Utf8AlternationBranchSpanMap.TryCreate(prepared.ExecutionTree, prepared.ScalarProgram!),
    };

    [Theory]
    [MemberData(nameof(Shapes))]
    public void CompleteRetainedProjectionIsTrackedByTheConstructionOwner(int producer, string pattern)
    {
        var prepared = Utf8FrontEnd.Compile(pattern, RegexOptions.CultureInvariant);
        using var owner = Utf8ConstructionScope.Enter();
        var result = Create(producer, prepared);
        Assert.NotNull(result);
        var tracked = false;
        for (var loan = owner.Budget.ObjectCheckpoint; loan is not null; loan = loan.Previous)
            tracked |= ReferenceEquals(loan.Value, result);
        Assert.True(tracked, "A returned projection remains live and must keep its complete object-state loan.");
        Assert.True(owner.Budget.ReservedBytes > 0);
        Assert.Equal(0ul, owner.Budget.Depth);
        // All descriptor loans stay rooted until the owner closes. Every other
        // remaining byte must belong to a returned array, including outer tables.
        var descriptorBytes = 0ul;
        for (var loan = owner.Budget.ObjectCheckpoint; loan is not null; loan = loan.Previous)
        {
            descriptorBytes += loan.Bytes;
            if (loan.Value.GetType().FullName?.StartsWith("Lokad.Utf8Regex.Internal.Execution.", StringComparison.Ordinal) != true)
                continue;
            var stateBytes = 0ul;
            foreach (var field in loan.Value.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
                stateBytes += field.FieldType == typeof(int) ? 4ul : field.FieldType == typeof(bool) ? 1ul : 8ul;
            Assert.True(loan.Bytes >= 32 + ((stateBytes + 15) & ~15ul) + 64,
                $"Incomplete descriptor extent for {loan.Value.GetType().Name}.");
        }
        var arrays = new HashSet<Array>(ReferenceEqualityComparer.Instance);
        var arrayBytes = 0ul;
        foreach (var field in result!.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            if (field.GetValue(result) is Array array) CountArrays(array);
        Assert.Equal(descriptorBytes + arrayBytes, owner.Budget.ReservedBytes);
        Assert.True(owner.Budget.PeakBytes > owner.Budget.ReservedBytes);

        void CountArrays(Array array)
        {
            if (!arrays.Add(array)) return;
            arrayBytes += array switch
            {
                int[] values => Utf8ConstructionBudget.ArrayBytes<int>(values.Length),
                int[][] values => Utf8ConstructionBudget.ArrayBytes<int[]>(values.Length),
                string[] values => Utf8ConstructionBudget.ArrayBytes<string>(values.Length),
                _ => throw new InvalidOperationException(),
            };
            if (array is int[][] branches) foreach (var branch in branches) CountArrays(branch);
        }
    }

    [Theory]
    [MemberData(nameof(Shapes))]
    public void EveryWorkCutoffRestoresTheParentWithoutRefundingWork(int producer, string pattern)
    {
        var prepared = Utf8FrontEnd.Compile(pattern, RegexOptions.CultureInvariant);
        ulong work;
        using (var measured = Utf8ConstructionScope.Enter())
        {
            Assert.NotNull(Create(producer, prepared));
            work = measured.Budget.WorkSteps;
        }
        for (var cutoff = 0ul; cutoff <= work; cutoff++)
        {
            using var owner = new Utf8ConstructionScope(0, ulong.MaxValue, cutoff + 2, ulong.MaxValue);
            var parent = owner.Budget.AllocateObject(17, static value => new ParentValue(value));
            var checkpoint = owner.Budget.ObjectCheckpoint;
            using (var discarded = Utf8ConstructionScope.Enter())
            {
                var failure = Record.Exception(() => Assert.NotNull(Create(producer, prepared)));
                if (cutoff < work)
                {
                    var breach = Assert.IsType<Utf8ResourceLimitExceededException>(failure);
                    Assert.Equal(Utf8ResourceLimitKind.ConstructionWorkSteps, breach.ResourceKind);
                    Assert.True(breach.Requested > owner.Budget.MaximumWorkSteps);
                }
                else Assert.Null(failure);
            }
            AssertParent(owner.Budget, checkpoint, parent);
            Assert.InRange(owner.Budget.WorkSteps, 2ul, cutoff + 2);
        }
    }

    [Theory]
    [MemberData(nameof(Shapes))]
    public void CapacityDenialsRestoreParentAndAllowRecovery(int producer, string pattern)
    {
        var prepared = Utf8FrontEnd.Compile(pattern, RegexOptions.CultureInvariant);
        var recoveryInput = Utf8FrontEnd.Compile("(a)", RegexOptions.CultureInvariant);
        ulong recoveryPeak;
        using (var measured = Utf8ConstructionScope.Enter())
        {
            measured.Budget.AllocateObject(17, static value => new ParentValue(value));
            Assert.NotNull(Utf8LiteralFamilyCaptureMap.TryCreate(recoveryInput.ExecutionTree));
            recoveryPeak = measured.Budget.PeakBytes;
        }
        ulong peak;
        using (var measured = Utf8ConstructionScope.Enter())
        {
            measured.Budget.AllocateObject(17, static value => new ParentValue(value));
            Assert.NotNull(Create(producer, prepared));
            peak = measured.Budget.PeakBytes;
        }
        foreach (var cap in new[] { 112ul, 256ul, 1024ul, peak - 16, peak - 1, peak }.Distinct())
        {
            using var owner = new Utf8ConstructionScope(0, cap, ulong.MaxValue, ulong.MaxValue);
            var parent = owner.Budget.AllocateObject(17, static value => new ParentValue(value));
            var checkpoint = owner.Budget.ObjectCheckpoint;
            using (var discarded = Utf8ConstructionScope.Enter())
            {
                var failure = Record.Exception(() => Assert.NotNull(Create(producer, prepared)));
                if (failure is not null)
                {
                    var breach = Assert.IsType<Utf8ResourceLimitExceededException>(failure);
                    Assert.Equal(Utf8ResourceLimitKind.ConstructionBytes, breach.ResourceKind);
                    Assert.Equal(cap, breach.Limit);
                    Assert.True(breach.Requested > cap);
                }
                else Assert.True(cap > 1024);
                if (cap == peak) Assert.Null(failure);
            }
            AssertParent(owner.Budget, checkpoint, parent);
            Assert.True(owner.Budget.PeakBytes <= cap);
            if (cap >= recoveryPeak)
            {
                var spent = owner.Budget.WorkSteps;
                using (var recovered = Utf8ConstructionScope.Enter())
                    Assert.NotNull(Utf8LiteralFamilyCaptureMap.TryCreate(recoveryInput.ExecutionTree));
                Assert.True(owner.Budget.WorkSteps > spent);
                AssertParent(owner.Budget, checkpoint, parent);
            }
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
    public void EveryCheckpointRetainsParentCancellationAndDeadline(int producer, string pattern, bool timeout)
    {
        var prepared = Utf8FrontEnd.Compile(pattern, RegexOptions.CultureInvariant);
        var checkpoints = 0;
        using (var measured = Utf8ConstructionScope.Enter())
        {
            measured.Budget.CheckpointHook = _ => checkpoints++;
            Assert.NotNull(Create(producer, prepared));
        }
        Assert.True(checkpoints > 0);
        for (var target = 1; target <= checkpoints; target++)
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
            var failure = Record.Exception(() => Create(producer, prepared));
            if (timeout) Assert.IsType<Utf8ExecutionDeadlineExpiredException>(failure);
            else Assert.Equal(cancellation.Token, Assert.IsType<OperationCanceledException>(failure).CancellationToken);
            Assert.Same(deadline, owner.Budget.Deadline);
            AssertParent(owner.Budget, checkpoint, parent);
            owner.Budget.CheckpointHook = null;
            owner.Budget.CancellationToken = default;
            owner.Budget.Deadline = Utf8ExecutionDeadline.Infinite;
            using (var recovered = Utf8ConstructionScope.Enter()) Assert.NotNull(Create(producer, prepared));
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

    [Theory]
    [InlineData(0, "(?<outer>a+)")]
    [InlineData(1, "(?<outer>a(?<inner>b|cd))")]
    [InlineData(2, "(?<outer>a(b)?(c)?(d)?(e)?(f)?(g)?(h)?z)")]
    [InlineData(2, "(?<outer>(?:ab|c)(?:ab|c)(?:ab|c)(?:ab|c)(?:ab|c)(?:ab|c)(?:ab|c))")]
    [InlineData(2, "(?<piece>ab|c){7}")]
    public void UnsupportedShapesReleaseScratchAndRetainSpentWork(int producer, string pattern)
    {
        var prepared = Utf8FrontEnd.Compile(pattern, RegexOptions.CultureInvariant);
        var recoveryInput = Utf8FrontEnd.Compile("(a)", RegexOptions.CultureInvariant);
        using var owner = Utf8ConstructionScope.Enter();
        var parent = owner.Budget.AllocateObject(17, static value => new ParentValue(value));
        var checkpoint = owner.Budget.ObjectCheckpoint;
        Assert.Null(Create(producer, prepared));
        Assert.True(owner.Budget.WorkSteps > 2);
        AssertParent(owner.Budget, checkpoint, parent);
        var spent = owner.Budget.WorkSteps;
        using (var recovered = Utf8ConstructionScope.Enter())
            Assert.NotNull(Utf8LiteralFamilyCaptureMap.TryCreate(recoveryInput.ExecutionTree));
        Assert.True(owner.Budget.WorkSteps > spent);
        AssertParent(owner.Budget, checkpoint, parent);
    }

    [Fact]
    public void OptionalScalarExpansionRetainsRightmostUnwindingAndSourcePreference()
    {
        var prepared = Utf8FrontEnd.Compile("(?<whole>a(?<x>é)?(?<y>😀)??(?<z>b)?c)", RegexOptions.CultureInvariant);
        using var owner = Utf8ConstructionScope.Enter();
        var result = Utf8AlternationBranchSpanMap.TryCreate(prepared.ExecutionTree, prepared.ScalarProgram!);
        Assert.NotNull(result);
        Assert.Equal(new[] { "aébc", "aéc", "aé😀bc", "aé😀c", "abc", "ac", "a😀bc", "a😀c" }, result.BranchExpansions);
        Assert.Equal(new[] { 1 }, result.WholeMatchGroups);
        Assert.Equal(new[] { 2, 4 }, result.BranchGroups[0]);
        Assert.Equal(new[] { 2, 3, 4 }, result.BranchGroups[2]);
        Assert.Equal(new[] { 1, 2, 4 }, result.BranchStarts[2]);
        Assert.Equal(new[] { 1, 2, 1 }, result.BranchLengths[2]);
    }
}
