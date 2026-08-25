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
            "IsMatch=Pcre2MultilinePrefix, Count=Pcre2MultilinePrefix, Enumerate=Pcre2MultilinePrefix, Match=Pcre2Backtracking, Replace=Pcre2Backtracking",
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
            default,
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
    public void ExactLinePrefixMatchesEquivalentVmAcrossAsciiOffsets()
    {
        var direct = new Utf8Pcre2Regex("^ERROR: .+$", Pcre2CompileOptions.Multiline);
        var vm = new Utf8Pcre2Regex("^(ERROR: ).+$", Pcre2CompileOptions.Multiline);
        Assert.IsType<Pcre2BacktrackingDirectProgram>(vm.DebugCompiledProgram.Operations.Count);

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
                Assert.Equal(vm.IsMatch(input, start), direct.IsMatch(input, start));
                Assert.Equal(vm.Count(input, start), direct.Count(input, start));

                var directRanges = new List<(int Start, int End)>();
                var directMatches = direct.EnumerateMatches(input, start);
                while (directMatches.MoveNext())
                {
                    directRanges.Add((
                        directMatches.Current.StartOffsetInBytes,
                        directMatches.Current.EndOffsetInBytes));
                }

                var vmRanges = new List<(int Start, int End)>();
                var vmMatches = vm.EnumerateMatches(input, start);
                while (vmMatches.MoveNext())
                {
                    vmRanges.Add((
                        vmMatches.Current.StartOffsetInBytes,
                        vmMatches.Current.EndOffsetInBytes));
                }

                Assert.Equal(vmRanges, directRanges);
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
            default,
            Regex.InfiniteMatchTimeout);

        Assert.IsType<Pcre2BacktrackingDirectProgram>(explicitCr.DebugCompiledProgram.Operations.Count);
        Assert.IsType<Pcre2BacktrackingDirectProgram>(
            new Utf8Pcre2Regex("^ERROR: .*$", Pcre2CompileOptions.Multiline)
                .DebugCompiledProgram.Operations.Count);
        Assert.IsType<Pcre2BacktrackingDirectProgram>(
            new Utf8Pcre2Regex("^ERROR: .+?$", Pcre2CompileOptions.Multiline)
                .DebugCompiledProgram.Operations.Count);
        Assert.IsType<Pcre2BacktrackingDirectProgram>(
            new Utf8Pcre2Regex("^ÉCHEC: .+$", Pcre2CompileOptions.Multiline)
                .DebugCompiledProgram.Operations.Count);
        Assert.IsType<Pcre2BacktrackingDirectProgram>(
            new Utf8Pcre2Regex("^ERROR\\n.+$", Pcre2CompileOptions.Multiline)
                .DebugCompiledProgram.Operations.Count);
        Assert.IsType<Pcre2BacktrackingDirectProgram>(
            new Utf8Pcre2Regex(
                "^ERROR: .+$",
                Pcre2CompileOptions.Multiline | Pcre2CompileOptions.DotAll)
                .DebugCompiledProgram.Operations.Count);
    }
}
