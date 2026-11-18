using System.Text;
using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8ScalarSimpleCaptureTests
{
    [Theory]
    [InlineData(false, false)] [InlineData(true, false)]
    [InlineData(false, true)] [InlineData(true, true)]
    public void CapturedCandidateScanningKeepsAllocationIndependentOfSubjectLength(bool compiled, bool count)
    {
        var regex = new Utf8Regex("(a(.)c)", options: RegexOptions.CultureInvariant | (compiled ? RegexOptions.Compiled : 0));
        var input = Encoding.UTF8.GetBytes(new string('x', 100000) + "ayc");
        // A live token selects scalar discovery; qualified no-token native lanes
        // remain available and are outside this allocation control.
        using var source = new CancellationTokenSource();
        if (count) Assert.Equal(1, regex.Count(input, source.Token));
        else Assert.True(regex.IsMatch(input, source.Token));
        var before = GC.GetAllocatedBytesForCurrentThread();
        if (count) Assert.Equal(1, regex.Count(input, source.Token));
        else Assert.True(regex.IsMatch(input, source.Token));
        Assert.InRange(GC.GetAllocatedBytesForCurrentThread() - before, 0L, 65536L);
        Assert.True(regex.Inspection.PreparedRegex.ScalarProgram is not null);
    }

    [Theory]
    [InlineData(false, false)] [InlineData(true, false)]
    [InlineData(false, true)] [InlineData(true, true)]
    public void AsciiCapturedDetailedDiscoveryDoesNotDecodeWholeSubject(bool compiled, bool miss)
    {
        var regex = new Utf8Regex("(a(.)c)", options: RegexOptions.CultureInvariant | (compiled ? RegexOptions.Compiled : 0));
        var input = Encoding.UTF8.GetBytes(new string('x', 100000) + (miss ? "ayz" : "ayc"));
        Assert.Equal(!miss, Utf8MatchSequence.First(regex.CreateMatchCursor(input)).Success);
        var before = GC.GetAllocatedBytesForCurrentThread();
        var match = Utf8MatchSequence.First(regex.CreateMatchCursor(input));
        Assert.Equal(!miss, match.Success);
        if (!miss)
        {
            Assert.Equal((100000, 3), (match.Value.StartOffsetInBytes, (match.Value.EndOffsetInBytes - match.Value.StartOffsetInBytes)));
            Assert.Equal("ayc", System.Text.Encoding.UTF8.GetString(match.GetGroupMemory(1).Span));
            Assert.Equal("y", System.Text.Encoding.UTF8.GetString(match.GetGroupMemory(2).Span));
        }
        // ASCII derived coordinates need no map arrays; scratch is input-independent.
        Assert.InRange(GC.GetAllocatedBytesForCurrentThread() - before, 0L, 65536L);
        Assert.True(regex.Inspection.PreparedRegex.ScalarProgram is not null);
        Assert.True(ReferenceEquals(regex.Inspection.PreparedRegex.SemanticRegex.CaptureCatalog, regex.Inspection.PreparedRegex.ScalarProgram!.CaptureCatalog));
    }

    [Theory]
    [InlineData(false, false, false)] [InlineData(true, false, false)]
    [InlineData(false, true, false)] [InlineData(true, true, false)]
    [InlineData(false, false, true)] [InlineData(true, false, true)]
    [InlineData(false, true, true)] [InlineData(true, true, true)]
    public void CapturedBranchChoiceHasCompleteScalarRanges(bool compiled, bool reverse, bool supplementary)
    {
        var options = RegexOptions.CultureInvariant | (compiled ? RegexOptions.Compiled : 0) |
            (reverse ? RegexOptions.RightToLeft : 0);
        var regex = new Utf8Regex("(a|(.)c)", options: options);
        var text = supplementary ? "x😀c" : "xac";
        var input = Encoding.UTF8.GetBytes(text);
        var match = Utf8MatchSequence.First(regex.CreateMatchCursor(input));
        var length = supplementary ? 5 : reverse ? 2 : 1;
        Assert.True(match.Success);
        Assert.Equal((1, length), (match.Value.StartOffsetInBytes, match.Value.EndOffsetInBytes - match.Value.StartOffsetInBytes));
        Assert.Equal(3, match.GroupNumbers.Length);
        Assert.Equal(supplementary ? "😀c" : reverse ? "ac" : "a", System.Text.Encoding.UTF8.GetString(match.GetValueMemory().Span));
        Assert.True(match.TryGetGroup(1, out var first));
        Assert.True(first.Success);
        Assert.Equal(System.Text.Encoding.UTF8.GetString(match.GetValueMemory().Span), System.Text.Encoding.UTF8.GetString(match.GetGroupMemory(1).Span));
        Assert.Equal(1, match.GetCaptures(1).Length);
        Assert.Equal((1, length), (match.GetCaptures(1)[0].StartOffsetInBytes, (match.GetCaptures(1)[0].EndOffsetInBytes - match.GetCaptures(1)[0].StartOffsetInBytes)));
        Assert.True(match.TryGetGroup(2, out var second));
        Assert.Equal(supplementary || reverse, second.Success);
        if (second.Success)
        {
            Assert.Equal((1, supplementary ? 4 : 1), (second.StartOffsetInBytes, (second.EndOffsetInBytes - second.StartOffsetInBytes)));
            Assert.Equal(supplementary ? "😀" : "a", System.Text.Encoding.UTF8.GetString(match.GetGroupMemory(2).Span));
            Assert.Equal(1, match.GetCaptures(2).Length);
        }
        else Assert.Equal(0, match.GetCaptures(2).Length);
        Assert.True(regex.Inspection.PreparedRegex.ScalarProgram is not null);
        Assert.True(ReferenceEquals(regex.Inspection.PreparedRegex.SemanticRegex.CaptureCatalog, regex.Inspection.PreparedRegex.ScalarProgram!.CaptureCatalog));
    }
}
