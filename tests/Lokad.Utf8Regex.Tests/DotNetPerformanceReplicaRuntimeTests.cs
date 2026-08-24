using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.FrontEnd;

namespace Lokad.Utf8Regex.Tests;

public sealed class DotNetPerformanceReplicaRuntimeTests
{
    [Theory]
    [InlineData(@"[\w\.+-]+@[\w\.-]+\.[\w\.-]+", "Reach équipe@exemple.fr and ops@example.net.")]
    [InlineData(@"[\w]+://[^/\s?#]+[^\s?#]+(?:\?[^\s#]*)?(?:#[^\s]*)?", "Open δοκιμή://παράδειγμα.gr/διαδρομή and https://example.net/café.")]
    [InlineData(@"[\w]+://[^/\s?#]+[^\s?#]+(?:\?[^\s#]*)?(?:#[^\s]*)?", "Open https://example.net/path and ftp://mirror.example.org/file.")]
    [InlineData("Шерлок Холмс", "Шерлок Холмс и доктор Ватсон. Шерлок Холмс.")]
    [InlineData("夏洛克·福尔摩斯", "夏洛克·福尔摩斯和约翰华生。夏洛克·福尔摩斯。")]
    [InlineData("夏洛克", "夏洛克和约翰华生。夏洛克。")]
    [InlineData("夏洛克·福尔摩斯|约翰华生|阿德勒|雷斯垂德|莫里亚蒂教授", "夏洛克·福尔摩斯和约翰华生拜访阿德勒。")]
    public void SelectedCountKernelMatchesPublicCountOnWellFormedInput(string pattern, string input)
    {
        var inputBytes = Encoding.UTF8.GetBytes(input);
        foreach (var options in new[] { RegexOptions.None, RegexOptions.Compiled })
        {
            var regex = new Utf8Regex(pattern, options);

            Assert.True(regex.Inspection.DebugTryCountSelectedKernelWithoutValidation(inputBytes, out var selectedCount));
            Assert.True(regex.Inspection.DebugTryGetSelectedCountKernelMetrics(inputBytes, out var metrics));
            Assert.NotEqual("Unsupported", regex.Inspection.DebugSelectedCountKernelKind);
            Assert.Equal(regex.Count(inputBytes), selectedCount);
            Assert.Equal(selectedCount, metrics.Matches);
            if (metrics.Candidates.HasValue)
            {
                Assert.True(metrics.Candidates.Value >= metrics.Matches);
            }

            if (metrics.FullVerifications.HasValue)
            {
                Assert.True(metrics.Candidates.HasValue);
                Assert.InRange(
                    metrics.FullVerifications.Value,
                    metrics.Matches,
                    metrics.Candidates.GetValueOrDefault());
            }
        }
    }

    [Fact]
    public void EmailPatternClassifiesAsUtf8WordDelimitedTokenCount()
    {
        var analysis = Utf8FrontEnd.Compile(@"[\w\.+-]+@[\w\.-]+\.[\w\.-]+", RegexOptions.None);

        Assert.Equal(Utf8FallbackDirectFamilyKind.Utf8WordDelimitedTokenCount, analysis.FallbackDirectFamily.Kind);
    }

    [Fact]
    public void ExplicitAsciiEmailPatternRetainsAsciiDelimitedTokenCount()
    {
        var analysis = Utf8FrontEnd.Compile(
            @"[A-Za-z0-9_\.+-]+@[A-Za-z0-9_\.-]+\.[A-Za-z0-9_\.-]+",
            RegexOptions.None);

        Assert.Equal(Utf8FallbackDirectFamilyKind.AsciiDelimitedTokenCount, analysis.FallbackDirectFamily.Kind);
    }

    [Fact]
    public void EmailCountMatchesDotNetOnRepresentativeInput()
    {
        const string pattern = @"[\w\.+-]+@[\w\.-]+\.[\w\.-]+";
        const string input = """
Reach ops.dispatch@northwind-control.net or retry cache.sync@edge-lab.io.
Invalid samples include ops.dispatch@northwind-control and just-a-symbol@.
Escalate to audit.queue@delta-hub.example when the incident repeats.
""";

        var utf8 = new Utf8Regex(pattern, RegexOptions.None);
        var dotnet = new Regex(pattern, RegexOptions.None, Regex.InfiniteMatchTimeout);

        Assert.Equal(dotnet.Count(input), utf8.Count(Encoding.UTF8.GetBytes(input)));
    }

    [Fact]
    public void EmailCountMatchesDotNetWithAdjacentDelimiters()
    {
        const string pattern = @"[\w\.+-]+@[\w\.-]+\.[\w\.-]+";
        const string input = "alpha@example.com,beta@example.net;gamma@example without-tail";

        var utf8 = new Utf8Regex(pattern, RegexOptions.None);
        var dotnet = new Regex(pattern, RegexOptions.None, Regex.InfiniteMatchTimeout);

        Assert.Equal(dotnet.Count(input), utf8.Count(Encoding.UTF8.GetBytes(input)));
    }

    [Fact]
    public void EmailCountMatchesDotNetWithUnicodeWordCharacters()
    {
        const string pattern = @"[\w\.+-]+@[\w\.-]+\.[\w\.-]+";
        const string input = "Reach équipe@exemple.fr and δοκιμή@παράδειγμα.gr.";

        var utf8 = new Utf8Regex(pattern, RegexOptions.None);
        var dotnet = new Regex(pattern, RegexOptions.None, Regex.InfiniteMatchTimeout);

        Assert.Equal(dotnet.Count(input), utf8.Count(Encoding.UTF8.GetBytes(input)));
    }

    [Theory]
    [InlineData("e\u0301quipe@exemple.fr")]
    [InlineData("δοκιμή@παράδειγμα.gr")]
    [InlineData("𐐀@example.org")]
    [InlineData("alpha@example.org.")]
    public void Utf8WordDelimitedTokenCountMatchesDotNetWordSemantics(string input)
    {
        const string pattern = @"[\w\.+-]+@[\w\.-]+\.[\w\.-]+";

        foreach (var options in new[] { RegexOptions.None, RegexOptions.Compiled })
        {
            Assert.Equal(
                Regex.Count(input, pattern, options),
                new Utf8Regex(pattern, options).Count(Encoding.UTF8.GetBytes(input)));
        }
    }

    [Fact]
    public void SelectedCountMetricsIdentifyFusedUtf8Validation()
    {
        var regex = new Utf8Regex("夏洛克", RegexOptions.None);
        var input = Encoding.UTF8.GetBytes("夏洛克和约翰华生。夏洛克。");

        Assert.True(regex.Inspection.DebugTryGetSelectedCountKernelMetrics(input, out var metrics));
        Assert.Equal("FusedValidatedBmpThreeByte", metrics.Route);
        Assert.True(metrics.IncludesUtf8Validation);
        Assert.Equal(regex.Count(input), metrics.Matches);
    }

    [Fact]
    public void Utf8WordDelimitedTokenCountStillRejectsMalformedSuffix()
    {
        var regex = new Utf8Regex(@"[\w\.+-]+@[\w\.-]+\.[\w\.-]+", RegexOptions.Compiled);
        byte[] malformed = [.. "alpha@example.org"u8, 0xFF];

        Assert.Throws<ArgumentException>(() => regex.Count(malformed));
    }

    [Theory]
    [InlineData("ops.dispatch@northwind-control.net")]
    [InlineData("ops.dispatch@[192.168.001.]42]")]
    [InlineData("ops.dispatch@northwind-control.net\n")]
    [InlineData("ops.dispatch@northwind-control")]
    [InlineData("ops.dispatch@northwind-control.net#")]
    [InlineData("@northwind-control.net")]
    [InlineData("ops.dispatch@[192.168.001.]")]
    public void EmailIsMatchMatchesDotNetOnRepresentativeInput(string input)
    {
        const string pattern = @"^([a-zA-Z0-9_\-\.]+)@((\[[0-9]{1,3}\.[0-9]{1,3}\.[0-9]{1,3}\.)|(([a-zA-Z0-9\-]+\.)+))([a-zA-Z]{2,12}|[0-9]{1,3})(\]?)$";
        var utf8 = new Utf8Regex(pattern, RegexOptions.Compiled);
        var dotnet = new Regex(pattern, RegexOptions.None, Regex.InfiniteMatchTimeout);

        Assert.Equal(dotnet.IsMatch(input), utf8.IsMatch(Encoding.UTF8.GetBytes(input)));
    }

    [Fact]
    public void AnchoredAsciiEmailWholeExecutorMatchesRepresentativeInput()
    {
        Assert.True(Utf8AsciiEmailWholeExecutor.TryMatchWhole("ops.dispatch@northwind-control.net"u8, out var matchedLength));
        Assert.Equal("ops.dispatch@northwind-control.net".Length, matchedLength);
    }

    [Fact]
    public void UriPatternClassifiesAsUtf8UriToken()
    {
        var analysis = Utf8FrontEnd.Compile(@"[\w]+://[^/\s?#]+[^\s?#]+(?:\?[^\s#]*)?(?:#[^\s]*)?", RegexOptions.None);

        Assert.Equal(Utf8FallbackDirectFamilyKind.Utf8UriToken, analysis.FallbackDirectFamily.Kind);
        Assert.True(analysis.FallbackDirectFamily.SupportsAsciiDefinitiveIsMatch);
    }

    [Fact]
    public void UriCountMatchesDotNetOnRepresentativeInput()
    {
        const string pattern = @"[\w]+://[^/\s?#]+[^\s?#]+(?:\?[^\s#]*)?(?:#[^\s]*)?";
        const string input = """
Open https://status.example.net/overview and ftp://bulk.archive.local/drop/item-42.
Skip malformed values like http://x and proto:// only-blank.
Keep mailto://ops?bad and ssh://edge-node/control?mode=full#tail in scope.
""";

        var utf8 = new Utf8Regex(pattern, RegexOptions.None);
        var dotnet = new Regex(pattern, RegexOptions.None, Regex.InfiniteMatchTimeout);

        Assert.Equal(dotnet.Count(input), utf8.Count(Encoding.UTF8.GetBytes(input)));
    }

    [Fact]
    public void UriCountMatchesDotNetWithUnicodeWordCharacters()
    {
        const string pattern = @"[\w]+://[^/\s?#]+[^\s?#]+(?:\?[^\s#]*)?(?:#[^\s]*)?";
        const string input = "Open δοκιμή://παράδειγμα.gr/διαδρομή and https://example.net/café.";

        var utf8 = new Utf8Regex(pattern, RegexOptions.None);
        var dotnet = new Regex(pattern, RegexOptions.None, Regex.InfiniteMatchTimeout);

        Assert.Equal(dotnet.Count(input), utf8.Count(Encoding.UTF8.GetBytes(input)));
    }

    [Fact]
    public void IpPatternClassifiesAsAsciiDottedDecimalQuadCount()
    {
        var analysis = Utf8FrontEnd.Compile(@"(?:(?:25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9])\.){3}(?:25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9])", RegexOptions.None);

        Assert.Equal(Utf8FallbackDirectFamilyKind.AsciiDottedDecimalQuadCount, analysis.FallbackDirectFamily.Kind);
        Assert.True(analysis.FallbackDirectFamily.SupportsAsciiDefinitiveIsMatch);
    }

    [Fact]
    public void IpCountMatchesDotNetOnRepresentativeInput()
    {
        const string pattern = @"(?:(?:25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9])\.){3}(?:25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9])";
        const string input = """
Edge nodes 012.200.033.199 and 255.255.255.000 are valid here.
Reject 300.001.002.003 and 1.2.3.4 because the source regex does not accept one-digit octets.
Keep 019.120.111.042 and 204.020.199.088 in the final count.
""";

        var utf8 = new Utf8Regex(pattern, RegexOptions.None);
        var dotnet = new Regex(pattern, RegexOptions.None, Regex.InfiniteMatchTimeout);

        Assert.Equal(dotnet.Count(input), utf8.Count(Encoding.UTF8.GetBytes(input)));
    }

    [Fact]
    public void DatePatternClassifiesAsAsciiBoundedDateToken()
    {
        var analysis = Utf8FrontEnd.Compile(@"\b\d{1,2}\/\d{1,2}\/\d{2,4}\b", RegexOptions.None);

        Assert.Equal(Utf8FallbackDirectFamilyKind.AsciiBoundedDateToken, analysis.FallbackDirectFamily.Kind);
        Assert.True(analysis.FallbackDirectFamily.SupportsAsciiDefinitiveIsMatch);
    }

    [Fact]
    public void DashDatePatternClassifiesAsAsciiBoundedDateToken()
    {
        var analysis = Utf8FrontEnd.Compile(@"\b\d{1,2}\-\d{1,2}\-\d{2,4}\b", RegexOptions.None);

        Assert.Equal(Utf8FallbackDirectFamilyKind.AsciiBoundedDateToken, analysis.FallbackDirectFamily.Kind);
        Assert.True(analysis.FallbackDirectFamily.SupportsAsciiDefinitiveIsMatch);
    }

    [Fact]
    public void IsoDatePatternClassifiesAsAsciiBoundedDateToken()
    {
        var analysis = Utf8FrontEnd.Compile(@"\b\d{4}\-\d{1,2}\-\d{1,2}\b", RegexOptions.None);

        Assert.Equal(Utf8FallbackDirectFamilyKind.AsciiBoundedDateToken, analysis.FallbackDirectFamily.Kind);
        Assert.True(analysis.FallbackDirectFamily.SupportsAsciiDefinitiveIsMatch);
    }

    [Fact]
    public void DottedDatePatternClassifiesAsAsciiBoundedDateToken()
    {
        var analysis = Utf8FrontEnd.Compile(@"\b\d{1,2}\.\d{1,2}\.\d{2,4}\b", RegexOptions.None);

        Assert.Equal(Utf8FallbackDirectFamilyKind.AsciiBoundedDateToken, analysis.FallbackDirectFamily.Kind);
        Assert.True(analysis.FallbackDirectFamily.SupportsAsciiDefinitiveIsMatch);
    }

    [Fact]
    public void IsoDottedDatePatternClassifiesAsAsciiBoundedDateToken()
    {
        var analysis = Utf8FrontEnd.Compile(@"\b\d{4}\.\d{1,2}\.\d{1,2}\b", RegexOptions.None);

        Assert.Equal(Utf8FallbackDirectFamilyKind.AsciiBoundedDateToken, analysis.FallbackDirectFamily.Kind);
        Assert.True(analysis.FallbackDirectFamily.SupportsAsciiDefinitiveIsMatch);
    }

    [Fact]
    public void DateIsMatchMatchesDotNetOnRepresentativeInput()
    {
        const string pattern = @"\b\d{1,2}\/\d{1,2}\/\d{2,4}\b";
        const string input = "Today is 11/18/2019 and tomorrow is 11/19/2019.";
        var utf8 = new Utf8Regex(pattern, RegexOptions.None);
        var dotnet = new Regex(pattern, RegexOptions.None, Regex.InfiniteMatchTimeout);

        Assert.Equal(dotnet.IsMatch(input), utf8.IsMatch(Encoding.UTF8.GetBytes(input)));
    }

    [Fact]
    public void FloatPatternClassifiesAsAnchoredAsciiSignedDecimalWhole()
    {
        var analysis = Utf8FrontEnd.Compile(@"^[-+]?\d*\.?\d*$", RegexOptions.None);

        Assert.Equal(Utf8FallbackDirectFamilyKind.AnchoredAsciiSignedDecimalWhole, analysis.FallbackDirectFamily.Kind);
        Assert.False(analysis.FallbackDirectFamily.SupportsAsciiDefinitiveIsMatch);
        Assert.False(analysis.FallbackDirectFamily.SupportsDefinitiveIsMatch);
    }

    [Fact]
    public void FloatIsMatchMatchesDotNetOnRepresentativeAsciiInput()
    {
        const string pattern = @"^[-+]?\d*\.?\d*$";
        const string input = "-3.14159";
        var utf8 = new Utf8Regex(pattern, RegexOptions.Compiled);
        var dotnet = new Regex(pattern, RegexOptions.None, Regex.InfiniteMatchTimeout);

        Assert.Equal(dotnet.IsMatch(input), utf8.IsMatch(Encoding.UTF8.GetBytes(input)));
    }

    [Fact]
    public void FloatIsMatchFallsBackCorrectlyForNonAsciiDigits()
    {
        const string pattern = @"^[-+]?\d*\.?\d*$";
        const string input = "٣.١٤";
        var utf8 = new Utf8Regex(pattern, RegexOptions.Compiled);
        var dotnet = new Regex(pattern, RegexOptions.None, Regex.InfiniteMatchTimeout);

        Assert.Equal(dotnet.IsMatch(input), utf8.IsMatch(Encoding.UTF8.GetBytes(input)));
    }

    [Fact]
    public void FloatIsMatchMatchesDotNetAcrossShortAsciiDomain()
    {
        const string pattern = @"^[-+]?\d*\.?\d*$";
        const string alphabet = "09.+-x\n";
        var ordinary = new Utf8Regex(pattern, RegexOptions.None);
        var compiled = new Utf8Regex(pattern, RegexOptions.Compiled);
        var oracle = new Regex(pattern, RegexOptions.None, Regex.InfiniteMatchTimeout);

        for (var length = 0; length <= 5; length++)
        {
            var combinationCount = (int)Math.Pow(alphabet.Length, length);
            for (var combination = 0; combination < combinationCount; combination++)
            {
                var remaining = combination;
                var characters = new char[length];
                for (var index = 0; index < characters.Length; index++)
                {
                    characters[index] = alphabet[remaining % alphabet.Length];
                    remaining /= alphabet.Length;
                }

                var input = new string(characters);
                var bytes = Encoding.UTF8.GetBytes(input);
                var expected = oracle.IsMatch(input);
                Assert.Equal(expected, ordinary.IsMatch(bytes));
                Assert.Equal(expected, compiled.IsMatch(bytes));
            }
        }
    }

    [Theory]
    [InlineData("100- this is a line of ftp response which contains a message string")]
    [InlineData("220 Service ready")]
    [InlineData("331")]
    [InlineData("123 trailing text\n")]
    [InlineData("123-\n")]
    [InlineData("x100- invalid")]
    [InlineData("-100 invalid")]
    [InlineData("")]
    public void FtpLineIsMatchMatchesDotNetOnRepresentativeInputs(string input)
    {
        const string pattern = @"^([0-9]+)(\-| |$)(.*)$";
        var utf8 = new Utf8Regex(pattern, RegexOptions.Compiled);
        var dotnet = new Regex(pattern, RegexOptions.None, Regex.InfiniteMatchTimeout);

        Assert.Equal(dotnet.IsMatch(input), utf8.IsMatch(Encoding.UTF8.GetBytes(input)));
    }

    [Theory]
    [InlineData("This regex has the potential to be optimized further")]
    [InlineData("bbb.ccc")]
    [InlineData("a.ccc")]
    [InlineData("bbb.z")]
    [InlineData("bbb.")]
    [InlineData("bbb.ccz")]
    [InlineData("é.ccc")]
    [InlineData("bbb.é")]
    [InlineData("é.z")]
    public void OneNodeBacktrackingIsMatchMatchesDotNetOnRepresentativeInput(string input)
    {
        const string pattern = @"[^a]+\.[^z]+";
        var utf8 = new Utf8Regex(pattern, RegexOptions.Compiled);
        var dotnet = new Regex(pattern, RegexOptions.None, Regex.InfiniteMatchTimeout);

        Assert.Equal(dotnet.IsMatch(input), utf8.IsMatch(Encoding.UTF8.GetBytes(input)));
    }

    [Theory]
    [InlineData("Essential services are provided by regular exprs.")]
    [InlineData("miss")]
    [InlineData("s")]
    public void BacktrackingIsMatchMatchesDotNetOnRepresentativeInput(string input)
    {
        const string pattern = @".*(ss)";
        var utf8 = new Utf8Regex(pattern, RegexOptions.None);
        var dotnet = new Regex(pattern, RegexOptions.None, Regex.InfiniteMatchTimeout);

        Assert.Equal(dotnet.IsMatch(input), utf8.IsMatch(Encoding.UTF8.GetBytes(input)));
    }

    [Fact]
    public void BacktrackingMatchMatchesDotNetOnRepresentativeInput()
    {
        const string pattern = @".*(ss)";
        const string input = "Essential services are provided by regular exprs.";
        var utf8 = new Utf8Regex(pattern, RegexOptions.None);
        var dotnet = new Regex(pattern, RegexOptions.None, Regex.InfiniteMatchTimeout);

        var utf8Match = utf8.Match(Encoding.UTF8.GetBytes(input));
        var dotnetMatch = dotnet.Match(input);

        Assert.Equal(dotnetMatch.Success, utf8Match.Success);
        Assert.Equal(dotnetMatch.Index, utf8Match.IndexInUtf16);
        Assert.Equal(dotnetMatch.Length, utf8Match.LengthInUtf16);
    }

    [Fact]
    public void DateMissMatchesDotNetOnRepresentativeInput()
    {
        const string pattern = @"\b\d{1,2}\/\d{1,2}\/\d{2,4}\b";
        const string input = "Today is 11/18/201A and tomorrow is 11/19/201A.";
        var utf8 = new Utf8Regex(pattern, RegexOptions.None);
        var dotnet = new Regex(pattern, RegexOptions.None, Regex.InfiniteMatchTimeout);

        Assert.Equal(dotnet.IsMatch(input), utf8.IsMatch(Encoding.UTF8.GetBytes(input)));
    }

    [Fact]
    public void DashDateIsMatchMatchesDotNetOnRepresentativeInput()
    {
        const string pattern = @"\b\d{1,2}\-\d{1,2}\-\d{2,4}\b";
        const string input = "Today is 11-18-2019 and tomorrow is 11-19-2019.";
        var utf8 = new Utf8Regex(pattern, RegexOptions.None);
        var dotnet = new Regex(pattern, RegexOptions.None, Regex.InfiniteMatchTimeout);

        Assert.Equal(dotnet.IsMatch(input), utf8.IsMatch(Encoding.UTF8.GetBytes(input)));
    }

    [Fact]
    public void IsoDateIsMatchMatchesDotNetOnRepresentativeInput()
    {
        const string pattern = @"\b\d{4}\-\d{1,2}\-\d{1,2}\b";
        const string input = "Release dates include 2024-11-18 and 2025-01-09 in the ledger.";
        var utf8 = new Utf8Regex(pattern, RegexOptions.None);
        var dotnet = new Regex(pattern, RegexOptions.None, Regex.InfiniteMatchTimeout);

        Assert.Equal(dotnet.IsMatch(input), utf8.IsMatch(Encoding.UTF8.GetBytes(input)));
    }

    [Fact]
    public void DottedDateIsMatchMatchesDotNetOnRepresentativeInput()
    {
        const string pattern = @"\b\d{1,2}\.\d{1,2}\.\d{2,4}\b";
        const string input = "Today is 11.18.2019 and tomorrow is 11.19.2019.";
        var utf8 = new Utf8Regex(pattern, RegexOptions.None);
        var dotnet = new Regex(pattern, RegexOptions.None, Regex.InfiniteMatchTimeout);

        Assert.Equal(dotnet.IsMatch(input), utf8.IsMatch(Encoding.UTF8.GetBytes(input)));
    }

    [Fact]
    public void IsoDottedDateIsMatchMatchesDotNetOnRepresentativeInput()
    {
        const string pattern = @"\b\d{4}\.\d{1,2}\.\d{1,2}\b";
        const string input = "Release dates include 2024.11.18 and 2025.01.09 in the ledger.";
        var utf8 = new Utf8Regex(pattern, RegexOptions.None);
        var dotnet = new Regex(pattern, RegexOptions.None, Regex.InfiniteMatchTimeout);

        Assert.Equal(dotnet.IsMatch(input), utf8.IsMatch(Encoding.UTF8.GetBytes(input)));
    }

    [Fact]
    public void UnicodeMathSymbolPatternClassifiesAsUnicodeCategoryCount()
    {
        var analysis = Utf8FrontEnd.Compile(@"\p{Sm}", RegexOptions.None);

        Assert.Equal(Utf8FallbackDirectFamilyKind.UnicodeCategoryCount, analysis.FallbackDirectFamily.Kind);
    }

    [Fact]
    public void UnicodeMathSymbolCountMatchesDotNetOnRepresentativeInput()
    {
        const string pattern = @"\p{Sm}";
        const string input = "Rivers ∑ flow + merge ≠ divide; prices < costs but √growth > 0.";

        var utf8 = new Utf8Regex(pattern, RegexOptions.None);
        var dotnet = new Regex(pattern, RegexOptions.None, Regex.InfiniteMatchTimeout);

        Assert.Equal(dotnet.Count(input), utf8.Count(Encoding.UTF8.GetBytes(input)));
    }

    [Fact]
    public void SymmetricLiteralWindowCountMatchesDotNetOnRepresentativeInput()
    {
        const string pattern = @"Holmes.{0,25}Watson|Watson.{0,25}Holmes";
        const string input = """
Sherlock Holmes and Watson solved it.
Watson stood near Holmes.
HolmesxxxxxxxxxxxxxxxxxxxxxxxxxWatson still counts.
WatsonxxxxxxxxxxxxxxxxxxxxxxxxxxHolmes does not count.
Holmes
Watson should not count across a newline.
""";

        var utf8 = new Utf8Regex(pattern, RegexOptions.None);
        var compiled = new Utf8Regex(pattern, RegexOptions.Compiled);
        var dotnet = new Regex(pattern, RegexOptions.None, Regex.InfiniteMatchTimeout);
        var bytes = Encoding.UTF8.GetBytes(input);

        Assert.Equal(dotnet.Count(input), utf8.Count(bytes));
        Assert.Equal(dotnet.Count(input), compiled.Count(bytes));
    }

    [Fact]
    public void SymmetricLiteralWindowMatchUsesGreedyBoundedGapSemantics()
    {
        const string pattern = @"Holmes.{0,25}Watson|Watson.{0,25}Holmes";
        const string input = "Holmes x Watson y Watson";

        var compiled = new Utf8Regex(pattern, RegexOptions.Compiled);
        var bytes = Encoding.UTF8.GetBytes(input);
        var match = compiled.Match(bytes);
        var dotnetMatch = Regex.Match(input, pattern);

        Assert.True(match.Success);
        Assert.Equal(dotnetMatch.Index, match.IndexInUtf16);
        Assert.Equal(dotnetMatch.Length, match.LengthInUtf16);
        Assert.Equal(dotnetMatch.Value, Encoding.UTF8.GetString(bytes.AsSpan(match.IndexInBytes, match.LengthInBytes)));
    }

    [Theory]
    [InlineData(@"Holmes.{0,25}Watson|Watson.{0,25}Holmes", "Holmes and Watson solved it.")]
    [InlineData(@"Holmes.{0,25}Watson|Watson.{0,25}Holmes", "Watson stood near Holmes.")]
    [InlineData(@"Holmes.{0,25}Watson|Watson.{0,25}Holmes", "HolmesxxxxxxxxxxxxxxxxxxxxxxxxxWatson still counts.")]
    [InlineData(@"Holmes.{0,25}Watson|Watson.{0,25}Holmes", "WatsonxxxxxxxxxxxxxxxxxxxxxxxxxxHolmes does not count.")]
    [InlineData(@"Holmes.{0,25}Watson|Watson.{0,25}Holmes", "Holmes\nWatson should not count across a newline.")]
    [InlineData(@"Tom.{10,25}river|river.{10,25}Tom", "Tom x river and river 1234567890 Tom plus Tom 1234567890 river")]
    [InlineData(@"Tom.{10,25}river|river.{10,25}Tom", "river 1234567890 Tom and Tom 1234567890 river")]
    [InlineData(@"Tom.{10,25}river|river.{10,25}Tom", "Tomxxxxxxxxxxxxxxxxxxxxxxxxxxriver should not count.")]
    [InlineData(@"Tom.{10,25}river|river.{10,25}Tom", "Tom\n1234567890 river should not count.")]
    public void CompiledPairedOrderedWindowCountMatchesDotNetAcrossOracleCases(string pattern, string input)
    {
        var baseline = new Utf8Regex(pattern, RegexOptions.None);
        var compiled = new Utf8Regex(pattern, RegexOptions.Compiled);
        var bytes = Encoding.UTF8.GetBytes(input);
        var dotnet = new Regex(pattern, RegexOptions.None, Regex.InfiniteMatchTimeout);

        Assert.Equal(dotnet.Count(input), baseline.Count(bytes));
        Assert.Equal(dotnet.Count(input), compiled.Count(bytes));
    }
}
