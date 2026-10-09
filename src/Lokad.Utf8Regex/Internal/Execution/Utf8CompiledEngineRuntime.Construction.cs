using Lokad.Utf8Regex.Internal.Planning;
using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex.Internal.Execution;

// Transfer state covers every runtime field. Referenced plans remain borrowed
// from their admitted owners. Dynamic emission and opaque token-search payloads
// remain separate admission debt; this slice admits the runtime objects.

internal sealed partial class Utf8StructuralFamilyCompiledEngineRuntime
{
    private readonly record struct ConstructionState(
        Utf8PreparedRegex RegexPlan,
        Utf8VerifierRuntime VerifierRuntime);

    internal static Utf8StructuralFamilyCompiledEngineRuntime Create(Utf8PreparedRegex plan, Utf8VerifierRuntime verifier)
    {
        using var scope = Utf8ConstructionScope.Enter();
        var budget = scope.Budget;
        budget.EnterDepth();
        try
        {
            var regexPlan = plan;
            var verifierRuntime = verifier;
            var result = budget.AllocateObject(new ConstructionState(regexPlan, verifierRuntime),
                static state => new Utf8StructuralFamilyCompiledEngineRuntime(state));
            scope.Complete();
            return result;
        }
        finally { budget.LeaveDepth(); }
    }
}

internal sealed partial class Utf8SimplePatternCompiledEngineRuntime
{
    private readonly record struct ConstructionState(
        Utf8PreparedRegex RegexPlan,
        Utf8StructuralLinearRuntime LinearRuntime,
        Utf8VerifierRuntime VerifierRuntime,
        Utf8CompiledPatternFamilyPlan CompiledPatternFamily,
        bool CanUseDirectAnchoredValidatorMatch,
        bool AnchoredValidatorAllowsTrailingNewline,
        AsciiSimplePatternAnchoredBoundedDatePlan AnchoredBoundedDatePlan,
        AsciiSimplePatternRepeatedDigitGroupPlan RepeatedDigitGroupPlan,
        Utf8EmittedWholeMatcher? EmittedWholeMatcher,
        AsciiSimplePatternBoundedSuffixLiteralPlan BoundedSuffixLiteralPlan,
        AsciiSimplePatternSymmetricLiteralWindowPlan SymmetricLiteralWindowPlan,
        Utf8CompiledSymmetricLiteralWindowCounter? CompiledSymmetricLiteralWindowCounter);

    internal static Utf8SimplePatternCompiledEngineRuntime Create(Utf8PreparedRegex plan, Utf8VerifierRuntime verifier, bool emit)
    {
        using var scope = Utf8ConstructionScope.Enter();
        var budget = scope.Budget;
        budget.EnterDepth();
        try
        {
            Utf8EmittedWholeMatcher? emittedWholeMatcher;
            var regexPlan = plan;
            var linearRuntime = Utf8StructuralLinearRuntime.Create(regexPlan);
            var verifierRuntime = verifier;
            var compiledPatternFamily = regexPlan.SimplePatternPlan.CompiledPatternFamily;
            var canUseDirectAnchoredValidatorMatch =
                compiledPatternFamily.Kind == Utf8CompiledPatternFamilyKind.AnchoredValidator ||
                compiledPatternFamily.Kind == Utf8CompiledPatternFamilyKind.AnchoredBoundedDate;
            var anchoredValidatorAllowsTrailingNewline = canUseDirectAnchoredValidatorMatch &&
                regexPlan.SimplePatternPlan.AllowsTrailingNewlineBeforeEnd;
            var anchoredBoundedDatePlan = compiledPatternFamily.AnchoredBoundedDatePlan;
            var repeatedDigitGroupPlan = compiledPatternFamily.RepeatedDigitGroupPlan;
            var boundedSuffixLiteralPlan = compiledPatternFamily.BoundedSuffixLiteralPlan;
            var symmetricLiteralWindowPlan = compiledPatternFamily.SymmetricLiteralWindowPlan;
            var compiledSymmetricLiteralWindowCounter = emit && symmetricLiteralWindowPlan.HasValue
                ? Utf8CompiledSymmetricLiteralWindowCounter.Create(symmetricLiteralWindowPlan)
                : null;
            var anchoredValidatorPlan = compiledPatternFamily.AnchoredValidatorPlan;
            if (!emit)
            {
                emittedWholeMatcher = null;
            }
            else if (anchoredBoundedDatePlan.HasValue)
            {
                emittedWholeMatcher = Utf8EmittedWholeMatcher.TryCreate(
                    anchoredBoundedDatePlan,
                    anchoredValidatorAllowsTrailingNewline,
                    out var boundedDateMatcher)
                    ? boundedDateMatcher
                    : null;
            }
            else if (regexPlan.SimplePatternPlan.AnchoredOptionalFieldPlan.HasValue)
            {
                emittedWholeMatcher = Utf8EmittedWholeMatcher.TryCreate(
                    regexPlan.SimplePatternPlan.AnchoredOptionalFieldPlan,
                    anchoredValidatorAllowsTrailingNewline,
                    out var optionalFieldMatcher)
                    ? optionalFieldMatcher
                    : null;
            }
            else if (repeatedDigitGroupPlan.HasValue)
            {
                emittedWholeMatcher = Utf8EmittedWholeMatcher.TryCreate(
                    repeatedDigitGroupPlan,
                    out var repeatedDigitGroupMatcher)
                    ? repeatedDigitGroupMatcher
                    : null;
            }
            else
            {
                emittedWholeMatcher =
                    Utf8SimplePatternCompiledRuntimePolicy.ShouldUseEmittedAnchoredValidator(anchoredValidatorPlan) &&
                    Utf8EmittedWholeMatcher.TryCreate(
                        anchoredValidatorPlan,
                        anchoredValidatorAllowsTrailingNewline,
                        out var anchoredValidatorMatcher)
                        ? anchoredValidatorMatcher
                        : null;
            }
            var result = budget.AllocateObject(new ConstructionState(regexPlan, linearRuntime, verifierRuntime, compiledPatternFamily, canUseDirectAnchoredValidatorMatch, anchoredValidatorAllowsTrailingNewline, anchoredBoundedDatePlan, repeatedDigitGroupPlan, emittedWholeMatcher, boundedSuffixLiteralPlan, symmetricLiteralWindowPlan, compiledSymmetricLiteralWindowCounter),
                static state => new Utf8SimplePatternCompiledEngineRuntime(state));
            scope.Complete();
            return result;
        }
        finally { budget.LeaveDepth(); }
    }
}

internal sealed partial class Utf8StructuralLinearAutomatonCompiledEngineRuntime
{
    private readonly record struct ConstructionState(
        Utf8PreparedRegex RegexPlan,
        Utf8StructuralLinearRuntime LinearRuntime,
        Utf8VerifierRuntime VerifierRuntime,
        bool EmitEnabled,
        Utf8EmittedDeterministicMatcher? EmittedDeterministicMatcher,
        Utf8EmittedKernelMatcher? EmittedKernelMatcher,
        Utf8EmittedLiteralFamilyCounter? EmittedBoundaryLiteralFamily,
        PreparedAsciiFindPlan OrderedWindowTrailingFindPlan);

    internal static Utf8StructuralLinearAutomatonCompiledEngineRuntime Create(Utf8PreparedRegex plan, Utf8VerifierRuntime verifier, bool emit)
    {
        using var scope = Utf8ConstructionScope.Enter();
        var budget = scope.Budget;
        budget.EnterDepth();
        try
        {
            var regexPlan = plan;
            var linearRuntime = Utf8StructuralLinearRuntime.Create(plan);
            var verifierRuntime = verifier;
            var emitEnabled = emit;
            var emittedDeterministicMatcher = emit && Utf8EmittedDeterministicMatcher.TryCreate(regexPlan.StructuralLinearProgram, out var matcher)
                ? matcher
                : null;
            var emittedKernelMatcher = emit && Utf8EmittedKernelMatcher.TryCreate(regexPlan, out var preparedKernelMatcher)
                ? preparedKernelMatcher
                : null;
            var emittedBoundaryLiteralFamily = emit &&
                (verifierRuntime.FallbackCandidateVerifier.FallbackOptions & RegexOptions.RightToLeft) == 0 &&
                Utf8EmittedLiteralFamilyCounter.TryCreateBoundaryOnlyStructuralFamily(
                    regexPlan,
                out var preparedBoundaryLiteralFamily)
                    ? preparedBoundaryLiteralFamily
                    : null;
            var orderedWindowTrailingFindPlan = regexPlan.ExecutionKind == NativeExecutionKind.AsciiOrderedLiteralWindow
                ? PreparedAsciiFindPlan.CreateForOrderedWindow(regexPlan.StructuralLinearProgram.OrderedLiteralWindowPlan)
                : default;
            var result = budget.AllocateObject(new ConstructionState(regexPlan, linearRuntime, verifierRuntime, emitEnabled, emittedDeterministicMatcher, emittedKernelMatcher, emittedBoundaryLiteralFamily, orderedWindowTrailingFindPlan),
                static state => new Utf8StructuralLinearAutomatonCompiledEngineRuntime(state));
            scope.Complete();
            return result;
        }
        finally { budget.LeaveDepth(); }
    }
}

internal sealed partial class Utf8ByteSafeLinearCompiledEngineRuntime
{
    private readonly record struct ConstructionState(
        Utf8PreparedRegex RegexPlan,
        Utf8VerifierRuntime VerifierRuntime,
        bool CanUseDirectPrefixUntilByteMatch,
        byte[] PrefixUntilByteLiteral,
        byte PrefixUntilByteTerminator);

    internal static Utf8ByteSafeLinearCompiledEngineRuntime Create(Utf8PreparedRegex plan, Utf8VerifierRuntime verifier)
    {
        using var scope = Utf8ConstructionScope.Enter();
        var budget = scope.Budget;
        budget.EnterDepth();
        try
        {
            var regexPlan = plan;
            var verifierRuntime = verifier;
            var directFamily = plan.FallbackDirectFamily;
            var canUseDirectPrefixUntilByteMatch = directFamily.Kind == Utf8FallbackDirectFamilyKind.AnchoredPrefixUntilByte &&
                directFamily.LiteralUtf8 is { Length: > 0 };
            var prefixUntilByteLiteral = directFamily.LiteralUtf8;
            var prefixUntilByteTerminator = directFamily.TerminatorByte;
            var result = budget.AllocateObject(new ConstructionState(regexPlan, verifierRuntime, canUseDirectPrefixUntilByteMatch, prefixUntilByteLiteral, prefixUntilByteTerminator),
                static state => new Utf8ByteSafeLinearCompiledEngineRuntime(state));
            scope.Complete();
            return result;
        }
        finally { budget.LeaveDepth(); }
    }
}

internal sealed partial class Utf8SearchGuidedFallbackCompiledEngineRuntime
{
    private readonly record struct ConstructionState(
        Utf8PreparedRegex RegexPlan,
        Utf8VerifierRuntime VerifierRuntime,
        Utf8EmittedSearchGuidedFallback? EmittedBackend);

    internal static Utf8SearchGuidedFallbackCompiledEngineRuntime Create(Utf8CompiledEngine compiledEngine, Utf8PreparedRegex plan, Utf8VerifierRuntime verifier)
    {
        using var scope = Utf8ConstructionScope.Enter();
        var budget = scope.Budget;
        budget.EnterDepth();
        try
        {
            var regexPlan = plan;
            var verifierRuntime = verifier;
            var backend = compiledEngine.Backend;
            var emittedBackend = backend == Utf8CompiledExecutionBackend.EmittedInstruction &&
                              Utf8EmittedSearchGuidedFallback.TryCreate(regexPlan, verifierRuntime, out var emitted)
                ? emitted
                : null;
            var result = budget.AllocateObject(new ConstructionState(regexPlan, verifierRuntime, emittedBackend),
                static state => new Utf8SearchGuidedFallbackCompiledEngineRuntime(state));
            scope.Complete();
            return result;
        }
        finally { budget.LeaveDepth(); }
    }
}

internal sealed partial class Utf8CompiledFallbackCompiledEngineRuntime
{
    private readonly record struct ConstructionState(
        Utf8PreparedRegex RegexPlan,
        Utf8VerifierRuntime VerifierRuntime,
        Utf8StructuralSearchPlan[] CandidatePlans);

    internal static Utf8CompiledFallbackCompiledEngineRuntime Create(Utf8PreparedRegex plan, Utf8VerifierRuntime verifier)
    {
        using var scope = Utf8ConstructionScope.Enter();
        var budget = scope.Budget;
        budget.EnterDepth();
        try
        {
            var regexPlan = plan;
            var verifierRuntime = verifier;
            var candidatePlans = regexPlan.SearchPlan.FallbackSearch.CandidatePlans ?? [];
            var result = budget.AllocateObject(new ConstructionState(regexPlan, verifierRuntime, candidatePlans),
                static state => new Utf8CompiledFallbackCompiledEngineRuntime(state));
            scope.Complete();
            return result;
        }
        finally { budget.LeaveDepth(); }
    }
}

internal sealed partial class Utf8FallbackRegexCompiledEngineRuntime
{
    private readonly record struct ConstructionState(
        Utf8PreparedRegex RegexPlan,
        Utf8VerifierRuntime VerifierRuntime,
        Utf8FallbackDirectFamilyPlan DirectFamily,
        Utf8AsciiLiteralFinder LinePrefixFinder,
        Utf8EmittedTokenFamilyMatcher? EmittedTokenFamilyMatcher,
        PreparedAsciiDelimitedTokenSearch DelimitedTokenSearch,
        PreparedAsciiLiteralStructuredTokenSearch LiteralStructuredTokenSearch);

    internal static Utf8FallbackRegexCompiledEngineRuntime Create(Utf8PreparedRegex plan, Utf8VerifierRuntime verifier)
    {
        using var scope = Utf8ConstructionScope.Enter();
        var budget = scope.Budget;
        budget.EnterDepth();
        try
        {
            var regexPlan = plan;
            var verifierRuntime = verifier;
            var directFamily = plan.FallbackDirectFamily;
            var linePrefixFinder = directFamily.LiteralUtf8 is { Length: > 0 } literal
                ? new Utf8AsciiLiteralFinder(literal)
                : default;
            var emittedTokenFamilyMatcher = Utf8EmittedTokenFamilyMatcher.TryCreate(directFamily, out var preparedTokenFamilyMatcher)
                ? preparedTokenFamilyMatcher
                : null;
            var delimitedTokenSearch = Utf8FallbackPreparedTokenSearchFactory.CreateDelimitedTokenSearch(directFamily);
            var literalStructuredTokenSearch = Utf8FallbackPreparedTokenSearchFactory.CreateLiteralStructuredTokenSearch(directFamily);
            var result = budget.AllocateObject(new ConstructionState(regexPlan, verifierRuntime, directFamily, linePrefixFinder, emittedTokenFamilyMatcher, delimitedTokenSearch, literalStructuredTokenSearch),
                static state => new Utf8FallbackRegexCompiledEngineRuntime(state));
            scope.Complete();
            return result;
        }
        finally { budget.LeaveDepth(); }
    }
}
