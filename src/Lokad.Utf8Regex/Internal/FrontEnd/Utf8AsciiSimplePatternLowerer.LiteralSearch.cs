using Lokad.Utf8Regex.Internal.Execution;
namespace Lokad.Utf8Regex.Internal.FrontEnd;

internal static partial class Utf8AsciiSimplePatternLowerer
{
    private readonly record struct LiteralRun(int Offset, byte[] Literal);

    internal static bool TryExtractLiteralBranches(AsciiSimplePatternPlan simplePatternPlan, out byte[][] literals)
    {
        literals = [];
        using var construction = Utf8ConstructionScope.Enter();
        var owner = construction.Budget; owner.EnterDepth();
        try
        {
            owner.ChargeWork(1);
            if (simplePatternPlan.IsStartAnchored || simplePatternPlan.IsEndAnchored || simplePatternPlan.Branches.Length == 0) return false;
            using var expanded = Utf8ConstructionBuffer<byte[]>.Create(owner);
            foreach (var branch in simplePatternPlan.Branches)
            {
                owner.ChargeWork(1);
                if (!TryExpandLiteralBranch(branch, simplePatternPlan.IgnoreCase, out var values) ||
                    expanded.Count + values.Length > MaxExpandedBranches) return false;
                // Mutate only the parent's vector, after the speculative child
                // has returned. Failure cannot leave a borrowed vector pointing
                // at capacity whose child loan was rolled back.
                expanded.AddRange(values); owner.ReleaseArray(values, false);
            }
            literals = expanded.Take(); construction.Complete(); return true;
        }
        finally { owner.LeaveDepth(); }
    }

    private static bool TryExpandLiteralBranch(IReadOnlyList<AsciiSimplePatternToken> tokens, bool ignoreCase, out byte[][] literals)
    {
        literals = [];
        using var construction = Utf8ConstructionScope.Enter();
        var owner = construction.Budget; owner.EnterDepth();
        try
        {
            using var current = Utf8ConstructionBuffer<byte[]>.Create(owner);
            current.Add([]);
            for (var index = 0; index < tokens.Count; index++)
            {
                owner.ChargeWork(1);
                var token = tokens[index];
                if (token.Kind == AsciiSimplePatternTokenKind.Literal)
                { AppendLiteralByte(current, token.Literal); continue; }
                // Sensitive [Aa] expands in ascending byte order; invariant
                // folding may collapse it only under the existing folding flag.
                if (ignoreCase && token.Kind == AsciiSimplePatternTokenKind.CharClass &&
                    !token.CharClass.IsEmpty && TryExtractIgnoreCaseLiteral(token.CharClass, out var literal))
                { AppendLiteralByte(current, literal); continue; }
                if (token.Kind != AsciiSimplePatternTokenKind.CharClass || token.CharClass.IsEmpty ||
                    !TryExtractLiteralBytes(token.CharClass, ignoreCase, out var literalBytes) ||
                    current.Count * literalBytes.Length > MaxExpandedBranches) return false;
                using var expanded = Utf8ConstructionBuffer<byte[]>.Create(owner);
                foreach (var prefix in current)
                {
                    owner.ChargeWork(1);
                    foreach (var value in literalBytes)
                    {
                        owner.ChargeWork(1);
                        var next = owner.AllocateArray<byte>(checked(prefix.Length + 1), false);
                        owner.ChargeWork((ulong)prefix.Length + 1);
                        prefix.CopyTo(next, 0); next[^1] = value; expanded.Add(next);
                    }
                }
                foreach (var prefix in current) { Utf8ConstructionScope.ChargeWork(1); owner.ReleaseArray(prefix, false); }
                current.Clear(); current.AddRange(expanded.Span); owner.ReleaseArray(literalBytes, false);
            }
            literals = current.Take(); construction.Complete(); return true;
        }
        finally { owner.LeaveDepth(); }
    }

    private static void AppendLiteralByte(Utf8ConstructionBuffer<byte[]> literals, byte value)
    {
        var owner = Utf8ConstructionScope.CurrentBudget;
        for (var index = 0; index < literals.Count; index++)
        {
            owner.ChargeWork(1);
            var previous = literals[index];
            var next = owner.AllocateArray<byte>(checked(previous.Length + 1), false);
            owner.ChargeWork((ulong)previous.Length + 1);
            previous.CopyTo(next, 0); next[^1] = value; literals[index] = next;
            owner.ReleaseArray(previous, false);
        }
    }

    private static bool TryExtractIgnoreCaseLiteral(AsciiCharClass charClass, out byte literal)
    {
        literal = 0;
        using var construction = Utf8ConstructionScope.Enter();
        var owner = construction.Budget; owner.EnterDepth();
        try
        {
            if (charClass.Negated) return false;
            var found = false;
            for (var value = 0; value < 128; value++)
            {
                owner.ChargeWork(1);
                if (!charClass.Contains((byte)value)) continue;
                var folded = Internal.Search.AsciiSearch.FoldCase((byte)value);
                if (found && folded != literal) return false;
                literal = folded; found = true;
            }
            return found;
        }
        finally { owner.LeaveDepth(); }
    }

    private static bool TryExtractLiteralBytes(AsciiCharClass charClass, bool ignoreCase, out byte[] literals)
    {
        literals = [];
        using var construction = Utf8ConstructionScope.Enter();
        var owner = construction.Budget; owner.EnterDepth();
        try
        {
            if (charClass.Negated) return false;
            ulong low = 0, high = 0; var count = 0;
            for (var value = 0; value < 128; value++)
            {
                owner.ChargeWork(1);
                if (!charClass.Contains((byte)value)) continue;
                var folded = ignoreCase ? Internal.Search.AsciiSearch.FoldCase((byte)value) : (byte)value;
                var bit = 1ul << (folded & 63);
                if (((folded < 64 ? low : high) & bit) != 0) continue;
                if (++count > MaxLiteralCharClassExpansion) return false;
                if (folded < 64) low |= bit; else high |= bit;
            }
            if (count == 0) return false;
            var result = owner.AllocateArray<byte>(count, false); var position = 0;
            for (var value = 0; value < 128; value++)
            {
                owner.ChargeWork(1);
                if (((value < 64 ? low : high) & (1ul << (value & 63))) != 0) result[position++] = (byte)value;
            }
            literals = result; construction.Complete(); return true;
        }
        finally { owner.LeaveDepth(); }
    }

    private static void NormalizeBranchesForIgnoreCase(AsciiSimplePatternToken[][] branches)
    {
        foreach (var tokens in branches)
        {
            Utf8ConstructionScope.ChargeWork(1);
            for (var i = 0; i < tokens.Length; i++)
            {
                Utf8ConstructionScope.ChargeWork(1);
                var token = tokens[i];
                switch (token.Kind)
                {
                    case AsciiSimplePatternTokenKind.Literal:
                        tokens[i] = new AsciiSimplePatternToken(Internal.Search.AsciiSearch.FoldCase(token.Literal));
                        break;

                    case AsciiSimplePatternTokenKind.CharClass when !token.CharClass.IsEmpty:
                        tokens[i] = new AsciiSimplePatternToken(
                            token.CharClass.ToIgnoreCaseInvariant(),
                            token.RequiresAsciiInput,
                            token.ScalarClassKind);
                        break;
                }
            }
        }
    }

    private static void NormalizeFixedChecksForIgnoreCase(Utf8ConstructionSession storage, AsciiFixedLiteralCheck[] fixedChecks)
    {
        for (var i = 0; i < fixedChecks.Length; i++)
        {
            Utf8ConstructionScope.ChargeWork(1);
            var literal = fixedChecks[i].Literal;
            var normalized = storage.Allocate<byte>(literal.Length, false);
            for (var j = 0; j < literal.Length; j++)
            {
                Utf8ConstructionScope.ChargeWork(1);
                normalized[j] = Internal.Search.AsciiSearch.FoldCase(literal[j]);
            }

            fixedChecks[i] = new AsciiFixedLiteralCheck(fixedChecks[i].Offset, normalized);
        }
    }

    private static void ExtractSearchLiterals(
        Utf8ConstructionSession storage,
        AsciiSimplePatternToken[][] branches,
        out int searchLiteralOffset,
        out byte[][] searchLiterals,
        out AsciiFixedLiteralCheck[] fixedLiteralChecks)
    {
        if (branches.Length == 1)
        {
            ExtractSingleBranchSearch(storage, branches[0], out searchLiteralOffset, out searchLiterals, out fixedLiteralChecks);
            return;
        }

        if (TryExtractAlternationPrefixes(storage, branches, out searchLiterals))
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
        Utf8ConstructionSession storage,
        AsciiSimplePatternToken[] tokens,
        out int searchLiteralOffset,
        out byte[][] searchLiterals,
        out AsciiFixedLiteralCheck[] fixedLiteralChecks)
    {
        var runs = ExtractLiteralRuns(storage, tokens);
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
            Utf8ConstructionScope.ChargeWork(1);
            if (runs[i].Literal.Length > runs[bestIndex].Literal.Length ||
                (runs[i].Literal.Length == runs[bestIndex].Literal.Length && runs[i].Offset > runs[bestIndex].Offset))
            {
                bestIndex = i;
            }
        }

        searchLiteralOffset = runs[bestIndex].Offset;
        searchLiterals = storage.Allocate<byte[]>(1, false);
        searchLiterals[0] = runs[bestIndex].Literal;

        var checks = storage.Buffer<AsciiFixedLiteralCheck>();
        for (var i = 0; i < runs.Count; i++)
        {
            Utf8ConstructionScope.ChargeWork(1);
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

        fixedLiteralChecks = storage.Copy(checks, false);
    }

    private static bool TryExtractAlternationPrefixes(Utf8ConstructionSession storage, AsciiSimplePatternToken[][] branches, out byte[][] prefixes)
    {
        prefixes = storage.Allocate<byte[]>(branches.Length, false);
        for (var i = 0; i < branches.Length; i++)
        {
            Utf8ConstructionScope.ChargeWork(1);
            var run = ExtractLeadingLiteral(storage, branches[i]);
            if (run.Length == 0)
            {
                prefixes = [];
                return false;
            }

            prefixes[i] = run;
        }

        return true;
    }

    private static Utf8ConstructionBuffer<LiteralRun> ExtractLiteralRuns(Utf8ConstructionSession storage, AsciiSimplePatternToken[] tokens)
    {
        var runs = storage.Buffer<LiteralRun>();
        var currentStart = -1;

        for (var i = 0; i <= tokens.Length; i++)
        {
            Utf8ConstructionScope.ChargeWork(1);
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
            var bytes = storage.Allocate<byte>(length, false);
            for (var j = 0; j < length; j++)
            {
                Utf8ConstructionScope.ChargeWork(1);
                bytes[j] = tokens[currentStart + j].Literal;
            }

            runs.Add(new LiteralRun(currentStart, bytes));
            currentStart = -1;
        }

        return runs;
    }

    private static byte[] ExtractLeadingLiteral(Utf8ConstructionSession storage, AsciiSimplePatternToken[] tokens)
    {
        var length = 0;
        while (length < tokens.Length && tokens[length].Kind == AsciiSimplePatternTokenKind.Literal)
        {
            Utf8ConstructionScope.ChargeWork(1);
            length++;
        }

        if (length == 0)
        {
            return [];
        }

        var bytes = storage.Allocate<byte>(length, false);
        for (var i = 0; i < length; i++)
        {
            Utf8ConstructionScope.ChargeWork(1);
            bytes[i] = tokens[i].Literal;
        }

        return bytes;
    }

    private static byte[] ExtractFullLiteral(Utf8ConstructionSession storage, ReadOnlySpan<AsciiSimplePatternToken> tokens)
    {
        if (tokens.Length == 0)
        {
            return [];
        }

        var bytes = storage.Allocate<byte>(tokens.Length, false);
        for (var i = 0; i < tokens.Length; i++)
        {
            Utf8ConstructionScope.ChargeWork(1);
            if (tokens[i].Kind != AsciiSimplePatternTokenKind.Literal)
            {
                return [];
            }

            bytes[i] = tokens[i].Literal;
        }

        return bytes;
    }
}
