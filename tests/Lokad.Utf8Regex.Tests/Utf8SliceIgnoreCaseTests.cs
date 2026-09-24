using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex.Tests;

// R03 case-insensitive slice: IgnoreCase folds per-scalar BCL-invariant case
// (ASCII pairs plus single-char ToLowerInvariant; U+0130 never folds to one
// char, astral halves never fold) across literals, classes (fold-then-test,
// so U+212A hits), and backreferences (scalar walk with per-side widths).
// Bare shapes keep native precedence; Turkic construction cultures keep
// legacy routing through the existing culture gate.
public sealed class Utf8SliceIgnoreCaseTests
{
    // Bare shapes keep native precedence (IgnoreCase-aware kernels with Kelvin
    // fencing); quantified and backreference shapes route owned.
    public static IEnumerable<object?[]> SliceIgnoreCaseShapes()
    {
        yield return new object?[] { "a+" };
        yield return new object?[] { "[a-z]+" };
        yield return new object?[] { "[^a]+" };
        yield return new object?[] { "(a)\\1" };
        yield return new object?[] { "(K)\\1" };
        yield return new object?[] { "[a-c]+" };
    }

    [Theory]
    [MemberData(nameof(SliceIgnoreCaseShapes))]
    public void IgnoreCaseShapesRouteOwned(string pattern)
    {
        foreach (var options in new[]
        {
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled,
        })
        {
            var regex = new Utf8Regex(pattern, options);
            Assert.True(regex.Inspection.DebugUsesOwnedSliceProgram, options.ToString());
        }
    }

    public static IEnumerable<object?[]> IsMatchCases()
    {
        yield return new object?[] { "a+", "AAA", true };
        yield return new object?[] { "a+", "aab", true };
        yield return new object?[] { "abc", "ABC", true };
        yield return new object?[] { "abc", "ABD", false };
        yield return new object?[] { "[a-z]+", "ABC", true };
        yield return new object?[] { "[A-Z]+", "abc", true };
        yield return new object?[] { "[abc]+", "ABC", true };
        yield return new object?[] { "[a-c]+", "B", true };
        yield return new object?[] { "[^a]+", "A", false };
        yield return new object?[] { "[^a]+", "B", true };
        yield return new object?[] { "k", "K", true };
        yield return new object?[] { "(a)\\1", "AA", true };
        yield return new object?[] { "(a)\\1", "Ab", false };
        yield return new object?[] { "(K)\\1", "kK", true };
        yield return new object?[] { "(a)(b)", "AB", true };
    }

    [Theory]
    [MemberData(nameof(IsMatchCases))]
    public void OwnedIsMatchMatchesOracle(string pattern, string inputText, bool expected)
    {
        var input = Encoding.UTF8.GetBytes(inputText);
        var oracle = new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, Regex.InfiniteMatchTimeout);
        Assert.Equal(expected, oracle.IsMatch(inputText));
        foreach (var options in new[]
        {
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled,
        })
        {
            var regex = new Utf8Regex(pattern, options);
            Assert.Equal(expected, regex.IsMatch(input));
            using var source = new CancellationTokenSource();
            Assert.Equal(expected, regex.IsMatch(input, source.Token));
            if (regex.Inspection.DebugUsesOwnedSliceProgram)
            {
                Assert.False(regex.Inspection.DebugFallbackRegexIsCreated, "pattern=" + pattern);
            }
        }
    }

    public static IEnumerable<object?[]> FoldBoundaryCases()
    {
        var kelvin = char.ConvertFromUtf32(0x212A);
        var sigma = char.ConvertFromUtf32(0x03C3);
        var finalSigma = char.ConvertFromUtf32(0x03C2);
        var dottedI = char.ConvertFromUtf32(0x0130);
        var eAcute = char.ConvertFromUtf32(0x00E9);
        var eAcuteUpper = char.ConvertFromUtf32(0x00C9);
        yield return new object?[] { "k", kelvin, true };
        yield return new object?[] { "[a-z]+", kelvin, true };
        yield return new object?[] { "[^a]+", kelvin, true };
        yield return new object?[] { "[a-z]+", sigma, false };
        yield return new object?[] { "[a-z]+", finalSigma, false };
        yield return new object?[] { "i", dottedI, false };
        yield return new object?[] { "[a-z]+", eAcute, false };
        yield return new object?[] { "[a-z]+", eAcuteUpper, false };
    }

    [Theory]
    [MemberData(nameof(FoldBoundaryCases))]
    public void OwnedFoldBoundariesMatchOracle(string pattern, string inputText, bool expected)
    {
        var input = Encoding.UTF8.GetBytes(inputText);
        var oracle = new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, Regex.InfiniteMatchTimeout);
        Assert.Equal(expected, oracle.IsMatch(inputText));
        var regex = new Utf8Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        Assert.Equal(expected, regex.IsMatch(input));
        if (regex.Inspection.DebugUsesOwnedSliceProgram)
        {
            Assert.False(regex.Inspection.DebugFallbackRegexIsCreated, "pattern=" + pattern);
        }
    }

    public static IEnumerable<object?[]> BackrefFoldCases()
    {
        var kelvin = char.ConvertFromUtf32(0x212A);
        var dottedI = char.ConvertFromUtf32(0x0130);
        yield return new object?[] { "(k)\\1", "k" + kelvin, true };
        yield return new object?[] { "(a)\\1", "aA", true };
        yield return new object?[] { "(ab)\\1", "ABab", true };
        yield return new object?[] { "(i)\\1", dottedI + "i", false };
        yield return new object?[] { "(i)\\1", dottedI + dottedI, false };
    }

    [Theory]
    [MemberData(nameof(BackrefFoldCases))]
    public void OwnedBackrefFoldsMatchOracle(string pattern, string inputText, bool expected)
    {
        var input = Encoding.UTF8.GetBytes(inputText);
        var oracle = new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, Regex.InfiniteMatchTimeout);
        Assert.Equal(expected, oracle.IsMatch(inputText));
        var regex = new Utf8Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        Assert.Equal(expected, regex.IsMatch(input));
        if (regex.Inspection.DebugUsesOwnedSliceProgram)
        {
            Assert.False(regex.Inspection.DebugFallbackRegexIsCreated, "pattern=" + pattern);
        }
    }

    [Theory]
    [InlineData("a+", "xAAx", 1)]
    [InlineData("[a-z]+", "AB cD", 2)]
    public void OwnedCountMatchesOracle(string pattern, string inputText, int expected)
    {
        var input = Encoding.UTF8.GetBytes(inputText);
        var oracle = new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, Regex.InfiniteMatchTimeout);
        Assert.Equal(expected, oracle.Matches(inputText).Count);
        var regex = new Utf8Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        Assert.True(regex.Inspection.DebugUsesOwnedSliceProgram, "pattern=" + pattern);
        Assert.Equal(expected, regex.Count(input));
        Assert.False(regex.Inspection.DebugFallbackRegexIsCreated, "pattern=" + pattern);
    }

    [Fact]
    public void OwnedDetailedProjectsFoldedGroups()
    {
        var regex = new Utf8Regex("(a)+(b)+", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        Assert.True(regex.Inspection.DebugUsesOwnedSliceProgram);
        var detailed = regex.MatchDetailed(Encoding.UTF8.GetBytes("AB"));
        Assert.True(detailed.Success);
        Assert.Equal("A", detailed.GetGroup(1).GetValueString());
        Assert.Equal("B", detailed.GetGroup(2).GetValueString());
        Assert.False(regex.Inspection.DebugFallbackRegexIsCreated);
    }

    [Fact]
    public void TurkicCultureKeepsLegacyRouting()
    {
        var savedCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("tr-TR");
            var oracle = new Regex("i", RegexOptions.IgnoreCase, Regex.InfiniteMatchTimeout);
            Assert.False(oracle.IsMatch("I"));
            var regex = new Utf8Regex("i", RegexOptions.IgnoreCase);
            Assert.False(regex.Inspection.DebugUsesOwnedSliceProgram);
            Assert.Equal(oracle.IsMatch("I"), regex.IsMatch(Encoding.UTF8.GetBytes("I")));
        }
        finally
        {
            CultureInfo.CurrentCulture = savedCulture;
        }
    }

    [Theory]
    [InlineData("a+", "AA AA", 2)]
    [InlineData("[a-z]+", "AB cD", 2)]
    public void OwnedSplitMatchesOracle(string pattern, string inputText, int expected)
    {
        var input = Encoding.UTF8.GetBytes(inputText);
        var oracle = new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, Regex.InfiniteMatchTimeout);
        var expectedSegments = new List<string>();
        foreach (var range in oracle.EnumerateSplits(inputText, expected))
        {
            expectedSegments.Add(inputText[range]);
        }

        var regex = new Utf8Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        Assert.True(regex.Inspection.DebugUsesOwnedSliceProgram, "pattern=" + pattern);
        var actualSegments = new List<string>();
        foreach (var split in regex.EnumerateSplits(input, expected))
        {
            actualSegments.Add(split.GetValueString());
        }

        Assert.Equal(expectedSegments, actualSegments);
        Assert.False(regex.Inspection.DebugFallbackRegexIsCreated, "pattern=" + pattern);
    }
}