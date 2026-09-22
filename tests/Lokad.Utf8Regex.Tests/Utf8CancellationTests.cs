using System.Text;

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
        var exception = Assert.Throws<OperationCanceledException>(() => regex.IsMatchFromUtf16Offset(s_input, 4, token));
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
    public void PreCanceledTokenThrowsBeforeCountFromOffset()
    {
        var regex = new Utf8Regex("cat|horse");
        var token = CanceledToken();
        var exception = Assert.Throws<OperationCanceledException>(() => regex.CountFromUtf16Offset(s_input, 4, token));
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
        var exception = Assert.Throws<OperationCanceledException>(() => regex.MatchFromUtf16Offset(s_input, 4, token));
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
        var exception = Assert.Throws<OperationCanceledException>(() => regex.MatchDetailedFromUtf16Offset(s_input, 4, token));
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
        var exception = Assert.Throws<OperationCanceledException>(() => regex.EnumerateMatchesFromUtf16Offset(s_input, 4, token));
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
    public void PreCanceledTokenThrowsBeforeReplaceEvaluator()
    {
        var regex = new Utf8Regex("cat|horse");
        var token = CanceledToken();
        var exception = Assert.Throws<OperationCanceledException>(() => regex.Replace(s_input, 0, static (in Utf8MatchContext context, ref Utf8ReplacementWriter writer, ref int state) => { writer.Append("pet"u8); }, token));
        Assert.Equal(token, exception.CancellationToken);
    }

    [Fact]
    public void PreCanceledTokenThrowsBeforeReplaceToString()
    {
        var regex = new Utf8Regex("cat|horse");
        var token = CanceledToken();
        var exception = Assert.Throws<OperationCanceledException>(() => regex.ReplaceToString(s_input, "pet", token));
        Assert.Equal(token, exception.CancellationToken);
    }

    [Fact]
    public void PreCanceledTokenThrowsBeforeReplaceToStringEvaluator()
    {
        var regex = new Utf8Regex("cat|horse");
        var token = CanceledToken();
        var exception = Assert.Throws<OperationCanceledException>(() => regex.ReplaceToString(s_input, 0, static (in Utf8MatchContext context, ref int state) => "pet", token));
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
        Assert.Equal(regex.ReplaceToString(s_input, "pet"), regex.ReplaceToString(s_input, "pet", token));
    }
}
