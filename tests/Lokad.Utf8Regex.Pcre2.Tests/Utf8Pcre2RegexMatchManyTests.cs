using Lokad.Utf8Regex.Pcre2;

namespace Lokad.Utf8Regex.Pcre2.Tests;

public sealed class Utf8Pcre2RegexMatchManyTests
{
    [Fact]
    public void MatchManyCanYieldThenPropagateSneakyLookbehindRuntimeError()
    {
        var regex = new Utf8Pcre2Regex("a|(?(DEFINE)(?<sneaky>\\Ka))(?<=(?&sneaky))b");
        Span<Utf8Pcre2MatchData> destination = stackalloc Utf8Pcre2MatchData[2];

        Pcre2MatchException? exception = null;
        try
        {
            _ = regex.MatchMany("ab"u8, destination, out _);
        }
        catch (Pcre2MatchException ex)
        {
            exception = ex;
        }

        Assert.NotNull(exception);
        Assert.Equal(Pcre2ErrorKinds.DisallowedLookaroundBackslashK, exception.ErrorKind);
        Assert.True(destination[0].Success);
        Assert.Equal(0, destination[0].StartOffsetInBytes);
        Assert.Equal(1, destination[0].EndOffsetInBytes);
    }

    [Fact]
    public void MatchManyShortDestinationDoesNotProbePastFirstSneakyMatch()
    {
        var regex = new Utf8Pcre2Regex("a|(?(DEFINE)(?<sneaky>\\Ka))(?<=(?&sneaky))b");
        Span<Utf8Pcre2MatchData> destination = stackalloc Utf8Pcre2MatchData[1];

        var written = regex.MatchMany("ab"u8, destination, out var isMore);

        Assert.Equal(1, written);
        Assert.True(isMore);
        Assert.True(destination[0].Success);
        Assert.Equal(0, destination[0].StartOffsetInBytes);
        Assert.Equal(1, destination[0].EndOffsetInBytes);
    }

    [Fact]
    public void MatchManySupportsMonotoneSneakyGlobalLookbehind()
    {
        var regex = new Utf8Pcre2Regex("a|(?(DEFINE)(?<sneaky>\\K\\Ga))(?<=(?&sneaky))b");
        Span<Utf8Pcre2MatchData> destination = stackalloc Utf8Pcre2MatchData[2];

        var written = regex.MatchMany("ab"u8, destination, out var isMore);

        Assert.Equal(1, written);
        Assert.False(isMore);
        Assert.True(destination[0].Success);
        Assert.Equal(0, destination[0].StartOffsetInBytes);
        Assert.Equal(1, destination[0].EndOffsetInBytes);
    }
}
