using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.FrontEnd;
using Lokad.Utf8Regex.Internal.Planning;

namespace Lokad.Utf8Regex.Tests;

public sealed class LokadPublicRouteGuardrailTests
{
    [Fact]
    public void MariomkaEmailCountStaysOnDelimitedTokenFallbackFamily()
    {
        const string pattern = @"[\w\.+-]+@[\w\.-]+\.[\w\.-]+";

        var analysis = Utf8FrontEnd.Analyze(pattern, RegexOptions.None);
        var regex = new Utf8Regex(pattern, RegexOptions.None);

        Assert.Equal(Utf8FallbackDirectFamilyKind.AsciiDelimitedTokenCount, analysis.RegexPlan.FallbackDirectFamily.Kind);
        Assert.Equal(NativeExecutionKind.FallbackRegex, regex.ExecutionKind);
        Assert.Equal(Utf8CompiledEngineKind.FallbackRegex, regex.CompiledEngineKind);
    }

    [Fact]
    public void MariomkaUriCountStaysOnStructuredTokenFallbackFamily()
    {
        const string pattern = @"[\w]+://[^/\s?#]+[^\s?#]+(?:\?[^\s#]*)?(?:#[^\s]*)?";

        var analysis = Utf8FrontEnd.Analyze(pattern, RegexOptions.None);
        var regex = new Utf8Regex(pattern, RegexOptions.None);

        Assert.Equal(Utf8FallbackDirectFamilyKind.AsciiUriToken, analysis.RegexPlan.FallbackDirectFamily.Kind);
        Assert.Equal(NativeExecutionKind.FallbackRegex, regex.ExecutionKind);
        Assert.Equal(Utf8CompiledEngineKind.FallbackRegex, regex.CompiledEngineKind);
    }

    [Fact]
    public void SherlockHolmesWindowStaysOnAsciiOrderedLiteralWindow()
    {
        var regex = new Utf8Regex(@"Holmes.{0,25}Watson|Watson.{0,25}Holmes", RegexOptions.None);

        Assert.Equal(NativeExecutionKind.AsciiOrderedLiteralWindow, regex.ExecutionKind);
        Assert.Equal(Utf8CompiledEngineKind.StructuralLinearAutomaton, regex.CompiledEngineKind);
        Assert.Equal(Utf8SearchKind.ExactAsciiLiterals, regex.SearchPlan.Kind);
    }

    [Fact]
    public void SherlockIngCountStaysOnAsciiSimplePatternInterpreter()
    {
        var regex = new Utf8Regex(@"\s[a-zA-Z]{0,12}ing\s", RegexOptions.None);

        Assert.Equal(NativeExecutionKind.AsciiSimplePattern, regex.ExecutionKind);
        Assert.Equal(Utf8CompiledEngineKind.SimplePatternInterpreter, regex.CompiledEngineKind);
        Assert.Equal(Utf8SearchKind.FixedDistanceAsciiLiteral, regex.SearchPlan.Kind);
        Assert.True(regex.RegexPlan.SimplePatternPlan.BoundedSuffixLiteralPlan.HasValue);
    }

    [Fact]
    public void SherlockWordHolmesStaysOnByteSafeLinearFallback()
    {
        var regex = new Utf8Regex(@"\w+\s+Holmes", RegexOptions.None);

        Assert.Equal(NativeExecutionKind.FallbackRegex, regex.ExecutionKind);
        Assert.Equal(Utf8CompiledEngineKind.ByteSafeLinear, regex.CompiledEngineKind);
    }

    [Fact]
    public void LeipzigNameFamilyStaysOnLiteralFamilyEngine()
    {
        var regex = new Utf8Regex("Tom|Sawyer|Huckleberry|Finn", RegexOptions.None);

        Assert.Equal(NativeExecutionKind.ExactUtf8Literals, regex.ExecutionKind);
        Assert.Equal(Utf8CompiledEngineKind.LiteralFamily, regex.CompiledEngineKind);
        Assert.Equal(Utf8SearchKind.ExactAsciiLiterals, regex.SearchPlan.Kind);
    }

    [Fact]
    public void CommonIpMatchUsesAsciiDirectFamilyForMatch()
    {
        var regex = new Utf8Regex(@"(?:(?:25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9])\.){3}(?:25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9])", RegexOptions.None);

        Assert.Equal(Utf8FallbackDirectFamilyKind.AsciiDottedDecimalQuadCount, regex.DebugFallbackDirectFamilyKind switch
        {
            nameof(Utf8FallbackDirectFamilyKind.AsciiDottedDecimalQuadCount) => Utf8FallbackDirectFamilyKind.AsciiDottedDecimalQuadCount,
            _ => throw new Xunit.Sdk.XunitException($"Unexpected direct family kind: {regex.DebugFallbackDirectFamilyKind}")
        });
        Assert.True(regex.DebugSupportsWellFormedOnlyMatch);
    }

    [Fact]
    public void CommonUriMatchUsesStructuredTokenDirectFamilyForMatch()
    {
        var regex = new Utf8Regex(@"[\w]+://[^/\s?#]+[^\s?#]+(?:\?[^\s#]*)?(?:#[^\s]*)?", RegexOptions.None);

        Assert.Equal(Utf8FallbackDirectFamilyKind.AsciiUriToken, regex.DebugFallbackDirectFamilyKind switch
        {
            nameof(Utf8FallbackDirectFamilyKind.AsciiUriToken) => Utf8FallbackDirectFamilyKind.AsciiUriToken,
            _ => throw new Xunit.Sdk.XunitException($"Unexpected direct family kind: {regex.DebugFallbackDirectFamilyKind}")
        });
        Assert.True(regex.DebugSupportsWellFormedOnlyMatch);
    }

    [Fact]
    public void CommonEmailMatchUsesAnchoredAsciiEmailWholeFamily()
    {
        var regex = new Utf8Regex(@"^([a-zA-Z0-9_\-\.]+)@((\[[0-9]{1,3}\.[0-9]{1,3}\.[0-9]{1,3}\.)|(([a-zA-Z0-9\-]+\.)+))([a-zA-Z]{2,12}|[0-9]{1,3})(\]?)$", RegexOptions.Compiled);

        Assert.Equal(Utf8FallbackDirectFamilyKind.AnchoredAsciiEmailWhole, regex.DebugFallbackDirectFamilyKind switch
        {
            nameof(Utf8FallbackDirectFamilyKind.AnchoredAsciiEmailWhole) => Utf8FallbackDirectFamilyKind.AnchoredAsciiEmailWhole,
            _ => throw new Xunit.Sdk.XunitException($"Unexpected direct family kind: {regex.DebugFallbackDirectFamilyKind}")
        });
        Assert.True(regex.DebugSupportsWellFormedOnlyMatch);
    }

    [Fact]
    public void OneNodeBacktrackingPatternUsesNegatedRunLiteralFamily()
    {
        var regex = new Utf8Regex(@"[^a]+\.[^z]+", RegexOptions.Compiled);

        Assert.Equal(Utf8FallbackDirectFamilyKind.AsciiLiteralBetweenNegatedRuns, regex.DebugFallbackDirectFamilyKind switch
        {
            nameof(Utf8FallbackDirectFamilyKind.AsciiLiteralBetweenNegatedRuns) => Utf8FallbackDirectFamilyKind.AsciiLiteralBetweenNegatedRuns,
            _ => throw new Xunit.Sdk.XunitException($"Unexpected direct family kind: {regex.DebugFallbackDirectFamilyKind}")
        });
        Assert.True(regex.DebugSupportsWellFormedOnlyMatch);
    }

    [Fact]
    public void CommonBacktrackingUsesLeadingAnyRunTrailingLiteralFamily()
    {
        var regex = new Utf8Regex(@".*(ss)", RegexOptions.None);

        Assert.Equal(Utf8FallbackDirectFamilyKind.LeadingAnyRunTrailingAsciiLiteral, regex.DebugFallbackDirectFamilyKind switch
        {
            nameof(Utf8FallbackDirectFamilyKind.LeadingAnyRunTrailingAsciiLiteral) => Utf8FallbackDirectFamilyKind.LeadingAnyRunTrailingAsciiLiteral,
            _ => throw new Xunit.Sdk.XunitException($"Unexpected direct family kind: {regex.DebugFallbackDirectFamilyKind}")
        });
        Assert.True(regex.DebugSupportsWellFormedOnlyMatch);
    }

    [Fact]
    public void LokadLexerIdentifierUsesAnchoredIdentifierPrefixFamily()
    {
        var regex = new Utf8Regex(@"\G[a-z][a-z0-9_]*", RegexOptions.Multiline | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase | RegexOptions.Compiled);

        Assert.Equal(Utf8FallbackDirectFamilyKind.AnchoredIdentifierPrefix, regex.DebugFallbackDirectFamilyKind switch
        {
            nameof(Utf8FallbackDirectFamilyKind.AnchoredIdentifierPrefix) => Utf8FallbackDirectFamilyKind.AnchoredIdentifierPrefix,
            _ => throw new Xunit.Sdk.XunitException($"Unexpected direct family kind: {regex.DebugFallbackDirectFamilyKind}")
        });
        Assert.True(regex.DebugSupportsWellFormedOnlyMatch);
    }

    [Fact]
    public void LokadHelperIdentifierUsesAsciiIdentifierTokenFamily()
    {
        var regex = new Utf8Regex(@"[a-zA-Z][a-zA-Z0-9]*", RegexOptions.Compiled);

        Assert.Equal(Utf8FallbackDirectFamilyKind.AsciiIdentifierToken, regex.DebugFallbackDirectFamilyKind switch
        {
            nameof(Utf8FallbackDirectFamilyKind.AsciiIdentifierToken) => Utf8FallbackDirectFamilyKind.AsciiIdentifierToken,
            _ => throw new Xunit.Sdk.XunitException($"Unexpected direct family kind: {regex.DebugFallbackDirectFamilyKind}")
        });
        Assert.True(regex.DebugSupportsWellFormedOnlyMatch);
    }

    [Fact]
    public void LokadStyleCellRefUsesAnchoredSpreadsheetFamily()
    {
        var regex = new Utf8Regex(@"^(?<col>[a-z])(?<row>(\d)+)$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        Assert.Equal(Utf8FallbackDirectFamilyKind.AnchoredAsciiCellReferenceWhole, regex.DebugFallbackDirectFamilyKind switch
        {
            nameof(Utf8FallbackDirectFamilyKind.AnchoredAsciiCellReferenceWhole) => Utf8FallbackDirectFamilyKind.AnchoredAsciiCellReferenceWhole,
            _ => throw new Xunit.Sdk.XunitException($"Unexpected direct family kind: {regex.DebugFallbackDirectFamilyKind}")
        });
        Assert.True(regex.DebugSupportsWellFormedOnlyMatch);
    }

    [Fact]
    public void LokadStyleRangeRefUsesAnchoredSpreadsheetFamily()
    {
        var regex = new Utf8Regex(@"^(?<col1>[a-z])(?<row1>(\d)+):?(?<col2>[a-z])(?<row2>(\d)+)$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        Assert.Equal(Utf8FallbackDirectFamilyKind.AnchoredAsciiRangeReferenceWhole, regex.DebugFallbackDirectFamilyKind switch
        {
            nameof(Utf8FallbackDirectFamilyKind.AnchoredAsciiRangeReferenceWhole) => Utf8FallbackDirectFamilyKind.AnchoredAsciiRangeReferenceWhole,
            _ => throw new Xunit.Sdk.XunitException($"Unexpected direct family kind: {regex.DebugFallbackDirectFamilyKind}")
        });
        Assert.True(regex.DebugSupportsWellFormedOnlyMatch);
    }

    [Fact]
    public void LokadLexerDocLineUsesAnchoredPrefixUntilByteFamily()
    {
        var regex = new Utf8Regex(@"\G///[^\n]*\n", RegexOptions.Multiline | RegexOptions.CultureInvariant | RegexOptions.Compiled);

        Assert.Equal(Utf8FallbackDirectFamilyKind.AnchoredPrefixUntilByte, regex.DebugFallbackDirectFamilyKind switch
        {
            nameof(Utf8FallbackDirectFamilyKind.AnchoredPrefixUntilByte) => Utf8FallbackDirectFamilyKind.AnchoredPrefixUntilByte,
            _ => throw new Xunit.Sdk.XunitException($"Unexpected direct family kind: {regex.DebugFallbackDirectFamilyKind}")
        });
    }

    [Fact]
    public void LokadDashboardUrlUsesAnchoredDigitsQueryFamily()
    {
        var regex = new Utf8Regex(@"^(?<before>.*)(https://go(\.testing)?\.lokad.com|~)(?<trigram>/[a-zA-Z0-9]+)?/d/(?<topicId>\d+)/?\?t=(?<tab>[^ ?]+)(?<rest>.*)$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        Assert.Equal(Utf8FallbackDirectFamilyKind.AnchoredAsciiDigitsQueryWhole, regex.DebugFallbackDirectFamilyKind switch
        {
            nameof(Utf8FallbackDirectFamilyKind.AnchoredAsciiDigitsQueryWhole) => Utf8FallbackDirectFamilyKind.AnchoredAsciiDigitsQueryWhole,
            _ => throw new Xunit.Sdk.XunitException($"Unexpected direct family kind: {regex.DebugFallbackDirectFamilyKind}")
        });
        Assert.True(regex.DebugSupportsWellFormedOnlyMatch);
    }

    [Fact]
    public void LokadDownloadUrlUsesAnchoredHexQueryFamily()
    {
        var regex = new Utf8Regex(@"^(?<before>.*)(https://go(\.testing)?\.lokad.com|~)(?<trigram>/[a-zA-Z0-9]+)?/gateway/BigFiles/Browse/Download\?hash=(?<hash>[a-fA-F0-9]*)(?<optPath>[?&]path=[^& \n]+)?[?&]name=(?<name>[^& ]+)(?<optPath2>[?&]path=[^& \n]+)?(?<rest>.*)$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        Assert.Equal(Utf8FallbackDirectFamilyKind.AnchoredAsciiHexQueryWhole, regex.DebugFallbackDirectFamilyKind switch
        {
            nameof(Utf8FallbackDirectFamilyKind.AnchoredAsciiHexQueryWhole) => Utf8FallbackDirectFamilyKind.AnchoredAsciiHexQueryWhole,
            _ => throw new Xunit.Sdk.XunitException($"Unexpected direct family kind: {regex.DebugFallbackDirectFamilyKind}")
        });
        Assert.True(regex.DebugSupportsWellFormedOnlyMatch);
    }

    [Fact]
    public void LokadHexColorUsesAnchoredHexColorFamily()
    {
        var regex = new Utf8Regex(@"^#?([a-f0-9]{6}|[a-f0-9]{3})$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        Assert.Equal(Utf8FallbackDirectFamilyKind.AnchoredAsciiHexColorWhole, regex.DebugFallbackDirectFamilyKind switch
        {
            nameof(Utf8FallbackDirectFamilyKind.AnchoredAsciiHexColorWhole) => Utf8FallbackDirectFamilyKind.AnchoredAsciiHexColorWhole,
            _ => throw new Xunit.Sdk.XunitException($"Unexpected direct family kind: {regex.DebugFallbackDirectFamilyKind}")
        });
        Assert.True(regex.DebugSupportsWellFormedOnlyMatch);
    }

    [Fact]
    public void MariomkaEmailCountUsesThrowIfInvalidOnlyForCompiledCount()
    {
        var regex = new Utf8Regex(@"[\w\.+-]+@[\w\.-]+\.[\w\.-]+", RegexOptions.Compiled);

        Assert.Equal(Utf8FallbackDirectFamilyKind.AsciiDelimitedTokenCount, regex.DebugFallbackDirectFamilyKind switch
        {
            nameof(Utf8FallbackDirectFamilyKind.AsciiDelimitedTokenCount) => Utf8FallbackDirectFamilyKind.AsciiDelimitedTokenCount,
            _ => throw new Xunit.Sdk.XunitException($"Unexpected direct family kind: {regex.DebugFallbackDirectFamilyKind}")
        });
        Assert.True(regex.DebugSupportsThrowIfInvalidOnlyCount);
    }

    [Fact]
    public void MariomkaUriCountUsesThrowIfInvalidOnlyForCompiledCount()
    {
        var regex = new Utf8Regex(@"[\w]+://[^/\s?#]+[^\s?#]+(?:\?[^\s#]*)?(?:#[^\s]*)?", RegexOptions.Compiled);

        Assert.Equal(Utf8FallbackDirectFamilyKind.AsciiUriToken, regex.DebugFallbackDirectFamilyKind switch
        {
            nameof(Utf8FallbackDirectFamilyKind.AsciiUriToken) => Utf8FallbackDirectFamilyKind.AsciiUriToken,
            _ => throw new Xunit.Sdk.XunitException($"Unexpected direct family kind: {regex.DebugFallbackDirectFamilyKind}")
        });
        Assert.True(regex.DebugSupportsThrowIfInvalidOnlyCount);
    }

    [Fact]
    public void CommonDateMatchUsesBoundedDateDirectFamilyForMatch()
    {
        var regex = new Utf8Regex(@"\b\d{1,2}\/\d{1,2}\/\d{2,4}\b", RegexOptions.None);

        Assert.Equal(Utf8FallbackDirectFamilyKind.AsciiBoundedDateToken, regex.DebugFallbackDirectFamilyKind switch
        {
            nameof(Utf8FallbackDirectFamilyKind.AsciiBoundedDateToken) => Utf8FallbackDirectFamilyKind.AsciiBoundedDateToken,
            _ => throw new Xunit.Sdk.XunitException($"Unexpected direct family kind: {regex.DebugFallbackDirectFamilyKind}")
        });
        Assert.True(regex.DebugSupportsWellFormedOnlyMatch);
    }

    [Fact]
    public void BoostdocsWholeDateMatchUsesNoValidationBoundedDateFastPath()
    {
        var regex = new Utf8Regex(@"^[0-9]{1,2}/[0-9]{1,2}/[0-9]{4}$", RegexOptions.Compiled);

        Assert.True(regex.DebugTryMatchWithoutValidation("12/12/2001"u8, out var match));
        Assert.True(match.Success);
    }

    [Fact]
    public void BoostdocsFloatMatchUsesNoValidationSignedDecimalFastPath()
    {
        var regex = new Utf8Regex(@"^[-+]?\d*\.?\d*$", RegexOptions.None);

        Assert.True(regex.DebugTryMatchWithoutValidation("-3.14159"u8, out var match));
        Assert.True(match.Success);
    }

    [Fact]
    public void BoostdocsCreditCardMatchUsesRepeatedDigitGroupPlan()
    {
        var regex = new Utf8Regex(@"([0-9]{4}[- ]){3}[0-9]{3,4}", RegexOptions.Compiled);

        Assert.True(regex.SimplePatternPlan.RepeatedDigitGroupPlan.HasValue);
    }

    [Fact]
    public void BoostdocsFtpLineMatchUsesAnchoredLeadingDigitsTailFamily()
    {
        var regex = new Utf8Regex(@"^([0-9]+)(\-| |$)(.*)$", RegexOptions.Compiled);

        Assert.Equal(Utf8FallbackDirectFamilyKind.AnchoredAsciiLeadingDigitsTail, regex.DebugFallbackDirectFamilyKind switch
        {
            nameof(Utf8FallbackDirectFamilyKind.AnchoredAsciiLeadingDigitsTail) => Utf8FallbackDirectFamilyKind.AnchoredAsciiLeadingDigitsTail,
            _ => throw new Xunit.Sdk.XunitException($"Unexpected direct family kind: {regex.DebugFallbackDirectFamilyKind}")
        });
        Assert.True(regex.DebugSupportsWellFormedOnlyMatch);
    }

    [Fact]
    public void LeipzigSymbolCountUsesUnicodeCategoryDirectFamily()
    {
        var regex = new Utf8Regex(@"\p{Sm}", RegexOptions.None);

        Assert.Equal(Utf8FallbackDirectFamilyKind.UnicodeCategoryCount, regex.DebugFallbackDirectFamilyKind switch
        {
            nameof(Utf8FallbackDirectFamilyKind.UnicodeCategoryCount) => Utf8FallbackDirectFamilyKind.UnicodeCategoryCount,
            _ => throw new Xunit.Sdk.XunitException($"Unexpected direct family kind: {regex.DebugFallbackDirectFamilyKind}")
        });
        Assert.False(regex.DebugSupportsWellFormedOnlyMatch);
    }
}
