using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Internal.FrontEnd;

internal static partial class Utf8AsciiSimplePatternLowerer
{
    // These are private speculative limits, not the public constructor ledger.
    // Lists, projected plans and subsequent native preparation still need aggregate accounting.
    private sealed class BranchLowerer(Utf8PreparationBudget budget)
    {
        private Utf8SliceInvocation _invocation = new(new Utf8ExecutionLimits(1_048_576, 0, 64 * 1024 * 1024),
            Regex.InfiniteMatchTimeout, CancellationToken.None);
        private readonly record struct Continuation(Utf8TreeNode Node, int ChildDepth, int NextChild,
            List<LoweredBranch>? Accumulated);

        internal bool TryLower(Utf8TreeNode node, int depth, out LoweredBranch[] branches)
        {
            branches = [];
            var pending = new Utf8InvocationStateBuffer<Continuation>();
            try
            {
                while (true)
                {
                    if (depth > MaxLoweringDepth || !budget.TryReserve(1, 0) ||
                        _invocation.Budget.TryNoteDepth((uint)depth + 1) != Utf8ExecutionLimitKind.None ||
                        _invocation.TryChargeInputStep() != Utf8ExecutionLimitKind.None) return false;

                    LoweredBranch[] result;
                    switch (node.Kind)
                    {
                        case Utf8TreeNodeKind.Capture:
                        case Utf8TreeNodeKind.Group:
                            if (node.ChildCount != 1) return false;
                            node = node.Child(0);
                            depth++;
                            continue;
                        case Utf8TreeNodeKind.Empty:
                            result = [new([], [], false, false, false, false)];
                            break;
                        case Utf8TreeNodeKind.Bol:
                        case Utf8TreeNodeKind.Beginning:
                        case Utf8TreeNodeKind.Start:
                            // Beginning is absolute; Start is the contiguous origin.
                            // This plan's downstream origin handling remains separately audited.
                            result = [new([], [], true, false,
                                node.Kind is Utf8TreeNodeKind.Beginning or Utf8TreeNodeKind.Start, false)];
                            break;
                        case Utf8TreeNodeKind.Eol:
                        case Utf8TreeNodeKind.EndZ:
                        case Utf8TreeNodeKind.End:
                            result = [new([], [], false, true, false,
                                node.Kind is Utf8TreeNodeKind.EndZ or Utf8TreeNodeKind.End)];
                            break;
                        case Utf8TreeNodeKind.One:
                        case Utf8TreeNodeKind.Oneloop:
                        case Utf8TreeNodeKind.Set:
                        case Utf8TreeNodeKind.Setloop:
                            AsciiSimplePatternToken token;
                            if (!budget.TryReserve(1, 1)) return false;
                            if (node.Kind is Utf8TreeNodeKind.One or Utf8TreeNodeKind.Oneloop)
                            {
                                if (!TryLowerLiteral(node.Ch, out token)) return false;
                            }
                            else if (!Utf8ScalarAsciiProjector.TryCreateToken(node.ScalarClass, out token)) return false;
                            result = [new([token], [], false, false, false, false)];
                            if ((node.Kind is Utf8TreeNodeKind.Oneloop or Utf8TreeNodeKind.Setloop) &&
                                !TryExpandFiniteRepeat(result, node.M, node.N, budget, out result)) return false;
                            break;
                        case Utf8TreeNodeKind.Multi:
                            if (!TryLowerLiteralText(node.Str, budget, out var tokens)) return false;
                            result = [new(tokens, [], false, false, false, false)];
                            break;
                        case Utf8TreeNodeKind.Loop:
                            if (node.ChildCount != 1 || node.N == int.MaxValue || node.N < node.M) return false;
                            if (pending.TryPush(new(node, depth + 1, 1, null), ref _invocation) != Utf8ExecutionLimitKind.None) return false;
                            node = node.Child(0);
                            depth++;
                            continue;
                        case Utf8TreeNodeKind.PositiveLookaround:
                            // The former helper and its child each consumed one depth unit.
                            if (depth + 1 > MaxLoweringDepth || node.ChildCount != 1 ||
                                (node.Options & RegexOptions.RightToLeft) != 0) return false;
                            if (pending.TryPush(new(node, depth + 2, 1, null), ref _invocation) != Utf8ExecutionLimitKind.None) return false;
                            node = node.Child(0);
                            depth += 2;
                            continue;
                        case Utf8TreeNodeKind.Concatenate:
                        case Utf8TreeNodeKind.Alternate:
                            if (depth + 1 > MaxLoweringDepth) return false;
                            var accumulated = node.Kind == Utf8TreeNodeKind.Concatenate
                                ? new List<LoweredBranch> { new([], [], false, false, false, false) }
                                : new List<LoweredBranch>(Math.Min(node.ChildCount, MaxExpandedBranches));
                            if (node.ChildCount == 0)
                            {
                                result = [.. accumulated];
                                break;
                            }
                            if (pending.TryPush(new(node, depth + 2, 1, accumulated), ref _invocation) != Utf8ExecutionLimitKind.None) return false;
                            node = node.Child(0);
                            depth += 2;
                            continue;
                        default:
                            return false;
                    }

                    var descend = false;
                    while (pending.Count != 0)
                    {
                        if (_invocation.TryChargeInputStep() != Utf8ExecutionLimitKind.None) return false;
                        var continuation = pending[pending.Count - 1];
                        pending.Pop();
                        if (continuation.Node.Kind == Utf8TreeNodeKind.Loop)
                        {
                            if (!TryExpandFiniteRepeat(result, continuation.Node.M, continuation.Node.N, budget, out result)) return false;
                            continue;
                        }
                        if (continuation.Node.Kind == Utf8TreeNodeKind.PositiveLookaround)
                        {
                            if (result.Length != 1) return false;
                            var child = result[0];
                            if (child.IsStartAnchored || child.IsEndAnchored || child.FixedChecks.Count != 0 ||
                                !budget.TryReserve(child.Tokens.Count, child.Tokens.Count)) return false;
                            var literal = ExtractFullLiteral(child.Tokens);
                            if (literal.Length == 0) return false;
                            result = [new([], [new AsciiFixedLiteralCheck(0, literal)], false, false, false, false)];
                            continue;
                        }

                        var branchesSoFar = continuation.Accumulated ?? throw new InvalidOperationException("Missing branch continuation.");
                        if (continuation.Node.Kind == Utf8TreeNodeKind.Alternate)
                        {
                            if (result.Length > MaxExpandedBranches - branchesSoFar.Count ||
                                !budget.TryReserve(result.Length, result.Length)) return false;
                            branchesSoFar.AddRange(result);
                        }
                        else if (branchesSoFar.Count == 1 && result.Length == 1 &&
                            !branchesSoFar[0].IsStartAnchored && !branchesSoFar[0].IsEndAnchored &&
                            !result[0].IsStartAnchored && !result[0].IsEndAnchored)
                        {
                            if (!TryAppendBranch(branchesSoFar[0], result[0], budget)) return false;
                        }
                        else if (!TryConcatenate(branchesSoFar, result, budget, out branchesSoFar)) return false;

                        if (continuation.NextChild < continuation.Node.ChildCount)
                        {
                            if (pending.TryPush(continuation with { NextChild = continuation.NextChild + 1,
                                Accumulated = branchesSoFar }, ref _invocation) != Utf8ExecutionLimitKind.None) return false;
                            node = continuation.Node.Child(continuation.NextChild);
                            depth = continuation.ChildDepth;
                            descend = true;
                            break;
                        }
                        result = [.. branchesSoFar];
                    }
                    if (descend) continue;
                    branches = result;
                    return true;
                }
            }
            finally { pending.Release(ref _invocation); }
        }
    }
}
