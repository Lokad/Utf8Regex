using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.FrontEnd;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8PreparationFrameAdmissionTests
{
    private static readonly Func<Utf8ExecutionNode, bool> ContainsCapture =
        (typeof(Utf8Regex).GetMethod("ContainsCapture", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)
        ?? throw new InvalidOperationException()).CreateDelegate<Func<Utf8ExecutionNode, bool>>();
    private static readonly Utf8SliceProgram CaptureProgram;
    static Utf8PreparationFrameAdmissionTests() => CaptureProgram = Utf8FrontEnd.Compile("(a)", RegexOptions.None).ScalarProgram!;
    private sealed class ParentValue(int value) { internal readonly int Value = value; }
    private static Utf8ExecutionNode Node(Utf8ExecutionNodeKind kind, Utf8ExecutionNode[]? children = null,
        int capture = 0, string text = "", char ch = 'a', int minimum = 0, int maximum = 1) =>
        new(kind, RegexOptions.None, children ?? [], text, ch, capture, minimum, maximum);

    private static Utf8ExecutionTree Tree(int route, int depth = 0)
    {
        var a = Node(Utf8ExecutionNodeKind.One);
        var b = Node(Utf8ExecutionNodeKind.Multi, text: "bb");
        // Put transparent wrappers inside an actually visited branch/loop body;
        // table admission intentionally unwraps groups surrounding the whole shape.
        for (var index = 0; index < depth; index++) a = Node(Utf8ExecutionNodeKind.Group, [a]);
        var body = route switch
        {
            4 => Node(Utf8ExecutionNodeKind.Alternate, [a, b]),
            5 => Node(Utf8ExecutionNodeKind.Concatenate, [Node(Utf8ExecutionNodeKind.Loop, [a]), Node(Utf8ExecutionNodeKind.LazyLoop, [b])]),
            6 => Node(Utf8ExecutionNodeKind.Concatenate, [Node(Utf8ExecutionNodeKind.Alternate, [a, b]), Node(Utf8ExecutionNodeKind.Alternate, [a, b])]),
            _ => a,
        };
        return new(Node(Utf8ExecutionNodeKind.Capture, [route == 7 ? body : Node(Utf8ExecutionNodeKind.Capture, [body], capture: 1)]));
    }

    private static Utf8TreeNode FindTree(int depth = 0)
    {
        var node = Utf8TreeNode.Create(Utf8TreeNodeKind.One, RegexOptions.None, 'a');
        for (var index = 0; index < depth; index++) node = Utf8TreeNode.Create(kind: Utf8TreeNodeKind.Group, options: RegexOptions.None,
            ch: '\0', str: null, m: 0, n: 0,
            children: node, parent: null, matchCaseInsensitive: (RegexOptions.None & RegexOptions.IgnoreCase) != 0);
        return node;
    }

    private static object? Prepare(int route, Utf8TreeNode find, Utf8ExecutionTree execution) => route switch
    {
        0 => Utf8OwnedFindOptimizations.ComputeByteWidth(find).Minimum,
        1 => Utf8OwnedFindOptimizations.ComputeByteWidth(find).Maximum,
        2 => Utf8OwnedFindOptimizations.TryExtractLongestLiteral(find),
        3 => Utf8GroupedLiteralSpanTable.TryCreate(execution, CaptureProgram),
        7 => ContainsCapture(execution.Root),
        _ => Utf8AlternationBranchSpanMap.TryCreate(execution, CaptureProgram),
    };

    public static IEnumerable<object[]> RootDenials()
    {
        for (var route = 0; route < 8; route++)
        for (var denial = 0; denial < 3; denial++) yield return [route, denial];
    }

    [Theory]
    [MemberData(nameof(RootDenials))]
    public void RootDenialRestoresTheEnclosingOwner(int route, int denial)
    {
        var find = FindTree();
        var execution = Tree(route);
        using var owner = new Utf8ConstructionScope(ulong.MaxValue, denial == 0 ? 112ul : ulong.MaxValue,
            denial == 2 ? 2ul : ulong.MaxValue, denial == 1 ? 0ul : ulong.MaxValue);
        var parent = owner.Budget.AllocateObject(17, static value => new ParentValue(value));
        var checkpoint = owner.Budget.ObjectCheckpoint;
        var failure = Assert.Throws<Utf8ResourceLimitExceededException>(() => Prepare(route, find, execution));
        Assert.Equal(denial switch { 0 => Utf8ResourceLimitKind.ConstructionBytes, 1 => Utf8ResourceLimitKind.ConstructionDepth,
            _ => Utf8ResourceLimitKind.ConstructionWorkSteps }, failure.ResourceKind);
        Assert.Equal(112ul, owner.Budget.ReservedBytes);
        Assert.Equal(112ul, owner.Budget.PeakBytes);
        Assert.Equal(0ul, owner.Budget.Depth);
        Assert.Same(checkpoint, owner.Budget.ObjectCheckpoint);
        Assert.Equal(17, parent.Value);
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)] [InlineData(5)] [InlineData(6)] [InlineData(7)]
    public void DeepChildDenialRestoresParentAndAllowsSameOwnerRecovery(int route)
    {
        var find = FindTree(32);
        var execution = Tree(route, 32);
        using var owner = new Utf8ConstructionScope(ulong.MaxValue, ulong.MaxValue, ulong.MaxValue, 8);
        var parent = owner.Budget.AllocateObject(17, static value => new ParentValue(value));
        var checkpoint = owner.Budget.ObjectCheckpoint;
        var failure = Assert.Throws<Utf8ResourceLimitExceededException>(() => Prepare(route, find, execution));
        Assert.Equal(Utf8ResourceLimitKind.ConstructionDepth, failure.ResourceKind);
        Assert.Equal(9ul, failure.Requested);
        Assert.Equal(8ul, owner.Budget.PeakDepth);
        Assert.Equal(112ul, owner.Budget.ReservedBytes);
        Assert.Equal(0ul, owner.Budget.Depth);
        Assert.Same(checkpoint, owner.Budget.ObjectCheckpoint);
        var spent = owner.Budget.WorkSteps;
        using (var recovery = Utf8ConstructionScope.Enter())
            Assert.NotNull(Prepare(route, FindTree(), Tree(route)));
        Assert.True(owner.Budget.WorkSteps > spent);
        Assert.Equal(112ul, owner.Budget.ReservedBytes);
        Assert.Same(checkpoint, owner.Budget.ObjectCheckpoint);
        Assert.Equal(17, parent.Value);
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)] [InlineData(5)] [InlineData(6)] [InlineData(7)]
    public void EveryWorkCutoffRestoresBorrowedPreparation(int route)
    {
        var find = FindTree(3);
        var execution = Tree(route, 3);
        ulong work;
        using (var measure = new Utf8ConstructionScope(ulong.MaxValue, ulong.MaxValue, ulong.MaxValue, ulong.MaxValue))
        {
            Assert.NotNull(Prepare(route, find, execution));
            work = measure.Budget.WorkSteps;
        }
        Assert.InRange(work, 1ul, 10000ul);
        for (var cutoff = 0ul; cutoff <= work; cutoff++)
        {
            using var owner = new Utf8ConstructionScope(ulong.MaxValue, ulong.MaxValue, cutoff + 2, ulong.MaxValue);
            var parent = owner.Budget.AllocateObject(17, static value => new ParentValue(value));
            var checkpoint = owner.Budget.ObjectCheckpoint;
            using (var speculative = Utf8ConstructionScope.Enter())
            {
                var failure = Record.Exception(() => Prepare(route, find, execution));
                if (cutoff < work)
                {
                    var breach = Assert.IsType<Utf8ResourceLimitExceededException>(failure);
                    Assert.Equal(Utf8ResourceLimitKind.ConstructionWorkSteps, breach.ResourceKind);
                    Assert.Equal(cutoff + 2, breach.Limit);
                    Assert.True(breach.Requested > breach.Limit);
                    Assert.Equal(112ul, owner.Budget.ReservedBytes);
                    Assert.Same(checkpoint, owner.Budget.ObjectCheckpoint);
                }
                else Assert.Null(failure);
                Assert.Equal(0ul, owner.Budget.Depth);
                Assert.True(owner.Budget.WorkSteps <= cutoff + 2);
            }
            Assert.Equal(112ul, owner.Budget.ReservedBytes);
            Assert.Same(checkpoint, owner.Budget.ObjectCheckpoint);
            Assert.Equal(17, parent.Value);
        }
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)]
    public void DeclinedScratchAndLateEligibilityReleaseCompletedLoans(int shape)
    {
        var a = Node(Utf8ExecutionNodeKind.One);
        var captured = Node(Utf8ExecutionNodeKind.Capture, [a], capture: 1);
        var body = shape switch
        {
            0 => Node(Utf8ExecutionNodeKind.Alternate, [captured, Node(Utf8ExecutionNodeKind.Capture,
                [Node(Utf8ExecutionNodeKind.Multi, text: "bb")], capture: 1)]),
            1 => Node(Utf8ExecutionNodeKind.Concatenate, [captured, Node(Utf8ExecutionNodeKind.Capture, [a], capture: 1)]),
            _ => Node(Utf8ExecutionNodeKind.Multi, text: ""),
        };
        var execution = new Utf8ExecutionTree(Node(Utf8ExecutionNodeKind.Capture, [body]));
        using var owner = new Utf8ConstructionScope(ulong.MaxValue, ulong.MaxValue, ulong.MaxValue, ulong.MaxValue);
        var parent = owner.Budget.AllocateObject(17, static value => new ParentValue(value));
        var checkpoint = owner.Budget.ObjectCheckpoint;
        Assert.Null(Utf8GroupedLiteralSpanTable.TryCreate(execution, CaptureProgram));
        Assert.Equal(112ul, owner.Budget.ReservedBytes);
        Assert.Same(checkpoint, owner.Budget.ObjectCheckpoint);
        Assert.Equal(0ul, owner.Budget.Depth);
        Assert.True(owner.Budget.WorkSteps > 2);
        using (var recovery = Utf8ConstructionScope.Enter()) Assert.NotNull(Utf8GroupedLiteralSpanTable.TryCreate(Tree(3), CaptureProgram));
        Assert.Equal(112ul, owner.Budget.ReservedBytes);
        Assert.Equal(17, parent.Value);
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)] [InlineData(5)] [InlineData(6)] [InlineData(7)]
    public void SuccessfulPreparationKeepsRetainedPayloadLoansUntilOwnerCloses(int route)
    {
        var find = FindTree(3);
        var execution = Tree(route, 3);
        using var owner = new Utf8ConstructionScope(ulong.MaxValue, ulong.MaxValue, ulong.MaxValue, ulong.MaxValue);
        var parent = owner.Budget.AllocateObject(17, static value => new ParentValue(value));
        var checkpoint = owner.Budget.ObjectCheckpoint;
        using (var child = Utf8ConstructionScope.Enter())
        {
            var result = Prepare(route, find, execution);
            Assert.NotNull(result);
            Assert.True(owner.Budget.PeakBytes > 112);
            if (route == 2)
            {
                Assert.Equal("a", Assert.IsType<string>(result));
                Assert.True(owner.Budget.ReservedBytes > 112);
                Assert.NotSame(checkpoint, owner.Budget.ObjectCheckpoint);
            }
            else if (route is >= 3 and <= 6)
            {
                Assert.True(owner.Budget.ReservedBytes > 112);
                Assert.NotSame(checkpoint, owner.Budget.ObjectCheckpoint);
                var rooted = false;
                for (var loan = owner.Budget.ObjectCheckpoint; loan is not null; loan = loan.Previous)
                    rooted |= ReferenceEquals(loan.Value, result);
                Assert.True(rooted);
            }
            else
            {
                Assert.Equal(112ul, owner.Budget.ReservedBytes);
                Assert.Same(checkpoint, owner.Budget.ObjectCheckpoint);
            }
            Assert.Equal(0ul, owner.Budget.Depth);
        }
        Assert.Equal(112ul, owner.Budget.ReservedBytes);
        Assert.Equal(0ul, owner.Budget.Depth);
        Assert.Same(checkpoint, owner.Budget.ObjectCheckpoint);
        Assert.Equal(17, parent.Value);
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)] [InlineData(5)] [InlineData(6)] [InlineData(7)]
    public void ChildArrayGrowthDenialRestoresParentBytes(int route)
    {
        var find = FindTree(128);
        var execution = Tree(route, 128);
        using var owner = new Utf8ConstructionScope(ulong.MaxValue, 2048, ulong.MaxValue, ulong.MaxValue);
        var parent = owner.Budget.AllocateObject(17, static value => new ParentValue(value));
        var checkpoint = owner.Budget.ObjectCheckpoint;
        var failure = Assert.Throws<Utf8ResourceLimitExceededException>(() => Prepare(route, find, execution));
        Assert.Equal(Utf8ResourceLimitKind.ConstructionBytes, failure.ResourceKind);
        Assert.Equal(2048ul, failure.Limit);
        Assert.True(failure.Requested > failure.Limit);
        Assert.InRange(owner.Budget.PeakBytes, 113ul, 2048ul);
        Assert.Equal(112ul, owner.Budget.ReservedBytes);
        Assert.Equal(0ul, owner.Budget.Depth);
        Assert.Same(checkpoint, owner.Budget.ObjectCheckpoint);
        Assert.Equal(17, parent.Value);
    }
}
