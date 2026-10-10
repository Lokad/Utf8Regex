using Lokad.Utf8Regex.Internal.Search;

namespace Lokad.Utf8Regex.Internal.Execution;

internal static class Utf8AsciiAnchoredUrlExecutor
{
    public static bool TryMatchDigitsQueryWhole(ReadOnlySpan<byte> input, in Utf8FallbackDirectFamilyPlan plan, out int matchedLength) =>
        TryMatchWhole(input, plan, false, out matchedLength);
    public static bool TryMatchHexQueryWhole(ReadOnlySpan<byte> input, in Utf8FallbackDirectFamilyPlan plan, out int matchedLength) =>
        TryMatchWhole(input, plan, true, out matchedLength);

    private static bool TryMatchWhole(ReadOnlySpan<byte> input, in Utf8FallbackDirectFamilyPlan plan, bool hex, out int matchedLength)
    {
        matchedLength = 0;
        // A required negative value class may consume LF, including final LF.
        // Dot tails exclude LF, while the end anchor can leave one final LF.
        if (TryPrefixes(input, plan, hex)) { matchedLength = input.Length; return true; }
        if (!input.IsEmpty && input[^1] == (byte)'\n' && TryPrefixes(input[..^1], plan, hex))
        { matchedLength = input.Length - 1; return true; }
        return false;
    }

    private static bool TryPrefixes(ReadOnlySpan<byte> input, in Utf8FallbackDirectFamilyPlan plan, bool hex)
    {
        var dots = plan.Url.PrefixWildcards;
        return TryPrefix(input, plan.PrimaryPrefixUtf8, dots.Primary, plan, hex) ||
            TryPrefix(input, plan.SecondaryPrefixUtf8, dots.Secondary, plan, hex) ||
            TryPrefix(input, plan.RelativePrefixUtf8, dots.Relative, plan, hex);
    }

    private static bool TryPrefix(ReadOnlySpan<byte> input, ReadOnlySpan<byte> prefix, UInt128 dots, in Utf8FallbackDirectFamilyPlan plan, bool hex)
    {
        if (prefix.IsEmpty || input.Length < prefix.Length) return false;
        var firstLf = input.IndexOf((byte)'\n'); var leadingEnd = firstLf < 0 ? input.Length : firstLf;
        var fixedWidth = 0;
        while (fixedWidth < prefix.Length && ((dots >> fixedWidth) & 1) == 0) fixedWidth++;
        if (fixedWidth == 0) return false; // The owned proof requires a fixed first byte.
        var searchFrom = 0;
        while (searchFrom <= input.Length - fixedWidth)
        {
            var found = AsciiSearch.IndexOfIgnoreCase(input[searchFrom..], prefix[..fixedWidth]);
            if (found < 0) return false;
            var candidate = searchFrom + found;
            if (candidate > leadingEnd) return false;
            if (input.Length - candidate < prefix.Length) return false;
            var matched = true;
            for (var index = fixedWidth; index < prefix.Length; index++)
            {
                var current = input[candidate + index];
                if (((dots >> index) & 1) != 0 ? current == (byte)'\n' :
                    AsciiSearch.FoldCase(current) != AsciiSearch.FoldCase(prefix[index])) { matched = false; break; }
            }
            if (matched && TrySuffix(input, candidate + prefix.Length, plan, hex)) return true;
            searchFrom = candidate + 1;
        }
        return false;
    }

    private static bool TrySuffix(ReadOnlySpan<byte> input, int index, in Utf8FallbackDirectFamilyPlan plan, bool hex)
    {
        // The optional /[A-Za-z0-9]+ must backtrack to absence if the route
        // itself begins with an alphanumeric segment. No route is hard-coded.
        var original = index;
        if (index < input.Length && input[index] == (byte)'/')
        {
            var start = ++index;
            while (index < input.Length && IsLetterOrDigit(input[index])) index++;
            if (index > start && TrySuffixCore(input, index, plan, hex)) return true;
        }
        return TrySuffixCore(input, original, plan, hex);
    }

    private static bool TrySuffixCore(ReadOnlySpan<byte> input, int index, in Utf8FallbackDirectFamilyPlan plan, bool hex)
    {
        if (!ConsumeLiteral(input, plan.RouteMarkerUtf8, ref index)) return false;
        if (!hex)
        {
            var start = index;
            while (index < input.Length && Utf8AsciiBytePredicates.IsDigit(input[index])) index++;
            if (index == start) return false;
            if (index < input.Length && input[index] == (byte)'/') index++;
            return TryRequired(input, index, plan.RequiredParameterUtf8, true);
        }
        while (index < input.Length && IsHex(input[index])) index++;
        var absent = index;
        if (!plan.OptionalParameterUtf8.AsSpan().IsEmpty && ConsumeParameter(input, plan.OptionalParameterUtf8, false, ref index))
        {
            var start = index;
            while (index < input.Length && input[index] is not ((byte)'&') and not ((byte)' ') and not ((byte)'\n')) index++;
            // '?' belongs to this value class. Greedy optional values must
            // return it to a following required ?name= marker when necessary.
            for (var end = index; end > start; end--)
                if (TryRequired(input, end, plan.RequiredParameterUtf8, false)) return true;
        }
        return TryRequired(input, absent, plan.RequiredParameterUtf8, false);
    }

    private static bool TryRequired(ReadOnlySpan<byte> input, int index, ReadOnlySpan<byte> name, bool questionOnly)
    {
        if (!ConsumeParameter(input, name, questionOnly, ref index)) return false;
        var start = index;
        while (index < input.Length && input[index] != (byte)' ' && input[index] != (questionOnly ? (byte)'?' : (byte)'&')) index++;
        if (index == start) return false;
        // The optional post-value parameter is contained in the non-LF dot
        // tail's language. Capture projection still executes the owned tree.
        return input[index..].IndexOf((byte)'\n') < 0;
    }

    private static bool ConsumeParameter(ReadOnlySpan<byte> input, ReadOnlySpan<byte> name, bool questionOnly, ref int index)
    {
        if (name.IsEmpty || index >= input.Length || (questionOnly ? input[index] != (byte)'?' : input[index] is not ((byte)'?') and not ((byte)'&'))) return false;
        index++;
        if (!ConsumeLiteral(input, name, ref index) || index >= input.Length || input[index] != (byte)'=') return false;
        index++; return true;
    }
    private static bool ConsumeLiteral(ReadOnlySpan<byte> input, ReadOnlySpan<byte> literal, ref int index)
    {
        if (input.Length - index < literal.Length || !AsciiSearch.MatchesIgnoreCase(input.Slice(index, literal.Length), literal)) return false;
        index += literal.Length; return true;
    }
    private static bool IsLetterOrDigit(byte value)
    { value = AsciiSearch.FoldCase(value); return (uint)(value - (byte)'a') <= 'z' - 'a' || Utf8AsciiBytePredicates.IsDigit(value); }
    private static bool IsHex(byte value)
    { value = AsciiSearch.FoldCase(value); return Utf8AsciiBytePredicates.IsDigit(value) || (uint)(value - (byte)'a') <= 'f' - 'a'; }
}
