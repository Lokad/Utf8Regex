using System.Text;
using Lokad.Utf8Regex.Pcre2;
using Lokad.Utf8Regex.Pcre2.Tests.Corpus;

namespace Lokad.Utf8Regex.Pcre2.Tests;

public sealed class Pcre2CorpusEnumerateExecutionTests
{
    [Theory]
    [MemberData(nameof(Pcre2CorpusExecutionData.EnumerateSubsetCases), MemberType = typeof(Pcre2CorpusExecutionData))]
    public void EnumerateSubsetCorpusCasesProduceExpectedResults(Pcre2CorpusCase corpusCase)
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
            if (string.Equals(corpusCase.Expected.ErrorKind, Pcre2ErrorKinds.DisallowedLookaroundBackslashK, StringComparison.Ordinal))
            {
                var exception = Assert.Throws<Pcre2MatchException>(() => Collect(regex.EnumerateMatches(input, corpusCase.StartOffsetInBytes)));
                Assert.Equal(corpusCase.Expected.ErrorKind, exception.ErrorKind);
            }
            else
            {
                Assert.Throws<NotSupportedException>(() => Collect(regex.EnumerateMatches(input, corpusCase.StartOffsetInBytes)));
            }

            return;
        }

        var matches = Collect(regex.EnumerateMatches(input, corpusCase.StartOffsetInBytes));
        Assert.Equal(corpusCase.Expected.MatchCount, matches.Count);
        for (var i = 0; i < matches.Count; i++)
        {
            Assert.Equal(corpusCase.Expected.Matches[i].Success, matches[i].Success);
            Assert.Equal(corpusCase.Expected.Matches[i].StartOffsetInBytes, matches[i].StartOffsetInBytes);
            Assert.Equal(corpusCase.Expected.Matches[i].EndOffsetInBytes, matches[i].EndOffsetInBytes);
            Assert.Equal(corpusCase.Expected.Matches[i].ValueText, matches[i].Value);
        }
    }

    private static List<(bool Success, int StartOffsetInBytes, int EndOffsetInBytes, string Value)> Collect(Utf8Pcre2ValueMatchEnumerator enumerator)
    {
        var results = new List<(bool, int, int, string)>();
        while (enumerator.MoveNext())
        {
            var match = enumerator.Current;
            results.Add((match.Success, match.StartOffsetInBytes, match.EndOffsetInBytes, match.GetValueString()));
        }

        return results;
    }

}
