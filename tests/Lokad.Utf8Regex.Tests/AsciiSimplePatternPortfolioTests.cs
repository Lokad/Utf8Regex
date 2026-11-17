using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Tests;

public sealed class AsciiSimplePatternPortfolioTests
{
    [Fact]
    public void IdentifierValidatorPlanCountsAsWholeInputCompiledSpecialization()
    {
        var regex = new Utf8Regex("^[a-z][a-z0-9_]*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

        Assert.True(regex.Inspection.SimplePatternPlan.AnchoredValidatorPlan.HasValue);
        Assert.Equal(Utf8CompiledPatternCategory.AnchoredWhole, regex.Inspection.PreparedRegex.CompiledPatternCategory);
    }

    [Fact]
    public void BoundedDatePlanCountsAsWholeInputCompiledSpecialization()
    {
        var regex = new Utf8Regex(@"^[0-9]{1,2}/[0-9]{1,2}/[0-9]{2,4}$", RegexOptions.CultureInvariant | RegexOptions.Compiled);

        Assert.True(regex.Inspection.SimplePatternPlan.AnchoredBoundedDatePlan.HasValue);
        Assert.Equal(Utf8CompiledPatternCategory.AnchoredWhole, regex.Inspection.PreparedRegex.CompiledPatternCategory);
    }

    [Theory]
    [InlineData(RegexOptions.IgnoreCase)]
    [InlineData(RegexOptions.ECMAScript)]
    public void BoundedSuffixOptionsKeepWhitespaceAndCaseSemantics(RegexOptions options)
    {
        const string pattern = @"\s[a-zA-Z]{0,12}ing\s";
        var regex = new Utf8Regex(pattern, options);
        foreach (var text in new[] { "\u2003sing\u00a0", " SING ", " singing ", " xing\n", "é sing Ω" })
        {
            var input = System.Text.Encoding.UTF8.GetBytes(text);
            Assert.Equal(Regex.IsMatch(text, pattern, options), regex.IsMatch(input));
            Assert.Equal(Regex.Count(text, pattern, options), regex.Count(input));
        }
    }
}
