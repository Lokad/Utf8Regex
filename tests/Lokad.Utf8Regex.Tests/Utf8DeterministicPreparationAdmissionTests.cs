using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.FrontEnd;
using Lokad.Utf8Regex.Internal.Planning;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8DeterministicPreparationAdmissionTests
{
    private sealed class ParentValue(int value) { internal readonly int Value = value; }

    private static Utf8ScalarClass Class(string pattern)
    {
        var tree = Utf8OwnedTreeParser.TryParse(pattern, RegexOptions.CultureInvariant);
        Assert.NotNull(tree);
        var scalarClass = tree.Root.Child(0).ScalarClass;
        Assert.NotNull(scalarClass);
        return scalarClass;
    }

    private static Utf8ExecutionNode Node(Utf8ExecutionNodeKind kind, string text, params Utf8ExecutionNode[] children)
        => new(kind, RegexOptions.None, children, kind == Utf8ExecutionNodeKind.Set ? Class(text) : text, '\0', 0, 0, 1);

    private static Utf8ExecutionNode Make(int route, int depth)
    {
        var node = Node(Utf8ExecutionNodeKind.Multi, "ab");
        for (var index = 0; index < depth; index++)
            node = route is 1 or 3
                ? Node(Utf8ExecutionNodeKind.Alternate, "", node, Node(Utf8ExecutionNodeKind.Multi, "xy"))
                : Node(Utf8ExecutionNodeKind.Concatenate, "", Node(Utf8ExecutionNodeKind.Group, "", node), Node(Utf8ExecutionNodeKind.Multi, "c"));
        return node;
    }

    private static object? Call(Type type, string name, params object?[] args)
    {
        var method = type.GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException();
        try { return method.Invoke(null, args); }
        catch (TargetInvocationException failure) when (failure.InnerException is { } inner)
        {
            ExceptionDispatchInfo.Capture(inner).Throw();
            throw;
        }
    }

    private static void Run(int route, Utf8ExecutionNode node)
    {
        // A preparation caller releases the folded payload after consuming it.
        using var operation = Utf8ConstructionScope.Enter();
        var anchor = typeof(Utf8DeterministicAnchorSearch);
        switch (route)
        {
            case 0:
                var prefix = Utf8ExecutionPrefixFamilies.Find(node);
                Assert.NotNull(prefix);
                Assert.StartsWith("ab", Assert.Single(prefix));
                break;
            case 1:
                Assert.True(Assert.IsType<bool>(Call(anchor, "TryGetLeadingAsciiByteMask", node, 0ul, 0ul)));
                break;
            case 2:
                Assert.True(Assert.IsType<bool>(Call(anchor, "IsByteSafeNode", node)));
                break;
            case 3:
                Assert.NotEmpty(Utf8ExecutionPrefixFamilies.Find(node) ?? []);
                break;
            case 4:
                Assert.NotEmpty(Assert.IsType<Utf8DeterministicByteGuard[]>(Call(typeof(Utf8DeterministicVerifierGuards), "TryExtractPrefixGuards", node, 0)));
                break;
            case 5:
                object?[] args = [new[] { "a000", "A000" }, null];
                Assert.True(Assert.IsType<bool>(Call(anchor, "TryCreateAsciiIgnoreCaseAnchorLiteral", args)));
                Assert.Equal("a000"u8.ToArray(), Assert.IsType<byte[]>(args[1]));
                break;
        }
    }

    public static IEnumerable<object[]> RootDenials()
    {
        for (var route = 0; route < 5; route++)
        for (var denial = 0; denial < 3; denial++) yield return [route, denial];
    }

    [Theory]
    [MemberData(nameof(RootDenials))]
    public void RootDenialPrecedesFrameAcquisition(int route, int denial)
    {
        var input = Make(route, 0);
        using var owner = new Utf8ConstructionScope(0, denial == 0 ? 128ul : ulong.MaxValue,
            denial == 2 ? 2ul : ulong.MaxValue, denial == 1 ? 0ul : ulong.MaxValue);
        var parent = owner.Budget.AllocateObject(17, static value => new ParentValue(value));
        var checkpoint = owner.Budget.ReservedBytes;
        var failure = Assert.Throws<Utf8ResourceLimitExceededException>(() => Run(route, input));
        Assert.Equal(denial switch { 0 => Utf8ResourceLimitKind.ConstructionBytes,
            1 => Utf8ResourceLimitKind.ConstructionDepth, _ => Utf8ResourceLimitKind.ConstructionWorkSteps }, failure.ResourceKind);
        Assert.Equal(128ul, owner.Budget.PeakBytes);
        Assert.Equal(128ul, owner.Budget.ReservedBytes);
        Assert.Equal(0ul, owner.Budget.Depth);
        Assert.Equal(checkpoint, owner.Budget.ReservedBytes);
        Assert.Equal(17, parent.Value);
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)]
    public void ChildDepthDenialRestoresParentAndAllowsRecovery(int route)
    {
        var input = Make(route, 32);
        using var owner = new Utf8ConstructionScope(0, ulong.MaxValue, ulong.MaxValue, 8);
        var parent = owner.Budget.AllocateObject(17, static value => new ParentValue(value));
        var checkpoint = owner.Budget.ReservedBytes;
        var failure = Assert.Throws<Utf8ResourceLimitExceededException>(() => Run(route, input));
        Assert.Equal(Utf8ResourceLimitKind.ConstructionDepth, failure.ResourceKind);
        Assert.Equal(9ul, failure.Requested);
        Assert.Equal(8ul, owner.Budget.PeakDepth);
        Assert.Equal(128ul, owner.Budget.ReservedBytes);
        Assert.Equal(0ul, owner.Budget.Depth);
        Assert.Equal(checkpoint, owner.Budget.ReservedBytes);
        var spent = owner.Budget.WorkSteps;
        Run(route, Make(route, 0));
        Assert.True(owner.Budget.WorkSteps > spent);
        Assert.Equal(128ul, owner.Budget.ReservedBytes);
        Assert.Equal(17, parent.Value);
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)]
    public void ArrayGrowthDenialReleasesEveryScratchLoan(int route)
    {
        var input = Make(route, 128);
        using var owner = new Utf8ConstructionScope(0, 2048, ulong.MaxValue, ulong.MaxValue);
        var parent = owner.Budget.AllocateObject(17, static value => new ParentValue(value));
        var checkpoint = owner.Budget.ReservedBytes;
        var failure = Assert.Throws<Utf8ResourceLimitExceededException>(() => Run(route, input));
        Assert.Equal(Utf8ResourceLimitKind.ConstructionBytes, failure.ResourceKind);
        Assert.True(failure.Requested > 2048);
        Assert.InRange(owner.Budget.PeakBytes, 113ul, 2048ul);
        Assert.Equal(128ul, owner.Budget.ReservedBytes);
        Assert.Equal(0ul, owner.Budget.Depth);
        Assert.Equal(checkpoint, owner.Budget.ReservedBytes);
        Assert.Equal(17, parent.Value);
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)] [InlineData(5)]
    public void EveryWorkCutoffReleasesScratchWithoutRefundingWork(int route)
    {
        var input = Make(route, 3);
        ulong work;
        using (var measure = new Utf8ConstructionScope(0, ulong.MaxValue, ulong.MaxValue, ulong.MaxValue))
        {
            Run(route, input);
            work = measure.Budget.WorkSteps;
            Assert.Equal(0ul, measure.Budget.ReservedBytes);
        }
        Assert.InRange(work, 1ul, 10000ul);
        for (var cutoff = 0ul; cutoff <= work; cutoff++)
        {
            using var owner = new Utf8ConstructionScope(0, ulong.MaxValue, cutoff + 2, ulong.MaxValue);
            var parent = owner.Budget.AllocateObject(17, static value => new ParentValue(value));
            var checkpoint = owner.Budget.ReservedBytes;
            var failure = Record.Exception(() => Run(route, input));
            if (cutoff < work)
            {
                var breach = Assert.IsType<Utf8ResourceLimitExceededException>(failure);
                Assert.Equal(Utf8ResourceLimitKind.ConstructionWorkSteps, breach.ResourceKind);
                Assert.True(breach.Requested > cutoff + 2);
            }
            else Assert.Null(failure);
            Assert.Equal(128ul, owner.Budget.ReservedBytes);
            Assert.Equal(0ul, owner.Budget.Depth);
            Assert.Equal(0ul, owner.Budget.Reserved);
            Assert.Equal(checkpoint, owner.Budget.ReservedBytes);
            Assert.InRange(owner.Budget.WorkSteps, 2ul, cutoff + 2);
            Assert.Equal(17, parent.Value);
        }
    }

    [Fact]
    public void BorrowedWhitespaceSuffixKeepsGuardOffsetsAndNestedPartialPrefix()
    {
        var root = Node(Utf8ExecutionNodeKind.Concatenate, "", Node(Utf8ExecutionNodeKind.Beginning, ""),
            Node(Utf8ExecutionNodeKind.Loop, "", Node(Utf8ExecutionNodeKind.Group, "", Node(Utf8ExecutionNodeKind.Set, @"\s"))),
            Node(Utf8ExecutionNodeKind.Boundary, ""),
            Node(Utf8ExecutionNodeKind.Concatenate, "", Node(Utf8ExecutionNodeKind.Multi, "ab"), Node(Utf8ExecutionNodeKind.Unsupported, "")),
            Node(Utf8ExecutionNodeKind.Multi, "c"));
        var plan = Utf8SearchPlan.Prepare(Utf8SearchFacts.Create(Utf8SearchKind.None,
            new Utf8SearchFactData { FallbackStartTransform = new(0, Utf8FallbackStartTransformKind.TrimLeadingAsciiWhitespace) }));
        var ascii = Node(Utf8ExecutionNodeKind.Concatenate, "", root.Children[0],
            Node(Utf8ExecutionNodeKind.Loop, "", Node(Utf8ExecutionNodeKind.Group, "", Node(Utf8ExecutionNodeKind.Set, @"[\t-\r ]"))),
            root.Children[2], root.Children[3], root.Children[4]);
        using var owner = new Utf8ConstructionScope(0, ulong.MaxValue, ulong.MaxValue, ulong.MaxValue);
        var parent = owner.Budget.AllocateObject(17, static value => new ParentValue(value));
        var checkpoint = owner.Budget.ReservedBytes;
        var slice = Assert.IsType<ValueTuple<Utf8ExecutionNode, int>>(Call(typeof(Utf8DeterministicVerifierGuards), "SkipLeadingAsciiWhitespacePrefix", root));
        Assert.Same(root, slice.Item1);
        Assert.Equal(0, slice.Item2);
        Assert.False(Utf8DeterministicVerifierGuards.Create(new(root), plan).HasValue);
        slice = Assert.IsType<ValueTuple<Utf8ExecutionNode, int>>(Call(typeof(Utf8DeterministicVerifierGuards), "SkipLeadingAsciiWhitespacePrefix", ascii));
        Assert.Same(ascii, slice.Item1);
        Assert.Equal(3, slice.Item2);
        using (var discarded = Utf8ConstructionScope.Enter())
        {
            var guards = Utf8DeterministicVerifierGuards.Create(new(ascii), plan);
            Assert.NotNull(guards.PrefixGuards);
            Assert.Equal(new[] { 0, 1 }, guards.PrefixGuards.Select(static guard => guard.Offset));
            Assert.True(guards.Matches(" \tabc"u8, 0));
            Assert.True(guards.Matches(" \tabx"u8, 0)); // opaque width cannot certify c
            Assert.False(guards.Matches(" \taxx"u8, 0));
        }
        Assert.Equal(128ul, owner.Budget.ReservedBytes);
        Assert.Equal(checkpoint, owner.Budget.ReservedBytes);
        Assert.Equal(0ul, owner.Budget.Depth);
        Assert.Equal(17, parent.Value);
    }

    [Fact]
    public void FoldedPayloadAdmitsBytesAfterValidationAndTransfersOnlyItsArrayLoan()
    {
        var anchor = typeof(Utf8DeterministicAnchorSearch);
        using var owner = new Utf8ConstructionScope(0, 128, ulong.MaxValue, ulong.MaxValue);
        var parent = owner.Budget.AllocateObject(17, static value => new ParentValue(value));
        var checkpoint = owner.Budget.ReservedBytes;
        object?[] rejected = [new[] { "abc", "ABC" }, null];
        Assert.False(Assert.IsType<bool>(Call(anchor, "TryCreateAsciiIgnoreCaseAnchorLiteral", rejected)));
        Assert.Empty(Assert.IsType<byte[]>(rejected[1]));
        Assert.Equal(128ul, owner.Budget.PeakBytes);
        object?[] accepted = [new[] { "a000", "A000" }, null];
        var failure = Assert.Throws<Utf8ResourceLimitExceededException>(() => Call(anchor, "TryCreateAsciiIgnoreCaseAnchorLiteral", accepted));
        Assert.Equal(Utf8ResourceLimitKind.ConstructionBytes, failure.ResourceKind);
        Assert.Equal(128ul + Utf8ConstructionBudget.ArrayBytes<byte>(4), failure.Requested);
        Assert.Equal(128ul, owner.Budget.ReservedBytes);
        Assert.Equal(checkpoint, owner.Budget.ReservedBytes);
        Assert.Equal(17, parent.Value);
    }

    [Fact]
    public void WideFoldedLiteralUsesAdmittedHeapPayloadWithoutWidthSizedStackScratch()
    {
        const int width = 1_000_001;
        var suffix = new string('0', width - 1);
        object?[] args = [new[] { "a" + suffix, "A" + suffix }, null];
        var bytes = Utf8ConstructionBudget.ArrayBytes<byte>(width);
        using var owner = new Utf8ConstructionScope(0, bytes, ulong.MaxValue, 0);
        Assert.True(Assert.IsType<bool>(Call(typeof(Utf8DeterministicAnchorSearch), "TryCreateAsciiIgnoreCaseAnchorLiteral", args)));
        var folded = Assert.IsType<byte[]>(args[1]);
        Assert.Equal(width, folded.Length);
        Assert.Equal((byte)'a', folded[0]);
        Assert.Equal(-1, folded.AsSpan(1).IndexOfAnyExcept((byte)'0'));
        Assert.Equal(bytes, owner.Budget.PeakBytes);
        Assert.Equal(bytes, owner.Budget.ReservedBytes);
        Assert.Equal(4ul * width + 3, owner.Budget.WorkSteps); // one distinct-member comparison
        Assert.Equal(0ul, owner.Budget.Depth);
        owner.Budget.ReleaseArray(folded, instructions: false);
        Assert.Equal(0ul, owner.Budget.ReservedBytes);
    }
}
