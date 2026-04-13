namespace Lokad.Utf8Regex.Internal.Execution;

internal static partial class Utf8AsciiSimplePatternLowerer
{
    internal static bool TryExtractLiteralBranches(AsciiSimplePatternPlan simplePatternPlan, out byte[][] literals)
    {
        literals = [];
        if (simplePatternPlan.IsStartAnchored || simplePatternPlan.IsEndAnchored || simplePatternPlan.Branches.Length == 0)
        {
            return false;
        }

        var expanded = new List<byte[]>(simplePatternPlan.Branches.Length);
        for (var i = 0; i < simplePatternPlan.Branches.Length; i++)
        {
            if (!TryExpandLiteralBranch(simplePatternPlan.Branches[i], simplePatternPlan.IgnoreCase, expanded))
            {
                literals = [];
                return false;
            }
        }

        literals = [.. expanded];
        return true;
    }

    private static bool TryExpandLiteralBranch(IReadOnlyList<AsciiSimplePatternToken> tokens, bool ignoreCase, List<byte[]> destination)
    {
        var current = new List<byte[]> { Array.Empty<byte>() };
        for (var i = 0; i < tokens.Count; i++)
        {
            var token = tokens[i];
            if (token.Kind == AsciiSimplePatternTokenKind.Literal)
            {
                AppendLiteralByte(current, token.Literal);
                continue;
            }

            if (token.Kind == AsciiSimplePatternTokenKind.CharClass &&
                token.CharClass is not null &&
                TryExtractIgnoreCaseLiteral(token.CharClass, out var literal))
            {
                AppendLiteralByte(current, literal);
                continue;
            }

            if (token.Kind == AsciiSimplePatternTokenKind.CharClass &&
                token.CharClass is not null &&
                TryExtractLiteralBytes(token.CharClass, ignoreCase, out var literalBytes))
            {
                if (current.Count * literalBytes.Length > MaxExpandedBranches)
                {
                    return false;
                }

                var expanded = new List<byte[]>(current.Count * literalBytes.Length);
                foreach (var prefix in current)
                {
                    foreach (var value in literalBytes)
                    {
                        var next = new byte[prefix.Length + 1];
                        prefix.CopyTo(next, 0);
                        next[^1] = value;
                        expanded.Add(next);
                    }
                }

                current = expanded;
                continue;
            }

            return false;
        }

        if (destination.Count + current.Count > MaxExpandedBranches)
        {
            return false;
        }

        destination.AddRange(current);
        return true;
    }

    private static void AppendLiteralByte(List<byte[]> literals, byte value)
    {
        for (var i = 0; i < literals.Count; i++)
        {
            var next = new byte[literals[i].Length + 1];
            literals[i].CopyTo(next, 0);
            next[^1] = value;
            literals[i] = next;
        }
    }

    private static bool TryExtractIgnoreCaseLiteral(AsciiCharClass charClass, out byte literal)
    {
        literal = 0;
        var values = charClass.GetPositiveMatchBytes();
        if (values.Length == 0)
        {
            return false;
        }

        var folded = Internal.Utilities.AsciiSearch.FoldCase(values[0]);
        for (var i = 1; i < values.Length; i++)
        {
            if (Internal.Utilities.AsciiSearch.FoldCase(values[i]) != folded)
            {
                return false;
            }
        }

        literal = folded;
        return true;
    }

    private static bool TryExtractLiteralBytes(AsciiCharClass charClass, bool ignoreCase, out byte[] literals)
    {
        literals = [];
        var values = charClass.GetPositiveMatchBytes();
        if (values.Length == 0)
        {
            return false;
        }

        if (!ignoreCase)
        {
            if (values.Length > MaxLiteralCharClassExpansion)
            {
                return false;
            }

            literals = values;
            return true;
        }

        var folded = new HashSet<byte>();
        for (var i = 0; i < values.Length; i++)
        {
            folded.Add(Internal.Utilities.AsciiSearch.FoldCase(values[i]));
            if (folded.Count > MaxLiteralCharClassExpansion)
            {
                return false;
            }
        }

        literals = [.. folded.Order()];
        return true;
    }

    private static void NormalizeBranchesForIgnoreCase(AsciiSimplePatternToken[][] branches)
    {
        foreach (var tokens in branches)
        {
            for (var i = 0; i < tokens.Length; i++)
            {
                var token = tokens[i];
                switch (token.Kind)
                {
                    case AsciiSimplePatternTokenKind.Literal:
                        tokens[i] = new AsciiSimplePatternToken(Internal.Utilities.AsciiSearch.FoldCase(token.Literal));
                        break;

                    case AsciiSimplePatternTokenKind.CharClass when token.CharClass is not null:
                        tokens[i] = new AsciiSimplePatternToken(token.CharClass.ToIgnoreCaseInvariant());
                        break;
                }
            }
        }
    }

    private static void NormalizeFixedChecksForIgnoreCase(AsciiFixedLiteralCheck[] fixedChecks)
    {
        for (var i = 0; i < fixedChecks.Length; i++)
        {
            var literal = fixedChecks[i].Literal;
            var normalized = new byte[literal.Length];
            for (var j = 0; j < literal.Length; j++)
            {
                normalized[j] = Internal.Utilities.AsciiSearch.FoldCase(literal[j]);
            }

            fixedChecks[i] = new AsciiFixedLiteralCheck(fixedChecks[i].Offset, normalized);
        }
    }

    private static void ExtractSearchLiterals(
        AsciiSimplePatternToken[][] branches,
        out int searchLiteralOffset,
        out byte[][] searchLiterals,
        out AsciiFixedLiteralCheck[] fixedLiteralChecks)
    {
        if (branches.Length == 1)
        {
            ExtractSingleBranchSearch(branches[0], out searchLiteralOffset, out searchLiterals, out fixedLiteralChecks);
            return;
        }

        if (TryExtractAlternationPrefixes(branches, out searchLiterals))
        {
            searchLiteralOffset = 0;
            fixedLiteralChecks = [];
            return;
        }

        searchLiteralOffset = 0;
        searchLiterals = [];
        fixedLiteralChecks = [];
    }

    private static void ExtractSingleBranchSearch(
        AsciiSimplePatternToken[] tokens,
        out int searchLiteralOffset,
        out byte[][] searchLiterals,
        out AsciiFixedLiteralCheck[] fixedLiteralChecks)
    {
        var runs = ExtractLiteralRuns(tokens);
        if (runs.Count == 0)
        {
            searchLiteralOffset = 0;
            searchLiterals = [];
            fixedLiteralChecks = [];
            return;
        }

        var bestIndex = 0;
        for (var i = 1; i < runs.Count; i++)
        {
            if (runs[i].Literal.Length > runs[bestIndex].Literal.Length ||
                (runs[i].Literal.Length == runs[bestIndex].Literal.Length && runs[i].Offset > runs[bestIndex].Offset))
            {
                bestIndex = i;
            }
        }

        searchLiteralOffset = runs[bestIndex].Offset;
        searchLiterals = [runs[bestIndex].Literal];

        var checks = new List<AsciiFixedLiteralCheck>(Math.Min(3, runs.Count - 1));
        for (var i = 0; i < runs.Count; i++)
        {
            if (i == bestIndex)
            {
                continue;
            }

            checks.Add(new AsciiFixedLiteralCheck(runs[i].Offset, runs[i].Literal));
            if (checks.Count == 3)
            {
                break;
            }
        }

        fixedLiteralChecks = [.. checks];
    }

    private static bool TryExtractAlternationPrefixes(AsciiSimplePatternToken[][] branches, out byte[][] prefixes)
    {
        prefixes = new byte[branches.Length][];
        for (var i = 0; i < branches.Length; i++)
        {
            var run = ExtractLeadingLiteral(branches[i]);
            if (run.Length == 0)
            {
                prefixes = [];
                return false;
            }

            prefixes[i] = run;
        }

        return true;
    }

    private static List<(int Offset, byte[] Literal)> ExtractLiteralRuns(AsciiSimplePatternToken[] tokens)
    {
        var runs = new List<(int Offset, byte[] Literal)>();
        var currentStart = -1;

        for (var i = 0; i <= tokens.Length; i++)
        {
            var isLiteral = i < tokens.Length && tokens[i].Kind == AsciiSimplePatternTokenKind.Literal;
            if (isLiteral)
            {
                if (currentStart < 0)
                {
                    currentStart = i;
                }

                continue;
            }

            if (currentStart < 0)
            {
                continue;
            }

            var length = i - currentStart;
            var bytes = new byte[length];
            for (var j = 0; j < length; j++)
            {
                bytes[j] = tokens[currentStart + j].Literal;
            }

            runs.Add((currentStart, bytes));
            currentStart = -1;
        }

        return runs;
    }

    private static byte[] ExtractLeadingLiteral(AsciiSimplePatternToken[] tokens)
    {
        var length = 0;
        while (length < tokens.Length && tokens[length].Kind == AsciiSimplePatternTokenKind.Literal)
        {
            length++;
        }

        if (length == 0)
        {
            return [];
        }

        var bytes = new byte[length];
        for (var i = 0; i < length; i++)
        {
            bytes[i] = tokens[i].Literal;
        }

        return bytes;
    }

    private static byte[] ExtractFullLiteral(IReadOnlyList<AsciiSimplePatternToken> tokens)
    {
        if (tokens.Count == 0)
        {
            return [];
        }

        var bytes = new byte[tokens.Count];
        for (var i = 0; i < tokens.Count; i++)
        {
            if (tokens[i].Kind != AsciiSimplePatternTokenKind.Literal)
            {
                return [];
            }

            bytes[i] = tokens[i].Literal;
        }

        return bytes;
    }
}
