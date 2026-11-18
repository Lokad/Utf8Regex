using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.FrontEnd;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8PreparationFrameAdmissionTests
{
    private static readonly Func<Utf8TreeNode, bool> ContainsCapture =
        (typeof(Utf8Regex).GetMethod("ContainsCapture", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)
        ?? throw new InvalidOperationException()).CreateDelegate<Func<Utf8TreeNode, bool>>();
    private static readonly Utf8SliceProgram CaptureProgram;
    static Utf8PreparationFrameAdmissionTests() => CaptureProgram = Utf8FrontEnd.Compile("(a)", RegexOptions.None).ScalarProgram!;
    private sealed class ParentValue(int value) { internal readonly int Value = value; }
    private static Utf8TreeNode Node(Utf8TreeNodeKind kind, Utf8TreeNode[]? children = null,
        int capture = 0, string text = "", char ch = 'a', int minimum = 0, int maximum = 1) =>
        Utf8TreeNode.Create(kind, RegexOptions.None, ch, text,
            kind == Utf8TreeNodeKind.Capture ? capture : minimum, maximum,
            children, null, matchCaseInsensitive: false);

    private static Utf8TreeNode Tree(int route, int depth = 0)
    {
        var leaf = Node(Utf8TreeNodeKind.One);
        for (var index = 0; index < depth; index++) leaf = Node(Utf8TreeNodeKind.Group, [leaf]);
        return Node(Utf8TreeNodeKind.Capture, [route == 7 ? leaf : Node(Utf8TreeNodeKind.Capture, [leaf], capture: 1)]);
    }

    private static Utf8TreeNode FindTree(int depth = 0)
    {
        var node = Utf8TreeNode.Create(Utf8TreeNodeKind.One, RegexOptions.None, 'a');
        for (var index = 0; index < depth; index++) node = Utf8TreeNode.Create(kind: Utf8TreeNodeKind.Group, options: RegexOptions.None,
            ch: '\0', str: null, m: 0, n: 0,
            children: node, parent: null, matchCaseInsensitive: (RegexOptions.None & RegexOptions.IgnoreCase) != 0);
        return node;
    }

    private static object? Prepare(int route, Utf8TreeNode find, Utf8TreeNode execution) => route switch
    {
        0 => Utf8OwnedFindOptimizations.ComputeByteWidth(find).Minimum,
        1 => Utf8OwnedFindOptimizations.ComputeByteWidth(find).Maximum,
        2 => Utf8OwnedFindOptimizations.TryExtractLongestLiteral(find),
        3 => Utf8GroupedLiteralSpanTable.TryCreate(execution, CaptureProgram),
        7 => ContainsCapture(execution),
        _ => throw new ArgumentOutOfRangeException(nameof(route)),
    };

    public static IEnumerable<object[]> RootDenials()
    {
        foreach (var route in new[] { 0, 1, 2, 3, 7 })
        for (var denial = 0; denial < 3; denial++) yield return [route, denial];
    }

    [Theory]
    [MemberData(nameof(RootDenials))]
    public void RootDenialRestoresTheEnclosingOwner(int route, int denial)
    {
        var find = FindTree();
        var execution = Tree(route);
        using var owner = new Utf8ConstructionScope(ulong.MaxValue, denial == 0 ? 128ul : ulong.MaxValue,
            denial == 2 ? 2ul : ulong.MaxValue, denial == 1 ? 0ul : ulong.MaxValue);
        var parent = owner.Budget.AllocateObject(17, static value => new ParentValue(value));
        var checkpoint = owner.Budget.ReservedBytes;
        var failure = Assert.Throws<Utf8ResourceLimitExceededException>(() => Prepare(route, find, execution));
        Assert.Equal(denial switch { 0 => Utf8ResourceLimitKind.ConstructionBytes, 1 => Utf8ResourceLimitKind.ConstructionDepth,
            _ => Utf8ResourceLimitKind.ConstructionWorkSteps }, failure.ResourceKind);
        Assert.Equal(128ul, owner.Budget.ReservedBytes);
        Assert.Equal(128ul, owner.Budget.PeakBytes);
        Assert.Equal(0ul, owner.Budget.Depth);
        Assert.Equal(checkpoint, owner.Budget.ReservedBytes);
        Assert.Equal(17, parent.Value);
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(7)]
    public void DeepChildDenialRestoresParentAndAllowsSameOwnerRecovery(int route)
    {
        var find = FindTree(32);
        var execution = Tree(route, 32);
        using var owner = new Utf8ConstructionScope(ulong.MaxValue, ulong.MaxValue, ulong.MaxValue, 8);
        var parent = owner.Budget.AllocateObject(17, static value => new ParentValue(value));
        var checkpoint = owner.Budget.ReservedBytes;
        var failure = Assert.Throws<Utf8ResourceLimitExceededException>(() => Prepare(route, find, execution));
        Assert.Equal(Utf8ResourceLimitKind.ConstructionDepth, failure.ResourceKind);
        Assert.Equal(9ul, failure.Requested);
        Assert.Equal(8ul, owner.Budget.PeakDepth);
        Assert.Equal(128ul, owner.Budget.ReservedBytes);
        Assert.Equal(0ul, owner.Budget.Depth);
        Assert.Equal(checkpoint, owner.Budget.ReservedBytes);
        var spent = owner.Budget.WorkSteps;
        using (var recovery = Utf8ConstructionScope.Enter())
            Assert.NotNull(Prepare(route, FindTree(), Tree(route)));
        Assert.True(owner.Budget.WorkSteps > spent);
        Assert.Equal(128ul, owner.Budget.ReservedBytes);
        Assert.Equal(checkpoint, owner.Budget.ReservedBytes);
        Assert.Equal(17, parent.Value);
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(7)]
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
            var checkpoint = owner.Budget.ReservedBytes;
            using (var speculative = Utf8ConstructionScope.Enter())
            {
                var failure = Record.Exception(() => Prepare(route, find, execution));
                if (cutoff < work)
                {
                    var breach = Assert.IsType<Utf8ResourceLimitExceededException>(failure);
                    Assert.Equal(Utf8ResourceLimitKind.ConstructionWorkSteps, breach.ResourceKind);
                    Assert.Equal(cutoff + 2, breach.Limit);
                    Assert.True(breach.Requested > breach.Limit);
                    Assert.Equal(128ul, owner.Budget.ReservedBytes);
                    Assert.Equal(checkpoint, owner.Budget.ReservedBytes);
                }
                else Assert.Null(failure);
                Assert.Equal(0ul, owner.Budget.Depth);
                Assert.True(owner.Budget.WorkSteps <= cutoff + 2);
            }
            Assert.Equal(128ul, owner.Budget.ReservedBytes);
            Assert.Equal(checkpoint, owner.Budget.ReservedBytes);
            Assert.Equal(17, parent.Value);
        }
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)]
    public void DeclinedScratchAndLateEligibilityReleaseCompletedLoans(int shape)
    {
        var a = Node(Utf8TreeNodeKind.One);
        var captured = Node(Utf8TreeNodeKind.Capture, [a], capture: 1);
        var body = shape switch
        {
            0 => Node(Utf8TreeNodeKind.Alternate, [captured, Node(Utf8TreeNodeKind.Capture,
                [Node(Utf8TreeNodeKind.Multi, text: "bb")], capture: 1)]),
            1 => Node(Utf8TreeNodeKind.Concatenate, [captured, Node(Utf8TreeNodeKind.Capture, [a], capture: 1)]),
            _ => Node(Utf8TreeNodeKind.Multi, text: ""),
        };
        var execution = Node(Utf8TreeNodeKind.Capture, [body]);
        using var owner = new Utf8ConstructionScope(ulong.MaxValue, ulong.MaxValue, ulong.MaxValue, ulong.MaxValue);
        var parent = owner.Budget.AllocateObject(17, static value => new ParentValue(value));
        var checkpoint = owner.Budget.ReservedBytes;
        Assert.Null(Utf8GroupedLiteralSpanTable.TryCreate(execution, CaptureProgram));
        Assert.Equal(128ul, owner.Budget.ReservedBytes);
        Assert.Equal(checkpoint, owner.Budget.ReservedBytes);
        Assert.Equal(0ul, owner.Budget.Depth);
        Assert.True(owner.Budget.WorkSteps > 2);
        using (var recovery = Utf8ConstructionScope.Enter()) Assert.NotNull(Utf8GroupedLiteralSpanTable.TryCreate(Tree(3), CaptureProgram));
        Assert.Equal(128ul, owner.Budget.ReservedBytes);
        Assert.Equal(17, parent.Value);
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(7)]
    public void SuccessfulPreparationKeepsRetainedPayloadLoansUntilOwnerCloses(int route)
    {
        var find = FindTree(3);
        var execution = Tree(route, 3);
        using var owner = new Utf8ConstructionScope(ulong.MaxValue, ulong.MaxValue, ulong.MaxValue, ulong.MaxValue);
        var parent = owner.Budget.AllocateObject(17, static value => new ParentValue(value));
        var checkpoint = owner.Budget.ReservedBytes;
        using (var child = Utf8ConstructionScope.Enter())
        {
            var result = Prepare(route, find, execution);
            Assert.NotNull(result);
            Assert.True(owner.Budget.PeakBytes > 128);
            if (route == 2)
            {
                Assert.Equal("a", Assert.IsType<string>(result));
                Assert.True(owner.Budget.ReservedBytes > 128);
                Assert.True(owner.Budget.ReservedBytes > checkpoint);
            }
            else if (route is >= 3 and <= 6)
            {
                Assert.True(owner.Budget.ReservedBytes > 128);
                Assert.True(owner.Budget.ReservedBytes > checkpoint);
                Assert.NotNull(result);
            }
            else
            {
                Assert.Equal(128ul, owner.Budget.ReservedBytes);
                Assert.Equal(checkpoint, owner.Budget.ReservedBytes);
            }
            Assert.Equal(0ul, owner.Budget.Depth);
        }
        Assert.Equal(128ul, owner.Budget.ReservedBytes);
        Assert.Equal(0ul, owner.Budget.Depth);
        Assert.Equal(checkpoint, owner.Budget.ReservedBytes);
        Assert.Equal(17, parent.Value);
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(7)]
    public void ChildArrayGrowthDenialRestoresParentBytes(int route)
    {
        var find = FindTree(128);
        var execution = Tree(route, 128);
        using var owner = new Utf8ConstructionScope(ulong.MaxValue, 2048, ulong.MaxValue, ulong.MaxValue);
        var parent = owner.Budget.AllocateObject(17, static value => new ParentValue(value));
        var checkpoint = owner.Budget.ReservedBytes;
        var failure = Assert.Throws<Utf8ResourceLimitExceededException>(() => Prepare(route, find, execution));
        Assert.Equal(Utf8ResourceLimitKind.ConstructionBytes, failure.ResourceKind);
        Assert.Equal(2048ul, failure.Limit);
        Assert.True(failure.Requested > failure.Limit);
        Assert.InRange(owner.Budget.PeakBytes, 113ul, 2048ul);
        Assert.Equal(128ul, owner.Budget.ReservedBytes);
        Assert.Equal(0ul, owner.Budget.Depth);
        Assert.Equal(checkpoint, owner.Budget.ReservedBytes);
        Assert.Equal(17, parent.Value);
    }
}
