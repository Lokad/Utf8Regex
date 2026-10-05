using System.Collections;
using System.Globalization;

namespace Lokad.Utf8Regex.Internal.FrontEnd;

// Owned parse-tree container: root plus capture accounting, options, and culture.
// Nodes are owned throughout; the owned parser builds them directly, so the
// provider seam carries one container type with no conversion step.
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
        : this(root, captureCount, captureNames, captureNameToNumberMapping,
            captureNumberSparseMapping, options, culture, null, false, false)
    {
    }

    internal Utf8OwnedRegexTree(
        Utf8TreeNode root,
        int captureCount,
        string[]? captureNames,
        Hashtable? captureNameToNumberMapping,
        Hashtable? captureNumberSparseMapping,
        RegexOptions options,
        CultureInfo? culture,
        Utf8CaptureCatalog? captureCatalog,
        bool usesLiteralClassBracketSyntax,
        bool usesIgnoreCase)
    {
        Root = root;
        CaptureCount = captureCount;
        CaptureNames = captureNames;
        CaptureNameToNumberMapping = captureNameToNumberMapping;
        CaptureNumberSparseMapping = captureNumberSparseMapping;
        Options = options;
        Culture = culture;
        CaptureCatalog = captureCatalog;
        UsesLiteralClassBracketSyntax = usesLiteralClassBracketSyntax;
        UsesIgnoreCase = usesIgnoreCase;
    }

    public Utf8TreeNode Root { get; }

    public int CaptureCount { get; }

    public CultureInfo? Culture { get; }

    public string[]? CaptureNames { get; }

    public Hashtable? CaptureNameToNumberMapping { get; }

    public Hashtable? CaptureNumberSparseMapping { get; }

    public RegexOptions Options { get; }

    internal Utf8CaptureCatalog? CaptureCatalog { get; }

    // PCRE2-INTEGRATION-POINT: retain parsed grammar facts for profile routing.
    internal bool UsesLiteralClassBracketSyntax { get; }

    internal bool UsesIgnoreCase { get; }
}
