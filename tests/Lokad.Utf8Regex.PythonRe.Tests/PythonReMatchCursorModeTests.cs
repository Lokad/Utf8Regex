using System.Text;

namespace Lokad.Utf8Regex.PythonRe.Tests;

public sealed class PythonReMatchCursorModeTests
{
    [Theory]
    [InlineData(Utf8PythonMatchMode.Search, "(?P<w>[a-z]+)", "π abc def😀tail", 3, 10, 3, 6, 1)]
    [InlineData(Utf8PythonMatchMode.Match, "(?P<w>[a-z]+)", "π abc def😀tail", 3, 10, 3, 6, 1)]
    [InlineData(Utf8PythonMatchMode.FullMatch, "(?P<w>[a-z]+)", "π abc def😀tail", 3, 10, -1, -1, null)]
    [InlineData(Utf8PythonMatchMode.FullMatch, "(?P<w>[a-z]+) (?P<x>[a-z]+)", "π abc def😀tail", 3, 10, 3, 10, 2)]
    [InlineData(Utf8PythonMatchMode.Match, "(?<=π )(?P<w>abc)(?= def)", "π abc def😀tail", 3, 10, 3, 6, 1)]
    [InlineData(Utf8PythonMatchMode.Match, "(?<=π )(?P<w>abc)(?= def)", "π abc def😀tail", 3, 6, -1, -1, null)]
    [InlineData(Utf8PythonMatchMode.Match, "(?P<x>a|ab)", "abX", 0, 2, 0, 1, 1)]
    [InlineData(Utf8PythonMatchMode.FullMatch, "(?P<x>a|ab)", "abX", 0, 2, 0, 2, 1)]
    [InlineData(Utf8PythonMatchMode.FullMatch, "(?P<outer>a(?P<inner>b))", "abX", 0, 2, 0, 2, 1)]
    [InlineData(Utf8PythonMatchMode.Match, "(?P<e>)", "😀x", 0, 4, 0, 0, 1)]
    [InlineData(Utf8PythonMatchMode.FullMatch, "(?P<e>)", "😀x", 0, 4, -1, -1, null)]
    [InlineData(Utf8PythonMatchMode.FullMatch, "(?P<e>)", "😀x", 0, 0, 0, 0, 1)]
    public void ModesAndBoundsPreservePythonExtentsAndCompletionOrder(Utf8PythonMatchMode mode,
        string pattern, string text, int start, int end, int expectedStart, int expectedEnd,
        int? lastIndex)
    {
        var input = Encoding.UTF8.GetBytes(text);
        var regex = new Utf8PythonRegex(pattern);
        using var cursor = regex.CreateMatchCursor(input, start, mode: mode, endOffsetInBytes: end);
        var found = cursor.MoveNext(out var match);
        Assert.Equal(expectedStart >= 0, found);
        if (!found) return;
        Assert.Equal((expectedStart, expectedEnd), (match.Value.StartOffsetInBytes, match.Value.EndOffsetInBytes));
        Assert.Equal(lastIndex, match.LastIndex);
        var retained = match;
        if (mode != Utf8PythonMatchMode.Search)
        {
            Assert.False(cursor.MoveNext(out _));
            Assert.False(cursor.MoveNext(out _));
        }
        cursor.Dispose();
        Assert.Equal(lastIndex, retained.LastIndex);
        Assert.Equal(input.AsSpan(expectedStart, expectedEnd - expectedStart).ToArray(), retained.GetValueMemory().ToArray());
    }

    [Fact]
    public void BoundedSearchKeepsScalarEmptyProgression()
    {
        using var cursor = new Utf8PythonRegex("(?P<e>)").CreateMatchCursor("😀x"u8.ToArray(), endOffsetInBytes: 4);
        Assert.True(cursor.MoveNext(out var first)); Assert.Equal(0, first.Value.StartOffsetInBytes);
        Assert.True(cursor.MoveNext(out var second)); Assert.Equal(4, second.Value.StartOffsetInBytes);
        Assert.False(cursor.MoveNext(out _));
    }

    [Theory]
    [InlineData(@"\B", " 😀", 0, false)]
    [InlineData(@"\B", " 😀", 1, true)]
    [InlineData(@"\B", "π!", 2, false)]
    [InlineData(@"(?a:\B)", "π!", 2, true)]
    public void NonBoundaryAtTheSelectedEndUsesTheEffectiveSubject(
        string pattern, string text, int offset, bool expected)
    {
        var input = Encoding.UTF8.GetBytes(text);
        using var regex = new Utf8PythonRegex(pattern);
        var value = regex.Match(input, offset, mode: Utf8PythonMatchMode.FullMatch, endOffsetInBytes: offset);
        using var cursor = regex.CreateMatchCursor(input, offset,
            mode: Utf8PythonMatchMode.FullMatch, endOffsetInBytes: offset);
        Assert.Equal(expected, value.Success);
        Assert.Equal(expected, cursor.MoveNext(out var snapshot));
        if (expected)
        {
            Assert.Equal((offset, offset), (value.StartOffsetInBytes, value.EndOffsetInBytes));
            Assert.Equal((offset, offset), (snapshot.Value.StartOffsetInBytes, snapshot.Value.EndOffsetInBytes));
        }
    }

    [Theory] [InlineData(Utf8PythonMatchMode.Match)] [InlineData(Utf8PythonMatchMode.FullMatch)]
    public void PositionedSingleCursorRefundsScratchAndPreservesRetainedMetadata(Utf8PythonMatchMode mode)
    {
        var account = new Account();
        using var cursor = new Utf8PythonRegex("(?P<x>a+)").CreateMatchCursor("aaa"u8.ToArray(),
            workingMemoryAllowance: account, mode: mode);
        Assert.True(cursor.MoveNext(out var match)); Assert.True(account.Used > 0);
        Assert.False(cursor.MoveNext(out _)); Assert.Equal(0UL, account.Used);
        Assert.Equal("aaa", Encoding.UTF8.GetString(match.GetGroupMemory(1).Span));
        Assert.Equal(1, match.LastIndex);
    }

    [Fact]
    public void OriginalCancellationStillAppliesAfterSinglePublication()
    {
        using var source = new CancellationTokenSource();
        using var cursor = new Utf8PythonRegex("(?P<x>a)").CreateMatchCursor("a"u8.ToArray(),
            cancellationToken: source.Token, mode: Utf8PythonMatchMode.Match);
        Assert.True(cursor.MoveNext(out var retained)); source.Cancel();
        Assert.Equal(source.Token, Assert.Throws<OperationCanceledException>(() => cursor.MoveNext(out _)).CancellationToken);
        Assert.Equal(1, retained.LastIndex);
    }

    [Theory] [InlineData(-1)] [InlineData(1)] [InlineData(3)] [InlineData(5)]
    public void InvalidOrNonScalarEndIsRejected(int end)
    {
        var regex = new Utf8PythonRegex(".");
        Assert.Equal("endOffsetInBytes", Assert.Throws<ArgumentOutOfRangeException>(() =>
            regex.CreateMatchCursor("😀"u8.ToArray(), endOffsetInBytes: end)).ParamName);
    }

    [Theory] [InlineData(Utf8PythonMatchMode.Match)] [InlineData(Utf8PythonMatchMode.FullMatch)]
    public void SinglePublicationRetainedReceiptOutlivesCursorAliases(Utf8PythonMatchMode mode)
    {
        var account = new Account();
        var cursor = new Utf8PythonRegex("(?P<outer>(?P<inner>a))").CreateMatchCursor("a"u8.ToArray(),
            workingMemoryAllowance: account, mode: mode);
        var alias = cursor;
        Assert.True(cursor.MoveNextRetained(out var receipt));
        using (receipt)
        {
            var retained = receipt.Match;
            Assert.Equal(1, retained.LastIndex);
            Assert.False(alias.MoveNext(out _));
            Assert.Equal(receipt.ChargedBytes, account.Used);
            cursor.Dispose(); alias.Dispose();
            Assert.Equal("a", Encoding.UTF8.GetString(retained.GetGroupMemory(2).Span));
            receipt.Dispose();
            Assert.Equal(0UL, account.Used);
            Assert.Equal("a", Encoding.UTF8.GetString(retained.GetValueMemory().Span));
        }
    }

    [Fact]
    public void PreCancellationWinsBeforeModeAndBoundsChecks()
    {
        using var source = new CancellationTokenSource(); source.Cancel();
        var regex = new Utf8PythonRegex(".");
        var error = Assert.Throws<OperationCanceledException>(() => regex.CreateMatchCursor("a"u8.ToArray(),
            -1, source.Token, mode: (Utf8PythonMatchMode)99, endOffsetInBytes: -1));
        Assert.Equal(source.Token, error.CancellationToken);
        Assert.Equal("mode", Assert.Throws<ArgumentOutOfRangeException>(() =>
            regex.CreateMatchCursor("a"u8.ToArray(), mode: (Utf8PythonMatchMode)99)).ParamName);
        Assert.Equal("startOffsetInBytes", Assert.Throws<ArgumentOutOfRangeException>(() =>
            regex.CreateMatchCursor("ab"u8.ToArray(), 2, endOffsetInBytes: 1)).ParamName);
    }

    private sealed class Account : IUtf8MemoryAllowance
    {
        internal ulong Used;
        public bool TryReserve(ulong bytes) { if (bytes > (1UL << 20) - Used) return false; Used += bytes; return true; }
        public void Release(ulong bytes) { Assert.InRange(bytes, 0UL, Used); Used -= bytes; }
    }
}
