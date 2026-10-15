using Lokad.Utf8Regex.Internal.Planning;
using Lokad.Utf8Regex.Internal.Search;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8AsciiSearchHeuristicsTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(7)]
    [InlineData(15)]
    [InlineData(31)]
    public void FoldedComparisonsRespectAdmittedWindowsAndAsciiCase(int offset)
    {
        // Exercise each primitive read boundary and overlapping tail against a
        // bytewise ASCII oracle, including punctuation that differs by bit 0x20.
        for (var length = 0; length <= 40; length++)
        {
            var literalStorage = Enumerable.Repeat((byte)'!', offset + length + 1).ToArray();
            var candidateStorage = Enumerable.Repeat((byte)'?', offset + length + 1).ToArray();
            var literal = literalStorage.AsSpan(offset, length);
            var candidate = candidateStorage.AsSpan(offset, length);
            for (var index = 0; index < length; index++)
                literal[index] = candidate[index] = (index % 20) switch
                {
                    16 => (byte)'[', 17 => (byte)'\\', 18 => (byte)'_', 19 => (byte)'`',
                    _ => (byte)('a' + index % 26),
                };
            Assert.True(AsciiSearch.MatchesFoldedIgnoreCase(candidate, literal));
            for (var position = 0; position < length; position++)
            {
                var original = candidate[position];
                for (var value = 0; value < 128; value++)
                {
                    candidate[position] = (byte)value;
                    var folded = value is >= 'A' and <= 'Z' ? value + 32 : value;
                    Assert.Equal(folded == literal[position],
                        AsciiSearch.MatchesFoldedIgnoreCase(candidate, literal));
                    if (length is >= 9 and <= 16)
                        Assert.Equal(folded == literal[position],
                            AsciiSearch.MatchesFoldedIgnoreCaseLength9To16(candidate, literal));
                }
                candidate[position] = original;
            }
        }
    }

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
