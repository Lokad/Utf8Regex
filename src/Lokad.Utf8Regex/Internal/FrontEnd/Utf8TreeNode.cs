namespace Lokad.Utf8Regex.Internal.FrontEnd;

// Owned regex parse-tree node: kind, options, payloads, and child storage with
// parent links. The parser and scalar/native preparation consumers borrow this
// representation; it carries grammar and lexical payloads, not BCL execution
// or per-node runtime parity facts.
internal sealed class Utf8TreeNode
{
    private readonly record struct ConstructionState(object? Children, Utf8TreeNode? Parent,
        Utf8TreeNodeKind Kind, RegexOptions Options, bool MatchCaseInsensitive, string? Str, char Ch, int M, int N);

    public object? Children;

    public Utf8TreeNode? Parent;

    private Utf8TreeNode(ConstructionState state)
    {
        Children = state.Children;
        Parent = state.Parent;
        Kind = state.Kind;
        Options = state.Options;
        MatchCaseInsensitive = state.MatchCaseInsensitive;
        Str = state.Str;
        Ch = state.Ch;
        M = state.M;
        N = state.N;
    }

    internal static Utf8TreeNode Create(Utf8TreeNodeKind kind, RegexOptions options) =>
        Create(kind, options, '\0', null, 0, 0, null, null, (options & RegexOptions.IgnoreCase) != 0);

    internal static Utf8TreeNode Create(Utf8TreeNodeKind kind, RegexOptions options, char ch) =>
        Create(kind, options, ch, null, 0, 0, null, null, (options & RegexOptions.IgnoreCase) != 0);

    internal static Utf8TreeNode Create(Utf8TreeNodeKind kind, RegexOptions options, string? str) =>
        Create(kind, options, '\0', str, 0, 0, null, null, (options & RegexOptions.IgnoreCase) != 0);

    internal static Utf8TreeNode Create(Utf8TreeNodeKind kind, RegexOptions options, int m, int n) =>
        Create(kind, options, '\0', null, m, n, null, null, (options & RegexOptions.IgnoreCase) != 0);

    // Strings and children are borrowed here; their separate acquisition loans
    // are charged by their producers. The complete node and its retaining root
    // are admitted before the only constructor can run.
    internal static Utf8TreeNode Create(Utf8TreeNodeKind kind, RegexOptions options, char ch,
        string? str, int m, int n, object? children, Utf8TreeNode? parent, bool matchCaseInsensitive)
    {
        using var scope = Utf8ConstructionScope.Enter();
        var node = scope.Budget.AllocateObject(new ConstructionState(children, parent, kind, options,
            matchCaseInsensitive, str, ch, m, n), static state => new Utf8TreeNode(state));
        scope.Complete();
        return node;
    }

    public Utf8TreeNodeKind Kind { get; set; }

    public RegexOptions Options { get; }

    // Execution retains the lexical fold flag even when structural analysis
    // consumes a partially expanded set and clears IgnoreCase on that node.
    internal bool MatchCaseInsensitive { get; set; }

    public string? Str { get; set; }

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
