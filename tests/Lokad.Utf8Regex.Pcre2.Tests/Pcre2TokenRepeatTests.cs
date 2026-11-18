using System.Text;

namespace Lokad.Utf8Regex.Pcre2.Tests;

public sealed class Pcre2TokenRepeatTests
{
    [Theory]
    [InlineData(@"\w*", "abcde", "abcde")]
    [InlineData(@"\w*?", "abcde", "")]
    [InlineData(@"\w*+", "abcde", "abcde")]
    [InlineData(@"\w{2,4}", "abcde", "abcd")]
    [InlineData(@"\w{2,4}?", "abcde", "ab")]
    [InlineData("a+", "aaab", "aaa")]
    [InlineData(".+", "abc\ndef", "abc")]
    [InlineData(@"\d{2,3}", "x1234", "123")]
    [InlineData(@"\p{L}+", "é漢!", "é漢")]
    public void TokenRepeatPreservesQuantifierPreference(
        string pattern,
        string inputText,
        string expected)
    {
        var regex = new Utf8Pcre2Regex(pattern);
        Assert.Equal(expected, System.Text.Encoding.UTF8.GetString(regex.Match(Encoding.UTF8.GetBytes(inputText)).GetValueBytes()));
    }

    [Fact]
    public void LazyRepeatRetriesNonemptyAtTheSameScalarPosition()
    {
        Assert.Equal(["", "a", "", "b", ""], GetValues(new Utf8Pcre2Regex(@"\w*?"), "ab"u8));
        Assert.Equal(["", ""], GetValues(new Utf8Pcre2Regex(@"\w*?"), "é"u8));
        Assert.Equal(["😀", "é"], GetValues(new Utf8Pcre2Regex(".+?"), "😀é"u8));
    }

    [Fact]
    public void TokenRepeatKeepsGlobalProgressionAcrossOperations()
    {
        var regex = new Utf8Pcre2Regex(@"[^\n]*");
        var input = "abc\ndef"u8;
        Assert.Equal(4, regex.Count(input));

        var values = new List<string>();
        var enumerator = regex.EnumerateMatches(input);
        while (enumerator.MoveNext())
        {
            values.Add(System.Text.Encoding.UTF8.GetString(enumerator.Current.GetValueBytes()));
        }

        Assert.Equal(["abc", "", "def", ""], values);

        Span<Utf8Pcre2MatchData> destination = stackalloc Utf8Pcre2MatchData[4];
        Assert.Equal(4, regex.MatchMany(input, destination, out var isMore));
        Assert.False(isMore);
        Assert.Equal("<abc><>\n<def><>", System.Text.Encoding.UTF8.GetString(regex.Replace(input, "<$0>")));
    }

    [Fact]
    public void TokenRepeatHonorsStartAnchorsAndNotEmptyOptions()
    {
        var regex = new Utf8Pcre2Regex(@"[^\n]*");
        var input = "abc\ndef"u8;

        Assert.Equal("def", System.Text.Encoding.UTF8.GetString(regex.Match(input, startOffsetInBytes: 3, matchOptions: Pcre2MatchOptions.NotEmptyAtStart).GetValueBytes()));
        Assert.False(regex.IsMatch(
            input,
            3,
            Pcre2MatchOptions.Anchored | Pcre2MatchOptions.NotEmptyAtStart));

        var endAnchored = new Utf8Pcre2Regex(@"\w*?", Pcre2CompileOptions.EndAnchored);
        Assert.Equal("abc", System.Text.Encoding.UTF8.GetString(endAnchored.Match("abc"u8).GetValueBytes()));

        var firstLine = new Utf8Pcre2Regex(@"\w+", Pcre2CompileOptions.FirstLine);
        Assert.Equal(1, firstLine.Count("abc\ndef"u8));
    }

    [Fact]
    public void TokenRepeatObservesFiniteWork()
    {
        var regex = new Utf8Pcre2Regex(
            @"\w+",
            Pcre2CompileOptions.None,
            default,
            new Utf8Pcre2ExecutionLimits { MatchLimit = 1 },
            System.Text.RegularExpressions.Regex.InfiniteMatchTimeout);
        Assert.Equal(
            Pcre2ErrorKind.MatchLimit,
            Assert.Throws<Pcre2MatchException>(() => regex.IsMatch("abc"u8)).ErrorKind);
    }

    [Fact]
    public void TokenRepeatPreservesVariableWidthTokenBoundaries()
    {
        var graphemes = new Utf8Pcre2Regex(@"\X+");
        var newlineSequences = new Utf8Pcre2Regex(@"\R+");
        var codeUnits = new Utf8Pcre2Regex(
            @"\C+",
            Pcre2CompileOptions.None,
            new Utf8Pcre2CompileSettings { BackslashC = Pcre2BackslashCPolicy.Allow },
            default(Utf8Pcre2ExecutionLimits),
            Utf8Pcre2Regex.DefaultMatchTimeout);
        Assert.Equal("a\u0301🇫🇷", System.Text.Encoding.UTF8.GetString(graphemes.Match(Encoding.UTF8.GetBytes("a\u0301🇫🇷")).GetValueBytes()));
        Assert.Equal("\r\n\n", System.Text.Encoding.UTF8.GetString(newlineSequences.Match("\r\n\nx"u8).GetValueBytes()));
        Assert.Equal("é", System.Text.Encoding.UTF8.GetString(codeUnits.Match("é"u8).GetValueBytes()));
    }

    [Fact]
    public void ExcludedUnicodeScalarPreservesWholeValues()
    {
        var regex = new Utf8Pcre2Regex(@"[^\x{e9}]*");
        Assert.Equal(["abc", "", "def", ""], GetValues(regex, "abcédef"u8));
    }

    [Theory]
    [InlineData("", 0, 1)]
    [InlineData("abc", 0, 2)]
    [InlineData("\n", 0, 2)]
    [InlineData("abc\ndef", 0, 4)]
    [InlineData("abc\n\ndef", 0, 5)]
    [InlineData("abc\ndef", 4, 2)]
    [InlineData("abc\ndef", 3, 3)]
    public void ExcludedAsciiRepeatPreservesGlobalProgression(
        string inputText,
        int startOffsetInBytes,
        int expected)
    {
        var regex = new Utf8Pcre2Regex(@"[^\n]*");
        Assert.Equal(expected, regex.Count(Encoding.UTF8.GetBytes(inputText), startOffsetInBytes));
    }

    private static List<string> GetValues(Utf8Pcre2Regex regex, ReadOnlySpan<byte> input)
    {
        var values = new List<string>();
        var enumerator = regex.EnumerateMatches(input);
        while (enumerator.MoveNext())
        {
            values.Add(System.Text.Encoding.UTF8.GetString(enumerator.Current.GetValueBytes()));
        }

        return values;
    }
}
