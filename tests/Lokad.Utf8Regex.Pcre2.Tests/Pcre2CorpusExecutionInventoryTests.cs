using Lokad.Utf8Regex.Pcre2.Tests.Corpus;

namespace Lokad.Utf8Regex.Pcre2.Tests;

public sealed class Pcre2CorpusExecutionInventoryTests
{
    [Fact]
    public void EveryExecutionFilterHasCases()
    {
        var cases = Pcre2CorpusLoader.LoadAll();
        Assert.Contains(cases, Pcre2CorpusExecutionFilter.CanExecuteCompile);
        Assert.Contains(cases, Pcre2CorpusExecutionFilter.CanExecuteManagedMatchSubset);
        Assert.Contains(cases, Pcre2CorpusExecutionFilter.CanExecuteSpecialMatchSubset);
        Assert.Contains(cases, Pcre2CorpusExecutionFilter.CanExecuteProbeSubset);
        Assert.Contains(cases, Pcre2CorpusExecutionFilter.CanExecuteReplaceSubset);
        Assert.Contains(cases, Pcre2CorpusExecutionFilter.CanExecuteCountSubset);
        Assert.Contains(cases, Pcre2CorpusExecutionFilter.CanExecuteEnumerateSubset);
    }
}
