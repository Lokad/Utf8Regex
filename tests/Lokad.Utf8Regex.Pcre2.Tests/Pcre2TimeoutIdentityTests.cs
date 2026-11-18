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
        var regex = new Utf8Pcre2Regex("(a+)+$", options: Pcre2CompileOptions.None, compileSettings: default, policy: new Utf8RegexPolicy
        {
            MatchTimeout = TimeSpan.FromMilliseconds(1)
        });
        Assert.IsType<Pcre2SliceDirectProgram>(regex.DebugCompiledProgram.Operations.Match);
        acquiring = true;
        var error = Assert.Throws<Pcre2MatchException>(() =>
        {
            ReadOnlyMemory<byte> result1Input = ("a"u8).ToArray();
            using var result1Cursor = regex.CreateMatchCursor(result1Input);
            result1Cursor.MoveNext(out var result1Result);
            result1Cursor.Dispose();
            _ = result1Result;
        });
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
        var regex = new Utf8Pcre2Regex("(a+)+$", options: Pcre2CompileOptions.None, compileSettings: default, policy: new Utf8RegexPolicy
        {
            MatchTimeout = TimeSpan.FromMilliseconds(1)
        });
        var exception = Assert.Throws<Pcre2MatchException>(() => regex.IsMatch(CreateCatastrophicInput()));
        Assert.Equal(Pcre2ErrorKind.Timeout, exception.ErrorKind);
    }

    [Fact]
    public void CatastrophicCountReportsRouteAndTimeoutIdentity()
    {
        var regex = new Utf8Pcre2Regex("(a+)+$", options: Pcre2CompileOptions.None, compileSettings: default, policy: new Utf8RegexPolicy
        {
            MatchTimeout = TimeSpan.FromMilliseconds(1)
        });
        var exception = Assert.Throws<Pcre2MatchException>(() => regex.Count(CreateCatastrophicInput()));
        Assert.Equal(Pcre2ErrorKind.Timeout, exception.ErrorKind);
    }

    [Fact]
    public void CatastrophicReplaceReportsTimeoutIdentity()
    {
        var regex = new Utf8Pcre2Regex("(a+)+$", options: Pcre2CompileOptions.None, compileSettings: default, policy: new Utf8RegexPolicy
        {
            MatchTimeout = TimeSpan.FromMilliseconds(1)
        });
        var exception = Assert.Throws<Pcre2MatchException>(() => regex.Replace(CreateCatastrophicInput(), "Z"u8));
        Assert.Equal(Pcre2ErrorKind.Timeout, exception.ErrorKind);
    }

    [Fact]
    public void CatastrophicEnumerateReportsTimeoutIdentity()
    {
        var regex = new Utf8Pcre2Regex("(a+)+$", options: Pcre2CompileOptions.None, compileSettings: default, policy: new Utf8RegexPolicy
        {
            MatchTimeout = TimeSpan.FromMilliseconds(1)
        });
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
        var regex = new Utf8Pcre2Regex("(a+)+$", options: Pcre2CompileOptions.None, compileSettings: default, policy: new Utf8RegexPolicy
        {
            MatchTimeout = TimeSpan.FromMilliseconds(1)
        });
        var exception = Assert.Throws<Pcre2MatchException>(() =>
        {
            ReadOnlyMemory<byte> result2Input = CreateCatastrophicInput();
            using var result2Cursor = regex.CreateMatchCursor(result2Input);
            result2Cursor.MoveNext(out var result2Result);
            result2Cursor.Dispose();
            _ = result2Result;
        });
        Assert.Equal(Pcre2ErrorKind.Timeout, exception.ErrorKind);
    }
}
