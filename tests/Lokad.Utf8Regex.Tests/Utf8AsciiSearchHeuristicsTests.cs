using Lokad.Utf8Regex.Internal.Planning;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8AsciiSearchHeuristicsTests
{
    [Theory]
    [InlineData('q', 10)]
    [InlineData('V', 8)]
    [InlineData('m', 6)]
    [InlineData('o', 5)]
    [InlineData('S', 4)]
    [InlineData('e', 2)]
    [InlineData('7', 5)]
    [InlineData('_', 3)]
    [InlineData('-', 1)]
    public void AnchorRarityScoreUsesOneCaseInvariantTruthTable(char value, int expected)
    {
        Assert.Equal(expected, Utf8AsciiSearchHeuristics.GetAnchorRarityScore((byte)value));
    }
}
