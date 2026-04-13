using Lokad.Utf8Regex.Pcre2.Tests.Corpus;

namespace Lokad.Utf8Regex.Pcre2.Tests;

public sealed class Pcre2CorpusExecutionInventoryTests
{
    [Fact]
    public void ExecutionFiltersNowCoverMeaningfulInactiveCorpus()
    {
        var cases = Pcre2CorpusLoader.LoadAll();

        var executableCompile = cases.Where(Pcre2CorpusExecutionFilter.CanExecuteCompile).ToArray();
        var executableManagedMatch = cases.Where(Pcre2CorpusExecutionFilter.CanExecuteManagedMatchSubset).ToArray();
        var executableSpecialMatch = cases.Where(Pcre2CorpusExecutionFilter.CanExecuteSpecialMatchSubset).ToArray();
        var executableProbe = cases.Where(Pcre2CorpusExecutionFilter.CanExecuteProbeSubset).ToArray();
        var executableReplace = cases.Where(Pcre2CorpusExecutionFilter.CanExecuteReplaceSubset).ToArray();
        var executableCount = cases.Where(Pcre2CorpusExecutionFilter.CanExecuteCountSubset).ToArray();
        var executableEnumerate = cases.Where(Pcre2CorpusExecutionFilter.CanExecuteEnumerateSubset).ToArray();

        Assert.True(executableCompile.Length >= 10, "Expected broad executable compile coverage.");
        Assert.True(executableManagedMatch.Length >= 18, "Expected a substantial managed match subset.");
        Assert.True(executableSpecialMatch.Length >= 54, "Expected a substantial special-match subset.");
        Assert.True(executableProbe.Length >= 1, "Expected probe execution coverage.");
        Assert.True(executableReplace.Length >= 35, "Expected substantial replacement execution coverage.");
        Assert.True(executableCount.Length >= 8, "Expected explicit count execution coverage.");
        Assert.True(executableEnumerate.Length >= 9, "Expected explicit enumerate execution coverage.");

        var executableActive = executableManagedMatch
            .Concat(executableSpecialMatch)
            .Concat(executableProbe)
            .Concat(executableReplace)
            .Concat(executableCount)
            .Concat(executableEnumerate)
            .Where(static c => c.Status == Pcre2CorpusCaseStatus.Active)
            .Select(static c => c.Id)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        var inactiveExecutable = executableManagedMatch
            .Concat(executableSpecialMatch)
            .Concat(executableProbe)
            .Concat(executableReplace)
            .Concat(executableCount)
            .Concat(executableEnumerate)
            .Where(static c => c.Status != Pcre2CorpusCaseStatus.Active)
            .Select(static c => c.Id)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        Assert.True(executableActive.Length >= 150, "Expected a large active executable corpus.");
        Assert.True(inactiveExecutable.Length >= 5, "Expected a small but non-zero executable backlog outside the active set.");
    }
}
