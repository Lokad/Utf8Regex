using System.Text;
using Lokad.Utf8Regex.Internal;

namespace Lokad.Utf8Regex.Pcre2.Tests;

public sealed class Pcre2FullProbeCaptureTests
{
    [Theory]
    [InlineData("dog(sbody)?", "dogsbody")]
    [InlineData("dog(sbody)?", "dog")]
    [InlineData("dog(?<tail>sbody)?", "dogsbody")]
    [InlineData("dog(sbody)??", "dogsbody")]
    [InlineData(@"^(?<last>a)++\w", "aaab")]
    [InlineData(@"^(a|)++\w", "aaab")]
    public void FullProbePreservesParticipatingAndMissingCaptureSlots(string pattern, string text)
    {
        var regex = new Utf8Pcre2Regex(pattern);
        Assert.NotEqual(Pcre2PartialProbeProgramKind.None, regex.DebugCompiledProgram.PartialProbe.Kind);
        var input = Encoding.UTF8.GetBytes(text);
        var expected = regex.MatchDetailed(input);
        var probe = regex.Probe(input, Pcre2PartialMode.Soft);
        Assert.Equal(Utf8Pcre2ProbeKind.FullMatch, probe.Kind);
        var actual = probe.GetMatch();
        Assert.Equal(expected.CaptureSlotCount, actual.CaptureSlotCount);
        for (var slot = 0; slot < expected.CaptureSlotCount; slot++)
        {
            var left = expected.GetGroup(slot); var right = actual.GetGroup(slot);
            Assert.Equal(left.Success, right.Success);
            if (!left.Success) continue;
            Assert.Equal(left.GetValueString(), right.GetValueString());
            Assert.Equal((left.StartOffsetInBytes, left.EndOffsetInBytes), (right.StartOffsetInBytes, right.EndOffsetInBytes));
        }
        var entries = new Pcre2NameEntry[regex.NameEntryCount];
        regex.CopyNameEntries(entries, out _);
        foreach (var entry in entries)
            Assert.Equal(expected.TryGetFirstSetGroup(entry.Name, out _), actual.TryGetFirstSetGroup(entry.Name, out _));
    }

    [Fact]
    public void CapturedFullProbeHasExactResultAdmissionAndIndependentSnapshots()
    {
        var capacity = Utf8ConstructionBudget.ArrayBytes<Pcre2GroupData>(2);
        Utf8Pcre2Regex Create(ulong resultBytes) => Utf8Pcre2Regex.Create(
            "dog(?<tail>sbody)?", Pcre2CompileOptions.None, default,
            new() { MaxWorkSteps = 100000, MaxDepth = 256, MaxWorkingBytes = 1 << 20, MaxResultBytes = resultBytes },
            Timeout.InfiniteTimeSpan);
        Assert.Equal(Pcre2ErrorKind.ResultLimit, Assert.Throws<Pcre2MatchException>(() =>
        { _ = Create(capacity - 1).Probe("xdogsbody dog"u8, Pcre2PartialMode.Soft, 1); }).ErrorKind);

        var regex = Create(capacity);
        var retained = regex.Probe("xdogsbody dog"u8, Pcre2PartialMode.Soft, 1).GetMatch();
        Assert.Equal((1, 9), (retained.GetGroup(0).StartOffsetInBytes, retained.GetGroup(0).EndOffsetInBytes));
        Assert.True(retained.TryGetFirstSetGroup("tail", out var named));
        Assert.Equal("sbody", named.GetValueString());
        var next = regex.Probe("dog"u8, Pcre2PartialMode.Soft).GetMatch();
        Assert.Equal(2, next.CaptureSlotCount);
        Assert.False(next.GetGroup(1).Success);
        Assert.Equal("dogsbody", retained.GetValueString());
        Assert.Equal("sbody", named.GetValueString());
    }

    [Fact]
    public void CapturedFullProbeKeepsOriginalWorkAndWorkingLimits()
    {
        Utf8Pcre2Regex Create(uint work, ulong heap) => new("dog(?<tail>sbody)?",
            Pcre2CompileOptions.None, default, new() { MatchLimit = work, HeapLimitInBytes = heap }, Timeout.InfiniteTimeSpan);
        Assert.Equal(Pcre2ErrorKind.MatchLimit, Assert.Throws<Pcre2MatchException>(() =>
        { _ = Create(1, 1 << 20).Probe("dogsbody"u8, Pcre2PartialMode.Soft); }).ErrorKind);
        Assert.Equal(Pcre2ErrorKind.HeapLimit, Assert.Throws<Pcre2MatchException>(() =>
        { _ = Create(100000, 1).Probe("dogsbody"u8, Pcre2PartialMode.Soft); }).ErrorKind);
        Assert.Equal("sbody", Create(100000, 1 << 20).Probe("dogsbody"u8, Pcre2PartialMode.Soft).GetMatch().GetGroup(1).GetValueString());
    }
}
