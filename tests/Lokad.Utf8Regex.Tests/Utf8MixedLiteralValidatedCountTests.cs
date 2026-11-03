using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Input;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8MixedLiteralValidatedCountTests
{
    [Theory]
    [InlineData("中é文")]
    [InlineData("汉字")]
    [InlineData("中😀")]
    [InlineData("中\u0800a")]
    [InlineData("中\uD7FF\uE000\U00010000\U0010FFFF")]
    [InlineData("中中中")]
    public void CheckedCounterAndPublicModesKeepNonoverlapAtEveryVectorOffset(string literal)
    {
        var needle = Encoding.UTF8.GetBytes(literal);
        foreach (var options in new[] { RegexOptions.None, RegexOptions.Compiled, RegexOptions.NonBacktracking })
        {
            var regex = new Utf8Regex(Regex.Escape(literal), options | RegexOptions.CultureInvariant);
            for (var offset = 0; offset < 65; offset++)
            for (var tail = 0; tail < 34; tail++)
            {
                var text = new string('x', offset) + literal + literal + "é😀\u0800\uD7FF_" + literal + new string('y', tail);
                var bytes = Encoding.UTF8.GetBytes(text);
                var storage = new byte[bytes.Length + 26];
                Array.Fill(storage, (byte)0xFF);
                bytes.CopyTo(storage, 7);
                var input = storage.AsSpan(7, bytes.Length);
                var expected = CountOrdinal(input, needle);
                Assert.Equal(expected, Utf8LiteralCompiledEngineRuntime.CountExactUtf8LiteralValidatedThreeByte(input, needle));
                Assert.Equal(expected, regex.Count(input));
            }
        }
    }

    [Theory]
    [InlineData("中", "中中中中中中中中中中中中中中中中中中中中中中中中中中中中中中中中中中")]
    [InlineData("中中中", "中中中中中中中中中中中中中中中中中中中中中中中中中中中中中中中中中中")]
    [InlineData("中é中", "中é中é中é中é中é中é中é中é中é中é中é中é中é中é中é中é中é中")]
    public void OverlappingNeedlesAndVectorTailsCountEachNonoverlappingMatchOnce(string literal, string text)
    {
        var needle = Encoding.UTF8.GetBytes(literal);
        for (var padding = 0; padding < 65; padding++)
        {
            var input = Encoding.UTF8.GetBytes(new string('x', padding) + text);
            Assert.Equal(CountOrdinal(input, needle),
                Utf8LiteralCompiledEngineRuntime.CountExactUtf8LiteralValidatedThreeByte(input, needle));
        }
    }

    [Fact]
    public void MalformedSequencesKeepTheFirstGlobalErrorAcrossEveryVectorBoundary()
    {
        byte[][] malformed = [[0x80], [0xC0, 0x80], [0xC1, 0xBF], [0xC2], [0xC2, 0x61],
            [0xE0, 0x9F, 0xBF], [0xED, 0xA0, 0x80], [0xE2, 0x82], [0xE2, 0x61, 0x80],
            [0xF0, 0x8F, 0xBF, 0xBF], [0xF4, 0x90, 0x80, 0x80], [0xF5, 0x80, 0x80, 0x80]];
        var needle = "中é"u8.ToArray();
        var ordinary = new Utf8Regex("中é", RegexOptions.CultureInvariant);
        var compiled = new Utf8Regex("中é", RegexOptions.Compiled | RegexOptions.CultureInvariant);
        foreach (var bad in malformed)
        for (var padding = 0; padding < 97; padding++)
        {
            byte[] input = [.. Encoding.UTF8.GetBytes("中é" + new string('x', padding)), .. bad];
            var expected = Assert.Throws<ArgumentException>(() => Utf8Validation.Validate(input));
            var direct = Assert.Throws<ArgumentException>(() =>
                Utf8LiteralCompiledEngineRuntime.CountExactUtf8LiteralValidatedThreeByte(input, needle));
            Assert.Equal(expected.Message, direct.Message);
            Assert.Equal(expected.Message, Assert.Throws<ArgumentException>(() => ordinary.Count(input)).Message);
            Assert.Equal(expected.Message, Assert.Throws<ArgumentException>(() => compiled.Count(input)).Message);
        }
    }

    [Fact]
    public void EligibilityKeepsTwoByteFamiliesRtlAndAssertionsOnTheirExistingRoutes()
    {
        Assert.True(new Utf8Regex("中é").Inspection.DebugCanUseFusedCompiledUtf8LiteralCount);
        Assert.False(new Utf8Regex("ЖЖ").Inspection.DebugCanUseFusedCompiledUtf8LiteralCount);
        Assert.False(new Utf8Regex("中é", RegexOptions.RightToLeft).Inspection.DebugCanUseFusedCompiledUtf8LiteralCount);
        Assert.False(new Utf8Regex("中é(?=x)").Inspection.DebugCanUseFusedCompiledUtf8LiteralCount);
        Assert.False(new Utf8Regex("中é|文é").Inspection.DebugCanUseFusedCompiledUtf8LiteralFamilyCount);
        foreach (var options in new[] { RegexOptions.None, RegexOptions.Compiled, RegexOptions.RightToLeft, RegexOptions.IgnoreCase })
        {
            var text = "x中é中É_中é";
            var oracle = new Regex(Regex.Escape("中é"), options | RegexOptions.CultureInvariant);
            Assert.Equal(oracle.Count(text), new Utf8Regex("中é", options | RegexOptions.CultureInvariant).Count(Encoding.UTF8.GetBytes(text)));
        }
    }

    [Fact]
    public void ControlledCountsKeepNumericAdmissionAndOriginalCancellation()
    {
        var input = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat("中é文 😀 ", 600)));
        var regex = new Utf8Regex("中é文", RegexOptions.Compiled, Regex.InfiniteMatchTimeout,
            new Utf8ResourcePolicy { MaxWorkSteps = 1_000_000, MaxDepth = 1, MaxWorkingBytes = 1, MaxResultBytes = 1 });
        using var token = new CancellationTokenSource();
        Assert.Equal(600, regex.Count(input));
        Assert.Equal(600, regex.Count(input, token.Token));
        token.Cancel();
        Assert.Equal(token.Token, Assert.Throws<OperationCanceledException>(() => regex.Count(input, token.Token)).CancellationToken);
        var denied = new Utf8Regex("中é文", default, Regex.InfiniteMatchTimeout, new Utf8ResourcePolicy { MaxWorkSteps = 1 });
        Assert.Equal(Utf8ResourceLimitKind.ExecutionWorkSteps,
            Assert.Throws<Utf8ResourceLimitExceededException>(() => denied.Count(input)).ResourceKind);
    }

    private static int CountOrdinal(ReadOnlySpan<byte> input, ReadOnlySpan<byte> literal)
    {
        var count = 0;
        while (input.IndexOf(literal) is var index && index >= 0)
        {
            count++;
            input = input[(index + literal.Length)..];
        }
        return count;
    }
}
