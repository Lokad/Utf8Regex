using System.Runtime.Intrinsics.X86;

namespace Lokad.Utf8Regex.Internal.Search;

internal readonly partial struct PreparedAsciiIgnoreCaseLiteralSetSearch
{
    public PreparedAsciiIgnoreCaseLiteralSetSearch(byte[][] literals)
    {
        _data = null;
        if (literals.Length == 0) return;
        using var scope = Utf8ConstructionScope.Enter();
        var budget = scope.Budget;
        budget.EnterDepth();
        try
        {
            var shortest = int.MaxValue;
            foreach (var literal in literals)
            {
                budget.ChargeWork(1);
                shortest = Math.Min(shortest, literal.Length);
                if (shortest == 0) break;
            }
            if (shortest == 0)
            {
                var references = budget.AllocateArray<byte[]>(1, false);
                references[0] = [];
                var buckets = budget.AllocateArray<AsciiIgnoreCaseLiteralBucket>(1, false);
                buckets[0] = new(0, references);
                var map = budget.AllocateArray<int>(256, false);
                budget.ChargeWork(257);
                Array.Fill(map, -1);
                map[0] = 0;
                _data = AcquireData(PreparedByteSearch.FromOwnedValues([], null), buckets, map, 0, default, budget);
                scope.Complete();
                return;
            }

            Span<int> indexByByte = stackalloc int[256];
            Span<int> counts = stackalloc int[256];
            Span<byte> firstBytes = stackalloc byte[256];
            budget.ChargeWork(512);
            indexByByte.Fill(-1);
            counts.Clear();
            var distinct = 0;
            foreach (var literal in literals)
            {
                budget.ChargeWork(1);
                var first = AsciiSearch.FoldCase(literal[0]);
                var group = indexByByte[first];
                if (group < 0)
                {
                    group = distinct++;
                    indexByByte[first] = group;
                    firstBytes[group] = first;
                }
                counts[group]++;
            }
            var foldedBuckets = budget.AllocateArray<AsciiIgnoreCaseLiteralBucket>(distinct, false);
            for (var group = 0; group < distinct; group++)
            {
                budget.ChargeWork(1);
                foldedBuckets[group] = new(firstBytes[group], budget.AllocateArray<byte[]>(counts[group], false));
                counts[group] = 0;
            }
            foreach (var literal in literals)
            {
                budget.ChargeWork(1);
                var normalized = budget.AllocateArray<byte>(literal.Length, false);
                budget.ChargeWork((ulong)literal.Length);
                for (var offset = 0; offset < literal.Length; offset++)
                    normalized[offset] = AsciiSearch.FoldCase(literal[offset]);
                var group = indexByByte[normalized[0]];
                foldedBuckets[group].Literals[counts[group]++] = normalized;
            }
            var foldedMap = budget.AllocateArray<int>(256, false);
            budget.ChargeWork(256);
            indexByByte.CopyTo(foldedMap);

            Span<bool> seen = stackalloc bool[256];
            Span<byte> searchBytes = stackalloc byte[256];
            budget.ChargeWork(256);
            seen.Clear();
            var searchCount = 0;
            for (var group = 0; group < distinct; group++)
            {
                budget.ChargeWork(1);
                var folded = firstBytes[group];
                if (!seen[folded]) { seen[folded] = true; searchBytes[searchCount++] = folded; }
                if ((uint)(folded - (byte)'a') <= 'z' - 'a')
                {
                    var upper = (byte)(folded - 32);
                    if (!seen[upper]) { seen[upper] = true; searchBytes[searchCount++] = upper; }
                }
            }
            var search = PreparedByteSearch.Create(searchBytes[..searchCount]);
            PreparedMultiLiteralPackedNibbleSimdPrefilter prefilter;
            if (literals.Length > 8 || !Ssse3.IsSupported || !Sse2.IsSupported)
            {
                // Declining mask preparation reads only lengths. Borrow the
                // originals instead of acquiring a discarded folded vector.
                prefilter = PreparedMultiLiteralPackedNibbleSimdPrefilter.CreateAsciiIgnoreCase(literals);
            }
            else
            {
                // Mask bits follow the original grouped order. The builder
                // copies masks/offsets/search values; it retains no references.
                var flattened = budget.AllocateArray<byte[]>(literals.Length, false);
                try
                {
                    var position = 0;
                    foreach (var bucket in foldedBuckets)
                    {
                        budget.ChargeWork(1 + (ulong)bucket.Literals.Length);
                        bucket.Literals.CopyTo(flattened, position);
                        position += bucket.Literals.Length;
                    }
                    prefilter = PreparedMultiLiteralPackedNibbleSimdPrefilter.CreateAsciiIgnoreCase(flattened);
                }
                finally { budget.ReleaseArray(flattened, false); }
            }
            _data = AcquireData(search, foldedBuckets, foldedMap, shortest, prefilter, budget);
            scope.Complete();
        }
        finally { budget.LeaveDepth(); }
    }

    private static PreparedAsciiIgnoreCaseLiteralSetSearchData AcquireData(PreparedByteSearch search,
        AsciiIgnoreCaseLiteralBucket[] buckets, int[] map, int shortest,
        PreparedMultiLiteralPackedNibbleSimdPrefilter prefilter, Utf8ConstructionBudget budget)
        => budget.AllocateObject(new PreparedAsciiIgnoreCaseLiteralSetSearchDataState(search, buckets, map, shortest, prefilter),
            static state => new PreparedAsciiIgnoreCaseLiteralSetSearchData(state));
}

// Covers every instance field of the retained descriptor. Payload loans are
// independent; the descriptor and its retaining node share the parent owner.
internal readonly struct PreparedAsciiIgnoreCaseLiteralSetSearchDataState(PreparedByteSearch search,
    AsciiIgnoreCaseLiteralBucket[] buckets, int[] map, int shortest, PreparedMultiLiteralPackedNibbleSimdPrefilter prefilter)
{
    internal readonly PreparedByteSearch Search = search;
    internal readonly AsciiIgnoreCaseLiteralBucket[] Buckets = buckets;
    internal readonly int[] Map = map;
    internal readonly int Shortest = shortest;
    internal readonly PreparedMultiLiteralPackedNibbleSimdPrefilter Prefilter = prefilter;
}
