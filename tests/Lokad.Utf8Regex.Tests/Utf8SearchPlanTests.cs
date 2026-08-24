using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.FrontEnd;
using Lokad.Utf8Regex.Internal.FrontEnd.Runtime;
using Lokad.Utf8Regex.Internal.Input;
using Lokad.Utf8Regex.Internal.Planning;
using Lokad.Utf8Regex.Internal.Search;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8SearchPlanTests
{
    [Fact]
    public void ConstructorCreatesExactLiteralSearchPlanForPlainAsciiLiteral()
    {
        var regex = new Utf8Regex("needle", RegexOptions.CultureInvariant);

        Assert.Equal(Utf8SearchKind.ExactAsciiLiteral, regex.Inspection.SearchPlan.Kind);
        Assert.True(regex.Inspection.SearchPlan.HasLiteral);
        Assert.True(regex.Inspection.SearchPlan.HasPreparedSearcher);
        Assert.Equal(PreparedSearcherKind.ExactLiteral, regex.Inspection.SearchPlan.PreparedSearcher.Kind);
        Assert.Equal(regex.Inspection.SearchPlan.PreparedSearcher.Kind, regex.Inspection.SearchPlan.PreparedSearcher.Kind);
    }

    [Fact]
    public void ConstructorCreatesIgnoreCaseSearchPlanForInvariantIgnoreCaseLiteral()
    {
        var regex = new Utf8Regex("needle", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        Assert.Equal(Utf8SearchKind.AsciiFoldedByteLiteral, regex.Inspection.SearchPlan.Kind);
        Assert.True(regex.Inspection.SearchPlan.HasLiteral);
        Assert.Equal(PreparedSearcherKind.IgnoreCaseLiteral, regex.Inspection.SearchPlan.PreparedSearcher.Kind);
    }

    [Fact]
    public void ConstructorCreatesIgnoreCaseSearchPlanForInvariantIgnoreCaseLiteralAlternation()
    {
        var regex = new Utf8Regex("needle|thread|fiber", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        Assert.Equal(Utf8SearchKind.AsciiFoldedByteLiterals, regex.Inspection.SearchPlan.Kind);
        Assert.Equal(NativeExecutionKind.AsciiLiteralIgnoreCaseLiterals, regex.Inspection.ExecutionKind);
        Assert.Equal(Utf8SearchPortfolioKind.AsciiIgnoreCaseFamily, regex.Inspection.SearchPortfolioKind);
        Assert.True(regex.Inspection.SearchPlan.HasAlternateLiterals);
        Assert.True(regex.Inspection.SearchPlan.AlternateIgnoreCaseLiteralSearch.HasValue);
        Assert.False(regex.Inspection.SearchPlan.AlternateLiteralSearch.HasValue);
        Assert.Equal(PreparedSearcherKind.MultiLiteral, regex.Inspection.SearchPlan.PreparedSearcher.Kind);

        var alternate = regex.Inspection.SearchPlan.AlternateIgnoreCaseLiteralSearch.Value;
        var prepared = regex.Inspection.SearchPlan.MultiLiteralSearch.IgnoreCaseSearch;
        Assert.Same(alternate.FirstByteSearchValues, prepared.FirstByteSearchValues);
        Assert.Same(alternate.Buckets, prepared.Buckets);
        Assert.Same(alternate.BucketIndexMap, prepared.BucketIndexMap);
    }

    [Fact]
    public void ConstructorCanCreateTreeDerivedSearchPlanForFallbackPattern()
    {
        var regex = new Utf8Regex("a.*b", RegexOptions.CultureInvariant);

        Assert.Equal(Utf8SearchKind.ExactAsciiLiteral, regex.Inspection.SearchPlan.Kind);
        Assert.True(regex.Inspection.SearchPlan.HasLiteral);
        Assert.True(regex.Inspection.SearchPlan.HasFallbackCandidates);
        Assert.True(regex.Inspection.StructuralSearchPlan.HasValue);
        Assert.True(regex.Inspection.SearchPlan.HasStructuralCandidates);
        Assert.Equal(Utf8StructuralSearchYieldKind.Start, regex.Inspection.StructuralSearchPlan.YieldKind);
        Assert.Equal(Utf8StructuralSearchStageKind.FindLiteralFamily, regex.Inspection.StructuralSearchPlan.Stages![0].Kind);
        Assert.Contains(regex.Inspection.StructuralSearchPlan.Stages!, static stage => stage.Kind == Utf8StructuralSearchStageKind.RequireByteAtOffset);
        Assert.Equal(Utf8StructuralSearchStageKind.YieldStart, regex.Inspection.StructuralSearchPlan.Stages![^1].Kind);
    }

    [Fact]
    public void ConstructorCanCreateAlternationPrefixSearchPlanForAsciiAlternation()
    {
        var regex = new Utf8Regex("cat|horse", RegexOptions.CultureInvariant);

        Assert.Equal(Utf8SearchKind.ExactAsciiLiterals, regex.Inspection.SearchPlan.Kind);
        Assert.Equal(NativeExecutionKind.ExactUtf8Literals, regex.Inspection.ExecutionKind);
        Assert.NotEqual(Utf8SearchPortfolioKind.None, regex.Inspection.SearchPortfolioKind);
        Assert.True(regex.Inspection.SearchPlan.HasAlternateLiterals);
        Assert.True(regex.Inspection.SearchPlan.AlternateLiteralSearch.HasValue);
        Assert.Equal(PreparedSearcherKind.MultiLiteral, regex.Inspection.SearchPlan.PreparedSearcher.Kind);
        Assert.Equal(["cat", "horse"], regex.Inspection.SearchPlan.AlternateLiteralsUtf8!.Select(Encoding.UTF8.GetString));
    }

    [Fact]
    public void ConstructorCanCreateAlternationSearchPlanForUtf8Alternation()
    {
        var regex = new Utf8Regex("café|niño", RegexOptions.CultureInvariant);

        Assert.Equal(Utf8SearchKind.ExactUtf8Literals, regex.Inspection.SearchPlan.Kind);
        Assert.True(regex.Inspection.SearchPlan.HasAlternateLiterals);
        Assert.True(regex.Inspection.SearchPlan.AlternateLiteralSearch.HasValue);
        Assert.Equal(["café", "niño"], regex.Inspection.SearchPlan.AlternateLiteralsUtf8!.Select(Encoding.UTF8.GetString));
    }

    [Fact]
    public void ConstructorCanCreateBoundaryWrappedAlternationSearchPlanForUtf8Alternation()
    {
        var regex = new Utf8Regex(@"\b(?:café|niño)\b", RegexOptions.CultureInvariant);

        Assert.Equal(Utf8SearchKind.ExactUtf8Literals, regex.Inspection.SearchPlan.Kind);
        Assert.True(regex.Inspection.SearchPlan.HasAlternateLiterals);
        Assert.True(regex.Inspection.SearchPlan.AlternateLiteralSearch.HasValue);
        Assert.True(regex.Inspection.StructuralSearchPlan.HasValue);
        Assert.Equal(Utf8StructuralSearchYieldKind.Start, regex.Inspection.StructuralSearchPlan.YieldKind);
        Assert.Equal(Utf8BoundaryRequirement.Boundary, regex.Inspection.SearchPlan.LeadingBoundary);
        Assert.Equal(Utf8BoundaryRequirement.Boundary, regex.Inspection.SearchPlan.TrailingBoundary);
        Assert.Equal(["café", "niño"], regex.Inspection.SearchPlan.AlternateLiteralsUtf8!.Select(Encoding.UTF8.GetString));
    }

    [Fact]
    public void ConstructorCanSelectEarliestPortfolioForTinyLongUtf8Alternation()
    {
        var regex = new Utf8Regex(@"\b(?:Task|ValueTask|IAsyncEnumerable)\b", RegexOptions.CultureInvariant);

        Assert.Equal(Utf8SearchKind.ExactAsciiLiterals, regex.Inspection.SearchPlan.Kind);
        Assert.Equal(Utf8SearchPortfolioKind.ExactEarliestFamily, regex.Inspection.SearchPortfolioKind);
        Assert.Equal(PreparedMultiLiteralKind.ExactEarliest, regex.Inspection.SearchPlan.MultiLiteralSearch.Kind);
    }

    [Fact]
    public void StructuralSearchPlanCanFilterBoundaryWrappedUtf8AlternationCandidates()
    {
        var regex = new Utf8Regex(@"\b(?:café|niño)\b", RegexOptions.CultureInvariant);
        var input = Encoding.UTF8.GetBytes("xcafé café niñoz niño");
        var firstExpected = Encoding.UTF8.GetByteCount("xcafé ");
        var secondExpected = Encoding.UTF8.GetByteCount("xcafé café niñoz ");
        var state = new Utf8StructuralSearchState(new PreparedSearchScanState(0, default), new PreparedWindowScanState(0, new PreparedSearchScanState(0, default)));

        Assert.True(regex.Inspection.StructuralSearchPlan.TryFindNextCandidate(input, ref state, out var first));
        Assert.Equal(firstExpected, first.StartIndex);
        Assert.True(regex.Inspection.StructuralSearchPlan.TryFindNextCandidate(input, ref state, out var second));
        Assert.Equal(secondExpected, second.StartIndex);
        Assert.False(regex.Inspection.StructuralSearchPlan.TryFindNextCandidate(input, ref state, out _));
    }

    [Fact]
    public void StructuralSearchPlanCanFindLastFilteredUtf8AlternationCandidate()
    {
        var regex = new Utf8Regex(@"\b(?:café|niño)\b", RegexOptions.CultureInvariant);
        var input = Encoding.UTF8.GetBytes("xcafé café niñoz niño");
        var expected = Encoding.UTF8.GetByteCount("xcafé café niñoz ");

        Assert.True(regex.Inspection.StructuralSearchPlan.TryFindLastCandidate(input, input.Length, out var candidate));
        Assert.Equal(expected, candidate.StartIndex);
    }

    [Fact]
    public void SearchExecutorUsesStructuralPlanForFilteredSingleLiteralSearch()
    {
        var regex = new Utf8Regex(@"\bfoo\b", RegexOptions.CultureInvariant);
        var input = Encoding.UTF8.GetBytes("xfoo foo fooz");

        Assert.Equal(5, Utf8SearchExecutor.FindFirst(regex.Inspection.SearchPlan, input));
        Assert.Equal(5, Utf8SearchExecutor.FindNext(regex.Inspection.SearchPlan, input, 0));
        Assert.Equal(5, Utf8SearchExecutor.FindLast(regex.Inspection.SearchPlan, input));
    }

    [Fact]
    public void StructuralStartPlanIncludesBoundaryStagesForBoundaryWrappedFallbackAnchor()
    {
        var plan = Utf8SearchPlan.Prepare(Utf8SearchFacts.Create(
                                              Utf8SearchKind.ExactAsciiLiteral,
                                              new Utf8SearchFactData
                                              {
                                                  LiteralUtf8 = Encoding.UTF8.GetBytes("foo"),
                                                  CanGuideFallbackStarts = true,
                                                  LeadingBoundary = Utf8BoundaryRequirement.Boundary,
                                                  TrailingBoundary = Utf8BoundaryRequirement.Boundary,
                                              }));

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
        var analysis = Utf8FrontEnd.Compile(pattern, RegexOptions.CultureInvariant);
        var family = RegexRequiredLiteralAnalyzer.FindBestRequiredLiteralFamily(analysis.SemanticRegex.RuntimeTree!.Root);
        Assert.True(family is { Length: > 0 }, DumpNode(analysis.SemanticRegex.RuntimeTree.Root));

        var regex = new Utf8Regex(pattern, RegexOptions.CultureInvariant);

        Assert.Equal(Utf8SearchKind.ExactAsciiLiteral, regex.Inspection.SearchPlan.Kind);
        Assert.Equal(PreparedSearcherKind.MultiLiteral, regex.Inspection.SearchPlan.RequiredPrefilterSearcher.Kind);
        Assert.True(regex.Inspection.SearchPlan.PrefilterPlan.HasValue);
        Assert.True(regex.Inspection.SearchPlan.FallbackSearch.HasRequiredPrefilter);
        Assert.Equal(["AIDA", "AKIA", "AROA", "ASIA"], regex.Inspection.SearchPlan.RequiredPrefilterAlternateLiteralsUtf8!.Select(Encoding.UTF8.GetString));
    }

    [Fact]
    public void FallbackSearchPlanCarriesExplicitCandidateStageForFallbackPattern()
    {
        var regex = new Utf8Regex("a.*b", RegexOptions.CultureInvariant);

        Assert.True(regex.Inspection.SearchPlan.FallbackSearch.HasCandidates);
        Assert.NotNull(regex.Inspection.SearchPlan.FallbackSearch.CandidatePlans);
        Assert.Same(regex.Inspection.SearchPlan.FallbackSearch.CandidatePlans, regex.Inspection.SearchPlan.FallbackCandidatePlans);
        Assert.Equal(Utf8CandidateSearchKind.StructuralSearchSet, regex.Inspection.SearchPlan.FallbackCandidateSource.Kind);
        Assert.True(regex.Inspection.SearchPlan.FallbackCandidateSource.Semantics.RequiresConfirmation);
    }

    [Fact]
    public void NativeSearchPlanCarriesExplicitCandidateSourceForExactLiteral()
    {
        var regex = new Utf8Regex("abc", RegexOptions.CultureInvariant);

        Assert.Equal(Utf8CandidateSearchKind.PreparedSearcher, regex.Inspection.SearchPlan.NativeCandidateSource.Kind);
        Assert.Equal(Utf8SearchOverlapPolicy.Overlapping, regex.Inspection.SearchPlan.NativeCandidateSource.Semantics.OverlapPolicy);
        Assert.Equal(regex.Inspection.SearchPlan.PreparedSearcher.Kind, regex.Inspection.SearchPlan.NativeCandidateSource.PreparedSearcher.Kind);
        Assert.Equal(Utf8SearchOperationKind.DirectSearch, regex.Inspection.SearchPlan.CountOperation.Kind);
    }

    [Fact]
    public void SearchPlanCarriesHybridStrategyForLargeAutomatonLiteralFamilies()
    {
        var regex = new Utf8Regex("Sherlock Holmes|John Watson|Irene Adler|Inspector Lestrade|Professor Moriarty", RegexOptions.CultureInvariant);

        Assert.Equal(Utf8SearchOperationKind.HybridSearch, regex.Inspection.SearchPlan.CountOperation.Kind);
        Assert.Equal(Utf8SearchOperationKind.HybridSearch, regex.Inspection.SearchPlan.FirstMatchOperation.Kind);
        Assert.Equal(Utf8CandidateSearchKind.PreparedSearcher, regex.Inspection.SearchPlan.CountOperation.CandidateSource.Kind);
        Assert.Equal(Utf8SearchObservabilityKind.Effectiveness, regex.Inspection.SearchPlan.CountOperation.ObservabilityKind);
    }

    [Fact]
    public void SearchPlanCarriesConfirmationAndProjectionPlans()
    {
        var boundaryRegex = new Utf8Regex(@"\b(?:Task|ValueTask|IAsyncEnumerable)\b", RegexOptions.CultureInvariant);
        var fallbackRegex = new Utf8Regex("a.*b", RegexOptions.CultureInvariant);

        Assert.Equal(Utf8ConfirmationKind.BoundaryRequirements, boundaryRegex.Inspection.SearchPlan.ConfirmationPlan.Kind);
        Assert.Equal(Utf8ProjectionKind.Utf16BoundaryMap, boundaryRegex.Inspection.SearchPlan.ProjectionPlan.Kind);
        Assert.Equal(Utf8ConfirmationKind.None, fallbackRegex.Inspection.SearchPlan.ConfirmationPlan.Kind);
    }

    [Fact]
    public void SearchPlanCarriesExecutablePipelines()
    {
        var regex = new Utf8Regex(@"\b(?:Task|ValueTask|IAsyncEnumerable)\b", RegexOptions.CultureInvariant);
        var fallbackRegex = new Utf8Regex("a.*b", RegexOptions.CultureInvariant);

        Assert.Equal(Utf8SearchOperationKind.PrefilterThenConfirm, regex.Inspection.SearchPlan.EnumerationOperation.Kind);
        Assert.Equal(Utf8ConfirmationKind.BoundaryRequirements, regex.Inspection.SearchPlan.EnumerationOperation.Confirmation.Kind);
        Assert.Equal(Utf8ProjectionKind.Utf16BoundaryMap, regex.Inspection.SearchPlan.EnumerationOperation.Projection.Kind);

        Assert.Equal(Utf8SearchOperationKind.PrefilterThenSearch, fallbackRegex.Inspection.SearchPlan.FirstMatchOperation.Kind);
        Assert.Equal(Utf8SearchOperationKind.PrefilterThenSearch, fallbackRegex.Inspection.SearchPlan.CountOperation.Kind);
        Assert.Equal(Utf8ConfirmationKind.None, fallbackRegex.Inspection.SearchPlan.FirstMatchOperation.Confirmation.Kind);
        Assert.Equal(Utf8ProjectionKind.Utf16BoundaryMap, fallbackRegex.Inspection.SearchPlan.EnumerationOperation.Projection.Kind);
    }

    [Fact]
    public void SearchPlanCarriesBackendInstructionPrograms()
    {
        var regex = new Utf8Regex(@"\b(?:Task|ValueTask|IAsyncEnumerable)\b", RegexOptions.CultureInvariant);
        var fallbackRegex = new Utf8Regex("a.*b", RegexOptions.CultureInvariant);

        Assert.True(regex.Inspection.SearchPlan.EnumerationOperation.CandidateSource.HasValue);
        Assert.True(regex.Inspection.SearchPlan.EnumerationOperation.Confirmation.HasValue);
        Assert.True(regex.Inspection.SearchPlan.EnumerationOperation.Projection.HasValue);

        Assert.True(fallbackRegex.Inspection.SearchPlan.FirstMatchOperation.CandidateSource.HasValue);
        Assert.False(fallbackRegex.Inspection.SearchPlan.FirstMatchOperation.Confirmation.HasValue);
    }

    [Fact]
    public void CompiledEngineSelectionUsesExecutablePipelines()
    {
        var literalFamily = new Utf8Regex(@"\b(?:Task|ValueTask|IAsyncEnumerable)\b", RegexOptions.CultureInvariant);
        var fallback = new Utf8Regex("a.*b", RegexOptions.CultureInvariant);
        var structural = new Utf8Regex("ab[0-9][0-9]cd", RegexOptions.CultureInvariant);

        Assert.Equal(Utf8CompiledEngineKind.SearchGuidedFallback, literalFamily.Inspection.CompiledEngineKind);
        Assert.Equal(Utf8CompiledEngineKind.SearchGuidedFallback, fallback.Inspection.CompiledEngineKind);
        Assert.Equal(Utf8CompiledEngineKind.StructuralLinearAutomaton, structural.Inspection.CompiledEngineKind);
        Assert.Equal(Utf8CompiledExecutionBackend.InterpretedInstruction, fallback.Inspection.CompiledExecutionBackend);
        Assert.Equal(Utf8CompiledExecutionBackend.EmittedInstruction, structural.Inspection.CompiledExecutionBackend);
    }

    [Fact]
    public void CompiledSearchAnalysisClassifiesPlanDrivenFamilies()
    {
        var literalFamily = new Utf8Regex(@"\b(?:Task|ValueTask|IAsyncEnumerable)\b", RegexOptions.CultureInvariant);
        var fallback = new Utf8Regex("a.*b", RegexOptions.CultureInvariant);
        var identifierFamily = new Utf8Regex(@"\b(?:record|struct|class)\s+[A-Z][A-Za-z0-9_]+", RegexOptions.CultureInvariant);
        var orderedWindow = new Utf8Regex(@"\bpublic\s+async\b", RegexOptions.CultureInvariant);

        var literalAnalysis = Utf8CompiledSearchAnalyzer.Analyze(literalFamily.Inspection.PreparedRegex, preferCompiled: true);
        var fallbackAnalysis = Utf8CompiledSearchAnalyzer.Analyze(fallback.Inspection.PreparedRegex, preferCompiled: true);
        var identifierAnalysis = Utf8CompiledSearchAnalyzer.Analyze(identifierFamily.Inspection.PreparedRegex, preferCompiled: true);
        var orderedWindowAnalysis = Utf8CompiledSearchAnalyzer.Analyze(orderedWindow.Inspection.PreparedRegex, preferCompiled: true);

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

        Assert.True(Utf8EmittedKernelLowerer.TryLower(regex.Inspection.PreparedRegex, out var kernelPlan));
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

        Assert.True(Utf8EmittedKernelLowerer.TryLower(regex.Inspection.PreparedRegex, out var kernelPlan));
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

        Assert.True(Utf8EmittedKernelLowerer.TryLower(regex.Inspection.PreparedRegex, out var kernelPlan));
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

        Assert.True(Utf8EmittedKernelLowerer.TryLower(regex.Inspection.PreparedRegex, out var kernelPlan));
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
        var fallback = Utf8FrontEnd.Compile("a.*b", RegexOptions.CultureInvariant);
        var structural = Utf8FrontEnd.Compile("ab[0-9][0-9]cd", RegexOptions.CultureInvariant);
        var orderedWindow = Utf8FrontEnd.Compile(@"\b(?:using\s+var|await\s+using\s+var)\s+[A-Za-z_][A-Za-z0-9_]*\s*=\s*await\b", RegexOptions.CultureInvariant);

        Assert.Equal(Utf8CandidateSearchKind.PreparedSearcher, literalFamily.Inspection.SearchPlan.NativeCandidateSource.Kind);
        Assert.Equal(Utf8CandidateSearchKind.StructuralSearchSet, Utf8SearchEngineExecutor.GetPrimaryExecutionEngine(fallback).Kind);
        Assert.Equal(Utf8CandidateSearchKind.StructuralDeterministicAutomaton, Utf8SearchEngineExecutor.GetPrimaryExecutionEngine(structural).Kind);
        Assert.Equal(Utf8CandidateSearchKind.StructuralDeterministicAutomaton, Utf8SearchEngineExecutor.GetPrimaryExecutionEngine(orderedWindow).Kind);
    }

    [Fact]
    public void EmittedLiteralFamilyCounterMatchesInstructionExecutor()
    {
        var regex = new Utf8Regex(@"\b(?:Task|ValueTask|IAsyncEnumerable)\b", RegexOptions.CultureInvariant);
        var input = Encoding.UTF8.GetBytes("Task task ValueTask IAsyncEnumerableX IAsyncEnumerable");

        Assert.True(Utf8EmittedLiteralFamilyCounter.TryCreate(regex.Inspection.SearchPlan, regex.Inspection.SearchPlan.CountOperation, regex.Inspection.SearchPlan.FirstMatchOperation, out var counter));
        Assert.NotNull(counter);
        Assert.Equal(
            Utf8BackendInstructionExecutor.CountLiteralFamily(regex.Inspection.SearchPlan, regex.Inspection.SearchPlan.CountOperation, input, budget: Utf8ExecutionDeadline.Infinite),
            counter!.Count(input));
    }

    [Fact]
    public void EmittedLiteralFamilyCounterMatchesInstructionExecutorForIsMatch()
    {
        var regex = new Utf8Regex(@"\b(?:Task|ValueTask|IAsyncEnumerable)\b", RegexOptions.CultureInvariant);
        var hit = Encoding.UTF8.GetBytes("Task task ValueTask IAsyncEnumerableX IAsyncEnumerable");
        var miss = Encoding.UTF8.GetBytes("task IAsyncEnumerableX stream");

        Assert.True(Utf8EmittedLiteralFamilyCounter.TryCreate(regex.Inspection.SearchPlan, regex.Inspection.SearchPlan.CountOperation, regex.Inspection.SearchPlan.FirstMatchOperation, out var counter));
        Assert.NotNull(counter);
        Assert.Equal(
            Utf8BackendInstructionExecutor.IsMatchLiteralFamily(regex.Inspection.SearchPlan, regex.Inspection.SearchPlan.FirstMatchOperation, hit, budget: Utf8ExecutionDeadline.Infinite, rightToLeft: false),
            counter!.IsMatch(hit));
        Assert.Equal(
            Utf8BackendInstructionExecutor.IsMatchLiteralFamily(regex.Inspection.SearchPlan, regex.Inspection.SearchPlan.FirstMatchOperation, miss, budget: Utf8ExecutionDeadline.Infinite, rightToLeft: false),
            counter.IsMatch(miss));
    }

    [Fact]
    public void EmittedLiteralFamilyCounterMatchesInstructionExecutorForFirstMatch()
    {
        var regex = new Utf8Regex(@"\b(?:Task|ValueTask|IAsyncEnumerable)\b", RegexOptions.CultureInvariant);
        var hit = Encoding.UTF8.GetBytes("Task task ValueTask IAsyncEnumerableX IAsyncEnumerable");
        var miss = Encoding.UTF8.GetBytes("task IAsyncEnumerableX stream");

        Assert.True(Utf8EmittedLiteralFamilyCounter.TryCreate(regex.Inspection.SearchPlan, regex.Inspection.SearchPlan.CountOperation, regex.Inspection.SearchPlan.FirstMatchOperation, out var counter));
        Assert.NotNull(counter);

        Assert.True(counter!.TryMatch(hit, out var emittedIndex, out var emittedLength));
        var interpreted = Utf8BackendInstructionExecutor.MatchLiteralFamily(regex.Inspection.SearchPlan, regex.Inspection.SearchPlan.FirstMatchOperation, hit, regex.Inspection.SearchPlan.AlternateLiteralUtf16Lengths, budget: Utf8ExecutionDeadline.Infinite, rightToLeft: false);
        Assert.True(interpreted.Success);
        Assert.Equal(interpreted.IndexInBytes, emittedIndex);
        Assert.Equal(interpreted.LengthInBytes, emittedLength);

        Assert.False(counter.TryMatch(miss, out emittedIndex, out emittedLength));
        Assert.Equal(-1, emittedIndex);
        Assert.Equal(0, emittedLength);
    }

    [Fact]
    public void EmittedUnconstrainedExactLiteralFamilyMatchesInstructionExecutor()
    {
        var regex = new Utf8Regex("alpha|bravo|charlie|delta|echo", RegexOptions.CultureInvariant);
        var input = Encoding.UTF8.GetBytes("\u00e9 ALPHA alpha bravo CHARLIE delta echo \ud83d\ude00");
        var plan = regex.Inspection.SearchPlan;

        Assert.Equal(Utf8ConfirmationKind.None, plan.CountOperation.Confirmation.Kind);
        Assert.Equal(Utf8ConfirmationKind.None, plan.FirstMatchOperation.Confirmation.Kind);
        Assert.True(Utf8EmittedLiteralFamilyCounter.TryCreate(plan, plan.CountOperation, plan.FirstMatchOperation, out var counter));
        Assert.NotNull(counter);
        Assert.Equal(
            Utf8BackendInstructionExecutor.CountLiteralFamily(plan, plan.CountOperation, input, budget: Utf8ExecutionDeadline.Infinite),
            counter!.Count(input));
        Assert.Equal(
            Utf8BackendInstructionExecutor.IsMatchLiteralFamily(plan, plan.FirstMatchOperation, input, budget: Utf8ExecutionDeadline.Infinite, rightToLeft: false),
            counter.IsMatch(input));

        Assert.True(counter.TryMatch(input, out var emittedIndex, out var emittedLength));
        var interpreted = Utf8BackendInstructionExecutor.MatchLiteralFamily(plan, plan.FirstMatchOperation, input, plan.AlternateLiteralUtf16Lengths, budget: Utf8ExecutionDeadline.Infinite, rightToLeft: false);
        Assert.True(interpreted.Success);
        Assert.Equal(interpreted.IndexInBytes, emittedIndex);
        Assert.Equal(interpreted.LengthInBytes, emittedLength);
    }

    [Fact]
    public void UnconstrainedIgnoreCaseLiteralFamilyUsesPreparedInstructionLoop()
    {
        var regex = new Utf8Regex(
            "Sherlock Holmes|John Watson|Irene Adler|Inspector Lestrade|Professor Moriarty",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);
        var plan = regex.Inspection.SearchPlan;

        Assert.Equal(Utf8ConfirmationKind.None, plan.CountOperation.Confirmation.Kind);
        Assert.Equal(Utf8ConfirmationKind.None, plan.FirstMatchOperation.Confirmation.Kind);
        Assert.Equal(Utf8CompiledExecutionBackend.InterpretedInstruction, regex.Inspection.CompiledExecutionBackend);
        Assert.False(Utf8EmittedLiteralFamilyCounter.TryCreate(plan, plan.CountOperation, plan.FirstMatchOperation, out _));
    }

    [Fact]
    public void EmittedSearchGuidedFallbackMatchesInterpreterForIsMatchAndCount()
    {
        var regex = new Utf8Regex("a.*b", RegexOptions.CultureInvariant);
        var analysis = Utf8FrontEnd.Compile("a.*b", RegexOptions.CultureInvariant);
        var verifierRuntime = Utf8VerifierRuntime.Create(analysis, "a.*b", RegexOptions.CultureInvariant, Utf8Regex.DefaultMatchTimeout);
        var hit = Encoding.UTF8.GetBytes("zzza123bzzz");
        var miss = Encoding.UTF8.GetBytes("zzza123czzz");

        Assert.False(Utf8EmittedSearchGuidedFallback.TryCreate(regex.Inspection.PreparedRegex, verifierRuntime, out var backend));
        Assert.Null(backend);
    }

    [Fact]
    public void EmittedSearchGuidedFallbackMatchesInterpreterForBoundaryLiteralFamilies()
    {
        const string pattern = @"\b(?:Task|ValueTask|IAsyncEnumerable)\b";
        var regex = new Utf8Regex(pattern, RegexOptions.CultureInvariant);
        var analysis = Utf8FrontEnd.Compile(pattern, RegexOptions.CultureInvariant);
        var verifierRuntime = Utf8VerifierRuntime.Create(analysis, pattern, RegexOptions.CultureInvariant, Utf8Regex.DefaultMatchTimeout);
        var input = Encoding.UTF8.GetBytes("Task task ValueTask IAsyncEnumerableX IAsyncEnumerable");

        Assert.True(Utf8EmittedSearchGuidedFallback.TryCreate(regex.Inspection.PreparedRegex, verifierRuntime, out var backend));
        Assert.NotNull(backend);

        Assert.Equal(
            Utf8SearchStrategyExecutor.CountLiteralFamily(regex.Inspection.SearchPlan, input, budget: Utf8ExecutionDeadline.Infinite),
            backend!.Count(input));
        Assert.Equal(
            Utf8SearchStrategyExecutor.IsMatchLiteralFamily(regex.Inspection.SearchPlan, input, budget: Utf8ExecutionDeadline.Infinite, rightToLeft: false),
            backend.IsMatch(input));
    }

    [Fact]
    public void SearchStrategyExecutorCanCountAndMatchLiteralFamilies()
    {
        var regex = new Utf8Regex(@"\b(?:Task|ValueTask|IAsyncEnumerable)\b", RegexOptions.CultureInvariant);
        var input = Encoding.UTF8.GetBytes("Task task ValueTask IAsyncEnumerableX IAsyncEnumerable");

        Assert.Equal(3, Utf8SearchStrategyExecutor.CountLiteralFamily(regex.Inspection.SearchPlan, input, budget: Utf8ExecutionDeadline.Infinite));
        Assert.True(Utf8SearchStrategyExecutor.IsMatchLiteralFamily(regex.Inspection.SearchPlan, input, budget: Utf8ExecutionDeadline.Infinite, rightToLeft: false));
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
        var analysis = Utf8FrontEnd.Compile(pattern, RegexOptions.Multiline);
        Assert.NotNull(analysis.SemanticRegex.RuntimeTree);
        var family = RegexRequiredLiteralAnalyzer.FindBestRequiredLiteralFamily(analysis.SemanticRegex.RuntimeTree!.Root);
        Assert.True(family is { Length: > 0 }, DumpNode(analysis.SemanticRegex.RuntimeTree.Root));

        var regex = new Utf8Regex(pattern, RegexOptions.Multiline);

        Assert.Equal(NativeExecutionKind.AsciiStructuralQuotedRelation, regex.Inspection.ExecutionKind);
        Assert.Equal(Utf8CompiledEngineKind.StructuralLinearAutomaton, regex.Inspection.CompiledEngineKind);
        Assert.Equal(Utf8StructuralLinearProgramKind.AsciiQuotedRelation, regex.Inspection.StructuralLinearProgramKind);
        Assert.Equal(PreparedSearcherKind.None, regex.Inspection.SearchPlan.RequiredPrefilterSearcher.Kind);
        Assert.Equal(PreparedSearcherKind.None, regex.Inspection.SearchPlan.SecondaryRequiredPrefilterSearcher.Kind);
        Assert.Null(regex.Inspection.SearchPlan.RequiredWindowPrefilterPlans);
        Assert.False(regex.Inspection.SearchPlan.HasFallbackCandidates);
        Assert.Null(regex.Inspection.SearchPlan.FallbackCandidatePlans);
    }

    [Fact]
    public void AwsKeyNativeQuotedRelationCanCountRepeatedForwardWindows()
    {
        const string pattern = "(('|\")((?:ASIA|AKIA|AROA|AIDA)([A-Z0-7]{16}))('|\").*?(\\n^.*?){0,4}(('|\")[a-zA-Z0-9+/]{40}('|\"))+|('|\")[a-zA-Z0-9+/]{40}('|\").*?(\\n^.*?){0,3}('|\")((?:ASIA|AKIA|AROA|AIDA)([A-Z0-7]{16}))('|\"))+";
        const string input = "\"AIDAABCDEFGHIJKLMNOP\"\nctx = 1\nctx = 2\n\"aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa\"\n\n\"AIDAABCDEFGHIJKLMNOP\"\nctx = 3\nctx = 4\n\"aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa\"\n";
        var regex = new Utf8Regex(pattern, RegexOptions.Multiline);
        var bytes = Encoding.UTF8.GetBytes(input);

        Assert.Equal(NativeExecutionKind.AsciiStructuralQuotedRelation, regex.Inspection.ExecutionKind);
        Assert.Equal(2, regex.Count(bytes));

        var match = regex.Match(bytes);
        Assert.True(match.Success);
        Assert.Equal(0, match.IndexInBytes);
    }

    [Fact]
    public void ConstructorCanCreateOrderedAsciiWindowSearchPlanForStructuralFallbackPattern()
    {
        var regex = new Utf8Regex(@"\b(?:using\s+var|await\s+using\s+var)\s+[A-Za-z_][A-Za-z0-9_]*\s*=\s*await\b", RegexOptions.CultureInvariant);

        Assert.Equal(NativeExecutionKind.AsciiStructuralIdentifierFamily, regex.Inspection.ExecutionKind);
        Assert.Equal(Utf8SearchKind.ExactAsciiLiterals, regex.Inspection.SearchPlan.Kind);
        Assert.True(regex.Inspection.StructuralSearchPlan.HasValue);
        Assert.Equal(Utf8StructuralSearchYieldKind.Start, regex.Inspection.StructuralSearchPlan.YieldKind);
        Assert.Equal(
            ["await using var", "using var"],
            regex.Inspection.SearchPlan.AlternateLiteralsUtf8!.Select(Encoding.UTF8.GetString).OrderBy(static value => value, StringComparer.Ordinal));
    }

    [Fact]
    public void RuffNoqaRealPatternUsesByteSafeInterpreterLaneWithStructuralStarts()
    {
        const string pattern = "(\\s*)((?:# [Nn][Oo][Qq][Aa])(?::\\s?(([A-Z]+[0-9]+(?:[,\\s]+)?)+))?)";
        var analysis = Utf8FrontEnd.Compile(pattern, RegexOptions.None);

        Assert.Equal(NativeExecutionKind.FallbackRegex, analysis.ExecutionKind);
        Assert.Equal(Utf8SearchKind.FixedDistanceAsciiSets, analysis.SearchPlan.Kind);
        Assert.False(analysis.SearchPlan.HasWindowSearch);
        Assert.True(Utf8ByteSafeInterpreterExecutor.CanExecute(analysis));
        Assert.Equal(Utf8CompiledEngineKind.ByteSafeLinear, Utf8CompiledEngineSelector.Select(analysis).Kind);
        Assert.True(analysis.SearchPlan.HasFallbackCandidates);
        Assert.True(analysis.StructuralSearchPlan.HasValue);
        Assert.Equal(Utf8StructuralSearchYieldKind.Start, analysis.StructuralSearchPlan.YieldKind);
        Assert.Equal(Utf8StructuralSearchStageKind.FindAscii, analysis.StructuralSearchPlan.Stages![0].Kind);
        Assert.Contains(analysis.StructuralSearchPlan.Stages!, static stage => stage.Kind == Utf8StructuralSearchStageKind.TransformCandidateStart);
        Assert.True(analysis.StructuralSearchPlan.Stages!.Count(static stage => stage.Kind == Utf8StructuralSearchStageKind.RequireByteAtOffset) >= 6);
        Assert.Contains(analysis.StructuralSearchPlan.Stages!, static stage => stage.Kind == Utf8StructuralSearchStageKind.RequireMinLength);
        Assert.Equal(Utf8StructuralSearchStageKind.YieldStart, analysis.StructuralSearchPlan.Stages![^1].Kind);
        Assert.Equal(Utf8FallbackStartTransformKind.TrimLeadingAsciiWhitespace, analysis.SearchPlan.FallbackStartTransform.Kind);
        Assert.NotNull(analysis.SearchPlan.FixedDistanceSets);
        Assert.True(analysis.SearchPlan.FixedDistanceSets!.Length >= 6);
        Assert.True(analysis.SearchPlan.FixedDistanceSets[0].Distance >= 2);
        var primaryBytes = analysis.SearchPlan.FixedDistanceSets[0].ByteSet.GetPositiveMatchBytes();
        Assert.Contains(primaryBytes, static value => value is (byte)'A' or (byte)'N' or (byte)'O' or (byte)'Q' or (byte)'a' or (byte)'n' or (byte)'o' or (byte)'q');
        Assert.True(analysis.DeterministicGuards.HasValue);
        Assert.NotNull(analysis.DeterministicGuards.PrefixGuards);
        Assert.NotEmpty(analysis.DeterministicGuards.PrefixGuards!);
        Assert.True(analysis.DeterministicGuards.PrefixGuards!.Length >= 6);
    }

    [Fact]
    public void RuffNoqaTweakedPatternUsesByteSafeInterpreterLane()
    {
        const string pattern = "(?:# [Nn][Oo][Qq][Aa])(?::\\s?(([A-Z]+[0-9]+(?:[,\\s]+)?)+))?";
        var analysis = Utf8FrontEnd.Compile(pattern, RegexOptions.None);

        Assert.Equal(NativeExecutionKind.FallbackRegex, analysis.ExecutionKind);
        Assert.True(Utf8ByteSafeInterpreterExecutor.CanExecute(analysis));
        Assert.Equal(Utf8CompiledEngineKind.ByteSafeLinear, Utf8CompiledEngineSelector.Select(analysis).Kind);
    }

    [Fact]
    public void VariableGapFixedDistanceLiteralCanUseAsciiStructuralTokenWindow()
    {
        const string pattern = "[A-Za-z]{10}\\s+[\\s\\S]{0,100}Result[\\s\\S]{0,100}\\s+[A-Za-z]{10}";
        var analysis = Utf8FrontEnd.Compile(pattern, RegexOptions.None);

        Assert.Equal(NativeExecutionKind.AsciiStructuralTokenWindow, analysis.ExecutionKind);
        Assert.Equal(Utf8SearchKind.ExactAsciiLiteral, analysis.SearchPlan.Kind);
        Assert.Equal(Utf8CompiledEngineKind.StructuralLinearAutomaton, Utf8CompiledEngineSelector.Select(analysis).Kind);
        Assert.Equal(Utf8StructuralLinearProgramKind.AsciiTokenWindow, analysis.StructuralLinearProgram.Kind);
    }

    [Fact]
    public void StructuralSearchPlanCanProduceFallbackStartCandidates()
    {
        var regex = new Utf8Regex("a.*b", RegexOptions.CultureInvariant);
        var input = Encoding.UTF8.GetBytes("zzza123bzzz");
        var state = new Utf8StructuralSearchState(new PreparedSearchScanState(0, default), new PreparedWindowScanState(0, new PreparedSearchScanState(0, default)));

        Assert.True(regex.Inspection.StructuralSearchPlan.TryFindNextCandidate(input, ref state, out var candidate));
        Assert.Equal(3, candidate.StartIndex);
        Assert.Equal(-1, candidate.EndIndex);
        Assert.Equal(1, candidate.MatchLength);
        Assert.Equal(0, candidate.LiteralId);
    }

    [Fact]
    public void StructuralSearchPlanCanApplyFallbackStartTransform()
    {
        const string pattern = "(\\s*)((?:# [Nn][Oo][Qq][Aa])(?::\\s?(([A-Z]+[0-9]+(?:[,\\s]+)?)+))?)";
        var analysis = Utf8FrontEnd.Compile(pattern, RegexOptions.None);
        var input = Encoding.UTF8.GetBytes("   # NOQA: ABC123");
        var state = new Utf8StructuralSearchState(new PreparedSearchScanState(0, default), new PreparedWindowScanState(0, new PreparedSearchScanState(0, default)));

        Assert.True(analysis.StructuralSearchPlan.TryFindNextCandidate(input, ref state, out var candidate));
        Assert.Equal(0, candidate.StartIndex);
        Assert.Equal(-1, candidate.EndIndex);
    }

    [Fact]
    public void StructuralSearchPlanCanRejectNonBoundaryFallbackAnchorCandidates()
    {
        var plan = Utf8SearchPlan.Prepare(Utf8SearchFacts.Create(
                                              Utf8SearchKind.ExactAsciiLiteral,
                                              new Utf8SearchFactData
                                              {
                                                  LiteralUtf8 = Encoding.UTF8.GetBytes("foo"),
                                                  CanGuideFallbackStarts = true,
                                                  LeadingBoundary = Utf8BoundaryRequirement.Boundary,
                                                  TrailingBoundary = Utf8BoundaryRequirement.Boundary,
                                              }));
        var input = Encoding.UTF8.GetBytes("xfood bar foo baz bar");
        var state = new Utf8StructuralSearchState(new PreparedSearchScanState(0, default), new PreparedWindowScanState(0, new PreparedSearchScanState(0, default)));

        Assert.True(plan.StructuralSearchPlan.TryFindNextCandidate(input, ref state, out var candidate));
        Assert.Equal("xfood bar ".Length, candidate.StartIndex);
        Assert.False(plan.StructuralSearchPlan.TryFindNextCandidate(input, ref state, out _));
    }

    [Fact]
    public void StructuralStartPlanIncludesTrailingLiteralStage()
    {
        var plan = Utf8SearchPlan.Prepare(Utf8SearchFacts.Create(
                                              Utf8SearchKind.ExactAsciiLiteral,
                                              new Utf8SearchFactData
                                              {
                                                  LiteralUtf8 = Encoding.UTF8.GetBytes("foo"),
                                                  CanGuideFallbackStarts = true,
                                                  TrailingLiteralUtf8 = Encoding.UTF8.GetBytes("bar"),
                                              }));

        Assert.Contains(
            plan.StructuralSearchPlan.Stages!,
            static stage => stage.Kind == Utf8StructuralSearchStageKind.RequireTrailingLiteral &&
                            stage.LiteralUtf8 is { Length: 3 });
    }

    [Fact]
    public void StructuralSearchPlanCanRejectCandidatesWithoutTrailingLiteral()
    {
        var plan = Utf8SearchPlan.Prepare(Utf8SearchFacts.Create(
                                              Utf8SearchKind.ExactAsciiLiteral,
                                              new Utf8SearchFactData
                                              {
                                                  LiteralUtf8 = Encoding.UTF8.GetBytes("foo"),
                                                  CanGuideFallbackStarts = true,
                                                  TrailingLiteralUtf8 = Encoding.UTF8.GetBytes("bar"),
                                              }));
        var input = Encoding.UTF8.GetBytes("fooqux foobar");
        var state = new Utf8StructuralSearchState(new PreparedSearchScanState(0, default), new PreparedWindowScanState(0, new PreparedSearchScanState(0, default)));

        Assert.True(plan.StructuralSearchPlan.TryFindNextCandidate(input, ref state, out var candidate));
        Assert.Equal("fooqux ".Length, candidate.StartIndex);
        Assert.False(plan.StructuralSearchPlan.TryFindNextCandidate(input, ref state, out _));
    }

    [Fact]
    public void StructuralStartPlanIncludesExactLengthStage()
    {
        var plan = Utf8SearchPlan.Prepare(Utf8SearchFacts.Create(
                                              Utf8SearchKind.ExactAsciiLiteral,
                                              new Utf8SearchFactData
                                              {
                                                  LiteralUtf8 = Encoding.UTF8.GetBytes("foo"),
                                                  CanGuideFallbackStarts = true,
                                                  ExactRequiredLength = 5,
                                              }));

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
        var plan = Utf8SearchPlan.Prepare(Utf8SearchFacts.Create(
                                              Utf8SearchKind.ExactAsciiLiteral,
                                              new Utf8SearchFactData
                                              {
                                                  LiteralUtf8 = Encoding.UTF8.GetBytes("foo"),
                                                  CanGuideFallbackStarts = true,
                                                  ExactRequiredLength = 5,
                                              }));
        var input = Encoding.UTF8.GetBytes("xxfoozzyy");
        var state = new Utf8StructuralSearchState(new PreparedSearchScanState(0, default), new PreparedWindowScanState(0, new PreparedSearchScanState(0, default)));

        Assert.True(plan.StructuralSearchPlan.TryFindNextCandidate(input, ref state, out var candidate));
        Assert.Equal(2, candidate.StartIndex);
        Assert.Equal(7, candidate.EndIndex);
    }

    [Fact]
    public void StructuralStartPlanIncludesMaxLengthBoundStage()
    {
        var plan = Utf8SearchPlan.Prepare(Utf8SearchFacts.Create(
                                              Utf8SearchKind.ExactAsciiLiteral,
                                              new Utf8SearchFactData
                                              {
                                                  LiteralUtf8 = Encoding.UTF8.GetBytes("foo"),
                                                  CanGuideFallbackStarts = true,
                                                  MaxPossibleLength = 6,
                                              }));

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
        var plan = Utf8SearchPlan.Prepare(Utf8SearchFacts.Create(
                                              Utf8SearchKind.ExactAsciiLiteral,
                                              new Utf8SearchFactData
                                              {
                                                  LiteralUtf8 = Encoding.UTF8.GetBytes("foo"),
                                                  CanGuideFallbackStarts = true,
                                                  MaxPossibleLength = 6,
                                              }));
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
    public void CandidatePortfolioCursorMergesSourcesByStartThenEnd()
    {
        var searcher = new PreparedSearcher(
            new PreparedSubstringSearch(Encoding.UTF8.GetBytes("a"), ignoreCase: false),
            ignoreCase: false);
        var plans = new[]
        {
            Utf8StructuralSearchPlan.CreateStartPlan(searcher).WithExactLength(5),
            Utf8StructuralSearchPlan.CreateStartPlan(searcher).WithExactLength(4),
        };
        var input = Encoding.UTF8.GetBytes("a---a----");

        using var cursor = new Utf8CandidatePortfolioCursor(plans, input, 0);

        Assert.True(cursor.TryGetNext(out var first));
        Assert.Equal(0, first.StartIndex);
        Assert.Equal(4, first.EndIndex);
        Assert.True(cursor.TryGetNext(out var second));
        Assert.Equal(0, second.StartIndex);
        Assert.Equal(5, second.EndIndex);
    }

    [Fact]
    public void CandidatePortfolioCursorRetainsMonotoneSourceState()
    {
        var dense = new PreparedSearcher(
            new PreparedSubstringSearch(Encoding.UTF8.GetBytes("a"), ignoreCase: false),
            ignoreCase: false);
        var far = new PreparedSearcher(
            new PreparedSubstringSearch(Encoding.UTF8.GetBytes("z"), ignoreCase: false),
            ignoreCase: false);
        var plans = new[]
        {
            Utf8StructuralSearchPlan.CreateStartPlan(dense),
            Utf8StructuralSearchPlan.CreateStartPlan(far),
        };
        var input = Encoding.UTF8.GetBytes(new string('a', 512) + "z");

        using var cursor = new Utf8CandidatePortfolioCursor(plans, input, 0);
        var count = 0;
        while (cursor.TryGetNext(out _))
        {
            count++;
        }

        Assert.Equal(513, count);
        Assert.Equal(count + plans.Length, cursor.SourceAdvanceCount);
    }

    [Fact]
    public void TwoSourceCandidatePortfolioCursorUsesInlineStorage()
    {
        var first = new PreparedSearcher(
            new PreparedSubstringSearch(Encoding.UTF8.GetBytes("a"), ignoreCase: false),
            ignoreCase: false);
        var second = new PreparedSearcher(
            new PreparedSubstringSearch(Encoding.UTF8.GetBytes("z"), ignoreCase: false),
            ignoreCase: false);
        var plans = new[]
        {
            Utf8StructuralSearchPlan.CreateStartPlan(first),
            Utf8StructuralSearchPlan.CreateStartPlan(second),
        };
        var input = Encoding.UTF8.GetBytes(new string('a', 128) + "z");
        using var cursor = new Utf8CandidatePortfolioCursor(plans, input, 0);
        var count = 0;
        while (cursor.TryGetNext(out _))
        {
            count++;
        }

        Assert.Equal(129, count);
        Assert.False(cursor.UsesPooledStorage);
    }

    [Fact]
    public void StructuralWindowPlanCanApplyBoundaryStages()
    {
        var plan = Utf8SearchPlan.Prepare(Utf8SearchFacts.Create(
                                              Utf8SearchKind.None,
                                              new Utf8SearchFactData
                                              {
                                                  OrderedWindowLeadingLiteralsUtf8 = [ Encoding.UTF8.GetBytes("foo"), ],
                                                  OrderedWindowTrailingLiteralUtf8 = Encoding.UTF8.GetBytes("bar"),
                                                  OrderedWindowMaxGap = 16,
                                                  OrderedWindowSameLine = true,
                                                  LeadingBoundary = Utf8BoundaryRequirement.Boundary,
                                                  TrailingBoundary = Utf8BoundaryRequirement.Boundary,
                                              }));
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

        var verifier = Utf8RegexPreparer.PrepareFallbackVerifier("using\\s+var.*await", RegexOptions.CultureInvariant, plan);

        Assert.True(verifier.RequiresCandidateEndCoverage);
        Assert.True(verifier.RequiresTrailingAnchorCoverage);
        Assert.Equal(Utf8FallbackVerifierMode.AnchoredSliceRegex, verifier.Mode);
    }

    [Fact]
    public void FallbackVerifierPlanUsesBoundedSliceModeForExactLengthStartPlans()
    {
        var plan = Utf8SearchPlan.Prepare(Utf8SearchFacts.Create(
                                              Utf8SearchKind.ExactAsciiLiteral,
                                              new Utf8SearchFactData
                                              {
                                                  LiteralUtf8 = Encoding.UTF8.GetBytes("foo"),
                                                  CanGuideFallbackStarts = true,
                                                  ExactRequiredLength = 5,
                                              })).StructuralSearchPlan;

        var verifier = Utf8RegexPreparer.PrepareFallbackVerifier("foo..", RegexOptions.CultureInvariant, plan);

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
    public void BoundedFallbackVerificationReusesOneDecodedUnicodeSubject()
    {
        var input = Encoding.UTF8.GetBytes("éfoofooéfoofoo");
        var validation = Utf8Validation.Validate(input);
        var plan = new Utf8FallbackVerifierPlan(
            Utf8FallbackVerifierMode.AnchoredSliceRegex,
            requiresCandidateEndCoverage: true,
            requiresTrailingAnchorCoverage: false);
        var verifier = Utf8FallbackVerifierRuntimeFactory.Create(
            plan,
            "(foo)\\1",
            RegexOptions.CultureInvariant,
            Regex.InfiniteMatchTimeout);
        Utf8BoundaryMap? map = null;
        string? decoded = null;

        Assert.True(verifier.TryVerify(
            input,
            Utf8StructuralCandidate.Start(2, 8, 6, 0),
            validation,
            ref map,
            ref decoded,
            out var first));
        var sharedDecoded = decoded;
        Assert.True(verifier.TryVerify(
            input,
            Utf8StructuralCandidate.Start(10, 16, 6, 0),
            validation,
            ref map,
            ref decoded,
            out var second));

        Assert.Same(sharedDecoded, decoded);
        Assert.Equal(2, first.IndexInBytes);
        Assert.Equal(10, second.IndexInBytes);
    }

    [Fact]
    public void FallbackVerifierPlanUsesBoundedSliceModeForMaxLengthStartPlans()
    {
        var plan = Utf8SearchPlan.Prepare(Utf8SearchFacts.Create(
                                              Utf8SearchKind.ExactAsciiLiteral,
                                              new Utf8SearchFactData
                                              {
                                                  LiteralUtf8 = Encoding.UTF8.GetBytes("foo"),
                                                  CanGuideFallbackStarts = true,
                                                  MaxPossibleLength = 6,
                                              })).StructuralSearchPlan;

        var verifier = Utf8RegexPreparer.PrepareFallbackVerifier("(foo)\\\\1?", RegexOptions.CultureInvariant, plan);

        Assert.True(plan.ProducesBoundedCandidates);
        Assert.False(verifier.RequiresCandidateEndCoverage);
        Assert.False(verifier.RequiresTrailingAnchorCoverage);
        Assert.Equal(Utf8FallbackVerifierMode.AnchoredSliceRegex, verifier.Mode);
    }

    [Fact]
    public void AnalyzerCanExtractFiniteOrderedWindowGap()
    {
        var analysis = Utf8FrontEnd.Compile(@"(?:using var|await using var)[A-Z]{1,3}await", RegexOptions.CultureInvariant);
        var searchFacts = Utf8FrontEndSearchAnalyzer.Analyze(analysis.SemanticRegex);

        Assert.Equal(Utf8SearchKind.FixedDistanceAsciiLiteral, searchFacts.Kind);
        Assert.Equal(3, searchFacts.OrderedWindowMaxGap);
        Assert.True(searchFacts.OrderedWindowSameLine);

        var searchPlan = Utf8SearchPlan.Prepare(Utf8SearchFacts.Create(
                                                    searchFacts.Kind,
                                                    new Utf8SearchFactData
                                                    {
                                                        LiteralUtf8 = searchFacts.LiteralUtf8,
                                                        AlternateLiteralsUtf8 = searchFacts.AlternateLiteralsUtf8,
                                                        CanGuideFallbackStarts = searchFacts.CanGuideFallbackStarts,
                                                        RequiredPrefilterLiteralUtf8 = searchFacts.RequiredPrefilterLiteralUtf8,
                                                        RequiredPrefilterAlternateLiteralsUtf8 = searchFacts.RequiredPrefilterAlternateLiteralsUtf8,
                                                        SecondaryRequiredPrefilterQuotedAsciiSet = searchFacts.SecondaryRequiredPrefilterQuotedAsciiSet,
                                                        SecondaryRequiredPrefilterQuotedAsciiLength = searchFacts.SecondaryRequiredPrefilterQuotedAsciiLength,
                                                        FixedDistanceSets = searchFacts.FixedDistanceSets,
                                                        TrailingLiteralUtf8 = searchFacts.TrailingLiteralUtf8,
                                                        OrderedWindowLeadingLiteralsUtf8 = searchFacts.OrderedWindowLeadingLiteralsUtf8,
                                                        OrderedWindowTrailingLiteralUtf8 = searchFacts.OrderedWindowTrailingLiteralUtf8,
                                                        RequiredWindowPrefilters = searchFacts.RequiredWindowPrefilters,
                                                        OrderedWindowMaxGap = searchFacts.OrderedWindowMaxGap,
                                                        OrderedWindowSameLine = searchFacts.OrderedWindowSameLine,
                                                        FallbackStartTransform = searchFacts.FallbackStartTransform,
                                                        Distance = searchFacts.Distance,
                                                        MinRequiredLength = searchFacts.MinRequiredLength,
                                                        ExactRequiredLength = searchFacts.ExactRequiredLength,
                                                        MaxPossibleLength = searchFacts.MaxPossibleLength,
                                                        LeadingBoundary = searchFacts.LeadingBoundary,
                                                        TrailingBoundary = searchFacts.TrailingBoundary,
                                                    }));
        Assert.Equal(3, searchPlan.OrderedWindowMaxGap);
        Assert.True(searchPlan.OrderedWindowSameLine);
    }

    [Fact]
    public void AnalyzerDoesNotInventLineBoundedWindowForNewlinePermittingGap()
    {
        var analysis = Utf8FrontEnd.Compile(@"foo\s+bar", RegexOptions.CultureInvariant);
        var searchFacts = Utf8FrontEndSearchAnalyzer.Analyze(analysis.SemanticRegex);

        Assert.False(searchFacts.OrderedWindowSameLine);
        Assert.Null(searchFacts.OrderedWindowMaxGap);
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

        Assert.Equal(Utf8SearchKind.FixedDistanceAsciiChar, regex.Inspection.SearchPlan.Kind);
        Assert.Equal("d", Encoding.UTF8.GetString(regex.Inspection.SearchPlan.LiteralUtf8!));
        Assert.Equal(5, regex.Inspection.SearchPlan.Distance);
        Assert.Equal(6, regex.Inspection.SearchPlan.MinRequiredLength);
    }

    [Fact]
    public void ConstructorCanStillCreateFixedDistanceLiteralSearchPlanWhenNoSetExists()
    {
        var regex = new Utf8Regex("ab..cd", RegexOptions.CultureInvariant);

        Assert.Equal(Utf8SearchKind.FixedDistanceAsciiChar, regex.Inspection.SearchPlan.Kind);
        Assert.Equal("d", Encoding.UTF8.GetString(regex.Inspection.SearchPlan.LiteralUtf8!));
        Assert.Equal(5, regex.Inspection.SearchPlan.Distance);
        Assert.Equal(6, regex.Inspection.SearchPlan.MinRequiredLength);
    }

    [Fact]
    public void ConstructorCanCreateTrailingFixedLengthAnchorSearchPlan()
    {
        var regex = new Utf8Regex(@"abc\z", RegexOptions.CultureInvariant);

        Assert.Equal(Utf8SearchKind.TrailingAnchorFixedLengthEnd, regex.Inspection.SearchPlan.Kind);
        Assert.Equal(3, regex.Inspection.SearchPlan.MinRequiredLength);
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
