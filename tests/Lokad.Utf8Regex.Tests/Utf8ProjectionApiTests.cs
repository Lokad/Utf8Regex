using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8ProjectionApiTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MatchDetailedProjectsCompleteScalar(bool compiled)
    {
        var regex = CreateUtf8Regex(".", compiled);
        var match = regex.MatchDetailed("😀"u8);
        Assert.True(match.Success);
        Assert.Equal((0, 4), (match.IndexInBytes, match.LengthInBytes));
        Assert.Equal((0, 2), (match.IndexInUtf16, match.LengthInUtf16));
        Assert.Equal("😀", match.GetValueString());
        Assert.True(match.IsByteAligned);
        Assert.True(match.TryGetByteRange(out var index, out var length));
        Assert.Equal((0, 4), (index, length));
        Assert.True(match.TryGetValueBytes(out var bytes));
        Assert.True(bytes.SequenceEqual("😀"u8));
        Assert.True(regex.Inspection.PreparedRegex.ScalarProgram is not null);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MatchDetailedProjectsCompleteScalarGroupAndCapture(bool compiled)
    {
        var regex = CreateUtf8Regex("(.)", compiled);
        var match = regex.MatchDetailed("😀"u8);
        var group = match.GetGroup(1);
        var capture = group.GetCapture(0);
        Assert.True(match.Success);
        Assert.Equal((0, 4), (match.IndexInBytes, match.LengthInBytes));
        Assert.True(group.Success);
        Assert.Equal(1, group.CaptureCount);
        Assert.Equal((0, 4), (group.IndexInBytes, group.LengthInBytes));
        Assert.Equal((0, 2), (group.IndexInUtf16, group.LengthInUtf16));
        Assert.Equal("😀", group.GetValueString());
        Assert.True(group.IsByteAligned);
        Assert.True(group.TryGetByteRange(out var index, out var length));
        Assert.Equal((0, 4), (index, length));
        Assert.True(group.TryGetValueBytes(out var bytes));
        Assert.True(bytes.SequenceEqual("😀"u8));
        Assert.True(group.GetValueBytes().SequenceEqual("😀"u8));
        Assert.True(capture.Success);
        Assert.Equal((0, 4), (capture.IndexInBytes, capture.LengthInBytes));
        Assert.Equal((0, 2), (capture.IndexInUtf16, capture.LengthInUtf16));
        Assert.Equal("😀", capture.GetValueString());
        Assert.True(capture.IsByteAligned);
        Assert.True(capture.TryGetByteRange(out index, out length));
        Assert.Equal((0, 4), (index, length));
        Assert.True(capture.TryGetValueBytes(out bytes));
        Assert.True(bytes.SequenceEqual("😀"u8));
        Assert.True(capture.GetValueBytes().SequenceEqual("😀"u8));
        Assert.True(regex.Inspection.PreparedRegex.ScalarProgram is not null);
    }

    private static Utf8Regex CreateUtf8Regex(string pattern, bool compiled)
        => new(pattern, compiled ? RegexOptions.Compiled | RegexOptions.CultureInvariant : RegexOptions.CultureInvariant);
}
