using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.FrontEnd;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8BalancedBeginEndCountTests
{
    private const string Pattern =
        "BEGIN(?:(?<open>BEGIN)|(?<-open>END)|(?:(?!BEGIN|END)[\\s\\S]))*END(?(open)(?!))";

    [Theory]
    [InlineData(RegexOptions.CultureInvariant)]
    [InlineData(RegexOptions.CultureInvariant | RegexOptions.Compiled)]
    public void CountMatchesDotNetAcrossExhaustiveTokenSequences(RegexOptions options)
    {
        var oracle = new Regex(Pattern, options, Regex.InfiniteMatchTimeout);
        var regex = new Utf8Regex(Pattern, options);

        for (var tokenCount = 0; tokenCount <= 10; tokenCount++)
        {
            var combinationCount = 1 << tokenCount;
            for (var combination = 0; combination < combinationCount; combination++)
            {
                var input = BuildTokenSequence(tokenCount, combination);
                Assert.Equal(oracle.Count(input), regex.Count(Encoding.UTF8.GetBytes(input)));
            }
        }
    }

    [Theory]
    [InlineData(RegexOptions.None)]
    [InlineData(RegexOptions.Compiled)]
    public void CountMatchesDotNetAcrossTextAndUnmatchedTokens(RegexOptions options)
    {
        string[] inputs =
        [
            "",
            "BEGINEND",
            "BEGINBEGINENDEND",
            "BEGINBEGINEND",
            "BEGINENDEND",
            "ENDBEGINEND",
            "BEGINENDBEGINEND",
            "BEGINBEGINENDBEGINEND",
            "BBEGIN x END",
            "BEGEND BEGIN payload END EEND",
            "é BEGIN раздел BEGIN данные END хвост END 終",
            "BEGIN outer BEGIN complete END BEGIN incomplete",
        ];
        var oracle = new Regex(Pattern, options, Regex.InfiniteMatchTimeout);
        var regex = new Utf8Regex(Pattern, options);

        foreach (var input in inputs)
        {
            Assert.Equal(oracle.Count(input), regex.Count(Encoding.UTF8.GetBytes(input)));
        }
    }

    [Fact]
    public void CountFallsBackBeyondBoundedNativeNesting()
    {
        var input = string.Concat(Enumerable.Repeat("BEGIN", 129)) +
            string.Concat(Enumerable.Repeat("END", 129));
        var bytes = Encoding.UTF8.GetBytes(input);
        var regex = new Utf8Regex(Pattern, RegexOptions.CultureInvariant);

        Assert.False(Utf8BalancedBeginEndExecutor.TryCount(bytes, out _));
        Assert.Equal(Regex.Count(input, Pattern), regex.Count(bytes));
    }

    [Theory]
    [InlineData(RegexOptions.CultureInvariant)]
    [InlineData(RegexOptions.CultureInvariant | RegexOptions.Compiled)]
    public void CountRejectsMalformedUtf8(RegexOptions options)
    {
        var regex = new Utf8Regex(Pattern, options);
        byte[] malformed = [.. "BEGIN payload END"u8, 0xFF];

        Assert.Throws<ArgumentException>(() => regex.Count(malformed));
    }

    [Fact]
    public void AnalyzerAdmitsOnlyCaseSensitiveCompatibleOptions()
    {
        Assert.True(Utf8FallbackRegexFamilyAnalyzer.IsUtf8BalancedBeginEndCount(Pattern, RegexOptions.None));
        Assert.True(Utf8FallbackRegexFamilyAnalyzer.IsUtf8BalancedBeginEndCount(
            Pattern,
            RegexOptions.CultureInvariant | RegexOptions.Compiled));
        Assert.False(Utf8FallbackRegexFamilyAnalyzer.IsUtf8BalancedBeginEndCount(Pattern, RegexOptions.IgnoreCase));
        Assert.False(Utf8FallbackRegexFamilyAnalyzer.IsUtf8BalancedBeginEndCount(Pattern + "x", RegexOptions.None));
    }

    private static string BuildTokenSequence(int tokenCount, int combination)
    {
        var builder = new StringBuilder(tokenCount * 7);
        for (var tokenIndex = 0; tokenIndex < tokenCount; tokenIndex++)
        {
            builder.Append((combination & (1 << tokenIndex)) == 0 ? "BEGIN" : "END");
            builder.Append('x');
        }

        return builder.ToString();
    }
}
