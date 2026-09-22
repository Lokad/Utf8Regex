using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.FrontEnd;
using RuntimeFrontEnd = Lokad.Utf8Regex.Internal.FrontEnd.Runtime;

namespace Lokad.Utf8Regex.Tests;

/// <summary>
/// R02 extraction 5: length arithmetic saturates exactly like the pinned BCL formulas
/// (long products and sums capped at int.MaxValue - 1 for minimums, null maximums on
/// overflow) instead of wrapping. Values are read off the owned analysis; behavior on
/// non-saturating shapes is pinned alongside so the formulas cannot drift silently.
/// </summary>
public sealed class Utf8LengthArithmeticTests
{
    private static RuntimeFrontEnd.RegexFindOptimizations Analyze(string pattern)
    {
        var tree = Utf8RuntimeTreeProvider.TryParse(pattern, RegexOptions.CultureInvariant);
        Assert.NotNull(tree);
        return new RuntimeFrontEnd.RegexFindOptimizations(tree.Root, RegexOptions.CultureInvariant);
    }

    [Fact]
    public void SmallShapesStayExact()
    {
        Assert.Equal(3, Analyze("abc").MinRequiredLength);
        Assert.Equal(3, Analyze("abc").MaxPossibleLength);
        Assert.Equal(9, Analyze("(?:abc){3}").MinRequiredLength);
        Assert.Equal(9, Analyze("(?:abc){3}").MaxPossibleLength);
        Assert.Equal(10, Analyze("(?:ab){3}(?:cd){2}").MinRequiredLength);
        Assert.Equal(10, Analyze("(?:ab){3}(?:cd){2}").MaxPossibleLength);
        Assert.Equal(2, Analyze("(?:ab|abcdef)").MinRequiredLength);
        Assert.Equal(6, Analyze("(?:ab|abcdef)").MaxPossibleLength);
        Assert.Equal(0, Analyze("(?:ab)*").MinRequiredLength);
        Assert.Null(Analyze("(?:ab)*").MaxPossibleLength);
    }

    [Fact]
    public void HugeLoopProductSaturatesMinimumAndNullsMaximum()
    {
        var analysis = Analyze("(?:abc){2000000000}");
        Assert.Equal(int.MaxValue - 1, analysis.MinRequiredLength);
        Assert.Null(analysis.MaxPossibleLength);
    }

    [Fact]
    public void HugeConcatenationSumSaturatesMinimumAndNullsMaximum()
    {
        var analysis = Analyze("(?:a){1500000000}(?:b){1500000000}");
        Assert.Equal(int.MaxValue - 1, analysis.MinRequiredLength);
        Assert.Null(analysis.MaxPossibleLength);
    }

    [Fact]
    public void MidSizeRepeatMatchesEndToEnd()
    {
        var regex = new Utf8Regex("(?:ab){1000}", RegexOptions.CultureInvariant);
        Assert.True(regex.IsMatch(Encoding.UTF8.GetBytes(new string('a', 1000).Replace("a", "ab"))));
        Assert.False(regex.IsMatch(Encoding.UTF8.GetBytes("ab")));
    }
}
