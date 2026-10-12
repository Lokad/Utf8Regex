using Lokad.Utf8Regex.Internal;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Pcre2.Tests;

[Collection(Pcre2AllocationTestCollection.Name)]
public sealed class Pcre2AnchoredPartialProbeTests
{
    // Frozen PCRE2 10.47 / private PCRE.NET 1.5.0 oracle outcomes. No native test dependency.
    public static IEnumerable<object[]> NativeCases()
    {
        yield return ["(a)b+", "xabbb", 0, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["(a)b+", "xabbb", 0, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["(a)b+", "xabbb", 1, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.FullMatch, 1, 5];
        yield return ["(a)b+", "xabbb", 1, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.PartialMatch, 1, 5];
        yield return ["(a)b+", "xa", 0, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["(a)b+", "xa", 0, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["(a)b+", "xa", 1, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.PartialMatch, 1, 2];
        yield return ["(a)b+", "xa", 1, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.PartialMatch, 1, 2];
        yield return ["cat|horse", "xhorse", 0, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["cat|horse", "xhorse", 0, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["cat|horse", "hors", 0, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.PartialMatch, 0, 4];
        yield return ["cat|horse", "hors", 0, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.PartialMatch, 0, 4];
        yield return ["cat|horse", "xhors", 0, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["cat|horse", "xhors", 0, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["dog(sbody)?", "xdogsbody", 0, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["dog(sbody)?", "xdogsbody", 0, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["dog(sbody)?", "dogs", 0, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.FullMatch, 0, 3];
        yield return ["dog(sbody)?", "dogs", 0, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.PartialMatch, 0, 4];
        yield return ["dog(sbody)??", "dogs", 0, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.FullMatch, 0, 3];
        yield return ["dog(sbody)??", "dogs", 0, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.FullMatch, 0, 3];
        yield return ["\\babc\\b", "x abc", 0, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["\\babc\\b", "x abc", 0, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["\\babc\\b", "aabc", 1, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["\\babc\\b", "aabc", 1, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["\\babc\\b", "abc", 0, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.FullMatch, 0, 3];
        yield return ["\\babc\\b", "abc", 0, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.PartialMatch, 0, 3];
        yield return ["abc\\K123", "abc123", 0, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.FullMatch, 3, 6];
        yield return ["abc\\K123", "abc123", 0, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.FullMatch, 3, 6];
        yield return ["abc\\K123", "abc12", 0, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.PartialMatch, 0, 5];
        yield return ["abc\\K123", "abc12", 0, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.PartialMatch, 0, 5];
        yield return ["abc\\K123", "xabc12", 0, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["abc\\K123", "xabc12", 0, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["(?<=abc)123", "abc123", 3, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.FullMatch, 3, 6];
        yield return ["(?<=abc)123", "abc123", 3, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.FullMatch, 3, 6];
        yield return ["(?<=abc)123", "abc12", 3, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.PartialMatch, 3, 5];
        yield return ["(?<=abc)123", "abc12", 3, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.PartialMatch, 3, 5];
        yield return ["(?<=abc)123", "abc12", 0, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["(?<=abc)123", "abc12", 0, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["(?<=pqr)abc(?=xyz)", "pqrabcxy", 3, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.PartialMatch, 3, 8];
        yield return ["(?<=pqr)abc(?=xyz)", "pqrabcxy", 3, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.PartialMatch, 3, 8];
        yield return ["abc(?=xyz)", "xabcxy", 0, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["abc(?=xyz)", "xabcxy", 0, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["\\Z", "x\n", 0, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["\\Z", "x\n", 0, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["\\Z", "x\n", 1, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.FullMatch, 1, 1];
        yield return ["\\Z", "x\n", 1, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.PartialMatch, 1, 2];
        yield return ["\\Z", "x\n", 2, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.FullMatch, 2, 2];
        yield return ["\\Z", "x\n", 2, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.PartialMatch, 2, 2];
        yield return ["c*+(?<=[bc])", "cccx", 0, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.FullMatch, 0, 3];
        yield return ["c*+(?<=[bc])", "cccx", 0, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.FullMatch, 0, 3];
        yield return ["c*+(?<=[bc])", "b", 1, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.FullMatch, 1, 1];
        yield return ["c*+(?<=[bc])", "b", 1, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.PartialMatch, 1, 1];
        yield return ["c++(?<=[bc])", "b", 1, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["c++(?<=[bc])", "b", 1, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["(?![ab]).*", "a", 0, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["(?![ab]).*", "a", 0, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["(?![ab]).*", "", 0, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.FullMatch, 0, 0];
        yield return ["(?![ab]).*", "", 0, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.PartialMatch, 0, 0];
        yield return ["abc$", "xabc", 0, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["abc$", "xabc", 0, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["abc$", "ab", 0, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.PartialMatch, 0, 2];
        yield return ["abc$", "ab", 0, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.PartialMatch, 0, 2];
        yield return ["abc$", "bc", 0, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["abc$", "bc", 0, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["^(a)++\\w", "xaaab", 1, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["^(a)++\\w", "xaaab", 1, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["^foo", "xfoo", 1, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["^foo", "xfoo", 1, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
    }

    [Theory]
    [MemberData(nameof(NativeCases))]
    public void AnchoredProbeUsesConsumedCandidateAndWholeSubject(string pattern, string text,
        int start, Pcre2PartialMode mode, Utf8Pcre2ProbeKind kind, int begin, int end)
    {
        var regex = new Utf8Pcre2Regex(pattern);
        var input = System.Text.Encoding.UTF8.GetBytes(text);
        var probe = regex.Probe(input, mode, start, Pcre2MatchOptions.Anchored);
        Assert.Equal(kind, probe.Kind);
        Assert.Equal((begin, end), (probe.Value.StartOffsetInBytes, probe.Value.EndOffsetInBytes));
    }

    [Theory]
    [InlineData(Pcre2PartialMode.Soft)] [InlineData(Pcre2PartialMode.Hard)]
    public void CompiledAnchoringHasTheSameCandidateRestriction(Pcre2PartialMode mode)
    {
        var regex = new Utf8Pcre2Regex("(a)b+", Pcre2CompileOptions.Anchored);
        Assert.Equal(Utf8Pcre2ProbeKind.NoMatch, regex.Probe("xabbb"u8, mode).Kind);
        Assert.NotEqual(Utf8Pcre2ProbeKind.NoMatch, regex.Probe("xabbb"u8, mode, 1).Kind);
    }

    [Fact]
    public void AnchoredPartialIsInlineAndFullCaptureAdmissionPreservesSnapshots()
    {
        var capacity = Utf8ConstructionBudget.ArrayBytes<Pcre2GroupData>(2);
        Utf8Pcre2Regex Create(ulong resultBytes) => Utf8Pcre2Regex.Create("(?<head>a)b+",
            Pcre2CompileOptions.None, default,
            new() { MaxWorkSteps = 100000, MaxDepth = 256, MaxWorkingBytes = 1 << 20, MaxResultBytes = resultBytes },
            Timeout.InfiniteTimeSpan);
        Assert.Equal(Utf8Pcre2ProbeKind.PartialMatch, Create(1).Probe("😀abbb"u8, Pcre2PartialMode.Hard, 4, Pcre2MatchOptions.Anchored).Kind);
        Assert.Equal(Pcre2ErrorKind.ResultLimit, Assert.Throws<Pcre2MatchException>(() =>
        { _ = Create(capacity - 1).Probe("😀abbb"u8, Pcre2PartialMode.Soft, 4, Pcre2MatchOptions.Anchored); }).ErrorKind);
        var regex = Create(capacity);
        var retained = regex.Probe("😀abbb"u8, Pcre2PartialMode.Soft, 4, Pcre2MatchOptions.Anchored).GetMatch();
        Assert.True(retained.TryGetFirstSetGroup("head", out var alias));
        Assert.Equal(Utf8Pcre2ProbeKind.NoMatch, regex.Probe("😀abbb"u8, Pcre2PartialMode.Hard, 0, Pcre2MatchOptions.Anchored).Kind);
        Assert.Equal("abbb", retained.GetValueString());
        Assert.Equal("a", alias.GetValueString());
        Assert.Equal("a", regex.Probe("😀abbb"u8, Pcre2PartialMode.Soft, 4, Pcre2MatchOptions.Anchored).GetMatch().GetGroup(1).GetValueString());
        Assert.Equal(Pcre2ErrorKind.MatchLimit, Assert.Throws<Pcre2MatchException>(() =>
        { _ = new Utf8Pcre2Regex("(a)b+", Pcre2CompileOptions.None, default, new() { MatchLimit = 1 }, Timeout.InfiniteTimeSpan)
            .Probe("abbb"u8, Pcre2PartialMode.Hard, 0, Pcre2MatchOptions.Anchored); }).ErrorKind);
        Assert.Equal(Pcre2ErrorKind.HeapLimit, Assert.Throws<Pcre2MatchException>(() =>
        { _ = new Utf8Pcre2Regex("(a)b+", Pcre2CompileOptions.None, default, new() { HeapLimitInBytes = 1 }, Timeout.InfiniteTimeSpan)
            .Probe("abbb"u8, Pcre2PartialMode.Soft, 0, Pcre2MatchOptions.Anchored); }).ErrorKind);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void AnchoredDiscoveryKeepsOriginalInterruption(bool cancel)
    {
        using var source = new CancellationTokenSource();
        var regex = new Utf8Pcre2Regex("(a)b+", Pcre2CompileOptions.None, default, default, TimeSpan.FromMilliseconds(20));
        var polls = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() =>
        {
            if (++polls < 3) return 0;
            if (cancel) { source.Cancel(); return 0; }
            return 25;
        });
        Exception? failure = null;
        try { _ = regex.Probe("abbb"u8, Pcre2PartialMode.Hard, 0, Pcre2MatchOptions.Anchored, source.Token); }
        catch (Exception error) { failure = error; }
        if (cancel) Assert.Equal(source.Token, Assert.IsType<OperationCanceledException>(failure).CancellationToken);
        else Assert.Equal(Pcre2ErrorKind.Timeout, Assert.IsType<Pcre2MatchException>(failure).ErrorKind);
    }
}
