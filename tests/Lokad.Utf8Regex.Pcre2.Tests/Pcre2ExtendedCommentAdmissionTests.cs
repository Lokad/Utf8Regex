using System.Text;

namespace Lokad.Utf8Regex.Pcre2.Tests;

public sealed class Pcre2ExtendedCommentAdmissionTests
{
    public static IEnumerable<object[]> CommentedSyntax()
    {
        foreach (var text in new[] { @"\C", @"(?=\K)", "(?C)", "(?X)(?z)",
            @"\idef", "{5,4}{65536}", "[(" })
        {
            yield return ["# " + text + "\n(😀)", Pcre2CompileOptions.Extended, "é😀z", 6];
            yield return ["(?x)# " + text + "\n(😀)", Pcre2CompileOptions.None, "é😀z", 6];
            yield return ["(?x:# " + text + "\n(😀))b", Pcre2CompileOptions.None, "é😀bz", 7];
        }
    }

    [Theory]
    [MemberData(nameof(CommentedSyntax))]
    public void IgnoredSyntaxPreservesScalarCapturesAndFiniteInvocation(string pattern,
        Pcre2CompileOptions options, string text, int end)
    {
        using var cancellation = new CancellationTokenSource();
        var regex = new Utf8Pcre2Regex(Encoding.UTF8.GetBytes(pattern), options: options, compileSettings: default, policy: new Utf8RegexPolicy
        {
            ResourcePolicy = new Utf8ResourcePolicy
            {
                MaxWorkSteps = 100000,
                MaxDepth = 256,
                MaxWorkingBytes = 1 << 20,
                MaxResultBytes = 1 << 20
            },
            MatchTimeout = Timeout.InfiniteTimeSpan
        });
        var bytes = Encoding.UTF8.GetBytes(text);
        Assert.Equal(1, regex.Count(bytes, 0, Pcre2MatchOptions.None, cancellation.Token));
        ReadOnlySpan<byte> match1Input = bytes;
        var match1Captures = new Utf8Pcre2CaptureRange[regex.CaptureGroupCount + 1];
        var match = regex.Match(match1Input, captures: match1Captures, cancellationToken: cancellation.Token);
        Assert.True(match.Success);
        Assert.Equal((2, end), (match.StartOffsetInBytes, match.EndOffsetInBytes));
        Assert.Equal(2, (match.Success ? match1Captures.Length : 0));
        Assert.Equal("😀", System.Text.Encoding.UTF8.GetString((match1Captures[1].Success ? match1Input.Slice(match1Captures[1].StartOffsetInBytes, match1Captures[1].EndOffsetInBytes - match1Captures[1].StartOffsetInBytes) : ReadOnlySpan<byte>.Empty)));
        Assert.Equal((2, 6), (match1Captures[1].StartOffsetInBytes, match1Captures[1].EndOffsetInBytes));
    }

    [Theory]
    [InlineData("(?x:a# [(?C)\n b)#c", "ab#c", Pcre2CompileOptions.None)]
    [InlineData("(?x)(?-x:a#b)# [(?C)\nc", "a#bc", Pcre2CompileOptions.None)]
    [InlineData("(?x:(?-x:a#b) # [(?C)\n c)", "a#bc", Pcre2CompileOptions.None)]
    [InlineData("(?xx:[ # ] # [(?C)\n a)", "#a", Pcre2CompileOptions.None)]
    [InlineData(@"\Q#\C(?C)\E", @"#\C(?C)", Pcre2CompileOptions.Extended)]
    [InlineData("[#]", "#", Pcre2CompileOptions.Extended)]
    [InlineData(@"(*MARK:# \C)a", "a", Pcre2CompileOptions.Extended)]
    public void ModesRestoreAndHashesStayLiteralWhereRequired(string pattern, string text, Pcre2CompileOptions options)
    {
        Assert.True(new Utf8Pcre2Regex(pattern, options: options).IsMatch(Encoding.UTF8.GetBytes(text)));
    }

    [Fact]
    public void ResetDirectiveKeepsLexicalAndExecutionMode()
    {
        const string pattern = "(?^:a#b)";
        Pcre2CompileValidator.ValidatePattern(pattern, Pcre2CompileOptions.Extended, default);
        Assert.True(new Utf8Pcre2Regex(pattern, options: Pcre2CompileOptions.Extended).IsMatch("a#b"u8));
    }

    [Theory]
    [InlineData(@"(?-x:#\C)", Pcre2ErrorKind.BackslashCDisabled)]
    [InlineData("(?x:# \\C\n)(?-x:\\C)", Pcre2ErrorKind.BackslashCDisabled)]
    [InlineData("(?x:# (?C)\n)(?C)", Pcre2ErrorKind.CalloutUnsupported)]
    [InlineData("(?x:# [(\na(?=\\K))", Pcre2ErrorKind.LookaroundBackslashKDisabled)]
    [InlineData(@"[]#](?=\K)", Pcre2ErrorKind.LookaroundBackslashKDisabled)]
    [InlineData(@"[\]#](?=\K)", Pcre2ErrorKind.LookaroundBackslashKDisabled)]
    [InlineData(@"[\Q]\E#](?=\K)", Pcre2ErrorKind.LookaroundBackslashKDisabled)]
    [InlineData(@"[[:alpha:]#](?=\K)", Pcre2ErrorKind.LookaroundBackslashKDisabled)]
    [InlineData(@"[]#](?C)", Pcre2ErrorKind.CalloutUnsupported)]
    [InlineData(@"(?xx)[ ]#](?=\K)", Pcre2ErrorKind.LookaroundBackslashKDisabled)]
    [InlineData(@"(?xx:[ ]#])(?=\K)", Pcre2ErrorKind.LookaroundBackslashKDisabled)]
    [InlineData(@"(?xx)[ ]#](?<100>a)", Pcre2ErrorKind.InvalidAfterParensQuery)]
    public void CommentsAndClassBoundariesCannotHideRealForbiddenSyntax(string pattern, Pcre2ErrorKind kind)
    {
        var failure = Assert.Throws<Pcre2CompileException>(() =>
            new Utf8Pcre2Regex(pattern, options: Pcre2CompileOptions.Extended));
        Assert.Equal(kind, failure.ErrorKind);
    }

    [Theory]
    [InlineData(Pcre2NewlineConvention.Lf, "\n")]
    [InlineData(Pcre2NewlineConvention.Cr, "\r")]
    [InlineData(Pcre2NewlineConvention.Crlf, "\r\n")]
    [InlineData(Pcre2NewlineConvention.Any, "\u2028")]
    [InlineData(Pcre2NewlineConvention.Any, "\u0085")]
    [InlineData(Pcre2NewlineConvention.AnyCrlf, "\r\n")]
    [InlineData(Pcre2NewlineConvention.Nul, "\0")]
    public void CommentsUseConfiguredNewline(Pcre2NewlineConvention newline, string separator)
    {
        var regex = new Utf8Pcre2Regex("# [ (?C) \\C" + separator + "(😀)", options: Pcre2CompileOptions.Extended, compileSettings: new() { Newline = newline }, policy: new Utf8RegexPolicy
        {
            MatchTimeout = Timeout.InfiniteTimeSpan
        });
        ReadOnlySpan<byte> match2Input = "😀"u8;
        var match2Captures = new Utf8Pcre2CaptureRange[regex.CaptureGroupCount + 1];
        var match = regex.Match(match2Input, captures: match2Captures);
        Assert.True(match.Success);
        Assert.Equal("😀", System.Text.Encoding.UTF8.GetString((match2Captures[1].Success ? match2Input.Slice(match2Captures[1].StartOffsetInBytes, match2Captures[1].EndOffsetInBytes - match2Captures[1].StartOffsetInBytes) : ReadOnlySpan<byte>.Empty)));
    }

    [Theory]
    [InlineData(@"[]#][\C]")]
    [InlineData(@"[\]#][\C]")]
    [InlineData(@"[[:alpha:]#][\C]")]
    public void AllowingByteAtomsStillRejectsActualByteEscapesInClasses(string pattern)
    {
        var failure = Assert.Throws<Pcre2CompileException>(() => new Utf8Pcre2Regex(pattern, options: Pcre2CompileOptions.Extended, compileSettings: new() { BackslashC = Pcre2BackslashCPolicy.Allow }, policy: new Utf8RegexPolicy
        {
            MatchTimeout = Timeout.InfiniteTimeSpan
        }));
        Assert.Equal(Pcre2ErrorKind.EscapeInvalidInClass, failure.ErrorKind);
    }
}
