using System.Globalization;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.FrontEnd;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8ExecutionLoweringAdmissionTests
{
    [Fact]
    public void CanonicalClassPayloadAndPublicMatchingKeepExactAsciiMembership()
    {
        foreach (var (pattern, accepted) in new[]
        {
            ("[a-c-[b]]", "ac"),
            ("[a-z-[b-y-[m]]]", "amz"),
            ("[\\t-\\r ]", "\t\n\v\f\r "),
            ("(?s:.)", new string(Enumerable.Range(0, 128).Select(static value => (char)value).ToArray())),
        })
        {
            var sourceAnalysis = Utf8FrontEnd.Analyze(pattern, RegexOptions.CultureInvariant);
            var tree = Utf8ExecutionTreeLowerer.Lower(sourceAnalysis.SemanticRegex);
            Assert.NotNull(tree);
            var sourceNode = sourceAnalysis.SemanticRegex.RuntimeTree!.Root;
            while (sourceNode.ScalarClass is null && sourceNode.ChildCount == 1) sourceNode = sourceNode.Child(0);
            var source = sourceNode.ScalarClass;
            Assert.NotNull(source);
            var executionNode = tree.Root;
            while (executionNode.Kind is Utf8ExecutionNodeKind.Capture or Utf8ExecutionNodeKind.Group && executionNode.Children.Count == 1)
                executionNode = executionNode.Children[0];
            Assert.Same(source, executionNode.Payload);
            Assert.Equal(string.Empty, executionNode.Text);
            for (var value = 0; value < 128; value++)
            {
                var expected = accepted.Contains((char)value);
                var match = new Utf8Regex(pattern, RegexOptions.CultureInvariant).Match([(byte)value]);
                Assert.Equal(expected, match.Success);
                if (expected) Assert.Equal(1, match.LengthInBytes);
            }
        }
        Assert.Equal(2, new Utf8Regex("(?s:.{1,2})", RegexOptions.CultureInvariant).Match("ab"u8).LengthInBytes);
        Assert.Equal(4, new Utf8Regex("(?s:.{1,2})", RegexOptions.CultureInvariant).Match("😀"u8).LengthInBytes);
        Assert.False(new Utf8Regex("\\p{L}+", RegexOptions.CultureInvariant).IsMatch("\0"u8));
    }

    private sealed class ParentValue(int value) { internal readonly int Value = value; }
    private sealed record Input(Utf8SemanticRegex Semantic);

    private static Input Make(int depth)
    {
        var source = Utf8TreeNode.Create(Utf8TreeNodeKind.One, RegexOptions.None, 'a');
        for (var index = 0; index < depth; index++)
        {
            var parent = Utf8TreeNode.Create(Utf8TreeNodeKind.Capture, RegexOptions.None, 1, -1);
            parent.WithChild(source);
            source = parent;
        }
        var owned = Utf8OwnedRegexTree.Create(source, 2, null, null, null, RegexOptions.None, CultureInfo.InvariantCulture, null, false, false);
        var semantic = new Utf8SemanticRegex("a", RegexOptions.None, "a", RegexOptions.None, owned);
        return new(semantic);
    }

    private static void Run(Input input) => Assert.NotNull(Utf8ExecutionTreeLowerer.Lower(input.Semantic));

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)]
    public void RootAdmissionPrecedesTraversalAndLeavesParentIntact(int denial)
    {
        var input = Make(0);
        using var owner = new Utf8ConstructionScope(ulong.MaxValue,
            denial == 0 ? 128ul : ulong.MaxValue, denial == 2 ? 2ul : ulong.MaxValue,
            denial == 1 ? 0ul : ulong.MaxValue);
        var parent = owner.Budget.AllocateObject(17, static value => new ParentValue(value));
        var checkpoint = owner.Budget.ReservedBytes;
        var failure = Assert.Throws<Utf8ResourceLimitExceededException>(() => Run(input));
        Assert.Equal(denial switch
        {
            0 => Utf8ResourceLimitKind.ConstructionBytes,
            1 => Utf8ResourceLimitKind.ConstructionDepth,
            _ => Utf8ResourceLimitKind.ConstructionWorkSteps,
        }, failure.ResourceKind);
        Assert.Equal(128ul, owner.Budget.ReservedBytes);
        Assert.Equal(128ul, owner.Budget.PeakBytes);
        Assert.Equal(0ul, owner.Budget.Depth);
        Assert.Equal(2ul, owner.Budget.WorkSteps);
        Assert.Equal(checkpoint, owner.Budget.ReservedBytes);
        Assert.Equal(17, parent.Value);
    }

    [Fact]
    public void ChildDepthDenialUnwindsAndAllowsRecovery()
    {
        var input = Make(32);
        using var owner = new Utf8ConstructionScope(ulong.MaxValue, ulong.MaxValue, ulong.MaxValue, 8);
        var parent = owner.Budget.AllocateObject(17, static value => new ParentValue(value));
        var checkpoint = owner.Budget.ReservedBytes;
        var failure = Assert.Throws<Utf8ResourceLimitExceededException>(() => Run(input));
        Assert.Equal(Utf8ResourceLimitKind.ConstructionDepth, failure.ResourceKind);
        Assert.Equal(9ul, failure.Requested);
        Assert.Equal(8ul, owner.Budget.PeakDepth);
        Assert.Equal(128ul, owner.Budget.ReservedBytes);
        Assert.Equal(0ul, owner.Budget.Depth);
        Assert.Equal(checkpoint, owner.Budget.ReservedBytes);
        var work = owner.Budget.WorkSteps;
        using (var recovery = Utf8ConstructionScope.Enter()) Run(Make(0));
        Assert.True(owner.Budget.WorkSteps > work);
        Assert.Equal(128ul, owner.Budget.ReservedBytes);
        Assert.Equal(checkpoint, owner.Budget.ReservedBytes);
        Assert.Equal(17, parent.Value);
    }

    [Fact]
    public void GrowingStorageDenialReleasesAllPayloadAndScratchLoans()
    {
        var input = Make(128);
        using var owner = new Utf8ConstructionScope(ulong.MaxValue, 2048, ulong.MaxValue, ulong.MaxValue);
        var parent = owner.Budget.AllocateObject(17, static value => new ParentValue(value));
        var checkpoint = owner.Budget.ReservedBytes;
        var failure = Assert.Throws<Utf8ResourceLimitExceededException>(() => Run(input));
        Assert.Equal(Utf8ResourceLimitKind.ConstructionBytes, failure.ResourceKind);
        Assert.True(failure.Requested > 2048);
        Assert.InRange(owner.Budget.PeakBytes, 113ul, 2048ul);
        Assert.Equal(128ul, owner.Budget.ReservedBytes);
        Assert.Equal(0ul, owner.Budget.Reserved);
        Assert.Equal(0ul, owner.Budget.Depth);
        Assert.Equal(checkpoint, owner.Budget.ReservedBytes);
        Assert.Equal(17, parent.Value);
    }

    [Fact]
    public void EveryWorkCutoffRestoresParentWithoutRefundingSpentWork()
    {
        var input = Make(3);
        ulong work;
        using (var measure = new Utf8ConstructionScope(ulong.MaxValue, ulong.MaxValue, ulong.MaxValue, ulong.MaxValue))
        {
            Run(input);
            work = measure.Budget.WorkSteps;
            Assert.True(measure.Budget.ReservedBytes > 0);
            Assert.Equal(0ul, measure.Budget.Depth);
        }
        Assert.InRange(work, 1ul, 10000ul);
        for (var cutoff = 0ul; cutoff <= work; cutoff++)
        {
            using var owner = new Utf8ConstructionScope(ulong.MaxValue, ulong.MaxValue, cutoff + 2, ulong.MaxValue);
            var parent = owner.Budget.AllocateObject(17, static value => new ParentValue(value));
            var checkpoint = owner.Budget.ReservedBytes;
            Exception? failure;
            using (var child = Utf8ConstructionScope.Enter()) failure = Record.Exception(() => Run(input));
            if (cutoff == work) Assert.Null(failure);
            else
            {
                var breach = Assert.IsType<Utf8ResourceLimitExceededException>(failure);
                Assert.Equal(Utf8ResourceLimitKind.ConstructionWorkSteps, breach.ResourceKind);
                Assert.True(breach.Requested > cutoff + 2);
            }
            Assert.InRange(owner.Budget.WorkSteps, 2ul, cutoff + 2);
            Assert.Equal(128ul, owner.Budget.ReservedBytes);
            Assert.Equal(0ul, owner.Budget.Reserved);
            Assert.Equal(0ul, owner.Budget.Depth);
            Assert.Equal(checkpoint, owner.Budget.ReservedBytes);
            Assert.Equal(17, parent.Value);
        }
    }

    [Fact]
    public void EveryByteCutoffAdmitsBeforeAcquisitionAndRestoresParent()
    {
        var input = Make(3);
        ulong peak;
        using (var measure = Utf8ConstructionScope.Enter())
        {
            Run(input);
            peak = measure.Budget.PeakBytes;
        }
        Assert.InRange(peak, 1ul, 10000ul);
        for (var allowance = 0ul; allowance <= peak; allowance++)
        {
            using var owner = new Utf8ConstructionScope(ulong.MaxValue, 128 + allowance, ulong.MaxValue, ulong.MaxValue);
            var parent = owner.Budget.AllocateObject(17, static value => new ParentValue(value));
            var checkpoint = owner.Budget.ReservedBytes;
            Exception? failure;
            using (var child = Utf8ConstructionScope.Enter()) failure = Record.Exception(() => Run(input));
            if (allowance == peak) Assert.Null(failure);
            if (failure is not null)
            {
                var breach = Assert.IsType<Utf8ResourceLimitExceededException>(failure);
                Assert.Equal(Utf8ResourceLimitKind.ConstructionBytes, breach.ResourceKind);
                Assert.True(breach.Requested > 128 + allowance);
            }
            Assert.True(owner.Budget.PeakBytes <= 128 + allowance);
            Assert.Equal(128ul, owner.Budget.ReservedBytes);
            Assert.Equal(0ul, owner.Budget.Reserved);
            Assert.Equal(0ul, owner.Budget.Depth);
            Assert.Equal(checkpoint, owner.Budget.ReservedBytes);
            Assert.Equal(17, parent.Value);
        }
    }

    [Fact]
    public void SuccessfulResultsTransferPayloadLoansWhileScratchDepthCloses()
    {
        var input = Make(3);
        using var owner = Utf8ConstructionScope.Enter();
        Run(input);
        Assert.True(owner.Budget.ReservedBytes > 0);
        Assert.Equal(0ul, owner.Budget.Depth);
        Assert.True(owner.Budget.ReservedBytes > 0);
        Assert.Equal(0ul, owner.Budget.Reserved);
    }

    [Fact]
    public void ScalarSharedDeclarationsRetainEveryCaptureAndTheFinalByteRange()
    {
        var regex = new Utf8Regex("(?<x>a)(?<x>😀)", RegexOptions.CultureInvariant);
        var match = regex.MatchDetailed("a😀"u8);
        Assert.True(match.Success);
        var group = match.GetGroup("x");
        Assert.Equal(2, group.CaptureCount);
        Assert.Equal((1, 4), (group.IndexInBytes, group.LengthInBytes));
        Assert.Equal("😀", group.GetValueString());
    }
}
