using System.Text;
using Lokad.Utf8Regex.Internal;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Pcre2.Tests;

[Collection(Pcre2AllocationTestCollection.Name)]
public sealed class Pcre2PartialProbeAuthorityTests
{
    [Theory]
    [InlineData("(a)b+", "abbbb")]
    [InlineData("(a)b+", "zzzz")]
    public void SpecializedProbeDiscoveryCannotBypassWork(string pattern, string text)
    {
        var regex = new Utf8Pcre2Regex(pattern, Pcre2CompileOptions.None, default,
            new() { MatchLimit = 1 }, Timeout.InfiniteTimeSpan);
        Assert.NotEqual(Pcre2PartialProbeProgramKind.None, regex.DebugCompiledProgram.PartialProbe.Kind);
        var input = Encoding.UTF8.GetBytes(text);
        Assert.Equal(Pcre2ErrorKind.MatchLimit, Assert.Throws<Pcre2MatchException>(() =>
        { _ = regex.Probe(input, Pcre2PartialMode.Soft); }).ErrorKind);
    }

    [Fact]
    public void SpecializedFullProbeAdmitsItsOwnedArray()
    {
        var regex = new Utf8Pcre2Regex("(a)b+", Pcre2CompileOptions.None, default,
            new() { HeapLimitInBytes = 1 }, Timeout.InfiniteTimeSpan);
        Assert.Equal(Pcre2ErrorKind.HeapLimit, Assert.Throws<Pcre2MatchException>(() =>
        { _ = regex.Probe("abbb"u8, Pcre2PartialMode.Soft); }).ErrorKind);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void SpecializedLoopObservesOriginalInterruption(bool cancel)
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
        var input = new byte[32768]; input.AsSpan().Fill((byte)'z');
        Exception? failure = null;
        try { _ = regex.Probe(input, Pcre2PartialMode.Soft, 0, Pcre2MatchOptions.None, source.Token); }
        catch (Exception error) { failure = error; }
        if (cancel) Assert.Equal(source.Token, Assert.IsType<OperationCanceledException>(failure).CancellationToken);
        else Assert.Equal(Pcre2ErrorKind.Timeout, Assert.IsType<Pcre2MatchException>(failure).ErrorKind);
    }

    [Fact]
    public void SpecializedFullSnapshotPreservesItsCaptureNameAndLifetime()
    {
        var regex = new Utf8Pcre2Regex("(?<head>a)b+");
        Assert.Equal(Pcre2PartialProbeProgramKind.AbPlus, regex.DebugCompiledProgram.PartialProbe.Kind);
        var retained = regex.Probe("xabbb"u8, Pcre2PartialMode.Soft).GetMatch();
        Assert.Equal(2, retained.CaptureSlotCount);
        Assert.Equal("a", retained.GetGroup(1).GetValueString());
        Assert.True(retained.TryGetFirstSetGroup("head", out var named));
        Assert.Equal((1, 2), (named.StartOffsetInBytes, named.EndOffsetInBytes));
        Assert.Equal(Utf8Pcre2ProbeKind.NoMatch, regex.Probe("zzz"u8, Pcre2PartialMode.Soft).Kind);
        Assert.Equal("abbb", retained.GetValueString());
        Assert.Equal("a", named.GetValueString());
    }

    [Theory]
    [InlineData(4094)] [InlineData(4095)] [InlineData(4096)]
    [InlineData(8190)] [InlineData(8191)] [InlineData(8192)]
    public void ControlledLiteralAlternationKeepsChunkBoundaryHitsAndPartialTails(int start)
    {
        var regex = new Utf8Pcre2Regex("cat|horse", Pcre2CompileOptions.None, default,
            new() { MatchLimit = 10000, HeapLimitInBytes = 4096 }, Timeout.InfiniteTimeSpan);
        Assert.Equal(Pcre2PartialProbeProgramKind.LiteralAlternation, regex.DebugCompiledProgram.PartialProbe.Kind);
        var hit = Encoding.UTF8.GetBytes(new string('x', start) + "horse");
        var full = regex.Probe(hit, Pcre2PartialMode.Soft);
        Assert.Equal(Utf8Pcre2ProbeKind.FullMatch, full.Kind);
        Assert.Equal((start, start + 5), (full.Value.StartOffsetInBytes, full.Value.EndOffsetInBytes));
        var tail = Encoding.UTF8.GetBytes(new string('x', start) + "hors");
        var partial = regex.Probe(tail, Pcre2PartialMode.Hard);
        Assert.Equal(Utf8Pcre2ProbeKind.PartialMatch, partial.Kind);
        Assert.Equal((start, start + 4), (partial.Value.StartOffsetInBytes, partial.Value.EndOffsetInBytes));
    }

    [Fact]
    public void SpecializedFullArrayHasExactCapacityAndPartialRangeStaysInline()
    {
        var capacity = Utf8ConstructionBudget.ArrayBytes<Pcre2GroupData>(2);
        Utf8Pcre2Regex Create(ulong heap) => new("(a)b+", Pcre2CompileOptions.None, default,
            new() { HeapLimitInBytes = heap }, Timeout.InfiniteTimeSpan);
        Assert.Equal(Utf8Pcre2ProbeKind.FullMatch, Create(capacity).Probe("abbb"u8, Pcre2PartialMode.Soft).Kind);
        Assert.Equal(Pcre2ErrorKind.HeapLimit, Assert.Throws<Pcre2MatchException>(() =>
        { _ = Create(capacity - 1).Probe("abbb"u8, Pcre2PartialMode.Soft); }).ErrorKind);
        Assert.Equal(Utf8Pcre2ProbeKind.PartialMatch, Create(1).Probe("abbb"u8, Pcre2PartialMode.Hard).Kind);
        Assert.Equal(Utf8Pcre2ProbeKind.FullMatch, Create(capacity).Probe("abbb"u8, Pcre2PartialMode.Soft).Kind);
    }
}
