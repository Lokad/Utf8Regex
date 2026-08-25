using System.Text;
using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex.Pcre2.Tests;

public sealed class Pcre2SameStartLiteralCompilerTests
{
    [Fact]
    public void EmptyFirstAlternativeSharesTheSameStartLiteralValueStream()
    {
        var regex = new Utf8Pcre2Regex("(?<=abc)(|def)");
        var input = "abcdefabc"u8;

        Assert.Equal(
            "IsMatch=Pcre2Backtracking, Count=Pcre2SameStartLiteral, Enumerate=Pcre2SameStartLiteral, Match=Pcre2Backtracking, Replace=Pcre2Backtracking",
            regex.DebugDescribeExecutionPlan());
        Assert.Equal(3, regex.Count(input));

        var ranges = new List<(int Start, int End)>();
        var matches = regex.EnumerateMatches(input);
        while (matches.MoveNext())
        {
            ranges.Add((matches.Current.StartOffsetInBytes, matches.Current.EndOffsetInBytes));
        }

        Assert.Equal([(3, 3), (3, 6), (9, 9)], ranges);

        Span<Utf8Pcre2MatchData> destination = stackalloc Utf8Pcre2MatchData[2];
        Assert.Equal(2, regex.MatchMany(input, destination, out var isMore));
        Assert.True(isMore);
        Assert.Equal((3, 3), (destination[0].StartOffsetInBytes, destination[0].EndOffsetInBytes));
        Assert.Equal((3, 6), (destination[1].StartOffsetInBytes, destination[1].EndOffsetInBytes));
    }

    [Fact]
    public void SameStartLiteralMatchesTheMeteredVmAtEveryValidStart()
    {
        const string Pattern = "(?<=abc)(|def|x)";
        var direct = new Utf8Pcre2Regex(Pattern);
        var vm = new Utf8Pcre2Regex(
            Pattern,
            Pcre2CompileOptions.None,
            default,
            new Utf8Pcre2ExecutionLimits { MatchLimit = 1_000_000 },
            Regex.InfiniteMatchTimeout);
        Assert.IsType<Pcre2SameStartLiteralDirectProgram>(direct.DebugCompiledProgram.Operations.Count);
        Assert.IsType<Pcre2SameStartLiteralDirectProgram>(vm.DebugCompiledProgram.Operations.Count);

        foreach (var inputText in new[]
                 {
                     string.Empty,
                     "abc",
                     "abcdefabc",
                     "zabcxabcdef",
                     "éabcdef🙂abcx",
                     "abc\0defabc",
                 })
        {
            var input = Encoding.UTF8.GetBytes(inputText);
            for (var start = 0; start <= input.Length; start++)
            {
                if (start < input.Length && (input[start] & 0xC0) == 0x80)
                {
                    continue;
                }

                Assert.Equal(vm.Count(input, start), direct.Count(input, start));
                Assert.Equal(CollectRanges(vm, input, start), CollectRanges(direct, input, start));
            }
        }

        static List<(int Start, int End)> CollectRanges(Utf8Pcre2Regex regex, byte[] input, int start)
        {
            var ranges = new List<(int Start, int End)>();
            var matches = regex.EnumerateMatches(input, start);
            while (matches.MoveNext())
            {
                ranges.Add((matches.Current.StartOffsetInBytes, matches.Current.EndOffsetInBytes));
            }

            return ranges;
        }
    }

    [Fact]
    public void SameStartLiteralFallsBackForMatchOptionsAndLimits()
    {
        const string Pattern = "(?<=abc)(|def)";
        var input = "abcdefabc"u8.ToArray();
        var direct = new Utf8Pcre2Regex(Pattern);
        var vm = new Utf8Pcre2Regex(
            Pattern,
            Pcre2CompileOptions.None,
            default,
            new Utf8Pcre2ExecutionLimits { MatchLimit = 1_000_000 },
            Regex.InfiniteMatchTimeout);

        Assert.Equal(
            vm.Count(input, 3, Pcre2MatchOptions.Anchored),
            direct.Count(input, 3, Pcre2MatchOptions.Anchored));
        Assert.Equal(
            vm.Count(input, 3, Pcre2MatchOptions.NotEmpty),
            direct.Count(input, 3, Pcre2MatchOptions.NotEmpty));

        var tightlyMetered = new Utf8Pcre2Regex(
            Pattern,
            Pcre2CompileOptions.None,
            default,
            new Utf8Pcre2ExecutionLimits { MatchLimit = 1 },
            Regex.InfiniteMatchTimeout);
        Assert.Equal(
            Pcre2ErrorKind.MatchLimit,
            Assert.Throws<Pcre2MatchException>(() => tightlyMetered.Count(input)).ErrorKind);
    }

    [Fact]
    public void SameStartLiteralAnalyzerRejectsDifferentSemantics()
    {
        foreach (var regex in new[]
                 {
                     new Utf8Pcre2Regex("(?<!abc)(|def)"),
                     new Utf8Pcre2Regex("(?<=abc)(def|)"),
                     new Utf8Pcre2Regex("(?<=é)(|def)"),
                     new Utf8Pcre2Regex("(?<=abc)(|(?<x>def))"),
                     new Utf8Pcre2Regex("(?<=abc)(|déf)"),
                     new Utf8Pcre2Regex("x(?<=abc)(|def)"),
                     new Utf8Pcre2Regex("(?<=abc)(def|ghi)"),
                     new Utf8Pcre2Regex("(?<=abc)(|def)", Pcre2CompileOptions.Caseless),
                 })
        {
            Assert.IsType<Pcre2BacktrackingDirectProgram>(regex.DebugCompiledProgram.Operations.Count);
            Assert.IsType<Pcre2BacktrackingDirectProgram>(regex.DebugCompiledProgram.Operations.Enumerate);
        }
    }
}
