using Lokad.Utf8Regex.Internal.Caching;
using Lokad.Utf8Regex.Internal.Diagnostics;
using Lokad.Utf8Regex.Internal.Search;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.FrontEnd;
using Lokad.Utf8Regex.Internal.Input;
using Lokad.Utf8Regex.Internal.Planning;
using Lokad.Utf8Regex.Internal.Replacement;
using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.Intrinsics.X86;
using System.Text.Unicode;

namespace Lokad.Utf8Regex;

// PLAN.md D09: compiled execution engines, split from the matching facade along the execution-engine ownership boundary.
/// <summary>Compiled execution engines for <see cref="Utf8Regex"/>.</summary>
public sealed partial class Utf8Regex
{
    private bool IsMatchViaCompiledExactLiteralEngine(ReadOnlySpan<byte> input, Utf8ExecutionDeadline budget)
    {
        return _compiledEngineRuntime.IsMatch(input, default, budget);
    }

    private bool IsMatchViaCompiledLiteralFamilyEngine(ReadOnlySpan<byte> input, Utf8ExecutionDeadline budget)
    {
        return _compiledEngineRuntime.IsMatch(input, default, budget);
    }

    private bool CanUseFusedCompiledUtf8LiteralCount()
    {
        if (_compiledEngine.Kind != Utf8CompiledEngineKind.ExactLiteral ||
            _preparedRegex.ExecutionKind != NativeExecutionKind.ExactUtf8Literal ||
            UsesRightToLeft() ||
            _preparedRegex.SearchPlan.HasBoundaryRequirements ||
            _preparedRegex.SearchPlan.HasTrailingLiteralRequirement ||
            _preparedRegex.LiteralUtf8 is not { Length: > 0 } literal)
        {
            return false;
        }

        if (literal.Length % 3 != 0)
        {
            return false;
        }

        for (var i = 0; i < literal.Length; i += 3)
        {
            var b0 = literal[i];
            if (b0 < 0xE0 || b0 >= 0xF0)
            {
                return false;
            }
        }

        return true;
    }

    private bool CanUseFusedCompiledUtf8LiteralFamilyCount()
    {
        // The fused family kernels are only evidenced with vector support: without SSSE3
        // the route miscounts ordered alternations (observed 2 vs oracle 1 on a CJK
        // alternation), so decline and let a verified fallback count instead.
        if (!Sse2.IsSupported || !Ssse3.IsSupported)
        {
            return false;
        }

        if (_compiledEngine.Kind != Utf8CompiledEngineKind.LiteralFamily ||
            _preparedRegex.ExecutionKind != NativeExecutionKind.ExactUtf8Literals ||
            UsesRightToLeft() ||
            _preparedRegex.SearchPlan.HasBoundaryRequirements ||
            _preparedRegex.SearchPlan.HasTrailingLiteralRequirement ||
            _preparedRegex.SearchPlan.AlternateLiteralsUtf8 is not { Length: > 0 } literals)
        {
            return false;
        }

        for (var i = 0; i < literals.Length; i++)
        {
            var literal = literals[i];
            if (literal.Length == 0 || literal.Length % 3 != 0)
            {
                return false;
            }

            for (var j = 0; j < literal.Length; j += 3)
            {
                var b0 = literal[j];
                if (b0 < 0xE0 || b0 >= 0xF0)
                {
                    return false;
                }
            }
        }

        return true;
    }

    private bool CanUseFusedCompiledAsciiLiteralFamilyCount()
    {
        if (_compiledEngine.Kind != Utf8CompiledEngineKind.LiteralFamily ||
            _preparedRegex.ExecutionKind != NativeExecutionKind.ExactUtf8Literals ||
            UsesRightToLeft() ||
            _preparedRegex.SearchPlan.HasBoundaryRequirements ||
            _preparedRegex.SearchPlan.HasTrailingLiteralRequirement ||
            _preparedRegex.SearchPlan.AlternateLiteralsUtf8 is not { Length: > 0 } literals)
        {
            return false;
        }

        for (var i = 0; i < literals.Length; i++)
        {
            if (!Utf8InputAnalyzer.IsAscii(literals[i]))
            {
                return false;
            }
        }

        return true;
    }

    private bool IsMatchViaCompiledEngine(ReadOnlySpan<byte> input, Utf8ValidationResult validation, Utf8ExecutionDeadline budget)
    {
        if (ShouldUseFallbackForAnchoredSimplePattern() || ShouldUseFallbackForNonAsciiSimplePattern(validation, allowByteSafeStructuralLinear: true))
        {
            return _verifierRuntime.FallbackCandidateVerifier.FallbackRegex.IsMatch(Encoding.UTF8.GetString(input));
        }

        if (ShouldUseInterpreterForTimeoutSensitiveSimplePattern())
        {
            return Utf8ExecutionInterpreter.FindNextSimplePattern(
                input,
                _preparedRegex.ExecutionProgram,
                _preparedRegex.SearchPlan,
                _preparedRegex.SimplePatternPlan,
                0,
                captures: null,
                budget,
                out _) >= 0;
        }

        return _compiledEngine.Kind switch
        {
            Utf8CompiledEngineKind.ExactLiteral => IsMatchViaCompiledExactLiteralEngine(input, budget),
            Utf8CompiledEngineKind.LiteralFamily => IsMatchViaCompiledLiteralFamilyEngine(input, budget),
            _ => _compiledEngineRuntime.IsMatch(input, validation, budget),
        };
    }

    private int CountViaCompiledExactLiteralEngine(ReadOnlySpan<byte> input, Utf8ExecutionDeadline budget)
    {
        return _compiledEngineRuntime.Count(input, default, budget);
    }

    private int CountViaCompiledLiteralFamilyEngine(ReadOnlySpan<byte> input, Utf8ExecutionDeadline budget)
    {
        return _compiledEngineRuntime.Count(input, default, budget);
    }

    private int CountViaCompiledEngine(ReadOnlySpan<byte> input, Utf8ValidationResult validation, Utf8ExecutionDeadline budget)
    {
        // Anchored literal prefixes count directly through the router below: at most one
        // anchored match, byte-exact on every input, so neither BCL fallback applies.
        if (_fallbackDirectFamily.Kind == Utf8FallbackDirectFamilyKind.AnchoredAsciiLiteralPrefix)
        {
            return _compiledEngineRuntime.Count(input, validation, budget);
        }

        var boundedSuffixLiteralPlan = _preparedRegex.SimplePatternPlan.BoundedSuffixLiteralPlan;
        if (!validation.IsAscii &&
            validation.ByteLength == input.Length &&
            boundedSuffixLiteralPlan.SupportsValidatedUtf8Input)
        {
            return Utf8AsciiBoundedSuffixLiteralExecutor.Count(input, boundedSuffixLiteralPlan, budget);
        }

        if (ShouldUseFallbackForAnchoredSimplePattern() ||
            ShouldUseFallbackForNonAsciiSimplePattern(validation, allowByteSafeStructuralLinear: true))
        {
            return _verifierRuntime.FallbackCandidateVerifier.FallbackRegex.Count(Encoding.UTF8.GetString(input));
        }

        return _compiledEngine.Kind switch
        {
            Utf8CompiledEngineKind.ExactLiteral => CountViaCompiledExactLiteralEngine(input, budget),
            Utf8CompiledEngineKind.LiteralFamily => CountViaCompiledLiteralFamilyEngine(input, budget),
            _ => _compiledEngineRuntime.Count(input, validation, budget),
        };
    }

    private Utf8ValueMatch MatchViaCompiledExactLiteralEngine(ReadOnlySpan<byte> input, Utf8ExecutionDeadline budget)
    {
        return _compiledEngineRuntime.Match(input, default, budget);
    }

    private Utf8ValueMatch MatchViaCompiledLiteralFamilyEngine(ReadOnlySpan<byte> input, Utf8ExecutionDeadline budget)
    {
        return _compiledEngineRuntime.Match(input, default, budget);
    }

    private Utf8ValueMatch MatchViaCompiledEngine(ReadOnlySpan<byte> input, Utf8ValidationResult validation, Utf8ExecutionDeadline budget)
    {
        if (ShouldUseFallbackForAnchoredSimplePattern() || ShouldUseFallbackForNonAsciiSimplePattern(validation))
        {
            return MatchFallback(input);
        }

        return _compiledEngine.Kind switch
        {
            Utf8CompiledEngineKind.ExactLiteral => MatchViaCompiledExactLiteralEngine(input, budget),
            Utf8CompiledEngineKind.LiteralFamily => MatchViaCompiledLiteralFamilyEngine(input, budget),
            _ => _compiledEngineRuntime.Match(input, validation, budget),
        };
    }

    private Utf8ValueMatchEnumerator CreateMatchEnumeratorViaCompiledEngine(ReadOnlySpan<byte> input, Utf8ValidationResult validation, byte[]? literal, Utf8ExecutionDeadline budget)
    {
        // The owned cursors do not enforce start anchors, so anchored shapes keep the BCL
        // enumerator until cursor anchor handling lands. The anchored literal-prefix family
        // bypasses this path through its anchor-enforcing owned cursor in EnumerateMatches.
        if (ShouldUseFallbackForAnchoredSimplePattern() || ShouldUseFallbackForNonAsciiSimplePattern(validation))
        {
            var analysis = Utf8InputAnalyzer.Analyze(input);
            return new Utf8ValueMatchEnumerator(input, Encoding.UTF8.GetString(input), _verifierRuntime.FallbackCandidateVerifier.FallbackRegex, analysis.Utf16ProjectionMap);
        }

        return Utf8CompiledOperationCursorFactory.CreateMatchEnumerator(
            _compiledEngineRuntime,
            _preparedRegex,
            _verifierRuntime,
            input,
            validation,
            budget);
    }

    private Utf8ValueSplitEnumerator CreateSplitEnumeratorViaCompiledEngine(
        ReadOnlySpan<byte> input,
        Utf8ValidationResult validation,
        int totalUtf16Length,
        int count,
        Utf8ExecutionDeadline budget)
    {
        if (ShouldUseFallbackForAnchoredSimplePattern() || ShouldUseFallbackForNonAsciiSimplePattern(validation))
        {
            var analysis = Utf8InputAnalyzer.Analyze(input);
            return new Utf8ValueSplitEnumerator(input, Encoding.UTF8.GetString(input), _verifierRuntime.FallbackCandidateVerifier.FallbackRegex, count, analysis.BoundaryMap);
        }

        return Utf8CompiledOperationCursorFactory.CreateSplitEnumerator(
            _preparedRegex,
            _verifierRuntime,
            input,
            validation,
            totalUtf16Length,
            count,
            budget);
    }
}
