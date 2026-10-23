using System.Buffers;
using System.Text;

namespace Lokad.Utf8Regex.Pcre2.Tests;

public sealed class Pcre2CrlfCandidateTests
{
    public static IEnumerable<object[]> Cases()
    {
        foreach (var pattern in new[] { ".", @"\N", "(.)", ".+", ".*x", @"(?=.)\N", @"[^\r]", @"([^\r])", @".(?#\n)" })
            yield return [pattern, new[] { "x" }];
        yield return [".x", Array.Empty<string>()];
        foreach (var pattern in new[] { @".|\n", @".|[\n]", @".|\x0A", @".|\x{A}", @".|\012", @"[^\x0C-\x0E]", @"([^\x0C-\x0E])", @"[^\x0C-\x0E]|y", @".\n{0}", @".(?(DEFINE)(?<unused>\n))" })
            yield return [pattern, new[] { "\n", "x" }];
        yield return [@"\B[^\x0C-\x0E]", new[] { "\n" }];
        yield return [@"\D", new[] { "\r", "\n", "x" }];
    }

    [Theory] [MemberData(nameof(Cases))]
    public void CandidateProgressionPreservesCrlfAndExplicitNewlineRoutes(string pattern, string[] expected)
    {
        var input = "\r\nx"u8.ToArray();
        foreach (var finite in new[] { false, true })
        {
            var regex = Create(pattern, finite);
            Assert.Equal(expected.Length != 0, regex.IsMatch(input));
            Assert.Equal(expected.Length, regex.Count(input));
            var first = regex.Match(input);
            Assert.Equal(expected.Length != 0, first.Success);
            if (first.Success) Assert.Equal(expected[0], first.GetValueString());
            var cursor = regex.EnumerateMatches(input);
            var previousEnd = 0;
            var replaced = new StringBuilder();
            foreach (var value in expected)
            {
                Assert.True(cursor.MoveNext());
                var start = "\r\nx".IndexOf(value, previousEnd, StringComparison.Ordinal);
                Assert.True(start >= previousEnd);
                Assert.Equal(start, cursor.Current.StartOffsetInBytes);
                Assert.Equal(start + value.Length, cursor.Current.EndOffsetInBytes);
                Assert.Equal(value, cursor.Current.GetValueString());
                replaced.Append("\r\nx".AsSpan(previousEnd, start - previousEnd)).Append('G');
                previousEnd = start + value.Length;
            }
            Assert.False(cursor.MoveNext());
            replaced.Append("\r\nx".AsSpan(previousEnd));
            Assert.Equal(replaced.ToString(), regex.ReplaceToString(input, "G"));
            var destination = new byte[3];
            Assert.Equal(OperationStatus.Done, regex.TryReplace(input, "G"u8, destination, out var written));
            Assert.Equal(replaced.ToString(), Encoding.UTF8.GetString(destination.AsSpan(0, written)));
            if (pattern == "(.)") Assert.Equal("x", regex.MatchDetailed(input).GetGroup(1).GetValueString());
        }
    }

    [Theory]
    [InlineData(".", 2)] [InlineData(@"\N", 2)] [InlineData("(.)", 2)] [InlineData(".+", 1)]
    public void ExplicitLfStartIsStillAdmissible(string pattern, int expectedCount)
    {
        foreach (var finite in new[] { false, true })
        {
            var regex = Create(pattern, finite);
            foreach (var options in new[] { Pcre2MatchOptions.None, Pcre2MatchOptions.Anchored })
            {
                Assert.False(regex.Match("\r\nx"u8, 0, Pcre2MatchOptions.Anchored).Success);
                var result = regex.Match("\r\nx"u8, 1, options);
                Assert.True(result.Success);
                Assert.Equal(1, result.StartOffsetInBytes);
            }
            Assert.Equal(expectedCount, regex.Count("\r\nx"u8, 1));
        }
    }

    [Theory]
    [InlineData(".")] [InlineData(@"\N")]
    public void CuratedPartialProbeKeepsTheSameCrlfProgression(string pattern)
    {
        foreach (var finite in new[] { false, true })
        foreach (var mode in new[] { Pcre2PartialMode.Soft, Pcre2PartialMode.Hard })
        {
            var regex = Create(pattern, finite);
            Assert.Equal(Utf8Pcre2ProbeKind.NoMatch, regex.Probe("\r\n"u8, mode).Kind);
            var result = regex.Probe("\r\nx"u8, mode);
            Assert.Equal(Utf8Pcre2ProbeKind.FullMatch, result.Kind);
            Assert.Equal((2, 3), (result.Value.StartOffsetInBytes, result.Value.EndOffsetInBytes));
            Assert.Equal(1, regex.Probe("\r\nx"u8, mode, 1).Value.StartOffsetInBytes);
        }
    }

    [Theory]
    [InlineData(Pcre2NewlineConvention.Crlf)]
    [InlineData(Pcre2NewlineConvention.AnyCrlf)]
    [InlineData(Pcre2NewlineConvention.Any)]
    public void DotAllStillConsumesCrAndLf(Pcre2NewlineConvention newline)
    {
        var settings = new Utf8Pcre2CompileSettings { Newline = newline };
        Assert.Equal(3, new Utf8Pcre2Regex(".", Pcre2CompileOptions.DotAll, settings, default, Timeout.InfiniteTimeSpan).Count("\r\nx"u8));
        Assert.Equal(1, new Utf8Pcre2Regex(@"\N", Pcre2CompileOptions.DotAll, settings, default, Timeout.InfiniteTimeSpan).Count("\r\nx"u8));
    }

    [Theory]
    [InlineData(".")] [InlineData("(.)")] [InlineData(@"[^\r]")]
    public void CorrectedRoutesKeepScalarValidationAndCancellation(string pattern)
    {
        var regex = Create(pattern, true);
        var result = regex.Match("\r\n😀x"u8);
        Assert.Equal((2, 6), (result.StartOffsetInBytes, result.EndOffsetInBytes));
        Assert.Equal(2, regex.Count("\r\n😀x"u8));
        using var source = new CancellationTokenSource();
        source.Cancel();
        var error = Assert.Throws<OperationCanceledException>(() => regex.Count("\r\n😀x"u8, 0, default, source.Token));
        Assert.Equal(source.Token, error.CancellationToken);
        Assert.Throws<ArgumentException>(() => regex.IsMatch(new byte[] { 13, 10, (byte)'x', 0xff }));
    }

    private static Utf8Pcre2Regex Create(string pattern, bool finite) => new(pattern, default,
        new() { Newline = Pcre2NewlineConvention.Crlf }, finite
            ? new() { MatchLimit = 1_000_000, DepthLimit = 1024, HeapLimitInBytes = 16 << 20, ResultLimitInBytes = 16 << 20 }
            : default, Timeout.InfiniteTimeSpan);
}
