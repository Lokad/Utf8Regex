using System.Text;
using Lokad.Utf8Regex.PythonRe;

namespace Lokad.Utf8Regex.Benchmarks;

internal enum PythonReBenchmarkOperation : byte
{
    IsMatch = 0,
    Search = 1,
    Match = 2,
    FullMatch = 3,
    SearchDetailed = 4,
    Count = 5,
    FindAllStrings = 6,
    FindAllUtf8 = 7,
    FindIterDetailed = 8,
    ReplaceString = 9,
    ReplaceUtf8 = 10,
    SubnString = 11,
    SubnUtf8 = 12,
    SubnEvaluatorString = 13,
    SplitStrings = 14,
    SubnEvaluatorUtf8 = 15,
}

internal sealed record PythonReBenchmarkCase(
    string Id,
    string Pattern,
    PythonReCompileOptions Options,
    PythonReBenchmarkOperation Operation,
    string Input,
    string Replacement,
    bool IncludesResultMaterialization);

internal static class PythonReBenchmarkCatalog
{
    internal static IReadOnlyList<PythonReBenchmarkCase> Cases { get; } =
    [
        new("literal/ismatch", "needle", PythonReCompileOptions.None, PythonReBenchmarkOperation.IsMatch,
            new string('x', 65_536) + "needle", string.Empty, false),
        new("literal/search", "needle", PythonReCompileOptions.None, PythonReBenchmarkOperation.Search,
            new string('x', 65_536) + "needle", string.Empty, false),
        new("literal/search-miss", "needle", PythonReCompileOptions.None, PythonReBenchmarkOperation.Search,
            new string('x', 65_536), string.Empty, false),
        new("prefix/match", "header:[0-9]+", PythonReCompileOptions.None, PythonReBenchmarkOperation.Match,
            "header:12345 " + new string('x', 16_384), string.Empty, false),
        new("literal/fullmatch", "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789_-", PythonReCompileOptions.None, PythonReBenchmarkOperation.FullMatch,
            "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789_-", string.Empty, false),
        new("unicode/fullmatch", "(?:Шерлок )+", PythonReCompileOptions.None, PythonReBenchmarkOperation.FullMatch,
            Repeat("Шерлок ", 1_024), string.Empty, false),
        new("capture/search-detailed", "([a-z]+)-([0-9]+)", PythonReCompileOptions.None, PythonReBenchmarkOperation.SearchDetailed,
            "prefix item-123 suffix", string.Empty, true),
        new("family/count", "cat|dog|bird", PythonReCompileOptions.None, PythonReBenchmarkOperation.Count,
            Repeat("cat fox dog owl bird ", 4_096), string.Empty, false),
        new("class-run/count", "[a-z]+", PythonReCompileOptions.None, PythonReBenchmarkOperation.Count,
            Repeat("alpha beta gamma 123 ", 4_096), string.Empty, false),
        new("unicode/count", "Шерлок", PythonReCompileOptions.None, PythonReBenchmarkOperation.Count,
            Repeat("Шерлок и Ватсон. ", 4_096), string.Empty, false),
        new("findall/full-strings", "[a-z]+", PythonReCompileOptions.None, PythonReBenchmarkOperation.FindAllStrings,
            Repeat("alpha beta gamma 123 ", 1_024), string.Empty, true),
        new("findall/full-utf8", "[a-z]+", PythonReCompileOptions.None, PythonReBenchmarkOperation.FindAllUtf8,
            Repeat("alpha beta gamma 123 ", 1_024), string.Empty, true),
        new("findall/unicode-full-strings", "Шерлок", PythonReCompileOptions.None, PythonReBenchmarkOperation.FindAllStrings,
            Repeat("Шерлок и Ватсон. ", 512), string.Empty, true),
        new("findall/unicode-full-utf8", "Шерлок", PythonReCompileOptions.None, PythonReBenchmarkOperation.FindAllUtf8,
            Repeat("Шерлок и Ватсон. ", 512), string.Empty, true),
        new("findall/one-capture-strings", "item-([0-9]+)", PythonReCompileOptions.None, PythonReBenchmarkOperation.FindAllStrings,
            Repeat("item-12 item-345 ", 1_024), string.Empty, true),
        new("findall/many-capture-strings", "([a-z]+)-([0-9]+)", PythonReCompileOptions.None, PythonReBenchmarkOperation.FindAllStrings,
            Repeat("item-12 other-345 ", 1_024), string.Empty, true),
        new("findall/many-capture-utf8", "([a-z]+)-([0-9]+)", PythonReCompileOptions.None, PythonReBenchmarkOperation.FindAllUtf8,
            Repeat("item-12 other-345 ", 1_024), string.Empty, true),
        new("findall/unicode-capture-utf8", "(é+)-(𝒜𝒜|𝒜)", PythonReCompileOptions.None, PythonReBenchmarkOperation.FindAllUtf8,
            Repeat("éé-𝒜𝒜 é-𝒜 ", 512), string.Empty, true),
        new("iteration/finditer-detailed", "([a-z]+)-([0-9]+)", PythonReCompileOptions.None, PythonReBenchmarkOperation.FindIterDetailed,
            Repeat("item-12 other-345 ", 256), string.Empty, true),
        new("zero-width/count", @"\b", PythonReCompileOptions.Ascii, PythonReBenchmarkOperation.Count,
            Repeat("alpha beta gamma ", 1_024), string.Empty, false),
        new("replacement/fixed-string", "cat", PythonReCompileOptions.None, PythonReBenchmarkOperation.ReplaceString,
            Repeat("cat fox cat dog ", 2_048), "tiger", true),
        new("replacement/fixed-utf8", "cat", PythonReCompileOptions.None, PythonReBenchmarkOperation.ReplaceUtf8,
            Repeat("cat fox cat dog ", 2_048), "tiger", true),
        new("replacement/subn-string", "cat", PythonReCompileOptions.None, PythonReBenchmarkOperation.SubnString,
            Repeat("cat fox cat dog ", 1_024), "tiger", true),
        new("replacement/subn-utf8", "cat", PythonReCompileOptions.None, PythonReBenchmarkOperation.SubnUtf8,
            Repeat("cat fox cat dog ", 1_024), "tiger", true),
        new("replacement/evaluator-string", "([a-z]+)", PythonReCompileOptions.None, PythonReBenchmarkOperation.SubnEvaluatorString,
            Repeat("cat fox dog ", 512), "token", true),
        new("replacement/evaluator-utf8", "([a-z]+)", PythonReCompileOptions.None, PythonReBenchmarkOperation.SubnEvaluatorUtf8,
            Repeat("cat fox dog ", 512), "token", true),
        new("split/no-captures", "[,;]", PythonReCompileOptions.None, PythonReBenchmarkOperation.SplitStrings,
            Repeat("alpha,beta;gamma,delta;", 512), string.Empty, true),
        new("split/captures", "([,;])", PythonReCompileOptions.None, PythonReBenchmarkOperation.SplitStrings,
            Repeat("alpha,beta;gamma,delta;", 512), string.Empty, true),
    ];

    private static string Repeat(string value, int count)
    {
        var builder = new StringBuilder(value.Length * count);
        for (var index = 0; index < count; index++)
        {
            builder.Append(value);
        }

        return builder.ToString();
    }

    internal static string GetComparatorOwner(PythonReBenchmarkOperation operation) => operation switch
    {
        PythonReBenchmarkOperation.IsMatch or PythonReBenchmarkOperation.Search =>
            "_sre C Pattern.search",
        PythonReBenchmarkOperation.Match => "_sre C Pattern.match",
        PythonReBenchmarkOperation.FullMatch => "_sre C Pattern.fullmatch",
        PythonReBenchmarkOperation.SearchDetailed =>
            "_sre C Pattern.search + Python detailed projection",
        PythonReBenchmarkOperation.Count => "_sre scanner + Python finditer/sum",
        PythonReBenchmarkOperation.FindAllStrings => "_sre C Pattern.findall",
        PythonReBenchmarkOperation.FindAllUtf8 =>
            "_sre C Pattern.findall + Python UTF-8 projection",
        PythonReBenchmarkOperation.FindIterDetailed =>
            "_sre scanner + Python detailed projection",
        PythonReBenchmarkOperation.ReplaceString or PythonReBenchmarkOperation.ReplaceUtf8 =>
            "_sre C Pattern.sub",
        PythonReBenchmarkOperation.SubnString or PythonReBenchmarkOperation.SubnUtf8 =>
            "_sre C Pattern.subn",
        PythonReBenchmarkOperation.SubnEvaluatorString or PythonReBenchmarkOperation.SubnEvaluatorUtf8 =>
            "_sre C Pattern.subn + Python callback",
        PythonReBenchmarkOperation.SplitStrings => "_sre C Pattern.split",
        _ => throw new ArgumentOutOfRangeException(nameof(operation)),
    };

    internal static PythonReByteControlEligibility GetByteControlEligibility(
        PythonReBenchmarkCase benchmarkCase,
        ReadOnlySpan<byte> inputBytes)
    {
        if (benchmarkCase.Operation is not PythonReBenchmarkOperation.IsMatch and
            not PythonReBenchmarkOperation.Search and
            not PythonReBenchmarkOperation.Match and
            not PythonReBenchmarkOperation.FullMatch)
        {
            return new(
                false,
                "Excluded: the first byte-control profile is limited to one-shot matching operations.");
        }

        if (benchmarkCase.Pattern.Any(static character => character > 0x7f) ||
            inputBytes.ContainsAnyExceptInRange((byte)0, (byte)0x7f))
        {
            return new(false, "Excluded: pattern or subject is not entirely ASCII.");
        }

        if ((benchmarkCase.Options & ~PythonReCompileOptions.Ascii) != PythonReCompileOptions.None)
        {
            return new(false, "Excluded: flags are not proven equivalent for CPython bytes patterns.");
        }

        return new(
            true,
            "Eligible: ASCII one-shot semantics and byte/UTF-16 coordinates are identical.");
    }
}

internal readonly record struct PythonReByteControlEligibility(bool IsEligible, string Reason);
