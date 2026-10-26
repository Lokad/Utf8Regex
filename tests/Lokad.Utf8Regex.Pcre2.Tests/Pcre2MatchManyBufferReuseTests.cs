using System.Text;
using Lokad.Utf8Regex.Pcre2;

namespace Lokad.Utf8Regex.Pcre2.Tests;

// Caller-owned destination reuse (the managed ovector analog): only the
// [0..written) prefix is ever valid, and every call reports its own prefix
// exactly, regardless of what previous calls left in the buffer. Unlike the
// native PCRE.NET baseline buffer, no match storage lives on the instance,
// so reuse across calls and patterns cannot leak state. See the R08 pcre-net
// disposition in PLAN.md.
public sealed class Pcre2MatchManyBufferReuseTests
{
    [Fact]
    public void ReusedDestinationReportsExactPrefixPerCall()
    {
        var regex = new Utf8Pcre2Regex("ab");
        var destination = new Utf8Pcre2MatchData[4];

        var written = regex.MatchMany(Encoding.UTF8.GetBytes("ab ab ab"), destination, out var isMore);
        Assert.Equal(3, written);
        Assert.False(isMore);
        Assert.Equal(new[] { 0, 3, 6 }, new[] { destination[0].StartOffsetInBytes, destination[1].StartOffsetInBytes, destination[2].StartOffsetInBytes });

        written = regex.MatchMany(Encoding.UTF8.GetBytes("xxab"), destination, out isMore);
        Assert.Equal(1, written);
        Assert.False(isMore);
        Assert.True(destination[0].Success);
        Assert.Equal(2, destination[0].StartOffsetInBytes);
        Assert.Equal(4, destination[0].EndOffsetInBytes);
    }

    [Fact]
    public void UndersizedDestinationReportsPartialPrefixAndMore()
    {
        var regex = new Utf8Pcre2Regex("ab");
        var destination = new Utf8Pcre2MatchData[2];

        var written = regex.MatchMany(Encoding.UTF8.GetBytes("ab ab ab"), destination, out var isMore);
        Assert.Equal(2, written);
        Assert.True(isMore);
        Assert.Equal(0, destination[0].StartOffsetInBytes);
        Assert.Equal(3, destination[1].StartOffsetInBytes);
    }

    [Fact]
    public void MissLeavesDestinationUntouched()
    {
        var regex = new Utf8Pcre2Regex("ab");
        var destination = new Utf8Pcre2MatchData[2];

        var written = regex.MatchMany(Encoding.UTF8.GetBytes("zz zz"), destination, out var isMore);
        Assert.Equal(0, written);
        Assert.False(isMore);
        Assert.False(destination[0].Success);
        Assert.False(destination[1].Success);
    }
}
