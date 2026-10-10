namespace Lokad.Utf8Regex.Internal.FrontEnd;

// Owned regex parse-tree node: kind, options, payloads, and child storage with
// parent links. The parser and scalar/native preparation consumers borrow this
// representation; it carries grammar and lexical payloads, not BCL execution
// or per-node runtime parity facts.
internal sealed class Utf8TreeNode
{
    private readonly record struct ConstructionState(object? Children, Utf8TreeNode? Parent,
        Utf8TreeNodeKind Kind, RegexOptions Options, bool MatchCaseInsensitive, object? Payload, char Ch, int M, int N);

    public object? Children;

    public Utf8TreeNode? Parent;

    private Utf8TreeNode(ConstructionState state)
    {
        Children = state.Children;
        Parent = state.Parent;
        Kind = state.Kind;
        Options = state.Options;
        MatchCaseInsensitive = state.MatchCaseInsensitive;
        _payload = state.Payload;
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

    internal static Utf8TreeNode Create(Utf8TreeNodeKind kind, RegexOptions options, Utf8ScalarClass scalarClass) =>
        CreatePayload(kind, options, '\0', scalarClass, 0, 0, null, null, (options & RegexOptions.IgnoreCase) != 0);

    // Quantification borrows the original canonical class and lexical fold flag.
    internal static Utf8TreeNode CreateSetRepeat(Utf8TreeNode atom, RegexOptions options, int minimum, int maximum, bool lazy) =>
        CreatePayload(lazy ? Utf8TreeNodeKind.Setlazy : Utf8TreeNodeKind.Setloop,
            options, '\0', atom._payload, minimum, maximum, null, null, atom.MatchCaseInsensitive);

    internal static Utf8TreeNode Create(Utf8TreeNodeKind kind, RegexOptions options, int m, int n) =>
        Create(kind, options, '\0', null, m, n, null, null, (options & RegexOptions.IgnoreCase) != 0);

    // Strings and children are borrowed here; their separate acquisition loans
    // are charged by their producers. The complete node and its retaining root
    // are admitted before the only constructor can run.
    internal static Utf8TreeNode Create(Utf8TreeNodeKind kind, RegexOptions options, char ch,
        string? str, int m, int n, object? children, Utf8TreeNode? parent, bool matchCaseInsensitive)
        => CreatePayload(kind, options, ch, str, m, n, children, parent, matchCaseInsensitive);

    private static Utf8TreeNode CreatePayload(Utf8TreeNodeKind kind, RegexOptions options, char ch,
        object? payload, int m, int n, object? children, Utf8TreeNode? parent, bool matchCaseInsensitive)
    {
        using var scope = Utf8ConstructionScope.Enter();
        var node = scope.Budget.AllocateObject(new ConstructionState(children, parent, kind, options,
            matchCaseInsensitive, payload, ch, m, n), static state => new Utf8TreeNode(state));
        scope.Complete();
        return node;
    }

    public Utf8TreeNodeKind Kind { get; set; }

    public RegexOptions Options { get; }

    // Execution retains the lexical fold flag even when structural analysis
    // consumes a partially expanded set and clears IgnoreCase on that node.
    internal bool MatchCaseInsensitive { get; set; }

    private object? _payload;
    internal Utf8ScalarClass? ScalarClass => _payload as Utf8ScalarClass;
    public string? Str
    {
        get => _payload as string;
        set => _payload = value;
    }

    public char Ch { get; set; }

    public int M { get; set; }

    public int N { get; set; }

    public int ChildCount => Children switch
    {
        null => 0,
        Utf8TreeNode => 1,
        Utf8TreeNode[] nodes => nodes.Length,
        Utf8ConstructionBuffer<Utf8TreeNode> scratch => scratch.Count,
        _ => throw new InvalidOperationException("Unexpected child storage."),
    };

    public Utf8TreeNode Child(int index)
    {
        return Children switch
        {
            Utf8TreeNode child when index == 0 => child,
            Utf8TreeNode[] nodes => nodes[index],
            Utf8ConstructionBuffer<Utf8TreeNode> scratch => scratch[index],
            null => throw new ArgumentOutOfRangeException(nameof(index)),
            _ => throw new InvalidOperationException("Unexpected child storage."),
        };
    }

}
