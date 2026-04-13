using Lokad.Utf8Regex.Pcre2.Tests.Corpus;

namespace Lokad.Utf8Regex.Pcre2.Tests;

public sealed class Pcre2CorpusLoaderTests
{
    [Fact]
    public void Pcre2CorpusLoadsSeedCases()
    {
        var cases = Pcre2CorpusLoader.LoadAll();

        Assert.NotEmpty(cases);
        Assert.Contains(cases, static c => c.Status == Pcre2CorpusCaseStatus.Active);
    }

    [Fact]
    public void Pcre2CorpusCaseIdsAreUnique()
    {
        var duplicateIds = Pcre2CorpusLoader
            .LoadAll()
            .GroupBy(static c => c.Id, StringComparer.Ordinal)
            .Where(static g => g.Count() > 1)
            .Select(static g => g.Key)
            .ToArray();

        Assert.Empty(duplicateIds);
    }

    [Fact]
    public void Pcre2CorpusCasesCarrySourceProvenance()
    {
        foreach (var corpusCase in Pcre2CorpusLoader.LoadAll())
        {
            Assert.False(string.IsNullOrWhiteSpace(corpusCase.Source), $"Missing source for corpus case '{corpusCase.Id}'.");
        }
    }

    [Fact]
    public void Pcre2CorpusResolvesSharedPatterns()
    {
        var cases = Pcre2CorpusLoader.LoadAll();

        var referencedCases = cases.Where(static c => !string.IsNullOrWhiteSpace(c.PatternRef)).ToArray();
        Assert.NotEmpty(referencedCases);

        foreach (var corpusCase in referencedCases)
        {
            Assert.False(string.IsNullOrWhiteSpace(corpusCase.Pattern), $"Shared pattern was not resolved for corpus case '{corpusCase.Id}'.");
        }
    }
}
