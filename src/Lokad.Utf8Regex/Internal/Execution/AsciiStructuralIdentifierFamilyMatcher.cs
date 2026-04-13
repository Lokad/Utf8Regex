using RuntimeFrontEnd = Lokad.Utf8Regex.Internal.FrontEnd.Runtime;

namespace Lokad.Utf8Regex.Internal.Execution;

internal static class AsciiStructuralIdentifierFamilyMatcher
{
    public static bool TryMatch(ReadOnlySpan<byte> input, int matchIndex, int prefixLength, in AsciiStructuralIdentifierFamilyPlan plan, out int matchedLength)
    {
        matchedLength = 0;
        var index = matchIndex + prefixLength;

        if (string.IsNullOrEmpty(plan.IdentifierStartSet) &&
            plan.SeparatorMinCount == 0 &&
            TryMatchSimpleSuffixOnlyFast(input, matchIndex, index, plan, out matchedLength))
        {
            return true;
        }

        if (plan.HasAsciiUpperWordTailKernel &&
            TryMatchAsciiUpperWordTail(input, matchIndex, index, plan, out matchedLength))
        {
            return true;
        }

        if (!TryConsumeSetLoop(input, ref index, plan.SeparatorSet, plan.SeparatorCharClass, plan.SeparatorMinCount))
        {
            return false;
        }

        var tailStart = -1;
        var tailEnd = -1;

        if (!string.IsNullOrEmpty(plan.IdentifierStartSet))
        {
            if ((uint)index >= (uint)input.Length || !MatchesSet(input[index], plan.IdentifierStartSet, plan.IdentifierStartCharClass))
            {
                return false;
            }

            index++;
            tailStart = index;
            while ((uint)index < (uint)input.Length &&
                   MatchesSet(input[index], plan.IdentifierTailSet, plan.IdentifierTailCharClass) &&
                   index - tailStart < plan.IdentifierTailMaxCount)
            {
                index++;
            }

            tailEnd = index;
            if (tailEnd - tailStart < plan.IdentifierTailMinCount)
            {
                return false;
            }

            if (plan.CompiledSuffixParts.Length > 0)
            {
                if (!TryMatchSuffixPartsAfterTail(input, tailStart + plan.IdentifierTailMinCount, tailEnd, plan.CompiledSuffixParts, out index))
                {
                    return false;
                }
            }
        }
        else if (plan.CompiledSuffixParts.Length > 0)
        {
            if (!TryMatchSuffixParts(input, index, plan.CompiledSuffixParts, out index))
            {
                return false;
            }
        }

        if (!MatchesBoundaryRequirement(plan.TrailingBoundary, input, index))
        {
            return false;
        }

        matchedLength = index - matchIndex;
        return true;
    }

    internal static bool TryMatchIdentifierTailOnly(
        ReadOnlySpan<byte> input,
        int matchIndex,
        int prefixLength,
        in AsciiStructuralIdentifierFamilyPlan plan,
        out int matchedLength)
    {
        matchedLength = 0;
        if (string.IsNullOrEmpty(plan.IdentifierStartSet) || plan.CompiledSuffixParts.Length != 0)
        {
            return false;
        }

        if (plan.HasAsciiUpperWordTailKernel)
        {
            return TryMatchAsciiUpperWordTail(input, matchIndex, matchIndex + prefixLength, plan, out matchedLength);
        }

        var index = matchIndex + prefixLength;
        if (!TryConsumeSetLoop(input, ref index, plan.SeparatorSet, plan.SeparatorCharClass, plan.SeparatorMinCount))
        {
            return false;
        }

        if ((uint)index >= (uint)input.Length || !MatchesSet(input[index], plan.IdentifierStartSet, plan.IdentifierStartCharClass))
        {
            return false;
        }

        index++;
        var tailStart = index;
        while ((uint)index < (uint)input.Length &&
               MatchesSet(input[index], plan.IdentifierTailSet, plan.IdentifierTailCharClass) &&
               index - tailStart < plan.IdentifierTailMaxCount)
        {
            index++;
        }

        if (index - tailStart < plan.IdentifierTailMinCount)
        {
            return false;
        }

        if (!MatchesBoundaryRequirement(plan.TrailingBoundary, input, index))
        {
            return false;
        }

        matchedLength = index - matchIndex;
        return true;
    }

    internal static bool TryMatchSimpleSuffix(
        ReadOnlySpan<byte> input,
        int matchIndex,
        int afterPrefix,
        in AsciiStructuralIdentifierFamilyPlan plan,
        out int matchedLength)
    {
        matchedLength = 0;
        var index = afterPrefix;

        // Consume the family plan's separator (e.g., \s*) before the suffix parts.
        if (!TryConsumeSetLoop(input, ref index, plan.SeparatorSet, plan.SeparatorCharClass, plan.SeparatorMinCount))
        {
            return false;
        }

        return TryMatchSimpleSuffixOnlyFast(input, matchIndex, index, plan, out matchedLength);
    }

    private static bool TryMatchSimpleSuffixOnlyFast(
        ReadOnlySpan<byte> input,
        int matchIndex,
        int index,
        in AsciiStructuralIdentifierFamilyPlan plan,
        out int matchedLength)
    {
        matchedLength = 0;
        var parts = plan.CompiledSuffixParts;
        if (parts.Length == 0)
        {
            if (!MatchesBoundaryRequirement(plan.TrailingBoundary, input, index))
            {
                return false;
            }

            matchedLength = index - matchIndex;
            return true;
        }

        if (parts.Length == 1 && parts[0].IsLiteral)
        {
            var literal = parts[0].LiteralUtf8;
            if (literal is null ||
                input.Length - index < literal.Length ||
                !input.Slice(index, literal.Length).SequenceEqual(literal))
            {
                return false;
            }

            index += literal.Length;
            if (!MatchesBoundaryRequirement(plan.TrailingBoundary, input, index))
            {
                return false;
            }

            matchedLength = index - matchIndex;
            return true;
        }

        if (parts.Length == 2 && parts[0].IsSeparator && parts[1].IsLiteral)
        {
            if (!TryConsumeSetLoop(input, ref index, parts[0].SeparatorSet, parts[0].SeparatorCharClass, parts[0].SeparatorMinCount))
            {
                return false;
            }

            var literal = parts[1].LiteralUtf8;
            if (literal is null ||
                input.Length - index < literal.Length ||
                !input.Slice(index, literal.Length).SequenceEqual(literal))
            {
                return false;
            }

            index += literal.Length;
            if (!MatchesBoundaryRequirement(plan.TrailingBoundary, input, index))
            {
                return false;
            }

            matchedLength = index - matchIndex;
            return true;
        }

        return false;
    }

    private static bool TryMatchAsciiUpperWordTail(
        ReadOnlySpan<byte> input,
        int matchIndex,
        int afterPrefix,
        in AsciiStructuralIdentifierFamilyPlan plan,
        out int matchedLength)
    {
        matchedLength = 0;
        var index = afterPrefix;
        if (!TryConsumeSetLoop(input, ref index, plan.SeparatorSet, plan.SeparatorCharClass, plan.SeparatorMinCount))
        {
            return false;
        }

        if ((uint)index >= (uint)input.Length || !IsAsciiUpper(input[index]))
        {
            return false;
        }

        index++;
        var tailStart = index;
        while ((uint)index < (uint)input.Length &&
               IsAsciiWordByte(input[index]) &&
               index - tailStart < plan.IdentifierTailMaxCount)
        {
            index++;
        }

        if (index - tailStart < plan.IdentifierTailMinCount ||
            !MatchesBoundaryRequirement(plan.TrailingBoundary, input, index))
        {
            return false;
        }

        matchedLength = index - matchIndex;
        return true;
    }

    private static bool TryConsumeSetLoop(ReadOnlySpan<byte> input, ref int index, string? set, AsciiCharClass? charClass, int minCount)
    {
        if (string.IsNullOrEmpty(set))
        {
            return true;
        }

        var count = 0;
        while ((uint)index < (uint)input.Length && MatchesSet(input[index], set, charClass))
        {
            index++;
            count++;
        }

        return count >= minCount;
    }

    private static bool TryMatchSuffixParts(ReadOnlySpan<byte> input, int startIndex, ReadOnlySpan<AsciiStructuralCompiledSuffixPart> suffixParts, out int endIndex)
    {
        endIndex = startIndex;
        var index = startIndex;

        for (var i = 0; i < suffixParts.Length; i++)
        {
            var part = suffixParts[i];
            if (part.IsSeparator)
            {
                if (!TryConsumeSetLoop(input, ref index, part.SeparatorSet, part.SeparatorCharClass, part.SeparatorMinCount))
                {
                    return false;
                }

                continue;
            }

            var literal = part.LiteralUtf8;
            if (literal is null ||
                literal.Length == 0 ||
                input.Length - index < literal.Length ||
                !input.Slice(index, literal.Length).SequenceEqual(literal))
            {
                return false;
            }

            index += literal.Length;
        }

        endIndex = index;
        return true;
    }

    private static bool TryMatchSuffixPartsAfterTail(
        ReadOnlySpan<byte> input,
        int searchStart,
        int tailEnd,
        ReadOnlySpan<AsciiStructuralCompiledSuffixPart> suffixParts,
        out int endIndex)
    {
        endIndex = tailEnd;
        if (suffixParts.Length == 0)
        {
            return true;
        }

        if (suffixParts[0].IsSeparator)
        {
            return TryMatchSuffixParts(input, tailEnd, suffixParts, out endIndex);
        }

        var firstLiteral = suffixParts[0].LiteralUtf8;
        if (firstLiteral is null || firstLiteral.Length == 0)
        {
            return false;
        }

        for (var start = tailEnd - firstLiteral.Length; start >= searchStart; start--)
        {
            if (TryMatchSuffixParts(input, start, suffixParts, out endIndex))
            {
                return true;
            }
        }

        return false;
    }

    private static bool MatchesSet(byte value, string runtimeSet, AsciiCharClass? charClass)
    {
        return charClass is not null
            ? charClass.Contains(value)
            : value < 128 && RuntimeFrontEnd.RegexCharClass.CharInClassBase((char)value, runtimeSet);
    }

    internal static bool MatchesBoundaryRequirement(Utf8BoundaryRequirement requirement, ReadOnlySpan<byte> input, int byteOffset)
    {
        return requirement switch
        {
            Utf8BoundaryRequirement.None => true,
            Utf8BoundaryRequirement.Boundary => IsWordBoundary(input, byteOffset),
            Utf8BoundaryRequirement.NonBoundary => !IsWordBoundary(input, byteOffset),
            _ => false,
        };
    }

    internal static bool IsAsciiWordByte(byte value) => RuntimeFrontEnd.RegexCharClass.IsBoundaryWordChar((char)value);

    internal static bool IsAsciiUpper(byte value) => value is >= (byte)'A' and <= (byte)'Z';

    private static bool IsWordBoundary(ReadOnlySpan<byte> input, int byteOffset)
    {
        var previousIsWord = byteOffset > 0 &&
            IsAsciiWordByte(input[byteOffset - 1]);
        var nextIsWord = byteOffset < input.Length &&
            IsAsciiWordByte(input[byteOffset]);
        return previousIsWord != nextIsWord;
    }
}
