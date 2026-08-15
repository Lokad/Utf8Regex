using System.Text;

using Lokad.Utf8Regex.Pcre2.Tests.Corpus;

namespace Lokad.Utf8Regex.Pcre2.Tests;

public sealed class Pcre2CorpusFamilyLedgerTests
{
    [Fact]
    public void CuratedCorpusFamilySummaryMatchesExpectation()
    {
        var cases = Pcre2CorpusLoader.LoadAll();
        var activeCases = Pcre2CorpusLoader.LoadByStatus(Pcre2CorpusCaseStatus.Active);

        var tags =
            new[]
            {
                Pcre2CorpusFamilyTags.Compile,
                Pcre2CorpusFamilyTags.Match,
                Pcre2CorpusFamilyTags.BranchReset,
                Pcre2CorpusFamilyTags.DuplicateNames,
                Pcre2CorpusFamilyTags.LookaroundK,
                Pcre2CorpusFamilyTags.BackslashC,
                Pcre2CorpusFamilyTags.ControlVerb,
                Pcre2CorpusFamilyTags.Mark,
                Pcre2CorpusFamilyTags.Partial,
                Pcre2CorpusFamilyTags.Replace,
                Pcre2CorpusFamilyTags.Probe,
                Pcre2CorpusFamilyTags.GlobalIteration,
                Pcre2CorpusFamilyTags.Recursion,
                Pcre2CorpusFamilyTags.NamedCaptures,
            };

        var summary = new StringBuilder()
            .AppendLine($"Total={cases.Count}")
            .AppendLine($"Active={activeCases.Count}");

        foreach (var tag in tags)
        {
            summary
                .AppendLine($"{tag}.All={cases.Count(c => Pcre2CorpusFamilyTags.Has(c, tag))}")
                .AppendLine($"{tag}.Active={activeCases.Count(c => Pcre2CorpusFamilyTags.Has(c, tag))}");
        }

        Assert.Equal(
            """
            Total=636
            Active=598
            Compile.All=20
            Compile.Active=16
            Match.All=231
            Match.Active=214
            BranchReset.All=75
            BranchReset.Active=71
            DuplicateNames.All=36
            DuplicateNames.Active=32
            LookaroundK.All=119
            LookaroundK.Active=116
            BackslashC.All=9
            BackslashC.Active=9
            ControlVerb.All=36
            ControlVerb.Active=34
            Mark.All=13
            Mark.Active=13
            Partial.All=135
            Partial.Active=119
            Replace.All=188
            Replace.Active=172
            Probe.All=119
            Probe.Active=119
            GlobalIteration.All=78
            GlobalIteration.Active=77
            Recursion.All=116
            Recursion.Active=103
            NamedCaptures.All=127
            NamedCaptures.Active=112

            """.ReplaceLineEndings(Environment.NewLine),
            summary.ToString().ReplaceLineEndings(Environment.NewLine));
    }
}
