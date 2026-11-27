using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.FrontEnd;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8ConstructionRollbackTests
{
    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(4)] [InlineData(8)]
    public void NestedParserFramesRejectBeforeExceedingDepthAndRecover(int maximum)
    {
        using var checkpointObservation = global::Lokad.Utf8Regex.Internal.Diagnostics.Utf8SearchDiagnosticsSession.Start();
        ulong recoveryDepth;
        using (var measured = new Utf8ConstructionScope(ulong.MaxValue, ulong.MaxValue, ulong.MaxValue))
        {
            Assert.NotNull(Utf8OwnedTreeParser.TryParse("", RegexOptions.None));
            // The lexical storage owner and every live continuation share depth.
            recoveryDepth = measured.Budget.PeakDepth;
        }
        using var scope = new Utf8ConstructionScope(ulong.MaxValue, ulong.MaxValue, (ulong)maximum);
        var parent = scope.Budget.AllocateArray<byte>(1, false);
        var failure = Assert.Throws<Utf8ResourceLimitExceededException>(() =>
            Utf8OwnedTreeParser.TryParse(new string('(', 32) + "😀" + new string(')', 32), RegexOptions.None));
        Assert.Equal(Utf8ResourceLimitKind.ConstructionDepth, failure.ResourceKind);
        Assert.Equal((ulong)maximum + 1, failure.Requested);
        Assert.Equal((ulong)maximum, scope.Budget.PeakDepth);
        Assert.Equal(0ul, scope.Budget.Depth);
        Assert.Equal(48ul, scope.Budget.ReservedBytes);
        Assert.True(scope.Budget.DiagnosticCheckpoints > 0);
        if ((ulong)maximum >= recoveryDepth) Assert.NotNull(Utf8OwnedTreeParser.TryParse("", RegexOptions.None));
        else
        {
            var recoveryFailure = Assert.Throws<Utf8ResourceLimitExceededException>(() =>
                Utf8OwnedTreeParser.TryParse("", RegexOptions.None));
            Assert.Equal(Utf8ResourceLimitKind.ConstructionDepth, recoveryFailure.ResourceKind);
            Assert.Equal((ulong)maximum + 1, recoveryFailure.Requested);
            Assert.Equal(0ul, scope.Budget.Depth);
            Assert.Equal(48ul, scope.Budget.ReservedBytes);
        }
        scope.Budget.ReleaseArray(parent, false);
    }

    [Fact]
    public void PreparationFrameDenialRestoresOwnedDepthAndStorage()
    {
        var leaf = Utf8TreeNode.Create(Utf8TreeNodeKind.One, RegexOptions.None, 'a');
        var root = Utf8TreeNode.Create(kind: Utf8TreeNodeKind.Group, options: RegexOptions.None,
            ch: '\0', str: null, m: 0, n: 0,
            children: leaf, parent: null, matchCaseInsensitive: (RegexOptions.None & RegexOptions.IgnoreCase) != 0);
        using var scope = new Utf8ConstructionScope(ulong.MaxValue, ulong.MaxValue, 1);
        var failure = Assert.Throws<Utf8ResourceLimitExceededException>(() => Utf8OwnedFindOptimizations.MeasureByteFacts(root, selectFixedLiteral: false).Width);
        Assert.Equal(Utf8ResourceLimitKind.ConstructionDepth, failure.ResourceKind);
        Assert.Equal(0ul, scope.Budget.Depth);
        Assert.Equal(0ul, scope.Budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void CanceledGrowthCopyReleasesOnlyTheNewArray(bool instructions)
    {
        var budget = new Utf8ConstructionBudget(ulong.MaxValue, ulong.MaxValue, ulong.MaxValue);
        using var buffer = new Utf8ConstructionBuffer<int>(budget, instructions);
        buffer.Add(42);
        var held = budget.ReservedBytes;
        using var cancellation = new CancellationTokenSource();
        budget.CancellationToken = cancellation.Token;
        budget.CheckpointHook = () => { if (budget.ReservedBytes > held) cancellation.Cancel(); };
        var failure = Assert.Throws<OperationCanceledException>(() => buffer.Add(43));
        Assert.Equal(cancellation.Token, failure.CancellationToken);
        Assert.Equal(1, buffer.Count);
        Assert.Equal(42, buffer[0]);
        Assert.Equal(Utf8ConstructionBudget.ArrayBytes<int>(1), budget.ReservedBytes);
        Assert.Equal(instructions ? 1ul : 0ul, budget.Reserved);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void CanceledFinalCopyLeavesTheOriginalVectorOwned(bool instructions)
    {
        var budget = new Utf8ConstructionBudget(ulong.MaxValue, ulong.MaxValue, ulong.MaxValue);
        using var buffer = new Utf8ConstructionBuffer<int>(budget, instructions);
        buffer.Add(1); buffer.Add(2); buffer.Add(3);
        var held = budget.ReservedBytes;
        using var cancellation = new CancellationTokenSource();
        budget.CancellationToken = cancellation.Token;
        budget.CheckpointHook = () => { if (budget.ReservedBytes > held) cancellation.Cancel(); };
        var failure = Assert.Throws<OperationCanceledException>(() => buffer.Take());
        Assert.Equal(cancellation.Token, failure.CancellationToken);
        Assert.Equal(3, buffer.Count);
        Assert.Equal(4, buffer.Capacity);
        Assert.Equal(new[] { 1, 2, 3 }, buffer.Span.ToArray());
        Assert.Equal(Utf8ConstructionBudget.ArrayBytes<int>(4), budget.ReservedBytes);
        Assert.Equal(instructions ? 4ul : 0ul, budget.Reserved);
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(127)] [InlineData(1000)]
    public void AdmittedSortMatchesTheReferenceWithoutAcquiringScratch(int length)
    {
        var input = Enumerable.Range(0, length).Select(i => (i * 37 % 101) - 50).Reverse().ToArray();
        var expected = input.ToArray();
        Array.Sort(expected);
        var budget = new Utf8ConstructionBudget(ulong.MaxValue, 0, ulong.MaxValue);
        Utf8ConstructionSort.Sort(input.AsSpan(), static (a,b) => a.CompareTo(b), budget);
        Assert.Equal(expected, input);
        Assert.Equal(0ul, budget.ReservedBytes);
        Assert.Equal(0ul, budget.PeakBytes);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void CorePublicConstructionCannotResetTheParserDepthOwner(bool compiled)
    {
        using (var scope = new Utf8ConstructionScope(ulong.MaxValue, ulong.MaxValue, 8))
        {
            var options = RegexOptions.CultureInvariant | (compiled ? RegexOptions.Compiled : RegexOptions.None);
            var failure = Assert.Throws<Utf8ResourceLimitExceededException>(() =>
                new Utf8Regex(new string('(', 32) + "." + new string(')', 32), options: options));
            Assert.Equal(Utf8ResourceLimitKind.ConstructionDepth, failure.ResourceKind);
            Assert.Equal(8ul, failure.Limit);
            Assert.Equal(0ul, scope.Budget.Depth);
            Assert.Equal(0ul, scope.Budget.ReservedBytes);
            Assert.True(new Utf8Regex(".", options: options).IsMatch("😀"u8));
        }
        Assert.False(Utf8ConstructionScope.IsActive);
    }

}
