using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex.Benchmarks;

internal static class LokadReplicaScriptBenchmarkCatalog
{
    private static readonly LokadReplicaScriptBenchmarkCase[] s_cases =
    [
        new(
            "lokad/imports/module-imports",
            "imports",
            LokadReplicaScriptBenchmarkModel.Count,
            "^import\\s+(?<shared>shared\\s+)?\"(?<path>(\\.|[^\\\\\"]*))\".*$",
            RegexOptions.Compiled | RegexOptions.Multiline,
            "Whole-document multiline import extraction from realistic Lokad script source files."),
        new(
            "lokad/folding/region-marker",
            "folding",
            LokadReplicaScriptBenchmarkModel.Count,
            "^\\s*///(?<title>#.*)$",
            RegexOptions.Compiled | RegexOptions.Multiline | RegexOptions.NonBacktracking,
            "Whole-document multiline region marker extraction used by the language server."),
        new(
            "lokad/lexer/identifier",
            "lexer",
            LokadReplicaScriptBenchmarkModel.PrefixMatchLoop,
            "\\G[a-z][a-z0-9_]*",
            RegexOptions.Compiled | RegexOptions.Multiline | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase,
            "Anchored lexer loop for case-insensitive identifiers.",
            sampleRelativePath: "Samples/lexer_identifier.txt"),
        new(
            "lokad/lexer/number",
            "lexer",
            LokadReplicaScriptBenchmarkModel.PrefixMatchLoop,
            "\\G-?[0-9]+(\\.[0-9]+)?(e[+-]?[0-9]+)?",
            RegexOptions.Compiled | RegexOptions.CultureInvariant,
            "Anchored lexer loop for script numeric literals.",
            sampleRelativePath: "Samples/lexer_number.txt"),
        new(
            "lokad/lexer/string",
            "lexer",
            LokadReplicaScriptBenchmarkModel.PrefixMatchLoop,
            "\\G\"([^\"\\\\]|\\\\.)*\"",
            RegexOptions.Compiled | RegexOptions.CultureInvariant,
            "Anchored lexer loop for quoted script text literals.",
            sampleRelativePath: "Samples/lexer_string.txt"),
        new(
            "lokad/lexer/operator-run",
            "lexer",
            LokadReplicaScriptBenchmarkModel.PrefixMatchLoop,
            "\\G[=+\\-^*/.*<>~!&|?]+",
            RegexOptions.Compiled | RegexOptions.CultureInvariant,
            "Anchored lexer loop for operator runs.",
            sampleRelativePath: "Samples/lexer_operator.txt"),
        new(
            "lokad/lexer/doc-line",
            "lexer",
            LokadReplicaScriptBenchmarkModel.PrefixMatchLoop,
            "\\G///[^\\n]*\\n",
            RegexOptions.Compiled | RegexOptions.Multiline | RegexOptions.CultureInvariant,
            "Anchored lexer loop for triple-slash documentation lines.",
            sampleRelativePath: "Samples/lexer_docline.txt",
            appendNewLineToSamples: true),
        new(
            "lokad/docs/inline-doc-prefix",
            "docs",
            LokadReplicaScriptBenchmarkModel.PrefixMatchLoop,
            """
            ^\s*
            (\[\|\s*)?
            ///\s?
            (?<doc>.*)$
            """,
            RegexOptions.Compiled | RegexOptions.IgnorePatternWhitespace,
            "Per-line inline documentation extraction used by Lokad scripts.",
            sampleRelativePath: "Samples/inline_doc_lines.txt"),
        new(
            "lokad/langserv/identifier-validator",
            "langserv",
            LokadReplicaScriptBenchmarkModel.PrefixMatchLoop,
            "^[a-z][a-z0-9_]*$",
            RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase,
            "Whole-string identifier validation used by completion and schema refactoring.",
            sampleRelativePath: "Samples/validator_identifier.txt"),
        new(
            "lokad/langserv/helper-identifier",
            "langserv",
            LokadReplicaScriptBenchmarkModel.PrefixMatchLoop,
            "[a-zA-Z][a-zA-Z0-9]*",
            RegexOptions.Compiled,
            "Helper identifier matching used by language-service helpers.",
            sampleRelativePath: "Samples/langserv_helper_identifier.txt"),
        new(
            "lokad/langserv/color-short-hex",
            "langserv",
            LokadReplicaScriptBenchmarkModel.PrefixMatchLoop,
            "^[a-f0-9]{3}$",
            RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase,
            "Three-digit hex parsing used by language-service color extraction.",
            sampleRelativePath: "Samples/langserv_color_short_hex.txt"),
        new(
            "lokad/langserv/color-rgb-hex",
            "langserv",
            LokadReplicaScriptBenchmarkModel.PrefixMatchLoop,
            "^(?<red>[a-f0-9]{2})(?<green>[a-f0-9]{2})(?<blue>[a-f0-9]{2})$",
            RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase,
            "Six-digit RGB hex parsing used by language-service color extraction.",
            sampleRelativePath: "Samples/langserv_color_rgb_hex.txt"),
        new(
            "lokad/langserv/url-dashboard",
            "langserv",
            LokadReplicaScriptBenchmarkModel.PrefixMatchLoop,
            "^(?<before>.*)(https://go(\\.testing)?\\.lokad.com|~)(?<trigram>/[a-zA-Z0-9]+)?/d/(?<topicId>\\d+)/?\\?t=(?<tab>[^ ?]+)(?<rest>.*)$",
            RegexOptions.Compiled | RegexOptions.IgnoreCase,
            "Dashboard URL detector from language-service URL refactoring.",
            sampleRelativePath: "Samples/langserv_url_dashboard.txt"),
        new(
            "lokad/langserv/url-download",
            "langserv",
            LokadReplicaScriptBenchmarkModel.PrefixMatchLoop,
            "^(?<before>.*)(https://go(\\.testing)?\\.lokad.com|~)(?<trigram>/[a-zA-Z0-9]+)?/gateway/BigFiles/Browse/Download\\?hash=(?<hash>[a-fA-F0-9]*)(?<optPath>[?&]path=[^& \\n]+)?[?&]name=(?<name>[^& ]+)(?<optPath2>[?&]path=[^& \\n]+)?(?<rest>.*)$",
            RegexOptions.Compiled | RegexOptions.IgnoreCase,
            "Download URL detector from language-service URL refactoring.",
            sampleRelativePath: "Samples/langserv_url_download.txt"),
        new(
            "lokad/style/hex-color",
            "style",
            LokadReplicaScriptBenchmarkModel.PrefixMatchLoop,
            "^#?([a-f0-9]{6}|[a-f0-9]{3})$",
            RegexOptions.Compiled | RegexOptions.IgnoreCase,
            "Whole-string style-code hex color validation.",
            sampleRelativePath: "Samples/validator_hex_color.txt"),
        new(
            "lokad/style/cell-ref",
            "style",
            LokadReplicaScriptBenchmarkModel.PrefixMatchLoop,
            "^(?<col>[a-z])(?<row>(\\d)+)$",
            RegexOptions.Compiled | RegexOptions.IgnoreCase,
            "Whole-string style-code single-cell reference validation.",
            sampleRelativePath: "Samples/validator_cell_ref.txt"),
        new(
            "lokad/style/range-ref",
            "style",
            LokadReplicaScriptBenchmarkModel.PrefixMatchLoop,
            "^(?<col1>[a-z])(?<row1>(\\d)+):?(?<col2>[a-z])(?<row2>(\\d)+)$",
            RegexOptions.Compiled | RegexOptions.IgnoreCase,
            "Whole-string style-code range reference validation.",
            sampleRelativePath: "Samples/validator_range_ref.txt"),
    ];

    public static IEnumerable<string> GetAllIds()
    {
        return s_cases.Select(static c => c.Id);
    }

    public static IEnumerable<LokadReplicaScriptBenchmarkCase> GetAllCases() => s_cases;

    public static LokadReplicaScriptBenchmarkCase Get(string id) => s_cases.First(c => c.Id == id);
}
