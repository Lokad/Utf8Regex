using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Pcre2.Tests;

public sealed class Pcre2AsciiRegularUnanchoredIsMatchCompilerTests
{
    private const string IpPattern = @"(?:(?:25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9])\.){3}(?:25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9])";

    [Fact]
    public void UnanchoredAsciiRegularUsesCoreOnlyForBooleanFacade()
    {
        var regex = new Utf8Pcre2Regex(IpPattern);
        var program = Assert.IsType<Pcre2AsciiRegularIsMatchDirectProgram>(
            regex.DebugCompiledProgram.Operations.IsMatch);

        Assert.False(program.SupportsPreparedOffsets);
        Assert.Equal(RegexOptions.None, program.Regex.Options);
        Assert.Equal(
            Utf8FallbackDirectFamilyKind.AsciiDottedDecimalQuadCount,
            program.Regex.Inspection.PreparedRegex.FallbackDirectFamily.Kind);
        Assert.Equal(
            "IsMatch=Pcre2AsciiRegularIsMatch, Count=Pcre2Backtracking, Enumerate=Pcre2Backtracking, Match=Pcre2Backtracking, Replace=Pcre2Backtracking",
            regex.DebugDescribeExecutionPlan());
        Assert.True(regex.IsMatch("prefix 192.168.001.001 suffix"u8));
        Assert.False(regex.IsMatch("prefix 999.999.999.999 suffix"u8));

        var detailed = regex.MatchDetailed("prefix 192.168.001.001 suffix"u8);
        Assert.True(detailed.Success);
        Assert.Equal("192.168.001.001", detailed.Value.GetValueString());
    }

    [Fact]
    public void UnanchoredAsciiRegularMatchesTheMeteredVmAtEveryValidStart()
    {
        var direct = new Utf8Pcre2Regex(IpPattern);
        var vm = CreateMetered(IpPattern, 1_000_000);

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
            Assert.Equal(vm.IsMatch(input), direct.IsMatch(input));

            for (var start = 0; start <= input.Length; start++)
            {
                if (start < input.Length && (input[start] & 0xC0) == 0x80)
                {
                    continue;
                }

                Assert.Equal(vm.IsMatch(input, start), direct.IsMatch(input, start));
            }
        }
    }

    [Fact]
    public void UnanchoredAsciiRegularRetainsVmForOptionsLimitsAndUnsafeClasses()
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

        Assert.IsNotType<Pcre2AsciiRegularIsMatchDirectProgram>(
            new Utf8Pcre2Regex(@"\d+\.\d+").DebugCompiledProgram.Operations.IsMatch);
        Assert.IsNotType<Pcre2AsciiRegularIsMatchDirectProgram>(
            new Utf8Pcre2Regex(@"[^.]+\.[^.]+").DebugCompiledProgram.Operations.IsMatch);
        Assert.IsNotType<Pcre2AsciiRegularIsMatchDirectProgram>(
            new Utf8Pcre2Regex(IpPattern, Pcre2CompileOptions.Ucp)
                .DebugCompiledProgram.Operations.IsMatch);

        var tightlyMetered = CreateMetered(IpPattern, 1);
        Assert.Equal(
            Pcre2ErrorKind.MatchLimit,
            Assert.Throws<Pcre2MatchException>(() => tightlyMetered.IsMatch(input)).ErrorKind);
        Assert.Throws<ArgumentException>(() => direct.IsMatch([0xFF]));
    }

    private static Utf8Pcre2Regex CreateMetered(string pattern, uint matchLimit) =>
        new(
            pattern,
            Pcre2CompileOptions.None,
            default,
            new Utf8Pcre2ExecutionLimits { MatchLimit = matchLimit },
            Regex.InfiniteMatchTimeout);
}
