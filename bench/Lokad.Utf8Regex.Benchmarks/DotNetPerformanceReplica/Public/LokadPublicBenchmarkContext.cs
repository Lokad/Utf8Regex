using System.Text;
using System.Text.Unicode;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Diagnostics;
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

    private static readonly (string Id, LokadPublicBenchmarkOperation Operation)[] s_cases =
    [
        ("common/email-match", LokadPublicBenchmarkOperation.IsMatch),
        ("common/email-miss", LokadPublicBenchmarkOperation.IsMatch),
        ("common/date-match", LokadPublicBenchmarkOperation.IsMatch),
        ("common/date-miss", LokadPublicBenchmarkOperation.IsMatch),
        ("common/ip-match", LokadPublicBenchmarkOperation.IsMatch),
        ("common/ip-miss", LokadPublicBenchmarkOperation.IsMatch),
        ("common/uri-match", LokadPublicBenchmarkOperation.IsMatch),
        ("common/uri-miss", LokadPublicBenchmarkOperation.IsMatch),
        ("common/matches-set", LokadPublicBenchmarkOperation.Count),
        ("common/matches-boundary", LokadPublicBenchmarkOperation.Count),
        ("common/matches-word", LokadPublicBenchmarkOperation.Count),
        ("common/matches-words", LokadPublicBenchmarkOperation.Count),
        ("common/match-word", LokadPublicBenchmarkOperation.Match),
        ("common/replace-words", LokadPublicBenchmarkOperation.Replace),
        ("common/split-words", LokadPublicBenchmarkOperation.Split),
        ("common/backtracking", LokadPublicBenchmarkOperation.IsMatch),
        ("common/one-node-backtracking", LokadPublicBenchmarkOperation.IsMatch),
        ("industry/mariomka-email-count", LokadPublicBenchmarkOperation.Count),
        ("industry/mariomka-uri-count", LokadPublicBenchmarkOperation.Count),
        ("industry/mariomka-ip-count", LokadPublicBenchmarkOperation.Count),
        ("industry/rust-sherlock-letter-count", LokadPublicBenchmarkOperation.Count),
        ("industry/rust-sherlock-holmes-window-count", LokadPublicBenchmarkOperation.Count),
        ("industry/rust-sherlock-ing-count", LokadPublicBenchmarkOperation.Count),
        ("industry/rust-sherlock-word-holmes-count", LokadPublicBenchmarkOperation.Count),
        ("industry/rust-sherlock-nonnewline-count", LokadPublicBenchmarkOperation.Count),
        ("industry/leipzig-twain-count", LokadPublicBenchmarkOperation.Count),
        ("industry/leipzig-name-family-count", LokadPublicBenchmarkOperation.Count),
        ("industry/leipzig-river-window-count", LokadPublicBenchmarkOperation.Count),
        ("industry/leipzig-symbol-count", LokadPublicBenchmarkOperation.Count),
        ("industry/boostdocs-ftp-line-match", LokadPublicBenchmarkOperation.IsMatch),
        ("industry/boostdocs-credit-card-match", LokadPublicBenchmarkOperation.IsMatch),
        ("industry/boostdocs-postcode-match", LokadPublicBenchmarkOperation.IsMatch),
        ("industry/boostdocs-date-match", LokadPublicBenchmarkOperation.IsMatch),
        ("industry/boostdocs-float-match", LokadPublicBenchmarkOperation.IsMatch),
    ];

    private static readonly string[] s_caseIds = s_cases.Select(static c => c.Id).ToArray();

    public LokadPublicBenchmarkContext(string caseId)
    {
        CaseId = caseId;
        (InputString, Replacement, Pattern, Options) = Resolve(caseId);
        Operation = GetOperation(caseId);
        InputBytes = Encoding.UTF8.GetBytes(InputString);
        Utf8Regex = new Utf8Regex(Pattern, Options);
        CompiledUtf8Regex = new Utf8Regex(Pattern, Options | RegexOptions.Compiled);
        Regex = new Regex(Pattern, Options, Regex.InfiniteMatchTimeout);
        CompiledRegex = new Regex(Pattern, Options | RegexOptions.Compiled, Regex.InfiniteMatchTimeout);
        HasCompiledLiteralFamilyRawMatch = CompiledUtf8Regex.Inspection.DebugTryMatchCompiledAsciiLiteralFamilyRaw(InputBytes, out _compiledLiteralFamilyRawIndex, out _compiledLiteralFamilyRawLength);
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

    public static LokadPublicBenchmarkOperation GetOperation(string caseId)
    {
        foreach (var item in s_cases)
            if (item.Id == caseId) return item.Operation;
        throw new ArgumentOutOfRangeException(nameof(caseId));
    }

    internal static Definition GetDefinition(string caseId)
    {
        var (input, replacement, pattern, options) = Resolve(caseId);
        return new Definition(input, replacement, pattern, options, GetOperation(caseId));
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

    public int ExecuteUtf8PrefilterOnly() => Utf8Regex.Inspection.DebugRejectsByRequiredPrefilter(InputBytes) ? 0 : 1;

    public int ExecuteUtf8DirectHookOnly()
    {
        Utf8Regex.TryMatchDirectWithoutValidation(InputBytes, out var match);
        return MeasureMatch(match);
    }

    public int ExecuteUtf8DirectBoolOnly()
    {
        return Utf8Regex.TryIsMatchDirectWithoutValidation(InputBytes, out var isMatch) && isMatch
            ? 1
            : 0;
    }

    public int ExecuteUtf8PublicAfterValidationOnly()
    {
        var validation = Utf8Validation.Validate(InputBytes);
        return Operation switch
        {
            LokadPublicBenchmarkOperation.IsMatch => Utf8Regex.Inspection.DebugMatchAfterValidation(InputBytes, validation).Success ? 1 : 0,
            LokadPublicBenchmarkOperation.Match => MeasureMatch(Utf8Regex.Inspection.DebugMatchAfterValidation(InputBytes, validation)),
            _ => 0,
        };
    }

    public int ExecuteUtf8FallbackSearchStartsOnly()
    {
        if (Operation != LokadPublicBenchmarkOperation.IsMatch || !Utf8Regex.Inspection.DebugCanGuideFallbackVerification)
        {
            return 0;
        }

        return Utf8FallbackReplay.IsMatch(Utf8Regex, InputBytes) ? 1 : 0;
    }

    public int ExecuteUtf8CompiledAfterValidationOnly()
    {
        var validation = Utf8Validation.Validate(InputBytes);
        return Operation switch
        {
            LokadPublicBenchmarkOperation.IsMatch => CompiledUtf8Regex.Inspection.DebugIsMatchViaCompiledEngine(InputBytes, validation) ? 1 : 0,
            LokadPublicBenchmarkOperation.Match => MeasureMatch(CompiledUtf8Regex.Inspection.DebugMatchViaCompiledEngine(InputBytes, validation)),
            _ => 0,
        };
    }

    public int ExecuteUtf8CompiledBoolAfterValidationOnly()
    {
        var validation = Utf8Validation.Validate(InputBytes);
        return Operation switch
        {
            LokadPublicBenchmarkOperation.IsMatch or LokadPublicBenchmarkOperation.Match => CompiledUtf8Regex.Inspection.DebugIsMatchViaCompiledEngine(InputBytes, validation) ? 1 : 0,
            _ => 0,
        };
    }

    public int ExecuteUtf8CompiledDirectNoValidationOnly()
    {
        return Operation switch
        {
            LokadPublicBenchmarkOperation.IsMatch => CompiledUtf8Regex.TryMatchDirectWithoutValidation(InputBytes, out var match) && match.Success ? 1 : 0,
            LokadPublicBenchmarkOperation.Match => MeasureMatch(CompiledUtf8Regex.TryMatchDirectWithoutValidation(InputBytes, out var match) ? match : Utf8ValueMatch.NoMatch),
            _ => 0,
        };
    }

    public int ExecuteUtf8CompiledLiteralFamilyRawMatchOnly()
    {
        if (Operation != LokadPublicBenchmarkOperation.Match)
        {
            return 0;
        }

        return CompiledUtf8Regex.Inspection.DebugTryMatchCompiledAsciiLiteralFamilyRaw(InputBytes, out var index, out var matchedLength)
            ? index + matchedLength
            : -1;
    }

    public int ExecuteUtf8CompiledLiteralFamilyProjectionOnly()
    {
        if (Operation != LokadPublicBenchmarkOperation.Match || !HasCompiledLiteralFamilyRawMatch)
        {
            return 0;
        }

        return Utf8MatchInspection.ProjectByteAlignedMatchOnly(_compiledLiteralFamilyRawIndex, _compiledLiteralFamilyRawLength);
    }

    public int ExecuteUtf8CompiledDirectCountOnly()
    {
        return Operation == LokadPublicBenchmarkOperation.Count
            ? CompiledUtf8Regex.Inspection.DebugCountViaCompiledEngine(InputBytes)
            : 0;
    }

    public int ExecuteAnchoredValidatorFixedPrefixOnly()
    {
        if (!Utf8Regex.Inspection.SimplePatternPlan.AnchoredValidatorPlan.HasValue)
        {
            return 0;
        }

        return Utf8Regex.Inspection.DebugTryMatchAnchoredValidatorFixedPrefixOnly(InputBytes, out var matchedLength)
            ? matchedLength
            : 0;
    }

    public int ExecuteAnchoredValidatorFirstBoundedSegmentOnly()
    {
        if (!Utf8Regex.Inspection.SimplePatternPlan.AnchoredValidatorPlan.HasValue)
        {
            return 0;
        }

        return Utf8Regex.Inspection.DebugTryMatchAnchoredValidatorFirstBoundedSegmentOnly(InputBytes, out var matchedLength)
            ? matchedLength
            : 0;
    }

    public int ExecuteAnchoredValidatorSuffixAfterFirstBoundedOnly()
    {
        if (!Utf8Regex.Inspection.SimplePatternPlan.AnchoredValidatorPlan.HasValue)
        {
            return 0;
        }

        return Utf8Regex.Inspection.DebugTryMatchAnchoredValidatorSuffixAfterFirstBounded(InputBytes, out var matchedLength)
            ? matchedLength
            : 0;
    }

    public int ExecuteAnchoredValidatorNativeWholeOnly()
    {
        if (!Utf8Regex.Inspection.SimplePatternPlan.AnchoredValidatorPlan.HasValue)
        {
            return 0;
        }

        return Utf8Regex.Inspection.DebugTryMatchAnchoredValidatorNativeWhole(InputBytes, out var matchedLength)
            ? matchedLength
            : 0;
    }

    public int ExecuteCompiledAnchoredValidatorDirectOnly()
    {
        if (!CompiledUtf8Regex.Inspection.SimplePatternPlan.AnchoredValidatorPlan.HasValue)
        {
            return 0;
        }

        return CompiledUtf8Regex.Inspection.DebugTryMatchCompiledAnchoredValidatorWithoutValidation(InputBytes, out var matchedLength)
            ? matchedLength
            : 0;
    }

    public int ExecuteEmittedWholeDirectOnly()
    {
        return CompiledUtf8Regex.Inspection.DebugTryMatchEmittedWhole(InputBytes, out var matchedLength)
            ? matchedLength
            : 0;
    }

    public int ExecuteDirectFallbackTokenRawOnly()
    {
        return Utf8Regex.Inspection.DebugTryFindDirectFallbackTokenWithoutValidation(InputBytes, out var matchIndex, out var matchedLength)
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
            CompiledUtf8Regex.Inspection.SimplePatternPlan.AnchoredBoundedDatePlan,
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
        var plan = Utf8Regex.Inspection.SimplePatternPlan.RepeatedDigitGroupPlan;
        return plan.HasValue && Utf8AsciiRepeatedDigitGroupExecutor.TryMatchWhole(
            InputBytes, plan, out var matchedLength, out _)
            ? matchedLength
            : 0;
    }

    public int ExecuteRepeatedDigitGroupFindOnly()
    {
        var plan = Utf8Regex.Inspection.SimplePatternPlan.RepeatedDigitGroupPlan;
        return plan.HasValue && Utf8AsciiRepeatedDigitGroupExecutor.TryFind(
            InputBytes, plan, out _, out var matchedLength)
            ? matchedLength
            : 0;
    }

    public int ExecuteUtf8DirectFixedLengthOnly()
    {
        var prepared = Utf8Regex.Inspection.PreparedRegex;
        if (!Utf8SimplePatternCompiledRuntimePolicy.CanUseDirectAnchoredFixedLengthSimplePattern(prepared))
        {
            return 0;
        }

        return Utf8SimplePatternCompiledWholeMatcher.TryMatchDirectAnchoredFixedLengthSimplePattern(
            prepared, InputBytes, out var matchedLength)
            ? matchedLength
            : 0;
    }

    public int ExecuteUtf8DirectFixedAlternationOnly()
    {
        var prepared = Utf8Regex.Inspection.PreparedRegex;
        if (!Utf8SimplePatternCompiledRuntimePolicy.CanUseDirectAnchoredFixedAlternationSimplePattern(prepared))
        {
            return 0;
        }

        return Utf8SimplePatternCompiledWholeMatcher.TryMatchDirectAnchoredFixedAlternationSimplePattern(
            prepared, InputBytes, out var matchedLength)
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
            LokadPublicBenchmarkOperation.Replace => RegexBenchmarkResult.ReplaceUtf8Length(regex, input, Replacement),
            LokadPublicBenchmarkOperation.Split => CountRegexSplits(regex, input),
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

    private static int CountRegexSplits(Regex regex, string input)
    {
        var count = 0;
        foreach (var _ in regex.EnumerateSplits(input))
        {
            count++;
        }

        return count;
    }

    private static (string Input, string Replacement, string Pattern, RegexOptions Options) Resolve(string caseId)
    {
        return caseId switch
        {
            "common/email-match" => ("ops.dispatch@northwind-control.net", string.Empty, @"^([a-zA-Z0-9_\-\.]+)@((\[[0-9]{1,3}\.[0-9]{1,3}\.[0-9]{1,3}\.)|(([a-zA-Z0-9\-]+\.)+))([a-zA-Z]{2,12}|[0-9]{1,3})(\]?)$", RegexOptions.None),
            "common/email-miss" => ("ops.dispatch@northwind-control.net#", string.Empty, @"^([a-zA-Z0-9_\-\.]+)@((\[[0-9]{1,3}\.[0-9]{1,3}\.[0-9]{1,3}\.)|(([a-zA-Z0-9\-]+\.)+))([a-zA-Z]{2,12}|[0-9]{1,3})(\]?)$", RegexOptions.None),
            "common/date-match" => ("Today is 11/18/2019 and tomorrow is 11/19/2019.", string.Empty, @"\b\d{1,2}\/\d{1,2}\/\d{2,4}\b", RegexOptions.None),
            "common/date-miss" => ResolveSharedSubject("common/date-miss"),
            "common/ip-match" => ResolveSharedSubject("common/ip-match"),
            "common/ip-miss" => ("012.200.033.19A", string.Empty, @"(?:(?:25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9])\.){3}(?:25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9])", RegexOptions.None),
            "common/uri-match" => ("https://atlas.example.org/reports/export?id=42", string.Empty, @"[\w]+://[^/\s?#]+[^\s?#]+(?:\?[^\s#]*)?(?:#[^\s]*)?", RegexOptions.None),
            "common/uri-miss" => ("https://a https://b", string.Empty, @"[\w]+://[^/\s?#]+[^\s?#]+(?:\?[^\s#]*)?(?:#[^\s]*)?", RegexOptions.None),
            "common/matches-set" => (DotNetPerformanceRegexData.CommonSearchText, string.Empty, @"\w{10,}", RegexOptions.None),
            "common/matches-boundary" => (DotNetPerformanceRegexData.CommonSearchText, string.Empty, @"\b\w{10,}\b", RegexOptions.None),
            "common/matches-word" => (DotNetPerformanceRegexData.CommonSearchText, string.Empty, @"tempus", RegexOptions.None),
            "common/matches-words" => (DotNetPerformanceRegexData.CommonSearchText, string.Empty, @"tempus|magna|semper", RegexOptions.None),
            "common/match-word" => (DotNetPerformanceRegexData.CommonSearchText, string.Empty, @"tempus|magna|semper", RegexOptions.None),
            "common/replace-words" => (DotNetPerformanceRegexData.CommonSearchText, "amoveatur", @"tempus|magna|semper", RegexOptions.None),
            "common/split-words" => (DotNetPerformanceRegexData.CommonSearchText, string.Empty, @"tempus|magna|semper", RegexOptions.None),
            "common/backtracking" => ("Essential services are provided by regular exprs.", string.Empty, ".*(ss)", RegexOptions.None),
            "common/one-node-backtracking" => ("This regex has the potential to be optimized further", string.Empty, @"[^a]+\.[^z]+", RegexOptions.None),
            "industry/mariomka-email-count" => ResolveSharedSubject("industry/mariomka-email-count"),
            "industry/mariomka-uri-count" => (DotNetPerformanceRegexData.MailNetworkCorpus, string.Empty, @"[\w]+://[^/\s?#]+[^\s?#]+(?:\?[^\s#]*)?(?:#[^\s]*)?", RegexOptions.None),
            "industry/mariomka-ip-count" => (DotNetPerformanceRegexData.MailNetworkCorpus, string.Empty, @"(?:(?:25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9])\.){3}(?:25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9])", RegexOptions.None),
            "industry/rust-sherlock-letter-count" => (DotNetPerformanceRegexData.DetectiveCorpus, string.Empty, @"\p{L}", RegexOptions.None),
            "industry/rust-sherlock-holmes-window-count" => (DotNetPerformanceRegexData.DetectiveCorpus, string.Empty, @"Holmes.{0,25}Watson|Watson.{0,25}Holmes", RegexOptions.None),
            "industry/rust-sherlock-ing-count" => (DotNetPerformanceRegexData.DetectiveCorpus, string.Empty, @"\s[a-zA-Z]{0,12}ing\s", RegexOptions.None),
            "industry/rust-sherlock-word-holmes-count" => (DotNetPerformanceRegexData.DetectiveCorpus, string.Empty, @"\w+\s+Holmes", RegexOptions.None),
            "industry/rust-sherlock-nonnewline-count" => (DotNetPerformanceRegexData.DetectiveCorpus, string.Empty, @"[^\n]*", RegexOptions.None),
            "industry/leipzig-twain-count" => (DotNetPerformanceRegexData.RiverCorpus, string.Empty, "Twain", RegexOptions.None),
            "industry/leipzig-name-family-count" => (DotNetPerformanceRegexData.RiverCorpus, string.Empty, "Tom|Sawyer|Huckleberry|Finn", RegexOptions.None),
            "industry/leipzig-river-window-count" => (DotNetPerformanceRegexData.RiverCorpus, string.Empty, "Tom.{10,25}river|river.{10,25}Tom", RegexOptions.None),
            "industry/leipzig-symbol-count" => (DotNetPerformanceRegexData.RiverCorpus, string.Empty, @"\p{Sm}", RegexOptions.None),
            "industry/boostdocs-ftp-line-match" => ("100- this is a line of ftp response which contains a message string", string.Empty, @"^([0-9]+)(\-| |$)(.*)$", RegexOptions.None),
            "industry/boostdocs-credit-card-match" => ("1234-5678-1234-456", string.Empty, @"(\d{4}[- ]){3}\d{3,4}", RegexOptions.None),
            "industry/boostdocs-postcode-match" => ("SW1 1ZZ", string.Empty, @"^[a-zA-Z]{1,2}[0-9][0-9A-Za-z]{0,1} {0,1}[0-9][A-Za-z]{2}$", RegexOptions.None),
            "industry/boostdocs-date-match" => ("12/12/2001", string.Empty, @"^\d{1,2}/\d{1,2}/\d{4}$", RegexOptions.None),
            "industry/boostdocs-float-match" => ("-3.14159", string.Empty, @"^[-+]?\d*\.?\d*$", RegexOptions.None),
            _ => throw new ArgumentOutOfRangeException(nameof(caseId)),
        };
    }

    private static (string Input, string Replacement, string Pattern, RegexOptions Options)
        ResolveSharedSubject(string id)
    {
        var subject = BenchmarkSubjectDefinitions.Get(id);
        return (subject.Input, string.Empty, subject.Pattern, RegexOptions.None);
    }
}
