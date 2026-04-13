using System.Text;
using System.Text.Unicode;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Input;

namespace Lokad.Utf8Regex.Benchmarks;

internal enum LokadPublicBenchmarkOperation : byte
{
    Count = 0,
    IsMatch = 1,
    Match = 2,
    Replace = 3,
    Split = 4,
}

internal sealed class LokadPublicBenchmarkContext
{
    internal readonly record struct Definition(
        string Input,
        string Replacement,
        string Pattern,
        RegexOptions Options,
        LokadPublicBenchmarkOperation Operation);

    private static readonly string[] s_caseIds =
    [
        "common/email-match",
        "common/email-miss",
        "common/date-match",
        "common/date-miss",
        "common/ip-match",
        "common/ip-miss",
        "common/uri-match",
        "common/uri-miss",
        "common/matches-set",
        "common/matches-boundary",
        "common/matches-word",
        "common/matches-words",
        "common/match-word",
        "common/replace-words",
        "common/split-words",
        "common/backtracking",
        "common/one-node-backtracking",
        "industry/mariomka-email-count",
        "industry/mariomka-uri-count",
        "industry/mariomka-ip-count",
        "industry/rust-sherlock-letter-count",
        "industry/rust-sherlock-holmes-window-count",
        "industry/rust-sherlock-ing-count",
        "industry/rust-sherlock-word-holmes-count",
        "industry/rust-sherlock-nonnewline-count",
        "industry/leipzig-twain-count",
        "industry/leipzig-name-family-count",
        "industry/leipzig-river-window-count",
        "industry/leipzig-symbol-count",
        "industry/boostdocs-ftp-line-match",
        "industry/boostdocs-credit-card-match",
        "industry/boostdocs-postcode-match",
        "industry/boostdocs-date-match",
        "industry/boostdocs-float-match",
    ];

    public LokadPublicBenchmarkContext(string caseId)
    {
        CaseId = caseId;
        (InputString, Replacement, Pattern, Options, Operation) = Resolve(caseId);
        InputBytes = Encoding.UTF8.GetBytes(InputString);
        Utf8Regex = new Utf8Regex(Pattern, Options);
        CompiledUtf8Regex = new Utf8Regex(Pattern, Options | RegexOptions.Compiled);
        Regex = new Regex(Pattern, Options, Regex.InfiniteMatchTimeout);
        CompiledRegex = new Regex(Pattern, Options | RegexOptions.Compiled, Regex.InfiniteMatchTimeout);
        HasCompiledLiteralFamilyRawMatch = CompiledUtf8Regex.DebugTryMatchCompiledAsciiLiteralFamilyRaw(InputBytes, out _compiledLiteralFamilyRawIndex, out _compiledLiteralFamilyRawLength);
    }

    private readonly bool HasCompiledLiteralFamilyRawMatch;
    private readonly int _compiledLiteralFamilyRawIndex;
    private readonly int _compiledLiteralFamilyRawLength;

    public string CaseId { get; }

    public string InputString { get; }

    public byte[] InputBytes { get; }

    public string Pattern { get; }

    public string Replacement { get; }

    public RegexOptions Options { get; }

    public LokadPublicBenchmarkOperation Operation { get; }

    public Utf8Regex Utf8Regex { get; }

    public Utf8Regex CompiledUtf8Regex { get; }

    public Regex Regex { get; }

    public Regex CompiledRegex { get; }

    public static IReadOnlyList<string> GetAllCaseIds() => s_caseIds;

    internal static Definition GetDefinition(string caseId)
    {
        var (input, replacement, pattern, options, operation) = Resolve(caseId);
        return new Definition(input, replacement, pattern, options, operation);
    }

    public int ExecuteUtf8Regex() => Execute(Utf8Regex);

    public int ExecuteUtf8Compiled() => Execute(CompiledUtf8Regex);

    public int ExecuteDecodeThenRegex() => Execute(Regex, Encoding.UTF8.GetString(InputBytes));

    public int ExecuteDecodeThenCompiledRegex() => Execute(CompiledRegex, Encoding.UTF8.GetString(InputBytes));

    public int ExecutePredecodedRegex() => Execute(Regex, InputString);

    public int ExecutePredecodedCompiledRegex() => Execute(CompiledRegex, InputString);

    public int ExecuteUtf8ValidationOnly() => Utf8Validation.Validate(InputBytes).Utf16Length;

    public int ExecuteUtf8IsValidOnly() => Utf8.IsValid(InputBytes) ? InputBytes.Length : 0;

    public int ExecuteUtf8InputValidateOnly() => Utf8InputAnalyzer.ValidateOnly(InputBytes).Utf16Length;

    public int ExecuteUtf8ValidationCoreWellFormedOnly()
    {
        return Utf8ValidationCore.TryValidate(InputBytes, computeUtf16Length: false, out _, out _)
            ? InputBytes.Length
            : 0;
    }

    public int ExecuteUtf8WellFormedOnly()
    {
        Utf8Validation.ThrowIfInvalidOnly(InputBytes);
        return InputBytes.Length;
    }

    public int ExecuteUtf8PrefilterOnly() => Utf8Regex.DebugRejectsByRequiredPrefilter(InputBytes) ? 0 : 1;

    public int ExecuteUtf8DirectHookOnly()
    {
        Utf8Regex.DebugTryMatchWithoutValidation(InputBytes, out var match);
        return MeasureMatch(match);
    }

    public int ExecuteUtf8DirectBoolOnly()
    {
        return Utf8Regex.DebugTryIsMatchWithoutValidation(InputBytes, out var isMatch) && isMatch
            ? 1
            : 0;
    }

    public int ExecuteUtf8PublicAfterValidationOnly()
    {
        var validation = Utf8Validation.Validate(InputBytes);
        return Operation switch
        {
            LokadPublicBenchmarkOperation.IsMatch => Utf8Regex.DebugMatchAfterValidation(InputBytes, validation).Success ? 1 : 0,
            LokadPublicBenchmarkOperation.Match => MeasureMatch(Utf8Regex.DebugMatchAfterValidation(InputBytes, validation)),
            _ => 0,
        };
    }

    public int ExecuteUtf8FallbackSearchStartsOnly()
    {
        if (Operation != LokadPublicBenchmarkOperation.IsMatch || !Utf8Regex.DebugCanGuideFallbackVerification)
        {
            return 0;
        }

        return Utf8Regex.DebugIsMatchFallbackViaSearchStarts(InputBytes) ? 1 : 0;
    }

    public int ExecuteUtf8CompiledAfterValidationOnly()
    {
        var validation = Utf8Validation.Validate(InputBytes);
        return Operation switch
        {
            LokadPublicBenchmarkOperation.IsMatch => CompiledUtf8Regex.DebugIsMatchViaCompiledEngine(InputBytes, validation) ? 1 : 0,
            LokadPublicBenchmarkOperation.Match => MeasureMatch(CompiledUtf8Regex.DebugMatchViaCompiledEngine(InputBytes, validation)),
            _ => 0,
        };
    }

    public int ExecuteUtf8CompiledBoolAfterValidationOnly()
    {
        var validation = Utf8Validation.Validate(InputBytes);
        return Operation switch
        {
            LokadPublicBenchmarkOperation.IsMatch or LokadPublicBenchmarkOperation.Match => CompiledUtf8Regex.DebugIsMatchViaCompiledEngine(InputBytes, validation) ? 1 : 0,
            _ => 0,
        };
    }

    public int ExecuteUtf8CompiledDirectNoValidationOnly()
    {
        return Operation switch
        {
            LokadPublicBenchmarkOperation.IsMatch => CompiledUtf8Regex.DebugTryMatchWithoutValidation(InputBytes, out var match) && match.Success ? 1 : 0,
            LokadPublicBenchmarkOperation.Match => MeasureMatch(CompiledUtf8Regex.DebugTryMatchWithoutValidation(InputBytes, out var match) ? match : Utf8ValueMatch.NoMatch),
            _ => 0,
        };
    }

    public int ExecuteUtf8CompiledLiteralFamilyRawMatchOnly()
    {
        if (Operation != LokadPublicBenchmarkOperation.Match)
        {
            return 0;
        }

        return CompiledUtf8Regex.DebugTryMatchCompiledAsciiLiteralFamilyRaw(InputBytes, out var index, out var matchedLength)
            ? index + matchedLength
            : -1;
    }

    public int ExecuteUtf8CompiledLiteralFamilyProjectionOnly()
    {
        if (Operation != LokadPublicBenchmarkOperation.Match || !HasCompiledLiteralFamilyRawMatch)
        {
            return 0;
        }

        return Utf8Regex.DebugProjectByteAlignedMatchOnly(_compiledLiteralFamilyRawIndex, _compiledLiteralFamilyRawLength);
    }

    public int ExecuteUtf8CompiledDirectCountOnly()
    {
        return Operation == LokadPublicBenchmarkOperation.Count
            ? CompiledUtf8Regex.DebugCountViaCompiledEngine(InputBytes)
            : 0;
    }

    public int ExecuteAnchoredValidatorFixedPrefixOnly()
    {
        if (!Utf8Regex.SimplePatternPlan.AnchoredValidatorPlan.HasValue)
        {
            return 0;
        }

        return Utf8Regex.DebugTryMatchAnchoredValidatorFixedPrefixOnly(InputBytes, out var matchedLength)
            ? matchedLength
            : 0;
    }

    public int ExecuteAnchoredValidatorFirstBoundedSegmentOnly()
    {
        if (!Utf8Regex.SimplePatternPlan.AnchoredValidatorPlan.HasValue)
        {
            return 0;
        }

        return Utf8Regex.DebugTryMatchAnchoredValidatorFirstBoundedSegmentOnly(InputBytes, out var matchedLength)
            ? matchedLength
            : 0;
    }

    public int ExecuteAnchoredValidatorSuffixAfterFirstBoundedOnly()
    {
        if (!Utf8Regex.SimplePatternPlan.AnchoredValidatorPlan.HasValue)
        {
            return 0;
        }

        return Utf8Regex.DebugTryMatchAnchoredValidatorSuffixAfterFirstBounded(InputBytes, out var matchedLength)
            ? matchedLength
            : 0;
    }

    public int ExecuteAnchoredValidatorNativeWholeOnly()
    {
        if (!Utf8Regex.SimplePatternPlan.AnchoredValidatorPlan.HasValue)
        {
            return 0;
        }

        return Utf8Regex.DebugTryMatchAnchoredValidatorNativeWhole(InputBytes, out var matchedLength)
            ? matchedLength
            : 0;
    }

    public int ExecuteCompiledAnchoredValidatorDirectOnly()
    {
        if (!CompiledUtf8Regex.SimplePatternPlan.AnchoredValidatorPlan.HasValue)
        {
            return 0;
        }

        return CompiledUtf8Regex.DebugTryMatchCompiledAnchoredValidatorWithoutValidation(InputBytes, out var matchedLength)
            ? matchedLength
            : 0;
    }

    public int ExecuteDirectFallbackTokenRawOnly()
    {
        return Utf8Regex.DebugTryFindDirectFallbackTokenWithoutValidation(InputBytes, out var matchIndex, out var matchedLength)
            ? matchIndex + matchedLength
            : 0;
    }

    public int ExecuteDateTokenWholeOnly()
    {
        if (Operation != LokadPublicBenchmarkOperation.IsMatch)
        {
            return 0;
        }

        return Utf8AsciiBoundedDateTokenExecutor.TryMatchWhole(
            InputBytes,
            CompiledUtf8Regex.SimplePatternPlan.AnchoredBoundedDatePlan,
            allowTrailingNewline: false,
            out var matchedLength,
            out _)
            ? matchedLength
            : 0;
    }

    public int ExecuteUriTokenWholeOnly()
    {
        if (Operation != LokadPublicBenchmarkOperation.IsMatch)
        {
            return 0;
        }

        return Utf8AsciiUriTokenExecutor.TryMatchWhole(InputBytes, out var matchedLength)
            ? matchedLength
            : 0;
    }

    public int ExecuteRepeatedDigitGroupWholeOnly()
    {
        return Utf8Regex.DebugTryMatchRepeatedDigitGroupWhole(InputBytes, out var matchedLength)
            ? matchedLength
            : 0;
    }

    public int ExecuteRepeatedDigitGroupFindOnly()
    {
        return Utf8Regex.DebugTryFindRepeatedDigitGroup(InputBytes, out var matchedLength)
            ? matchedLength
            : 0;
    }

    public int ExecuteUtf8DirectFixedLengthOnly()
    {
        if (!Utf8Regex.DebugSimplePatternCanUseDirectAnchoredFixedLength)
        {
            return 0;
        }

        return Utf8Regex.DebugTryMatchDirectAnchoredFixedLengthSimplePattern(InputBytes, out var matchedLength)
            ? matchedLength
            : 0;
    }

    public int ExecuteUtf8DirectFixedAlternationOnly()
    {
        if (!Utf8Regex.DebugSimplePatternCanUseDirectAnchoredFixedAlternation)
        {
            return 0;
        }

        return Utf8Regex.DebugTryMatchDirectAnchoredFixedAlternationSimplePattern(InputBytes, out var matchedLength)
            ? matchedLength
            : 0;
    }

    private int Execute(Utf8Regex regex)
    {
        return Operation switch
        {
            LokadPublicBenchmarkOperation.Count => regex.Count(InputBytes),
            LokadPublicBenchmarkOperation.IsMatch => regex.IsMatch(InputBytes) ? 1 : 0,
            LokadPublicBenchmarkOperation.Match => MeasureMatch(regex.Match(InputBytes)),
            LokadPublicBenchmarkOperation.Replace => regex.Replace(InputBytes, Replacement).Length,
            LokadPublicBenchmarkOperation.Split => CountUtf8Splits(regex, InputBytes),
            _ => throw new ArgumentOutOfRangeException(),
        };
    }

    private int Execute(Regex regex, string input)
    {
        return Operation switch
        {
            LokadPublicBenchmarkOperation.Count => regex.Count(input),
            LokadPublicBenchmarkOperation.IsMatch => regex.IsMatch(input) ? 1 : 0,
            LokadPublicBenchmarkOperation.Match => MeasureMatch(regex.Match(input)),
            LokadPublicBenchmarkOperation.Replace => regex.Replace(input, Replacement).Length,
            LokadPublicBenchmarkOperation.Split => regex.Split(input).Length,
            _ => throw new ArgumentOutOfRangeException(),
        };
    }

    private static int MeasureMatch(Utf8ValueMatch match) => match.Success ? match.IndexInUtf16 + match.LengthInUtf16 : -1;

    private static int MeasureMatch(Match match) => match.Success ? match.Index + match.Length : -1;

    private static int CountUtf8Splits(Utf8Regex regex, byte[] input)
    {
        var count = 0;
        foreach (var _ in regex.EnumerateSplits(input))
        {
            count++;
        }

        return count;
    }

    private static (string Input, string Replacement, string Pattern, RegexOptions Options, LokadPublicBenchmarkOperation Operation) Resolve(string caseId)
    {
        return caseId switch
        {
            "common/email-match" => ("ops.dispatch@northwind-control.net", string.Empty, @"^([a-zA-Z0-9_\-\.]+)@((\[[0-9]{1,3}\.[0-9]{1,3}\.[0-9]{1,3}\.)|(([a-zA-Z0-9\-]+\.)+))([a-zA-Z]{2,12}|[0-9]{1,3})(\]?)$", RegexOptions.None, LokadPublicBenchmarkOperation.IsMatch),
            "common/email-miss" => ("ops.dispatch@northwind-control.net#", string.Empty, @"^([a-zA-Z0-9_\-\.]+)@((\[[0-9]{1,3}\.[0-9]{1,3}\.[0-9]{1,3}\.)|(([a-zA-Z0-9\-]+\.)+))([a-zA-Z]{2,12}|[0-9]{1,3})(\]?)$", RegexOptions.None, LokadPublicBenchmarkOperation.IsMatch),
            "common/date-match" => ("Today is 11/18/2019 and tomorrow is 11/19/2019.", string.Empty, @"\b\d{1,2}\/\d{1,2}\/\d{2,4}\b", RegexOptions.None, LokadPublicBenchmarkOperation.IsMatch),
            "common/date-miss" => ("Today is 11/18/201A and tomorrow is 11/19/201A.", string.Empty, @"\b\d{1,2}\/\d{1,2}\/\d{2,4}\b", RegexOptions.None, LokadPublicBenchmarkOperation.IsMatch),
            "common/ip-match" => ("012.200.033.199", string.Empty, @"(?:(?:25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9])\.){3}(?:25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9])", RegexOptions.None, LokadPublicBenchmarkOperation.IsMatch),
            "common/ip-miss" => ("012.200.033.19A", string.Empty, @"(?:(?:25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9])\.){3}(?:25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9])", RegexOptions.None, LokadPublicBenchmarkOperation.IsMatch),
            "common/uri-match" => ("https://atlas.example.org/reports/export?id=42", string.Empty, @"[\w]+://[^/\s?#]+[^\s?#]+(?:\?[^\s#]*)?(?:#[^\s]*)?", RegexOptions.None, LokadPublicBenchmarkOperation.IsMatch),
            "common/uri-miss" => ("https://a https://b", string.Empty, @"[\w]+://[^/\s?#]+[^\s?#]+(?:\?[^\s#]*)?(?:#[^\s]*)?", RegexOptions.None, LokadPublicBenchmarkOperation.IsMatch),
            "common/matches-set" => (DotNetPerformanceRegexData.CommonSearchText, string.Empty, @"\w{10,}", RegexOptions.None, LokadPublicBenchmarkOperation.Count),
            "common/matches-boundary" => (DotNetPerformanceRegexData.CommonSearchText, string.Empty, @"\b\w{10,}\b", RegexOptions.None, LokadPublicBenchmarkOperation.Count),
            "common/matches-word" => (DotNetPerformanceRegexData.CommonSearchText, string.Empty, @"tempus", RegexOptions.None, LokadPublicBenchmarkOperation.Count),
            "common/matches-words" => (DotNetPerformanceRegexData.CommonSearchText, string.Empty, @"tempus|magna|semper", RegexOptions.None, LokadPublicBenchmarkOperation.Count),
            "common/match-word" => (DotNetPerformanceRegexData.CommonSearchText, string.Empty, @"tempus|magna|semper", RegexOptions.None, LokadPublicBenchmarkOperation.Match),
            "common/replace-words" => (DotNetPerformanceRegexData.CommonSearchText, "amoveatur", @"tempus|magna|semper", RegexOptions.None, LokadPublicBenchmarkOperation.Replace),
            "common/split-words" => (DotNetPerformanceRegexData.CommonSearchText, string.Empty, @"tempus|magna|semper", RegexOptions.None, LokadPublicBenchmarkOperation.Split),
            "common/backtracking" => ("Essential services are provided by regular exprs.", string.Empty, ".*(ss)", RegexOptions.None, LokadPublicBenchmarkOperation.IsMatch),
            "common/one-node-backtracking" => ("This regex has the potential to be optimized further", string.Empty, @"[^a]+\.[^z]+", RegexOptions.None, LokadPublicBenchmarkOperation.IsMatch),
            "industry/mariomka-email-count" => (DotNetPerformanceRegexData.MailNetworkCorpus, string.Empty, @"[\w\.+-]+@[\w\.-]+\.[\w\.-]+", RegexOptions.None, LokadPublicBenchmarkOperation.Count),
            "industry/mariomka-uri-count" => (DotNetPerformanceRegexData.MailNetworkCorpus, string.Empty, @"[\w]+://[^/\s?#]+[^\s?#]+(?:\?[^\s#]*)?(?:#[^\s]*)?", RegexOptions.None, LokadPublicBenchmarkOperation.Count),
            "industry/mariomka-ip-count" => (DotNetPerformanceRegexData.MailNetworkCorpus, string.Empty, @"(?:(?:25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9])\.){3}(?:25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9])", RegexOptions.None, LokadPublicBenchmarkOperation.Count),
            "industry/rust-sherlock-letter-count" => (DotNetPerformanceRegexData.DetectiveCorpus, string.Empty, @"\p{L}", RegexOptions.None, LokadPublicBenchmarkOperation.Count),
            "industry/rust-sherlock-holmes-window-count" => (DotNetPerformanceRegexData.DetectiveCorpus, string.Empty, @"Holmes.{0,25}Watson|Watson.{0,25}Holmes", RegexOptions.None, LokadPublicBenchmarkOperation.Count),
            "industry/rust-sherlock-ing-count" => (DotNetPerformanceRegexData.DetectiveCorpus, string.Empty, @"\s[a-zA-Z]{0,12}ing\s", RegexOptions.None, LokadPublicBenchmarkOperation.Count),
            "industry/rust-sherlock-word-holmes-count" => (DotNetPerformanceRegexData.DetectiveCorpus, string.Empty, @"\w+\s+Holmes", RegexOptions.None, LokadPublicBenchmarkOperation.Count),
            "industry/rust-sherlock-nonnewline-count" => (DotNetPerformanceRegexData.DetectiveCorpus, string.Empty, @"[^\n]*", RegexOptions.None, LokadPublicBenchmarkOperation.Count),
            "industry/leipzig-twain-count" => (DotNetPerformanceRegexData.RiverCorpus, string.Empty, "Twain", RegexOptions.None, LokadPublicBenchmarkOperation.Count),
            "industry/leipzig-name-family-count" => (DotNetPerformanceRegexData.RiverCorpus, string.Empty, "Tom|Sawyer|Huckleberry|Finn", RegexOptions.None, LokadPublicBenchmarkOperation.Count),
            "industry/leipzig-river-window-count" => (DotNetPerformanceRegexData.RiverCorpus, string.Empty, "Tom.{10,25}river|river.{10,25}Tom", RegexOptions.None, LokadPublicBenchmarkOperation.Count),
            "industry/leipzig-symbol-count" => (DotNetPerformanceRegexData.RiverCorpus, string.Empty, @"\p{Sm}", RegexOptions.None, LokadPublicBenchmarkOperation.Count),
            "industry/boostdocs-ftp-line-match" => ("100- this is a line of ftp response which contains a message string", string.Empty, @"^([0-9]+)(\-| |$)(.*)$", RegexOptions.None, LokadPublicBenchmarkOperation.IsMatch),
            "industry/boostdocs-credit-card-match" => ("1234-5678-1234-456", string.Empty, @"(\d{4}[- ]){3}\d{3,4}", RegexOptions.None, LokadPublicBenchmarkOperation.IsMatch),
            "industry/boostdocs-postcode-match" => ("SW1 1ZZ", string.Empty, @"^[a-zA-Z]{1,2}[0-9][0-9A-Za-z]{0,1} {0,1}[0-9][A-Za-z]{2}$", RegexOptions.None, LokadPublicBenchmarkOperation.IsMatch),
            "industry/boostdocs-date-match" => ("12/12/2001", string.Empty, @"^\d{1,2}/\d{1,2}/\d{4}$", RegexOptions.None, LokadPublicBenchmarkOperation.IsMatch),
            "industry/boostdocs-float-match" => ("-3.14159", string.Empty, @"^[-+]?\d*\.?\d*$", RegexOptions.None, LokadPublicBenchmarkOperation.IsMatch),
            _ => throw new ArgumentOutOfRangeException(nameof(caseId)),
        };
    }
}
