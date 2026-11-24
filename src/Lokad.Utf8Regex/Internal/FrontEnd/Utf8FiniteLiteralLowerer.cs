using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Planning;
namespace Lokad.Utf8Regex.Internal.FrontEnd;


internal static partial class Utf8FiniteLiteralLowerer
{
    private const int MaxExpandedBranches = 64;
    private const int MaxLiteralCharClassExpansion = 4;
    private const int MaxPreparationWork = 4096;
    private const int MaxPreparationOutput = 4096;
    // Keep the optimizer's existing admission boundary after removing CLR recursion.
    // Declining this speculative plan leaves the other owned execution routes available.
    private const int MaxLoweringDepth = 256;

    // This optimizer returns only finite literal alternatives. It never supplies
    // an execution engine or drops an assertion from the matching language.
    public static bool TryCreateLiteralBranches(Utf8SemanticRegex semanticRegex,
        RegexOptions options, out byte[][] literals)
    {
        literals = [];
        if (semanticRegex.RuntimeTree?.Root is not { Kind: Utf8TreeNodeKind.Capture, ChildCount: 1 } root) return false;
        using var construction = Utf8ConstructionScope.Enter();
        using var storage = Utf8ConstructionSession.Create(construction.Budget);
        var preparation = construction.Budget.AllocateObject((MaxPreparationWork, MaxPreparationOutput),
            static state => new Utf8PreparationBudget(state.MaxPreparationWork, state.MaxPreparationOutput));
        var ignoreCase = (options & RegexOptions.IgnoreCase) != 0;
        if (!TryLowerBranches(storage, root.Child(0), preparation, 0, ignoreCase, out var lowered) || lowered.Count == 0)
            return false;
        var branches = storage.Allocate<AsciiPatternToken[]>(lowered.Count, false);
        for (var index = 0; index < lowered.Count; index++)
        {
            storage.Budget.ChargeWork(1);
            var branch = lowered[index];
            if (branch.Tokens.Count == 0)
                return false;
            branches[index] = storage.Copy(branch.Tokens, false);
        }
        if (ignoreCase) NormalizeBranchesForIgnoreCase(branches);
        if (!TryExpandLiteralBranches(branches, ignoreCase, out literals)) return false;
        storage.Complete();
        construction.Complete();
        return true;
    }

    private static bool TryLowerLiteral(char ch, out AsciiPatternToken token)
    {
        if (ch > 0x7F)
        {
            token = default;
            return false;
        }

        token = new AsciiPatternToken((byte)ch);
        return true;
    }

    private static bool TryLowerLiteralText(Utf8ConstructionSession storage, Utf8TreeNode node, Utf8PreparationBudget budget, out Utf8ConstructionBuffer<AsciiPatternToken> tokens)
    {
        tokens = storage.Buffer<AsciiPatternToken>();
        if (node.LiteralUtf8 is { } bytes)
        {
            if (!budget.TryReserve(bytes.Length, bytes.Length)) return false;
            foreach (var value in bytes)
            {
                Utf8ConstructionScope.ChargeWork(1);
                if (value > 0x7f) return false;
            }
            foreach (var value in bytes) { Utf8ConstructionScope.ChargeWork(1); tokens.Add(new AsciiPatternToken(value)); }
            return true;
        }
        var text = node.Str;
        if (text is null || !budget.TryReserve(text.Length, text.Length))
        {
            return false;
        }

        foreach (var ch in text)
        {
            Utf8ConstructionScope.ChargeWork(1);
            if (ch > 0x7F) return false;
        }

        foreach (var ch in text) { Utf8ConstructionScope.ChargeWork(1); tokens.Add(new AsciiPatternToken((byte)ch)); }

        return true;
    }

    private readonly record struct LoweredBranch(Utf8ConstructionBuffer<AsciiPatternToken> Tokens);
}
