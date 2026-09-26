using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.FrontEnd;
using RuntimeFrontEnd = Lokad.Utf8Regex.Internal.FrontEnd.Runtime;

namespace Lokad.Utf8Regex.Tests;

// Owned parser shorthand stage: the differential battery pins predefined
// shorthands, Unicode properties, and named blocks identical to the provider where the
// trees (kinds, options, payloads, children, capture accounting), plus
// fork still agrees with BCL; fork-divergent block names, range-into-set shapes, and
// unknown properties pin owned residuals instead (no-vendor rule: the fork is never extended).
public sealed class Utf8OwnedTreeParserShorthandTests
{
    public static IEnumerable<object?[]> ShorthandAcceptCases()
    {
        yield return new object?[] { "\\d", 512 };
        yield return new object?[] { "\\D", 512 };
        yield return new object?[] { "\\w", 512 };
        yield return new object?[] { "\\W", 512 };
        yield return new object?[] { "\\s", 512 };
        yield return new object?[] { "\\S", 512 };
        yield return new object?[] { "\\p{L}", 512 };
        yield return new object?[] { "\\P{L}", 512 };
        yield return new object?[] { "\\p{Lu}", 512 };
        yield return new object?[] { "\\p{Ll}", 512 };
        yield return new object?[] { "\\p{Nd}", 512 };
        yield return new object?[] { "\\p{P}", 512 };
        yield return new object?[] { "\\p{Z}", 512 };
        yield return new object?[] { "\\p{M}", 512 };
        yield return new object?[] { "\\p{C}", 512 };
        yield return new object?[] { "\\p{S}", 512 };
        yield return new object?[] { "\\p{Lt}", 512 };
        yield return new object?[] { "\\p{Lm}", 512 };
        yield return new object?[] { "\\p{Lo}", 512 };
        yield return new object?[] { "\\p{Mn}", 512 };
        yield return new object?[] { "\\p{Mc}", 512 };
        yield return new object?[] { "\\p{Me}", 512 };
        yield return new object?[] { "\\p{Nl}", 512 };
        yield return new object?[] { "\\p{No}", 512 };
        yield return new object?[] { "\\p{Zs}", 512 };
        yield return new object?[] { "\\p{Zl}", 512 };
        yield return new object?[] { "\\p{Zp}", 512 };
        yield return new object?[] { "\\p{Cc}", 512 };
        yield return new object?[] { "\\p{Cf}", 512 };
        yield return new object?[] { "\\p{Cs}", 512 };
        yield return new object?[] { "\\p{Co}", 512 };
        yield return new object?[] { "\\p{Cn}", 512 };
        yield return new object?[] { "\\p{Pc}", 512 };
        yield return new object?[] { "\\p{Pd}", 512 };
        yield return new object?[] { "\\p{Ps}", 512 };
        yield return new object?[] { "\\p{Pe}", 512 };
        yield return new object?[] { "\\p{Pi}", 512 };
        yield return new object?[] { "\\p{Pf}", 512 };
        yield return new object?[] { "\\p{Po}", 512 };
        yield return new object?[] { "\\p{Sm}", 512 };
        yield return new object?[] { "\\p{Sc}", 512 };
        yield return new object?[] { "\\p{Sk}", 512 };
        yield return new object?[] { "\\p{So}", 512 };
        yield return new object?[] { "\\p{N}", 512 };
        yield return new object?[] { "\\P{Nd}", 512 };
        yield return new object?[] { "\\P{Lu}", 512 };
        yield return new object?[] { "\\d+", 512 };
        yield return new object?[] { "\\d*", 512 };
        yield return new object?[] { "\\d?", 512 };
        yield return new object?[] { "\\d{2}", 512 };
        yield return new object?[] { "\\d{2,4}", 512 };
        yield return new object?[] { "(\\d+)", 512 };
        yield return new object?[] { "\\d+\\w*", 512 };
        yield return new object?[] { "\\p{L}+", 512 };
        yield return new object?[] { "(\\p{Lu}\\p{Ll}*)", 512 };
        yield return new object?[] { "\\d\\p{L}", 512 };
        yield return new object?[] { "(\\d)(\\w)", 512 };
        yield return new object?[] { "\\dq", 512 };
        yield return new object?[] { "q\\d", 512 };
        yield return new object?[] { "\\d\\D", 512 };
        yield return new object?[] { "[\\d]", 512 };
        yield return new object?[] { "[\\D]", 512 };
        yield return new object?[] { "[\\w]", 512 };
        yield return new object?[] { "[\\W]", 512 };
        yield return new object?[] { "[\\s]", 512 };
        yield return new object?[] { "[\\S]", 512 };
        yield return new object?[] { "[\\p{L}]", 512 };
        yield return new object?[] { "[\\P{L}]", 512 };
        yield return new object?[] { "[a\\d]", 512 };
        yield return new object?[] { "[\\da]", 512 };
        yield return new object?[] { "[a\\dz]", 512 };
        yield return new object?[] { "[\\d\\w]", 512 };
        yield return new object?[] { "[^\\d]", 512 };
        yield return new object?[] { "[^\\\\w]", 512 };
        yield return new object?[] { "[^\\p{L}]", 512 };
        yield return new object?[] { "[\\d-z]", 512 };
        yield return new object?[] { "[\\d-\\w]", 512 };
        yield return new object?[] { "[a\\s]", 512 };
        yield return new object?[] { "[\\sa]", 512 };
        yield return new object?[] { "[\\s\\d]", 512 };
        yield return new object?[] { "[\\D\\s]", 512 };
        yield return new object?[] { "[^\\s]", 512 };
        yield return new object?[] { "[^\\S]", 512 };
        yield return new object?[] { "[^\\D]", 512 };
        yield return new object?[] { "[^\\W]", 512 };
        yield return new object?[] { "[\\d\\D]", 512 };
        yield return new object?[] { "[\\w\\W]", 512 };
        yield return new object?[] { "[\\s\\S]", 512 };
        yield return new object?[] { "[a\\D]", 512 };
        yield return new object?[] { "[\\w\\d\\s]", 512 };
        yield return new object?[] { "[a-z\\d]", 512 };
        yield return new object?[] { "[\\d-a]", 512 };
        yield return new object?[] { "[\\w-_]", 512 };
        yield return new object?[] { "[_\\d]", 512 };
        yield return new object?[] { "[\\d_]", 512 };
        yield return new object?[] { "[\\S\\d]", 512 };
        yield return new object?[] { "[\\P{Nd}]", 512 };
        yield return new object?[] { "[^\\P{Nd}]", 512 };
        yield return new object?[] { "[a\\P{Nd}]", 512 };
        yield return new object?[] { "[\\p{Nd}\\P{Nd}]", 512 };
        yield return new object?[] { "[\\d-]", 512 };
        yield return new object?[] { "[-\\d]", 512 };
        yield return new object?[] { "[\\w-a]", 512 };
        yield return new object?[] { "[\\d\\d]", 512 };
        yield return new object?[] { "[a\\da]", 512 };
        yield return new object?[] { "[\\D-]", 512 };
        yield return new object?[] { "[\\p{Lu}-]", 512 };
        yield return new object?[] { "[-\\p{Lu}]", 512 };
        yield return new object?[] { "[\\p{Lu}-a]", 512 };
        yield return new object?[] { "[\\b\\d]", 512 };
        yield return new object?[] { "[\\d\\b]", 512 };
        yield return new object?[] { "[\\p{Lu}\\p{Ll}]", 512 };
        yield return new object?[] { "[a\\p{Lu}]", 512 };
        yield return new object?[] { "[\\p{Lu}a]", 512 };
        yield return new object?[] { "[^\\p{Lu}]", 512 };
        yield return new object?[] { "[\\p{Lu}-\\p{Ll}]", 512 };
        yield return new object?[] { "[\\P{Lu}]", 512 };
        yield return new object?[] { "\\d", 513 };
        yield return new object?[] { "\\w", 513 };
        yield return new object?[] { "\\s", 513 };
        yield return new object?[] { "\\p{L}", 513 };
        yield return new object?[] { "\\P{Nd}", 513 };
        yield return new object?[] { "\\d+", 513 };
        yield return new object?[] { "\\d", 768 };
        yield return new object?[] { "\\w", 768 };
        yield return new object?[] { "\\s", 768 };
        yield return new object?[] { "\\p{Lu}", 768 };
        yield return new object?[] { "\\p{Ll}", 768 };
        yield return new object?[] { "\\d+", 768 };
        yield return new object?[] { "\\d", 576 };
        yield return new object?[] { "\\p{L}", 576 };
        yield return new object?[] { "[\\d]", 576 };
        yield return new object?[] { "\\d+", 576 };
    }

    public static IEnumerable<object?[]> ShorthandClassAnchorDeclineCases()
    {
        // Anchor escapes are BCL-invalid inside a class (UnrecognizedEscape);
        // the owned parser declines them to the BCL fallback exactly like the oracle.
        yield return new object?[] { "[\\A\\d]", 512, RegexParseError.UnrecognizedEscape, 3 };
    }

    [Theory]
    [MemberData(nameof(ShorthandClassAnchorDeclineCases))]
    public void OwnedShorthandDeclinesClassAnchorEscapes(string pattern, int options, RegexParseError error, int offset)
    {
        Assert.Null(Utf8OwnedTreeParser.TryParse(pattern, (RegexOptions)options));
        var thrown = Assert.Throws<RegexParseException>(() => new Regex(pattern, (RegexOptions)options));
        Assert.Equal(error, thrown.Error);
        Assert.Equal(offset, thrown.Offset);
    }
    public static IEnumerable<object?[]> ShorthandDeclineCases()
    {
        yield return new object?[] { "\\p{}" };
        yield return new object?[] { "\\p{L" };
        yield return new object?[] { "\\p" };
        yield return new object?[] { "\\P" };
    }

    [Theory]
    [MemberData(nameof(ShorthandAcceptCases))]
    public void OwnedShorthandMatchesProviderTree(string pattern, int options)
    {
        var expected = Utf8RuntimeTreeProvider.TryParseLegacy(pattern, (RegexOptions)options);
        Assert.NotNull(expected);
        var actual = Utf8OwnedTreeParser.TryParse(pattern, (RegexOptions)options);
        Assert.NotNull(actual);
        Assert.Equal(expected.CaptureCount, actual.CaptureCount);
        Assert.Equal(expected.CaptureNames ?? [], actual.CaptureNames ?? []);
        Assert.Equal(expected.Options, actual.Options);
        Assert.Equal(expected.Culture, actual.Culture);
        Assert.True(TreesEqual(expected.Root, actual.Root));
    }

    public static IEnumerable<object?[]> BlockResidualCases()
    {
        // Fork-narrowing debt (no-vendor rule): these BCL-valid block and _xml names
        // are rejected by the pristine fork, while the owned parser serves them. They pin
        // owned-accept plus legacy-decline instead of tree agreement.
        yield return new object?[] { "[\\p{IsGreek}a]", 512 };
        yield return new object?[] { "[a\\p{IsGreek}]", 512 };
        yield return new object?[] { "\\p{_xmlC}", 512 };
        yield return new object?[] { "\\P{_xmlC}", 512 };
        yield return new object?[] { "\\p{_xmlD}", 512 };
        yield return new object?[] { "\\P{_xmlD}", 512 };
        yield return new object?[] { "\\p{_xmlI}", 512 };
        yield return new object?[] { "\\P{_xmlI}", 512 };
        yield return new object?[] { "\\p{_xmlW}", 512 };
        yield return new object?[] { "\\P{_xmlW}", 512 };
        yield return new object?[] { "\\p{IsAlphabeticPresentationForms}", 512 };
        yield return new object?[] { "\\P{IsAlphabeticPresentationForms}", 512 };
        yield return new object?[] { "\\p{IsArabic}", 512 };
        yield return new object?[] { "\\P{IsArabic}", 512 };
        yield return new object?[] { "\\p{IsArabicPresentationForms-A}", 512 };
        yield return new object?[] { "\\P{IsArabicPresentationForms-A}", 512 };
        yield return new object?[] { "\\p{IsArabicPresentationForms-B}", 512 };
        yield return new object?[] { "\\P{IsArabicPresentationForms-B}", 512 };
        yield return new object?[] { "\\p{IsArmenian}", 512 };
        yield return new object?[] { "\\P{IsArmenian}", 512 };
        yield return new object?[] { "\\p{IsArrows}", 512 };
        yield return new object?[] { "\\P{IsArrows}", 512 };
        yield return new object?[] { "\\p{IsBasicLatin}", 512 };
        yield return new object?[] { "\\P{IsBasicLatin}", 512 };
        yield return new object?[] { "\\p{IsBengali}", 512 };
        yield return new object?[] { "\\P{IsBengali}", 512 };
        yield return new object?[] { "\\p{IsBlockElements}", 512 };
        yield return new object?[] { "\\P{IsBlockElements}", 512 };
        yield return new object?[] { "\\p{IsBopomofo}", 512 };
        yield return new object?[] { "\\P{IsBopomofo}", 512 };
        yield return new object?[] { "\\p{IsBopomofoExtended}", 512 };
        yield return new object?[] { "\\P{IsBopomofoExtended}", 512 };
        yield return new object?[] { "\\p{IsBoxDrawing}", 512 };
        yield return new object?[] { "\\P{IsBoxDrawing}", 512 };
        yield return new object?[] { "\\p{IsBraillePatterns}", 512 };
        yield return new object?[] { "\\P{IsBraillePatterns}", 512 };
        yield return new object?[] { "\\p{IsBuhid}", 512 };
        yield return new object?[] { "\\P{IsBuhid}", 512 };
        yield return new object?[] { "\\p{IsCherokee}", 512 };
        yield return new object?[] { "\\P{IsCherokee}", 512 };
        yield return new object?[] { "\\p{IsCJKCompatibility}", 512 };
        yield return new object?[] { "\\P{IsCJKCompatibility}", 512 };
        yield return new object?[] { "\\p{IsCJKCompatibilityForms}", 512 };
        yield return new object?[] { "\\P{IsCJKCompatibilityForms}", 512 };
        yield return new object?[] { "\\p{IsCJKCompatibilityIdeographs}", 512 };
        yield return new object?[] { "\\P{IsCJKCompatibilityIdeographs}", 512 };
        yield return new object?[] { "\\p{IsCJKRadicalsSupplement}", 512 };
        yield return new object?[] { "\\P{IsCJKRadicalsSupplement}", 512 };
        yield return new object?[] { "\\p{IsCJKSymbolsandPunctuation}", 512 };
        yield return new object?[] { "\\P{IsCJKSymbolsandPunctuation}", 512 };
        yield return new object?[] { "\\p{IsCJKUnifiedIdeographs}", 512 };
        yield return new object?[] { "\\P{IsCJKUnifiedIdeographs}", 512 };
        yield return new object?[] { "\\p{IsCJKUnifiedIdeographsExtensionA}", 512 };
        yield return new object?[] { "\\P{IsCJKUnifiedIdeographsExtensionA}", 512 };
        yield return new object?[] { "\\p{IsCombiningDiacriticalMarks}", 512 };
        yield return new object?[] { "\\P{IsCombiningDiacriticalMarks}", 512 };
        yield return new object?[] { "\\p{IsCombiningDiacriticalMarksforSymbols}", 512 };
        yield return new object?[] { "\\P{IsCombiningDiacriticalMarksforSymbols}", 512 };
        yield return new object?[] { "\\p{IsCombiningHalfMarks}", 512 };
        yield return new object?[] { "\\P{IsCombiningHalfMarks}", 512 };
        yield return new object?[] { "\\p{IsCombiningMarksforSymbols}", 512 };
        yield return new object?[] { "\\P{IsCombiningMarksforSymbols}", 512 };
        yield return new object?[] { "\\p{IsControlPictures}", 512 };
        yield return new object?[] { "\\P{IsControlPictures}", 512 };
        yield return new object?[] { "\\p{IsCurrencySymbols}", 512 };
        yield return new object?[] { "\\P{IsCurrencySymbols}", 512 };
        yield return new object?[] { "\\p{IsCyrillic}", 512 };
        yield return new object?[] { "\\P{IsCyrillic}", 512 };
        yield return new object?[] { "\\p{IsCyrillicSupplement}", 512 };
        yield return new object?[] { "\\P{IsCyrillicSupplement}", 512 };
        yield return new object?[] { "\\p{IsDevanagari}", 512 };
        yield return new object?[] { "\\P{IsDevanagari}", 512 };
        yield return new object?[] { "\\p{IsDingbats}", 512 };
        yield return new object?[] { "\\P{IsDingbats}", 512 };
        yield return new object?[] { "\\p{IsEnclosedAlphanumerics}", 512 };
        yield return new object?[] { "\\P{IsEnclosedAlphanumerics}", 512 };
        yield return new object?[] { "\\p{IsEnclosedCJKLettersandMonths}", 512 };
        yield return new object?[] { "\\P{IsEnclosedCJKLettersandMonths}", 512 };
        yield return new object?[] { "\\p{IsEthiopic}", 512 };
        yield return new object?[] { "\\P{IsEthiopic}", 512 };
        yield return new object?[] { "\\p{IsGeneralPunctuation}", 512 };
        yield return new object?[] { "\\P{IsGeneralPunctuation}", 512 };
        yield return new object?[] { "\\p{IsGeometricShapes}", 512 };
        yield return new object?[] { "\\P{IsGeometricShapes}", 512 };
        yield return new object?[] { "\\p{IsGeorgian}", 512 };
        yield return new object?[] { "\\P{IsGeorgian}", 512 };
        yield return new object?[] { "\\p{IsGreek}", 512 };
        yield return new object?[] { "\\P{IsGreek}", 512 };
        yield return new object?[] { "\\p{IsGreekandCoptic}", 512 };
        yield return new object?[] { "\\P{IsGreekandCoptic}", 512 };
        yield return new object?[] { "\\p{IsGreekExtended}", 512 };
        yield return new object?[] { "\\P{IsGreekExtended}", 512 };
        yield return new object?[] { "\\p{IsGujarati}", 512 };
        yield return new object?[] { "\\P{IsGujarati}", 512 };
        yield return new object?[] { "\\p{IsGurmukhi}", 512 };
        yield return new object?[] { "\\P{IsGurmukhi}", 512 };
        yield return new object?[] { "\\p{IsHalfwidthandFullwidthForms}", 512 };
        yield return new object?[] { "\\P{IsHalfwidthandFullwidthForms}", 512 };
        yield return new object?[] { "\\p{IsHangulCompatibilityJamo}", 512 };
        yield return new object?[] { "\\P{IsHangulCompatibilityJamo}", 512 };
        yield return new object?[] { "\\p{IsHangulJamo}", 512 };
        yield return new object?[] { "\\P{IsHangulJamo}", 512 };
        yield return new object?[] { "\\p{IsHangulSyllables}", 512 };
        yield return new object?[] { "\\P{IsHangulSyllables}", 512 };
        yield return new object?[] { "\\p{IsHanunoo}", 512 };
        yield return new object?[] { "\\P{IsHanunoo}", 512 };
        yield return new object?[] { "\\p{IsHebrew}", 512 };
        yield return new object?[] { "\\P{IsHebrew}", 512 };
        yield return new object?[] { "\\p{IsHighPrivateUseSurrogates}", 512 };
        yield return new object?[] { "\\P{IsHighPrivateUseSurrogates}", 512 };
        yield return new object?[] { "\\p{IsHighSurrogates}", 512 };
        yield return new object?[] { "\\P{IsHighSurrogates}", 512 };
        yield return new object?[] { "\\p{IsHiragana}", 512 };
        yield return new object?[] { "\\P{IsHiragana}", 512 };
        yield return new object?[] { "\\p{IsIdeographicDescriptionCharacters}", 512 };
        yield return new object?[] { "\\P{IsIdeographicDescriptionCharacters}", 512 };
        yield return new object?[] { "\\p{IsIPAExtensions}", 512 };
        yield return new object?[] { "\\P{IsIPAExtensions}", 512 };
        yield return new object?[] { "\\p{IsKanbun}", 512 };
        yield return new object?[] { "\\P{IsKanbun}", 512 };
        yield return new object?[] { "\\p{IsKangxiRadicals}", 512 };
        yield return new object?[] { "\\P{IsKangxiRadicals}", 512 };
        yield return new object?[] { "\\p{IsKannada}", 512 };
        yield return new object?[] { "\\P{IsKannada}", 512 };
        yield return new object?[] { "\\p{IsKatakana}", 512 };
        yield return new object?[] { "\\P{IsKatakana}", 512 };
        yield return new object?[] { "\\p{IsKatakanaPhoneticExtensions}", 512 };
        yield return new object?[] { "\\P{IsKatakanaPhoneticExtensions}", 512 };
        yield return new object?[] { "\\p{IsKhmer}", 512 };
        yield return new object?[] { "\\P{IsKhmer}", 512 };
        yield return new object?[] { "\\p{IsKhmerSymbols}", 512 };
        yield return new object?[] { "\\P{IsKhmerSymbols}", 512 };
        yield return new object?[] { "\\p{IsLao}", 512 };
        yield return new object?[] { "\\P{IsLao}", 512 };
        yield return new object?[] { "\\p{IsLatin-1Supplement}", 512 };
        yield return new object?[] { "\\P{IsLatin-1Supplement}", 512 };
        yield return new object?[] { "\\p{IsLatinExtended-A}", 512 };
        yield return new object?[] { "\\P{IsLatinExtended-A}", 512 };
        yield return new object?[] { "\\p{IsLatinExtended-B}", 512 };
        yield return new object?[] { "\\P{IsLatinExtended-B}", 512 };
        yield return new object?[] { "\\p{IsLatinExtendedAdditional}", 512 };
        yield return new object?[] { "\\P{IsLatinExtendedAdditional}", 512 };
        yield return new object?[] { "\\p{IsLetterlikeSymbols}", 512 };
        yield return new object?[] { "\\P{IsLetterlikeSymbols}", 512 };
        yield return new object?[] { "\\p{IsLimbu}", 512 };
        yield return new object?[] { "\\P{IsLimbu}", 512 };
        yield return new object?[] { "\\p{IsLowSurrogates}", 512 };
        yield return new object?[] { "\\P{IsLowSurrogates}", 512 };
        yield return new object?[] { "\\p{IsMalayalam}", 512 };
        yield return new object?[] { "\\P{IsMalayalam}", 512 };
        yield return new object?[] { "\\p{IsMathematicalOperators}", 512 };
        yield return new object?[] { "\\P{IsMathematicalOperators}", 512 };
        yield return new object?[] { "\\p{IsMiscellaneousMathematicalSymbols-A}", 512 };
        yield return new object?[] { "\\P{IsMiscellaneousMathematicalSymbols-A}", 512 };
        yield return new object?[] { "\\p{IsMiscellaneousMathematicalSymbols-B}", 512 };
        yield return new object?[] { "\\P{IsMiscellaneousMathematicalSymbols-B}", 512 };
        yield return new object?[] { "\\p{IsMiscellaneousSymbols}", 512 };
        yield return new object?[] { "\\P{IsMiscellaneousSymbols}", 512 };
        yield return new object?[] { "\\p{IsMiscellaneousSymbolsandArrows}", 512 };
        yield return new object?[] { "\\P{IsMiscellaneousSymbolsandArrows}", 512 };
        yield return new object?[] { "\\p{IsMiscellaneousTechnical}", 512 };
        yield return new object?[] { "\\P{IsMiscellaneousTechnical}", 512 };
        yield return new object?[] { "\\p{IsMongolian}", 512 };
        yield return new object?[] { "\\P{IsMongolian}", 512 };
        yield return new object?[] { "\\p{IsMyanmar}", 512 };
        yield return new object?[] { "\\P{IsMyanmar}", 512 };
        yield return new object?[] { "\\p{IsNumberForms}", 512 };
        yield return new object?[] { "\\P{IsNumberForms}", 512 };
        yield return new object?[] { "\\p{IsOgham}", 512 };
        yield return new object?[] { "\\P{IsOgham}", 512 };
        yield return new object?[] { "\\p{IsOpticalCharacterRecognition}", 512 };
        yield return new object?[] { "\\P{IsOpticalCharacterRecognition}", 512 };
        yield return new object?[] { "\\p{IsOriya}", 512 };
        yield return new object?[] { "\\P{IsOriya}", 512 };
        yield return new object?[] { "\\p{IsPhoneticExtensions}", 512 };
        yield return new object?[] { "\\P{IsPhoneticExtensions}", 512 };
        yield return new object?[] { "\\p{IsPrivateUse}", 512 };
        yield return new object?[] { "\\P{IsPrivateUse}", 512 };
        yield return new object?[] { "\\p{IsPrivateUseArea}", 512 };
        yield return new object?[] { "\\P{IsPrivateUseArea}", 512 };
        yield return new object?[] { "\\p{IsRunic}", 512 };
        yield return new object?[] { "\\P{IsRunic}", 512 };
        yield return new object?[] { "\\p{IsSinhala}", 512 };
        yield return new object?[] { "\\P{IsSinhala}", 512 };
        yield return new object?[] { "\\p{IsSmallFormVariants}", 512 };
        yield return new object?[] { "\\P{IsSmallFormVariants}", 512 };
        yield return new object?[] { "\\p{IsSpacingModifierLetters}", 512 };
        yield return new object?[] { "\\P{IsSpacingModifierLetters}", 512 };
        yield return new object?[] { "\\p{IsSpecials}", 512 };
        yield return new object?[] { "\\P{IsSpecials}", 512 };
        yield return new object?[] { "\\p{IsSuperscriptsandSubscripts}", 512 };
        yield return new object?[] { "\\P{IsSuperscriptsandSubscripts}", 512 };
        yield return new object?[] { "\\p{IsSupplementalArrows-A}", 512 };
        yield return new object?[] { "\\P{IsSupplementalArrows-A}", 512 };
        yield return new object?[] { "\\p{IsSupplementalArrows-B}", 512 };
        yield return new object?[] { "\\P{IsSupplementalArrows-B}", 512 };
        yield return new object?[] { "\\p{IsSupplementalMathematicalOperators}", 512 };
        yield return new object?[] { "\\P{IsSupplementalMathematicalOperators}", 512 };
        yield return new object?[] { "\\p{IsSyriac}", 512 };
        yield return new object?[] { "\\P{IsSyriac}", 512 };
        yield return new object?[] { "\\p{IsTagalog}", 512 };
        yield return new object?[] { "\\P{IsTagalog}", 512 };
        yield return new object?[] { "\\p{IsTagbanwa}", 512 };
        yield return new object?[] { "\\P{IsTagbanwa}", 512 };
        yield return new object?[] { "\\p{IsTaiLe}", 512 };
        yield return new object?[] { "\\P{IsTaiLe}", 512 };
        yield return new object?[] { "\\p{IsTamil}", 512 };
        yield return new object?[] { "\\P{IsTamil}", 512 };
        yield return new object?[] { "\\p{IsTelugu}", 512 };
        yield return new object?[] { "\\P{IsTelugu}", 512 };
        yield return new object?[] { "\\p{IsThaana}", 512 };
        yield return new object?[] { "\\P{IsThaana}", 512 };
        yield return new object?[] { "\\p{IsThai}", 512 };
        yield return new object?[] { "\\P{IsThai}", 512 };
        yield return new object?[] { "\\p{IsTibetan}", 512 };
        yield return new object?[] { "\\P{IsTibetan}", 512 };
        yield return new object?[] { "\\p{IsUnifiedCanadianAboriginalSyllabics}", 512 };
        yield return new object?[] { "\\P{IsUnifiedCanadianAboriginalSyllabics}", 512 };
        yield return new object?[] { "\\p{IsVariationSelectors}", 512 };
        yield return new object?[] { "\\P{IsVariationSelectors}", 512 };
        yield return new object?[] { "\\p{IsYijingHexagramSymbols}", 512 };
        yield return new object?[] { "\\P{IsYijingHexagramSymbols}", 512 };
        yield return new object?[] { "\\p{IsYiRadicals}", 512 };
        yield return new object?[] { "\\P{IsYiRadicals}", 512 };
        yield return new object?[] { "\\p{IsYiSyllables}", 512 };
        yield return new object?[] { "\\P{IsYiSyllables}", 512 };
    }

    [Theory]
    [MemberData(nameof(BlockResidualCases))]
    public void OwnedBlockAcceptsWhileLegacyDeclines(string pattern, int options)
    {
        var actual = Utf8OwnedTreeParser.TryParse(pattern, (RegexOptions)options);
        Assert.NotNull(actual);
        Assert.Null(Utf8RuntimeTreeProvider.TryParseLegacy(pattern, (RegexOptions)options));
    }

    [Theory]
    [MemberData(nameof(ShorthandDeclineCases))]
    public void OwnedShorthandDeclinesInvalidProperty(string pattern)
    {
        Assert.Null(Utf8OwnedTreeParser.TryParse(pattern, RegexOptions.CultureInvariant));
        Assert.Throws<RuntimeFrontEnd.RegexParseException>(() => RuntimeFrontEnd.RegexParser.Parse(
            pattern,
            RegexOptions.CultureInvariant,
            RuntimeFrontEnd.RegexParser.GetTargetCulture(RegexOptions.CultureInvariant)));
    }

    public static IEnumerable<object?[]> ShorthandResidualCases()
    {
        // Fork-widening debt (no-vendor rule): BCL rejects a shorthand or category
        // escape in upper-bound class position while the pristine fork accepts it. These pin
        // owned-decline plus legacy-accept; the provider gatekeeper still declines them so
        // construction fails BCL-exactly through the fallback.
        yield return new object?[] { "[a-\\d]" };
        yield return new object?[] { "[z-\\d]" };
        yield return new object?[] { "[--\\d]" };
        yield return new object?[] { "[a-\\p{Lu}]" };
    }

    public static IEnumerable<object?[]> UnknownPropertyCases()
    {
        // Fork-leak debt (no-vendor rule): the pristine fork leaks a bare ArgumentException
        // for unknown property names where BCL raises a parse error. These pin owned-decline
        // plus the exact leak; the provider seam maps it to a decline so construction still
        // fails BCL-exactly through the fallback.
        yield return new object?[] { "\\p{Nope}" };
        yield return new object?[] { "\\p{Any}" };
        yield return new object?[] { "\\p{Assigned}" };
        yield return new object?[] { "\\p{lu}" };
        yield return new object?[] { "\\p{LU}" };
        yield return new object?[] { "\\p{ L}" };
        yield return new object?[] { "\\p{L }" };
        yield return new object?[] { "\\p{Greek}" };
        yield return new object?[] { "\\p{isgreek}" };
    }

    [Theory]
    [MemberData(nameof(ShorthandResidualCases))]
    public void OwnedShorthandDeclinesResidual(string pattern)
    {
        Assert.Null(Utf8OwnedTreeParser.TryParse(pattern, RegexOptions.CultureInvariant));
        Assert.NotNull(Utf8RuntimeTreeProvider.TryParseLegacy(pattern, RegexOptions.CultureInvariant));
    }

    [Theory]
    [MemberData(nameof(UnknownPropertyCases))]
    public void OwnedShorthandDeclinesUnknownProperty(string pattern)
    {
        Assert.Null(Utf8OwnedTreeParser.TryParse(pattern, RegexOptions.CultureInvariant));
        Assert.Null(Utf8RuntimeTreeProvider.TryParseLegacy(pattern, RegexOptions.CultureInvariant));
        var leak = Assert.Throws<ArgumentException>(() => RuntimeFrontEnd.RegexParser.Parse(
            pattern,
            RegexOptions.CultureInvariant,
            RuntimeFrontEnd.RegexParser.GetTargetCulture(RegexOptions.CultureInvariant)));
        Assert.Equal("categoryName", leak.ParamName);
    }

    private static bool TreesEqual(RuntimeFrontEnd.RegexNode left, Utf8TreeNode right)
    {
        return (int)right.Kind == (int)left.Kind &&
            right.Options == left.Options &&
            right.Ch == left.Ch &&
            right.Str == left.Str &&
            right.Str2 == left.Str2 &&
            right.M == left.M &&
            right.N == left.N &&
            right.ChildCount == left.ChildCount &&
            Enumerable.Range(0, left.ChildCount).All(index => TreesEqual(left.Child(index), right.Child(index)));
    }
}