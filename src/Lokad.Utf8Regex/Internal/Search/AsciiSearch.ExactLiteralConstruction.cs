namespace Lokad.Utf8Regex.Internal.Search;

internal static partial class AsciiSearch
{
    public static AsciiExactLiteralSearchData CreateExactLiteralSearchData(byte[][] literals)
        => CreateExactLiteralSearchData(literals, fullMetadata: true);

    // Small-family verification uses literal/discriminator maps and full literal
    // equality. It has no consumer for prefix copies or second-byte membership.
    internal static AsciiExactLiteralSearchData CreateExactLiteralSearchData(byte[][] literals, bool fullMetadata)
    {
        if (literals.Length == 0) return new(PreparedByteSearch.FromOwnedValues([], null), [], [], int.MaxValue, 0);
        using var scope = Utf8ConstructionScope.Enter();
        var budget = scope.Budget;
        budget.EnterDepth();
        try
        {
            var shortest = int.MaxValue;
            var longest = 0;
            foreach (var literal in literals)
            {
                budget.Checkpoint();
                shortest = Math.Min(shortest, literal.Length);
                if (shortest == 0) break;
                longest = Math.Max(longest, literal.Length);
            }
            if (shortest == 0)
            {
                var references = budget.AllocateArray<byte[]>(1, false);
                references[0] = [];
                var emptyBuckets = budget.AllocateArray<AsciiExactLiteralBucket>(1, false);
                emptyBuckets[0] = new(0, references, [], null, default);
                var emptyMap = budget.AllocateArray<int>(256, false);
                budget.Checkpoint();
                Array.Fill(emptyMap, -1);
                emptyMap[0] = 0;
                var empty = new AsciiExactLiteralSearchData(PreparedByteSearch.FromOwnedValues([], null), emptyBuckets, emptyMap, 0, 0);
                scope.Complete();
                return empty;
            }

            Span<int> indexByByte = stackalloc int[256];
            Span<int> counts = stackalloc int[256];
            Span<byte> firstBytes = stackalloc byte[256];
            budget.Checkpoint();
            indexByByte.Fill(-1);
            counts.Clear();
            var distinct = 0;
            foreach (var literal in literals)
            {
                budget.Checkpoint();
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
                budget.Checkpoint();
                buckets[group] = new(firstBytes[group], budget.AllocateArray<byte[]>(counts[group], false), [], null, default);
                counts[group] = 0;
            }
            foreach (var literal in literals)
            {
                budget.Checkpoint();
                var group = indexByByte[literal[0]];
                buckets[group].Literals[counts[group]++] = literal;
            }
            for (var group = 0; group < distinct; group++)
            {
                budget.Checkpoint();
                var references = buckets[group].Literals;
                var prefixLength = 0;
                var prefix = fullMetadata || references.Length > 1
                    ? GetCommonPrefix(references, budget, fullMetadata, out prefixLength) : [];
                buckets[group] = new(firstBytes[group], references, prefix,
                    fullMetadata ? GetDistinctSecondBytes(references, budget) : null,
                    CreatePrefixDiscriminator(references, prefixLength, budget));
            }
            var map = budget.AllocateArray<int>(256, false);
            budget.Checkpoint();
            indexByByte.CopyTo(map);
            var result = new AsciiExactLiteralSearchData(PreparedByteSearch.Create(firstBytes[..distinct]), buckets, map, shortest, longest);
            scope.Complete();
            return result;
        }
        finally { budget.LeaveDepth(); }
    }

    private static byte[] GetCommonPrefix(byte[][] literals, Utf8ConstructionBudget budget, bool retain, out int prefixLength)
    {
        var first = literals[0];
        var length = first.Length;
        for (var index = 1; index < literals.Length && length > 1; index++)
        {
            budget.Checkpoint();
            var other = literals[index];
            var limit = Math.Min(length, other.Length);
            var common = 0;
            while (common < limit)
            {
                budget.Checkpoint();
                if (first[common] != other[common]) break;
                common++;
            }
            length = common;
        }
        prefixLength = length <= 1 ? 0 : length;
        if (prefixLength == 0 || !retain) return [];
        var prefix = budget.AllocateArray<byte>(length, false);
        budget.Checkpoint();
        first.AsSpan(0, length).CopyTo(prefix);
        return prefix;
    }

    private static byte[]? GetDistinctSecondBytes(byte[][] literals, Utf8ConstructionBudget budget)
    {
        if (literals.Length <= 1) return null;
        Span<bool> seen = stackalloc bool[256];
        Span<byte> values = stackalloc byte[256];
        budget.Checkpoint();
        seen.Clear();
        var count = 0;
        foreach (var literal in literals)
        {
            budget.Checkpoint();
            if (literal.Length <= 1) return null;
            var value = literal[1];
            if (seen[value]) continue;
            seen[value] = true;
            values[count++] = value;
        }
        if (count <= 1) return null;
        var owned = budget.AllocateArray<byte>(count, false);
        budget.Checkpoint();
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
        budget.Checkpoint();
        seen.Clear();
        foreach (var literal in literals)
        {
            budget.Checkpoint();
            var value = literal[prefixLength];
            if (seen[value]) return default;
            seen[value] = true;
        }
        var map = budget.AllocateArray<ushort>(256, false);
        for (var index = 0; index < literals.Length; index++)
        {
            budget.Checkpoint();
            map[literals[index][prefixLength]] = (ushort)(index + 1);
        }
        return new(prefixLength, map);
    }
}
