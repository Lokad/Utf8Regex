using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.FrontEnd;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8AsciiPreparationContinuationTests
{
    private const RegexOptions Options = RegexOptions.CultureInvariant;
    private static Utf8TreeNode Text(string value) => Utf8TreeNode.Create(Utf8TreeNodeKind.Multi, Options, value);
    private static Utf8TreeNode Node(Utf8TreeNodeKind kind, params Utf8TreeNode[] children)
        => Utf8TreeNode.Create(kind: kind, options: Options,
            ch: '\0', str: null, m: 0, n: 0,
            children: children.ToList(), parent: null, matchCaseInsensitive: (Options & RegexOptions.IgnoreCase) != 0);
    private static Utf8TreeNode Wrap(Utf8TreeNode node, Utf8TreeNodeKind kind, int depth)
    {
        for (var index = 0; index < depth; index++)
        {
            var wrapper = Node(kind, node);
            if (kind == Utf8TreeNodeKind.Loop) { wrapper.M = 1; wrapper.N = 1; }
            node = wrapper;
        }
        return node;
    }

    private sealed class CompilerState
    {
        internal Utf8ConstructionBudget? Budget;
    }
    private readonly record struct BranchSnapshot(List<AsciiSimplePatternToken> Tokens, List<AsciiFixedLiteralCheck> FixedChecks);
    private static CompilerState Compiler() => new();
    private static Utf8ConstructionBudget Invocation(CompilerState compiler) => compiler.Budget ?? throw new InvalidOperationException();
    private static bool Lower(CompilerState compiler, Utf8TreeNode node, out Array branches)
    {
        using var construction = Utf8ConstructionScope.Enter();
        compiler.Budget = construction.Budget;
        using var storage = Internal.Utf8ConstructionSession.Create(construction.Budget);
        var preparation = construction.Budget.AllocateObject((4096, 4096),
            static limits => new Utf8PreparationBudget(limits.Item1, limits.Item2));
        object?[] arguments = [storage, node, preparation, 0, null];
        bool found;
        try
        {
            found = Assert.IsType<bool>((typeof(Utf8AsciiSimplePatternLowerer)
                .GetMethod("TryLowerBranches", BindingFlags.NonPublic | BindingFlags.Static)
                ?? throw new InvalidOperationException()).Invoke(null, arguments));
        }
        catch (TargetInvocationException failure) when (failure.InnerException is not null)
        {
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure.InnerException).Throw();
            throw;
        }
        // Snapshot witnesses before session disposal; the production buffers never
        // escape this test owner. Private optimizer declines remain empty results.
        branches = found ? ReadBuffer<object>(arguments[4] ?? throw new InvalidOperationException())
            .Select(value => new BranchSnapshot(ReadBuffer<AsciiSimplePatternToken>(Property<object>(value, "Tokens")),
                ReadBuffer<AsciiFixedLiteralCheck>(Property<object>(value, "FixedChecks")))).ToArray() : [];
        return found;
    }
    private static List<T> ReadBuffer<T>(object value)
    {
        const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
        var count = Assert.IsType<int>(value.GetType().GetProperty("Count", flags)?.GetValue(value));
        var item = value.GetType().GetProperty("Item", flags) ?? throw new InvalidOperationException();
        return Enumerable.Range(0, count).Select(index => (T)(item.GetValue(value, [index])
            ?? throw new InvalidOperationException())).ToList();
    }
    private static T Property<T>(object value, string name)
        => (T)(value.GetType().GetProperty(name)?.GetValue(value) ?? throw new InvalidOperationException());
    private static string[] Literals(Array branches)
        => branches.Cast<BranchSnapshot>().Select(branch => Encoding.ASCII.GetString(branch.Tokens
            .Select(token => token.Literal).ToArray())).ToArray();

    public static IEnumerable<object[]> DepthCases()
    {
        foreach (var kind in new[] { Utf8TreeNodeKind.Group, Utf8TreeNodeKind.Capture, Utf8TreeNodeKind.Concatenate, Utf8TreeNodeKind.Loop })
        {
            var boundary = kind == Utf8TreeNodeKind.Concatenate ? 128 : 256;
            yield return [(int)kind, boundary, true];
            yield return [(int)kind, boundary + 1, false];
            yield return [(int)kind, 16385, false];
        }
    }
    [Theory]
    [MemberData(nameof(DepthCases))]
    public void ExistingDepthBoundaryIsRetainedWithoutRecursiveLowering(int kind, int depth, bool accepted)
    {
        var compiler = Compiler();
        Assert.Equal(accepted, Lower(compiler, Wrap(Text("a"), (Utf8TreeNodeKind)kind, depth), out var branches));
        Assert.Equal(accepted ? new[] { "a" } : [], Literals(branches));
        Assert.Equal(0UL, Invocation(compiler).ReservedBytes);
    }
    [Theory]
    [InlineData(4095, true)]
    [InlineData(4096, false)]
    [InlineData(65536, false)]
    public void LiteralPayloadMustFitPreparationBudgetBeforeMaterialization(int length, bool accepted)
    {
        Assert.Equal(accepted, Lower(Compiler(), Text(new string('a', length)), out var branches));
        Assert.Equal(accepted ? length : 0, Literals(branches).Sum(value => value.Length));
    }
    [Theory]
    [InlineData(1365, true)]
    [InlineData(1366, false)]
    [InlineData(16385, false)]
    public void SingleBranchConcatenationCannotBypassTheWorkBudget(int width, bool accepted)
    {
        var tree = Node(Utf8TreeNodeKind.Concatenate, Enumerable.Range(0, width)
            .Select(_ => Utf8TreeNode.Create(Utf8TreeNodeKind.One, Options, 'a')).ToArray());
        Assert.Equal(accepted, Lower(Compiler(), tree, out var branches));
        Assert.Equal(accepted ? width : 0, Literals(branches).Sum(value => value.Length));
    }
    [Theory]
    [InlineData(64, true)]
    [InlineData(65, false)]
    [InlineData(16385, false)]
    public void AlternationChecksExpandedBranchCapacityBeforeAppending(int width, bool accepted)
    {
        var tree = Node(Utf8TreeNodeKind.Alternate, Enumerable.Range(0, width).Select(index => Text(index.ToString())).ToArray());
        Assert.Equal(accepted, Lower(Compiler(), tree, out var branches));
        Assert.Equal(accepted ? Enumerable.Range(0, width).Select(index => index.ToString()).ToArray() : [], Literals(branches));
    }
    [Fact]
    public void ProductAndGreedyRepeatKeepOriginalBranchOrdering()
    {
        var left = Node(Utf8TreeNodeKind.Alternate, Text("a"), Text("b"));
        var right = Node(Utf8TreeNodeKind.Alternate, Text("x"), Text("y"));
        Assert.True(Lower(Compiler(), Node(Utf8TreeNodeKind.Concatenate, left, right), out var product));
        Assert.Equal(["ax", "ay", "bx", "by"], Literals(product));
        var loop = Node(Utf8TreeNodeKind.Loop, left);
        loop.M = 1; loop.N = 2;
        Assert.True(Lower(Compiler(), loop, out var variants));
        Assert.Equal(["aa", "ab", "ba", "bb", "a", "b"], Literals(variants));
    }
    [Theory]
    [InlineData(254, true)]
    [InlineData(255, false)]
    public void LookaroundRetainsItsTwoDepthUnitsAndShiftedLiteralChecks(int wrappers, bool accepted)
    {
        var look = Node(Utf8TreeNodeKind.PositiveLookaround, Wrap(Text("bc"), Utf8TreeNodeKind.Group, wrappers));
        Assert.Equal(accepted, Lower(Compiler(), look, out var isolated));
        if (accepted) Assert.Equal("bc", Encoding.ASCII.GetString(Property<List<AsciiFixedLiteralCheck>>(isolated.GetValue(0)
            ?? throw new InvalidOperationException(), "FixedChecks")[0].Literal));
        var shallow = Node(Utf8TreeNodeKind.PositiveLookaround, Text("bc"));
        Assert.True(Lower(Compiler(), Node(Utf8TreeNodeKind.Concatenate, Text("a"), shallow, Text("b")), out var branches));
        var branch = branches.GetValue(0) ?? throw new InvalidOperationException();
        Assert.Equal(["ab"], Literals(branches));
        var check = Assert.Single(Property<List<AsciiFixedLiteralCheck>>(branch, "FixedChecks"));
        Assert.Equal(1, check.Offset);
        Assert.Equal("bc", Encoding.ASCII.GetString(check.Literal));
    }
    [Fact]
    public void EveryContinuationWorkDepthAndStorageDenialReleasesScratchAndKeepsOtherLeases()
    {
        var tree = Wrap(Text("a"), Utf8TreeNodeKind.Concatenate, 60);
        var baseline = Compiler();
        Assert.True(Lower(baseline, tree, out _));
        var measured = Invocation(baseline);
        Assert.True(measured.PeakBytes > 0);
        var limits = Enumerable.Range(0, checked((int)measured.WorkSteps))
            .Select(value => (Bytes: ulong.MaxValue, Work: (ulong)value, Depth: ulong.MaxValue,
                Kind: Utf8ResourceLimitKind.ConstructionWorkSteps))
            .Concat(Enumerable.Range(0, checked((int)measured.PeakDepth)).Select(value =>
                (Bytes: ulong.MaxValue, Work: ulong.MaxValue, Depth: (ulong)value,
                    Kind: Utf8ResourceLimitKind.ConstructionDepth)))
            .Concat(new[] { (Bytes: 7ul, Work: ulong.MaxValue, Depth: ulong.MaxValue, Kind: Utf8ResourceLimitKind.ConstructionBytes),
                (Bytes: measured.PeakBytes + 6, Work: ulong.MaxValue, Depth: ulong.MaxValue, Kind: Utf8ResourceLimitKind.ConstructionBytes) });
        foreach (var limit in limits)
        {
            using var owner = new Utf8ConstructionScope(0, limit.Bytes, limit.Work, limit.Depth);
            owner.Budget.ReserveBytes(7);
            var checkpoint = owner.Budget.ObjectCheckpoint;
            var failure = Assert.Throws<Utf8ResourceLimitExceededException>(() => Lower(Compiler(), tree, out _));
            Assert.Equal(limit.Kind, failure.ResourceKind);
            Assert.True(failure.Requested > failure.Limit);
            Assert.Equal(7ul, owner.Budget.ReservedBytes);
            Assert.Equal(0ul, owner.Budget.Reserved);
            Assert.Equal(0ul, owner.Budget.Depth);
            Assert.Same(checkpoint, owner.Budget.ObjectCheckpoint);
        }
        using var recovery = Utf8ConstructionScope.Enter();
        recovery.Budget.ReserveBytes(7);
        Assert.True(Lower(Compiler(), tree, out _));
        Assert.Equal(7ul, recovery.Budget.ReservedBytes);
        Assert.Equal(0ul, recovery.Budget.Depth);
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DescentCancellationAndDeadlineReleasePendingFrames(bool expire)
    {
        using var clock = Utf8DeadlineTestClock.Start();
        var compiler = Compiler();
        using var source = new CancellationTokenSource();
        var tree = Wrap(Text("a"), Utf8TreeNodeKind.Concatenate, 100);
        using var owner = Utf8ConstructionScope.Enter();
        owner.Budget.ReserveBytes(7);
        var checkpoint = owner.Budget.ObjectCheckpoint;
        var deadline = Utf8ExecutionDeadline.Start(expire ? TimeSpan.FromMilliseconds(200) : Regex.InfiniteMatchTimeout);
        owner.Budget.Deadline = deadline;
        owner.Budget.CancellationToken = source.Token;
        var entered = false;
        owner.Budget.CheckpointHook = _ => { entered = true; if (expire) clock.Advance(250); else source.Cancel(); };
        var failure = Record.Exception(() => Lower(compiler, tree, out _));
        if (expire) Assert.IsType<Utf8ExecutionDeadlineExpiredException>(failure);
        else Assert.Equal(source.Token, Assert.IsType<OperationCanceledException>(failure).CancellationToken);
        Assert.True(entered);
        Assert.Same(deadline, owner.Budget.Deadline);
        Assert.Equal(7ul, owner.Budget.ReservedBytes);
        Assert.Equal(0ul, owner.Budget.Depth);
        Assert.Same(checkpoint, owner.Budget.ObjectCheckpoint);
    }
    [Theory]
    [InlineData(65)]
    [InlineData(16385)]
    public void DeepTrailingNewlineAndCaseFactsUseIterativeTraversal(int depth)
    {
        var newline = typeof(Utf8AsciiSimplePatternLowerer).GetMethod("AllowsTrailingNewlineBeforeEnd", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException();
        var folded = typeof(Utf8AsciiSimplePatternLowerer).GetMethod("HasCaseClearingGroup", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException();
        var tail = Wrap(Utf8TreeNode.Create(Utf8TreeNodeKind.EndZ, Options), Utf8TreeNodeKind.Capture, depth);
        Assert.True(Assert.IsType<bool>(newline.Invoke(null, [Node(Utf8TreeNodeKind.Alternate, tail, tail)])));
        Assert.False(Assert.IsType<bool>(newline.Invoke(null, [Node(Utf8TreeNodeKind.Alternate, tail, Text("a"))])));
        var clearing = Wrap(Node(Utf8TreeNodeKind.Group, Text("a")), Utf8TreeNodeKind.Capture, depth);
        Assert.True(Assert.IsType<bool>(folded.Invoke(null, [Node(Utf8TreeNodeKind.Concatenate, Text("x"), clearing)])));
        Assert.False(Assert.IsType<bool>(folded.Invoke(null, [Wrap(Text("a"), Utf8TreeNodeKind.Capture, depth)])));
    }
    public static IEnumerable<object[]> PublicCases()
    {
        foreach (var pattern in new[] { "[ab]{96}", "(?=abc)ab[cd]", "(?:ab|cd){1,2}[xy]", "\\Aab[cd]\\Z" })
        foreach (var compiled in new[] { false, true })
        foreach (var rtl in new[] { false, true })
            yield return [pattern, Options | (compiled ? RegexOptions.Compiled : RegexOptions.None) | (rtl ? RegexOptions.RightToLeft : RegexOptions.None)];
    }
    [Theory]
    [MemberData(nameof(PublicCases))]
    public void DeclinedOptimizationsAndAdmittedPlansRetainCompleteOwnedOperations(string pattern, RegexOptions options)
    {
        string[] texts = ["", "abc", "abd", "abx", "cdaby", "abcdx", "abcy", "abc\n", "\U0001f600abcy中"];
        if (pattern == "[ab]{96}")
        {
            var parsed = Utf8OwnedTreeParser.TryParse(pattern, options) ?? throw new InvalidOperationException();
            Assert.False(Lower(Compiler(), parsed.Root.Child(0), out _));
            texts = ["", new string('a', 96), new string('b', 96), new string('a', 95), "x" + new string('a', 96) + "中"];
        }
        Utf8SliceSparseGroupTests.AssertOwnedOperations(pattern, options, texts, options);
    }
}
