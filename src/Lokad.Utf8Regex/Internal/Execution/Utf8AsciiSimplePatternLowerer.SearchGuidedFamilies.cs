namespace Lokad.Utf8Regex.Internal.Execution;

using Lokad.Utf8Regex.Internal.Utilities;

internal static partial class Utf8AsciiSimplePatternLowerer
{
    private static bool TryExtractBoundedSuffixLiteralPlan(
        AsciiSimplePatternToken[][] branches,
        bool isStartAnchored,
        bool isEndAnchored,
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
            firstBranch[0].CharClass is not { } prefixCharClass ||
            firstBranch[^1].Kind != AsciiSimplePatternTokenKind.CharClass ||
            firstBranch[^1].CharClass is not { } suffixCharClass ||
            !TryExtractTrailingLiteral(firstBranch, out var literalStart, out var literalUtf8) ||
            literalStart < 1 ||
            !TryGetUniformRepeatedClass(firstBranch, 1, literalStart, out var repeatedCharClass))
        {
            return false;
        }

        var minLength = literalStart - 1;
        var maxLength = minLength;
        for (var i = 1; i < branches.Length; i++)
        {
            var branch = branches[i];
            if (branch.Length < literalUtf8.Length + 2 ||
                branch[0].Kind != AsciiSimplePatternTokenKind.CharClass ||
                branch[0].CharClass is not { } nextPrefix ||
                !prefixCharClass.HasSameDefinition(nextPrefix) ||
                branch[^1].Kind != AsciiSimplePatternTokenKind.CharClass ||
                branch[^1].CharClass is not { } nextSuffix ||
                !suffixCharClass.HasSameDefinition(nextSuffix) ||
                !TryExtractTrailingLiteral(branch, out var nextLiteralStart, out var nextLiteralUtf8) ||
                !nextLiteralUtf8.AsSpan().SequenceEqual(literalUtf8))
            {
                return false;
            }

            var repeatedLength = nextLiteralStart - 1;
            if (repeatedLength > 0)
            {
                if (!TryGetUniformRepeatedClass(branch, 1, nextLiteralStart, out var nextRepeatedCharClass) ||
                    !repeatedCharClass.HasSameDefinition(nextRepeatedCharClass))
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
            suffixCharClass);
        return plan.HasValue;
    }

    private static bool TryExtractTrailingLiteral(
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
            start--;
        }

        if (start == endExclusive)
        {
            return false;
        }

        literalStart = start;
        literalUtf8 = new byte[endExclusive - start];
        for (var i = 0; i < literalUtf8.Length; i++)
        {
            literalUtf8[i] = tokens[start + i].Literal;
        }

        return true;
    }

    private static bool TryExtractSymmetricLiteralWindowPlan(
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
        var direction0Gaps = new HashSet<int>();
        var direction1Gaps = new HashSet<int>();

        for (var i = 0; i < branches.Length; i++)
        {
            if (TryParseLiteralGapLiteralBranch(branches[i], out var leadingLiteral, out var trailingLiteral, out var branchGapCount, out var branchGapSameLine))
            {
                if (literalA is null)
                {
                    literalA = leadingLiteral;
                    literalB = trailingLiteral;
                    minGap = branchGapCount;
                    maxGap = branchGapCount;
                    gapSameLine = branchGapSameLine;
                    direction0Gaps.Add(branchGapCount);
                    continue;
                }

                if (branchGapSameLine != gapSameLine)
                {
                    return false;
                }

                minGap = Math.Min(minGap, branchGapCount);
                maxGap = Math.Max(maxGap, branchGapCount);

                if (leadingLiteral.AsSpan().SequenceEqual(literalA) &&
                    trailingLiteral.AsSpan().SequenceEqual(literalB))
                {
                    direction0Gaps.Add(branchGapCount);
                    continue;
                }

                if (leadingLiteral.AsSpan().SequenceEqual(literalB) &&
                    trailingLiteral.AsSpan().SequenceEqual(literalA))
                {
                    direction1Gaps.Add(branchGapCount);
                    continue;
                }

                return false;
            }

            if (!TryExtractFullLiteral(branches[i], out var fullLiteral))
            {
                return false;
            }

            if (literalA is null || literalB is null)
            {
                return false;
            }

            if (fullLiteral.AsSpan().SequenceEqual(Concat(literalA, literalB)))
            {
                direction0Gaps.Add(0);
                minGap = Math.Min(minGap, 0);
                maxGap = Math.Max(maxGap, 0);
                continue;
            }

            if (fullLiteral.AsSpan().SequenceEqual(Concat(literalB, literalA)))
            {
                direction1Gaps.Add(0);
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
            if (!direction0Gaps.Contains(gap) || !direction1Gaps.Contains(gap))
            {
                return false;
            }
        }

        var anchorOffset = SelectSymmetricWindowAnchorOffset(literalA, literalB);
        SelectSymmetricWindowFilterOffsets(literalA, literalB, anchorOffset, out var firstFilterOffset, out var secondFilterOffset);
        plan = new AsciiSimplePatternSymmetricLiteralWindowPlan(
            literalA,
            literalB,
            AsciiSearch.CreateExactLiteralSearchData([literalA, literalB]),
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
        int excludedOffset = -1)
    {
        var maxOffset = Math.Min(literalA.Length, literalB.Length) - 1;
        for (var delta = 0; delta <= maxOffset; delta++)
        {
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
            leadingLength++;
        }

        if (leadingLength == 0 || leadingLength == tokens.Length)
        {
            return false;
        }

        var trailingStart = tokens.Length;
        while (trailingStart > leadingLength && tokens[trailingStart - 1].Kind == AsciiSimplePatternTokenKind.Literal)
        {
            trailingStart--;
        }

        if (trailingStart == tokens.Length)
        {
            return false;
        }

        gapCount = trailingStart - leadingLength;
        for (var i = leadingLength; i < trailingStart; i++)
        {
            if (tokens[i].Kind != AsciiSimplePatternTokenKind.Dot)
            {
                return false;
            }
        }

        leadingLiteral = new byte[leadingLength];
        for (var i = 0; i < leadingLength; i++)
        {
            leadingLiteral[i] = tokens[i].Literal;
        }

        var trailingLength = tokens.Length - trailingStart;
        trailingLiteral = new byte[trailingLength];
        for (var i = 0; i < trailingLength; i++)
        {
            trailingLiteral[i] = tokens[trailingStart + i].Literal;
        }

        gapSameLine = true;
        return true;
    }

    private static bool TryExtractFullLiteral(AsciiSimplePatternToken[] tokens, out byte[] literal)
    {
        literal = new byte[tokens.Length];
        for (var i = 0; i < tokens.Length; i++)
        {
            if (tokens[i].Kind != AsciiSimplePatternTokenKind.Literal)
            {
                literal = [];
                return false;
            }

            literal[i] = tokens[i].Literal;
        }

        return literal.Length > 0;
    }

    private static byte[] Concat(byte[] left, byte[] right)
    {
        var combined = new byte[left.Length + right.Length];
        left.CopyTo(combined, 0);
        right.CopyTo(combined, left.Length);
        return combined;
    }
}
