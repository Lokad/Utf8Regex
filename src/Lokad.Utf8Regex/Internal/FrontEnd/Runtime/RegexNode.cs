using System.Globalization;

namespace Lokad.Utf8Regex.Internal.FrontEnd.Runtime;

internal sealed class RegexNode
{
    internal const int MultiVsRepeaterLimit = 64;

    public object? Children;

    public RegexNode? Parent;

    public RegexNode(RegexNodeKind kind, RegexOptions options)
    {
        Kind = kind;
        Options = options;
    }

    public RegexNode(RegexNodeKind kind, RegexOptions options, char ch)
        : this(kind, options)
    {
        Ch = ch;
    }

    public RegexNode(RegexNodeKind kind, RegexOptions options, string? str)
        : this(kind, options)
    {
        Str = str;
    }

    public RegexNode(RegexNodeKind kind, RegexOptions options, int m, int n)
        : this(kind, options)
    {
        M = m;
        N = n;
    }

    public RegexNode(RegexNodeKind kind, RegexOptions options, int m)
        : this(kind, options)
    {
        M = m;
    }

    public RegexNodeKind Kind { get; set; }

    public RegexOptions Options { get; }

    public bool IsBacktrackingConstruct => Kind switch
    {
        RegexNodeKind.Alternate => true,
        RegexNodeKind.Loop => true,
        RegexNodeKind.Lazyloop => true,
        RegexNodeKind.Oneloop => true,
        RegexNodeKind.Notoneloop => true,
        RegexNodeKind.Setloop => true,
        RegexNodeKind.Onelazy => true,
        RegexNodeKind.Notonelazy => true,
        RegexNodeKind.Setlazy => true,
        RegexNodeKind.BackreferenceConditional => true,
        RegexNodeKind.ExpressionConditional => true,
        _ => false,
    };

    public string? Str { get; set; }

    public string? Str2 { get; set; }

    public char Ch { get; set; }

    public int M { get; set; }

    public int N { get; set; }

    public int Position { get; set; }

    public int ChildCount => Children switch
    {
        null => 0,
        RegexNode => 1,
        List<RegexNode> list => list.Count,
        _ => throw new InvalidOperationException("Unexpected child storage."),
    };

    public RegexNode Child(int index)
    {
        return Children switch
        {
            RegexNode child when index == 0 => child,
            List<RegexNode> list => list[index],
            null => throw new ArgumentOutOfRangeException(nameof(index)),
            _ => throw new InvalidOperationException("Unexpected child storage."),
        };
    }

    public IReadOnlyList<RegexNode> ChildList => Children switch
    {
        null => [],
        RegexNode child => [child],
        List<RegexNode> list => list,
        _ => throw new InvalidOperationException("Unexpected child storage."),
    };

    public RegexNode AddChild(RegexNode child)
    {
        ArgumentNullException.ThrowIfNull(child);
        child.Parent = this;

        switch (Children)
        {
            case null:
                Children = child;
                break;
            case RegexNode existing:
                Children = new List<RegexNode>(4) { existing, child };
                break;
            case List<RegexNode> list:
                list.Add(child);
                break;
            default:
                throw new InvalidOperationException("Unexpected child storage.");
        }

        return this;
    }

    public void ReplaceChild(int index, RegexNode child)
    {
        ArgumentNullException.ThrowIfNull(child);
        child.Parent = this;

        switch (Children)
        {
            case RegexNode existing when index == 0:
                Children = child;
                break;

            case List<RegexNode> list:
                list[index] = child;
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(index));
        }
    }

    public RegexNode Reduce()
    {
        return Kind switch
        {
            RegexNodeKind.Concatenate => ReduceConcatenation(),
            RegexNodeKind.Alternate => ReduceAlternation(),
            RegexNodeKind.Group => ChildCount == 1 ? Child(0) : this,
            _ => this,
        };
    }

    public RegexNode MakeQuantifier(bool lazy, int min, int max)
    {
        if (min == 1 && max == 1)
        {
            return this;
        }

        if (max == 0)
        {
            return new RegexNode(RegexNodeKind.Empty, Options);
        }

        if (ChildCount != 0)
        {
            var loopKind = lazy ? RegexNodeKind.Lazyloop : RegexNodeKind.Loop;
            return new RegexNode(loopKind, Options, min, max).AddChild(this);
        }

        switch (Kind)
        {
            case RegexNodeKind.One:
                return CreateRepeater(lazy ? RegexNodeKind.Onelazy : RegexNodeKind.Oneloop, Ch, min, max);

            case RegexNodeKind.Set when Str is not null:
                return CreateRepeater(lazy ? RegexNodeKind.Setlazy : RegexNodeKind.Setloop, Str, min, max);

            case RegexNodeKind.Multi when Str is { Length: 1 } literal:
                return CreateRepeater(lazy ? RegexNodeKind.Onelazy : RegexNodeKind.Oneloop, literal[0], min, max);

            default:
                var genericLoopKind = lazy ? RegexNodeKind.Lazyloop : RegexNodeKind.Loop;
                return new RegexNode(genericLoopKind, Options, min, max).AddChild(this);
        }
    }

    public static RegexNode CreateOneWithCaseConversion(char ch, RegexOptions options, CultureInfo? culture, ref RegexCaseBehavior caseBehavior)
    {
        if ((options & RegexOptions.IgnoreCase) != 0)
        {
            _ = culture;
            _ = caseBehavior;

            var lower = char.ToLowerInvariant(ch);
            var upper = char.ToUpperInvariant(ch);
            if (lower == ch && upper == ch)
            {
                return new RegexNode(RegexNodeKind.One, options & ~RegexOptions.IgnoreCase, ch);
            }

            Span<char> chars = stackalloc char[2];
            var count = 0;
            chars[count++] = lower;
            if (upper != lower)
            {
                chars[count++] = upper;
            }

            return new RegexNode(
                RegexNodeKind.Set,
                options & ~RegexOptions.IgnoreCase,
                RegexCharClass.CharsToStringClass(chars[..count]));
        }

        return new RegexNode(RegexNodeKind.One, options, ch);
    }

    private RegexNode CreateRepeater(RegexNodeKind kind, char ch, int min, int max)
    {
        var repeater = new RegexNode(kind, Options, ch)
        {
            M = min,
            N = max,
        };

        return repeater;
    }

    private RegexNode CreateRepeater(RegexNodeKind kind, string set, int min, int max)
    {
        var repeater = new RegexNode(kind, Options, set)
        {
            M = min,
            N = max,
        };

        return repeater;
    }

    private RegexNode ReduceConcatenation()
    {
        if (Children is not List<RegexNode> list)
        {
            return ChildCount == 1 ? Child(0) : this;
        }

        var reduced = new List<RegexNode>(list.Count);
        foreach (var child in list)
        {
            if (child.Kind == RegexNodeKind.Empty)
            {
                continue;
            }

            if (child.Kind == RegexNodeKind.Concatenate)
            {
                foreach (var nested in child.ChildList)
                {
                    AppendReducedConcatenationTerm(reduced, nested);
                }

                continue;
            }

            AppendReducedConcatenationTerm(reduced, child);
        }

        return ReplaceChildrenFromReduction(reduced);
    }

    private RegexNode ReduceAlternation()
    {
        if (Children is not List<RegexNode> list)
        {
            return ChildCount == 1 ? Child(0) : this;
        }

        var reduced = new List<RegexNode>(list.Count);
        var sawEmpty = false;
        foreach (var child in list)
        {
            if (child.Kind == RegexNodeKind.Alternate)
            {
                foreach (var nested in child.ChildList)
                {
                    AppendReducedAlternationTerm(reduced, nested, ref sawEmpty);
                }

                continue;
            }

            AppendReducedAlternationTerm(reduced, child, ref sawEmpty);
        }

        return ReplaceChildrenFromReduction(reduced);
    }

    private RegexNode ReplaceChildrenFromReduction(List<RegexNode> reduced)
    {
        switch (reduced.Count)
        {
            case 0:
                return new RegexNode(RegexNodeKind.Empty, Options);

            case 1:
                return reduced[0];

            default:
                Children = reduced;
                foreach (var child in reduced)
                {
                    child.Parent = this;
                }

                return this;
        }
    }

    private static void AppendReducedConcatenationTerm(List<RegexNode> reduced, RegexNode child)
    {
        if (reduced.Count > 0 && TryGetLiteralText(reduced[^1], out var previousLiteral) && TryGetLiteralText(child, out var currentLiteral) && reduced[^1].Options == child.Options)
        {
            reduced[^1] = new RegexNode(RegexNodeKind.Multi, child.Options, previousLiteral + currentLiteral);
            return;
        }

        reduced.Add(child);
    }

    private static void AppendReducedAlternationTerm(List<RegexNode> reduced, RegexNode child, ref bool sawEmpty)
    {
        if (child.Kind == RegexNodeKind.Empty)
        {
            if (sawEmpty)
            {
                return;
            }

            sawEmpty = true;
        }

        reduced.Add(child);
    }

    private static bool TryGetLiteralText(RegexNode node, out string literal)
    {
        switch (node.Kind)
        {
            case RegexNodeKind.One:
                literal = node.Ch.ToString();
                return true;

            case RegexNodeKind.Multi when node.Str is not null:
                literal = node.Str;
                return true;

            default:
                literal = string.Empty;
                return false;
        }
    }
}
