using Lokad.Utf8Regex.Internal;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Pcre2.Tests;

[Collection(Pcre2AllocationTestCollection.Name)]
public sealed class Pcre2SpecializedProbeOptionTests
{
    [Theory]
    [InlineData("(a)b+", "xabbbb", 0, Pcre2MatchOptions.Anchored, false)]
    [InlineData("(a)b+", "xabbbb", 1, Pcre2MatchOptions.Anchored, true)]
    [InlineData("(a)b+", "abbbx", 0, Pcre2MatchOptions.EndAnchored, false)]
    [InlineData("(a)b+", "abbb", 0, Pcre2MatchOptions.EndAnchored, true)]
    [InlineData(@"^(?<last>a)++\w", "aaab", 0, Pcre2MatchOptions.NotBol, false)]
    public void NonPartialSpecializedProbeHonorsNativeOptions(string pattern, string text,
        int start, Pcre2MatchOptions options, bool success)
    {
        var regex = new Utf8Pcre2Regex(pattern);
        Assert.NotEqual(Pcre2PartialProbeProgramKind.None, regex.DebugCompiledProgram.PartialProbe.Kind);
        Assert.IsNotType<Pcre2LiteralDirectProgram>(regex.DebugCompiledProgram.Operations.Match);
        var input = System.Text.Encoding.UTF8.GetBytes(text);
        var expected = regex.MatchDetailed(input, start, options);
        Assert.Equal(success, expected.Success);
        var probe = regex.Probe(input, Pcre2PartialMode.None, start, options);
        Assert.Equal(success ? Utf8Pcre2ProbeKind.FullMatch : Utf8Pcre2ProbeKind.NoMatch, probe.Kind);
        if (!success) return;
        var actual = probe.GetMatch();
        Assert.Equal(expected.CaptureSlotCount, actual.CaptureSlotCount);
        for (var slot = 0; slot < actual.CaptureSlotCount; slot++)
        {
            var left = expected.GetGroup(slot);
            var right = actual.GetGroup(slot);
            Assert.Equal(left.Success, right.Success);
            if (!left.Success) continue;
            Assert.Equal((left.StartOffsetInBytes, left.EndOffsetInBytes), (right.StartOffsetInBytes, right.EndOffsetInBytes));
        }
    }

    [Fact]
    public void ExplicitOptionCapturePublicationKeepsFiniteAdmissionAndSnapshots()
    {
        var capacity = Utf8ConstructionBudget.ArrayBytes<Pcre2GroupData>(2);
        Utf8Pcre2Regex Create(ulong resultBytes) => Utf8Pcre2Regex.Create("(?<head>a)b+",
            Pcre2CompileOptions.None, default,
            new() { MaxWorkSteps = 100000, MaxDepth = 256, MaxWorkingBytes = 1 << 20, MaxResultBytes = resultBytes },
            Timeout.InfiniteTimeSpan);
        Assert.Equal(Pcre2ErrorKind.ResultLimit, Assert.Throws<Pcre2MatchException>(() =>
        { _ = Create(capacity - 1).Probe("😀abbb"u8, Pcre2PartialMode.None, 4, Pcre2MatchOptions.Anchored); }).ErrorKind);
        var regex = Create(capacity);
        var retained = regex.Probe("😀abbb"u8, Pcre2PartialMode.None, 4, Pcre2MatchOptions.Anchored).GetMatch();
        Assert.Equal((4, 8), (retained.GetGroup(0).StartOffsetInBytes, retained.GetGroup(0).EndOffsetInBytes));
        Assert.True(retained.TryGetFirstSetGroup("head", out var alias));
        Assert.Equal("a", alias.GetValueString());
        Assert.Equal(Utf8Pcre2ProbeKind.NoMatch, regex.Probe("xabbb"u8, Pcre2PartialMode.None, 0, Pcre2MatchOptions.Anchored).Kind);
        Assert.Equal("abbb", retained.GetValueString());
        Assert.Equal("a", alias.GetValueString());
        Assert.Equal("a", regex.Probe("😀abbb"u8, Pcre2PartialMode.None, 4, Pcre2MatchOptions.Anchored).GetMatch().GetGroup(1).GetValueString());
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void ExplicitOptionsObserveOriginalInterruption(bool cancel)
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
        try { _ = regex.Probe("abbb"u8, Pcre2PartialMode.None, 0, Pcre2MatchOptions.Anchored, source.Token); }
        catch (Exception error) { failure = error; }
        if (cancel) Assert.Equal(source.Token, Assert.IsType<OperationCanceledException>(failure).CancellationToken);
        else Assert.Equal(Pcre2ErrorKind.Timeout, Assert.IsType<Pcre2MatchException>(failure).ErrorKind);
    }
}
