using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex.DiffTests;

public sealed class Utf8RegexMatchGroupDiffTests
{
    [Theory]
    [InlineData(@"([a-zA-Z]+)\s(\w+)", "David Bau", RegexOptions.None)]
    [InlineData(@"(\x30\x31\x32)", "012", RegexOptions.None)]
    [InlineData(@"(a)(b)?", "xab ay", RegexOptions.None)]
    [InlineData(@"(?<word>abc)(?<digit>\d)", "xxabc1yy", RegexOptions.None)]
    [InlineData(@"(?<b>a)(c)+", "xaccy", RegexOptions.CultureInvariant)]
    [InlineData(@"(?<a>x)(b)(?<c>y)", "xby", RegexOptions.CultureInvariant)]
    [InlineData(@"((?<a>x)+)", "xx", RegexOptions.CultureInvariant)]
    public void MatchDetailedMatchesRuntimeForCuratedGroupCases(string pattern, string input, RegexOptions options)
    {
        RegexParityContext.Create(pattern, input, options).AssertDetailedMatchGroupParity();
    }

    [Theory]
    [InlineData("cat|horse", "cat horse fish horse", RegexOptions.CultureInvariant)]
    [InlineData("cat|horse", "horsecat", RegexOptions.CultureInvariant)]
    [InlineData("cat|horse", "no match here", RegexOptions.CultureInvariant)]
    [InlineData("cat|horse", "", RegexOptions.CultureInvariant)]
    [InlineData("cat|horse", "cat", RegexOptions.CultureInvariant)]
    [InlineData("(cat|horse)", "cat horse fish horse", RegexOptions.CultureInvariant)]
    [InlineData("(cat|horse)", "horsecat", RegexOptions.CultureInvariant)]
    [InlineData("(cat|horse)", "no match here", RegexOptions.CultureInvariant)]
    [InlineData("(cat|horse)", "", RegexOptions.CultureInvariant)]
    [InlineData("(?<w>cat|horse)", "cat horse fish horse", RegexOptions.CultureInvariant)]
    [InlineData("(?<w>cat|horse)", "no match here", RegexOptions.CultureInvariant)]
    [InlineData("(a|ab|abc)", "abcababa", RegexOptions.CultureInvariant)]
    [InlineData("(caf\u00E9|ni\u00F1o)", "\u03B1caf\u00E9\u03B2ni\u00F1o\u03B3", RegexOptions.None)]
    [InlineData("(caf\u00E9|ni\u00F1o)", "no match here", RegexOptions.None)]
    [InlineData("(cat|horse)", "CAT horse Cat HORSE cat", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    [InlineData("(cat|horse)", "no match here", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    [InlineData("(a)", "baac", RegexOptions.CultureInvariant)]
    [InlineData("(abc)", "xxabcxx", RegexOptions.CultureInvariant)]
    [InlineData("(abc)", "no match here", RegexOptions.CultureInvariant)]
    [InlineData("(?:cat|horse)", "cat horse fish horse", RegexOptions.CultureInvariant)]
    [InlineData("(?:cat|horse)(?=horse)", "cathorsecathorse", RegexOptions.CultureInvariant)]
    [InlineData("\\b(?:cat|horse)\\b", "acat horsecatx cathorse", RegexOptions.CultureInvariant)]
    [InlineData("(k|q)", "K Q \u212A", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    [InlineData("(Sherlock Holmes|John Watson)", "John Watson esq", RegexOptions.None)]
    [InlineData("(cat)|(horse)", "cat horse fish horse", RegexOptions.CultureInvariant)]
    [InlineData("(cat)|(horse)", "no match here", RegexOptions.CultureInvariant)]
    public void MatchDetailedMatchesRuntimeForOwnedLiteralFamily(string pattern, string input, RegexOptions options)
    {
        RegexParityContext.Create(pattern, input, options).AssertDetailedMatchGroupParity();
    }

    [Theory]
    [InlineData("(cat|horse)", "cat horse fish horse", 0, RegexOptions.CultureInvariant)]
    [InlineData("(cat|horse)", "cat horse fish horse", 5, RegexOptions.CultureInvariant)]
    [InlineData("(cat|horse)", "cat horse fish horse", 20, RegexOptions.CultureInvariant)]
    [InlineData("(cat|horse)", "no match here", 4, RegexOptions.CultureInvariant)]
    [InlineData("cat|horse", "cat horse fish horse", 4, RegexOptions.CultureInvariant)]
    [InlineData("(?<w>cat|horse)", "cat horse fish horse", 9, RegexOptions.CultureInvariant)]
    [InlineData("(?:cat|horse)(?=horse)", "cathorsecathorse", 3, RegexOptions.CultureInvariant)]
    [InlineData("\\b(?:cat|horse)\\b", "acat horsecatx cathorse", 5, RegexOptions.CultureInvariant)]
    [InlineData("(caf\u00E9|ni\u00F1o)", "\u03B1caf\u00E9\u03B2ni\u00F1o\u03B3", 2, RegexOptions.None)]
    [InlineData("(cat|horse)", "CAT horse", 1, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    public void MatchDetailedFromOffsetMatchesRuntimeForOwnedLiteralFamily(string pattern, string input, int offset, RegexOptions options)
    {
        RegexParityContext.Create(pattern, input, options).AssertDetailedMatchGroupParityFromOffset(offset);
    }

    [Theory]
    [InlineData(@"a(.)c(.)e", "123abcde456aBCDe789", RegexOptions.IgnoreCase)]
    [InlineData(@"a(?<dot1>.)c(.)e", "123abcde456aBCDe789", RegexOptions.IgnoreCase)]
    [InlineData(@"(hello|hi){1,3}", "hellohihey", RegexOptions.None)]
    public void MatchDetailedMatchesRuntimeForMirroredDotNetBatches(string pattern, string input, RegexOptions options)
    {
        // Mirrored and adapted from (research checkout external/runtime @ 9ffface2f3f):
        // external/runtime/src/libraries/System.Text.RegularExpressions/tests/FunctionalTests/Regex.Match.Tests.cs
        // external/runtime/src/libraries/System.Text.RegularExpressions/tests/FunctionalTests/Regex.Groups.Tests.cs
        RegexParityContext.Create(pattern, input, options).AssertDetailedMatchGroupParity();
    }

    [Theory]
    [InlineData("(cat)|(horse)", "cat horse fish horse", RegexOptions.CultureInvariant)]
    [InlineData("(cat)|(horse)", "horsecat", RegexOptions.CultureInvariant)]
    [InlineData("(cat)|(horse)", "cathorse", RegexOptions.CultureInvariant)]
    [InlineData("(cat)|(horse)", "no match here", RegexOptions.CultureInvariant)]
    [InlineData("(cat)|(horse)", "", RegexOptions.CultureInvariant)]
    [InlineData("(cat)|(horse)", "CAT HORSE Cat", RegexOptions.CultureInvariant)]
    [InlineData("(?<w>cat)|(?<v>horse)", "cat horse fish", RegexOptions.CultureInvariant)]
    [InlineData("(?<w>cat)|(?<v>horse)", "nothing here", RegexOptions.CultureInvariant)]
    [InlineData("(a)|(ab)", "ab", RegexOptions.CultureInvariant)]
    [InlineData("(ab)|(a)", "ab", RegexOptions.CultureInvariant)]
    [InlineData("(cat)|(horse)|(dog)", "dog horse cat", RegexOptions.CultureInvariant)]
    [InlineData("(caf\u00E9)|(ni\u00F1o)", "\u03B1caf\u00E9\u03B2ni\u00F1o\u03B3", RegexOptions.None)]
    [InlineData("(caf\u00E9)|(ni\u00F1o)", "plain ascii", RegexOptions.None)]
    [InlineData("(cat)|(horse)", "CAT horse", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    [InlineData("(CAT)|(DOG)", "cat dog", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    [InlineData("(cat)|horse", "horsecat", RegexOptions.CultureInvariant)]
    [InlineData("(cat)|horse", "catcat", RegexOptions.CultureInvariant)]
    [InlineData("horse|(cat)", "horsecat", RegexOptions.CultureInvariant)]
    [InlineData("(?:(a)|(b))", "xbxa", RegexOptions.CultureInvariant)]
    [InlineData("(k)|(q)", "K Q", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    [InlineData("(cat)|(horse)", "cat horse", RegexOptions.RightToLeft | RegexOptions.CultureInvariant)]
    [InlineData("\\b(cat)|(horse)\\b", "acat horsecatx", RegexOptions.CultureInvariant)]
    [InlineData("((cat)|(horse))", "cat horse", RegexOptions.CultureInvariant)]
    [InlineData("(cat)|(horse)", "caf\u00E9 cat \U0001F600horse", RegexOptions.CultureInvariant)]
    public void MatchDetailedMatchesRuntimeForBranchedLiteralFamily(string pattern, string input, RegexOptions options)
    {
        RegexParityContext.Create(pattern, input, options).AssertDetailedMatchGroupParity();
    }

    [Theory]
    [InlineData("(cat)|(horse)", "cat horse fish horse", 0, RegexOptions.CultureInvariant)]
    [InlineData("(cat)|(horse)", "cat horse fish horse", 4, RegexOptions.CultureInvariant)]
    [InlineData("(cat)|(horse)", "cat horse fish horse", 10, RegexOptions.CultureInvariant)]
    [InlineData("(cat)|(horse)", "horsecat", 5, RegexOptions.CultureInvariant)]
    [InlineData("(?<w>cat)|(?<v>horse)", "cat horse fish", 4, RegexOptions.CultureInvariant)]
    [InlineData("(a)|(ab)", "xab", 1, RegexOptions.CultureInvariant)]
    [InlineData("(cat)|horse", "horsecat", 5, RegexOptions.CultureInvariant)]
    public void MatchDetailedFromOffsetMatchesRuntimeForBranchedLiteralFamily(string pattern, string input, int offset, RegexOptions options)
    {
        RegexParityContext.Create(pattern, input, options).AssertDetailedMatchGroupParityFromOffset(offset);
    }
}
