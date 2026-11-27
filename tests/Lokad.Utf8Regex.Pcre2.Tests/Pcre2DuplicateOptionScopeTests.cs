using System.Text;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Pcre2;

namespace Lokad.Utf8Regex.Pcre2.Tests;

[Collection(Pcre2AllocationTestCollection.Name)]
public sealed class Pcre2DuplicateOptionScopeTests
{
    [Theory]
    [InlineData(@"\k<x>\z", 8)]
    [InlineData(@"(?(x)é|x)\z", 10)]
    public void DuplicateCaptureReadersChargeEachAdditionalBlock(string suffix, int firstBlockWork)
    {
        using var live = new CancellationTokenSource();
        using var firstBlock = Create(63, firstBlockWork);
        using var denied = Create(64, firstBlockWork);
        using var twoBlocks = Create(64, firstBlockWork + 1);

        Assert.True(firstBlock.IsMatch("éé"u8, cancellationToken: live.Token));
        Assert.True(twoBlocks.IsMatch("éé"u8, cancellationToken: live.Token));

        Utf8Pcre2Regex Create(int unused, int work)
        {
            var pattern = @"(?J)(?(DEFINE)" + string.Concat(Enumerable.Repeat("(?<x>a)", unused)) + @")\A(?<x>é)" + suffix;
            return new Utf8Pcre2Regex(pattern, policy: new Utf8RegexPolicy { ResourcePolicy = new() { MaxDepth = 4096 } });
        }
    }

    [Fact]
    public void DuplicateBackreferenceKeepsTheFirstParticipatingCapture()
    {
        using var regex = new Utf8Pcre2Regex(@"(?J)\A(?<x>é)(?<x>😀)\k<x>\z");
        Assert.True(regex.IsMatch("é😀é"u8));
        Assert.False(regex.IsMatch("é😀😀"u8));
    }

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
        var regex = new Utf8Pcre2Regex(pattern, options: Pcre2CompileOptions.None, compileSettings: default, policy: new Utf8RegexPolicy
        {
            ResourcePolicy = new Utf8ResourcePolicy
            {
                MaxDepth = 256,
                MaxWorkingBytes = 1 << 20,
                MaxResultBytes = 1 << 20
            },
            MatchTimeout = Timeout.InfiniteTimeSpan
        });
        var bytes = Encoding.UTF8.GetBytes(text);
        Assert.True(regex.IsMatch(bytes, 0, Pcre2MatchOptions.None, cancellation.Token));
        ReadOnlyMemory<byte> match1Input = bytes;
        using var match1Cursor = regex.CreateMatchCursor(match1Input, 0, Pcre2MatchOptions.None, cancellation.Token);
        match1Cursor.MoveNext(out var match1Result);
        match1Cursor.Dispose();
        var match = match1Result;
        Assert.Equal(expected, System.Text.Encoding.UTF8.GetString((match.Value.Success ? match1Input.Span.Slice(match.Value.StartOffsetInBytes, match.Value.EndOffsetInBytes - match.Value.StartOffsetInBytes) : ReadOnlySpan<byte>.Empty)));
        Span<int> numbers = stackalloc int[3];
        var count = regex.CopyNumbersForUtf8Name("x"u8, numbers, out var more, cancellation.Token);
        Assert.False(more);
        var selected = 0;
        foreach (var number in numbers[..count])
        {
            Assert.True(match.TryGetGroup(number, out var capture));
            if (!capture.Success) continue;
            selected = capture.Number;
            break;
        }
        Assert.NotEqual(0, selected);
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
        var regex = new Utf8Pcre2Regex(@"(?J:(?<x>😀)|(?<x>é))\k<x>", options: Pcre2CompileOptions.None, compileSettings: default, policy: new Utf8RegexPolicy
        {
            ResourcePolicy = controlled ? new()
            {
                MaxDepth = 256,
                MaxWorkingBytes = 1 << 20,
                MaxResultBytes = 1 << 20,
            } : Utf8ResourcePolicy.Unlimited,
            MatchTimeout = Timeout.InfiniteTimeSpan
        });
        var input = "q😀😀 éé"u8.ToArray();
        ReadOnlySpan<byte> retained2Input = input;
        var retained2Captures = new Utf8Pcre2CaptureRange[regex.CaptureGroupCount + 1];
        var retained = regex.Match(retained2Input, captures: retained2Captures, cancellationToken: cancellation.Token);
        Assert.Equal(3, (retained.Success ? retained2Captures.Length : 0));
        Assert.Equal(2, regex.NameEntryCount);
        Assert.Equal((1, 9), (retained.StartOffsetInBytes, retained.EndOffsetInBytes));
        Assert.Equal("😀", System.Text.Encoding.UTF8.GetString((retained2Captures[1].Success ? retained2Input.Slice(retained2Captures[1].StartOffsetInBytes, retained2Captures[1].EndOffsetInBytes - retained2Captures[1].StartOffsetInBytes) : ReadOnlySpan<byte>.Empty)));
        Assert.False(retained2Captures[2].Success);
        Span<int> numbers = stackalloc int[2];
        Assert.Equal(2, regex.CopyNumbersForUtf8Name("x"u8, numbers, out var isMore));
        Assert.False(isMore);
        Assert.Equal(new[] { 1, 2 }, numbers.ToArray());
        Assert.Equal(2, regex.Count(input, 0, Pcre2MatchOptions.None, cancellation.Token));
        Assert.Equal("q<😀> <é>", System.Text.Encoding.UTF8.GetString(regex.Replace(input, "<$x>"u8)));
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
        Assert.Equal("😀", System.Text.Encoding.UTF8.GetString((retained2Captures[1].Success ? retained2Input.Slice(retained2Captures[1].StartOffsetInBytes, retained2Captures[1].EndOffsetInBytes - retained2Captures[1].StartOffsetInBytes) : ReadOnlySpan<byte>.Empty)));
    }

    [Theory]
    [InlineData(1, Pcre2ErrorKind.DepthLimit)]
    [InlineData(2, Pcre2ErrorKind.HeapLimit)]
    [InlineData(3, Pcre2ErrorKind.ResultLimit)]
    public void ExistingFiniteControlsStillDenyTheOwnedRoute(int limit, Pcre2ErrorKind expected)
    {
        var regex = new Utf8Pcre2Regex(@"(?J:(?<x>😀)(?:aa|bb)|(?<x>é))\k<x>", options: Pcre2CompileOptions.None, compileSettings: default, policy: new Utf8RegexPolicy
        {
            ResourcePolicy = new Utf8ResourcePolicy
            {
                MaxDepth = (limit == 1 ? 1U : 256U) == 0 ? null : (limit == 1 ? 1U : 256U),
                MaxWorkingBytes = (limit == 2 ? 1UL : 1UL << 20) == 0 ? null : (limit == 2 ? 1UL : 1UL << 20),
                MaxResultBytes = (limit == 3 ? 1UL : 1UL << 20) == 0 ? null : (limit == 3 ? 1UL : 1UL << 20)
            },
            MatchTimeout = Timeout.InfiniteTimeSpan
        });
        var error = Assert.Throws<Pcre2MatchException>(() =>
        {
            ReadOnlyMemory<byte> result3Input = ("😀aa😀"u8).ToArray();
            using var result3Cursor = regex.CreateMatchCursor(result3Input);
            result3Cursor.MoveNext(out var result3Result);
            result3Cursor.Dispose();
            _ = result3Result;
        });
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
        var regex = new Utf8Pcre2Regex(@"(?J:(?<x>😀)|(?<x>é))\k<x>", options: Pcre2CompileOptions.None, compileSettings: default, policy: new Utf8RegexPolicy
        {
            ResourcePolicy = new Utf8ResourcePolicy
            {
                MaxDepth = 256,
                MaxWorkingBytes = 1 << 20,
                MaxResultBytes = 1 << 20
            },
            MatchTimeout = TimeSpan.FromMilliseconds(20)
        });
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
        Assert.Equal("😀😀", System.Text.Encoding.UTF8.GetString(retained.GetValueBytes()));
        Assert.True(regex.Match("éé"u8).Success);
    }
}
