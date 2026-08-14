using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Pcre2;

namespace Lokad.Utf8Regex.Benchmarks;

internal static class Utf8Pcre2BenchmarkCatalog
{
    private static readonly Utf8Pcre2BenchmarkCase[] s_curatedCases =
    [
        new(
            "literal/early",
            "needle",
            "needle " + new string('x', 4096),
            supportedOperations: Utf8Pcre2BenchmarkOperation.IsMatch),
        new(
            "literal/late",
            "needle",
            new string('x', 4096) + " needle",
            supportedOperations: Utf8Pcre2BenchmarkOperation.IsMatch),
        new(
            "literal/missing",
            "needle",
            new string('x', 4096),
            supportedOperations: Utf8Pcre2BenchmarkOperation.IsMatch),
        new(
            "literal/absolute-anchored",
            @"\Aneedle",
            "needle " + new string('x', 4096),
            supportedOperations: Utf8Pcre2BenchmarkOperation.IsMatch),
        new(
            "literal/empty-unicode",
            string.Empty,
            string.Concat(Enumerable.Repeat("😀x", 64)),
            replacement: "|",
            supportedOperations: Utf8Pcre2BenchmarkOperation.Count |
                Utf8Pcre2BenchmarkOperation.EnumerateMatches |
                Utf8Pcre2BenchmarkOperation.MatchMany |
                Utf8Pcre2BenchmarkOperation.Replace,
            supportedBackends: Utf8Pcre2BenchmarkBackend.Pcre2Only),
        new(
            "literal/partial-probe",
            "needle",
            new string('x', 4096) + "need",
            supportedOperations: Utf8Pcre2BenchmarkOperation.None,
            supportedBackends: Utf8Pcre2BenchmarkBackend.Pcre2Only),
        new("simple/foo-dense", "foo", "xxfoozz foo foo xx"),
        new("simple/foo-optional-bar", "foo(?<Bar>BAR)?", "foo fooBAR x fooBAR foo", replacement: "bar"),
        new("simple/ab-plus", "(a)b+", "ab abb abbb x ab", replacement: "bar"),
        new(
            "simple/httpclient-caseless",
            "httpclient",
            "HttpClient httpclient x HTTPCLIENT httpClient",
            RegexOptions.CultureInvariant | RegexOptions.IgnoreCase,
            replacement: "client"),
        new(
            "character/unicode-class-dense",
            @"[\p{L}\p{N}_]",
            string.Concat(Enumerable.Repeat("aé٣_-", 1024)),
            replacement: "x",
            supportedOperations: Utf8Pcre2BenchmarkOperation.Count),
        new(
            "backtracking/alternation-repeat",
            "(?:ab|a)+z",
            string.Concat(Enumerable.Repeat("ababaz ", 1024)),
            replacement: "x",
            supportedOperations: Utf8Pcre2BenchmarkOperation.Count),
        new(
            "backtracking/capture-backreference",
            "([0-9]+)-\\1",
            string.Concat(Enumerable.Repeat("123-123 999-999 42-24 ", 1024)),
            replacement: "$1",
            supportedOperations: Utf8Pcre2BenchmarkOperation.IsMatch),
        new(
            "backtracking/assertion-capture",
            "(?=(?<token>[a-z]{3}[0-9]{3}))abc123",
            new string('x', 4096) + " abc123",
            replacement: "$1",
            supportedOperations: Utf8Pcre2BenchmarkOperation.IsMatch),
        new(
            "simple/loglevel-multiline",
            "^ERROR: .+$",
            "INFO: boot\nERROR: first failure\nDEBUG: noisy\nERROR: second failure\n",
            RegexOptions.CultureInvariant | RegexOptions.Multiline,
            replacement: "ERROR: masked"),
        new(
            "pcre2/branch-reset-basic",
            "(?|(abc)|(xyz))",
            "abc xyz xyz abc q",
            replacement: "hit",
            supportedOperations: Utf8Pcre2BenchmarkOperation.Count | Utf8Pcre2BenchmarkOperation.EnumerateMatches | Utf8Pcre2BenchmarkOperation.MatchMany | Utf8Pcre2BenchmarkOperation.Replace,
            supportedBackends: Utf8Pcre2BenchmarkBackend.Pcre2Only),
        new(
            "pcre2/branch-reset-backref",
            "(?|(abc)|(xyz))\\1",
            "abcabc xyzxyz xx abcabc",
            replacement: "pair",
            supportedBackends: Utf8Pcre2BenchmarkBackend.Pcre2Only),
        new(
            "pcre2/branch-reset-nested",
            "(x)(?|(abc)|(xyz))(x)",
            "xabcx xxyzx xqqx xabcx",
            replacement: "wrap",
            supportedOperations: Utf8Pcre2BenchmarkOperation.Count | Utf8Pcre2BenchmarkOperation.EnumerateMatches | Utf8Pcre2BenchmarkOperation.MatchMany | Utf8Pcre2BenchmarkOperation.Replace,
            supportedBackends: Utf8Pcre2BenchmarkBackend.Pcre2Only),
        new(
            "pcre2/branch-reset-followup",
            "(?|(?'a'aaa)|(?'a'b))(?'a'cccc)\\k'a'",
            "aaaccccaaa bccccb xx aaaccccaaa",
            replacement: "tail",
            supportedOperations: Utf8Pcre2BenchmarkOperation.Count | Utf8Pcre2BenchmarkOperation.EnumerateMatches | Utf8Pcre2BenchmarkOperation.MatchMany | Utf8Pcre2BenchmarkOperation.Replace,
            supportedBackends: Utf8Pcre2BenchmarkBackend.Pcre2Only),
        new(
            "pcre2/duplicate-names",
            @"(?:(?<n>foo)|(?<n>bar))\k<n>",
            "foofoo barbar xx foofoo",
            replacement: "pair",
            supportedOperations: Utf8Pcre2BenchmarkOperation.Count | Utf8Pcre2BenchmarkOperation.EnumerateMatches | Utf8Pcre2BenchmarkOperation.MatchMany | Utf8Pcre2BenchmarkOperation.Replace,
            supportedBackends: Utf8Pcre2BenchmarkBackend.Pcre2Only),
        new(
            "pcre2/kreset-global",
            @"abc\K123",
            "abc123 xx abc123 yy abc123",
            replacement: "Z",
            supportedOperations: Utf8Pcre2BenchmarkOperation.Count | Utf8Pcre2BenchmarkOperation.EnumerateMatches | Utf8Pcre2BenchmarkOperation.MatchMany | Utf8Pcre2BenchmarkOperation.Replace,
            supportedBackends: Utf8Pcre2BenchmarkBackend.Pcre2Only),
        new(
            "pcre2/kreset-bar-or-baz",
            @"(foo)(\Kbar|baz)",
            "foobar xx foobaz yy foobaz",
            replacement: "Z",
            supportedOperations: Utf8Pcre2BenchmarkOperation.Count | Utf8Pcre2BenchmarkOperation.EnumerateMatches | Utf8Pcre2BenchmarkOperation.MatchMany | Utf8Pcre2BenchmarkOperation.Replace,
            supportedBackends: Utf8Pcre2BenchmarkBackend.Pcre2Only),
        new(
            "pcre2/kreset-repeat",
            @"(?:a\Kb)*",
            "ab xx ab yy ab",
            replacement: "R",
            supportedOperations: Utf8Pcre2BenchmarkOperation.Count | Utf8Pcre2BenchmarkOperation.EnumerateMatches | Utf8Pcre2BenchmarkOperation.MatchMany | Utf8Pcre2BenchmarkOperation.Replace,
            supportedBackends: Utf8Pcre2BenchmarkBackend.Pcre2Only),
        new(
            "pcre2/kreset-captured-repeat",
            @"(a\Kb)*",
            "ab xx ab yy ab",
            replacement: "R",
            supportedOperations: Utf8Pcre2BenchmarkOperation.Count | Utf8Pcre2BenchmarkOperation.EnumerateMatches | Utf8Pcre2BenchmarkOperation.MatchMany | Utf8Pcre2BenchmarkOperation.Replace,
            supportedBackends: Utf8Pcre2BenchmarkBackend.Pcre2Only),
        new(
            "pcre2/kreset-atomic-alt",
            @"(?>a\Kbz|ab)",
            "abz ab xx abz",
            replacement: "R",
            supportedOperations: Utf8Pcre2BenchmarkOperation.Count | Utf8Pcre2BenchmarkOperation.EnumerateMatches | Utf8Pcre2BenchmarkOperation.MatchMany | Utf8Pcre2BenchmarkOperation.Replace,
            supportedBackends: Utf8Pcre2BenchmarkBackend.Pcre2Only),
        new(
            "pcre2/grapheme-cluster",
            @"\X",
            string.Concat(Enumerable.Repeat("a\u0301🇫🇷👩🏽‍💻\r\n한", 8)),
            replacement: "G",
            supportedOperations: Utf8Pcre2BenchmarkOperation.IsMatch | Utf8Pcre2BenchmarkOperation.Count | Utf8Pcre2BenchmarkOperation.EnumerateMatches | Utf8Pcre2BenchmarkOperation.MatchMany | Utf8Pcre2BenchmarkOperation.Replace,
            supportedBackends: Utf8Pcre2BenchmarkBackend.Pcre2Only),
        new(
            "pcre2/same-start-global",
            "(?<=abc)(|def)",
            "abcdefabc",
            replacement: "[${0}]",
            supportedOperations: Utf8Pcre2BenchmarkOperation.Count | Utf8Pcre2BenchmarkOperation.EnumerateMatches | Utf8Pcre2BenchmarkOperation.MatchMany | Utf8Pcre2BenchmarkOperation.Replace,
            supportedBackends: Utf8Pcre2BenchmarkBackend.Pcre2Only),
        new(
            "pcre2/conditional-lookahead",
            "^(?(?=(a))abc|def)",
            "abc",
            replacement: "cond",
            supportedOperations: Utf8Pcre2BenchmarkOperation.IsMatch,
            supportedBackends: Utf8Pcre2BenchmarkBackend.Pcre2Only),
        new(
            "pcre2/conditional-negative-lookahead",
            "^(?(?!(a))def|abc)",
            "def",
            replacement: "cond",
            supportedOperations: Utf8Pcre2BenchmarkOperation.IsMatch,
            supportedBackends: Utf8Pcre2BenchmarkBackend.Pcre2Only),
        new(
            "pcre2/conditional-accept-negative-lookahead",
            "^(?(?!(a)(*ACCEPT))def|abc)",
            "def",
            replacement: "cond",
            supportedOperations: Utf8Pcre2BenchmarkOperation.IsMatch,
            supportedBackends: Utf8Pcre2BenchmarkBackend.Pcre2Only),
        new(
            "pcre2/subroutine-prefix-digits",
            "^(?1)\\d{3}(a)",
            "a123a",
            replacement: "sub",
            supportedOperations: Utf8Pcre2BenchmarkOperation.IsMatch,
            supportedBackends: Utf8Pcre2BenchmarkBackend.Pcre2Only),
        new(
            "pcre2/backslash-c-literal",
            @"ab\Cde",
            "abcde",
            compileSettings: new Utf8Pcre2CompileSettings { BackslashC = Pcre2BackslashCPolicy.Allow },
            replacement: "c",
            supportedOperations: Utf8Pcre2BenchmarkOperation.IsMatch,
            supportedBackends: Utf8Pcre2BenchmarkBackend.Pcre2Only),
        new(
            "pcre2/recursive-palindrome",
            "^((.)(?1)\\2|.?)$",
            "redder",
            replacement: "pal",
            supportedOperations: Utf8Pcre2BenchmarkOperation.IsMatch,
            supportedBackends: Utf8Pcre2BenchmarkBackend.Pcre2Only),
        new(
            "pcre2/recursive-optional",
            "^(.|(.)(?1)?\\2)$",
            "redder",
            replacement: "pal",
            supportedOperations: Utf8Pcre2BenchmarkOperation.IsMatch,
            supportedBackends: Utf8Pcre2BenchmarkBackend.Pcre2Only)
    ];

    private static readonly Utf8Pcre2BenchmarkCase[] s_cases = CreateCases();

    public static IEnumerable<string> GetIds(Utf8Pcre2BenchmarkOperation operation, Utf8Pcre2BenchmarkBackend backends)
    {
        return s_cases.Where(c => c.Supports(operation, backends)).Select(static c => c.Id);
    }

    public static IEnumerable<Utf8Pcre2BenchmarkCase> GetAllCases() => s_cases;

    public static Utf8Pcre2BenchmarkCase Get(string id) => s_cases.First(c => c.Id == id);

    public static Pcre2CompileOptions ToPcre2Options(RegexOptions options)
    {
        var result = Pcre2CompileOptions.None;
        if ((options & RegexOptions.IgnoreCase) != 0)
        {
            result |= Pcre2CompileOptions.Caseless;
        }

        if ((options & RegexOptions.Multiline) != 0)
        {
            result |= Pcre2CompileOptions.Multiline;
        }

        if ((options & RegexOptions.Singleline) != 0)
        {
            result |= Pcre2CompileOptions.DotAll;
        }

        if ((options & RegexOptions.IgnorePatternWhitespace) != 0)
        {
            result |= Pcre2CompileOptions.Extended;
        }

        return result;
    }

    private static Utf8Pcre2BenchmarkCase[] CreateCases()
    {
        return s_curatedCases
            .Concat(CreateDotNetPerformanceManagedCompatibleCases())
            .ToArray();
    }

    private static IEnumerable<Utf8Pcre2BenchmarkCase> CreateDotNetPerformanceManagedCompatibleCases()
    {
        foreach (var caseId in LokadPublicBenchmarkContext.GetAllCaseIds())
        {
            var definition = LokadPublicBenchmarkContext.GetDefinition(caseId);
            var supportedOperations = definition.Operation switch
            {
                LokadPublicBenchmarkOperation.IsMatch => Utf8Pcre2BenchmarkOperation.IsMatch,
                LokadPublicBenchmarkOperation.Count => Utf8Pcre2BenchmarkOperation.Count,
                LokadPublicBenchmarkOperation.Replace => Utf8Pcre2BenchmarkOperation.Replace,
                _ => Utf8Pcre2BenchmarkOperation.None,
            };

            if (supportedOperations == Utf8Pcre2BenchmarkOperation.None)
            {
                continue;
            }

            if (!CanUseDotNetPerformanceManagedCompatibleCase(definition.Pattern, definition.Options))
            {
                continue;
            }

            yield return new Utf8Pcre2BenchmarkCase(
                caseId,
                definition.Pattern,
                definition.Input,
                definition.Options,
                replacement: definition.Replacement,
                supportedOperations: supportedOperations,
                supportedBackends: Utf8Pcre2BenchmarkBackend.AllManagedComparisons);
        }
    }

    private static bool CanUseDotNetPerformanceManagedCompatibleCase(string pattern, RegexOptions options)
    {
        try
        {
            _ = new Utf8Pcre2Regex(pattern, ToPcre2Options(options));
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
        catch (NotSupportedException)
        {
            return false;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }
}
