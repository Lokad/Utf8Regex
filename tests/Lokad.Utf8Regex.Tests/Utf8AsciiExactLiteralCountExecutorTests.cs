using System.Runtime.Intrinsics;
using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8AsciiExactLiteralCountExecutorTests
{
    private const string Literal = "CancellationToken";

    private static ReadOnlySpan<byte> LiteralUtf8 => "CancellationToken"u8;

    [Fact]
    public void SelectsRareInternalAnchor()
    {
        Assert.Equal(Literal.IndexOf('k'), Utf8AsciiExactLiteralCountExecutor.SelectRarestAnchorOffset(LiteralUtf8));
        Assert.Equal(-1, Utf8AsciiExactLiteralCountExecutor.SelectRarestAnchorOffset("short-rare-x"u8));
        Assert.Equal(-1, Utf8AsciiExactLiteralCountExecutor.SelectRarestAnchorOffset("éxxxxxxxxxxxxxxxx"u8));
        Assert.Equal(-1, Utf8AsciiExactLiteralCountExecutor.SelectRarestAnchorOffset("aaaaaaaaaaaaaaaaa"u8));
    }

    [Fact]
    public void FusedCountCoversEveryVectorLaneAndTail()
    {
        if (!Vector256.IsHardwareAccelerated)
        {
            return;
        }

        var literal = LiteralUtf8.ToArray();
        var anchorOffset = Utf8AsciiExactLiteralCountExecutor.SelectRarestAnchorOffset(literal);
        for (var prefixLength = 0; prefixLength < 64; prefixLength++)
        {
            var input = Encoding.UTF8.GetBytes(new string('x', prefixLength) + Literal + "-" + Literal + "tail");

            Assert.True(Utf8AsciiExactLiteralCountExecutor.TryCountAndValidateAscii(
                input,
                literal,
                anchorOffset,
                out var count));
            Assert.Equal(2, count);
        }
    }

    [Theory]
    [InlineData(RegexOptions.CultureInvariant)]
    [InlineData(RegexOptions.CultureInvariant | RegexOptions.Compiled)]
    public void PublicCountMatchesDotNetAcrossRandomAsciiSubjects(RegexOptions options)
    {
        var random = new Random(0x51A7);
        var regex = new Utf8Regex(Literal, options);
        var oracle = new Regex(Literal, options, Regex.InfiniteMatchTimeout);

        for (var iteration = 0; iteration < 256; iteration++)
        {
            var chars = new char[384];
            for (var index = 0; index < chars.Length; index++)
            {
                chars[index] = (char)('a' + random.Next(26));
            }

            if ((iteration & 1) == 0)
            {
                Literal.CopyTo(0, chars, random.Next(chars.Length - Literal.Length), Literal.Length);
            }

            if (iteration % 5 == 0)
            {
                Literal.CopyTo(0, chars, random.Next(chars.Length - Literal.Length), Literal.Length);
            }

            var input = new string(chars);
            Assert.Equal(oracle.Count(input), regex.Count(Encoding.UTF8.GetBytes(input)));
        }
    }

    [Fact]
    public void FusedCountPreservesNonOverlappingProgression()
    {
        const string repeatedLiteral = "aaaaaaaaaaaaaaaaa";
        var input = new string('a', 51);
        var regex = new Utf8Regex(repeatedLiteral, RegexOptions.CultureInvariant);

        Assert.Equal(Regex.Count(input, repeatedLiteral), regex.Count(Encoding.UTF8.GetBytes(input)));

        if (Vector256.IsHardwareAccelerated)
        {
            Assert.True(Utf8AsciiExactLiteralCountExecutor.TryCountAndValidateAscii(
                Encoding.UTF8.GetBytes(input),
                Encoding.UTF8.GetBytes(repeatedLiteral),
                anchorOffset: 0,
                out var directCount));
            Assert.Equal(3, directCount);
        }
    }

    [Theory]
    [InlineData(RegexOptions.CultureInvariant)]
    [InlineData(RegexOptions.CultureInvariant | RegexOptions.Compiled)]
    public void PublicCountFallsBackForNonAsciiAndRejectsMalformedUtf8(RegexOptions options)
    {
        const string input = "é CancellationToken λCancellationToken";
        var regex = new Utf8Regex(Literal, options);
        var bytes = Encoding.UTF8.GetBytes(input);

        Assert.Equal(Regex.Count(input, Literal, options), regex.Count(bytes));
        Assert.Throws<ArgumentException>(() => regex.Count([.. LiteralUtf8, 0xFF]));
    }

    [Fact]
    public void InfiniteTimeoutRouteUsesFusedCount()
    {
        var input = Encoding.UTF8.GetBytes(new string('x', 256) + Literal);
        var infinite = new Utf8Regex(Literal, RegexOptions.CultureInvariant, Regex.InfiniteMatchTimeout);
        var finite = new Utf8Regex(Literal, RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));

        if (Vector256.IsHardwareAccelerated)
        {
            Assert.Equal("compiled_fused_ascii_literal_count", infinite.CollectCountDiagnostics(input).ExecutionRoute);
            Assert.NotEqual("compiled_fused_ascii_literal_count", finite.CollectCountDiagnostics(input).ExecutionRoute);
        }

        Assert.Equal(infinite.Count(input), finite.Count(input));
    }
}
