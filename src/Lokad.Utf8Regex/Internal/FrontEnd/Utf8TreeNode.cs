namespace Lokad.Utf8Regex.Internal.FrontEnd;

// Owned regex parse-tree node: kind, options, payloads, and child storage with
// parent links. This mirrors the production-used fork node surface exactly
// (construction shapes, single-or-list child storage, backtracking predicate)
// so consumers migrate by renaming; fork-only surface (Reduce, MakeQuantifier,
// case-conversion constructors, child replacement, and the scan position) is
// deliberately omitted with no production readers.
internal sealed class Utf8TreeNode
{
    public object? Children;

    public Utf8TreeNode? Parent;

    public Utf8TreeNode(Utf8TreeNodeKind kind, RegexOptions options)
    {
        Kind = kind;
        Options = options;
    }

    public Utf8TreeNode(Utf8TreeNodeKind kind, RegexOptions options, char ch)
        : this(kind, options)
    {
        Ch = ch;
    }

    public Utf8TreeNode(Utf8TreeNodeKind kind, RegexOptions options, string? str)
        : this(kind, options)
    {
        Str = str;
    }

    public Utf8TreeNode(Utf8TreeNodeKind kind, RegexOptions options, int m, int n)
        : this(kind, options)
    {
        M = m;
        N = n;
    }

    public Utf8TreeNode(Utf8TreeNodeKind kind, RegexOptions options, int m)
        : this(kind, options)
    {
        M = m;
    }

    public Utf8TreeNodeKind Kind { get; set; }

    public RegexOptions Options { get; }

    public bool IsBacktrackingConstruct => Kind switch
    {
        Utf8TreeNodeKind.Alternate => true,
        Utf8TreeNodeKind.Loop => true,
        Utf8TreeNodeKind.Lazyloop => true,
        Utf8TreeNodeKind.Oneloop => true,
        Utf8TreeNodeKind.Notoneloop => true,
        Utf8TreeNodeKind.Setloop => true,
        Utf8TreeNodeKind.Onelazy => true,
        Utf8TreeNodeKind.Notonelazy => true,
        Utf8TreeNodeKind.Setlazy => true,
        Utf8TreeNodeKind.BackreferenceConditional => true,
        Utf8TreeNodeKind.ExpressionConditional => true,
        _ => false,
    };

    public string? Str { get; set; }

    public string? Str2 { get; set; }

    public char Ch { get; set; }

    public int M { get; set; }

    public int N { get; set; }

    public int ChildCount => Children switch
    {
        null => 0,
        Utf8TreeNode => 1,
        List<Utf8TreeNode> list => list.Count,
        _ => throw new InvalidOperationException("Unexpected child storage."),
    };

    public Utf8TreeNode Child(int index)
    {
        return Children switch
        {
            Utf8TreeNode child when index == 0 => child,
            List<Utf8TreeNode> list => list[index],
            null => throw new ArgumentOutOfRangeException(nameof(index)),
            _ => throw new InvalidOperationException("Unexpected child storage."),
        };
    }

    public IReadOnlyList<Utf8TreeNode> ChildList => Children switch
    {
        null => [],
        Utf8TreeNode child => [child],
        List<Utf8TreeNode> list => list,
        _ => throw new InvalidOperationException("Unexpected child storage."),
    };

    public Utf8TreeNode AddChild(Utf8TreeNode child)
    {
        child.Parent = this;

        switch (Children)
        {
            case null:
                Children = child;
                break;
            case Utf8TreeNode existing:
                Children = new List<Utf8TreeNode>(4) { existing, child };
                break;
            case List<Utf8TreeNode> list:
                list.Add(child);
                break;
            default:
                throw new InvalidOperationException("Unexpected child storage.");
        }

        return this;
    }
}