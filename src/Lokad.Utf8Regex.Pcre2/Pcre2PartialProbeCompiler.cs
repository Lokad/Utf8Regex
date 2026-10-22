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
    internal static Pcre2PartialProbeProgram Compile(Pcre2CharacterProgram characterProgram, Pcre2CompileRequest request)
    {
        using var construction = Utf8ConstructionScope.Enter();
        var budget = construction.Budget;
        budget.ChargeWork(1);
        budget.EnterDepth();
        try
        {
            if (!TryCompileCharacterProgram(characterProgram.Tokens, request, out var program))
                return Pcre2PartialProbeProgram.None;
            construction.Complete();
            return program;
        }
        finally { budget.LeaveDepth(); }
    }

    internal static Pcre2PartialProbeProgram Compile(IPcre2BacktrackingNode root, Pcre2CompileRequest request)
    {
        using var construction = Utf8ConstructionScope.Enter();
        var budget = construction.Budget;
        // The closed recognizers inspect at most a fixed number of nodes and
        // constant-length texts. No traversal of an unrelated tree is needed.
        budget.ChargeWork(128);
        budget.EnterDepth();
        try
        {
            var program = CompileTree(root, request);
            if (program.Kind != Pcre2PartialProbeProgramKind.None) construction.Complete();
            return program;
        }
        finally { budget.LeaveDepth(); }
    }

    private static bool TryCompileCharacterProgram(
        Pcre2CharacterToken[] tokens,
        Pcre2CompileRequest request,
        out Pcre2PartialProbeProgram program)
    {
        if (tokens.Length == 1 &&
            tokens[0].Kind is Pcre2CharacterTokenKind.EndOfSubject or Pcre2CharacterTokenKind.EndOfSubjectOrFinalNewline)
        {
            program = Create(Pcre2PartialProbeProgramKind.EndAssertion,
                flag: tokens[0].Kind == Pcre2CharacterTokenKind.EndOfSubjectOrFinalNewline);
            return true;
        }

        if (tokens.Length == 2 &&
            tokens[0].Kind is Pcre2CharacterTokenKind.BeginningOfLine or Pcre2CharacterTokenKind.BeginningOfSubject &&
            tokens[1].Kind == Pcre2CharacterTokenKind.NewlineSequence)
        {
            program = Create(Pcre2PartialProbeProgramKind.AnchoredNewlineSequence, first: 1, second: 1);
            return true;
        }

        if (request.Settings.Newline == Pcre2NewlineConvention.Crlf &&
            tokens.Length == 1 &&
            tokens[0].Kind is Pcre2CharacterTokenKind.Any or Pcre2CharacterTokenKind.AnyNotNewline)
        {
            program = Create(Pcre2PartialProbeProgramKind.CrlfDotQuantifier, first: 1, second: 1);
            return true;
        }

        if (TryGetLiteral(tokens, 0, tokens.Length, out var literal, Pcre2CharacterOptions.None) &&
            literal.Length > 0)
        {
            program = Create(Pcre2PartialProbeProgramKind.LiteralAlternation, literal);
            return true;
        }

        var first = 0;
        var anchored = tokens.Length > 0 &&
            tokens[0].Kind is Pcre2CharacterTokenKind.BeginningOfLine or Pcre2CharacterTokenKind.BeginningOfSubject;
        if (anchored)
        {
            first++;
        }

        var last = tokens.Length;
        Pcre2CharacterTokenKind? trailing = null;
        if (last > first && tokens[last - 1].Kind is Pcre2CharacterTokenKind.EndOfLine or
            Pcre2CharacterTokenKind.EndOfSubject or Pcre2CharacterTokenKind.EndOfSubjectOrFinalNewline or
            Pcre2CharacterTokenKind.WordBoundary or Pcre2CharacterTokenKind.NonWordBoundary)
        {
            trailing = tokens[last - 1].Kind;
            last--;
        }

        if (first < last &&
            tokens[first].Kind == Pcre2CharacterTokenKind.WordBoundary &&
            trailing == Pcre2CharacterTokenKind.WordBoundary &&
            TryGetLiteral(tokens, first + 1, last, out literal, Pcre2CharacterOptions.Multiline | Pcre2CharacterOptions.DotAll))
        {
            program = Create(Pcre2PartialProbeProgramKind.WordBoundaryLiteral, literal);
            return true;
        }

        if (TryGetLiteral(tokens, first, last, out literal,
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

        program = Pcre2PartialProbeProgram.None;
        return false;
    }

    private static bool TryGetLiteral(Pcre2CharacterToken[] tokens, int first, int last, out byte[] literal,
        Pcre2CharacterOptions inertOptions)
    {
        var budget = Utf8ConstructionScope.CurrentBudget;
        budget.ChargeWork((ulong)(last - first) + 1);
        var length = 0;
        for (var index = first; index < last; index++)
        {
            if (((index - first) & 255) == 0) budget.ChargeWork(0);
            var token = tokens[index];
            if (token.Kind != Pcre2CharacterTokenKind.Literal ||
                (token.Options & ~inertOptions) != Pcre2CharacterOptions.None)
            {
                literal = [];
                return false;
            }
            length = checked(length + token.Literal.Utf8SequenceLength);
        }
        literal = budget.AllocateArray<byte>(length, false);
        var written = 0;
        budget.ChargeWork((ulong)(last - first) + 1);
        for (var index = first; index < last; index++)
        {
            if (((index - first) & 255) == 0) budget.ChargeWork(0);
            written += tokens[index].Literal.EncodeToUtf8(literal.AsSpan(written));
        }
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
