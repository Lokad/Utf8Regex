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

    private static Utf8Pcre2Regex Create() => new("foo", Pcre2CompileOptions.None, default, default(Utf8Pcre2ExecutionLimits), Utf8Pcre2Regex.DefaultMatchTimeout);

    [Fact]
    public void PreCanceledTokenThrowsBeforeIsMatch()
    {
        var regex = Create();
        var token = CanceledToken();
        var exception = Assert.Throws<OperationCanceledException>(() => regex.IsMatch(s_input, 0, Pcre2MatchOptions.None, token));
        Assert.Equal(token, exception.CancellationToken);
    }

    [Fact]
    public void PreCanceledTokenThrowsBeforeCount()
    {
        var regex = Create();
        var token = CanceledToken();
        var exception = Assert.Throws<OperationCanceledException>(() => regex.Count(s_input, 0, Pcre2MatchOptions.None, token));
        Assert.Equal(token, exception.CancellationToken);
    }

    [Fact]
    public void PreCanceledTokenThrowsBeforeMatch()
    {
        var regex = Create();
        var token = CanceledToken();
        var exception = Assert.Throws<OperationCanceledException>(() => regex.Match(s_input, 0, Pcre2MatchOptions.None, token));
        Assert.Equal(token, exception.CancellationToken);
    }

    [Fact]
    public void PreCanceledTokenThrowsBeforeMatchDetailed()
    {
        var regex = Create();
        var token = CanceledToken();
        var exception = Assert.Throws<OperationCanceledException>(() => regex.MatchDetailed(s_input, 0, Pcre2MatchOptions.None, token));
        Assert.Equal(token, exception.CancellationToken);
    }

    [Fact]
    public void PreCanceledTokenThrowsBeforeEnumerateMatches()
    {
        var regex = Create();
        var token = CanceledToken();
        var exception = Assert.Throws<OperationCanceledException>(() => regex.EnumerateMatches(s_input, 0, Pcre2MatchOptions.None, token));
        Assert.Equal(token, exception.CancellationToken);
    }

    [Fact]
    public void PreCanceledTokenThrowsBeforeProbe()
    {
        var regex = Create();
        var token = CanceledToken();
        var exception = Assert.Throws<OperationCanceledException>(() => regex.Probe(s_input, Pcre2PartialMode.None, 0, Pcre2MatchOptions.None, token));
        Assert.Equal(token, exception.CancellationToken);
    }

    [Fact]
    public void PreCanceledTokenThrowsBeforeReplace()
    {
        var regex = Create();
        var token = CanceledToken();
        var exception = Assert.Throws<OperationCanceledException>(() => regex.Replace(s_input, "bar", 0, Pcre2SubstitutionOptions.None, Pcre2MatchOptions.None, token));
        Assert.Equal(token, exception.CancellationToken);
    }

    [Fact]
    public void PreCanceledTokenThrowsBeforeReplaceBytes()
    {
        var regex = Create();
        var token = CanceledToken();
        var exception = Assert.Throws<OperationCanceledException>(() => regex.Replace(s_input, "bar"u8, 0, Pcre2SubstitutionOptions.None, Pcre2MatchOptions.None, token));
        Assert.Equal(token, exception.CancellationToken);
    }

    [Fact]
    public void PreCanceledTokenThrowsBeforeReplaceEvaluator()
    {
        var regex = Create();
        var token = CanceledToken();
        var exception = Assert.Throws<OperationCanceledException>(() => regex.Replace(s_input, 0, static (in Utf8Pcre2MatchContext match, ref Utf8ReplacementWriter writer, ref int state) => { writer.Append("bar"u8); }, 0, Pcre2MatchOptions.None, token));
        Assert.Equal(token, exception.CancellationToken);
    }

    [Fact]
    public void PreCanceledTokenThrowsBeforeReplaceToString()
    {
        var regex = Create();
        var token = CanceledToken();
        var exception = Assert.Throws<OperationCanceledException>(() => regex.ReplaceToString(s_input, "bar", 0, Pcre2SubstitutionOptions.None, Pcre2MatchOptions.None, token));
        Assert.Equal(token, exception.CancellationToken);
    }

    [Fact]
    public void PreCanceledTokenThrowsBeforeReplaceToStringEvaluator()
    {
        var regex = Create();
        var token = CanceledToken();
        var exception = Assert.Throws<OperationCanceledException>(() => regex.ReplaceToString(s_input, 0, static (in Utf8Pcre2MatchContext match, ref int state) => "bar", 0, Pcre2MatchOptions.None, token));
        Assert.Equal(token, exception.CancellationToken);
    }

    [Fact]
    public void PreCanceledTokenThrowsBeforeTryReplace()
    {
        var regex = Create();
        var token = CanceledToken();
        var exception = Assert.Throws<OperationCanceledException>(() => regex.TryReplace(s_input, "bar"u8, new byte[64], out _, 0, Pcre2SubstitutionOptions.None, Pcre2MatchOptions.None, token));
        Assert.Equal(token, exception.CancellationToken);
    }

    [Fact]
    public void PreCanceledTokenThrowsBeforeMatchMany()
    {
        var regex = Create();
        var token = CanceledToken();
        var exception = Assert.Throws<OperationCanceledException>(() => regex.MatchMany(s_input, new Utf8Pcre2MatchData[8], out _, 0, Pcre2MatchOptions.None, token));
        Assert.Equal(token, exception.CancellationToken);
    }

    [Fact]
    public void PreCanceledTokenThrowsBeforeTryGetFirstSetGroup()
    {
        var regex = new Utf8Pcre2Regex("(?<w>foo)", Pcre2CompileOptions.None, default, default(Utf8Pcre2ExecutionLimits), Utf8Pcre2Regex.DefaultMatchTimeout);
        var token = CanceledToken();
        var exception = Assert.Throws<OperationCanceledException>(() => regex.TryGetFirstSetGroup(s_input, "w", out _, 0, Pcre2MatchOptions.None, token));
        Assert.Equal(token, exception.CancellationToken);
    }

    [Fact]
    public void PreCanceledTokenThrowsBeforeStaticIsMatch()
    {
        var token = CanceledToken();
        var exception = Assert.Throws<OperationCanceledException>(() => Utf8Pcre2Regex.IsMatch(s_input, "foo", Pcre2CompileOptions.None, default, default, Utf8Pcre2Regex.DefaultMatchTimeout, 0, token));
        Assert.Equal(token, exception.CancellationToken);
    }

    [Fact]
    public void PreCanceledTokenThrowsBeforeStaticMatch()
    {
        var token = CanceledToken();
        var exception = Assert.Throws<OperationCanceledException>(() => Utf8Pcre2Regex.Match(s_input, "foo", Pcre2CompileOptions.None, default, default, Utf8Pcre2Regex.DefaultMatchTimeout, 0, token));
        Assert.Equal(token, exception.CancellationToken);
    }

    [Fact]
    public void PreCanceledTokenThrowsBeforeStaticReplace()
    {
        var token = CanceledToken();
        var exception = Assert.Throws<OperationCanceledException>(() => Utf8Pcre2Regex.Replace(s_input, "foo", "bar", Pcre2CompileOptions.None, default, default, Utf8Pcre2Regex.DefaultMatchTimeout, 0, Pcre2SubstitutionOptions.None, token));
        Assert.Equal(token, exception.CancellationToken);
    }

    [Fact]
    public void CancellationBeatsValidation()
    {
        var regex = Create();
        var token = CanceledToken();
        var invalid = new byte[] { 0xFF, 0xFF };
        var exception = Assert.Throws<OperationCanceledException>(() => regex.IsMatch(invalid, 0, Pcre2MatchOptions.None, token));
        Assert.Equal(token, exception.CancellationToken);
    }

    [Fact]
    public void UncanceledTokenMatchesTokenlessResults()
    {
        var regex = Create();
        var token = CancellationToken.None;
        Assert.Equal(regex.IsMatch(s_input, 0, Pcre2MatchOptions.None), regex.IsMatch(s_input, 0, Pcre2MatchOptions.None, token));
        Assert.Equal(regex.Count(s_input, 0, Pcre2MatchOptions.None), regex.Count(s_input, 0, Pcre2MatchOptions.None, token));
        Assert.Equal(regex.Match(s_input, 0, Pcre2MatchOptions.None).GetValueString(), regex.Match(s_input, 0, Pcre2MatchOptions.None, token).GetValueString());
        Assert.Equal(regex.ReplaceToString(s_input, "bar", 0, Pcre2SubstitutionOptions.None, Pcre2MatchOptions.None), regex.ReplaceToString(s_input, "bar", 0, Pcre2SubstitutionOptions.None, Pcre2MatchOptions.None, token));
    }

    [Fact]
    public void CancelInsideEvaluatorAbortsLiteralReplace()
    {
        var regex = new Utf8Pcre2Regex("foo", Pcre2CompileOptions.None, default, default(Utf8Pcre2ExecutionLimits), Utf8Pcre2Regex.DefaultMatchTimeout);
        using var canceled = new CancellationTokenSource();
        var input = Encoding.UTF8.GetBytes("foo foo foo");
        var calls = 0;
        var exception = Assert.Throws<OperationCanceledException>(() => regex.Replace(
            input,
            0,
            (in Utf8Pcre2MatchContext match, ref Utf8ReplacementWriter writer, ref int state) => { if (++calls == 1) { canceled.Cancel(); } writer.Append("bar"u8); },
            0,
            Pcre2MatchOptions.None,
            canceled.Token));
        Assert.Equal(canceled.Token, exception.CancellationToken);
        Assert.Equal(1, calls);
    }

    [Fact]
    public void CancelInsideEvaluatorAbortsBacktrackingReplace()
    {
        var regex = new Utf8Pcre2Regex("cat|horse", Pcre2CompileOptions.None, default, default(Utf8Pcre2ExecutionLimits), Utf8Pcre2Regex.DefaultMatchTimeout);
        using var canceled = new CancellationTokenSource();
        var input = Encoding.UTF8.GetBytes("cat horse cat");
        var calls = 0;
        var exception = Assert.Throws<OperationCanceledException>(() => regex.Replace(
            input,
            0,
            (in Utf8Pcre2MatchContext match, ref Utf8ReplacementWriter writer, ref int state) => { if (++calls == 1) { canceled.Cancel(); } writer.Append("bar"u8); },
            0,
            Pcre2MatchOptions.None,
            canceled.Token));
        Assert.Equal(canceled.Token, exception.CancellationToken);
        Assert.Equal(1, calls);
    }

    [Fact]
    public void CancelInsideEvaluatorAbortsReplaceToString()
    {
        var regex = new Utf8Pcre2Regex("foo", Pcre2CompileOptions.None, default, default(Utf8Pcre2ExecutionLimits), Utf8Pcre2Regex.DefaultMatchTimeout);
        using var canceled = new CancellationTokenSource();
        var input = Encoding.UTF8.GetBytes("foo foo foo");
        var calls = 0;
        var exception = Assert.Throws<OperationCanceledException>(() => regex.ReplaceToString(
            input,
            0,
            (in Utf8Pcre2MatchContext match, ref int state) => { if (++calls == 1) { canceled.Cancel(); } return "bar"; },
            0,
            Pcre2MatchOptions.None,
            canceled.Token));
        Assert.Equal(canceled.Token, exception.CancellationToken);
        Assert.Equal(1, calls);
    }

    [Fact]
    public void UncanceledEvaluatorParity()
    {
        var regex = new Utf8Pcre2Regex("foo", Pcre2CompileOptions.None, default, default(Utf8Pcre2ExecutionLimits), Utf8Pcre2Regex.DefaultMatchTimeout);
        var token = CancellationToken.None;
        var input = Encoding.UTF8.GetBytes("foo foo foo");
        var plainCalls = 0;
        var tokenCalls = 0;
        var plain = regex.Replace(input, 0, (in Utf8Pcre2MatchContext match, ref Utf8ReplacementWriter writer, ref int state) => { plainCalls++; writer.Append("bar"u8); }, 0, Pcre2MatchOptions.None);
        var withToken = regex.Replace(input, 0, (in Utf8Pcre2MatchContext match, ref Utf8ReplacementWriter writer, ref int state) => { tokenCalls++; writer.Append("bar"u8); }, 0, Pcre2MatchOptions.None, token);
        Assert.Equal(plain, withToken);
        Assert.Equal(plainCalls, tokenCalls);
    }
}
