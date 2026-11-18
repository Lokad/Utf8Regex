using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.FrontEnd;
using Lokad.Utf8Regex.Internal.Input;
using Lokad.Utf8Regex.Internal.Planning;
using Lokad.Utf8Regex.Internal.Search;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8SearchPlanTests
{

    [Fact]
    public void ConstructorCreatesExactLiteralSearchPlanForPlainAsciiLiteral()
    {
        var regex = new Utf8Regex("needle", options: RegexOptions.CultureInvariant);

        Assert.Equal(Utf8SearchKind.ExactAsciiLiteral, regex.Inspection.SearchPlan.Kind);
        Assert.True(regex.Inspection.SearchPlan.HasLiteral);
        Assert.True(regex.Inspection.SearchPlan.HasPreparedSearcher);
        Assert.Equal(PreparedSearcherKind.ExactLiteral, regex.Inspection.SearchPlan.PreparedSearcher.Kind);
        Assert.Equal(regex.Inspection.SearchPlan.PreparedSearcher.Kind, regex.Inspection.SearchPlan.PreparedSearcher.Kind);
    }

    [Fact]
    public void ConstructorCreatesIgnoreCaseSearchPlanForInvariantIgnoreCaseLiteral()
    {
        var regex = new Utf8Regex("needle", options: RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        Assert.Equal(Utf8SearchKind.AsciiFoldedByteLiteral, regex.Inspection.SearchPlan.Kind);
        Assert.True(regex.Inspection.SearchPlan.HasLiteral);
        Assert.Equal(PreparedSearcherKind.IgnoreCaseLiteral, regex.Inspection.SearchPlan.PreparedSearcher.Kind);
    }

    [Fact]
    public void ConstructorCreatesIgnoreCaseSearchPlanForInvariantIgnoreCaseLiteralAlternation()
    {
        var regex = new Utf8Regex("needle|thread|fiber", options: RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        Assert.Equal(Utf8SearchKind.AsciiFoldedByteLiterals, regex.Inspection.SearchPlan.Kind);
        Assert.Equal(NativeExecutionKind.AsciiLiteralIgnoreCaseLiterals, regex.Inspection.ExecutionKind);
        Assert.Equal(Utf8SearchPortfolioKind.AsciiIgnoreCaseFamily, regex.Inspection.SearchPortfolioKind);
        Assert.True(regex.Inspection.SearchPlan.HasAlternateLiterals);
        Assert.True(regex.Inspection.SearchPlan.AlternateIgnoreCaseLiteralSearch.HasValue);
        Assert.False(regex.Inspection.SearchPlan.AlternateLiteralSearch.HasValue);
        Assert.Equal(PreparedSearcherKind.MultiLiteral, regex.Inspection.SearchPlan.PreparedSearcher.Kind);

        var alternate = regex.Inspection.SearchPlan.AlternateIgnoreCaseLiteralSearch.Value;
        var prepared = regex.Inspection.SearchPlan.MultiLiteralSearch.IgnoreCaseSearch;
        // The value search shares its owned byte array and byte-set object.
        Assert.Equal(alternate.FirstByteSearch, prepared.FirstByteSearch);
        Assert.Same(alternate.Buckets, prepared.Buckets);
        Assert.Same(alternate.BucketIndexMap, prepared.BucketIndexMap);
    }

    [Fact]
    public void ScalarFallbackRetainsSearchFactsWithoutUnusedTreeGuards()
    {
        var regex = new Utf8Regex("a.*b", options: RegexOptions.CultureInvariant);

        Assert.Equal(Utf8SearchKind.ExactAsciiLiteral, regex.Inspection.SearchPlan.Kind);
        Assert.True(regex.Inspection.SearchPlan.HasLiteral);
        Assert.True(regex.Inspection.SearchPlan.HasFallbackCandidates);
        Assert.True(regex.Inspection.StructuralSearchPlan.HasValue);
        Assert.True(regex.Inspection.SearchPlan.HasStructuralCandidates);
        Assert.Equal(Utf8StructuralSearchYieldKind.Start, regex.Inspection.StructuralSearchPlan.YieldKind);
        Assert.Equal(Utf8StructuralSearchStageKind.FindLiteralFamily, regex.Inspection.StructuralSearchPlan.Stages![0].Kind);
        Assert.DoesNotContain(regex.Inspection.StructuralSearchPlan.Stages!, static stage => stage.Kind == Utf8StructuralSearchStageKind.RequireByteAtOffset);
        Assert.Equal(1, regex.Count("a😀b"u8));
        Assert.Equal(6, (regex.Match("a😀b"u8).EndOffsetInBytes - regex.Match("a😀b"u8).StartOffsetInBytes));
        Assert.Equal(Utf8StructuralSearchStageKind.YieldStart, regex.Inspection.StructuralSearchPlan.Stages![^1].Kind);
    }

    [Fact]
    public void ConstructorCanCreateAlternationPrefixSearchPlanForAsciiAlternation()
    {
        var regex = new Utf8Regex("cat|horse", options: RegexOptions.CultureInvariant);

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
        var regex = new Utf8Regex("café|niño", options: RegexOptions.CultureInvariant);

        Assert.Equal(Utf8SearchKind.ExactUtf8Literals, regex.Inspection.SearchPlan.Kind);
        Assert.True(regex.Inspection.SearchPlan.HasAlternateLiterals);
        Assert.True(regex.Inspection.SearchPlan.AlternateLiteralSearch.HasValue);
        Assert.Equal(["café", "niño"], regex.Inspection.SearchPlan.AlternateLiteralsUtf8!.Select(Encoding.UTF8.GetString));
    }

    [Fact]
    public void ConstructorCanCreateBoundaryWrappedAlternationSearchPlanForUtf8Alternation()
    {
        var regex = new Utf8Regex(@"\b(?:café|niño)\b", options: RegexOptions.CultureInvariant);

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
        var regex = new Utf8Regex(@"\b(?:Task|ValueTask|IAsyncEnumerable)\b", options: RegexOptions.CultureInvariant);

        Assert.Equal(Utf8SearchKind.ExactAsciiLiterals, regex.Inspection.SearchPlan.Kind);
        Assert.Equal(Utf8SearchPortfolioKind.ExactEarliestFamily, regex.Inspection.SearchPortfolioKind);
        Assert.Equal(PreparedMultiLiteralKind.ExactEarliest, regex.Inspection.SearchPlan.MultiLiteralSearch.Kind);
    }

    [Fact]
    public void StructuralSearchPlanCanFilterBoundaryWrappedUtf8AlternationCandidates()
    {
        var regex = new Utf8Regex(@"\b(?:café|niño)\b", options: RegexOptions.CultureInvariant);
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
        var regex = new Utf8Regex(@"\b(?:café|niño)\b", options: RegexOptions.CultureInvariant);
        var input = Encoding.UTF8.GetBytes("xcafé café niñoz niño");
        var expected = Encoding.UTF8.GetByteCount("xcafé café niñoz ");

        Assert.True(regex.Inspection.StructuralSearchPlan.TryFindLastCandidate(input, input.Length, out var candidate));
        Assert.Equal(expected, candidate.StartIndex);
    }

    [Fact]
    public void SearchExecutorUsesStructuralPlanForFilteredSingleLiteralSearch()
    {
        var regex = new Utf8Regex(@"\bfoo\b", options: RegexOptions.CultureInvariant);
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
                                                  TrailingBoundary = Utf8BoundaryRequirement.Boundary
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
        var analysis = Utf8FrontEnd.Analyze(pattern, RegexOptions.CultureInvariant);
        var family = Utf8OwnedRequiredLiterals.FindBestRequiredLiteralFamily(analysis.SemanticRegex.RuntimeTree!.Root);
        Assert.True(family is { Length: > 0 }, DumpNode(analysis.SemanticRegex.RuntimeTree.Root));

        var regex = new Utf8Regex(pattern, options: RegexOptions.CultureInvariant);

        // Neither foo nor bar is mandatory across both branches. The shared
        // covering credential family remains the required prefilter.
        Assert.Equal(Utf8SearchKind.None, regex.Inspection.SearchPlan.Kind);
        Assert.Equal(PreparedSearcherKind.MultiLiteral, regex.Inspection.SearchPlan.RequiredPrefilterSearcher.Kind);
        Assert.True(regex.Inspection.SearchPlan.PrefilterPlan.HasValue);
        Assert.Equal(["AIDA", "AKIA", "AROA", "ASIA"], regex.Inspection.SearchPlan.RequiredPrefilterAlternateLiteralsUtf8!.Select(Encoding.UTF8.GetString));
        foreach (var subject in new[] { "fooASIA" + new string('A', 56), new string('A', 40) + "barASIA" + new string('A', 16) })
        {
            var input = Encoding.UTF8.GetBytes(subject);
            Assert.True(regex.IsMatch(input));
            Assert.Equal(1, regex.Count(input));
            var match = regex.Match(input);
            Assert.True(match.Success);
            Assert.Equal((0, input.Length), (match.StartOffsetInBytes, (match.EndOffsetInBytes - match.StartOffsetInBytes)));
        }
    }

    [Fact]
    public void FallbackSearchPlanCarriesExplicitCandidateStageForFallbackPattern()
    {
        var regex = new Utf8Regex("a.*b", options: RegexOptions.CultureInvariant);

        Assert.True(regex.Inspection.SearchPlan.HasFallbackCandidates);
        Assert.NotNull(regex.Inspection.SearchPlan.FallbackCandidatePlans);
        Assert.Equal(Utf8CandidateSearchKind.StructuralSearchSet, regex.Inspection.SearchPlan.FallbackCandidateSource.Kind);
        Assert.Equal(1, regex.Count("a😀b"u8));
        Assert.Equal(6, (regex.Match("a😀b"u8).EndOffsetInBytes - regex.Match("a😀b"u8).StartOffsetInBytes));
    }

    [Fact]
    public void NativeSearchPlanCarriesExplicitCandidateSourceForExactLiteral()
    {
        var regex = new Utf8Regex("abc", options: RegexOptions.CultureInvariant);

        Assert.Equal(Utf8CandidateSearchKind.PreparedSearcher, regex.Inspection.SearchPlan.NativeCandidateSource.Kind);
        Assert.Equal(regex.Inspection.SearchPlan.PreparedSearcher.Kind, regex.Inspection.SearchPlan.NativeCandidateSource.PreparedSearcher.Kind);
        Assert.Equal(Utf8SearchOperationKind.DirectSearch, regex.Inspection.SearchPlan.CountOperation.Kind);
    }

    [Fact]
    public void SearchPlanCarriesHybridStrategyForLargeAutomatonLiteralFamilies()
    {
        var regex = new Utf8Regex("Sherlock Holmes|John Watson|Irene Adler|Inspector Lestrade|Professor Moriarty", options: RegexOptions.CultureInvariant);

        Assert.Equal(Utf8SearchOperationKind.HybridSearch, regex.Inspection.SearchPlan.CountOperation.Kind);
        Assert.Equal(Utf8SearchOperationKind.HybridSearch, regex.Inspection.SearchPlan.FirstMatchOperation.Kind);
        Assert.Equal(Utf8CandidateSearchKind.PreparedSearcher, regex.Inspection.SearchPlan.CountOperation.CandidateSource.Kind);
        Assert.Equal(Utf8SearchObservabilityKind.Effectiveness, regex.Inspection.SearchPlan.CountOperation.ObservabilityKind);
    }

    [Fact]
    public void SearchPlanCarriesConfirmationAndProjectionPlans()
    {
        var boundaryRegex = new Utf8Regex(@"\b(?:Task|ValueTask|IAsyncEnumerable)\b", options: RegexOptions.CultureInvariant);
        var fallbackRegex = new Utf8Regex("a.*b", options: RegexOptions.CultureInvariant);

        Assert.Equal(Utf8ConfirmationKind.BoundaryRequirements, boundaryRegex.Inspection.SearchPlan.ConfirmationPlan.Kind);
        Assert.Equal(Utf8ConfirmationKind.None, fallbackRegex.Inspection.SearchPlan.ConfirmationPlan.Kind);
    }

    [Fact]
    public void SearchPlanCarriesExecutablePipelines()
    {
        var regex = new Utf8Regex(@"\b(?:Task|ValueTask|IAsyncEnumerable)\b", options: RegexOptions.CultureInvariant);
        var fallbackRegex = new Utf8Regex("a.*b", options: RegexOptions.CultureInvariant);

        Assert.Equal(Utf8SearchOperationKind.SearchThenConfirm, regex.Inspection.SearchPlan.EnumerationOperation.Kind);
        Assert.Equal(Utf8ConfirmationKind.BoundaryRequirements, regex.Inspection.SearchPlan.EnumerationOperation.Confirmation.Kind);

        Assert.Equal(Utf8SearchOperationKind.PrefilterThenSearch, fallbackRegex.Inspection.SearchPlan.FirstMatchOperation.Kind);
        Assert.Equal(Utf8SearchOperationKind.PrefilterThenSearch, fallbackRegex.Inspection.SearchPlan.CountOperation.Kind);
        Assert.Equal(Utf8ConfirmationKind.None, fallbackRegex.Inspection.SearchPlan.FirstMatchOperation.Confirmation.Kind);
    }

    [Fact]
    public void SearchPlanCarriesBackendInstructionPrograms()
    {
        var regex = new Utf8Regex(@"\b(?:Task|ValueTask|IAsyncEnumerable)\b", options: RegexOptions.CultureInvariant);
        var fallbackRegex = new Utf8Regex("a.*b", options: RegexOptions.CultureInvariant);

        Assert.True(regex.Inspection.SearchPlan.EnumerationOperation.CandidateSource.HasValue);
        Assert.True(regex.Inspection.SearchPlan.EnumerationOperation.Confirmation.HasValue);

        Assert.True(fallbackRegex.Inspection.SearchPlan.FirstMatchOperation.CandidateSource.HasValue);
        Assert.False(fallbackRegex.Inspection.SearchPlan.FirstMatchOperation.Confirmation.HasValue);
    }

    [Fact]
    public void CompiledFamiliesUseSelectedRuntimesAndNativeKernels()
    {
        const RegexOptions options = RegexOptions.CultureInvariant | RegexOptions.Compiled;
        var literal = new Utf8Regex(@"\b(?:Task|ValueTask|IAsyncEnumerable)\b", options: options);
        var fallback = new Utf8Regex("a.*b", options: options);
        var identifier = new Utf8Regex(@"\b(?:record|struct|class)\s+[A-Z][A-Za-z0-9_]+", options: options);
        var window = new Utf8Regex(@"\bpublic\s+async\b", options: options);

        Assert.Equal(Utf8CompiledEngineKind.LiteralFamily, literal.Inspection.CompiledEngineKind);
        Assert.Equal(Utf8CompiledEngineKind.FallbackRegex, fallback.Inspection.CompiledEngineKind);
        Assert.Equal(1, literal.Count("Task"u8));
        Assert.Equal(1, fallback.Count("axb"u8));
        Assert.Equal(1, identifier.Count("class Bravo"u8));
        Assert.Equal(1, window.Count("public async"u8));
    }

    [Fact]
    public void RegexPlanCarriesExplicitPrimaryExecutionEngineKinds()
    {
        var literalFamily = new Utf8Regex(@"\b(?:Task|ValueTask|IAsyncEnumerable)\b", options: RegexOptions.CultureInvariant);
        var fallback = Utf8FrontEnd.Compile("a.*b", RegexOptions.CultureInvariant);
        var structural = Utf8FrontEnd.Compile("ab[0-9][0-9]cd", RegexOptions.CultureInvariant);
        var orderedWindow = Utf8FrontEnd.Compile(@"\b(?:using\s+var|await\s+using\s+var)\s+[A-Za-z_][A-Za-z0-9_]*\s*=\s*await\b", RegexOptions.CultureInvariant);

        Assert.Equal(Utf8CandidateSearchKind.PreparedSearcher, literalFamily.Inspection.SearchPlan.NativeCandidateSource.Kind);
        Assert.Equal(Utf8CandidateSearchKind.StructuralSearchSet, fallback.SearchPlan.FallbackCandidateSource.Kind);
        Assert.Equal(Utf8CompiledEngineKind.LiteralFamily, Utf8CompiledEngineSelector.Select(literalFamily.Inspection.PreparedRegex).Kind);
        Assert.Equal(Utf8CompiledEngineKind.FallbackRegex, Utf8CompiledEngineSelector.Select(fallback).Kind);
    }

    [Fact]
    public void EmittedLiteralFamilyCounterMatchesInstructionExecutor()
    {
        var regex = new Utf8Regex(@"\b(?:Task|ValueTask|IAsyncEnumerable)\b", options: RegexOptions.CultureInvariant);
        var input = Encoding.UTF8.GetBytes("Task task ValueTask IAsyncEnumerableX IAsyncEnumerable");

        Assert.True(Utf8EmittedLiteralFamilyCounter.TryCreate(regex.Inspection.SearchPlan, regex.Inspection.SearchPlan.CountOperation, regex.Inspection.SearchPlan.FirstMatchOperation, out var counter));
        Assert.NotNull(counter);
        Assert.Equal(
            Utf8BackendInstructionExecutor.CountLiteralFamily(regex.Inspection.SearchPlan, regex.Inspection.SearchPlan.CountOperation, input, budget: Utf8ExecutionDeadline.Infinite),
            counter!.Count(input));
    }

    [Fact]
    public void EmittedLiteralFamilyCounterSupportsSharedPrefixBoundaries()
    {
        var regex = new Utf8Regex(@"\b(?:LogTrace|LogDebug|LogInformation|LogWarning|LogError)\b", options: RegexOptions.CultureInvariant);
        var plan = regex.Inspection.SearchPlan;
        var input = Encoding.UTF8.GetBytes("LogTrace xLogError LogWarning_ LogDebug");

        Assert.Equal(Utf8SearchKind.ExactAsciiLiterals, plan.Kind);
        Assert.True(Utf8EmittedLiteralFamilyCounter.CanCreate(plan, plan.CountOperation, plan.FirstMatchOperation));
        Assert.True(Utf8EmittedLiteralFamilyCounter.TryCreate(plan, plan.CountOperation, plan.FirstMatchOperation, out var counter));
        Assert.NotNull(counter);
        Assert.Equal(2, counter!.Count(input));
    }

    [Fact]
    public void EmittedLiteralFamilyCounterMatchesInstructionExecutorForIsMatch()
    {
        var regex = new Utf8Regex(@"\b(?:Task|ValueTask|IAsyncEnumerable)\b", options: RegexOptions.CultureInvariant);
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
        var regex = new Utf8Regex(@"\b(?:Task|ValueTask|IAsyncEnumerable)\b", options: RegexOptions.CultureInvariant);
        var hit = Encoding.UTF8.GetBytes("Task task ValueTask IAsyncEnumerableX IAsyncEnumerable");
        var miss = Encoding.UTF8.GetBytes("task IAsyncEnumerableX stream");

        Assert.True(Utf8EmittedLiteralFamilyCounter.TryCreate(regex.Inspection.SearchPlan, regex.Inspection.SearchPlan.CountOperation, regex.Inspection.SearchPlan.FirstMatchOperation, out var counter));
        Assert.NotNull(counter);

        Assert.True(counter!.TryMatch(hit, out var emittedIndex, out var emittedLength));
        var interpreted = Utf8BackendInstructionExecutor.MatchLiteralFamily(regex.Inspection.SearchPlan, regex.Inspection.SearchPlan.FirstMatchOperation, hit, budget: Utf8ExecutionDeadline.Infinite, rightToLeft: false);
        Assert.True(interpreted.Success);
        Assert.Equal(interpreted.StartOffsetInBytes, emittedIndex);
        Assert.Equal((interpreted.EndOffsetInBytes - interpreted.StartOffsetInBytes), emittedLength);

        Assert.False(counter.TryMatch(miss, out emittedIndex, out emittedLength));
        Assert.Equal(-1, emittedIndex);
        Assert.Equal(0, emittedLength);
    }

    [Fact]
    public void EmittedUnconstrainedExactLiteralFamilyMatchesInstructionExecutor()
    {
        var regex = new Utf8Regex("alpha|bravo|charlie|delta|echo", options: RegexOptions.CultureInvariant);
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
        var interpreted = Utf8BackendInstructionExecutor.MatchLiteralFamily(plan, plan.FirstMatchOperation, input, budget: Utf8ExecutionDeadline.Infinite, rightToLeft: false);
        Assert.True(interpreted.Success);
        Assert.Equal(interpreted.StartOffsetInBytes, emittedIndex);
        Assert.Equal((interpreted.EndOffsetInBytes - interpreted.StartOffsetInBytes), emittedLength);
    }

    [Fact]
    public void UnconstrainedIgnoreCaseLiteralFamilyUsesPreparedInstructionLoop()
    {
        var regex = new Utf8Regex(            "Sherlock Holmes|John Watson|Irene Adler|Inspector Lestrade|Professor Moriarty", options:             RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);
        var plan = regex.Inspection.SearchPlan;

        Assert.Equal(Utf8ConfirmationKind.None, plan.CountOperation.Confirmation.Kind);
        Assert.Equal(Utf8ConfirmationKind.None, plan.FirstMatchOperation.Confirmation.Kind);
        Assert.Equal(Utf8CompiledExecutionBackend.InterpretedInstruction, regex.Inspection.CompiledExecutionBackend);
        Assert.False(Utf8EmittedLiteralFamilyCounter.TryCreate(plan, plan.CountOperation, plan.FirstMatchOperation, out _));
    }

    [Fact]
    public void SearchGuidedOwnedRuntimeMatchesTheOriginalHitAndMissSubjects()
    {
        var regex = new Utf8Regex("a.*b", options: RegexOptions.CultureInvariant);
        var analysis = Utf8FrontEnd.Compile("a.*b", RegexOptions.CultureInvariant);
        var hit = Encoding.UTF8.GetBytes("zzza123bzzz");
        var miss = Encoding.UTF8.GetBytes("zzza123czzz");
        Assert.True(regex.IsMatch(hit));
        Assert.Equal(1, regex.Count(hit));
        Assert.False(regex.IsMatch(miss));
        Assert.Equal(0, regex.Count(miss));
    }

    [Fact]
    public void ProductionSearchCanCountAndMatchLiteralFamilies()
    {
        var regex = new Utf8Regex(@"\b(?:Task|ValueTask|IAsyncEnumerable)\b", options: RegexOptions.CultureInvariant);
        var input = Encoding.UTF8.GetBytes("Task task ValueTask IAsyncEnumerableX IAsyncEnumerable");

        var plan = regex.Inspection.SearchPlan;
        Assert.Equal(3, Utf8BackendInstructionExecutor.CountLiteralFamily(plan, plan.CountOperation,
            input, budget: Utf8ExecutionDeadline.Infinite));
        Assert.True(Utf8BackendInstructionExecutor.IsMatchLiteralFamily(plan, plan.FirstMatchOperation,
            input, budget: Utf8ExecutionDeadline.Infinite, rightToLeft: false));
    }

    [Fact]
    public void QuotedRelationsCanCountRepeatedForwardWindows()
    {
        const string pattern = "(('|\")((?:ASIA|AKIA|AROA|AIDA)([A-Z0-7]{16}))('|\").*?(\\n^.*?){0,4}(('|\")[a-zA-Z0-9+/]{40}('|\"))+|('|\")[a-zA-Z0-9+/]{40}('|\").*?(\\n^.*?){0,3}('|\")((?:ASIA|AKIA|AROA|AIDA)([A-Z0-7]{16}))('|\"))+";
        const string input = "\"AIDAABCDEFGHIJKLMNOP\"\nctx = 1\nctx = 2\n\"aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa\"\n\n\"AIDAABCDEFGHIJKLMNOP\"\nctx = 3\nctx = 4\n\"aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa\"\n";
        var regex = new Utf8Regex(pattern, options: RegexOptions.Multiline);
        var bytes = Encoding.UTF8.GetBytes(input);
        Assert.Equal(2, regex.Count(bytes));

        var match = regex.Match(bytes);
        Assert.True(match.Success);
        Assert.Equal(0, match.StartOffsetInBytes);
    }

    [Fact]
    public void RuffNoqaRealPatternRetainsScalarSearchFacts()
    {
        const string pattern = "(\\s*)((?:# [Nn][Oo][Qq][Aa])(?::\\s?(([A-Z]+[0-9]+(?:[,\\s]+)?)+))?)";
        var scalarAnalysis = Utf8FrontEnd.Compile(pattern, RegexOptions.None);
        // Unicode whitespace does not certify the six-byte ASCII inverse
        // transform. Keep the original grammar and qualify its scalar route.
        Assert.Equal(Utf8SearchKind.None, scalarAnalysis.SearchPlan.Kind);
        Assert.False(scalarAnalysis.SearchPlan.HasFallbackCandidates);
        Assert.False(scalarAnalysis.SearchPlan.StructuralSearchPlan.HasValue);
        Assert.Equal(Utf8FallbackStartTransformKind.None, scalarAnalysis.SearchPlan.FallbackStartTransform.Kind);
        Assert.Null(scalarAnalysis.SearchPlan.FixedDistanceSets);
        var original = new Utf8Regex(pattern);
        Assert.NotNull(original.Inspection.PreparedRegex.ScalarProgram);
        // The same structural assertions remain for the complete ASCII control.
        var analysis = Utf8FrontEnd.Compile(pattern.Replace("\\s", "[\\t-\\r ]", StringComparison.Ordinal), RegexOptions.None);

        Assert.Equal(NativeExecutionKind.FallbackRegex, analysis.ExecutionKind);
        Assert.Equal(Utf8SearchKind.FixedDistanceAsciiSets, analysis.SearchPlan.Kind);
        Assert.False(analysis.SearchPlan.HasWindowSearch);
        Assert.Equal(Utf8CompiledEngineKind.FallbackRegex, Utf8CompiledEngineSelector.Select(analysis).Kind);
        Assert.True(analysis.SearchPlan.HasFallbackCandidates);
        Assert.True(analysis.SearchPlan.StructuralSearchPlan.HasValue);
        Assert.Equal(Utf8StructuralSearchYieldKind.Start, analysis.SearchPlan.StructuralSearchPlan.YieldKind);
        Assert.Equal(Utf8StructuralSearchStageKind.FindAscii, analysis.SearchPlan.StructuralSearchPlan.Stages![0].Kind);
        Assert.Contains(analysis.SearchPlan.StructuralSearchPlan.Stages!, static stage => stage.Kind == Utf8StructuralSearchStageKind.TransformCandidateStart);
        Assert.Equal(Utf8StructuralSearchStageKind.YieldStart, analysis.SearchPlan.StructuralSearchPlan.Stages![^1].Kind);
        Assert.Equal(Utf8FallbackStartTransformKind.TrimLeadingAsciiWhitespace, analysis.SearchPlan.FallbackStartTransform.Kind);
        Assert.NotNull(analysis.SearchPlan.FixedDistanceSets);
        Assert.True(analysis.SearchPlan.FixedDistanceSets!.Length >= 6);
        Assert.True(analysis.SearchPlan.FixedDistanceSets[0].Distance >= 2);
        var primaryBytes = analysis.SearchPlan.FixedDistanceSets[0].ByteSet.GetPositiveMatchBytes();
        Assert.Contains(primaryBytes, static value => value is (byte)'A' or (byte)'N' or (byte)'O' or (byte)'Q' or (byte)'a' or (byte)'n' or (byte)'o' or (byte)'q');
    }

    [Fact]
    public void RuffNoqaTweakedPatternUsesScalar()
    {
        const string pattern = "(?:# [Nn][Oo][Qq][Aa])(?::\\s?(([A-Z]+[0-9]+(?:[,\\s]+)?)+))?";
        var analysis = Utf8FrontEnd.Compile(pattern, RegexOptions.None);

        Assert.Equal(NativeExecutionKind.FallbackRegex, analysis.ExecutionKind);
        Assert.Equal(Utf8CompiledEngineKind.FallbackRegex, Utf8CompiledEngineSelector.Select(analysis).Kind);
    }

    [Fact]
    public void VariableGapRequiredLiteralRetainsSearchFacts()
    {
        const string pattern = "[A-Za-z]{10}\\s+[\\s\\S]{0,100}Result[\\s\\S]{0,100}\\s+[A-Za-z]{10}";
        var analysis = Utf8FrontEnd.Compile(pattern, RegexOptions.None);
        Assert.Equal(Utf8SearchKind.ExactAsciiLiteral, analysis.SearchPlan.Kind);
    }

    [Fact]
    public void StructuralSearchPlanCanProduceFallbackStartCandidates()
    {
        var regex = new Utf8Regex("a.*b", options: RegexOptions.CultureInvariant);
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
        var scalarAnalysis = Utf8FrontEnd.Compile(pattern, RegexOptions.None);
        Assert.False(scalarAnalysis.SearchPlan.StructuralSearchPlan.HasValue);
        foreach (var subject in new[] { "   # NOQA: ABC123", "\u00a0# NOQA: ABC123", "\u2003# NOQA: ABC123" })
        foreach (var mode in new[] { RegexOptions.None, RegexOptions.Compiled, RegexOptions.RightToLeft, RegexOptions.Compiled | RegexOptions.RightToLeft })
        {
            var regex = new Utf8Regex(pattern, options: mode);
            var bytes = Encoding.UTF8.GetBytes(subject);
            Assert.True(regex.IsMatch(bytes));
            Assert.Equal(1, regex.Count(bytes));
            var match = regex.Match(bytes);
                Assert.Equal((0, bytes.Length), (match.StartOffsetInBytes, (match.EndOffsetInBytes - match.StartOffsetInBytes)));
                var detailed = Utf8MatchSequence.First(regex.CreateMatchCursor(bytes));
                Assert.Equal((0, bytes.Length), (detailed.Value.StartOffsetInBytes, (detailed.Value.EndOffsetInBytes - detailed.Value.StartOffsetInBytes)));
            }
        var analysis = Utf8FrontEnd.Compile(pattern.Replace("\\s", "[\\t-\\r ]", StringComparison.Ordinal), RegexOptions.None);
        var input = Encoding.UTF8.GetBytes("   # NOQA: ABC123");
        var state = new Utf8StructuralSearchState(new PreparedSearchScanState(0, default), new PreparedWindowScanState(0, new PreparedSearchScanState(0, default)));

        Assert.True(analysis.SearchPlan.StructuralSearchPlan.TryFindNextCandidate(input, ref state, out var candidate));
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
                                                  TrailingBoundary = Utf8BoundaryRequirement.Boundary
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
                                                  TrailingLiteralUtf8 = Encoding.UTF8.GetBytes("bar")
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
                                                  TrailingLiteralUtf8 = Encoding.UTF8.GetBytes("bar")
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
                                                  ExactRequiredLength = 5
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
                                                  ExactRequiredLength = 5
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
                                                  MaxPossibleLength = 6
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
                                                  MaxPossibleLength = 6
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
    public void StructuralWindowPlanCanApplyBoundaryStages()
    {
        var plan = Utf8SearchPlan.Prepare(Utf8SearchFacts.Create(
                                              Utf8SearchKind.None,
                                              new Utf8SearchFactData
                                              {
                                                  OrderedWindowLeadingLiteralsUtf8 = [Encoding.UTF8.GetBytes("foo"),],
                                                  OrderedWindowTrailingLiteralUtf8 = Encoding.UTF8.GetBytes("bar"),
                                                  OrderedWindowMaxGap = 16,
                                                  OrderedWindowSameLine = true,
                                                  LeadingBoundary = Utf8BoundaryRequirement.Boundary,
                                                  TrailingBoundary = Utf8BoundaryRequirement.Boundary
                                              }));
        var input = Encoding.UTF8.GetBytes("xfoo bar foo barz foo bar");
        var state = new Utf8StructuralSearchState(default, new PreparedWindowScanState(0, new PreparedSearchScanState(0, default)));

        Assert.True(plan.StructuralSearchPlan.TryFindNextCandidate(input, ref state, out var candidate));
        Assert.Equal(Encoding.UTF8.GetByteCount("xfoo bar foo barz "), candidate.StartIndex);
        Assert.False(plan.StructuralSearchPlan.TryFindNextCandidate(input, ref state, out _));
    }

    [Fact]
    public void ExactLengthFallbackSliceVerificationRequiresFullCandidateCoverage()
    {
        var regex = new Utf8Regex("(foo)\\1", options: RegexOptions.CultureInvariant);

        Assert.True(regex.IsMatch(Encoding.UTF8.GetBytes("xxfoofooyy")));
        Assert.False(regex.IsMatch(Encoding.UTF8.GetBytes("xxfooxxxyy")));
    }

    [Theory]
    [InlineData("^ab", "xxab")]
    [InlineData("^ab", "xxabxxab")]
    [InlineData("^a", "xa")]
    [InlineData("\\Aab", "xab")]
    [InlineData("^ab", "abxx")]
    [InlineData("\\Aab", "abxx")]
    [InlineData("(?m:^ab)", "ab")]
    [InlineData("(?m:^ab)", "x\nab")]
    public void StartAnchoredCandidatesVerifyAgainstTheTrueStringStart(string pattern, string inputText)
    {
        // Bounded slices rebase ^ and \\A to the slice start; verification must use
        // the full-tail anchored mode so misses away from the string start stay misses.
        var input = Encoding.UTF8.GetBytes(inputText);
        var oracle = new Regex(pattern, RegexOptions.CultureInvariant, Regex.InfiniteMatchTimeout);
        var regex = new Utf8Regex(pattern, options: RegexOptions.CultureInvariant);

        Assert.Equal(oracle.IsMatch(inputText), regex.IsMatch(input));
        Assert.Equal(oracle.Matches(inputText).Count, regex.Count(input));
        var expected = oracle.Match(inputText);
        var actual = regex.Match(input);
        Assert.Equal(expected.Success, actual.Success);
        if (expected.Success)
        {
            Assert.Equal(System.Text.Encoding.UTF8.GetByteCount(inputText.AsSpan(0, expected.Index)), actual.StartOffsetInBytes);
            Assert.Equal(System.Text.Encoding.UTF8.GetByteCount(inputText.AsSpan(expected.Index, expected.Length)), (actual.EndOffsetInBytes - actual.StartOffsetInBytes));
        }

        var expectedDetailed = oracle.Match(inputText);
        var actualDetailed = Utf8MatchSequence.First(regex.CreateMatchCursor(input));
        Assert.Equal(expectedDetailed.Success, actualDetailed.Success);
    }

    [Fact]
    public void OffsetMatchingReusesOnePreparedUnicodeProgram()
    {
        var input = Encoding.UTF8.GetBytes("éfoofooéfoofoo");
        var regex = new Utf8Regex("(foo)\\1", options: RegexOptions.CultureInvariant);
        var first = regex.Match(input);
        var second = regex.Match(input, first.StartOffsetInBytes + (first.EndOffsetInBytes - first.StartOffsetInBytes), []);
        Assert.Equal((2, 6), (first.StartOffsetInBytes, (first.EndOffsetInBytes - first.StartOffsetInBytes)));
        Assert.Equal((10, 6), (second.StartOffsetInBytes, (second.EndOffsetInBytes - second.StartOffsetInBytes)));
    }

    [Fact]
    public void AnalyzerCanExtractFiniteOrderedWindowGap()
    {
        var analysis = Utf8FrontEnd.Analyze(@"(?:using var|await using var)[A-Z]{1,3}await", RegexOptions.CultureInvariant);
        var searchFacts = Utf8FrontEndSearchAnalyzer.Analyze(analysis.SemanticRegex);

        Assert.Equal(Utf8SearchKind.ExactAsciiLiterals, searchFacts.Kind);
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
                                                        TrailingBoundary = searchFacts.TrailingBoundary
                                                    }));
        Assert.Equal(3, searchPlan.OrderedWindowMaxGap);
        Assert.True(searchPlan.OrderedWindowSameLine);
    }

    [Fact]
    public void AnalyzerDoesNotInventLineBoundedWindowForNewlinePermittingGap()
    {
        var analysis = Utf8FrontEnd.Analyze(@"foo\s+bar", RegexOptions.CultureInvariant);
        var searchFacts = Utf8FrontEndSearchAnalyzer.Analyze(analysis.SemanticRegex);

        Assert.False(searchFacts.OrderedWindowSameLine);
        Assert.Null(searchFacts.OrderedWindowMaxGap);
    }

    private static string DumpNode(Utf8TreeNode node, int depth = 0)
    {
        var indent = new string(' ', depth * 2);
        var line = $"{indent}{node.Kind} ch={node.Ch} str={node.Str ?? "<null>"} m={node.M} n={node.N} children={node.ChildCount}";
        if (node.ChildCount == 0)
        {
            return line;
        }

        return line + Environment.NewLine + string.Join(Environment.NewLine,
            Enumerable.Range(0, node.ChildCount).Select(index => DumpNode(node.Child(index), depth + 1)));
    }

    [Fact]
    public void ConstructorCanCreateTrailingFixedLengthAnchorSearchPlan()
    {
        var regex = new Utf8Regex(@"abc\z", options: RegexOptions.CultureInvariant);

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
        var first = search.IndexOf(input);
        var last = search.LastIndexOf(input);

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
        var first = search.IndexOf(input);
        var last = search.LastIndexOf(input);

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
        Assert.Equal(16, search.IndexOf(input));
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
        Assert.Equal(2, search.IndexOf(input));
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
        var first = search.IndexOf(input);
        var last = search.LastIndexOf(input);

        Assert.Equal("beacon", Encoding.UTF8.GetString(input.AsSpan(first, 6)));
        Assert.Equal("needle", Encoding.UTF8.GetString(input.AsSpan(last, 6)));
        Assert.True(search.TryGetMatchedLiteralLength(input, first, out var matchedLength));
        Assert.Equal(6, matchedLength);
    }
}
