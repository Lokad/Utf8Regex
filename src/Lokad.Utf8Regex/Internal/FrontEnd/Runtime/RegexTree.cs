using System.Collections;
using System.Globalization;

namespace Lokad.Utf8Regex.Internal.FrontEnd.Runtime;

internal sealed class RegexTree
{
    public RegexTree(
        RegexNode root,
        int captureCount,
        string[]? captureNames,
        Hashtable? captureNameToNumberMapping,
        Hashtable? captureNumberSparseMapping,
        RegexOptions options,
        CultureInfo? culture = null,
        RegexFindOptimizations? findOptimizations = null)
    {
        Root = root;
        CaptureCount = captureCount;
        CaptureNames = captureNames;
        CaptureNameToNumberMapping = captureNameToNumberMapping;
        CaptureNumberSparseMapping = captureNumberSparseMapping;
        Options = options;
        Culture = culture;
        FindOptimizations = findOptimizations;
    }

    public RegexNode Root { get; }

    public RegexFindOptimizations? FindOptimizations { get; }

    public int CaptureCount { get; }

    public CultureInfo? Culture { get; }

    public string[]? CaptureNames { get; }

    public Hashtable? CaptureNameToNumberMapping { get; }

    public Hashtable? CaptureNumberSparseMapping { get; }

    public RegexOptions Options { get; }
}
