using System.Text;
using Lokad.Utf8Regex.Internal;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Pcre2.Tests;

[Collection(Pcre2AllocationTestCollection.Name)]
public sealed class Pcre2InspectedEofProbeTests
{
    // Frozen PCRE2 10.47 / private PCRE.NET 1.5.0; no native test dependency.
    public static IEnumerable<object[]> NativeCases()
    {
        yield return ["abc\\K123", "", 0, Pcre2CompileOptions.None, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["abc\\K123", "", 0, Pcre2CompileOptions.None, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["abc\\K123", "", 0, Pcre2CompileOptions.None, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["abc\\K123", "a", 0, Pcre2CompileOptions.None, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["abc\\K123", "a", 0, Pcre2CompileOptions.None, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.PartialMatch, 0, 1];
        yield return ["abc\\K123", "a", 0, Pcre2CompileOptions.None, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.PartialMatch, 0, 1];
        yield return ["abc\\K123", "ab", 0, Pcre2CompileOptions.None, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["abc\\K123", "ab", 0, Pcre2CompileOptions.None, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.PartialMatch, 0, 2];
        yield return ["abc\\K123", "ab", 0, Pcre2CompileOptions.None, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.PartialMatch, 0, 2];
        yield return ["abc\\K123", "abc", 0, Pcre2CompileOptions.None, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["abc\\K123", "abc", 0, Pcre2CompileOptions.None, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.PartialMatch, 0, 3];
        yield return ["abc\\K123", "abc", 0, Pcre2CompileOptions.None, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.PartialMatch, 0, 3];
        yield return ["abc\\K123", "abc1", 0, Pcre2CompileOptions.None, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["abc\\K123", "abc1", 0, Pcre2CompileOptions.None, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.PartialMatch, 0, 4];
        yield return ["abc\\K123", "abc1", 0, Pcre2CompileOptions.None, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.PartialMatch, 0, 4];
        yield return ["abc\\K123", "abc123", 0, Pcre2CompileOptions.None, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.FullMatch, 3, 6];
        yield return ["abc\\K123", "abc123", 0, Pcre2CompileOptions.None, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.FullMatch, 3, 6];
        yield return ["abc\\K123", "abc123", 0, Pcre2CompileOptions.None, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.FullMatch, 3, 6];
        yield return ["abc\\K123", "abcX", 0, Pcre2CompileOptions.None, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["abc\\K123", "abcX", 0, Pcre2CompileOptions.None, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["abc\\K123", "abcX", 0, Pcre2CompileOptions.None, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["abc\\K123", "abX", 0, Pcre2CompileOptions.None, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["abc\\K123", "abX", 0, Pcre2CompileOptions.None, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["abc\\K123", "abX", 0, Pcre2CompileOptions.None, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["abc\\K123", "xab", 0, Pcre2CompileOptions.None, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["abc\\K123", "xab", 0, Pcre2CompileOptions.None, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.PartialMatch, 1, 3];
        yield return ["abc\\K123", "xab", 0, Pcre2CompileOptions.None, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.PartialMatch, 1, 3];
        yield return ["abc\\K123", "xabc", 0, Pcre2CompileOptions.None, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["abc\\K123", "xabc", 0, Pcre2CompileOptions.None, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.PartialMatch, 1, 4];
        yield return ["abc\\K123", "xabc", 0, Pcre2CompileOptions.None, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.PartialMatch, 1, 4];
        yield return ["(?<=abc)123", "", 0, Pcre2CompileOptions.None, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["(?<=abc)123", "", 0, Pcre2CompileOptions.None, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["(?<=abc)123", "", 0, Pcre2CompileOptions.None, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["(?<=abc)123", "ab", 0, Pcre2CompileOptions.None, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["(?<=abc)123", "ab", 0, Pcre2CompileOptions.None, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["(?<=abc)123", "ab", 0, Pcre2CompileOptions.None, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["(?<=abc)123", "abc", 0, Pcre2CompileOptions.None, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["(?<=abc)123", "abc", 0, Pcre2CompileOptions.None, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.PartialMatch, 3, 3];
        yield return ["(?<=abc)123", "abc", 0, Pcre2CompileOptions.None, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.PartialMatch, 3, 3];
        yield return ["(?<=abc)123", "abc1", 0, Pcre2CompileOptions.None, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["(?<=abc)123", "abc1", 0, Pcre2CompileOptions.None, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.PartialMatch, 3, 4];
        yield return ["(?<=abc)123", "abc1", 0, Pcre2CompileOptions.None, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.PartialMatch, 3, 4];
        yield return ["(?<=abc)123", "abc123", 0, Pcre2CompileOptions.None, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.FullMatch, 3, 6];
        yield return ["(?<=abc)123", "abc123", 0, Pcre2CompileOptions.None, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.FullMatch, 3, 6];
        yield return ["(?<=abc)123", "abc123", 0, Pcre2CompileOptions.None, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.FullMatch, 3, 6];
        yield return ["(?<=abc)123", "abcX", 0, Pcre2CompileOptions.None, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["(?<=abc)123", "abcX", 0, Pcre2CompileOptions.None, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["(?<=abc)123", "abcX", 0, Pcre2CompileOptions.None, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["(?<=pqr)abc(?=xyz)", "", 0, Pcre2CompileOptions.None, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["(?<=pqr)abc(?=xyz)", "", 0, Pcre2CompileOptions.None, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["(?<=pqr)abc(?=xyz)", "", 0, Pcre2CompileOptions.None, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["(?<=pqr)abc(?=xyz)", "pqr", 0, Pcre2CompileOptions.None, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["(?<=pqr)abc(?=xyz)", "pqr", 0, Pcre2CompileOptions.None, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.PartialMatch, 3, 3];
        yield return ["(?<=pqr)abc(?=xyz)", "pqr", 0, Pcre2CompileOptions.None, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.PartialMatch, 3, 3];
        yield return ["(?<=pqr)abc(?=xyz)", "pqra", 0, Pcre2CompileOptions.None, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["(?<=pqr)abc(?=xyz)", "pqra", 0, Pcre2CompileOptions.None, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.PartialMatch, 3, 4];
        yield return ["(?<=pqr)abc(?=xyz)", "pqra", 0, Pcre2CompileOptions.None, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.PartialMatch, 3, 4];
        yield return ["(?<=pqr)abc(?=xyz)", "pqrab", 0, Pcre2CompileOptions.None, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["(?<=pqr)abc(?=xyz)", "pqrab", 0, Pcre2CompileOptions.None, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.PartialMatch, 3, 5];
        yield return ["(?<=pqr)abc(?=xyz)", "pqrab", 0, Pcre2CompileOptions.None, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.PartialMatch, 3, 5];
        yield return ["(?<=pqr)abc(?=xyz)", "pqrabc", 0, Pcre2CompileOptions.None, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["(?<=pqr)abc(?=xyz)", "pqrabc", 0, Pcre2CompileOptions.None, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.PartialMatch, 3, 6];
        yield return ["(?<=pqr)abc(?=xyz)", "pqrabc", 0, Pcre2CompileOptions.None, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.PartialMatch, 3, 6];
        yield return ["(?<=pqr)abc(?=xyz)", "pqrabcx", 0, Pcre2CompileOptions.None, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["(?<=pqr)abc(?=xyz)", "pqrabcx", 0, Pcre2CompileOptions.None, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.PartialMatch, 3, 7];
        yield return ["(?<=pqr)abc(?=xyz)", "pqrabcx", 0, Pcre2CompileOptions.None, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.PartialMatch, 3, 7];
        yield return ["(?<=pqr)abc(?=xyz)", "pqrabcxyz", 0, Pcre2CompileOptions.None, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.FullMatch, 3, 6];
        yield return ["(?<=pqr)abc(?=xyz)", "pqrabcxyz", 0, Pcre2CompileOptions.None, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.FullMatch, 3, 6];
        yield return ["(?<=pqr)abc(?=xyz)", "pqrabcxyz", 0, Pcre2CompileOptions.None, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.FullMatch, 3, 6];
        yield return ["(?<=pqr)abc(?=xyz)", "pqrabX", 0, Pcre2CompileOptions.None, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["(?<=pqr)abc(?=xyz)", "pqrabX", 0, Pcre2CompileOptions.None, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["(?<=pqr)abc(?=xyz)", "pqrabX", 0, Pcre2CompileOptions.None, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["abc(?=xyz)", "", 0, Pcre2CompileOptions.None, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["abc(?=xyz)", "", 0, Pcre2CompileOptions.None, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["abc(?=xyz)", "", 0, Pcre2CompileOptions.None, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["abc(?=xyz)", "a", 0, Pcre2CompileOptions.None, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["abc(?=xyz)", "a", 0, Pcre2CompileOptions.None, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.PartialMatch, 0, 1];
        yield return ["abc(?=xyz)", "a", 0, Pcre2CompileOptions.None, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.PartialMatch, 0, 1];
        yield return ["abc(?=xyz)", "ab", 0, Pcre2CompileOptions.None, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["abc(?=xyz)", "ab", 0, Pcre2CompileOptions.None, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.PartialMatch, 0, 2];
        yield return ["abc(?=xyz)", "ab", 0, Pcre2CompileOptions.None, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.PartialMatch, 0, 2];
        yield return ["abc(?=xyz)", "abc", 0, Pcre2CompileOptions.None, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["abc(?=xyz)", "abc", 0, Pcre2CompileOptions.None, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.PartialMatch, 0, 3];
        yield return ["abc(?=xyz)", "abc", 0, Pcre2CompileOptions.None, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.PartialMatch, 0, 3];
        yield return ["abc(?=xyz)", "abcx", 0, Pcre2CompileOptions.None, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["abc(?=xyz)", "abcx", 0, Pcre2CompileOptions.None, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.PartialMatch, 0, 4];
        yield return ["abc(?=xyz)", "abcx", 0, Pcre2CompileOptions.None, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.PartialMatch, 0, 4];
        yield return ["abc(?=xyz)", "abcxyz", 0, Pcre2CompileOptions.None, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.FullMatch, 0, 3];
        yield return ["abc(?=xyz)", "abcxyz", 0, Pcre2CompileOptions.None, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.FullMatch, 0, 3];
        yield return ["abc(?=xyz)", "abcxyz", 0, Pcre2CompileOptions.None, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.FullMatch, 0, 3];
        yield return ["abc(?=xyz)", "abX", 0, Pcre2CompileOptions.None, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["abc(?=xyz)", "abX", 0, Pcre2CompileOptions.None, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["abc(?=xyz)", "abX", 0, Pcre2CompileOptions.None, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["abc\\K123", "\ud83d\ude00ab", 4, Pcre2CompileOptions.None, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["abc\\K123", "\ud83d\ude00ab", 4, Pcre2CompileOptions.None, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.PartialMatch, 4, 6];
        yield return ["abc\\K123", "\ud83d\ude00ab", 4, Pcre2CompileOptions.None, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.PartialMatch, 4, 6];
        yield return ["abc\\K123", "\ud83d\ude00abc", 4, Pcre2CompileOptions.None, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["abc\\K123", "\ud83d\ude00abc", 4, Pcre2CompileOptions.None, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.PartialMatch, 4, 7];
        yield return ["abc\\K123", "\ud83d\ude00abc", 4, Pcre2CompileOptions.None, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.PartialMatch, 4, 7];
        yield return ["abc\\K123", "xabc", 0, Pcre2CompileOptions.None, Pcre2MatchOptions.Anchored, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["abc\\K123", "xabc", 0, Pcre2CompileOptions.None, Pcre2MatchOptions.Anchored, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["abc\\K123", "xabc", 0, Pcre2CompileOptions.None, Pcre2MatchOptions.Anchored, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["abc\\K123", "xabc", 1, Pcre2CompileOptions.None, Pcre2MatchOptions.Anchored, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["abc\\K123", "xabc", 1, Pcre2CompileOptions.None, Pcre2MatchOptions.Anchored, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.PartialMatch, 1, 4];
        yield return ["abc\\K123", "xabc", 1, Pcre2CompileOptions.None, Pcre2MatchOptions.Anchored, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.PartialMatch, 1, 4];
        yield return ["(?<=abc)123", "abc", 3, Pcre2CompileOptions.None, Pcre2MatchOptions.Anchored, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["(?<=abc)123", "abc", 3, Pcre2CompileOptions.None, Pcre2MatchOptions.Anchored, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["(?<=abc)123", "abc", 3, Pcre2CompileOptions.None, Pcre2MatchOptions.Anchored, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["(?<=pqr)abc(?=xyz)", "pqrab", 3, Pcre2CompileOptions.None, Pcre2MatchOptions.Anchored, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["(?<=pqr)abc(?=xyz)", "pqrab", 3, Pcre2CompileOptions.None, Pcre2MatchOptions.Anchored, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.PartialMatch, 3, 5];
        yield return ["(?<=pqr)abc(?=xyz)", "pqrab", 3, Pcre2CompileOptions.None, Pcre2MatchOptions.Anchored, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.PartialMatch, 3, 5];
        yield return ["abc(?=xyz)", "xab", 0, Pcre2CompileOptions.Anchored, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["abc(?=xyz)", "xab", 0, Pcre2CompileOptions.Anchored, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["abc(?=xyz)", "xab", 0, Pcre2CompileOptions.Anchored, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["abc(?=xyz)", "xab", 1, Pcre2CompileOptions.Anchored, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["abc(?=xyz)", "xab", 1, Pcre2CompileOptions.Anchored, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.PartialMatch, 1, 3];
        yield return ["abc(?=xyz)", "xab", 1, Pcre2CompileOptions.Anchored, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.PartialMatch, 1, 3];
        yield return ["abc\\K123", "x\nab", 0, Pcre2CompileOptions.FirstLine, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["abc\\K123", "x\nab", 0, Pcre2CompileOptions.FirstLine, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["abc\\K123", "x\nab", 0, Pcre2CompileOptions.FirstLine, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["(?<=abc)123", "x\nabc", 0, Pcre2CompileOptions.FirstLine, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["(?<=abc)123", "x\nabc", 0, Pcre2CompileOptions.FirstLine, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["(?<=abc)123", "x\nabc", 0, Pcre2CompileOptions.FirstLine, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["(?<=pqr)abc(?=xyz)", "x\npqrab", 0, Pcre2CompileOptions.FirstLine, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["(?<=pqr)abc(?=xyz)", "x\npqrab", 0, Pcre2CompileOptions.FirstLine, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["(?<=pqr)abc(?=xyz)", "x\npqrab", 0, Pcre2CompileOptions.FirstLine, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["abc(?=xyz)", "x\nab", 0, Pcre2CompileOptions.FirstLine, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["abc(?=xyz)", "x\nab", 0, Pcre2CompileOptions.FirstLine, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["abc(?=xyz)", "x\nab", 0, Pcre2CompileOptions.FirstLine, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["abc\\K123", "abc", 0, Pcre2CompileOptions.None, Pcre2MatchOptions.NotEmpty, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["abc\\K123", "abc", 0, Pcre2CompileOptions.None, Pcre2MatchOptions.NotEmpty, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.PartialMatch, 0, 3];
        yield return ["abc\\K123", "abc", 0, Pcre2CompileOptions.None, Pcre2MatchOptions.NotEmpty, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.PartialMatch, 0, 3];
        yield return ["(?<=abc)123", "abc", 3, Pcre2CompileOptions.None, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["(?<=abc)123", "abc", 3, Pcre2CompileOptions.None, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.PartialMatch, 3, 3];
        yield return ["(?<=abc)123", "abc", 3, Pcre2CompileOptions.None, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.PartialMatch, 3, 3];
        yield return ["(?<=abc)123", "abc", 0, Pcre2CompileOptions.Anchored, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["(?<=abc)123", "abc", 0, Pcre2CompileOptions.Anchored, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["(?<=abc)123", "abc", 0, Pcre2CompileOptions.Anchored, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["(?<=pqr)abc(?=xyz)", "pqr", 3, Pcre2CompileOptions.None, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["(?<=pqr)abc(?=xyz)", "pqr", 3, Pcre2CompileOptions.None, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.PartialMatch, 3, 3];
        yield return ["(?<=pqr)abc(?=xyz)", "pqr", 3, Pcre2CompileOptions.None, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.PartialMatch, 3, 3];
        yield return ["(?<=pqr)abc(?=xyz)", "pqr", 3, Pcre2CompileOptions.None, Pcre2MatchOptions.Anchored, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["(?<=pqr)abc(?=xyz)", "pqr", 3, Pcre2CompileOptions.None, Pcre2MatchOptions.Anchored, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["(?<=pqr)abc(?=xyz)", "pqr", 3, Pcre2CompileOptions.None, Pcre2MatchOptions.Anchored, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
    }

    [Theory]
    [MemberData(nameof(NativeCases))]
    public void InspectedLiteralsPreserveNativeEofConsumptionAndContext(string pattern, string text, int start,
        Pcre2CompileOptions options, Pcre2MatchOptions flags, Pcre2PartialMode mode,
        Utf8Pcre2ProbeKind kind, int begin, int end)
    {
        var regex = new Utf8Pcre2Regex(pattern, options);
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
    public void InspectedEofHasExactResultAdmissionAndRetainedWholeContext()
    {
        var capacity = Utf8ConstructionBudget.ArrayBytes<Pcre2GroupData>(1);
        Utf8Pcre2Regex Create(ulong bytes) => Utf8Pcre2Regex.Create(@"(?<=pqr)abc(?=xyz)", Pcre2CompileOptions.None, default,
            new() { MaxWorkSteps = 100000, MaxDepth = 256, MaxWorkingBytes = 1 << 20, MaxResultBytes = bytes },
            Timeout.InfiniteTimeSpan);
        Assert.Equal(Pcre2ErrorKind.ResultLimit, Assert.Throws<Pcre2MatchException>(() =>
        { _ = Create(capacity - 1).Probe("😀pqrabcxyz"u8, Pcre2PartialMode.Soft, 7); }).ErrorKind);
        var regex = Create(capacity);
        var retained = regex.Probe("😀pqrabcxyz"u8, Pcre2PartialMode.Soft, 7).GetMatch();
        var alias = retained.GetGroup(0);
        Assert.Equal((7, 10), (alias.StartOffsetInBytes, alias.EndOffsetInBytes));
        Assert.Equal("abc", regex.Probe("pqrabcxyz"u8, Pcre2PartialMode.Soft, 3).GetMatch().GetValueString());
        Assert.Equal("abc", retained.GetValueString());
        Assert.Equal((7, 10), (alias.StartOffsetInBytes, alias.EndOffsetInBytes));
        var partial = Create(1).Probe("pqrab"u8, Pcre2PartialMode.Hard);
        Assert.Equal(Utf8Pcre2ProbeKind.PartialMatch, partial.Kind);
        Assert.Equal((3, 5), (partial.Value.StartOffsetInBytes, partial.Value.EndOffsetInBytes));
    }

    [Fact]
    public void InspectedEofKeepsOriginalWorkAndWorkingAllowance()
    {
        Utf8Pcre2Regex Create(uint work, ulong heap) => new(@"(?<=pqr)abc(?=xyz)", Pcre2CompileOptions.None, default,
            new() { MatchLimit = work, HeapLimitInBytes = heap }, Timeout.InfiniteTimeSpan);
        Assert.Equal(Pcre2ErrorKind.MatchLimit, Assert.Throws<Pcre2MatchException>(() =>
        { _ = Create(1, 1 << 20).Probe("pqrab"u8, Pcre2PartialMode.Soft); }).ErrorKind);
        Assert.Equal(Pcre2ErrorKind.HeapLimit, Assert.Throws<Pcre2MatchException>(() =>
        { _ = Create(100000, 1).Probe("pqrabcxyz"u8, Pcre2PartialMode.Soft); }).ErrorKind);
        Assert.Equal(Utf8Pcre2ProbeKind.PartialMatch, Create(100000, 1 << 20).Probe("pqrab"u8, Pcre2PartialMode.Soft).Kind);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void InspectedEofKeepsOriginalInterruption(bool cancel)
    {
        using var source = new CancellationTokenSource();
        var regex = new Utf8Pcre2Regex(@"(?<=pqr)abc(?=xyz)", Pcre2CompileOptions.None, default, default, TimeSpan.FromMilliseconds(20));
        var input = Encoding.UTF8.GetBytes(new string('x', 20000) + "pqrab");
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
