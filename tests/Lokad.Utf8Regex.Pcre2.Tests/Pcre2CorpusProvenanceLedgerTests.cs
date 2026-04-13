using System.Text;

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

    [Fact]
    public void LocalProvenanceSummaryMatchesExpectation()
    {
        var provenance = Pcre2CorpusProvenanceLoader.LoadAll();

        var summary = new StringBuilder()
            .AppendLine($"Total={provenance.Count}")
            .AppendLine($"Active={provenance.Count(static entry => entry.Status == Pcre2CorpusCaseStatus.Active)}")
            .AppendLine($"UnsupportedYet={provenance.Count(static entry => entry.Status == Pcre2CorpusCaseStatus.UnsupportedYet)}")
            .AppendLine($"OutOfScopeBySpec={provenance.Count(static entry => entry.Status == Pcre2CorpusCaseStatus.OutOfScopeBySpec)}")
            .AppendLine($"Compile={provenance.Count(static entry => entry.Operation == Pcre2CorpusOperationKind.Compile)}")
            .AppendLine($"Count={provenance.Count(static entry => entry.Operation == Pcre2CorpusOperationKind.Count)}")
            .AppendLine($"EnumerateMatches={provenance.Count(static entry => entry.Operation == Pcre2CorpusOperationKind.EnumerateMatches)}")
            .AppendLine($"Match={provenance.Count(static entry => entry.Operation == Pcre2CorpusOperationKind.Match)}")
            .AppendLine($"MatchDetailed={provenance.Count(static entry => entry.Operation == Pcre2CorpusOperationKind.MatchDetailed)}")
            .AppendLine($"Probe={provenance.Count(static entry => entry.Operation == Pcre2CorpusOperationKind.Probe)}")
            .AppendLine($"Replace={provenance.Count(static entry => entry.Operation == Pcre2CorpusOperationKind.Replace)}")
            .AppendLine($"testinput1={provenance.Count(static entry => entry.SourceFile == "testdata/testinput1")}")
            .AppendLine($"testinput2={provenance.Count(static entry => entry.SourceFile == "testdata/testinput2")}")
            .AppendLine($"testinput20={provenance.Count(static entry => entry.SourceFile == "testdata/testinput20")}")
            .AppendLine($"testinput21={provenance.Count(static entry => entry.SourceFile == "testdata/testinput21")}")
            .AppendLine($"testinput22={provenance.Count(static entry => entry.SourceFile == "testdata/testinput22")}")
            .AppendLine($"testinput5={provenance.Count(static entry => entry.SourceFile == "testdata/testinput5")}")
            .AppendLine($"testinput6={provenance.Count(static entry => entry.SourceFile == "testdata/testinput6")}");

        Assert.Equal(
            """
            Total=433
            Active=413
            UnsupportedYet=0
            OutOfScopeBySpec=20
            Compile=16
            Count=11
            EnumerateMatches=10
            Match=123
            MatchDetailed=100
            Probe=1
            Replace=172
            testinput1=198
            testinput2=211
            testinput20=8
            testinput21=1
            testinput22=2
            testinput5=1
            testinput6=12

            """.ReplaceLineEndings(Environment.NewLine),
            summary.ToString().ReplaceLineEndings(Environment.NewLine));
    }

    private static string GetSourceFile(string source)
    {
        var parts = source.Split(':', StringSplitOptions.None);
        Assert.True(parts.Length >= 2, $"Unexpected source format: '{source}'.");
        return parts[1];
    }
}
