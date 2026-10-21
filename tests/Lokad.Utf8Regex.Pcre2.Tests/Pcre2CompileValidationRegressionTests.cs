using System.Text;
using Lokad.Utf8Regex.Internal;

namespace Lokad.Utf8Regex.Pcre2.Tests;

public sealed class Pcre2CompileValidationRegressionTests
{
    [Theory]
    [InlineData("a{5,4}", Pcre2ErrorKind.QuantifierNumbersOutOfOrder)]
    [InlineData("a{7,6}", Pcre2ErrorKind.QuantifierNumbersOutOfOrder)]
    [InlineData("(?J)(a){ 42, 9 }", Pcre2ErrorKind.QuantifierNumbersOutOfOrder)]
    [InlineData("a{65536}", Pcre2ErrorKind.QuantifierTooBig)]
    [InlineData("a{65537}", Pcre2ErrorKind.QuantifierTooBig)]
    [InlineData("a{1,65537}", Pcre2ErrorKind.QuantifierTooBig)]
    [InlineData("a{999999999999999999999,}", Pcre2ErrorKind.QuantifierTooBig)]
    [InlineData("(?z)", Pcre2ErrorKind.InvalidAfterParensQuery)]
    [InlineData("(?y:a)", Pcre2ErrorKind.InvalidAfterParensQuery)]
    [InlineData("(?iy)a", Pcre2ErrorKind.InvalidAfterParensQuery)]
    [InlineData(@"\idef", Pcre2ErrorKind.UnrecognizedEscape)]
    [InlineData(@"\iabc", Pcre2ErrorKind.UnrecognizedEscape)]
    [InlineData(@"[\j]", Pcre2ErrorKind.UnrecognizedEscape)]
    public void InvalidSyntaxIsRejectedAcrossStringAndByteEntries(string pattern, Pcre2ErrorKind expected)
    {
        Assert.Equal(expected, Assert.Throws<Pcre2CompileException>(() => new Utf8Pcre2Regex(pattern)).ErrorKind);
        Assert.Equal(expected, Assert.Throws<Pcre2CompileException>(() => new Utf8Pcre2Regex(Encoding.UTF8.GetBytes(pattern))).ErrorKind);
    }

    [Theory]
    [InlineData(@"\Q{65537}(?y)\i\E", "{65537}(?y)\\i")]
    [InlineData("[{}65537]+", "{65537}")]
    [InlineData("(?x)a # {65537}(?y)\\i\n", "a")]
    [InlineData("(?# {65537})a", "a")]
    [InlineData(@"\x{10000}", "𐀀")]
    [InlineData(@"\o{200000}", "𐀀")]
    [InlineData(@"\{65537\}", "{65537}")]
    [InlineData(@"(?J)(😀)\1", "😀😀")]
    public void LiteralAndEscapePayloadsRemainContextual(string pattern, string subject)
    {
        Assert.Equal(subject, new Utf8Pcre2Regex(pattern).Match(Encoding.UTF8.GetBytes(subject)).GetValueString());
    }

    [Fact]
    public void MaximumSupportedBoundRemainsValidWithoutExpandingItsSubject()
    {
        var regex = new Utf8Pcre2Regex("a{65535,}");
        Assert.False(regex.IsMatch("aaa"u8));
    }

    [Fact]
    public void UnsupportedGrammarRefusesBeforeProgramPublication()
    {
        Assert.Throws<NotSupportedException>(() => new Utf8Pcre2Regex(@"\u0041"));
    }
}
