using System.Text;

namespace Lokad.Utf8Regex.Pcre2.Tests;

public sealed class Pcre2SingleTokenRepeatCompilerTests
{
    [Theory]
    [InlineData(@"\w*", "abcde", "abcde")]
    [InlineData(@"\w*?", "abcde", "")]
    [InlineData(@"\w*+", "abcde", "abcde")]
    [InlineData(@"\w{2,4}", "abcde", "abcd")]
    [InlineData(@"\w{2,4}?", "abcde", "ab")]
    public void DirectRepeatPreservesQuantifierPreference(
        string pattern,
        string inputText,
        string expected)
    {
        var regex = new Utf8Pcre2Regex(pattern);

        Assert.IsType<Pcre2SingleTokenRepeatDirectProgram>(regex.DebugCompiledProgram.Operations.Match);
        Assert.Equal(expected, regex.Match(Encoding.UTF8.GetBytes(inputText)).GetValueString());
    }

    [Fact]
    public void DirectRepeatSharesOneGlobalStreamAcrossOperations()
    {
        var regex = new Utf8Pcre2Regex(@"[^\n]*");
        var input = "abc\ndef"u8;

        Assert.Equal(
            "IsMatch=Pcre2SingleTokenRepeat, Count=Pcre2SingleTokenRepeat, Enumerate=Pcre2SingleTokenRepeat, Match=Pcre2SingleTokenRepeat, Replace=Pcre2SingleTokenRepeat",
            regex.DebugDescribeExecutionPlan());
        Assert.Equal(4, regex.Count(input));

        var values = new List<string>();
        var enumerator = regex.EnumerateMatches(input);
        while (enumerator.MoveNext())
        {
            values.Add(enumerator.Current.GetValueString());
        }

        Assert.Equal(["abc", "", "def", ""], values);

        Span<Utf8Pcre2MatchData> destination = stackalloc Utf8Pcre2MatchData[4];
        Assert.Equal(4, regex.MatchMany(input, destination, out var isMore));
        Assert.False(isMore);
        Assert.Equal("<abc><>\n<def><>", regex.ReplaceToString(input, "<$0>"));
    }

    [Fact]
    public void DirectRepeatHonorsStartAnchorsAndNotEmptyOptions()
    {
        var regex = new Utf8Pcre2Regex(@"[^\n]*");
        var input = "abc\ndef"u8;

        Assert.Equal("def", regex.Match(input, 3, Pcre2MatchOptions.NotEmptyAtStart).GetValueString());
        Assert.False(regex.IsMatch(
            input,
            3,
            Pcre2MatchOptions.Anchored | Pcre2MatchOptions.NotEmptyAtStart));

        var endAnchored = new Utf8Pcre2Regex(@"\w*?", Pcre2CompileOptions.EndAnchored);
        Assert.Equal("abc", endAnchored.Match("abc"u8).GetValueString());

        var firstLine = new Utf8Pcre2Regex(@"\w+", Pcre2CompileOptions.FirstLine);
        Assert.Equal(1, firstLine.Count("abc\ndef"u8));
    }

    [Fact]
    public void DirectRepeatFallsBackToTheVmForMeteredExecution()
    {
        var regex = new Utf8Pcre2Regex(
            @"\w+",
            Pcre2CompileOptions.None,
            default,
            new Utf8Pcre2ExecutionLimits { MatchLimit = 1 },
            System.Text.RegularExpressions.Regex.InfiniteMatchTimeout);

        Assert.IsType<Pcre2SingleTokenRepeatDirectProgram>(regex.DebugCompiledProgram.Operations.Match);
        Assert.Equal(
            "MatchLimit",
            Assert.Throws<Pcre2MatchException>(() => regex.IsMatch("abc"u8)).ErrorKind);
    }

    [Fact]
    public void DirectRepeatPreservesVariableWidthTokenBoundaries()
    {
        var graphemes = new Utf8Pcre2Regex(@"\X+");
        var newlineSequences = new Utf8Pcre2Regex(@"\R+");
        var codeUnits = new Utf8Pcre2Regex(
            @"\C+",
            Pcre2CompileOptions.None,
            new Utf8Pcre2CompileSettings { BackslashC = Pcre2BackslashCPolicy.Allow },
            default,
            default);

        Assert.IsType<Pcre2SingleTokenRepeatDirectProgram>(graphemes.DebugCompiledProgram.Operations.Match);
        Assert.Equal("a\u0301🇫🇷", graphemes.Match(Encoding.UTF8.GetBytes("a\u0301🇫🇷")).GetValueString());
        Assert.Equal("\r\n\n", newlineSequences.Match("\r\n\nx"u8).GetValueString());
        Assert.Equal("é", codeUnits.Match("é"u8).GetValueString());
    }

    [Fact]
    public void ExcludedByteKernelRequiresAnAsciiScalar()
    {
        var regex = new Utf8Pcre2Regex(@"[^\x{e9}]*");

        var direct = Assert.IsType<Pcre2SingleTokenRepeatDirectProgram>(regex.DebugCompiledProgram.Operations.Count);
        Assert.Null(direct.Program.GreedyExcludedAsciiByte);
        Assert.Equal(["abc", "", "def", ""], GetValues(regex, "abcédef"u8));
    }

    [Fact]
    public void DirectRepeatAnalyzerRejectsComposedAndCapturedShapes()
    {
        Assert.IsType<Pcre2BacktrackingDirectProgram>(
            new Utf8Pcre2Regex(@"\w+z").DebugCompiledProgram.Operations.Match);
        Assert.IsType<Pcre2BacktrackingDirectProgram>(
            new Utf8Pcre2Regex(@"(\w+)").DebugCompiledProgram.Operations.Match);
    }

    private static List<string> GetValues(Utf8Pcre2Regex regex, ReadOnlySpan<byte> input)
    {
        var values = new List<string>();
        var enumerator = regex.EnumerateMatches(input);
        while (enumerator.MoveNext())
        {
            values.Add(enumerator.Current.GetValueString());
        }

        return values;
    }
}
