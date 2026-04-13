using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8ValueSplitTests
{
    [Fact]
    public void SplitByteAccessThrowsForNonAlignedBoundary()
    {
        var regex = new Utf8Regex(".", RegexOptions.CultureInvariant);
        var aligned = new List<bool>();
        var values = new List<string>();

        foreach (var split in regex.EnumerateSplits("😀"u8))
        {
            aligned.Add(split.IsByteAligned);
            values.Add(split.GetValueString());

            if (!split.IsByteAligned)
            {
                try
                {
                    _ = split.IndexInBytes;
                    throw new Xunit.Sdk.XunitException("Expected IndexInBytes to throw.");
                }
                catch (InvalidOperationException)
                {
                }

                try
                {
                    _ = split.LengthInBytes;
                    throw new Xunit.Sdk.XunitException("Expected LengthInBytes to throw.");
                }
                catch (InvalidOperationException)
                {
                }

                try
                {
                    _ = split.GetValueBytes();
                    throw new Xunit.Sdk.XunitException("Expected GetValueBytes to throw.");
                }
                catch (InvalidOperationException)
                {
                }
            }
        }

        Assert.Equal(3, aligned.Count);
        Assert.Equal(["", "", ""], values);
        Assert.True(aligned[0]);
        Assert.False(aligned[1]);
        Assert.True(aligned[2]);
    }
}
