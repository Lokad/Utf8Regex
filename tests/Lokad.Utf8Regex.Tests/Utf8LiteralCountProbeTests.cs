using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Benchmarks;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8LiteralCountProbeTests
{
    [Theory]
    [InlineData("汉字")]
    [InlineData("é")]
    [InlineData("éa")]
    [InlineData("😀")]
    [InlineData("ab")]
    [InlineData("abé")]
    public void AvailableProbesAgreeWithPublicCountOnScalarInputsAndBorrowedViews(string literal)
    {
        foreach (var options in new[] { RegexOptions.CultureInvariant, RegexOptions.CultureInvariant | RegexOptions.Compiled,
            RegexOptions.CultureInvariant | RegexOptions.RightToLeft })
        {
            var regex = new Utf8Regex(Regex.Escape(literal), options);
            foreach (var tail in new[] { "", literal[..^1] })
            {
                // Keep every input a complete scalar, including a supplementary tail.
                var suffix = tail.Length > 0 && char.IsHighSurrogate(tail[^1]) ? "" : tail;
                var bytes = Encoding.UTF8.GetBytes("x" + literal + "!" + literal + "!" + suffix);
                foreach (var offset in new[] { 0, 1, 31 })
                {
                    var storage = new byte[offset + bytes.Length + 32];
                    bytes.CopyTo(storage.AsSpan(offset));
                    var input = storage.AsSpan(offset, bytes.Length);
                    var expected = regex.Count(input);
                    Assert.Equal(2, expected);
                    if (regex.Inspection.DebugTryCountExactUtf8LiteralValidatedThreeByte(input, out var threeByte))
                        Assert.Equal(expected, threeByte);
                    if (regex.Inspection.DebugTryCountExactUtf8LiteralLeadingScalarAnchored(input, out var leading))
                        Assert.Equal(expected, leading);
                    if (regex.Inspection.DebugTryCountExactUtf8LiteralPreparedSearch(input, out var prepared))
                        Assert.Equal(expected, prepared);
                    if (regex.Inspection.DebugTryCountExactUtf8LiteralAnchored(input, out var anchored))
                        Assert.Equal(expected, anchored);
                }
            }
        }
    }

    [Fact]
    public void ProbesExerciseTheirProductionKernelsWhenEligible()
    {
        var threeByte = new Utf8Regex("汉字");
        Assert.True(threeByte.Inspection.DebugTryCountExactUtf8LiteralValidatedThreeByte("汉字!汉字"u8, out var validated));
        Assert.True(threeByte.Inspection.DebugTryCountExactUtf8LiteralLeadingScalarAnchored("汉字!汉字"u8, out var leading));
        Assert.True(threeByte.Inspection.DebugTryCountExactUtf8LiteralPreparedSearch("汉字!汉字"u8, out var prepared));
        Assert.Equal(2, validated);
        Assert.Equal(2, leading);
        Assert.Equal(2, prepared);
        var anchor = new Utf8Regex("abé");
        Assert.True(anchor.Inspection.DebugTryCountExactUtf8LiteralAnchored("abé!abé"u8, out var anchored));
        Assert.Equal(2, anchored);
    }

    [Fact]
    public void ProbesDeclineNonliteralRuntimeWithoutRunningAnAlternativeEngine()
    {
        var regex = new Utf8Regex("a+");
        Assert.False(regex.Inspection.DebugTryCountExactUtf8LiteralValidatedThreeByte("aaa"u8, out var threeByte));
        Assert.False(regex.Inspection.DebugTryCountExactUtf8LiteralLeadingScalarAnchored("aaa"u8, out var leading));
        Assert.False(regex.Inspection.DebugTryCountExactUtf8LiteralPreparedSearch("aaa"u8, out var prepared));
        Assert.False(regex.Inspection.DebugTryCountExactUtf8LiteralAnchored("aaa"u8, out var anchored));
        Assert.Equal(0, threeByte);
        Assert.Equal(0, leading);
        Assert.Equal(0, prepared);
        Assert.Equal(0, anchored);
    }
}
