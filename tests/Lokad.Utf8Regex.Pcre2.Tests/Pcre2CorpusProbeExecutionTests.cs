using System.Text;
using Lokad.Utf8Regex.Pcre2;
using Lokad.Utf8Regex.Pcre2.Tests.Corpus;

namespace Lokad.Utf8Regex.Pcre2.Tests;

public sealed class Pcre2CorpusProbeExecutionTests
{
    [Theory]
    [MemberData(nameof(Pcre2CorpusExecutionData.ProbeSubsetCases), MemberType = typeof(Pcre2CorpusExecutionData))]
    public void ProbeSubsetCorpusCasesProduceExpectedResults(Pcre2CorpusCase corpusCase)
    {
        var regex = new Utf8Pcre2Regex(
            corpusCase.Pattern,
            Pcre2CorpusOptions.ParseCompileOptions(corpusCase.CompileOptions),
            Pcre2CorpusOptions.CreateCompileSettings(corpusCase.CompileSettings),
            default,
            Utf8Pcre2Regex.DefaultMatchTimeout);

        var input = Encoding.UTF8.GetBytes(corpusCase.InputText);
        var probe = regex.Probe(input, Enum.Parse<Pcre2PartialMode>(corpusCase.PartialMode, ignoreCase: false), corpusCase.StartOffsetInBytes);

        Assert.Equal(
            corpusCase.Expected.Outcome switch
            {
                Pcre2CorpusOutcomeKind.FullMatch => Utf8Pcre2ProbeKind.FullMatch,
                Pcre2CorpusOutcomeKind.PartialMatch => Utf8Pcre2ProbeKind.PartialMatch,
                _ => Utf8Pcre2ProbeKind.NoMatch,
            },
            probe.Kind);

        if (probe.Kind == Utf8Pcre2ProbeKind.NoMatch)
        {
            return;
        }

        Assert.Equal(corpusCase.Expected.StartOffsetInBytes, probe.Value.StartOffsetInBytes);
        Assert.Equal(corpusCase.Expected.EndOffsetInBytes, probe.Value.EndOffsetInBytes);

        if (probe.Kind == Utf8Pcre2ProbeKind.FullMatch)
        {
            Assert.Equal(corpusCase.Expected.ValueText, probe.GetMatch().GetValueString());
        }
        else
        {
            Assert.Equal(corpusCase.Expected.ValueText, probe.GetPartial().GetValueString());
        }
    }

}
