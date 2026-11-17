using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.FrontEnd;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8FinalFamilyQualificationTests
{
    private const string Email = @"^[a-zA-Z0-9_\-\.]+@(\[[0-9]{1,3}\.[0-9]{1,3}\.[0-9]{1,3}\.|([a-zA-Z0-9\-]+\.)+)([a-zA-Z]{2,12}|[0-9]{1,3})\]?$";
    private const string Digits = @"^(?<before>.*)(https://go(\.testing)?\.lokad.com|~)(?<trigram>/[a-zA-Z0-9]+)?/d/(?<topicId>\d+)/?\?t=(?<tab>[^ ?]+)(?<rest>.*)$";
    private const string Hex = @"^(?<before>.*)(https://go(\.testing)?\.lokad.com|~)(?<trigram>/[a-zA-Z0-9]+)?/gateway/BigFiles/Browse/Download\?hash=(?<hash>[a-fA-F0-9]*)(?<optPath>[?&]path=[^& \n]+)?[?&]name=(?<name>[^& ]+)(?<optPath2>[?&]path=[^& \n]+)?(?<rest>.*)$";
    private const string HexRoute = "/gateway/BigFiles/Browse/Download?hash=";
    private const RegexOptions Fold = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;
    private static void PublicRange(string pattern, RegexOptions options, string text, int index, int length, bool rtl)
    {
        var input = Encoding.UTF8.GetBytes(text);
        foreach (var lane in rtl ? new[] { RegexOptions.None, RegexOptions.Compiled, RegexOptions.RightToLeft, RegexOptions.RightToLeft | RegexOptions.Compiled } : new[] { RegexOptions.None, RegexOptions.Compiled })
        {
            var regex = new Utf8Regex(pattern, options | lane); var match = regex.Match(input); var detailed = Utf8MatchSequence.First(regex.CreateMatchCursor(input));
            Assert.True(regex.IsMatch(input)); Assert.Equal(1, regex.Count(input));
            Assert.Equal((index, length), (match.StartOffsetInBytes, (match.EndOffsetInBytes - match.StartOffsetInBytes))); Assert.Equal((index, length), (detailed.Value.StartOffsetInBytes, (detailed.Value.EndOffsetInBytes - detailed.Value.StartOffsetInBytes)));
        }
    }

    [Fact]
    public void EmailProofBorrowsAliasesCapturesAndAsciiIntersections()
    {
        var alias = Email.Replace("^", @"\A").Replace("@", @"(?<at>\x40)").Replace("[0-9]", @"\p{Nd}").Replace("$", @"\Z");
        PublicRange(alias, RegexOptions.CultureInvariant, "a@[١.٢.٣.٤]", 0, 15, true);
        Assert.True(Utf8MatchSequence.First(new Utf8Regex(alias).CreateMatchCursor("a@a.ab"u8.ToArray())).TryGetGroup("at", out var group));
        Assert.Equal((1, 1), (group.StartOffsetInBytes, (group.EndOffsetInBytes - group.StartOffsetInBytes)));
    }

    [Fact]
    public void EmailWholeEndpointsAgreeWithIndependentAsciiGrammar()
    {
        var oracle = new Regex(Email, RegexOptions.CultureInvariant); var regex = new Utf8Regex(Email, RegexOptions.CultureInvariant);
        foreach (var local in new[] { "a", "1", "-", ".", "a_b", "", "é" })
        foreach (var domain in new[] { "a.ab", "a.1", "a.a", "a.abcdefghijkl", "a.abcdefghijklm", "a.ab]", "[1.2.3.4]", "[1.2.3.ab", "[1.2.3.4", "[1.2.3.4444]" })
        foreach (var suffix in new[] { "", "\n", "\n\n", "\r", " " })
        {
            var text = local + "@" + domain + suffix; var expected = oracle.Match(text); var actual = regex.Match(Encoding.UTF8.GetBytes(text));
            Assert.Equal(expected.Success, actual.Success);
            if (actual.Success) Assert.Equal(expected.Length, actual.EndOffsetInBytes);
        }
    }

    [Fact]
    public void UngroupedEmailTailAlternativeKeepsItsOwnedUnanchoredGrammar()
    {
        var pattern = Email.Replace("([a-zA-Z]{2,12}|[0-9]{1,3})", "[a-zA-Z]{2,12}|[0-9]{1,3}");
        PublicRange(pattern, RegexOptions.None, "1]", 0, 2, true);
    }

    [Fact]
    public void QueryMarkersRequireEqualsAndOptionalTrigramCanBacktrack()
    {
        Assert.False(new Utf8Regex(Digits, Fold).IsMatch("~/d/1?tX"u8));
        var hex = new Utf8Regex(Hex, Fold);
        Assert.True(hex.IsMatch(Encoding.ASCII.GetBytes("~" + HexRoute + "&name=x")));
        Assert.True(hex.IsMatch(Encoding.ASCII.GetBytes("~" + HexRoute + "?path=x\r&name=y")));
    }

    [Fact]
    public void QueryValueClassesAndFinalLfOwnTheirExactByteExtent()
    {
        var oracle = new Regex(Digits, Fold); var regex = new Utf8Regex(Digits, Fold);
        foreach (var text in new[] { "x\r~/d/1?t=x", "~/d/1?t=x\n", "~/d/1?t=x \n", "~/d/1?t=x\nz", "~/d/1?t=\n", "~/d/1?t=?", "~/d/1?t=x?tail\n" })
        {
            var expected = oracle.Match(text); var actual = regex.Match(Encoding.ASCII.GetBytes(text));
            Assert.Equal(expected.Success, actual.Success); if (actual.Success) Assert.Equal(expected.Length, actual.EndOffsetInBytes);
        }
        PublicRange(Digits, Fold, "😀~/d/١?t=é\n", 0, 16, true);
    }

    [Fact]
    public void QueryProofBorrowsFragmentedScopedLiteralsAndCapturedCulture()
    {
        var alias = @"(?i:\A" + Digits[1..^1].Replace("https", @"(?<scheme>\x68)ttps") + @"\Z)";
        PublicRange(alias, RegexOptions.CultureInvariant, "https://go.lokad.com/d/1?t=x", 0, 28, true);
        var before = new string('a', 110);
        foreach (var prefix in new[] { before, before + "a" })
        {
            var regex = new Utf8Regex(Digits.Replace("https://go", prefix), Fold);
            var hit = Encoding.ASCII.GetBytes(prefix + ".testing.lokadXcom/d/1?t=x");
            Assert.Equal(hit.Length, regex.Match(hit).EndOffsetInBytes);
        }
        Assert.Equal(19, new Utf8Regex(Digits.Replace("https://go", @"\x00"), Fold).Match("\0.lokadXcom/d/1?t=x"u8).EndOffsetInBytes);
    }

    [Fact]
    public void QueryFixedProfilesAgreeWithIndependentAsciiCountsAndRanges()
    {
        foreach (var pattern in new[] { Digits, Hex })
        {
            var oracle = new Regex(pattern, Fold);
            foreach (var prefix in new[] { "~", "https://go.lokad.com", "https://go.lokadXcom", "HTTPS://GO.TESTING.LOKAD.COM", "miss", "x\r~", "x\n~" })
            foreach (var trigram in new[] { "", "/abc", "/d", "/123", "/?" })
            foreach (var tail in pattern == Digits ? new[] { "/d/1?t=x", "/d/12/?t=x", "/d/1&t=x", "/d/1?t=x&z", "/d/1?t=x\nz", "/d/1?t=x \n" } :
                new[] { HexRoute + "&name=x", HexRoute + "ab?path=x&name=y", HexRoute + "?path=x\n&name=y", HexRoute + "&name=x\nz", HexRoute + "&name=x?path=z", HexRoute + "&name=x \n" })
            {
                var text = prefix + trigram + tail; var expected = oracle.Match(text); var input = Encoding.ASCII.GetBytes(text);
                var actual = new Utf8Regex(pattern, Fold).Match(input); Assert.Equal(expected.Success, actual.Success); if (actual.Success) Assert.Equal(expected.Length, actual.EndOffsetInBytes);
                Assert.Equal(oracle.Count(text), new Utf8Regex(pattern, Fold | RegexOptions.Compiled).Count(input));
            }
        }
    }
}
