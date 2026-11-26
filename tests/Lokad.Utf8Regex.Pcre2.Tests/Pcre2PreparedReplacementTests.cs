using System.Text;
using Lokad.Utf8Regex.Internal;

namespace Lokad.Utf8Regex.Pcre2.Tests;

public sealed class Pcre2PreparedReplacementTests
{
    [Theory]
    [InlineData(@"\x{1f600}", "😀")]
    [InlineData(@"\U\x{10428}", "𐐀")]
    [InlineData(@"\x41\x4f", "AO")]
    [InlineData(@"\N{U+1F600}", "😀")]
    [InlineData(@"\cA", "\u0001")]
    public void ExtendedScalarEscapesArePreparedAsUtf8(string replacement, string expected)
        => Assert.Equal(Encoding.UTF8.GetBytes(expected), new Utf8Pcre2Regex("(x)").Replace("x"u8, Encoding.UTF8.GetBytes(replacement), 0, Pcre2SubstitutionOptions.Extended));

    [Theory]
    [InlineData(@"${1:+${1}:fallback}", "x", "x")]
    [InlineData(@"${1:+${1:-other}:fallback}", "x", "x")]
    [InlineData(@"${1:+a\}b:c}", "x", "a}b")]
    [InlineData(@"${1:+a\:b:c}", "x", "a:b")]
    [InlineData(@"${1:+\c}:fallback}", "x", "=")]
    [InlineData(@"${1:+\c::fallback}", "x", "z")]
    public void NestedBranchesAndEscapedSeparatorsPreserveGrammar(string replacement, string input, string expected)
        => Assert.Equal(expected, System.Text.Encoding.UTF8.GetString(new Utf8Pcre2Regex("(x)?").Replace(Encoding.UTF8.GetBytes(input), Encoding.UTF8.GetBytes(replacement), 0,
            Pcre2SubstitutionOptions.Extended, Pcre2MatchOptions.NotEmpty)));

    [Theory]
    [InlineData(@"\x{d800}")] [InlineData(@"\x{110000}")] [InlineData(@"\x{z}")]
    [InlineData(@"\x{}")] [InlineData(@"\xG")] [InlineData(@"\o{}")] [InlineData(@"\N{U+d800}")]
    [InlineData(@"\c😀")]
    public void InvalidScalarEscapesHaveAReplacementError(string template)
    {
        var error = Assert.Throws<Pcre2SubstitutionException>(() => new Utf8Pcre2Regex("(x)").Replace("x"u8, Encoding.UTF8.GetBytes(template), 0, Pcre2SubstitutionOptions.Extended));
        Assert.Equal(Pcre2ErrorKind.BadReplacementEscape, error.ErrorKind);
    }

    [Theory]
    [InlineData(@"${1:-\x{z}}", "x")]
    [InlineData(@"${1:+good:$missing}", "good")]
    [InlineData(@"${1:+\Q$1\E:bad}$1", "$1x")]
    public void DeferredBranchesKeepTheirValidationAndState(string template, string expected)
        => Assert.Equal(expected, System.Text.Encoding.UTF8.GetString(new Utf8Pcre2Regex("(x)").Replace("x"u8, Encoding.UTF8.GetBytes(template), 0, Pcre2SubstitutionOptions.Extended)));

    [Fact]
    public void PlusValidatesBothBranchesButNoMatchDoesNotEvaluateErrors()
    {
        var regex = new Utf8Pcre2Regex("(x)");
        var template = @"${1:+good:\x{z}}";
        Assert.Equal(Pcre2ErrorKind.BadReplacementEscape, Assert.Throws<Pcre2SubstitutionException>(() => regex.Replace("x"u8, Encoding.UTF8.GetBytes(template), 0, Pcre2SubstitutionOptions.Extended)).ErrorKind);
        Assert.Equal("y"u8.ToArray(), regex.Replace("y"u8, Encoding.UTF8.GetBytes(template), 0, Pcre2SubstitutionOptions.Extended));
    }

    [Fact]
    public void ChosenCaseStateContinuesAfterTheBranchAndResetsPerMatch()
        => Assert.Equal("HELLO hello", System.Text.Encoding.UTF8.GetString(new Utf8Pcre2Regex("(x)?y").Replace("xy y"u8, @"${1:+\U:\L}HeLLo"u8, 0, Pcre2SubstitutionOptions.Extended)));

    [Fact]
    public void DefaultAnglesAndBareMarkUsePcre2Syntax()
    {
        Assert.Equal("😀", System.Text.Encoding.UTF8.GetString(new Utf8Pcre2Regex("(?<w>😀)").Replace("😀"u8, "$<w>"u8)));
        Assert.Equal("pear", System.Text.Encoding.UTF8.GetString(new Utf8Pcre2Regex("(*MARK:pear)x").Replace("x"u8, "$*MARK"u8)));
    }

    [Theory]
    [InlineData(@"\12", "\n")]
    [InlineData(@"\1234", "S4")]
    [InlineData(@"\0113", "\t3")]
    public void UnbracedOctalReadsAtMostThreeDigits(string template, string expected)
        => Assert.Equal(expected, System.Text.Encoding.UTF8.GetString(new Utf8Pcre2Regex("x").Replace("x"u8, Encoding.UTF8.GetBytes(template), 0, Pcre2SubstitutionOptions.Extended)));

    [Fact]
    public void ExistingMultiDigitGroupWinsOverAmbiguousOctal()
        => Assert.Equal("x", System.Text.Encoding.UTF8.GetString(new Utf8Pcre2Regex(string.Concat(Enumerable.Repeat("(x)", 12))).Replace("xxxxxxxxxxxx"u8, @"\12"u8, 0, Pcre2SubstitutionOptions.Extended)));

    [Fact]
    public void OversizedNumericReferenceFailsInTheProfileVocabulary()
        => Assert.Equal(Pcre2ErrorKind.UnknownReplacementGroup, Assert.Throws<Pcre2SubstitutionException>(() => new Utf8Pcre2Regex("(x)").Replace("x"u8, Encoding.UTF8.GetBytes("$" + new string('9', 100)))).ErrorKind);

    [Fact]
    public void DeepFallbacksUseIterativePreparationAndExpansion()
    {
        var template = "z";
        for (var index = 0; index < 256; index++) template = "${1:-" + template + "}";
        Assert.Equal("z", System.Text.Encoding.UTF8.GetString(new Utf8Pcre2Regex("(a)?b").Replace("b"u8, Encoding.UTF8.GetBytes(template), 0, Pcre2SubstitutionOptions.Extended)));
    }

    [Theory]
    [InlineData(@"\U\Q𐐨$1\E𐐨", "𐐀$1𐐀")]
    [InlineData(@"\L\Q𐐀X\E𐐀", "𐐨x𐐨")]
    public void EndingQuotationPreservesContinuousCaseForcing(string template, string expected)
        => Assert.Equal(expected, System.Text.Encoding.UTF8.GetString(new Utf8Pcre2Regex("(x)").Replace("x"u8, Encoding.UTF8.GetBytes(template), 0, Pcre2SubstitutionOptions.Extended)));
}
