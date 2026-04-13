using Lokad.Utf8Regex.Pcre2.Tests.Corpus;

namespace Lokad.Utf8Regex.Pcre2.Tests;

public sealed class Pcre2CorpusActiveCaseTests
{
    [Theory]
    [MemberData(nameof(Pcre2CorpusCaseData.ActiveCases), MemberType = typeof(Pcre2CorpusCaseData))]
    public void ActivePcre2CorpusCasesHaveConcreteExpectations(Pcre2CorpusCase corpusCase)
    {
        Assert.NotNull(corpusCase.Expected);
        Assert.True(corpusCase.StartOffsetInBytes >= 0, $"Negative start offset for corpus case '{corpusCase.Id}'.");

        if (corpusCase.Operation == Pcre2CorpusOperationKind.Replace)
        {
            Assert.False(string.IsNullOrEmpty(corpusCase.ReplacementPattern), $"Missing replacement pattern for corpus case '{corpusCase.Id}'.");
        }

        if (corpusCase.Expected.StartOffsetInBytes is int start && corpusCase.Expected.EndOffsetInBytes is int end)
        {
            Assert.True(start >= 0, $"Negative expected start offset for corpus case '{corpusCase.Id}'.");
            Assert.True(end >= 0, $"Negative expected end offset for corpus case '{corpusCase.Id}'.");
        }
    }
}
