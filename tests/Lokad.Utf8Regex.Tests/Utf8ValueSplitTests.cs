using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8ValueSplitTests
{
    [Fact]
    public void SplitByteAccessPreservesScalarBoundaries()
    {
        var regex = new Utf8Regex(".", RegexOptions.CultureInvariant);
        var byteRanges = new List<(int, int)>();
        foreach (var split in regex.EnumerateSplits("😀"u8))
        {
            Assert.Equal(split.GetValueBytes().ToArray(), global::System.Text.Encoding.UTF8.GetBytes(split.GetValueString()));
            Assert.Equal("", split.GetValueString());
            Assert.True(split.GetValueBytes().IsEmpty);
            byteRanges.Add((split.IndexInBytes, split.LengthInBytes));
        }
        Assert.Equal([(0, 0), (4, 0)], byteRanges);
        Assert.True(regex.Inspection.PreparedRegex.ScalarProgram is not null);
    }
}
