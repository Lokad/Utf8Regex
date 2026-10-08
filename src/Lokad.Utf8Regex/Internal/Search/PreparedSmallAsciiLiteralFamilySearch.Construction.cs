namespace Lokad.Utf8Regex.Internal.Search;

internal readonly partial struct PreparedSmallAsciiLiteralFamilySearch
{
    private readonly record struct RankedOffset(int Offset, int Score);

    public static bool TryCreate(byte[][] literals, out PreparedSmallAsciiLiteralFamilySearch search)
    {
        search = default;
        if (literals is not { Length: >= 3 and <= 6 }) return false;
        using var scope = Utf8ConstructionScope.Enter();
        var budget = scope.Budget;
        budget.EnterDepth();
        try
        {
            Span<byte> firstBytes = stackalloc byte[6];
            var distinct = 0;
            foreach (var literal in literals)
            {
                budget.ChargeWork(1);
                if (literal.Length < 2) return false;
                foreach (var value in literal)
                {
                    budget.ChargeWork(1);
                    if (value >= 0x80) return false;
                }
                budget.ChargeWork((ulong)distinct);
                if (!firstBytes[..distinct].Contains(literal[0])) firstBytes[distinct++] = literal[0];
            }
            // The original one-bucket rejection needs no prepared state.
            if (distinct < 2) return false;
            var data = AsciiSearch.CreateExactLiteralSearchData(literals, fullMetadata: false);

            Span<RankedOffset> best = stackalloc RankedOffset[4];
            var count = SelectOffsets(literals, data.ShortestLength, best, budget);
            // Eligibility proves offset zero has two through six distinct
            // values. A candidate always exists; the old fallback was dead.
            var anchor = CreateAnchorSearch(literals, best[0].Offset, budget);
            var filters = budget.AllocateArray<Filter>(count - 1, false);
            for (var index = 1; index < count; index++)
            {
                budget.ChargeWork(1);
                filters[index - 1] = new(best[index].Offset, CreateFilterValues(literals, best[index].Offset, budget));
            }
            var pair = CreateDispatch(literals, best[0].Offset, data.ShortestLength, budget, out var triple);
            search = new(data, best[0].Offset, anchor, filters, triple, pair);
            scope.Complete();
            return true;
        }
        finally { budget.LeaveDepth(); }
    }

    private static int SelectOffsets(byte[][] literals, int shortest, Span<RankedOffset> best,
        Utf8ConstructionBudget budget)
    {
        Span<int> seen = stackalloc int[128];
        Span<byte> values = stackalloc byte[6];
        budget.ChargeWork(128);
        seen.Clear();
        var selected = 0;
        for (var offset = 0; offset < shortest; offset++)
        {
            budget.ChargeWork(1);
            var count = 0;
            var stamp = offset + 1; // Array lengths keep this below Int32.MaxValue.
            foreach (var literal in literals)
            {
                budget.ChargeWork(1);
                var value = literal[offset];
                if (seen[value] == stamp) continue;
                seen[value] = stamp;
                values[count++] = value;
            }
            if (count <= 1) continue;
            var commonness = 0;
            budget.ChargeWork((ulong)count);
            for (var index = 0; index < count; index++)
                commonness += PreparedMultiLiteralRareBytePrefilter.GetAsciiFrequencyRank(values[index]);
            var candidate = new RankedOffset(offset, commonness * 8 + count);
            // At most four comparisons and shifts. This is the original
            // ascending score, descending offset order, without discarded arrays.
            budget.ChargeWork(8);
            var position = 0;
            while (position < selected && (best[position].Score < candidate.Score ||
                best[position].Score == candidate.Score && best[position].Offset > candidate.Offset)) position++;
            if (position == best.Length) continue;
            var end = Math.Min(selected, best.Length - 1);
            for (var index = end; index > position; index--) best[index] = best[index - 1];
            best[position] = candidate;
            selected = Math.Min(selected + 1, best.Length);
        }
        return selected;
    }

    private static int CollectValues(byte[][] literals, int offset, Span<byte> values, Utf8ConstructionBudget budget)
    {
        var count = 0;
        foreach (var literal in literals)
        {
            budget.ChargeWork(1 + (ulong)count);
            var value = literal[offset];
            if (!values[..count].Contains(value)) values[count++] = value;
        }
        return count;
    }

    private static PreparedByteSearch CreateAnchorSearch(byte[][] literals, int offset, Utf8ConstructionBudget budget)
    {
        Span<byte> values = stackalloc byte[6];
        var count = CollectValues(literals, offset, values, budget);
        return PreparedByteSearch.Create(values[..count]);
    }

    private static byte[] CreateFilterValues(byte[][] literals, int offset, Utf8ConstructionBudget budget)
    {
        Span<byte> values = stackalloc byte[6];
        var count = CollectValues(literals, offset, values, budget);
        var owned = budget.AllocateArray<byte>(count, false);
        budget.ChargeWork((ulong)count);
        values[..count].CopyTo(owned);
        return owned;
    }

    private static PairDispatch CreateDispatch(byte[][] literals, int primary, int shortest,
        Utf8ConstructionBudget budget, out TripleDispatch triple)
    {
        triple = default;
        for (var secondary = 0; secondary < shortest; secondary++)
        {
            budget.ChargeWork(1);
            if (secondary == primary || !HasUniquePairs(literals, primary, secondary, budget)) continue;
            if (literals.Length == 3)
            {
                budget.ChargeWork(3);
                triple = new(primary, secondary,
                    literals[0][primary], literals[0][secondary], literals[0],
                    literals[1][primary], literals[1][secondary], literals[1],
                    literals[2][primary], literals[2][secondary], literals[2]);
                return default;
            }
            var entries = budget.AllocateArray<PairDispatchEntry>(literals.Length, false);
            for (var index = 0; index < literals.Length; index++)
            {
                budget.ChargeWork(1);
                entries[index] = new(literals[index][primary], literals[index][secondary], literals[index]);
            }
            return new(primary, secondary, entries);
        }
        return default;
    }

    private static bool HasUniquePairs(byte[][] literals, int primary, int secondary, Utf8ConstructionBudget budget)
    {
        for (var first = 0; first < literals.Length; first++)
        {
            budget.ChargeWork(1);
            for (var second = first + 1; second < literals.Length; second++)
            {
                budget.ChargeWork(1);
                if (literals[first][primary] == literals[second][primary] &&
                    literals[first][secondary] == literals[second][secondary]) return false;
            }
        }
        return true;
    }
}
