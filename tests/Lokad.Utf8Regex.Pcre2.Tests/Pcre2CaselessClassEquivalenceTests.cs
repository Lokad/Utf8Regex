using System.Text;

namespace Lokad.Utf8Regex.Pcre2.Tests;

public sealed class Pcre2CaselessClassEquivalenceTests
{
    // Native PCRE2 10.47 controls: simple-fold equivalence is symmetric,
    // includes symbol partners, and does not expand one scalar into text.
    [Theory]
    [InlineData(@"(?i)[K]", "K", true)]
    [InlineData(@"(?i)[K]", "k", true)]
    [InlineData(@"(?i)[ſ]", "S", true)]
    [InlineData(@"(?i)[ſ]", "s", true)]
    [InlineData(@"(?i)[µ]", "Μ", true)]
    [InlineData(@"(?i)[µ]", "μ", true)]
    [InlineData(@"(?i)[Ω]", "ω", true)]
    [InlineData(@"(?i)[K-Å]", "å", true)]
    [InlineData(@"(?i)[K-Å]", "L", false)]
    [InlineData(@"(?i)[^K]", "k", false)]
    [InlineData(@"(?i)[^ſ]", "s", false)]
    [InlineData(@"(?i)[^K]", "L", true)]
    [InlineData(@"(?i)[\x{345}]", "ι", true)]
    [InlineData(@"(?i)[\x{1FBE}]", "Ι", true)]
    [InlineData(@"(?i)[\x{3D0}]", "β", true)]
    [InlineData(@"(?i)[\x{3F4}]", "ϑ", true)]
    [InlineData(@"(?i)[\x{1C80}]", "В", true)]
    [InlineData(@"(?i)[\x{1C85}]", "т", true)]
    [InlineData(@"(?i)[\x{390}]", "\u1FD3", true)]
    [InlineData(@"(?i)[\x{1FE3}]", "\u03B0", true)]
    [InlineData(@"(?i)[\x{FB05}]", "\uFB06", true)]
    [InlineData(@"(?i)[i]", "I", true)]
    [InlineData(@"(?i)[i]", "ı", false)]
    [InlineData(@"(?i)[i]", "İ", false)]
    [InlineData(@"(?i)[ß]", "ẞ", true)]
    [InlineData(@"(?i)[ß]", "s", false)]
    [InlineData(@"(?i)[\x{10400}]", "𐐨", true)]
    [InlineData(@"[K]", "k", false)]
    [InlineData(@"(?J)(?i)i", "ı", false)]
    [InlineData(@"(?J)(?i)(ſ)\1", "ſS", true)]
    [InlineData(@"(?J)(?i)([i])\1", "iı", false)]
    public void ClassesLiteralsAndReferencesUseCompleteSimpleFoldPartners(
        string pattern, string text, bool expected)
    {
        var bytes = Encoding.UTF8.GetBytes("padding" + text + "padding");
        var input = bytes.AsSpan(7, Encoding.UTF8.GetByteCount(text));
        using var token = new CancellationTokenSource();
        foreach (var limits in new[] { default(Utf8Pcre2ExecutionLimits), new Utf8Pcre2ExecutionLimits
        {
            MatchLimit = 1000000, DepthLimit = 256,
            HeapLimitInBytes = 1 << 20, ResultLimitInBytes = 1 << 20
        } })
        {
            var regex = new Utf8Pcre2Regex(pattern, options: Pcre2CompileOptions.Ucp, compileSettings: default, policy: new Utf8RegexPolicy
            {
                ResourcePolicy = new Utf8ResourcePolicy
                {
                    MaxWorkSteps = ((Utf8Pcre2ExecutionLimits)(limits)).MatchLimit == 0 ? null : ((Utf8Pcre2ExecutionLimits)(limits)).MatchLimit,
                    MaxDepth = ((Utf8Pcre2ExecutionLimits)(limits)).DepthLimit == 0 ? null : ((Utf8Pcre2ExecutionLimits)(limits)).DepthLimit,
                    MaxWorkingBytes = ((Utf8Pcre2ExecutionLimits)(limits)).HeapLimitInBytes == 0 ? null : ((Utf8Pcre2ExecutionLimits)(limits)).HeapLimitInBytes,
                    MaxResultBytes = ((Utf8Pcre2ExecutionLimits)(limits)).ResultLimitInBytes == 0 ? null : ((Utf8Pcre2ExecutionLimits)(limits)).ResultLimitInBytes
                },
                MatchTimeout = Timeout.InfiniteTimeSpan
            });
            Assert.Equal(expected, regex.IsMatch(input, 0, Pcre2MatchOptions.None, token.Token));
            Assert.Equal(expected ? 1 : 0, regex.Count(input, 0, Pcre2MatchOptions.None, token.Token));
            var match = regex.MatchDetailed(input, 0, Pcre2MatchOptions.None, token.Token);
            Assert.Equal(expected, match.Success);
            if (expected)
            {
                Assert.Equal(text, System.Text.Encoding.UTF8.GetString(match.Value.GetValueBytes()));
                Assert.Equal((0, input.Length), (match.Value.StartOffsetInBytes, match.Value.EndOffsetInBytes));
            }
        }
    }

    [Fact]
    public void ClassFoldingDoesNotNormalizeOrExpandSubjectScalars()
    {
        Assert.False(new Utf8Pcre2Regex(@"(?i)^[ß]$").IsMatch("ss"u8));
        Assert.False(new Utf8Pcre2Regex(@"(?i)^[é]$").IsMatch("e\u0301"u8));
        var regex = new Utf8Pcre2Regex(@"(?i)(?<x>[Kſ]+)");
        var match = regex.MatchDetailed("😀kS"u8);
        Assert.Equal("kS", System.Text.Encoding.UTF8.GetString(match.GetGroup(1).GetValueBytes()));
        Assert.Equal((4, 6), (match.GetGroup(1).StartOffsetInBytes, match.GetGroup(1).EndOffsetInBytes));
    }
}
