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
            Pcre2CorpusOptions.ParseCompileOptions(corpusCase.CompileOptions),
            Pcre2CorpusOptions.CreateCompileSettings(corpusCase.CompileSettings),
            default,
            Utf8Pcre2Regex.DefaultMatchTimeout);

        var input = Encoding.UTF8.GetBytes(corpusCase.InputText);
        var replacementOptions = ParseSubstitutionOptions(corpusCase.SubstitutionOptions);
        if (corpusCase.Expected.Outcome == Pcre2CorpusOutcomeKind.ReplacementError)
        {
            var exception = Assert.Throws<Pcre2SubstitutionException>(
                () => regex.Replace(input, corpusCase.ReplacementPattern!, corpusCase.StartOffsetInBytes, replacementOptions));
            Assert.Equal(corpusCase.Expected.ErrorKind, exception.ErrorKind.ToString());
            return;
        }

        var output = regex.Replace(input, corpusCase.ReplacementPattern!, corpusCase.StartOffsetInBytes, replacementOptions);
        Assert.Equal(corpusCase.Expected.ReplacementText, Encoding.UTF8.GetString(output));
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

}
