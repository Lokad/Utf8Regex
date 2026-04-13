using Lokad.Utf8Regex.Pcre2.Tests.Corpus;

namespace Lokad.Utf8Regex.Pcre2.Tests;

public sealed class Pcre2CorpusRoutingTests
{
    [Fact]
    public void ActivePcre2CorpusCasesRouteToSupportedExecutionBuckets()
    {
        foreach (var corpusCase in Pcre2CorpusLoader.LoadByStatus(Pcre2CorpusCaseStatus.Active))
        {
            var executionKind = Pcre2CorpusCaseRouting.GetExecutionKind(corpusCase);
            Assert.True(Enum.IsDefined(executionKind), $"Unexpected execution bucket for '{corpusCase.Id}'.");
        }
    }
}
