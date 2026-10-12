using System.Text;
using Lokad.Utf8Regex;
using Lokad.Utf8Regex.Pcre2;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Pcre2.Tests;

// A fast path succeeds while its deadline is live and reports Timeout when
// expired. Cold JIT and scheduling can legitimately exceed a real 1-ms budget.
public sealed class Pcre2FastPathTimeoutTests
{
    [Theory]
    [InlineData(0, 0)] [InlineData(0, 2)] [InlineData(0, 4)]
    [InlineData(1, 0)] [InlineData(1, 2)] [InlineData(1, 4)]
    [InlineData(2, 0)] [InlineData(2, 2)] [InlineData(2, 4)]
    [InlineData(3, 0)] [InlineData(3, 2)] [InlineData(3, 4)]
    [InlineData(4, 0)] [InlineData(4, 2)] [InlineData(4, 4)]
    [InlineData(5, 0)] [InlineData(5, 2)] [InlineData(5, 4)]
    public void FastOperationsRespectTheirActualDeadline(int operation, int expireAtRead)
    {
        var armed = false;
        var reads = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() =>
        {
            if (!armed) return 0;
            // Invocation starts at zero. Advance only when execution reads its
            // captured clock again, so expiry is independent of machine speed.
            reads++;
            return expireAtRead != 0 && reads >= expireAtRead ? 2 : 0;
        });
        var regex = new Utf8Pcre2Regex("abc", Pcre2CompileOptions.None, default, default(Utf8Pcre2ExecutionLimits), TimeSpan.FromMilliseconds(1));
        var input = Encoding.UTF8.GetBytes("xxabcxx");
        Assert.True(regex.Analyze().IsFullyNative);
        armed = true;
        if (expireAtRead != 0)
        {
            var failure = Assert.Throws<Pcre2MatchException>(() => Run(regex, input, operation));
            Assert.Equal(Pcre2ErrorKind.Timeout, failure.ErrorKind);
            Assert.True(reads >= expireAtRead);
        }
        else
        {
            Assert.Equal(1, Run(regex, input, operation));
            Assert.True(reads > 1);
        }
    }

    [Theory]
    [InlineData("", "", Pcre2MatchOptions.NotEmpty)]
    [InlineData(@"\Aabc", "abc", Pcre2MatchOptions.None)]
    [InlineData(@"\Gabc", "abc", Pcre2MatchOptions.None)]
    [InlineData(@"abc\z", "xxabc", Pcre2MatchOptions.None)]
    [InlineData("absent", "😀é", Pcre2MatchOptions.None)]
    public void ShortAnchoredEmptyAndTerminalMissesCheckBeforeReturning(string pattern, string text, Pcre2MatchOptions options)
    {
        var armed = false; var reads = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() =>
        {
            if (!armed) return 0;
            // Start, entry, then return; these shapes do not need a search block.
            return ++reads >= 3 ? 2 : 0;
        });
        var regex = new Utf8Pcre2Regex(pattern, Pcre2CompileOptions.None, default,
            default(Utf8Pcre2ExecutionLimits), TimeSpan.FromMilliseconds(1));
        var input = Encoding.UTF8.GetBytes(text);
        armed = true;
        var failure = Assert.Throws<Pcre2MatchException>(() => regex.Match(input, 0, options));
        Assert.Equal(Pcre2ErrorKind.Timeout, failure.ErrorKind);
        Assert.True(reads >= 3);
    }

    [Fact]
    public void MeteredLongSearchChecksEachExistingChunk()
    {
        var armed = false; var reads = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() =>
        {
            if (!armed) return 0;
            return ++reads >= 5 ? 2 : 0;
        });
        var regex = new Utf8Pcre2Regex("abc", Pcre2CompileOptions.None, default,
            default(Utf8Pcre2ExecutionLimits), TimeSpan.FromMilliseconds(1));
        var input = Encoding.UTF8.GetBytes(new string('x', 100000));
        armed = true;
        var failure = Assert.Throws<Pcre2MatchException>(() => regex.IsMatch(input));
        Assert.Equal(Pcre2ErrorKind.Timeout, failure.ErrorKind);
        Assert.Equal(5, reads);
    }

    [Theory]
    [InlineData(false, 5)] [InlineData(false, 7)]
    [InlineData(true, 5)] [InlineData(true, 7)]
    public void SingleResultProjectionCannotLoseTheLiteralDeadline(bool detailed, int expireAtRead)
    {
        var armed = false; var reads = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() =>
        {
            if (!armed) return 0;
            return ++reads >= expireAtRead ? 2 : 0;
        });
        var regex = new Utf8Pcre2Regex("abc", Pcre2CompileOptions.None, default,
            default(Utf8Pcre2ExecutionLimits), TimeSpan.FromMilliseconds(1));
        armed = true;
        var failure = Assert.Throws<Pcre2MatchException>(() =>
        {
            if (detailed) _ = regex.MatchDetailed("xxabcxx"u8);
            else _ = regex.Match("xxabcxx"u8);
        });
        Assert.Equal(Pcre2ErrorKind.Timeout, failure.ErrorKind);
        Assert.True(reads >= expireAtRead);
    }

    [Fact]
    public void LiteralSingleProjectionJoinsTheExistingCandidateWorkAllowance()
    {
        var tight = new Utf8Pcre2Regex("abc", Pcre2CompileOptions.None, default,
            new Utf8Pcre2ExecutionLimits { MatchLimit = 1 }, Timeout.InfiniteTimeSpan);
        Assert.True(tight.IsMatch("xxabcxx"u8));
        var failure = Assert.Throws<Pcre2MatchException>(() => tight.Match("xxabcxx"u8));
        Assert.Equal(Pcre2ErrorKind.MatchLimit, failure.ErrorKind);
        var admitted = new Utf8Pcre2Regex("abc", Pcre2CompileOptions.None, default,
            new Utf8Pcre2ExecutionLimits { MatchLimit = 3 }, Timeout.InfiniteTimeSpan);
        var result = admitted.Match("xxabcxx"u8);
        Assert.True(result.Success); Assert.Equal(2, result.StartOffsetInBytes);
        Assert.Equal(5, result.EndOffsetInBytes); Assert.Equal(2, result.StartOffsetInUtf16);
    }

    private static int Run(Utf8Pcre2Regex regex, byte[] input, int operation)
    {
        switch (operation)
        {
            case 0: return regex.IsMatch(input) ? 1 : 0;
            case 1: return regex.Match(input).Success ? 1 : 0;
            case 2: return regex.Count(input);
            case 3:
                var destination = new Utf8Pcre2MatchData[8];
                var count = regex.MatchMany(input, destination, out var isMore);
                Assert.False(isMore);
                return count;
            case 4:
                var matches = 0;
                var enumerator = regex.EnumerateMatches(input);
                while (enumerator.MoveNext()) matches++;
                return matches;
            case 5: return regex.MatchDetailed(input).Success ? 1 : 0;
            default: throw new ArgumentOutOfRangeException(nameof(operation));
        }
    }
}
