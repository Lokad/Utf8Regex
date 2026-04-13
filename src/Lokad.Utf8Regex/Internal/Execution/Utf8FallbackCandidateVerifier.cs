using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Input;
using Lokad.Utf8Regex.Internal.Planning;

namespace Lokad.Utf8Regex.Internal.Execution;

internal abstract class Utf8FallbackCandidateVerifier
{
    protected Utf8FallbackCandidateVerifier(Utf8FallbackVerifierPlan plan, Regex fallbackRegex, Regex? anchoredFallbackRegex)
    {
        Plan = plan;
        FallbackRegex = fallbackRegex;
        AnchoredFallbackRegex = anchoredFallbackRegex;
    }

    public Utf8FallbackVerifierPlan Plan { get; }

    public Regex FallbackRegex { get; }

    public Regex? AnchoredFallbackRegex { get; }

    public abstract bool TryVerify(
        ReadOnlySpan<byte> input,
        Utf8StructuralCandidate candidate,
        Utf8ValidationResult validation,
        ref Utf8BoundaryMap? boundaryMap,
        ref string? decoded,
        out Utf8FallbackVerificationResult result);

    protected FallbackVerifierMatch MatchCandidate(string decoded, int startUtf16Offset)
    {
        if (AnchoredFallbackRegex is null)
        {
            var match = FallbackRegex.Match(decoded, startUtf16Offset);
            return new FallbackVerifierMatch(match.Success, match.Index, match.Length);
        }

        var anchored = AnchoredFallbackRegex.Match(decoded, startUtf16Offset);
        return new FallbackVerifierMatch(anchored.Success, anchored.Index, anchored.Length);
    }

    protected static bool IsVerifiedMatch(
        FallbackVerifierMatch match,
        int candidateStartUtf16Offset,
        Utf8StructuralCandidate candidate,
        Utf8BoundaryMap boundaryMap,
        bool requiresCandidateEndCoverage,
        bool requiresTrailingAnchorCoverage)
    {
        if (!match.Success || match.Index != candidateStartUtf16Offset)
        {
            return false;
        }

        if (requiresTrailingAnchorCoverage &&
            !MatchesTrailingAnchorCoverage(match, candidate, boundaryMap))
        {
            return false;
        }

        if (!requiresCandidateEndCoverage || candidate.EndIndex < 0)
        {
            return true;
        }

        var candidateEnd = boundaryMap.Resolve(candidate.EndIndex);
        return candidateEnd.IsScalarBoundary && match.Index + match.Length >= candidateEnd.Utf16Offset;
    }

    protected static bool MatchesTrailingAnchorCoverage(
        FallbackVerifierMatch match,
        Utf8StructuralCandidate candidate,
        Utf8BoundaryMap boundaryMap)
    {
        if (candidate.TrailingIndex < 0 || candidate.TrailingMatchLength <= 0)
        {
            return false;
        }

        var trailingStart = boundaryMap.Resolve(candidate.TrailingIndex);
        var trailingEnd = boundaryMap.Resolve(candidate.TrailingIndex + candidate.TrailingMatchLength);
        if (!trailingStart.IsScalarBoundary || !trailingEnd.IsScalarBoundary)
        {
            return false;
        }

        var matchEnd = match.Index + match.Length;
        return trailingStart.Utf16Offset >= match.Index &&
            trailingEnd.Utf16Offset <= matchEnd;
    }

    protected static bool MatchesTrailingAnchorCoverage(
        int matchedUtf16Length,
        Utf8StructuralCandidate candidate,
        int baseByteOffset,
        ReadOnlySpan<byte> slice,
        Utf8ValidationResult validation)
    {
        if (candidate.TrailingIndex < 0 || candidate.TrailingMatchLength <= 0)
        {
            return false;
        }

        var trailingStartByte = candidate.TrailingIndex - baseByteOffset;
        var trailingEndByte = trailingStartByte + candidate.TrailingMatchLength;
        if (trailingStartByte < 0 || trailingEndByte > slice.Length)
        {
            return false;
        }

        var matchedEndByte = validation.IsAscii
            ? matchedUtf16Length
            : Utf8BoundaryMap.Create(slice, validation).Resolve(matchedUtf16Length).ByteOffset;
        return trailingEndByte <= matchedEndByte;
    }

    protected readonly record struct FallbackVerifierMatch(bool Success, int Index, int Length);
}

internal sealed class Utf8StartFallbackCandidateVerifier : Utf8FallbackCandidateVerifier
{
    public Utf8StartFallbackCandidateVerifier(Utf8FallbackVerifierPlan plan, Regex fallbackRegex, Regex? anchoredFallbackRegex)
        : base(plan, fallbackRegex, anchoredFallbackRegex)
    {
    }

    public override bool TryVerify(
        ReadOnlySpan<byte> input,
        Utf8StructuralCandidate candidate,
        Utf8ValidationResult validation,
        ref Utf8BoundaryMap? boundaryMap,
        ref string? decoded,
        out Utf8FallbackVerificationResult result)
    {
        result = default;
        boundaryMap ??= Utf8BoundaryMap.Create(input, validation);
        var candidateStart = boundaryMap.Resolve(boundaryMap.GetUtf16OffsetForByteOffset(candidate.StartIndex));
        decoded ??= Encoding.UTF8.GetString(input);
        var match = MatchCandidate(decoded, candidateStart.Utf16Offset);
        if (!IsVerifiedMatch(
                match,
                candidateStart.Utf16Offset,
                candidate,
                boundaryMap,
                Plan.RequiresCandidateEndCoverage,
                Plan.RequiresTrailingAnchorCoverage))
        {
            return false;
        }

        var end = boundaryMap.Resolve(match.Index + match.Length);
        var isByteAligned = candidateStart.IsScalarBoundary && end.IsScalarBoundary;
        result = new Utf8FallbackVerificationResult(
            Success: true,
            IndexInUtf16: match.Index,
            LengthInUtf16: match.Length,
            IndexInBytes: candidateStart.ByteOffset,
            LengthInBytes: end.ByteOffset - candidateStart.ByteOffset,
            IsByteAligned: isByteAligned);
        return true;
    }
}

internal sealed class Utf8BoundedSliceFallbackCandidateVerifier : Utf8FallbackCandidateVerifier
{
    public Utf8BoundedSliceFallbackCandidateVerifier(Utf8FallbackVerifierPlan plan, Regex fallbackRegex, Regex anchoredFallbackRegex)
        : base(plan, fallbackRegex, anchoredFallbackRegex)
    {
    }

    public override bool TryVerify(
        ReadOnlySpan<byte> input,
        Utf8StructuralCandidate candidate,
        Utf8ValidationResult validation,
        ref Utf8BoundaryMap? boundaryMap,
        ref string? decoded,
        out Utf8FallbackVerificationResult result)
    {
        decoded = null;
        result = default;
        if (candidate.EndIndex <= candidate.StartIndex ||
            candidate.StartIndex < 0 ||
            candidate.EndIndex > input.Length)
        {
            return false;
        }

        if ((candidate.StartIndex > 0 && (input[candidate.StartIndex] & 0xC0) == 0x80) ||
            (candidate.EndIndex < input.Length && (input[candidate.EndIndex] & 0xC0) == 0x80))
        {
            return false;
        }

        var slice = input[candidate.StartIndex..candidate.EndIndex];
        var sliceValidation = validation.IsAscii && slice.Length == input.Length
            ? validation
            : Utf8Validation.Validate(slice);
        var sliceDecoded = Encoding.UTF8.GetString(slice);
        var match = AnchoredFallbackRegex!.Match(sliceDecoded, 0);
        if (!match.Success || match.Index != 0)
        {
            return false;
        }

        if (Plan.RequiresCandidateEndCoverage &&
            match.Length < sliceDecoded.Length)
        {
            return false;
        }

        if (Plan.RequiresTrailingAnchorCoverage &&
            !MatchesTrailingAnchorCoverage(match.Length, candidate, candidate.StartIndex, slice, sliceValidation))
        {
            return false;
        }

        var matchedByteLength = sliceValidation.IsAscii
            ? match.Length
            : Utf8BoundaryMap.Create(slice, sliceValidation).Resolve(match.Length).ByteOffset;
        boundaryMap ??= Utf8BoundaryMap.Create(input, validation);
        var candidateStartBoundary = boundaryMap.Resolve(boundaryMap.GetUtf16OffsetForByteOffset(candidate.StartIndex));
        result = new Utf8FallbackVerificationResult(
            Success: true,
            IndexInUtf16: candidateStartBoundary.Utf16Offset,
            LengthInUtf16: match.Length,
            IndexInBytes: candidate.StartIndex,
            LengthInBytes: matchedByteLength,
            IsByteAligned: true);
        return true;
    }
}

internal readonly record struct Utf8FallbackVerificationResult(
    bool Success,
    int IndexInUtf16,
    int LengthInUtf16,
    int IndexInBytes,
    int LengthInBytes,
    bool IsByteAligned);
