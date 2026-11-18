using System.Text;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Pcre2.Tests;

public sealed class Pcre2CommentSemanticsTests
{
    [Theory]
    [InlineData(@"\w", "é", Pcre2CompileOptions.None, false)]
    [InlineData(@"\d", "٣", Pcre2CompileOptions.None, false)]
    [InlineData(@"\s", "\u00a0", Pcre2CompileOptions.None, false)]
    [InlineData(@"\w", "é", Pcre2CompileOptions.Ucp, true)]
    public void CommentDoesNotChangeCharacterSemantics(string body, string text, Pcre2CompileOptions options, bool expected)
    {
        var input = Encoding.UTF8.GetBytes(text);
        Assert.Equal(expected, new Utf8Pcre2Regex(body, options: options).IsMatch(input));
        Assert.Equal(expected, new Utf8Pcre2Regex("(?#ignored)" + body, options: options).IsMatch(input));
    }

    [Fact]
    public void CommentDoesNotChangeDeclarationOrderCapturesAndReplacement()
    {
        const string body = "(?<first>é)(😀)";
        foreach (var pattern in new[] { body, "(?#ignored)" + body })
        {
            var regex = new Utf8Pcre2Regex(pattern);
            ReadOnlySpan<byte> match1Input = "é😀"u8;
            var match1Captures = new Utf8Pcre2CaptureRange[regex.CaptureGroupCount + 1];
            var match = regex.Match(match1Input, captures: match1Captures);
            Assert.True(match.Success);
            Assert.Equal("é", System.Text.Encoding.UTF8.GetString((match1Captures[1].Success ? match1Input.Slice(match1Captures[1].StartOffsetInBytes, match1Captures[1].EndOffsetInBytes - match1Captures[1].StartOffsetInBytes) : ReadOnlySpan<byte>.Empty)));
            Assert.Equal("😀", System.Text.Encoding.UTF8.GetString((match1Captures[2].Success ? match1Input.Slice(match1Captures[2].StartOffsetInBytes, match1Captures[2].EndOffsetInBytes - match1Captures[2].StartOffsetInBytes) : ReadOnlySpan<byte>.Empty)));
            Assert.Equal("é😀", System.Text.Encoding.UTF8.GetString(regex.Replace("é😀"u8, "$1$2"u8)));
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CommentDoesNotBypassOutputHeap(bool text)
    {
        var regex = new Utf8Pcre2Regex("(?#ignored)(é)(😀)", options: Pcre2CompileOptions.None, compileSettings: default, policy: new Utf8RegexPolicy
        {
            ResourcePolicy = new Utf8ResourcePolicy
            {
                MaxWorkingBytes = 4096
            },
            MatchTimeout = Timeout.InfiniteTimeSpan
        });
        var input = new byte[1 << 20]; input.AsSpan().Fill((byte)'z');
        var failure = Assert.Throws<Pcre2MatchException>(() =>
        {
            if (text) _ = System.Text.Encoding.UTF8.GetString(regex.Replace(input, "$2$1"u8, 0, Pcre2SubstitutionOptions.None, Pcre2MatchOptions.Anchored));
            else _ = regex.Replace(input, "$2$1"u8, 0, Pcre2SubstitutionOptions.None, Pcre2MatchOptions.Anchored);
        });
        Assert.Equal(Pcre2ErrorKind.HeapLimit, failure.ErrorKind);
    }

    [Fact]
    public void CommentDoesNotBypassSingleMatchWork()
    {
        var regex = new Utf8Pcre2Regex("(?#ignored)(é)(😀)", options: Pcre2CompileOptions.None, compileSettings: default, policy: new Utf8RegexPolicy
        {
            ResourcePolicy = new Utf8ResourcePolicy
            {
                MaxWorkSteps = 1
            },
            MatchTimeout = Timeout.InfiniteTimeSpan
        });
        Assert.Equal(Pcre2ErrorKind.MatchLimit, Assert.Throws<Pcre2MatchException>(() =>
        {
            ReadOnlyMemory<byte> result2Input = ("é😀"u8).ToArray();
            using var result2Cursor = regex.CreateMatchCursor(result2Input);
            result2Cursor.MoveNext(out var result2Result);
            result2Cursor.Dispose();
            _ = result2Result;
        }).ErrorKind);
    }

    [Theory]
    [InlineData("(?#one)é(?#two)😀(?#tail)", "xé😀", "é😀")]
    [InlineData(@"(?#one)\A(?#two)é😀\z(?#tail)", "é😀", "é😀")]
    [InlineData(@"(?#one)\Gé😀(?#tail)", "é😀x", "é😀")]
    [InlineData("a(?#between)+?b", "aaab", "aaab")]
    [InlineData("a+(?#modifier)?", "aaa", "a")]
    [InlineData("a+(?#modifier)+a", "aaa", null)]
    [InlineData("a(?#between){2}?", "aaa", "aa")]
    [InlineData("(?#empty)", "😀", "")]
    [InlineData(@"\Q(?#literal)\E", "x(?#literal)y", "(?#literal)")]
    [InlineData("[(?#)]", "?", "?")]
    [InlineData(@"\(\?\#literal\)", "(?#literal)", "(?#literal)")]
    [InlineData("(?#fake (?C)ab", "xxab", "ab")]
    public void CommentsPreserveLexicalContextsAndQuantifierAssociation(string pattern, string subject, string? expected)
    {
        var regex = new Utf8Pcre2Regex(pattern);
        var match = regex.Match(Encoding.UTF8.GetBytes(subject));
        Assert.Equal(expected is not null, match.Success);
        if (expected is not null) Assert.Equal(expected, System.Text.Encoding.UTF8.GetString(match.GetValueBytes()));
    }

    [Theory]
    [InlineData(@"\A(?#ignored)+", Pcre2ErrorKind.QuantifierInvalid)]
    [InlineData("(?#ignored)*", Pcre2ErrorKind.QuantifierInvalid)]
    [InlineData("(?#missing", Pcre2ErrorKind.MissingCommentClosing)]
    [InlineData("a(?#missing", Pcre2ErrorKind.MissingCommentClosing)]
    public void CommentsCannotHideMalformedOrUnrepeatableItems(string pattern, Pcre2ErrorKind expected)
        => Assert.Equal(expected, Assert.Throws<Pcre2CompileException>(() => new Utf8Pcre2Regex(pattern)).ErrorKind);

    [Fact]
    public void EmptyCommentPatternAdvancesOverCompleteScalars()
    {
        var regex = new Utf8Pcre2Regex("(?#empty)");
        var cursor = regex.EnumerateMatches("é😀"u8);
        var starts = new List<int>();
        while (cursor.MoveNext()) starts.Add(cursor.Current.StartOffsetInBytes);
        Assert.Equal(new[] { 0, 2, 6 }, starts);
    }

    [Fact]
    public void EmptyCommentDelimitsNumericReferencesWithoutAddingSlots()
    {
        var regex = new Utf8Pcre2Regex(@"(a)\1(?#)2");
        ReadOnlySpan<byte> match3Input = "aa2"u8;
        var match3Captures = new Utf8Pcre2CaptureRange[regex.CaptureGroupCount + 1];
        var match = regex.Match(match3Input, captures: match3Captures);
        Assert.True(match.Success); Assert.Equal(2, (match.Success ? match3Captures.Length : 0));
        Assert.Equal("a", System.Text.Encoding.UTF8.GetString((match3Captures[1].Success ? match3Input.Slice(match3Captures[1].StartOffsetInBytes, match3Captures[1].EndOffsetInBytes - match3Captures[1].StartOffsetInBytes) : ReadOnlySpan<byte>.Empty)));
    }

    [Fact]
    public void CommentedLiteralKeepsNativeDeadlineAuthority()
    {
        var armed = false; var reads = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => !armed ? 0 : ++reads >= 2 ? 2 : 0);
        var regex = new Utf8Pcre2Regex("(?#ignored)abc", options: Pcre2CompileOptions.None, compileSettings: default, policy: new Utf8RegexPolicy
        {
            MatchTimeout = TimeSpan.FromMilliseconds(1)
        });
        armed = true;
        Assert.Equal(Pcre2ErrorKind.Timeout, Assert.Throws<Pcre2MatchException>(() => regex.IsMatch("xxabc"u8)).ErrorKind);
    }
}
