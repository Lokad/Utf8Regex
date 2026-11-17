using Lokad.Utf8Regex.Internal.Execution;
namespace Lokad.Utf8Regex.Internal.FrontEnd;

internal static partial class Utf8FiniteLiteralLowerer
{

    internal static bool TryExpandLiteralBranches(AsciiPatternToken[][] branches, bool ignoreCase, out byte[][] literals)
    {
        literals = [];
        using var construction = Utf8ConstructionScope.Enter();
        var owner = construction.Budget; owner.EnterDepth();
        try
        {
            owner.ChargeWork(1);
            if (branches.Length == 0) return false;
            using var expanded = Utf8ConstructionBuffer<byte[]>.Create(owner);
            foreach (var branch in branches)
            {
                owner.ChargeWork(1);
                if (!TryExpandLiteralBranch(branch, ignoreCase, out var values) ||
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

    private static bool TryExpandLiteralBranch(ReadOnlySpan<AsciiPatternToken> tokens, bool ignoreCase, out byte[][] literals)
    {
        literals = [];
        using var construction = Utf8ConstructionScope.Enter();
        var owner = construction.Budget; owner.EnterDepth();
        try
        {
            using var current = Utf8ConstructionBuffer<byte[]>.Create(owner);
            current.Add([]);
            for (var index = 0; index < tokens.Length; index++)
            {
                owner.ChargeWork(1);
                var token = tokens[index];
                if (token.Kind == AsciiPatternTokenKind.Literal)
                {
                    var end = index + 1;
                    while (end < tokens.Length && tokens[end].Kind == AsciiPatternTokenKind.Literal)
                    { owner.ChargeWork(1); end++; }
                    if (end == index + 1) AppendLiteralByte(current, token.Literal);
                    else AppendLiteralRun(current, tokens[index..end]);
                    index = end - 1;
                    continue;
                }
                // Sensitive [Aa] expands in ascending byte order; invariant
                // folding may collapse it only under the existing folding flag.
                if (ignoreCase && token.Kind == AsciiPatternTokenKind.CharClass &&
                    !token.CharClass.IsEmpty && TryExtractIgnoreCaseLiteral(token.CharClass, out var literal))
                { AppendLiteralByte(current, literal); continue; }
                if (token.Kind != AsciiPatternTokenKind.CharClass || token.CharClass.IsEmpty ||
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

    private static void AppendLiteralRun(Utf8ConstructionBuffer<byte[]> literals, ReadOnlySpan<AsciiPatternToken> tokens)
    {
        var owner = Utf8ConstructionScope.CurrentBudget;
        for (var index = 0; index < literals.Count; index++)
        {
            owner.ChargeWork(1);
            var previous = literals[index];
            // Fund the complete append while its old prefix is still held.
            // Adjacent literal tokens need one acquisition, not every prefix.
            var next = owner.AllocateArray<byte>(checked(previous.Length + tokens.Length), false);
            owner.ChargeWork((ulong)previous.Length + (ulong)tokens.Length);
            for (var offset = 0; offset < previous.Length;)
            {
                owner.ChargeWork(0);
                var count = Math.Min(2048, previous.Length - offset);
                previous.AsSpan(offset, count).CopyTo(next.AsSpan(offset, count));
                offset += count;
            }
            for (var offset = 0; offset < tokens.Length;)
            {
                owner.ChargeWork(0);
                var end = offset + Math.Min(2048, tokens.Length - offset);
                for (; offset < end; offset++) next[previous.Length + offset] = tokens[offset].Literal;
            }
            owner.ChargeWork(0);
            literals[index] = next;
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

    private static void NormalizeBranchesForIgnoreCase(AsciiPatternToken[][] branches)
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
                    case AsciiPatternTokenKind.Literal:
                        tokens[i] = new AsciiPatternToken(Internal.Search.AsciiSearch.FoldCase(token.Literal));
                        break;

                    case AsciiPatternTokenKind.CharClass when !token.CharClass.IsEmpty:
                        tokens[i] = new AsciiPatternToken(token.CharClass.ToIgnoreCaseInvariant());
                        break;
                }
            }
        }
    }

}
