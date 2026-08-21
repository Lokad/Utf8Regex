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
            Pcre2CorpusOptions.ParseCompileOptions(corpusCase.CompileOptions),
            Pcre2CorpusOptions.CreateCompileSettings(corpusCase.CompileSettings),
            default,
            Utf8Pcre2Regex.DefaultMatchTimeout);

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

}
