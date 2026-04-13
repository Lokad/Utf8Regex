using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex.DiffTests;

public sealed class Utf8RegexMatchGroupDiffTests
{
    [Theory]
    [InlineData(@"([a-zA-Z]+)\s(\w+)", "David Bau", RegexOptions.None)]
    [InlineData(@"(\x30\x31\x32)", "012", RegexOptions.None)]
    [InlineData(@"(a)(b)?", "xab ay", RegexOptions.None)]
    [InlineData(@"(?<word>abc)(?<digit>\d)", "xxabc1yy", RegexOptions.None)]
    public void MatchDetailedMatchesRuntimeForCuratedGroupCases(string pattern, string input, RegexOptions options)
    {
        RegexParityContext.Create(pattern, input, options).AssertDetailedMatchGroupParity();
    }

    [Theory]
    [InlineData(@"a(.)c(.)e", "123abcde456aBCDe789", RegexOptions.IgnoreCase)]
    [InlineData(@"a(?<dot1>.)c(.)e", "123abcde456aBCDe789", RegexOptions.IgnoreCase)]
    [InlineData(@"(hello|hi){1,3}", "hellohihey", RegexOptions.None)]
    public void MatchDetailedMatchesRuntimeForMirroredDotNetBatches(string pattern, string input, RegexOptions options)
    {
        // Mirrored and adapted from:
        // external/dotnet-runtime/src/libraries/System.Text.RegularExpressions/tests/FunctionalTests/Regex.Match.Tests.cs
        // external/dotnet-runtime/src/libraries/System.Text.RegularExpressions/tests/FunctionalTests/Regex.Groups.Tests.cs
        RegexParityContext.Create(pattern, input, options).AssertDetailedMatchGroupParity();
    }
}
