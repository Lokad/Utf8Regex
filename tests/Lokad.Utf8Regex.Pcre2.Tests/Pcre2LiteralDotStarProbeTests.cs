using System.Text;
using Lokad.Utf8Regex.Internal;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Pcre2.Tests;

[Collection(Pcre2AllocationTestCollection.Name)]
public sealed class Pcre2LiteralDotStarProbeTests
{
    // Frozen PCRE2 10.47 / private PCRE.NET 1.5.0 outcomes; no native test dependency.
    public static IEnumerable<object[]> NativeCases()
    {
        yield return ["for", 0, true, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.FullMatch, 0, 3];
        yield return ["for", 0, true, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.FullMatch, 0, 3];
        yield return ["for", 0, true, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.PartialMatch, 0, 3];
        yield return ["f", 0, false, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.FullMatch, 0, 1];
        yield return ["f", 0, false, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.FullMatch, 0, 1];
        yield return ["f", 0, false, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.PartialMatch, 0, 1];
        yield return ["fish", 0, false, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.FullMatch, 0, 4];
        yield return ["fish", 0, false, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.FullMatch, 0, 4];
        yield return ["fish", 0, false, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.PartialMatch, 0, 4];
        yield return ["xfor", 0, true, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.FullMatch, 1, 4];
        yield return ["xfor", 0, true, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.FullMatch, 1, 4];
        yield return ["xfor", 0, true, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.PartialMatch, 1, 4];
        yield return ["xfor", 0, true, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.Anchored, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["xfor", 0, true, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.Anchored, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["xfor", 0, true, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.Anchored, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["xfor", 1, true, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.Anchored, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.FullMatch, 1, 4];
        yield return ["xfor", 1, true, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.Anchored, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.FullMatch, 1, 4];
        yield return ["xfor", 1, true, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.Anchored, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.PartialMatch, 1, 4];
        yield return ["ffoo", 1, false, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.FullMatch, 1, 4];
        yield return ["ffoo", 1, false, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.FullMatch, 1, 4];
        yield return ["ffoo", 1, false, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.PartialMatch, 1, 4];
        yield return ["abc", 0, false, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["abc", 0, false, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["abc", 0, false, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["", 0, true, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["", 0, true, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["", 0, true, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["f", 1, true, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["f", 1, true, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["f", 1, true, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["f\ny", 0, false, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.FullMatch, 0, 1];
        yield return ["f\ny", 0, false, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.FullMatch, 0, 1];
        yield return ["f\ny", 0, false, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.FullMatch, 0, 1];
        yield return ["f\ny", 0, true, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.FullMatch, 0, 3];
        yield return ["f\ny", 0, true, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.FullMatch, 0, 3];
        yield return ["f\ny", 0, true, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.PartialMatch, 0, 3];
        yield return ["\ud83d\ude00f\ud83d\ude00\u00e9", 0, false, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.FullMatch, 4, 11];
        yield return ["\ud83d\ude00f\ud83d\ude00\u00e9", 0, false, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.FullMatch, 4, 11];
        yield return ["\ud83d\ude00f\ud83d\ude00\u00e9", 0, false, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.PartialMatch, 4, 11];
        yield return ["\ud83d\ude00f\ud83d\ude00\u00e9", 4, false, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.Anchored, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.FullMatch, 4, 11];
        yield return ["\ud83d\ude00f\ud83d\ude00\u00e9", 4, false, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.Anchored, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.FullMatch, 4, 11];
        yield return ["\ud83d\ude00f\ud83d\ude00\u00e9", 4, false, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.Anchored, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.PartialMatch, 4, 11];
        yield return ["f\r\nx", 0, false, Pcre2NewlineConvention.Crlf, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.FullMatch, 0, 1];
        yield return ["f\r\nx", 0, false, Pcre2NewlineConvention.Crlf, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.FullMatch, 0, 1];
        yield return ["f\r\nx", 0, false, Pcre2NewlineConvention.Crlf, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.FullMatch, 0, 1];
        yield return ["f\r", 0, false, Pcre2NewlineConvention.Crlf, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.FullMatch, 0, 2];
        yield return ["f\r", 0, false, Pcre2NewlineConvention.Crlf, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.FullMatch, 0, 2];
        yield return ["f\r", 0, false, Pcre2NewlineConvention.Crlf, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.PartialMatch, 0, 2];
        yield return ["f\r", 0, true, Pcre2NewlineConvention.Crlf, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.FullMatch, 0, 2];
        yield return ["f\r", 0, true, Pcre2NewlineConvention.Crlf, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.FullMatch, 0, 2];
        yield return ["f\r", 0, true, Pcre2NewlineConvention.Crlf, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.PartialMatch, 0, 2];
        yield return ["f\nx", 0, false, Pcre2NewlineConvention.Crlf, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.FullMatch, 0, 3];
        yield return ["f\nx", 0, false, Pcre2NewlineConvention.Crlf, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.FullMatch, 0, 3];
        yield return ["f\nx", 0, false, Pcre2NewlineConvention.Crlf, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.PartialMatch, 0, 3];
        yield return ["f\rx", 0, false, Pcre2NewlineConvention.Crlf, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.FullMatch, 0, 3];
        yield return ["f\rx", 0, false, Pcre2NewlineConvention.Crlf, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.FullMatch, 0, 3];
        yield return ["f\rx", 0, false, Pcre2NewlineConvention.Crlf, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.PartialMatch, 0, 3];
        yield return ["f\u2028x", 0, false, Pcre2NewlineConvention.Any, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.FullMatch, 0, 1];
        yield return ["f\u2028x", 0, false, Pcre2NewlineConvention.Any, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.FullMatch, 0, 1];
        yield return ["f\u2028x", 0, false, Pcre2NewlineConvention.Any, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.FullMatch, 0, 1];
        yield return ["f\u0000x", 0, false, Pcre2NewlineConvention.Nul, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.FullMatch, 0, 1];
        yield return ["f\u0000x", 0, false, Pcre2NewlineConvention.Nul, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.FullMatch, 0, 1];
        yield return ["f\u0000x", 0, false, Pcre2NewlineConvention.Nul, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.FullMatch, 0, 1];
        yield return ["f\rx", 0, false, Pcre2NewlineConvention.Cr, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.FullMatch, 0, 1];
        yield return ["f\rx", 0, false, Pcre2NewlineConvention.Cr, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.FullMatch, 0, 1];
        yield return ["f\rx", 0, false, Pcre2NewlineConvention.Cr, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.FullMatch, 0, 1];
        yield return ["f\ny", 0, false, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.NotEmpty | Pcre2MatchOptions.NotBol | Pcre2MatchOptions.NotEol, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.FullMatch, 0, 1];
        yield return ["f\ny", 0, false, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.NotEmpty | Pcre2MatchOptions.NotBol | Pcre2MatchOptions.NotEol, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.FullMatch, 0, 1];
        yield return ["f\ny", 0, false, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.NotEmpty | Pcre2MatchOptions.NotBol | Pcre2MatchOptions.NotEol, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.FullMatch, 0, 1];
        yield return ["xfor", 0, true, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.NotEmptyAtStart, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.FullMatch, 1, 4];
        yield return ["xfor", 0, true, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.NotEmptyAtStart, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.FullMatch, 1, 4];
        yield return ["xfor", 0, true, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.NotEmptyAtStart, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.PartialMatch, 1, 4];
    }

    [Theory]
    [MemberData(nameof(NativeCases))]
    public void LiteralDotStarPreservesNativeScalarAndNewlineOutcome(string text, int start,
        bool dotAll, Pcre2NewlineConvention newline, Pcre2MatchOptions flags, Pcre2PartialMode mode,
        Utf8Pcre2ProbeKind kind, int begin, int end)
    {
        var regex = new Utf8Pcre2Regex("f.*", dotAll ? Pcre2CompileOptions.DotAll : Pcre2CompileOptions.None,
            new() { Newline = newline }, default, Timeout.InfiniteTimeSpan);
        Assert.NotEqual(Pcre2PartialProbeProgramKind.None, regex.DebugCompiledProgram.PartialProbe.Kind);
        var input = Encoding.UTF8.GetBytes(text);
        var probe = regex.Probe(input, mode, start, flags);
        Assert.Equal(kind, probe.Kind);
        Assert.Equal((begin, end), (probe.Value.StartOffsetInBytes, probe.Value.EndOffsetInBytes));
        if (kind == Utf8Pcre2ProbeKind.FullMatch)
        {
            var match = probe.GetMatch();
            Assert.Equal(1, match.CaptureSlotCount);
            Assert.Equal(Encoding.UTF8.GetString(input.AsSpan(begin, end - begin)), match.GetValueString());
        }
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void AdmittedInlineDotOptionAndCompiledAnchorKeepOriginalSubject(bool dotAll)
    {
        var pattern = dotAll ? "(?s:f.*)" : "(?-s:f.*)";
        var regex = new Utf8Pcre2Regex(pattern, Pcre2CompileOptions.Anchored | Pcre2CompileOptions.DotAll);
        Assert.Equal(Utf8Pcre2ProbeKind.NoMatch, regex.Probe("😀f\né"u8, Pcre2PartialMode.Hard).Kind);
        var probe = regex.Probe("😀f\né"u8, Pcre2PartialMode.Hard, 4);
        Assert.Equal(dotAll ? Utf8Pcre2ProbeKind.PartialMatch : Utf8Pcre2ProbeKind.FullMatch, probe.Kind);
        Assert.Equal((4, dotAll ? 8 : 5), (probe.Value.StartOffsetInBytes, probe.Value.EndOffsetInBytes));
    }

    [Fact]
    public void FullPublicationHasExactResultBoundaryAndRetainedAlias()
    {
        var capacity = Utf8ConstructionBudget.ArrayBytes<Pcre2GroupData>(1);
        Utf8Pcre2Regex Create(ulong bytes) => Utf8Pcre2Regex.Create("f.*", Pcre2CompileOptions.None, default,
            new() { MaxWorkSteps = 100000, MaxDepth = 256, MaxWorkingBytes = 1 << 20, MaxResultBytes = bytes },
            Timeout.InfiniteTimeSpan);
        Assert.Equal(Pcre2ErrorKind.ResultLimit, Assert.Throws<Pcre2MatchException>(() =>
        { _ = Create(capacity - 1).Probe("😀f😀é"u8, Pcre2PartialMode.Soft); }).ErrorKind);
        var regex = Create(capacity);
        var retained = regex.Probe("😀f😀é"u8, Pcre2PartialMode.Soft).GetMatch();
        var alias = retained.GetGroup(0);
        Assert.Equal((4, 11), (alias.StartOffsetInBytes, alias.EndOffsetInBytes));
        Assert.Equal(Utf8Pcre2ProbeKind.NoMatch, regex.Probe("xyz"u8, Pcre2PartialMode.Soft).Kind);
        Assert.Equal("f😀é", retained.GetValueString());
        Assert.Equal("f😀é", alias.GetValueString());
        Assert.Equal(Utf8Pcre2ProbeKind.FullMatch, regex.Probe("😀f😀é"u8, Pcre2PartialMode.Soft).Kind);
        Assert.Equal(Utf8Pcre2ProbeKind.PartialMatch, Create(1).Probe("😀f😀é"u8, Pcre2PartialMode.Hard).Kind);
    }

    [Fact]
    public void GreedyScalarWorkCannotBypassFiniteAllowance()
    {
        var regex = new Utf8Pcre2Regex("f.*", Pcre2CompileOptions.DotAll, default,
            new() { MatchLimit = 20 }, Timeout.InfiniteTimeSpan);
        var input = Encoding.UTF8.GetBytes("f" + new string('x', 32768));
        Assert.Equal(Pcre2ErrorKind.MatchLimit, Assert.Throws<Pcre2MatchException>(() =>
        { _ = regex.Probe(input, Pcre2PartialMode.Hard); }).ErrorKind);
        Assert.Equal(Utf8Pcre2ProbeKind.PartialMatch, regex.Probe("f"u8, Pcre2PartialMode.Hard).Kind);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void GreedyScalarLoopObservesOriginalInterruption(bool cancel)
    {
        using var source = new CancellationTokenSource();
        var regex = new Utf8Pcre2Regex("f.*", Pcre2CompileOptions.DotAll, default, default, TimeSpan.FromMilliseconds(20));
        var polls = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() =>
        {
            if (++polls < 10) return 0;
            if (cancel) { source.Cancel(); return 0; }
            return 25;
        });
        Exception? failure = null;
        try { _ = regex.Probe("f😀éxyzxyzxyzxyzxyz"u8, Pcre2PartialMode.Hard, 0, Pcre2MatchOptions.None, source.Token); }
        catch (Exception error) { failure = error; }
        if (cancel) Assert.Equal(source.Token, Assert.IsType<OperationCanceledException>(failure).CancellationToken);
        else Assert.Equal(Pcre2ErrorKind.Timeout, Assert.IsType<Pcre2MatchException>(failure).ErrorKind);
    }
}
