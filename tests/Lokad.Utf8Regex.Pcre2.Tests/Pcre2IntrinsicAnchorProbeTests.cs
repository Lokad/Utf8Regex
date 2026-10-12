using System.Text;
using Lokad.Utf8Regex.Internal;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Pcre2.Tests;

[Collection(Pcre2AllocationTestCollection.Name)]
public sealed class Pcre2IntrinsicAnchorProbeTests
{
    // Frozen PCRE2 10.47 / private PCRE.NET 1.5.0 outcomes; no native test dependency.
    public static IEnumerable<object[]> NativeCases()
    {
        yield return ["^foo", "xfoo", 1, Pcre2CompileOptions.None, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["^foo", "xfoo", 1, Pcre2CompileOptions.None, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["^foo", "xfoo", 1, Pcre2CompileOptions.None, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["\\Afoo", "xfoo", 1, Pcre2CompileOptions.None, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["\\Afoo", "xfoo", 1, Pcre2CompileOptions.None, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["\\Afoo", "xfoo", 1, Pcre2CompileOptions.None, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["^(a)++\\w", "xaaab", 1, Pcre2CompileOptions.None, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["^(a)++\\w", "xaaab", 1, Pcre2CompileOptions.None, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["^(a)++\\w", "xaaab", 1, Pcre2CompileOptions.None, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["^foo", "foo", 0, Pcre2CompileOptions.None, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.FullMatch, 0, 3];
        yield return ["^foo", "foo", 0, Pcre2CompileOptions.None, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.FullMatch, 0, 3];
        yield return ["^foo", "foo", 0, Pcre2CompileOptions.None, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.FullMatch, 0, 3];
        yield return ["^foo", "x\nbar\nfoobar", 0, Pcre2CompileOptions.Multiline, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.FullMatch, 6, 9];
        yield return ["^foo", "x\nbar\nfoobar", 0, Pcre2CompileOptions.Multiline, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.FullMatch, 6, 9];
        yield return ["^foo", "x\nbar\nfoobar", 0, Pcre2CompileOptions.Multiline, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.FullMatch, 6, 9];
        yield return ["^foo", "x\nbar\nfo", 0, Pcre2CompileOptions.Multiline, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["^foo", "x\nbar\nfo", 0, Pcre2CompileOptions.Multiline, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.PartialMatch, 6, 8];
        yield return ["^foo", "x\nbar\nfo", 0, Pcre2CompileOptions.Multiline, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.PartialMatch, 6, 8];
        yield return ["^foo", "\ud83d\ude00\nbar\nfoobar", 5, Pcre2CompileOptions.Multiline, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.FullMatch, 9, 12];
        yield return ["^foo", "\ud83d\ude00\nbar\nfoobar", 5, Pcre2CompileOptions.Multiline, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.FullMatch, 9, 12];
        yield return ["^foo", "\ud83d\ude00\nbar\nfoobar", 5, Pcre2CompileOptions.Multiline, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.FullMatch, 9, 12];
        yield return ["^foo", "x\nfoo", 0, Pcre2CompileOptions.Multiline, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.Anchored, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["^foo", "x\nfoo", 0, Pcre2CompileOptions.Multiline, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.Anchored, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["^foo", "x\nfoo", 0, Pcre2CompileOptions.Multiline, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.Anchored, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["^foo", "x\nfoo", 2, Pcre2CompileOptions.Multiline, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.Anchored, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.FullMatch, 2, 5];
        yield return ["^foo", "x\nfoo", 2, Pcre2CompileOptions.Multiline, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.Anchored, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.FullMatch, 2, 5];
        yield return ["^foo", "x\nfoo", 2, Pcre2CompileOptions.Multiline, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.Anchored, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.FullMatch, 2, 5];
        yield return ["^foo", "foo\nbar\nfoo", 0, Pcre2CompileOptions.Multiline, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.NotBol, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.FullMatch, 8, 11];
        yield return ["^foo", "foo\nbar\nfoo", 0, Pcre2CompileOptions.Multiline, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.NotBol, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.FullMatch, 8, 11];
        yield return ["^foo", "foo\nbar\nfoo", 0, Pcre2CompileOptions.Multiline, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.NotBol, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.FullMatch, 8, 11];
        yield return ["\\Afoo", "foo", 0, Pcre2CompileOptions.None, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.NotBol, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.FullMatch, 0, 3];
        yield return ["\\Afoo", "foo", 0, Pcre2CompileOptions.None, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.NotBol, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.FullMatch, 0, 3];
        yield return ["\\Afoo", "foo", 0, Pcre2CompileOptions.None, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.NotBol, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.FullMatch, 0, 3];
        yield return ["^abc$", "xabc", 1, Pcre2CompileOptions.None, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["^abc$", "xabc", 1, Pcre2CompileOptions.None, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["^abc$", "xabc", 1, Pcre2CompileOptions.None, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["^abc$", "abc\n", 0, Pcre2CompileOptions.None, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.FullMatch, 0, 3];
        yield return ["^abc$", "abc\n", 0, Pcre2CompileOptions.None, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.FullMatch, 0, 3];
        yield return ["^abc$", "abc\n", 0, Pcre2CompileOptions.None, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.PartialMatch, 0, 4];
        yield return ["^abc$", "abc", 0, Pcre2CompileOptions.None, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.FullMatch, 0, 3];
        yield return ["^abc$", "abc", 0, Pcre2CompileOptions.None, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.FullMatch, 0, 3];
        yield return ["^abc$", "abc", 0, Pcre2CompileOptions.None, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.PartialMatch, 0, 3];
        yield return ["^abc$", "abc\nx", 0, Pcre2CompileOptions.Multiline, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.FullMatch, 0, 3];
        yield return ["^abc$", "abc\nx", 0, Pcre2CompileOptions.Multiline, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.FullMatch, 0, 3];
        yield return ["^abc$", "abc\nx", 0, Pcre2CompileOptions.Multiline, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.FullMatch, 0, 3];
        yield return ["^abc$", "abcx\nabc\n", 0, Pcre2CompileOptions.Multiline, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.FullMatch, 5, 8];
        yield return ["^abc$", "abcx\nabc\n", 0, Pcre2CompileOptions.Multiline, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.FullMatch, 5, 8];
        yield return ["^abc$", "abcx\nabc\n", 0, Pcre2CompileOptions.Multiline, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.FullMatch, 5, 8];
        yield return ["^abc$", "x\nab", 0, Pcre2CompileOptions.Multiline, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["^abc$", "x\nab", 0, Pcre2CompileOptions.Multiline, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.PartialMatch, 2, 4];
        yield return ["^abc$", "x\nab", 0, Pcre2CompileOptions.Multiline, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.PartialMatch, 2, 4];
        yield return ["^abc$", "x\nabc", 0, Pcre2CompileOptions.Multiline, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.NotEol, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["^abc$", "x\nabc", 0, Pcre2CompileOptions.Multiline, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.NotEol, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["^abc$", "x\nabc", 0, Pcre2CompileOptions.Multiline, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.NotEol, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["^abc$", "abc\nx", 0, Pcre2CompileOptions.Multiline, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.NotEol, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.FullMatch, 0, 3];
        yield return ["^abc$", "abc\nx", 0, Pcre2CompileOptions.Multiline, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.NotEol, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.FullMatch, 0, 3];
        yield return ["^abc$", "abc\nx", 0, Pcre2CompileOptions.Multiline, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.NotEol, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.FullMatch, 0, 3];
        yield return ["\\Aabc\\Z", "abc\n", 0, Pcre2CompileOptions.None, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.NotBol | Pcre2MatchOptions.NotEol, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.FullMatch, 0, 3];
        yield return ["\\Aabc\\Z", "abc\n", 0, Pcre2CompileOptions.None, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.NotBol | Pcre2MatchOptions.NotEol, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.FullMatch, 0, 3];
        yield return ["\\Aabc\\Z", "abc\n", 0, Pcre2CompileOptions.None, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.NotBol | Pcre2MatchOptions.NotEol, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.PartialMatch, 0, 4];
        yield return ["^foo\\b", "foobar\nfoo!", 0, Pcre2CompileOptions.Multiline, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.FullMatch, 7, 10];
        yield return ["^foo\\b", "foobar\nfoo!", 0, Pcre2CompileOptions.Multiline, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.FullMatch, 7, 10];
        yield return ["^foo\\b", "foobar\nfoo!", 0, Pcre2CompileOptions.Multiline, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.FullMatch, 7, 10];
        yield return ["^foo\\B", "foo!\nfoobar", 0, Pcre2CompileOptions.Multiline, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.FullMatch, 5, 8];
        yield return ["^foo\\B", "foo!\nfoobar", 0, Pcre2CompileOptions.Multiline, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.FullMatch, 5, 8];
        yield return ["^foo\\B", "foo!\nfoobar", 0, Pcre2CompileOptions.Multiline, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.FullMatch, 5, 8];
        yield return ["^abc\\z", "abc\nabc", 0, Pcre2CompileOptions.Multiline, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.FullMatch, 4, 7];
        yield return ["^abc\\z", "abc\nabc", 0, Pcre2CompileOptions.Multiline, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.FullMatch, 4, 7];
        yield return ["^abc\\z", "abc\nabc", 0, Pcre2CompileOptions.Multiline, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.PartialMatch, 4, 7];
        yield return ["^\ud83d\ude00$", "x\n\ud83d\ude00\n", 0, Pcre2CompileOptions.Multiline, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.FullMatch, 2, 6];
        yield return ["^\ud83d\ude00$", "x\n\ud83d\ude00\n", 0, Pcre2CompileOptions.Multiline, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.FullMatch, 2, 6];
        yield return ["^\ud83d\ude00$", "x\n\ud83d\ude00\n", 0, Pcre2CompileOptions.Multiline, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.FullMatch, 2, 6];
        yield return ["^foo", "x\r\nbar\r\nfoo", 0, Pcre2CompileOptions.Multiline, Pcre2NewlineConvention.Crlf, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.FullMatch, 8, 11];
        yield return ["^foo", "x\r\nbar\r\nfoo", 0, Pcre2CompileOptions.Multiline, Pcre2NewlineConvention.Crlf, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.FullMatch, 8, 11];
        yield return ["^foo", "x\r\nbar\r\nfoo", 0, Pcre2CompileOptions.Multiline, Pcre2NewlineConvention.Crlf, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.FullMatch, 8, 11];
        yield return ["^abc$", "x\r\nabc\r\n", 0, Pcre2CompileOptions.Multiline, Pcre2NewlineConvention.Crlf, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.FullMatch, 3, 6];
        yield return ["^abc$", "x\r\nabc\r\n", 0, Pcre2CompileOptions.Multiline, Pcre2NewlineConvention.Crlf, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.FullMatch, 3, 6];
        yield return ["^abc$", "x\r\nabc\r\n", 0, Pcre2CompileOptions.Multiline, Pcre2NewlineConvention.Crlf, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.FullMatch, 3, 6];
        yield return ["^abc$", "abc\r", 0, Pcre2CompileOptions.None, Pcre2NewlineConvention.Crlf, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["^abc$", "abc\r", 0, Pcre2CompileOptions.None, Pcre2NewlineConvention.Crlf, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.PartialMatch, 0, 4];
        yield return ["^abc$", "abc\r", 0, Pcre2CompileOptions.None, Pcre2NewlineConvention.Crlf, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.PartialMatch, 0, 4];
        yield return ["^abc$", "abc\r", 0, Pcre2CompileOptions.Multiline, Pcre2NewlineConvention.Crlf, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["^abc$", "abc\r", 0, Pcre2CompileOptions.Multiline, Pcre2NewlineConvention.Crlf, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.PartialMatch, 0, 4];
        yield return ["^abc$", "abc\r", 0, Pcre2CompileOptions.Multiline, Pcre2NewlineConvention.Crlf, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.PartialMatch, 0, 4];
        yield return ["^abc$", "abc\r", 0, Pcre2CompileOptions.Multiline, Pcre2NewlineConvention.Crlf, Pcre2MatchOptions.NotEol, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["^abc$", "abc\r", 0, Pcre2CompileOptions.Multiline, Pcre2NewlineConvention.Crlf, Pcre2MatchOptions.NotEol, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.PartialMatch, 0, 4];
        yield return ["^abc$", "abc\r", 0, Pcre2CompileOptions.Multiline, Pcre2NewlineConvention.Crlf, Pcre2MatchOptions.NotEol, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.PartialMatch, 0, 4];
        yield return ["^abc$", "abc\n", 0, Pcre2CompileOptions.Multiline, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.FullMatch, 0, 3];
        yield return ["^abc$", "abc\n", 0, Pcre2CompileOptions.Multiline, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.FullMatch, 0, 3];
        yield return ["^abc$", "abc\n", 0, Pcre2CompileOptions.Multiline, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.FullMatch, 0, 3];
        yield return ["^a$", "a", 0, Pcre2CompileOptions.None, Pcre2NewlineConvention.Crlf, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.FullMatch, 0, 1];
        yield return ["^a$", "a", 0, Pcre2CompileOptions.None, Pcre2NewlineConvention.Crlf, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.FullMatch, 0, 1];
        yield return ["^a$", "a", 0, Pcre2CompileOptions.None, Pcre2NewlineConvention.Crlf, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.PartialMatch, 0, 1];
        yield return ["^a$", "a\r", 0, Pcre2CompileOptions.None, Pcre2NewlineConvention.Crlf, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["^a$", "a\r", 0, Pcre2CompileOptions.None, Pcre2NewlineConvention.Crlf, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.PartialMatch, 0, 2];
        yield return ["^a$", "a\r", 0, Pcre2CompileOptions.None, Pcre2NewlineConvention.Crlf, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.PartialMatch, 0, 2];
        yield return ["^a$", "a\r\n", 0, Pcre2CompileOptions.None, Pcre2NewlineConvention.Crlf, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.FullMatch, 0, 1];
        yield return ["^a$", "a\r\n", 0, Pcre2CompileOptions.None, Pcre2NewlineConvention.Crlf, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.FullMatch, 0, 1];
        yield return ["^a$", "a\r\n", 0, Pcre2CompileOptions.None, Pcre2NewlineConvention.Crlf, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.PartialMatch, 0, 3];
        yield return ["^abc$", "abc\r", 0, Pcre2CompileOptions.None, Pcre2NewlineConvention.Crlf, Pcre2MatchOptions.NotEol, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["^abc$", "abc\r", 0, Pcre2CompileOptions.None, Pcre2NewlineConvention.Crlf, Pcre2MatchOptions.NotEol, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["^abc$", "abc\r", 0, Pcre2CompileOptions.None, Pcre2NewlineConvention.Crlf, Pcre2MatchOptions.NotEol, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["^abc$", "abc\r\n", 0, Pcre2CompileOptions.None, Pcre2NewlineConvention.Crlf, Pcre2MatchOptions.NotEol, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["^abc$", "abc\r\n", 0, Pcre2CompileOptions.None, Pcre2NewlineConvention.Crlf, Pcre2MatchOptions.NotEol, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["^abc$", "abc\r\n", 0, Pcre2CompileOptions.None, Pcre2NewlineConvention.Crlf, Pcre2MatchOptions.NotEol, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
    }

    [Theory]
    [MemberData(nameof(NativeCases))]
    public void IntrinsicAnchorsPreserveOriginalSubjectAndLineCandidates(string pattern, string text, int start,
        Pcre2CompileOptions compile, Pcre2NewlineConvention newline, Pcre2MatchOptions flags,
        Pcre2PartialMode mode, Utf8Pcre2ProbeKind kind, int begin, int end)
    {
        var regex = new Utf8Pcre2Regex(pattern, compile, new() { Newline = newline }, default, Timeout.InfiniteTimeSpan);
        var input = Encoding.UTF8.GetBytes(text);
        var probe = regex.Probe(input, mode, start, flags);
        Assert.Equal(kind, probe.Kind);
        Assert.Equal((begin, end), (probe.Value.StartOffsetInBytes, probe.Value.EndOffsetInBytes));
        if (kind == Utf8Pcre2ProbeKind.FullMatch)
            Assert.Equal(Encoding.UTF8.GetString(input.AsSpan(begin, end - begin)), probe.GetMatch().GetValueString());
    }

    [Fact]
    public void LaterLineFullPreservesFiniteAdmissionAndRetainedAlias()
    {
        var capacity = Utf8ConstructionBudget.ArrayBytes<Pcre2GroupData>(1);
        Utf8Pcre2Regex Create(ulong bytes) => Utf8Pcre2Regex.Create("^abc$", Pcre2CompileOptions.Multiline, default,
            new() { MaxWorkSteps = 100000, MaxDepth = 256, MaxWorkingBytes = 1 << 20, MaxResultBytes = bytes },
            Timeout.InfiniteTimeSpan);
        Assert.Equal(Pcre2ErrorKind.ResultLimit, Assert.Throws<Pcre2MatchException>(() =>
        { _ = Create(capacity - 1).Probe("😀\nbar\nabc\nx"u8, Pcre2PartialMode.Hard); }).ErrorKind);
        var regex = Create(capacity);
        var retained = regex.Probe("😀\nbar\nabc\nx"u8, Pcre2PartialMode.Hard).GetMatch();
        var alias = retained.GetGroup(0);
        Assert.Equal((9, 12), (alias.StartOffsetInBytes, alias.EndOffsetInBytes));
        Assert.Equal(Utf8Pcre2ProbeKind.NoMatch, regex.Probe("xabc"u8, Pcre2PartialMode.Soft, 1).Kind);
        Assert.Equal("abc", retained.GetValueString());
        Assert.Equal("abc", alias.GetValueString());
        Assert.Equal(Utf8Pcre2ProbeKind.FullMatch, regex.Probe("😀\nbar\nabc\nx"u8, Pcre2PartialMode.Hard).Kind);
        Assert.Equal(Utf8Pcre2ProbeKind.PartialMatch, Create(1).Probe("😀\nbar\nab"u8, Pcre2PartialMode.Hard).Kind);
    }

    [Fact]
    public void OriginalAnchorKeepsOwnedNamedCapturesAcrossRejectedOffset()
    {
        var regex = Utf8Pcre2Regex.Create(@"^(?<last>a)++\w", Pcre2CompileOptions.None, default,
            new() { MaxWorkSteps = 100000, MaxDepth = 256, MaxWorkingBytes = 1 << 20, MaxResultBytes = 4096 },
            Timeout.InfiniteTimeSpan);
        var retained = regex.Probe("aaab"u8, Pcre2PartialMode.Soft).GetMatch();
        Assert.True(retained.TryGetFirstSetGroup("last", out var alias));
        Assert.Equal((2, 3), (alias.StartOffsetInBytes, alias.EndOffsetInBytes));
        Assert.Equal(Utf8Pcre2ProbeKind.NoMatch, regex.Probe("xaaab"u8, Pcre2PartialMode.Soft, 1).Kind);
        Assert.Equal("aaab", retained.GetValueString());
        Assert.Equal("a", alias.GetValueString());
    }

    [Fact]
    public void MultilineSearchCannotBypassFiniteWork()
    {
        var regex = new Utf8Pcre2Regex("^foo", Pcre2CompileOptions.Multiline, default,
            new() { MatchLimit = 20 }, Timeout.InfiniteTimeSpan);
        var input = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat("x\n", 100)) + "foo");
        Assert.Equal(Pcre2ErrorKind.MatchLimit, Assert.Throws<Pcre2MatchException>(() =>
        { _ = regex.Probe(input, Pcre2PartialMode.Soft); }).ErrorKind);
        Assert.Equal(Utf8Pcre2ProbeKind.FullMatch, regex.Probe("foo"u8, Pcre2PartialMode.Soft).Kind);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void MultilineCandidatesObserveOriginalInterruption(bool cancel)
    {
        using var source = new CancellationTokenSource();
        var regex = new Utf8Pcre2Regex("^foo", Pcre2CompileOptions.Multiline, default, default, TimeSpan.FromMilliseconds(20));
        var polls = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() =>
        {
            if (++polls < 10) return 0;
            if (cancel) { source.Cancel(); return 0; }
            return 25;
        });
        Exception? failure = null;
        try { _ = regex.Probe("x\nx\nx\nx\nx\nx\nx\nx\nx\nfoo"u8, Pcre2PartialMode.Soft, 0, Pcre2MatchOptions.None, source.Token); }
        catch (Exception error) { failure = error; }
        if (cancel) Assert.Equal(source.Token, Assert.IsType<OperationCanceledException>(failure).CancellationToken);
        else Assert.Equal(Pcre2ErrorKind.Timeout, Assert.IsType<Pcre2MatchException>(failure).ErrorKind);
    }
}
