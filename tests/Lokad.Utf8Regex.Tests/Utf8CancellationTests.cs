using System.Text;
using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8CancellationTests
{
    private static readonly byte[] s_input = Encoding.UTF8.GetBytes("cat horse cat");

    private static CancellationToken CanceledToken()
    {
        var source = new CancellationTokenSource();
        source.Cancel();
        return source.Token;
    }

    [Fact]
    public void PreCanceledTokenThrowsBeforeIsMatch()
    {
        var regex = new Utf8Regex("cat|horse");
        var token = CanceledToken();
        var exception = Assert.Throws<OperationCanceledException>(() => regex.IsMatch(s_input, token));
        Assert.Equal(token, exception.CancellationToken);
    }

    [Fact]
    public void PreCanceledTokenThrowsBeforeIsMatchFromOffset()
    {
        var regex = new Utf8Regex("cat|horse");
        var token = CanceledToken();
        var exception = Assert.Throws<OperationCanceledException>(() => regex.Match(s_input, 4, [], token).Success);
        Assert.Equal(token, exception.CancellationToken);
    }

    [Fact]
    public void PreCanceledTokenThrowsBeforeCount()
    {
        var regex = new Utf8Regex("cat|horse");
        var token = CanceledToken();
        var exception = Assert.Throws<OperationCanceledException>(() => regex.Count(s_input, token));
        Assert.Equal(token, exception.CancellationToken);
    }

    [Fact]
    public void PreCanceledTokenThrowsBeforeMatch()
    {
        var regex = new Utf8Regex("cat|horse");
        var token = CanceledToken();
        var exception = Assert.Throws<OperationCanceledException>(() => regex.Match(s_input, token));
        Assert.Equal(token, exception.CancellationToken);
    }

    [Fact]
    public void PreCanceledTokenThrowsBeforeMatchFromOffset()
    {
        var regex = new Utf8Regex("cat|horse");
        var token = CanceledToken();
        var exception = Assert.Throws<OperationCanceledException>(() => regex.Match(s_input, Encoding.UTF8.GetByteCount(Encoding.UTF8.GetString(s_input).AsSpan(0, 4)), [], token));
        Assert.Equal(token, exception.CancellationToken);
    }

    [Fact]
    public void PreCanceledTokenThrowsBeforeMatchDetailed()
    {
        var regex = new Utf8Regex("cat|horse");
        var token = CanceledToken();
        var exception = Assert.Throws<OperationCanceledException>(() => regex.MatchDetailed(s_input, token));
        Assert.Equal(token, exception.CancellationToken);
    }

    [Fact]
    public void PreCanceledTokenThrowsBeforeMatchDetailedFromOffset()
    {
        var regex = new Utf8Regex("cat|horse");
        var token = CanceledToken();
        var exception = Assert.Throws<OperationCanceledException>(() => Utf8MatchSequence.First(regex.CreateMatchCursor(s_input, 4, token)));
        Assert.Equal(token, exception.CancellationToken);
    }

    [Fact]
    public void PreCanceledTokenThrowsBeforeEnumerateMatches()
    {
        var regex = new Utf8Regex("cat|horse");
        var token = CanceledToken();
        var exception = Assert.Throws<OperationCanceledException>(() => regex.EnumerateMatches(s_input, token));
        Assert.Equal(token, exception.CancellationToken);
    }

    [Fact]
    public void PreCanceledTokenThrowsBeforeEnumerateMatchesFromOffset()
    {
        var regex = new Utf8Regex("cat|horse");
        var token = CanceledToken();
        var exception = Assert.Throws<OperationCanceledException>(() => regex.EnumerateMatches(s_input, 4, token));
        Assert.Equal(token, exception.CancellationToken);
    }

    [Fact]
    public void PreCanceledTokenThrowsBeforeEnumerateSplits()
    {
        var regex = new Utf8Regex("cat|horse");
        var token = CanceledToken();
        var exception = Assert.Throws<OperationCanceledException>(() => regex.EnumerateSplits(s_input, token));
        Assert.Equal(token, exception.CancellationToken);
    }

    [Fact]
    public void PreCanceledTokenThrowsBeforeEnumerateSplitsWithCount()
    {
        var regex = new Utf8Regex("cat|horse");
        var token = CanceledToken();
        var exception = Assert.Throws<OperationCanceledException>(() => regex.EnumerateSplits(s_input, 2, token));
        Assert.Equal(token, exception.CancellationToken);
    }

    [Fact]
    public void PreCanceledTokenThrowsBeforeReplace()
    {
        var regex = new Utf8Regex("cat|horse");
        var token = CanceledToken();
        var exception = Assert.Throws<OperationCanceledException>(() => regex.Replace(s_input, "pet", token));
        Assert.Equal(token, exception.CancellationToken);
    }

    [Fact]
    public void PreCanceledTokenThrowsBeforeReplaceBytes()
    {
        var regex = new Utf8Regex("cat|horse");
        var token = CanceledToken();
        var exception = Assert.Throws<OperationCanceledException>(() => regex.Replace(s_input, "pet"u8, token));
        Assert.Equal(token, exception.CancellationToken);
    }

    [Fact]
    public void PreCanceledTokenThrowsBeforeTryReplace()
    {
        var regex = new Utf8Regex("cat|horse");
        var token = CanceledToken();
        var destination = new byte[64];
        var exception = Assert.Throws<OperationCanceledException>(() => regex.TryReplace(s_input, "pet", destination, out _, token));
        Assert.Equal(token, exception.CancellationToken);
    }

    [Fact]
    public void PreCanceledTokenThrowsBeforeTryReplaceBytes()
    {
        var regex = new Utf8Regex("cat|horse");
        var token = CanceledToken();
        var destination = new byte[64];
        var exception = Assert.Throws<OperationCanceledException>(() => regex.TryReplace(s_input, "pet"u8, destination, out _, token));
        Assert.Equal(token, exception.CancellationToken);
    }

    [Fact]
    public void CancellationBeatsValidation()
    {
        var regex = new Utf8Regex("cat|horse");
        var token = CanceledToken();
        var invalid = new byte[] { 0xFF, 0xFF };
        var exception = Assert.Throws<OperationCanceledException>(() => regex.IsMatch(invalid, token));
        Assert.Equal(token, exception.CancellationToken);
    }

    [Fact]
    public void UncanceledTokenMatchesTokenlessResults()
    {
        var regex = new Utf8Regex("cat|horse");
        var token = CancellationToken.None;
        Assert.Equal(regex.IsMatch(s_input), regex.IsMatch(s_input, token));
        Assert.Equal(regex.Count(s_input), regex.Count(s_input, token));
        Assert.Equal((regex.Match(s_input).IndexInUtf16, regex.Match(s_input).LengthInUtf16), (regex.Match(s_input, token).IndexInUtf16, regex.Match(s_input, token).LengthInUtf16));
        Assert.Equal(Encoding.UTF8.GetString(regex.Replace(s_input, "pet")), Encoding.UTF8.GetString(regex.Replace(s_input, "pet", token)));
    }

}
