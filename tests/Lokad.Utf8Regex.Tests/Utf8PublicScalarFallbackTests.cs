using System.Text;
using System.Text.RegularExpressions;
using Xunit;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8PublicScalarFallbackTests
{
    public static IEnumerable<object[]> ScalarCases()
    {
        (string Pattern, string Subject, int Count, int FirstStart, int FirstLength, int LastStart, int LastLength)[] cases =
        [
            (".", "a😀é", 3, 0, 1, 5, 2),
            ("[a-z]+", "😀abc😀z", 2, 4, 3, 11, 1),
            ("\\p{L}{1,2}", "😀\U00010400a", 1, 4, 5, 4, 5),
            ("a.*b", "😀a😀b", 1, 4, 6, 4, 6),
            ("(?<=😀)(?<x>.)", "😀a😀b", 2, 4, 1, 9, 1),
            ("(?<x>😀)(?<12>a)", "z😀a", 1, 1, 5, 1, 5),
            (".?", "a😀", 3, 0, 1, 1, 4),
            ("(?<x>.)+", "😀é", 1, 0, 6, 0, 6),
        ];
        foreach (var item in cases)
        foreach (var compiled in new[] { false, true })
        foreach (var rtl in new[] { false, true })
            yield return [item.Pattern, item.Subject, item.Count,
                rtl ? item.LastStart : item.FirstStart, rtl ? item.LastLength : item.FirstLength, compiled, rtl];
    }

    [Theory]
    [MemberData(nameof(ScalarCases))]
    public void PublicValueAndDetailedLanesUseScalarOutcomes(string pattern, string subject,
        int count, int start, int length, bool compiled, bool rtl)
    {
        var regex = new Utf8Regex(pattern, options: Options(compiled, rtl));
        var input = Encoding.UTF8.GetBytes(subject);
        using var tokenSource = new CancellationTokenSource();
        foreach (var token in new[] { CancellationToken.None, tokenSource.Token })
        {
            Assert.True(regex.IsMatch(input, token));
            Assert.Equal(count, regex.Count(input, token));
            var match = regex.Match(input, cancellationToken: token);
            Assert.True(match.Success);
            Assert.True(match.Success);
            Assert.Equal(start, match.StartOffsetInBytes);
            Assert.Equal(length, (match.EndOffsetInBytes - match.StartOffsetInBytes));
            var detailed = Utf8MatchSequence.First(regex.CreateMatchCursor(input, cancellationToken: token));
            Assert.Equal(match.StartOffsetInBytes, detailed.Value.StartOffsetInBytes);
            Assert.Equal((match.EndOffsetInBytes - match.StartOffsetInBytes), (detailed.Value.EndOffsetInBytes - detailed.Value.StartOffsetInBytes));
            Assert.Equal(Encoding.UTF8.GetString(input.AsSpan(start, length)), System.Text.Encoding.UTF8.GetString(detailed.GetValueMemory().Span));
        }
        Assert.True(regex.Inspection.PreparedRegex.ScalarProgram is not null);
    }

    public static IEnumerable<object[]> AnchorOffsets()
    {
        foreach (var prefix in new[] { false, true })
        foreach (var compiled in new[] { false, true })
        foreach (var rtl in new[] { false, true })
        foreach (var end in new[] { false, true }) yield return [prefix, compiled, rtl, end];
    }

    [Theory]
    [MemberData(nameof(AnchorOffsets))]
    public void OffsetAnchorsRespectTheScalarSearchDirection(bool prefix, bool compiled, bool rtl, bool end)
    {
        var regex = new Utf8Regex(prefix ? "\\Afoo" : "foo\\z", options: Options(compiled, rtl));
        var input = "foo-foo"u8.ToArray();
        var offset = end ? input.Length : 0;
        var expected = rtl ? end : !end;
        using var tokenSource = new CancellationTokenSource();
        foreach (var token in new[] { CancellationToken.None, tokenSource.Token })
        {
            Assert.Equal(expected ? 1 : 0, Utf8MatchSequence.Count(regex.EnumerateMatches(input, offset, token)));
            var match = regex.Match(input, offset, [], token);
            using var captureCursor = regex.CreateMatchCursor(input, offset, token);
            Assert.Equal(expected, captureCursor.MoveNext(out var detailed));
            Assert.Equal(expected, match.Success);
            if (expected)
            {
                Assert.Equal(prefix ? 0 : 4, match.StartOffsetInBytes);
                Assert.Equal(3, (match.EndOffsetInBytes - match.StartOffsetInBytes));
                Assert.Equal(match.StartOffsetInBytes, detailed.Value.StartOffsetInBytes);
                Assert.Equal("foo"u8.ToArray(), detailed.GetValueMemory().ToArray());
            }
        }
        Assert.True(regex.Inspection.PreparedRegex.ScalarProgram is not null);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void ByteOffsetsRejectScalarInteriors(bool compiled, bool rtl)
    {
        var regex = new Utf8Regex("(?<x>.)", options: Options(compiled, rtl));
        var input = "😀a"u8.ToArray();
        Assert.Throws<ArgumentOutOfRangeException>(() => regex.Match(input, 1, []).Success);
        Assert.Throws<ArgumentOutOfRangeException>(() => Utf8MatchSequence.Count(regex.EnumerateMatches(input, 1)));
        Assert.Throws<ArgumentOutOfRangeException>(() => { using var cursor = regex.CreateMatchCursor(input, 1); });
        Assert.True(regex.Inspection.PreparedRegex.ScalarProgram is not null);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void CaptureCursorPreservesWholeScalarCaptures(bool compiled, bool rtl)
    {
        var options = RegexOptions.CultureInvariant |
            (compiled ? RegexOptions.Compiled : RegexOptions.None) |
            (rtl ? RegexOptions.RightToLeft : RegexOptions.None);
        var regex = new Utf8Regex("(?<=😀)(?<x>.)(?<12>.)", options: options);
        var input = Encoding.UTF8.GetBytes("z😀😀a");
        using var cursor = regex.CreateMatchCursor(input, rtl ? input.Length : 5);
        Assert.True(cursor.MoveNext(out var match));
        Assert.Equal((5, 10), (match.Value.StartOffsetInBytes, match.Value.EndOffsetInBytes));
        Assert.Equal("😀a"u8.ToArray(), match.GetValueMemory().ToArray());
        Assert.True(match.TryGetGroup("x", out var first));
        Assert.True(first.Success);
        Assert.Equal((5, 9), (first.StartOffsetInBytes, first.EndOffsetInBytes));
        Assert.Equal("😀"u8.ToArray(), match.GetGroupMemory(regex.GroupNumberFromUtf8Name("x"u8)).ToArray());
        Assert.Equal("a"u8.ToArray(), match.GetGroupMemory(12).ToArray());
        Assert.Equal(1, match.GetCaptures(regex.GroupNumberFromUtf8Name("x"u8)).Length);
        Assert.True(regex.Inspection.PreparedRegex.ScalarProgram is not null);
    }

    private static RegexOptions Options(bool compiled, bool rtl) => RegexOptions.CultureInvariant |
        (compiled ? RegexOptions.Compiled : RegexOptions.None) |
        (rtl ? RegexOptions.RightToLeft : RegexOptions.None);
}
