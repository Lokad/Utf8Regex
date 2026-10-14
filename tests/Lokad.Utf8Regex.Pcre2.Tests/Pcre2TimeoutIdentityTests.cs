using Lokad.Utf8Regex.Pcre2;

namespace Lokad.Utf8Regex.Pcre2.Tests;

/// <summary>
/// R01 failure-vocabulary evidence: owned PCRE2 routes report
/// Pcre2MatchException(Timeout), including deadline expiry during initial admission.
/// </summary>
public sealed class Pcre2TimeoutIdentityTests
{
    [Fact]
    public void ExpiredDetailedSlotAdmissionReportsTheProfileTimeout()
    {
        long ticks = 0;
        var acquiring = false;
        using var clock = Lokad.Utf8Regex.Internal.Execution.Utf8ExecutionDeadline.UseClockForDiagnostics(
            () => acquiring ? ticks++ : 0);
        var regex = new Utf8Pcre2Regex("(a+)+$", Pcre2CompileOptions.None, default,
            default(Utf8Pcre2ExecutionLimits), TimeSpan.FromMilliseconds(1));
        Assert.IsType<Pcre2SliceDirectProgram>(regex.DebugCompiledProgram.Operations.Match);
        acquiring = true;
        var error = Assert.Throws<Pcre2MatchException>(() => regex.MatchDetailed("a"u8));
        Assert.Equal(Pcre2ErrorKind.Timeout, error.ErrorKind);
    }

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
    [Fact]
    public void CatastrophicMatchManyReportsTimeoutIdentity()
    {
        var regex = new Utf8Pcre2Regex(
            "(a+)+$",
            Pcre2CompileOptions.None,
            default,
            default(Utf8Pcre2ExecutionLimits),
            TimeSpan.FromMilliseconds(1));
        Assert.True(regex.Analyze().IsFullyNative);
        var destination = new Utf8Pcre2MatchData[8];
        var exception = Assert.Throws<Pcre2MatchException>(() => regex.MatchMany(CreateCatastrophicInput(), destination, out _));
        Assert.Equal(Pcre2ErrorKind.Timeout, exception.ErrorKind);
    }

    [Fact]
    public void CatastrophicReplaceReportsTimeoutIdentity()
    {
        var regex = new Utf8Pcre2Regex(
            "(a+)+$",
            Pcre2CompileOptions.None,
            default,
            default(Utf8Pcre2ExecutionLimits),
            TimeSpan.FromMilliseconds(1));
        Assert.True(regex.Analyze().IsFullyNative);
        var exception = Assert.Throws<Pcre2MatchException>(() => regex.Replace(CreateCatastrophicInput(), "Z"));
        Assert.Equal(Pcre2ErrorKind.Timeout, exception.ErrorKind);
    }

    [Fact]
    public void CatastrophicEnumerateReportsTimeoutIdentity()
    {
        var regex = new Utf8Pcre2Regex(
            "(a+)+$",
            Pcre2CompileOptions.None,
            default,
            default(Utf8Pcre2ExecutionLimits),
            TimeSpan.FromMilliseconds(1));
        Assert.True(regex.Analyze().IsFullyNative);
        Pcre2MatchException? caught = null;
        try
        {
            // Startup participates in the same deadline as MoveNext.
            var enumerator = regex.EnumerateMatches(CreateCatastrophicInput());
            while (enumerator.MoveNext())
            {
            }
        }
        catch (Pcre2MatchException exception)
        {
            caught = exception;
        }

        Assert.NotNull(caught);
        Assert.Equal(Pcre2ErrorKind.Timeout, caught.ErrorKind);
    }

    [Fact]
    public void CatastrophicMatchDetailedReportsTimeoutIdentity()
    {
        var regex = new Utf8Pcre2Regex(
            "(a+)+$",
            Pcre2CompileOptions.None,
            default,
            default(Utf8Pcre2ExecutionLimits),
            TimeSpan.FromMilliseconds(1));
        Assert.True(regex.Analyze().IsFullyNative);
        var exception = Assert.Throws<Pcre2MatchException>(() => regex.MatchDetailed(CreateCatastrophicInput()));
        Assert.Equal(Pcre2ErrorKind.Timeout, exception.ErrorKind);
    }
}
