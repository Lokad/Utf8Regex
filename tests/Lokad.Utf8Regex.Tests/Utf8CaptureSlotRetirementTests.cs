using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.FrontEnd;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8CaptureSlotRetirementTests
{
    [Fact]
    public void ShippedCoreRetainsNoSecondCaptureNumberingTranslator()
    {
        Assert.Null(typeof(Utf8Regex).Assembly.GetType("Lokad.Utf8Regex.Internal.Execution.Utf8BclSlotTranslator"));
    }

    [Theory]
    [InlineData("(?<outer>a(?<inner>😀)b)", false)]
    [InlineData("(a)(?<outer>(?<inner>😀))", false)]
    [InlineData("(?<outer>a(?<inner>😀)?b)", false)]
    [InlineData("(?<outer>a(?<inner>é|😀)b)", true)]
    [InlineData("(?<outer>(?<inner>a|b)(?:é|😀))", true)]
    [InlineData("(?<outer>(?<left>a)?(?<right>😀)??b)", true)]
    [InlineData("(?<piece>é|😀){2}", true)]
    public void CaptureMapsPreserveAlreadyFinalizedCatalogNumbers(string pattern, bool alternation)
    {
        var analyzed = Utf8FrontEnd.Analyze(pattern, RegexOptions.CultureInvariant);
        var prepared = Lokad.Utf8Regex.Internal.Planning.Utf8RegexPreparer.Prepare(analyzed);
        var tree = Utf8ExecutionTreeLowerer.Lower(analyzed.SemanticRegex);
        Assert.NotNull(tree);
        var catalog = prepared.SemanticRegex.CaptureCatalog!;
        Assert.Same(catalog, prepared.ScalarProgram!.CaptureCatalog);
        Assert.False(catalog.HasSharedCaptureDeclarations);
        Assert.False(catalog.HasExplicitCaptureNumbers);
        IEnumerable<int> mapped;
        if (alternation)
        {
            var map = Assert.IsType<Utf8AlternationBranchSpanMap>(Utf8AlternationBranchSpanMap.TryCreate(tree, prepared.ScalarProgram!));
            mapped = map.WholeMatchGroups.Concat(map.BranchGroups.SelectMany(static groups => groups));
        }
        else
        {
            var table = Assert.IsType<Utf8GroupedLiteralSpanTable>(Utf8GroupedLiteralSpanTable.TryCreate(tree, prepared.ScalarProgram!));
            mapped = table.Groups;
        }
        Assert.Equal(catalog.GroupNumbers.Skip(1), mapped.Distinct().Order());
        var pending = new Stack<Utf8ExecutionNode>();
        pending.Push(tree!.Root);
        var loweredNumbers = new List<int>();
        while (pending.TryPop(out var node))
        {
            if (node.Kind == Utf8ExecutionNodeKind.Capture && node.CaptureNumber > 0) loweredNumbers.Add(node.CaptureNumber);
            foreach (var child in node.Children) pending.Push(child);
        }
        Assert.Equal(catalog.GroupNumbers.Skip(1), loweredNumbers.Distinct().Order());
    }

    [Theory]
    [InlineData("(?<2>é)(?<5>😀)")]
    [InlineData("(?<x>é)(?<x>😀)")]
    [InlineData("(?<x>a)|(?<x>😀)")]
    [InlineData("(?<x>é)(?<y-x>😀)")]
    public void StatefulCaptureCatalogsDeclineBeforeScratchAndPreserveParentLoans(string pattern)
    {
        var analyzed = Utf8FrontEnd.Analyze(pattern, RegexOptions.CultureInvariant);
        var prepared = Lokad.Utf8Regex.Internal.Planning.Utf8RegexPreparer.Prepare(analyzed);
        var tree = Utf8ExecutionTreeLowerer.Lower(analyzed.SemanticRegex);
        Assert.NotNull(tree);
        var program = prepared.ScalarProgram!;
        Assert.True(program.HasBalancing || program.HasSharedCaptureDeclarations || program.HasExplicitCaptureNumbers);
        // One work step belongs to acquiring the parent array; no work or
        // storage remains available to the map producers after that setup.
        using var owner = new Utf8ConstructionScope(ulong.MaxValue, 48, 1, 0);
        var parent = owner.Budget.AllocateArray<byte>(1, false);
        parent[0] = 0xa5;
        Assert.Null(Utf8GroupedLiteralSpanTable.TryCreate(tree, program));
        Assert.Null(Utf8AlternationBranchSpanMap.TryCreate(tree, program));
        Assert.Equal(48ul, owner.Budget.ReservedBytes);
        Assert.Equal(48ul, owner.Budget.PeakBytes);
        Assert.Equal(1ul, owner.Budget.WorkSteps);
        Assert.Equal(0ul, owner.Budget.Depth);

        Assert.Equal((byte)0xa5, parent[0]);
        owner.Budget.ReleaseArray(parent, false);
    }

    public static IEnumerable<object[]> PublicCases()
    {
        (string Pattern, string Text)[] cases =
        [
            ("(?<outer>a(?<inner>😀)b)", "xa😀by"),
            ("(a)(?<outer>(?<inner>😀))", "xa😀y"),
            ("(?<outer>a(?<inner>😀)?b)", "ab a😀b"),
            ("(?<outer>a(?<inner>é|😀)b)", "aéb a😀b"),
            ("(?<outer>(?<inner>a|b)(?:é|😀))", "aé b😀"),
            ("(?<outer>(?<left>a)?(?<right>😀)??b)", "b ab 😀b a😀b"),
            ("(?<piece>é|😀){2}", "é😀 😀é"),
            ("(?<2>é)(?<5>😀)", "xé😀y"),
            ("(?<x>é)(?<x>😀)", "xé😀y"),
            ("(?<x>a)|(?<x>😀)", "a😀a"),
            ("(?<x>é)(?<y-x>😀)", "xé😀y"),
        ];
        foreach (var (pattern, text) in cases)
        foreach (var options in new[] { RegexOptions.None, RegexOptions.Compiled, RegexOptions.RightToLeft, RegexOptions.RightToLeft | RegexOptions.Compiled })
            yield return [pattern, text, options | RegexOptions.CultureInvariant];
    }

    [Theory]
    [MemberData(nameof(PublicCases))]
    public void PublicScalarCapturesRetainNamesNumbersRangesAndCompleteHistories(string pattern, string text, RegexOptions options)
    {
        // These patterns contain whole scalar literals only, so this comparator
        // overlaps the UTF-8 contract even with supplementary characters.
        var oracle = new Regex(pattern, options & ~RegexOptions.Compiled);
        var regex = new Utf8Regex(pattern, options);
        Assert.Equal(oracle.GetGroupNumbers(), regex.GetGroupNumbers());
        Assert.Equal(oracle.GetGroupNames(), regex.GetGroupNames());
        foreach (var name in oracle.GetGroupNames()) Assert.Equal(oracle.GroupNumberFromName(name), regex.GroupNumberFromName(name));
        Utf8SliceSparseGroupTests.AssertOwnedOperations(regex, oracle, [text]);
        var input = Encoding.UTF8.GetBytes(text);
        var hit = regex.MatchDetailed(input);
        for (var ordinal = 0; ordinal < regex.GetGroupNumbers().Length; ordinal++)
        {
            var group = hit.GetGroup(regex.GetGroupNumbers()[ordinal]);
            for (var capture = 0; capture < group.CaptureCount; capture++) Assert.True(group.GetCapture(capture).IsByteAligned);
        }
    }
}
