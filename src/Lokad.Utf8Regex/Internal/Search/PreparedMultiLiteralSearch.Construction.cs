namespace Lokad.Utf8Regex.Internal.Search;

internal readonly partial struct PreparedMultiLiteralSearch
{
    private PreparedMultiLiteralSearch(byte[][] literals, PreparedMultiLiteralKind kind,
        PreparedLiteralSetSearch exact, PreparedMultiLiteralTrieSearch trie,
        PreparedMultiLiteralAutomatonSearch automaton, PreparedMultiLiteralEarliestExactSearch earliest,
        PreparedMultiLiteralPackedSearch packed, PreparedAsciiIgnoreCaseLiteralSetSearch ignoreCase)
    {
        Literals = literals; Kind = kind; ExactSearch = exact; TrieSearch = trie;
        AutomatonSearch = automaton; EarliestSearch = earliest; PackedSearch = packed; IgnoreCaseSearch = ignoreCase;
    }

    public PreparedMultiLiteralSearch(byte[][] literals, bool ignoreCase)
    {
        if (literals.Length == 0)
        {
            this = ignoreCase ? new(literals, PreparedMultiLiteralKind.AsciiIgnoreCase, default, default, default, default, default, new(literals))
                : new(literals, PreparedMultiLiteralKind.ExactDirect, new(literals), default, default, default, default, default);
            return;
        }
        using var scope = Utf8ConstructionScope.Enter();
        var budget = scope.Budget;
        budget.EnterDepth();
        try
        {
            this = ignoreCase ? new(literals, PreparedMultiLiteralKind.AsciiIgnoreCase, default, default, default, default, default, new(literals))
                : PrepareExact(literals, null, budget);
            scope.Complete();
        }
        finally { budget.LeaveDepth(); }
    }

    public PreparedMultiLiteralSearch(PreparedLiteralSetSearch search)
    {
        if (search.SearchData.Buckets.Length == 0)
        { this = new([], PreparedMultiLiteralKind.ExactDirect, search, default, default, default, default, default); return; }
        using var scope = Utf8ConstructionScope.Enter();
        var budget = scope.Budget;
        budget.EnterDepth();
        try
        {
            var count = 0;
            foreach (var bucket in search.SearchData.Buckets)
            { budget.ChargeWork(1); count = checked(count + bucket.Literals.Length); }
            var literals = budget.AllocateArray<byte[]>(count, false);
            var next = 0;
            foreach (var bucket in search.SearchData.Buckets)
            {
                budget.ChargeWork(1 + (ulong)bucket.Literals.Length);
                bucket.Literals.CopyTo(literals, next); next += bucket.Literals.Length;
            }
            this = PrepareExact(literals, search, budget);
            scope.Complete();
        }
        finally { budget.LeaveDepth(); }
    }

    public PreparedMultiLiteralSearch(PreparedAsciiIgnoreCaseLiteralSetSearch search)
    {
        if (search.Buckets.Length == 0)
        { this = new([], PreparedMultiLiteralKind.AsciiIgnoreCase, default, default, default, default, default, search); return; }
        using var scope = Utf8ConstructionScope.Enter();
        var budget = scope.Budget;
        budget.EnterDepth();
        try
        {
            var count = 0;
            foreach (var bucket in search.Buckets)
            { budget.ChargeWork(1); count = checked(count + bucket.Literals.Length); }
            var literals = budget.AllocateArray<byte[]>(count, false);
            var next = 0;
            foreach (var bucket in search.Buckets)
            {
                budget.ChargeWork(1 + (ulong)bucket.Literals.Length);
                bucket.Literals.CopyTo(literals, next); next += bucket.Literals.Length;
            }
            this = new(literals, PreparedMultiLiteralKind.AsciiIgnoreCase, default, default, default, default, default, search);
            scope.Complete();
        }
        finally { budget.LeaveDepth(); }
    }

    private static PreparedMultiLiteralSearch PrepareExact(byte[][] literals, PreparedLiteralSetSearch? borrowed,
        Utf8ConstructionBudget budget)
    {
        var shortest = borrowed?.SearchData.ShortestLength ?? PreparedMultiLiteralConstruction.GetShortestLength(literals, budget);
        // Exact data canonically collapses any empty alternative to one empty
        // bucket. Keep that direct route even when the original vector is large.
        if (shortest == 0)
            return new(literals, PreparedMultiLiteralKind.ExactDirect, borrowed ?? new PreparedLiteralSetSearch(literals), default, default, default, default, default);
        // Select from borrowed literals/facts before acquiring exact buckets, anchors
        // or a trie. Those structures have no consumer in packed/earliest routes.
        if (literals.Length is >= 3 and < TrieThreshold && shortest >= 4 && ContainsNonAscii(literals, budget)
            && PreparedMultiLiteralPackedSearch.TryCreate(literals, out var packed))
            return new(literals, PreparedMultiLiteralKind.ExactPacked, default, default, default, default, packed, default);

        if (literals.Length <= 3 && shortest >= 4)
        {
            ulong totalBytes = 0;
            foreach (var literal in literals) { budget.ChargeWork(1); totalBytes += (ulong)literal.Length; }
            if (totalBytes >= 24)
                return new(literals, PreparedMultiLiteralKind.ExactEarliest, default, default, default, new(literals), default, default);
        }

        if (literals.Length >= AutomatonThreshold)
        {
            var prefix = borrowed.HasValue ? borrowed.Value.Strategy == PreparedLiteralSetStrategy.SingleBucketPrefix
                : HasSingleBucketPrefix(literals, shortest, budget);
            if (!prefix)
            {
                // Both helpers are live: trie verifies candidate lengths/identities,
                // and the portfolio's large-family Count uses earliest/hybrid lanes.
                // A supplied search's trie may number branches in original order;
                // this wrapper's flattened vector uses bucket order instead.
                var trie = PreparedMultiLiteralTrieSearch.Create(literals);
                return new(literals, PreparedMultiLiteralKind.ExactAutomaton, default, trie,
                    PreparedMultiLiteralAutomatonSearch.Create(literals), new(literals), default, default);
            }
            if (literals.Length >= TrieThreshold)
                return new(literals, PreparedMultiLiteralKind.ExactTrie, default, PreparedMultiLiteralTrieSearch.Create(literals), default, default, default, default);
        }
        else if (literals.Length >= 3 && shortest >= 4 &&
            (borrowed.HasValue ? borrowed.Value.Strategy == PreparedLiteralSetStrategy.UniqueAnchorByte
                : HasSmallUniqueAnchor(literals, shortest, budget)))
            return new(literals, PreparedMultiLiteralKind.ExactTrie, default, PreparedMultiLiteralTrieSearch.Create(literals), default, default, default, default);

        return new(literals, PreparedMultiLiteralKind.ExactDirect, borrowed ?? new PreparedLiteralSetSearch(literals), default, default, default, default, default);
    }

    private static bool ContainsNonAscii(byte[][] literals, Utf8ConstructionBudget budget)
    {
        foreach (var literal in literals)
        {
            budget.ChargeWork(1);
            foreach (var value in literal) { budget.ChargeWork(1); if (value >= 0x80) return true; }
        }
        return false;
    }

    private static bool HasSingleBucketPrefix(byte[][] literals, int shortest, Utf8ConstructionBudget budget)
    {
        if (shortest < 2) return false;
        var first = literals[0];
        foreach (var literal in literals)
        {
            budget.ChargeWork(2);
            if (literal[0] != first[0] || literal[1] != first[1]) return false;
        }
        return true;
    }

    private static bool HasSmallUniqueAnchor(byte[][] literals, int shortest, Utf8ConstructionBudget budget)
    {
        Span<bool> seen = stackalloc bool[256];
        budget.ChargeWork(256); seen.Clear();
        var distinctFirst = 0;
        foreach (var literal in literals)
        {
            budget.ChargeWork(1);
            if (seen[literal[0]]) continue;
            seen[literal[0]] = true; distinctFirst++;
        }
        // Single-byte buckets need no anchor. A sole bucket chooses its prefix
        // or varying-second-byte route before considering a unique anchor.
        if (distinctFirst == 1 || distinctFirst == literals.Length) return false;
        for (var offset = 1; offset < shortest; offset++)
        {
            budget.ChargeWork(256); seen.Clear();
            var unique = true;
            foreach (var literal in literals)
            {
                budget.ChargeWork(1);
                if (seen[literal[offset]]) { unique = false; break; }
                seen[literal[offset]] = true;
            }
            if (unique) return true;
        }
        return false;
    }
}

internal readonly partial struct PreparedMultiLiteralEarliestExactSearch
{
    public PreparedMultiLiteralEarliestExactSearch(byte[][] literals)
    {
        Literals = literals; ShortestLength = int.MaxValue; LongestLength = 0;
        if (literals.Length == 0) return;
        using var scope = Utf8ConstructionScope.Enter();
        var budget = scope.Budget;
        budget.EnterDepth();
        try
        {
            foreach (var literal in literals)
            {
                budget.ChargeWork(1);
                ShortestLength = Math.Min(ShortestLength, literal.Length);
                LongestLength = Math.Max(LongestLength, literal.Length);
            }
            scope.Complete();
        }
        finally { budget.LeaveDepth(); }
    }
}
