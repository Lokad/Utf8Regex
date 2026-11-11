using System.Numerics;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;

namespace Lokad.Utf8Regex.Internal.Search;

internal readonly partial struct PreparedLiteralSetSearch
{
    private static bool TryFindShortDistinctHeads(ReadOnlySpan<byte> input,
        AsciiExactLiteralSearchData data, ref int offset, out int matchedLength)
    {
        matchedLength = 0;
        var first = data.Buckets[0].Literals[0];
        var second = data.Buckets[1].Literals[0];
        var third = data.Buckets[2].Literals[0];
        var fourth = data.Buckets.Length == 4 ? data.Buckets[3].Literals[0] : third;
        if (first.Length > 16 || second.Length > 16 || third.Length > 16 || fourth.Length > 16)
            return false;

        // Avoid vector setup/reloading when the next dense match follows a
        // short delimiter. These are byte probes; full comparison proves a hit.
        for (; offset < 8; offset++)
        {
            var bucket = data.BucketIndexMap[input[offset]];
            if (bucket < 0) continue;
            var literal = data.Buckets[bucket].Literals[0];
            if (!LiteralMatchesAt(input, offset, literal)) continue;
            matchedLength = literal.Length;
            return true;
        }

        var anchor = data.ShortestLength - 1;
        var head0 = Vector512.Create(first[0]);
        var tail0 = Vector512.Create(first[anchor]);
        var head1 = Vector512.Create(second[0]);
        var tail1 = Vector512.Create(second[anchor]);
        var head2 = Vector512.Create(third[0]);
        var tail2 = Vector512.Create(third[anchor]);
        var head3 = Vector512.Create(fourth[0]);
        var tail3 = Vector512.Create(fourth[anchor]);
        var limit = input.Length - anchor - Vector512<byte>.Count;
        while (offset <= limit)
        {
            var heads = MemoryMarshal.Read<Vector512<byte>>(input[offset..]);
            var tails = MemoryMarshal.Read<Vector512<byte>>(input[(offset + anchor)..]);
            var candidates = (AsciiSearch.CorrelateExactPairs(heads, tails, head0, tail0, head1, tail1) |
                AsciiSearch.CorrelateExactPairs(heads, tails, head2, tail2, head3, tail3)).ExtractMostSignificantBits();
            while (candidates != 0)
            {
                var candidate = offset + BitOperations.TrailingZeroCount(candidates);
                var bucket = data.BucketIndexMap[input[candidate]];
                if (bucket >= 0)
                {
                    var literal = data.Buckets[bucket].Literals[0];
                    if (LiteralMatchesAt(input, candidate, literal))
                    {
                        offset = candidate;
                        matchedLength = literal.Length;
                        return true;
                    }
                }
                candidates &= candidates - 1;
            }
            offset += Vector512<byte>.Count;
        }
        // The canonical scalar loop resumes at this first unprocessed start.
        return false;
    }
}
