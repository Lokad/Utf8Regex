using System.Globalization;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.FrontEnd;
using RuntimeFrontEnd = Lokad.Utf8Regex.Internal.FrontEnd.Runtime;

namespace Lokad.Utf8Regex.Tests;

// Owned construction-prelude differential: the effective construction culture and
// the leading-global-option normalization must match the fork exactly, since cache
// identity, the ASCII case-fold gate, owned tree metadata, and the execution
// pattern all flow through them. Expectations come from the fork live at run time;
// the input rows below only enumerate shapes (edge groups, unknown letters, bare
// and unterminated groups, dash runs, colon groups, non-leading groups).
public sealed class Utf8OwnedOptionPreludeTests
{
    public static IEnumerable<object?[]> NormalizeCases()
    {
        yield return new object?[] { "", 512 };
        yield return new object?[] { "a", 512 };
        yield return new object?[] { "(?i)", 512 };
        yield return new object?[] { "(?i)a", 512 };
        yield return new object?[] { "(?i-m)a", 512 };
        yield return new object?[] { "(?i-m:s)a", 512 };
        yield return new object?[] { "(?i)(?m)x", 512 };
        yield return new object?[] { "(?i)(?-m)x", 512 };
        yield return new object?[] { "x(?i)", 512 };
        yield return new object?[] { "(?i", 512 };
        yield return new object?[] { "(?", 512 };
        yield return new object?[] { "(?i))a", 512 };
        yield return new object?[] { "(?i--m)a", 512 };
        yield return new object?[] { "(?z)a", 512 };
        yield return new object?[] { "(?I)a", 512 };
        yield return new object?[] { "(?d)a", 512 };
        yield return new object?[] { "(?n)ab", 512 };
        yield return new object?[] { "(?x) a", 512 };
        yield return new object?[] { "(?s)(?m)ab", 512 };
        yield return new object?[] { "(?m-i)(?s)x", 512 };
        yield return new object?[] { "((?i))", 512 };
        yield return new object?[] { "(?:(?i))", 512 };
        yield return new object?[] { "(?i)a{2}", 512 };
        yield return new object?[] { "(?-i)a", 512 };
        yield return new object?[] { "(?i-m", 512 };
        yield return new object?[] { "(?)", 512 };
        yield return new object?[] { "(?-)", 512 };
        yield return new object?[] { "(?i:ab)", 512 };
        yield return new object?[] { "(?im-sx:ab)", 512 };
        yield return new object?[] { "(?i)(?i)a", 512 };
        yield return new object?[] { "(?m)(?-i)a", 512 };
        yield return new object?[] { "(?x)(?x)#c", 512 };
        yield return new object?[] { "(?i)ab(?m)cd", 512 };
        yield return new object?[] { "(?i)ab", 512 };
        yield return new object?[] { "(?n)(?m)ab", 512 };
        yield return new object?[] { "(?-n)a", 512 };
        yield return new object?[] { "(?s-i-m)x", 512 };
        yield return new object?[] { "(?i)(?-)", 512 };
        yield return new object?[] { "(?m-i-x)s", 512 };
        yield return new object?[] { "(abc)", 512 };
        yield return new object?[] { "(?i)(-m)", 512 };
        yield return new object?[] { "(?i-m-)a", 512 };
        yield return new object?[] { "(?C)a", 512 };
        yield return new object?[] { "(?P<n>a)", 512 };
        yield return new object?[] { "(?<n>a)", 512 };
        yield return new object?[] { "(?i)", 0 };
        yield return new object?[] { "(?i-m)a", 0 };
        yield return new object?[] { "(?i-m)a", 1 };
        yield return new object?[] { "(?n)ab", 0 };
        yield return new object?[] { "(?i)(?m)x", 8 };
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
    public void NormalizeLeadingGlobalOptionsMatchesFork(string pattern, int options)
    {
        var ownedOptions = (RegexOptions)options;
        var forkOptions = (RegexOptions)options;
        var ownedPattern = Utf8OwnedOptionPrelude.NormalizeLeadingGlobalOptions(pattern, ref ownedOptions);
        var forkPattern = RuntimeFrontEnd.RegexParser.NormalizeLeadingGlobalOptions(pattern, ref forkOptions);
        Assert.Equal(forkPattern, ownedPattern);
        Assert.Equal(forkOptions, ownedOptions);
    }

    [Theory]
    [MemberData(nameof(CultureCases))]
    public void GetTargetCultureMatchesFork(int options)
    {
        Assert.Same(
            RuntimeFrontEnd.RegexParser.GetTargetCulture((RegexOptions)options),
            Utf8OwnedOptionPrelude.GetTargetCulture((RegexOptions)options));
    }

    [Fact]
    public void CultureInvariantSelectsInvariantCulture()
    {
        Assert.Same(CultureInfo.InvariantCulture, Utf8OwnedOptionPrelude.GetTargetCulture(RegexOptions.CultureInvariant));
    }
}