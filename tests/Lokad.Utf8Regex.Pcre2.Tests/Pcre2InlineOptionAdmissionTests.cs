using System.Text;
using Lokad.Utf8Regex.Pcre2;

namespace Lokad.Utf8Regex.Pcre2.Tests;

public sealed class Pcre2InlineOptionAdmissionTests
{
    [Theory]
    [InlineData("(?J--J)(?<x>a)")]
    [InlineData("(?J--J:(?<x>a))")]
    [InlineData("(?i--m:(a))")]
    [InlineData("(?i-m-s)(a)")]
    [InlineData("(?i-m-s:(a))")]
    public void ASecondHyphenIsACompileError(string pattern)
    {
        var error = Assert.Throws<Pcre2CompileException>(() => new Utf8Pcre2Regex(pattern));
        Assert.Equal(Pcre2ErrorKind.InvalidAfterParensQuery, error.ErrorKind);
    }

    [Theory]
    [InlineData("(?i-i)(a)", "a", true)]
    [InlineData("(?i-i)(a)", "A", false)]
    [InlineData("(?i-i:(a))", "a", true)]
    [InlineData("(?i-i:(a))", "A", false)]
    [InlineData("(?J-J:(?<x>a))", "a", true)]
    [InlineData("(?-J:(?<x>a))", "a", true)]
    [InlineData("(?-:(a))", "a", true)]
    [InlineData("(?-)(a)", "a", true)]
    [InlineData("(?i-:(a))", "A", true)]
    [InlineData("(?i-)(a)", "A", true)]
    [InlineData("(?i)(?-:(a))", "A", true)]
    [InlineData("(?i)(?-)(a)", "A", true)]
    [InlineData("(?)(a)", "a", true)]
    [InlineData("(?)(a)", "A", false)]
    public void ValidEmptyAndSingleHyphenDirectivesKeepTheirSemantics(string pattern, string text, bool expected)
    {
        using var cancellation = new CancellationTokenSource();
        var regex = new Utf8Pcre2Regex(pattern, Pcre2CompileOptions.None, default,
            new() { MatchLimit = 100000, DepthLimit = 256, HeapLimitInBytes = 1 << 20,
                ResultLimitInBytes = 1 << 20 }, Timeout.InfiniteTimeSpan);
        var input = Encoding.UTF8.GetBytes(text);
        Assert.Equal(expected, regex.IsMatch(input, 0, Pcre2MatchOptions.None, cancellation.Token));
        Assert.Equal(expected, regex.MatchDetailed(input, 0, Pcre2MatchOptions.None, cancellation.Token).Success);
    }

    [Theory]
    [InlineData(@"\Q(?J--J)\E", "(?J--J)")]
    [InlineData("[(?J)-]+", "(?J--J)")]
    [InlineData("(?#(?J--J)a", "a")]
    [InlineData("(?x)# (?J--J)\na", "a")]
    [InlineData("(*MARK:(?J--J)a", "a")]
    public void LiteralAndIgnoredTextDoesNotBecomeAnOptionDirective(string pattern, string text)
    {
        Assert.True(new Utf8Pcre2Regex(pattern).IsMatch(Encoding.UTF8.GetBytes(text)));
    }

    [Theory]
    [InlineData("(?)")]
    [InlineData("(?-)")]
    [InlineData("(?-:)")]
    public void EmptyDirectiveKeepsScalarCapturesAndEmptyAdvancement(string directive)
    {
        var regex = new Utf8Pcre2Regex(directive + "(?<x>😀)");
        var retained = regex.MatchDetailed("é😀"u8);
        Assert.Equal((2, 6), (retained.Value.StartOffsetInBytes, retained.Value.EndOffsetInBytes));
        Assert.Equal("😀", retained.GetGroup(1).GetValueString());
        Assert.Equal("é<😀>", regex.ReplaceToString("é😀"u8, "<$x>"));
        using var cancellation = new CancellationTokenSource();
        var empty = new Utf8Pcre2Regex(directive, Pcre2CompileOptions.None, default,
            new() { MatchLimit = 100000, DepthLimit = 256, HeapLimitInBytes = 1 << 20,
                ResultLimitInBytes = 1 << 20 }, Timeout.InfiniteTimeSpan);
        Assert.Equal(2, empty.Count("😀"u8, 0, Pcre2MatchOptions.None, cancellation.Token));
        var cursor = empty.EnumerateMatches("😀"u8, 0, Pcre2MatchOptions.None, cancellation.Token);
        Assert.True(cursor.MoveNext());
        Assert.Equal(0, cursor.Current.StartOffsetInBytes);
        Assert.True(cursor.MoveNext());
        Assert.Equal(4, cursor.Current.StartOffsetInBytes);
        Assert.False(cursor.MoveNext());
        Assert.Equal("😀", retained.GetGroup(1).GetValueString());
    }

    [Theory]
    [InlineData(@"(?-)(?<n>a)(b)", "ab", "a", "b")]
    [InlineData(@"(?-:(?<n>a)(b))", "ab", "a", "b")]
    [InlineData(@"(?-)(?<n>a)(b)\1", "aba", "a", "b")]
    [InlineData(@"(?-:(?<n>a)(b))\1", "aba", "a", "b")]
    [InlineData(@"(?-:(?<n>😀)(é))\1", "😀é😀", "😀", "é")]
    [InlineData(@"(?J)(?-)(?<n>a)(b)", "ab", "a", "b")]
    [InlineData(@"(?i-)(?<n>a)(b)", "AB", "A", "B")]
    [InlineData(@"(?)(?<n>a)(b)", "ab", "a", "b")]
    [InlineData(@"(?-i)(?<n>a)(b)", "ab", "a", "b")]
    [InlineData(@"(?:(?<n>a)(b))", "ab", "a", "b")]
    public void EmptyHyphenDoesNotChangeProfileCaptureOrder(
        string pattern, string text, string first, string second)
    {
        using var cancellation = new CancellationTokenSource();
        var regex = new Utf8Pcre2Regex(pattern, Pcre2CompileOptions.None, default,
            new() { MatchLimit = 100000, DepthLimit = 256, HeapLimitInBytes = 1 << 20,
                ResultLimitInBytes = 1 << 20 }, Timeout.InfiniteTimeSpan);
        var input = Encoding.UTF8.GetBytes("q" + text);
        Assert.True(regex.IsMatch(input, 0, Pcre2MatchOptions.None, cancellation.Token));
        var retained = regex.MatchDetailed(input, 0, Pcre2MatchOptions.None, cancellation.Token);
        Assert.True(retained.Success);
        Assert.Equal(3, retained.CaptureSlotCount);
        Assert.Equal((1, input.Length), (retained.Value.StartOffsetInBytes, retained.Value.EndOffsetInBytes));
        Assert.Equal(first, retained.GetGroup(1).GetValueString());
        Assert.Equal(second, retained.GetGroup(2).GetValueString());
        Assert.True(retained.TryGetFirstSetGroup("n", out var named));
        Assert.Equal(first, named.GetValueString());
        Assert.Equal("q" + first + "/" + second, regex.ReplaceToString(input, "$1/$2"));
        Assert.Equal(1, regex.Count(input, 0, Pcre2MatchOptions.None, cancellation.Token));
        if (pattern.Contains(@"\1", StringComparison.Ordinal))
        {
            var wrongReference = Encoding.UTF8.GetBytes(first + second + second);
            Assert.False(regex.IsMatch(wrongReference));
        }
        cancellation.Cancel();
        Assert.Throws<OperationCanceledException>(() =>
            regex.Count(input, 0, Pcre2MatchOptions.None, cancellation.Token));
        Assert.Equal(first, retained.GetGroup(1).GetValueString());
    }

    [Theory]
    [InlineData("(?n)(?-)(?<n>a)(b)")]
    [InlineData("(?n)(?-:(?<n>a)(b))")]
    public void EmptyHyphenPreservesInheritedNoAutoCapture(string pattern)
    {
        var match = new Utf8Pcre2Regex(pattern).MatchDetailed("ab"u8);
        Assert.True(match.Success);
        Assert.Equal(2, match.CaptureSlotCount);
        Assert.Equal("a", match.GetGroup(1).GetValueString());
    }
}
