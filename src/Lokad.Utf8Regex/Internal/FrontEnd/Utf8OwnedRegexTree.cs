using System.Collections;
using System.Globalization;

namespace Lokad.Utf8Regex.Internal.FrontEnd;

using RuntimeFrontEnd = Lokad.Utf8Regex.Internal.FrontEnd.Runtime;

// Owned parse-tree container: root plus capture accounting, options, and culture.
// Nodes are owned throughout; FromLegacy deep-copies fallback trees field by
// field (verified by the tree batteries), so the provider seam returns one
// container type for trees of either provenance. The legacy analysis instance
// is dead (no production reader) and is not carried over.
internal sealed class Utf8OwnedRegexTree
{
    public Utf8OwnedRegexTree(
        Utf8TreeNode root,
        int captureCount,
        string[]? captureNames,
        Hashtable? captureNameToNumberMapping,
        Hashtable? captureNumberSparseMapping,
        RegexOptions options,
        CultureInfo? culture)
    {
        Root = root;
        CaptureCount = captureCount;
        CaptureNames = captureNames;
        CaptureNameToNumberMapping = captureNameToNumberMapping;
        CaptureNumberSparseMapping = captureNumberSparseMapping;
        Options = options;
        Culture = culture;
    }

    public Utf8TreeNode Root { get; }

    public int CaptureCount { get; }

    public CultureInfo? Culture { get; }

    public string[]? CaptureNames { get; }

    public Hashtable? CaptureNameToNumberMapping { get; }

    public Hashtable? CaptureNumberSparseMapping { get; }

    public RegexOptions Options { get; }

    public static Utf8OwnedRegexTree? FromLegacy(RuntimeFrontEnd.RegexTree? tree)
    {
        if (tree is null || tree.Root is null)
        {
            return null;
        }

        return new Utf8OwnedRegexTree(
            CopyNode(tree.Root),
            tree.CaptureCount,
            tree.CaptureNames,
            tree.CaptureNameToNumberMapping,
            tree.CaptureNumberSparseMapping,
            tree.Options,
            tree.Culture);

        static Utf8TreeNode CopyNode(RuntimeFrontEnd.RegexNode node)
        {
            var copy = new Utf8TreeNode((Utf8TreeNodeKind)(int)node.Kind, node.Options)
            {
                Str = node.Str,
                Str2 = node.Str2,
                Ch = node.Ch,
                M = node.M,
                N = node.N,
            };

            if (node.Children is RuntimeFrontEnd.RegexNode single)
            {
                var singleCopy = CopyNode(single);
                copy.Children = singleCopy;
                singleCopy.Parent = copy;
            }
            else if (node.Children is List<RuntimeFrontEnd.RegexNode> children)
            {
                var copies = new List<Utf8TreeNode>(children.Count);
                foreach (var child in children)
                {
                    var childCopy = CopyNode(child);
                    childCopy.Parent = copy;
                    copies.Add(childCopy);
                }

                copy.Children = copies;
            }

            return copy;
        }
    }
}