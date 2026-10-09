using System.Buffers;
using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.FrontEnd;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8ScalarClassRepresentationTests
{
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
        var checkpoint = owner.Budget.ObjectCheckpoint;
        var breach = Assert.Throws<Utf8ResourceLimitExceededException>(() => Utf8OwnedTreeParser.TryParse(pattern, RegexOptions.None));
        Assert.Equal(workLimit ? Utf8ResourceLimitKind.ConstructionWorkSteps : Utf8ResourceLimitKind.ConstructionBytes, breach.ResourceKind);
        Assert.True(breach.Requested > breach.Limit);
        Assert.Equal(before, owner.Budget.ReservedBytes);
        Assert.Equal(0ul, owner.Budget.Reserved); Assert.Equal(0ul, owner.Budget.Depth);
        Assert.Same(checkpoint, owner.Budget.ObjectCheckpoint);
        Assert.True(owner.Budget.WorkSteps > 0);
        Assert.True(owner.Budget.PeakBytes <= owner.Budget.MaximumBytes);
        if (workLimit)
        {
            // Failed byte loans roll back; cumulative work does not. An
            // exhausted owner cannot admit even a small subsequent parse.
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
    }

    private static RegexOptions Options(bool compiled, bool rtl) => RegexOptions.CultureInvariant |
        (compiled ? RegexOptions.Compiled : 0) | (rtl ? RegexOptions.RightToLeft : 0);

    private static void AssertOwned(Utf8Regex regex)
    {
        Assert.False(regex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(regex.Inspection.DebugAnchoredFallbackRegexIsCreated);
    }
}
