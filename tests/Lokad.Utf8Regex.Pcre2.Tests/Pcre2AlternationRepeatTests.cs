using System.Text;
using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex.Pcre2.Tests;

public sealed class Pcre2AlternationRepeatTests
{
    [Fact]
    public void ControlledCountRejectsMissingFinalByteAfterTheRequestedStart()
    {
        const string pattern = "(?:ab|a)+z";
        var subject = Encoding.UTF8.GetBytes("😀z " + string.Concat(Enumerable.Repeat("ababax ", 1024)));
        var policy = new Utf8RegexPolicy { ResourcePolicy = new() { MaxDepth = 4096 } };
        using var regex = new Utf8Pcre2Regex(pattern, policy: policy);
        using var source = new CancellationTokenSource();
        Assert.Equal(0, regex.Count(subject, "😀z "u8.Length, cancellationToken: source.Token));

        using var tight = new Utf8Pcre2Regex(pattern, policy: new Utf8RegexPolicy
        {
            ResourcePolicy = new() { MaxDepth = 4096 }
        });
    }

    [Theory]
    [InlineData("(?:ab|a)+z", "ababax ")]
    [InlineData("(?:ab|a|bc)+z", "abcz ")]
    [InlineData("(?:(?:ab|a)+y|(?:ab|a)+z)", "abay ")]
    [InlineData("(?:(?:ab|a)+é|(?:ab|a)+z)", "abaé ")]
    public void FinalByteSearchKeepsEverySuccessfulAlternative(string pattern, string token)
    {
        var text = "😀 " + string.Concat(Enumerable.Repeat(token, 128));
        var subject = Encoding.UTF8.GetBytes(text);
        using var regex = new Utf8Pcre2Regex(pattern);
        using var core = new Utf8Regex(pattern);
        var count = Regex.Count(text, pattern, RegexOptions.CultureInvariant, Regex.InfiniteMatchTimeout);
        Assert.Equal(count, regex.Count(subject));
        Assert.Equal(count, core.Count(subject));
    }

    [Fact]
    public void GreedyAlternationRewindsToTheLeftmostStartUnderLimits()
    {
        using var regex = new Utf8Pcre2Regex("(?<run>(?:ab|a|bc)+)z", policy: new Utf8RegexPolicy
        {
            ResourcePolicy = new() { MaxDepth = 32, MaxWorkingBytes = 4_096 }
        });
        using var source = new CancellationTokenSource();
        Span<Utf8Pcre2CaptureRange> captures = stackalloc Utf8Pcre2CaptureRange[2];
        var result = regex.Match("😀 abcz!"u8, startOffsetInBytes: 5, captures: captures, cancellationToken: source.Token);
        Assert.True(result.Success);
        Assert.Equal((5, 9), (result.StartOffsetInBytes, result.EndOffsetInBytes));
        Assert.True(captures[1].Success);
        Assert.Equal((5, 8), (captures[1].StartOffsetInBytes, captures[1].EndOffsetInBytes));
    }

    [Fact]
    public void AlternationRepeatsKeepPublicValuesAndOutputs()
    {
        var regex = new Utf8Pcre2Regex("(?:ab|a)+z");
        var input = Encoding.UTF8.GetBytes("az abz aabz ababaz abax éababaz夏");
        Assert.Equal(5, regex.Count(input));

        var values = new List<string>();
        var matches = regex.EnumerateMatches(input);
        while (matches.MoveNext())
        {
            values.Add(System.Text.Encoding.UTF8.GetString(matches.Current.GetValueBytes()));
        }

        Assert.Equal(["az", "abz", "aabz", "ababaz", "ababaz"], values);
        Assert.Equal("<az> <abz> <aabz> <ababaz> abax é<ababaz>夏", System.Text.Encoding.UTF8.GetString(regex.Replace(input, "<$0>"u8)));
    }

    [Fact]
    public void LiteralAlternationRepeatMatchesEquivalentVmAcrossAsciiOffsets()
    {
        var direct = new Utf8Pcre2Regex("(?:ab|a)+z");
        // The capturing comparison shape now rides the slice, so the VM side spells its group Python-style (the core slice declines it): identical language, generic-VM vehicle.
        var vm = new Utf8Pcre2Regex("(?P<x>(?:ab|a))+z");
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
    }

    [Theory]
    [InlineData("(?:a|aa)+a", "(?P<x>(?:a|aa))+a")]
    [InlineData("(?:aa|a)+a", "(?P<x>(?:aa|a))+a")]
    [InlineData("(?:ab|a)+ab", "(?P<x>(?:ab|a))+ab")]
    [InlineData("(?:foo|f|bar)+baz", "(?P<x>(?:foo|f|bar))+baz")]
    public void LiteralAlternationRepeatPreservesGreedyAlternativeOrdering(
        string directPattern,
        string vmPattern)
    {
        var direct = new Utf8Pcre2Regex(directPattern);
        var vm = new Utf8Pcre2Regex(vmPattern);
        var input = Encoding.UTF8.GetBytes("aaaaaa abaabab foofoobarbaz miss éfoofoobarbaz夏");
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

    [Theory]
    [InlineData("(?:a|ab){2,3}", "xaabab")]
    [InlineData("(?:a|ab){2,3}", "xaabab\U0001F600")]
    [InlineData("(?:a|ab){2,3}", "aaba")]
    [InlineData("(?:ab|a){2,3}", "xaabab")]
    [InlineData("(?:a|ab){2}", "xaab")]
    [InlineData("(?:a|ab){2,3}c", "xaababc")]
    public void RangedDivergentRepeatMatchesOrderedSpans(string pattern, string inputText)
    {
        // R05: greedy ranged counts over mixed-derivation bodies decline the
        // finite-literal program, so bare ranged rows enumerate ordered
        // first-success spans identical to the oracle, like exact counts and
        // trailing-context ranged counts.
        var direct = new Utf8Pcre2Regex(pattern, options: Pcre2CompileOptions.None);
        var oracle = new Regex(pattern, RegexOptions.CultureInvariant, Regex.InfiniteMatchTimeout);
        var input = Encoding.UTF8.GetBytes(inputText);
        Assert.Equal(oracle.Match(inputText).Value, System.Text.Encoding.UTF8.GetString(direct.Match(input).GetValueBytes()));
        var values = new System.Collections.Generic.List<string>();
        var matches = direct.EnumerateMatches(input);
        while (matches.MoveNext())
        {
            values.Add(System.Text.Encoding.UTF8.GetString(matches.Current.GetValueBytes()));
        }
        var wanted = new System.Collections.Generic.List<string>();
        foreach (Match m in oracle.Matches(inputText))
        {
            wanted.Add(m.Value);
        }
        Assert.Equal(wanted, values);
    }
}
