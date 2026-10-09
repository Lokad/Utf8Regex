using Lokad.Utf8Regex.Internal.Input;
using Lokad.Utf8Regex.Internal.Planning;
using System.Text;
using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex.Internal.Execution;

internal abstract class Utf8FallbackCandidateVerifier
{
    // R03: BCL engines construct lazily so owned routes never construct them; the first
    // fallback use pays the construction cost once (thread-safe).
    internal readonly record struct ConstructionState(
        Utf8FallbackVerifierPlan Plan, Lazy<Regex> FallbackRegex,
        Lazy<Regex>? AnchoredFallbackRegex, RegexOptions FallbackOptions);

    protected Utf8FallbackCandidateVerifier(ConstructionState state)
    {
        Plan = state.Plan;
        FallbackOptions = state.FallbackOptions;
        _fallbackRegex = state.FallbackRegex;
        _anchoredFallbackRegex = state.AnchoredFallbackRegex;
    }

    public Utf8FallbackVerifierPlan Plan { get; }

    /// <summary>Gets the options the lazily constructed fallback engines use, so routing
    /// decisions can read them without constructing anything.</summary>
    public RegexOptions FallbackOptions { get; }

    private readonly Lazy<Regex> _fallbackRegex;

    private readonly Lazy<Regex>? _anchoredFallbackRegex;

    public Regex FallbackRegex => _fallbackRegex.Value;

    public Regex? AnchoredFallbackRegex => _anchoredFallbackRegex?.Value;

    public bool IsFallbackRegexCreated => _fallbackRegex.IsValueCreated;

    public bool IsAnchoredFallbackRegexCreated => _anchoredFallbackRegex?.IsValueCreated ?? false;

    internal int[] GetGroupNumbers() => FallbackRegex.GetGroupNumbers();

    internal string[] GetGroupNames() => FallbackRegex.GetGroupNames();

    /// <summary>
    /// Verifies one conservative byte-range candidate against the transitional
    /// BCL regex. This migration route does not establish scalar acceptance.
    /// The complete subject must already be valid UTF-8 and the
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

        if (!boundaryMap.TryGetUtf16OffsetForByteOffset(candidate.EndIndex, out var candidateEndUtf16Offset))
        {
            // Scalar-split candidate end (astral input): byte-exact ends do not exist
            // in char space. Start and exact-length candidates carry no trailing anchor
            // (TrailingIndex < 0); their fixed length holds by construction for the
            // BCL-verified match starting here, so coverage holds. Windowed candidates
            // keep failing: their trailing bounds cannot be evaluated on a split end.
            return candidate.TrailingIndex < 0;
        }

        var candidateEnd = boundaryMap.Resolve(candidateEndUtf16Offset);
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

        if (!boundaryMap.TryGetUtf16OffsetForByteOffset(candidate.TrailingIndex, out var trailingStartUtf16Offset) ||
            !boundaryMap.TryGetUtf16OffsetForByteOffset(candidate.TrailingIndex + candidate.TrailingMatchLength, out var trailingEndUtf16Offset))
        {
            return false;
        }

        var trailingStart = boundaryMap.Resolve(trailingStartUtf16Offset);
        var trailingEnd = boundaryMap.Resolve(trailingEndUtf16Offset);
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
    internal Utf8StartFallbackCandidateVerifier(ConstructionState state)
        : base(state)
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
        if (!map.TryGetUtf16OffsetForByteOffset(candidate.StartIndex, out var candidateStartUtf16Offset))
        {
            return false;
        }

        var candidateStart = map.Resolve(candidateStartUtf16Offset);
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
    internal Utf8BoundedSliceFallbackCandidateVerifier(ConstructionState state)
        : base(state)
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
        if (!map.TryGetUtf16OffsetForByteOffset(candidate.StartIndex, out var candidateStartUtf16Offset))
        {
            return false;
        }

        var candidateStart = map.Resolve(candidateStartUtf16Offset);
        if (!map.TryGetUtf16OffsetForByteOffset(candidate.EndIndex, out var candidateEndUtf16Offset))
        {
            // Scalar-split candidate end (astral input): the bounded slice cannot end
            // exactly, so verify anchored from the candidate start over the tail and let
            // the split-tolerant end coverage decide.
            decoded ??= Encoding.UTF8.GetString(input);
            return TryVerifyWholeTail(candidate, map, decoded, candidateStart, ref result);
        }

        var candidateEnd = map.Resolve(candidateEndUtf16Offset);
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
            // Span missed: byte-measured window ends can exclude the true tail on
            // non-ASCII input, so retry anchored over the whole tail; ASCII keeps
            // the single span attempt and coverage still decides everywhere.
            return !validation.IsAscii &&
                TryVerifyWholeTail(candidate, map, decoded, candidateStart, ref result);
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

    // Whole-tail anchored verification shared by scalar-split ends and span misses:
    // BCL decides from the candidate start over the full tail while the plan
    // coverage still adjudicates window bounds.
    private bool TryVerifyWholeTail(
        Utf8StructuralCandidate candidate,
        Utf8BoundaryMap map,
        string decoded,
        Utf16Boundary candidateStart,
        ref Utf8FallbackVerificationResult result)
    {
        result = default;
        var tailMatch = MatchCandidate(decoded, candidateStart.Utf16Offset);
        if (!IsVerifiedMatch(
                tailMatch,
                candidateStart.Utf16Offset,
                candidate,
                map,
                Plan.RequiresCandidateEndCoverage,
                Plan.RequiresTrailingAnchorCoverage))
        {
            return false;
        }

        var tailEnd = map.Resolve(tailMatch.Index + tailMatch.Length);
        var tailAligned = candidateStart.IsScalarBoundary && tailEnd.IsScalarBoundary;
        result = new Utf8FallbackVerificationResult(
            Success: true,
            IndexInUtf16: tailMatch.Index,
            LengthInUtf16: tailMatch.Length,
            IndexInBytes: candidateStart.ByteOffset,
            LengthInBytes: tailEnd.ByteOffset - candidateStart.ByteOffset,
            IsByteAligned: tailAligned);
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
