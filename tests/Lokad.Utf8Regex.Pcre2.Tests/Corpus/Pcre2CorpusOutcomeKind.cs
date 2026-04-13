namespace Lokad.Utf8Regex.Pcre2.Tests.Corpus;

public enum Pcre2CorpusOutcomeKind
{
    FullMatch = 0,
    NoMatch = 1,
    PartialMatch = 2,
    CompileError = 3,
    MatchError = 4,
    ReplacementError = 5,
}
