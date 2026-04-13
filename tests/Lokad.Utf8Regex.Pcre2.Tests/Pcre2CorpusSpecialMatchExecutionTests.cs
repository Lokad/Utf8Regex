using System.Text;
using Lokad.Utf8Regex.Pcre2;
using Lokad.Utf8Regex.Pcre2.Tests.Corpus;

namespace Lokad.Utf8Regex.Pcre2.Tests;

public sealed class Pcre2CorpusSpecialMatchExecutionTests
{
    [Theory]
    [MemberData(nameof(Pcre2CorpusExecutionData.SpecialMatchSubsetCases), MemberType = typeof(Pcre2CorpusExecutionData))]
    public void SpecialSubsetCorpusMatchCasesProduceExpectedResults(Pcre2CorpusCase corpusCase)
    {
        var regex = new Utf8Pcre2Regex(
            corpusCase.Pattern,
            ParseCompileOptions(corpusCase.CompileOptions),
            CreateSettings(corpusCase.CompileSettings));

        var input = Encoding.UTF8.GetBytes(corpusCase.InputText);

        if (corpusCase.Operation == Pcre2CorpusOperationKind.Match)
        {
            if (corpusCase.Expected.Outcome == Pcre2CorpusOutcomeKind.MatchError)
            {
                var exception = Assert.Throws<Pcre2MatchException>(() => regex.Match(input, corpusCase.StartOffsetInBytes));
                Assert.Equal(corpusCase.Expected.ErrorKind, exception.ErrorKind);
                return;
            }

            var match = regex.Match(input, corpusCase.StartOffsetInBytes);
            Assert.Equal(corpusCase.Expected.Outcome == Pcre2CorpusOutcomeKind.FullMatch, match.Success);
            if (match.Success)
            {
                Assert.Equal(corpusCase.Expected.StartOffsetInBytes, match.StartOffsetInBytes);
                Assert.Equal(corpusCase.Expected.EndOffsetInBytes, match.EndOffsetInBytes);
                if (corpusCase.Expected.HasContiguousByteRange is bool hasContiguous)
                {
                    Assert.Equal(hasContiguous, match.HasContiguousByteRange);
                }

                if (corpusCase.Expected.ValueText is not null)
                {
                    Assert.Equal(corpusCase.Expected.ValueText, match.GetValueString());
                }
            }
        }
        else
        {
            if (corpusCase.Expected.Outcome == Pcre2CorpusOutcomeKind.MatchError)
            {
                var exception = Assert.Throws<Pcre2MatchException>(() => regex.MatchDetailed(input, corpusCase.StartOffsetInBytes));
                Assert.Equal(corpusCase.Expected.ErrorKind, exception.ErrorKind);
                return;
            }

            var match = regex.MatchDetailed(input, corpusCase.StartOffsetInBytes);
            Assert.Equal(corpusCase.Expected.Outcome == Pcre2CorpusOutcomeKind.FullMatch, match.Success);
            Assert.Equal(corpusCase.Expected.Mark, match.Mark);

            if (match.Success)
            {
                if (corpusCase.Expected.HasContiguousByteRange is bool hasContiguous)
                {
                    Assert.Equal(hasContiguous, match.Value.HasContiguousByteRange);
                }

                foreach (var expectedGroup in corpusCase.Expected.Groups)
                {
                    var actualGroup = match.GetGroup(expectedGroup.Number);
                    Assert.Equal(expectedGroup.Success, actualGroup.Success);
                    if (expectedGroup.Success)
                    {
                        Assert.Equal(expectedGroup.StartOffsetInBytes, actualGroup.StartOffsetInBytes);
                        Assert.Equal(expectedGroup.EndOffsetInBytes, actualGroup.EndOffsetInBytes);
                        if (expectedGroup.ValueText is not null)
                        {
                            Assert.Equal(expectedGroup.ValueText, actualGroup.GetValueString());
                        }
                    }
                }

                if (corpusCase.Expected.NameEntries.Length > 0)
                {
                    var entries = new Pcre2NameEntry[8];
                    var written = match.CopyNameEntries(entries, out var isMore);
                    Assert.False(isMore);
                    Assert.Equal(corpusCase.Expected.NameEntries.Length, written);
                    for (var i = 0; i < written; i++)
                    {
                        Assert.Equal(corpusCase.Expected.NameEntries[i].Name, entries[i].Name);
                        Assert.Equal(corpusCase.Expected.NameEntries[i].Number, entries[i].Number);
                    }
                }
            }
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
