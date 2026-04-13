using System.Text;

using Lokad.Utf8Regex.Pcre2.Tests.Corpus;

namespace Lokad.Utf8Regex.Pcre2.Tests;

public sealed class Utf8Pcre2CompletionLedgerTests
{
    [Fact]
    public void ActiveCorpusLedgerSummaryMatchesExpectation()
    {
        var activeCases = Pcre2CorpusLoader.LoadByStatus(Pcre2CorpusCaseStatus.Active);

        var compileCovered = activeCases.Count(Pcre2CorpusExecutionFilter.CanExecuteCompile);
        var managedMatchCovered = activeCases.Count(Pcre2CorpusExecutionFilter.CanExecuteManagedMatchSubset);
        var specialMatchCovered = activeCases.Count(Pcre2CorpusExecutionFilter.CanExecuteSpecialMatchSubset);
        var probeCovered = activeCases.Count(Pcre2CorpusExecutionFilter.CanExecuteProbeSubset);
        var replaceCovered = activeCases.Count(Pcre2CorpusExecutionFilter.CanExecuteReplaceSubset);
        var countCovered = activeCases.Count(Pcre2CorpusExecutionFilter.CanExecuteCountSubset);
        var enumerateCovered = activeCases.Count(Pcre2CorpusExecutionFilter.CanExecuteEnumerateSubset);

        var compileRejected = activeCases.Count(static c =>
            c.Operation == Pcre2CorpusOperationKind.Compile &&
            c.Expected.Outcome == Pcre2CorpusOutcomeKind.CompileError);

        var runtimeRejected = activeCases.Count(static c =>
            c.Expected.ErrorKind is "NotSupportedException" or "DisallowedLookaroundBackslashK");

        var deferred = activeCases.Where(static c => !IsExplicitlyLedgered(c)).Select(static c => c.Id).OrderBy(static id => id, StringComparer.Ordinal).ToArray();

        var summary = new StringBuilder()
            .AppendLine($"Active={activeCases.Count}")
            .AppendLine($"CompileCovered={compileCovered}")
            .AppendLine($"ManagedMatchCovered={managedMatchCovered}")
            .AppendLine($"SpecialMatchCovered={specialMatchCovered}")
            .AppendLine($"ProbeCovered={probeCovered}")
            .AppendLine($"ReplaceCovered={replaceCovered}")
            .AppendLine($"CountCovered={countCovered}")
            .AppendLine($"EnumerateCovered={enumerateCovered}")
            .AppendLine($"CompileRejected={compileRejected}")
            .AppendLine($"RuntimeRejected={runtimeRejected}")
            .AppendLine($"Deferred={deferred.Length}")
            .AppendLine("DeferredIds:")
            .AppendJoin('\n', deferred)
            .ToString();

        // This frozen snapshot is the project closure ledger for the active PCRE2 corpus.
        // Changes here should come from an explicit support/reject decision, not incidental drift.
        Assert.Equal(
            """
            Active=600
            CompileCovered=16
            ManagedMatchCovered=26
            SpecialMatchCovered=196
            ProbeCovered=119
            ReplaceCovered=170
            CountCovered=37
            EnumerateCovered=36
            CompileRejected=16
            RuntimeRejected=17
            Deferred=0
            DeferredIds:

            """.ReplaceLineEndings(Environment.NewLine),
            summary.ReplaceLineEndings(Environment.NewLine));
    }

    private static bool IsExplicitlyLedgered(Pcre2CorpusCase corpusCase)
    {
        return Pcre2CorpusExecutionFilter.CanExecuteCompile(corpusCase) ||
            Pcre2CorpusExecutionFilter.CanExecuteManagedMatchSubset(corpusCase) ||
            Pcre2CorpusExecutionFilter.CanExecuteSpecialMatchSubset(corpusCase) ||
            Pcre2CorpusExecutionFilter.CanExecuteProbeSubset(corpusCase) ||
            Pcre2CorpusExecutionFilter.CanExecuteReplaceSubset(corpusCase) ||
            Pcre2CorpusExecutionFilter.CanExecuteCountSubset(corpusCase) ||
            Pcre2CorpusExecutionFilter.CanExecuteEnumerateSubset(corpusCase) ||
            corpusCase.Operation == Pcre2CorpusOperationKind.Compile ||
            corpusCase.Expected.ErrorKind is "NotSupportedException" or "DisallowedLookaroundBackslashK";
    }
}
