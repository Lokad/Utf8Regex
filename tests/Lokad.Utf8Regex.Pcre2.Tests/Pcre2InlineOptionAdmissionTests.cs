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
        var input = Encoding.UTF8.GetBytes(text);
        Assert.Equal(expected, regex.IsMatch(input, 0, Pcre2MatchOptions.None, cancellation.Token));
        Assert.Equal(expected, regex.Match(input, cancellationToken: cancellation.Token).Success);
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
        ReadOnlySpan<byte> retained2Input = "é😀"u8;
        var retained2Captures = new Utf8Pcre2CaptureRange[regex.CaptureGroupCount + 1];
        var retained = regex.Match(retained2Input, captures: retained2Captures);
        Assert.Equal((2, 6), (retained.StartOffsetInBytes, retained.EndOffsetInBytes));
        Assert.Equal("😀", System.Text.Encoding.UTF8.GetString((retained2Captures[1].Success ? retained2Input.Slice(retained2Captures[1].StartOffsetInBytes, retained2Captures[1].EndOffsetInBytes - retained2Captures[1].StartOffsetInBytes) : ReadOnlySpan<byte>.Empty)));
        Assert.Equal("é<😀>", System.Text.Encoding.UTF8.GetString(regex.Replace("é😀"u8, "<$x>"u8)));
        using var cancellation = new CancellationTokenSource();
        var empty = new Utf8Pcre2Regex(directive, options: Pcre2CompileOptions.None, compileSettings: default, policy: new Utf8RegexPolicy
        {
            ResourcePolicy = new Utf8ResourcePolicy
            {
                MaxDepth = 256,
                MaxWorkingBytes = 1 << 20,
                MaxResultBytes = 1 << 20
            },
            MatchTimeout = Timeout.InfiniteTimeSpan
        });
        Assert.Equal(2, empty.Count("😀"u8, 0, Pcre2MatchOptions.None, cancellation.Token));
        var cursor = empty.EnumerateMatches("😀"u8, 0, Pcre2MatchOptions.None, cancellation.Token);
        Assert.True(cursor.MoveNext());
        Assert.Equal(0, cursor.Current.StartOffsetInBytes);
        Assert.True(cursor.MoveNext());
        Assert.Equal(4, cursor.Current.StartOffsetInBytes);
        Assert.False(cursor.MoveNext());
        Assert.Equal("😀", System.Text.Encoding.UTF8.GetString((retained2Captures[1].Success ? retained2Input.Slice(retained2Captures[1].StartOffsetInBytes, retained2Captures[1].EndOffsetInBytes - retained2Captures[1].StartOffsetInBytes) : ReadOnlySpan<byte>.Empty)));
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
        var input = Encoding.UTF8.GetBytes("q" + text);
        Assert.True(regex.IsMatch(input, 0, Pcre2MatchOptions.None, cancellation.Token));
        ReadOnlyMemory<byte> retained3Input = input;
        using var retained3Cursor = regex.CreateMatchCursor(retained3Input, 0, Pcre2MatchOptions.None, cancellation.Token);
        retained3Cursor.MoveNext(out var retained3Result);
        retained3Cursor.Dispose();
        var retained = retained3Result;
        Assert.True(retained.Success);
        Assert.Equal(3, retained.Groups.Length);
        Assert.Equal((1, input.Length), (retained.Value.StartOffsetInBytes, retained.Value.EndOffsetInBytes));
        Assert.Equal(first, System.Text.Encoding.UTF8.GetString((retained.Groups[1].Success ? retained3Input.Span.Slice(retained.Groups[1].StartOffsetInBytes, retained.Groups[1].EndOffsetInBytes - retained.Groups[1].StartOffsetInBytes) : ReadOnlySpan<byte>.Empty)));
        Assert.Equal(second, System.Text.Encoding.UTF8.GetString((retained.Groups[2].Success ? retained3Input.Span.Slice(retained.Groups[2].StartOffsetInBytes, retained.Groups[2].EndOffsetInBytes - retained.Groups[2].StartOffsetInBytes) : ReadOnlySpan<byte>.Empty)));
        Assert.True(retained.TryGetGroup(1, out var named)); Assert.True(named.Success);
        Assert.Equal(first, System.Text.Encoding.UTF8.GetString((named.Success ? retained3Input.Span.Slice(named.StartOffsetInBytes, named.EndOffsetInBytes - named.StartOffsetInBytes) : ReadOnlySpan<byte>.Empty)));
        Assert.Equal("q" + first + "/" + second, System.Text.Encoding.UTF8.GetString(regex.Replace(input, "$1/$2"u8)));
        Assert.Equal(1, regex.Count(input, 0, Pcre2MatchOptions.None, cancellation.Token));
        if (pattern.Contains(@"\1", StringComparison.Ordinal))
        {
            var wrongReference = Encoding.UTF8.GetBytes(first + second + second);
            Assert.False(regex.IsMatch(wrongReference));
        }
        cancellation.Cancel();
        Assert.Throws<OperationCanceledException>(() =>
            regex.Count(input, 0, Pcre2MatchOptions.None, cancellation.Token));
        Assert.Equal(first, System.Text.Encoding.UTF8.GetString((retained.Groups[1].Success ? retained3Input.Span.Slice(retained.Groups[1].StartOffsetInBytes, retained.Groups[1].EndOffsetInBytes - retained.Groups[1].StartOffsetInBytes) : ReadOnlySpan<byte>.Empty)));
    }

    [Theory]
    [InlineData("(?n)(?-)(?<n>a)(b)")]
    [InlineData("(?n)(?-:(?<n>a)(b))")]
    public void EmptyHyphenPreservesInheritedNoAutoCapture(string pattern)
    {
        var match4Regex = new Utf8Pcre2Regex(pattern);
        ReadOnlySpan<byte> match4Input = "ab"u8;
        var match4Captures = new Utf8Pcre2CaptureRange[match4Regex.CaptureGroupCount + 1];
        var match = match4Regex.Match(match4Input, captures: match4Captures);
        Assert.True(match.Success);
        Assert.Equal(2, (match.Success ? match4Captures.Length : 0));
        Assert.Equal("a", System.Text.Encoding.UTF8.GetString((match4Captures[1].Success ? match4Input.Slice(match4Captures[1].StartOffsetInBytes, match4Captures[1].EndOffsetInBytes - match4Captures[1].StartOffsetInBytes) : ReadOnlySpan<byte>.Empty)));
    }
}
