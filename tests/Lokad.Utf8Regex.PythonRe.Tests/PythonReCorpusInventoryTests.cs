using Lokad.Utf8Regex.PythonRe.Tests.Corpus;

namespace Lokad.Utf8Regex.PythonRe.Tests;

public sealed class PythonReCorpusInventoryTests
{
    [Fact]
    public void CorpusIdsAreUnique()
    {
        var cases = PythonReCorpusLoader.LoadAll();

        var duplicateIds = cases
            .GroupBy(c => c.Id, StringComparer.Ordinal)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToArray();

        Assert.Empty(duplicateIds);
    }

    [Fact]
    public void CorpusSourcesArePresent()
    {
        var cases = PythonReCorpusLoader.LoadAll();

        Assert.All(cases, c => Assert.False(string.IsNullOrWhiteSpace(c.Source)));
    }
}
