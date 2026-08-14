using System.Buffers;
using System.Globalization;
using System.Text;

namespace Lokad.Utf8Regex.Pcre2;

internal enum Pcre2PartialProbeProgramKind : byte
{
    None = 0,
    PartialSoftDotAllLiteral = 1,
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
    AnchoredOptionalNewlineThenX = 12,
    AnchoredAtomicAPlusB = 13,
    AnchoredRepeatedLiteral = 14,
    AnchoredConditionalAbcDotStarOrZ = 15,
    AnchoredLiteralPlusTerminal = 16,
    AnchoredAPlusWord = 17,
    AnchoredAaOrAPlusWord = 18,
    AnchoredACrlfEnd = 19,
    CrlfDotQuantifier = 20,
    TrailingLiteralAssertion = 21,
    AnchoredPrefixLiteral = 22,
    AnchoredExactLiteral = 23,
}

internal enum Pcre2PartialProbeTrailingAssertion : byte
{
    None = 0,
    Dollar = 1,
    EndAbsolute = 2,
    EndBeforeFinalNewline = 3,
    WordBoundary = 4,
    NonWordBoundary = 5,
}

internal sealed class Pcre2PartialProbeProgram
{
    internal static Pcre2PartialProbeProgram None { get; } = new(
        Pcre2PartialProbeProgramKind.None,
        [],
        0,
        0,
        false,
        '\0',
        Pcre2PartialProbeTrailingAssertion.None);

    internal Pcre2PartialProbeProgram(
        Pcre2PartialProbeProgramKind kind,
        byte[][] operands,
        int first,
        int second,
        bool flag,
        char terminal,
        Pcre2PartialProbeTrailingAssertion trailingAssertion)
    {
        Kind = kind;
        Operands = operands;
        First = first;
        Second = second;
        Flag = flag;
        Terminal = terminal;
        TrailingAssertion = trailingAssertion;
    }

    internal Pcre2PartialProbeProgramKind Kind { get; }

    internal byte[][] Operands { get; }

    internal int First { get; }

    internal int Second { get; }

    internal bool Flag { get; }

    internal char Terminal { get; }

    internal Pcre2PartialProbeTrailingAssertion TrailingAssertion { get; }
}

internal static class Pcre2PartialProbeCompiler
{
    internal static Pcre2PartialProbeProgram Compile(Pcre2CharacterProgram characterProgram, Pcre2CompileRequest request)
    {
        ArgumentNullException.ThrowIfNull(characterProgram);
        var tokens = characterProgram.Tokens;
        if (TryCompileCharacterProgram(tokens, request, out var program))
        {
            return program;
        }

        return Pcre2PartialProbeProgram.None;
    }

    internal static Pcre2PartialProbeProgram Compile(Pcre2BacktrackingSyntaxTree syntaxTree, Pcre2CompileRequest request)
    {
        ArgumentNullException.ThrowIfNull(syntaxTree);
        var shape = Describe(syntaxTree.Root);
        return shape switch
        {
            "S(T(0,0,66);R(0,2147483647,0:T(2,0));)" or
            "S(T(0,4,66);R(0,2147483647,0:T(2,4));)" => Create(Pcre2PartialProbeProgramKind.PartialSoftDotAllLiteral, "f"),
            "S(C(1:T(0,0,61));R(1,2147483647,0:T(0,0,62));)" => Create(Pcre2PartialProbeProgramKind.AbPlus),
            "A(S(T(0,0,63);T(0,0,61);T(0,0,74););S(T(0,0,68);T(0,0,6F);T(0,0,72);T(0,0,73);T(0,0,65););)" => Create(Pcre2PartialProbeProgramKind.LiteralAlternation, "cat", "horse"),
            "S(T(0,0,64);T(0,0,6F);T(0,0,67);R(0,1,0:C(1:S(T(0,0,73);T(0,0,62);T(0,0,6F);T(0,0,64);T(0,0,79);)));)" or
            "A(S(T(0,0,64);T(0,0,6F);T(0,0,67);T(0,0,73);T(0,0,62);T(0,0,6F);T(0,0,64);T(0,0,79););S(T(0,0,64);T(0,0,6F);T(0,0,67););)" => Create(Pcre2PartialProbeProgramKind.OrderedLiteralAlternation, "dogsbody", "dog"),
            "S(T(0,0,64);T(0,0,6F);T(0,0,67);R(0,1,1:C(1:S(T(0,0,73);T(0,0,62);T(0,0,6F);T(0,0,64);T(0,0,79);)));)" or
            "A(S(T(0,0,64);T(0,0,6F);T(0,0,67););S(T(0,0,64);T(0,0,6F);T(0,0,67);T(0,0,73);T(0,0,62);T(0,0,6F);T(0,0,64);T(0,0,79););)" => Create(Pcre2PartialProbeProgramKind.OrderedLiteralAlternation, "dog", "dogsbody"),
            "S(T(0,0,61);T(0,0,62);T(0,0,63);K;T(0,0,31);T(0,0,32);T(0,0,33);)" => Create(Pcre2PartialProbeProgramKind.InspectedPrefixLiteral, true, "abc", "123"),
            "S(Q(2:S(T(0,0,61);T(0,0,62);T(0,0,63);));T(0,0,31);T(0,0,32);T(0,0,33);)" => Create(Pcre2PartialProbeProgramKind.InspectedPrefixLiteral, false, "abc", "123"),
            "S(T(0,0,61);T(0,0,62);T(0,0,63);Q(0:S(T(0,0,78);T(0,0,79);T(0,0,7A);));)" => Create(Pcre2PartialProbeProgramKind.InspectedContextLiteral, false, string.Empty, "abc", "xyz"),
            "S(Q(2:S(T(0,0,70);T(0,0,71);T(0,0,72);));T(0,0,61);T(0,0,62);T(0,0,63);Q(0:S(T(0,0,78);T(0,0,79);T(0,0,7A);));)" => Create(Pcre2PartialProbeProgramKind.InspectedContextLiteral, true, "pqr", "abc", "xyz"),
            "S(T(0,0,61);T(0,0,62);T(0,0,63);Q(0:S(T(0,0,61);T(0,0,62);T(0,0,63);T(0,0,64);T(0,0,65);));Q(0:S(T(0,0,61);T(0,0,62);));)" => Create(Pcre2PartialProbeProgramKind.InspectedContextLiteral, false, string.Empty, "abc", "abcde"),
            "S(R(0,2147483647,2:T(0,0,63));Q(2:T(1,0,0:0,98,98,0,.0,99,99,0,.));)" => Create(Pcre2PartialProbeProgramKind.TrailingCWithLookbehind, false),
            "S(R(1,2147483647,2:T(0,0,63));Q(2:T(1,0,0:0,98,98,0,.0,99,99,0,.));)" => Create(Pcre2PartialProbeProgramKind.TrailingCWithLookbehind, true),
            "S(Q(1:T(1,0,0:0,97,97,0,.0,98,98,0,.));R(0,2147483647,0:T(2,0));)" => Create(Pcre2PartialProbeProgramKind.NegativeStartClassDotStar),
            "S(T(5,0);R(2,3,0:T(4,0));T(0,0,78);)" or
            "S(T(5,0);R(2,3,1:T(4,0));T(0,0,78);)" => Create(Pcre2PartialProbeProgramKind.AnchoredNewlineSequence, 2, 3, true),
            "S(T(5,0);R(0,1,0:T(4,0));T(0,0,78);)" => Create(Pcre2PartialProbeProgramKind.AnchoredOptionalNewlineThenX),
            "S(T(5,0);R(1,2147483647,0:T(4,0));T(0,0,78);)" => Create(Pcre2PartialProbeProgramKind.AnchoredNewlineSequence, 1, int.MaxValue, true),
            "O(S(R(1,2147483647,0:T(0,0,61));T(0,0,62);))" => Create(Pcre2PartialProbeProgramKind.AnchoredAtomicAPlusB),
            "S(C(1:S(T(0,0,61);T(0,0,62);T(0,0,63);));U(0,1,);)" => Create(Pcre2PartialProbeProgramKind.AnchoredRepeatedLiteral, 2, "abc"),
            "D(1:Q(0:S(T(0,0,61);T(0,0,62);T(0,0,63);)):R(0,2147483647,0:T(2,0)):T(0,0,5A))" => Create(Pcre2PartialProbeProgramKind.AnchoredConditionalAbcDotStarOrZ),
            "S(R(1,2147483647,2:C(1:S(T(0,0,61);T(0,0,62);T(0,0,63);)));T(0,0,78);)" => Create(Pcre2PartialProbeProgramKind.AnchoredLiteralPlusTerminal, 'x', "abc"),
            "S(T(5,0);R(1,2147483647,2:T(0,0,61));T(1,0,0:3,0,0,0,.);)" or
            "S(T(5,0);R(1,2147483647,2:C(1:T(0,0,61)));T(1,0,0:3,0,0,0,.);)" => Create(Pcre2PartialProbeProgramKind.AnchoredAPlusWord, false),
            "S(T(5,0);A(S(T(0,0,61);T(0,0,61););S(R(1,2147483647,2:T(0,0,61));T(1,0,0:3,0,0,0,.);););)" => Create(Pcre2PartialProbeProgramKind.AnchoredAaOrAPlusWord),
            "S(T(5,0);R(0,2147483647,2:T(0,0,61));T(1,0,0:3,0,0,0,.);)" or
            "S(T(5,0);R(1,2147483647,2:C(1:A(T(0,0,61);E;)));T(1,0,0:3,0,0,0,.);)" => Create(Pcre2PartialProbeProgramKind.AnchoredAPlusWord, true),
            "S(T(5,0);C(1:A(S(T(0,0,61);T(6,0););S(T(0,0,61);T(0,0,D););));)" when request.Settings.Newline == Pcre2NewlineConvention.Crlf => Create(Pcre2PartialProbeProgramKind.AnchoredACrlfEnd, true),
            "R(2,3,0:T(2,0))" or "R(2,3,1:T(2,0))" when request.Settings.Newline == Pcre2NewlineConvention.Crlf => Create(Pcre2PartialProbeProgramKind.CrlfDotQuantifier, 2, 3, false),
            _ => Pcre2PartialProbeProgram.None,
        };
    }

    private static bool TryCompileCharacterProgram(
        Pcre2CharacterToken[] tokens,
        Pcre2CompileRequest request,
        out Pcre2PartialProbeProgram program)
    {
        if (tokens.Length == 1 && tokens[0].Kind == Pcre2CharacterTokenKind.EndOfSubjectOrFinalNewline)
        {
            program = Create(Pcre2PartialProbeProgramKind.EndAssertion, true);
            return true;
        }

        if (tokens.Length == 2 &&
            tokens[0].Kind is Pcre2CharacterTokenKind.BeginningOfLine or Pcre2CharacterTokenKind.BeginningOfSubject &&
            tokens[1].Kind == Pcre2CharacterTokenKind.NewlineSequence)
        {
            program = Create(Pcre2PartialProbeProgramKind.AnchoredNewlineSequence, 1, 1, false);
            return true;
        }

        if (request.Settings.Newline == Pcre2NewlineConvention.Crlf &&
            tokens.Length == 1 &&
            tokens[0].Kind is Pcre2CharacterTokenKind.Any or Pcre2CharacterTokenKind.AnyNotNewline)
        {
            program = Create(Pcre2PartialProbeProgramKind.CrlfDotQuantifier, 1, 1, false);
            return true;
        }

        if (TryGetBoundedLiteral(tokens, 0, tokens.Length, out var literal) &&
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
        var trailing = Pcre2PartialProbeTrailingAssertion.None;
        if (last > first)
        {
            trailing = tokens[last - 1].Kind switch
            {
                Pcre2CharacterTokenKind.EndOfLine => Pcre2PartialProbeTrailingAssertion.Dollar,
                Pcre2CharacterTokenKind.EndOfSubject => Pcre2PartialProbeTrailingAssertion.EndAbsolute,
                Pcre2CharacterTokenKind.EndOfSubjectOrFinalNewline => Pcre2PartialProbeTrailingAssertion.EndBeforeFinalNewline,
                Pcre2CharacterTokenKind.WordBoundary => Pcre2PartialProbeTrailingAssertion.WordBoundary,
                Pcre2CharacterTokenKind.NonWordBoundary => Pcre2PartialProbeTrailingAssertion.NonWordBoundary,
                _ => Pcre2PartialProbeTrailingAssertion.None,
            };
            if (trailing != Pcre2PartialProbeTrailingAssertion.None)
            {
                last--;
            }
        }

        if (first < last &&
            tokens[first].Kind == Pcre2CharacterTokenKind.WordBoundary &&
            trailing == Pcre2PartialProbeTrailingAssertion.WordBoundary &&
            TryGetBoundedLiteral(tokens, first + 1, last, out literal))
        {
            program = Create(Pcre2PartialProbeProgramKind.WordBoundaryLiteral, literal);
            return true;
        }

        if (TryGetBoundedLiteral(tokens, first, last, out literal) && literal.Length > 0)
        {
            if (request.Settings.Newline == Pcre2NewlineConvention.Crlf &&
                anchored &&
                trailing == Pcre2PartialProbeTrailingAssertion.Dollar &&
                literal.AsSpan().SequenceEqual("a"u8))
            {
                program = Create(Pcre2PartialProbeProgramKind.AnchoredACrlfEnd, false);
                return true;
            }

            if (anchored && trailing != Pcre2PartialProbeTrailingAssertion.None)
            {
                program = Create(Pcre2PartialProbeProgramKind.AnchoredExactLiteral, literal);
                return true;
            }

            if (anchored)
            {
                program = Create(Pcre2PartialProbeProgramKind.AnchoredPrefixLiteral, literal);
                return true;
            }

            if (trailing != Pcre2PartialProbeTrailingAssertion.None)
            {
                program = Create(Pcre2PartialProbeProgramKind.TrailingLiteralAssertion, trailing, literal);
                return true;
            }
        }

        program = Pcre2PartialProbeProgram.None;
        return false;
    }

    private static bool TryGetBoundedLiteral(Pcre2CharacterToken[] tokens, int first, int last, out byte[] literal)
    {
        var builder = new ArrayBufferWriter<byte>();
        for (var index = first; index < last; index++)
        {
            var token = tokens[index];
            if (token.Kind != Pcre2CharacterTokenKind.Literal ||
                token.Options != Pcre2CharacterOptions.None)
            {
                literal = [];
                return false;
            }

            var destination = builder.GetSpan(token.Literal.Utf8SequenceLength);
            var written = token.Literal.EncodeToUtf8(destination);
            builder.Advance(written);
        }

        literal = builder.WrittenSpan.ToArray();
        return true;
    }

    private static Pcre2PartialProbeProgram Create(Pcre2PartialProbeProgramKind kind, params string[] operands)
        => new(kind, operands.Select(Encoding.UTF8.GetBytes).ToArray(), 0, 0, false, '\0', Pcre2PartialProbeTrailingAssertion.None);

    private static Pcre2PartialProbeProgram Create(Pcre2PartialProbeProgramKind kind)
        => new(kind, [], 0, 0, false, '\0', Pcre2PartialProbeTrailingAssertion.None);

    private static Pcre2PartialProbeProgram Create(Pcre2PartialProbeProgramKind kind, params byte[][] operands)
        => new(kind, operands, 0, 0, false, '\0', Pcre2PartialProbeTrailingAssertion.None);

    private static Pcre2PartialProbeProgram Create(Pcre2PartialProbeProgramKind kind, bool flag, params string[] operands)
        => new(kind, operands.Select(Encoding.UTF8.GetBytes).ToArray(), 0, 0, flag, '\0', Pcre2PartialProbeTrailingAssertion.None);

    private static Pcre2PartialProbeProgram Create(Pcre2PartialProbeProgramKind kind, int first, string operand)
        => new(kind, [Encoding.UTF8.GetBytes(operand)], first, 0, false, '\0', Pcre2PartialProbeTrailingAssertion.None);

    private static Pcre2PartialProbeProgram Create(Pcre2PartialProbeProgramKind kind, int first, int second, bool flag)
        => new(kind, [], first, second, flag, '\0', Pcre2PartialProbeTrailingAssertion.None);

    private static Pcre2PartialProbeProgram Create(Pcre2PartialProbeProgramKind kind, char terminal, string operand)
        => new(kind, [Encoding.UTF8.GetBytes(operand)], 0, 0, false, terminal, Pcre2PartialProbeTrailingAssertion.None);

    private static Pcre2PartialProbeProgram Create(Pcre2PartialProbeProgramKind kind, Pcre2PartialProbeTrailingAssertion assertion, byte[] operand)
        => new(kind, [operand], 0, 0, false, '\0', assertion);

    private static string Describe(IPcre2BacktrackingNode root)
    {
        var builder = new StringBuilder();
        AppendNode(builder, root);
        return builder.ToString();
    }

    private static void AppendNode(StringBuilder builder, IPcre2BacktrackingNode node)
    {
        switch (node)
        {
            case Pcre2EmptyBacktrackingNode:
                builder.Append('E');
                break;
            case Pcre2TokenBacktrackingNode token:
                AppendToken(builder, token.Token);
                break;
            case Pcre2SequenceBacktrackingNode sequence:
                AppendChildren(builder, 'S', sequence.Children);
                break;
            case Pcre2AlternationBacktrackingNode alternation:
                AppendChildren(builder, 'A', alternation.Alternatives);
                break;
            case Pcre2RepeatBacktrackingNode repeat:
                builder.Append("R(")
                    .Append(repeat.Minimum)
                    .Append(',')
                    .Append(repeat.Maximum)
                    .Append(',')
                    .Append((int)repeat.Preference)
                    .Append(':');
                AppendNode(builder, repeat.Body);
                builder.Append(')');
                break;
            case Pcre2CaptureBacktrackingNode capture:
                builder.Append("C(").Append(capture.Slot).Append(':');
                AppendNode(builder, capture.Body);
                builder.Append(')');
                break;
            case Pcre2BackreferenceBacktrackingNode backreference:
                AppendTarget(builder, 'B', backreference.Target);
                break;
            case Pcre2AssertionBacktrackingNode assertion:
                builder.Append("Q(").Append((int)assertion.AssertionKind).Append(':');
                AppendNode(builder, assertion.Body);
                builder.Append(')');
                break;
            case Pcre2SubroutineCallBacktrackingNode subroutine:
                AppendTarget(builder, 'U', subroutine.Target);
                break;
            case Pcre2ConditionalBacktrackingNode conditional:
                builder.Append("D(").Append((int)conditional.ConditionKind).Append(':');
                if (conditional.Assertion is not null)
                {
                    AppendNode(builder, conditional.Assertion);
                }

                builder.Append(':');
                AppendNode(builder, conditional.YesBranch);
                builder.Append(':');
                AppendNode(builder, conditional.NoBranch);
                builder.Append(')');
                break;
            case Pcre2AtomicBacktrackingNode atomic:
                builder.Append("O(");
                AppendNode(builder, atomic.Body);
                builder.Append(')');
                break;
            case Pcre2ControlVerbBacktrackingNode control:
                builder.Append("V(").Append((int)control.VerbKind).Append(')');
                break;
            case Pcre2MatchBoundaryResetBacktrackingNode:
                builder.Append('K');
                break;
            default:
                builder.Append('?');
                break;
        }
    }

    private static void AppendChildren(StringBuilder builder, char prefix, IPcre2BacktrackingNode[] children)
    {
        builder.Append(prefix).Append('(');
        foreach (var child in children)
        {
            AppendNode(builder, child);
            builder.Append(';');
        }

        builder.Append(')');
    }

    private static void AppendToken(StringBuilder builder, Pcre2CharacterToken token)
    {
        builder.Append("T(")
            .Append((int)token.Kind)
            .Append(',')
            .Append((int)token.Options);
        if (token.Kind == Pcre2CharacterTokenKind.Literal)
        {
            builder.Append(',').Append(token.Literal.Value.ToString("X", CultureInfo.InvariantCulture));
        }
        else if (token.Kind == Pcre2CharacterTokenKind.CharacterClass)
        {
            builder.Append(',').Append(token.CharacterClass.Negated ? '1' : '0').Append(':');
            foreach (var term in token.CharacterClass.Terms)
            {
                builder.Append((int)term.Kind)
                    .Append(',')
                    .Append(term.Range.Low)
                    .Append(',')
                    .Append(term.Range.High)
                    .Append(',')
                    .Append(term.Negated ? '1' : '0')
                    .Append(',')
                    .Append(term.Name)
                    .Append('.');
            }
        }

        builder.Append(')');
    }

    private static void AppendTarget(StringBuilder builder, char prefix, Pcre2BackreferenceTarget target)
    {
        builder.Append(prefix)
            .Append('(')
            .Append((int)target.Kind)
            .Append(',')
            .Append(target.Number)
            .Append(',')
            .Append(target.Name)
            .Append(')');
    }
}
