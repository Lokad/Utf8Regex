using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8ValueSplitTests
{
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void OptionalCountKeepsUnlimitedScalarByteSegments(bool rtl)
    {
        var regex = new Utf8Regex(",", options: rtl ? RegexOptions.RightToLeft : RegexOptions.None);
        using var source = new CancellationTokenSource();
        var expected = rtl ? new[] { (9, 1), (3, 5), (0, 2) } : new[] { (0, 2), (3, 5), (9, 1) };
        var defaults = new List<(int, int)>();
        foreach (var split in regex.EnumerateSplits("é,a😀,b"u8, cancellationToken: source.Token))
            defaults.Add((split.IndexInBytes, split.LengthInBytes));
        Assert.Equal(expected, defaults);
        var zero = new List<(int, int)>();
        foreach (var split in regex.EnumerateSplits("é,a😀,b"u8, count: 0))
            zero.Add((split.IndexInBytes, split.LengthInBytes));
        Assert.Equal(expected, zero);
    }

    [Fact]
    public void SplitByteAccessPreservesScalarBoundaries()
    {
        var regex = new Utf8Regex(".", options: RegexOptions.CultureInvariant);
        var byteRanges = new List<(int, int)>();
        foreach (var split in regex.EnumerateSplits("😀"u8))
        {
            Assert.True(split.GetValueBytes().IsEmpty);
            byteRanges.Add((split.IndexInBytes, split.LengthInBytes));
        }
        Assert.Equal([(0, 0), (4, 0)], byteRanges);
        Assert.True(regex.Inspection.PreparedRegex.ScalarProgram is not null);
    }
}
