using System.Text;
using Lokad.Utf8Regex.Internal;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Pcre2.Tests;

[Collection(Pcre2AllocationTestCollection.Name)]
public sealed class Pcre2LiteralBoundaryProbeTests
{
    // Frozen PCRE2 10.47 / private PCRE.NET 1.5.0 outcomes; no native test dependency.
    public static IEnumerable<object[]> NativeCases()
    {
        yield return ["abc$", "abcx abc", 0, Pcre2CompileOptions.None, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.FullMatch, 5, 8];
        yield return ["abc$", "abcx abc", 0, Pcre2CompileOptions.None, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.FullMatch, 5, 8];
        yield return ["abc$", "abcx abc", 0, Pcre2CompileOptions.None, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.PartialMatch, 5, 8];
        yield return ["abc\\z", "abcx abc", 0, Pcre2CompileOptions.None, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.FullMatch, 5, 8];
        yield return ["abc\\z", "abcx abc", 0, Pcre2CompileOptions.None, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.FullMatch, 5, 8];
        yield return ["abc\\z", "abcx abc", 0, Pcre2CompileOptions.None, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.PartialMatch, 5, 8];
        yield return ["abc\\Z", "abcx abc\r\n", 0, Pcre2CompileOptions.None, Pcre2NewlineConvention.Crlf, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.FullMatch, 5, 8];
        yield return ["abc\\Z", "abcx abc\r\n", 0, Pcre2CompileOptions.None, Pcre2NewlineConvention.Crlf, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.FullMatch, 5, 8];
        yield return ["abc\\Z", "abcx abc\r\n", 0, Pcre2CompileOptions.None, Pcre2NewlineConvention.Crlf, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.PartialMatch, 5, 10];
        yield return ["abc\\b", "abcx abc!", 0, Pcre2CompileOptions.None, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.FullMatch, 5, 8];
        yield return ["abc\\b", "abcx abc!", 0, Pcre2CompileOptions.None, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.FullMatch, 5, 8];
        yield return ["abc\\b", "abcx abc!", 0, Pcre2CompileOptions.None, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.FullMatch, 5, 8];
        yield return ["abc\\B", "abc!abcx", 0, Pcre2CompileOptions.None, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.FullMatch, 4, 7];
        yield return ["abc\\B", "abc!abcx", 0, Pcre2CompileOptions.None, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.FullMatch, 4, 7];
        yield return ["abc\\B", "abc!abcx", 0, Pcre2CompileOptions.None, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.FullMatch, 4, 7];
        yield return ["abc\\b", "abc\u00e9 abc!", 0, Pcre2CompileOptions.Ucp, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.FullMatch, 6, 9];
        yield return ["abc\\b", "abc\u00e9 abc!", 0, Pcre2CompileOptions.Ucp, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.FullMatch, 6, 9];
        yield return ["abc\\b", "abc\u00e9 abc!", 0, Pcre2CompileOptions.Ucp, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.FullMatch, 6, 9];
        yield return ["abc\\B", "abc\u00e9", 0, Pcre2CompileOptions.Ucp, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.FullMatch, 0, 3];
        yield return ["abc\\B", "abc\u00e9", 0, Pcre2CompileOptions.Ucp, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.FullMatch, 0, 3];
        yield return ["abc\\B", "abc\u00e9", 0, Pcre2CompileOptions.Ucp, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.FullMatch, 0, 3];
        yield return ["\\babc\\b", "\u00e9abc! abc!", 0, Pcre2CompileOptions.Ucp, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.FullMatch, 7, 10];
        yield return ["\\babc\\b", "\u00e9abc! abc!", 0, Pcre2CompileOptions.Ucp, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.FullMatch, 7, 10];
        yield return ["\\babc\\b", "\u00e9abc! abc!", 0, Pcre2CompileOptions.Ucp, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.FullMatch, 7, 10];
        yield return ["\\babc\\b", "\ud83d\ude00abc!", 0, Pcre2CompileOptions.Ucp, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.FullMatch, 4, 7];
        yield return ["\\babc\\b", "\ud83d\ude00abc!", 0, Pcre2CompileOptions.Ucp, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.FullMatch, 4, 7];
        yield return ["\\babc\\b", "\ud83d\ude00abc!", 0, Pcre2CompileOptions.Ucp, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.FullMatch, 4, 7];
        yield return ["\\b\ud83d\ude00\\b", "a\ud83d\ude00b", 0, Pcre2CompileOptions.Ucp, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.FullMatch, 1, 5];
        yield return ["\\b\ud83d\ude00\\b", "a\ud83d\ude00b", 0, Pcre2CompileOptions.Ucp, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.FullMatch, 1, 5];
        yield return ["\\b\ud83d\ude00\\b", "a\ud83d\ude00b", 0, Pcre2CompileOptions.Ucp, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.FullMatch, 1, 5];
        yield return ["\ud83d\ude00\\b", "\ud83d\ude00\u00e9", 0, Pcre2CompileOptions.Ucp, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.FullMatch, 0, 4];
        yield return ["\ud83d\ude00\\b", "\ud83d\ude00\u00e9", 0, Pcre2CompileOptions.Ucp, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.FullMatch, 0, 4];
        yield return ["\ud83d\ude00\\b", "\ud83d\ude00\u00e9", 0, Pcre2CompileOptions.Ucp, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.FullMatch, 0, 4];
        yield return ["\ud83d\ude00\\B", "\ud83d\ude00\u00e9", 0, Pcre2CompileOptions.Ucp, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["\ud83d\ude00\\B", "\ud83d\ude00\u00e9", 0, Pcre2CompileOptions.Ucp, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.PartialMatch, 6, 6];
        yield return ["\ud83d\ude00\\B", "\ud83d\ude00\u00e9", 0, Pcre2CompileOptions.Ucp, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.PartialMatch, 6, 6];
        yield return ["\\babc\\b", "abc\u00e9", 0, Pcre2CompileOptions.None, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.FullMatch, 0, 3];
        yield return ["\\babc\\b", "abc\u00e9", 0, Pcre2CompileOptions.None, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.FullMatch, 0, 3];
        yield return ["\\babc\\b", "abc\u00e9", 0, Pcre2CompileOptions.None, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.FullMatch, 0, 3];
        yield return ["\\babc\\b", "\u00e9abc!", 2, Pcre2CompileOptions.Ucp, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.Anchored, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["\\babc\\b", "\u00e9abc!", 2, Pcre2CompileOptions.Ucp, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.Anchored, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["\\babc\\b", "\u00e9abc!", 2, Pcre2CompileOptions.Ucp, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.Anchored, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["abc\\z", "x ab", 0, Pcre2CompileOptions.None, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["abc\\z", "x ab", 0, Pcre2CompileOptions.None, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.PartialMatch, 2, 4];
        yield return ["abc\\z", "x ab", 0, Pcre2CompileOptions.None, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.PartialMatch, 2, 4];
        yield return ["abc\\Z", "x ab", 0, Pcre2CompileOptions.None, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["abc\\Z", "x ab", 0, Pcre2CompileOptions.None, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.PartialMatch, 2, 4];
        yield return ["abc\\Z", "x ab", 0, Pcre2CompileOptions.None, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.PartialMatch, 2, 4];
        yield return ["abc\\b", "x ab", 0, Pcre2CompileOptions.None, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["abc\\b", "x ab", 0, Pcre2CompileOptions.None, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.PartialMatch, 2, 4];
        yield return ["abc\\b", "x ab", 0, Pcre2CompileOptions.None, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.PartialMatch, 2, 4];
        yield return ["\\babc\\b", "\u00e9ab", 0, Pcre2CompileOptions.Ucp, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["\\babc\\b", "\u00e9ab", 0, Pcre2CompileOptions.Ucp, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.PartialMatch, 4, 4];
        yield return ["\\babc\\b", "\u00e9ab", 0, Pcre2CompileOptions.Ucp, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.PartialMatch, 4, 4];
        yield return ["abc$", "abcx abc\r", 0, Pcre2CompileOptions.None, Pcre2NewlineConvention.Crlf, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["abc$", "abcx abc\r", 0, Pcre2CompileOptions.None, Pcre2NewlineConvention.Crlf, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.PartialMatch, 5, 9];
        yield return ["abc$", "abcx abc\r", 0, Pcre2CompileOptions.None, Pcre2NewlineConvention.Crlf, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.PartialMatch, 5, 9];
        yield return ["abc$", "abc\nx", 0, Pcre2CompileOptions.Multiline, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.NotEol, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.FullMatch, 0, 3];
        yield return ["abc$", "abc\nx", 0, Pcre2CompileOptions.Multiline, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.NotEol, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.FullMatch, 0, 3];
        yield return ["abc$", "abc\nx", 0, Pcre2CompileOptions.Multiline, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.NotEol, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.FullMatch, 0, 3];
        yield return ["\\babc\\b", "abcx abc", 0, Pcre2CompileOptions.None, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.Anchored, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["\\babc\\b", "abcx abc", 0, Pcre2CompileOptions.None, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.Anchored, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["\\babc\\b", "abcx abc", 0, Pcre2CompileOptions.None, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.Anchored, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["^\\babc\\b", "xabc abc", 0, Pcre2CompileOptions.None, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["^\\babc\\b", "xabc abc", 0, Pcre2CompileOptions.None, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["^\\babc\\b", "xabc abc", 0, Pcre2CompileOptions.None, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["^\\babc\\b", "x\nabc!", 0, Pcre2CompileOptions.Multiline, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.FullMatch, 2, 5];
        yield return ["^\\babc\\b", "x\nabc!", 0, Pcre2CompileOptions.Multiline, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.FullMatch, 2, 5];
        yield return ["^\\babc\\b", "x\nabc!", 0, Pcre2CompileOptions.Multiline, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.FullMatch, 2, 5];
        yield return ["abc\\b", "\ud83d\ude00abcx abc!", 4, Pcre2CompileOptions.None, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.FullMatch, 9, 12];
        yield return ["abc\\b", "\ud83d\ude00abcx abc!", 4, Pcre2CompileOptions.None, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.FullMatch, 9, 12];
        yield return ["abc\\b", "\ud83d\ude00abcx abc!", 4, Pcre2CompileOptions.None, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.FullMatch, 9, 12];
        yield return ["abc$", "abcx abc", 5, Pcre2CompileOptions.None, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.Anchored, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.FullMatch, 5, 8];
        yield return ["abc$", "abcx abc", 5, Pcre2CompileOptions.None, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.Anchored, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.FullMatch, 5, 8];
        yield return ["abc$", "abcx abc", 5, Pcre2CompileOptions.None, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.Anchored, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.PartialMatch, 5, 8];
        yield return ["abc\\b", "abc", 0, Pcre2CompileOptions.None, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.FullMatch, 0, 3];
        yield return ["abc\\b", "abc", 0, Pcre2CompileOptions.None, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.FullMatch, 0, 3];
        yield return ["abc\\b", "abc", 0, Pcre2CompileOptions.None, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.PartialMatch, 0, 3];
        yield return ["\\babc\\b", "abc", 0, Pcre2CompileOptions.None, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.FullMatch, 0, 3];
        yield return ["\\babc\\b", "abc", 0, Pcre2CompileOptions.None, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.FullMatch, 0, 3];
        yield return ["\\babc\\b", "abc", 0, Pcre2CompileOptions.None, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.PartialMatch, 0, 3];
        yield return ["abc\\B", "abc", 0, Pcre2CompileOptions.None, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["abc\\B", "abc", 0, Pcre2CompileOptions.None, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.PartialMatch, 0, 3];
        yield return ["abc\\B", "abc", 0, Pcre2CompileOptions.None, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.PartialMatch, 0, 3];
        yield return ["abc\\b", "x", 1, Pcre2CompileOptions.None, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["abc\\b", "x", 1, Pcre2CompileOptions.None, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.PartialMatch, 1, 1];
        yield return ["abc\\b", "x", 1, Pcre2CompileOptions.None, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.PartialMatch, 1, 1];
        yield return ["abc\\b", "x", 1, Pcre2CompileOptions.Ucp, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["abc\\b", "x", 1, Pcre2CompileOptions.Ucp, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.PartialMatch, 1, 1];
        yield return ["abc\\b", "x", 1, Pcre2CompileOptions.Ucp, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.PartialMatch, 1, 1];
        yield return ["\\babc\\b", "x", 1, Pcre2CompileOptions.None, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["\\babc\\b", "x", 1, Pcre2CompileOptions.None, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.PartialMatch, 1, 1];
        yield return ["\\babc\\b", "x", 1, Pcre2CompileOptions.None, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.PartialMatch, 1, 1];
        yield return ["\\babc\\b", "", 0, Pcre2CompileOptions.Ucp, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["\\babc\\b", "", 0, Pcre2CompileOptions.Ucp, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.PartialMatch, 0, 0];
        yield return ["\\babc\\b", "", 0, Pcre2CompileOptions.Ucp, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.PartialMatch, 0, 0];
        yield return ["\ud83d\ude00\\B", "", 0, Pcre2CompileOptions.Ucp, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["\ud83d\ude00\\B", "", 0, Pcre2CompileOptions.Ucp, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.PartialMatch, 0, 0];
        yield return ["\ud83d\ude00\\B", "", 0, Pcre2CompileOptions.Ucp, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.PartialMatch, 0, 0];
        yield return ["\ud83d\ude00\\B", "x", 1, Pcre2CompileOptions.Ucp, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.Anchored, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["\ud83d\ude00\\B", "x", 1, Pcre2CompileOptions.Ucp, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.Anchored, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["\ud83d\ude00\\B", "x", 1, Pcre2CompileOptions.Ucp, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.Anchored, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["\ud83d\ude00\\B", "\ud83d\ude00\u00e9", 0, Pcre2CompileOptions.None, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.FullMatch, 0, 4];
        yield return ["\ud83d\ude00\\B", "\ud83d\ude00\u00e9", 0, Pcre2CompileOptions.None, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.FullMatch, 0, 4];
        yield return ["\ud83d\ude00\\B", "\ud83d\ude00\u00e9", 0, Pcre2CompileOptions.None, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.FullMatch, 0, 4];
        yield return ["abc\\B", "x", 0, Pcre2CompileOptions.Ucp, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["abc\\B", "x", 0, Pcre2CompileOptions.Ucp, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.PartialMatch, 1, 1];
        yield return ["abc\\B", "x", 0, Pcre2CompileOptions.Ucp, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.PartialMatch, 1, 1];
        yield return ["^\\babc\\b", "", 0, Pcre2CompileOptions.Multiline, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["^\\babc\\b", "", 0, Pcre2CompileOptions.Multiline, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.PartialMatch, 0, 0];
        yield return ["^\\babc\\b", "", 0, Pcre2CompileOptions.Multiline, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.PartialMatch, 0, 0];
        yield return ["^\\babc\\b", "", 0, Pcre2CompileOptions.None, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["^\\babc\\b", "", 0, Pcre2CompileOptions.None, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["^\\babc\\b", "", 0, Pcre2CompileOptions.None, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["\\babc\\b", "x", 1, Pcre2CompileOptions.None, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.Anchored, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["\\babc\\b", "x", 1, Pcre2CompileOptions.None, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.Anchored, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["\\babc\\b", "x", 1, Pcre2CompileOptions.None, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.Anchored, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
    }

    [Theory]
    [MemberData(nameof(NativeCases))]
    public void LiteralAssertionsPreserveNativeCandidatesAndScalarBoundaries(string pattern, string text, int start,
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
    public void LaterScalarBoundaryFullPreservesFiniteAdmissionAndRetainedAlias()
    {
        var capacity = Utf8ConstructionBudget.ArrayBytes<Pcre2GroupData>(1);
        Utf8Pcre2Regex Create(ulong bytes) => Utf8Pcre2Regex.Create(@"abc\b", Pcre2CompileOptions.Ucp, default,
            new() { MaxWorkSteps = 100000, MaxDepth = 256, MaxWorkingBytes = 1 << 20, MaxResultBytes = bytes },
            Timeout.InfiniteTimeSpan);
        Assert.Equal(Pcre2ErrorKind.ResultLimit, Assert.Throws<Pcre2MatchException>(() =>
        { _ = Create(capacity - 1).Probe("abcé abc!"u8, Pcre2PartialMode.Soft); }).ErrorKind);
        var regex = Create(capacity);
        var retained = regex.Probe("abcé abc!"u8, Pcre2PartialMode.Soft).GetMatch();
        var alias = retained.GetGroup(0);
        Assert.Equal((6, 9), (alias.StartOffsetInBytes, alias.EndOffsetInBytes));
        Assert.Equal(Utf8Pcre2ProbeKind.PartialMatch, regex.Probe("abcé"u8, Pcre2PartialMode.Soft).Kind);
        Assert.Equal("abc", retained.GetValueString());
        Assert.Equal("abc", alias.GetValueString());
        Assert.Equal(Utf8Pcre2ProbeKind.FullMatch, regex.Probe("abcé abc!"u8, Pcre2PartialMode.Soft).Kind);
        Assert.Equal(Utf8Pcre2ProbeKind.PartialMatch, Create(1).Probe(""u8, Pcre2PartialMode.Soft).Kind);
    }

    [Fact]
    public void FailedBoundaryCandidatesCannotBypassFiniteWork()
    {
        var regex = new Utf8Pcre2Regex(@"abc\b", Pcre2CompileOptions.Ucp, default,
            new() { MatchLimit = 20 }, Timeout.InfiniteTimeSpan);
        var input = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat("abcx ", 100)) + "abc!");
        Assert.Equal(Pcre2ErrorKind.MatchLimit, Assert.Throws<Pcre2MatchException>(() =>
        { _ = regex.Probe(input, Pcre2PartialMode.Soft); }).ErrorKind);
        Assert.Equal(Utf8Pcre2ProbeKind.FullMatch, regex.Probe("abc!"u8, Pcre2PartialMode.Soft).Kind);
    }

    [Theory]
    [InlineData(4094)] [InlineData(4095)] [InlineData(4096)]
    public void ControlledLiteralSearchKeepsChunkBoundaryAndPartialTail(int offset)
    {
        var regex = new Utf8Pcre2Regex(@"abc\z", Pcre2CompileOptions.None, default,
            new() { MatchLimit = 100000, HeapLimitInBytes = 4096, ResultLimitInBytes = 4096 }, Timeout.InfiniteTimeSpan);
        var full = regex.Probe(Encoding.UTF8.GetBytes(new string('x', offset) + "abc"), Pcre2PartialMode.Soft);
        Assert.Equal(Utf8Pcre2ProbeKind.FullMatch, full.Kind);
        Assert.Equal((offset, offset + 3), (full.Value.StartOffsetInBytes, full.Value.EndOffsetInBytes));
        var partial = regex.Probe(Encoding.UTF8.GetBytes(new string('x', offset) + "ab"), Pcre2PartialMode.Hard);
        Assert.Equal(Utf8Pcre2ProbeKind.PartialMatch, partial.Kind);
        Assert.Equal((offset, offset + 2), (partial.Value.StartOffsetInBytes, partial.Value.EndOffsetInBytes));
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void BoundaryCandidateSearchObservesOriginalInterruption(bool cancel)
    {
        using var source = new CancellationTokenSource();
        var regex = new Utf8Pcre2Regex(@"abc\b", Pcre2CompileOptions.Ucp, default, default, TimeSpan.FromMilliseconds(20));
        var polls = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() =>
        {
            if (++polls < 10) return 0;
            if (cancel) { source.Cancel(); return 0; }
            return 25;
        });
        Exception? failure = null;
        try { _ = regex.Probe("abcx abcx abcx abcx abcx abcx abc!"u8, Pcre2PartialMode.Soft, 0, Pcre2MatchOptions.None, source.Token); }
        catch (Exception error) { failure = error; }
        if (cancel) Assert.Equal(source.Token, Assert.IsType<OperationCanceledException>(failure).CancellationToken);
        else Assert.Equal(Pcre2ErrorKind.Timeout, Assert.IsType<Pcre2MatchException>(failure).ErrorKind);
    }
}
