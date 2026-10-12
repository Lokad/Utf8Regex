using Lokad.Utf8Regex.Internal;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Pcre2.Tests;

[Collection(Pcre2AllocationTestCollection.Name)]
public sealed class Pcre2PartialFlagTests
{
    // Frozen PCRE2 10.47 / private PCRE.NET 1.5.0 outcomes; no native test dependency.
    public static IEnumerable<object[]> NativeCases()
    {
        yield return ["^(?<last>a)++\\w", "aaab", 0, Pcre2MatchOptions.NotBol, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["^(?<last>a)++\\w", "aaab", 0, Pcre2MatchOptions.NotBol, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["^(?<last>a)++\\w", "aaaa", 0, Pcre2MatchOptions.NotBol, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["^(?<last>a)++\\w", "aaaa", 0, Pcre2MatchOptions.NotBol, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["^(a)++\\w", "aaab", 0, Pcre2MatchOptions.Anchored | Pcre2MatchOptions.NotBol, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["^(a)++\\w", "aaab", 0, Pcre2MatchOptions.Anchored | Pcre2MatchOptions.NotBol, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["abc$", "abc", 0, Pcre2MatchOptions.NotEol, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["abc$", "abc", 0, Pcre2MatchOptions.NotEol, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["abc$", "abc\n", 0, Pcre2MatchOptions.NotEol, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["abc$", "abc\n", 0, Pcre2MatchOptions.NotEol, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["abc$", "ab", 0, Pcre2MatchOptions.NotEol, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.PartialMatch, 0, 2];
        yield return ["abc$", "ab", 0, Pcre2MatchOptions.NotEol, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.PartialMatch, 0, 2];
        yield return ["^abc$", "abc", 0, Pcre2MatchOptions.NotEol, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["^abc$", "abc", 0, Pcre2MatchOptions.NotEol, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["^abc$", "abc\n", 0, Pcre2MatchOptions.NotEol, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["^abc$", "abc\n", 0, Pcre2MatchOptions.NotEol, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["\\Z", "", 0, Pcre2MatchOptions.NotEmpty, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.PartialMatch, 0, 0];
        yield return ["\\Z", "", 0, Pcre2MatchOptions.NotEmpty, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.PartialMatch, 0, 0];
        yield return ["\\Z", "x", 0, Pcre2MatchOptions.NotEmpty, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.PartialMatch, 1, 1];
        yield return ["\\Z", "x", 0, Pcre2MatchOptions.NotEmpty, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.PartialMatch, 1, 1];
        yield return ["\\Z", "x", 0, Pcre2MatchOptions.NotEmptyAtStart, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.FullMatch, 1, 1];
        yield return ["\\Z", "x", 0, Pcre2MatchOptions.NotEmptyAtStart, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.PartialMatch, 1, 1];
        yield return ["\\Z", "x", 1, Pcre2MatchOptions.NotEmptyAtStart, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.PartialMatch, 1, 1];
        yield return ["\\Z", "x", 1, Pcre2MatchOptions.NotEmptyAtStart, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.PartialMatch, 1, 1];
        yield return ["\\Z", "x\n", 1, Pcre2MatchOptions.NotEmptyAtStart, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.FullMatch, 2, 2];
        yield return ["\\Z", "x\n", 1, Pcre2MatchOptions.NotEmptyAtStart, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.PartialMatch, 1, 2];
        yield return ["\\Z", "x\n", 1, Pcre2MatchOptions.Anchored | Pcre2MatchOptions.NotEmptyAtStart, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.PartialMatch, 1, 2];
        yield return ["\\Z", "x\n", 1, Pcre2MatchOptions.Anchored | Pcre2MatchOptions.NotEmptyAtStart, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.PartialMatch, 1, 2];
        yield return ["c*+(?<=[bc])", "b", 1, Pcre2MatchOptions.NotEmpty, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.PartialMatch, 1, 1];
        yield return ["c*+(?<=[bc])", "b", 1, Pcre2MatchOptions.NotEmpty, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.PartialMatch, 1, 1];
        yield return ["c*+(?<=[bc])", "b", 0, Pcre2MatchOptions.NotEmptyAtStart, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.FullMatch, 1, 1];
        yield return ["c*+(?<=[bc])", "b", 0, Pcre2MatchOptions.NotEmptyAtStart, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.PartialMatch, 1, 1];
        yield return ["c*+(?<=[bc])", "b", 1, Pcre2MatchOptions.NotEmptyAtStart, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.PartialMatch, 1, 1];
        yield return ["c*+(?<=[bc])", "b", 1, Pcre2MatchOptions.NotEmptyAtStart, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.PartialMatch, 1, 1];
        yield return ["c*+(?<=[bc])", "cccx", 0, Pcre2MatchOptions.NotEmpty, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.FullMatch, 0, 3];
        yield return ["c*+(?<=[bc])", "cccx", 0, Pcre2MatchOptions.NotEmpty, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.FullMatch, 0, 3];
        yield return ["(?![ab]).*", "", 0, Pcre2MatchOptions.NotEmpty, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.PartialMatch, 0, 0];
        yield return ["(?![ab]).*", "", 0, Pcre2MatchOptions.NotEmpty, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.PartialMatch, 0, 0];
        yield return ["(?![ab]).*", "", 0, Pcre2MatchOptions.NotEmptyAtStart, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.PartialMatch, 0, 0];
        yield return ["(?![ab]).*", "", 0, Pcre2MatchOptions.NotEmptyAtStart, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.PartialMatch, 0, 0];
        yield return ["(?![ab]).*", "a", 0, Pcre2MatchOptions.NotEmptyAtStart, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.FullMatch, 1, 1];
        yield return ["(?![ab]).*", "a", 0, Pcre2MatchOptions.NotEmptyAtStart, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.PartialMatch, 1, 1];
        yield return ["abc\\z", "abc", 0, Pcre2MatchOptions.NotEol, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.FullMatch, 0, 3];
        yield return ["abc\\z", "abc", 0, Pcre2MatchOptions.NotEol, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.PartialMatch, 0, 3];
        yield return ["abc\\Z", "abc", 0, Pcre2MatchOptions.NotEol, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.FullMatch, 0, 3];
        yield return ["abc\\Z", "abc", 0, Pcre2MatchOptions.NotEol, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.PartialMatch, 0, 3];
        yield return ["c*+(?<=[bc])", "", 0, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.PartialMatch, 0, 0];
        yield return ["c*+(?<=[bc])", "", 0, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.PartialMatch, 0, 0];
        yield return ["c*+(?<=[bc])", "", 0, Pcre2MatchOptions.NotEmpty, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.PartialMatch, 0, 0];
        yield return ["c*+(?<=[bc])", "", 0, Pcre2MatchOptions.NotEmpty, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.PartialMatch, 0, 0];
        yield return ["c*+(?<=[bc])", "b", 0, Pcre2MatchOptions.NotEmpty, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.PartialMatch, 1, 1];
        yield return ["c*+(?<=[bc])", "b", 0, Pcre2MatchOptions.NotEmpty, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.PartialMatch, 1, 1];
        yield return ["c++(?<=[bc])", "b", 0, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.PartialMatch, 1, 1];
        yield return ["c++(?<=[bc])", "b", 0, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.PartialMatch, 1, 1];
        yield return ["(?![ab]).*", "a", 0, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.FullMatch, 1, 1];
        yield return ["(?![ab]).*", "a", 0, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.PartialMatch, 1, 1];
        yield return ["(?![ab]).*", "x\ny", 0, Pcre2MatchOptions.NotEmpty, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.FullMatch, 0, 1];
        yield return ["(?![ab]).*", "x\ny", 0, Pcre2MatchOptions.NotEmpty, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.FullMatch, 0, 1];
        yield return ["(?![ab]).*", "ab", 0, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.FullMatch, 2, 2];
        yield return ["(?![ab]).*", "ab", 0, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.PartialMatch, 2, 2];
        yield return ["abc\\B", "abc", 0, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.PartialMatch, 0, 3];
        yield return ["abc\\B", "abc", 0, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.PartialMatch, 0, 3];
    }

    [Theory]
    [MemberData(nameof(NativeCases))]
    public void PartialFlagsPreserveNativeOutcomeAndRange(string pattern, string text,
        int start, Pcre2MatchOptions flags, Pcre2PartialMode mode, Utf8Pcre2ProbeKind kind, int begin, int end)
    {
        var regex = new Utf8Pcre2Regex(pattern);
        var input = System.Text.Encoding.UTF8.GetBytes(text);
        var probe = regex.Probe(input, mode, start, flags);
        Assert.Equal(kind, probe.Kind);
        Assert.Equal((begin, end), (probe.Value.StartOffsetInBytes, probe.Value.EndOffsetInBytes));
    }

    [Fact]
    public void DollarNotEolAlsoUsesCorrectNormalMatcherSemantics()
    {
        var regex = new Utf8Pcre2Regex("abc$");
        Assert.False(regex.MatchDetailed("abc\n"u8, 0, Pcre2MatchOptions.NotEol).Success);
        Assert.True(regex.MatchDetailed("abc\n"u8).Success);
        Assert.True(new Utf8Pcre2Regex(@"abc\Z").MatchDetailed("abc\n"u8, 0, Pcre2MatchOptions.NotEol).Success);
    }

    [Fact]
    public void EmptyRejectionKeepsInlinePartialAndAdmittedLaterFullSnapshot()
    {
        Utf8Pcre2Regex Create(ulong resultBytes) => Utf8Pcre2Regex.Create(@"\Z", Pcre2CompileOptions.None, default,
            new() { MaxWorkSteps = 100000, MaxDepth = 256, MaxWorkingBytes = 1 << 20, MaxResultBytes = resultBytes },
            Timeout.InfiniteTimeSpan);
        Assert.Equal(Utf8Pcre2ProbeKind.PartialMatch, Create(1).Probe("x\n"u8, Pcre2PartialMode.Soft, 1,
            Pcre2MatchOptions.Anchored | Pcre2MatchOptions.NotEmptyAtStart).Kind);
        var capacity = Utf8ConstructionBudget.ArrayBytes<Pcre2GroupData>(1);
        Assert.Equal(Pcre2ErrorKind.ResultLimit, Assert.Throws<Pcre2MatchException>(() =>
        { _ = Create(capacity - 1).Probe("x\n"u8, Pcre2PartialMode.Soft, 1, Pcre2MatchOptions.NotEmptyAtStart); }).ErrorKind);
        var regex = Create(capacity);
        var retained = regex.Probe("x\n"u8, Pcre2PartialMode.Soft, 1, Pcre2MatchOptions.NotEmptyAtStart).GetMatch();
        Assert.Equal((2, 2), (retained.GetGroup(0).StartOffsetInBytes, retained.GetGroup(0).EndOffsetInBytes));
        Assert.Equal(Utf8Pcre2ProbeKind.PartialMatch, regex.Probe(""u8, Pcre2PartialMode.Soft, 0, Pcre2MatchOptions.NotEmpty).Kind);
        Assert.True(retained.GetGroup(0).Success);
        Assert.Equal("", retained.GetValueString());
        Assert.Equal(2, regex.Probe("x\n"u8, Pcre2PartialMode.Soft, 1, Pcre2MatchOptions.NotEmptyAtStart).Value.StartOffsetInBytes);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void RejectedEmptyStillObservesOriginalInterruption(bool cancel)
    {
        using var source = new CancellationTokenSource();
        var regex = new Utf8Pcre2Regex(@"\Z", Pcre2CompileOptions.None, default, default, TimeSpan.FromMilliseconds(20));
        var polls = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() =>
        {
            if (++polls < 3) return 0;
            if (cancel) { source.Cancel(); return 0; }
            return 25;
        });
        Exception? failure = null;
        try { _ = regex.Probe("x\n"u8, Pcre2PartialMode.Soft, 1, Pcre2MatchOptions.NotEmptyAtStart, source.Token); }
        catch (Exception error) { failure = error; }
        if (cancel) Assert.Equal(source.Token, Assert.IsType<OperationCanceledException>(failure).CancellationToken);
        else Assert.Equal(Pcre2ErrorKind.Timeout, Assert.IsType<Pcre2MatchException>(failure).ErrorKind);
    }
}
