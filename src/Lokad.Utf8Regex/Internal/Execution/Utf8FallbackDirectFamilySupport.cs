namespace Lokad.Utf8Regex.Internal.Execution;

internal static class Utf8FallbackDirectFamilySupport
{
    public static bool SupportsAsciiDefinitiveIsMatch(Utf8FallbackDirectFamilyKind kind)
        => Utf8FallbackDirectFamilyCategories.IsAsciiDefinitiveMatchFamily(kind);

    public static bool SupportsDefinitiveIsMatch(Utf8FallbackDirectFamilyKind kind)
        => SupportsAsciiDefinitiveIsMatch(kind) ||
        kind == Utf8FallbackDirectFamilyKind.AnchoredQuotedStringPrefix;

    public static bool SupportsNativeFallbackRoute(Utf8FallbackDirectFamilyKind kind)
        => SupportsDefinitiveIsMatch(kind) ||
        Utf8FallbackDirectFamilyCategories.IsNativeFallbackOnlyCountFamily(kind);

    public static bool SupportsThrowIfInvalidOnlyCount(Utf8FallbackDirectFamilyKind kind)
        => Utf8FallbackDirectFamilyCategories.SupportsThrowIfInvalidOnlyCount(kind);

    public static bool SkipsRequiredPrefilterForCount(Utf8FallbackDirectFamilyKind kind)
        => Utf8FallbackDirectFamilyCategories.SkipsRequiredPrefilterForCount(kind);

    public static bool SupportsAsciiTryMatchWithoutValidation(Utf8FallbackDirectFamilyKind kind)
        => SupportsAsciiDefinitiveIsMatch(kind) ||
        kind == Utf8FallbackDirectFamilyKind.AnchoredQuotedStringPrefix;
}
