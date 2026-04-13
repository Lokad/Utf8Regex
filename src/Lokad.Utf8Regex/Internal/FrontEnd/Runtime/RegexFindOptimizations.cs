using System.Globalization;

namespace Lokad.Utf8Regex.Internal.FrontEnd.Runtime;

internal sealed class RegexFindOptimizations
{
    private readonly string[]? _alternatePrefixes;
    private readonly string? _leadingLiteral;
    private readonly string? _longestLiteral;
    private readonly (string Literal, int Distance)? _fixedDistanceLiteral;
    private readonly FixedDistanceSet[]? _fixedDistanceSets;

    public RegexFindOptimizations(RegexNode root, RegexOptions options, CultureInfo? culture = null)
    {
        Root = root;
        Options = options;
        Culture = culture;
        MinRequiredLength = ComputeMinLength(root);
        MaxPossibleLength = ComputeMaxLength(root);
        LeadingAnchor = FindLeadingAnchor(root);
        TrailingAnchor = FindTrailingAnchor(root);
        _leadingLiteral = TryExtractLeadingLiteral(root);
        _longestLiteral = TryExtractLongestLiteral(root);
        _fixedDistanceLiteral = TryExtractFixedDistanceLiteral(root);
        _fixedDistanceSets = TryExtractFixedDistanceSets(root);
        _alternatePrefixes = RegexPrefixAnalyzer.FindPrefixes(root, (options & RegexOptions.IgnoreCase) != 0) ?? TryExtractAlternatePrefixes(root);
    }

    public RegexNode Root { get; }

    public RegexOptions Options { get; }

    public CultureInfo? Culture { get; }

    public int MinRequiredLength { get; }

    public int? MaxPossibleLength { get; }

    public RegexNodeKind LeadingAnchor { get; }

    public RegexNodeKind TrailingAnchor { get; }

    public string? LeadingLiteral => _leadingLiteral;

    public string? LongestLiteral => _longestLiteral;

    public string[]? AlternatePrefixes => _alternatePrefixes;

    public (string Literal, int Distance)? FixedDistanceLiteral => _fixedDistanceLiteral;

    public FixedDistanceSet[]? FixedDistanceSets => _fixedDistanceSets;

    public bool IsFixedLength => MaxPossibleLength is int max && max == MinRequiredLength;

    public readonly record struct FixedDistanceSet(string Set, int Distance, bool Negated, char[]? Chars, (char LowInclusive, char HighInclusive)? Range);

    private static string? TryExtractLeadingLiteral(RegexNode node)
    {
        if (node.Kind is RegexNodeKind.Capture or RegexNodeKind.Group && node.ChildCount == 1)
        {
            return TryExtractLeadingLiteral(node.Child(0));
        }

        if (TryGetLiteralText(node, out var nodeLiteral))
        {
            return nodeLiteral;
        }

        if (node.Kind != RegexNodeKind.Concatenate)
        {
            return null;
        }

        for (var i = 0; i < node.ChildCount; i++)
        {
            var child = node.Child(i);
            if (TryGetLiteralText(child, out var childLiteral))
            {
                return childLiteral;
            }

            if (child.Kind is RegexNodeKind.Bol or RegexNodeKind.Eol)
            {
                continue;
            }

            return null;
        }

        return null;
    }

    private static int ComputeMinLength(RegexNode node)
    {
        return node.Kind switch
        {
            RegexNodeKind.One or RegexNodeKind.Notone or RegexNodeKind.Set => 1,
            RegexNodeKind.Multi when node.Str is not null => node.Str.Length,
            RegexNodeKind.Oneloop or RegexNodeKind.Onelazy or RegexNodeKind.Oneloopatomic or
            RegexNodeKind.Notoneloop or RegexNodeKind.Notonelazy or RegexNodeKind.Notoneloopatomic or
            RegexNodeKind.Setloop or RegexNodeKind.Setlazy or RegexNodeKind.Setloopatomic => node.M,
            RegexNodeKind.Loop or RegexNodeKind.Lazyloop when node.ChildCount == 1 => node.M * ComputeMinLength(node.Child(0)),
            RegexNodeKind.Capture or RegexNodeKind.Group or RegexNodeKind.Atomic when node.ChildCount == 1 => ComputeMinLength(node.Child(0)),
            RegexNodeKind.Concatenate => ComputeConcatenationMinLength(node),
            RegexNodeKind.Alternate => ComputeAlternationMinLength(node),
            _ => 0,
        };
    }

    private static int? ComputeMaxLength(RegexNode node)
    {
        return node.Kind switch
        {
            RegexNodeKind.One or RegexNodeKind.Notone or RegexNodeKind.Set => 1,
            RegexNodeKind.Multi when node.Str is not null => node.Str.Length,
            RegexNodeKind.Oneloop or RegexNodeKind.Onelazy or RegexNodeKind.Oneloopatomic or
            RegexNodeKind.Notoneloop or RegexNodeKind.Notonelazy or RegexNodeKind.Notoneloopatomic or
            RegexNodeKind.Setloop or RegexNodeKind.Setlazy or RegexNodeKind.Setloopatomic => node.N == int.MaxValue ? null : node.N,
            RegexNodeKind.Loop or RegexNodeKind.Lazyloop when node.ChildCount == 1 => MultiplyMax(node.N, ComputeMaxLength(node.Child(0))),
            RegexNodeKind.Capture or RegexNodeKind.Group or RegexNodeKind.Atomic when node.ChildCount == 1 => ComputeMaxLength(node.Child(0)),
            RegexNodeKind.Concatenate => ComputeConcatenationMaxLength(node),
            RegexNodeKind.Alternate => ComputeAlternationMaxLength(node),
            _ => 0,
        };
    }

    private static RegexNodeKind FindLeadingAnchor(RegexNode node)
    {
        while (node.Kind is RegexNodeKind.Capture or RegexNodeKind.Group or RegexNodeKind.Atomic && node.ChildCount == 1)
        {
            node = node.Child(0);
        }

        if (node.Kind is RegexNodeKind.Beginning or RegexNodeKind.Start or RegexNodeKind.EndZ or RegexNodeKind.End or RegexNodeKind.Bol)
        {
            return node.Kind;
        }

        if (node.Kind == RegexNodeKind.Concatenate && node.ChildCount > 0)
        {
            return FindLeadingAnchor(node.Child(0));
        }

        return RegexNodeKind.Unknown;
    }

    private static RegexNodeKind FindTrailingAnchor(RegexNode node)
    {
        while (node.Kind is RegexNodeKind.Capture or RegexNodeKind.Group or RegexNodeKind.Atomic && node.ChildCount == 1)
        {
            node = node.Child(0);
        }

        if (node.Kind is RegexNodeKind.End or RegexNodeKind.EndZ or RegexNodeKind.Eol)
        {
            return node.Kind;
        }

        if (node.Kind == RegexNodeKind.Concatenate && node.ChildCount > 0)
        {
            return FindTrailingAnchor(node.Child(node.ChildCount - 1));
        }

        return RegexNodeKind.Unknown;
    }

    private static (string Literal, int Distance)? TryExtractFixedDistanceLiteral(RegexNode node)
    {
        return TryExtractFixedDistanceLiteralCore(node, 0, out var bestLiteral, out var bestDistance)
            ? (bestLiteral, bestDistance)
            : null;
    }

    private static FixedDistanceSet[]? TryExtractFixedDistanceSets(RegexNode node)
    {
        var sets = new List<FixedDistanceSet>();
        return TryExtractFixedDistanceSetsCore(node, 0, sets) && sets.Count > 0
            ? [.. sets]
            : null;
    }

    private static bool TryExtractFixedDistanceSetsCore(RegexNode node, int offset, List<FixedDistanceSet> sets)
    {
        switch (node.Kind)
        {
            case RegexNodeKind.Capture:
            case RegexNodeKind.Group:
            case RegexNodeKind.Atomic:
                return node.ChildCount == 1 && TryExtractFixedDistanceSetsCore(node.Child(0), offset, sets);

            case RegexNodeKind.Concatenate:
                var currentOffset = offset;
                for (var i = 0; i < node.ChildCount; i++)
                {
                    var child = node.Child(i);
                    if (!TryExtractFixedDistanceSetsCore(child, currentOffset, sets))
                    {
                        return false;
                    }

                    currentOffset += ComputeMinLength(child);
                }

                return true;

            case RegexNodeKind.Set:
                if (!TryProjectAsciiSet(node.Str, offset, out var projectedSet))
                {
                    return false;
                }

                sets.Add(projectedSet);
                return true;

            case RegexNodeKind.One:
            case RegexNodeKind.Multi:
            case RegexNodeKind.Bol:
            case RegexNodeKind.Eol:
            case RegexNodeKind.Beginning:
            case RegexNodeKind.Start:
            case RegexNodeKind.End:
            case RegexNodeKind.EndZ:
            case RegexNodeKind.Empty:
                return true;

            default:
                return false;
        }
    }

    private static bool TryProjectAsciiSet(string? set, int distance, out FixedDistanceSet projectedSet)
    {
        projectedSet = default;
        if (set is null || !RegexCharClass.IsAscii(set))
        {
            return false;
        }

        if (TryProjectPredefinedAsciiSet(set, distance, out projectedSet))
        {
            return true;
        }

        var negated = RegexCharClass.IsNegated(set);
        if (RegexCharClass.TryGetSingleRange(set, out var first, out var last) &&
            first <= 0x7F &&
            last <= 0x7F)
        {
            projectedSet = new FixedDistanceSet(set, distance, negated, Chars: null, Range: (first, last));
            return true;
        }

        if (!RegexCharClass.CanEasilyEnumerateSetContents(set))
        {
            return false;
        }

        var chars = RegexCharClass.GetSetChars(set)
            .Where(static ch => ch <= 0x7F)
            .Distinct()
            .Order()
            .ToArray();
        if (chars.Length == 0)
        {
            return false;
        }

        projectedSet = new FixedDistanceSet(set, distance, negated, chars, Range: null);
        return true;
    }

    private static bool TryProjectPredefinedAsciiSet(string set, int distance, out FixedDistanceSet projectedSet)
    {
        projectedSet = set switch
        {
            RegexCharClass.DigitClass or RegexCharClass.ECMADigitClass =>
                new FixedDistanceSet(set, distance, Negated: false, Chars: null, Range: ('0', '9')),
            RegexCharClass.NotDigitClass or RegexCharClass.NotECMADigitClass =>
                new FixedDistanceSet(set, distance, Negated: true, Chars: null, Range: ('0', '9')),
            RegexCharClass.SpaceClass or RegexCharClass.ECMASpaceClass =>
                new FixedDistanceSet(set, distance, Negated: false, Chars: [' ', '\t', '\r', '\n', '\f', '\v'], Range: null),
            RegexCharClass.NotSpaceClass or RegexCharClass.NotECMASpaceClass =>
                new FixedDistanceSet(set, distance, Negated: true, Chars: [' ', '\t', '\r', '\n', '\f', '\v'], Range: null),
            _ => default,
        };

        return projectedSet.Set is not null;
    }

    private static bool TryExtractFixedDistanceLiteralCore(RegexNode node, int offset, out string literal, out int distance)
    {
        literal = string.Empty;
        distance = 0;

        if (TryGetLiteralText(node, out var directLiteral))
        {
            literal = directLiteral;
            distance = offset;
            return true;
        }

        switch (node.Kind)
        {
            case RegexNodeKind.Capture:
            case RegexNodeKind.Group:
            case RegexNodeKind.Atomic:
                return node.ChildCount == 1 &&
                    TryExtractFixedDistanceLiteralCore(node.Child(0), offset, out literal, out distance);

            case RegexNodeKind.Concatenate:
                var currentOffset = offset;
                var found = false;
                var bestLength = 0;
                var bestDistance = -1;
                for (var i = 0; i < node.ChildCount; i++)
                {
                    var child = node.Child(i);
                    if (TryExtractFixedDistanceLiteralCore(child, currentOffset, out var childLiteral, out var childDistance) &&
                        (childLiteral.Length > bestLength ||
                        (childLiteral.Length == bestLength && childDistance > bestDistance)))
                    {
                        literal = childLiteral;
                        distance = childDistance;
                        bestLength = childLiteral.Length;
                        bestDistance = childDistance;
                        found = true;
                    }

                    currentOffset += ComputeMinLength(child);
                }

                return found;

            default:
                return false;
        }
    }

    private static int ComputeConcatenationMinLength(RegexNode node)
    {
        var total = 0;
        for (var i = 0; i < node.ChildCount; i++)
        {
            total += ComputeMinLength(node.Child(i));
        }

        return total;
    }

    private static int ComputeAlternationMinLength(RegexNode node)
    {
        if (node.ChildCount == 0)
        {
            return 0;
        }

        var min = int.MaxValue;
        for (var i = 0; i < node.ChildCount; i++)
        {
            min = Math.Min(min, ComputeMinLength(node.Child(i)));
        }

        return min == int.MaxValue ? 0 : min;
    }

    private static int? ComputeConcatenationMaxLength(RegexNode node)
    {
        var total = 0;
        for (var i = 0; i < node.ChildCount; i++)
        {
            var child = ComputeMaxLength(node.Child(i));
            if (child is null)
            {
                return null;
            }

            total += child.Value;
        }

        return total;
    }

    private static int? ComputeAlternationMaxLength(RegexNode node)
    {
        var max = 0;
        for (var i = 0; i < node.ChildCount; i++)
        {
            var child = ComputeMaxLength(node.Child(i));
            if (child is null)
            {
                return null;
            }

            max = Math.Max(max, child.Value);
        }

        return max;
    }

    private static int? MultiplyMax(int multiplier, int? value)
    {
        if (multiplier == int.MaxValue || value is null)
        {
            return null;
        }

        return multiplier * value.Value;
    }

    private static string? TryExtractLongestLiteral(RegexNode node)
    {
        if (node.Kind is RegexNodeKind.Capture or RegexNodeKind.Group && node.ChildCount == 1)
        {
            return TryExtractLongestLiteral(node.Child(0));
        }

        return TryGetLongestLiteral(node, out var literal) ? literal : null;
    }

    private static string[]? TryExtractAlternatePrefixes(RegexNode node)
    {
        if (node.Kind is RegexNodeKind.Capture or RegexNodeKind.Group && node.ChildCount == 1)
        {
            return TryExtractAlternatePrefixes(node.Child(0));
        }

        if (node.Kind != RegexNodeKind.Alternate || node.ChildCount == 0)
        {
            return null;
        }

        var prefixes = new string[node.ChildCount];
        for (var i = 0; i < node.ChildCount; i++)
        {
            if (!TryGetLeadingLiteral(node.Child(i), out var prefix) || prefix.Length == 0)
            {
                return null;
            }

            prefixes[i] = prefix;
        }

        return prefixes;
    }

    private static bool TryGetLongestLiteral(RegexNode node, out string literal)
    {
        literal = string.Empty;
        if (TryGetLiteralText(node, out var nodeLiteral))
        {
            literal = nodeLiteral;
            return true;
        }

        switch (node.Kind)
        {
            case RegexNodeKind.Concatenate:
            case RegexNodeKind.Alternate:
                var found = false;
                for (var i = 0; i < node.ChildCount; i++)
                {
                    if (!TryGetLongestLiteral(node.Child(i), out var childLiteral))
                    {
                        continue;
                    }

                    if (!found || childLiteral.Length > literal.Length)
                    {
                        literal = childLiteral;
                        found = true;
                    }
                }

                return found;

            default:
                return false;
        }
    }

    private static bool TryGetLeadingLiteral(RegexNode node, out string literal)
    {
        literal = string.Empty;
        if (TryGetLiteralText(node, out var nodeLiteral))
        {
            literal = nodeLiteral;
            return true;
        }

        if (node.Kind != RegexNodeKind.Concatenate)
        {
            return false;
        }

        for (var i = 0; i < node.ChildCount; i++)
        {
            var child = node.Child(i);
            if (TryGetLiteralText(child, out var childLiteral))
            {
                literal = childLiteral;
                return true;
            }

            if (child.Kind is RegexNodeKind.Bol or RegexNodeKind.Eol)
            {
                continue;
            }

            return false;
        }

        return false;
    }

    private static bool TryGetLiteralText(RegexNode node, out string literal)
    {
        switch (node.Kind)
        {
            case RegexNodeKind.One:
                literal = node.Ch.ToString();
                return true;

            case RegexNodeKind.Multi when !string.IsNullOrEmpty(node.Str):
                literal = node.Str;
                return true;

            default:
                literal = string.Empty;
                return false;
        }
    }
}
