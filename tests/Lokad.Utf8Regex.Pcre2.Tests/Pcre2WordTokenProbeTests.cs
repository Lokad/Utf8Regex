using System.Text;
using Lokad.Utf8Regex.Internal;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Pcre2.Tests;

[Collection(Pcre2AllocationTestCollection.Name)]
public sealed class Pcre2WordTokenProbeTests
{
    // Frozen PCRE2 10.47 / private PCRE.NET 1.5.0 outcomes; no native test dependency.
    public static IEnumerable<object[]> NativeCases()
    {
        yield return ["^(?:a)++\\w", "a", 0, Pcre2CompileOptions.Ucp, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["^(?:a)++\\w", "a", 0, Pcre2CompileOptions.Ucp, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.PartialMatch, 0, 1];
        yield return ["^(?:a)++\\w", "a", 0, Pcre2CompileOptions.Ucp, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.PartialMatch, 0, 1];
        yield return ["^(?:a)++\\w", "aa", 0, Pcre2CompileOptions.Ucp, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["^(?:a)++\\w", "aa", 0, Pcre2CompileOptions.Ucp, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.PartialMatch, 0, 2];
        yield return ["^(?:a)++\\w", "aa", 0, Pcre2CompileOptions.Ucp, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.PartialMatch, 0, 2];
        yield return ["^(?:a)++\\w", "a\u00e9", 0, Pcre2CompileOptions.Ucp, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.FullMatch, 0, 3];
        yield return ["^(?:a)++\\w", "a\u00e9", 0, Pcre2CompileOptions.Ucp, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.FullMatch, 0, 3];
        yield return ["^(?:a)++\\w", "a\u00e9", 0, Pcre2CompileOptions.Ucp, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.FullMatch, 0, 3];
        yield return ["^(?:a)++\\w", "", 0, Pcre2CompileOptions.Ucp, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["^(?:a)++\\w", "", 0, Pcre2CompileOptions.Ucp, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["^(?:a)++\\w", "", 0, Pcre2CompileOptions.Ucp, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["^(a)++\\w", "a", 0, Pcre2CompileOptions.Ucp, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["^(a)++\\w", "a", 0, Pcre2CompileOptions.Ucp, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.PartialMatch, 0, 1];
        yield return ["^(a)++\\w", "a", 0, Pcre2CompileOptions.Ucp, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.PartialMatch, 0, 1];
        yield return ["^(a)++\\w", "aa", 0, Pcre2CompileOptions.Ucp, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["^(a)++\\w", "aa", 0, Pcre2CompileOptions.Ucp, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.PartialMatch, 0, 2];
        yield return ["^(a)++\\w", "aa", 0, Pcre2CompileOptions.Ucp, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.PartialMatch, 0, 2];
        yield return ["^(a)++\\w", "a\u00e9", 0, Pcre2CompileOptions.Ucp, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.FullMatch, 0, 3];
        yield return ["^(a)++\\w", "a\u00e9", 0, Pcre2CompileOptions.Ucp, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.FullMatch, 0, 3];
        yield return ["^(a)++\\w", "a\u00e9", 0, Pcre2CompileOptions.Ucp, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.FullMatch, 0, 3];
        yield return ["^(a)++\\w", "", 0, Pcre2CompileOptions.Ucp, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["^(a)++\\w", "", 0, Pcre2CompileOptions.Ucp, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["^(a)++\\w", "", 0, Pcre2CompileOptions.Ucp, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["^(?:a)*+\\w", "a", 0, Pcre2CompileOptions.Ucp, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["^(?:a)*+\\w", "a", 0, Pcre2CompileOptions.Ucp, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.PartialMatch, 0, 1];
        yield return ["^(?:a)*+\\w", "a", 0, Pcre2CompileOptions.Ucp, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.PartialMatch, 0, 1];
        yield return ["^(?:a)*+\\w", "aa", 0, Pcre2CompileOptions.Ucp, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["^(?:a)*+\\w", "aa", 0, Pcre2CompileOptions.Ucp, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.PartialMatch, 0, 2];
        yield return ["^(?:a)*+\\w", "aa", 0, Pcre2CompileOptions.Ucp, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.PartialMatch, 0, 2];
        yield return ["^(?:a)*+\\w", "a\u00e9", 0, Pcre2CompileOptions.Ucp, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.FullMatch, 0, 3];
        yield return ["^(?:a)*+\\w", "a\u00e9", 0, Pcre2CompileOptions.Ucp, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.FullMatch, 0, 3];
        yield return ["^(?:a)*+\\w", "a\u00e9", 0, Pcre2CompileOptions.Ucp, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.FullMatch, 0, 3];
        yield return ["^(?:a)*+\\w", "", 0, Pcre2CompileOptions.Ucp, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["^(?:a)*+\\w", "", 0, Pcre2CompileOptions.Ucp, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["^(?:a)*+\\w", "", 0, Pcre2CompileOptions.Ucp, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["^(a|)++\\w", "a", 0, Pcre2CompileOptions.Ucp, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["^(a|)++\\w", "a", 0, Pcre2CompileOptions.Ucp, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.PartialMatch, 0, 1];
        yield return ["^(a|)++\\w", "a", 0, Pcre2CompileOptions.Ucp, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.PartialMatch, 0, 1];
        yield return ["^(a|)++\\w", "aa", 0, Pcre2CompileOptions.Ucp, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["^(a|)++\\w", "aa", 0, Pcre2CompileOptions.Ucp, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.PartialMatch, 0, 2];
        yield return ["^(a|)++\\w", "aa", 0, Pcre2CompileOptions.Ucp, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.PartialMatch, 0, 2];
        yield return ["^(a|)++\\w", "a\u00e9", 0, Pcre2CompileOptions.Ucp, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.FullMatch, 0, 3];
        yield return ["^(a|)++\\w", "a\u00e9", 0, Pcre2CompileOptions.Ucp, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.FullMatch, 0, 3];
        yield return ["^(a|)++\\w", "a\u00e9", 0, Pcre2CompileOptions.Ucp, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.FullMatch, 0, 3];
        yield return ["^(a|)++\\w", "", 0, Pcre2CompileOptions.Ucp, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["^(a|)++\\w", "", 0, Pcre2CompileOptions.Ucp, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["^(a|)++\\w", "", 0, Pcre2CompileOptions.Ucp, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["^(?:aa|(?:a)++\\w)", "a", 0, Pcre2CompileOptions.Ucp, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["^(?:aa|(?:a)++\\w)", "a", 0, Pcre2CompileOptions.Ucp, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.PartialMatch, 0, 1];
        yield return ["^(?:aa|(?:a)++\\w)", "a", 0, Pcre2CompileOptions.Ucp, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.PartialMatch, 0, 1];
        yield return ["^(?:aa|(?:a)++\\w)", "aa", 0, Pcre2CompileOptions.Ucp, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.FullMatch, 0, 2];
        yield return ["^(?:aa|(?:a)++\\w)", "aa", 0, Pcre2CompileOptions.Ucp, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.FullMatch, 0, 2];
        yield return ["^(?:aa|(?:a)++\\w)", "aa", 0, Pcre2CompileOptions.Ucp, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.FullMatch, 0, 2];
        yield return ["^(?:aa|(?:a)++\\w)", "a\u00e9", 0, Pcre2CompileOptions.Ucp, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.FullMatch, 0, 3];
        yield return ["^(?:aa|(?:a)++\\w)", "a\u00e9", 0, Pcre2CompileOptions.Ucp, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.FullMatch, 0, 3];
        yield return ["^(?:aa|(?:a)++\\w)", "a\u00e9", 0, Pcre2CompileOptions.Ucp, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.FullMatch, 0, 3];
        yield return ["^(?:aa|(?:a)++\\w)", "", 0, Pcre2CompileOptions.Ucp, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["^(?:aa|(?:a)++\\w)", "", 0, Pcre2CompileOptions.Ucp, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["^(?:aa|(?:a)++\\w)", "", 0, Pcre2CompileOptions.Ucp, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["^(?:a)++\\w", "a\ud801\udc00", 0, Pcre2CompileOptions.Ucp, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.FullMatch, 0, 5];
        yield return ["^(?:a)++\\w", "a\ud801\udc00", 0, Pcre2CompileOptions.Ucp, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.FullMatch, 0, 5];
        yield return ["^(?:a)++\\w", "a\ud801\udc00", 0, Pcre2CompileOptions.Ucp, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.FullMatch, 0, 5];
        yield return ["^(?:a)++\\w", "a\u0301", 0, Pcre2CompileOptions.Ucp, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.FullMatch, 0, 3];
        yield return ["^(?:a)++\\w", "a\u0301", 0, Pcre2CompileOptions.Ucp, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.FullMatch, 0, 3];
        yield return ["^(?:a)++\\w", "a\u0301", 0, Pcre2CompileOptions.Ucp, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.FullMatch, 0, 3];
        yield return ["^(?:a)++\\w", "a\ud83d\ude00", 0, Pcre2CompileOptions.Ucp, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["^(?:a)++\\w", "a\ud83d\ude00", 0, Pcre2CompileOptions.Ucp, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["^(?:a)++\\w", "a\ud83d\ude00", 0, Pcre2CompileOptions.Ucp, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["^(?:a)*+\\w", "a\ud801\udc00", 0, Pcre2CompileOptions.Ucp, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.FullMatch, 0, 5];
        yield return ["^(?:a)*+\\w", "a\ud801\udc00", 0, Pcre2CompileOptions.Ucp, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.FullMatch, 0, 5];
        yield return ["^(?:a)*+\\w", "a\ud801\udc00", 0, Pcre2CompileOptions.Ucp, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.FullMatch, 0, 5];
        yield return ["^(?:a)*+\\w", "a\u0301", 0, Pcre2CompileOptions.Ucp, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.FullMatch, 0, 3];
        yield return ["^(?:a)*+\\w", "a\u0301", 0, Pcre2CompileOptions.Ucp, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.FullMatch, 0, 3];
        yield return ["^(?:a)*+\\w", "a\u0301", 0, Pcre2CompileOptions.Ucp, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.FullMatch, 0, 3];
        yield return ["^(?:a)*+\\w", "a\ud83d\ude00", 0, Pcre2CompileOptions.Ucp, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["^(?:a)*+\\w", "a\ud83d\ude00", 0, Pcre2CompileOptions.Ucp, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["^(?:a)*+\\w", "a\ud83d\ude00", 0, Pcre2CompileOptions.Ucp, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["^(a|)++\\w", "a\ud801\udc00", 0, Pcre2CompileOptions.Ucp, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.FullMatch, 0, 5];
        yield return ["^(a|)++\\w", "a\ud801\udc00", 0, Pcre2CompileOptions.Ucp, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.FullMatch, 0, 5];
        yield return ["^(a|)++\\w", "a\ud801\udc00", 0, Pcre2CompileOptions.Ucp, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.FullMatch, 0, 5];
        yield return ["^(a|)++\\w", "a\u0301", 0, Pcre2CompileOptions.Ucp, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.FullMatch, 0, 3];
        yield return ["^(a|)++\\w", "a\u0301", 0, Pcre2CompileOptions.Ucp, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.FullMatch, 0, 3];
        yield return ["^(a|)++\\w", "a\u0301", 0, Pcre2CompileOptions.Ucp, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.FullMatch, 0, 3];
        yield return ["^(a|)++\\w", "a\ud83d\ude00", 0, Pcre2CompileOptions.Ucp, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["^(a|)++\\w", "a\ud83d\ude00", 0, Pcre2CompileOptions.Ucp, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["^(a|)++\\w", "a\ud83d\ude00", 0, Pcre2CompileOptions.Ucp, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["^(?:aa|(?:a)++\\w)", "a\ud801\udc00", 0, Pcre2CompileOptions.Ucp, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.FullMatch, 0, 5];
        yield return ["^(?:aa|(?:a)++\\w)", "a\ud801\udc00", 0, Pcre2CompileOptions.Ucp, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.FullMatch, 0, 5];
        yield return ["^(?:aa|(?:a)++\\w)", "a\ud801\udc00", 0, Pcre2CompileOptions.Ucp, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.FullMatch, 0, 5];
        yield return ["^(?:aa|(?:a)++\\w)", "a\u0301", 0, Pcre2CompileOptions.Ucp, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.FullMatch, 0, 3];
        yield return ["^(?:aa|(?:a)++\\w)", "a\u0301", 0, Pcre2CompileOptions.Ucp, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.FullMatch, 0, 3];
        yield return ["^(?:aa|(?:a)++\\w)", "a\u0301", 0, Pcre2CompileOptions.Ucp, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.FullMatch, 0, 3];
        yield return ["^(?:aa|(?:a)++\\w)", "a\ud83d\ude00", 0, Pcre2CompileOptions.Ucp, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["^(?:aa|(?:a)++\\w)", "a\ud83d\ude00", 0, Pcre2CompileOptions.Ucp, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["^(?:aa|(?:a)++\\w)", "a\ud83d\ude00", 0, Pcre2CompileOptions.Ucp, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["^(?:a)++\\w", "a\u00e9", 0, Pcre2CompileOptions.None, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["^(?:a)++\\w", "a\u00e9", 0, Pcre2CompileOptions.None, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["^(?:a)++\\w", "a\u00e9", 0, Pcre2CompileOptions.None, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["^(?:a)*+\\w", "\u00e9", 0, Pcre2CompileOptions.None, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["^(?:a)*+\\w", "\u00e9", 0, Pcre2CompileOptions.None, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["^(?:a)*+\\w", "\u00e9", 0, Pcre2CompileOptions.None, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["^(?:a)*+\\w", "\u00e9", 0, Pcre2CompileOptions.Ucp, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.FullMatch, 0, 2];
        yield return ["^(?:a)*+\\w", "\u00e9", 0, Pcre2CompileOptions.Ucp, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.FullMatch, 0, 2];
        yield return ["^(?:a)*+\\w", "\u00e9", 0, Pcre2CompileOptions.Ucp, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.FullMatch, 0, 2];
        yield return ["^(a|)++\\w", "\u00e9", 0, Pcre2CompileOptions.Ucp, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.FullMatch, 0, 2];
        yield return ["^(a|)++\\w", "\u00e9", 0, Pcre2CompileOptions.Ucp, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.FullMatch, 0, 2];
        yield return ["^(a|)++\\w", "\u00e9", 0, Pcre2CompileOptions.Ucp, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.FullMatch, 0, 2];
        yield return ["^(?:a)++\\w", "aa_", 0, Pcre2CompileOptions.None, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.FullMatch, 0, 3];
        yield return ["^(?:a)++\\w", "aa_", 0, Pcre2CompileOptions.None, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.FullMatch, 0, 3];
        yield return ["^(?:a)++\\w", "aa_", 0, Pcre2CompileOptions.None, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.FullMatch, 0, 3];
        yield return ["^(?:a)++\\w", "a!", 0, Pcre2CompileOptions.Ucp, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["^(?:a)++\\w", "a!", 0, Pcre2CompileOptions.Ucp, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["^(?:a)++\\w", "a!", 0, Pcre2CompileOptions.Ucp, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["^(?:a)++\\w", "\ud83d\ude00a\u00e9", 4, Pcre2CompileOptions.Ucp, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["^(?:a)++\\w", "\ud83d\ude00a\u00e9", 4, Pcre2CompileOptions.Ucp, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["^(?:a)++\\w", "\ud83d\ude00a\u00e9", 4, Pcre2CompileOptions.Ucp, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["^(?:a)++\\w", "a\u00e9", 0, Pcre2CompileOptions.Ucp, Pcre2MatchOptions.NotBol, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["^(?:a)++\\w", "a\u00e9", 0, Pcre2CompileOptions.Ucp, Pcre2MatchOptions.NotBol, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["^(?:a)++\\w", "a\u00e9", 0, Pcre2CompileOptions.Ucp, Pcre2MatchOptions.NotBol, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["^(?:aa|(?:a)++\\w)", "aa\u00e9", 0, Pcre2CompileOptions.Ucp, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.FullMatch, 0, 2];
        yield return ["^(?:aa|(?:a)++\\w)", "aa\u00e9", 0, Pcre2CompileOptions.Ucp, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.FullMatch, 0, 2];
        yield return ["^(?:aa|(?:a)++\\w)", "aa\u00e9", 0, Pcre2CompileOptions.Ucp, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.FullMatch, 0, 2];
    }

    [Theory]
    [MemberData(nameof(NativeCases))]
    public void WordProbesPreserveNativeScalarAndPossessiveOutcomes(string pattern, string text, int start,
        Pcre2CompileOptions options, Pcre2MatchOptions flags, Pcre2PartialMode mode,
        Utf8Pcre2ProbeKind kind, int begin, int end)
    {
        var regex = new Utf8Pcre2Regex(pattern, options);
        var input = Encoding.UTF8.GetBytes(text);
        var probe = regex.Probe(input, mode, start, flags);
        Assert.Equal(kind, probe.Kind);
        Assert.Equal((begin, end), (probe.Value.StartOffsetInBytes, probe.Value.EndOffsetInBytes));
        if (kind != Utf8Pcre2ProbeKind.FullMatch) return;
        var actual = probe.GetMatch();
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
        Assert.Equal(Encoding.UTF8.GetString(input.AsSpan(begin, end - begin)), actual.GetValueString());
    }

    [Fact]
    public void UnicodeWordFullHasExactResultAdmissionAndRetainedNamedCaptures()
    {
        var capacity = Utf8ConstructionBudget.ArrayBytes<Pcre2GroupData>(2);
        Utf8Pcre2Regex Create(ulong bytes) => Utf8Pcre2Regex.Create(@"^(?<last>a)++\w", Pcre2CompileOptions.Ucp, default,
            new() { MaxWorkSteps = 100000, MaxDepth = 256, MaxWorkingBytes = 1 << 20, MaxResultBytes = bytes },
            Timeout.InfiniteTimeSpan);
        Assert.Equal(Pcre2ErrorKind.ResultLimit, Assert.Throws<Pcre2MatchException>(() =>
        { _ = Create(capacity - 1).Probe("a𐐀"u8, Pcre2PartialMode.Soft); }).ErrorKind);
        var regex = Create(capacity);
        var retained = regex.Probe("a𐐀"u8, Pcre2PartialMode.Soft).GetMatch();
        Assert.Equal((0, 5), (retained.GetGroup(0).StartOffsetInBytes, retained.GetGroup(0).EndOffsetInBytes));
        Assert.True(retained.TryGetFirstSetGroup("last", out var alias));
        Assert.Equal("a", alias.GetValueString());
        Assert.Equal("aé", regex.Probe("aé"u8, Pcre2PartialMode.Hard).GetMatch().GetValueString());
        Assert.Equal("a𐐀", retained.GetValueString());
        Assert.Equal((0, 1), (alias.StartOffsetInBytes, alias.EndOffsetInBytes));
        Assert.Equal(Utf8Pcre2ProbeKind.PartialMatch, Create(1).Probe("aaa"u8, Pcre2PartialMode.Hard).Kind);
    }

    [Fact]
    public void UnicodeWordCannotBypassOriginalWorkOrWorkingAllowance()
    {
        var regex = new Utf8Pcre2Regex(@"^(a)++\w", Pcre2CompileOptions.Ucp, default,
            new() { MatchLimit = 200 }, Timeout.InfiniteTimeSpan);
        var input = Encoding.UTF8.GetBytes(new string('a', 1000) + "𐐀");
        Assert.Equal(Pcre2ErrorKind.MatchLimit, Assert.Throws<Pcre2MatchException>(() =>
        { _ = regex.Probe(input, Pcre2PartialMode.Soft); }).ErrorKind);
        Assert.Equal(Utf8Pcre2ProbeKind.FullMatch, regex.Probe("a𐐀"u8, Pcre2PartialMode.Soft).Kind);
        var heapLimited = new Utf8Pcre2Regex(@"^(a)++\w", Pcre2CompileOptions.Ucp, default,
            new() { MatchLimit = 100000, HeapLimitInBytes = 1 }, Timeout.InfiniteTimeSpan);
        Assert.Equal(Pcre2ErrorKind.HeapLimit, Assert.Throws<Pcre2MatchException>(() =>
        { _ = heapLimited.Probe("a𐐀"u8, Pcre2PartialMode.Soft); }).ErrorKind);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void UnicodeWordObservesOriginalInterruption(bool cancel)
    {
        using var source = new CancellationTokenSource();
        var regex = new Utf8Pcre2Regex(@"^(a)++\w", Pcre2CompileOptions.Ucp, default, default, TimeSpan.FromMilliseconds(20));
        var polls = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() =>
        {
            if (++polls < 10) return 0;
            if (cancel) { source.Cancel(); return 0; }
            return 25;
        });
        Exception? failure = null;
        try { _ = regex.Probe("aaaaaaaaaaaaaaaa𐐀"u8, Pcre2PartialMode.Soft, 0, Pcre2MatchOptions.None, source.Token); }
        catch (Exception error) { failure = error; }
        if (cancel) Assert.IsAssignableFrom<OperationCanceledException>(failure);
        else Assert.Equal(Pcre2ErrorKind.Timeout, Assert.IsType<Pcre2MatchException>(failure).ErrorKind);
    }

}
