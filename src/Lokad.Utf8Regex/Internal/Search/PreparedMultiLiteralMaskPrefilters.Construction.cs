using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using Lokad.Utf8Regex.Internal.Input;

namespace Lokad.Utf8Regex.Internal.Search;

internal static class PreparedMultiLiteralMaskConstruction
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

internal readonly partial struct PreparedMultiLiteralPackedNibbleSimdPrefilter
{
    public PreparedMultiLiteralPackedNibbleSimdPrefilter(byte[][] literals)
        : this(literals, asciiIgnoreCase: false, invariantCyrillicIgnoreCase: false) { }

    private PreparedMultiLiteralPackedNibbleSimdPrefilter(byte[][] literals, bool asciiIgnoreCase, bool invariantCyrillicIgnoreCase)
    {
        ShortestLength = int.MaxValue;
        MaskLength = 0;
        MaskOffsets = [];
        LowMaskVectors = [];
        HighMaskVectors = [];
        Search = default;
        if (literals.Length == 0) return;
        using var scope = Utf8ConstructionScope.Enter();
        var budget = scope.Budget;
        budget.EnterDepth();
        try
        {
            ShortestLength = PreparedMultiLiteralMaskConstruction.GetShortestLength(literals, budget);
            if (!Ssse3.IsSupported || !Sse2.IsSupported || literals.Length > 8 || ShortestLength <= 0 || ShortestLength == int.MaxValue)
            {
                scope.Complete();
                return;
            }
            MaskOffsets = invariantCyrillicIgnoreCase ? CreateSequentialOffsets(ShortestLength, budget)
                : asciiIgnoreCase && ShortestLength >= 8 ? SelectAsciiIgnoreCaseOffsets(literals, ShortestLength, budget)
                : SelectExactOffsets(literals, ShortestLength, budget);
            MaskLength = MaskOffsets.Length;
            LowMaskVectors = budget.AllocateArray<Vector256<byte>>(MaskLength, false);
            HighMaskVectors = budget.AllocateArray<Vector256<byte>>(MaskLength, false);
            budget.ChargeWork(256);
            Span<bool> firstSeen = stackalloc bool[256];
            firstSeen.Clear();
            Span<byte> firstBytes = stackalloc byte[256];
            Span<byte> lowTable = stackalloc byte[16];
            Span<byte> highTable = stackalloc byte[16];
            var firstCount = 0;
            for (var offset = 0; offset < MaskLength; offset++)
            {
                budget.ChargeWork(32);
                lowTable.Clear();
                highTable.Clear();
                for (var literalIndex = 0; literalIndex < literals.Length; literalIndex++)
                {
                    budget.ChargeWork(1);
                    var bit = (byte)(1 << literalIndex);
                    var value = literals[literalIndex][MaskOffsets[offset]];
                    AddMaskValue(lowTable, highTable, value, bit);
                    if (asciiIgnoreCase && IsFoldedAsciiLetter(value))
                        AddMaskValue(lowTable, highTable, (byte)(value - ('a' - 'A')), bit);
                    else if (invariantCyrillicIgnoreCase &&
                        Utf8InvariantCyrillicCase.TryGetCounterpartByte(literals[literalIndex], MaskOffsets[offset], out var counterpart))
                        AddMaskValue(lowTable, highTable, counterpart, bit);
                }
                budget.ChargeWork(32);
                var lowMask = Vector128.Create(
                    lowTable[0], lowTable[1], lowTable[2], lowTable[3],
                    lowTable[4], lowTable[5], lowTable[6], lowTable[7],
                    lowTable[8], lowTable[9], lowTable[10], lowTable[11],
                    lowTable[12], lowTable[13], lowTable[14], lowTable[15]);
                var highMask = Vector128.Create(
                    highTable[0], highTable[1], highTable[2], highTable[3],
                    highTable[4], highTable[5], highTable[6], highTable[7],
                    highTable[8], highTable[9], highTable[10], highTable[11],
                    highTable[12], highTable[13], highTable[14], highTable[15]);
                LowMaskVectors[offset] = Vector256.Create(lowMask, lowMask);
                HighMaskVectors[offset] = Vector256.Create(highMask, highMask);
            }
            for (var literalIndex = 0; literalIndex < literals.Length; literalIndex++)
            {
                budget.ChargeWork(1);
                var first = literals[literalIndex][0];
                if (!firstSeen[first]) { firstSeen[first] = true; firstBytes[firstCount++] = first; }
                if (asciiIgnoreCase && IsFoldedAsciiLetter(first))
                {
                    var upper = (byte)(first - ('a' - 'A'));
                    if (!firstSeen[upper]) { firstSeen[upper] = true; firstBytes[firstCount++] = upper; }
                }
                else if (invariantCyrillicIgnoreCase &&
                    Utf8InvariantCyrillicCase.TryGetCounterpartByte(literals[literalIndex], 0, out var counterpart) && !firstSeen[counterpart])
                { firstSeen[counterpart] = true; firstBytes[firstCount++] = counterpart; }
            }
            Search = firstCount == 0 ? default : PreparedByteSearch.Create(firstBytes[..firstCount]);
            scope.Complete();
        }
        finally { budget.LeaveDepth(); }
    }

    private static int[] CreateSequentialOffsets(int shortestLength, Utf8ConstructionBudget budget)
    {
        var offsets = budget.AllocateArray<int>(Math.Min(MaxMaskLen, shortestLength), false);
        budget.ChargeWork((ulong)offsets.Length);
        for (var offset = 0; offset < offsets.Length; offset++) offsets[offset] = offset;
        return offsets;
    }

    private static int[] SelectExactOffsets(byte[][] literals, int shortestLength, Utf8ConstructionBudget budget)
    {
        var threeScalars = shortestLength >= 9;
        if (threeScalars)
            foreach (var literal in literals)
            {
                budget.ChargeWork(9);
                if (!(literal[0] is >= 0xE0 and < 0xF0 && literal[1] is >= 0x80 and < 0xC0 && literal[2] is >= 0x80 and < 0xC0 &&
                    literal[3] is >= 0xE0 and < 0xF0 && literal[4] is >= 0x80 and < 0xC0 && literal[5] is >= 0x80 and < 0xC0 &&
                    literal[6] is >= 0xE0 and < 0xF0 && literal[7] is >= 0x80 and < 0xC0 && literal[8] is >= 0x80 and < 0xC0))
                { threeScalars = false; break; }
            }
        if (!threeScalars) return CreateSequentialOffsets(shortestLength, budget);
        var offsets = budget.AllocateArray<int>(3, false);
        budget.ChargeWork(3);
        offsets[0] = 2; offsets[1] = 5; offsets[2] = 8;
        return offsets;
    }

    private static int[] SelectAsciiIgnoreCaseOffsets(byte[][] literals, int shortestLength, Utf8ConstructionBudget budget)
    {
        const int selectedCount = 3;
        var offsets = budget.AllocateArray<int>(selectedCount, false);
        budget.ChargeWork((ulong)literals.Length);
        Span<long> weights = stackalloc long[literals.Length]; // Eligibility bounds this extent to eight.
        weights.Fill(1);
        for (var pick = 0; pick < selectedCount; pick++)
        {
            var bestOffset = -1;
            var bestScore = long.MaxValue;
            for (var offset = 0; offset < shortestLength; offset++)
            {
                budget.ChargeWork((ulong)(pick + 1));
                // At most three offsets are selected; no length-sized bitmap is needed.
                if (offsets.AsSpan(0, pick).Contains(offset)) continue;
                long score = 0;
                for (var literalIndex = 0; literalIndex < literals.Length; literalIndex++)
                {
                    budget.ChargeWork(1);
                    score += weights[literalIndex] * (PreparedMultiLiteralRareBytePrefilter.GetAsciiFrequencyRank(literals[literalIndex][offset]) + 1L);
                }
                if (score < bestScore) { bestScore = score; bestOffset = offset; }
            }
            budget.ChargeWork(1);
            offsets[pick] = bestOffset;
            for (var literalIndex = 0; literalIndex < literals.Length; literalIndex++)
            {
                budget.ChargeWork(1);
                weights[literalIndex] *= PreparedMultiLiteralRareBytePrefilter.GetAsciiFrequencyRank(literals[literalIndex][bestOffset]) + 1L;
            }
        }
        return offsets;
    }
}

internal readonly partial struct PreparedMultiLiteralNibbleMaskPrefilter
{
    public PreparedMultiLiteralNibbleMaskPrefilter(byte[][] literals)
    {
        ShortestLength = int.MaxValue;
        Search = default;
        LowMasks = [];
        HighMasks = [];
        MaskLength = 0;
        if (literals.Length == 0) return;
        using var scope = Utf8ConstructionScope.Enter();
        var budget = scope.Budget;
        budget.EnterDepth();
        try
        {
            ShortestLength = PreparedMultiLiteralMaskConstruction.GetShortestLength(literals, budget);
            if (literals.Length > 64 || ShortestLength <= 0 || ShortestLength == int.MaxValue)
            { scope.Complete(); return; }
            MaskLength = Math.Min(MaxMaskLen, ShortestLength);
            LowMasks = budget.AllocateArray<ulong[]>(MaskLength, false);
            HighMasks = budget.AllocateArray<ulong[]>(MaskLength, false);
            for (var offset = 0; offset < MaskLength; offset++)
            {
                LowMasks[offset] = budget.AllocateArray<ulong>(16, false);
                HighMasks[offset] = budget.AllocateArray<ulong>(16, false);
            }
            budget.ChargeWork(256);
            Span<bool> firstSeen = stackalloc bool[256];
            firstSeen.Clear();
            Span<byte> firstBytes = stackalloc byte[256];
            var firstCount = 0;
            for (var literalIndex = 0; literalIndex < literals.Length; literalIndex++)
            {
                budget.ChargeWork(1);
                var literal = literals[literalIndex];
                var bit = 1UL << literalIndex;
                for (var offset = 0; offset < MaskLength; offset++)
                {
                    budget.ChargeWork(1);
                    var value = literal[offset];
                    LowMasks[offset][value & 0xF] |= bit;
                    HighMasks[offset][value >> 4] |= bit;
                }
                var first = literal[0];
                if (!firstSeen[first]) { firstSeen[first] = true; firstBytes[firstCount++] = first; }
            }
            Search = firstCount == 0 ? default : PreparedByteSearch.Create(firstBytes[..firstCount]);
            scope.Complete();
        }
        finally { budget.LeaveDepth(); }
    }
}

internal readonly partial struct PreparedMultiLiteralOffsetMaskPrefilter
{
    public PreparedMultiLiteralOffsetMaskPrefilter(byte[][] literals)
    {
        ShortestLength = int.MaxValue;
        Offsets = [];
        Masks = [];
        Search = default;
        if (literals.Length == 0) return;
        using var scope = Utf8ConstructionScope.Enter();
        var budget = scope.Budget;
        budget.EnterDepth();
        try
        {
            ShortestLength = PreparedMultiLiteralMaskConstruction.GetShortestLength(literals, budget);
            if (ShortestLength <= 0 || ShortestLength == int.MaxValue) { scope.Complete(); return; }
            var count = Math.Min(MaxOffsets, ShortestLength);
            Span<OffsetScore> selected = stackalloc OffsetScore[MaxOffsets];
            Span<bool> seen = stackalloc bool[256];
            var selectedCount = 0;
            for (var offset = 0; offset < ShortestLength; offset++)
            {
                budget.ChargeWork(256);
                seen.Clear();
                var cardinality = 0; var frequency = 0;
                foreach (var literal in literals)
                {
                    budget.ChargeWork(1);
                    var value = literal[offset];
                    if (seen[value]) continue;
                    seen[value] = true; cardinality++;
                    frequency += PreparedMultiLiteralRareBytePrefilter.GetAsciiFrequencyRank(value);
                }
                var score = new OffsetScore(offset, cardinality, frequency);
                var position = 0;
                while (position < selectedCount)
                {
                    budget.ChargeWork(1);
                    if (IsBetterScore(score, selected[position])) break;
                    position++;
                }
                if (position >= count) continue;
                var end = Math.Min(selectedCount, count - 1);
                budget.ChargeWork((ulong)(end - position + 1));
                for (var index = end; index > position; index--) selected[index] = selected[index - 1];
                selected[position] = score;
                selectedCount = Math.Min(selectedCount + 1, count);
            }
            Offsets = budget.AllocateArray<int>(count, false);
            Masks = budget.AllocateArray<byte[]>(count, false);
            for (var index = 0; index < count; index++)
            {
                budget.ChargeWork(1);
                Offsets[index] = selected[index].Offset;
                var mask = budget.AllocateArray<byte>(256, false);
                foreach (var literal in literals)
                { budget.ChargeWork(1); mask[literal[selected[index].Offset]] = 1; }
                Masks[index] = mask;
            }
            Span<byte> searchBytes = stackalloc byte[256];
            var searchCount = 0;
            budget.ChargeWork(256);
            for (var value = 0; value < 256; value++)
                if (Masks[0][value] != 0) searchBytes[searchCount++] = (byte)value;
            Search = searchCount == 0 ? default : PreparedByteSearch.Create(searchBytes[..searchCount]);
            scope.Complete();
        }
        finally { budget.LeaveDepth(); }
    }

    private static bool IsBetterScore(OffsetScore left, OffsetScore right) =>
        left.Cardinality < right.Cardinality || left.Cardinality == right.Cardinality &&
        (left.FrequencyScore < right.FrequencyScore || left.FrequencyScore == right.FrequencyScore && left.Offset < right.Offset);
}

internal readonly partial struct PreparedMultiLiteralRareBytePrefilter
{
    public PreparedMultiLiteralRareBytePrefilter(byte[][] literals)
    {
        ShortestLength = int.MaxValue;
        OffsetByValue = [];
        Search = default;
        if (literals.Length == 0) return;
        using var scope = Utf8ConstructionScope.Enter();
        var budget = scope.Budget;
        budget.EnterDepth();
        try
        {
            ShortestLength = PreparedMultiLiteralMaskConstruction.GetShortestLength(literals, budget);
            OffsetByValue = budget.AllocateArray<int>(256, false);
            budget.ChargeWork(768);
            Array.Fill(OffsetByValue, -1);
            Span<bool> seen = stackalloc bool[256];
            seen.Clear();
            Span<int> maxOffsets = stackalloc int[256];
            maxOffsets.Fill(-1);
            foreach (var literal in literals)
                for (var offset = 0; offset < literal.Length; offset++)
                {
                    budget.ChargeWork(1);
                    var value = literal[offset];
                    seen[value] = true;
                    if (offset > maxOffsets[value]) maxOffsets[value] = offset;
                }
            Span<byte> selected = stackalloc byte[3];
            var count = 0;
            for (var pick = 0; pick < selected.Length; pick++)
            {
                var bestValue = -1; var bestRank = int.MaxValue;
                budget.ChargeWork(256);
                for (var value = 0; value < 256; value++)
                {
                    if (!seen[value] || OffsetByValue[value] >= 0) continue;
                    var rank = GetAsciiFrequencyRank((byte)value);
                    if (rank < bestRank) { bestRank = rank; bestValue = value; }
                }
                if (bestValue < 0) break;
                budget.ChargeWork(1);
                selected[count++] = (byte)bestValue;
                OffsetByValue[bestValue] = maxOffsets[bestValue];
            }
            Search = count == 0 ? default : PreparedByteSearch.Create(selected[..count]);
            scope.Complete();
        }
        finally { budget.LeaveDepth(); }
    }
}
