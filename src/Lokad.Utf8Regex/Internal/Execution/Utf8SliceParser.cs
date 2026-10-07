using Lokad.Utf8Regex.Internal.FrontEnd;

namespace Lokad.Utf8Regex.Internal.Execution;

/// <summary>Compiles the scalar UTF-8 VM from the authoritative owned tree.
/// Raw-pattern callers use the same front end as production preparation;
/// prepared callers reuse their tree and resolved capture catalog. Remaining
/// unsupported grammar can decline to residual routing. Lowering instruction
/// and buffer-capacity exhaustion throws an owned resource failure before
/// acquisition; pattern parsing and IR preparation remain separately bounded work.</summary>
internal static class Utf8SliceParser
{
    internal static Utf8SliceProgram? TryParse(string pattern, RegexOptions options)
    {
        if (pattern is null) return null;
        return TryParse(options, Utf8OwnedTreeParser.TryParse(pattern, options));
    }

    internal static Utf8SliceProgram? TryParse(RegexOptions options, Utf8OwnedRegexTree? tree)
    {
        if (tree is null) return null;
        if ((options & RegexOptions.NonBacktracking) != 0 &&
            Utf8NonBacktrackingGrammar.FindUnsupportedConstruct(tree.Root) is not null) return null;
        return Utf8OwnedTreeSliceLowerer.TryLower(tree, options);
    }

    internal static Utf8SliceProgram? TryParseNonBacktrackingFlatGraph(RegexOptions options, Utf8OwnedRegexTree? tree)
    {
        if (tree is null) return null;
        // No consuming loop, assertion recursion, reference, conditional or
        // atomic choice enters this admission. Candidate work is bounded by
        // the cached finite graph, independent of subject length. This proof
        // limits native discovery only; general graphs use the owned scalar NFA.
        var pending = new Stack<Utf8TreeNode>();
        pending.Push(tree.Root);
        while (pending.TryPop(out var node))
        {
            if (node.Kind is not (Utf8TreeNodeKind.One or Utf8TreeNodeKind.Notone or Utf8TreeNodeKind.Set or
                Utf8TreeNodeKind.Multi or Utf8TreeNodeKind.Empty or Utf8TreeNodeKind.Nothing or
                Utf8TreeNodeKind.Capture or Utf8TreeNodeKind.Group or Utf8TreeNodeKind.Concatenate or Utf8TreeNodeKind.Alternate or
                Utf8TreeNodeKind.Bol or Utf8TreeNodeKind.Eol or Utf8TreeNodeKind.Beginning or Utf8TreeNodeKind.EndZ or Utf8TreeNodeKind.End or
                Utf8TreeNodeKind.Boundary or Utf8TreeNodeKind.NonBoundary or Utf8TreeNodeKind.ECMABoundary or Utf8TreeNodeKind.NonECMABoundary))
                return null;
            if (node.Kind == Utf8TreeNodeKind.Capture && node.N >= 0) return null;
            for (var index = 0; index < node.ChildCount; index++) pending.Push(node.Child(index));
        }
        var program = TryParse(options & ~RegexOptions.NonBacktracking, tree);
        // Aliased declarations can write a public slot more than once even
        // without loops. Their NonBacktracking capture-history rule is separate.
        return program is { HasSharedCaptureDeclarations: false, HasBalancing: false } ? program : null;
    }
}
