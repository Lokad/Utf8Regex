using System.Text;
using Lokad.Utf8Regex.Internal;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Pcre2.Tests;

[Collection(Pcre2AllocationTestCollection.Name)]
public sealed class Pcre2FinalNewlineProbeTests
{
    // Frozen PCRE2 10.47 / private PCRE.NET 1.5.0 outcomes; no native test dependency.
    public static IEnumerable<object[]> NativeCases()
    {
        yield return ["\\Z", "x\r\n", 0, Pcre2CompileOptions.None, Pcre2NewlineConvention.Crlf, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.FullMatch, 1, 1];
        yield return ["\\Z", "x\r\n", 0, Pcre2CompileOptions.None, Pcre2NewlineConvention.Crlf, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.FullMatch, 1, 1];
        yield return ["\\Z", "x\r\n", 0, Pcre2CompileOptions.None, Pcre2NewlineConvention.Crlf, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.PartialMatch, 1, 3];
        yield return ["\\Z", "x\r\n", 0, Pcre2CompileOptions.FirstLine, Pcre2NewlineConvention.Crlf, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.FullMatch, 1, 1];
        yield return ["\\Z", "x\r\n", 0, Pcre2CompileOptions.FirstLine, Pcre2NewlineConvention.Crlf, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.FullMatch, 1, 1];
        yield return ["\\Z", "x\r\n", 0, Pcre2CompileOptions.FirstLine, Pcre2NewlineConvention.Crlf, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.PartialMatch, 1, 3];
        yield return ["\\Z", "x\r", 0, Pcre2CompileOptions.None, Pcre2NewlineConvention.Crlf, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.FullMatch, 2, 2];
        yield return ["\\Z", "x\r", 0, Pcre2CompileOptions.None, Pcre2NewlineConvention.Crlf, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.FullMatch, 2, 2];
        yield return ["\\Z", "x\r", 0, Pcre2CompileOptions.None, Pcre2NewlineConvention.Crlf, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.PartialMatch, 1, 2];
        yield return ["\\Z", "x\r", 0, Pcre2CompileOptions.FirstLine, Pcre2NewlineConvention.Crlf, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.FullMatch, 2, 2];
        yield return ["\\Z", "x\r", 0, Pcre2CompileOptions.FirstLine, Pcre2NewlineConvention.Crlf, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.FullMatch, 2, 2];
        yield return ["\\Z", "x\r", 0, Pcre2CompileOptions.FirstLine, Pcre2NewlineConvention.Crlf, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.PartialMatch, 1, 2];
        yield return ["\\Z", "x\r\n", 0, Pcre2CompileOptions.None, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.FullMatch, 2, 2];
        yield return ["\\Z", "x\r\n", 0, Pcre2CompileOptions.None, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.FullMatch, 2, 2];
        yield return ["\\Z", "x\r\n", 0, Pcre2CompileOptions.None, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.PartialMatch, 2, 3];
        yield return ["\\Z", "x\r\n", 0, Pcre2CompileOptions.FirstLine, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.FullMatch, 2, 2];
        yield return ["\\Z", "x\r\n", 0, Pcre2CompileOptions.FirstLine, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.FullMatch, 2, 2];
        yield return ["\\Z", "x\r\n", 0, Pcre2CompileOptions.FirstLine, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.PartialMatch, 2, 3];
        yield return ["\\Z", "x\u2028", 0, Pcre2CompileOptions.None, Pcre2NewlineConvention.Any, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.FullMatch, 1, 1];
        yield return ["\\Z", "x\u2028", 0, Pcre2CompileOptions.None, Pcre2NewlineConvention.Any, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.FullMatch, 1, 1];
        yield return ["\\Z", "x\u2028", 0, Pcre2CompileOptions.None, Pcre2NewlineConvention.Any, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.PartialMatch, 1, 4];
        yield return ["\\Z", "x\u2028", 0, Pcre2CompileOptions.FirstLine, Pcre2NewlineConvention.Any, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.FullMatch, 1, 1];
        yield return ["\\Z", "x\u2028", 0, Pcre2CompileOptions.FirstLine, Pcre2NewlineConvention.Any, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.FullMatch, 1, 1];
        yield return ["\\Z", "x\u2028", 0, Pcre2CompileOptions.FirstLine, Pcre2NewlineConvention.Any, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.PartialMatch, 1, 4];
        yield return ["\\Z", "x\u0085", 0, Pcre2CompileOptions.None, Pcre2NewlineConvention.Any, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.FullMatch, 1, 1];
        yield return ["\\Z", "x\u0085", 0, Pcre2CompileOptions.None, Pcre2NewlineConvention.Any, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.FullMatch, 1, 1];
        yield return ["\\Z", "x\u0085", 0, Pcre2CompileOptions.None, Pcre2NewlineConvention.Any, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.PartialMatch, 1, 3];
        yield return ["\\Z", "x\u0085", 0, Pcre2CompileOptions.FirstLine, Pcre2NewlineConvention.Any, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.FullMatch, 1, 1];
        yield return ["\\Z", "x\u0085", 0, Pcre2CompileOptions.FirstLine, Pcre2NewlineConvention.Any, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.FullMatch, 1, 1];
        yield return ["\\Z", "x\u0085", 0, Pcre2CompileOptions.FirstLine, Pcre2NewlineConvention.Any, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.PartialMatch, 1, 3];
        yield return ["\\Z", "x\r", 0, Pcre2CompileOptions.None, Pcre2NewlineConvention.Any, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.FullMatch, 1, 1];
        yield return ["\\Z", "x\r", 0, Pcre2CompileOptions.None, Pcre2NewlineConvention.Any, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.FullMatch, 1, 1];
        yield return ["\\Z", "x\r", 0, Pcre2CompileOptions.None, Pcre2NewlineConvention.Any, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.PartialMatch, 1, 2];
        yield return ["\\Z", "x\r", 0, Pcre2CompileOptions.FirstLine, Pcre2NewlineConvention.Any, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.FullMatch, 1, 1];
        yield return ["\\Z", "x\r", 0, Pcre2CompileOptions.FirstLine, Pcre2NewlineConvention.Any, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.FullMatch, 1, 1];
        yield return ["\\Z", "x\r", 0, Pcre2CompileOptions.FirstLine, Pcre2NewlineConvention.Any, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.PartialMatch, 1, 2];
        yield return ["\\Z", "x\r", 0, Pcre2CompileOptions.None, Pcre2NewlineConvention.AnyCrlf, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.FullMatch, 1, 1];
        yield return ["\\Z", "x\r", 0, Pcre2CompileOptions.None, Pcre2NewlineConvention.AnyCrlf, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.FullMatch, 1, 1];
        yield return ["\\Z", "x\r", 0, Pcre2CompileOptions.None, Pcre2NewlineConvention.AnyCrlf, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.PartialMatch, 1, 2];
        yield return ["\\Z", "x\r", 0, Pcre2CompileOptions.FirstLine, Pcre2NewlineConvention.AnyCrlf, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.FullMatch, 1, 1];
        yield return ["\\Z", "x\r", 0, Pcre2CompileOptions.FirstLine, Pcre2NewlineConvention.AnyCrlf, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.FullMatch, 1, 1];
        yield return ["\\Z", "x\r", 0, Pcre2CompileOptions.FirstLine, Pcre2NewlineConvention.AnyCrlf, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.PartialMatch, 1, 2];
        yield return ["\\Z", "x\r", 0, Pcre2CompileOptions.None, Pcre2NewlineConvention.Cr, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.FullMatch, 1, 1];
        yield return ["\\Z", "x\r", 0, Pcre2CompileOptions.None, Pcre2NewlineConvention.Cr, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.FullMatch, 1, 1];
        yield return ["\\Z", "x\r", 0, Pcre2CompileOptions.None, Pcre2NewlineConvention.Cr, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.PartialMatch, 1, 2];
        yield return ["\\Z", "x\r", 0, Pcre2CompileOptions.FirstLine, Pcre2NewlineConvention.Cr, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.FullMatch, 1, 1];
        yield return ["\\Z", "x\r", 0, Pcre2CompileOptions.FirstLine, Pcre2NewlineConvention.Cr, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.FullMatch, 1, 1];
        yield return ["\\Z", "x\r", 0, Pcre2CompileOptions.FirstLine, Pcre2NewlineConvention.Cr, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.PartialMatch, 1, 2];
        yield return ["\\Z", "x\n", 0, Pcre2CompileOptions.None, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.FullMatch, 1, 1];
        yield return ["\\Z", "x\n", 0, Pcre2CompileOptions.None, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.FullMatch, 1, 1];
        yield return ["\\Z", "x\n", 0, Pcre2CompileOptions.None, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.PartialMatch, 1, 2];
        yield return ["\\Z", "x\n", 0, Pcre2CompileOptions.FirstLine, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.FullMatch, 1, 1];
        yield return ["\\Z", "x\n", 0, Pcre2CompileOptions.FirstLine, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.FullMatch, 1, 1];
        yield return ["\\Z", "x\n", 0, Pcre2CompileOptions.FirstLine, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.PartialMatch, 1, 2];
        yield return ["\\Z", "x\u0000", 0, Pcre2CompileOptions.None, Pcre2NewlineConvention.Nul, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.FullMatch, 1, 1];
        yield return ["\\Z", "x\u0000", 0, Pcre2CompileOptions.None, Pcre2NewlineConvention.Nul, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.FullMatch, 1, 1];
        yield return ["\\Z", "x\u0000", 0, Pcre2CompileOptions.None, Pcre2NewlineConvention.Nul, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.PartialMatch, 1, 2];
        yield return ["\\Z", "x\u0000", 0, Pcre2CompileOptions.FirstLine, Pcre2NewlineConvention.Nul, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.FullMatch, 1, 1];
        yield return ["\\Z", "x\u0000", 0, Pcre2CompileOptions.FirstLine, Pcre2NewlineConvention.Nul, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.FullMatch, 1, 1];
        yield return ["\\Z", "x\u0000", 0, Pcre2CompileOptions.FirstLine, Pcre2NewlineConvention.Nul, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.PartialMatch, 1, 2];
        yield return ["\\Z", "x\r\n", 1, Pcre2CompileOptions.None, Pcre2NewlineConvention.Crlf, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.FullMatch, 1, 1];
        yield return ["\\Z", "x\r\n", 1, Pcre2CompileOptions.None, Pcre2NewlineConvention.Crlf, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.FullMatch, 1, 1];
        yield return ["\\Z", "x\r\n", 1, Pcre2CompileOptions.None, Pcre2NewlineConvention.Crlf, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.PartialMatch, 1, 3];
        yield return ["\\Z", "x\r\n", 2, Pcre2CompileOptions.None, Pcre2NewlineConvention.Crlf, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.FullMatch, 3, 3];
        yield return ["\\Z", "x\r\n", 2, Pcre2CompileOptions.None, Pcre2NewlineConvention.Crlf, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.FullMatch, 3, 3];
        yield return ["\\Z", "x\r\n", 2, Pcre2CompileOptions.None, Pcre2NewlineConvention.Crlf, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.PartialMatch, 3, 3];
        yield return ["\\Z", "x\r\n", 3, Pcre2CompileOptions.None, Pcre2NewlineConvention.Crlf, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.FullMatch, 3, 3];
        yield return ["\\Z", "x\r\n", 3, Pcre2CompileOptions.None, Pcre2NewlineConvention.Crlf, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.FullMatch, 3, 3];
        yield return ["\\Z", "x\r\n", 3, Pcre2CompileOptions.None, Pcre2NewlineConvention.Crlf, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.PartialMatch, 3, 3];
        yield return ["\\Z", "x\u2028", 4, Pcre2CompileOptions.None, Pcre2NewlineConvention.Any, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.FullMatch, 4, 4];
        yield return ["\\Z", "x\u2028", 4, Pcre2CompileOptions.None, Pcre2NewlineConvention.Any, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.FullMatch, 4, 4];
        yield return ["\\Z", "x\u2028", 4, Pcre2CompileOptions.None, Pcre2NewlineConvention.Any, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.PartialMatch, 4, 4];
        yield return ["\\Z", "", 0, Pcre2CompileOptions.None, Pcre2NewlineConvention.Any, Pcre2MatchOptions.NotEmpty, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["\\Z", "", 0, Pcre2CompileOptions.None, Pcre2NewlineConvention.Any, Pcre2MatchOptions.NotEmpty, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.PartialMatch, 0, 0];
        yield return ["\\Z", "", 0, Pcre2CompileOptions.None, Pcre2NewlineConvention.Any, Pcre2MatchOptions.NotEmpty, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.PartialMatch, 0, 0];
        yield return ["\\Z", "", 0, Pcre2CompileOptions.None, Pcre2NewlineConvention.Any, Pcre2MatchOptions.NotEmptyAtStart, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["\\Z", "", 0, Pcre2CompileOptions.None, Pcre2NewlineConvention.Any, Pcre2MatchOptions.NotEmptyAtStart, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.PartialMatch, 0, 0];
        yield return ["\\Z", "", 0, Pcre2CompileOptions.None, Pcre2NewlineConvention.Any, Pcre2MatchOptions.NotEmptyAtStart, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.PartialMatch, 0, 0];
        yield return ["\\Z", "\ud83d\ude00", 0, Pcre2CompileOptions.None, Pcre2NewlineConvention.Any, Pcre2MatchOptions.NotEmpty, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["\\Z", "\ud83d\ude00", 0, Pcre2CompileOptions.None, Pcre2NewlineConvention.Any, Pcre2MatchOptions.NotEmpty, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.PartialMatch, 4, 4];
        yield return ["\\Z", "\ud83d\ude00", 0, Pcre2CompileOptions.None, Pcre2NewlineConvention.Any, Pcre2MatchOptions.NotEmpty, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.PartialMatch, 4, 4];
        yield return ["\\Z", "\ud83d\ude00", 0, Pcre2CompileOptions.None, Pcre2NewlineConvention.Any, Pcre2MatchOptions.NotEmptyAtStart, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.FullMatch, 4, 4];
        yield return ["\\Z", "\ud83d\ude00", 0, Pcre2CompileOptions.None, Pcre2NewlineConvention.Any, Pcre2MatchOptions.NotEmptyAtStart, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.FullMatch, 4, 4];
        yield return ["\\Z", "\ud83d\ude00", 0, Pcre2CompileOptions.None, Pcre2NewlineConvention.Any, Pcre2MatchOptions.NotEmptyAtStart, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.PartialMatch, 4, 4];
        yield return ["\\Z", "x\r\n", 0, Pcre2CompileOptions.None, Pcre2NewlineConvention.Crlf, Pcre2MatchOptions.NotEmpty, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["\\Z", "x\r\n", 0, Pcre2CompileOptions.None, Pcre2NewlineConvention.Crlf, Pcre2MatchOptions.NotEmpty, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.PartialMatch, 1, 3];
        yield return ["\\Z", "x\r\n", 0, Pcre2CompileOptions.None, Pcre2NewlineConvention.Crlf, Pcre2MatchOptions.NotEmpty, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.PartialMatch, 1, 3];
        yield return ["\\Z", "x\r\n", 0, Pcre2CompileOptions.None, Pcre2NewlineConvention.Crlf, Pcre2MatchOptions.NotEmptyAtStart, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.FullMatch, 1, 1];
        yield return ["\\Z", "x\r\n", 0, Pcre2CompileOptions.None, Pcre2NewlineConvention.Crlf, Pcre2MatchOptions.NotEmptyAtStart, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.FullMatch, 1, 1];
        yield return ["\\Z", "x\r\n", 0, Pcre2CompileOptions.None, Pcre2NewlineConvention.Crlf, Pcre2MatchOptions.NotEmptyAtStart, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.PartialMatch, 1, 3];
        yield return ["\\Z", "x\r", 0, Pcre2CompileOptions.None, Pcre2NewlineConvention.Crlf, Pcre2MatchOptions.NotEmpty, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["\\Z", "x\r", 0, Pcre2CompileOptions.None, Pcre2NewlineConvention.Crlf, Pcre2MatchOptions.NotEmpty, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.PartialMatch, 1, 2];
        yield return ["\\Z", "x\r", 0, Pcre2CompileOptions.None, Pcre2NewlineConvention.Crlf, Pcre2MatchOptions.NotEmpty, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.PartialMatch, 1, 2];
        yield return ["\\Z", "x\r", 0, Pcre2CompileOptions.None, Pcre2NewlineConvention.Crlf, Pcre2MatchOptions.NotEmptyAtStart, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.FullMatch, 2, 2];
        yield return ["\\Z", "x\r", 0, Pcre2CompileOptions.None, Pcre2NewlineConvention.Crlf, Pcre2MatchOptions.NotEmptyAtStart, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.FullMatch, 2, 2];
        yield return ["\\Z", "x\r", 0, Pcre2CompileOptions.None, Pcre2NewlineConvention.Crlf, Pcre2MatchOptions.NotEmptyAtStart, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.PartialMatch, 1, 2];
        yield return ["\\Z", "x\u2028", 0, Pcre2CompileOptions.None, Pcre2NewlineConvention.Any, Pcre2MatchOptions.NotEmpty, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["\\Z", "x\u2028", 0, Pcre2CompileOptions.None, Pcre2NewlineConvention.Any, Pcre2MatchOptions.NotEmpty, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.PartialMatch, 1, 4];
        yield return ["\\Z", "x\u2028", 0, Pcre2CompileOptions.None, Pcre2NewlineConvention.Any, Pcre2MatchOptions.NotEmpty, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.PartialMatch, 1, 4];
        yield return ["\\Z", "x\u2028", 0, Pcre2CompileOptions.None, Pcre2NewlineConvention.Any, Pcre2MatchOptions.NotEmptyAtStart, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.FullMatch, 1, 1];
        yield return ["\\Z", "x\u2028", 0, Pcre2CompileOptions.None, Pcre2NewlineConvention.Any, Pcre2MatchOptions.NotEmptyAtStart, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.FullMatch, 1, 1];
        yield return ["\\Z", "x\u2028", 0, Pcre2CompileOptions.None, Pcre2NewlineConvention.Any, Pcre2MatchOptions.NotEmptyAtStart, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.PartialMatch, 1, 4];
        yield return ["\\Z", "x\r\n", 1, Pcre2CompileOptions.None, Pcre2NewlineConvention.Crlf, Pcre2MatchOptions.Anchored, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.FullMatch, 1, 1];
        yield return ["\\Z", "x\r\n", 1, Pcre2CompileOptions.None, Pcre2NewlineConvention.Crlf, Pcre2MatchOptions.Anchored, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.FullMatch, 1, 1];
        yield return ["\\Z", "x\r\n", 1, Pcre2CompileOptions.None, Pcre2NewlineConvention.Crlf, Pcre2MatchOptions.Anchored, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.PartialMatch, 1, 3];
        yield return ["\\Z", "x\r\n", 2, Pcre2CompileOptions.None, Pcre2NewlineConvention.Crlf, Pcre2MatchOptions.Anchored, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["\\Z", "x\r\n", 2, Pcre2CompileOptions.None, Pcre2NewlineConvention.Crlf, Pcre2MatchOptions.Anchored, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["\\Z", "x\r\n", 2, Pcre2CompileOptions.None, Pcre2NewlineConvention.Crlf, Pcre2MatchOptions.Anchored, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["\\Z", "x\r\n", 1, Pcre2CompileOptions.Anchored, Pcre2NewlineConvention.Crlf, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.FullMatch, 1, 1];
        yield return ["\\Z", "x\r\n", 1, Pcre2CompileOptions.Anchored, Pcre2NewlineConvention.Crlf, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.FullMatch, 1, 1];
        yield return ["\\Z", "x\r\n", 1, Pcre2CompileOptions.Anchored, Pcre2NewlineConvention.Crlf, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.PartialMatch, 1, 3];
        yield return ["\\Z", "x\r\n", 0, Pcre2CompileOptions.None, Pcre2NewlineConvention.Crlf, Pcre2MatchOptions.NotEol, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.FullMatch, 1, 1];
        yield return ["\\Z", "x\r\n", 0, Pcre2CompileOptions.None, Pcre2NewlineConvention.Crlf, Pcre2MatchOptions.NotEol, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.FullMatch, 1, 1];
        yield return ["\\Z", "x\r\n", 0, Pcre2CompileOptions.None, Pcre2NewlineConvention.Crlf, Pcre2MatchOptions.NotEol, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.PartialMatch, 1, 3];
        yield return ["abc\\Z", "abc\r\n", 0, Pcre2CompileOptions.None, Pcre2NewlineConvention.Crlf, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.FullMatch, 0, 3];
        yield return ["abc\\Z", "abc\r\n", 0, Pcre2CompileOptions.None, Pcre2NewlineConvention.Crlf, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.FullMatch, 0, 3];
        yield return ["abc\\Z", "abc\r\n", 0, Pcre2CompileOptions.None, Pcre2NewlineConvention.Crlf, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.PartialMatch, 0, 5];
        yield return ["^abc\\Z", "abc\r\n", 0, Pcre2CompileOptions.None, Pcre2NewlineConvention.Crlf, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.FullMatch, 0, 3];
        yield return ["^abc\\Z", "abc\r\n", 0, Pcre2CompileOptions.None, Pcre2NewlineConvention.Crlf, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.FullMatch, 0, 3];
        yield return ["^abc\\Z", "abc\r\n", 0, Pcre2CompileOptions.None, Pcre2NewlineConvention.Crlf, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.PartialMatch, 0, 5];
        yield return ["abc\\Z", "abc\r", 0, Pcre2CompileOptions.None, Pcre2NewlineConvention.Crlf, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["abc\\Z", "abc\r", 0, Pcre2CompileOptions.None, Pcre2NewlineConvention.Crlf, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.PartialMatch, 0, 4];
        yield return ["abc\\Z", "abc\r", 0, Pcre2CompileOptions.None, Pcre2NewlineConvention.Crlf, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.PartialMatch, 0, 4];
        yield return ["^abc\\Z", "abc\r", 0, Pcre2CompileOptions.None, Pcre2NewlineConvention.Crlf, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["^abc\\Z", "abc\r", 0, Pcre2CompileOptions.None, Pcre2NewlineConvention.Crlf, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.PartialMatch, 0, 4];
        yield return ["^abc\\Z", "abc\r", 0, Pcre2CompileOptions.None, Pcre2NewlineConvention.Crlf, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.PartialMatch, 0, 4];
        yield return ["abc\\Z", "abc\u2028", 0, Pcre2CompileOptions.None, Pcre2NewlineConvention.Any, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.FullMatch, 0, 3];
        yield return ["abc\\Z", "abc\u2028", 0, Pcre2CompileOptions.None, Pcre2NewlineConvention.Any, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.FullMatch, 0, 3];
        yield return ["abc\\Z", "abc\u2028", 0, Pcre2CompileOptions.None, Pcre2NewlineConvention.Any, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.PartialMatch, 0, 6];
        yield return ["^abc\\Z", "abc\u2028", 0, Pcre2CompileOptions.None, Pcre2NewlineConvention.Any, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.FullMatch, 0, 3];
        yield return ["^abc\\Z", "abc\u2028", 0, Pcre2CompileOptions.None, Pcre2NewlineConvention.Any, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.FullMatch, 0, 3];
        yield return ["^abc\\Z", "abc\u2028", 0, Pcre2CompileOptions.None, Pcre2NewlineConvention.Any, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.PartialMatch, 0, 6];
        yield return ["abc\\Z", "\ud83d\ude00abc\u2028", 4, Pcre2CompileOptions.FirstLine, Pcre2NewlineConvention.Any, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.FullMatch, 4, 7];
        yield return ["abc\\Z", "\ud83d\ude00abc\u2028", 4, Pcre2CompileOptions.FirstLine, Pcre2NewlineConvention.Any, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.FullMatch, 4, 7];
        yield return ["abc\\Z", "\ud83d\ude00abc\u2028", 4, Pcre2CompileOptions.FirstLine, Pcre2NewlineConvention.Any, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.PartialMatch, 4, 10];
        yield return ["abc\\Z", "abc\r", 0, Pcre2CompileOptions.None, Pcre2NewlineConvention.Crlf, Pcre2MatchOptions.NotEol, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["abc\\Z", "abc\r", 0, Pcre2CompileOptions.None, Pcre2NewlineConvention.Crlf, Pcre2MatchOptions.NotEol, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.PartialMatch, 0, 4];
        yield return ["abc\\Z", "abc\r", 0, Pcre2CompileOptions.None, Pcre2NewlineConvention.Crlf, Pcre2MatchOptions.NotEol, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.PartialMatch, 0, 4];
        yield return ["abc\\Z", "abc\r\n", 0, Pcre2CompileOptions.None, Pcre2NewlineConvention.Crlf, Pcre2MatchOptions.Anchored, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.FullMatch, 0, 3];
        yield return ["abc\\Z", "abc\r\n", 0, Pcre2CompileOptions.None, Pcre2NewlineConvention.Crlf, Pcre2MatchOptions.Anchored, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.FullMatch, 0, 3];
        yield return ["abc\\Z", "abc\r\n", 0, Pcre2CompileOptions.None, Pcre2NewlineConvention.Crlf, Pcre2MatchOptions.Anchored, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.PartialMatch, 0, 5];
        yield return ["\\z", "x\r\n", 0, Pcre2CompileOptions.None, Pcre2NewlineConvention.Crlf, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.FullMatch, 3, 3];
        yield return ["\\z", "x\r\n", 0, Pcre2CompileOptions.None, Pcre2NewlineConvention.Crlf, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.FullMatch, 3, 3];
        yield return ["\\z", "x\r\n", 0, Pcre2CompileOptions.None, Pcre2NewlineConvention.Crlf, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.PartialMatch, 3, 3];
        yield return ["\\z", "x\u2028", 0, Pcre2CompileOptions.FirstLine, Pcre2NewlineConvention.Any, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["\\z", "x\u2028", 0, Pcre2CompileOptions.FirstLine, Pcre2NewlineConvention.Any, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["\\z", "x\u2028", 0, Pcre2CompileOptions.FirstLine, Pcre2NewlineConvention.Any, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["\\Z", "x\r", 1, Pcre2CompileOptions.None, Pcre2NewlineConvention.Crlf, Pcre2MatchOptions.Anchored, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["\\Z", "x\r", 1, Pcre2CompileOptions.None, Pcre2NewlineConvention.Crlf, Pcre2MatchOptions.Anchored, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.PartialMatch, 1, 2];
        yield return ["\\Z", "x\r", 1, Pcre2CompileOptions.None, Pcre2NewlineConvention.Crlf, Pcre2MatchOptions.Anchored, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.PartialMatch, 1, 2];
        yield return ["\\Z", "x\r", 2, Pcre2CompileOptions.None, Pcre2NewlineConvention.Crlf, Pcre2MatchOptions.Anchored, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.FullMatch, 2, 2];
        yield return ["\\Z", "x\r", 2, Pcre2CompileOptions.None, Pcre2NewlineConvention.Crlf, Pcre2MatchOptions.Anchored, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.FullMatch, 2, 2];
        yield return ["\\Z", "x\r", 2, Pcre2CompileOptions.None, Pcre2NewlineConvention.Crlf, Pcre2MatchOptions.Anchored, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.PartialMatch, 2, 2];
        yield return ["\\Z", "x\r\n", 2, Pcre2CompileOptions.None, Pcre2NewlineConvention.Any, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.FullMatch, 2, 2];
        yield return ["\\Z", "x\r\n", 2, Pcre2CompileOptions.None, Pcre2NewlineConvention.Any, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.FullMatch, 2, 2];
        yield return ["\\Z", "x\r\n", 2, Pcre2CompileOptions.None, Pcre2NewlineConvention.Any, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.PartialMatch, 2, 3];
        yield return ["\\Z", "x\r\n", 2, Pcre2CompileOptions.FirstLine, Pcre2NewlineConvention.Any, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.FullMatch, 2, 2];
        yield return ["\\Z", "x\r\n", 2, Pcre2CompileOptions.FirstLine, Pcre2NewlineConvention.Any, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.FullMatch, 2, 2];
        yield return ["\\Z", "x\r\n", 2, Pcre2CompileOptions.FirstLine, Pcre2NewlineConvention.Any, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.PartialMatch, 2, 3];
        yield return ["\\Z", "x\r\n", 1, Pcre2CompileOptions.FirstLine, Pcre2NewlineConvention.Crlf, Pcre2MatchOptions.NotEmptyAtStart, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["\\Z", "x\r\n", 1, Pcre2CompileOptions.FirstLine, Pcre2NewlineConvention.Crlf, Pcre2MatchOptions.NotEmptyAtStart, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.PartialMatch, 1, 3];
        yield return ["\\Z", "x\r\n", 1, Pcre2CompileOptions.FirstLine, Pcre2NewlineConvention.Crlf, Pcre2MatchOptions.NotEmptyAtStart, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.PartialMatch, 1, 3];
        yield return ["\\Z", "x\r\n", 1, Pcre2CompileOptions.None, Pcre2NewlineConvention.Crlf, Pcre2MatchOptions.NotEmptyAtStart, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.FullMatch, 3, 3];
        yield return ["\\Z", "x\r\n", 1, Pcre2CompileOptions.None, Pcre2NewlineConvention.Crlf, Pcre2MatchOptions.NotEmptyAtStart, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.FullMatch, 3, 3];
        yield return ["\\Z", "x\r\n", 1, Pcre2CompileOptions.None, Pcre2NewlineConvention.Crlf, Pcre2MatchOptions.NotEmptyAtStart, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.PartialMatch, 1, 3];
    }

    [Theory]
    [MemberData(nameof(NativeCases))]
    public void EndProbesPreserveNativeScalarNewlinesOffsetsAndPartialExtent(string pattern, string text, int start,
        Pcre2CompileOptions options, Pcre2NewlineConvention newline, Pcre2MatchOptions flags, Pcre2PartialMode mode,
        Utf8Pcre2ProbeKind kind, int begin, int end)
    {
        var regex = new Utf8Pcre2Regex(pattern, options, new() { Newline = newline }, default, Timeout.InfiniteTimeSpan);
        var input = Encoding.UTF8.GetBytes(text);
        var probe = regex.Probe(input, mode, start, flags);
        Assert.Equal(kind, probe.Kind);
        Assert.Equal((begin, end), (probe.Value.StartOffsetInBytes, probe.Value.EndOffsetInBytes));
        if (kind != Utf8Pcre2ProbeKind.FullMatch) return;
        var match = probe.GetMatch();
        Assert.Equal(1, match.CaptureSlotCount);
        Assert.Equal(Encoding.UTF8.GetString(input.AsSpan(begin, end - begin)), match.GetValueString());
        var expected = regex.MatchDetailed(input, start, flags).GetGroup(0);
        Assert.Equal((begin, end), (expected.StartOffsetInBytes, expected.EndOffsetInBytes));
    }

    [Fact]
    public void FinalNewlineHasExactResultAdmissionAndRetainedEmptyAliases()
    {
        var capacity = Utf8ConstructionBudget.ArrayBytes<Pcre2GroupData>(1);
        Utf8Pcre2Regex Create(ulong bytes) => Utf8Pcre2Regex.Create(@"\Z", Pcre2CompileOptions.FirstLine,
            new() { Newline = Pcre2NewlineConvention.Any },
            new() { MaxWorkSteps = 100000, MaxDepth = 256, MaxWorkingBytes = 1 << 20, MaxResultBytes = bytes },
            Timeout.InfiniteTimeSpan);
        Assert.Equal(Pcre2ErrorKind.ResultLimit, Assert.Throws<Pcre2MatchException>(() =>
        { _ = Create(capacity - 1).Probe("😀x\u2028"u8, Pcre2PartialMode.Soft); }).ErrorKind);
        var regex = Create(capacity);
        var retained = regex.Probe("😀x\u2028"u8, Pcre2PartialMode.Soft).GetMatch();
        var alias = retained.GetGroup(0);
        Assert.Equal((5, 5), (alias.StartOffsetInBytes, alias.EndOffsetInBytes));
        Assert.Equal((1, 1), (regex.Probe("x\n"u8, Pcre2PartialMode.Soft).GetMatch().GetGroup(0).StartOffsetInBytes,
            regex.Probe("x\n"u8, Pcre2PartialMode.Soft).GetMatch().GetGroup(0).EndOffsetInBytes));
        Assert.Equal((5, 5), (alias.StartOffsetInBytes, alias.EndOffsetInBytes));
        Assert.Equal("", retained.GetValueString());
        var partial = Create(1).Probe("😀x\u2028"u8, Pcre2PartialMode.Hard);
        Assert.Equal(Utf8Pcre2ProbeKind.PartialMatch, partial.Kind);
        Assert.Equal((5, 8), (partial.Value.StartOffsetInBytes, partial.Value.EndOffsetInBytes));
    }

    [Fact]
    public void FinalNewlineKeepsOriginalWorkAndWorkingAllowance()
    {
        Utf8Pcre2Regex Create(uint work, ulong heap) => new(@"\Z", Pcre2CompileOptions.FirstLine,
            new() { Newline = Pcre2NewlineConvention.Any },
            new() { MatchLimit = work, HeapLimitInBytes = heap }, Timeout.InfiniteTimeSpan);
        Assert.Equal(Pcre2ErrorKind.MatchLimit, Assert.Throws<Pcre2MatchException>(() =>
        { _ = Create(1, 1 << 20).Probe("x\u2028"u8, Pcre2PartialMode.Soft); }).ErrorKind);
        Assert.Equal(Pcre2ErrorKind.HeapLimit, Assert.Throws<Pcre2MatchException>(() =>
        { _ = Create(100000, 1).Probe("x\u2028"u8, Pcre2PartialMode.Soft); }).ErrorKind);
        Assert.Equal(Utf8Pcre2ProbeKind.FullMatch, Create(100000, 1 << 20).Probe("x\u2028"u8, Pcre2PartialMode.Soft).Kind);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void FinalNewlineKeepsOriginalInterruption(bool cancel)
    {
        using var source = new CancellationTokenSource();
        var regex = new Utf8Pcre2Regex(@"\Z", Pcre2CompileOptions.FirstLine,
            new() { Newline = Pcre2NewlineConvention.Any }, default, TimeSpan.FromMilliseconds(20));
        var input = Encoding.UTF8.GetBytes(new string('x', 20000) + "\u2028");
        var polls = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() =>
        {
            if (++polls < 10) return 0;
            if (cancel) { source.Cancel(); return 0; }
            return 25;
        });
        Exception? failure = null;
        try { _ = regex.Probe(input, Pcre2PartialMode.Soft, 0, Pcre2MatchOptions.None, source.Token); }
        catch (Exception error) { failure = error; }
        if (cancel) Assert.IsAssignableFrom<OperationCanceledException>(failure);
        else Assert.Equal(Pcre2ErrorKind.Timeout, Assert.IsType<Pcre2MatchException>(failure).ErrorKind);
    }

}
