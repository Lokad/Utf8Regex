using System.Runtime.CompilerServices;
using Lokad.Utf8Regex.Internal.Planning;

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

        if (!AsciiStructuralSuffixMatcher.TryConsumeSetLoop(input, ref index, plan.SeparatorSet, plan.SeparatorCharClass, plan.SeparatorMinCount))
        {
            return false;
        }

        var tailStart = -1;
        var tailEnd = -1;

        if (!string.IsNullOrEmpty(plan.IdentifierStartSet))
        {
            if ((uint)index >= (uint)input.Length || !AsciiStructuralSuffixMatcher.MatchesSet(input[index], plan.IdentifierStartSet, plan.IdentifierStartCharClass))
            {
                return false;
            }

            index++;
            tailStart = index;
            while ((uint)index < (uint)input.Length &&
                   AsciiStructuralSuffixMatcher.MatchesSet(input[index], plan.IdentifierTailSet, plan.IdentifierTailCharClass) &&
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
                if (!AsciiStructuralSuffixMatcher.TryMatchAfterTail(input, tailStart + plan.IdentifierTailMinCount, tailEnd, plan.CompiledSuffixParts, out index))
                {
                    return false;
                }
            }
        }
        else if (plan.CompiledSuffixParts.Length > 0)
        {
            if (!AsciiStructuralSuffixMatcher.TryMatch(input, index, plan.CompiledSuffixParts, out index))
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
        if (!AsciiStructuralSuffixMatcher.TryConsumeSetLoop(input, ref index, plan.SeparatorSet, plan.SeparatorCharClass, plan.SeparatorMinCount))
        {
            return false;
        }

        if ((uint)index >= (uint)input.Length || !AsciiStructuralSuffixMatcher.MatchesSet(input[index], plan.IdentifierStartSet, plan.IdentifierStartCharClass))
        {
            return false;
        }

        index++;
        var tailStart = index;
        while ((uint)index < (uint)input.Length &&
               AsciiStructuralSuffixMatcher.MatchesSet(input[index], plan.IdentifierTailSet, plan.IdentifierTailCharClass) &&
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
        if (!AsciiStructuralSuffixMatcher.TryConsumeSetLoop(input, ref index, plan.SeparatorSet, plan.SeparatorCharClass, plan.SeparatorMinCount))
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
            if (!AsciiStructuralSuffixMatcher.TryConsumeSetLoop(input, ref index, parts[0].SeparatorSet, parts[0].SeparatorCharClass, parts[0].SeparatorMinCount))
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
        if (!AsciiStructuralSuffixMatcher.TryConsumeSetLoop(input, ref index, plan.SeparatorSet, plan.SeparatorCharClass, plan.SeparatorMinCount))
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

    // Ordered-window kernels keep this owner-local forwarding name at their hot call sites;
    // the boundary truth itself is centralized in DotNetUtf8WordBoundary.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool MatchesBoundaryRequirement(Utf8BoundaryRequirement requirement, ReadOnlySpan<byte> input, int byteOffset)
    {
        return DotNetUtf8WordBoundary.MatchesRequirement(requirement, input, byteOffset);
    }

    internal static bool IsAsciiWordByte(byte value) => Utf8AsciiBytePredicates.IsWord(value);

    internal static bool IsAsciiUpper(byte value) => value is >= (byte)'A' and <= (byte)'Z';
}
