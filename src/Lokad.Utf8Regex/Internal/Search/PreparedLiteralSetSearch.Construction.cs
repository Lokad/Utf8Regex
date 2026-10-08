namespace Lokad.Utf8Regex.Internal.Search;

internal readonly partial struct PreparedLiteralSetSearch
{
    private PreparedLiteralSetSearch(AsciiExactLiteralSearchData data, PreparedLiteralSetUniqueAnchor? anchor,
        PreparedMultiLiteralTrieSearch trie, PreparedLiteralSetStrategy strategy)
    { SearchData = data; UniqueAnchor = anchor; MultiLiteral = trie; Strategy = strategy; }

    public PreparedLiteralSetSearch(byte[][] literals)
    {
        if (literals.Length == 0)
        { this = new(AsciiSearch.CreateExactLiteralSearchData(literals), null, default, PreparedLiteralSetStrategy.Empty); return; }
        using var scope = Utf8ConstructionScope.Enter();
        var budget = scope.Budget;
        budget.EnterDepth();
        try
        {
            this = Prepare(AsciiSearch.CreateExactLiteralSearchData(literals), literals, budget);
            scope.Complete();
        }
        finally { budget.LeaveDepth(); }
    }

    public PreparedLiteralSetSearch(AsciiExactLiteralSearchData data)
    {
        if (data.ShortestLength == int.MaxValue)
        { this = new(data, null, default, PreparedLiteralSetStrategy.Empty); return; }
        using var scope = Utf8ConstructionScope.Enter();
        var budget = scope.Budget;
        budget.EnterDepth();
        try { this = Prepare(data, null, budget); scope.Complete(); }
        finally { budget.LeaveDepth(); }
    }

    private static PreparedLiteralSetSearch Prepare(AsciiExactLiteralSearchData data, byte[][]? original,
        Utf8ConstructionBudget budget)
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
        if (original is { } borrowed)
            return new(data, null, PreparedMultiLiteralTrieSearch.Create(borrowed), PreparedLiteralSetStrategy.MultiLiteralTrie);
        var flattened = budget.AllocateArray<byte[]>(count, false);
        try
        {
            var index = 0;
            foreach (var bucket in data.Buckets)
            {
                budget.ChargeWork(1 + (ulong)bucket.Literals.Length);
                bucket.Literals.CopyTo(flattened, index);
                index += bucket.Literals.Length;
            }
            // The trie retains lengths/order and copied keys, not this reference vector.
            return new(data, null, PreparedMultiLiteralTrieSearch.Create(flattened), PreparedLiteralSetStrategy.MultiLiteralTrie);
        }
        finally { budget.ReleaseArray(flattened, false); }
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
            var map = budget.AllocateArray<byte[]>(256, false);
            foreach (var bucket in data.Buckets)
            {
                budget.ChargeWork(1);
                foreach (var literal in bucket.Literals)
                { budget.ChargeWork(1); map[literal[offset]] = literal; }
            }
            return new PreparedLiteralSetUniqueAnchor(offset, PreparedByteSearch.Create(anchors[..used]), map, data.ShortestLength);
        }
        return null;
    }
}
