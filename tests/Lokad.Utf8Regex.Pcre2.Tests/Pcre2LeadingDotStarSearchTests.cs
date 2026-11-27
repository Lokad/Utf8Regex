using System.Text;
using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex.Pcre2.Tests;

public sealed class Pcre2LeadingDotStarSearchTests
{
    [Fact]
    public void LeadingDotStarPreservesCapturedGreedySearch()
    {
        var regex = new Utf8Pcre2Regex(@".*(ss)");

        Assert.True(regex.IsMatch("Essential services"u8));
        Assert.False(regex.IsMatch("A regular service"u8));
        ReadOnlySpan<byte> detailed1Input = "glass"u8;
        var detailed1Captures = new Utf8Pcre2CaptureRange[regex.CaptureGroupCount + 1];

        var detailed = regex.Match(detailed1Input, captures: detailed1Captures);
        Assert.True(detailed.Success);
        Assert.Equal("glass", System.Text.Encoding.UTF8.GetString(detailed.GetValueBytes()));
        Assert.Equal("ss", System.Text.Encoding.UTF8.GetString((detailed1Captures[1].Success ? detailed1Input.Slice(detailed1Captures[1].StartOffsetInBytes, detailed1Captures[1].EndOffsetInBytes - detailed1Captures[1].StartOffsetInBytes) : ReadOnlySpan<byte>.Empty)));
    }

    [Theory]
    [InlineData(@".*(ss)")]
    [InlineData(@".*ss")]
    [InlineData(@".*(?:ss)")]
    [InlineData(@".*((ab))")]
    [InlineData(@".*?tail")]
    [InlineData(".*é🙂")]
    [InlineData(".*\\n")]
    public void LeadingDotStarMatchesOverlappingBclGrammarAtValidStarts(string pattern)
    {
        var direct = new Utf8Pcre2Regex(pattern);
        var vm = CreateMetered(pattern);
        // An unconstrained dot repeat plus exact suffix has the same Boolean
        // existence over these scalar-aligned subjects in the BCL grammar.
        var oracle = new Regex(pattern, RegexOptions.CultureInvariant);

        foreach (var inputText in new[]
                 {
                     "",
                     "s",
                     "ss",
                     "glass",
                     "Essential services are provided",
                     "x\nss",
                     "s\ns",
                     "é🙂",
                     "xé🙂y",
                     "ab x tail",
                     "\n",
                     "x\ny",
                 })
        {
            var input = Encoding.UTF8.GetBytes(inputText);
            Assert.Equal(oracle.IsMatch(inputText), direct.IsMatch(input));
            Assert.Equal(oracle.IsMatch(inputText), vm.IsMatch(input));

            for (var start = 0; start <= input.Length; start++)
            {
                if (start < input.Length && (input[start] & 0xC0) == 0x80)
                {
                    continue;
                }

                var characterStart = Encoding.UTF8.GetCharCount(input.AsSpan(0, start));
                Assert.Equal(oracle.IsMatch(inputText, characterStart), direct.IsMatch(input, start));
                Assert.Equal(oracle.IsMatch(inputText, characterStart), vm.IsMatch(input, start));
            }
        }
    }

    private static Utf8Pcre2Regex CreateMetered(string pattern) =>
        new(pattern, options: Pcre2CompileOptions.None, compileSettings: default, policy: new Utf8RegexPolicy
        {
            ResourcePolicy = new Utf8ResourcePolicy
            {
                MaxDepth = 4096
            },
            MatchTimeout = Regex.InfiniteMatchTimeout
        });
}
