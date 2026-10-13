using System.Text;
using Lokad.Utf8Regex.Internal;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Pcre2.Tests;

[Collection(Pcre2AllocationTestCollection.Name)]
public sealed class Pcre2FirstLineProbeTests
{
    // Frozen PCRE2 10.47 / private PCRE.NET 1.5.0; no native test dependency.
    public static IEnumerable<object[]> NativeCases()
    {
        yield return ["(a)b+", "x\nab", 0, Pcre2CompileOptions.FirstLine, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["(a)b+", "x\nab", 0, Pcre2CompileOptions.FirstLine, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["(a)b+", "x\nab", 0, Pcre2CompileOptions.FirstLine, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["f.*", "x\nfor", 0, Pcre2CompileOptions.FirstLine, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["f.*", "x\nfor", 0, Pcre2CompileOptions.FirstLine, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["f.*", "x\nfor", 0, Pcre2CompileOptions.FirstLine, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["cat|horse", "x\nhorse", 0, Pcre2CompileOptions.FirstLine, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["cat|horse", "x\nhorse", 0, Pcre2CompileOptions.FirstLine, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["cat|horse", "x\nhorse", 0, Pcre2CompileOptions.FirstLine, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["dogsbody|dog", "x\ndog", 0, Pcre2CompileOptions.FirstLine, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["dogsbody|dog", "x\ndog", 0, Pcre2CompileOptions.FirstLine, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["dogsbody|dog", "x\ndog", 0, Pcre2CompileOptions.FirstLine, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["abc\\K123", "x\nabc123", 0, Pcre2CompileOptions.FirstLine, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["abc\\K123", "x\nabc123", 0, Pcre2CompileOptions.FirstLine, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["abc\\K123", "x\nabc123", 0, Pcre2CompileOptions.FirstLine, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["(?<=abc)123", "x\nabc123", 0, Pcre2CompileOptions.FirstLine, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["(?<=abc)123", "x\nabc123", 0, Pcre2CompileOptions.FirstLine, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["(?<=abc)123", "x\nabc123", 0, Pcre2CompileOptions.FirstLine, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["(?<=pqr)abc(?=xyz)", "x\npqrabcxyz", 0, Pcre2CompileOptions.FirstLine, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["(?<=pqr)abc(?=xyz)", "x\npqrabcxyz", 0, Pcre2CompileOptions.FirstLine, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["(?<=pqr)abc(?=xyz)", "x\npqrabcxyz", 0, Pcre2CompileOptions.FirstLine, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["\\z", "x\ny", 0, Pcre2CompileOptions.FirstLine, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["\\z", "x\ny", 0, Pcre2CompileOptions.FirstLine, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["\\z", "x\ny", 0, Pcre2CompileOptions.FirstLine, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["\\Z", "x\n", 0, Pcre2CompileOptions.FirstLine, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.FullMatch, 1, 1];
        yield return ["\\Z", "x\n", 0, Pcre2CompileOptions.FirstLine, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.FullMatch, 1, 1];
        yield return ["\\Z", "x\n", 0, Pcre2CompileOptions.FirstLine, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.PartialMatch, 1, 2];
        yield return ["c*+(?<=[bc])", "x\nc", 0, Pcre2CompileOptions.FirstLine, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["c*+(?<=[bc])", "x\nc", 0, Pcre2CompileOptions.FirstLine, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["c*+(?<=[bc])", "x\nc", 0, Pcre2CompileOptions.FirstLine, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["c++(?<=[bc])", "x\nc", 0, Pcre2CompileOptions.FirstLine, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["c++(?<=[bc])", "x\nc", 0, Pcre2CompileOptions.FirstLine, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["c++(?<=[bc])", "x\nc", 0, Pcre2CompileOptions.FirstLine, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["(?![ab]).*", "ab\nfoo", 0, Pcre2CompileOptions.FirstLine, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.FullMatch, 2, 2];
        yield return ["(?![ab]).*", "ab\nfoo", 0, Pcre2CompileOptions.FirstLine, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.FullMatch, 2, 2];
        yield return ["(?![ab]).*", "ab\nfoo", 0, Pcre2CompileOptions.FirstLine, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.FullMatch, 2, 2];
        yield return ["(?>a+b)", "x\nab", 0, Pcre2CompileOptions.FirstLine, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["(?>a+b)", "x\nab", 0, Pcre2CompileOptions.FirstLine, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["(?>a+b)", "x\nab", 0, Pcre2CompileOptions.FirstLine, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["(abc)(?1)", "x\nabcabc", 0, Pcre2CompileOptions.FirstLine, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["(abc)(?1)", "x\nabcabc", 0, Pcre2CompileOptions.FirstLine, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["(abc)(?1)", "x\nabcabc", 0, Pcre2CompileOptions.FirstLine, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["(?(?=abc).*|Z)", "x\nZ", 0, Pcre2CompileOptions.FirstLine, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["(?(?=abc).*|Z)", "x\nZ", 0, Pcre2CompileOptions.FirstLine, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["(?(?=abc).*|Z)", "x\nZ", 0, Pcre2CompileOptions.FirstLine, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["(abc)++x", "x\nabcx", 0, Pcre2CompileOptions.FirstLine, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["(abc)++x", "x\nabcx", 0, Pcre2CompileOptions.FirstLine, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["(abc)++x", "x\nabcx", 0, Pcre2CompileOptions.FirstLine, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["abc\\b", "x\nabc", 0, Pcre2CompileOptions.FirstLine, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["abc\\b", "x\nabc", 0, Pcre2CompileOptions.FirstLine, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["abc\\b", "x\nabc", 0, Pcre2CompileOptions.FirstLine, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["abc\\B", "x\nabc", 0, Pcre2CompileOptions.FirstLine, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["abc\\B", "x\nabc", 0, Pcre2CompileOptions.FirstLine, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["abc\\B", "x\nabc", 0, Pcre2CompileOptions.FirstLine, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["foo", "x\nfoo", 0, Pcre2CompileOptions.FirstLine, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["foo", "x\nfoo", 0, Pcre2CompileOptions.FirstLine, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["foo", "x\nfoo", 0, Pcre2CompileOptions.FirstLine, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["cat|horse", "x\nho", 0, Pcre2CompileOptions.FirstLine, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["cat|horse", "x\nho", 0, Pcre2CompileOptions.FirstLine, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["cat|horse", "x\nho", 0, Pcre2CompileOptions.FirstLine, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["dogsbody|dog", "x\ndo", 0, Pcre2CompileOptions.FirstLine, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["dogsbody|dog", "x\ndo", 0, Pcre2CompileOptions.FirstLine, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["dogsbody|dog", "x\ndo", 0, Pcre2CompileOptions.FirstLine, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["abc\\b", "x\n", 0, Pcre2CompileOptions.FirstLine, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["abc\\b", "x\n", 0, Pcre2CompileOptions.FirstLine, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["abc\\b", "x\n", 0, Pcre2CompileOptions.FirstLine, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["abc\\b", "", 0, Pcre2CompileOptions.FirstLine, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["abc\\b", "", 0, Pcre2CompileOptions.FirstLine, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.PartialMatch, 0, 0];
        yield return ["abc\\b", "", 0, Pcre2CompileOptions.FirstLine, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.PartialMatch, 0, 0];
        yield return ["f.*", "for\nend", 0, Pcre2CompileOptions.FirstLine, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.FullMatch, 0, 3];
        yield return ["f.*", "for\nend", 0, Pcre2CompileOptions.FirstLine, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.FullMatch, 0, 3];
        yield return ["f.*", "for\nend", 0, Pcre2CompileOptions.FirstLine, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.FullMatch, 0, 3];
        yield return ["(?<=pqr)abc(?=xyz)", "pqrabcxyz\nq", 0, Pcre2CompileOptions.FirstLine, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.FullMatch, 3, 6];
        yield return ["(?<=pqr)abc(?=xyz)", "pqrabcxyz\nq", 0, Pcre2CompileOptions.FirstLine, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.FullMatch, 3, 6];
        yield return ["(?<=pqr)abc(?=xyz)", "pqrabcxyz\nq", 0, Pcre2CompileOptions.FirstLine, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.FullMatch, 3, 6];
        yield return ["abc\\K123", "abc123\nq", 0, Pcre2CompileOptions.FirstLine, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.FullMatch, 3, 6];
        yield return ["abc\\K123", "abc123\nq", 0, Pcre2CompileOptions.FirstLine, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.FullMatch, 3, 6];
        yield return ["abc\\K123", "abc123\nq", 0, Pcre2CompileOptions.FirstLine, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.FullMatch, 3, 6];
        yield return ["c*+(?<=[bc])", "b\nc", 0, Pcre2CompileOptions.FirstLine, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.FullMatch, 1, 1];
        yield return ["c*+(?<=[bc])", "b\nc", 0, Pcre2CompileOptions.FirstLine, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.FullMatch, 1, 1];
        yield return ["c*+(?<=[bc])", "b\nc", 0, Pcre2CompileOptions.FirstLine, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.FullMatch, 1, 1];
        yield return ["f.*", "for\nend", 0, Pcre2CompileOptions.FirstLine | Pcre2CompileOptions.DotAll, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.FullMatch, 0, 7];
        yield return ["f.*", "for\nend", 0, Pcre2CompileOptions.FirstLine | Pcre2CompileOptions.DotAll, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.FullMatch, 0, 7];
        yield return ["f.*", "for\nend", 0, Pcre2CompileOptions.FirstLine | Pcre2CompileOptions.DotAll, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.PartialMatch, 0, 7];
        yield return ["f.*", "x\nfor", 2, Pcre2CompileOptions.FirstLine, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.FullMatch, 2, 5];
        yield return ["f.*", "x\nfor", 2, Pcre2CompileOptions.FirstLine, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.FullMatch, 2, 5];
        yield return ["f.*", "x\nfor", 2, Pcre2CompileOptions.FirstLine, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.PartialMatch, 2, 5];
        yield return ["f.*", "x\nfor", 1, Pcre2CompileOptions.FirstLine, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["f.*", "x\nfor", 1, Pcre2CompileOptions.FirstLine, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["f.*", "x\nfor", 1, Pcre2CompileOptions.FirstLine, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["f.*", "x\nfor", 2, Pcre2CompileOptions.FirstLine | Pcre2CompileOptions.Anchored, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.FullMatch, 2, 5];
        yield return ["f.*", "x\nfor", 2, Pcre2CompileOptions.FirstLine | Pcre2CompileOptions.Anchored, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.FullMatch, 2, 5];
        yield return ["f.*", "x\nfor", 2, Pcre2CompileOptions.FirstLine | Pcre2CompileOptions.Anchored, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.PartialMatch, 2, 5];
        yield return ["f.*", "x\nfor", 2, Pcre2CompileOptions.FirstLine, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.Anchored, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.FullMatch, 2, 5];
        yield return ["f.*", "x\nfor", 2, Pcre2CompileOptions.FirstLine, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.Anchored, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.FullMatch, 2, 5];
        yield return ["f.*", "x\nfor", 2, Pcre2CompileOptions.FirstLine, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.Anchored, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.PartialMatch, 2, 5];
        yield return ["(?<=pqr)abc(?=xyz)", "pqrabcxyz\nq", 3, Pcre2CompileOptions.FirstLine, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.FullMatch, 3, 6];
        yield return ["(?<=pqr)abc(?=xyz)", "pqrabcxyz\nq", 3, Pcre2CompileOptions.FirstLine, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.FullMatch, 3, 6];
        yield return ["(?<=pqr)abc(?=xyz)", "pqrabcxyz\nq", 3, Pcre2CompileOptions.FirstLine, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.FullMatch, 3, 6];
        yield return ["abc\\K123", "abc123\nq", 3, Pcre2CompileOptions.FirstLine, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["abc\\K123", "abc123\nq", 3, Pcre2CompileOptions.FirstLine, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["abc\\K123", "abc123\nq", 3, Pcre2CompileOptions.FirstLine, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["(?![ab]).*", "ab\nfoo", 0, Pcre2CompileOptions.FirstLine, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.NotEmpty, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["(?![ab]).*", "ab\nfoo", 0, Pcre2CompileOptions.FirstLine, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.NotEmpty, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["(?![ab]).*", "ab\nfoo", 0, Pcre2CompileOptions.FirstLine, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.NotEmpty, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["f.*", "\ud83d\ude00\u2028for", 0, Pcre2CompileOptions.FirstLine, Pcre2NewlineConvention.Any, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["f.*", "\ud83d\ude00\u2028for", 0, Pcre2CompileOptions.FirstLine, Pcre2NewlineConvention.Any, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["f.*", "\ud83d\ude00\u2028for", 0, Pcre2CompileOptions.FirstLine, Pcre2NewlineConvention.Any, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["f.*", "\ud83d\ude00\u2028for", 0, Pcre2CompileOptions.FirstLine, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.FullMatch, 7, 10];
        yield return ["f.*", "\ud83d\ude00\u2028for", 0, Pcre2CompileOptions.FirstLine, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.FullMatch, 7, 10];
        yield return ["f.*", "\ud83d\ude00\u2028for", 0, Pcre2CompileOptions.FirstLine, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.PartialMatch, 7, 10];
        yield return ["f.*", "x\r\nfor", 0, Pcre2CompileOptions.FirstLine, Pcre2NewlineConvention.Crlf, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["f.*", "x\r\nfor", 0, Pcre2CompileOptions.FirstLine, Pcre2NewlineConvention.Crlf, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["f.*", "x\r\nfor", 0, Pcre2CompileOptions.FirstLine, Pcre2NewlineConvention.Crlf, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["f.*", "x\rfor", 0, Pcre2CompileOptions.FirstLine, Pcre2NewlineConvention.Crlf, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.FullMatch, 2, 5];
        yield return ["f.*", "x\rfor", 0, Pcre2CompileOptions.FirstLine, Pcre2NewlineConvention.Crlf, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.FullMatch, 2, 5];
        yield return ["f.*", "x\rfor", 0, Pcre2CompileOptions.FirstLine, Pcre2NewlineConvention.Crlf, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.PartialMatch, 2, 5];
        yield return ["f.*", "x\rfor", 0, Pcre2CompileOptions.FirstLine, Pcre2NewlineConvention.AnyCrlf, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["f.*", "x\rfor", 0, Pcre2CompileOptions.FirstLine, Pcre2NewlineConvention.AnyCrlf, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["f.*", "x\rfor", 0, Pcre2CompileOptions.FirstLine, Pcre2NewlineConvention.AnyCrlf, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["f.*", "x\r\nfor", 3, Pcre2CompileOptions.FirstLine, Pcre2NewlineConvention.Crlf, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.FullMatch, 3, 6];
        yield return ["f.*", "x\r\nfor", 3, Pcre2CompileOptions.FirstLine, Pcre2NewlineConvention.Crlf, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.FullMatch, 3, 6];
        yield return ["f.*", "x\r\nfor", 3, Pcre2CompileOptions.FirstLine, Pcre2NewlineConvention.Crlf, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.PartialMatch, 3, 6];
        yield return ["f.*", "x\r\nfor", 2, Pcre2CompileOptions.FirstLine, Pcre2NewlineConvention.Crlf, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.FullMatch, 3, 6];
        yield return ["f.*", "x\r\nfor", 2, Pcre2CompileOptions.FirstLine, Pcre2NewlineConvention.Crlf, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.FullMatch, 3, 6];
        yield return ["f.*", "x\r\nfor", 2, Pcre2CompileOptions.FirstLine, Pcre2NewlineConvention.Crlf, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.PartialMatch, 3, 6];
        yield return ["^\\R", "\n", 0, Pcre2CompileOptions.FirstLine, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.FullMatch, 0, 1];
        yield return ["^\\R", "\n", 0, Pcre2CompileOptions.FirstLine, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.FullMatch, 0, 1];
        yield return ["^\\R", "\n", 0, Pcre2CompileOptions.FirstLine, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.FullMatch, 0, 1];
        yield return ["^abc$", "q\nabc", 0, Pcre2CompileOptions.FirstLine | Pcre2CompileOptions.Multiline, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["^abc$", "q\nabc", 0, Pcre2CompileOptions.FirstLine | Pcre2CompileOptions.Multiline, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["^abc$", "q\nabc", 0, Pcre2CompileOptions.FirstLine | Pcre2CompileOptions.Multiline, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return [".", "\r\nq", 0, Pcre2CompileOptions.FirstLine, Pcre2NewlineConvention.Crlf, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return [".", "\r\nq", 0, Pcre2CompileOptions.FirstLine, Pcre2NewlineConvention.Crlf, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return [".", "\r\nq", 0, Pcre2CompileOptions.FirstLine, Pcre2NewlineConvention.Crlf, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return [".", "\r\nq", 0, Pcre2CompileOptions.FirstLine | Pcre2CompileOptions.DotAll, Pcre2NewlineConvention.Crlf, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.FullMatch, 0, 1];
        yield return [".", "\r\nq", 0, Pcre2CompileOptions.FirstLine | Pcre2CompileOptions.DotAll, Pcre2NewlineConvention.Crlf, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.FullMatch, 0, 1];
        yield return [".", "\r\nq", 0, Pcre2CompileOptions.FirstLine | Pcre2CompileOptions.DotAll, Pcre2NewlineConvention.Crlf, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.FullMatch, 0, 1];
        yield return ["f.*", "x\nfor", 0, Pcre2CompileOptions.None, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.FullMatch, 2, 5];
        yield return ["f.*", "x\nfor", 0, Pcre2CompileOptions.None, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.FullMatch, 2, 5];
        yield return ["f.*", "x\nfor", 0, Pcre2CompileOptions.None, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.PartialMatch, 2, 5];
        yield return ["\\z", "", 0, Pcre2CompileOptions.FirstLine, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.FullMatch, 0, 0];
        yield return ["\\z", "", 0, Pcre2CompileOptions.FirstLine, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.FullMatch, 0, 0];
        yield return ["\\z", "", 0, Pcre2CompileOptions.FirstLine, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.PartialMatch, 0, 0];
        yield return ["\\z", "", 0, Pcre2CompileOptions.FirstLine, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.NotEmpty, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["\\z", "", 0, Pcre2CompileOptions.FirstLine, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.NotEmpty, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.PartialMatch, 0, 0];
        yield return ["\\z", "", 0, Pcre2CompileOptions.FirstLine, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.NotEmpty, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.PartialMatch, 0, 0];
        yield return ["\\z", "x", 0, Pcre2CompileOptions.FirstLine, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.NotEmpty, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["\\z", "x", 0, Pcre2CompileOptions.FirstLine, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.NotEmpty, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.PartialMatch, 1, 1];
        yield return ["\\z", "x", 0, Pcre2CompileOptions.FirstLine, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.NotEmpty, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.PartialMatch, 1, 1];
        yield return ["\\Z", "x\n", 0, Pcre2CompileOptions.FirstLine, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.NotEmpty, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["\\Z", "x\n", 0, Pcre2CompileOptions.FirstLine, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.NotEmpty, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.PartialMatch, 1, 2];
        yield return ["\\Z", "x\n", 0, Pcre2CompileOptions.FirstLine, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.NotEmpty, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.PartialMatch, 1, 2];
        yield return ["\\Z", "x\n", 0, Pcre2CompileOptions.FirstLine, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.NotEmptyAtStart, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.FullMatch, 1, 1];
        yield return ["\\Z", "x\n", 0, Pcre2CompileOptions.FirstLine, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.NotEmptyAtStart, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.FullMatch, 1, 1];
        yield return ["\\Z", "x\n", 0, Pcre2CompileOptions.FirstLine, Pcre2NewlineConvention.Lf, Pcre2MatchOptions.NotEmptyAtStart, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.PartialMatch, 1, 2];
    }

    [Theory]
    [MemberData(nameof(NativeCases))]
    public void ProbeCandidatesPreserveNativeFirstLineAndWholeSubject(string pattern, string text, int start,
        Pcre2CompileOptions options, Pcre2NewlineConvention newline, Pcre2MatchOptions flags, Pcre2PartialMode mode,
        Utf8Pcre2ProbeKind kind, int begin, int end)
    {
        var regex = new Utf8Pcre2Regex(pattern, options, new() { Newline = newline }, default, Timeout.InfiniteTimeSpan);
        var input = Encoding.UTF8.GetBytes(text);
        var probe = regex.Probe(input, mode, start, flags);
        Assert.Equal(kind, probe.Kind);
        Assert.Equal((begin, end), (probe.Value.StartOffsetInBytes, probe.Value.EndOffsetInBytes));
        if (kind != Utf8Pcre2ProbeKind.FullMatch) return;
        var actual = probe.GetMatch();
        Assert.Equal(Encoding.UTF8.GetString(input.AsSpan(begin, end - begin)), actual.GetValueString());
        var expected = regex.MatchDetailed(input, start, flags);
        Assert.Equal(expected.CaptureSlotCount, actual.CaptureSlotCount);
        for (var slot = 0; slot < actual.CaptureSlotCount; slot++)
        {
            var left = expected.GetGroup(slot); var right = actual.GetGroup(slot);
            Assert.Equal(left.Success, right.Success);
            if (left.Success)
            {
                Assert.Equal(left.GetValueString(), right.GetValueString());
                Assert.Equal((left.StartOffsetInBytes, left.EndOffsetInBytes), (right.StartOffsetInBytes, right.EndOffsetInBytes));
            }
        }
    }

    [Fact]
    public void FirstLineRetainsExactResultsAndEarlierAlternativeAliases()
    {
        var capacity = Utf8ConstructionBudget.ArrayBytes<Pcre2GroupData>(1);
        Utf8Pcre2Regex Create(ulong bytes) => Utf8Pcre2Regex.Create("cat|horse", Pcre2CompileOptions.FirstLine, default,
            new() { MaxWorkSteps = 100000, MaxDepth = 256, MaxWorkingBytes = 1 << 20, MaxResultBytes = bytes },
            Timeout.InfiniteTimeSpan);
        Assert.Equal(Pcre2ErrorKind.ResultLimit, Assert.Throws<Pcre2MatchException>(() =>
        { _ = Create(capacity - 1).Probe("horse\ncat"u8, Pcre2PartialMode.Soft); }).ErrorKind);
        var regex = Create(capacity);
        var retained = regex.Probe("horse\ncat"u8, Pcre2PartialMode.Soft).GetMatch();
        var alias = retained.GetGroup(0);
        Assert.Equal((0, 5), (alias.StartOffsetInBytes, alias.EndOffsetInBytes));
        Assert.Equal("cat", regex.Probe("cat\nhorse"u8, Pcre2PartialMode.Soft).GetMatch().GetValueString());
        Assert.Equal("horse", retained.GetValueString());
        Assert.Equal("horse", alias.GetValueString());
        Assert.Equal(Utf8Pcre2ProbeKind.PartialMatch, Create(1).Probe("ho"u8, Pcre2PartialMode.Hard).Kind);
        Assert.Equal(Utf8Pcre2ProbeKind.NoMatch, Create(1).Probe("x\nho"u8, Pcre2PartialMode.Hard).Kind);
    }

    [Fact]
    public void FirstLineCandidateWalkIsMeteredWithoutEagerEarlyHitScan()
    {
        var regex = new Utf8Pcre2Regex("cat|horse", Pcre2CompileOptions.FirstLine, default,
            new() { MatchLimit = 80 }, Timeout.InfiniteTimeSpan);
        var late = Encoding.UTF8.GetBytes(new string('x', 20000) + "horse\nq");
        Assert.Equal(Pcre2ErrorKind.MatchLimit, Assert.Throws<Pcre2MatchException>(() =>
        { _ = regex.Probe(late, Pcre2PartialMode.Soft); }).ErrorKind);
        var early = Encoding.UTF8.GetBytes("horse" + new string('x', 20000) + "\ncat");
        Assert.Equal("horse", regex.Probe(early, Pcre2PartialMode.Soft).GetMatch().GetValueString());
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void FirstLineCandidateWalkKeepsOriginalInterruption(bool cancel)
    {
        using var source = new CancellationTokenSource();
        var regex = new Utf8Pcre2Regex("cat|horse", Pcre2CompileOptions.FirstLine, default, default, TimeSpan.FromMilliseconds(20));
        var input = Encoding.UTF8.GetBytes(new string('x', 20000) + "horse");
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
