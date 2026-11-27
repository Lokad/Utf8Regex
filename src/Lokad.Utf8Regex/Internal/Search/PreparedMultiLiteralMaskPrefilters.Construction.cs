using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using Lokad.Utf8Regex.Internal.Input;

namespace Lokad.Utf8Regex.Internal.Search;

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
            ShortestLength = PreparedMultiLiteralConstruction.GetShortestLength(literals, budget);
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
            budget.Checkpoint();
            Span<bool> firstSeen = stackalloc bool[256];
            firstSeen.Clear();
            Span<byte> firstBytes = stackalloc byte[256];
            Span<byte> lowTable = stackalloc byte[16];
            Span<byte> highTable = stackalloc byte[16];
            var firstCount = 0;
            for (var offset = 0; offset < MaskLength; offset++)
            {
                budget.Checkpoint();
                lowTable.Clear();
                highTable.Clear();
                for (var literalIndex = 0; literalIndex < literals.Length; literalIndex++)
                {
                    budget.Checkpoint();
                    var bit = (byte)(1 << literalIndex);
                    var value = literals[literalIndex][MaskOffsets[offset]];
                    AddMaskValue(lowTable, highTable, value, bit);
                    if (asciiIgnoreCase && IsFoldedAsciiLetter(value))
                        AddMaskValue(lowTable, highTable, (byte)(value - ('a' - 'A')), bit);
                    else if (invariantCyrillicIgnoreCase &&
                        Utf8InvariantCyrillicCase.TryGetCounterpartByte(literals[literalIndex], MaskOffsets[offset], out var counterpart))
                        AddMaskValue(lowTable, highTable, counterpart, bit);
                }
                budget.Checkpoint();
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
                budget.Checkpoint();
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
        budget.Checkpoint();
        for (var offset = 0; offset < offsets.Length; offset++) offsets[offset] = offset;
        return offsets;
    }

    private static int[] SelectExactOffsets(byte[][] literals, int shortestLength, Utf8ConstructionBudget budget)
    {
        var threeScalars = shortestLength >= 9;
        if (threeScalars)
            foreach (var literal in literals)
            {
                budget.Checkpoint();
                if (!(literal[0] is >= 0xE0 and < 0xF0 && literal[1] is >= 0x80 and < 0xC0 && literal[2] is >= 0x80 and < 0xC0 &&
                    literal[3] is >= 0xE0 and < 0xF0 && literal[4] is >= 0x80 and < 0xC0 && literal[5] is >= 0x80 and < 0xC0 &&
                    literal[6] is >= 0xE0 and < 0xF0 && literal[7] is >= 0x80 and < 0xC0 && literal[8] is >= 0x80 and < 0xC0))
                { threeScalars = false; break; }
            }
        if (!threeScalars) return CreateSequentialOffsets(shortestLength, budget);
        var offsets = budget.AllocateArray<int>(3, false);
        budget.Checkpoint();
        offsets[0] = 2; offsets[1] = 5; offsets[2] = 8;
        return offsets;
    }

    private static int[] SelectAsciiIgnoreCaseOffsets(byte[][] literals, int shortestLength, Utf8ConstructionBudget budget)
    {
        const int selectedCount = 3;
        var offsets = budget.AllocateArray<int>(selectedCount, false);
        budget.Checkpoint();
        Span<long> weights = stackalloc long[literals.Length]; // Eligibility bounds this extent to eight.
        weights.Fill(1);
        for (var pick = 0; pick < selectedCount; pick++)
        {
            var bestOffset = -1;
            var bestScore = long.MaxValue;
            for (var offset = 0; offset < shortestLength; offset++)
            {
                budget.Checkpoint();
                // At most three offsets are selected; no length-sized bitmap is needed.
                if (offsets.AsSpan(0, pick).Contains(offset)) continue;
                long score = 0;
                for (var literalIndex = 0; literalIndex < literals.Length; literalIndex++)
                {
                    budget.Checkpoint();
                    score += weights[literalIndex] * (AsciiSearch.GetAsciiFrequencyRank(literals[literalIndex][offset]) + 1L);
                }
                if (score < bestScore) { bestScore = score; bestOffset = offset; }
            }
            budget.Checkpoint();
            offsets[pick] = bestOffset;
            for (var literalIndex = 0; literalIndex < literals.Length; literalIndex++)
            {
                budget.Checkpoint();
                weights[literalIndex] *= AsciiSearch.GetAsciiFrequencyRank(literals[literalIndex][bestOffset]) + 1L;
            }
        }
        return offsets;
    }
}
