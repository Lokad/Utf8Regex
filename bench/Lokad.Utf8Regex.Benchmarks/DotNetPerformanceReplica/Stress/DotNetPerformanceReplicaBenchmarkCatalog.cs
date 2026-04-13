using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex.Benchmarks;

internal static class DotNetPerformanceReplicaBenchmarkCatalog
{
    private static readonly DotNetPerformanceReplicaBenchmarkCase[] s_cases =
    [
        // Source: dotnet-performance/benchmarks/definitions/curated/01-literal.toml
        new("literal/sherlock-en", "literal", DotNetPerformanceReplicaBenchmarkModel.Count,
            pattern: "Sherlock Holmes", regexRelativePath: null, regexPerLineAlternate: false,
            haystackRelativePath: @"opensubtitles\en-sampled.txt", haystackInline: null, haystackUtf8Lossy: false, haystackLineStart: null, haystackLineEnd: null,
            options: RegexOptions.None, expectedCount: 513, isDevelopmentSlice: false, origin: "curated/01-literal.toml :: sherlock-en"),
        // Source: dotnet-performance/benchmarks/definitions/curated/01-literal.toml
        new("literal/sherlock-casei-en", "literal", DotNetPerformanceReplicaBenchmarkModel.Count,
            pattern: "Sherlock Holmes", regexRelativePath: null, regexPerLineAlternate: false,
            haystackRelativePath: @"opensubtitles\en-sampled.txt", haystackInline: null, haystackUtf8Lossy: false, haystackLineStart: null, haystackLineEnd: null,
            options: RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, expectedCount: 522, isDevelopmentSlice: false, origin: "curated/01-literal.toml :: sherlock-casei-en"),
        // Source: dotnet-performance/benchmarks/definitions/curated/01-literal.toml
        new("literal/sherlock-ru", "literal", DotNetPerformanceReplicaBenchmarkModel.Count,
            pattern: "Шерлок Холмс", regexRelativePath: null, regexPerLineAlternate: false,
            haystackRelativePath: @"opensubtitles\ru-sampled.txt", haystackInline: null, haystackUtf8Lossy: false, haystackLineStart: null, haystackLineEnd: null,
            options: RegexOptions.None, expectedCount: 724, isDevelopmentSlice: false, origin: "curated/01-literal.toml :: sherlock-ru"),
        // Source: dotnet-performance/benchmarks/definitions/curated/01-literal.toml
        new("literal/sherlock-casei-ru", "literal", DotNetPerformanceReplicaBenchmarkModel.Count,
            pattern: "Шерлок Холмс", regexRelativePath: null, regexPerLineAlternate: false,
            haystackRelativePath: @"opensubtitles\ru-sampled.txt", haystackInline: null, haystackUtf8Lossy: false, haystackLineStart: null, haystackLineEnd: null,
            options: RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, expectedCount: 746, isDevelopmentSlice: false, origin: "curated/01-literal.toml :: sherlock-casei-ru"),
        // Source: dotnet-performance/benchmarks/definitions/curated/01-literal.toml
        new("literal/sherlock-zh", "literal", DotNetPerformanceReplicaBenchmarkModel.Count,
            pattern: "夏洛克·福尔摩斯", regexRelativePath: null, regexPerLineAlternate: false,
            haystackRelativePath: @"opensubtitles\zh-sampled.txt", haystackInline: null, haystackUtf8Lossy: false, haystackLineStart: null, haystackLineEnd: null,
            options: RegexOptions.None, expectedCount: 30, isDevelopmentSlice: false, origin: "curated/01-literal.toml :: sherlock-zh"),

        // Source: dotnet-performance/benchmarks/definitions/curated/02-literal-alternate.toml
        new("literal-alternate/sherlock-en", "literal-alternate", DotNetPerformanceReplicaBenchmarkModel.Count,
            pattern: "Sherlock Holmes|John Watson|Irene Adler|Inspector Lestrade|Professor Moriarty", regexRelativePath: null, regexPerLineAlternate: false,
            haystackRelativePath: @"opensubtitles\en-sampled.txt", haystackInline: null, haystackUtf8Lossy: false, haystackLineStart: null, haystackLineEnd: null,
            options: RegexOptions.None, expectedCount: 714, isDevelopmentSlice: false, origin: "curated/02-literal-alternate.toml :: sherlock-en"),
        // Synthetic no-match sibling for large-family literal diagnostics.
        new("literal-alternate/sherlock-en-nomatch", "literal-alternate", DotNetPerformanceReplicaBenchmarkModel.Count,
            pattern: "Mycroft Holmes|Mary Morstan|Mrs Hudson|Sebastian Moran|Charles Augustus Milverton", regexRelativePath: null, regexPerLineAlternate: false,
            haystackRelativePath: @"opensubtitles\en-sampled.txt", haystackInline: null, haystackUtf8Lossy: false, haystackLineStart: null, haystackLineEnd: null,
            options: RegexOptions.None, expectedCount: 0, isDevelopmentSlice: false, origin: "synthetic large-family no-match :: sherlock-en"),
        // Synthetic mixed-hit sibling for large-family literal diagnostics.
        new("literal-alternate/sherlock-en-mixed", "literal-alternate", DotNetPerformanceReplicaBenchmarkModel.Count,
            pattern: "Sherlock Holmes|John Watson|Mycroft Holmes|Mary Morstan|Mrs Hudson", regexRelativePath: null, regexPerLineAlternate: false,
            haystackRelativePath: @"opensubtitles\en-sampled.txt", haystackInline: null, haystackUtf8Lossy: false, haystackLineStart: null, haystackLineEnd: null,
            options: RegexOptions.None, expectedCount: null, isDevelopmentSlice: false, origin: "synthetic large-family mixed :: sherlock-en"),
        // Source: dotnet-performance/benchmarks/definitions/curated/02-literal-alternate.toml
        new("literal-alternate/sherlock-casei-en", "literal-alternate", DotNetPerformanceReplicaBenchmarkModel.Count,
            pattern: "Sherlock Holmes|John Watson|Irene Adler|Inspector Lestrade|Professor Moriarty", regexRelativePath: null, regexPerLineAlternate: false,
            haystackRelativePath: @"opensubtitles\en-sampled.txt", haystackInline: null, haystackUtf8Lossy: false, haystackLineStart: null, haystackLineEnd: null,
            options: RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, expectedCount: 725, isDevelopmentSlice: false, origin: "curated/02-literal-alternate.toml :: sherlock-casei-en"),
        // Source: dotnet-performance/benchmarks/definitions/curated/02-literal-alternate.toml
        new("literal-alternate/sherlock-ru", "literal-alternate", DotNetPerformanceReplicaBenchmarkModel.Count,
            pattern: "Шерлок Холмс|Джон Уотсон|Ирен Адлер|инспектор Лестрейд|профессор Мориарти", regexRelativePath: null, regexPerLineAlternate: false,
            haystackRelativePath: @"opensubtitles\ru-sampled.txt", haystackInline: null, haystackUtf8Lossy: false, haystackLineStart: null, haystackLineEnd: null,
            options: RegexOptions.None, expectedCount: 899, isDevelopmentSlice: false, origin: "curated/02-literal-alternate.toml :: sherlock-ru"),
        // Source: dotnet-performance/benchmarks/definitions/curated/02-literal-alternate.toml
        new("literal-alternate/sherlock-casei-ru", "literal-alternate", DotNetPerformanceReplicaBenchmarkModel.Count,
            pattern: "Шерлок Холмс|Джон Уотсон|Ирен Адлер|инспектор Лестрейд|профессор Мориарти", regexRelativePath: null, regexPerLineAlternate: false,
            haystackRelativePath: @"opensubtitles\ru-sampled.txt", haystackInline: null, haystackUtf8Lossy: false, haystackLineStart: null, haystackLineEnd: null,
            options: RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, expectedCount: 971, isDevelopmentSlice: false, origin: "curated/02-literal-alternate.toml :: sherlock-casei-ru"),
        // Source: dotnet-performance/benchmarks/definitions/curated/02-literal-alternate.toml
        new("literal-alternate/sherlock-zh", "literal-alternate", DotNetPerformanceReplicaBenchmarkModel.Count,
            pattern: "夏洛克·福尔摩斯|约翰华生|阿德勒|雷斯垂德|莫里亚蒂教授", regexRelativePath: null, regexPerLineAlternate: false,
            haystackRelativePath: @"opensubtitles\zh-sampled.txt", haystackInline: null, haystackUtf8Lossy: false, haystackLineStart: null, haystackLineEnd: null,
            options: RegexOptions.None, expectedCount: 207, isDevelopmentSlice: false, origin: "curated/02-literal-alternate.toml :: sherlock-zh"),

        // Source: dotnet-performance/benchmarks/definitions/curated/10-bounded-repeat.toml
        new("bounded-repeat/letters-en", "bounded-repeat", DotNetPerformanceReplicaBenchmarkModel.Count,
            pattern: "[A-Za-z]{8,13}", regexRelativePath: null, regexPerLineAlternate: false,
            haystackRelativePath: @"opensubtitles\en-sampled.txt", haystackInline: null, haystackUtf8Lossy: false, haystackLineStart: null, haystackLineEnd: 5000,
            options: RegexOptions.None, expectedCount: 1833, isDevelopmentSlice: false, origin: "curated/10-bounded-repeat.toml :: letters-en"),
        // Source: dotnet-performance/benchmarks/definitions/curated/10-bounded-repeat.toml
        new("bounded-repeat/letters-ru", "bounded-repeat", DotNetPerformanceReplicaBenchmarkModel.Count,
            pattern: "\\p{L}{8,13}", regexRelativePath: null, regexPerLineAlternate: false,
            haystackRelativePath: @"opensubtitles\ru-sampled.txt", haystackInline: null, haystackUtf8Lossy: false, haystackLineStart: null, haystackLineEnd: 5000,
            options: RegexOptions.None, expectedCount: 3475, isDevelopmentSlice: false, origin: "curated/10-bounded-repeat.toml :: letters-ru"),
        // Source: dotnet-performance/benchmarks/definitions/curated/10-bounded-repeat.toml
        new("bounded-repeat/context", "bounded-repeat", DotNetPerformanceReplicaBenchmarkModel.Count,
            pattern: "[A-Za-z]{10}\\s+[\\s\\S]{0,100}Result[\\s\\S]{0,100}\\s+[A-Za-z]{10}", regexRelativePath: null, regexPerLineAlternate: false,
            haystackRelativePath: @"rust-src-tools-3b0d4813.txt", haystackInline: null, haystackUtf8Lossy: false, haystackLineStart: null, haystackLineEnd: null,
            options: RegexOptions.None, expectedCount: 53, isDevelopmentSlice: false, origin: "curated/10-bounded-repeat.toml :: context"),
        // Source: dotnet-performance/benchmarks/definitions/curated/10-bounded-repeat.toml
        new("bounded-repeat/capitals", "bounded-repeat", DotNetPerformanceReplicaBenchmarkModel.Count,
            pattern: "(?:[A-Z][a-z]+\\s*){10,100}", regexRelativePath: null, regexPerLineAlternate: false,
            haystackRelativePath: @"rust-src-tools-3b0d4813.txt", haystackInline: null, haystackUtf8Lossy: false, haystackLineStart: null, haystackLineEnd: null,
            options: RegexOptions.None, expectedCount: 11, isDevelopmentSlice: false, origin: "curated/10-bounded-repeat.toml :: capitals"),
        // Source: dotnet-performance/benchmarks/definitions/curated/10-bounded-repeat.toml
        new("bounded-repeat/compile-context", "bounded-repeat", DotNetPerformanceReplicaBenchmarkModel.Compile,
            pattern: "[A-Za-z]{10}\\s+[\\s\\S]{0,100}Result[\\s\\S]{0,100}\\s+[A-Za-z]{10}", regexRelativePath: null, regexPerLineAlternate: false,
            haystackRelativePath: null, haystackInline: "abcdefghij blah blah blah Result blib blab klmnopqrst", haystackUtf8Lossy: false, haystackLineStart: null, haystackLineEnd: null,
            options: RegexOptions.None, expectedCount: 1, isDevelopmentSlice: false, origin: "curated/10-bounded-repeat.toml :: compile-context"),
        // Source: dotnet-performance/benchmarks/definitions/curated/10-bounded-repeat.toml
        new("bounded-repeat/compile-capitals", "bounded-repeat", DotNetPerformanceReplicaBenchmarkModel.Compile,
            pattern: "(?:[A-Z][a-z]+\\s*){10,100}", regexRelativePath: null, regexPerLineAlternate: false,
            haystackRelativePath: null, haystackInline: "Crazy Janey Mission Man Wild Billy Greasy Lake Hazy Davy Killer Joe", haystackUtf8Lossy: false, haystackLineStart: null, haystackLineEnd: null,
            options: RegexOptions.None, expectedCount: 1, isDevelopmentSlice: false, origin: "curated/10-bounded-repeat.toml :: compile-capitals"),

        // Source: dotnet-performance/benchmarks/definitions/curated/12-dictionary.toml
        new("dictionary/single", "dictionary", DotNetPerformanceReplicaBenchmarkModel.Count,
            pattern: null, regexRelativePath: @"dictionary\english\length-15.txt", regexPerLineAlternate: true,
            haystackRelativePath: @"opensubtitles\en-medium.txt", haystackInline: null, haystackUtf8Lossy: false, haystackLineStart: null, haystackLineEnd: null,
            options: RegexOptions.None, expectedCount: 1, isDevelopmentSlice: false, origin: "curated/12-dictionary.toml :: single"),
        // Source: dotnet-performance/benchmarks/definitions/curated/12-dictionary.toml
        new("dictionary/compile-single", "dictionary", DotNetPerformanceReplicaBenchmarkModel.Compile,
            pattern: null, regexRelativePath: @"dictionary\english\length-15.txt", regexPerLineAlternate: true,
            haystackRelativePath: null, haystackInline: "Zubeneschamali's", haystackUtf8Lossy: false, haystackLineStart: null, haystackLineEnd: null,
            options: RegexOptions.None, expectedCount: 1, isDevelopmentSlice: false, origin: "curated/12-dictionary.toml :: compile-single"),

        // Source: dotnet-performance/benchmarks/definitions/curated/09-aws-keys.toml
        new("aws-keys/full", "aws-keys", DotNetPerformanceReplicaBenchmarkModel.Count,
            pattern: "(('|\")((?:ASIA|AKIA|AROA|AIDA)([A-Z0-7]{16}))('|\").*?(\\n^.*?){0,4}(('|\\\")[a-zA-Z0-9+/]{40}('|\\\"))+|('|\\\")[a-zA-Z0-9+/]{40}('|\\\").*?(\\n^.*?){0,3}('|\\\")((?:ASIA|AKIA|AROA|AIDA)([A-Z0-7]{16}))('|\\\"))+",
            regexRelativePath: null, regexPerLineAlternate: false,
            haystackRelativePath: @"wild\cpython-226484e4.py", haystackInline: null, haystackUtf8Lossy: true, haystackLineStart: null, haystackLineEnd: null,
            options: RegexOptions.Multiline, expectedCount: 0, isDevelopmentSlice: false, origin: "curated/09-aws-keys.toml :: full"),
        // Source: dotnet-performance/benchmarks/definitions/curated/09-aws-keys.toml
        new("aws-keys/full-dev", "aws-keys", DotNetPerformanceReplicaBenchmarkModel.Count,
            pattern: "(('|\")((?:ASIA|AKIA|AROA|AIDA)([A-Z0-7]{16}))('|\").*?(\\n^.*?){0,4}(('|\\\")[a-zA-Z0-9+/]{40}('|\\\"))+|('|\\\")[a-zA-Z0-9+/]{40}('|\\\").*?(\\n^.*?){0,3}('|\\\")((?:ASIA|AKIA|AROA|AIDA)([A-Z0-7]{16}))('|\\\"))+",
            regexRelativePath: null, regexPerLineAlternate: false,
            haystackRelativePath: @"wild\cpython-226484e4.py", haystackInline: null, haystackUtf8Lossy: true, haystackLineStart: 0, haystackLineEnd: 20000,
            options: RegexOptions.Multiline, expectedCount: null, isDevelopmentSlice: true, origin: "curated/09-aws-keys.toml :: full (dev slice)"),
        // Source: dotnet-performance/benchmarks/definitions/curated/09-aws-keys.toml
        new("aws-keys/quick", "aws-keys", DotNetPerformanceReplicaBenchmarkModel.Count,
            pattern: "((?:ASIA|AKIA|AROA|AIDA)([A-Z0-7]{16}))", regexRelativePath: null, regexPerLineAlternate: false,
            haystackRelativePath: @"wild\cpython-226484e4.py", haystackInline: null, haystackUtf8Lossy: true, haystackLineStart: null, haystackLineEnd: null,
            options: RegexOptions.None, expectedCount: 0, isDevelopmentSlice: false, origin: "curated/09-aws-keys.toml :: quick"),
        // Source: dotnet-performance/benchmarks/definitions/curated/09-aws-keys.toml
        new("aws-keys/quick-dev", "aws-keys", DotNetPerformanceReplicaBenchmarkModel.Count,
            pattern: "((?:ASIA|AKIA|AROA|AIDA)([A-Z0-7]{16}))", regexRelativePath: null, regexPerLineAlternate: false,
            haystackRelativePath: @"wild\cpython-226484e4.py", haystackInline: null, haystackUtf8Lossy: true, haystackLineStart: 0, haystackLineEnd: 20000,
            options: RegexOptions.None, expectedCount: null, isDevelopmentSlice: true, origin: "curated/09-aws-keys.toml :: quick (dev slice)"),
        // Source: dotnet-performance/benchmarks/definitions/curated/09-aws-keys.toml
        new("aws-keys/compile-full", "aws-keys", DotNetPerformanceReplicaBenchmarkModel.Compile,
            pattern: "(('|\")((?:ASIA|AKIA|AROA|AIDA)([A-Z0-7]{16}))('|\").*?(\\n^.*?){0,4}(('|\\\")[a-zA-Z0-9+/]{40}('|\\\"))+|('|\\\")[a-zA-Z0-9+/]{40}('|\\\").*?(\\n^.*?){0,3}('|\\\")((?:ASIA|AKIA|AROA|AIDA)([A-Z0-7]{16}))('|\\\"))+",
            regexRelativePath: null, regexPerLineAlternate: false,
            haystackRelativePath: null, haystackInline: "\"AIDAABCDEFGHIJKLMNOP\"\"aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa\"", haystackUtf8Lossy: false, haystackLineStart: null, haystackLineEnd: null,
            options: RegexOptions.Multiline, expectedCount: 1, isDevelopmentSlice: false, origin: "curated/09-aws-keys.toml :: compile-full"),
        // Source: dotnet-performance/benchmarks/definitions/curated/09-aws-keys.toml
        new("aws-keys/compile-quick", "aws-keys", DotNetPerformanceReplicaBenchmarkModel.Compile,
            pattern: "((?:ASIA|AKIA|AROA|AIDA)([A-Z0-7]{16}))", regexRelativePath: null, regexPerLineAlternate: false,
            haystackRelativePath: null, haystackInline: "AIDAABCDEFGHIJKLMNOP", haystackUtf8Lossy: false, haystackLineStart: null, haystackLineEnd: null,
            options: RegexOptions.None, expectedCount: 1, isDevelopmentSlice: false, origin: "curated/09-aws-keys.toml :: compile-quick"),

        // Source: dotnet-performance/benchmarks/definitions/curated/04-ruff-noqa.toml
        new("ruff-noqa/real", "ruff-noqa", DotNetPerformanceReplicaBenchmarkModel.Count,
            pattern: "(\\s*)((?:# [Nn][Oo][Qq][Aa])(?::\\s?(([A-Z]+[0-9]+(?:[,\\s]+)?)+))?)", regexRelativePath: null, regexPerLineAlternate: false,
            haystackRelativePath: @"wild\cpython-226484e4.py", haystackInline: null, haystackUtf8Lossy: true, haystackLineStart: null, haystackLineEnd: null,
            options: RegexOptions.None, expectedCount: null, isDevelopmentSlice: false, origin: "curated/04-ruff-noqa.toml :: real"),
        // Source: dotnet-performance/benchmarks/definitions/curated/04-ruff-noqa.toml
        new("ruff-noqa/real-dev", "ruff-noqa", DotNetPerformanceReplicaBenchmarkModel.Count,
            pattern: "(\\s*)((?:# [Nn][Oo][Qq][Aa])(?::\\s?(([A-Z]+[0-9]+(?:[,\\s]+)?)+))?)", regexRelativePath: null, regexPerLineAlternate: false,
            haystackRelativePath: @"wild\cpython-226484e4.py", haystackInline: null, haystackUtf8Lossy: true, haystackLineStart: 0, haystackLineEnd: 2000,
            options: RegexOptions.None, expectedCount: null, isDevelopmentSlice: true, origin: "curated/04-ruff-noqa.toml :: real (dev slice)"),
        // Source: dotnet-performance/benchmarks/definitions/curated/04-ruff-noqa.toml
        new("ruff-noqa/tweaked", "ruff-noqa", DotNetPerformanceReplicaBenchmarkModel.Count,
            pattern: "(?:# [Nn][Oo][Qq][Aa])(?::\\s?(([A-Z]+[0-9]+(?:[,\\s]+)?)+))?", regexRelativePath: null, regexPerLineAlternate: false,
            haystackRelativePath: @"wild\cpython-226484e4.py", haystackInline: null, haystackUtf8Lossy: true, haystackLineStart: null, haystackLineEnd: null,
            options: RegexOptions.None, expectedCount: null, isDevelopmentSlice: false, origin: "curated/04-ruff-noqa.toml :: tweaked"),
        // Source: dotnet-performance/benchmarks/definitions/curated/04-ruff-noqa.toml
        new("ruff-noqa/tweaked-dev", "ruff-noqa", DotNetPerformanceReplicaBenchmarkModel.Count,
            pattern: "(?:# [Nn][Oo][Qq][Aa])(?::\\s?(([A-Z]+[0-9]+(?:[,\\s]+)?)+))?", regexRelativePath: null, regexPerLineAlternate: false,
            haystackRelativePath: @"wild\cpython-226484e4.py", haystackInline: null, haystackUtf8Lossy: true, haystackLineStart: 0, haystackLineEnd: 2000,
            options: RegexOptions.None, expectedCount: null, isDevelopmentSlice: true, origin: "curated/04-ruff-noqa.toml :: tweaked (dev slice)"),
        // Source: dotnet-performance/benchmarks/definitions/curated/04-ruff-noqa.toml
        new("ruff-noqa/compile-real", "ruff-noqa", DotNetPerformanceReplicaBenchmarkModel.Compile,
            pattern: "(\\s*)((?:# [Nn][Oo][Qq][Aa])(?::\\s?(([A-Z]+[0-9]+(?:[,\\s]+)?)+))?)", regexRelativePath: null, regexPerLineAlternate: false,
            haystackRelativePath: null, haystackInline: "# noqa", haystackUtf8Lossy: false, haystackLineStart: null, haystackLineEnd: null,
            options: RegexOptions.None, expectedCount: 1, isDevelopmentSlice: false, origin: "curated/04-ruff-noqa.toml :: compile-real"),
    ];

    public static IEnumerable<string> GetIds(string group, DotNetPerformanceReplicaBenchmarkModel model, bool developmentOnly = false)
    {
        return s_cases
            .Where(c => c.Group == group && c.Model == model && c.IsDevelopmentSlice == developmentOnly)
            .Select(static c => c.Id);
    }

    public static IEnumerable<DotNetPerformanceReplicaBenchmarkCase> GetAllCases() => s_cases;

    public static DotNetPerformanceReplicaBenchmarkCase Get(string id) => s_cases.First(c => c.Id == id);
}
