using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Benchmarks;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8LiteralCountProbeTests
{
    [Theory]
    [InlineData("夏洛克·福尔摩斯")]
    [InlineData("夏·é")]
    [InlineData("夏洛😀")]
    [InlineData("夏😀")]
    [InlineData("夏A")]
    public void PublicCountPreservesDensePrefixTailsNonoverlapAndBorrowedBounds(string literal)
    {
        var scalars = literal.EnumerateRunes().ToArray();
        var prefix = string.Concat(scalars.Take(Math.Min(2, scalars.Length - 1)).Select(scalar => scalar.ToString()));
        var text = string.Concat(Enumerable.Repeat(prefix + "?", 256)) + literal + literal + prefix;
        var bytes = Encoding.UTF8.GetBytes(text);
        foreach (var options in new[] { RegexOptions.None, RegexOptions.Compiled, RegexOptions.RightToLeft, RegexOptions.NonBacktracking })
        foreach (var controlled in new[] { false, true })
        {
            using var token = new CancellationTokenSource();
            var regex = new Utf8Regex(literal, options, Timeout.InfiniteTimeSpan,
                controlled ? new() { MaxWorkSteps = 1_000_000, MaxDepth = 1024, MaxWorkingBytes = 1 << 20, MaxResultBytes = 1 << 20 } : default);
            foreach (var offset in new[] { 0, 1, 31, 32 })
            {
                byte[] storage = [.. Enumerable.Repeat((byte)0xff, offset), .. bytes, 0xff];
                Assert.Equal(2, regex.Count(storage.AsSpan(offset, bytes.Length), controlled ? token.Token : default));
            }
            byte[] invalid = [.. bytes, 0xff];
            Assert.Throws<ArgumentException>(() => regex.Count(invalid, controlled ? token.Token : default));
        }
        var repeated = new Utf8Regex("夏夏A");
        Assert.Equal(1, repeated.Count("夏夏夏A"u8));
        Assert.Equal(2, repeated.Count("夏夏A夏夏A夏夏"u8));
    }

    [Theory]
    [InlineData("夏洛克·福尔摩斯")]
    [InlineData("夏·é")]
    [InlineData("夏洛😀")]
    [InlineData("夏😀")]
    public void PublicCountIgnoresPartialLeadingScalarAnchorsAtTheSubjectTail(string literal)
    {
        foreach (var options in new[] { RegexOptions.None, RegexOptions.Compiled,
            RegexOptions.NonBacktracking, RegexOptions.RightToLeft })
        foreach (var controlled in new[] { false, true })
        {
            using var cancellation = new CancellationTokenSource();
            var policy = controlled ? new Utf8ResourcePolicy { MaxWorkSteps = 100_000,
                MaxDepth = 256, MaxWorkingBytes = 1UL << 20, MaxResultBytes = 1UL << 20 } : default;
            var regex = new Utf8Regex(literal, options, Timeout.InfiniteTimeSpan, policy);
            var prefix = "";
            foreach (var scalar in literal.EnumerateRunes().SkipLast(1))
            {
                prefix += scalar.ToString();
                foreach (var offset in new[] { 0, 1, 31, 32 })
                {
                    var bytes = Encoding.UTF8.GetBytes(prefix + new string('x', 32) + literal + new string('x', 32) + prefix);
                    var storage = new byte[offset + bytes.Length + 16];
                    Array.Fill(storage, (byte)0xff);
                    bytes.CopyTo(storage.AsSpan(offset));
                    var subject = storage.AsSpan(offset, bytes.Length);
                    Assert.Equal(1, regex.Count(subject, controlled ? cancellation.Token : default));
                    Assert.Equal(0, regex.Count(Encoding.UTF8.GetBytes(new string('x', 32) + prefix),
                        controlled ? cancellation.Token : default));
                }
            }
        }
    }

    [Theory]
    [InlineData("汉字")]
    [InlineData("夏洛克·福尔摩斯")]
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
        Assert.True(threeByte.Inspection.DebugTryCountExactUtf8LiteralPreparedSearch("汉字!汉字"u8, out var prepared));
        Assert.Equal(2, validated);
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
        Assert.False(regex.Inspection.DebugTryCountExactUtf8LiteralPreparedSearch("aaa"u8, out var prepared));
        Assert.False(regex.Inspection.DebugTryCountExactUtf8LiteralAnchored("aaa"u8, out var anchored));
        Assert.Equal(0, threeByte);
        Assert.Equal(0, prepared);
        Assert.Equal(0, anchored);
    }
}
