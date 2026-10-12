using System.Text;

using Lokad.Utf8Regex.Pcre2;

namespace Lokad.Utf8Regex.Pcre2.Tests;

// R03 Unicode properties for the PCRE2 profile: general categories keep
// their native single-token programs (UCP semantics agree with BCL on the
// pinned shapes) while named blocks delegate to the core library with BCL
// parity in both cases (caseless blocks match through core invariant equivalents;
// native PCRE2 leaves them uncompilable; unknown names keep the
// explicit None/NotSupported vocabulary). Mixed category classes decline
// slice routing through the Unicode-shorthand marker (like hybrids) and
// keep their native programs; a negated category before a dash throws at
// native construction (pre-existing divergence, verified at HEAD). The
// Core block extensions execute their scalar program under the profile
// invocation; ordinary PCRE2 categories retain native compilation.
public sealed class Pcre2SliceCategoryTests
{
    private static byte[] Utf8(string value) => Encoding.UTF8.GetBytes(value);

    private static System.Text.RegularExpressions.Regex BclIgnoreCase(string pattern)
    {
        return new System.Text.RegularExpressions.Regex(
            pattern,
            System.Text.RegularExpressions.RegexOptions.CultureInvariant | System.Text.RegularExpressions.RegexOptions.IgnoreCase,
            System.Text.RegularExpressions.Regex.InfiniteMatchTimeout);
    }

    [Theory]
    [InlineData(@"\p{IsBasicLatin}+")]
    [InlineData(@"\P{IsBasicLatin}+")]
    [InlineData(@"[\p{IsBasicLatin}a]+")]
    [InlineData(@"[^\p{IsBasicLatin}a]+")]
    [InlineData(@"\p{IsCherokee}+")]
    [InlineData(@"\p{_xmlC}+")]
    [InlineData(@"\p{_xmlD}+")]
    public void CaselessBlockShapesDelegateToCore(string pattern)
    {
        // Blocks match through invariant case equivalents on the core slice,
        // so caseless block shapes (bare or mixed) delegate to the core library
        // exactly like their case-sensitive twins, with BCL parity below.
        var regex = new Utf8Pcre2Regex(pattern, Pcre2CompileOptions.Caseless);
        Assert.Equal(
            "IsMatch=Utf8Regex, Count=Utf8Regex, Enumerate=Utf8Regex, Match=Utf8Regex, Replace=Utf8Regex",
            regex.DebugDescribeExecutionPlan());
    }

    private static System.Text.RegularExpressions.Regex Bcl(string pattern)
    {
        return new System.Text.RegularExpressions.Regex(
            pattern,
            System.Text.RegularExpressions.RegexOptions.CultureInvariant,
            System.Text.RegularExpressions.Regex.InfiniteMatchTimeout);
    }

    [Theory]
    [InlineData(@"\p{L}+")]
    [InlineData(@"\p{Lu}+")]
    [InlineData(@"\p{Cs}+")]
    [InlineData(@"\p{Cn}+")]
    [InlineData(@"\P{L}+")]
    [InlineData(@"[\p{L}]+")]
    [InlineData(@"[^\p{L}]+")]
    public void GeneralCategoriesDeclineSliceRouting(string pattern)
    {
        var regex = new Utf8Pcre2Regex(pattern, Pcre2CompileOptions.None);
        Assert.DoesNotContain("Pcre2Slice", regex.DebugDescribeExecutionPlan());
    }

    [Theory]
    [InlineData(@"\p{IsGreek}+")]
    [InlineData(@"\p{IsBasicLatin}+")]
    [InlineData(@"\p{IsHighSurrogates}+")]
    [InlineData(@"\p{_xmlC}+")]
    public void BlockShapesDelegateToCore(string pattern)
    {
        var regex = new Utf8Pcre2Regex(pattern, Pcre2CompileOptions.None);
        Assert.Equal(
            "IsMatch=Utf8Regex, Count=Utf8Regex, Enumerate=Utf8Regex, Match=Utf8Regex, Replace=Utf8Regex",
            regex.DebugDescribeExecutionPlan());
    }

    [Theory]
    [InlineData(@"[\p{L}a]+")]
    [InlineData(@"[^\p{L}a]+")]
    [InlineData(@"[\p{L}\p{Nd}_]+")]
    [InlineData(@"[\p{IsGreek}a]+")]
    [InlineData(@"[\p{L}\D]+")]
    public void MixedCategoryShapesDeclineSliceRouting(string pattern)
    {
        var regex = new Utf8Pcre2Regex(pattern, Pcre2CompileOptions.None);
        Assert.DoesNotContain("Pcre2Slice", regex.DebugDescribeExecutionPlan());
    }

    [Fact]
    public void NegatedCategoryDashShapeThrowsNative()
    {
        // Pre-existing native divergence (verified at HEAD): PCRE2 rejects
        // a dash after \P in a class while BCL reads it literally; the
        // adapter surfaces the native construction throw.
        Assert.Throws<Pcre2CompileException>(() => new Utf8Pcre2Regex(@"[\P{L}-x]+", Pcre2CompileOptions.None));
    }

    [Theory]
    [InlineData(@"\p{Greek}+")]
    [InlineData(@"\p{xx}")]
    public void UnknownNamesKeepNoneProgramVocabulary(string pattern)
    {
        var regex = new Utf8Pcre2Regex(pattern, Pcre2CompileOptions.None);
        Assert.Equal(
            "IsMatch=None, Count=None, Enumerate=None, Match=None, Replace=None",
            regex.DebugDescribeExecutionPlan());
    }

    [Theory]
    [InlineData(@"\p{Greek}+", "ab")]
    [InlineData(@"\p{xx}", "ab")]
    public void UnknownNamesThrowNotSupported(string pattern, string inputText)
    {
        var regex = new Utf8Pcre2Regex(pattern, Pcre2CompileOptions.None);
        Assert.Throws<NotSupportedException>(() => regex.IsMatch(Utf8(inputText)));
    }

    public static IEnumerable<object?[]> IsMatchCases()
    {
        yield return new object?[] { @"[\p{L}a]+", "1aA", true };
        yield return new object?[] { @"[^\p{L}a]+", "a1 b", true };
        yield return new object?[] { @"[\p{L}\p{Nd}_]+", "a1_", true };
        yield return new object?[] { @"[\p{L}\D]+", "a1", true };
        yield return new object?[] { @"[\p{L}a]+", "129", false };
        yield return new object?[] { @"\p{L}+", "a\u03B1", true };
        yield return new object?[] { @"\p{L}+", "a1", true };
        yield return new object?[] { @"\p{Ll}+", "ABC", false };
        yield return new object?[] { @"\p{Lu}+", "aAB", true };
        yield return new object?[] { @"\p{Cn}+", "a\u0378b", true };
        yield return new object?[] { @"\P{L}+", "a1 ", true };
        yield return new object?[] { @"\P{L}+", "abc", false };
        yield return new object?[] { @"\p{IsGreek}+", "\u03B1x", true };
        yield return new object?[] { @"\p{IsGreek}+", "ax", false };
        yield return new object?[] { @"\p{IsBasicLatin}+", "AB\u00C9", true };
    }

    [Theory]
    [MemberData(nameof(IsMatchCases))]
    public void InlineIsMatchMatchesOracle(string pattern, string inputText, bool expected)
    {
        Assert.Equal(expected, Bcl(pattern).IsMatch(inputText));
        var regex = new Utf8Pcre2Regex(pattern, Pcre2CompileOptions.None);
        Assert.Equal(expected, regex.IsMatch(Utf8(inputText)));
        Assert.True(regex.DebugCompiledProgram is not null, "pattern=" + pattern);
    }

    public static IEnumerable<object?[]> CountCases()
    {
        yield return new object?[] { @"[\p{L}a]+", "1aA", 1 };
        yield return new object?[] { @"[\p{IsGreek}a]+", "aAb", 1 };
        yield return new object?[] { @"\p{L}+", "a\u03B1", 1 };
        yield return new object?[] { @"\p{Lu}+", "aAB", 1 };
        yield return new object?[] { @"\P{L}+", "a1 ", 1 };
        yield return new object?[] { @"\p{IsGreek}+", "\u03B11\u03B2", 2 };
    }

    public static IEnumerable<object?[]> AstralScalarSemanticsCases()
    {
        // PCRE2 matches astral scalars whole (UCP scalar semantics) while the
        // .NET oracle tests UTF-16 halves (surrogates are Cs): the profile keeps
        // native scalar behavior here, exactly like astral dots (OwnedIsMatch
        // ServesAstralDotScalar). Each row pins both verdicts so the split is
        // deliberate, not drift.
        yield return new object?[] { @"\p{Lu}+", "\U00010400", false, true };
        yield return new object?[] { @"\p{L}+", "\U00010400", false, true };
        yield return new object?[] { @"\p{Nd}+", "\U0001D7CE", false, true };
        yield return new object?[] { @"\p{Cs}+", "\U0001D11E", true, false };
        yield return new object?[] { @"\P{Cs}+", "\U0001D11E", false, true };
        yield return new object?[] { @"[\p{Lu}a]+", "\U00010400", false, true };
        yield return new object?[] { @"\P{L}+", "\U00010400", true, false };
        yield return new object?[] { @"\P{L}+", "a\U00010400", true, false };
    }

    [Theory]
    [MemberData(nameof(AstralScalarSemanticsCases))]
    public void AstralCategoriesFollowNativeScalarSemantics(string pattern, string inputText, bool bclExpected, bool nativeExpected)
    {
        Assert.Equal(bclExpected, Bcl(pattern).IsMatch(inputText));
        var regex = new Utf8Pcre2Regex(pattern, Pcre2CompileOptions.None);
        Assert.Equal(nativeExpected, regex.IsMatch(Utf8(inputText)));
        Assert.True(regex.DebugCompiledProgram is not null, "pattern=" + pattern);
    }

    public static IEnumerable<object?[]> CaselessIsMatchCases()
    {
        yield return new object?[] { @"\p{IsBasicLatin}+", "\u212A", true };
        yield return new object?[] { @"\p{IsBasicLatin}+", "\u017F", false };
        yield return new object?[] { @"\p{IsBasicLatin}+", "a\u00E9", true };
        yield return new object?[] { @"\P{IsBasicLatin}+", "\u212A", true };
        yield return new object?[] { @"\P{IsBasicLatin}+", "k", true };
        yield return new object?[] { @"\P{IsBasicLatin}+", "abc", false };
        yield return new object?[] { @"\p{IsCherokee}+", "x\uAB70y", true };
        yield return new object?[] { @"\p{_xmlC}+", "a-", true };
        yield return new object?[] { @"\p{_xmlC}+", "\u212A\u00E9", true };
        yield return new object?[] { @"\P{_xmlC}+", "a", false };
        yield return new object?[] { @"\p{_xmlD}+", "5a", true };
        yield return new object?[] { @"\p{IsCherokee}+", "abc", false };
        yield return new object?[] { @"[\p{IsBasicLatin}a]+", "\u212A", true };
        yield return new object?[] { @"[^\p{IsBasicLatin}a]+", "k", false };
    }

    [Theory]
    [MemberData(nameof(CaselessIsMatchCases))]
    public void CaselessBlockIsMatchMatchesOracle(string pattern, string inputText, bool expected)
    {
        Assert.Equal(expected, BclIgnoreCase(pattern).IsMatch(inputText));
        var regex = new Utf8Pcre2Regex(pattern, Pcre2CompileOptions.Caseless);
        Assert.Equal(expected, regex.IsMatch(Utf8(inputText)));
        Assert.True(regex.DebugCompiledProgram is not null, "pattern=" + pattern);
    }

    public static IEnumerable<object?[]> CaselessCountCases()
    {
        yield return new object?[] { @"\p{IsBasicLatin}+", "a\u00E9B", 2 };
        yield return new object?[] { @"\P{IsBasicLatin}+", "aK\u00E9", 1 };
        yield return new object?[] { @"[\p{IsBasicLatin}a]+", "1aKB\u00E9", 1 };
        yield return new object?[] { @"[^\p{IsBasicLatin}a]+", "aKB\u00E9", 1 };
        yield return new object?[] { @"\p{IsCherokee}+", "x\u13A0\uAB70y", 1 };
        yield return new object?[] { @"\p{_xmlC}+", "a-1\u00E9", 1 };
    }

    [Theory]
    [MemberData(nameof(CaselessCountCases))]
    public void CaselessBlockCountMatchesOracle(string pattern, string inputText, int expected)
    {
        Assert.Equal(expected, BclIgnoreCase(pattern).Matches(inputText).Count);
        var regex = new Utf8Pcre2Regex(pattern, Pcre2CompileOptions.Caseless);
        Assert.Equal(expected, regex.Count(Utf8(inputText), 0));
        Assert.True(regex.DebugCompiledProgram is not null, "pattern=" + pattern);
    }

    [Theory]
    [MemberData(nameof(CountCases))]
    public void InlineCountMatchesOracle(string pattern, string inputText, int expected)
    {
        Assert.Equal(expected, Bcl(pattern).Matches(inputText).Count);
        var regex = new Utf8Pcre2Regex(pattern, Pcre2CompileOptions.None);
        Assert.Equal(expected, regex.Count(Utf8(inputText), 0));
        Assert.True(regex.DebugCompiledProgram is not null, "pattern=" + pattern);
    }

    [Theory]
    [InlineData(@"\p{L}+", "xa\u03B1", 0, 4)]
    [InlineData(@"\p{Cn}+", "xa\u0378b", 2, 4)]
    [InlineData(@"\p{IsGreek}+", "x\u03B1\u03B2y", 1, 5)]
    public void InlineFirstMatchProjectsBytes(string pattern, string inputText, int expectedStart, int expectedEnd)
    {
        var expected = Bcl(pattern).Match(inputText);
        Assert.True(expected.Success);
        Assert.Equal(expectedStart, expected.Index);
        var regex = new Utf8Pcre2Regex(pattern, Pcre2CompileOptions.None);
        var found = regex.Match(Utf8(inputText), 0);
        Assert.True(found.Success);
        Assert.Equal(expectedStart, found.StartOffsetInBytes);
        Assert.Equal(expectedEnd, found.EndOffsetInBytes);
        Assert.True(regex.DebugCompiledProgram is not null, "pattern=" + pattern);
    }

    [Fact]
    public void DelegatedDetailedProjectsBlockGroup()
    {
        const string pattern = @"(\p{IsGreek}+)";
        var expected = Bcl(pattern).Match("x\u03B1");
        Assert.True(expected.Success);
        var regex = new Utf8Pcre2Regex(pattern, Pcre2CompileOptions.None);
        var detailed = regex.MatchDetailed(Utf8("x\u03B1"), 0);
        Assert.True(detailed.Success);
        Assert.Equal("\u03B1", detailed.Value.GetValueString());
        Assert.Equal("\u03B1", detailed.GetGroup(1).GetValueString());
        Assert.True(regex.DebugCompiledProgram is not null, "pattern=" + pattern);
    }

    public static IEnumerable<object?[]> ReplaceCases()
    {
        yield return new object?[] { @"\p{L}+", "xa1", "<xa>1" };
        yield return new object?[] { @"\p{IsGreek}+", "x\u03B1y", "x<\u03B1>y" };
    }

    [Theory]
    [MemberData(nameof(ReplaceCases))]
    public void InlineReplaceMatchesOracle(string pattern, string inputText, string expected)
    {
        Assert.Equal(expected, Bcl(pattern).Replace(inputText, "<$0>"));
        var regex = new Utf8Pcre2Regex(pattern, Pcre2CompileOptions.None);
        Assert.Equal(expected, regex.ReplaceToString(Utf8(inputText), "<$0>"));
        Assert.True(regex.DebugCompiledProgram is not null, "pattern=" + pattern);
    }
}
