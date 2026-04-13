using System.Text;
using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex.Benchmarks;

internal static class Utf8RegexBenchmarkCatalog
{
    private static readonly Utf8RegexBenchmarkCase[] s_cases =
    [
        new(
            "ismatch_ascii_literal_hit",
            "LiteralAscii",
            Utf8RegexBenchmarkOperation.IsMatch,
            "needle",
            BuildRepeated("prefix-", 1024) + "needle" + BuildRepeated("-suffix", 1024),
            RegexOptions.CultureInvariant,
            replacement: null,
            expectedSupport: "Native",
            notes: "Long ASCII haystack with one exact literal hit near the middle."),
        new(
            "ismatch_ascii_literal_miss",
            "LiteralAscii",
            Utf8RegexBenchmarkOperation.IsMatch,
            "needle",
            BuildRepeated("prefix-", 2048) + "haystack",
            RegexOptions.CultureInvariant,
            replacement: null,
            expectedSupport: "Native",
            notes: "No-match scan intended to stress candidate search throughput."),
        new(
            "ismatch_utf8_literal_hit",
            "LiteralUtf8",
            Utf8RegexBenchmarkOperation.IsMatch,
            "café",
            BuildRepeated("resume ", 1024) + "café" + BuildRepeated(" fin", 1024),
            RegexOptions.CultureInvariant,
            replacement: null,
            expectedSupport: "Native",
            notes: "BMP-heavy haystack with a UTF-8 literal anchor."),
        new(
            "ismatch_utf8_emoji_literal_hit",
            "LiteralUtf8",
            Utf8RegexBenchmarkOperation.IsMatch,
            "😀",
            BuildRepeated("status:ok ", 1024) + "😀" + BuildRepeated(" status:end", 1024),
            RegexOptions.CultureInvariant,
            replacement: null,
            expectedSupport: "Native",
            notes: "Supplementary-scalar literal match on a large haystack."),
        new(
            "ismatch_utf8_multialt",
            "LargeDictionaryStyle",
            Utf8RegexBenchmarkOperation.IsMatch,
            "café|niño|résumé|jalapeño",
            BuildRepeated("status ok ", 1024) + "jalapeño" + BuildRepeated(" done", 1024),
            RegexOptions.CultureInvariant,
            replacement: null,
            expectedSupport: "Native",
            notes: "UTF-8 literal alternation search over a large multilingual haystack."),
        new(
            "ismatch_utf8_lookahead",
            "Lookarounds",
            Utf8RegexBenchmarkOperation.IsMatch,
            "café(?= noir)",
            BuildRepeated("café gris ", 1024) + "café noir" + BuildRepeated(" café gris", 1024),
            RegexOptions.CultureInvariant,
            replacement: null,
            expectedSupport: "Native",
            notes: "UTF-8 literal with positive literal lookahead over a mostly negative corpus."),
        new(
            "ismatch_required_literal_ascii",
            "PrefixSearch",
            Utf8RegexBenchmarkOperation.IsMatch,
            "ab[0-9][0-9]cd",
            BuildRepeated("zzzzzzzzzz", 2048) + "ab42cd",
            RegexOptions.CultureInvariant,
            replacement: null,
            expectedSupport: "Native",
            notes: "Required-literal search challenge with a fixed interior structure."),
        new(
            "ismatch_multialt_ascii",
            "LargeDictionaryStyle",
            Utf8RegexBenchmarkOperation.IsMatch,
            "cat|dog|yak|goat|llama|alpaca",
            BuildRepeated("zzzzzzzzzz", 2048) + "alpaca",
            RegexOptions.CultureInvariant,
            replacement: null,
            expectedSupport: "Native",
            notes: "Alternation search with several literal prefixes."),
        new(
            "ismatch_lookahead",
            "Lookarounds",
            Utf8RegexBenchmarkOperation.IsMatch,
            "foo(?=bar)",
            BuildRepeated("fooqux ", 512) + "foobar" + BuildRepeated(" fooqux", 512),
            RegexOptions.CultureInvariant,
            replacement: null,
            expectedSupport: "Native",
            notes: "Positive lookahead over a mostly negative corpus."),
        new(
            "ismatch_rtl_literal",
            "RightToLeft",
            Utf8RegexBenchmarkOperation.IsMatch,
            "needle",
            BuildRepeated("prefix-", 1024) + "needle" + BuildRepeated("-suffix", 1024),
            RegexOptions.CultureInvariant | RegexOptions.RightToLeft,
            replacement: null,
            expectedSupport: "Native",
            notes: "Right-to-left exact literal search."),
        new(
            "ismatch_sift_literal_family_boundary",
            "LokadStructural",
            Utf8RegexBenchmarkOperation.IsMatch,
            @"\b(?:Task|ValueTask|IAsyncEnumerable)\b",
            BuildLokadLiteralFamilyCorpus(),
            RegexOptions.CultureInvariant,
            replacement: null,
            expectedSupport: "Native",
            notes: "Lokad-code-shaped API literal family with word-boundary checks."),
        new(
            "ismatch_sift_identifier_tail_async",
            "LokadStructural",
            Utf8RegexBenchmarkOperation.IsMatch,
            @"\b(?:Get|TryGet|Create)[A-Z][A-Za-z0-9]+Async\b",
            BuildLokadAsyncIdentifierCorpus(),
            RegexOptions.CultureInvariant,
            replacement: null,
            expectedSupport: "Native",
            notes: "Lokad-code-shaped async API family with a literal prefix and identifier tail."),
        new(
            "ismatch_sift_type_declaration",
            "LokadStructural",
            Utf8RegexBenchmarkOperation.IsMatch,
            @"\b(?:record|struct|class)\s+[A-Z][A-Za-z0-9_]+",
            BuildLokadDeclarationCorpus(),
            RegexOptions.CultureInvariant,
            replacement: null,
            expectedSupport: "Native",
            notes: "Lokad-code-shaped type declaration with keyword family, whitespace gap, and identifier tail."),
        new(
            "ismatch_sift_logging_call",
            "LokadStructural",
            Utf8RegexBenchmarkOperation.IsMatch,
            @"\b(?:LogError|LogWarning|LogInformation)\s*\(",
            BuildLokadLoggingCorpus(),
            RegexOptions.CultureInvariant,
            replacement: null,
            expectedSupport: "Native",
            notes: "Lokad-code-shaped logging invocation family with optional whitespace before the call."),
        new(
            "ismatch_sift_ordered_window",
            "LokadStructural",
            Utf8RegexBenchmarkOperation.IsMatch,
            @"\b(?:using\s+var|await\s+using\s+var)\s+[A-Za-z_][A-Za-z0-9_]*\s*=\s*await\b",
            BuildLokadOrderedWindowCorpus(),
            RegexOptions.CultureInvariant,
            replacement: null,
            expectedSupport: "Native",
            notes: "Lokad-code-shaped ordered structural family with bounded separator runs and a required trailing anchor."),
        new(
            "match_utf8_literal_single_hit",
            "LiteralUtf8",
            Utf8RegexBenchmarkOperation.Match,
            "café",
            BuildRepeated("resume ", 1024) + "café" + BuildRepeated(" fin", 1024),
            RegexOptions.CultureInvariant,
            replacement: null,
            expectedSupport: "Native",
            notes: "Single BMP UTF-8 literal match near the middle of a large haystack."),
        new(
            "match_utf8_emoji_single_hit",
            "LiteralUtf8",
            Utf8RegexBenchmarkOperation.Match,
            "😀",
            BuildRepeated("status:ok ", 1024) + "😀" + BuildRepeated(" status:end", 1024),
            RegexOptions.CultureInvariant,
            replacement: null,
            expectedSupport: "Native",
            notes: "Single supplementary-scalar literal match near the middle of a large haystack."),
        new(
            "match_utf8_literal_no_match",
            "LiteralUtf8",
            Utf8RegexBenchmarkOperation.Match,
            "café",
            BuildRepeated("resume ", 2048) + "done",
            RegexOptions.CultureInvariant,
            replacement: null,
            expectedSupport: "Native",
            notes: "No-match BMP UTF-8 literal scan intended to stress candidate search throughput."),
        new(
            "match_utf8_multialt",
            "LargeDictionaryStyle",
            Utf8RegexBenchmarkOperation.Match,
            "café|niño|résumé|jalapeño",
            BuildRepeated("status ok ", 1024) + "résumé" + BuildRepeated(" done", 1024),
            RegexOptions.CultureInvariant,
            replacement: null,
            expectedSupport: "Native",
            notes: "UTF-8 literal alternation match projection on a single-hit multilingual haystack."),
        new(
            "match_utf8_multialt_boundary",
            "LiteralUtf8",
            Utf8RegexBenchmarkOperation.Match,
            @"\b(?:café|niño|résumé)\b",
            BuildRepeated("caféx xniño ", 1024) + "résumé" + BuildRepeated(" caféx xniño", 1024),
            RegexOptions.CultureInvariant,
            replacement: null,
            expectedSupport: "Native",
            notes: "Boundary-wrapped UTF-8 literal alternation with one valid hit in a mostly negative corpus."),
        new(
            "match_utf8_lookahead",
            "Lookarounds",
            Utf8RegexBenchmarkOperation.Match,
            "café(?= noir)",
            BuildRepeated("café gris ", 1024) + "café noir" + BuildRepeated(" café gris", 1024),
            RegexOptions.CultureInvariant,
            replacement: null,
            expectedSupport: "Native",
            notes: "UTF-8 literal match projection with positive literal lookahead."),
        new(
            "match_utf8_literal_rtl",
            "RightToLeft",
            Utf8RegexBenchmarkOperation.Match,
            "café",
            BuildRepeated("resume ", 1024) + "café" + BuildRepeated(" fin", 1024),
            RegexOptions.CultureInvariant | RegexOptions.RightToLeft,
            replacement: null,
            expectedSupport: "Native",
            notes: "Right-to-left UTF-8 literal match on the native literal path."),
        new(
            "match_sift_identifier_tail_async",
            "LokadStructural",
            Utf8RegexBenchmarkOperation.Match,
            @"\b(?:Get|TryGet|Create)[A-Z][A-Za-z0-9]+Async\b",
            BuildLokadAsyncIdentifierCorpus(),
            RegexOptions.CultureInvariant,
            replacement: null,
            expectedSupport: "Native",
            notes: "Lokad-code-shaped async API family on single-match projection."),
        new(
            "match_sift_logging_call",
            "LokadStructural",
            Utf8RegexBenchmarkOperation.Match,
            @"\b(?:LogError|LogWarning|LogInformation)\s*\(",
            BuildLokadLoggingCorpus(),
            RegexOptions.CultureInvariant,
            replacement: null,
            expectedSupport: "Native",
            notes: "Lokad-code-shaped logging invocation family on single-match projection."),
        new(
            "count_ascii_dense",
            "LiteralAscii",
            Utf8RegexBenchmarkOperation.Count,
            "ab[0-9]d",
            BuildRepeated("ab1d-", 4096),
            RegexOptions.CultureInvariant,
            replacement: null,
            expectedSupport: "Native",
            notes: "Dense match workload for count throughput."),
        new(
            "count_ascii_alternation",
            "LargeDictionaryStyle",
            Utf8RegexBenchmarkOperation.Count,
            "cat|dog|yak",
            BuildRepeated("cat dog yak ", 4096),
            RegexOptions.CultureInvariant,
            replacement: null,
            expectedSupport: "Native",
            notes: "Many short literal matches with alternation search."),
        new(
            "count_multilingual_literal",
            "LiteralUtf8",
            Utf8RegexBenchmarkOperation.Count,
            "café",
            BuildRepeated("café ", 4096),
            RegexOptions.CultureInvariant,
            replacement: null,
            expectedSupport: "Native",
            notes: "BMP UTF-8 literal count on a dense multilingual corpus."),
        new(
            "count_backreference",
            "BacktrackingRealistic",
            Utf8RegexBenchmarkOperation.Count,
            "([0-9]+)-\\1",
            BuildRepeated("123-123 999-999 42-24 ", 2048),
            RegexOptions.CultureInvariant,
            replacement: null,
            expectedSupport: "Native",
            notes: "Backreference workload with both hits and misses."),
        new(
            "count_balancing",
            "BacktrackingRealistic",
            Utf8RegexBenchmarkOperation.Count,
            "(?<open>a)(?<-open>b)",
            BuildRepeated("ab aa bb ab ", 4096),
            RegexOptions.CultureInvariant,
            replacement: null,
            expectedSupport: "Native",
            notes: "Balancing-group workload over short repeated tokens."),
        new(
            "ismatch_balancing_begin_end",
            "BalancingGroups",
            Utf8RegexBenchmarkOperation.IsMatch,
            @"BEGIN(?:(?<open>BEGIN)|(?<-open>END)|(?:(?!BEGIN|END)[\s\S]))*END(?(open)(?!))",
            BuildBalancingBeginEndCorpus() + "BEGIN section BEGIN nested END tail END" + BuildRepeated(" separator ", 1024),
            RegexOptions.CultureInvariant,
            replacement: null,
            expectedSupport: "Fallback",
            notes: "BEGIN/END balancing-group search with one nested hit inside a large mostly-negative corpus."),
        new(
            "count_balancing_begin_end",
            "BalancingGroups",
            Utf8RegexBenchmarkOperation.Count,
            @"BEGIN(?:(?<open>BEGIN)|(?<-open>END)|(?:(?!BEGIN|END)[\s\S]))*END(?(open)(?!))",
            BuildBalancingBeginEndCorpus(),
            RegexOptions.CultureInvariant,
            replacement: null,
            expectedSupport: "Fallback",
            notes: "BEGIN/END balancing-group count over a dense synthetic structured-text corpus."),
        new(
            "enumerate_balancing_begin_end",
            "BalancingGroups",
            Utf8RegexBenchmarkOperation.EnumerateMatches,
            @"BEGIN(?:(?<open>BEGIN)|(?<-open>END)|(?:(?!BEGIN|END)[\s\S]))*END(?(open)(?!))",
            BuildBalancingBeginEndCorpus(),
            RegexOptions.CultureInvariant,
            replacement: null,
            expectedSupport: "Fallback",
            notes: "BEGIN/END balancing-group enumeration over repeated balanced and near-balanced blocks."),
        new(
            "count_unicode_fallback_boundary",
            "FallbackCoverage",
            Utf8RegexBenchmarkOperation.Count,
            "\\bé\\b",
            BuildRepeated("é a é b ", 4096),
            RegexOptions.CultureInvariant,
            replacement: null,
            expectedSupport: "Native",
            notes: "Unicode word-boundary benchmark on the native boundary-literal subset."),
        new(
            "enumerate_ascii_dense",
            "LiteralAscii",
            Utf8RegexBenchmarkOperation.EnumerateMatches,
            "ab[0-9]d",
            BuildRepeated("ab1d-", 4096),
            RegexOptions.CultureInvariant,
            replacement: null,
            expectedSupport: "Native",
            notes: "Dense enumerate workload using a native simple pattern."),
        new(
            "enumerate_ascii_dense_long",
            "LiteralAscii",
            Utf8RegexBenchmarkOperation.EnumerateMatches,
            "ab[0-9]cd",
            BuildRepeated("ab1cd-", 4096),
            RegexOptions.CultureInvariant,
            replacement: null,
            expectedSupport: "Native",
            notes: "Dense enumerate workload using a longer fixed-width native simple pattern."),
        new(
            "enumerate_lookbehind",
            "Lookarounds",
            Utf8RegexBenchmarkOperation.EnumerateMatches,
            "(?<=foo)bar",
            BuildRepeated("foobar fooqux ", 4096),
            RegexOptions.CultureInvariant,
            replacement: null,
            expectedSupport: "Native",
            notes: "Fixed-width lookbehind enumeration."),
        new(
            "enumerate_utf8_literal",
            "LiteralUtf8",
            Utf8RegexBenchmarkOperation.EnumerateMatches,
            "😀",
            BuildRepeated("😀 ok ", 4096),
            RegexOptions.CultureInvariant,
            replacement: null,
            expectedSupport: "Native",
            notes: "Supplementary-scalar literal enumeration."),
        new(
            "enumerate_utf8_bmp_dense",
            "LiteralUtf8",
            Utf8RegexBenchmarkOperation.EnumerateMatches,
            "café",
            BuildRepeated("café ", 4096),
            RegexOptions.CultureInvariant,
            replacement: null,
            expectedSupport: "Native",
            notes: "Dense BMP UTF-8 literal enumeration over multilingual text."),
        new(
            "enumerate_utf8_literal_no_match",
            "LiteralUtf8",
            Utf8RegexBenchmarkOperation.EnumerateMatches,
            "café",
            BuildRepeated("resume ", 4096),
            RegexOptions.CultureInvariant,
            replacement: null,
            expectedSupport: "Native",
            notes: "No-match UTF-8 literal enumeration to isolate scan cost."),
        new(
            "enumerate_utf8_multialt_dense",
            "LargeDictionaryStyle",
            Utf8RegexBenchmarkOperation.EnumerateMatches,
            "café|niño|résumé",
            BuildRepeated("café niño résumé ", 2048),
            RegexOptions.CultureInvariant,
            replacement: null,
            expectedSupport: "Native",
            notes: "Dense multilingual exact-literal alternation enumeration."),
        new(
            "enumerate_utf8_multialt_boundary_dense",
            "LiteralUtf8",
            Utf8RegexBenchmarkOperation.EnumerateMatches,
            @"\b(?:café|niño|résumé)\b",
            BuildRepeated("café niño caféx xniño résumé ", 1536),
            RegexOptions.CultureInvariant,
            replacement: null,
            expectedSupport: "Native",
            notes: "Dense boundary-wrapped UTF-8 literal alternation enumeration."),
        new(
            "enumerate_utf8_lookahead_dense",
            "Lookarounds",
            Utf8RegexBenchmarkOperation.EnumerateMatches,
            "café(?= noir)",
            BuildRepeated("café noir café gris ", 4096),
            RegexOptions.CultureInvariant,
            replacement: null,
            expectedSupport: "Native",
            notes: "Dense UTF-8 literal enumeration with positive literal lookahead."),
        new(
            "enumerate_rtl_literal",
            "RightToLeft",
            Utf8RegexBenchmarkOperation.EnumerateMatches,
            "needle",
            BuildRepeated("needle ", 4096),
            RegexOptions.CultureInvariant | RegexOptions.RightToLeft,
            replacement: null,
            expectedSupport: "Native",
            notes: "Right-to-left enumeration over many hits."),
        new(
            "enumerate_unicode_fallback_boundary",
            "FallbackCoverage",
            Utf8RegexBenchmarkOperation.EnumerateMatches,
            "\\bé\\b",
            BuildRepeated("é a é b ", 4096),
            RegexOptions.CultureInvariant,
            replacement: null,
            expectedSupport: "Native",
            notes: "Unicode boundary enumeration benchmark on the native boundary-literal subset."),
        new(
            "enumerate_sift_literal_family_boundary",
            "LokadStructural",
            Utf8RegexBenchmarkOperation.EnumerateMatches,
            @"\b(?:Task|ValueTask|IAsyncEnumerable)\b",
            BuildLokadLiteralFamilyDenseCorpus(),
            RegexOptions.CultureInvariant,
            replacement: null,
            expectedSupport: "Native",
            notes: "Dense Lokad-code-style API literal family enumeration."),
        new(
            "enumerate_sift_logging_call",
            "LokadStructural",
            Utf8RegexBenchmarkOperation.EnumerateMatches,
            @"\b(?:LogError|LogWarning|LogInformation)\s*\(",
            BuildLokadLoggingDenseCorpus(),
            RegexOptions.CultureInvariant,
            replacement: null,
            expectedSupport: "Native",
            notes: "Dense Lokad-code-style logging invocation family enumeration."),
        new(
            "replace_ascii_fixed",
            "Replace",
            Utf8RegexBenchmarkOperation.Replace,
            "ab[0-9]d",
            BuildRepeated("ab1d-", 2048),
            RegexOptions.CultureInvariant,
            replacement: "ZZ",
            expectedSupport: "Native",
            notes: "Fixed replacement over dense native simple-pattern matches."),
        new(
            "replace_capture_substitution",
            "Replace",
            Utf8RegexBenchmarkOperation.Replace,
            "(?<left>ab)(?<digit>[0-9])d",
            BuildRepeated("ab1d-", 2048),
            RegexOptions.CultureInvariant,
            replacement: "${digit}${left}",
            expectedSupport: "Native",
            notes: "Substitution-heavy fixed replacement with named captures."),
        new(
            "replace_utf8_literal",
            "Replace",
            Utf8RegexBenchmarkOperation.Replace,
            "café",
            BuildRepeated("café ", 2048),
            RegexOptions.CultureInvariant,
            replacement: "bistro",
            expectedSupport: "Native",
            notes: "UTF-8 literal replacement on BMP-heavy text."),
        new(
            "replace_utf8_literal_substitution",
            "Replace",
            Utf8RegexBenchmarkOperation.Replace,
            "(café)",
            BuildRepeated("café ", 2048),
            RegexOptions.CultureInvariant,
            replacement: "$1-bistro",
            expectedSupport: "Native",
            notes: "BMP UTF-8 replacement with substitution token, shaped after runtime replacement tests."),
        new(
            "replace_utf8_emoji_literal",
            "Replace",
            Utf8RegexBenchmarkOperation.Replace,
            "😀",
            BuildRepeated("😀 ok ", 2048),
            RegexOptions.CultureInvariant,
            replacement: ":-)",
            expectedSupport: "Native",
            notes: "Supplementary-scalar literal replacement over a dense native corpus."),
        new(
            "replace_utf8_multialt",
            "Replace",
            Utf8RegexBenchmarkOperation.Replace,
            "café|niño|résumé",
            BuildRepeated("café niño résumé ", 1024),
            RegexOptions.CultureInvariant,
            replacement: "X",
            expectedSupport: "Native",
            notes: "Fixed replacement over exact UTF-8 literal alternation matches."),
        new(
            "replace_utf8_multialt_substitution",
            "Replace",
            Utf8RegexBenchmarkOperation.Replace,
            "(café|niño|résumé)",
            BuildRepeated("café niño résumé ", 1024),
            RegexOptions.CultureInvariant,
            replacement: "$1!",
            expectedSupport: "Native",
            notes: "Substitution over captured exact UTF-8 literal alternation matches."),
        new(
            "replace_utf8_multialt_boundary",
            "Replace",
            Utf8RegexBenchmarkOperation.Replace,
            @"\b(?:café|niño|résumé)\b",
            BuildRepeated("café niño caféx xniño résumé ", 1024),
            RegexOptions.CultureInvariant,
            replacement: "X",
            expectedSupport: "Native",
            notes: "Fixed replacement over boundary-wrapped UTF-8 literal alternation matches."),
        new(
            "replace_utf8_multialt_boundary_substitution",
            "Replace",
            Utf8RegexBenchmarkOperation.Replace,
            @"\b(café|niño|résumé)\b",
            BuildRepeated("café niño caféx xniño résumé ", 1024),
            RegexOptions.CultureInvariant,
            replacement: "$1!",
            expectedSupport: "Native",
            notes: "Substitution over captured boundary-wrapped UTF-8 literal alternation matches."),
        new(
            "replace_utf8_lookahead",
            "Replace",
            Utf8RegexBenchmarkOperation.Replace,
            "café(?= noir)",
            BuildRepeated("café noir café gris ", 2048),
            RegexOptions.CultureInvariant,
            replacement: "bistro",
            expectedSupport: "Native",
            notes: "UTF-8 literal replacement with positive literal lookahead."),
        new(
            "replace_rtl_literal",
            "Replace",
            Utf8RegexBenchmarkOperation.Replace,
            "needle",
            BuildRepeated("needle ", 2048),
            RegexOptions.CultureInvariant | RegexOptions.RightToLeft,
            replacement: "pin",
            expectedSupport: "Native",
            notes: "Right-to-left fixed replacement workload."),
        new(
            "replace_unicode_fallback_boundary",
            "FallbackCoverage",
            Utf8RegexBenchmarkOperation.Replace,
            "\\bé\\b",
            BuildRepeated("é a é b ", 2048),
            RegexOptions.CultureInvariant,
            replacement: "x",
            expectedSupport: "Native",
            notes: "Unicode boundary replacement benchmark on the native boundary-literal subset."),
        new(
            "split_utf8_literal_dense",
            "Split",
            Utf8RegexBenchmarkOperation.EnumerateSplits,
            "café",
            BuildRepeated("left café right café ", 2048),
            RegexOptions.CultureInvariant,
            replacement: null,
            expectedSupport: "Native",
            notes: "Dense BMP UTF-8 literal split on the native literal path."),
        new(
            "split_utf8_emoji_dense",
            "Split",
            Utf8RegexBenchmarkOperation.EnumerateSplits,
            "😀",
            BuildRepeated("left 😀 right 😀 ", 2048),
            RegexOptions.CultureInvariant,
            replacement: null,
            expectedSupport: "Native",
            notes: "Dense supplementary-scalar split workload on the native literal path."),
        new(
            "split_utf8_literal_no_match",
            "Split",
            Utf8RegexBenchmarkOperation.EnumerateSplits,
            "café",
            BuildRepeated("resume ", 4096),
            RegexOptions.CultureInvariant,
            replacement: null,
            expectedSupport: "Native",
            notes: "No-match UTF-8 literal split, useful to isolate native fixed overhead."),
        new(
            "split_utf8_multialt_dense",
            "Split",
            Utf8RegexBenchmarkOperation.EnumerateSplits,
            "café|niño|résumé",
            BuildRepeated("café niño résumé ", 1024),
            RegexOptions.CultureInvariant,
            replacement: null,
            expectedSupport: "Native",
            notes: "Dense exact UTF-8 literal alternation split enumeration."),
        new(
            "split_utf8_multialt_boundary_dense",
            "Split",
            Utf8RegexBenchmarkOperation.EnumerateSplits,
            @"\b(?:café|niño|résumé)\b",
            BuildRepeated("café niño caféx xniño résumé ", 1024),
            RegexOptions.CultureInvariant,
            replacement: null,
            expectedSupport: "Native",
            notes: "Dense boundary-wrapped UTF-8 literal alternation split enumeration."),
    ];

    public static IEnumerable<string> GetIds(Utf8RegexBenchmarkOperation operation)
    {
        return s_cases.Where(c => c.Operation == operation).Select(static c => c.Id);
    }

    public static IEnumerable<Utf8RegexBenchmarkCase> GetAllCases() => s_cases;

    public static Utf8RegexBenchmarkCase Get(string id)
    {
        return s_cases.First(c => c.Id == id);
    }

    private static string BuildLokadLiteralFamilyCorpus()
    {
        return BuildRepeated("public sealed class Worker { IEnumerable<int> items; } ", 512) +
               "public async ValueTask<int> FetchAsync() => 42; " +
               BuildRepeated("private readonly string _text; ", 512);
    }

    private static string BuildLokadLiteralFamilyDenseCorpus()
    {
        return BuildRepeated("Task ValueTask IAsyncEnumerable IEnumerable ", 2048);
    }

    private static string BuildLokadAsyncIdentifierCorpus()
    {
        return BuildRepeated("Getvalue Tryget cache createalpha sync ", 512) +
               "public async Task<User> TryGetCustomerRecordAsync() => await _repo.LoadAsync(); " +
               BuildRepeated("Getx createy TryGetz async ", 512);
    }

    private static string BuildLokadDeclarationCorpus()
    {
        return BuildRepeated("internal sealed worker_state value_holder ", 512) +
               "public record CustomerExportJob(int Id); " +
               BuildRepeated("private readonly worker_state value_holder ", 512);
    }

    private static string BuildLokadLoggingCorpus()
    {
        return BuildRepeated("Logger.Debug value logger.info value ", 512) +
               "logger.LogWarning   (\"disk nearly full\"); " +
               BuildRepeated("logTrace value debug() ", 512);
    }

    private static string BuildLokadLoggingDenseCorpus()
    {
        return BuildRepeated("LogError( LogWarning ( LogInformation   ( ", 1536);
    }

    private static string BuildLokadOrderedWindowCorpus()
    {
        return BuildRepeated("using local = value; await local.ConfigureAwait(false); ", 384) +
               "await using var stream = await OpenStreamAsync(); " +
               BuildRepeated("await using thing local = value; using item = result; ", 384);
    }

    private static string BuildBalancingBeginEndCorpus()
    {
        const string balanced = """
BEGIN block
  alpha
  BEGIN nested
    beta
  END
END

""";
        const string malformed = """
BEGIN broken
  gamma
END
END

""";
        const string shallow = """
BEGIN item
  delta
END

""";

        return BuildRepeated(balanced + shallow + malformed + "separator\n", 256);
    }

    private static string BuildRepeated(string chunk, int repeatCount)
    {
        var builder = new StringBuilder(chunk.Length * repeatCount);
        for (var i = 0; i < repeatCount; i++)
        {
            builder.Append(chunk);
        }

        return builder.ToString();
    }
}
