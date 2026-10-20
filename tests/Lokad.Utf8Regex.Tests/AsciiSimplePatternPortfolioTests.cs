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

    [Fact]
    public void RepeatedDigitGroupPlanCountsAsWholeInputCompiledSpecialization()
    {
        var regex = new Utf8Regex(@"^([0-9]{4}[- ]){3}[0-9]{3,4}$", RegexOptions.CultureInvariant | RegexOptions.Compiled);

        Assert.True(regex.Inspection.SimplePatternPlan.RepeatedDigitGroupPlan.HasValue);
        Assert.Equal(Utf8CompiledPatternCategory.AnchoredWhole, regex.Inspection.PreparedRegex.CompiledPatternCategory);
    }

    [Fact]
    public void BoundedSuffixLiteralPlanCountsAsSearchCompiledSpecialization()
    {
        var regex = new Utf8Regex(@"\s[a-zA-Z]{0,12}ing\s", RegexOptions.Compiled);

        Assert.True(regex.Inspection.SimplePatternPlan.BoundedSuffixLiteralPlan.HasValue);
        Assert.True(regex.Inspection.SimplePatternPlan.BoundedSuffixLiteralPlan.SupportsValidatedUtf8Input);
        Assert.Equal(Utf8CompiledPatternCategory.SearchGuided, regex.Inspection.PreparedRegex.CompiledPatternCategory);
    }

    [Theory]
    [InlineData(RegexOptions.IgnoreCase)]
    [InlineData(RegexOptions.ECMAScript)]
    public void BoundedSuffixLiteralPlanDoesNotClaimUnicodeSafeExecutionForDifferentSemantics(RegexOptions options)
    {
        var regex = new Utf8Regex(@"\s[a-zA-Z]{0,12}ing\s", options);

        Assert.False(regex.Inspection.SimplePatternPlan.BoundedSuffixLiteralPlan.SupportsValidatedUtf8Input);
    }
}
