using System.Globalization;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.FrontEnd;

namespace Lokad.Utf8Regex.Tests;

// Owned construction-prelude checks: the effective construction culture and
// the leading-global-option normalization pin hardcoded expectations, since cache
// identity, the ASCII case-fold gate, owned tree metadata, and the execution
// pattern all flow through them. The rows below enumerate shapes (edge groups,
// unknown letters, bare
// and unterminated groups, dash runs, colon groups, non-leading groups).
public sealed class Utf8OwnedOptionPreludeTests
{
    public static IEnumerable<object?[]> NormalizeCases()
    {
        yield return new object?[] { "", 512, "", 512 };
        yield return new object?[] { "a", 512, "a", 512 };
        yield return new object?[] { "(?i)", 512, "", 513 };
        yield return new object?[] { "(?i)a", 512, "a", 513 };
        yield return new object?[] { "(?i-m)a", 512, "a", 513 };
        yield return new object?[] { "(?i)(?m)x", 512, "x", 515 };
        yield return new object?[] { "(?i)(?-m)x", 512, "x", 513 };
        yield return new object?[] { "x(?i)", 512, "x(?i)", 512 };
        yield return new object?[] { "(?i", 512, "(?i", 512 };
        yield return new object?[] { "(?", 512, "(?", 512 };
        yield return new object?[] { "(?i))a", 512, ")a", 513 };
        yield return new object?[] { "(?i--m)a", 512, "a", 513 };
        yield return new object?[] { "(?z)a", 512, "(?z)a", 512 };
        yield return new object?[] { "(?I)a", 512, "(?I)a", 512 };
        yield return new object?[] { "(?d)a", 512, "(?d)a", 512 };
        yield return new object?[] { "(?n)ab", 512, "ab", 516 };
        yield return new object?[] { "(?x) a", 512, " a", 544 };
        yield return new object?[] { "(?s)(?m)ab", 512, "ab", 530 };
        yield return new object?[] { "(?m-i)(?s)x", 512, "x", 530 };
        yield return new object?[] { "((?i))", 512, "((?i))", 512 };
        yield return new object?[] { "(?:(?i))", 512, "(?:(?i))", 512 };
        yield return new object?[] { "(?i)a{2}", 512, "a{2}", 513 };
        yield return new object?[] { "(?-i)a", 512, "a", 512 };
        yield return new object?[] { "(?)", 512, "(?)", 512 };
        yield return new object?[] { "(?-)", 512, "(?-)", 512 };
        yield return new object?[] { "(?i)(?i)a", 512, "a", 513 };
        yield return new object?[] { "(?m)(?-i)a", 512, "a", 514 };
        yield return new object?[] { "(?x)(?x)#c", 512, "#c", 544 };
        yield return new object?[] { "(?i)ab(?m)cd", 512, "ab(?m)cd", 513 };
        yield return new object?[] { "(?i)ab", 512, "ab", 513 };
        yield return new object?[] { "(?n)(?m)ab", 512, "ab", 518 };
        yield return new object?[] { "(?-n)a", 512, "a", 512 };
        yield return new object?[] { "(?s-i-m)x", 512, "x", 528 };
        yield return new object?[] { "(?i)(?-)", 512, "(?-)", 513 };
        yield return new object?[] { "(?m-i-x)s", 512, "s", 514 };
        yield return new object?[] { "(abc)", 512, "(abc)", 512 };
        yield return new object?[] { "(?i)(-m)", 512, "(-m)", 513 };
        yield return new object?[] { "(?i-m-)a", 512, "a", 513 };
        yield return new object?[] { "(?C)a", 512, "(?C)a", 512 };
        yield return new object?[] { "(?P<n>a)", 512, "(?P<n>a)", 512 };
        yield return new object?[] { "(?<n>a)", 512, "(?<n>a)", 512 };
        yield return new object?[] { "(?i)", 0, "", 1 };
        yield return new object?[] { "(?i-m)a", 0, "a", 1 };
        yield return new object?[] { "(?i-m)a", 1, "a", 1 };
        yield return new object?[] { "(?n)ab", 0, "ab", 4 };
        yield return new object?[] { "(?i)(?m)x", 8, "x", 11 };
    }

    public static IEnumerable<object?[]> CultureCases()
    {
        yield return new object?[] { 0 };
        yield return new object?[] { 1 };
        yield return new object?[] { 2 };
        yield return new object?[] { 4 };
        yield return new object?[] { 8 };
        yield return new object?[] { 16 };
        yield return new object?[] { 32 };
        yield return new object?[] { 64 };
        yield return new object?[] { 256 };
        yield return new object?[] { 512 };
        yield return new object?[] { 513 };
        yield return new object?[] { 3 };
        yield return new object?[] { 33 };
        yield return new object?[] { 65 };
        yield return new object?[] { 257 };
        yield return new object?[] { 576 };
        yield return new object?[] { 520 };
        yield return new object?[] { 7 };
        yield return new object?[] { 1023 };
    }

    [Theory]
    [MemberData(nameof(NormalizeCases))]
    public void NormalizeLeadingGlobalOptionsMatchesPinnedFacts(string pattern, int options, string expectedPattern, int expectedOptions)
    {
        var actualOptions = (RegexOptions)options;
        var actualPattern = Utf8OwnedOptionPrelude.NormalizeLeadingGlobalOptions(pattern, ref actualOptions);
        Assert.Equal(expectedPattern, actualPattern);
        Assert.Equal((RegexOptions)expectedOptions, actualOptions);
    }

    // Failed groups (colon, unknown letter, unterminated) leave construction options
    // untouched: BCL end-to-end keeps trailing content case-sensitive after a leading
    // scope (witnessed by (?i:k)K staying miss on Kk).
    public static IEnumerable<object?[]> FailedGroupLeavesOptionsCases()
    {
        yield return new object?[] { "(?i-m:s)a", 512 };
        yield return new object?[] { "(?i-m", 512 };
        yield return new object?[] { "(?i:ab)", 512 };
        yield return new object?[] { "(?im-sx:ab)", 512 };
    }

    [Theory]
    [MemberData(nameof(FailedGroupLeavesOptionsCases))]
    public void FailedGroupsLeaveOptionsUntouched(string pattern, int options)
    {
        var ownedOptions = (RegexOptions)options;
        var ownedPattern = Utf8OwnedOptionPrelude.NormalizeLeadingGlobalOptions(pattern, ref ownedOptions);
        Assert.Equal(pattern, ownedPattern);
        Assert.Equal((RegexOptions)options, ownedOptions);
    }

    [Theory]
    [MemberData(nameof(CultureCases))]
    public void GetTargetCultureMatchesPinnedRule(int options)
    {
        var expected = ((RegexOptions)options & RegexOptions.CultureInvariant) != 0
            ? CultureInfo.InvariantCulture
            : CultureInfo.CurrentCulture;
        Assert.Same(expected, Utf8OwnedOptionPrelude.GetTargetCulture((RegexOptions)options));
    }

    [Fact]
    public void CultureInvariantSelectsInvariantCulture()
    {
        Assert.Same(CultureInfo.InvariantCulture, Utf8OwnedOptionPrelude.GetTargetCulture(RegexOptions.CultureInvariant));
    }
}