namespace Lokad.Utf8Regex.Pcre2.Tests.Corpus;

public static class Pcre2CorpusExecutionFilter
{
    public static bool CanExecuteCompile(Pcre2CorpusCase corpusCase)
    {
        ArgumentNullException.ThrowIfNull(corpusCase);
        return corpusCase.Operation == Pcre2CorpusOperationKind.Compile;
    }

    public static bool CanExecuteManagedMatchSubset(Pcre2CorpusCase corpusCase)
    {
        ArgumentNullException.ThrowIfNull(corpusCase);

        if (corpusCase.Operation is not (Pcre2CorpusOperationKind.Match or Pcre2CorpusOperationKind.MatchDetailed))
        {
            return false;
        }

        return !corpusCase.Pattern.Contains("(?|", StringComparison.Ordinal) &&
               !corpusCase.Pattern.Contains(@"(?<n>", StringComparison.Ordinal) &&
               !corpusCase.Pattern.Contains("(*", StringComparison.Ordinal) &&
               !corpusCase.Pattern.Contains(@"\K", StringComparison.Ordinal) &&
               !corpusCase.Pattern.Contains(@"\C", StringComparison.Ordinal) &&
               !corpusCase.Pattern.Contains("(?1)", StringComparison.Ordinal) &&
               !corpusCase.Pattern.Contains("(?2)", StringComparison.Ordinal) &&
               !corpusCase.Pattern.Contains("(?&", StringComparison.Ordinal) &&
               !corpusCase.Pattern.Contains(@"\g{", StringComparison.Ordinal) &&
               !corpusCase.Pattern.Contains(@"\g<", StringComparison.Ordinal) &&
               !corpusCase.Pattern.Contains(@"\g'", StringComparison.Ordinal) &&
               !corpusCase.Pattern.Contains("(?(DEFINE)", StringComparison.Ordinal) &&
               !string.Equals(corpusCase.Pattern, "^(ba|b*){1,2}?bc", StringComparison.Ordinal) &&
               !string.Equals(corpusCase.Pattern, "(?>ab|abab){1,5}?M", StringComparison.Ordinal) &&
               !string.Equals(corpusCase.Pattern, "(?>ab|abab){2}?M", StringComparison.Ordinal) &&
               !string.Equals(corpusCase.Pattern, "((?(?=(a))a)+k)", StringComparison.Ordinal) &&
               !string.Equals(corpusCase.Pattern, "((?(?=(a))a|)+k)", StringComparison.Ordinal) &&
               !string.Equals(corpusCase.Pattern, "^(?(?=(a))abc|def)", StringComparison.Ordinal) &&
               !string.Equals(corpusCase.Pattern, "^(?(?!(a))def|abc)", StringComparison.Ordinal);
    }

    public static bool CanExecuteSpecialMatchSubset(Pcre2CorpusCase corpusCase)
    {
        ArgumentNullException.ThrowIfNull(corpusCase);

        if (corpusCase.Operation is not (Pcre2CorpusOperationKind.Match or Pcre2CorpusOperationKind.MatchDetailed))
        {
            return false;
        }

        if (string.Equals(corpusCase.PatternRef, "pcre2.testinput1.mailbox-rfc2822", StringComparison.Ordinal))
        {
            return true;
        }

        return corpusCase.Pattern is "(?|(abc)|(xyz))"
            or "(?|(abc)|(xyz))\\1"
            or "(x)(?|(abc)|(xyz))(x)"
            or "(x)(?|(abc)(pqr)|(xyz))(x)"
            or "(?|(aaa)|(b))"
            or "(?|(abc)|(xyz))(?1)"
            or "(?|(aaa)|(b))\\g{1}"
            or "(?|(?'a'aaa)|(?'a'b))\\k'a'"
            or "(?|(?'a'aaa)|(?'a'b))(?'a'cccc)\\k'a'"
            or "^(ba|b*){1,2}?bc"
            or @"(?:(?<n>foo)|(?<n>bar))\k<n>"
            or "(*MARK:A)(*SKIP:B)(C|X)"
            or @"(foo)\Kbar"
            or @"(foo)(\Kbar|baz)"
            or @"(foo\Kbar)baz"
            or @"abc\K123"
            or @"123\Kabc"
            or @"^abc\K"
            or @"^(?:(?=abc)|abc\K)"
            or @"(?>a\Kb)"
            or @"((?>a\Kb))"
            or @"(a\Kb)"
            or @"^a\Kcz|ac"
            or @"(?>a\Kbz|ab)"
            or @"^(?&t)(?(DEFINE)(?<t>a\Kb))$"
            or @"(?:a\Kb)*+"
            or @"(?>a\Kb)*"
            or @"(?:a\Kb)*"
            or @"(a\Kb)*+"
            or @"(a\Kb)*"
            or @"a\K.(?0)*"
            or @"(a\K.(?1)*)"
            or @"(?=ab\K)"
            or @"(?<=\Ka)"
            or @"(?(?=\Gc)(?<=\Kb)c|(?<=\Kab))"
            or @"(?(?=\Gc)(?<=\Kab)|(?<=\Kb))"
            or @"(?=.{10}(?1))x(\K){0}"
            or @"(?=.{10}(.))(*scs:(1)(?2))x(\K){0}"
            or @"(?=.{5}(?1))\d*(\K){0}"
            or @"(?(DEFINE)(?<sneaky>b\K))a(?=(?&sneaky))"
            or @"a|(?(DEFINE)(?<sneaky>\Ka))(?<=(?&sneaky))b"
            or @"a|(?(DEFINE)(?<sneaky>\K\Ga))(?<=(?&sneaky))b"
            or @"ab\Cde"
            or "^(.|(.)(?1)\\2)$"
            or "^((.)(?1)\\2|.?)$"
            or "^(.)(\\1|a(?2))"
            or "^(.|(.)(?1)?\\2)$"
            or "(?>ab|abab){1,5}?M"
            or "(?>ab|abab){2}?M"
            or "((?(?=(a))a)+k)"
            or "((?(?=(a))a|)+k)"
            or "^(?(?=(a))abc|def)"
            or "^(?(?!(a))def|abc)"
            or "^(?(?=(a)(*ACCEPT))abc|def)"
            or "^(?(?!(a)(*ACCEPT))def|abc)"
            or "^(?1)\\d{3}(a)"
            or "(?1)(A(*COMMIT)|B)D"
            or "(?<DEFINE>b)(?(DEFINE)(a+))(?&DEFINE)";
    }

    public static bool CanExecuteProbeSubset(Pcre2CorpusCase corpusCase)
    {
        ArgumentNullException.ThrowIfNull(corpusCase);
        return corpusCase.Status == Pcre2CorpusCaseStatus.Active &&
               corpusCase.Operation == Pcre2CorpusOperationKind.Probe &&
               corpusCase.Pattern is "f.*" or "foo" or "^foo" or "^abc$" or "abc$" or "(a)b+" or "cat|horse" or "(?:cat|horse)" or "dog(sbody)?" or "dog(sbody)??" or "dog|dogsbody" or "dogsbody|dog" or "\\bthe cat\\b" or "\\babc\\b" or "abc\\K123" or "(?<=abc)123" or "abc(?=xyz)" or "(?<=pqr)abc(?=xyz)" or "abc(?=abcde)(?=ab)" or "\\z" or "\\Z" or "c*+(?<=[bc])" or "c++(?<=[bc])" or "(?![ab]).*" or "(?>a+b)" or "(abc)(?1)" or "(?(?=abc).*|Z)" or "(abc)++x" or "^(?:a)++\\w" or "^(?:aa|(?:a)++\\w)" or "^(?:a)*+\\w" or "^(a)++\\w" or "^(a|)++\\w" or "^\\R" or "^\\R{2,3}x" or "^\\R{2,3}?x" or "^\\R?x" or "^\\R+x" or "^a$" or "^(a$|a\\r)" or "." or ".{2,3}" or ".{2,3}?" or "abc\\z" or "abc\\Z" or "abc\\b" or "abc\\B";
    }

    public static bool CanExecuteReplaceSubset(Pcre2CorpusCase corpusCase)
    {
        ArgumentNullException.ThrowIfNull(corpusCase);
        if (corpusCase.Operation != Pcre2CorpusOperationKind.Replace ||
            !string.Equals(corpusCase.PartialMode, "None", StringComparison.Ordinal))
        {
            return false;
        }

        return corpusCase.Pattern is "foo"
            or "a"
            or "abc"
            or "abcd"
            or "foo(?<Bar>BAR)?"
            or "a(b)c(d)e"
            or "a(..)d"
            or "55|a(..)d"
            or "a(b)(c)"
            or "a(bc)(DE)"
            or "a(?<ONE>b)c(?<TWO>d)e"
            or "a(?<namED_1>b)c"
            or "a(?<namedverylongbutperfectlylegalsoyoushouldnthaveaproblem_1>b)c"
            or "."
            or "(.)(.)"
            or "a(b)c"
            or "a(?:(b)|(c))"
            or "A|(B)"
            or "(Hello)|wORLD"
            or "a(b)c|xyz"
            or "a|(b)c"
            or "a|(?'X'b)c"
            or "X(b)Y"
            or "(a)"
            or "(abcd)"
            or "(aa)(BB)"
            or "ABC"
            or "a(?<named>b)c"
            or "(?:(?<n>foo)|(?<n>bar))\\k<n>"
            or "(?|(abc)|(xyz))"
            or "(?|(abc)|(xyz))(?1)"
            or "(?|(aaa)|(b))\\g{1}"
            or "(?|(?'a'aaa)|(?'a'b))\\k'a'"
            or "(x)(?|(abc)(pqr)|(xyz))(x)"
            or "(?|(?'a'aaa)|(?'a'b))(?'a'cccc)\\k'a'"
            or "(*MARK:A)(*SKIP:B)(C|X)"
            or "^(?1)\\d{3}(a)"
            or "(?1)(A(*COMMIT)|B)D"
            or "(?<DEFINE>b)(?(DEFINE)(a+))(?&DEFINE)"
            or "(foo)\\Kbar"
            or "(foo)(\\Kbar|baz)"
            or "(foo\\Kbar)baz"
            or "abc\\K123"
            or "123\\Kabc"
            or "^abc\\K"
            or "^(?:(?=abc)|abc\\K)"
            or "(a\\Kb)"
            or "((?>a\\Kb))"
            or "^a\\Kcz|ac"
            or "(?>a\\Kbz|ab)"
            or "^(?&t)(?(DEFINE)(?<t>a\\Kb))$"
            or "(?:a\\Kb)*+"
            or "(?>a\\Kb)*"
            or "(?:a\\Kb)*"
            or "a\\K.(?0)*"
            or "(a\\K.(?1)*)"
            or "(?<=abc)(|def)"
            or "(?<=abc)(|DEF)"
            or "(*:pear)apple|(*:orange)lemon|(*:strawberry)blackberry"
            or "(*:pear)apple";
    }

    public static bool CanExecuteCountSubset(Pcre2CorpusCase corpusCase)
    {
        ArgumentNullException.ThrowIfNull(corpusCase);
        if (corpusCase.Operation != Pcre2CorpusOperationKind.Count)
        {
            return false;
        }

        return corpusCase.Pattern is "abc\\K|def\\K"
            or "ab\\Kc|de\\Kf"
            or "(?=C)"
            or "(?<=abc)(|def)"
            or "(?<=\\G.)"
            or "(?|(aaa)|(b))"
            or "(?|(abc)|(xyz))\\1"
            or "(x)(?|(abc)|(xyz))(x)"
            or "(x)(?|(abc)(pqr)|(xyz))(x)"
            or "(?|(abc)|(xyz))(?1)"
            or "(?|(aaa)|(b))\\g{1}"
            or "(?:(?<n>foo)|(?<n>bar))\\k<n>"
            or "(?|(?'a'aaa)|(?'a'b))\\k'a'"
            or "(?|(?'a'aaa)|(?'a'b))(?'a'cccc)\\k'a'"
            or "(?<=\\Ka)"
            or "(?(?=\\Gc)(?<=\\Kb)c|(?<=\\Kab))"
            or "(?(?=\\Gc)(?<=\\Kab)|(?<=\\Kb))"
            or "^(?&t)(?(DEFINE)(?<t>a\\Kb))$"
            or "(?:a\\Kb)*+"
            or "(?>a\\Kb)*"
            or "(?:a\\Kb)*"
            or "a\\K.(?0)*"
            or "(a\\K.(?1)*)"
            or "((?(?=(a))a)+k)"
            or "((?(?=(a))a|)+k)"
            or "^(?(?=(a))abc|def)"
            or "^(?(?!(a))def|abc)"
            or "^(?(?=(a)(*ACCEPT))abc|def)"
            or "^(?(?!(a)(*ACCEPT))def|abc)"
            or "ab\\Cde"
            or "^(?1)\\d{3}(a)"
            or "(?1)(A(*COMMIT)|B)D"
            or "(?<DEFINE>b)(?(DEFINE)(a+))(?&DEFINE)"
            or "(?(DEFINE)(?<sneaky>b\\K))a(?=(?&sneaky))"
            or "a|(?(DEFINE)(?<sneaky>\\Ka))(?<=(?&sneaky))b"
            or "a|(?(DEFINE)(?<sneaky>\\K\\Ga))(?<=(?&sneaky))b";
    }

    public static bool CanExecuteEnumerateSubset(Pcre2CorpusCase corpusCase)
    {
        ArgumentNullException.ThrowIfNull(corpusCase);
        if (corpusCase.Operation != Pcre2CorpusOperationKind.EnumerateMatches)
        {
            return false;
        }

        return corpusCase.Pattern is "abc\\K|def\\K"
            or "ab\\Kc|de\\Kf"
            or "(?=C)"
            or "(?<=abc)(|def)"
            or "(?<=\\G.)"
            or "(?|(aaa)|(b))"
            or "(?|(abc)|(xyz))\\1"
            or "(x)(?|(abc)|(xyz))(x)"
            or "(x)(?|(abc)(pqr)|(xyz))(x)"
            or "(?|(abc)|(xyz))(?1)"
            or "(?|(aaa)|(b))\\g{1}"
            or "(?:(?<n>foo)|(?<n>bar))\\k<n>"
            or "(?|(?'a'aaa)|(?'a'b))\\k'a'"
            or "(?|(?'a'aaa)|(?'a'b))(?'a'cccc)\\k'a'"
            or "(?<=\\Ka)"
            or "(?(?=\\Gc)(?<=\\Kb)c|(?<=\\Kab))"
            or "(?(?=\\Gc)(?<=\\Kab)|(?<=\\Kb))"
            or "(?=ab\\K)"
            or "^(?&t)(?(DEFINE)(?<t>a\\Kb))$"
            or "(?:a\\Kb)*+"
            or "(?>a\\Kb)*"
            or "(?:a\\Kb)*"
            or "a\\K.(?0)*"
            or "(a\\K.(?1)*)"
            or "((?(?=(a))a)+k)"
            or "((?(?=(a))a|)+k)"
            or "^(?(?=(a))abc|def)"
            or "^(?(?!(a))def|abc)"
            or "^(?(?=(a)(*ACCEPT))abc|def)"
            or "^(?(?!(a)(*ACCEPT))def|abc)"
            or "ab\\Cde"
            or "^(?1)\\d{3}(a)"
            or "(?1)(A(*COMMIT)|B)D"
            or "(?<DEFINE>b)(?(DEFINE)(a+))(?&DEFINE)"
            or "(?(DEFINE)(?<sneaky>b\\K))a(?=(?&sneaky))"
            or "a|(?(DEFINE)(?<sneaky>\\K\\Ga))(?<=(?&sneaky))b";
    }
}
