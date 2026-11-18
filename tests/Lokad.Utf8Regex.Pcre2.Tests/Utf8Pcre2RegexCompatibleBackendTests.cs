using System.Buffers;

using Lokad.Utf8Regex.Pcre2;

namespace Lokad.Utf8Regex.Pcre2.Tests;

public sealed class Utf8Pcre2RegexCompatibleBackendTests
{
    [Fact]
    public void FooLiteralSupportsMatchCountAndEnumerate()
    {
        var regex = new Utf8Pcre2Regex("foo");

        var match = regex.Match("xxfoozz"u8);
        Assert.True(match.Success);
        Assert.Equal("foo", System.Text.Encoding.UTF8.GetString(match.GetValueBytes()));
        Assert.Equal(2, match.StartOffsetInBytes);
        Assert.Equal(5, match.EndOffsetInBytes);

        Assert.Equal(1, regex.Count("xxfoozz"u8));

        var enumerator = regex.EnumerateMatches("xxfoozz"u8);
        Assert.True(enumerator.MoveNext());
        Assert.Equal("foo", System.Text.Encoding.UTF8.GetString(enumerator.Current.GetValueBytes()));
        Assert.False(enumerator.MoveNext());
    }

    [Fact]
    public void FooOptionalBarSupportsDetailedMatchThroughCompatibleBackend()
    {
        var regex = new Utf8Pcre2Regex("foo(?<Bar>BAR)?");
        ReadOnlySpan<byte> match1Input = "xxfooBARzz"u8;
        var match1Captures = new Utf8Pcre2CaptureRange[regex.CaptureGroupCount + 1];

        var match = regex.Match(match1Input, captures: match1Captures);

        Assert.True(match.Success);
        Assert.Equal("fooBAR", System.Text.Encoding.UTF8.GetString(match.GetValueBytes()));
        Assert.Equal("BAR", System.Text.Encoding.UTF8.GetString((match1Captures[1].Success ? match1Input.Slice(match1Captures[1].StartOffsetInBytes, match1Captures[1].EndOffsetInBytes - match1Captures[1].StartOffsetInBytes) : ReadOnlySpan<byte>.Empty)));
    }

    [Fact]
    public void AbPlusSupportsMatchCountAndEnumerateThroughCompatibleBackend()
    {
        var regex = new Utf8Pcre2Regex("(a)b+");

        var match = regex.Match("ab abb x"u8);
        Assert.True(match.Success);
        Assert.Equal("ab", System.Text.Encoding.UTF8.GetString(match.GetValueBytes()));

        Assert.Equal(2, regex.Count("ab abb x"u8));

        var enumerator = regex.EnumerateMatches("ab abb x"u8);
        Assert.True(enumerator.MoveNext());
        Assert.Equal("ab", System.Text.Encoding.UTF8.GetString(enumerator.Current.GetValueBytes()));
        Assert.True(enumerator.MoveNext());
        Assert.Equal("abb", System.Text.Encoding.UTF8.GetString(enumerator.Current.GetValueBytes()));
        Assert.False(enumerator.MoveNext());
    }

    [Fact]
    public void IgnoreCaseLiteralUsesUtf8RegexDirectBackend()
    {
        var regex = new Utf8Pcre2Regex("httpclient", options: Pcre2CompileOptions.Caseless);

        Assert.Equal(4, regex.Count("HttpClient httpclient x HTTPCLIENT httpClient"u8));
        var enumerator = regex.EnumerateMatches("HttpClient httpclient x HTTPCLIENT httpClient"u8);
        Assert.True(enumerator.MoveNext());
        Assert.Equal("HttpClient", System.Text.Encoding.UTF8.GetString(enumerator.Current.GetValueBytes()));
        Assert.True(enumerator.MoveNext());
        Assert.Equal("httpclient", System.Text.Encoding.UTF8.GetString(enumerator.Current.GetValueBytes()));
        Assert.True(enumerator.MoveNext());
        Assert.Equal("HTTPCLIENT", System.Text.Encoding.UTF8.GetString(enumerator.Current.GetValueBytes()));
        Assert.True(enumerator.MoveNext());
        Assert.Equal("httpClient", System.Text.Encoding.UTF8.GetString(enumerator.Current.GetValueBytes()));
        Assert.False(enumerator.MoveNext());
        Assert.Equal("client client x client client", System.Text.Encoding.UTF8.GetString(regex.Replace("HttpClient httpclient x HTTPCLIENT httpClient"u8, "client")));
    }

    [Fact]
    public void IgnoreCaseLiteralReplacementHonorsStartOffsetThroughCompatibleBackend()
    {
        var regex = new Utf8Pcre2Regex("httpclient", options: Pcre2CompileOptions.Caseless);

        Assert.Equal(
            "HttpClient httpclient x client client",
            System.Text.Encoding.UTF8.GetString(regex.Replace("HttpClient httpclient x HTTPCLIENT httpClient"u8, "client", startOffsetInBytes: 23)));
    }

    [Fact]
    public void CompatibleBackendHonorsByteStartOffsetWithoutUtf16Fallback()
    {
        var regex = new Utf8Pcre2Regex("foo");

        var match = regex.Match("xxfoozzfoo"u8, startOffsetInBytes: 5);
        Assert.True(match.Success);
        Assert.Equal("foo", System.Text.Encoding.UTF8.GetString(match.GetValueBytes()));
        Assert.Equal(7, match.StartOffsetInBytes);
        Assert.Equal(10, match.EndOffsetInBytes);

        Assert.Equal(1, regex.Count("xxfoozzfoo"u8, startOffsetInBytes: 5));

        var enumerator = regex.EnumerateMatches("xxfoozzfoo"u8, startOffsetInBytes: 5);
        Assert.True(enumerator.MoveNext());
        Assert.Equal(7, enumerator.Current.StartOffsetInBytes);
        Assert.Equal(10, enumerator.Current.EndOffsetInBytes);
        Assert.False(enumerator.MoveNext());
    }

    [Fact]
    public void SpecialGlobalIterationPatternsDoNotUseUtf8RegexCountShortcut()
    {
        var regex = new Utf8Pcre2Regex("(?<=abc)(|def)");

        Assert.Equal(4, regex.Count("123abcxyzabcdef789abcpqr"u8));
    }

    [Fact]
    public void SimpleLiteralReplacementUsesCompatibleBackendSemantics()
    {
        var regex = new Utf8Pcre2Regex("foo");

        Assert.Equal("xxbarzz", System.Text.Encoding.UTF8.GetString(regex.Replace("xxfoozz"u8, "bar")));
        Assert.Equal("xx$zz", System.Text.Encoding.UTF8.GetString(regex.Replace("xxfoozz"u8, "$", 0, substitutionOptions: Pcre2SubstitutionOptions.SubstituteLiteral, Pcre2MatchOptions.None)));
    }

    [Fact]
    public void TryReplaceSupportsCompatibleLiteralFastPath()
    {
        var regex = new Utf8Pcre2Regex("foo");
        Span<byte> destination = stackalloc byte[16];

        var status = regex.TryReplace("xxfoozz"u8, "bar"u8, destination, out var bytesWritten);

        Assert.Equal(OperationStatus.Done, status);
        Assert.Equal("xxbarzz", System.Text.Encoding.UTF8.GetString(destination[..bytesWritten]));
    }

    [Fact]
    public void MatchManyUsesCompatibleManagedBackend()
    {
        var regex = new Utf8Pcre2Regex("foo");
        Span<Utf8Pcre2MatchData> destination = stackalloc Utf8Pcre2MatchData[2];

        var written = regex.MatchMany("xxfoozzfoo"u8, destination, out var isMore);

        Assert.Equal(2, written);
        Assert.False(isMore);
        Assert.Equal(2, destination[0].StartOffsetInBytes);
        Assert.Equal(5, destination[0].EndOffsetInBytes);
        Assert.Equal(7, destination[1].StartOffsetInBytes);
        Assert.Equal(10, destination[1].EndOffsetInBytes);
    }

    [Fact]
    public void MatchManyReportsIsMoreForCompatibleManagedBackend()
    {
        var regex = new Utf8Pcre2Regex("(a)b+");
        Span<Utf8Pcre2MatchData> destination = stackalloc Utf8Pcre2MatchData[1];

        var written = regex.MatchMany("ab abb x"u8, destination, out var isMore);

        Assert.Equal(1, written);
        Assert.True(isMore);
        Assert.Equal(0, destination[0].StartOffsetInBytes);
        Assert.Equal(2, destination[0].EndOffsetInBytes);
    }

    [Fact]
    public void BranchResetSubroutineUsesSearchEquivalentForCountAndEnumerate()
    {
        var regex = new Utf8Pcre2Regex("(?|(abc)|(xyz))(?1)");

        Assert.Equal(2, regex.Count("abcabcxyzabc"u8));

        var enumerator = regex.EnumerateMatches("abcabcxyzabc"u8);
        Assert.True(enumerator.MoveNext());
        Assert.Equal("abcabc", System.Text.Encoding.UTF8.GetString(enumerator.Current.GetValueBytes()));
        Assert.True(enumerator.MoveNext());
        Assert.Equal("xyzabc", System.Text.Encoding.UTF8.GetString(enumerator.Current.GetValueBytes()));
        Assert.False(enumerator.MoveNext());
    }

    [Fact]
    public void BranchResetSubroutineUsesSearchEquivalentForMatchMany()
    {
        var regex = new Utf8Pcre2Regex("(?|(abc)|(xyz))(?1)");
        Span<Utf8Pcre2MatchData> destination = stackalloc Utf8Pcre2MatchData[2];

        var written = regex.MatchMany("abcabcxyzabc"u8, destination, out var isMore);

        Assert.Equal(2, written);
        Assert.False(isMore);
        Assert.Equal(0, destination[0].StartOffsetInBytes);
        Assert.Equal(6, destination[0].EndOffsetInBytes);
        Assert.Equal(6, destination[1].StartOffsetInBytes);
        Assert.Equal(12, destination[1].EndOffsetInBytes);
    }

    [Fact]
    public void BranchResetBasicUsesSearchEquivalentForCountAndEnumerate()
    {
        var regex = new Utf8Pcre2Regex("(?|(abc)|(xyz))");

        Assert.Equal(3, regex.Count("abcxyzqqqabc"u8));

        var enumerator = regex.EnumerateMatches("abcxyzqqqabc"u8);
        Assert.True(enumerator.MoveNext());
        Assert.Equal("abc", System.Text.Encoding.UTF8.GetString(enumerator.Current.GetValueBytes()));
        Assert.True(enumerator.MoveNext());
        Assert.Equal("xyz", System.Text.Encoding.UTF8.GetString(enumerator.Current.GetValueBytes()));
        Assert.True(enumerator.MoveNext());
        Assert.Equal("abc", System.Text.Encoding.UTF8.GetString(enumerator.Current.GetValueBytes()));
        Assert.False(enumerator.MoveNext());
    }

    [Fact]
    public void BranchResetNestedUsesSearchEquivalentForCountEnumerateAndMatchMany()
    {
        var regex = new Utf8Pcre2Regex("(x)(?|(abc)|(xyz))(x)");

        Assert.Equal(2, regex.Count("xabcx xxyzx"u8));

        var enumerator = regex.EnumerateMatches("xabcx xxyzx"u8);
        Assert.True(enumerator.MoveNext());
        Assert.Equal("xabcx", System.Text.Encoding.UTF8.GetString(enumerator.Current.GetValueBytes()));
        Assert.True(enumerator.MoveNext());
        Assert.Equal("xxyzx", System.Text.Encoding.UTF8.GetString(enumerator.Current.GetValueBytes()));
        Assert.False(enumerator.MoveNext());

        Span<Utf8Pcre2MatchData> destination = stackalloc Utf8Pcre2MatchData[2];
        var written = regex.MatchMany("xabcx xxyzx"u8, destination, out var isMore);
        Assert.Equal(2, written);
        Assert.False(isMore);
        Assert.Equal(0, destination[0].StartOffsetInBytes);
        Assert.Equal(5, destination[0].EndOffsetInBytes);
        Assert.Equal(6, destination[1].StartOffsetInBytes);
        Assert.Equal(11, destination[1].EndOffsetInBytes);
    }
}
