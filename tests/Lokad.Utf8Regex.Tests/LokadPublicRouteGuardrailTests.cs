using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.FrontEnd;
using Lokad.Utf8Regex.Internal.Planning;

namespace Lokad.Utf8Regex.Tests;

public sealed class LokadPublicRouteGuardrailTests
{
    [Fact]
    public void MariomkaEmailCountStaysOnUtf8WordDelimitedTokenFamily()
    {
        const string pattern = @"[\w\.+-]+@[\w\.-]+\.[\w\.-]+";

        var analysis = Utf8FrontEnd.Compile(pattern, RegexOptions.None);
        var regex = new Utf8Regex(pattern, RegexOptions.None);

        Assert.Equal(Utf8FallbackDirectFamilyKind.Utf8WordDelimitedTokenCount, analysis.FallbackDirectFamily.Kind);
        Assert.Equal(NativeExecutionKind.FallbackRegex, regex.Inspection.ExecutionKind);
        Assert.Equal(Utf8CompiledEngineKind.FallbackRegex, regex.Inspection.CompiledEngineKind);
    }

    [Fact]
    public void MariomkaUriCountStaysOnStructuredTokenFallbackFamily()
    {
        const string pattern = @"[\w]+://[^/\s?#]+[^\s?#]+(?:\?[^\s#]*)?(?:#[^\s]*)?";

        var analysis = Utf8FrontEnd.Compile(pattern, RegexOptions.None);
        var regex = new Utf8Regex(pattern, RegexOptions.None);

        Assert.Equal(Utf8FallbackDirectFamilyKind.Utf8UriToken, analysis.FallbackDirectFamily.Kind);
        Assert.Equal(NativeExecutionKind.FallbackRegex, regex.Inspection.ExecutionKind);
        Assert.Equal(Utf8CompiledEngineKind.FallbackRegex, regex.Inspection.CompiledEngineKind);
    }

    [Fact]
    public void SherlockHolmesWindowStaysOnAsciiOrderedLiteralWindow()
    {
        var regex = new Utf8Regex(@"Holmes.{0,25}Watson|Watson.{0,25}Holmes", RegexOptions.None);

        Assert.Equal(NativeExecutionKind.AsciiOrderedLiteralWindow, regex.Inspection.ExecutionKind);
        Assert.Equal(Utf8CompiledEngineKind.StructuralLinearAutomaton, regex.Inspection.CompiledEngineKind);
        Assert.Equal(Utf8SearchKind.ExactAsciiLiterals, regex.Inspection.SearchPlan.Kind);
    }

    [Fact]
    public void SherlockIngCountStaysOnAsciiSimplePatternInterpreter()
    {
        var regex = new Utf8Regex(@"\s[a-zA-Z]{0,12}ing\s", RegexOptions.None);

        Assert.Equal(NativeExecutionKind.AsciiSimplePattern, regex.Inspection.ExecutionKind);
        Assert.Equal(Utf8CompiledEngineKind.SimplePatternInterpreter, regex.Inspection.CompiledEngineKind);
        Assert.Equal(Utf8SearchKind.FixedDistanceAsciiLiteral, regex.Inspection.SearchPlan.Kind);
        Assert.True(regex.Inspection.PreparedRegex.SimplePatternPlan.BoundedSuffixLiteralPlan.HasValue);
    }

    [Fact]
    public void SherlockWordHolmesStaysOnByteSafeLinearFallback()
    {
        var regex = new Utf8Regex(@"\w+\s+Holmes", RegexOptions.None);

        Assert.Equal(NativeExecutionKind.FallbackRegex, regex.Inspection.ExecutionKind);
        Assert.Equal(Utf8CompiledEngineKind.ByteSafeLinear, regex.Inspection.CompiledEngineKind);
    }

    [Fact]
    public void LeipzigNameFamilyStaysOnLiteralFamilyEngine()
    {
        var regex = new Utf8Regex("Tom|Sawyer|Huckleberry|Finn", RegexOptions.None);

        Assert.Equal(NativeExecutionKind.ExactUtf8Literals, regex.Inspection.ExecutionKind);
        Assert.Equal(Utf8CompiledEngineKind.LiteralFamily, regex.Inspection.CompiledEngineKind);
        Assert.Equal(Utf8SearchKind.ExactAsciiLiterals, regex.Inspection.SearchPlan.Kind);
    }

    [Theory]
    [InlineData(RegexOptions.None)]
    [InlineData(RegexOptions.Compiled)]
    public void CommonIpMatchUsesAsciiDirectFamilyForMatch(RegexOptions options)
    {
        const string pattern = @"(?:(?:25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9])\.){3}(?:25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9])";
        var regex = new Utf8Regex(pattern, options);
        var oracle = new Regex(pattern, options, Regex.InfiniteMatchTimeout);

        Assert.Equal(Utf8FallbackDirectFamilyKind.AsciiDottedDecimalQuadCount, regex.Inspection.DebugFallbackDirectFamilyKind switch
        {
            nameof(Utf8FallbackDirectFamilyKind.AsciiDottedDecimalQuadCount) => Utf8FallbackDirectFamilyKind.AsciiDottedDecimalQuadCount,
            _ => throw new Xunit.Sdk.XunitException($"Unexpected direct family kind: {regex.Inspection.DebugFallbackDirectFamilyKind}")
        });
        Assert.True(regex.Inspection.DebugSupportsWellFormedOnlyMatch);

        string[] inputs =
        [
            "012.200.033.199",
            "012.200.033.19A",
            "01.02.03.04",
            "1.2.3.4",
            "25.26.27.28",
            "255.249.199.000",
            "256.200.033.199",
            "999.999.999.999",
            "prefix 012.200.033.199 suffix",
            "é 012.200.033.199",
            "012.200.033.199é",
            "٠١٢.٢٠٠.٠٣٣.١٩٩",
        ];
        foreach (var input in inputs)
        {
            Assert.Equal(oracle.IsMatch(input), regex.IsMatch(System.Text.Encoding.UTF8.GetBytes(input)));
        }

        for (var value = 0; value < 300; value++)
        {
            var firstOctet = $"{value:D3}.200.033.199";
            Assert.Equal(oracle.IsMatch(firstOctet), regex.IsMatch(System.Text.Encoding.UTF8.GetBytes(firstOctet)));

            var finalOctet = $"012.200.033.{value:D3}";
            Assert.Equal(oracle.IsMatch(finalOctet), regex.IsMatch(System.Text.Encoding.UTF8.GetBytes(finalOctet)));
        }

        Assert.Throws<ArgumentException>(() => regex.IsMatch(
            [(byte)'0', (byte)'1', (byte)'2', (byte)'.', (byte)'2', (byte)'0', (byte)'0', (byte)'.', (byte)'0', (byte)'3', (byte)'3', (byte)'.', (byte)'1', (byte)'9', 0xFF]));
    }

    [Theory]
    [InlineData(RegexOptions.None)]
    [InlineData(RegexOptions.Compiled)]
    public void BoostDateMatchUsesPrioritizedBoundedDateBooleanFamily(RegexOptions options)
    {
        const string pattern = @"^\d{1,2}/\d{1,2}/\d{4}$";
        var regex = new Utf8Regex(pattern, options);

        Assert.True(regex.Inspection.SimplePatternPlan.AnchoredBoundedDatePlan.HasValue);
        Assert.Equal(
            (options & RegexOptions.Compiled) != 0,
            regex.Inspection.DebugUsesEmittedWholeMatcher);
        Assert.True(regex.Inspection.DebugTryIsMatchWithoutValidation("12/12/2001"u8, out var directMatch));
        Assert.True(directMatch);

        string[] inputs =
        [
            "1/2/2001",
            "1/12/2001",
            "12/2/2001",
            "12/12/2001",
            "12/12/2001\n",
            "12/123/2001",
            "12-12-2001",
            "١٢/١٢/٢٠٠١",
        ];
        foreach (var input in inputs)
        {
            Assert.Equal(Regex.IsMatch(input, pattern), regex.IsMatch(System.Text.Encoding.UTF8.GetBytes(input)));
        }

        Assert.Throws<ArgumentException>(() => regex.IsMatch([(byte)'1', (byte)'2', (byte)'/', 0xFF]));
    }

    [Theory]
    [InlineData(RegexOptions.None)]
    [InlineData(RegexOptions.Compiled)]
    public void CommonUriMatchUsesStructuredTokenDirectFamilyForMatch(RegexOptions options)
    {
        const string pattern = @"[\w]+://[^/\s?#]+[^\s?#]+(?:\?[^\s#]*)?(?:#[^\s]*)?";
        var regex = new Utf8Regex(pattern, options);
        var oracle = new Regex(pattern, options, Regex.InfiniteMatchTimeout);

        Assert.Equal(Utf8FallbackDirectFamilyKind.Utf8UriToken, regex.Inspection.DebugFallbackDirectFamilyKind switch
        {
            nameof(Utf8FallbackDirectFamilyKind.Utf8UriToken) => Utf8FallbackDirectFamilyKind.Utf8UriToken,
            _ => throw new Xunit.Sdk.XunitException($"Unexpected direct family kind: {regex.Inspection.DebugFallbackDirectFamilyKind}")
        });
        Assert.True(regex.Inspection.DebugSupportsWellFormedOnlyMatch);
        Assert.True(regex.Inspection.DebugTryIsMatchWithoutValidation(
            "https://example.com/path?q=1"u8,
            out var isMatch));
        Assert.True(isMatch);
        Assert.False(regex.Inspection.DebugTryIsMatchWithoutValidation(
            "https://example.com/caf\u00E9"u8,
            out _));

        string[] inputs =
        [
            "scheme://ab",
            "https://atlas.example.org/reports/export?id=42",
            "https://example.com/path#fragment",
            "https://example.com/path?q=1#fragment",
            "xx https://host/path trailing",
            "https://a https://valid.example/path",
            "prefix:noise https://valid.example/path",
            "prefix:/noise:still-no-uri",
            "https://a\rhttps://b",
            "https://ab\ttrailing",
            "https://ab\ntrailing",
            "https://ab\0rest",
            $"https://{new string('a', 33)}",
            $"https://{new string('a', 34)}",
            $"https://{new string('a', 35)}",
            "é://host.example/path",
            "é https://atlas.example/path",
            "https://hôte.example/path https://atlas.example/path",
            "not a uri é",
        ];
        foreach (var input in inputs)
        {
            Assert.Equal(oracle.IsMatch(input), regex.IsMatch(System.Text.Encoding.UTF8.GetBytes(input)));
        }

        Assert.Throws<ArgumentException>(() => regex.IsMatch(
            [(byte)'x', 0xFF, (byte)' ', (byte)'h', (byte)'t', (byte)'t', (byte)'p', (byte)'s', (byte)':', (byte)'/', (byte)'/', (byte)'a', (byte)'b']));
    }

    [Fact]
    public void CommonEmailMatchUsesAnchoredAsciiEmailWholeFamily()
    {
        var regex = new Utf8Regex(@"^([a-zA-Z0-9_\-\.]+)@((\[[0-9]{1,3}\.[0-9]{1,3}\.[0-9]{1,3}\.)|(([a-zA-Z0-9\-]+\.)+))([a-zA-Z]{2,12}|[0-9]{1,3})(\]?)$", RegexOptions.Compiled);

        Assert.Equal(Utf8FallbackDirectFamilyKind.AnchoredAsciiEmailWhole, regex.Inspection.DebugFallbackDirectFamilyKind switch
        {
            nameof(Utf8FallbackDirectFamilyKind.AnchoredAsciiEmailWhole) => Utf8FallbackDirectFamilyKind.AnchoredAsciiEmailWhole,
            _ => throw new Xunit.Sdk.XunitException($"Unexpected direct family kind: {regex.Inspection.DebugFallbackDirectFamilyKind}")
        });
        Assert.True(regex.Inspection.DebugSupportsWellFormedOnlyMatch);
    }

    [Fact]
    public void OneNodeBacktrackingPatternUsesNegatedRunLiteralFamily()
    {
        var regex = new Utf8Regex(@"[^a]+\.[^z]+", RegexOptions.Compiled);

        Assert.Equal(Utf8FallbackDirectFamilyKind.AsciiLiteralBetweenNegatedRuns, regex.Inspection.DebugFallbackDirectFamilyKind switch
        {
            nameof(Utf8FallbackDirectFamilyKind.AsciiLiteralBetweenNegatedRuns) => Utf8FallbackDirectFamilyKind.AsciiLiteralBetweenNegatedRuns,
            _ => throw new Xunit.Sdk.XunitException($"Unexpected direct family kind: {regex.Inspection.DebugFallbackDirectFamilyKind}")
        });
        Assert.True(regex.Inspection.DebugSupportsWellFormedOnlyMatch);
        Assert.True(regex.Inspection.DebugTryIsMatchWithoutValidation("bbb.ccc"u8, out var asciiMatch));
        Assert.True(asciiMatch);
        Assert.True(regex.Inspection.DebugTryIsMatchWithoutValidation("no literal"u8, out var asciiMiss));
        Assert.False(asciiMiss);
        Assert.False(regex.Inspection.DebugTryIsMatchWithoutValidation("é.ccc"u8, out _));
        Assert.Throws<ArgumentException>(() => regex.IsMatch([(byte)'b', (byte)'.', 0xFF]));
        Assert.Throws<ArgumentException>(() => regex.IsMatch([(byte)'b', (byte)'.', (byte)'c', 0xFF]));
    }

    [Fact]
    public void CommonBacktrackingUsesLeadingAnyRunTrailingLiteralFamily()
    {
        var regex = new Utf8Regex(@".*(ss)", RegexOptions.None);

        Assert.Equal(Utf8FallbackDirectFamilyKind.LeadingAnyRunTrailingAsciiLiteral, regex.Inspection.DebugFallbackDirectFamilyKind switch
        {
            nameof(Utf8FallbackDirectFamilyKind.LeadingAnyRunTrailingAsciiLiteral) => Utf8FallbackDirectFamilyKind.LeadingAnyRunTrailingAsciiLiteral,
            _ => throw new Xunit.Sdk.XunitException($"Unexpected direct family kind: {regex.Inspection.DebugFallbackDirectFamilyKind}")
        });
        Assert.True(regex.Inspection.DebugSupportsWellFormedOnlyMatch);
        Assert.True(regex.Inspection.DebugTryIsMatchWithoutValidation(
            "Essential services are provided by regular exprs."u8,
            out var isMatch));
        Assert.True(isMatch);
        Assert.True(regex.IsMatch("ss at the start"u8));
        Assert.True(regex.IsMatch("at the end ss"u8));
        Assert.True(regex.IsMatch("before a newline\nss"u8));
        Assert.True(regex.IsMatch("café ss"u8));
        Assert.False(regex.IsMatch("s\ns"u8));
        Assert.Throws<ArgumentException>(() => regex.IsMatch([(byte)'s', 0xFF, (byte)'s']));

        var noncapturing = new Utf8Regex(@".*(?:needle)", RegexOptions.Compiled);
        Assert.True(noncapturing.IsMatch("a needle in text"u8));
        Assert.False(noncapturing.IsMatch("no match"u8));
    }

    [Fact]
    public void LokadLexerIdentifierUsesAnchoredIdentifierPrefixFamily()
    {
        var regex = new Utf8Regex(@"\G[a-z][a-z0-9_]*", RegexOptions.Multiline | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase | RegexOptions.Compiled);

        Assert.Equal(Utf8FallbackDirectFamilyKind.AnchoredIdentifierPrefix, regex.Inspection.DebugFallbackDirectFamilyKind switch
        {
            nameof(Utf8FallbackDirectFamilyKind.AnchoredIdentifierPrefix) => Utf8FallbackDirectFamilyKind.AnchoredIdentifierPrefix,
            _ => throw new Xunit.Sdk.XunitException($"Unexpected direct family kind: {regex.Inspection.DebugFallbackDirectFamilyKind}")
        });
        Assert.True(regex.Inspection.DebugSupportsWellFormedOnlyMatch);
    }

    [Fact]
    public void LokadHelperIdentifierUsesAsciiIdentifierTokenFamily()
    {
        var regex = new Utf8Regex(@"[a-zA-Z][a-zA-Z0-9]*", RegexOptions.Compiled);

        Assert.Equal(Utf8FallbackDirectFamilyKind.AsciiIdentifierToken, regex.Inspection.DebugFallbackDirectFamilyKind switch
        {
            nameof(Utf8FallbackDirectFamilyKind.AsciiIdentifierToken) => Utf8FallbackDirectFamilyKind.AsciiIdentifierToken,
            _ => throw new Xunit.Sdk.XunitException($"Unexpected direct family kind: {regex.Inspection.DebugFallbackDirectFamilyKind}")
        });
        Assert.True(regex.Inspection.DebugSupportsWellFormedOnlyMatch);
    }

    [Fact]
    public void LokadStyleCellRefUsesAnchoredSpreadsheetFamily()
    {
        var regex = new Utf8Regex(@"^(?<col>[a-z])(?<row>(\d)+)$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        Assert.Equal(Utf8FallbackDirectFamilyKind.AnchoredAsciiCellReferenceWhole, regex.Inspection.DebugFallbackDirectFamilyKind switch
        {
            nameof(Utf8FallbackDirectFamilyKind.AnchoredAsciiCellReferenceWhole) => Utf8FallbackDirectFamilyKind.AnchoredAsciiCellReferenceWhole,
            _ => throw new Xunit.Sdk.XunitException($"Unexpected direct family kind: {regex.Inspection.DebugFallbackDirectFamilyKind}")
        });
        Assert.True(regex.Inspection.DebugSupportsWellFormedOnlyMatch);
    }

    [Fact]
    public void LokadStyleRangeRefUsesAnchoredSpreadsheetFamily()
    {
        var regex = new Utf8Regex(@"^(?<col1>[a-z])(?<row1>(\d)+):?(?<col2>[a-z])(?<row2>(\d)+)$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        Assert.Equal(Utf8FallbackDirectFamilyKind.AnchoredAsciiRangeReferenceWhole, regex.Inspection.DebugFallbackDirectFamilyKind switch
        {
            nameof(Utf8FallbackDirectFamilyKind.AnchoredAsciiRangeReferenceWhole) => Utf8FallbackDirectFamilyKind.AnchoredAsciiRangeReferenceWhole,
            _ => throw new Xunit.Sdk.XunitException($"Unexpected direct family kind: {regex.Inspection.DebugFallbackDirectFamilyKind}")
        });
        Assert.True(regex.Inspection.DebugSupportsWellFormedOnlyMatch);
    }

    [Fact]
    public void LokadLexerDocLineUsesAnchoredPrefixUntilByteFamily()
    {
        var regex = new Utf8Regex(@"\G///[^\n]*\n", RegexOptions.Multiline | RegexOptions.CultureInvariant | RegexOptions.Compiled);

        Assert.Equal(Utf8FallbackDirectFamilyKind.AnchoredPrefixUntilByte, regex.Inspection.DebugFallbackDirectFamilyKind switch
        {
            nameof(Utf8FallbackDirectFamilyKind.AnchoredPrefixUntilByte) => Utf8FallbackDirectFamilyKind.AnchoredPrefixUntilByte,
            _ => throw new Xunit.Sdk.XunitException($"Unexpected direct family kind: {regex.Inspection.DebugFallbackDirectFamilyKind}")
        });
    }

    [Fact]
    public void LokadDashboardUrlUsesAnchoredDigitsQueryFamily()
    {
        var regex = new Utf8Regex(@"^(?<before>.*)(https://go(\.testing)?\.lokad.com|~)(?<trigram>/[a-zA-Z0-9]+)?/d/(?<topicId>\d+)/?\?t=(?<tab>[^ ?]+)(?<rest>.*)$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        Assert.Equal(Utf8FallbackDirectFamilyKind.AnchoredAsciiDigitsQueryWhole, regex.Inspection.DebugFallbackDirectFamilyKind switch
        {
            nameof(Utf8FallbackDirectFamilyKind.AnchoredAsciiDigitsQueryWhole) => Utf8FallbackDirectFamilyKind.AnchoredAsciiDigitsQueryWhole,
            _ => throw new Xunit.Sdk.XunitException($"Unexpected direct family kind: {regex.Inspection.DebugFallbackDirectFamilyKind}")
        });
        Assert.True(regex.Inspection.DebugSupportsWellFormedOnlyMatch);
    }

    [Fact]
    public void LokadDownloadUrlUsesAnchoredHexQueryFamily()
    {
        var regex = new Utf8Regex(@"^(?<before>.*)(https://go(\.testing)?\.lokad.com|~)(?<trigram>/[a-zA-Z0-9]+)?/gateway/BigFiles/Browse/Download\?hash=(?<hash>[a-fA-F0-9]*)(?<optPath>[?&]path=[^& \n]+)?[?&]name=(?<name>[^& ]+)(?<optPath2>[?&]path=[^& \n]+)?(?<rest>.*)$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        Assert.Equal(Utf8FallbackDirectFamilyKind.AnchoredAsciiHexQueryWhole, regex.Inspection.DebugFallbackDirectFamilyKind switch
        {
            nameof(Utf8FallbackDirectFamilyKind.AnchoredAsciiHexQueryWhole) => Utf8FallbackDirectFamilyKind.AnchoredAsciiHexQueryWhole,
            _ => throw new Xunit.Sdk.XunitException($"Unexpected direct family kind: {regex.Inspection.DebugFallbackDirectFamilyKind}")
        });
        Assert.True(regex.Inspection.DebugSupportsWellFormedOnlyMatch);
    }

    [Fact]
    public void LokadHexColorUsesAnchoredHexColorFamily()
    {
        var regex = new Utf8Regex(@"^#?([a-f0-9]{6}|[a-f0-9]{3})$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        Assert.Equal(Utf8FallbackDirectFamilyKind.AnchoredAsciiHexColorWhole, regex.Inspection.DebugFallbackDirectFamilyKind switch
        {
            nameof(Utf8FallbackDirectFamilyKind.AnchoredAsciiHexColorWhole) => Utf8FallbackDirectFamilyKind.AnchoredAsciiHexColorWhole,
            _ => throw new Xunit.Sdk.XunitException($"Unexpected direct family kind: {regex.Inspection.DebugFallbackDirectFamilyKind}")
        });
        Assert.True(regex.Inspection.DebugSupportsWellFormedOnlyMatch);
    }

    [Fact]
    public void MariomkaEmailCountUsesThrowIfInvalidOnlyForCompiledCount()
    {
        var regex = new Utf8Regex(@"[\w\.+-]+@[\w\.-]+\.[\w\.-]+", RegexOptions.Compiled);

        Assert.Equal(Utf8FallbackDirectFamilyKind.Utf8WordDelimitedTokenCount, regex.Inspection.DebugFallbackDirectFamilyKind switch
        {
            nameof(Utf8FallbackDirectFamilyKind.Utf8WordDelimitedTokenCount) => Utf8FallbackDirectFamilyKind.Utf8WordDelimitedTokenCount,
            _ => throw new Xunit.Sdk.XunitException($"Unexpected direct family kind: {regex.Inspection.DebugFallbackDirectFamilyKind}")
        });
        Assert.True(regex.Inspection.DebugSupportsThrowIfInvalidOnlyCount);
    }

    [Fact]
    public void MariomkaUriCountUsesThrowIfInvalidOnlyForCompiledCount()
    {
        var regex = new Utf8Regex(@"[\w]+://[^/\s?#]+[^\s?#]+(?:\?[^\s#]*)?(?:#[^\s]*)?", RegexOptions.Compiled);

        Assert.Equal(Utf8FallbackDirectFamilyKind.Utf8UriToken, regex.Inspection.DebugFallbackDirectFamilyKind switch
        {
            nameof(Utf8FallbackDirectFamilyKind.Utf8UriToken) => Utf8FallbackDirectFamilyKind.Utf8UriToken,
            _ => throw new Xunit.Sdk.XunitException($"Unexpected direct family kind: {regex.Inspection.DebugFallbackDirectFamilyKind}")
        });
        Assert.True(regex.Inspection.DebugSupportsThrowIfInvalidOnlyCount);
    }

    [Theory]
    [InlineData(RegexOptions.None)]
    [InlineData(RegexOptions.Compiled)]
    public void CommonDateMatchUsesPrioritizedBoundedDateBooleanFamily(RegexOptions options)
    {
        const string pattern = @"\b\d{1,2}\/\d{1,2}\/\d{2,4}\b";
        var regex = new Utf8Regex(pattern, options);

        Assert.Equal(Utf8FallbackDirectFamilyKind.AsciiBoundedDateToken, regex.Inspection.DebugFallbackDirectFamilyKind switch
        {
            nameof(Utf8FallbackDirectFamilyKind.AsciiBoundedDateToken) => Utf8FallbackDirectFamilyKind.AsciiBoundedDateToken,
            _ => throw new Xunit.Sdk.XunitException($"Unexpected direct family kind: {regex.Inspection.DebugFallbackDirectFamilyKind}")
        });
        Assert.True(regex.Inspection.DebugSupportsWellFormedOnlyMatch);
        Assert.True(regex.Inspection.DebugTryMatchWithoutValidation(
            "Today is 11/18/2019."u8,
            out var match));
        Assert.True(match.Success);
        Assert.True(regex.Inspection.DebugTryIsMatchWithoutValidation(
            "Today is 11/18/2019."u8,
            out var isMatch));
        Assert.True(isMatch);
        Assert.False(regex.Inspection.DebugTryMatchWithoutValidation(
            "Today is 11/18/2019. \xC3"u8,
            out _));

        string[] inputs =
        [
            "1/2/20",
            "Today is 11/18/2019.",
            "Today is 11/18/20199.",
            "x11/18/2019",
            "é11/18/2019",
            "١١/١٨/٢٠١٩",
        ];
        foreach (var input in inputs)
        {
            Assert.Equal(Regex.IsMatch(input, pattern), regex.IsMatch(System.Text.Encoding.UTF8.GetBytes(input)));
        }

        Assert.Throws<ArgumentException>(() => regex.IsMatch(
            [(byte)'1', (byte)'1', (byte)'/', (byte)'1', (byte)'8', (byte)'/', (byte)'2', (byte)'0', (byte)'1', (byte)'9', 0xFF]));
    }

    [Theory]
    [InlineData(RegexOptions.None)]
    [InlineData(RegexOptions.Compiled)]
    public void CommonDateMatcherMatchesDotNetAcrossFieldWidthsAndBoundaries(RegexOptions options)
    {
        const string pattern = @"\b\d{1,2}\/\d{1,2}\/\d{2,4}\b";
        var regex = new Utf8Regex(pattern, options);
        var oracle = new Regex(pattern, options, Regex.InfiniteMatchTimeout);
        string[] boundaries = [string.Empty, " ", "x"];

        for (var firstWidth = 0; firstWidth <= 3; firstWidth++)
        {
            for (var secondWidth = 0; secondWidth <= 3; secondWidth++)
            {
                for (var thirdWidth = 0; thirdWidth <= 5; thirdWidth++)
                {
                    foreach (var prefix in boundaries)
                    {
                        foreach (var suffix in boundaries)
                        {
                            var input = prefix +
                                new string('1', firstWidth) + "/" +
                                new string('2', secondWidth) + "/" +
                                new string('3', thirdWidth) +
                                suffix;

                            Assert.Equal(
                                oracle.IsMatch(input),
                                regex.IsMatch(System.Text.Encoding.UTF8.GetBytes(input)));
                        }
                    }
                }
            }
        }
    }

    [Fact]
    public void BoostdocsWholeDateMatchUsesNoValidationBoundedDateFastPath()
    {
        var regex = new Utf8Regex(@"^[0-9]{1,2}/[0-9]{1,2}/[0-9]{4}$", RegexOptions.Compiled);

        Assert.True(regex.Inspection.DebugTryMatchWithoutValidation("12/12/2001"u8, out var match));
        Assert.True(match.Success);
    }

    [Theory]
    [InlineData(RegexOptions.None)]
    [InlineData(RegexOptions.Compiled)]
    public void BoostdocsPostcodeMatchPreservesOptionalShapeSemantics(RegexOptions options)
    {
        const string pattern = "^[a-zA-Z]{1,2}[0-9][0-9A-Za-z]{0,1} {0,1}[0-9][A-Za-z]{2}$";
        var regex = new Utf8Regex(pattern, options);
        Assert.True(regex.Inspection.SimplePatternPlan.AnchoredOptionalFieldPlan.HasValue);
        Assert.Equal(
            (options & RegexOptions.Compiled) != 0,
            regex.Inspection.DebugUsesEmittedWholeMatcher);

        string[] inputs =
        [
            "A01ZZ",
            "A0 1ZZ",
            "A0Z1ZZ",
            "A0Z 1ZZ",
            "AB01ZZ",
            "AB0 1ZZ",
            "AB0Z1ZZ",
            "AB0Z 1ZZ",
            "AB0Z 1ZZ\n",
            "ABC0Z 1ZZ",
            "AB0Z 1Z9",
            "éB0Z 1ZZ",
        ];
        foreach (var input in inputs)
        {
            Assert.Equal(Regex.IsMatch(input, pattern), regex.IsMatch(System.Text.Encoding.UTF8.GetBytes(input)));
        }

        Assert.Throws<ArgumentException>(() => regex.IsMatch(
            [(byte)'A', (byte)'B', (byte)'0', (byte)'Z', (byte)' ', (byte)'1', (byte)'Z', (byte)'Z', 0xFF]));
    }

    [Theory]
    [InlineData(RegexOptions.None)]
    [InlineData(RegexOptions.Compiled)]
    public void BoostdocsFloatMatchUsesNoValidationSignedDecimalFastPath(RegexOptions options)
    {
        const string pattern = @"^[-+]?\d*\.?\d*$";
        var regex = new Utf8Regex(pattern, options);

        Assert.True(regex.Inspection.DebugTryMatchWithoutValidation("-3.14159"u8, out var match));
        Assert.True(match.Success);

        string[] inputs =
        [
            "",
            "+",
            "-3.14159",
            ".5",
            "5.",
            "5\n",
            "+.\n",
            "1.2.3",
            "--1",
            "١.٥",
        ];
        foreach (var input in inputs)
        {
            Assert.Equal(Regex.IsMatch(input, pattern), regex.IsMatch(System.Text.Encoding.UTF8.GetBytes(input)));
        }

        Assert.Throws<ArgumentException>(() => regex.IsMatch([(byte)'1', (byte)'.', (byte)'5', 0xFF]));
    }

    [Theory]
    [InlineData(RegexOptions.None)]
    [InlineData(RegexOptions.Compiled)]
    public void BoostdocsCreditCardMatchUsesRepeatedDigitGroupPlan(RegexOptions options)
    {
        const string pattern = @"([0-9]{4}[- ]){3}[0-9]{3,4}";
        var regex = new Utf8Regex(pattern, options);

        Assert.True(regex.Inspection.SimplePatternPlan.RepeatedDigitGroupPlan.HasValue);
        Assert.Equal(
            (options & RegexOptions.Compiled) != 0,
            regex.Inspection.DebugUsesEmittedWholeMatcher);

        string[] inputs =
        [
            "1234-5678-1234-456",
            "1234 5678 1234 4567",
            "1234-5678 1234-456",
            "x1234-5678-1234-456y",
            "1234-5678-1234-456x",
            "1234-5678-1234-45",
            "1234-5678_1234-456",
            "1234-5678-123x-456",
            "١٢٣٤-٥٦٧٨-١٢٣٤-٤٥٦",
        ];
        foreach (var input in inputs)
        {
            Assert.Equal(Regex.IsMatch(input, pattern), regex.IsMatch(System.Text.Encoding.UTF8.GetBytes(input)));
        }

        Assert.Throws<ArgumentException>(() => regex.IsMatch(
            [(byte)'1', (byte)'2', (byte)'3', (byte)'4', (byte)'-',
                (byte)'5', (byte)'6', (byte)'7', (byte)'8', (byte)'-',
                (byte)'1', (byte)'2', (byte)'3', (byte)'4', (byte)'-',
                (byte)'4', (byte)'5', (byte)'6', 0xFF]));
    }

    [Theory]
    [InlineData(RegexOptions.None)]
    [InlineData(RegexOptions.Compiled)]
    public void BoostdocsFtpLineMatchUsesPrioritizedAnchoredLeadingDigitsTailFamily(RegexOptions options)
    {
        const string pattern = @"^([0-9]+)(\-| |$)(.*)$";
        var regex = new Utf8Regex(pattern, options);

        Assert.Equal(Utf8FallbackDirectFamilyKind.AnchoredAsciiLeadingDigitsTail, regex.Inspection.DebugFallbackDirectFamilyKind switch
        {
            nameof(Utf8FallbackDirectFamilyKind.AnchoredAsciiLeadingDigitsTail) => Utf8FallbackDirectFamilyKind.AnchoredAsciiLeadingDigitsTail,
            _ => throw new Xunit.Sdk.XunitException($"Unexpected direct family kind: {regex.Inspection.DebugFallbackDirectFamilyKind}")
        });
        Assert.True(regex.Inspection.DebugSupportsWellFormedOnlyMatch);

        string[] inputs =
        [
            "100- message",
            "100 message",
            "100",
            "100\n",
            "100-",
            "100 \n",
            "100_x",
            "100-\nmore",
            "é100-message",
            "100-é",
        ];
        foreach (var input in inputs)
        {
            Assert.Equal(Regex.IsMatch(input, pattern), regex.IsMatch(System.Text.Encoding.UTF8.GetBytes(input)));
        }

        Assert.Throws<ArgumentException>(() => regex.IsMatch(
            [(byte)'1', (byte)'0', (byte)'0', (byte)'-', 0xFF]));
    }

    [Fact]
    public void LeipzigSymbolCountUsesUnicodeCategoryDirectFamily()
    {
        var regex = new Utf8Regex(@"\p{Sm}", RegexOptions.None);

        Assert.Equal(Utf8FallbackDirectFamilyKind.UnicodeCategoryCount, regex.Inspection.DebugFallbackDirectFamilyKind switch
        {
            nameof(Utf8FallbackDirectFamilyKind.UnicodeCategoryCount) => Utf8FallbackDirectFamilyKind.UnicodeCategoryCount,
            _ => throw new Xunit.Sdk.XunitException($"Unexpected direct family kind: {regex.Inspection.DebugFallbackDirectFamilyKind}")
        });
        Assert.False(regex.Inspection.DebugSupportsWellFormedOnlyMatch);
    }
}
