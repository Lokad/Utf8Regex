using System.Text;

using Lokad.Utf8Regex.Pcre2;

namespace Lokad.Utf8Regex.Pcre2.Tests;

public sealed class Utf8Pcre2RegexTranslationTests
{
    [Fact]
    public void ExactLiteralUsesUtf8RegexTranslationForPublicSurface()
    {
        var regex = new Utf8Pcre2Regex("foo");

        Assert.True(regex.DebugUsesUtf8RegexTranslation);
        Assert.Equal("IsMatch=Utf8Regex, Count=Utf8Regex, Enumerate=Utf8Regex, Match=Utf8Regex, Replace=Utf8Regex", regex.DebugDescribeExecutionPlan());

        Assert.True(regex.IsMatch("xxfooyy"u8));
        Assert.Equal(2, regex.Count("foo xx foo"u8));

        var match = regex.Match("xxfooyy"u8);
        Assert.True(match.Success);
        Assert.Equal(2, match.StartOffsetInBytes);
        Assert.Equal(5, match.EndOffsetInBytes);

        var enumerator = regex.EnumerateMatches("foo xx foo"u8);
        Assert.True(enumerator.MoveNext());
        Assert.Equal(0, enumerator.Current.StartOffsetInBytes);
        Assert.True(enumerator.MoveNext());
        Assert.Equal(7, enumerator.Current.StartOffsetInBytes);
        Assert.False(enumerator.MoveNext());
    }

    [Fact]
    public void IgnoreCaseLiteralUsesUtf8RegexTranslation()
    {
        var regex = new Utf8Pcre2Regex("httpclient", Pcre2CompileOptions.Caseless);

        Assert.True(regex.DebugUsesUtf8RegexTranslation);
        Assert.Equal("IsMatch=Utf8Regex, Count=Utf8Regex, Enumerate=Utf8Regex, Match=Utf8Regex, Replace=Utf8Regex", regex.DebugDescribeExecutionPlan());
        Assert.Equal(3, regex.Count("HttpClient x httpclient HTTPCLIENT"u8));
    }

    [Fact]
    public void Utf8LiteralAlternationUsesUtf8RegexTranslation()
    {
        var regex = new Utf8Pcre2Regex("café|naïve");

        Assert.True(regex.DebugUsesUtf8RegexTranslation);
        Assert.Equal("IsMatch=Utf8Regex, Count=Utf8Regex, Enumerate=Utf8Regex, Match=Utf8Regex, Replace=Utf8Regex", regex.DebugDescribeExecutionPlan());

        Assert.Equal(2, regex.Count("xxcafé yy naïve zz"u8));

        var first = regex.Match("xxcafé yy naïve zz"u8);
        Assert.True(first.Success);
        Assert.Equal("café", first.GetValueString());
    }

    [Fact]
    public void OptionalBarPatternUsesUtf8RegexTranslation()
    {
        var regex = new Utf8Pcre2Regex("foo(?<Bar>BAR)?");

        Assert.True(regex.DebugUsesUtf8RegexTranslation);
        Assert.False(regex.DebugHasManagedRegex);
        Assert.Equal("IsMatch=Utf8Regex, Count=Utf8Regex, Enumerate=Utf8Regex, Match=Utf8Regex, Replace=Utf8Regex", regex.DebugDescribeExecutionPlan());

        var match = regex.Match("xxfooBARzz"u8);
        Assert.True(match.Success);
        Assert.Equal("fooBAR", match.GetValueString());

        Assert.Equal(2, regex.Count("foo fooBAR x"u8));
    }

    [Fact]
    public void OptionalBarTranslatedTemplateReplacementUsesNamedCaptureWithoutManagedRegex()
    {
        var regex = new Utf8Pcre2Regex("foo(?<Bar>BAR)?");

        Assert.True(regex.DebugUsesUtf8RegexTranslation);
        Assert.False(regex.DebugHasManagedRegex);

        var replaced = regex.Replace("fooBAR x"u8, "<$0|$Bar>");

        Assert.Equal("<fooBAR|BAR> x", Encoding.UTF8.GetString(replaced));
    }

    [Fact]
    public void OptionalBarTranslatedEvaluatorReplacementUsesDetailedCapturesWithoutManagedRegex()
    {
        var regex = new Utf8Pcre2Regex("foo(?<Bar>BAR)?");

        Assert.True(regex.DebugUsesUtf8RegexTranslation);
        Assert.False(regex.DebugHasManagedRegex);

        var state = 0;
        var replaced = regex.ReplaceToString(
            "foo fooBAR x"u8,
            state,
            static (in Utf8Pcre2MatchContext match, ref int _) =>
            {
                var bar = match.TryGetFirstSetGroup("Bar", out var named) && named.Success
                    ? named.GetValueString()
                    : "none";
                return $"<{match.Value.GetValueString()}|{bar}>";
            });

        Assert.Equal("<foo|none> <fooBAR|BAR> x", replaced);
    }

    [Fact]
    public void AbPlusPatternUsesUtf8RegexTranslationForNonPartialSurface()
    {
        var regex = new Utf8Pcre2Regex("(a)b+");

        Assert.True(regex.DebugUsesUtf8RegexTranslation);
        Assert.Equal("IsMatch=Utf8Regex, Count=Utf8Regex, Enumerate=Utf8Regex, Match=Utf8Regex, Replace=Utf8Regex", regex.DebugDescribeExecutionPlan());
        Assert.True(regex.IsMatch("xxabbbzz"u8));
        Assert.Equal(2, regex.Count("ab abb"u8));

        var probe = regex.Probe("a"u8, Pcre2PartialMode.Hard);
        Assert.Equal(Utf8Pcre2ProbeKind.PartialMatch, probe.Kind);
        Assert.Equal("a", probe.GetPartial().Value.GetValueString());
    }

    [Fact]
    public void ManagedRegexAsciiSimplePatternCountUsesUtf8RegexTranslation()
    {
        var regex = new Utf8Pcre2Regex(@"\s[a-zA-Z]{0,12}ing\s");

        Assert.True(regex.DebugUsesUtf8RegexTranslation);
        Assert.Equal("IsMatch=Utf8Regex, Count=Utf8Regex, Enumerate=Utf8Regex, Match=Utf8Regex, Replace=Utf8Regex", regex.DebugDescribeExecutionPlan());
        Assert.Equal(3, regex.Count(" sing  bringing  going  go "u8));
    }

    [Fact]
    public void ManagedRegexAsciiOrderedWindowUsesUtf8RegexTranslation()
    {
        var regex = new Utf8Pcre2Regex("Tom.{10,25}river|river.{10,25}Tom");

        Assert.True(regex.DebugUsesUtf8RegexTranslation);
        Assert.Equal("IsMatch=Utf8Regex, Count=Utf8Regex, Enumerate=Utf8Regex, Match=Utf8Regex, Replace=Utf8Regex", regex.DebugDescribeExecutionPlan());
        Assert.Equal(2, regex.Count("Tom and Becky near the river xx river beside old Tom"u8));
    }

    [Fact]
    public void DateValidatorUsesUtf8RegexTranslation()
    {
        var regex = new Utf8Pcre2Regex(@"^\d{1,2}/\d{1,2}/\d{4}$");

        Assert.True(regex.DebugUsesUtf8RegexTranslation);
        Assert.Equal("IsMatch=Utf8Regex, Count=Utf8Regex, Enumerate=Utf8Regex, Match=Utf8Regex, Replace=Utf8Regex", regex.DebugDescribeExecutionPlan());
        Assert.True(regex.IsMatch("12/12/2001"u8));
        Assert.False(regex.IsMatch("12-12-2001"u8));
    }

    [Fact]
    public void CreditCardValidatorUsesUtf8RegexTranslation()
    {
        var regex = new Utf8Pcre2Regex(@"(\d{4}[- ]){3}\d{3,4}");

        Assert.True(regex.DebugUsesUtf8RegexTranslation);
        Assert.True(regex.IsMatch("1234-5678-1234-456"u8));
        Assert.False(regex.IsMatch("1234-5678"u8));
    }

    [Fact]
    public void FloatValidatorUsesUtf8RegexTranslation()
    {
        var regex = new Utf8Pcre2Regex(@"^[-+]?\d*\.?\d*$");

        Assert.True(regex.DebugUsesUtf8RegexTranslation);
        Assert.True(regex.IsMatch("-3.14159"u8));
        Assert.False(regex.IsMatch("3.14.159"u8));
    }

    [Fact]
    public void DateSearchUsesUtf8RegexTranslation()
    {
        var regex = new Utf8Pcre2Regex(@"\b\d{1,2}\/\d{1,2}\/\d{2,4}\b");

        Assert.True(regex.DebugUsesUtf8RegexTranslation);
        Assert.True(regex.IsMatch("today is 12/12/2001 indeed"u8));
        Assert.False(regex.IsMatch("today is 12-12-2001 indeed"u8));
    }

    [Fact]
    public void IpSearchUsesUtf8RegexTranslation()
    {
        var regex = new Utf8Pcre2Regex(@"(?:(?:25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9])\.){3}(?:25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9])");

        Assert.True(regex.DebugUsesUtf8RegexTranslation);
        Assert.True(regex.IsMatch("127.000.000.001"u8));
        Assert.False(regex.IsMatch("not an ip address"u8));
    }

    [Fact]
    public void UriSearchUsesUtf8RegexTranslation()
    {
        var regex = new Utf8Pcre2Regex(@"[\w]+://[^/\s?#]+[^\s?#]+(?:\?[^\s#]*)?(?:#[^\s]*)?");

        Assert.True(regex.DebugUsesUtf8RegexTranslation);
        Assert.True(regex.IsMatch("http://example.com/path?q=1#frag"u8));
        Assert.False(regex.IsMatch("not a uri"u8));
    }

    [Fact]
    public void TranslatedGroupZeroOnlyPatternUsesUtf8RegexForDetailedMatch()
    {
        var regex = new Utf8Pcre2Regex(@"\w{10,}");

        var match = regex.MatchDetailed("abcdefghij short"u8);

        Assert.True(match.Success);
        Assert.Equal("abcdefghij", match.Value.GetValueString());
        Assert.Equal(0, match.Value.StartOffsetInBytes);
        Assert.Equal(10, match.Value.EndOffsetInBytes);
    }

    [Fact]
    public void TranslatedCapturedPatternUsesUtf8RegexForDetailedMatch()
    {
        var regex = new Utf8Pcre2Regex("foo(?<Bar>BAR)?");

        var match = regex.MatchDetailed("xxfooBARzz"u8, startOffsetInBytes: 2);

        Assert.True(match.Success);
        Assert.Equal("fooBAR", match.Value.GetValueString());

        var whole = match.GetGroup(0);
        Assert.True(whole.Success);
        Assert.Equal("fooBAR", whole.GetValueString());

        var capture = match.GetGroup(1);
        Assert.True(capture.Success);
        Assert.Equal("BAR", capture.GetValueString());

        Assert.True(match.TryGetFirstSetGroup("Bar", out var named));
        Assert.True(named.Success);
        Assert.Equal("BAR", named.GetValueString());
    }

    [Fact]
    public void LongWordCountUsesUtf8RegexTranslation()
    {
        var regex = new Utf8Pcre2Regex(@"\w{10,}");

        Assert.True(regex.DebugUsesUtf8RegexTranslation);
        Assert.Equal(2, regex.Count("abcdefghij short klmnopqrst"u8));
    }

    [Fact]
    public void BoundaryLongWordCountUsesUtf8RegexTranslation()
    {
        var regex = new Utf8Pcre2Regex(@"\b\w{10,}\b");

        Assert.True(regex.DebugUsesUtf8RegexTranslation);
        Assert.Equal(2, regex.Count("abcdefghij short klmnopqrst"u8));
    }

    [Fact]
    public void HolmesWordCountUsesUtf8RegexTranslation()
    {
        var regex = new Utf8Pcre2Regex(@"\w+\s+Holmes");

        Assert.True(regex.DebugUsesUtf8RegexTranslation);
        Assert.Equal(2, regex.Count("Mr Holmes and Sherlock Holmes"u8));
    }

    [Fact]
    public void NonNewlineCountUsesUtf8RegexTranslation()
    {
        var regex = new Utf8Pcre2Regex(@"[^\n]*");

        Assert.True(regex.DebugUsesUtf8RegexTranslation);
        Assert.Equal(4, regex.Count("abc\ndef"u8));
    }

    [Fact]
    public void FtpLineValidatorUsesUtf8RegexTranslation()
    {
        var regex = new Utf8Pcre2Regex(@"^([0-9]+)(\-| |$)(.*)$");

        Assert.True(regex.DebugUsesUtf8RegexTranslation);
        Assert.True(regex.IsMatch("100- this is a line of ftp response"u8));
        Assert.False(regex.IsMatch("ftp response without code"u8));
    }

    [Fact]
    public void EmailPatternUsesUtf8RegexTranslation()
    {
        var regex = new Utf8Pcre2Regex(@"[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}");

        Assert.True(regex.DebugUsesUtf8RegexTranslation);
        Assert.False(regex.DebugHasManagedRegex);
        Assert.True(regex.IsMatch("user@example.com"u8));
        Assert.False(regex.IsMatch("not-an-email"u8));
    }

    [Fact]
    public void MultilineAnchoredPatternUsesUtf8RegexTranslation()
    {
        var regex = new Utf8Pcre2Regex("^ERROR: .+$", Pcre2CompileOptions.Multiline);

        Assert.True(regex.DebugUsesUtf8RegexTranslation);
        Assert.False(regex.DebugHasManagedRegex);
        Assert.True(regex.IsMatch("INFO: ok\nERROR: boom"u8));
        Assert.Equal(1, regex.Count("INFO: ok\nERROR: boom"u8));
    }

    [Fact]
    public void BacktrackingShapeUsesUtf8RegexTranslation()
    {
        var regex = new Utf8Pcre2Regex(@".*(ss)");

        Assert.True(regex.DebugUsesUtf8RegexTranslation);
        Assert.True(regex.IsMatch("glass"u8));
        var match = regex.MatchDetailed("glass"u8);
        Assert.True(match.Success);
        Assert.True(match.GetGroup(1).Success);
        Assert.Equal("ss", match.GetGroup(1).GetValueString());
    }

    [Fact]
    public void UnicodePropertyPatternsUseUtf8RegexTranslation()
    {
        var letters = new Utf8Pcre2Regex(@"\p{L}");
        var symbols = new Utf8Pcre2Regex(@"\p{Sm}");

        Assert.True(letters.DebugUsesUtf8RegexTranslation);
        Assert.True(symbols.DebugUsesUtf8RegexTranslation);
        Assert.Equal(3, letters.Count("abc123"u8));
        Assert.Equal(1, symbols.Count("a+b"u8));
    }
}
