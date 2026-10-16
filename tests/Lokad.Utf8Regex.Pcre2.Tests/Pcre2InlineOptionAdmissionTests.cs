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

    [Fact]
    public void EmptyDirectiveKeepsScalarCapturesAndEmptyAdvancement()
    {
        var regex = new Utf8Pcre2Regex("(?)(?<x>😀)");
        var retained = regex.MatchDetailed("é😀"u8);
        Assert.Equal((2, 6), (retained.Value.StartOffsetInBytes, retained.Value.EndOffsetInBytes));
        Assert.Equal("😀", retained.GetGroup(1).GetValueString());
        Assert.Equal("é<😀>", regex.ReplaceToString("é😀"u8, "<$x>"));
        using var cancellation = new CancellationTokenSource();
        var empty = new Utf8Pcre2Regex("(?)", Pcre2CompileOptions.None, default,
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
}
