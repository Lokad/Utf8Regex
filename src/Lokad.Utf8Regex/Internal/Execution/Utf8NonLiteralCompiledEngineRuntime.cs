using System.Text;
using Lokad.Utf8Regex.Internal.Diagnostics;
using Lokad.Utf8Regex.Internal.Input;
using Lokad.Utf8Regex.Internal.Planning;

namespace Lokad.Utf8Regex.Internal.Execution;

internal sealed class Utf8NonLiteralCompiledEngineRuntime : Utf8CompiledEngineRuntime
{
    private readonly Utf8RegexPlan _regexPlan;
    private readonly Utf8VerifierRuntime _verifierRuntime;
    private readonly Utf8CompiledPatternFamilyPlan _compiledPatternFamily;

    public Utf8NonLiteralCompiledEngineRuntime(Utf8RegexPlan regexPlan, Utf8VerifierRuntime verifierRuntime)
    {
        _regexPlan = regexPlan;
        _verifierRuntime = verifierRuntime;
        _compiledPatternFamily = _regexPlan.SimplePatternPlan.CompiledPatternFamily;
    }

    internal Utf8RegexPlan RegexPlan => _regexPlan;

    internal Utf8VerifierRuntime VerifierRuntime => _verifierRuntime;

    public override bool IsMatch(ReadOnlySpan<byte> input, Utf8ValidationResult validation, Utf8ExecutionBudget? budget)
    {
        return _regexPlan.CompiledEngine.Kind switch
        {
            Utf8CompiledEngineKind.SimplePatternInterpreter when validation.IsAscii || _regexPlan.SimplePatternPlan.IsUtf8ByteSafe
                => IsMatchSimplePattern(input, budget),
            Utf8CompiledEngineKind.ByteSafeLinear
                => Utf8ByteSafeLinearExecutor.IsMatch(input, _regexPlan, _verifierRuntime.StructuralVerifierRuntime, budget),
            _ => throw UnexpectedEngineKind(),
        };
    }

    public override int Count(ReadOnlySpan<byte> input, Utf8ValidationResult validation, Utf8ExecutionBudget? budget)
    {
        return _regexPlan.CompiledEngine.Kind switch
        {
            Utf8CompiledEngineKind.SimplePatternInterpreter when validation.IsAscii || _regexPlan.SimplePatternPlan.IsUtf8ByteSafe
                => CountSimplePattern(input, budget),
            Utf8CompiledEngineKind.ByteSafeLinear
                => CountByteSafeFallback(input, budget),
            _ => throw UnexpectedEngineKind(),
        };
    }

    public override Utf8ValueMatch Match(ReadOnlySpan<byte> input, Utf8ValidationResult validation, Utf8ExecutionBudget? budget)
    {
        return _regexPlan.CompiledEngine.Kind switch
        {
            Utf8CompiledEngineKind.SimplePatternInterpreter
                => validation.IsAscii || _regexPlan.SimplePatternPlan.IsUtf8ByteSafe
                    ? MatchSimplePattern(input, budget)
                    : Utf8ValueMatch.NoMatch,
            Utf8CompiledEngineKind.ByteSafeLinear
                => Utf8ValueMatch.NoMatch,
            _ => throw UnexpectedEngineKind(),
        };
    }

    public override Utf8ValueMatchEnumerator CreateMatchEnumerator(ReadOnlySpan<byte> input, Utf8ValidationResult validation, Utf8ExecutionBudget? budget)
    {
        return _regexPlan.CompiledEngine.Kind switch
        {
            Utf8CompiledEngineKind.SimplePatternInterpreter when validation.IsAscii
                => new Utf8ValueMatchEnumerator(input, _regexPlan.ExecutionProgram, _regexPlan.SearchPlan, _regexPlan.SimplePatternPlan, budget),
            _ => throw UnexpectedEngineKind(),
        };
    }

    public override Utf8ValueSplitEnumerator CreateSplitEnumerator(ReadOnlySpan<byte> input, Utf8ValidationResult validation, int count, Utf8ExecutionBudget? budget)
    {
        return _regexPlan.CompiledEngine.Kind switch
        {
            Utf8CompiledEngineKind.SimplePatternInterpreter when validation.IsAscii
                => new Utf8ValueSplitEnumerator(input, _regexPlan.SearchPlan, _regexPlan.ExecutionProgram, _regexPlan.SimplePatternPlan, count, budget),
            _ => throw UnexpectedEngineKind(),
        };
    }

    private int CountSimplePattern(ReadOnlySpan<byte> input, Utf8ExecutionBudget? budget)
    {
        if (_compiledPatternFamily.Kind == Utf8CompiledPatternFamilyKind.BoundedSuffixLiteral)
        {
            return Utf8AsciiBoundedSuffixLiteralExecutor.Count(input, _compiledPatternFamily.BoundedSuffixLiteralPlan, budget);
        }

        if (_regexPlan.SimplePatternPlan.RunPlan.HasValue)
        {
            Utf8SearchDiagnosticsSession.Current?.MarkExecutionRoute("native_ascii_char_class_run");
            return Utf8AsciiCharClassRunExecutor.Count(input, _regexPlan.SimplePatternPlan.RunPlan, budget);
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

    private Utf8ValueMatch MatchSimplePattern(ReadOnlySpan<byte> input, Utf8ExecutionBudget? budget)
    {
        if (_compiledPatternFamily.Kind == Utf8CompiledPatternFamilyKind.BoundedSuffixLiteral)
        {
            return Utf8AsciiBoundedSuffixLiteralExecutor.Match(input, _compiledPatternFamily.BoundedSuffixLiteralPlan, budget);
        }

        if (_regexPlan.SimplePatternPlan.RunPlan.HasValue)
        {
            var runIndex = Utf8AsciiCharClassRunExecutor.FindNext(input, _regexPlan.SimplePatternPlan.RunPlan, 0, out var runMatchLength, budget);
            return runIndex < 0
                ? Utf8ValueMatch.NoMatch
                : new Utf8ValueMatch(true, true, runIndex, runMatchLength, runIndex, runMatchLength);
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

    private bool IsMatchSimplePattern(ReadOnlySpan<byte> input, Utf8ExecutionBudget? budget)
    {
        if (_compiledPatternFamily.Kind == Utf8CompiledPatternFamilyKind.BoundedSuffixLiteral)
        {
            return Utf8AsciiBoundedSuffixLiteralExecutor.IsMatch(input, _compiledPatternFamily.BoundedSuffixLiteralPlan, budget);
        }

        if (_regexPlan.SimplePatternPlan.RunPlan.HasValue)
        {
            return Utf8AsciiCharClassRunExecutor.IsMatch(input, _regexPlan.SimplePatternPlan.RunPlan, budget);
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

    private int CountByteSafeFallback(ReadOnlySpan<byte> input, Utf8ExecutionBudget? budget)
    {
        Utf8SearchDiagnosticsSession.Current?.MarkExecutionRoute("fallback_byte_safe_linear");
        return Utf8ByteSafeLinearExecutor.Count(input, _regexPlan, _verifierRuntime.StructuralVerifierRuntime, budget);
    }

    private InvalidOperationException UnexpectedEngineKind()
    {
        return new InvalidOperationException($"Unexpected compiled engine '{_regexPlan.CompiledEngine.Kind}' for non-literal runtime.");
    }
}
