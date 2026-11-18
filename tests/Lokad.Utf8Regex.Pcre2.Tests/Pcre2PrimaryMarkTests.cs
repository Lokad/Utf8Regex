using System.Text;

namespace Lokad.Utf8Regex.Pcre2.Tests;

public sealed class Pcre2PrimaryMarkTests
{
    [Theory]
    [InlineData("abc", "πabc", true, null)]
    [InlineData("abc", "πxyz", false, null)]
    [InlineData("(?:ab|cd)+", "abcd", true, null)]
    [InlineData("(*MARK:A)ac|(*MARK:B)(b)", "b", true, "B")]
    [InlineData("(*MARK:A)a|b", "c", false, "A")]
    [InlineData("(?=(*MARK:A)a)a", "a", true, "A")]
    [InlineData("(*MARK:A)(?<x>é😀)", "πé😀", true, "A")]
    [InlineData("(*MARK:B)(\\C)", "😀", true, "B")]
    [InlineData("(*MARK:K)(?=(ab)\\K)", "ab", true, "K")]
    public void PrimaryResultsKeepTheExistingMarkForEveryCallerOutputWidth(string pattern, string text, bool success, string? mark)
    {
        var input = Encoding.UTF8.GetBytes(text);
        using var regex = new Utf8Pcre2Regex(pattern, compileSettings: new()
        { BackslashC = Pcre2BackslashCPolicy.Allow, AllowLookaroundBackslashK = true }, policy: new()
        { ResourcePolicy = new() { MaxWorkSteps = 1_000_000, MaxDepth = 512, MaxWorkingBytes = 1 << 20, MaxResultBytes = 1 << 20 } });
        ReadOnlySpan<byte> expected1Input = input;
        var expected1Captures = new Utf8Pcre2CaptureRange[regex.CaptureGroupCount + 1];
        var expected = regex.Match(expected1Input, captures: expected1Captures);
        Assert.Equal(success, expected.Success); Assert.Equal(mark, expected.Mark);
        var count = regex.CaptureGroupCount + 1;
        foreach (var width in new[] { 0, 1, count, count + 2 }.Distinct())
        {
            var captures = Enumerable.Repeat(Sentinel, width).ToArray(); var actual = regex.Match(input, captures: captures);
            Assert.Equal(success, actual.Success); Assert.Equal(mark, actual.Mark);
            Assert.Equal(expected.StartOffsetInBytes, actual.StartOffsetInBytes);
            Assert.Equal(expected.EndOffsetInBytes, actual.EndOffsetInBytes);
            Assert.Equal(expected.HasContiguousByteRange, actual.HasContiguousByteRange);
            Assert.Equal(expected.IsUtf8SliceWellFormed, actual.IsUtf8SliceWellFormed);
            for (var slot = 0; slot < Math.Min(width, count); slot++)
            {
                if (!success) { Assert.Equal(default, captures[slot]); continue; }
                var group = expected1Captures[slot];
                Assert.Equal((group.Success, group.StartOffsetInBytes, group.EndOffsetInBytes, group.IsUtf8SliceWellFormed),
                    (captures[slot].Success, captures[slot].StartOffsetInBytes, captures[slot].EndOffsetInBytes, captures[slot].IsUtf8SliceWellFormed));
            }
            foreach (var tail in captures.AsSpan(Math.Min(count, width))) Assert.Equal(Sentinel, tail);
        }
    }

    [Fact]
    public void MarkReferenceNeedsNoOwnedResultPayloadAndSurvivesProgramClose()
    {
        var account = new Account();
        using var regex = new Utf8Pcre2Regex("(*MARK:é😀)(?<x>a)", policy: new()
        { ProgramMemoryAllowance = account, ResourcePolicy = new() { MaxWorkSteps = 100000, MaxResultBytes = 1 } });
        Span<Utf8Pcre2CaptureRange> captures = stackalloc Utf8Pcre2CaptureRange[2];
        var retained = regex.Match("πa"u8, 2, captures, Pcre2MatchOptions.Anchored);
        Assert.True(retained.Success); Assert.Equal("é😀", retained.Mark); Assert.Equal((2, 3), (retained.StartOffsetInBytes, retained.EndOffsetInBytes));
        var miss = regex.Match("πb"u8, 2, captures, Pcre2MatchOptions.Anchored);
        Assert.False(miss.Success); Assert.Equal("é😀", miss.Mark); Assert.Equal(default, captures[0]);
        regex.Dispose(); Assert.Equal(0UL, account.Used);
        Assert.Equal("é😀", retained.Mark); Assert.Equal("é😀", miss.Mark); Assert.Equal("a"u8.ToArray(), retained.GetValueBytes().ToArray());
    }

    [Fact]
    public void ProbeValueProjectionsKeepTheirExistingMark()
    {
        var regex = new Utf8Pcre2Regex("(*MARK:tag)a");
        var full = regex.Probe("a"u8, Pcre2PartialMode.None);
        Assert.Equal(full.Mark, full.Value.Mark); Assert.Equal(full.Mark, full.GetMatch().Value.Mark);
        var miss = regex.Probe("b"u8, Pcre2PartialMode.None);
        Assert.Equal("tag", miss.Mark); Assert.Equal(miss.Mark, miss.Value.Mark);
        var partial = new Utf8Pcre2Regex("ab").Probe("a"u8, Pcre2PartialMode.Hard);
        Assert.Equal(Utf8Pcre2ProbeKind.PartialMatch, partial.Kind);
        Assert.Equal(partial.Mark, partial.Value.Mark); Assert.Equal(partial.Mark, partial.GetPartial().Value.Mark);
    }

    [Fact]
    public void NotEmptyKeepsMarkAcrossCandidatesAtAnOriginalByteStart()
    {
        var regex = new Utf8Pcre2Regex("(*MARK:tag)(a*)");
        foreach (var width in new[] { 0, 1, 2 })
        {
            var captures = new Utf8Pcre2CaptureRange[width];
            var match = regex.Match("πba"u8, 2, captures, Pcre2MatchOptions.NotEmpty);
            Assert.True(match.Success); Assert.Equal("tag", match.Mark);
            Assert.Equal((3, 4), (match.StartOffsetInBytes, match.EndOffsetInBytes));
            foreach (var capture in captures) Assert.Equal((3, 4), (capture.StartOffsetInBytes, capture.EndOffsetInBytes));
        }
    }

    [Fact]
    public void EntryCancellationKeepsCallerOutputAndOriginalToken()
    {
        var regex = new Utf8Pcre2Regex("(*MARK:tag)(a)"); var captures = new[] { Sentinel, Sentinel };
        using var source = new CancellationTokenSource(); source.Cancel();
        Assert.Equal(source.Token, Assert.Throws<OperationCanceledException>(() =>
            regex.Match("a"u8, captures: captures, cancellationToken: source.Token)).CancellationToken);
        Assert.All(captures, value => Assert.Equal(Sentinel, value));
    }

    private static Utf8Pcre2CaptureRange Sentinel => new(91, true, 5, 6, true);
    private sealed class Account : IUtf8MemoryAllowance
    {
        internal ulong Used;
        public bool TryReserve(ulong bytes) { if (bytes > (64UL << 20) - Used) return false; Used += bytes; return true; }
        public void Release(ulong bytes) { Assert.InRange(bytes, 0UL, Used); Used -= bytes; }
    }
}
