using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.FrontEnd;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8ConstructionWorkDepthTests
{
    [Theory]
    [InlineData("a|é")] [InlineData("[A-Z-[AE]]")]
    [InlineData("(?<x>😀)\\k<x>")] [InlineData("(?i:[é😀-🙏])")]
    public void ParserWorkCutoffsPreserveParentCapacityAndDepth(string pattern)
    {
        // The admitted frame-buffer descriptor remains in the enclosing owner;
        // continuation-array scratch is released before capture numbering.
        var frameDescriptor = Utf8ConstructionBudget.ObjectBytes<(Utf8ConstructionBudget, bool, Array, int)>()
            + Utf8ConstructionBudget.ObjectBytes<(object, Utf8ConstructionBudget.ObjectAllocation?, ulong)>();
        ulong work;
        using (var measured = new Utf8ConstructionScope(ulong.MaxValue, ulong.MaxValue, ulong.MaxValue, ulong.MaxValue))
        {
            Assert.NotNull(Utf8OwnedTreeParser.TryParse(pattern, RegexOptions.CultureInvariant));
            work = measured.Budget.WorkSteps;
            Assert.Equal(frameDescriptor, measured.Budget.ReservedBytes);
            Assert.Equal(0ul, measured.Budget.Depth);
        }
        Assert.InRange(work, 1ul, 2_000_000ul);
        // Cover individual cutoffs for small parses and bounded sampled cutoffs
        // for scalar-class expansion; exact measured total must still admit.
        var cutoffs = work < 512 ? Enumerable.Range(0, (int)work + 1).Select(i => (ulong)i)
            : new[] { 0ul, 1ul, work / 4, work / 2, work - 1, work };
        foreach (var cutoff in cutoffs)
        {
            using var scope = new Utf8ConstructionScope(ulong.MaxValue, ulong.MaxValue, cutoff + 1, ulong.MaxValue);
            var parent = scope.Budget.AllocateArray<byte>(1, false);
            scope.Budget.EnterDepth();
            var failure = Record.Exception(() => Utf8OwnedTreeParser.TryParse(pattern, RegexOptions.CultureInvariant));
            Assert.Equal(cutoff < work ? 48ul : 48 + frameDescriptor, scope.Budget.ReservedBytes);
            Assert.Equal(1ul, scope.Budget.Depth);
            Assert.True(scope.Budget.WorkSteps <= cutoff + 1);
            if (cutoff < work)
            {
                var breach = Assert.IsType<Utf8ResourceLimitExceededException>(failure);
                Assert.Equal(Utf8ResourceLimitKind.ConstructionWorkSteps, breach.ResourceKind);
                Assert.Equal(cutoff + 1, breach.Limit);
                Assert.True(breach.Requested > breach.Limit);
            }
            else Assert.Null(failure);
            scope.Budget.LeaveDepth();
            scope.Budget.ReleaseArray(parent, false);
        }
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(4)] [InlineData(8)]
    public void NestedParserFramesRejectBeforeExceedingDepthAndRecover(int maximum)
    {
        using var scope = new Utf8ConstructionScope(ulong.MaxValue, ulong.MaxValue, ulong.MaxValue, (ulong)maximum);
        var parent = scope.Budget.AllocateArray<byte>(1, false);
        var failure = Assert.Throws<Utf8ResourceLimitExceededException>(() =>
            Utf8OwnedTreeParser.TryParse(new string('(', 32) + "😀" + new string(')', 32), RegexOptions.None));
        Assert.Equal(Utf8ResourceLimitKind.ConstructionDepth, failure.ResourceKind);
        Assert.Equal((ulong)maximum + 1, failure.Requested);
        Assert.Equal((ulong)maximum, scope.Budget.PeakDepth);
        Assert.Equal(0ul, scope.Budget.Depth);
        Assert.Equal(48ul, scope.Budget.ReservedBytes);
        Assert.True(scope.Budget.WorkSteps > 0);
        if (maximum >= 2) Assert.NotNull(Utf8OwnedTreeParser.TryParse("", RegexOptions.None));
        scope.Budget.ReleaseArray(parent, false);
    }

    [Fact]
    public void SpeculativeFailureAndNestedScopeCannotRefundSpentWork()
    {
        using var parent = new Utf8ConstructionScope(ulong.MaxValue, ulong.MaxValue, 10000, 8);
        parent.Budget.ChargeWork(3);
        using (var child = new Utf8ConstructionScope(ulong.MaxValue, ulong.MaxValue, ulong.MaxValue, ulong.MaxValue))
        {
            Assert.Same(parent.Budget, child.Budget);
            child.Budget.ChargeWork(4);
            _ = child.Budget.AllocateArray<byte>(1, false);
        }
        Assert.Equal(8ul, parent.Budget.WorkSteps);
        Assert.Equal(0ul, parent.Budget.ReservedBytes);
        Assert.Null(Utf8OwnedTreeParser.TryParse("(", RegexOptions.None));
        Assert.True(parent.Budget.WorkSteps > 8);
        parent.Budget.ChargeWork(10000 - parent.Budget.WorkSteps);
        using var retry = new Utf8ConstructionScope(ulong.MaxValue, ulong.MaxValue, ulong.MaxValue, ulong.MaxValue);
        var failure = Assert.Throws<Utf8ResourceLimitExceededException>(() => retry.Budget.ChargeWork(1));
        Assert.Equal(10000ul, failure.Limit);
        Assert.Equal(10000ul, parent.Budget.WorkSteps);
    }

    [Theory]
    [InlineData(ulong.MaxValue, 1ul)] [InlineData(0ul, ulong.MaxValue)]
    [InlineData(1ul, ulong.MaxValue)] [InlineData(2ul, ulong.MaxValue)]
    public void PreparationFrameDenialRestoresOwnedDepthAndStorageWithoutSpendingCleanupWork(ulong work, ulong depth)
    {
        var leaf = new Utf8TreeNode(Utf8TreeNodeKind.One, RegexOptions.None, 'a');
        var root = new Utf8TreeNode(Utf8TreeNodeKind.Group, RegexOptions.None) { Children = leaf };
        using var scope = new Utf8ConstructionScope(ulong.MaxValue, ulong.MaxValue, work, depth);
        var failure = Assert.Throws<Utf8ResourceLimitExceededException>(() => Utf8OwnedFindOptimizations.ComputeByteWidth(root));
        Assert.Equal(depth == 1 ? Utf8ResourceLimitKind.ConstructionDepth : Utf8ResourceLimitKind.ConstructionWorkSteps, failure.ResourceKind);
        Assert.Equal(0ul, scope.Budget.Depth);
        Assert.Equal(0ul, scope.Budget.ReservedBytes);
        Assert.True(scope.Budget.WorkSteps <= work);
    }

    [Fact]
    public void WorkOverflowReportsSaturationWithoutWrappingTheCounter()
    {
        var budget = new Utf8ConstructionBudget(ulong.MaxValue, ulong.MaxValue, ulong.MaxValue, ulong.MaxValue);
        budget.ChargeWork(ulong.MaxValue);
        var failure = Assert.Throws<Utf8ResourceLimitExceededException>(() => budget.ChargeWork(1));
        Assert.Equal(Utf8ResourceLimitKind.ConstructionWorkSteps, failure.ResourceKind);
        Assert.Equal(ulong.MaxValue, failure.Requested);
        Assert.Equal(ulong.MaxValue, budget.WorkSteps);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void FailedGrowthCopyReleasesOnlyTheNewArray(bool instructions)
    {
        var budget = new Utf8ConstructionBudget(ulong.MaxValue, ulong.MaxValue, 3, ulong.MaxValue);
        using var buffer = new Utf8ConstructionBuffer<int>(budget, instructions);
        buffer.Add(42);
        var failure = Assert.Throws<Utf8ResourceLimitExceededException>(() => buffer.Add(43));
        Assert.Equal(Utf8ResourceLimitKind.ConstructionWorkSteps, failure.ResourceKind);
        Assert.Equal(1, buffer.Count);
        Assert.Equal(42, buffer[0]);
        Assert.Equal(Utf8ConstructionBudget.ArrayBytes<int>(1), budget.ReservedBytes);
        Assert.Equal(instructions ? 1ul : 0ul, budget.Reserved);
        Assert.Equal(3ul, budget.WorkSteps);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void FailedFinalCopyLeavesTheOriginalVectorOwned(bool instructions)
    {
        var budget = new Utf8ConstructionBudget(ulong.MaxValue, ulong.MaxValue, 13, ulong.MaxValue);
        using var buffer = new Utf8ConstructionBuffer<int>(budget, instructions);
        buffer.Add(1); buffer.Add(2); buffer.Add(3);
        var failure = Assert.Throws<Utf8ResourceLimitExceededException>(() => buffer.Take());
        Assert.Equal(Utf8ResourceLimitKind.ConstructionWorkSteps, failure.ResourceKind);
        Assert.Equal(3, buffer.Count);
        Assert.Equal(4, buffer.Capacity);
        Assert.Equal(Utf8ConstructionBudget.ArrayBytes<int>(4), budget.ReservedBytes);
        Assert.Equal(instructions ? 4ul : 0ul, budget.Reserved);
        Assert.Equal(13ul, budget.WorkSteps);
    }

    [Fact]
    public void FailedMapRehashPreservesItsPreviousArrayAndEntryCount()
    {
        var budget = new Utf8ConstructionBudget(ulong.MaxValue, ulong.MaxValue, 15, ulong.MaxValue);
        using var map = new Utf8ConstructionMap<int, int>(budget, EqualityComparer<int>.Default);
        map.Add(0, 10); map.Add(1, 11);
        var held = budget.ReservedBytes;
        var failure = Assert.Throws<Utf8ResourceLimitExceededException>(() => map.Add(2, 12));
        Assert.Equal(Utf8ResourceLimitKind.ConstructionWorkSteps, failure.ResourceKind);
        Assert.Equal(2, map.Count);
        Assert.Equal(held, budget.ReservedBytes);
        Assert.Equal(15ul, budget.WorkSteps);
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(127)] [InlineData(1000)]
    public void AdmittedSortMatchesTheReferenceWithoutAcquiringScratch(int length)
    {
        var input = Enumerable.Range(0, length).Select(i => (i * 37 % 101) - 50).Reverse().ToArray();
        var expected = input.ToArray();
        Array.Sort(expected);
        var budget = new Utf8ConstructionBudget(ulong.MaxValue, 0, ulong.MaxValue, ulong.MaxValue);
        Utf8ConstructionSort.Sort(input.AsSpan(), static (a,b) => a.CompareTo(b), budget);
        Assert.Equal(expected, input);
        Assert.Equal(0ul, budget.ReservedBytes);
        Assert.Equal(0ul, budget.PeakBytes);
    }

    [Theory]
    [InlineData(0)] [InlineData(1)]
    public void SortFailurePropagatesTheTypedResourceBeforeTheDeniedMutation(int maximum)
    {
        var input = new[] { 2, 1 };
        var budget = new Utf8ConstructionBudget(ulong.MaxValue, 0, (ulong)maximum, ulong.MaxValue);
        var failure = Assert.Throws<Utf8ResourceLimitExceededException>(() =>
            Utf8ConstructionSort.Sort(input.AsSpan(), static (a,b) => a.CompareTo(b), budget));
        Assert.Equal(Utf8ResourceLimitKind.ConstructionWorkSteps, failure.ResourceKind);
        Assert.Equal(new[] { 2, 1 }, input);
        Assert.Equal((ulong)maximum, budget.WorkSteps);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void CorePublicConstructionCannotResetTheParserDepthOwner(bool compiled)
    {
        using (var scope = new Utf8ConstructionScope(ulong.MaxValue, ulong.MaxValue, ulong.MaxValue, 8))
        {
            var options = RegexOptions.CultureInvariant | (compiled ? RegexOptions.Compiled : RegexOptions.None);
            var failure = Assert.Throws<Utf8ResourceLimitExceededException>(() =>
                new Utf8Regex(new string('(', 32) + "." + new string(')', 32), options));
            Assert.Equal(Utf8ResourceLimitKind.ConstructionDepth, failure.ResourceKind);
            Assert.Equal(8ul, failure.Limit);
            Assert.Equal(0ul, scope.Budget.Depth);
            Assert.Equal(0ul, scope.Budget.ReservedBytes);
            Assert.True(new Utf8Regex(".", options).IsMatch("😀"u8));
        }
        Assert.False(Utf8ConstructionScope.IsActive);
    }

}
