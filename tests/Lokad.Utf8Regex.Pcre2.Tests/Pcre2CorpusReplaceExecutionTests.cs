using System.Text;
using Lokad.Utf8Regex.Pcre2;
using Lokad.Utf8Regex.Pcre2.Tests.Corpus;

namespace Lokad.Utf8Regex.Pcre2.Tests;

public sealed class Pcre2CorpusReplaceExecutionTests
{
    [Theory]
    [MemberData(nameof(Pcre2CorpusExecutionData.ReplaceSubsetCases), MemberType = typeof(Pcre2CorpusExecutionData))]
    public void ReplaceSubsetCorpusCasesProduceExpectedResults(Pcre2CorpusCase corpusCase)
    {
        var regex = new Utf8Pcre2Regex(
            corpusCase.Pattern,
            ParseCompileOptions(corpusCase.CompileOptions),
            CreateSettings(corpusCase.CompileSettings));

        var input = Encoding.UTF8.GetBytes(corpusCase.InputText);
        var replacementOptions = ParseSubstitutionOptions(corpusCase.SubstitutionOptions);
        if (corpusCase.Expected.Outcome == Pcre2CorpusOutcomeKind.ReplacementError)
        {
            var exception = Assert.Throws<Pcre2SubstitutionException>(
                () => regex.Replace(input, corpusCase.ReplacementPattern!, corpusCase.StartOffsetInBytes, replacementOptions));
            Assert.Equal(corpusCase.Expected.ErrorKind, exception.ErrorKind);
            return;
        }

        var output = regex.Replace(input, corpusCase.ReplacementPattern!, corpusCase.StartOffsetInBytes, replacementOptions);
        Assert.Equal(corpusCase.Expected.ReplacementText, Encoding.UTF8.GetString(output));
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

    private static Pcre2SubstitutionOptions ParseSubstitutionOptions(string[] options)
    {
        var result = Pcre2SubstitutionOptions.None;
        foreach (var option in options)
        {
            result |= Enum.Parse<Pcre2SubstitutionOptions>(option, ignoreCase: false);
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
