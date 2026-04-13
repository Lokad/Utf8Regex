using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex.Tests;

public sealed class AsciiSimplePatternPortfolioTests
{
    [Fact]
    public void IdentifierValidatorPlanCountsAsWholeInputCompiledSpecialization()
    {
        var regex = new Utf8Regex("^[a-z][a-z0-9_]*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

        Assert.True(regex.SimplePatternPlan.AnchoredValidatorPlan.HasValue);
        Assert.True(regex.SimplePatternPlan.HasWholeInputCompiledSpecialization);
        Assert.False(regex.SimplePatternPlan.HasSearchCompiledSpecialization);
    }

    [Fact]
    public void BoundedDatePlanCountsAsWholeInputCompiledSpecialization()
    {
        var regex = new Utf8Regex(@"^[0-9]{1,2}/[0-9]{1,2}/[0-9]{2,4}$", RegexOptions.CultureInvariant | RegexOptions.Compiled);

        Assert.True(regex.SimplePatternPlan.AnchoredBoundedDatePlan.HasValue);
        Assert.True(regex.SimplePatternPlan.HasWholeInputCompiledSpecialization);
    }

    [Fact]
    public void RepeatedDigitGroupPlanCountsAsWholeInputCompiledSpecialization()
    {
        var regex = new Utf8Regex(@"^([0-9]{4}[- ]){3}[0-9]{3,4}$", RegexOptions.CultureInvariant | RegexOptions.Compiled);

        Assert.True(regex.SimplePatternPlan.RepeatedDigitGroupPlan.HasValue);
        Assert.True(regex.SimplePatternPlan.HasWholeInputCompiledSpecialization);
    }

    [Fact]
    public void BoundedSuffixLiteralPlanCountsAsSearchCompiledSpecialization()
    {
        var regex = new Utf8Regex(@"\s[a-zA-Z]{0,12}ing\s", RegexOptions.Compiled);

        Assert.True(regex.SimplePatternPlan.BoundedSuffixLiteralPlan.HasValue);
        Assert.True(regex.SimplePatternPlan.HasSearchCompiledSpecialization);
        Assert.False(regex.SimplePatternPlan.HasWholeInputCompiledSpecialization);
    }
}
