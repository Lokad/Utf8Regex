using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.FrontEnd;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8ByteSafeCompilerContinuationTests
{
    private static readonly string[] Patterns =
    [
        @"^ab[0-9]{2}$", @"\bab[0-9]{2}\b", @"^ab(?:cd|ef)$", @"^ab(?:cd|ef)?gh$",
        @"^ab(?:cd|ef){2}$", @"^ab(?:cd[0-9]){2}$", @"^abx+y$", @"^abx+$",
    ];

    private static Utf8ExecutionNode Node(Utf8ExecutionNodeKind kind, params Utf8ExecutionNode[] children)
        => new(kind, RegexOptions.CultureInvariant, children, "", '\0', 1, 1, 1);

    private static Utf8ExecutionNode Text(string text)
        => new(Utf8ExecutionNodeKind.Multi, RegexOptions.CultureInvariant, [], text, '\0', 0, 1, 1);

    private static Utf8ExecutionNode Loop(Utf8ExecutionNode child, int min, int max)
        => new(Utf8ExecutionNodeKind.Loop, RegexOptions.CultureInvariant, [child], "", '\0', 0, min, max);

    private static Utf8ExecutionNode Deep(Utf8ExecutionNode node, int depth, int shape)
    {
        for (var index = 0; index < depth; index++)
            node = shape switch
            {
                0 => Node(Utf8ExecutionNodeKind.Concatenate, node),
                1 => Node(Utf8ExecutionNodeKind.Capture, Node(Utf8ExecutionNodeKind.Concatenate, node)),
                _ => Loop(node, 1, 1),
            };
        return node;
    }

    private static IEnumerable<string> Describe(Utf8ByteSafeLinearVerifierStep[] steps)
    {
        var pending = new Stack<(Utf8ByteSafeLinearVerifierStep Step, bool End)>();
        for (var index = steps.Length - 1; index >= 0; index--) pending.Push((steps[index], false));
        while (pending.TryPop(out var frame))
        {
            if (frame.End) { yield return "]"; continue; }
            var step = frame.Step;
            // Unit loops are equivalent to their body. Compare the complete
            // remaining instruction graph without recursive array equality.
            if (step.Kind != Utf8ByteSafeLinearVerifierStepKind.LoopProgram || step.Min != 1 || step.Max != 1)
            {
                yield return $"{step.Kind}:{step.Value}:{Convert.ToHexString(step.Text)}:{step.CharClass.LowMask:X16}/{step.CharClass.HighMask:X16}:{step.Min}:{step.Max}:{step.Options}:" +
                    string.Join(',', step.Alternatives.Select(static value => Convert.ToHexString(value)));
                if (step.Kind == Utf8ByteSafeLinearVerifierStepKind.LoopProgram) pending.Push((step, true));
            }
            if (step.Kind == Utf8ByteSafeLinearVerifierStepKind.LoopProgram)
                for (var index = step.Program.Length - 1; index >= 0; index--) pending.Push((step.Program[index], false));
        }
    }

    public static IEnumerable<object[]> Cases()
    {
        foreach (var depth in new[] { 65, 16385 })
        foreach (var shape in Enumerable.Range(0, 3))
        foreach (var family in Enumerable.Range(0, Patterns.Length))
        foreach (var compiled in new[] { false, true })
            yield return [depth, shape, family, compiled];
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void DeepCompilerKeepsCompleteInstructionsAndFlatProgramBehavior(int depth, int shape, int family, bool compiled)
    {
        var options = RegexOptions.CultureInvariant | (compiled ? RegexOptions.Compiled : RegexOptions.None);
        var analysis = Utf8FrontEnd.Compile(Patterns[family], options);
        Assert.NotNull(analysis.ExecutionTree);
        var baseline = Utf8ByteSafeLinearVerifierProgram.Compile(analysis.ExecutionTree);
        Assert.True(baseline.Succeeded);
        var result = Utf8ByteSafeLinearVerifierProgram.Compile(new Utf8ExecutionTree(Deep(analysis.ExecutionTree.Root, depth, shape)));
        Assert.True(result.Succeeded);
        Assert.Equal(baseline.FailureKind, result.FailureKind);
        Assert.Equal(Describe(baseline.Program.Steps), Describe(result.Program.Steps));
        var oracle = new Regex(Patterns[family], options);
        foreach (var subject in new[] { "", "ab12", "ab00x", "abcd", "abefgh", "abgh", "abcdcd", "abcd1cd2", "abxxxy", "abxxx", "zab12", "ab1x", "\nab12" })
        {
            var expected = oracle.Match(subject);
            var input = Encoding.UTF8.GetBytes(subject);
            var hit = result.Program.TryMatch(input, 0, out var length, out var fallback);
            Assert.False(fallback);
            Assert.Equal(expected.Success && expected.Index == 0, hit);
            if (hit) Assert.Equal(expected.Length, length);
        }
    }

    private static object Compiler()
    {
        var type = typeof(Utf8ByteSafeLinearVerifierProgram).GetNestedType("Compiler", BindingFlags.NonPublic);
        Assert.NotNull(type);
        var compiler = Activator.CreateInstance(type, nonPublic: true);
        Assert.NotNull(compiler);
        return compiler;
    }

    private static object? Call(object compiler, string name, params object?[] args)
    {
        var method = compiler.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        return method.Invoke(compiler, args);
    }

    [Theory]
    [InlineData(65)]
    [InlineData(16385)]
    public void DeepFixedBranchesAndOptionalProbesKeepTextAndOrder(int depth)
    {
        var first = Deep(Text("cat"), depth, 0);
        var second = Deep(Text("dog"), depth, 1);
        var alternate = Node(Utf8ExecutionNodeKind.Alternate, first, second);
        for (var index = 0; index < depth; index++) alternate = Loop(alternate, 0, 1);
        object?[] args = [alternate, null, false];
        Assert.True(Assert.IsType<bool>(Call(Compiler(), "TryExtractAlternationTexts", args)));
        Assert.True(Assert.IsType<bool>(args[2]));
        Assert.Equal(new[] { "cat", "dog" }, Assert.IsType<byte[][]>(args[1]).Select(static value => Encoding.ASCII.GetString(value)));
    }

    [Theory]
    [InlineData(65)]
    [InlineData(16385)]
    public void DeepMaskUnionsKeepSuccessfulSiblingsAndDiscardFailedChildMasks(int depth)
    {
        var branch = Text("cat");
        for (var index = 0; index < depth; index++) branch = Node(Utf8ExecutionNodeKind.Alternate, branch, Text("dog"));
        object?[] args = [branch, 0UL, 0UL];
        Assert.True(Assert.IsType<bool>(Call(Compiler(), "TryGetLeadingAsciiByteMask", args)));
        Assert.Equal(0UL, args[1]);
        Assert.Equal((1UL << ('c' - 64)) | (1UL << ('d' - 64)), args[2]);
        var failed = Node(Utf8ExecutionNodeKind.Alternate, branch, Node(Utf8ExecutionNodeKind.Unsupported));
        var root = Node(Utf8ExecutionNodeKind.Alternate, Text("ab"), failed);
        args = [root, 0UL, 0UL];
        Assert.False(Assert.IsType<bool>(Call(Compiler(), "TryGetLeadingAsciiByteMask", args)));
        Assert.Equal(0UL, args[1]);
        Assert.Equal(0UL, args[2]);
    }

    [Fact]
    public void CompactOptionalLoopProbeTerminatesWithoutRevisitingItself()
    {
        var compact = new Utf8ExecutionNode(Utf8ExecutionNodeKind.Loop, RegexOptions.CultureInvariant, [], "", 'x', 0, 0, 1);
        object?[] args = [compact, null, false];
        Assert.False(Assert.IsType<bool>(Call(Compiler(), "TryExtractAlternationTexts", args)));
        Assert.Empty(Assert.IsType<byte[][]>(args[1]));
        Assert.False(Assert.IsType<bool>(args[2]));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void MinimumWidthChecksOverflowingChildrenBeforeOuterMultiplication(int outerMin)
    {
        var child = new[] { Utf8ByteSafeLinearVerifierStep.LoopByte((byte)'a', int.MaxValue, int.MaxValue), Utf8ByteSafeLinearVerifierStep.MatchByte((byte)'b') };
        var steps = new[] { Utf8ByteSafeLinearVerifierStep.LoopProgram(child, outerMin, outerMin) };
        var failure = Assert.Throws<TargetInvocationException>(() => Call(Compiler(), "GetMinimumConsumedLength", steps));
        Assert.IsType<OverflowException>(failure.InnerException);
    }

    [Theory]
    [InlineData(65)]
    [InlineData(16385)]
    public void DeepNestedProgramMinimumUsesAllMarkersAndRequiredIterations(int depth)
    {
        var steps = new[] { Utf8ByteSafeLinearVerifierStep.MatchText("cat"u8.ToArray()) };
        for (var index = 0; index < depth; index++) steps = [Utf8ByteSafeLinearVerifierStep.RequireBeginning(RegexOptions.None), Utf8ByteSafeLinearVerifierStep.LoopProgram(steps, 1, 1)];
        Assert.Equal(3, Call(Compiler(), "GetMinimumConsumedLength", steps));
    }

    [Theory]
    [InlineData(65)]
    [InlineData(16385)]
    public void DeepUnsupportedAndZeroWidthBodiesRetainFailureDiagnostics(int depth)
    {
        var unsupported = Utf8ByteSafeLinearVerifierProgram.Compile(new Utf8ExecutionTree(Deep(Node(Utf8ExecutionNodeKind.Unsupported), depth, 0)));
        Assert.False(unsupported.Succeeded);
        Assert.Equal(Utf8ByteSafeLinearCompileFailureKind.UnsupportedNode, unsupported.FailureKind);
        var empty = Utf8ByteSafeLinearVerifierProgram.Compile(new Utf8ExecutionTree(Loop(Deep(Node(Utf8ExecutionNodeKind.Beginning), depth, 0), 1, 1)));
        Assert.False(empty.Succeeded);
        Assert.Equal(Utf8ByteSafeLinearCompileFailureKind.NestedSubProgram, empty.FailureKind);
    }

    public static IEnumerable<object[]> AnchoredCases()
    {
        foreach (var pattern in new[] { @"^abx?xy$", @"\Aabx{0,2}xy\z", @"^(?<head>ab)x?xy$", @"\A(?<head>ab)x{0,2}xy\z" })
        foreach (var compiled in new[] { false, true })
        foreach (var rtl in new[] { false, true })
            yield return [pattern, compiled, rtl];
    }

    [Theory]
    [MemberData(nameof(AnchoredCases))]
    public void AnchoredLoopOverlapRetainsOwnedCompletePublicOperations(string pattern, bool compiled, bool rtl)
    {
        var options = RegexOptions.CultureInvariant | (compiled ? RegexOptions.Compiled : RegexOptions.None) |
            (rtl ? RegexOptions.RightToLeft : RegexOptions.None);
        Utf8SliceSparseGroupTests.AssertOwnedOperations(pattern, options,
            ["", "abxy", "abxxy", "abxxxy", "abxy\n", "abxxy\n", "abxy\r\n", "abxy\nabxxy", "\U0001f600abxy", "abxy\U0001f600"], options);
    }
}
