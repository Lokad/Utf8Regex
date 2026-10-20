using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal;
using Lokad.Utf8Regex.Internal.Input;

namespace Lokad.Utf8Regex.Pcre2.Tests;

[Collection(Pcre2AllocationTestCollection.Name)]
public sealed class Pcre2ProbePublicationAuthorityTests
{
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void LiteralFullProbeFundsProjectionAndItsResultArray(bool heap)
    {
        var regex = new Utf8Pcre2Regex("A", Pcre2CompileOptions.None, default,
            new() { MatchLimit = heap ? 0u : 1u, HeapLimitInBytes = heap ? 1UL : 0UL }, Timeout.InfiniteTimeSpan);
        Assert.IsType<Pcre2LiteralDirectProgram>(regex.DebugCompiledProgram.Operations.Match);
        Assert.Equal(heap ? Pcre2ErrorKind.HeapLimit : Pcre2ErrorKind.MatchLimit,
            Assert.Throws<Pcre2MatchException>(() => { _ = regex.Probe("A"u8, Pcre2PartialMode.None); }).ErrorKind);
    }

    [Fact]
    public void LiteralProbeObservesTheOriginalLiveTokenDuringExecution()
    {
        using var source = new CancellationTokenSource();
        var regex = new Utf8Pcre2Regex("A", Pcre2CompileOptions.None, default, default, TimeSpan.FromMilliseconds(20));
        var polls = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() =>
        {
            if (++polls == 2) source.Cancel();
            return 0;
        });
        var failure = Assert.Throws<OperationCanceledException>(() =>
        { _ = regex.Probe("A"u8, Pcre2PartialMode.None, 0, Pcre2MatchOptions.None, source.Token); });
        Assert.True(source.IsCancellationRequested);
        Assert.Equal(source.Token, failure.CancellationToken);
    }

    [Fact]
    public void NonPartialProbePreservesOwnedNamesMarkAndRetainedCaptures()
    {
        var regex = new Utf8Pcre2Regex(@"(?<v>\x41)(😀)");
        var result = regex.Probe("A😀"u8, Pcre2PartialMode.None);
        var retained = result.GetMatch();
        Assert.True(retained.TryGetFirstSetGroup("v", out var named));
        Assert.Equal("A", named.GetValueString());
        Assert.Equal(1, retained.NameEntryCount);
        Assert.Equal("😀", retained.GetGroup(2).GetValueString());
        Assert.Equal(Utf8Pcre2ProbeKind.FullMatch, regex.Probe("xA😀"u8, Pcre2PartialMode.None).Kind);
        Assert.Equal("A😀", retained.GetValueString());
        var marked = new Utf8Pcre2Regex("(*MARK:tag)(é)(😀)").Probe("é😀"u8, Pcre2PartialMode.None);
        Assert.Equal("tag", marked.Mark);
    }

    [Fact]
    public void NonPartialProbeForwardsNativeMatchOptions()
    {
        var regex = new Utf8Pcre2Regex("(?<v>é)(😀)");
        Assert.Equal(Utf8Pcre2ProbeKind.NoMatch,
            regex.Probe("xé😀"u8, Pcre2PartialMode.None, 0, Pcre2MatchOptions.Anchored).Kind);
        Assert.Equal(Utf8Pcre2ProbeKind.FullMatch,
            regex.Probe("xé😀"u8, Pcre2PartialMode.None, 1, Pcre2MatchOptions.Anchored).Kind);
    }

    [Theory]
    [InlineData(Pcre2PartialMode.None, Utf8Pcre2ProbeKind.FullMatch)]
    [InlineData(Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.FullMatch)]
    [InlineData(Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.PartialMatch)]
    public void FiniteLiteralEndAssertionKeepsScalarRanges(Pcre2PartialMode mode, Utf8Pcre2ProbeKind kind)
    {
        var regex = new Utf8Pcre2Regex(@"é😀\z", Pcre2CompileOptions.None, default,
            new() { MatchLimit = 1000, HeapLimitInBytes = 4096 }, Timeout.InfiniteTimeSpan);
        var result = regex.Probe("xé😀"u8, mode, 1, Pcre2MatchOptions.None);
        Assert.Equal(kind, result.Kind);
        Assert.Equal((1, 7), (result.Value.StartOffsetInBytes, result.Value.EndOffsetInBytes));
        Assert.Equal((1, 4), (result.Value.StartOffsetInUtf16, result.Value.EndOffsetInUtf16));
    }

    [Fact]
    public void LiteralFullArrayHasAnExactMajorCapacityCutoffWhilePartialStaysInline()
    {
        var capacity = Utf8ConstructionBudget.ArrayBytes<Pcre2GroupData>(1);
        Utf8Pcre2Regex Create(ulong bytes) => new(@"é😀\z", Pcre2CompileOptions.None, default,
            new() { HeapLimitInBytes = bytes }, Timeout.InfiniteTimeSpan);
        Assert.Equal(Utf8Pcre2ProbeKind.FullMatch, Create(capacity).Probe("é😀"u8, Pcre2PartialMode.Soft).Kind);
        Assert.Equal(Pcre2ErrorKind.HeapLimit, Assert.Throws<Pcre2MatchException>(() =>
        { _ = Create(capacity - 1).Probe("é😀"u8, Pcre2PartialMode.Soft); }).ErrorKind);
        Assert.Equal(Utf8Pcre2ProbeKind.PartialMatch, Create(1).Probe("é😀"u8, Pcre2PartialMode.Hard).Kind);
        var suffix = new Utf8Pcre2Regex("é😀a", Pcre2CompileOptions.None, default,
            new() { HeapLimitInBytes = 1 }, Timeout.InfiniteTimeSpan).Probe("xé😀"u8, Pcre2PartialMode.Soft, 1);
        Assert.Equal(Utf8Pcre2ProbeKind.PartialMatch, suffix.Kind);
        Assert.Equal((1, 7), (suffix.Value.StartOffsetInBytes, suffix.Value.EndOffsetInBytes));
    }

    [Fact]
    public void LiteralProjectionKeepsTheDeadlineThatStartedWithItsKernel()
    {
        var regex = new Utf8Pcre2Regex("é😀", Pcre2CompileOptions.None, default, default, TimeSpan.FromMilliseconds(20));
        var compiled = regex.DebugCompiledProgram;
        var literal = Assert.IsType<Pcre2LiteralDirectProgram>(compiled.Operations.Match);
        var nativePolls = -1;
        var polls = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() =>
            ++polls > nativePolls && nativePolls >= 0 ? 25 : 0);
        var input = Utf8ValidatedInput.Create("é😀"u8);
        var budget = new Pcre2ResourceBudget(compiled.Request.DefaultLimits, compiled.Request.MatchTimeout, false);
        Assert.True(Pcre2LiteralRunner.Match(literal.Program, ref input, new Utf8BytePosition(0), Pcre2MatchOptions.None, ref budget).Success);
        nativePolls = polls;
        polls = 0;
        Assert.Equal(Pcre2ErrorKind.Timeout, Assert.Throws<Pcre2MatchException>(() =>
        { _ = regex.Probe("é😀"u8, Pcre2PartialMode.None); }).ErrorKind);
    }
}
