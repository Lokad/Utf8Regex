using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace Lokad.Utf8Regex.Tests;

/// <summary>R02a output accounting: owned fixed-template replacement matches
/// the BCL oracle on literals, group and special substitutions, unmatched
/// groups, empty advancement, and astral input, with ledger-charged growth and
/// no BCL fallback construction on owned paths.</summary>
public sealed class Utf8SliceReplaceTests
{
    private static void AssertReplaceParity(string pattern, string inputText, string replacement)
    {
        var input = Encoding.UTF8.GetBytes(inputText);
        var oracle = new Regex(pattern, RegexOptions.CultureInvariant, Regex.InfiniteMatchTimeout).Replace(inputText, replacement);
        foreach (var options in new[] { RegexOptions.CultureInvariant, RegexOptions.CultureInvariant | RegexOptions.Compiled })
        {
            var regex = new Utf8Regex(pattern, options);
            Assert.True(regex.Inspection.DebugUsesOwnedSliceProgram, options.ToString());
            Assert.Equal(oracle, Encoding.UTF8.GetString(regex.Replace(input, replacement)));
            Assert.Equal(oracle, Encoding.UTF8.GetString(regex.Replace(input, Encoding.UTF8.GetBytes(replacement))));
            using var source = new CancellationTokenSource();
            Assert.Equal(oracle, Encoding.UTF8.GetString(regex.Replace(input, replacement, source.Token)));
        }
    }

    [Theory]
    [InlineData("(a+)(b+)", "xxaaabbyy", "Z")]
    [InlineData("(a+)(b+)", "xxaaabbyy", "[$1|$2]")]
    [InlineData("(a+)(b+)", "xxaaabbyy", "$&!$&")]
    [InlineData("(a+)(b+)", "xxaaabbyy", "$$")]
    [InlineData("(a+)(b+)", "xxaaabbyy", "<$`>")]
    [InlineData("(a+)(b+)", "xxaaabbyy", "<$'>")]
    [InlineData("(a+)(b+)", "xxaaabbyy", "<$+>")]
    [InlineData("(a+)(b+)", "xxaaabbyy", "<$3>")]
    [InlineData("(a+)|(b+)", "xxaaayy", "<$1|$2>")]
    [InlineData("(ab|cd)+", "xxabcdcdyy", "[$1]")]
    [InlineData("a*?b", "xxaaabyy", "Z")]
    [InlineData("a*", "bb", "[Z]")]
    [InlineData("(a+)(b+)", "éaaabbé", "[$1|$2]")]
    [InlineData("(a+)(b+)", "xxaaabbyy zz", "Z $&")]
    public void OwnedReplaceMatchesOracle(string pattern, string inputText, string replacement)
    {
        AssertReplaceParity(pattern, inputText, replacement);
    }

    [Theory]
    [InlineData("(a.*b)", "xxaa1bbyy", "<$1>")]
    [InlineData("a(.*)b", "xa1bxa22by", "<$1>")]
    [InlineData("(a.c)+", "xxabcayc", "[$1]")]
    [InlineData(".*", "ab", "<$&>")]
    public void OwnedDotReplaceMatchesOracle(string pattern, string inputText, string replacement)
    {
        AssertReplaceParity(pattern, inputText, replacement);
    }

    [Fact]
    public void OwnedReplaceNeverConstructsBclFallback()
    {
        var regex = new Utf8Regex("(a+)(b+)", RegexOptions.CultureInvariant);
        var result = regex.Replace(Encoding.UTF8.GetBytes("xxaaabbyy"), "[$1]");
        Assert.Equal("xx[aaa]yy", Encoding.UTF8.GetString(result));
        Assert.False(regex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(regex.Inspection.DebugAnchoredFallbackRegexIsCreated);
    }

    [Fact]
    public void PreCanceledTokenAbortsOwnedReplace()
    {
        var regex = new Utf8Regex("(a+)(b+)", RegexOptions.CultureInvariant);
        using var canceled = new CancellationTokenSource();
        canceled.Cancel();
        Assert.Throws<OperationCanceledException>(
            () => regex.Replace(Encoding.UTF8.GetBytes("xxaaabbyy"), "Z", canceled.Token));
    }
}
