using Lokad.Utf8Regex.Pcre2.Tests.Corpus;

namespace Lokad.Utf8Regex.Pcre2.Tests;

public sealed class Pcre2CorpusProvenanceLedgerTests
{
    [Fact]
    public void LocalProvenanceIndexExactlyMatchesUpstreamDerivedCuratedCases()
    {
        var provenance = Pcre2CorpusProvenanceLoader.LoadAll();
        var upstreamCases = Pcre2CorpusLoader.LoadAll()
            .Where(static corpusCase => corpusCase.Source.StartsWith("pcre2:", StringComparison.Ordinal))
            .ToArray();

        Assert.Equal(upstreamCases.Length, provenance.Count);
        Assert.Equal(provenance.Count, provenance.Select(static entry => entry.Id).Distinct(StringComparer.Ordinal).Count());

        var provenanceIds = provenance.Select(static entry => entry.Id).ToHashSet(StringComparer.Ordinal);
        foreach (var entry in provenance)
        {
            var corpusCase = Assert.Single(upstreamCases, corpusCase => corpusCase.Id == entry.Id);
            Assert.Equal(corpusCase.Source, entry.Source);
            Assert.Equal(corpusCase.Operation, entry.Operation);
            Assert.Equal(corpusCase.Status, entry.Status);
            Assert.Equal(GetSourceFile(corpusCase.Source), entry.SourceFile);
        }

        foreach (var upstreamCase in upstreamCases)
        {
            Assert.Contains(upstreamCase.Id, provenanceIds);
        }
    }

    private static string GetSourceFile(string source)
    {
        var parts = source.Split(':', StringSplitOptions.None);
        Assert.True(parts.Length >= 2, $"Unexpected source format: '{source}'.");
        return parts[1];
    }
}
