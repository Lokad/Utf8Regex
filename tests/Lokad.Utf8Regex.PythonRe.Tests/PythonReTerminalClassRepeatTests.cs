using System.Text;

namespace Lokad.Utf8Regex.PythonRe.Tests;

public sealed class PythonReTerminalClassRepeatTests
{
    [Theory]
    [InlineData(@"\w+", "é²a\u0301𐐀", new[] { "é²a", "𐐀" })]
    [InlineData(@"\w+", "😀!", new string[0])]
    [InlineData(@"\d{2,3}", "\U000104A0\U000104A1\U000104A2 12A9", new[] { "\U000104A0\U000104A1\U000104A2", "12" })]
    [InlineData(@"\D+", "1😀é2_", new[] { "😀é", "_" })]
    [InlineData(@"\s+", "X\u001c \u00a0Y", new[] { "\u001c \u00a0" })]
    [InlineData(@"\S{1,2}", "😀éZ ", new[] { "😀é", "Z" })]
    [InlineData(@"[^\w]+", "a\u0301😀²", new[] { "\u0301😀" })]
    [InlineData(@"(?i)[a-z]+", "KKſİıX", new[] { "KKſİıX" })]
    [InlineData(@"(?ai)[a-z]+", "KKſİıX", new[] { "K", "X" })]
    [InlineData(@"\w{2}", "😀é²abc", new[] { "é²", "ab" })]
    public void PublicCountsValuesAndByteRangesUseThePinnedProfile(string pattern, string text, string[] expected)
    {
        var regex = Create(pattern, 256, 1 << 20);
        var input = Encoding.UTF8.GetBytes(text);
        using var token = new CancellationTokenSource();
        Assert.Equal(expected.Length, regex.Count(input, 0, token.Token));
        var values = regex.FindAllToUtf8(input, 0, token.Token);
        Assert.Equal(expected, values.ScalarValues.Select(Encoding.UTF8.GetString).ToArray());
        var matches = regex.FindIterDetailed(input, 0, token.Token);
        Assert.Equal(expected.Length, matches.Length);
        var previous = 0;
        for (var i = 0; i < expected.Length; i++)
        {
            var start = text.IndexOf(expected[i], previous, StringComparison.Ordinal);
            Assert.True(start >= 0);
            Assert.Equal(Encoding.UTF8.GetByteCount(text.AsSpan(0, start)), matches[i].Value.StartOffsetInBytes);
            Assert.Equal(Encoding.UTF8.GetByteCount(text.AsSpan(0, start + expected[i].Length)), matches[i].Value.EndOffsetInBytes);
            Assert.Null(matches[i].LastIndex);
            Assert.Null(matches[i].LastGroup);
            previous = start + expected[i].Length;
        }
    }

    [Fact]
    public void FewerFramesDoNotRemoveWorkingResultOrCaptureLimits()
    {
        Assert.Equal(2, Create(@"\w+", 1, 1).Count("abc ééé"u8));
        Assert.Equal(Utf8ResourceLimitKind.ExecutionDepth,
            Assert.Throws<Utf8ResourceLimitExceededException>(() => Create(@"(\w+)", 1, 1 << 20).Count("abc ééé"u8)).ResourceKind);
        Assert.Equal(Utf8ResourceLimitKind.ExecutionResultBytes,
            Assert.Throws<Utf8ResourceLimitExceededException>(() => Create(@"\w+", 1, 1).FindAllToUtf8("abc ééé"u8)).ResourceKind);
        var tiny = new Utf8PythonRegex(@"\w+", PythonReCompileOptions.None, Timeout.InfiniteTimeSpan,
            new Utf8ResourcePolicy { MaxWorkingBytes = 1 });
        Assert.Equal(2, tiny.Count("abc ééé"u8));
        var captured = new Utf8PythonRegex(@"(\w+)", PythonReCompileOptions.None, Timeout.InfiniteTimeSpan,
            new Utf8ResourcePolicy { MaxWorkingBytes = 1 });
        Assert.Equal(Utf8ResourceLimitKind.ExecutionWorkingBytes,
            Assert.Throws<Utf8ResourceLimitExceededException>(() => captured.Count("abc"u8)).ResourceKind);
    }

    [Fact]
    public void StartFloorsWholeValidationAndPinnedNewerCharactersRemainAuthoritative()
    {
        var regex = Create(@"\w+", 1, 1 << 20);
        Assert.Equal(1, regex.Count("😀é² ab"u8, 9));
        Assert.Equal(9, regex.Search("😀é² ab"u8, 9).StartOffsetInBytes);
        Assert.Throws<ArgumentOutOfRangeException>(() => regex.Count("😀é² ab"u8, 1));
        Assert.Throws<ArgumentException>(() => regex.Count(new byte[] { (byte)'a', 255 }));
        Assert.Equal(0, regex.Count(Encoding.UTF8.GetBytes("\u1c89\U0001e5f1")));
    }

    [Fact]
    public void CursorAliasesAndRetainedCompletionSurviveTheNextRunAndDisposal()
    {
        var regex = Create(@"\w+", 1, 1 << 20);
        using var cursor = regex.CreateMatchCursor("é²!😀ab"u8.ToArray());
        var alias = cursor;
        Assert.True(cursor.MoveNext());
        var first = alias.Current;
        Assert.Equal("é²", first.Value.ValueText);
        Assert.True(alias.MoveNext());
        Assert.Equal("ab", cursor.Current.Value.ValueText);
        Assert.Equal(9, cursor.Current.Value.StartOffsetInBytes);
        alias.Dispose();
        Assert.Throws<ObjectDisposedException>(() => cursor.MoveNext());
        Assert.Equal("é²", first.Value.ValueText);
    }

    [Fact]
    public void FullMatchAnchoringAndFollowingFailureRetainTheirGeneralPaths()
    {
        var regex = Create(@"\w+", 256, 1 << 20);
        Assert.True(regex.FullMatch("é²"u8).Success);
        Assert.False(regex.FullMatch("abc!"u8).Success);
        Assert.False(regex.Match("😀abc"u8).Success);
        Assert.Equal(4, regex.Search("😀abc"u8).StartOffsetInBytes);
        Assert.Equal("abcZ", Encoding.UTF8.GetString(Create(@"\w+Z", 256, 1 << 20).FindAllToUtf8("abcZ"u8).ScalarValues[0]));
        Assert.Equal("XYZ", Encoding.UTF8.GetString(Create(@"(?<=\w{2})\w+", 256, 1 << 20).FindAllToUtf8("abXYZ"u8).ScalarValues[0]));
    }

    private static Utf8PythonRegex Create(string pattern, uint depth, ulong results) => new(pattern,
        PythonReCompileOptions.None, Timeout.InfiniteTimeSpan,
        new Utf8ResourcePolicy { MaxWorkSteps = 1000000, MaxDepth = depth,
            MaxWorkingBytes = 1 << 20, MaxResultBytes = results });
}
