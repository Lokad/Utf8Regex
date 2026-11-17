using System.Text;
using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex.Pcre2.Tests;

public sealed class Pcre2MultilineSearchTests
{
    [Fact]
    public void ExactLinePrefixKeepsPublicValueAndOutputSemantics()
    {
        var regex = new Utf8Pcre2Regex("^ERROR: .+$", Pcre2CompileOptions.Multiline);
        var input = Encoding.UTF8.GetBytes(
            "INFO: boot\nERROR: first\nERROR: \nERROR: \r\nERROR: café\n");
        Assert.True(regex.IsMatch(input));
        Assert.Equal(3, regex.Count(input));
        Assert.Equal("ERROR: first", regex.Match(input).GetValueString());

        var values = new List<string>();
        var matches = regex.EnumerateMatches(input);
        while (matches.MoveNext())
        {
            values.Add(matches.Current.GetValueString());
        }

        Assert.Equal(["ERROR: first", "ERROR: \r", "ERROR: café"], values);

        Span<Utf8Pcre2MatchData> destination = stackalloc Utf8Pcre2MatchData[2];
        Assert.Equal(2, regex.MatchMany(input, destination, out var isMore));
        Assert.True(isMore);
        Assert.Equal("INFO: boot\n<ERROR: first>\nERROR: \n<ERROR: \r>\n<ERROR: café>\n",
            regex.ReplaceToString(input, "<$0>"));
    }

    [Fact]
    public void ExactLinePrefixHonorsLfBoundariesAndStartOffsets()
    {
        var regex = new Utf8Pcre2Regex(
            "^ERROR: .+$",
            Pcre2CompileOptions.Multiline,
            new Utf8Pcre2CompileSettings { Newline = Pcre2NewlineConvention.Lf },
            default(Utf8Pcre2ExecutionLimits),
            Regex.InfiniteMatchTimeout);
        var input = "ERROR: zero\nINFO: one\nERROR: two\r\nERROR: three"u8;
        var secondMatchStart = "ERROR: zero\nINFO: one\n"u8.Length;
        Assert.Equal(2, regex.Count(input, 1));
        Assert.Equal(2, regex.Count(input, secondMatchStart));
        Assert.Equal("ERROR: two\r", regex.Match(input, secondMatchStart).GetValueString());
        Assert.True(regex.IsMatch(input, secondMatchStart, Pcre2MatchOptions.Anchored));
        Assert.False(regex.IsMatch(input, secondMatchStart + 1, Pcre2MatchOptions.Anchored));
    }

    [Fact]
    public void ExactLinePrefixSupportsFiniteLimits()
    {
        var regex = new Utf8Pcre2Regex(
            "^ERROR: .+$",
            Pcre2CompileOptions.Multiline,
            default,
            new Utf8Pcre2ExecutionLimits { MatchLimit = 1 },
            Regex.InfiniteMatchTimeout);
        Assert.Equal(
            Pcre2ErrorKind.MatchLimit,
            Assert.Throws<Pcre2MatchException>(() => regex.IsMatch("INFO: ok\nERROR: boom"u8)).ErrorKind);
    }

    [Fact]
    public void ExactLinePrefixMatchesBclAcrossAsciiOffsets()
    {
        var direct = new Utf8Pcre2Regex("^ERROR: .+$", Pcre2CompileOptions.Multiline);
        var oracle = new Regex("^ERROR: .+$", RegexOptions.Multiline | RegexOptions.CultureInvariant);

        foreach (var inputText in new[]
                 {
                     string.Empty,
                     "ERROR: x",
                     "ERROR: \nERROR: y\n",
                     "INFO: x\nERROR: y\nERROR: z",
                     "\nERROR: \r\nINFO: tail\n",
                     "ERROR: a\0b\nERROR: c",
                 })
        {
            var input = Encoding.UTF8.GetBytes(inputText);
            for (var start = 0; start <= input.Length; start++)
            {
                var expected = oracle.Matches(inputText, start);
                Assert.Equal(expected.Count != 0, direct.IsMatch(input, start));
                Assert.Equal(expected.Count, direct.Count(input, start));

                var directRanges = new List<(int Start, int End)>();
                var directMatches = direct.EnumerateMatches(input, start);
                while (directMatches.MoveNext())
                {
                    directRanges.Add((
                        directMatches.Current.StartOffsetInBytes,
                        directMatches.Current.EndOffsetInBytes));
                }

                Assert.Equal(expected.Select(m => (m.Index, m.Index + m.Length)).ToList(), directRanges);
            }
        }
    }

    [Fact]
    public void ExplicitCrLineConventionPreservesItsOwnBoundaries()
    {
        var regex = new Utf8Pcre2Regex("^ERROR: .+$", Pcre2CompileOptions.Multiline,
            new Utf8Pcre2CompileSettings { Newline = Pcre2NewlineConvention.Cr },
            default(Utf8Pcre2ExecutionLimits), Regex.InfiniteMatchTimeout);
        Assert.Equal(2, regex.Count("ERROR: first\rERROR: second"u8));
        Assert.True(regex.IsMatch("ERROR: \n"u8));
        Assert.False(regex.IsMatch("ERROR: \r"u8));
    }

    [Fact]
    public void ExplicitLfLineShapeMatchesOracleOnSlice()
    {
        var regex = new Utf8Pcre2Regex("^ERROR\\n.+$", Pcre2CompileOptions.Multiline);
        Assert.IsType<Pcre2SliceDirectProgram>(regex.DebugCompiledProgram.Operations.Count);
        var oracle = new Regex(
            "^ERROR\\n.+$",
            RegexOptions.Multiline | RegexOptions.CultureInvariant,
            Regex.InfiniteMatchTimeout);
        foreach (var inputText in new[]
                 {
                     "ERROR\nabc\nERROR\ndef",
                     "INFO\nERROR\nabc",
                     "ERROR\n",
                     "xERROR\nabc",
                 })
        {
            var input = Encoding.UTF8.GetBytes(inputText);
            Assert.Equal(oracle.IsMatch(inputText), regex.IsMatch(input));
            Assert.Equal(oracle.Matches(inputText).Count, regex.Count(input));
        }
    }
}
