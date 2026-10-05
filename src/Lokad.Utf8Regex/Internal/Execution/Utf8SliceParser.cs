using Lokad.Utf8Regex.Internal.FrontEnd;

namespace Lokad.Utf8Regex.Internal.Execution;

/// <summary>Compiles the shared UTF-16 VM from the authoritative owned tree.
/// Raw-pattern callers use the same front end as production preparation;
/// prepared callers reuse their tree and resolved capture catalog. Remaining
/// admission limits decline to residual routing without changing construction
/// behavior.</summary>
internal static class Utf8SliceParser
{
    internal const int MaxNestingDepth = 64;
    internal const int MaxPatternLength = 65536;

    internal static Utf8SliceProgram? TryParse(string pattern, RegexOptions options)
    {
        if (pattern is null || pattern.Length > MaxPatternLength ||
            (options & RegexOptions.NonBacktracking) != 0) return null;
        return TryParse(pattern, options, Utf8OwnedTreeParser.TryParse(pattern, options));
    }

    internal static Utf8SliceProgram? TryParse(string pattern, RegexOptions options, Utf8OwnedRegexTree? tree)
    {
        if (pattern.Length > MaxPatternLength || tree is null) return null;
        return Utf8OwnedTreeSliceLowerer.TryLower(tree, options);
    }
}
