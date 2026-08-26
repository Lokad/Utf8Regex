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
    bool IncludesResultMaterialization,
    PythonReBenchmarkCoverage Coverage);

internal sealed record PythonReBenchmarkCoverage(
    string Section,
    string FeatureFamily,
    string InputShape,
    string ExpectedResultCardinality,
    int StartOffsetInBytes,
    int ReplacementCount,
    int MaxSplit,
    string ProjectionKind,
    string ComparatorOwner,
    string ByteControlExpectation,
    string IntendedManagedRouteClass,
    string CorpusProvenance,
    string ClaimClass,
    bool FirstMilestoneSentinel);

internal static class PythonReBenchmarkCatalog
{
    private const string PatternSearchOwner = "_sre C Pattern.search";
    private const string PatternMatchOwner = "_sre C Pattern.match";
    private const string PatternFullMatchOwner = "_sre C Pattern.fullmatch";
    private const string DetailedSearchOwner = "_sre C Pattern.search + Python detailed projection";
    private const string CountOwner = "_sre scanner + Python finditer/sum";
    private const string FindAllOwner = "_sre C Pattern.findall";
    private const string FindAllUtf8Owner = "_sre C Pattern.findall + Python UTF-8 projection";
    private const string DetailedIterationOwner = "_sre scanner + Python detailed projection";
    private const string SubOwner = "_sre C Pattern.sub";
    private const string SubnOwner = "_sre C Pattern.subn";
    private const string CallbackSubnOwner = "_sre C Pattern.subn + Python callback";
    private const string SplitOwner = "_sre C Pattern.split";

    internal static IReadOnlyList<PythonReBenchmarkCase> Cases { get; } =
    [
        new("literal/ismatch", "needle", PythonReCompileOptions.None, PythonReBenchmarkOperation.IsMatch,
            new string('x', 65_536) + "needle", string.Empty, false,
            C("Direct matching", "Exact literal", "Long ASCII late hit", "One", "Boolean",
                PatternSearchOwner, "Eligible", "ExactAsciiLiteral")),
        new("literal/search", "needle", PythonReCompileOptions.None, PythonReBenchmarkOperation.Search,
            new string('x', 65_536) + "needle", string.Empty, false,
            C("Direct matching", "Exact literal", "Long ASCII late hit", "One", "GroupZeroRanges",
                PatternSearchOwner, "Eligible", "ExactAsciiLiteral")),
        new("literal/search-miss", "needle", PythonReCompileOptions.None, PythonReBenchmarkOperation.Search,
            new string('x', 65_536), string.Empty, false,
            C("Direct matching", "Exact literal", "Long ASCII miss", "Zero", "GroupZeroRanges",
                PatternSearchOwner, "Eligible", "ExactAsciiLiteral")),
        new("prefix/match", "header:[0-9]+", PythonReCompileOptions.None, PythonReBenchmarkOperation.Match,
            "header:12345 " + new string('x', 16_384), string.Empty, false,
            C("Direct matching", "Prefix and digit repeat", "Long ASCII anchored hit", "One",
                "GroupZeroRanges", PatternMatchOwner, "Eligible", "ManagedFallback")),
        new("literal/fullmatch", "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789_-", PythonReCompileOptions.None, PythonReBenchmarkOperation.FullMatch,
            "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789_-", string.Empty, false,
            C("Direct matching", "Exact literal", "Short ASCII full hit", "One", "GroupZeroRanges",
                PatternFullMatchOwner, "Eligible", "SimpleNative")),
        new("unicode/fullmatch", "(?:Шерлок )+", PythonReCompileOptions.None, PythonReBenchmarkOperation.FullMatch,
            Repeat("Шерлок ", 1_024), string.Empty, false,
            C("Direct matching", "Quantified Unicode literal", "Long two-byte full hit", "One",
                "GroupZeroRanges", PatternFullMatchOwner, "NonAscii", "ManagedFallback")),
        new("literal/search-early", "needle", PythonReCompileOptions.None, PythonReBenchmarkOperation.Search,
            "needle" + new string('x', 65_536), string.Empty, false,
            C("Direct matching", "Exact literal", "Long ASCII early hit", "One", "GroupZeroRanges",
                PatternSearchOwner, "Eligible", "ExactAsciiLiteral", firstMilestoneSentinel: false)),
        new("anchor/fullmatch-miss", "[A-Z]{3}[0-9]{3}", PythonReCompileOptions.None,
            PythonReBenchmarkOperation.FullMatch, "ABC12x", string.Empty, false,
            C("Direct matching", "Anchors and fixed classes", "Short ASCII full miss", "Zero",
                "GroupZeroRanges", PatternFullMatchOwner, "Eligible", "SimpleNative",
                firstMilestoneSentinel: false)),
        new("ignorecase/search-hit", "sherlock", PythonReCompileOptions.IgnoreCase,
            PythonReBenchmarkOperation.Search, "prefix SHERLOCK suffix", string.Empty, false,
            C("Direct matching", "Ignore-case literal", "Short ASCII early hit", "One",
                "GroupZeroRanges", PatternSearchOwner, "Eligible", "SimpleNative",
                firstMilestoneSentinel: false)),
        new("ignorecase/search-miss", "sherlock", PythonReCompileOptions.IgnoreCase,
            PythonReBenchmarkOperation.Search, "prefix watson suffix", string.Empty, false,
            C("Direct matching", "Ignore-case literal", "Short ASCII miss", "Zero",
                "GroupZeroRanges", PatternSearchOwner, "Eligible", "SimpleNative",
                firstMilestoneSentinel: false)),
        new("multiline/search-hit", "^target$", PythonReCompileOptions.Multiline,
            PythonReBenchmarkOperation.Search, "before\ntarget\nafter", string.Empty, false,
            C("Direct matching", "Multiline anchors", "Short ASCII interior-line hit", "One",
                "GroupZeroRanges", PatternSearchOwner, "Eligible", "GeneralNative",
                firstMilestoneSentinel: false)),
        new("multiline/search-miss", "^target$", PythonReCompileOptions.Multiline,
            PythonReBenchmarkOperation.Search, "before\ntarget!\nafter", string.Empty, false,
            C("Direct matching", "Multiline anchors", "Short ASCII line miss", "Zero",
                "GroupZeroRanges", PatternSearchOwner, "Eligible", "GeneralNative",
                firstMilestoneSentinel: false)),
        new("dotall/fullmatch-hit", "start.*end", PythonReCompileOptions.DotAll,
            PythonReBenchmarkOperation.FullMatch, "start\n" + new string('x', 4_096) + "end", string.Empty, false,
            C("Direct matching", "Dot-all wildcard", "Medium ASCII full hit", "One",
                "GroupZeroRanges", PatternFullMatchOwner, "Eligible", "GeneralNative",
                firstMilestoneSentinel: false)),
        new("verbose/fullmatch-hit", "item \\s+ [0-9]+", PythonReCompileOptions.Verbose,
            PythonReBenchmarkOperation.FullMatch, "item   12345", string.Empty, false,
            C("Direct matching", "Verbose classes", "Short ASCII full hit", "One",
                "GroupZeroRanges", PatternFullMatchOwner, "Eligible", "GeneralNative",
                firstMilestoneSentinel: false)),
        new("unicode-option/search-hit", @"\w+", PythonReCompileOptions.Unicode,
            PythonReBenchmarkOperation.Search, "東京", string.Empty, false,
            C("Direct matching", "Unicode word category", "Short three-byte hit", "One",
                "GroupZeroRanges", PatternSearchOwner, "NonAscii", "ManagedFallback",
                firstMilestoneSentinel: false)),
        new("ascii-option/search-hit", @"\w+", PythonReCompileOptions.Ascii,
            PythonReBenchmarkOperation.Search, "abc_123", string.Empty, false,
            C("Direct matching", "ASCII word category", "Short ASCII full-span hit", "One",
                "GroupZeroRanges", PatternSearchOwner, "Eligible", "ManagedFallback",
                firstMilestoneSentinel: false)),
        new("lookahead/search-hit", "foo(?=bar)", PythonReCompileOptions.None,
            PythonReBenchmarkOperation.Search, "xxfoobarzz", string.Empty, false,
            C("Direct matching", "Positive lookahead", "Short ASCII interior hit", "One",
                "GroupZeroRanges", PatternSearchOwner, "Eligible", "GeneralNative",
                firstMilestoneSentinel: false)),
        new("lookbehind/search-miss", "(?<=foo)bar", PythonReCompileOptions.None,
            PythonReBenchmarkOperation.Search, "fooxbar", string.Empty, false,
            C("Direct matching", "Fixed-width lookbehind", "Short ASCII miss", "Zero",
                "GroupZeroRanges", PatternSearchOwner, "Eligible", "GeneralNative",
                firstMilestoneSentinel: false)),
        new("backreference/fullmatch-hit", @"([a-z]+)-\1", PythonReCompileOptions.None,
            PythonReBenchmarkOperation.FullMatch, "token-token", string.Empty, false,
            C("Direct matching", "Numeric capture and backreference", "Short ASCII full hit", "One",
                "GroupZeroRanges", PatternFullMatchOwner, "Eligible", "ManagedFallback",
                firstMilestoneSentinel: false)),
        new("atomic/search-hit", "(?>ab+)c", PythonReCompileOptions.None,
            PythonReBenchmarkOperation.Search, "xxabbbczz", string.Empty, false,
            C("Direct matching", "Atomic group", "Short ASCII interior hit", "One",
                "GroupZeroRanges", PatternSearchOwner, "Eligible", "GeneralNative",
                firstMilestoneSentinel: false)),
        new("possessive/search-miss", "a++a", PythonReCompileOptions.None,
            PythonReBenchmarkOperation.Search, "aaaa", string.Empty, false,
            C("Direct matching", "Possessive repeat", "Short ASCII miss", "Zero",
                "GroupZeroRanges", PatternSearchOwner, "Eligible", "GeneralNative",
                firstMilestoneSentinel: false)),
        new("reluctant/search-hit", "a+?b", PythonReCompileOptions.None,
            PythonReBenchmarkOperation.Search, "xxaaaabzz", string.Empty, false,
            C("Direct matching", "Reluctant repeat", "Short ASCII interior hit", "One",
                "GroupZeroRanges", PatternSearchOwner, "Eligible", "GeneralNative",
                firstMilestoneSentinel: false)),
        new("scoped-inline/search-hit", "(?i:token)-(?-i:ID)", PythonReCompileOptions.None,
            PythonReBenchmarkOperation.Search, "TOKEN-ID", string.Empty, false,
            C("Direct matching", "Scoped inline flags", "Short ASCII full-span hit", "One",
                "GroupZeroRanges", PatternSearchOwner, "Eligible", "GeneralNative",
                firstMilestoneSentinel: false)),
        new("supplementary/fullmatch-hit", "𝒜", PythonReCompileOptions.None,
            PythonReBenchmarkOperation.FullMatch, "𝒜", string.Empty, false,
            C("Direct matching", "Supplementary exact literal", "Short four-byte full hit", "One",
                "GroupZeroRanges", PatternFullMatchOwner, "NonAscii", "ManagedFallback",
                firstMilestoneSentinel: false)),
        new("capture/search-detailed", "([a-z]+)-([0-9]+)", PythonReCompileOptions.None, PythonReBenchmarkOperation.SearchDetailed,
            "prefix item-123 suffix", string.Empty, true,
            C("Detailed and scalar projections", "Separated captures", "Short ASCII early hit", "One",
                "DetailedMatch", DetailedSearchOwner, "OperationExcluded", "ManagedFallback",
                claimClass: "Composed")),
        new("family/count", "cat|dog|bird", PythonReCompileOptions.None, PythonReBenchmarkOperation.Count,
            Repeat("cat fox dog owl bird ", 4_096), string.Empty, false,
            C("Count, FindAll, and FindIter", "Literal alternation", "Long ASCII many matches", "Many",
                "ScalarCount", CountOwner, "OperationExcluded", "ExactUtf8Literal",
                claimClass: "Composed")),
        new("class-run/count", "[a-z]+", PythonReCompileOptions.None, PythonReBenchmarkOperation.Count,
            Repeat("alpha beta gamma 123 ", 4_096), string.Empty, false,
            C("Count, FindAll, and FindIter", "ASCII class run", "Long ASCII many matches", "Many",
                "ScalarCount", CountOwner, "OperationExcluded", "ManagedFallback",
                claimClass: "Composed")),
        new("unicode/count", "Шерлок", PythonReCompileOptions.None, PythonReBenchmarkOperation.Count,
            Repeat("Шерлок и Ватсон. ", 4_096), string.Empty, false,
            C("Count, FindAll, and FindIter", "Unicode literal", "Long two-byte many matches", "Many",
                "ScalarCount", CountOwner, "OperationExcluded", "ExactUtf8Literal",
                claimClass: "Composed")),
        new("findall/full-strings", "[a-z]+", PythonReCompileOptions.None, PythonReBenchmarkOperation.FindAllStrings,
            Repeat("alpha beta gamma 123 ", 1_024), string.Empty, true,
            C("Count, FindAll, and FindIter", "ASCII class run", "Long ASCII many matches", "Many",
                "StringValues", FindAllOwner, "OperationExcluded", "ManagedFallback")),
        new("findall/full-utf8", "[a-z]+", PythonReCompileOptions.None, PythonReBenchmarkOperation.FindAllUtf8,
            Repeat("alpha beta gamma 123 ", 1_024), string.Empty, true,
            C("Count, FindAll, and FindIter", "ASCII class run", "Long ASCII many matches", "Many",
                "Utf8Values", FindAllUtf8Owner, "OperationExcluded", "ManagedFallback",
                claimClass: "Composed")),
        new("findall/unicode-full-strings", "Шерлок", PythonReCompileOptions.None, PythonReBenchmarkOperation.FindAllStrings,
            Repeat("Шерлок и Ватсон. ", 512), string.Empty, true,
            C("Count, FindAll, and FindIter", "Unicode literal", "Long two-byte many matches", "Many",
                "StringValues", FindAllOwner, "OperationExcluded", "ExactUtf8Literal")),
        new("findall/unicode-full-utf8", "Шерлок", PythonReCompileOptions.None, PythonReBenchmarkOperation.FindAllUtf8,
            Repeat("Шерлок и Ватсон. ", 512), string.Empty, true,
            C("Count, FindAll, and FindIter", "Unicode literal", "Long two-byte many matches", "Many",
                "Utf8Values", FindAllUtf8Owner, "OperationExcluded", "ExactUtf8Literal",
                claimClass: "Composed")),
        new("findall/one-capture-strings", "item-([0-9]+)", PythonReCompileOptions.None, PythonReBenchmarkOperation.FindAllStrings,
            Repeat("item-12 item-345 ", 1_024), string.Empty, true,
            C("Count, FindAll, and FindIter", "One capture", "Long ASCII many matches", "Many",
                "CapturedStrings", FindAllOwner, "OperationExcluded", "ManagedFallback")),
        new("findall/many-capture-strings", "([a-z]+)-([0-9]+)", PythonReCompileOptions.None, PythonReBenchmarkOperation.FindAllStrings,
            Repeat("item-12 other-345 ", 1_024), string.Empty, true,
            C("Count, FindAll, and FindIter", "Separated captures", "Long ASCII many matches", "Many",
                "CaptureTuples", FindAllOwner, "OperationExcluded", "ManagedFallback")),
        new("findall/many-capture-utf8", "([a-z]+)-([0-9]+)", PythonReCompileOptions.None, PythonReBenchmarkOperation.FindAllUtf8,
            Repeat("item-12 other-345 ", 1_024), string.Empty, true,
            C("Count, FindAll, and FindIter", "Separated captures", "Long ASCII many matches", "Many",
                "CaptureTupleUtf8", FindAllUtf8Owner, "OperationExcluded", "ManagedFallback",
                claimClass: "Composed")),
        new("findall/unicode-capture-utf8", "(é+)-(𝒜𝒜|𝒜)", PythonReCompileOptions.None, PythonReBenchmarkOperation.FindAllUtf8,
            Repeat("éé-𝒜𝒜 é-𝒜 ", 512), string.Empty, true,
            C("Count, FindAll, and FindIter", "Mixed-width captures", "Long mixed-width many matches", "Many",
                "CaptureTupleUtf8", FindAllUtf8Owner, "OperationExcluded", "ManagedFallback",
                claimClass: "Composed")),
        new("iteration/finditer-detailed", "([a-z]+)-([0-9]+)", PythonReCompileOptions.None, PythonReBenchmarkOperation.FindIterDetailed,
            Repeat("item-12 other-345 ", 256), string.Empty, true,
            C("Count, FindAll, and FindIter", "Separated captures", "Medium ASCII many matches", "Many",
                "DetailedMatches", DetailedIterationOwner, "OperationExcluded", "ManagedFallback",
                claimClass: "Composed")),
        new("zero-width/count", @"\b", PythonReCompileOptions.Ascii, PythonReBenchmarkOperation.Count,
            Repeat("alpha beta gamma ", 1_024), string.Empty, false,
            C("Count, FindAll, and FindIter", "ASCII word boundary", "Long ASCII many empty matches", "ManyEmpty",
                "ScalarCount", CountOwner, "OperationExcluded", "ManagedFallback",
                claimClass: "Composed")),
        new("replacement/fixed-string", "cat", PythonReCompileOptions.None, PythonReBenchmarkOperation.ReplaceString,
            Repeat("cat fox cat dog ", 2_048), "tiger", true,
            C("Replace and Subn", "Exact literal replacement", "Long ASCII many matches", "Many",
                "ReplacementString", SubOwner, "OperationExcluded", "ExactAsciiLiteral")),
        new("replacement/fixed-utf8", "cat", PythonReCompileOptions.None, PythonReBenchmarkOperation.ReplaceUtf8,
            Repeat("cat fox cat dog ", 2_048), "tiger", true,
            C("Replace and Subn", "Exact literal replacement", "Long ASCII many matches", "Many",
                "ReplacementUtf8", SubOwner, "OperationExcluded", "ExactAsciiLiteral")),
        new("replacement/subn-string", "cat", PythonReCompileOptions.None, PythonReBenchmarkOperation.SubnString,
            Repeat("cat fox cat dog ", 1_024), "tiger", true,
            C("Replace and Subn", "Exact literal replacement", "Long ASCII many matches", "Many",
                "SubnString", SubnOwner, "OperationExcluded", "ExactAsciiLiteral")),
        new("replacement/subn-utf8", "cat", PythonReCompileOptions.None, PythonReBenchmarkOperation.SubnUtf8,
            Repeat("cat fox cat dog ", 1_024), "tiger", true,
            C("Replace and Subn", "Exact literal replacement", "Long ASCII many matches", "Many",
                "SubnUtf8", SubnOwner, "OperationExcluded", "ExactAsciiLiteral")),
        new("replacement/evaluator-string", "([a-z]+)", PythonReCompileOptions.None, PythonReBenchmarkOperation.SubnEvaluatorString,
            Repeat("cat fox dog ", 512), "token", true,
            C("Replace and Subn", "Callback replacement", "Medium ASCII many matches", "Many",
                "CallbackSubnString", CallbackSubnOwner, "OperationExcluded", "ManagedFallback",
                claimClass: "Composed")),
        new("replacement/evaluator-utf8", "([a-z]+)", PythonReCompileOptions.None, PythonReBenchmarkOperation.SubnEvaluatorUtf8,
            Repeat("cat fox dog ", 512), "token", true,
            C("Replace and Subn", "Callback replacement", "Medium ASCII many matches", "Many",
                "CallbackSubnUtf8", CallbackSubnOwner, "OperationExcluded", "ManagedFallback",
                claimClass: "Composed")),
        new("split/no-captures", "[,;]", PythonReCompileOptions.None, PythonReBenchmarkOperation.SplitStrings,
            Repeat("alpha,beta;gamma,delta;", 512), string.Empty, true,
            C("Split", "Separator class", "Medium ASCII many separators", "Many",
                "SplitStrings", SplitOwner, "OperationExcluded", "ManagedFallback")),
        new("split/captures", "([,;])", PythonReCompileOptions.None, PythonReBenchmarkOperation.SplitStrings,
            Repeat("alpha,beta;gamma,delta;", 512), string.Empty, true,
            C("Split", "Captured separator", "Medium ASCII many separators", "Many",
                "SplitStringsWithCaptures", SplitOwner, "OperationExcluded", "ManagedFallback")),
    ];

    private static PythonReBenchmarkCoverage C(
        string section,
        string featureFamily,
        string inputShape,
        string expectedResultCardinality,
        string projectionKind,
        string comparatorOwner,
        string byteControlExpectation,
        string intendedManagedRouteClass,
        string claimClass = "Public",
        int startOffsetInBytes = 0,
        int replacementCount = -1,
        int maxSplit = -1,
        string corpusProvenance = "Synthetic catalog generator",
        bool firstMilestoneSentinel = true) => new(
            section,
            featureFamily,
            inputShape,
            expectedResultCardinality,
            startOffsetInBytes,
            replacementCount,
            maxSplit,
            projectionKind,
            comparatorOwner,
            byteControlExpectation,
            intendedManagedRouteClass,
            corpusProvenance,
            claimClass,
            firstMilestoneSentinel);

    private static string Repeat(string value, int count)
    {
        var builder = new StringBuilder(value.Length * count);
        for (var index = 0; index < count; index++)
        {
            builder.Append(value);
        }

        return builder.ToString();
    }

    internal static string GetComparatorOwner(PythonReBenchmarkCase benchmarkCase) =>
        benchmarkCase.Coverage.ComparatorOwner;

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

        if ((benchmarkCase.Options & (PythonReCompileOptions.Locale | PythonReCompileOptions.Unicode)) !=
            PythonReCompileOptions.None)
        {
            return new(false, "Excluded: Locale or Unicode flags are not valid for equivalent CPython bytes patterns.");
        }

        return new(
            true,
            "Eligible: ASCII one-shot semantics and byte/UTF-16 coordinates are identical.");
    }
}

internal readonly record struct PythonReByteControlEligibility(bool IsEligible, string Reason);
