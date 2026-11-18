using Lokad.Utf8Regex.Internal.Input;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Planning;
using Lokad.Utf8Regex.Internal.Search;

namespace Lokad.Utf8Regex.Benchmarks;

internal static class Utf8MatchInspection
{
    public static bool IsMatchCompiled(Utf8Regex regex, ReadOnlySpan<byte> input, Utf8ValidationResult validation)
        => !Utf8PrefilterExecutor.Rejects(regex.Inspection.SearchPlan.PrefilterPlan, input) &&
           (regex.Inspection.CompiledRuntime is { } runtime
               ? runtime.IsMatch(input, Utf8ExecutionDeadline.Infinite)
               : Utf8PreparedScalarExecutor.IsMatch(regex.Inspection.PreparedRegex, input, validation, Utf8ExecutionDeadline.Infinite));

    public static Utf8CaptureRange MatchCompiled(Utf8Regex regex, ReadOnlySpan<byte> input, Utf8ValidationResult validation)
        => Utf8PrefilterExecutor.Rejects(regex.Inspection.SearchPlan.PrefilterPlan, input)
            ? default(Utf8CaptureRange)
            : regex.Inspection.CompiledRuntime is { } runtime
                ? runtime.Match(input, Utf8ExecutionDeadline.Infinite)
                : Utf8PreparedScalarExecutor.Match(regex.Inspection.PreparedRegex, input, validation, Utf8ExecutionDeadline.Infinite);

    public static bool TryCountSelectedKernelWithoutValidation(Utf8Regex regex, ReadOnlySpan<byte> input, out int count)
    {
        if (regex._packedNibbleLiteralFamilyCountStrategy is { } packed)
        {
            count = packed.Count(input);
            return true;
        }
        if (regex.Inspection.CompiledEngineKind is Utf8CompiledEngineKind.ExactLiteral or Utf8CompiledEngineKind.LiteralFamily)
        {
            count = regex.Inspection.CompiledRuntime!.Count(input, Utf8ExecutionDeadline.Infinite);
            return true;
        }
        count = 0;
        return false;
    }

    public static string SelectedCountKernelKind(Utf8Regex regex)
        => regex._packedNibbleLiteralFamilyCountStrategy is not null ? "PackedNibbleLiteralFamily"
            : regex.Inspection.CompiledEngineKind is Utf8CompiledEngineKind.ExactLiteral or Utf8CompiledEngineKind.LiteralFamily
                    ? $"{regex.Inspection.CompiledEngineKind}/{regex.Inspection.DebugCompiledEngineRuntimeType}" : "Unsupported";

    public static bool DebugTryMatchCompiledAsciiLiteralFamilyRaw(
        this Utf8Regex.Utf8RegexInspection inspection, ReadOnlySpan<byte> input, out int index, out int matchedByteLength)
    {
        var runtime = GetLiteralRuntime(inspection);
        if (runtime is null || !runtime.CanProjectLiteralFamilyAsAscii(input))
        {
            index = -1;
            matchedByteLength = 0;
            return false;
        }
        if (!runtime._usesRightToLeft &&
            runtime._smallAsciiLiteralFamilyPrimitive is { } primitive &&
            primitive.TryFindFirst(input, out index, out matchedByteLength))
            return true;
        if (!runtime._usesRightToLeft &&
            runtime._smallAsciiLiteralFamily is { } smallFamily &&
            runtime._smallAsciiLiteralFamilyFirstBytes.Count > 0 &&
            Utf8LiteralCompiledEngineRuntime.TryFindNextSmallAsciiLiteralFamily(input, 0, smallFamily,
                runtime._smallAsciiLiteralFamilyFirstBytes, out index, out matchedByteLength))
            return true;
        if (!runtime._usesRightToLeft &&
            runtime._emittedLiteralFamilyCounter is { } emitted &&
            emitted.TryMatch(input, out index, out matchedByteLength))
            return true;

        PreparedSearchMatch match;
        if (runtime._usesRightToLeft)
        {
            if (!Utf8SearchExecutor.TryFindLastMatch(inspection.SearchPlan, input, input.Length, out match))
            {
                index = -1;
                matchedByteLength = 0;
                return false;
            }
        }
        else if (!Utf8SearchExecutor.TryFindNextMatch(inspection.SearchPlan, input, 0, out match))
        {
            index = -1;
            matchedByteLength = 0;
            return false;
        }
        index = match.Index;
        matchedByteLength = match.Length;
        return index >= 0;
    }

    public static bool DebugTryIsMatchLiteralFamily(
        this Utf8Regex.Utf8RegexInspection inspection, ReadOnlySpan<byte> input, out bool isMatch)
    {
        var runtime = GetLiteralRuntime(inspection);
        if (runtime is null || runtime._usesRightToLeft ||
            inspection.ExecutionKind is not (NativeExecutionKind.ExactUtf8Literals or NativeExecutionKind.AsciiLiteralIgnoreCaseLiterals))
        {
            isMatch = false;
            return false;
        }
        isMatch = runtime.IsMatchLiteralFamily(input, Utf8ExecutionDeadline.Infinite, rightToLeft: false);
        return true;
    }

    public static bool DebugTryIsMatchExactLiteral(
        this Utf8Regex.Utf8RegexInspection inspection, ReadOnlySpan<byte> input, out bool isMatch)
    {
        var runtime = GetLiteralRuntime(inspection);
        if (runtime is null || runtime._usesRightToLeft ||
            inspection.ExecutionKind is not (NativeExecutionKind.ExactAsciiLiteral or NativeExecutionKind.ExactUtf8Literal))
        {
            isMatch = false;
            return false;
        }
        isMatch = runtime.FindFirstLiteralViaSearch(input, Utf8ExecutionDeadline.Infinite) >= 0;
        return true;
    }

    public static bool DebugTryMatchExactLiteral(
        this Utf8Regex.Utf8RegexInspection inspection, ReadOnlySpan<byte> input, out Utf8CaptureRange match)
    {
        var runtime = GetLiteralRuntime(inspection);
        if (runtime is null || runtime._usesRightToLeft)
        {
            match = default(Utf8CaptureRange);
            return false;
        }
        match = inspection.ExecutionKind switch
        {
            NativeExecutionKind.ExactAsciiLiteral => runtime.MatchExactAsciiLiteral(input, Utf8ExecutionDeadline.Infinite, rightToLeft: false),
            NativeExecutionKind.ExactUtf8Literal => runtime.MatchExactUtf8Literal(input, Utf8ExecutionDeadline.Infinite, rightToLeft: false),
            _ => default(Utf8CaptureRange),
        };
        return inspection.ExecutionKind is NativeExecutionKind.ExactAsciiLiteral or NativeExecutionKind.ExactUtf8Literal;
    }

    private static Utf8LiteralCompiledEngineRuntime? GetLiteralRuntime(Utf8Regex.Utf8RegexInspection inspection)
        => inspection.CompiledRuntime;

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

    public static bool DebugTryCountExactUtf8LiteralPreparedSearch(
        this Utf8Regex.Utf8RegexInspection inspection, ReadOnlySpan<byte> input, out int count)
    {
        if (inspection.CompiledEngineKind is Utf8CompiledEngineKind.ExactLiteral or Utf8CompiledEngineKind.LiteralFamily &&
            inspection.SearchPlan.LiteralSearch is { } search)
        {
            count = search.Count(input);
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
        var match = new Utf8CaptureRange(0, 0 + (matchedLength));
        return match.StartOffsetInBytes + (match.EndOffsetInBytes - match.StartOffsetInBytes);
    }

    public static int ProjectByteAlignedMatchOnly(int index, int matchedLength)
    {
        var match = new Utf8CaptureRange(index, (index) + (matchedLength));
        return match.StartOffsetInBytes + (match.EndOffsetInBytes - match.StartOffsetInBytes);
    }
}
