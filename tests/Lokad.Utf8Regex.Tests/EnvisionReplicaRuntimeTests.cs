using System.Text;
using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex.Tests;

public sealed class EnvisionReplicaRuntimeTests
{
    [Fact]
    public void ModuleImportCountMatchesDotNetOnRepresentativeInput()
    {
        const string pattern = "^import\\s+(?<shared>shared\\s+)?\\\"(?<path>(\\.|[^\\\\\\\"]*))\\\".*$";
        const string input = """
/// #region Shared modules
import "_Common" as Common
import "_Formatting" as Formatting
import shared "_Calendar" as Calendar
import shared "_Navigation" as Navigation
""";

        var utf8 = new Utf8Regex(pattern, RegexOptions.Multiline);
        var dotnet = new Regex(pattern, RegexOptions.Multiline, Regex.InfiniteMatchTimeout);

        Assert.Equal(dotnet.Count(input), utf8.Count(Encoding.UTF8.GetBytes(input)));
    }

    [Fact]
    public void ModuleImportCountMatchesDotNetWhenQuotedPathSpansLines()
    {
        const string pattern = "^import\\s+(?<shared>shared\\s+)?\\\"(?<path>(\\.|[^\\\\\\\"]*))\\\".*$";
        const string input = """
import "alpha
beta"
import shared "_Calendar" as Calendar
""";

        var utf8 = new Utf8Regex(pattern, RegexOptions.Multiline);
        var dotnet = new Regex(pattern, RegexOptions.Multiline, Regex.InfiniteMatchTimeout);

        Assert.Equal(dotnet.Count(input), utf8.Count(Encoding.UTF8.GetBytes(input)));
    }

    [Fact]
    public void ModuleImportCountFamilyHandlesEquivalentNonCapturingVariant()
    {
        const string pattern = "^import\\s+(?:shared\\s+)?\\\"(?<path>(\\.|[^\\\\\\\"]*))\\\".*$";
        const string input = """
import "_Common" as Common
import shared "_Calendar" as Calendar
""";

        var utf8 = new Utf8Regex(pattern, RegexOptions.Multiline);
        var dotnet = new Regex(pattern, RegexOptions.Multiline, Regex.InfiniteMatchTimeout);

        Assert.Equal(dotnet.Count(input), utf8.Count(Encoding.UTF8.GetBytes(input)));
    }

    [Fact]
    public void RegionMarkerCountMatchesDotNetOnRepresentativeInput()
    {
        const string pattern = @"^\s*///(?<title>#.*)$";
        const string input = """
///#region Inputs
show scalar "x" with 1

///#region Review
show scalar "y" with 2
""";

        var utf8 = new Utf8Regex(pattern, RegexOptions.Multiline | RegexOptions.NonBacktracking);
        var dotnet = new Regex(pattern, RegexOptions.Multiline | RegexOptions.NonBacktracking, Regex.InfiniteMatchTimeout);

        Assert.Equal(dotnet.Count(input), utf8.Count(Encoding.UTF8.GetBytes(input)));
    }

    [Fact]
    public void IdentifierValidatorMatchMatchesDotNetOnRepresentativeInput()
    {
        const string pattern = "^[a-z][a-z0-9_]*$";
        const string input = "module_color";

        var utf8 = new Utf8Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        var dotnet = new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, Regex.InfiniteMatchTimeout);

        var expected = dotnet.Match(input);
        var actual = utf8.Match(Encoding.UTF8.GetBytes(input));

        Assert.Equal(expected.Success, actual.Success);
        Assert.Equal(expected.Index, actual.IndexInUtf16);
        Assert.Equal(expected.Length, actual.LengthInUtf16);
    }

    [Fact]
    public void ColorShortHexMatchMatchesDotNetWithTrailingNewline()
    {
        const string pattern = "^[a-f0-9]{3}$";
        const string input = "abc\n";

        var utf8 = new Utf8Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        var dotnet = new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, Regex.InfiniteMatchTimeout);

        var expected = dotnet.Match(input);
        var actual = utf8.Match(Encoding.UTF8.GetBytes(input));

        Assert.Equal(expected.Success, actual.Success);
        Assert.Equal(expected.Index, actual.IndexInUtf16);
        Assert.Equal(expected.Length, actual.LengthInUtf16);
    }

    [Fact]
    public void DocLineMatchMatchesDotNetOnRepresentativeInput()
    {
        const string pattern = "\\G///[^\\n]*\\n";
        const string input = "/// commentaire de module\nshow table \"x\" with 1\n";

        var utf8 = new Utf8Regex(pattern, RegexOptions.Multiline | RegexOptions.CultureInvariant);
        var dotnet = new Regex(pattern, RegexOptions.Multiline | RegexOptions.CultureInvariant, Regex.InfiniteMatchTimeout);

        var expected = dotnet.Match(input);
        var actual = utf8.Match(Encoding.UTF8.GetBytes(input));

        Assert.Equal(expected.Success, actual.Success);
        Assert.Equal(expected.Index, actual.IndexInUtf16);
        Assert.Equal(expected.Length, actual.LengthInUtf16);
    }

    [Fact]
    public void LexerIdentifierMatchStopsBeforeNonIdentifierTail()
    {
        const string pattern = "\\G[a-z][a-z0-9_]*";
        const string input = "module_color;";

        var utf8 = new Utf8Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.CultureInvariant);
        var dotnet = new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.CultureInvariant, Regex.InfiniteMatchTimeout);

        var expected = dotnet.Match(input);
        var actual = utf8.Match(Encoding.UTF8.GetBytes(input));

        Assert.Equal(expected.Success, actual.Success);
        Assert.Equal(expected.Index, actual.IndexInUtf16);
        Assert.Equal(expected.Length, actual.LengthInUtf16);
    }

    [Fact]
    public void LexerIdentifierFamilyAlsoHandlesUppercaseClassVariant()
    {
        const string pattern = "\\G[A-Z][A-Z0-9_]*";
        const string input = "MODULE_COLOR;";

        var utf8 = new Utf8Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.CultureInvariant);
        var dotnet = new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.CultureInvariant, Regex.InfiniteMatchTimeout);
        
        var expected = dotnet.Match(input);
        var actual = utf8.Match(Encoding.UTF8.GetBytes(input));

        Assert.Equal(expected.Success, actual.Success);
        Assert.Equal(expected.Index, actual.IndexInUtf16);
        Assert.Equal(expected.Length, actual.LengthInUtf16);
    }

    [Fact]
    public void HexColorMatchMatchesDotNetOnRepresentativeInput()
    {
        const string pattern = "^#?([a-f0-9]{6}|[a-f0-9]{3})$";
        const string input = "#ff8800";

        var utf8 = new Utf8Regex(pattern, RegexOptions.IgnoreCase);
        var dotnet = new Regex(pattern, RegexOptions.IgnoreCase, Regex.InfiniteMatchTimeout);

        var expected = dotnet.Match(input);
        var actual = utf8.Match(Encoding.UTF8.GetBytes(input));

        Assert.Equal(expected.Success, actual.Success);
        Assert.Equal(expected.Index, actual.IndexInUtf16);
        Assert.Equal(expected.Length, actual.LengthInUtf16);
    }

    [Fact]
    public void HelperIdentifierMatchMatchesDotNetOnRepresentativeInput()
    {
        const string pattern = "[a-zA-Z][a-zA-Z0-9]*";
        const string input = "42 ColorPalette+";

        var utf8 = new Utf8Regex(pattern, RegexOptions.None);
        var dotnet = new Regex(pattern, RegexOptions.None, Regex.InfiniteMatchTimeout);

        var expected = dotnet.Match(input);
        var actual = utf8.Match(Encoding.UTF8.GetBytes(input));

        Assert.Equal(expected.Success, actual.Success);
        Assert.Equal(expected.Index, actual.IndexInUtf16);
        Assert.Equal(expected.Length, actual.LengthInUtf16);
        Assert.True(actual.IsByteAligned);
        Assert.Equal(expected.Index, actual.IndexInBytes);
        Assert.Equal(expected.Length, actual.LengthInBytes);
    }

    [Fact]
    public void IdentifierValidatorMatchMatchesDotNetWithTrailingNewline()
    {
        const string pattern = "^[a-z][a-z0-9_]*$";
        const string input = "module_color\n";

        var utf8 = new Utf8Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        var dotnet = new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, Regex.InfiniteMatchTimeout);

        var expected = dotnet.Match(input);
        var actual = utf8.Match(Encoding.UTF8.GetBytes(input));

        Assert.Equal(expected.Success, actual.Success);
        Assert.Equal(expected.Index, actual.IndexInUtf16);
        Assert.Equal(expected.Length, actual.LengthInUtf16);
    }

    [Fact]
    public void IdentifierValidatorMatchRejectsMalformedUtf8()
    {
        const string pattern = "^[a-z][a-z0-9_]*$";
        var invalid = new byte[] { (byte)'m', (byte)'o', 0xC3 };

        var utf8 = new Utf8Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        Assert.Throws<ArgumentException>(() => utf8.Match(invalid));
    }

    [Fact]
    public void LexerIdentifierIsMatchMatchesDotNetOnPrefixInput()
    {
        const string pattern = "\\G[a-z][a-z0-9_]*";
        const string input = "module_color;";

        var utf8 = new Utf8Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.CultureInvariant);
        var dotnet = new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.CultureInvariant, Regex.InfiniteMatchTimeout);

        Assert.Equal(dotnet.IsMatch(input), utf8.IsMatch(Encoding.UTF8.GetBytes(input)));
    }

    [Fact]
    public void LexerNumberMatchStopsBeforeIncompleteExponent()
    {
        const string pattern = "\\G-?[0-9]+(\\.[0-9]+)?(e[+-]?[0-9]+)?";
        const string input = "12e+";

        var utf8 = new Utf8Regex(pattern, RegexOptions.CultureInvariant);
        var dotnet = new Regex(pattern, RegexOptions.CultureInvariant, Regex.InfiniteMatchTimeout);

        var expected = dotnet.Match(input);
        var actual = utf8.Match(Encoding.UTF8.GetBytes(input));

        Assert.Equal(expected.Success, actual.Success);
        Assert.Equal(expected.Index, actual.IndexInUtf16);
        Assert.Equal(expected.Length, actual.LengthInUtf16);
    }

    [Fact]
    public void LexerOperatorRunStopsBeforeNonOperatorTail()
    {
        const string pattern = "\\G[=+\\-^*/.*<>~!&|?]+";
        const string input = ">>=value";

        var utf8 = new Utf8Regex(pattern, RegexOptions.CultureInvariant);
        var dotnet = new Regex(pattern, RegexOptions.CultureInvariant, Regex.InfiniteMatchTimeout);

        var expected = dotnet.Match(input);
        var actual = utf8.Match(Encoding.UTF8.GetBytes(input));

        Assert.Equal(expected.Success, actual.Success);
        Assert.Equal(expected.Index, actual.IndexInUtf16);
        Assert.Equal(expected.Length, actual.LengthInUtf16);
    }

    [Fact]
    public void LexerNumberFamilyHandlesNonCapturingOptionalGroups()
    {
        const string pattern = "\\G-?[0-9]+(?:\\.[0-9]+)?(?:e[+-]?[0-9]+)?";
        const string input = "12e+";

        var utf8 = new Utf8Regex(pattern, RegexOptions.CultureInvariant);
        var dotnet = new Regex(pattern, RegexOptions.CultureInvariant, Regex.InfiniteMatchTimeout);

        var expected = dotnet.Match(input);
        var actual = utf8.Match(Encoding.UTF8.GetBytes(input));

        Assert.Equal(expected.Success, actual.Success);
        Assert.Equal(expected.Index, actual.IndexInUtf16);
        Assert.Equal(expected.Length, actual.LengthInUtf16);
    }

    [Fact]
    public void LexerOperatorRunFamilyHandlesEquivalentReorderedClass()
    {
        const string pattern = "\\G[?|&!~<>.=+\\-^*/]+";
        const string input = ">>=value";

        var utf8 = new Utf8Regex(pattern, RegexOptions.CultureInvariant);
        var dotnet = new Regex(pattern, RegexOptions.CultureInvariant, Regex.InfiniteMatchTimeout);

        var expected = dotnet.Match(input);
        var actual = utf8.Match(Encoding.UTF8.GetBytes(input));

        Assert.Equal(expected.Success, actual.Success);
        Assert.Equal(expected.Index, actual.IndexInUtf16);
        Assert.Equal(expected.Length, actual.LengthInUtf16);
    }

    [Fact]
    public void LexerStringMatchHandlesEscapedQuote()
    {
        const string pattern = "\\G\"([^\"\\\\]|\\\\.)*\"";
        const string input = "\"alpha\\\"beta\" tail";

        var utf8 = new Utf8Regex(pattern, RegexOptions.CultureInvariant);
        var dotnet = new Regex(pattern, RegexOptions.CultureInvariant, Regex.InfiniteMatchTimeout);

        var expected = dotnet.Match(input);
        var actual = utf8.Match(Encoding.UTF8.GetBytes(input));

        Assert.Equal(expected.Success, actual.Success);
        Assert.Equal(expected.Index, actual.IndexInUtf16);
        Assert.Equal(expected.Length, actual.LengthInUtf16);
    }

    [Fact]
    public void LexerStringFamilyHandlesNonCapturingAlternation()
    {
        const string pattern = "\\G\"(?:[^\"\\\\]|\\\\.)*\"";
        const string input = "\"alpha\\\"beta\" tail";

        var utf8 = new Utf8Regex(pattern, RegexOptions.CultureInvariant);
        var dotnet = new Regex(pattern, RegexOptions.CultureInvariant, Regex.InfiniteMatchTimeout);

        var expected = dotnet.Match(input);
        var actual = utf8.Match(Encoding.UTF8.GetBytes(input));

        Assert.Equal(expected.Success, actual.Success);
        Assert.Equal(expected.Index, actual.IndexInUtf16);
        Assert.Equal(expected.Length, actual.LengthInUtf16);
    }

    [Fact]
    public void DashboardUrlMatchRemainsByteAlignedOnAsciiInput()
    {
        const string pattern = "^(?<before>.*)(https://go(\\.testing)?\\.lokad.com|~)(?<trigram>/[a-zA-Z0-9]+)?/d/(?<topicId>\\d+)/?\\?t=(?<tab>[^ ?]+)(?<rest>.*)$";
        const string input = "See https://go.testing.lokad.com/DEMO/d/123?t=overview for details";

        var utf8 = new Utf8Regex(pattern, RegexOptions.IgnoreCase);
        var dotnet = new Regex(pattern, RegexOptions.IgnoreCase, Regex.InfiniteMatchTimeout);

        var expected = dotnet.Match(input);
        var actual = utf8.Match(Encoding.UTF8.GetBytes(input));

        Assert.Equal(expected.Success, actual.Success);
        Assert.Equal(expected.Index, actual.IndexInUtf16);
        Assert.Equal(expected.Length, actual.LengthInUtf16);
        Assert.True(actual.IsByteAligned);
        Assert.Equal(expected.Index, actual.IndexInBytes);
        Assert.Equal(expected.Length, actual.LengthInBytes);
    }

    [Fact]
    public void DashboardUrlIsMatchMatchesDotNetOnRepresentativeInput()
    {
        const string pattern = "^(?<before>.*)(https://go(\\.testing)?\\.lokad.com|~)(?<trigram>/[a-zA-Z0-9]+)?/d/(?<topicId>\\d+)/?\\?t=(?<tab>[^ ?]+)(?<rest>.*)$";
        const string input = "See https://go.testing.lokad.com/DEMO/d/123?t=overview for details";

        var utf8 = new Utf8Regex(pattern, RegexOptions.IgnoreCase);
        var dotnet = new Regex(pattern, RegexOptions.IgnoreCase, Regex.InfiniteMatchTimeout);

        Assert.Equal(dotnet.IsMatch(input), utf8.IsMatch(Encoding.UTF8.GetBytes(input)));
    }

    [Fact]
    public void DownloadUrlMatchMatchesDotNetOnRepresentativeInput()
    {
        const string pattern = "^(?<before>.*)(https://go(\\.testing)?\\.lokad.com|~)(?<trigram>/[a-zA-Z0-9]+)?/gateway/BigFiles/Browse/Download\\?hash=(?<hash>[a-fA-F0-9]*)(?<optPath>[?&]path=[^& \\n]+)?[?&]name=(?<name>[^& ]+)(?<optPath2>[?&]path=[^& \\n]+)?(?<rest>.*)$";
        const string input = "Attachment https://go.lokad.com/DEMO/gateway/BigFiles/Browse/Download?hash=FFEEDDCCBBAA00998877665544332211&path=%2FExports%2FItems&name=ItemInspector.csv";

        var utf8 = new Utf8Regex(pattern, RegexOptions.IgnoreCase);
        var dotnet = new Regex(pattern, RegexOptions.IgnoreCase, Regex.InfiniteMatchTimeout);

        var expected = dotnet.Match(input);
        var actual = utf8.Match(Encoding.UTF8.GetBytes(input));

        Assert.Equal(expected.Success, actual.Success);
        Assert.Equal(expected.Index, actual.IndexInUtf16);
        Assert.Equal(expected.Length, actual.LengthInUtf16);
        Assert.True(actual.IsByteAligned);
    }

    [Fact]
    public void DownloadUrlMatchFallsBackForNonCanonicalCaseAndStillMatchesDotNet()
    {
        const string pattern = "^(?<before>.*)(https://go(\\.testing)?\\.lokad.com|~)(?<trigram>/[a-zA-Z0-9]+)?/gateway/BigFiles/Browse/Download\\?hash=(?<hash>[a-fA-F0-9]*)(?<optPath>[?&]path=[^& \\n]+)?[?&]name=(?<name>[^& ]+)(?<optPath2>[?&]path=[^& \\n]+)?(?<rest>.*)$";
        const string input = "Attachment https://go.lokad.com/DEMO/GATEWAY/BIGFILES/BROWSE/DOWNLOAD?hash=FFEEDDCCBBAA00998877665544332211&path=%2FExports%2FItems&name=ItemInspector.csv";

        var utf8 = new Utf8Regex(pattern, RegexOptions.IgnoreCase);
        var dotnet = new Regex(pattern, RegexOptions.IgnoreCase, Regex.InfiniteMatchTimeout);

        var expected = dotnet.Match(input);
        var actual = utf8.Match(Encoding.UTF8.GetBytes(input));

        Assert.Equal(expected.Success, actual.Success);
        Assert.Equal(expected.Index, actual.IndexInUtf16);
        Assert.Equal(expected.Length, actual.LengthInUtf16);
    }

    [Fact]
    public void LexerStringMatchPreservesUtf16LengthOnNonAsciiInput()
    {
        const string pattern = "\\G\"([^\"\\\\]|\\\\.)*\"";
        const string input = "\"été\" tail";

        var utf8 = new Utf8Regex(pattern, RegexOptions.CultureInvariant);
        var dotnet = new Regex(pattern, RegexOptions.CultureInvariant, Regex.InfiniteMatchTimeout);

        var expected = dotnet.Match(input);
        var actual = utf8.Match(Encoding.UTF8.GetBytes(input));

        Assert.Equal(expected.Success, actual.Success);
        Assert.Equal(expected.Index, actual.IndexInUtf16);
        Assert.Equal(expected.Length, actual.LengthInUtf16);
    }

}
