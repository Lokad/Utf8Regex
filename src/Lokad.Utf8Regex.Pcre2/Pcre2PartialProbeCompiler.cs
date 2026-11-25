using Lokad.Utf8Regex.Internal;

namespace Lokad.Utf8Regex.Pcre2;

internal enum Pcre2PartialProbeProgramKind : byte
{
    None = 0,
    LiteralDotStar = 1,
    AbPlus = 2,
    LiteralAlternation = 3,
    OrderedLiteralAlternation = 4,
    WordBoundaryLiteral = 5,
    InspectedPrefixLiteral = 6,
    InspectedContextLiteral = 7,
    EndAssertion = 8,
    TrailingCWithLookbehind = 9,
    NegativeStartClassDotStar = 10,
    AnchoredNewlineSequence = 11,
    AtomicAPlusB = 13,
    RepeatedLiteral = 14,
    ConditionalAbcDotStarOrZ = 15,
    LiteralPlusTerminal = 16,
    AnchoredAPlusWord = 17,
    AnchoredAaOrAPlusWord = 18,
    CapturedEndAlternative = 19,
    CrlfDotQuantifier = 20,
    TrailingLiteralAssertion = 21,
    AnchoredPrefixLiteral = 22,
    AnchoredExactLiteral = 23,
}

internal sealed class Pcre2PartialProbeProgram
{
    internal static Pcre2PartialProbeProgram None { get; } = new(
        Pcre2PartialProbeProgramKind.None,
        [],
        0,
        0,
        false,
        '\0');

    internal Pcre2PartialProbeProgram(
        Pcre2PartialProbeProgramKind kind,
        byte[][] operands,
        int first,
        int second,
        bool flag,
        char terminal)
    {
        Kind = kind;
        Operands = operands;
        First = first;
        Second = second;
        Flag = flag;
        Terminal = terminal;
    }

    internal Pcre2PartialProbeProgramKind Kind { get; }

    internal byte[][] Operands { get; }

    internal int First { get; }

    internal int Second { get; }

    internal bool Flag { get; }

    internal char Terminal { get; }

}

internal static partial class Pcre2PartialProbeCompiler
{
    internal static Pcre2PartialProbeProgram Compile(IPcre2BacktrackingNode root, Pcre2CompileRequest request)
    {
        using var construction = Utf8ConstructionScope.Enter();
        var budget = construction.Budget;
        // Flat token/literal recognition borrows and meters the parsed nodes.
        // Other curated recognizers inspect only fixed portions of the tree.
        budget.ChargeWork(128);
        budget.EnterDepth();
        try
        {
            var program = TryCompileTokenSequence(root, request, out var tokens)
                ? tokens : CompileTree(root, request);
            if (program.Kind != Pcre2PartialProbeProgramKind.None) construction.Complete();
            return program;
        }
        finally { budget.LeaveDepth(); }
    }

    private static bool TryCompileTokenSequence(
        IPcre2BacktrackingNode root,
        Pcre2CompileRequest request,
        out Pcre2PartialProbeProgram program)
    {
        program = Pcre2PartialProbeProgram.None;
        if (root is not (Pcre2TokenBacktrackingNode or Pcre2SequenceBacktrackingNode)) return false;
        ReadOnlySpan<IPcre2BacktrackingNode> nodes = root is Pcre2SequenceBacktrackingNode sequence
            ? sequence.Children : new ReadOnlySpan<IPcre2BacktrackingNode>(in root);
        if (nodes.IsEmpty) return false;
        // Reject unrelated trees at their first non-token rather than copying or
        // serializing them. Admission/checkpoints cover every inspected flat node.
        var budget = Utf8ConstructionScope.CurrentBudget;
        foreach (var node in nodes)
        {
            budget.ChargeWork(1);
            if (node is not Pcre2TokenBacktrackingNode) return false;
        }
        var firstToken = ((Pcre2TokenBacktrackingNode)nodes[0]).Token;
        var lastToken = ((Pcre2TokenBacktrackingNode)nodes[^1]).Token;
        if (nodes.Length == 1 && firstToken.Kind is
            Pcre2CharacterTokenKind.EndOfSubject or Pcre2CharacterTokenKind.EndOfSubjectOrFinalNewline)
        {
            program = Create(Pcre2PartialProbeProgramKind.EndAssertion,
                flag: firstToken.Kind == Pcre2CharacterTokenKind.EndOfSubjectOrFinalNewline);
            return true;
        }
        if (nodes.Length == 2 &&
            firstToken.Kind is Pcre2CharacterTokenKind.BeginningOfLine or Pcre2CharacterTokenKind.BeginningOfSubject &&
            lastToken.Kind == Pcre2CharacterTokenKind.NewlineSequence)
        {
            program = Create(Pcre2PartialProbeProgramKind.AnchoredNewlineSequence, first: 1, second: 1);
            return true;
        }
        if (request.Settings.Newline == Pcre2NewlineConvention.Crlf && nodes.Length == 1 &&
            firstToken.Kind is Pcre2CharacterTokenKind.Any or Pcre2CharacterTokenKind.AnyNotNewline)
        {
            program = Create(Pcre2PartialProbeProgramKind.CrlfDotQuantifier, first: 1, second: 1);
            return true;
        }
        if (TryGetLiteral(nodes, out var literal, Pcre2CharacterOptions.None) && literal.Length > 0)
        {
            program = Create(Pcre2PartialProbeProgramKind.LiteralAlternation, literal);
            return true;
        }
        var anchored = firstToken.Kind is Pcre2CharacterTokenKind.BeginningOfLine or Pcre2CharacterTokenKind.BeginningOfSubject;
        var first = anchored ? 1 : 0;
        var last = nodes.Length;
        Pcre2CharacterTokenKind? trailing = null;
        if (last > first && lastToken.Kind is Pcre2CharacterTokenKind.EndOfLine or
            Pcre2CharacterTokenKind.EndOfSubject or Pcre2CharacterTokenKind.EndOfSubjectOrFinalNewline or
            Pcre2CharacterTokenKind.WordBoundary or Pcre2CharacterTokenKind.NonWordBoundary)
        {
            trailing = lastToken.Kind;
            last--;
        }
        if (first < last && ((Pcre2TokenBacktrackingNode)nodes[first]).Token.Kind == Pcre2CharacterTokenKind.WordBoundary &&
            trailing == Pcre2CharacterTokenKind.WordBoundary &&
            TryGetLiteral(nodes[(first + 1)..last], out literal, Pcre2CharacterOptions.Multiline | Pcre2CharacterOptions.DotAll))
        {
            program = Create(Pcre2PartialProbeProgramKind.WordBoundaryLiteral, literal);
            return true;
        }
        if (TryGetLiteral(nodes[first..last], out literal,
            Pcre2CharacterOptions.Multiline | Pcre2CharacterOptions.DotAll) && literal.Length > 0)
        {
            if (anchored && trailing is not null)
            {
                program = Create(Pcre2PartialProbeProgramKind.AnchoredExactLiteral, literal);
                return true;
            }
            if (anchored)
            {
                program = Create(Pcre2PartialProbeProgramKind.AnchoredPrefixLiteral, literal);
                return true;
            }
            if (trailing is not null)
            {
                program = Create(Pcre2PartialProbeProgramKind.TrailingLiteralAssertion, literal);
                return true;
            }
        }
        return false;
    }

    private static bool TryGetLiteral(ReadOnlySpan<IPcre2BacktrackingNode> nodes, out byte[] literal,
        Pcre2CharacterOptions inertOptions)
    {
        var budget = Utf8ConstructionScope.CurrentBudget;
        budget.ChargeWork((ulong)nodes.Length + 1);
        var length = 0;
        for (var index = 0; index < nodes.Length; index++)
        {
            if ((index & 255) == 0) budget.ChargeWork(0);
            var token = ((Pcre2TokenBacktrackingNode)nodes[index]).Token;
            if (token.Kind != Pcre2CharacterTokenKind.Literal ||
                (token.Options & ~inertOptions) != Pcre2CharacterOptions.None)
            {
                literal = [];
                return false;
            }
            length = checked(length + token.Literal.Utf8SequenceLength);
        }
        literal = Pcre2LiteralCompiler.EncodeLiteralTokens(nodes, length, budget);
        return true;
    }

    private static Pcre2PartialProbeProgram Create(Pcre2PartialProbeProgramKind kind,
        string? operand0 = null, string? operand1 = null, string? operand2 = null,
        int first = 0, int second = 0, bool flag = false, char terminal = '\0')
    {
        var budget = Utf8ConstructionScope.CurrentBudget;
        var count = operand2 is not null ? 3 : operand1 is not null ? 2 : operand0 is not null ? 1 : 0;
        var operands = budget.AllocateArray<byte[]>(count, false);
        if (operand0 is not null) operands[0] = budget.EncodeUtf8(operand0);
        if (operand1 is not null) operands[1] = budget.EncodeUtf8(operand1);
        if (operand2 is not null) operands[2] = budget.EncodeUtf8(operand2);
        return Publish(kind, operands, first, second, flag, terminal);
    }

    private static Pcre2PartialProbeProgram Create(Pcre2PartialProbeProgramKind kind, byte[] operand)
    {
        var operands = Utf8ConstructionScope.CurrentBudget.AllocateArray<byte[]>(1, false);
        operands[0] = operand;
        return Publish(kind, operands, 0, 0, false, '\0');
    }

    private static Pcre2PartialProbeProgram Publish(Pcre2PartialProbeProgramKind kind,
        byte[][] operands, int first, int second, bool flag, char terminal)
        => Utf8ConstructionScope.CurrentBudget.AllocateObject((kind, operands, first, second, flag, terminal),
            static state => new Pcre2PartialProbeProgram(state.kind, state.operands,
                state.first, state.second, state.flag, state.terminal));
}
