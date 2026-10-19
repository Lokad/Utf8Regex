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
        if ((ShouldUseFallbackForAnchoredSimplePattern() && !CanServeLineAnchoredOwned(input)) || ShouldUseFallbackForNonAsciiSimplePattern(validation, allowByteSafeStructuralLinear: true))
        {
            // R05: scalar-verifiable simple patterns answer owned on astral-free
            // non-ASCII input; other admitted shapes use the cached scalar program.
            if (Utf8ScalarSimplePatternVerifier.TryIsMatch(input, _preparedRegex.ExecutionKind, _preparedRegex.SimplePatternPlan, budget, out var scalarIsMatch))
            {
                return scalarIsMatch;
            }
            var invocation = new Utf8SliceInvocation(Utf8ExecutionLimits.Unlimited, budget, CancellationToken.None);
            return OwnedSliceIsMatchFromByte(input, _ownedSliceProgram,
                _ownedSliceProgram.RightToLeft ? input.Length : 0, ref invocation);
        }

        if (ShouldUseInterpreterForTimeoutSensitiveSimplePattern())
        {
            return Utf8ExecutionInterpreter.FindNextSimplePattern(
                input,
                _preparedRegex.ExecutionProgram,
                _preparedRegex.SearchPlan,
                _preparedRegex.SimplePatternPlan,
                0,
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
        // Ordered-window kernels count byte gaps. Mixed subjects require the
        // cached scalar program before the internal ASCII router can run.
        if (_preparedRegex.ExecutionKind == NativeExecutionKind.AsciiOrderedLiteralWindow && !validation.IsAscii)
        {
            if (Utf8InputAnalyzer.IsAscii(input))
            {
                // A default validation result does not prove the subject is
                // mixed. Reuse this ASCII proof in the router below.
                validation = new Utf8ValidationResult(input.Length, input.Length, true, false);
            }
            else
            {
                var invocation = new Utf8SliceInvocation(Utf8ExecutionLimits.Unlimited, budget, CancellationToken.None);
                return OwnedSliceCountFromByte(input, _ownedSliceProgram, 0, ref invocation);
            }
        }
        // Anchored literal prefixes count directly through the router below: at most one
        // anchored match, byte-exact on every input, so neither BCL fallback applies.
        // Folded prefixes ride the same router arm, which switches on the kind.
        if (_fallbackDirectFamily.Kind is Utf8FallbackDirectFamilyKind.AnchoredAsciiLiteralPrefix or Utf8FallbackDirectFamilyKind.AnchoredFoldedAsciiLiteralPrefix)
        {
            return _compiledEngineRuntime.Count(input, validation, budget);
        }

        // Anchored literal suffixes count the same way: at most one match at the
        // input end, byte-exact on every input. Folded suffixes ride the same router
        // arm, which switches on the kind.
        if (Utf8FallbackDirectFamilyCategories.IsAnchoredSuffixMatchFamily(_fallbackDirectFamily.Kind) ||
            _fallbackDirectFamily.Kind is Utf8FallbackDirectFamilyKind.AnchoredFoldedAsciiLiteralSuffix or Utf8FallbackDirectFamilyKind.AnchoredFoldedAsciiLiteralSuffixStrictEnd)
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

        if ((ShouldUseFallbackForAnchoredSimplePattern() && !CanServeLineAnchoredOwned(input)) ||
            ShouldUseFallbackForNonAsciiSimplePattern(validation, allowByteSafeStructuralLinear: true))
        {
            // R05: same scalar-verified route as IsMatch above.
            if (Utf8ScalarSimplePatternVerifier.TryCount(input, _preparedRegex.ExecutionKind, _preparedRegex.SimplePatternPlan, budget, out var scalarCount))
            {
                return scalarCount;
            }
            var invocation = new Utf8SliceInvocation(Utf8ExecutionLimits.Unlimited, budget, CancellationToken.None);
            return OwnedSliceCountFromByte(input, _ownedSliceProgram,
                _ownedSliceProgram.RightToLeft ? input.Length : 0, ref invocation);
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
        if ((ShouldUseFallbackForAnchoredSimplePattern() && !CanServeLineAnchoredOwned(input)) || ShouldUseFallbackForNonAsciiSimplePattern(validation))
        {
            // A complete scalar scan makes misses definitive. Other admitted
            // shapes use the cached scalar program with the same deadline.
            if (Utf8ScalarSimplePatternVerifier.TryFindFirst(input, _preparedRegex.ExecutionKind, _preparedRegex.SimplePatternPlan, budget, out var scalarStart, out var scalarLength))
            {
                if (scalarStart >= 0 &&
                    Utf8ScalarSimplePatternVerifier.TryProjectMatch(input, scalarStart, scalarLength, out var scalarMatch))
                {
                    return scalarMatch;
                }

                if (scalarStart < 0)
                {
                    return Utf8ValueMatch.NoMatch;
                }
            }
            var invocation = new Utf8SliceInvocation(Utf8ExecutionLimits.Unlimited, budget, CancellationToken.None);
            return OwnedSliceMatchFromByte(input, _ownedSliceProgram,
                _ownedSliceProgram.RightToLeft ? input.Length : 0, validation, ref invocation);
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
        // Qualified simple kernels keep their native cursor. Remaining anchored
        // or mixed-subject shapes transfer to the cached scalar program.
        if ((ShouldUseFallbackForAnchoredSimplePattern() && !CanServeLineAnchoredOwned(input)) || ShouldUseFallbackForNonAsciiSimplePattern(validation))
        {
            if (Utf8ScalarSimplePatternVerifier.CanServeScalar(_preparedRegex.ExecutionKind, _preparedRegex.SimplePatternPlan, input))
            {
                var analysis = Utf8InputAnalyzer.Analyze(input);
                return new Utf8ValueMatchEnumerator(
                    new Utf8OperationMatchCursor(input, _preparedRegex, analysis.BoundaryMap, budget))
                    .WithTimeoutMapping(input, Pattern, MatchTimeout);
            }
            var invocation = new Utf8SliceInvocation(Utf8ExecutionLimits.Unlimited, budget, CancellationToken.None);
            if (validation.ByteLength != input.Length)
                validation = Utf8Validation.Validate(input, ref invocation);
            var cursor = Utf8OperationMatchCursor.CreateMapped(_ownedSliceProgram, input, validation, ref invocation, false, out _);
            return new Utf8ValueMatchEnumerator(cursor).WithTimeoutMapping(input, Pattern, MatchTimeout);
        }

        return Utf8CompiledOperationCursorFactory.CreateMatchEnumerator(
            _compiledEngineRuntime,
            _preparedRegex,
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
        // R05: anchored ASCII literal families split through the anchor-enforcing owned
        // cursors (at most one match at a fixed position), mirroring enumeration; the
        // split router admits these families past the simple-pattern gate below.
        if (_fallbackDirectFamily.Kind == Utf8FallbackDirectFamilyKind.AnchoredAsciiLiteralPrefix &&
            _fallbackDirectFamily.LiteralUtf8 is { Length: > 0 } anchoredSplitLiteral)
        {
            return new Utf8ValueSplitEnumerator(
                input,
                new Utf8OperationMatchCursor(input, anchoredSplitLiteral, budget),
                count,
                totalUtf16Length);
        }

        // Folded anchored prefixes split the same single anchored hit with per-byte
        // invariant folding; Kelvin and Turkic divergences stay fenced in the split
        // router upstream.
        if (_fallbackDirectFamily.Kind == Utf8FallbackDirectFamilyKind.AnchoredFoldedAsciiLiteralPrefix &&
            _fallbackDirectFamily.LiteralUtf8 is { Length: > 0 } foldedSplitLiteral)
        {
            return new Utf8ValueSplitEnumerator(
                input,
                new Utf8OperationMatchCursor(input, foldedSplitLiteral, budget, _fallbackDirectFamily.Kind),
                count,
                totalUtf16Length);
        }

        // Folded anchored suffixes split the same single tail hit with per-byte invariant
        // folding under the same router fence contract as prefixes.
        if ((Utf8FallbackDirectFamilyCategories.IsAnchoredSuffixMatchFamily(_fallbackDirectFamily.Kind) ||
            _fallbackDirectFamily.Kind is Utf8FallbackDirectFamilyKind.AnchoredFoldedAsciiLiteralSuffix or Utf8FallbackDirectFamilyKind.AnchoredFoldedAsciiLiteralSuffixStrictEnd) &&
            _fallbackDirectFamily.LiteralUtf8 is { Length: > 0 } anchoredSplitSuffix)
        {
            return new Utf8ValueSplitEnumerator(
                input,
                new Utf8OperationMatchCursor(input, anchoredSplitSuffix, budget, _fallbackDirectFamily.Kind),
                count,
                totalUtf16Length);
        }


        return Utf8CompiledOperationCursorFactory.CreateSplitEnumerator(
            _preparedRegex,
            input,
            validation,
            totalUtf16Length,
            count,
            budget);
    }
}
