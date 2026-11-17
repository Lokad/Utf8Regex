using System.Globalization;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.FrontEnd;
using Lokad.Utf8Regex.Internal.Planning;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8StructuralPlanAdmissionTests
{
    private sealed class ParentValue(int value) { internal readonly int Value = value; }
    private static Utf8TreeNode Node(Utf8TreeNodeKind kind, params Utf8TreeNode[] children)
    {
        var node = Utf8TreeNode.Create(kind, RegexOptions.CultureInvariant);
        foreach (var child in children) node.WithChild(child);
        return node;
    }
    private static Utf8TreeNode Text(string text) => Utf8TreeNode.Create(Utf8TreeNodeKind.Multi, RegexOptions.CultureInvariant, text);
    private static Utf8ScalarClass Class(string pattern)
    {
        var node = Utf8OwnedTreeParser.TryParse(pattern, RegexOptions.CultureInvariant)!.Root;
        while (node.Kind is Utf8TreeNodeKind.Capture or Utf8TreeNodeKind.Group) node = node.Child(0);
        return node.ScalarClass!;
    }
    private static Utf8TreeNode Set(string pattern, int min, int max)
    {
        var atom = Utf8TreeNode.Create(Utf8TreeNodeKind.Set, RegexOptions.CultureInvariant, Class(pattern));
        return Utf8TreeNode.CreateSetRepeat(atom, RegexOptions.CultureInvariant, min, max, false);
    }
    private static AsciiCharClass Mask(string pattern)
    {
        Assert.True(Utf8ScalarAsciiProjector.TryProjectStructuralClass(Class(pattern), out var mask));
        return mask;
    }
    private static Utf8TreeNode Identifier() => Node(Utf8TreeNodeKind.Concatenate,
        Node(Utf8TreeNodeKind.Alternate, Text("call"), Text("invoke")), Set("[ \\t]", 1, int.MaxValue),
        Utf8TreeNode.Create(Utf8TreeNodeKind.Set, RegexOptions.CultureInvariant, Class("[A-Z]")), Set("[a-z]", 0, int.MaxValue), Text("("));
    private static Utf8TreeNode Ordered(string first = "call", string last = "Result", int gap = 3)
        => Node(Utf8TreeNodeKind.Concatenate, Text(first), Set("[\\s\\S]", 0, gap), Text(last));
    private static Utf8TreeNode Body(int route) => route switch
    {
        6 => Ordered(),
        7 => Node(Utf8TreeNodeKind.Alternate, Ordered("call", "Result"), Ordered("invoke", "Other")),
        8 => Node(Utf8TreeNodeKind.Concatenate, Text("call"), Node(Utf8TreeNodeKind.PositiveLookaround,
            Node(Utf8TreeNodeKind.Concatenate, Set("[ \\t]", 1, int.MaxValue), Text("Result")))),
        _ => throw new ArgumentOutOfRangeException(nameof(route)),
    };
    private static Utf8SemanticRegex Semantic(Utf8TreeNode body)
        => new("fixture", RegexOptions.CultureInvariant, "fixture", RegexOptions.CultureInvariant,
            Utf8OwnedRegexTree.Create(Node(Utf8TreeNodeKind.Capture, body), 1, null, null, null, RegexOptions.CultureInvariant, CultureInfo.InvariantCulture, null, false, false));
    private static object Input(int route)
    {
        if (route <= 5)
        {
            var suffix = new[] { AsciiStructuralSuffixPart.CreateLiteral("("u8.ToArray()),
                AsciiStructuralSuffixPart.CreateSeparator(Mask("[ \\t]"), 1) };
            var steps = new[] { AsciiStructuralVerifierStep.MatchSuffixAtCurrent(suffix), AsciiStructuralVerifierStep.Accept() };
            return route switch
            {
                0 => new object[] { new[] { "call"u8.ToArray(), "invoke"u8.ToArray() }, Mask("[ \\t]"), Mask("[A-Z]"), Mask("[a-z]"), suffix },
                1 => Node(Utf8TreeNodeKind.Concatenate, Text("("), Set("[ \t]", 1, int.MaxValue)),
                2 => new AsciiStructuralIdentifierFamilyPlan(["call"u8.ToArray(), "invoke"u8.ToArray()], null, 0,
                    null, null, 0, 0, null, suffix, Utf8BoundaryRequirement.None, Utf8BoundaryRequirement.None),
                3 or 4 => steps,
                _ => new object[] { "call"u8.ToArray(), "Result"u8.ToArray() },
            };
        }
        if (route <= 8) return Body(route);
        return Semantic(route == 12 ? Identifier() : Body(7));
    }
    private static object? Call(string name, params object?[] args)
    {
        var method = typeof(Utf8NativeExecutionAnalyzer).GetMethods(BindingFlags.Static | BindingFlags.NonPublic)
            .Single(value => value.Name == name && value.GetParameters().Length == args.Length);
        try { return method.Invoke(null, args); }
        catch (TargetInvocationException error) when (error.InnerException is { } inner)
        { ExceptionDispatchInfo.Capture(inner).Throw(); throw; }
    }
    private static void Run(int route, object input)
    {
        if (route <= 5)
        {
            switch (route)
            {
                case 0:
                    var fields = (object[])input;
                    var plan = new AsciiStructuralIdentifierFamilyPlan((byte[][])fields[0], (AsciiCharClass)fields[1], 1,
                        (AsciiCharClass)fields[2], (AsciiCharClass)fields[3], 0, 9, null, (AsciiStructuralSuffixPart[])fields[4],
                        Utf8BoundaryRequirement.None, Utf8BoundaryRequirement.Boundary);
                    Assert.Equal(6, plan.VerifierProgram.Steps.Length); Assert.Equal(6, plan.VerifierProgram.LinearProgram.Instructions.Length);
                    Assert.Same(fields[0], plan.Prefixes); Assert.Same(fields[4], plan.SuffixParts); break;
                case 1:
                    var parts = new object?[] { (Utf8TreeNode)input, 0, 1, null };
                    Assert.True((bool)Call("TryExtractAsciiStructuralSuffixParts", parts)!);
                    Assert.Equal(2, ((AsciiStructuralSuffixPart[])parts[3]!).Length); break;
                case 2:
                    var original = (AsciiStructuralIdentifierFamilyPlan)input;
                    Assert.Equal(2, AsciiStructuralVerifierProgram.Create(original).Steps.Length); break;
                case 3 or 4:
                    var steps = (AsciiStructuralVerifierStep[])input;
                    var linear = route == 3 ? new AsciiStructuralVerifierProgram(steps).LinearProgram
                        : AsciiStructuralLinearVerifierProgram.Create(steps);
                    Assert.True(linear.TryMatch("( "u8, 0, 0, out var length)); Assert.Equal(2, length); break;
                default:
                    var bytes = (object[])input;
                    var window = new AsciiOrderedLiteralWindowPlan((byte[])bytes[0], null, (byte[])bytes[1], null,
                        3, 0, true, default, null, false, default, default, default, default);
                    Assert.Single(window.TrailingSearchers); Assert.Same(bytes[1], window.TrailingLiteralUtf8); break;
            }
            return;
        }
        var name = route switch
        {
            6 => "TryExtractOrderedLiteralWindow", 7 => "TryExtractSymmetricAlternateOrderedLiteralWindow",
            8 => "TryExtractOrderedLiteralLookaheadWindow",             12 => "TryAnalyzeAsciiStructuralIdentifierFamilyTree", 13 => "TryAnalyzeAsciiOrderedLiteralWindowTree",
            _ => throw new ArgumentOutOfRangeException(nameof(route)),
        };
        object?[] args = route <= 11 ? [input, null] : [input, "fixture", RegexOptions.CultureInvariant, null];
        Assert.True(Assert.IsType<bool>(Call(name, args)));
    }
    public static IEnumerable<object[]> Routes() => Enumerable.Range(0, 17).Where(route => route <= 8 || route is 12 or 13).Select(route => new object[] { route });
    public static IEnumerable<object[]> Denials()
        => from route in Enumerable.Range(0, 17).Where(route => route <= 8 || route is 12 or 13) from denial in Enumerable.Range(0, 3) select new object[] { route, denial };
    [Theory]
    [MemberData(nameof(Denials))]
    public void RootDenialRecoversParentBeforeAcquisition(int route, int denial)
    {
        var input = Input(route);
        using var owner = new Utf8ConstructionScope(0, denial == 0 ? 128ul : ulong.MaxValue,
            denial == 2 ? 2ul : ulong.MaxValue, denial == 1 ? 0ul : ulong.MaxValue);
        var parent = owner.Budget.AllocateObject(17, static value => new ParentValue(value)); var checkpoint = owner.Budget.ReservedBytes;
        var error = Assert.Throws<Utf8ResourceLimitExceededException>(() => Run(route, input));
        Assert.Equal(denial switch { 0 => Utf8ResourceLimitKind.ConstructionBytes, 1 => Utf8ResourceLimitKind.ConstructionDepth,
            _ => Utf8ResourceLimitKind.ConstructionWorkSteps }, error.ResourceKind);
        Assert.Equal(128ul, owner.Budget.ReservedBytes); Assert.Equal(128ul, owner.Budget.PeakBytes);
        Assert.Equal(0ul, owner.Budget.Depth); Assert.Equal(checkpoint, owner.Budget.ReservedBytes); Assert.Equal(17, parent.Value);
    }
    [Theory]
    [MemberData(nameof(Routes))]
    public void EveryWorkCutoffRecoversParentAndAcceptedPayloadsRetainLoans(int route)
    {
        var input = Input(route); ulong work;
        using (var measure = new Utf8ConstructionScope(0, ulong.MaxValue, ulong.MaxValue, ulong.MaxValue))
        { Run(route, input); work = measure.Budget.WorkSteps; }
        Assert.InRange(work, 1ul, 100000ul);
        for (var cutoff = 0ul; cutoff <= work; cutoff++)
        {
            using var owner = new Utf8ConstructionScope(0, ulong.MaxValue, cutoff + 2, ulong.MaxValue);
            var parent = owner.Budget.AllocateObject(17, static value => new ParentValue(value)); var checkpoint = owner.Budget.ReservedBytes;
            var error = Record.Exception(() => Run(route, input));
            if (cutoff < work)
            {
                Assert.Equal(Utf8ResourceLimitKind.ConstructionWorkSteps, Assert.IsType<Utf8ResourceLimitExceededException>(error).ResourceKind);
                Assert.Equal(128ul, owner.Budget.ReservedBytes); Assert.Equal(checkpoint, owner.Budget.ReservedBytes);
            }
            else { Assert.Null(error); Assert.True(owner.Budget.ReservedBytes > 128); }
            Assert.Equal(0ul, owner.Budget.Depth); Assert.InRange(owner.Budget.WorkSteps, 2ul, cutoff + 2); Assert.Equal(17, parent.Value);
        }
    }
    [Theory]
    [InlineData(6)] [InlineData(7)] [InlineData(8)]
    public void WrapperDepthDenialPreservesBorrowedParentsAndUnwindsLoans(int route)
    {
        var root = Body(route); var wrapped = root;
        for (var depth = 0; depth < 32; depth++) wrapped = Node(Utf8TreeNodeKind.Capture, wrapped);
        var expectedParent = root.Parent;
        using var owner = new Utf8ConstructionScope(0, ulong.MaxValue, ulong.MaxValue, 8);
        Assert.Equal(Utf8ResourceLimitKind.ConstructionDepth,
            Assert.Throws<Utf8ResourceLimitExceededException>(() => Run(route, wrapped)).ResourceKind);
        Assert.Equal(0ul, owner.Budget.ReservedBytes); Assert.Equal(0ul, owner.Budget.Depth); Assert.Same(expectedParent, root.Parent);
    }
    [Theory]
    [InlineData(0)] [InlineData(3)] [InlineData(5)] [InlineData(7)]
    public void EveryByteCutoffAdmitsActualExtentAndRecoversPartialPayloads(int route)
    {
        var input = Input(route); ulong peak;
        using (var measure = new Utf8ConstructionScope(0, ulong.MaxValue, ulong.MaxValue, ulong.MaxValue))
        { Run(route, input); peak = measure.Budget.PeakBytes; }
        Assert.InRange(peak, 1ul, 10000ul); var failures = 0; var successes = 0;
        for (var allowance = 0ul; allowance <= peak; allowance++)
        {
            using var owner = new Utf8ConstructionScope(0, 128 + allowance, ulong.MaxValue, ulong.MaxValue);
            var parent = owner.Budget.AllocateObject(17, static value => new ParentValue(value)); var checkpoint = owner.Budget.ReservedBytes;
            var error = Record.Exception(() => Run(route, input));
            if (error is null) { successes++; Assert.True(owner.Budget.ReservedBytes > 128); }
            else
            {
                failures++; var limit = Assert.IsType<Utf8ResourceLimitExceededException>(error);
                Assert.Equal(Utf8ResourceLimitKind.ConstructionBytes, limit.ResourceKind); Assert.True(limit.Requested > 128 + allowance);
                Assert.Equal(128ul, owner.Budget.ReservedBytes); Assert.Equal(checkpoint, owner.Budget.ReservedBytes);
            }
            Assert.InRange(owner.Budget.PeakBytes, 128ul, 128 + allowance); Assert.Equal(0ul, owner.Budget.Depth); Assert.Equal(17, parent.Value);
        }
        Assert.True(failures > 0); Assert.True(successes > 0);
    }
    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)]
    public void LateDeclineRestoresSuccessfulChildren(int family)
    {
        var root = family switch
        {
            0 => Node(Utf8TreeNodeKind.Concatenate, Text("call"), Set("[\\s\\S]", 0, 3), Text("Result"), Node(Utf8TreeNodeKind.Empty)),
            1 => Node(Utf8TreeNodeKind.Alternate, Ordered(), Ordered("invoke", "Other", 4)),
            2 => Node(Utf8TreeNodeKind.Concatenate, Text("call"), Node(Utf8TreeNodeKind.PositiveLookaround,
                Node(Utf8TreeNodeKind.Concatenate, Set("[ \\t]", 0, int.MaxValue), Text("Result")))),
            _ => throw new ArgumentOutOfRangeException(nameof(family)),
        };
        var route = family switch { 0 => 6, 1 => 7, 2 => 8, _ => throw new ArgumentOutOfRangeException(nameof(family)) };
        var name = route switch { 6 => "TryExtractOrderedLiteralWindow", 7 => "TryExtractSymmetricAlternateOrderedLiteralWindow",
            8 => "TryExtractOrderedLiteralLookaheadWindow", _ => throw new ArgumentOutOfRangeException(nameof(route)) };
        var parents = Children(root).Select(node => (Node: node, Parent: node.Parent)).ToArray();
        using var owner = new Utf8ConstructionScope(0, ulong.MaxValue, ulong.MaxValue, ulong.MaxValue);
        var parent = owner.Budget.AllocateObject(17, static value => new ParentValue(value)); var checkpoint = owner.Budget.ReservedBytes;
        Assert.False(Assert.IsType<bool>(Call(name, root, null)));
        Assert.Equal(128ul, owner.Budget.ReservedBytes); Assert.Equal(0ul, owner.Budget.Depth); Assert.Equal(checkpoint, owner.Budget.ReservedBytes);
        foreach (var pair in parents) Assert.Same(pair.Parent, pair.Node.Parent);
        Assert.Equal(17, parent.Value);
    }
    private static IEnumerable<Utf8TreeNode> Children(Utf8TreeNode root)
    {
        yield return root;
        for (var index = 0; index < root.ChildCount; index++) foreach (var child in Children(root.Child(index))) yield return child;
    }
    [Fact]
    public void SingletonSuffixAndLinearProgramRetainBorrowedPayloadsAndAdmitOnlyOwnedArrays()
    {
        var literal = "("u8.ToArray(); var prefixes = new[] { "call"u8.ToArray(), "invoke"u8.ToArray() };
        using var owner = new Utf8ConstructionScope(0, ulong.MaxValue, ulong.MaxValue, ulong.MaxValue);
        var plan = new AsciiStructuralIdentifierFamilyPlan(prefixes, null, 0, null, null, 0, 0,
            literal, null, Utf8BoundaryRequirement.None, Utf8BoundaryRequirement.None);
        Assert.Same(prefixes, plan.Prefixes); Assert.Same(literal, Assert.Single(plan.SuffixParts).LiteralUtf8);
        Assert.Equal(new[] { AsciiStructuralVerifierStepKind.MatchSuffixAtCurrent, AsciiStructuralVerifierStepKind.Accept },
            plan.VerifierProgram.Steps.Select(step => step.Kind));
        Assert.Same(plan.SuffixParts, plan.VerifierProgram.LinearProgram.Instructions[0].SuffixParts);
        var expected = Utf8ConstructionBudget.ArrayBytes<AsciiStructuralSuffixPart>(1)
            + Utf8ConstructionBudget.ArrayBytes<AsciiStructuralVerifierStep>(2)
            + Utf8ConstructionBudget.ArrayBytes<AsciiStructuralLinearVerifierInstruction>(2);
        Assert.Equal(expected, owner.Budget.ReservedBytes); Assert.Equal(0ul, owner.Budget.Depth);
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void NativeStructuralFamiliesKeepScalarByteOperationsWithoutBcl(bool compiled)
    {
        foreach (var pattern in new[] { @"(?:call|invoke)[ \t]+[A-Z][a-z]*\(", @"call[\s\S]{0,3}Result",
            @"(?:call[\s\S]{0,3}Result|invoke[\s\S]{0,3}Other)", @"call(?=[ \t]+Result)" })
        {
            var regex = new Utf8Regex(pattern, RegexOptions.CultureInvariant | (compiled ? RegexOptions.Compiled : RegexOptions.None));
            Utf8OwnedVerifierAssertions.RetainsPreparedOwner(regex);
            var subject = pattern.Contains("[A-Z]") ? "😀call Name(中invoke Name("
                : pattern.Contains("?=") ? "😀call Result中call Result" : "😀call xResult中call xResult";
            var bytes = Encoding.UTF8.GetBytes(subject); Assert.True(regex.IsMatch(bytes)); Assert.Equal(2, regex.Count(bytes));
            var match = regex.Match(bytes); Assert.True(match.Success); Assert.Equal(4, match.StartOffsetInBytes);
            Assert.Equal(match.StartOffsetInBytes, Utf8MatchSequence.First(regex.CreateMatchCursor(bytes)).Value.StartOffsetInBytes);
            var count = 0; foreach (var item in regex.EnumerateMatches(bytes)) { Assert.True((item.EndOffsetInBytes - item.StartOffsetInBytes) > 0); count++; }
            Assert.Equal(2, count); Assert.Contains("X", Encoding.UTF8.GetString(regex.Replace(bytes, "X")));
            Assert.True(regex.Inspection.PreparedRegex.ScalarProgram is not null); Assert.True(ReferenceEquals(regex.Inspection.PreparedRegex.SemanticRegex.CaptureCatalog, regex.Inspection.PreparedRegex.ScalarProgram!.CaptureCatalog));
        }
    }
}
