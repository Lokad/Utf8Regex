using Lokad.Utf8Regex.Internal.Input;
using Lokad.Utf8Regex.Internal.Planning;

namespace Lokad.Utf8Regex.Internal.Execution;

using Lokad.Utf8Regex.Internal.Search;

internal static class Utf8ProjectionExecutor
{
    internal static Utf8ExecutionLimitKind TryProjectScalarVerification(ReadOnlySpan<byte> input,
        Utf8ValidationResult validation, in Utf8ScalarVerificationResult verification,
        ref Utf8SliceInvocation invocation, out Utf8ValueMatch match)
    {
        match = Utf8ValueMatch.NoMatch;
        if (!verification.Success) return Utf8ExecutionLimitKind.None;
        var index = verification.IndexInBytes;
        var length = verification.LengthInBytes;
        if (!validation.IsAscii)
        {
            var breach = Utf8Validation.TryValidate(input[..index], ref invocation, out var prefix);
            if (breach != Utf8ExecutionLimitKind.None) return breach;
            breach = Utf8Validation.TryValidate(input.Slice(index, length), ref invocation, out var extent);
            if (breach != Utf8ExecutionLimitKind.None) return breach;
            index = prefix.Utf16Length;
            length = extent.Utf16Length;
        }
        var final = invocation.TryChargeInputStep();
        invocation.Budget.ThrowIfCancellationRequested();
        if (final != Utf8ExecutionLimitKind.None) return final;
        match = new(true, true, index, length, verification.IndexInBytes, verification.LengthInBytes);
        return Utf8ExecutionLimitKind.None;
    }



    public static Utf8ValueMatch ProjectLiteralFamilyMatch(
        Utf8SearchOperationPlan program,
        ReadOnlySpan<byte> input,
        int[]? alternateLiteralUtf16Lengths,
        int consumedBytes,
        int consumedUtf16,
        in PreparedSearchMatch match,
        ref Utf8SliceInvocation invocation,
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
            ref invocation,
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
        ref Utf8SliceInvocation invocation,
        out int nextConsumedBytes,
        out int nextConsumedUtf16)
    {
        var matchedByteLength = match.Length;
        var matchedUtf16Length = alternateLiteralUtf16Lengths is { Length: > 0 } utf16Lengths &&
            (uint)match.LiteralId < (uint)utf16Lengths.Length
            ? utf16Lengths[match.LiteralId]
            : Utf8Validation.Validate(input.Slice(match.Index, matchedByteLength), ref invocation).Utf16Length;

        return ProjectMatch(
            projectionPlan,
            input,
            consumedBytes,
            consumedUtf16,
            match.Index,
            matchedByteLength,
            matchedUtf16Length,
            ref invocation,
            out nextConsumedBytes,
            out nextConsumedUtf16);
    }

    public static Utf8ValueMatch ProjectMatch(
        Utf8SearchOperationPlan program,
        ReadOnlySpan<byte> input,
        int consumedBytes,
        int consumedUtf16,
        int matchIndex,
        int matchedByteLength,
        int matchedUtf16Length,
        ref Utf8SliceInvocation invocation,
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
            ref invocation,
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
        ref Utf8SliceInvocation invocation,
        out int nextConsumedBytes,
        out int nextConsumedUtf16)
    {
        var breach = TryProjectMatch(projectionPlan, input, consumedBytes, consumedUtf16,
            matchIndex, matchedByteLength, matchedUtf16Length, ref invocation,
            out var projected, out nextConsumedBytes, out nextConsumedUtf16);
        if (breach != Utf8ExecutionLimitKind.None)
            throw new InvalidOperationException($"Match projection exceeded the invocation {breach} budget.");
        return projected;
    }

    internal static Utf8ExecutionLimitKind TryProjectMatch(
        Utf8ProjectionPlan projectionPlan, ReadOnlySpan<byte> input,
        int consumedBytes, int consumedUtf16, int matchIndex,
        int matchedByteLength, int matchedUtf16Length,
        ref Utf8SliceInvocation invocation, out Utf8ValueMatch projected,
        out int nextConsumedBytes, out int nextConsumedUtf16)
    {
        projected = Utf8ValueMatch.NoMatch;
        nextConsumedBytes = consumedBytes;
        nextConsumedUtf16 = consumedUtf16;
        var breach = invocation.TryChargeInputStep();
        if (breach != Utf8ExecutionLimitKind.None) return breach;
        invocation.Budget.ThrowIfCancellationRequested();
        var relativeUtf16Index = matchIndex - consumedBytes;
        if ((projectionPlan.Kind is Utf8ProjectionKind.Utf16Incremental or Utf8ProjectionKind.Utf16BoundaryMap) &&
            matchIndex != consumedBytes)
        {
            breach = Utf8Validation.TryValidate(input.Slice(consumedBytes, matchIndex - consumedBytes),
                ref invocation, out var gap);
            if (breach != Utf8ExecutionLimitKind.None) return breach;
            relativeUtf16Index = gap.Utf16Length;
        }

        breach = invocation.TryChargeInputStep();
        if (breach != Utf8ExecutionLimitKind.None) return breach;
        invocation.Budget.ThrowIfCancellationRequested();
        nextConsumedBytes = matchIndex + matchedByteLength;
        nextConsumedUtf16 = consumedUtf16 + relativeUtf16Index + matchedUtf16Length;

        projected = new Utf8ValueMatch(
            success: true,
            isByteAligned: true,
            indexInUtf16: consumedUtf16 + relativeUtf16Index,
            lengthInUtf16: matchedUtf16Length,
            indexInBytes: matchIndex,
            lengthInBytes: matchedByteLength);
        return Utf8ExecutionLimitKind.None;
    }
}
