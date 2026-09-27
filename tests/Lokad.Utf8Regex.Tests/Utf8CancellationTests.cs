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

    [Fact]
    public void CancelInsideEvaluatorAbortsReplace()
    {
        var regex = new Utf8Regex("cat|horse");
        using var canceled = new CancellationTokenSource();
        var calls = 0;
        var exception = Assert.Throws<OperationCanceledException>(() => regex.Replace(
            s_input,
            0,
            (in Utf8MatchContext context, ref Utf8ReplacementWriter writer, ref int state) => { if (++calls == 1) { canceled.Cancel(); } writer.Append("pet"u8); },
            canceled.Token));
        Assert.Equal(canceled.Token, exception.CancellationToken);
        Assert.Equal(1, calls);
    }

    [Fact]
    public void CancelInsideMappedBranchEvaluatorAbortsReplace()
    {
        var regex = new Utf8Regex("(cat)|(horse)");
        using var canceled = new CancellationTokenSource();
        var input = Encoding.UTF8.GetBytes("cat horse");
        var calls = 0;
        var exception = Assert.Throws<OperationCanceledException>(() => regex.Replace(
            input,
            0,
            (in Utf8MatchContext context, ref Utf8ReplacementWriter writer, ref int state) => { if (++calls == 1) { canceled.Cancel(); } writer.Append("pet"u8); },
            canceled.Token));
        Assert.Equal(canceled.Token, exception.CancellationToken);
        Assert.Equal(1, calls);
    }

    [Fact]
    public void CancelInsideGroupedSpanEvaluatorAbortsReplace()
    {
        var regex = new Utf8Regex("(a)(b)");
        using var canceled = new CancellationTokenSource();
        var input = Encoding.UTF8.GetBytes("ab ab");
        var calls = 0;
        var exception = Assert.Throws<OperationCanceledException>(() => regex.Replace(
            input,
            0,
            (in Utf8MatchContext context, ref Utf8ReplacementWriter writer, ref int state) => { if (++calls == 1) { canceled.Cancel(); } writer.Append("pet"u8); },
            canceled.Token));
        Assert.Equal(canceled.Token, exception.CancellationToken);
        Assert.Equal(1, calls);
        Assert.False(regex.Inspection.DebugFallbackRegexIsCreated);
    }

    [Fact]
    public void CancelInsideDivergentBranchEvaluatorAbortsReplace()
    {
        var regex = new Utf8Regex("(ab|c)(d)");
        using var canceled = new CancellationTokenSource();
        var input = Encoding.UTF8.GetBytes("abd cd");
        var calls = 0;
        var exception = Assert.Throws<OperationCanceledException>(() => regex.Replace(
            input,
            0,
            (in Utf8MatchContext context, ref Utf8ReplacementWriter writer, ref int state) => { if (++calls == 1) { canceled.Cancel(); } writer.Append("pet"u8); },
            canceled.Token));
        Assert.Equal(canceled.Token, exception.CancellationToken);
        Assert.Equal(1, calls);
        Assert.False(regex.Inspection.DebugFallbackRegexIsCreated);
    }

    [Fact]
    public void CancelInsideBclFallbackEvaluatorAbortsReplace()
    {
        // Seven-loop shapes stay past the six-loop map cap and keep the BCL evaluator
        // route (declared R05 debt).
        var regex = new Utf8Regex("(a)?b(c)?d(e)?f(g)?h(i)?j(k)?l(m)?");
        using var canceled = new CancellationTokenSource();
        var input = Encoding.UTF8.GetBytes("abcdefghijklm abcdefghijklm");
        var calls = 0;
        var exception = Assert.Throws<OperationCanceledException>(() => regex.Replace(
            input,
            0,
            (in Utf8MatchContext context, ref Utf8ReplacementWriter writer, ref int state) => { if (++calls == 1) { canceled.Cancel(); } writer.Append("pet"u8); },
            canceled.Token));
        Assert.Equal(canceled.Token, exception.CancellationToken);
        Assert.Equal(1, calls);
        Assert.True(regex.Inspection.DebugFallbackRegexIsCreated);
    }

    [Fact]
    public void CancelInsideEvaluatorAbortsReplaceToString()
    {
        var regex = new Utf8Regex("cat|horse");
        using var canceled = new CancellationTokenSource();
        var calls = 0;
        var exception = Assert.Throws<OperationCanceledException>(() => regex.ReplaceToString(
            s_input,
            0,
            (in Utf8MatchContext context, ref int state) => { if (++calls == 1) { canceled.Cancel(); } return "pet"; },
            canceled.Token));
        Assert.Equal(canceled.Token, exception.CancellationToken);
        Assert.Equal(1, calls);
    }

    [Fact]
    public void UncanceledEvaluatorParity()
    {
        var regex = new Utf8Regex("cat|horse");
        var token = CancellationToken.None;
        var plainCalls = 0;
        var tokenCalls = 0;
        var plain = regex.Replace(s_input, 0, (in Utf8MatchContext context, ref Utf8ReplacementWriter writer, ref int state) => { plainCalls++; writer.Append("pet"u8); });
        var withToken = regex.Replace(s_input, 0, (in Utf8MatchContext context, ref Utf8ReplacementWriter writer, ref int state) => { tokenCalls++; writer.Append("pet"u8); }, token);
        Assert.Equal(plain, withToken);
        Assert.Equal(plainCalls, tokenCalls);
    }
}
