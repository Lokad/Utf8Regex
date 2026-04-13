namespace Lokad.Utf8Regex.Internal.Execution;

using RuntimeFrontEnd = Lokad.Utf8Regex.Internal.FrontEnd.Runtime;

internal static partial class Utf8AsciiSimplePatternLowerer
{
    private static bool TryLowerBranches(RuntimeFrontEnd.RegexNode node, out LoweredBranch[] branches)
    {
        switch (node.Kind)
        {
            case RuntimeFrontEnd.RegexNodeKind.Capture:
            case RuntimeFrontEnd.RegexNodeKind.Group:
                if (node.ChildCount != 1)
                {
                    branches = [];
                    return false;
                }

                return TryLowerBranches(node.Child(0), out branches);

            case RuntimeFrontEnd.RegexNodeKind.Empty:
                branches = [new LoweredBranch([], [], false, false)];
                return true;

            case RuntimeFrontEnd.RegexNodeKind.Bol:
            case RuntimeFrontEnd.RegexNodeKind.Beginning:
            case RuntimeFrontEnd.RegexNodeKind.Start:
                branches = [new LoweredBranch([], [], true, false)];
                return true;

            case RuntimeFrontEnd.RegexNodeKind.Eol:
            case RuntimeFrontEnd.RegexNodeKind.EndZ:
            case RuntimeFrontEnd.RegexNodeKind.End:
                branches = [new LoweredBranch([], [], false, true)];
                return true;

            case RuntimeFrontEnd.RegexNodeKind.One:
                if (!TryLowerLiteral(node.Ch, out var literalToken))
                {
                    branches = [];
                    return false;
                }

                branches = [new LoweredBranch([literalToken], [], false, false)];
                return true;

            case RuntimeFrontEnd.RegexNodeKind.Multi:
                if (!TryLowerLiteralText(node.Str, out var literalTokens))
                {
                    branches = [];
                    return false;
                }

                branches = [new LoweredBranch(literalTokens, [], false, false)];
                return true;

            case RuntimeFrontEnd.RegexNodeKind.Set:
                if (!TryLowerSet(node.Str, out var setToken))
                {
                    branches = [];
                    return false;
                }

                branches = [new LoweredBranch([setToken], [], false, false)];
                return true;

            case RuntimeFrontEnd.RegexNodeKind.PositiveLookaround:
                return TryLowerPositiveLookaround(node, out branches);

            case RuntimeFrontEnd.RegexNodeKind.Concatenate:
                return TryLowerConcatenation(node, out branches);

            case RuntimeFrontEnd.RegexNodeKind.Alternate:
                return TryLowerAlternation(node, out branches);

            case RuntimeFrontEnd.RegexNodeKind.Oneloop:
                if (!TryLowerLiteral(node.Ch, out var repeatedLiteral))
                {
                    branches = [];
                    return false;
                }

                return TryExpandFiniteRepeat(
                    [new LoweredBranch([repeatedLiteral], [], false, false)],
                    node.M,
                    node.N,
                    out branches);

            case RuntimeFrontEnd.RegexNodeKind.Setloop:
                if (!TryLowerSet(node.Str, out var repeatedSet))
                {
                    branches = [];
                    return false;
                }

                return TryExpandFiniteRepeat(
                    [new LoweredBranch([repeatedSet], [], false, false)],
                    node.M,
                    node.N,
                    out branches);

            case RuntimeFrontEnd.RegexNodeKind.Loop:
                if (node.ChildCount != 1 || node.N == int.MaxValue || node.N < node.M)
                {
                    branches = [];
                    return false;
                }

                if (!TryLowerBranches(node.Child(0), out var loopBranches))
                {
                    branches = [];
                    return false;
                }

                return TryExpandFiniteRepeat(loopBranches, node.M, node.N, out branches);

            default:
                branches = [];
                return false;
        }
    }

    private static bool AllowsTrailingNewlineBeforeEnd(RuntimeFrontEnd.RegexNode node)
    {
        switch (node.Kind)
        {
            case RuntimeFrontEnd.RegexNodeKind.Eol:
            case RuntimeFrontEnd.RegexNodeKind.EndZ:
                return true;

            case RuntimeFrontEnd.RegexNodeKind.Capture:
            case RuntimeFrontEnd.RegexNodeKind.Group:
                return node.ChildCount == 1 && AllowsTrailingNewlineBeforeEnd(node.Child(0));

            case RuntimeFrontEnd.RegexNodeKind.Concatenate:
                return node.ChildCount > 0 && AllowsTrailingNewlineBeforeEnd(node.Child(node.ChildCount - 1));

            case RuntimeFrontEnd.RegexNodeKind.Alternate:
                if (node.ChildCount == 0)
                {
                    return false;
                }

                for (var i = 0; i < node.ChildCount; i++)
                {
                    if (!AllowsTrailingNewlineBeforeEnd(node.Child(i)))
                    {
                        return false;
                    }
                }

                return true;

            default:
                return false;
        }
    }

    private static bool TryLowerConcatenation(RuntimeFrontEnd.RegexNode node, out LoweredBranch[] branches)
    {
        var accumulated = new List<LoweredBranch> { new([], [], false, false) };

        for (var i = 0; i < node.ChildCount; i++)
        {
            if (!TryLowerBranches(node.Child(i), out var childBranches))
            {
                branches = [];
                return false;
            }

            if (!TryConcatenate(accumulated, childBranches, out accumulated))
            {
                branches = [];
                return false;
            }
        }

        branches = [.. accumulated];
        return true;
    }

    private static bool TryLowerAlternation(RuntimeFrontEnd.RegexNode node, out LoweredBranch[] branches)
    {
        var alternates = new List<LoweredBranch>(node.ChildCount);
        for (var i = 0; i < node.ChildCount; i++)
        {
            if (!TryLowerBranches(node.Child(i), out var childBranches))
            {
                branches = [];
                return false;
            }

            alternates.AddRange(childBranches);
            if (alternates.Count > MaxExpandedBranches)
            {
                branches = [];
                return false;
            }
        }

        branches = [.. alternates];
        return true;
    }

    private static bool TryConcatenate(
        List<LoweredBranch> leftBranches,
        IReadOnlyList<LoweredBranch> rightBranches,
        out List<LoweredBranch> combined)
    {
        if (leftBranches.Count == 0 || rightBranches.Count == 0)
        {
            combined = [];
            return false;
        }

        if (leftBranches.Count * rightBranches.Count > MaxExpandedBranches)
        {
            combined = [];
            return false;
        }

        combined = new List<LoweredBranch>(leftBranches.Count * rightBranches.Count);
        foreach (var left in leftBranches)
        {
            foreach (var right in rightBranches)
            {
                if (!TryCombineBranches(left, right, out var branch))
                {
                    return false;
                }

                combined.Add(branch);
            }
        }

        return true;
    }

    private static bool TryCombineBranches(LoweredBranch left, LoweredBranch right, out LoweredBranch branch)
    {
        if (right.IsStartAnchored && (left.IsStartAnchored || left.IsEndAnchored || left.Tokens.Count != 0))
        {
            branch = default;
            return false;
        }

        if (left.IsEndAnchored && (right.IsStartAnchored || right.IsEndAnchored || right.Tokens.Count != 0))
        {
            branch = default;
            return false;
        }

        var combinedTokens = new List<AsciiSimplePatternToken>(left.Tokens.Count + right.Tokens.Count);
        combinedTokens.AddRange(left.Tokens);
        combinedTokens.AddRange(right.Tokens);

        var combinedChecks = new List<AsciiFixedLiteralCheck>(left.FixedChecks.Count + right.FixedChecks.Count);
        combinedChecks.AddRange(left.FixedChecks);
        foreach (var check in right.FixedChecks)
        {
            combinedChecks.Add(new AsciiFixedLiteralCheck(left.Tokens.Count + check.Offset, check.Literal));
        }

        branch = new LoweredBranch(
            combinedTokens,
            combinedChecks,
            left.IsStartAnchored || right.IsStartAnchored,
            left.IsEndAnchored || right.IsEndAnchored);
        return true;
    }

    private static bool TryExpandFiniteRepeat(
        IReadOnlyList<LoweredBranch> repeatedBranches,
        int minCount,
        int maxCount,
        out LoweredBranch[] branches)
    {
        branches = [];
        if (minCount < 0 || maxCount < minCount || maxCount == int.MaxValue)
        {
            return false;
        }

        var variants = new List<LoweredBranch>();
        for (var count = maxCount; count >= minCount; count--)
        {
            var expanded = new List<LoweredBranch> { new([], [], false, false) };
            for (var i = 0; i < count; i++)
            {
                if (!TryConcatenate(expanded, repeatedBranches, out expanded))
                {
                    return false;
                }
            }

            variants.AddRange(expanded);
            if (variants.Count > MaxExpandedBranches)
            {
                return false;
            }
        }

        branches = [.. variants];
        return true;
    }

    private static bool TryLowerPositiveLookaround(RuntimeFrontEnd.RegexNode node, out LoweredBranch[] branches)
    {
        branches = [];
        if (node.ChildCount != 1 ||
            !TryLowerBranches(node.Child(0), out var childBranches) ||
            childBranches.Length != 1)
        {
            return false;
        }

        var child = childBranches[0];
        if (child.IsStartAnchored || child.IsEndAnchored || child.FixedChecks.Count != 0)
        {
            return false;
        }

        var literal = ExtractFullLiteral(child.Tokens);
        if (literal.Length == 0)
        {
            return false;
        }

        branches = [new LoweredBranch([], [new AsciiFixedLiteralCheck(0, literal)], false, false)];
        return true;
    }
}
