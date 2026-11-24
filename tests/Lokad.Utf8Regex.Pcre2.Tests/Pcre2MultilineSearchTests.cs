using System.Text;
using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex.Pcre2.Tests;

public sealed class Pcre2MultilineSearchTests
{
    [Fact]
    public void ExactLinePrefixKeepsPublicValueAndOutputSemantics()
    {
        var regex = new Utf8Pcre2Regex("^ERROR: .+$", options: Pcre2CompileOptions.Multiline);
        var input = Encoding.UTF8.GetBytes(
            "INFO: boot\nERROR: first\nERROR: \nERROR: \r\nERROR: café\n");
        Assert.True(regex.IsMatch(input));
        Assert.Equal(3, regex.Count(input));
        Assert.Equal("ERROR: first", System.Text.Encoding.UTF8.GetString(regex.Match(input).GetValueBytes()));

        var values = new List<string>();
        var matches = regex.EnumerateMatches(input);
        while (matches.MoveNext())
        {
            values.Add(System.Text.Encoding.UTF8.GetString(matches.Current.GetValueBytes()));
        }

        Assert.Equal(["ERROR: first", "ERROR: \r", "ERROR: café"], values);


        Assert.Equal("INFO: boot\n<ERROR: first>\nERROR: \n<ERROR: \r>\n<ERROR: café>\n",
            System.Text.Encoding.UTF8.GetString(regex.Replace(input, "<$0>"u8)));
    }

    [Fact]
    public void ExactLinePrefixHonorsLfBoundariesAndStartOffsets()
    {
        var regex = new Utf8Pcre2Regex(            "^ERROR: .+$", options:             Pcre2CompileOptions.Multiline, compileSettings:             new Utf8Pcre2CompileSettings { Newline = Pcre2NewlineConvention.Lf }, policy: new Utf8RegexPolicy
        {
            MatchTimeout = Regex.InfiniteMatchTimeout
        });
        var input = "ERROR: zero\nINFO: one\nERROR: two\r\nERROR: three"u8;
        var secondMatchStart = "ERROR: zero\nINFO: one\n"u8.Length;
        Assert.Equal(2, regex.Count(input, 1));
        Assert.Equal(2, regex.Count(input, secondMatchStart));
        Assert.Equal("ERROR: two\r", System.Text.Encoding.UTF8.GetString(regex.Match(input, startOffsetInBytes: secondMatchStart).GetValueBytes()));
        Assert.True(regex.IsMatch(input, secondMatchStart, Pcre2MatchOptions.Anchored));
        Assert.False(regex.IsMatch(input, secondMatchStart + 1, Pcre2MatchOptions.Anchored));
    }

    [Fact]
    public void ExactLinePrefixSupportsFiniteLimits()
    {
        var regex = new Utf8Pcre2Regex(            "^ERROR: .+$", options:             Pcre2CompileOptions.Multiline, compileSettings:             default, policy: new Utf8RegexPolicy
        {
            ResourcePolicy = new Utf8ResourcePolicy
            {
                MaxWorkSteps = 1
            },
            MatchTimeout = Regex.InfiniteMatchTimeout
        });
        Assert.Equal(
            Pcre2ErrorKind.MatchLimit,
            Assert.Throws<Pcre2MatchException>(() => regex.IsMatch("INFO: ok\nERROR: boom"u8)).ErrorKind);
    }

    [Fact]
    public void ExactLinePrefixEnumerationUsesBoundedWorkAcrossNonStarters()
    {
        using var regex = new Utf8Pcre2Regex("^ERROR: .+$", options: Pcre2CompileOptions.Multiline,
            policy: new() { ResourcePolicy = new() { MaxWorkSteps = 128 } });
        using var live = new CancellationTokenSource();
        var gap = "😀" + new string('x', 4096) + "\n";
        var input = Encoding.UTF8.GetBytes(gap + "ERROR: é\n" + gap);
        var start = Encoding.UTF8.GetByteCount(gap);

        var matches = regex.EnumerateMatches(input, 4, cancellationToken: live.Token);
        Assert.True(matches.MoveNext());
        Assert.Equal(start, matches.Current.StartOffsetInBytes);
        Assert.Equal(start + "ERROR: é"u8.Length, matches.Current.EndOffsetInBytes);
        Assert.False(matches.MoveNext());
    }

    [Fact]
    public void ExactLinePrefixSkipsInteriorDecoysAndFindsPairAcrossScanBlocks()
    {
        using var regex = new Utf8Pcre2Regex("^ERROR: .+$", options: Pcre2CompileOptions.Multiline,
            policy: new() { ResourcePolicy = new() { MaxWorkSteps = 128 } });
        using var live = new CancellationTokenSource();
        var decoys = "😀 " + string.Concat(Enumerable.Repeat("DEBUG: ERROR: decoy ", 90));
        var prefix = decoys + new string('x', 2047 - Encoding.UTF8.GetByteCount(decoys)) + "\n";
        var input = Encoding.UTF8.GetBytes(prefix + "ERROR: é😀");

        var matches = regex.EnumerateMatches(input, cancellationToken: live.Token);
        Assert.True(matches.MoveNext());
        Assert.Equal(2048, matches.Current.StartOffsetInBytes);
        Assert.Equal(input.Length, matches.Current.EndOffsetInBytes);
        Assert.False(matches.MoveNext());
    }

    [Fact]
    public void ExactLinePrefixMatchesBclAcrossAsciiOffsets()
    {
        var direct = new Utf8Pcre2Regex("^ERROR: .+$", options: Pcre2CompileOptions.Multiline);
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
        var regex = new Utf8Pcre2Regex("^ERROR: .+$", options: Pcre2CompileOptions.Multiline, compileSettings:             new Utf8Pcre2CompileSettings { Newline = Pcre2NewlineConvention.Cr }, policy: new Utf8RegexPolicy
        {
            MatchTimeout = Regex.InfiniteMatchTimeout
        });
        Assert.Equal(2, regex.Count("ERROR: first\rERROR: second"u8));
        Assert.True(regex.IsMatch("ERROR: \n"u8));
        Assert.False(regex.IsMatch("ERROR: \r"u8));
    }

    [Fact]
    public void ExplicitLfLineShapeMatchesOracleOnSlice()
    {
        var regex = new Utf8Pcre2Regex("^ERROR\\n.+$", options: Pcre2CompileOptions.Multiline);
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
