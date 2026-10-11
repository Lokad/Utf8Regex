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
        var regex = new Utf8Regex("(a(.)c)", RegexOptions.CultureInvariant | (compiled ? RegexOptions.Compiled : 0));
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
        var regex = new Utf8Regex("(a(.)c)", RegexOptions.CultureInvariant | (compiled ? RegexOptions.Compiled : 0));
        var input = Encoding.UTF8.GetBytes(new string('x', 100000) + (miss ? "ayz" : "ayc"));
        Assert.Equal(!miss, regex.MatchDetailed(input).Success);
        var before = GC.GetAllocatedBytesForCurrentThread();
        var match = regex.MatchDetailed(input);
        Assert.Equal(!miss, match.Success);
        if (!miss)
        {
            Assert.Equal((100000, 3), (match.IndexInBytes, match.LengthInBytes));
            Assert.Equal("ayc", match.GetGroup(1).GetValueString());
            Assert.Equal("y", match.GetGroup(2).GetValueString());
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
        var regex = new Utf8Regex("(a|(.)c)", options);
        var text = supplementary ? "x😀c" : "xac";
        var input = Encoding.UTF8.GetBytes(text);
        var match = regex.MatchDetailed(input);
        var length = supplementary ? 5 : reverse ? 2 : 1;
        Assert.True(match.Success);
        Assert.Equal((1, length, 1, supplementary ? 3 : length),
            (match.IndexInBytes, match.LengthInBytes, match.IndexInUtf16, match.LengthInUtf16));
        Assert.Equal(3, match.GroupCount);
        Assert.Equal(supplementary ? "😀c" : reverse ? "ac" : "a", match.GetValueString());
        var first = match.GetGroup(1);
        Assert.True(first.Success);
        Assert.Equal(match.GetValueString(), first.GetValueString());
        Assert.Equal(1, first.CaptureCount);
        Assert.Equal((1, length), (first.GetCapture(0).IndexInBytes, first.GetCapture(0).LengthInBytes));
        var second = match.GetGroup(2);
        Assert.Equal(supplementary || reverse, second.Success);
        if (second.Success)
        {
            Assert.Equal((1, supplementary ? 4 : 1), (second.IndexInBytes, second.LengthInBytes));
            Assert.Equal(supplementary ? "😀" : "a", second.GetValueString());
            Assert.Equal(1, second.CaptureCount);
        }
        else Assert.Equal(0, second.CaptureCount);
        Assert.True(regex.Inspection.PreparedRegex.ScalarProgram is not null);
        Assert.True(ReferenceEquals(regex.Inspection.PreparedRegex.SemanticRegex.CaptureCatalog, regex.Inspection.PreparedRegex.ScalarProgram!.CaptureCatalog));
    }
}
