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
        => Assert.Equal(Encoding.UTF8.GetBytes(expected), new Utf8Pcre2Regex("(x)").Replace("x"u8, replacement, 0, Pcre2SubstitutionOptions.Extended));

    [Theory]
    [InlineData(@"${1:+${1}:fallback}", "x", "x")]
    [InlineData(@"${1:+${1:-other}:fallback}", "x", "x")]
    [InlineData(@"${1:+a\}b:c}", "x", "a}b")]
    [InlineData(@"${1:+a\:b:c}", "x", "a:b")]
    [InlineData(@"${1:+\c}:fallback}", "x", "=")]
    [InlineData(@"${1:+\c::fallback}", "x", "z")]
    public void NestedBranchesAndEscapedSeparatorsPreserveGrammar(string replacement, string input, string expected)
        => Assert.Equal(expected, new Utf8Pcre2Regex("(x)?").ReplaceToString(Encoding.UTF8.GetBytes(input), replacement, 0,
            Pcre2SubstitutionOptions.Extended, Pcre2MatchOptions.NotEmpty));

    [Fact]
    public void GeneralTemplatesHaveAnOwnedPreparedProgram()
        => Assert.NotNull(typeof(Utf8Pcre2Regex).Assembly.GetType("Lokad.Utf8Regex.Pcre2.Pcre2PreparedReplacementPlan"));

    [Theory]
    [InlineData(@"\x{d800}")] [InlineData(@"\x{110000}")] [InlineData(@"\x{z}")]
    [InlineData(@"\x{}")] [InlineData(@"\xG")] [InlineData(@"\o{}")] [InlineData(@"\N{U+d800}")]
    [InlineData(@"\c😀")]
    public void InvalidScalarEscapesHaveAReplacementError(string template)
    {
        var error = Assert.Throws<Pcre2SubstitutionException>(() => new Utf8Pcre2Regex("(x)").Replace("x"u8, template, 0, Pcre2SubstitutionOptions.Extended));
        Assert.Equal(Pcre2ErrorKind.BadReplacementEscape, error.ErrorKind);
    }

    [Theory]
    [InlineData(@"${1:-\x{z}}", "x")]
    [InlineData(@"${1:+good:$missing}", "good")]
    [InlineData(@"${1:+\Q$1\E:bad}$1", "$1x")]
    public void DeferredBranchesKeepTheirValidationAndState(string template, string expected)
        => Assert.Equal(expected, new Utf8Pcre2Regex("(x)").ReplaceToString("x"u8, template, 0, Pcre2SubstitutionOptions.Extended));

    [Fact]
    public void PlusValidatesBothBranchesButNoMatchDoesNotEvaluateErrors()
    {
        var regex = new Utf8Pcre2Regex("(x)");
        var template = @"${1:+good:\x{z}}";
        Assert.Equal(Pcre2ErrorKind.BadReplacementEscape, Assert.Throws<Pcre2SubstitutionException>(() => regex.Replace("x"u8, template, 0, Pcre2SubstitutionOptions.Extended)).ErrorKind);
        Assert.Equal("y"u8.ToArray(), regex.Replace("y"u8, template, 0, Pcre2SubstitutionOptions.Extended));
    }

    [Fact]
    public void ChosenCaseStateContinuesAfterTheBranchAndResetsPerMatch()
        => Assert.Equal("HELLO hello", new Utf8Pcre2Regex("(x)?y").ReplaceToString("xy y"u8, @"${1:+\U:\L}HeLLo", 0, Pcre2SubstitutionOptions.Extended));

    [Fact]
    public void DefaultAnglesAndBareMarkUsePcre2Syntax()
    {
        Assert.Equal("😀", new Utf8Pcre2Regex("(?<w>😀)").ReplaceToString("😀"u8, "$<w>"));
        Assert.Equal("pear", new Utf8Pcre2Regex("(*MARK:pear)x").ReplaceToString("x"u8, "$*MARK"));
    }

    [Theory]
    [InlineData(@"\12", "\n")]
    [InlineData(@"\1234", "S4")]
    [InlineData(@"\0113", "\t3")]
    public void UnbracedOctalReadsAtMostThreeDigits(string template, string expected)
        => Assert.Equal(expected, new Utf8Pcre2Regex("x").ReplaceToString("x"u8, template, 0, Pcre2SubstitutionOptions.Extended));

    [Fact]
    public void ExistingMultiDigitGroupWinsOverAmbiguousOctal()
        => Assert.Equal("x", new Utf8Pcre2Regex(string.Concat(Enumerable.Repeat("(x)", 12))).ReplaceToString("xxxxxxxxxxxx"u8, @"\12", 0, Pcre2SubstitutionOptions.Extended));

    [Fact]
    public void OversizedNumericReferenceFailsInTheProfileVocabulary()
        => Assert.Equal(Pcre2ErrorKind.UnknownReplacementGroup, Assert.Throws<Pcre2SubstitutionException>(() => new Utf8Pcre2Regex("(x)").Replace("x"u8, "$" + new string('9', 100))).ErrorKind);

    [Fact]
    public void DeepFallbacksUseIterativePreparationAndExpansion()
    {
        var template = "z";
        for (var index = 0; index < 256; index++) template = "${1:-" + template + "}";
        Assert.Equal("z", new Utf8Pcre2Regex("(a)?b").ReplaceToString("b"u8, template, 0, Pcre2SubstitutionOptions.Extended));
    }

    [Theory]
    [InlineData(@"\U\Q𐐨$1\E𐐨", "𐐀$1𐐀")]
    [InlineData(@"\L\Q𐐀X\E𐐀", "𐐨x𐐨")]
    public void EndingQuotationPreservesContinuousCaseForcing(string template, string expected)
        => Assert.Equal(expected, new Utf8Pcre2Regex("(x)").ReplaceToString("x"u8, template, 0, Pcre2SubstitutionOptions.Extended));
}
