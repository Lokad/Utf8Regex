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
        var regex = new Utf8Regex(pattern, Options(compiled, rtl));
        var input = Encoding.UTF8.GetBytes(subject);
        using var tokenSource = new CancellationTokenSource();
        foreach (var token in new[] { CancellationToken.None, tokenSource.Token })
        {
            Assert.True(regex.IsMatch(input, token));
            Assert.Equal(count, regex.Count(input, token));
            var match = regex.Match(input, token);
            Assert.True(match.Success);
            Assert.True(match.IsByteAligned);
            Assert.Equal(start, match.IndexInBytes);
            Assert.Equal(length, match.LengthInBytes);
            var detailed = regex.MatchDetailed(input, token);
            Assert.Equal(match.IndexInBytes, detailed.IndexInBytes);
            Assert.Equal(match.LengthInBytes, detailed.LengthInBytes);
            Assert.Equal(match.IndexInUtf16, detailed.IndexInUtf16);
            Assert.Equal(match.LengthInUtf16, detailed.LengthInUtf16);
            Assert.Equal(Encoding.UTF8.GetString(input.AsSpan(start, length)), detailed.GetValueString());
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
        var regex = new Utf8Regex(prefix ? "\\Afoo" : "foo\\z", Options(compiled, rtl));
        var input = "foo-foo"u8.ToArray();
        var offset = end ? input.Length : 0;
        var expected = rtl ? end : !end;
        using var tokenSource = new CancellationTokenSource();
        foreach (var token in new[] { CancellationToken.None, tokenSource.Token })
        {
            Assert.Equal(expected, regex.IsMatchFromUtf16Offset(input, offset, token));
            Assert.Equal(expected ? 1 : 0, regex.CountFromUtf16Offset(input, offset, token));
            var match = regex.MatchFromUtf16Offset(input, offset, token);
            var detailed = regex.MatchDetailedFromUtf16Offset(input, offset, token);
            Assert.Equal(expected, match.Success);
            Assert.Equal(expected, detailed.Success);
            if (expected)
            {
                Assert.Equal(prefix ? 0 : 4, match.IndexInBytes);
                Assert.Equal(3, match.LengthInBytes);
                Assert.Equal(match.IndexInBytes, detailed.IndexInBytes);
                Assert.Equal("foo", detailed.GetValueString());
            }
        }
        Assert.True(regex.Inspection.PreparedRegex.ScalarProgram is not null);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void DerivedOffsetsRejectSurrogateInteriors(bool compiled, bool rtl)
    {
        var regex = new Utf8Regex("(?<x>.)", Options(compiled, rtl));
        var input = "😀a"u8.ToArray();
        Assert.Throws<ArgumentOutOfRangeException>(() => regex.IsMatchFromUtf16Offset(input, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => regex.CountFromUtf16Offset(input, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => regex.MatchFromUtf16Offset(input, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => { _ = regex.MatchDetailedFromUtf16Offset(input, 1); });
        Assert.True(regex.Inspection.PreparedRegex.ScalarProgram is not null);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void DetailedOffsetPreservesWholeScalarCaptures(bool compiled, bool rtl)
    {
        var options = RegexOptions.CultureInvariant |
            (compiled ? RegexOptions.Compiled : RegexOptions.None) |
            (rtl ? RegexOptions.RightToLeft : RegexOptions.None);
        var regex = new Utf8Regex("(?<=😀)(?<x>.)(?<12>.)", options);
        var input = Encoding.UTF8.GetBytes("z😀😀a");
        var match = regex.MatchDetailedFromUtf16Offset(input, rtl ? 6 : 3);
        Assert.True(match.Success);
        Assert.Equal(5, match.IndexInBytes);
        Assert.Equal(5, match.LengthInBytes);
        Assert.Equal(3, match.IndexInUtf16);
        Assert.Equal(3, match.LengthInUtf16);
        Assert.Equal("😀a", match.GetValueString());
        var first = match.GetGroup("x");
        Assert.True(first.IsByteAligned);
        Assert.Equal(5, first.IndexInBytes);
        Assert.Equal(4, first.LengthInBytes);
        Assert.Equal("😀", first.GetValueString());
        Assert.Equal("a", match.GetGroup(12).GetValueString());
        Assert.Equal(1, first.CaptureCount);
        Assert.True(regex.Inspection.PreparedRegex.ScalarProgram is not null);
    }

    private static RegexOptions Options(bool compiled, bool rtl) => RegexOptions.CultureInvariant |
        (compiled ? RegexOptions.Compiled : RegexOptions.None) |
        (rtl ? RegexOptions.RightToLeft : RegexOptions.None);
}
