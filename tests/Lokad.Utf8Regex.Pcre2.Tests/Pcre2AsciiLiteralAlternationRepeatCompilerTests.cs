using System.Text;
using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex.Pcre2.Tests;

public sealed class Pcre2AsciiLiteralAlternationRepeatCompilerTests
{
    [Fact]
    public void LiteralAlternationRepeatReusesPreparedCountOnly()
    {
        var regex = new Utf8Pcre2Regex("(?:ab|a)+z");
        var input = Encoding.UTF8.GetBytes("az abz aabz ababaz abax éababaz夏");

        Assert.Equal(
            "IsMatch=Pcre2Backtracking, Count=Pcre2AsciiLiteralAlternationRepeat, Enumerate=Pcre2Backtracking, Match=Pcre2Backtracking, Replace=Pcre2Backtracking",
            regex.DebugDescribeExecutionPlan());
        Assert.Equal(5, regex.Count(input));

        var values = new List<string>();
        var matches = regex.EnumerateMatches(input);
        while (matches.MoveNext())
        {
            values.Add(matches.Current.GetValueString());
        }

        Assert.Equal(["az", "abz", "aabz", "ababaz", "ababaz"], values);
        Assert.Equal("<az> <abz> <aabz> <ababaz> abax é<ababaz>夏", regex.ReplaceToString(input, "<$0>"));
    }

    [Fact]
    public void LiteralAlternationRepeatMatchesEquivalentVmAcrossAsciiOffsets()
    {
        var direct = new Utf8Pcre2Regex("(?:ab|a)+z");
        var vm = new Utf8Pcre2Regex("(?:(ab)|(a))+z");
        Assert.IsType<Pcre2BacktrackingDirectProgram>(vm.DebugCompiledProgram.Operations.Count);

        foreach (var inputText in new[]
                 {
                     string.Empty,
                     "az abz aabz ababaz",
                     "abax aabbz baaz",
                     "xabababz y aaz z",
                     "ab\naz a\0z",
                 })
        {
            var input = Encoding.UTF8.GetBytes(inputText);
            for (var start = 0; start <= input.Length; start++)
            {
                Assert.Equal(vm.Count(input, start), direct.Count(input, start));
            }
        }

        var unicode = Encoding.UTF8.GetBytes("é ababaz 夏 az");
        Assert.Equal(vm.Count(unicode), direct.Count(unicode));
        Assert.Equal(vm.Count(unicode, "é "u8.Length), direct.Count(unicode, "é "u8.Length));

        Assert.Throws<ArgumentException>(() => direct.Count([0xFF, (byte)'a', (byte)'z']));
    }

    [Theory]
    [InlineData("(?:a|aa)+a", "(?:(a)|(aa))+a")]
    [InlineData("(?:aa|a)+a", "(?:(aa)|(a))+a")]
    [InlineData("(?:ab|a)+ab", "(?:(ab)|(a))+ab")]
    [InlineData("(?:foo|f|bar)+baz", "(?:(foo)|(f)|(bar))+baz")]
    public void LiteralAlternationRepeatPreservesGreedyAlternativeOrdering(
        string directPattern,
        string vmPattern)
    {
        var direct = new Utf8Pcre2Regex(directPattern);
        var vm = new Utf8Pcre2Regex(vmPattern);
        var input = Encoding.UTF8.GetBytes("aaaaaa abaabab foofoobarbaz miss éfoofoobarbaz夏");

        Assert.IsType<Pcre2AsciiLiteralAlternationRepeatDirectProgram>(
            direct.DebugCompiledProgram.Operations.Count);
        Assert.IsType<Pcre2BacktrackingDirectProgram>(vm.DebugCompiledProgram.Operations.Count);
        for (var start = 0; start <= input.Length; start++)
        {
            if (start < input.Length && (input[start] & 0xC0) == 0x80)
            {
                continue;
            }

            Assert.Equal(vm.Count(input, start), direct.Count(input, start));
        }
    }

    [Fact]
    public void LiteralAlternationRepeatCountIsAllocationFreeAfterWarmup()
    {
        var regex = new Utf8Pcre2Regex("(?:ab|a)+z");
        var input = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat("ababaz ", 1024)));

        for (var index = 0; index < 32; index++)
        {
            Assert.Equal(1024, regex.Count(input));
        }

        for (var index = 0; index < 256; index++)
        {
            _ = regex.Count(input);
        }

        var before = GC.GetAllocatedBytesForCurrentThread();
        var total = 0;
        for (var index = 0; index < 256; index++)
        {
            total += regex.Count(input);
        }

        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
        Assert.Equal(256 * 1024, total);
    }

    [Fact]
    public void LiteralAlternationRepeatFallsBackForOptionsAndLimits()
    {
        var regex = new Utf8Pcre2Regex("(?:ab|a)+z");
        Assert.Equal(0, regex.Count("xababaz"u8, 0, Pcre2MatchOptions.Anchored));
        Assert.Equal(1, regex.Count("xababaz"u8, 1, Pcre2MatchOptions.Anchored));

        var metered = new Utf8Pcre2Regex(
            "(?:ab|a)+z",
            Pcre2CompileOptions.None,
            default,
            new Utf8Pcre2ExecutionLimits { MatchLimit = 1 },
            Regex.InfiniteMatchTimeout);
        Assert.IsType<Pcre2AsciiLiteralAlternationRepeatDirectProgram>(
            metered.DebugCompiledProgram.Operations.Count);
        Assert.Equal(
            Pcre2ErrorKind.MatchLimit,
            Assert.Throws<Pcre2MatchException>(() => metered.Count("ababaz"u8)).ErrorKind);
    }

    [Fact]
    public void LiteralAlternationRepeatAnalyzerRejectsDifferentSemantics()
    {
        Assert.IsType<Pcre2BacktrackingDirectProgram>(
            new Utf8Pcre2Regex("(?:ab|a)+?z").DebugCompiledProgram.Operations.Count);
        Assert.IsType<Pcre2BacktrackingDirectProgram>(
            new Utf8Pcre2Regex("(?:ab|a)*z").DebugCompiledProgram.Operations.Count);
        Assert.IsType<Pcre2BacktrackingDirectProgram>(
            new Utf8Pcre2Regex("(?:ab|a)++z").DebugCompiledProgram.Operations.Count);
        Assert.IsType<Pcre2BacktrackingDirectProgram>(
            new Utf8Pcre2Regex("(?:áb|a)+z").DebugCompiledProgram.Operations.Count);
        Assert.IsType<Pcre2BacktrackingDirectProgram>(
            new Utf8Pcre2Regex("(?:(ab)|a)+z").DebugCompiledProgram.Operations.Count);
        Assert.IsType<Pcre2BacktrackingDirectProgram>(
            new Utf8Pcre2Regex("(?:ab|a)+z", Pcre2CompileOptions.Caseless)
                .DebugCompiledProgram.Operations.Count);
    }
}
