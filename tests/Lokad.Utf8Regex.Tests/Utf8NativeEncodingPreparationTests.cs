using System.Globalization;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.FrontEnd;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8NativeEncodingPreparationTests
{
    private sealed class ParentValue(int value) { internal readonly int Value = value; }
    private static Utf8TreeNode Node(Utf8TreeNodeKind kind, params Utf8TreeNode[] children)
    {
        var node = Utf8TreeNode.Create(kind, RegexOptions.CultureInvariant);
        foreach (var child in children) node.WithChild(child);
        return node;
    }
    private static Utf8TreeNode One(char value) => Utf8TreeNode.Create(Utf8TreeNodeKind.One, RegexOptions.CultureInvariant, value);
    private static Utf8TreeNode Text(string value) => Utf8TreeNode.Create(Utf8TreeNodeKind.Multi, RegexOptions.CultureInvariant, value);
    private static Utf8TreeNode Wrap(Utf8TreeNode node, int depth)
    {
        for (var index = 0; index < depth; index++) node = Node(Utf8TreeNodeKind.Capture, node);
        return node;
    }
    private static object? Call(string name, params object?[] args)
    {
        var method = typeof(Utf8NativeExecutionAnalyzer).GetMethods(BindingFlags.Static | BindingFlags.NonPublic)
            .Single(value => value.Name == name && value.GetParameters().Length == args.Length);
        try { return method.Invoke(null, args); }
        catch (TargetInvocationException error) when (error.InnerException is { } inner)
        { ExceptionDispatchInfo.Capture(inner).Throw(); throw; }
    }
    private static Utf8SemanticRegex Semantic(Utf8TreeNode body, string pattern, RegexOptions options)
        => new(pattern, options, 0, options, Utf8OwnedRegexTree.Create(Node(Utf8TreeNodeKind.Capture, body), 1, null, null, null, options, CultureInfo.InvariantCulture, null, false, false));
    private sealed record InputValue(Utf8TreeNode Root, Utf8SemanticRegex Semantic);
    private static InputValue Input(int route, int depth)
    {
        var alternate = Node(Utf8TreeNodeKind.Alternate, One('a'), Text("😀"));
        var root = route switch
        {
            2 => Node(Utf8TreeNodeKind.Concatenate, One('é'), Text("😀"), Node(Utf8TreeNodeKind.PositiveLookaround, One('x'))),
            3 or 10 => Node(Utf8TreeNodeKind.Concatenate, Wrap(alternate, depth), Node(Utf8TreeNodeKind.PositiveLookaround, One('x'))),
            4 or 8 => Wrap(alternate, depth),
            5 => Wrap(One('é'), depth),
            6 => Node(Utf8TreeNodeKind.Concatenate, Node(Utf8TreeNodeKind.Boundary), Wrap(alternate, depth), Node(Utf8TreeNodeKind.Boundary)),
            7 => Node(Utf8TreeNodeKind.Concatenate, Node(Utf8TreeNodeKind.Boundary), One('é'), Node(Utf8TreeNodeKind.Boundary)),
            9 => Node(Utf8TreeNodeKind.Concatenate, One('é'), Text("😀"), Node(Utf8TreeNodeKind.PositiveLookaround, One('x'))),
            _ => Text("é😀"),
        };
        return new(root, Semantic(root, route switch { 8 => "a|😀", 9 => "é😀(?=x)", 10 => "(?:a|😀)(?=x)", _ => "é😀" }, RegexOptions.CultureInvariant));
    }
    private static void Run(int route, InputValue input)
    {
        var root = input.Root;
        var options = RegexOptions.CultureInvariant;
        if (route == 0)
        {
            const string pattern = "(é😀)";
            Assert.True(Utf8NativeExecutionAnalyzer.TryExtractSingleCapturedLiteralPattern(pattern, false, out var literal));
            Assert.True(literal == pattern.AsSpan(1, 3));
            return;
        }
        if (route == 1)
        {
            Assert.True(Utf8NativeExecutionAnalyzer.TryExtractPlainAsciiLiteralAlternationPattern("call|invoke", false, out var literals));
            Assert.Equal(new[] { "call", "invoke" }, literals.Select(Encoding.UTF8.GetString));
            return;
        }
        if (route == 11)
        {
            Assert.True(Utf8NativeExecutionAnalyzer.TryAnalyze(input.Semantic, default, out var analysis));
            Assert.NotNull(analysis.LiteralUtf8);
            Assert.Equal("é😀", Encoding.UTF8.GetString(analysis.LiteralUtf8));
            return;
        }

        var name = route switch
        {
            2 => "TryExtractLiteralPrefixWithPositiveLiteralLookahead", 3 => "TryExtractPositiveLiteralLookahead",
            4 => "TryUnwrapLiteralAlternation", 5 => "TryGetLiteralBranchUtf8",
            6 => "TryUnwrapLiteralAlternationWithBoundaries", 7 => "TryExtractBoundaryWrappedLiteral",
            8 => "TryExtractUtf8LiteralAlternationTree", 9 => "TryExtractLiteralWithPositiveLiteralLookaheadTree",
            _ => "TryExtractLiteralAlternationWithPositiveLiteralLookaheadTree",
        };
        object?[] args = route switch
        {
            2 or 3 => [root, options, null, null],
            4 or 5 => [root, null], 6 or 7 => [root, null, null, null],
            8 => [input.Semantic, null, null, null],
            9 => [input.Semantic, null, null],
            _ => [input.Semantic, options, null, null, null, null],
        };
        Assert.True(Assert.IsType<bool>(Call(name, args)));
        switch (route)
        {
            case 2: Assert.Equal("é😀"u8.ToArray(), args[2]); Assert.Equal("x"u8.ToArray(), args[3]); break;
            case 3: Assert.Same(root.Child(0), args[2]); Assert.Equal("x"u8.ToArray(), args[3]); break;
            case 4: Assert.Equal(new[] { "a", "😀" }, Assert.IsType<byte[][]>(args[1]).Select(Encoding.UTF8.GetString)); break;
            case 5: Assert.Equal("é"u8.ToArray(), args[1]); break;
            case 7: Assert.Equal("é", args[1]); break;
            case 6: Assert.Equal(new[] { "a", "😀" }, Assert.IsType<byte[][]>(args[1]).Select(Encoding.UTF8.GetString)); break;
            case 8: Assert.Equal(new[] { "a", "😀" }, Assert.IsType<byte[][]>(args[1]).Select(Encoding.UTF8.GetString)); break;
            case 9: Assert.Equal("é😀"u8.ToArray(), args[1]); Assert.Equal("x", Encoding.UTF8.GetString(Assert.IsType<byte[]>(args[2]))); break;
            case 10: Assert.Equal(new[] { "a", "😀" }, Assert.IsType<byte[][]>(args[2]).Select(Encoding.UTF8.GetString)); break;
        }
    }
    public static IEnumerable<object[]> RootDenials()
    {
        for (var route = 0; route < 12; route++)
        for (var denial = route == 0 ? 1 : 0; denial < 3; denial++) yield return [route, denial];
    }
    [Theory]
    [MemberData(nameof(RootDenials))]
    public void RootDenialPrecedesTraversalOrOutputAcquisition(int route, int denial)
    {
        var root = Input(route, 0);
        using var owner = new Utf8ConstructionScope(0, denial == 0 ? 128ul : ulong.MaxValue,
            denial == 2 ? 2ul : ulong.MaxValue, denial == 1 ? 0ul : ulong.MaxValue);
        var parent = owner.Budget.AllocateObject(17, static value => new ParentValue(value)); var checkpoint = owner.Budget.ReservedBytes;
        var error = Assert.Throws<Utf8ResourceLimitExceededException>(() => Run(route, root));
        Assert.Equal(denial switch { 0 => Utf8ResourceLimitKind.ConstructionBytes, 1 => Utf8ResourceLimitKind.ConstructionDepth,
            _ => Utf8ResourceLimitKind.ConstructionWorkSteps }, error.ResourceKind);
        Assert.Equal(128ul, owner.Budget.PeakBytes); Assert.Equal(128ul, owner.Budget.ReservedBytes);
        Assert.Equal(0ul, owner.Budget.Depth); Assert.Equal(checkpoint, owner.Budget.ReservedBytes); Assert.Equal(17, parent.Value);
    }
    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)] [InlineData(5)]
    [InlineData(6)] [InlineData(7)] [InlineData(8)] [InlineData(9)] [InlineData(10)] [InlineData(11)]
    public void EveryWorkCutoffRecoversParentAndAcceptedOutputsRetainLoans(int route)
    {
        var root = Input(route, 0); ulong work;
        using (var measure = new Utf8ConstructionScope(0, ulong.MaxValue, ulong.MaxValue, ulong.MaxValue))
        { Run(route, root); work = measure.Budget.WorkSteps; }
        Assert.InRange(work, 1ul, 10000ul);
        for (var cutoff = 0ul; cutoff <= work; cutoff++)
        {
            using var owner = new Utf8ConstructionScope(0, ulong.MaxValue, cutoff + 2, ulong.MaxValue);
            var parent = owner.Budget.AllocateObject(17, static value => new ParentValue(value)); var checkpoint = owner.Budget.ReservedBytes;
            var error = Record.Exception(() => Run(route, root));
            if (cutoff < work)
            {
                Assert.Equal(Utf8ResourceLimitKind.ConstructionWorkSteps, Assert.IsType<Utf8ResourceLimitExceededException>(error).ResourceKind);
                Assert.Equal(128ul, owner.Budget.ReservedBytes); Assert.Equal(checkpoint, owner.Budget.ReservedBytes);
            }
            else
            {
                Assert.Null(error);
                if (route == 0) Assert.Equal(128ul, owner.Budget.ReservedBytes);
                else Assert.True(owner.Budget.ReservedBytes > 128);
            }
            Assert.Equal(0ul, owner.Budget.Depth); Assert.Equal(0ul, owner.Budget.Reserved);
            Assert.InRange(owner.Budget.WorkSteps, 2ul, cutoff + 2); Assert.Equal(17, parent.Value);
        }
    }
    [Theory]
    [InlineData(4)] [InlineData(5)] [InlineData(6)] [InlineData(8)] [InlineData(10)]
    public void WrapperDepthDenialUnwindsAndAllowsLaterWork(int route)
    {
        var root = Input(route, 32);
        var recovered = Input(route, 0);
        using var owner = new Utf8ConstructionScope(0, ulong.MaxValue, ulong.MaxValue, 8);
        var parent = owner.Budget.AllocateObject(17, static value => new ParentValue(value)); var checkpoint = owner.Budget.ReservedBytes;
        var error = Assert.Throws<Utf8ResourceLimitExceededException>(() => Run(route, root));
        Assert.Equal(Utf8ResourceLimitKind.ConstructionDepth, error.ResourceKind); Assert.Equal(9ul, error.Requested);
        Assert.Equal(128ul, owner.Budget.ReservedBytes); Assert.Equal(0ul, owner.Budget.Depth); Assert.Equal(checkpoint, owner.Budget.ReservedBytes);
        Run(route, recovered); Assert.Equal(17, parent.Value);
    }

    [Fact]
    public void CapturedLiteralProbeBorrowsSourceWithNoByteHeadroom()
    {
        var input = Input(0, 0);
        using var owner = new Utf8ConstructionScope(0, 0, ulong.MaxValue, 1);
        Run(0, input);
        Assert.Equal(0ul, owner.Budget.PeakBytes);
        Assert.Equal(1ul, owner.Budget.PeakDepth);
        Assert.Equal(0ul, owner.Budget.Depth);
    }

    [Fact]
    public void BoundaryProbeUsesDepthWithoutAcquiringScratch()
    {
        var root = Input(7, 0);
        var resultBytes = Utf8ConstructionBudget.StringBytes(1);
        using var owner = new Utf8ConstructionScope(0, resultBytes, ulong.MaxValue, 2);
        Run(7, root);
        Assert.Equal(2ul, owner.Budget.PeakDepth);
        Assert.Equal(0ul, owner.Budget.Depth);
        Assert.Equal(resultBytes, owner.Budget.PeakBytes);
        Assert.Equal(resultBytes, owner.Budget.ReservedBytes);
    }

    [Fact]
    public void LookaheadProbeBorrowsLiteralBytesWithoutAcquiringScratch()
    {
        const RegexOptions options = RegexOptions.CultureInvariant;
        var expected = "é😀"u8.ToArray();
        var prefix = One('a');
        var lookahead = Node(Utf8TreeNodeKind.PositiveLookaround, Utf8TreeNode.CreateUtf8Literal(options, expected));
        var body = Node(Utf8TreeNodeKind.Concatenate, prefix, lookahead);
        using var owner = new Utf8ConstructionScope(0, 0, ulong.MaxValue, 2);
        object?[] args = [body, options, null, null];

        Assert.True(Assert.IsType<bool>(Call("TryExtractPositiveLiteralLookahead", args)));
        Assert.Same(prefix, args[2]);
        Assert.Same(expected, args[3]);
        Assert.Equal(2ul, owner.Budget.PeakDepth);
        Assert.Equal(0ul, owner.Budget.Depth);
        Assert.Equal(0ul, owner.Budget.PeakBytes);
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)] [InlineData(5)]
    public void LateDeclineRestoresSuccessfulChildTextAndArrayLoans(int route)
    {
        var alternate = Node(Utf8TreeNodeKind.Alternate, One('a'), One('b'));
        var empty = Node(Utf8TreeNodeKind.Empty);
        object?[] args = route switch
        {
            0 => [Node(Utf8TreeNodeKind.Concatenate, One('a'), empty, Node(Utf8TreeNodeKind.PositiveLookaround, One('x'))), RegexOptions.CultureInvariant, null, null],
            1 => [Node(Utf8TreeNodeKind.Alternate, One('a'), empty), null],
            2 => [Node(Utf8TreeNodeKind.Concatenate, Node(Utf8TreeNodeKind.Boundary), alternate, empty), null, null, null],
            3 => [Node(Utf8TreeNodeKind.Concatenate, Node(Utf8TreeNodeKind.Boundary), One('a'), empty), null, null, null],
            _ => [],
        };
        var name = route switch { 0 => "TryExtractLiteralPrefixWithPositiveLiteralLookahead", 1 => "TryUnwrapLiteralAlternation",
            2 => "TryUnwrapLiteralAlternationWithBoundaries", 3 => "TryExtractBoundaryWrappedLiteral",
            _ => string.Empty };
        const RegexOptions folded = RegexOptions.CultureInvariant | RegexOptions.IgnoreCase;
        Utf8SemanticRegex? nonAscii = route == 5
            ? Semantic(Node(Utf8TreeNodeKind.Alternate, One('a'), Text("😀")), "a|😀", folded)
            : null;
        using var owner = new Utf8ConstructionScope(0, ulong.MaxValue, ulong.MaxValue, ulong.MaxValue);
        var parent = owner.Budget.AllocateObject(17, static value => new ParentValue(value)); var checkpoint = owner.Budget.ReservedBytes;
        if (nonAscii is { } semantic)
            Assert.False(Utf8NativeExecutionAnalyzer.TryAnalyze(semantic, default, out _));
        else if (route == 4)
            Assert.False(Utf8NativeExecutionAnalyzer.TryExtractSingleCapturedLiteralPattern("(a*)", false, out _));
        else
            Assert.False(Assert.IsType<bool>(Call(name, args)));
        Assert.Equal(128ul, owner.Budget.ReservedBytes); Assert.Equal(0ul, owner.Budget.Depth); Assert.Equal(checkpoint, owner.Budget.ReservedBytes);
        Assert.Equal(17, parent.Value);
    }
    [Theory]
    [InlineData(1)] [InlineData(8)] [InlineData(9)] [InlineData(10)]
    public void EveryByteCutoffRecoversPartiallyAcquiredOutputs(int route)
    {
        var root = Input(route, 0); ulong peak;
        using (var measure = new Utf8ConstructionScope(0, ulong.MaxValue, ulong.MaxValue, ulong.MaxValue))
        { measure.Budget.AllocateObject(17, static value => new ParentValue(value)); Run(route, root); peak = measure.Budget.PeakBytes; }
        var partialDenial = false; var success = false;
        for (var cap = 128ul; cap <= peak; cap += 16)
        {
            using var owner = new Utf8ConstructionScope(0, cap, ulong.MaxValue, ulong.MaxValue);
            var parent = owner.Budget.AllocateObject(17, static value => new ParentValue(value)); var checkpoint = owner.Budget.ReservedBytes;
            var error = Record.Exception(() => Run(route, root));
            if (error is not null)
            {
                Assert.Equal(Utf8ResourceLimitKind.ConstructionBytes, Assert.IsType<Utf8ResourceLimitExceededException>(error).ResourceKind);
                Assert.Equal(128ul, owner.Budget.ReservedBytes); Assert.Equal(checkpoint, owner.Budget.ReservedBytes);
                partialDenial |= owner.Budget.PeakBytes > 128;
            }
            else { success = true; Assert.True(owner.Budget.ReservedBytes > 128); }
            Assert.InRange(owner.Budget.PeakBytes, 128ul, cap); Assert.Equal(0ul, owner.Budget.Depth); Assert.Equal(17, parent.Value);
        }
        Assert.True(partialDenial); Assert.True(success);
    }
    [Theory]
    [InlineData("")] [InlineData("ASCII")] [InlineData("é中😀\0")]
    public void AdmittedEncodingConsumesWholeScalars(string text)
    {
        using var owner = new Utf8ConstructionScope(0, ulong.MaxValue, ulong.MaxValue, ulong.MaxValue);
        var bytes = owner.Budget.EncodeUtf8(text);
        Assert.Equal(new UTF8Encoding(false, true).GetBytes(text), bytes);
        Assert.Equal(Utf8ConstructionBudget.ArrayBytes<byte>(bytes.Length), owner.Budget.ReservedBytes);
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void InvalidScalarCannotBecomeAReplacementCharacterLiteral(bool low)
    {
        var text = low ? "x" + ((char)0xdfff).ToString() : ((char)0xd800).ToString();
        using var owner = new Utf8ConstructionScope(0, ulong.MaxValue, ulong.MaxValue, ulong.MaxValue);
        Assert.Throws<ArgumentException>(() => owner.Budget.EncodeUtf8(text));
        Assert.Equal(0ul, owner.Budget.ReservedBytes);
    }
    [Fact]
    public void FlatAlternationScanningUsesLinearWorkWithoutSplitStrings()
    {
        var pattern = string.Join('|', Enumerable.Repeat("a", 2048));
        using var owner = new Utf8ConstructionScope(0, ulong.MaxValue, (ulong)pattern.Length * 20, ulong.MaxValue);
        Assert.True(Utf8NativeExecutionAnalyzer.TryExtractPlainAsciiLiteralAlternationPattern(pattern, false, out var literals));
        Assert.Equal(2048, literals.Length);

    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void NativeLiteralsAndLookaheadsRetainOwnedScalarOperations(bool compiled)
    {
        foreach (var row in new[] { (Pattern: "é😀", Subject: "xé😀中é😀x", Count: 2, Replaced: "xX中Xx"),
            (Pattern: "(é😀)", Subject: "xé😀中é😀x", Count: 2, Replaced: "xX中Xx"),
            (Pattern: "é😀(?=x)", Subject: "é😀x中é😀y", Count: 1, Replaced: "Xx中é😀y"),
            (Pattern: "(?:é😀|中)(?=x)", Subject: "é😀x中x😀", Count: 2, Replaced: "XxXx😀"),
            (Pattern: "call|invoke", Subject: "😀call中invoke😀", Count: 2, Replaced: "😀X中X😀") })
        {
            var regex = new Utf8Regex(row.Pattern, options: RegexOptions.CultureInvariant | (compiled ? RegexOptions.Compiled : RegexOptions.None));
            var bytes = Encoding.UTF8.GetBytes(row.Subject); Assert.True(regex.IsMatch(bytes)); Assert.Equal(row.Count, regex.Count(bytes));
            var match = regex.Match(bytes); Assert.True(match.Success);
            Assert.Equal(row.Pattern == "call|invoke" ? 4 : row.Subject.StartsWith('x') ? 1 : 0, match.StartOffsetInBytes);
            Assert.Equal(row.Pattern == "call|invoke" ? 4 : 6, (match.EndOffsetInBytes - match.StartOffsetInBytes));
            Assert.Equal(match.StartOffsetInBytes, Utf8MatchSequence.First(regex.CreateMatchCursor(bytes)).Value.StartOffsetInBytes);
            if (row.Pattern == "(é😀)") Assert.Equal("é😀", System.Text.Encoding.UTF8.GetString(Utf8MatchSequence.First(regex.CreateMatchCursor(bytes)).GetGroupMemory(1).Span));
            var count = 0; foreach (var item in regex.EnumerateMatches(bytes)) { Assert.True((item.EndOffsetInBytes - item.StartOffsetInBytes) > 0); count++; }
            Assert.Equal(row.Count, count); Assert.Equal(row.Replaced, Encoding.UTF8.GetString(regex.Replace(bytes, "X"u8)));
            Assert.True(regex.Inspection.PreparedRegex.ScalarProgram is not null); Assert.True(ReferenceEquals(regex.Inspection.PreparedRegex.SemanticRegex.CaptureCatalog, regex.Inspection.PreparedRegex.ScalarProgram!.CaptureCatalog));
        }
    }
}
