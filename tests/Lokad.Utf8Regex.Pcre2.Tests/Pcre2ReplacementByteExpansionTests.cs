using System.Text;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Pcre2.Tests;

public sealed class Pcre2ReplacementByteExpansionTests
{
    [Fact]
    public void FixedTemplateExpansionOwnsAByteWriter()
    {
        var type = typeof(Utf8Pcre2Regex).Assembly.GetType("Lokad.Utf8Regex.Pcre2.Pcre2ReplacementTemplateWriter");
        Assert.NotNull(type);
        Assert.True(type.IsByRefLike);
    }

    [Theory]
    [InlineData(@"\o{202050}", "𐐨")]
    [InlineData(@"\U\o{202050}", "𐐀")]
    [InlineData(@"\o{373000}", "😀")]
    public void OctalEscapesProduceOneCompleteScalar(string template, string expected)
    {
        Assert.Equal(Encoding.UTF8.GetBytes(expected), new Utf8Pcre2Regex("(x)").Replace("x"u8, template, 0, Pcre2SubstitutionOptions.Extended));
    }

    [Theory]
    [InlineData(@"\o{154000}")]
    [InlineData(@"\o{4200000}")]
    public void InvalidScalarOctalEscapesAreRejected(string template)
    {
        var error = Assert.Throws<Pcre2SubstitutionException>(() => new Utf8Pcre2Regex("(x)").Replace("x"u8, template, 0, Pcre2SubstitutionOptions.Extended));
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
        Assert.Equal(Encoding.UTF8.GetBytes(expected), regex.Replace("α😀𐐨β"u8, template, 0, Pcre2SubstitutionOptions.Extended));
        Assert.Equal(expected, regex.ReplaceToString("α😀𐐨β"u8, template, 0, Pcre2SubstitutionOptions.Extended));
    }

    [Fact]
    public void ExplicitByteCapturesAreCopiedWithoutUtf16Projection()
    {
        var regex = new Utf8Pcre2Regex(@"(\C)", Pcre2CompileOptions.None,
            new Utf8Pcre2CompileSettings { BackslashC = Pcre2BackslashCPolicy.Allow }, default, Utf8Pcre2Regex.DefaultMatchTimeout);
        Assert.Equal("😀"u8.ToArray(), regex.Replace("😀"u8, "$1"));
        Assert.Equal(new byte[] {0xf0, 0xf0, 0x9f, 0x9f, 0x98, 0x98, 0x80, 0x80}, regex.Replace("😀"u8, "$1$1"));
        Assert.Throws<ArgumentException>(() => regex.ReplaceToString("😀"u8, "$1$1"));
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void ReplacementOnlyAndStartOffsetsPreserveNativeScalarDiscovery(bool replacementOnly)
    {
        var regex = new Utf8Pcre2Regex("(?<w>😀)");
        var options = replacementOnly ? Pcre2SubstitutionOptions.SubstituteReplacementOnly : Pcre2SubstitutionOptions.None;
        var actual = regex.Replace("😀x😀"u8, "[${w}]", 4, options);
        Assert.Equal(Encoding.UTF8.GetBytes(replacementOnly ? "[😀]" : "😀x[😀]"), actual);
    }

    [Fact]
    public void UnpairedTemplateFragmentsCannotJoinAcrossAnEmptyCapture()
    {
        var template = new string((char)0xd83d, 1) + "$1" + new string((char)0xde00, 1);
        Assert.Equal("��😀��", new Utf8Pcre2Regex("()").ReplaceToString("😀"u8, template));
    }

    [Fact]
    public void NoMatchLeavesInvalidTemplatesUnevaluated()
    {
        var regex = new Utf8Pcre2Regex("(cat)");
        Assert.Equal("😀dog"u8.ToArray(), regex.Replace("😀dog"u8, @"\o{Z}", 0, Pcre2SubstitutionOptions.Extended));
        Assert.Empty(regex.Replace("😀dog"u8, "${", 0, Pcre2SubstitutionOptions.SubstituteReplacementOnly));
    }

    [Fact]
    public void GrowingOutputAdmitsOldAndNewWorkingBuffersAndAlwaysReleases()
    {
        var writer = new Pcre2ReplacementTemplateWriter("😀"u8, [], 32, Timeout.InfiniteTimeSpan, default);
        try
        {
            Assert.Equal(4ul, writer.ReservedWorkingBytes);
            writer.AppendTemplate(new(null, Pcre2PreparedReplacementParser.Parse("café😀", Pcre2SubstitutionOptions.None)), Pcre2SubstitutionOptions.None, [], 0, 4, null);
            Assert.Equal(9ul, writer.ReservedWorkingBytes);
            var result = writer.Complete();
            Assert.Equal("café😀"u8.ToArray(), result);
            Assert.Equal(0ul, writer.ReservedWorkingBytes);
        }
        finally { writer.Release(); }
        writer.Release();
        Assert.Equal(0ul, writer.ReservedWorkingBytes);

        var limited = new Pcre2ReplacementTemplateWriter("😀"u8, [], 11, Timeout.InfiniteTimeSpan, default);
        try
        {
            Pcre2MatchException? failure = null;
            try { limited.AppendTemplate(new(null, Pcre2PreparedReplacementParser.Parse("café😀", Pcre2SubstitutionOptions.None)), Pcre2SubstitutionOptions.None, [], 0, 4, null); }
            catch (Pcre2MatchException error) { failure = error; }
            Assert.NotNull(failure);
            Assert.Equal(Pcre2ErrorKind.HeapLimit, failure.ErrorKind);
            Assert.Equal(4ul, limited.ReservedWorkingBytes);
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
        var writer = new Pcre2ReplacementTemplateWriter("😀"u8, [], 0,
            deadline ? TimeSpan.FromMilliseconds(1) : Timeout.InfiniteTimeSpan, token.Token);
        if (deadline) clock++;
        else token.Cancel();
        try
        {
            Exception? failure = null;
            try { writer.AppendTemplate(new(null, Pcre2PreparedReplacementParser.Parse(new string('x', 256), Pcre2SubstitutionOptions.None)), Pcre2SubstitutionOptions.None, [], 0, 4, null); }
            catch (Exception error) { failure = error; }
            if (deadline) Assert.Equal(Pcre2ErrorKind.Timeout, Assert.IsType<Pcre2MatchException>(failure).ErrorKind);
            else Assert.Equal(token.Token, Assert.IsType<OperationCanceledException>(failure).CancellationToken);
        }
        finally { writer.Release(); }
        Assert.Equal(0ul, writer.ReservedWorkingBytes);
    }
}
