using Lokad.Utf8Regex.Pcre2.Tests.Corpus;

namespace Lokad.Utf8Regex.Pcre2.Tests;

public sealed class Pcre2CorpusCoverageComparisonTests
{
    [Fact]
    public void LocalBacklogExactlyAccountsForNonActiveCuratedCases()
    {
        var manifests = Pcre2CorpusBacklogLoader.LoadManifests();
        var backlog = Pcre2CorpusBacklogLoader.LoadAll();
        var nonActiveCases = Pcre2CorpusLoader.LoadAll()
            .Where(static c => c.Status != Pcre2CorpusCaseStatus.Active)
            .ToArray();

        Assert.Equal(backlog.Count, manifests.Sum(static manifest => manifest.Entries.Count));
        Assert.Equal(nonActiveCases.Length, backlog.Count);
        Assert.Equal(backlog.Count, backlog.Select(static entry => entry.Id).Distinct(StringComparer.Ordinal).Count());

        foreach (var backlogEntry in backlog)
        {
            var corpusCase = Assert.Single(nonActiveCases, c => c.Id == backlogEntry.Id);
            Assert.Equal(corpusCase.Operation, backlogEntry.Operation);
            Assert.Equal(corpusCase.Status, backlogEntry.Status);
            Assert.Equal(corpusCase.Source, backlogEntry.Source);
            Assert.False(string.IsNullOrWhiteSpace(backlogEntry.Reason));
        }
    }

}
