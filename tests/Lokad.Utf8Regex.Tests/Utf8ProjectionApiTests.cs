using System.Text;
using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8ProjectionApiTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MatchDetailedProjectsNonByteAlignedDotMatchLikeRuntime(bool compiled)
    {
        const string pattern = ".";
        const string input = "😀";

        var regex = CreateUtf8Regex(pattern, compiled);
        var runtime = CreateRuntimeRegex(pattern, compiled);
        var match = regex.MatchDetailed(Encoding.UTF8.GetBytes(input));
        var expected = runtime.Match(input);

        Assert.True(match.Success);
        Assert.Equal(expected.Index, match.IndexInUtf16);
        Assert.Equal(expected.Length, match.LengthInUtf16);
        Assert.Equal(expected.Value, match.GetValueString());
        Assert.False(match.IsByteAligned);
        Assert.False(match.TryGetByteRange(out _, out _));
        Assert.False(match.TryGetValueBytes(out _));
        AssertByteAccessThrows(
            static (in Utf8MatchContext value) => _ = value.IndexInBytes,
            match);
        AssertByteAccessThrows(
            static (in Utf8MatchContext value) => _ = value.LengthInBytes,
            match);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MatchDetailedProjectsNonByteAlignedGroupAndCaptureLikeRuntime(bool compiled)
    {
        const string pattern = "(.)";
        const string input = "😀";

        var regex = CreateUtf8Regex(pattern, compiled);
        var runtime = CreateRuntimeRegex(pattern, compiled);
        var match = regex.MatchDetailed(Encoding.UTF8.GetBytes(input));
        var expected = runtime.Match(input);
        var group = match.GetGroup(1);
        var expectedGroup = expected.Groups[1];
        var capture = group.GetCapture(0);
        var expectedCapture = expectedGroup.Captures[0];

        Assert.True(group.Success);
        Assert.Equal(expectedGroup.Index, group.IndexInUtf16);
        Assert.Equal(expectedGroup.Length, group.LengthInUtf16);
        Assert.Equal(expectedGroup.Value, group.GetValueString());
        Assert.False(group.IsByteAligned);
        Assert.False(group.TryGetByteRange(out _, out _));
        Assert.False(group.TryGetValueBytes(out _));
        AssertByteAccessThrows(
            static (in Utf8GroupContext value) => _ = value.IndexInBytes,
            group);
        AssertByteAccessThrows(
            static (in Utf8GroupContext value) => _ = value.LengthInBytes,
            group);
        AssertByteAccessThrows(
            static (in Utf8GroupContext value) => _ = value.GetValueBytes(),
            group);

        Assert.True(capture.Success);
        Assert.Equal(expectedCapture.Index, capture.IndexInUtf16);
        Assert.Equal(expectedCapture.Length, capture.LengthInUtf16);
        Assert.Equal(expectedCapture.Value, capture.GetValueString());
        Assert.False(capture.IsByteAligned);
        Assert.False(capture.TryGetByteRange(out _, out _));
        Assert.False(capture.TryGetValueBytes(out _));
        AssertByteAccessThrows(
            static (in Utf8CaptureContext value) => _ = value.IndexInBytes,
            capture);
        AssertByteAccessThrows(
            static (in Utf8CaptureContext value) => _ = value.LengthInBytes,
            capture);
        AssertByteAccessThrows(
            static (in Utf8CaptureContext value) => _ = value.GetValueBytes(),
            capture);
    }

    private static Utf8Regex CreateUtf8Regex(string pattern, bool compiled)
        => new(pattern, compiled ? RegexOptions.Compiled | RegexOptions.CultureInvariant : RegexOptions.CultureInvariant);

    private static Regex CreateRuntimeRegex(string pattern, bool compiled)
        => new(pattern, compiled ? RegexOptions.Compiled | RegexOptions.CultureInvariant : RegexOptions.CultureInvariant, Regex.InfiniteMatchTimeout);

    private delegate void MatchByteAccess(in Utf8MatchContext context);

    private delegate void GroupByteAccess(in Utf8GroupContext context);

    private delegate void CaptureByteAccess(in Utf8CaptureContext context);

    private static void AssertByteAccessThrows(MatchByteAccess accessor, in Utf8MatchContext context)
    {
        try
        {
            accessor(context);
            throw new Xunit.Sdk.XunitException("Expected match byte access to throw.");
        }
        catch (InvalidOperationException)
        {
        }
    }

    private static void AssertByteAccessThrows(GroupByteAccess accessor, in Utf8GroupContext context)
    {
        try
        {
            accessor(context);
            throw new Xunit.Sdk.XunitException("Expected group byte access to throw.");
        }
        catch (InvalidOperationException)
        {
        }
    }

    private static void AssertByteAccessThrows(CaptureByteAccess accessor, in Utf8CaptureContext context)
    {
        try
        {
            accessor(context);
            throw new Xunit.Sdk.XunitException("Expected capture byte access to throw.");
        }
        catch (InvalidOperationException)
        {
        }
    }
}
