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
            ParseCompileOptions(corpusCase.CompileOptions),
            CreateSettings(corpusCase.CompileSettings));

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

    private static Pcre2CompileOptions ParseCompileOptions(string[] options)
    {
        var result = Pcre2CompileOptions.None;
        foreach (var option in options)
        {
            result |= option switch
            {
                "IgnoreCase" => Pcre2CompileOptions.Caseless,
                _ => Enum.Parse<Pcre2CompileOptions>(option, ignoreCase: false),
            };
        }

        return result;
    }

    private static Utf8Pcre2CompileSettings CreateSettings(Pcre2CorpusCompileSettings settings)
    {
        return new Utf8Pcre2CompileSettings
        {
            AllowDuplicateNames = settings.AllowDuplicateNames,
            BackslashC = Enum.Parse<Pcre2BackslashCPolicy>(settings.BackslashC, ignoreCase: false),
            AllowLookaroundBackslashK = settings.AllowLookaroundBackslashK,
            Newline = Enum.Parse<Pcre2NewlineConvention>(settings.Newline, ignoreCase: false),
            Bsr = Enum.Parse<Pcre2BsrConvention>(settings.Bsr, ignoreCase: false),
        };
    }
}
