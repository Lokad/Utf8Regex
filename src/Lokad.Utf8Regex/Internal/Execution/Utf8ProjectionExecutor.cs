using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Planning;

namespace Lokad.Utf8Regex.Internal.Execution;

internal static class Utf8ProjectionExecutor
{
    public static Utf8ValueMatch ProjectByteAlignedMatch(int indexInBytes, int lengthInBytes)
    {
        return new Utf8ValueMatch(true, true, indexInBytes, lengthInBytes, indexInBytes, lengthInBytes);
    }

    public static Utf8ValueMatch ProjectFallbackVerification(in Utf8FallbackVerificationResult verification)
    {
        return new Utf8ValueMatch(
            verification.Success,
            verification.IsByteAligned,
            verification.IndexInUtf16,
            verification.LengthInUtf16,
            verification.IndexInBytes,
            verification.LengthInBytes);
    }

    public static Utf8ValueMatch ProjectFallbackRegexMatch(ReadOnlySpan<byte> input, Match match, Utf8BoundaryMap boundaryMap)
    {
        if (!match.Success)
        {
            return Utf8ValueMatch.NoMatch;
        }

        if (boundaryMap.TryGetByteRange(match.Index, match.Length, out var indexInBytes, out var lengthInBytes))
        {
            return new Utf8ValueMatch(true, true, match.Index, match.Length, indexInBytes, lengthInBytes);
        }

        return new Utf8ValueMatch(true, false, match.Index, match.Length);
    }

    public static Utf8ValueMatch ProjectLiteralFamilyMatch(
        Utf8BackendInstructionProgram program,
        ReadOnlySpan<byte> input,
        int[]? alternateLiteralUtf16Lengths,
        int consumedBytes,
        int consumedUtf16,
        in PreparedSearchMatch match,
        out int nextConsumedBytes,
        out int nextConsumedUtf16)
    {
        return ProjectLiteralFamilyMatch(
            program.Projection,
            input,
            alternateLiteralUtf16Lengths,
            consumedBytes,
            consumedUtf16,
            match,
            out nextConsumedBytes,
            out nextConsumedUtf16);
    }

    public static Utf8ValueMatch ProjectLiteralFamilyMatch(
        Utf8ProjectionPlan projectionPlan,
        ReadOnlySpan<byte> input,
        int[]? alternateLiteralUtf16Lengths,
        int consumedBytes,
        int consumedUtf16,
        in PreparedSearchMatch match,
        out int nextConsumedBytes,
        out int nextConsumedUtf16)
    {
        var matchedByteLength = match.Length;
        var matchedUtf16Length = alternateLiteralUtf16Lengths is { Length: > 0 } utf16Lengths &&
            (uint)match.LiteralId < (uint)utf16Lengths.Length
            ? utf16Lengths[match.LiteralId]
            : Utf8Validation.Validate(input.Slice(match.Index, matchedByteLength)).Utf16Length;

        return ProjectMatch(
            projectionPlan,
            input,
            consumedBytes,
            consumedUtf16,
            match.Index,
            matchedByteLength,
            matchedUtf16Length,
            out nextConsumedBytes,
            out nextConsumedUtf16);
    }

    public static Utf8ValueMatch ProjectMatch(
        Utf8BackendInstructionProgram program,
        ReadOnlySpan<byte> input,
        int consumedBytes,
        int consumedUtf16,
        int matchIndex,
        int matchedByteLength,
        int matchedUtf16Length,
        out int nextConsumedBytes,
        out int nextConsumedUtf16)
    {
        return ProjectMatch(
            program.Projection,
            input,
            consumedBytes,
            consumedUtf16,
            matchIndex,
            matchedByteLength,
            matchedUtf16Length,
            out nextConsumedBytes,
            out nextConsumedUtf16);
    }

    public static Utf8ValueMatch ProjectMatch(
        Utf8ProjectionPlan projectionPlan,
        ReadOnlySpan<byte> input,
        int consumedBytes,
        int consumedUtf16,
        int matchIndex,
        int matchedByteLength,
        int matchedUtf16Length,
        out int nextConsumedBytes,
        out int nextConsumedUtf16)
    {
        var relativeUtf16Index = projectionPlan.Kind switch
        {
            Utf8ProjectionKind.ByteOnly => matchIndex - consumedBytes,
            Utf8ProjectionKind.Utf16Incremental or Utf8ProjectionKind.Utf16BoundaryMap =>
                matchIndex == consumedBytes
                    ? 0
                    : Utf8Validation.Validate(input.Slice(consumedBytes, matchIndex - consumedBytes)).Utf16Length,
            _ => matchIndex - consumedBytes,
        };

        nextConsumedBytes = matchIndex + matchedByteLength;
        nextConsumedUtf16 = consumedUtf16 + relativeUtf16Index + matchedUtf16Length;

        return new Utf8ValueMatch(
            success: true,
            isByteAligned: true,
            indexInUtf16: consumedUtf16 + relativeUtf16Index,
            lengthInUtf16: matchedUtf16Length,
            indexInBytes: matchIndex,
            lengthInBytes: matchedByteLength);
    }
}
