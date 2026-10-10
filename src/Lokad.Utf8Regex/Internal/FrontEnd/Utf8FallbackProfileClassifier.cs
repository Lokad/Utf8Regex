using Lokad.Utf8Regex.Internal.Execution;
using static Lokad.Utf8Regex.Internal.FrontEnd.Utf8FallbackTreeQuery;

namespace Lokad.Utf8Regex.Internal.FrontEnd;

// These fixed profiles borrow the owned grammar and retain only value plans.
// Unicode URI predicates require a full language proof; date digits are an
// ASCII intersection whose wider subjects remain with the scalar executor.
internal static class Utf8FallbackProfileClassifier
{
    internal static Utf8FallbackDirectFamilyPlan Classify(Utf8OwnedRegexTree? tree)
    {
        if (tree is null) return default;
        using var scope = Utf8ConstructionScope.Enter();
        if (!Admit(tree.Root, false)) return default;
        var body = Unwrap(tree.Root);
        if (body.Kind != Utf8TreeNodeKind.Concatenate) return default;
        var plan = TryUri(body) ? Utf8FallbackDirectFamilyPlan.ForKind(Utf8FallbackDirectFamilyKind.Utf8UriToken, Utf8FallbackFindModeKind.FindToken) :
            TryIpv4(body) ? Utf8FallbackDirectFamilyPlan.ForKind(Utf8FallbackDirectFamilyKind.AsciiDottedDecimalQuadCount, Utf8FallbackFindModeKind.FindToken) : ClassifyDate(body);
        if (plan.HasValue) scope.Complete();
        return plan;
    }

    private static Utf8FallbackDirectFamilyPlan ClassifyDate(Utf8TreeNode body)
    {
        var first = body.ChildCount > 0 && Unwrap(body.Child(0)).Kind == Utf8TreeNodeKind.Boundary ? 1 : 0;
        var trailing = body.ChildCount > first && Unwrap(body.Child(body.ChildCount - 1)).Kind == Utf8TreeNodeKind.Boundary;
        if (body.ChildCount != first + 5 + (trailing ? 1 : 0) ||
            !TryDateField(body.Child(first), out var aMin, out var aMax) || !TrySeparator(body.Child(first + 1), out var aSeparator) ||
            !TryDateField(body.Child(first + 2), out var bMin, out var bMax) || !TrySeparator(body.Child(first + 3), out var bSeparator) ||
            !TryDateField(body.Child(first + 4), out var cMin, out var cMax)) return default;
        return Utf8FallbackDirectFamilyPlan.ForBoundedDateToken(aMin, aMax, bMin, bMax, cMin, cMax, aSeparator, bSeparator, first != 0, trailing);
    }

    private static bool TryDateField(Utf8TreeNode node, out byte minimum, out byte maximum)
    {
        minimum = maximum = 0;
        if (!TryRepeat(node, out var atom, out var min, out var max) || min <= 0 || max > byte.MaxValue ||
            !TryMask(atom, out var mask) || mask != AsciiCharClass.FromBytes("0123456789"u8)) return false;
        minimum = (byte)min; maximum = (byte)max; return true;
    }

    private static bool TrySeparator(Utf8TreeNode node, out byte separator)
    {
        separator = 0;
        if (!TryLiteral(node, out var literal) || LiteralLength(literal) != 1) return false;
        separator = FirstByte(literal); return separator is (byte)'/' or (byte)'-' or (byte)'.';
    }

    private static bool TryUri(Utf8TreeNode body) => body.ChildCount == 6 &&
        TryRepeat(body.Child(0), out var scheme, out var minimum, out var maximum) && minimum == 1 && maximum == int.MaxValue && IsWord(scheme) &&
        IsLiteral(body.Child(1), "://") && IsUriRun(body.Child(2), 1, "/?#"u8) && IsUriRun(body.Child(3), 1, "?#"u8) &&
        IsOptionalUriTail(body.Child(4), '?', "#"u8) && IsOptionalUriTail(body.Child(5), '#', []);

    private static bool IsOptionalUriTail(Utf8TreeNode source, char marker, ReadOnlySpan<byte> excluded)
    {
        var loop = Unwrap(source);
        if (loop.Kind != Utf8TreeNodeKind.Loop || loop.M != 0 || loop.N != 1 || loop.ChildCount != 1) return false;
        var body = Unwrap(loop.Child(0));
        return body.Kind == Utf8TreeNodeKind.Concatenate && body.ChildCount == 2 && IsByte(body.Child(0), marker) && IsUriRun(body.Child(1), 0, excluded);
    }

    private static bool IsUriRun(Utf8TreeNode node, int requiredMinimum, ReadOnlySpan<byte> excluded)
    {
        if (!TryRepeat(node, out var atom, out var minimum, out var maximum) || minimum != requiredMinimum || maximum != int.MaxValue ||
            atom.ScalarClass is not { } scalarClass) return false;
        var data = scalarClass.Data.AsSpan(); Utf8ConstructionScope.ChargeWork((ulong)data.Length);
        if (data.Length < 4 || data[0] != 1 || data[1] < 0 || (data[1] & 1) != 0 || data[2] != 1 ||
            4L + data[1] != data.Length || data[^1] != 100) return false;
        ulong punct = 0, expected = 0;
        foreach (var value in excluded) expected |= 1ul << value;
        for (var index = 3; index < data.Length - 1; index += 2)
        {
            var start = data[index]; var end = data[index + 1]; if (start < 0 || start >= end || end > 128) return false;
            for (var value = start; value < end; value++)
            {
                Utf8ConstructionScope.ChargeWork(1);
                if (char.IsWhiteSpace((char)value)) continue;
                if (value >= 64 || excluded.IndexOf((byte)value) < 0) return false;
                punct |= 1ul << value;
            }
        }
        return punct == expected;
    }

    private static bool IsWord(Utf8TreeNode atom)
    {
        if (atom.ScalarClass is not { } scalarClass) return false;
        var data = scalarClass.Data.AsSpan(); Utf8ConstructionScope.ChargeWork((ulong)data.Length);
        if (data.Length < 3 || data[0] != 0 || data[1] < 0 || (data[1] & 1) != 0 || data[2] < 0 || 3L + data[1] + data[2] != data.Length) return false;
        uint categories = 0; var inGroup = false;
        foreach (var code in data[(3 + data[1])..])
        {
            if (code == 0) { inGroup = !inGroup; continue; }
            if (code is < 1 or > 30) return false;
            categories |= 1u << code;
        }
        if (inGroup || categories != ((1u << 1) | (1u << 2) | (1u << 3) | (1u << 4) | (1u << 5) | (1u << 6) | (1u << 9) | (1u << 19))) return false;
        for (var index = 3; index < 3 + data[1]; index += 2)
        {
            var start = data[index]; var end = data[index + 1]; if (start < 0 || start >= end || end > 128) return false;
            for (var value = start; value < end; value++)
            { Utf8ConstructionScope.ChargeWork(1); if (!Utf8AsciiBytePredicates.IsWord((byte)value)) return false; }
        }
        return true;
    }

    private static bool TryIpv4(Utf8TreeNode body)
    {
        if (body.ChildCount != 2) return false;
        var loop = Unwrap(body.Child(0));
        if (loop.Kind != Utf8TreeNodeKind.Loop || loop.M != 3 || loop.N != 3 || loop.ChildCount != 1) return false;
        var pair = Unwrap(loop.Child(0));
        return pair.Kind == Utf8TreeNodeKind.Concatenate && pair.ChildCount == 2 && IsOctet(pair.Child(0)) && IsByte(pair.Child(1), '.') && IsOctet(body.Child(1));
    }

    private static bool IsOctet(Utf8TreeNode source)
    {
        var alternate = Unwrap(source);
        // Branch order determines the final octet's greedy endpoint. Equivalent
        // character unions alone cannot qualify this specialized matcher.
        if (alternate.Kind != Utf8TreeNodeKind.Alternate || alternate.ChildCount != 3) return false;
        var high = Unwrap(alternate.Child(0)); var middle = Unwrap(alternate.Child(1)); var low = Unwrap(alternate.Child(2));
        return high.Kind == Utf8TreeNodeKind.Concatenate && high.ChildCount == 2 && IsLiteral(high.Child(0), "25") && IsAsciiSet(high.Child(1), 1, 1, "012345"u8) &&
            middle.Kind == Utf8TreeNodeKind.Concatenate && middle.ChildCount == 3 && IsByte(middle.Child(0), '2') && IsAsciiSet(middle.Child(1), 1, 1, "01234"u8) &&
            IsAsciiSet(middle.Child(2), 1, 1, "0123456789"u8) && low.Kind == Utf8TreeNodeKind.Concatenate && low.ChildCount == 3 &&
            IsAsciiSet(low.Child(0), 0, 1, "01"u8) && IsAsciiSet(low.Child(1), 1, 1, "0123456789"u8) && IsAsciiSet(low.Child(2), 1, 1, "0123456789"u8);
    }

    private static bool IsAsciiSet(Utf8TreeNode node, int min, int max, ReadOnlySpan<byte> values) =>
        TryRepeat(node, out var atom, out var minimum, out var maximum) && minimum == min && maximum == max &&
        Utf8ScalarAsciiProjector.TryProjectPositiveAsciiDefinition(atom.ScalarClass, out var mask) && mask == AsciiCharClass.FromBytes(values);

    private static bool IsByte(Utf8TreeNode node, char value) => TryLiteral(node, out var literal) && LiteralLength(literal) == 1 && FirstByte(literal) == value;
    private static bool IsLiteral(Utf8TreeNode node, string value)
    {
        if (!TryLiteral(node, out var literal) || LiteralLength(literal) != value.Length) return false;
        Utf8ConstructionScope.ChargeWork((ulong)value.Length);
        return literal.Kind == Utf8TreeNodeKind.One ? literal.Ch == value[0] : literal.Str == value;
    }
}
