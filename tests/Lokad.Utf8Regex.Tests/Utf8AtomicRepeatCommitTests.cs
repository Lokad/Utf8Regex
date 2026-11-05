using System.Text;
using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8AtomicRepeatCommitTests
{
    [Theory]
    [InlineData("(?>a+)b", "xxaaaabzz")]
    [InlineData("(?>ab+)c", "xxabbbdzz")]
    [InlineData("(?>ab+)c", "xxabbbczz")]
    [InlineData("(?>(a+))b", "xxaaaabzz")]
    [InlineData("(?>(?<g>(?<g>a+)))b", "aaab")]
    [InlineData("(?:(?>(a+))b)+", "aabaaab")]
    [InlineData("(?>x(?>a+))b", "xxaaaabzz")]
    [InlineData("(?>a+)a|aa", "aaaa")]
    [InlineData("(?>a{2,3})a", "aaaa aa")]
    [InlineData("(?>a{2,3})b", "ab aaab")]
    [InlineData("(?>a*)b", "b aaab")]
    [InlineData("(?>a*)", "ab")]
    [InlineData("(?>(a{0,2}))b", "b aab")]
    [InlineData("(?>[ab]+)c", "xxababczz")]
    [InlineData(@"(?>\p{L}+)1", "😀é中1Z")]
    [InlineData("(?>(?:😀)+)X", "é😀😀X")]
    [InlineData("(?=(?>(a+))b)a+b", "xxaaaabzz")]
    [InlineData("(?<=(?>a+))b", "aaaab")]
    [InlineData("(?=(?>a+)a)a", "aaaa")]
    [InlineData("(?!x(?>a+)b).", "xaaac")]
    [InlineData("(?>a+?)b", "xxaaaabzz")]
    [InlineData("(?>a+?)a", "aaaa")]
    [InlineData("(?>(a)+)b", "aaab")]
    [InlineData("(?>a+|ab)c", "abc aac")]
    [InlineData("(?i:(?>a+))b", "xxAAAAbzz")]
    [InlineData("(?<x>a)(?>(?<-x>a+))b(?(x)c|d)", "aaabd")]
    public void PublicResultsAndHistoriesRetainAtomicSemantics(string pattern, string text)
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
            Assert.Equal(expected.Success, regex.IsMatch(input));
            Assert.Equal(oracle.Count(text), regex.Count(input));
            Utf8SliceSparseGroupTests.AssertDetailed(oracle, expected, retained);
        }
    }
}
