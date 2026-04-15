using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.FrontEnd;
using Lokad.Utf8Regex.Internal.Planning;
using RuntimeFrontEnd = Lokad.Utf8Regex.Internal.FrontEnd.Runtime;

namespace Lokad.Utf8Regex.Tests;

public sealed class FrontEndRuntimeSmokeTests
{
    [Fact]
    public void VendoredRegexNodeKindMapsToVendoredOpcodes()
    {
        Assert.Equal(
            (int)RuntimeFrontEnd.RegexOpcode.One,
            (int)RuntimeFrontEnd.RegexNodeKind.One);
        Assert.Equal(
            (int)RuntimeFrontEnd.RegexOpcode.Setloopatomic,
            (int)RuntimeFrontEnd.RegexNodeKind.Setloopatomic);
    }

    [Fact]
    public void VendoredRegexTreeStoresRuntimeShapedNodeGraph()
    {
        var root = new RuntimeFrontEnd.RegexNode(RuntimeFrontEnd.RegexNodeKind.Concatenate, RegexOptions.None)
            .AddChild(new RuntimeFrontEnd.RegexNode(RuntimeFrontEnd.RegexNodeKind.One, RegexOptions.None, 'a'))
            .AddChild(new RuntimeFrontEnd.RegexNode(RuntimeFrontEnd.RegexNodeKind.One, RegexOptions.None, 'b'));
        var findOptimizations = new RuntimeFrontEnd.RegexFindOptimizations(root, RegexOptions.None, CultureInfo.InvariantCulture);
        var tree = new RuntimeFrontEnd.RegexTree(
            root,
            1,
            null,
            null,
            null,
            RegexOptions.None,
            CultureInfo.InvariantCulture,
            findOptimizations);

        Assert.Same(root, tree.Root);
        Assert.Equal(2, tree.Root.ChildCount);
        Assert.Equal('a', tree.Root.Child(0).Ch);
        Assert.Equal('b', tree.Root.Child(1).Ch);
        Assert.Same(root, tree.Root.Child(0).Parent);
        Assert.IsType<List<RuntimeFrontEnd.RegexNode>>(tree.Root.Children);
        Assert.Same(findOptimizations, tree.FindOptimizations);
        Assert.Equal(CultureInfo.InvariantCulture, tree.Culture);
    }

    [Fact]
    public void VendoredRegexNodeCanReduceNestedConcatsAndAlternates()
    {
        var concat = new RuntimeFrontEnd.RegexNode(RuntimeFrontEnd.RegexNodeKind.Concatenate, RegexOptions.None)
            .AddChild(new RuntimeFrontEnd.RegexNode(RuntimeFrontEnd.RegexNodeKind.One, RegexOptions.None, 'a'))
            .AddChild(new RuntimeFrontEnd.RegexNode(RuntimeFrontEnd.RegexNodeKind.Empty, RegexOptions.None))
            .AddChild(new RuntimeFrontEnd.RegexNode(RuntimeFrontEnd.RegexNodeKind.Concatenate, RegexOptions.None)
                .AddChild(new RuntimeFrontEnd.RegexNode(RuntimeFrontEnd.RegexNodeKind.One, RegexOptions.None, 'b'))
                .AddChild(new RuntimeFrontEnd.RegexNode(RuntimeFrontEnd.RegexNodeKind.One, RegexOptions.None, 'c')));
        var alternate = new RuntimeFrontEnd.RegexNode(RuntimeFrontEnd.RegexNodeKind.Alternate, RegexOptions.None)
            .AddChild(new RuntimeFrontEnd.RegexNode(RuntimeFrontEnd.RegexNodeKind.Empty, RegexOptions.None))
            .AddChild(new RuntimeFrontEnd.RegexNode(RuntimeFrontEnd.RegexNodeKind.Alternate, RegexOptions.None)
                .AddChild(new RuntimeFrontEnd.RegexNode(RuntimeFrontEnd.RegexNodeKind.Empty, RegexOptions.None))
                .AddChild(new RuntimeFrontEnd.RegexNode(RuntimeFrontEnd.RegexNodeKind.One, RegexOptions.None, 'x')));

        var reducedConcat = concat.Reduce();
        var reducedAlternate = alternate.Reduce();

        Assert.Equal(RuntimeFrontEnd.RegexNodeKind.Multi, reducedConcat.Kind);
        Assert.Equal("abc", reducedConcat.Str);
        Assert.Equal(RuntimeFrontEnd.RegexNodeKind.Alternate, reducedAlternate.Kind);
        Assert.Equal(2, reducedAlternate.ChildCount);
        Assert.Equal(RuntimeFrontEnd.RegexNodeKind.Empty, reducedAlternate.Child(0).Kind);
        Assert.Equal(RuntimeFrontEnd.RegexNodeKind.One, reducedAlternate.Child(1).Kind);
    }

    [Fact]
    public void VendoredRegexFindOptimizationsCanExposeLeadingLiteral()
    {
        var root = new RuntimeFrontEnd.RegexNode(RuntimeFrontEnd.RegexNodeKind.Capture, RegexOptions.None, 0, -1)
            .AddChild(new RuntimeFrontEnd.RegexNode(RuntimeFrontEnd.RegexNodeKind.Concatenate, RegexOptions.None)
                .AddChild(new RuntimeFrontEnd.RegexNode(RuntimeFrontEnd.RegexNodeKind.Bol, RegexOptions.None))
                .AddChild(new RuntimeFrontEnd.RegexNode(RuntimeFrontEnd.RegexNodeKind.Multi, RegexOptions.None, "abc")));

        var findOptimizations = new RuntimeFrontEnd.RegexFindOptimizations(root, RegexOptions.None, CultureInfo.InvariantCulture);

        Assert.Equal("abc", findOptimizations.LeadingLiteral);
    }

    [Fact]
    public void VendoredRegexFindOptimizationsCanExposeLongestLiteralAndAlternatePrefixes()
    {
        var root = new RuntimeFrontEnd.RegexNode(RuntimeFrontEnd.RegexNodeKind.Capture, RegexOptions.None, 0, -1)
            .AddChild(new RuntimeFrontEnd.RegexNode(RuntimeFrontEnd.RegexNodeKind.Alternate, RegexOptions.None)
                .AddChild(new RuntimeFrontEnd.RegexNode(RuntimeFrontEnd.RegexNodeKind.Multi, RegexOptions.None, "cat"))
                .AddChild(new RuntimeFrontEnd.RegexNode(RuntimeFrontEnd.RegexNodeKind.Concatenate, RegexOptions.None)
                    .AddChild(new RuntimeFrontEnd.RegexNode(RuntimeFrontEnd.RegexNodeKind.Bol, RegexOptions.None))
                    .AddChild(new RuntimeFrontEnd.RegexNode(RuntimeFrontEnd.RegexNodeKind.Multi, RegexOptions.None, "horse"))));

        var findOptimizations = new RuntimeFrontEnd.RegexFindOptimizations(root, RegexOptions.None, CultureInfo.InvariantCulture);

        Assert.Equal("horse", findOptimizations.LongestLiteral);
        Assert.NotNull(findOptimizations.AlternatePrefixes);
        Assert.Equal(["cat", "horse"], findOptimizations.AlternatePrefixes);
    }

    [Fact]
    public void VendoredRegexFindOptimizationsCanExposeFixedDistanceSets()
    {
        var tree = RuntimeFrontEnd.RegexParser.Parse("ab[0-9][0-9]cd", RegexOptions.CultureInvariant, CultureInfo.InvariantCulture);
        var findOptimizations = tree.FindOptimizations;

        Assert.NotNull(findOptimizations);
        Assert.NotNull(findOptimizations.FixedDistanceSets);
        Assert.Collection(
            findOptimizations.FixedDistanceSets!,
            set =>
            {
                Assert.Equal(2, set.Distance);
                Assert.NotNull(set.Range);
                Assert.Equal('0', set.Range!.Value.LowInclusive);
                Assert.Equal('9', set.Range!.Value.HighInclusive);
            },
            set =>
            {
                Assert.Equal(3, set.Distance);
                Assert.NotNull(set.Range);
                Assert.Equal('0', set.Range!.Value.LowInclusive);
                Assert.Equal('9', set.Range!.Value.HighInclusive);
            });
    }

    [Fact]
    public void VendoredRegexPrefixAnalyzerCanExtractPrefixesFromAlternation()
    {
        var root = new RuntimeFrontEnd.RegexNode(RuntimeFrontEnd.RegexNodeKind.Capture, RegexOptions.None, 0, -1)
            .AddChild(new RuntimeFrontEnd.RegexNode(RuntimeFrontEnd.RegexNodeKind.Alternate, RegexOptions.None)
                .AddChild(new RuntimeFrontEnd.RegexNode(RuntimeFrontEnd.RegexNodeKind.Multi, RegexOptions.None, "cat"))
                .AddChild(new RuntimeFrontEnd.RegexNode(RuntimeFrontEnd.RegexNodeKind.Multi, RegexOptions.None, "horse")));

        var prefixes = RuntimeFrontEnd.RegexPrefixAnalyzer.FindPrefixes(root, ignoreCase: false);

        Assert.NotNull(prefixes);
        Assert.Equal(["cat", "horse"], prefixes);
    }

    [Fact]
    public void FrontEndAnalysisCarriesSemanticSource()
    {
        var literal = Utf8FrontEnd.Analyze("abc", RegexOptions.None);
        var simple = Utf8FrontEnd.Analyze("a.c", RegexOptions.CultureInvariant);
        var fallback = Utf8FrontEnd.Analyze("(?!a)b", RegexOptions.CultureInvariant);

        Assert.Equal(Utf8SemanticSource.NativeLiteral, literal.SemanticRegex.Source);
        Assert.Equal(Utf8SemanticSource.NativeSimplePattern, simple.SemanticRegex.Source);
        Assert.Equal(Utf8SemanticSource.FallbackRegex, fallback.SemanticRegex.Source);
    }

    [Fact]
    public void FrontEndDerivesSemanticSourceFromAnalyzedTreeInsteadOfSupportClassification()
    {
        var analysis = Utf8FrontEnd.Analyze(@"(ab.cd)", RegexOptions.CultureInvariant);

        Assert.Equal(Utf8SemanticSource.NativeSimplePattern, analysis.SemanticRegex.Source);
        Assert.Equal(NativeExecutionKind.AsciiSimplePattern, analysis.AnalyzedRegex.ExecutionKind);
    }

    [Fact]
    public void FrontEndNormalizesLeadingGlobalInlineOptionsForExecution()
    {
        var analysis = Utf8FrontEnd.Analyze("(?i)abc", RegexOptions.CultureInvariant);

        Assert.Equal(Utf8SemanticSource.NativeLiteral, analysis.SemanticRegex.Source);
        Assert.Equal("abc", analysis.SemanticRegex.ExecutionPattern);
        Assert.Equal(RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, analysis.SemanticRegex.ExecutionOptions);
        Assert.Equal("abc", analysis.AnalyzedRegex.ExecutionPattern);
        Assert.Equal(NativeExecutionKind.AsciiLiteralIgnoreCase, analysis.RegexPlan.ExecutionKind);
    }

    [Fact]
    public void FrontEndNormalizesLeadingGlobalInlineOptionsForSimplePatternExecution()
    {
        var analysis = Utf8FrontEnd.Analyze("(?i)a.c", RegexOptions.CultureInvariant);

        Assert.Equal(Utf8SemanticSource.NativeSimplePattern, analysis.SemanticRegex.Source);
        Assert.Equal("a.c", analysis.SemanticRegex.ExecutionPattern);
        Assert.Equal(RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, analysis.SemanticRegex.ExecutionOptions);
        Assert.Equal("a.c", analysis.AnalyzedRegex.ExecutionPattern);
        Assert.Equal(NativeExecutionKind.AsciiSimplePattern, analysis.RegexPlan.ExecutionKind);
        Assert.True(analysis.RegexPlan.SimplePatternPlan.IgnoreCase);
    }

    [Fact]
    public void FrontEndAnalysisCarriesLoweredRegexPlan()
    {
        var literal = Utf8FrontEnd.Analyze("abc", RegexOptions.None);
        var simple = Utf8FrontEnd.Analyze("a.c", RegexOptions.CultureInvariant);
        var fallback = Utf8FrontEnd.Analyze("(?!a)b", RegexOptions.CultureInvariant);

        Assert.Equal(NativeExecutionKind.ExactAsciiLiteral, literal.RegexPlan.ExecutionKind);
        Assert.Equal(Utf8ExecutionBackend.NativeLiteral, literal.RegexPlan.ExecutionBackend);
        Assert.Equal(Utf8SearchKind.ExactAsciiLiteral, literal.RegexPlan.SearchPlan.Kind);
        Assert.Equal(NativeExecutionKind.AsciiSimplePattern, simple.RegexPlan.ExecutionKind);
        Assert.Equal(NativeExecutionKind.FallbackRegex, fallback.RegexPlan.ExecutionKind);
        Assert.Equal(Utf8ExecutionBackend.FallbackRegex, fallback.RegexPlan.ExecutionBackend);
        Assert.Equal("unsupported_lookaround", fallback.RegexPlan.FallbackReason);
    }

    [Fact]
    public void FrontEndAnalysisCarriesGeneralExecutionTree()
    {
        var literal = Utf8FrontEnd.Analyze("abc", RegexOptions.None);
        var simple = Utf8FrontEnd.Analyze("^a[0-9]{2,4}$", RegexOptions.CultureInvariant);
        var fallback = Utf8FrontEnd.Analyze("(?=ab)ab", RegexOptions.CultureInvariant);

        Assert.NotNull(literal.RegexPlan.ExecutionTree);
        Assert.Equal(Utf8ExecutionNodeKind.Capture, literal.RegexPlan.ExecutionTree!.Root.Kind);
        Assert.Equal(Utf8ExecutionNodeKind.Multi, literal.RegexPlan.ExecutionTree.Root.Children[0].Kind);
        Assert.Equal("abc", literal.RegexPlan.ExecutionTree.Root.Children[0].Text);

        Assert.NotNull(simple.RegexPlan.ExecutionTree);
        Assert.Equal(Utf8ExecutionNodeKind.Concatenate, simple.RegexPlan.ExecutionTree!.Root.Children[0].Kind);
        Assert.Equal(Utf8ExecutionNodeKind.Loop, simple.RegexPlan.ExecutionTree.Root.Children[0].Children[2].Kind);

        Assert.NotNull(fallback.RegexPlan.ExecutionTree);
        Assert.Equal(Utf8ExecutionNodeKind.Concatenate, fallback.RegexPlan.ExecutionTree!.Root.Children[0].Kind);
        Assert.Equal(Utf8ExecutionNodeKind.PositiveLookaround, fallback.RegexPlan.ExecutionTree.Root.Children[0].Children[0].Kind);
        Assert.Equal(0, literal.RegexPlan.ExecutionTree.Root.CaptureNumber);
    }

    [Fact]
    public void FrontEndAnalysisCarriesLinearExecutionProgram()
    {
        var literal = Utf8FrontEnd.Analyze("abc", RegexOptions.None);
        var simple = Utf8FrontEnd.Analyze("^a[0-9]{2,4}$", RegexOptions.CultureInvariant);

        Assert.NotNull(literal.RegexPlan.ExecutionProgram);
        Assert.NotEmpty(literal.RegexPlan.ExecutionProgram!.Instructions);
        Assert.Equal(Utf8ExecutionInstructionKind.Enter, literal.RegexPlan.ExecutionProgram.Instructions[0].Kind);
        Assert.Equal(Utf8ExecutionNodeKind.Capture, literal.RegexPlan.ExecutionProgram.Instructions[0].NodeKind);
        Assert.Equal(Utf8ExecutionInstructionKind.Exit, literal.RegexPlan.ExecutionProgram.Instructions[^1].Kind);
        Assert.Equal(literal.RegexPlan.ExecutionProgram.Instructions.Count - 1, literal.RegexPlan.ExecutionProgram.Instructions[0].PartnerIndex);
        Assert.Equal(0, literal.RegexPlan.ExecutionProgram.Instructions[^1].PartnerIndex);

        Assert.NotNull(simple.RegexPlan.ExecutionProgram);
        Assert.Contains(simple.RegexPlan.ExecutionProgram!.Instructions, instruction =>
            instruction.Kind == Utf8ExecutionInstructionKind.Enter &&
            instruction.NodeKind == Utf8ExecutionNodeKind.Loop &&
            instruction.Min == 2 &&
            instruction.Max == 4);
    }

    [Fact]
    public void FrontEndExecutionIrCarriesCaptureAndBackreferenceOperands()
    {
        var analysis = Utf8FrontEnd.Analyze(@"(?<word>ab)\k<word>", RegexOptions.CultureInvariant);
        var body = analysis.RegexPlan.ExecutionTree!.Root.Children[0];
        var capture = body.Children[0];
        var backreference = body.Children[1];

        Assert.Equal(Utf8ExecutionNodeKind.Capture, capture.Kind);
        Assert.Equal(1, capture.CaptureNumber);
        Assert.Equal(Utf8ExecutionNodeKind.Backreference, backreference.Kind);
        Assert.Equal(1, backreference.CaptureNumber);
        Assert.Equal("word", backreference.Text);

        Assert.Contains(analysis.RegexPlan.ExecutionProgram!.Instructions, instruction =>
            instruction.Kind == Utf8ExecutionInstructionKind.Enter &&
            instruction.NodeKind == Utf8ExecutionNodeKind.Capture &&
            instruction.CaptureNumber == 1);
    }

    [Fact]
    public void ExecutionInterpreterCanMatchLiteralPrefixFromLinearProgram()
    {
        var analysis = Utf8FrontEnd.Analyze("abc", RegexOptions.None);

        Assert.True(Utf8ExecutionInterpreter.TryMatchLiteralPrefix("abcdef"u8, analysis.RegexPlan.ExecutionProgram, ignoreCase: false, budget: null, out var matchedLength));
        Assert.Equal(3, matchedLength);
        Assert.False(Utf8ExecutionInterpreter.TryMatchLiteralPrefix("abxdef"u8, analysis.RegexPlan.ExecutionProgram, ignoreCase: false, budget: null, out _));
    }

    [Fact]
    public void ExecutionInterpreterCanMatchIgnoreCaseLiteralPrefixFromLinearProgram()
    {
        var analysis = Utf8FrontEnd.Analyze("(?i)abc", RegexOptions.CultureInvariant);

        Assert.True(Utf8ExecutionInterpreter.TryMatchLiteralPrefix("ABCdef"u8, analysis.RegexPlan.ExecutionProgram, ignoreCase: true, budget: null, out var matchedLength));
        Assert.Equal(3, matchedLength);
    }

    [Fact]
    public void ExecutionInterpreterCanMatchAnchoredFiniteSimplePatternFromLinearProgram()
    {
        var analysis = Utf8FrontEnd.Analyze("^ab[0-9]{2,4}$", RegexOptions.CultureInvariant);

        Assert.True(Utf8ExecutionInterpreter.TryMatchPrefix("ab123"u8, analysis.RegexPlan.ExecutionProgram, 0, out var matchedLength));
        Assert.Equal(5, matchedLength);
        Assert.False(Utf8ExecutionInterpreter.TryMatchPrefix("xab123"u8, analysis.RegexPlan.ExecutionProgram, 1, out _));
        Assert.False(Utf8ExecutionInterpreter.TryMatchPrefix("ab1x"u8, analysis.RegexPlan.ExecutionProgram, 0, out _));
    }

    [Fact]
    public void ExecutionInterpreterCanMatchAlternationFromLinearProgram()
    {
        var analysis = Utf8FrontEnd.Analyze("cat|horse", RegexOptions.CultureInvariant);

        Assert.True(Utf8ExecutionInterpreter.TryMatchPrefix("horse!"u8, analysis.RegexPlan.ExecutionProgram, 0, out var matchedLength));
        Assert.Equal(5, matchedLength);
        Assert.False(Utf8ExecutionInterpreter.TryMatchPrefix("dog"u8, analysis.RegexPlan.ExecutionProgram, 0, out _));
    }

    [Fact]
    public void ExecutionInterpreterPreservesOptionalCaptureSlots()
    {
        var analysis = Utf8FrontEnd.Analyze("(a)(b)?", RegexOptions.CultureInvariant);
        var captures = new Utf8CaptureSlots(3);

        Assert.True(Utf8ExecutionInterpreter.TryMatchPrefix("ab"u8, analysis.RegexPlan.ExecutionProgram, 0, captures, budget: null, out var matchedLength));
        Assert.Equal(2, matchedLength);
        Assert.True(captures.TryGet(1, out var firstStart, out var firstLength));
        Assert.Equal(0, firstStart);
        Assert.Equal(1, firstLength);
        Assert.True(captures.TryGet(2, out var secondStart, out var secondLength));
        Assert.Equal(1, secondStart);
        Assert.Equal(1, secondLength);

        Assert.True(Utf8ExecutionInterpreter.TryMatchPrefix("a"u8, analysis.RegexPlan.ExecutionProgram, 0, captures, budget: null, out matchedLength));
        Assert.Equal(1, matchedLength);
        Assert.True(captures.TryGet(1, out firstStart, out firstLength));
        Assert.Equal(0, firstStart);
        Assert.Equal(1, firstLength);
        Assert.False(captures.TryGet(2, out _, out _));
    }

    [Fact]
    public void FrontEndAnalysisCarriesRepoOwnedAnalyzedRegex()
    {
        var literal = Utf8FrontEnd.Analyze("abc", RegexOptions.None);
        var simple = Utf8FrontEnd.Analyze("a.c", RegexOptions.CultureInvariant);

        Assert.Equal(Utf8SemanticSource.NativeLiteral, literal.AnalyzedRegex.SemanticRegex.Source);
        Assert.Equal(1, literal.AnalyzedRegex.Features.CaptureCount);
        Assert.False(literal.AnalyzedRegex.Features.HasAlternation);
        Assert.Equal(Utf8SearchKind.ExactAsciiLiteral, literal.AnalyzedRegex.SearchInfo.Kind);
        Assert.Equal(NativeExecutionKind.ExactAsciiLiteral, literal.AnalyzedRegex.ExecutionKind);

        Assert.Equal(Utf8SemanticSource.NativeSimplePattern, simple.AnalyzedRegex.SemanticRegex.Source);
        Assert.False(simple.AnalyzedRegex.Features.HasNamedCaptures);
        Assert.Equal(NativeExecutionKind.AsciiSimplePattern, simple.AnalyzedRegex.ExecutionKind);
    }

    [Fact]
    public void FrontEndSearchAnalyzerCarriesBoundaryRequirementsForFallbackTrees()
    {
        var analysis = Utf8FrontEnd.Analyze(@"\bfoo.*bar\b", RegexOptions.CultureInvariant);

        Assert.Equal(Utf8BoundaryRequirement.Boundary, analysis.AnalyzedRegex.SearchInfo.LeadingBoundary);
        Assert.Equal(Utf8BoundaryRequirement.Boundary, analysis.AnalyzedRegex.SearchInfo.TrailingBoundary);
        Assert.True(analysis.RegexPlan.StructuralSearchPlan.HasValue);
        Assert.Contains(
            analysis.RegexPlan.StructuralSearchPlan.Stages!,
            static stage => stage.Kind == Utf8StructuralSearchStageKind.RequireLeadingBoundary &&
                            stage.BoundaryRequirement == Utf8BoundaryRequirement.Boundary);
        Assert.Contains(
            analysis.RegexPlan.StructuralSearchPlan.Stages!,
            static stage => stage.Kind == Utf8StructuralSearchStageKind.RequireTrailingBoundary &&
                            stage.BoundaryRequirement == Utf8BoundaryRequirement.Boundary);
    }

    [Fact]
    public void FrontEndAnalyzedLiteralCanBeDerivedFromRuntimeTree()
    {
        var literal = Utf8FrontEnd.Analyze("abc", RegexOptions.None);

        Assert.NotNull(literal.SemanticRegex.RuntimeTree);
        Assert.Equal(NativeExecutionKind.ExactAsciiLiteral, literal.AnalyzedRegex.ExecutionKind);
        Assert.Equal(Utf8SearchKind.ExactAsciiLiteral, literal.AnalyzedRegex.SearchInfo.Kind);
        Assert.Equal("abc", Encoding.UTF8.GetString(literal.AnalyzedRegex.LiteralUtf8!));
    }

    [Fact]
    public void FrontEndAnalyzedUtf8LiteralCanBeDerivedWithoutFallback()
    {
        var literal = Utf8FrontEnd.Analyze("café", RegexOptions.CultureInvariant);

        Assert.Equal(NativeExecutionKind.ExactUtf8Literal, literal.AnalyzedRegex.ExecutionKind);
        Assert.Equal(Utf8SearchKind.ExactAsciiLiteral, literal.AnalyzedRegex.SearchInfo.Kind);
        Assert.Equal("café", Encoding.UTF8.GetString(literal.AnalyzedRegex.LiteralUtf8!));
    }

    [Fact]
    public void FrontEndAnalyzedBoundaryWrappedUtf8LiteralAlternationCanBeDerivedWithoutFallback()
    {
        var analysis = Utf8FrontEnd.Analyze(@"\b(?:café|niño)\b", RegexOptions.CultureInvariant);

        Assert.Equal(NativeExecutionKind.ExactUtf8Literals, analysis.AnalyzedRegex.ExecutionKind);
        Assert.Equal(Utf8SearchKind.ExactUtf8Literals, analysis.AnalyzedRegex.SearchInfo.Kind);
        Assert.Equal(Utf8BoundaryRequirement.Boundary, analysis.AnalyzedRegex.SearchInfo.LeadingBoundary);
        Assert.Equal(Utf8BoundaryRequirement.Boundary, analysis.AnalyzedRegex.SearchInfo.TrailingBoundary);
        Assert.Equal(["café", "niño"], analysis.AnalyzedRegex.SearchInfo.AlternateLiteralsUtf8!.Select(Encoding.UTF8.GetString));
    }

    [Fact]
    public void FrontEndAnalyzedIdentifierFamilyUsesStructuralExecution()
    {
        var analysis = Utf8FrontEnd.Analyze(@"\b(?:Get|TryGet|Create|Load)[A-Z][A-Za-z0-9]+Async\b", RegexOptions.CultureInvariant);

        Assert.Equal(NativeExecutionKind.AsciiStructuralIdentifierFamily, analysis.AnalyzedRegex.ExecutionKind);
        Assert.Equal(Utf8SearchKind.ExactAsciiLiterals, analysis.AnalyzedRegex.SearchInfo.Kind);
        Assert.Equal(Utf8BoundaryRequirement.Boundary, analysis.AnalyzedRegex.SearchInfo.LeadingBoundary);
    }

    [Fact]
    public void FrontEndAnalyzedUtf8LiteralLookaheadCanBeDerivedWithoutFallback()
    {
        var analysis = Utf8FrontEnd.Analyze("café(?= noir)", RegexOptions.CultureInvariant);

        Assert.Equal(NativeExecutionKind.ExactUtf8Literal, analysis.AnalyzedRegex.ExecutionKind);
        Assert.Equal(Utf8SearchKind.ExactAsciiLiteral, analysis.AnalyzedRegex.SearchInfo.Kind);
        Assert.Equal("café", Encoding.UTF8.GetString(analysis.AnalyzedRegex.LiteralUtf8!));
        Assert.Equal(" noir", Encoding.UTF8.GetString(analysis.AnalyzedRegex.SearchInfo.TrailingLiteralUtf8!));
    }

    [Fact]
    public void FrontEndLoweredLiteralSearchPlanCanBeDerivedFromRuntimeTree()
    {
        var literal = Utf8FrontEnd.Analyze("abc", RegexOptions.None);

        Assert.Equal(Utf8SearchKind.ExactAsciiLiteral, literal.RegexPlan.SearchPlan.Kind);
        Assert.Equal("abc", Encoding.UTF8.GetString(literal.RegexPlan.LiteralUtf8!));
    }

    [Fact]
    public void FrontEndAnalyzedSimplePatternCanBeDerivedFromRuntimeTree()
    {
        var simple = Utf8FrontEnd.Analyze("^ab.c$", RegexOptions.CultureInvariant);

        Assert.NotNull(simple.SemanticRegex.RuntimeTree);
        Assert.Equal(NativeExecutionKind.AsciiSimplePattern, simple.AnalyzedRegex.ExecutionKind);
        Assert.Equal(Utf8SearchKind.TrailingAnchorFixedLengthEndZ, simple.AnalyzedRegex.SearchInfo.Kind);
        Assert.Equal(4, simple.AnalyzedRegex.SearchInfo.MinRequiredLength);
        Assert.True(simple.AnalyzedRegex.SimplePatternPlan.IsStartAnchored);
        Assert.True(simple.AnalyzedRegex.SimplePatternPlan.IsEndAnchored);
        Assert.False(simple.AnalyzedRegex.SimplePatternPlan.IsUtf8ByteSafe);
    }

    [Fact]
    public void FrontEndMarksAsciiClassAndLiteralSimplePatternAsUtf8ByteSafe()
    {
        var simple = Utf8FrontEnd.Analyze("((?:ASIA|AKIA|AROA|AIDA)([A-Z0-7]{16}))", RegexOptions.CultureInvariant);

        Assert.Equal(NativeExecutionKind.AsciiSimplePattern, simple.AnalyzedRegex.ExecutionKind);
        Assert.True(simple.AnalyzedRegex.SimplePatternPlan.IsUtf8ByteSafe);
    }

    [Fact]
    public void FrontEndCanLowerGroupedFiniteSimplePatternFromRuntimeTree()
    {
        var simple = Utf8FrontEnd.Analyze("(?:ab|cd){2}", RegexOptions.CultureInvariant);

        Assert.Equal(NativeExecutionKind.ExactUtf8Literals, simple.AnalyzedRegex.ExecutionKind);
        Assert.Equal(Utf8SearchKind.ExactAsciiLiterals, simple.AnalyzedRegex.SearchInfo.Kind);
        Assert.Equal(
            ["abab", "abcd", "cdab", "cdcd"],
            simple.AnalyzedRegex.SearchInfo.AlternateLiteralsUtf8!
                .Select(Encoding.UTF8.GetString)
                .OrderBy(static value => value, StringComparer.Ordinal));
    }

    [Fact]
    public void FrontEndAnalyzedAlternationSearchLiteralCanBeDerivedFromRuntimeTree()
    {
        var simple = Utf8FrontEnd.Analyze("cat|horse", RegexOptions.CultureInvariant);

        Assert.NotNull(simple.SemanticRegex.RuntimeTree);
        Assert.Equal(Utf8SearchKind.ExactAsciiLiterals, simple.AnalyzedRegex.SearchInfo.Kind);
        Assert.NotNull(simple.AnalyzedRegex.SearchInfo.AlternateLiteralsUtf8);
        Assert.Equal(["cat", "horse"], simple.AnalyzedRegex.SearchInfo.AlternateLiteralsUtf8!.Select(Encoding.UTF8.GetString));
    }

    [Fact]
    public void FrontEndSearchAnalyzerCanDeriveLiteralAndSimplePatternSearchInfo()
    {
        var literal = Utf8FrontEnd.Analyze("abc", RegexOptions.None);
        var simple = Utf8FrontEnd.Analyze("cat|horse", RegexOptions.CultureInvariant);

        var literalSearch = Utf8FrontEndSearchAnalyzer.AnalyzeLiteral(
            literal.AnalyzedRegex.LiteralUtf8!,
            literal.AnalyzedRegex.ExecutionKind);
        var simpleSearch = Utf8FrontEndSearchAnalyzer.AnalyzeSimplePattern(
            simple.SemanticRegex,
            simple.AnalyzedRegex.SimplePatternPlan,
            new Utf8SearchPlan(Utf8SearchKind.None, null));

        Assert.Equal(Utf8SearchKind.ExactAsciiLiteral, literalSearch.Kind);
        Assert.Equal("abc", Encoding.UTF8.GetString(literalSearch.LiteralUtf8!));
        Assert.Equal(Utf8SearchKind.ExactAsciiLiterals, simpleSearch.Kind);
        Assert.Equal(["cat", "horse"], simpleSearch.AlternateLiteralsUtf8!.Select(Encoding.UTF8.GetString));
    }

    [Fact]
    public void FrontEndBuildsRuntimeShapedLiteralTreeForBootstrapLiteral()
    {
        var literal = Utf8FrontEnd.Analyze("abc", RegexOptions.None);

        Assert.NotNull(literal.SemanticRegex.RuntimeTree);
        Assert.Equal(RuntimeFrontEnd.RegexNodeKind.Capture, literal.SemanticRegex.RuntimeTree!.Root.Kind);
        Assert.Equal(1, literal.SemanticRegex.RuntimeTree.Root.ChildCount);
        Assert.Equal(RuntimeFrontEnd.RegexNodeKind.Multi, literal.SemanticRegex.RuntimeTree.Root.Child(0).Kind);
        Assert.Equal("abc", literal.SemanticRegex.RuntimeTree.Root.Child(0).Str);
    }

    [Fact]
    public void FrontEndBuildsRuntimeShapedSimplePatternTreeForLiteralDotSubset()
    {
        var simple = Utf8FrontEnd.Analyze("^ab.c$", RegexOptions.CultureInvariant);

        Assert.NotNull(simple.SemanticRegex.RuntimeTree);
        var body = simple.SemanticRegex.RuntimeTree!.Root.Child(0);
        Assert.Equal(RuntimeFrontEnd.RegexNodeKind.Concatenate, body.Kind);
        Assert.Equal(RuntimeFrontEnd.RegexNodeKind.Beginning, body.Child(0).Kind);
        Assert.Equal(RuntimeFrontEnd.RegexNodeKind.Multi, body.Child(1).Kind);
        Assert.Equal("ab", body.Child(1).Str);
        Assert.Equal(RuntimeFrontEnd.RegexNodeKind.Set, body.Child(2).Kind);
        Assert.Equal(RuntimeFrontEnd.RegexCharClass.NotNewLineClass, body.Child(2).Str);
        Assert.Equal(RuntimeFrontEnd.RegexNodeKind.One, body.Child(3).Kind);
        Assert.Equal('c', body.Child(3).Ch);
        Assert.Equal(RuntimeFrontEnd.RegexNodeKind.EndZ, body.Child(4).Kind);
    }

    [Fact]
    public void FrontEndBuildsRuntimeShapedSimplePatternTreeForAsciiCharClassSubset()
    {
        var simple = Utf8FrontEnd.Analyze("a[0-9]c", RegexOptions.CultureInvariant);

        Assert.NotNull(simple.SemanticRegex.RuntimeTree);
        var body = simple.SemanticRegex.RuntimeTree!.Root.Child(0);
        Assert.Equal(RuntimeFrontEnd.RegexNodeKind.Concatenate, body.Kind);
        Assert.Equal(RuntimeFrontEnd.RegexNodeKind.One, body.Child(0).Kind);
        Assert.Equal('a', body.Child(0).Ch);
        Assert.Equal(RuntimeFrontEnd.RegexNodeKind.Set, body.Child(1).Kind);
        Assert.Equal("\0\u0002\0\u0030\u003A", body.Child(1).Str);
        Assert.Equal(RuntimeFrontEnd.RegexNodeKind.One, body.Child(2).Kind);
        Assert.Equal('c', body.Child(2).Ch);
    }

    [Theory]
    [InlineData("", (int)RuntimeFrontEnd.RegexCaseBehavior.Invariant)]
    [InlineData("fr-FR", (int)RuntimeFrontEnd.RegexCaseBehavior.NonTurkish)]
    [InlineData("tr-TR", (int)RuntimeFrontEnd.RegexCaseBehavior.Turkish)]
    [InlineData("az-Latn-AZ", (int)RuntimeFrontEnd.RegexCaseBehavior.Turkish)]
    public void VendoredRegexCaseBehaviorMatchesRuntimeClassification(string cultureName, int expected)
    {
        var culture = CultureInfo.GetCultureInfo(cultureName);
        Assert.Equal(expected, (int)RuntimeFrontEnd.RegexCaseEquivalences.GetRegexBehavior(culture));
    }

    [Fact]
    public void VendoredRegexCharClassRoundTripsSimpleRanges()
    {
        var charClass = new RuntimeFrontEnd.RegexCharClass();
        charClass.AddRange('a', 'c');
        charClass.AddChar('z');

        var payload = charClass.ToStringClass();
        var reparsed = RuntimeFrontEnd.RegexCharClass.Parse(payload);

        Assert.Equal(payload, reparsed.ToStringClass());
    }

    [Fact]
    public void VendoredRegexCharClassAddsPredefinedDigitClass()
    {
        var charClass = new RuntimeFrontEnd.RegexCharClass();
        charClass.AddDigit(ecma: false, negate: false, "pattern", 0);

        Assert.Equal(RuntimeFrontEnd.RegexCharClass.DigitClass, charClass.ToStringClass());
    }

    [Fact]
    public void VendoredRegexCharClassAddsEcmaPredefinedClasses()
    {
        var digit = new RuntimeFrontEnd.RegexCharClass();
        digit.AddDigit(ecma: true, negate: false, "pattern", 0);

        var word = new RuntimeFrontEnd.RegexCharClass();
        word.AddWord(ecma: true, negate: false);

        var space = new RuntimeFrontEnd.RegexCharClass();
        space.AddSpace(ecma: true, negate: false);

        Assert.Equal(RuntimeFrontEnd.RegexCharClass.ECMADigitClass, digit.ToStringClass());
        Assert.Equal(RuntimeFrontEnd.RegexCharClass.ECMAWordClass, word.ToStringClass());
        Assert.Equal(RuntimeFrontEnd.RegexCharClass.ECMASpaceClass, space.ToStringClass());
    }

    [Fact]
    public void VendoredRegexCharClassExposesBasicQuerySurface()
    {
        var set = "\0\u0002\0\u0030\u003A";

        Assert.False(RuntimeFrontEnd.RegexCharClass.IsNegated(set));
        Assert.False(RuntimeFrontEnd.RegexCharClass.IsEmpty(set));
        Assert.True(RuntimeFrontEnd.RegexCharClass.TryGetSingleRange(set, out var first, out var last));
        Assert.Equal('0', first);
        Assert.Equal('9', last);
        Assert.True(RuntimeFrontEnd.RegexCharClass.CharInClass('4', set));
        Assert.False(RuntimeFrontEnd.RegexCharClass.CharInClass('x', set));
    }

    [Fact]
    public void VendoredRegexCharClassCanMatchCategoryBasedPredefinedClasses()
    {
        Assert.True(RuntimeFrontEnd.RegexCharClass.CharInClass('4', RuntimeFrontEnd.RegexCharClass.DigitClass));
        Assert.False(RuntimeFrontEnd.RegexCharClass.CharInClass('x', RuntimeFrontEnd.RegexCharClass.DigitClass));
        Assert.True(RuntimeFrontEnd.RegexCharClass.CharInClass('x', RuntimeFrontEnd.RegexCharClass.NotDigitClass));
        Assert.True(RuntimeFrontEnd.RegexCharClass.CharInClass('A', RuntimeFrontEnd.RegexCharClass.WordClass));
        Assert.True(RuntimeFrontEnd.RegexCharClass.CharInClass('_', RuntimeFrontEnd.RegexCharClass.WordClass));
        Assert.True(RuntimeFrontEnd.RegexCharClass.CharInClass(' ', RuntimeFrontEnd.RegexCharClass.SpaceClass));
        Assert.True(RuntimeFrontEnd.RegexCharClass.CharInClass('\t', RuntimeFrontEnd.RegexCharClass.SpaceClass));
    }

    [Fact]
    public void VendoredRegexCharClassCanExposeBaseMembershipForMergedAsciiSets()
    {
        var analysis = Utf8FrontEnd.Analyze(@"[\w-]", RegexOptions.CultureInvariant);
        var set = analysis.SemanticRegex.RuntimeTree!.Root.Child(0).Str!;

        Assert.True(RuntimeFrontEnd.RegexCharClass.CharInClassBase('A', set));
        Assert.True(RuntimeFrontEnd.RegexCharClass.CharInClassBase('-', set));
        Assert.False(RuntimeFrontEnd.RegexCharClass.CharInClassBase(' ', set));
    }

    [Fact]
    public void VendoredRegexCharClassRecognizesSingletonSet()
    {
        var set = new RuntimeFrontEnd.RegexCharClass();
        set.AddChar('Q');
        var payload = set.ToStringClass();

        Assert.True(RuntimeFrontEnd.RegexCharClass.IsSingleton(payload));
        Assert.Equal('Q', RuntimeFrontEnd.RegexCharClass.SingletonChar(payload));
    }

    [Fact]
    public void VendoredRegexCharClassExposesDoubleRangeAndEnumerationHelpers()
    {
        const string set = "\0\u0004\0AZaz";

        Assert.True(RuntimeFrontEnd.RegexCharClass.TryGetDoubleRange(set, out var firstRange, out var secondRange));
        Assert.Equal(('A', 'Y'), firstRange);
        Assert.Equal(('a', 'y'), secondRange);
        Assert.True(RuntimeFrontEnd.RegexCharClass.CanEasilyEnumerateSetContents(set));
        Assert.Contains('A', RuntimeFrontEnd.RegexCharClass.GetSetChars(set));
        Assert.Contains('x', RuntimeFrontEnd.RegexCharClass.GetSetChars(set));
        Assert.True(RuntimeFrontEnd.RegexCharClass.SetContainsAsciiOrdinalIgnoreCaseCharacter(set, 'X'));
    }

    [Fact]
    public void VendoredRegexCharClassExposesWordCharacterHelpers()
    {
        Assert.True(RuntimeFrontEnd.RegexCharClass.IsECMAWordChar('A'));
        Assert.True(RuntimeFrontEnd.RegexCharClass.IsECMAWordChar('_'));
        Assert.False(RuntimeFrontEnd.RegexCharClass.IsECMAWordChar('-'));

        Assert.True(RuntimeFrontEnd.RegexCharClass.IsWordChar('7'));
        Assert.True(RuntimeFrontEnd.RegexCharClass.IsBoundaryWordChar('_'));
        Assert.False(RuntimeFrontEnd.RegexCharClass.IsWordChar('-'));
        Assert.True(RuntimeFrontEnd.RegexCharClass.WordCharAsciiLookup.Length > 0);
    }

    [Fact]
    public void VendoredRegexCharClassExposesStaticStringClassBuilders()
    {
        Assert.Equal("\0\u0002\0QR", RuntimeFrontEnd.RegexCharClass.OneToStringClass('Q'));
        Assert.Equal("\0\u0004\0ABXY", RuntimeFrontEnd.RegexCharClass.CharsToStringClass("AX"));
    }

    [Fact]
    public void VendoredRegexCharClassCanClassifyAsciiSets()
    {
        Assert.True(RuntimeFrontEnd.RegexCharClass.IsAscii(RuntimeFrontEnd.RegexCharClass.AsciiLetterOrDigitClass));
        Assert.False(RuntimeFrontEnd.RegexCharClass.IsAscii("\0\u0002\0\u00FF\u0100"));
    }

    [Fact]
    public void VendoredRegexCharClassCanDetectOverlap()
    {
        Assert.True(RuntimeFrontEnd.RegexCharClass.MayOverlap(
            RuntimeFrontEnd.RegexCharClass.AsciiLetterClass,
            RuntimeFrontEnd.RegexCharClass.AsciiLetterOrDigitClass));
        Assert.False(RuntimeFrontEnd.RegexCharClass.MayOverlap(
            RuntimeFrontEnd.RegexCharClass.OneToStringClass('A'),
            RuntimeFrontEnd.RegexCharClass.OneToStringClass('Z')));
    }

    [Fact]
    public void VendoredRegexCharClassCanDetectCategoryOnlyPayloads()
    {
        Assert.True(RuntimeFrontEnd.RegexCharClass.TryGetOnlyCategories(RuntimeFrontEnd.RegexCharClass.DigitClass, out var categories));
        Assert.Equal("\u0009", categories);
        Assert.False(RuntimeFrontEnd.RegexCharClass.TryGetOnlyCategories(RuntimeFrontEnd.RegexCharClass.AsciiLetterClass, out _));
    }

    [Fact]
    public void VendoredRegexCharClassCanClassifyMergeablePayloads()
    {
        Assert.True(RuntimeFrontEnd.RegexCharClass.IsMergeable(RuntimeFrontEnd.RegexCharClass.AsciiLetterClass));
        Assert.False(RuntimeFrontEnd.RegexCharClass.IsMergeable("\u0001\u0004\0A[a{"));
    }

    [Fact]
    public void VendoredRegexParserParsesBootstrapLiteralSubset()
    {
        var tree = RuntimeFrontEnd.RegexParser.Parse("abc", RegexOptions.None, CultureInfo.InvariantCulture);

        Assert.Equal(RuntimeFrontEnd.RegexNodeKind.Capture, tree.Root.Kind);
        Assert.Equal(RuntimeFrontEnd.RegexNodeKind.Multi, tree.Root.Child(0).Kind);
        Assert.Equal("abc", tree.Root.Child(0).Str);
    }

    [Fact]
    public void VendoredRegexParserExposesTryParseForBootstrapSubset()
    {
        var success = RuntimeFrontEnd.RegexParser.TryParse("abc", RegexOptions.None, CultureInfo.InvariantCulture, out var tree);
        var fallback = RuntimeFrontEnd.RegexParser.TryParse("a{2,}", RegexOptions.CultureInvariant, CultureInfo.InvariantCulture, out var fallbackTree);
        var fallbackLazy = RuntimeFrontEnd.RegexParser.TryParse("a{2,}?", RegexOptions.CultureInvariant, CultureInfo.InvariantCulture, out var fallbackLazyTree);

        Assert.True(success);
        Assert.NotNull(tree);
        Assert.True(fallback);
        Assert.NotNull(fallbackTree);
        Assert.Equal(RuntimeFrontEnd.RegexNodeKind.Oneloop, fallbackTree.Root.Child(0).Kind);
        Assert.Equal(2, fallbackTree.Root.Child(0).M);
        Assert.Equal(int.MaxValue, fallbackTree.Root.Child(0).N);
        Assert.True(fallbackLazy);
        Assert.NotNull(fallbackLazyTree);
        Assert.Equal(RuntimeFrontEnd.RegexNodeKind.Onelazy, fallbackLazyTree.Root.Child(0).Kind);
        Assert.Equal(2, fallbackLazyTree.Root.Child(0).M);
        Assert.Equal(int.MaxValue, fallbackLazyTree.Root.Child(0).N);
    }

    [Fact]
    public void VendoredRegexParserThrowsTypedParseExceptionForUnsupportedPatterns()
    {
        var error = Assert.Throws<RuntimeFrontEnd.RegexParseException>(() =>
            RuntimeFrontEnd.RegexParser.Parse("a{,2}", RegexOptions.CultureInvariant, CultureInfo.InvariantCulture));

        Assert.Equal(RuntimeFrontEnd.RegexParseError.InvalidPattern, error.Error);
        Assert.Equal(5, error.Offset);
    }

    [Fact]
    public void VendoredRegexParserReportsSyntaxFailureOffsets()
    {
        var group = Assert.Throws<RuntimeFrontEnd.RegexParseException>(() =>
            RuntimeFrontEnd.RegexParser.Parse("(", RegexOptions.CultureInvariant, CultureInfo.InvariantCulture));
        var escape = Assert.Throws<RuntimeFrontEnd.RegexParseException>(() =>
            RuntimeFrontEnd.RegexParser.Parse(@"a\x", RegexOptions.CultureInvariant, CultureInfo.InvariantCulture));
        var unicode = Assert.Throws<RuntimeFrontEnd.RegexParseException>(() =>
            RuntimeFrontEnd.RegexParser.Parse(@"\u12", RegexOptions.CultureInvariant, CultureInfo.InvariantCulture));
        var unrecognized = Assert.Throws<RuntimeFrontEnd.RegexParseException>(() =>
            RuntimeFrontEnd.RegexParser.Parse(@"a\q", RegexOptions.CultureInvariant, CultureInfo.InvariantCulture));
        var classEscape = Assert.Throws<RuntimeFrontEnd.RegexParseException>(() =>
            RuntimeFrontEnd.RegexParser.Parse(@"[\8]", RegexOptions.CultureInvariant, CultureInfo.InvariantCulture));
        var bracket = Assert.Throws<RuntimeFrontEnd.RegexParseException>(() =>
            RuntimeFrontEnd.RegexParser.Parse("[abc", RegexOptions.CultureInvariant, CultureInfo.InvariantCulture));
        var reversedRange = Assert.Throws<RuntimeFrontEnd.RegexParseException>(() =>
            RuntimeFrontEnd.RegexParser.Parse(@"[z-a]", RegexOptions.CultureInvariant, CultureInfo.InvariantCulture));
        var exclusionNotLast = Assert.Throws<RuntimeFrontEnd.RegexParseException>(() =>
            RuntimeFrontEnd.RegexParser.Parse("[a-z-[aeiou]x]", RegexOptions.CultureInvariant, CultureInfo.InvariantCulture));
        var quantifier = Assert.Throws<RuntimeFrontEnd.RegexParseException>(() =>
            RuntimeFrontEnd.RegexParser.Parse("*a", RegexOptions.CultureInvariant, CultureInfo.InvariantCulture));
        var grouping = Assert.Throws<RuntimeFrontEnd.RegexParseException>(() =>
            RuntimeFrontEnd.RegexParser.Parse("(?q:a)", RegexOptions.CultureInvariant, CultureInfo.InvariantCulture));

        Assert.Equal(RuntimeFrontEnd.RegexParseError.NotEnoughParentheses, group.Error);
        Assert.Equal(RuntimeFrontEnd.RegexParseError.InsufficientOrInvalidHexDigits, escape.Error);
        Assert.Equal(RuntimeFrontEnd.RegexParseError.InsufficientOrInvalidHexDigits, unicode.Error);
        Assert.Equal(RuntimeFrontEnd.RegexParseError.UnrecognizedEscape, unrecognized.Error);
        Assert.Equal(RuntimeFrontEnd.RegexParseError.UnrecognizedEscape, classEscape.Error);
        Assert.Equal(RuntimeFrontEnd.RegexParseError.UnterminatedBracket, bracket.Error);
        Assert.Equal(RuntimeFrontEnd.RegexParseError.ReversedCharacterRange, reversedRange.Error);
        Assert.Equal(RuntimeFrontEnd.RegexParseError.ExclusionGroupNotLast, exclusionNotLast.Error);
        Assert.Equal(RuntimeFrontEnd.RegexParseError.QuantifierAfterNothing, quantifier.Error);
        Assert.Equal(RuntimeFrontEnd.RegexParseError.InvalidGroupingConstruct, grouping.Error);
        Assert.Equal(1, group.Offset);
        Assert.Equal(3, escape.Offset);
        Assert.Equal(2, unicode.Offset);
        Assert.Equal(3, unrecognized.Offset);
        Assert.Equal(3, classEscape.Offset);
        Assert.Equal(4, bracket.Offset);
        Assert.Equal(4, reversedRange.Offset);
        Assert.Equal(12, exclusionNotLast.Offset);
        Assert.Equal(0, quantifier.Offset);
        Assert.Equal(1, grouping.Offset);
    }

    [Fact]
    public void VendoredRegexParserThrowsTypedParseExceptionForUndefinedReferences()
    {
        var numbered = Assert.Throws<RuntimeFrontEnd.RegexParseException>(() =>
            RuntimeFrontEnd.RegexParser.Parse(@"a\1", RegexOptions.CultureInvariant, CultureInfo.InvariantCulture));
        var named = Assert.Throws<RuntimeFrontEnd.RegexParseException>(() =>
            RuntimeFrontEnd.RegexParser.Parse(@"a\k<missing>", RegexOptions.CultureInvariant, CultureInfo.InvariantCulture));

        Assert.Equal(RuntimeFrontEnd.RegexParseError.UndefinedNumberedReference, numbered.Error);
        Assert.Equal(RuntimeFrontEnd.RegexParseError.UndefinedNamedReference, named.Error);
        Assert.Equal(1, numbered.Offset);
        Assert.Equal(1, named.Offset);
    }

    [Fact]
    public void VendoredRegexParserParsesBootstrapSimpleSubset()
    {
        var tree = RuntimeFrontEnd.RegexParser.Parse("^ab.c$", RegexOptions.CultureInvariant, CultureInfo.InvariantCulture);
        var body = tree.Root.Child(0);

        Assert.Equal(RuntimeFrontEnd.RegexNodeKind.Concatenate, body.Kind);
        Assert.Equal(RuntimeFrontEnd.RegexNodeKind.Beginning, body.Child(0).Kind);
        Assert.Equal(RuntimeFrontEnd.RegexNodeKind.EndZ, body.Child(body.ChildCount - 1).Kind);
    }

    [Theory]
    [InlineData("(?:cat|horse)", (int)RuntimeFrontEnd.RegexNodeKind.Group)]
    [InlineData("(ab.cd)", (int)RuntimeFrontEnd.RegexNodeKind.Capture)]
    [InlineData(@"[\x30A]", (int)RuntimeFrontEnd.RegexNodeKind.Set)]
    public void VendoredRegexParserHandlesBroaderBootstrapSubset(string pattern, int expectedBodyKind)
    {
        var tree = RuntimeFrontEnd.RegexParser.Parse(pattern, RegexOptions.CultureInvariant, CultureInfo.InvariantCulture);

        Assert.Equal(RuntimeFrontEnd.RegexNodeKind.Capture, tree.Root.Kind);
        Assert.Equal(1, tree.Root.ChildCount);
        Assert.Equal(expectedBodyKind, (int)tree.Root.Child(0).Kind);
        Assert.NotNull(tree.FindOptimizations);
    }

    [Fact]
    public void VendoredRegexParserHandlesBootstrapBoundedQuantifierSubset()
    {
        var tree = RuntimeFrontEnd.RegexParser.Parse(@"\d{2,4}", RegexOptions.CultureInvariant, CultureInfo.InvariantCulture);

        Assert.Equal(RuntimeFrontEnd.RegexNodeKind.Capture, tree.Root.Kind);
        Assert.Equal(1, tree.Root.ChildCount);
        Assert.Equal(RuntimeFrontEnd.RegexNodeKind.Setloop, tree.Root.Child(0).Kind);
        Assert.Equal(2, tree.Root.Child(0).M);
        Assert.Equal(4, tree.Root.Child(0).N);
        Assert.NotNull(tree.FindOptimizations);
    }

    [Fact]
    public void VendoredRegexParserHandlesUnicodeAndControlEscapes()
    {
        var unicode = RuntimeFrontEnd.RegexParser.Parse(@"\u00E9", RegexOptions.CultureInvariant, CultureInfo.InvariantCulture);
        var hex = RuntimeFrontEnd.RegexParser.Parse(@"\xE9", RegexOptions.CultureInvariant, CultureInfo.InvariantCulture);
        var control = RuntimeFrontEnd.RegexParser.Parse(@"\cA", RegexOptions.CultureInvariant, CultureInfo.InvariantCulture);
        var charClass = RuntimeFrontEnd.RegexParser.Parse(@"[\u0030\xE9\cA]", RegexOptions.CultureInvariant, CultureInfo.InvariantCulture);

        Assert.Equal('é', unicode.Root.Child(0).Ch);
        Assert.Equal('é', hex.Root.Child(0).Ch);
        Assert.Equal('\u0001', control.Root.Child(0).Ch);
        Assert.Equal(RuntimeFrontEnd.RegexNodeKind.Set, charClass.Root.Child(0).Kind);
        Assert.True(RuntimeFrontEnd.RegexCharClass.CharInClass('0', charClass.Root.Child(0).Str!));
        Assert.True(RuntimeFrontEnd.RegexCharClass.CharInClass('é', charClass.Root.Child(0).Str!));
        Assert.True(RuntimeFrontEnd.RegexCharClass.CharInClass('\u0001', charClass.Root.Child(0).Str!));
    }

    [Fact]
    public void VendoredRegexParserHandlesNullEscapes()
    {
        var literal = RuntimeFrontEnd.RegexParser.Parse(@"\0", RegexOptions.CultureInvariant, CultureInfo.InvariantCulture);
        var octal = RuntimeFrontEnd.RegexParser.Parse(@"\040", RegexOptions.CultureInvariant, CultureInfo.InvariantCulture);
        var octalFallback = RuntimeFrontEnd.RegexParser.Parse(@"\11", RegexOptions.CultureInvariant, CultureInfo.InvariantCulture);
        var splitOctalFallback = RuntimeFrontEnd.RegexParser.Parse(@"\18", RegexOptions.CultureInvariant, CultureInfo.InvariantCulture);
        var backreference = RuntimeFrontEnd.RegexParser.Parse(@"(a)(b)(c)(d)(e)(f)(g)(h)(i)(j)(k)\11", RegexOptions.CultureInvariant, CultureInfo.InvariantCulture);
        var charClass = RuntimeFrontEnd.RegexParser.Parse(@"[\0\b\BA]", RegexOptions.CultureInvariant, CultureInfo.InvariantCulture);
        var octalClass = RuntimeFrontEnd.RegexParser.Parse(@"[\040]", RegexOptions.CultureInvariant, CultureInfo.InvariantCulture);
        var octalFallbackClass = RuntimeFrontEnd.RegexParser.Parse(@"[\11]", RegexOptions.CultureInvariant, CultureInfo.InvariantCulture);
        var splitOctalFallbackClass = RuntimeFrontEnd.RegexParser.Parse(@"[\18]", RegexOptions.CultureInvariant, CultureInfo.InvariantCulture);

        Assert.Equal('\0', literal.Root.Child(0).Ch);
        Assert.Equal(' ', octal.Root.Child(0).Ch);
        Assert.Equal('\t', octalFallback.Root.Child(0).Ch);
        Assert.Equal(RuntimeFrontEnd.RegexNodeKind.Multi, splitOctalFallback.Root.Child(0).Kind);
        Assert.Equal("\u00018", splitOctalFallback.Root.Child(0).Str);
        Assert.Equal(RuntimeFrontEnd.RegexNodeKind.Backreference, backreference.Root.Child(0).Child(11).Kind);
        Assert.Equal(11, backreference.Root.Child(0).Child(11).M);
        Assert.Equal(RuntimeFrontEnd.RegexNodeKind.Set, charClass.Root.Child(0).Kind);
        Assert.True(RuntimeFrontEnd.RegexCharClass.CharInClass('\0', charClass.Root.Child(0).Str!));
        Assert.True(RuntimeFrontEnd.RegexCharClass.CharInClass('\b', charClass.Root.Child(0).Str!));
        Assert.True(RuntimeFrontEnd.RegexCharClass.CharInClass('B', charClass.Root.Child(0).Str!));
        Assert.True(RuntimeFrontEnd.RegexCharClass.CharInClass('A', charClass.Root.Child(0).Str!));
        Assert.True(RuntimeFrontEnd.RegexCharClass.CharInClass(' ', octalClass.Root.Child(0).Str!));
        Assert.True(RuntimeFrontEnd.RegexCharClass.CharInClass('\t', octalFallbackClass.Root.Child(0).Str!));
        Assert.True(RuntimeFrontEnd.RegexCharClass.CharInClass('\u0001', splitOctalFallbackClass.Root.Child(0).Str!));
        Assert.True(RuntimeFrontEnd.RegexCharClass.CharInClass('8', splitOctalFallbackClass.Root.Child(0).Str!));
    }

    [Fact]
    public void VendoredRegexParserTreatsBoundaryEscapesAsLiteralsInsideCharClasses()
    {
        var charClass = RuntimeFrontEnd.RegexParser.Parse(@"[\A\G\Z\z]", RegexOptions.CultureInvariant, CultureInfo.InvariantCulture);

        Assert.Equal(RuntimeFrontEnd.RegexNodeKind.Set, charClass.Root.Child(0).Kind);
        Assert.True(RuntimeFrontEnd.RegexCharClass.CharInClass('A', charClass.Root.Child(0).Str!));
        Assert.True(RuntimeFrontEnd.RegexCharClass.CharInClass('G', charClass.Root.Child(0).Str!));
        Assert.True(RuntimeFrontEnd.RegexCharClass.CharInClass('Z', charClass.Root.Child(0).Str!));
        Assert.True(RuntimeFrontEnd.RegexCharClass.CharInClass('z', charClass.Root.Child(0).Str!));
    }

    [Fact]
    public void VendoredRegexParserSharesLiteralEscapeScanningAcrossPatternAndCharClasses()
    {
        var pattern = RuntimeFrontEnd.RegexParser.Parse(@"\a\e\n\r\t\f\v\[", RegexOptions.CultureInvariant, CultureInfo.InvariantCulture);
        var charClass = RuntimeFrontEnd.RegexParser.Parse(@"[\a\e\n\r\t\f\v\[]", RegexOptions.CultureInvariant, CultureInfo.InvariantCulture);

        Assert.Equal(RuntimeFrontEnd.RegexNodeKind.Multi, pattern.Root.Child(0).Kind);
        Assert.Equal("\a\u001B\n\r\t\f\v[", pattern.Root.Child(0).Str);
        Assert.True(RuntimeFrontEnd.RegexCharClass.CharInClass('\a', charClass.Root.Child(0).Str!));
        Assert.True(RuntimeFrontEnd.RegexCharClass.CharInClass('\u001B', charClass.Root.Child(0).Str!));
        Assert.True(RuntimeFrontEnd.RegexCharClass.CharInClass('\n', charClass.Root.Child(0).Str!));
        Assert.True(RuntimeFrontEnd.RegexCharClass.CharInClass('\r', charClass.Root.Child(0).Str!));
        Assert.True(RuntimeFrontEnd.RegexCharClass.CharInClass('\t', charClass.Root.Child(0).Str!));
        Assert.True(RuntimeFrontEnd.RegexCharClass.CharInClass('\f', charClass.Root.Child(0).Str!));
        Assert.True(RuntimeFrontEnd.RegexCharClass.CharInClass('\v', charClass.Root.Child(0).Str!));
        Assert.True(RuntimeFrontEnd.RegexCharClass.CharInClass('[', charClass.Root.Child(0).Str!));
    }

    [Fact]
    public void VendoredRegexParserPreservesBootstrapCaptureAndGroupStructure()
    {
        var capturing = RuntimeFrontEnd.RegexParser.Parse("(ab.cd)", RegexOptions.CultureInvariant, CultureInfo.InvariantCulture);
        var nonCapturing = RuntimeFrontEnd.RegexParser.Parse("(?:cat|horse)", RegexOptions.CultureInvariant, CultureInfo.InvariantCulture);

        Assert.Equal(2, capturing.CaptureCount);
        Assert.Equal(RuntimeFrontEnd.RegexNodeKind.Capture, capturing.Root.Child(0).Kind);
        Assert.Equal(RuntimeFrontEnd.RegexNodeKind.Concatenate, capturing.Root.Child(0).Child(0).Kind);

        Assert.Equal(1, nonCapturing.CaptureCount);
        Assert.Equal(RuntimeFrontEnd.RegexNodeKind.Group, nonCapturing.Root.Child(0).Kind);
        Assert.Equal(RuntimeFrontEnd.RegexNodeKind.Alternate, nonCapturing.Root.Child(0).Child(0).Kind);
    }

    [Fact]
    public void VendoredRegexParserTracksNamedCaptureMetadata()
    {
        var tree = RuntimeFrontEnd.RegexParser.Parse("(?<word>ab)", RegexOptions.CultureInvariant, CultureInfo.InvariantCulture);

        Assert.Equal(2, tree.CaptureCount);
        Assert.NotNull(tree.CaptureNames);
        Assert.Equal("word", tree.CaptureNames![1]);
        Assert.NotNull(tree.CaptureNameToNumberMapping);
        Assert.Equal(1, tree.CaptureNameToNumberMapping!["word"]);
    }

    [Fact]
    public void VendoredRegexParserUsesRuntimeStyleMixedCaptureNumbering()
    {
        var tree = RuntimeFrontEnd.RegexParser.Parse("(?<name>a)(b)", RegexOptions.CultureInvariant, CultureInfo.InvariantCulture);
        var body = tree.Root.Child(0);

        Assert.Equal(RuntimeFrontEnd.RegexNodeKind.Concatenate, body.Kind);
        Assert.Equal(2, body.Child(0).M);
        Assert.Equal("name", body.Child(0).Str);
        Assert.Equal(1, body.Child(1).M);
        Assert.NotNull(tree.CaptureNames);
        Assert.Equal(["0", "1", "name"], tree.CaptureNames!);
    }

    [Fact]
    public void VendoredRegexParserUsesPrecountedImplicitCaptureSlots()
    {
        var tree = RuntimeFrontEnd.RegexParser.Parse("(?<2>a)(b)", RegexOptions.CultureInvariant, CultureInfo.InvariantCulture);
        var body = tree.Root.Child(0);

        Assert.Equal(RuntimeFrontEnd.RegexNodeKind.Concatenate, body.Kind);
        Assert.Equal(2, body.Child(0).M);
        Assert.Equal("2", body.Child(0).Str);
        Assert.Equal(1, body.Child(1).M);
        Assert.Equal(3, tree.CaptureCount);
        Assert.Equal("2", RuntimeFrontEnd.RegexParser.GroupNameFromNumber(
            tree.CaptureNumberSparseMapping,
            tree.CaptureNames,
            tree.CaptureCount,
            2));
    }

    [Fact]
    public void VendoredRegexParserExposesRuntimeStyleCaptureHelpers()
    {
        var tree = RuntimeFrontEnd.RegexParser.Parse("(?<name>a)(b)", RegexOptions.CultureInvariant, CultureInfo.InvariantCulture);

        Assert.Equal("name", RuntimeFrontEnd.RegexParser.GroupNameFromNumber(
            tree.CaptureNumberSparseMapping,
            tree.CaptureNames,
            tree.CaptureCount,
            2));
        Assert.Equal("1", RuntimeFrontEnd.RegexParser.GroupNameFromNumber(
            tree.CaptureNumberSparseMapping,
            tree.CaptureNames,
            tree.CaptureCount,
            1));
        Assert.Equal(2, RuntimeFrontEnd.RegexParser.GroupNumberFromName(
            tree.CaptureNameToNumberMapping,
            tree.CaptureNames,
            tree.CaptureCount,
            "name"));
        Assert.Equal(1, RuntimeFrontEnd.RegexParser.GroupNumberFromName(
            tree.CaptureNameToNumberMapping,
            tree.CaptureNames,
            tree.CaptureCount,
            "1"));
        Assert.Equal(-1, RuntimeFrontEnd.RegexParser.GroupNumberFromName(
            tree.CaptureNameToNumberMapping,
            tree.CaptureNames,
            tree.CaptureCount,
            "missing"));
        Assert.Equal(2, RuntimeFrontEnd.RegexParser.MapCaptureNumber(2, tree.CaptureNumberSparseMapping));
    }

    [Fact]
    public void VendoredRegexParserRespectsExplicitCaptureForUnnamedGroups()
    {
        var optionsTree = RuntimeFrontEnd.RegexParser.Parse("(ab)", RegexOptions.ExplicitCapture | RegexOptions.CultureInvariant, CultureInfo.InvariantCulture);
        var inlineTree = RuntimeFrontEnd.RegexParser.Parse("(?n)(ab)", RegexOptions.CultureInvariant, CultureInfo.InvariantCulture);

        Assert.Equal(1, optionsTree.CaptureCount);
        Assert.Equal(RuntimeFrontEnd.RegexNodeKind.Group, optionsTree.Root.Child(0).Kind);
        Assert.Equal(1, inlineTree.CaptureCount);
        Assert.Equal(RuntimeFrontEnd.RegexNodeKind.Group, inlineTree.Root.Child(0).Kind);
    }

    [Fact]
    public void VendoredRegexParserAppliesScopedInlineOptionsToGroupBody()
    {
        var tree = RuntimeFrontEnd.RegexParser.Parse("(?i:a.c)", RegexOptions.CultureInvariant, CultureInfo.InvariantCulture);
        var group = tree.Root.Child(0);
        var body = group.Child(0);

        Assert.Equal(RuntimeFrontEnd.RegexNodeKind.Group, group.Kind);
        Assert.True((group.Options & RegexOptions.IgnoreCase) != 0);
        Assert.True((body.Options & RegexOptions.IgnoreCase) != 0);
        Assert.Equal(RuntimeFrontEnd.RegexNodeKind.Concatenate, body.Kind);
    }

    [Fact]
    public void VendoredRegexParserKeepsStandaloneInlineOptionsInRuntimeShapedTree()
    {
        var tree = RuntimeFrontEnd.RegexParser.Parse("a(?i)bC", RegexOptions.CultureInvariant, CultureInfo.InvariantCulture);
        var body = tree.Root.Child(0);

        Assert.Equal(RuntimeFrontEnd.RegexNodeKind.Concatenate, body.Kind);
        Assert.Equal('a', body.Child(0).Ch);
        Assert.Equal(RuntimeFrontEnd.RegexNodeKind.Set, body.Child(1).Kind);
        Assert.Equal(RuntimeFrontEnd.RegexNodeKind.Set, body.Child(2).Kind);
    }

    [Fact]
    public void VendoredRegexParserRestoresOuterOptionsAfterScopedGroup()
    {
        var tree = RuntimeFrontEnd.RegexParser.Parse("(?i)a(?-i:b)c", RegexOptions.CultureInvariant, CultureInfo.InvariantCulture);
        var body = tree.Root.Child(0);

        Assert.Equal(RuntimeFrontEnd.RegexNodeKind.Concatenate, body.Kind);
        Assert.Equal(RuntimeFrontEnd.RegexNodeKind.Set, body.Child(0).Kind);
        Assert.Equal(RuntimeFrontEnd.RegexNodeKind.Group, body.Child(1).Kind);
        Assert.Equal(RegexOptions.CultureInvariant, body.Child(1).Child(0).Options);
        Assert.Equal(RuntimeFrontEnd.RegexNodeKind.Set, body.Child(2).Kind);
    }

    [Fact]
    public void VendoredRegexParserStripsIgnoreCaseFromRootAndSetNodes()
    {
        var tree = RuntimeFrontEnd.RegexParser.Parse("(?i)\\d.", RegexOptions.CultureInvariant, CultureInfo.InvariantCulture);
        var body = tree.Root.Child(0);

        Assert.Equal(RegexOptions.CultureInvariant, tree.Root.Options);
        Assert.Equal(RuntimeFrontEnd.RegexNodeKind.Set, body.Child(0).Kind);
        Assert.Equal(RegexOptions.CultureInvariant, body.Child(0).Options);
        Assert.Equal(RuntimeFrontEnd.RegexNodeKind.Set, body.Child(1).Kind);
        Assert.Equal(RegexOptions.CultureInvariant, body.Child(1).Options);
    }

    [Fact]
    public void VendoredRegexParserBuildsFallbackTreesForLookaroundsBackreferencesAndAtomicGroups()
    {
        var lookahead = RuntimeFrontEnd.RegexParser.Parse("(?=ab)ab", RegexOptions.CultureInvariant, CultureInfo.InvariantCulture);
        var atomic = RuntimeFrontEnd.RegexParser.Parse("(?>ab)c", RegexOptions.CultureInvariant, CultureInfo.InvariantCulture);
        var backreference = RuntimeFrontEnd.RegexParser.Parse(@"(ab)\1", RegexOptions.CultureInvariant, CultureInfo.InvariantCulture);
        var namedBackreference = RuntimeFrontEnd.RegexParser.Parse(@"(?<word>ab)\k<word>", RegexOptions.CultureInvariant, CultureInfo.InvariantCulture);

        Assert.Equal(RuntimeFrontEnd.RegexNodeKind.PositiveLookaround, lookahead.Root.Child(0).Child(0).Kind);
        Assert.Equal(RuntimeFrontEnd.RegexNodeKind.Atomic, atomic.Root.Child(0).Child(0).Kind);
        Assert.Equal(RuntimeFrontEnd.RegexNodeKind.Backreference, backreference.Root.Child(0).Child(1).Kind);
        Assert.Equal(1, backreference.Root.Child(0).Child(1).M);
        Assert.Equal(RuntimeFrontEnd.RegexNodeKind.Backreference, namedBackreference.Root.Child(0).Child(1).Kind);
        Assert.Equal("word", namedBackreference.Root.Child(0).Child(1).Str);
        Assert.Equal(1, namedBackreference.Root.Child(0).Child(1).M);
    }

    [Fact]
    public void VendoredRegexParserBuildsBoundaryAndAbsoluteAnchorNodes()
    {
        var boundary = RuntimeFrontEnd.RegexParser.Parse(@"\bword\B", RegexOptions.CultureInvariant, CultureInfo.InvariantCulture);
        var absolute = RuntimeFrontEnd.RegexParser.Parse(@"\Aword\Z", RegexOptions.CultureInvariant, CultureInfo.InvariantCulture);
        var strict = RuntimeFrontEnd.RegexParser.Parse(@"word\z", RegexOptions.CultureInvariant, CultureInfo.InvariantCulture);

        Assert.Equal(RuntimeFrontEnd.RegexNodeKind.Boundary, boundary.Root.Child(0).Child(0).Kind);
        Assert.Equal(RuntimeFrontEnd.RegexNodeKind.NonBoundary, boundary.Root.Child(0).Child(2).Kind);
        Assert.Equal(RuntimeFrontEnd.RegexNodeKind.Beginning, absolute.Root.Child(0).Child(0).Kind);
        Assert.Equal(RuntimeFrontEnd.RegexNodeKind.EndZ, absolute.Root.Child(0).Child(2).Kind);
        Assert.Equal(RuntimeFrontEnd.RegexNodeKind.End, strict.Root.Child(0).Child(1).Kind);
    }

    [Fact]
    public void VendoredRegexParserBuildsDeclarationIdentifierFamilyShape()
    {
        var tree = RuntimeFrontEnd.RegexParser.Parse(@"\b(?:record|struct|class)\s+[A-Z][A-Za-z0-9_]+", RegexOptions.CultureInvariant, CultureInfo.InvariantCulture);
        var body = tree.Root.Child(0);

        Assert.Equal(RuntimeFrontEnd.RegexNodeKind.Concatenate, body.Kind);
        Assert.Equal(RuntimeFrontEnd.RegexNodeKind.Boundary, body.Child(0).Kind);
        Assert.Equal(RuntimeFrontEnd.RegexNodeKind.Group, body.Child(1).Kind);
        Assert.Equal(RuntimeFrontEnd.RegexNodeKind.Alternate, body.Child(1).Child(0).Kind);
        Assert.Equal(RuntimeFrontEnd.RegexNodeKind.Setloop, body.Child(2).Kind);
        Assert.Equal(RuntimeFrontEnd.RegexNodeKind.Set, body.Child(3).Kind);
        Assert.Equal(RuntimeFrontEnd.RegexNodeKind.Setloop, body.Child(4).Kind);
    }

    [Fact]
    public void VendoredRegexParserBuildsLoggingInvocationFamilyShape()
    {
        var tree = RuntimeFrontEnd.RegexParser.Parse(@"\b(?:LogError|LogWarning|LogInformation)\s*\(", RegexOptions.CultureInvariant, CultureInfo.InvariantCulture);
        var body = tree.Root.Child(0);

        Assert.Equal(RuntimeFrontEnd.RegexNodeKind.Concatenate, body.Kind);
        Assert.Equal(RuntimeFrontEnd.RegexNodeKind.Boundary, body.Child(0).Kind);
        Assert.Equal(RuntimeFrontEnd.RegexNodeKind.Group, body.Child(1).Kind);
        Assert.Equal(RuntimeFrontEnd.RegexNodeKind.Alternate, body.Child(1).Child(0).Kind);
        Assert.Equal(RuntimeFrontEnd.RegexNodeKind.Setloop, body.Child(2).Kind);
        Assert.Equal(RuntimeFrontEnd.RegexCharClass.SpaceClass, body.Child(2).Str);
        Assert.Equal(RuntimeFrontEnd.RegexNodeKind.One, body.Child(3).Kind);
    }

    [Fact]
    public void VendoredRegexParserBuildsUnicodeCategorySetNodes()
    {
        var literal = RuntimeFrontEnd.RegexParser.Parse(@"\p{Lu}", RegexOptions.CultureInvariant, CultureInfo.InvariantCulture);
        var negated = RuntimeFrontEnd.RegexParser.Parse(@"\P{Nd}", RegexOptions.CultureInvariant, CultureInfo.InvariantCulture);
        var charClass = RuntimeFrontEnd.RegexParser.Parse(@"[\p{Lu}\P{Nd}]", RegexOptions.CultureInvariant, CultureInfo.InvariantCulture);

        Assert.Equal(RuntimeFrontEnd.RegexNodeKind.Set, literal.Root.Child(0).Kind);
        Assert.True(RuntimeFrontEnd.RegexCharClass.CharInClass('A', literal.Root.Child(0).Str!));
        Assert.Equal(RuntimeFrontEnd.RegexNodeKind.Set, negated.Root.Child(0).Kind);
        Assert.False(RuntimeFrontEnd.RegexCharClass.CharInClass('5', negated.Root.Child(0).Str!));
        Assert.Equal(RuntimeFrontEnd.RegexNodeKind.Set, charClass.Root.Child(0).Kind);
    }

    [Fact]
    public void VendoredRegexParserBuildsUnicodeCharClassNodes()
    {
        var singleton = RuntimeFrontEnd.RegexParser.Parse("[é]", RegexOptions.CultureInvariant, CultureInfo.InvariantCulture);
        var range = RuntimeFrontEnd.RegexParser.Parse("[À-Ö]", RegexOptions.CultureInvariant, CultureInfo.InvariantCulture);
        var escapedRange = RuntimeFrontEnd.RegexParser.Parse(@"[\u00C0-\u00D6]", RegexOptions.CultureInvariant, CultureInfo.InvariantCulture);

        Assert.Equal(RuntimeFrontEnd.RegexNodeKind.Set, singleton.Root.Child(0).Kind);
        Assert.True(RuntimeFrontEnd.RegexCharClass.CharInClass('é', singleton.Root.Child(0).Str!));
        Assert.Equal(RuntimeFrontEnd.RegexNodeKind.Set, range.Root.Child(0).Kind);
        Assert.True(RuntimeFrontEnd.RegexCharClass.CharInClass('Ä', range.Root.Child(0).Str!));
        Assert.False(RuntimeFrontEnd.RegexCharClass.CharInClass('ß', range.Root.Child(0).Str!));
        Assert.Equal(RuntimeFrontEnd.RegexNodeKind.Set, escapedRange.Root.Child(0).Kind);
        Assert.True(RuntimeFrontEnd.RegexCharClass.CharInClass('Ä', escapedRange.Root.Child(0).Str!));
        Assert.False(RuntimeFrontEnd.RegexCharClass.CharInClass('ß', escapedRange.Root.Child(0).Str!));
    }

    [Fact]
    public void VendoredRegexParserExpandsIgnoreCaseCharClasses()
    {
        var tree = RuntimeFrontEnd.RegexParser.Parse("[a]", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, CultureInfo.InvariantCulture);
        var set = tree.Root.Child(0).Str!;

        Assert.Equal(RuntimeFrontEnd.RegexNodeKind.Set, tree.Root.Child(0).Kind);
        Assert.True(RuntimeFrontEnd.RegexCharClass.CharInClass('a', set));
        Assert.True(RuntimeFrontEnd.RegexCharClass.CharInClass('A', set));
        Assert.False(RuntimeFrontEnd.RegexCharClass.CharInClass('b', set));
    }

    [Fact]
    public void VendoredRegexParserBuildsNestedCharClassSubtractions()
    {
        var tree = RuntimeFrontEnd.RegexParser.Parse("[a-z-[d-w-[m-p]]]", RegexOptions.CultureInvariant, CultureInfo.InvariantCulture);
        var set = tree.Root.Child(0).Str!;

        Assert.Equal(RuntimeFrontEnd.RegexNodeKind.Set, tree.Root.Child(0).Kind);
        Assert.True(RuntimeFrontEnd.RegexCharClass.CharInClass('b', set));
        Assert.True(RuntimeFrontEnd.RegexCharClass.CharInClass('n', set));
        Assert.False(RuntimeFrontEnd.RegexCharClass.CharInClass('e', set));
        Assert.True(RuntimeFrontEnd.RegexCharClass.CharInClass('z', set));
    }

    [Fact]
    public void VendoredRegexParserTreatsInlineCommentGroupsAsEmpty()
    {
        var tree = RuntimeFrontEnd.RegexParser.Parse("a(?#comment)b", RegexOptions.CultureInvariant, CultureInfo.InvariantCulture);

        Assert.Equal(RuntimeFrontEnd.RegexNodeKind.Multi, tree.Root.Child(0).Kind);
        Assert.Equal("ab", tree.Root.Child(0).Str);
    }

    [Fact]
    public void VendoredRegexParserBuildsLoopNodesForFallbackQuantifiers()
    {
        var greedy = RuntimeFrontEnd.RegexParser.Parse("ab+", RegexOptions.CultureInvariant, CultureInfo.InvariantCulture);
        var lazy = RuntimeFrontEnd.RegexParser.Parse("ab+?", RegexOptions.CultureInvariant, CultureInfo.InvariantCulture);
        var optionalLazy = RuntimeFrontEnd.RegexParser.Parse("ab??", RegexOptions.CultureInvariant, CultureInfo.InvariantCulture);
        var setLoop = RuntimeFrontEnd.RegexParser.Parse(@"\d+", RegexOptions.CultureInvariant, CultureInfo.InvariantCulture);

        Assert.Equal(RuntimeFrontEnd.RegexNodeKind.Oneloop, greedy.Root.Child(0).Child(1).Kind);
        Assert.Equal(1, greedy.Root.Child(0).Child(1).M);
        Assert.Equal(int.MaxValue, greedy.Root.Child(0).Child(1).N);

        Assert.Equal(RuntimeFrontEnd.RegexNodeKind.Onelazy, lazy.Root.Child(0).Child(1).Kind);
        Assert.Equal(1, lazy.Root.Child(0).Child(1).M);
        Assert.Equal(int.MaxValue, lazy.Root.Child(0).Child(1).N);

        Assert.Equal(RuntimeFrontEnd.RegexNodeKind.Onelazy, optionalLazy.Root.Child(0).Child(1).Kind);
        Assert.Equal(0, optionalLazy.Root.Child(0).Child(1).M);
        Assert.Equal(1, optionalLazy.Root.Child(0).Child(1).N);

        Assert.Equal(RuntimeFrontEnd.RegexNodeKind.Setloop, setLoop.Root.Child(0).Kind);
        Assert.Equal(1, setLoop.Root.Child(0).M);
        Assert.Equal(int.MaxValue, setLoop.Root.Child(0).N);
    }

    [Fact]
    public void FrontEndCanCarryRuntimeTreeForFallbackPatterns()
    {
        var analysis = Utf8FrontEnd.Analyze(@"(?<word>ab)\k<word>", RegexOptions.CultureInvariant);

        Assert.Equal(Utf8SemanticSource.FallbackRegex, analysis.SemanticRegex.Source);
        Assert.NotNull(analysis.SemanticRegex.RuntimeTree);
        Assert.Equal(NativeExecutionKind.FallbackRegex, analysis.RegexPlan.ExecutionKind);
        Assert.Equal("unsupported_backreference", analysis.AnalyzedRegex.FallbackReason);
        Assert.Equal(2, analysis.AnalyzedRegex.Features.CaptureCount);
        Assert.True(analysis.AnalyzedRegex.Features.HasNamedCaptures);
        Assert.True(analysis.AnalyzedRegex.Features.HasBackreferences);
        Assert.Equal(RuntimeFrontEnd.RegexNodeKind.Backreference, analysis.SemanticRegex.RuntimeTree!.Root.Child(0).Child(1).Kind);
    }

    [Fact]
    public void VendoredRegexParserBuildsBackreferenceConditionalFallbackTrees()
    {
        var numeric = RuntimeFrontEnd.RegexParser.Parse("(a)?(?(1)ab|cd)", RegexOptions.CultureInvariant, CultureInfo.InvariantCulture);
        var named = RuntimeFrontEnd.RegexParser.Parse("(?<word>x)(?(<word>)ab|cd)", RegexOptions.CultureInvariant, CultureInfo.InvariantCulture);

        Assert.Equal(RuntimeFrontEnd.RegexNodeKind.Concatenate, numeric.Root.Child(0).Kind);
        Assert.Equal(RuntimeFrontEnd.RegexNodeKind.BackreferenceConditional, numeric.Root.Child(0).Child(1).Kind);
        Assert.Equal(1, numeric.Root.Child(0).Child(1).M);
        Assert.Equal(2, numeric.Root.Child(0).Child(1).ChildCount);
        Assert.Equal(RuntimeFrontEnd.RegexNodeKind.Multi, numeric.Root.Child(0).Child(1).Child(0).Kind);
        Assert.Equal("ab", numeric.Root.Child(0).Child(1).Child(0).Str);
        Assert.Equal("cd", numeric.Root.Child(0).Child(1).Child(1).Str);

        Assert.Equal(RuntimeFrontEnd.RegexNodeKind.Concatenate, named.Root.Child(0).Kind);
        Assert.Equal(RuntimeFrontEnd.RegexNodeKind.BackreferenceConditional, named.Root.Child(0).Child(1).Kind);
        Assert.Equal("word", named.Root.Child(0).Child(1).Str);
    }

    [Fact]
    public void VendoredRegexParserBuildsExpressionConditionalFallbackTrees()
    {
        var tree = RuntimeFrontEnd.RegexParser.Parse("(?(?=a)b|c)", RegexOptions.CultureInvariant, CultureInfo.InvariantCulture);

        Assert.Equal(RuntimeFrontEnd.RegexNodeKind.ExpressionConditional, tree.Root.Child(0).Kind);
        Assert.Equal(3, tree.Root.Child(0).ChildCount);
        Assert.Equal(RuntimeFrontEnd.RegexNodeKind.PositiveLookaround, tree.Root.Child(0).Child(0).Kind);
        Assert.Equal(RuntimeFrontEnd.RegexNodeKind.One, tree.Root.Child(0).Child(0).Child(0).Kind);
        Assert.Equal('a', tree.Root.Child(0).Child(0).Child(0).Ch);
        Assert.Equal(RuntimeFrontEnd.RegexNodeKind.One, tree.Root.Child(0).Child(1).Kind);
        Assert.Equal('b', tree.Root.Child(0).Child(1).Ch);
        Assert.Equal(RuntimeFrontEnd.RegexNodeKind.One, tree.Root.Child(0).Child(2).Kind);
        Assert.Equal('c', tree.Root.Child(0).Child(2).Ch);
    }

    [Fact]
    public void FrontEndCanDeriveNativeLookaheadSearchLiteralFromRuntimeTree()
    {
        var analysis = Utf8FrontEnd.Analyze("(?=ab)ab", RegexOptions.CultureInvariant);

        Assert.Equal(NativeExecutionKind.ExactAsciiLiteral, analysis.RegexPlan.ExecutionKind);
        Assert.Equal(Utf8SearchKind.ExactAsciiLiteral, analysis.AnalyzedRegex.SearchInfo.Kind);
        Assert.Equal("ab", Encoding.UTF8.GetString(analysis.AnalyzedRegex.SearchInfo.LiteralUtf8!));
        Assert.Equal(Utf8SearchKind.ExactAsciiLiteral, analysis.RegexPlan.SearchPlan.Kind);
        Assert.Equal("ab", Encoding.UTF8.GetString(analysis.RegexPlan.SearchPlan.LiteralUtf8!));
    }

    [Fact]
    public void FrontEndCanDeriveFixedDistanceLiteralSearchInfo()
    {
        var analysis = Utf8FrontEnd.Analyze("ab[0-9][0-9]cd", RegexOptions.CultureInvariant);

        Assert.Equal(Utf8SearchKind.FixedDistanceAsciiChar, analysis.AnalyzedRegex.SearchInfo.Kind);
        Assert.Equal("d", Encoding.UTF8.GetString(analysis.AnalyzedRegex.SearchInfo.LiteralUtf8!));
        Assert.Equal(5, analysis.AnalyzedRegex.SearchInfo.Distance);
        Assert.Equal(6, analysis.AnalyzedRegex.SearchInfo.MinRequiredLength);
    }

    [Fact]
    public void FrontEndCanDeriveTrailingFixedLengthAnchorSearchInfo()
    {
        var analysis = Utf8FrontEnd.Analyze(@"abc\z", RegexOptions.CultureInvariant);

        Assert.Equal(Utf8SearchKind.TrailingAnchorFixedLengthEnd, analysis.AnalyzedRegex.SearchInfo.Kind);
        Assert.Equal(3, analysis.AnalyzedRegex.SearchInfo.MinRequiredLength);
    }

    [Fact]
    public void FrontEndAnalyzedFeaturesCanDetectLookaroundsAtomicGroupsLoopsAndAlternation()
    {
        var analysis = Utf8FrontEnd.Analyze(@"(?=ab)(?>a|b)c+", RegexOptions.CultureInvariant);

        Assert.True(analysis.AnalyzedRegex.Features.HasLookarounds);
        Assert.True(analysis.AnalyzedRegex.Features.HasAtomicGroups);
        Assert.True(analysis.AnalyzedRegex.Features.HasLoops);
        Assert.True(analysis.AnalyzedRegex.Features.HasAlternation);
    }

    [Fact]
    public void FrontEndAnalyzedFeaturesAndFallbackReasonCanDetectConditionals()
    {
        var analysis = Utf8FrontEnd.Analyze("(a)?(?(1)ab|cd)", RegexOptions.CultureInvariant);

        Assert.True(analysis.AnalyzedRegex.Features.HasConditionals);
        Assert.Equal("unsupported_conditional", analysis.AnalyzedRegex.FallbackReason);
        Assert.Equal(RuntimeFrontEnd.RegexNodeKind.BackreferenceConditional, analysis.SemanticRegex.RuntimeTree!.Root.Child(0).Child(1).Kind);
    }

    [Fact]
    public void FrontEndCanCarryExpressionConditionalFallbackTrees()
    {
        var analysis = Utf8FrontEnd.Analyze("(?(?=a)b|c)", RegexOptions.CultureInvariant);

        Assert.True(analysis.AnalyzedRegex.Features.HasConditionals);
        Assert.Equal("unsupported_conditional", analysis.AnalyzedRegex.FallbackReason);
        Assert.Equal(RuntimeFrontEnd.RegexNodeKind.ExpressionConditional, analysis.SemanticRegex.RuntimeTree!.Root.Child(0).Kind);
    }

    [Fact]
    public void VendoredRegexParserParsesLeadingInlineOptions()
    {
        var options = RuntimeFrontEnd.RegexParser.ParseOptionsInPattern("(?i)(?m:abc)", RegexOptions.None);

        Assert.True((options & RegexOptions.IgnoreCase) != 0);
        Assert.True((options & RegexOptions.Multiline) != 0);
    }

    [Fact]
    public void VendoredRegexParserSkipsIgnoredPatternWhitespaceAndComments()
    {
        var tree = RuntimeFrontEnd.RegexParser.Parse("(?x) a b # comment", RegexOptions.CultureInvariant, CultureInfo.InvariantCulture);

        Assert.Equal(RuntimeFrontEnd.RegexNodeKind.Capture, tree.Root.Kind);
        Assert.Equal(RuntimeFrontEnd.RegexNodeKind.Multi, tree.Root.Child(0).Kind);
        Assert.Equal("ab", tree.Root.Child(0).Str);
    }

    [Fact]
    public void VendoredRegexParserAppliesStandaloneInlineOptionsToRemainingScope()
    {
        var tree = RuntimeFrontEnd.RegexParser.Parse("a(?i)bC", RegexOptions.CultureInvariant, CultureInfo.InvariantCulture);
        var body = tree.Root.Child(0);

        Assert.Equal(RuntimeFrontEnd.RegexNodeKind.Concatenate, body.Kind);
        Assert.Equal(RuntimeFrontEnd.RegexNodeKind.One, body.Child(0).Kind);
        Assert.Equal('a', body.Child(0).Ch);
        Assert.Equal(RuntimeFrontEnd.RegexNodeKind.Set, body.Child(1).Kind);
        Assert.Equal(RuntimeFrontEnd.RegexNodeKind.Set, body.Child(2).Kind);
        Assert.True(RuntimeFrontEnd.RegexCharClass.CharInClass('b', body.Child(1).Str!));
        Assert.True(RuntimeFrontEnd.RegexCharClass.CharInClass('B', body.Child(1).Str!));
        Assert.True(RuntimeFrontEnd.RegexCharClass.CharInClass('c', body.Child(2).Str!));
        Assert.True(RuntimeFrontEnd.RegexCharClass.CharInClass('C', body.Child(2).Str!));
    }

    [Fact]
    public void VendoredRegexParserParsesBootstrapLiteralWithLeadingGlobalOptions()
    {
        var tree = RuntimeFrontEnd.RegexParser.Parse("(?i)abc", RegexOptions.CultureInvariant, CultureInfo.InvariantCulture);

        Assert.Equal(RuntimeFrontEnd.RegexNodeKind.Capture, tree.Root.Kind);
        Assert.Equal(RuntimeFrontEnd.RegexNodeKind.Concatenate, tree.Root.Child(0).Kind);
        Assert.Equal(RuntimeFrontEnd.RegexNodeKind.Set, tree.Root.Child(0).Child(0).Kind);
        Assert.Equal(RuntimeFrontEnd.RegexNodeKind.Set, tree.Root.Child(0).Child(1).Kind);
        Assert.Equal(RuntimeFrontEnd.RegexNodeKind.Set, tree.Root.Child(0).Child(2).Kind);
        Assert.True((tree.Options & RegexOptions.IgnoreCase) != 0);
    }

    [Fact]
    public void VendoredRegexParserUsesAnyClassForSinglelineDot()
    {
        var tree = RuntimeFrontEnd.RegexParser.Parse("a.b", RegexOptions.Singleline | RegexOptions.CultureInvariant, CultureInfo.InvariantCulture);
        var body = tree.Root.Child(0);

        Assert.Equal(RuntimeFrontEnd.RegexNodeKind.Concatenate, body.Kind);
        Assert.Equal(RuntimeFrontEnd.RegexNodeKind.Set, body.Child(1).Kind);
        Assert.Equal(RuntimeFrontEnd.RegexCharClass.AnyClass, body.Child(1).Str);
    }

    [Fact]
    public void VendoredRegexParserParsesReplacementTokens()
    {
        var replacement = RuntimeFrontEnd.RegexParser.ParseReplacement("x$1$&$$${2}");

        Assert.Collection(
            replacement.Tokens,
            token =>
            {
                Assert.Equal(RuntimeFrontEnd.RegexReplacementTokenKind.Literal, token.Kind);
                Assert.Equal("x", token.Literal);
            },
            token =>
            {
                Assert.Equal(RuntimeFrontEnd.RegexReplacementTokenKind.Group, token.Kind);
                Assert.Equal(1, token.GroupNumber);
            },
            token => Assert.Equal(RuntimeFrontEnd.RegexReplacementTokenKind.WholeMatch, token.Kind),
            token =>
            {
                Assert.Equal(RuntimeFrontEnd.RegexReplacementTokenKind.Literal, token.Kind);
                Assert.Equal("$", token.Literal);
            },
            token =>
            {
                Assert.Equal(RuntimeFrontEnd.RegexReplacementTokenKind.Group, token.Kind);
                Assert.Equal(2, token.GroupNumber);
                Assert.True(token.IsBraceEnclosed);
            });
    }

    [Fact]
    public void VendoredRegexReplacementPatternCanClassifyLiteralOnlyReplacement()
    {
        var replacement = RuntimeFrontEnd.RegexParser.ParseReplacement("x$$y");

        Assert.False(replacement.ContainsSubstitutions);
        Assert.False(replacement.ContainsGroupReferences);
        Assert.False(replacement.ContainsSpecialSubstitutions);
        Assert.True(replacement.TryGetLiteralText(out var literalText));
        Assert.Equal("x$y", literalText);
    }

    [Fact]
    public void FrontEndCanAnalyzeLiteralOnlyReplacement()
    {
        var analyzed = Utf8FrontEndReplacementAnalyzer.Analyze("x$$y");

        Assert.True(analyzed.IsLiteral);
        Assert.Equal("x$y", Encoding.UTF8.GetString(analyzed.LiteralUtf8!));
        Assert.Collection(
            analyzed.Plan.Instructions,
            instruction =>
            {
                Assert.Equal(Utf8ReplacementInstructionKind.Literal, instruction.Kind);
                Assert.Equal("x$y", Encoding.UTF8.GetString(instruction.LiteralUtf8!));
            });
    }

    [Fact]
    public void VendoredRegexReplacementPatternCanClassifySubstitutionKinds()
    {
        var replacement = RuntimeFrontEnd.RegexParser.ParseReplacement("${name}-$1-$&");

        Assert.True(replacement.ContainsSubstitutions);
        Assert.True(replacement.ContainsGroupReferences);
        Assert.True(replacement.ContainsNamedGroups);
        Assert.True(replacement.ContainsSpecialSubstitutions);
    }

    [Fact]
    public void FrontEndCanLiteralizeInvalidReplacementGroupReferences()
    {
        var analyzed = Utf8FrontEndReplacementAnalyzer.Analyze("$1-${1}-${name}-$10", [0], ["0"]);

        Assert.True(analyzed.IsLiteral);
        Assert.Equal("$1-${1}-${name}-$10", Encoding.UTF8.GetString(analyzed.LiteralUtf8!));
        Assert.True(analyzed.ContainsGroupReferences);
        Assert.True(analyzed.ContainsNamedGroups);
        Assert.False(analyzed.ContainsSpecialSubstitutions);
    }

    [Fact]
    public void FrontEndKeepsValidReplacementGroupReferencesAsSubstitutions()
    {
        var analyzed = Utf8FrontEndReplacementAnalyzer.Analyze("$1-${name}", [0, 1], ["0", "name"]);

        Assert.False(analyzed.IsLiteral);
        Assert.True(analyzed.ContainsSubstitutions);
        Assert.True(analyzed.ContainsGroupReferences);
        Assert.True(analyzed.ContainsNamedGroups);
    }

    [Fact]
    public void FrontEndLowersReplacementTokensIntoSharedPlan()
    {
        var analyzed = Utf8FrontEndReplacementAnalyzer.Analyze("${name}-$1-$&", [0, 1], ["0", "name"]);

        Assert.Collection(
            analyzed.Plan.Instructions,
            instruction =>
            {
                Assert.Equal(Utf8ReplacementInstructionKind.Group, instruction.Kind);
                Assert.Equal(1, instruction.GroupNumber);
                Assert.True(instruction.IsBraceEnclosed);
            },
            instruction =>
            {
                Assert.Equal(Utf8ReplacementInstructionKind.Literal, instruction.Kind);
                Assert.Equal("-", Encoding.UTF8.GetString(instruction.LiteralUtf8!));
            },
            instruction =>
            {
                Assert.Equal(Utf8ReplacementInstructionKind.Group, instruction.Kind);
                Assert.Equal(1, instruction.GroupNumber);
            },
            instruction =>
            {
                Assert.Equal(Utf8ReplacementInstructionKind.Literal, instruction.Kind);
                Assert.Equal("-", Encoding.UTF8.GetString(instruction.LiteralUtf8!));
            },
            instruction => Assert.Equal(Utf8ReplacementInstructionKind.WholeMatch, instruction.Kind));
    }

    [Fact]
    public void ReplacementPlanInterpreterAppliesSpecialReplacementSubstitutions()
    {
        const string input = "xab ay";
        var regex = new Regex("(a)(b)?", RegexOptions.CultureInvariant);
        var match = regex.Match(input);
        var replacement = Utf8FrontEndReplacementAnalyzer.Analyze("$`|$'|$+|$_", [0, 1, 2], ["0", "1", "2"]);

        var rewritten = Utf8ReplacementPlanInterpreter.Apply(replacement.Plan, match, input);

        Assert.Equal(match.Result("$`|$'|$+|$_"), rewritten);
    }

    [Fact]
    public void VendoredRegexParserParsesNamedReplacementTokens()
    {
        var replacement = RuntimeFrontEnd.RegexParser.ParseReplacement("${name}-$1");

        Assert.Collection(
            replacement.Tokens,
            token =>
            {
                Assert.Equal(RuntimeFrontEnd.RegexReplacementTokenKind.Group, token.Kind);
                Assert.Equal("name", token.GroupName);
                Assert.True(token.IsBraceEnclosed);
            },
            token =>
            {
                Assert.Equal(RuntimeFrontEnd.RegexReplacementTokenKind.Literal, token.Kind);
                Assert.Equal("-", token.Literal);
            },
            token =>
            {
                Assert.Equal(RuntimeFrontEnd.RegexReplacementTokenKind.Group, token.Kind);
                Assert.Equal(1, token.GroupNumber);
                Assert.False(token.IsBraceEnclosed);
            });
    }

    [Fact]
    public void VendoredRegexParserCanResolveReplacementGroupsAgainstCaptureMetadata()
    {
        var replacement = RuntimeFrontEnd.RegexParser.ParseReplacement("${name}-$1-${missing}-$9", [0, 1], ["0", "name"]);

        Assert.Collection(
            replacement.Tokens,
            token =>
            {
                Assert.Equal(RuntimeFrontEnd.RegexReplacementTokenKind.Group, token.Kind);
                Assert.Equal(1, token.GroupNumber);
                Assert.True(token.IsBraceEnclosed);
            },
            token =>
            {
                Assert.Equal(RuntimeFrontEnd.RegexReplacementTokenKind.Literal, token.Kind);
                Assert.Equal("-", token.Literal);
            },
            token =>
            {
                Assert.Equal(RuntimeFrontEnd.RegexReplacementTokenKind.Group, token.Kind);
                Assert.Equal(1, token.GroupNumber);
                Assert.False(token.IsBraceEnclosed);
            },
            token =>
            {
                Assert.Equal(RuntimeFrontEnd.RegexReplacementTokenKind.Literal, token.Kind);
                Assert.Equal("-${missing}-$9", token.Literal);
            });
    }

    [Fact]
    public void VendoredRegexParserResolvesReplacementGroupsDuringMetadataAwareParsing()
    {
        var replacement = RuntimeFrontEnd.RegexParser.ParseReplacement("${name}-$1-$11", [0, 1], ["0", "name"]);

        Assert.Collection(
            replacement.Tokens,
            token =>
            {
                Assert.Equal(RuntimeFrontEnd.RegexReplacementTokenKind.Group, token.Kind);
                Assert.Equal(1, token.GroupNumber);
                Assert.True(token.IsBraceEnclosed);
            },
            token =>
            {
                Assert.Equal(RuntimeFrontEnd.RegexReplacementTokenKind.Literal, token.Kind);
                Assert.Equal("-", token.Literal);
            },
            token =>
            {
                Assert.Equal(RuntimeFrontEnd.RegexReplacementTokenKind.Group, token.Kind);
                Assert.Equal(1, token.GroupNumber);
            },
            token =>
            {
                Assert.Equal(RuntimeFrontEnd.RegexReplacementTokenKind.Literal, token.Kind);
                Assert.Equal("-$11", token.Literal);
            });
    }

    [Fact]
    public void VendoredRegexParserMatchesRuntimeNumberedReplacementDisambiguation()
    {
        var invalidWide = RuntimeFrontEnd.RegexParser.ParseReplacement("$11", [0, 1, 2], ["0", "1", "2"]);
        var padded = RuntimeFrontEnd.RegexParser.ParseReplacement("$01", [0, 1, 2], ["0", "1", "2"]);
        var validWide = RuntimeFrontEnd.RegexParser.ParseReplacement("$11", Enumerable.Range(0, 12).ToArray(), Enumerable.Range(0, 12).Select(static i => i.ToString(CultureInfo.InvariantCulture)).ToArray());
        var wholeMatch = RuntimeFrontEnd.RegexParser.ParseReplacement("$000", [0, 1, 2], ["0", "1", "2"]);
        var paddedSingle = RuntimeFrontEnd.RegexParser.ParseReplacement("$001", [0, 1, 2], ["0", "1", "2"]);

        Assert.Collection(
            invalidWide.Tokens,
            token =>
            {
                Assert.Equal(RuntimeFrontEnd.RegexReplacementTokenKind.Literal, token.Kind);
                Assert.Equal("$11", token.Literal);
            });
        Assert.Collection(
            padded.Tokens,
            token =>
            {
                Assert.Equal(RuntimeFrontEnd.RegexReplacementTokenKind.Group, token.Kind);
                Assert.Equal(1, token.GroupNumber);
            });
        Assert.Collection(
            validWide.Tokens,
            token =>
            {
                Assert.Equal(RuntimeFrontEnd.RegexReplacementTokenKind.Group, token.Kind);
                Assert.Equal(11, token.GroupNumber);
            });
        Assert.Collection(
            wholeMatch.Tokens,
            token =>
            {
                Assert.Equal(RuntimeFrontEnd.RegexReplacementTokenKind.Group, token.Kind);
                Assert.Equal(0, token.GroupNumber);
            });
        Assert.Collection(
            paddedSingle.Tokens,
            token =>
            {
                Assert.Equal(RuntimeFrontEnd.RegexReplacementTokenKind.Group, token.Kind);
                Assert.Equal(1, token.GroupNumber);
            });
    }

    [Fact]
    public void VendoredRegexParserLiteralizesMalformedReplacementTokens()
    {
        var bareDollar = RuntimeFrontEnd.RegexParser.ParseReplacement("$");
        var badNamed = RuntimeFrontEnd.RegexParser.ParseReplacement("${}");
        var unterminatedNamed = RuntimeFrontEnd.RegexParser.ParseReplacement("${name");
        var badMarker = RuntimeFrontEnd.RegexParser.ParseReplacement("$x");
        var escapedThenGroup = RuntimeFrontEnd.RegexParser.ParseReplacement("$$$1");

        Assert.True(bareDollar.TryGetLiteralText(out var bareText));
        Assert.Equal("$", bareText);
        Assert.True(badNamed.TryGetLiteralText(out var badNamedText));
        Assert.Equal("${}", badNamedText);
        Assert.True(unterminatedNamed.TryGetLiteralText(out var unterminatedText));
        Assert.Equal("${name", unterminatedText);
        Assert.True(badMarker.TryGetLiteralText(out var badMarkerText));
        Assert.Equal("$x", badMarkerText);
        Assert.Collection(
            escapedThenGroup.Tokens,
            token =>
            {
                Assert.Equal(RuntimeFrontEnd.RegexReplacementTokenKind.Literal, token.Kind);
                Assert.Equal("$", token.Literal);
            },
            token =>
            {
                Assert.Equal(RuntimeFrontEnd.RegexReplacementTokenKind.Group, token.Kind);
                Assert.Equal(1, token.GroupNumber);
            });
    }

    [Fact]
    public void VendoredRegexParserCoalescesReplacementLiteralRunsAroundResolvedGroups()
    {
        var replacement = RuntimeFrontEnd.RegexParser.ParseReplacement("pre${name}mid$1post", [0, 1], ["0", "name"]);

        Assert.Collection(
            replacement.Tokens,
            token =>
            {
                Assert.Equal(RuntimeFrontEnd.RegexReplacementTokenKind.Literal, token.Kind);
                Assert.Equal("pre", token.Literal);
            },
            token =>
            {
                Assert.Equal(RuntimeFrontEnd.RegexReplacementTokenKind.Group, token.Kind);
                Assert.Equal(1, token.GroupNumber);
                Assert.True(token.IsBraceEnclosed);
            },
            token =>
            {
                Assert.Equal(RuntimeFrontEnd.RegexReplacementTokenKind.Literal, token.Kind);
                Assert.Equal("mid", token.Literal);
            },
            token =>
            {
                Assert.Equal(RuntimeFrontEnd.RegexReplacementTokenKind.Group, token.Kind);
                Assert.Equal(1, token.GroupNumber);
                Assert.False(token.IsBraceEnclosed);
            },
            token =>
            {
                Assert.Equal(RuntimeFrontEnd.RegexReplacementTokenKind.Literal, token.Kind);
                Assert.Equal("post", token.Literal);
            });
    }

    [Fact]
    public void VendoredRegexTreeAnalyzerMarksCapturesLoopsAndAtomicity()
    {
        var tree = RuntimeFrontEnd.RegexParser.Parse("(a(b|c)){2}", RegexOptions.CultureInvariant, CultureInfo.InvariantCulture);
        var analysis = RuntimeFrontEnd.RegexTreeAnalyzer.Analyze(tree);
        var rootCapture = tree.Root;
        var loop = rootCapture.Child(0);
        var repeatedCapture = loop.Child(0);
        var concatenation = repeatedCapture.Child(0);
        var alternation = concatenation.Child(1);

        Assert.True(analysis.Complete);
        Assert.True(analysis.MayContainCapture(rootCapture));
        Assert.True(analysis.MayContainCapture(loop));
        Assert.True(analysis.IsAtomicByAncestor(rootCapture));
        Assert.True(analysis.IsInLoop(repeatedCapture));
        Assert.True(analysis.IsInLoop(alternation));
        Assert.True(analysis.MayBacktrack(loop));
        Assert.True(analysis.MayBacktrack(alternation));
    }

    [Fact]
    public void FrontEndCarriesVendoredRuntimeTreeAnalysis()
    {
        var analysis = Utf8FrontEnd.Analyze("(a(b|c)){2}", RegexOptions.CultureInvariant);

        Assert.NotNull(analysis.SemanticRegex.RuntimeAnalysis);
        Assert.True(analysis.SemanticRegex.RuntimeAnalysis!.MayContainCapture(analysis.SemanticRegex.RuntimeTree!.Root));
        Assert.True(analysis.SemanticRegex.RuntimeAnalysis.IsInLoop(analysis.SemanticRegex.RuntimeTree.Root.Child(0).Child(0)));
    }

    [Fact]
    public void FrontEndCanNormalizeMixedCaptureNumberingFromRuntimeMetadata()
    {
        var analysis = Utf8FrontEnd.Analyze("(?<name>a)(b)", RegexOptions.CultureInvariant);
        var body = analysis.SemanticRegex.RuntimeTree!.Root.Child(0);

        Assert.Equal(RuntimeFrontEnd.RegexNodeKind.Concatenate, body.Kind);
        Assert.Equal(RuntimeFrontEnd.RegexNodeKind.Capture, body.Child(0).Kind);
        Assert.Equal("name", body.Child(0).Str);
        Assert.Equal(2, body.Child(0).M);
        Assert.Equal(RuntimeFrontEnd.RegexNodeKind.Capture, body.Child(1).Kind);
        Assert.Equal(1, body.Child(1).M);
        Assert.NotNull(analysis.SemanticRegex.RuntimeTree.CaptureNames);
        Assert.Equal(["0", "1", "name"], analysis.SemanticRegex.RuntimeTree.CaptureNames!);
        Assert.Equal(2, analysis.SemanticRegex.RuntimeTree.CaptureNameToNumberMapping!["name"]);
    }

    [Fact]
    public void FrontEndCanNormalizeNamedBackreferenceNumbersFromRuntimeMetadata()
    {
        var analysis = Utf8FrontEnd.Analyze("(?<name>a)(b)\\k<name>", RegexOptions.CultureInvariant);
        var body = analysis.SemanticRegex.RuntimeTree!.Root.Child(0);

        Assert.Equal(RuntimeFrontEnd.RegexNodeKind.Backreference, body.Child(2).Kind);
        Assert.Equal("name", body.Child(2).Str);
        Assert.Equal(2, body.Child(2).M);
    }

    [Fact]
    public void VendoredRegexParserAllowsDuplicateNamedCaptures()
    {
        var tree = RuntimeFrontEnd.RegexParser.Parse("(?<x>a)(?<x>b)\\k<x>", RegexOptions.CultureInvariant, CultureInfo.InvariantCulture);
        var body = tree.Root.Child(0);

        Assert.Equal(RuntimeFrontEnd.RegexNodeKind.Concatenate, body.Kind);
        Assert.Equal(RuntimeFrontEnd.RegexNodeKind.Capture, body.Child(0).Kind);
        Assert.Equal("x", body.Child(0).Str);
        Assert.Equal(1, body.Child(0).M);
        Assert.Equal(RuntimeFrontEnd.RegexNodeKind.Capture, body.Child(1).Kind);
        Assert.Equal("x", body.Child(1).Str);
        Assert.Equal(1, body.Child(1).M);
        Assert.Equal(RuntimeFrontEnd.RegexNodeKind.Backreference, body.Child(2).Kind);
        Assert.Equal("x", body.Child(2).Str);
        Assert.Equal(1, body.Child(2).M);
        Assert.NotNull(tree.CaptureNames);
        Assert.Equal(["0", "x"], tree.CaptureNames);
        Assert.Equal(1, tree.CaptureNameToNumberMapping!["x"]);
    }

    [Fact]
    public void VendoredRegexParserKeepsNumericGroupNamesInvariantAcrossCultures()
    {
        var tree = RuntimeFrontEnd.RegexParser.Parse("(?<x>a)(b)", RegexOptions.None, CultureInfo.GetCultureInfo("ar-SA"));

        Assert.Equal(3, tree.CaptureCount);
        Assert.NotNull(tree.CaptureNames);
        Assert.Equal(["0", "1", "x"], tree.CaptureNames);
        Assert.Equal(2, tree.CaptureNameToNumberMapping!["x"]);
    }

    [Fact]
    public void VendoredRegexParserKeepsNumericQuantifiersInvariantAcrossCultures()
    {
        var tree = RuntimeFrontEnd.RegexParser.Parse("a{2,4}", RegexOptions.None, CultureInfo.GetCultureInfo("ar-SA"));

        Assert.Equal(RuntimeFrontEnd.RegexNodeKind.Oneloop, tree.Root.Child(0).Kind);
        Assert.Equal(2, tree.Root.Child(0).M);
        Assert.Equal(4, tree.Root.Child(0).N);
    }

    [Fact]
    public void FrontEndCanNormalizeDuplicateNamedCaptureSemantics()
    {
        var analysis = Utf8FrontEnd.Analyze("(?<x>a)(?<x>b)\\k<x>", RegexOptions.CultureInvariant);
        var body = analysis.SemanticRegex.RuntimeTree!.Root.Child(0);

        Assert.Equal(2, analysis.AnalyzedRegex.Features.CaptureCount);
        Assert.True(analysis.AnalyzedRegex.Features.HasNamedCaptures);
        Assert.Equal(RuntimeFrontEnd.RegexNodeKind.Capture, body.Child(0).Kind);
        Assert.Equal(1, body.Child(0).M);
        Assert.Equal(RuntimeFrontEnd.RegexNodeKind.Capture, body.Child(1).Kind);
        Assert.Equal(1, body.Child(1).M);
        Assert.Equal(RuntimeFrontEnd.RegexNodeKind.Backreference, body.Child(2).Kind);
        Assert.Equal(1, body.Child(2).M);
    }

    [Fact]
    public void VendoredRegexParserBuildsBalancingCaptureFallbackTrees()
    {
        var tree = RuntimeFrontEnd.RegexParser.Parse("(?<a>b)(?<c-a>x)", RegexOptions.CultureInvariant, CultureInfo.InvariantCulture);
        var body = tree.Root.Child(0);

        Assert.Equal(RuntimeFrontEnd.RegexNodeKind.Concatenate, body.Kind);
        Assert.Equal(RuntimeFrontEnd.RegexNodeKind.Capture, body.Child(0).Kind);
        Assert.Equal("a", body.Child(0).Str);
        Assert.Equal(1, body.Child(0).M);
        Assert.Equal(-1, body.Child(0).N);
        Assert.Equal(RuntimeFrontEnd.RegexNodeKind.Capture, body.Child(1).Kind);
        Assert.Equal("c", body.Child(1).Str);
        Assert.Equal(2, body.Child(1).M);
        Assert.Equal(1, body.Child(1).N);
        Assert.Null(body.Child(1).Str2);
        Assert.NotNull(tree.CaptureNames);
        Assert.Equal(["0", "a", "c"], tree.CaptureNames);
    }

    [Fact]
    public void VendoredRegexParserBuildsUncaptureBalancingFallbackTrees()
    {
        var tree = RuntimeFrontEnd.RegexParser.Parse("(?<a>b)(?<-a>x)", RegexOptions.CultureInvariant, CultureInfo.InvariantCulture);
        var body = tree.Root.Child(0);

        Assert.Equal(RuntimeFrontEnd.RegexNodeKind.Capture, body.Child(1).Kind);
        Assert.Null(body.Child(1).Str);
        Assert.Equal(-1, body.Child(1).M);
        Assert.Equal(1, body.Child(1).N);
        Assert.Null(body.Child(1).Str2);
    }

    [Fact]
    public void VendoredRegexParserRejectsUndefinedBalancingReferences()
    {
        var named = Assert.Throws<RuntimeFrontEnd.RegexParseException>(() =>
            RuntimeFrontEnd.RegexParser.Parse("(?<a-b>x)", RegexOptions.CultureInvariant, CultureInfo.InvariantCulture));
        var numbered = Assert.Throws<RuntimeFrontEnd.RegexParseException>(() =>
            RuntimeFrontEnd.RegexParser.Parse("(?<2-1>x)", RegexOptions.CultureInvariant, CultureInfo.InvariantCulture));

        Assert.Equal(RuntimeFrontEnd.RegexParseError.UndefinedNamedReference, named.Error);
        Assert.Equal(RuntimeFrontEnd.RegexParseError.UndefinedNumberedReference, numbered.Error);
    }

    [Fact]
    public void FrontEndKeepsBalancingGroupsOnFallbackPath()
    {
        var analysis = Utf8FrontEnd.Analyze("(?<a>b)(?<c-a>x)", RegexOptions.CultureInvariant);
        var body = analysis.SemanticRegex.RuntimeTree!.Root.Child(0);

        Assert.Equal(Utf8SemanticSource.FallbackRegex, analysis.SemanticRegex.Source);
        Assert.Equal(NativeExecutionKind.FallbackRegex, analysis.RegexPlan.ExecutionKind);
        Assert.Equal(RuntimeFrontEnd.RegexNodeKind.Capture, body.Child(1).Kind);
        Assert.Equal(1, body.Child(1).N);
        Assert.Null(body.Child(1).Str2);
    }

    [Fact]
    public void VendoredRegexParserBuildsCharacterClassSubtractionSets()
    {
        var tree = RuntimeFrontEnd.RegexParser.Parse("[a-z-[aeiou]]", RegexOptions.CultureInvariant, CultureInfo.InvariantCulture);
        var set = tree.Root.Child(0).Str;

        Assert.NotNull(set);
        Assert.True(RuntimeFrontEnd.RegexCharClass.IsSubtraction(set));
        Assert.True(RuntimeFrontEnd.RegexCharClass.CharInClass('b', set));
        Assert.False(RuntimeFrontEnd.RegexCharClass.CharInClass('a', set));
        Assert.False(RuntimeFrontEnd.RegexCharClass.CharInClass('e', set));
    }

    [Fact]
    public void FrontEndKeepsCharacterClassSubtractionsOnFallbackPath()
    {
        var analysis = Utf8FrontEnd.Analyze("[a-z-[aeiou]]", RegexOptions.CultureInvariant);

        Assert.Equal(Utf8SemanticSource.FallbackRegex, analysis.SemanticRegex.Source);
        Assert.Equal(NativeExecutionKind.FallbackRegex, analysis.RegexPlan.ExecutionKind);
        Assert.True(RuntimeFrontEnd.RegexCharClass.IsSubtraction(analysis.SemanticRegex.RuntimeTree!.Root.Child(0).Str!));
    }
}
