using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.FrontEnd;
using Lokad.Utf8Regex.Internal.FrontEnd.Runtime;
using Lokad.Utf8Regex.Internal.Planning;
using Lokad.Utf8Regex.Internal.Utilities;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8SearchPlanTests
{
    [Fact]
    public void ConstructorCreatesExactLiteralSearchPlanForPlainAsciiLiteral()
    {
        var regex = new Utf8Regex("needle", RegexOptions.CultureInvariant);

        Assert.Equal(Utf8SearchKind.ExactAsciiLiteral, regex.SearchPlan.Kind);
        Assert.True(regex.SearchPlan.HasLiteral);
        Assert.True(regex.SearchPlan.NativeSearch.HasPreparedSearcher);
        Assert.Equal(PreparedSearcherKind.ExactLiteral, regex.SearchPlan.PreparedSearcher.Kind);
        Assert.Equal(regex.SearchPlan.PreparedSearcher.Kind, regex.SearchPlan.NativeSearch.PreparedSearcher.Kind);
    }

    [Fact]
    public void ConstructorCreatesIgnoreCaseSearchPlanForInvariantIgnoreCaseLiteral()
    {
        var regex = new Utf8Regex("needle", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        Assert.Equal(Utf8SearchKind.AsciiLiteralIgnoreCase, regex.SearchPlan.Kind);
        Assert.True(regex.SearchPlan.HasLiteral);
        Assert.Equal(PreparedSearcherKind.IgnoreCaseLiteral, regex.SearchPlan.PreparedSearcher.Kind);
    }

    [Fact]
    public void ConstructorCreatesIgnoreCaseSearchPlanForInvariantIgnoreCaseLiteralAlternation()
    {
        var regex = new Utf8Regex("needle|thread|fiber", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        Assert.Equal(Utf8SearchKind.AsciiLiteralIgnoreCaseLiterals, regex.SearchPlan.Kind);
        Assert.Equal(NativeExecutionKind.AsciiLiteralIgnoreCaseLiterals, regex.ExecutionKind);
        Assert.Equal(Utf8SearchPortfolioKind.AsciiIgnoreCaseFamily, regex.SearchPortfolioKind);
        Assert.True(regex.SearchPlan.HasAlternateLiterals);
        Assert.True(regex.SearchPlan.AlternateIgnoreCaseLiteralSearch.HasValue);
        Assert.Equal(PreparedSearcherKind.MultiLiteral, regex.SearchPlan.PreparedSearcher.Kind);
    }

    [Fact]
    public void ConstructorCanCreateTreeDerivedSearchPlanForFallbackPattern()
    {
        var regex = new Utf8Regex("a.*b", RegexOptions.CultureInvariant);

        Assert.Equal(Utf8SearchKind.ExactAsciiLiteral, regex.SearchPlan.Kind);
        Assert.True(regex.SearchPlan.HasLiteral);
        Assert.True(regex.SearchPlan.HasFallbackCandidates);
        Assert.True(regex.StructuralSearchPlan.HasValue);
        Assert.True(regex.SearchPlan.NativeSearch.HasStructuralCandidates);
        Assert.Equal(Utf8StructuralSearchYieldKind.Start, regex.StructuralSearchPlan.YieldKind);
        Assert.Equal(Utf8StructuralSearchStageKind.FindLiteralFamily, regex.StructuralSearchPlan.Stages![0].Kind);
        Assert.Contains(regex.StructuralSearchPlan.Stages!, static stage => stage.Kind == Utf8StructuralSearchStageKind.RequireByteAtOffset);
        Assert.Equal(Utf8StructuralSearchStageKind.YieldStart, regex.StructuralSearchPlan.Stages![^1].Kind);
    }

    [Fact]
    public void ConstructorCanCreateAlternationPrefixSearchPlanForAsciiAlternation()
    {
        var regex = new Utf8Regex("cat|horse", RegexOptions.CultureInvariant);

        Assert.Equal(Utf8SearchKind.ExactAsciiLiterals, regex.SearchPlan.Kind);
        Assert.Equal(NativeExecutionKind.ExactUtf8Literals, regex.ExecutionKind);
        Assert.NotEqual(Utf8SearchPortfolioKind.None, regex.SearchPortfolioKind);
        Assert.True(regex.SearchPlan.HasAlternateLiterals);
        Assert.True(regex.SearchPlan.AlternateLiteralSearch.HasValue);
        Assert.Equal(PreparedSearcherKind.MultiLiteral, regex.SearchPlan.PreparedSearcher.Kind);
        Assert.Equal(["cat", "horse"], regex.SearchPlan.AlternateLiteralsUtf8!.Select(Encoding.UTF8.GetString));
    }

    [Fact]
    public void ConstructorCanCreateAlternationSearchPlanForUtf8Alternation()
    {
        var regex = new Utf8Regex("café|niño", RegexOptions.CultureInvariant);

        Assert.Equal(Utf8SearchKind.ExactUtf8Literals, regex.SearchPlan.Kind);
        Assert.True(regex.SearchPlan.HasAlternateLiterals);
        Assert.True(regex.SearchPlan.AlternateLiteralSearch.HasValue);
        Assert.Equal(["café", "niño"], regex.SearchPlan.AlternateLiteralsUtf8!.Select(Encoding.UTF8.GetString));
    }

    [Fact]
    public void ConstructorCanCreateBoundaryWrappedAlternationSearchPlanForUtf8Alternation()
    {
        var regex = new Utf8Regex(@"\b(?:café|niño)\b", RegexOptions.CultureInvariant);

        Assert.Equal(Utf8SearchKind.ExactUtf8Literals, regex.SearchPlan.Kind);
        Assert.True(regex.SearchPlan.HasAlternateLiterals);
        Assert.True(regex.SearchPlan.AlternateLiteralSearch.HasValue);
        Assert.True(regex.StructuralSearchPlan.HasValue);
        Assert.Equal(Utf8StructuralSearchYieldKind.Start, regex.StructuralSearchPlan.YieldKind);
        Assert.Equal(Utf8BoundaryRequirement.Boundary, regex.SearchPlan.LeadingBoundary);
        Assert.Equal(Utf8BoundaryRequirement.Boundary, regex.SearchPlan.TrailingBoundary);
        Assert.Equal(["café", "niño"], regex.SearchPlan.AlternateLiteralsUtf8!.Select(Encoding.UTF8.GetString));
    }

    [Fact]
    public void ConstructorCanSelectEarliestPortfolioForTinyLongUtf8Alternation()
    {
        var regex = new Utf8Regex(@"\b(?:Task|ValueTask|IAsyncEnumerable)\b", RegexOptions.CultureInvariant);

        Assert.Equal(Utf8SearchKind.ExactAsciiLiterals, regex.SearchPlan.Kind);
        Assert.Equal(Utf8SearchPortfolioKind.ExactEarliestFamily, regex.SearchPortfolioKind);
        Assert.Equal(PreparedMultiLiteralKind.ExactEarliest, regex.SearchPlan.MultiLiteralSearch.Kind);
    }

    [Fact]
    public void StructuralSearchPlanCanFilterBoundaryWrappedUtf8AlternationCandidates()
    {
        var regex = new Utf8Regex(@"\b(?:café|niño)\b", RegexOptions.CultureInvariant);
        var input = Encoding.UTF8.GetBytes("xcafé café niñoz niño");
        var firstExpected = Encoding.UTF8.GetByteCount("xcafé ");
        var secondExpected = Encoding.UTF8.GetByteCount("xcafé café niñoz ");
        var state = new Utf8StructuralSearchState(new PreparedSearchScanState(0, default), new PreparedWindowScanState(0, new PreparedSearchScanState(0, default)));

        Assert.True(regex.StructuralSearchPlan.TryFindNextCandidate(input, ref state, out var first));
        Assert.Equal(firstExpected, first.StartIndex);
        Assert.True(regex.StructuralSearchPlan.TryFindNextCandidate(input, ref state, out var second));
        Assert.Equal(secondExpected, second.StartIndex);
        Assert.False(regex.StructuralSearchPlan.TryFindNextCandidate(input, ref state, out _));
    }

    [Fact]
    public void StructuralSearchPlanCanFindLastFilteredUtf8AlternationCandidate()
    {
        var regex = new Utf8Regex(@"\b(?:café|niño)\b", RegexOptions.CultureInvariant);
        var input = Encoding.UTF8.GetBytes("xcafé café niñoz niño");
        var expected = Encoding.UTF8.GetByteCount("xcafé café niñoz ");

        Assert.True(regex.StructuralSearchPlan.TryFindLastCandidate(input, input.Length, out var candidate));
        Assert.Equal(expected, candidate.StartIndex);
    }

    [Fact]
    public void SearchExecutorUsesStructuralPlanForFilteredSingleLiteralSearch()
    {
        var regex = new Utf8Regex(@"\bfoo\b", RegexOptions.CultureInvariant);
        var input = Encoding.UTF8.GetBytes("xfoo foo fooz");

        Assert.Equal(5, Utf8SearchExecutor.FindFirst(regex.SearchPlan, input));
        Assert.Equal(5, Utf8SearchExecutor.FindNext(regex.SearchPlan, input, 0));
        Assert.Equal(5, Utf8SearchExecutor.FindLast(regex.SearchPlan, input));
    }

    [Fact]
    public void StructuralStartPlanIncludesBoundaryStagesForBoundaryWrappedFallbackAnchor()
    {
        var plan = new Utf8SearchPlan(
            Utf8SearchKind.ExactAsciiLiteral,
            Encoding.UTF8.GetBytes("foo"),
            canGuideFallbackStarts: true,
            leadingBoundary: Utf8BoundaryRequirement.Boundary,
            trailingBoundary: Utf8BoundaryRequirement.Boundary);

        Assert.True(plan.StructuralSearchPlan.HasValue);
        Assert.Equal(Utf8StructuralSearchYieldKind.Start, plan.StructuralSearchPlan.YieldKind);
        Assert.Contains(
            plan.StructuralSearchPlan.Stages!,
            static stage => stage.Kind == Utf8StructuralSearchStageKind.RequireLeadingBoundary &&
                            stage.BoundaryRequirement == Utf8BoundaryRequirement.Boundary);
        Assert.Contains(
            plan.StructuralSearchPlan.Stages!,
            static stage => stage.Kind == Utf8StructuralSearchStageKind.RequireTrailingBoundary &&
                            stage.BoundaryRequirement == Utf8BoundaryRequirement.Boundary);
    }

    [Fact]
    public void ConstructorCanCreateRequiredLiteralPrefilterForComplexFallbackPattern()
    {
        const string pattern = "(?:foo((?:ASIA|AKIA|AROA|AIDA)[A-Z0-7]{16}).*?[a-zA-Z0-9+/]{40}|[a-zA-Z0-9+/]{40}.*?bar((?:ASIA|AKIA|AROA|AIDA)[A-Z0-7]{16}))";
        var analysis = Utf8FrontEnd.Analyze(pattern, RegexOptions.CultureInvariant);
        var family = RegexRequiredLiteralAnalyzer.FindBestRequiredLiteralFamily(analysis.SemanticRegex.RuntimeTree!.Root);
        Assert.True(family is { Length: > 0 }, DumpNode(analysis.SemanticRegex.RuntimeTree.Root));

        var regex = new Utf8Regex(pattern, RegexOptions.CultureInvariant);

        Assert.Equal(Utf8SearchKind.ExactAsciiLiteral, regex.SearchPlan.Kind);
        Assert.Equal(PreparedSearcherKind.MultiLiteral, regex.SearchPlan.RequiredPrefilterSearcher.Kind);
        Assert.True(regex.SearchPlan.PrefilterPlan.HasValue);
        Assert.True(regex.SearchPlan.FallbackSearch.HasRequiredPrefilter);
        Assert.Equal(["AIDA", "AKIA", "AROA", "ASIA"], regex.SearchPlan.RequiredPrefilterAlternateLiteralsUtf8!.Select(Encoding.UTF8.GetString));
    }

    [Fact]
    public void FallbackSearchPlanCarriesExplicitCandidateStageForFallbackPattern()
    {
        var regex = new Utf8Regex("a.*b", RegexOptions.CultureInvariant);

        Assert.True(regex.SearchPlan.FallbackSearch.HasCandidates);
        Assert.NotNull(regex.SearchPlan.FallbackSearch.CandidatePlans);
        Assert.Same(regex.SearchPlan.FallbackSearch.CandidatePlans, regex.SearchPlan.FallbackCandidatePlans);
        Assert.Equal(Utf8SearchEngineKind.StructuralSearchSet, regex.SearchPlan.FallbackCandidateEngine.Kind);
        Assert.True(regex.SearchPlan.FallbackCandidateEngine.Semantics.RequiresConfirmation);
    }

    [Fact]
    public void NativeSearchPlanCarriesExplicitCandidateEngineForExactLiteral()
    {
        var regex = new Utf8Regex("abc", RegexOptions.CultureInvariant);

        Assert.Equal(Utf8SearchEngineKind.PreparedSearcher, regex.SearchPlan.NativeCandidateEngine.Kind);
        Assert.Equal(Utf8SearchOverlapPolicy.Overlapping, regex.SearchPlan.NativeCandidateEngine.Semantics.OverlapPolicy);
        Assert.Equal(regex.SearchPlan.PreparedSearcher.Kind, regex.SearchPlan.NativeCandidateEngine.PreparedSearcher.Kind);
        Assert.Equal(Utf8SearchMetaStrategyKind.DirectSearch, regex.SearchPlan.CountStrategy.Kind);
    }

    [Fact]
    public void SearchPlanCarriesHybridStrategyForLargeAutomatonLiteralFamilies()
    {
        var regex = new Utf8Regex("Sherlock Holmes|John Watson|Irene Adler|Inspector Lestrade|Professor Moriarty", RegexOptions.CultureInvariant);

        Assert.Equal(Utf8SearchMetaStrategyKind.HybridSearch, regex.SearchPlan.CountStrategy.Kind);
        Assert.Equal(Utf8SearchMetaStrategyKind.HybridSearch, regex.SearchPlan.FirstMatchStrategy.Kind);
        Assert.Equal(Utf8SearchEngineKind.PreparedSearcher, regex.SearchPlan.CountStrategy.CandidateEngine.Kind);
        Assert.Equal(Utf8SearchObservabilityKind.Effectiveness, regex.SearchPlan.CountStrategy.ObservabilityKind);
    }

    [Fact]
    public void SearchPlanCarriesConfirmationAndProjectionPlans()
    {
        var boundaryRegex = new Utf8Regex(@"\b(?:Task|ValueTask|IAsyncEnumerable)\b", RegexOptions.CultureInvariant);
        var fallbackRegex = new Utf8Regex("a.*b", RegexOptions.CultureInvariant);

        Assert.Equal(Utf8ConfirmationKind.BoundaryRequirements, boundaryRegex.SearchPlan.ConfirmationPlan.Kind);
        Assert.Equal(Utf8ProjectionKind.Utf16BoundaryMap, boundaryRegex.SearchPlan.ProjectionPlan.Kind);
        Assert.Equal(Utf8ConfirmationKind.None, fallbackRegex.SearchPlan.ConfirmationPlan.Kind);
    }

    [Fact]
    public void SearchPlanCarriesExecutablePipelines()
    {
        var regex = new Utf8Regex(@"\b(?:Task|ValueTask|IAsyncEnumerable)\b", RegexOptions.CultureInvariant);
        var fallbackRegex = new Utf8Regex("a.*b", RegexOptions.CultureInvariant);

        Assert.Equal(Utf8SearchMetaStrategyKind.PrefilterThenConfirm, regex.SearchPlan.EnumerationPipeline.Strategy.Kind);
        Assert.Equal(Utf8ConfirmationKind.BoundaryRequirements, regex.SearchPlan.EnumerationPipeline.Confirmation.Kind);
        Assert.Equal(Utf8ProjectionKind.Utf16BoundaryMap, regex.SearchPlan.EnumerationPipeline.Projection.Kind);

        Assert.Equal(Utf8SearchMetaStrategyKind.PrefilterThenSearch, fallbackRegex.SearchPlan.FirstMatchPipeline.Strategy.Kind);
        Assert.Equal(Utf8SearchMetaStrategyKind.PrefilterThenSearch, fallbackRegex.SearchPlan.CountPipeline.Strategy.Kind);
        Assert.Equal(Utf8ConfirmationKind.None, fallbackRegex.SearchPlan.FirstMatchPipeline.Confirmation.Kind);
        Assert.Equal(Utf8ProjectionKind.Utf16BoundaryMap, fallbackRegex.SearchPlan.EnumerationPipeline.Projection.Kind);
    }

    [Fact]
    public void SearchPlanCarriesBackendInstructionPrograms()
    {
        var regex = new Utf8Regex(@"\b(?:Task|ValueTask|IAsyncEnumerable)\b", RegexOptions.CultureInvariant);
        var fallbackRegex = new Utf8Regex("a.*b", RegexOptions.CultureInvariant);

        Assert.Equal(3, regex.SearchPlan.EnumerationProgram.InstructionCount);
        Assert.Equal(Utf8BackendInstructionKind.Search, regex.SearchPlan.EnumerationProgram.First.Kind);
        Assert.Equal(Utf8BackendInstructionKind.Confirm, regex.SearchPlan.EnumerationProgram.Second.Kind);
        Assert.Equal(Utf8BackendInstructionKind.Project, regex.SearchPlan.EnumerationProgram.Third.Kind);

        Assert.Equal(1, fallbackRegex.SearchPlan.FirstMatchProgram.InstructionCount);
        Assert.Equal(Utf8BackendInstructionKind.Search, fallbackRegex.SearchPlan.FirstMatchProgram.First.Kind);
    }

    [Fact]
    public void CompiledEngineSelectionUsesExecutablePipelines()
    {
        var literalFamily = new Utf8Regex(@"\b(?:Task|ValueTask|IAsyncEnumerable)\b", RegexOptions.CultureInvariant);
        var fallback = new Utf8Regex("a.*b", RegexOptions.CultureInvariant);
        var structural = new Utf8Regex("ab[0-9][0-9]cd", RegexOptions.CultureInvariant);

        Assert.Equal(Utf8CompiledEngineKind.SearchGuidedFallback, literalFamily.CompiledEngineKind);
        Assert.Equal(Utf8CompiledEngineKind.SearchGuidedFallback, fallback.CompiledEngineKind);
        Assert.Equal(Utf8CompiledEngineKind.StructuralLinearAutomaton, structural.CompiledEngineKind);
        Assert.Equal(Utf8CompiledExecutionBackend.InterpretedInstruction, fallback.CompiledExecutionBackend);
        Assert.Equal(Utf8CompiledExecutionBackend.EmittedInstruction, structural.CompiledExecutionBackend);
    }

    [Fact]
    public void CompiledSearchAnalysisClassifiesPlanDrivenFamilies()
    {
        var literalFamily = new Utf8Regex(@"\b(?:Task|ValueTask|IAsyncEnumerable)\b", RegexOptions.CultureInvariant);
        var fallback = new Utf8Regex("a.*b", RegexOptions.CultureInvariant);
        var identifierFamily = new Utf8Regex(@"\b(?:record|struct|class)\s+[A-Z][A-Za-z0-9_]+", RegexOptions.CultureInvariant);
        var orderedWindow = new Utf8Regex(@"\bpublic\s+async\b", RegexOptions.CultureInvariant);

        var literalAnalysis = Utf8CompiledSearchAnalyzer.Analyze(literalFamily.RegexPlan, preferCompiled: true);
        var fallbackAnalysis = Utf8CompiledSearchAnalyzer.Analyze(fallback.RegexPlan, preferCompiled: true);
        var identifierAnalysis = Utf8CompiledSearchAnalyzer.Analyze(identifierFamily.RegexPlan, preferCompiled: true);
        var orderedWindowAnalysis = Utf8CompiledSearchAnalyzer.Analyze(orderedWindow.RegexPlan, preferCompiled: true);

        Assert.Equal(Utf8CompiledSearchMode.SearchGuidedFallback, literalAnalysis.Mode);
        Assert.Equal(Utf8CompiledSearchMode.CompiledFallback, fallbackAnalysis.Mode);
        Assert.Equal(Utf8CompiledSearchMode.StructuralIdentifierFamily, identifierAnalysis.Mode);
        Assert.Equal(Utf8CompiledEmittedFamily.UpperWordIdentifier, identifierAnalysis.EmittedFamily);
        Assert.Equal(Utf8CompiledSearchMode.OrderedLiteralWindow, orderedWindowAnalysis.Mode);
        Assert.Equal(Utf8CompiledEmittedFamily.OrderedLiteralWindow, orderedWindowAnalysis.EmittedFamily);
    }

    [Fact]
    public void EmittedKernelLowererProducesExplicitBlocksForIdentifierFamily()
    {
        var regex = new Utf8Regex(@"\b(?:record|struct|class)\s+[A-Z][A-Za-z0-9_]+", RegexOptions.CultureInvariant);

        Assert.True(Utf8EmittedKernelLowerer.TryLower(regex.RegexPlan, out var kernelPlan));
        Assert.Equal(Utf8EmittedKernelKind.UpperWordIdentifierFamily, kernelPlan.Kind);
        Assert.Equal(
            [
                Utf8EmittedKernelBlockKind.FindAnchorSet,
                Utf8EmittedKernelBlockKind.DispatchPrefixesAtAnchor,
                Utf8EmittedKernelBlockKind.ConsumeAsciiWhitespace,
                Utf8EmittedKernelBlockKind.RequireAsciiUpper,
                Utf8EmittedKernelBlockKind.ConsumeAsciiWordTail,
                Utf8EmittedKernelBlockKind.AcceptAndAdvance,
            ],
            kernelPlan.Blocks.Select(static block => block.Kind));
    }

    [Fact]
    public void EmittedKernelLowererProducesExplicitBlocksForSharedPrefixSuffixFamily()
    {
        var regex = new Utf8Regex(@"\b(?:LogError|LogWarning|LogInformation)\s*\(", RegexOptions.CultureInvariant);

        Assert.True(Utf8EmittedKernelLowerer.TryLower(regex.RegexPlan, out var kernelPlan));
        Assert.Equal(Utf8EmittedKernelKind.SharedPrefixAsciiWhitespaceSuffix, kernelPlan.Kind);
        Assert.Equal(
            [
                Utf8EmittedKernelBlockKind.FindCommonPrefix,
                Utf8EmittedKernelBlockKind.MatchSharedPrefixSuffix,
                Utf8EmittedKernelBlockKind.AcceptAndAdvance,
            ],
            kernelPlan.Blocks.Select(static block => block.Kind));
    }

    [Fact]
    public void EmittedKernelLowererProducesExplicitBlocksForOrderedLiteralWindow()
    {
        var regex = new Utf8Regex(@"\bpublic\s+async\b", RegexOptions.CultureInvariant);

        Assert.True(Utf8EmittedKernelLowerer.TryLower(regex.RegexPlan, out var kernelPlan));
        Assert.Equal(Utf8EmittedKernelKind.OrderedAsciiWhitespaceLiteralWindow, kernelPlan.Kind);
        Assert.Equal(
            [
                Utf8EmittedKernelBlockKind.FindTrailingLiteral,
                Utf8EmittedKernelBlockKind.ConsumeReverseAsciiWhitespace,
                Utf8EmittedKernelBlockKind.MatchLeadingLiteralBeforeSeparator,
                Utf8EmittedKernelBlockKind.AcceptAndAdvance,
            ],
            kernelPlan.Blocks.Select(static block => block.Kind));
    }

    [Fact]
    public void EmittedKernelLowererProducesExplicitBlocksForBoundedOrderedLiteralWindow()
    {
        var regex = new Utf8Regex(@"\bawait\b\s+.{0,60}\bConfigureAwait\b", RegexOptions.CultureInvariant);

        Assert.True(Utf8EmittedKernelLowerer.TryLower(regex.RegexPlan, out var kernelPlan));
        Assert.Equal(Utf8EmittedKernelKind.OrderedAsciiWhitespaceLiteralWindow, kernelPlan.Kind);
        Assert.Equal(
            [
                Utf8EmittedKernelBlockKind.FindTrailingLiteral,
                Utf8EmittedKernelBlockKind.ConsumeReverseAsciiWhitespace,
                Utf8EmittedKernelBlockKind.MatchLeadingLiteralBeforeSeparator,
                Utf8EmittedKernelBlockKind.AcceptAndAdvance,
            ],
            kernelPlan.Blocks.Select(static block => block.Kind));
    }

    [Fact]
    public void RegexPlanCarriesExplicitPrimaryExecutionEngineKinds()
    {
        var literalFamily = new Utf8Regex(@"\b(?:Task|ValueTask|IAsyncEnumerable)\b", RegexOptions.CultureInvariant);
        var fallback = Utf8FrontEnd.Analyze("a.*b", RegexOptions.CultureInvariant);
        var structural = Utf8FrontEnd.Analyze("ab[0-9][0-9]cd", RegexOptions.CultureInvariant);
        var orderedWindow = Utf8FrontEnd.Analyze(@"\b(?:using\s+var|await\s+using\s+var)\s+[A-Za-z_][A-Za-z0-9_]*\s*=\s*await\b", RegexOptions.CultureInvariant);

        Assert.Equal(Utf8SearchEngineKind.PreparedSearcher, literalFamily.SearchPlan.NativeCandidateEngine.Kind);
        Assert.Equal(Utf8SearchEngineKind.StructuralSearchSet, fallback.RegexPlan.PrimaryExecutionEngine.Kind);
        Assert.Equal(Utf8SearchEngineKind.StructuralDeterministicAutomaton, structural.RegexPlan.PrimaryExecutionEngine.Kind);
        Assert.Equal(Utf8SearchEngineKind.StructuralDeterministicAutomaton, orderedWindow.RegexPlan.PrimaryExecutionEngine.Kind);
    }

    [Fact]
    public void EmittedLiteralFamilyCounterMatchesInstructionExecutor()
    {
        var regex = new Utf8Regex(@"\b(?:Task|ValueTask|IAsyncEnumerable)\b", RegexOptions.CultureInvariant);
        var input = Encoding.UTF8.GetBytes("Task task ValueTask IAsyncEnumerableX IAsyncEnumerable");

        Assert.True(Utf8EmittedLiteralFamilyCounter.TryCreate(regex.SearchPlan, regex.SearchPlan.CountProgram, regex.SearchPlan.FirstMatchProgram, out var counter));
        Assert.NotNull(counter);
        Assert.Equal(
            Utf8BackendInstructionExecutor.CountLiteralFamily(regex.SearchPlan, regex.SearchPlan.CountProgram, input, budget: null),
            counter!.Count(input));
    }

    [Fact]
    public void EmittedLiteralFamilyCounterMatchesInstructionExecutorForIsMatch()
    {
        var regex = new Utf8Regex(@"\b(?:Task|ValueTask|IAsyncEnumerable)\b", RegexOptions.CultureInvariant);
        var hit = Encoding.UTF8.GetBytes("Task task ValueTask IAsyncEnumerableX IAsyncEnumerable");
        var miss = Encoding.UTF8.GetBytes("task IAsyncEnumerableX stream");

        Assert.True(Utf8EmittedLiteralFamilyCounter.TryCreate(regex.SearchPlan, regex.SearchPlan.CountProgram, regex.SearchPlan.FirstMatchProgram, out var counter));
        Assert.NotNull(counter);
        Assert.Equal(
            Utf8BackendInstructionExecutor.IsMatchLiteralFamily(regex.SearchPlan, regex.SearchPlan.FirstMatchProgram, hit, budget: null, rightToLeft: false),
            counter!.IsMatch(hit));
        Assert.Equal(
            Utf8BackendInstructionExecutor.IsMatchLiteralFamily(regex.SearchPlan, regex.SearchPlan.FirstMatchProgram, miss, budget: null, rightToLeft: false),
            counter.IsMatch(miss));
    }

    [Fact]
    public void EmittedLiteralFamilyCounterMatchesInstructionExecutorForFirstMatch()
    {
        var regex = new Utf8Regex(@"\b(?:Task|ValueTask|IAsyncEnumerable)\b", RegexOptions.CultureInvariant);
        var hit = Encoding.UTF8.GetBytes("Task task ValueTask IAsyncEnumerableX IAsyncEnumerable");
        var miss = Encoding.UTF8.GetBytes("task IAsyncEnumerableX stream");

        Assert.True(Utf8EmittedLiteralFamilyCounter.TryCreate(regex.SearchPlan, regex.SearchPlan.CountProgram, regex.SearchPlan.FirstMatchProgram, out var counter));
        Assert.NotNull(counter);

        Assert.True(counter!.TryMatch(hit, out var emittedIndex, out var emittedLength));
        var interpreted = Utf8BackendInstructionExecutor.MatchLiteralFamily(regex.SearchPlan, regex.SearchPlan.FirstMatchProgram, hit, regex.SearchPlan.AlternateLiteralUtf16Lengths, budget: null, rightToLeft: false);
        Assert.True(interpreted.Success);
        Assert.Equal(interpreted.IndexInBytes, emittedIndex);
        Assert.Equal(interpreted.LengthInBytes, emittedLength);

        Assert.False(counter.TryMatch(miss, out emittedIndex, out emittedLength));
        Assert.Equal(-1, emittedIndex);
        Assert.Equal(0, emittedLength);
    }

    [Fact]
    public void EmittedSearchGuidedFallbackMatchesInterpreterForIsMatchAndCount()
    {
        var regex = new Utf8Regex("a.*b", RegexOptions.CultureInvariant);
        var analysis = Utf8FrontEnd.Analyze("a.*b", RegexOptions.CultureInvariant);
        var verifierRuntime = Utf8VerifierRuntime.Create(analysis.RegexPlan, "a.*b", RegexOptions.CultureInvariant, Utf8Regex.DefaultMatchTimeout);
        var hit = Encoding.UTF8.GetBytes("zzza123bzzz");
        var miss = Encoding.UTF8.GetBytes("zzza123czzz");

        Assert.False(Utf8EmittedSearchGuidedFallback.TryCreate(regex.RegexPlan, verifierRuntime, out var backend));
        Assert.Null(backend);
    }

    [Fact]
    public void EmittedSearchGuidedFallbackMatchesInterpreterForBoundaryLiteralFamilies()
    {
        const string pattern = @"\b(?:Task|ValueTask|IAsyncEnumerable)\b";
        var regex = new Utf8Regex(pattern, RegexOptions.CultureInvariant);
        var analysis = Utf8FrontEnd.Analyze(pattern, RegexOptions.CultureInvariant);
        var verifierRuntime = Utf8VerifierRuntime.Create(analysis.RegexPlan, pattern, RegexOptions.CultureInvariant, Utf8Regex.DefaultMatchTimeout);
        var input = Encoding.UTF8.GetBytes("Task task ValueTask IAsyncEnumerableX IAsyncEnumerable");

        Assert.True(Utf8EmittedSearchGuidedFallback.TryCreate(regex.RegexPlan, verifierRuntime, out var backend));
        Assert.NotNull(backend);

        Assert.Equal(
            Utf8SearchStrategyExecutor.CountLiteralFamily(regex.SearchPlan, input, budget: null),
            backend!.Count(input));
        Assert.Equal(
            Utf8SearchStrategyExecutor.IsMatchLiteralFamily(regex.SearchPlan, input, budget: null, rightToLeft: false),
            backend.IsMatch(input));
    }

    [Fact]
    public void SearchStrategyExecutorCanCountAndMatchLiteralFamilies()
    {
        var regex = new Utf8Regex(@"\b(?:Task|ValueTask|IAsyncEnumerable)\b", RegexOptions.CultureInvariant);
        var input = Encoding.UTF8.GetBytes("Task task ValueTask IAsyncEnumerableX IAsyncEnumerable");

        Assert.Equal(3, Utf8SearchStrategyExecutor.CountLiteralFamily(regex.SearchPlan, input, budget: null));
        Assert.True(Utf8SearchStrategyExecutor.IsMatchLiteralFamily(regex.SearchPlan, input, budget: null, rightToLeft: false));
    }

    [Fact]
    public void ConstructorClassifiesAwsKeysFullPatternAsNativeQuotedRelation()
    {
        const string pattern = "(('|\")((?:ASIA|AKIA|AROA|AIDA)([A-Z0-7]{16}))('|\").*?(\\n^.*?){0,4}(('|\")[a-zA-Z0-9+/]{40}('|\"))+|('|\")[a-zA-Z0-9+/]{40}('|\").*?(\\n^.*?){0,3}('|\")((?:ASIA|AKIA|AROA|AIDA)([A-Z0-7]{16}))('|\"))+";
        var parserException = Record.Exception(() => RegexParser.Parse(pattern, RegexOptions.Multiline, RegexParser.GetTargetCulture(RegexOptions.Multiline)));
        if (parserException is Lokad.Utf8Regex.Internal.FrontEnd.Runtime.RegexParseException parseException)
        {
            throw new Xunit.Sdk.XunitException($"Parser failed: {parseException.Error} at {parseException.Offset}");
        }

        Assert.Null(parserException);
        var analysis = Utf8FrontEnd.Analyze(pattern, RegexOptions.Multiline);
        Assert.NotNull(analysis.SemanticRegex.RuntimeTree);
        var family = RegexRequiredLiteralAnalyzer.FindBestRequiredLiteralFamily(analysis.SemanticRegex.RuntimeTree!.Root);
        Assert.True(family is { Length: > 0 }, DumpNode(analysis.SemanticRegex.RuntimeTree.Root));

        var regex = new Utf8Regex(pattern, RegexOptions.Multiline);

        Assert.Equal(NativeExecutionKind.AsciiStructuralQuotedRelation, regex.ExecutionKind);
        Assert.Equal(Utf8CompiledEngineKind.StructuralLinearAutomaton, regex.CompiledEngineKind);
        Assert.Equal(Utf8StructuralLinearProgramKind.AsciiQuotedRelation, regex.StructuralLinearProgramKind);
        Assert.Equal(PreparedSearcherKind.None, regex.SearchPlan.RequiredPrefilterSearcher.Kind);
        Assert.Equal(PreparedSearcherKind.None, regex.SearchPlan.SecondaryRequiredPrefilterSearcher.Kind);
        Assert.Null(regex.SearchPlan.RequiredWindowPrefilterPlans);
        Assert.False(regex.SearchPlan.HasFallbackCandidates);
        Assert.Null(regex.SearchPlan.FallbackCandidatePlans);
    }

    [Fact]
    public void AwsKeyNativeQuotedRelationCanCountRepeatedForwardWindows()
    {
        const string pattern = "(('|\")((?:ASIA|AKIA|AROA|AIDA)([A-Z0-7]{16}))('|\").*?(\\n^.*?){0,4}(('|\")[a-zA-Z0-9+/]{40}('|\"))+|('|\")[a-zA-Z0-9+/]{40}('|\").*?(\\n^.*?){0,3}('|\")((?:ASIA|AKIA|AROA|AIDA)([A-Z0-7]{16}))('|\"))+";
        const string input = "\"AIDAABCDEFGHIJKLMNOP\"\nctx = 1\nctx = 2\n\"aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa\"\n\n\"AIDAABCDEFGHIJKLMNOP\"\nctx = 3\nctx = 4\n\"aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa\"\n";
        var regex = new Utf8Regex(pattern, RegexOptions.Multiline);
        var bytes = Encoding.UTF8.GetBytes(input);

        Assert.Equal(NativeExecutionKind.AsciiStructuralQuotedRelation, regex.ExecutionKind);
        Assert.Equal(2, regex.Count(bytes));

        var match = regex.Match(bytes);
        Assert.True(match.Success);
        Assert.Equal(0, match.IndexInBytes);
    }

    [Fact]
    public void ConstructorCanCreateOrderedAsciiWindowSearchPlanForStructuralFallbackPattern()
    {
        var regex = new Utf8Regex(@"\b(?:using\s+var|await\s+using\s+var)\s+[A-Za-z_][A-Za-z0-9_]*\s*=\s*await\b", RegexOptions.CultureInvariant);

        Assert.Equal(NativeExecutionKind.AsciiStructuralIdentifierFamily, regex.ExecutionKind);
        Assert.Equal(Utf8SearchKind.ExactAsciiLiterals, regex.SearchPlan.Kind);
        Assert.True(regex.StructuralSearchPlan.HasValue);
        Assert.Equal(Utf8StructuralSearchYieldKind.Start, regex.StructuralSearchPlan.YieldKind);
        Assert.Equal(
            ["await using var", "using var"],
            regex.SearchPlan.AlternateLiteralsUtf8!.Select(Encoding.UTF8.GetString).OrderBy(static value => value, StringComparer.Ordinal));
    }

    [Fact]
    public void RuffNoqaRealPatternUsesByteSafeInterpreterLaneWithStructuralStarts()
    {
        const string pattern = "(\\s*)((?:# [Nn][Oo][Qq][Aa])(?::\\s?(([A-Z]+[0-9]+(?:[,\\s]+)?)+))?)";
        var analysis = Utf8FrontEnd.Analyze(pattern, RegexOptions.None);

        Assert.Equal(NativeExecutionKind.FallbackRegex, analysis.RegexPlan.ExecutionKind);
        Assert.Equal(Utf8SearchKind.FixedDistanceAsciiSets, analysis.RegexPlan.SearchPlan.Kind);
        Assert.False(analysis.RegexPlan.SearchPlan.HasWindowSearch);
        Assert.True(Utf8ByteSafeInterpreterExecutor.CanExecute(analysis.RegexPlan));
        Assert.Equal(Utf8CompiledEngineKind.ByteSafeLinear, analysis.RegexPlan.CompiledEngine.Kind);
        Assert.True(analysis.RegexPlan.SearchPlan.HasFallbackCandidates);
        Assert.True(analysis.RegexPlan.StructuralSearchPlan.HasValue);
        Assert.Equal(Utf8StructuralSearchYieldKind.Start, analysis.RegexPlan.StructuralSearchPlan.YieldKind);
        Assert.Equal(Utf8StructuralSearchStageKind.FindAscii, analysis.RegexPlan.StructuralSearchPlan.Stages![0].Kind);
        Assert.Contains(analysis.RegexPlan.StructuralSearchPlan.Stages!, static stage => stage.Kind == Utf8StructuralSearchStageKind.TransformCandidateStart);
        Assert.True(analysis.RegexPlan.StructuralSearchPlan.Stages!.Count(static stage => stage.Kind == Utf8StructuralSearchStageKind.RequireByteAtOffset) >= 6);
        Assert.Contains(analysis.RegexPlan.StructuralSearchPlan.Stages!, static stage => stage.Kind == Utf8StructuralSearchStageKind.RequireMinLength);
        Assert.Equal(Utf8StructuralSearchStageKind.YieldStart, analysis.RegexPlan.StructuralSearchPlan.Stages![^1].Kind);
        Assert.Equal(Utf8FallbackStartTransformKind.TrimLeadingAsciiWhitespace, analysis.RegexPlan.SearchPlan.FallbackStartTransform.Kind);
        Assert.NotNull(analysis.RegexPlan.SearchPlan.FixedDistanceSets);
        Assert.True(analysis.RegexPlan.SearchPlan.FixedDistanceSets!.Length >= 6);
        Assert.True(analysis.RegexPlan.SearchPlan.FixedDistanceSets[0].Distance >= 2);
        Assert.NotNull(analysis.RegexPlan.SearchPlan.FixedDistanceSets[0].Chars);
        Assert.Contains(analysis.RegexPlan.SearchPlan.FixedDistanceSets[0].Chars!, static value => value is (byte)'A' or (byte)'N' or (byte)'O' or (byte)'Q' or (byte)'a' or (byte)'n' or (byte)'o' or (byte)'q');
        Assert.True(analysis.RegexPlan.DeterministicGuards.HasValue);
        Assert.NotNull(analysis.RegexPlan.DeterministicGuards.PrefixGuards);
        Assert.NotEmpty(analysis.RegexPlan.DeterministicGuards.PrefixGuards!);
        Assert.True(analysis.RegexPlan.DeterministicGuards.PrefixGuards!.Length >= 6);
    }

    [Fact]
    public void RuffNoqaTweakedPatternUsesByteSafeInterpreterLane()
    {
        const string pattern = "(?:# [Nn][Oo][Qq][Aa])(?::\\s?(([A-Z]+[0-9]+(?:[,\\s]+)?)+))?";
        var analysis = Utf8FrontEnd.Analyze(pattern, RegexOptions.None);

        Assert.Equal(NativeExecutionKind.FallbackRegex, analysis.RegexPlan.ExecutionKind);
        Assert.True(Utf8ByteSafeInterpreterExecutor.CanExecute(analysis.RegexPlan));
        Assert.Equal(Utf8CompiledEngineKind.ByteSafeLinear, analysis.RegexPlan.CompiledEngine.Kind);
    }

    [Fact]
    public void VariableGapFixedDistanceLiteralCanUseAsciiStructuralTokenWindow()
    {
        const string pattern = "[A-Za-z]{10}\\s+[\\s\\S]{0,100}Result[\\s\\S]{0,100}\\s+[A-Za-z]{10}";
        var analysis = Utf8FrontEnd.Analyze(pattern, RegexOptions.None);

        Assert.Equal(NativeExecutionKind.AsciiStructuralTokenWindow, analysis.RegexPlan.ExecutionKind);
        Assert.Equal(Utf8SearchKind.ExactAsciiLiteral, analysis.RegexPlan.SearchPlan.Kind);
        Assert.Equal(Utf8CompiledEngineKind.StructuralLinearAutomaton, analysis.RegexPlan.CompiledEngine.Kind);
        Assert.Equal(Utf8StructuralLinearProgramKind.AsciiTokenWindow, analysis.RegexPlan.StructuralLinearProgram.Kind);
    }

    [Fact]
    public void StructuralSearchPlanCanProduceFallbackStartCandidates()
    {
        var regex = new Utf8Regex("a.*b", RegexOptions.CultureInvariant);
        var input = Encoding.UTF8.GetBytes("zzza123bzzz");
        var state = new Utf8StructuralSearchState(new PreparedSearchScanState(0, default), new PreparedWindowScanState(0, new PreparedSearchScanState(0, default)));

        Assert.True(regex.StructuralSearchPlan.TryFindNextCandidate(input, ref state, out var candidate));
        Assert.Equal(3, candidate.StartIndex);
        Assert.Equal(-1, candidate.EndIndex);
        Assert.Equal(1, candidate.MatchLength);
        Assert.Equal(0, candidate.LiteralId);
    }

    [Fact]
    public void StructuralSearchPlanCanApplyFallbackStartTransform()
    {
        const string pattern = "(\\s*)((?:# [Nn][Oo][Qq][Aa])(?::\\s?(([A-Z]+[0-9]+(?:[,\\s]+)?)+))?)";
        var analysis = Utf8FrontEnd.Analyze(pattern, RegexOptions.None);
        var input = Encoding.UTF8.GetBytes("   # NOQA: ABC123");
        var state = new Utf8StructuralSearchState(new PreparedSearchScanState(0, default), new PreparedWindowScanState(0, new PreparedSearchScanState(0, default)));

        Assert.True(analysis.RegexPlan.StructuralSearchPlan.TryFindNextCandidate(input, ref state, out var candidate));
        Assert.Equal(0, candidate.StartIndex);
        Assert.Equal(-1, candidate.EndIndex);
    }

    [Fact]
    public void StructuralSearchPlanCanRejectNonBoundaryFallbackAnchorCandidates()
    {
        var plan = new Utf8SearchPlan(
            Utf8SearchKind.ExactAsciiLiteral,
            Encoding.UTF8.GetBytes("foo"),
            canGuideFallbackStarts: true,
            leadingBoundary: Utf8BoundaryRequirement.Boundary,
            trailingBoundary: Utf8BoundaryRequirement.Boundary);
        var input = Encoding.UTF8.GetBytes("xfood bar foo baz bar");
        var state = new Utf8StructuralSearchState(new PreparedSearchScanState(0, default), new PreparedWindowScanState(0, new PreparedSearchScanState(0, default)));

        Assert.True(plan.StructuralSearchPlan.TryFindNextCandidate(input, ref state, out var candidate));
        Assert.Equal("xfood bar ".Length, candidate.StartIndex);
        Assert.False(plan.StructuralSearchPlan.TryFindNextCandidate(input, ref state, out _));
    }

    [Fact]
    public void StructuralStartPlanIncludesTrailingLiteralStage()
    {
        var plan = new Utf8SearchPlan(
            Utf8SearchKind.ExactAsciiLiteral,
            Encoding.UTF8.GetBytes("foo"),
            canGuideFallbackStarts: true,
            trailingLiteralUtf8: Encoding.UTF8.GetBytes("bar"));

        Assert.Contains(
            plan.StructuralSearchPlan.Stages!,
            static stage => stage.Kind == Utf8StructuralSearchStageKind.RequireTrailingLiteral &&
                            stage.LiteralUtf8 is { Length: 3 });
    }

    [Fact]
    public void StructuralSearchPlanCanRejectCandidatesWithoutTrailingLiteral()
    {
        var plan = new Utf8SearchPlan(
            Utf8SearchKind.ExactAsciiLiteral,
            Encoding.UTF8.GetBytes("foo"),
            canGuideFallbackStarts: true,
            trailingLiteralUtf8: Encoding.UTF8.GetBytes("bar"));
        var input = Encoding.UTF8.GetBytes("fooqux foobar");
        var state = new Utf8StructuralSearchState(new PreparedSearchScanState(0, default), new PreparedWindowScanState(0, new PreparedSearchScanState(0, default)));

        Assert.True(plan.StructuralSearchPlan.TryFindNextCandidate(input, ref state, out var candidate));
        Assert.Equal("fooqux ".Length, candidate.StartIndex);
        Assert.False(plan.StructuralSearchPlan.TryFindNextCandidate(input, ref state, out _));
    }

    [Fact]
    public void StructuralStartPlanIncludesExactLengthStage()
    {
        var plan = new Utf8SearchPlan(
            Utf8SearchKind.ExactAsciiLiteral,
            Encoding.UTF8.GetBytes("foo"),
            canGuideFallbackStarts: true,
            exactRequiredLength: 5);

        Assert.True(plan.StructuralSearchPlan.HasValue);
        Assert.True(plan.StructuralSearchPlan.ProducesBoundedCandidates);
        Assert.Contains(
            plan.StructuralSearchPlan.Stages!,
            static stage => stage.Kind == Utf8StructuralSearchStageKind.RequireExactLength &&
                            stage.MinLength == 5);
    }

    [Fact]
    public void StructuralSearchPlanCanProduceBoundedStartCandidatesFromExactLength()
    {
        var plan = new Utf8SearchPlan(
            Utf8SearchKind.ExactAsciiLiteral,
            Encoding.UTF8.GetBytes("foo"),
            canGuideFallbackStarts: true,
            exactRequiredLength: 5);
        var input = Encoding.UTF8.GetBytes("xxfoozzyy");
        var state = new Utf8StructuralSearchState(new PreparedSearchScanState(0, default), new PreparedWindowScanState(0, new PreparedSearchScanState(0, default)));

        Assert.True(plan.StructuralSearchPlan.TryFindNextCandidate(input, ref state, out var candidate));
        Assert.Equal(2, candidate.StartIndex);
        Assert.Equal(7, candidate.EndIndex);
    }

    [Fact]
    public void StructuralStartPlanIncludesMaxLengthBoundStage()
    {
        var plan = new Utf8SearchPlan(
            Utf8SearchKind.ExactAsciiLiteral,
            Encoding.UTF8.GetBytes("foo"),
            canGuideFallbackStarts: true,
            maxPossibleLength: 6);

        Assert.True(plan.StructuralSearchPlan.HasValue);
        Assert.True(plan.StructuralSearchPlan.ProducesBoundedCandidates);
        Assert.False(plan.StructuralSearchPlan.RequiresCandidateEndCoverage);
        Assert.Contains(
            plan.StructuralSearchPlan.Stages!,
            static stage => stage.Kind == Utf8StructuralSearchStageKind.BoundMaxLength &&
                            stage.MaxSpan == 6);
    }

    [Fact]
    public void StructuralSearchPlanCanBoundStartCandidatesByMaxLength()
    {
        var plan = new Utf8SearchPlan(
            Utf8SearchKind.ExactAsciiLiteral,
            Encoding.UTF8.GetBytes("foo"),
            canGuideFallbackStarts: true,
            maxPossibleLength: 6);
        var input = Encoding.UTF8.GetBytes("xxfoozzyy");
        var state = new Utf8StructuralSearchState(new PreparedSearchScanState(0, default), new PreparedWindowScanState(0, new PreparedSearchScanState(0, default)));

        Assert.True(plan.StructuralSearchPlan.TryFindNextCandidate(input, ref state, out var candidate));
        Assert.Equal(2, candidate.StartIndex);
        Assert.Equal(8, candidate.EndIndex);
    }

    [Fact]
    public void StructuralWindowPlanIncludesExplicitSpanStages()
    {
        var windowSearch = new PreparedWindowSearch(
            new PreparedSearcher(new PreparedMultiLiteralSearch([Encoding.UTF8.GetBytes("using var")], ignoreCase: false)),
            new PreparedSearcher(new PreparedSubstringSearch(Encoding.UTF8.GetBytes("await"), ignoreCase: false), ignoreCase: false),
            maxGap: 32,
            sameLine: true);

        var plan = Utf8StructuralSearchPlan.Create(
            Utf8SearchKind.None,
            0,
            canGuideFallbackStarts: false,
            default,
            windowSearch,
            null,
            default);

        Assert.True(plan.HasValue);
        Assert.Equal(Utf8StructuralSearchYieldKind.Window, plan.YieldKind);
        Assert.Contains(plan.Stages!, static stage => stage.Kind == Utf8StructuralSearchStageKind.RequireWithinByteSpan && stage.MaxSpan == 32);
        Assert.Contains(plan.Stages!, static stage => stage.Kind == Utf8StructuralSearchStageKind.RequireWithinLineSpan && stage.MaxLines == 1);
        Assert.Equal(Utf8StructuralSearchStageKind.YieldWindow, plan.Stages![^1].Kind);
    }

    [Fact]
    public void StructuralWindowPlanEnforcesExplicitLineSpanStages()
    {
        var windowSearch = new PreparedWindowSearch(
            new PreparedSearcher(new PreparedMultiLiteralSearch([Encoding.UTF8.GetBytes("using var")], ignoreCase: false)),
            new PreparedSearcher(new PreparedSubstringSearch(Encoding.UTF8.GetBytes("await"), ignoreCase: false), ignoreCase: false),
            maxGap: 64,
            sameLine: true);

        var plan = Utf8StructuralSearchPlan.Create(
            Utf8SearchKind.None,
            0,
            canGuideFallbackStarts: false,
            default,
            windowSearch,
            null,
            default);

        var input = Encoding.UTF8.GetBytes("using var value = await\nusing var other = await");
        var state = new Utf8StructuralSearchState(default, new PreparedWindowScanState(0, new PreparedSearchScanState(0, default)));

        Assert.True(plan.TryFindNextCandidate(input, ref state, out var first));
        Assert.Equal(0, first.StartIndex);
        Assert.Equal("using var value = await".Length, first.EndIndex);
        Assert.Equal("using var".Length, first.MatchLength);
        Assert.Equal("using var value = ".Length, first.TrailingIndex);
        Assert.Equal("await".Length, first.TrailingMatchLength);
        Assert.True(plan.TryFindNextCandidate(input, ref state, out var second));
        Assert.Equal("using var value = await\n".Length, second.StartIndex);
        Assert.Equal(input.Length, second.EndIndex);
        Assert.Equal("using var".Length, second.MatchLength);
        Assert.Equal(input.Length - "await".Length, second.TrailingIndex);
        Assert.Equal("await".Length, second.TrailingMatchLength);
        Assert.False(plan.TryFindNextCandidate(input, ref state, out _));
    }

    [Fact]
    public void StructuralWindowPlanCanApplyBoundaryStages()
    {
        var plan = new Utf8SearchPlan(
            Utf8SearchKind.None,
            null,
            orderedWindowLeadingLiteralsUtf8:
            [
                Encoding.UTF8.GetBytes("foo"),
            ],
            orderedWindowTrailingLiteralUtf8: Encoding.UTF8.GetBytes("bar"),
            orderedWindowMaxGap: 16,
            orderedWindowSameLine: true,
            leadingBoundary: Utf8BoundaryRequirement.Boundary,
            trailingBoundary: Utf8BoundaryRequirement.Boundary);
        var input = Encoding.UTF8.GetBytes("xfoo bar foo barz foo bar");
        var state = new Utf8StructuralSearchState(default, new PreparedWindowScanState(0, new PreparedSearchScanState(0, default)));

        Assert.True(plan.StructuralSearchPlan.TryFindNextCandidate(input, ref state, out var candidate));
        Assert.Equal(Encoding.UTF8.GetByteCount("xfoo bar foo barz "), candidate.StartIndex);
        Assert.False(plan.StructuralSearchPlan.TryFindNextCandidate(input, ref state, out _));
    }

    [Fact]
    public void FallbackVerifierPlanRequiresTrailingAnchorCoverageForWindowPlans()
    {
        var windowSearch = new PreparedWindowSearch(
            new PreparedSearcher(new PreparedMultiLiteralSearch([Encoding.UTF8.GetBytes("using var")], ignoreCase: false)),
            new PreparedSearcher(new PreparedSubstringSearch(Encoding.UTF8.GetBytes("await"), ignoreCase: false), ignoreCase: false),
            maxGap: 32,
            sameLine: true);
        var plan = Utf8StructuralSearchPlan.Create(
            Utf8SearchKind.None,
            0,
            canGuideFallbackStarts: false,
            default,
            windowSearch,
            null,
            default);

        var verifier = Utf8FallbackVerifierPlan.Create("using\\s+var.*await", RegexOptions.CultureInvariant, plan);

        Assert.True(verifier.RequiresCandidateEndCoverage);
        Assert.True(verifier.RequiresTrailingAnchorCoverage);
        Assert.Equal(Utf8FallbackVerifierMode.AnchoredSliceRegex, verifier.Mode);
    }

    [Fact]
    public void FallbackVerifierPlanUsesBoundedSliceModeForExactLengthStartPlans()
    {
        var plan = new Utf8SearchPlan(
            Utf8SearchKind.ExactAsciiLiteral,
            Encoding.UTF8.GetBytes("foo"),
            canGuideFallbackStarts: true,
            exactRequiredLength: 5).StructuralSearchPlan;

        var verifier = Utf8FallbackVerifierPlan.Create("foo..", RegexOptions.CultureInvariant, plan);

        Assert.True(verifier.RequiresCandidateEndCoverage);
        Assert.False(verifier.RequiresTrailingAnchorCoverage);
        Assert.Equal(Utf8FallbackVerifierMode.AnchoredSliceRegex, verifier.Mode);
    }

    [Fact]
    public void ExactLengthFallbackSliceVerificationRequiresFullCandidateCoverage()
    {
        var regex = new Utf8Regex("(foo)\\1", RegexOptions.CultureInvariant);

        Assert.True(regex.IsMatch(Encoding.UTF8.GetBytes("xxfoofooyy")));
        Assert.False(regex.IsMatch(Encoding.UTF8.GetBytes("xxfooxxxyy")));
    }

    [Fact]
    public void FallbackVerifierPlanUsesBoundedSliceModeForMaxLengthStartPlans()
    {
        var plan = new Utf8SearchPlan(
            Utf8SearchKind.ExactAsciiLiteral,
            Encoding.UTF8.GetBytes("foo"),
            canGuideFallbackStarts: true,
            maxPossibleLength: 6).StructuralSearchPlan;

        var verifier = Utf8FallbackVerifierPlan.Create("(foo)\\\\1?", RegexOptions.CultureInvariant, plan);

        Assert.True(plan.ProducesBoundedCandidates);
        Assert.False(verifier.RequiresCandidateEndCoverage);
        Assert.False(verifier.RequiresTrailingAnchorCoverage);
        Assert.Equal(Utf8FallbackVerifierMode.AnchoredSliceRegex, verifier.Mode);
    }

    [Fact]
    public void AnalyzerCanExtractFiniteOrderedWindowGap()
    {
        var analysis = Utf8FrontEnd.Analyze(@"(?:using var|await using var)[A-Z]{1,3}await", RegexOptions.CultureInvariant);
        var searchInfo = Utf8FrontEndSearchAnalyzer.Analyze(analysis.SemanticRegex);

        Assert.Equal(Utf8SearchKind.FixedDistanceAsciiLiteral, searchInfo.Kind);
        Assert.Equal(3, searchInfo.OrderedWindowMaxGap);
        Assert.True(searchInfo.OrderedWindowSameLine);

        var searchPlan = new Utf8SearchPlan(
            searchInfo.Kind,
            literalUtf8: searchInfo.LiteralUtf8,
            alternateLiteralsUtf8: searchInfo.AlternateLiteralsUtf8,
            canGuideFallbackStarts: searchInfo.CanGuideFallbackStarts,
            requiredPrefilterLiteralUtf8: searchInfo.RequiredPrefilterLiteralUtf8,
            requiredPrefilterAlternateLiteralsUtf8: searchInfo.RequiredPrefilterAlternateLiteralsUtf8,
            secondaryRequiredPrefilterQuotedAsciiSet: searchInfo.SecondaryRequiredPrefilterQuotedAsciiSet,
            secondaryRequiredPrefilterQuotedAsciiLength: searchInfo.SecondaryRequiredPrefilterQuotedAsciiLength,
            fixedDistanceSets: searchInfo.FixedDistanceSets,
            trailingLiteralUtf8: searchInfo.TrailingLiteralUtf8,
            orderedWindowLeadingLiteralsUtf8: searchInfo.OrderedWindowLeadingLiteralsUtf8,
            orderedWindowTrailingLiteralUtf8: searchInfo.OrderedWindowTrailingLiteralUtf8,
            requiredWindowPrefilters: searchInfo.RequiredWindowPrefilters,
            orderedWindowMaxGap: searchInfo.OrderedWindowMaxGap,
            orderedWindowSameLine: searchInfo.OrderedWindowSameLine,
            fallbackStartTransform: searchInfo.FallbackStartTransform,
            distance: searchInfo.Distance,
            minRequiredLength: searchInfo.MinRequiredLength,
            exactRequiredLength: searchInfo.ExactRequiredLength,
            maxPossibleLength: searchInfo.MaxPossibleLength,
            leadingBoundary: searchInfo.LeadingBoundary,
            trailingBoundary: searchInfo.TrailingBoundary);
        Assert.Equal(3, searchPlan.OrderedWindowMaxGap);
        Assert.True(searchPlan.OrderedWindowSameLine);
    }

    [Fact]
    public void AnalyzerDoesNotInventLineBoundedWindowForNewlinePermittingGap()
    {
        var analysis = Utf8FrontEnd.Analyze(@"foo\s+bar", RegexOptions.CultureInvariant);
        var searchInfo = Utf8FrontEndSearchAnalyzer.Analyze(analysis.SemanticRegex);

        Assert.False(searchInfo.OrderedWindowSameLine);
        Assert.Null(searchInfo.OrderedWindowMaxGap);
    }

    private static string DumpNode(RegexNode node, int depth = 0)
    {
        var indent = new string(' ', depth * 2);
        var line = $"{indent}{node.Kind} ch={node.Ch} str={node.Str ?? "<null>"} m={node.M} n={node.N} children={node.ChildCount}";
        if (node.ChildCount == 0)
        {
            return line;
        }

        return line + Environment.NewLine + string.Join(Environment.NewLine, node.ChildList.Select(child => DumpNode(child, depth + 1)));
    }

    [Fact]
    public void ConstructorPrefersFixedDistanceLiteralSearchPlanForAsciiSimplePattern()
    {
        var regex = new Utf8Regex("ab[0-9][0-9]cd", RegexOptions.CultureInvariant);

        Assert.Equal(Utf8SearchKind.FixedDistanceAsciiChar, regex.SearchPlan.Kind);
        Assert.Equal("d", Encoding.UTF8.GetString(regex.SearchPlan.LiteralUtf8!));
        Assert.Equal(5, regex.SearchPlan.Distance);
        Assert.Equal(6, regex.SearchPlan.MinRequiredLength);
    }

    [Fact]
    public void ConstructorCanStillCreateFixedDistanceLiteralSearchPlanWhenNoSetExists()
    {
        var regex = new Utf8Regex("ab..cd", RegexOptions.CultureInvariant);

        Assert.Equal(Utf8SearchKind.FixedDistanceAsciiChar, regex.SearchPlan.Kind);
        Assert.Equal("d", Encoding.UTF8.GetString(regex.SearchPlan.LiteralUtf8!));
        Assert.Equal(5, regex.SearchPlan.Distance);
        Assert.Equal(6, regex.SearchPlan.MinRequiredLength);
    }

    [Fact]
    public void ConstructorCanCreateTrailingFixedLengthAnchorSearchPlan()
    {
        var regex = new Utf8Regex(@"abc\z", RegexOptions.CultureInvariant);

        Assert.Equal(Utf8SearchKind.TrailingAnchorFixedLengthEnd, regex.SearchPlan.Kind);
        Assert.Equal(3, regex.SearchPlan.MinRequiredLength);
    }

    [Fact]
    public void ExactLiteralKernelSupportsSharedFirstByteFamilies()
    {
        var search = new PreparedLiteralSetSearch(
        [
            Encoding.UTF8.GetBytes("amber"),
            Encoding.UTF8.GetBytes("atlas"),
            Encoding.UTF8.GetBytes("axiom"),
            Encoding.UTF8.GetBytes("adore"),
        ]);
        var input = Encoding.UTF8.GetBytes("scan:aqqqq;scan:amber;scan:aqqqq;scan:axiom;");

        Assert.Equal(PreparedLiteralSetStrategy.Bucketed, search.Strategy);
        var first = Utf8SearchKernel.IndexOfAnyLiteral(input, search);
        var last = Utf8SearchKernel.LastIndexOfAnyLiteral(input, search);

        Assert.Equal("amber", Encoding.UTF8.GetString(input.AsSpan(first, 5)));
        Assert.Equal("axiom", Encoding.UTF8.GetString(input.AsSpan(last, 5)));
    }

    [Fact]
    public void ExactLiteralKernelSupportsMixedBucketsWithSharedFirstByteFamilies()
    {
        var search = new PreparedLiteralSetSearch(
        [
            Encoding.UTF8.GetBytes("amber"),
            Encoding.UTF8.GetBytes("atlas"),
            Encoding.UTF8.GetBytes("axiom"),
            Encoding.UTF8.GetBytes("adore"),
            Encoding.UTF8.GetBytes("needle"),
        ]);
        var input = Encoding.UTF8.GetBytes("scan:aqqqq;scan:needle;scan:aqqqq;scan:amber;");

        Assert.Equal(PreparedLiteralSetStrategy.UniqueAnchorByte, search.Strategy);
        var first = Utf8SearchKernel.IndexOfAnyLiteral(input, search);
        var last = Utf8SearchKernel.LastIndexOfAnyLiteral(input, search);

        Assert.Equal("needle", Encoding.UTF8.GetString(input.AsSpan(first, 6)));
        Assert.Equal("amber", Encoding.UTF8.GetString(input.AsSpan(last, 5)));
    }

    [Fact]
    public void ExactLiteralKernelUsesSingleLiteralBucketStrategyWhenBucketsAreUnique()
    {
        var search = new PreparedLiteralSetSearch(
        [
            Encoding.UTF8.GetBytes("abc"),
            Encoding.UTF8.GetBytes("dbc"),
            Encoding.UTF8.GetBytes("ebc"),
            Encoding.UTF8.GetBytes("fbc"),
        ]);
        var input = Encoding.UTF8.GetBytes("scan:zebra;scan:ebc;scan:fbc;");

        Assert.Equal(PreparedLiteralSetStrategy.SingleLiteralBuckets, search.Strategy);
        Assert.Equal(16, Utf8SearchKernel.IndexOfAnyLiteral(input, search));
    }

    [Fact]
    public void ExactLiteralKernelUsesSingleLiteralBucketsWhenFirstBytesAlreadyDisambiguateLiterals()
    {
        var search = new PreparedLiteralSetSearch(
        [
            Encoding.UTF8.GetBytes("cab"),
            Encoding.UTF8.GetBytes("dad"),
            Encoding.UTF8.GetBytes("eaf"),
        ]);
        var input = Encoding.UTF8.GetBytes("xxeafxx");

        Assert.Equal(PreparedLiteralSetStrategy.SingleLiteralBuckets, search.Strategy);
        Assert.Equal(2, Utf8SearchKernel.IndexOfAnyLiteral(input, search));
    }

    [Fact]
    public void ExactLiteralKernelUsesPreparedTrieForLargerLiteralFamilies()
    {
        var search = new PreparedLiteralSetSearch(
        [
            Encoding.UTF8.GetBytes("abacus"),
            Encoding.UTF8.GetBytes("absorb"),
            Encoding.UTF8.GetBytes("accord"),
            Encoding.UTF8.GetBytes("acumen"),
            Encoding.UTF8.GetBytes("anchor"),
            Encoding.UTF8.GetBytes("anthem"),
            Encoding.UTF8.GetBytes("aspire"),
            Encoding.UTF8.GetBytes("aviate"),
            Encoding.UTF8.GetBytes("beacon"),
            Encoding.UTF8.GetBytes("binary"),
            Encoding.UTF8.GetBytes("bronze"),
            Encoding.UTF8.GetBytes("candid"),
            Encoding.UTF8.GetBytes("cobble"),
            Encoding.UTF8.GetBytes("cortex"),
            Encoding.UTF8.GetBytes("dynamo"),
            Encoding.UTF8.GetBytes("needle"),
        ]);
        var input = Encoding.UTF8.GetBytes("scan:haystack;scan:beacon;scan:needle;");

        Assert.Equal(PreparedLiteralSetStrategy.MultiLiteralTrie, search.Strategy);
        var first = Utf8SearchKernel.IndexOfAnyLiteral(input, search);
        var last = Utf8SearchKernel.LastIndexOfAnyLiteral(input, search);

        Assert.Equal("beacon", Encoding.UTF8.GetString(input.AsSpan(first, 6)));
        Assert.Equal("needle", Encoding.UTF8.GetString(input.AsSpan(last, 6)));
        Assert.True(Utf8SearchKernel.TryGetMatchedLiteralLength(input, first, search, out var matchedLength));
        Assert.Equal(6, matchedLength);
    }
}

