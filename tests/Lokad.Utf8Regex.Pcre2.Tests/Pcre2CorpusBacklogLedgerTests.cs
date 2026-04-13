using System.Text;

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

        Assert.Collection(
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

    [Fact]
    public void LocalBacklogSummaryMatchesExpectation()
    {
        var allCases = Pcre2CorpusLoader.LoadAll()
            .ToDictionary(static corpusCase => corpusCase.Id, StringComparer.Ordinal);
        var backlog = Pcre2CorpusBacklogLoader.LoadAll();

        var summary = new StringBuilder()
            .AppendLine($"Total={backlog.Count}")
            .AppendLine($"UnsupportedYet={backlog.Count(static entry => entry.Status == Pcre2CorpusCaseStatus.UnsupportedYet)}")
            .AppendLine($"OutOfScopeBySpec={backlog.Count(static entry => entry.Status == Pcre2CorpusCaseStatus.OutOfScopeBySpec)}")
            .AppendLine($"Compile={backlog.Count(static entry => entry.Operation == Pcre2CorpusOperationKind.Compile)}")
            .AppendLine($"MatchDetailed={backlog.Count(static entry => entry.Operation == Pcre2CorpusOperationKind.MatchDetailed)}")
            .AppendLine($"EnumerateMatches={backlog.Count(static entry => entry.Operation == Pcre2CorpusOperationKind.EnumerateMatches)}")
            .AppendLine($"Replace={backlog.Count(static entry => entry.Operation == Pcre2CorpusOperationKind.Replace)}")
            .AppendLine($"BranchReset={backlog.Count(entry => Pcre2CorpusFamilyTags.Has(allCases[entry.Id], Pcre2CorpusFamilyTags.BranchReset))}")
            .AppendLine($"DuplicateNames={backlog.Count(entry => Pcre2CorpusFamilyTags.Has(allCases[entry.Id], Pcre2CorpusFamilyTags.DuplicateNames))}")
            .AppendLine($"LookaroundK={backlog.Count(entry => Pcre2CorpusFamilyTags.Has(allCases[entry.Id], Pcre2CorpusFamilyTags.LookaroundK))}")
            .AppendLine($"Partial={backlog.Count(entry => Pcre2CorpusFamilyTags.Has(allCases[entry.Id], Pcre2CorpusFamilyTags.Partial))}")
            .AppendLine($"GlobalIteration={backlog.Count(entry => Pcre2CorpusFamilyTags.Has(allCases[entry.Id], Pcre2CorpusFamilyTags.GlobalIteration))}")
            .AppendLine($"NamedCaptures={backlog.Count(entry => Pcre2CorpusFamilyTags.Has(allCases[entry.Id], Pcre2CorpusFamilyTags.NamedCaptures))}");

        Assert.Equal(
            """
            Total=22
            UnsupportedYet=0
            OutOfScopeBySpec=22
            Compile=1
            MatchDetailed=4
            EnumerateMatches=1
            Replace=16
            BranchReset=4
            DuplicateNames=4
            LookaroundK=1
            Partial=16
            GlobalIteration=1
            NamedCaptures=4

            """.ReplaceLineEndings(Environment.NewLine),
            summary.ToString().ReplaceLineEndings(Environment.NewLine));
    }
}
