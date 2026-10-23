using Lokad.Utf8Regex.Pcre2.Tests.Corpus;

namespace Lokad.Utf8Regex.Pcre2.Tests;

public sealed class Pcre2CorpusBacklogLedgerTests
{
    [Fact]
    public void LocalBacklogManifestsMatchThemeExpectations()
    {
        var manifests = Pcre2CorpusBacklogLoader.LoadManifests()
            .ToDictionary(static manifest => manifest.ManifestName, StringComparer.Ordinal);

        Assert.Equal(3, manifests.Count);

        Assert.All(
            manifests["local-backlog-compile"].Entries,
            static entry => Assert.Equal(Pcre2CorpusOperationKind.Compile, entry.Operation));

        Assert.All(
            manifests["local-backlog-match"].Entries,
            static entry => Assert.True(
                entry.Operation is Pcre2CorpusOperationKind.Match or
                    Pcre2CorpusOperationKind.MatchDetailed or
                    Pcre2CorpusOperationKind.Probe or
                    Pcre2CorpusOperationKind.Count or
                    Pcre2CorpusOperationKind.EnumerateMatches));

        Assert.All(
            manifests["local-backlog-replace"].Entries,
            static entry => Assert.Equal(Pcre2CorpusOperationKind.Replace, entry.Operation));
    }

    [Fact]
    public void LocalBacklogEntriesCarryNormalizedProvenance()
    {
        var backlog = Pcre2CorpusBacklogLoader.LoadAll();

        Assert.All(
            backlog,
            static entry => Assert.True(
                entry.Source.StartsWith("pcre2:", StringComparison.Ordinal) ||
                entry.Source.StartsWith("ported/", StringComparison.Ordinal)));
    }
}
