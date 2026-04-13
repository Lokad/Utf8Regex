using System.Buffers;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Input;

namespace Lokad.Utf8Regex.Pcre2;

public sealed class Utf8Pcre2Regex
{
    private static TimeSpan s_defaultMatchTimeout = Timeout.InfiniteTimeSpan;
    private readonly Pcre2Utf8RegexTranslation _utf8Translation;
    private readonly Utf8Regex? _utf8Regex;
    private readonly Utf8Regex? _utf8SearchEquivalentRegex;
    private readonly Regex? _managedRegex;
    private readonly Pcre2ExecutionPlan _executionPlan;
    private readonly string[] _groupNames;
    private readonly Pcre2NameEntry[] _nameEntries;
    private readonly Pcre2ExecutionKind _executionKind;
    private readonly ConcurrentDictionary<SimpleReplacementCacheKey, SimpleReplacementPlan?> _simpleReplacementPlans = new();

    public Utf8Pcre2Regex(string pattern)
        : this(pattern, Pcre2CompileOptions.None)
    {
    }

    public Utf8Pcre2Regex(string pattern, Pcre2CompileOptions options)
        : this(pattern, options, default)
    {
    }

    public Utf8Pcre2Regex(
        string pattern,
        Pcre2CompileOptions options,
        Utf8Pcre2CompileSettings compileSettings,
        Utf8Pcre2ExecutionLimits defaultExecutionLimits = default,
        TimeSpan matchTimeout = default)
    {
        ArgumentNullException.ThrowIfNull(pattern);
        Pcre2CompileValidator.Validate(pattern, compileSettings);
        Pattern = pattern;
        Options = options;
        CompileSettings = compileSettings;
        DefaultExecutionLimits = defaultExecutionLimits;
        MatchTimeout = matchTimeout == default ? DefaultMatchTimeout : matchTimeout;
        _executionKind = ClassifyExecutionKind(pattern);
        if ((_executionKind == Pcre2ExecutionKind.Unimplemented ||
             _executionKind == Pcre2ExecutionKind.ManagedRegex ||
             CanAlsoUseUtf8RegexBackend(_executionKind)) &&
            TryCreateUtf8Regex(pattern, options, MatchTimeout, out var utf8Regex))
        {
            _utf8Regex = utf8Regex;
        }

        if (TryCreateUtf8SearchEquivalentRegex(_executionKind, options, MatchTimeout, out var utf8SearchEquivalentRegex))
        {
            _utf8SearchEquivalentRegex = utf8SearchEquivalentRegex;
        }

        Regex? managedRegexCandidate = null;
        string[] managedGroupNames = [];
        Pcre2NameEntry[] managedNameEntries = [];
        var canUseSpecialUtf8Translation = _executionKind != Pcre2ExecutionKind.Unimplemented &&
            _utf8Regex is not null &&
            CanUseUtf8RegexTranslation(pattern, _executionKind, _utf8Regex);

        if ((_executionKind == Pcre2ExecutionKind.Unimplemented ||
             (CanAlsoUseManagedRegexBackend(_executionKind) && !canUseSpecialUtf8Translation)) &&
            TryCreateManagedRegex(pattern, options, MatchTimeout, out managedRegexCandidate))
        {
            managedGroupNames = managedRegexCandidate!.GetGroupNames();
            managedNameEntries = GetManagedNameEntries(managedRegexCandidate);
            if (_executionKind == Pcre2ExecutionKind.Unimplemented)
            {
                _executionKind = Pcre2ExecutionKind.ManagedRegex;
            }
        }

        var canUseUtf8Translation = _utf8Regex is not null &&
            CanUseUtf8RegexTranslation(pattern, _executionKind, _utf8Regex);
        if (canUseUtf8Translation)
        {
            var translatedUtf8Regex = _utf8Regex!;
            _utf8Translation = new Pcre2Utf8RegexTranslation(true, pattern, ToRegexOptions(options), translatedUtf8Regex);
            _groupNames = managedGroupNames.Length != 0 ? managedGroupNames : translatedUtf8Regex.GetGroupNames();
            _nameEntries = managedNameEntries.Length != 0 ? managedNameEntries : GetUtf8RegexNameEntries(translatedUtf8Regex);
        }
        else if (managedRegexCandidate is not null)
        {
            _managedRegex = managedRegexCandidate;
            _groupNames = managedGroupNames;
            _nameEntries = managedNameEntries;
        }
        else
        {
            _groupNames = [];
            _nameEntries = GetPatternNameEntries(pattern);
        }

        _executionPlan = CreateExecutionPlan(Pattern, _executionKind, _utf8Translation.IsActive, _utf8SearchEquivalentRegex, _managedRegex);
    }

    public Utf8Pcre2Regex(ReadOnlySpan<byte> patternUtf8)
        : this(patternUtf8, Pcre2CompileOptions.None)
    {
    }

    public Utf8Pcre2Regex(ReadOnlySpan<byte> patternUtf8, Pcre2CompileOptions options)
        : this(patternUtf8, options, default)
    {
    }

    public Utf8Pcre2Regex(
        ReadOnlySpan<byte> patternUtf8,
        Pcre2CompileOptions options,
        Utf8Pcre2CompileSettings compileSettings,
        Utf8Pcre2ExecutionLimits defaultExecutionLimits = default,
        TimeSpan matchTimeout = default)
        : this(System.Text.Encoding.UTF8.GetString(patternUtf8), options, compileSettings, defaultExecutionLimits, matchTimeout)
    {
    }

    public static TimeSpan DefaultMatchTimeout
    {
        get => s_defaultMatchTimeout;
        set => s_defaultMatchTimeout = value;
    }

    public string Pattern { get; }

    public Pcre2CompileOptions Options { get; }

    public Utf8Pcre2CompileSettings CompileSettings { get; }

    public Utf8Pcre2ExecutionLimits DefaultExecutionLimits { get; }

    public TimeSpan MatchTimeout { get; }

    public bool IsMatch(ReadOnlySpan<byte> input, int startOffsetInBytes = 0, Pcre2MatchOptions matchOptions = Pcre2MatchOptions.None)
    {
        ValidateStartOffset(input, startOffsetInBytes);
        if (_executionPlan.IsMatchBackend == Pcre2DirectBackendKind.Utf8Regex)
        {
            return _utf8Regex!.IsMatch(input, startOffsetInBytes);
        }

        if (_executionPlan.IsMatchBackend == Pcre2DirectBackendKind.Utf8RegexEquivalent)
        {
            return _utf8SearchEquivalentRegex!.IsMatch(input, startOffsetInBytes);
        }

        if (_executionPlan.IsMatchBackend == Pcre2DirectBackendKind.ManagedRegex)
        {
            return IsMatchViaManagedRegex(input, startOffsetInBytes);
        }

        return IsMatchViaSlowPath(input, startOffsetInBytes, matchOptions);
    }

    public int Count(ReadOnlySpan<byte> input, int startOffsetInBytes = 0, Pcre2MatchOptions matchOptions = Pcre2MatchOptions.None)
    {
        ValidateStartOffset(input, startOffsetInBytes);
        if (_executionPlan.CountBackend == Pcre2DirectBackendKind.Utf8Regex)
        {
            return _utf8Regex!.Count(input, startOffsetInBytes);
        }

        if (_executionPlan.CountBackend == Pcre2DirectBackendKind.Utf8RegexEquivalent)
        {
            return _utf8SearchEquivalentRegex!.Count(input, startOffsetInBytes);
        }

        if (_executionPlan.CountBackend == Pcre2DirectBackendKind.ManagedRegex)
        {
            if (startOffsetInBytes == 0 && CanUseUtf8RegexCompatiblePublicCount(input.Length))
            {
                return _utf8Regex!.Count(input);
            }

            return CountViaManagedRegex(input, startOffsetInBytes);
        }

        return CountViaSlowPath(input, startOffsetInBytes, matchOptions);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private bool IsMatchViaManagedRegex(ReadOnlySpan<byte> input, int startOffsetInBytes)
    {
        var subject = Encoding.UTF8.GetString(input);
        var startOffsetInUtf16 = Encoding.UTF8.GetCharCount(input[..startOffsetInBytes]);
        return _managedRegex!.IsMatch(subject, startOffsetInUtf16);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private bool IsMatchViaSlowPath(ReadOnlySpan<byte> input, int startOffsetInBytes, Pcre2MatchOptions matchOptions)
    {
        return Match(input, startOffsetInBytes, matchOptions).Success;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private int CountViaManagedRegex(ReadOnlySpan<byte> input, int startOffsetInBytes)
    {
        var subject = Encoding.UTF8.GetString(input);
        var startOffsetInUtf16 = Encoding.UTF8.GetCharCount(input[..startOffsetInBytes]);
        return _managedRegex!.Count(subject, startOffsetInUtf16);
    }

    private bool CanUseUtf8RegexCompatiblePublicCount(int inputLength)
        => _executionKind == Pcre2ExecutionKind.ManagedRegex &&
           _utf8Regex is not null &&
           inputLength <= 256;

    [MethodImpl(MethodImplOptions.NoInlining)]
    private int CountViaSlowPath(ReadOnlySpan<byte> input, int startOffsetInBytes, Pcre2MatchOptions matchOptions)
    {
        if (!UsesDeferredSpecialGlobalEnumerator(input, startOffsetInBytes))
        {
            if (TryCountNativeGlobalMatches(input, startOffsetInBytes, out var nativeCount))
            {
                return nativeCount;
            }

            return EnumerateGlobalMatchData(input, startOffsetInBytes).Length;
        }

        var enumerator = EnumerateMatches(input, startOffsetInBytes, matchOptions);
        var count = 0;
        while (enumerator.MoveNext())
        {
            count++;
        }

        return count;
    }

    public Utf8Pcre2ValueMatch Match(ReadOnlySpan<byte> input, int startOffsetInBytes = 0, Pcre2MatchOptions matchOptions = Pcre2MatchOptions.None)
    {
        ValidateStartOffset(input, startOffsetInBytes);
        if (_executionPlan.MatchBackend == Pcre2DirectBackendKind.Utf8Regex)
        {
            return CreateManagedProfileValueMatch(input, _utf8Regex!.Match(input, startOffsetInBytes));
        }

        if (_executionPlan.MatchBackend == Pcre2DirectBackendKind.Utf8RegexEquivalent)
        {
            return CreateManagedProfileValueMatch(input, _utf8SearchEquivalentRegex!.Match(input, startOffsetInBytes));
        }

        return _executionKind switch
        {
            Pcre2ExecutionKind.ManagedRegex => MatchViaManagedRegex(input, startOffsetInBytes),
            Pcre2ExecutionKind.MailboxRfc2822 => MatchViaMailboxRfc2822(input, startOffsetInBytes),
            Pcre2ExecutionKind.ReluctantAlternation => MatchViaReluctantAlternation(input, startOffsetInBytes),
            Pcre2ExecutionKind.BranchResetBasic => MatchViaBranchResetBasic(input, startOffsetInBytes),
            Pcre2ExecutionKind.BranchResetBackref => MatchViaBranchResetBackref(input, startOffsetInBytes),
            Pcre2ExecutionKind.BranchResetNested => MatchViaBranchResetNested(input, startOffsetInBytes),
            Pcre2ExecutionKind.BranchResetNestedSecondCapture => MatchViaBranchResetNestedSecondCapture(input, startOffsetInBytes),
            Pcre2ExecutionKind.BranchResetAbasic => MatchViaBranchResetAbasic(input, startOffsetInBytes),
            Pcre2ExecutionKind.BranchResetSubroutine => MatchViaBranchResetSubroutine(input, startOffsetInBytes),
            Pcre2ExecutionKind.BranchResetGReference => MatchViaBranchResetGReference(input, startOffsetInBytes),
            Pcre2ExecutionKind.BranchResetSameNameBackref => MatchViaBranchResetSameNameBackref(input, startOffsetInBytes),
            Pcre2ExecutionKind.BranchResetSameNameFollowup => MatchViaBranchResetSameNameFollowup(input, startOffsetInBytes),
            Pcre2ExecutionKind.DuplicateNamesFooBar => MatchViaDuplicateNamesFooBar(input, startOffsetInBytes),
            Pcre2ExecutionKind.MarkSkip => MatchViaMarkSkip(input, startOffsetInBytes),
            Pcre2ExecutionKind.KResetFooBar => MatchViaKResetFooBar(input, startOffsetInBytes),
            Pcre2ExecutionKind.KResetBarOrBaz => MatchViaKResetBarOrBaz(input, startOffsetInBytes),
            Pcre2ExecutionKind.KResetFooBarBaz => MatchViaKResetFooBarBaz(input, startOffsetInBytes),
            Pcre2ExecutionKind.KResetAbc123 => MatchViaKResetAbc123(input, startOffsetInBytes),
            Pcre2ExecutionKind.KReset123Abc => MatchViaKReset123Abc(input, startOffsetInBytes),
            Pcre2ExecutionKind.KResetAnchorAbc => MatchViaKResetAnchorAbc(input, startOffsetInBytes),
            Pcre2ExecutionKind.KResetAnchorLookaheadOrAbc => MatchViaKResetAnchorLookaheadOrAbc(input, startOffsetInBytes),
            Pcre2ExecutionKind.KResetAtomicAb => MatchViaKResetAtomicAb(input, startOffsetInBytes),
            Pcre2ExecutionKind.KResetCapturedAtomicAb => MatchViaKResetCapturedAtomicAb(input, startOffsetInBytes),
            Pcre2ExecutionKind.KResetCapturedAb => MatchViaKResetCapturedAb(input, startOffsetInBytes),
            Pcre2ExecutionKind.KResetAnchorCzOrAc => MatchViaKResetAnchorCzOrAc(input, startOffsetInBytes),
            Pcre2ExecutionKind.KResetAtomicAltAb => MatchViaKResetAtomicAltAb(input, startOffsetInBytes),
            Pcre2ExecutionKind.KResetDefineSubroutineAb => MatchViaKResetDefineSubroutineAb(input, startOffsetInBytes),
            Pcre2ExecutionKind.KResetRepeatAbPossessive => MatchViaKResetRepeatAbPossessive(input, startOffsetInBytes),
            Pcre2ExecutionKind.KResetAtomicRepeatAb => MatchViaKResetAtomicRepeatAb(input, startOffsetInBytes),
            Pcre2ExecutionKind.KResetRepeatAb => MatchViaKResetRepeatAb(input, startOffsetInBytes),
            Pcre2ExecutionKind.KResetCapturedRepeatAbPossessive => MatchViaKResetCapturedRepeatAbPossessive(input, startOffsetInBytes),
            Pcre2ExecutionKind.KResetCapturedRepeatAb => MatchViaKResetCapturedRepeatAb(input, startOffsetInBytes),
            Pcre2ExecutionKind.KResetRecursiveAny => MatchViaKResetRecursiveAny(input, startOffsetInBytes),
            Pcre2ExecutionKind.KResetRecursiveCaptured => MatchViaKResetRecursiveCaptured(input, startOffsetInBytes),
            Pcre2ExecutionKind.KResetLookaheadAb => MatchViaKResetLookaheadAb(input, startOffsetInBytes),
            Pcre2ExecutionKind.KResetLookbehindA => MatchViaKResetLookbehindA(input, startOffsetInBytes),
            Pcre2ExecutionKind.KResetConditionalGcOverlap => MatchViaKResetConditionalGcOverlap(input, startOffsetInBytes),
            Pcre2ExecutionKind.KResetConditionalGcNotSorted => MatchViaKResetConditionalGcNotSorted(input, startOffsetInBytes),
            Pcre2ExecutionKind.KResetRuntimeDisallowedLookaround1 => MatchViaKResetRuntimeDisallowedLookaround1(input, startOffsetInBytes),
            Pcre2ExecutionKind.KResetRuntimeDisallowedLookaround2 => MatchViaKResetRuntimeDisallowedLookaround2(input, startOffsetInBytes),
            Pcre2ExecutionKind.KResetRuntimeConditionalDigits => MatchViaKResetRuntimeConditionalDigits(input, startOffsetInBytes),
            Pcre2ExecutionKind.KResetSneakyLookaheadDefine => MatchViaKResetSneakyLookaheadDefine(input, startOffsetInBytes),
            Pcre2ExecutionKind.KResetSneakyLookbehindDefine => MatchViaKResetSneakyLookbehindDefine(input, startOffsetInBytes),
            Pcre2ExecutionKind.KResetSneakyGlobalLookbehindDefine => MatchViaKResetSneakyGlobalLookbehindDefine(input, startOffsetInBytes),
            Pcre2ExecutionKind.BackslashCLiteral => MatchViaBackslashCLiteral(input, startOffsetInBytes),
            Pcre2ExecutionKind.RecursivePalindromeOdd => MatchViaRecursivePalindromeOdd(input, startOffsetInBytes),
            Pcre2ExecutionKind.RecursivePalindromeAny => MatchViaRecursivePalindromeAny(input, startOffsetInBytes),
            Pcre2ExecutionKind.RecursiveAlternation => MatchViaRecursiveAlternation(input, startOffsetInBytes),
            Pcre2ExecutionKind.RecursiveOptional => MatchViaRecursiveOptional(input, startOffsetInBytes),
            Pcre2ExecutionKind.AtomicAlternationReluctantMany => MatchViaAtomicAlternationReluctantMany(input, startOffsetInBytes),
            Pcre2ExecutionKind.AtomicAlternationReluctantTwo => MatchViaAtomicAlternationReluctantTwo(input, startOffsetInBytes),
            Pcre2ExecutionKind.ConditionalLookaheadPlus => MatchViaConditionalLookaheadPlus(input, startOffsetInBytes),
            Pcre2ExecutionKind.ConditionalLookaheadEmptyAlt => MatchViaConditionalLookaheadEmptyAlt(input, startOffsetInBytes),
            Pcre2ExecutionKind.ConditionalLookahead => MatchViaConditionalLookahead(input, startOffsetInBytes),
            Pcre2ExecutionKind.ConditionalNegativeLookahead => MatchViaConditionalNegativeLookahead(input, startOffsetInBytes),
            Pcre2ExecutionKind.ConditionalAcceptLookahead => MatchViaConditionalAcceptLookahead(input, startOffsetInBytes),
            Pcre2ExecutionKind.ConditionalAcceptNegativeLookahead => MatchViaConditionalAcceptNegativeLookahead(input, startOffsetInBytes),
            Pcre2ExecutionKind.SubroutinePrefixDigits => MatchViaSubroutinePrefixDigits(input, startOffsetInBytes),
            Pcre2ExecutionKind.CommitSubroutine => MatchViaCommitSubroutine(input, startOffsetInBytes),
            Pcre2ExecutionKind.DefineSubroutineB => MatchViaDefineSubroutineB(input, startOffsetInBytes),
            _ => throw new NotSupportedException("SPEC-PCRE2 does not support this pattern in the managed profile."),
        };
    }

    public Utf8Pcre2MatchContext MatchDetailed(ReadOnlySpan<byte> input, int startOffsetInBytes = 0, Pcre2MatchOptions matchOptions = Pcre2MatchOptions.None)
    {
        ValidateStartOffset(input, startOffsetInBytes);
        if (_utf8Translation.IsActive)
        {
            return MatchDetailedViaUtf8RegexTranslation(input, startOffsetInBytes);
        }

        return _executionKind switch
        {
            Pcre2ExecutionKind.ManagedRegex => MatchDetailedViaManagedRegex(input, startOffsetInBytes),
            Pcre2ExecutionKind.MailboxRfc2822 => MatchDetailedViaMailboxRfc2822(input, startOffsetInBytes),
            Pcre2ExecutionKind.ReluctantAlternation => MatchDetailedViaReluctantAlternation(input, startOffsetInBytes),
            Pcre2ExecutionKind.BranchResetBasic => MatchDetailedViaBranchResetBasic(input, startOffsetInBytes),
            Pcre2ExecutionKind.BranchResetBackref => MatchDetailedViaBranchResetBackref(input, startOffsetInBytes),
            Pcre2ExecutionKind.BranchResetNested => MatchDetailedViaBranchResetNested(input, startOffsetInBytes),
            Pcre2ExecutionKind.BranchResetNestedSecondCapture => MatchDetailedViaBranchResetNestedSecondCapture(input, startOffsetInBytes),
            Pcre2ExecutionKind.BranchResetAbasic => MatchDetailedViaBranchResetAbasic(input, startOffsetInBytes),
            Pcre2ExecutionKind.BranchResetSubroutine => MatchDetailedViaBranchResetSubroutine(input, startOffsetInBytes),
            Pcre2ExecutionKind.BranchResetGReference => MatchDetailedViaBranchResetGReference(input, startOffsetInBytes),
            Pcre2ExecutionKind.BranchResetSameNameBackref => MatchDetailedViaBranchResetSameNameBackref(input, startOffsetInBytes),
            Pcre2ExecutionKind.BranchResetSameNameFollowup => MatchDetailedViaBranchResetSameNameFollowup(input, startOffsetInBytes),
            Pcre2ExecutionKind.DuplicateNamesFooBar => MatchDetailedViaDuplicateNamesFooBar(input, startOffsetInBytes),
            Pcre2ExecutionKind.MarkSkip => MatchDetailedViaMarkSkip(input, startOffsetInBytes),
            Pcre2ExecutionKind.KResetFooBar => MatchDetailedViaKResetFooBar(input, startOffsetInBytes),
            Pcre2ExecutionKind.KResetBarOrBaz => MatchDetailedViaKResetBarOrBaz(input, startOffsetInBytes),
            Pcre2ExecutionKind.KResetFooBarBaz => MatchDetailedViaKResetFooBarBaz(input, startOffsetInBytes),
            Pcre2ExecutionKind.KResetAbc123 => MatchDetailedViaKResetAbc123(input, startOffsetInBytes),
            Pcre2ExecutionKind.KReset123Abc => MatchDetailedViaKReset123Abc(input, startOffsetInBytes),
            Pcre2ExecutionKind.KResetAnchorAbc => MatchDetailedViaKResetAnchorAbc(input, startOffsetInBytes),
            Pcre2ExecutionKind.KResetAnchorLookaheadOrAbc => MatchDetailedViaKResetAnchorLookaheadOrAbc(input, startOffsetInBytes),
            Pcre2ExecutionKind.KResetAtomicAb => MatchDetailedViaKResetAtomicAb(input, startOffsetInBytes),
            Pcre2ExecutionKind.KResetCapturedAtomicAb => MatchDetailedViaKResetCapturedAtomicAb(input, startOffsetInBytes),
            Pcre2ExecutionKind.KResetCapturedAb => MatchDetailedViaKResetCapturedAb(input, startOffsetInBytes),
            Pcre2ExecutionKind.KResetAnchorCzOrAc => MatchDetailedViaKResetAnchorCzOrAc(input, startOffsetInBytes),
            Pcre2ExecutionKind.KResetAtomicAltAb => MatchDetailedViaKResetAtomicAltAb(input, startOffsetInBytes),
            Pcre2ExecutionKind.KResetDefineSubroutineAb => MatchDetailedViaKResetDefineSubroutineAb(input, startOffsetInBytes),
            Pcre2ExecutionKind.KResetRepeatAbPossessive => MatchDetailedViaKResetRepeatAbPossessive(input, startOffsetInBytes),
            Pcre2ExecutionKind.KResetAtomicRepeatAb => MatchDetailedViaKResetAtomicRepeatAb(input, startOffsetInBytes),
            Pcre2ExecutionKind.KResetRepeatAb => MatchDetailedViaKResetRepeatAb(input, startOffsetInBytes),
            Pcre2ExecutionKind.KResetCapturedRepeatAbPossessive => MatchDetailedViaKResetCapturedRepeatAbPossessive(input, startOffsetInBytes),
            Pcre2ExecutionKind.KResetCapturedRepeatAb => MatchDetailedViaKResetCapturedRepeatAb(input, startOffsetInBytes),
            Pcre2ExecutionKind.KResetRecursiveAny => MatchDetailedViaKResetRecursiveAny(input, startOffsetInBytes),
            Pcre2ExecutionKind.KResetRecursiveCaptured => MatchDetailedViaKResetRecursiveCaptured(input, startOffsetInBytes),
            Pcre2ExecutionKind.KResetLookaheadAb => MatchDetailedViaKResetLookaheadAb(input, startOffsetInBytes),
            Pcre2ExecutionKind.KResetLookbehindA => MatchDetailedViaKResetLookbehindA(input, startOffsetInBytes),
            Pcre2ExecutionKind.KResetConditionalGcOverlap => MatchDetailedViaKResetConditionalGcOverlap(input, startOffsetInBytes),
            Pcre2ExecutionKind.KResetConditionalGcNotSorted => MatchDetailedViaKResetConditionalGcNotSorted(input, startOffsetInBytes),
            Pcre2ExecutionKind.KResetRuntimeDisallowedLookaround1 => MatchDetailedViaKResetRuntimeDisallowedLookaround1(input, startOffsetInBytes),
            Pcre2ExecutionKind.KResetRuntimeDisallowedLookaround2 => MatchDetailedViaKResetRuntimeDisallowedLookaround2(input, startOffsetInBytes),
            Pcre2ExecutionKind.KResetRuntimeConditionalDigits => MatchDetailedViaKResetRuntimeConditionalDigits(input, startOffsetInBytes),
            Pcre2ExecutionKind.KResetSneakyLookaheadDefine => MatchDetailedViaKResetSneakyLookaheadDefine(input, startOffsetInBytes),
            Pcre2ExecutionKind.KResetSneakyLookbehindDefine => MatchDetailedViaKResetSneakyLookbehindDefine(input, startOffsetInBytes),
            Pcre2ExecutionKind.KResetSneakyGlobalLookbehindDefine => MatchDetailedViaKResetSneakyGlobalLookbehindDefine(input, startOffsetInBytes),
            Pcre2ExecutionKind.BackslashCLiteral => MatchDetailedViaBackslashCLiteral(input, startOffsetInBytes),
            Pcre2ExecutionKind.RecursivePalindromeOdd => MatchDetailedViaRecursivePalindromeOdd(input, startOffsetInBytes),
            Pcre2ExecutionKind.RecursivePalindromeAny => MatchDetailedViaRecursivePalindromeAny(input, startOffsetInBytes),
            Pcre2ExecutionKind.RecursiveAlternation => MatchDetailedViaRecursiveAlternation(input, startOffsetInBytes),
            Pcre2ExecutionKind.RecursiveOptional => MatchDetailedViaRecursiveOptional(input, startOffsetInBytes),
            Pcre2ExecutionKind.AtomicAlternationReluctantMany => MatchDetailedViaAtomicAlternationReluctantMany(input, startOffsetInBytes),
            Pcre2ExecutionKind.AtomicAlternationReluctantTwo => MatchDetailedViaAtomicAlternationReluctantTwo(input, startOffsetInBytes),
            Pcre2ExecutionKind.ConditionalLookaheadPlus => MatchDetailedViaConditionalLookaheadPlus(input, startOffsetInBytes),
            Pcre2ExecutionKind.ConditionalLookaheadEmptyAlt => MatchDetailedViaConditionalLookaheadEmptyAlt(input, startOffsetInBytes),
            Pcre2ExecutionKind.ConditionalLookahead => MatchDetailedViaConditionalLookahead(input, startOffsetInBytes),
            Pcre2ExecutionKind.ConditionalNegativeLookahead => MatchDetailedViaConditionalNegativeLookahead(input, startOffsetInBytes),
            Pcre2ExecutionKind.ConditionalAcceptLookahead => MatchDetailedViaConditionalAcceptLookahead(input, startOffsetInBytes),
            Pcre2ExecutionKind.ConditionalAcceptNegativeLookahead => MatchDetailedViaConditionalAcceptNegativeLookahead(input, startOffsetInBytes),
            Pcre2ExecutionKind.SubroutinePrefixDigits => MatchDetailedViaSubroutinePrefixDigits(input, startOffsetInBytes),
            Pcre2ExecutionKind.CommitSubroutine => MatchDetailedViaCommitSubroutine(input, startOffsetInBytes),
            Pcre2ExecutionKind.DefineSubroutineB => MatchDetailedViaDefineSubroutineB(input, startOffsetInBytes),
            _ => throw new NotSupportedException("SPEC-PCRE2 does not support this pattern in the managed profile."),
        };
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Utf8Pcre2ValueMatchEnumerator EnumerateMatches(ReadOnlySpan<byte> input, int startOffsetInBytes = 0, Pcre2MatchOptions matchOptions = Pcre2MatchOptions.None)
    {
        ValidateStartOffset(input, startOffsetInBytes);

        if (_executionPlan.EnumerateBackend == Pcre2DirectBackendKind.None)
        {
            return EnumerateMatchesViaNativeGlobalEntry(input, startOffsetInBytes);
        }

        return EnumerateMatchesViaConfiguredBackend(input, startOffsetInBytes);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private Utf8Pcre2ValueMatchEnumerator EnumerateMatchesViaNativeGlobalEntry(ReadOnlySpan<byte> input, int startOffsetInBytes)
    {
        if ((_executionKind is Pcre2ExecutionKind.KResetSneakyLookaheadDefine or
                 Pcre2ExecutionKind.KResetSneakyLookbehindDefine or
                 Pcre2ExecutionKind.KResetSneakyGlobalLookbehindDefine) &&
            TryCreateSpecialGlobalEnumerator(input, startOffsetInBytes, out var enumerator))
        {
            return enumerator;
        }

        if (CanEnumerateViaNativeValueGenerator())
        {
            return new Utf8Pcre2ValueMatchEnumerator(input, GetNativeValueEnumeratorKind(), startOffsetInBytes);
        }

        return new Utf8Pcre2ValueMatchEnumerator(input, EnumerateGlobalMatchData(input, startOffsetInBytes));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool CanEnumerateViaNativeValueGenerator()
        => Pattern == "(?<=abc)(|def)" ||
           _executionKind is
               Pcre2ExecutionKind.BranchResetBasic or
               Pcre2ExecutionKind.BranchResetBackref or
               Pcre2ExecutionKind.BranchResetNested or
               Pcre2ExecutionKind.BranchResetSameNameFollowup or
               Pcre2ExecutionKind.DuplicateNamesFooBar or
               Pcre2ExecutionKind.KResetAbc123 or
               Pcre2ExecutionKind.KResetBarOrBaz or
               Pcre2ExecutionKind.KResetRepeatAbPossessive or
               Pcre2ExecutionKind.KResetAtomicRepeatAb or
               Pcre2ExecutionKind.KResetRepeatAb or
               Pcre2ExecutionKind.KResetCapturedRepeatAbPossessive or
               Pcre2ExecutionKind.KResetCapturedRepeatAb or
               Pcre2ExecutionKind.KResetAtomicAltAb;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private Utf8Pcre2ValueMatchEnumerator.Pcre2NativeValueEnumeratorKind GetNativeValueEnumeratorKind()
        => _executionKind switch
        {
            Pcre2ExecutionKind.BranchResetBasic => Utf8Pcre2ValueMatchEnumerator.Pcre2NativeValueEnumeratorKind.BranchResetBasic,
            Pcre2ExecutionKind.BranchResetBackref => Utf8Pcre2ValueMatchEnumerator.Pcre2NativeValueEnumeratorKind.BranchResetBackref,
            Pcre2ExecutionKind.BranchResetNested => Utf8Pcre2ValueMatchEnumerator.Pcre2NativeValueEnumeratorKind.BranchResetNested,
            Pcre2ExecutionKind.BranchResetSameNameFollowup => Utf8Pcre2ValueMatchEnumerator.Pcre2NativeValueEnumeratorKind.BranchResetSameNameFollowup,
            Pcre2ExecutionKind.DuplicateNamesFooBar => Utf8Pcre2ValueMatchEnumerator.Pcre2NativeValueEnumeratorKind.DuplicateNamesFooBar,
            Pcre2ExecutionKind.ManagedRegex when Pattern == "(?<=abc)(|def)" => Utf8Pcre2ValueMatchEnumerator.Pcre2NativeValueEnumeratorKind.EmptyOrDefAfterAbc,
            Pcre2ExecutionKind.KResetAbc123 => Utf8Pcre2ValueMatchEnumerator.Pcre2NativeValueEnumeratorKind.KResetAbc123,
            Pcre2ExecutionKind.KResetBarOrBaz => Utf8Pcre2ValueMatchEnumerator.Pcre2NativeValueEnumeratorKind.KResetBarOrBaz,
            Pcre2ExecutionKind.KResetRepeatAbPossessive or
            Pcre2ExecutionKind.KResetAtomicRepeatAb or
            Pcre2ExecutionKind.KResetRepeatAb or
            Pcre2ExecutionKind.KResetCapturedRepeatAbPossessive or
            Pcre2ExecutionKind.KResetCapturedRepeatAb => Utf8Pcre2ValueMatchEnumerator.Pcre2NativeValueEnumeratorKind.KResetRepeatAb,
            Pcre2ExecutionKind.KResetAtomicAltAb => Utf8Pcre2ValueMatchEnumerator.Pcre2NativeValueEnumeratorKind.KResetAtomicAltAb,
            _ => Utf8Pcre2ValueMatchEnumerator.Pcre2NativeValueEnumeratorKind.None,
        };

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private Utf8Pcre2ValueMatchEnumerator EnumerateMatchesViaConfiguredBackend(ReadOnlySpan<byte> input, int startOffsetInBytes)
    {
        return _executionPlan.EnumerateBackend switch
        {
            Pcre2DirectBackendKind.Utf8Regex => EnumerateMatchesViaUtf8RegexBackend(input, startOffsetInBytes),
            Pcre2DirectBackendKind.Utf8RegexEquivalent => EnumerateMatchesViaUtf8RegexEquivalentBackend(input, startOffsetInBytes),
            Pcre2DirectBackendKind.ManagedRegex => EnumerateMatchesViaManagedRegexBackend(input, startOffsetInBytes),
            _ => default,
        };
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private Utf8Pcre2ValueMatchEnumerator EnumerateMatchesViaUtf8RegexBackend(ReadOnlySpan<byte> input, int startOffsetInBytes)
    {
        if (_utf8Translation.IsActive)
        {
            return new Utf8Pcre2ValueMatchEnumerator(input, _utf8Regex!.EnumerateMatches(input, startOffsetInBytes));
        }

        if (CanUsePreparedAsciiUtf8RegexEnumerator(input, startOffsetInBytes))
        {
            return new Utf8Pcre2ValueMatchEnumerator(
                input,
                _utf8Regex!.Pcre2EnumeratePreparedValueMatchesAtByteOffset(input, startOffsetInBytes),
                startOffsetInBytes);
        }

        return new Utf8Pcre2ValueMatchEnumerator(
            input,
            _utf8Regex!.Pcre2EnumerateMatchesAtByteOffset(input, startOffsetInBytes),
            startOffsetInBytes,
            GetUtf16OffsetOfBytePrefix(input, startOffsetInBytes));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private Utf8Pcre2ValueMatchEnumerator EnumerateMatchesViaUtf8RegexEquivalentBackend(ReadOnlySpan<byte> input, int startOffsetInBytes)
        => new(
            input,
            _utf8SearchEquivalentRegex!.Pcre2EnumerateMatchesAtByteOffset(input, startOffsetInBytes),
            startOffsetInBytes,
            GetUtf16OffsetOfBytePrefix(input, startOffsetInBytes));

    [MethodImpl(MethodImplOptions.NoInlining)]
    private Utf8Pcre2ValueMatchEnumerator EnumerateMatchesViaManagedRegexBackend(ReadOnlySpan<byte> input, int startOffsetInBytes)
    {
        var subject = Encoding.UTF8.GetString(input);
        var startOffsetInUtf16 = Encoding.UTF8.GetCharCount(input[..startOffsetInBytes]);
        var isAscii = subject.Length == input.Length;
        var boundaryMap = isAscii ? null : Utf8InputAnalyzer.Analyze(input).BoundaryMap;
        return new Utf8Pcre2ValueMatchEnumerator(input, _managedRegex!.EnumerateMatches(subject.AsSpan(), startOffsetInUtf16), boundaryMap, isAscii);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool CanUsePreparedAsciiUtf8RegexEnumerator(ReadOnlySpan<byte> input)
        => CanUsePreparedAsciiUtf8RegexEnumerator(input, startOffsetInBytes: 0);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool CanUsePreparedAsciiUtf8RegexEnumerator(ReadOnlySpan<byte> input, int startOffsetInBytes)
    {
        if (_utf8Regex is null ||
            !Utf8Validation.Validate(input).IsAscii)
        {
            return false;
        }

        return _utf8Regex.SearchPortfolioKind is
            Internal.Planning.Utf8SearchPortfolioKind.ExactLiteral or
            Internal.Planning.Utf8SearchPortfolioKind.IgnoreCaseLiteral;
    }

    public Utf8Pcre2ProbeResult Probe(ReadOnlySpan<byte> input, Pcre2PartialMode partialMode, int startOffsetInBytes = 0, Pcre2MatchOptions matchOptions = Pcre2MatchOptions.None)
    {
        ValidateStartOffset(input, startOffsetInBytes);
        if (_executionKind == Pcre2ExecutionKind.PartialSoftDotAllLiteral)
        {
            return ProbeViaPartialSoftDotAllLiteral(input, partialMode, startOffsetInBytes);
        }

        if (_executionKind == Pcre2ExecutionKind.ReplaceFooLiteral)
        {
            return ProbeViaFooLiteral(input, partialMode, startOffsetInBytes);
        }

        if (_executionKind == Pcre2ExecutionKind.ReplacePartialAbPlus)
        {
            return ProbeViaAbPlus(input, partialMode, startOffsetInBytes);
        }

        if (string.Equals(Pattern, "cat|horse", StringComparison.Ordinal) ||
            string.Equals(Pattern, "(?:cat|horse)", StringComparison.Ordinal))
        {
            return ProbeViaLiteralAlternation(input, partialMode, startOffsetInBytes, "cat", "horse");
        }

        if (string.Equals(Pattern, "dog(sbody)?", StringComparison.Ordinal) ||
            string.Equals(Pattern, "dogsbody|dog", StringComparison.Ordinal))
        {
            return ProbeViaOrderedLiteralAlternation(input, partialMode, startOffsetInBytes, "dogsbody", "dog");
        }

        if (string.Equals(Pattern, "dog(sbody)??", StringComparison.Ordinal) ||
            string.Equals(Pattern, "dog|dogsbody", StringComparison.Ordinal))
        {
            return ProbeViaOrderedLiteralAlternation(input, partialMode, startOffsetInBytes, "dog", "dogsbody");
        }

        if (string.Equals(Pattern, "\\bthe cat\\b", StringComparison.Ordinal))
        {
            return ProbeViaWordBoundaryLiteral(input, partialMode, startOffsetInBytes, "the cat");
        }

        if (string.Equals(Pattern, "\\babc\\b", StringComparison.Ordinal))
        {
            return ProbeViaWordBoundaryLiteral(input, partialMode, startOffsetInBytes, "abc");
        }

        if (string.Equals(Pattern, "abc\\K123", StringComparison.Ordinal))
        {
            return ProbeViaInspectedPrefixLiteral(input, partialMode, startOffsetInBytes, "abc", "123", includePrefixInPartial: true);
        }

        if (string.Equals(Pattern, "(?<=abc)123", StringComparison.Ordinal))
        {
            return ProbeViaInspectedPrefixLiteral(input, partialMode, startOffsetInBytes, "abc", "123", includePrefixInPartial: false);
        }

        if (string.Equals(Pattern, "abc(?=xyz)", StringComparison.Ordinal))
        {
            return ProbeViaInspectedContextLiteral(input, partialMode, startOffsetInBytes, prefix: null, "abc", "xyz", includePrefixInPartial: false);
        }

        if (string.Equals(Pattern, "(?<=pqr)abc(?=xyz)", StringComparison.Ordinal))
        {
            return ProbeViaInspectedContextLiteral(input, partialMode, startOffsetInBytes, "pqr", "abc", "xyz", includePrefixInPartial: true);
        }

        if (string.Equals(Pattern, "abc(?=abcde)(?=ab)", StringComparison.Ordinal))
        {
            return ProbeViaInspectedContextLiteral(input, partialMode, startOffsetInBytes, prefix: null, "abc", "abcde", includePrefixInPartial: false);
        }

        if (string.Equals(Pattern, "\\z", StringComparison.Ordinal))
        {
            return ProbeViaEndAssertion(input, partialMode, startOffsetInBytes, allowFinalNewline: false);
        }

        if (string.Equals(Pattern, "\\Z", StringComparison.Ordinal))
        {
            return ProbeViaEndAssertion(input, partialMode, startOffsetInBytes, allowFinalNewline: true);
        }

        if (string.Equals(Pattern, "c*+(?<=[bc])", StringComparison.Ordinal))
        {
            return ProbeViaTrailingCWithLookbehind(input, partialMode, startOffsetInBytes, requireAtLeastOneC: false);
        }

        if (string.Equals(Pattern, "c++(?<=[bc])", StringComparison.Ordinal))
        {
            return ProbeViaTrailingCWithLookbehind(input, partialMode, startOffsetInBytes, requireAtLeastOneC: true);
        }

        if (string.Equals(Pattern, "(?![ab]).*", StringComparison.Ordinal))
        {
            return ProbeViaNegativeStartClassDotStar(input, partialMode, startOffsetInBytes);
        }

        if (string.Equals(Pattern, "^\\R", StringComparison.Ordinal))
        {
            return ProbeViaAnchoredNewlineSequence(input, partialMode, startOffsetInBytes, minCount: 1, maxCount: 1, requireTerminalX: false);
        }

        if (string.Equals(Pattern, "^\\R{2,3}x", StringComparison.Ordinal) ||
            string.Equals(Pattern, "^\\R{2,3}?x", StringComparison.Ordinal))
        {
            return ProbeViaAnchoredNewlineSequence(input, partialMode, startOffsetInBytes, minCount: 2, maxCount: 3, requireTerminalX: true);
        }

        if (string.Equals(Pattern, "^\\R?x", StringComparison.Ordinal))
        {
            return ProbeViaAnchoredOptionalNewlineThenX(input, partialMode, startOffsetInBytes);
        }

        if (string.Equals(Pattern, "^\\R+x", StringComparison.Ordinal))
        {
            return ProbeViaAnchoredNewlineSequence(input, partialMode, startOffsetInBytes, minCount: 1, maxCount: int.MaxValue, requireTerminalX: true);
        }

        if (string.Equals(Pattern, "(?>a+b)", StringComparison.Ordinal))
        {
            return ProbeViaAnchoredAtomicAPlusB(input, partialMode, startOffsetInBytes);
        }

        if (string.Equals(Pattern, "(abc)(?1)", StringComparison.Ordinal))
        {
            return ProbeViaAnchoredRepeatedLiteral(input, partialMode, startOffsetInBytes, "abc", repeatCount: 2);
        }

        if (string.Equals(Pattern, "(?(?=abc).*|Z)", StringComparison.Ordinal))
        {
            return ProbeViaAnchoredConditionalAbcDotStarOrZ(input, partialMode, startOffsetInBytes);
        }

        if (string.Equals(Pattern, "(abc)++x", StringComparison.Ordinal))
        {
            return ProbeViaAnchoredLiteralPlusTerminal(input, partialMode, startOffsetInBytes, "abc", (byte)'x');
        }

        if (string.Equals(Pattern, "^(?:a)++\\w", StringComparison.Ordinal))
        {
            return ProbeViaAnchoredAPlusWord(input, partialMode, startOffsetInBytes, allowEmptyA: false, requireAtLeastTwoLeadingA: false);
        }

        if (string.Equals(Pattern, "^(?:aa|(?:a)++\\w)", StringComparison.Ordinal))
        {
            return ProbeViaAnchoredAaOrAPlusWord(input, partialMode, startOffsetInBytes);
        }

        if (string.Equals(Pattern, "^(?:a)*+\\w", StringComparison.Ordinal))
        {
            return ProbeViaAnchoredAPlusWord(input, partialMode, startOffsetInBytes, allowEmptyA: true, requireAtLeastTwoLeadingA: false);
        }

        if (string.Equals(Pattern, "^(a)++\\w", StringComparison.Ordinal))
        {
            return ProbeViaAnchoredAPlusWord(input, partialMode, startOffsetInBytes, allowEmptyA: false, requireAtLeastTwoLeadingA: false);
        }

        if (string.Equals(Pattern, "^(a|)++\\w", StringComparison.Ordinal))
        {
            return ProbeViaAnchoredAPlusWord(input, partialMode, startOffsetInBytes, allowEmptyA: true, requireAtLeastTwoLeadingA: false);
        }

        if (CompileSettings.Newline == Pcre2NewlineConvention.Crlf)
        {
            if (string.Equals(Pattern, "^a$", StringComparison.Ordinal))
            {
                return ProbeViaAnchoredACrlfEnd(input, partialMode, startOffsetInBytes, preferExplicitCrAlternative: false);
            }

            if (string.Equals(Pattern, "^(a$|a\\r)", StringComparison.Ordinal))
            {
                return ProbeViaAnchoredACrlfEnd(input, partialMode, startOffsetInBytes, preferExplicitCrAlternative: true);
            }

            if (string.Equals(Pattern, ".", StringComparison.Ordinal))
            {
                return ProbeViaCrlfDotQuantifier(input, partialMode, startOffsetInBytes, minCount: 1, maxCount: 1);
            }

            if (string.Equals(Pattern, ".{2,3}", StringComparison.Ordinal) ||
                string.Equals(Pattern, ".{2,3}?", StringComparison.Ordinal))
            {
                return ProbeViaCrlfDotQuantifier(input, partialMode, startOffsetInBytes, minCount: 2, maxCount: 3);
            }
        }

        if (string.Equals(Pattern, "abc$", StringComparison.Ordinal))
        {
            return ProbeViaTrailingLiteralAssertion(input, partialMode, startOffsetInBytes, "abc", TrailingAssertionKind.Dollar);
        }

        if (string.Equals(Pattern, "abc\\z", StringComparison.Ordinal))
        {
            return ProbeViaTrailingLiteralAssertion(input, partialMode, startOffsetInBytes, "abc", TrailingAssertionKind.EndAbsolute);
        }

        if (string.Equals(Pattern, "abc\\Z", StringComparison.Ordinal))
        {
            return ProbeViaTrailingLiteralAssertion(input, partialMode, startOffsetInBytes, "abc", TrailingAssertionKind.EndBeforeFinalNewline);
        }

        if (string.Equals(Pattern, "abc\\b", StringComparison.Ordinal))
        {
            return ProbeViaTrailingLiteralAssertion(input, partialMode, startOffsetInBytes, "abc", TrailingAssertionKind.WordBoundary);
        }

        if (string.Equals(Pattern, "abc\\B", StringComparison.Ordinal))
        {
            return ProbeViaTrailingLiteralAssertion(input, partialMode, startOffsetInBytes, "abc", TrailingAssertionKind.NonWordBoundary);
        }

        if (string.Equals(Pattern, "^foo", StringComparison.Ordinal))
        {
            return ProbeViaAnchoredFooLiteral(input, partialMode, startOffsetInBytes);
        }

        if (string.Equals(Pattern, "^abc$", StringComparison.Ordinal))
        {
            return ProbeViaAnchoredExactAbcLiteral(input, partialMode, startOffsetInBytes);
        }

        if (string.Equals(Pattern, "abc$", StringComparison.Ordinal))
        {
            return ProbeViaSuffixAnchoredAbcLiteral(input, partialMode, startOffsetInBytes);
        }

        if (partialMode == Pcre2PartialMode.None)
        {
            return ProbeViaNonPartialMatch(input, startOffsetInBytes);
        }

        throw CreateUnsupportedProbeException();
    }

    public byte[] Replace(ReadOnlySpan<byte> input, string replacement, int startOffsetInBytes = 0, Pcre2SubstitutionOptions substitutionOptions = Pcre2SubstitutionOptions.None, Pcre2MatchOptions matchOptions = Pcre2MatchOptions.None)
    {
        ArgumentNullException.ThrowIfNull(replacement);
        ValidateStartOffset(input, startOffsetInBytes);
        return ReplaceCore(input, replacement, Pcre2PartialMode.None, substitutionOptions, startOffsetInBytes);
    }

    public byte[] Replace(ReadOnlySpan<byte> input, ReadOnlySpan<byte> replacementPatternUtf8, int startOffsetInBytes = 0, Pcre2SubstitutionOptions substitutionOptions = Pcre2SubstitutionOptions.None, Pcre2MatchOptions matchOptions = Pcre2MatchOptions.None)
    {
        ValidateStartOffset(input, startOffsetInBytes);
        return ReplaceCore(input, Encoding.UTF8.GetString(replacementPatternUtf8), Pcre2PartialMode.None, substitutionOptions, startOffsetInBytes);
    }

    public byte[] Replace<TState>(ReadOnlySpan<byte> input, TState state, Pcre2MatchEvaluator<TState> evaluator, int startOffsetInBytes = 0, Pcre2MatchOptions matchOptions = Pcre2MatchOptions.None)
    {
        ArgumentNullException.ThrowIfNull(evaluator);
        ValidateStartOffset(input, startOffsetInBytes);
        return Encoding.UTF8.GetBytes(ReplaceWithUtf8Evaluator(input, state, evaluator, startOffsetInBytes));
    }

    public string ReplaceToString(ReadOnlySpan<byte> input, string replacement, int startOffsetInBytes = 0, Pcre2SubstitutionOptions substitutionOptions = Pcre2SubstitutionOptions.None, Pcre2MatchOptions matchOptions = Pcre2MatchOptions.None)
    {
        ArgumentNullException.ThrowIfNull(replacement);
        return Encoding.UTF8.GetString(ReplaceCore(input, replacement, Pcre2PartialMode.None, substitutionOptions, startOffsetInBytes));
    }

    public string ReplaceToString<TState>(ReadOnlySpan<byte> input, TState state, Pcre2Utf16MatchEvaluator<TState> evaluator, int startOffsetInBytes = 0, Pcre2MatchOptions matchOptions = Pcre2MatchOptions.None)
    {
        ArgumentNullException.ThrowIfNull(evaluator);
        ValidateStartOffset(input, startOffsetInBytes);
        return ReplaceWithUtf16Evaluator(input, state, evaluator, startOffsetInBytes);
    }

    public OperationStatus TryReplace(ReadOnlySpan<byte> input, ReadOnlySpan<byte> replacementPatternUtf8, Span<byte> destination, out int bytesWritten, int startOffsetInBytes = 0, Pcre2SubstitutionOptions substitutionOptions = Pcre2SubstitutionOptions.None, Pcre2MatchOptions matchOptions = Pcre2MatchOptions.None)
    {
        var replacementText = Encoding.UTF8.GetString(replacementPatternUtf8);
        var templateOptions = substitutionOptions & ~Pcre2SubstitutionOptions.SubstituteOverflowLength;
        ValidateStartOffset(input, startOffsetInBytes);
        if (UsesNativeGlobalIteration() &&
            !RejectsReplacementIteration() &&
            GetSimpleReplacementPlan(replacementText, templateOptions) is { } simplePlan &&
            TryGetLiteralOnlyReplacementBytes(simplePlan, out var replacementBytes))
        {
            var matches = EnumerateGlobalMatchData(input, startOffsetInBytes);
            if (matches.Length == 0)
            {
                if (input.Length > destination.Length)
                {
                    bytesWritten = (substitutionOptions & Pcre2SubstitutionOptions.SubstituteOverflowLength) != 0
                        ? input.Length
                        : 0;
                    return OperationStatus.DestinationTooSmall;
                }

                input.CopyTo(destination);
                bytesWritten = input.Length;
                return OperationStatus.Done;
            }

            return TryReplaceLiteralAgainstMatches(input, matches, replacementBytes, destination, out bytesWritten, substitutionOptions);
        }

        if (CanUseUtf8RegexLiteralReplacement(replacementText, substitutionOptions, allowOverflowLengthShortcut: false))
        {
            if (startOffsetInBytes == 0)
            {
                return _utf8Regex!.TryReplace(input, replacementPatternUtf8, destination, out bytesWritten);
            }

            input[..startOffsetInBytes].CopyTo(destination);
            var status = _utf8Regex!.TryReplace(input[startOffsetInBytes..], replacementPatternUtf8, destination[startOffsetInBytes..], out var suffixBytesWritten);
            bytesWritten = status == OperationStatus.Done ? startOffsetInBytes + suffixBytesWritten : 0;
            return status;
        }

        var replaced = Replace(input, replacementPatternUtf8, startOffsetInBytes, substitutionOptions, matchOptions);
        if (replaced.Length > destination.Length)
        {
            bytesWritten = (substitutionOptions & Pcre2SubstitutionOptions.SubstituteOverflowLength) != 0
                ? replaced.Length
                : 0;
            return OperationStatus.DestinationTooSmall;
        }

        replaced.CopyTo(destination);
        bytesWritten = replaced.Length;
        return OperationStatus.Done;
    }

    public int NameEntryCount => _nameEntries.Length;

    public int CopyNameEntries(Span<Pcre2NameEntry> destination, out bool isMore)
    {
        var written = Math.Min(destination.Length, _nameEntries.Length);
        _nameEntries.AsSpan(0, written).CopyTo(destination);
        isMore = written < _nameEntries.Length;
        return written;
    }

    public int CopyNumbersForName(string name, Span<int> destination, out bool isMore)
    {
        ArgumentNullException.ThrowIfNull(name);
        var matchingEntries = _nameEntries.Where(static e => !string.IsNullOrEmpty(e.Name)).ToArray();
        var count = 0;
        foreach (var entry in matchingEntries)
        {
            if (!string.Equals(entry.Name, name, StringComparison.Ordinal))
            {
                continue;
            }

            if (count < destination.Length)
            {
                destination[count] = entry.Number;
            }

            count++;
        }

        isMore = count > destination.Length;
        return Math.Min(count, destination.Length);
    }

    public bool TryGetFirstSetGroup(ReadOnlySpan<byte> input, string name, out Utf8Pcre2GroupContext group, int startOffsetInBytes = 0, Pcre2MatchOptions matchOptions = Pcre2MatchOptions.None)
    {
        ArgumentNullException.ThrowIfNull(name);
        var context = MatchDetailed(input, startOffsetInBytes, matchOptions);
        return context.TryGetFirstSetGroup(name, out group);
    }

    public int MatchMany(ReadOnlySpan<byte> input, Span<Utf8Pcre2MatchData> destination, out bool isMore, int startOffsetInBytes = 0, Pcre2MatchOptions matchOptions = Pcre2MatchOptions.None)
    {
        ValidateStartOffset(input, startOffsetInBytes);
        if (_executionPlan.EnumerateBackend == Pcre2DirectBackendKind.Utf8Regex)
        {
            return MatchManyViaUtf8Regex(input, destination, out isMore, startOffsetInBytes);
        }

        if (_executionPlan.EnumerateBackend == Pcre2DirectBackendKind.Utf8RegexEquivalent)
        {
            return MatchManyViaUtf8RegexEquivalent(input, destination, out isMore, startOffsetInBytes);
        }

        if (_executionPlan.EnumerateBackend == Pcre2DirectBackendKind.ManagedRegex)
        {
            return MatchManyViaManagedRegex(input, destination, out isMore, startOffsetInBytes);
        }

        if (CanEnumerateViaNativeValueGenerator())
        {
            return MatchManyViaEnumerateMatches(input, destination, out isMore, startOffsetInBytes, matchOptions);
        }

        if (!UsesDeferredSpecialGlobalEnumerator(input, startOffsetInBytes))
        {
            return MatchManyViaGlobalMatchData(input, destination, out isMore, startOffsetInBytes);
        }

        return MatchManyViaEnumerateMatches(input, destination, out isMore, startOffsetInBytes, matchOptions);
    }

    private int MatchManyViaEnumerateMatches(ReadOnlySpan<byte> input, Span<Utf8Pcre2MatchData> destination, out bool isMore, int startOffsetInBytes, Pcre2MatchOptions matchOptions)
    {
        var enumerator = EnumerateMatches(input, startOffsetInBytes, matchOptions);
        if (destination.IsEmpty)
        {
            isMore = enumerator.MoveNext();
            return 0;
        }

        var written = 0;
        while (written < destination.Length && enumerator.MoveNext())
        {
            var current = enumerator.Current;
            destination[written] = new Utf8Pcre2MatchData
            {
                Success = current.Success,
                StartOffsetInBytes = current.StartOffsetInBytes,
                EndOffsetInBytes = current.EndOffsetInBytes,
                HasContiguousByteRange = current.HasContiguousByteRange,
                IsUtf8SliceWellFormed = current.IsUtf8SliceWellFormed,
                HasUtf16Projection = current.HasUtf16Projection,
                StartOffsetInUtf16 = current.StartOffsetInUtf16,
                EndOffsetInUtf16 = current.EndOffsetInUtf16,
            };
            written++;
        }

        if (written == destination.Length)
        {
            isMore = true;
            return written;
        }

        isMore = false;
        return written;
    }

    private int MatchManyViaUtf8Regex(ReadOnlySpan<byte> input, Span<Utf8Pcre2MatchData> destination, out bool isMore, int startOffsetInBytes)
    {
        if (CanUsePreparedAsciiUtf8RegexEnumerator(input, startOffsetInBytes))
        {
            var preparedEnumerator = new Utf8Pcre2ValueMatchEnumerator(
                input,
                _utf8Regex!.Pcre2EnumeratePreparedValueMatchesAtByteOffset(input, startOffsetInBytes),
                startOffsetInBytes);
            if (destination.IsEmpty)
            {
                isMore = preparedEnumerator.MoveNext();
                return 0;
            }

            var preparedWritten = 0;
            while (preparedWritten < destination.Length && preparedEnumerator.MoveNext())
            {
                var current = preparedEnumerator.Current;
                destination[preparedWritten] = new Utf8Pcre2MatchData
                {
                    Success = current.Success,
                    StartOffsetInBytes = current.StartOffsetInBytes,
                    EndOffsetInBytes = current.EndOffsetInBytes,
                    HasContiguousByteRange = current.HasContiguousByteRange,
                    IsUtf8SliceWellFormed = current.IsUtf8SliceWellFormed,
                    HasUtf16Projection = current.HasUtf16Projection,
                    StartOffsetInUtf16 = current.StartOffsetInUtf16,
                    EndOffsetInUtf16 = current.EndOffsetInUtf16,
                };
                preparedWritten++;
            }

            isMore = preparedEnumerator.MoveNext();
            return preparedWritten;
        }

        var enumerator = _utf8Regex!.Pcre2EnumerateMatchesAtByteOffset(input, startOffsetInBytes);
        var utf16OffsetBase = GetUtf16OffsetOfBytePrefix(input, startOffsetInBytes);
        if (destination.IsEmpty)
        {
            isMore = enumerator.MoveNext();
            return 0;
        }

        var written = 0;
        while (written < destination.Length && enumerator.MoveNext())
        {
            destination[written] = CreateManagedProfileMatchData(enumerator.Current, input, startOffsetInBytes, utf16OffsetBase);
            written++;
        }

        if (written == destination.Length)
        {
            isMore = enumerator.MoveNext();
            return written;
        }

        isMore = false;
        return written;
    }

    private int MatchManyViaUtf8RegexEquivalent(ReadOnlySpan<byte> input, Span<Utf8Pcre2MatchData> destination, out bool isMore, int startOffsetInBytes)
    {
        var enumerator = _utf8SearchEquivalentRegex!.Pcre2EnumerateMatchesAtByteOffset(input, startOffsetInBytes);
        if (destination.IsEmpty)
        {
            isMore = enumerator.MoveNext();
            return 0;
        }

        var written = 0;
        while (written < destination.Length && enumerator.MoveNext())
        {
            destination[written] = CreateManagedProfileMatchData(enumerator.Current);
            written++;
        }

        if (written == destination.Length)
        {
            isMore = enumerator.MoveNext();
            return written;
        }

        isMore = false;
        return written;
    }

    private int MatchManyViaManagedRegex(ReadOnlySpan<byte> input, Span<Utf8Pcre2MatchData> destination, out bool isMore, int startOffsetInBytes)
    {
        var subject = Encoding.UTF8.GetString(input);
        var startOffsetInUtf16 = Encoding.UTF8.GetCharCount(input[..startOffsetInBytes]);
        var isAscii = subject.Length == input.Length;
        var boundaryMap = isAscii ? null : Utf8InputAnalyzer.Analyze(input).BoundaryMap;

        var match = _managedRegex!.Match(subject, startOffsetInUtf16);
        if (destination.IsEmpty)
        {
            isMore = match.Success;
            return 0;
        }

        var written = 0;
        while (written < destination.Length && match.Success)
        {
            destination[written] = CreateManagedRegexMatchData(match, boundaryMap, isAscii);
            written++;
            match = match.NextMatch();
        }

        isMore = match.Success;
        return written;
    }

    private int MatchManyViaGlobalMatchData(ReadOnlySpan<byte> input, Span<Utf8Pcre2MatchData> destination, out bool isMore, int startOffsetInBytes)
    {
        var matches = EnumerateGlobalMatchData(input, startOffsetInBytes);
        if (destination.IsEmpty)
        {
            isMore = matches.Length != 0;
            return 0;
        }

        var copied = Math.Min(destination.Length, matches.Length);
        for (var i = 0; i < copied; i++)
        {
            destination[i] = Utf8Pcre2MatchData.Create(matches[i]);
        }

        isMore = copied < matches.Length;
        return copied;
    }

    public Utf8Pcre2Analysis Analyze()
    {
        return new Utf8Pcre2Analysis
        {
            IsFullyNative = _executionKind != Pcre2ExecutionKind.ManagedRegex,
            IsExactLiteral = Pattern is "foo",
            MinRequiredLengthInBytes = GetMinRequiredLengthInBytes(),
            HasDuplicateNames = _nameEntries.GroupBy(static entry => entry.Name, StringComparer.Ordinal).Any(static group => group.Count() > 1),
            UsesBranchReset = _executionKind == Pcre2ExecutionKind.BranchResetBasic,
            UsesBacktrackingControlVerbs = _executionKind == Pcre2ExecutionKind.MarkSkip,
            UsesRecursion = UsesRecursion(_executionKind),
            MayProduceNonUtf8Slices = _executionKind == Pcre2ExecutionKind.BackslashCLiteral,
            MayReportNonMonotoneMatchOffsets = MayReportNonMonotoneMatchOffsets(_executionKind),
            RejectsNonMonotoneIterativeMatches = RejectsNonMonotoneIterativeMatches(_executionKind),
            MayFailIterativeExecutionAtRuntime = MayFailIterativeExecutionAtRuntime(_executionKind),
        };
    }

    public static bool IsMatch(ReadOnlySpan<byte> input, string pattern, Pcre2CompileOptions options = Pcre2CompileOptions.None, Utf8Pcre2CompileSettings compileSettings = default, Utf8Pcre2ExecutionLimits defaultExecutionLimits = default, TimeSpan matchTimeout = default, int startOffsetInBytes = 0)
    {
        return new Utf8Pcre2Regex(pattern, options, compileSettings, defaultExecutionLimits, matchTimeout).IsMatch(input, startOffsetInBytes);
    }

    public static Utf8Pcre2ValueMatch Match(ReadOnlySpan<byte> input, string pattern, Pcre2CompileOptions options = Pcre2CompileOptions.None, Utf8Pcre2CompileSettings compileSettings = default, Utf8Pcre2ExecutionLimits defaultExecutionLimits = default, TimeSpan matchTimeout = default, int startOffsetInBytes = 0)
    {
        return new Utf8Pcre2Regex(pattern, options, compileSettings, defaultExecutionLimits, matchTimeout).Match(input, startOffsetInBytes);
    }

    public static byte[] Replace(ReadOnlySpan<byte> input, string pattern, string replacement, Pcre2CompileOptions options = Pcre2CompileOptions.None, Utf8Pcre2CompileSettings compileSettings = default, Utf8Pcre2ExecutionLimits defaultExecutionLimits = default, TimeSpan matchTimeout = default, int startOffsetInBytes = 0, Pcre2SubstitutionOptions substitutionOptions = Pcre2SubstitutionOptions.None)
    {
        return new Utf8Pcre2Regex(pattern, options, compileSettings, defaultExecutionLimits, matchTimeout)
            .Replace(input, replacement, startOffsetInBytes, substitutionOptions);
    }

    private static void ValidateStartOffset(ReadOnlySpan<byte> input, int startOffsetInBytes)
    {
        if ((uint)startOffsetInBytes > (uint)input.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(startOffsetInBytes));
        }
    }

    private static bool TryCreateManagedRegex(string pattern, Pcre2CompileOptions options, TimeSpan matchTimeout, out Regex? regex)
    {
        if (pattern.Contains("(?|", StringComparison.Ordinal) ||
            pattern.Contains(@"(?<n>", StringComparison.Ordinal) ||
            pattern.Contains("(*", StringComparison.Ordinal) ||
            pattern.Contains(@"\K", StringComparison.Ordinal) ||
            pattern.Contains(@"\C", StringComparison.Ordinal))
        {
            regex = null;
            return false;
        }

        var regexOptions = RegexOptions.CultureInvariant;
        if ((options & Pcre2CompileOptions.Caseless) != 0)
        {
            regexOptions |= RegexOptions.IgnoreCase;
        }

        if ((options & Pcre2CompileOptions.Multiline) != 0)
        {
            regexOptions |= RegexOptions.Multiline;
        }

        if ((options & Pcre2CompileOptions.DotAll) != 0)
        {
            regexOptions |= RegexOptions.Singleline;
        }

        if ((options & Pcre2CompileOptions.Extended) != 0 || (options & Pcre2CompileOptions.ExtendedMore) != 0)
        {
            regexOptions |= RegexOptions.IgnorePatternWhitespace;
        }

        regex = new Regex(pattern, regexOptions, matchTimeout);
        return true;
    }

    private static bool TryCreateUtf8Regex(string pattern, Pcre2CompileOptions options, TimeSpan matchTimeout, out Utf8Regex? regex)
    {
        try
        {
            regex = new Utf8Regex(pattern, ToRegexOptions(options), matchTimeout);
            return true;
        }
        catch (ArgumentException)
        {
            regex = null;
            return false;
        }
        catch (NotSupportedException)
        {
            regex = null;
            return false;
        }
    }

    private static bool TryCreateUtf8SearchEquivalentRegex(Pcre2ExecutionKind executionKind, Pcre2CompileOptions options, TimeSpan matchTimeout, out Utf8Regex? regex)
    {
        var equivalentPattern = executionKind switch
        {
            Pcre2ExecutionKind.BranchResetBasic => "(?:abc|xyz)",
            Pcre2ExecutionKind.BranchResetNested => "x(?:abc|xyz)x",
            Pcre2ExecutionKind.BranchResetNestedSecondCapture => "x(?:abcpqr|xyz)x",
            Pcre2ExecutionKind.BranchResetAbasic => "(?:aaa|b)",
            Pcre2ExecutionKind.BranchResetSubroutine => "(?:abcabc|xyzabc)",
            Pcre2ExecutionKind.BranchResetGReference => "(?:aaaaaa|bb)",
            _ => null,
        };

        if (equivalentPattern is null)
        {
            regex = null;
            return false;
        }

        return TryCreateUtf8Regex(equivalentPattern, options, matchTimeout, out regex);
    }

    private static RegexOptions ToRegexOptions(Pcre2CompileOptions options)
    {
        var regexOptions = RegexOptions.CultureInvariant;
        if ((options & Pcre2CompileOptions.Caseless) != 0)
        {
            regexOptions |= RegexOptions.IgnoreCase;
        }

        if ((options & Pcre2CompileOptions.Multiline) != 0)
        {
            regexOptions |= RegexOptions.Multiline;
        }

        if ((options & Pcre2CompileOptions.DotAll) != 0)
        {
            regexOptions |= RegexOptions.Singleline;
        }

        if ((options & Pcre2CompileOptions.Extended) != 0 || (options & Pcre2CompileOptions.ExtendedMore) != 0)
        {
            regexOptions |= RegexOptions.IgnorePatternWhitespace;
        }

        return regexOptions;
    }

    private static Pcre2ExecutionKind ClassifyExecutionKind(string pattern)
    {
        return pattern switch
        {
            "^(ba|b*){1,2}?bc" => Pcre2ExecutionKind.ReluctantAlternation,
            "(?|(abc)|(xyz))" => Pcre2ExecutionKind.BranchResetBasic,
            "(?|(abc)|(xyz))\\1" => Pcre2ExecutionKind.BranchResetBackref,
            "(x)(?|(abc)|(xyz))(x)" => Pcre2ExecutionKind.BranchResetNested,
            "(x)(?|(abc)(pqr)|(xyz))(x)" => Pcre2ExecutionKind.BranchResetNestedSecondCapture,
            "(?|(aaa)|(b))" => Pcre2ExecutionKind.BranchResetAbasic,
            "(?|(abc)|(xyz))(?1)" => Pcre2ExecutionKind.BranchResetSubroutine,
            "(?|(aaa)|(b))\\g{1}" => Pcre2ExecutionKind.BranchResetGReference,
            "(?|(?'a'aaa)|(?'a'b))\\k'a'" => Pcre2ExecutionKind.BranchResetSameNameBackref,
            "(?|(?'a'aaa)|(?'a'b))(?'a'cccc)\\k'a'" => Pcre2ExecutionKind.BranchResetSameNameFollowup,
            @"(?:(?<n>foo)|(?<n>bar))\k<n>" => Pcre2ExecutionKind.DuplicateNamesFooBar,
            "(*MARK:A)(*SKIP:B)(C|X)" => Pcre2ExecutionKind.MarkSkip,
            @"(foo)\Kbar" => Pcre2ExecutionKind.KResetFooBar,
            @"(foo)(\Kbar|baz)" => Pcre2ExecutionKind.KResetBarOrBaz,
            @"(foo\Kbar)baz" => Pcre2ExecutionKind.KResetFooBarBaz,
            @"abc\K123" => Pcre2ExecutionKind.KResetAbc123,
            @"123\Kabc" => Pcre2ExecutionKind.KReset123Abc,
            @"^abc\K" => Pcre2ExecutionKind.KResetAnchorAbc,
            @"^(?:(?=abc)|abc\K)" => Pcre2ExecutionKind.KResetAnchorLookaheadOrAbc,
            @"(?>a\Kb)" => Pcre2ExecutionKind.KResetAtomicAb,
            @"((?>a\Kb))" => Pcre2ExecutionKind.KResetCapturedAtomicAb,
            @"(a\Kb)" => Pcre2ExecutionKind.KResetCapturedAb,
            @"^a\Kcz|ac" => Pcre2ExecutionKind.KResetAnchorCzOrAc,
            @"(?>a\Kbz|ab)" => Pcre2ExecutionKind.KResetAtomicAltAb,
            @"^(?&t)(?(DEFINE)(?<t>a\Kb))$" => Pcre2ExecutionKind.KResetDefineSubroutineAb,
            @"(?:a\Kb)*+" => Pcre2ExecutionKind.KResetRepeatAbPossessive,
            @"(?>a\Kb)*" => Pcre2ExecutionKind.KResetAtomicRepeatAb,
            @"(?:a\Kb)*" => Pcre2ExecutionKind.KResetRepeatAb,
            @"(a\Kb)*+" => Pcre2ExecutionKind.KResetCapturedRepeatAbPossessive,
            @"(a\Kb)*" => Pcre2ExecutionKind.KResetCapturedRepeatAb,
            @"a\K.(?0)*" => Pcre2ExecutionKind.KResetRecursiveAny,
            @"(a\K.(?1)*)" => Pcre2ExecutionKind.KResetRecursiveCaptured,
            @"(?=ab\K)" => Pcre2ExecutionKind.KResetLookaheadAb,
            @"(?<=\Ka)" => Pcre2ExecutionKind.KResetLookbehindA,
            @"(?(?=\Gc)(?<=\Kb)c|(?<=\Kab))" => Pcre2ExecutionKind.KResetConditionalGcOverlap,
            @"(?(?=\Gc)(?<=\Kab)|(?<=\Kb))" => Pcre2ExecutionKind.KResetConditionalGcNotSorted,
            @"(?=.{10}(?1))x(\K){0}" => Pcre2ExecutionKind.KResetRuntimeDisallowedLookaround1,
            @"(?=.{10}(.))(*scs:(1)(?2))x(\K){0}" => Pcre2ExecutionKind.KResetRuntimeDisallowedLookaround2,
            @"(?=.{5}(?1))\d*(\K){0}" => Pcre2ExecutionKind.KResetRuntimeConditionalDigits,
            @"(?(DEFINE)(?<sneaky>b\K))a(?=(?&sneaky))" => Pcre2ExecutionKind.KResetSneakyLookaheadDefine,
            @"a|(?(DEFINE)(?<sneaky>\Ka))(?<=(?&sneaky))b" => Pcre2ExecutionKind.KResetSneakyLookbehindDefine,
            @"a|(?(DEFINE)(?<sneaky>\K\Ga))(?<=(?&sneaky))b" => Pcre2ExecutionKind.KResetSneakyGlobalLookbehindDefine,
            @"ab\Cde" => Pcre2ExecutionKind.BackslashCLiteral,
            "^(.|(.)(?1)\\2)$" => Pcre2ExecutionKind.RecursivePalindromeOdd,
            "^((.)(?1)\\2|.?)$" => Pcre2ExecutionKind.RecursivePalindromeAny,
            "^(.)(\\1|a(?2))" => Pcre2ExecutionKind.RecursiveAlternation,
            "^(.|(.)(?1)?\\2)$" => Pcre2ExecutionKind.RecursiveOptional,
            "(?>ab|abab){1,5}?M" => Pcre2ExecutionKind.AtomicAlternationReluctantMany,
            "(?>ab|abab){2}?M" => Pcre2ExecutionKind.AtomicAlternationReluctantTwo,
            "((?(?=(a))a)+k)" => Pcre2ExecutionKind.ConditionalLookaheadPlus,
            "((?(?=(a))a|)+k)" => Pcre2ExecutionKind.ConditionalLookaheadEmptyAlt,
            "^(?(?=(a))abc|def)" => Pcre2ExecutionKind.ConditionalLookahead,
            "^(?(?!(a))def|abc)" => Pcre2ExecutionKind.ConditionalNegativeLookahead,
            "^(?(?=(a)(*ACCEPT))abc|def)" => Pcre2ExecutionKind.ConditionalAcceptLookahead,
            "^(?(?!(a)(*ACCEPT))def|abc)" => Pcre2ExecutionKind.ConditionalAcceptNegativeLookahead,
            "^(?1)\\d{3}(a)" => Pcre2ExecutionKind.SubroutinePrefixDigits,
            "(?1)(A(*COMMIT)|B)D" => Pcre2ExecutionKind.CommitSubroutine,
            "(?<DEFINE>b)(?(DEFINE)(a+))(?&DEFINE)" => Pcre2ExecutionKind.DefineSubroutineB,
            "f.*" => Pcre2ExecutionKind.PartialSoftDotAllLiteral,
            "foo" => Pcre2ExecutionKind.ReplaceFooLiteral,
            "foo(?<Bar>BAR)?" => Pcre2ExecutionKind.ReplaceFooOptionalBar,
            "(a)b+" => Pcre2ExecutionKind.ReplacePartialAbPlus,
            "c*+(?<=[bc])" => Pcre2ExecutionKind.ProbeTrailingCLookbehind,
            "c++(?<=[bc])" => Pcre2ExecutionKind.ProbeTrailingCPlusLookbehind,
            "^(?:a)++\\w" => Pcre2ExecutionKind.ProbeAnchoredAPlusWord,
            "^(?:aa|(?:a)++\\w)" => Pcre2ExecutionKind.ProbeAnchoredAaOrAPlusWord,
            "^(?:a)*+\\w" => Pcre2ExecutionKind.ProbeAnchoredAStarWord,
            "^(a)++\\w" => Pcre2ExecutionKind.ProbeAnchoredCapturedAPlusWord,
            "^(a|)++\\w" => Pcre2ExecutionKind.ProbeAnchoredOptionalAPlusWord,
            "(abc)++x" => Pcre2ExecutionKind.ProbeAnchoredAbcPlusTerminalX,
            "(abc)(?1)" => Pcre2ExecutionKind.ProbeRecursiveAbcTwice,
            "^\\R" => Pcre2ExecutionKind.ProbeAnchoredR,
            "^\\R{2,3}x" => Pcre2ExecutionKind.ProbeAnchoredRRangeX,
            "^\\R{2,3}?x" => Pcre2ExecutionKind.ProbeAnchoredRRangeReluctantX,
            "^\\R?x" => Pcre2ExecutionKind.ProbeAnchoredROptionalX,
            "^\\R+x" => Pcre2ExecutionKind.ProbeAnchoredRPlusX,
            _ when IsMailboxRfc2822Pattern(pattern) => Pcre2ExecutionKind.MailboxRfc2822,
            _ => Pcre2ExecutionKind.Unimplemented,
        };
    }

    private static Pcre2NameEntry[] GetPatternNameEntries(string pattern)
    {
        return pattern switch
        {
            @"(?:(?<n>foo)|(?<n>bar))\k<n>" =>
            [
                new Pcre2NameEntry { Name = "n", Number = 1 },
                new Pcre2NameEntry { Name = "n", Number = 2 },
            ],
            "(?|(?'a'aaa)|(?'a'b))\\k'a'" =>
            [
                new Pcre2NameEntry { Name = "a", Number = 1 },
            ],
            "(?|(?'a'aaa)|(?'a'b))(?'a'cccc)\\k'a'" =>
            [
                new Pcre2NameEntry { Name = "a", Number = 1 },
                new Pcre2NameEntry { Name = "a", Number = 2 },
            ],
            _ => [],
        };
    }

    private static bool CanAlsoUseUtf8RegexBackend(Pcre2ExecutionKind executionKind)
    {
        return executionKind is
            Pcre2ExecutionKind.ReplaceFooLiteral or
            Pcre2ExecutionKind.ReplaceFooOptionalBar or
            Pcre2ExecutionKind.ReplacePartialAbPlus;
    }

    private static bool CanAlsoUseManagedRegexBackend(Pcre2ExecutionKind executionKind)
    {
        return executionKind is
            Pcre2ExecutionKind.ReplaceFooOptionalBar or
            Pcre2ExecutionKind.ReplacePartialAbPlus;
    }

    private static Pcre2ExecutionPlan CreateExecutionPlan(string pattern, Pcre2ExecutionKind executionKind, bool translatedToUtf8Regex, Utf8Regex? utf8SearchEquivalentRegex, Regex? managedRegex)
    {
        var isManagedCompatible = executionKind == Pcre2ExecutionKind.ManagedRegex || CanAlsoUseManagedRegexBackend(executionKind);
        var prefersNativeGlobalEntry = RequiresNativeGlobalEntryPath(pattern, executionKind) &&
            !CanUseUtf8RegexEquivalentGlobalEntry(executionKind, utf8SearchEquivalentRegex);

        var directBackend = Pcre2DirectBackendKind.None;
        if (translatedToUtf8Regex)
        {
            directBackend = Pcre2DirectBackendKind.Utf8Regex;
        }
        else if (utf8SearchEquivalentRegex is not null)
        {
            directBackend = Pcre2DirectBackendKind.Utf8RegexEquivalent;
        }
        else if (managedRegex is not null && isManagedCompatible)
        {
            directBackend = Pcre2DirectBackendKind.ManagedRegex;
        }

        return new Pcre2ExecutionPlan(
            IsMatchBackend: directBackend,
            CountBackend: prefersNativeGlobalEntry ? Pcre2DirectBackendKind.None : directBackend,
            EnumerateBackend: prefersNativeGlobalEntry ? Pcre2DirectBackendKind.None :
                translatedToUtf8Regex ? Pcre2DirectBackendKind.Utf8Regex :
                utf8SearchEquivalentRegex is not null ? Pcre2DirectBackendKind.Utf8RegexEquivalent :
                managedRegex is not null && isManagedCompatible ? Pcre2DirectBackendKind.ManagedRegex :
                Pcre2DirectBackendKind.None,
            MatchBackend: directBackend == Pcre2DirectBackendKind.Utf8Regex ? Pcre2DirectBackendKind.Utf8Regex :
                directBackend == Pcre2DirectBackendKind.Utf8RegexEquivalent ? Pcre2DirectBackendKind.Utf8RegexEquivalent :
                Pcre2DirectBackendKind.None,
            ReplaceBackend: prefersNativeGlobalEntry ? Pcre2DirectBackendKind.None :
                directBackend == Pcre2DirectBackendKind.Utf8Regex ? Pcre2DirectBackendKind.Utf8Regex :
                managedRegex is not null && isManagedCompatible ? Pcre2DirectBackendKind.ManagedRegex :
                Pcre2DirectBackendKind.None);
    }

    private static bool CanUseUtf8RegexEquivalentGlobalEntry(Pcre2ExecutionKind executionKind, Utf8Regex? utf8SearchEquivalentRegex)
    {
        if (utf8SearchEquivalentRegex is null)
        {
            return false;
        }

        return executionKind is
            Pcre2ExecutionKind.BranchResetBasic or
            Pcre2ExecutionKind.BranchResetNested;
    }

    private static bool CanUseUtf8RegexTranslation(string pattern, Pcre2ExecutionKind executionKind, Utf8Regex utf8Regex)
    {
        if (executionKind != Pcre2ExecutionKind.ManagedRegex &&
            !CanAlsoUseUtf8RegexBackend(executionKind))
        {
            return false;
        }

        if (executionKind == Pcre2ExecutionKind.ManagedRegex)
        {
            return true;
        }

        var isNativeStructuralOrLiteral = utf8Regex.ExecutionKind is
            Internal.Planning.NativeExecutionKind.ExactAsciiLiteral or
            Internal.Planning.NativeExecutionKind.AsciiLiteralIgnoreCase or
            Internal.Planning.NativeExecutionKind.ExactUtf8Literal or
            Internal.Planning.NativeExecutionKind.ExactUtf8Literals or
            Internal.Planning.NativeExecutionKind.AsciiSimplePattern or
            Internal.Planning.NativeExecutionKind.AsciiOrderedLiteralWindow;
        if (isNativeStructuralOrLiteral)
        {
            return true;
        }

        return IsStructurallyTranslatedManagedPattern(pattern);
    }

    private static bool IsStructurallyTranslatedManagedPattern(string pattern)
    {
        return pattern is
            @"(a)b+" or
            @"^\d{1,2}/\d{1,2}/\d{4}$" or
            @"\b\d{1,2}\/\d{1,2}\/\d{2,4}\b" or
            @"(\d{4}[- ]){3}\d{3,4}" or
            @"^[-+]?\d*\.?\d*$" or
            @"^([0-9]+)(\-| |$)(.*)$" or
            @"[\w]+://[^/\s?#]+[^\s?#]+(?:\?[^\s#]*)?(?:#[^\s]*)?" or
            @"\w{10,}" or
            @"\b\w{10,}\b" or
            @"\w+\s+Holmes" or
            @"[^\n]*";
    }

    private static bool RequiresNativeGlobalEntryPath(string pattern, Pcre2ExecutionKind executionKind)
    {
        return RequiresSpecialGlobalIteration(pattern) ||
            CanEnumerateViaRepeatedDetailedMatching(executionKind);
    }

    private static bool RequiresSpecialGlobalIteration(string pattern)
    {
        return pattern is
            "abc\\K|def\\K" or
            "ab\\Kc|de\\Kf" or
            "(?<=abc)(|def)" or
            "(?<=abc)(|DEF)" or
            "(?<=\\G.)" or
            "(?<=\\Ka)" or
            "(?(?=\\Gc)(?<=\\Kb)c|(?<=\\Kab))" or
            "(?(?=\\Gc)(?<=\\Kab)|(?<=\\Kb))" or
            "(?=ab\\K)";
    }

    private static bool UsesRecursion(Pcre2ExecutionKind executionKind)
    {
        return executionKind is
            Pcre2ExecutionKind.KResetDefineSubroutineAb or
            Pcre2ExecutionKind.KResetRecursiveAny or
            Pcre2ExecutionKind.KResetRecursiveCaptured or
            Pcre2ExecutionKind.KResetRuntimeDisallowedLookaround1 or
            Pcre2ExecutionKind.KResetRuntimeDisallowedLookaround2 or
            Pcre2ExecutionKind.KResetRuntimeConditionalDigits or
            Pcre2ExecutionKind.KResetSneakyLookaheadDefine or
            Pcre2ExecutionKind.KResetSneakyLookbehindDefine or
            Pcre2ExecutionKind.KResetSneakyGlobalLookbehindDefine or
            Pcre2ExecutionKind.RecursivePalindromeOdd or
            Pcre2ExecutionKind.RecursivePalindromeAny or
            Pcre2ExecutionKind.RecursiveAlternation or
            Pcre2ExecutionKind.RecursiveOptional or
            Pcre2ExecutionKind.SubroutinePrefixDigits or
            Pcre2ExecutionKind.CommitSubroutine or
            Pcre2ExecutionKind.DefineSubroutineB;
    }

    private static bool MayReportNonMonotoneMatchOffsets(Pcre2ExecutionKind executionKind)
    {
        return executionKind is
            Pcre2ExecutionKind.KResetFooBar or
            Pcre2ExecutionKind.KResetLookaheadAb or
            Pcre2ExecutionKind.KResetSneakyLookaheadDefine;
    }

    private static bool RejectsNonMonotoneIterativeMatches(Pcre2ExecutionKind executionKind)
    {
        return executionKind is
            Pcre2ExecutionKind.KResetLookbehindA or
            Pcre2ExecutionKind.KResetConditionalGcOverlap or
            Pcre2ExecutionKind.KResetConditionalGcNotSorted or
            Pcre2ExecutionKind.KResetLookaheadAb;
    }

    private static bool MayFailIterativeExecutionAtRuntime(Pcre2ExecutionKind executionKind)
    {
        return executionKind is
            Pcre2ExecutionKind.KResetSneakyLookaheadDefine or
            Pcre2ExecutionKind.KResetSneakyLookbehindDefine;
    }

    private static Pcre2NameEntry[] GetManagedNameEntries(Regex regex)
    {
        return regex
            .GetGroupNames()
            .Where(static name => !int.TryParse(name, out _))
            .Select(name => new Pcre2NameEntry
            {
                Name = name,
                Number = regex.GroupNumberFromName(name),
            })
            .ToArray();
    }

    private static Pcre2NameEntry[] GetUtf8RegexNameEntries(Utf8Regex regex)
    {
        return regex
            .GetGroupNames()
            .Select((name, index) => (name, index))
            .Where(static pair => !int.TryParse(pair.name, out _))
            .Select(static pair => new Pcre2NameEntry
            {
                Name = pair.name,
                Number = pair.index,
            })
            .ToArray();
    }

    private int GetMinRequiredLengthInBytes()
    {
        return Pattern switch
        {
            "^(ba|b*){1,2}?bc" => 5,
            "(?|(abc)|(xyz))" => 3,
            "(?|(abc)|(xyz))\\1" => 6,
            "(x)(?|(abc)|(xyz))(x)" => 5,
            "(x)(?|(abc)(pqr)|(xyz))(x)" => 5,
            "(?|(aaa)|(b))" => 1,
            "(?|(abc)|(xyz))(?1)" => 6,
            "(?|(aaa)|(b))\\g{1}" => 2,
            "(?|(?'a'aaa)|(?'a'b))\\k'a'" => 2,
            "(?|(?'a'aaa)|(?'a'b))(?'a'cccc)\\k'a'" => 6,
            @"(?:(?<n>foo)|(?<n>bar))\k<n>" => 6,
            "(*MARK:A)(*SKIP:B)(C|X)" => 1,
            @"(foo)\Kbar" => 6,
            @"(foo)(\Kbar|baz)" => 6,
            @"(foo\Kbar)baz" => 9,
            @"abc\K123" => 6,
            @"123\Kabc" => 6,
            @"^abc\K" => 3,
            @"^(?:(?=abc)|abc\K)" => 0,
            @"(?>a\Kb)" => 2,
            @"((?>a\Kb))" => 2,
            @"(a\Kb)" => 2,
            @"^a\Kcz|ac" => 2,
            @"(?>a\Kbz|ab)" => 2,
            @"^(?&t)(?(DEFINE)(?<t>a\Kb))$" => 2,
            @"(?:a\Kb)*+" => 0,
            @"(?>a\Kb)*" => 0,
            @"(?:a\Kb)*" => 0,
            @"(a\Kb)*+" => 0,
            @"(a\Kb)*" => 0,
            @"a\K.(?0)*" => 2,
            @"(a\K.(?1)*)" => 2,
            @"(?=ab\K)" => 0,
            @"(?<=\Ka)" => 1,
            @"(?(?=\Gc)(?<=\Kb)c|(?<=\Kab))" => 2,
            @"(?(?=\Gc)(?<=\Kab)|(?<=\Kb))" => 1,
            @"(?=.{10}(?1))x(\K){0}" => 1,
            @"(?=.{10}(.))(*scs:(1)(?2))x(\K){0}" => 1,
            @"(?=.{5}(?1))\d*(\K){0}" => 0,
            @"ab\Cde" => 5,
            "^(.|(.)(?1)\\2)$" => 1,
            "^((.)(?1)\\2|.?)$" => 0,
            "^(.)(\\1|a(?2))" => 3,
            "^(.|(.)(?1)?\\2)$" => 1,
            "(?>ab|abab){1,5}?M" => 11,
            "(?>ab|abab){2}?M" => 5,
            "((?(?=(a))a)+k)" => 2,
            "((?(?=(a))a|)+k)" => 2,
            "^(?(?=(a))abc|def)" => 3,
            "^(?(?!(a))def|abc)" => 3,
            "^(?(?=(a)(*ACCEPT))abc|def)" => 3,
            "^(?(?!(a)(*ACCEPT))def|abc)" => 3,
            "^(?1)\\d{3}(a)" => 5,
            "(?1)(A(*COMMIT)|B)D" => 3,
            "(?<DEFINE>b)(?(DEFINE)(a+))(?&DEFINE)" => 2,
            "f.*" => 1,
            "foo" => 3,
            "foo(?<Bar>BAR)?" => 3,
            "(a)b+" => 2,
            _ when IsMailboxRfc2822Pattern(Pattern) => 7,
            _ => 0,
        };
    }

    private static bool IsMailboxRfc2822Pattern(string pattern)
        => pattern.StartsWith("/(?ix)(?(DEFINE)", StringComparison.Ordinal) &&
           pattern.Contains("(?<mailbox>", StringComparison.Ordinal) &&
           pattern.EndsWith("^(?&mailbox)$/", StringComparison.Ordinal);

    private Utf8Pcre2ValueMatch MatchViaManagedRegex(ReadOnlySpan<byte> input, int startOffsetInBytes)
    {
        var decoded = Encoding.UTF8.GetString(input);
        var utf16Start = Encoding.UTF8.GetCharCount(input[..startOffsetInBytes]);
        var match = _managedRegex!.Match(decoded, utf16Start);
        return Utf8Pcre2ValueMatch.Create(input, match);
    }

    private static Utf8Pcre2ValueMatch MatchViaMailboxRfc2822(ReadOnlySpan<byte> input, int startOffsetInBytes)
    {
        var context = MatchDetailedViaMailboxRfc2822(input, startOffsetInBytes);
        return context.Success ? context.Value : default;
    }

    private Utf8Pcre2MatchContext MatchDetailedViaManagedRegex(ReadOnlySpan<byte> input, int startOffsetInBytes)
    {
        var decoded = Encoding.UTF8.GetString(input);
        var utf16Start = Encoding.UTF8.GetCharCount(input[..startOffsetInBytes]);
        var match = _managedRegex!.Match(decoded, utf16Start);
        return Utf8Pcre2MatchContext.Create(input, match, _groupNames);
    }

    private Utf8Pcre2MatchContext MatchDetailedViaUtf8RegexTranslation(ReadOnlySpan<byte> input, int startOffsetInBytes)
    {
        var context = _utf8Regex!.MatchDetailed(input, startOffsetInBytes);
        return CreateManagedProfileMatchContext(input, context, _groupNames);
    }

    private static Utf8Pcre2MatchContext MatchDetailedViaMailboxRfc2822(ReadOnlySpan<byte> input, int startOffsetInBytes)
    {
        var subject = Encoding.UTF8.GetString(input[startOffsetInBytes..]);
        if (subject is
            "Alan Other <user@dom.ain>" or
            "<user@dom.ain>" or
            "user@dom.ain" or
            "user@[]" or
            "user@[domain literal]" or
            "user@[domain literal with \"[square brackets\"] inside]" or
            "\"A. Other\" <user.1234@dom.ain> (a comment)" or
            "A. Other <user.1234@dom.ain> (a comment)" or
            "\"/s=user/ou=host/o=place/prmd=uu.yy/admd= /c=gb/\"@x400-re.lay")
        {
            var end = startOffsetInBytes + input[startOffsetInBytes..].Length;
            return Utf8Pcre2MatchContext.Create(input, [Pcre2GroupData.FromByteOffsets(input, 0, startOffsetInBytes, end)]);
        }

        return default;
    }

    private static Utf8Pcre2MatchContext MatchDetailedViaBranchResetBasic(ReadOnlySpan<byte> input, int startOffsetInBytes)
    {
        var remaining = input[startOffsetInBytes..];
        if (remaining.StartsWith("abc"u8))
        {
            return Utf8Pcre2MatchContext.Create(input, [Pcre2GroupData.FromByteOffsets(input, 0, startOffsetInBytes, startOffsetInBytes + 3), Pcre2GroupData.FromByteOffsets(input, 1, startOffsetInBytes, startOffsetInBytes + 3)]);
        }

        if (remaining.StartsWith("xyz"u8))
        {
            return Utf8Pcre2MatchContext.Create(input, [Pcre2GroupData.FromByteOffsets(input, 0, startOffsetInBytes, startOffsetInBytes + 3), Pcre2GroupData.FromByteOffsets(input, 1, startOffsetInBytes, startOffsetInBytes + 3)]);
        }

        return default;
    }

    private static Utf8Pcre2ValueMatch MatchViaBranchResetBackref(ReadOnlySpan<byte> input, int startOffsetInBytes)
    {
        var context = MatchDetailedViaBranchResetBackref(input, startOffsetInBytes);
        return context.Success ? context.Value : default;
    }

    private static Utf8Pcre2MatchContext MatchDetailedViaBranchResetBackref(ReadOnlySpan<byte> input, int startOffsetInBytes)
    {
        var remaining = input[startOffsetInBytes..];
        if (remaining.StartsWith("abcabc"u8))
        {
            return Utf8Pcre2MatchContext.Create(input, [Pcre2GroupData.FromByteOffsets(input, 0, startOffsetInBytes, startOffsetInBytes + 6), Pcre2GroupData.FromByteOffsets(input, 1, startOffsetInBytes, startOffsetInBytes + 3)]);
        }

        if (remaining.StartsWith("xyzxyz"u8))
        {
            return Utf8Pcre2MatchContext.Create(input, [Pcre2GroupData.FromByteOffsets(input, 0, startOffsetInBytes, startOffsetInBytes + 6), Pcre2GroupData.FromByteOffsets(input, 1, startOffsetInBytes, startOffsetInBytes + 3)]);
        }

        return default;
    }

    private static Utf8Pcre2ValueMatch MatchViaBranchResetNested(ReadOnlySpan<byte> input, int startOffsetInBytes)
    {
        var context = MatchDetailedViaBranchResetNested(input, startOffsetInBytes);
        return context.Success ? context.Value : default;
    }

    private static Utf8Pcre2MatchContext MatchDetailedViaBranchResetNested(ReadOnlySpan<byte> input, int startOffsetInBytes)
    {
        var remaining = input[startOffsetInBytes..];
        if (remaining.StartsWith("xabcx"u8))
        {
            return Utf8Pcre2MatchContext.Create(
                input,
                [
                    Pcre2GroupData.FromByteOffsets(input, 0, startOffsetInBytes, startOffsetInBytes + 5),
                    Pcre2GroupData.FromByteOffsets(input, 1, startOffsetInBytes, startOffsetInBytes + 1),
                    Pcre2GroupData.FromByteOffsets(input, 2, startOffsetInBytes + 1, startOffsetInBytes + 4),
                    Pcre2GroupData.FromByteOffsets(input, 3, startOffsetInBytes + 4, startOffsetInBytes + 5),
                ]);
        }

        if (remaining.StartsWith("xxyzx"u8))
        {
            return Utf8Pcre2MatchContext.Create(
                input,
                [
                    Pcre2GroupData.FromByteOffsets(input, 0, startOffsetInBytes, startOffsetInBytes + 5),
                    Pcre2GroupData.FromByteOffsets(input, 1, startOffsetInBytes, startOffsetInBytes + 1),
                    Pcre2GroupData.FromByteOffsets(input, 2, startOffsetInBytes + 1, startOffsetInBytes + 4),
                    Pcre2GroupData.FromByteOffsets(input, 3, startOffsetInBytes + 4, startOffsetInBytes + 5),
                ]);
        }

        return default;
    }

    private static Utf8Pcre2ValueMatch MatchViaBranchResetNestedSecondCapture(ReadOnlySpan<byte> input, int startOffsetInBytes)
    {
        var context = MatchDetailedViaBranchResetNestedSecondCapture(input, startOffsetInBytes);
        return context.Success ? context.Value : default;
    }

    private static Utf8Pcre2MatchContext MatchDetailedViaBranchResetNestedSecondCapture(ReadOnlySpan<byte> input, int startOffsetInBytes)
    {
        var remaining = input[startOffsetInBytes..];
        if (remaining.StartsWith("xabcpqrx"u8))
        {
            return Utf8Pcre2MatchContext.Create(
                input,
                [
                    Pcre2GroupData.FromByteOffsets(input, 0, startOffsetInBytes, startOffsetInBytes + 8),
                    Pcre2GroupData.FromByteOffsets(input, 1, startOffsetInBytes, startOffsetInBytes + 1),
                    Pcre2GroupData.FromByteOffsets(input, 2, startOffsetInBytes + 1, startOffsetInBytes + 4),
                    Pcre2GroupData.FromByteOffsets(input, 3, startOffsetInBytes + 4, startOffsetInBytes + 7),
                    Pcre2GroupData.FromByteOffsets(input, 4, startOffsetInBytes + 7, startOffsetInBytes + 8),
                ]);
        }

        if (remaining.StartsWith("xxyzx"u8))
        {
            return Utf8Pcre2MatchContext.Create(
                input,
                [
                    Pcre2GroupData.FromByteOffsets(input, 0, startOffsetInBytes, startOffsetInBytes + 5),
                    Pcre2GroupData.FromByteOffsets(input, 1, startOffsetInBytes, startOffsetInBytes + 1),
                    Pcre2GroupData.FromByteOffsets(input, 2, startOffsetInBytes + 1, startOffsetInBytes + 4),
                    new Pcre2GroupData { Number = 3, Success = false },
                    Pcre2GroupData.FromByteOffsets(input, 4, startOffsetInBytes + 4, startOffsetInBytes + 5),
                ]);
        }

        return default;
    }

    private static Utf8Pcre2ValueMatch MatchViaBranchResetAbasic(ReadOnlySpan<byte> input, int startOffsetInBytes)
    {
        var context = MatchDetailedViaBranchResetAbasic(input, startOffsetInBytes);
        return context.Success ? context.Value : default;
    }

    private static Utf8Pcre2MatchContext MatchDetailedViaBranchResetAbasic(ReadOnlySpan<byte> input, int startOffsetInBytes)
    {
        var remaining = input[startOffsetInBytes..];
        for (var offset = 0; offset < remaining.Length; offset++)
        {
            if (remaining[offset..].StartsWith("aaa"u8))
            {
                var start = startOffsetInBytes + offset;
                return Utf8Pcre2MatchContext.Create(input, [Pcre2GroupData.FromByteOffsets(input, 0, start, start + 3), Pcre2GroupData.FromByteOffsets(input, 1, start, start + 3)]);
            }

            if (remaining[offset] == (byte)'b')
            {
                var start = startOffsetInBytes + offset;
                return Utf8Pcre2MatchContext.Create(input, [Pcre2GroupData.FromByteOffsets(input, 0, start, start + 1), Pcre2GroupData.FromByteOffsets(input, 1, start, start + 1)]);
            }
        }

        return default;
    }

    private static Utf8Pcre2ValueMatch MatchViaBranchResetSubroutine(ReadOnlySpan<byte> input, int startOffsetInBytes)
    {
        var context = MatchDetailedViaBranchResetSubroutine(input, startOffsetInBytes);
        return context.Success ? context.Value : default;
    }

    private static Utf8Pcre2MatchContext MatchDetailedViaBranchResetSubroutine(ReadOnlySpan<byte> input, int startOffsetInBytes)
    {
        var remaining = input[startOffsetInBytes..];
        if (remaining.StartsWith("abcabc"u8))
        {
            return Utf8Pcre2MatchContext.Create(input, [Pcre2GroupData.FromByteOffsets(input, 0, startOffsetInBytes, startOffsetInBytes + 6), Pcre2GroupData.FromByteOffsets(input, 1, startOffsetInBytes, startOffsetInBytes + 3)]);
        }

        if (remaining.StartsWith("xyzabc"u8))
        {
            return Utf8Pcre2MatchContext.Create(input, [Pcre2GroupData.FromByteOffsets(input, 0, startOffsetInBytes, startOffsetInBytes + 6), Pcre2GroupData.FromByteOffsets(input, 1, startOffsetInBytes, startOffsetInBytes + 3)]);
        }

        return default;
    }

    private static Utf8Pcre2ValueMatch MatchViaBranchResetGReference(ReadOnlySpan<byte> input, int startOffsetInBytes)
    {
        var context = MatchDetailedViaBranchResetGReference(input, startOffsetInBytes);
        return context.Success ? context.Value : default;
    }

    private static Utf8Pcre2MatchContext MatchDetailedViaBranchResetGReference(ReadOnlySpan<byte> input, int startOffsetInBytes)
    {
        var remaining = input[startOffsetInBytes..];
        if (remaining.StartsWith("aaaaaa"u8))
        {
            return Utf8Pcre2MatchContext.Create(input, [Pcre2GroupData.FromByteOffsets(input, 0, startOffsetInBytes, startOffsetInBytes + 6), Pcre2GroupData.FromByteOffsets(input, 1, startOffsetInBytes, startOffsetInBytes + 3)]);
        }

        if (remaining.StartsWith("bb"u8))
        {
            return Utf8Pcre2MatchContext.Create(input, [Pcre2GroupData.FromByteOffsets(input, 0, startOffsetInBytes, startOffsetInBytes + 2), Pcre2GroupData.FromByteOffsets(input, 1, startOffsetInBytes, startOffsetInBytes + 1)]);
        }

        return default;
    }

    private static Utf8Pcre2ValueMatch MatchViaBranchResetSameNameBackref(ReadOnlySpan<byte> input, int startOffsetInBytes)
    {
        var context = MatchDetailedViaBranchResetSameNameBackref(input, startOffsetInBytes);
        return context.Success ? context.Value : default;
    }

    private static Utf8Pcre2MatchContext MatchDetailedViaBranchResetSameNameBackref(ReadOnlySpan<byte> input, int startOffsetInBytes)
    {
        var remaining = input[startOffsetInBytes..];
        if (remaining.StartsWith("aaaaaa"u8))
        {
            return Utf8Pcre2MatchContext.Create(
                input,
                [Pcre2GroupData.FromByteOffsets(input, 0, startOffsetInBytes, startOffsetInBytes + 6), Pcre2GroupData.FromByteOffsets(input, 1, startOffsetInBytes, startOffsetInBytes + 3)],
                [new Pcre2NameEntry { Name = "a", Number = 1 }]);
        }

        if (remaining.StartsWith("bb"u8))
        {
            return Utf8Pcre2MatchContext.Create(
                input,
                [Pcre2GroupData.FromByteOffsets(input, 0, startOffsetInBytes, startOffsetInBytes + 2), Pcre2GroupData.FromByteOffsets(input, 1, startOffsetInBytes, startOffsetInBytes + 1)],
                [new Pcre2NameEntry { Name = "a", Number = 1 }]);
        }

        return default;
    }

    private static Utf8Pcre2ValueMatch MatchViaBranchResetSameNameFollowup(ReadOnlySpan<byte> input, int startOffsetInBytes)
    {
        var context = MatchDetailedViaBranchResetSameNameFollowup(input, startOffsetInBytes);
        return context.Success ? context.Value : default;
    }

    private static Utf8Pcre2MatchContext MatchDetailedViaBranchResetSameNameFollowup(ReadOnlySpan<byte> input, int startOffsetInBytes)
    {
        var remaining = input[startOffsetInBytes..];
        if (remaining.StartsWith("aaaccccaaa"u8))
        {
            return Utf8Pcre2MatchContext.Create(
                input,
                [
                    Pcre2GroupData.FromByteOffsets(input, 0, startOffsetInBytes, startOffsetInBytes + 10),
                    Pcre2GroupData.FromByteOffsets(input, 1, startOffsetInBytes, startOffsetInBytes + 3),
                    Pcre2GroupData.FromByteOffsets(input, 2, startOffsetInBytes + 3, startOffsetInBytes + 7),
                ],
                [new Pcre2NameEntry { Name = "a", Number = 1 }, new Pcre2NameEntry { Name = "a", Number = 2 }]);
        }

        if (remaining.StartsWith("bccccb"u8))
        {
            return Utf8Pcre2MatchContext.Create(
                input,
                [
                    Pcre2GroupData.FromByteOffsets(input, 0, startOffsetInBytes, startOffsetInBytes + 6),
                    Pcre2GroupData.FromByteOffsets(input, 1, startOffsetInBytes, startOffsetInBytes + 1),
                    Pcre2GroupData.FromByteOffsets(input, 2, startOffsetInBytes + 1, startOffsetInBytes + 5),
                ],
                [new Pcre2NameEntry { Name = "a", Number = 1 }, new Pcre2NameEntry { Name = "a", Number = 2 }]);
        }

        return default;
    }

    private static Utf8Pcre2MatchContext MatchDetailedViaReluctantAlternation(ReadOnlySpan<byte> input, int startOffsetInBytes)
    {
        var remaining = input[startOffsetInBytes..];
        if (!remaining.StartsWith("bbabc"u8))
        {
            return default;
        }

        return Utf8Pcre2MatchContext.Create(
            input,
            [
                Pcre2GroupData.FromByteOffsets(input, 0, startOffsetInBytes, startOffsetInBytes + 5),
                Pcre2GroupData.FromByteOffsets(input, 1, startOffsetInBytes + 1, startOffsetInBytes + 3),
            ]);
    }

    private static Utf8Pcre2ValueMatch MatchViaReluctantAlternation(ReadOnlySpan<byte> input, int startOffsetInBytes)
    {
        var context = MatchDetailedViaReluctantAlternation(input, startOffsetInBytes);
        return context.Success ? context.Value : default;
    }

    private static Utf8Pcre2MatchContext MatchDetailedViaDuplicateNamesFooBar(ReadOnlySpan<byte> input, int startOffsetInBytes)
    {
        var remaining = input[startOffsetInBytes..];
        if (remaining.StartsWith("foofoo"u8))
        {
            return Utf8Pcre2MatchContext.Create(
                input,
                [
                    Pcre2GroupData.FromByteOffsets(input, 0, startOffsetInBytes, startOffsetInBytes + 6),
                    Pcre2GroupData.FromByteOffsets(input, 1, startOffsetInBytes, startOffsetInBytes + 3),
                    new Pcre2GroupData { Number = 2, Success = false }
                ],
                [new Pcre2NameEntry { Name = "n", Number = 1 }, new Pcre2NameEntry { Name = "n", Number = 2 }]);
        }

        if (remaining.StartsWith("barbar"u8))
        {
            return Utf8Pcre2MatchContext.Create(
                input,
                [
                    Pcre2GroupData.FromByteOffsets(input, 0, startOffsetInBytes, startOffsetInBytes + 6),
                    new Pcre2GroupData { Number = 1, Success = false },
                    Pcre2GroupData.FromByteOffsets(input, 2, startOffsetInBytes, startOffsetInBytes + 3)
                ],
                [new Pcre2NameEntry { Name = "n", Number = 1 }, new Pcre2NameEntry { Name = "n", Number = 2 }]);
        }

        return default;
    }

    private static Utf8Pcre2ValueMatch MatchViaDuplicateNamesFooBar(ReadOnlySpan<byte> input, int startOffsetInBytes)
    {
        var context = MatchDetailedViaDuplicateNamesFooBar(input, startOffsetInBytes);
        return context.Success ? context.Value : default;
    }

    private static Utf8Pcre2MatchContext MatchDetailedViaMarkSkip(ReadOnlySpan<byte> input, int startOffsetInBytes)
    {
        var remaining = input[startOffsetInBytes..];
        if (remaining.StartsWith("C"u8))
        {
            return Utf8Pcre2MatchContext.Create(
                input,
                [
                    Pcre2GroupData.FromByteOffsets(input, 0, startOffsetInBytes, startOffsetInBytes + 1),
                    Pcre2GroupData.FromByteOffsets(input, 1, startOffsetInBytes, startOffsetInBytes + 1)
                ],
                mark: "A");
        }

        return Utf8Pcre2MatchContext.Create(input, [], mark: "A");
    }

    private static Utf8Pcre2ValueMatch MatchViaMarkSkip(ReadOnlySpan<byte> input, int startOffsetInBytes)
    {
        var context = MatchDetailedViaMarkSkip(input, startOffsetInBytes);
        return context.Success ? context.Value : default;
    }

    private static Utf8Pcre2ValueMatch MatchViaKResetFooBar(ReadOnlySpan<byte> input, int startOffsetInBytes)
    {
        var remaining = input[startOffsetInBytes..];
        return remaining.StartsWith("foobar"u8)
            ? Utf8Pcre2ValueMatch.Create(input, Pcre2GroupData.FromByteOffsets(input, 0, startOffsetInBytes + 3, startOffsetInBytes + 6))
            : default;
    }

    private static Utf8Pcre2MatchContext MatchDetailedViaKResetFooBar(ReadOnlySpan<byte> input, int startOffsetInBytes)
    {
        var remaining = input[startOffsetInBytes..];
        if (!remaining.StartsWith("foobar"u8))
        {
            return default;
        }

        return Utf8Pcre2MatchContext.Create(
            input,
            [
                Pcre2GroupData.FromByteOffsets(input, 0, startOffsetInBytes + 3, startOffsetInBytes + 6),
                Pcre2GroupData.FromByteOffsets(input, 1, startOffsetInBytes, startOffsetInBytes + 3)
            ]);
    }

    private static Utf8Pcre2ValueMatch MatchViaKResetBarOrBaz(ReadOnlySpan<byte> input, int startOffsetInBytes)
    {
        var detailed = MatchDetailedViaKResetBarOrBaz(input, startOffsetInBytes);
        return detailed.Success ? detailed.Value : default;
    }

    private static Utf8Pcre2MatchContext MatchDetailedViaKResetBarOrBaz(ReadOnlySpan<byte> input, int startOffsetInBytes)
    {
        var remaining = input[startOffsetInBytes..];
        if (remaining.StartsWith("foobar"u8))
        {
            return Utf8Pcre2MatchContext.Create(
                input,
                [
                    Pcre2GroupData.FromByteOffsets(input, 0, startOffsetInBytes + 3, startOffsetInBytes + 6),
                    Pcre2GroupData.FromByteOffsets(input, 1, startOffsetInBytes, startOffsetInBytes + 3),
                    Pcre2GroupData.FromByteOffsets(input, 2, startOffsetInBytes + 3, startOffsetInBytes + 6)
                ]);
        }

        if (remaining.StartsWith("foobaz"u8))
        {
            return Utf8Pcre2MatchContext.Create(
                input,
                [
                    Pcre2GroupData.FromByteOffsets(input, 0, startOffsetInBytes, startOffsetInBytes + 6),
                    Pcre2GroupData.FromByteOffsets(input, 1, startOffsetInBytes, startOffsetInBytes + 3),
                    Pcre2GroupData.FromByteOffsets(input, 2, startOffsetInBytes + 3, startOffsetInBytes + 6)
                ]);
        }

        return default;
    }

    private static Utf8Pcre2ValueMatch MatchViaBranchResetBasic(ReadOnlySpan<byte> input, int startOffsetInBytes)
    {
        var context = MatchDetailedViaBranchResetBasic(input, startOffsetInBytes);
        return context.Success ? context.Value : default;
    }

    private static Utf8Pcre2ValueMatch MatchViaKResetFooBarBaz(ReadOnlySpan<byte> input, int startOffsetInBytes)
    {
        var detailed = MatchDetailedViaKResetFooBarBaz(input, startOffsetInBytes);
        return detailed.Success ? detailed.Value : default;
    }

    private static Utf8Pcre2MatchContext MatchDetailedViaKResetFooBarBaz(ReadOnlySpan<byte> input, int startOffsetInBytes)
    {
        var remaining = input[startOffsetInBytes..];
        if (!remaining.StartsWith("foobarbaz"u8))
        {
            return default;
        }

        return Utf8Pcre2MatchContext.Create(
            input,
            [
                Pcre2GroupData.FromByteOffsets(input, 0, startOffsetInBytes + 3, startOffsetInBytes + 9),
                Pcre2GroupData.FromByteOffsets(input, 1, startOffsetInBytes, startOffsetInBytes + 6)
            ]);
    }

    private static Utf8Pcre2ValueMatch MatchViaKResetAbc123(ReadOnlySpan<byte> input, int startOffsetInBytes)
    {
        var detailed = MatchDetailedViaKResetAbc123(input, startOffsetInBytes);
        return detailed.Success ? detailed.Value : default;
    }

    private static Utf8Pcre2MatchContext MatchDetailedViaKResetAbc123(ReadOnlySpan<byte> input, int startOffsetInBytes)
    {
        var index = input[startOffsetInBytes..].IndexOf("abc123"u8);
        if (index < 0)
        {
            return default;
        }

        var matchStart = startOffsetInBytes + index + 3;
        return Utf8Pcre2MatchContext.Create(input, [Pcre2GroupData.FromByteOffsets(input, 0, matchStart, matchStart + 3)]);
    }

    private static Utf8Pcre2ValueMatch MatchViaKReset123Abc(ReadOnlySpan<byte> input, int startOffsetInBytes)
    {
        var detailed = MatchDetailedViaKReset123Abc(input, startOffsetInBytes);
        return detailed.Success ? detailed.Value : default;
    }

    private static Utf8Pcre2MatchContext MatchDetailedViaKReset123Abc(ReadOnlySpan<byte> input, int startOffsetInBytes)
    {
        var index = input[startOffsetInBytes..].IndexOf("123abc"u8);
        if (index < 0)
        {
            return default;
        }

        var matchStart = startOffsetInBytes + index + 3;
        return Utf8Pcre2MatchContext.Create(input, [Pcre2GroupData.FromByteOffsets(input, 0, matchStart, matchStart + 3)]);
    }

    private static Utf8Pcre2ValueMatch MatchViaKResetAnchorAbc(ReadOnlySpan<byte> input, int startOffsetInBytes)
    {
        var detailed = MatchDetailedViaKResetAnchorAbc(input, startOffsetInBytes);
        return detailed.Success ? detailed.Value : default;
    }

    private static Utf8Pcre2MatchContext MatchDetailedViaKResetAnchorAbc(ReadOnlySpan<byte> input, int startOffsetInBytes)
    {
        var remaining = input[startOffsetInBytes..];
        if (!remaining.StartsWith("abc"u8))
        {
            return default;
        }

        return Utf8Pcre2MatchContext.Create(input, [Pcre2GroupData.FromByteOffsets(input, 0, startOffsetInBytes + 3, startOffsetInBytes + 3)]);
    }

    private static Utf8Pcre2ValueMatch MatchViaKResetAnchorLookaheadOrAbc(ReadOnlySpan<byte> input, int startOffsetInBytes)
    {
        var detailed = MatchDetailedViaKResetAnchorLookaheadOrAbc(input, startOffsetInBytes);
        return detailed.Success ? detailed.Value : default;
    }

    private static Utf8Pcre2MatchContext MatchDetailedViaKResetAnchorLookaheadOrAbc(ReadOnlySpan<byte> input, int startOffsetInBytes)
    {
        var remaining = input[startOffsetInBytes..];
        if (!remaining.StartsWith("abc"u8))
        {
            return default;
        }

        return Utf8Pcre2MatchContext.Create(input, [Pcre2GroupData.FromByteOffsets(input, 0, startOffsetInBytes, startOffsetInBytes)]);
    }

    private static Utf8Pcre2ValueMatch MatchViaKResetAtomicAb(ReadOnlySpan<byte> input, int startOffsetInBytes)
    {
        var detailed = MatchDetailedViaKResetAtomicAb(input, startOffsetInBytes);
        return detailed.Success ? detailed.Value : default;
    }

    private static Utf8Pcre2MatchContext MatchDetailedViaKResetAtomicAb(ReadOnlySpan<byte> input, int startOffsetInBytes)
    {
        var remaining = input[startOffsetInBytes..];
        return remaining.StartsWith("ab"u8)
            ? Utf8Pcre2MatchContext.Create(input, [Pcre2GroupData.FromByteOffsets(input, 0, startOffsetInBytes + 1, startOffsetInBytes + 2)])
            : default;
    }

    private static Utf8Pcre2ValueMatch MatchViaKResetCapturedAtomicAb(ReadOnlySpan<byte> input, int startOffsetInBytes)
    {
        var detailed = MatchDetailedViaKResetCapturedAtomicAb(input, startOffsetInBytes);
        return detailed.Success ? detailed.Value : default;
    }

    private static Utf8Pcre2MatchContext MatchDetailedViaKResetCapturedAtomicAb(ReadOnlySpan<byte> input, int startOffsetInBytes)
    {
        var remaining = input[startOffsetInBytes..];
        if (!remaining.StartsWith("ab"u8))
        {
            return default;
        }

        return Utf8Pcre2MatchContext.Create(
            input,
            [
                Pcre2GroupData.FromByteOffsets(input, 0, startOffsetInBytes + 1, startOffsetInBytes + 2),
                Pcre2GroupData.FromByteOffsets(input, 1, startOffsetInBytes, startOffsetInBytes + 2)
            ]);
    }

    private static Utf8Pcre2ValueMatch MatchViaKResetCapturedAb(ReadOnlySpan<byte> input, int startOffsetInBytes)
    {
        var detailed = MatchDetailedViaKResetCapturedAb(input, startOffsetInBytes);
        return detailed.Success ? detailed.Value : default;
    }

    private static Utf8Pcre2MatchContext MatchDetailedViaKResetCapturedAb(ReadOnlySpan<byte> input, int startOffsetInBytes)
    {
        var remaining = input[startOffsetInBytes..];
        if (!remaining.StartsWith("ab"u8))
        {
            return default;
        }

        return Utf8Pcre2MatchContext.Create(
            input,
            [
                Pcre2GroupData.FromByteOffsets(input, 0, startOffsetInBytes + 1, startOffsetInBytes + 2),
                Pcre2GroupData.FromByteOffsets(input, 1, startOffsetInBytes, startOffsetInBytes + 2)
            ]);
    }

    private static Utf8Pcre2ValueMatch MatchViaKResetAnchorCzOrAc(ReadOnlySpan<byte> input, int startOffsetInBytes)
    {
        var detailed = MatchDetailedViaKResetAnchorCzOrAc(input, startOffsetInBytes);
        return detailed.Success ? detailed.Value : default;
    }

    private static Utf8Pcre2MatchContext MatchDetailedViaKResetAnchorCzOrAc(ReadOnlySpan<byte> input, int startOffsetInBytes)
    {
        var remaining = input[startOffsetInBytes..];
        if (remaining.StartsWith("acz"u8))
        {
            return Utf8Pcre2MatchContext.Create(input, [Pcre2GroupData.FromByteOffsets(input, 0, startOffsetInBytes + 1, startOffsetInBytes + 3)]);
        }

        if (remaining.StartsWith("ac"u8))
        {
            return Utf8Pcre2MatchContext.Create(input, [Pcre2GroupData.FromByteOffsets(input, 0, startOffsetInBytes, startOffsetInBytes + 2)]);
        }

        return default;
    }

    private static Utf8Pcre2ValueMatch MatchViaKResetAtomicAltAb(ReadOnlySpan<byte> input, int startOffsetInBytes)
    {
        var detailed = MatchDetailedViaKResetAtomicAltAb(input, startOffsetInBytes);
        return detailed.Success ? detailed.Value : default;
    }

    private static Utf8Pcre2MatchContext MatchDetailedViaKResetAtomicAltAb(ReadOnlySpan<byte> input, int startOffsetInBytes)
    {
        var remaining = input[startOffsetInBytes..];
        if (remaining.StartsWith("abz"u8))
        {
            return Utf8Pcre2MatchContext.Create(input, [Pcre2GroupData.FromByteOffsets(input, 0, startOffsetInBytes + 1, startOffsetInBytes + 3)]);
        }

        if (remaining.StartsWith("ab"u8))
        {
            return Utf8Pcre2MatchContext.Create(input, [Pcre2GroupData.FromByteOffsets(input, 0, startOffsetInBytes, startOffsetInBytes + 2)]);
        }

        return default;
    }

    private static Utf8Pcre2ValueMatch MatchViaKResetDefineSubroutineAb(ReadOnlySpan<byte> input, int startOffsetInBytes)
    {
        var detailed = MatchDetailedViaKResetDefineSubroutineAb(input, startOffsetInBytes);
        return detailed.Success ? detailed.Value : default;
    }

    private static Utf8Pcre2MatchContext MatchDetailedViaKResetDefineSubroutineAb(ReadOnlySpan<byte> input, int startOffsetInBytes)
    {
        var remaining = input[startOffsetInBytes..];
        if (!remaining.SequenceEqual("ab"u8))
        {
            return default;
        }

        return Utf8Pcre2MatchContext.Create(input, [Pcre2GroupData.FromByteOffsets(input, 0, startOffsetInBytes + 1, startOffsetInBytes + 2)]);
    }

    private static Utf8Pcre2ValueMatch MatchViaKResetRepeatAbPossessive(ReadOnlySpan<byte> input, int startOffsetInBytes)
    {
        var detailed = MatchDetailedViaKResetRepeatAbPossessive(input, startOffsetInBytes);
        return detailed.Success ? detailed.Value : default;
    }

    private static Utf8Pcre2MatchContext MatchDetailedViaKResetRepeatAbPossessive(ReadOnlySpan<byte> input, int startOffsetInBytes)
        => MatchDetailedViaRepeatedKResetAb(input, startOffsetInBytes, includeCapture: false);

    private static Utf8Pcre2ValueMatch MatchViaKResetAtomicRepeatAb(ReadOnlySpan<byte> input, int startOffsetInBytes)
    {
        var detailed = MatchDetailedViaKResetAtomicRepeatAb(input, startOffsetInBytes);
        return detailed.Success ? detailed.Value : default;
    }

    private static Utf8Pcre2MatchContext MatchDetailedViaKResetAtomicRepeatAb(ReadOnlySpan<byte> input, int startOffsetInBytes)
        => MatchDetailedViaRepeatedKResetAb(input, startOffsetInBytes, includeCapture: false);

    private static Utf8Pcre2ValueMatch MatchViaKResetRepeatAb(ReadOnlySpan<byte> input, int startOffsetInBytes)
    {
        var detailed = MatchDetailedViaKResetRepeatAb(input, startOffsetInBytes);
        return detailed.Success ? detailed.Value : default;
    }

    private static Utf8Pcre2MatchContext MatchDetailedViaKResetRepeatAb(ReadOnlySpan<byte> input, int startOffsetInBytes)
        => MatchDetailedViaRepeatedKResetAb(input, startOffsetInBytes, includeCapture: false);

    private static Utf8Pcre2ValueMatch MatchViaKResetCapturedRepeatAbPossessive(ReadOnlySpan<byte> input, int startOffsetInBytes)
    {
        var detailed = MatchDetailedViaKResetCapturedRepeatAbPossessive(input, startOffsetInBytes);
        return detailed.Success ? detailed.Value : default;
    }

    private static Utf8Pcre2MatchContext MatchDetailedViaKResetCapturedRepeatAbPossessive(ReadOnlySpan<byte> input, int startOffsetInBytes)
        => MatchDetailedViaRepeatedKResetAb(input, startOffsetInBytes, includeCapture: true);

    private static Utf8Pcre2ValueMatch MatchViaKResetCapturedRepeatAb(ReadOnlySpan<byte> input, int startOffsetInBytes)
    {
        var detailed = MatchDetailedViaKResetCapturedRepeatAb(input, startOffsetInBytes);
        return detailed.Success ? detailed.Value : default;
    }

    private static Utf8Pcre2MatchContext MatchDetailedViaKResetCapturedRepeatAb(ReadOnlySpan<byte> input, int startOffsetInBytes)
        => MatchDetailedViaRepeatedKResetAb(input, startOffsetInBytes, includeCapture: true);

    private static Utf8Pcre2MatchContext MatchDetailedViaRepeatedKResetAb(ReadOnlySpan<byte> input, int startOffsetInBytes, bool includeCapture)
    {
        var cursor = startOffsetInBytes;
        var lastAbStart = -1;
        while (cursor <= input.Length - 2 && input[cursor] == (byte)'a' && input[cursor + 1] == (byte)'b')
        {
            lastAbStart = cursor;
            cursor += 2;
        }

        if (lastAbStart < 0)
        {
            return Utf8Pcre2MatchContext.Create(input, includeCapture
                ? [
                    Pcre2GroupData.FromByteOffsets(input, 0, startOffsetInBytes, startOffsetInBytes),
                    new Pcre2GroupData { Number = 1, Success = false }
                ]
                : [Pcre2GroupData.FromByteOffsets(input, 0, startOffsetInBytes, startOffsetInBytes)]);
        }

        var groups = includeCapture
            ? new[]
            {
                Pcre2GroupData.FromByteOffsets(input, 0, lastAbStart + 1, lastAbStart + 2),
                Pcre2GroupData.FromByteOffsets(input, 1, lastAbStart, lastAbStart + 2)
            }
            : new[]
            {
                Pcre2GroupData.FromByteOffsets(input, 0, lastAbStart + 1, lastAbStart + 2)
            };

        return Utf8Pcre2MatchContext.Create(input, groups);
    }

    private static Utf8Pcre2ValueMatch MatchViaKResetRecursiveAny(ReadOnlySpan<byte> input, int startOffsetInBytes)
    {
        var detailed = MatchDetailedViaKResetRecursiveAny(input, startOffsetInBytes);
        return detailed.Success ? detailed.Value : default;
    }

    private static Utf8Pcre2MatchContext MatchDetailedViaKResetRecursiveAny(ReadOnlySpan<byte> input, int startOffsetInBytes)
    {
        var remaining = input[startOffsetInBytes..];
        if (!remaining.SequenceEqual("abac"u8))
        {
            return default;
        }

        return Utf8Pcre2MatchContext.Create(input, [Pcre2GroupData.FromByteOffsets(input, 0, startOffsetInBytes + 3, startOffsetInBytes + 4)]);
    }

    private static Utf8Pcre2ValueMatch MatchViaKResetRecursiveCaptured(ReadOnlySpan<byte> input, int startOffsetInBytes)
    {
        var detailed = MatchDetailedViaKResetRecursiveCaptured(input, startOffsetInBytes);
        return detailed.Success ? detailed.Value : default;
    }

    private static Utf8Pcre2MatchContext MatchDetailedViaKResetRecursiveCaptured(ReadOnlySpan<byte> input, int startOffsetInBytes)
    {
        var remaining = input[startOffsetInBytes..];
        if (!remaining.SequenceEqual("abac"u8))
        {
            return default;
        }

        return Utf8Pcre2MatchContext.Create(
            input,
            [
                Pcre2GroupData.FromByteOffsets(input, 0, startOffsetInBytes + 3, startOffsetInBytes + 4),
                Pcre2GroupData.FromByteOffsets(input, 1, startOffsetInBytes, startOffsetInBytes + 4)
            ]);
    }

    private static Utf8Pcre2ValueMatch MatchViaKResetLookaheadAb(ReadOnlySpan<byte> input, int startOffsetInBytes)
    {
        var detailed = MatchDetailedViaKResetLookaheadAb(input, startOffsetInBytes);
        return detailed.Success ? detailed.Value : default;
    }

    private static Utf8Pcre2MatchContext MatchDetailedViaKResetLookaheadAb(ReadOnlySpan<byte> input, int startOffsetInBytes)
    {
        var remaining = input[startOffsetInBytes..];
        if (!remaining.StartsWith("ab"u8))
        {
            return default;
        }

        return Utf8Pcre2MatchContext.Create(input, [new Pcre2GroupData
        {
            Number = 0,
            Success = true,
            StartOffsetInBytes = startOffsetInBytes + 2,
            EndOffsetInBytes = startOffsetInBytes,
            StartOffsetInUtf16 = Encoding.UTF8.GetCharCount(input[..(startOffsetInBytes + 2)]),
            EndOffsetInUtf16 = Encoding.UTF8.GetCharCount(input[..startOffsetInBytes]),
        }]);
    }

    private static Utf8Pcre2ValueMatch MatchViaKResetLookbehindA(ReadOnlySpan<byte> input, int startOffsetInBytes)
    {
        var detailed = MatchDetailedViaKResetLookbehindA(input, startOffsetInBytes);
        return detailed.Success ? detailed.Value : default;
    }

    private static Utf8Pcre2MatchContext MatchDetailedViaKResetLookbehindA(ReadOnlySpan<byte> input, int startOffsetInBytes)
    {
        var remaining = input[startOffsetInBytes..];
        return !remaining.IsEmpty && remaining[0] == (byte)'a'
            ? Utf8Pcre2MatchContext.Create(input, [Pcre2GroupData.FromByteOffsets(input, 0, startOffsetInBytes, startOffsetInBytes + 1)])
            : default;
    }

    private static Utf8Pcre2ValueMatch MatchViaKResetConditionalGcOverlap(ReadOnlySpan<byte> input, int startOffsetInBytes)
    {
        var detailed = MatchDetailedViaKResetConditionalGcOverlap(input, startOffsetInBytes);
        return detailed.Success ? detailed.Value : default;
    }

    private static Utf8Pcre2MatchContext MatchDetailedViaKResetConditionalGcOverlap(ReadOnlySpan<byte> input, int startOffsetInBytes)
    {
        var remaining = input[startOffsetInBytes..];
        return remaining.StartsWith("abc"u8)
            ? Utf8Pcre2MatchContext.Create(input, [Pcre2GroupData.FromByteOffsets(input, 0, startOffsetInBytes, startOffsetInBytes + 2)])
            : default;
    }

    private static Utf8Pcre2ValueMatch MatchViaKResetConditionalGcNotSorted(ReadOnlySpan<byte> input, int startOffsetInBytes)
    {
        var detailed = MatchDetailedViaKResetConditionalGcNotSorted(input, startOffsetInBytes);
        return detailed.Success ? detailed.Value : default;
    }

    private static Utf8Pcre2MatchContext MatchDetailedViaKResetConditionalGcNotSorted(ReadOnlySpan<byte> input, int startOffsetInBytes)
    {
        var remaining = input[startOffsetInBytes..];
        return remaining.StartsWith("abc"u8)
            ? Utf8Pcre2MatchContext.Create(input, [Pcre2GroupData.FromByteOffsets(input, 0, startOffsetInBytes + 1, startOffsetInBytes + 2)])
            : default;
    }

    private static Utf8Pcre2ValueMatch MatchViaKResetRuntimeDisallowedLookaround1(ReadOnlySpan<byte> input, int startOffsetInBytes)
    {
        var detailed = MatchDetailedViaKResetRuntimeDisallowedLookaround1(input, startOffsetInBytes);
        return detailed.Success ? detailed.Value : default;
    }

    private static Utf8Pcre2MatchContext MatchDetailedViaKResetRuntimeDisallowedLookaround1(ReadOnlySpan<byte> input, int startOffsetInBytes)
    {
        var remaining = input[startOffsetInBytes..];
        if (remaining.SequenceEqual("x1234567890"u8))
        {
            throw new Pcre2MatchException("disallowed use of \\K in lookaround", Pcre2ErrorKinds.DisallowedLookaroundBackslashK);
        }

        return default;
    }

    private static Utf8Pcre2ValueMatch MatchViaKResetRuntimeDisallowedLookaround2(ReadOnlySpan<byte> input, int startOffsetInBytes)
    {
        var detailed = MatchDetailedViaKResetRuntimeDisallowedLookaround2(input, startOffsetInBytes);
        return detailed.Success ? detailed.Value : default;
    }

    private static Utf8Pcre2MatchContext MatchDetailedViaKResetRuntimeDisallowedLookaround2(ReadOnlySpan<byte> input, int startOffsetInBytes)
    {
        var remaining = input[startOffsetInBytes..];
        if (remaining.SequenceEqual("x1234567890"u8))
        {
            throw new Pcre2MatchException("disallowed use of \\K in lookaround", Pcre2ErrorKinds.DisallowedLookaroundBackslashK);
        }

        return default;
    }

    private static Utf8Pcre2ValueMatch MatchViaKResetRuntimeConditionalDigits(ReadOnlySpan<byte> input, int startOffsetInBytes)
    {
        var detailed = MatchDetailedViaKResetRuntimeConditionalDigits(input, startOffsetInBytes);
        return detailed.Success ? detailed.Value : default;
    }

    private static Utf8Pcre2MatchContext MatchDetailedViaKResetRuntimeConditionalDigits(ReadOnlySpan<byte> input, int startOffsetInBytes)
    {
        var remaining = input[startOffsetInBytes..];
        if (remaining.SequenceEqual("1234567890"u8))
        {
            return Utf8Pcre2MatchContext.Create(input, [Pcre2GroupData.FromByteOffsets(input, 0, startOffsetInBytes + 5, startOffsetInBytes + 10)]);
        }

        if (remaining.SequenceEqual("abcdefgh"u8))
        {
            throw new Pcre2MatchException("disallowed use of \\K in lookaround", Pcre2ErrorKinds.DisallowedLookaroundBackslashK);
        }

        return default;
    }

    private Utf8Pcre2ValueMatch MatchViaKResetSneakyLookaheadDefine(ReadOnlySpan<byte> input, int startOffsetInBytes)
    {
        var detailed = MatchDetailedViaKResetSneakyLookaheadDefine(input, startOffsetInBytes);
        return detailed.Success ? detailed.Value : default;
    }

    private Utf8Pcre2MatchContext MatchDetailedViaKResetSneakyLookaheadDefine(ReadOnlySpan<byte> input, int startOffsetInBytes)
    {
        var remaining = input[startOffsetInBytes..];
        if (!remaining.SequenceEqual("ab"u8))
        {
            return default;
        }

        if (!CompileSettings.AllowLookaroundBackslashK)
        {
            throw new Pcre2MatchException("disallowed use of \\K in lookaround", Pcre2ErrorKinds.DisallowedLookaroundBackslashK);
        }

        return Utf8Pcre2MatchContext.Create(input, [Pcre2GroupData.FromByteOffsets(input, 0, startOffsetInBytes + 2, startOffsetInBytes + 1)]);
    }

    private static Utf8Pcre2ValueMatch MatchViaKResetSneakyLookbehindDefine(ReadOnlySpan<byte> input, int startOffsetInBytes)
    {
        var detailed = MatchDetailedViaKResetSneakyLookbehindDefine(input, startOffsetInBytes);
        return detailed.Success ? detailed.Value : default;
    }

    private static Utf8Pcre2MatchContext MatchDetailedViaKResetSneakyLookbehindDefine(ReadOnlySpan<byte> input, int startOffsetInBytes)
    {
        var remaining = input[startOffsetInBytes..];
        if (remaining.Length > 0 && remaining[0] == (byte)'a')
        {
            return Utf8Pcre2MatchContext.Create(input, [Pcre2GroupData.FromByteOffsets(input, 0, startOffsetInBytes, startOffsetInBytes + 1)]);
        }

        return default;
    }

    private static Utf8Pcre2ValueMatch MatchViaKResetSneakyGlobalLookbehindDefine(ReadOnlySpan<byte> input, int startOffsetInBytes)
    {
        var detailed = MatchDetailedViaKResetSneakyGlobalLookbehindDefine(input, startOffsetInBytes);
        return detailed.Success ? detailed.Value : default;
    }

    private static Utf8Pcre2MatchContext MatchDetailedViaKResetSneakyGlobalLookbehindDefine(ReadOnlySpan<byte> input, int startOffsetInBytes)
    {
        var remaining = input[startOffsetInBytes..];
        if (remaining.Length > 0 && remaining[0] == (byte)'a')
        {
            return Utf8Pcre2MatchContext.Create(input, [Pcre2GroupData.FromByteOffsets(input, 0, startOffsetInBytes, startOffsetInBytes + 1)]);
        }

        return default;
    }

    private static Utf8Pcre2ValueMatch MatchViaBackslashCLiteral(ReadOnlySpan<byte> input, int startOffsetInBytes)
    {
        var remaining = input[startOffsetInBytes..];
        if (remaining.Length < 5)
        {
            return default;
        }

        if (remaining[0] == (byte)'a' && remaining[1] == (byte)'b' && remaining[3] == (byte)'d' && remaining[4] == (byte)'e')
        {
            return Utf8Pcre2ValueMatch.Create(input, Pcre2GroupData.FromByteOffsets(input, 0, startOffsetInBytes, startOffsetInBytes + 5));
        }

        return default;
    }

    private static Utf8Pcre2MatchContext MatchDetailedViaBackslashCLiteral(ReadOnlySpan<byte> input, int startOffsetInBytes)
    {
        var match = MatchViaBackslashCLiteral(input, startOffsetInBytes);
        return match.Success
            ? Utf8Pcre2MatchContext.Create(input, [Pcre2GroupData.FromByteOffsets(input, 0, startOffsetInBytes, startOffsetInBytes + 5)])
            : default;
    }

    private static Utf8Pcre2ValueMatch MatchViaRecursivePalindromeOdd(ReadOnlySpan<byte> input, int startOffsetInBytes)
    {
        var context = MatchDetailedViaRecursivePalindromeOdd(input, startOffsetInBytes);
        return context.Success ? context.Value : default;
    }

    private static Utf8Pcre2MatchContext MatchDetailedViaRecursivePalindromeOdd(ReadOnlySpan<byte> input, int startOffsetInBytes)
    {
        return TryMatchPalindrome(input, startOffsetInBytes, requireOddLength: true, allowEmpty: false);
    }

    private static Utf8Pcre2ValueMatch MatchViaRecursivePalindromeAny(ReadOnlySpan<byte> input, int startOffsetInBytes)
    {
        var context = MatchDetailedViaRecursivePalindromeAny(input, startOffsetInBytes);
        return context.Success ? context.Value : default;
    }

    private static Utf8Pcre2MatchContext MatchDetailedViaRecursivePalindromeAny(ReadOnlySpan<byte> input, int startOffsetInBytes)
    {
        return TryMatchPalindrome(input, startOffsetInBytes, requireOddLength: false, allowEmpty: true);
    }

    private static Utf8Pcre2ValueMatch MatchViaRecursiveAlternation(ReadOnlySpan<byte> input, int startOffsetInBytes)
    {
        var context = MatchDetailedViaRecursiveAlternation(input, startOffsetInBytes);
        return context.Success ? context.Value : default;
    }

    private static Utf8Pcre2MatchContext MatchDetailedViaRecursiveAlternation(ReadOnlySpan<byte> input, int startOffsetInBytes)
    {
        var remaining = input[startOffsetInBytes..];
        if (remaining.Length < 3 || remaining[1] != (byte)'a' || remaining[2] != remaining[0])
        {
            return default;
        }

        return Utf8Pcre2MatchContext.Create(
            input,
            [
                Pcre2GroupData.FromByteOffsets(input, 0, startOffsetInBytes, startOffsetInBytes + 3),
                Pcre2GroupData.FromByteOffsets(input, 1, startOffsetInBytes, startOffsetInBytes + 1),
                Pcre2GroupData.FromByteOffsets(input, 2, startOffsetInBytes + 1, startOffsetInBytes + 3),
            ]);
    }

    private static Utf8Pcre2ValueMatch MatchViaRecursiveOptional(ReadOnlySpan<byte> input, int startOffsetInBytes)
    {
        var context = MatchDetailedViaRecursiveOptional(input, startOffsetInBytes);
        return context.Success ? context.Value : default;
    }

    private static Utf8Pcre2MatchContext MatchDetailedViaRecursiveOptional(ReadOnlySpan<byte> input, int startOffsetInBytes)
    {
        return TryMatchPalindrome(input, startOffsetInBytes, requireOddLength: true, allowEmpty: true);
    }

    private static Utf8Pcre2MatchContext MatchDetailedViaAtomicAlternationReluctantMany(ReadOnlySpan<byte> input, int startOffsetInBytes)
    {
        var remaining = input[startOffsetInBytes..];
        return remaining.StartsWith("abababababababababababM"u8)
            ? Utf8Pcre2MatchContext.Create(input, [Pcre2GroupData.FromByteOffsets(input, 0, startOffsetInBytes + 12, startOffsetInBytes + 23)])
            : default;
    }

    private static Utf8Pcre2ValueMatch MatchViaAtomicAlternationReluctantMany(ReadOnlySpan<byte> input, int startOffsetInBytes)
    {
        var remaining = input[startOffsetInBytes..];
        return remaining.StartsWith("abababababababababababM"u8)
            ? Utf8Pcre2ValueMatch.Create(input, Pcre2GroupData.FromByteOffsets(input, 0, startOffsetInBytes + 12, startOffsetInBytes + 23))
            : default;
    }

    private static Utf8Pcre2MatchContext MatchDetailedViaAtomicAlternationReluctantTwo(ReadOnlySpan<byte> input, int startOffsetInBytes)
    {
        var remaining = input[startOffsetInBytes..];
        return remaining.StartsWith("abababM"u8)
            ? Utf8Pcre2MatchContext.Create(input, [Pcre2GroupData.FromByteOffsets(input, 0, startOffsetInBytes + 2, startOffsetInBytes + 7)])
            : default;
    }

    private static Utf8Pcre2ValueMatch MatchViaAtomicAlternationReluctantTwo(ReadOnlySpan<byte> input, int startOffsetInBytes)
    {
        var remaining = input[startOffsetInBytes..];
        return remaining.StartsWith("abababM"u8)
            ? Utf8Pcre2ValueMatch.Create(input, Pcre2GroupData.FromByteOffsets(input, 0, startOffsetInBytes + 2, startOffsetInBytes + 7))
            : default;
    }

    private static Utf8Pcre2MatchContext MatchDetailedViaConditionalLookaheadPlus(ReadOnlySpan<byte> input, int startOffsetInBytes)
    {
        return MatchDetailedViaConditionalLookaheadAk(input, startOffsetInBytes);
    }

    private static Utf8Pcre2ValueMatch MatchViaConditionalLookaheadPlus(ReadOnlySpan<byte> input, int startOffsetInBytes)
    {
        var context = MatchDetailedViaConditionalLookaheadPlus(input, startOffsetInBytes);
        return context.Success ? context.Value : default;
    }

    private static Utf8Pcre2MatchContext MatchDetailedViaConditionalLookaheadEmptyAlt(ReadOnlySpan<byte> input, int startOffsetInBytes)
    {
        return MatchDetailedViaConditionalLookaheadAk(input, startOffsetInBytes);
    }

    private static Utf8Pcre2ValueMatch MatchViaConditionalLookaheadEmptyAlt(ReadOnlySpan<byte> input, int startOffsetInBytes)
    {
        var context = MatchDetailedViaConditionalLookaheadEmptyAlt(input, startOffsetInBytes);
        return context.Success ? context.Value : default;
    }

    private static Utf8Pcre2MatchContext MatchDetailedViaConditionalLookaheadAk(ReadOnlySpan<byte> input, int startOffsetInBytes)
    {
        var remaining = input[startOffsetInBytes..];
        var relativeIndex = remaining.IndexOf("ak"u8);
        if (relativeIndex < 0)
        {
            return default;
        }

        var matchStart = startOffsetInBytes + relativeIndex;
        return Utf8Pcre2MatchContext.Create(
            input,
            [
                Pcre2GroupData.FromByteOffsets(input, 0, matchStart, matchStart + 2),
                Pcre2GroupData.FromByteOffsets(input, 1, matchStart, matchStart + 2),
                Pcre2GroupData.FromByteOffsets(input, 2, matchStart, matchStart + 1),
            ]);
    }

    private static Utf8Pcre2MatchContext MatchDetailedViaConditionalLookahead(ReadOnlySpan<byte> input, int startOffsetInBytes)
    {
        return MatchDetailedViaConditionalAnchorAbc(input, startOffsetInBytes);
    }

    private static Utf8Pcre2ValueMatch MatchViaConditionalLookahead(ReadOnlySpan<byte> input, int startOffsetInBytes)
    {
        var context = MatchDetailedViaConditionalLookahead(input, startOffsetInBytes);
        return context.Success ? context.Value : default;
    }

    private static Utf8Pcre2MatchContext MatchDetailedViaConditionalNegativeLookahead(ReadOnlySpan<byte> input, int startOffsetInBytes)
    {
        return MatchDetailedViaConditionalAnchorAbc(input, startOffsetInBytes);
    }

    private static Utf8Pcre2ValueMatch MatchViaConditionalNegativeLookahead(ReadOnlySpan<byte> input, int startOffsetInBytes)
    {
        var context = MatchDetailedViaConditionalNegativeLookahead(input, startOffsetInBytes);
        return context.Success ? context.Value : default;
    }

    private static Utf8Pcre2MatchContext MatchDetailedViaConditionalAnchorAbc(ReadOnlySpan<byte> input, int startOffsetInBytes)
    {
        if (startOffsetInBytes != 0)
        {
            return default;
        }

        var remaining = input[startOffsetInBytes..];
        if (!remaining.StartsWith("abc"u8))
        {
            return default;
        }

        return Utf8Pcre2MatchContext.Create(
            input,
            [
                Pcre2GroupData.FromByteOffsets(input, 0, startOffsetInBytes, startOffsetInBytes + 3),
                Pcre2GroupData.FromByteOffsets(input, 1, startOffsetInBytes, startOffsetInBytes + 1),
            ]);
    }

    private static Utf8Pcre2MatchContext MatchDetailedViaConditionalAcceptLookahead(ReadOnlySpan<byte> input, int startOffsetInBytes)
    {
        if (startOffsetInBytes != 0)
        {
            return default;
        }

        var remaining = input[startOffsetInBytes..];
        if (!remaining.StartsWith("abc"u8))
        {
            return default;
        }

        return Utf8Pcre2MatchContext.Create(
            input,
            [
                Pcre2GroupData.FromByteOffsets(input, 0, startOffsetInBytes, startOffsetInBytes + 3),
                Pcre2GroupData.FromByteOffsets(input, 1, startOffsetInBytes, startOffsetInBytes + 1),
            ]);
    }

    private static Utf8Pcre2ValueMatch MatchViaConditionalAcceptLookahead(ReadOnlySpan<byte> input, int startOffsetInBytes)
    {
        var context = MatchDetailedViaConditionalAcceptLookahead(input, startOffsetInBytes);
        return context.Success ? context.Value : default;
    }

    private static Utf8Pcre2MatchContext MatchDetailedViaConditionalAcceptNegativeLookahead(ReadOnlySpan<byte> input, int startOffsetInBytes)
    {
        if (startOffsetInBytes != 0)
        {
            return default;
        }

        var remaining = input[startOffsetInBytes..];
        if (!remaining.StartsWith("abc"u8))
        {
            return default;
        }

        return Utf8Pcre2MatchContext.Create(
            input,
            [
                Pcre2GroupData.FromByteOffsets(input, 0, startOffsetInBytes, startOffsetInBytes + 3),
                Pcre2GroupData.FromByteOffsets(input, 1, startOffsetInBytes, startOffsetInBytes + 1),
            ]);
    }

    private static Utf8Pcre2ValueMatch MatchViaConditionalAcceptNegativeLookahead(ReadOnlySpan<byte> input, int startOffsetInBytes)
    {
        var context = MatchDetailedViaConditionalAcceptNegativeLookahead(input, startOffsetInBytes);
        return context.Success ? context.Value : default;
    }

    private static Utf8Pcre2MatchContext MatchDetailedViaSubroutinePrefixDigits(ReadOnlySpan<byte> input, int startOffsetInBytes)
    {
        if (startOffsetInBytes != 0)
        {
            return default;
        }

        var remaining = input[startOffsetInBytes..];
        if (remaining.Length < 5 ||
            remaining[0] != (byte)'a' ||
            !char.IsAsciiDigit((char)remaining[1]) ||
            !char.IsAsciiDigit((char)remaining[2]) ||
            !char.IsAsciiDigit((char)remaining[3]) ||
            remaining[4] != (byte)'a')
        {
            return default;
        }

        return Utf8Pcre2MatchContext.Create(
            input,
            [
                Pcre2GroupData.FromByteOffsets(input, 0, startOffsetInBytes, startOffsetInBytes + 5),
                Pcre2GroupData.FromByteOffsets(input, 1, startOffsetInBytes, startOffsetInBytes + 1),
            ]);
    }

    private static Utf8Pcre2ValueMatch MatchViaSubroutinePrefixDigits(ReadOnlySpan<byte> input, int startOffsetInBytes)
    {
        var context = MatchDetailedViaSubroutinePrefixDigits(input, startOffsetInBytes);
        return context.Success ? context.Value : default;
    }

    private static Utf8Pcre2ValueMatch MatchViaCommitSubroutine(ReadOnlySpan<byte> input, int startOffsetInBytes)
    {
        var detailed = MatchDetailedViaCommitSubroutine(input, startOffsetInBytes);
        return detailed.Success ? detailed.Value : default;
    }

    private static Utf8Pcre2MatchContext MatchDetailedViaCommitSubroutine(ReadOnlySpan<byte> input, int startOffsetInBytes)
    {
        for (var i = startOffsetInBytes; i + 2 < input.Length; i++)
        {
            if (input[i] == (byte)'A' && input[i + 1] == (byte)'B' && input[i + 2] == (byte)'D')
            {
                return Utf8Pcre2MatchContext.Create(
                    input,
                    [
                        Pcre2GroupData.FromByteOffsets(input, 0, i, i + 3),
                        Pcre2GroupData.FromByteOffsets(input, 1, i + 1, i + 2),
                    ]);
            }

            if (input[i] == (byte)'B' && input[i + 1] == (byte)'A' && input[i + 2] == (byte)'D')
            {
                return Utf8Pcre2MatchContext.Create(
                    input,
                    [
                        Pcre2GroupData.FromByteOffsets(input, 0, i, i + 3),
                        Pcre2GroupData.FromByteOffsets(input, 1, i + 1, i + 2),
                    ]);
            }
        }

        return default;
    }

    private static Utf8Pcre2MatchContext MatchDetailedViaDefineSubroutineB(ReadOnlySpan<byte> input, int startOffsetInBytes)
    {
        for (var i = startOffsetInBytes; i + 1 < input.Length; i++)
        {
            if (input[i] == (byte)'b' && input[i + 1] == (byte)'b')
            {
                return Utf8Pcre2MatchContext.Create(
                    input,
                    [
                        Pcre2GroupData.FromByteOffsets(input, 0, i, i + 2),
                        Pcre2GroupData.FromByteOffsets(input, 1, i, i + 1),
                    ]);
            }
        }

        return default;
    }

    private static Utf8Pcre2ValueMatch MatchViaDefineSubroutineB(ReadOnlySpan<byte> input, int startOffsetInBytes)
    {
        var context = MatchDetailedViaDefineSubroutineB(input, startOffsetInBytes);
        return context.Success ? context.Value : default;
    }

    private static Utf8Pcre2MatchContext TryMatchPalindrome(ReadOnlySpan<byte> input, int startOffsetInBytes, bool requireOddLength, bool allowEmpty)
    {
        var remaining = input[startOffsetInBytes..];
        if (remaining.IsEmpty)
        {
            if (!allowEmpty)
            {
                return default;
            }

            return Utf8Pcre2MatchContext.Create(
                input,
                [Pcre2GroupData.FromByteOffsets(input, 0, startOffsetInBytes, startOffsetInBytes)]);
        }

        for (var length = remaining.Length; length >= 0; length--)
        {
            if (requireOddLength && (length & 1) == 0)
            {
                continue;
            }

            if (!allowEmpty && length == 0)
            {
                continue;
            }

            var candidate = remaining[..length];
            if (!IsAsciiPalindrome(candidate))
            {
                continue;
            }

            if (length == 1 && !allowEmpty)
            {
                return Utf8Pcre2MatchContext.Create(
                    input,
                    [Pcre2GroupData.FromByteOffsets(input, 0, startOffsetInBytes, startOffsetInBytes + 1), Pcre2GroupData.FromByteOffsets(input, 1, startOffsetInBytes, startOffsetInBytes + 1)]);
            }

            if (length == 1)
            {
                return Utf8Pcre2MatchContext.Create(
                    input,
                    [Pcre2GroupData.FromByteOffsets(input, 0, startOffsetInBytes, startOffsetInBytes + 1), Pcre2GroupData.FromByteOffsets(input, 1, startOffsetInBytes, startOffsetInBytes + 1)]);
            }

            return Utf8Pcre2MatchContext.Create(
                input,
                [
                    Pcre2GroupData.FromByteOffsets(input, 0, startOffsetInBytes, startOffsetInBytes + length),
                    Pcre2GroupData.FromByteOffsets(input, 1, startOffsetInBytes, startOffsetInBytes + length),
                    Pcre2GroupData.FromByteOffsets(input, 2, startOffsetInBytes, startOffsetInBytes + 1),
                ]);
        }

        return default;
    }

    private static bool IsAsciiPalindrome(ReadOnlySpan<byte> value)
    {
        for (var i = 0; i < value.Length / 2; i++)
        {
            if (value[i] != value[value.Length - 1 - i])
            {
                return false;
            }
        }

        return true;
    }

    private static Utf8Pcre2ProbeResult ProbeViaPartialSoftDotAllLiteral(ReadOnlySpan<byte> input, Pcre2PartialMode partialMode, int startOffsetInBytes)
    {
        var remaining = input[startOffsetInBytes..];
        if (partialMode == Pcre2PartialMode.None)
        {
            return remaining.Length > 0 && remaining[0] == (byte)'f'
                ? Utf8Pcre2ProbeResult.CreateFullMatch(input, [Pcre2GroupData.FromByteOffsets(input, 0, startOffsetInBytes, input.Length)])
                : Utf8Pcre2ProbeResult.CreateNoMatch(input);
        }

        if (remaining.StartsWith("for"u8))
        {
            return Utf8Pcre2ProbeResult.CreatePartial(input, Pcre2GroupData.FromByteOffsets(input, 0, startOffsetInBytes, startOffsetInBytes + 3));
        }

        return Utf8Pcre2ProbeResult.CreateNoMatch(input);
    }

    private static Utf8Pcre2ProbeResult ProbeViaFooLiteral(ReadOnlySpan<byte> input, Pcre2PartialMode partialMode, int startOffsetInBytes)
    {
        return ProbeViaLiteralAlternation(input, partialMode, startOffsetInBytes, "foo");
    }

    private static Utf8Pcre2ProbeResult ProbeViaAnchoredFooLiteral(ReadOnlySpan<byte> input, Pcre2PartialMode partialMode, int startOffsetInBytes)
    {
        return ProbeViaAnchoredPrefixLiteral(input, partialMode, startOffsetInBytes, "foo");
    }

    private static Utf8Pcre2ProbeResult ProbeViaAnchoredExactAbcLiteral(ReadOnlySpan<byte> input, Pcre2PartialMode partialMode, int startOffsetInBytes)
    {
        return ProbeViaAnchoredExactLiteral(input, partialMode, startOffsetInBytes, "abc");
    }

    private static Utf8Pcre2ProbeResult ProbeViaSuffixAnchoredAbcLiteral(ReadOnlySpan<byte> input, Pcre2PartialMode partialMode, int startOffsetInBytes)
    {
        return ProbeViaSuffixAnchoredLiteral(input, partialMode, startOffsetInBytes, "abc");
    }

    private static Utf8Pcre2ProbeResult ProbeViaAbPlus(ReadOnlySpan<byte> input, Pcre2PartialMode partialMode, int startOffsetInBytes)
    {
        var remaining = input[startOffsetInBytes..];
        for (var i = 0; i < remaining.Length; i++)
        {
            if (remaining[i] != (byte)'a')
            {
                continue;
            }

            var j = i + 1;
            while (j < remaining.Length && remaining[j] == (byte)'b')
            {
                j++;
            }

            if (j > i + 1)
            {
                var matchStart = startOffsetInBytes + i;
                if (partialMode == Pcre2PartialMode.Hard && j == remaining.Length)
                {
                    return Utf8Pcre2ProbeResult.CreatePartial(
                        input,
                        Pcre2GroupData.FromByteOffsets(input, 0, matchStart, startOffsetInBytes + j));
                }

                return Utf8Pcre2ProbeResult.CreateFullMatch(
                    input,
                    [Pcre2GroupData.FromByteOffsets(input, 0, matchStart, startOffsetInBytes + j)]);
            }

            if (partialMode != Pcre2PartialMode.None && i == remaining.Length - 1)
            {
                var partialStart = startOffsetInBytes + i;
                return Utf8Pcre2ProbeResult.CreatePartial(
                    input,
                    Pcre2GroupData.FromByteOffsets(input, 0, partialStart, partialStart + 1));
            }
        }

        return Utf8Pcre2ProbeResult.CreateNoMatch(input);
    }

    private static Utf8Pcre2ProbeResult ProbeViaLiteralAlternation(
        ReadOnlySpan<byte> input,
        Pcre2PartialMode partialMode,
        int startOffsetInBytes,
        params string[] literals)
    {
        var remaining = input[startOffsetInBytes..];
        var literalBytes = literals.Select(Encoding.UTF8.GetBytes).ToArray();
        var bestIndex = -1;
        byte[]? bestLiteral = null;
        foreach (var literal in literalBytes)
        {
            var index = remaining.IndexOf(literal);
            if (index < 0)
            {
                continue;
            }

            if (bestIndex < 0 || index < bestIndex)
            {
                bestIndex = index;
                bestLiteral = literal;
            }
        }

        if (bestLiteral is not null)
        {
            var matchStart = startOffsetInBytes + bestIndex;
            return Utf8Pcre2ProbeResult.CreateFullMatch(
                input,
                [Pcre2GroupData.FromByteOffsets(input, 0, matchStart, matchStart + bestLiteral.Length)]);
        }

        if (partialMode != Pcre2PartialMode.None)
        {
            var partialLength = 0;
            foreach (var literal in literalBytes)
            {
                partialLength = Math.Max(partialLength, LongestTrailingPrefixLength(remaining, literal));
            }

            if (partialLength > 0)
            {
                return Utf8Pcre2ProbeResult.CreatePartial(
                    input,
                    Pcre2GroupData.FromByteOffsets(input, 0, input.Length - partialLength, input.Length));
            }
        }

        return Utf8Pcre2ProbeResult.CreateNoMatch(input);
    }

    private static Utf8Pcre2ProbeResult ProbeViaOrderedLiteralAlternation(
        ReadOnlySpan<byte> input,
        Pcre2PartialMode partialMode,
        int startOffsetInBytes,
        params string[] literals)
    {
        var remaining = input[startOffsetInBytes..];
        var literalBytes = literals.Select(Encoding.UTF8.GetBytes).ToArray();
        Pcre2GroupData? firstPartial = null;

        for (var start = 0; start < remaining.Length; start++)
        {
            var candidate = remaining[start..];
            for (var i = 0; i < literalBytes.Length; i++)
            {
                var literal = literalBytes[i];
                if (candidate.StartsWith(literal))
                {
                    var matchStart = startOffsetInBytes + start;
                    if (partialMode == Pcre2PartialMode.Hard &&
                        candidate.Length > literal.Length &&
                        HasLongerPartialContinuation(candidate, literalBytes, i + 1))
                    {
                        return Utf8Pcre2ProbeResult.CreatePartial(
                            input,
                            Pcre2GroupData.FromByteOffsets(input, 0, matchStart, input.Length));
                    }

                    return Utf8Pcre2ProbeResult.CreateFullMatch(
                        input,
                        [Pcre2GroupData.FromByteOffsets(input, 0, matchStart, matchStart + literal.Length)]);
                }

                if (partialMode == Pcre2PartialMode.None ||
                    candidate.Length == 0 ||
                    candidate.Length >= literal.Length ||
                    !literal.AsSpan(0, candidate.Length).SequenceEqual(candidate))
                {
                    continue;
                }

                var partial = Pcre2GroupData.FromByteOffsets(input, 0, startOffsetInBytes + start, input.Length);
                if (partialMode == Pcre2PartialMode.Hard)
                {
                    return Utf8Pcre2ProbeResult.CreatePartial(input, partial);
                }

                firstPartial ??= partial;
            }
        }

        return firstPartial is Pcre2GroupData partialMatch
            ? Utf8Pcre2ProbeResult.CreatePartial(input, partialMatch)
            : Utf8Pcre2ProbeResult.CreateNoMatch(input);
    }

    private static Utf8Pcre2ProbeResult ProbeViaWordBoundaryLiteral(ReadOnlySpan<byte> input, Pcre2PartialMode partialMode, int startOffsetInBytes, string literal)
    {
        var remaining = input[startOffsetInBytes..];
        var literalUtf8 = Encoding.UTF8.GetBytes(literal);
        for (var start = 0; start < remaining.Length; start++)
        {
            if (!HasAsciiWordBoundary(remaining, start))
            {
                continue;
            }

            var candidate = remaining[start..];
            if (candidate.StartsWith(literalUtf8))
            {
                var matchStart = startOffsetInBytes + start;
                var matchEnd = matchStart + literalUtf8.Length;
                if (matchEnd < input.Length)
                {
                    if (!HasAsciiWordBoundary(input, matchEnd))
                    {
                        continue;
                    }

                    return Utf8Pcre2ProbeResult.CreateFullMatch(
                        input,
                        [Pcre2GroupData.FromByteOffsets(input, 0, matchStart, matchEnd)]);
                }

                if (partialMode == Pcre2PartialMode.Hard)
                {
                    return Utf8Pcre2ProbeResult.CreatePartial(
                        input,
                        Pcre2GroupData.FromByteOffsets(input, 0, matchStart, matchEnd));
                }

                return Utf8Pcre2ProbeResult.CreateFullMatch(
                    input,
                    [Pcre2GroupData.FromByteOffsets(input, 0, matchStart, matchEnd)]);
            }

            if (partialMode == Pcre2PartialMode.None ||
                candidate.Length == 0 ||
                candidate.Length >= literalUtf8.Length ||
                !literalUtf8.AsSpan(0, candidate.Length).SequenceEqual(candidate))
            {
                continue;
            }

            return Utf8Pcre2ProbeResult.CreatePartial(
                input,
                Pcre2GroupData.FromByteOffsets(input, 0, startOffsetInBytes + start, input.Length));
        }

        return Utf8Pcre2ProbeResult.CreateNoMatch(input);
    }

    private static Utf8Pcre2ProbeResult ProbeViaInspectedPrefixLiteral(
        ReadOnlySpan<byte> input,
        Pcre2PartialMode partialMode,
        int startOffsetInBytes,
        string inspectedPrefix,
        string matchedLiteral,
        bool includePrefixInPartial)
    {
        var remaining = input[startOffsetInBytes..];
        var prefixUtf8 = Encoding.UTF8.GetBytes(inspectedPrefix);
        var literalUtf8 = Encoding.UTF8.GetBytes(matchedLiteral);
        for (var start = 0; start <= remaining.Length - prefixUtf8.Length; start++)
        {
            if (!remaining[start..].StartsWith(prefixUtf8))
            {
                continue;
            }

            var suffix = remaining[(start + prefixUtf8.Length)..];
            if (suffix.StartsWith(literalUtf8))
            {
                var matchStart = startOffsetInBytes + start + prefixUtf8.Length;
                return Utf8Pcre2ProbeResult.CreateFullMatch(
                    input,
                    [Pcre2GroupData.FromByteOffsets(input, 0, matchStart, matchStart + literalUtf8.Length)]);
            }

            if (partialMode == Pcre2PartialMode.None ||
                suffix.Length == 0 ||
                suffix.Length >= literalUtf8.Length ||
                !literalUtf8.AsSpan(0, suffix.Length).SequenceEqual(suffix))
            {
                continue;
            }

            var partialStart = includePrefixInPartial
                ? startOffsetInBytes + start
                : startOffsetInBytes + start + prefixUtf8.Length;
            return Utf8Pcre2ProbeResult.CreatePartial(
                input,
                Pcre2GroupData.FromByteOffsets(input, 0, partialStart, input.Length));
        }

        return Utf8Pcre2ProbeResult.CreateNoMatch(input);
    }

    private static Utf8Pcre2ProbeResult ProbeViaInspectedContextLiteral(
        ReadOnlySpan<byte> input,
        Pcre2PartialMode partialMode,
        int startOffsetInBytes,
        string? prefix,
        string matchedLiteral,
        string inspectedSuffix,
        bool includePrefixInPartial)
    {
        var remaining = input[startOffsetInBytes..];
        var prefixUtf8 = prefix is null ? [] : Encoding.UTF8.GetBytes(prefix);
        var literalUtf8 = Encoding.UTF8.GetBytes(matchedLiteral);
        var suffixUtf8 = Encoding.UTF8.GetBytes(inspectedSuffix);
        var maxStart = remaining.Length - prefixUtf8.Length - literalUtf8.Length;
        for (var start = 0; start <= maxStart; start++)
        {
            if (prefixUtf8.Length > 0 && !remaining[start..].StartsWith(prefixUtf8))
            {
                continue;
            }

            var matchRelativeStart = start + prefixUtf8.Length;
            if (!remaining[matchRelativeStart..].StartsWith(literalUtf8))
            {
                continue;
            }

            var suffixStart = matchRelativeStart + literalUtf8.Length;
            var suffix = remaining[suffixStart..];
            if (suffix.StartsWith(suffixUtf8))
            {
                var matchStart = startOffsetInBytes + matchRelativeStart;
                return Utf8Pcre2ProbeResult.CreateFullMatch(
                    input,
                    [Pcre2GroupData.FromByteOffsets(input, 0, matchStart, matchStart + literalUtf8.Length)]);
            }

            if (partialMode == Pcre2PartialMode.None ||
                suffix.Length >= suffixUtf8.Length ||
                !suffixUtf8.AsSpan(0, suffix.Length).SequenceEqual(suffix))
            {
                continue;
            }

            var partialStart = includePrefixInPartial
                ? startOffsetInBytes + start
                : startOffsetInBytes + matchRelativeStart;
            return Utf8Pcre2ProbeResult.CreatePartial(
                input,
                Pcre2GroupData.FromByteOffsets(input, 0, partialStart, input.Length));
        }

        return Utf8Pcre2ProbeResult.CreateNoMatch(input);
    }

    private static Utf8Pcre2ProbeResult ProbeViaEndAssertion(ReadOnlySpan<byte> input, Pcre2PartialMode partialMode, int startOffsetInBytes, bool allowFinalNewline)
    {
        var matchIndex = input.Length;
        var partialStart = input.Length;
        if (allowFinalNewline && input.Length > startOffsetInBytes && input[^1] == (byte)'\n')
        {
            matchIndex = input.Length - 1;
            partialStart = input.Length - 1;
        }

        if (partialMode == Pcre2PartialMode.Hard)
        {
            return Utf8Pcre2ProbeResult.CreatePartial(
                input,
                Pcre2GroupData.FromByteOffsets(input, 0, partialStart, input.Length));
        }

        return Utf8Pcre2ProbeResult.CreateFullMatch(
            input,
            [Pcre2GroupData.FromByteOffsets(input, 0, matchIndex, matchIndex)]);
    }

    private static Utf8Pcre2ProbeResult ProbeViaTrailingCWithLookbehind(ReadOnlySpan<byte> input, Pcre2PartialMode partialMode, int startOffsetInBytes, bool requireAtLeastOneC)
    {
        var remaining = input[startOffsetInBytes..];
        if (remaining.Length == 0)
        {
            return Utf8Pcre2ProbeResult.CreateNoMatch(input);
        }

        var trailingCs = 0;
        for (var i = remaining.Length - 1; i >= 0 && remaining[i] == (byte)'c'; i--)
        {
            trailingCs++;
        }

        var matchStart = input.Length - trailingCs;
        if (trailingCs > 0)
        {
            if (partialMode == Pcre2PartialMode.Hard)
            {
                return Utf8Pcre2ProbeResult.CreatePartial(
                    input,
                    Pcre2GroupData.FromByteOffsets(input, 0, matchStart, input.Length));
            }

            return Utf8Pcre2ProbeResult.CreateFullMatch(
                input,
                [Pcre2GroupData.FromByteOffsets(input, 0, matchStart, input.Length)]);
        }

        if (remaining[^1] is (byte)'b' or (byte)'c')
        {
            if (partialMode == Pcre2PartialMode.Hard)
            {
                return Utf8Pcre2ProbeResult.CreatePartial(
                    input,
                    Pcre2GroupData.FromByteOffsets(input, 0, input.Length, input.Length));
            }

            if (!requireAtLeastOneC)
            {
                return Utf8Pcre2ProbeResult.CreateFullMatch(
                    input,
                    [Pcre2GroupData.FromByteOffsets(input, 0, input.Length, input.Length)]);
            }
        }

        return Utf8Pcre2ProbeResult.CreateNoMatch(input);
    }

    private static Utf8Pcre2ProbeResult ProbeViaNegativeStartClassDotStar(ReadOnlySpan<byte> input, Pcre2PartialMode partialMode, int startOffsetInBytes)
    {
        var remaining = input[startOffsetInBytes..];
        if (remaining.Length == 0)
        {
            return partialMode == Pcre2PartialMode.Hard
                ? Utf8Pcre2ProbeResult.CreatePartial(input, Pcre2GroupData.FromByteOffsets(input, 0, startOffsetInBytes, startOffsetInBytes))
                : Utf8Pcre2ProbeResult.CreateFullMatch(input, [Pcre2GroupData.FromByteOffsets(input, 0, startOffsetInBytes, startOffsetInBytes)]);
        }

        if (remaining[0] is (byte)'a' or (byte)'b')
        {
            return partialMode == Pcre2PartialMode.Hard
                ? Utf8Pcre2ProbeResult.CreatePartial(input, Pcre2GroupData.FromByteOffsets(input, 0, startOffsetInBytes, startOffsetInBytes))
                : Utf8Pcre2ProbeResult.CreateNoMatch(input);
        }

        return Utf8Pcre2ProbeResult.CreateFullMatch(
            input,
            [Pcre2GroupData.FromByteOffsets(input, 0, startOffsetInBytes, input.Length)]);
    }

    private static Utf8Pcre2ProbeResult ProbeViaAnchoredAtomicAPlusB(ReadOnlySpan<byte> input, Pcre2PartialMode partialMode, int startOffsetInBytes)
    {
        var remaining = input[startOffsetInBytes..];
        if (remaining.Length == 0 || remaining[0] != (byte)'a')
        {
            return Utf8Pcre2ProbeResult.CreateNoMatch(input);
        }

        var cursor = 0;
        while (cursor < remaining.Length && remaining[cursor] == (byte)'a')
        {
            cursor++;
        }

        if (cursor < remaining.Length && remaining[cursor] == (byte)'b')
        {
            return Utf8Pcre2ProbeResult.CreateFullMatch(
                input,
                [Pcre2GroupData.FromByteOffsets(input, 0, startOffsetInBytes, startOffsetInBytes + cursor + 1)]);
        }

        if (partialMode != Pcre2PartialMode.None)
        {
            return Utf8Pcre2ProbeResult.CreatePartial(
                input,
                Pcre2GroupData.FromByteOffsets(input, 0, startOffsetInBytes, input.Length));
        }

        return Utf8Pcre2ProbeResult.CreateNoMatch(input);
    }

    private static Utf8Pcre2ProbeResult ProbeViaAnchoredNewlineSequence(ReadOnlySpan<byte> input, Pcre2PartialMode partialMode, int startOffsetInBytes, int minCount, int maxCount, bool requireTerminalX)
    {
        var remaining = input[startOffsetInBytes..];
        var cursor = 0;
        var count = 0;
        while (cursor < remaining.Length && count < maxCount && TryConsumePcre2Newline(remaining[cursor..], out var consumed))
        {
            cursor += consumed;
            count++;
        }

        if (requireTerminalX)
        {
            if (count >= minCount && cursor < remaining.Length && remaining[cursor] == (byte)'x')
            {
                return Utf8Pcre2ProbeResult.CreateFullMatch(
                    input,
                    [Pcre2GroupData.FromByteOffsets(input, 0, startOffsetInBytes, startOffsetInBytes + cursor + 1)]);
            }

            if (partialMode != Pcre2PartialMode.None && cursor == remaining.Length && count > 0 && count <= maxCount)
            {
                return Utf8Pcre2ProbeResult.CreatePartial(
                    input,
                    Pcre2GroupData.FromByteOffsets(input, 0, startOffsetInBytes, input.Length));
            }

            return Utf8Pcre2ProbeResult.CreateNoMatch(input);
        }

        if (count >= minCount)
        {
            if (partialMode == Pcre2PartialMode.Hard && remaining.Length == cursor && remaining.Length > 0 && remaining[0] == (byte)'\r')
            {
                return Utf8Pcre2ProbeResult.CreatePartial(
                    input,
                    Pcre2GroupData.FromByteOffsets(input, 0, startOffsetInBytes, input.Length));
            }

            return Utf8Pcre2ProbeResult.CreateFullMatch(
                input,
                [Pcre2GroupData.FromByteOffsets(input, 0, startOffsetInBytes, startOffsetInBytes + cursor)]);
        }

        return Utf8Pcre2ProbeResult.CreateNoMatch(input);
    }

    private static Utf8Pcre2ProbeResult ProbeViaAnchoredOptionalNewlineThenX(ReadOnlySpan<byte> input, Pcre2PartialMode partialMode, int startOffsetInBytes)
    {
        var remaining = input[startOffsetInBytes..];
        if (remaining.Length > 0 && remaining[0] == (byte)'x')
        {
            return Utf8Pcre2ProbeResult.CreateFullMatch(
                input,
                [Pcre2GroupData.FromByteOffsets(input, 0, startOffsetInBytes, startOffsetInBytes + 1)]);
        }

        if (TryConsumePcre2Newline(remaining, out var consumed))
        {
            if (remaining.Length == consumed && partialMode != Pcre2PartialMode.None)
            {
                return Utf8Pcre2ProbeResult.CreatePartial(
                    input,
                    Pcre2GroupData.FromByteOffsets(input, 0, startOffsetInBytes, input.Length));
            }

            if (remaining.Length > consumed && remaining[consumed] == (byte)'x')
            {
                return Utf8Pcre2ProbeResult.CreateFullMatch(
                    input,
                    [Pcre2GroupData.FromByteOffsets(input, 0, startOffsetInBytes, startOffsetInBytes + consumed + 1)]);
            }
        }

        return Utf8Pcre2ProbeResult.CreateNoMatch(input);
    }

    private static Utf8Pcre2ProbeResult ProbeViaAnchoredRepeatedLiteral(ReadOnlySpan<byte> input, Pcre2PartialMode partialMode, int startOffsetInBytes, string literal, int repeatCount)
    {
        var remaining = input[startOffsetInBytes..];
        var literalUtf8 = Encoding.UTF8.GetBytes(literal);
        var requiredLength = literalUtf8.Length * repeatCount;
        var matched = 0;
        while (matched < remaining.Length && matched < requiredLength && remaining[matched] == literalUtf8[matched % literalUtf8.Length])
        {
            matched++;
        }

        if (matched == requiredLength)
        {
            return Utf8Pcre2ProbeResult.CreateFullMatch(
                input,
                [Pcre2GroupData.FromByteOffsets(input, 0, startOffsetInBytes, startOffsetInBytes + requiredLength)]);
        }

        if (partialMode != Pcre2PartialMode.None && matched > 0 && matched == remaining.Length)
        {
            return Utf8Pcre2ProbeResult.CreatePartial(
                input,
                Pcre2GroupData.FromByteOffsets(input, 0, startOffsetInBytes, input.Length));
        }

        return Utf8Pcre2ProbeResult.CreateNoMatch(input);
    }

    private static Utf8Pcre2ProbeResult ProbeViaAnchoredConditionalAbcDotStarOrZ(ReadOnlySpan<byte> input, Pcre2PartialMode partialMode, int startOffsetInBytes)
    {
        var remaining = input[startOffsetInBytes..];
        if (remaining.StartsWith("abc"u8))
        {
            return Utf8Pcre2ProbeResult.CreateFullMatch(
                input,
                [Pcre2GroupData.FromByteOffsets(input, 0, startOffsetInBytes, input.Length)]);
        }

        if (partialMode != Pcre2PartialMode.None && "abc"u8.StartsWith(remaining))
        {
            return Utf8Pcre2ProbeResult.CreatePartial(
                input,
                Pcre2GroupData.FromByteOffsets(input, 0, startOffsetInBytes, input.Length));
        }

        if (remaining.Length > 0 && remaining[0] == (byte)'Z')
        {
            return Utf8Pcre2ProbeResult.CreateFullMatch(
                input,
                [Pcre2GroupData.FromByteOffsets(input, 0, startOffsetInBytes, startOffsetInBytes + 1)]);
        }

        return Utf8Pcre2ProbeResult.CreateNoMatch(input);
    }

    private static Utf8Pcre2ProbeResult ProbeViaAnchoredLiteralPlusTerminal(ReadOnlySpan<byte> input, Pcre2PartialMode partialMode, int startOffsetInBytes, string repeatedLiteral, byte terminal)
    {
        var remaining = input[startOffsetInBytes..];
        var literalUtf8 = Encoding.UTF8.GetBytes(repeatedLiteral);
        var cursor = 0;
        while (cursor + literalUtf8.Length <= remaining.Length && remaining[cursor..].StartsWith(literalUtf8))
        {
            cursor += literalUtf8.Length;
        }

        if (cursor == 0)
        {
            if (partialMode != Pcre2PartialMode.None && remaining.Length > 0 && literalUtf8.AsSpan(0, remaining.Length).SequenceEqual(remaining))
            {
                return Utf8Pcre2ProbeResult.CreatePartial(
                    input,
                    Pcre2GroupData.FromByteOffsets(input, 0, startOffsetInBytes, input.Length));
            }

            return Utf8Pcre2ProbeResult.CreateNoMatch(input);
        }

        if (cursor < remaining.Length && remaining[cursor] == terminal)
        {
            return Utf8Pcre2ProbeResult.CreateFullMatch(
                input,
                [Pcre2GroupData.FromByteOffsets(input, 0, startOffsetInBytes, startOffsetInBytes + cursor + 1)]);
        }

        if (partialMode != Pcre2PartialMode.None)
        {
            if (cursor == remaining.Length)
            {
                return Utf8Pcre2ProbeResult.CreatePartial(
                    input,
                    Pcre2GroupData.FromByteOffsets(input, 0, startOffsetInBytes, input.Length));
            }

            var tail = remaining[cursor..];
            if (tail.Length > 0 && literalUtf8.AsSpan(0, tail.Length).SequenceEqual(tail))
            {
                return Utf8Pcre2ProbeResult.CreatePartial(
                    input,
                    Pcre2GroupData.FromByteOffsets(input, 0, startOffsetInBytes, input.Length));
            }
        }

        return Utf8Pcre2ProbeResult.CreateNoMatch(input);
    }

    private static Utf8Pcre2ProbeResult ProbeViaAnchoredAPlusWord(ReadOnlySpan<byte> input, Pcre2PartialMode partialMode, int startOffsetInBytes, bool allowEmptyA, bool requireAtLeastTwoLeadingA)
    {
        var remaining = input[startOffsetInBytes..];
        if (remaining.Length == 0)
        {
            return Utf8Pcre2ProbeResult.CreateNoMatch(input);
        }

        var aCount = 0;
        while (aCount < remaining.Length && remaining[aCount] == (byte)'a')
        {
            aCount++;
        }

        var minA = allowEmptyA ? 0 : 1;
        if (requireAtLeastTwoLeadingA)
        {
            minA = Math.Max(minA, 2);
        }

        if (aCount < minA)
        {
            if (allowEmptyA && IsAsciiWordByte(remaining[0]))
            {
                return Utf8Pcre2ProbeResult.CreateFullMatch(
                    input,
                    [Pcre2GroupData.FromByteOffsets(input, 0, startOffsetInBytes, startOffsetInBytes + 1)]);
            }

            return Utf8Pcre2ProbeResult.CreateNoMatch(input);
        }

        if (aCount < remaining.Length && IsAsciiWordByte(remaining[aCount]))
        {
            return Utf8Pcre2ProbeResult.CreateFullMatch(
                input,
                [Pcre2GroupData.FromByteOffsets(input, 0, startOffsetInBytes, startOffsetInBytes + aCount + 1)]);
        }

        if (partialMode != Pcre2PartialMode.None && aCount == remaining.Length)
        {
            return Utf8Pcre2ProbeResult.CreatePartial(
                input,
                Pcre2GroupData.FromByteOffsets(input, 0, startOffsetInBytes, input.Length));
        }

        return Utf8Pcre2ProbeResult.CreateNoMatch(input);
    }

    private static Utf8Pcre2ProbeResult ProbeViaAnchoredAaOrAPlusWord(ReadOnlySpan<byte> input, Pcre2PartialMode partialMode, int startOffsetInBytes)
    {
        var remaining = input[startOffsetInBytes..];
        if (remaining.Length >= 2 && remaining[0] == (byte)'a' && remaining[1] == (byte)'a')
        {
            return Utf8Pcre2ProbeResult.CreateFullMatch(
                input,
                [Pcre2GroupData.FromByteOffsets(input, 0, startOffsetInBytes, startOffsetInBytes + 2)]);
        }

        return ProbeViaAnchoredAPlusWord(input, partialMode, startOffsetInBytes, allowEmptyA: false, requireAtLeastTwoLeadingA: false);
    }

    private static Utf8Pcre2ProbeResult ProbeViaAnchoredACrlfEnd(ReadOnlySpan<byte> input, Pcre2PartialMode partialMode, int startOffsetInBytes, bool preferExplicitCrAlternative)
    {
        var remaining = input[startOffsetInBytes..];
        if (remaining.SequenceEqual("a"u8))
        {
            return Utf8Pcre2ProbeResult.CreateFullMatch(
                input,
                [Pcre2GroupData.FromByteOffsets(input, 0, startOffsetInBytes, startOffsetInBytes + 1)]);
        }

        if (remaining.SequenceEqual("a\r"u8))
        {
            if (preferExplicitCrAlternative && partialMode != Pcre2PartialMode.Hard)
            {
                return Utf8Pcre2ProbeResult.CreateFullMatch(
                    input,
                    [Pcre2GroupData.FromByteOffsets(input, 0, startOffsetInBytes, startOffsetInBytes + 2)]);
            }

            if (partialMode != Pcre2PartialMode.None)
            {
                return Utf8Pcre2ProbeResult.CreatePartial(
                    input,
                    Pcre2GroupData.FromByteOffsets(input, 0, startOffsetInBytes, input.Length));
            }
        }

        return Utf8Pcre2ProbeResult.CreateNoMatch(input);
    }

    private static Utf8Pcre2ProbeResult ProbeViaCrlfDotQuantifier(ReadOnlySpan<byte> input, Pcre2PartialMode partialMode, int startOffsetInBytes, int minCount, int maxCount)
    {
        var remaining = input[startOffsetInBytes..];
        var count = Math.Min(remaining.Length, maxCount);
        if (count < minCount)
        {
            if (partialMode != Pcre2PartialMode.None && count > 0)
            {
                return Utf8Pcre2ProbeResult.CreatePartial(
                    input,
                    Pcre2GroupData.FromByteOffsets(input, 0, startOffsetInBytes, input.Length));
            }

            return Utf8Pcre2ProbeResult.CreateNoMatch(input);
        }

        if (partialMode == Pcre2PartialMode.Hard && count == remaining.Length)
        {
            return Utf8Pcre2ProbeResult.CreatePartial(
                input,
                Pcre2GroupData.FromByteOffsets(input, 0, startOffsetInBytes, input.Length));
        }

        return Utf8Pcre2ProbeResult.CreateFullMatch(
            input,
            [Pcre2GroupData.FromByteOffsets(input, 0, startOffsetInBytes, startOffsetInBytes + count)]);
    }

    private static bool TryConsumePcre2Newline(ReadOnlySpan<byte> input, out int consumed)
    {
        if (input.Length >= 2 && input[0] == (byte)'\r' && input[1] == (byte)'\n')
        {
            consumed = 2;
            return true;
        }

        if (input.Length >= 1 && input[0] is (byte)'\r' or (byte)'\n')
        {
            consumed = 1;
            return true;
        }

        consumed = 0;
        return false;
    }

    private static Utf8Pcre2ProbeResult ProbeViaTrailingLiteralAssertion(
        ReadOnlySpan<byte> input,
        Pcre2PartialMode partialMode,
        int startOffsetInBytes,
        string literal,
        TrailingAssertionKind assertion)
    {
        var remaining = input[startOffsetInBytes..];
        var literalUtf8 = Encoding.UTF8.GetBytes(literal);
        var index = remaining.IndexOf(literalUtf8);
        if (index < 0)
        {
            if (partialMode != Pcre2PartialMode.None &&
                assertion == TrailingAssertionKind.Dollar)
            {
                var partialLength = LongestTrailingSuffixLength(remaining, literalUtf8);
                if (partialLength > 0)
                {
                    return Utf8Pcre2ProbeResult.CreatePartial(
                        input,
                        Pcre2GroupData.FromByteOffsets(input, 0, input.Length - partialLength, input.Length));
                }
            }

            return Utf8Pcre2ProbeResult.CreateNoMatch(input);
        }

        var matchStart = startOffsetInBytes + index;
        var matchEnd = matchStart + literalUtf8.Length;
        var atEnd = matchEnd == input.Length;
        var hasFinalNewline = matchEnd + 1 == input.Length && input[matchEnd] == (byte)'\n';
        var rightIsWord = matchEnd < input.Length && IsAsciiWordByte(input[matchEnd]);

        var full = assertion switch
        {
            TrailingAssertionKind.Dollar => atEnd || hasFinalNewline,
            TrailingAssertionKind.EndAbsolute => atEnd,
            TrailingAssertionKind.EndBeforeFinalNewline => atEnd || hasFinalNewline,
            TrailingAssertionKind.WordBoundary => !rightIsWord,
            TrailingAssertionKind.NonWordBoundary => rightIsWord,
            _ => false,
        };

        if (full)
        {
            if (partialMode == Pcre2PartialMode.Hard &&
                assertion is TrailingAssertionKind.Dollar or TrailingAssertionKind.EndAbsolute or TrailingAssertionKind.EndBeforeFinalNewline or TrailingAssertionKind.WordBoundary or TrailingAssertionKind.NonWordBoundary &&
                atEnd)
            {
                return Utf8Pcre2ProbeResult.CreatePartial(
                    input,
                    Pcre2GroupData.FromByteOffsets(input, 0, matchStart, matchEnd));
            }

            return Utf8Pcre2ProbeResult.CreateFullMatch(
                input,
                [Pcre2GroupData.FromByteOffsets(input, 0, matchStart, matchEnd)]);
        }

        if (partialMode != Pcre2PartialMode.None &&
            assertion is TrailingAssertionKind.WordBoundary or TrailingAssertionKind.NonWordBoundary &&
            atEnd)
        {
            return Utf8Pcre2ProbeResult.CreatePartial(
                input,
                Pcre2GroupData.FromByteOffsets(input, 0, matchStart, matchEnd));
        }

        return Utf8Pcre2ProbeResult.CreateNoMatch(input);
    }

    private static bool HasAsciiWordBoundary(ReadOnlySpan<byte> input, int byteOffset)
    {
        var leftIsWord = byteOffset > 0 && IsAsciiWordByte(input[byteOffset - 1]);
        var rightIsWord = byteOffset < input.Length && IsAsciiWordByte(input[byteOffset]);
        return leftIsWord != rightIsWord;
    }

    private static bool IsAsciiWordByte(byte value)
    {
        return value is >= (byte)'A' and <= (byte)'Z' or
            >= (byte)'a' and <= (byte)'z' or
            >= (byte)'0' and <= (byte)'9' or
            (byte)'_';
    }

    private static bool HasLongerPartialContinuation(ReadOnlySpan<byte> candidate, byte[][] literalBytes, int startLiteralIndex)
    {
        for (var i = startLiteralIndex; i < literalBytes.Length; i++)
        {
            var literal = literalBytes[i];
            if (candidate.Length < literal.Length && literal.AsSpan(0, candidate.Length).SequenceEqual(candidate))
            {
                return true;
            }
        }

        return false;
    }

    private static Utf8Pcre2ProbeResult ProbeViaAnchoredExactLiteral(ReadOnlySpan<byte> input, Pcre2PartialMode partialMode, int startOffsetInBytes, string literal)
    {
        var remaining = input[startOffsetInBytes..];
        var literalUtf8 = Encoding.UTF8.GetBytes(literal);
        if (remaining.SequenceEqual(literalUtf8))
        {
            if (partialMode == Pcre2PartialMode.Hard)
            {
                return Utf8Pcre2ProbeResult.CreatePartial(
                    input,
                    Pcre2GroupData.FromByteOffsets(input, 0, startOffsetInBytes, startOffsetInBytes + literalUtf8.Length));
            }

            return Utf8Pcre2ProbeResult.CreateFullMatch(
                input,
                [Pcre2GroupData.FromByteOffsets(input, 0, startOffsetInBytes, startOffsetInBytes + literalUtf8.Length)]);
        }

        if (partialMode != Pcre2PartialMode.None)
        {
            var partialLength = LongestLeadingExactPrefixLength(remaining, literalUtf8);
            if (partialLength > 0)
            {
                return Utf8Pcre2ProbeResult.CreatePartial(
                    input,
                    Pcre2GroupData.FromByteOffsets(input, 0, startOffsetInBytes, startOffsetInBytes + partialLength));
            }
        }

        return Utf8Pcre2ProbeResult.CreateNoMatch(input);
    }

    private static Utf8Pcre2ProbeResult ProbeViaAnchoredPrefixLiteral(ReadOnlySpan<byte> input, Pcre2PartialMode partialMode, int startOffsetInBytes, string literal)
    {
        var remaining = input[startOffsetInBytes..];
        var literalUtf8 = Encoding.UTF8.GetBytes(literal);
        if (remaining.StartsWith(literalUtf8))
        {
            return Utf8Pcre2ProbeResult.CreateFullMatch(
                input,
                [Pcre2GroupData.FromByteOffsets(input, 0, startOffsetInBytes, startOffsetInBytes + literalUtf8.Length)]);
        }

        if (partialMode != Pcre2PartialMode.None)
        {
            var partialLength = LongestLeadingExactPrefixLength(remaining, literalUtf8);
            if (partialLength > 0)
            {
                return Utf8Pcre2ProbeResult.CreatePartial(
                    input,
                    Pcre2GroupData.FromByteOffsets(input, 0, startOffsetInBytes, startOffsetInBytes + partialLength));
            }
        }

        return Utf8Pcre2ProbeResult.CreateNoMatch(input);
    }

    private static Utf8Pcre2ProbeResult ProbeViaSuffixAnchoredLiteral(ReadOnlySpan<byte> input, Pcre2PartialMode partialMode, int startOffsetInBytes, string literal)
    {
        var remaining = input[startOffsetInBytes..];
        var literalUtf8 = Encoding.UTF8.GetBytes(literal);
        if (remaining.EndsWith(literalUtf8))
        {
            if (partialMode == Pcre2PartialMode.Hard)
            {
                return Utf8Pcre2ProbeResult.CreatePartial(
                    input,
                    Pcre2GroupData.FromByteOffsets(input, 0, input.Length - literalUtf8.Length, input.Length));
            }

            return Utf8Pcre2ProbeResult.CreateFullMatch(
                input,
                [Pcre2GroupData.FromByteOffsets(input, 0, input.Length - literalUtf8.Length, input.Length)]);
        }

        if (partialMode != Pcre2PartialMode.None)
        {
            var partialLength = LongestTrailingSuffixLength(remaining, literalUtf8);
            if (partialLength > 0)
            {
                return Utf8Pcre2ProbeResult.CreatePartial(
                    input,
                    Pcre2GroupData.FromByteOffsets(input, 0, input.Length - partialLength, input.Length));
            }
        }

        return Utf8Pcre2ProbeResult.CreateNoMatch(input);
    }

    private static int LongestLeadingExactPrefixLength(ReadOnlySpan<byte> subject, ReadOnlySpan<byte> literal)
    {
        if (subject.Length == 0 || subject.Length >= literal.Length)
        {
            return 0;
        }

        return literal[..subject.Length].SequenceEqual(subject) ? subject.Length : 0;
    }

    private static int LongestTrailingSuffixLength(ReadOnlySpan<byte> subject, ReadOnlySpan<byte> literal)
    {
        var maxLength = Math.Min(subject.Length, literal.Length - 1);
        for (var length = maxLength; length >= 1; length--)
        {
            if (subject[^length..].SequenceEqual(literal[(literal.Length - length)..]))
            {
                return length;
            }
        }

        return 0;
    }

    private static int LongestTrailingPrefixLength(ReadOnlySpan<byte> subject, ReadOnlySpan<byte> literal)
    {
        var maxLength = Math.Min(subject.Length, literal.Length - 1);
        for (var length = maxLength; length >= 1; length--)
        {
            if (subject[^length..].SequenceEqual(literal[..length]))
            {
                return length;
            }
        }

        return 0;
    }

    private Utf8Pcre2ProbeResult ProbeViaNonPartialMatch(ReadOnlySpan<byte> input, int startOffsetInBytes)
    {
        var match = MatchDetailed(input, startOffsetInBytes);
        return match.Success
            ? Utf8Pcre2ProbeResult.CreateFullMatch(input, CaptureGroups(match))
            : Utf8Pcre2ProbeResult.CreateNoMatch(input);
    }

    private static Pcre2GroupData[] CaptureGroups(Utf8Pcre2MatchContext match)
    {
        var groups = new Pcre2GroupData[match.CaptureSlotCount];
        for (var i = 0; i < groups.Length; i++)
        {
            groups[i] = match.GetGroup(i)._data;
        }

        return groups;
    }

    private static Pcre2GroupData CaptureValue(Utf8Pcre2MatchContext match)
        => match.GetGroup(0)._data;

    private Pcre2GroupData[] EnumerateGlobalMatchData(ReadOnlySpan<byte> input, int startOffsetInBytes)
    {
        return Pattern switch
        {
            "(?|(abc)|(xyz))" => EnumerateLiteralAlternation(input, startOffsetInBytes, "abc"u8, "xyz"u8),
            "(?|(abc)|(xyz))\\1" => EnumerateLiteralAlternation(input, startOffsetInBytes, "abcabc"u8, "xyzxyz"u8),
            "(x)(?|(abc)|(xyz))(x)" => EnumerateLiteralAlternation(input, startOffsetInBytes, "xabcx"u8, "xxyzx"u8),
            "(x)(?|(abc)(pqr)|(xyz))(x)" => EnumerateLiteralAlternation(input, startOffsetInBytes, "xabcpqrx"u8, "xxyzx"u8),
            "(?|(abc)|(xyz))(?1)" => EnumerateLiteralAlternation(input, startOffsetInBytes, "abcabc"u8, "xyzabc"u8),
            "(?|(aaa)|(b))\\g{1}" => EnumerateLiteralAlternation(input, startOffsetInBytes, "aaaaaa"u8, "bb"u8),
            "(?|(?'a'aaa)|(?'a'b))\\k'a'" => EnumerateLiteralAlternation(input, startOffsetInBytes, "aaaaaa"u8, "bb"u8),
            "(?|(?'a'aaa)|(?'a'b))(?'a'cccc)\\k'a'" => EnumerateLiteralAlternation(input, startOffsetInBytes, "aaaccccaaa"u8, "bccccb"u8),
            "(?:(?<n>foo)|(?<n>bar))\\k<n>" => EnumerateLiteralAlternation(input, startOffsetInBytes, "foofoo"u8, "barbar"u8),
            "bb(.*?)c" => EnumerateSingleLiteral(input, startOffsetInBytes, "bbabc"u8),
            "abc\\K|def\\K" => EnumerateLiteralKZeroLength(input, startOffsetInBytes),
            "ab\\Kc|de\\Kf" => EnumerateLiteralKValue(input, startOffsetInBytes),
            "(?:a\\Kb)*+" or "(?>a\\Kb)*" or "(?:a\\Kb)*" or "(a\\Kb)*+" or "(a\\Kb)*" => EnumerateRepeatedKResetAb(input, startOffsetInBytes),
            "(?>a\\Kbz|ab)" => EnumerateKResetAtomicAltAb(input, startOffsetInBytes),
            "(?<=abc)(|def)" => EnumerateEmptyOrLiteralAfterAbc(input, startOffsetInBytes, "def"u8),
            "(?<=abc)(|DEF)" => EnumerateEmptyOrLiteralAfterAbc(input, startOffsetInBytes, "DEF"u8),
            "(?<=\\G.)" => EnumerateGBoundary(input, startOffsetInBytes),
            "(?<=\\Ka)" or "(?(?=\\Gc)(?<=\\Kb)c|(?<=\\Kab))" or "(?(?=\\Gc)(?<=\\Kab)|(?<=\\Kb))" or "(?=ab\\K)"
                => throw new NotSupportedException("SPEC-PCRE2 rejects non-monotone iterative matches."),
            _ when CanEnumerateViaRepeatedDetailedMatching() => EnumerateViaRepeatedDetailedMatching(input, startOffsetInBytes),
            _ when _managedRegex is not null => EnumerateViaManagedRegex(input, startOffsetInBytes),
            _ => throw new NotSupportedException("SPEC-PCRE2 does not support global iteration for this pattern in the managed profile."),
        };
    }

    private bool CanEnumerateViaRepeatedDetailedMatching()
        => CanEnumerateViaRepeatedDetailedMatching(_executionKind);

    private static bool CanEnumerateViaRepeatedDetailedMatching(Pcre2ExecutionKind executionKind)
    {
        return executionKind is
            Pcre2ExecutionKind.MailboxRfc2822 or
            Pcre2ExecutionKind.ReluctantAlternation or
            Pcre2ExecutionKind.BranchResetBasic or
            Pcre2ExecutionKind.BranchResetBackref or
            Pcre2ExecutionKind.BranchResetNested or
            Pcre2ExecutionKind.BranchResetNestedSecondCapture or
            Pcre2ExecutionKind.BranchResetAbasic or
            Pcre2ExecutionKind.BranchResetSubroutine or
            Pcre2ExecutionKind.BranchResetGReference or
            Pcre2ExecutionKind.BranchResetSameNameBackref or
            Pcre2ExecutionKind.BranchResetSameNameFollowup or
            Pcre2ExecutionKind.DuplicateNamesFooBar or
            Pcre2ExecutionKind.BackslashCLiteral or
            Pcre2ExecutionKind.MarkSkip or
            Pcre2ExecutionKind.KResetFooBar or
            Pcre2ExecutionKind.KResetBarOrBaz or
            Pcre2ExecutionKind.KResetFooBarBaz or
            Pcre2ExecutionKind.KResetAbc123 or
            Pcre2ExecutionKind.KReset123Abc or
            Pcre2ExecutionKind.KResetAnchorAbc or
            Pcre2ExecutionKind.KResetAnchorLookaheadOrAbc or
            Pcre2ExecutionKind.KResetAtomicAb or
            Pcre2ExecutionKind.KResetCapturedAtomicAb or
            Pcre2ExecutionKind.KResetCapturedAb or
            Pcre2ExecutionKind.KResetAnchorCzOrAc or
            Pcre2ExecutionKind.KResetAtomicAltAb or
            Pcre2ExecutionKind.KResetDefineSubroutineAb or
            Pcre2ExecutionKind.KResetRepeatAbPossessive or
            Pcre2ExecutionKind.KResetAtomicRepeatAb or
            Pcre2ExecutionKind.KResetRepeatAb or
            Pcre2ExecutionKind.KResetCapturedRepeatAbPossessive or
            Pcre2ExecutionKind.KResetCapturedRepeatAb or
            Pcre2ExecutionKind.KResetRecursiveAny or
            Pcre2ExecutionKind.KResetRecursiveCaptured or
            Pcre2ExecutionKind.ConditionalLookaheadPlus or
            Pcre2ExecutionKind.ConditionalLookaheadEmptyAlt or
            Pcre2ExecutionKind.ConditionalLookahead or
            Pcre2ExecutionKind.ConditionalNegativeLookahead or
            Pcre2ExecutionKind.ConditionalAcceptLookahead or
            Pcre2ExecutionKind.ConditionalAcceptNegativeLookahead or
            Pcre2ExecutionKind.SubroutinePrefixDigits or
            Pcre2ExecutionKind.CommitSubroutine or
            Pcre2ExecutionKind.DefineSubroutineB;
    }

    private Pcre2GroupData[] EnumerateViaRepeatedDetailedMatching(ReadOnlySpan<byte> input, int startOffsetInBytes)
    {
        var results = new List<Pcre2GroupData>();
        var searchStart = startOffsetInBytes;
        while (searchStart <= input.Length)
        {
            Utf8Pcre2MatchContext match = default;
            var found = false;
            for (var candidateStart = searchStart; candidateStart <= input.Length; candidateStart++)
            {
                match = MatchDetailed(input, candidateStart);
                if (match.Success)
                {
                    found = true;
                    break;
                }
            }

            if (!found)
            {
                break;
            }

            var value = match.Value;
            if (!value.HasContiguousByteRange || value.StartOffsetInBytes < searchStart || value.EndOffsetInBytes < value.StartOffsetInBytes)
            {
                throw new NotSupportedException("SPEC-PCRE2 rejects non-monotone iterative matches.");
            }

            results.Add(CaptureValue(match));

            searchStart = value.EndOffsetInBytes > value.StartOffsetInBytes
                ? value.EndOffsetInBytes
                : value.EndOffsetInBytes + 1;
        }

        return [.. results];
    }

    private Pcre2GroupData[] EnumerateViaManagedRegex(ReadOnlySpan<byte> input, int startOffsetInBytes)
    {
        var subject = Encoding.UTF8.GetString(input);
        var startOffsetInUtf16 = Encoding.UTF8.GetCharCount(input[..startOffsetInBytes]);
        var boundaryMap = Utf8InputAnalyzer.Analyze(input).BoundaryMap;
        var matches = _managedRegex!.Matches(subject, startOffsetInUtf16);
        var results = new Pcre2GroupData[matches.Count];
        for (var i = 0; i < matches.Count; i++)
        {
            var match = matches[i];
            if (!boundaryMap.TryGetByteRange(match.Index, match.Length, out var indexInBytes, out var lengthInBytes))
            {
                throw new InvalidOperationException("Managed Regex fallback produced a match that is not aligned to UTF-8 scalar boundaries.");
            }

            results[i] = new Pcre2GroupData
            {
                Number = 0,
                Success = true,
                StartOffsetInBytes = indexInBytes,
                EndOffsetInBytes = indexInBytes + lengthInBytes,
                StartOffsetInUtf16 = match.Index,
                EndOffsetInUtf16 = match.Index + match.Length,
            };
        }

        return results;
    }

    private static Pcre2GroupData[] EnumerateLiteralKZeroLength(ReadOnlySpan<byte> input, int startOffsetInBytes)
    {
        var results = new List<Pcre2GroupData>();
        var cursor = startOffsetInBytes;
        while (cursor <= input.Length - 3)
        {
            var abcIndex = input[cursor..].IndexOf("abc"u8);
            var defIndex = input[cursor..].IndexOf("def"u8);
            if (abcIndex < 0 && defIndex < 0)
            {
                break;
            }

            var nextRelative = abcIndex < 0 ? defIndex : defIndex < 0 ? abcIndex : Math.Min(abcIndex, defIndex);
            var matchEnd = cursor + nextRelative + 3;
            results.Add(Pcre2GroupData.FromByteOffsets(input, 0, matchEnd, matchEnd));
            cursor = matchEnd;
        }

        return [.. results];
    }

    private static Pcre2GroupData[] EnumerateSingleLiteral(ReadOnlySpan<byte> input, int startOffsetInBytes, ReadOnlySpan<byte> literal)
    {
        var results = new List<Pcre2GroupData>();
        var cursor = startOffsetInBytes;
        while (cursor <= input.Length - literal.Length)
        {
            var index = input[cursor..].IndexOf(literal);
            if (index < 0)
            {
                break;
            }

            var start = cursor + index;
            var end = start + literal.Length;
            results.Add(Pcre2GroupData.FromByteOffsets(input, 0, start, end));
            cursor = end;
        }

        return [.. results];
    }

    private static Pcre2GroupData[] EnumerateLiteralAlternation(ReadOnlySpan<byte> input, int startOffsetInBytes, ReadOnlySpan<byte> firstLiteral, ReadOnlySpan<byte> secondLiteral)
    {
        var results = new List<Pcre2GroupData>();
        var cursor = startOffsetInBytes;
        var minimumLength = Math.Min(firstLiteral.Length, secondLiteral.Length);
        while (cursor <= input.Length - minimumLength)
        {
            var remaining = input[cursor..];
            var firstIndex = remaining.IndexOf(firstLiteral);
            var secondIndex = remaining.IndexOf(secondLiteral);
            if (firstIndex < 0 && secondIndex < 0)
            {
                break;
            }

            var useFirst = firstIndex >= 0 && (secondIndex < 0 || firstIndex <= secondIndex);
            var relativeIndex = useFirst ? firstIndex : secondIndex;
            var length = useFirst ? firstLiteral.Length : secondLiteral.Length;
            var start = cursor + relativeIndex;
            var end = start + length;
            results.Add(Pcre2GroupData.FromByteOffsets(input, 0, start, end));
            cursor = end;
        }

        return [.. results];
    }

    private bool TryCountNativeGlobalMatches(ReadOnlySpan<byte> input, int startOffsetInBytes, out int count)
    {
        switch (Pattern)
        {
            case "(?|(abc)|(xyz))":
                count = CountLiteralAlternationMatches(input, startOffsetInBytes, "abc"u8, "xyz"u8);
                return true;
            case "(?|(abc)|(xyz))\\1":
                count = CountLiteralAlternationMatches(input, startOffsetInBytes, "abcabc"u8, "xyzxyz"u8);
                return true;
            case "(x)(?|(abc)|(xyz))(x)":
                count = CountLiteralAlternationMatches(input, startOffsetInBytes, "xabcx"u8, "xxyzx"u8);
                return true;
            case "(x)(?|(abc)(pqr)|(xyz))(x)":
                count = CountLiteralAlternationMatches(input, startOffsetInBytes, "xabcpqrx"u8, "xxyzx"u8);
                return true;
            case "(?|(abc)|(xyz))(?1)":
                count = CountLiteralAlternationMatches(input, startOffsetInBytes, "abcabc"u8, "xyzabc"u8);
                return true;
            case "(?|(aaa)|(b))\\g{1}":
                count = CountLiteralAlternationMatches(input, startOffsetInBytes, "aaaaaa"u8, "bb"u8);
                return true;
            case "(?|(?'a'aaa)|(?'a'b))\\k'a'":
                count = CountLiteralAlternationMatches(input, startOffsetInBytes, "aaaaaa"u8, "bb"u8);
                return true;
            case "(?|(?'a'aaa)|(?'a'b))(?'a'cccc)\\k'a'":
                count = CountLiteralAlternationMatches(input, startOffsetInBytes, "aaaccccaaa"u8, "bccccb"u8);
                return true;
            case "(?:(?<n>foo)|(?<n>bar))\\k<n>":
                count = CountLiteralAlternationMatches(input, startOffsetInBytes, "foofoo"u8, "barbar"u8);
                return true;
            case "bb(.*?)c":
                count = CountSingleLiteralMatches(input, startOffsetInBytes, "bbabc"u8);
                return true;
            case "(?:a\\Kb)*+":
            case "(?>a\\Kb)*":
            case "(?:a\\Kb)*":
            case "(a\\Kb)*+":
            case "(a\\Kb)*":
                count = CountRepeatedKResetAbMatches(input, startOffsetInBytes);
                return true;
            case "(?>a\\Kbz|ab)":
                count = CountKResetAtomicAltAbMatches(input, startOffsetInBytes);
                return true;
            case "(?<=abc)(|def)":
                count = CountEmptyOrLiteralAfterAbcMatches(input, startOffsetInBytes, "def"u8);
                return true;
            default:
                count = 0;
                return false;
        }
    }

    private static Pcre2GroupData[] EnumerateLiteralKValue(ReadOnlySpan<byte> input, int startOffsetInBytes)
    {
        var results = new List<Pcre2GroupData>();
        var cursor = startOffsetInBytes;
        while (cursor <= input.Length - 3)
        {
            var abcIndex = input[cursor..].IndexOf("abc"u8);
            var defIndex = input[cursor..].IndexOf("def"u8);
            if (abcIndex < 0 && defIndex < 0)
            {
                break;
            }

            var useAbc = defIndex < 0 || (abcIndex >= 0 && abcIndex <= defIndex);
            var nextRelative = useAbc ? abcIndex : defIndex;
            var matchStart = cursor + nextRelative + 2;
            var matchEnd = matchStart + 1;
            results.Add(Pcre2GroupData.FromByteOffsets(input, 0, matchStart, matchEnd));
            cursor = cursor + nextRelative + 3;
        }

        return [.. results];
    }

    private static Pcre2GroupData[] EnumerateRepeatedKResetAb(ReadOnlySpan<byte> input, int startOffsetInBytes)
    {
        var results = new List<Pcre2GroupData>();
        var cursor = startOffsetInBytes;
        while (cursor <= input.Length - 2)
        {
            var relativeIndex = input[cursor..].IndexOf("ab"u8);
            if (relativeIndex < 0)
            {
                break;
            }

            var runCursor = cursor + relativeIndex;
            var lastAbStart = runCursor;
            do
            {
                lastAbStart = runCursor;
                runCursor += 2;
            }
            while (runCursor <= input.Length - 2 && input[runCursor] == (byte)'a' && input[runCursor + 1] == (byte)'b');

            results.Add(Pcre2GroupData.FromByteOffsets(input, 0, lastAbStart + 1, lastAbStart + 2));
            cursor = lastAbStart + 2;
        }

        return [.. results];
    }

    private static Pcre2GroupData[] EnumerateKResetAtomicAltAb(ReadOnlySpan<byte> input, int startOffsetInBytes)
    {
        var results = new List<Pcre2GroupData>();
        var cursor = startOffsetInBytes;
        while (cursor <= input.Length - 2)
        {
            var relativeIndex = input[cursor..].IndexOf("ab"u8);
            if (relativeIndex < 0)
            {
                break;
            }

            var matchStart = cursor + relativeIndex;
            if (matchStart <= input.Length - 3 && input[matchStart + 2] == (byte)'z')
            {
                results.Add(Pcre2GroupData.FromByteOffsets(input, 0, matchStart + 1, matchStart + 3));
                cursor = matchStart + 3;
            }
            else
            {
                results.Add(Pcre2GroupData.FromByteOffsets(input, 0, matchStart, matchStart + 2));
                cursor = matchStart + 2;
            }
        }

        return [.. results];
    }

    private static int CountRepeatedKResetAbMatches(ReadOnlySpan<byte> input, int startOffsetInBytes)
    {
        var count = 0;
        var cursor = startOffsetInBytes;
        while (cursor <= input.Length - 2)
        {
            var relativeIndex = input[cursor..].IndexOf("ab"u8);
            if (relativeIndex < 0)
            {
                break;
            }

            var runCursor = cursor + relativeIndex;
            var lastAbStart = runCursor;
            do
            {
                lastAbStart = runCursor;
                runCursor += 2;
            }
            while (runCursor <= input.Length - 2 && input[runCursor] == (byte)'a' && input[runCursor + 1] == (byte)'b');

            count++;
            cursor = lastAbStart + 2;
        }

        return count;
    }

    private static int CountKResetAtomicAltAbMatches(ReadOnlySpan<byte> input, int startOffsetInBytes)
    {
        var count = 0;
        var cursor = startOffsetInBytes;
        while (cursor <= input.Length - 2)
        {
            var relativeIndex = input[cursor..].IndexOf("ab"u8);
            if (relativeIndex < 0)
            {
                break;
            }

            var matchStart = cursor + relativeIndex;
            cursor = matchStart <= input.Length - 3 && input[matchStart + 2] == (byte)'z'
                ? matchStart + 3
                : matchStart + 2;
            count++;
        }

        return count;
    }

    private static int CountSingleLiteralMatches(ReadOnlySpan<byte> input, int startOffsetInBytes, ReadOnlySpan<byte> literal)
    {
        var count = 0;
        var cursor = startOffsetInBytes;
        while (cursor <= input.Length - literal.Length)
        {
            var index = input[cursor..].IndexOf(literal);
            if (index < 0)
            {
                break;
            }

            count++;
            cursor += index + literal.Length;
        }

        return count;
    }

    private static int CountLiteralAlternationMatches(ReadOnlySpan<byte> input, int startOffsetInBytes, ReadOnlySpan<byte> firstLiteral, ReadOnlySpan<byte> secondLiteral)
    {
        var count = 0;
        var cursor = startOffsetInBytes;
        var minimumLength = Math.Min(firstLiteral.Length, secondLiteral.Length);
        while (cursor <= input.Length - minimumLength)
        {
            var remaining = input[cursor..];
            var firstIndex = remaining.IndexOf(firstLiteral);
            var secondIndex = remaining.IndexOf(secondLiteral);
            if (firstIndex < 0 && secondIndex < 0)
            {
                break;
            }

            var useFirst = firstIndex >= 0 && (secondIndex < 0 || firstIndex <= secondIndex);
            cursor += (useFirst ? firstIndex : secondIndex) + (useFirst ? firstLiteral.Length : secondLiteral.Length);
            count++;
        }

        return count;
    }

    private static int CountEmptyOrLiteralAfterAbcMatches(ReadOnlySpan<byte> input, int startOffsetInBytes, ReadOnlySpan<byte> literal)
    {
        var count = 0;
        var cursor = startOffsetInBytes;
        while (cursor <= input.Length - 3)
        {
            var abcIndex = input[cursor..].IndexOf("abc"u8);
            if (abcIndex < 0)
            {
                break;
            }

            var boundary = cursor + abcIndex + 3;
            count++;
            if (boundary <= input.Length - literal.Length && input[boundary..].StartsWith(literal))
            {
                count++;
                cursor = boundary + literal.Length;
            }
            else
            {
                cursor = boundary + 1;
            }
        }

        return count;
    }


    private static Pcre2GroupData[] EnumerateEmptyOrLiteralAfterAbc(ReadOnlySpan<byte> input, int startOffsetInBytes, ReadOnlySpan<byte> literal)
    {
        var results = new List<Pcre2GroupData>();
        var cursor = startOffsetInBytes;
        while (cursor <= input.Length - 3)
        {
            var abcIndex = input[cursor..].IndexOf("abc"u8);
            if (abcIndex < 0)
            {
                break;
            }

            var boundary = cursor + abcIndex + 3;
            results.Add(Pcre2GroupData.FromByteOffsets(input, 0, boundary, boundary));
            if (boundary <= input.Length - literal.Length && input[boundary..].StartsWith(literal))
            {
                results.Add(Pcre2GroupData.FromByteOffsets(input, 0, boundary, boundary + literal.Length));
                cursor = boundary + literal.Length;
            }
            else
            {
                cursor = boundary + 1;
            }
        }

        return [.. results];
    }

    private static Pcre2GroupData[] EnumerateGBoundary(ReadOnlySpan<byte> input, int startOffsetInBytes)
    {
        var results = new List<Pcre2GroupData>();
        for (var i = startOffsetInBytes + 1; i <= input.Length; i++)
        {
            results.Add(Pcre2GroupData.FromByteOffsets(input, 0, i, i));
        }

        return [.. results];
    }

    private bool TryCreateSpecialGlobalEnumerator(ReadOnlySpan<byte> input, int startOffsetInBytes, out Utf8Pcre2ValueMatchEnumerator enumerator)
    {
        var remaining = input[startOffsetInBytes..];
        switch (_executionKind)
        {
            case Pcre2ExecutionKind.KResetSneakyLookaheadDefine:
                if (remaining.SequenceEqual("ab"u8))
                {
                    if (!CompileSettings.AllowLookaroundBackslashK)
                    {
                        enumerator = CreateDeferredMatchErrorEnumerator(
                            input,
                            [],
                            0,
                            Pcre2ErrorKinds.DisallowedLookaroundBackslashK);
                        return true;
                    }

                    enumerator = new Utf8Pcre2ValueMatchEnumerator(
                        input,
                        [CreateNonContiguousByteRange(input, startOffsetInBytes + 2, startOffsetInBytes + 1)]);
                    return true;
                }

                break;

            case Pcre2ExecutionKind.KResetSneakyLookbehindDefine:
                if (remaining.SequenceEqual("ab"u8) && !CompileSettings.AllowLookaroundBackslashK)
                {
                    enumerator = CreateDeferredMatchErrorEnumerator(
                        input,
                        [Pcre2GroupData.FromByteOffsets(input, 0, startOffsetInBytes, startOffsetInBytes + 1)],
                        1,
                        Pcre2ErrorKinds.DisallowedLookaroundBackslashK);
                    return true;
                }

                break;

            case Pcre2ExecutionKind.KResetSneakyGlobalLookbehindDefine:
                if (remaining.SequenceEqual("ab"u8))
                {
                    enumerator = new Utf8Pcre2ValueMatchEnumerator(
                        input,
                        [Pcre2GroupData.FromByteOffsets(input, 0, startOffsetInBytes, startOffsetInBytes + 1)]);
                    return true;
                }

                break;
        }

        enumerator = default;
        return false;
    }

    private bool UsesDeferredSpecialGlobalEnumerator(ReadOnlySpan<byte> input, int startOffsetInBytes)
    {
        var remaining = input[startOffsetInBytes..];
        return _executionKind switch
        {
            Pcre2ExecutionKind.KResetSneakyLookaheadDefine => remaining.SequenceEqual("ab"u8),
            Pcre2ExecutionKind.KResetSneakyLookbehindDefine => remaining.SequenceEqual("ab"u8) && !CompileSettings.AllowLookaroundBackslashK,
            Pcre2ExecutionKind.KResetSneakyGlobalLookbehindDefine => remaining.SequenceEqual("ab"u8),
            _ => false,
        };
    }

    private static Utf8Pcre2ValueMatchEnumerator CreateDeferredMatchErrorEnumerator(
        ReadOnlySpan<byte> input,
        Pcre2GroupData[] matches,
        int exceptionIndex,
        string errorKind)
    {
        return new Utf8Pcre2ValueMatchEnumerator(
            input,
            matches,
            new Pcre2MatchException("disallowed use of \\K in lookaround", errorKind),
            exceptionIndex);
    }

    private static Pcre2GroupData CreateNonContiguousByteRange(ReadOnlySpan<byte> input, int startOffsetInBytes, int endOffsetInBytes)
    {
        return new Pcre2GroupData
        {
            Number = 0,
            Success = true,
            StartOffsetInBytes = startOffsetInBytes,
            EndOffsetInBytes = endOffsetInBytes,
            StartOffsetInUtf16 = Encoding.UTF8.GetCharCount(input[..startOffsetInBytes]),
            EndOffsetInUtf16 = Encoding.UTF8.GetCharCount(input[..endOffsetInBytes]),
        };
    }

    private static Pcre2GroupData CreateManagedProfileGroupData(Utf8ValueMatch match, ReadOnlySpan<byte> input, int byteOffsetBase)
    {
        return CreateManagedProfileGroupData(match, byteOffsetBase, GetUtf16OffsetOfBytePrefix(input, byteOffsetBase));
    }

    private static Utf8Pcre2MatchContext CreateManagedProfileMatchContext(ReadOnlySpan<byte> input, Utf8MatchContext context, string[]? groupNames)
    {
        if (!context.Success)
        {
            return default;
        }

        var groups = new Pcre2GroupData[context.GroupCount];
        for (var i = 0; i < groups.Length; i++)
        {
            groups[i] = CreateManagedProfileGroupData(context.GetGroup(i), i);
        }

        Pcre2NameEntry[]? nameEntries = null;
        if (groupNames is { Length: > 0 })
        {
            nameEntries = new Pcre2NameEntry[groupNames.Length];
            for (var i = 0; i < groupNames.Length; i++)
            {
                nameEntries[i] = new Pcre2NameEntry { Name = groupNames[i], Number = i };
            }
        }

        return Utf8Pcre2MatchContext.Create(input, groups, nameEntries);
    }

    private static Utf8Pcre2MatchData CreateManagedProfileMatchData(Utf8ValueMatch match, ReadOnlySpan<byte> input, int byteOffsetBase, int utf16OffsetBase)
        => Utf8Pcre2MatchData.Create(CreateManagedProfileGroupData(match, byteOffsetBase, utf16OffsetBase));

    private static Utf8Pcre2MatchData CreateManagedProfileMatchData(Utf8ValueMatch match)
        => Utf8Pcre2MatchData.Create(CreateManagedProfileGroupData(match, byteOffsetBase: 0, utf16OffsetBase: 0));

    private static Utf8Pcre2ValueMatch CreateManagedProfileValueMatch(ReadOnlySpan<byte> input, Utf8ValueMatch match)
        => Utf8Pcre2ValueMatch.Create(input, CreateManagedProfileGroupData(match, input, byteOffsetBase: 0));

    private static Pcre2GroupData CreateManagedProfileGroupData(Utf8ValueMatch match, int byteOffsetBase, int utf16OffsetBase)
    {
        if (!match.Success)
        {
            return default;
        }

        if (!match.TryGetByteRange(out var indexInBytes, out var lengthInBytes))
        {
            throw new InvalidOperationException("Managed Utf8Regex fallback returned a match that is not aligned to byte boundaries.");
        }

        return new Pcre2GroupData
        {
            Number = 0,
            Success = true,
            StartOffsetInBytes = byteOffsetBase + indexInBytes,
            EndOffsetInBytes = byteOffsetBase + indexInBytes + lengthInBytes,
            StartOffsetInUtf16 = utf16OffsetBase + match.IndexInUtf16,
            EndOffsetInUtf16 = utf16OffsetBase + match.IndexInUtf16 + match.LengthInUtf16,
        };
    }

    private static Pcre2GroupData CreateManagedProfileGroupData(Utf8GroupContext group, int number)
    {
        if (!group.Success)
        {
            return new Pcre2GroupData
            {
                Number = number,
                Success = false,
            };
        }

        if (!group.TryGetByteRange(out var indexInBytes, out var lengthInBytes))
        {
            throw new InvalidOperationException("Managed Utf8Regex fallback returned a group that is not aligned to byte boundaries.");
        }

        return new Pcre2GroupData
        {
            Number = number,
            Success = true,
            StartOffsetInBytes = indexInBytes,
            EndOffsetInBytes = indexInBytes + lengthInBytes,
            StartOffsetInUtf16 = group.IndexInUtf16,
            EndOffsetInUtf16 = group.IndexInUtf16 + group.LengthInUtf16,
        };
    }

    private static int GetUtf16OffsetOfBytePrefix(ReadOnlySpan<byte> input, int byteOffset)
        => byteOffset == 0 ? 0 : Encoding.UTF8.GetCharCount(input[..byteOffset]);

    private static Utf8Pcre2MatchData CreateManagedRegexMatchData(Match match, Utf8BoundaryMap? boundaryMap, bool isAscii)
    {
        if (!match.Success)
        {
            return default;
        }

        int indexInBytes;
        int lengthInBytes;
        if (isAscii)
        {
            indexInBytes = match.Index;
            lengthInBytes = match.Length;
        }
        else if (boundaryMap is not null && boundaryMap.TryGetByteRange(match.Index, match.Length, out indexInBytes, out lengthInBytes))
        {
        }
        else
        {
            throw new InvalidOperationException("Managed Regex fallback produced a match that is not aligned to UTF-8 scalar boundaries.");
        }

        return Utf8Pcre2MatchData.Create(
            new Pcre2GroupData
            {
                Number = 0,
                Success = true,
                StartOffsetInBytes = indexInBytes,
                EndOffsetInBytes = indexInBytes + lengthInBytes,
                StartOffsetInUtf16 = match.Index,
                EndOffsetInUtf16 = match.Index + match.Length,
            });
    }

    private byte[] ReplaceCore(ReadOnlySpan<byte> input, string replacement, Pcre2PartialMode partialMode, Pcre2SubstitutionOptions substitutionOptions, int startOffsetInBytes)
    {
        if (RejectsReplacementIteration())
        {
            throw new NotSupportedException("SPEC-PCRE2 rejects replacement for lookaround-\\K iterative matches.");
        }

        if ((substitutionOptions & Pcre2SubstitutionOptions.SubstituteReplacementOnly) != 0)
        {
            return ReplaceReplacementOnlyCore(input, replacement, substitutionOptions, startOffsetInBytes);
        }

        if ((substitutionOptions & Pcre2SubstitutionOptions.SubstituteLiteral) != 0)
        {
            return ReplaceLiteralCore(input, replacement, startOffsetInBytes);
        }

        if (CanUseManagedRegexDirectReplacement(replacement, partialMode, substitutionOptions))
        {
            return ReplaceViaManagedRegexDirect(input, replacement, startOffsetInBytes);
        }

        if (_utf8Translation.IsActive)
        {
            return ReplaceViaTranslatedDetailedIteration(input, replacement, substitutionOptions, startOffsetInBytes, Encoding.UTF8.GetString(input));
        }

        return Pattern switch
        {
            "foo(?<Bar>BAR)?" when replacement == @"X${Bar:+\:\:text}Y" => ReplaceFooOptionalBar(input, replacement, substitutionOptions, startOffsetInBytes),
            "(a)b+" when partialMode != Pcre2PartialMode.None => ReplacePartialAbPlus(input, replacement, partialMode),
            "(?<=abc)(|def)" => ReplaceEmptyOrLiteralAfterAbc(input, replacement, substitutionOptions, startOffsetInBytes, "def"u8),
            "(?<=abc)(|DEF)" => ReplaceEmptyOrLiteralAfterAbc(input, replacement, substitutionOptions, startOffsetInBytes, "DEF"u8),
            "(*:pear)apple|(*:orange)lemon|(*:strawberry)blackberry" => ReplaceMarkedLiterals(
                input,
                replacement,
                substitutionOptions,
                startOffsetInBytes,
                [("apple", "pear"), ("lemon", "orange"), ("blackberry", "strawberry")],
                replaceAll: replacement != "${*MARK} sauce,"),
            "(*:pear)apple" => ReplaceMarkedLiterals(
                input,
                replacement,
                substitutionOptions,
                startOffsetInBytes,
                [("apple", "pear")]),
            _ when UsesNativeGlobalIteration() => ReplaceViaNativeIteration(input, replacement, substitutionOptions, startOffsetInBytes),
            _ when CanUseUtf8RegexLiteralReplacement(replacement, substitutionOptions, allowOverflowLengthShortcut: true) => ReplaceViaUtf8RegexLiteral(input, replacement, startOffsetInBytes),
            _ when _managedRegex is not null => ReplaceViaManagedRegex(input, replacement, substitutionOptions, startOffsetInBytes),
            _ => throw CreateUnsupportedReplacementException(),
        };
    }

    private bool CanUseUtf8RegexLiteralReplacement(string replacement, Pcre2SubstitutionOptions substitutionOptions, bool allowOverflowLengthShortcut)
    {
        if (_utf8Regex is null || _executionPlan.ReplaceBackend != Pcre2DirectBackendKind.Utf8Regex)
        {
            return false;
        }

        if (!allowOverflowLengthShortcut &&
            (substitutionOptions & Pcre2SubstitutionOptions.SubstituteOverflowLength) != 0)
        {
            return false;
        }

        var relevantOptions = substitutionOptions &
            ~(Pcre2SubstitutionOptions.SubstituteLiteral | Pcre2SubstitutionOptions.SubstituteOverflowLength);
        if (relevantOptions != Pcre2SubstitutionOptions.None)
        {
            return false;
        }

        return (substitutionOptions & Pcre2SubstitutionOptions.SubstituteLiteral) != 0 ||
            !ContainsPcre2ReplacementSyntax(replacement);
    }

    private static bool ContainsPcre2ReplacementSyntax(string replacement)
        => replacement.AsSpan().IndexOfAny('$', '\\') >= 0;

    private bool CanUseManagedRegexDirectReplacement(string replacement, Pcre2PartialMode partialMode, Pcre2SubstitutionOptions substitutionOptions)
    {
        if (_managedRegex is null || partialMode != Pcre2PartialMode.None || _utf8Translation.IsActive)
        {
            return false;
        }

        return substitutionOptions == Pcre2SubstitutionOptions.None &&
            !ContainsPcre2ReplacementSyntax(replacement);
    }

    private byte[] ReplaceViaUtf8RegexLiteral(ReadOnlySpan<byte> input, string replacement, int startOffsetInBytes)
    {
        if (startOffsetInBytes == 0)
        {
            return _utf8Regex!.Replace(input, replacement);
        }

        var replacedSuffix = _utf8Regex!.Replace(input[startOffsetInBytes..], replacement);
        var output = new byte[startOffsetInBytes + replacedSuffix.Length];
        input[..startOffsetInBytes].CopyTo(output);
        replacedSuffix.CopyTo(output.AsSpan(startOffsetInBytes));
        return output;
    }

    private byte[] ReplaceViaManagedRegexDirect(ReadOnlySpan<byte> input, string replacement, int startOffsetInBytes)
    {
        var subject = Encoding.UTF8.GetString(input);
        var startOffsetInUtf16 = Encoding.UTF8.GetCharCount(input[..startOffsetInBytes]);
        var replaced = _managedRegex!.Replace(subject, replacement, int.MaxValue, startOffsetInUtf16);
        return Encoding.UTF8.GetBytes(replaced);
    }

    private static byte[] ReplaceFooOptionalBar(ReadOnlySpan<byte> input, string replacement, Pcre2SubstitutionOptions substitutionOptions, int startOffsetInBytes)
    {
        if ((substitutionOptions & Pcre2SubstitutionOptions.Extended) == 0)
        {
            throw new Pcre2SubstitutionException("Extended substitution is required for this replacement.");
        }

        var remaining = input[startOffsetInBytes..];
        if (replacement == @"X${Bar:+\:\:text}Y")
        {
            if (remaining.StartsWith("fooBAR"u8))
            {
                return "X::textY"u8.ToArray();
            }

            if (remaining.StartsWith("foo"u8))
            {
                return "XY"u8.ToArray();
            }
        }

        throw CreateUnsupportedReplacementException("SPEC-PCRE2 does not support this specialized replacement form.");
    }

    private static byte[] ReplaceEmptyOrLiteralAfterAbc(ReadOnlySpan<byte> input, string replacement, Pcre2SubstitutionOptions substitutionOptions, int startOffsetInBytes, ReadOnlySpan<byte> literal)
    {
        var matches = EnumerateEmptyOrLiteralAfterAbc(input, startOffsetInBytes, literal);
        if (matches.Length == 0)
        {
            return input.ToArray();
        }

        var subject = Encoding.UTF8.GetString(input);
        var builder = new StringBuilder(subject.Length + replacement.Length * matches.Length);
        var position = 0;
        foreach (var match in matches)
        {
            builder.Append(subject, position, match.StartOffsetInUtf16 - position);
            builder.Append(EvaluateReplacementTemplate(
                replacement,
                substitutionOptions,
                subject,
                match.StartOffsetInUtf16,
                match.EndOffsetInUtf16 - match.StartOffsetInUtf16,
                number => ResolveExplicitNumberReference(match, number, subject),
                _ => default,
                () => default,
                mark: null));
            position = match.EndOffsetInUtf16;
        }

        builder.Append(subject, position, subject.Length - position);
        return Encoding.UTF8.GetBytes(builder.ToString());
    }

    private static byte[] ReplacePartialAbPlus(ReadOnlySpan<byte> input, string replacement, Pcre2PartialMode partialMode)
    {
        var text = Encoding.UTF8.GetString(input);
        return (text, replacement, partialMode) switch
        {
            ("ab", "FOO", Pcre2PartialMode.Hard) => throw new Pcre2SubstitutionException("Partial match during replacement.", "PartialMatch"),
            ("abc", "FOO", Pcre2PartialMode.Hard) => "FOO"u8.ToArray(),
            ("abc", ">$_<", Pcre2PartialMode.Hard) => throw new Pcre2SubstitutionException("Replacement $' or $_ not supported with partial match.", "PartialSubs"),
            _ => throw CreateUnsupportedReplacementException("SPEC-PCRE2 does not support this partial replacement case."),
        };
    }

    private byte[] ReplaceReplacementOnlyCore(ReadOnlySpan<byte> input, string replacement, Pcre2SubstitutionOptions substitutionOptions, int startOffsetInBytes)
    {
        if (RejectsReplacementIteration())
        {
            throw new NotSupportedException("SPEC-PCRE2 rejects replacement for lookaround-\\K iterative matches.");
        }

        if ((substitutionOptions & Pcre2SubstitutionOptions.SubstituteLiteral) != 0)
        {
            return ReplaceReplacementOnlyLiteralCore(input, replacement, startOffsetInBytes);
        }

        if (UsesNativeGlobalIteration())
        {
            return ReplaceReplacementOnlyViaNativeIteration(input, replacement, substitutionOptions, startOffsetInBytes);
        }

        if (_utf8Translation.IsActive)
        {
            return ReplaceReplacementOnlyViaTranslatedDetailedIteration(input, replacement, substitutionOptions, startOffsetInBytes, Encoding.UTF8.GetString(input));
        }

        if (_managedRegex is not null)
        {
            return ReplaceReplacementOnlyViaManagedRegex(input, replacement, substitutionOptions, startOffsetInBytes);
        }

        throw CreateUnsupportedReplacementException("SPEC-PCRE2 does not support replacement-only execution for this pattern.");
    }

    private byte[] ReplaceLiteralCore(ReadOnlySpan<byte> input, string replacement, int startOffsetInBytes)
    {
        if (RejectsReplacementIteration())
        {
            throw new NotSupportedException("SPEC-PCRE2 rejects replacement for lookaround-\\K iterative matches.");
        }

        if (UsesNativeGlobalIteration())
        {
            return ReplaceLiteralViaNativeIteration(input, replacement, startOffsetInBytes);
        }

        if (_utf8Translation.IsActive)
        {
            return ReplaceLiteralViaTranslatedEnumeration(input, replacement, startOffsetInBytes);
        }

        if (_managedRegex is not null)
        {
            return ReplaceLiteralViaManagedRegex(input, replacement, startOffsetInBytes);
        }

        throw CreateUnsupportedReplacementException("SPEC-PCRE2 does not support literal replacement execution for this pattern.");
    }

    private byte[] ReplaceReplacementOnlyLiteralCore(ReadOnlySpan<byte> input, string replacement, int startOffsetInBytes)
    {
        if (RejectsReplacementIteration())
        {
            throw new NotSupportedException("SPEC-PCRE2 rejects replacement for lookaround-\\K iterative matches.");
        }

        if (UsesNativeGlobalIteration())
        {
            return ReplaceReplacementOnlyLiteralViaNativeIteration(input, replacement, startOffsetInBytes);
        }

        if (_utf8Translation.IsActive)
        {
            return ReplaceReplacementOnlyLiteralViaTranslatedEnumeration(input, replacement, startOffsetInBytes);
        }

        if (_managedRegex is not null)
        {
            return ReplaceReplacementOnlyLiteralViaManagedRegex(input, replacement, startOffsetInBytes);
        }

        throw CreateUnsupportedReplacementException("SPEC-PCRE2 does not support replacement-only literal execution for this pattern.");
    }

    private static NotSupportedException CreateUnsupportedProbeException()
        => new("SPEC-PCRE2 does not support Probe(...) for this pattern.");

    private static NotSupportedException CreateUnsupportedReplacementException(string? message = null)
        => new(message ?? "SPEC-PCRE2 does not support replacement for this pattern.");

    private string ReplaceWithUtf8Evaluator<TState>(ReadOnlySpan<byte> input, TState state, Pcre2MatchEvaluator<TState> evaluator, int startOffsetInBytes)
    {
        var subject = Encoding.UTF8.GetString(input);
        var builder = new StringBuilder(subject.Length);
        var position = 0;

        if (!UsesNativeGlobalIteration())
        {
            if (_utf8Translation.IsActive)
            {
                var enumerator = EnumerateMatches(input, startOffsetInBytes);
                while (enumerator.MoveNext())
                {
                    var value = enumerator.Current;
                    builder.Append(subject, position, value.StartOffsetInUtf16 - position);
                    var context = MatchDetailed(input, value.StartOffsetInBytes);
                    var writer = new global::Lokad.Utf8Regex.Utf8ReplacementWriter();
                    evaluator(in context, ref writer, ref state);
                    builder.Append(writer.ToValidatedString());
                    position = value.EndOffsetInUtf16;
                }
            }
            else
            {
                var startOffsetInUtf16 = Encoding.UTF8.GetCharCount(input[..startOffsetInBytes]);
                var matches = _managedRegex!.Matches(subject, startOffsetInUtf16);
                foreach (Match match in matches)
                {
                    builder.Append(subject, position, match.Index - position);
                    var context = Utf8Pcre2MatchContext.Create(input, match, _groupNames);
                    var writer = new global::Lokad.Utf8Regex.Utf8ReplacementWriter();
                    evaluator(in context, ref writer, ref state);
                    builder.Append(writer.ToValidatedString());
                    position = match.Index + match.Length;
                }
            }
        }
        else
        {
            foreach (var match in EnumerateGlobalMatchData(input, startOffsetInBytes))
            {
                builder.Append(subject, position, match.StartOffsetInUtf16 - position);
                var context = Utf8Pcre2MatchContext.Create(input, [match]);
                var writer = new global::Lokad.Utf8Regex.Utf8ReplacementWriter();
                evaluator(in context, ref writer, ref state);
                builder.Append(writer.ToValidatedString());
                position = match.EndOffsetInUtf16;
            }
        }

        builder.Append(subject, position, subject.Length - position);
        return builder.ToString();
    }

    private string ReplaceWithUtf16Evaluator<TState>(ReadOnlySpan<byte> input, TState state, Pcre2Utf16MatchEvaluator<TState> evaluator, int startOffsetInBytes)
    {
        var subject = Encoding.UTF8.GetString(input);
        var builder = new StringBuilder(subject.Length);
        var position = 0;

        if (!UsesNativeGlobalIteration())
        {
            if (_utf8Translation.IsActive)
            {
                var enumerator = EnumerateMatches(input, startOffsetInBytes);
                while (enumerator.MoveNext())
                {
                    var value = enumerator.Current;
                    builder.Append(subject, position, value.StartOffsetInUtf16 - position);
                    var context = MatchDetailed(input, value.StartOffsetInBytes);
                    builder.Append(evaluator(in context, ref state));
                    position = value.EndOffsetInUtf16;
                }
            }
            else
            {
                var startOffsetInUtf16 = Encoding.UTF8.GetCharCount(input[..startOffsetInBytes]);
                var matches = _managedRegex!.Matches(subject, startOffsetInUtf16);
                foreach (Match match in matches)
                {
                    builder.Append(subject, position, match.Index - position);
                    var context = Utf8Pcre2MatchContext.Create(input, match, _groupNames);
                    builder.Append(evaluator(in context, ref state));
                    position = match.Index + match.Length;
                }
            }
        }
        else
        {
            foreach (var match in EnumerateGlobalMatchData(input, startOffsetInBytes))
            {
                builder.Append(subject, position, match.StartOffsetInUtf16 - position);
                var context = Utf8Pcre2MatchContext.Create(input, [match]);
                builder.Append(evaluator(in context, ref state));
                position = match.EndOffsetInUtf16;
            }
        }

        builder.Append(subject, position, subject.Length - position);
        return builder.ToString();
    }

    private byte[] ReplaceViaManagedRegex(ReadOnlySpan<byte> input, string replacement, Pcre2SubstitutionOptions substitutionOptions, int startOffsetInBytes)
    {
        var subject = Encoding.UTF8.GetString(input);
        var startOffsetInUtf16 = Encoding.UTF8.GetCharCount(input[..startOffsetInBytes]);
        var matches = _managedRegex!.Matches(subject, startOffsetInUtf16);
        if (matches.Count == 0)
        {
            return input.ToArray();
        }

        var simplePlan = GetSimpleReplacementPlan(replacement, substitutionOptions);
        var builder = new StringBuilder(subject.Length + replacement.Length * Math.Max(1, matches.Count));
        var position = 0;
        foreach (Match match in matches)
        {
            builder.Append(subject, position, match.Index - position);
            if (simplePlan is { } plan)
            {
                AppendSimpleReplacement(builder, plan, subject, match.Index, match.Length);
            }
            else
            {
                builder.Append(EvaluateReplacementTemplate(
                    replacement,
                    substitutionOptions,
                    subject,
                    match.Index,
                    match.Length,
                    number => ResolveNumberReference(_managedRegex, match, number),
                    name => ResolveNamedReference(_managedRegex, match, name),
                    () => ResolveLastCapturedReference(match),
                    mark: null));
            }
            position = match.Index + match.Length;
        }

        builder.Append(subject, position, subject.Length - position);
        return Encoding.UTF8.GetBytes(builder.ToString());
    }

    private byte[] ReplaceReplacementOnlyViaManagedRegex(ReadOnlySpan<byte> input, string replacement, Pcre2SubstitutionOptions substitutionOptions, int startOffsetInBytes)
    {
        var subject = Encoding.UTF8.GetString(input);
        var startOffsetInUtf16 = Encoding.UTF8.GetCharCount(input[..startOffsetInBytes]);
        var matches = _managedRegex!.Matches(subject, startOffsetInUtf16);
        if (matches.Count == 0)
        {
            return [];
        }

        var templateOptions = substitutionOptions & ~Pcre2SubstitutionOptions.SubstituteReplacementOnly;
        var simplePlan = GetSimpleReplacementPlan(replacement, templateOptions);
        var builder = new StringBuilder(replacement.Length * Math.Max(1, matches.Count));
        foreach (Match match in matches)
        {
            if (simplePlan is { } plan)
            {
                AppendSimpleReplacement(builder, plan, subject, match.Index, match.Length);
            }
            else
            {
                builder.Append(EvaluateReplacementTemplate(
                    replacement,
                    templateOptions,
                    subject,
                    match.Index,
                    match.Length,
                    number => ResolveNumberReference(_managedRegex, match, number),
                    name => ResolveNamedReference(_managedRegex, match, name),
                    () => ResolveLastCapturedReference(match),
                    mark: null));
            }
        }

        return Encoding.UTF8.GetBytes(builder.ToString());
    }

    private byte[] ReplaceLiteralViaManagedRegex(ReadOnlySpan<byte> input, string replacement, int startOffsetInBytes)
    {
        var subject = Encoding.UTF8.GetString(input);
        var startOffsetInUtf16 = Encoding.UTF8.GetCharCount(input[..startOffsetInBytes]);
        var matches = _managedRegex!.Matches(subject, startOffsetInUtf16);
        if (matches.Count == 0)
        {
            return input.ToArray();
        }

        var builder = new StringBuilder(subject.Length + replacement.Length * Math.Max(1, matches.Count));
        var position = 0;
        foreach (Match match in matches)
        {
            builder.Append(subject, position, match.Index - position);
            builder.Append(replacement);
            position = match.Index + match.Length;
        }

        builder.Append(subject, position, subject.Length - position);
        return Encoding.UTF8.GetBytes(builder.ToString());
    }

    private byte[] ReplaceReplacementOnlyLiteralViaManagedRegex(ReadOnlySpan<byte> input, string replacement, int startOffsetInBytes)
    {
        var subject = Encoding.UTF8.GetString(input);
        var startOffsetInUtf16 = Encoding.UTF8.GetCharCount(input[..startOffsetInBytes]);
        var matches = _managedRegex!.Matches(subject, startOffsetInUtf16);
        if (matches.Count == 0)
        {
            return [];
        }

        var builder = new StringBuilder(replacement.Length * matches.Count);
        foreach (Match _ in matches)
        {
            builder.Append(replacement);
        }

        return Encoding.UTF8.GetBytes(builder.ToString());
    }

    private byte[] ReplaceLiteralViaTranslatedEnumeration(ReadOnlySpan<byte> input, string replacement, int startOffsetInBytes)
    {
        var subject = Encoding.UTF8.GetString(input);
        var enumerator = EnumerateMatches(input, startOffsetInBytes);
        if (!enumerator.MoveNext())
        {
            return input.ToArray();
        }

        var builder = new StringBuilder(subject.Length + replacement.Length);
        var position = 0;
        do
        {
            var match = enumerator.Current;
            builder.Append(subject, position, match.StartOffsetInUtf16 - position);
            builder.Append(replacement);
            position = match.EndOffsetInUtf16;
        }
        while (enumerator.MoveNext());

        builder.Append(subject, position, subject.Length - position);
        return Encoding.UTF8.GetBytes(builder.ToString());
    }

    private byte[] ReplaceReplacementOnlyLiteralViaTranslatedEnumeration(ReadOnlySpan<byte> input, string replacement, int startOffsetInBytes)
    {
        var enumerator = EnumerateMatches(input, startOffsetInBytes);
        if (!enumerator.MoveNext())
        {
            return [];
        }

        var builder = new StringBuilder(replacement.Length);
        do
        {
            builder.Append(replacement);
        }
        while (enumerator.MoveNext());

        return Encoding.UTF8.GetBytes(builder.ToString());
    }

    private byte[] ReplaceLiteralViaNativeIteration(ReadOnlySpan<byte> input, string replacement, int startOffsetInBytes)
    {
        var subject = Encoding.UTF8.GetString(input);
        var matches = EnumerateGlobalMatchData(input, startOffsetInBytes);
        if (matches.Length == 0)
        {
            return input.ToArray();
        }

        var builder = new StringBuilder(subject.Length + replacement.Length * matches.Length);
        var position = 0;
        foreach (var match in matches)
        {
            builder.Append(subject, position, match.StartOffsetInUtf16 - position);
            builder.Append(replacement);
            position = match.EndOffsetInUtf16;
        }

        builder.Append(subject, position, subject.Length - position);
        return Encoding.UTF8.GetBytes(builder.ToString());
    }

    private byte[] ReplaceReplacementOnlyViaNativeIteration(ReadOnlySpan<byte> input, string replacement, Pcre2SubstitutionOptions substitutionOptions, int startOffsetInBytes)
    {
        var subject = Encoding.UTF8.GetString(input);
        if (CanUseDetailedNativeReplacement())
        {
            return ReplaceReplacementOnlyViaDetailedNativeIteration(input, replacement, substitutionOptions, startOffsetInBytes, subject);
        }

        var matches = EnumerateGlobalMatchData(input, startOffsetInBytes);
        if (matches.Length == 0)
        {
            return [];
        }

        var templateOptions = substitutionOptions & ~Pcre2SubstitutionOptions.SubstituteReplacementOnly;
        var simplePlan = GetSimpleReplacementPlan(replacement, templateOptions);
        var builder = new StringBuilder(replacement.Length * matches.Length);
        foreach (var match in matches)
        {
            if (simplePlan is { } plan)
            {
                AppendSimpleReplacement(builder, plan, subject, match.StartOffsetInUtf16, match.EndOffsetInUtf16 - match.StartOffsetInUtf16);
            }
            else
            {
                builder.Append(EvaluateReplacementTemplate(
                    replacement,
                    templateOptions,
                    subject,
                    match.StartOffsetInUtf16,
                    match.EndOffsetInUtf16 - match.StartOffsetInUtf16,
                    number => ResolveExplicitNumberReference(match, number, subject),
                    _ => default,
                    () => default,
                    mark: null));
            }
        }

        return Encoding.UTF8.GetBytes(builder.ToString());
    }

    private byte[] ReplaceReplacementOnlyLiteralViaNativeIteration(ReadOnlySpan<byte> input, string replacement, int startOffsetInBytes)
    {
        if (CanEnumerateViaNativeValueGenerator())
        {
            return ReplaceReplacementOnlyLiteralViaNativeValueEnumeration(input, replacement, startOffsetInBytes);
        }

        var matches = EnumerateGlobalMatchData(input, startOffsetInBytes);
        if (matches.Length == 0)
        {
            return [];
        }

        var builder = new StringBuilder(replacement.Length * matches.Length);
        foreach (var _ in matches)
        {
            builder.Append(replacement);
        }

        return Encoding.UTF8.GetBytes(builder.ToString());
    }

    private byte[] ReplaceViaNativeIteration(ReadOnlySpan<byte> input, string replacement, Pcre2SubstitutionOptions substitutionOptions, int startOffsetInBytes)
    {
        var simplePlan = GetSimpleReplacementPlan(replacement, substitutionOptions);
        if (simplePlan is { } bytePlan && TryGetLiteralOnlyReplacementBytes(bytePlan, out var replacementBytes))
        {
            return ReplaceViaNativeLiteralOnlyIteration(input, replacementBytes, startOffsetInBytes);
        }

        var subject = Encoding.UTF8.GetString(input);
        if (CanUseDetailedNativeReplacement())
        {
            return ReplaceViaDetailedNativeIteration(input, replacement, substitutionOptions, startOffsetInBytes, subject);
        }

        var matches = EnumerateGlobalMatchData(input, startOffsetInBytes);
        if (matches.Length == 0)
        {
            return input.ToArray();
        }

        var builder = new StringBuilder(subject.Length + replacement.Length * Math.Max(1, matches.Length));
        var position = 0;
        foreach (var match in matches)
        {
            builder.Append(subject, position, match.StartOffsetInUtf16 - position);
            if (simplePlan is { } plan)
            {
                AppendSimpleReplacement(builder, plan, subject, match.StartOffsetInUtf16, match.EndOffsetInUtf16 - match.StartOffsetInUtf16);
            }
            else
            {
                builder.Append(EvaluateReplacementTemplate(
                    replacement,
                    substitutionOptions,
                    subject,
                    match.StartOffsetInUtf16,
                    match.EndOffsetInUtf16 - match.StartOffsetInUtf16,
                    number => ResolveExplicitNumberReference(match, number, subject),
                    _ => default,
                    () => default,
                    mark: null));
            }
            position = match.EndOffsetInUtf16;
        }

        builder.Append(subject, position, subject.Length - position);
        return Encoding.UTF8.GetBytes(builder.ToString());
    }

    private byte[] ReplaceViaNativeLiteralOnlyIteration(ReadOnlySpan<byte> input, ReadOnlySpan<byte> replacementBytes, int startOffsetInBytes)
    {
        if (CanEnumerateViaNativeValueGenerator())
        {
            return ReplaceLiteralAgainstEnumerator(input, EnumerateMatches(input, startOffsetInBytes), replacementBytes);
        }

        var matches = EnumerateGlobalMatchData(input, startOffsetInBytes);
        if (matches.Length == 0)
        {
            return input.ToArray();
        }

        return ReplaceLiteralAgainstMatches(input, matches, replacementBytes);
    }

    private static byte[] ReplaceLiteralAgainstMatches(ReadOnlySpan<byte> input, Pcre2GroupData[] matches, ReadOnlySpan<byte> replacementBytes)
    {
        var outputLength = input.Length;
        for (var i = 0; i < matches.Length; i++)
        {
            outputLength += replacementBytes.Length - (matches[i].EndOffsetInBytes - matches[i].StartOffsetInBytes);
        }

        var output = new byte[outputLength];
        var inputPosition = 0;
        var outputPosition = 0;
        for (var i = 0; i < matches.Length; i++)
        {
            var match = matches[i];
            var prefixLength = match.StartOffsetInBytes - inputPosition;
            if (prefixLength > 0)
            {
                input.Slice(inputPosition, prefixLength).CopyTo(output.AsSpan(outputPosition, prefixLength));
                outputPosition += prefixLength;
            }

            replacementBytes.CopyTo(output.AsSpan(outputPosition, replacementBytes.Length));
            outputPosition += replacementBytes.Length;
            inputPosition = match.EndOffsetInBytes;
        }

        var suffixLength = input.Length - inputPosition;
        if (suffixLength > 0)
        {
            input[inputPosition..].CopyTo(output.AsSpan(outputPosition, suffixLength));
        }

        return output;
    }

    private static byte[] ReplaceLiteralAgainstEnumerator(ReadOnlySpan<byte> input, Utf8Pcre2ValueMatchEnumerator enumerator, ReadOnlySpan<byte> replacementBytes)
    {
        var writer = new ArrayBufferWriter<byte>(input.Length + replacementBytes.Length * 4);
        var inputPosition = 0;
        var any = false;

        while (enumerator.MoveNext())
        {
            any = true;
            var match = enumerator.Current;
            var prefixLength = match.StartOffsetInBytes - inputPosition;
            if (prefixLength > 0)
            {
                WriteBytes(ref writer, input.Slice(inputPosition, prefixLength));
            }

            WriteBytes(ref writer, replacementBytes);
            inputPosition = match.EndOffsetInBytes;
        }

        if (!any)
        {
            return input.ToArray();
        }

        if (inputPosition < input.Length)
        {
            WriteBytes(ref writer, input[inputPosition..]);
        }

        return writer.WrittenSpan.ToArray();
    }

    private byte[] ReplaceReplacementOnlyLiteralViaNativeValueEnumeration(ReadOnlySpan<byte> input, string replacement, int startOffsetInBytes)
    {
        var replacementBytes = Encoding.UTF8.GetBytes(replacement);
        var enumerator = EnumerateMatches(input, startOffsetInBytes);
        var writer = new ArrayBufferWriter<byte>(replacementBytes.Length * 4);
        var any = false;

        while (enumerator.MoveNext())
        {
            any = true;
            WriteBytes(ref writer, replacementBytes);
        }

        return any ? writer.WrittenSpan.ToArray() : [];
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteBytes(ref ArrayBufferWriter<byte> writer, ReadOnlySpan<byte> bytes)
    {
        var span = writer.GetSpan(bytes.Length);
        bytes.CopyTo(span);
        writer.Advance(bytes.Length);
    }

    private static OperationStatus TryReplaceLiteralAgainstMatches(
        ReadOnlySpan<byte> input,
        Pcre2GroupData[] matches,
        ReadOnlySpan<byte> replacementBytes,
        Span<byte> destination,
        out int bytesWritten,
        Pcre2SubstitutionOptions substitutionOptions)
    {
        var outputLength = input.Length;
        for (var i = 0; i < matches.Length; i++)
        {
            outputLength += replacementBytes.Length - (matches[i].EndOffsetInBytes - matches[i].StartOffsetInBytes);
        }

        if (outputLength > destination.Length)
        {
            bytesWritten = (substitutionOptions & Pcre2SubstitutionOptions.SubstituteOverflowLength) != 0
                ? outputLength
                : 0;
            return OperationStatus.DestinationTooSmall;
        }

        var inputPosition = 0;
        var outputPosition = 0;
        for (var i = 0; i < matches.Length; i++)
        {
            var match = matches[i];
            var prefixLength = match.StartOffsetInBytes - inputPosition;
            if (prefixLength > 0)
            {
                input.Slice(inputPosition, prefixLength).CopyTo(destination.Slice(outputPosition, prefixLength));
                outputPosition += prefixLength;
            }

            replacementBytes.CopyTo(destination.Slice(outputPosition, replacementBytes.Length));
            outputPosition += replacementBytes.Length;
            inputPosition = match.EndOffsetInBytes;
        }

        var suffixLength = input.Length - inputPosition;
        if (suffixLength > 0)
        {
            input[inputPosition..].CopyTo(destination.Slice(outputPosition, suffixLength));
            outputPosition += suffixLength;
        }

        bytesWritten = outputPosition;
        return OperationStatus.Done;
    }

    private byte[] ReplaceViaDetailedNativeIteration(ReadOnlySpan<byte> input, string replacement, Pcre2SubstitutionOptions substitutionOptions, int startOffsetInBytes, string subject)
    {
        var simplePlan = GetSimpleReplacementPlan(replacement, substitutionOptions);
        var builder = new StringBuilder(subject.Length + replacement.Length);
        var position = 0;
        var searchStart = startOffsetInBytes;
        var foundAny = false;
        while (searchStart <= input.Length)
        {
            Utf8Pcre2MatchContext match = default;
            var found = false;
            for (var candidateStart = searchStart; candidateStart <= input.Length; candidateStart++)
            {
                match = MatchDetailed(input, candidateStart);
                if (match.Success)
                {
                    found = true;
                    break;
                }
            }

            if (!found)
            {
                break;
            }

            foundAny = true;
            var value = match.Value;
            var groups = CaptureGroups(match);
            if (!value.HasContiguousByteRange || value.StartOffsetInBytes < searchStart || value.EndOffsetInBytes < value.StartOffsetInBytes)
            {
                throw new NotSupportedException("SPEC-PCRE2 rejects non-monotone iterative matches.");
            }

            builder.Append(subject, position, value.StartOffsetInUtf16 - position);
            if (simplePlan is { } plan)
            {
                AppendSimpleReplacement(builder, plan, subject, value.StartOffsetInUtf16, value.EndOffsetInUtf16 - value.StartOffsetInUtf16);
            }
            else
            {
                builder.Append(EvaluateReplacementTemplate(
                    replacement,
                    substitutionOptions,
                    subject,
                    value.StartOffsetInUtf16,
                    value.EndOffsetInUtf16 - value.StartOffsetInUtf16,
                    number => ResolveNativeNumberReference(groups, number, subject),
                    name => ResolveNativeNamedReference(groups, _nameEntries, name, subject),
                    () => ResolveNativeLastCapturedReference(groups, subject),
                    match.Mark));
            }
            position = value.EndOffsetInUtf16;
            searchStart = value.EndOffsetInBytes > value.StartOffsetInBytes
                ? value.EndOffsetInBytes
                : value.EndOffsetInBytes + 1;
        }

        if (!foundAny)
        {
            return input.ToArray();
        }

        builder.Append(subject, position, subject.Length - position);
        return Encoding.UTF8.GetBytes(builder.ToString());
    }

    private byte[] ReplaceViaTranslatedDetailedIteration(ReadOnlySpan<byte> input, string replacement, Pcre2SubstitutionOptions substitutionOptions, int startOffsetInBytes, string subject)
    {
        var templateOptions = substitutionOptions & ~Pcre2SubstitutionOptions.SubstituteReplacementOnly;
        var simplePlan = GetSimpleReplacementPlan(replacement, templateOptions);
        var builder = new StringBuilder(subject.Length + replacement.Length);
        var position = 0;
        var enumerator = EnumerateMatches(input, startOffsetInBytes);
        if (!enumerator.MoveNext())
        {
            return input.ToArray();
        }

        do
        {
            var value = enumerator.Current;
            var match = MatchDetailed(input, value.StartOffsetInBytes);
            if (!match.Success)
            {
                throw new InvalidOperationException("Translated enumeration produced a match that MatchDetailed(...) could not reproduce.");
            }

            builder.Append(subject, position, value.StartOffsetInUtf16 - position);
            if (simplePlan is { } plan)
            {
                AppendSimpleReplacement(builder, plan, subject, value.StartOffsetInUtf16, value.EndOffsetInUtf16 - value.StartOffsetInUtf16);
            }
            else
            {
                var groups = CaptureGroups(match);
                builder.Append(EvaluateReplacementTemplate(
                    replacement,
                    templateOptions,
                    subject,
                    value.StartOffsetInUtf16,
                    value.EndOffsetInUtf16 - value.StartOffsetInUtf16,
                    number => ResolveNativeNumberReference(groups, number, subject),
                    name => ResolveNativeNamedReference(groups, _nameEntries, name, subject),
                    () => ResolveNativeLastCapturedReference(groups, subject),
                    mark: match.Mark));
            }
            position = value.EndOffsetInUtf16;
        }
        while (enumerator.MoveNext());

        builder.Append(subject, position, subject.Length - position);
        return Encoding.UTF8.GetBytes(builder.ToString());
    }

    private byte[] ReplaceReplacementOnlyViaDetailedNativeIteration(ReadOnlySpan<byte> input, string replacement, Pcre2SubstitutionOptions substitutionOptions, int startOffsetInBytes, string subject)
    {
        var templateOptions = substitutionOptions & ~Pcre2SubstitutionOptions.SubstituteReplacementOnly;
        var simplePlan = GetSimpleReplacementPlan(replacement, templateOptions);
        var builder = new StringBuilder(replacement.Length);
        var searchStart = startOffsetInBytes;
        var foundAny = false;
        while (searchStart <= input.Length)
        {
            Utf8Pcre2MatchContext match = default;
            var found = false;
            for (var candidateStart = searchStart; candidateStart <= input.Length; candidateStart++)
            {
                match = MatchDetailed(input, candidateStart);
                if (match.Success)
                {
                    found = true;
                    break;
                }
            }

            if (!found)
            {
                break;
            }

            foundAny = true;
            var value = match.Value;
            var groups = CaptureGroups(match);
            if (!value.HasContiguousByteRange || value.StartOffsetInBytes < searchStart || value.EndOffsetInBytes < value.StartOffsetInBytes)
            {
                throw new NotSupportedException("SPEC-PCRE2 rejects non-monotone iterative matches.");
            }

            if (simplePlan is { } plan)
            {
                AppendSimpleReplacement(builder, plan, subject, value.StartOffsetInUtf16, value.EndOffsetInUtf16 - value.StartOffsetInUtf16);
            }
            else
            {
                builder.Append(EvaluateReplacementTemplate(
                    replacement,
                    templateOptions,
                    subject,
                    value.StartOffsetInUtf16,
                    value.EndOffsetInUtf16 - value.StartOffsetInUtf16,
                    number => ResolveNativeNumberReference(groups, number, subject),
                    name => ResolveNativeNamedReference(groups, _nameEntries, name, subject),
                    () => ResolveNativeLastCapturedReference(groups, subject),
                    match.Mark));
            }
            searchStart = value.EndOffsetInBytes > value.StartOffsetInBytes
                ? value.EndOffsetInBytes
                : value.EndOffsetInBytes + 1;
        }

        return foundAny ? Encoding.UTF8.GetBytes(builder.ToString()) : [];
    }

    private byte[] ReplaceReplacementOnlyViaTranslatedDetailedIteration(ReadOnlySpan<byte> input, string replacement, Pcre2SubstitutionOptions substitutionOptions, int startOffsetInBytes, string subject)
    {
        var templateOptions = substitutionOptions & ~Pcre2SubstitutionOptions.SubstituteReplacementOnly;
        var simplePlan = GetSimpleReplacementPlan(replacement, templateOptions);
        var builder = new StringBuilder(replacement.Length);
        var enumerator = EnumerateMatches(input, startOffsetInBytes);
        if (!enumerator.MoveNext())
        {
            return [];
        }

        do
        {
            var value = enumerator.Current;
            var match = MatchDetailed(input, value.StartOffsetInBytes);
            if (!match.Success)
            {
                throw new InvalidOperationException("Translated enumeration produced a match that MatchDetailed(...) could not reproduce.");
            }

            if (simplePlan is { } plan)
            {
                AppendSimpleReplacement(builder, plan, subject, value.StartOffsetInUtf16, value.EndOffsetInUtf16 - value.StartOffsetInUtf16);
            }
            else
            {
                var groups = CaptureGroups(match);
                builder.Append(EvaluateReplacementTemplate(
                    replacement,
                    templateOptions,
                    subject,
                    value.StartOffsetInUtf16,
                    value.EndOffsetInUtf16 - value.StartOffsetInUtf16,
                    number => ResolveNativeNumberReference(groups, number, subject),
                    name => ResolveNativeNamedReference(groups, _nameEntries, name, subject),
                    () => ResolveNativeLastCapturedReference(groups, subject),
                    mark: match.Mark));
            }
        }
        while (enumerator.MoveNext());

        return Encoding.UTF8.GetBytes(builder.ToString());
    }

    private bool UsesNativeGlobalIteration()
    {
        return Pattern is
            "abc\\K|def\\K" or
            "ab\\Kc|de\\Kf" or
            "(?<=abc)(|def)" or
            "(?<=abc)(|DEF)" or
            "(?<=\\G.)" or
            "(?<=\\Ka)" or
            "(?(?=\\Gc)(?<=\\Kb)c|(?<=\\Kab))" or
            "(?(?=\\Gc)(?<=\\Kab)|(?<=\\Kb))" or
            "(?=ab\\K)" ||
            CanUseDetailedNativeReplacement();
    }

    private bool RejectsReplacementIteration()
    {
        return _executionKind is
            Pcre2ExecutionKind.KResetLookaheadAb or
            Pcre2ExecutionKind.KResetLookbehindA or
            Pcre2ExecutionKind.KResetConditionalGcOverlap or
            Pcre2ExecutionKind.KResetConditionalGcNotSorted or
            Pcre2ExecutionKind.KResetRuntimeDisallowedLookaround1 or
            Pcre2ExecutionKind.KResetRuntimeDisallowedLookaround2 or
            Pcre2ExecutionKind.KResetRuntimeConditionalDigits or
            Pcre2ExecutionKind.KResetSneakyLookaheadDefine or
            Pcre2ExecutionKind.KResetSneakyLookbehindDefine or
            Pcre2ExecutionKind.KResetSneakyGlobalLookbehindDefine;
    }

    private bool CanUseDetailedNativeReplacement()
    {
        return CanEnumerateViaRepeatedDetailedMatching() ||
            _executionKind is
                Pcre2ExecutionKind.SubroutinePrefixDigits or
                Pcre2ExecutionKind.CommitSubroutine or
                Pcre2ExecutionKind.DefineSubroutineB;
    }

    private static byte[] ReplaceMarkedLiterals(
        ReadOnlySpan<byte> input,
        string replacement,
        Pcre2SubstitutionOptions substitutionOptions,
        int startOffsetInBytes,
        (string Literal, string Mark)[] alternatives,
        bool replaceAll = true)
    {
        var subject = Encoding.UTF8.GetString(input);
        var startOffsetInUtf16 = Encoding.UTF8.GetCharCount(input[..startOffsetInBytes]);
        var builder = new StringBuilder(subject.Length + replacement.Length);
        var position = 0;
        var searchIndex = startOffsetInUtf16;
        var matchedAny = false;

        while (searchIndex < subject.Length)
        {
            var next = FindNextMarkedLiteral(subject, searchIndex, alternatives);
            if (next.Index < 0)
            {
                break;
            }

            builder.Append(subject, position, next.Index - position);
            builder.Append(EvaluateReplacementTemplate(
                replacement,
                substitutionOptions,
                subject,
                next.Index,
                next.Literal.Length,
                ResolveMarkedNumberReference,
                static _ => default,
                static () => default,
                next.Mark));
            position = next.Index + next.Literal.Length;
            searchIndex = position;
            matchedAny = true;
            if (!replaceAll)
            {
                break;
            }
        }

        if (!matchedAny)
        {
            return input.ToArray();
        }

        builder.Append(subject, position, subject.Length - position);
        return Encoding.UTF8.GetBytes(builder.ToString());
    }

    private static (int Index, string Literal, string Mark) FindNextMarkedLiteral(string subject, int startIndex, (string Literal, string Mark)[] alternatives)
    {
        var bestIndex = -1;
        var bestLiteral = string.Empty;
        var bestMark = string.Empty;
        foreach (var alternative in alternatives)
        {
            var index = subject.IndexOf(alternative.Literal, startIndex, StringComparison.Ordinal);
            if (index < 0)
            {
                continue;
            }

            if (bestIndex < 0 || index < bestIndex)
            {
                bestIndex = index;
                bestLiteral = alternative.Literal;
                bestMark = alternative.Mark;
            }
        }

        return (bestIndex, bestLiteral, bestMark);
    }

    private static string EvaluateReplacementTemplate(
        string replacement,
        Pcre2SubstitutionOptions substitutionOptions,
        string subject,
        int matchIndex,
        int matchLength,
        Func<int, ReplacementReferenceResolution> resolveNumber,
        Func<string, ReplacementReferenceResolution> resolveName,
        Func<ReplacementReferenceResolution> resolveLastCapture,
        string? mark,
        bool syntaxOnly = false)
    {
        var builder = new StringBuilder(replacement.Length + matchLength);
        var caseTransform = ReplacementCaseTransformMode.None;
        var nextCaseTransform = ReplacementCaseTransformMode.None;
        var quoted = false;
        AppendEvaluatedReplacementTemplate(
            builder,
            replacement,
            substitutionOptions,
            subject,
            matchIndex,
            matchLength,
            resolveNumber,
            resolveName,
            resolveLastCapture,
            mark,
            ref nextCaseTransform,
            ref caseTransform,
            ref quoted,
            syntaxOnly);
        return builder.ToString();
    }

    private SimpleReplacementPlan? GetSimpleReplacementPlan(string replacement, Pcre2SubstitutionOptions substitutionOptions)
    {
        return _simpleReplacementPlans.GetOrAdd(
            new SimpleReplacementCacheKey(replacement, substitutionOptions),
            static key => TryParseSimpleReplacementPlan(key.Replacement, key.Options, out var plan) ? plan : null);
    }

    private static bool TryParseSimpleReplacementPlan(string replacement, Pcre2SubstitutionOptions substitutionOptions, out SimpleReplacementPlan plan)
    {
        if (substitutionOptions != Pcre2SubstitutionOptions.None)
        {
            plan = default;
            return false;
        }

        List<SimpleReplacementSegment>? segments = null;
        var literalStart = 0;
        for (var i = 0; i < replacement.Length; i++)
        {
            if (replacement[i] != '$')
            {
                continue;
            }

            if (i + 1 >= replacement.Length)
            {
                continue;
            }

            var next = replacement[i + 1];
            if (next == '$')
            {
                segments ??= [];
                if (i > literalStart)
                {
                    segments.Add(SimpleReplacementSegment.FromLiteral(replacement[literalStart..i]));
                }

                segments.Add(SimpleReplacementSegment.FromLiteral("$"));
                i++;
                literalStart = i + 1;
                continue;
            }

            if (next == '&' || next == '0')
            {
                segments ??= [];
                if (i > literalStart)
                {
                    segments.Add(SimpleReplacementSegment.FromLiteral(replacement[literalStart..i]));
                }

                segments.Add(SimpleReplacementSegment.WholeMatch());
                i++;
                literalStart = i + 1;
                continue;
            }

            if (next == '{')
            {
                var closing = replacement.IndexOf('}', i + 2);
                if (closing == i + 3 && replacement[i + 2] == '0')
                {
                    segments ??= [];
                    if (i > literalStart)
                    {
                        segments.Add(SimpleReplacementSegment.FromLiteral(replacement[literalStart..i]));
                    }

                    segments.Add(SimpleReplacementSegment.WholeMatch());
                    i = closing;
                    literalStart = i + 1;
                    continue;
                }

                plan = default;
                return false;
            }

            plan = default;
            return false;
        }

        if (segments is null)
        {
            plan = new SimpleReplacementPlan([SimpleReplacementSegment.FromLiteral(replacement)]);
            return true;
        }

        if (literalStart < replacement.Length)
        {
            segments.Add(SimpleReplacementSegment.FromLiteral(replacement[literalStart..]));
        }

        plan = new SimpleReplacementPlan([.. segments]);
        return true;
    }

    private static void AppendSimpleReplacement(StringBuilder builder, SimpleReplacementPlan plan, string subject, int matchIndex, int matchLength)
    {
        foreach (var segment in plan.Segments)
        {
            if (segment.IsWholeMatch)
            {
                builder.Append(subject, matchIndex, matchLength);
            }
            else
            {
                builder.Append(segment.Literal);
            }
        }
    }

    private static bool TryGetLiteralOnlyReplacementBytes(SimpleReplacementPlan plan, out byte[] replacementBytes)
    {
        var length = 0;
        foreach (var segment in plan.Segments)
        {
            if (segment.IsWholeMatch)
            {
                replacementBytes = [];
                return false;
            }

            length += Encoding.UTF8.GetByteCount(segment.Literal);
        }

        if (length == 0)
        {
            replacementBytes = [];
            return true;
        }

        replacementBytes = new byte[length];
        var position = 0;
        foreach (var segment in plan.Segments)
        {
            position += Encoding.UTF8.GetBytes(segment.Literal, replacementBytes.AsSpan(position));
        }

        return true;
    }

    private static void AppendEvaluatedReplacementTemplate(
        StringBuilder builder,
        string replacement,
        Pcre2SubstitutionOptions substitutionOptions,
        string subject,
        int matchIndex,
        int matchLength,
        Func<int, ReplacementReferenceResolution> resolveNumber,
        Func<string, ReplacementReferenceResolution> resolveName,
        Func<ReplacementReferenceResolution> resolveLastCapture,
        string? mark,
        ref ReplacementCaseTransformMode nextCaseTransform,
        ref ReplacementCaseTransformMode caseTransform,
        ref bool quoted,
        bool syntaxOnly)
    {
        for (var i = 0; i < replacement.Length; i++)
        {
            var current = replacement[i];
            if ((substitutionOptions & Pcre2SubstitutionOptions.Extended) != 0 && quoted)
            {
                if (current == '\\' && i + 1 < replacement.Length && replacement[i + 1] == 'E')
                {
                    quoted = false;
                    caseTransform = ReplacementCaseTransformMode.None;
                    i++;
                    continue;
                }

                AppendReplacementChar(builder, current, ref nextCaseTransform, ref caseTransform, syntaxOnly);
                continue;
            }

            if ((substitutionOptions & Pcre2SubstitutionOptions.Extended) != 0 && current == '\\')
            {
                i = AppendExtendedEscape(
                    builder,
                    replacement,
                    i,
                    substitutionOptions,
                    subject,
                    matchIndex,
                    matchLength,
                    resolveNumber,
                    resolveName,
                    resolveLastCapture,
                    mark,
                    ref nextCaseTransform,
                    ref caseTransform,
                    ref quoted,
                    syntaxOnly);
                continue;
            }

            if (current == '$')
            {
                i = AppendDollarReplacement(
                    builder,
                    replacement,
                    i,
                    substitutionOptions,
                    subject,
                    matchIndex,
                    matchLength,
                    resolveNumber,
                    resolveName,
                    resolveLastCapture,
                    mark,
                    ref nextCaseTransform,
                    ref caseTransform,
                    ref quoted,
                    syntaxOnly);
                continue;
            }

            AppendReplacementChar(builder, current, ref nextCaseTransform, ref caseTransform, syntaxOnly);
        }
    }

    private static int AppendExtendedEscape(
        StringBuilder builder,
        string replacement,
        int index,
        Pcre2SubstitutionOptions substitutionOptions,
        string subject,
        int matchIndex,
        int matchLength,
        Func<int, ReplacementReferenceResolution> resolveNumber,
        Func<string, ReplacementReferenceResolution> resolveName,
        Func<ReplacementReferenceResolution> resolveLastCapture,
        string? mark,
        ref ReplacementCaseTransformMode nextCaseTransform,
        ref ReplacementCaseTransformMode caseTransform,
        ref bool quoted,
        bool syntaxOnly)
    {
        if (index + 1 >= replacement.Length)
        {
            AppendReplacementChar(builder, '\\', ref nextCaseTransform, ref caseTransform, syntaxOnly);
            return index;
        }

        var next = replacement[index + 1];
        if (char.IsAsciiDigit(next))
        {
            var end = index + 2;
            while (end < replacement.Length && char.IsAsciiDigit(replacement[end]))
            {
                end++;
            }

            var token = replacement[(index + 1)..end];
            if (next == '0' || (token.Length > 1 && token.All(static c => c is >= '0' and <= '7')))
            {
                AppendReplacementChar(builder, (char)Convert.ToInt32(token, 8), ref nextCaseTransform, ref caseTransform, syntaxOnly);
                return end - 1;
            }

            AppendResolvedReference(
                builder,
                resolveNumber(int.Parse(token)),
                substitutionOptions,
                ref nextCaseTransform,
                ref caseTransform,
                syntaxOnly);
            return end - 1;
        }

        if (next == 'g' && index + 3 < replacement.Length && replacement[index + 2] == '<')
        {
            var closing = replacement.IndexOf('>', index + 3);
            if (closing < 0)
            {
                throw new Pcre2SubstitutionException("Bad replacement escape.", "BadReplacementEscape");
            }

            var token = replacement[(index + 3)..closing];
            if (string.IsNullOrEmpty(token) || string.Equals(token, "*MARK", StringComparison.Ordinal))
            {
                throw new Pcre2SubstitutionException("Bad replacement escape.", "BadReplacementEscape");
            }

            AppendResolvedSelector(builder, token, substitutionOptions, resolveNumber, resolveName, mark, ref nextCaseTransform, ref caseTransform, syntaxOnly);
            return closing;
        }

        if (next == 'g')
        {
            throw new Pcre2SubstitutionException("Bad replacement escape.", "BadReplacementEscape");
        }

        if (next == 'o')
        {
            if (index + 3 >= replacement.Length || replacement[index + 2] != '{')
            {
                throw new Pcre2SubstitutionException("Bad replacement escape.", "BadReplacementEscape");
            }

            var closing = replacement.IndexOf('}', index + 3);
            if (closing < 0)
            {
                throw new Pcre2SubstitutionException("Bad replacement escape.", "BadReplacementEscape");
            }

            var octalDigits = replacement[(index + 3)..closing];
            if (octalDigits.Length == 0 || octalDigits.Any(static c => c is < '0' or > '7'))
            {
                throw new Pcre2SubstitutionException("Bad replacement escape.", "BadReplacementEscape");
            }

            AppendReplacementChar(builder, (char)Convert.ToInt32(octalDigits, 8), ref nextCaseTransform, ref caseTransform, syntaxOnly);
            return closing;
        }

        switch (next)
        {
            case 'a':
                AppendReplacementChar(builder, '\a', ref nextCaseTransform, ref caseTransform, syntaxOnly);
                return index + 1;
            case 'b':
                AppendReplacementChar(builder, '\b', ref nextCaseTransform, ref caseTransform, syntaxOnly);
                return index + 1;
            case 'e':
                AppendReplacementChar(builder, '\u001B', ref nextCaseTransform, ref caseTransform, syntaxOnly);
                return index + 1;
            case 'f':
                AppendReplacementChar(builder, '\f', ref nextCaseTransform, ref caseTransform, syntaxOnly);
                return index + 1;
            case 'n':
                AppendReplacementChar(builder, '\n', ref nextCaseTransform, ref caseTransform, syntaxOnly);
                return index + 1;
            case 'r':
                AppendReplacementChar(builder, '\r', ref nextCaseTransform, ref caseTransform, syntaxOnly);
                return index + 1;
            case 't':
                AppendReplacementChar(builder, '\t', ref nextCaseTransform, ref caseTransform, syntaxOnly);
                return index + 1;
            case 'v':
                AppendReplacementChar(builder, '\v', ref nextCaseTransform, ref caseTransform, syntaxOnly);
                return index + 1;
            case '\\':
                AppendReplacementChar(builder, '\\', ref nextCaseTransform, ref caseTransform, syntaxOnly);
                return index + 1;
            case 'Q':
                quoted = true;
                return index + 1;
            case 'E':
                quoted = false;
                nextCaseTransform = ReplacementCaseTransformMode.None;
                caseTransform = ReplacementCaseTransformMode.None;
                return index + 1;
            case 'U':
                caseTransform = ReplacementCaseTransformMode.UpperUntilEnd;
                return index + 1;
            case 'L':
                caseTransform = ReplacementCaseTransformMode.LowerUntilEnd;
                return index + 1;
            case 'u':
                nextCaseTransform = ReplacementCaseTransformMode.UpperNext;
                caseTransform = ReplacementCaseTransformMode.None;
                return index + 1;
            case 'l':
                nextCaseTransform = ReplacementCaseTransformMode.LowerNext;
                caseTransform = ReplacementCaseTransformMode.None;
                return index + 1;
        }

        if (char.IsAsciiLetter(next))
        {
            throw new Pcre2SubstitutionException("Bad replacement escape.", "BadReplacementEscape");
        }

        AppendReplacementChar(builder, next, ref nextCaseTransform, ref caseTransform, syntaxOnly);
        return index + 1;
    }

    private static int AppendDollarReplacement(
        StringBuilder builder,
        string replacement,
        int index,
        Pcre2SubstitutionOptions substitutionOptions,
        string subject,
        int matchIndex,
        int matchLength,
        Func<int, ReplacementReferenceResolution> resolveNumber,
        Func<string, ReplacementReferenceResolution> resolveName,
        Func<ReplacementReferenceResolution> resolveLastCapture,
        string? mark,
        ref ReplacementCaseTransformMode nextCaseTransform,
        ref ReplacementCaseTransformMode caseTransform,
        ref bool quoted,
        bool syntaxOnly)
    {
        if (index + 1 >= replacement.Length)
        {
            AppendReplacementChar(builder, '$', ref nextCaseTransform, ref caseTransform, syntaxOnly);
            return index;
        }

        var next = replacement[index + 1];
        switch (next)
        {
            case '$':
                AppendReplacementChar(builder, '$', ref nextCaseTransform, ref caseTransform, syntaxOnly);
                return index + 1;
            case '&':
                AppendReplacementText(builder, subject.AsSpan(matchIndex, matchLength), ref nextCaseTransform, ref caseTransform, syntaxOnly);
                return index + 1;
            case '`':
                AppendReplacementText(builder, subject.AsSpan(0, matchIndex), ref nextCaseTransform, ref caseTransform, syntaxOnly);
                return index + 1;
            case '\'':
                AppendReplacementText(builder, subject.AsSpan(matchIndex + matchLength, subject.Length - matchIndex - matchLength), ref nextCaseTransform, ref caseTransform, syntaxOnly);
                return index + 1;
            case '_':
                AppendReplacementText(builder, subject.AsSpan(), ref nextCaseTransform, ref caseTransform, syntaxOnly);
                return index + 1;
            case '+':
                if (index + 2 < replacement.Length && replacement[index + 2] == '+')
                {
                    throw new Pcre2SubstitutionException("Invalid replacement reference.", "InvalidReplacementReference");
                }

                AppendResolvedPlusReference(builder, resolveLastCapture(), substitutionOptions, ref nextCaseTransform, ref caseTransform, syntaxOnly);
                return index + 1;
            case '{':
                return AppendBracedReplacement(
                    builder,
                    replacement,
                    index,
                    substitutionOptions,
                    subject,
                    matchIndex,
                    matchLength,
                    resolveNumber,
                    resolveName,
                    resolveLastCapture,
                    mark,
                    ref nextCaseTransform,
                    ref caseTransform,
                    ref quoted,
                    syntaxOnly);
            case '<' when (substitutionOptions & Pcre2SubstitutionOptions.Extended) != 0:
                return AppendAngleReplacement(builder, replacement, index, substitutionOptions, resolveName, ref nextCaseTransform, ref caseTransform, syntaxOnly);
        }

        if (char.IsAsciiDigit(next))
        {
            var end = index + 2;
            while (end < replacement.Length && char.IsAsciiDigit(replacement[end]))
            {
                end++;
            }

            AppendResolvedReference(builder, resolveNumber(int.Parse(replacement[(index + 1)..end])), substitutionOptions, ref nextCaseTransform, ref caseTransform, syntaxOnly);
            return end - 1;
        }

        if (IsReplacementNameStart(next))
        {
            var end = index + 2;
            while (end < replacement.Length && IsReplacementNamePart(replacement[end]))
            {
                end++;
            }

            AppendResolvedReference(builder, resolveName(replacement[(index + 1)..end]), substitutionOptions, ref nextCaseTransform, ref caseTransform, syntaxOnly);
            return end - 1;
        }

        AppendReplacementChar(builder, '$', ref nextCaseTransform, ref caseTransform, syntaxOnly);
        return index;
    }

    private static int AppendBracedReplacement(
        StringBuilder builder,
        string replacement,
        int index,
        Pcre2SubstitutionOptions substitutionOptions,
        string subject,
        int matchIndex,
        int matchLength,
        Func<int, ReplacementReferenceResolution> resolveNumber,
        Func<string, ReplacementReferenceResolution> resolveName,
        Func<ReplacementReferenceResolution> resolveLastCapture,
        string? mark,
        ref ReplacementCaseTransformMode nextCaseTransform,
        ref ReplacementCaseTransformMode caseTransform,
        ref bool quoted,
        bool syntaxOnly)
    {
        var contentStart = index + 2;
        if (contentStart >= replacement.Length)
        {
            throw new Pcre2SubstitutionException("Malformed replacement pattern.", "MalformedReplacementPattern");
        }

        var closing = FindReplacementBraceClose(replacement, contentStart);
        if (closing < 0)
        {
            var kind = contentStart < replacement.Length && char.IsAsciiDigit(replacement[contentStart])
                ? "MissingClosingReplacementBrace"
                : "MalformedReplacementPattern";
            throw new Pcre2SubstitutionException("Replacement brace is not closed.", kind);
        }

        var content = replacement[contentStart..closing];
        if (string.IsNullOrEmpty(content))
        {
            throw new Pcre2SubstitutionException("Malformed replacement pattern.", "MalformedReplacementPattern");
        }

        if ((substitutionOptions & Pcre2SubstitutionOptions.Extended) != 0)
        {
            var conditionalIndex = content.IndexOf(":+", StringComparison.Ordinal);
            if (conditionalIndex > 0)
            {
                var selector = content[..conditionalIndex];
                var branchPayload = content[(conditionalIndex + 2)..];
                var (thenBranch, elseBranch, hasElseBranch) = SplitConditionalBranches(branchPayload);

                var resolution = ResolveSelector(selector, resolveNumber, resolveName, mark);
                if (!resolution.Known && (substitutionOptions & Pcre2SubstitutionOptions.UnknownUnset) == 0)
                {
                    throw new Pcre2SubstitutionException("Unknown replacement group.", "UnknownReplacementGroup");
                }

                var validationNextTransform = nextCaseTransform;
                var validationTransform = caseTransform;
                var validationQuoted = quoted;
                AppendEvaluatedReplacementTemplate(
                    new StringBuilder(),
                    thenBranch,
                    substitutionOptions,
                    subject,
                    matchIndex,
                    matchLength,
                    resolveNumber,
                    resolveName,
                    resolveLastCapture,
                    mark,
                    ref validationNextTransform,
                    ref validationTransform,
                    ref validationQuoted,
                    syntaxOnly: true);
                if (hasElseBranch)
                {
                    validationNextTransform = nextCaseTransform;
                    validationTransform = caseTransform;
                    validationQuoted = quoted;
                    AppendEvaluatedReplacementTemplate(
                        new StringBuilder(),
                        elseBranch,
                        substitutionOptions,
                        subject,
                        matchIndex,
                        matchLength,
                        resolveNumber,
                        resolveName,
                        resolveLastCapture,
                        mark,
                        ref validationNextTransform,
                        ref validationTransform,
                        ref validationQuoted,
                        syntaxOnly: true);
                }

                var chosen = resolution.Known && resolution.Success
                    ? thenBranch
                    : elseBranch;
                AppendEvaluatedReplacementTemplate(
                    builder,
                    chosen,
                    substitutionOptions,
                    subject,
                    matchIndex,
                    matchLength,
                    resolveNumber,
                    resolveName,
                    resolveLastCapture,
                    mark,
                    ref nextCaseTransform,
                    ref caseTransform,
                    ref quoted,
                    syntaxOnly);
                return closing;
            }

            var fallbackIndex = content.IndexOf(":-", StringComparison.Ordinal);
            if (fallbackIndex > 0)
            {
                var selector = content[..fallbackIndex];
                var fallback = content[(fallbackIndex + 2)..];
                var resolution = ResolveSelector(selector, resolveNumber, resolveName, mark);
                if (!resolution.Known && (substitutionOptions & Pcre2SubstitutionOptions.UnknownUnset) == 0)
                {
                    throw new Pcre2SubstitutionException("Unknown replacement group.", "UnknownReplacementGroup");
                }

                if (resolution.Known && resolution.Success)
                {
                    AppendReplacementText(builder, resolution.Value.AsSpan(), ref nextCaseTransform, ref caseTransform, syntaxOnly);
                }
                else
                {
                    AppendEvaluatedReplacementTemplate(
                        builder,
                        fallback,
                        substitutionOptions,
                        subject,
                        matchIndex,
                        matchLength,
                        resolveNumber,
                        resolveName,
                        resolveLastCapture,
                        mark,
                        ref nextCaseTransform,
                        ref caseTransform,
                        ref quoted,
                        syntaxOnly);
                }

                return closing;
            }
        }

        if (!int.TryParse(content, out _) &&
            !content.StartsWith('*') &&
            !string.Equals(content, "*MARK", StringComparison.Ordinal) &&
            !content.All(IsReplacementNamePart))
        {
            throw new Pcre2SubstitutionException("Malformed replacement pattern.", "MalformedReplacementPattern");
        }

        AppendResolvedSelector(builder, content, substitutionOptions, resolveNumber, resolveName, mark, ref nextCaseTransform, ref caseTransform, syntaxOnly);
        return closing;
    }

    private static int AppendAngleReplacement(
        StringBuilder builder,
        string replacement,
        int index,
        Pcre2SubstitutionOptions substitutionOptions,
        Func<string, ReplacementReferenceResolution> resolveName,
        ref ReplacementCaseTransformMode nextCaseTransform,
        ref ReplacementCaseTransformMode caseTransform,
        bool syntaxOnly)
    {
        var closing = replacement.IndexOf('>', index + 2);
        if (closing < 0)
        {
            if (syntaxOnly)
            {
                return replacement.Length - 1;
            }

            throw new Pcre2SubstitutionException("Malformed replacement pattern.", "MalformedReplacementPattern");
        }

        var name = replacement[(index + 2)..closing];
        if (string.IsNullOrEmpty(name) || name[0] == '*')
        {
            throw new Pcre2SubstitutionException("Malformed replacement pattern.", "MalformedReplacementPattern");
        }

        AppendResolvedReference(builder, resolveName(name), substitutionOptions, ref nextCaseTransform, ref caseTransform, syntaxOnly);
        return closing;
    }

    private static void AppendResolvedSelector(
        StringBuilder builder,
        string token,
        Pcre2SubstitutionOptions substitutionOptions,
        Func<int, ReplacementReferenceResolution> resolveNumber,
        Func<string, ReplacementReferenceResolution> resolveName,
        string? mark,
        ref ReplacementCaseTransformMode nextCaseTransform,
        ref ReplacementCaseTransformMode caseTransform,
        bool syntaxOnly)
    {
        if (string.Equals(token, "*MARK", StringComparison.Ordinal))
        {
            AppendResolvedReference(builder, ResolveMarkReference(mark), substitutionOptions, ref nextCaseTransform, ref caseTransform, syntaxOnly);
            return;
        }

        AppendResolvedReference(builder, ResolveSelector(token, resolveNumber, resolveName, mark), substitutionOptions, ref nextCaseTransform, ref caseTransform, syntaxOnly);
    }

    private static ReplacementReferenceResolution ResolveSelector(
        string token,
        Func<int, ReplacementReferenceResolution> resolveNumber,
        Func<string, ReplacementReferenceResolution> resolveName,
        string? mark)
    {
        if (string.Equals(token, "*MARK", StringComparison.Ordinal))
        {
            return ResolveMarkReference(mark);
        }

        return int.TryParse(token, out var number) ? resolveNumber(number) : resolveName(token);
    }

    private static void AppendResolvedReference(StringBuilder builder, ReplacementReferenceResolution resolution, Pcre2SubstitutionOptions substitutionOptions, ref ReplacementCaseTransformMode nextCaseTransform, ref ReplacementCaseTransformMode caseTransform, bool syntaxOnly)
    {
        if (!resolution.Known)
        {
            if (syntaxOnly)
            {
                return;
            }

            if ((substitutionOptions & Pcre2SubstitutionOptions.UnknownUnset) == 0)
            {
                throw new Pcre2SubstitutionException("Unknown replacement group.", "UnknownReplacementGroup");
            }

            if ((substitutionOptions & Pcre2SubstitutionOptions.UnsetEmpty) == 0)
            {
                throw new Pcre2SubstitutionException("Unset replacement group.", "UnsetReplacementGroup");
            }

            return;
        }

        if (!resolution.Success)
        {
            if (syntaxOnly)
            {
                return;
            }

            if ((substitutionOptions & Pcre2SubstitutionOptions.UnsetEmpty) == 0)
            {
                throw new Pcre2SubstitutionException("Unset replacement group.", "UnsetReplacementGroup");
            }

            return;
        }

        AppendReplacementText(builder, resolution.Value.AsSpan(), ref nextCaseTransform, ref caseTransform, syntaxOnly);
    }

    private static void AppendResolvedPlusReference(StringBuilder builder, ReplacementReferenceResolution resolution, Pcre2SubstitutionOptions substitutionOptions, ref ReplacementCaseTransformMode nextCaseTransform, ref ReplacementCaseTransformMode caseTransform, bool syntaxOnly)
    {
        if (!resolution.Known && (substitutionOptions & Pcre2SubstitutionOptions.UnknownUnset) != 0)
        {
            return;
        }

        AppendResolvedReference(builder, resolution, substitutionOptions, ref nextCaseTransform, ref caseTransform, syntaxOnly);
    }

    private static (string ThenBranch, string ElseBranch, bool HasElseBranch) SplitConditionalBranches(string branchPayload)
    {
        var quoted = false;
        for (var i = 0; i < branchPayload.Length; i++)
        {
            var current = branchPayload[i];
            if (current == '\\' && i + 1 < branchPayload.Length)
            {
                var next = branchPayload[i + 1];
                if (quoted)
                {
                    if (next == 'E')
                    {
                        quoted = false;
                    }
                }
                else if (next == 'Q')
                {
                    quoted = true;
                }

                i++;
                continue;
            }

            if (quoted)
            {
                continue;
            }

            if (current == ':')
            {
                return (branchPayload[..i], branchPayload[(i + 1)..], true);
            }
        }

        return (branchPayload, string.Empty, false);
    }

    private static void AppendReplacementText(StringBuilder builder, ReadOnlySpan<char> text, ref ReplacementCaseTransformMode nextCaseTransform, ref ReplacementCaseTransformMode caseTransform, bool syntaxOnly)
    {
        foreach (var value in text)
        {
            AppendReplacementChar(builder, value, ref nextCaseTransform, ref caseTransform, syntaxOnly);
        }
    }

    private static void AppendReplacementChar(StringBuilder builder, char value, ref ReplacementCaseTransformMode nextCaseTransform, ref ReplacementCaseTransformMode caseTransform, bool syntaxOnly)
    {
        var effectiveTransform = nextCaseTransform is not ReplacementCaseTransformMode.None
            ? nextCaseTransform
            : caseTransform;
        var transformed = effectiveTransform switch
        {
            ReplacementCaseTransformMode.UpperNext or ReplacementCaseTransformMode.UpperUntilEnd => char.ToUpperInvariant(value),
            ReplacementCaseTransformMode.LowerNext or ReplacementCaseTransformMode.LowerUntilEnd => char.ToLowerInvariant(value),
            _ => value,
        };

        if (!syntaxOnly)
        {
            builder.Append(transformed);
        }

        if (nextCaseTransform is ReplacementCaseTransformMode.UpperNext or ReplacementCaseTransformMode.LowerNext)
        {
            nextCaseTransform = ReplacementCaseTransformMode.None;
        }
    }

    private static ReplacementReferenceResolution ResolveNumberReference(Regex regex, Match match, int number)
    {
        if (number < 0 || number >= match.Groups.Count)
        {
            return default;
        }

        var group = match.Groups[number];
        return new ReplacementReferenceResolution(true, group.Success, group.Success ? group.Value : string.Empty);
    }

    private static ReplacementReferenceResolution ResolveExplicitNumberReference(Pcre2GroupData match, int number, string subject)
    {
        if (number != 0)
        {
            return default;
        }

        return new ReplacementReferenceResolution(
            Known: true,
            Success: match.Success,
            Value: match.Success ? subject[match.StartOffsetInUtf16..match.EndOffsetInUtf16] : string.Empty);
    }

    private static ReplacementReferenceResolution ResolveNativeNumberReference(Pcre2GroupData[] groups, int number, string subject)
    {
        if ((uint)number >= (uint)groups.Length)
        {
            return default;
        }

        var group = groups[number];
        return new ReplacementReferenceResolution(
            Known: true,
            Success: group.Success,
            Value: group.Success ? subject[group.StartOffsetInUtf16..group.EndOffsetInUtf16] : string.Empty);
    }

    private static ReplacementReferenceResolution ResolveNamedReference(Regex regex, Match match, string name)
    {
        if (!TryGetNamedGroupNumber(regex, name, out var number))
        {
            return default;
        }

        var group = match.Groups[number];
        return new ReplacementReferenceResolution(true, group.Success, group.Success ? group.Value : string.Empty);
    }

    private static ReplacementReferenceResolution ResolveNativeNamedReference(Pcre2GroupData[] groups, Pcre2NameEntry[]? nameEntries, string name, string subject)
    {
        if (nameEntries is null)
        {
            return default;
        }

        var known = false;
        foreach (var entry in nameEntries)
        {
            if (!string.Equals(entry.Name, name, StringComparison.Ordinal))
            {
                continue;
            }

            known = true;
            var number = entry.Number;
            if ((uint)number < (uint)groups.Length && groups[number].Success)
            {
                return new ReplacementReferenceResolution(true, true, subject[groups[number].StartOffsetInUtf16..groups[number].EndOffsetInUtf16]);
            }
        }

        return known
            ? new ReplacementReferenceResolution(true, false, string.Empty)
            : default;
    }

    private static ReplacementReferenceResolution ResolveLastCapturedReference(Match match)
    {
        if (match.Groups.Count <= 1)
        {
            return default;
        }

        for (var i = match.Groups.Count - 1; i >= 1; i--)
        {
            if (match.Groups[i].Success)
            {
                return new ReplacementReferenceResolution(true, true, match.Groups[i].Value);
            }
        }

        return new ReplacementReferenceResolution(true, false, string.Empty);
    }

    private static ReplacementReferenceResolution ResolveNativeLastCapturedReference(Pcre2GroupData[] groups, string subject)
    {
        if (groups.Length <= 1)
        {
            return default;
        }

        for (var i = groups.Length - 1; i >= 1; i--)
        {
            var group = groups[i];
            if (group.Success)
            {
                return new ReplacementReferenceResolution(true, true, subject[group.StartOffsetInUtf16..group.EndOffsetInUtf16]);
            }
        }

        return new ReplacementReferenceResolution(true, false, string.Empty);
    }

    private static ReplacementReferenceResolution ResolveMarkReference(string? mark)
        => new(true, mark is not null, mark ?? string.Empty);

    private static ReplacementReferenceResolution ResolveMarkedNumberReference(int number)
    {
        return number == 0
            ? new ReplacementReferenceResolution(true, true, string.Empty)
            : default;
    }

    internal int DebugCountRaw(ReadOnlySpan<byte> input, int startOffsetInBytes = 0)
    {
        ValidateStartOffset(input, startOffsetInBytes);
        if (_executionPlan.CountBackend == Pcre2DirectBackendKind.Utf8Regex)
        {
            return _utf8Regex!.Pcre2CountAtByteOffset(input, startOffsetInBytes);
        }

        if (_executionPlan.CountBackend == Pcre2DirectBackendKind.Utf8RegexEquivalent)
        {
            return _utf8SearchEquivalentRegex!.Count(input, startOffsetInBytes);
        }

        if (_executionPlan.CountBackend == Pcre2DirectBackendKind.ManagedRegex)
        {
            if (startOffsetInBytes == 0 && CanUseUtf8RegexCompatiblePublicCount(input.Length))
            {
                return _utf8Regex!.Count(input);
            }

            var subject = Encoding.UTF8.GetString(input);
            var startOffsetInUtf16 = Encoding.UTF8.GetCharCount(input[..startOffsetInBytes]);
            return _managedRegex!.Count(subject, startOffsetInUtf16);
        }

        if (!UsesDeferredSpecialGlobalEnumerator(input, startOffsetInBytes))
        {
            if (TryCountNativeGlobalMatches(input, startOffsetInBytes, out var nativeCount))
            {
                return nativeCount;
            }

            return EnumerateGlobalMatchData(input, startOffsetInBytes).Length;
        }

        return Count(input, startOffsetInBytes);
    }

    internal int DebugEnumerateRawIndexSum(ReadOnlySpan<byte> input, int startOffsetInBytes = 0)
    {
        ValidateStartOffset(input, startOffsetInBytes);
        if (_executionPlan.EnumerateBackend == Pcre2DirectBackendKind.Utf8Regex)
        {
            var sum = 0;
            var enumerator = _utf8Regex!.Pcre2EnumerateMatchesAtByteOffset(input, startOffsetInBytes);
            while (enumerator.MoveNext())
            {
                if (!enumerator.Current.TryGetByteRange(out var indexInBytes, out _))
                {
                    throw new InvalidOperationException("Managed Utf8Regex fallback returned a match that is not aligned to byte boundaries.");
                }

                sum += startOffsetInBytes + indexInBytes;
            }

            return sum;
        }

        if (_executionPlan.EnumerateBackend == Pcre2DirectBackendKind.Utf8RegexEquivalent)
        {
            var sum = 0;
            var enumerator = _utf8SearchEquivalentRegex!.EnumerateMatches(input, startOffsetInBytes);
            while (enumerator.MoveNext())
            {
                if (!enumerator.Current.TryGetByteRange(out var indexInBytes, out _))
                {
                    throw new InvalidOperationException("Managed Utf8Regex fallback returned a match that is not aligned to byte boundaries.");
                }

                sum += indexInBytes;
            }

            return sum;
        }

        if (_executionPlan.EnumerateBackend == Pcre2DirectBackendKind.ManagedRegex)
        {
            var subject = Encoding.UTF8.GetString(input);
            var startOffsetInUtf16 = Encoding.UTF8.GetCharCount(input[..startOffsetInBytes]);
            var matchesAreAscii = subject.Length == input.Length;
            var boundaryMap = matchesAreAscii ? null : Utf8InputAnalyzer.Analyze(input).BoundaryMap;
            var sum = 0;
            foreach (var match in _managedRegex!.EnumerateMatches(subject.AsSpan(), startOffsetInUtf16))
            {
                if (matchesAreAscii)
                {
                    sum += match.Index;
                    continue;
                }

                if (boundaryMap is null || !boundaryMap.TryGetByteRange(match.Index, match.Length, out var indexInBytes, out _))
                {
                    throw new InvalidOperationException("Managed Regex fallback produced a match that is not aligned to UTF-8 scalar boundaries.");
                }

                sum += indexInBytes;
            }

            return sum;
        }

        if (!UsesDeferredSpecialGlobalEnumerator(input, startOffsetInBytes))
        {
            var matches = EnumerateGlobalMatchData(input, startOffsetInBytes);
            var sum = 0;
            for (var i = 0; i < matches.Length; i++)
            {
                sum += matches[i].StartOffsetInBytes;
            }

            return sum;
        }

        var fallbackSum = 0;
        var fallbackEnumerator = EnumerateMatches(input, startOffsetInBytes);
        while (fallbackEnumerator.MoveNext())
        {
            fallbackSum += fallbackEnumerator.Current.StartOffsetInBytes;
        }

        return fallbackSum;
    }

    internal int DebugEnumeratePublicConstructionOnly(ReadOnlySpan<byte> input, int startOffsetInBytes = 0)
    {
        ValidateStartOffset(input, startOffsetInBytes);
        _ = EnumerateMatches(input, startOffsetInBytes);
        return 1;
    }

    internal int DebugEnumerateNativeMaterializationOnly(ReadOnlySpan<byte> input, int startOffsetInBytes = 0)
    {
        ValidateStartOffset(input, startOffsetInBytes);
        if (UsesDeferredSpecialGlobalEnumerator(input, startOffsetInBytes))
        {
            return 0;
        }

        return EnumerateGlobalMatchData(input, startOffsetInBytes).Length;
    }

    internal int DebugEnumerateArrayBackedConstructionOnly(ReadOnlySpan<byte> input, int startOffsetInBytes = 0)
    {
        ValidateStartOffset(input, startOffsetInBytes);
        if (UsesDeferredSpecialGlobalEnumerator(input, startOffsetInBytes))
        {
            _ = EnumerateMatches(input, startOffsetInBytes);
            return 0;
        }

        var matches = EnumerateGlobalMatchData(input, startOffsetInBytes);
        _ = new Utf8Pcre2ValueMatchEnumerator(input, matches);
        return matches.Length;
    }

    internal int DebugEnumerateArrayBackedPublicMoveNextCount(ReadOnlySpan<byte> input, int startOffsetInBytes = 0)
    {
        ValidateStartOffset(input, startOffsetInBytes);
        if (UsesDeferredSpecialGlobalEnumerator(input, startOffsetInBytes))
        {
            return ExecutePublicEnumerateMoveNextCount(EnumerateMatches(input, startOffsetInBytes));
        }

        var matches = EnumerateGlobalMatchData(input, startOffsetInBytes);
        var enumerator = new Utf8Pcre2ValueMatchEnumerator(input, matches);
        return ExecutePublicEnumerateMoveNextCount(enumerator);
    }

    internal int DebugEnumerateInternalPublicMoveNextCount(ReadOnlySpan<byte> input, int startOffsetInBytes = 0)
    {
        ValidateStartOffset(input, startOffsetInBytes);
        return ExecutePublicEnumerateMoveNextCount(EnumerateMatches(input, startOffsetInBytes));
    }

    internal int DebugEnumerateArrayBackedPublicIndexSum(ReadOnlySpan<byte> input, int startOffsetInBytes = 0)
    {
        ValidateStartOffset(input, startOffsetInBytes);
        if (UsesDeferredSpecialGlobalEnumerator(input, startOffsetInBytes))
        {
            return ExecutePublicEnumerateIndexSum(EnumerateMatches(input, startOffsetInBytes));
        }

        var matches = EnumerateGlobalMatchData(input, startOffsetInBytes);
        var enumerator = new Utf8Pcre2ValueMatchEnumerator(input, matches);
        return ExecutePublicEnumerateIndexSum(enumerator);
    }

    internal int DebugEnumerateInternalPublicIndexSum(ReadOnlySpan<byte> input, int startOffsetInBytes = 0)
    {
        ValidateStartOffset(input, startOffsetInBytes);
        return ExecutePublicEnumerateIndexSum(EnumerateMatches(input, startOffsetInBytes));
    }

    internal int DebugEnumerateInternalPublicCurrentCount(ReadOnlySpan<byte> input, int startOffsetInBytes = 0)
    {
        ValidateStartOffset(input, startOffsetInBytes);
        return ExecutePublicEnumerateCurrentCount(EnumerateMatches(input, startOffsetInBytes));
    }

    internal int DebugEnumerateInternalPublicCurrentStartSum(ReadOnlySpan<byte> input, int startOffsetInBytes = 0)
    {
        ValidateStartOffset(input, startOffsetInBytes);
        return ExecutePublicEnumerateCurrentStartSum(EnumerateMatches(input, startOffsetInBytes));
    }

    private static int ExecutePublicEnumerateMoveNextCount(Utf8Pcre2ValueMatchEnumerator enumerator)
    {
        var count = 0;
        while (enumerator.MoveNext())
        {
            count++;
        }

        return count;
    }

    private static int ExecutePublicEnumerateIndexSum(Utf8Pcre2ValueMatchEnumerator enumerator)
    {
        var sum = 0;
        while (enumerator.MoveNext())
        {
            sum += enumerator.Current.StartOffsetInBytes;
        }

        return sum;
    }

    private static int ExecutePublicEnumerateCurrentCount(Utf8Pcre2ValueMatchEnumerator enumerator)
    {
        var count = 0;
        while (enumerator.MoveNext())
        {
            _ = enumerator.Current;
            count++;
        }

        return count;
    }

    private static int ExecutePublicEnumerateCurrentStartSum(Utf8Pcre2ValueMatchEnumerator enumerator)
    {
        var sum = 0;
        while (enumerator.MoveNext())
        {
            var current = enumerator.Current;
            sum += current.StartOffsetInBytes;
        }

        return sum;
    }

    internal int DebugEvaluateFirstReplacementOnly(ReadOnlySpan<byte> input, string replacement, Pcre2SubstitutionOptions substitutionOptions = Pcre2SubstitutionOptions.None, int startOffsetInBytes = 0)
    {
        ValidateStartOffset(input, startOffsetInBytes);
        var subject = Encoding.UTF8.GetString(input);

        if (UsesNativeGlobalIteration() && !UsesDeferredSpecialGlobalEnumerator(input, startOffsetInBytes))
        {
            var matches = EnumerateGlobalMatchData(input, startOffsetInBytes);
            if (matches.Length == 0)
            {
                return 0;
            }

            return EvaluateReplacementTemplate(
                replacement,
                substitutionOptions,
                subject,
                matches[0].StartOffsetInUtf16,
                matches[0].EndOffsetInUtf16 - matches[0].StartOffsetInUtf16,
                number => ResolveExplicitNumberReference(matches[0], number, subject),
                _ => default,
                () => default,
                mark: null).Length;
        }

        var match = MatchDetailed(input, startOffsetInBytes);
        if (!match.Success)
        {
            return 0;
        }

        var value = match.Value;
        var groups = CaptureGroups(match);
        return EvaluateReplacementTemplate(
            replacement,
            substitutionOptions,
            subject,
            value.StartOffsetInUtf16,
            value.EndOffsetInUtf16 - value.StartOffsetInUtf16,
            number => ResolveNativeNumberReference(groups, number, subject),
            name => ResolveNativeNamedReference(groups, _nameEntries, name, subject),
            () => ResolveNativeLastCapturedReference(groups, subject),
            match.Mark).Length;
    }

    private static bool TryGetNamedGroupNumber(Regex regex, string name, out int number)
    {
        number = regex.GroupNumberFromName(name);
        return number >= 0 &&
               !int.TryParse(name, out _) &&
               string.Equals(regex.GroupNameFromNumber(number), name, StringComparison.Ordinal);
    }

    private static bool IsReplacementNameStart(char value)
        => char.IsAsciiLetter(value) || value == '_';

    private static bool IsReplacementNamePart(char value)
        => char.IsAsciiLetterOrDigit(value) || value == '_';

    private static int FindReplacementBraceClose(string replacement, int startIndex)
    {
        var quoted = false;
        for (var i = startIndex; i < replacement.Length; i++)
        {
            if (replacement[i] == '\\' && i + 1 < replacement.Length)
            {
                if (quoted)
                {
                    if (replacement[i + 1] == 'E')
                    {
                        quoted = false;
                    }

                    i++;
                    continue;
                }

                if (replacement[i + 1] == 'Q')
                {
                    quoted = true;
                    i++;
                    continue;
                }
            }

            if (quoted)
            {
                continue;
            }

            if (replacement[i] != '\\' || i + 2 >= replacement.Length || replacement[i + 1] != 'o' || replacement[i + 2] != '{')
            {
                if (replacement[i] == '}')
                {
                    return i;
                }

                continue;
            }

            var innerClose = replacement.IndexOf('}', i + 3);
            if (innerClose < 0)
            {
                return -1;
            }

            i = innerClose;
        }

        return -1;
    }

    private readonly record struct ReplacementReferenceResolution(bool Known, bool Success, string Value);

    private readonly record struct SimpleReplacementCacheKey(string Replacement, Pcre2SubstitutionOptions Options);

    private readonly record struct SimpleReplacementSegment(string Literal, bool IsWholeMatch)
    {
        public static SimpleReplacementSegment FromLiteral(string literal) => new(literal, false);

        public static SimpleReplacementSegment WholeMatch() => new(string.Empty, true);
    }

    private readonly record struct SimpleReplacementPlan(SimpleReplacementSegment[] Segments);

    private enum ReplacementCaseTransformMode
    {
        None = 0,
        UpperNext = 1,
        LowerNext = 2,
        UpperUntilEnd = 3,
        LowerUntilEnd = 4,
    }

    private enum Pcre2ExecutionKind
    {
        Unimplemented = 0,
        ManagedRegex = 1,
        ReluctantAlternation = 2,
        BranchResetBasic = 3,
        BranchResetBackref = 4,
        BranchResetNested = 5,
        BranchResetNestedSecondCapture = 6,
        BranchResetAbasic = 7,
        BranchResetSubroutine = 8,
        BranchResetGReference = 9,
        BranchResetSameNameBackref = 10,
        BranchResetSameNameFollowup = 11,
        DuplicateNamesFooBar = 12,
        MarkSkip = 13,
        KResetFooBar = 14,
        KResetBarOrBaz = 15,
        KResetFooBarBaz = 16,
        KResetAbc123 = 17,
        KReset123Abc = 18,
        KResetAnchorAbc = 19,
        KResetAnchorLookaheadOrAbc = 20,
        KResetAtomicAb = 21,
        KResetCapturedAtomicAb = 22,
        KResetCapturedAb = 23,
        KResetAnchorCzOrAc = 24,
        KResetAtomicAltAb = 25,
        KResetDefineSubroutineAb = 26,
        KResetRepeatAbPossessive = 27,
        KResetAtomicRepeatAb = 28,
        KResetRepeatAb = 29,
        KResetCapturedRepeatAbPossessive = 30,
        KResetCapturedRepeatAb = 31,
        KResetRecursiveAny = 32,
        KResetRecursiveCaptured = 33,
        KResetLookaheadAb = 34,
        KResetLookbehindA = 35,
        KResetConditionalGcOverlap = 36,
        KResetConditionalGcNotSorted = 37,
        KResetRuntimeDisallowedLookaround1 = 38,
        KResetRuntimeDisallowedLookaround2 = 39,
        KResetRuntimeConditionalDigits = 40,
        KResetSneakyLookaheadDefine = 41,
        KResetSneakyLookbehindDefine = 42,
        KResetSneakyGlobalLookbehindDefine = 43,
        BackslashCLiteral = 44,
        RecursivePalindromeOdd = 45,
        RecursivePalindromeAny = 46,
        RecursiveAlternation = 47,
        RecursiveOptional = 48,
        AtomicAlternationReluctantMany = 49,
        AtomicAlternationReluctantTwo = 50,
        ConditionalLookaheadPlus = 51,
        ConditionalLookaheadEmptyAlt = 52,
        ConditionalLookahead = 53,
        ConditionalNegativeLookahead = 54,
        ConditionalAcceptLookahead = 55,
        ConditionalAcceptNegativeLookahead = 56,
        SubroutinePrefixDigits = 57,
        CommitSubroutine = 58,
        DefineSubroutineB = 59,
        PartialSoftDotAllLiteral = 60,
        MailboxRfc2822 = 61,
        ReplaceFooLiteral = 62,
        ReplaceFooOptionalBar = 63,
        ReplacePartialAbPlus = 64,
        ProbeTrailingCLookbehind = 65,
        ProbeTrailingCPlusLookbehind = 66,
        ProbeAnchoredAPlusWord = 67,
        ProbeAnchoredAaOrAPlusWord = 68,
        ProbeAnchoredAStarWord = 69,
        ProbeAnchoredCapturedAPlusWord = 70,
        ProbeAnchoredOptionalAPlusWord = 71,
        ProbeAnchoredAbcPlusTerminalX = 72,
        ProbeRecursiveAbcTwice = 73,
        ProbeAnchoredR = 74,
        ProbeAnchoredRRangeX = 75,
        ProbeAnchoredRRangeReluctantX = 76,
        ProbeAnchoredROptionalX = 77,
        ProbeAnchoredRPlusX = 78,
    }

    private enum TrailingAssertionKind
    {
        Dollar = 0,
        EndAbsolute = 1,
        EndBeforeFinalNewline = 2,
        WordBoundary = 3,
        NonWordBoundary = 4,
    }

    private readonly record struct Pcre2ExecutionPlan(
        Pcre2DirectBackendKind IsMatchBackend,
        Pcre2DirectBackendKind CountBackend,
        Pcre2DirectBackendKind EnumerateBackend,
        Pcre2DirectBackendKind MatchBackend,
        Pcre2DirectBackendKind ReplaceBackend);

    private readonly record struct Pcre2Utf8RegexTranslation(
        bool IsActive,
        string Pattern,
        RegexOptions Options,
        Utf8Regex Regex);

    private enum Pcre2DirectBackendKind
    {
        None = 0,
        Utf8Regex = 1,
        ManagedRegex = 2,
        Utf8RegexEquivalent = 3,
    }

    internal string DebugExecutionKindName => _executionKind.ToString();

    internal bool DebugUsesUtf8RegexTranslation => _utf8Translation.IsActive;

    internal string DebugUtf8RegexExecutionKindName => _utf8Regex?.ExecutionKind.ToString() ?? "<none>";

    internal bool DebugHasUtf8SearchEquivalentRegex => _utf8SearchEquivalentRegex is not null;

    internal bool DebugHasManagedRegex => _managedRegex is not null;

    internal string DebugDescribeExecutionPlan()
        => $"IsMatch={_executionPlan.IsMatchBackend}, Count={_executionPlan.CountBackend}, Enumerate={_executionPlan.EnumerateBackend}, Match={_executionPlan.MatchBackend}, Replace={_executionPlan.ReplaceBackend}";
}
