using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.FrontEnd;

namespace Lokad.Utf8Regex.Tests;

/// <summary>
/// R02 extraction 5: length arithmetic saturates in the owned hub (long products and
/// sums capped at int.MaxValue for byte minimums, null maximums on overflow) instead of
/// wrapping like the pristine fork. Values are read off the owned analysis; behavior on
/// non-saturating shapes is pinned alongside so the formulas cannot drift silently.
/// </summary>
public sealed class Utf8LengthArithmeticTests
{
    private static Utf8OwnedFindOptimizations.ByteWidth Measure(string pattern)
    {
        var tree = Utf8OwnedTreeParser.TryParse(pattern, RegexOptions.CultureInvariant);
        Assert.NotNull(tree);
        return Utf8OwnedFindOptimizations.MeasureByteFacts(tree.Root, selectFixedLiteral: false).Width;
    }

    [Fact]
    public void SmallShapesStayExact()
    {
        Assert.Equal(new Utf8OwnedFindOptimizations.ByteWidth(3, 3), Measure("abc"));
        Assert.Equal(new Utf8OwnedFindOptimizations.ByteWidth(9, 9), Measure("(?:abc){3}"));
        Assert.Equal(new Utf8OwnedFindOptimizations.ByteWidth(10, 10), Measure("(?:ab){3}(?:cd){2}"));
        Assert.Equal(new Utf8OwnedFindOptimizations.ByteWidth(2, 6), Measure("(?:ab|abcdef)"));
        Assert.Equal(new Utf8OwnedFindOptimizations.ByteWidth(0, null), Measure("(?:ab)*"));
    }

    [Fact]
    public void HugeLoopProductSaturatesMinimumAndNullsMaximum()
    {
        Assert.Equal(new Utf8OwnedFindOptimizations.ByteWidth(int.MaxValue, null), Measure("(?:abc){2000000000}"));
    }

    [Fact]
    public void HugeConcatenationSumSaturatesMinimumAndNullsMaximum()
    {
        Assert.Equal(new Utf8OwnedFindOptimizations.ByteWidth(int.MaxValue, null),
            Measure("(?:a){1500000000}(?:b){1500000000}"));
    }

    [Fact]
    public void MidSizeRepeatMatchesEndToEnd()
    {
        var regex = new Utf8Regex("(?:ab){1000}", options: RegexOptions.CultureInvariant);
        Assert.True(regex.IsMatch(Encoding.UTF8.GetBytes(new string('a', 1000).Replace("a", "ab"))));
        Assert.False(regex.IsMatch(Encoding.UTF8.GetBytes("ab")));
    }
}
