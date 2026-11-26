using System.Numerics;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Input;
using Lokad.Utf8Regex.Internal.Planning;
using Lokad.Utf8Regex.Internal.Search;

namespace Lokad.Utf8Regex.Benchmarks;

// Untimed diagnostic replays live with the benchmark driver, outside shipped libraries.
internal static class Utf8CountKernelCensus
{
    internal static bool TryInspect(Utf8Regex regex, ReadOnlySpan<byte> input, out Utf8SelectedCountKernelMetrics metrics)
    {
        if (regex.Inspection.DebugHasPackedNibbleLiteralFamilyCountStrategy)
        {
            var prefilter = PreparedMultiLiteralCandidatePrefilter.CreatePackedNibbleSimd(regex.Inspection.SearchPlan.AlternateLiteralsUtf8!);
            metrics = InspectPackedMetrics(input, prefilter);
            return true;
        }
        return TryInspectSelectedCountKernelMetrics(regex.Inspection.PreparedRegex, input, out metrics);
    }

    internal static bool TryInspectSelectedCountKernelMetrics(
        Utf8PreparedRegex plan,
        ReadOnlySpan<byte> input,
        out Utf8SelectedCountKernelMetrics metrics)
    {
        var literal = plan.LiteralUtf8;
        if (plan.ExecutionKind is not (NativeExecutionKind.ExactAsciiLiteral or NativeExecutionKind.ExactUtf8Literal) ||
            literal is not { Length: > 0 } ||
            literal.Length == 3 && literal[0] is >= 0xE0 and < 0xF0)
        {
            metrics = default;
            return false;
        }

        if (plan.ExecutionKind == NativeExecutionKind.ExactUtf8Literal &&
            literal.Length >= 4 &&
            literal[0] is >= 0xC2 and < 0xE0 &&
            input.Length >= Utf8LiteralCompiledEngineRuntime.CorrelatedTwoByteLiteralCountThresholdBytes &&
            Vector256.IsHardwareAccelerated)
        {
            metrics = InspectCorrelatedTwoBytePrefixMetrics(input, literal);
            return true;
        }

        if (plan.ExecutionKind == NativeExecutionKind.ExactUtf8Literal &&
            Utf8LiteralCompiledEngineRuntime.HasLeadingThreeByteScalarAndTail(literal))
        {
            var route = "SpanSequenceSearch";
            if (literal.Length > 16 && plan.SearchPlan.LiteralSearch is { HasExactAnchors: true } prepared)
                route = prepared.GetExactCountAnchor(input.Length) > 0 ? "BoundedByteAnchor" : "PreparedPrefixSearch";
            metrics = InspectFullSequenceSearchMetrics(input, literal, route);
            return true;
        }

        if (plan.SearchPlan.Kind == Utf8SearchKind.ExactAsciiLiteral &&
            literal.Length is >= 5 and <= 16)
        {
            metrics = InspectFullSequenceSearchMetrics(input, literal, "SpanSequenceSearch");
            return true;
        }

        if (plan.SearchPlan.LiteralSearch.HasValue)
        {
            metrics = InspectFullSequenceSearchMetrics(input, literal, "PreparedLiteralSearch");
            return true;
        }

        if (plan.ExecutionKind == NativeExecutionKind.ExactUtf8Literal &&
            Utf8LiteralCompiledEngineRuntime.TryGetExactUtf8LiteralAnchor(literal, out _, out _))
        {
            metrics = InspectFullSequenceSearchMetrics(input, literal, "BoundedByteAnchor");
            return true;
        }

        metrics = InspectFullSequenceSearchMetrics(input, literal, "SequenceSearchFallback");
        return true;
    }

    private static Utf8SelectedCountKernelMetrics InspectCorrelatedTwoBytePrefixMetrics(
        ReadOnlySpan<byte> input,
        ReadOnlySpan<byte> literal)
    {
        var candidates = 0;
        var matches = 0;
        var index = 0;
        var maxStart = input.Length - literal.Length;
        var first = Vector256.Create(literal[0]);
        var second = Vector256.Create(literal[1]);
        var fourth = Vector256.Create(literal[3]);

        while (index <= maxStart - (Vector256<byte>.Count - 1))
        {
            var correlated = Vector256.BitwiseAnd(
                Vector256.Equals(MemoryMarshal.Read<Vector256<byte>>(input[index..]), first),
                Vector256.Equals(MemoryMarshal.Read<Vector256<byte>>(input[(index + 1)..]), second));
            correlated = Vector256.BitwiseAnd(
                correlated,
                Vector256.Equals(MemoryMarshal.Read<Vector256<byte>>(input[(index + 3)..]), fourth));
            var candidateBits = correlated.ExtractMostSignificantBits();
            var matched = false;
            while (candidateBits != 0)
            {
                candidates++;
                var candidate = index + BitOperations.TrailingZeroCount(candidateBits);
                if (input.Slice(candidate, literal.Length).SequenceEqual(literal))
                {
                    matches++;
                    index = candidate + literal.Length;
                    matched = true;
                    break;
                }

                candidateBits &= candidateBits - 1;
            }

            if (!matched)
            {
                index += Vector256<byte>.Count;
            }
        }

        while (index <= maxStart)
        {
            if (input[index] == literal[0] &&
                input[index + 1] == literal[1] &&
                input[index + 3] == literal[3])
            {
                candidates++;
                if (input.Slice(index, literal.Length).SequenceEqual(literal))
                {
                    matches++;
                    index += literal.Length;
                    continue;
                }
            }

            index++;
        }

        return new Utf8SelectedCountKernelMetrics(
            "CorrelatedTwoBytePrefix",
            "starts matching literal bytes 0, 1, and 3",
            candidates,
            candidates,
            matches);
    }

    private static Utf8SelectedCountKernelMetrics InspectFullSequenceSearchMetrics(
        ReadOnlySpan<byte> input,
        ReadOnlySpan<byte> literal,
        string route)
    {
        var matches = 0;
        var index = 0;
        while (index <= input.Length - literal.Length)
        {
            var relative = input[index..].IndexOf(literal);
            if (relative < 0)
            {
                break;
            }

            matches++;
            index += relative + literal.Length;
        }

        return new Utf8SelectedCountKernelMetrics(
            route,
            "internal search candidates unavailable",
            Candidates: null,
            FullVerifications: null,
            matches);
    }

    private static Utf8SelectedCountKernelMetrics InspectPackedMetrics(ReadOnlySpan<byte> input, PreparedMultiLiteralCandidatePrefilter prefilter)
    {
        var candidates = 0;
        var fullVerifications = 0;
        var matches = 0;
        var state = new PreparedMultiLiteralScanState(0, 0, 0);
        while (prefilter.TryFindNextCandidate(input, ref state, out var candidateIndex))
        {
            candidates++;
            fullVerifications++;
            if (!prefilter.TryGetMatchedLength(input, candidateIndex, out var matchedLength))
            {
                continue;
            }

            matches++;
            state = new PreparedMultiLiteralScanState(candidateIndex + matchedLength, 0, 0);
        }

        return new Utf8SelectedCountKernelMetrics(
            "PackedNibbleLiteralFamily",
            "packed-nibble correlated start lanes",
            candidates,
            fullVerifications,
            matches);
    }
}
