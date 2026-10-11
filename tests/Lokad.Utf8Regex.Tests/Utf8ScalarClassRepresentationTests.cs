using System.Buffers;
using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.FrontEnd;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8ScalarClassRepresentationTests
{
    private sealed class ParentValue(int value) { internal readonly int Value = value; }

    [Fact]
    public void CanonicalAtomProducersKeepCompleteScalarByteRanges()
    {
        (string Pattern, RegexOptions Extra, string Subject, (int, int)[] Ranges)[] cases =
        [
            (".", 0, "😀\n𐐀", [(0, 4), (5, 4)]),
            (".", RegexOptions.Singleline, "😀\n𐐀", [(0, 4), (4, 1), (5, 4)]),
            (@"\d", 0, "1𝟎!", [(0, 1), (1, 4)]),
            (@"\D", 0, "𝟎😀", [(4, 4)]),
            (@"\w", 0, "𐐀_😀", [(0, 4), (4, 1)]),
            (@"\W", 0, "𐐀_😀", [(5, 4)]),
            (@"\s", 0, "😀\u0085\u00a0\u2028", [(4, 2), (6, 2), (8, 3)]),
            (@"\S", 0, "😀\u0085", [(0, 4)]),
            (@"\p{L}", 0, "𐐀😀", [(0, 4)]),
            (@"\P{L}", 0, "𐐀😀", [(4, 4)]),
            (@"\p{IsSpecials}", 0, "\uffff\U00010000", [(0, 3)]),
            (@"\P{IsSpecials}", 0, "\uffff\U00010000", [(3, 4)]),
            (@"[\P{IsBasicLatin}-[\p{L}]]", 0, "😀𐐀!", [(0, 4)]),
            (@"(?i:k)", 0, "KkK😀", [(0, 1), (1, 1), (2, 3)]),
            (@"\d", RegexOptions.ECMAScript, "1𝟎😀_İ ", [(0, 1)]),
            (@"\D", RegexOptions.ECMAScript, "1𝟎😀_İ ", [(1, 4), (5, 4), (9, 1), (10, 2), (12, 1)]),
            (@"\w", RegexOptions.ECMAScript, "1𝟎😀_İ ", [(0, 1), (9, 1), (10, 2)]),
            (@"\W", RegexOptions.ECMAScript, "1𝟎😀_İ ", [(1, 4), (5, 4), (12, 1)]),
            (@"\s", RegexOptions.ECMAScript, "1𝟎😀_İ ", [(12, 1)]),
            (@"\S", RegexOptions.ECMAScript, "1𝟎😀_İ ", [(0, 1), (1, 4), (5, 4), (9, 1), (10, 2)]),
        ];
        foreach (var row in cases)
        foreach (var compiled in new[] { false, true })
        foreach (var rtl in (row.Extra & RegexOptions.ECMAScript) != 0 ? new[] { false } : new[] { false, true })
        {
            var options = Options(compiled, rtl) | row.Extra;
            var tree = Utf8OwnedTreeParser.TryParse(row.Pattern, options);
            Assert.NotNull(tree);
            var pending = new Stack<Utf8TreeNode>(); pending.Push(tree.Root);
            while (pending.TryPop(out var node))
            {
                if (node.Kind is Utf8TreeNodeKind.Set or Utf8TreeNodeKind.Setloop or Utf8TreeNodeKind.Setlazy or Utf8TreeNodeKind.Setloopatomic)
                {
                    Assert.NotNull(node.ScalarClass);
                    Assert.Null(node.Str);
                }
                for (var index = 0; index < node.ChildCount; index++) pending.Push(node.Child(index));
            }
            var input = Encoding.UTF8.GetBytes(row.Subject);
            var regex = new Utf8Regex(row.Pattern, options);
            var expected = rtl ? row.Ranges.Reverse().ToArray() : row.Ranges;
            Assert.True(regex.IsMatch(input));
            Assert.Equal(expected.Length, regex.Count(input));
            var actual = new List<(int, int)>();
            foreach (var match in regex.EnumerateMatches(input)) actual.Add((match.IndexInBytes, match.LengthInBytes));
            Assert.Equal(expected, actual);
            var first = regex.MatchDetailed(input);
            Assert.True(first.Success);
            Assert.Equal(expected[0], (first.IndexInBytes, first.LengthInBytes));
            AssertOwned(regex);
        }
    }

    [Fact]
    public void CanonicalParserClassProducersRecoverEveryWorkCutoffAndRootDenial()
    {
        foreach (var pattern in new[] { ".", "(?s:.)", @"\w", @"\P{IsSpecials}", @"\p{L}", @"(?i:K)", @"[x\P{IsBasicLatin}-[\d]]" })
        foreach (var options in new[] { RegexOptions.CultureInvariant, RegexOptions.CultureInvariant | RegexOptions.ECMAScript })
        {
            ulong work;
            using (var measured = new Utf8ConstructionScope(ulong.MaxValue, ulong.MaxValue, ulong.MaxValue, ulong.MaxValue))
            {
                Assert.NotNull(Utf8OwnedTreeParser.TryParse(pattern, options));
                work = measured.Budget.WorkSteps;
            }
            Assert.InRange(work, 1ul, 20_000ul);
            for (ulong cutoff = 0; cutoff <= work; cutoff++)
            {
                using var owner = new Utf8ConstructionScope(ulong.MaxValue, ulong.MaxValue, cutoff + 2, ulong.MaxValue);
                var parent = owner.Budget.AllocateObject(17, static value => new ParentValue(value));
                var checkpoint = owner.Budget.ReservedBytes;
                if (cutoff < work)
                {
                    var error = Assert.Throws<Utf8ResourceLimitExceededException>(() => Utf8OwnedTreeParser.TryParse(pattern, options));
                    Assert.Equal(Utf8ResourceLimitKind.ConstructionWorkSteps, error.ResourceKind);
                    Assert.Equal(128ul, owner.Budget.ReservedBytes);
                    Assert.Equal(checkpoint, owner.Budget.ReservedBytes);
                }
                else Assert.NotNull(Utf8OwnedTreeParser.TryParse(pattern, options));
                Assert.Equal(17, parent.Value);
                Assert.Equal(0ul, owner.Budget.Depth);
                Assert.InRange(owner.Budget.WorkSteps, 2ul, cutoff + 2);
            }
            foreach (var depth in new[] { false, true })
            {
                using var owner = new Utf8ConstructionScope(ulong.MaxValue, depth ? ulong.MaxValue : 128, ulong.MaxValue, depth ? 0ul : ulong.MaxValue);
                var parent = owner.Budget.AllocateObject(17, static value => new ParentValue(value));
                var checkpoint = owner.Budget.ReservedBytes;
                var error = Assert.Throws<Utf8ResourceLimitExceededException>(() => Utf8OwnedTreeParser.TryParse(pattern, options));
                Assert.Equal(depth ? Utf8ResourceLimitKind.ConstructionDepth : Utf8ResourceLimitKind.ConstructionBytes, error.ResourceKind);
                Assert.Equal(128ul, owner.Budget.ReservedBytes);
                Assert.Equal(checkpoint, owner.Budget.ReservedBytes);
                Assert.Equal(0ul, owner.Budget.Depth);
                Assert.Equal(17, parent.Value);
                if (depth) Assert.Equal(128ul, owner.Budget.PeakBytes);
            }
        }
    }

    [Fact]
    public void AllRecognizedPropertyBlocksUseExplicitIntegerEndpointPairs()
    {
        Assert.Equal(112, Utf8SliceCategories.BlockNames.Count);
        foreach (var name in Utf8SliceCategories.BlockNames)
        {
            Assert.True(Utf8SliceCategories.TryResolve(name, out var mask, out var ranges, out var block));
            Assert.True(block); Assert.Equal(0u, mask);
            Assert.IsType<int[]>(ranges);
            Assert.Equal(0, ranges.Length & 1);
            Assert.NotEmpty(ranges);
            for (var index = 0; index < ranges.Length; index += 2)
            {
                Assert.InRange(ranges[index], 0, 0x10ffff);
                Assert.InRange(ranges[index + 1], ranges[index] + 1, 0x110000);
            }
            if (name == "IsSpecials") Assert.Equal(new[] { 0xfff0, 0x10000 }, ranges);
        }
    }

    private static readonly string SparseMembers = MakeSparseMembers();
    private static readonly string RepeatedCategories = string.Concat(Enumerable.Repeat(@"\p{Lu}", 65536));

    private static string MakeSparseMembers()
    {
        var text = new StringBuilder();
        for (var index = 0; index < 33000; index++) text.Append(char.ConvertFromUtf32(0x10000 + 2 * index));
        return text.Append("\U0010fffe").ToString();
    }

    [Theory]
    [InlineData(false, false)] [InlineData(false, true)]
    [InlineData(true, false)] [InlineData(true, true)]
    public void LargeSparseClassKeepsScalarCoordinatesAcrossOperations(bool compiled, bool rtl)
    {
        var options = Options(compiled, rtl);
        var input = "x\U0001f600\U0001f601\U00010400y"u8.ToArray();
        foreach (var shape in new[] { 0, 1, 2 })
        {
            var pattern = "[" + (shape == 1 ? "^" : "") + SparseMembers + (shape == 2 ? "-[\U00010400]]" : "]");
            var regex = new Utf8Regex(pattern, options);
            var expected = shape switch
            {
                0 => new[] { (1, 4), (9, 4) },
                1 => new[] { (0, 1), (5, 4), (13, 1) },
                _ => new[] { (1, 4) },
            };
            if (rtl) Array.Reverse(expected);
            Assert.True(regex.IsMatch(input));
            Assert.Equal(expected.Length, regex.Count(input));
            var ranges = new List<(int, int)>();
            foreach (var match in regex.EnumerateMatches(input)) ranges.Add((match.IndexInBytes, match.LengthInBytes));
            Assert.Equal(expected, ranges);
            var first = regex.Match(input);
            Assert.Equal(expected[0], (first.IndexInBytes, first.LengthInBytes));
            var detailed = regex.MatchDetailed(input);
            Assert.Equal(expected[0], (detailed.IndexInBytes, detailed.LengthInBytes));
            Assert.Equal(Encoding.UTF8.GetString(input.AsSpan(expected[0].Item1, expected[0].Item2)), detailed.GetValueString());
            var expectedText = shape switch
            {
                0 => "xQ\U0001f601Qy",
                1 => "Q\U0001f600Q\U00010400Q",
                _ => "xQ\U0001f601\U00010400y",
            };
            var output = Encoding.UTF8.GetBytes(expectedText);
            Assert.Equal(output, regex.Replace(input, "Q"u8));
            Assert.Equal(expectedText, regex.ReplaceToString(input, "Q"));
            var destination = new byte[output.Length];
            Assert.Equal(OperationStatus.Done, regex.TryReplace(input, "Q", destination, out var written));
            Assert.Equal(output.Length, written); Assert.Equal(output, destination);
            var parts = new List<(int, int)>();
            foreach (var part in regex.EnumerateSplits(input)) parts.Add((part.IndexInBytes, part.LengthInBytes));
            var expectedParts = shape switch
            {
                0 => new[] { (0, 1), (5, 4), (13, 1) },
                1 => new[] { (0, 0), (1, 4), (9, 4), (14, 0) },
                _ => new[] { (0, 1), (5, 9) },
            };
            if (rtl) Array.Reverse(expectedParts);
            Assert.Equal(expectedParts, parts);
            AssertOwned(regex);
        }
        // The far-plane endpoint remains distinct from its adjacent scalar.
        var endpoint = new Utf8Regex("[" + SparseMembers + "]", options);
        Assert.True(endpoint.IsMatch("\U0010fffe"u8));
        Assert.False(endpoint.IsMatch("\U0010ffff"u8));
        AssertOwned(endpoint);
    }

    [Theory]
    [InlineData(false, false)] [InlineData(false, true)]
    [InlineData(true, false)] [InlineData(true, true)]
    public void LargeClassesKeepQuantifiersCaptureHistoryReferencesAndAssertions(bool compiled, bool rtl)
    {
        var options = Options(compiled, rtl);
        var atom = "[" + SparseMembers + "]";
        var regex = new Utf8Regex("(?<x>" + atom + "){2}", options);
        var match = regex.MatchDetailed("\U0001f600\U00010400"u8);
        Assert.True(match.Success);
        Assert.Equal((0, 8), (match.IndexInBytes, match.LengthInBytes));
        var group = match.GetGroup("x");
        Assert.Equal(2, group.CaptureCount);
        for (var index = 0; index < 2; index++)
        {
            var capture = group.GetCapture(index);
            Assert.Equal((rtl ? 4 - 4 * index : 4 * index, 4), (capture.IndexInBytes, capture.LengthInBytes));
        }
        AssertOwned(regex);
        var reference = new Utf8Regex(rtl ? @"\k<x>(?<x>" + atom + ")" : "(?<x>" + atom + @")\k<x>", options);
        Assert.True(reference.IsMatch("\U0001f600\U0001f600"u8));
        Assert.False(reference.IsMatch("\U0001f600\U0001f601"u8));
        AssertOwned(reference);
        var assertion = new Utf8Regex("(?=" + atom + ")" + atom, options);
        Assert.Equal(1, assertion.Count("\U0001f600\U0001f601"u8));
        AssertOwned(assertion);
        var folded = new Utf8Regex("[A" + SparseMembers + "]", options | RegexOptions.IgnoreCase);
        Assert.Equal(2, folded.Count("a\U0001f600\U0001f601"u8));
        AssertOwned(folded);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void LargePredicatePayloadKeepsFullPlaneCategoriesAndComplement(bool compiled)
    {
        foreach (var negated in new[] { false, true })
        {
            var regex = new Utf8Regex("[" + (negated ? "^" : "") + RepeatedCategories + "]", Options(compiled, false));
            Assert.Equal(negated ? 2 : 1, regex.Count("\U00010400\U00010428\U0001f600"u8));
            Assert.Equal(negated ? "\U00010400QQ" : "Q\U00010428\U0001f600",
                regex.ReplaceToString("\U00010400\U00010428\U0001f600"u8, "Q"));
            using var canceled = new CancellationTokenSource(); canceled.Cancel();
            Assert.Equal(canceled.Token, Assert.ThrowsAny<OperationCanceledException>(() =>
                regex.Count("\U00010400"u8, canceled.Token)).CancellationToken);
            Assert.Equal(1, regex.Count("\U00010400\U0001f600"u8));
            AssertOwned(regex);
        }
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void ClassAcquisitionDenialRestoresTheParentAndKeepsWork(bool workLimit)
    {
        var pattern = "[" + SparseMembers + "]";
        using var owner = new Utf8ConstructionScope(Utf8ConstructionScope.DefaultInstructionCapacity,
            workLimit ? Utf8ConstructionScope.DefaultBufferBytes : 8192,
            workLimit ? 4096ul : Utf8ConstructionScope.DefaultWorkSteps, Utf8ConstructionScope.DefaultDepth);
        var held = owner.Budget.AllocateArray<byte>(16, false);
        var before = owner.Budget.ReservedBytes;
        var checkpoint = owner.Budget.ReservedBytes;
        var breach = Assert.Throws<Utf8ResourceLimitExceededException>(() => Utf8OwnedTreeParser.TryParse(pattern, RegexOptions.None));
        Assert.Equal(workLimit ? Utf8ResourceLimitKind.ConstructionWorkSteps : Utf8ResourceLimitKind.ConstructionBytes, breach.ResourceKind);
        Assert.True(breach.Requested > breach.Limit);
        Assert.Equal(before, owner.Budget.ReservedBytes);
        Assert.Equal(0ul, owner.Budget.Reserved); Assert.Equal(0ul, owner.Budget.Depth);
        Assert.Equal(checkpoint, owner.Budget.ReservedBytes);
        Assert.True(owner.Budget.WorkSteps > 0);
        Assert.True(owner.Budget.PeakBytes <= owner.Budget.MaximumBytes);
        if (workLimit)
        {
            // A denied buffer-copy batch has not run and can leave unused
            // work. Admitted work stays spent after rollback. Consume the
            // remainder explicitly before checking the exhausted owner.
            var spent = owner.Budget.WorkSteps;
            Assert.InRange(spent, 1ul, 4096ul);
            owner.Budget.ChargeWork(4096ul - spent);
            Assert.Equal(4096ul, owner.Budget.WorkSteps);
            Assert.Equal(Utf8ResourceLimitKind.ConstructionWorkSteps,
                Assert.Throws<Utf8ResourceLimitExceededException>(() =>
                    Utf8OwnedTreeParser.TryParse("[a]", RegexOptions.None)).ResourceKind);
            Assert.Equal(before, owner.Budget.ReservedBytes);
        }
        else Assert.NotNull(Utf8OwnedTreeParser.TryParse("[a]", RegexOptions.None));
        owner.Budget.ReleaseArray(held, false);
    }

    [Fact]
    public void LargeClassProvidesFixedScalarByteWidthWithoutNativeProjection()
    {
        var tree = Utf8OwnedTreeParser.TryParse("[" + SparseMembers + "]", RegexOptions.CultureInvariant);
        Assert.NotNull(tree);
        var node = tree.Root.Child(0);
        Assert.NotNull(node.ScalarClass);
        Assert.Null(node.Str);
        Assert.Equal(new Utf8OwnedFindOptimizations.ByteWidth(4, 4), Utf8OwnedFindOptimizations.ComputeByteWidth(node));
        Assert.NotNull(Utf8OwnedTreeSliceLowerer.TryLower(tree, RegexOptions.CultureInvariant));
        var semantic = new Utf8SemanticRegex("large class", node.Options, "large class", node.Options, tree);
        var execution = Utf8ExecutionTreeLowerer.Lower(semantic);
        Assert.NotNull(execution);
        Assert.Same(node.ScalarClass, execution.Root.Children[0].Payload);
        Assert.Equal(string.Empty, execution.Root.Children[0].Text);
        var program = Utf8ExecutionProgramLowerer.Lower(execution);
        Assert.NotNull(program);
        var instruction = Assert.Single(program.Instructions.Where(static instruction =>
            instruction.Kind == Utf8ExecutionInstructionKind.Enter && instruction.NodeKind == Utf8ExecutionNodeKind.Set));
        Assert.Same(node.ScalarClass, instruction.Payload);
        Assert.Null(instruction.CharClass);
        foreach (var lazy in new[] { false, true })
        {
            var repeat = Utf8TreeNode.CreateSetRepeat(node, node.Options, 2, 3, lazy);
            Assert.Same(node.ScalarClass, repeat.ScalarClass);
            Assert.Null(repeat.Str);
            Assert.Equal(node.MatchCaseInsensitive, repeat.MatchCaseInsensitive);
            Assert.Equal(lazy ? Utf8TreeNodeKind.Setlazy : Utf8TreeNodeKind.Setloop, repeat.Kind);
            Assert.Equal(new Utf8OwnedFindOptimizations.ByteWidth(8, 12), Utf8OwnedFindOptimizations.ComputeByteWidth(repeat));
        }
    }

    [Theory]
    [InlineData(false, false)] [InlineData(false, true)]
    [InlineData(true, false)] [InlineData(true, true)]
    public void DirectLargeClassQuantificationKeepsItsScalarPayload(bool compiled, bool rtl)
    {
        var options = Options(compiled, rtl);
        var input = "x\U0001f600\U00010400y"u8.ToArray();
        var atom = "[" + SparseMembers + "]";
        foreach (var suffix in new[] { "{2}", "{2}?", "+", "+?" })
        {
            var regex = new Utf8Regex("(?<x>" + atom + suffix + ")", options);
            // The lazy unbounded repeat gives one complete scalar per match.
            var singles = suffix == "+?";
            Assert.True(regex.IsMatch(input));
            Assert.Equal(singles ? 2 : 1, regex.Count(input));
            var expected = singles ? new[] { (1, 4), (5, 4) } : new[] { (1, 8) };
            if (rtl) Array.Reverse(expected);
            var actual = new List<(int, int)>();
            foreach (var value in regex.EnumerateMatches(input)) actual.Add((value.IndexInBytes, value.LengthInBytes));
            Assert.Equal(expected, actual);
            var match = regex.MatchDetailed(input);
            Assert.True(match.Success);
            Assert.Equal(expected[0], (match.IndexInBytes, match.LengthInBytes));
            Assert.Equal(expected[0], (match.GetGroup("x").IndexInBytes, match.GetGroup("x").LengthInBytes));
            var replaced = singles ? "x<\U0001f600><\U00010400>y" : "x<\U0001f600\U00010400>y";
            Assert.Equal(replaced, regex.ReplaceToString(input, "<${x}>"));
            Assert.False(regex.IsMatch("\U0001f601\U0001f601"u8));
            AssertOwned(regex);
        }
    }

    private static RegexOptions Options(bool compiled, bool rtl) => RegexOptions.CultureInvariant |
        (compiled ? RegexOptions.Compiled : 0) | (rtl ? RegexOptions.RightToLeft : 0);

    private static void AssertOwned(Utf8Regex regex)
    {
        Assert.True(regex.Inspection.PreparedRegex.ScalarProgram is not null);
        Assert.True(ReferenceEquals(regex.Inspection.PreparedRegex.SemanticRegex.CaptureCatalog, regex.Inspection.PreparedRegex.ScalarProgram!.CaptureCatalog));
    }
}
