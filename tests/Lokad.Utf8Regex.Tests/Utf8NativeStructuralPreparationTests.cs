using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.FrontEnd;
using Lokad.Utf8Regex.Internal.Planning;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8NativeStructuralPreparationTests
{
    private sealed class ParentValue(int value) { internal readonly int Value = value; }
    private static Utf8TreeNode Node(Utf8TreeNodeKind kind, params Utf8TreeNode[] children)
    {
        var node = Utf8TreeNode.Create(kind, RegexOptions.CultureInvariant);
        foreach (var child in children) node.AddChild(child);
        return node;
    }
    private static Utf8TreeNode Text(string text) => Utf8TreeNode.Create(Utf8TreeNodeKind.Multi, RegexOptions.CultureInvariant, text);
    private static Utf8TreeNode One(char value) => Utf8TreeNode.Create(Utf8TreeNodeKind.One, RegexOptions.CultureInvariant, value);
    private static Utf8TreeNode Wrap(Utf8TreeNode node, int depth)
    {
        for (var index = 0; index < depth; index++) node = Node(Utf8TreeNodeKind.Capture, node);
        return node;
    }
    private static Utf8ScalarClass Class(string pattern)
    {
        var tree = Utf8OwnedTreeParser.TryParse(pattern, RegexOptions.CultureInvariant);
        Assert.NotNull(tree);
        var node = tree.Root;
        while (node.Kind is Utf8TreeNodeKind.Capture or Utf8TreeNodeKind.Group) node = node.Child(0);
        Assert.NotNull(node.ScalarClass);
        return node.ScalarClass;
    }
    private static Utf8TreeNode SetLoop(string pattern, int min, int max)
    {
        var atom = Utf8TreeNode.Create(Utf8TreeNodeKind.Set, RegexOptions.CultureInvariant, Class(pattern));
        return Utf8TreeNode.CreateSetRepeat(atom, RegexOptions.CultureInvariant, min, max, false);
    }
    private static object? Call(string name, params object?[] args)
    {
        var method = typeof(Utf8NativeExecutionAnalyzer).GetMethods(BindingFlags.Static | BindingFlags.NonPublic)
            .Single(value => value.Name == name && value.GetParameters().Length == args.Length);
        try { return method.Invoke(null, args); }
        catch (TargetInvocationException error) when (error.InnerException is { } inner)
        { ExceptionDispatchInfo.Capture(inner).Throw(); throw; }
    }
    private static Utf8TreeNode Input(int route, int depth)
    {
        var root = route switch
        {
            0 or 1 => Node(Utf8TreeNodeKind.Concatenate, One('\n'), Text("a\n\r")),
            2 => Text("ASCII"),
            3 => Node(Utf8TreeNodeKind.Alternate, Text("ONE"), Text("TWO")),
            4 => Node(Utf8TreeNodeKind.Concatenate, Wrap(One(')'), depth), SetLoop("[ \\t]", 1, int.MaxValue), Text("tail")),
            5 => Node(Utf8TreeNodeKind.Alternate, One('\''), One('"')),
            6 => SetLoop("[ \\t]", 0, int.MaxValue),
            7 => Node(Utf8TreeNodeKind.Concatenate, One('\''),
                Wrap(Node(Utf8TreeNodeKind.Concatenate, Node(Utf8TreeNodeKind.Alternate, Text("AKIA"), Text("ASIA")),
                    SetLoop("[A-Z]", 16, 16)), depth), One('\'')),
            _ => Node(Utf8TreeNodeKind.Concatenate, Node(Utf8TreeNodeKind.Alternate, Text("call"), Text("invoke")),
                SetLoop("[ \\t]", 1, int.MaxValue), Utf8TreeNode.Create(Utf8TreeNodeKind.Set, RegexOptions.CultureInvariant, Class("[A-Z]")),
                SetLoop("[a-z]", 0, int.MaxValue), Text("(")),
        };
        return route is 4 or 7 ? root : Wrap(root, depth);
    }
    private static bool ProducesOutput(int route) => route is 2 or 3 or 4 or 7 or 8;
    private static void Run(int route, Utf8TreeNode root)
    {
        var name = route switch
        {
            0 or 1 => "TryGetFiniteLineBreakCount", 2 => "TryGetAsciiLiteral", 3 => "TryGetAsciiLiteralAlternation",
            4 => "TryExtractAsciiStructuralSuffixParts", 5 => "IsQuoteNode", 6 => "TryGetIdentifierSeparatorLoop",
            7 => "TryExtractQuotedPrefixedAsciiRun", _ => "TryExtractAsciiStructuralIdentifierFamily",
        };
        object?[] args = route switch
        {
            1 or 4 => [root, 0, root.ChildCount - 1, null],
            5 => [root], 6 => [root, null, 0], 7 => [root, null, null, 0],
            8 => [root, null, null, 0, null, null, 0, 0, null, null, Utf8BoundaryRequirement.None, Utf8BoundaryRequirement.None],
            _ => [root, null],
        };
        Assert.True(Assert.IsType<bool>(Call(name, args)));
        switch (route)
        {
            case 0: Assert.Equal(2, Assert.IsType<int>(args[1])); break;
            case 1: Assert.Equal(2, Assert.IsType<int>(args[3])); break;
            case 2: Assert.Equal("ASCII", Encoding.ASCII.GetString(Assert.IsType<byte[]>(args[1]))); break;
            case 3: Assert.Equal(new[] { "ONE", "TWO" }, Assert.IsType<byte[][]>(args[1]).Select(Encoding.ASCII.GetString)); break;
            case 4:
                var parts = Assert.IsType<AsciiStructuralSuffixPart[]>(args[3]);
                Assert.Equal(3, parts.Length);
                Assert.Equal(")", Encoding.ASCII.GetString(parts[0].LiteralUtf8));
                Assert.True(parts[1].IsSeparator); Assert.Equal(1, parts[1].SeparatorMinCount);
                Assert.Equal("tail", Encoding.ASCII.GetString(parts[2].LiteralUtf8)); break;
            case 6:
                var separator = Assert.IsType<AsciiCharClass>(args[1]);
                Assert.Equal(2, separator.Count);
                for (var value = 0; value < 128; value++)
                    Assert.Equal(value is ' ' or '\t', separator.Contains((byte)value));
                Assert.Equal(0, Assert.IsType<int>(args[2])); break;
            case 7: Assert.Equal(new[] { "AKIA", "ASIA" }, Assert.IsType<byte[][]>(args[1]).Select(Encoding.ASCII.GetString)); Assert.Equal(16, Assert.IsType<int>(args[3])); break;
            case 8:
                Assert.Equal(new[] { "call", "invoke" }, Assert.IsType<byte[][]>(args[1]).Select(Encoding.ASCII.GetString));
                Assert.Equal("(", Encoding.ASCII.GetString(Assert.IsType<byte[]>(args[8])));
                Assert.Single(Assert.IsType<AsciiStructuralSuffixPart[]>(args[9])); break;
        }
    }
    public static IEnumerable<object[]> RootDenials()
    {
        for (var route = 0; route < 9; route++)
        for (var denial = 0; denial < 3; denial++) yield return [route, denial];
    }
    [Theory]
    [MemberData(nameof(RootDenials))]
    public void RootDenialPrecedesTraversalAndOutputAllocation(int route, int denial)
    {
        var root = Input(route, 0);
        using var owner = new Utf8ConstructionScope(0, denial == 0 ? 112ul : ulong.MaxValue,
            denial == 2 ? 2ul : ulong.MaxValue, denial == 1 ? 0ul : ulong.MaxValue);
        var parent = owner.Budget.AllocateObject(17, static value => new ParentValue(value));
        var checkpoint = owner.Budget.ObjectCheckpoint;
        var error = Assert.Throws<Utf8ResourceLimitExceededException>(() => Run(route, root));
        Assert.Equal(denial switch { 0 => Utf8ResourceLimitKind.ConstructionBytes, 1 => Utf8ResourceLimitKind.ConstructionDepth,
            _ => Utf8ResourceLimitKind.ConstructionWorkSteps }, error.ResourceKind);
        Assert.Equal(112ul, owner.Budget.PeakBytes); Assert.Equal(112ul, owner.Budget.ReservedBytes);
        Assert.Equal(0ul, owner.Budget.Depth); Assert.Same(checkpoint, owner.Budget.ObjectCheckpoint); Assert.Equal(17, parent.Value);
    }
    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)]
    [InlineData(5)] [InlineData(6)] [InlineData(7)] [InlineData(8)]
    public void WrapperDepthDenialUnwindsAncestorsAndAllowsLaterWork(int route)
    {
        var root = Input(route, 32); var shallow = Input(route, 0);
        using var owner = new Utf8ConstructionScope(0, ulong.MaxValue, ulong.MaxValue, 8);
        var parent = owner.Budget.AllocateObject(17, static value => new ParentValue(value)); var checkpoint = owner.Budget.ObjectCheckpoint;
        var error = Assert.Throws<Utf8ResourceLimitExceededException>(() => Run(route, root));
        Assert.Equal(Utf8ResourceLimitKind.ConstructionDepth, error.ResourceKind); Assert.Equal(9ul, error.Requested);
        Assert.Equal(112ul, owner.Budget.ReservedBytes); Assert.Equal(0ul, owner.Budget.Depth); Assert.Same(checkpoint, owner.Budget.ObjectCheckpoint);
        Run(route, shallow);
        Assert.Equal(ProducesOutput(route), owner.Budget.ReservedBytes > 112); Assert.Equal(0ul, owner.Budget.Depth); Assert.Equal(17, parent.Value);
    }
    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)]
    [InlineData(5)] [InlineData(6)] [InlineData(7)] [InlineData(8)]
    public void EveryWorkCutoffRecoversScratchAndKeepsAcceptedOutputLoans(int route)
    {
        var root = Input(route, 0); ulong work;
        using (var measure = new Utf8ConstructionScope(0, ulong.MaxValue, ulong.MaxValue, ulong.MaxValue))
        { Run(route, root); work = measure.Budget.WorkSteps; }
        Assert.InRange(work, 1ul, 10000ul);
        for (var cutoff = 0ul; cutoff <= work; cutoff++)
        {
            using var owner = new Utf8ConstructionScope(0, ulong.MaxValue, cutoff + 2, ulong.MaxValue);
            var parent = owner.Budget.AllocateObject(17, static value => new ParentValue(value)); var checkpoint = owner.Budget.ObjectCheckpoint;
            var error = Record.Exception(() => Run(route, root));
            if (cutoff < work)
            {
                Assert.Equal(Utf8ResourceLimitKind.ConstructionWorkSteps, Assert.IsType<Utf8ResourceLimitExceededException>(error).ResourceKind);
                Assert.Equal(112ul, owner.Budget.ReservedBytes); Assert.Same(checkpoint, owner.Budget.ObjectCheckpoint);
            }
            else { Assert.Null(error); Assert.Equal(ProducesOutput(route), owner.Budget.ReservedBytes > 112); }
            Assert.Equal(0ul, owner.Budget.Depth); Assert.Equal(0ul, owner.Budget.Reserved);
            Assert.InRange(owner.Budget.WorkSteps, 2ul, cutoff + 2); Assert.Equal(17, parent.Value);
        }
    }
    [Theory]
    [InlineData("\r", true, 0)] [InlineData("\n", true, 1)] [InlineData("é😀\n\r", true, 1)]
    [InlineData(@"[\n]", true, 1)] [InlineData(@"[^\n]", true, 0)]
    [InlineData(@"[a-z-[aeiou]]", true, 0)] [InlineData(@"[\s-[\t]]", true, 1)]
    [InlineData(@"[\s-[\s]]", true, 0)] [InlineData(@"[^\S-[\t]]", true, 1)]
    [InlineData(@"[^\n]*", true, 0)] [InlineData("", true, 0)] [InlineData(@"\n{2}", false, 0)]
    public void NativeLineBoundRetainsItsLfConsumerAndSupportedGrammar(string pattern, bool expected, int count)
    {
        var tree = Utf8OwnedTreeParser.TryParse(pattern, RegexOptions.CultureInvariant); Assert.NotNull(tree);
        using var owner = new Utf8ConstructionScope(0, ulong.MaxValue, ulong.MaxValue, ulong.MaxValue);
        var parent = owner.Budget.AllocateObject(17, static value => new ParentValue(value)); var checkpoint = owner.Budget.ObjectCheckpoint;
        object?[] args = [tree.Root, 0];
        Assert.Equal(expected, Assert.IsType<bool>(Call("TryGetFiniteLineBreakCount", args))); Assert.Equal(count, Assert.IsType<int>(args[1]));
        Assert.Equal(112ul, owner.Budget.ReservedBytes); Assert.Equal(0ul, owner.Budget.Depth); Assert.Same(checkpoint, owner.Budget.ObjectCheckpoint); Assert.Equal(17, parent.Value);
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void CheckedLineBoundOverflowRestoresTheParent(bool repeat)
    {
        var loop = Node(Utf8TreeNodeKind.Loop, Text(repeat ? "\n\n" : "\n")); loop.M = 1; loop.N = int.MaxValue - 1;
        var root = repeat ? loop : Node(Utf8TreeNodeKind.Concatenate, loop, Text("\n\n"));
        using var owner = new Utf8ConstructionScope(0, ulong.MaxValue, ulong.MaxValue, ulong.MaxValue);
        var parent = owner.Budget.AllocateObject(17, static value => new ParentValue(value)); var checkpoint = owner.Budget.ObjectCheckpoint;
        Assert.Throws<OverflowException>(() => Call("TryGetFiniteLineBreakCount", root, 0));
        Assert.Equal(112ul, owner.Budget.ReservedBytes); Assert.Equal(0ul, owner.Budget.Depth); Assert.Same(checkpoint, owner.Budget.ObjectCheckpoint); Assert.Equal(17, parent.Value);
    }
    [Theory]
    [InlineData(2)] [InlineData(3)] [InlineData(4)] [InlineData(7)] [InlineData(8)]
    public void LateDeclineRecoversAllEarlierLiteralAndPrefixLoans(int route)
    {
        var root = route switch
        {
            2 => Text("é😀"), 3 => Node(Utf8TreeNodeKind.Alternate, Text("good"), Text("é")),
            4 => Node(Utf8TreeNodeKind.Concatenate, Text("good"), Node(Utf8TreeNodeKind.Empty)),
            7 => Node(Utf8TreeNodeKind.Concatenate, One('\''), Node(Utf8TreeNodeKind.Concatenate,
                Node(Utf8TreeNodeKind.Alternate, Text("AKIA"), Text("ASIA")), Text("bad-tail")), One('\'')),
            _ => Node(Utf8TreeNodeKind.Concatenate, Node(Utf8TreeNodeKind.Alternate, Text("call"), Text("invoke")), Node(Utf8TreeNodeKind.Empty)),
        };
        using var owner = new Utf8ConstructionScope(0, ulong.MaxValue, ulong.MaxValue, ulong.MaxValue);
        var parent = owner.Budget.AllocateObject(17, static value => new ParentValue(value)); var checkpoint = owner.Budget.ObjectCheckpoint;
        object?[] args = route switch { 4 => [root, 0, 1, null], 7 => [root, null, null, 0],
            8 => [root, null, null, 0, null, null, 0, 0, null, null, Utf8BoundaryRequirement.None, Utf8BoundaryRequirement.None], _ => [root, null] };
        var name = route switch { 2 => "TryGetAsciiLiteral", 3 => "TryGetAsciiLiteralAlternation", 4 => "TryExtractAsciiStructuralSuffixParts",
            7 => "TryExtractQuotedPrefixedAsciiRun", _ => "TryExtractAsciiStructuralIdentifierFamily" };
        Assert.False(Assert.IsType<bool>(Call(name, args)));
        Assert.Equal(112ul, owner.Budget.ReservedBytes); Assert.Equal(0ul, owner.Budget.Depth); Assert.Same(checkpoint, owner.Budget.ObjectCheckpoint); Assert.Equal(17, parent.Value);
    }
    [Theory]
    [InlineData(0)] [InlineData(2)]
    public void BorrowedLiteralScanningCannotEvadeWorkAdmission(int route)
    {
        var root = Text(new string(route == 0 ? '\n' : 'A', 100000));
        using var owner = new Utf8ConstructionScope(0, ulong.MaxValue, 64, ulong.MaxValue);
        var parent = owner.Budget.AllocateObject(17, static value => new ParentValue(value)); var checkpoint = owner.Budget.ObjectCheckpoint;
        var error = Assert.Throws<Utf8ResourceLimitExceededException>(() => Call(route == 0 ? "TryGetFiniteLineBreakCount" : "TryGetAsciiLiteral", root, null));
        Assert.Equal(Utf8ResourceLimitKind.ConstructionWorkSteps, error.ResourceKind); Assert.True(error.Requested > 64);
        Assert.Equal(112ul, owner.Budget.ReservedBytes); Assert.Equal(0ul, owner.Budget.Depth); Assert.Same(checkpoint, owner.Budget.ObjectCheckpoint); Assert.Equal(17, parent.Value);
    }
    [Theory]
    [InlineData(@"[a-z]")] [InlineData(@"[^a]")] [InlineData(@"[\s-[\t]]")] [InlineData(@"[\s-[\s]]")]
    [InlineData(@"[^\S-[\t]]")] [InlineData(@"[\s-[\t-[\r]]]")] [InlineData(@"[^a-[^b]]")] [InlineData(@"[\p{L}-[A-Z]]")]
    public void PreparedClassQueriesAgreeOnAsciiOverlap(string pattern)
    {
        // The remaining construction-query consumer still takes its native
        // projection; structural plan producers above borrow canonical classes.
        var wire = Class(pattern).NativeProjection;
        Assert.NotNull(wire);
        var comparator = new Regex(@"\A(?:" + pattern + @")\z", RegexOptions.CultureInvariant);
        using var owner = new Utf8ConstructionScope(0, ulong.MaxValue, ulong.MaxValue, ulong.MaxValue);
        using var query = Utf8ConstructionClass.Create(owner.Budget, wire);
        for (var value = 0; value < 128; value++) Assert.Equal(comparator.IsMatch(((char)value).ToString()), query.Contains((char)value));
    }

    [Fact]
    public void EverySubtractionWorkCutoffRecoversPartiallyAcquiredSegments()
    {
        var node = Utf8TreeNode.Create(Utf8TreeNodeKind.Set, RegexOptions.CultureInvariant, Class(@"[\s-[\t-[\r]]]"));
        ulong work;
        using (var measure = new Utf8ConstructionScope(0, ulong.MaxValue, ulong.MaxValue, ulong.MaxValue))
        { Assert.True(Assert.IsType<bool>(Call("TryGetFiniteLineBreakCount", node, 0))); work = measure.Budget.WorkSteps; }
        for (var cutoff = 0ul; cutoff < work; cutoff++)
        {
            using var owner = new Utf8ConstructionScope(0, ulong.MaxValue, cutoff + 2, ulong.MaxValue);
            var parent = owner.Budget.AllocateObject(17, static value => new ParentValue(value)); var checkpoint = owner.Budget.ObjectCheckpoint;
            var error = Assert.Throws<Utf8ResourceLimitExceededException>(() => Call("TryGetFiniteLineBreakCount", node, 0));
            Assert.Equal(Utf8ResourceLimitKind.ConstructionWorkSteps, error.ResourceKind);
            Assert.Equal(112ul, owner.Budget.ReservedBytes); Assert.Equal(0ul, owner.Budget.Depth);
            Assert.Same(checkpoint, owner.Budget.ObjectCheckpoint); Assert.Equal(17, parent.Value);
        }
    }

    [Fact]
    public void SubtractionByteDenialAfterSegmentAcquisitionRestoresTheParent()
    {
        var node = Utf8TreeNode.Create(Utf8TreeNodeKind.Set, RegexOptions.CultureInvariant, Class(@"[\s-[\t-[\r]]]"));
        using var owner = new Utf8ConstructionScope(0, 640, ulong.MaxValue, ulong.MaxValue);
        var parent = owner.Budget.AllocateObject(17, static value => new ParentValue(value)); var checkpoint = owner.Budget.ObjectCheckpoint;
        var error = Assert.Throws<Utf8ResourceLimitExceededException>(() => Call("TryGetFiniteLineBreakCount", node, 0));
        Assert.Equal(Utf8ResourceLimitKind.ConstructionBytes, error.ResourceKind); Assert.True(error.Requested > 640);
        Assert.True(owner.Budget.PeakBytes > 432); Assert.Equal(112ul, owner.Budget.ReservedBytes);
        Assert.Equal(0ul, owner.Budget.Depth); Assert.Same(checkpoint, owner.Budget.ObjectCheckpoint); Assert.Equal(17, parent.Value);
    }
    [Theory]
    [InlineData(RegexOptions.CultureInvariant)] [InlineData(RegexOptions.CultureInvariant | RegexOptions.Compiled)]
    [InlineData(RegexOptions.CultureInvariant | RegexOptions.RightToLeft)] [InlineData(RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.RightToLeft)]
    public void StructuralSuffixesKeepOwnedScalarByteOperations(RegexOptions options)
    {
        foreach (var row in new[]
        {
            (Pattern: @"(?:call|invoke)[ \t]+([A-Z][a-z]*)[ \t]*=[ \t]*await", First: "call Name = await", Second: "invoke Other = await"),
            (Pattern: @"(?:using|await)[ \t]+([A-Z][a-z]*)[ \t]*\([ \t]*\)", First: "using Name()", Second: "await Other ( )"),
        })
        {
            var regex = new Utf8Regex(row.Pattern, options);
            var property = typeof(Utf8Regex).GetProperty("_verifierRuntime", BindingFlags.NonPublic | BindingFlags.Instance); Assert.NotNull(property);
            var runtime = Assert.IsType<Utf8VerifierRuntime>(property.GetValue(regex));
            Utf8OwnedVerifierAssertions.RetainsPreparedOwner(runtime);
            var input = Encoding.UTF8.GetBytes("😀" + row.First + "中" + row.Second + "😀");
            Assert.True(regex.IsMatch(input)); Assert.Equal(2, regex.Count(input));
            var rtl = (options & RegexOptions.RightToLeft) != 0; var match = regex.Match(input);
            Assert.True(match.Success); Assert.Equal(rtl ? 7 + row.First.Length : 4, match.IndexInBytes);
            Assert.Equal(rtl ? row.Second.Length : row.First.Length, match.LengthInBytes);
            var detailed = regex.MatchDetailed(input); Assert.Equal(match.IndexInBytes, detailed.IndexInBytes);
            Assert.Equal(rtl ? "Other" : "Name", detailed.GetGroup(1).GetValueString());
            var count = 0; foreach (var item in regex.EnumerateMatches(input)) { Assert.True(item.LengthInBytes > 0); count++; }
            Assert.Equal(2, count); Assert.Equal("😀X中X😀", regex.ReplaceToString(input, "X"));
            Assert.True(regex.Inspection.PreparedRegex.ScalarProgram is not null); Assert.True(ReferenceEquals(regex.Inspection.PreparedRegex.SemanticRegex.RuntimeTree!.CaptureCatalog, regex.Inspection.PreparedRegex.ScalarProgram!.CaptureCatalog));
        }
    }
}
