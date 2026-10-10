using Lokad.Utf8Regex.Internal.Search;
using Lokad.Utf8Regex.Internal.Execution;
namespace Lokad.Utf8Regex.Internal.FrontEnd;


internal static partial class Utf8AsciiSimplePatternLowerer
{
    private static bool TryExtractBoundedSuffixLiteralPlan(
        Utf8ConstructionSession storage,
        AsciiSimplePatternToken[][] branches,
        bool isStartAnchored,
        bool isEndAnchored,
        bool allowUnicodeWhitespace,
        out AsciiSimplePatternBoundedSuffixLiteralPlan plan)
    {
        plan = default;
        if (isStartAnchored || isEndAnchored || branches.Length < 2)
        {
            return false;
        }

        var firstBranch = branches[0];
        if (firstBranch.Length < 3 ||
            firstBranch[0].Kind != AsciiSimplePatternTokenKind.CharClass ||
            firstBranch[0].CharClass.IsEmpty ||
            firstBranch[^1].Kind != AsciiSimplePatternTokenKind.CharClass ||
            firstBranch[^1].CharClass.IsEmpty ||
            !TryExtractTrailingLiteral(storage, firstBranch, out var literalStart, out var literalUtf8) ||
            literalStart < 1 ||
            !TryGetUniformRepeatedClass(
                firstBranch,
                1,
                literalStart,
                out var repeatedCharClass,
                out var repeatedRequiresAsciiInput))
        {
            return false;
        }

        var prefixCharClass = firstBranch[0].CharClass;
        var suffixCharClass = firstBranch[^1].CharClass;

        var minLength = literalStart - 1;
        var maxLength = minLength;
        for (var i = 1; i < branches.Length; i++)
        {
            Utf8ConstructionScope.ChargeWork(1);
            var branch = branches[i];
            if (branch.Length < literalUtf8.Length + 2 ||
                branch[0].Kind != AsciiSimplePatternTokenKind.CharClass ||
                branch[0].CharClass.IsEmpty ||
                !prefixCharClass.HasSameDefinition(branch[0].CharClass) ||
                branch[0].ScalarClassKind != firstBranch[0].ScalarClassKind ||
                branch[^1].Kind != AsciiSimplePatternTokenKind.CharClass ||
                branch[^1].CharClass.IsEmpty ||
                !suffixCharClass.HasSameDefinition(branch[^1].CharClass) ||
                branch[^1].ScalarClassKind != firstBranch[^1].ScalarClassKind ||
                !TryExtractTrailingLiteral(storage, branch, out var nextLiteralStart, out var nextLiteralUtf8) ||
                !SameLiteral(nextLiteralUtf8, literalUtf8))
            {
                return false;
            }

            var repeatedLength = nextLiteralStart - 1;
            if (repeatedLength > 0)
            {
                if (!TryGetUniformRepeatedClass(
                        branch,
                        1,
                        nextLiteralStart,
                        out var nextRepeatedCharClass,
                        out var nextRepeatedRequiresAsciiInput) ||
                    !repeatedCharClass.HasSameDefinition(nextRepeatedCharClass) ||
                    nextRepeatedRequiresAsciiInput != repeatedRequiresAsciiInput)
                {
                    return false;
                }
            }

            minLength = Math.Min(minLength, repeatedLength);
            maxLength = Math.Max(maxLength, repeatedLength);
        }

        plan = new AsciiSimplePatternBoundedSuffixLiteralPlan(
            prefixCharClass,
            repeatedCharClass,
            minLength,
            maxLength,
            literalUtf8,
            suffixCharClass,
            allowUnicodeWhitespace
                ? firstBranch[0].ScalarClassKind
                : Utf8SimplePatternScalarClassKind.None,
            allowUnicodeWhitespace
                ? firstBranch[^1].ScalarClassKind
                : Utf8SimplePatternScalarClassKind.None,
            repeatedRequiresAsciiInput);
        return plan.HasValue;
    }

    private static bool TryExtractTrailingLiteral(
        Utf8ConstructionSession storage,
        AsciiSimplePatternToken[] tokens,
        out int literalStart,
        out byte[] literalUtf8)
    {
        literalStart = -1;
        literalUtf8 = [];
        if (tokens.Length < 3 || tokens[^1].Kind == AsciiSimplePatternTokenKind.Literal)
        {
            return false;
        }

        var endExclusive = tokens.Length - 1;
        var start = endExclusive;
        while (start > 0 && tokens[start - 1].Kind == AsciiSimplePatternTokenKind.Literal)
        {
            Utf8ConstructionScope.ChargeWork(1);
            start--;
        }

        if (start == endExclusive)
        {
            return false;
        }

        literalStart = start;
        literalUtf8 = storage.Allocate<byte>(endExclusive - start, false);
        for (var i = 0; i < literalUtf8.Length; i++)
        {
            Utf8ConstructionScope.ChargeWork(1);
            literalUtf8[i] = tokens[start + i].Literal;
        }

        return true;
    }

    private static bool TryExtractSymmetricLiteralWindowPlan(
        Utf8ConstructionSession storage,
        AsciiSimplePatternToken[][] branches,
        bool isStartAnchored,
        bool isEndAnchored,
        out AsciiSimplePatternSymmetricLiteralWindowPlan plan)
    {
        plan = default;
        if (isStartAnchored || isEndAnchored || branches.Length < 2)
        {
            return false;
        }

        byte[]? literalA = null;
        byte[]? literalB = null;
        var minGap = int.MaxValue;
        var maxGap = int.MinValue;
        bool? gapSameLine = null;
        using var direction0Gaps = Utf8ConstructionMap<int, byte>.Create(storage.Budget, EqualityComparer<int>.Default);
        using var direction1Gaps = Utf8ConstructionMap<int, byte>.Create(storage.Budget, EqualityComparer<int>.Default);

        for (var i = 0; i < branches.Length; i++)
        {
            Utf8ConstructionScope.ChargeWork(1);
            if (TryParseLiteralGapLiteralBranch(storage, branches[i], out var leadingLiteral, out var trailingLiteral, out var branchGapCount, out var branchGapSameLine))
            {
                if (literalA is null)
                {
                    literalA = leadingLiteral;
                    literalB = trailingLiteral;
                    minGap = branchGapCount;
                    maxGap = branchGapCount;
                    gapSameLine = branchGapSameLine;
                    direction0Gaps[branchGapCount] = 0;
                    continue;
                }

                if (branchGapSameLine != gapSameLine)
                {
                    return false;
                }

                minGap = Math.Min(minGap, branchGapCount);
                maxGap = Math.Max(maxGap, branchGapCount);

                if (SameLiteral(leadingLiteral, literalA) &&
                    SameLiteral(trailingLiteral, literalB))
                {
                    direction0Gaps[branchGapCount] = 0;
                    continue;
                }

                if (SameLiteral(leadingLiteral, literalB) &&
                    SameLiteral(trailingLiteral, literalA))
                {
                    direction1Gaps[branchGapCount] = 0;
                    continue;
                }

                return false;
            }

            if (!TryExtractFullLiteral(storage, branches[i], out var fullLiteral))
            {
                return false;
            }

            if (literalA is null || literalB is null)
            {
                return false;
            }

            if (EqualsConcat(fullLiteral, literalA, literalB))
            {
                direction0Gaps[0] = 0;
                minGap = Math.Min(minGap, 0);
                maxGap = Math.Max(maxGap, 0);
                continue;
            }

            if (EqualsConcat(fullLiteral, literalB, literalA))
            {
                direction1Gaps[0] = 0;
                minGap = Math.Min(minGap, 0);
                maxGap = Math.Max(maxGap, 0);
                continue;
            }

            return false;
        }

        if (literalA is null || literalB is null || direction0Gaps.Count == 0 || direction1Gaps.Count == 0)
        {
            return false;
        }

        var expectedGapCount = maxGap - minGap + 1;
        var expectedBranchCount = 2 * expectedGapCount;
        if (branches.Length != expectedBranchCount || literalA[0] == literalB[0])
        {
            return false;
        }

        for (var gap = minGap; gap <= maxGap; gap++)
        {
            Utf8ConstructionScope.ChargeWork(1);
            if (!direction0Gaps.ContainsKey(gap) || !direction1Gaps.ContainsKey(gap))
            {
                return false;
            }
        }

        var anchorOffset = SelectSymmetricWindowAnchorOffset(literalA, literalB);
        SelectSymmetricWindowFilterOffsets(literalA, literalB, anchorOffset, out var firstFilterOffset, out var secondFilterOffset);
        var pair = storage.Allocate<byte[]>(2, false);
        pair[0] = literalA;
        pair[1] = literalB;
        plan = new AsciiSimplePatternSymmetricLiteralWindowPlan(
            literalA,
            literalB,
            AsciiSearch.CreateExactLiteralSearchData(pair),
            anchorOffset,
            literalA[anchorOffset],
            literalB[anchorOffset],
            minGap,
            maxGap,
            gapSameLine ?? true,
            firstFilterOffset,
            firstFilterOffset >= 0 && firstFilterOffset < literalA.Length ? literalA[firstFilterOffset] : (byte)0,
            firstFilterOffset >= 0 && firstFilterOffset < literalB.Length ? literalB[firstFilterOffset] : (byte)0,
            secondFilterOffset,
            secondFilterOffset >= 0 && secondFilterOffset < literalA.Length ? literalA[secondFilterOffset] : (byte)0,
            secondFilterOffset >= 0 && secondFilterOffset < literalB.Length ? literalB[secondFilterOffset] : (byte)0);
        return true;
    }

    private static int SelectSymmetricWindowAnchorOffset(byte[] literalA, byte[] literalB)
    {
        var maxSharedOffset = Math.Min(literalA.Length, literalB.Length) - 1;
        var bestOffset = 0;
        var bestScore = int.MaxValue;

        for (var offset = 0; offset <= maxSharedOffset; offset++)
        {
            Utf8ConstructionScope.ChargeWork(1);
            if (literalA[offset] == literalB[offset])
            {
                continue;
            }

            var score = GetSymmetricWindowAnchorScore(literalA[offset]) + GetSymmetricWindowAnchorScore(literalB[offset]);
            if (score < bestScore)
            {
                bestScore = score;
                bestOffset = offset;
            }
        }

        return bestOffset;
    }

    private static int GetSymmetricWindowAnchorScore(byte value)
    {
        value = value is >= (byte)'A' and <= (byte)'Z' ? (byte)(value + 32) : value;
        return value switch
        {
            (byte)'e' => 26,
            (byte)'t' => 25,
            (byte)'a' => 24,
            (byte)'o' => 23,
            (byte)'i' => 22,
            (byte)'n' => 21,
            (byte)'s' => 20,
            (byte)'h' => 19,
            (byte)'r' => 18,
            (byte)'d' => 17,
            (byte)'l' => 16,
            (byte)'u' => 15,
            (byte)'c' => 14,
            (byte)'m' => 13,
            (byte)'f' => 12,
            (byte)'y' => 11,
            (byte)'w' => 10,
            (byte)'g' => 9,
            (byte)'p' => 8,
            (byte)'b' => 7,
            (byte)'v' => 6,
            (byte)'k' => 5,
            (byte)'x' => 4,
            (byte)'q' => 3,
            (byte)'j' => 2,
            (byte)'z' => 1,
            _ => 0,
        };
    }

    private static void SelectSymmetricWindowFilterOffsets(byte[] literalA, byte[] literalB, int anchorOffset, out int firstOffset, out int secondOffset)
    {
        firstOffset = -1;
        secondOffset = -1;

        var maxSharedOffset = Math.Min(literalA.Length, literalB.Length) - 1;
        if (maxSharedOffset < 0)
        {
            return;
        }

        var preferredPrimary = Math.Min(3, maxSharedOffset);
        firstOffset = FindNearestDifferingOffset(literalA, literalB, preferredPrimary, minOffset: 0, excludedOffset: anchorOffset);
        if (firstOffset < 0)
        {
            return;
        }

        secondOffset = FindNearestDifferingOffset(literalA, literalB, preferredOffset: 1, minOffset: 0, excludedOffset: firstOffset);
        if (secondOffset == anchorOffset)
        {
            secondOffset = -1;
        }
    }

    private static int FindNearestDifferingOffset(
        byte[] literalA,
        byte[] literalB,
        int preferredOffset,
        int minOffset,
        int excludedOffset)
    {
        var maxOffset = Math.Min(literalA.Length, literalB.Length) - 1;
        for (var delta = 0; delta <= maxOffset; delta++)
        {
            Utf8ConstructionScope.ChargeWork(1);
            var lower = preferredOffset - delta;
            if (lower >= minOffset &&
                lower <= maxOffset &&
                lower != excludedOffset &&
                literalA[lower] != literalB[lower])
            {
                return lower;
            }

            if (delta == 0)
            {
                continue;
            }

            var upper = preferredOffset + delta;
            if (upper >= minOffset &&
                upper <= maxOffset &&
                upper != excludedOffset &&
                literalA[upper] != literalB[upper])
            {
                return upper;
            }
        }

        return -1;
    }

    private static bool TryParseLiteralGapLiteralBranch(
        Utf8ConstructionSession storage,
        AsciiSimplePatternToken[] tokens,
        out byte[] leadingLiteral,
        out byte[] trailingLiteral,
        out int gapCount,
        out bool gapSameLine)
    {
        leadingLiteral = [];
        trailingLiteral = [];
        gapCount = 0;
        gapSameLine = false;

        var leadingLength = 0;
        while (leadingLength < tokens.Length && tokens[leadingLength].Kind == AsciiSimplePatternTokenKind.Literal)
        {
            Utf8ConstructionScope.ChargeWork(1);
            leadingLength++;
        }

        if (leadingLength == 0 || leadingLength == tokens.Length)
        {
            return false;
        }

        var trailingStart = tokens.Length;
        while (trailingStart > leadingLength && tokens[trailingStart - 1].Kind == AsciiSimplePatternTokenKind.Literal)
        {
            Utf8ConstructionScope.ChargeWork(1);
            trailingStart--;
        }

        if (trailingStart == tokens.Length)
        {
            return false;
        }

        gapCount = trailingStart - leadingLength;
        for (var i = leadingLength; i < trailingStart; i++)
        {
            Utf8ConstructionScope.ChargeWork(1);
            if (tokens[i].Kind != AsciiSimplePatternTokenKind.Dot)
            {
                return false;
            }
        }

        leadingLiteral = storage.Allocate<byte>(leadingLength, false);
        for (var i = 0; i < leadingLength; i++)
        {
            Utf8ConstructionScope.ChargeWork(1);
            leadingLiteral[i] = tokens[i].Literal;
        }

        var trailingLength = tokens.Length - trailingStart;
        trailingLiteral = storage.Allocate<byte>(trailingLength, false);
        for (var i = 0; i < trailingLength; i++)
        {
            Utf8ConstructionScope.ChargeWork(1);
            trailingLiteral[i] = tokens[trailingStart + i].Literal;
        }

        gapSameLine = true;
        return true;
    }

    private static bool TryExtractFullLiteral(Utf8ConstructionSession storage, AsciiSimplePatternToken[] tokens, out byte[] literal)
    {
        literal = storage.Allocate<byte>(tokens.Length, false);
        for (var i = 0; i < tokens.Length; i++)
        {
            Utf8ConstructionScope.ChargeWork(1);
            if (tokens[i].Kind != AsciiSimplePatternTokenKind.Literal)
            {
                literal = [];
                return false;
            }

            literal[i] = tokens[i].Literal;
        }

        return literal.Length > 0;
    }

    private static bool SameLiteral(byte[] left, byte[] right)
    {
        Utf8ConstructionScope.ChargeWork((ulong)Math.Min(left.Length, right.Length));
        return left.AsSpan().SequenceEqual(right);
    }

    private static bool EqualsConcat(byte[] full, byte[] left, byte[] right)
    {
        if ((long)left.Length + right.Length != full.Length) return false;
        Utf8ConstructionScope.ChargeWork((ulong)full.Length);
        return full.AsSpan(0, left.Length).SequenceEqual(left) && full.AsSpan(left.Length).SequenceEqual(right);
    }
}
