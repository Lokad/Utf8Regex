using System.Text;
using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex.Pcre2.Tests;

public sealed class Pcre2WindowSemanticsTests
{
    [Theory]
    [InlineData("é", 25, 1)]
    [InlineData("中", 25, 1)]
    [InlineData("K", 25, 1)]
    [InlineData("é", 26, 0)]
    [InlineData("😀", 25, 1)]
    public void CountKeepsScalarWindowWidthsWithoutUnusedCoordinates(string scalar, int width, int expected)
    {
        var regex = new Utf8Pcre2Regex("Holmes.{0,25}Watson|Watson.{0,25}Holmes");
        const string prefix = "😀π ";
        var text = prefix + "Holmes" + string.Concat(Enumerable.Repeat(scalar, width)) + "Watson";
        var storage = Encoding.UTF8.GetBytes("pad" + text + "pad");
        var input = storage.AsSpan(3, Encoding.UTF8.GetByteCount(text));
        Assert.Equal(expected, regex.Count(input, Encoding.UTF8.GetByteCount(prefix)));
    }

    [Fact]
    public void WindowPublicOperationsPreserveRangesAndReplacement()
    {
        var regex = new Utf8Pcre2Regex("Holmes.{0,25}Watson|Watson.{0,25}Holmes");
        var input = "Holmes and Watson\nWatson near Holmes\nHolmes\nWatson\nHolmesxxxxxxxxxxxxxxxxxxxxxxxxxWatson"u8;
        Assert.True(regex.IsMatch(input));
        Assert.Equal(3, regex.Count(input));
        Assert.Equal("Holmes and Watson", regex.Match(input).GetValueString());

        var values = new List<string>();
        var matches = regex.EnumerateMatches(input);
        while (matches.MoveNext())
        {
            values.Add(matches.Current.GetValueString());
        }

        Assert.Equal(
            ["Holmes and Watson", "Watson near Holmes", "HolmesxxxxxxxxxxxxxxxxxxxxxxxxxWatson"],
            values);
        Assert.Equal(
            "<Holmes and Watson>\n<Watson near Holmes>\nHolmes\nWatson\n<HolmesxxxxxxxxxxxxxxxxxxxxxxxxxWatson>",
            regex.ReplaceToString(input, "<$0>"));
    }

    [Fact]
    public void DifferentLiteralWindowsPreserveCounts()
    {
        var regex = new Utf8Pcre2Regex("Tom.{10,25}river|river.{10,25}Tom");
        var input = "Tom 1234567890 river\nriver 1234567890 Tom"u8;
        Assert.Equal(2, regex.Count(input));
    }

    [Fact]
    public void CapturingAndUncapturedWindowsAgreeAcrossAsciiOffsets()
    {
        var direct = new Utf8Pcre2Regex("Holmes.{0,25}Watson|Watson.{0,25}Holmes");
        var vm = new Utf8Pcre2Regex("(Holmes).{0,25}Watson|(Watson).{0,25}Holmes");

        foreach (var inputText in new[]
                 {
                     string.Empty,
                     "Holmes Watson",
                     "x Holmes one Watson y Watson two Holmes z",
                     "Holmes\nWatson Watson\rHolmes",
                     "Holmes a\0b Watson HolmesxxxxxxxxxxxxxxxxxxxxxxxxxxWatson",
                 })
        {
            var input = Encoding.UTF8.GetBytes(inputText);
            for (var start = 0; start <= input.Length; start++)
            {
                Assert.Equal(vm.Count(input, start), direct.Count(input, start));
            }
        }
    }

    [Fact]
    public void WindowsPreserveSupplementaryScalarsAndFiniteLimits()
    {
        var unicode = new Utf8Pcre2Regex("Holmes.{0,25}Watson|Watson.{0,25}Holmes");
        Assert.Equal(1, unicode.Count("HolmeséWatson"u8));
        var unicodeInput = Encoding.UTF8.GetBytes($"Holmes{string.Concat(Enumerable.Repeat("😀", 25))}Watson");
        Assert.Equal(1, unicode.Count(unicodeInput));
        Assert.Equal(Encoding.UTF8.GetString(unicodeInput), unicode.Match(unicodeInput).GetValueString());
        Assert.Equal(0, unicode.Count("xHolmes and Watson"u8, 0, Pcre2MatchOptions.Anchored));
        Assert.Equal(1, unicode.Count("xHolmes and Watson"u8, 1, Pcre2MatchOptions.Anchored));

        var metered = new Utf8Pcre2Regex(
            "Holmes.{0,25}Watson|Watson.{0,25}Holmes",
            Pcre2CompileOptions.None,
            default,
            new Utf8Pcre2ExecutionLimits { MatchLimit = 1 },
            Regex.InfiniteMatchTimeout);
        Assert.Equal(
            Pcre2ErrorKind.MatchLimit,
            Assert.Throws<Pcre2MatchException>(() => metered.Count("Holmes and Watson"u8)).ErrorKind);
    }

    [Fact]
    public void WindowPriorityAndNewlineOptionsKeepProfileSemantics()
    {
        const string greedy = "Holmes.{0,25}Watson|Watson.{0,25}Holmes";
        const string lazy = "Holmes.{0,25}?Watson|Watson.{0,25}?Holmes";
        var cr = new Utf8Pcre2Regex(greedy, Pcre2CompileOptions.None,
            new Utf8Pcre2CompileSettings { Newline = Pcre2NewlineConvention.Cr });
        Assert.Equal(1, cr.Count("Holmes\nWatson"u8));
        Assert.Equal(0, cr.Count("Holmes\rWatson"u8));
        var text = "Holmes a Watson b Watson"u8.ToArray();
        Assert.Equal("Holmes a Watson b Watson", new Utf8Pcre2Regex(greedy).Match(text).GetValueString());
        Assert.Equal("Holmes a Watson", new Utf8Pcre2Regex(lazy).Match(text).GetValueString());
        Assert.Equal("Watson a Holmes", new Utf8Pcre2Regex(lazy).Match("Watson a Holmes b Holmes"u8).GetValueString());
        var folded = new Utf8Pcre2Regex(greedy, Pcre2CompileOptions.Caseless);
        Assert.Equal("HolmeséWATSON", folded.Match("HolmeséWATSON"u8).GetValueString());
    }
}
