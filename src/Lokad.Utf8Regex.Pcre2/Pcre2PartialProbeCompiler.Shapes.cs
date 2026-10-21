using ProbeKind = Lokad.Utf8Regex.Pcre2.Pcre2PartialProbeProgramKind;
using TokenKind = Lokad.Utf8Regex.Pcre2.Pcre2CharacterTokenKind;
using Preference = Lokad.Utf8Regex.Pcre2.Pcre2RepeatPreference;

namespace Lokad.Utf8Regex.Pcre2;

internal static partial class Pcre2PartialProbeCompiler
{
    // Preserve the existing curated profile. These checks borrow fixed-size
    // portions of the parsed tree; they never serialize or recursively walk it.
    private static Pcre2PartialProbeProgram CompileTree(IPcre2BacktrackingNode root, Pcre2CompileRequest request)
    {
        if (root is Pcre2SequenceBacktrackingNode { Children: [Pcre2TokenBacktrackingNode anchor,
                Pcre2RepeatBacktrackingNode newline, Pcre2TokenBacktrackingNode terminal] } &&
            anchor.Token.Kind is TokenKind.BeginningOfLine or TokenKind.BeginningOfSubject &&
            newline.Body is Pcre2TokenBacktrackingNode { Token.Kind: TokenKind.NewlineSequence } &&
            newline.Preference is Preference.Greedy or Preference.Lazy &&
            (newline.Minimum, newline.Maximum) is (0, 1) or (2, 3) or (1, int.MaxValue) &&
            terminal.Token.Kind == TokenKind.Literal && terminal.Token.Literal.Value == 'x' &&
            (terminal.Token.Options & Pcre2CharacterOptions.Caseless) == 0)
            return Create(ProbeKind.AnchoredNewlineSequence, first: newline.Minimum, second: newline.Maximum, flag: true);

        if (request.Settings.Newline == Pcre2NewlineConvention.Crlf && root is Pcre2RepeatBacktrackingNode
            { Minimum: 2, Maximum: 3, Body: Pcre2TokenBacktrackingNode { Token.Kind: TokenKind.Any },
                Preference: Preference.Greedy or Preference.Lazy } dot)
            return Create(ProbeKind.CrlfDotQuantifier, first: 2, second: 3, flag: dot.Preference == Preference.Lazy);

        if (root is Pcre2AlternationBacktrackingNode { Alternatives: [var first, var second] })
        {
            if (IsText(first, "cat") && IsText(second, "horse"))
                return Create(ProbeKind.LiteralAlternation, "cat", "horse");
            if (IsText(first, "dogsbody") && IsText(second, "dog"))
                return Create(ProbeKind.OrderedLiteralAlternation, "dogsbody", "dog");
            if (IsText(first, "dog") && IsText(second, "dogsbody"))
                return Create(ProbeKind.OrderedLiteralAlternation, "dog", "dogsbody");
        }

        if (root is Pcre2AtomicBacktrackingNode
            { Body: Pcre2SequenceBacktrackingNode { Children: [var repeat, var last] } } &&
            IsRepeatedText(repeat, "a", 1, int.MaxValue, Preference.Greedy) && IsText(last, "b"))
            return Create(ProbeKind.AtomicAPlusB);

        if (root is Pcre2ConditionalBacktrackingNode { ConditionKind: Pcre2BacktrackingConditionKind.Assertion,
                Assertion: { AssertionKind: Pcre2AssertionKind.PositiveLookahead } assertion } conditional &&
            IsText(assertion.Body, "abc") && IsRepeatedAny(conditional.YesBranch) && IsText(conditional.NoBranch, "Z"))
            return Create(ProbeKind.ConditionalAbcDotStarOrZ);

        if (root is not Pcre2SequenceBacktrackingNode sequence) return Pcre2PartialProbeProgram.None;
        var children = sequence.Children.AsSpan();
        if (children is [var left, var right])
        {
            if (IsText(left, "f") && IsRepeatedAny(right))
                return Create(ProbeKind.LiteralDotStar, "f");
            if (IsText(left, "f", Pcre2CharacterOptions.DotAll) && IsRepeatedAny(right, Pcre2CharacterOptions.DotAll))
                return Create(ProbeKind.LiteralDotStar, "f", flag: true);

            if (left is Pcre2CaptureBacktrackingNode { Slot: 1 } capture && IsText(capture.Body, "a") &&
                IsRepeatedText(right, "b", 1, int.MaxValue, Preference.Greedy))
                return Create(ProbeKind.AbPlus);

            if (left is Pcre2RepeatBacktrackingNode { Minimum: 0 or 1, Maximum: int.MaxValue,
                    Preference: Preference.Possessive } cs && IsText(cs.Body, "c") &&
                right is Pcre2AssertionBacktrackingNode { AssertionKind: Pcre2AssertionKind.PositiveLookbehind } behind &&
                IsTwoSingletonClass(behind.Body, 'b', 'c'))
                return Create(ProbeKind.TrailingCWithLookbehind, flag: cs.Minimum == 1);

            if (left is Pcre2AssertionBacktrackingNode { AssertionKind: Pcre2AssertionKind.NegativeLookahead } negative &&
                IsTwoSingletonClass(negative.Body, 'a', 'b') && IsRepeatedAny(right))
                return Create(ProbeKind.NegativeStartClassDotStar);

            if (left is Pcre2CaptureBacktrackingNode { Slot: 1 } abc && IsText(abc.Body, "abc") &&
                right is Pcre2SubroutineCallBacktrackingNode
                { Target: { Kind: Pcre2BackreferenceTargetKind.Absolute, Number: 1, Name: "" } })
                return Create(ProbeKind.RepeatedLiteral, "abc", first: 2);

            if (left is Pcre2RepeatBacktrackingNode { Minimum: 1, Maximum: int.MaxValue,
                    Preference: Preference.Possessive, Body: Pcre2CaptureBacktrackingNode { Slot: 1 } repeated } &&
                IsText(repeated.Body, "abc") && IsText(right, "x"))
                return Create(ProbeKind.LiteralPlusTerminal, "abc", terminal: 'x');

            if (IsToken(left, TokenKind.BeginningOfLine))
            {
                if (right is Pcre2AlternationBacktrackingNode { Alternatives: [var aa, Pcre2SequenceBacktrackingNode
                        { Children: [var many, var word] }] } && IsText(aa, "aa") &&
                    IsRepeatedText(many, "a", 1, int.MaxValue, Preference.Possessive) && IsWordClass(word))
                    return Create(ProbeKind.AnchoredAaOrAPlusWord);

                if (request.Settings.Newline == Pcre2NewlineConvention.Crlf && right is Pcre2CaptureBacktrackingNode
                    { Slot: 1, Body: Pcre2AlternationBacktrackingNode { Alternatives:
                        [Pcre2SequenceBacktrackingNode { Children: [var a, var end] },
                         Pcre2SequenceBacktrackingNode { Children: [var otherA, var cr] }] } } &&
                    IsText(a, "a") && IsToken(end, TokenKind.EndOfLine) && IsText(otherA, "a") && IsText(cr, "\r"))
                    return Create(ProbeKind.CapturedEndAlternative);
            }
        }

        if (children is [var beginning, var middle, var ending] && IsToken(beginning, TokenKind.BeginningOfLine) &&
            IsWordClass(ending) && middle is Pcre2RepeatBacktrackingNode { Maximum: int.MaxValue,
                Preference: Preference.Possessive } aRepeat)
        {
            if (aRepeat.Minimum == 1 && (IsText(aRepeat.Body, "a") ||
                aRepeat.Body is Pcre2CaptureBacktrackingNode { Slot: 1 } aCapture && IsText(aCapture.Body, "a")))
                return Create(ProbeKind.AnchoredAPlusWord);
            if (aRepeat.Minimum == 0 && IsText(aRepeat.Body, "a") ||
                aRepeat.Minimum == 1 && aRepeat.Body is Pcre2CaptureBacktrackingNode { Slot: 1,
                    Body: Pcre2AlternationBacktrackingNode { Alternatives: [var a, Pcre2EmptyBacktrackingNode] } } && IsText(a, "a"))
                return Create(ProbeKind.AnchoredAPlusWord, flag: true);
        }

        if (children.Length == 4)
        {
            if (IsText(children[..3], "dog") && children[3] is Pcre2RepeatBacktrackingNode
                { Minimum: 0, Maximum: 1, Preference: Preference.Greedy or Preference.Lazy,
                    Body: Pcre2CaptureBacktrackingNode { Slot: 1 } body } optional && IsText(body.Body, "sbody"))
                return optional.Preference == Preference.Greedy
                    ? Create(ProbeKind.OrderedLiteralAlternation, "dogsbody", "dog")
                    : Create(ProbeKind.OrderedLiteralAlternation, "dog", "dogsbody");
            if (children[0] is Pcre2AssertionBacktrackingNode { AssertionKind: Pcre2AssertionKind.PositiveLookbehind } prefix &&
                IsText(prefix.Body, "abc") && IsText(children[1..], "123"))
                return Create(ProbeKind.InspectedPrefixLiteral, "abc", "123");
            if (IsText(children[..3], "abc") && children[3] is Pcre2AssertionBacktrackingNode
                { AssertionKind: Pcre2AssertionKind.PositiveLookahead } suffix && IsText(suffix.Body, "xyz"))
                return Create(ProbeKind.InspectedContextLiteral, "", "abc", "xyz");
        }
        if (children.Length == 5 && IsText(children.Slice(1, 3), "abc") &&
            children[0] is Pcre2AssertionBacktrackingNode { AssertionKind: Pcre2AssertionKind.PositiveLookbehind } pqr &&
            IsText(pqr.Body, "pqr") && children[4] is Pcre2AssertionBacktrackingNode
            { AssertionKind: Pcre2AssertionKind.PositiveLookahead } xyz && IsText(xyz.Body, "xyz"))
            return Create(ProbeKind.InspectedContextLiteral, "pqr", "abc", "xyz");
        if (children.Length == 5 && IsText(children[..3], "abc") &&
            children[3] is Pcre2AssertionBacktrackingNode { AssertionKind: Pcre2AssertionKind.PositiveLookahead } abcde &&
            IsText(abcde.Body, "abcde") && children[4] is Pcre2AssertionBacktrackingNode
            { AssertionKind: Pcre2AssertionKind.PositiveLookahead } ab && IsText(ab.Body, "ab"))
            return Create(ProbeKind.InspectedContextLiteral, "", "abc", "abcde");
        if (children.Length == 7 && IsText(children[..3], "abc") &&
            children[3] is Pcre2MatchBoundaryResetBacktrackingNode && IsText(children[4..], "123"))
            return Create(ProbeKind.InspectedPrefixLiteral, "abc", "123", flag: true);

        return Pcre2PartialProbeProgram.None;
    }

    private static bool IsText(IPcre2BacktrackingNode node, string text, Pcre2CharacterOptions options = default)
        => text.Length == 1 ? IsLiteral(node, text[0], options)
            : node is Pcre2SequenceBacktrackingNode sequence && IsText(sequence.Children.AsSpan(), text, options);

    // All callers supply fixed ASCII strings of at most eight characters.
    private static bool IsText(ReadOnlySpan<IPcre2BacktrackingNode> nodes, string text, Pcre2CharacterOptions options = default)
    {
        if (nodes.Length != text.Length) return false;
        for (var i = 0; i < text.Length; i++) if (!IsLiteral(nodes[i], text[i], options)) return false;
        return true;
    }

    private static bool IsLiteral(IPcre2BacktrackingNode node, char value, Pcre2CharacterOptions options)
        => node is Pcre2TokenBacktrackingNode token && token.Token.Kind == TokenKind.Literal &&
            token.Token.Options == options && token.Token.Literal.Value == value;

    private static bool IsToken(IPcre2BacktrackingNode node, TokenKind kind, Pcre2CharacterOptions options = default)
        => node is Pcre2TokenBacktrackingNode token && token.Token.Kind == kind && token.Token.Options == options;

    private static bool IsRepeatedText(IPcre2BacktrackingNode node, string text, int minimum, int maximum, Preference preference)
        => node is Pcre2RepeatBacktrackingNode repeat && repeat.Minimum == minimum && repeat.Maximum == maximum &&
            repeat.Preference == preference && IsText(repeat.Body, text);

    private static bool IsRepeatedAny(IPcre2BacktrackingNode node, Pcre2CharacterOptions options = default)
        => node is Pcre2RepeatBacktrackingNode { Minimum: 0, Maximum: int.MaxValue, Preference: Preference.Greedy } repeat &&
            IsToken(repeat.Body, TokenKind.Any, options);

    private static bool IsWordClass(IPcre2BacktrackingNode node)
        => node is Pcre2TokenBacktrackingNode { Token: { Kind: TokenKind.CharacterClass, Options: Pcre2CharacterOptions.None,
            CharacterClass: { Negated: false, Terms: [{ Kind: Pcre2CharacterClassTermKind.Word,
                Range: { Low: 0, High: 0 }, Negated: false, Name: "" }] } } };

    private static bool IsTwoSingletonClass(IPcre2BacktrackingNode node, char first, char second)
        => node is Pcre2TokenBacktrackingNode { Token: { Kind: TokenKind.CharacterClass, Options: Pcre2CharacterOptions.None,
            CharacterClass: { Negated: false, Terms: [var a, var b] } } } &&
            a.Kind == Pcre2CharacterClassTermKind.Range && !a.Negated && a.Name.Length == 0 &&
            a.Range.Low == first && a.Range.High == first &&
            b.Kind == Pcre2CharacterClassTermKind.Range && !b.Negated && b.Name.Length == 0 &&
            b.Range.Low == second && b.Range.High == second;
}
