using System.Buffers;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Text;
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
        var family = regex.Inspection.PreparedRegex.FallbackDirectFamily.Kind;
        if (family == Utf8FallbackDirectFamilyKind.Utf8WordDelimitedTokenCount)
        {
            metrics = InspectWordMetrics(input);
            return true;
        }
        if (family == Utf8FallbackDirectFamilyKind.Utf8UriToken)
        {
            metrics = Utf8InputAnalyzer.IsAscii(input) ? InspectAsciiUriMetrics(input) : InspectUnicodeUriMetrics(input);
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
            literal is not { Length: > 0 })
        {
            metrics = default;
            return false;
        }

        if (plan.ExecutionKind == NativeExecutionKind.ExactUtf8Literal &&
            Utf8LiteralCompiledEngineRuntime.CanUseFusedValidatedBmpThreeByteLiteralCount(literal))
        {
            metrics = InspectValidatedThreeByteMetrics(input, literal);
            return true;
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
            Utf8LiteralCompiledEngineRuntime.TryGetLeadingUtf8SegmentLength(literal, out var leadingSegmentLength))
        {
            metrics = InspectLeadingScalarMetrics(input, literal, leadingSegmentLength);
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
            Utf8LiteralCompiledEngineRuntime.TryGetExactUtf8LiteralAnchor(literal, out var anchorIndex, out var anchorByte))
        {
            metrics = InspectAnchoredMetrics(input, literal, anchorIndex, anchorByte);
            return true;
        }

        metrics = InspectFullSequenceSearchMetrics(input, literal, "SequenceSearchFallback");
        return true;

        static Utf8SelectedCountKernelMetrics InspectLeadingScalarMetrics(
            ReadOnlySpan<byte> input,
            ReadOnlySpan<byte> literal,
            int scalarLength)
        {
            var candidates = 0;
            var matches = 0;
            var searchIndex = 0;
            var maxStart = input.Length - literal.Length;
            var anchor = literal[..scalarLength];
            while (searchIndex <= maxStart)
            {
                var relative = input[searchIndex..].IndexOf(anchor);
                if (relative < 0)
                {
                    break;
                }

                candidates++;
                var candidate = searchIndex + relative;
                if (input.Slice(candidate, literal.Length).SequenceEqual(literal))
                {
                    matches++;
                    searchIndex = candidate + literal.Length;
                }
                else
                {
                    searchIndex = candidate + 1;
                }
            }

            return new Utf8SelectedCountKernelMetrics(
                "LeadingUtf8Scalar",
                "leading-scalar sequence hits",
                candidates,
                candidates,
                matches,
                IncludesUtf8Validation: false);
        }
    }

    private static Utf8SelectedCountKernelMetrics InspectValidatedThreeByteMetrics(
        ReadOnlySpan<byte> input,
        ReadOnlySpan<byte> literal)
    {
        var candidates = 0;
        var matches = 0;
        var index = 0;
        var maxStart = input.Length - literal.Length;
        while (index < input.Length)
        {
            if (Rune.DecodeFromUtf8(input[index..], out _, out var consumed) != OperationStatus.Done)
            {
                throw new InvalidOperationException("Selected Count metric replay received malformed UTF-8.");
            }

            if (index <= maxStart && input[index] == literal[0])
            {
                candidates++;
                if (input.Slice(index, literal.Length).SequenceEqual(literal))
                {
                    matches++;
                    index += literal.Length;
                    continue;
                }
            }

            index += consumed;
        }

        return new Utf8SelectedCountKernelMetrics(
            "FusedValidatedBmpThreeByte",
            "scalar starts matching the literal lead byte",
            candidates,
            candidates,
            matches,
            IncludesUtf8Validation: true);
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
            matches,
            IncludesUtf8Validation: false);
    }

    private static Utf8SelectedCountKernelMetrics InspectAnchoredMetrics(
        ReadOnlySpan<byte> input,
        ReadOnlySpan<byte> literal,
        int anchorIndex,
        byte anchorByte)
    {
        var candidates = 0;
        var matches = 0;
        var searchIndex = anchorIndex;
        var maxStart = input.Length - literal.Length;
        while (searchIndex < input.Length)
        {
            var relative = input[searchIndex..].IndexOf(anchorByte);
            if (relative < 0)
            {
                break;
            }

            var anchorPosition = searchIndex + relative;
            var candidate = anchorPosition - anchorIndex;
            searchIndex = anchorPosition + 1;
            if ((uint)candidate > (uint)maxStart)
            {
                continue;
            }

            candidates++;
            if (input.Slice(candidate, literal.Length).SequenceEqual(literal))
            {
                matches++;
                searchIndex = candidate + literal.Length + anchorIndex;
            }
        }

        return new Utf8SelectedCountKernelMetrics(
            "RareByteAnchored",
            $"byte 0x{anchorByte:X2} hits at literal offset {anchorIndex}",
            candidates,
            candidates,
            matches,
            IncludesUtf8Validation: false);
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
            matches,
            IncludesUtf8Validation: false);
    }

    private static Utf8SelectedCountKernelMetrics InspectWordMetrics(ReadOnlySpan<byte> input)
    {
        var candidates = 0;
        var matches = 0;
        var searchFrom = 0;
        while ((uint)searchFrom < (uint)input.Length)
        {
            var relative = input[searchFrom..].IndexOf((byte)'@');
            if (relative < 0)
            {
                break;
            }

            candidates++;
            var delimiterIndex = searchFrom + relative;
            var tokenStart = delimiterIndex;
            while (tokenStart > searchFrom && Utf8WordDelimitedTokenExecutor.TryConsumePreviousHead(input, ref tokenStart))
            {
            }

            if (tokenStart == delimiterIndex)
            {
                searchFrom = delimiterIndex + 1;
                continue;
            }

            var tokenEnd = delimiterIndex + 1;
            var bodyScalarCount = 0;
            var lastSecondaryIndex = -1;
            var hasSecondaryWithTail = false;
            while (Utf8WordDelimitedTokenExecutor.TryConsumeNextBody(input, ref tokenEnd, out var scalarStart, out var isSecondary))
            {
                if (lastSecondaryIndex > delimiterIndex + 1)
                {
                    hasSecondaryWithTail = true;
                }

                if (isSecondary)
                {
                    lastSecondaryIndex = scalarStart;
                }

                bodyScalarCount++;
            }

            if (bodyScalarCount > 0 && hasSecondaryWithTail)
            {
                matches++;
                searchFrom = tokenEnd;
            }
            else
            {
                searchFrom = delimiterIndex + 1;
            }
        }

        return new Utf8SelectedCountKernelMetrics(
            "FallbackDirect/Utf8WordDelimitedTokenCount",
            "@ delimiter probes",
            candidates,
            candidates,
            matches,
            IncludesUtf8Validation: false);
    }

    private static Utf8SelectedCountKernelMetrics InspectUnicodeUriMetrics(ReadOnlySpan<byte> input)
    {
        var invocation = new Utf8SliceInvocation(default, Timeout.InfiniteTimeSpan, default);
        var candidates = 0;
        var matches = 0;
        var searchFrom = 0;
        while ((uint)searchFrom < (uint)input.Length)
        {
            var relative = input[searchFrom..].IndexOf("://"u8);
            if (relative < 0)
            {
                break;
            }

            candidates++;
            var delimiterIndex = searchFrom + relative;
            if (!Utf8UriTokenCountExecutor.TryFindSchemeStart(input, searchFrom, delimiterIndex, ref invocation, out _) ||
                !Utf8UriTokenCountExecutor.TryConsumeBody(input, delimiterIndex + 3, ref invocation, out var tokenEnd))
            {
                searchFrom = delimiterIndex + 3;
                continue;
            }

            matches++;
            searchFrom = tokenEnd;
        }

        return new Utf8SelectedCountKernelMetrics(
            "FallbackDirect/Utf8UriToken",
            ":// delimiter probes",
            candidates,
            candidates,
            matches,
            IncludesUtf8Validation: false);
    }

    private static Utf8SelectedCountKernelMetrics InspectAsciiUriMetrics(ReadOnlySpan<byte> input)
    {
        var candidates = 0;
        var matches = 0;
        var startIndex = 0;
        while ((uint)startIndex < (uint)input.Length)
        {
            var searchFrom = startIndex;
            var foundMatch = false;
            while ((uint)searchFrom < (uint)input.Length)
            {
                var relative = input[searchFrom..].IndexOf((byte)':');
                if (relative < 0)
                {
                    break;
                }

                var delimiterIndex = searchFrom + relative;
                searchFrom = delimiterIndex + 1;
                if (input.Length - delimiterIndex < 3 ||
                    input[delimiterIndex + 1] != (byte)'/' ||
                    input[delimiterIndex + 2] != (byte)'/')
                {
                    continue;
                }

                candidates++;
                searchFrom += 2;
                if (!Utf8AsciiUriTokenExecutor.TryMatchAtDelimiter(input, startIndex, delimiterIndex, out var matchIndex, out var matchedLength))
                {
                    continue;
                }

                matches++;
                startIndex = matchIndex + Math.Max(matchedLength, 1);
                foundMatch = true;
                break;
            }

            if (!foundMatch)
            {
                break;
            }
        }

        return new Utf8SelectedCountKernelMetrics(
            "FallbackDirect/AsciiUriToken",
            ":// delimiter probes",
            candidates,
            candidates,
            matches,
            IncludesUtf8Validation: false);
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
            matches,
            IncludesUtf8Validation: false);
    }
}
