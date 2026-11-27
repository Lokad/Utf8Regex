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


        Assert.Equal("<abc><>\n<def><>", System.Text.Encoding.UTF8.GetString(regex.Replace(input, "<$0>"u8)));
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

        var endAnchored = new Utf8Pcre2Regex(@"\w*?", options: Pcre2CompileOptions.EndAnchored);
        Assert.Equal("abc", System.Text.Encoding.UTF8.GetString(endAnchored.Match("abc"u8).GetValueBytes()));

        var firstLine = new Utf8Pcre2Regex(@"\w+", options: Pcre2CompileOptions.FirstLine);
        Assert.Equal(1, firstLine.Count("abc\ndef"u8));
    }

    [Fact]
    public void TokenRepeatPreservesVariableWidthTokenBoundaries()
    {
        var graphemes = new Utf8Pcre2Regex(@"\X+");
        var newlineSequences = new Utf8Pcre2Regex(@"\R+");
        var codeUnits = new Utf8Pcre2Regex(            @"\C+", options:             Pcre2CompileOptions.None, compileSettings:             new Utf8Pcre2CompileSettings { BackslashC = Pcre2BackslashCPolicy.Allow }, policy: new Utf8RegexPolicy
        {
            MatchTimeout = Utf8Pcre2Regex.DefaultMatchTimeout
        });
        Assert.Equal("a\u0301🇫🇷", System.Text.Encoding.UTF8.GetString(graphemes.Match(Encoding.UTF8.GetBytes("a\u0301🇫🇷")).GetValueBytes()));
        Assert.Equal("\r\n\n", System.Text.Encoding.UTF8.GetString(newlineSequences.Match("\r\n\nx"u8).GetValueBytes()));
        Assert.Equal("é", System.Text.Encoding.UTF8.GetString(codeUnits.Match("é"u8).GetValueBytes()));
    }

    [Theory]
    [InlineData(".+", Pcre2NewlineConvention.Crlf, Pcre2CompileOptions.None)]
    [InlineData(@"\N+", Pcre2NewlineConvention.Any, Pcre2CompileOptions.DotAll)]
    public void StrictEndDotRepeatKeepsByteCapturesWithLimitedDepth(
        string run, Pcre2NewlineConvention newline, Pcre2CompileOptions options)
    {
        using var regex = new Utf8Pcre2Regex(@"\G(?<body>" + run + @"\z)", options: options,
            compileSettings: new() { Newline = newline },
            policy: new() { ResourcePolicy = new() { MaxDepth = 8 } });
        var text = "é" + string.Concat(Enumerable.Repeat("😀é", 64));
        var input = Encoding.UTF8.GetBytes(text);
        var captures = new Utf8Pcre2CaptureRange[2];
        using var source = new CancellationTokenSource();

        var match = regex.Match(input, 2, captures, cancellationToken: source.Token);
        Assert.True(match.Success);
        Assert.Equal((2, input.Length), (match.StartOffsetInBytes, match.EndOffsetInBytes));
        Assert.True(captures[1].Success);
        Assert.Equal((2, input.Length), (captures[1].StartOffsetInBytes, captures[1].EndOffsetInBytes));
        Assert.False(regex.IsMatch(Encoding.UTF8.GetBytes(text + "\r\n"), 2, cancellationToken: source.Token));
    }

    [Fact]
    public void DotAllCanGiveBackFinalLfAtALaxEndBeforeAConsumingSuffix()
    {
        using var regex = new Utf8Pcre2Regex(@"\A(?<body>.+\Z)\n", options: Pcre2CompileOptions.DotAll);
        var captures = new Utf8Pcre2CaptureRange[2];
        var match = regex.Match("é😀\n"u8, captures: captures);
        Assert.True(match.Success);
        Assert.Equal(7, match.EndOffsetInBytes);
        Assert.True(captures[1].Success);
        Assert.Equal((0, 6), (captures[1].StartOffsetInBytes, captures[1].EndOffsetInBytes));
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
