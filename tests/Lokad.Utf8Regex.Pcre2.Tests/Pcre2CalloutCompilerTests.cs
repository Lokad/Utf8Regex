using System.Text;
using Lokad.Utf8Regex.Internal;
using Lokad.Utf8Regex.Internal.FrontEnd;

namespace Lokad.Utf8Regex.Pcre2.Tests;

public sealed class Pcre2CalloutCompilerTests
{
    [Fact]
    public void CalloutTextInCharacterClassMatchesLiterally()
    {
        var match = new Utf8Pcre2Regex("[(?C]").Match("x?y"u8);

        Assert.True(match.Success);
        Assert.Equal("?", System.Text.Encoding.UTF8.GetString(match.GetValueBytes()));
        Assert.Equal(1, match.StartOffsetInBytes);
        Assert.Equal(2, match.EndOffsetInBytes);
    }

    [Fact]
    public void CalloutTextInQuotedLiteralMatchesLiterally()
    {
        var match = new Utf8Pcre2Regex("\\Q(?C\\E").Match("a(?Cb"u8);

        Assert.True(match.Success);
        Assert.Equal("(?C", System.Text.Encoding.UTF8.GetString(match.GetValueBytes()));
        Assert.Equal(1, match.StartOffsetInBytes);
        Assert.Equal(4, match.EndOffsetInBytes);
    }

    [Fact]
    public void CalloutTextInCommentIsIgnored()
    {
        var match = new Utf8Pcre2Regex("(?#(?C)ab").Match("xxab"u8);

        Assert.True(match.Success);
        Assert.Equal("ab", System.Text.Encoding.UTF8.GetString(match.GetValueBytes()));
        Assert.Equal(2, match.StartOffsetInBytes);
        Assert.Equal(4, match.EndOffsetInBytes);
    }

    [Fact]
    public void CalloutTextBehindEscapesMatchesLiterally()
    {
        var match = new Utf8Pcre2Regex("\\(\\?C\\)").Match("a(?C)b"u8);

        Assert.True(match.Success);
        Assert.Equal("(?C)", System.Text.Encoding.UTF8.GetString(match.GetValueBytes()));
        Assert.Equal(1, match.StartOffsetInBytes);
        Assert.Equal(5, match.EndOffsetInBytes);
    }

    [Theory]
    [InlineData("^a(?C1)b$", "ab", "ac")]
    [InlineData("^a+(?C255)b$", "aaab", "aaac")]
    [InlineData("^(?C0)(?C1)$", "", "a")]
    [InlineData("(?x)^a (?C) b$", "ab", "ac")]
    [InlineData("^(?(?C9)(?=a)ab|de)$", "ab", "ac")]
    [InlineData("^(?(?C9)(?=a)ab|de)$", "de", "ac")]
    public void NumberedCalloutsHaveNoMatchingEffect(string pattern, string hit, string miss)
    {
        using var text = Utf8Pcre2Regex.Compile(pattern);
        using var bytes = Utf8Pcre2Regex.Compile(Encoding.UTF8.GetBytes(pattern));
        foreach (var regex in new[] { text, bytes })
        {
            var input = Encoding.UTF8.GetBytes(hit);
            Assert.Equal(pattern, regex.Pattern);
            Assert.True(regex.IsMatch(input));
            Assert.False(regex.IsMatch(Encoding.UTF8.GetBytes(miss)));
            Assert.Equal(1, regex.Count(input));
            Assert.Equal(hit, Encoding.UTF8.GetString(regex.Match(input).GetValueBytes()));
        }
    }

    [Fact]
    public void NumberedCalloutsPreserveCaptureSlotsAndByteRanges()
    {
        using var regex = Utf8Pcre2Regex.Compile("(?<left>é)(?C001)(?<right>😀)"u8);
        var captures = new Utf8Pcre2CaptureRange[3];
        var match = regex.Match("xé😀"u8, captures: captures);

        Assert.Equal(2, regex.CaptureGroupCount);
        Assert.Equal((1, 7), (match.StartOffsetInBytes, match.EndOffsetInBytes));
        Assert.Equal((1, 3), (captures[1].StartOffsetInBytes, captures[1].EndOffsetInBytes));
        Assert.Equal((3, 7), (captures[2].StartOffsetInBytes, captures[2].EndOffsetInBytes));
    }

    [Theory]
    [InlineData("a(?C1)*b", Pcre2ErrorKind.QuantifierInvalid)]
    [InlineData("(?x)a(?C1) # comment\n {2}b", Pcre2ErrorKind.QuantifierInvalid)]
    [InlineData("a(?C256)b", Pcre2ErrorKind.InvalidAfterParensQuery)]
    [InlineData("a(?C1x)b", Pcre2ErrorKind.InvalidAfterParensQuery)]
    [InlineData("a(?C1", Pcre2ErrorKind.MissingClosingParenthesis)]
    [InlineData("(?(?C9)(1)ab|de)", Pcre2ErrorKind.InvalidAfterParensQuery)]
    [InlineData("(?C\"(?<100>foo)\\C\")(a)", Pcre2ErrorKind.CalloutUnsupported)]
    public void InvalidAndUnsupportedCalloutFormsReportCompileErrors(string pattern, Pcre2ErrorKind kind)
    {
        Assert.Equal(kind, Assert.Throws<Pcre2CompileException>(() => Utf8Pcre2Regex.Compile(pattern)).ErrorKind);
        Assert.Equal(kind, Assert.Throws<Pcre2CompileException>(() => Utf8Pcre2Regex.Compile(Encoding.UTF8.GetBytes(pattern))).ErrorKind);
    }

    [Fact]
    public void LongCalloutNumberChecksForCancellationBeforeAdvancing()
    {
        var pattern = "(?C" + new string('0', 4096) + "1)";
        using var scope = Utf8ConstructionScope.Enter();
        using var cancellation = new CancellationTokenSource();
        scope.Budget.CancellationToken = cancellation.Token;
        var checkpoints = 0;
        scope.Budget.CheckpointHook = () => { if (++checkpoints == 2) cancellation.Cancel(); };
        var offset = 0;
        var failure = Assert.Throws<OperationCanceledException>(() =>
            Pcre2PatternCodeScan.TrySkipNumberedCallout(new Utf8ConstructionPattern(pattern), ref offset));
        Assert.Equal(cancellation.Token, failure.CancellationToken);
        Assert.Equal(0, offset);
        Assert.Equal(0ul, scope.Budget.ReservedBytes);
    }

    [Theory]
    [InlineData("[{5,4}]", "a5b", "5", 1, 2)]
    [InlineData("\\Q{5,4}\\E", "x{5,4}y", "{5,4}", 1, 6)]
    [InlineData("a\\{5,4\\}", "a{5,4}", "a{5,4}", 0, 6)]
    [InlineData("[{65536}]", "a6b", "6", 1, 2)]
    [InlineData("[(?X)]", "aXb", "X", 1, 2)]
    [InlineData("\\Q(?X)\\E", "(?X)", "(?X)", 0, 4)]
    [InlineData("(?#(?z)ab", "xxab", "ab", 2, 4)]
    [InlineData("\\\\idef", "\\idef", "\\idef", 0, 5)]
    public void RawScanSentinelsInLiteralContextMatchLiterally(string pattern, string input, string expectedValue, int expectedStart, int expectedEnd)
    {
        var match = new Utf8Pcre2Regex(pattern).Match(System.Text.Encoding.UTF8.GetBytes(input));

        Assert.True(match.Success);
        Assert.Equal(expectedValue, System.Text.Encoding.UTF8.GetString(match.GetValueBytes()));
        Assert.Equal(expectedStart, match.StartOffsetInBytes);
        Assert.Equal(expectedEnd, match.EndOffsetInBytes);
    }

    [Fact]
    public void QuotedResetInLookaroundIsLiteral()
    {
        var match = new Utf8Pcre2Regex("(?=x\\Q\\K\\E)x\\K").Match("x\\K"u8);

        Assert.True(match.Success);
        Assert.Equal(1, match.StartOffsetInBytes);
        Assert.Equal(1, match.EndOffsetInBytes);
    }

    [Fact]
    public void UnrecognizedEscapeInClassStaysRejected()
    {
        var exception = Assert.Throws<Pcre2CompileException>(() => new Utf8Pcre2Regex("[\\idef]"));

        Assert.Equal(Pcre2ErrorKind.UnrecognizedEscape, exception.ErrorKind);
    }
}
