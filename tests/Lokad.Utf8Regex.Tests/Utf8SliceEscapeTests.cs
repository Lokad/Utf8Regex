using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex.Tests;

// R03 numeric escapes: \xHH (exactly two hex), \uHHHH (exactly four hex),
// and octal (\0-led in literals, bare in classes, up to three digits) with
// ASCII results lower to literals, members, and range bounds. Malformed
// spellings decline to legacy-exact construction throws, and non-ASCII
// results decline with legacy-exact behavior. Bare shapes keep native
// precedence; quantified shapes route owned.
// Bare C0 control escapes (\a \e \f \n \r \t \v plus \c@..\c_ letters)
// lower to literals with BCL-probed control codes; invalid \c shapes
// decline with legacy-exact construction failures.
public sealed class Utf8SliceEscapeTests
{
    public static IEnumerable<object?[]> SliceEscapeShapes()
    {
        yield return new object?[] { "\\x41+" };
        yield return new object?[] { "\\u0041+" };
        yield return new object?[] { "\\01+" };
        yield return new object?[] { "[\\x41-\\x5A]+" };
        yield return new object?[] { "[\\101-\\132]+" };
        yield return new object?[] { "[\\u0041]+" };
    }

    [Theory]
    [MemberData(nameof(SliceEscapeShapes))]
    public void EscapeShapesRouteOwned(string pattern)
    {
        foreach (var options in new[]
        {
            RegexOptions.None,
            RegexOptions.CultureInvariant,
            RegexOptions.Compiled | RegexOptions.CultureInvariant,
        })
        {
            var regex = new Utf8Regex(pattern, options);
            Assert.True(regex.Inspection.DebugUsesOwnedSliceProgram, options.ToString());
        }
    }

    public static IEnumerable<object?[]> IsMatchCases()
    {
        yield return new object?[] { "\\x41", "A", true };
        yield return new object?[] { "\\x41", "B", false };
        yield return new object?[] { "\\x41+", "AAB", true };
        yield return new object?[] { "\\u0041", "A", true };
        yield return new object?[] { "\\u004A", "J", true };
        yield return new object?[] { "\\0", "\u0000", true };
        yield return new object?[] { "\\00", "\u0000", true };
        yield return new object?[] { "\\000", "\u0000", true };
        yield return new object?[] { "\\07", "\u0007", true };
        yield return new object?[] { "\\12", "\n", true };
        yield return new object?[] { "\\12B", "\nB", true };
        yield return new object?[] { "\\08", "\u0000" + "8", true };
        yield return new object?[] { "a\\0", "a\u0000", true };
        yield return new object?[] { "[\\x41]", "A", true };
        yield return new object?[] { "[\\x41]", "B", false };
        yield return new object?[] { "[\\x41-\\x5A]", "M", true };
        yield return new object?[] { "[\\x41-\\x5A]", "a", false };
        yield return new object?[] { "[\\101-\\132]", "S", true };
        yield return new object?[] { "[\\u0041]", "A", true };
        yield return new object?[] { "[\\0]", "\u0000", true };
        yield return new object?[] { "[\\7]", "\u0007", true };
        yield return new object?[] { "[\\12]", "\n", true };
        yield return new object?[] { "[\\77]", "?", true };
        yield return new object?[] { "(\\x41)(\\x42)", "AB", true };
    }

    [Theory]
    [MemberData(nameof(IsMatchCases))]
    public void OwnedIsMatchMatchesOracle(string pattern, string inputText, bool expected)
    {
        var input = Encoding.UTF8.GetBytes(inputText);
        var oracle = new Regex(pattern, RegexOptions.CultureInvariant, Regex.InfiniteMatchTimeout);
        Assert.Equal(expected, oracle.IsMatch(inputText));
        foreach (var options in new[]
        {
            RegexOptions.None,
            RegexOptions.CultureInvariant,
            RegexOptions.Compiled | RegexOptions.CultureInvariant,
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

    public static IEnumerable<object?[]> ThrowCases()
    {
        yield return new object?[] { "\\x4" };
        yield return new object?[] { "\\xG1" };
        yield return new object?[] { "\\x" };
        yield return new object?[] { "\\u41" };
        yield return new object?[] { "\\u004" };
        yield return new object?[] { "\\8" };
        yield return new object?[] { "\\9" };
        yield return new object?[] { "[\\x4]" };
        yield return new object?[] { "[\\u41]" };
        yield return new object?[] { "[\\8]" };
    }

    [Theory]
    [MemberData(nameof(ThrowCases))]
    public void MalformedEscapesFailConstruction(string pattern)
    {
        Assert.Throws<RegexParseException>(
            () => new Utf8Regex(pattern, RegexOptions.CultureInvariant));
    }

    public static IEnumerable<object?[]> NonAsciiCases()
    {
        yield return new object?[] { "\\xFF", "ÿ" };
        yield return new object?[] { "\\u00E9", "é" };
        yield return new object?[] { "\\400", "Ā" };
        yield return new object?[] { "[\\xFF]", "ÿ" };
        yield return new object?[] { "[\\400]", "Ā" };
    }

    [Theory]
    [MemberData(nameof(NonAsciiCases))]
    public void NonAsciiEscapesKeepOracleAgreement(string pattern, string inputText)
    {
        var input = Encoding.UTF8.GetBytes(inputText);
        var oracle = new Regex(pattern, RegexOptions.CultureInvariant, Regex.InfiniteMatchTimeout);
        var regex = new Utf8Regex(pattern, RegexOptions.CultureInvariant);
        Assert.Equal(pattern != "\\400", regex.Inspection.DebugUsesOwnedSliceProgram);
        Assert.Equal(oracle.IsMatch(inputText), regex.IsMatch(input));
        if (pattern != "\\400") Assert.False(regex.Inspection.DebugFallbackRegexIsCreated);
    }

    [Theory]
    [InlineData("\\x41+", "aAb", 1)]
    [InlineData("[\\x41-\\x5A]+", "ABc", 1)]
    public void OwnedCountMatchesOracle(string pattern, string inputText, int expected)
    {
        var input = Encoding.UTF8.GetBytes(inputText);
        var oracle = new Regex(pattern, RegexOptions.CultureInvariant, Regex.InfiniteMatchTimeout);
        Assert.Equal(expected, oracle.Matches(inputText).Count);
        var regex = new Utf8Regex(pattern, RegexOptions.CultureInvariant);
        Assert.True(regex.Inspection.DebugUsesOwnedSliceProgram, "pattern=" + pattern);
        Assert.Equal(expected, regex.Count(input));
        Assert.False(regex.Inspection.DebugFallbackRegexIsCreated, "pattern=" + pattern);
    }

    [Theory]
    [InlineData("\\t+", "\ta\t", "\t")]
    [InlineData("\\cA+", "x\u0001\u0001", "\u0001\u0001")]
    [InlineData("(?<=\\n)x", "a\nx", "x")]
    public void OwnedIgnoreCaseFoldsControlLiterals(string pattern, string inputText, string expected)
    {
        var input = Encoding.UTF8.GetBytes(inputText);
        var oracle = new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, Regex.InfiniteMatchTimeout);
        Assert.Equal(expected, oracle.Match(inputText).Value);
        var regex = new Utf8Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        Assert.True(regex.Inspection.DebugUsesOwnedSliceProgram, "pattern=" + pattern);
        Assert.Equal(expected, regex.MatchDetailed(input).GetGroup(0).GetValueString());
        Assert.False(regex.Inspection.DebugFallbackRegexIsCreated, "pattern=" + pattern);
    }

    [Theory]
    [InlineData("\\x41+", "aa", "aa")]
    public void OwnedIgnoreCaseFoldsEscapeLiterals(string pattern, string inputText, string expected)
    {
        var input = Encoding.UTF8.GetBytes(inputText);
        var oracle = new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, Regex.InfiniteMatchTimeout);
        Assert.Equal(expected, oracle.Match(inputText).Value);
        var regex = new Utf8Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        Assert.True(regex.Inspection.DebugUsesOwnedSliceProgram, "pattern=" + pattern);
        Assert.Equal(expected, regex.MatchDetailed(input).GetGroup(0).GetValueString());
        Assert.False(regex.Inspection.DebugFallbackRegexIsCreated, "pattern=" + pattern);
    }

    public static IEnumerable<object?[]> SliceControlShapes()
    {
        yield return new object?[] { "\\n+" };
        yield return new object?[] { "\\t+" };
        yield return new object?[] { "\\r+" };
        yield return new object?[] { "\\a+" };
        yield return new object?[] { "\\f+" };
        yield return new object?[] { "\\v+" };
        yield return new object?[] { "\\e+" };
        yield return new object?[] { "\\cA+" };
        yield return new object?[] { "\\cZ+" };
        yield return new object?[] { "(?<=\\n)x" };
    }

    [Theory]
    [MemberData(nameof(SliceControlShapes))]
    public void ControlShapesRouteOwned(string pattern)
    {
        foreach (var options in new[]
        {
            RegexOptions.None,
            RegexOptions.CultureInvariant,
            RegexOptions.Compiled | RegexOptions.CultureInvariant,
        })
        {
            var regex = new Utf8Regex(pattern, options);
            Assert.True(regex.Inspection.DebugUsesOwnedSliceProgram, options.ToString());
        }
    }

    public static IEnumerable<object?[]> ControlIsMatchCases()
    {
        yield return new object?[] { "\\n", "\n", true };
        yield return new object?[] { "\\n", "n", false };
        yield return new object?[] { "\\t", "\t", true };
        yield return new object?[] { "\\r", "\r", true };
        yield return new object?[] { "\\a", "\u0007", true };
        yield return new object?[] { "\\a", "a", false };
        yield return new object?[] { "\\f", "\f", true };
        yield return new object?[] { "\\v", "\v", true };
        yield return new object?[] { "\\e", "\u001B", true };
        yield return new object?[] { "\\e", "e", false };
        yield return new object?[] { "\\cA", "\u0001", true };
        yield return new object?[] { "\\cA", "A", false };
        yield return new object?[] { "\\cZ", "\u001A", true };
        yield return new object?[] { "\\c@", "\u0000", true };
        yield return new object?[] { "\\n+", "\n\n\n", true };
        yield return new object?[] { "\\cA+", "\u0001\u0001", true };
        yield return new object?[] { "(?<=\\n)x", "\nx", true };
        yield return new object?[] { "(?<=\\n)x", "ax", false };
        yield return new object?[] { "\\cM", "M", false };
        yield return new object?[] { "\\cm", "m", false };
    }

    [Theory]
    [MemberData(nameof(ControlIsMatchCases))]
    public void OwnedControlIsMatchMatchesOracle(string pattern, string inputText, bool expected)
    {
        var input = Encoding.UTF8.GetBytes(inputText);
        var oracle = new Regex(pattern, RegexOptions.CultureInvariant, Regex.InfiniteMatchTimeout);
        Assert.Equal(expected, oracle.IsMatch(inputText));
        foreach (var options in new[]
        {
            RegexOptions.None,
            RegexOptions.CultureInvariant,
            RegexOptions.Compiled | RegexOptions.CultureInvariant,
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

    [Theory]
    [InlineData("\\t+", "\ta\t", 2)]
    [InlineData("\\cA+", "\u0001x\u0001", 2)]
    [InlineData("(?<=\\n)x", "ax\nx", 1)]
    public void OwnedControlCountMatchesOracle(string pattern, string inputText, int expected)
    {
        var input = Encoding.UTF8.GetBytes(inputText);
        var oracle = new Regex(pattern, RegexOptions.CultureInvariant, Regex.InfiniteMatchTimeout);
        Assert.Equal(expected, oracle.Matches(inputText).Count);
        var regex = new Utf8Regex(pattern, RegexOptions.CultureInvariant);
        Assert.True(regex.Inspection.DebugUsesOwnedSliceProgram, "pattern=" + pattern);
        Assert.Equal(expected, regex.Count(input));
        Assert.False(regex.Inspection.DebugFallbackRegexIsCreated, "pattern=" + pattern);
    }

    [Theory]
    [InlineData("\\n+", "\n\n", "\n\n")]
    [InlineData("\\cZ+", "a\u001A\u001A", "\u001A\u001A")]
    public void OwnedControlMatchMatchesOracle(string pattern, string inputText, string expected)
    {
        var input = Encoding.UTF8.GetBytes(inputText);
        var oracle = new Regex(pattern, RegexOptions.CultureInvariant, Regex.InfiniteMatchTimeout);
        Assert.Equal(expected, oracle.Match(inputText).Value);
        var regex = new Utf8Regex(pattern, RegexOptions.CultureInvariant);
        Assert.True(regex.Inspection.DebugUsesOwnedSliceProgram, "pattern=" + pattern);
        Assert.Equal(expected, regex.MatchDetailed(input).GetGroup(0).GetValueString());
        Assert.False(regex.Inspection.DebugFallbackRegexIsCreated, "pattern=" + pattern);
    }

    public static IEnumerable<object?[]> ControlLetterCodeCases()
    {
        yield return new object?[] { "@", "\u0000" };
        yield return new object?[] { "A", "\u0001" };
        yield return new object?[] { "B", "\u0002" };
        yield return new object?[] { "C", "\u0003" };
        yield return new object?[] { "D", "\u0004" };
        yield return new object?[] { "E", "\u0005" };
        yield return new object?[] { "F", "\u0006" };
        yield return new object?[] { "G", "\u0007" };
        yield return new object?[] { "H", "\u0008" };
        yield return new object?[] { "I", "\u0009" };
        yield return new object?[] { "J", "\u000A" };
        yield return new object?[] { "K", "\u000B" };
        yield return new object?[] { "L", "\u000C" };
        yield return new object?[] { "M", "\u000D" };
        yield return new object?[] { "N", "\u000E" };
        yield return new object?[] { "O", "\u000F" };
        yield return new object?[] { "P", "\u0010" };
        yield return new object?[] { "Q", "\u0011" };
        yield return new object?[] { "R", "\u0012" };
        yield return new object?[] { "S", "\u0013" };
        yield return new object?[] { "T", "\u0014" };
        yield return new object?[] { "U", "\u0015" };
        yield return new object?[] { "V", "\u0016" };
        yield return new object?[] { "W", "\u0017" };
        yield return new object?[] { "X", "\u0018" };
        yield return new object?[] { "Y", "\u0019" };
        yield return new object?[] { "Z", "\u001A" };
        yield return new object?[] { "[", "\u001B" };
        yield return new object?[] { "\\", "\u001C" };
        yield return new object?[] { "]", "\u001D" };
        yield return new object?[] { "^", "\u001E" };
        yield return new object?[] { "_", "\u001F" };
        yield return new object?[] { "a", "\u0001" };
        yield return new object?[] { "m", "\u000D" };
        yield return new object?[] { "z", "\u001A" };
    }

    [Theory]
    [MemberData(nameof(ControlLetterCodeCases))]
    public void OwnedControlLetterMatchesCode(string letter, string inputText)
    {
        // Bare single literals keep native precedence, so the range sweep
        // quantifies: quantified control letters route owned like \cA+.
        var pattern = "\\c" + letter + "+";
        var doubledText = inputText + inputText;
        var input = Encoding.UTF8.GetBytes(doubledText);
        var oracle = new Regex(pattern, RegexOptions.CultureInvariant, Regex.InfiniteMatchTimeout);
        Assert.True(oracle.IsMatch(doubledText), "pattern=" + pattern);
        foreach (var options in new[]
        {
            RegexOptions.None,
            RegexOptions.CultureInvariant,
            RegexOptions.Compiled | RegexOptions.CultureInvariant,
        })
        {
            var regex = new Utf8Regex(pattern, options);
            Assert.True(regex.Inspection.DebugUsesOwnedSliceProgram, options.ToString());
            Assert.True(regex.IsMatch(input), "pattern=" + pattern);
            Assert.False(regex.Inspection.DebugFallbackRegexIsCreated, "pattern=" + pattern);
        }
    }

    public static IEnumerable<object?[]> TurkicControlShapes()
    {
        // Slice IgnoreCase folds invariantly, so Turkic-style construction
        // cultures stay on legacy routing with oracle-exact behavior.
        yield return new object?[] { "\\t+", "\tA\t" };
        yield return new object?[] { "\\cA+", "\u0001A" };
    }

    [Theory]
    [MemberData(nameof(TurkicControlShapes))]
    public void TurkicControlShapesDeclineToLegacy(string pattern, string inputText)
    {
        var savedCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("tr-TR");
            var options = RegexOptions.IgnoreCase;
            var input = Encoding.UTF8.GetBytes(inputText);
            var oracle = new Regex(pattern, options, Regex.InfiniteMatchTimeout);
            var regex = new Utf8Regex(pattern, options);
            Assert.False(regex.Inspection.DebugUsesOwnedSliceProgram, "pattern=" + pattern);
            Assert.Equal(oracle.IsMatch(inputText), regex.IsMatch(input));
            Assert.Equal(oracle.Matches(inputText).Count, regex.Count(input));
        }
        finally
        {
            CultureInfo.CurrentCulture = savedCulture;
        }
    }

    public static IEnumerable<object?[]> InvalidControlShapes()
    {
        yield return new object?[] { "\\c " };
        yield return new object?[] { "\\c" };
        yield return new object?[] { "\\c?" };
        yield return new object?[] { "\\c`" };
        yield return new object?[] { "\\c~" };
        yield return new object?[] { "\\C" };
    }

    [Theory]
    [MemberData(nameof(InvalidControlShapes))]
    public void InvalidControlEscapesFailConstruction(string pattern)
    {
        Assert.Throws<RegexParseException>(() => new Regex(pattern, RegexOptions.CultureInvariant));
        Assert.Throws<RegexParseException>(() => new Utf8Regex(pattern, RegexOptions.CultureInvariant));
    }
}
