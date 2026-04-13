using Lokad.Utf8Regex.Pcre2.Tests.Corpus;

namespace Lokad.Utf8Regex.Pcre2.Tests;

public sealed class Pcre2CorpusFamilyInventoryTests
{
    [Fact]
    public void CuratedCorpusCasesResolveToLocalSemanticFamilies()
    {
        var cases = Pcre2CorpusLoader.LoadAll();

        Assert.All(
            cases,
            static corpusCase => Assert.NotEmpty(
                Pcre2CorpusFamilyTags.GetAll(corpusCase)));
    }

    [Fact]
    public void CuratedCorpusMaintainsLocalFamilyDepth()
    {
        var cases = Pcre2CorpusLoader.LoadAll();

        Assert.True(Count(cases, Pcre2CorpusFamilyTags.BranchReset) >= 20, "Expected broad local branch-reset coverage.");
        Assert.True(Count(cases, Pcre2CorpusFamilyTags.DuplicateNames) >= 10, "Expected local duplicate-name coverage.");
        Assert.True(Count(cases, Pcre2CorpusFamilyTags.LookaroundK) >= 10, "Expected local \\K coverage.");
        Assert.True(Count(cases, Pcre2CorpusFamilyTags.BackslashC) >= 1, "Expected local \\C coverage.");
        Assert.True(Count(cases, Pcre2CorpusFamilyTags.ControlVerb) >= 10, "Expected local control-verb coverage.");
        Assert.True(Count(cases, Pcre2CorpusFamilyTags.Mark) >= 8, "Expected local MARK coverage.");
        Assert.True(Count(cases, Pcre2CorpusFamilyTags.Partial) >= 10, "Expected local partial/probe coverage.");
        Assert.True(Count(cases, Pcre2CorpusFamilyTags.Replace) >= 70, "Expected local replacement coverage.");
        Assert.True(Count(cases, Pcre2CorpusFamilyTags.Probe) >= 10, "Expected local probe coverage.");
        Assert.True(Count(cases, Pcre2CorpusFamilyTags.GlobalIteration) >= 15, "Expected local global-iteration coverage.");
        Assert.True(Count(cases, Pcre2CorpusFamilyTags.Recursion) >= 10, "Expected local recursion/subroutine coverage.");
        Assert.True(Count(cases, Pcre2CorpusFamilyTags.NamedCaptures) >= 30, "Expected local named-capture coverage.");
    }

    [Fact]
    public void ActiveCorpusMaintainsLocalFamilyDepth()
    {
        var activeCases = Pcre2CorpusLoader.LoadByStatus(Pcre2CorpusCaseStatus.Active);

        Assert.True(Count(activeCases, Pcre2CorpusFamilyTags.BranchReset) >= 20, "Expected active branch-reset coverage.");
        Assert.True(Count(activeCases, Pcre2CorpusFamilyTags.DuplicateNames) >= 10, "Expected active duplicate-name coverage.");
        Assert.True(Count(activeCases, Pcre2CorpusFamilyTags.LookaroundK) >= 10, "Expected active \\K coverage.");
        Assert.True(Count(activeCases, Pcre2CorpusFamilyTags.Replace) >= 70, "Expected active replacement coverage.");
        Assert.True(Count(activeCases, Pcre2CorpusFamilyTags.GlobalIteration) >= 15, "Expected active global-iteration coverage.");
        Assert.True(Count(activeCases, Pcre2CorpusFamilyTags.Recursion) >= 10, "Expected active recursion coverage.");
    }

    private static int Count(IReadOnlyList<Pcre2CorpusCase> cases, string tag)
        => cases.Count(corpusCase => Pcre2CorpusFamilyTags.Has(corpusCase, tag));
}
