using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.FrontEnd;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8ProfileTokenQualificationTests
{
    private const string Uri = @"[\w]+://[^/\s?#]+[^\s?#]+(?:\?[^\s#]*)?(?:#[^\s]*)?";
    private const string Octet = @"(?:25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9])";
    private const string Ip = "(?:" + Octet + @"\.){3}" + Octet;
    private static void Range(string pattern, string text, int index, int length)
    {
        var input = Encoding.UTF8.GetBytes(text);
        foreach (var options in new[] { RegexOptions.None, RegexOptions.Compiled, RegexOptions.RightToLeft, RegexOptions.RightToLeft | RegexOptions.Compiled })
        {
            var regex = new Utf8Regex(pattern, options | RegexOptions.CultureInvariant); Assert.True(regex.IsMatch(input)); Assert.Equal(1, regex.Count(input));
            var match = regex.Match(input); var detailed = Utf8MatchSequence.First(regex.CreateMatchCursor(input));
            Assert.Equal((index, length), (match.StartOffsetInBytes, (match.EndOffsetInBytes - match.StartOffsetInBytes))); Assert.Equal((index, length), (detailed.Value.StartOffsetInBytes, (detailed.Value.EndOffsetInBytes - detailed.Value.StartOffsetInBytes)));
        }
    }
    private static void AsciiOracle(string pattern, IEnumerable<string> subjects)
    {
        var oracle = new Regex(pattern, RegexOptions.CultureInvariant);
        var regex = new Utf8Regex(pattern, RegexOptions.CultureInvariant);
        foreach (var text in subjects)
        {
            var input = Encoding.ASCII.GetBytes(text);
            Assert.Equal(oracle.Count(text), regex.Count(input));
            for (var floor = 0; floor <= input.Length; floor++)
            {
                var expected = oracle.Match(text, floor);
                var actual = regex.Match(input, floor, Span<Utf8CaptureRange>.Empty);
                Assert.Equal(expected.Success, actual.Success);
                if (actual.Success) Assert.Equal((expected.Index, expected.Length), (actual.StartOffsetInBytes, actual.EndOffsetInBytes - actual.StartOffsetInBytes));
            }
        }
    }

    [Fact]
    public void UriBodyMinimumCountsCompleteScalarsRatherThanSurrogateUnits()
    {
        foreach (var text in new[] { "x://😀", "𐐀://😀?q#f", "x://𐐀", "x:///😀😀" })
        {
            foreach (var options in new[] { RegexOptions.None, RegexOptions.Compiled }) Assert.Equal(0, new Utf8Regex(Uri, options).Count(Encoding.UTF8.GetBytes(text)));
        }
        Range(Uri, " x://😀/ ", 1, 9); Range(Uri, " 𐐀://😀😀?q#f ", 1, 19);
        Assert.Equal(1, new Utf8Regex(Uri).Count("𐐀://😀😀"u8));
    }

    [Fact]
    public void UriFragmentAllowsNulAndDoesNotStartASecondTokenInsideIt()
    {
        var text = "x://😀a#\0y://ab";
        Assert.Equal(1, new Utf8Regex(Uri).Count(Encoding.UTF8.GetBytes(text)));
        Range(Uri, text, 0, Encoding.UTF8.GetByteCount(text));
    }

    [Fact]
    public void UriAliasesProveTheEntireUnicodeWordAndWhitespaceLanguages()
    {
        const string alias = @"[\p{L}\p{Mn}\p{Nd}\p{Pc}]+\x3a//[^\s\x2f\x3f\x23]+[^\s\x3f\x23]+(?:\x3f[^\s\x23]*)?(?:\x23[^\s]*)?";
        Range(alias, " 𐐀://😀/ ", 1, 12);

        AsciiOracle(Uri, new[] { "", "x://a", "x://ab", "x://a/", "x://ab#\0y://cd", "x://ab?\0#f", "x://ab y://cd", "///x://ab", "x://ab\vx://cd", "_://a\0" });
    }

    [Fact]
    public void FiveByteDateUsesItsDeclaredMinimumAndPreservesScalarRanges()
    {
        Range(@"\d{1}/\d{1}/\d{1}", "1/2/3", 0, 5);
        Assert.Equal(1, new Utf8Regex(@"\d{1}/\d{1}/\d{1}").Count("1/2/3"u8));
        Range(@"\d{1}/\d{1}/\d{1}", "😀 ١/٢/٣", 5, 8);
    }

    [Fact]
    public void DateAliasesKeepScalarFieldsCapturesAndBoundaries()
    {
        const string pattern = @"\b(?<day>\p{Nd}{1,2})\x2f(?:[0-9]{1,2})\x2d\d{2,4}\b";
        Range(pattern, "😀 ١/2-٢٠ ", 5, 9);
        Assert.True(Utf8MatchSequence.First(new Utf8Regex(pattern).CreateMatchCursor(Encoding.UTF8.GetBytes("😀 ١/2-٢٠ "))).TryGetGroup("day", out var captured));
        Assert.Equal((5, 2), (captured.StartOffsetInBytes, (captured.EndOffsetInBytes - captured.StartOffsetInBytes)));
        var input = Encoding.UTF8.GetBytes("𐐀1/2-20"); Assert.False(new Utf8Regex(pattern).IsMatch(input));
        Range(@"\d{0,2}/\d{1}/\d{1}", "😀 /2/3", 5, 4);
    }

    [Fact]
    public void DatePublicRangesAgreeAtEveryByteFloor()
    {
        var subjects = Enumerable.Range(0, 1 << 7).Select(bits => new string(Enumerable.Range(0, 7)
            .Select(bit => (bits & (1 << bit)) == 0 ? '1' : '/').ToArray()));
        using var source = new CancellationTokenSource();
        foreach (var pattern in new[] { @"\d{1}/\d{1}/\d{1}", @"\d{1,2}/\d{1,2}/\d{1,3}", @"\b\d{1,2}/\d{1,2}/\d{2,4}\b" })
        {
            var oracle = new Regex(pattern, RegexOptions.CultureInvariant);
            var regex = new Utf8Regex(pattern, RegexOptions.CultureInvariant, resourcePolicy: new()
            { MaxWorkSteps = 1_000_000, MaxDepth = 4096, MaxWorkingBytes = 1 << 20, MaxResultBytes = 1 << 20 });
            foreach (var text in subjects.Concat(["1/2/3", "11/22/333", "a1/2/33b", " 11/2/33 ", "1/2/3 1/2/3"]))
            {
                var input = Encoding.ASCII.GetBytes(text);
                Assert.Equal(oracle.Count(text), regex.Count(input, source.Token));
                for (var floor = 0; floor <= input.Length; floor++)
                {
                    var expected = oracle.Match(text, floor);
                    var actual = regex.Match(input, floor, Span<Utf8CaptureRange>.Empty, source.Token);
                    Assert.Equal(expected.Success, actual.Success);
                    if (expected.Success) Assert.Equal((expected.Index, expected.Length),
                        (actual.StartOffsetInBytes, actual.EndOffsetInBytes - actual.StartOffsetInBytes));
                }
            }
        }
    }

    [Fact]
    public void Ipv4AliasesUseOneSelectorAndPreserveOrderedOctetBranches()
    {
        var alias = Ip.Replace("25", @"\x32\x35").Replace(@"\.", @"\x2e");
        Range(alias, "😀00.11.25.255", 4, 12);
        var reorderedOctet = @"(?:[01]?[0-9][0-9]|25[0-5]|2[0-4][0-9])";
        var reordered = "(?:" + Octet + @"\.){3}" + reorderedOctet;
        // In RTL the two-digit arm leaves a digit before the separator and
        // backtracks to the three-digit arm. Keep both directional outcomes.
        foreach (var options in new[] { RegexOptions.None, RegexOptions.Compiled, RegexOptions.RightToLeft, RegexOptions.RightToLeft | RegexOptions.Compiled })
        {
            var regex = new Utf8Regex(reordered, options | RegexOptions.CultureInvariant);
            var expectedLength = (options & RegexOptions.RightToLeft) != 0 ? 12 : 11;
            var match = regex.Match("00.11.22.255"u8); var detailed = Utf8MatchSequence.First(regex.CreateMatchCursor("00.11.22.255"u8.ToArray()));
            Assert.Equal((0, expectedLength), (match.StartOffsetInBytes, (match.EndOffsetInBytes - match.StartOffsetInBytes)));
            Assert.Equal((0, expectedLength), (detailed.Value.StartOffsetInBytes, (detailed.Value.EndOffsetInBytes - detailed.Value.StartOffsetInBytes)));
            Assert.True(regex.IsMatch("00.11.22.255"u8)); Assert.Equal(1, regex.Count("00.11.22.255"u8));
        }
    }

    [Fact]
    public void Ipv4NativeRangesAgreeWithOrderedAsciiOracleAtEveryFloor()
    {
        var subjects = from a in new[] { "00", "199", "255", "256" } from b in new[] { "01", "249", "255" }
            from c in new[] { "00", "200", "255" } from d in new[] { "0", "00", "099", "199", "200", "249", "255", "256", "999" }
            select $"!{a}.{b}.{c}.{d}!";
        AsciiOracle(Ip, subjects.Concat(["00.11.22.25500", "255.255.255.255", "00.00.00.00 11.11.11.11"]));
    }
}
