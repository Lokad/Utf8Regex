using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex.Benchmarks;

internal static class LokadReplicaCodeBenchmarkCatalog
{
    private static readonly LokadReplicaCodeBenchmarkCase[] s_cases =
    [
        new(
            "literal/identifier-token",
            "literal",
            LokadReplicaCodeBenchmarkModel.Count,
            LokadCodePatternMode.Literal,
            "CancellationToken",
            RegexOptions.CultureInvariant,
            expectedCount: null,
            intent: "literal_identifier",
            notes: "Coding-agent-style codebase probe for an exact identifier token."),
        new(
            "literal/call-token",
            "literal",
            LokadReplicaCodeBenchmarkModel.Count,
            LokadCodePatternMode.Literal,
            "ConfigureAwait(",
            RegexOptions.CultureInvariant,
            expectedCount: null,
            intent: "literal_long",
            notes: "Coding-agent-style exact call-token probe over a C# corpus."),
        new(
            "literal/identifier-token-casei",
            "literal",
            LokadReplicaCodeBenchmarkModel.Count,
            LokadCodePatternMode.Literal,
            "httpclient",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
            expectedCount: null,
            intent: "literal_identifier",
            notes: "Coding-agent-style ignore-case identifier probe over mixed source files."),
        new(
            "literal-family/type-token-family",
            "literal-family",
            LokadReplicaCodeBenchmarkModel.Count,
            LokadCodePatternMode.Regex,
            @"\b(?:Task|ValueTask|IAsyncEnumerable)\b",
            RegexOptions.CultureInvariant,
            expectedCount: null,
            intent: "regex_byte_fast",
            notes: "Coding-agent-style type-token family probe with identifier boundaries."),
        new(
            "literal-family/method-token-family",
            "literal-family",
            LokadReplicaCodeBenchmarkModel.Count,
            LokadCodePatternMode.Regex,
            @"\b(?:LogTrace|LogDebug|LogInformation|LogWarning|LogError)\b",
            RegexOptions.CultureInvariant,
            expectedCount: null,
            intent: "regex_byte_fast",
            notes: "Coding-agent-style method-token family probe across logging-heavy code."),
        new(
            "structural/keyword-to-generic-type",
            "structural",
            LokadReplicaCodeBenchmarkModel.Count,
            LokadCodePatternMode.Regex,
            @"async\s+Task<",
            RegexOptions.CultureInvariant,
            expectedCount: null,
            intent: "regex_byte_fast",
            notes: "Coding-agent-style signature probe for keyword plus generic return type."),
        new(
            "structural/keyword-family-to-capitalized-identifier",
            "structural",
            LokadReplicaCodeBenchmarkModel.Count,
            LokadCodePatternMode.Regex,
            @"\b(?:record|struct|class)\s+[A-Z][A-Za-z0-9_]+",
            RegexOptions.CultureInvariant,
            expectedCount: null,
            intent: "regex_byte_fast",
            notes: "Coding-agent-style declaration probe with keyword family plus capitalized identifier."),
        new(
            "structural/method-family-call",
            "structural",
            LokadReplicaCodeBenchmarkModel.Count,
            LokadCodePatternMode.Regex,
            @"\b(?:LogError|LogWarning|LogInformation)\s*\(",
            RegexOptions.CultureInvariant,
            expectedCount: null,
            intent: "regex_byte_fast",
            notes: "Coding-agent-style method-family call probe with optional separator."),
        new(
            "structural/ordered-keyword-pair",
            "structural",
            LokadReplicaCodeBenchmarkModel.Count,
            LokadCodePatternMode.Regex,
            @"await\s+using",
            RegexOptions.CultureInvariant,
            expectedCount: null,
            intent: "regex_byte_fast",
            notes: "Coding-agent-style ordered keyword-pair probe."),
        new(
            "structural/ordered-identifier-window",
            "structural",
            LokadReplicaCodeBenchmarkModel.Count,
            LokadCodePatternMode.Regex,
            @"\bHttpClient\b[\s\S]{0,80}\bSendAsync\b",
            RegexOptions.CultureInvariant,
            expectedCount: null,
            intent: "regex_multiline",
            notes: "Coding-agent-style ordered identifier-window probe with a bounded multiline gap."),
        new(
            "structural/modifier-family-to-type-window",
            "structural",
            LokadReplicaCodeBenchmarkModel.Count,
            LokadCodePatternMode.Regex,
            @"\b(?:public|private|internal)\b\s+.{0,80}\bclass\b",
            RegexOptions.CultureInvariant,
            expectedCount: null,
            intent: "regex_byte_fast",
            notes: "Coding-agent-style modifier-family to type-keyword window probe."),
        new(
            "structural/ordered-keyword-window",
            "structural",
            LokadReplicaCodeBenchmarkModel.Count,
            LokadCodePatternMode.Regex,
            @"\bawait\b\s+.{0,60}\bConfigureAwait\b",
            RegexOptions.CultureInvariant,
            expectedCount: null,
            intent: "regex_byte_fast",
            notes: "Coding-agent-style ordered keyword-window probe on a bounded same-line gap."),
        new(
            "structural/method-family-generic-call",
            "structural",
            LokadReplicaCodeBenchmarkModel.Count,
            LokadCodePatternMode.Regex,
            @"\b(?:AddSingleton|AddScoped|AddTransient)\s*<",
            RegexOptions.CultureInvariant,
            expectedCount: null,
            intent: "regex_byte_fast",
            notes: "Coding-agent-style generic method-family call probe."),
        new(
            "fallback/lookahead",
            "fallback",
            LokadReplicaCodeBenchmarkModel.Count,
            LokadCodePatternMode.Regex,
            @"HttpClient(?=\s+client)",
            RegexOptions.CultureInvariant,
            expectedCount: null,
            intent: "regex_fallback",
            notes: "Positive lookahead guardrail case that should remain semantically aligned with .NET Regex."),
        new(
            "balancing/begin-end-blocks",
            "balancing",
            LokadReplicaCodeBenchmarkModel.Count,
            LokadCodePatternMode.Regex,
            @"BEGIN(?:(?<open>BEGIN)|(?<-open>END)|(?:(?!BEGIN|END)[\s\S]))*END(?(open)(?!))",
            RegexOptions.CultureInvariant,
            expectedCount: null,
            intent: "regex_balancing",
            notes: "Synthetic BEGIN/END nested-block case added to stress .NET balancing-group semantics on a larger text corpus."),
        new(
            "structural/ordered-modifier-pair",
            "structural",
            LokadReplicaCodeBenchmarkModel.Count,
            LokadCodePatternMode.Regex,
            @"\bpublic\s+async\b",
            RegexOptions.CultureInvariant,
            expectedCount: null,
            intent: "regex_byte_fast",
            notes: "Coding-agent-style ordered modifier-pair probe with a separator-only gap."),
        new(
            "structural/modifier-family-to-type-pair",
            "structural",
            LokadReplicaCodeBenchmarkModel.Count,
            LokadCodePatternMode.Regex,
            @"\b(?:public|private|internal)\s+class\b",
            RegexOptions.CultureInvariant,
            expectedCount: null,
            intent: "regex_byte_fast",
            notes: "Coding-agent-style modifier-family to type-keyword probe with a separator-only gap."),
    ];

    public static IEnumerable<string> GetIds(string group, LokadReplicaCodeBenchmarkModel model)
    {
        return s_cases
            .Where(c => c.Group == group && c.Model == model)
            .Select(static c => c.Id);
    }

    public static IEnumerable<string> GetAllIds(LokadReplicaCodeBenchmarkModel model)
    {
        return s_cases
            .Where(c => c.Model == model)
            .Select(static c => c.Id);
    }

    public static IEnumerable<LokadReplicaCodeBenchmarkCase> GetAllCases() => s_cases;

    public static LokadReplicaCodeBenchmarkCase Get(string id) => s_cases.First(c => c.Id == id);
}
