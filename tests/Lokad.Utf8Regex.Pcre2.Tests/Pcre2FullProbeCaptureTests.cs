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
        ReadOnlyMemory<byte> expected1Input = input;
        using var expected1Cursor = regex.CreateMatchCursor(expected1Input);
        expected1Cursor.MoveNext(out var expected1Result);
        expected1Cursor.Dispose();
        var expected = expected1Result;
        var probe = regex.Probe(input, Pcre2PartialMode.Soft);
        Assert.Equal(Utf8Pcre2ProbeKind.FullMatch, probe.Kind);
        var actual = probe.GetMatch();
        Assert.Equal(expected.Groups.Length, actual.CaptureSlotCount);
        for (var slot = 0; slot < expected.Groups.Length; slot++)
        {
            var left = expected.Groups[slot]; var right = actual.GetGroup(slot);
            Assert.Equal(left.Success, right.Success);
            if (!left.Success) continue;
            Assert.Equal(System.Text.Encoding.UTF8.GetString((left.Success ? expected1Input.Span.Slice(left.StartOffsetInBytes, left.EndOffsetInBytes - left.StartOffsetInBytes) : ReadOnlySpan<byte>.Empty)), System.Text.Encoding.UTF8.GetString(right.GetValueBytes()));
            Assert.Equal((left.StartOffsetInBytes, left.EndOffsetInBytes), (right.StartOffsetInBytes, right.EndOffsetInBytes));
        }
    }

    [Fact]
    public void CapturedFullProbeHasExactResultAdmissionAndIndependentSnapshots()
    {
        var capacity = Utf8ConstructionBudget.ArrayBytes<Pcre2GroupData>(2);
        Utf8Pcre2Regex Create(ulong resultBytes) => new Utf8Pcre2Regex("dog(?<tail>sbody)?", options: Pcre2CompileOptions.None, compileSettings: default, policy: new Utf8RegexPolicy
        {
            ResourcePolicy = new()
            {
                MaxWorkSteps = 100000,
                MaxDepth = 256,
                MaxWorkingBytes = 1 << 20,
                MaxResultBytes = resultBytes
            },
            MatchTimeout = Timeout.InfiniteTimeSpan
        });
        Assert.Equal(Pcre2ErrorKind.ResultLimit, Assert.Throws<Pcre2MatchException>(() =>
        { _ = Create(capacity - 1).Probe("xdogsbody dog"u8, Pcre2PartialMode.Soft, 1); }).ErrorKind);

        var regex = Create(capacity);
        var retained = regex.Probe("xdogsbody dog"u8, Pcre2PartialMode.Soft, 1).GetMatch();
        Assert.Equal((1, 9), (retained.GetGroup(0).StartOffsetInBytes, retained.GetGroup(0).EndOffsetInBytes));
        Assert.True(retained.TryGetGroup(1, out var named)); Assert.True(named.Success);
        Assert.Equal("sbody", System.Text.Encoding.UTF8.GetString(named.GetValueBytes()));
        var next = regex.Probe("dog"u8, Pcre2PartialMode.Soft).GetMatch();
        Assert.Equal(2, next.CaptureSlotCount);
        Assert.False(next.GetGroup(1).Success);
        Assert.Equal("dogsbody", System.Text.Encoding.UTF8.GetString(retained.Value.GetValueBytes()));
        Assert.Equal("sbody", System.Text.Encoding.UTF8.GetString(named.GetValueBytes()));
    }

    [Fact]
    public void CapturedFullProbeKeepsOriginalWorkAndWorkingLimits()
    {
        Utf8Pcre2Regex Create(uint work, ulong heap) => new("dog(?<tail>sbody)?", options: Pcre2CompileOptions.None, compileSettings: default, policy: new Utf8RegexPolicy
        {
            ResourcePolicy = new Utf8ResourcePolicy
            {
                MaxWorkSteps = (work) == 0 ? null : (work),
                MaxWorkingBytes = (heap) == 0 ? null : (heap)
            },
            MatchTimeout = Timeout.InfiniteTimeSpan
        });
        Assert.Equal(Pcre2ErrorKind.MatchLimit, Assert.Throws<Pcre2MatchException>(() =>
        { _ = Create(1, 1 << 20).Probe("dogsbody"u8, Pcre2PartialMode.Soft); }).ErrorKind);
        Assert.Equal(Pcre2ErrorKind.HeapLimit, Assert.Throws<Pcre2MatchException>(() =>
        { _ = Create(100000, 1).Probe("dogsbody"u8, Pcre2PartialMode.Soft); }).ErrorKind);
        Assert.Equal("sbody", System.Text.Encoding.UTF8.GetString(Create(100000, 1 << 20).Probe("dogsbody"u8, Pcre2PartialMode.Soft).GetMatch().GetGroup(1).GetValueBytes()));
    }
}
