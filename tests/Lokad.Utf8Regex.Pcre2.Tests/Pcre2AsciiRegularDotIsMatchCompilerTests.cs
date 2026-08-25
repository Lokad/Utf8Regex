using System.Text;
using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex.Pcre2.Tests;

public sealed class Pcre2AsciiRegularDotIsMatchCompilerTests
{
    private const string FtpLinePattern = @"^([0-9]+)(\-| |$)(.*)$";

    [Fact]
    public void AnchoredDefaultDotUsesPreparedIsMatchAndVmForDetailedOperations()
    {
        var regex = new Utf8Pcre2Regex(FtpLinePattern);

        Assert.Equal(
            "IsMatch=Pcre2AsciiRegularIsMatch, Count=Pcre2Backtracking, Enumerate=Pcre2Backtracking, Match=Pcre2Backtracking, Replace=Pcre2Backtracking",
            regex.DebugDescribeExecutionPlan());
        Assert.True(regex.IsMatch("123-file listing"u8));
        Assert.True(regex.IsMatch("123 αβ"u8));
        Assert.True(regex.IsMatch("123\n"u8));
        Assert.False(regex.IsMatch("123-contains\nembedded"u8));

        var detailed = regex.MatchDetailed("123-file listing"u8);
        Assert.True(detailed.Success);
        Assert.Equal("123", detailed.GetGroup(1).GetValueString());
        Assert.Equal("-", detailed.GetGroup(2).GetValueString());
        Assert.Equal("file listing", detailed.GetGroup(3).GetValueString());
    }

    [Fact]
    public void AnchoredDefaultDotMatchesTheMeteredVmAtEveryValidStart()
    {
        var direct = new Utf8Pcre2Regex(FtpLinePattern);
        var vm = CreateMetered(FtpLinePattern, 1_000_000);
        Assert.IsType<Pcre2AsciiRegularIsMatchDirectProgram>(direct.DebugCompiledProgram.Operations.IsMatch);
        Assert.IsType<Pcre2AsciiRegularIsMatchDirectProgram>(vm.DebugCompiledProgram.Operations.IsMatch);

        foreach (var inputText in new[]
                 {
                     "",
                     "123",
                     "123\n",
                     "123-",
                     "123-file listing",
                     "123 αβ",
                     "123-αβ\n",
                     "123-contains\nembedded",
                     "123\rrest",
                     "abc-file listing",
                     "prefix 123-file listing",
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
    public void AnchoredDefaultDotFallsBackOutsideTheProvenContract()
    {
        var direct = new Utf8Pcre2Regex(FtpLinePattern);
        var vm = CreateMetered(FtpLinePattern, 1_000_000);
        var input = "123-file listing"u8.ToArray();

        foreach (var options in new[]
                 {
                     Pcre2MatchOptions.Anchored,
                     Pcre2MatchOptions.EndAnchored,
                     Pcre2MatchOptions.NotBol,
                     Pcre2MatchOptions.NotEol,
                     Pcre2MatchOptions.NotEmpty,
                     Pcre2MatchOptions.NotEmptyAtStart,
                 })
        {
            Assert.Equal(vm.IsMatch(input, 0, options), direct.IsMatch(input, 0, options));
        }

        var tightlyMetered = CreateMetered(FtpLinePattern, 1);
        Assert.Equal(
            Pcre2ErrorKind.MatchLimit,
            Assert.Throws<Pcre2MatchException>(() => tightlyMetered.IsMatch(input)).ErrorKind);
        Assert.IsNotType<Pcre2AsciiRegularIsMatchDirectProgram>(
            new Utf8Pcre2Regex(@".+").DebugCompiledProgram.Operations.IsMatch);
        Assert.IsNotType<Pcre2AsciiRegularIsMatchDirectProgram>(
            new Utf8Pcre2Regex(FtpLinePattern, Pcre2CompileOptions.DotAll)
                .DebugCompiledProgram.Operations.IsMatch);
        Assert.IsNotType<Pcre2AsciiRegularIsMatchDirectProgram>(
            new Utf8Pcre2Regex(
                FtpLinePattern,
                Pcre2CompileOptions.None,
                new Utf8Pcre2CompileSettings { Newline = Pcre2NewlineConvention.Cr },
                default,
                Regex.InfiniteMatchTimeout)
                .DebugCompiledProgram.Operations.IsMatch);
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
