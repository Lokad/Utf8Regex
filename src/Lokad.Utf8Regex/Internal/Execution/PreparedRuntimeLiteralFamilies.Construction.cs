using Lokad.Utf8Regex.Internal.Input;
using Lokad.Utf8Regex.Internal.Search;

namespace Lokad.Utf8Regex.Internal.Execution;

internal readonly partial struct PreparedBmpThreeByteLiteralFamilySearch
{
    private readonly record struct ConstructionScalar(byte First, byte Second, byte Third);

    public static bool TryCreate(byte[][] literals, out PreparedBmpThreeByteLiteralFamilySearch search)
    {
        search = default;
        if (literals.Length == 0) return false;
        using var scope = Utf8ConstructionScope.Enter();
        var budget = scope.Budget;
        budget.EnterDepth();
        try
        {
            var shortest = PreparedMultiLiteralConstruction.GetShortestLength(literals, budget);
            // The caller proves the BMP scalar shape. This shared helper retains
            // three-byte membership guards; it does not establish UTF-8 validity.
            if (shortest < 3) { scope.Complete(); return false; }
            using var first = Utf8ConstructionBuffer<ConstructionScalar>.Create(budget);
            using var second = shortest >= 6 ? Utf8ConstructionBuffer<ConstructionScalar>.Create(budget) : null;
            using var third = shortest >= 9 ? Utf8ConstructionBuffer<ConstructionScalar>.Create(budget) : null;
            foreach (var literal in literals)
            {
                budget.ChargeWork(1);
                AddUnique(first, literal, 0, budget);
                if (second is not null) AddUnique(second, literal, 3, budget);
                if (third is not null) AddUnique(third, literal, 6, budget);
            }
            search = new(literals, Freeze(first, budget), second is null ? [] : Freeze(second, budget),
                third is null ? [] : Freeze(third, budget), shortest);
            scope.Complete();
            return true;
        }
        finally { budget.LeaveDepth(); }
    }

    private static void AddUnique(Utf8ConstructionBuffer<ConstructionScalar> scalars, byte[] literal, int offset,
        Utf8ConstructionBudget budget)
    {
        budget.ChargeWork(3);
        var scalar = new ConstructionScalar(literal[offset], literal[offset + 1], literal[offset + 2]);
        foreach (var existing in scalars)
        {
            budget.ChargeWork(3);
            if (existing == scalar) return;
        }
        scalars.Add(scalar);
    }

    private static byte[][] Freeze(Utf8ConstructionBuffer<ConstructionScalar> scalars, Utf8ConstructionBudget budget)
    {
        var result = budget.AllocateArray<byte[]>(scalars.Count, false);
        for (var index = 0; index < scalars.Count; index++)
        {
            var scalar = scalars[index];
            var bytes = budget.AllocateArray<byte>(3, false);
            budget.ChargeWork(3);
            bytes[0] = scalar.First; bytes[1] = scalar.Second; bytes[2] = scalar.Third;
            result[index] = bytes;
        }
        return result;
    }
}

internal readonly partial struct PreparedShortAsciiLiteralFamilyCounter
{
    public static bool TryCreate(byte[][] literals, out PreparedShortAsciiLiteralFamilyCounter counter)
    {
        counter = default;
        if (literals is not { Length: >= 2 and <= 4 }) return false;
        using var scope = Utf8ConstructionScope.Enter();
        var budget = scope.Budget;
        budget.EnterDepth();
        try
        {
            var shortest = int.MaxValue;
            foreach (var literal in literals)
            {
                budget.ChargeWork(1);
                if (literal.Length is < 3 or > 8) { scope.Complete(); return false; }
                budget.ChargeWork((ulong)literal.Length);
                if (!Utf8InputAnalyzer.IsAscii(literal)) { scope.Complete(); return false; }
                shortest = Math.Min(shortest, literal.Length);
            }
            if (!TryChooseBestUniquePair(literals, shortest, budget, out var primary, out var secondary))
            { scope.Complete(); return false; }

            Span<byte> values = stackalloc byte[4];
            // Membership has no consumer for duplicate bytes. Keeping them
            // could unnecessarily select an owned set for four literals with
            // only two or three distinct primary values.
            var count = GetDistinctValues(literals, primary, values, budget);
            counter = new(literals[0], literals[1], literals.Length > 2 ? literals[2] : [], literals.Length > 3 ? literals[3] : [],
                literals[0][primary], literals[1][primary], literals.Length > 2 ? literals[2][primary] : default,
                literals.Length > 3 ? literals[3][primary] : default,
                literals[0][secondary], literals[1][secondary], literals.Length > 2 ? literals[2][secondary] : default,
                literals.Length > 3 ? literals[3][secondary] : default,
                (byte)literals.Length, primary, secondary, shortest, PreparedByteSearch.Create(values[..count]));
            scope.Complete();
            return true;
        }
        finally { budget.LeaveDepth(); }
    }

    private static bool TryChooseBestUniquePair(byte[][] literals, int shortest, Utf8ConstructionBudget budget,
        out int primary, out int secondary)
    {
        primary = -1; secondary = -1;
        var bestScore = int.MaxValue;
        Span<byte> firstValues = stackalloc byte[4];
        Span<byte> secondValues = stackalloc byte[4];
        for (var first = 0; first < shortest; first++)
        {
            budget.ChargeWork(1);
            var firstCount = GetDistinctValues(literals, first, firstValues, budget);
            if (firstCount <= 1) continue;
            var firstScore = ScoreValues(firstValues[..firstCount], budget);
            for (var second = 0; second < shortest; second++)
            {
                budget.ChargeWork(1);
                if (second == first || !HasUniquePairs(literals, first, second, budget)) continue;
                var secondCount = GetDistinctValues(literals, second, secondValues, budget);
                if (secondCount <= 1) continue;
                var score = firstScore * 4 + ScoreValues(secondValues[..secondCount], budget);
                if (score < bestScore || score == bestScore && (primary < 0 || first < primary || first == primary && second < secondary))
                { bestScore = score; primary = first; secondary = second; }
            }
        }
        return primary >= 0;
    }

    private static int GetDistinctValues(byte[][] literals, int offset, Span<byte> values, Utf8ConstructionBudget budget)
    {
        var count = 0;
        foreach (var literal in literals)
        {
            budget.ChargeWork(1);
            var value = literal[offset];
            var found = false;
            for (var index = 0; index < count; index++)
            {
                budget.ChargeWork(1);
                if (values[index] != value) continue;
                found = true; break;
            }
            if (!found) values[count++] = value;
        }
        return count;
    }

    private static bool HasUniquePairs(byte[][] literals, int first, int second, Utf8ConstructionBudget budget)
    {
        for (var left = 0; left < literals.Length; left++)
            for (var right = left + 1; right < literals.Length; right++)
            {
                budget.ChargeWork(2);
                if (literals[left][first] == literals[right][first] && literals[left][second] == literals[right][second]) return false;
            }
        return true;
    }

    private static int ScoreValues(ReadOnlySpan<byte> values, Utf8ConstructionBudget budget)
    {
        var commonness = 0;
        foreach (var value in values)
        { budget.ChargeWork(1); commonness += PreparedMultiLiteralRareBytePrefilter.GetAsciiFrequencyRank(value); }
        return commonness * 8 + values.Length;
    }
}
