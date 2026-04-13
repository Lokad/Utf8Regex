using Lokad.Utf8Regex.Pcre2.Tests.Corpus;

namespace Lokad.Utf8Regex.Pcre2.Tests;

public sealed class Pcre2CorpusInventoryTests
{
    [Fact]
    public void Pcre2CorpusCasesHaveUniqueIdsAndSources()
    {
        var cases = Pcre2CorpusLoader.LoadAll();

        Assert.NotEmpty(cases);
        Assert.Equal(cases.Count, cases.Select(static c => c.Id).Distinct(StringComparer.Ordinal).Count());

        foreach (var corpusCase in cases)
        {
            Assert.False(string.IsNullOrWhiteSpace(corpusCase.Id), "Corpus case id must be present.");
            Assert.False(string.IsNullOrWhiteSpace(corpusCase.Source), $"Corpus case '{corpusCase.Id}' must have provenance.");
            Assert.True(
                corpusCase.Source.StartsWith("pcre2:", StringComparison.Ordinal) ||
                corpusCase.Source.StartsWith("ported/", StringComparison.Ordinal) ||
                corpusCase.Source.StartsWith("spec-pcre2:", StringComparison.Ordinal),
                $"Corpus case '{corpusCase.Id}' has unrecognized provenance '{corpusCase.Source}'.");
            Assert.True(corpusCase.StartOffsetInBytes >= 0, $"Negative start offset for corpus case '{corpusCase.Id}'.");
        }
    }

    [Fact]
    public void Pcre2CorpusReplacementCasesAlwaysDeclareReplacementPatterns()
    {
        var replacementCases = Pcre2CorpusLoader.LoadAll()
            .Where(static c => c.Operation == Pcre2CorpusOperationKind.Replace)
            .ToArray();

        Assert.NotEmpty(replacementCases);

        foreach (var corpusCase in replacementCases)
        {
            Assert.False(string.IsNullOrEmpty(corpusCase.ReplacementPattern), $"Replacement pattern is missing for corpus case '{corpusCase.Id}'.");
        }
    }

    [Fact]
    public void Pcre2CorpusGlobalOperationCasesDeclareSequenceExpectations()
    {
        var cases = Pcre2CorpusLoader.LoadAll();

        foreach (var corpusCase in cases.Where(static c => c.Operation == Pcre2CorpusOperationKind.Count))
        {
            if (corpusCase.Expected.Outcome == Pcre2CorpusOutcomeKind.FullMatch)
            {
                Assert.True(corpusCase.Expected.MatchCount is >= 0, $"Count case '{corpusCase.Id}' must declare Expected.MatchCount.");
            }
        }

        foreach (var corpusCase in cases.Where(static c => c.Operation == Pcre2CorpusOperationKind.EnumerateMatches))
        {
            if (corpusCase.Expected.Outcome == Pcre2CorpusOutcomeKind.FullMatch)
            {
                Assert.NotEmpty(corpusCase.Expected.Matches);
                Assert.True(
                    corpusCase.Expected.MatchCount is null || corpusCase.Expected.MatchCount == corpusCase.Expected.Matches.Length,
                    $"Enumerate case '{corpusCase.Id}' has inconsistent MatchCount.");
            }
        }
    }

    [Fact]
    public void Pcre2CorpusMaintainsInventoryDepthAcrossStatusesAndOperations()
    {
        var cases = Pcre2CorpusLoader.LoadAll();

        Assert.True(cases.Count >= 218, $"Expected at least 218 corpus cases, found {cases.Count}.");
        Assert.True(cases.Count(static c => c.Status == Pcre2CorpusCaseStatus.Active) >= 190, "Expected a large active corpus.");
        Assert.True(cases.Count(static c => c.Status == Pcre2CorpusCaseStatus.OutOfScopeBySpec) >= 20, "Expected an explicit remaining out-of-scope backlog.");
        Assert.True(cases.Count(static c => c.Operation == Pcre2CorpusOperationKind.Replace) >= 70, "Expected broad replacement coverage.");
        Assert.True(cases.Count(static c => c.Operation == Pcre2CorpusOperationKind.MatchDetailed) >= 65, "Expected broad detailed-match coverage.");
        Assert.True(cases.Count(static c => c.Operation == Pcre2CorpusOperationKind.Match) >= 27, "Expected broad simple-match coverage.");
        Assert.True(cases.Count(static c => c.Operation == Pcre2CorpusOperationKind.Count) >= 8, "Expected explicit count semantics coverage.");
        Assert.True(cases.Count(static c => c.Operation == Pcre2CorpusOperationKind.EnumerateMatches) >= 9, "Expected explicit enumerate semantics coverage.");
    }

    [Fact]
    public void Pcre2CorpusCoversKeySemanticFamilies()
    {
        var cases = Pcre2CorpusLoader.LoadAll();

        Assert.Contains(cases, static c => Pcre2CorpusFamilyTags.Has(c, Pcre2CorpusFamilyTags.BranchReset));
        Assert.Contains(cases, static c => Pcre2CorpusFamilyTags.Has(c, Pcre2CorpusFamilyTags.DuplicateNames));
        Assert.Contains(cases, static c => Pcre2CorpusFamilyTags.Has(c, Pcre2CorpusFamilyTags.ControlVerb));
        Assert.Contains(cases, static c => Pcre2CorpusFamilyTags.Has(c, Pcre2CorpusFamilyTags.LookaroundK));
        Assert.Contains(cases, static c => Pcre2CorpusFamilyTags.Has(c, Pcre2CorpusFamilyTags.BackslashC));
        Assert.Contains(cases, static c => Pcre2CorpusFamilyTags.Has(c, Pcre2CorpusFamilyTags.Probe));
        Assert.Contains(cases, static c => Pcre2CorpusFamilyTags.Has(c, Pcre2CorpusFamilyTags.GlobalIteration));
    }
}
