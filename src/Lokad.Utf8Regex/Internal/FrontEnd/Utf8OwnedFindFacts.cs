namespace Lokad.Utf8Regex.Internal.FrontEnd;

using RuntimeFrontEnd = Lokad.Utf8Regex.Internal.FrontEnd.Runtime;

// Owned find-optimization facts for one runtime tree, computed once per analysis
// pass from the owned hub. This container replaces the legacy find
// optimizations instance for search analysis: every property mirrors a legacy
// member over the same tree, differentially pinned row by row in the hub
// battery. Trees of either provenance feed it, so analyzer migration lands
// before the provider cutover instead of with it.
internal sealed class Utf8OwnedFindFacts
{
    private Utf8OwnedFindFacts(RuntimeFrontEnd.RegexTree tree)
    {
        Root = tree.Root;
        MinRequiredLength = Utf8OwnedFindOptimizations.ComputeMinLength(tree.Root);
        MaxPossibleLength = Utf8OwnedFindOptimizations.ComputeMaxLength(tree.Root);
        IsFixedLength = MaxPossibleLength is int max && max == MinRequiredLength;
        LeadingAnchor = Utf8OwnedFindOptimizations.FindLeadingAnchor(tree.Root);
        TrailingAnchor = Utf8OwnedFindOptimizations.FindTrailingAnchor(tree.Root);
        LeadingLiteral = Utf8OwnedFindOptimizations.TryExtractLeadingLiteral(tree.Root);
        LongestLiteral = Utf8OwnedFindOptimizations.TryExtractLongestLiteral(tree.Root);
        AlternatePrefixes = Utf8OwnedFindOptimizations.FindAlternatePrefixes(tree.Root, tree.Options);
        FixedDistanceLiteral = Utf8OwnedFindOptimizations.TryExtractFixedDistanceLiteral(tree.Root);
        FixedDistanceSets = Utf8OwnedFindOptimizations.FindFixedDistanceSets(tree.Root);
    }

    public static Utf8OwnedFindFacts? FromTree(RuntimeFrontEnd.RegexTree? tree)
    {
        if (tree is null)
        {
            return null;
        }

        return new Utf8OwnedFindFacts(tree);
    }

    public RuntimeFrontEnd.RegexNode Root { get; }

    public bool IsFixedLength { get; }

    public int MinRequiredLength { get; }

    public int? MaxPossibleLength { get; }

    public RuntimeFrontEnd.RegexNodeKind LeadingAnchor { get; }

    public RuntimeFrontEnd.RegexNodeKind TrailingAnchor { get; }

    public string? LeadingLiteral { get; }

    public string? LongestLiteral { get; }

    public string[]? AlternatePrefixes { get; }

    public (string Literal, int Distance)? FixedDistanceLiteral { get; }

    public Utf8OwnedFindOptimizations.OwnedSet[]? FixedDistanceSets { get; }
}
