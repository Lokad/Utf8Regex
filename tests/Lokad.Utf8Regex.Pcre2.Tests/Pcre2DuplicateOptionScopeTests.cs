using System.Text;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Pcre2;

namespace Lokad.Utf8Regex.Pcre2.Tests;

[Collection(Pcre2AllocationTestCollection.Name)]
public sealed class Pcre2DuplicateOptionScopeTests
{
    [Theory]
    [InlineData("(?J:(?<x>a)|(?<x>b))", "b", "b")]
    [InlineData(@"(?J:(?<x>a)|(?<x>b))\k<x>", "bb", "bb")]
    [InlineData(@"(?J:(?<x>😀)|(?<x>é))\k<x>", "éé", "éé")]
    [InlineData("(?J)(?-J:(?<x>a))(?<x>b)", "ab", "ab")]
    [InlineData("(?:(?J)(?<x>a)|(?<x>b))", "b", "b")]
    [InlineData("(?J:(?<x>a)(?-J:(?<y>b))|(?<x>c))", "c", "c")]
    [InlineData("(?J:(?<x>a)|(?<x>b))(?(x)c|d)", "bc", "bc")]
    [InlineData("(?^J:(?<x>a)|(?<x>b))", "b", "b")]
    [InlineData("(?J:(?<x>a)|(?<x>b))(?<y>c)", "bc", "bc")]
    [InlineData("(?J:(?<x>a)|(?<x>b))(?J:(?<x>c))", "bc", "bc")]
    [InlineData("(?J)(?:(?-J)(?<x>a))(?<x>b)", "ab", "ab")]
    [InlineData("(?J)(?^:(?<x>a)|(?<x>b))", "b", "b")]
    [InlineData("(?J)(?|(?-J:(?<x>a))|(?-J:(?<x>b)))", "b", "b")]
    public void PermissionFollowsLexicalScopeAndPersistsAcrossAlternatives(
        string pattern, string text, string expected)
    {
        using var cancellation = new CancellationTokenSource();
        var regex = new Utf8Pcre2Regex(pattern, Pcre2CompileOptions.None, default,
            new() { MatchLimit = 100000, DepthLimit = 256, HeapLimitInBytes = 1 << 20,
                ResultLimitInBytes = 1 << 20 }, Timeout.InfiniteTimeSpan);
        var bytes = Encoding.UTF8.GetBytes(text);
        Assert.True(regex.IsMatch(bytes, 0, Pcre2MatchOptions.None, cancellation.Token));
        var match = regex.MatchDetailed(bytes, 0, Pcre2MatchOptions.None, cancellation.Token);
        Assert.Equal(expected, match.GetValueString());
        Assert.True(match.TryGetFirstSetGroup("x", out var capture));
        Assert.True(capture.Success);
    }

    [Theory]
    [InlineData("(?J:(?<x>a))(?<x>b)")]
    [InlineData("(?:(?J)(?<x>a))(?<x>b)")]
    [InlineData("((?J)(?<x>a))(?<x>b)")]
    [InlineData("(?=(?J)(?<x>a))(?<x>b)")]
    [InlineData("(?!(?J)(?<x>a))(?<x>b)")]
    [InlineData("(?<=(?J)(?<x>a))(?<x>b)")]
    [InlineData("(?<!(?J)(?<x>a))(?<x>b)")]
    [InlineData("(?>(?J)(?<x>a))(?<x>b)")]
    [InlineData("(?|(?J)(?<x>a))(?<x>b)")]
    [InlineData("(?(DEFINE)(?J)(?<x>a))(?<x>b)")]
    [InlineData("(?(?=a)(?J)(?<x>a)|b)(?<x>c)")]
    [InlineData("(?(?=(?J)(?<x>a))a|b)(?<x>c)")]
    [InlineData("(?J)(?-J:(?<x>a)(?<x>b))")]
    [InlineData("(?J)(?<x>a)(?-J)(?<x>b)")]
    [InlineData("(?J:(?<x>a)|(?<x>b))(?^:(?<x>c))")]
    public void PermissionCannotEscapeItsGroupOrOverrideAnExplicitDisable(string pattern)
    {
        var error = Assert.Throws<Pcre2CompileException>(() => new Utf8Pcre2Regex(pattern));
        Assert.Equal(Pcre2ErrorKind.InvalidAfterParensQuery, error.ErrorKind);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ScalarSlotsReferencesIterationAndRetainedResultsKeepTheirAuthority(bool controlled)
    {
        using var cancellation = new CancellationTokenSource();
        var regex = new Utf8Pcre2Regex(@"(?J:(?<x>😀)|(?<x>é))\k<x>",
            Pcre2CompileOptions.None, default, controlled ?
                new() { MatchLimit = 100000, DepthLimit = 256, HeapLimitInBytes = 1 << 20,
                    ResultLimitInBytes = 1 << 20 } : default, Timeout.InfiniteTimeSpan);
        var input = "q😀😀 éé"u8.ToArray();
        var retained = regex.MatchDetailed(input, 0, Pcre2MatchOptions.None, cancellation.Token);
        Assert.Equal(3, retained.CaptureSlotCount);
        Assert.Equal(2, regex.NameEntryCount);
        Assert.Equal((1, 9), (retained.Value.StartOffsetInBytes, retained.Value.EndOffsetInBytes));
        Assert.Equal("😀", retained.GetGroup(1).GetValueString());
        Assert.False(retained.GetGroup(2).Success);
        Span<int> numbers = stackalloc int[2];
        Assert.Equal(2, regex.CopyNumbersForName("x", numbers, out var isMore));
        Assert.False(isMore);
        Assert.Equal(new[] { 1, 2 }, numbers.ToArray());
        Assert.Equal(2, regex.Count(input, 0, Pcre2MatchOptions.None, cancellation.Token));
        Assert.Equal("q<😀> <é>", regex.ReplaceToString(input, "<$x>"));
        var cursor = regex.EnumerateMatches(input, 0, Pcre2MatchOptions.None, cancellation.Token);
        Assert.True(cursor.MoveNext());
        Assert.Equal((1, 9), (cursor.Current.StartOffsetInBytes, cursor.Current.EndOffsetInBytes));
        Assert.True(cursor.MoveNext());
        Assert.Equal((10, 14), (cursor.Current.StartOffsetInBytes, cursor.Current.EndOffsetInBytes));
        Assert.False(cursor.MoveNext());
        cancellation.Cancel();
        var cancelled = Assert.Throws<OperationCanceledException>(() =>
            regex.Count(input, 0, Pcre2MatchOptions.None, cancellation.Token));
        Assert.Equal(cancellation.Token, cancelled.CancellationToken);
        Assert.Equal("😀", retained.GetGroup(1).GetValueString());
    }

    [Theory]
    [InlineData(0, Pcre2ErrorKind.MatchLimit)]
    [InlineData(1, Pcre2ErrorKind.DepthLimit)]
    [InlineData(2, Pcre2ErrorKind.HeapLimit)]
    [InlineData(3, Pcre2ErrorKind.ResultLimit)]
    public void ExistingFiniteControlsStillDenyTheOwnedRoute(int limit, Pcre2ErrorKind expected)
    {
        var regex = new Utf8Pcre2Regex(@"(?J:(?<x>😀)(?:aa|bb)|(?<x>é))\k<x>",
            Pcre2CompileOptions.None, default, new() {
                MatchLimit = limit == 0 ? 1U : 100000U,
                DepthLimit = limit == 1 ? 1U : 256U,
                HeapLimitInBytes = limit == 2 ? 1UL : 1UL << 20,
                ResultLimitInBytes = limit == 3 ? 1UL : 1UL << 20
            }, Timeout.InfiniteTimeSpan);
        var error = Assert.Throws<Pcre2MatchException>(() => regex.MatchDetailed("😀aa😀"u8));
        Assert.Equal(expected, error.ErrorKind);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CursorAliasesRetainTheOriginalDeadlineAndToken(bool cancel)
    {
        long ticks = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
        using var cancellation = new CancellationTokenSource();
        var regex = new Utf8Pcre2Regex(@"(?J:(?<x>😀)|(?<x>é))\k<x>",
            Pcre2CompileOptions.None, default,
            new() { MatchLimit = 100000, DepthLimit = 256, HeapLimitInBytes = 1 << 20,
                ResultLimitInBytes = 1 << 20 }, TimeSpan.FromMilliseconds(20));
        var cursor = regex.EnumerateMatches("😀😀éé"u8, 0, Pcre2MatchOptions.None, cancellation.Token);
        Assert.True(cursor.MoveNext());
        var retained = cursor.Current;
        var alias = cursor;
        if (cancel) cancellation.Cancel(); else ticks = 25;
        Exception? failure = null;
        try { _ = alias.MoveNext(); } catch (Exception error) { failure = error; }
        if (cancel)
            Assert.Equal(cancellation.Token, Assert.IsType<OperationCanceledException>(failure).CancellationToken);
        else
            Assert.Equal(Pcre2ErrorKind.Timeout, Assert.IsType<Pcre2MatchException>(failure).ErrorKind);
        Assert.Equal("😀😀", retained.GetValueString());
        Assert.True(regex.MatchDetailed("éé"u8).Success);
    }
}
