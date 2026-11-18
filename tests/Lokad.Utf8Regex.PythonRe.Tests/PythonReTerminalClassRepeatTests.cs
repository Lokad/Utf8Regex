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
    [InlineData(@"\w*", "a😀", new[] { "a", "", "" })]
    [InlineData(@"[a-z]{0,2}", "abc😀", new[] { "ab", "c", "", "" })]
    [InlineData(@"\d*", "1a", new[] { "1", "", "" })]
    public void PublicCountsValuesAndByteRangesUseThePinnedProfile(string pattern, string text, string[] expected)
    {
        var regex = Create(pattern, 256, 1 << 20);
        var input = Encoding.UTF8.GetBytes(text);
        using var token = new CancellationTokenSource();
        Assert.Equal(expected.Length, regex.Count(input, 0, token.Token));
        ReadOnlyMemory<byte> snapshots1Input = input;
        var snapshots1 = new List<Utf8PythonRangeMatch>();
        using (var snapshots1Cursor = regex.CreateMatchCursor(snapshots1Input, 0, token.Token))
        {
            while (snapshots1Cursor.MoveNext(out var snapshots1Match))
                snapshots1.Add(snapshots1Match);
        }
        var values = snapshots1;
        Assert.Equal(expected, values.Select(captureSnapshot1 => captureSnapshot1.GetGroupMemory(captureSnapshot1.Groups.Length == 2 ? 1 : 0).ToArray()).ToArray().Select(Encoding.UTF8.GetString).ToArray());
        var collectedRanges1 = new List<Utf8PythonRangeMatch>();
        using (var rangeCursor1 = regex.CreateMatchCursor(input, 0, token.Token))
        {
            while (rangeCursor1.MoveNext(out var rangeMatch1))
                collectedRanges1.Add(rangeMatch1);
        }
        var matches = collectedRanges1.ToArray();
        Assert.Equal(expected.Length, matches.Length);
        var previous = 0;
        for (var i = 0; i < expected.Length; i++)
        {
            // These greedy atom roots cannot consume after an empty match at
            // the same start; the next empty result advances one scalar.
            if (i > 0 && expected[i - 1].Length == 0 && previous < text.Length)
            {
                previous += char.IsSurrogatePair(text, previous) ? 2 : 1;
            }
            var start = text.IndexOf(expected[i], previous, StringComparison.Ordinal);
            Assert.True(start >= 0);
            Assert.Equal(Encoding.UTF8.GetByteCount(text.AsSpan(0, start)), matches[i].Value.StartOffsetInBytes);
            Assert.Equal(Encoding.UTF8.GetByteCount(text.AsSpan(0, start + expected[i].Length)), matches[i].Value.EndOffsetInBytes);
            Assert.Null(matches[i].LastIndex);
            previous = start + expected[i].Length;
        }
    }

    [Fact]
    public void StorageFreeCountsKeepObservedCaptureAndOutputLimits()
    {
        Assert.Equal(2, Create(@"\w+", 1, 1).Count("abc ééé"u8));
        Assert.Equal(2, Create(@"(\w+)", 1, 1 << 20).Count("abc ééé"u8));
        Assert.Equal(2, Create(@"(\w)+", 1, 1 << 20).Count("abc ééé"u8));
        Assert.Equal(Utf8ResourceLimitKind.ExecutionDepth,
            Assert.Throws<Utf8ResourceLimitExceededException>(() =>
            {
                var snapshots2Regex = Create(@"(\w)+", 1, 1 << 20);
                ReadOnlyMemory<byte> snapshots2Input = ("abc ééé"u8).ToArray();
                var snapshots2 = new List<Utf8PythonRangeMatch>();
                using (var snapshots2Cursor = snapshots2Regex.CreateMatchCursor(snapshots2Input, 0, default))
                {
                    while (snapshots2Cursor.MoveNext(out var snapshots2Match))
                        snapshots2.Add(snapshots2Match);
                }
                return snapshots2;
            }).ResourceKind);
        Assert.Equal(Utf8ResourceLimitKind.ExecutionResultBytes,
            Assert.Throws<Utf8ResourceLimitExceededException>(() =>
            {
                var snapshots3Regex = Create(@"\w+", 1, 1);
                ReadOnlyMemory<byte> snapshots3Input = ("abc ééé"u8).ToArray();
                var snapshots3 = new List<Utf8PythonRangeMatch>();
                using (var snapshots3Cursor = snapshots3Regex.CreateMatchCursor(snapshots3Input, 0, default))
                {
                    while (snapshots3Cursor.MoveNext(out var snapshots3Match))
                        snapshots3.Add(snapshots3Match);
                }
                return snapshots3;
            }).ResourceKind);
        var tiny = new Utf8PythonRegex(@"\w+", options: PythonReCompileOptions.None, policy: new Utf8RegexPolicy()
        {
            MatchTimeout = Timeout.InfiniteTimeSpan,
            ResourcePolicy = new Utf8ResourcePolicy
            {
                MaxWorkingBytes = 1
            }
        });
        Assert.Equal(2, tiny.Count("abc ééé"u8));
        var captured = new Utf8PythonRegex(@"(\w+)", options: PythonReCompileOptions.None, policy: new Utf8RegexPolicy()
        {
            MatchTimeout = Timeout.InfiniteTimeSpan,
            ResourcePolicy = new Utf8ResourcePolicy
            {
                MaxWorkingBytes = 1
            }
        });
        Assert.Equal(1, captured.Count("abc"u8));
        Assert.Equal(Utf8ResourceLimitKind.ExecutionWorkingBytes,
            Assert.Throws<Utf8ResourceLimitExceededException>(() =>
            {
                ReadOnlyMemory<byte> snapshots4Input = ("abc"u8).ToArray();
                var snapshots4 = new List<Utf8PythonRangeMatch>();
                using (var snapshots4Cursor = captured.CreateMatchCursor(snapshots4Input, 0, default))
                {
                    while (snapshots4Cursor.MoveNext(out var snapshots4Match))
                        snapshots4.Add(snapshots4Match);
                }
                return snapshots4;
            }).ResourceKind);
    }

    [Fact]
    public void ScalarStartFloorsAndPinnedNewerCharactersRemainAuthoritative()
    {
        var regex = Create(@"\w+", 1, 1 << 20);
        Assert.Equal(1, regex.Count("😀é² ab"u8, 9));
        Assert.Equal(9, regex.Match("😀é² ab"u8, 9, mode: Utf8PythonMatchMode.Search).StartOffsetInBytes);
        Assert.Throws<ArgumentOutOfRangeException>(() => regex.Count("😀é² ab"u8, 1));

        Assert.Equal(0, regex.Count(Encoding.UTF8.GetBytes("\u1c89\U0001e5f1")));
    }

    [Fact]
    public void CursorAliasesAndRetainedCompletionSurviveTheNextRunAndDisposal()
    {
        Utf8PythonRangeMatch cursorRange = default;
        var regex = Create(@"\w+", 1, 1 << 20);
        using var cursor = regex.CreateMatchCursor("é²!😀ab"u8.ToArray());
        var alias = cursor;
        Assert.True(cursor.MoveNext(out cursorRange));
        var first = cursorRange;
        Assert.Equal("é²", System.Text.Encoding.UTF8.GetString(first.GetValueMemory().Span));
        Assert.True(alias.MoveNext(out cursorRange));
        Assert.Equal("ab", System.Text.Encoding.UTF8.GetString(cursorRange.GetValueMemory().Span));
        Assert.Equal(9, cursorRange.Value.StartOffsetInBytes);
        alias.Dispose();
        Assert.Throws<ObjectDisposedException>(() => cursor.MoveNext(out _));
        Assert.Equal("é²", System.Text.Encoding.UTF8.GetString(first.GetValueMemory().Span));
    }

    [Fact]
    public void FullMatchAnchoringAndFollowingFailureRetainTheirGeneralPaths()
    {
        var regex = Create(@"\w+", 256, 1 << 20);
        Assert.True(regex.Match("é²"u8, mode: Utf8PythonMatchMode.FullMatch).Success);
        Assert.False(regex.Match("abc!"u8, mode: Utf8PythonMatchMode.FullMatch).Success);
        Assert.False(regex.Match("😀abc"u8).Success);
        Assert.Equal(4, regex.Match("😀abc"u8, mode: Utf8PythonMatchMode.Search).StartOffsetInBytes);
        var snapshots5Regex = Create(@"\w+Z", 256, 1 << 20);
        ReadOnlyMemory<byte> snapshots5Input = ("abcZ"u8).ToArray();
        var snapshots5 = new List<Utf8PythonRangeMatch>();
        using (var snapshots5Cursor = snapshots5Regex.CreateMatchCursor(snapshots5Input, 0, default))
        {
            while (snapshots5Cursor.MoveNext(out var snapshots5Match))
                snapshots5.Add(snapshots5Match);
        }
        Assert.Equal("abcZ", Encoding.UTF8.GetString(snapshots5.Select(captureSnapshot2 => captureSnapshot2.GetGroupMemory(captureSnapshot2.Groups.Length == 2 ? 1 : 0).ToArray()).ToArray()[0]));
        var snapshots6Regex = Create(@"(?<=\w{2})\w+", 256, 1 << 20);
        ReadOnlyMemory<byte> snapshots6Input = ("abXYZ"u8).ToArray();
        var snapshots6 = new List<Utf8PythonRangeMatch>();
        using (var snapshots6Cursor = snapshots6Regex.CreateMatchCursor(snapshots6Input, 0, default))
        {
            while (snapshots6Cursor.MoveNext(out var snapshots6Match))
                snapshots6.Add(snapshots6Match);
        }
        Assert.Equal("XYZ", Encoding.UTF8.GetString(snapshots6.Select(captureSnapshot3 => captureSnapshot3.GetGroupMemory(captureSnapshot3.Groups.Length == 2 ? 1 : 0).ToArray()).ToArray()[0]));
    }

    private static Utf8PythonRegex Create(string pattern, uint depth, ulong results) => new(pattern, options: PythonReCompileOptions.None, policy: new Utf8RegexPolicy()
    {
        MatchTimeout = Timeout.InfiniteTimeSpan,
        ResourcePolicy = new Utf8ResourcePolicy
        {
            MaxWorkSteps = 1000000,
            MaxDepth = depth,
            MaxWorkingBytes = 1 << 20,
            MaxResultBytes = results
        }
    });
}
