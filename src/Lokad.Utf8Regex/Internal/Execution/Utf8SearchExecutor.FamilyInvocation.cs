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
        // Borrow the immutable family tables. An absent exact set has empty
        // search data and cannot contribute a first-byte filter.
        ref readonly var exactSearch = ref Nullable.GetValueRefOrDefaultRef(in plan.AlternateLiteralSearch);
        // A small exact family can discover complete needles in bounded
        // windows. Only short distinct heads can carry that proof through
        // boundary rejection without hiding another branch at the same start.
        if (!folded && !reverse && !plan.HasTrailingLiteralRequirement &&
            plan.AlternateLiteralsUtf8 is { Length: >= 3 and <= 8 } literals &&
            (!plan.HasBoundaryRequirements || literals.Length <= 4))
            return TryFindSmallExactFamilyCandidate(literals, input, start, end,
                ref invocation, out match, out found, in exactSearch.SearchData,
                plan.LeadingBoundary, plan.TrailingBoundary);
        PreparedByteSearch? firstByteSearch = null;
        if (folded && plan.AlternateIgnoreCaseLiteralSearch is
            { ShortestLength: > 0 and < int.MaxValue } foldedSearch)
            firstByteSearch = foldedSearch.FirstByteSearch;
        else if (!folded && exactSearch.SearchData.ShortestLength is > 0 and < int.MaxValue)
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
        ref readonly var search = ref Nullable.GetValueRefOrDefaultRef(in plan.AlternateLiteralSearch);
        // Preserve the existing pair and small complete-needle searches.
        if (literals.Length <= 8)
        {
            var breach = TryFindLiteralFamilyCandidate(literals, false, input, start, end, false,
                ref invocation, out var smallMatch, out var smallFound, in search.SearchData);
            if (breach == Utf8ExecutionLimitKind.None && smallFound) candidate = smallMatch.Index;
            return breach;
        }
        // A short remaining subject bounds the prepared trie's whole walk.
        // Reuse its shared prefixes instead of confirming each expanded needle
        // after every first-byte hit. Only a necessary start escapes to the VM.
        if ((uint)(end - start) <= 256 && search.Strategy == PreparedLiteralSetStrategy.MultiLiteralTrie)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(start);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(end, input.Length);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(start, end);
            var breach = invocation.TryChargeInputStep();
            if (breach != Utf8ExecutionLimitKind.None) return breach;
            var relative = search.IndexOf(input[start..end]);
            breach = invocation.TryChargeInputStep();
            invocation.Budget.ThrowIfCancellationRequested();
            if (breach == Utf8ExecutionLimitKind.None && relative >= 0) candidate = start + relative;
            return breach;
        }
        var buckets = search.SearchData.Buckets;
        if (buckets is { Length: 1 } && buckets[0].PrefixDiscriminator is { HasValue: true } discriminator)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(start);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(end, input.Length);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(start, end);
            // The prepared discriminator proves this prefix is shared by every
            // branch. Search it once instead of replaying a full confirmation
            // after each first-byte hit. Keep a bounded complete-prefix window;
            // long prefixes and selected tails retain chunked verification.
            var prefixLength = Math.Min(discriminator.Offset, 1024);
            var bucketLiterals = buckets[0].Literals;
            var prefix = bucketLiterals[0].AsSpan(0, prefixLength);
            var verified = prefixLength == discriminator.Offset ? prefixLength + 1 : prefixLength;
            for (var low = start; low < end;)
            {
                var breach = invocation.TryChargeInputStep();
                if (breach != Utf8ExecutionLimitKind.None) return breach;
                var starts = Math.Min(2049 - prefixLength, end - low);
                var block = input.Slice(low, Math.Min(starts + prefixLength - 1, end - low));
                var relative = block.IndexOf(prefix);
                if (relative < 0) { low += starts; continue; }
                var at = low + relative;
                // The prepared column is unique across all branches. A selected
                // byte plus the searched prefix proves no remaining tail.
                if ((uint)discriminator.Offset < (uint)(end - at) &&
                    discriminator.FindLiteral(input[at + discriminator.Offset], bucketLiterals) is { } literal &&
                    literal.Length <= end - at)
                {
                    breach = TryCompareLiteral(input.Slice(at + verified, literal.Length - verified),
                        literal.AsSpan(verified), false,
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
            plan.AlternateLiteralSearch.HasValue ? search.SearchData.FirstByteSearch : null,
            Utf8BoundaryRequirement.None, Utf8BoundaryRequirement.None, null,
            input, start, end, false, ref invocation, out var match, out var found);
        if (result == Utf8ExecutionLimitKind.None && found) candidate = match.Index;
        return result;
    }

    internal static Utf8ExecutionLimitKind TryFindLiteralFamilyCandidate(
        byte[][] literals, bool folded, ReadOnlySpan<byte> input, int start, int end,
        bool reverse, ref Utf8SliceInvocation invocation, out PreparedSearchMatch match, out bool found,
        in AsciiExactLiteralSearchData prepared = default)
        // The caller verifies the full regex. No boundary or suffix checks
        // apply during discovery.
        => !folded && !reverse && literals.Length is >= 3 and <= 8
            ? TryFindSmallExactFamilyCandidate(literals, input, start, end, ref invocation, out match, out found, prepared)
            : TryFindLiteralFamilyCore(literals, folded, false, null,
            Utf8BoundaryRequirement.None, Utf8BoundaryRequirement.None, null,
            input, start, end, reverse, ref invocation, out match, out found);

    // Keep the qualified pair/confirmed loops intact. Small raw exact families
    // can search complete needles in bounded spans without prepared storage or
    // per-byte false-candidate replay. The caller still verifies the full regex.
    private static Utf8ExecutionLimitKind TryFindSmallExactFamilyCandidate(byte[][] literals,
        ReadOnlySpan<byte> input, int start, int end, ref Utf8SliceInvocation invocation,
        out PreparedSearchMatch match, out bool found, in AsciiExactLiteralSearchData prepared = default,
        Utf8BoundaryRequirement leadingBoundary = Utf8BoundaryRequirement.None,
        Utf8BoundaryRequirement trailingBoundary = Utf8BoundaryRequirement.None)
    {
        match = default; found = false;
        ArgumentOutOfRangeException.ThrowIfNegative(start);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(end, input.Length);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(start, end);
        var confirmBoundaries = leadingBoundary != Utf8BoundaryRequirement.None ||
            trailingBoundary != Utf8BoundaryRequirement.None;
        var longest = prepared.LongestLength;
        if (longest is > 0 and <= 1024)
        {
            // The immutable length bound is prepared once. Charge a single
            // entry check before reading the subject window.
            if (!confirmBoundaries)
            {
                var eligibility = invocation.TryChargeInputStep();
                if (eligibility != Utf8ExecutionLimitKind.None) return eligibility;
            }
        }
        else
        {
            longest = 0;
            foreach (var literal in literals)
            {
                if (!confirmBoundaries)
                {
                    var eligibility = invocation.TryChargeInputStep();
                    if (eligibility != Utf8ExecutionLimitKind.None) return eligibility;
                }
                if (literal.Length is 0 or > 1024)
                {
                    longest = 0;
                    break;
                }
                longest = Math.Max(longest, literal.Length);
            }
        }
        // One bucket per original needle proves distinct heads. For raw
        // candidates, one shared prefix can screen all needles together.
        var shortPrepared = prepared.ShortestLength >= 3 && longest is > 0 and <= 16;
        var distinctHeads = shortPrepared && prepared.Buckets is { Length: 3 or 4 } buckets &&
            buckets.Length == literals.Length;
        if (distinctHeads && !confirmBoundaries && invocation.IsUnmetered && invocation.CheckpointHook is null)
        {
            // The prepared scan visits each start once. A unique head also
            // identifies the original ordinal because bucket order is insertion order.
            found = PreparedLiteralSetSearch.TryFindFirstMatchSingleLiteralBuckets(input[start..end], prepared,
                out var relative, out var length);
            if (found)
            {
                var index = start + relative;
                match = new PreparedSearchMatch(index, length, prepared.BucketIndexMap[input[index]]);
            }
            return Utf8ExecutionLimitKind.None;
        }
        var sharedPrefix = shortPrepared && !confirmBoundaries &&
            prepared.Buckets is [{ CommonPrefix.Length: >= 3, Literals.Length: var size }] && size == literals.Length;
        var sharedEndpoints = !confirmBoundaries && shortPrepared && Vector512.IsHardwareAccelerated &&
            end - start >= 2048 && prepared.ShortestLength == longest &&
            literals.Length == 4 && prepared.Buckets is [{ Literals.Length: 4 }] &&
            literals[0][^1] == literals[1][^1] && literals[0][^1] == literals[2][^1] &&
            literals[0][^1] == literals[3][^1];
        if (longest == 0 || confirmBoundaries && !distinctHeads)
        {
            PreparedByteSearch? firstBytes = confirmBoundaries && prepared.ShortestLength is > 0 and < int.MaxValue
                ? prepared.FirstByteSearch : null;
            return TryFindLiteralFamilyCore(literals, false, confirmBoundaries, firstBytes,
                leadingBoundary, trailingBoundary, null, input, start, end, false, ref invocation, out match, out found);
        }
        for (var low = start; low < end;)
        {
            // Complete-needle lookahead fits in 2048 bytes. Later starts belong
            // to the next window; a full window advances at least 1025 starts.
            var starts = Math.Min(2049 - longest, end - low);
            var block = input.Slice(low, Math.Min(starts + longest - 1, end - low));
            var best = -1; var ordinal = -1;
            var firstUnsearched = 0;
            if (distinctHeads || sharedPrefix || sharedEndpoints)
            {
                // The same checked search used by ordinary execution runs
                // wholly inside this admitted window. Charge the combined
                // primitive once; original authority also gates publication.
                var breach = invocation.TryChargeInputStep();
                if (breach != Utf8ExecutionLimitKind.None) return breach;
                if (sharedEndpoints)
                {
                    best = IndexOfShortFamilyWithSharedEndpoints(block, starts, literals, longest,
                        out ordinal, out firstUnsearched);
                }
                else
                {
                    firstUnsearched = starts;
                    // Dense false prefixes hand the remaining starts to the
                    // complete-needle search, within the original block bounds.
                    var foundInBlock = sharedPrefix
                        ? PreparedLiteralSetSearch.TryFindFirstMatchSingleBucketPrefix(block, prepared,
                            out var relative, out _, out firstUnsearched, maxRejected: 4)
                        : PreparedLiteralSetSearch.TryFindFirstMatchSingleLiteralBuckets(block, prepared, out relative, out _);
                    if (foundInBlock && relative < starts)
                    {
                        if (distinctHeads)
                        {
                            best = relative;
                            ordinal = prepared.BucketIndexMap[block[relative]];
                        }
                        else
                        {
                            // Shared prefixes retain pattern order at the same start.
                            for (var id = 0; id < literals.Length; id++)
                                if (block[relative..].StartsWith(literals[id]))
                                { best = relative; ordinal = id; break; }
                        }
                    }
                }
            }
            if (firstUnsearched < starts) for (var id = 0; id < literals.Length; id++)
            {
                var breach = invocation.TryChargeInputStep();
                if (breach != Utf8ExecutionLimitKind.None) return breach;
                var literal = literals[id];
                // A later needle must start strictly before the current
                // winner. Keep its full lookahead at that last valid start.
                var endOfSearch = best < 0 ? block.Length : Math.Min(block.Length, best + literal.Length - 1);
                var relative = block[firstUnsearched..endOfSearch].IndexOf(literal);
                if (relative < 0) continue;
                relative += firstUnsearched;
                if (relative < starts)
                { best = relative; ordinal = id; }
                if (best == firstUnsearched) break;
            }
            if (best >= 0)
            {
                var index = low + best;
                var length = literals[ordinal].Length;
                if (confirmBoundaries)
                {
                    var confirmation = TryConfirmLiteral(leadingBoundary, trailingBoundary, null,
                        input, index, length, ref invocation, out var accepted);
                    if (confirmation != Utf8ExecutionLimitKind.None) return confirmation;
                    if (!accepted)
                    {
                        // Check the boundaries before comparing more literals.
                        // Other branches may still start inside the rejected match.
                        return TryFindLiteralFamilyCore(literals, false, true, prepared.FirstByteSearch,
                            leadingBoundary, trailingBoundary, null, input, index + 1, end, false,
                            ref invocation, out match, out found);
                    }
                }
                var publication = invocation.TryChargeInputStep();
                invocation.Budget.ThrowIfCancellationRequested();
                if (publication != Utf8ExecutionLimitKind.None) return publication;
                match = new PreparedSearchMatch(index, length, ordinal);
                found = true;
                if (confirmBoundaries) Utf8SearchDiagnosticsSession.Current?.CountSearchCandidate();
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
        // The first window and the final outcome each check the deadline.
        var breach = invocation.TryChargeBoundedCheck();
        if (breach != Utf8ExecutionLimitKind.None) return breach;
        var candidateBoundary = confirm && !reverse ? leadingBoundary : Utf8BoundaryRequirement.None;
        var matchBoundary = reverse ? leadingBoundary : Utf8BoundaryRequirement.None;
        var confirmAfterComparison = confirm && (matchBoundary != Utf8BoundaryRequirement.None ||
            trailingBoundary != Utf8BoundaryRequirement.None || trailingLiteral is { Length: > 0 });
        var boundedExactHeads = !folded && !reverse && literals.Length <= 8;
        var low = start;
        var high = end - 1;
        if (Vector512.IsHardwareAccelerated && !confirm && !reverse && !folded && firstByteSearch is null &&
            literals.Length == 2 && literals[0].Length is >= 3 and <= 16 && literals[1].Length is >= 3 and <= 16)
        {
            // The pair scan visits starts in order and verifies both complete
            // needles. Admit each bounded window when the invocation needs
            // checkpoints. Otherwise one scan can cover the remaining span.
            var wholeSpan = invocation.IsUnmetered && invocation.CheckpointHook is null;
            var lookahead = Math.Max(literals[0].Length, literals[1].Length) - 1;
            while (end - low >= Vector512<byte>.Count)
            {
                var count = wholeSpan ? end - low : Math.Min(2048 - lookahead, end - low);
                var length = wholeSpan ? count : Math.Min(count + lookahead, end - low);
                breach = invocation.TryChargeInputStep();
                if (breach != Utf8ExecutionLimitKind.None) return breach;
                var relative = IndexOfShortExactPair(input.Slice(low, length), count, literals[0], literals[1], out var id);
                if (relative >= 0)
                {
                    breach = invocation.TryChargeInputStep();
                    invocation.Budget.ThrowIfCancellationRequested();
                    if (breach != Utf8ExecutionLimitKind.None) return breach;
                    match = new PreparedSearchMatch(low + relative, literals[id].Length, id);
                    found = true;
                    return Utf8ExecutionLimitKind.None;
                }
                low += count;
            }
        }
        if (literals.Length > 0)
        {
            var admittedEnd = low;
            while (low <= high)
            {
                // Search only one candidate window for every branch. Searching
                // an absent branch to the subject end on every MoveNext would
                // make a dense later branch quadratic over the enumeration.
                var reusesWindow = low < admittedEnd;
                var count = reusesWindow ? admittedEnd - low : Math.Min(2048, high - low + 1);
                var blockStart = reverse ? high - count + 1 : low;
                var block = input.Slice(blockStart, count);
                var candidateByte = -1;
                if (!reverse && firstByteSearch is { } search)
                {
                    // False candidates resume within the same admitted window.
                    // Each seek still charges work and checks cancellation.
                    breach = reusesWindow ? invocation.TryChargeBoundedCheck() : invocation.TryChargeInputStep();
                    if (breach != Utf8ExecutionLimitKind.None) return breach;
                    admittedEnd = low + count;
                    // Borrow the prepared union instead of rescanning this
                    // bounded window once per branch at every false candidate.
                    while (true)
                    {
                        var relative = search.IndexOf(block);
                        if (relative < 0) break;
                        var at = admittedEnd - block.Length + relative;
                        if (candidateBoundary != Utf8BoundaryRequirement.None)
                        {
                            breach = invocation.TryChargeBoundedCheck();
                            if (breach != Utf8ExecutionLimitKind.None) return breach;
                            if (!Utf8ScalarWordBoundary.MatchesRequirement(candidateBoundary, input, at))
                            {
                                // Resume within the same window with the usual
                                // work and interruption check for each seek.
                                block = block[(relative + 1)..];
                                if (block.IsEmpty) break;
                                breach = invocation.TryChargeBoundedCheck();
                                if (breach != Utf8ExecutionLimitKind.None) return breach;
                                continue;
                            }
                        }
                        candidateByte = at;
                        break;
                    }
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
                        var relative = pairBlock.IndexOf(literals[0]);
                        if (relative >= count) relative = -1;
                        if (relative != 0)
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
                        if (relative >= 0) candidateByte = blockStart + relative;
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

                if (candidateBoundary != Utf8BoundaryRequirement.None && firstByteSearch is null)
                {
                    // Forward candidates are UTF-8 lead bytes. Every branch at
                    // this start has the same leading boundary requirement.
                    breach = invocation.TryChargeBoundedCheck();
                    if (breach != Utf8ExecutionLimitKind.None) return breach;
                    if (!Utf8ScalarWordBoundary.MatchesRequirement(candidateBoundary, input, candidateByte))
                    {
                        low = candidateByte + 1;
                        continue;
                    }
                }

                var head = boundedExactHeads ? input[candidateByte] : -1;
                for (var id = 0; id < literals.Length; id++)
                {
                    var literal = literals[id];
                    // The charged candidate search also covers up to eight
                    // exact head checks. Matching heads keep branch admission.
                    if (boundedExactHeads && head != literal[0]) continue;
                    breach = invocation.TryChargeBoundedCheck();
                    if (breach != Utf8ExecutionLimitKind.None) return breach;
                    var candidate = reverse && confirm ? candidateByte + 1 - literal.Length : candidateByte;
                    if (candidate < start || candidate > end - literal.Length) continue;
                    if (!boundedExactHeads && !folded && input[candidate] != literal[0]) continue;
                    var candidateBytes = input.Slice(candidate, literal.Length);
                    if (boundedExactHeads && literal.Length <= 32)
                    {
                        // One work step covers this bounded probe. The next
                        // window or outcome checks the clock directly.
                        breach = invocation.TryChargeBoundedCheck();
                        if (breach != Utf8ExecutionLimitKind.None) return breach;
                        if (!candidateBytes.SequenceEqual(literal)) continue;
                    }
                    else
                    {
                        breach = TryCompareLiteral(candidateBytes, literal, folded, ref invocation, out var equal);
                        if (breach != Utf8ExecutionLimitKind.None) return breach;
                        if (!equal) continue;
                    }
                    var accepted = true;
                    if (confirmAfterComparison)
                        breach = TryConfirmLiteral(matchBoundary, trailingBoundary, trailingLiteral,
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
        for (; position <= limit; position += Vector512<byte>.Count)
        {
            var heads = MemoryMarshal.Read<Vector512<byte>>(input[position..]);
            var firstHeads = Vector512.Equals(heads, head0);
            var secondHeads = Vector512.Equals(heads, head1);
            if ((firstHeads | secondHeads) == Vector512<byte>.Zero) continue;
            var tails = MemoryMarshal.Read<Vector512<byte>>(input[(position + anchor)..]);
            var candidates = ((firstHeads & Vector512.Equals(tails, tail0)) |
                (secondHeads & Vector512.Equals(tails, tail1))).ExtractMostSignificantBits();
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
        }
        var remaining = input[position..];
        var a = remaining.IndexOf(first);
        var b = remaining.IndexOf(second);
        var found = a < 0 ? b : b < 0 ? a : Math.Min(a, b);
        if (found < 0 || found >= starts - position) return -1;
        literalId = a >= 0 && a == found ? 0 : 1;
        return position + found;
    }

    private static int IndexOfShortFamilyWithSharedEndpoints(ReadOnlySpan<byte> input, int starts,
        byte[][] literals, int width, out int ordinal, out int firstUnsearched)
    {
        ordinal = -1;
        firstUnsearched = starts;
        var head = literals[0][0];
        var tail = literals[0][^1];
        var heads = Vector512.Create(head);
        var tails = Vector512.Create(tail);
        var lastStart = Math.Min(starts, input.Length - width + 1);
        var remainingRejects = 32;
        var position = 0;
        for (; position <= lastStart - Vector512<byte>.Count; position += Vector512<byte>.Count)
        {
            var first = MemoryMarshal.Read<Vector512<byte>>(input[position..]);
            var last = MemoryMarshal.Read<Vector512<byte>>(input[(position + width - 1)..]);
            var candidates = (Vector512.Equals(first, heads) & Vector512.Equals(last, tails)).ExtractMostSignificantBits();
            while (candidates != 0)
            {
                var candidate = position + BitOperations.TrailingZeroCount(candidates);
                ordinal = FindMatchingLiteral(input.Slice(candidate, width), literals);
                if (ordinal >= 0) return candidate;
                // Dense false candidates resume the existing complete-needle search.
                if (--remainingRejects == 0)
                {
                    firstUnsearched = candidate + 1;
                    return -1;
                }
                candidates &= candidates - 1;
            }
        }
        for (; position < lastStart; position++)
        {
            if (input[position] != head || input[position + width - 1] != tail) continue;
            ordinal = FindMatchingLiteral(input.Slice(position, width), literals);
            if (ordinal >= 0) return position;
        }
        return -1;

        static int FindMatchingLiteral(ReadOnlySpan<byte> candidate, byte[][] family)
        {
            for (var id = 0; id < family.Length; id++)
                if (candidate.SequenceEqual(family[id])) return id;
            return -1;
        }
    }
}
