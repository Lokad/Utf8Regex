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
using System.Text.Unicode;

namespace Lokad.Utf8Regex;

// compiled execution engines, split from the matching facade along the execution-engine ownership boundary.
/// <summary>Compiled execution engines for <see cref="Utf8Regex"/>.</summary>
public sealed partial class Utf8Regex
{
    private bool CanUseFusedCompiledAsciiLiteralFamilyCount()
    {
        if (CompiledEngineKind != Utf8CompiledEngineKind.LiteralFamily ||
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

    private Utf8ValueSplitEnumerator CreateSplitEnumeratorViaCompiledEngine(
        ReadOnlySpan<byte> input, int count, Utf8ExecutionDeadline budget)
    {
        // R05: anchored ASCII literal families split through the anchor-enforcing owned
        // cursors (at most one match at a fixed position), mirroring enumeration; the
        // split router admits these families past the simple-pattern gate below.
        if (_anchoredLiteral.Kind == Utf8AnchoredLiteralKind.AnchoredAsciiLiteralPrefix &&
            _anchoredLiteral.LiteralUtf8 is { Length: > 0 } anchoredSplitLiteral)
        {
            return new Utf8ValueSplitEnumerator(
                input,
                new Utf8OperationMatchCursor(input, anchoredSplitLiteral, budget),
                count);
        }

        // Folded anchored prefixes split the same single anchored hit with per-byte
        // invariant folding; Kelvin and Turkic divergences stay fenced in the split
        // router upstream.
        if (_anchoredLiteral.Kind == Utf8AnchoredLiteralKind.AnchoredFoldedAsciiLiteralPrefix &&
            _anchoredLiteral.LiteralUtf8 is { Length: > 0 } foldedSplitLiteral)
        {
            return new Utf8ValueSplitEnumerator(
                input,
                new Utf8OperationMatchCursor(input, foldedSplitLiteral, budget, _anchoredLiteral.Kind),
                count);
        }

        // Folded anchored suffixes split the same single tail hit with per-byte invariant
        // folding under the same router fence contract as prefixes.
        if ((_anchoredLiteral.IsExactSuffix ||
            _anchoredLiteral.Kind is Utf8AnchoredLiteralKind.AnchoredFoldedAsciiLiteralSuffix or Utf8AnchoredLiteralKind.AnchoredFoldedAsciiLiteralSuffixStrictEnd) &&
            _anchoredLiteral.LiteralUtf8 is { Length: > 0 } anchoredSplitSuffix)
        {
            return new Utf8ValueSplitEnumerator(
                input,
                new Utf8OperationMatchCursor(input, anchoredSplitSuffix, budget, _anchoredLiteral.Kind),
                count);
        }

        return Utf8CompiledOperationCursorFactory.CreateSplitEnumerator(
            _preparedRegex,
            input,
            count,
            budget);
    }
}
