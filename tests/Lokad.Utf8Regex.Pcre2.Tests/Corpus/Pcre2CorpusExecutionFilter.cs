namespace Lokad.Utf8Regex.Pcre2.Tests.Corpus;

// The corpus status selects supported cases; regex spelling is not an execution
// gate. Compile also checks deliberately excluded syntax's expected rejection.
public static class Pcre2CorpusExecutionFilter
{
    public static bool CanExecuteCompile(Pcre2CorpusCase corpusCase)
        => corpusCase.Operation == Pcre2CorpusOperationKind.Compile;

    public static bool CanExecuteMatch(Pcre2CorpusCase corpusCase)
        => corpusCase.Status == Pcre2CorpusCaseStatus.Active &&
           corpusCase.Operation is Pcre2CorpusOperationKind.Match or Pcre2CorpusOperationKind.MatchDetailed;

    public static bool CanExecuteProbeSubset(Pcre2CorpusCase corpusCase)
        => corpusCase.Status == Pcre2CorpusCaseStatus.Active && corpusCase.Operation == Pcre2CorpusOperationKind.Probe;

    public static bool CanExecuteReplaceSubset(Pcre2CorpusCase corpusCase)
        => corpusCase.Status == Pcre2CorpusCaseStatus.Active && corpusCase.Operation == Pcre2CorpusOperationKind.Replace;

    public static bool CanExecuteCountSubset(Pcre2CorpusCase corpusCase)
        => corpusCase.Status == Pcre2CorpusCaseStatus.Active && corpusCase.Operation == Pcre2CorpusOperationKind.Count;

    public static bool CanExecuteEnumerateSubset(Pcre2CorpusCase corpusCase)
        => corpusCase.Operation == Pcre2CorpusOperationKind.EnumerateMatches &&
           (corpusCase.Status == Pcre2CorpusCaseStatus.Active ||
            corpusCase.Expected.ErrorKind == nameof(NotSupportedException));
}
