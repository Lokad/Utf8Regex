using Lokad.Utf8Regex.Internal.Diagnostics;
using Lokad.Utf8Regex.Internal.Input;
using Lokad.Utf8Regex.Internal.Planning;
using System.Text;
using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex.Internal.Execution;

internal readonly record struct Utf8CompiledRuntimeCapabilities(
    bool SupportsAsciiWellFormedOnlyMatch,
    bool SupportsWellFormedOnlyMatch,
    bool WellFormedOnlyMatchMissIsDefinitive,
    bool SupportsWellFormedOnlyCount,
    bool SupportsThrowIfInvalidOnlyCount,
    bool PreferValidateOnlyCount,
    bool SkipRequiredPrefilterForMatch,
    bool SkipRequiredPrefilterForCount,
    bool UsesEmittedAnchoredValidatorMatcher,
    bool UsesEmittedKernelMatcher);

internal interface IUtf8AsciiWellFormedMatchRuntime
{
    /// <summary>
    /// Attempts a match after the caller has proved that the subject is ASCII
    /// and well formed. A false result is definitive only when the runtime's
    /// capability record says so.
    /// </summary>
    bool TryMatchAsciiWellFormed(ReadOnlySpan<byte> input, out Utf8ValueMatch match);
}

internal interface IUtf8WellFormedMatchRuntime
{
    /// <summary>
    /// Attempts a match after the caller has validated the complete subject.
    /// A false result is definitive only when the capability record says so.
    /// </summary>
    bool TryMatchWellFormed(ReadOnlySpan<byte> input, out Utf8ValueMatch match);
}

internal interface IUtf8UnvalidatedMatchRuntime
{
    /// <summary>
    /// Attempts a byte-safe match without validation. The implementation must
    /// charge the supplied deadline and return false when it cannot safely
    /// decide the operation; it must not treat malformed UTF-8 as accepted.
    /// </summary>
    bool ExecuteMatchWithoutValidation(
        ReadOnlySpan<byte> input,
        Utf8ExecutionDeadline budget,
        out Utf8ValueMatch match);
}

internal abstract class Utf8CompiledEngineRuntime
{
    protected static Utf8ValidationResult GetProbeValidation(ReadOnlySpan<byte> input, Utf8ValidationResult validation)
    {
        return validation.ByteLength == input.Length
            ? validation
            : Utf8InputAnalyzer.ValidateOnly(input);
    }

    /// <summary>
    /// Describes optional whole-operation shortcuts implemented by this
    /// runtime. A false member means that the canonical validated operation
    /// path must be used; it is never permission to skip validation.
    /// </summary>
    public virtual Utf8CompiledRuntimeCapabilities Capabilities => default;

    public bool SupportsAsciiWellFormedOnlyMatch => Capabilities.SupportsAsciiWellFormedOnlyMatch;
    public bool SupportsWellFormedOnlyMatch => Capabilities.SupportsWellFormedOnlyMatch;
    public bool WellFormedOnlyMatchMissIsDefinitive => Capabilities.WellFormedOnlyMatchMissIsDefinitive;
    public bool SupportsWellFormedOnlyCount => Capabilities.SupportsWellFormedOnlyCount;
    public bool SupportsThrowIfInvalidOnlyCount => Capabilities.SupportsThrowIfInvalidOnlyCount;
    public bool PreferValidateOnlyCount => Capabilities.PreferValidateOnlyCount;
    public bool SkipRequiredPrefilterForMatch => Capabilities.SkipRequiredPrefilterForMatch;
    public bool SkipRequiredPrefilterForCount => Capabilities.SkipRequiredPrefilterForCount;
    public bool UsesEmittedAnchoredValidatorMatcher => Capabilities.UsesEmittedAnchoredValidatorMatcher;
    public bool UsesEmittedKernelMatcher => Capabilities.UsesEmittedKernelMatcher;

    public bool TryMatchAsciiWellFormedOnly(ReadOnlySpan<byte> input, out Utf8ValueMatch match)
    {
        if (this is IUtf8AsciiWellFormedMatchRuntime runtime)
        {
            return runtime.TryMatchAsciiWellFormed(input, out match);
        }

        match = Utf8ValueMatch.NoMatch;
        return false;
    }

    public bool TryMatchWellFormedOnly(ReadOnlySpan<byte> input, out Utf8ValueMatch match)
    {
        if (this is IUtf8WellFormedMatchRuntime runtime)
        {
            return runtime.TryMatchWellFormed(input, out match);
        }

        match = Utf8ValueMatch.NoMatch;
        return false;
    }

    /// <summary>
    /// Evaluates boolean match semantics for a complete subject. The caller
    /// owns validation and supplies either its result or the documented
    /// well-formed shortcut sentinel. Implementations must charge the deadline
    /// and a false result is definitive.
    /// </summary>
    public abstract bool IsMatch(ReadOnlySpan<byte> input, Utf8ValidationResult validation, Utf8ExecutionDeadline budget);

    /// <summary>
    /// Counts nonoverlapping matches using .NET global-progression semantics.
    /// Validation is caller-owned, every scan charges the deadline, and the
    /// returned count is complete rather than a lower bound.
    /// </summary>
    public abstract int Count(ReadOnlySpan<byte> input, Utf8ValidationResult validation, Utf8ExecutionDeadline budget);

    /// <summary>
    /// Returns the first capture-zero match for the supplied validated subject,
    /// or <see cref="Utf8ValueMatch.NoMatch"/>. Byte and UTF-16 coordinates
    /// must describe the same match and all work charges the deadline.
    /// </summary>
    public abstract Utf8ValueMatch Match(ReadOnlySpan<byte> input, Utf8ValidationResult validation, Utf8ExecutionDeadline budget);

    public bool TryMatchWithoutValidation(ReadOnlySpan<byte> input, Utf8ExecutionDeadline budget, out Utf8ValueMatch match)
    {
        if (this is IUtf8UnvalidatedMatchRuntime runtime)
        {
            return runtime.ExecuteMatchWithoutValidation(input, budget, out match);
        }

        match = Utf8ValueMatch.NoMatch;
        return false;
    }

    public bool TryDebugCountExactUtf8LiteralValidatedThreeByte(ReadOnlySpan<byte> input, out int count)
        => Utf8CompiledRuntimeInspection.TryCountValidatedThreeByte(this, input, out count);

    public bool TryDebugCountExactUtf8LiteralLeadingScalarAnchored(ReadOnlySpan<byte> input, out int count)
        => Utf8CompiledRuntimeInspection.TryCountLeadingScalarAnchored(this, input, out count);

    public bool TryDebugCountExactUtf8LiteralPreparedSearch(ReadOnlySpan<byte> input, out int count)
        => Utf8CompiledRuntimeInspection.TryCountPreparedSearch(this, input, out count);

    public bool TryDebugCountExactUtf8LiteralAnchored(ReadOnlySpan<byte> input, out int count)
        => Utf8CompiledRuntimeInspection.TryCountAnchored(this, input, out count);

    public bool TryDebugMatchAsciiLiteralFamilyRaw(ReadOnlySpan<byte> input, out int index, out int matchedByteLength)
        => Utf8CompiledRuntimeInspection.TryMatchAsciiLiteralFamily(this, input, out index, out matchedByteLength);

    public bool TryDebugIsMatchLiteralFamily(ReadOnlySpan<byte> input, out bool isMatch)
        => Utf8CompiledRuntimeInspection.TryIsMatchLiteralFamily(this, input, out isMatch);

    public static Utf8CompiledEngineRuntime Create(Utf8PreparedRegex regexPlan, Utf8VerifierRuntime verifierRuntime, RegexOptions options)
    {
        return Create(Utf8CompiledEngineSelector.Select(regexPlan), regexPlan, verifierRuntime, options);
    }

    public static Utf8CompiledEngineRuntime Create(Utf8CompiledEngine compiledEngine, Utf8PreparedRegex regexPlan, Utf8VerifierRuntime verifierRuntime, RegexOptions options)
        => Utf8CompiledRuntimeFactory.Create(compiledEngine, regexPlan, verifierRuntime, options);

}

internal sealed class Utf8ExactLiteralCompiledEngineRuntime : Utf8CompiledEngineRuntime,
    IUtf8AsciiWellFormedMatchRuntime,
    IUtf8WellFormedMatchRuntime,
    IUtf8UnvalidatedMatchRuntime
{
    private readonly Utf8LiteralCompiledEngineRuntime _inner;

    public Utf8ExactLiteralCompiledEngineRuntime(Utf8LiteralCompiledEngineRuntime inner) => _inner = inner;

    internal Utf8LiteralCompiledEngineRuntime Inner => _inner;

    public override Utf8CompiledRuntimeCapabilities Capabilities => _inner.Capabilities;
    public bool TryMatchAsciiWellFormed(ReadOnlySpan<byte> input, out Utf8ValueMatch match) => _inner.TryMatchAsciiWellFormedOnly(input, out match);
    public bool TryMatchWellFormed(ReadOnlySpan<byte> input, out Utf8ValueMatch match) => _inner.TryMatchWellFormedOnly(input, out match);
    public override bool IsMatch(ReadOnlySpan<byte> input, Utf8ValidationResult validation, Utf8ExecutionDeadline budget) => _inner.IsMatch(input, validation, budget);
    public override int Count(ReadOnlySpan<byte> input, Utf8ValidationResult validation, Utf8ExecutionDeadline budget) => _inner.Count(input, validation, budget);
    public override Utf8ValueMatch Match(ReadOnlySpan<byte> input, Utf8ValidationResult validation, Utf8ExecutionDeadline budget) => _inner.Match(input, validation, budget);
    public bool ExecuteMatchWithoutValidation(ReadOnlySpan<byte> input, Utf8ExecutionDeadline budget, out Utf8ValueMatch match) => _inner.TryMatchWithoutValidation(input, budget, out match);
}

internal sealed class Utf8LiteralFamilyCompiledEngineRuntime : Utf8CompiledEngineRuntime,
    IUtf8AsciiWellFormedMatchRuntime,
    IUtf8WellFormedMatchRuntime,
    IUtf8UnvalidatedMatchRuntime
{
    private readonly Utf8LiteralCompiledEngineRuntime _inner;

    public Utf8LiteralFamilyCompiledEngineRuntime(Utf8LiteralCompiledEngineRuntime inner) => _inner = inner;

    internal Utf8LiteralCompiledEngineRuntime Inner => _inner;

    public override Utf8CompiledRuntimeCapabilities Capabilities => _inner.Capabilities;
    public bool TryMatchAsciiWellFormed(ReadOnlySpan<byte> input, out Utf8ValueMatch match) => _inner.TryMatchAsciiWellFormedOnly(input, out match);
    public bool TryMatchWellFormed(ReadOnlySpan<byte> input, out Utf8ValueMatch match) => _inner.TryMatchWellFormedOnly(input, out match);
    public override bool IsMatch(ReadOnlySpan<byte> input, Utf8ValidationResult validation, Utf8ExecutionDeadline budget) => _inner.IsMatch(input, validation, budget);
    public override int Count(ReadOnlySpan<byte> input, Utf8ValidationResult validation, Utf8ExecutionDeadline budget) => _inner.Count(input, validation, budget);
    public override Utf8ValueMatch Match(ReadOnlySpan<byte> input, Utf8ValidationResult validation, Utf8ExecutionDeadline budget) => _inner.Match(input, validation, budget);
    public bool ExecuteMatchWithoutValidation(ReadOnlySpan<byte> input, Utf8ExecutionDeadline budget, out Utf8ValueMatch match) => _inner.TryMatchWithoutValidation(input, budget, out match);
}

internal sealed class Utf8StructuralFamilyCompiledEngineRuntime : Utf8CompiledEngineRuntime
{
    private readonly Utf8PreparedRegex _regexPlan;
    private readonly Utf8VerifierRuntime _verifierRuntime;

    public Utf8StructuralFamilyCompiledEngineRuntime(Utf8NonLiteralCompiledEngineRuntime inner)
    {
        _regexPlan = inner.PreparedRegex;
        _verifierRuntime = inner.VerifierRuntime;
    }

    public override bool IsMatch(ReadOnlySpan<byte> input, Utf8ValidationResult validation, Utf8ExecutionDeadline budget)
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

    public override int Count(ReadOnlySpan<byte> input, Utf8ValidationResult validation, Utf8ExecutionDeadline budget)
    {
        Utf8SearchDiagnosticsSession.Current?.MarkExecutionRoute(Utf8ExecutionRoute.NativeStructuralFamily);
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

    public override Utf8ValueMatch Match(ReadOnlySpan<byte> input, Utf8ValidationResult validation, Utf8ExecutionDeadline budget)
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

    private Utf8ValueMatch MatchFallback(ReadOnlySpan<byte> input)
    {
        return Utf8CompiledFallbackMatchProjection.Match(input, _verifierRuntime.FallbackCandidateVerifier.FallbackRegex);
    }

    private bool UsesRightToLeft()
    {
        return (_verifierRuntime.FallbackCandidateVerifier.FallbackRegex.Options & RegexOptions.RightToLeft) != 0;
    }
}

internal sealed class Utf8SimplePatternCompiledEngineRuntime : Utf8CompiledEngineRuntime,
    IUtf8WellFormedMatchRuntime,
    IUtf8UnvalidatedMatchRuntime
{
    private readonly Utf8PreparedRegex _regexPlan;
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
        _regexPlan = inner.PreparedRegex;
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

    public override Utf8CompiledRuntimeCapabilities Capabilities => new(
        SupportsAsciiWellFormedOnlyMatch: _canUseDirectAnchoredValidatorMatch,
        SupportsWellFormedOnlyMatch: _canUseDirectAnchoredValidatorMatch,
        WellFormedOnlyMatchMissIsDefinitive: _canUseDirectAnchoredValidatorMatch,
        SupportsWellFormedOnlyCount: false,
        SupportsThrowIfInvalidOnlyCount: false,
        PreferValidateOnlyCount: false,
        SkipRequiredPrefilterForMatch: _compiledPatternFamily.Category == Utf8CompiledPatternCategory.AnchoredWhole,
        SkipRequiredPrefilterForCount: false,
        UsesEmittedAnchoredValidatorMatcher: _emittedAnchoredValidatorMatcher is not null,
        UsesEmittedKernelMatcher: false);

    public bool TryMatchWellFormed(ReadOnlySpan<byte> input, out Utf8ValueMatch match)
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

    public bool ExecuteMatchWithoutValidation(ReadOnlySpan<byte> input, Utf8ExecutionDeadline budget, out Utf8ValueMatch match)
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

    public override bool IsMatch(ReadOnlySpan<byte> input, Utf8ValidationResult validation, Utf8ExecutionDeadline budget)
    {
        if (_regexPlan.StructuralLinearProgram.Kind == Utf8StructuralLinearProgramKind.AsciiFixedTokenPattern)
        {
            return _linearRuntime.IsMatch(input, validation, _verifierRuntime, budget);
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

    public override int Count(ReadOnlySpan<byte> input, Utf8ValidationResult validation, Utf8ExecutionDeadline budget)
    {
        if (_regexPlan.StructuralLinearProgram.Kind == Utf8StructuralLinearProgramKind.AsciiFixedTokenPattern)
        {
            return _linearRuntime.Count(input, validation, _verifierRuntime, budget);
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
                Utf8SearchDiagnosticsSession.Current?.MarkExecutionRoute(Utf8ExecutionRoute.NativeAsciiAnchoredValidator);
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
            Utf8SearchDiagnosticsSession.Current?.MarkExecutionRoute(Utf8ExecutionRoute.NativeAsciiSimplePatternFixedAlternation);
            return 1;
        }

        if (Utf8SimplePatternCompiledWholeMatcher.TryMatchDirectAnchoredFixedLengthSimplePattern(_regexPlan, input, out _))
        {
            Utf8SearchDiagnosticsSession.Current?.MarkExecutionRoute(Utf8ExecutionRoute.NativeAsciiSimplePatternFixedLength);
            return 1;
        }

        Utf8SearchDiagnosticsSession.Current?.MarkExecutionRoute(Utf8ExecutionRoute.NativeAsciiSimplePattern);
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

    public override Utf8ValueMatch Match(ReadOnlySpan<byte> input, Utf8ValidationResult validation, Utf8ExecutionDeadline budget)
    {
        if (_regexPlan.StructuralLinearProgram.Kind == Utf8StructuralLinearProgramKind.AsciiFixedTokenPattern)
        {
            return _linearRuntime.Match(input, validation, _verifierRuntime, budget);
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

}

internal sealed class Utf8StructuralLinearAutomatonCompiledEngineRuntime : Utf8CompiledEngineRuntime
{
    private readonly Utf8PreparedRegex _regexPlan;
    private readonly Utf8StructuralLinearRuntime _linearRuntime;
    private readonly Utf8VerifierRuntime _verifierRuntime;
    private readonly bool _emitEnabled;
    private readonly Utf8EmittedDeterministicMatcher? _emittedDeterministicMatcher;
    private readonly Utf8EmittedKernelMatcher? _emittedKernelMatcher;
    private readonly PreparedAsciiFindPlan _orderedWindowTrailingFindPlan;

    public Utf8StructuralLinearAutomatonCompiledEngineRuntime(Utf8NonLiteralCompiledEngineRuntime inner, bool emitEnabled)
    {
        _regexPlan = inner.PreparedRegex;
        _linearRuntime = Utf8StructuralLinearRuntime.Create(inner.PreparedRegex.StructuralLinearProgram);
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

    public override Utf8CompiledRuntimeCapabilities Capabilities => new(
        SupportsAsciiWellFormedOnlyMatch: false,
        SupportsWellFormedOnlyMatch: false,
        WellFormedOnlyMatchMissIsDefinitive: false,
        SupportsWellFormedOnlyCount: false,
        SupportsThrowIfInvalidOnlyCount: false,
        PreferValidateOnlyCount: false,
        SkipRequiredPrefilterForMatch: false,
        SkipRequiredPrefilterForCount: false,
        UsesEmittedAnchoredValidatorMatcher: false,
        UsesEmittedKernelMatcher: _emittedKernelMatcher is not null);

    public override bool IsMatch(ReadOnlySpan<byte> input, Utf8ValidationResult validation, Utf8ExecutionDeadline budget)
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

    public override int Count(ReadOnlySpan<byte> input, Utf8ValidationResult validation, Utf8ExecutionDeadline budget)
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

    public override Utf8ValueMatch Match(ReadOnlySpan<byte> input, Utf8ValidationResult validation, Utf8ExecutionDeadline budget)
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

}

internal sealed class Utf8ByteSafeLinearCompiledEngineRuntime : Utf8CompiledEngineRuntime,
    IUtf8AsciiWellFormedMatchRuntime,
    IUtf8UnvalidatedMatchRuntime
{
    private readonly Utf8PreparedRegex _regexPlan;
    private readonly Utf8VerifierRuntime _verifierRuntime;
    private readonly bool _canUseDirectPrefixUntilByteMatch;
    private readonly byte[] _prefixUntilByteLiteral;
    private readonly byte _prefixUntilByteTerminator;

    public Utf8ByteSafeLinearCompiledEngineRuntime(Utf8NonLiteralCompiledEngineRuntime inner)
    {
        _regexPlan = inner.PreparedRegex;
        _verifierRuntime = inner.VerifierRuntime;
        var directFamily = inner.PreparedRegex.FallbackDirectFamily;
        _canUseDirectPrefixUntilByteMatch = directFamily.Kind == Utf8FallbackDirectFamilyKind.AnchoredPrefixUntilByte &&
            directFamily.LiteralUtf8 is { Length: > 0 };
        _prefixUntilByteLiteral = directFamily.LiteralUtf8;
        _prefixUntilByteTerminator = directFamily.TerminatorByte;
    }

    public override Utf8CompiledRuntimeCapabilities Capabilities => new(
        SupportsAsciiWellFormedOnlyMatch: _canUseDirectPrefixUntilByteMatch,
        SupportsWellFormedOnlyMatch: _canUseDirectPrefixUntilByteMatch,
        WellFormedOnlyMatchMissIsDefinitive: _canUseDirectPrefixUntilByteMatch,
        SupportsWellFormedOnlyCount: false,
        SupportsThrowIfInvalidOnlyCount: false,
        PreferValidateOnlyCount: false,
        SkipRequiredPrefilterForMatch: _canUseDirectPrefixUntilByteMatch,
        SkipRequiredPrefilterForCount: false,
        UsesEmittedAnchoredValidatorMatcher: false,
        UsesEmittedKernelMatcher: false);

    public bool TryMatchAsciiWellFormed(ReadOnlySpan<byte> input, out Utf8ValueMatch match)
    {
        if (_canUseDirectPrefixUntilByteMatch &&
            Utf8AsciiPrefixTokenExecutor.TryMatchAnchoredPrefixUntilByte(input, _prefixUntilByteLiteral, _prefixUntilByteTerminator, out var docLineLength))
        {
            match = new Utf8ValueMatch(true, true, 0, docLineLength, 0, docLineLength);
            return true;
        }

        match = Utf8ValueMatch.NoMatch;
        return false;
    }

    public bool ExecuteMatchWithoutValidation(ReadOnlySpan<byte> input, Utf8ExecutionDeadline budget, out Utf8ValueMatch match)
    {
        if (_canUseDirectPrefixUntilByteMatch &&
            Utf8InputAnalyzer.IsAscii(input))
        {
            match = Utf8AsciiPrefixTokenExecutor.TryMatchAnchoredPrefixUntilByte(input, _prefixUntilByteLiteral, _prefixUntilByteTerminator, out var docLineLength)
                ? new Utf8ValueMatch(true, true, 0, docLineLength, 0, docLineLength)
                : Utf8ValueMatch.NoMatch;
            return true;
        }

        match = Utf8ValueMatch.NoMatch;
        return false;
    }

    public override bool IsMatch(ReadOnlySpan<byte> input, Utf8ValidationResult validation, Utf8ExecutionDeadline budget)
    {
        return Utf8ByteSafeLinearExecutor.IsMatch(input, _regexPlan, _verifierRuntime.StructuralVerifierRuntime, budget);
    }

    public override int Count(ReadOnlySpan<byte> input, Utf8ValidationResult validation, Utf8ExecutionDeadline budget)
    {
        Utf8SearchDiagnosticsSession.Current?.MarkExecutionRoute(Utf8ExecutionRoute.FallbackByteSafeLinear);
        return Utf8ByteSafeLinearExecutor.Count(input, _regexPlan, _verifierRuntime.StructuralVerifierRuntime, budget);
    }

    public override Utf8ValueMatch Match(ReadOnlySpan<byte> input, Utf8ValidationResult validation, Utf8ExecutionDeadline budget)
    {
        if (_canUseDirectPrefixUntilByteMatch && validation.IsAscii &&
            Utf8AsciiPrefixTokenExecutor.TryMatchAnchoredPrefixUntilByte(input, _prefixUntilByteLiteral, _prefixUntilByteTerminator, out var docLineLength))
        {
            return new Utf8ValueMatch(true, true, 0, docLineLength, 0, docLineLength);
        }

        var index = Utf8ByteSafeLinearExecutor.FindNext(input, _regexPlan, _verifierRuntime.StructuralVerifierRuntime, 0, budget, out var matchedLength);
        return index < 0
            ? Utf8ValueMatch.NoMatch
            : new Utf8ValueMatch(true, true, index, matchedLength, index, matchedLength);
    }

}

internal sealed class Utf8SearchGuidedFallbackCompiledEngineRuntime : Utf8CompiledEngineRuntime
{
    private readonly Utf8PreparedRegex _regexPlan;
    private readonly Utf8VerifierRuntime _verifierRuntime;
    private readonly Utf8CompiledExecutionBackend _backend;
    private readonly Utf8StructuralSearchPlan[] _candidatePlans;
    private readonly Utf8EmittedSearchGuidedFallback? _emittedBackend;

    public Utf8SearchGuidedFallbackCompiledEngineRuntime(Utf8CompiledEngine compiledEngine, Utf8NonLiteralCompiledEngineRuntime inner)
    {
        _regexPlan = inner.PreparedRegex;
        _verifierRuntime = inner.VerifierRuntime;
        _backend = compiledEngine.Backend;
        _candidatePlans = _regexPlan.SearchPlan.FallbackSearch.CandidatePlans ?? [];
        _emittedBackend = _backend == Utf8CompiledExecutionBackend.EmittedInstruction &&
                          Utf8EmittedSearchGuidedFallback.TryCreate(_regexPlan, _verifierRuntime, out var emitted)
            ? emitted
            : null;
    }

    public override bool IsMatch(ReadOnlySpan<byte> input, Utf8ValidationResult validation, Utf8ExecutionDeadline budget)
    {
        if (Utf8SearchGuidedFallbackCompiledPolicy.ShouldBypassIsMatch(_regexPlan))
        {
            Utf8SearchDiagnosticsSession.Current?.MarkExecutionRoute(Utf8ExecutionRoute.FallbackDirectRegex);
            return _verifierRuntime.FallbackCandidateVerifier.FallbackRegex.IsMatch(Encoding.UTF8.GetString(input));
        }

        Utf8SearchDiagnosticsSession.Current?.MarkExecutionRoute(Utf8ExecutionRoute.FallbackSearchGuided);
        if (Utf8SearchGuidedFallbackCompiledPolicy.CanUseEmittedBackend(_emittedBackend, budget) &&
            _emittedBackend is { } emittedBackend)
        {
            return emittedBackend.IsMatch(input);
        }

        var probe = GetProbeValidation(input, validation);
        Utf8BoundaryMap? boundaryMap = null;
        string? decoded = null;
        return TryFindNextVerifiedMatch(input, probe, 0, ref boundaryMap, ref decoded, out _, out _);
    }

    public override int Count(ReadOnlySpan<byte> input, Utf8ValidationResult validation, Utf8ExecutionDeadline budget)
    {
        Utf8SearchDiagnosticsSession.Current?.MarkExecutionRoute(Utf8ExecutionRoute.FallbackSearchGuided);
        if (Utf8SearchGuidedFallbackCompiledPolicy.CanUseEmittedBackend(_emittedBackend, budget) &&
            _emittedBackend is { } emittedBackend)
        {
            return emittedBackend.Count(input);
        }

        var probe = GetProbeValidation(input, validation);
        Utf8BoundaryMap? boundaryMap = null;
        string? decoded = null;
        var count = 0;
        var startIndex = 0;
        var verifierCount = 0;
        var verifierInvocations = 0;
        while ((uint)startIndex <= (uint)input.Length &&
            TryFindNextVerifiedMatch(
                input,
                probe,
                startIndex,
                ref boundaryMap,
                ref decoded,
                out var currentVerifierInvocations,
                out var verification))
        {
            Utf8SearchDiagnosticsSession.Current?.CountVerifierMatch();
            count++;
            verifierCount++;
            verifierInvocations += currentVerifierInvocations;
            if (Utf8SearchGuidedFallbackCompiledPolicy.ShouldDemoteToFallbackCount(verifierCount))
            {
                Utf8SearchDiagnosticsSession.Current?.MarkExecutionRoute(Utf8ExecutionRoute.FallbackSearchGuidedDemoted);
                return _verifierRuntime.FallbackCandidateVerifier.FallbackRegex.Count(Encoding.UTF8.GetString(input));
            }

            if (Utf8SearchGuidedFallbackCompiledPolicy.ShouldDemoteToFallbackCountByInvocations(verifierInvocations))
            {
                Utf8SearchDiagnosticsSession.Current?.MarkExecutionRoute(Utf8ExecutionRoute.FallbackSearchGuidedDemoted);
                return _verifierRuntime.FallbackCandidateVerifier.FallbackRegex.Count(Encoding.UTF8.GetString(input));
            }

            startIndex = verification.IndexInBytes + Math.Max(verification.LengthInBytes, 1);
        }

        return count;
    }

    public override Utf8ValueMatch Match(ReadOnlySpan<byte> input, Utf8ValidationResult validation, Utf8ExecutionDeadline budget)
    {
        var probe = GetProbeValidation(input, validation);
        Utf8BoundaryMap? boundaryMap = null;
        string? decoded = null;
        if (!TryFindNextVerifiedMatch(input, probe, 0, ref boundaryMap, ref decoded, out _, out var verification))
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

    private bool TryFindNextVerifiedMatch(
        ReadOnlySpan<byte> input,
        Utf8ValidationResult probe,
        int startIndex,
        ref Utf8BoundaryMap? boundaryMap,
        ref string? decoded,
        out int verifierInvocations,
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
            out verifierInvocations,
            out verification);
    }

}

internal sealed class Utf8CompiledFallbackCompiledEngineRuntime : Utf8CompiledEngineRuntime
{
    private readonly Utf8PreparedRegex _regexPlan;
    private readonly Utf8VerifierRuntime _verifierRuntime;
    private readonly Utf8StructuralSearchPlan[] _candidatePlans;
    private readonly Utf8ExecutionProgram _program;

    public Utf8CompiledFallbackCompiledEngineRuntime(Utf8NonLiteralCompiledEngineRuntime inner)
    {
        _regexPlan = inner.PreparedRegex;
        _verifierRuntime = inner.VerifierRuntime;
        _candidatePlans = _regexPlan.SearchPlan.FallbackSearch.CandidatePlans ?? [];
        _program = _regexPlan.ExecutionProgram
            ?? throw new InvalidOperationException("Compiled fallback requires an execution program.");
    }

    public override Utf8CompiledRuntimeCapabilities Capabilities => new(
        SupportsAsciiWellFormedOnlyMatch: false,
        SupportsWellFormedOnlyMatch: false,
        WellFormedOnlyMatchMissIsDefinitive: false,
        SupportsWellFormedOnlyCount: true,
        SupportsThrowIfInvalidOnlyCount: false,
        PreferValidateOnlyCount: false,
        SkipRequiredPrefilterForMatch: false,
        SkipRequiredPrefilterForCount: false,
        UsesEmittedAnchoredValidatorMatcher: false,
        UsesEmittedKernelMatcher: false);
    public override bool IsMatch(ReadOnlySpan<byte> input, Utf8ValidationResult validation, Utf8ExecutionDeadline budget)
    {
        Utf8SearchDiagnosticsSession.Current?.MarkExecutionRoute(Utf8ExecutionRoute.FallbackCompiled);
        Utf8BoundaryMap? boundaryMap = null;
        string? decoded = null;
        var probe = GetProbeValidation(input, validation);
        return TryFindNextMatch(input, probe, 0, ref boundaryMap, ref decoded, budget, out _);
    }

    public override int Count(ReadOnlySpan<byte> input, Utf8ValidationResult validation, Utf8ExecutionDeadline budget)
    {
        Utf8SearchDiagnosticsSession.Current?.MarkExecutionRoute(Utf8ExecutionRoute.FallbackCompiled);
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

    public override Utf8ValueMatch Match(ReadOnlySpan<byte> input, Utf8ValidationResult validation, Utf8ExecutionDeadline budget)
    {
        return Utf8CompiledFallbackMatchProjection.Match(input, _verifierRuntime.FallbackCandidateVerifier.FallbackRegex);
    }

    private bool TryFindNextMatch(
        ReadOnlySpan<byte> input,
        Utf8ValidationResult validation,
        int startIndex,
        ref Utf8BoundaryMap? boundaryMap,
        ref string? decoded,
        Utf8ExecutionDeadline budget,
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

internal sealed class Utf8FallbackRegexCompiledEngineRuntime : Utf8CompiledEngineRuntime,
    IUtf8AsciiWellFormedMatchRuntime,
    IUtf8WellFormedMatchRuntime,
    IUtf8UnvalidatedMatchRuntime
{
    private readonly Utf8PreparedRegex _regexPlan;
    private readonly Utf8VerifierRuntime _verifierRuntime;
    private readonly Utf8FallbackDirectFamilyPlan _directFamily;
    private readonly Utf8AsciiLiteralFinder _linePrefixFinder;
    private readonly Utf8EmittedTokenFamilyMatcher? _emittedTokenFamilyMatcher;
    private readonly PreparedAsciiDelimitedTokenSearch _delimitedTokenSearch;
    private readonly PreparedAsciiLiteralStructuredTokenSearch _literalStructuredTokenSearch;

    public Utf8FallbackRegexCompiledEngineRuntime(Utf8NonLiteralCompiledEngineRuntime inner)
    {
        _regexPlan = inner.PreparedRegex;
        _verifierRuntime = inner.VerifierRuntime;
        _directFamily = inner.PreparedRegex.FallbackDirectFamily;
        _linePrefixFinder = _directFamily.LiteralUtf8 is { Length: > 0 } literal
            ? new Utf8AsciiLiteralFinder(literal)
            : default;
        _emittedTokenFamilyMatcher = Utf8EmittedTokenFamilyMatcher.TryCreate(_directFamily, out var emittedTokenFamilyMatcher)
            ? emittedTokenFamilyMatcher
            : null;
        _delimitedTokenSearch = Utf8FallbackPreparedTokenSearchFactory.CreateDelimitedTokenSearch(_directFamily);
        _literalStructuredTokenSearch = Utf8FallbackPreparedTokenSearchFactory.CreateLiteralStructuredTokenSearch(_directFamily);
    }

    public override Utf8CompiledRuntimeCapabilities Capabilities
    {
        get
        {
            var supportsWellFormedMatch = Utf8FallbackDirectFamilyRuntimePolicy.SupportsWellFormedOnlyMatch(_directFamily);
            return new Utf8CompiledRuntimeCapabilities(
                SupportsAsciiWellFormedOnlyMatch: Utf8FallbackDirectFamilyRuntimePolicy.SupportsAsciiWellFormedOnlyMatch(_directFamily),
                SupportsWellFormedOnlyMatch: supportsWellFormedMatch,
                WellFormedOnlyMatchMissIsDefinitive: supportsWellFormedMatch,
                SupportsWellFormedOnlyCount: true,
                SupportsThrowIfInvalidOnlyCount: Utf8FallbackDirectFamilyRuntimePolicy.SupportsThrowIfInvalidOnlyCount(_directFamily),
                PreferValidateOnlyCount: _directFamily.Kind == Utf8FallbackDirectFamilyKind.AnchoredQuotedLineSegmentCount,
                SkipRequiredPrefilterForMatch: Utf8FallbackDirectFamilyRuntimePolicy.SkipRequiredPrefilterForMatch(_directFamily),
                SkipRequiredPrefilterForCount: Utf8FallbackDirectFamilyRuntimePolicy.SkipRequiredPrefilterForCount(_directFamily),
                UsesEmittedAnchoredValidatorMatcher: false,
                UsesEmittedKernelMatcher: false);
        }
    }

    public bool TryMatchAsciiWellFormed(ReadOnlySpan<byte> input, out Utf8ValueMatch match)
    {
        return Utf8FallbackDirectFamilyMatchRouter.TryMatchAsciiWellFormedOnly(
            input,
            _directFamily,
            _emittedTokenFamilyMatcher,
            _delimitedTokenSearch,
            _literalStructuredTokenSearch,
            out match);
    }

    public bool TryMatchWellFormed(ReadOnlySpan<byte> input, out Utf8ValueMatch match)
    {
        return Utf8FallbackDirectFamilyMatchRouter.TryMatchWellFormedOnly(
            input,
            _directFamily,
            _emittedTokenFamilyMatcher,
            _delimitedTokenSearch,
            _literalStructuredTokenSearch,
            out match);
    }

    public override bool IsMatch(ReadOnlySpan<byte> input, Utf8ValidationResult validation, Utf8ExecutionDeadline budget)
    {
        if (validation.IsAscii &&
            TryMatchDirectAsciiFamily(input, out _, out _))
        {
            return true;
        }

        return _verifierRuntime.FallbackCandidateVerifier.FallbackRegex.IsMatch(Encoding.UTF8.GetString(input));
    }

    public override int Count(ReadOnlySpan<byte> input, Utf8ValidationResult validation, Utf8ExecutionDeadline budget)
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

        Utf8SearchDiagnosticsSession.Current?.MarkExecutionRoute(Utf8ExecutionRoute.FallbackDirectRegex);
        return _verifierRuntime.FallbackCandidateVerifier.FallbackRegex.Count(Encoding.UTF8.GetString(input));
    }

    public override Utf8ValueMatch Match(ReadOnlySpan<byte> input, Utf8ValidationResult validation, Utf8ExecutionDeadline budget)
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

    public bool ExecuteMatchWithoutValidation(ReadOnlySpan<byte> input, Utf8ExecutionDeadline budget, out Utf8ValueMatch match)
    {
        return Utf8FallbackDirectFamilyRuntimePolicy.TryMatchWithoutValidation(
            input,
            _directFamily,
            _delimitedTokenSearch,
            _literalStructuredTokenSearch,
            out match);
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
