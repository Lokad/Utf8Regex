using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.FrontEnd;

namespace Lokad.Utf8Regex.Tests;

internal static class Utf8ScalarCaptureWitness
{
    internal static Utf8SliceProgram Lower(string pattern, RegexOptions options, int depth, int shape)
    {
        var tree = Utf8OwnedTreeParser.TryParse(pattern, options);
        Assert.NotNull(tree);
        Assert.Equal(Utf8TreeNodeKind.Capture, tree.Root.Kind);
        Assert.Equal(0, tree.Root.M);
        var node = tree.Root.Child(0);
        for (var index = 0; index < depth; index++)
        {
            var parent = Utf8TreeNode.Create(shape switch
            {
                0 => Utf8TreeNodeKind.Concatenate,
                1 => Utf8TreeNodeKind.Group,
                _ => Utf8TreeNodeKind.Concatenate,
            }, options, 0, -1);
            // Group zero belongs only at the root of the scalar grammar. The
            // old byte fixture nested synthetic group-zero captures; replace
            // that transparent wrapper with an empty concatenation here.
            if (shape == 2) parent.WithChild(Utf8TreeNode.Create(Utf8TreeNodeKind.Empty, options));
            parent.WithChild(node);
            node = parent;
        }
        var root = Utf8TreeNode.Create(Utf8TreeNodeKind.Capture, tree.Root.Options, 0, -1);
        root.WithChild(node);
        var wrapped = Utf8OwnedRegexTree.Create(root, tree.CaptureCount, tree.CaptureNames,
            tree.CaptureNameToNumberMapping, tree.CaptureNumberSparseMapping, options, tree.Culture,
            tree.CaptureCatalog, tree.UsesLiteralClassBracketSyntax, tree.UsesIgnoreCase);
        var program = Utf8OwnedTreeSliceLowerer.TryLower(wrapped, options);
        Assert.NotNull(program);
        return program;
    }

    // The BCL comparison is deliberately ASCII-only. Supplementary scalars have
    // explicit byte/history outcomes in Utf8ScalarCaptureContinuationTests.
    internal static void AssertAsciiAt(Utf8SliceProgram program, Regex oracle, string text, int start)
    {
        Assert.All(text, static ch => Assert.InRange(ch, (char)0, (char)127));
        var expected = oracle.Match(text, start);
        var starts = new int[program.GroupCount];
        var ends = new int[program.GroupCount];
        var invocation = new Utf8SliceInvocation(Utf8ExecutionLimits.Unlimited, Regex.InfiniteMatchTimeout, CancellationToken.None);
        Assert.True(invocation.Ledger.TryReserve(7));
        var outcome = Utf8SliceRunner.TryMatchAt(program, Encoding.ASCII.GetBytes(text), start, starts, ends, true, true, ref invocation);
        try
        {
            Assert.Equal(Utf8ExecutionLimitKind.None, outcome.Breach);
            Assert.Equal(expected.Success && expected.Index == start, outcome.IsMatch);
            if (!outcome.IsMatch)
            {
                Assert.Null(outcome.CaptureStarts);
                Assert.Null(outcome.CaptureLengths);
                return;
            }
            Assert.Equal(expected.Index + expected.Length, outcome.EndIndex);
            Assert.Equal(oracle.GetGroupNumbers(), program.GroupNumbers);
            for (var slot = 0; slot < program.GroupCount; slot++)
            {
                var captures = expected.Groups[program.GroupNumbers[slot + 1]].Captures;
                Assert.Equal(captures.Select(static capture => capture.Index), outcome.CaptureStarts?[slot]);
                Assert.Equal(captures.Select(static capture => capture.Length), outcome.CaptureLengths?[slot]);
                Assert.Equal(captures.Count == 0 ? -1 : captures[^1].Index, starts[slot]);
                Assert.Equal(captures.Count == 0 ? -1 : captures[^1].Index + captures[^1].Length, ends[slot]);
            }
        }
        finally
        {
            Utf8SliceRunner.ReleaseHistory(outcome.CaptureStarts, outcome.CaptureLengths, ref invocation);
            Assert.Equal(7UL, invocation.Ledger.ReservedBytes);
            invocation.Ledger.Release(7);
        }
    }
}
