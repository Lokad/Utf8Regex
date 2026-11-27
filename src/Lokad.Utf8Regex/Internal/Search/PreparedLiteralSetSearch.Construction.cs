namespace Lokad.Utf8Regex.Internal.Search;

internal readonly partial struct PreparedLiteralSetSearch
{
    private PreparedLiteralSetSearch(AsciiExactLiteralSearchData data, PreparedLiteralSetUniqueAnchor? anchor,
        PreparedMultiLiteralTrieSearch trie, PreparedLiteralSetStrategy strategy)
    { SearchData = data; UniqueAnchor = anchor; MultiLiteral = trie; Strategy = strategy; }

    // A supplied trie was built from these literals in their original order.
    public PreparedLiteralSetSearch(byte[][] literals, in PreparedMultiLiteralTrieSearch preparedTrie = default)
    {
        if (literals.Length == 0)
        { this = new(AsciiSearch.CreateExactLiteralSearchData(literals), null, default, PreparedLiteralSetStrategy.Empty); return; }
        using var scope = Utf8ConstructionScope.Enter();
        var budget = scope.Budget;
        budget.EnterDepth();
        try
        {
            this = Prepare(AsciiSearch.CreateExactLiteralSearchData(literals), literals, preparedTrie, budget);
            scope.Complete();
        }
        finally { budget.LeaveDepth(); }
    }

    private static PreparedLiteralSetSearch Prepare(AsciiExactLiteralSearchData data, byte[][] literals,
        in PreparedMultiLiteralTrieSearch preparedTrie, Utf8ConstructionBudget budget)
    {
        if (data.Buckets.Length == 1)
        {
            if (data.Buckets[0].CommonPrefix.Length > 1)
                return new(data, null, default, PreparedLiteralSetStrategy.SingleBucketPrefix);
            // Preserve first-byte selection for a varying second-byte family.
            // Its membership filter remains live, but the unused search tier is gone.
            if (data.Buckets[0].SecondByteCandidates is not null)
                return new(data, null, default, PreparedLiteralSetStrategy.Bucketed);
        }
        var count = 0;
        var singles = data.Buckets.Length > 0;
        foreach (var bucket in data.Buckets)
        {
            budget.ChargeWork(1);
            count = checked(count + bucket.Literals.Length);
            singles &= bucket.Literals.Length == 1;
        }
        if (singles) return new(data, null, default, PreparedLiteralSetStrategy.SingleLiteralBuckets);
        var anchor = CreateUniqueAnchor(data, count, budget);
        if (anchor.HasValue) return new(data, anchor, default, PreparedLiteralSetStrategy.UniqueAnchorByte);
        if (count < MultiLiteralThreshold || data.ShortestLength == 0)
            return new(data, null, default, PreparedLiteralSetStrategy.Bucketed);
        var trie = preparedTrie.StateCount > 0 ? preparedTrie : PreparedMultiLiteralTrieSearch.Create(literals);
        return new(data, null, trie, PreparedLiteralSetStrategy.MultiLiteralTrie);
    }

    private static PreparedLiteralSetUniqueAnchor? CreateUniqueAnchor(AsciiExactLiteralSearchData data, int count,
        Utf8ConstructionBudget budget)
    {
        if (data.ShortestLength <= 1 || count <= 1 || count > 256) return null;
        Span<byte> anchors = stackalloc byte[256];
        Span<bool> seen = stackalloc bool[256];
        for (var offset = 1; offset < data.ShortestLength; offset++)
        {
            budget.ChargeWork(257);
            seen.Clear();
            var used = 0;
            var duplicate = false;
            foreach (var bucket in data.Buckets)
            {
                budget.ChargeWork(1);
                foreach (var literal in bucket.Literals)
                {
                    budget.ChargeWork(1);
                    var value = literal[offset];
                    if (seen[value]) { duplicate = true; break; }
                    seen[value] = true;
                    anchors[used++] = value;
                }
                if (duplicate) break;
            }
            if (duplicate) continue;
            var map = budget.AllocateArray<ushort>(256, false);
            for (var bucketIndex = 0; bucketIndex < data.Buckets.Length; bucketIndex++)
            {
                budget.ChargeWork(1);
                var literals = data.Buckets[bucketIndex].Literals;
                for (var index = 0; index < literals.Length; index++)
                {
                    budget.ChargeWork(1);
                    map[literals[index][offset]] = (ushort)((bucketIndex << 8) | index);
                }
            }
            return new PreparedLiteralSetUniqueAnchor(offset, PreparedByteSearch.Create(anchors[..used]), map, data.ShortestLength);
        }
        return null;
    }
}
