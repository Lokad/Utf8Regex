using System.Text;
using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex.Pcre2.Tests;

public sealed class Pcre2AsciiOrderedLiteralWindowCompilerTests
{
    [Fact]
    public void SymmetricOrderedWindowReusesPreparedCountOnly()
    {
        var regex = new Utf8Pcre2Regex("Holmes.{0,25}Watson|Watson.{0,25}Holmes");
        var input = "Holmes and Watson\nWatson near Holmes\nHolmes\nWatson\nHolmesxxxxxxxxxxxxxxxxxxxxxxxxxWatson"u8;

        Assert.Equal(
            "IsMatch=Pcre2Backtracking, Count=Pcre2AsciiOrderedLiteralWindow, Enumerate=Pcre2Backtracking, Match=Pcre2Backtracking, Replace=Pcre2Backtracking",
            regex.DebugDescribeExecutionPlan());
        Assert.True(regex.IsMatch(input));
        Assert.Equal(3, regex.Count(input));
        Assert.Equal("Holmes and Watson", regex.Match(input).GetValueString());

        var values = new List<string>();
        var matches = regex.EnumerateMatches(input);
        while (matches.MoveNext())
        {
            values.Add(matches.Current.GetValueString());
        }

        Assert.Equal(
            ["Holmes and Watson", "Watson near Holmes", "HolmesxxxxxxxxxxxxxxxxxxxxxxxxxWatson"],
            values);
        Assert.Equal(
            "<Holmes and Watson>\n<Watson near Holmes>\nHolmes\nWatson\n<HolmesxxxxxxxxxxxxxxxxxxxxxxxxxWatson>",
            regex.ReplaceToString(input, "<$0>"));
    }

    [Fact]
    public void IndependentOrderedWindowShapeUsesTheSameProof()
    {
        var regex = new Utf8Pcre2Regex("Tom.{10,25}river|river.{10,25}Tom");
        var input = "Tom 1234567890 river\nriver 1234567890 Tom"u8;

        Assert.IsType<Pcre2AsciiOrderedLiteralWindowDirectProgram>(
            regex.DebugCompiledProgram.Operations.Count);
        Assert.Equal(2, regex.Count(input));
    }

    [Fact]
    public void OrderedWindowMatchesEquivalentVmAcrossAsciiOffsets()
    {
        var direct = new Utf8Pcre2Regex("Holmes.{0,25}Watson|Watson.{0,25}Holmes");
        var vm = new Utf8Pcre2Regex("(Holmes).{0,25}Watson|(Watson).{0,25}Holmes");
        Assert.IsType<Pcre2BacktrackingDirectProgram>(vm.DebugCompiledProgram.Operations.Count);

        foreach (var inputText in new[]
                 {
                     string.Empty,
                     "Holmes Watson",
                     "x Holmes one Watson y Watson two Holmes z",
                     "Holmes\nWatson Watson\rHolmes",
                     "Holmes a\0b Watson HolmesxxxxxxxxxxxxxxxxxxxxxxxxxxWatson",
                 })
        {
            var input = Encoding.UTF8.GetBytes(inputText);
            for (var start = 0; start <= input.Length; start++)
            {
                Assert.Equal(vm.Count(input, start), direct.Count(input, start));
            }
        }
    }

    [Fact]
    public void OrderedWindowFallsBackForSupplementaryAndMeteredSubjects()
    {
        var unicode = new Utf8Pcre2Regex("Holmes.{0,25}Watson|Watson.{0,25}Holmes");
        Assert.IsType<Pcre2AsciiOrderedLiteralWindowDirectProgram>(
            unicode.DebugCompiledProgram.Operations.Count);
        Assert.Equal(1, unicode.Count("HolmeséWatson"u8));
        var unicodeInput = Encoding.UTF8.GetBytes($"Holmes{string.Concat(Enumerable.Repeat("😀", 25))}Watson");
        Assert.Equal(1, unicode.Count(unicodeInput));
        Assert.Equal(Encoding.UTF8.GetString(unicodeInput), unicode.Match(unicodeInput).GetValueString());
        Assert.Equal(0, unicode.Count("xHolmes and Watson"u8, 0, Pcre2MatchOptions.Anchored));
        Assert.Equal(1, unicode.Count("xHolmes and Watson"u8, 1, Pcre2MatchOptions.Anchored));

        var metered = new Utf8Pcre2Regex(
            "Holmes.{0,25}Watson|Watson.{0,25}Holmes",
            Pcre2CompileOptions.None,
            default,
            new Utf8Pcre2ExecutionLimits { MatchLimit = 1 },
            Regex.InfiniteMatchTimeout);
        Assert.IsType<Pcre2AsciiOrderedLiteralWindowDirectProgram>(
            metered.DebugCompiledProgram.Operations.Count);
        Assert.Equal(
            Pcre2ErrorKind.MatchLimit,
            Assert.Throws<Pcre2MatchException>(() => metered.Count("Holmes and Watson"u8)).ErrorKind);
    }

    [Fact]
    public void OrderedWindowAnalyzerRejectsDifferentSemantics()
    {
        var explicitCr = new Utf8Pcre2Regex(
            "Holmes.{0,25}Watson|Watson.{0,25}Holmes",
            Pcre2CompileOptions.None,
            new Utf8Pcre2CompileSettings { Newline = Pcre2NewlineConvention.Cr },
            default,
            Regex.InfiniteMatchTimeout);

        Assert.IsType<Pcre2BacktrackingDirectProgram>(explicitCr.DebugCompiledProgram.Operations.Count);
        Assert.IsType<Pcre2BacktrackingDirectProgram>(
            new Utf8Pcre2Regex("Holmes.{0,25}?Watson|Watson.{0,25}?Holmes")
                .DebugCompiledProgram.Operations.Count);
        Assert.IsType<Pcre2BacktrackingDirectProgram>(
            new Utf8Pcre2Regex("Holmes.*Watson|Watson.*Holmes")
                .DebugCompiledProgram.Operations.Count);
        Assert.IsType<Pcre2BacktrackingDirectProgram>(
            new Utf8Pcre2Regex("Hólmes.{0,25}Watson|Watson.{0,25}Hólmes")
                .DebugCompiledProgram.Operations.Count);
        Assert.IsType<Pcre2BacktrackingDirectProgram>(
            new Utf8Pcre2Regex(
                "Holmes.{0,25}Watson|Watson.{0,25}Holmes",
                Pcre2CompileOptions.Caseless)
                .DebugCompiledProgram.Operations.Count);
    }
}
