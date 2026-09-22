using Lokad.Utf8Regex.Pcre2;

namespace Lokad.Utf8Regex.Pcre2.Tests;

/// <summary>
/// R01 failure-vocabulary evidence: timeout-breach exception identity per route. The owned
/// backtracking engine is expected to raise Pcre2MatchException(Timeout) while BCL-delegated
/// routes surface RegexMatchTimeoutException. These pins decide the R01 and R06 mapping.
/// </summary>
public sealed class Pcre2TimeoutIdentityTests
{
    private static byte[] CreateCatastrophicInput()
    {
        var input = new byte[50_000];
        Array.Fill(input, (byte)'a');
        input[^1] = (byte)'!';
        return input;
    }

    [Fact]
    public void CatastrophicIsMatchReportsRouteAndTimeoutIdentity()
    {
        var regex = new Utf8Pcre2Regex(
            "(a+)+$",
            Pcre2CompileOptions.None,
            default,
            default(Utf8Pcre2ExecutionLimits),
            TimeSpan.FromMilliseconds(1));
        Assert.True(regex.Analyze().IsFullyNative);
        var exception = Assert.Throws<Pcre2MatchException>(() => regex.IsMatch(CreateCatastrophicInput()));
        Assert.Equal(Pcre2ErrorKind.Timeout, exception.ErrorKind);
    }

    [Fact]
    public void CatastrophicCountReportsRouteAndTimeoutIdentity()
    {
        var regex = new Utf8Pcre2Regex(
            "(a+)+$",
            Pcre2CompileOptions.None,
            default,
            default(Utf8Pcre2ExecutionLimits),
            TimeSpan.FromMilliseconds(1));
        Assert.True(regex.Analyze().IsFullyNative);
        var exception = Assert.Throws<Pcre2MatchException>(() => regex.Count(CreateCatastrophicInput()));
        Assert.Equal(Pcre2ErrorKind.Timeout, exception.ErrorKind);
    }
}
