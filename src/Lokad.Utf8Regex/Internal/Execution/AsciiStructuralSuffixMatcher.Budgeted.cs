namespace Lokad.Utf8Regex.Internal.Execution;

internal static partial class AsciiStructuralSuffixMatcher
{
    internal static bool TryConsumeSetLoop(ReadOnlySpan<byte> input, ref int index,
        string? set, AsciiCharClass charClass, int minCount,
        ref Utf8SliceInvocation invocation, out Utf8ExecutionLimitKind breach)
    {
        breach = invocation.TryChargeInputStep();
        if (breach != Utf8ExecutionLimitKind.None) return false;
        if (string.IsNullOrEmpty(set)) return true;
        var count = 0;
        while ((uint)index < (uint)input.Length && MatchesSet(input[index], set, charClass))
        {
            if ((count & 2047) == 0)
            {
                breach = invocation.TryChargeInputStep();
                if (breach != Utf8ExecutionLimitKind.None) return false;
            }
            index++;
            count++;
        }
        return count >= minCount;
    }

    internal static bool TryMatch(ReadOnlySpan<byte> input, int startIndex,
        ReadOnlySpan<AsciiStructuralCompiledSuffixPart> suffixParts,
        ref Utf8SliceInvocation invocation, out int endIndex, out Utf8ExecutionLimitKind breach)
    {
        endIndex = startIndex;
        breach = invocation.TryChargeInputStep();
        if (breach != Utf8ExecutionLimitKind.None) return false;
        var index = startIndex;
        foreach (var part in suffixParts)
        {
            breach = invocation.TryChargeInputStep();
            if (breach != Utf8ExecutionLimitKind.None) return false;
            if (part.IsSeparator)
            {
                if (!TryConsumeSetLoop(input, ref index, part.SeparatorSet, part.SeparatorCharClass,
                    part.SeparatorMinCount, ref invocation, out breach)) return false;
                continue;
            }
            var literal = part.LiteralUtf8;
            if (literal.Length == 0 || input.Length - index < literal.Length) return false;
            for (var offset = 0; offset < literal.Length;)
            {
                breach = invocation.TryChargeInputStep();
                if (breach != Utf8ExecutionLimitKind.None) return false;
                var length = Math.Min(2048, literal.Length - offset);
                if (!input.Slice(index + offset, length).SequenceEqual(literal.AsSpan(offset, length))) return false;
                offset += length;
            }
            index += literal.Length;
        }
        endIndex = index;
        return true;
    }

    internal static bool TryMatchAfterTail(ReadOnlySpan<byte> input, int searchStart, int tailEnd,
        ReadOnlySpan<AsciiStructuralCompiledSuffixPart> suffixParts,
        ref Utf8SliceInvocation invocation, out int endIndex, out Utf8ExecutionLimitKind breach)
    {
        endIndex = tailEnd;
        breach = invocation.TryChargeInputStep();
        if (breach != Utf8ExecutionLimitKind.None) return false;
        if (suffixParts.Length == 0) return true;
        if (suffixParts[0].IsSeparator)
            return TryMatch(input, tailEnd, suffixParts, ref invocation, out endIndex, out breach);
        var firstLiteral = suffixParts[0].LiteralUtf8;
        if (firstLiteral.Length == 0) return false;
        for (var start = tailEnd - firstLiteral.Length; start >= searchStart; start--)
        {
            if (TryMatch(input, start, suffixParts, ref invocation, out endIndex, out breach)) return true;
            if (breach != Utf8ExecutionLimitKind.None) return false;
        }
        return false;
    }
}
