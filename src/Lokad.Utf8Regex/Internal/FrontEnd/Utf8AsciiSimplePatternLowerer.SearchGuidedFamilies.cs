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

    private static bool SameLiteral(byte[] left, byte[] right)
    {
        Utf8ConstructionScope.ChargeWork((ulong)Math.Min(left.Length, right.Length));
        return left.AsSpan().SequenceEqual(right);
    }
}
