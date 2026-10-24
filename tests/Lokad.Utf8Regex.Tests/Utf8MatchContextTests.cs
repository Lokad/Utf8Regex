using System.Text;
using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8MatchContextTests
{
    [Fact]
    public void DefaultMatchViewExposesAnEmptyUnsuccessfulRange()
    {
        var match = default(Utf8MatchContext);
        Assert.False(match.Success);
        Assert.Equal(0, match.GroupCount);
        Assert.True(match.IsByteAligned);
        Assert.Equal((0, 0, 0, 0), (match.IndexInBytes, match.LengthInBytes, match.IndexInUtf16, match.LengthInUtf16));
        Assert.True(match.TryGetByteRange(out var index, out var length));
        Assert.Equal((0, 0), (index, length));
        Assert.True(match.TryGetValueBytes(out var bytes));
        Assert.True(bytes.IsEmpty);
        Assert.Equal(string.Empty, match.GetValueString());
        Assert.False(match.TryGetGroup(0, out var group));
        Assert.False(group.Success);
        Assert.True(group.GetValueBytes().IsEmpty);
    }

    [Fact]
    public void DefaultCaptureViewExposesAnEmptyAbsentRange()
    {
        var capture = default(Utf8CaptureContext);
        Assert.False(capture.Success);
        Assert.True(capture.IsByteAligned);
        Assert.Equal((0, 0, 0, 0), (capture.IndexInBytes, capture.LengthInBytes, capture.IndexInUtf16, capture.LengthInUtf16));
        Assert.True(capture.TryGetByteRange(out var index, out var length));
        Assert.Equal((0, 0), (index, length));
        Assert.True(capture.TryGetValueBytes(out var bytes));
        Assert.True(bytes.IsEmpty);
        Assert.True(capture.GetValueBytes().IsEmpty);
        Assert.Equal(string.Empty, capture.GetValueString());
    }

    [Fact]
    public void MatchDetailedExposesGroupAndCaptureHistoryOutsideReplace()
    {
        var regex = new Utf8Regex("(a)+", RegexOptions.CultureInvariant);

        var match = regex.MatchDetailed("aa"u8);

        Assert.True(match.Success);
        Assert.Equal("aa", match.GetValueString());
        Assert.Equal(2, match.GroupCount);
        Assert.True(match.IsByteAligned);
        Assert.Equal(0, match.IndexInBytes);
        Assert.Equal(2, match.LengthInBytes);
        Assert.True(match.TryGetByteRange(out var matchIndexInBytes, out var matchLengthInBytes));
        Assert.Equal(0, matchIndexInBytes);
        Assert.Equal(2, matchLengthInBytes);
        Assert.True(match.TryGetValueBytes(out var matchValueBytes));
        Assert.Equal("aa"u8.ToArray(), matchValueBytes.ToArray());

        var group = match.GetGroup(1);
        Assert.True(group.Success);
        Assert.True(group.IsByteAligned);
        Assert.Equal(1, group.IndexInUtf16);
        Assert.Equal(1, group.LengthInUtf16);
        Assert.Equal(1, group.IndexInBytes);
        Assert.Equal(1, group.LengthInBytes);
        Assert.Equal("a", group.GetValueString());
        Assert.Equal("a"u8.ToArray(), group.GetValueBytes().ToArray());
        Assert.True(group.TryGetByteRange(out var groupIndexInBytes, out var groupLengthInBytes));
        Assert.Equal(1, groupIndexInBytes);
        Assert.Equal(1, groupLengthInBytes);
        Assert.True(group.TryGetValueBytes(out var groupValueBytes));
        Assert.Equal("a"u8.ToArray(), groupValueBytes.ToArray());
        Assert.Equal(2, group.CaptureCount);
        var firstCapture = group.GetCapture(0);
        Assert.Equal("a", firstCapture.GetValueString());
        Assert.True(firstCapture.TryGetByteRange(out var firstCaptureIndexInBytes, out var firstCaptureLengthInBytes));
        Assert.Equal(0, firstCaptureIndexInBytes);
        Assert.Equal(1, firstCaptureLengthInBytes);
        Assert.True(firstCapture.TryGetValueBytes(out var firstCaptureValueBytes));
        Assert.Equal("a"u8.ToArray(), firstCaptureValueBytes.ToArray());
        Assert.Equal("a", group.GetCapture(1).GetValueString());
    }

    [Fact]
    public void MatchDetailedReturnsUnsuccessfulContextWhenNoMatchExists()
    {
        var regex = new Utf8Regex("cat", RegexOptions.CultureInvariant);

        var match = regex.MatchDetailed("dog"u8);

        Assert.False(match.Success);
        Assert.Equal(string.Empty, match.GetValueString());
        Assert.Equal(1, match.GroupCount);
        Assert.True(match.TryGetByteRange(out var indexInBytes, out var lengthInBytes));
        Assert.Equal(0, indexInBytes);
        Assert.Equal(0, lengthInBytes);
        Assert.True(match.TryGetValueBytes(out var valueBytes));
        Assert.True(valueBytes.IsEmpty);
    }

    [Fact]
    public void StaticMatchDetailedExposesDetailedContext()
    {
        var match = Utf8Regex.MatchDetailed("xxabcxx"u8, "(abc)", RegexOptions.CultureInvariant);

        Assert.True(match.Success);
        Assert.Equal("abc", match.GetValueString());
        Assert.Equal("abc", match.GetGroup(1).GetValueString());
    }

    [Fact]
    public void MatchDetailedSupportsTryGetGroupByName()
    {
        var regex = new Utf8Regex("(?<word>abc)(?<missing>z)?", RegexOptions.CultureInvariant);
        var match = regex.MatchDetailed("abc"u8);

        Assert.True(match.TryGetGroup("word", out var wordGroup));
        Assert.True(wordGroup.Success);
        Assert.Equal("abc", wordGroup.GetValueString());

        Assert.True(match.TryGetGroup("missing", out var missingGroup));
        Assert.False(missingGroup.Success);
        Assert.Equal(string.Empty, missingGroup.GetValueString());

        Assert.False(match.TryGetGroup("nope", out _));
        Assert.True(match.TryGetGroup(1, out var numberedGroup));
        Assert.Equal("abc", numberedGroup.GetValueString());
        Assert.False(match.TryGetGroup(99, out _));
    }

    [Fact]
    public void EvaluatorContextExposesGroupAndCaptureHistory()
    {
        var regex = new Utf8Regex("(a)+", RegexOptions.CultureInvariant);
        var observed = new[] { false };

        var replaced = regex.ReplaceToString(
            "aa"u8,
            observed,
            static (in Utf8MatchContext match, ref bool[] seen) =>
            {
                seen[0] = true;

                Assert.True(match.Success);
                Assert.Equal("aa", match.GetValueString());
                Assert.Equal(2, match.GroupCount);
                Assert.True(match.IsByteAligned);
                Assert.Equal(0, match.IndexInBytes);
                Assert.Equal(2, match.LengthInBytes);

                var group = match.GetGroup(1);
                Assert.True(group.Success);
                Assert.True(group.IsByteAligned);
                Assert.Equal("a", group.GetValueString());
                Assert.Equal("a"u8.ToArray(), group.GetValueBytes().ToArray());
                Assert.Equal(2, group.CaptureCount);
                Assert.Equal("a", group.GetCapture(0).GetValueString());
                Assert.Equal("a", group.GetCapture(1).GetValueString());

                return "X";
            });

        Assert.True(observed[0]);
        Assert.Equal("X", replaced);
    }

    [Fact]
    public void ByteAccessPreservesScalarEvaluatorMatch()
    {
        var regex = new Utf8Regex(".", RegexOptions.CultureInvariant);
        var replaced = regex.ReplaceToString("😀"u8, 0,
            static (in Utf8MatchContext match, ref int calls) =>
            {
                Assert.True(match.Success);
                Assert.Equal((0, 4), (match.IndexInBytes, match.LengthInBytes));
                Assert.Equal((0, 2), (match.IndexInUtf16, match.LengthInUtf16));
                Assert.True(match.IsByteAligned);
                Assert.True(match.TryGetByteRange(out var index, out var length));
                Assert.Equal((0, 4), (index, length));
                Assert.True(match.TryGetValueBytes(out var bytes));
                Assert.True(bytes.SequenceEqual("😀"u8));
                Assert.Equal(0, calls++);
                return "ok";
            });
        Assert.Equal("ok", replaced);
        Assert.True(regex.Inspection.PreparedRegex.ScalarProgram is not null);
    }
}
