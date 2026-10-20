using System.Globalization;

namespace Lokad.Utf8Regex.Internal.FrontEnd;

// Owned parse-tree container: root plus capture accounting, options, and culture.
// Nodes are owned throughout; the owned parser builds them directly, so the
// parsed container passes directly to analysis with no provider conversion.
internal sealed class Utf8OwnedRegexTree
{
    private readonly record struct ConstructionState(Utf8TreeNode Root, int CaptureCount, CultureInfo? Culture, string[]? CaptureNames,
        IReadOnlyDictionary<string, int>? CaptureNameToNumberMapping,
        IReadOnlyDictionary<int, int>? CaptureNumberSparseMapping, RegexOptions Options,
        Utf8CaptureCatalog? CaptureCatalog, bool UsesLiteralClassBracketSyntax, bool UsesIgnoreCase);

    internal static Utf8OwnedRegexTree Create(
        Utf8TreeNode root,
        int captureCount,
        string[]? captureNames,
        IReadOnlyDictionary<string, int>? captureNameToNumberMapping,
        IReadOnlyDictionary<int, int>? captureNumberSparseMapping,
        RegexOptions options,
        CultureInfo? culture,
        Utf8CaptureCatalog? captureCatalog,
        bool usesLiteralClassBracketSyntax,
        bool usesIgnoreCase)
    {
        using var scope = Utf8ConstructionScope.Enter();
        var tree = scope.Budget.AllocateObject(new ConstructionState(root, captureCount, culture, captureNames,
            captureNameToNumberMapping, captureNumberSparseMapping, options, captureCatalog,
            usesLiteralClassBracketSyntax, usesIgnoreCase), static state => new Utf8OwnedRegexTree(state));
        scope.Complete();
        return tree;
    }

    private Utf8OwnedRegexTree(ConstructionState state)
    {
        Root = state.Root;
        CaptureCount = state.CaptureCount;
        CaptureNames = state.CaptureNames;
        CaptureNameToNumberMapping = state.CaptureNameToNumberMapping;
        CaptureNumberSparseMapping = state.CaptureNumberSparseMapping;
        Options = state.Options;
        Culture = state.Culture;
        CaptureCatalog = state.CaptureCatalog;
        UsesLiteralClassBracketSyntax = state.UsesLiteralClassBracketSyntax;
        UsesIgnoreCase = state.UsesIgnoreCase;
    }

    public Utf8TreeNode Root { get; }

    public int CaptureCount { get; }

    public CultureInfo? Culture { get; }

    public string[]? CaptureNames { get; }

    public IReadOnlyDictionary<string, int>? CaptureNameToNumberMapping { get; }

    public IReadOnlyDictionary<int, int>? CaptureNumberSparseMapping { get; }

    public RegexOptions Options { get; }

    internal Utf8CaptureCatalog? CaptureCatalog { get; }

    // PCRE2-INTEGRATION-POINT: retain parsed grammar facts for profile routing.
    internal bool UsesLiteralClassBracketSyntax { get; }

    internal bool UsesIgnoreCase { get; }
}
