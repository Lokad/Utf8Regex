using System.Text;
using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex.Pcre2.Tests;

public sealed class Pcre2MultilinePrefixCompilerTests
{
    [Fact]
    public void ExactLinePrefixSharesOneDirectValueStream()
    {
        var regex = new Utf8Pcre2Regex("^ERROR: .+$", Pcre2CompileOptions.Multiline);
        var input = Encoding.UTF8.GetBytes(
            "INFO: boot\nERROR: first\nERROR: \nERROR: \r\nERROR: café\n");

        Assert.Equal(
            "IsMatch=Pcre2MultilinePrefix, Count=Pcre2MultilinePrefix, Enumerate=Pcre2MultilinePrefix, Match=Pcre2Slice, Replace=Pcre2Slice",
            regex.DebugDescribeExecutionPlan());
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

        Assert.IsType<Pcre2MultilinePrefixDirectProgram>(regex.DebugCompiledProgram.Operations.Count);
        Assert.Equal(2, regex.Count(input, 1));
        Assert.Equal(2, regex.Count(input, secondMatchStart));
        Assert.Equal("ERROR: two\r", regex.Match(input, secondMatchStart).GetValueString());
        Assert.True(regex.IsMatch(input, secondMatchStart, Pcre2MatchOptions.Anchored));
        Assert.False(regex.IsMatch(input, secondMatchStart + 1, Pcre2MatchOptions.Anchored));
    }

    [Fact]
    public void ExactLinePrefixFallsBackToTheVmForMeteredExecution()
    {
        var regex = new Utf8Pcre2Regex(
            "^ERROR: .+$",
            Pcre2CompileOptions.Multiline,
            default,
            new Utf8Pcre2ExecutionLimits { MatchLimit = 1 },
            Regex.InfiniteMatchTimeout);

        Assert.IsType<Pcre2MultilinePrefixDirectProgram>(regex.DebugCompiledProgram.Operations.IsMatch);
        Assert.Equal(
            Pcre2ErrorKind.MatchLimit,
            Assert.Throws<Pcre2MatchException>(() => regex.IsMatch("INFO: ok\nERROR: boom"u8)).ErrorKind);
    }

    [Fact]
    public void ExactLinePrefixMatchesEquivalentOwnedAcrossAsciiOffsets()
    {
        var direct = new Utf8Pcre2Regex("^ERROR: .+$", Pcre2CompileOptions.Multiline);
        var owned = new Utf8Pcre2Regex("^(ERROR: ).+$", Pcre2CompileOptions.Multiline);
        Assert.IsType<Pcre2SliceDirectProgram>(owned.DebugCompiledProgram.Operations.Count);

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
                Assert.Equal(owned.IsMatch(input, start), direct.IsMatch(input, start));
                Assert.Equal(owned.Count(input, start), direct.Count(input, start));

                var directRanges = new List<(int Start, int End)>();
                var directMatches = direct.EnumerateMatches(input, start);
                while (directMatches.MoveNext())
                {
                    directRanges.Add((
                        directMatches.Current.StartOffsetInBytes,
                        directMatches.Current.EndOffsetInBytes));
                }

                var ownedRanges = new List<(int Start, int End)>();
                var ownedMatches = owned.EnumerateMatches(input, start);
                while (ownedMatches.MoveNext())
                {
                    ownedRanges.Add((
                        ownedMatches.Current.StartOffsetInBytes,
                        ownedMatches.Current.EndOffsetInBytes));
                }

                Assert.Equal(ownedRanges, directRanges);
            }
        }
    }

    [Fact]
    public void ExactLinePrefixAnalyzerRejectsDifferentSemantics()
    {
        var explicitCr = new Utf8Pcre2Regex(
            "^ERROR: .+$",
            Pcre2CompileOptions.Multiline,
            new Utf8Pcre2CompileSettings { Newline = Pcre2NewlineConvention.Cr },
            default(Utf8Pcre2ExecutionLimits),
            Regex.InfiniteMatchTimeout);

        Assert.IsType<Pcre2BacktrackingDirectProgram>(explicitCr.DebugCompiledProgram.Operations.Count);
        // R03 anchor slice: star, lazy, and DotAll line shapes decline the
        // prefix program in the analyzer but land on the shared slice
        // runner instead of backtracking. None route MultilinePrefix.
        Assert.IsType<Pcre2SliceDirectProgram>(
            new Utf8Pcre2Regex("^ERROR: .*$", Pcre2CompileOptions.Multiline)
                .DebugCompiledProgram.Operations.Count);
        Assert.IsType<Pcre2SliceDirectProgram>(
            new Utf8Pcre2Regex("^ERROR: .+?$", Pcre2CompileOptions.Multiline)
                .DebugCompiledProgram.Operations.Count);
        Assert.IsType<Pcre2BacktrackingDirectProgram>(
            new Utf8Pcre2Regex("^ÉCHEC: .+$", Pcre2CompileOptions.Multiline)
                .DebugCompiledProgram.Operations.Count);
        // The explicit-LF shape joins the slice set now that bare C0
        // control escapes route owned (core IsMatch/Count parity is
        // pinned by ExplicitLfLineShapeMatchesOracleOnSlice below).
        Assert.IsType<Pcre2SliceDirectProgram>(
            new Utf8Pcre2Regex("^ERROR\\n.+$", Pcre2CompileOptions.Multiline)
                .DebugCompiledProgram.Operations.Count);
        Assert.IsType<Pcre2SliceDirectProgram>(
            new Utf8Pcre2Regex(
                "^ERROR: .+$",
                Pcre2CompileOptions.Multiline | Pcre2CompileOptions.DotAll)
                .DebugCompiledProgram.Operations.Count);
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
