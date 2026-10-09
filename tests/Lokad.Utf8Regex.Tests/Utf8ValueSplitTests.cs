using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8ValueSplitTests
{
    [Fact]
    public void SplitByteAccessPreservesScalarBoundaries()
    {
        var regex = new Utf8Regex(".", RegexOptions.CultureInvariant);
        var byteRanges = new List<(int, int)>();
        var unitRanges = new List<(int, int)>();
        foreach (var split in regex.EnumerateSplits("😀"u8))
        {
            Assert.True(split.IsByteAligned);
            Assert.Equal("", split.GetValueString());
            Assert.True(split.GetValueBytes().IsEmpty);
            byteRanges.Add((split.IndexInBytes, split.LengthInBytes));
            unitRanges.Add((split.IndexInUtf16, split.LengthInUtf16));
        }
        Assert.Equal([(0, 0), (4, 0)], byteRanges);
        Assert.Equal([(0, 0), (2, 0)], unitRanges);
        Assert.False(regex.Inspection.DebugFallbackRegexIsCreated);
    }
}
