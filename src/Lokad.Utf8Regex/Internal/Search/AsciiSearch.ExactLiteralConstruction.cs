namespace Lokad.Utf8Regex.Internal.Search;

internal static partial class AsciiSearch
{
    public static AsciiExactLiteralSearchData CreateExactLiteralSearchData(byte[][] literals)
    {
        if (literals.Length == 0) return new(PreparedByteSearch.FromOwnedValues([], null), [], [], int.MaxValue);
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
                var emptyBuckets = budget.AllocateArray<AsciiExactLiteralBucket>(1, false);
                emptyBuckets[0] = new(0, references, [], null, default);
                var emptyMap = budget.AllocateArray<int>(256, false);
                budget.ChargeWork(257);
                Array.Fill(emptyMap, -1);
                emptyMap[0] = 0;
                var empty = new AsciiExactLiteralSearchData(PreparedByteSearch.FromOwnedValues([], null), emptyBuckets, emptyMap, 0);
                scope.Complete();
                return empty;
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
                var first = literal[0];
                var group = indexByByte[first];
                if (group < 0)
                {
                    group = distinct++;
                    indexByByte[first] = group;
                    firstBytes[group] = first;
                }
                counts[group]++;
            }
            var buckets = budget.AllocateArray<AsciiExactLiteralBucket>(distinct, false);
            for (var group = 0; group < distinct; group++)
            {
                budget.ChargeWork(1);
                buckets[group] = new(firstBytes[group], budget.AllocateArray<byte[]>(counts[group], false), [], null, default);
                counts[group] = 0;
            }
            foreach (var literal in literals)
            {
                budget.ChargeWork(1);
                var group = indexByByte[literal[0]];
                buckets[group].Literals[counts[group]++] = literal;
            }
            for (var group = 0; group < distinct; group++)
            {
                budget.ChargeWork(1);
                var references = buckets[group].Literals;
                var prefix = GetCommonPrefix(references, budget);
                buckets[group] = new(firstBytes[group], references, prefix,
                    GetDistinctSecondBytes(references, budget), CreatePrefixDiscriminator(references, prefix.Length, budget));
            }
            var map = budget.AllocateArray<int>(256, false);
            budget.ChargeWork(256);
            indexByByte.CopyTo(map);
            var result = new AsciiExactLiteralSearchData(PreparedByteSearch.Create(firstBytes[..distinct]), buckets, map, shortest);
            scope.Complete();
            return result;
        }
        finally { budget.LeaveDepth(); }
    }

    private static byte[] GetCommonPrefix(byte[][] literals, Utf8ConstructionBudget budget)
    {
        var first = literals[0];
        var length = first.Length;
        for (var index = 1; index < literals.Length && length > 1; index++)
        {
            budget.ChargeWork(1);
            var other = literals[index];
            var limit = Math.Min(length, other.Length);
            var common = 0;
            while (common < limit)
            {
                budget.ChargeWork(1);
                if (first[common] != other[common]) break;
                common++;
            }
            length = common;
        }
        if (length <= 1) return [];
        var prefix = budget.AllocateArray<byte>(length, false);
        budget.ChargeWork((ulong)length);
        first.AsSpan(0, length).CopyTo(prefix);
        return prefix;
    }

    private static byte[]? GetDistinctSecondBytes(byte[][] literals, Utf8ConstructionBudget budget)
    {
        if (literals.Length <= 1) return null;
        Span<bool> seen = stackalloc bool[256];
        Span<byte> values = stackalloc byte[256];
        budget.ChargeWork(256);
        seen.Clear();
        var count = 0;
        foreach (var literal in literals)
        {
            budget.ChargeWork(1);
            if (literal.Length <= 1) return null;
            var value = literal[1];
            if (seen[value]) continue;
            seen[value] = true;
            values[count++] = value;
        }
        if (count <= 1) return null;
        var owned = budget.AllocateArray<byte>(count, false);
        budget.ChargeWork((ulong)count);
        values[..count].CopyTo(owned);
        return owned;
    }

    private static AsciiExactLiteralPrefixDiscriminator CreatePrefixDiscriminator(byte[][] literals, int prefixLength,
        Utf8ConstructionBudget budget)
    {
        if (literals.Length <= 1 || literals.Length > 256 || prefixLength <= 0) return default;
        var shortest = PreparedMultiLiteralConstruction.GetShortestLength(literals, budget);
        if (prefixLength >= shortest) return default;
        Span<bool> seen = stackalloc bool[256];
        budget.ChargeWork(256);
        seen.Clear();
        foreach (var literal in literals)
        {
            budget.ChargeWork(1);
            var value = literal[prefixLength];
            if (seen[value]) return default;
            seen[value] = true;
        }
        var map = budget.AllocateArray<byte[]>(256, false);
        foreach (var literal in literals)
        {
            budget.ChargeWork(1);
            map[literal[prefixLength]] = literal;
        }
        return new(prefixLength, map);
    }
}
