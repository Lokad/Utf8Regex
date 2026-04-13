using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Diagnostics;
using Lokad.Utf8Regex.Internal.Input;
using Lokad.Utf8Regex.Internal.Planning;

namespace Lokad.Utf8Regex.Internal.Execution;

internal abstract class Utf8CompiledEngineRuntime
{
    protected static Utf8ValidationResult GetProbeValidation(ReadOnlySpan<byte> input, Utf8ValidationResult validation)
    {
        return validation.ByteLength == input.Length
            ? validation
            : Utf8InputAnalyzer.ValidateOnly(input);
    }

    public virtual bool SupportsAsciiWellFormedOnlyMatch => false;
    public virtual bool SupportsWellFormedOnlyMatch => SupportsAsciiWellFormedOnlyMatch;
    public virtual bool WellFormedOnlyMatchMissIsDefinitive => false;
    public virtual bool SupportsWellFormedOnlyCount => false;
    public virtual bool SupportsThrowIfInvalidOnlyCount => false;
    public virtual bool PreferValidateOnlyCount => false;
    public virtual bool SkipRequiredPrefilterForMatch => false;
    public virtual bool SkipRequiredPrefilterForCount => false;
    public virtual bool UsesEmittedAnchoredValidatorMatcher => false;
    public virtual bool UsesEmittedKernelMatcher => false;

    public virtual bool TryMatchAsciiWellFormedOnly(ReadOnlySpan<byte> input, out Utf8ValueMatch match)
    {
        match = Utf8ValueMatch.NoMatch;
        return false;
    }

    public virtual bool TryMatchWellFormedOnly(ReadOnlySpan<byte> input, out Utf8ValueMatch match)
    {
        return TryMatchAsciiWellFormedOnly(input, out match);
    }

    public abstract bool IsMatch(ReadOnlySpan<byte> input, Utf8ValidationResult validation, Utf8ExecutionBudget? budget);

    public abstract int Count(ReadOnlySpan<byte> input, Utf8ValidationResult validation, Utf8ExecutionBudget? budget);

    public abstract Utf8ValueMatch Match(ReadOnlySpan<byte> input, Utf8ValidationResult validation, Utf8ExecutionBudget? budget);

    public virtual bool TryMatchWithoutValidation(ReadOnlySpan<byte> input, Utf8ExecutionBudget? budget, out Utf8ValueMatch match)
    {
        match = Utf8ValueMatch.NoMatch;
        return false;
    }

    public virtual bool TryDebugCountExactUtf8LiteralValidatedThreeByte(ReadOnlySpan<byte> input, out int count)
    {
        count = 0;
        return false;
    }

    public virtual bool TryDebugCountExactUtf8LiteralLeadingScalarAnchored(ReadOnlySpan<byte> input, out int count)
    {
        count = 0;
        return false;
    }

    public virtual bool TryDebugCountExactUtf8LiteralPreparedSearch(ReadOnlySpan<byte> input, out int count)
    {
        count = 0;
        return false;
    }

    public virtual bool TryDebugCountExactUtf8LiteralAnchored(ReadOnlySpan<byte> input, out int count)
    {
        count = 0;
        return false;
    }

    public virtual bool TryDebugMatchAsciiLiteralFamilyRaw(ReadOnlySpan<byte> input, out int index, out int matchedByteLength)
    {
        index = -1;
        matchedByteLength = 0;
        return false;
    }

    public abstract Utf8ValueMatchEnumerator CreateMatchEnumerator(ReadOnlySpan<byte> input, Utf8ValidationResult validation, Utf8ExecutionBudget? budget);

    public abstract Utf8ValueSplitEnumerator CreateSplitEnumerator(ReadOnlySpan<byte> input, Utf8ValidationResult validation, int count, Utf8ExecutionBudget? budget);

    public virtual byte[] ReplaceExactLiteral(ReadOnlySpan<byte> input, byte[] replacementBytes, Utf8ExecutionBudget? budget)
    {
        throw new InvalidOperationException("Exact literal replacement bytes are only valid for compiled literal engines.");
    }

    public virtual byte[] ReplaceLiteralBytes(ReadOnlySpan<byte> input, Utf8ValidationResult validation, byte[] replacementBytes, Utf8ExecutionBudget? budget)
    {
        throw new InvalidOperationException("Literal replacement bytes are not supported by this compiled engine.");
    }

    public virtual bool TryReplaceLiteralBytes(ReadOnlySpan<byte> input, Utf8ValidationResult validation, byte[] replacementBytes, Span<byte> destination, out int bytesWritten, Utf8ExecutionBudget? budget)
    {
        bytesWritten = 0;
        throw new InvalidOperationException("Literal replacement bytes are not supported by this compiled engine.");
    }

    public static Utf8CompiledEngineRuntime Create(Utf8RegexPlan regexPlan, Utf8VerifierRuntime verifierRuntime, RegexOptions options)
    {
        return Create(regexPlan.CompiledEngine, regexPlan, verifierRuntime, options);
    }

    public static Utf8CompiledEngineRuntime Create(Utf8CompiledEngine compiledEngine, Utf8RegexPlan regexPlan, Utf8VerifierRuntime verifierRuntime, RegexOptions options)
        => Utf8CompiledRuntimeFactory.Create(compiledEngine, regexPlan, verifierRuntime, options);

    protected static RegexOptions NormalizeDirectRouteOptions(RegexOptions options) => Utf8CompiledRuntimeFactory.NormalizeDirectRouteOptions(options);
}

internal sealed class Utf8ExactLiteralCompiledEngineRuntime : Utf8CompiledEngineRuntime
{
    private readonly Utf8LiteralCompiledEngineRuntime _inner;

    public Utf8ExactLiteralCompiledEngineRuntime(Utf8LiteralCompiledEngineRuntime inner) => _inner = inner;

    public override bool SupportsAsciiWellFormedOnlyMatch => _inner.SupportsAsciiWellFormedOnlyMatch;
    public override bool SupportsWellFormedOnlyMatch => _inner.SupportsWellFormedOnlyMatch;
    public override bool WellFormedOnlyMatchMissIsDefinitive => _inner.WellFormedOnlyMatchMissIsDefinitive;
    public override bool SupportsWellFormedOnlyCount => _inner.SupportsWellFormedOnlyCount;
    public override bool SupportsThrowIfInvalidOnlyCount => _inner.SupportsThrowIfInvalidOnlyCount;
    public override bool PreferValidateOnlyCount => _inner.PreferValidateOnlyCount;
    public override bool TryMatchAsciiWellFormedOnly(ReadOnlySpan<byte> input, out Utf8ValueMatch match) => _inner.TryMatchAsciiWellFormedOnly(input, out match);
    public override bool TryMatchWellFormedOnly(ReadOnlySpan<byte> input, out Utf8ValueMatch match) => _inner.TryMatchWellFormedOnly(input, out match);
    public override bool IsMatch(ReadOnlySpan<byte> input, Utf8ValidationResult validation, Utf8ExecutionBudget? budget) => _inner.IsMatch(input, validation, budget);
    public override int Count(ReadOnlySpan<byte> input, Utf8ValidationResult validation, Utf8ExecutionBudget? budget) => _inner.Count(input, validation, budget);
    public override Utf8ValueMatch Match(ReadOnlySpan<byte> input, Utf8ValidationResult validation, Utf8ExecutionBudget? budget) => _inner.Match(input, validation, budget);
    public override bool TryMatchWithoutValidation(ReadOnlySpan<byte> input, Utf8ExecutionBudget? budget, out Utf8ValueMatch match) => _inner.TryMatchWithoutValidation(input, budget, out match);
    public override bool TryDebugCountExactUtf8LiteralValidatedThreeByte(ReadOnlySpan<byte> input, out int count) => _inner.TryDebugCountExactUtf8LiteralValidatedThreeByte(input, out count);
    public override bool TryDebugCountExactUtf8LiteralLeadingScalarAnchored(ReadOnlySpan<byte> input, out int count) => _inner.TryDebugCountExactUtf8LiteralLeadingScalarAnchored(input, out count);
    public override bool TryDebugCountExactUtf8LiteralPreparedSearch(ReadOnlySpan<byte> input, out int count) => _inner.TryDebugCountExactUtf8LiteralPreparedSearch(input, out count);
    public override bool TryDebugCountExactUtf8LiteralAnchored(ReadOnlySpan<byte> input, out int count) => _inner.TryDebugCountExactUtf8LiteralAnchored(input, out count);
    public override bool TryDebugMatchAsciiLiteralFamilyRaw(ReadOnlySpan<byte> input, out int index, out int matchedByteLength) => _inner.TryDebugMatchAsciiLiteralFamilyRaw(input, out index, out matchedByteLength);
    public override Utf8ValueMatchEnumerator CreateMatchEnumerator(ReadOnlySpan<byte> input, Utf8ValidationResult validation, Utf8ExecutionBudget? budget) => _inner.CreateMatchEnumerator(input, validation, budget);
    public override Utf8ValueSplitEnumerator CreateSplitEnumerator(ReadOnlySpan<byte> input, Utf8ValidationResult validation, int count, Utf8ExecutionBudget? budget) => _inner.CreateSplitEnumerator(input, validation, count, budget);
    public override byte[] ReplaceExactLiteral(ReadOnlySpan<byte> input, byte[] replacementBytes, Utf8ExecutionBudget? budget) => _inner.ReplaceExactLiteral(input, replacementBytes, budget);
}

internal sealed class Utf8LiteralFamilyCompiledEngineRuntime : Utf8CompiledEngineRuntime
{
    private readonly Utf8LiteralCompiledEngineRuntime _inner;

    public Utf8LiteralFamilyCompiledEngineRuntime(Utf8LiteralCompiledEngineRuntime inner) => _inner = inner;

    public override bool SupportsAsciiWellFormedOnlyMatch => _inner.SupportsAsciiWellFormedOnlyMatch;
    public override bool SupportsWellFormedOnlyMatch => _inner.SupportsWellFormedOnlyMatch;
    public override bool WellFormedOnlyMatchMissIsDefinitive => _inner.WellFormedOnlyMatchMissIsDefinitive;
    public override bool SupportsWellFormedOnlyCount => _inner.SupportsWellFormedOnlyCount;
    public override bool SupportsThrowIfInvalidOnlyCount => _inner.SupportsThrowIfInvalidOnlyCount;
    public override bool PreferValidateOnlyCount => _inner.PreferValidateOnlyCount;
    public override bool TryMatchAsciiWellFormedOnly(ReadOnlySpan<byte> input, out Utf8ValueMatch match) => _inner.TryMatchAsciiWellFormedOnly(input, out match);
    public override bool TryMatchWellFormedOnly(ReadOnlySpan<byte> input, out Utf8ValueMatch match) => _inner.TryMatchWellFormedOnly(input, out match);
    public override bool IsMatch(ReadOnlySpan<byte> input, Utf8ValidationResult validation, Utf8ExecutionBudget? budget) => _inner.IsMatch(input, validation, budget);
    public override int Count(ReadOnlySpan<byte> input, Utf8ValidationResult validation, Utf8ExecutionBudget? budget) => _inner.Count(input, validation, budget);
    public override Utf8ValueMatch Match(ReadOnlySpan<byte> input, Utf8ValidationResult validation, Utf8ExecutionBudget? budget) => _inner.Match(input, validation, budget);
    public override bool TryMatchWithoutValidation(ReadOnlySpan<byte> input, Utf8ExecutionBudget? budget, out Utf8ValueMatch match) => _inner.TryMatchWithoutValidation(input, budget, out match);
    public override bool TryDebugMatchAsciiLiteralFamilyRaw(ReadOnlySpan<byte> input, out int index, out int matchedByteLength) => _inner.TryDebugMatchAsciiLiteralFamilyRaw(input, out index, out matchedByteLength);
    public override Utf8ValueMatchEnumerator CreateMatchEnumerator(ReadOnlySpan<byte> input, Utf8ValidationResult validation, Utf8ExecutionBudget? budget) => _inner.CreateMatchEnumerator(input, validation, budget);
    public override Utf8ValueSplitEnumerator CreateSplitEnumerator(ReadOnlySpan<byte> input, Utf8ValidationResult validation, int count, Utf8ExecutionBudget? budget) => _inner.CreateSplitEnumerator(input, validation, count, budget);
    public override byte[] ReplaceExactLiteral(ReadOnlySpan<byte> input, byte[] replacementBytes, Utf8ExecutionBudget? budget) => _inner.ReplaceExactLiteral(input, replacementBytes, budget);
}

internal sealed class Utf8StructuralFamilyCompiledEngineRuntime : Utf8CompiledEngineRuntime
{
    private readonly Utf8RegexPlan _regexPlan;
    private readonly Utf8VerifierRuntime _verifierRuntime;

    public Utf8StructuralFamilyCompiledEngineRuntime(Utf8NonLiteralCompiledEngineRuntime inner)
    {
        _regexPlan = inner.RegexPlan;
        _verifierRuntime = inner.VerifierRuntime;
    }

    public override bool IsMatch(ReadOnlySpan<byte> input, Utf8ValidationResult validation, Utf8ExecutionBudget? budget)
    {
        return UsesRightToLeft()
            ? _verifierRuntime.FallbackCandidateVerifier.FallbackRegex.IsMatch(Encoding.UTF8.GetString(input))
            : Utf8AsciiStructuralIdentifierFamilyExecutor.FindNext(
                input,
                _regexPlan.StructuralIdentifierFamilyPlan,
                _regexPlan.SearchPlan,
                _regexPlan.StructuralSearchPlan,
                _verifierRuntime.StructuralVerifierRuntime,
                0,
                budget,
                out _) >= 0;
    }

    public override int Count(ReadOnlySpan<byte> input, Utf8ValidationResult validation, Utf8ExecutionBudget? budget)
    {
        Utf8SearchDiagnosticsSession.Current?.MarkExecutionRoute("native_structural_family");
        return UsesRightToLeft()
            ? _verifierRuntime.FallbackCandidateVerifier.FallbackRegex.Count(Encoding.UTF8.GetString(input))
            : Utf8AsciiStructuralIdentifierFamilyExecutor.Count(
                input,
                _regexPlan.StructuralIdentifierFamilyPlan,
                _regexPlan.SearchPlan,
                _regexPlan.StructuralSearchPlan,
                _verifierRuntime.StructuralVerifierRuntime,
                budget);
    }

    public override Utf8ValueMatch Match(ReadOnlySpan<byte> input, Utf8ValidationResult validation, Utf8ExecutionBudget? budget)
    {
        if (UsesRightToLeft())
        {
            return MatchFallback(input);
        }

        var index = Utf8AsciiStructuralIdentifierFamilyExecutor.FindNext(
            input,
            _regexPlan.StructuralIdentifierFamilyPlan,
            _regexPlan.SearchPlan,
            _regexPlan.StructuralSearchPlan,
            _verifierRuntime.StructuralVerifierRuntime,
            0,
            budget,
            out var matchedLength);
        if (index < 0)
        {
            return Utf8ValueMatch.NoMatch;
        }

        return new Utf8ValueMatch(true, true, index, matchedLength, index, matchedLength);
    }

    public override Utf8ValueMatchEnumerator CreateMatchEnumerator(ReadOnlySpan<byte> input, Utf8ValidationResult validation, Utf8ExecutionBudget? budget)
    {
        throw new InvalidOperationException("Structural family engines do not expose a dedicated match enumerator runtime.");
    }

    public override Utf8ValueSplitEnumerator CreateSplitEnumerator(ReadOnlySpan<byte> input, Utf8ValidationResult validation, int count, Utf8ExecutionBudget? budget)
    {
        throw new InvalidOperationException("Structural family engines do not expose a dedicated split enumerator runtime.");
    }

    private Utf8ValueMatch MatchFallback(ReadOnlySpan<byte> input)
    {
        return Utf8CompiledFallbackMatchProjection.Match(input, _verifierRuntime.FallbackCandidateVerifier.FallbackRegex);
    }

    private bool UsesRightToLeft()
    {
        return (_verifierRuntime.FallbackCandidateVerifier.FallbackRegex.Options & RegexOptions.RightToLeft) != 0;
    }
}

internal sealed class Utf8SimplePatternCompiledEngineRuntime : Utf8CompiledEngineRuntime
{
    private readonly Utf8RegexPlan _regexPlan;
    private readonly Utf8StructuralLinearRuntime _linearRuntime;
    private readonly Utf8VerifierRuntime _verifierRuntime;
    private readonly Utf8CompiledPatternFamilyPlan _compiledPatternFamily;
    private readonly bool _canUseDirectAnchoredValidatorMatch;
    private readonly bool _anchoredValidatorAllowsTrailingNewline;
    private readonly AsciiSimplePatternAnchoredBoundedDatePlan _anchoredBoundedDatePlan;
    private readonly AsciiSimplePatternRepeatedDigitGroupPlan _repeatedDigitGroupPlan;
    private readonly Utf8EmittedAnchoredValidatorMatcher? _emittedAnchoredValidatorMatcher;
    private readonly AsciiSimplePatternBoundedSuffixLiteralPlan _boundedSuffixLiteralPlan;
    private readonly AsciiSimplePatternSymmetricLiteralWindowPlan _symmetricLiteralWindowPlan;
    private readonly Utf8CompiledSymmetricLiteralWindowCounter? _compiledSymmetricLiteralWindowCounter;

    public Utf8SimplePatternCompiledEngineRuntime(Utf8NonLiteralCompiledEngineRuntime inner, bool emitEnabled)
    {
        _regexPlan = inner.RegexPlan;
        _linearRuntime = Utf8StructuralLinearRuntime.Create(_regexPlan.StructuralLinearProgram);
        _verifierRuntime = inner.VerifierRuntime;
        _compiledPatternFamily = _regexPlan.SimplePatternPlan.CompiledPatternFamily;
        _canUseDirectAnchoredValidatorMatch =
            _compiledPatternFamily.Kind == Utf8CompiledPatternFamilyKind.AnchoredValidator ||
            _compiledPatternFamily.Kind == Utf8CompiledPatternFamilyKind.AnchoredBoundedDate;
        _anchoredValidatorAllowsTrailingNewline = _canUseDirectAnchoredValidatorMatch &&
            _regexPlan.SimplePatternPlan.AllowsTrailingNewlineBeforeEnd;
        _anchoredBoundedDatePlan = _compiledPatternFamily.AnchoredBoundedDatePlan;
        _repeatedDigitGroupPlan = _compiledPatternFamily.RepeatedDigitGroupPlan;
        _boundedSuffixLiteralPlan = _compiledPatternFamily.BoundedSuffixLiteralPlan;
        _symmetricLiteralWindowPlan = _compiledPatternFamily.SymmetricLiteralWindowPlan;
        _compiledSymmetricLiteralWindowCounter = emitEnabled && _symmetricLiteralWindowPlan.HasValue
            ? new Utf8CompiledSymmetricLiteralWindowCounter(_symmetricLiteralWindowPlan)
            : null;
        var anchoredValidatorPlan = _compiledPatternFamily.AnchoredValidatorPlan;
        _emittedAnchoredValidatorMatcher = emitEnabled &&
            Utf8SimplePatternCompiledRuntimePolicy.ShouldUseEmittedAnchoredValidator(anchoredValidatorPlan) &&
            Utf8EmittedAnchoredValidatorMatcher.TryCreate(
                anchoredValidatorPlan,
                _anchoredValidatorAllowsTrailingNewline,
                out var emittedAnchoredValidatorMatcher)
            ? emittedAnchoredValidatorMatcher
            : null;
    }

    public override bool SupportsAsciiWellFormedOnlyMatch => _canUseDirectAnchoredValidatorMatch;

    public override bool SupportsWellFormedOnlyMatch => _canUseDirectAnchoredValidatorMatch;

    public override bool WellFormedOnlyMatchMissIsDefinitive => _canUseDirectAnchoredValidatorMatch;

    public override bool SkipRequiredPrefilterForMatch => _compiledPatternFamily.Category == Utf8CompiledPatternCategory.AnchoredWhole;

    public override bool UsesEmittedAnchoredValidatorMatcher => _emittedAnchoredValidatorMatcher is not null;

    public override bool TryMatchWellFormedOnly(ReadOnlySpan<byte> input, out Utf8ValueMatch match)
    {
        if (_canUseDirectAnchoredValidatorMatch &&
            Utf8SimplePatternCompiledWholeMatcher.TryMatchAnchoredValidator(
                _regexPlan,
                _emittedAnchoredValidatorMatcher,
                _anchoredValidatorAllowsTrailingNewline,
                input,
                out var anchoredValidatorLength))
        {
            match = new Utf8ValueMatch(true, true, 0, anchoredValidatorLength, 0, anchoredValidatorLength);
            return true;
        }

        match = Utf8ValueMatch.NoMatch;
        return false;
    }

    public override bool TryMatchWithoutValidation(ReadOnlySpan<byte> input, Utf8ExecutionBudget? budget, out Utf8ValueMatch match)
    {
        if (Utf8SimplePatternCompiledSearchGuidedRouter.TryMatchWithoutValidation(
                _anchoredBoundedDatePlan,
                _repeatedDigitGroupPlan,
                _regexPlan.SimplePatternPlan.AllowsTrailingNewlineBeforeEnd,
                input,
                out match))
        {
            return true;
        }

        if (_canUseDirectAnchoredValidatorMatch)
        {
            var direct = Utf8SimplePatternCompiledWholeMatcher.TryMatchAnchoredValidatorWithoutValidation(
                _regexPlan,
                _anchoredValidatorAllowsTrailingNewline,
                input,
                out var anchoredValidatorLength);
            if (direct == Utf8AsciiAnchoredValidatorExecutor.DirectMatchResult.Match)
            {
                match = new Utf8ValueMatch(true, true, 0, anchoredValidatorLength, 0, anchoredValidatorLength);
                return true;
            }

            if (direct == Utf8AsciiAnchoredValidatorExecutor.DirectMatchResult.NeedsValidation)
            {
                match = Utf8ValueMatch.NoMatch;
                return false;
            }
            match = Utf8ValueMatch.NoMatch;
            return true;
        }

        match = Utf8ValueMatch.NoMatch;
        return false;
    }

    public override bool IsMatch(ReadOnlySpan<byte> input, Utf8ValidationResult validation, Utf8ExecutionBudget? budget)
    {
        if (_regexPlan.StructuralLinearProgram.Kind == Utf8StructuralLinearProgramKind.AsciiFixedTokenPattern)
        {
            return _linearRuntime.IsMatch(input, validation, default!, budget);
        }

        if (_canUseDirectAnchoredValidatorMatch)
        {
            if (validation.IsAscii &&
                Utf8SimplePatternCompiledWholeMatcher.TryMatchAnchoredValidator(
                    _regexPlan,
                    _emittedAnchoredValidatorMatcher,
                    _anchoredValidatorAllowsTrailingNewline,
                    input,
                    out _))
            {
                return true;
            }

            if (Utf8SimplePatternCompiledRuntimePolicy.ShouldFallbackAfterAnchoredValidatorMiss(input, validation, _anchoredValidatorAllowsTrailingNewline))
            {
                return _verifierRuntime.FallbackCandidateVerifier.FallbackRegex.IsMatch(Encoding.UTF8.GetString(input));
            }
        }

        if (validation.IsAscii || _regexPlan.SimplePatternPlan.IsUtf8ByteSafe)
        {
            if (Utf8SimplePatternCompiledSearchGuidedRouter.TryIsMatch(
                    _repeatedDigitGroupPlan,
                    _boundedSuffixLiteralPlan,
                    _symmetricLiteralWindowPlan,
                    input,
                    validation,
                    budget,
                    out var guidedIsMatch))
            {
                return guidedIsMatch;
            }

            if (Utf8SimplePatternCompiledWholeMatcher.TryMatchDirectAnchoredFixedAlternationSimplePattern(_regexPlan, input, out _))
            {
                return true;
            }

            if (Utf8SimplePatternCompiledWholeMatcher.TryMatchDirectAnchoredFixedLengthSimplePattern(_regexPlan, input, out _))
            {
                return true;
            }

            return Utf8ExecutionInterpreter.FindNextSimplePattern(
                input,
                _regexPlan.ExecutionProgram,
                _regexPlan.SearchPlan,
                _regexPlan.SimplePatternPlan,
                0,
                captures: null,
                budget,
                out _) >= 0;
        }

        return _verifierRuntime.FallbackCandidateVerifier.FallbackRegex.IsMatch(Encoding.UTF8.GetString(input));
    }

    public override int Count(ReadOnlySpan<byte> input, Utf8ValidationResult validation, Utf8ExecutionBudget? budget)
    {
        if (_regexPlan.StructuralLinearProgram.Kind == Utf8StructuralLinearProgramKind.AsciiFixedTokenPattern)
        {
            return _linearRuntime.Count(input, validation, default!, budget);
        }

        if (_canUseDirectAnchoredValidatorMatch)
        {
            if (validation.IsAscii &&
                Utf8SimplePatternCompiledWholeMatcher.TryMatchAnchoredValidator(
                    _regexPlan,
                    _emittedAnchoredValidatorMatcher,
                    _anchoredValidatorAllowsTrailingNewline,
                    input,
                    out _))
            {
                Utf8SearchDiagnosticsSession.Current?.MarkExecutionRoute("native_ascii_anchored_validator");
                return 1;
            }

            if (Utf8SimplePatternCompiledRuntimePolicy.ShouldFallbackAfterAnchoredValidatorMiss(input, validation, _anchoredValidatorAllowsTrailingNewline))
            {
                return _verifierRuntime.FallbackCandidateVerifier.FallbackRegex.Count(Encoding.UTF8.GetString(input));
            }
        }

        if (!validation.IsAscii && !_regexPlan.SimplePatternPlan.IsUtf8ByteSafe)
        {
            return _verifierRuntime.FallbackCandidateVerifier.FallbackRegex.Count(Encoding.UTF8.GetString(input));
        }

        if (Utf8SimplePatternCompiledSearchGuidedRouter.TryCount(
                _repeatedDigitGroupPlan,
                _boundedSuffixLiteralPlan,
                _symmetricLiteralWindowPlan,
                _compiledSymmetricLiteralWindowCounter,
                input,
                validation,
                budget,
                out var guidedCount))
        {
            return guidedCount;
        }

        if (Utf8SimplePatternCompiledWholeMatcher.TryMatchDirectAnchoredFixedAlternationSimplePattern(_regexPlan, input, out _))
        {
            Utf8SearchDiagnosticsSession.Current?.MarkExecutionRoute("native_ascii_simple_pattern_fixed_alternation");
            return 1;
        }

        if (Utf8SimplePatternCompiledWholeMatcher.TryMatchDirectAnchoredFixedLengthSimplePattern(_regexPlan, input, out _))
        {
            Utf8SearchDiagnosticsSession.Current?.MarkExecutionRoute("native_ascii_simple_pattern_fixed_length");
            return 1;
        }

        Utf8SearchDiagnosticsSession.Current?.MarkExecutionRoute("native_ascii_simple_pattern");
        var count = 0;
        var index = 0;
        while (index <= input.Length)
        {
            var found = Utf8ExecutionInterpreter.FindNextSimplePattern(
                input,
                _regexPlan.ExecutionProgram,
                _regexPlan.SearchPlan,
                _regexPlan.SimplePatternPlan,
                index,
                captures: null,
                budget,
                out var matchedLength);
            if (found < 0)
            {
                return count;
            }

            count++;
            index = found + Math.Max(matchedLength, 1);
        }

        return count;
    }

    public override Utf8ValueMatch Match(ReadOnlySpan<byte> input, Utf8ValidationResult validation, Utf8ExecutionBudget? budget)
    {
        if (_regexPlan.StructuralLinearProgram.Kind == Utf8StructuralLinearProgramKind.AsciiFixedTokenPattern)
        {
            return _linearRuntime.Match(input, validation, default!, budget);
        }

        if (_canUseDirectAnchoredValidatorMatch)
        {
            if (validation.IsAscii &&
                Utf8SimplePatternCompiledWholeMatcher.TryMatchAnchoredValidator(
                    _regexPlan,
                    _emittedAnchoredValidatorMatcher,
                    _anchoredValidatorAllowsTrailingNewline,
                    input,
                    out var anchoredValidatorLength))
            {
                return new Utf8ValueMatch(true, true, 0, anchoredValidatorLength, 0, anchoredValidatorLength);
            }

            if (Utf8SimplePatternCompiledRuntimePolicy.ShouldFallbackAfterAnchoredValidatorMiss(input, validation, _anchoredValidatorAllowsTrailingNewline))
            {
                var fallbackDecoded = Encoding.UTF8.GetString(input);
                var fallbackMatch = _verifierRuntime.FallbackCandidateVerifier.FallbackRegex.Match(fallbackDecoded);
                if (!fallbackMatch.Success)
                {
                    return Utf8ValueMatch.NoMatch;
                }

                return Utf8ProjectionExecutor.ProjectByteAlignedMatch(fallbackMatch.Index, fallbackMatch.Length);
            }
        }

        if (validation.IsAscii || _regexPlan.SimplePatternPlan.IsUtf8ByteSafe)
        {
            if (Utf8SimplePatternCompiledSearchGuidedRouter.TryMatch(
                    _repeatedDigitGroupPlan,
                    _boundedSuffixLiteralPlan,
                    _symmetricLiteralWindowPlan,
                    input,
                    validation,
                    budget,
                    out var guidedMatch))
            {
                return guidedMatch;
            }

            if (Utf8SimplePatternCompiledWholeMatcher.TryMatchDirectAnchoredFixedAlternationSimplePattern(_regexPlan, input, out var alternationMatchedLength))
            {
                return new Utf8ValueMatch(true, true, 0, alternationMatchedLength, 0, alternationMatchedLength);
            }

            if (Utf8SimplePatternCompiledWholeMatcher.TryMatchDirectAnchoredFixedLengthSimplePattern(_regexPlan, input, out var directMatchedLength))
            {
                return new Utf8ValueMatch(true, true, 0, directMatchedLength, 0, directMatchedLength);
            }

            var index = Utf8ExecutionInterpreter.FindNextSimplePattern(
                input,
                _regexPlan.ExecutionProgram,
                _regexPlan.SearchPlan,
                _regexPlan.SimplePatternPlan,
                0,
                captures: null,
                budget,
                out var matchLength);
            if (index < 0)
            {
                return Utf8ValueMatch.NoMatch;
            }

            return new Utf8ValueMatch(true, true, index, matchLength, index, matchLength);
        }

        var analysis = Utf8InputAnalyzer.Analyze(input);
        var decoded = Encoding.UTF8.GetString(input);
        var match = _verifierRuntime.FallbackCandidateVerifier.FallbackRegex.Match(decoded);
        if (!match.Success)
        {
            return Utf8ValueMatch.NoMatch;
        }

        if (analysis.BoundaryMap.TryGetByteRange(match.Index, match.Length, out var indexInBytes, out var lengthInBytes))
        {
            return new Utf8ValueMatch(true, true, match.Index, match.Length, indexInBytes, lengthInBytes);
        }

        return new Utf8ValueMatch(true, false, match.Index, match.Length);
    }

    public override Utf8ValueMatchEnumerator CreateMatchEnumerator(ReadOnlySpan<byte> input, Utf8ValidationResult validation, Utf8ExecutionBudget? budget)
    {
        if (_regexPlan.StructuralLinearProgram.Kind == Utf8StructuralLinearProgramKind.AsciiFixedTokenPattern)
        {
            return new Utf8ValueMatchEnumerator(input, _regexPlan.StructuralLinearProgram, budget);
        }

        if (validation.IsAscii)
        {
            return new Utf8ValueMatchEnumerator(input, _regexPlan.ExecutionProgram, _regexPlan.SearchPlan, _regexPlan.SimplePatternPlan, budget);
        }

        var analysis = Utf8InputAnalyzer.Analyze(input);
        return new Utf8ValueMatchEnumerator(input, Encoding.UTF8.GetString(input), _verifierRuntime.FallbackCandidateVerifier.FallbackRegex, analysis.BoundaryMap);
    }

    public override Utf8ValueSplitEnumerator CreateSplitEnumerator(ReadOnlySpan<byte> input, Utf8ValidationResult validation, int count, Utf8ExecutionBudget? budget)
    {
        if (validation.IsAscii)
        {
            return new Utf8ValueSplitEnumerator(input, _regexPlan.SearchPlan, _regexPlan.ExecutionProgram, _regexPlan.SimplePatternPlan, count, budget);
        }

        var analysis = Utf8InputAnalyzer.Analyze(input);
        return new Utf8ValueSplitEnumerator(input, Encoding.UTF8.GetString(input), _verifierRuntime.FallbackCandidateVerifier.FallbackRegex, count, analysis.BoundaryMap);
    }
}

internal sealed class Utf8StructuralLinearAutomatonCompiledEngineRuntime : Utf8CompiledEngineRuntime
{
    private readonly Utf8RegexPlan _regexPlan;
    private readonly Utf8StructuralLinearRuntime _linearRuntime;
    private readonly Utf8VerifierRuntime _verifierRuntime;
    private readonly bool _emitEnabled;
    private readonly Utf8EmittedDeterministicMatcher? _emittedDeterministicMatcher;
    private readonly Utf8EmittedKernelMatcher? _emittedKernelMatcher;
    private readonly PreparedAsciiFindPlan _orderedWindowTrailingFindPlan;

    public Utf8StructuralLinearAutomatonCompiledEngineRuntime(Utf8NonLiteralCompiledEngineRuntime inner, bool emitEnabled)
    {
        _regexPlan = inner.RegexPlan;
        _linearRuntime = Utf8StructuralLinearRuntime.Create(inner.RegexPlan.StructuralLinearProgram);
        _verifierRuntime = inner.VerifierRuntime;
        _emitEnabled = emitEnabled;
        _emittedDeterministicMatcher = emitEnabled && Utf8EmittedDeterministicMatcher.TryCreate(_regexPlan.StructuralLinearProgram, out var matcher)
            ? matcher
            : null;
        _emittedKernelMatcher = emitEnabled && Utf8EmittedKernelMatcher.TryCreate(_regexPlan, out var emittedKernelMatcher)
            ? emittedKernelMatcher
            : null;
        _orderedWindowTrailingFindPlan = _regexPlan.ExecutionKind == NativeExecutionKind.AsciiOrderedLiteralWindow
            ? PreparedAsciiFindPlan.CreateForOrderedWindow(_regexPlan.StructuralLinearProgram.OrderedLiteralWindowPlan)
            : default;
    }

    public override bool UsesEmittedKernelMatcher => _emittedKernelMatcher is not null;

    public override bool IsMatch(ReadOnlySpan<byte> input, Utf8ValidationResult validation, Utf8ExecutionBudget? budget)
    {
        if (_regexPlan.ExecutionKind == NativeExecutionKind.AsciiSimplePattern &&
            _regexPlan.SimplePatternPlan.RunPlan.HasValue)
        {
            return Utf8AsciiCharClassRunExecutor.IsMatch(input, _regexPlan.SimplePatternPlan.RunPlan, budget);
        }

        if (Utf8StructuralLinearCompiledRouter.TryIsMatch(
                _regexPlan,
                _verifierRuntime,
                _emitEnabled,
                _emittedDeterministicMatcher,
                _emittedKernelMatcher,
                _orderedWindowTrailingFindPlan,
                input,
                validation,
                budget,
                out var routedIsMatch))
        {
            return routedIsMatch;
        }

        return _linearRuntime.IsMatch(input, validation, _verifierRuntime, budget);
    }

    public override int Count(ReadOnlySpan<byte> input, Utf8ValidationResult validation, Utf8ExecutionBudget? budget)
    {
        if (_regexPlan.ExecutionKind == NativeExecutionKind.AsciiSimplePattern &&
            _regexPlan.SimplePatternPlan.RunPlan.HasValue)
        {
            return Utf8AsciiCharClassRunExecutor.Count(input, _regexPlan.SimplePatternPlan.RunPlan, budget);
        }

        if (Utf8StructuralLinearCompiledRouter.TryCount(
                _regexPlan,
                _verifierRuntime,
                _emitEnabled,
                _emittedDeterministicMatcher,
                _emittedKernelMatcher,
                _orderedWindowTrailingFindPlan,
                input,
                validation,
                budget,
                out var routedCount))
        {
            return routedCount;
        }

        return _linearRuntime.Count(input, validation, _verifierRuntime, budget);
    }

    public override Utf8ValueMatch Match(ReadOnlySpan<byte> input, Utf8ValidationResult validation, Utf8ExecutionBudget? budget)
    {
        if (_regexPlan.ExecutionKind == NativeExecutionKind.AsciiSimplePattern &&
            _regexPlan.SimplePatternPlan.RunPlan.HasValue)
        {
            var index = Utf8AsciiCharClassRunExecutor.FindNext(input, _regexPlan.SimplePatternPlan.RunPlan, 0, out var matchedLength, budget);
            return index < 0
                ? Utf8ValueMatch.NoMatch
                : new Utf8ValueMatch(true, true, index, matchedLength, index, matchedLength);
        }

        if (Utf8StructuralLinearCompiledRouter.TryMatch(
                _regexPlan,
                _verifierRuntime,
                _emitEnabled,
                _emittedDeterministicMatcher,
                _emittedKernelMatcher,
                _orderedWindowTrailingFindPlan,
                input,
                validation,
                budget,
                out var routedMatch))
        {
            return routedMatch;
        }

        return _linearRuntime.Match(input, validation, _verifierRuntime, budget);
    }

    public override Utf8ValueMatchEnumerator CreateMatchEnumerator(ReadOnlySpan<byte> input, Utf8ValidationResult validation, Utf8ExecutionBudget? budget)
    {
        if (_regexPlan.StructuralLinearProgram.DeterministicProgram.HasValue && validation.IsAscii)
        {
            return new Utf8ValueMatchEnumerator(input, _regexPlan.StructuralLinearProgram, budget);
        }

        var analysis = Utf8InputAnalyzer.Analyze(input);
        return new Utf8ValueMatchEnumerator(input, Encoding.UTF8.GetString(input), _verifierRuntime.FallbackCandidateVerifier.FallbackRegex, analysis.BoundaryMap);
    }

    public override Utf8ValueSplitEnumerator CreateSplitEnumerator(ReadOnlySpan<byte> input, Utf8ValidationResult validation, int count, Utf8ExecutionBudget? budget)
    {
        if (_regexPlan.StructuralLinearProgram.DeterministicProgram.HasValue && validation.IsAscii)
        {
            return new Utf8ValueSplitEnumerator(input, _regexPlan.StructuralLinearProgram, count, budget);
        }

        var analysis = Utf8InputAnalyzer.Analyze(input);
        return new Utf8ValueSplitEnumerator(input, Encoding.UTF8.GetString(input), _verifierRuntime.FallbackCandidateVerifier.FallbackRegex, count, analysis.BoundaryMap);
    }

    public override byte[] ReplaceLiteralBytes(ReadOnlySpan<byte> input, Utf8ValidationResult validation, byte[] replacementBytes, Utf8ExecutionBudget? budget)
    {
        if (_regexPlan.StructuralLinearProgram.Kind == Utf8StructuralLinearProgramKind.AsciiFixedTokenPattern)
        {
            return Utf8FixedMatchReplaceEngine.Replace(
                input,
                replacementBytes,
                _regexPlan.StructuralLinearProgram,
                budget);
        }

        return Utf8LiteralReplaceEngine.Replace(
            input,
            replacementBytes,
            (ReadOnlySpan<byte> bytes, int start, out int matchIndex, out int matchLength) =>
            {
                if (_linearRuntime.TryFindNext(bytes, validation, _verifierRuntime, start, budget, out matchIndex, out matchLength))
                {
                    return true;
                }

                matchIndex = -1;
                matchLength = 0;
                return false;
            },
            budget);
    }

    public override bool TryReplaceLiteralBytes(ReadOnlySpan<byte> input, Utf8ValidationResult validation, byte[] replacementBytes, Span<byte> destination, out int bytesWritten, Utf8ExecutionBudget? budget)
    {
        if (_regexPlan.StructuralLinearProgram.Kind == Utf8StructuralLinearProgramKind.AsciiFixedTokenPattern)
        {
            return Utf8FixedMatchReplaceEngine.TryReplace(
                input,
                replacementBytes,
                _regexPlan.StructuralLinearProgram,
                destination,
                out bytesWritten,
                budget);
        }

        return Utf8LiteralReplaceEngine.TryReplace(
            input,
            replacementBytes,
            (ReadOnlySpan<byte> bytes, int start, out int matchIndex, out int matchLength) =>
            {
                if (_linearRuntime.TryFindNext(bytes, validation, _verifierRuntime, start, budget, out matchIndex, out matchLength))
                {
                    return true;
                }

                matchIndex = -1;
                matchLength = 0;
                return false;
            },
            destination,
            out bytesWritten,
            budget);
    }
}

internal sealed class Utf8ByteSafeLinearCompiledEngineRuntime : Utf8CompiledEngineRuntime
{
    private readonly Utf8RegexPlan _regexPlan;
    private readonly Utf8VerifierRuntime _verifierRuntime;
    private readonly bool _canUseDirectPrefixUntilByteMatch;
    private readonly byte[]? _prefixUntilByteLiteral;
    private readonly byte _prefixUntilByteTerminator;

    public Utf8ByteSafeLinearCompiledEngineRuntime(Utf8NonLiteralCompiledEngineRuntime inner)
    {
        _regexPlan = inner.RegexPlan;
        _verifierRuntime = inner.VerifierRuntime;
        var directFamily = inner.RegexPlan.FallbackDirectFamily;
        _canUseDirectPrefixUntilByteMatch = directFamily.Kind == Utf8FallbackDirectFamilyKind.AnchoredPrefixUntilByte &&
            directFamily.LiteralUtf8 is { Length: > 0 };
        _prefixUntilByteLiteral = directFamily.LiteralUtf8;
        _prefixUntilByteTerminator = directFamily.TerminatorByte;
    }

    public override bool SupportsAsciiWellFormedOnlyMatch => _canUseDirectPrefixUntilByteMatch;
    public override bool SupportsWellFormedOnlyMatch => _canUseDirectPrefixUntilByteMatch;
    public override bool WellFormedOnlyMatchMissIsDefinitive => _canUseDirectPrefixUntilByteMatch;
    public override bool SkipRequiredPrefilterForMatch => _canUseDirectPrefixUntilByteMatch;

    public override bool TryMatchAsciiWellFormedOnly(ReadOnlySpan<byte> input, out Utf8ValueMatch match)
    {
        if (_canUseDirectPrefixUntilByteMatch &&
            Utf8AsciiPrefixTokenExecutor.TryMatchAnchoredPrefixUntilByte(input, _prefixUntilByteLiteral!, _prefixUntilByteTerminator, out var docLineLength))
        {
            match = new Utf8ValueMatch(true, true, 0, docLineLength, 0, docLineLength);
            return true;
        }

        match = Utf8ValueMatch.NoMatch;
        return false;
    }

    public override bool TryMatchWithoutValidation(ReadOnlySpan<byte> input, Utf8ExecutionBudget? budget, out Utf8ValueMatch match)
    {
        if (_canUseDirectPrefixUntilByteMatch &&
            Utf8InputAnalyzer.IsAscii(input))
        {
            match = Utf8AsciiPrefixTokenExecutor.TryMatchAnchoredPrefixUntilByte(input, _prefixUntilByteLiteral!, _prefixUntilByteTerminator, out var docLineLength)
                ? new Utf8ValueMatch(true, true, 0, docLineLength, 0, docLineLength)
                : Utf8ValueMatch.NoMatch;
            return true;
        }

        match = Utf8ValueMatch.NoMatch;
        return false;
    }

    public override bool IsMatch(ReadOnlySpan<byte> input, Utf8ValidationResult validation, Utf8ExecutionBudget? budget)
    {
        return Utf8ByteSafeLinearExecutor.IsMatch(input, _regexPlan, _verifierRuntime.StructuralVerifierRuntime, budget);
    }

    public override int Count(ReadOnlySpan<byte> input, Utf8ValidationResult validation, Utf8ExecutionBudget? budget)
    {
        Utf8SearchDiagnosticsSession.Current?.MarkExecutionRoute("fallback_byte_safe_linear");
        return Utf8ByteSafeLinearExecutor.Count(input, _regexPlan, _verifierRuntime.StructuralVerifierRuntime, budget);
    }

    public override Utf8ValueMatch Match(ReadOnlySpan<byte> input, Utf8ValidationResult validation, Utf8ExecutionBudget? budget)
    {
        if (_canUseDirectPrefixUntilByteMatch && validation.IsAscii &&
            Utf8AsciiPrefixTokenExecutor.TryMatchAnchoredPrefixUntilByte(input, _prefixUntilByteLiteral!, _prefixUntilByteTerminator, out var docLineLength))
        {
            return new Utf8ValueMatch(true, true, 0, docLineLength, 0, docLineLength);
        }

        var index = Utf8ByteSafeLinearExecutor.FindNext(input, _regexPlan, _verifierRuntime.StructuralVerifierRuntime, 0, budget, out var matchedLength);
        return index < 0
            ? Utf8ValueMatch.NoMatch
            : new Utf8ValueMatch(true, true, index, matchedLength, index, matchedLength);
    }

    public override Utf8ValueMatchEnumerator CreateMatchEnumerator(ReadOnlySpan<byte> input, Utf8ValidationResult validation, Utf8ExecutionBudget? budget)
    {
        var analysis = Utf8InputAnalyzer.Analyze(input);
        return new Utf8ValueMatchEnumerator(input, Encoding.UTF8.GetString(input), _verifierRuntime.FallbackCandidateVerifier.FallbackRegex, analysis.BoundaryMap);
    }

    public override Utf8ValueSplitEnumerator CreateSplitEnumerator(ReadOnlySpan<byte> input, Utf8ValidationResult validation, int count, Utf8ExecutionBudget? budget)
    {
        var analysis = Utf8InputAnalyzer.Analyze(input);
        return new Utf8ValueSplitEnumerator(input, Encoding.UTF8.GetString(input), _verifierRuntime.FallbackCandidateVerifier.FallbackRegex, count, analysis.BoundaryMap);
    }

}

internal sealed class Utf8SearchGuidedFallbackCompiledEngineRuntime : Utf8CompiledEngineRuntime
{
    private readonly Utf8RegexPlan _regexPlan;
    private readonly Utf8VerifierRuntime _verifierRuntime;
    private readonly Utf8CompiledExecutionBackend _backend;
    private readonly Utf8StructuralSearchPlan[] _candidatePlans;
    private readonly Utf8EmittedSearchGuidedFallback? _emittedBackend;

    public Utf8SearchGuidedFallbackCompiledEngineRuntime(Utf8CompiledEngine compiledEngine, Utf8NonLiteralCompiledEngineRuntime inner)
    {
        _regexPlan = inner.RegexPlan;
        _verifierRuntime = inner.VerifierRuntime;
        _backend = compiledEngine.Backend;
        _candidatePlans = _regexPlan.SearchPlan.FallbackSearch.CandidatePlans ?? [];
        _emittedBackend = _backend == Utf8CompiledExecutionBackend.EmittedInstruction &&
                          Utf8EmittedSearchGuidedFallback.TryCreate(_regexPlan, _verifierRuntime, out var emitted)
            ? emitted
            : null;
    }

    public override bool IsMatch(ReadOnlySpan<byte> input, Utf8ValidationResult validation, Utf8ExecutionBudget? budget)
    {
        if (Utf8SearchGuidedFallbackCompiledPolicy.ShouldBypassIsMatch(_regexPlan))
        {
            Utf8SearchDiagnosticsSession.Current?.MarkExecutionRoute("fallback_direct_regex");
            return _verifierRuntime.FallbackCandidateVerifier.FallbackRegex.IsMatch(Encoding.UTF8.GetString(input));
        }

        Utf8SearchDiagnosticsSession.Current?.MarkExecutionRoute("fallback_search_guided");
        if (Utf8SearchGuidedFallbackCompiledPolicy.CanUseEmittedBackend(_emittedBackend, budget))
        {
            return _emittedBackend!.IsMatch(input);
        }

        var probe = GetProbeValidation(input, validation);
        Utf8BoundaryMap? boundaryMap = null;
        string? decoded = null;
        return TryFindNextVerifiedMatch(input, probe, 0, ref boundaryMap, ref decoded, out _);
    }

    public override int Count(ReadOnlySpan<byte> input, Utf8ValidationResult validation, Utf8ExecutionBudget? budget)
    {
        Utf8SearchDiagnosticsSession.Current?.MarkExecutionRoute("fallback_search_guided");
        if (Utf8SearchGuidedFallbackCompiledPolicy.CanUseEmittedBackend(_emittedBackend, budget))
        {
            return _emittedBackend!.Count(input);
        }

        var probe = GetProbeValidation(input, validation);
        Utf8BoundaryMap? boundaryMap = null;
        string? decoded = null;
        var count = 0;
        var startIndex = 0;
        var verifierCount = 0;
        var verifierInvocations = 0;
        while ((uint)startIndex <= (uint)input.Length &&
            TryFindNextVerifiedMatch(input, probe, startIndex, ref boundaryMap, ref decoded, out var verification))
        {
            Utf8SearchDiagnosticsSession.Current?.CountVerifierMatch();
            count++;
            verifierCount++;
            verifierInvocations = Utf8SearchDiagnosticsSession.Current?.VerifierInvocations ?? 0;
            if (Utf8SearchGuidedFallbackCompiledPolicy.ShouldDemoteToFallbackCount(verifierCount))
            {
                Utf8SearchDiagnosticsSession.Current?.MarkExecutionRoute("fallback_search_guided_demoted");
                return _verifierRuntime.FallbackCandidateVerifier.FallbackRegex.Count(Encoding.UTF8.GetString(input));
            }

            if (Utf8SearchGuidedFallbackCompiledPolicy.ShouldDemoteToFallbackCountByInvocations())
            {
                Utf8SearchDiagnosticsSession.Current?.MarkExecutionRoute("fallback_search_guided_demoted");
                return _verifierRuntime.FallbackCandidateVerifier.FallbackRegex.Count(Encoding.UTF8.GetString(input));
            }

            startIndex = verification.IndexInBytes + Math.Max(verification.LengthInBytes, 1);
        }

        return count;
    }

    public override Utf8ValueMatch Match(ReadOnlySpan<byte> input, Utf8ValidationResult validation, Utf8ExecutionBudget? budget)
    {
        var probe = GetProbeValidation(input, validation);
        Utf8BoundaryMap? boundaryMap = null;
        string? decoded = null;
        if (!TryFindNextVerifiedMatch(input, probe, 0, ref boundaryMap, ref decoded, out var verification))
        {
            return Utf8ValueMatch.NoMatch;
        }

        Utf8SearchDiagnosticsSession.Current?.CountVerifierMatch();
        return new Utf8ValueMatch(
            verification.Success,
            verification.IsByteAligned,
            verification.IndexInUtf16,
            verification.LengthInUtf16,
            verification.IndexInBytes,
            verification.LengthInBytes);
    }

    public override Utf8ValueMatchEnumerator CreateMatchEnumerator(ReadOnlySpan<byte> input, Utf8ValidationResult validation, Utf8ExecutionBudget? budget)
    {
        return Utf8CompiledFallbackEnumeratorFactory.CreateMatchEnumerator(input, _verifierRuntime.FallbackCandidateVerifier.FallbackRegex);
    }

    public override Utf8ValueSplitEnumerator CreateSplitEnumerator(ReadOnlySpan<byte> input, Utf8ValidationResult validation, int count, Utf8ExecutionBudget? budget)
    {
        return Utf8CompiledFallbackEnumeratorFactory.CreateSplitEnumerator(input, _verifierRuntime.FallbackCandidateVerifier.FallbackRegex, count);
    }

    private bool TryFindNextVerifiedMatch(
        ReadOnlySpan<byte> input,
        Utf8ValidationResult probe,
        int startIndex,
        ref Utf8BoundaryMap? boundaryMap,
        ref string? decoded,
        out Utf8FallbackVerificationResult verification)
    {
        return Utf8FallbackSearchExecutor.TryFindNextVerifiedMatch(
            _regexPlan.SearchPlan,
            _verifierRuntime,
            input,
            probe,
            startIndex,
            ref boundaryMap,
            ref decoded,
            out verification);
    }

}

internal sealed class Utf8CompiledFallbackCompiledEngineRuntime : Utf8CompiledEngineRuntime
{
    private readonly Utf8RegexPlan _regexPlan;
    private readonly Utf8VerifierRuntime _verifierRuntime;
    private readonly Utf8StructuralSearchPlan[] _candidatePlans;
    private readonly Utf8ExecutionProgram _program;

    public Utf8CompiledFallbackCompiledEngineRuntime(Utf8NonLiteralCompiledEngineRuntime inner)
    {
        _regexPlan = inner.RegexPlan;
        _verifierRuntime = inner.VerifierRuntime;
        _candidatePlans = _regexPlan.SearchPlan.FallbackSearch.CandidatePlans ?? [];
        _program = _regexPlan.ExecutionProgram
            ?? throw new InvalidOperationException("Compiled fallback requires an execution program.");
    }

    public override bool SupportsWellFormedOnlyCount => true;
    public override bool IsMatch(ReadOnlySpan<byte> input, Utf8ValidationResult validation, Utf8ExecutionBudget? budget)
    {
        Utf8SearchDiagnosticsSession.Current?.MarkExecutionRoute("fallback_compiled");
        Utf8BoundaryMap? boundaryMap = null;
        string? decoded = null;
        var probe = GetProbeValidation(input, validation);
        return TryFindNextMatch(input, probe, 0, ref boundaryMap, ref decoded, budget, out _);
    }

    public override int Count(ReadOnlySpan<byte> input, Utf8ValidationResult validation, Utf8ExecutionBudget? budget)
    {
        Utf8SearchDiagnosticsSession.Current?.MarkExecutionRoute("fallback_compiled");
        var count = 0;
        var startIndex = 0;
        Utf8BoundaryMap? boundaryMap = null;
        string? decoded = null;
        var probe = GetProbeValidation(input, validation);
        while ((uint)startIndex <= (uint)input.Length &&
               TryFindNextMatch(input, probe, startIndex, ref boundaryMap, ref decoded, budget, out var match))
        {
            count++;
            startIndex = match.IndexInBytes + Math.Max(match.LengthInBytes, 1);
        }

        return count;
    }

    public override Utf8ValueMatch Match(ReadOnlySpan<byte> input, Utf8ValidationResult validation, Utf8ExecutionBudget? budget)
    {
        return Utf8CompiledFallbackMatchProjection.Match(input, _verifierRuntime.FallbackCandidateVerifier.FallbackRegex);
    }

    public override Utf8ValueMatchEnumerator CreateMatchEnumerator(ReadOnlySpan<byte> input, Utf8ValidationResult validation, Utf8ExecutionBudget? budget)
    {
        return Utf8CompiledFallbackEnumeratorFactory.CreateMatchEnumerator(input, _verifierRuntime.FallbackCandidateVerifier.FallbackRegex);
    }

    public override Utf8ValueSplitEnumerator CreateSplitEnumerator(ReadOnlySpan<byte> input, Utf8ValidationResult validation, int count, Utf8ExecutionBudget? budget)
    {
        return Utf8CompiledFallbackEnumeratorFactory.CreateSplitEnumerator(input, _verifierRuntime.FallbackCandidateVerifier.FallbackRegex, count);
    }

    private bool TryFindNextMatch(
        ReadOnlySpan<byte> input,
        Utf8ValidationResult validation,
        int startIndex,
        ref Utf8BoundaryMap? boundaryMap,
        ref string? decoded,
        Utf8ExecutionBudget? budget,
        out Utf8ValueMatch match)
    {
        return Utf8CompiledFallbackMatchRouter.TryFindNextMatch(
            _candidatePlans,
            _verifierRuntime,
            _program,
            input,
            validation,
            startIndex,
            ref boundaryMap,
            ref decoded,
            budget,
            out match);
    }
}

internal sealed class Utf8FallbackRegexCompiledEngineRuntime : Utf8CompiledEngineRuntime
{
    private readonly Utf8RegexPlan _regexPlan;
    private readonly Utf8VerifierRuntime _verifierRuntime;
    private readonly Utf8FallbackDirectFamilyPlan _directFamily;
    private readonly Utf8AsciiLiteralFinder _linePrefixFinder;
    private readonly Utf8EmittedTokenFamilyMatcher? _emittedTokenFamilyMatcher;
    private readonly PreparedAsciiDelimitedTokenSearch _delimitedTokenSearch;
    private readonly PreparedAsciiLiteralStructuredTokenSearch _literalStructuredTokenSearch;

    public Utf8FallbackRegexCompiledEngineRuntime(Utf8NonLiteralCompiledEngineRuntime inner)
    {
        _regexPlan = inner.RegexPlan;
        _verifierRuntime = inner.VerifierRuntime;
        _directFamily = inner.RegexPlan.FallbackDirectFamily.HasValue
            ? inner.RegexPlan.FallbackDirectFamily
            : Utf8FallbackRegexFamilyAnalyzer.ClassifyPattern(
                inner.VerifierRuntime.FallbackCandidateVerifier.FallbackRegex.ToString(),
                inner.VerifierRuntime.FallbackCandidateVerifier.FallbackRegex.ToString(),
                NormalizeDirectRouteOptions(inner.VerifierRuntime.FallbackCandidateVerifier.FallbackRegex.Options),
                inner.RegexPlan.SearchPlan.RequiredPrefilterLiteralUtf8);
        _linePrefixFinder = _directFamily.LiteralUtf8 is { Length: > 0 } literal
            ? new Utf8AsciiLiteralFinder(literal)
            : default;
        _emittedTokenFamilyMatcher = Utf8EmittedTokenFamilyMatcher.TryCreate(_directFamily, out var emittedTokenFamilyMatcher)
            ? emittedTokenFamilyMatcher
            : null;
        _delimitedTokenSearch = Utf8FallbackPreparedTokenSearchFactory.CreateDelimitedTokenSearch(_directFamily);
        _literalStructuredTokenSearch = Utf8FallbackPreparedTokenSearchFactory.CreateLiteralStructuredTokenSearch(_directFamily);
    }

    public override bool SupportsAsciiWellFormedOnlyMatch => Utf8FallbackDirectFamilyRuntimePolicy.SupportsAsciiWellFormedOnlyMatch(_directFamily);

    public override bool SupportsWellFormedOnlyMatch => Utf8FallbackDirectFamilyRuntimePolicy.SupportsWellFormedOnlyMatch(_directFamily);

    public override bool WellFormedOnlyMatchMissIsDefinitive => SupportsWellFormedOnlyMatch;
    public override bool SkipRequiredPrefilterForMatch => Utf8FallbackDirectFamilyRuntimePolicy.SkipRequiredPrefilterForMatch(_directFamily);

    public override bool SupportsWellFormedOnlyCount => true;
    public override bool SupportsThrowIfInvalidOnlyCount => Utf8FallbackDirectFamilyRuntimePolicy.SupportsThrowIfInvalidOnlyCount(_directFamily);
    public override bool PreferValidateOnlyCount => _directFamily.Kind == Utf8FallbackDirectFamilyKind.AnchoredQuotedLineSegmentCount;

    public override bool SkipRequiredPrefilterForCount => Utf8FallbackDirectFamilyRuntimePolicy.SkipRequiredPrefilterForCount(_directFamily);

    public override bool TryMatchAsciiWellFormedOnly(ReadOnlySpan<byte> input, out Utf8ValueMatch match)
    {
        return Utf8FallbackDirectFamilyMatchRouter.TryMatchAsciiWellFormedOnly(
            input,
            _directFamily,
            _emittedTokenFamilyMatcher,
            _delimitedTokenSearch,
            _literalStructuredTokenSearch,
            out match);
    }

    public override bool TryMatchWellFormedOnly(ReadOnlySpan<byte> input, out Utf8ValueMatch match)
    {
        return Utf8FallbackDirectFamilyMatchRouter.TryMatchWellFormedOnly(
            input,
            _directFamily,
            _emittedTokenFamilyMatcher,
            _delimitedTokenSearch,
            _literalStructuredTokenSearch,
            out match);
    }

    public override bool IsMatch(ReadOnlySpan<byte> input, Utf8ValidationResult validation, Utf8ExecutionBudget? budget)
    {
        if (validation.IsAscii &&
            TryMatchDirectAsciiFamily(input, out _, out _))
        {
            return true;
        }

        return _verifierRuntime.FallbackCandidateVerifier.FallbackRegex.IsMatch(Encoding.UTF8.GetString(input));
    }

    public override int Count(ReadOnlySpan<byte> input, Utf8ValidationResult validation, Utf8ExecutionBudget? budget)
    {
        if (Utf8FallbackDirectFamilyCountRouter.TryCount(
            input,
            validation,
            _directFamily,
            _emittedTokenFamilyMatcher,
            _linePrefixFinder,
            _delimitedTokenSearch,
            _literalStructuredTokenSearch,
            _verifierRuntime.FallbackCandidateVerifier.FallbackRegex,
            out var directCount))
        {
            return directCount;
        }

        Utf8SearchDiagnosticsSession.Current?.MarkExecutionRoute("fallback_direct_regex");
        return _verifierRuntime.FallbackCandidateVerifier.FallbackRegex.Count(Encoding.UTF8.GetString(input));
    }

    public override Utf8ValueMatch Match(ReadOnlySpan<byte> input, Utf8ValidationResult validation, Utf8ExecutionBudget? budget)
    {
        if (validation.IsAscii &&
            TryMatchDirectAsciiFamily(input, out var matchIndex, out var matchedLength))
        {
            return new Utf8ValueMatch(true, true, matchIndex, matchedLength, matchIndex, matchedLength);
        }

        var decoded = Encoding.UTF8.GetString(input);
        var match = _verifierRuntime.FallbackCandidateVerifier.FallbackRegex.Match(decoded);
        if (!match.Success)
        {
            return Utf8ValueMatch.NoMatch;
        }

        if (validation.IsAscii)
        {
            return Utf8ProjectionExecutor.ProjectByteAlignedMatch(match.Index, match.Length);
        }

        var boundaryMap = Utf8InputAnalyzer.Analyze(input).BoundaryMap;
        return Utf8ProjectionExecutor.ProjectFallbackRegexMatch(input, match, boundaryMap);
    }

    public override bool TryMatchWithoutValidation(ReadOnlySpan<byte> input, Utf8ExecutionBudget? budget, out Utf8ValueMatch match)
    {
        return Utf8FallbackDirectFamilyRuntimePolicy.TryMatchWithoutValidation(
            input,
            _directFamily,
            _delimitedTokenSearch,
            _literalStructuredTokenSearch,
            out match);
    }

    public override Utf8ValueMatchEnumerator CreateMatchEnumerator(ReadOnlySpan<byte> input, Utf8ValidationResult validation, Utf8ExecutionBudget? budget)
    {
        var analysis = Utf8InputAnalyzer.Analyze(input);
        return new Utf8ValueMatchEnumerator(input, Encoding.UTF8.GetString(input), _verifierRuntime.FallbackCandidateVerifier.FallbackRegex, analysis.BoundaryMap);
    }

    public override Utf8ValueSplitEnumerator CreateSplitEnumerator(ReadOnlySpan<byte> input, Utf8ValidationResult validation, int count, Utf8ExecutionBudget? budget)
    {
        var analysis = Utf8InputAnalyzer.Analyze(input);
        return new Utf8ValueSplitEnumerator(input, Encoding.UTF8.GetString(input), _verifierRuntime.FallbackCandidateVerifier.FallbackRegex, count, analysis.BoundaryMap);
    }

    private bool TryMatchDirectAsciiFamily(ReadOnlySpan<byte> input, out int matchIndex, out int matchedLength)
    {
        return Utf8AsciiDirectFamilyExecutor.TryFindMatch(
            input,
            _directFamily,
            _emittedTokenFamilyMatcher,
            _delimitedTokenSearch,
            _literalStructuredTokenSearch,
            out matchIndex,
            out matchedLength);
    }
}
