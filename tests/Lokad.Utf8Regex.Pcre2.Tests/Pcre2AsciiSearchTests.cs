using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Input;

namespace Lokad.Utf8Regex.Pcre2.Tests;

public sealed class Pcre2AsciiSearchTests
{
    private const string IpPattern = @"(?:(?:25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9])\.){3}(?:25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9])";

    [Fact]
    public void UnanchoredAsciiGrammarKeepsBooleanAndCaptureResults()
    {
        var regex = new Utf8Pcre2Regex(IpPattern);
        Assert.True(regex.IsMatch("prefix 192.168.001.001 suffix"u8));
        Assert.False(regex.IsMatch("prefix 999.999.999.999 suffix"u8));

        var detailed = regex.Match("prefix 192.168.001.001 suffix"u8);
        Assert.True(detailed.Success);
        Assert.Equal("192.168.001.001", System.Text.Encoding.UTF8.GetString(detailed.GetValueBytes()));
    }

    [Fact]
    public void UnanchoredAsciiMatchesAnIndependentOracleAtValidStarts()
    {
        var direct = new Utf8Pcre2Regex(IpPattern);
        var vm = CreateMetered(IpPattern, 1_000_000);
        var oracle = new Regex(IpPattern.Replace(@"\d", "[0-9]", StringComparison.Ordinal), RegexOptions.CultureInvariant);

        foreach (var inputText in new[]
                 {
                     "",
                     "192.168.001.001",
                     "prefix 192.168.001.001 suffix",
                     "00.00.00.00",
                     "255.255.255.255",
                     "256.255.255.255",
                     "999.999.999.999",
                     "1.1.1.1",
                     "192.168.001.001\n",
                     "α192.168.001.001β",
                 })
        {
            var input = Encoding.UTF8.GetBytes(inputText);
            var expected = oracle.IsMatch(inputText);
            Assert.Equal(expected, direct.IsMatch(input));
            Assert.Equal(expected, vm.IsMatch(input));

            for (var start = 0; start <= input.Length; start++)
            {
                if (start < input.Length && (input[start] & 0xC0) == 0x80)
                {
                    continue;
                }

                var expectedAtStart = oracle.IsMatch(inputText, Encoding.UTF8.GetCharCount(input.AsSpan(0, start)));
                Assert.Equal(expectedAtStart, direct.IsMatch(input, start));
                Assert.Equal(expectedAtStart, vm.IsMatch(input, start));
            }
        }
    }

    [Fact]
    public void UnanchoredAsciiObservesOptionsAndLimits()
    {
        var direct = new Utf8Pcre2Regex(IpPattern);
        var vm = CreateMetered(IpPattern, 1_000_000);
        var input = "prefix 192.168.001.001 suffix"u8.ToArray();

        foreach (var options in new[]
                 {
                     Pcre2MatchOptions.Anchored,
                     Pcre2MatchOptions.EndAnchored,
                     Pcre2MatchOptions.NotEmpty,
                     Pcre2MatchOptions.NotEmptyAtStart,
                 })
        {
            Assert.Equal(vm.IsMatch(input, 0, options), direct.IsMatch(input, 0, options));
        }

        var tightlyMetered = CreateMetered(IpPattern, 1);
        Assert.Equal(
            Pcre2ErrorKind.MatchLimit,
            Assert.Throws<Pcre2MatchException>(() => tightlyMetered.IsMatch(input)).ErrorKind);
    }

    [Theory]
    [InlineData("abbb", true)]
    [InlineData("xx", false)]
    public void TokenOnlyFallbackPollsBetweenLeadingByteSearchBlocks(string tail, bool expected)
    {
        using var regex = CreateMetered("(a)b+", 0);
        using var cancellation = new CancellationTokenSource();
        const Pcre2MatchOptions options = Pcre2MatchOptions.NotEmpty;
        var shortInput = Encoding.UTF8.GetBytes("éx" + tail);
        var longInput = Encoding.UTF8.GetBytes("é" + new string('x', 8193) + tail);

        Assert.Equal(expected, regex.IsMatch(longInput, 2, options, cancellation.Token));
        var shortSteps = SearchSteps(shortInput);
        var longSteps = SearchSteps(longInput);
        Assert.InRange(longSteps - shortSteps, 4UL, 6UL);

        ulong SearchSteps(byte[] input)
        {
            var compiled = regex.DebugCompiledProgram;
            var budget = new Pcre2ResourceBudget(compiled.Request, cancellation.Token);
            Assert.True(Pcre2Runner.TryIsMatch(compiled, input, new Utf8BytePosition(2), options, ref budget, out var found));
            Assert.Equal(expected, found);
            return budget.WorkingInvocation.Budget.Steps;
        }
    }

    private static Utf8Pcre2Regex CreateMetered(string pattern, uint matchLimit) =>
        new(pattern, options: Pcre2CompileOptions.None, compileSettings: default, policy: new Utf8RegexPolicy
        {
            ResourcePolicy = new Utf8ResourcePolicy
            {
                MaxWorkSteps = (matchLimit) == 0 ? null : (matchLimit)
            },
            MatchTimeout = Regex.InfiniteMatchTimeout
        });
}
