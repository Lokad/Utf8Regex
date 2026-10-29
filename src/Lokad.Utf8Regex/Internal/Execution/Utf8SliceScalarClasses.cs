using System.Globalization;

namespace Lokad.Utf8Regex.Internal.Execution;

/// <summary>Character predicates over complete Unicode scalars. BMP block ranges
/// remain BMP-only; general categories and shorthands cover every scalar plane.</summary>
internal static class Utf8SliceScalarClasses
{
    // Borrowed static ASCII word table; no per-parser or per-program copy.
    internal static ReadOnlySpan<byte> WordCharAsciiLookup =>
    [
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
        1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 0, 0, 0, 0, 0, 0,
        0, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1,
        1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 0, 0, 0, 0, 1,
        0, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1,
        1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 0, 0, 0, 0, 0,
    ];

    private const int WordCategories =
        (1 << (int)UnicodeCategory.UppercaseLetter) |
        (1 << (int)UnicodeCategory.LowercaseLetter) |
        (1 << (int)UnicodeCategory.TitlecaseLetter) |
        (1 << (int)UnicodeCategory.ModifierLetter) |
        (1 << (int)UnicodeCategory.OtherLetter) |
        (1 << (int)UnicodeCategory.NonSpacingMark) |
        (1 << (int)UnicodeCategory.DecimalDigitNumber) |
        (1 << (int)UnicodeCategory.ConnectorPunctuation);

    internal static bool IsWord(Rune scalar, bool includeJoiners, bool ecma)
    {
        if (ecma)
            return scalar.Value is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9' or '_' or 0x0130;
        return (WordCategories & (1 << (int)Rune.GetUnicodeCategory(scalar))) != 0 ||
            includeJoiners && scalar.Value is 0x200C or 0x200D;
    }

    internal static bool IsBoundaryWord(Rune scalar, Utf8SliceWordKind kind)
        => kind == Utf8SliceWordKind.Ascii
            ? (uint)scalar.Value < 128 && WordCharAsciiLookup[scalar.Value] != 0
            : IsWord(scalar, true, kind == Utf8SliceWordKind.Ecma);

    internal static bool IsBoundaryWord(Rune scalar, Utf8SliceProgram program, in Utf8SliceInstruction instruction)
        => instruction.C == 0 ? IsBoundaryWord(scalar, (Utf8SliceWordKind)instruction.B)
            : program.CategorySetMembers[instruction.C - 1][0].Table.ScalarPredicate!(scalar.Value);

    internal static bool MatchesShorthand(Rune scalar, Utf8SliceShorthand shorthand)
    {
        var hit = ((int)shorthand >> 1) switch
        {
            0 => Rune.GetUnicodeCategory(scalar) == UnicodeCategory.DecimalDigitNumber,
            1 => IsWord(scalar, false, false),
            _ => Rune.IsWhiteSpace(scalar),
        };
        return hit != (((int)shorthand & 1) != 0);
    }

    internal static bool MatchesTable(Rune scalar, Utf8SliceCategoryTable table, bool fold, bool negated,
        Utf8ScalarCaseBehavior behavior, ref Utf8SliceInvocation invocation, out Utf8ExecutionLimitKind breach)
    {
        breach = Utf8ExecutionLimitKind.None;
        if (table.ScalarPredicate is { } predicate) return predicate(scalar.Value) != negated;
        var category = Rune.GetUnicodeCategory(scalar);
        var maskHit = fold && category is UnicodeCategory.LowercaseLetter or UnicodeCategory.UppercaseLetter or UnicodeCategory.TitlecaseLetter
            ? (table.Mask & Utf8SliceCategories.CasedLetterMask) != 0
            : ((table.Mask >> (int)category) & 1u) != 0;
        if (maskHit) return !negated;
        var rangeHit = Utf8SliceCaseEquivalences.RangesHit(table.Ranges, scalar.Value, ref invocation, out breach);
        if (breach != Utf8ExecutionLimitKind.None) return false;
        return rangeHit != negated || fold && table.Ranges.Length != 0 &&
            Utf8SliceCaseEquivalences.EquivalentsHitRanges(scalar, table.Ranges, negated, behavior, ref invocation, out breach);
    }

    internal static bool MatchesTables(Rune scalar, ReadOnlySpan<Utf8SliceNode.CategorySetMember> members,
        bool fold, Utf8ScalarCaseBehavior behavior, ref Utf8SliceInvocation invocation, out Utf8ExecutionLimitKind breach)
    {
        breach = Utf8ExecutionLimitKind.None;
        for (var index = 0; index < members.Length; index++)
        {
            // The atom/segment covers its first 32 tables. Check the deadline
            // directly for further blocks, like other bounded buffer scans.
            if (index != 0 && (index & 31) == 0)
            {
                breach = invocation.TryChargeInputStep();
                if (breach != Utf8ExecutionLimitKind.None) return false;
            }
            var member = members[index];
            var hit = MatchesTable(scalar, member.Table, fold, member.Negated, behavior, ref invocation, out breach);
            if (breach != Utf8ExecutionLimitKind.None) return false;
            if (hit) return true;
        }
        return false;
    }

    internal static bool MatchesMask(Rune scalar, byte[] mask, bool fold, Utf8ScalarCaseBehavior behavior)
    {
        var value = scalar.Value;
        if (value < 128 && (mask[value >> 3] & (1 << (value & 7))) != 0) return true;
        if (!fold || !scalar.IsBmp) return false;
        foreach (var equivalent in Utf8SliceCaseEquivalences.GetEquivalents((char)value, behavior))
            if (equivalent < 128 && (mask[equivalent >> 3] & (1 << (equivalent & 7))) != 0) return true;
        return false;
    }

    internal static bool MatchesMembers(Rune scalar, int shorthandBits)
    {
        if (shorthandBits == 0) return false;
        for (var bit = 0; bit < 6; bit++)
            if ((shorthandBits & (1 << bit)) != 0 && MatchesShorthand(scalar, (Utf8SliceShorthand)bit)) return true;
        return false;
    }
}
