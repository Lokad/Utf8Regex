using System.Text;
using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex.Pcre2.Tests;

public sealed class Pcre2AnchoredDotSearchTests
{
    private const string FtpLinePattern = @"^([0-9]+)(\-| |$)(.*)$";

    [Fact]
    public void AnchoredDefaultDotPreservesBooleanAndDetailedResults()
    {
        var regex = new Utf8Pcre2Regex(FtpLinePattern);

        Assert.Equal(
            "IsMatch=Pcre2Slice, Count=Pcre2Slice, Enumerate=Pcre2Slice, Match=Pcre2Slice, Replace=Pcre2Slice",
            regex.DebugDescribeExecutionPlan());
        Assert.True(regex.IsMatch("123-file listing"u8));
        Assert.True(regex.IsMatch("123 αβ"u8));
        Assert.True(regex.IsMatch("123\n"u8));
        Assert.False(regex.IsMatch("123-contains\nembedded"u8));

        var detailed = regex.MatchDetailed("123-file listing"u8);
        Assert.True(detailed.Success);
        Assert.Equal("123", System.Text.Encoding.UTF8.GetString(detailed.GetGroup(1).GetValueBytes()));
        Assert.Equal("-", System.Text.Encoding.UTF8.GetString(detailed.GetGroup(2).GetValueBytes()));
        Assert.Equal("file listing", System.Text.Encoding.UTF8.GetString(detailed.GetGroup(3).GetValueBytes()));
    }

    [Fact]
    public void ExplicitZeroOffsetKeepsTheSubjectSliceAndByteStartContract()
    {
        var regex = new Utf8Pcre2Regex(FtpLinePattern);
        foreach (var text in new[] { "123-file listing", "123-\u00e9\u4e2d\U0001f600", "abc-" + new string('\u00e9', 131072), "abc-" + new string('x', 262144) })
        {
            var bytes = Encoding.UTF8.GetBytes(text);
            byte[] storage = [0xff, .. bytes, 0xff];
            var view = storage.AsSpan(1, bytes.Length);
            Assert.Equal(text.StartsWith("123-", StringComparison.Ordinal), regex.IsMatch(view, 0));
            Assert.Equal(regex.IsMatch(view), regex.IsMatch(view, 0));
        }
    }

    [Fact]
    public void AnchoredDefaultDotMatchesAnIndependentOracleAtValidStarts()
    {
        var direct = new Utf8Pcre2Regex(FtpLinePattern);
        var vm = CreateMetered(FtpLinePattern, 1_000_000);
        var oracle = new Regex(FtpLinePattern.Replace(@"\d", "[0-9]", StringComparison.Ordinal), RegexOptions.CultureInvariant);

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
    public void AnchoredDefaultDotObservesOptionsAndLimits()
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
    }

    private static Utf8Pcre2Regex CreateMetered(string pattern, uint matchLimit) =>
        new(
            pattern,
            Pcre2CompileOptions.None,
            default,
            new Utf8Pcre2ExecutionLimits { MatchLimit = matchLimit },
            Regex.InfiniteMatchTimeout);
}
