using Lokad.Utf8Regex.Internal.FrontEnd;

namespace Lokad.Utf8Regex.Internal.Execution;

/// <summary>Compiles the scalar UTF-8 VM from the authoritative owned tree.
/// Raw-pattern callers use the same front end as production preparation;
/// prepared callers reuse their tree and resolved capture catalog. Remaining
/// admission limits decline to residual routing without changing construction
/// behavior.</summary>
internal static class Utf8SliceParser
{
    internal const int MaxNestingDepth = 64;

    internal static Utf8SliceProgram? TryParse(string pattern, RegexOptions options)
    {
        if (pattern is null || (options & RegexOptions.NonBacktracking) != 0) return null;
        return TryParse(options, Utf8OwnedTreeParser.TryParse(pattern, options));
    }

    internal static Utf8SliceProgram? TryParse(RegexOptions options, Utf8OwnedRegexTree? tree)
    {
        if (tree is null) return null;
        return Utf8OwnedTreeSliceLowerer.TryLower(tree, options);
    }
}
