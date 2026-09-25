using System.Collections;
using System.Globalization;

namespace Lokad.Utf8Regex.Internal.FrontEnd;

using RuntimeFrontEnd = Lokad.Utf8Regex.Internal.FrontEnd.Runtime;

// Owned parse-tree container: root plus capture accounting, options, and culture.
// Nodes stay fork-shaped until the node migration lands; the legacy analysis
// instance is already dead (no production reader), so it is not carried over.
// FromLegacy wraps a fallback tree without copying, so the provider seam keeps
// returning one container type for trees of either provenance.
internal sealed class Utf8OwnedRegexTree
{
    public Utf8OwnedRegexTree(
        RuntimeFrontEnd.RegexNode root,
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

    public RuntimeFrontEnd.RegexNode Root { get; }

    public int CaptureCount { get; }

    public CultureInfo? Culture { get; }

    public string[]? CaptureNames { get; }

    public Hashtable? CaptureNameToNumberMapping { get; }

    public Hashtable? CaptureNumberSparseMapping { get; }

    public RegexOptions Options { get; }

    public static Utf8OwnedRegexTree? FromLegacy(RuntimeFrontEnd.RegexTree? tree)
    {
        return tree is null
            ? null
            : new Utf8OwnedRegexTree(
                tree.Root,
                tree.CaptureCount,
                tree.CaptureNames,
                tree.CaptureNameToNumberMapping,
                tree.CaptureNumberSparseMapping,
                tree.Options,
                tree.Culture);
    }
}