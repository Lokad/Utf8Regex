using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8RegexStartAtTests
{
    [Fact]
    public void ByteStartMatchPreservesOriginalAnchorContext()
    {
        var regex = new Utf8Regex(@"\Aabc", RegexOptions.CultureInvariant);

        Assert.False(regex.Match("xxabc"u8, 2, []).Success);
    }

    [Fact]
    public void CountStartAtMatchesRuntimeSemantics()
    {
        var regex = new Utf8Regex("abc", RegexOptions.CultureInvariant);

        Assert.Equal(1, Utf8MatchSequence.Count(regex.EnumerateMatches("abcabc"u8, 3)));
    }

    [Fact]
    public void ByteStartMatchReturnsFirstSelectedRange()
    {
        var regex = new Utf8Regex("abc", RegexOptions.CultureInvariant);

        var match = regex.Match("xxabcxxabc"u8, 5, []);

        Assert.True(match.Success);
        Assert.Equal(7, match.StartOffsetInBytes);
        Assert.Equal(3, (match.EndOffsetInBytes - match.StartOffsetInBytes));
    }

    [Fact]
    public void CaptureCursorStartsAtTheRequestedBytePosition()
    {
        var regex = new Utf8Regex("(abc)", RegexOptions.CultureInvariant);

        using var cursor = regex.CreateMatchCursor("xxabcxxabc"u8.ToArray(), 5);
        Assert.True(cursor.MoveNext(out var match));
        Assert.Equal("abc"u8.ToArray(), match.GetValueMemory().ToArray());
        Assert.Equal("abc"u8.ToArray(), match.GetGroupMemory(1).ToArray());
        Assert.Equal((7, 10), (match.Value.StartOffsetInBytes, match.Value.EndOffsetInBytes));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void EnumerateMatchesStartAtHonorsRightToLeftStartPosition(bool compiled)
    {
        var options = RegexOptions.CultureInvariant | RegexOptions.RightToLeft |
            (compiled ? RegexOptions.Compiled : RegexOptions.None);
        var regex = new Utf8Regex("abc", options);

        var enumerator = regex.EnumerateMatches("xxabcxxabc"u8, 6);
        Assert.True(enumerator.MoveNext());

        var match = enumerator.Current;
        Assert.True(match.Success);
        Assert.Equal(2, match.StartOffsetInBytes);
        Assert.Equal(3, (match.EndOffsetInBytes - match.StartOffsetInBytes));
    }

    [Fact]
    public void EnumerateMatchesStartAtReturnsByteAlignedMatchesFromScalarBoundary()
    {
        var regex = new Utf8Regex("abc", RegexOptions.CultureInvariant);

        var enumerator = regex.EnumerateMatches("éabcéabc"u8, 2);
        Assert.True(enumerator.MoveNext());

        var match = enumerator.Current;
        Assert.True(match.Success);
        Assert.True(match.Success);
        Assert.Equal(3, (match.EndOffsetInBytes - match.StartOffsetInBytes));
        Assert.Equal(2, match.StartOffsetInBytes);
        Assert.Equal(3, (match.EndOffsetInBytes - match.StartOffsetInBytes));
    }

    [Fact]
    public void CompiledStructuralFamilyEnumerationStartAtPreservesBaseOffsets()
    {
        var regex = new Utf8Regex(
            @"\b(?:LogError|LogWarning|LogInformation)\s*\(",
            RegexOptions.CultureInvariant | RegexOptions.Compiled);

        var enumerator = regex.EnumerateMatches("é LogError( xx LogWarning ("u8, 13);
        Assert.True(enumerator.MoveNext());

        var match = enumerator.Current;
        Assert.True(match.Success);
        Assert.True(match.Success);
        Assert.Equal(12, (match.EndOffsetInBytes - match.StartOffsetInBytes));
        Assert.Equal(16, match.StartOffsetInBytes);
        Assert.Equal(12, (match.EndOffsetInBytes - match.StartOffsetInBytes));
        Assert.False(enumerator.MoveNext());
    }

    [Fact]
    public void FromOffsetRejectsScalarInterior()
    {
        Utf8AsciiScalarOperationAssertions.Offset("a", RegexOptions.CultureInvariant, "😀a", 1);
    }

    [Fact]
    public void StartAtThrowsWhenBeyondSubjectLength()
    {
        var regex = new Utf8Regex("abc", RegexOptions.CultureInvariant);

        Assert.Throws<ArgumentOutOfRangeException>(() => regex.Match("abc"u8, 4, []).Success);
        Assert.Throws<ArgumentOutOfRangeException>(() => Utf8MatchSequence.Count(regex.EnumerateMatches("abc"u8, 4)));
        Assert.Throws<ArgumentOutOfRangeException>(() => Utf8MatchSequence.First(regex.CreateMatchCursor("abc"u8.ToArray(), 4)));
        Assert.Throws<ArgumentOutOfRangeException>(() => regex.EnumerateMatches("abc"u8, 4));
    }
}
