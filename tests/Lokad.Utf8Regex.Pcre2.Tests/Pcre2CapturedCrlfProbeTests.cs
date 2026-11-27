using System.Text;
using Lokad.Utf8Regex.Internal;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Pcre2.Tests;

[Collection(Pcre2AllocationTestCollection.Name)]
public sealed class Pcre2CapturedCrlfProbeTests
{
    // Frozen PCRE2 10.47 / private PCRE.NET 1.5.0 outcomes; no native test dependency.
    public static IEnumerable<object[]> NativeCases()
    {
        yield return ["^(a$|a\\r)", "", 0, Pcre2CompileOptions.None, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["^(a$|a\\r)", "", 0, Pcre2CompileOptions.None, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["^(a$|a\\r)", "", 0, Pcre2CompileOptions.None, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["^(a$|a\\r)", "a", 0, Pcre2CompileOptions.None, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.FullMatch, 0, 1];
        yield return ["^(a$|a\\r)", "a", 0, Pcre2CompileOptions.None, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.FullMatch, 0, 1];
        yield return ["^(a$|a\\r)", "a", 0, Pcre2CompileOptions.None, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.PartialMatch, 0, 1];
        yield return ["^(a$|a\\r)", "a\r", 0, Pcre2CompileOptions.None, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.FullMatch, 0, 2];
        yield return ["^(a$|a\\r)", "a\r", 0, Pcre2CompileOptions.None, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.FullMatch, 0, 2];
        yield return ["^(a$|a\\r)", "a\r", 0, Pcre2CompileOptions.None, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.PartialMatch, 0, 2];
        yield return ["^(a$|a\\r)", "a\r\n", 0, Pcre2CompileOptions.None, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.FullMatch, 0, 1];
        yield return ["^(a$|a\\r)", "a\r\n", 0, Pcre2CompileOptions.None, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.FullMatch, 0, 1];
        yield return ["^(a$|a\\r)", "a\r\n", 0, Pcre2CompileOptions.None, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.PartialMatch, 0, 3];
        yield return ["^(a$|a\\r)", "a\r\nx", 0, Pcre2CompileOptions.None, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.FullMatch, 0, 2];
        yield return ["^(a$|a\\r)", "a\r\nx", 0, Pcre2CompileOptions.None, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.FullMatch, 0, 2];
        yield return ["^(a$|a\\r)", "a\r\nx", 0, Pcre2CompileOptions.None, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.FullMatch, 0, 2];
        yield return ["^(a$|a\\r)", "a\n", 0, Pcre2CompileOptions.None, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["^(a$|a\\r)", "a\n", 0, Pcre2CompileOptions.None, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["^(a$|a\\r)", "a\n", 0, Pcre2CompileOptions.None, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["^(a$|a\\r)", "a\rx", 0, Pcre2CompileOptions.None, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.FullMatch, 0, 2];
        yield return ["^(a$|a\\r)", "a\rx", 0, Pcre2CompileOptions.None, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.FullMatch, 0, 2];
        yield return ["^(a$|a\\r)", "a\rx", 0, Pcre2CompileOptions.None, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.FullMatch, 0, 2];
        yield return ["^(a$|a\\r)", "a\ud83d\ude00", 0, Pcre2CompileOptions.None, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["^(a$|a\\r)", "a\ud83d\ude00", 0, Pcre2CompileOptions.None, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["^(a$|a\\r)", "a\ud83d\ude00", 0, Pcre2CompileOptions.None, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["^(a$|a\\r)", "a\r\ud83d\ude00", 0, Pcre2CompileOptions.None, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.FullMatch, 0, 2];
        yield return ["^(a$|a\\r)", "a\r\ud83d\ude00", 0, Pcre2CompileOptions.None, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.FullMatch, 0, 2];
        yield return ["^(a$|a\\r)", "a\r\ud83d\ude00", 0, Pcre2CompileOptions.None, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.FullMatch, 0, 2];
        yield return ["^(a$|a\\r)", "ab", 0, Pcre2CompileOptions.None, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["^(a$|a\\r)", "ab", 0, Pcre2CompileOptions.None, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["^(a$|a\\r)", "ab", 0, Pcre2CompileOptions.None, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["^(a$|a\\r)", "\ud83d\ude00a", 4, Pcre2CompileOptions.None, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["^(a$|a\\r)", "\ud83d\ude00a", 4, Pcre2CompileOptions.None, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["^(a$|a\\r)", "\ud83d\ude00a", 4, Pcre2CompileOptions.None, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["^(a$|a\\r)", "xa\r", 1, Pcre2CompileOptions.None, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["^(a$|a\\r)", "xa\r", 1, Pcre2CompileOptions.None, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["^(a$|a\\r)", "xa\r", 1, Pcre2CompileOptions.None, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["^(a$|a\\r)", "a", 0, Pcre2CompileOptions.None, Pcre2MatchOptions.NotEol, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["^(a$|a\\r)", "a", 0, Pcre2CompileOptions.None, Pcre2MatchOptions.NotEol, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.PartialMatch, 0, 1];
        yield return ["^(a$|a\\r)", "a", 0, Pcre2CompileOptions.None, Pcre2MatchOptions.NotEol, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.PartialMatch, 0, 1];
        yield return ["^(a$|a\\r)", "a\r", 0, Pcre2CompileOptions.None, Pcre2MatchOptions.NotEol, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.FullMatch, 0, 2];
        yield return ["^(a$|a\\r)", "a\r", 0, Pcre2CompileOptions.None, Pcre2MatchOptions.NotEol, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.FullMatch, 0, 2];
        yield return ["^(a$|a\\r)", "a\r", 0, Pcre2CompileOptions.None, Pcre2MatchOptions.NotEol, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.FullMatch, 0, 2];
        yield return ["^(a$|a\\r)", "a\r\n", 0, Pcre2CompileOptions.None, Pcre2MatchOptions.NotEol, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.FullMatch, 0, 2];
        yield return ["^(a$|a\\r)", "a\r\n", 0, Pcre2CompileOptions.None, Pcre2MatchOptions.NotEol, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.FullMatch, 0, 2];
        yield return ["^(a$|a\\r)", "a\r\n", 0, Pcre2CompileOptions.None, Pcre2MatchOptions.NotEol, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.FullMatch, 0, 2];
        yield return ["^(a$|a\\r)", "a", 0, Pcre2CompileOptions.None, Pcre2MatchOptions.NotBol, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["^(a$|a\\r)", "a", 0, Pcre2CompileOptions.None, Pcre2MatchOptions.NotBol, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["^(a$|a\\r)", "a", 0, Pcre2CompileOptions.None, Pcre2MatchOptions.NotBol, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["^(a$|a\\r)", "a\r", 0, Pcre2CompileOptions.None, Pcre2MatchOptions.NotBol, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["^(a$|a\\r)", "a\r", 0, Pcre2CompileOptions.None, Pcre2MatchOptions.NotBol, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["^(a$|a\\r)", "a\r", 0, Pcre2CompileOptions.None, Pcre2MatchOptions.NotBol, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["^(a$|a\\r)", "a\r\n", 0, Pcre2CompileOptions.None, Pcre2MatchOptions.NotEmptyAtStart, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.FullMatch, 0, 1];
        yield return ["^(a$|a\\r)", "a\r\n", 0, Pcre2CompileOptions.None, Pcre2MatchOptions.NotEmptyAtStart, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.FullMatch, 0, 1];
        yield return ["^(a$|a\\r)", "a\r\n", 0, Pcre2CompileOptions.None, Pcre2MatchOptions.NotEmptyAtStart, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.PartialMatch, 0, 3];
        yield return ["^(?<part>a$|a\\r)", "a", 0, Pcre2CompileOptions.None, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.FullMatch, 0, 1];
        yield return ["^(?<part>a$|a\\r)", "a", 0, Pcre2CompileOptions.None, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.FullMatch, 0, 1];
        yield return ["^(?<part>a$|a\\r)", "a", 0, Pcre2CompileOptions.None, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.PartialMatch, 0, 1];
        yield return ["^(?<part>a$|a\\r)", "a\r", 0, Pcre2CompileOptions.None, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.FullMatch, 0, 2];
        yield return ["^(?<part>a$|a\\r)", "a\r", 0, Pcre2CompileOptions.None, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.FullMatch, 0, 2];
        yield return ["^(?<part>a$|a\\r)", "a\r", 0, Pcre2CompileOptions.None, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.PartialMatch, 0, 2];
        yield return ["^(?<part>a$|a\\r)", "a\r\n", 0, Pcre2CompileOptions.None, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.FullMatch, 0, 1];
        yield return ["^(?<part>a$|a\\r)", "a\r\n", 0, Pcre2CompileOptions.None, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.FullMatch, 0, 1];
        yield return ["^(?<part>a$|a\\r)", "a\r\n", 0, Pcre2CompileOptions.None, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.PartialMatch, 0, 3];
        yield return ["^(?<part>a$|a\\r)", "a\rx", 0, Pcre2CompileOptions.None, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.FullMatch, 0, 2];
        yield return ["^(?<part>a$|a\\r)", "a\rx", 0, Pcre2CompileOptions.None, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.FullMatch, 0, 2];
        yield return ["^(?<part>a$|a\\r)", "a\rx", 0, Pcre2CompileOptions.None, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.FullMatch, 0, 2];
        yield return ["^(?<part>a$|a\\r)", "a\r\ud83d\ude00", 0, Pcre2CompileOptions.None, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.FullMatch, 0, 2];
        yield return ["^(?<part>a$|a\\r)", "a\r\ud83d\ude00", 0, Pcre2CompileOptions.None, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.FullMatch, 0, 2];
        yield return ["^(?<part>a$|a\\r)", "a\r\ud83d\ude00", 0, Pcre2CompileOptions.None, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.FullMatch, 0, 2];
        yield return ["^(a$|a\\r)", "a\r\n", 0, Pcre2CompileOptions.Anchored, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.FullMatch, 0, 1];
        yield return ["^(a$|a\\r)", "a\r\n", 0, Pcre2CompileOptions.Anchored, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.FullMatch, 0, 1];
        yield return ["^(a$|a\\r)", "a\r\n", 0, Pcre2CompileOptions.Anchored, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.PartialMatch, 0, 3];
        yield return ["^(a$|a\\r)", "a", 0, Pcre2CompileOptions.None, Pcre2MatchOptions.NotEmpty, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.FullMatch, 0, 1];
        yield return ["^(a$|a\\r)", "a", 0, Pcre2CompileOptions.None, Pcre2MatchOptions.NotEmpty, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.FullMatch, 0, 1];
        yield return ["^(a$|a\\r)", "a", 0, Pcre2CompileOptions.None, Pcre2MatchOptions.NotEmpty, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.PartialMatch, 0, 1];
    }

    [Theory]
    [MemberData(nameof(NativeCases))]
    public void CapturedAlternativesPreserveNativePriorityAndInspectedExtent(string pattern, string text, int start,
        Pcre2CompileOptions options, Pcre2MatchOptions flags, Pcre2PartialMode mode,
        Utf8Pcre2ProbeKind kind, int begin, int end)
    {
        var regex = new Utf8Pcre2Regex(pattern, options: options, compileSettings: new() { Newline = Pcre2NewlineConvention.Crlf }, policy: new Utf8RegexPolicy
        {
            MatchTimeout = Timeout.InfiniteTimeSpan
        });
        var input = Encoding.UTF8.GetBytes(text);
        var probe = regex.Probe(input, mode, start, flags);
        Assert.Equal(kind, probe.Kind);
        Assert.Equal((begin, end), (probe.Value.StartOffsetInBytes, probe.Value.EndOffsetInBytes));
        if (kind != Utf8Pcre2ProbeKind.FullMatch) return;
        var actual = probe.GetMatch();
        Assert.Equal(2, actual.CaptureSlotCount);
        ReadOnlySpan<byte> expected1Input = input;
        var expected1Captures = new Utf8Pcre2CaptureRange[regex.CaptureGroupCount + 1];
        var expected = regex.Match(expected1Input, startOffsetInBytes: start, captures: expected1Captures, matchOptions: flags);
        for (var slot = 0; slot < 2; slot++)
        {
            var left = expected1Captures[slot]; var right = actual.GetGroup(slot);
            Assert.True(right.Success);
            Assert.Equal(System.Text.Encoding.UTF8.GetString((left.Success ? expected1Input.Slice(left.StartOffsetInBytes, left.EndOffsetInBytes - left.StartOffsetInBytes) : ReadOnlySpan<byte>.Empty)), System.Text.Encoding.UTF8.GetString(right.GetValueBytes()));
            Assert.Equal((begin, end), (right.StartOffsetInBytes, right.EndOffsetInBytes));
        }
        if (pattern.Contains("?<part>"))
        {
            Assert.True(actual.TryGetGroup(1, out var named)); Assert.True(named.Success);
            Assert.Equal(System.Text.Encoding.UTF8.GetString(actual.Value.GetValueBytes()), System.Text.Encoding.UTF8.GetString(named.GetValueBytes()));
        }
    }

    [Fact]
    public void CapturedAlternativeHasExactResultAdmissionAndIndependentAliases()
    {
        var capacity = Utf8ConstructionBudget.ArrayBytes<Pcre2GroupData>(2);
        Utf8Pcre2Regex Create(ulong bytes) => new Utf8Pcre2Regex(@"^(?<part>a$|a\r)", options: Pcre2CompileOptions.None, compileSettings: new() { Newline = Pcre2NewlineConvention.Crlf }, policy: new Utf8RegexPolicy
        {
            ResourcePolicy = new()
            {
                MaxDepth = 256,
                MaxWorkingBytes = 1 << 20,
                MaxResultBytes = bytes
            },
            MatchTimeout = Timeout.InfiniteTimeSpan
        });
        Assert.Equal(Pcre2ErrorKind.ResultLimit, Assert.Throws<Pcre2MatchException>(() =>
        { _ = Create(capacity - 1).Probe("a\r\n"u8, Pcre2PartialMode.Soft); }).ErrorKind);
        var regex = Create(capacity);
        var retained = regex.Probe("a\r\n"u8, Pcre2PartialMode.Soft).GetMatch();
        Assert.True(retained.TryGetGroup(1, out var alias)); Assert.True(alias.Success);
        Assert.Equal((0, 1), (alias.StartOffsetInBytes, alias.EndOffsetInBytes));
        Assert.Equal("a\r", System.Text.Encoding.UTF8.GetString(regex.Probe("a\r😀"u8, Pcre2PartialMode.Soft).GetMatch().Value.GetValueBytes()));
        Assert.Equal("a", System.Text.Encoding.UTF8.GetString(retained.Value.GetValueBytes()));
        Assert.Equal("a", System.Text.Encoding.UTF8.GetString(alias.GetValueBytes()));
        var partial = Create(1).Probe("a\r\n"u8, Pcre2PartialMode.Hard);
        Assert.Equal(Utf8Pcre2ProbeKind.PartialMatch, partial.Kind);
        Assert.Equal((0, 3), (partial.Value.StartOffsetInBytes, partial.Value.EndOffsetInBytes));
    }

    [Fact]
    public void CapturedAlternativeKeepsOriginalWorkAndWorkingAllowance()
    {
        Utf8Pcre2Regex Create(uint work, ulong heap) => new(@"^(a$|a\r)", options: Pcre2CompileOptions.None, compileSettings: new() { Newline = Pcre2NewlineConvention.Crlf }, policy: new Utf8RegexPolicy
        {
            ResourcePolicy = new Utf8ResourcePolicy
            {
                MaxWorkingBytes = (heap) == 0 ? null : (heap)
            },
            MatchTimeout = Timeout.InfiniteTimeSpan
        });
        Assert.Equal(Pcre2ErrorKind.HeapLimit, Assert.Throws<Pcre2MatchException>(() =>
        { _ = Create(100000, 1).Probe("a\r\n"u8, Pcre2PartialMode.Soft); }).ErrorKind);
        Assert.Equal(Utf8Pcre2ProbeKind.FullMatch, Create(100000, 1 << 20).Probe("a\r\n"u8, Pcre2PartialMode.Soft).Kind);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CapturedAlternativeObservesOriginalInterruption(bool cancel)
    {
        using var source = new CancellationTokenSource();
        var regex = new Utf8Pcre2Regex(@"^(a$|a\r)", options: Pcre2CompileOptions.None, compileSettings: new() { Newline = Pcre2NewlineConvention.Crlf }, policy: new Utf8RegexPolicy
        {
            MatchTimeout = TimeSpan.FromMilliseconds(20)
        });
        var polls = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() =>
        {
            if (++polls < 4) return 0;
            if (cancel) { source.Cancel(); return 0; }
            return 25;
        });
        Exception? failure = null;
        try { _ = regex.Probe("a\r\n"u8, Pcre2PartialMode.Soft, 0, Pcre2MatchOptions.None, source.Token); }
        catch (Exception error) { failure = error; }
        if (cancel) Assert.IsAssignableFrom<OperationCanceledException>(failure);
        else Assert.Equal(Pcre2ErrorKind.Timeout, Assert.IsType<Pcre2MatchException>(failure).ErrorKind);
    }

}
