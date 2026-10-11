using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.FrontEnd;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8VerifierScanAdmissionTests
{
    private sealed class ParentValue(int value) { internal readonly int Value = value; }
    private sealed record Input(Utf8ExecutionNode Root, Utf8ExecutionNode[] Children, byte[][] Alternatives, byte[] Prefix);

    private static Utf8ExecutionNode Node(Utf8ExecutionNodeKind kind, string text, params Utf8ExecutionNode[] children)
        => new(kind, RegexOptions.None, children, text, '\0', 0, 0, 1);

    private static Input Make(int route, int depth)
    {
        var body = Node(Utf8ExecutionNodeKind.Multi, "ab");
        for (var index = 0; index < depth; index++)
            body = Node(Utf8ExecutionNodeKind.Concatenate, "", Node(Utf8ExecutionNodeKind.Group, "", body), Node(Utf8ExecutionNodeKind.Multi, "c"));
        var root = body;
        if (route == 1)
        {
            root = Node(Utf8ExecutionNodeKind.Alternate, "", body, Node(Utf8ExecutionNodeKind.Multi, "xy"));
            for (var index = 0; index < depth; index++) root = Node(Utf8ExecutionNodeKind.Loop, "", root);
        }
        if (route == 2)
        {
            root = Node(Utf8ExecutionNodeKind.Multi, "dog");
            for (var index = 0; index < depth; index++) root = Node(Utf8ExecutionNodeKind.Alternate, "", root, Node(Utf8ExecutionNodeKind.Multi, "egg"));
            root = Node(Utf8ExecutionNodeKind.Alternate, "", Node(Utf8ExecutionNodeKind.Multi, "cat"), root);
        }
        var children = route == 5
            ? Enumerable.Range(0, depth + 1).Select(static _ => Node(Utf8ExecutionNodeKind.Group, "", Node(Utf8ExecutionNodeKind.Beginning, ""))).ToArray()
            : new[] { Node(Utf8ExecutionNodeKind.Beginning, ""), body };
        return new(root, children, ["cat"u8.ToArray(), "dog"u8.ToArray(), "egg"u8.ToArray()], "tail"u8.ToArray());
    }

    private static object? Call(string name, params object?[] args)
    {
        var type = typeof(Utf8ByteSafeLinearVerifierProgram).GetNestedType("Compiler", BindingFlags.NonPublic)
            ?? throw new InvalidOperationException();
        var instance = Activator.CreateInstance(type);
        var method = type.GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException();
        try { return method.Invoke(instance, args); }
        catch (TargetInvocationException failure) when (failure.InnerException is { } inner)
        {
            ExceptionDispatchInfo.Capture(inner).Throw();
            throw;
        }
    }

    private static void Run(int route, Input input)
    {
        // These probes discard successful payloads; close their loan scope
        // before inspecting the measured parent's accounting.
        using var discarded = Utf8ConstructionScope.Enter();
        object?[] args = route switch
        {
            0 => [input.Root, null],
            1 => [input.Root, null, false],
            2 => [input.Root, 0ul, 0ul],
            3 => [input.Children, 0, null],
            4 => [input.Children, 0, 0ul, 0ul],
            5 => [input.Children, 0],
            6 => [input.Alternatives],
            _ => [input.Alternatives, input.Prefix],
        };
        var name = route switch
        {
            0 => "TryExtractFixedAsciiText", 1 => "TryExtractAlternationTexts", 2 => "TryGetLeadingAsciiByteMask",
            3 => "TryExtractFollowingAsciiPrefix", 4 => "TryGetFollowingAsciiByteMask", 5 => "CanTreatRemainingAsZeroWidth",
            6 => "AreAlternativesPrefixFree", _ => "IsSafeOptionalAlternation",
        };
        Assert.True(Assert.IsType<bool>(Call(name, args)));
    }

    public static IEnumerable<object[]> RootDenials()
    {
        for (var route = 0; route < 3; route++)
        for (var denial = 0; denial < 3; denial++) yield return [route, denial];
    }

    [Theory]
    [MemberData(nameof(RootDenials))]
    public void RootDenialPrecedesScratchAndBuilderAcquisition(int route, int denial)
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
    [InlineData(0)] [InlineData(1)] [InlineData(2)]
    public void ChildDepthDenialPreservesParentAndAllowsRecovery(int route)
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
        Assert.Equal(checkpoint, owner.Budget.ReservedBytes);
        Assert.Equal(17, parent.Value);
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)]
    public void ArrayGrowthDenialRestoresEveryAdmittedLoan(int route)
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
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)]
    [InlineData(4)] [InlineData(5)] [InlineData(6)] [InlineData(7)]
    public void EveryWorkCutoffRestoresScratchWithoutRefundingTheCounter(int route)
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
            Assert.Equal(17, parent.Value);
            Assert.InRange(owner.Budget.WorkSteps, 2ul, cutoff + 2);
        }
    }

    [Fact]
    public void CompletedBranchesKeepTextOrderAndDropFailedChildMasks()
    {
        var body = Make(0, 65).Root;
        var optional = Node(Utf8ExecutionNodeKind.Loop, "", Node(Utf8ExecutionNodeKind.Alternate, "", body, Node(Utf8ExecutionNodeKind.Multi, "xy")));
        var inner = Node(Utf8ExecutionNodeKind.Alternate, "", Node(Utf8ExecutionNodeKind.Multi, "dog"), Node(Utf8ExecutionNodeKind.Unsupported, ""));
        var outer = Node(Utf8ExecutionNodeKind.Alternate, "", Node(Utf8ExecutionNodeKind.Multi, "cat"), inner);
        using var owner = new Utf8ConstructionScope(0, ulong.MaxValue, ulong.MaxValue, ulong.MaxValue);
        var parent = owner.Budget.AllocateObject(17, static value => new ParentValue(value));
        var checkpoint = owner.Budget.ReservedBytes;
        using (var discarded = Utf8ConstructionScope.Enter())
        {
            object?[] args = [optional, null, false];
            Assert.True(Assert.IsType<bool>(Call("TryExtractAlternationTexts", args)));
            Assert.True(Assert.IsType<bool>(args[2]));
            Assert.Equal(new[] { "ab" + new string('c', 65), "xy" }, Assert.IsType<byte[][]>(args[1]).Select(Encoding.ASCII.GetString));
            args = [outer, 0ul, 0ul];
            Assert.False(Assert.IsType<bool>(Call("TryGetLeadingAsciiByteMask", args)));
            Assert.Equal(0ul, args[1]);
            Assert.Equal(0ul, args[2]);
            args = [Node(Utf8ExecutionNodeKind.Concatenate, "", body, Node(Utf8ExecutionNodeKind.Unsupported, "")), null];
            Assert.False(Assert.IsType<bool>(Call("TryExtractFixedAsciiText", args)));
            Assert.Empty(Assert.IsType<byte[]>(args[1]));
        }
        Assert.True(owner.Budget.PeakBytes > 2048);
        Assert.Equal(128ul, owner.Budget.ReservedBytes);
        Assert.Equal(0ul, owner.Budget.Depth);
        Assert.Equal(checkpoint, owner.Budget.ReservedBytes);
        Assert.Equal(17, parent.Value);
    }

    [Fact]
    public void SharedAsciiScanMetersCopyAndStopsAtTheFirstNonAsciiCharacter()
    {
        for (var cutoff = 0ul; cutoff <= 6; cutoff++)
        {
            using var owner = new Utf8ConstructionScope(0, ulong.MaxValue, cutoff, ulong.MaxValue);
            var failure = Record.Exception(() => Assert.True(Utf8DeterministicAnchorSearch.TryGetAsciiBytes("abc", out _)));
            // The original six-step controls cannot acquire the now-admitted
            // payload: scan, array acquisition and copy each cost three steps.
            Assert.Equal(Utf8ResourceLimitKind.ConstructionWorkSteps, Assert.IsType<Utf8ResourceLimitExceededException>(failure).ResourceKind);
            Assert.InRange(owner.Budget.WorkSteps, 0ul, cutoff);
            Assert.Equal(0ul, owner.Budget.ReservedBytes);
        }
        for (var cutoff = 7ul; cutoff <= 9; cutoff++)
        {
            using var owner = new Utf8ConstructionScope(0, ulong.MaxValue, cutoff, ulong.MaxValue);
            byte[] payload = [];
            var failure = Record.Exception(() => Assert.True(Utf8DeterministicAnchorSearch.TryGetAsciiBytes("abc", out payload)));
            if (cutoff < 9)
            {
                Assert.Equal(Utf8ResourceLimitKind.ConstructionWorkSteps, Assert.IsType<Utf8ResourceLimitExceededException>(failure).ResourceKind);
                Assert.Equal(0ul, owner.Budget.ReservedBytes);
            }
            else
            {
                Assert.Null(failure);
                Assert.Equal(48ul, owner.Budget.ReservedBytes);
                owner.Budget.ReleaseArray(payload, false);
                Assert.Equal(0ul, owner.Budget.ReservedBytes);
            }
            Assert.InRange(owner.Budget.WorkSteps, 0ul, cutoff);
        }
        using var decline = new Utf8ConstructionScope(0, ulong.MaxValue, 2, ulong.MaxValue);
        Assert.False(Utf8DeterministicAnchorSearch.TryGetAsciiBytes("aéz", out var bytes));
        Assert.Empty(bytes);
        Assert.Equal(2ul, decline.Budget.WorkSteps);
    }

    [Fact]
    public void SharedUnwrapCannotBypassAConstructionWorkLimit()
    {
        var input = Node(Utf8ExecutionNodeKind.Multi, "ab");
        for (var index = 0; index < 65; index++) input = Node(Utf8ExecutionNodeKind.Group, "", input);
        using var owner = new Utf8ConstructionScope(0, ulong.MaxValue, 32, ulong.MaxValue);
        var failure = Assert.Throws<Utf8ResourceLimitExceededException>(() => Utf8DeterministicAnchorSearch.Unwrap(input));
        Assert.Equal(Utf8ResourceLimitKind.ConstructionWorkSteps, failure.ResourceKind);
        Assert.Equal(33ul, failure.Requested);
        Assert.Equal(32ul, owner.Budget.WorkSteps);
        Assert.Equal(0ul, owner.Budget.ReservedBytes);
    }
}
