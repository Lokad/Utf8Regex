using Lokad.Utf8Regex.Internal.Diagnostics;
using Lokad.Utf8Regex.Internal.Input;
using Lokad.Utf8Regex.Internal.Planning;
using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex.Internal.Execution;

internal readonly record struct Utf8CompiledRuntimeCapabilities(
    bool SupportsAsciiWellFormedOnlyMatch, bool SupportsWellFormedOnlyMatch, bool WellFormedOnlyMatchMissIsDefinitive, bool SupportsWellFormedOnlyCount, bool SkipRequiredPrefilterForMatch, bool SkipRequiredPrefilterForCount);

internal interface IUtf8AsciiWellFormedMatchRuntime
{
    /// <summary>
    /// Attempts a match after the caller has proved that the subject is ASCII
    /// and well formed. A false result is definitive only when the runtime's
    /// capability record says so.
    /// </summary>
    bool TryMatchAsciiWellFormed(ReadOnlySpan<byte> input, out Utf8CaptureRange match);
}

internal interface IUtf8WellFormedMatchRuntime
{
    /// <summary>
    /// Attempts a match over caller-valid UTF-8 text.
    /// A false result is definitive only when the capability record says so.
    /// </summary>
    bool TryMatchWellFormed(ReadOnlySpan<byte> input, out Utf8CaptureRange match);
}

internal interface IUtf8UnvalidatedMatchRuntime
{
    /// <summary>
    /// Attempts a byte-safe match without validation. The implementation must
    /// charge the supplied deadline and return false when it cannot safely
    /// decide the operation over caller-valid UTF-8.
    /// </summary>
    bool ExecuteMatchWithoutValidation(
        ReadOnlySpan<byte> input, Utf8ExecutionDeadline budget, out Utf8CaptureRange match);
}

internal abstract class Utf8CompiledEngineRuntime
{
    /// <summary>
    /// Describes optional whole-operation shortcuts implemented by this
    /// runtime. A false member means that the canonical matching operation
    /// path must be used; all paths require caller-valid text.
    /// </summary>
    public virtual Utf8CompiledRuntimeCapabilities Capabilities => default;

    public bool SupportsAsciiWellFormedOnlyMatch => Capabilities.SupportsAsciiWellFormedOnlyMatch;
    public bool SupportsWellFormedOnlyMatch => Capabilities.SupportsWellFormedOnlyMatch;
    public bool WellFormedOnlyMatchMissIsDefinitive => Capabilities.WellFormedOnlyMatchMissIsDefinitive;
    public bool SupportsWellFormedOnlyCount => Capabilities.SupportsWellFormedOnlyCount;
    public bool SkipRequiredPrefilterForMatch => Capabilities.SkipRequiredPrefilterForMatch;
    public bool SkipRequiredPrefilterForCount => Capabilities.SkipRequiredPrefilterForCount;

    public bool TryMatchAsciiWellFormedOnly(ReadOnlySpan<byte> input, out Utf8CaptureRange match)
    {
        if (this is IUtf8AsciiWellFormedMatchRuntime runtime)
        {
            return runtime.TryMatchAsciiWellFormed(input, out match);
        }

        match = default(Utf8CaptureRange);
        return false;
    }

    public bool TryMatchWellFormedOnly(ReadOnlySpan<byte> input, out Utf8CaptureRange match)
    {
        if (this is IUtf8WellFormedMatchRuntime runtime)
        {
            return runtime.TryMatchWellFormed(input, out match);
        }

        match = default(Utf8CaptureRange);
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
    /// or a default range. Endpoints refer to the original bytes and all work
    /// charges the deadline.
    /// </summary>
    public abstract Utf8CaptureRange Match(ReadOnlySpan<byte> input, Utf8ValidationResult validation, Utf8ExecutionDeadline budget);

    public bool TryMatchWithoutValidation(ReadOnlySpan<byte> input, Utf8ExecutionDeadline budget, out Utf8CaptureRange match)
    {
        if (this is IUtf8UnvalidatedMatchRuntime runtime)
        {
            return runtime.ExecuteMatchWithoutValidation(input, budget, out match);
        }

        match = default(Utf8CaptureRange);
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
    }

    public override Utf8CompiledRuntimeCapabilities Capabilities => new(
        SupportsAsciiWellFormedOnlyMatch: _canUseDirectAnchoredValidatorMatch,
        SupportsWellFormedOnlyMatch: _canUseDirectAnchoredValidatorMatch,
        WellFormedOnlyMatchMissIsDefinitive: _canUseDirectAnchoredValidatorMatch,
        SupportsWellFormedOnlyCount: false,
        SkipRequiredPrefilterForMatch: _canUseDirectAnchoredValidatorMatch || _repeatedDigitGroupPlan.HasValue,
        SkipRequiredPrefilterForCount: false);

    public bool TryMatchWellFormed(ReadOnlySpan<byte> input, out Utf8CaptureRange match)
    {
        if (_canUseDirectAnchoredValidatorMatch &&
            Utf8SimplePatternCompiledWholeMatcher.TryMatchAnchoredValidator(
                _regexPlan,
                _anchoredValidatorAllowsTrailingNewline,
                input,
                out var anchoredValidatorLength))
        {
            match = new Utf8CaptureRange(0, 0 + (anchoredValidatorLength));
            return true;
        }

        match = default(Utf8CaptureRange);
        return false;
    }

    public bool ExecuteMatchWithoutValidation(ReadOnlySpan<byte> input, Utf8ExecutionDeadline budget, out Utf8CaptureRange match)
    {
        // Whole-match fast paths are definitive only when a miss at offset 0 rules
        // out every substring match. Line-anchored (`(?m:^)`, `(?m:$)`) plans can
        // still match inside the input, so they decline to the validating scan paths.
        if ((_regexPlan.SimplePatternPlan.IsStartAnchored &&
            !_regexPlan.SimplePatternPlan.IsAbsoluteStartAnchored) ||
            (_regexPlan.SimplePatternPlan.IsEndAnchored &&
            !_regexPlan.SimplePatternPlan.IsAbsoluteEndAnchored))
        {
            match = default(Utf8CaptureRange);
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
                match = new Utf8CaptureRange(0, 0 + (anchoredValidatorLength));
                return true;
            }

            if (direct == Utf8AsciiAnchoredValidatorExecutor.DirectMatchResult.NeedsValidation)
            {
                match = default(Utf8CaptureRange);
                return false;
            }
            match = default(Utf8CaptureRange);
            return true;
        }

        match = default(Utf8CaptureRange);
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
                    _repeatedDigitGroupPlan, input, validation, out var guidedIsMatch))
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
                _repeatedDigitGroupPlan, input, validation, out var guidedCount))
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

    public override Utf8CaptureRange Match(ReadOnlySpan<byte> input, Utf8ValidationResult validation, Utf8ExecutionDeadline budget)
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
                    _anchoredValidatorAllowsTrailingNewline,
                    input,
                    out var anchoredValidatorLength))
            {
                return new Utf8CaptureRange(0, 0 + (anchoredValidatorLength));
            }

            if (Utf8SimplePatternCompiledRuntimePolicy.ShouldFallbackAfterAnchoredValidatorMiss(input, validation, _anchoredValidatorAllowsTrailingNewline))
            {
                return Utf8PreparedScalarExecutor.Match(_regexPlan, input, validation, budget);
            }
        }

        if (validation.IsAscii || _regexPlan.SimplePatternPlan.IsUtf8ByteSafe)
        {
            if (Utf8SimplePatternCompiledSearchGuidedRouter.TryMatch(
                    _repeatedDigitGroupPlan, input, validation, out var guidedMatch))
            {
                return guidedMatch;
            }

            if (Utf8SimplePatternCompiledWholeMatcher.TryMatchDirectAnchoredFixedAlternationSimplePattern(_regexPlan, input, out var alternationMatchedLength))
            {
                return new Utf8CaptureRange(0, 0 + (alternationMatchedLength));
            }

            if (Utf8SimplePatternCompiledWholeMatcher.TryMatchDirectAnchoredFixedLengthSimplePattern(_regexPlan, input, out var directMatchedLength))
            {
                return new Utf8CaptureRange(0, 0 + (directMatchedLength));
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
                return default(Utf8CaptureRange);
            }

            return new Utf8CaptureRange(index, (index) + (matchLength));
        }

        return Utf8PreparedScalarExecutor.Match(_regexPlan, input, validation, budget);
    }

}

internal sealed class Utf8StructuralLinearAutomatonCompiledEngineRuntime : Utf8CompiledEngineRuntime
{
    private readonly Utf8PreparedRegex _regexPlan;
    private readonly Utf8StructuralLinearRuntime _linearRuntime;
    private readonly Utf8VerifierRuntime _verifierRuntime;
    private readonly Utf8EmittedLiteralFamilyCounter? _emittedBoundaryLiteralFamily;
    private readonly PreparedAsciiFindPlan _orderedWindowTrailingFindPlan;

    internal Utf8StructuralLinearAutomatonCompiledEngineRuntime(Utf8PreparedRegex plan, Utf8VerifierRuntime verifier, bool emit)
    {
        _regexPlan = plan;
        _linearRuntime = Utf8StructuralLinearRuntime.Create(plan);
        _verifierRuntime = verifier;
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
        SkipRequiredPrefilterForMatch: false,
        SkipRequiredPrefilterForCount: false);

    internal Utf8EmittedLiteralFamilyCounter? EmittedBoundaryLiteralFamily =>
        _emittedBoundaryLiteralFamily;

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

    public override Utf8CaptureRange Match(ReadOnlySpan<byte> input, Utf8ValidationResult validation, Utf8ExecutionDeadline budget)
    {
        if (_regexPlan.ScalarProgram is { RightToLeft: true } ||
            !validation.IsAscii && input.IndexOfAnyInRange((byte)0x80, byte.MaxValue) >= 0)
            return Utf8PreparedScalarExecutor.Match(_regexPlan, input, validation, budget);

        if (_regexPlan.ExecutionKind == NativeExecutionKind.AsciiSimplePattern &&
            _regexPlan.SimplePatternPlan.RunPlan.HasValue)
        {
            var index = Utf8AsciiCharClassRunExecutor.FindNext(input, _regexPlan.SimplePatternPlan.RunPlan, 0, out var matchedLength, budget);
            if (index < 0) return default(Utf8CaptureRange);
            if (validation.IsAscii)
                return new Utf8CaptureRange(index, (index) + (matchedLength));
            // A class-run search returns bytes even when every matched byte is
            // ASCII. Preceding Unicode scalars still change the UTF-16 index.
            if (Utf8ScalarSimplePatternVerifier.TryCreateScalarMatchRange(input, index, matchedLength, out var projectedRun))
                return projectedRun;
        }

        if (validation.IsAscii &&
            budget.IsInfinite &&
            _emittedBoundaryLiteralFamily is { } emittedBoundaryLiteralFamily)
        {
            return emittedBoundaryLiteralFamily.TryMatch(input, out var index, out var matchedLength)
                ? new Utf8CaptureRange(index, (index) + (matchedLength))
                : default(Utf8CaptureRange);
        }

        if (Utf8StructuralLinearCompiledRouter.TryMatch(
                _regexPlan,
                _verifierRuntime,
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
