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
        Utf8PatternFailureAssert.Throws(
            () => new Utf8Regex(pattern, options: RegexOptions.CultureInvariant));
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
        var regex = new Utf8Regex(pattern, options: RegexOptions.CultureInvariant);
        // Byte-wrapped class octal zero now admits the native ASCII literal.
        Assert.Equal(pattern is not "\\400" and not "[\\400]", regex.Inspection.DebugUsesOwnedSliceProgram);
        Assert.Equal(oracle.IsMatch(inputText), regex.IsMatch(input));
        if (pattern != "\\400") Assert.True(regex.Inspection.PreparedRegex.ScalarProgram is not null);
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
        var regex = new Utf8Regex(pattern, options: RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        Assert.True(regex.Inspection.DebugUsesOwnedSliceProgram, "pattern=" + pattern);
        Assert.Equal(expected, System.Text.Encoding.UTF8.GetString(Utf8MatchSequence.First(regex.CreateMatchCursor(input)).GetGroupMemory(0).Span));
        Assert.True(regex.Inspection.PreparedRegex.ScalarProgram is not null, "pattern=" + pattern);
    }

    [Theory]
    [InlineData("\\x41+", "aa", "aa")]
    public void OwnedIgnoreCaseFoldsEscapeLiterals(string pattern, string inputText, string expected)
    {
        var input = Encoding.UTF8.GetBytes(inputText);
        var oracle = new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, Regex.InfiniteMatchTimeout);
        Assert.Equal(expected, oracle.Match(inputText).Value);
        var regex = new Utf8Regex(pattern, options: RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        Assert.True(regex.Inspection.DebugUsesOwnedSliceProgram, "pattern=" + pattern);
        Assert.Equal(expected, System.Text.Encoding.UTF8.GetString(Utf8MatchSequence.First(regex.CreateMatchCursor(input)).GetGroupMemory(0).Span));
        Assert.True(regex.Inspection.PreparedRegex.ScalarProgram is not null, "pattern=" + pattern);
    }

    [Theory]
    [InlineData("\\n+", "\n\n", "\n\n")]
    [InlineData("\\cZ+", "a\u001A\u001A", "\u001A\u001A")]
    public void OwnedControlMatchMatchesOracle(string pattern, string inputText, string expected)
    {
        var input = Encoding.UTF8.GetBytes(inputText);
        var oracle = new Regex(pattern, RegexOptions.CultureInvariant, Regex.InfiniteMatchTimeout);
        Assert.Equal(expected, oracle.Match(inputText).Value);
        var regex = new Utf8Regex(pattern, options: RegexOptions.CultureInvariant);
        Assert.True(regex.Inspection.DebugUsesOwnedSliceProgram, "pattern=" + pattern);
        Assert.Equal(expected, System.Text.Encoding.UTF8.GetString(Utf8MatchSequence.First(regex.CreateMatchCursor(input)).GetGroupMemory(0).Span));
        Assert.True(regex.Inspection.PreparedRegex.ScalarProgram is not null, "pattern=" + pattern);
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
            var regex = new Utf8Regex(pattern, options: options);
            Assert.True(regex.Inspection.DebugUsesOwnedSliceProgram, options.ToString());
            Assert.True(regex.IsMatch(input), "pattern=" + pattern);
            Assert.True(regex.Inspection.PreparedRegex.ScalarProgram is not null, "pattern=" + pattern);
        }
    }

    public static IEnumerable<object?[]> TurkicControlShapes()
    {
        // Control literals retain owned scalar execution under Turkic folding.
        yield return new object?[] { "\\t+", "\tA\t" };
        yield return new object?[] { "\\cA+", "\u0001A" };
    }

    [Theory]
    [MemberData(nameof(TurkicControlShapes))]
    public void TurkicControlShapesUseOwnedScalarExecution(string pattern, string inputText)
    {
        var savedCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("tr-TR");
            var options = RegexOptions.IgnoreCase;
            var input = Encoding.UTF8.GetBytes(inputText);
            var oracle = new Regex(pattern, options, Regex.InfiniteMatchTimeout);
            var regex = new Utf8Regex(pattern, options: options);
            Assert.True(regex.Inspection.DebugUsesOwnedSliceProgram, "pattern=" + pattern);
            Assert.Equal(oracle.IsMatch(inputText), regex.IsMatch(input));
            Assert.Equal(oracle.Matches(inputText).Count, regex.Count(input));
            Assert.True(regex.Inspection.PreparedRegex.ScalarProgram is not null);
            Assert.True(ReferenceEquals(regex.Inspection.PreparedRegex.SemanticRegex.CaptureCatalog, regex.Inspection.PreparedRegex.ScalarProgram!.CaptureCatalog));
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
        Utf8PatternFailureAssert.Throws(() => new Utf8Regex(pattern, options: RegexOptions.CultureInvariant));
    }
}
