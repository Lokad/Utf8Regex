using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8FallbackDirectFamilyPlanTests
{
    [Fact]
    public void AnchoredHexColorFamilySupportsAsciiDefinitiveMatchButNotCountShortcuts()
    {
        var plan = Utf8FallbackDirectFamilyPlan.ForKind(
            Utf8FallbackDirectFamilyKind.AnchoredAsciiHexColorWhole,
            Utf8FallbackFindModeKind.MatchAtStart);

        Assert.True(plan.SupportsAsciiDefinitiveIsMatch);
        Assert.True(plan.SupportsDefinitiveIsMatch);
        Assert.True(plan.SupportsAsciiTryMatchWithoutValidation);
        Assert.False(plan.SupportsThrowIfInvalidOnlyCount);
        Assert.False(plan.SkipsRequiredPrefilterForCount);
    }

    [Fact]
    public void AsciiIdentifierTokenFamilySupportsDirectAsciiCountShortcuts()
    {
        var plan = Utf8FallbackDirectFamilyPlan.ForKind(
            Utf8FallbackDirectFamilyKind.AsciiIdentifierToken,
            Utf8FallbackFindModeKind.FindToken);

        Assert.True(plan.SupportsAsciiDefinitiveIsMatch);
        Assert.True(plan.SupportsAsciiTryMatchWithoutValidation);
        Assert.True(plan.SupportsThrowIfInvalidOnlyCount);
        Assert.True(plan.SkipsRequiredPrefilterForCount);
    }

    [Fact]
    public void AnchoredQuotedStringPrefixSupportsTryMatchWithoutAsciiDefinitiveMatch()
    {
        var plan = Utf8FallbackDirectFamilyPlan.ForKind(
            Utf8FallbackDirectFamilyKind.AnchoredQuotedStringPrefix,
            Utf8FallbackFindModeKind.MatchAtStart);

        Assert.False(plan.SupportsAsciiDefinitiveIsMatch);
        Assert.True(plan.SupportsDefinitiveIsMatch);
        Assert.True(plan.SupportsAsciiTryMatchWithoutValidation);
    }

    [Fact]
    public void AnchoredAsciiLiteralPrefixSupportsDefinitiveDirectMatch()
    {
        var plan = Utf8FallbackDirectFamilyPlan.ForLiteral(
            Utf8FallbackDirectFamilyKind.AnchoredAsciiLiteralPrefix,
            Utf8FallbackFindModeKind.MatchAtStart,
            "ab"u8.ToArray());

        Assert.True(plan.SupportsAsciiDefinitiveIsMatch);
        Assert.True(plan.SupportsDefinitiveIsMatch);
        Assert.True(plan.SupportsAsciiTryMatchWithoutValidation);
        Assert.False(plan.SupportsThrowIfInvalidOnlyCount);
        Assert.False(plan.SkipsRequiredPrefilterForCount);
    }
}
