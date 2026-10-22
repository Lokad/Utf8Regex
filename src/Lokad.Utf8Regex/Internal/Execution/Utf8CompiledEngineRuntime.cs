using Lokad.Utf8Regex.Internal.Diagnostics;
using Lokad.Utf8Regex.Internal.Input;
using Lokad.Utf8Regex.Internal.Planning;
using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex.Internal.Execution;

internal readonly record struct Utf8CompiledRuntimeCapabilities(
    bool SupportsAsciiWellFormedOnlyMatch,
    bool SupportsWellFormedOnlyMatch,
    bool WellFormedOnlyMatchMissIsDefinitive,
    bool SupportsWellFormedOnlyCount,
    bool SupportsThrowIfInvalidOnlyCount,
    bool SkipRequiredPrefilterForMatch,
    bool SkipRequiredPrefilterForCount,
    bool UsesEmittedWholeMatcher,
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
    public bool SkipRequiredPrefilterForMatch => Capabilities.SkipRequiredPrefilterForMatch;
    public bool SkipRequiredPrefilterForCount => Capabilities.SkipRequiredPrefilterForCount;
    public bool UsesEmittedWholeMatcher => Capabilities.UsesEmittedWholeMatcher;
    public bool UsesEmittedKernelMatcher => Capabilities.UsesEmittedKernelMatcher;

    /// <summary>
    /// Returns an emitted kernel only when it implements complete global-match
    /// semantics for the supplied validated subject and deadline. Implementors
    /// return <see langword="null"/> whenever their kernel cannot preserve the
    /// validation, capture, or timeout contract for this invocation.
    /// </summary>
    internal virtual Utf8EmittedKernelMatcher? GetGlobalMatchKernel(
        Utf8ValidationResult validation,
        Utf8ExecutionDeadline budget) => null;

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
    /// Counts nonoverlapping matches, progressing through complete scalars.
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

    public static Utf8CompiledEngineRuntime Create(Utf8CompiledEngine compiledEngine, Utf8PreparedRegex regexPlan, Utf8VerifierRuntime verifierRuntime, RegexOptions options)
        => Utf8CompiledRuntimeFactory.Create(compiledEngine, regexPlan, verifierRuntime, options);

}

internal sealed class Utf8SimplePatternCompiledEngineRuntime : Utf8CompiledEngineRuntime,
    IUtf8WellFormedMatchRuntime,
    IUtf8UnvalidatedMatchRuntime
{
    private readonly Utf8PreparedRegex _regexPlan;
    private readonly Utf8StructuralLinearRuntime _linearRuntime;
    private readonly Utf8VerifierRuntime _verifierRuntime;
    private readonly bool _canUseDirectAnchoredValidatorMatch;
    private readonly bool _anchoredValidatorAllowsTrailingNewline;
    private readonly AsciiSimplePatternAnchoredBoundedDatePlan _anchoredBoundedDatePlan;
    private readonly AsciiSimplePatternRepeatedDigitGroupPlan _repeatedDigitGroupPlan;
    private readonly Utf8EmittedWholeMatcher? _emittedWholeMatcher;
    private readonly AsciiSimplePatternBoundedSuffixLiteralPlan _boundedSuffixLiteralPlan;
    private readonly AsciiSimplePatternSymmetricLiteralWindowPlan _symmetricLiteralWindowPlan;
    private readonly Utf8CompiledSymmetricLiteralWindowCounter? _compiledSymmetricLiteralWindowCounter;

    internal Utf8SimplePatternCompiledEngineRuntime(Utf8PreparedRegex plan, Utf8VerifierRuntime verifier, bool emit)
    {
        _regexPlan = plan;
        _linearRuntime = Utf8StructuralLinearRuntime.Create(_regexPlan);
        _verifierRuntime = verifier;
        var compiledPatternFamily = _regexPlan.SimplePatternPlan.CompiledPatternFamily;
        _canUseDirectAnchoredValidatorMatch =
            compiledPatternFamily.Kind == Utf8CompiledPatternFamilyKind.AnchoredValidator ||
            compiledPatternFamily.Kind == Utf8CompiledPatternFamilyKind.AnchoredBoundedDate;
        _anchoredValidatorAllowsTrailingNewline = _canUseDirectAnchoredValidatorMatch &&
            _regexPlan.SimplePatternPlan.AllowsTrailingNewlineBeforeEnd;
        _anchoredBoundedDatePlan = compiledPatternFamily.AnchoredBoundedDatePlan;
        _repeatedDigitGroupPlan = compiledPatternFamily.RepeatedDigitGroupPlan;
        _boundedSuffixLiteralPlan = compiledPatternFamily.BoundedSuffixLiteralPlan;
        _symmetricLiteralWindowPlan = compiledPatternFamily.SymmetricLiteralWindowPlan;
        _compiledSymmetricLiteralWindowCounter = emit && _symmetricLiteralWindowPlan.HasValue
            ? Utf8CompiledSymmetricLiteralWindowCounter.Create(_symmetricLiteralWindowPlan)
            : null;
        var anchoredValidatorPlan = compiledPatternFamily.AnchoredValidatorPlan;
        if (!emit)
        {
            _emittedWholeMatcher = null;
        }
        else if (_anchoredBoundedDatePlan.HasValue)
        {
            _emittedWholeMatcher = Utf8EmittedWholeMatcher.TryCreate(
                _anchoredBoundedDatePlan,
                _anchoredValidatorAllowsTrailingNewline,
                out var boundedDateMatcher)
                ? boundedDateMatcher
                : null;
        }
        else if (_regexPlan.SimplePatternPlan.AnchoredOptionalFieldPlan.HasValue)
        {
            _emittedWholeMatcher = Utf8EmittedWholeMatcher.TryCreate(
                _regexPlan.SimplePatternPlan.AnchoredOptionalFieldPlan,
                _anchoredValidatorAllowsTrailingNewline,
                out var optionalFieldMatcher)
                ? optionalFieldMatcher
                : null;
        }
        else if (_repeatedDigitGroupPlan.HasValue)
        {
            _emittedWholeMatcher = Utf8EmittedWholeMatcher.TryCreate(
                _repeatedDigitGroupPlan,
                out var repeatedDigitGroupMatcher)
                ? repeatedDigitGroupMatcher
                : null;
        }
        else
        {
            _emittedWholeMatcher =
                Utf8SimplePatternCompiledRuntimePolicy.ShouldUseEmittedAnchoredValidator(anchoredValidatorPlan) &&
                Utf8EmittedWholeMatcher.TryCreate(
                    anchoredValidatorPlan,
                    _anchoredValidatorAllowsTrailingNewline,
                    out var anchoredValidatorMatcher)
                    ? anchoredValidatorMatcher
                    : null;
        }
    }

    public override Utf8CompiledRuntimeCapabilities Capabilities => new(
        SupportsAsciiWellFormedOnlyMatch: _canUseDirectAnchoredValidatorMatch,
        SupportsWellFormedOnlyMatch: _canUseDirectAnchoredValidatorMatch,
        WellFormedOnlyMatchMissIsDefinitive: _canUseDirectAnchoredValidatorMatch,
        SupportsWellFormedOnlyCount: false,
        SupportsThrowIfInvalidOnlyCount: false,
        SkipRequiredPrefilterForMatch: _canUseDirectAnchoredValidatorMatch || _repeatedDigitGroupPlan.HasValue,
        SkipRequiredPrefilterForCount: false,
        UsesEmittedWholeMatcher: _emittedWholeMatcher is not null,
        UsesEmittedKernelMatcher: false);

    public bool TryMatchEmittedWhole(ReadOnlySpan<byte> input, out int matchedLength)
    {
        matchedLength = _emittedWholeMatcher?.MatchWhole(input) ?? -1;
        return matchedLength >= 0;
    }

    public bool TryMatchWellFormed(ReadOnlySpan<byte> input, out Utf8ValueMatch match)
    {
        if (_canUseDirectAnchoredValidatorMatch &&
            Utf8SimplePatternCompiledWholeMatcher.TryMatchAnchoredValidator(
                _regexPlan,
                _emittedWholeMatcher,
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
        // Whole-match fast paths are definitive only when a miss at offset 0 rules
        // out every substring match. Line-anchored (`(?m:^)`, `(?m:$)`) plans can
        // still match inside the input, so they decline to the validating scan paths.
        if ((_regexPlan.SimplePatternPlan.IsStartAnchored &&
            !_regexPlan.SimplePatternPlan.IsAbsoluteStartAnchored) ||
            (_regexPlan.SimplePatternPlan.IsEndAnchored &&
            !_regexPlan.SimplePatternPlan.IsAbsoluteEndAnchored))
        {
            match = Utf8ValueMatch.NoMatch;
            return false;
        }

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
        if (_regexPlan.ScalarProgram is { RightToLeft: true } ||
            !validation.IsAscii && input.IndexOfAnyInRange((byte)0x80, byte.MaxValue) >= 0)
            return Utf8PreparedScalarExecutor.IsMatch(_regexPlan, input, validation, budget);

        if (_regexPlan.StructuralLinearProgram.Kind == Utf8StructuralLinearProgramKind.AsciiFixedTokenPattern)
        {
            return _linearRuntime.IsMatch(input, validation, _verifierRuntime, budget);
        }

        if (_canUseDirectAnchoredValidatorMatch)
        {
            if (validation.IsAscii &&
                Utf8SimplePatternCompiledWholeMatcher.TryMatchAnchoredValidator(
                    _regexPlan,
                    _emittedWholeMatcher,
                    _anchoredValidatorAllowsTrailingNewline,
                    input,
                    out _))
            {
                return true;
            }

            if (Utf8SimplePatternCompiledRuntimePolicy.ShouldFallbackAfterAnchoredValidatorMiss(input, validation, _anchoredValidatorAllowsTrailingNewline))
            {
                return Utf8PreparedScalarExecutor.IsMatch(_regexPlan, input, validation, budget);
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
                _regexPlan.SearchPlan,
                _regexPlan.SimplePatternPlan,
                0,
                budget,
                out _) >= 0;
        }

        return Utf8PreparedScalarExecutor.IsMatch(_regexPlan, input, validation, budget);
    }

    public override int Count(ReadOnlySpan<byte> input, Utf8ValidationResult validation, Utf8ExecutionDeadline budget)
    {
        if (_regexPlan.ScalarProgram is { RightToLeft: true } ||
            !validation.IsAscii && input.IndexOfAnyInRange((byte)0x80, byte.MaxValue) >= 0)
            return Utf8PreparedScalarExecutor.Count(_regexPlan, input, validation, budget);

        if (_regexPlan.StructuralLinearProgram.Kind == Utf8StructuralLinearProgramKind.AsciiFixedTokenPattern)
        {
            return _linearRuntime.Count(input, validation, _verifierRuntime, budget);
        }

        if (_canUseDirectAnchoredValidatorMatch)
        {
            if (validation.IsAscii &&
                Utf8SimplePatternCompiledWholeMatcher.TryMatchAnchoredValidator(
                    _regexPlan,
                    _emittedWholeMatcher,
                    _anchoredValidatorAllowsTrailingNewline,
                    input,
                    out _))
            {
                Utf8SearchDiagnosticsSession.Current?.MarkExecutionRoute(Utf8ExecutionRoute.NativeAsciiAnchoredValidator);
                return 1;
            }

            if (Utf8SimplePatternCompiledRuntimePolicy.ShouldFallbackAfterAnchoredValidatorMiss(input, validation, _anchoredValidatorAllowsTrailingNewline))
            {
                return Utf8PreparedScalarExecutor.Count(_regexPlan, input, validation, budget);
            }
        }

        if (!validation.IsAscii && !_regexPlan.SimplePatternPlan.IsUtf8ByteSafe)
        {
            return Utf8PreparedScalarExecutor.Count(_regexPlan, input, validation, budget);
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

        return Utf8ExecutionInterpreter.CountSimplePattern(
            input,
            _regexPlan.SearchPlan,
            _regexPlan.SimplePatternPlan,
            budget);
    }

    public override Utf8ValueMatch Match(ReadOnlySpan<byte> input, Utf8ValidationResult validation, Utf8ExecutionDeadline budget)
    {
        if (_regexPlan.ScalarProgram is { RightToLeft: true } ||
            !validation.IsAscii && input.IndexOfAnyInRange((byte)0x80, byte.MaxValue) >= 0)
            return Utf8PreparedScalarExecutor.Match(_regexPlan, input, validation, budget);

        if (_regexPlan.StructuralLinearProgram.Kind == Utf8StructuralLinearProgramKind.AsciiFixedTokenPattern)
        {
            return _linearRuntime.Match(input, validation, _verifierRuntime, budget);
        }

        if (_canUseDirectAnchoredValidatorMatch)
        {
            if (validation.IsAscii &&
                Utf8SimplePatternCompiledWholeMatcher.TryMatchAnchoredValidator(
                    _regexPlan,
                    _emittedWholeMatcher,
                    _anchoredValidatorAllowsTrailingNewline,
                    input,
                    out var anchoredValidatorLength))
            {
                return new Utf8ValueMatch(true, true, 0, anchoredValidatorLength, 0, anchoredValidatorLength);
            }

            if (Utf8SimplePatternCompiledRuntimePolicy.ShouldFallbackAfterAnchoredValidatorMiss(input, validation, _anchoredValidatorAllowsTrailingNewline))
            {
                return Utf8PreparedScalarExecutor.Match(_regexPlan, input, validation, budget);
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
                _regexPlan.SearchPlan,
                _regexPlan.SimplePatternPlan,
                0,
                budget,
                out var matchLength);
            if (index < 0)
            {
                return Utf8ValueMatch.NoMatch;
            }

            return new Utf8ValueMatch(true, true, index, matchLength, index, matchLength);
        }

        return Utf8PreparedScalarExecutor.Match(_regexPlan, input, validation, budget);
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
    private readonly Utf8EmittedLiteralFamilyCounter? _emittedBoundaryLiteralFamily;
    private readonly PreparedAsciiFindPlan _orderedWindowTrailingFindPlan;

    internal Utf8StructuralLinearAutomatonCompiledEngineRuntime(Utf8PreparedRegex plan, Utf8VerifierRuntime verifier, bool emit)
    {
        _regexPlan = plan;
        _linearRuntime = Utf8StructuralLinearRuntime.Create(plan);
        _verifierRuntime = verifier;
        _emitEnabled = emit;
        _emittedDeterministicMatcher = emit && Utf8EmittedDeterministicMatcher.TryCreate(_regexPlan.StructuralLinearProgram, out var matcher)
            ? matcher
            : null;
        _emittedKernelMatcher = emit && Utf8EmittedKernelMatcher.TryCreate(_regexPlan, out var preparedKernelMatcher)
            ? preparedKernelMatcher
            : null;
        _emittedBoundaryLiteralFamily = emit &&
            (_regexPlan.SemanticRegex.Options & RegexOptions.RightToLeft) == 0 &&
            Utf8EmittedLiteralFamilyCounter.TryCreateBoundaryOnlyStructuralFamily(
                _regexPlan,
            out var preparedBoundaryLiteralFamily)
                ? preparedBoundaryLiteralFamily
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
        SkipRequiredPrefilterForMatch: false,
        SkipRequiredPrefilterForCount: false,
        UsesEmittedWholeMatcher: false,
        UsesEmittedKernelMatcher: _emittedKernelMatcher is not null);

    internal Utf8EmittedLiteralFamilyCounter? EmittedBoundaryLiteralFamily =>
        _emittedBoundaryLiteralFamily;

    internal Utf8EmittedKernelMatcher? PreparedKernelMatcher => _emittedKernelMatcher;

    internal override Utf8EmittedKernelMatcher? GetGlobalMatchKernel(
        Utf8ValidationResult validation,
        Utf8ExecutionDeadline budget)
    {
        return _regexPlan.ExecutionKind == NativeExecutionKind.AsciiStructuralIdentifierFamily &&
            _regexPlan.Features.CaptureCount <= 1 &&
            validation.IsAscii &&
            budget.IsInfinite
                ? _emittedKernelMatcher
                : null;
    }

    public override bool IsMatch(ReadOnlySpan<byte> input, Utf8ValidationResult validation, Utf8ExecutionDeadline budget)
    {
        if (_regexPlan.ScalarProgram is { RightToLeft: true } ||
            !validation.IsAscii && input.IndexOfAnyInRange((byte)0x80, byte.MaxValue) >= 0)
            return Utf8PreparedScalarExecutor.IsMatch(_regexPlan, input, validation, budget);

        if (_regexPlan.ExecutionKind == NativeExecutionKind.AsciiSimplePattern &&
            _regexPlan.SimplePatternPlan.RunPlan.HasValue)
        {
            return Utf8AsciiCharClassRunExecutor.IsMatch(input, _regexPlan.SimplePatternPlan.RunPlan, budget);
        }

        if (budget.IsInfinite &&
            _emittedBoundaryLiteralFamily is { } emittedBoundaryLiteralFamily)
        {
            return emittedBoundaryLiteralFamily.IsMatch(input);
        }

        if (Utf8StructuralLinearCompiledRouter.TryIsMatch(
                _regexPlan,
                _verifierRuntime,
                _emitEnabled,
                _emittedDeterministicMatcher,
                _emittedKernelMatcher,
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
        if (_regexPlan.ScalarProgram is { RightToLeft: true } ||
            !validation.IsAscii && input.IndexOfAnyInRange((byte)0x80, byte.MaxValue) >= 0)
            return Utf8PreparedScalarExecutor.Count(_regexPlan, input, validation, budget);

        if (_regexPlan.ExecutionKind == NativeExecutionKind.AsciiSimplePattern &&
            _regexPlan.SimplePatternPlan.RunPlan.HasValue)
        {
            return Utf8AsciiCharClassRunExecutor.Count(input, _regexPlan.SimplePatternPlan.RunPlan, budget);
        }

        if (budget.IsInfinite &&
            _emittedBoundaryLiteralFamily is { } emittedBoundaryLiteralFamily)
        {
            Utf8SearchDiagnosticsSession.Current?.MarkExecutionRoute(Utf8ExecutionRoute.NativeStructuralFamilyEmit);
            return emittedBoundaryLiteralFamily.Count(input);
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
        if (_regexPlan.ScalarProgram is { RightToLeft: true } ||
            !validation.IsAscii && input.IndexOfAnyInRange((byte)0x80, byte.MaxValue) >= 0)
            return Utf8PreparedScalarExecutor.Match(_regexPlan, input, validation, budget);

        if (_regexPlan.ExecutionKind == NativeExecutionKind.AsciiSimplePattern &&
            _regexPlan.SimplePatternPlan.RunPlan.HasValue)
        {
            var index = Utf8AsciiCharClassRunExecutor.FindNext(input, _regexPlan.SimplePatternPlan.RunPlan, 0, out var matchedLength, budget);
            if (index < 0) return Utf8ValueMatch.NoMatch;
            if (validation.IsAscii)
                return new Utf8ValueMatch(true, true, index, matchedLength, index, matchedLength);
            // A class-run search returns bytes even when every matched byte is
            // ASCII. Preceding Unicode scalars still change the UTF-16 index.
            if (Utf8ScalarSimplePatternVerifier.TryProjectMatch(input, index, matchedLength, out var projectedRun))
                return projectedRun;
        }

        if (validation.IsAscii &&
            budget.IsInfinite &&
            _emittedBoundaryLiteralFamily is { } emittedBoundaryLiteralFamily)
        {
            return emittedBoundaryLiteralFamily.TryMatch(input, out var index, out var matchedLength)
                ? new Utf8ValueMatch(true, true, index, matchedLength, index, matchedLength)
                : Utf8ValueMatch.NoMatch;
        }

        if (Utf8StructuralLinearCompiledRouter.TryMatch(
                _regexPlan,
                _verifierRuntime,
                _emitEnabled,
                _emittedDeterministicMatcher,
                _emittedKernelMatcher,
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

internal sealed class Utf8FallbackRegexCompiledEngineRuntime : Utf8CompiledEngineRuntime,
    IUtf8AsciiWellFormedMatchRuntime,
    IUtf8WellFormedMatchRuntime,
    IUtf8UnvalidatedMatchRuntime
{
    private readonly Utf8PreparedRegex _regexPlan;
    private readonly Utf8FallbackDirectFamilyPlan _directFamily;
    private readonly Utf8EmittedTokenFamilyMatcher? _emittedTokenFamilyMatcher;
    private readonly PreparedAsciiDelimitedTokenSearch _delimitedTokenSearch;
    private readonly PreparedAsciiLiteralStructuredTokenSearch _literalStructuredTokenSearch;

    internal Utf8FallbackRegexCompiledEngineRuntime(Utf8PreparedRegex plan)
    {
        _regexPlan = plan;
        _directFamily = plan.FallbackDirectFamily;
        _emittedTokenFamilyMatcher = Utf8EmittedTokenFamilyMatcher.TryCreate(_directFamily, out var preparedTokenFamilyMatcher)
            ? preparedTokenFamilyMatcher
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
                SkipRequiredPrefilterForMatch: Utf8FallbackDirectFamilyRuntimePolicy.SkipRequiredPrefilterForMatch(_directFamily),
                SkipRequiredPrefilterForCount: Utf8FallbackDirectFamilyRuntimePolicy.SkipRequiredPrefilterForCount(_directFamily),
                UsesEmittedWholeMatcher: false,
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

        return Utf8PreparedScalarExecutor.IsMatch(_regexPlan, input, validation, budget);
    }

    public override int Count(ReadOnlySpan<byte> input, Utf8ValidationResult validation, Utf8ExecutionDeadline budget)
    {
        if (Utf8FallbackDirectFamilyCountRouter.TryCount(
            input,
            validation,
            _directFamily,
            _emittedTokenFamilyMatcher,
            _delimitedTokenSearch,
            _literalStructuredTokenSearch,
            out var directCount))
        {
            return directCount;
        }

        Utf8SearchDiagnosticsSession.Current?.MarkExecutionRoute(Utf8ExecutionRoute.FallbackDirectRegex);
        return Utf8PreparedScalarExecutor.Count(_regexPlan, input, validation, budget);
    }

    public override Utf8ValueMatch Match(ReadOnlySpan<byte> input, Utf8ValidationResult validation, Utf8ExecutionDeadline budget)
    {
        if (validation.IsAscii &&
            TryMatchDirectAsciiFamily(input, out var matchIndex, out var matchedLength))
        {
            return new Utf8ValueMatch(true, true, matchIndex, matchedLength, matchIndex, matchedLength);
        }

        return Utf8PreparedScalarExecutor.Match(_regexPlan, input, validation, budget);
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
