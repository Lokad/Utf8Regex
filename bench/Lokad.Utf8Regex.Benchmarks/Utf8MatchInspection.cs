using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Planning;

namespace Lokad.Utf8Regex.Benchmarks;

internal static class Utf8MatchInspection
{
    public static bool IsMatchCompiled(Utf8Regex regex, ReadOnlySpan<byte> input)
        => regex.Inspection.CompiledRuntime is { } runtime
            ? runtime.IsMatch(input, Utf8ExecutionDeadline.Infinite)
            : regex.IsMatch(input);

    public static Utf8CaptureRange MatchCompiled(Utf8Regex regex, ReadOnlySpan<byte> input)
        => regex.Inspection.CompiledRuntime is { } runtime
            ? runtime.Match(input, Utf8ExecutionDeadline.Infinite)
            : regex.Match(input);

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

}
