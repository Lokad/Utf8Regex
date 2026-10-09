using System.Text;
using Lokad.Utf8Regex.PythonRe;

namespace Lokad.Utf8Regex.PythonRe.Tests;

[Collection(PythonReGlobalSettingsCollection.Name)]
public sealed class PythonReCancellationTests
{
    private static readonly byte[] s_input = Encoding.UTF8.GetBytes("cat horse");

    private static CancellationToken CanceledToken()
    {
        var source = new CancellationTokenSource();
        source.Cancel();
        return source.Token;
    }

    private static Utf8PythonRegex Create() => new("cat|horse", PythonReCompileOptions.None);

    [Fact]
    public void PreCanceledTokenThrowsBeforeIsMatch()
    {
        var regex = Create();
        var token = CanceledToken();
        var exception = Assert.Throws<OperationCanceledException>(() => regex.IsMatch(s_input, 4, token));
        Assert.Equal(token, exception.CancellationToken);
    }

    [Fact]
    public void PreCanceledTokenThrowsBeforeSearch()
    {
        var regex = Create();
        var token = CanceledToken();
        var exception = Assert.Throws<OperationCanceledException>(() => regex.Search(s_input, 4, token));
        Assert.Equal(token, exception.CancellationToken);
    }

    [Fact]
    public void PreCanceledTokenThrowsBeforeMatch()
    {
        var regex = Create();
        var token = CanceledToken();
        var exception = Assert.Throws<OperationCanceledException>(() => regex.Match(s_input, 4, token));
        Assert.Equal(token, exception.CancellationToken);
    }

    [Fact]
    public void PreCanceledTokenThrowsBeforeFullMatch()
    {
        var regex = Create();
        var token = CanceledToken();
        var exception = Assert.Throws<OperationCanceledException>(() => regex.FullMatch(s_input, 4, token));
        Assert.Equal(token, exception.CancellationToken);
    }

    [Fact]
    public void PreCanceledTokenThrowsBeforeCount()
    {
        var regex = Create();
        var token = CanceledToken();
        var exception = Assert.Throws<OperationCanceledException>(() => regex.Count(s_input, 4, token));
        Assert.Equal(token, exception.CancellationToken);
    }

    [Fact]
    public void PreCanceledTokenThrowsBeforeSearchDetailed()
    {
        var regex = Create();
        var token = CanceledToken();
        var exception = Assert.Throws<OperationCanceledException>(() => regex.SearchDetailed(s_input, 4, token));
        Assert.Equal(token, exception.CancellationToken);
    }

    [Fact]
    public void PreCanceledTokenThrowsBeforeMatchDetailed()
    {
        var regex = Create();
        var token = CanceledToken();
        var exception = Assert.Throws<OperationCanceledException>(() => regex.MatchDetailed(s_input, 4, token));
        Assert.Equal(token, exception.CancellationToken);
    }

    [Fact]
    public void PreCanceledTokenThrowsBeforeFullMatchDetailed()
    {
        var regex = Create();
        var token = CanceledToken();
        var exception = Assert.Throws<OperationCanceledException>(() => regex.FullMatchDetailed(s_input, 4, token));
        Assert.Equal(token, exception.CancellationToken);
    }

    [Fact]
    public void PreCanceledTokenThrowsBeforeSearchDetailedData()
    {
        var regex = Create();
        var token = CanceledToken();
        var exception = Assert.Throws<OperationCanceledException>(() => regex.SearchDetailedData(s_input, 4, token));
        Assert.Equal(token, exception.CancellationToken);
    }

    [Fact]
    public void PreCanceledTokenThrowsBeforeMatchDetailedData()
    {
        var regex = Create();
        var token = CanceledToken();
        var exception = Assert.Throws<OperationCanceledException>(() => regex.MatchDetailedData(s_input, 4, token));
        Assert.Equal(token, exception.CancellationToken);
    }

    [Fact]
    public void PreCanceledTokenThrowsBeforeFullMatchDetailedData()
    {
        var regex = Create();
        var token = CanceledToken();
        var exception = Assert.Throws<OperationCanceledException>(() => regex.FullMatchDetailedData(s_input, 4, token));
        Assert.Equal(token, exception.CancellationToken);
    }

    [Fact]
    public void PreCanceledTokenThrowsBeforeSearchToString()
    {
        var regex = Create();
        var token = CanceledToken();
        var exception = Assert.Throws<OperationCanceledException>(() => regex.SearchToString(s_input, 4, token));
        Assert.Equal(token, exception.CancellationToken);
    }

    [Fact]
    public void PreCanceledTokenThrowsBeforeMatchToString()
    {
        var regex = Create();
        var token = CanceledToken();
        var exception = Assert.Throws<OperationCanceledException>(() => regex.MatchToString(s_input, 4, token));
        Assert.Equal(token, exception.CancellationToken);
    }

    [Fact]
    public void PreCanceledTokenThrowsBeforeFullMatchToString()
    {
        var regex = Create();
        var token = CanceledToken();
        var exception = Assert.Throws<OperationCanceledException>(() => regex.FullMatchToString(s_input, 4, token));
        Assert.Equal(token, exception.CancellationToken);
    }

    [Fact]
    public void PreCanceledTokenThrowsBeforeFindAll()
    {
        var regex = Create();
        var token = CanceledToken();
        var exception = Assert.Throws<OperationCanceledException>(() => regex.FindAll(s_input, 4, token));
        Assert.Equal(token, exception.CancellationToken);
    }

    [Fact]
    public void PreCanceledTokenThrowsBeforeFindAllToStrings()
    {
        var regex = Create();
        var token = CanceledToken();
        var exception = Assert.Throws<OperationCanceledException>(() => regex.FindAllToStrings(s_input, 4, token));
        Assert.Equal(token, exception.CancellationToken);
    }

    [Fact]
    public void PreCanceledTokenThrowsBeforeFindAllToUtf8()
    {
        var regex = Create();
        var token = CanceledToken();
        var exception = Assert.Throws<OperationCanceledException>(() => regex.FindAllToUtf8(s_input, 4, token));
        Assert.Equal(token, exception.CancellationToken);
    }

    [Fact]
    public void PreCanceledTokenThrowsBeforeFindIterDetailed()
    {
        var regex = Create();
        var token = CanceledToken();
        var exception = Assert.Throws<OperationCanceledException>(() => regex.FindIterDetailed(s_input, 4, token));
        Assert.Equal(token, exception.CancellationToken);
    }

    [Fact]
    public void PreCanceledTokenThrowsBeforeSplitToStrings()
    {
        var regex = Create();
        var token = CanceledToken();
        var exception = Assert.Throws<OperationCanceledException>(() => regex.SplitToStrings(s_input, 1, 4, token));
        Assert.Equal(token, exception.CancellationToken);
    }

    [Fact]
    public void PreCanceledTokenThrowsBeforeSplitDetailed()
    {
        var regex = Create();
        var token = CanceledToken();
        var exception = Assert.Throws<OperationCanceledException>(() => regex.SplitDetailed(s_input, 1, 4, token));
        Assert.Equal(token, exception.CancellationToken);
    }

    [Fact]
    public void PreCanceledTokenThrowsBeforeReplace()
    {
        var regex = Create();
        var token = CanceledToken();
        var exception = Assert.Throws<OperationCanceledException>(() => regex.Replace(s_input, "pet", 0, 4, token));
        Assert.Equal(token, exception.CancellationToken);
    }

    [Fact]
    public void PreCanceledTokenThrowsBeforeReplaceToString()
    {
        var regex = Create();
        var token = CanceledToken();
        var exception = Assert.Throws<OperationCanceledException>(() => regex.ReplaceToString(s_input, "pet", 0, 4, token));
        Assert.Equal(token, exception.CancellationToken);
    }

    [Fact]
    public void PreCanceledTokenThrowsBeforeSubnToString()
    {
        var regex = Create();
        var token = CanceledToken();
        var exception = Assert.Throws<OperationCanceledException>(() => regex.SubnToString(s_input, "pet", 0, 4, token));
        Assert.Equal(token, exception.CancellationToken);
    }

    [Fact]
    public void PreCanceledTokenThrowsBeforeSubn()
    {
        var regex = Create();
        var token = CanceledToken();
        var exception = Assert.Throws<OperationCanceledException>(() => regex.Subn(s_input, "pet", 0, 4, token));
        Assert.Equal(token, exception.CancellationToken);
    }

    [Fact]
    public void PreCanceledTokenThrowsBeforeReplaceEvaluator()
    {
        var regex = Create();
        var token = CanceledToken();
        var exception = Assert.Throws<OperationCanceledException>(() => regex.Replace(s_input, 0, static (int state, Utf8PythonDetailedMatchData match) => "[" + match.Value.ValueText + "]", 0, 4, token));
        Assert.Equal(token, exception.CancellationToken);
    }

    [Fact]
    public void PreCanceledTokenThrowsBeforeReplaceToStringEvaluator()
    {
        var regex = Create();
        var token = CanceledToken();
        var exception = Assert.Throws<OperationCanceledException>(() => regex.ReplaceToString(s_input, 0, static (int state, Utf8PythonDetailedMatchData match) => "[" + match.Value.ValueText + "]", 0, 4, token));
        Assert.Equal(token, exception.CancellationToken);
    }

    [Fact]
    public void PreCanceledTokenThrowsBeforeSubnToStringEvaluator()
    {
        var regex = Create();
        var token = CanceledToken();
        var exception = Assert.Throws<OperationCanceledException>(() => regex.SubnToString(s_input, 0, static (int state, Utf8PythonDetailedMatchData match) => "[" + match.Value.ValueText + "]", 0, 4, token));
        Assert.Equal(token, exception.CancellationToken);
    }

    [Fact]
    public void PreCanceledTokenThrowsBeforeSubnEvaluator()
    {
        var regex = Create();
        var token = CanceledToken();
        var exception = Assert.Throws<OperationCanceledException>(() => regex.Subn(s_input, 0, static (int state, Utf8PythonDetailedMatchData match) => "[" + match.Value.ValueText + "]", 0, 4, token));
        Assert.Equal(token, exception.CancellationToken);
    }

    [Fact]
    public void PreCanceledTokenThrowsBeforeReplaceUtf8Evaluator()
    {
        var regex = Create();
        var token = CanceledToken();
        var exception = Assert.Throws<OperationCanceledException>(() => regex.Replace(s_input, 0, static (int state, Utf8PythonDetailedMatchData match) => Encoding.UTF8.GetBytes("[" + match.Value.ValueText + "]"), 0, 4, token));
        Assert.Equal(token, exception.CancellationToken);
    }

    [Fact]
    public void PreCanceledTokenThrowsBeforeSubnUtf8Evaluator()
    {
        var regex = Create();
        var token = CanceledToken();
        var exception = Assert.Throws<OperationCanceledException>(() => regex.Subn(s_input, 0, static (int state, Utf8PythonDetailedMatchData match) => Encoding.UTF8.GetBytes("[" + match.Value.ValueText + "]"), 0, 4, token));
        Assert.Equal(token, exception.CancellationToken);
    }

    [Fact]
    public void CancellationBeatsValidation()
    {
        var regex = Create();
        var token = CanceledToken();
        var invalid = new byte[] { 0xFF, 0xFF };
        var exception = Assert.Throws<OperationCanceledException>(() => regex.IsMatch(invalid, 0, token));
        Assert.Equal(token, exception.CancellationToken);
    }

    [Fact]
    public void UncanceledTokenMatchesTokenlessResults()
    {
        var regex = Create();
        var token = CancellationToken.None;
        Assert.Equal(regex.IsMatch(s_input, 4), regex.IsMatch(s_input, 4, token));
        Assert.Equal(regex.Count(s_input, 4), regex.Count(s_input, 4, token));
        Assert.Equal(regex.Match(s_input, 4).GetValueString(), regex.Match(s_input, 4, token).GetValueString());
        Assert.Equal(regex.ReplaceToString(s_input, "pet", 0, 4), regex.ReplaceToString(s_input, "pet", 0, 4, token));
    }

    [Fact]
    public void CancelInsideEvaluatorAbortsOwnedSubn()
    {
        var regex = Create();
        using var canceled = new CancellationTokenSource();
        var input = Encoding.UTF8.GetBytes("cat horse cat");
        var calls = 0;
        var exception = Assert.Throws<OperationCanceledException>(() => regex.Subn(
            input,
            0,
            (int state, Utf8PythonDetailedMatchData match) => { if (++calls == 1) { canceled.Cancel(); } return "[" + match.Value.ValueText + "]"; },
            0,
            0,
            canceled.Token));
        Assert.Equal(canceled.Token, exception.CancellationToken);
        Assert.Equal(1, calls);
        Assert.True(regex.DebugUsesOwnedSlice);
    }

    [Fact]
    public void CancelInsideEvaluatorAbortsScalarSubn()
    {
        var regex = new Utf8PythonRegex("(a)(b)", PythonReCompileOptions.None);
        using var canceled = new CancellationTokenSource();
        var input = Encoding.UTF8.GetBytes("ab ab");
        var calls = 0;
        var exception = Assert.Throws<OperationCanceledException>(() => regex.Subn(
            input,
            0,
            (int state, Utf8PythonDetailedMatchData match) => { if (++calls == 1) { canceled.Cancel(); } return "[" + match.Value.ValueText + "]"; },
            0,
            0,
            canceled.Token));
        Assert.Equal(canceled.Token, exception.CancellationToken);
        Assert.Equal(1, calls);
        Assert.True(regex.DebugUsesOwnedSlice);
    }

    [Fact]
    public void CancelInsideUtf8EvaluatorAbortsSubn()
    {
        var regex = Create();
        using var canceled = new CancellationTokenSource();
        var input = Encoding.UTF8.GetBytes("cat horse cat");
        var calls = 0;
        var exception = Assert.Throws<OperationCanceledException>(() => regex.Subn(
            input,
            0,
            (int state, Utf8PythonDetailedMatchData match) => { if (++calls == 1) { canceled.Cancel(); } return Encoding.UTF8.GetBytes("[" + match.Value.ValueText + "]"); },
            0,
            0,
            canceled.Token));
        Assert.Equal(canceled.Token, exception.CancellationToken);
        Assert.Equal(1, calls);
    }

    [Fact]
    public void CancelInsideEvaluatorAbortsSubnToString()
    {
        var regex = Create();
        using var canceled = new CancellationTokenSource();
        var input = Encoding.UTF8.GetBytes("cat horse cat");
        var calls = 0;
        var exception = Assert.Throws<OperationCanceledException>(() => regex.SubnToString(
            input,
            0,
            (int state, Utf8PythonDetailedMatchData match) => { if (++calls == 1) { canceled.Cancel(); } return "[" + match.Value.ValueText + "]"; },
            0,
            0,
            canceled.Token));
        Assert.Equal(canceled.Token, exception.CancellationToken);
        Assert.Equal(1, calls);
    }

    [Fact]
    public void UncanceledEvaluatorParity()
    {
        var regex = Create();
        var token = CancellationToken.None;
        var input = Encoding.UTF8.GetBytes("cat horse cat");
        var plainCalls = 0;
        var tokenCalls = 0;
        var plain = regex.Subn(input, 0, (int state, Utf8PythonDetailedMatchData match) => { plainCalls++; return "[" + match.Value.ValueText + "]"; }, 0, 0);
        var withToken = regex.Subn(input, 0, (int state, Utf8PythonDetailedMatchData match) => { tokenCalls++; return "[" + match.Value.ValueText + "]"; }, 0, 0, token);
        Assert.Equal(plain.ResultBytes, withToken.ResultBytes);
        Assert.Equal(plain.ReplacementCount, withToken.ReplacementCount);
        Assert.Equal(plainCalls, tokenCalls);
    }
}
