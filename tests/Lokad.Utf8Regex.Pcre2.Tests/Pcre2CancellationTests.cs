using System.Text;
using Lokad.Utf8Regex.Pcre2;

namespace Lokad.Utf8Regex.Pcre2.Tests;

public sealed class Pcre2CancellationTests
{
    private static readonly byte[] s_input = Encoding.UTF8.GetBytes("foo foo");

    private static CancellationToken CanceledToken()
    {
        var source = new CancellationTokenSource();
        source.Cancel();
        return source.Token;
    }

    private static Utf8Pcre2Regex Create() => new("foo", options: Pcre2CompileOptions.None, compileSettings: default, policy: new Utf8RegexPolicy
    {
        MatchTimeout = Utf8Pcre2Regex.DefaultMatchTimeout
    });

    [Fact]
    public void PreCanceledTokenThrowsBeforeIsMatch()
    {
        var regex = Create();
        var token = CanceledToken();
        var exception = Assert.Throws<OperationCanceledException>(() => regex.IsMatch(s_input, cancellationToken: token));
        Assert.Equal(token, exception.CancellationToken);
    }

    [Fact]
    public void PreCanceledTokenThrowsBeforeCount()
    {
        var regex = Create();
        var token = CanceledToken();
        var exception = Assert.Throws<OperationCanceledException>(() => regex.Count(s_input, cancellationToken: token));
        Assert.Equal(token, exception.CancellationToken);
    }

    [Fact]
    public void PreCanceledTokenThrowsBeforeMatch()
    {
        var regex = Create();
        var token = CanceledToken();
        var exception = Assert.Throws<OperationCanceledException>(() => regex.Match(s_input, cancellationToken: token));
        Assert.Equal(token, exception.CancellationToken);
    }

    [Fact]
    public void PreCanceledTokenThrowsBeforeMatchDetailed()
    {
        var regex = Create();
        var token = CanceledToken();
        var exception = Assert.Throws<OperationCanceledException>(() => regex.MatchDetailed(s_input, cancellationToken: token));
        Assert.Equal(token, exception.CancellationToken);
    }

    [Fact]
    public void PreCanceledTokenThrowsBeforeEnumerateMatches()
    {
        var regex = Create();
        var token = CanceledToken();
        var exception = Assert.Throws<OperationCanceledException>(() => regex.EnumerateMatches(s_input, cancellationToken: token));
        Assert.Equal(token, exception.CancellationToken);
    }

    [Fact]
    public void PreCanceledTokenThrowsBeforeProbe()
    {
        var regex = Create();
        var token = CanceledToken();
        var exception = Assert.Throws<OperationCanceledException>(() => regex.Probe(s_input, Pcre2PartialMode.None, cancellationToken: token));
        Assert.Equal(token, exception.CancellationToken);
    }

    [Fact]
    public void PreCanceledTokenThrowsBeforeReplace()
    {
        var regex = Create();
        var token = CanceledToken();
        var exception = Assert.Throws<OperationCanceledException>(() => regex.Replace(s_input, "bar", cancellationToken: token));
        Assert.Equal(token, exception.CancellationToken);
    }

    [Fact]
    public void PreCanceledTokenThrowsBeforeReplaceBytes()
    {
        var regex = Create();
        var token = CanceledToken();
        var exception = Assert.Throws<OperationCanceledException>(() => regex.Replace(s_input, "bar"u8, cancellationToken: token));
        Assert.Equal(token, exception.CancellationToken);
    }

    [Fact]
    public void PreCanceledTokenThrowsBeforeTryReplace()
    {
        var regex = Create();
        var token = CanceledToken();
        var exception = Assert.Throws<OperationCanceledException>(() => regex.TryReplace(s_input, "bar"u8, new byte[64], out _, cancellationToken: token));
        Assert.Equal(token, exception.CancellationToken);
    }

    [Fact]
    public void PreCanceledTokenThrowsBeforeMatchMany()
    {
        var regex = Create();
        var token = CanceledToken();
        var exception = Assert.Throws<OperationCanceledException>(() => regex.MatchMany(s_input, new Utf8Pcre2MatchData[8], out _, cancellationToken: token));
        Assert.Equal(token, exception.CancellationToken);
    }

    [Fact]
    public void CancellationBeatsValidation()
    {
        var regex = Create();
        var token = CanceledToken();
        var invalid = new byte[] { 0xFF, 0xFF };
        var exception = Assert.Throws<OperationCanceledException>(() => regex.IsMatch(invalid, cancellationToken: token));
        Assert.Equal(token, exception.CancellationToken);
    }

    [Fact]
    public void OptionalArgumentsPreserveOffsetsOptionsAndDefaultSubstitution()
    {
        var regex = Create();
        Assert.Equal(2, regex.Count(s_input, cancellationToken: default));
        Assert.Equal(1, regex.Count(s_input, startOffsetInBytes: 4));
        Assert.Equal(4, regex.Match(s_input, startOffsetInBytes: 4).StartOffsetInBytes);
        Assert.False(regex.IsMatch(s_input, startOffsetInBytes: 1, matchOptions: Pcre2MatchOptions.Anchored));
        Assert.Equal("bar bar", Encoding.UTF8.GetString(regex.Replace(s_input, "bar")));
        var destination = new byte[16];
        Assert.Equal(System.Buffers.OperationStatus.Done, regex.TryReplace(s_input, "bar"u8,
            destination, out var written, substitutionOptions: Pcre2SubstitutionOptions.None));
        Assert.Equal("bar bar", Encoding.UTF8.GetString(destination.AsSpan(0, written)));
    }
}
