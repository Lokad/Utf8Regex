using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.FrontEnd;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8NativeLiteralPreparationTests
{
    private sealed class ParentValue(int value) { internal readonly int Value = value; }
    private static Utf8TreeNode Node(Utf8TreeNodeKind kind, string text, params Utf8TreeNode[] children)
    {
        var node = new Utf8TreeNode(kind, RegexOptions.CultureInvariant, text);
        foreach (var child in children) node.AddChild(child);
        return node;
    }
    private static Utf8TreeNode One(char value) => new(Utf8TreeNodeKind.One, RegexOptions.CultureInvariant, value);
    private static string Class(string pattern)
    {
        var tree = Utf8OwnedTreeParser.TryParse(pattern, RegexOptions.CultureInvariant);
        Assert.NotNull(tree);
        var node = tree.Root;
        while (node.Kind is Utf8TreeNodeKind.Capture or Utf8TreeNodeKind.Group) node = node.Child(0);
        Assert.NotNull(node.Str);
        return node.Str;
    }
    private static object? Call(string name, params object?[] args)
    {
        var method = typeof(Utf8NativeExecutionAnalyzer).GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(method);
        try { return method.Invoke(null, args); }
        catch (TargetInvocationException error) when (error.InnerException is { } inner)
        {
            ExceptionDispatchInfo.Capture(inner).Throw(); throw;
        }
    }
    private static Utf8TreeNode Input(int route, int depth)
    {
        var root = route switch
        {
            2 => Node(Utf8TreeNodeKind.Concatenate, "", One('A'), Node(Utf8TreeNodeKind.Multi, "B")),
            4 => One('x'),
            _ => Node(Utf8TreeNodeKind.Concatenate, "", Node(Utf8TreeNodeKind.Multi, "A"),
                Node(Utf8TreeNodeKind.Alternate, "", Node(Utf8TreeNodeKind.Multi, "B"), Node(Utf8TreeNodeKind.Multi, "C")), One('D')),
        };
        for (var index = 0; index < depth; index++) root = Node(Utf8TreeNodeKind.Capture, "", root);
        return root;
    }
    private static void Run(int route, Utf8TreeNode root)
    {
        var name = route switch { 0 => "TryExtractLiteralFamilyStrings", 1 => "TryExtractInvariantIgnoreCaseAsciiLiteralFamilyStrings",
            2 => "TryGetInvariantIgnoreCaseAsciiLiteralText", 3 => "TryExtractLiteralFamily", _ => "TryGetLiteralText" };
        object?[] args = [root, null];
        Assert.True(Assert.IsType<bool>(Call(name, args)));
        switch (route)
        {
            case 0: Assert.Equal(new[] { "ABD", "ACD" }, Assert.IsType<string[]>(args[1])); break;
            case 1: Assert.Equal(new[] { "abd", "acd" }, Assert.IsType<string[]>(args[1])); break;
            case 2: Assert.Equal("ab", Assert.IsType<string>(args[1])); break;
            case 3: Assert.Equal(new[] { "ABD", "ACD" }, Assert.IsType<byte[][]>(args[1]).Select(Encoding.ASCII.GetString)); break;
            default: Assert.Equal("x", Assert.IsType<string>(args[1])); break;
        }
    }

    public static IEnumerable<object[]> RootDenials()
    {
        for (var route = 0; route < 5; route++)
        for (var denial = 0; denial < 3; denial++) yield return [route, denial];
    }
    [Theory]
    [MemberData(nameof(RootDenials))]
    public void RootDenialPrecedesResultOrTraversalAcquisition(int route, int denial)
    {
        var root = Input(route, 0);
        using var owner = new Utf8ConstructionScope(0, denial == 0 ? 112ul : ulong.MaxValue,
            denial == 2 ? 2ul : ulong.MaxValue, denial == 1 ? 0ul : ulong.MaxValue);
        var parent = owner.Budget.AllocateObject(17, static n => new ParentValue(n));
        var checkpoint = owner.Budget.ObjectCheckpoint;
        var error = Assert.Throws<Utf8ResourceLimitExceededException>(() => Run(route, root));
        Assert.Equal(denial switch { 0 => Utf8ResourceLimitKind.ConstructionBytes, 1 => Utf8ResourceLimitKind.ConstructionDepth,
            _ => Utf8ResourceLimitKind.ConstructionWorkSteps }, error.ResourceKind);
        Assert.Equal(112ul, owner.Budget.PeakBytes);
        Assert.Equal(112ul, owner.Budget.ReservedBytes);
        Assert.Equal(0ul, owner.Budget.Depth);
        Assert.Same(checkpoint, owner.Budget.ObjectCheckpoint);
        Assert.Equal(17, parent.Value);
    }
    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)]
    public void ChildDepthDenialKeepsParentAndAllowsLaterConstruction(int route)
    {
        var root = Input(route, 32);
        using var owner = new Utf8ConstructionScope(0, ulong.MaxValue, ulong.MaxValue, 8);
        var parent = owner.Budget.AllocateObject(17, static n => new ParentValue(n));
        var checkpoint = owner.Budget.ObjectCheckpoint;
        var error = Assert.Throws<Utf8ResourceLimitExceededException>(() => Run(route, root));
        Assert.Equal(Utf8ResourceLimitKind.ConstructionDepth, error.ResourceKind);
        Assert.Equal(9ul, error.Requested);
        Assert.Equal(112ul, owner.Budget.ReservedBytes);
        Assert.Equal(0ul, owner.Budget.Depth);
        Assert.Same(checkpoint, owner.Budget.ObjectCheckpoint);
        Run(route, Input(route, 0));
        Assert.True(owner.Budget.ReservedBytes > 112);
        Assert.Equal(0ul, owner.Budget.Depth);
        Assert.Equal(17, parent.Value);
    }
    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)]
    public void EveryWorkCutoffBalancesScratchAndRetainsOnlySuccessfulResults(int route)
    {
        var root = Input(route, 0); ulong work;
        using (var measure = new Utf8ConstructionScope(0, ulong.MaxValue, ulong.MaxValue, ulong.MaxValue))
        { Run(route, root); work = measure.Budget.WorkSteps; }
        Assert.InRange(work, 1ul, 10000ul);
        for (var cutoff = 0ul; cutoff <= work; cutoff++)
        {
            using var owner = new Utf8ConstructionScope(0, ulong.MaxValue, cutoff + 2, ulong.MaxValue);
            var parent = owner.Budget.AllocateObject(17, static n => new ParentValue(n));
            var checkpoint = owner.Budget.ObjectCheckpoint;
            var failure = Record.Exception(() => Run(route, root));
            if (cutoff < work)
            {
                var error = Assert.IsType<Utf8ResourceLimitExceededException>(failure);
                Assert.Equal(Utf8ResourceLimitKind.ConstructionWorkSteps, error.ResourceKind);
                Assert.True(error.Requested > cutoff + 2);
                Assert.Equal(112ul, owner.Budget.ReservedBytes);
                Assert.Same(checkpoint, owner.Budget.ObjectCheckpoint);
            }
            else { Assert.Null(failure); Assert.True(owner.Budget.ReservedBytes > 112); }
            Assert.Equal(0ul, owner.Budget.Depth);
            Assert.Equal(0ul, owner.Budget.Reserved);
            Assert.InRange(owner.Budget.WorkSteps, 2ul, cutoff + 2);
            Assert.Equal(17, parent.Value);
        }
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void FailedCartesianProductRestoresAllOwnedStringsAndArrays(bool folded)
    {
        var root = Node(Utf8TreeNodeKind.Concatenate, "",
            Node(Utf8TreeNodeKind.Alternate, "", One('a'), One('b')),
            Node(Utf8TreeNodeKind.Alternate, "", One('c'), One('d')));
        for (var index = 0; index < 8; index++) root.AddChild(Node(Utf8TreeNodeKind.Alternate, "", One('e'), One('f')));
        using var owner = new Utf8ConstructionScope(0, ulong.MaxValue, ulong.MaxValue, ulong.MaxValue);
        var parent = owner.Budget.AllocateObject(17, static n => new ParentValue(n));
        var checkpoint = owner.Budget.ObjectCheckpoint;
        object?[] args = [root, null];
        Assert.False(Assert.IsType<bool>(Call(folded ? "TryExtractInvariantIgnoreCaseAsciiLiteralFamilyStrings" : "TryExtractLiteralFamilyStrings", args)));
        Assert.Empty(Assert.IsType<string[]>(args[1]));
        Assert.Equal(112ul, owner.Budget.ReservedBytes);
        Assert.Equal(0ul, owner.Budget.Depth);
        Assert.Same(checkpoint, owner.Budget.ObjectCheckpoint);
        Assert.True(owner.Budget.WorkSteps > 2);
        Assert.Equal(17, parent.Value);
    }
    [Fact]
    public void NonAsciiProjectionDeclineRestoresItsSuccessfulStringFamily()
    {
        var root = Node(Utf8TreeNodeKind.Multi, "é😀");
        using var owner = new Utf8ConstructionScope(0, ulong.MaxValue, ulong.MaxValue, ulong.MaxValue);
        var parent = owner.Budget.AllocateObject(17, static n => new ParentValue(n));
        var checkpoint = owner.Budget.ObjectCheckpoint;
        object?[] args = [root, null];
        Assert.False(Assert.IsType<bool>(Call("TryExtractLiteralFamily", args)));
        Assert.Empty(Assert.IsType<byte[][]>(args[1]));
        Assert.Equal(112ul, owner.Budget.ReservedBytes);
        Assert.Equal(0ul, owner.Budget.Depth);
        Assert.Same(checkpoint, owner.Budget.ObjectCheckpoint);
        Assert.Equal(17, parent.Value);
    }
    [Theory]
    [InlineData("[aA]", true, (byte)'a')]
    [InlineData("[^a]", false, (byte)0)]
    [InlineData(@"[^\n]", false, (byte)0)]
    [InlineData("[aA-[a]]", false, (byte)0)]
    [InlineData("[a-z]", false, (byte)0)]
    public void FoldedLiteralProofIncludesWholeClassNegationAndSubtraction(string pattern, bool expected, byte value)
    {
        var wire = Class(pattern);
        using var owner = new Utf8ConstructionScope(0, ulong.MaxValue, ulong.MaxValue, ulong.MaxValue);
        object?[] args = [wire, (byte)0];
        Assert.Equal(expected, Assert.IsType<bool>(Call("TryGetInvariantIgnoreCaseAsciiSetLiteral", args)));
        Assert.Equal(value, Assert.IsType<byte>(args[1]));
        Assert.Equal(0ul, owner.Budget.ReservedBytes);
        Assert.Equal(0ul, owner.Budget.Depth);
    }
    [Theory]
    [InlineData(@"[\s-[A]]", true)]
    [InlineData(@"[\s-[\t]]", true)]
    [InlineData(@"[\s-[\s]]", false)]
    [InlineData(@"[a-z-[aeiou]]", false)]
    [InlineData(@"[^\S]", true)]
    [InlineData(@"[^\S-[\t]]", true)]
    [InlineData(@"[\s-[\t-[\r]]]", true)]
    public void WhitespaceSubtractionUsesAdmittedSegmentsAndRecoversTheirLoans(string pattern, bool expected)
    {
        var wire = Class(pattern);
        // ASCII membership is an overlapping comparator, not a UTF-16 subject contract.
        var comparator = new Regex(@"\A(?:" + pattern + @")\z", RegexOptions.CultureInvariant);
        var any = false;
        var onlyWhitespace = true;
        for (var value = 0; value < 128; value++)
        {
            if (!comparator.IsMatch(((char)value).ToString())) continue;
            any = true;
            onlyWhitespace &= value is ' ' or '\t' or '\r' or '\n' or '\f' or '\v';
        }
        Assert.Equal(expected, any && onlyWhitespace);
        using var owner = new Utf8ConstructionScope(0, ulong.MaxValue, ulong.MaxValue, ulong.MaxValue);
        var parent = owner.Budget.AllocateObject(17, static n => new ParentValue(n));
        var checkpoint = owner.Budget.ObjectCheckpoint;
        Assert.Equal(expected, Assert.IsType<bool>(Call("IsAsciiWhitespaceOnlySet", wire)));
        Assert.Equal(112ul, owner.Budget.ReservedBytes);
        Assert.Equal(0ul, owner.Budget.Depth);
        Assert.Same(checkpoint, owner.Budget.ObjectCheckpoint);
        Assert.True(owner.Budget.WorkSteps > 2);
        Assert.Equal(17, parent.Value);
    }
    [Fact]
    public void LongBorrowedFoldedTextCannotEvadeSharedWorkAdmission()
    {
        var node = Node(Utf8TreeNodeKind.Multi, new string('A', 100000));
        using var owner = new Utf8ConstructionScope(0, ulong.MaxValue, 64, ulong.MaxValue);
        var parent = owner.Budget.AllocateObject(17, static n => new ParentValue(n));
        var checkpoint = owner.Budget.ObjectCheckpoint;
        var error = Assert.Throws<Utf8ResourceLimitExceededException>(() => Call("TryGetInvariantIgnoreCaseAsciiLiteralText", node, ""));
        Assert.Equal(Utf8ResourceLimitKind.ConstructionWorkSteps, error.ResourceKind);
        Assert.Equal(112ul, owner.Budget.ReservedBytes);
        Assert.Equal(0ul, owner.Budget.Depth);
        Assert.Same(checkpoint, owner.Budget.ObjectCheckpoint);
        Assert.Throws<Utf8ResourceLimitExceededException>(() => Call("TryGetInvariantIgnoreCaseAsciiLiteralText", node, ""));
        Assert.Equal(17, parent.Value);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void NestedAlternatesKeepCartesianOrderAndDeclineMultiValuedBranches(bool folded)
    {
        var root = Node(Utf8TreeNodeKind.Concatenate, "",
            Node(Utf8TreeNodeKind.Alternate, "", One('A'), One('B')),
            Node(Utf8TreeNodeKind.Alternate, "", One('C'), One('D')));
        using var owner = new Utf8ConstructionScope(0, ulong.MaxValue, ulong.MaxValue, ulong.MaxValue);
        object?[] args = [root, null];
        var method = folded ? "TryExtractInvariantIgnoreCaseAsciiLiteralFamilyStrings" : "TryExtractLiteralFamilyStrings";
        Assert.True(Assert.IsType<bool>(Call(method, args)));
        Assert.Equal(folded ? new[] { "ac", "ad", "bc", "bd" } : new[] { "AC", "AD", "BC", "BD" },
            Assert.IsType<string[]>(args[1]));
        var checkpoint = owner.Budget.ObjectCheckpoint;
        var held = owner.Budget.ReservedBytes;
        args = [Node(Utf8TreeNodeKind.Alternate, "", root, One('x')), null];
        Assert.False(Assert.IsType<bool>(Call(method, args)));
        Assert.Empty(Assert.IsType<string[]>(args[1]));
        Assert.Equal(held, owner.Budget.ReservedBytes);
        Assert.Same(checkpoint, owner.Budget.ObjectCheckpoint);
        Assert.Equal(0ul, owner.Budget.Depth);
    }

    [Theory]
    [InlineData(RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    [InlineData(RegexOptions.CultureInvariant | RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    [InlineData(RegexOptions.CultureInvariant | RegexOptions.IgnoreCase | RegexOptions.RightToLeft)]
    [InlineData(RegexOptions.CultureInvariant | RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.RightToLeft)]
    public void NegatedAndSubtractedClassesKeepOwnedScalarOperations(RegexOptions options)
    {
        foreach (var pattern in new[] { "([^a])", "([^a])(?=!)", "([aA-[a]])" })
        {
            var regex = new Utf8Regex(pattern, options);
            var property = typeof(Utf8Regex).GetProperty("_verifierRuntime", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(property);
            var runtime = Assert.IsType<Utf8VerifierRuntime>(property.GetValue(regex));
            foreach (var name in new[] { "_fallbackRegex", "_anchoredFallbackRegex" })
            {
                var field = typeof(Utf8FallbackCandidateVerifier).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance);
                Assert.NotNull(field);
                field.SetValue(runtime.FallbackCandidateVerifier, new Lazy<Regex>(() =>
                    throw new InvalidOperationException("Class matching accessed BCL fallback.")));
            }
            var subtracted = pattern == "([aA-[a]])";
            // Folding [aA] before subtracting folded [a] yields an empty class.
            var input = Encoding.UTF8.GetBytes(subtracted ? "Aa" : "A😀!");
            var expected = subtracted ? 0 : pattern.Contains("(?=", StringComparison.Ordinal) ? 1 : 2;
            Assert.Equal(expected != 0, regex.IsMatch(input));
            Assert.Equal(expected, regex.Count(input));
            var match = regex.Match(input);
            Assert.Equal(expected != 0, match.Success);
            var detailed = regex.MatchDetailed(input);
            Assert.Equal(match.Success, detailed.Success);
            if (expected != 0)
            {
                var last = expected == 2 && (options & RegexOptions.RightToLeft) != 0;
                Assert.Equal(last ? 5 : 1, match.IndexInBytes);
                Assert.Equal(last ? 1 : 4, match.LengthInBytes);
                Assert.Equal(match.IndexInBytes, detailed.GetGroup(1).IndexInBytes);
                Assert.Equal(match.LengthInBytes, detailed.GetGroup(1).LengthInBytes);
            }
            var count = 0;
            foreach (var item in regex.EnumerateMatches(input)) { Assert.True(item.LengthInBytes is 1 or 4); count++; }
            Assert.Equal(expected, count);
            Assert.Equal(subtracted ? "Aa" : expected == 1 ? "AX!" : "AXX", regex.ReplaceToString(input, "X"));
            Assert.False(regex.Inspection.DebugFallbackRegexIsCreated);
            Assert.False(regex.Inspection.DebugAnchoredFallbackRegexIsCreated);
        }
    }
}
