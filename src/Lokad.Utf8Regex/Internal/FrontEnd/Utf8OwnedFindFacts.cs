namespace Lokad.Utf8Regex.Internal.FrontEnd;


// Search preparation over borrowed grammar. Keep only facts with search-plan
// consumers; the caller already owns the tree. Width/distance producers still
// use scalar byte bounds, while acquisition of this container and the
// remaining family/set results is outside the partial construction admission.
internal sealed class Utf8OwnedFindFacts
{
    private Utf8OwnedFindFacts(Utf8OwnedRegexTree tree)
    {
        var width = Utf8OwnedFindOptimizations.ComputeByteWidth(tree.Root);
        MinRequiredLength = width.Minimum;
        MaxPossibleLength = width.Maximum;
        IsFixedLength = width.IsFixed;
        TrailingAnchor = Utf8OwnedFindOptimizations.FindTrailingAnchor(tree.Root);
        LeadingLiteral = Utf8OwnedFindOptimizations.TryExtractLeadingLiteral(tree.Root);
        LongestLiteral = Utf8OwnedFindOptimizations.TryExtractLongestLiteral(tree.Root);
        AlternatePrefixes = Utf8OwnedFindOptimizations.FindAlternatePrefixes(tree.Root, tree.Options);
        FixedDistanceLiteral = Utf8OwnedFindOptimizations.TryExtractFixedDistanceLiteral(tree.Root);
        FixedDistanceSets = Utf8OwnedFindOptimizations.FindFixedDistanceSets(tree.Root);
    }

    public static Utf8OwnedFindFacts? FromTree(Utf8OwnedRegexTree? tree)
    {
        if (tree is null)
        {
            return null;
        }

        return new Utf8OwnedFindFacts(tree);
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
