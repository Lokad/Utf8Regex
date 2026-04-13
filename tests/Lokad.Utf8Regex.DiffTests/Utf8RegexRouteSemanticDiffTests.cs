using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex.DiffTests;

public sealed class Utf8RegexRouteSemanticDiffTests
{
    [Fact]
    public void ExactAsciiLiteralKeepsSemanticParityAcrossRoutes()
    {
        var parity = RegexParityContext.Create(
            "needle",
            "pre needle mid needle post",
            RegexOptions.CultureInvariant);

        parity.AssertIsMatchParity();
        parity.AssertMatchParity();
        parity.AssertCountParity();
    }

    [Fact]
    public void Utf8LiteralKeepsSemanticParityAcrossRoutes()
    {
        var parity = RegexParityContext.Create(
            "café",
            "pré café puis café",
            RegexOptions.CultureInvariant);

        parity.AssertIsMatchParity();
        parity.AssertMatchParity();
        parity.AssertCountParity();
    }

    [Fact]
    public void AsciiIgnoreCaseLiteralKeepsSemanticParityAcrossRoutes()
    {
        var parity = RegexParityContext.Create(
            "HttpClient",
            "xx httpclient yy HTTPCLIENT zz",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        parity.AssertIsMatchParity();
        parity.AssertMatchParity();
        parity.AssertCountParity();
    }

    [Fact]
    public void LiteralAlternationKeepsSemanticParityAcrossRoutes()
    {
        var parity = RegexParityContext.Create(
            "cat|horse",
            "zzhorseyy cat",
            RegexOptions.CultureInvariant);

        parity.AssertIsMatchParity();
        parity.AssertMatchParity();
        parity.AssertCountParity();
    }

    [Fact]
    public void StructuralFamilyKeepsSemanticParityAcrossRoutes()
    {
        var parity = RegexParityContext.Create(
            @"\b(?:LogError|LogWarning|LogInformation)\s*\(",
            "LogError(\"bad\"); LogDebug(\"skip\"); LogInformation (\"ok\");",
            RegexOptions.CultureInvariant);

        parity.AssertIsMatchParity();
        parity.AssertMatchParity();
        parity.AssertCountParity();
    }

    [Fact]
    public void AnchoredValidatorKeepsSemanticParityAcrossRoutes()
    {
        var parity = RegexParityContext.Create(
            @"^[A-Za-z]{1,2}[0-9][0-9A-Za-z]?[ ]?[0-9][A-Za-z]{2}$",
            "SW1A 1AA",
            RegexOptions.CultureInvariant);

        parity.AssertIsMatchParity();
        parity.AssertMatchParity();
        parity.AssertCountParity();
    }

    [Fact]
    public void TokenFamilyDateKeepsSemanticParityAcrossRoutes()
    {
        var parity = RegexParityContext.Create(
            @"\b\d{1,2}/\d{1,2}/\d{2,4}\b",
            "1/2/34 12/12/2001 x",
            RegexOptions.None);

        parity.AssertIsMatchParity();
        parity.AssertMatchParity();
        parity.AssertCountParity();
    }

    [Fact]
    public void TokenFamilyUriKeepsSemanticParityAcrossRoutes()
    {
        var parity = RegexParityContext.Create(
            @"[\w]+://[^/\s?#]+[^\s?#]+(?:\?[^\s#]*)?(?:#[^\s]*)?",
            "go://a/b?q#r ftp://x/y nope",
            RegexOptions.None);

        parity.AssertIsMatchParity();
        parity.AssertMatchParity();
        parity.AssertCountParity();
    }

    [Fact]
    public void FallbackRegexFamilyKeepsSemanticParityAcrossRoutes()
    {
        var parity = RegexParityContext.Create(
            "foo(?>bar|baz)",
            "foobaz fooqux foobar",
            RegexOptions.CultureInvariant);

        parity.AssertIsMatchParity();
        parity.AssertMatchParity();
        parity.AssertCountParity();
    }

    [Fact]
    public void ByteSafeStructuralFamilyKeepsSemanticParityAcrossRoutes()
    {
        var parity = RegexParityContext.Create(
            @"((?:ASIA|AKIA|AROA|AIDA)([A-Z0-7]{16}))",
            "préface AKIAABCDEFGHIJKLMNOP suffixe",
            RegexOptions.CultureInvariant);

        parity.AssertIsMatchParity();
        parity.AssertCountParity();
    }
}
