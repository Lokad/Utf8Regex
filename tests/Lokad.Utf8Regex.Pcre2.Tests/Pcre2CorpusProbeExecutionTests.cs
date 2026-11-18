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
        var regex = new Utf8Pcre2Regex(corpusCase.Pattern, options: Pcre2CorpusOptions.ParseCompileOptions(corpusCase.CompileOptions), compileSettings: Pcre2CorpusOptions.CreateCompileSettings(corpusCase.CompileSettings), policy: new Utf8RegexPolicy
        {
            MatchTimeout = Utf8Pcre2Regex.DefaultMatchTimeout
        });

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
            Assert.Equal(corpusCase.Expected.ValueText, System.Text.Encoding.UTF8.GetString(probe.GetMatch().Value.GetValueBytes()));
            ReadOnlySpan<byte> detailed1Input = input;
            var detailed1Captures = new Utf8Pcre2CaptureRange[regex.CaptureGroupCount + 1];
            var detailed = regex.Match(detailed1Input, startOffsetInBytes: corpusCase.StartOffsetInBytes, captures: detailed1Captures);
            var full = probe.GetMatch();
            Assert.Equal((detailed.Success ? detailed1Captures.Length : 0), full.CaptureSlotCount);
            for (var slot = 0; slot < (detailed.Success ? detailed1Captures.Length : 0); slot++)
            {
                var expected = detailed1Captures[slot]; var actual = full.GetGroup(slot);
                Assert.Equal(expected.Success, actual.Success);
                if (!expected.Success) continue;
                Assert.Equal((expected.StartOffsetInBytes, expected.EndOffsetInBytes),
                    (actual.StartOffsetInBytes, actual.EndOffsetInBytes));
            }
        }
        else
        {
            Assert.Equal(corpusCase.Expected.ValueText, System.Text.Encoding.UTF8.GetString(probe.GetPartial().Value.GetValueBytes()));
        }
    }

}
