namespace Lokad.Utf8Regex.Pcre2.Tests.Corpus;

public static class Pcre2CorpusCaseRouting
{
    public static Pcre2CorpusExecutionKind GetExecutionKind(Pcre2CorpusCase corpusCase)
    {
        ArgumentNullException.ThrowIfNull(corpusCase);

        return corpusCase.Operation switch
        {
            Pcre2CorpusOperationKind.Compile => Pcre2CorpusExecutionKind.Compile,
            Pcre2CorpusOperationKind.Replace => Pcre2CorpusExecutionKind.Replace,
            Pcre2CorpusOperationKind.Match => Pcre2CorpusExecutionKind.Match,
            Pcre2CorpusOperationKind.MatchDetailed => Pcre2CorpusExecutionKind.Match,
            Pcre2CorpusOperationKind.Probe => Pcre2CorpusExecutionKind.Match,
            Pcre2CorpusOperationKind.Count => Pcre2CorpusExecutionKind.Match,
            Pcre2CorpusOperationKind.EnumerateMatches => Pcre2CorpusExecutionKind.Match,
            _ => throw new ArgumentOutOfRangeException(nameof(corpusCase), corpusCase.Operation, "Unsupported corpus operation kind."),
        };
    }
}
