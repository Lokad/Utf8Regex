using Lokad.Utf8Regex.Internal.Caching;
using Lokad.Utf8Regex.Internal.Diagnostics;
using Lokad.Utf8Regex.Internal.Search;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.FrontEnd;
using Lokad.Utf8Regex.Internal.Input;
using Lokad.Utf8Regex.Internal.Planning;
using Lokad.Utf8Regex.Internal.Replacement;
using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Text.Unicode;

namespace Lokad.Utf8Regex;

internal enum Utf8PrioritizedBooleanFamilyKind : byte
{
    None = 0,
    LeadingAnyRunTrailingAsciiLiteral = 1,
    AnchoredBoundedDate = 2,
    CompiledAnchoredOptionalField = 3,
    AsciiBoundedDateToken = 4,
    AnchoredAsciiLeadingDigitsTail = 5,
    CompiledRepeatedDigitGroup = 6,
    AnchoredAsciiSignedDecimalWhole = 7,
    AnchoredAsciiOptionalFieldWhole = 8,
    AsciiUriToken = 9,
    AsciiIpv4Token = 10,
}

/// <summary>Matches .NET regular-expression semantics directly over well-formed UTF-8 input.</summary>
/// <remarks>Patterns and coordinates use .NET UTF-16 semantics; value APIs additionally expose scalar-aligned ranges in the original bytes.</remarks>
public sealed partial class Utf8Regex
{
    private static TimeSpan s_defaultMatchTimeout = Regex.InfiniteMatchTimeout;

    private readonly Utf8RegexProgram _program;
    private readonly Utf8PrioritizedBooleanFamilyKind _prioritizedBooleanFamilyKind;
    private readonly bool _prioritizePlainAsciiLiteralCount;
    private readonly int _fusedAsciiLiteralCountAnchorOffset;
    private readonly bool _prioritizeBoundaryLiteralFamilyCount;
    private readonly Utf8EmittedLiteralFamilyCounter? _fusedAsciiBoundaryLiteralFamilyCounter;
    private readonly bool _prioritizeOrderedLiteralWindowCount;
    private readonly bool _requiresKelvinSignFallback;
    private readonly Utf8InvariantCyrillicLiteralCountStrategy? _invariantCyrillicLiteralCountStrategy;
    private readonly Utf8PackedNibbleLiteralFamilyCountStrategy? _packedNibbleLiteralFamilyCountStrategy;
    private readonly Utf8LiteralFamilyCaptureMap? _literalFamilyCaptureMap;
    private readonly Utf8GroupedLiteralSpanTable? _groupedLiteralSpanTable;
    private readonly Utf8SliceProgram? _sliceProgram;
    private readonly Utf8ReplacementPlanCache _replacementCache = new();

    /// <summary>Compiles a culture-invariant expression with <see cref="DefaultMatchTimeout"/>.</summary>
    public Utf8Regex(string pattern)
        : this(pattern, RegexOptions.CultureInvariant, DefaultMatchTimeout)
    {
    }

    /// <summary>Compiles an expression with the specified .NET regex options and <see cref="DefaultMatchTimeout"/>.</summary>
    public Utf8Regex(string pattern, RegexOptions options)
        : this(pattern, options, DefaultMatchTimeout)
    {
    }

    /// <summary>Compiles an expression with the specified options, match timeout, and invocation resource policy.</summary>
    /// <exception cref="ArgumentOutOfRangeException">A policy budget is zero; use null for unlimited.</exception>
    /// <exception cref="NotSupportedException">A work, depth, working-bytes, or result-bytes budget is set. This profile enforces only match timeouts.</exception>
    public Utf8Regex(string pattern, RegexOptions options, TimeSpan matchTimeout, Utf8ResourcePolicy resourcePolicy)
        : this(pattern, options, matchTimeout, resourcePolicy, checkPolicy: true)
    {
    }

    /// <summary>Compiles an expression with the specified .NET regex options and match timeout.</summary>
    public Utf8Regex(string pattern, RegexOptions options, TimeSpan matchTimeout)
        : this(pattern, options, matchTimeout, Utf8ResourcePolicy.Unlimited, checkPolicy: false)
    {
    }

    /// <summary>Shared construction worker. Validates the policy before any parsing or preparation when requested.</summary>
    private Utf8Regex(string pattern, RegexOptions options, TimeSpan matchTimeout, Utf8ResourcePolicy resourcePolicy, bool checkPolicy)
    {
        if (checkPolicy)
        {
            ValidateCorePolicy(resourcePolicy, nameof(resourcePolicy));
        }

        Utf8MatchTimeout.Validate(matchTimeout, nameof(matchTimeout));
        ValidateOptions(options);
        Pattern = pattern;
        Options = options;
        MatchTimeout = matchTimeout;

        _program = Utf8RegexProgram.Compile(pattern, options, matchTimeout);
        var preparedRegex = _program.PreparedRegex;
        _prioritizedBooleanFamilyKind = SelectPrioritizedBooleanFamily(preparedRegex, options);
        _prioritizePlainAsciiLiteralCount = CanPrioritizePlainAsciiLiteralCount(pattern, preparedRegex, options);
        _fusedAsciiLiteralCountAnchorOffset =
            _prioritizePlainAsciiLiteralCount &&
            matchTimeout == Regex.InfiniteMatchTimeout &&
            preparedRegex.LiteralUtf8 is { } fusedAsciiLiteral
                ? Utf8AsciiExactLiteralCountExecutor.SelectRarestAnchorOffset(fusedAsciiLiteral)
                : -1;
        _prioritizeBoundaryLiteralFamilyCount =
            (options & RegexOptions.RightToLeft) == 0 &&
            (Utf8EmittedLiteralFamilyCounter.CanCreateBoundaryOnlyStructuralFamily(preparedRegex) ||
             preparedRegex.ExecutionKind == NativeExecutionKind.ExactUtf8Literals &&
             preparedRegex.SearchPlan.HasBoundaryRequirements &&
             Utf8EmittedLiteralFamilyCounter.CanCreate(
                 preparedRegex.SearchPlan,
                 preparedRegex.SearchPlan.CountOperation,
                 preparedRegex.SearchPlan.FirstMatchOperation));
        if (matchTimeout == Regex.InfiniteMatchTimeout &&
            _program.CompiledEngineRuntime is Utf8StructuralLinearAutomatonCompiledEngineRuntime structuralRuntime &&
            structuralRuntime.EmittedBoundaryLiteralFamily is { SupportsAsciiValidatedCount: true } boundaryLiteralFamilyCounter)
        {
            _fusedAsciiBoundaryLiteralFamilyCounter = boundaryLiteralFamilyCounter;
        }
        _prioritizeOrderedLiteralWindowCount =
            preparedRegex.ExecutionKind == NativeExecutionKind.AsciiOrderedLiteralWindow;
        _requiresKelvinSignFallback = RequiresKelvinSignFallback(_program.PreparedRegex);
        _invariantCyrillicLiteralCountStrategy =
            Utf8InvariantCyrillicLiteralCountStrategy.TryCreate(
                pattern,
                options,
                matchTimeout,
                out var invariantCyrillicLiteralCountStrategy)
                    ? invariantCyrillicLiteralCountStrategy
                    : null;
        _packedNibbleLiteralFamilyCountStrategy =
            Utf8PackedNibbleLiteralFamilyCountStrategy.TryCreate(
                preparedRegex,
                options,
                matchTimeout,
                out var packedNibbleLiteralFamilyCountStrategy)
                    ? packedNibbleLiteralFamilyCountStrategy
                    : null;
        _literalFamilyCaptureMap = Utf8LiteralFamilyCaptureMap.TryCreate(preparedRegex.ExecutionTree);
        _groupedLiteralSpanTable = UsesRightToLeft() ? null : Utf8GroupedLiteralSpanTable.TryCreate(preparedRegex.ExecutionTree);
        // R02a: fallback-shaped literal/concat/alt/repeat/capture patterns run the owned
        // slice (R03 adds dots). R02b construction admission caps the pattern length and lowered
        // instruction count; over-cap shapes decline here to legacy routing.
        _sliceProgram = preparedRegex.ExecutionKind == NativeExecutionKind.FallbackRegex && !RequiresCultureSensitiveFallback
            ? Utf8SliceParser.TryParse(pattern, options)
            : null;
        // R03 lookbehind: the owned front end leaves no runtime tree for
        // captures in lookbehind, which defers group metadata to first use.
        // Invalid patterns must still fail construction exactly like the
        // BCL-backed route, so when neither the tree nor the slice serves
        // the pattern, resolve group numbers now (BCL-invalid shapes throw
        // here; BCL-valid legacy shapes keep current eager behavior).
        if (_sliceProgram is null && preparedRegex.SemanticRegex.RuntimeTree is null)
        {
            _ = _program.GroupNumbers.Value;
        }

        // NonBacktracking construction validates through the fallback engine: BCL
        // throws NotSupportedException at construction for unsupported features
        // (backreferences, lookarounds, balancing, conditionals, atomics, \G),
        // so the fallback regex builds eagerly here to preserve failure timing.
        // Supported shapes pay one early build they would incur on first use;
        // malformed patterns already failed above through the front end.
        if ((options & RegexOptions.NonBacktracking) != 0)
        {
            _ = _verifierRuntime.FallbackCandidateVerifier.FallbackRegex;
        }

        // Declined conditionals validate the same way: BCL rejects malformed
        // conditionals (extra branches, undefined numbers) at construction while
        // lazy fallback would defer the failure to first use. The substring gate
        // is intentionally heuristic (whitespace-split (? ( slips through); only
        // declined shapes pay the early build, and owned conditionals keep lazy
        // construction. Malformed patterns already failed above through the front end.
        if (_sliceProgram is null && pattern.Contains("(?(", StringComparison.Ordinal))
        {
            _ = _verifierRuntime.FallbackCandidateVerifier.FallbackRegex;
        }

        static bool CanPrioritizePlainAsciiLiteralCount(
            string pattern,
            Utf8PreparedRegex preparedRegex,
            RegexOptions options)
        {
            if (preparedRegex.ExecutionKind != NativeExecutionKind.ExactAsciiLiteral ||
                (options & RegexOptions.RightToLeft) != 0 ||
                preparedRegex.LiteralUtf8 is not { } literal ||
                pattern.Length != literal.Length)
            {
                return false;
            }

            for (var i = 0; i < literal.Length; i++)
            {
                if (pattern[i] != literal[i])
                {
                    return false;
                }
            }

            return true;
        }

        static Utf8PrioritizedBooleanFamilyKind SelectPrioritizedBooleanFamily(
            Utf8PreparedRegex preparedRegex,
            RegexOptions options)
        {
            if (preparedRegex.FallbackDirectFamily.Kind == Utf8FallbackDirectFamilyKind.LeadingAnyRunTrailingAsciiLiteral)
            {
                return Utf8PrioritizedBooleanFamilyKind.LeadingAnyRunTrailingAsciiLiteral;
            }

            if (preparedRegex.SimplePatternPlan.AnchoredBoundedDatePlan.HasValue)
            {
                return Utf8PrioritizedBooleanFamilyKind.AnchoredBoundedDate;
            }

            if ((options & RegexOptions.Compiled) != 0)
            {
                if (preparedRegex.SimplePatternPlan.AnchoredOptionalFieldPlan.HasValue)
                {
                    return Utf8PrioritizedBooleanFamilyKind.CompiledAnchoredOptionalField;
                }

                if (preparedRegex.SimplePatternPlan.RepeatedDigitGroupPlan.HasValue)
                {
                    return Utf8PrioritizedBooleanFamilyKind.CompiledRepeatedDigitGroup;
                }
            }

            if (preparedRegex.SimplePatternPlan.AnchoredOptionalFieldPlan.CanUseShortAsciiWholeMatcher)
            {
                return Utf8PrioritizedBooleanFamilyKind.AnchoredAsciiOptionalFieldWhole;
            }

            return preparedRegex.FallbackDirectFamily.Kind switch
            {
                Utf8FallbackDirectFamilyKind.AnchoredAsciiSignedDecimalWhole =>
                    Utf8PrioritizedBooleanFamilyKind.AnchoredAsciiSignedDecimalWhole,
                Utf8FallbackDirectFamilyKind.Utf8UriToken =>
                    Utf8PrioritizedBooleanFamilyKind.AsciiUriToken,
                Utf8FallbackDirectFamilyKind.AsciiIpv4Token =>
                    Utf8PrioritizedBooleanFamilyKind.AsciiIpv4Token,
                Utf8FallbackDirectFamilyKind.AsciiDottedDecimalQuadCount =>
                    Utf8PrioritizedBooleanFamilyKind.AsciiIpv4Token,
                Utf8FallbackDirectFamilyKind.AsciiBoundedDateToken =>
                    Utf8PrioritizedBooleanFamilyKind.AsciiBoundedDateToken,
                Utf8FallbackDirectFamilyKind.AnchoredAsciiLeadingDigitsTail =>
                    Utf8PrioritizedBooleanFamilyKind.AnchoredAsciiLeadingDigitsTail,
                _ => Utf8PrioritizedBooleanFamilyKind.None,
            };
        }

        ResourcePolicy = resourcePolicy;
    }

    private static void ValidateCorePolicy(Utf8ResourcePolicy resourcePolicy, string paramName)
    {
        resourcePolicy.Validate(paramName);
        if (resourcePolicy.MaxWorkSteps.HasValue || resourcePolicy.MaxDepth.HasValue || resourcePolicy.MaxWorkingBytes.HasValue || resourcePolicy.MaxResultBytes.HasValue)
        {
            throw new NotSupportedException("This profile enforces only match timeouts; work, depth, and memory budgets are not supported here.");
        }
    }

    /// <summary>Gets or sets the timeout used by constructors and cached operations that do not specify one.</summary>
    /// <remarks>The initial value is <see cref="Regex.InfiniteMatchTimeout"/>.</remarks>
    public static TimeSpan DefaultMatchTimeout
    {
        get => s_defaultMatchTimeout;
        set
        {
            s_defaultMatchTimeout = Utf8MatchTimeout.Validate(value, nameof(value));
        }
    }

    internal static TimeSpan DefaultTimeout => DefaultMatchTimeout;

    /// <summary>Gets the regular-expression pattern.</summary>
    public string Pattern { get; }

    /// <summary>Gets the .NET regular-expression options used during compilation.</summary>
    public RegexOptions Options { get; }

    /// <summary>Gets the maximum duration of an individual matching operation.</summary>
    public TimeSpan MatchTimeout { get; }

    /// <summary>Gets the invocation resource policy.</summary>
    public Utf8ResourcePolicy ResourcePolicy { get; }

    private Utf8PreparedRegex _preparedRegex => _program.PreparedRegex;

    private Utf8CompiledEngine _compiledEngine => _program.CompiledEngine;

    private Utf8VerifierRuntime _verifierRuntime => _program.VerifierRuntime;

    private Utf8CompiledEngineRuntime _compiledEngineRuntime => _program.CompiledEngineRuntime;

    private Utf8AsciiCultureInvariantStrategy? _asciiCultureInvariantStrategy =>
        _program.AsciiCultureInvariantStrategy;

    // PLAN.md C02: true when the construction culture folds ASCII case-sensitively
    // (Turkic dotted/dotless I). No invariant-folding native kernel may answer then.
    private bool RequiresCultureSensitiveFallback => _program.RequiresCultureSensitiveFallback;

    private Utf8EmittedKernelMatcher? _directStructuralFamilyKernelMatcher =>
        _program.DirectStructuralFamilyKernelMatcher;

    private int[] _groupNumbers => _sliceProgram is not null ? _sliceProgram.OrdinalGroupNumbers : _program.GroupNumbers.Value;

    private string[] _groupNames => _sliceProgram is not null ? _sliceProgram.GroupNames : _program.GroupNames.Value;

    private bool _allowsTrailingNewlineBeforeEnd =>
        _preparedRegex.SimplePatternPlan.AllowsTrailingNewlineBeforeEnd;

    private AsciiSimplePatternAnchoredHeadTailRunPlan _anchoredHeadTailRunPlan =>
        _preparedRegex.SimplePatternPlan.AnchoredHeadTailRunPlan;

    private AsciiSimplePatternAnchoredValidatorPlan _anchoredValidatorPlan =>
        _preparedRegex.SimplePatternPlan.AnchoredValidatorPlan;

    private AsciiSimplePatternAnchoredBoundedDatePlan _anchoredBoundedDatePlan =>
        _preparedRegex.SimplePatternPlan.AnchoredBoundedDatePlan;

    private AsciiSimplePatternAnchoredOptionalFieldPlan _anchoredOptionalFieldPlan =>
        _preparedRegex.SimplePatternPlan.AnchoredOptionalFieldPlan;

    private ref readonly Utf8FallbackDirectFamilyPlan _fallbackDirectFamily =>
        ref _preparedRegex.FallbackDirectFamily;

    private bool _hasDirectAnchoredHeadTailWithoutValidation =>
        _preparedRegex.ExecutionKind == NativeExecutionKind.AsciiSimplePattern &&
        _anchoredHeadTailRunPlan.HasValue;

    private bool _hasDirectAnchoredValidatorWithoutValidation =>
        _preparedRegex.ExecutionKind == NativeExecutionKind.AsciiSimplePattern &&
        _anchoredValidatorPlan.HasValue;

    private bool _hasDirectFallbackTokenFamilyWithoutValidation =>
        Utf8FallbackDirectFamilyCategories.IsTokenFindFamily(_fallbackDirectFamily.Kind);

    private bool _hasDirectFallbackAsciiFamilyWithoutValidation =>
        (_fallbackDirectFamily.SupportsAsciiDefinitiveIsMatch ||
         _fallbackDirectFamily.Kind == Utf8FallbackDirectFamilyKind.AnchoredAsciiSignedDecimalWhole) &&
        !_hasDirectFallbackTokenFamilyWithoutValidation;

    private NativeExecutionKind ExecutionKind => _preparedRegex.ExecutionKind;

    private Utf8PreparedRegex PreparedRegex => _preparedRegex;

    internal Utf8ByteOffsetExecution ByteOffsetExecution => new(this); // PCRE2-INTEGRATION-POINT

    internal Utf8RegexInspection Inspection => new(this);

    private Utf8SearchPlan SearchPlan => _preparedRegex.SearchPlan;

    private Utf8StructuralSearchPlan StructuralSearchPlan => _preparedRegex.StructuralSearchPlan;

    private Utf8SearchPortfolioKind SearchPortfolioKind => _preparedRegex.SearchPlan.PortfolioKind;

    private Utf8CompiledEngineKind CompiledEngineKind => _compiledEngine.Kind;

    private Utf8CompiledExecutionBackend CompiledExecutionBackend => _compiledEngine.Backend;

    private Utf8StructuralLinearProgramKind StructuralLinearProgramKind => _preparedRegex.StructuralLinearProgram.Kind;

    private Utf8StructuralVerifierPlan StructuralVerifierPlan => _verifierRuntime.StructuralVerifierPlan;

    private AsciiStructuralIdentifierFamilyPlan StructuralIdentifierFamilyPlan => _preparedRegex.StructuralIdentifierFamilyPlan;

    private Utf8AsciiStructuralIdentifierFamilyExecutor.SharedPrefixSuffixKernelDiagnostics
        DebugStructuralSharedPrefixSuffixKernelDiagnostics =>
            _preparedRegex.ExecutionKind == NativeExecutionKind.AsciiStructuralIdentifierFamily
                ? Utf8AsciiStructuralIdentifierFamilyExecutor.GetSharedPrefixSuffixKernelDiagnostics(
                    _preparedRegex.StructuralIdentifierFamilyPlan,
                    _preparedRegex.SearchPlan)
                : default;

    private AsciiSimplePatternPlan SimplePatternPlan => _preparedRegex.SimplePatternPlan;

    private string? FallbackReason => _preparedRegex.FallbackReason;

    private static void ValidateOptions(RegexOptions options)
    {
        // R00: owned validation; no BCL regex construction. Matches BCL ValidateOptions semantics.
        Utf8RegexSyntax.ValidateOptions(options);
    }

    private static Utf8ValueMatch CreateValueMatch(ReadOnlySpan<byte> input, Utf8BoundaryMap boundaryMap, Match match)
    {
        if (!match.Success)
        {
            return Utf8ValueMatch.NoMatch;
        }

        var start = boundaryMap.Resolve(match.Index);
        var end = boundaryMap.Resolve(match.Index + match.Length);
        var isByteAligned = start.IsScalarBoundary && end.IsScalarBoundary;
        return new Utf8ValueMatch(
            success: true,
            isByteAligned,
            indexInUtf16: match.Index,
            lengthInUtf16: match.Length,
            indexInBytes: isByteAligned ? start.ByteOffset : 0,
            lengthInBytes: isByteAligned ? end.ByteOffset - start.ByteOffset : 0);
    }

    /// <summary>Returns the number assigned to a group name, or <c>-1</c> when the name is undefined.</summary>
    public int GroupNumberFromName(string name)
    {
        // Slice programs publish BCL-exact names by final number, so name
        // answers need no fallback engine.
        if (_sliceProgram is not null)
        {
            if (name is null)
            {
                throw new ArgumentNullException(nameof(name));
            }

            var numbers = _sliceProgram.GroupNames;
            for (var number = 0; number < numbers.Length; number++)
            {
                if (numbers[number] == name)
                {
                    return number;
                }
            }

            return uint.TryParse(name, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var result) && result <= (uint)_sliceProgram.GroupCount
                ? (int)result
                : -1;
        }

        return _program.GroupMetadata?.GroupNumberFromName(name)
            ?? _verifierRuntime.FallbackCandidateVerifier.FallbackRegex.GroupNumberFromName(name);
    }

    /// <summary>Returns the name assigned to a group number, or an empty string when the number is undefined.</summary>
    public string GroupNameFromNumber(int i)
    {
        if (_sliceProgram is not null)
        {
            var names = _sliceProgram.GroupNames;
            return (uint)i < (uint)names.Length ? names[i] : string.Empty;
        }

        return _program.GroupMetadata?.GroupNameFromNumber(i)
            ?? _verifierRuntime.FallbackCandidateVerifier.FallbackRegex.GroupNameFromNumber(i);
    }

    /// <summary>Returns the group names defined by the expression, including group zero.</summary>
    public string[] GetGroupNames()
    {
        if (_sliceProgram is not null)
        {
            return _sliceProgram.GroupNames;
        }

        return _program.GroupMetadata?.GroupNames ?? _verifierRuntime.FallbackCandidateVerifier.FallbackRegex.GetGroupNames();
    }

    /// <summary>Returns the group numbers defined by the expression, including group zero.</summary>
    public int[] GetGroupNumbers()
    {
        if (_sliceProgram is not null)
        {
            return _sliceProgram.OrdinalGroupNumbers;
        }

        return _program.GroupMetadata?.GroupNumbers ?? _verifierRuntime.FallbackCandidateVerifier.FallbackRegex.GetGroupNumbers();
    }

    /// <summary>Determines whether the well-formed UTF-8 input contains a match.</summary>
    public bool IsMatch(ReadOnlySpan<byte> input)
    {
        if (_prioritizedBooleanFamilyKind == Utf8PrioritizedBooleanFamilyKind.AnchoredAsciiSignedDecimalWhole)
        {
            if (Utf8AsciiPrefixTokenExecutor.TryMatchSignedDecimalWhole(input, out _))
            {
                return true;
            }

            if (input.IndexOfAnyInRange((byte)0x80, byte.MaxValue) < 0)
            {
                return false;
            }
        }

        if (_prioritizedBooleanFamilyKind == Utf8PrioritizedBooleanFamilyKind.AsciiBoundedDateToken &&
            input.IndexOfAnyInRange((byte)0x80, byte.MaxValue) < 0)
        {
            return Utf8AsciiBoundedDateTokenExecutor.TryFindAsciiBoundedDateToken(
                input,
                0,
                _fallbackDirectFamily,
                out _,
                out _);
        }

        if (_prioritizedBooleanFamilyKind == Utf8PrioritizedBooleanFamilyKind.AnchoredAsciiOptionalFieldWhole)
        {
            var optionalFieldMatched = Utf8AsciiAnchoredOptionalFieldExecutor.TryMatchWhole(
                input,
                _anchoredOptionalFieldPlan,
                _allowsTrailingNewlineBeforeEnd,
                out _,
                out var optionalFieldNeedsValidation);
            if (!optionalFieldNeedsValidation)
            {
                return optionalFieldMatched;
            }
        }

        try
        {
            return IsMatchCore(input, CancellationToken.None);
        }
        catch (Utf8ExecutionDeadlineExpiredException)
        {
            throw CreateMatchTimeoutException(input);
        }
    }

    private bool OwnedSliceIsMatch(ReadOnlySpan<byte> input, Utf8SliceProgram program, CancellationToken token)
    {
        var map = Utf8BoundaryMap.Create(input);
        return OwnedSliceIsMatchFromUnit(input, program, map, program.RightToLeft ? map.Utf16Length : 0, token);
    }

    private Utf8ValueMatch OwnedSliceMatch(ReadOnlySpan<byte> input, Utf8SliceProgram program, CancellationToken token)
    {
        var map = Utf8BoundaryMap.Create(input);
        return OwnedSliceMatchFromUnit(input, program, map, program.RightToLeft ? map.Utf16Length : 0, token);
    }

    private Utf8MatchContext OwnedSliceMatchDetailed(ReadOnlySpan<byte> input, Utf8SliceProgram program, CancellationToken token)
    {
        var analysis = Utf8InputAnalyzer.Analyze(input);
        return OwnedSliceMatchDetailedFromUnit(input, program, analysis.GetDecodedString(), analysis.BoundaryMap, program.RightToLeft ? analysis.BoundaryMap.Utf16Length : 0, token);
    }

    private Utf8MatchContext OwnedSliceMatchDetailedFromUnit(
        ReadOnlySpan<byte> input,
        Utf8SliceProgram program,
        string decoded,
        Utf8BoundaryMap map,
        int fromUnit,
        CancellationToken token)
    {
        var invocation = new Utf8SliceInvocation(Utf8ExecutionLimits.Unlimited, MatchTimeout, token);
        var slots = Utf8SliceRunner.RentSlots(program.GroupCount, ref invocation);
        if (fromUnit >= 0 && fromUnit <= map.Utf16Length)
        {
            invocation.AnchorOriginByte = map.Resolve(fromUnit).ByteOffset;
        }

        int[][]? byteStarts = null;
        int[][]? byteLengths = null;
        int[]? groupStarts = null;
        int[]? groupLengths = null;
        int[][]? historyStarts = null;
        int[][]? historyLengths = null;
        try
        {
            var outcome = Utf8SliceRunner.TryFindFirstDetailed(program, input, map, fromUnit, slots.Starts, slots.Ends, ref invocation);
            if (outcome.Breach != Utf8ExecutionLimitKind.None)
            {
                throw new InvalidOperationException("Owned slice execution breached an unlimited invocation budget.");
            }

            if (!outcome.Found || outcome.CaptureStarts is null || outcome.CaptureLengths is null)
            {
                if (outcome.Found)
                {
                    throw new InvalidOperationException("Owned slice detailed match is missing capture history.");
                }

                return Utf8MatchContext.Unsuccessful(input, decoded, map, _groupNames);
            }

            byteStarts = outcome.CaptureStarts;
            byteLengths = outcome.CaptureLengths;
            groupStarts = RentGroupArray(program.GroupCount + 1, ref invocation);
            groupLengths = RentGroupArray(program.GroupCount + 1, ref invocation);
            historyStarts = RentHistoryOuters(program.GroupCount + 1, ref invocation);
            historyLengths = RentHistoryOuters(program.GroupCount + 1, ref invocation);
            groupStarts[0] = outcome.StartUnit;
            groupLengths[0] = outcome.EndUnit - outcome.StartUnit;
            historyStarts[0] = RentSingle(outcome.StartUnit, ref invocation);
            historyLengths[0] = RentSingle(outcome.EndUnit - outcome.StartUnit, ref invocation);
            for (var group = 1; group <= program.GroupCount; group++)
            {
                ConvertHistory(
                    map,
                    byteStarts[group - 1],
                    byteLengths[group - 1],
                    ref invocation,
                    out historyStarts[group],
                    out historyLengths[group]);
                if (historyStarts[group].Length == 0)
                {
                    groupStarts[group] = -1;
                    groupLengths[group] = 0;
                }
                else
                {
                    groupStarts[group] = historyStarts[group][^1];
                    groupLengths[group] = historyLengths[group][^1];
                }
            }

            return new Utf8MatchContext(
                input, decoded, map, _groupNames,
                outcome.StartUnit, outcome.EndUnit - outcome.StartUnit,
                groupStarts, groupLengths, historyStarts, historyLengths);
        }
        finally
        {
            // The context retains the converted arrays; their reservations transfer
            // to the caller-owned result while working storage balances to zero.
            if (historyStarts is not null && historyLengths is not null)
            {
                for (var group = 0; group < historyStarts.Length; group++)
                {
                    if (historyStarts[group] is not null)
                    {
                        ReleaseGroupArray(historyStarts[group], ref invocation);
                    }

                    if (historyLengths[group] is not null)
                    {
                        ReleaseGroupArray(historyLengths[group], ref invocation);
                    }
                }

                ReleaseHistoryOuters(historyStarts, ref invocation);
                ReleaseHistoryOuters(historyLengths, ref invocation);
            }

            if (groupStarts is not null)
            {
                ReleaseGroupArray(groupStarts, ref invocation);
            }

            if (groupLengths is not null)
            {
                ReleaseGroupArray(groupLengths, ref invocation);
            }

            Utf8SliceRunner.ReleaseHistory(byteStarts, byteLengths, ref invocation);
            Utf8SliceRunner.ReleaseSlots(slots, ref invocation);
        }
    }

    private static int[][] RentHistoryOuters(int length, ref Utf8SliceInvocation invocation)
    {
        if (!invocation.Ledger.TryReserve((ulong)length * 8))
        {
            throw new InvalidOperationException("Owned slice group storage breached an unlimited invocation budget.");
        }

        return new int[length][];
    }

    private static void ReleaseHistoryOuters(int[][] arrays, ref Utf8SliceInvocation invocation)
    {
        invocation.Ledger.Release((ulong)arrays.Length * 8);
    }

    private static int[] RentSingle(int value, ref Utf8SliceInvocation invocation)
    {
        if (!invocation.Ledger.TryReserve(4))
        {
            throw new InvalidOperationException("Owned slice group storage breached an unlimited invocation budget.");
        }

        return [value];
    }

    private static void ConvertHistory(
        Utf8BoundaryMap map,
        int[] startsBytes,
        int[] lengthsBytes,
        ref Utf8SliceInvocation invocation,
        out int[] startsUnits,
        out int[] lengthsUnits)
    {
        startsUnits = RentGroupArray(startsBytes.Length, ref invocation);
        lengthsUnits = RentGroupArray(lengthsBytes.Length, ref invocation);
        for (var capture = 0; capture < startsBytes.Length; capture++)
        {
            startsUnits[capture] = map.GetUtf16OffsetForByteOffset(startsBytes[capture]);
            lengthsUnits[capture] = map.GetUtf16OffsetForByteOffset(startsBytes[capture] + lengthsBytes[capture]) - startsUnits[capture];
        }
    }

    private static int[] RentGroupArray(int length, ref Utf8SliceInvocation invocation)
    {
        if (!invocation.Ledger.TryReserve((ulong)length * 4))
        {
            throw new InvalidOperationException("Owned slice group storage breached an unlimited invocation budget.");
        }

        return new int[length];
    }

    private static void ReleaseGroupArray(int[] array, ref Utf8SliceInvocation invocation)
    {
        invocation.Ledger.Release((ulong)array.Length * 4);
    }

    private int OwnedSliceCount(ReadOnlySpan<byte> input, Utf8SliceProgram program, CancellationToken token)
    {
        var map = Utf8BoundaryMap.Create(input);
        return OwnedSliceCountFromUnit(input, program, map, program.RightToLeft ? map.Utf16Length : 0, token);
    }

    private bool IsMatchCore(ReadOnlySpan<byte> input, CancellationToken cancellationToken)
    {
        if (_sliceProgram is not null && _sliceProgram.ServesInput(input))
        {
            return OwnedSliceIsMatch(input, _sliceProgram, cancellationToken);
        }

        // PLAN.md C02: Turkic-style instances bypass every invariant-folding
        // native kernel and answer from the construction-culture oracle.
        if (RequiresCultureSensitiveFallback)
        {
            return _verifierRuntime.FallbackCandidateVerifier.FallbackRegex.IsMatch(
                Utf8Validation.DecodeStrict(input));
        }

        if (TryIsMatchPrioritizedBooleanFamilyWithoutValidation(input, out var prioritizedIsMatch))
        {
            return prioritizedIsMatch;
        }

        if (ShouldUseKelvinSignFallback(input))
        {
            return _verifierRuntime.FallbackCandidateVerifier.FallbackRegex.IsMatch(
                Utf8Validation.DecodeStrict(input));
        }

        if (ShouldUseRightToLeftScalarFallback())
        {
            return _verifierRuntime.FallbackCandidateVerifier.FallbackRegex.IsMatch(
                Utf8Validation.DecodeStrict(input));
        }

        if (TryIsMatchDirectWithoutValidation(input, out var directIsMatch))
        {
            return directIsMatch;
        }

        if (TryMatchDirectWithoutValidation(input, out var directMatch))
        {
            return directMatch.Success;
        }

        if (TryGetAsciiCultureInvariantTwin(input, out var twin))
        {
            if (twin.TryMatchDirectWithoutValidation(input, out var twinDirectMatch))
            {
                return twinDirectMatch.Success;
            }

            return twin.IsMatch(input);
        }

        if (ShouldDecodeWholeSubjectForFallbackValueOperation())
        {
            return _verifierRuntime.FallbackCandidateVerifier.FallbackRegex.IsMatch(
                Utf8Validation.DecodeStrict(input));
        }

        if (CanUseWellFormedOnlyValidation())
        {
            if (!TryUseAsciiInputValidationShortcut(input))
            {
                Utf8Validation.ThrowIfInvalidOnly(input);
            }

            if (!ShouldSkipRequiredPrefilterForMatch() && RejectsByRequiredPrefilter(input))
            {
                return false;
            }

            var fastBudget = CreateExecutionBudget();
            return IsMatchViaCompiledEngine(input, default, fastBudget);
        }

        var validation = Utf8Validation.Validate(input);
        if (ShouldFallbackForTrailingNewlineAnchoredValidator(input, validation))
        {
            return _verifierRuntime.FallbackCandidateVerifier.FallbackRegex.IsMatch(Encoding.UTF8.GetString(input));
        }

        if (!ShouldSkipRequiredPrefilterForMatch() && RejectsByRequiredPrefilter(input))
        {
            return false;
        }

        var budget = CreateExecutionBudget();

        return IsMatchViaCompiledEngine(input, validation, budget);
    }

    /// <summary>Determines whether the well-formed UTF-8 input contains a match, observing cancellation before matching begins.</summary>
    /// <param name="input">The well-formed UTF-8 input to match against.</param>
    /// <param name="cancellationToken">The token to observe before matching begins.</param>
    /// <exception cref="OperationCanceledException">The token was already canceled.</exception>
    public bool IsMatch(ReadOnlySpan<byte> input, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            return IsMatchCore(input, cancellationToken);
        }
        catch (Utf8ExecutionDeadlineExpiredException)
        {
            throw CreateMatchTimeoutException(input);
        }
    }

    /// <summary>Determines whether a match starts at or after the specified UTF-16 code-unit offset.</summary>
    public bool IsMatchFromUtf16Offset(ReadOnlySpan<byte> input, int utf16Offset)
    {
        try
        {
            return IsMatchFromUtf16OffsetCore(input, utf16Offset, CancellationToken.None);
        }
        catch (Utf8ExecutionDeadlineExpiredException)
        {
            throw CreateMatchTimeoutException(input);
        }
    }

    /// <summary>Determines whether a match starts at or after the specified UTF-16 code-unit offset, observing cancellation before matching begins.</summary>
    /// <param name="input">The well-formed UTF-8 input to match against.</param>
    /// <param name="utf16Offset">The zero-based UTF-16 code-unit offset of the position where matching starts.</param>
    /// <param name="cancellationToken">The token to observe before matching begins.</param>
    /// <exception cref="OperationCanceledException">The token was already canceled.</exception>
    public bool IsMatchFromUtf16Offset(ReadOnlySpan<byte> input, int utf16Offset, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            return IsMatchFromUtf16OffsetCore(input, utf16Offset, cancellationToken);
        }
        catch (Utf8ExecutionDeadlineExpiredException)
        {
            throw CreateMatchTimeoutException(input);
        }
    }

    private bool IsMatchFromUtf16OffsetCore(ReadOnlySpan<byte> input, int utf16Offset, CancellationToken cancellationToken)
    {
        var subject = Utf8InputAnalyzer.Analyze(input);
        var start = subject.GetUtf16Position(utf16Offset, nameof(utf16Offset));
        if (_fallbackDirectFamily.Kind == Utf8FallbackDirectFamilyKind.AnchoredAsciiLiteralPrefix)
        {
            Utf8SearchDiagnosticsSession.Current?.MarkExecutionRoute(Utf8ExecutionRoute.FallbackDirectAnchoredAsciiLiteralPrefix);
            return start.Value == 0 &&
                Utf8AsciiAnchoredPrefixFamilyExecutor.TryMatchPrefix(input, _fallbackDirectFamily, out _);
        }

        // Anchored suffixes match at most once at a fixed end position: a start past
        // it misses definitively, otherwise the direct hit itself decides.
        if (Utf8FallbackDirectFamilyCategories.IsAnchoredSuffixMatchFamily(_fallbackDirectFamily.Kind) &&
            _fallbackDirectFamily.LiteralUtf8 is { Length: > 0 })
        {
            Utf8SearchDiagnosticsSession.Current?.MarkExecutionRoute(Utf8ExecutionRoute.FallbackDirectAnchoredAsciiLiteralSuffix);
            return TryMatchDirectFallbackFamilyWithoutValidation(input, out var suffixDirect) &&
                suffixDirect.Success &&
                start.Value <= suffixDirect.IndexInUtf16;
        }

        if (_sliceProgram is not null && _sliceProgram.ServesInput(input))
        {
            return OwnedSliceIsMatchFromUnit(input, _sliceProgram, subject.BoundaryMap, start.Value, cancellationToken);
        }

        return _verifierRuntime.FallbackCandidateVerifier.FallbackRegex.IsMatch(
            subject.GetDecodedString().AsSpan(),
            start.Value);
    }

    /// <summary>Owned slice boolean search from a UTF-16 start unit.</summary>
    private bool OwnedSliceIsMatchFromUnit(
        ReadOnlySpan<byte> input,
        Utf8SliceProgram program,
        Utf8BoundaryMap map,
        int startUnit,
        CancellationToken token)
    {
        var invocation = new Utf8SliceInvocation(Utf8ExecutionLimits.Unlimited, MatchTimeout, token);
        var slots = Utf8SliceRunner.RentSlots(program.GroupCount, ref invocation);
        if (startUnit >= 0 && startUnit <= map.Utf16Length)
        {
            invocation.AnchorOriginByte = map.Resolve(startUnit).ByteOffset;
        }

        try
        {
            var outcome = Utf8SliceRunner.TryFindFirst(program, input, map, startUnit, slots.Starts, slots.Ends, ref invocation);
            if (outcome.Breach != Utf8ExecutionLimitKind.None)
            {
                throw new InvalidOperationException("Owned slice execution breached an unlimited invocation budget.");
            }

            return outcome.Found;
        }
        finally
        {
            Utf8SliceRunner.ReleaseSlots(slots, ref invocation);
        }
    }

    /// <summary>Counts non-overlapping matches in the well-formed UTF-8 input.</summary>
    public int Count(ReadOnlySpan<byte> input)
    {
        try
        {
            return CountCore(input, CancellationToken.None);
        }
        catch (Utf8ExecutionDeadlineExpiredException)
        {
            throw CreateMatchTimeoutException(input);
        }
    }

    /// <summary>Counts non-overlapping matches in the well-formed UTF-8 input, observing cancellation before matching begins.</summary>
    /// <param name="input">The well-formed UTF-8 input to match against.</param>
    /// <param name="cancellationToken">The token to observe before matching begins.</param>
    /// <exception cref="OperationCanceledException">The token was already canceled.</exception>
    public int Count(ReadOnlySpan<byte> input, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            return CountCore(input, cancellationToken);
        }
        catch (Utf8ExecutionDeadlineExpiredException)
        {
            throw CreateMatchTimeoutException(input);
        }
    }

    private int CountCore(ReadOnlySpan<byte> input, CancellationToken cancellationToken)
    {
        if (_sliceProgram is not null && _sliceProgram.ServesInput(input))
        {
            return OwnedSliceCount(input, _sliceProgram, cancellationToken);
        }

        // PLAN.md C02: Turkic-style instances bypass every invariant-folding
        // native kernel and answer from the construction-culture oracle.
        if (RequiresCultureSensitiveFallback)
        {
            return _verifierRuntime.FallbackCandidateVerifier.FallbackRegex.Count(
                Utf8Validation.DecodeStrict(input));
        }

        if (_fusedAsciiLiteralCountAnchorOffset >= 0 &&
            _preparedRegex.LiteralUtf8 is { } fusedAsciiLiteral &&
            Utf8AsciiExactLiteralCountExecutor.TryCountAndValidateAscii(
                input,
                fusedAsciiLiteral,
                _fusedAsciiLiteralCountAnchorOffset,
                out var fusedAsciiLiteralCount))
        {
            Utf8SearchDiagnosticsSession.Current?.MarkExecutionRoute(Utf8ExecutionRoute.CompiledFusedAsciiLiteralCount);
            return fusedAsciiLiteralCount;
        }

        if (_fusedAsciiBoundaryLiteralFamilyCounter is { } fusedAsciiBoundaryLiteralFamilyCounter &&
            fusedAsciiBoundaryLiteralFamilyCounter.TryCountAndValidateAscii(input, out var fusedBoundaryLiteralFamilyCount))
        {
            Utf8SearchDiagnosticsSession.Current?.MarkExecutionRoute(Utf8ExecutionRoute.CompiledFusedAsciiLiteralFamilyCount);
            return fusedBoundaryLiteralFamilyCount;
        }

        if (_packedNibbleLiteralFamilyCountStrategy is { } packedNibbleLiteralFamilyCountStrategy)
        {
            Utf8Validation.ThrowIfInvalidOnly(input);
            return packedNibbleLiteralFamilyCountStrategy.Count(input);
        }

        if (_prioritizeOrderedLiteralWindowCount)
        {
            var orderedWindowPlan = _program.PreparedRegex.StructuralLinearProgram.OrderedLiteralWindowPlan;
            if (orderedWindowPlan.TrailingLiteralUtf8.Length >= 2 &&
                AsciiOrderedLiteralWindowExecutor.CanUseSeparatorOnlySingleLiteralFastPath(orderedWindowPlan))
            {
                var orderedWindowBudget = CreateExecutionBudget();
                if (AsciiOrderedLiteralWindowExecutor.TryCountSeparatorOnlySingleLiteralAscii(
                        input,
                        orderedWindowPlan,
                        orderedWindowBudget,
                        out var orderedWindowCount))
                {
                    Utf8SearchDiagnosticsSession.Current?.MarkExecutionRoute(Utf8ExecutionRoute.NativeOrderedLiteralWindow);
                    return orderedWindowCount;
                }

                // The native separator set is ASCII-only; valid non-ASCII input needs the semantic oracle too.
                return _verifierRuntime.FallbackCandidateVerifier.FallbackRegex.Count(
                    Utf8Validation.DecodeStrict(input));
            }
        }

        if (_prioritizePlainAsciiLiteralCount ||
            _prioritizeBoundaryLiteralFamilyCount ||
            _prioritizeOrderedLiteralWindowCount)
        {
            if (!TryUseAsciiInputValidationShortcut(input))
            {
                Utf8Validation.ThrowIfInvalidOnly(input);
            }

            return CountViaCompiledEngine(input, default, CreateExecutionBudget());
        }

        if (CanValidateKelvinSignDuringNativeCount())
        {
            var kelvinValidation = Utf8InputAnalyzer.ValidateOnly(input, out var containsKelvinSign);
            if (containsKelvinSign)
            {
                return _verifierRuntime.FallbackCandidateVerifier.FallbackRegex.Count(
                    Utf8Validation.DecodeStrict(input));
            }

            if (!ShouldSkipRequiredPrefilterForCount() && RejectsByRequiredPrefilter(input))
            {
                Utf8SearchDiagnosticsSession.Current?.MarkExecutionRoute(Utf8ExecutionRoute.RequiredPrefilterReject);
                return 0;
            }

            return CountViaCompiledEngine(input, kelvinValidation, CreateExecutionBudget());
        }

        if (ShouldUseKelvinSignFallback(input))
        {
            return _verifierRuntime.FallbackCandidateVerifier.FallbackRegex.Count(
                Utf8Validation.DecodeStrict(input));
        }

        if (ShouldUseRightToLeftScalarFallback())
        {
            return _verifierRuntime.FallbackCandidateVerifier.FallbackRegex.Count(
                Utf8Validation.DecodeStrict(input));
        }

        if (TryGetAsciiCultureInvariantTwin(input, out var twin))
        {
            return twin.Count(input);
        }

        if (_invariantCyrillicLiteralCountStrategy is { } invariantCyrillicLiteralCountStrategy)
        {
            return invariantCyrillicLiteralCountStrategy.Count(input);
        }

        if (ShouldDecodeWholeSubjectForFallbackValueOperation())
        {
            return _verifierRuntime.FallbackCandidateVerifier.FallbackRegex.Count(
                Utf8Validation.DecodeStrict(input));
        }

        if (_fallbackDirectFamily.Kind is
            Utf8FallbackDirectFamilyKind.Utf8WordDelimitedTokenCount or
            Utf8FallbackDirectFamilyKind.Utf8UriToken)
        {
            Utf8Validation.ThrowIfInvalidOnly(input);
            return CountViaCompiledEngine(input, default, CreateExecutionBudget());
        }

        if (_fallbackDirectFamily.Kind == Utf8FallbackDirectFamilyKind.AsciiDelimitedTokenCount ||
            Utf8FallbackDirectFamilyCategories.IsTokenCountFamily(_fallbackDirectFamily.Kind))
        {
            if (!Utf8InputAnalyzer.IsAscii(input))
            {
                return _verifierRuntime.FallbackCandidateVerifier.FallbackRegex.Count(
                    Utf8Validation.DecodeStrict(input));
            }

            var asciiValidation = new Utf8ValidationResult(
                input.Length,
                input.Length,
                isAscii: true,
                containsSupplementaryScalars: false);
            return CountViaCompiledEngine(input, asciiValidation, CreateExecutionBudget());
        }

        if (CanUseFusedCompiledAsciiLiteralFamilyCount())
        {
            if (!TryUseAsciiInputValidationShortcut(input))
            {
                Utf8Validation.ThrowIfInvalidOnly(input);
            }

            Utf8SearchDiagnosticsSession.Current?.MarkExecutionRoute(Utf8ExecutionRoute.CompiledFusedAsciiLiteralFamilyCount);
            var fastBudget = CreateExecutionBudget();
            return CountViaCompiledEngine(input, default, fastBudget);
        }

        if (CanUseFusedCompiledUtf8LiteralCount() || CanUseFusedCompiledUtf8LiteralFamilyCount())
        {
            Utf8SearchDiagnosticsSession.Current?.MarkExecutionRoute(
                CanUseFusedCompiledUtf8LiteralCount()
                    ? Utf8ExecutionRoute.CompiledFusedUtf8LiteralCount
                    : Utf8ExecutionRoute.CompiledFusedUtf8LiteralFamilyCount);
            var fastBudget = CreateExecutionBudget();
            return CountViaCompiledEngine(input, default, fastBudget);
        }

        if (_compiledEngineRuntime.SupportsWellFormedOnlyCount)
        {
            Utf8ValidationResult countValidation;
            if (_compiledEngineRuntime.PreferValidateOnlyCount)
            {
                countValidation = Utf8InputAnalyzer.ValidateOnly(input);
            }
            else if (_compiledEngineRuntime.SupportsThrowIfInvalidOnlyCount)
            {
                if (!TryUseAsciiInputValidationShortcut(input))
                {
                    Utf8Validation.ThrowIfInvalidOnly(input);
                }

                countValidation = default;
            }
            else
            {
                countValidation = GetWellFormedOnlyValidation(input);
            }

            if (!ShouldSkipRequiredPrefilterForCount() && RejectsByRequiredPrefilter(input))
            {
                Utf8SearchDiagnosticsSession.Current?.MarkExecutionRoute(Utf8ExecutionRoute.RequiredPrefilterReject);
                return 0;
            }

            var fastBudget = CreateExecutionBudget();
            return CountViaCompiledEngine(input, countValidation, fastBudget);
        }

        if (CanUseWellFormedOnlyValidation())
        {
            if (!TryUseAsciiInputValidationShortcut(input))
            {
                Utf8Validation.ThrowIfInvalidOnly(input);
            }

            if (!ShouldSkipRequiredPrefilterForCount() && RejectsByRequiredPrefilter(input))
            {
                Utf8SearchDiagnosticsSession.Current?.MarkExecutionRoute(Utf8ExecutionRoute.RequiredPrefilterReject);
                return 0;
            }

            var fastBudget = CreateExecutionBudget();
            if (fastBudget.IsInfinite &&
                _directStructuralFamilyKernelMatcher is not null)
            {
                Utf8SearchDiagnosticsSession.Current?.MarkExecutionRoute(Utf8ExecutionRoute.NativeStructuralFamilyEmitSharedPrefixSuffix);
                return _directStructuralFamilyKernelMatcher.Count(input);
            }

            return CountViaCompiledEngine(input, default, fastBudget);
        }

        var validation = Utf8Validation.Validate(input);
        if (ShouldFallbackForTrailingNewlineAnchoredValidator(input, validation))
        {
            return _verifierRuntime.FallbackCandidateVerifier.FallbackRegex.Count(Encoding.UTF8.GetString(input));
        }

        if (RejectsByRequiredPrefilter(input))
        {
            Utf8SearchDiagnosticsSession.Current?.MarkExecutionRoute(Utf8ExecutionRoute.RequiredPrefilterReject);
            return 0;
        }

        var budget = CreateExecutionBudget();

        return CountViaCompiledEngine(input, validation, budget);
    }

    /// <summary>Counts non-overlapping matches starting at the specified UTF-16 code-unit offset.</summary>
    public int CountFromUtf16Offset(ReadOnlySpan<byte> input, int utf16Offset)
    {
        try
        {
            return CountFromUtf16OffsetCore(input, utf16Offset, CancellationToken.None);
        }
        catch (Utf8ExecutionDeadlineExpiredException)
        {
            throw CreateMatchTimeoutException(input);
        }
    }

    /// <summary>Counts non-overlapping matches starting at the specified UTF-16 code-unit offset, observing cancellation before matching begins.</summary>
    /// <param name="input">The well-formed UTF-8 input to match against.</param>
    /// <param name="utf16Offset">The zero-based UTF-16 code-unit offset of the position where matching starts.</param>
    /// <param name="cancellationToken">The token to observe before matching begins.</param>
    /// <exception cref="OperationCanceledException">The token was already canceled.</exception>
    public int CountFromUtf16Offset(ReadOnlySpan<byte> input, int utf16Offset, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            return CountFromUtf16OffsetCore(input, utf16Offset, cancellationToken);
        }
        catch (Utf8ExecutionDeadlineExpiredException)
        {
            throw CreateMatchTimeoutException(input);
        }
    }

    private int CountFromUtf16OffsetCore(ReadOnlySpan<byte> input, int utf16Offset, CancellationToken cancellationToken)
    {
        var subject = Utf8InputAnalyzer.Analyze(input);
        var start = subject.GetUtf16Position(utf16Offset, nameof(utf16Offset));
        // At most one anchored suffix match exists past the start: count it directly.
        if (Utf8FallbackDirectFamilyCategories.IsAnchoredSuffixMatchFamily(_fallbackDirectFamily.Kind) &&
            _fallbackDirectFamily.LiteralUtf8 is { Length: > 0 })
        {
            Utf8SearchDiagnosticsSession.Current?.MarkExecutionRoute(Utf8ExecutionRoute.FallbackDirectAnchoredAsciiLiteralSuffix);
            return TryMatchDirectFallbackFamilyWithoutValidation(input, out var suffixCount) &&
                suffixCount.Success &&
                start.Value <= suffixCount.IndexInUtf16
                ? 1
                : 0;
        }

        if (_sliceProgram is not null && _sliceProgram.ServesInput(input))
        {
            return OwnedSliceCountFromUnit(input, _sliceProgram, subject.BoundaryMap, start.Value, cancellationToken);
        }

        return _verifierRuntime.FallbackCandidateVerifier.FallbackRegex.Count(
            subject.GetDecodedString().AsSpan(),
            start.Value);
    }

    /// <summary>Owned slice count from a UTF-16 start unit.</summary>
    private int OwnedSliceCountFromUnit(
        ReadOnlySpan<byte> input,
        Utf8SliceProgram program,
        Utf8BoundaryMap map,
        int startUnit,
        CancellationToken token)
    {
        var invocation = new Utf8SliceInvocation(Utf8ExecutionLimits.Unlimited, MatchTimeout, token);
        var slots = Utf8SliceRunner.RentSlots(program.GroupCount, ref invocation);
        try
        {
            var outcome = Utf8SliceRunner.CountFrom(program, input, map, startUnit, slots.Starts, slots.Ends, ref invocation);
            if (outcome.Breach != Utf8ExecutionLimitKind.None)
            {
                throw new InvalidOperationException("Owned slice execution breached an unlimited invocation budget.");
            }

            return outcome.Count;
        }
        finally
        {
            Utf8SliceRunner.ReleaseSlots(slots, ref invocation);
        }
    }

    /// <summary>Finds the first match and returns its UTF-16 and, when scalar-aligned, UTF-8 byte coordinates.</summary>
    public Utf8ValueMatch Match(ReadOnlySpan<byte> input)
    {
        try
        {
            return MatchCore(input, CancellationToken.None);
        }
        catch (Utf8ExecutionDeadlineExpiredException)
        {
            throw CreateMatchTimeoutException(input);
        }
    }

    /// <summary>Finds the first match and returns its UTF-16 and, when scalar-aligned, UTF-8 byte coordinates, observing cancellation before matching begins.</summary>
    /// <param name="input">The well-formed UTF-8 input to match against.</param>
    /// <param name="cancellationToken">The token to observe before matching begins.</param>
    /// <exception cref="OperationCanceledException">The token was already canceled.</exception>
    public Utf8ValueMatch Match(ReadOnlySpan<byte> input, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            return MatchCore(input, cancellationToken);
        }
        catch (Utf8ExecutionDeadlineExpiredException)
        {
            throw CreateMatchTimeoutException(input);
        }
    }

    private Utf8ValueMatch MatchCore(ReadOnlySpan<byte> input, CancellationToken cancellationToken)
    {
        if (_sliceProgram is not null && _sliceProgram.ServesInput(input))
        {
            return OwnedSliceMatch(input, _sliceProgram, cancellationToken);
        }

        // PLAN.md C02: Turkic-style instances bypass every invariant-folding
        // native kernel and answer from the construction-culture oracle.
        if (RequiresCultureSensitiveFallback)
        {
            return MatchSemanticFallback(input);
        }

        if (ShouldUseKelvinSignFallback(input))
        {
            return MatchSemanticFallback(input);
        }

        if (ShouldUseRightToLeftScalarFallback())
        {
            return MatchSemanticFallback(input);
        }

        if (TryMatchDirectWithoutValidation(input, out var directMatch))
        {
            return directMatch;
        }

        if (TryGetAsciiCultureInvariantTwin(input, out var twin))
        {
            if (twin.TryMatchDirectWithoutValidation(input, out var twinDirectMatch))
            {
                return twinDirectMatch;
            }

            return twin.Match(input);
        }

        var canUseAsciiWellFormedOnlyMatch =
            _compiledEngineRuntime.SupportsAsciiWellFormedOnlyMatch &&
            TryUseAsciiInputValidationShortcut(input);

        // An ASCII whole-match is incomplete for non-ASCII input unless the
        // simple-pattern plan is UTF-8 byte-safe (PLAN.md C01): a miss must not
        // shortcut Unicode verification. Those inputs fall through to full
        // validation and the compiled engine, which selects the semantic fallback.
        // This stays inline: single-caller private helpers count as source debt.
        var requiresUnicodeVerification =
            _preparedRegex.ExecutionKind == NativeExecutionKind.AsciiSimplePattern &&
            !_preparedRegex.SimplePatternPlan.IsUtf8ByteSafe &&
            !Utf8InputAnalyzer.IsAscii(input);

        var canUseWellFormedOnlyMatch =
            _compiledEngineRuntime.SupportsWellFormedOnlyMatch &&
            !requiresUnicodeVerification;

        if (canUseAsciiWellFormedOnlyMatch || canUseWellFormedOnlyMatch)
        {
            if (!canUseAsciiWellFormedOnlyMatch)
            {
                _ = GetWellFormedOnlyValidation(input);
            }

            if (!ShouldSkipRequiredPrefilterForMatch() && RejectsByRequiredPrefilter(input))
            {
                return Utf8ValueMatch.NoMatch;
            }

            var matched = canUseAsciiWellFormedOnlyMatch
                ? _compiledEngineRuntime.TryMatchAsciiWellFormedOnly(input, out var wellFormedMatch)
                : _compiledEngineRuntime.TryMatchWellFormedOnly(input, out wellFormedMatch);

            if (matched)
            {
                return wellFormedMatch;
            }

            // A whole-match miss is definitive only when a miss at offset 0 rules
            // out every substring match. Line-anchored (`(?m:^)`, `(?m:$)`) plans can
            // still match inside the input, so they fall through to the engine below.
            if (_compiledEngineRuntime.WellFormedOnlyMatchMissIsDefinitive &&
                !(_preparedRegex.SimplePatternPlan.IsStartAnchored &&
                    !_preparedRegex.SimplePatternPlan.IsAbsoluteStartAnchored) &&
                !(_preparedRegex.SimplePatternPlan.IsEndAnchored &&
                    !_preparedRegex.SimplePatternPlan.IsAbsoluteEndAnchored))
            {
                return Utf8ValueMatch.NoMatch;
            }
        }

        var validation = Utf8Validation.Validate(input);
        if (ShouldFallbackForTrailingNewlineAnchoredValidator(input, validation))
        {
            return MatchFallback(input);
        }

        if (!ShouldSkipRequiredPrefilterForMatch() && RejectsByRequiredPrefilter(input))
        {
            return Utf8ValueMatch.NoMatch;
        }

        var budget = CreateExecutionBudget();
        return MatchViaCompiledEngine(input, validation, budget);
    }

    /// <summary>Finds the first match at or after the specified UTF-16 code-unit offset.</summary>
    public Utf8ValueMatch MatchFromUtf16Offset(ReadOnlySpan<byte> input, int utf16Offset)
    {
        try
        {
            return MatchFromUtf16OffsetCore(input, utf16Offset, CancellationToken.None);
        }
        catch (Utf8ExecutionDeadlineExpiredException)
        {
            throw CreateMatchTimeoutException(input);
        }
    }

    /// <summary>Finds the first match at or after the specified UTF-16 code-unit offset, observing cancellation before matching begins.</summary>
    /// <param name="input">The well-formed UTF-8 input to match against.</param>
    /// <param name="utf16Offset">The zero-based UTF-16 code-unit offset of the position where matching starts.</param>
    /// <param name="cancellationToken">The token to observe before matching begins.</param>
    /// <exception cref="OperationCanceledException">The token was already canceled.</exception>
    public Utf8ValueMatch MatchFromUtf16Offset(ReadOnlySpan<byte> input, int utf16Offset, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            return MatchFromUtf16OffsetCore(input, utf16Offset, cancellationToken);
        }
        catch (Utf8ExecutionDeadlineExpiredException)
        {
            throw CreateMatchTimeoutException(input);
        }
    }

    private Utf8ValueMatch MatchFromUtf16OffsetCore(ReadOnlySpan<byte> input, int utf16Offset, CancellationToken cancellationToken)
    {
        var subject = Utf8InputAnalyzer.Analyze(input);
        var start = subject.GetUtf16Position(utf16Offset, nameof(utf16Offset));
        if (_fallbackDirectFamily.Kind == Utf8FallbackDirectFamilyKind.AnchoredAsciiLiteralPrefix &&
            _fallbackDirectFamily.LiteralUtf8 is { Length: > 0 })
        {
            Utf8SearchDiagnosticsSession.Current?.MarkExecutionRoute(Utf8ExecutionRoute.FallbackDirectAnchoredAsciiLiteralPrefix);
            if (start.Value == 0 &&
                Utf8AsciiAnchoredPrefixFamilyExecutor.TryMatchPrefix(input, _fallbackDirectFamily, out var anchoredOffsetMatchLength))
            {
                return new Utf8ValueMatch(true, true, 0, anchoredOffsetMatchLength, 0, anchoredOffsetMatchLength);
            }

            return Utf8ValueMatch.NoMatch;
        }

        if (Utf8FallbackDirectFamilyCategories.IsAnchoredSuffixMatchFamily(_fallbackDirectFamily.Kind) &&
            _fallbackDirectFamily.LiteralUtf8 is { Length: > 0 })
        {
            Utf8SearchDiagnosticsSession.Current?.MarkExecutionRoute(Utf8ExecutionRoute.FallbackDirectAnchoredAsciiLiteralSuffix);
            if (TryMatchDirectFallbackFamilyWithoutValidation(input, out var suffixDirect) &&
                suffixDirect.Success &&
                start.Value <= suffixDirect.IndexInUtf16)
            {
                return suffixDirect;
            }

            return Utf8ValueMatch.NoMatch;
        }

        if (_sliceProgram is not null && _sliceProgram.ServesInput(input))
        {
            return OwnedSliceMatchFromUnit(input, _sliceProgram, subject.BoundaryMap, start.Value, cancellationToken);
        }

        var match = _verifierRuntime.FallbackCandidateVerifier.FallbackRegex.Match(
            subject.GetDecodedString(),
            start.Value);
        return CreateValueMatch(input, subject.BoundaryMap, match);
    }

    /// <summary>Owned slice first-match from a UTF-16 start unit.</summary>
    private Utf8ValueMatch OwnedSliceMatchFromUnit(
        ReadOnlySpan<byte> input,
        Utf8SliceProgram program,
        Utf8BoundaryMap map,
        int startUnit,
        CancellationToken token)
    {
        var invocation = new Utf8SliceInvocation(Utf8ExecutionLimits.Unlimited, MatchTimeout, token);
        var slots = Utf8SliceRunner.RentSlots(program.GroupCount, ref invocation);
        if (startUnit >= 0 && startUnit <= map.Utf16Length)
        {
            invocation.AnchorOriginByte = map.Resolve(startUnit).ByteOffset;
        }

        try
        {
            var outcome = Utf8SliceRunner.TryFindFirst(program, input, map, startUnit, slots.Starts, slots.Ends, ref invocation);
            if (outcome.Breach != Utf8ExecutionLimitKind.None)
            {
                throw new InvalidOperationException("Owned slice execution breached an unlimited invocation budget.");
            }

            if (!outcome.Found)
            {
                return Utf8ValueMatch.NoMatch;
            }

            return Utf8SliceRunner.ProjectMatch(map, outcome);
        }
        finally
        {
            Utf8SliceRunner.ReleaseSlots(slots, ref invocation);
        }
    }

    /// <summary>Finds the first match and exposes its groups and captures over the original UTF-8 input.</summary>
    public Utf8MatchContext MatchDetailed(ReadOnlySpan<byte> input)
    {
        return MatchDetailedCore(input, CancellationToken.None);
    }

    private Utf8MatchContext MatchDetailedCore(ReadOnlySpan<byte> input, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (_sliceProgram is not null && _sliceProgram.ServesInput(input))
        {
            return OwnedSliceMatchDetailed(input, _sliceProgram, cancellationToken);
        }

        if (TryGetAsciiCultureInvariantTwin(input, out var twin))
        {
            return twin.MatchDetailed(input);
        }

        var analysis = Utf8InputAnalyzer.Analyze(input);
        var decoded = analysis.GetDecodedString();
        // Anchored ASCII literal prefixes shape directly from the prefix compare:
        // byte-exact on every input, so detailed matches never construct the BCL engine.
        if (_fallbackDirectFamily.Kind == Utf8FallbackDirectFamilyKind.AnchoredAsciiLiteralPrefix &&
            _fallbackDirectFamily.LiteralUtf8 is { Length: > 0 } &&
            _groupNumbers.Length <= 1)
        {
            Utf8SearchDiagnosticsSession.Current?.MarkExecutionRoute(Utf8ExecutionRoute.FallbackDirectAnchoredAsciiLiteralPrefix);
            if (Utf8AsciiAnchoredPrefixFamilyExecutor.TryMatchPrefix(input, _fallbackDirectFamily, out var anchoredDetailedLength))
            {
                return new Utf8MatchContext(input, decoded, analysis.BoundaryMap, _groupNames, 0, anchoredDetailedLength);
            }

            return Utf8MatchContext.Unsuccessful(input, decoded, analysis.BoundaryMap, _groupNames);
        }

        // Anchored suffixes shape the same way from the direct hit, which already
        // carries UTF-16 coordinates without BCL construction.
        if (Utf8FallbackDirectFamilyCategories.IsAnchoredSuffixMatchFamily(_fallbackDirectFamily.Kind) &&
            _fallbackDirectFamily.LiteralUtf8 is { Length: > 0 } &&
            _groupNumbers.Length <= 1)
        {
            Utf8SearchDiagnosticsSession.Current?.MarkExecutionRoute(Utf8ExecutionRoute.FallbackDirectAnchoredAsciiLiteralSuffix);
            if (TryMatchDirectFallbackFamilyWithoutValidation(input, out var suffixDetailed) &&
                suffixDetailed.Success)
            {
                return new Utf8MatchContext(input, decoded, analysis.BoundaryMap, _groupNames, suffixDetailed.IndexInUtf16, suffixDetailed.LengthInUtf16);
            }

            return Utf8MatchContext.Unsuccessful(input, decoded, analysis.BoundaryMap, _groupNames);
        }

        if (TryGetOwnedDetailedGroupCount(out var ownedGroupCount))
        {
            // R03: discovery runs through the shared value path and shaping projects owned
            // spans, so qualifying literal shapes never execute the BCL engine; semantic declines
            // inside MatchCore keep their existing BCL routes.
            Utf8ValueMatch first;
            try
            {
                first = MatchCore(input, cancellationToken);
            }
            catch (Utf8ExecutionDeadlineExpiredException)
            {
                throw CreateMatchTimeoutException(input);
            }

            if (!first.Success)
            {
                return Utf8MatchContext.Unsuccessful(input, decoded, analysis.BoundaryMap, _groupNames);
            }

            return ownedGroupCount > 1
                ? new Utf8MatchContext(input, decoded, analysis.BoundaryMap, _groupNames, first.IndexInUtf16, first.LengthInUtf16, ownedGroupCount)
                : new Utf8MatchContext(input, decoded, analysis.BoundaryMap, _groupNames, first.IndexInUtf16, first.LengthInUtf16);
        }

        if (_groupedLiteralSpanTable is not null &&
            !RequiresCultureSensitiveFallback &&
            !ShouldUseKelvinSignFallback(input))
        {
            // R05: grouped single-literal shapes discover through the shared value path
            // and project every group from fixed literal-relative spans. A hit-length
            // disagreement declines to the routes below instead of shaping blindly.
            Utf8ValueMatch groupedFirst;
            try
            {
                groupedFirst = MatchCore(input, cancellationToken);
            }
            catch (Utf8ExecutionDeadlineExpiredException)
            {
                throw CreateMatchTimeoutException(input);
            }

            if (!groupedFirst.Success)
            {
                return Utf8MatchContext.Unsuccessful(input, decoded, analysis.BoundaryMap, _groupNames);
            }

            if (TryShapeGroupedLiteralDetailedMatch(input, decoded, analysis.BoundaryMap, groupedFirst, out var groupedDetailed))
            {
                return groupedDetailed;
            }
        }

        if (TryGetBranchedLiteralFamilyMap(out var branchMap) &&
            TryMatchDetailedBranchedLiteralFamily(input, decoded, analysis.BoundaryMap, branchMap, 0, out var branched))
        {
            return branched;
        }

        var match = _verifierRuntime.FallbackCandidateVerifier.FallbackRegex.Match(decoded);
        return new Utf8MatchContext(input, decoded, match, analysis.BoundaryMap, _groupNames);
    }

    /// <summary>Finds the first match and exposes its groups and captures over the original UTF-8 input, observing cancellation before matching begins.</summary>
    /// <param name="input">The well-formed UTF-8 input to match against.</param>
    /// <param name="cancellationToken">The token to observe before matching begins.</param>
    /// <exception cref="OperationCanceledException">The token was already canceled.</exception>
    public Utf8MatchContext MatchDetailed(ReadOnlySpan<byte> input, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return MatchDetailedCore(input, cancellationToken);
    }

    /// <summary>Finds the first detailed match at or after the specified UTF-16 code-unit offset.</summary>
    public Utf8MatchContext MatchDetailedFromUtf16Offset(ReadOnlySpan<byte> input, int utf16Offset)
    {
        try
        {
            return MatchDetailedFromUtf16OffsetCore(input, utf16Offset, CancellationToken.None);
        }
        catch (Utf8ExecutionDeadlineExpiredException)
        {
            throw CreateMatchTimeoutException(input);
        }
    }

    /// <summary>Finds the first detailed match at or after the specified UTF-16 code-unit offset, observing cancellation before matching begins.</summary>
    /// <param name="input">The well-formed UTF-8 input to match against.</param>
    /// <param name="utf16Offset">The zero-based UTF-16 code-unit offset of the position where matching starts.</param>
    /// <param name="cancellationToken">The token to observe before matching begins.</param>
    /// <exception cref="OperationCanceledException">The token was already canceled.</exception>
    public Utf8MatchContext MatchDetailedFromUtf16Offset(ReadOnlySpan<byte> input, int utf16Offset, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            return MatchDetailedFromUtf16OffsetCore(input, utf16Offset, cancellationToken);
        }
        catch (Utf8ExecutionDeadlineExpiredException)
        {
            throw CreateMatchTimeoutException(input);
        }
    }

    private Utf8MatchContext MatchDetailedFromUtf16OffsetCore(ReadOnlySpan<byte> input, int utf16Offset, CancellationToken cancellationToken)
    {
        var analysis = Utf8InputAnalyzer.Analyze(input);
        var start = analysis.GetUtf16Position(utf16Offset, nameof(utf16Offset));
        var decoded = analysis.GetDecodedString();
        // Anchored prefixes anchor to offset 0: a nonzero start is a definitive miss,
        // while offset 0 reuses the byte-exact prefix compare without BCL construction.
        if (_fallbackDirectFamily.Kind == Utf8FallbackDirectFamilyKind.AnchoredAsciiLiteralPrefix &&
            _fallbackDirectFamily.LiteralUtf8 is { Length: > 0 } &&
            _groupNumbers.Length <= 1)
        {
            Utf8SearchDiagnosticsSession.Current?.MarkExecutionRoute(Utf8ExecutionRoute.FallbackDirectAnchoredAsciiLiteralPrefix);
            if (start.Value == 0 &&
                Utf8AsciiAnchoredPrefixFamilyExecutor.TryMatchPrefix(input, _fallbackDirectFamily, out var anchoredOffsetLength))
            {
                return new Utf8MatchContext(input, decoded, analysis.BoundaryMap, _groupNames, 0, anchoredOffsetLength);
            }

            return Utf8MatchContext.Unsuccessful(input, decoded, analysis.BoundaryMap, _groupNames);
        }

        if (Utf8FallbackDirectFamilyCategories.IsAnchoredSuffixMatchFamily(_fallbackDirectFamily.Kind) &&
            _fallbackDirectFamily.LiteralUtf8 is { Length: > 0 } &&
            _groupNumbers.Length <= 1)
        {
            Utf8SearchDiagnosticsSession.Current?.MarkExecutionRoute(Utf8ExecutionRoute.FallbackDirectAnchoredAsciiLiteralSuffix);
            if (TryMatchDirectFallbackFamilyWithoutValidation(input, out var suffixOffsetDetailed) &&
                suffixOffsetDetailed.Success &&
                start.Value <= suffixOffsetDetailed.IndexInUtf16)
            {
                return new Utf8MatchContext(input, decoded, analysis.BoundaryMap, _groupNames, suffixOffsetDetailed.IndexInUtf16, suffixOffsetDetailed.LengthInUtf16);
            }

            return Utf8MatchContext.Unsuccessful(input, decoded, analysis.BoundaryMap, _groupNames);
        }

        if (_sliceProgram is not null && _sliceProgram.ServesInput(input))
        {
            return OwnedSliceMatchDetailedFromUnit(input, _sliceProgram, decoded, analysis.BoundaryMap, start.Value, cancellationToken);
        }
        if (TryGetOwnedDetailedGroupCount(out var ownedGroupCount))
        {
            // R03: first-of-shared-enumeration keeps origin semantics (absolute anchors,
            // boundaries, and lookarounds divert to full-input verification inside); shaping
            // stays owned for qualifying shapes.
            Utf8ValueMatch first = default;
            var found = false;
            foreach (var candidate in EnumerateMatchesFromUtf16Offset(input, utf16Offset))
            {
                first = candidate;
                found = true;
                break;
            }

            if (!found || !first.Success)
            {
                return Utf8MatchContext.Unsuccessful(input, decoded, analysis.BoundaryMap, _groupNames);
            }

            return ownedGroupCount > 1
                ? new Utf8MatchContext(input, decoded, analysis.BoundaryMap, _groupNames, first.IndexInUtf16, first.LengthInUtf16, ownedGroupCount)
                : new Utf8MatchContext(input, decoded, analysis.BoundaryMap, _groupNames, first.IndexInUtf16, first.LengthInUtf16);
        }

        if (_groupedLiteralSpanTable is not null &&
            !RequiresCultureSensitiveFallback &&
            !ShouldUseKelvinSignFallback(input))
        {
            // R05: grouped single-literal shapes keep origin semantics through
            // first-of-shared-enumeration and project from fixed spans, mirroring
            // the zero-offset arm above.
            Utf8ValueMatch groupedOffsetFirst = default;
            var groupedOffsetFound = false;
            foreach (var candidate in EnumerateMatchesFromUtf16Offset(input, utf16Offset))
            {
                groupedOffsetFirst = candidate;
                groupedOffsetFound = true;
                break;
            }

            if (!groupedOffsetFound || !groupedOffsetFirst.Success)
            {
                return Utf8MatchContext.Unsuccessful(input, decoded, analysis.BoundaryMap, _groupNames);
            }

            if (TryShapeGroupedLiteralDetailedMatch(input, decoded, analysis.BoundaryMap, groupedOffsetFirst, out var groupedOffsetDetailed))
            {
                return groupedOffsetDetailed;
            }
        }

        if (TryGetBranchedLiteralFamilyMap(out var branchMap) &&
            TryMatchDetailedBranchedLiteralFamily(input, decoded, analysis.BoundaryMap, branchMap, start.Value, out var branched))
        {
            return branched;
        }

        var match = _verifierRuntime.FallbackCandidateVerifier.FallbackRegex.Match(decoded, start.Value);
        return new Utf8MatchContext(input, decoded, match, analysis.BoundaryMap, _groupNames);
    }

    private bool TryGetOwnedDetailedGroupCount(out int groupCount)
    {
        // R03: groupless shapes (literal or simple) and single groups wrapping a whole
        // literal family discover through the shared value path and shape from owned
        // spans. Semantic declines inside the value path keep their existing BCL
        // routes; everything else keeps BCL discovery and shaping until capture
        // tracking migrates.
        groupCount = 1;
        // Anchored literal prefixes shape through the dedicated prefix-family arm above,
        // not through this literal-kernel gate (their ExecutionKind is FallbackRegex).
        if (_preparedRegex.ExecutionKind is not (NativeExecutionKind.ExactAsciiLiteral or NativeExecutionKind.ExactUtf8Literal or NativeExecutionKind.ExactUtf8Literals or NativeExecutionKind.AsciiLiteralIgnoreCase or NativeExecutionKind.AsciiLiteralIgnoreCaseLiterals or NativeExecutionKind.AsciiSimplePattern))
        {
            return false;
        }

        if (_groupNumbers.Length <= 1)
        {
            return true;
        }

        if (_groupNumbers.Length == 2 && GetWholeLiteralCaptureNumber() == 1)
        {
            groupCount = 2;
            return true;
        }

        return false;
    }

    private bool TryShapeGroupedLiteralDetailedMatch(
        ReadOnlySpan<byte> input,
        string decoded,
        Utf8BoundaryMap boundaryMap,
        Utf8ValueMatch hit,
        out Utf8MatchContext detailed)
    {
        // R05: projects every group from fixed literal-relative spans onto the owned
        // discovery hit. A hit-length disagreement or an out-of-range capture number
        // declines to the BCL routes instead of shaping blindly.
        detailed = default;
        var table = _groupedLiteralSpanTable;
        if (table is null || hit.LengthInUtf16 != table.TotalLengthInUtf16)
        {
            return false;
        }

        var groupCount = _groupNumbers.Length;
        var starts = new int[groupCount];
        var lengths = new int[groupCount];
        Array.Fill(starts, -1);
        starts[0] = hit.IndexInUtf16;
        lengths[0] = hit.LengthInUtf16;
        for (var i = 0; i < table.Groups.Length; i++)
        {
            var number = table.Groups[i];
            if ((uint)number >= (uint)groupCount)
            {
                return false;
            }

            starts[number] = hit.IndexInUtf16 + table.StartsInLiteral[i];
            lengths[number] = table.LengthsInLiteral[i];
        }

        detailed = new Utf8MatchContext(input, decoded, boundaryMap, _groupNames, hit.IndexInUtf16, hit.LengthInUtf16, starts, lengths);
        return true;
    }

    private bool TryGetBranchedLiteralFamilyMap(out Utf8LiteralFamilyCaptureMap? map)
    {
        // R03: multi-group top-level literal alternations with a branch-to-group map discover
        // through the family cursor and shape from owned spans. The cursor scans left to right,
        // so right-to-left shapes keep the BCL routes.
        map = null;
        if (_literalFamilyCaptureMap is null ||
            _preparedRegex.ExecutionKind is not (NativeExecutionKind.ExactUtf8Literals or NativeExecutionKind.AsciiLiteralIgnoreCaseLiterals) ||
            _groupNumbers.Length <= 2 ||
            RequiresCultureSensitiveFallback ||
            UsesRightToLeft())
        {
            return false;
        }

        map = _literalFamilyCaptureMap;
        return true;
    }

    // Adapter-owned split discovery: literal shapes (singular or family, grouped or not) share
    // one branch-indexed cursor over the construction-time capture map. False declines to the
    // adapter BCL routes; the caller validates branch identity on its first hit.
    internal bool TryCreateOwnedSplitCursor(ReadOnlySpan<byte> input, out Utf8OperationMatchCursor cursor, out Utf8LiteralFamilyCaptureMap? map)
    {
        cursor = default;
        map = null;
        if (_literalFamilyCaptureMap is null ||
            _preparedRegex.ExecutionKind is not (NativeExecutionKind.ExactAsciiLiteral or NativeExecutionKind.ExactUtf8Literal or NativeExecutionKind.ExactUtf8Literals or NativeExecutionKind.AsciiLiteralIgnoreCase or NativeExecutionKind.AsciiLiteralIgnoreCaseLiterals) ||
            RequiresCultureSensitiveFallback ||
            UsesRightToLeft() ||
            ShouldUseKelvinSignFallback(input))
        {
            return false;
        }

        var validation = TryUseAsciiInputValidationShortcut(input)
            ? default
            : Utf8Validation.Validate(input);
        cursor = Utf8CompiledOperationCursorFactory.CreateMatchCursor(
            _preparedRegex,
            _verifierRuntime,
            input,
            validation,
            CreateExecutionBudget());
        map = _literalFamilyCaptureMap;
        return true;
    }

    // Returns true when the branched path owns the result (hit or miss); false declines to BCL.
    private bool TryMatchDetailedBranchedLiteralFamily(
        ReadOnlySpan<byte> input,
        string decoded,
        Utf8BoundaryMap boundaryMap,
        Utf8LiteralFamilyCaptureMap map,
        int startInUtf16,
        out Utf8MatchContext detailed)
    {
        detailed = default;
        if (RequiresCultureSensitiveFallback || ShouldUseKelvinSignFallback(input))
        {
            return false;
        }

        var validation = TryUseAsciiInputValidationShortcut(input)
            ? default
            : Utf8Validation.Validate(input);
        var budget = CreateExecutionBudget();
        var cursor = Utf8CompiledOperationCursorFactory.CreateMatchCursor(
            _preparedRegex,
            _verifierRuntime,
            input,
            validation,
            budget);
        try
        {
            while (cursor.MoveNext())
            {
                var candidate = cursor.CurrentValueMatch;
                if (!candidate.Success || candidate.IndexInUtf16 < startInUtf16)
                {
                    continue;
                }

                var branch = cursor.LastLiteralFamilyBranchId;
                if (branch < 0 || (uint)branch >= (uint)map.BranchGroups.Length)
                {
                    return false;
                }

                detailed = CreateBranchedDetailedMatch(input, decoded, boundaryMap, map, branch, candidate.IndexInUtf16, candidate.LengthInUtf16);
                return true;
            }
        }
        catch (Utf8ExecutionDeadlineExpiredException)
        {
            throw CreateMatchTimeoutException(input);
        }

        detailed = Utf8MatchContext.Unsuccessful(input, decoded, boundaryMap, _groupNames);
        return true;
    }

    // Shapes one owned detailed view from a mapped branch hit shared by detailed matching
    // and evaluator replacement.
    private Utf8MatchContext CreateBranchedDetailedMatch(
        ReadOnlySpan<byte> input,
        string decoded,
        Utf8BoundaryMap boundaryMap,
        Utf8LiteralFamilyCaptureMap map,
        int branch,
        int indexInUtf16,
        int lengthInUtf16)
    {
        var starts = new int[_groupNumbers.Length];
        var lengths = new int[_groupNumbers.Length];
        Array.Fill(starts, -1);
        starts[0] = indexInUtf16;
        lengths[0] = lengthInUtf16;
        foreach (var whole in map.WholeMatchGroups)
        {
            if ((uint)whole < (uint)starts.Length)
            {
                starts[whole] = indexInUtf16;
                lengths[whole] = lengthInUtf16;
            }
        }

        foreach (var group in map.BranchGroups[branch])
        {
            if ((uint)group < (uint)starts.Length)
            {
                starts[group] = indexInUtf16;
                lengths[group] = lengthInUtf16;
            }
        }

        return new Utf8MatchContext(input, decoded, boundaryMap, _groupNames, indexInUtf16, lengthInUtf16, starts, lengths);
    }

    /// <summary>Enumerates non-overlapping matches in the well-formed UTF-8 input.</summary>
    public Utf8ValueMatchEnumerator EnumerateMatches(ReadOnlySpan<byte> input)
    {
        // Anchored prefixes yield at most one match at offset 0 through the
        // anchor-enforcing owned cursor, without BCL construction.
        if (_fallbackDirectFamily.Kind == Utf8FallbackDirectFamilyKind.AnchoredAsciiLiteralPrefix &&
            _fallbackDirectFamily.LiteralUtf8 is { Length: > 0 } anchoredEnumerateLiteral)
        {
            Utf8SearchDiagnosticsSession.Current?.MarkExecutionRoute(Utf8ExecutionRoute.FallbackDirectAnchoredAsciiLiteralPrefix);
            return new Utf8ValueMatchEnumerator(
                new Utf8OperationMatchCursor(input, anchoredEnumerateLiteral, CreateExecutionBudget()))
                .WithTimeoutMapping(input, Pattern, MatchTimeout);
        }

        // Anchored suffixes yield at most one match at the input end through the
        // anchor-enforcing owned cursor, without BCL construction.
        if (Utf8FallbackDirectFamilyCategories.IsAnchoredSuffixMatchFamily(_fallbackDirectFamily.Kind) &&
            _fallbackDirectFamily.LiteralUtf8 is { Length: > 0 })
        {
            Utf8SearchDiagnosticsSession.Current?.MarkExecutionRoute(Utf8ExecutionRoute.FallbackDirectAnchoredAsciiLiteralSuffix);
            return new Utf8ValueMatchEnumerator(
                new Utf8OperationMatchCursor(input, _fallbackDirectFamily.LiteralUtf8, CreateExecutionBudget(), _fallbackDirectFamily.Kind))
                .WithTimeoutMapping(input, Pattern, MatchTimeout);
        }

        if (TryGetAsciiCultureInvariantTwin(input, out var twin))
        {
            return twin.EnumerateMatches(input).WithTimeoutMapping(input, Pattern, MatchTimeout);
        }

        if (_sliceProgram is not null && _sliceProgram.ServesInput(input))
        {
            return OwnedSliceEnumerateMatches(input, _sliceProgram, CancellationToken.None)
                .WithTimeoutMapping(input, Pattern, MatchTimeout);
        }

        var subject = Utf8ValidatedInput.Create(input);
        var startOffsetInBytes = UsesRightToLeft() ? input.Length : 0;
        var start = subject.GetBytePosition(startOffsetInBytes, "startOffsetInBytes");
        return new Utf8ByteOffsetExecution(this).EnumerateMatches(subject, start)
            .WithTimeoutMapping(input, Pattern, MatchTimeout);
    }

    private Utf8ValueMatchEnumerator OwnedSliceEnumerateMatches(ReadOnlySpan<byte> input, Utf8SliceProgram program, CancellationToken token)
    {
        var map = Utf8BoundaryMap.Create(input);
        return new Utf8ValueMatchEnumerator(program, input, map, MatchTimeout, token);
    }

    /// <summary>Enumerates non-overlapping matches in the well-formed UTF-8 input, observing cancellation when enumeration begins.</summary>
    /// <param name="input">The well-formed UTF-8 input to match against.</param>
    /// <param name="cancellationToken">The token to observe when enumeration begins.</param>
    /// <exception cref="OperationCanceledException">The token was already canceled.</exception>
    public Utf8ValueMatchEnumerator EnumerateMatches(ReadOnlySpan<byte> input, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (_sliceProgram is not null && _sliceProgram.ServesInput(input))
        {
            return OwnedSliceEnumerateMatches(input, _sliceProgram, cancellationToken)
                .WithTimeoutMapping(input, Pattern, MatchTimeout);
        }

        return EnumerateMatches(input);
    }

    /// <summary>Enumerates non-overlapping matches starting at the specified UTF-16 code-unit offset.</summary>
    public Utf8ValueMatchEnumerator EnumerateMatchesFromUtf16Offset(ReadOnlySpan<byte> input, int utf16Offset)
    {
        var analysis = Utf8InputAnalyzer.Analyze(input);
        var startInUtf16 = analysis.GetUtf16Position(utf16Offset, nameof(utf16Offset));
        // Anchored prefixes from a nonzero start are a definitive miss; from offset 0
        // they reuse the single-match owned cursor without BCL construction.
        if (_fallbackDirectFamily.Kind == Utf8FallbackDirectFamilyKind.AnchoredAsciiLiteralPrefix &&
            _fallbackDirectFamily.LiteralUtf8 is { Length: > 0 } anchoredOffsetLiteral)
        {
            Utf8SearchDiagnosticsSession.Current?.MarkExecutionRoute(Utf8ExecutionRoute.FallbackDirectAnchoredAsciiLiteralPrefix);
            if (startInUtf16.Value != 0)
            {
                return new Utf8ValueMatchEnumerator(
                    new Utf8OperationMatchCursor(input[..0], Array.Empty<byte>(), CreateExecutionBudget()))
                    .WithTimeoutMapping(input, Pattern, MatchTimeout);
            }

            return new Utf8ValueMatchEnumerator(
                new Utf8OperationMatchCursor(input, anchoredOffsetLiteral, CreateExecutionBudget()))
                .WithTimeoutMapping(input, Pattern, MatchTimeout);
        }

        // Anchored suffixes match at most once at a fixed end position: a start past
        // it misses definitively, otherwise the single-match cursor decides.
        if (Utf8FallbackDirectFamilyCategories.IsAnchoredSuffixMatchFamily(_fallbackDirectFamily.Kind) &&
            _fallbackDirectFamily.LiteralUtf8 is { Length: > 0 })
        {
            Utf8SearchDiagnosticsSession.Current?.MarkExecutionRoute(Utf8ExecutionRoute.FallbackDirectAnchoredAsciiLiteralSuffix);
            if (TryMatchDirectFallbackFamilyWithoutValidation(input, out var suffixEnumerate) &&
                suffixEnumerate.Success &&
                startInUtf16.Value <= suffixEnumerate.IndexInUtf16)
            {
                return new Utf8ValueMatchEnumerator(
                    new Utf8OperationMatchCursor(input, _fallbackDirectFamily.LiteralUtf8, CreateExecutionBudget(), _fallbackDirectFamily.Kind))
                    .WithTimeoutMapping(input, Pattern, MatchTimeout);
            }

            return new Utf8ValueMatchEnumerator(
                new Utf8OperationMatchCursor(input[..0], Array.Empty<byte>(), CreateExecutionBudget()))
                .WithTimeoutMapping(input, Pattern, MatchTimeout);
        }

        if (!UsesRightToLeft())
        {
            var startBoundary = analysis.BoundaryMap.Resolve(startInUtf16.Value);
            if (startBoundary.IsScalarBoundary)
            {
                var startInBytes = analysis.GetBytePosition(startBoundary.ByteOffset, nameof(utf16Offset));
                return new Utf8ByteOffsetExecution(this).EnumerateMatches(analysis, startInBytes)
                    .WithTimeoutMapping(input, Pattern, MatchTimeout);
            }
        }

        var decoded = analysis.GetDecodedString();
        return new Utf8ValueMatchEnumerator(
            input,
            _verifierRuntime.FallbackCandidateVerifier.FallbackRegex,
            decoded,
            startInUtf16.Value,
            analysis.Utf16ProjectionMap)
            .WithTimeoutMapping(input, Pattern, MatchTimeout);
    }

    /// <summary>Enumerates non-overlapping matches starting at the specified UTF-16 code-unit offset, observing cancellation when enumeration begins.</summary>
    /// <param name="input">The well-formed UTF-8 input to match against.</param>
    /// <param name="utf16Offset">The zero-based UTF-16 code-unit offset of the position where matching starts.</param>
    /// <param name="cancellationToken">The token to observe when enumeration begins.</param>
    /// <exception cref="OperationCanceledException">The token was already canceled.</exception>
    public Utf8ValueMatchEnumerator EnumerateMatchesFromUtf16Offset(ReadOnlySpan<byte> input, int utf16Offset, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return EnumerateMatchesFromUtf16Offset(input, utf16Offset);
    }

    /// <summary>Enumerates all split segments, including the final tail.</summary>
    public Utf8ValueSplitEnumerator EnumerateSplits(ReadOnlySpan<byte> input) =>
        EnumerateSplits(input, int.MaxValue);

    /// <summary>Enumerates all split segments, including the final tail, observing cancellation when enumeration begins.</summary>
    /// <param name="input">The well-formed UTF-8 input to match against.</param>
    /// <param name="cancellationToken">The token to observe when enumeration begins.</param>
    /// <exception cref="OperationCanceledException">The token was already canceled.</exception>
    public Utf8ValueSplitEnumerator EnumerateSplits(ReadOnlySpan<byte> input, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return EnumerateSplits(input);
    }

    /// <summary>Enumerates at most the requested number of split segments.</summary>
    public Utf8ValueSplitEnumerator EnumerateSplits(ReadOnlySpan<byte> input, int count)
    {
        if (TryGetAsciiCultureInvariantTwin(input, out var twin))
        {
            return twin.EnumerateSplits(input, count).WithTimeoutMapping(Pattern, MatchTimeout);
        }

        if (RequiresCultureSensitiveFallback || UsesRightToLeft() || ShouldUseKelvinSignFallback(input))
        {
            var subject = Utf8InputAnalyzer.Analyze(input);
            return new Utf8ValueSplitEnumerator(input, subject.GetDecodedString(), _verifierRuntime.FallbackCandidateVerifier.FallbackRegex, count, subject.BoundaryMap)
                .WithTimeoutMapping(Pattern, MatchTimeout);
        }

        if (count > 0 && TryGetSmallAsciiLiteralFamilySplitSearch(input, out var smallAsciiLiteralFamilySearch))
        {
            return new Utf8ValueSplitEnumerator(
                input,
                smallAsciiLiteralFamilySearch,
                count)
                .WithTimeoutMapping(Pattern, MatchTimeout);
        }


        var budget = CreateExecutionBudget();
        var usesAsciiValidationShortcut = TryUseAsciiInputValidationShortcut(input);
        var validation = usesAsciiValidationShortcut
            ? default
            : Utf8Validation.Validate(input);
        var totalUtf16Length = usesAsciiValidationShortcut
            ? input.Length
            : validation.Utf16Length;
        if (count > 0 && CanUseNativeSplit(validation))
        {
            return CreateSplitEnumeratorViaCompiledEngine(input, validation, totalUtf16Length, count, budget)
                .WithTimeoutMapping(Pattern, MatchTimeout);
        }

        // R03: split segments project the owned match cursor, whose empty-match
        // advancement already follows the BCL search order. Counts below 2 keep the
        // BCL tail below with identical behavior (count 0 means unlimited there,
        // while count 1 throws inside its first MoveNext). Right-to-left splits keep
        // legacy routing until the enumerator stages buffered right-to-left ranges
        // (segments stay positional while discovery runs right-to-left).
        if (count > 1 && _sliceProgram is not null && !_sliceProgram.RightToLeft && _sliceProgram.ServesInput(input))
        {
            var analysis = Utf8InputAnalyzer.Analyze(input);
            var decoded = analysis.GetDecodedString();
            return new Utf8ValueSplitEnumerator(
                input,
                decoded,
                new Utf8OperationMatchCursor(_sliceProgram, input, analysis.BoundaryMap, MatchTimeout, CancellationToken.None),
                analysis.BoundaryMap,
                count,
                decoded.Length)
                .WithTimeoutMapping(Pattern, MatchTimeout);
        }

        var fallbackSubject = Utf8InputAnalyzer.Analyze(input);
        return new Utf8ValueSplitEnumerator(input, fallbackSubject.GetDecodedString(), _verifierRuntime.FallbackCandidateVerifier.FallbackRegex, count, fallbackSubject.BoundaryMap)
            .WithTimeoutMapping(Pattern, MatchTimeout);
    }

    /// <summary>Enumerates at most the requested number of split segments, observing cancellation when enumeration begins.</summary>
    /// <param name="input">The well-formed UTF-8 input to match against.</param>
    /// <param name="count">The maximum number of split segments to return.</param>
    /// <param name="cancellationToken">The token to observe when enumeration begins.</param>
    /// <exception cref="OperationCanceledException">The token was already canceled.</exception>
    public Utf8ValueSplitEnumerator EnumerateSplits(ReadOnlySpan<byte> input, int count, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return EnumerateSplits(input, count);
    }

    /// <summary>Replaces all matches and returns newly allocated UTF-8 bytes.</summary>
    /// <param name="input">The well-formed UTF-8 input.</param>
    /// <param name="replacement">A .NET replacement pattern expressed as UTF-16 text.</param>
    public byte[] Replace(ReadOnlySpan<byte> input, string replacement)
    {
        try
        {
            return ReplaceStringCore(input, replacement);
        }
        catch (Utf8ExecutionDeadlineExpiredException)
        {
            throw CreateMatchTimeoutException(input);
        }
    }

    /// <summary>Replaces all matches and returns newly allocated UTF-8 bytes, observing cancellation before matching begins.</summary>
    /// <param name="input">The well-formed UTF-8 input to match against.</param>
    /// <param name="replacement">The replacement text.</param>
    /// <param name="cancellationToken">The token to observe before matching begins.</param>
    /// <exception cref="OperationCanceledException">The token was already canceled.</exception>
    public byte[] Replace(ReadOnlySpan<byte> input, string replacement, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            return ReplaceStringCore(input, replacement, cancellationToken);
        }
        catch (Utf8ExecutionDeadlineExpiredException)
        {
            throw CreateMatchTimeoutException(input);
        }
    }

    private byte[] ReplaceStringCore(ReadOnlySpan<byte> input, string replacement)
    {
        return ReplaceStringCore(input, replacement, CancellationToken.None);
    }

    private byte[] ReplaceStringCore(ReadOnlySpan<byte> input, string replacement, CancellationToken cancellationToken)
    {
        var analyzed = GetParsedReplacement(replacement);
        if (TryGetAsciiCultureInvariantTwin(input, out var twin))
        {
            return Utf8AsciiCultureInvariantReplacement.Replace(twin, input, analyzed);
        }

        return ReplaceCore(input, replacement, analyzed, cancellationToken);
    }

    private byte[] OwnedSliceReplace(
        ReadOnlySpan<byte> input,
        Utf8SliceProgram program,
        Utf8AnalyzedReplacement replacement,
        CancellationToken token)
    {
        var map = Utf8BoundaryMap.Create(input);
        var invocation = new Utf8SliceInvocation(Utf8ExecutionLimits.Unlimited, MatchTimeout, token);
        var slots = Utf8SliceRunner.RentSlots(program.GroupCount, ref invocation);
        if (!Utf8SliceOutputWriter.Rent(input.Length, ref invocation, out var writer))
        {
            throw new InvalidOperationException("Owned slice output storage breached an unlimited invocation budget.");
        }

        // Right-to-left replacement discovers the right-to-left tiling (which can
        // differ from the left-to-right tiling) through the shared cursor, then emits
        // left-to-right with one deterministic single-attempt detailed re-query per
        // match. Ranges stage as unit pairs in a ledger-charged buffer; capture
        // histories materialize per emitted match exactly like the forward loop.
        static void AppendRtlReplaceRange(ref int[] ranges, ref int rangeCount, int startUnit, int endUnit, ref Utf8SliceInvocation rangeInvocation)
        {
            if (rangeCount * 2 + 2 > ranges.Length)
            {
                var grownLength = ranges.Length == 0 ? 16 : ranges.Length * 2;
                if (!rangeInvocation.Ledger.TryReserve((ulong)grownLength * 4))
                {
                    throw new InvalidOperationException("Owned slice output storage breached an unlimited invocation budget.");
                }

                var grown = new int[grownLength];
                Array.Copy(ranges, grown, ranges.Length);
                if (ranges.Length != 0)
                {
                    rangeInvocation.Ledger.Release((ulong)ranges.Length * 4);
                }

                ranges = grown;
            }

            ranges[rangeCount * 2] = startUnit;
            ranges[rangeCount * 2 + 1] = endUnit;
            rangeCount++;
        }

        var rtlRanges = Array.Empty<int>();
        var rtlRangeCount = 0;
        var unit = 0;
        var lastEndByte = 0;
        // Contiguous matches chain BCL startat across scans (advancing to each
        // match end, including past empty matches whose positions all miss).
        var scanByte = 0;
        try
        {
            if (program.RightToLeft)
            {
                var rtlCursor = new Utf8OperationMatchCursor(program, input, map, MatchTimeout, token);
                while (rtlCursor.MoveNext())
                {
                    var rtlCurrent = rtlCursor.Current;
                    AppendRtlReplaceRange(ref rtlRanges, ref rtlRangeCount, rtlCurrent.IndexInUtf16, rtlCurrent.IndexInUtf16 + rtlCurrent.LengthInUtf16, ref invocation);
                }

                var rtlEmitByte = 0;
                for (var rtlMatch = rtlRangeCount - 1; rtlMatch >= 0; rtlMatch--)
                {
                    var rtlStartUnit = rtlRanges[rtlMatch * 2];
                    var rtlEndUnit = rtlRanges[rtlMatch * 2 + 1];
                    var rtlStartByte = rtlStartUnit >= map.Utf16Length ? input.Length : map.Resolve(rtlStartUnit).ByteOffset;
                    var rtlEdgeByte = rtlEndUnit >= map.Utf16Length ? input.Length : map.Resolve(rtlEndUnit).ByteOffset;
                    // The re-query reproduces its discovery attempt exactly (same
                    // edge, same edge alignment, same chain pin), so the detailed
                    // outcome always agrees; anything else fails closed.
                    invocation.AnchorOriginByte = rtlMatch == 0 ? input.Length : map.Resolve(rtlRanges[(rtlMatch - 1) * 2]).ByteOffset;
                    var rtlEdgeClean = rtlEndUnit >= map.Utf16Length || rtlEndUnit <= 0 || map.Resolve(rtlEndUnit).IsScalarBoundary;
                    var rtlDetail = Utf8SliceRunner.TryMatchAt(program, input, rtlEdgeByte, slots.Starts, slots.Ends, true, rtlEdgeClean, ref invocation);
                    if (rtlDetail.Breach != Utf8ExecutionLimitKind.None)
                    {
                        throw new InvalidOperationException("Owned slice execution breached an unlimited invocation budget.");
                    }

                    if (!rtlDetail.IsMatch || rtlDetail.EndIndex != rtlStartByte || rtlDetail.CaptureStarts is null || rtlDetail.CaptureLengths is null)
                    {
                        throw new InvalidOperationException("Owned slice detailed re-query missed its discovered match.");
                    }

                    var rtlOutcome = new Utf8SliceDetailedOutcome(
                        true, rtlStartUnit, rtlEndUnit, rtlStartByte, rtlEdgeByte,
                        rtlDetail.CaptureStarts, rtlDetail.CaptureLengths, Utf8ExecutionLimitKind.None);
                    if (!writer.Append(input.Slice(rtlEmitByte, rtlStartByte - rtlEmitByte), ref invocation) ||
                        !ExpandOwnedSliceTemplate(ref writer, replacement, input, rtlOutcome, program.GroupCount, ref invocation))
                    {
                        throw new InvalidOperationException("Owned slice output storage breached an unlimited invocation budget.");
                    }

                    Utf8SliceRunner.ReleaseHistory(rtlDetail.CaptureStarts, rtlDetail.CaptureLengths, ref invocation);
                    rtlEmitByte = rtlEdgeByte;
                }

                if (!writer.Append(input.Slice(rtlEmitByte), ref invocation))
                {
                    throw new InvalidOperationException("Owned slice output storage breached an unlimited invocation budget.");
                }

                return writer.ToArrayAndRelease(ref invocation);
            }

            while (unit <= map.Utf16Length)
            {
                invocation.AnchorOriginByte = scanByte;
                var outcome = Utf8SliceRunner.TryFindFirstDetailed(program, input, map, unit, slots.Starts, slots.Ends, ref invocation);
                if (outcome.Breach != Utf8ExecutionLimitKind.None)
                {
                    throw new InvalidOperationException("Owned slice execution breached an unlimited invocation budget.");
                }

                if (!outcome.Found || outcome.CaptureStarts is null || outcome.CaptureLengths is null)
                {
                    break;
                }

                if (!writer.Append(input.Slice(lastEndByte, outcome.StartByte - lastEndByte), ref invocation) ||
                    !ExpandOwnedSliceTemplate(ref writer, replacement, input, outcome, program.GroupCount, ref invocation))
                {
                    throw new InvalidOperationException("Owned slice output storage breached an unlimited invocation budget.");
                }

                Utf8SliceRunner.ReleaseHistory(outcome.CaptureStarts, outcome.CaptureLengths, ref invocation);
                lastEndByte = outcome.EndByte;
                unit = outcome.EndUnit > outcome.StartUnit ? outcome.EndUnit : outcome.StartUnit + 1;
                scanByte = outcome.EndByte;
            }

            if (!writer.Append(input.Slice(lastEndByte), ref invocation))
            {
                throw new InvalidOperationException("Owned slice output storage breached an unlimited invocation budget.");
            }

            return writer.ToArrayAndRelease(ref invocation);
        }
        finally
        {
            if (rtlRanges.Length != 0)
            {
                invocation.Ledger.Release((ulong)rtlRanges.Length * 4);
            }

            writer.Release(ref invocation);
            Utf8SliceRunner.ReleaseSlots(slots, ref invocation);
        }
    }

    private static bool ExpandOwnedSliceTemplate(
        ref Utf8SliceOutputWriter writer,
        Utf8AnalyzedReplacement replacement,
        ReadOnlySpan<byte> input,
        Utf8SliceDetailedOutcome outcome,
        int groups,
        ref Utf8SliceInvocation invocation)
    {
        foreach (var instruction in replacement.Plan.Instructions)
        {
            switch (instruction.Kind)
            {
                case Utf8ReplacementInstructionKind.Literal:
                    if (!writer.Append(instruction.LiteralUtf8, ref invocation))
                    {
                        return false;
                    }

                    break;
                case Utf8ReplacementInstructionKind.Group:
                    if (!AppendOwnedSliceGroup(ref writer, input, outcome, instruction.GroupNumber, groups, ref invocation))
                    {
                        return false;
                    }

                    break;
                case Utf8ReplacementInstructionKind.WholeMatch:
                    if (!writer.Append(input.Slice(outcome.StartByte, outcome.EndByte - outcome.StartByte), ref invocation))
                    {
                        return false;
                    }

                    break;
                case Utf8ReplacementInstructionKind.LeftPortion:
                    if (!writer.Append(input.Slice(0, outcome.StartByte), ref invocation))
                    {
                        return false;
                    }

                    break;
                case Utf8ReplacementInstructionKind.RightPortion:
                    if (!writer.Append(input.Slice(outcome.EndByte), ref invocation))
                    {
                        return false;
                    }

                    break;
                case Utf8ReplacementInstructionKind.LastGroup:
                    if (!AppendOwnedSliceLastGroup(ref writer, input, outcome, groups, ref invocation))
                    {
                        return false;
                    }

                    break;
                case Utf8ReplacementInstructionKind.WholeString:
                    if (!writer.Append(input, ref invocation))
                    {
                        return false;
                    }

                    break;
                default:
                    throw new InvalidOperationException("Unsupported replacement instruction kind.");
            }
        }

        return true;
    }

    private static bool AppendOwnedSliceGroup(
        ref Utf8SliceOutputWriter writer,
        ReadOnlySpan<byte> input,
        Utf8SliceDetailedOutcome outcome,
        int groupNumber,
        int groups,
        ref Utf8SliceInvocation invocation)
    {
        if (groupNumber == 0)
        {
            return writer.Append(input.Slice(outcome.StartByte, outcome.EndByte - outcome.StartByte), ref invocation);
        }

        if (outcome.CaptureStarts is null || outcome.CaptureLengths is null ||
            groupNumber < 1 || groupNumber > groups)
        {
            return true;
        }

        var starts = outcome.CaptureStarts[groupNumber - 1];
        var lengths = outcome.CaptureLengths[groupNumber - 1];
        if (starts.Length == 0)
        {
            return true;
        }

        return writer.Append(
            input.Slice(starts[^1], lengths[^1]),
            ref invocation);
    }

    private static bool AppendOwnedSliceLastGroup(
        ref Utf8SliceOutputWriter writer,
        ReadOnlySpan<byte> input,
        Utf8SliceDetailedOutcome outcome,
        int groups,
        ref Utf8SliceInvocation invocation)
    {
        if (outcome.CaptureStarts is null || outcome.CaptureLengths is null)
        {
            return true;
        }

        for (var group = groups; group >= 1; group--)
        {
            var starts = outcome.CaptureStarts[group - 1];
            var lengths = outcome.CaptureLengths[group - 1];
            if (starts.Length != 0)
            {
                return writer.Append(input.Slice(starts[^1], lengths[^1]), ref invocation);
            }
        }

        return true;
    }

    /// <summary>Replaces all matches using a .NET replacement pattern encoded as well-formed UTF-8.</summary>
    public byte[] Replace(ReadOnlySpan<byte> input, ReadOnlySpan<byte> replacementPatternUtf8)
    {
        try
        {
            return ReplaceUtf8Core(input, replacementPatternUtf8);
        }
        catch (Utf8ExecutionDeadlineExpiredException)
        {
            throw CreateMatchTimeoutException(input);
        }
    }

    /// <summary>Replaces all matches using a .NET replacement pattern encoded as well-formed UTF-8, observing cancellation before matching begins.</summary>
    /// <param name="input">The well-formed UTF-8 input to match against.</param>
    /// <param name="replacementPatternUtf8">The replacement pattern encoded as well-formed UTF-8.</param>
    /// <param name="cancellationToken">The token to observe before matching begins.</param>
    /// <exception cref="OperationCanceledException">The token was already canceled.</exception>
    public byte[] Replace(ReadOnlySpan<byte> input, ReadOnlySpan<byte> replacementPatternUtf8, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            return ReplaceUtf8Core(input, replacementPatternUtf8, cancellationToken);
        }
        catch (Utf8ExecutionDeadlineExpiredException)
        {
            throw CreateMatchTimeoutException(input);
        }
    }

    private byte[] ReplaceUtf8Core(ReadOnlySpan<byte> input, ReadOnlySpan<byte> replacementPatternUtf8)
    {
        return ReplaceUtf8Core(input, replacementPatternUtf8, CancellationToken.None);
    }

    private byte[] ReplaceUtf8Core(ReadOnlySpan<byte> input, ReadOnlySpan<byte> replacementPatternUtf8, CancellationToken cancellationToken)
    {
        var validation = TryUseAsciiInputValidationShortcut(input)
            ? default
            : Utf8Validation.Validate(input);
        _ = Utf8Validation.Validate(replacementPatternUtf8);
        if (RequiresCultureSensitiveFallback || ShouldUseKelvinSignFallback(input))
        {
            return Encoding.UTF8.GetBytes(_verifierRuntime.FallbackCandidateVerifier.FallbackRegex.Replace(
                Encoding.UTF8.GetString(input),
                Encoding.UTF8.GetString(replacementPatternUtf8)));
        }

        if (_sliceProgram is not null && _sliceProgram.ServesInput(input))
        {
            return OwnedSliceReplace(
                input,
                _sliceProgram,
                GetParsedReplacement(Encoding.UTF8.GetString(replacementPatternUtf8)),
                cancellationToken);
        }

        if (TryGetDirectLiteralReplacementBytes(replacementPatternUtf8, out var directReplacementBytes))
        {
            return ReplaceLiteralBytesCore(input, validation, directReplacementBytes);
        }

        var replacementText = Encoding.UTF8.GetString(replacementPatternUtf8);
        var replacementPattern = GetParsedReplacement(replacementText);
        var budget = CreateExecutionBudget();
        if (ShouldUseFallbackForSimplePatternReplacement(replacementPattern))
        {
            return Encoding.UTF8.GetBytes(_verifierRuntime.FallbackCandidateVerifier.FallbackRegex.Replace(
                Encoding.UTF8.GetString(input),
                replacementText));
        }

        if (TryGetExactLiteralReplacementBytes(replacementPattern, out var exactLiteralReplacementBytes))
        {
            return ReplaceLiteralBytesCore(input, validation, exactLiteralReplacementBytes);
        }

        if (TryReplaceViaNativePlan(input, validation, replacementPattern, budget, out var nativeResult))
        {
            return nativeResult;
        }

        if (!TryGetNativeReplacementBytes(replacementPattern, out var replacementBytes))
        {
            return Encoding.UTF8.GetBytes(ReplaceFallbackWithSharedPlan(input, replacementPattern));
        }

        return ReplaceLiteralBytesCore(input, validation, replacementBytes);
    }

    /// <summary>Replaces all matches through a stateful callback that writes UTF-8 replacement content.</summary>
    /// <typeparam name="TState">The caller-defined state type.</typeparam>
    /// <exception cref="InvalidOperationException">A match splits a UTF-8 scalar.</exception>
    /// <exception cref="ArgumentException">The evaluator produces malformed UTF-8.</exception>
    public byte[] Replace<TState>(ReadOnlySpan<byte> input, TState state, Utf8MatchEvaluator<TState> evaluator)
    {
        return ReplaceEvaluatorCore(input, state, evaluator, default);
    }

    private byte[] ReplaceEvaluatorCore<TState>(ReadOnlySpan<byte> input, TState state, Utf8MatchEvaluator<TState> evaluator, CancellationToken cancellationToken)
    {
        var validation = Utf8Validation.Validate(input);
        var decoded = Encoding.UTF8.GetString(input);
        var boundaryMap = Utf8BoundaryMap.Create(input, validation);
        var output = new ArrayBufferWriter<byte>(input.Length);
        var writer = new Utf8ReplacementWriter(output);
        var sourcePosition = 0;
        // R03: patterns without captures project every callback argument from owned spans,
        // so discovery and shaping both run without the BCL engine; other patterns keep it.
        if (_groupNumbers.Length <= 1)
        {
            foreach (var valueMatch in EnumerateMatches(input))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!valueMatch.IsByteAligned)
                {
                    throw new InvalidOperationException("The evaluator match is not aligned to UTF-8 scalar boundaries.");
                }

                writer.Append(input[sourcePosition..valueMatch.IndexInBytes]);
                var context = new Utf8MatchContext(input, decoded, boundaryMap, _groupNames, valueMatch.IndexInUtf16, valueMatch.LengthInUtf16);
                evaluator(in context, ref writer, ref state);
                sourcePosition = valueMatch.IndexInBytes + valueMatch.LengthInBytes;
            }

            writer.Append(input[sourcePosition..]);
            return writer.GetValidatedBytes().ToArray();
        }
        // R03: single groups wrapping a whole literal family enumerate like groupless
        // shapes and project group 1 from the whole span.
        if (TryGetOwnedDetailedGroupCount(out var ownedGroupCount) && ownedGroupCount > 1)
        {
            foreach (var valueMatch in EnumerateMatches(input))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!valueMatch.IsByteAligned)
                {
                    throw new InvalidOperationException("The evaluator match is not aligned to UTF-8 scalar boundaries.");
                }

                writer.Append(input[sourcePosition..valueMatch.IndexInBytes]);
                var context = new Utf8MatchContext(input, decoded, boundaryMap, _groupNames, valueMatch.IndexInUtf16, valueMatch.LengthInUtf16, ownedGroupCount);
                evaluator(in context, ref writer, ref state);
                sourcePosition = valueMatch.IndexInBytes + valueMatch.LengthInBytes;
            }

            writer.Append(input[sourcePosition..]);
            return writer.GetValidatedBytes().ToArray();
        }


        // R03: mapped branch shapes discover through the family cursor and shape every
        // callback argument from owned spans. The first hit validates the branch channel;
        // no output is produced before it, so an unknown branch still declines cleanly.
        if (TryGetBranchedLiteralFamilyMap(out var branchMap) &&
            !RequiresCultureSensitiveFallback &&
            !ShouldUseKelvinSignFallback(input))
        {
            var budget = CreateExecutionBudget();
            var cursor = Utf8CompiledOperationCursorFactory.CreateMatchCursor(
                _preparedRegex,
                _verifierRuntime,
                input,
                validation,
                budget);
            var declined = false;
            try
            {
                var first = true;
                while (cursor.MoveNext())
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var candidate = cursor.CurrentValueMatch;
                    if (!candidate.IsByteAligned)
                    {
                        throw new InvalidOperationException("The evaluator match is not aligned to UTF-8 scalar boundaries.");
                    }

                    var branch = cursor.LastLiteralFamilyBranchId;
                    if (first)
                    {
                        first = false;
                        if (branch < 0 || (uint)branch >= (uint)branchMap.BranchGroups.Length)
                        {
                            declined = true;
                            break;
                        }
                    }

                    writer.Append(input[sourcePosition..candidate.IndexInBytes]);
                    var context = CreateBranchedDetailedMatch(input, decoded, boundaryMap, branchMap, branch, candidate.IndexInUtf16, candidate.LengthInUtf16);
                    evaluator(in context, ref writer, ref state);
                    sourcePosition = candidate.IndexInBytes + candidate.LengthInBytes;
                }
            }
            catch (Utf8ExecutionDeadlineExpiredException)
            {
                throw CreateMatchTimeoutException(input);
            }

            if (!declined)
            {
                writer.Append(input[sourcePosition..]);
                return writer.GetValidatedBytes().ToArray();
            }
        }

        var match = _verifierRuntime.FallbackCandidateVerifier.FallbackRegex.Match(decoded);
        while (match.Success)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var matchStart = boundaryMap.Resolve(match.Index);
            var matchEnd = boundaryMap.Resolve(match.Index + match.Length);
            if (!matchStart.IsScalarBoundary || !matchEnd.IsScalarBoundary)
            {
                throw new InvalidOperationException("The evaluator match is not aligned to UTF-8 scalar boundaries.");
            }

            writer.Append(input[sourcePosition..matchStart.ByteOffset]);
            var context = new Utf8MatchContext(input, decoded, match, boundaryMap, _groupNames);
            evaluator(in context, ref writer, ref state);
            sourcePosition = matchEnd.ByteOffset;
            match = match.NextMatch();
        }

        writer.Append(input[sourcePosition..]);
        return writer.GetValidatedBytes().ToArray();
    }

    /// <summary>Replaces all matches through a stateful callback that writes UTF-8 replacement content, observing cancellation before matching begins.</summary>
    /// <typeparam name="TState">The type of additional state passed to the evaluator.</typeparam>
    /// <param name="input">The well-formed UTF-8 input to match against.</param>
    /// <param name="state">Additional state passed to the evaluator.</param>
    /// <param name="evaluator">The callback invoked for each match to append replacement content.</param>
    /// <param name="cancellationToken">The token to observe before matching begins.</param>
    /// <exception cref="OperationCanceledException">The token was already canceled.</exception>
    public byte[] Replace<TState>(ReadOnlySpan<byte> input, TState state, Utf8MatchEvaluator<TState> evaluator, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ReplaceEvaluatorCore(input, state, evaluator, cancellationToken);
    }

    /// <summary>Replaces all matches and returns the result as a UTF-16 string.</summary>
    public string ReplaceToString(ReadOnlySpan<byte> input, string replacement)
    {
        try
        {
            return ReplaceToStringMappedCore(input, replacement);
        }
        catch (Utf8ExecutionDeadlineExpiredException)
        {
            throw CreateMatchTimeoutException(input);
        }
    }

    /// <summary>Replaces all matches and returns the result as a UTF-16 string, observing cancellation before matching begins.</summary>
    /// <param name="input">The well-formed UTF-8 input to match against.</param>
    /// <param name="replacement">The replacement text.</param>
    /// <param name="cancellationToken">The token to observe before matching begins.</param>
    /// <exception cref="OperationCanceledException">The token was already canceled.</exception>
    public string ReplaceToString(ReadOnlySpan<byte> input, string replacement, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ReplaceToString(input, replacement);
    }

    private string ReplaceToStringMappedCore(ReadOnlySpan<byte> input, string replacement)
    {
        var analyzed = GetParsedReplacement(replacement);
        if (TryGetAsciiCultureInvariantTwin(input, out var twin))
        {
            return Utf8AsciiCultureInvariantReplacement.ReplaceToString(twin, input, analyzed);
        }

        _ = Utf8Validation.Validate(input);
        return ReplaceToStringCore(input, analyzed);
    }

    /// <summary>Replaces all matches through a stateful callback that returns UTF-16 replacement text.</summary>
    /// <typeparam name="TState">The caller-defined state type.</typeparam>
    public string ReplaceToString<TState>(ReadOnlySpan<byte> input, TState state, Utf16MatchEvaluator<TState> evaluator)
    {
        return ReplaceToStringEvaluatorCore(input, state, evaluator, default);
    }

    private string ReplaceToStringEvaluatorCore<TState>(ReadOnlySpan<byte> input, TState state, Utf16MatchEvaluator<TState> evaluator, CancellationToken cancellationToken)
    {
        var validation = Utf8Validation.Validate(input);
        var inputBytes = input.ToArray();
        var decoded = Encoding.UTF8.GetString(input);
        var boundaryMap = Utf8InputAnalyzer.Analyze(inputBytes).BoundaryMap;
        // R03: groupless matches enumerate and shape from owned spans; string slicing
        // needs no byte alignment, matching the BCL oracle on every input.
        if (_groupNumbers.Length <= 1)
        {
            var builder = new StringBuilder(decoded.Length);
            var sourcePosition = 0;
            foreach (var valueMatch in EnumerateMatches(input))
            {
                cancellationToken.ThrowIfCancellationRequested();
                builder.Append(decoded, sourcePosition, valueMatch.IndexInUtf16 - sourcePosition);
                var context = new Utf8MatchContext(input, decoded, boundaryMap, _groupNames, valueMatch.IndexInUtf16, valueMatch.LengthInUtf16);
                builder.Append(evaluator(in context, ref state));
                sourcePosition = valueMatch.IndexInUtf16 + valueMatch.LengthInUtf16;
            }

            builder.Append(decoded, sourcePosition, decoded.Length - sourcePosition);
            return builder.ToString();
        }
        // R03: single groups wrapping a whole literal family enumerate like groupless
        // shapes and project group 1 from the whole span.
        if (TryGetOwnedDetailedGroupCount(out var ownedGroupCount) && ownedGroupCount > 1)
        {
            var singleBuilder = new StringBuilder(decoded.Length);
            var singlePosition = 0;
            foreach (var valueMatch in EnumerateMatches(input))
            {
                cancellationToken.ThrowIfCancellationRequested();
                singleBuilder.Append(decoded, singlePosition, valueMatch.IndexInUtf16 - singlePosition);
                var singleContext = new Utf8MatchContext(input, decoded, boundaryMap, _groupNames, valueMatch.IndexInUtf16, valueMatch.LengthInUtf16, ownedGroupCount);
                singleBuilder.Append(evaluator(in singleContext, ref state));
                singlePosition = valueMatch.IndexInUtf16 + valueMatch.LengthInUtf16;
            }

            singleBuilder.Append(decoded, singlePosition, decoded.Length - singlePosition);
            return singleBuilder.ToString();
        }


        // R03: mapped branch shapes discover through the family cursor and shape every
        // callback argument from owned spans, with the same first-hit decline contract as
        // the byte-wise evaluator overload.
        if (TryGetBranchedLiteralFamilyMap(out var branchMap) &&
            !RequiresCultureSensitiveFallback &&
            !ShouldUseKelvinSignFallback(input))
        {
            var budget = CreateExecutionBudget();
            var cursor = Utf8CompiledOperationCursorFactory.CreateMatchCursor(
                _preparedRegex,
                _verifierRuntime,
                input,
                validation,
                budget);
            var builder = new StringBuilder(decoded.Length);
            var sourcePosition = 0;
            var declined = false;
            try
            {
                var first = true;
                while (cursor.MoveNext())
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var candidate = cursor.CurrentValueMatch;
                    var branch = cursor.LastLiteralFamilyBranchId;
                    if (first)
                    {
                        first = false;
                        if (branch < 0 || (uint)branch >= (uint)branchMap.BranchGroups.Length)
                        {
                            declined = true;
                            break;
                        }
                    }

                    builder.Append(decoded, sourcePosition, candidate.IndexInUtf16 - sourcePosition);
                    var context = CreateBranchedDetailedMatch(input, decoded, boundaryMap, branchMap, branch, candidate.IndexInUtf16, candidate.LengthInUtf16);
                    builder.Append(evaluator(in context, ref state));
                    sourcePosition = candidate.IndexInUtf16 + candidate.LengthInUtf16;
                }
            }
            catch (Utf8ExecutionDeadlineExpiredException)
            {
                throw CreateMatchTimeoutException(input);
            }

            if (!declined)
            {
                builder.Append(decoded, sourcePosition, decoded.Length - sourcePosition);
                return builder.ToString();
            }
        }

        return _verifierRuntime.FallbackCandidateVerifier.FallbackRegex.Replace(
            decoded,
            match =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                var context = new Utf8MatchContext(inputBytes, decoded, match, boundaryMap, _groupNames);
                return evaluator(in context, ref state);
            });
    }

    /// <summary>Replaces all matches through a stateful callback that returns UTF-16 replacement text, observing cancellation before matching begins.</summary>
    /// <typeparam name="TState">The type of additional state passed to the evaluator.</typeparam>
    /// <param name="input">The well-formed UTF-8 input to match against.</param>
    /// <param name="state">Additional state passed to the evaluator.</param>
    /// <param name="evaluator">The callback invoked for each match to produce replacement text.</param>
    /// <param name="cancellationToken">The token to observe before matching begins.</param>
    /// <exception cref="OperationCanceledException">The token was already canceled.</exception>
    public string ReplaceToString<TState>(ReadOnlySpan<byte> input, TState state, Utf16MatchEvaluator<TState> evaluator, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ReplaceToStringEvaluatorCore(input, state, evaluator, cancellationToken);
    }

    /// <summary>Attempts to write a replacement result into a caller-provided UTF-8 destination.</summary>
    /// <returns><see cref="OperationStatus.Done"/> on success, or <see cref="OperationStatus.DestinationTooSmall"/> when the destination cannot hold the complete result.</returns>
    public OperationStatus TryReplace(
        ReadOnlySpan<byte> input,
        string replacement,
        Span<byte> destination,
        out int bytesWritten)
    {
        try
        {
            return TryReplaceStringCore(input, replacement, destination, out bytesWritten);
        }
        catch (Utf8ExecutionDeadlineExpiredException)
        {
            throw CreateMatchTimeoutException(input);
        }
    }

    private OperationStatus TryReplaceStringCore(
        ReadOnlySpan<byte> input,
        string replacement,
        Span<byte> destination,
        out int bytesWritten)
    {
        var analyzed = GetParsedReplacement(replacement);
        if (TryGetAsciiCultureInvariantTwin(input, out var twin))
        {
            return Utf8AsciiCultureInvariantReplacement.TryReplace(twin, input, analyzed, destination, out bytesWritten);
        }

        return TryReplaceCore(input, analyzed, replacement, destination, out bytesWritten);
    }

    /// <summary>Attempts to apply a UTF-8-encoded replacement pattern into a caller-provided UTF-8 destination.</summary>
    /// <returns><see cref="OperationStatus.Done"/> on success, or <see cref="OperationStatus.DestinationTooSmall"/> when the destination cannot hold the complete result.</returns>
    public OperationStatus TryReplace(
        ReadOnlySpan<byte> input,
        ReadOnlySpan<byte> replacementPatternUtf8,
        Span<byte> destination,
        out int bytesWritten)
    {
        try
        {
            return TryReplaceUtf8Core(input, replacementPatternUtf8, destination, out bytesWritten);
        }
        catch (Utf8ExecutionDeadlineExpiredException)
        {
            throw CreateMatchTimeoutException(input);
        }
    }

    private OperationStatus TryReplaceUtf8Core(
        ReadOnlySpan<byte> input,
        ReadOnlySpan<byte> replacementPatternUtf8,
        Span<byte> destination,
        out int bytesWritten)
    {
        _ = Utf8Validation.Validate(replacementPatternUtf8);
        var validation = Utf8Validation.Validate(input);
        if (RequiresCultureSensitiveFallback || ShouldUseKelvinSignFallback(input))
        {
            var replaced = _verifierRuntime.FallbackCandidateVerifier.FallbackRegex.Replace(
                Encoding.UTF8.GetString(input),
                Encoding.UTF8.GetString(replacementPatternUtf8));
            return TryEncodeUtf8ToDestination(replaced, destination, out bytesWritten)
                ? OperationStatus.Done
                : OperationStatus.DestinationTooSmall;
        }

        if (TryGetDirectLiteralReplacementBytes(replacementPatternUtf8, out var directReplacementBytes))
        {
            return TryReplaceLiteralBytesCore(input, validation, directReplacementBytes, destination, out bytesWritten);
        }

        var replacementText = Encoding.UTF8.GetString(replacementPatternUtf8);
        var replacementPattern = GetParsedReplacement(replacementText);
        return TryReplaceCore(input, replacementPattern, replacementText, destination, out bytesWritten);
    }

    /// <summary>Attempts to write a replacement result into a caller-provided UTF-8 destination, observing cancellation before matching begins.</summary>
    /// <param name="input">The well-formed UTF-8 input to match against.</param>
    /// <param name="replacement">The replacement text.</param>
    /// <param name="destination">The destination buffer for the UTF-8 replacement result.</param>
    /// <param name="bytesWritten">When this method returns, the number of bytes written to the destination.</param>
    /// <param name="cancellationToken">The token to observe before matching begins.</param>
    /// <exception cref="OperationCanceledException">The token was already canceled.</exception>
    public OperationStatus TryReplace(ReadOnlySpan<byte> input, string replacement, Span<byte> destination, out int bytesWritten, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return TryReplace(input, replacement, destination, out bytesWritten);
    }

    /// <summary>Attempts to apply a UTF-8-encoded replacement pattern into a caller-provided UTF-8 destination, observing cancellation before matching begins.</summary>
    /// <param name="input">The well-formed UTF-8 input to match against.</param>
    /// <param name="replacementPatternUtf8">The replacement pattern encoded as well-formed UTF-8.</param>
    /// <param name="destination">The destination buffer for the UTF-8 replacement result.</param>
    /// <param name="bytesWritten">When this method returns, the number of bytes written to the destination.</param>
    /// <param name="cancellationToken">The token to observe before matching begins.</param>
    /// <exception cref="OperationCanceledException">The token was already canceled.</exception>
    public OperationStatus TryReplace(ReadOnlySpan<byte> input, ReadOnlySpan<byte> replacementPatternUtf8, Span<byte> destination, out int bytesWritten, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return TryReplace(input, replacementPatternUtf8, destination, out bytesWritten);
    }

    internal Utf8IsMatchDiagnostics CollectIsMatchDiagnostics(ReadOnlySpan<byte> input)
    {
        var session = Utf8SearchDiagnosticsSession.Start();
        try
        {
            var result = IsMatch(input);
            return new Utf8IsMatchDiagnostics(
                result,
                _preparedRegex.ExecutionKind.ToString(),
                _preparedRegex.SearchPlan.Kind.ToString(),
                _preparedRegex.FallbackVerifier.Mode.ToString(),
                _preparedRegex.FallbackVerifier.RequiresCandidateEndCoverage,
                _preparedRegex.FallbackVerifier.RequiresTrailingAnchorCoverage,
                session.SearchCandidates,
                session.FixedCheckRejects,
                session.VerifierInvocations,
                session.VerifierMatches,
                session.PrefilterWindows,
                session.PrefilterSkippedWindows,
                session.PrefilterPromotedWindows,
                session.PrefilterSkippedBytes,
                session.PrefilterPromotedBytes,
                session.EngineDemotions);
        }
        finally
        {
            session.Complete();
        }
    }

    internal Utf8CountDiagnostics CollectCountDiagnostics(ReadOnlySpan<byte> input)
    {
        var session = Utf8SearchDiagnosticsSession.Start();
        try
        {
            var result = Count(input);
            return new Utf8CountDiagnostics(
                result,
                _preparedRegex.ExecutionKind.ToString(),
                _preparedRegex.SearchPlan.Kind.ToString(),
                _preparedRegex.FallbackVerifier.Mode.ToString(),
                _preparedRegex.FallbackVerifier.RequiresCandidateEndCoverage,
                _preparedRegex.FallbackVerifier.RequiresTrailingAnchorCoverage,
                session.ExecutionRoute.Format(),
                session.SearchCandidates,
                session.FixedCheckRejects,
                session.VerifierInvocations,
                session.VerifierMatches,
                session.PrefilterWindows,
                session.PrefilterSkippedWindows,
                session.PrefilterPromotedWindows,
                session.PrefilterSkippedBytes,
                session.PrefilterPromotedBytes,
                session.EngineDemotions);
        }
        finally
        {
            session.Complete();
        }
    }

    private Utf8ExecutionDeadline CreateExecutionBudget()
    {
        return Utf8ExecutionDeadline.Start(MatchTimeout);
    }

    private RegexMatchTimeoutException CreateMatchTimeoutException(ReadOnlySpan<byte> input) =>
        new(Encoding.UTF8.GetString(input), Pattern, MatchTimeout);

    private static bool TryEncodeUtf8ToDestination(string value, Span<byte> destination, out int bytesWritten)
    {
        var requiredLength = Encoding.UTF8.GetByteCount(value);
        if (requiredLength > destination.Length)
        {
            bytesWritten = 0;
            return false;
        }

        bytesWritten = Encoding.UTF8.GetBytes(value, destination);
        return true;
    }

    private bool CanUseNativeSplit(Utf8ValidationResult validation)
    {
        // R03: split segments are match-delimited gaps (BCL EnumerateSplits never splices
        // group captures, unlike array Split), so grouped literal shapes split natively on the
        // same group-agnostic cursors; only the compiled engine kind gates the route.
        return _compiledEngine.Kind switch
        {
            Utf8CompiledEngineKind.ExactLiteral => true,
            Utf8CompiledEngineKind.LiteralFamily => true,
            Utf8CompiledEngineKind.StructuralLinearAutomaton
                => _preparedRegex.StructuralLinearProgram.DeterministicProgram.HasValue && validation.IsAscii,
            _ => false,
        };
    }

    private bool TryGetSmallAsciiLiteralFamilySplitSearch(
        ReadOnlySpan<byte> input,
        out PreparedSmallAsciiLiteralFamilySearch search)
    {
        if (MatchTimeout != Regex.InfiniteMatchTimeout ||
            _preparedRegex.ExecutionPattern.IndexOf('(') >= 0 ||
            _compiledEngineRuntime is not Utf8LiteralFamilyCompiledEngineRuntime literalFamilyRuntime)
        {
            search = default;
            return false;
        }

        return literalFamilyRuntime.Inner.TryGetSmallAsciiLiteralFamilyPrimitive(input, out search);
    }

    private bool UsesRightToLeft()
    {
        return (Options & RegexOptions.RightToLeft) != 0;
    }

    private bool ShouldUseRightToLeftScalarFallback()
    {
        return UsesRightToLeft() &&
            _preparedRegex.ExecutionKind == NativeExecutionKind.FallbackRegex;
    }

    private bool CanUseWellFormedOnlyValidation()
    {
        return _preparedRegex.ExecutionKind is NativeExecutionKind.ExactAsciiLiteral or NativeExecutionKind.ExactUtf8Literal or NativeExecutionKind.ExactUtf8Literals
                or NativeExecutionKind.AsciiLiteralIgnoreCase or NativeExecutionKind.AsciiLiteralIgnoreCaseLiterals
                or NativeExecutionKind.AsciiOrderedLiteralWindow or NativeExecutionKind.AsciiStructuralIdentifierFamily ||
            (_compiledEngine.Kind == Utf8CompiledEngineKind.FallbackRegex &&
             !CanGuideFallbackVerification());
    }

    private bool TryUseAsciiInputValidationShortcut(ReadOnlySpan<byte> input)
    {
        if (_preparedRegex.ExecutionKind is not (NativeExecutionKind.ExactAsciiLiteral or
                                             NativeExecutionKind.ExactUtf8Literal or
                                             NativeExecutionKind.ExactUtf8Literals or
                                             NativeExecutionKind.AsciiLiteralIgnoreCase or
                                             NativeExecutionKind.AsciiLiteralIgnoreCaseLiterals or
                                             NativeExecutionKind.AsciiOrderedLiteralWindow or
                                             NativeExecutionKind.AsciiStructuralIdentifierFamily) &&
            !_preparedRegex.FallbackDirectFamily.SupportsAsciiDefinitiveIsMatch)
        {
            return false;
        }

        return Utf8InputAnalyzer.IsAscii(input);
    }

    private bool TryGetAsciiCultureInvariantTwin(
        ReadOnlySpan<byte> input,
        [NotNullWhen(true)] out Utf8AsciiCultureInvariantStrategy? strategy)
    {
        if (_asciiCultureInvariantStrategy is not null &&
            Utf8InputAnalyzer.IsAscii(input))
        {
            strategy = _asciiCultureInvariantStrategy;
            return true;
        }

        strategy = null;
        return false;
    }

}
