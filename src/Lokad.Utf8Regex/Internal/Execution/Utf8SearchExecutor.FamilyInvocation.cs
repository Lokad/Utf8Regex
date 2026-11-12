using Lokad.Utf8Regex.Internal.Diagnostics;
using Lokad.Utf8Regex.Internal.Planning;
using Lokad.Utf8Regex.Internal.Search;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;

namespace Lokad.Utf8Regex.Internal.Execution;

internal static partial class Utf8SearchExecutor
{
    // Each branch retains its original ordinal. LTR chooses the earliest start;
    // RTL chooses the latest end, then the first branch at that end, as .NET does.
    // All rejected candidates and long predicates charge the same invocation.
    internal static Utf8ExecutionLimitKind TryFindLiteralFamily(
        in Utf8SearchPlan plan, ReadOnlySpan<byte> input, int start, int end,
        bool reverse, ref Utf8SliceInvocation invocation, out PreparedSearchMatch match,
        out bool found)
    {
        var folded = plan.Kind == Utf8SearchKind.AsciiFoldedByteLiterals;
        // With no confirmation predicates, complete-needle discovery already
        // proves this search language. Borrow its existing bounded windows and
        // original invocation; callers retain any enclosing regex verification.
        if (!folded && !reverse && !plan.HasBoundaryRequirements && !plan.HasTrailingLiteralRequirement &&
            plan.AlternateLiteralsUtf8 is { Length: >= 3 and <= 8 } literals)
            return TryFindSmallExactFamilyCandidate(literals, input, start, end,
                ref invocation, out match, out found, plan.AlternateLiteralSearch?.SearchData ?? default);
        PreparedByteSearch? firstByteSearch = null;
        if (folded && plan.AlternateIgnoreCaseLiteralSearch is
            { ShortestLength: > 0 and < int.MaxValue } foldedSearch)
            firstByteSearch = foldedSearch.FirstByteSearch;
        else if (!folded && plan.AlternateLiteralSearch is
            { SearchData.ShortestLength: > 0 and < int.MaxValue } exactSearch)
            firstByteSearch = exactSearch.SearchData.FirstByteSearch;
        return TryFindLiteralFamilyCore(plan.AlternateLiteralsUtf8 ?? [], folded, true,
            firstByteSearch, plan.LeadingBoundary, plan.TrailingBoundary, plan.TrailingLiteralUtf8,
            input, start, end, reverse, ref invocation, out match, out found);
    }

    // Prepared Python hints can lend their first-byte union without applying
    // Core boundary/suffix predicates. Only the necessary start escapes; the
    // profile VM verifies ordered alternatives and captures there.
    internal static Utf8ExecutionLimitKind TryFindExactLiteralFamilyCandidate(
        in Utf8SearchPlan plan, ReadOnlySpan<byte> input, int start, int end,
        ref Utf8SliceInvocation invocation, out int candidate)
    {
        candidate = -1;
        var literals = plan.AlternateLiteralsUtf8 ?? [];
        // Preserve the existing pair and small complete-needle searches.
        if (literals.Length <= 8)
        {
            var breach = literals.Length >= 3
                ? TryFindSmallExactFamilyCandidate(literals, input, start, end, ref invocation,
                    out var smallMatch, out var smallFound, plan.AlternateLiteralSearch?.SearchData ?? default)
                : TryFindLiteralFamilyCandidate(literals, false, input, start, end, false,
                    ref invocation, out smallMatch, out smallFound);
            if (breach == Utf8ExecutionLimitKind.None && smallFound) candidate = smallMatch.Index;
            return breach;
        }
        // A short remaining subject bounds the prepared trie's whole walk.
        // Reuse its shared prefixes instead of confirming each expanded needle
        // after every first-byte hit. Only a necessary start escapes to the VM.
        if ((uint)(end - start) <= 256 && plan.AlternateLiteralSearch is
            { Strategy: PreparedLiteralSetStrategy.MultiLiteralTrie } trie)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(start);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(end, input.Length);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(start, end);
            var breach = invocation.TryChargeInputStep();
            if (breach != Utf8ExecutionLimitKind.None) return breach;
            var relative = trie.IndexOf(input[start..end]);
            breach = invocation.TryChargeInputStep();
            invocation.Budget.ThrowIfCancellationRequested();
            if (breach == Utf8ExecutionLimitKind.None && relative >= 0) candidate = start + relative;
            return breach;
        }
        var buckets = plan.AlternateLiteralSearch?.SearchData.Buckets;
        if (buckets is { Length: 1 } && buckets[0].PrefixDiscriminator is { HasValue: true } discriminator)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(start);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(end, input.Length);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(start, end);
            var first = buckets[0].FirstByte;
            for (var low = start; low < end;)
            {
                var breach = invocation.TryChargeInputStep();
                if (breach != Utf8ExecutionLimitKind.None) return breach;
                var count = Math.Min(2048, end - low);
                var relative = input.Slice(low, count).IndexOf(first);
                if (relative < 0) { low += count; continue; }
                var at = low + relative;
                // The prepared column is unique across all branches. A selected
                // byte proves no prefix: confirm the entire needle under budget.
                if ((uint)discriminator.Offset < (uint)(end - at) &&
                    discriminator.LiteralsByByte[input[at + discriminator.Offset]] is { } literal &&
                    literal.Length <= end - at)
                {
                    breach = TryCompareLiteral(input.Slice(at, literal.Length), literal, false,
                        ref invocation, out var equal);
                    if (breach != Utf8ExecutionLimitKind.None) return breach;
                    if (equal)
                    {
                        breach = invocation.TryChargeInputStep();
                        invocation.Budget.ThrowIfCancellationRequested();
                        if (breach == Utf8ExecutionLimitKind.None) candidate = at;
                        return breach;
                    }
                }
                low = at + 1;
            }
            var completion = invocation.TryChargeInputStep();
            invocation.Budget.ThrowIfCancellationRequested();
            return completion;
        }
        var result = TryFindLiteralFamilyCore(literals, false, false,
            plan.AlternateLiteralSearch?.SearchData.FirstByteSearch,
            Utf8BoundaryRequirement.None, Utf8BoundaryRequirement.None, null,
            input, start, end, false, ref invocation, out var match, out var found);
        if (result == Utf8ExecutionLimitKind.None && found) candidate = match.Index;
        return result;
    }

    internal static Utf8ExecutionLimitKind TryFindLiteralFamilyCandidate(
        byte[][] literals, bool folded, ReadOnlySpan<byte> input, int start, int end,
        bool reverse, ref Utf8SliceInvocation invocation, out PreparedSearchMatch match, out bool found)
        // Candidate searches have no union or confirmation predicates. Passing
        // these facts directly avoids materializing a large empty search plan.
        => !folded && !reverse && literals.Length is >= 3 and <= 8
            ? TryFindSmallExactFamilyCandidate(literals, input, start, end, ref invocation, out match, out found)
            : TryFindLiteralFamilyCore(literals, folded, false, null,
            Utf8BoundaryRequirement.None, Utf8BoundaryRequirement.None, null,
            input, start, end, reverse, ref invocation, out match, out found);

    // Keep the qualified pair/confirmed loops intact. Small raw exact families
    // can search complete needles in bounded spans without prepared storage or
    // per-byte false-candidate replay. The caller still verifies the full regex.
    private static Utf8ExecutionLimitKind TryFindSmallExactFamilyCandidate(byte[][] literals,
        ReadOnlySpan<byte> input, int start, int end, ref Utf8SliceInvocation invocation,
        out PreparedSearchMatch match, out bool found, in AsciiExactLiteralSearchData prepared = default)
    {
        match = default; found = false;
        ArgumentOutOfRangeException.ThrowIfNegative(start);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(end, input.Length);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(start, end);
        var longest = 0;
        foreach (var literal in literals)
        {
            var eligibility = invocation.TryChargeInputStep();
            if (eligibility != Utf8ExecutionLimitKind.None) return eligibility;
            if (literal.Length is 0 or > 1024)
                return TryFindLiteralFamilyCore(literals, false, false, null,
                    Utf8BoundaryRequirement.None, Utf8BoundaryRequirement.None, null,
                    input, start, end, false, ref invocation, out match, out found);
            longest = Math.Max(longest, literal.Length);
        }
        // One bucket per original needle proves distinct heads. Borrow only
        // short prepared families; raw/overlapping/other families keep the
        // complete per-needle primitive below, without new preparation.
        var usePrepared = prepared.Buckets is { Length: 3 or 4 } buckets &&
            buckets.Length == literals.Length && prepared.ShortestLength >= 3 && longest <= 16;
        for (var low = start; low < end;)
        {
            // Complete-needle lookahead fits in 2048 bytes. Later starts belong
            // to the next window; a full window advances at least 1025 starts.
            var starts = Math.Min(2049 - longest, end - low);
            var block = input.Slice(low, Math.Min(starts + longest - 1, end - low));
            var best = -1; var ordinal = -1;
            if (usePrepared)
            {
                // The same checked search used by ordinary execution runs
                // wholly inside this admitted window. Charge the combined
                // primitive once; original authority also gates publication.
                var breach = invocation.TryChargeInputStep();
                if (breach != Utf8ExecutionLimitKind.None) return breach;
                if (PreparedLiteralSetSearch.TryFindFirstMatchSingleLiteralBuckets(block, prepared,
                    out var relative, out _) && relative < starts)
                {
                    // Distinct heads identify the original branch after full
                    // verification. Bucket sorting cannot change its ordinal.
                    for (var id = 0; id < literals.Length; id++)
                        if (literals[id][0] == block[relative])
                        { best = relative; ordinal = id; break; }
                }
            }
            else for (var id = 0; id < literals.Length; id++)
            {
                var breach = invocation.TryChargeInputStep();
                if (breach != Utf8ExecutionLimitKind.None) return breach;
                var relative = block.IndexOf(literals[id]);
                if (relative >= 0 && relative < starts && (best < 0 || relative < best))
                { best = relative; ordinal = id; }
                if (best == 0) break;
            }
            if (best >= 0)
            {
                var publication = invocation.TryChargeInputStep();
                invocation.Budget.ThrowIfCancellationRequested();
                if (publication != Utf8ExecutionLimitKind.None) return publication;
                match = new PreparedSearchMatch(low + best, literals[ordinal].Length, ordinal);
                found = true;
                return Utf8ExecutionLimitKind.None;
            }
            low += starts;
        }
        var completion = invocation.TryChargeInputStep();
        invocation.Budget.ThrowIfCancellationRequested();
        return completion;
    }

    private static Utf8ExecutionLimitKind TryFindLiteralFamilyCore(
        byte[][] literals, bool folded, bool confirm, PreparedByteSearch? firstByteSearch,
        Utf8BoundaryRequirement leadingBoundary, Utf8BoundaryRequirement trailingBoundary,
        byte[]? trailingLiteral,
        ReadOnlySpan<byte> input, int start, int end, bool reverse,
        ref Utf8SliceInvocation invocation, out PreparedSearchMatch match, out bool found)
    {
        match = default;
        found = false;
        ArgumentOutOfRangeException.ThrowIfNegative(start);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(end, input.Length);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(start, end);
        var breach = invocation.TryChargeInputStep();
        if (breach != Utf8ExecutionLimitKind.None) return breach;
        var low = start;
        var high = end - 1;
        if (literals.Length > 0)
        {
            while (low <= high)
            {
                // Search only one candidate window for every branch. Searching
                // an absent branch to the subject end on every MoveNext would
                // make a dense later branch quadratic over the enumeration.
                var count = Math.Min(2048, high - low + 1);
                var blockStart = reverse ? high - count + 1 : low;
                var block = input.Slice(blockStart, count);
                var candidateByte = -1;
                if (!reverse && firstByteSearch is { } search)
                {
                    breach = invocation.TryChargeInputStep();
                    if (breach != Utf8ExecutionLimitKind.None) return breach;
                    // Borrow the prepared union instead of rescanning this
                    // bounded window once per branch at every false candidate.
                    var relative = search.IndexOf(block);
                    if (relative >= 0) candidateByte = blockStart + relative;
                }
                else if (!confirm && !reverse && !folded && literals.Length == 2 &&
                    literals[0].Length > 0 && literals[1].Length > 0)
                {
                    if (literals[0].Length <= 1024 && literals[1].Length <= 1024)
                    {
                        // Full-needle scans reserve lookahead inside the 2048-byte
                        // bound. Full windows still advance >=1025 starts.
                        // Later starts belong to the next window, even if a short
                        // needle is already visible in this one's lookahead.
                        var longest = Math.Max(literals[0].Length, literals[1].Length);
                        count = Math.Min(2049 - longest, high - low + 1);
                        var pairBlock = input.Slice(blockStart, Math.Min(count + longest - 1, end - blockStart));
                        breach = invocation.TryChargeInputStep();
                        if (breach != Utf8ExecutionLimitKind.None) return breach;
                        var shared = Vector512.IsHardwareAccelerated && count >= Vector512<byte>.Count &&
                            longest <= 16 && Math.Min(literals[0].Length, literals[1].Length) >= 3;
                        var provedId = -1;
                        var relative = shared ? IndexOfShortExactPair(pairBlock, count, literals[0], literals[1], out provedId)
                            : pairBlock.IndexOf(literals[0]);
                        if (relative >= count) relative = -1;
                        if (!shared && relative != 0)
                        {
                            breach = invocation.TryChargeInputStep();
                            if (breach != Utf8ExecutionLimitKind.None) return breach;
                            // Only an earlier start can beat the first branch.
                            // Include that last start's complete second needle;
                            // miss windows retain their full bounded scan.
                            var otherBlock = relative < 0 ? pairBlock :
                                pairBlock[..Math.Min(pairBlock.Length, relative + literals[1].Length - 1)];
                            var other = otherBlock.IndexOf(literals[1]);
                            if (other >= 0 && other < count && (relative < 0 || other < relative)) relative = other;
                        }
                        if (relative >= 0)
                        {
                            candidateByte = blockStart + relative;
                            if (shared)
                            {
                                // The bounded primitive already compared complete
                                // needles in pattern order. Publish that exact
                                // prefix proof under the original authority.
                                breach = invocation.TryChargeInputStep();
                                if (breach != Utf8ExecutionLimitKind.None) return breach;
                                invocation.Budget.ThrowIfCancellationRequested();
                                match = new PreparedSearchMatch(candidateByte, literals[provedId].Length, provedId);
                                found = true;
                                return Utf8ExecutionLimitKind.None;
                            }
                        }
                    }
                    else
                    {
                        // Long comparisons retain their original split checkpoints.
                        breach = invocation.TryChargeInputStep();
                        if (breach != Utf8ExecutionLimitKind.None) return breach;
                        var relative = block.IndexOfAny(literals[0][0], literals[1][0]);
                        if (relative >= 0) candidateByte = blockStart + relative;
                    }
                }
                else
                {
                    for (var id = 0; id < literals.Length; id++)
                    {
                        breach = invocation.TryChargeInputStep();
                        if (breach != Utf8ExecutionLimitKind.None) return breach;
                        var literal = literals[id];
                        if (literal.Length == 0)
                            throw new InvalidOperationException("Native literal families require nonempty branches.");
                        // Regex RTL literal execution prioritizes the latest end.
                        // Raw prepared candidate searches prioritize latest starts.
                        var value = reverse && confirm ? literal[^1] : literal[0];
                        var other = folded && (uint)((value | 0x20) - 'a') <= 'z' - 'a' ? (byte)(value ^ 0x20) : value;
                        var relative = reverse ? block.LastIndexOfAny(value, other) : block.IndexOfAny(value, other);
                        if (relative >= 0 && (candidateByte < 0 || (reverse
                                ? blockStart + relative > candidateByte : blockStart + relative < candidateByte)))
                            candidateByte = blockStart + relative;
                    }
                }
                if (candidateByte < 0)
                {
                    if (reverse) high -= count;
                    else low += count;
                    continue;
                }

                for (var id = 0; id < literals.Length; id++)
                {
                    breach = invocation.TryChargeInputStep();
                    if (breach != Utf8ExecutionLimitKind.None) return breach;
                    var literal = literals[id];
                    var candidate = reverse && confirm ? candidateByte + 1 - literal.Length : candidateByte;
                    if (candidate < start || candidate > end - literal.Length) continue;
                    // The branch charge covers this rejection; a different
                    // first byte needs no bounded full-literal comparison.
                    if (!folded && input[candidate] != literal[0]) continue;
                    breach = TryCompareLiteral(input.Slice(candidate, literal.Length), literal, folded,
                        ref invocation, out var equal);
                    if (breach != Utf8ExecutionLimitKind.None) return breach;
                    if (!equal) continue;
                    var accepted = true;
                    if (confirm && (leadingBoundary != Utf8BoundaryRequirement.None ||
                        trailingBoundary != Utf8BoundaryRequirement.None || trailingLiteral is { Length: > 0 }))
                        breach = TryConfirmLiteral(leadingBoundary, trailingBoundary, trailingLiteral,
                            input, candidate, literal.Length,
                            ref invocation, out accepted);
                    if (breach != Utf8ExecutionLimitKind.None) return breach;
                    if (accepted)
                    {
                        breach = invocation.TryChargeInputStep();
                        if (breach != Utf8ExecutionLimitKind.None) return breach;
                        invocation.Budget.ThrowIfCancellationRequested();
                        match = new PreparedSearchMatch(candidate, literal.Length, id);
                        found = true;
                        if (confirm) Utf8SearchDiagnosticsSession.Current?.CountSearchCandidate();
                        return Utf8ExecutionLimitKind.None;
                    }
                }
                if (reverse) high = candidateByte - 1;
                else low = candidateByte + 1;
            }
        }
        breach = invocation.TryChargeInputStep();
        if (breach != Utf8ExecutionLimitKind.None) return breach;
        invocation.Budget.ThrowIfCancellationRequested();
        return Utf8ExecutionLimitKind.None;
    }

    private static int IndexOfShortExactPair(ReadOnlySpan<byte> input, int starts,
        ReadOnlySpan<byte> first, ReadOnlySpan<byte> second, out int literalId)
    {
        literalId = -1;
        // Dense early matches keep their short comparisons. Long scans share
        // input loads while retaining each literal's own correlated anchors.
        if (input.StartsWith(first)) { literalId = 0; return 0; }
        if (input.StartsWith(second)) { literalId = 1; return 0; }
        var anchor = Math.Min(first.Length, second.Length) - 1;
        var head0 = Vector512.Create(first[0]);
        var head1 = Vector512.Create(second[0]);
        var tail0 = Vector512.Create(first[anchor]);
        var tail1 = Vector512.Create(second[anchor]);
        var limit = Math.Min(starts, input.Length - anchor) - Vector512<byte>.Count;
        var position = 0;
        while (position <= limit)
        {
            var heads = MemoryMarshal.Read<Vector512<byte>>(input[position..]);
            var tails = MemoryMarshal.Read<Vector512<byte>>(input[(position + anchor)..]);
            var candidates = AsciiSearch.CorrelateExactPairs(heads, tails, head0, tail0, head1, tail1)
                .ExtractMostSignificantBits();
            while (candidates != 0)
            {
                var lane = BitOperations.TrailingZeroCount(candidates);
                var candidate = position + lane;
                // Both needles are <=16 bytes; verification stays inside this
                // already admitted primitive, with no false-candidate replay.
                if (first.Length <= input.Length - candidate &&
                    input.Slice(candidate, first.Length).SequenceEqual(first))
                { literalId = 0; return candidate; }
                if (second.Length <= input.Length - candidate &&
                    input.Slice(candidate, second.Length).SequenceEqual(second))
                { literalId = 1; return candidate; }
                candidates &= candidates - 1;
            }
            position += Vector512<byte>.Count;
        }
        var remaining = input[position..];
        var a = remaining.IndexOf(first);
        var b = remaining.IndexOf(second);
        var found = a < 0 ? b : b < 0 ? a : Math.Min(a, b);
        if (found < 0 || found >= starts - position) return -1;
        literalId = a >= 0 && a == found ? 0 : 1;
        return position + found;
    }
}
