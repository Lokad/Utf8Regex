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
                for (var value = 0; value < 256; value++)
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

    [Fact]
    public void FoldedEqualityKeepsNonAsciiByteNeedlesAndPrefixExtent()
    {
        for (var value = 128; value < 256; value++)
            Assert.True(AsciiSearch.MatchesFoldedIgnoreCase(new byte[] { (byte)value }, new byte[] { (byte)value }));

        var literal = System.Text.Encoding.UTF8.GetBytes("éabcdefghijklmnopqrstuvwxyz");
        var candidate = System.Text.Encoding.UTF8.GetBytes("éABCDEFGHIJKLMNOPQRSTUVWXYZ-trailer");
        Assert.True(AsciiSearch.MatchesFoldedIgnoreCase(candidate, literal));
        candidate[0] = 0xff;
        Assert.False(AsciiSearch.MatchesFoldedIgnoreCase(candidate, literal));

        for (var length = 0; length <= 40; length++)
        {
            var ascii = Enumerable.Repeat((byte)'a', length).ToArray();
            var prefix = Enumerable.Repeat((byte)'A', length).Concat(new byte[] { 0xff }).ToArray();
            Assert.True(AsciiSearch.MatchesFoldedIgnoreCase(prefix, ascii));
        }
    }

    [Theory]
    [InlineData(17)]
    [InlineData(24)]
    [InlineData(33)]
    public void PreparedFoldedSearchVerifiesWholeCandidatesAcrossBorrowedTails(int length)
    {
        var needleText = new string(Enumerable.Range(0, length)
            .Select(index => (char)('a' + index % 26)).ToArray());
        var needle = System.Text.Encoding.ASCII.GetBytes(needleText);
        var prepared = new PreparedSubstringSearch(needle, ignoreCase: true);
        var upper = needleText.ToUpperInvariant();
        var nearMiss = upper.ToCharArray();
        nearMiss[length / 2] = '0';
        var ordinary = new Utf8Regex(needleText,
            System.Text.RegularExpressions.RegexOptions.IgnoreCase |
            System.Text.RegularExpressions.RegexOptions.CultureInvariant);
        var compiled = new Utf8Regex(needleText,
            System.Text.RegularExpressions.RegexOptions.IgnoreCase |
            System.Text.RegularExpressions.RegexOptions.CultureInvariant |
            System.Text.RegularExpressions.RegexOptions.Compiled);

        foreach (var padding in new[] { 0, 15, 31, 32, 33 })
        foreach (var storageOffset in new[] { 1, 17, 31 })
        {
            var prefix = new string('.', padding) + new string(nearMiss) + " ";
            var input = System.Text.Encoding.ASCII.GetBytes(prefix + upper);
            var storage = Enumerable.Repeat((byte)0xff, storageOffset + input.Length + 1).ToArray();
            input.CopyTo(storage, storageOffset);
            var borrowed = storage.AsSpan(storageOffset, input.Length);
            Assert.Equal(prefix.Length, prepared.IndexOf(borrowed));
            Assert.Equal(1, ordinary.Count(borrowed));
            Assert.Equal(1, compiled.Count(borrowed));
            Assert.Equal(1, ordinary.Count(System.Text.Encoding.UTF8.GetBytes("😀夏 " + prefix + upper)));
        }

        var candidate = System.Text.Encoding.ASCII.GetBytes(upper);
        for (var position = 0; position < length; position++)
        {
            var original = candidate[position];
            for (var value = 0; value < 256; value++)
            {
                candidate[position] = (byte)value;
                var folded = value is >= 'A' and <= 'Z' ? value + 32 : value;
                Assert.Equal(folded == needle[position] ? 0 : -1, prepared.IndexOf(candidate));
            }
            candidate[position] = original;
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
