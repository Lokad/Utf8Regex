namespace Lokad.Utf8Regex.Internal.Search;

internal static class PreparedMultiLiteralConstruction
{
    internal static int GetShortestLength(byte[][] literals, Utf8ConstructionBudget budget)
    {
        var shortest = int.MaxValue;
        foreach (var literal in literals)
        {
            budget.ChargeWork(1);
            shortest = Math.Min(shortest, literal.Length);
        }
        return shortest;
    }
}

internal readonly partial struct PreparedMultiLiteralPackedSearch
{
    public static bool TryCreate(byte[][] literals, out PreparedMultiLiteralPackedSearch search)
    {
        search = default;
        if (literals.Length == 0) return false;
        using var scope = Utf8ConstructionScope.Enter();
        var budget = scope.Budget;
        budget.EnterDepth();
        try
        {
            var shortest = PreparedMultiLiteralConstruction.GetShortestLength(literals, budget);
            // A byte discriminator cannot distinguish more than 256 literals.
            // Reject that family without repeatedly scanning every offset.
            if (shortest > 0 && literals.Length <= 256)
                for (var offset = 0; offset < shortest; offset++)
                {
                    budget.ChargeWork(1);
                    if (!TryCreateAtOffset(literals, shortest, offset, budget, out search)) continue;
                    scope.Complete();
                    return true;
                }
            scope.Complete();
            return false;
        }
        finally { budget.LeaveDepth(); }
    }

    internal static bool TryCreateAtDiscriminatorOffset(byte[][] literals, int offset, out PreparedMultiLiteralPackedSearch search)
    {
        search = default;
        if (literals.Length == 0) return false;
        using var scope = Utf8ConstructionScope.Enter();
        var budget = scope.Budget;
        budget.EnterDepth();
        try
        {
            var shortest = PreparedMultiLiteralConstruction.GetShortestLength(literals, budget);
            var accepted = shortest > 0 && literals.Length <= 256 && TryCreateAtOffset(literals, shortest, offset, budget, out search);
            scope.Complete();
            return accepted;
        }
        finally { budget.LeaveDepth(); }
    }

    private static bool TryCreateAtOffset(byte[][] literals, int shortest, int offset, Utf8ConstructionBudget budget,
        out PreparedMultiLiteralPackedSearch search)
    {
        search = default;
        if ((uint)offset >= (uint)shortest) return false;
        Span<byte> discriminatorBytes = stackalloc byte[256];
        Span<bool> seen = stackalloc bool[256];
        budget.ChargeWork(256);
        seen.Clear();
        for (var index = 0; index < literals.Length; index++)
        {
            budget.ChargeWork(1);
            var value = literals[index][offset];
            if (seen[value]) return false;
            seen[value] = true;
            discriminatorBytes[index] = value;
        }
        var literalMap = budget.AllocateArray<byte[]>(256, false);
        var idMap = budget.AllocateArray<int>(256, false);
        var hasSecond = offset + 1 < shortest;
        var hasThird = offset + 2 < shortest;
        // Runtime accesses are guarded by these flags. Absent anchors borrow
        // empty tables rather than retaining two unused 256-entry extents.
        var secondMap = hasSecond ? budget.AllocateArray<byte>(256, false) : [];
        var thirdMap = hasThird ? budget.AllocateArray<byte>(256, false) : [];
        budget.ChargeWork(256);
        Array.Fill(idMap, -1);
        for (var index = 0; index < literals.Length; index++)
        {
            budget.ChargeWork(1);
            var literal = literals[index];
            var discriminator = literal[offset];
            literalMap[discriminator] = literal;
            idMap[discriminator] = index;
            if (hasSecond) secondMap[discriminator] = literal[offset + 1];
            if (hasThird) thirdMap[discriminator] = literal[offset + 2];
        }
        search = new(PreparedByteSearch.Create(discriminatorBytes[..literals.Length]), literals,
            literalMap, idMap, secondMap, thirdMap, hasSecond, hasThird, offset, shortest);
        return true;
    }
}

internal readonly partial struct PreparedMultiLiteralLeadingUtf8SegmentPrefilter
{
    public PreparedMultiLiteralLeadingUtf8SegmentPrefilter(byte[][] literals)
    {
        ShortestLength = int.MaxValue;
        Segments = [];
        if (literals.Length == 0) return;
        using var scope = Utf8ConstructionScope.Enter();
        var budget = scope.Budget;
        budget.EnterDepth();
        try
        {
            ShortestLength = PreparedMultiLiteralConstruction.GetShortestLength(literals, budget);
            foreach (var literal in literals)
            {
                budget.ChargeWork(6);
                if (TryGetLeadingSegmentLength(literal, out _)) continue;
                scope.Complete();
                return;
            }
            using var segments = Utf8ConstructionBuffer<byte[]>.Create(budget);
            foreach (var literal in literals)
            {
                budget.ChargeWork(6);
                _ = TryGetLeadingSegmentLength(literal, out var segmentLength);
                var segment = literal.AsSpan(0, segmentLength);
                var found = false;
                foreach (var existing in segments)
                {
                    budget.ChargeWork(1);
                    if (existing.Length != segmentLength) continue;
                    budget.ChargeWork((ulong)segmentLength);
                    if (existing.AsSpan().SequenceEqual(segment)) { found = true; break; }
                }
                if (found) continue;
                var owned = budget.AllocateArray<byte>(segmentLength, false);
                budget.ChargeWork((ulong)segmentLength);
                segment.CopyTo(owned);
                segments.Add(owned);
            }
            Segments = segments.Take();
            scope.Complete();
        }
        finally { budget.LeaveDepth(); }
    }
}
