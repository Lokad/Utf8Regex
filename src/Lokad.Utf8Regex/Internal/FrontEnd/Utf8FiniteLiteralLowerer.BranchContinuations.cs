using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Internal.FrontEnd;

internal static partial class Utf8FiniteLiteralLowerer
{
    // Private optimizer limits qualify eligibility; the parent construction
    // owner independently admits all actual work, storage and live depth.
    private sealed class BranchLowerer(Utf8PreparationBudget budget, Utf8ConstructionSession storage)
    {
        private struct Frame(Utf8TreeNode node, int depth)
        {
            internal readonly Utf8TreeNode Node = node;
            internal readonly int Depth = depth;
            internal int Stage;
            internal int Index;
            internal Utf8ConstructionBuffer<LoweredBranch>? Accumulated;
        }

        internal static BranchLowerer Create(Utf8PreparationBudget budget, Utf8ConstructionSession storage) =>
            storage.Budget.AllocateObject((budget, storage), static state => new BranchLowerer(state.budget, state.storage));

        internal bool TryLower(Utf8TreeNode node, int depth, bool ignoreCase, out Utf8ConstructionBuffer<LoweredBranch> branches)
        {
            branches = storage.Buffer<LoweredBranch>();
            using var frames = Utf8ConstructionFrames<Frame>.Create(storage.Budget, new(node, depth));
            Utf8ConstructionBuffer<LoweredBranch>? completed = null;
            while (frames.Count != 0)
            {
                storage.Budget.ChargeWork(1);
                var index = frames.Count - 1;
                var frame = frames[index];
                node = frame.Node;
                Utf8ConstructionBuffer<LoweredBranch>? result = null;
                Frame? child = null;
                if (frame.Stage == 0)
                {
                    if (frame.Depth > MaxLoweringDepth || !budget.TryReserve(1, 0)) return false;
                    frame.Stage = 1;
                    switch (node.Kind)
                    {
                        case Utf8TreeNodeKind.Group when ignoreCase && (node.Options & RegexOptions.IgnoreCase) == 0:
                            // Folded branch normalization cannot honor a locally exact group.
                            return false;
                        case Utf8TreeNodeKind.Capture:
                        case Utf8TreeNodeKind.Group:
                            if (node.ChildCount != 1) return false;
                            child = new(node.Child(0), frame.Depth + 1);
                            break;
                        case Utf8TreeNodeKind.Empty:
                            result = SingleBranch(storage, EmptyBranch(storage));
                            break;
                        case Utf8TreeNodeKind.One:
                        case Utf8TreeNodeKind.Oneloop:
                        case Utf8TreeNodeKind.Set:
                        case Utf8TreeNodeKind.Setloop:
                            AsciiPatternToken token;
                            if (!budget.TryReserve(1, 1)) return false;
                            if (node.Kind is Utf8TreeNodeKind.One or Utf8TreeNodeKind.Oneloop)
                            {
                                if (!TryLowerLiteral(node.Ch, out token)) return false;
                            }
                            else
                            {
                                if (!Utf8ScalarAsciiProjector.TryCreateToken(node.ScalarClass, out token)) return false;
                                // A consuming variant must expand the class. Each folded
                                // ASCII value represents at most two members.
                                if (node.Kind == Utf8TreeNodeKind.Setloop && node.N > 0 &&
                                    token.Kind == AsciiPatternTokenKind.CharClass &&
                                    token.CharClass.Count > MaxLiteralCharClassExpansion * (ignoreCase ? 2 : 1)) return false;
                            }
                            var branch = EmptyBranch(storage);
                            branch.Tokens.Add(token);
                            result = SingleBranch(storage, branch);
                            if ((node.Kind is Utf8TreeNodeKind.Oneloop or Utf8TreeNodeKind.Setloop) &&
                                !TryExpandFiniteRepeat(storage, result, node.M, node.N, budget, out result)) return false;
                            break;
                        case Utf8TreeNodeKind.Multi:
                            if (!TryLowerLiteralText(storage, node, budget, out var tokens)) return false;
                            result = SingleBranch(storage, new(tokens));
                            break;
                        case Utf8TreeNodeKind.Loop:
                            if (node.ChildCount != 1 || node.N == int.MaxValue || node.N < node.M) return false;
                            child = new(node.Child(0), frame.Depth + 1);
                            break;
                        case Utf8TreeNodeKind.Concatenate:
                        case Utf8TreeNodeKind.Alternate:
                            if (frame.Depth + 1 > MaxLoweringDepth) return false;
                            frame.Accumulated = storage.Buffer<LoweredBranch>();
                            if (node.Kind == Utf8TreeNodeKind.Concatenate) frame.Accumulated.Add(EmptyBranch(storage));
                            if (node.ChildCount == 0) result = frame.Accumulated;
                            else child = new(node.Child(0), frame.Depth + 2);
                            break;
                        default:
                            return false;
                    }
                }
                else
                {
                    var value = completed ?? throw new InvalidOperationException("Missing completed branch continuation.");
                    switch (node.Kind)
                    {
                        case Utf8TreeNodeKind.Capture:
                        case Utf8TreeNodeKind.Group:
                            result = value;
                            break;
                        case Utf8TreeNodeKind.Loop:
                            if (!TryExpandFiniteRepeat(storage, value, node.M, node.N, budget, out result)) return false;
                            break;
                        default:
                            var accumulated = frame.Accumulated ?? throw new InvalidOperationException("Missing branch accumulation.");
                            if (node.Kind == Utf8TreeNodeKind.Alternate)
                            {
                                if (value.Count > MaxExpandedBranches - accumulated.Count ||
                                    !budget.TryReserve(value.Count, value.Count)) return false;
                                accumulated.AddRange(value.Span);
                            }
                            else if (accumulated.Count == 1 && value.Count == 1)
                            {
                                if (!TryAppendBranch(accumulated[0], value[0], budget)) return false;
                            }
                            else if (!TryConcatenate(storage, accumulated, value, budget, out accumulated)) return false;
                            frame.Accumulated = accumulated;
                            if (++frame.Index < node.ChildCount) child = new(node.Child(frame.Index), frame.Depth + 2);
                            else result = accumulated;
                            break;
                    }
                }
                frames[index] = frame;
                if (child is { } pending) frames.Push(pending);
                else
                {
                    completed = result ?? throw new InvalidOperationException("Missing branch result.");
                    frames.Pop();
                }
            }
            branches = completed ?? throw new InvalidOperationException("Missing lowered root.");
            return true;
        }
    }

    private static LoweredBranch EmptyBranch(Utf8ConstructionSession storage) =>
        new(storage.Buffer<AsciiPatternToken>());

    private static Utf8ConstructionBuffer<LoweredBranch> SingleBranch(Utf8ConstructionSession storage, LoweredBranch branch)
    {
        var result = storage.Buffer<LoweredBranch>();
        result.Add(branch);
        return result;
    }
}
