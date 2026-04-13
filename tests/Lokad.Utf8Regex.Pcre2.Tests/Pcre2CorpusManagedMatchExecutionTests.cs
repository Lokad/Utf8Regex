using System.Text;
using Lokad.Utf8Regex.Pcre2;
using Lokad.Utf8Regex.Pcre2.Tests.Corpus;

namespace Lokad.Utf8Regex.Pcre2.Tests;

public sealed class Pcre2CorpusManagedMatchExecutionTests
{
    [Theory]
    [MemberData(nameof(Pcre2CorpusExecutionData.ManagedMatchSubsetCases), MemberType = typeof(Pcre2CorpusExecutionData))]
    public void ManagedSubsetCorpusMatchCasesProduceExpectedResults(Pcre2CorpusCase corpusCase)
    {
        var regex = new Utf8Pcre2Regex(
            corpusCase.Pattern,
            ParseCompileOptions(corpusCase.CompileOptions),
            CreateSettings(corpusCase.CompileSettings));

        var input = Encoding.UTF8.GetBytes(corpusCase.InputText);

        if (corpusCase.Operation == Pcre2CorpusOperationKind.Match)
        {
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
            var match = regex.MatchDetailed(input, corpusCase.StartOffsetInBytes);
            Assert.Equal(corpusCase.Expected.Outcome == Pcre2CorpusOutcomeKind.FullMatch, match.Success);
            if (match.Success)
            {
                Assert.Equal(corpusCase.Expected.StartOffsetInBytes, match.Value.StartOffsetInBytes);
                Assert.Equal(corpusCase.Expected.EndOffsetInBytes, match.Value.EndOffsetInBytes);
                if (corpusCase.Expected.HasContiguousByteRange is bool hasContiguous)
                {
                    Assert.Equal(hasContiguous, match.Value.HasContiguousByteRange);
                }

                if (corpusCase.Expected.ValueText is not null)
                {
                    Assert.Equal(corpusCase.Expected.ValueText, match.GetValueString());
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
