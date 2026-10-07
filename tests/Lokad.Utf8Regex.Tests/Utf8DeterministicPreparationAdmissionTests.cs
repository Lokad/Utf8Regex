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

    private static Utf8ExecutionNode Node(Utf8ExecutionNodeKind kind, string text, params Utf8ExecutionNode[] children)
        => new(kind, RegexOptions.None, children, text, '\0', 0, 0, 1);

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
                var prefix = new StringBuilder();
                Call(anchor, "AppendDeterministicLeadingAscii", node, prefix);
                Assert.StartsWith("ab", prefix.ToString());
                break;
            case 1:
                Assert.True(Assert.IsType<bool>(Call(anchor, "TryGetLeadingAsciiByteMask", node, 0ul, 0ul)));
                break;
            case 2:
                Assert.True(Assert.IsType<bool>(Call(anchor, "IsByteSafeNode", node)));
                break;
            case 3:
                Assert.NotEmpty(Assert.IsType<List<string>>(Call(anchor, "TryExtractLeadingAsciiLiteralsCore", node)));
                break;
            case 4:
                Assert.True(Assert.IsType<bool>(Call(typeof(Utf8DeterministicVerifierGuards), "TryAppendPrefixGuards",
                    node, new List<Utf8DeterministicByteGuard>(), 0, 0)));
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
        using var owner = new Utf8ConstructionScope(0, denial == 0 ? 112ul : ulong.MaxValue,
            denial == 2 ? 2ul : ulong.MaxValue, denial == 1 ? 0ul : ulong.MaxValue);
        var parent = owner.Budget.AllocateObject(17, static value => new ParentValue(value));
        var checkpoint = owner.Budget.ObjectCheckpoint;
        var failure = Assert.Throws<Utf8ResourceLimitExceededException>(() => Run(route, input));
        Assert.Equal(denial switch { 0 => Utf8ResourceLimitKind.ConstructionBytes,
            1 => Utf8ResourceLimitKind.ConstructionDepth, _ => Utf8ResourceLimitKind.ConstructionWorkSteps }, failure.ResourceKind);
        Assert.Equal(112ul, owner.Budget.PeakBytes);
        Assert.Equal(112ul, owner.Budget.ReservedBytes);
        Assert.Equal(0ul, owner.Budget.Depth);
        Assert.Same(checkpoint, owner.Budget.ObjectCheckpoint);
        Assert.Equal(17, parent.Value);
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)]
    public void ChildDepthDenialRestoresParentAndAllowsRecovery(int route)
    {
        var input = Make(route, 32);
        using var owner = new Utf8ConstructionScope(0, ulong.MaxValue, ulong.MaxValue, 8);
        var parent = owner.Budget.AllocateObject(17, static value => new ParentValue(value));
        var checkpoint = owner.Budget.ObjectCheckpoint;
        var failure = Assert.Throws<Utf8ResourceLimitExceededException>(() => Run(route, input));
        Assert.Equal(Utf8ResourceLimitKind.ConstructionDepth, failure.ResourceKind);
        Assert.Equal(9ul, failure.Requested);
        Assert.Equal(8ul, owner.Budget.PeakDepth);
        Assert.Equal(112ul, owner.Budget.ReservedBytes);
        Assert.Equal(0ul, owner.Budget.Depth);
        Assert.Same(checkpoint, owner.Budget.ObjectCheckpoint);
        var spent = owner.Budget.WorkSteps;
        Run(route, Make(route, 0));
        Assert.True(owner.Budget.WorkSteps > spent);
        Assert.Equal(112ul, owner.Budget.ReservedBytes);
        Assert.Equal(17, parent.Value);
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)]
    public void ArrayGrowthDenialReleasesEveryScratchLoan(int route)
    {
        var input = Make(route, 128);
        using var owner = new Utf8ConstructionScope(0, 2048, ulong.MaxValue, ulong.MaxValue);
        var parent = owner.Budget.AllocateObject(17, static value => new ParentValue(value));
        var checkpoint = owner.Budget.ObjectCheckpoint;
        var failure = Assert.Throws<Utf8ResourceLimitExceededException>(() => Run(route, input));
        Assert.Equal(Utf8ResourceLimitKind.ConstructionBytes, failure.ResourceKind);
        Assert.True(failure.Requested > 2048);
        Assert.InRange(owner.Budget.PeakBytes, 113ul, 2048ul);
        Assert.Equal(112ul, owner.Budget.ReservedBytes);
        Assert.Equal(0ul, owner.Budget.Depth);
        Assert.Same(checkpoint, owner.Budget.ObjectCheckpoint);
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
            var checkpoint = owner.Budget.ObjectCheckpoint;
            var failure = Record.Exception(() => Run(route, input));
            if (cutoff < work)
            {
                var breach = Assert.IsType<Utf8ResourceLimitExceededException>(failure);
                Assert.Equal(Utf8ResourceLimitKind.ConstructionWorkSteps, breach.ResourceKind);
                Assert.True(breach.Requested > cutoff + 2);
            }
            else Assert.Null(failure);
            Assert.Equal(112ul, owner.Budget.ReservedBytes);
            Assert.Equal(0ul, owner.Budget.Depth);
            Assert.Equal(0ul, owner.Budget.Reserved);
            Assert.Same(checkpoint, owner.Budget.ObjectCheckpoint);
            Assert.InRange(owner.Budget.WorkSteps, 2ul, cutoff + 2);
            Assert.Equal(17, parent.Value);
        }
    }

    [Fact]
    public void BorrowedWhitespaceSuffixKeepsGuardOffsetsAndNestedPartialPrefix()
    {
        var root = Node(Utf8ExecutionNodeKind.Concatenate, "", Node(Utf8ExecutionNodeKind.Beginning, ""),
            Node(Utf8ExecutionNodeKind.Loop, "", Node(Utf8ExecutionNodeKind.Group, "", Node(Utf8ExecutionNodeKind.Set, Utf8OwnedCharClass.SpaceClass))),
            Node(Utf8ExecutionNodeKind.Boundary, ""),
            Node(Utf8ExecutionNodeKind.Concatenate, "", Node(Utf8ExecutionNodeKind.Multi, "ab"), Node(Utf8ExecutionNodeKind.Unsupported, "")),
            Node(Utf8ExecutionNodeKind.Multi, "c"));
        var plan = Utf8SearchPlan.Prepare(Utf8SearchFacts.Create(Utf8SearchKind.None,
            new Utf8SearchFactData { FallbackStartTransform = new(0, Utf8FallbackStartTransformKind.TrimLeadingAsciiWhitespace) }));
        using var owner = new Utf8ConstructionScope(0, ulong.MaxValue, ulong.MaxValue, ulong.MaxValue);
        var parent = owner.Budget.AllocateObject(17, static value => new ParentValue(value));
        var checkpoint = owner.Budget.ObjectCheckpoint;
        var slice = Assert.IsType<ValueTuple<Utf8ExecutionNode, int>>(Call(typeof(Utf8DeterministicVerifierGuards), "SkipLeadingAsciiWhitespacePrefix", root));
        Assert.Same(root, slice.Item1);
        Assert.Equal(3, slice.Item2);
        var guards = Utf8DeterministicVerifierGuards.Create(new(root), plan);
        Assert.NotNull(guards.PrefixGuards);
        Assert.Equal(new[] { 0, 1, 2 }, guards.PrefixGuards.Select(static guard => guard.Offset));
        Assert.True(guards.Matches(" \tabc"u8, 0));
        Assert.False(guards.Matches(" \tabx"u8, 0));
        Assert.Equal(112ul, owner.Budget.ReservedBytes);
        Assert.Same(checkpoint, owner.Budget.ObjectCheckpoint);
        Assert.Equal(0ul, owner.Budget.Depth);
        Assert.Equal(17, parent.Value);
    }

    [Fact]
    public void FoldedPayloadAdmitsBytesAfterValidationAndTransfersOnlyItsArrayLoan()
    {
        var anchor = typeof(Utf8DeterministicAnchorSearch);
        using var owner = new Utf8ConstructionScope(0, 112, ulong.MaxValue, ulong.MaxValue);
        var parent = owner.Budget.AllocateObject(17, static value => new ParentValue(value));
        var checkpoint = owner.Budget.ObjectCheckpoint;
        object?[] rejected = [new[] { "abc", "ABC" }, null];
        Assert.False(Assert.IsType<bool>(Call(anchor, "TryCreateAsciiIgnoreCaseAnchorLiteral", rejected)));
        Assert.Empty(Assert.IsType<byte[]>(rejected[1]));
        Assert.Equal(112ul, owner.Budget.PeakBytes);
        object?[] accepted = [new[] { "a000", "A000" }, null];
        var failure = Assert.Throws<Utf8ResourceLimitExceededException>(() => Call(anchor, "TryCreateAsciiIgnoreCaseAnchorLiteral", accepted));
        Assert.Equal(Utf8ResourceLimitKind.ConstructionBytes, failure.ResourceKind);
        Assert.Equal(160ul, failure.Requested);
        Assert.Equal(112ul, owner.Budget.ReservedBytes);
        Assert.Same(checkpoint, owner.Budget.ObjectCheckpoint);
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
        Assert.Equal(4ul * width + 2, owner.Budget.WorkSteps);
        Assert.Equal(0ul, owner.Budget.Depth);
        owner.Budget.ReleaseArray(folded, instructions: false);
        Assert.Equal(0ul, owner.Budget.ReservedBytes);
    }
}
