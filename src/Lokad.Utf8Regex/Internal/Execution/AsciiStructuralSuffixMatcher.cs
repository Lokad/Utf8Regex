using System.Runtime.CompilerServices;
using RuntimeFrontEnd = Lokad.Utf8Regex.Internal.FrontEnd.Runtime;

namespace Lokad.Utf8Regex.Internal.Execution;

/// <summary>
/// Owns the shared structural-suffix semantics used by both verifier programs and specialized family matchers.
/// </summary>
internal static class AsciiStructuralSuffixMatcher
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryConsumeSetLoop(
        ReadOnlySpan<byte> input,
        ref int index,
        string? set,
        AsciiCharClass charClass,
        int minCount)
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

    public static bool TryMatch(
        ReadOnlySpan<byte> input,
        int startIndex,
        ReadOnlySpan<AsciiStructuralCompiledSuffixPart> suffixParts,
        out int endIndex)
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
            if (literal.Length == 0 ||
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

    public static bool TryMatchAfterTail(
        ReadOnlySpan<byte> input,
        int searchStart,
        int tailEnd,
        ReadOnlySpan<AsciiStructuralCompiledSuffixPart> suffixParts,
        out int endIndex)
    {
        endIndex = tailEnd;

        // Empty suffixes are successful no-ops. Verifier factories omit such instructions,
        // but keeping the policy here prevents specialized callers from drifting.
        if (suffixParts.Length == 0)
        {
            return true;
        }

        if (suffixParts[0].IsSeparator)
        {
            return TryMatch(input, tailEnd, suffixParts, out endIndex);
        }

        var firstLiteral = suffixParts[0].LiteralUtf8;
        if (firstLiteral.Length == 0)
        {
            return false;
        }

        for (var start = tailEnd - firstLiteral.Length; start >= searchStart; start--)
        {
            if (TryMatch(input, start, suffixParts, out endIndex))
            {
                return true;
            }
        }

        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool MatchesSet(byte value, string runtimeSet, AsciiCharClass charClass)
    {
        return !charClass.IsEmpty
            ? charClass.Contains(value)
            : value < 128 && RuntimeFrontEnd.RegexCharClass.CharInClassBase((char)value, runtimeSet);
    }
}
