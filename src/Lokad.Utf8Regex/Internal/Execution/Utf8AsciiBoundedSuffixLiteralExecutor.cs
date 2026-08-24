using System.Buffers;
using System.Text;
using Lokad.Utf8Regex.Internal.Diagnostics;

namespace Lokad.Utf8Regex.Internal.Execution;

internal static class Utf8AsciiBoundedSuffixLiteralExecutor
{
    public static bool IsMatch(ReadOnlySpan<byte> input, AsciiSimplePatternBoundedSuffixLiteralPlan plan, Utf8ExecutionDeadline budget)
    {
        return FindNext(input, plan, 0, out _, out _, budget) >= 0;
    }

    public static int Count(ReadOnlySpan<byte> input, AsciiSimplePatternBoundedSuffixLiteralPlan plan, Utf8ExecutionDeadline budget)
    {
        Utf8SearchDiagnosticsSession.Current?.MarkExecutionRoute(Utf8ExecutionRoute.NativeAsciiBoundedSuffixLiteral);
        var count = 0;
        var startIndex = 0;
        while (startIndex <= input.Length)
        {
            var matchIndex = FindNext(input, plan, startIndex, out var matchedLength, out _, budget);
            if (matchIndex < 0)
            {
                break;
            }

            count++;
            startIndex = matchIndex + Math.Max(matchedLength, 1);
        }

        return count;
    }

    public static Utf8ValueMatch Match(ReadOnlySpan<byte> input, AsciiSimplePatternBoundedSuffixLiteralPlan plan, Utf8ExecutionDeadline budget)
    {
        var index = FindNext(input, plan, 0, out var matchedLength, out _, budget);
        return index < 0
            ? Utf8ValueMatch.NoMatch
            : new Utf8ValueMatch(true, true, index, matchedLength, index, matchedLength);
    }

    public static int FindNext(
        ReadOnlySpan<byte> input,
        AsciiSimplePatternBoundedSuffixLiteralPlan plan,
        int startIndex,
        out int matchedLength,
        out int literalIndex,
        Utf8ExecutionDeadline budget)
    {
        matchedLength = 0;
        literalIndex = -1;

        if (!plan.HasValue || startIndex < 0 || startIndex >= input.Length)
        {
            return -1;
        }

        var literal = plan.LiteralUtf8;
        if (literal.Length == 0 || input.Length < literal.Length + 1)
        {
            return -1;
        }

        var searchFrom = Math.Max(startIndex, 0);
        var latestLiteralEnd = input.Length - 2;
        var literalSuffixOffset = literal.Length - 1;
        var candidateSearchStart = Math.Max(searchFrom + literalSuffixOffset, literalSuffixOffset);
        while (candidateSearchStart <= latestLiteralEnd)
        {
            var relative = input[candidateSearchStart..].IndexOf(plan.LiteralLastByte);
            if (relative < 0)
            {
                return -1;
            }

            var literalEndIndex = candidateSearchStart + relative;
            candidateSearchStart = literalEndIndex + 1;
            if (!TryMatchScalarClassAt(
                    input,
                    literalEndIndex + 1,
                    plan.SuffixCharClass,
                    plan.SuffixScalarClassKind,
                    out var suffixByteLength))
            {
                continue;
            }

            var candidateLiteralIndex = literalEndIndex - literalSuffixOffset;
            if (candidateLiteralIndex < searchFrom ||
                !input.Slice(candidateLiteralIndex, literal.Length).SequenceEqual(literal))
            {
                continue;
            }

            Utf8SearchDiagnosticsSession.Current?.CountSearchCandidate();
            budget.Step();

            Utf8SearchDiagnosticsSession.Current?.CountVerifierInvocation();
            if (!TryMatchAt(
                    input,
                    plan,
                    candidateLiteralIndex,
                    suffixByteLength,
                    startIndex,
                    out var candidateStart,
                    out matchedLength))
            {
                continue;
            }

            Utf8SearchDiagnosticsSession.Current?.CountVerifierMatch();
            literalIndex = candidateLiteralIndex;
            return candidateStart;
        }

        return -1;
    }

    private static bool TryMatchAt(
        ReadOnlySpan<byte> input,
        AsciiSimplePatternBoundedSuffixLiteralPlan plan,
        int literalIndex,
        int suffixByteLength,
        int minStartIndex,
        out int matchStart,
        out int matchedLength)
    {
        matchStart = -1;
        matchedLength = 0;

        var index = literalIndex - 1;
        var repeatedCount = 0;
        while (index >= minStartIndex &&
            repeatedCount < plan.RepeatedMaxLength &&
            plan.RepeatedCharClass.Contains(input[index]))
        {
            repeatedCount++;
            index--;
        }

        if (repeatedCount < plan.RepeatedMinLength || index < minStartIndex)
        {
            return false;
        }

        var prefixEnd = index + 1;
        if (!TryMatchScalarClassBefore(
                input,
                prefixEnd,
                plan.PrefixCharClass,
                plan.PrefixScalarClassKind,
                out var prefixByteLength))
        {
            return false;
        }

        matchStart = prefixEnd - prefixByteLength;
        if (matchStart < minStartIndex)
        {
            return false;
        }

        matchedLength = prefixByteLength + repeatedCount + plan.LiteralUtf8.Length + suffixByteLength;
        return true;
    }

    private static bool TryMatchScalarClassAt(
        ReadOnlySpan<byte> input,
        int byteOffset,
        AsciiCharClass asciiClass,
        Utf8SimplePatternScalarClassKind scalarClassKind,
        out int byteLength)
    {
        var first = input[byteOffset];
        if (first < 0x80)
        {
            byteLength = 1;
            return asciiClass.Contains(first);
        }

        if (scalarClassKind != Utf8SimplePatternScalarClassKind.UnicodeWhitespace ||
            Rune.DecodeFromUtf8(input[byteOffset..], out var scalar, out byteLength) != OperationStatus.Done)
        {
            byteLength = 0;
            return false;
        }

        return Rune.IsWhiteSpace(scalar);
    }

    private static bool TryMatchScalarClassBefore(
        ReadOnlySpan<byte> input,
        int byteOffset,
        AsciiCharClass asciiClass,
        Utf8SimplePatternScalarClassKind scalarClassKind,
        out int byteLength)
    {
        var last = input[byteOffset - 1];
        if (last < 0x80)
        {
            byteLength = 1;
            return asciiClass.Contains(last);
        }

        if (scalarClassKind != Utf8SimplePatternScalarClassKind.UnicodeWhitespace ||
            Rune.DecodeLastFromUtf8(input[..byteOffset], out var scalar, out byteLength) != OperationStatus.Done)
        {
            byteLength = 0;
            return false;
        }

        return Rune.IsWhiteSpace(scalar);
    }
}
