using System.Text;
using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8UnicodeLiteralWindowCountTests
{
    [Theory]
    [InlineData("a.{1,1}b", "a😀b", 1)]
    [InlineData("a.{2,2}b", "a😀b", 0)]
    [InlineData("a.{1,2}b", "é a😀éb a😀😀😀b a\0b", 2)]
    [InlineData("a.{0,4}b", "a😀ba-b", 1)]
    [InlineData("a.{0,1}b", "é!a0ba1b", 2)]
    [InlineData("ab.{0,2}x|a.{0,2}y", "ab😀y ab😀x", 2)]
    [InlineData("a.{1,2}b|b.{1,2}a", "a😀b!béra", 2)]
    [InlineData(@"\ba.{1,1}b\b", "λa😀b a😀b_ a😀b!", 1)]
    [InlineData("a.{0,3}b", "é a😀\nb a😀b", 1)]
    [InlineData("a.{0,3}b", "é a😀\rba😀b", 2)]
    [InlineData("Tom.{10,25}river|river.{10,25}Tom", "é Tom😀😀😀😀😀😀😀😀😀😀river!river0123456789Tom", 1)]
    [InlineData("Tom.{10,25}river|river.{10,25}Tom", "é Tom😀😀😀😀😀😀😀😀😀😀river\nriver0123456789Tom", 2)]
    [InlineData("a.{0,2}b", "😀 a😀x", 0)]
    public void IndependentScalarExtentsKeepGreedyNonoverlappingCounts(string pattern, string text, int expected)
    {
        var storage = Encoding.UTF8.GetBytes("pad" + text + "pad");
        foreach (var options in new[] { RegexOptions.None, RegexOptions.Compiled })
        {
            var regex = new Utf8Regex(pattern, options | RegexOptions.CultureInvariant);
            var input = storage.AsSpan(3, Encoding.UTF8.GetByteCount(text));
            Assert.Equal(expected, regex.Count(input));
            // Match/cursor keep their existing coordinate paths. They provide
            // a separate consistency check in addition to the hand-derived count.
            var cursor = regex.EnumerateMatches(input);
            var count = 0;
            while (cursor.MoveNext()) count++;
            Assert.Equal(expected, count);
            using var token = new CancellationTokenSource();
            var limited = new Utf8Regex(pattern, options | RegexOptions.CultureInvariant,
                Regex.InfiniteMatchTimeout, new() { MaxWorkSteps = 1_000_000,
                    MaxDepth = 1024, MaxWorkingBytes = 1 << 20, MaxResultBytes = 1 });
            Assert.Equal(expected, limited.Count(input, token.Token));
        }
    }

    [Theory]
    [InlineData(2047)] [InlineData(2048)] [InlineData(2049)]
    public void GapLimitEdgesCountScalarsAndKeepTheGeneralLargeGapRoute(int gap)
    {
        var pattern = $"left.{{{gap},{gap}}}right|right.{{{gap},{gap}}}left";
        var text = "é left" + new string('x', gap - 1) + "😀right!";
        var input = Encoding.UTF8.GetBytes(text);
        foreach (var options in new[] { RegexOptions.None, RegexOptions.Compiled })
            Assert.Equal(1, new Utf8Regex(pattern, options).Count(input));
    }

    [Theory]
    [InlineData(@"a\s{2,}b", "é a\u00a0\u00a0b", 1)]
    [InlineData(@"a\s+?b", "a\u00a0b a\tb", 2)]
    public void UnicodeSeparatorsKeepTheirScalarAdmission(string pattern, string text, int expected)
    {
        foreach (var options in new[] { RegexOptions.None, RegexOptions.Compiled })
            Assert.Equal(expected, new Utf8Regex(pattern, options).Count(Encoding.UTF8.GetBytes(text)));
    }

    [Fact]
    public void UnicodeCountReusesBoundedWindowsButKeepsSeparatorAndLargeGapControlsGeneral()
    {
        var regex = new Utf8Regex("Tom.{10,25}river|river.{10,25}Tom", RegexOptions.Compiled);
        var result = regex.CollectCountDiagnostics("é Tom0123456789river\nriver0123456789Tom"u8);
        Assert.Equal(2, result.Result);
        Assert.Equal("native_ordered_literal_window", result.ExecutionRoute);
        Assert.Equal(2, regex.Count("é Tom0123456789river\nriver0123456789Tom"u8));
        foreach (var pattern in new[] { @"a\s+b", "a.{2049,2050}b" })
        {
            var control = new Utf8Regex(pattern, RegexOptions.Compiled);
            Assert.NotEqual("native_ordered_literal_window", control.CollectCountDiagnostics("é a b"u8).ExecutionRoute);
        }
    }

    [Fact]
    public void ScalarWindowCountValidatesTheEntireSubjectAndObservesOriginalCancellation()
    {
        var regex = new Utf8Regex("a.{0,2}b", RegexOptions.Compiled);
        byte[] malformed = [.. "é a😀b"u8, 0xf0, 0x9f];
        Assert.Throws<ArgumentException>(() => regex.Count(malformed));
        using var token = new CancellationTokenSource(); token.Cancel();
        Assert.Equal(token.Token, Assert.Throws<OperationCanceledException>(() =>
            regex.Count("é a😀b"u8, token.Token)).CancellationToken);
        var limited = new Utf8Regex("a.{0,2}b", RegexOptions.Compiled, Regex.InfiniteMatchTimeout,
            new() { MaxWorkSteps = 1 });
        Assert.Equal(Utf8ResourceLimitKind.ExecutionWorkSteps,
            Assert.Throws<Utf8ResourceLimitExceededException>(() => limited.Count("é a😀b"u8)).ResourceKind);
    }
}
