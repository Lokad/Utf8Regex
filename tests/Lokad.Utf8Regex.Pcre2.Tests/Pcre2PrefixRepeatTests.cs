using System.Text;
using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex.Pcre2.Tests;

public sealed class Pcre2PrefixRepeatTests
{
    [Fact]
    public void CapturedPrefixAndRepeatKeepPublicValueAndOutputSemantics()
    {
        var regex = new Utf8Pcre2Regex("(a)b+");
        var input = "ab abb abbb x ab"u8;
        Assert.True(regex.IsMatch(input));
        Assert.Equal(4, regex.Count(input));

        var ranges = new List<(int Start, int End)>();
        var matches = regex.EnumerateMatches(input);
        while (matches.MoveNext())
        {
            ranges.Add((matches.Current.StartOffsetInBytes, matches.Current.EndOffsetInBytes));
        }

        Assert.Equal([(0, 2), (3, 6), (7, 11), (14, 16)], ranges);


        Assert.Equal("<ab> <abb> <abbb> x <ab>", System.Text.Encoding.UTF8.GetString(regex.Replace(input, "<$0>")));
    }

    [Theory]
    [InlineData("(a)b+", "é ab abb ax abbb 🙂ab")]
    [InlineData("xyc{2,4}", "xyc xycxyccxycccccxxyccc")]
    [InlineData("xyc{2,4}?", "xyccccc xyc xycc")]
    [InlineData("(ab)c*", "ababcabccccx")]
    [InlineData("((ab))c++", "abcc x abc ab")]
    [InlineData("(a)(b)c+", "abc abcc x abccc")]
    public void PrefixRepeatsMatchAnIndependentOracleAtValidStarts(string pattern, string inputText)
    {
        var direct = new Utf8Pcre2Regex(pattern);
        var vm = new Utf8Pcre2Regex(            pattern, options:             Pcre2CompileOptions.None, compileSettings:             default, policy: new Utf8RegexPolicy
        {
            ResourcePolicy = new Utf8ResourcePolicy
            {
                MaxWorkSteps = 1_000_000
            },
            MatchTimeout = Regex.InfiniteMatchTimeout
        });

        var oracle = new Regex(pattern.Replace("c++", "(?>c+)", StringComparison.Ordinal), RegexOptions.CultureInvariant);
        var input = Encoding.UTF8.GetBytes(inputText);
        for (var start = 0; start <= input.Length; start++)
        {
            if (start < input.Length && (input[start] & 0xC0) == 0x80)
            {
                continue;
            }

            var expected = oracle.Matches(inputText, Encoding.UTF8.GetCharCount(input.AsSpan(0, start)));
            var ranges = expected.Select(m => (
                Encoding.UTF8.GetByteCount(inputText.AsSpan(0, m.Index)),
                Encoding.UTF8.GetByteCount(inputText.AsSpan(0, m.Index + m.Length)))).ToList();
            Assert.Equal(expected.Count != 0, direct.IsMatch(input, start));
            Assert.Equal(expected.Count, direct.Count(input, start));
            Assert.Equal(expected.Count, vm.Count(input, start));
            Assert.Equal(ranges, CollectRanges(direct, input, start));
            Assert.Equal(ranges, CollectRanges(vm, input, start));
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
    public void PrefixRepeatsObserveOptionsAndLimits()
    {
        const string Pattern = "(a)b+";
        var input = "ab abb abbb x ab"u8.ToArray();
        var direct = new Utf8Pcre2Regex(Pattern);
        var vm = new Utf8Pcre2Regex(            Pattern, options:             Pcre2CompileOptions.None, compileSettings:             default, policy: new Utf8RegexPolicy
        {
            ResourcePolicy = new Utf8ResourcePolicy
            {
                MaxWorkSteps = 1_000_000
            },
            MatchTimeout = Regex.InfiniteMatchTimeout
        });

        foreach (var options in new[]
                 {
                     Pcre2MatchOptions.Anchored,
                     Pcre2MatchOptions.NotEmpty,
                     Pcre2MatchOptions.EndAnchored,
                 })
        {
            Assert.Equal(vm.IsMatch(input, 3, options), direct.IsMatch(input, 3, options));
            Assert.Equal(vm.Count(input, 3, options), direct.Count(input, 3, options));
        }

        var tightlyMetered = new Utf8Pcre2Regex(            Pattern, options:             Pcre2CompileOptions.None, compileSettings:             default, policy: new Utf8RegexPolicy
        {
            ResourcePolicy = new Utf8ResourcePolicy
            {
                MaxWorkSteps = 1
            },
            MatchTimeout = Regex.InfiniteMatchTimeout
        });
        Assert.Equal(
            Pcre2ErrorKind.MatchLimit,
            Assert.Throws<Pcre2MatchException>(() => tightlyMetered.Count(input)).ErrorKind);
    }
}
