namespace Lokad.Utf8Regex.Internal.FrontEnd;


// Search preparation over borrowed grammar. The facts holder, covering prefix
// ropes/families/frames/results and fixed-set state use shared construction
// admission. Leading/longest literals are exact mandatory scalar fragments;
// fixed distances require byte-width proofs. Broader preparation producers and
// retained-policy qualification still have their separate cleanup gates.
internal sealed class Utf8OwnedFindFacts
{
    private readonly record struct ConstructionState(bool IsFixedLength, int MinRequiredLength,
        int? MaxPossibleLength, Utf8TreeNodeKind TrailingAnchor, string? LeadingLiteral,
        string? LongestLiteral, string[]? AlternatePrefixes, (string Literal, int Distance)? FixedDistanceLiteral,
        Utf8OwnedFindOptimizations.OwnedSet[]? FixedDistanceSets);

    private Utf8OwnedFindFacts(ConstructionState state)
    {
        IsFixedLength = state.IsFixedLength;
        MinRequiredLength = state.MinRequiredLength;
        MaxPossibleLength = state.MaxPossibleLength;
        TrailingAnchor = state.TrailingAnchor;
        LeadingLiteral = state.LeadingLiteral;
        LongestLiteral = state.LongestLiteral;
        AlternatePrefixes = state.AlternatePrefixes;
        FixedDistanceLiteral = state.FixedDistanceLiteral;
        FixedDistanceSets = state.FixedDistanceSets;
    }

    public static Utf8OwnedFindFacts? FromTree(Utf8OwnedRegexTree? tree)
    {
        if (tree is null) return null;
        using var scope = Utf8ConstructionScope.Enter();
        var width = Utf8OwnedFindOptimizations.ComputeByteWidth(tree.Root);
        var state = new ConstructionState(width.IsFixed, width.Minimum, width.Maximum,
            Utf8OwnedFindOptimizations.FindTrailingAnchor(tree.Root),
            Utf8OwnedFindOptimizations.TryExtractLeadingLiteral(tree.Root),
            Utf8OwnedFindOptimizations.TryExtractLongestLiteral(tree.Root),
            Utf8OwnedFindOptimizations.FindAlternatePrefixes(tree.Root, tree.Options),
            Utf8OwnedFindOptimizations.TryExtractFixedDistanceLiteral(tree.Root),
            Utf8OwnedFindOptimizations.FindFixedDistanceSets(tree.Root));
        var result = scope.Budget.AllocateObject(state, static value => new Utf8OwnedFindFacts(value));
        scope.Complete();
        return result;
    }

    public bool IsFixedLength { get; }

    public int MinRequiredLength { get; }

    public int? MaxPossibleLength { get; }

    public Utf8TreeNodeKind TrailingAnchor { get; }

    public string? LeadingLiteral { get; }

    public string? LongestLiteral { get; }

    public string[]? AlternatePrefixes { get; }

    public (string Literal, int Distance)? FixedDistanceLiteral { get; }

    public Utf8OwnedFindOptimizations.OwnedSet[]? FixedDistanceSets { get; }
}
