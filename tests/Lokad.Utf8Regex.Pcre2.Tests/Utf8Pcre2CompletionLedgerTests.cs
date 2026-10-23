using Lokad.Utf8Regex.Pcre2.Tests.Corpus;

namespace Lokad.Utf8Regex.Pcre2.Tests;

public sealed class Utf8Pcre2CompletionLedgerTests
{
    [Fact]
    public void EveryActiveCaseHasExecutionOrExplicitFailureCoverage()
    {
        var activeCases = Pcre2CorpusLoader.LoadByStatus(Pcre2CorpusCaseStatus.Active);
        Assert.NotEmpty(activeCases);
        Assert.All(activeCases, static corpusCase =>
            Assert.True(IsExplicitlyLedgered(corpusCase), $"No execution/failure coverage for {corpusCase.Id}."));
    }

    private static bool IsExplicitlyLedgered(Pcre2CorpusCase corpusCase) =>
        Pcre2CorpusExecutionFilter.CanExecuteCompile(corpusCase) ||
        Pcre2CorpusExecutionFilter.CanExecuteManagedMatchSubset(corpusCase) ||
        Pcre2CorpusExecutionFilter.CanExecuteSpecialMatchSubset(corpusCase) ||
        Pcre2CorpusExecutionFilter.CanExecuteProbeSubset(corpusCase) ||
        Pcre2CorpusExecutionFilter.CanExecuteReplaceSubset(corpusCase) ||
        Pcre2CorpusExecutionFilter.CanExecuteCountSubset(corpusCase) ||
        Pcre2CorpusExecutionFilter.CanExecuteEnumerateSubset(corpusCase) ||
        corpusCase.Operation == Pcre2CorpusOperationKind.Compile ||
        corpusCase.Expected.ErrorKind is "NotSupportedException" or "DisallowedLookaroundBackslashK";
}
