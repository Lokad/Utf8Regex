using System.Text;
using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8LazyDisjointRepeatTests
{
    [Theory]
    [InlineData("a+?b", "xxaaaabzz")]
    [InlineData("(a+?)b", "xxaaaabzz")]
    [InlineData("((a+?)b){2}", "aabaaaab")]
    [InlineData("[ab]+?c", "xxababczz")]
    [InlineData("[^x]*?x", "é😀xZ")]
    [InlineData("(?:😀)+?X", "é😀😀X")]
    [InlineData(@"\d+?X", "é١٢X")]
    [InlineData(@"\p{L}+?1", "😀é中1Z")]
    [InlineData("a{2,3}?b", "xaaaab aab")]
    [InlineData("a{2}?b", "xaaabzz")]
    [InlineData("(?=(a+?b))a+?b", "xxaaaabzz")]
    [InlineData("(?i:a+?)b", "xxAAAAbzz")]
    [InlineData("a+?a", "aaaa")]
    [InlineData("a+?", "aaaa")]
    [InlineData("[^x]*?", "ab")]
    [InlineData("(?=(a+?))a", "aaa")]
    [InlineData("a+?$", "aaaa")]
    public void PublicRangesAndCaptureHistoriesKeepLazySemantics(string pattern, string text)
    {
        var input = Encoding.UTF8.GetBytes(text);
        foreach (var mode in new[] { RegexOptions.None, RegexOptions.Compiled, RegexOptions.RightToLeft })
        {
            var options = mode | RegexOptions.CultureInvariant;
            var oracle = new Regex(pattern, options);
            var regex = new Utf8Regex(pattern, options, Regex.InfiniteMatchTimeout,
                new() { MaxWorkSteps = 1_000_000, MaxDepth = 10000,
                    MaxWorkingBytes = 1 << 24, MaxResultBytes = 1 << 24 });
            var expected = oracle.Match(text);
            var retained = regex.MatchDetailed(input);
            Utf8SliceSparseGroupTests.AssertDetailed(oracle, expected, retained);
            if (expected.Success)
            {
                Assert.Equal(Encoding.UTF8.GetByteCount(text.AsSpan(0, expected.Index)), retained.IndexInBytes);
                Assert.Equal(Encoding.UTF8.GetByteCount(expected.Value), retained.LengthInBytes);
            }
            Assert.Equal(oracle.Count(text), regex.Count(input));
            Utf8SliceSparseGroupTests.AssertDetailed(oracle, expected, retained);
        }
    }
}
