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
            ParseCompileOptions(corpusCase.CompileOptions),
            CreateSettings(corpusCase.CompileSettings));

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
