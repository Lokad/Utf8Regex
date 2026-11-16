namespace Lokad.Utf8Regex.Tests;

// Aggregate the production enumerator without hiding its subject/start/token.
internal static class Utf8MatchSequence
{
    internal static int Count(Utf8ValueMatchEnumerator matches)
    {
        var count = 0;
        while (matches.MoveNext()) count = checked(count + 1);
        return count;
    }

    internal static void AssertRange(Utf8CaptureRange actual, string text, int index, int length)
    {
        Assert.True(actual.Success);
        Assert.Equal((System.Text.Encoding.UTF8.GetByteCount(text.AsSpan(0, index)),
            System.Text.Encoding.UTF8.GetByteCount(text.AsSpan(0, index + length))),
            (actual.StartOffsetInBytes, actual.EndOffsetInBytes));
    }

    internal static void EqualValue(Utf8ValueMatch expected, Utf8CaptureRange actual)
    {
        Assert.Equal(expected.Success, actual.Success);
        Assert.Equal(expected.Success ? (expected.IndexInBytes, expected.IndexInBytes + expected.LengthInBytes) : (0, 0),
            (actual.StartOffsetInBytes, actual.EndOffsetInBytes));
    }
}
