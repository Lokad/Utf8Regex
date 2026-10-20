using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Planning;

namespace Lokad.Utf8Regex.Benchmarks;

internal static class Utf8MatchInspection
{
    // Alternative Count probes belong to benchmark tooling. Production keeps its
    // selected kernels; these probes reuse existing prepared metadata and bodies.
    public static bool DebugTryCountExactUtf8LiteralValidatedThreeByte(
        this Utf8Regex.Utf8RegexInspection inspection, ReadOnlySpan<byte> input, out int count)
    {
        var prepared = inspection.PreparedRegex;
        if (inspection.CompiledEngineKind is Utf8CompiledEngineKind.ExactLiteral or Utf8CompiledEngineKind.LiteralFamily &&
            prepared.ExecutionKind == NativeExecutionKind.ExactUtf8Literal &&
            prepared.LiteralUtf8 is { Length: > 0 } literal &&
            Utf8LiteralCompiledEngineRuntime.CanUseFusedValidatedBmpThreeByteLiteralCount(literal))
        {
            count = Utf8LiteralCompiledEngineRuntime.CountExactUtf8LiteralValidatedThreeByte(input, literal);
            return true;
        }
        count = 0;
        return false;
    }

    public static bool DebugTryCountExactUtf8LiteralLeadingScalarAnchored(
        this Utf8Regex.Utf8RegexInspection inspection, ReadOnlySpan<byte> input, out int count)
    {
        var prepared = inspection.PreparedRegex;
        if (inspection.CompiledEngineKind is Utf8CompiledEngineKind.ExactLiteral or Utf8CompiledEngineKind.LiteralFamily &&
            prepared.ExecutionKind == NativeExecutionKind.ExactUtf8Literal &&
            prepared.LiteralUtf8 is { Length: > 0 } literal &&
            Utf8LiteralCompiledEngineRuntime.TryGetLeadingUtf8SegmentLength(literal, out var leadingSegmentLength))
        {
            count = Utf8LiteralCompiledEngineRuntime.CountExactUtf8LiteralLeadingScalarAnchored(input, literal, leadingSegmentLength);
            return true;
        }
        count = 0;
        return false;
    }

    public static bool DebugTryCountExactUtf8LiteralPreparedSearch(
        this Utf8Regex.Utf8RegexInspection inspection, ReadOnlySpan<byte> input, out int count)
    {
        if (inspection.CompiledEngineKind is Utf8CompiledEngineKind.ExactLiteral or Utf8CompiledEngineKind.LiteralFamily &&
            inspection.SearchPlan.LiteralSearch is { } search)
        {
            count = search.CountWithMetrics(input, out _, out _);
            return true;
        }
        count = 0;
        return false;
    }

    public static bool DebugTryCountExactUtf8LiteralAnchored(
        this Utf8Regex.Utf8RegexInspection inspection, ReadOnlySpan<byte> input, out int count)
    {
        var prepared = inspection.PreparedRegex;
        if (inspection.CompiledEngineKind is Utf8CompiledEngineKind.ExactLiteral or Utf8CompiledEngineKind.LiteralFamily &&
            prepared.ExecutionKind == NativeExecutionKind.ExactUtf8Literal &&
            prepared.LiteralUtf8 is { Length: > 0 } literal &&
            Utf8LiteralCompiledEngineRuntime.TryGetExactUtf8LiteralAnchor(literal, out var anchorIndex, out var anchorByte))
        {
            count = Utf8LiteralCompiledEngineRuntime.CountExactUtf8LiteralAnchored(input, literal, anchorIndex, anchorByte);
            return true;
        }
        count = 0;
        return false;
    }

    public static int ProjectWholeMatchOnly(int matchedLength)
    {
        var match = new Utf8ValueMatch(true, true, 0, matchedLength, 0, matchedLength);
        return match.IndexInUtf16 + match.LengthInUtf16;
    }

    public static int ProjectByteAlignedMatchOnly(int index, int matchedLength)
    {
        var match = new Utf8ValueMatch(true, true, index, matchedLength, index, matchedLength);
        return match.IndexInUtf16 + match.LengthInUtf16;
    }
}
