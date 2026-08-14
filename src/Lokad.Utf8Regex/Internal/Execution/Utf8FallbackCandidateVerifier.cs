using Lokad.Utf8Regex.Internal.Input;
using Lokad.Utf8Regex.Internal.Planning;
using System.Text;
using System.Text.RegularExpressions;

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

    /// <summary>
    /// Verifies one conservative byte-range candidate against the authoritative
    /// managed regex. The complete subject must already be valid UTF-8 and the
    /// candidate must use admitted byte boundaries. The shared map and decoded
    /// subject are created at most once and returned through their references.
    /// A false result is definitive for this candidate, not for later ones.
    /// </summary>
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

        var candidateEnd = boundaryMap.Resolve(boundaryMap.GetUtf16OffsetForByteOffset(candidate.EndIndex));
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

        var trailingStart = boundaryMap.Resolve(boundaryMap.GetUtf16OffsetForByteOffset(candidate.TrailingIndex));
        var trailingEnd = boundaryMap.Resolve(boundaryMap.GetUtf16OffsetForByteOffset(candidate.TrailingIndex + candidate.TrailingMatchLength));
        if (!trailingStart.IsScalarBoundary || !trailingEnd.IsScalarBoundary)
        {
            return false;
        }

        var matchEnd = match.Index + match.Length;
        return trailingStart.Utf16Offset >= match.Index &&
            trailingEnd.Utf16Offset <= matchEnd;
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
        var map = boundaryMap ?? Utf8BoundaryMap.Create(input, validation);
        boundaryMap = map;
        var candidateStart = map.Resolve(map.GetUtf16OffsetForByteOffset(candidate.StartIndex));
        decoded ??= Encoding.UTF8.GetString(input);
        var match = MatchCandidate(decoded, candidateStart.Utf16Offset);
        if (!IsVerifiedMatch(
                match,
                candidateStart.Utf16Offset,
                candidate,
                map,
                Plan.RequiresCandidateEndCoverage,
                Plan.RequiresTrailingAnchorCoverage))
        {
            return false;
        }

        var end = map.Resolve(match.Index + match.Length);
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
        result = default;
        if (candidate.EndIndex <= candidate.StartIndex ||
            candidate.StartIndex < 0 ||
            candidate.EndIndex > input.Length)
        {
            return false;
        }

        var map = boundaryMap ?? Utf8BoundaryMap.Create(input, validation);
        boundaryMap = map;
        var candidateStart = map.Resolve(map.GetUtf16OffsetForByteOffset(candidate.StartIndex));
        var candidateEnd = map.Resolve(map.GetUtf16OffsetForByteOffset(candidate.EndIndex));
        if (!candidateStart.IsScalarBoundary || !candidateEnd.IsScalarBoundary)
        {
            return false;
        }

        decoded ??= Encoding.UTF8.GetString(input);
        var anchoredRegex = AnchoredFallbackRegex ?? throw new InvalidOperationException("A bounded verifier requires an anchored fallback regex.");
        var matches = anchoredRegex.EnumerateMatches(decoded.AsSpan(
            candidateStart.Utf16Offset,
            candidateEnd.Utf16Offset - candidateStart.Utf16Offset));
        if (!matches.MoveNext() || matches.Current.Index != 0)
        {
            return false;
        }

        var valueMatch = matches.Current;
        var match = new FallbackVerifierMatch(
            Success: true,
            Index: candidateStart.Utf16Offset,
            Length: valueMatch.Length);
        if (!IsVerifiedMatch(
                match,
                candidateStart.Utf16Offset,
                candidate,
                map,
                Plan.RequiresCandidateEndCoverage,
                Plan.RequiresTrailingAnchorCoverage))
        {
            return false;
        }

        var matchEnd = map.Resolve(match.Index + match.Length);
        result = new Utf8FallbackVerificationResult(
            Success: true,
            IndexInUtf16: candidateStart.Utf16Offset,
            LengthInUtf16: match.Length,
            IndexInBytes: candidate.StartIndex,
            LengthInBytes: matchEnd.ByteOffset - candidate.StartIndex,
            IsByteAligned: matchEnd.IsScalarBoundary);
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
