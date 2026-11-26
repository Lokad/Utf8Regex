using System.Text;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Pcre2.Tests;

public sealed class Pcre2ReplacementByteExpansionTests
{
    [Theory]
    [InlineData(@"\o{202050}", "𐐨")]
    [InlineData(@"\U\o{202050}", "𐐀")]
    [InlineData(@"\o{373000}", "😀")]
    public void OctalEscapesProduceOneCompleteScalar(string template, string expected)
    {
        Assert.Equal(Encoding.UTF8.GetBytes(expected), new Utf8Pcre2Regex("(x)").Replace("x"u8, Encoding.UTF8.GetBytes(template), 0, Pcre2SubstitutionOptions.Extended));
    }

    [Theory]
    [InlineData(@"\o{154000}")]
    [InlineData(@"\o{4200000}")]
    public void InvalidScalarOctalEscapesAreRejected(string template)
    {
        var error = Assert.Throws<Pcre2SubstitutionException>(() => new Utf8Pcre2Regex("(x)").Replace("x"u8, Encoding.UTF8.GetBytes(template), 0, Pcre2SubstitutionOptions.Extended));
        Assert.Equal(Pcre2ErrorKind.BadReplacementEscape, error.ErrorKind);
    }

    [Theory]
    [InlineData("$0", "α😀𐐨β")]
    [InlineData("${w}", "α😀𐐨β")]
    [InlineData("$1$1", "α😀𐐨😀𐐨β")]
    [InlineData("$`", "ααβ")]
    [InlineData("$'", "αββ")]
    [InlineData("$_", "αα😀𐐨ββ")]
    [InlineData("$+", "α😀𐐨β")]
    [InlineData(@"\U${w}", "α😀𐐀β")]
    [InlineData(@"${w:+\U$1:none}", "α😀𐐀β")]
    public void CapturesAndSubjectReferencesUseExactScalarByteRanges(string template, string expected)
    {
        var regex = new Utf8Pcre2Regex("(?<w>😀𐐨)");
        Assert.Equal(Encoding.UTF8.GetBytes(expected), regex.Replace("α😀𐐨β"u8, Encoding.UTF8.GetBytes(template), 0, Pcre2SubstitutionOptions.Extended));
        Assert.Equal(expected, System.Text.Encoding.UTF8.GetString(regex.Replace("α😀𐐨β"u8, Encoding.UTF8.GetBytes(template), 0, Pcre2SubstitutionOptions.Extended)));
    }

    [Fact]
    public void ExplicitByteCapturesAreCopiedWithoutUtf16Projection()
    {
        var regex = new Utf8Pcre2Regex(@"(\C)", options: Pcre2CompileOptions.None, compileSettings:             new Utf8Pcre2CompileSettings { BackslashC = Pcre2BackslashCPolicy.Allow }, policy: new Utf8RegexPolicy
        {
            MatchTimeout = Utf8Pcre2Regex.DefaultMatchTimeout
        });
        Assert.Equal("😀"u8.ToArray(), regex.Replace("😀"u8, "$1"u8));
        Assert.Equal(new byte[] {0xf0, 0xf0, 0x9f, 0x9f, 0x98, 0x98, 0x80, 0x80}, regex.Replace("😀"u8, "$1$1"u8));
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void ReplacementOnlyAndStartOffsetsPreserveNativeScalarDiscovery(bool replacementOnly)
    {
        var regex = new Utf8Pcre2Regex("(?<w>😀)");
        var options = replacementOnly ? Pcre2SubstitutionOptions.SubstituteReplacementOnly : Pcre2SubstitutionOptions.None;
        var actual = regex.Replace("😀x😀"u8, "[${w}]"u8, 4, options);
        Assert.Equal(Encoding.UTF8.GetBytes(replacementOnly ? "[😀]" : "😀x[😀]"), actual);
    }

    [Fact]
    public void UnicodeTemplateFragmentsSurviveEmptyCaptureExpansion()
    {
        Assert.Equal("𐀀é😀𐀀é"u8.ToArray(), new Utf8Pcre2Regex("()").Replace("😀"u8, "𐀀$1é"u8));
    }

    [Fact]
    public void NoMatchLeavesInvalidTemplatesUnevaluated()
    {
        var regex = new Utf8Pcre2Regex("(cat)");
        Assert.Equal("😀dog"u8.ToArray(), regex.Replace("😀dog"u8, @"\o{Z}"u8, 0, Pcre2SubstitutionOptions.Extended));
        Assert.Empty(regex.Replace("😀dog"u8, "${"u8, 0, Pcre2SubstitutionOptions.SubstituteReplacementOnly));
    }

    [Fact]
    public void GrowingOutputAdmitsOldAndNewWorkingBuffersAndAlwaysReleases()
    {
        var writer = new Pcre2ReplacementTemplateWriter("😀"u8, [],
            new Utf8SliceInvocation(new Utf8ExecutionLimits(0, 0, 96),
                Timeout.InfiniteTimeSpan, default));
        try
        {
            Assert.Equal(48ul, writer.ReservedWorkingBytes);
            writer.AppendTemplate(new(null, Pcre2PreparedReplacementParser.Parse("café😀"u8, Pcre2SubstitutionOptions.None)), Pcre2SubstitutionOptions.None, [], 0, 4, null);
            Assert.Equal(48ul, writer.ReservedWorkingBytes);
            var result = writer.Complete();
            Assert.Equal("café😀"u8.ToArray(), result);
            Assert.Equal(0ul, writer.ReservedWorkingBytes);
        }
        finally { writer.Release(); }
        writer.Release();
        Assert.Equal(0ul, writer.ReservedWorkingBytes);

        var limited = new Pcre2ReplacementTemplateWriter("😀"u8, [],
            new Utf8SliceInvocation(new Utf8ExecutionLimits(0, 0, 95),
                Timeout.InfiniteTimeSpan, default));
        try
        {
            Pcre2MatchException? failure = null;
            try { limited.AppendTemplate(new(null, Pcre2PreparedReplacementParser.Parse("café😀"u8, Pcre2SubstitutionOptions.None)), Pcre2SubstitutionOptions.None, [], 0, 4, null); }
            catch (Pcre2MatchException error) { failure = error; }
            Assert.NotNull(failure);
            Assert.Equal(Pcre2ErrorKind.HeapLimit, failure.ErrorKind);
            Assert.Equal(48ul, limited.ReservedWorkingBytes);
        }
        finally { limited.Release(); }
        Assert.Equal(0ul, limited.ReservedWorkingBytes);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void ByteShapingKeepsCancellationAndDeadlineAndReleasesWorkingCapacity(bool deadline)
    {
        using var token = new CancellationTokenSource();
        long clock = 10000;
        using var clockScope = Utf8ExecutionDeadline.UseClockForDiagnostics(() => clock);
        var writer = new Pcre2ReplacementTemplateWriter("😀"u8, [],
            new Utf8SliceInvocation(new Utf8ExecutionLimits(0, 0, 0),
                deadline ? TimeSpan.FromMilliseconds(1) : Timeout.InfiniteTimeSpan, token.Token));
        if (deadline) clock++;
        else token.Cancel();
        try
        {
            Exception? failure = null;
            try { writer.AppendTemplate(new(null, Pcre2PreparedReplacementParser.Parse(Encoding.UTF8.GetBytes(new string('x', 256)), Pcre2SubstitutionOptions.None)), Pcre2SubstitutionOptions.None, [], 0, 4, null); }
            catch (Exception error) { failure = error; }
            if (deadline) Assert.Equal(Pcre2ErrorKind.Timeout, Assert.IsType<Pcre2MatchException>(failure).ErrorKind);
            else Assert.Equal(token.Token, Assert.IsType<OperationCanceledException>(failure).CancellationToken);
        }
        finally { writer.Release(); }
        Assert.Equal(0ul, writer.ReservedWorkingBytes);
    }
}
