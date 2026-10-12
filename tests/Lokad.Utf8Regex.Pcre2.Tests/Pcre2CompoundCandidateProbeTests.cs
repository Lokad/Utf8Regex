using System.Text;
using Lokad.Utf8Regex.Internal;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Pcre2.Tests;

[Collection(Pcre2AllocationTestCollection.Name)]
public sealed class Pcre2CompoundCandidateProbeTests
{
    // Frozen PCRE2 10.47 / private PCRE.NET 1.5.0 outcomes; no native test dependency.
    public static IEnumerable<object[]> NativeCases()
    {
        yield return ["(?>a+b)", "xaab", 0, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.FullMatch, 1, 4];
        yield return ["(?>a+b)", "xaab", 0, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.FullMatch, 1, 4];
        yield return ["(?>a+b)", "xaab", 0, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.FullMatch, 1, 4];
        yield return ["(?>a+b)", "aaax", 0, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["(?>a+b)", "aaax", 0, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["(?>a+b)", "aaax", 0, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["(?>a+b)", "aaa", 0, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["(?>a+b)", "aaa", 0, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.PartialMatch, 0, 3];
        yield return ["(?>a+b)", "aaa", 0, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.PartialMatch, 0, 3];
        yield return ["(?>a+b)", "\ud83d\ude00aab", 4, Pcre2MatchOptions.Anchored, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.FullMatch, 4, 7];
        yield return ["(?>a+b)", "\ud83d\ude00aab", 4, Pcre2MatchOptions.Anchored, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.FullMatch, 4, 7];
        yield return ["(?>a+b)", "\ud83d\ude00aab", 4, Pcre2MatchOptions.Anchored, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.FullMatch, 4, 7];
        yield return ["(?>a+b)", "xaab", 0, Pcre2MatchOptions.Anchored, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["(?>a+b)", "xaab", 0, Pcre2MatchOptions.Anchored, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["(?>a+b)", "xaab", 0, Pcre2MatchOptions.Anchored, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["(?>a+b)", "aax aab", 0, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.FullMatch, 4, 7];
        yield return ["(?>a+b)", "aax aab", 0, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.FullMatch, 4, 7];
        yield return ["(?>a+b)", "aax aab", 0, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.FullMatch, 4, 7];
        yield return ["(abc)(?1)", "xabcabc", 0, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.FullMatch, 1, 7];
        yield return ["(abc)(?1)", "xabcabc", 0, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.FullMatch, 1, 7];
        yield return ["(abc)(?1)", "xabcabc", 0, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.FullMatch, 1, 7];
        yield return ["(abc)(?1)", "xabcab", 0, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["(abc)(?1)", "xabcab", 0, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.PartialMatch, 1, 6];
        yield return ["(abc)(?1)", "xabcab", 0, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.PartialMatch, 1, 6];
        yield return ["(abc)(?1)", "abcxabcabc", 0, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.FullMatch, 4, 10];
        yield return ["(abc)(?1)", "abcxabcabc", 0, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.FullMatch, 4, 10];
        yield return ["(abc)(?1)", "abcxabcabc", 0, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.FullMatch, 4, 10];
        yield return ["(abc)(?1)", "\ud83d\ude00abcabc", 4, Pcre2MatchOptions.Anchored, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.FullMatch, 4, 10];
        yield return ["(abc)(?1)", "\ud83d\ude00abcabc", 4, Pcre2MatchOptions.Anchored, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.FullMatch, 4, 10];
        yield return ["(abc)(?1)", "\ud83d\ude00abcabc", 4, Pcre2MatchOptions.Anchored, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.FullMatch, 4, 10];
        yield return ["(abc)(?1)", "xabcabc", 0, Pcre2MatchOptions.Anchored, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["(abc)(?1)", "xabcabc", 0, Pcre2MatchOptions.Anchored, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["(abc)(?1)", "xabcabc", 0, Pcre2MatchOptions.Anchored, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["(abc)(?1)", "abcabc", 0, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.FullMatch, 0, 6];
        yield return ["(abc)(?1)", "abcabc", 0, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.FullMatch, 0, 6];
        yield return ["(abc)(?1)", "abcabc", 0, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.FullMatch, 0, 6];
        yield return ["(abc)++x", "xabcabcx", 0, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.FullMatch, 1, 8];
        yield return ["(abc)++x", "xabcabcx", 0, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.FullMatch, 1, 8];
        yield return ["(abc)++x", "xabcabcx", 0, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.FullMatch, 1, 8];
        yield return ["(abc)++x", "abcqabcx", 0, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.FullMatch, 4, 8];
        yield return ["(abc)++x", "abcqabcx", 0, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.FullMatch, 4, 8];
        yield return ["(abc)++x", "abcqabcx", 0, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.FullMatch, 4, 8];
        yield return ["(abc)++x", "abcabc", 0, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["(abc)++x", "abcabc", 0, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.PartialMatch, 0, 6];
        yield return ["(abc)++x", "abcabc", 0, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.PartialMatch, 0, 6];
        yield return ["(abc)++x", "\ud83d\ude00abcx", 4, Pcre2MatchOptions.Anchored, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.FullMatch, 4, 8];
        yield return ["(abc)++x", "\ud83d\ude00abcx", 4, Pcre2MatchOptions.Anchored, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.FullMatch, 4, 8];
        yield return ["(abc)++x", "\ud83d\ude00abcx", 4, Pcre2MatchOptions.Anchored, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.FullMatch, 4, 8];
        yield return ["(abc)++x", "xabcx", 0, Pcre2MatchOptions.Anchored, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["(abc)++x", "xabcx", 0, Pcre2MatchOptions.Anchored, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["(abc)++x", "xabcx", 0, Pcre2MatchOptions.Anchored, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["(abc)++x", "abcdefgh", 0, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["(abc)++x", "abcdefgh", 0, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["(abc)++x", "abcdefgh", 0, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["(?(?=abc).*|Z)", "xZ", 0, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.FullMatch, 1, 2];
        yield return ["(?(?=abc).*|Z)", "xZ", 0, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.FullMatch, 1, 2];
        yield return ["(?(?=abc).*|Z)", "xZ", 0, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.FullMatch, 1, 2];
        yield return ["(?(?=abc).*|Z)", "xabc\ny", 0, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.FullMatch, 1, 4];
        yield return ["(?(?=abc).*|Z)", "xabc\ny", 0, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.FullMatch, 1, 4];
        yield return ["(?(?=abc).*|Z)", "xabc\ny", 0, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.FullMatch, 1, 4];
        yield return ["(?(?=abc).*|Z)", "abc", 0, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.FullMatch, 0, 3];
        yield return ["(?(?=abc).*|Z)", "abc", 0, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.FullMatch, 0, 3];
        yield return ["(?(?=abc).*|Z)", "abc", 0, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.PartialMatch, 0, 3];
        yield return ["(?(?=abc).*|Z)", "ab", 0, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["(?(?=abc).*|Z)", "ab", 0, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.PartialMatch, 0, 2];
        yield return ["(?(?=abc).*|Z)", "ab", 0, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.PartialMatch, 0, 2];
        yield return ["(?(?=abc).*|Z)", "", 0, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["(?(?=abc).*|Z)", "", 0, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.PartialMatch, 0, 0];
        yield return ["(?(?=abc).*|Z)", "", 0, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.PartialMatch, 0, 0];
        yield return ["(?(?=abc).*|Z)", "abZ", 0, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.FullMatch, 2, 3];
        yield return ["(?(?=abc).*|Z)", "abZ", 0, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.FullMatch, 2, 3];
        yield return ["(?(?=abc).*|Z)", "abZ", 0, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.FullMatch, 2, 3];
        yield return ["(?(?=abc).*|Z)", "\ud83d\ude00abc\ud83d\ude00\nx", 0, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.FullMatch, 4, 11];
        yield return ["(?(?=abc).*|Z)", "\ud83d\ude00abc\ud83d\ude00\nx", 0, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.FullMatch, 4, 11];
        yield return ["(?(?=abc).*|Z)", "\ud83d\ude00abc\ud83d\ude00\nx", 0, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.FullMatch, 4, 11];
        yield return ["(?(?=abc).*|Z)", "xZ", 0, Pcre2MatchOptions.Anchored, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["(?(?=abc).*|Z)", "xZ", 0, Pcre2MatchOptions.Anchored, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["(?(?=abc).*|Z)", "xZ", 0, Pcre2MatchOptions.Anchored, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["(?(?=abc).*|Z)", "xZ", 1, Pcre2MatchOptions.Anchored, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.FullMatch, 1, 2];
        yield return ["(?(?=abc).*|Z)", "xZ", 1, Pcre2MatchOptions.Anchored, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.FullMatch, 1, 2];
        yield return ["(?(?=abc).*|Z)", "xZ", 1, Pcre2MatchOptions.Anchored, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.FullMatch, 1, 2];
        yield return ["(abc)++x", "abcx", 0, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.FullMatch, 0, 4];
        yield return ["(abc)++x", "abcx", 0, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.FullMatch, 0, 4];
        yield return ["(abc)++x", "abcx", 0, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.FullMatch, 0, 4];
    }

    [Theory]
    [MemberData(nameof(NativeCases))]
    public void CompoundProbesPreserveNativeCandidatesAndPrecedence(string pattern, string text, int start,
        Pcre2MatchOptions flags, Pcre2PartialMode mode, Utf8Pcre2ProbeKind kind, int begin, int end)
    {
        var regex = new Utf8Pcre2Regex(pattern);
        var input = Encoding.UTF8.GetBytes(text);
        var probe = regex.Probe(input, mode, start, flags);
        Assert.Equal(kind, probe.Kind);
        Assert.Equal((begin, end), (probe.Value.StartOffsetInBytes, probe.Value.EndOffsetInBytes));
        if (kind == Utf8Pcre2ProbeKind.FullMatch)
            Assert.Equal(Encoding.UTF8.GetString(input.AsSpan(begin, end - begin)), probe.GetMatch().GetValueString());
    }
    [Fact]
    public void LaterSubroutineFullPreservesFiniteCapturesAndNamedAlias()
    {
        var capacity = Utf8ConstructionBudget.ArrayBytes<Pcre2GroupData>(2);
        Utf8Pcre2Regex Create(ulong bytes) => Utf8Pcre2Regex.Create("(?<unit>abc)(?1)", Pcre2CompileOptions.None, default,
            new() { MaxWorkSteps = 100000, MaxDepth = 256, MaxWorkingBytes = 1 << 20, MaxResultBytes = bytes },
            Timeout.InfiniteTimeSpan);
        Assert.Equal(Pcre2ErrorKind.ResultLimit, Assert.Throws<Pcre2MatchException>(() =>
        { _ = Create(capacity - 1).Probe("😀xabcabc"u8, Pcre2PartialMode.Soft); }).ErrorKind);
        var regex = Create(capacity);
        var retained = regex.Probe("😀xabcabc"u8, Pcre2PartialMode.Soft).GetMatch();
        Assert.True(retained.TryGetFirstSetGroup("unit", out var alias));
        Assert.Equal((5, 11), (retained.GetGroup(0).StartOffsetInBytes, retained.GetGroup(0).EndOffsetInBytes));
        Assert.Equal((5, 8), (alias.StartOffsetInBytes, alias.EndOffsetInBytes));
        Assert.Equal(Utf8Pcre2ProbeKind.NoMatch, regex.Probe("abcx"u8, Pcre2PartialMode.Soft).Kind);
        Assert.Equal("abcabc", retained.GetValueString());
        Assert.Equal("abc", alias.GetValueString());
        Assert.Equal(Utf8Pcre2ProbeKind.FullMatch, regex.Probe("😀xabcabc"u8, Pcre2PartialMode.Soft).Kind);
        Assert.Equal(Utf8Pcre2ProbeKind.PartialMatch, Create(1).Probe("xabcab"u8, Pcre2PartialMode.Hard).Kind);
    }

    [Theory]
    [InlineData("(?>a+b)")] [InlineData("(abc)(?1)")]
    [InlineData("(abc)++x")] [InlineData("(?(?=abc).*|Z)")]
    public void CompoundCandidateWorkCannotBypassFiniteAllowance(string pattern)
    {
        var regex = new Utf8Pcre2Regex(pattern, Pcre2CompileOptions.None, default,
            new() { MatchLimit = 20 }, Timeout.InfiniteTimeSpan);
        var input = Encoding.UTF8.GetBytes(new string('q', 1000));
        Assert.Equal(Pcre2ErrorKind.MatchLimit, Assert.Throws<Pcre2MatchException>(() =>
        { _ = regex.Probe(input, Pcre2PartialMode.Soft); }).ErrorKind);
        Assert.Equal(Utf8Pcre2ProbeKind.NoMatch, regex.Probe("q"u8, Pcre2PartialMode.Soft, 0, Pcre2MatchOptions.Anchored).Kind);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void CompoundCandidatesObserveOriginalInterruption(bool cancel)
    {
        using var source = new CancellationTokenSource();
        var regex = new Utf8Pcre2Regex("(?>a+b)", Pcre2CompileOptions.None, default, default, TimeSpan.FromMilliseconds(20));
        var polls = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() =>
        {
            if (++polls < 10) return 0;
            if (cancel) { source.Cancel(); return 0; }
            return 25;
        });
        Exception? failure = null;
        try { _ = regex.Probe("qqqqqqqqqqqqqqqqqqqqqqqqqqqaab"u8, Pcre2PartialMode.Soft, 0, Pcre2MatchOptions.None, source.Token); }
        catch (Exception error) { failure = error; }
        if (cancel) Assert.Equal(source.Token, Assert.IsType<OperationCanceledException>(failure).CancellationToken);
        else Assert.Equal(Pcre2ErrorKind.Timeout, Assert.IsType<Pcre2MatchException>(failure).ErrorKind);
    }
}
