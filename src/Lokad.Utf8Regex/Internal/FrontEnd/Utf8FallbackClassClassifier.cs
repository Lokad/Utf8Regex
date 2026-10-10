using System.Globalization;
using Lokad.Utf8Regex.Internal.Execution;
using static Lokad.Utf8Regex.Internal.FrontEnd.Utf8FallbackTreeQuery;

namespace Lokad.Utf8Regex.Internal.FrontEnd;

internal static class Utf8FallbackClassClassifier
{
    internal static Utf8FallbackDirectFamilyPlan Classify(Utf8OwnedRegexTree? tree)
    {
        if (tree is null || (tree.Options & (RegexOptions.RightToLeft | RegexOptions.ECMAScript)) != 0) return default;
        using var scope = Utf8ConstructionScope.Enter();
        if (!Admit(tree.Root, true)) return default;
        var body = Unwrap(tree.Root); var result = ClassifyBody(body, tree.Options, scope.Budget);
        if (result.HasValue) scope.Complete();
        return result;
    }

    private static Utf8FallbackDirectFamilyPlan ClassifyBody(Utf8TreeNode body, RegexOptions options, Utf8ConstructionBudget budget)
    {
        if (TryRepeat(body, out var atom, out var minimum, out var maximum))
        {
            if (!atom.MatchCaseInsensitive && minimum == 0 && maximum == int.MaxValue && TryExcludedByte(atom, out var excluded))
                return Utf8FallbackDirectFamilyPlan.ForAsciiUntilByteStarCount(excluded);
            if (IsLetterClass(atom) && minimum > 0 && maximum < int.MaxValue && (options & RegexOptions.NonBacktracking) == 0)
                return minimum == 1 && maximum == 1 ? Utf8FallbackDirectFamilyPlan.ForKind(Utf8FallbackDirectFamilyKind.UnicodeLetterCount) :
                    Utf8FallbackDirectFamilyPlan.ForCountBounds(Utf8FallbackDirectFamilyKind.UnicodeLetterBoundedCount, minimum, maximum);
            if (!atom.MatchCaseInsensitive && minimum == 1 && maximum == 1 && TryCategory(atom, out var category))
                return Utf8FallbackDirectFamilyPlan.ForUnicodeCategory(category);
            if (minimum > 0 && maximum == int.MaxValue && IsWordClass(atom))
                return Utf8FallbackDirectFamilyPlan.ForCountBounds(Utf8FallbackDirectFamilyKind.AsciiWordBoundedCount, minimum);
        }
        if (body.Kind != Utf8TreeNodeKind.Concatenate) return default;
        if (body.ChildCount == 3 && Unwrap(body.Child(0)).Kind == Utf8TreeNodeKind.Boundary && Unwrap(body.Child(2)).Kind == Utf8TreeNodeKind.Boundary &&
            TryRepeat(body.Child(1), out atom, out minimum, out maximum) && minimum > 0 && maximum == int.MaxValue && IsWordClass(atom))
            return Utf8FallbackDirectFamilyPlan.ForCountBounds(Utf8FallbackDirectFamilyKind.AsciiWordBoundedCount, minimum);

        var anchored = body.ChildCount > 0 && Unwrap(body.Child(0)).Kind == Utf8TreeNodeKind.Start;
        var offset = anchored ? 1 : 0;
        if (anchored && body.ChildCount == 2 && TryRepeat(body.Child(1), out atom, out minimum, out maximum) &&
            minimum == 1 && maximum == int.MaxValue && TryMask(atom, out var operators) &&
            operators == AsciiCharClass.FromBytes("=+-^*/.<>~!&|?"u8))
            return Utf8FallbackDirectFamilyPlan.ForKind(Utf8FallbackDirectFamilyKind.AnchoredOperatorRun, Utf8FallbackFindModeKind.MatchAtStart);
        if (body.ChildCount == offset + 2 && TryRepeat(body.Child(offset), out var head, out var headMin, out var headMax) &&
            headMin == 1 && headMax == 1 && TryMask(head, out var headMask) &&
            headMask == AsciiCharClass.FromBytes("abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ"u8) &&
            TryRepeat(body.Child(offset + 1), out var tail, out var tailMin, out var tailMax) && tailMin == 0 && tailMax == int.MaxValue &&
            TryMask(tail, out var tailMask) && tailMask == (anchored ? AsciiCharClass.FromBytes("abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789_"u8) :
                AsciiCharClass.FromBytes("abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789"u8)))
            return Utf8FallbackDirectFamilyPlan.ForKind(anchored ? Utf8FallbackDirectFamilyKind.AnchoredIdentifierPrefix : Utf8FallbackDirectFamilyKind.AsciiIdentifierToken,
                anchored ? Utf8FallbackFindModeKind.MatchAtStart : Utf8FallbackFindModeKind.FindToken);
        if (body.ChildCount == 3 && TryRepeat(body.Child(0), out head, out headMin, out headMax) && headMin == 1 && headMax == int.MaxValue &&
            !head.MatchCaseInsensitive && TryExcludedByte(head, out var headExcluded) && TryLiteral(body.Child(1), out var literal) &&
            !literal.MatchCaseInsensitive && LiteralLength(literal) == 1 && TryRepeat(body.Child(2), out tail, out tailMin, out tailMax) &&
            tailMin == 1 && tailMax == int.MaxValue && !tail.MatchCaseInsensitive && TryExcludedByte(tail, out var tailExcluded))
            return Utf8FallbackDirectFamilyPlan.ForLiteralBetweenNegatedRuns(CopyLiteral(literal, budget), headExcluded, tailExcluded);
        return default;
    }

    private static bool TryExcludedByte(Utf8TreeNode atom, out byte excluded)
    {
        Utf8ConstructionScope.ChargeWork(1); excluded = 0;
        if (atom.Kind is Utf8TreeNodeKind.Notone or Utf8TreeNodeKind.Notoneloop)
        { if (atom.Ch >= 128) return false; excluded = (byte)atom.Ch; return true; }
        if (atom.ScalarClass is not { } scalarClass) return false;
        var data = scalarClass.Data.AsSpan(); Utf8ConstructionScope.ChargeWork((ulong)data.Length);
        if (data.Length != 5 || data[0] != 1 || data[1] != 2 || data[2] != 0 || data[3] is < 0 or >= 128 || data[4] != data[3] + 1) return false;
        excluded = (byte)data[3]; return true;
    }

    private static bool IsLetterClass(Utf8TreeNode atom) => IsPredicateGroup(atom, [0, 2, 4, 5, 3, 1, 0], false);
    private static bool IsWordClass(Utf8TreeNode atom) => IsPredicateGroup(atom, [0, 2, 4, 5, 3, 1, 6, 9, 19, 0], true);

    private static bool IsPredicateGroup(Utf8TreeNode atom, ReadOnlySpan<int> group, bool word)
    {
        if (atom.ScalarClass is not { } scalarClass) return false;
        var data = scalarClass.Data.AsSpan(); Utf8ConstructionScope.ChargeWork((ulong)data.Length);
        if (data.Length < 3 || data[0] != 0 || data[1] < 0 || (data[1] & 1) != 0 || data[2] != group.Length ||
            3L + data[1] + group.Length != data.Length || !data[(3 + data[1])..].SequenceEqual(group)) return false;
        for (var index = 3; index < 3 + data[1]; index += 2)
        {
            var start = data[index]; var end = data[index + 1]; if (start < 0 || end > 128 || start >= end) return false;
            for (var value = start; value < end; value++)
            {
                Utf8ConstructionScope.ChargeWork(1);
                if (!((uint)((value | 32) - 'a') <= 'z' - 'a') && !(word && (value is >= '0' and <= '9' || value == '_'))) return false;
            }
        }
        return true;
    }

    private static bool TryCategory(Utf8TreeNode atom, out UnicodeCategory category)
    {
        category = default;
        if (atom.ScalarClass is not { } scalarClass) return false;
        var data = scalarClass.Data.AsSpan(); Utf8ConstructionScope.ChargeWork((ulong)data.Length);
        if (data.Length != 4 || data[0] != 0 || data[1] != 0 || data[2] != 1 || data[3] is < 1 or > 30) return false;
        category = (UnicodeCategory)(data[3] - 1); return true;
    }
}
