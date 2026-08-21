using System.Text;
using Lokad.Utf8Regex.Pcre2;
using Lokad.Utf8Regex.Pcre2.Tests.Corpus;

namespace Lokad.Utf8Regex.Pcre2.Tests;

public sealed class Pcre2CorpusCountExecutionTests
{
    [Theory]
    [MemberData(nameof(Pcre2CorpusExecutionData.CountSubsetCases), MemberType = typeof(Pcre2CorpusExecutionData))]
    public void CountSubsetCorpusCasesProduceExpectedResults(Pcre2CorpusCase corpusCase)
    {
        var regex = new Utf8Pcre2Regex(
            corpusCase.Pattern,
            Pcre2CorpusOptions.ParseCompileOptions(corpusCase.CompileOptions),
            Pcre2CorpusOptions.CreateCompileSettings(corpusCase.CompileSettings),
            default,
            Utf8Pcre2Regex.DefaultMatchTimeout);

        var input = Encoding.UTF8.GetBytes(corpusCase.InputText);
        if (corpusCase.Expected.Outcome == Pcre2CorpusOutcomeKind.MatchError)
        {
            if (string.Equals(corpusCase.Expected.ErrorKind, nameof(Pcre2ErrorKind.DisallowedLookaroundBackslashK), StringComparison.Ordinal))
            {
                var exception = Assert.Throws<Pcre2MatchException>(() => regex.Count(input, corpusCase.StartOffsetInBytes));
                Assert.Equal(corpusCase.Expected.ErrorKind, exception.ErrorKind.ToString());
            }
            else
            {
                Assert.Throws<NotSupportedException>(() => regex.Count(input, corpusCase.StartOffsetInBytes));
            }

            return;
        }

        var count = regex.Count(input, corpusCase.StartOffsetInBytes);
        Assert.Equal(corpusCase.Expected.MatchCount, count);
    }

}
