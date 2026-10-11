using System.Buffers;
using Lokad.Utf8Regex.Internal;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Caching;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.FrontEnd;
using Lokad.Utf8Regex.Internal.Input;
using Lokad.Utf8Regex.Internal.Planning;
using Lokad.Utf8Regex.Internal.Replacement;

namespace Lokad.Utf8Regex.Pcre2;

/// <summary>Implements a strictly managed, UTF-8-native subset of PCRE2 semantics.</summary>
/// <remarks>Unsupported constructs or operation combinations fail explicitly; no native PCRE2 binary or interop layer is used.</remarks>
public sealed partial class Utf8Pcre2Regex
{
    private static TimeSpan s_defaultMatchTimeout = Timeout.InfiniteTimeSpan;
    private readonly Pcre2CompiledProgram _program;
    private readonly IPcre2DirectProgram? _unmeteredIsMatchProgram;
    private readonly Pcre2ReplacementComponent _replacementComponent;

    /// <summary>Compiles a PCRE2 pattern with default options, settings, limits, and timeout.</summary>
    public Utf8Pcre2Regex(string pattern)
        : this(pattern, Pcre2CompileOptions.None)
    {
    }

    /// <summary>Compiles a PCRE2 pattern with the specified options and default settings, limits, and timeout.</summary>
    public Utf8Pcre2Regex(string pattern, Pcre2CompileOptions options)
        : this(pattern, options, default(Utf8Pcre2CompileSettings), default(Utf8Pcre2ExecutionLimits), DefaultMatchTimeout)
    {
    }

    /// <summary>Compiles a PCRE2 pattern with explicit options, settings, invocation resource policy, and timeout.</summary>
    /// <remarks>Policy-based construction is a named factory rather than a constructor overload so
    /// that legacy <c>new</c> calls with bare <c>default</c> settings or limits keep binding the
    /// <see cref="Utf8Pcre2ExecutionLimits"/> constructor overload unambiguously.</remarks>
    /// <exception cref="ArgumentOutOfRangeException">A policy budget is zero or exceeds the 32-bit work/depth range.</exception>
    /// <exception cref="NotSupportedException">A result-bytes budget is set.</exception>
    public static Utf8Pcre2Regex Create(
        string pattern,
        Pcre2CompileOptions options,
        Utf8Pcre2CompileSettings compileSettings,
        Utf8ResourcePolicy resourcePolicy,
        TimeSpan matchTimeout)
    {
        return new Utf8Pcre2Regex(pattern, options, compileSettings, ToExecutionLimits(resourcePolicy), matchTimeout);
    }

    /// <summary>Compiles a PCRE2 pattern with explicit options, settings, execution limits, and timeout.</summary>
    public Utf8Pcre2Regex(
        string pattern,
        Pcre2CompileOptions options,
        Utf8Pcre2CompileSettings compileSettings,
        Utf8Pcre2ExecutionLimits defaultExecutionLimits,
        TimeSpan matchTimeout)
        : this(new Utf8ConstructionPattern(pattern), options, compileSettings, defaultExecutionLimits, matchTimeout)
    {
    }

    /// <summary>Shares decoding and compilation admission without retaining borrowed pattern bytes.</summary>
    private Utf8Pcre2Regex(
        Utf8ConstructionPattern patternSource,
        Pcre2CompileOptions options,
        Utf8Pcre2CompileSettings compileSettings,
        Utf8Pcre2ExecutionLimits defaultExecutionLimits,
        TimeSpan matchTimeout)
    {
        using var construction = Utf8ConstructionScope.Enter();
        var pattern = patternSource.Text;
        if (patternSource.IsUtf8)
        {
            construction.Budget.ChargeWork((ulong)patternSource.Utf8.Length);
            _ = Utf8Validation.Validate(patternSource.Utf8);
            pattern = construction.Budget.DecodeUtf8(patternSource.Utf8, Encoding.UTF8);
        }
        var request = new Pcre2CompileRequest(
            pattern,
            options,
            compileSettings,
            defaultExecutionLimits,
            Utf8MatchTimeout.Validate(matchTimeout, nameof(matchTimeout)));
        _program = Pcre2Compiler.Compile(request, CreateFoundationProgram);
        var isMatchProgram = _program.Operations.IsMatch;
        if (Pcre2GlobalOperationDriver.HasUnmeteredExecution(request) &&
            isMatchProgram is Pcre2FiniteLiteralLanguageDirectProgram or
                Pcre2LiteralPrefixRepeatDirectProgram or
                Pcre2PalindromeIsMatchDirectProgram or
                Pcre2LeadingDotStarLiteralIsMatchDirectProgram or
                Pcre2SeparatedRunsIsMatchDirectProgram or
                Pcre2AsciiBoundedIsMatchDirectProgram or
                Pcre2AsciiAnchoredDfaIsMatchDirectProgram or
                Pcre2AsciiRegularIsMatchDirectProgram)
        {
            _unmeteredIsMatchProgram = isMatchProgram;
        }
        _replacementComponent = Pcre2ReplacementComponent.Create();
        construction.Complete();
    }

    /// <summary>Maps a shared resource policy onto PCRE2 execution limits.</summary>
    /// <exception cref="ArgumentOutOfRangeException">A policy budget is zero or exceeds the 32-bit work/depth range.</exception>
    /// <exception cref="NotSupportedException">A result-bytes budget is set. The PCRE2 profile does not yet enforce result-capacity limits.</exception>
    private static Utf8Pcre2ExecutionLimits ToExecutionLimits(Utf8ResourcePolicy resourcePolicy)
    {
        resourcePolicy.Validate(nameof(resourcePolicy));
        if (resourcePolicy.MaxResultBytes.HasValue)
        {
            throw new NotSupportedException("The PCRE2 profile does not yet enforce result-capacity limits.");
        }

        uint workSteps = 0;
        if (resourcePolicy.MaxWorkSteps.HasValue && resourcePolicy.MaxWorkSteps.Value > uint.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(resourcePolicy), "The work-steps budget exceeds the 32-bit PCRE2 range.");
        }

        if (resourcePolicy.MaxWorkSteps.HasValue)
        {
            workSteps = (uint)resourcePolicy.MaxWorkSteps.Value;
        }

        return new Utf8Pcre2ExecutionLimits
        {
            MatchLimit = workSteps,
            DepthLimit = resourcePolicy.MaxDepth ?? 0u,
            HeapLimitInBytes = resourcePolicy.MaxWorkingBytes ?? 0ul,
        };
    }

    private static Pcre2CompiledProgram CreateFoundationProgram(Pcre2CompileRequest request)
    {
        _ = TryCreateUtf8Regex(request.Pattern, request.Options, request.MatchTimeout, out var utf8Regex);

        IPcre2Utf8ProgramSlot primaryUtf8 = utf8Regex is null
            ? Pcre2EmptyUtf8ProgramSlot.Instance
            : new Pcre2Utf8ProgramSlot(utf8Regex);
        IPcre2DirectProgram direct = primaryUtf8 is Pcre2Utf8ProgramSlot utf8Slot
            ? new Pcre2Utf8DirectProgram(utf8Slot.Regex)
            : Pcre2NoDirectProgram.Instance;
        var operations = new Pcre2OperationPrograms(direct, direct, direct, direct, direct);
        var groupNames = utf8Regex?.GetGroupNames() ?? ["0"];
        var nameEntries = utf8Regex is not null
            ? GetUtf8RegexNameEntries(utf8Regex)
            : [];
        return new Pcre2CompiledProgram(
            request,
            primaryUtf8,
            operations,
            default,
            Pcre2PartialProbeProgram.None,
            Pcre2LegacySyntaxTree.Instance,
            groupNames,
            nameEntries);
    }

    /// <summary>Compiles a well-formed UTF-8 PCRE2 pattern with default options, settings, limits, and timeout.</summary>
    public Utf8Pcre2Regex(ReadOnlySpan<byte> patternUtf8)
        : this(patternUtf8, Pcre2CompileOptions.None)
    {
    }

    /// <summary>Compiles a well-formed UTF-8 PCRE2 pattern with the specified options and default settings, limits, and timeout.</summary>
    public Utf8Pcre2Regex(ReadOnlySpan<byte> patternUtf8, Pcre2CompileOptions options)
        : this(patternUtf8, options, default(Utf8Pcre2CompileSettings), default(Utf8Pcre2ExecutionLimits), DefaultMatchTimeout)
    {
    }

    /// <summary>Compiles a well-formed UTF-8 PCRE2 pattern with explicit options, settings, invocation resource policy, and timeout.</summary>
    /// <remarks>The policy is validated before the UTF-8 pattern bytes are decoded. Policy-based
    /// construction is a named factory rather than a constructor overload; see
    /// <see cref="Create(string, Pcre2CompileOptions, Utf8Pcre2CompileSettings, Utf8ResourcePolicy, TimeSpan)"/>.</remarks>
    /// <exception cref="ArgumentOutOfRangeException">A policy budget is zero or exceeds the 32-bit work/depth range.</exception>
    /// <exception cref="NotSupportedException">A result-bytes budget is set.</exception>
    /// <exception cref="ArgumentException">The pattern bytes are not valid UTF-8.</exception>
    public static Utf8Pcre2Regex Create(
        ReadOnlySpan<byte> patternUtf8,
        Pcre2CompileOptions options,
        Utf8Pcre2CompileSettings compileSettings,
        Utf8ResourcePolicy resourcePolicy,
        TimeSpan matchTimeout)
    {
        var defaultExecutionLimits = ToExecutionLimits(resourcePolicy);
        return new Utf8Pcre2Regex(new Utf8ConstructionPattern(patternUtf8), options, compileSettings, defaultExecutionLimits, matchTimeout);
    }

    /// <summary>Compiles a well-formed UTF-8 PCRE2 pattern with explicit options, settings, execution limits, and timeout.</summary>
    public Utf8Pcre2Regex(
        ReadOnlySpan<byte> patternUtf8,
        Pcre2CompileOptions options,
        Utf8Pcre2CompileSettings compileSettings,
        Utf8Pcre2ExecutionLimits defaultExecutionLimits,
        TimeSpan matchTimeout)
        : this(new Utf8ConstructionPattern(patternUtf8), options, compileSettings, defaultExecutionLimits, matchTimeout)
    {
    }

    /// <summary>Gets or sets the timeout used by constructors and static operations that do not specify one.</summary>
    /// <remarks>The initial value is an infinite timeout.</remarks>
    public static TimeSpan DefaultMatchTimeout
    {
        get => s_defaultMatchTimeout;
        set => s_defaultMatchTimeout = Utf8MatchTimeout.Validate(value, nameof(value));
    }

    /// <summary>Gets the compiled PCRE2 pattern.</summary>
    public string Pattern => _program.Request.Pattern;

    /// <summary>Gets the compile-option flags.</summary>
    public Pcre2CompileOptions Options => _program.Request.Options;

    /// <summary>Gets the compile-time newline, group-name, and byte-oriented settings.</summary>
    public Utf8Pcre2CompileSettings CompileSettings => _program.Request.Settings;

    /// <summary>Gets the default execution budgets applied to matching operations.</summary>
    public Utf8Pcre2ExecutionLimits DefaultExecutionLimits => _program.Request.DefaultLimits;

    /// <summary>Gets the maximum duration of an individual matching operation.</summary>
    public TimeSpan MatchTimeout => _program.Request.MatchTimeout;

    private bool HasPrimaryUtf8Regex => _program.PrimaryUtf8.IsPresent;

    private Utf8Regex PrimaryUtf8Regex => ((Pcre2Utf8ProgramSlot)_program.PrimaryUtf8).Regex;

    private bool UsesUtf8Translation => HasPrimaryUtf8Regex;

    private string[] GroupNames => _program.GroupNames;

    private Pcre2NameEntry[] NameEntries => _program.NameEntries;

    /// <summary>Determines whether the well-formed UTF-8 subject contains a match.</summary>
    public bool IsMatch(ReadOnlySpan<byte> input)
    {
        if (_unmeteredIsMatchProgram is Pcre2AsciiRegularIsMatchDirectProgram asciiRegular)
        {
            if (!asciiRegular.SupportsPreparedOffsets ||
                asciiRegular.Regex.Inspection.ExecutionKind == NativeExecutionKind.FallbackRegex)
            {
                return asciiRegular.Regex.IsMatch(input);
            }

            var subject = Utf8ValidatedInput.Create(input);
            if (asciiRegular.Regex.ByteOffsetExecution.TryIsMatchPrepared(
                    subject,
                    new Utf8BytePosition(0),
                    out var result))
            {
                return result;
            }

            return IsMatch(input, 0, Pcre2MatchOptions.None);
        }

        if (_unmeteredIsMatchProgram is not null)
        {
            _ = Utf8ValidatedInput.Create(input);
            switch (_unmeteredIsMatchProgram.Kind)
            {
                case Pcre2DirectProgramKind.Pcre2LiteralFamily:
                    return ((Pcre2FiniteLiteralLanguageDirectProgram)_unmeteredIsMatchProgram)
                        .BooleanSearch.IsMatch(input, 0);
                case Pcre2DirectProgramKind.Pcre2LiteralPrefixRepeat:
                    return Pcre2LiteralPrefixRepeatRunner.IsMatch(
                        (Pcre2LiteralPrefixRepeatDirectProgram)_unmeteredIsMatchProgram,
                        input,
                        0);
                case Pcre2DirectProgramKind.Pcre2PalindromeIsMatch:
                    return Pcre2PalindromeIsMatchRunner.IsMatch(
                        (Pcre2PalindromeIsMatchDirectProgram)_unmeteredIsMatchProgram,
                        input);
                case Pcre2DirectProgramKind.Pcre2LeadingDotStarLiteralIsMatch:
                    return Pcre2LeadingDotStarLiteralIsMatchRunner.IsMatch(
                        (Pcre2LeadingDotStarLiteralIsMatchDirectProgram)_unmeteredIsMatchProgram,
                        input,
                        0);
                case Pcre2DirectProgramKind.Pcre2SeparatedRunsIsMatch:
                    return Pcre2SeparatedRunsIsMatchRunner.IsMatch(
                        (Pcre2SeparatedRunsIsMatchDirectProgram)_unmeteredIsMatchProgram,
                        input,
                        0);
                case Pcre2DirectProgramKind.Pcre2AsciiBoundedIsMatch:
                    return Pcre2AsciiBoundedIsMatchRunner.IsMatch(
                        (Pcre2AsciiBoundedIsMatchDirectProgram)_unmeteredIsMatchProgram,
                        input,
                        0);
                case Pcre2DirectProgramKind.Pcre2AsciiAnchoredDfaIsMatch:
                    return Pcre2AsciiAnchoredDfaIsMatchRunner.IsMatch(
                        (Pcre2AsciiAnchoredDfaIsMatchDirectProgram)_unmeteredIsMatchProgram,
                        input,
                        0);
            }
        }

        return IsMatch(input, 0, Pcre2MatchOptions.None);
    }

    /// <summary>Determines whether a match exists at or after a scalar-aligned UTF-8 byte offset.</summary>
    public bool IsMatch(ReadOnlySpan<byte> input, int startOffsetInBytes)
        => IsMatch(input, startOffsetInBytes, Pcre2MatchOptions.None);

    /// <summary>Determines whether a match exists using an explicit start byte offset and per-operation options.</summary>
    public bool IsMatch(ReadOnlySpan<byte> input, int startOffsetInBytes, Pcre2MatchOptions matchOptions)
    {
        return IsMatchCore(input, startOffsetInBytes, matchOptions, CancellationToken.None);
    }

    private bool IsMatchCore(ReadOnlySpan<byte> input, int startOffsetInBytes, Pcre2MatchOptions matchOptions, CancellationToken cancellationToken)
    {
        var subject = ValidateSubjectAndStart(input, startOffsetInBytes, out var start);
        if (Pcre2Runner.TryIsMatch(_program, ref subject, start, matchOptions, cancellationToken, out var result))
        {
            return result;
        }

        throw CreateUnsupportedPatternException();
    }

    /// <summary>Determines whether a match exists using an explicit start byte offset and per-operation options, observing cancellation before matching begins.</summary>
    /// <param name="input">The well-formed UTF-8 subject to match against.</param>
    /// <param name="startOffsetInBytes">The zero-based UTF-8 byte offset where matching starts.</param>
    /// <param name="matchOptions">The per-operation match options.</param>
    /// <param name="cancellationToken">The token to observe before matching begins.</param>
    /// <exception cref="OperationCanceledException">The token was already canceled.</exception>
    public bool IsMatch(ReadOnlySpan<byte> input, int startOffsetInBytes, Pcre2MatchOptions matchOptions, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return IsMatchCore(input, startOffsetInBytes, matchOptions, cancellationToken);
    }

    /// <summary>Counts non-overlapping matches in the well-formed UTF-8 subject.</summary>
    public int Count(ReadOnlySpan<byte> input)
        => Count(input, 0, Pcre2MatchOptions.None);

    /// <summary>Counts non-overlapping matches at or after a scalar-aligned UTF-8 byte offset.</summary>
    public int Count(ReadOnlySpan<byte> input, int startOffsetInBytes)
        => Count(input, startOffsetInBytes, Pcre2MatchOptions.None);

    /// <summary>Counts non-overlapping matches using an explicit start byte offset and per-operation options.</summary>
    public int Count(ReadOnlySpan<byte> input, int startOffsetInBytes, Pcre2MatchOptions matchOptions)
    {
        return CountCore(input, startOffsetInBytes, matchOptions, CancellationToken.None);
    }

    private int CountCore(ReadOnlySpan<byte> input, int startOffsetInBytes, Pcre2MatchOptions matchOptions, CancellationToken cancellationToken)
    {
        ThrowIfGenericIterationMayBeNonMonotone();
        var subject = ValidateSubjectAndStart(input, startOffsetInBytes, out var start);
        if (Pcre2GlobalOperationDriver.TryCount(_program, ref subject, start, matchOptions, cancellationToken, out var result))
        {
            return result;
        }

        throw CreateUnsupportedPatternException();
    }

    /// <summary>Counts non-overlapping matches using an explicit start byte offset and per-operation options, observing cancellation before matching begins.</summary>
    /// <param name="input">The well-formed UTF-8 subject to match against.</param>
    /// <param name="startOffsetInBytes">The zero-based UTF-8 byte offset where matching starts.</param>
    /// <param name="matchOptions">The per-operation match options.</param>
    /// <param name="cancellationToken">The token to observe before matching begins.</param>
    /// <exception cref="OperationCanceledException">The token was already canceled.</exception>
    public int Count(ReadOnlySpan<byte> input, int startOffsetInBytes, Pcre2MatchOptions matchOptions, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return CountCore(input, startOffsetInBytes, matchOptions, cancellationToken);
    }

    /// <summary>Finds the first match in the well-formed UTF-8 subject.</summary>
    public Utf8Pcre2ValueMatch Match(ReadOnlySpan<byte> input)
        => Match(input, 0, Pcre2MatchOptions.None);

    /// <summary>Finds the first match at or after a scalar-aligned UTF-8 byte offset.</summary>
    public Utf8Pcre2ValueMatch Match(ReadOnlySpan<byte> input, int startOffsetInBytes)
        => Match(input, startOffsetInBytes, Pcre2MatchOptions.None);

    /// <summary>Finds the first match using an explicit start byte offset and per-operation options.</summary>
    public Utf8Pcre2ValueMatch Match(ReadOnlySpan<byte> input, int startOffsetInBytes, Pcre2MatchOptions matchOptions)
    {
        return MatchCore(input, startOffsetInBytes, matchOptions, CancellationToken.None);
    }

    private Utf8Pcre2ValueMatch MatchCore(ReadOnlySpan<byte> input, int startOffsetInBytes, Pcre2MatchOptions matchOptions, CancellationToken cancellationToken)
    {
        var subject = ValidateSubjectAndStart(input, startOffsetInBytes, out var start);
        if (Pcre2Runner.TryMatch(_program, ref subject, start, matchOptions, cancellationToken, out var directMatch))
        {
            return Utf8Pcre2ValueMatch.Create(input, directMatch);
        }

        if (_program.Operations.Match.Kind == Pcre2DirectProgramKind.Utf8Regex)
        {
            return CreateManagedProfileValueMatch(
                input,
                PrimaryUtf8Regex.ByteOffsetExecution.Match(subject, start));
        }

        throw CreateUnsupportedPatternException();
    }

    /// <summary>Finds the first match using an explicit start byte offset and per-operation options, observing cancellation before matching begins.</summary>
    /// <param name="input">The well-formed UTF-8 subject to match against.</param>
    /// <param name="startOffsetInBytes">The zero-based UTF-8 byte offset where matching starts.</param>
    /// <param name="matchOptions">The per-operation match options.</param>
    /// <param name="cancellationToken">The token to observe before matching begins.</param>
    /// <exception cref="OperationCanceledException">The token was already canceled.</exception>
    public Utf8Pcre2ValueMatch Match(ReadOnlySpan<byte> input, int startOffsetInBytes, Pcre2MatchOptions matchOptions, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return MatchCore(input, startOffsetInBytes, matchOptions, cancellationToken);
    }

    /// <summary>Finds the first match with capture slots, duplicate-name metadata, and <c>(*MARK)</c> state.</summary>
    public Utf8Pcre2MatchContext MatchDetailed(ReadOnlySpan<byte> input)
        => MatchDetailed(input, 0, Pcre2MatchOptions.None);

    /// <summary>Finds the first detailed match at or after a scalar-aligned UTF-8 byte offset.</summary>
    public Utf8Pcre2MatchContext MatchDetailed(ReadOnlySpan<byte> input, int startOffsetInBytes)
        => MatchDetailed(input, startOffsetInBytes, Pcre2MatchOptions.None);

    /// <summary>Finds the first detailed match using an explicit start byte offset and per-operation options.</summary>
    public Utf8Pcre2MatchContext MatchDetailed(ReadOnlySpan<byte> input, int startOffsetInBytes, Pcre2MatchOptions matchOptions)
    {
        return MatchDetailedCore(input, startOffsetInBytes, matchOptions, CancellationToken.None);
    }

    private Utf8Pcre2MatchContext MatchDetailedCore(ReadOnlySpan<byte> input, int startOffsetInBytes, Pcre2MatchOptions matchOptions, CancellationToken cancellationToken)
    {
        var subject = ValidateSubjectAndStart(input, startOffsetInBytes, out var start);
        if (Pcre2Runner.TryMatchDetailed(
                _program,
                ref subject,
                start,
                matchOptions,
                cancellationToken,
                out var directGroups,
                out var directMark))
        {
            return directGroups.Length != 0 || directMark is not null
                ? Utf8Pcre2MatchContext.Create(input, directGroups, NameEntries, directMark)
                : default;
        }

        if (Pcre2Runner.TryMatch(_program, ref subject, start, matchOptions, CancellationToken.None, out var directMatch))
        {
            return directMatch.Success
                ? Utf8Pcre2MatchContext.Create(input, [directMatch], NameEntries)
                : default;
        }

        if (_program.Operations.Match.Kind == Pcre2DirectProgramKind.Utf8Regex)
        {
            return MatchDetailedUsingUtf8Backend(subject, start);
        }

        throw CreateUnsupportedPatternException();
    }

    /// <summary>Finds the first detailed match using an explicit start byte offset and per-operation options, observing cancellation before matching begins.</summary>
    /// <param name="input">The well-formed UTF-8 subject to match against.</param>
    /// <param name="startOffsetInBytes">The zero-based UTF-8 byte offset where matching starts.</param>
    /// <param name="matchOptions">The per-operation match options.</param>
    /// <param name="cancellationToken">The token to observe before matching begins.</param>
    /// <exception cref="OperationCanceledException">The token was already canceled.</exception>
    public Utf8Pcre2MatchContext MatchDetailed(ReadOnlySpan<byte> input, int startOffsetInBytes, Pcre2MatchOptions matchOptions, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return MatchDetailedCore(input, startOffsetInBytes, matchOptions, cancellationToken);
    }

    /// <summary>Creates a cursor over non-overlapping matches in the UTF-8 subject.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Utf8Pcre2ValueMatchEnumerator EnumerateMatches(ReadOnlySpan<byte> input)
        => EnumerateMatches(input, 0, Pcre2MatchOptions.None);

    /// <summary>Creates a match cursor starting at a scalar-aligned UTF-8 byte offset.</summary>
    public Utf8Pcre2ValueMatchEnumerator EnumerateMatches(ReadOnlySpan<byte> input, int startOffsetInBytes)
        => EnumerateMatches(input, startOffsetInBytes, Pcre2MatchOptions.None);

    /// <summary>Creates a match cursor using an explicit start byte offset and per-operation options.</summary>
    // Keep the large discriminated-union cursor router out of hot consumer loops. Tiered PGO
    // otherwise expands every backend construction branch and its bulk struct copies at the call site.
    [MethodImpl(MethodImplOptions.NoInlining)]
    public Utf8Pcre2ValueMatchEnumerator EnumerateMatches(ReadOnlySpan<byte> input, int startOffsetInBytes, Pcre2MatchOptions matchOptions)
    {
        return EnumerateMatchesCore(input, startOffsetInBytes, matchOptions, CancellationToken.None);
    }

    private Utf8Pcre2ValueMatchEnumerator EnumerateMatchesCore(ReadOnlySpan<byte> input, int startOffsetInBytes, Pcre2MatchOptions matchOptions, CancellationToken cancellationToken)
    {
        ThrowIfGenericIterationMayBeNonMonotone();
        var subject = ValidateSubjectAndStart(input, startOffsetInBytes, out var start);

        if (Pcre2GlobalOperationDriver.TryCreateCursor(
                _program,
                subject,
                start,
                matchOptions,
                out var directCursor))
        {
            return new Utf8Pcre2ValueMatchEnumerator(input, directCursor);
        }

        if (_program.Operations.Enumerate.Kind != Pcre2DirectProgramKind.None)
        {
            return EnumerateMatchesViaConfiguredBackend(subject, start, matchOptions, cancellationToken);
        }

        throw CreateUnsupportedPatternException();
    }

    private static NotSupportedException CreateUnsupportedPatternException() =>
        new("SPEC-PCRE2 does not support this pattern in the managed profile.");

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private Utf8Pcre2ValueMatchEnumerator EnumerateMatchesViaConfiguredBackend(
        Utf8ValidatedInput input,
        Utf8BytePosition start,
        Pcre2MatchOptions matchOptions,
        CancellationToken cancellationToken)
    {
        if (matchOptions == Pcre2MatchOptions.None &&
            _program.Operations.Enumerate is Pcre2SliceDirectProgram slice)
        {
            return new Utf8Pcre2ValueMatchEnumerator(
                input.Bytes,
                new Pcre2SliceGlobalMatchCursor(
                    slice.SliceProgram,
                    input,
                    start,
                    _program.Request,
                    cancellationToken));
        }

        if (_program.Operations.Enumerate.Kind == Pcre2DirectProgramKind.Pcre2Slice)
        {
            var fallback = (_program.Operations.Enumerate as Pcre2SliceDirectProgram)?.Fallback
                ?? throw new InvalidOperationException(
                    "The PCRE2 slice enumerator has no execution program.");
            var fallbackCursor = Pcre2GlobalMatchCursor.CreateBacktracking(
                fallback,
                _program.CandidateSearchPlan,
                input,
                start,
                matchOptions,
                _program.Request,
                collectDiagnostics: false);
            return new Utf8Pcre2ValueMatchEnumerator(input.Bytes, fallbackCursor);
        }

        return _program.Operations.Enumerate.Kind switch
        {
            Pcre2DirectProgramKind.Utf8Regex => EnumerateMatchesViaUtf8RegexBackend(input, start),
            _ => default,
        };
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private Utf8Pcre2ValueMatchEnumerator EnumerateMatchesViaUtf8RegexBackend(
        Utf8ValidatedInput input,
        Utf8BytePosition start)
    {
        if (!UsesUtf8Translation && CanUsePreparedAsciiUtf8RegexEnumerator(input.IsAscii))
        {
            return new Utf8Pcre2ValueMatchEnumerator(
                input.Bytes,
                PrimaryUtf8Regex.ByteOffsetExecution.EnumeratePreparedMatches(input, start),
                start.Value);
        }

        return new Utf8Pcre2ValueMatchEnumerator(
            input.Bytes,
            PrimaryUtf8Regex.ByteOffsetExecution.EnumerateMatches(input, start));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool CanUsePreparedAsciiUtf8RegexEnumerator(ReadOnlySpan<byte> input)
        => CanUsePreparedAsciiUtf8RegexEnumerator(Utf8InputAnalyzer.ValidateOnly(input).IsAscii);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool CanUsePreparedAsciiUtf8RegexEnumerator(bool isAscii)
    {
        if (!HasPrimaryUtf8Regex ||
            !isAscii)
        {
            return false;
        }

        return PrimaryUtf8Regex.ByteOffsetExecution.SearchPortfolioKind is
            Internal.Planning.Utf8SearchPortfolioKind.ExactLiteral or
            Internal.Planning.Utf8SearchPortfolioKind.IgnoreCaseLiteral;
    }

    /// <summary>Creates a match cursor using an explicit start byte offset and per-operation options, observing cancellation when enumeration begins.</summary>
    /// <param name="input">The well-formed UTF-8 subject to match against.</param>
    /// <param name="startOffsetInBytes">The zero-based UTF-8 byte offset where matching starts.</param>
    /// <param name="matchOptions">The per-operation match options.</param>
    /// <param name="cancellationToken">The token to observe before matching begins.</param>
    /// <exception cref="OperationCanceledException">The token was already canceled.</exception>
    public Utf8Pcre2ValueMatchEnumerator EnumerateMatches(ReadOnlySpan<byte> input, int startOffsetInBytes, Pcre2MatchOptions matchOptions, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return EnumerateMatchesCore(input, startOffsetInBytes, matchOptions, cancellationToken);
    }

    /// <summary>Probes the UTF-8 subject for a full or permitted subject-end partial match.</summary>
    public Utf8Pcre2ProbeResult Probe(ReadOnlySpan<byte> input, Pcre2PartialMode partialMode)
        => Probe(input, partialMode, 0, Pcre2MatchOptions.None);

    /// <summary>Probes for a full or partial match at or after a scalar-aligned UTF-8 byte offset.</summary>
    public Utf8Pcre2ProbeResult Probe(ReadOnlySpan<byte> input, Pcre2PartialMode partialMode, int startOffsetInBytes)
        => Probe(input, partialMode, startOffsetInBytes, Pcre2MatchOptions.None);

    /// <summary>Probes for a full or partial match using an explicit start byte offset and per-operation options.</summary>
    public Utf8Pcre2ProbeResult Probe(ReadOnlySpan<byte> input, Pcre2PartialMode partialMode, int startOffsetInBytes, Pcre2MatchOptions matchOptions)
    {
        var subject = ValidateSubjectAndStart(input, startOffsetInBytes, out var start);
        if (_program.Operations.Match is Pcre2LiteralDirectProgram literalProgram)
        {
            return Pcre2LiteralProbeRunner.Probe(
                input,
                literalProgram.Program,
                subject,
                start,
                partialMode,
                matchOptions,
                _program.Request);
        }

        if (_program.PartialProbe.Kind != Pcre2PartialProbeProgramKind.None)
        {
            return Pcre2PartialProbeRunner.Probe(input, partialMode, startOffsetInBytes, _program.PartialProbe);
        }

        if (partialMode == Pcre2PartialMode.None)
        {
            return ProbeViaNonPartialMatch(input, startOffsetInBytes);
        }

        if (_program.Operations.Match 
            is Pcre2BacktrackingDirectProgram or Pcre2SliceDirectProgram &&
            Pcre2Runner.TryMatchDetailed(
                _program,
                ref subject,
                start,
                matchOptions,
                CancellationToken.None,
                out var directGroups,
                out var directMark))
        {
            if (directGroups.Length != 0)
            {
                return Utf8Pcre2ProbeResult.CreateFullMatch(input, directGroups, NameEntries, directMark);
            }

            if (directMark is not null)
            {
                return Utf8Pcre2ProbeResult.CreateNoMatch(input, directMark);
            }
        }

        throw CreateUnsupportedProbeException();
    }

    /// <summary>Probes for a full or partial match using an explicit start byte offset and per-operation options, observing cancellation before matching begins.</summary>
    /// <param name="input">The well-formed UTF-8 subject to match against.</param>
    /// <param name="partialMode">The permitted subject-end partial-match mode.</param>
    /// <param name="startOffsetInBytes">The zero-based UTF-8 byte offset where matching starts.</param>
    /// <param name="matchOptions">The per-operation match options.</param>
    /// <param name="cancellationToken">The token to observe before matching begins.</param>
    /// <exception cref="OperationCanceledException">The token was already canceled.</exception>
    public Utf8Pcre2ProbeResult Probe(ReadOnlySpan<byte> input, Pcre2PartialMode partialMode, int startOffsetInBytes, Pcre2MatchOptions matchOptions, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Probe(input, partialMode, startOffsetInBytes, matchOptions);
    }

    /// <summary>Replaces matches using a PCRE2 replacement template and returns owned UTF-8 bytes.</summary>
    public byte[] Replace(ReadOnlySpan<byte> input, string replacement)
        => Replace(input, replacement, 0, Pcre2SubstitutionOptions.None, Pcre2MatchOptions.None);

    /// <summary>Replaces matches at or after a scalar-aligned UTF-8 byte offset.</summary>
    public byte[] Replace(ReadOnlySpan<byte> input, string replacement, int startOffsetInBytes)
        => Replace(input, replacement, startOffsetInBytes, Pcre2SubstitutionOptions.None, Pcre2MatchOptions.None);

    /// <summary>Replaces matches using an explicit start byte offset and substitution options.</summary>
    public byte[] Replace(ReadOnlySpan<byte> input, string replacement, int startOffsetInBytes, Pcre2SubstitutionOptions substitutionOptions)
        => Replace(input, replacement, startOffsetInBytes, substitutionOptions, Pcre2MatchOptions.None);

    /// <summary>Replaces matches using explicit start, substitution, and per-match options.</summary>
    public byte[] Replace(ReadOnlySpan<byte> input, string replacement, int startOffsetInBytes, Pcre2SubstitutionOptions substitutionOptions, Pcre2MatchOptions matchOptions)
    {
        ValidateStartOffset(input, startOffsetInBytes);
        return ReplaceCore(input, replacement, substitutionOptions, startOffsetInBytes, matchOptions, CancellationToken.None);
    }

    /// <summary>Replaces matches using explicit start, substitution, and per-match options, observing cancellation before matching begins.</summary>
    /// <param name="input">The well-formed UTF-8 subject to match against.</param>
    /// <param name="replacement">The PCRE2 replacement template.</param>
    /// <param name="startOffsetInBytes">The zero-based UTF-8 byte offset where matching starts.</param>
    /// <param name="substitutionOptions">The substitution options.</param>
    /// <param name="matchOptions">The per-operation match options.</param>
    /// <param name="cancellationToken">The token to observe before matching begins.</param>
    /// <exception cref="OperationCanceledException">The token was already canceled.</exception>
    public byte[] Replace(ReadOnlySpan<byte> input, string replacement, int startOffsetInBytes, Pcre2SubstitutionOptions substitutionOptions, Pcre2MatchOptions matchOptions, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ValidateStartOffset(input, startOffsetInBytes);
        return ReplaceCore(input, replacement, substitutionOptions, startOffsetInBytes, matchOptions, cancellationToken);
    }

    /// <summary>Replaces matches using a well-formed UTF-8 PCRE2 replacement template.</summary>
    public byte[] Replace(ReadOnlySpan<byte> input, ReadOnlySpan<byte> replacementPatternUtf8)
        => Replace(input, replacementPatternUtf8, 0, Pcre2SubstitutionOptions.None, Pcre2MatchOptions.None);

    /// <summary>Replaces matches using a UTF-8 template at or after a scalar-aligned byte offset.</summary>
    public byte[] Replace(ReadOnlySpan<byte> input, ReadOnlySpan<byte> replacementPatternUtf8, int startOffsetInBytes)
        => Replace(input, replacementPatternUtf8, startOffsetInBytes, Pcre2SubstitutionOptions.None, Pcre2MatchOptions.None);

    /// <summary>Replaces matches using a UTF-8 template with explicit start and substitution options.</summary>
    public byte[] Replace(ReadOnlySpan<byte> input, ReadOnlySpan<byte> replacementPatternUtf8, int startOffsetInBytes, Pcre2SubstitutionOptions substitutionOptions)
        => Replace(input, replacementPatternUtf8, startOffsetInBytes, substitutionOptions, Pcre2MatchOptions.None);

    /// <summary>Replaces matches using a UTF-8 template with explicit start, substitution, and per-match options.</summary>
    public byte[] Replace(ReadOnlySpan<byte> input, ReadOnlySpan<byte> replacementPatternUtf8, int startOffsetInBytes, Pcre2SubstitutionOptions substitutionOptions, Pcre2MatchOptions matchOptions)
    {
        _ = Utf8Validation.Validate(replacementPatternUtf8);
        ValidateStartOffset(input, startOffsetInBytes);
        return ReplaceCore(input, Encoding.UTF8.GetString(replacementPatternUtf8), substitutionOptions, startOffsetInBytes, matchOptions, CancellationToken.None);
    }

    /// <summary>Replaces matches using a UTF-8 template with explicit start, substitution, and per-match options, observing cancellation before matching begins.</summary>
    /// <param name="input">The well-formed UTF-8 subject to match against.</param>
    /// <param name="replacementPatternUtf8">The PCRE2 replacement template encoded as well-formed UTF-8.</param>
    /// <param name="startOffsetInBytes">The zero-based UTF-8 byte offset where matching starts.</param>
    /// <param name="substitutionOptions">The substitution options.</param>
    /// <param name="matchOptions">The per-operation match options.</param>
    /// <param name="cancellationToken">The token to observe before matching begins.</param>
    /// <exception cref="OperationCanceledException">The token was already canceled.</exception>
    public byte[] Replace(ReadOnlySpan<byte> input, ReadOnlySpan<byte> replacementPatternUtf8, int startOffsetInBytes, Pcre2SubstitutionOptions substitutionOptions, Pcre2MatchOptions matchOptions, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _ = Utf8Validation.Validate(replacementPatternUtf8);
        ValidateStartOffset(input, startOffsetInBytes);
        return ReplaceCore(input, Encoding.UTF8.GetString(replacementPatternUtf8), substitutionOptions, startOffsetInBytes, matchOptions, cancellationToken);
    }

    /// <summary>Replaces matches through a stateful callback that writes UTF-8 replacement content.</summary>
    public byte[] Replace<TState>(ReadOnlySpan<byte> input, TState state, Pcre2MatchEvaluator<TState> evaluator)
        => Replace(input, state, evaluator, 0, Pcre2MatchOptions.None);

    /// <summary>Replaces matches through a UTF-8 callback using explicit start and per-match options.</summary>
    public byte[] Replace<TState>(ReadOnlySpan<byte> input, TState state, Pcre2MatchEvaluator<TState> evaluator, int startOffsetInBytes, Pcre2MatchOptions matchOptions)
        => ReplaceCallback(input, state, new Pcre2ByteCallback<TState>(evaluator), startOffsetInBytes, matchOptions,
            CancellationToken.None, false).Bytes;

    /// <summary>Replaces matches through a UTF-8 callback using explicit start and per-match options, observing cancellation before matching begins.</summary>
    /// <param name="input">The well-formed UTF-8 subject to match against.</param>
    /// <param name="state">Additional state passed to the evaluator.</param>
    /// <param name="evaluator">The callback invoked for each match.</param>
    /// <param name="startOffsetInBytes">The zero-based UTF-8 byte offset where matching starts.</param>
    /// <param name="matchOptions">The per-operation match options.</param>
    /// <typeparam name="TState">The type of additional state passed to the evaluator.</typeparam>
    /// <param name="cancellationToken">The token to observe before matching begins.</param>
    /// <exception cref="OperationCanceledException">The token was already canceled.</exception>
    public byte[] Replace<TState>(ReadOnlySpan<byte> input, TState state, Pcre2MatchEvaluator<TState> evaluator, int startOffsetInBytes, Pcre2MatchOptions matchOptions, CancellationToken cancellationToken)
        => ReplaceCallback(input, state, new Pcre2ByteCallback<TState>(evaluator), startOffsetInBytes, matchOptions,
            cancellationToken, false).Bytes;

    /// <summary>Replaces matches using a PCRE2 replacement template and returns UTF-16 text.</summary>
    public string ReplaceToString(ReadOnlySpan<byte> input, string replacement)
        => ReplaceToString(input, replacement, 0, Pcre2SubstitutionOptions.None, Pcre2MatchOptions.None);

    /// <summary>Replaces matches to UTF-16 text at or after a scalar-aligned UTF-8 byte offset.</summary>
    public string ReplaceToString(ReadOnlySpan<byte> input, string replacement, int startOffsetInBytes)
        => ReplaceToString(input, replacement, startOffsetInBytes, Pcre2SubstitutionOptions.None, Pcre2MatchOptions.None);

    /// <summary>Replaces matches to UTF-16 text using explicit substitution options.</summary>
    public string ReplaceToString(ReadOnlySpan<byte> input, string replacement, Pcre2SubstitutionOptions substitutionOptions)
        => ReplaceToString(input, replacement, 0, substitutionOptions, Pcre2MatchOptions.None);

    /// <summary>Replaces matches to UTF-16 text using explicit start and substitution options.</summary>
    public string ReplaceToString(ReadOnlySpan<byte> input, string replacement, int startOffsetInBytes, Pcre2SubstitutionOptions substitutionOptions)
        => ReplaceToString(input, replacement, startOffsetInBytes, substitutionOptions, Pcre2MatchOptions.None);

    /// <summary>Replaces matches to UTF-16 text using explicit start, substitution, and per-match options.</summary>
    public string ReplaceToString(ReadOnlySpan<byte> input, string replacement, int startOffsetInBytes, Pcre2SubstitutionOptions substitutionOptions, Pcre2MatchOptions matchOptions)
    {
        var result = ReplaceCore(input, replacement, substitutionOptions, startOffsetInBytes, matchOptions, CancellationToken.None);
        _ = Utf8Validation.Validate(result);
        return Encoding.UTF8.GetString(result);
    }

    /// <summary>Replaces matches to UTF-16 text using explicit start, substitution, and per-match options, observing cancellation before matching begins.</summary>
    /// <param name="input">The well-formed UTF-8 subject to match against.</param>
    /// <param name="replacement">The PCRE2 replacement template.</param>
    /// <param name="startOffsetInBytes">The zero-based UTF-8 byte offset where matching starts.</param>
    /// <param name="substitutionOptions">The substitution options.</param>
    /// <param name="matchOptions">The per-operation match options.</param>
    /// <param name="cancellationToken">The token to observe before matching begins.</param>
    /// <exception cref="OperationCanceledException">The token was already canceled.</exception>
    public string ReplaceToString(ReadOnlySpan<byte> input, string replacement, int startOffsetInBytes, Pcre2SubstitutionOptions substitutionOptions, Pcre2MatchOptions matchOptions, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var tokenResult = ReplaceCore(input, replacement, substitutionOptions, startOffsetInBytes, matchOptions, cancellationToken);
        _ = Utf8Validation.Validate(tokenResult);
        return Encoding.UTF8.GetString(tokenResult);
    }

    /// <summary>Replaces matches through a stateful callback that returns UTF-16 replacement text.</summary>
    public string ReplaceToString<TState>(ReadOnlySpan<byte> input, TState state, Pcre2Utf16MatchEvaluator<TState> evaluator)
        => ReplaceToString(input, state, evaluator, 0, Pcre2MatchOptions.None);

    /// <summary>Replaces matches through a UTF-16 callback using explicit start and per-match options.</summary>
    public string ReplaceToString<TState>(ReadOnlySpan<byte> input, TState state, Pcre2Utf16MatchEvaluator<TState> evaluator, int startOffsetInBytes, Pcre2MatchOptions matchOptions)
        => ReplaceCallback(input, state, new Pcre2StringCallback<TState>(evaluator), startOffsetInBytes, matchOptions,
            CancellationToken.None, true).Text;

    /// <summary>Replaces matches through a UTF-16 callback using explicit start and per-match options, observing cancellation before matching begins.</summary>
    /// <param name="input">The well-formed UTF-8 subject to match against.</param>
    /// <param name="state">Additional state passed to the evaluator.</param>
    /// <param name="evaluator">The callback invoked for each match.</param>
    /// <param name="startOffsetInBytes">The zero-based UTF-8 byte offset where matching starts.</param>
    /// <param name="matchOptions">The per-operation match options.</param>
    /// <typeparam name="TState">The type of additional state passed to the evaluator.</typeparam>
    /// <param name="cancellationToken">The token to observe before matching begins.</param>
    /// <exception cref="OperationCanceledException">The token was already canceled.</exception>
    public string ReplaceToString<TState>(ReadOnlySpan<byte> input, TState state, Pcre2Utf16MatchEvaluator<TState> evaluator, int startOffsetInBytes, Pcre2MatchOptions matchOptions, CancellationToken cancellationToken)
        => ReplaceCallback(input, state, new Pcre2StringCallback<TState>(evaluator), startOffsetInBytes, matchOptions,
            cancellationToken, true).Text;

    /// <summary>Attempts to write a UTF-8 replacement result into a caller-provided destination.</summary>
    public OperationStatus TryReplace(ReadOnlySpan<byte> input, ReadOnlySpan<byte> replacementPatternUtf8, Span<byte> destination, out int bytesWritten)
        => TryReplace(input, replacementPatternUtf8, destination, out bytesWritten, 0, Pcre2SubstitutionOptions.None, Pcre2MatchOptions.None);

    /// <summary>Attempts to write a replacement result using explicit substitution options.</summary>
    public OperationStatus TryReplace(ReadOnlySpan<byte> input, ReadOnlySpan<byte> replacementPatternUtf8, Span<byte> destination, out int bytesWritten, Pcre2SubstitutionOptions substitutionOptions)
        => TryReplace(input, replacementPatternUtf8, destination, out bytesWritten, 0, substitutionOptions, Pcre2MatchOptions.None);

    /// <summary>Attempts to write a replacement result using explicit start, substitution, and per-match options.</summary>
    /// <returns><see cref="OperationStatus.Done"/> on success, or <see cref="OperationStatus.DestinationTooSmall"/> when the complete result does not fit.</returns>
    public OperationStatus TryReplace(ReadOnlySpan<byte> input, ReadOnlySpan<byte> replacementPatternUtf8, Span<byte> destination, out int bytesWritten, int startOffsetInBytes, Pcre2SubstitutionOptions substitutionOptions, Pcre2MatchOptions matchOptions)
    {
        _ = Utf8Validation.Validate(replacementPatternUtf8);
        var replacementText = Encoding.UTF8.GetString(replacementPatternUtf8);
        ValidateStartOffset(input, startOffsetInBytes);
        if (TryReplaceUsingLiteralDriver(
                input,
                replacementText,
                startOffsetInBytes,
                substitutionOptions,
                matchOptions,
                destination,
                out bytesWritten,
                out var literalStatus))
        {
            return literalStatus;
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

    /// <summary>Attempts to write a replacement result using explicit start, substitution, and per-match options, observing cancellation before matching begins.</summary>
    /// <param name="input">The well-formed UTF-8 subject to match against.</param>
    /// <param name="replacementPatternUtf8">The PCRE2 replacement template encoded as well-formed UTF-8.</param>
    /// <param name="destination">The destination buffer for the UTF-8 replacement result.</param>
    /// <param name="bytesWritten">When this method returns, the number of bytes written to the destination.</param>
    /// <param name="startOffsetInBytes">The zero-based UTF-8 byte offset where matching starts.</param>
    /// <param name="substitutionOptions">The substitution options.</param>
    /// <param name="matchOptions">The per-operation match options.</param>
    /// <param name="cancellationToken">The token to observe before matching begins.</param>
    /// <exception cref="OperationCanceledException">The token was already canceled.</exception>
    public OperationStatus TryReplace(ReadOnlySpan<byte> input, ReadOnlySpan<byte> replacementPatternUtf8, Span<byte> destination, out int bytesWritten, int startOffsetInBytes, Pcre2SubstitutionOptions substitutionOptions, Pcre2MatchOptions matchOptions, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return TryReplace(input, replacementPatternUtf8, destination, out bytesWritten, startOffsetInBytes, substitutionOptions, matchOptions);
    }

    /// <summary>Gets the number of name-to-slot entries, including duplicate names.</summary>
    public int NameEntryCount => NameEntries.Length;

    /// <summary>Copies name-to-slot entries into a caller-provided destination.</summary>
    /// <returns>The number of entries copied; <paramref name="isMore"/> reports whether additional entries did not fit.</returns>
    public int CopyNameEntries(Span<Pcre2NameEntry> destination, out bool isMore)
    {
        var written = Math.Min(destination.Length, NameEntries.Length);
        NameEntries.AsSpan(0, written).CopyTo(destination);
        isMore = written < NameEntries.Length;
        return written;
    }

    /// <summary>Copies every capture slot assigned to a possibly duplicated group name.</summary>
    /// <returns>The number of slot numbers copied; <paramref name="isMore"/> reports whether additional numbers did not fit.</returns>
    public int CopyNumbersForName(string name, Span<int> destination, out bool isMore)
    {
        var matchingEntries = NameEntries.Where(static e => !string.IsNullOrEmpty(e.Name)).ToArray();
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

    /// <summary>Attempts to find the first participating capture slot assigned to a group name.</summary>
    public bool TryGetFirstSetGroup(ReadOnlySpan<byte> input, string name, out Utf8Pcre2GroupContext group)
        => TryGetFirstSetGroup(input, name, out group, 0, Pcre2MatchOptions.None);

    /// <summary>Attempts to find the first participating named slot using explicit start and per-match options.</summary>
    public bool TryGetFirstSetGroup(ReadOnlySpan<byte> input, string name, out Utf8Pcre2GroupContext group, int startOffsetInBytes, Pcre2MatchOptions matchOptions)
    {
        var context = MatchDetailed(input, startOffsetInBytes, matchOptions);
        return context.TryGetFirstSetGroup(name, out group);
    }

    /// <summary>Attempts to find the first participating named slot using explicit start and per-match options, observing cancellation before matching begins.</summary>
    /// <param name="input">The well-formed UTF-8 subject to match against.</param>
    /// <param name="name">The capture group name.</param>
    /// <param name="group">When this method returns, the first participating capture slot.</param>
    /// <param name="startOffsetInBytes">The zero-based UTF-8 byte offset where matching starts.</param>
    /// <param name="matchOptions">The per-operation match options.</param>
    /// <param name="cancellationToken">The token to observe before matching begins.</param>
    /// <exception cref="OperationCanceledException">The token was already canceled.</exception>
    public bool TryGetFirstSetGroup(ReadOnlySpan<byte> input, string name, out Utf8Pcre2GroupContext group, int startOffsetInBytes, Pcre2MatchOptions matchOptions, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return TryGetFirstSetGroup(input, name, out group, startOffsetInBytes, matchOptions);
    }

    /// <summary>Copies successive non-overlapping match snapshots into a caller-provided destination.</summary>
    /// <returns>The number of matches copied; <paramref name="isMore"/> reports whether another match is available.</returns>
    public int MatchMany(ReadOnlySpan<byte> input, Span<Utf8Pcre2MatchData> destination, out bool isMore)
        => MatchMany(input, destination, out isMore, 0, Pcre2MatchOptions.None);

    /// <summary>Copies successive match snapshots using explicit start and per-match options.</summary>
    /// <returns>The number of matches copied; <paramref name="isMore"/> reports whether another match is available.</returns>
    public int MatchMany(ReadOnlySpan<byte> input, Span<Utf8Pcre2MatchData> destination, out bool isMore, int startOffsetInBytes, Pcre2MatchOptions matchOptions)
    {
        ThrowIfGenericIterationMayBeNonMonotone();
        var subject = ValidateSubjectAndStart(input, startOffsetInBytes, out var start);
        if (Pcre2GlobalOperationDriver.TryCreateCursor(
                _program,
                subject,
                start,
                matchOptions,
                out var directCursor))
        {
            var written = 0;
            while (written < destination.Length && directCursor.MoveNext())
            {
                destination[written] = Utf8Pcre2MatchData.Create(directCursor.Current);
                written++;
            }

            if (written == destination.Length && written != 0 &&
                _program.Operations.Enumerate is Pcre2BacktrackingDirectProgram
                {
                    Program.MayThrowDeferredLookaroundReset: true,
                })
            {
                isMore = true;
                return written;
            }

            isMore = directCursor.MoveNext();
            return written;
        }

        if (_program.Operations.Enumerate.Kind == Pcre2DirectProgramKind.Utf8Regex)
        {
            return MatchManyViaUtf8Regex(subject, destination, out isMore, start);
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

        // Probe exactly like the sibling MatchMany fallbacks (and the direct
        // cursor fast path): a full destination does not imply a remaining
        // match, and slice-served enumeration can end exactly at capacity.
        if (written == destination.Length)
        {
            isMore = enumerator.MoveNext();
            return written;
        }

        isMore = false;
        return written;
    }

    private int MatchManyViaUtf8Regex(
        Utf8ValidatedInput input,
        Span<Utf8Pcre2MatchData> destination,
        out bool isMore,
        Utf8BytePosition start)
    {
        if (CanUsePreparedAsciiUtf8RegexEnumerator(input.IsAscii))
        {
            var preparedEnumerator = new Utf8Pcre2ValueMatchEnumerator(
                input.Bytes,
                PrimaryUtf8Regex.ByteOffsetExecution.EnumeratePreparedMatches(input, start),
                start.Value);
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

        var enumerator = PrimaryUtf8Regex.ByteOffsetExecution.EnumerateMatches(input, start);
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

    /// <summary>Copies successive match snapshots using explicit start and per-match options, observing cancellation before matching begins.</summary>
    /// <param name="input">The well-formed UTF-8 subject to match against.</param>
    /// <param name="destination">The destination for match snapshots.</param>
    /// <param name="isMore">When this method returns, whether additional matches remain.</param>
    /// <param name="startOffsetInBytes">The zero-based UTF-8 byte offset where matching starts.</param>
    /// <param name="matchOptions">The per-operation match options.</param>
    /// <param name="cancellationToken">The token to observe before matching begins.</param>
    /// <exception cref="OperationCanceledException">The token was already canceled.</exception>
    public int MatchMany(ReadOnlySpan<byte> input, Span<Utf8Pcre2MatchData> destination, out bool isMore, int startOffsetInBytes, Pcre2MatchOptions matchOptions, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return MatchMany(input, destination, out isMore, startOffsetInBytes, matchOptions);
    }

    /// <summary>Returns the compiled expression's execution and result-shaping characteristics.</summary>
    public Utf8Pcre2Analysis Analyze()
    {
        var backtracking = (_program.Operations.Match as Pcre2BacktrackingDirectProgram)?.Program;
        var sliceFallback = (_program.Operations.Match as Pcre2SliceDirectProgram)?.Fallback;
        var literal = (_program.Operations.Match as Pcre2LiteralDirectProgram)?.Program;
        return new Utf8Pcre2Analysis
        {
            // R05: every selectable program kind is owned; no BCL-direct kind remains.
            IsFullyNative = true,
            IsExactLiteral = literal is not null,
            MinRequiredLengthInBytes = literal?.LiteralUtf8.Length ?? backtracking?.AnalyzedMinimumByteLength ?? sliceFallback?.AnalyzedMinimumByteLength ?? 0,
            HasDuplicateNames = NameEntries.GroupBy(static entry => entry.Name, StringComparer.Ordinal).Any(static group => group.Count() > 1),
            UsesBranchReset = backtracking?.GroupNames.Length > 1 &&
                backtracking.CaptureSlotCount < backtracking.GroupNames.Length,
            UsesBacktrackingControlVerbs = backtracking?.UsesBacktrackingControlVerbs ?? false,
            UsesRecursion = backtracking is not null && UsesSubroutines(backtracking),
            MayProduceNonUtf8Slices = GenericProgramUsesCodeUnit,
            MayReportNonMonotoneMatchOffsets = GenericBacktrackingMayReportNonMonotoneMatchOffsets,
            RejectsNonMonotoneIterativeMatches = GenericBacktrackingMayReportNonMonotoneMatchOffsets,
            MayFailIterativeExecutionAtRuntime = backtracking is not null && HasDeferredLookaroundReset(backtracking),
        };
    }

    /// <summary>Compiles a default PCRE2 pattern and tests the UTF-8 subject.</summary>
    public static bool IsMatch(ReadOnlySpan<byte> input, string pattern)
        => IsMatch(input, pattern, Pcre2CompileOptions.None, default, default, DefaultMatchTimeout, 0);

    /// <summary>Compiles a PCRE2 pattern with the specified options and tests the UTF-8 subject.</summary>
    public static bool IsMatch(ReadOnlySpan<byte> input, string pattern, Pcre2CompileOptions options)
        => IsMatch(input, pattern, options, default, default, DefaultMatchTimeout, 0);

    /// <summary>Compiles and tests a PCRE2 pattern with explicit settings, limits, timeout, and start byte offset.</summary>
    public static bool IsMatch(ReadOnlySpan<byte> input, string pattern, Pcre2CompileOptions options, Utf8Pcre2CompileSettings compileSettings, Utf8Pcre2ExecutionLimits defaultExecutionLimits, TimeSpan matchTimeout, int startOffsetInBytes)
    {
        return new Utf8Pcre2Regex(pattern, options, compileSettings, defaultExecutionLimits, matchTimeout).IsMatch(input, startOffsetInBytes, Pcre2MatchOptions.None);
    }

    /// <summary>Compiles and tests a PCRE2 pattern with explicit settings, limits, timeout, and start byte offset, observing cancellation before matching begins.</summary>
    /// <param name="input">The well-formed UTF-8 subject to match against.</param>
    /// <param name="pattern">The PCRE2 pattern.</param>
    /// <param name="options">The PCRE2 compile options.</param>
    /// <param name="compileSettings">The compile-time settings.</param>
    /// <param name="defaultExecutionLimits">The default execution budgets.</param>
    /// <param name="matchTimeout">The maximum duration of the operation.</param>
    /// <param name="startOffsetInBytes">The zero-based UTF-8 byte offset where matching starts.</param>
    /// <param name="cancellationToken">The token to observe before matching begins.</param>
    /// <exception cref="OperationCanceledException">The token was already canceled.</exception>
    public static bool IsMatch(ReadOnlySpan<byte> input, string pattern, Pcre2CompileOptions options, Utf8Pcre2CompileSettings compileSettings, Utf8Pcre2ExecutionLimits defaultExecutionLimits, TimeSpan matchTimeout, int startOffsetInBytes, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return new Utf8Pcre2Regex(pattern, options, compileSettings, defaultExecutionLimits, matchTimeout).IsMatch(input, startOffsetInBytes, Pcre2MatchOptions.None, cancellationToken);
    }

    /// <summary>Compiles a default PCRE2 pattern and finds the first match.</summary>
    public static Utf8Pcre2ValueMatch Match(ReadOnlySpan<byte> input, string pattern)
        => Match(input, pattern, Pcre2CompileOptions.None, default, default, DefaultMatchTimeout, 0);

    /// <summary>Compiles a PCRE2 pattern with the specified options and finds the first match.</summary>
    public static Utf8Pcre2ValueMatch Match(ReadOnlySpan<byte> input, string pattern, Pcre2CompileOptions options)
        => Match(input, pattern, options, default, default, DefaultMatchTimeout, 0);

    /// <summary>Compiles and matches a PCRE2 pattern with explicit settings, limits, timeout, and start byte offset.</summary>
    public static Utf8Pcre2ValueMatch Match(ReadOnlySpan<byte> input, string pattern, Pcre2CompileOptions options, Utf8Pcre2CompileSettings compileSettings, Utf8Pcre2ExecutionLimits defaultExecutionLimits, TimeSpan matchTimeout, int startOffsetInBytes)
    {
        return new Utf8Pcre2Regex(pattern, options, compileSettings, defaultExecutionLimits, matchTimeout).Match(input, startOffsetInBytes, Pcre2MatchOptions.None);
    }

    /// <summary>Compiles and matches a PCRE2 pattern with explicit settings, limits, timeout, and start byte offset, observing cancellation before matching begins.</summary>
    /// <param name="input">The well-formed UTF-8 subject to match against.</param>
    /// <param name="pattern">The PCRE2 pattern.</param>
    /// <param name="options">The PCRE2 compile options.</param>
    /// <param name="compileSettings">The compile-time settings.</param>
    /// <param name="defaultExecutionLimits">The default execution budgets.</param>
    /// <param name="matchTimeout">The maximum duration of the operation.</param>
    /// <param name="startOffsetInBytes">The zero-based UTF-8 byte offset where matching starts.</param>
    /// <param name="cancellationToken">The token to observe before matching begins.</param>
    /// <exception cref="OperationCanceledException">The token was already canceled.</exception>
    public static Utf8Pcre2ValueMatch Match(ReadOnlySpan<byte> input, string pattern, Pcre2CompileOptions options, Utf8Pcre2CompileSettings compileSettings, Utf8Pcre2ExecutionLimits defaultExecutionLimits, TimeSpan matchTimeout, int startOffsetInBytes, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return new Utf8Pcre2Regex(pattern, options, compileSettings, defaultExecutionLimits, matchTimeout).Match(input, startOffsetInBytes, Pcre2MatchOptions.None, cancellationToken);
    }

    /// <summary>Compiles a default PCRE2 pattern and returns replaced UTF-8 bytes.</summary>
    public static byte[] Replace(ReadOnlySpan<byte> input, string pattern, string replacement)
        => Replace(input, pattern, replacement, Pcre2CompileOptions.None, default, default, DefaultMatchTimeout, 0, Pcre2SubstitutionOptions.None);

    /// <summary>Compiles a PCRE2 pattern with the specified options and returns replaced UTF-8 bytes.</summary>
    public static byte[] Replace(ReadOnlySpan<byte> input, string pattern, string replacement, Pcre2CompileOptions options)
        => Replace(input, pattern, replacement, options, default, default, DefaultMatchTimeout, 0, Pcre2SubstitutionOptions.None);

    /// <summary>Compiles and replaces with explicit PCRE2 settings, limits, timeout, start offset, and substitution options.</summary>
    public static byte[] Replace(ReadOnlySpan<byte> input, string pattern, string replacement, Pcre2CompileOptions options, Utf8Pcre2CompileSettings compileSettings, Utf8Pcre2ExecutionLimits defaultExecutionLimits, TimeSpan matchTimeout, int startOffsetInBytes, Pcre2SubstitutionOptions substitutionOptions)
    {
        return new Utf8Pcre2Regex(pattern, options, compileSettings, defaultExecutionLimits, matchTimeout)
            .Replace(input, replacement, startOffsetInBytes, substitutionOptions, Pcre2MatchOptions.None);
    }

    /// <summary>Compiles and replaces with explicit PCRE2 settings, limits, timeout, start offset, and substitution options, observing cancellation before matching begins.</summary>
    /// <param name="input">The well-formed UTF-8 subject to match against.</param>
    /// <param name="pattern">The PCRE2 pattern.</param>
    /// <param name="replacement">The PCRE2 replacement template.</param>
    /// <param name="options">The PCRE2 compile options.</param>
    /// <param name="compileSettings">The compile-time settings.</param>
    /// <param name="defaultExecutionLimits">The default execution budgets.</param>
    /// <param name="matchTimeout">The maximum duration of the operation.</param>
    /// <param name="startOffsetInBytes">The zero-based UTF-8 byte offset where matching starts.</param>
    /// <param name="substitutionOptions">The substitution options.</param>
    /// <param name="cancellationToken">The token to observe before matching begins.</param>
    /// <exception cref="OperationCanceledException">The token was already canceled.</exception>
    public static byte[] Replace(ReadOnlySpan<byte> input, string pattern, string replacement, Pcre2CompileOptions options, Utf8Pcre2CompileSettings compileSettings, Utf8Pcre2ExecutionLimits defaultExecutionLimits, TimeSpan matchTimeout, int startOffsetInBytes, Pcre2SubstitutionOptions substitutionOptions, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return new Utf8Pcre2Regex(pattern, options, compileSettings, defaultExecutionLimits, matchTimeout).Replace(input, replacement, startOffsetInBytes, substitutionOptions, Pcre2MatchOptions.None, cancellationToken);
    }

    private static Utf8ValidatedInput ValidateSubjectAndStart(
        ReadOnlySpan<byte> input,
        int startOffsetInBytes,
        out Utf8BytePosition start)
    {
        var subject = Utf8ValidatedInput.Create(input);
        start = subject.GetBytePosition(startOffsetInBytes, nameof(startOffsetInBytes));
        return subject;
    }

    private static void ValidateStartOffset(ReadOnlySpan<byte> input, int startOffsetInBytes)
    {
        var subject = Utf8ValidatedInput.Create(input);
        _ = subject.GetBytePosition(startOffsetInBytes, nameof(startOffsetInBytes));
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

    private static RegexOptions ToRegexOptions(Pcre2CompileOptions options)
    {
        var regexOptions = RegexOptions.None;
        if ((options & Pcre2CompileOptions.Caseless) != 0)
        {
            regexOptions |= RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;
        }

        if ((options & Pcre2CompileOptions.Multiline) != 0)
        {
            regexOptions |= RegexOptions.Multiline;
        }

        if ((options & Pcre2CompileOptions.DotAll) != 0)
        {
            regexOptions |= RegexOptions.Singleline;
        }

        if ((options & (Pcre2CompileOptions.Extended | Pcre2CompileOptions.ExtendedMore)) != 0)
        {
            regexOptions |= RegexOptions.IgnorePatternWhitespace;
        }

        return regexOptions;
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

    private Utf8Pcre2MatchContext MatchDetailedUsingUtf8Backend(
        Utf8ValidatedInput input,
        Utf8BytePosition start)
    {
        var context = PrimaryUtf8Regex.ByteOffsetExecution.MatchDetailed(input, start);
        return CreateManagedProfileMatchContext(input.Bytes, context, GroupNames);
    }

    private Utf8Pcre2ProbeResult ProbeViaNonPartialMatch(ReadOnlySpan<byte> input, int startOffsetInBytes)
    {
        var match = MatchDetailed(input, startOffsetInBytes, Pcre2MatchOptions.None);
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
        => Utf8Pcre2ValueMatch.Create(input, CreateManagedProfileGroupData(match, byteOffsetBase: 0, utf16OffsetBase: 0));

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

    private bool TryReplaceUsingLiteralDriver(
        ReadOnlySpan<byte> input,
        string replacement,
        int startOffsetInBytes,
        Pcre2SubstitutionOptions substitutionOptions,
        Pcre2MatchOptions matchOptions,
        out byte[] result)
    {
        if (!TryGetLiteralDriverReplacementPlan(
                replacement,
                substitutionOptions,
                out var plan,
                out var replacementOnly))
        {
            result = [];
            return false;
        }

        var subject = ValidateSubjectAndStart(input, startOffsetInBytes, out var start);
        if (!Pcre2GlobalOperationDriver.TryCreateCursor(
                _program,
                subject,
                start,
                matchOptions,
                out var cursor))
        {
            result = [];
            return false;
        }

        var ledger = new Utf8ReplacementRangeLedger();
        ulong outputReservation = 0;
        try
        {
            var outputLength = BuildLiteralDriverReplacementRanges(
                input.Length,
                plan,
                replacementOnly,
                ref cursor,
                ref ledger);
            if (ledger.Count == 0)
            {
                result = Utf8InvocationOutput.AllocateBytes(replacementOnly ? 0 : input.Length, cursor.WorkingBufferControl, out outputReservation);
                if (!replacementOnly) Utf8InvocationOutput.Copy(input, result, cursor.WorkingBufferControl);
                return true;
            }

            result = Utf8InvocationOutput.AllocateBytes(outputLength.Value, cursor.WorkingBufferControl, out outputReservation);
            EmitLiteralDriverReplacement(input, plan, replacementOnly, result, ledger.WrittenRanges, cursor.WorkingBufferControl);
            return true;
        }
        finally
        {
            cursor.WorkingBufferControl.ReleaseWorking(outputReservation);
            ledger.Dispose(cursor.WorkingBufferControl);
        }
    }

    private bool TryReplaceUsingLiteralDriver(
        ReadOnlySpan<byte> input,
        string replacement,
        int startOffsetInBytes,
        Pcre2SubstitutionOptions substitutionOptions,
        Pcre2MatchOptions matchOptions,
        Span<byte> destination,
        out int bytesWritten,
        out OperationStatus status)
    {
        if (!TryGetLiteralDriverReplacementPlan(
                replacement,
                substitutionOptions,
                out var plan,
                out var replacementOnly))
        {
            bytesWritten = 0;
            status = default;
            return false;
        }

        var subject = ValidateSubjectAndStart(input, startOffsetInBytes, out var start);
        if (!Pcre2GlobalOperationDriver.TryCreateCursor(
                _program,
                subject,
                start,
                matchOptions,
                out var cursor))
        {
            bytesWritten = 0;
            status = default;
            return false;
        }

        var ledger = new Utf8ReplacementRangeLedger();
        try
        {
            var outputLength = BuildLiteralDriverReplacementRanges(
                input.Length,
                plan,
                replacementOnly,
                ref cursor,
                ref ledger);
            if (outputLength.Value > destination.Length)
            {
                bytesWritten = (substitutionOptions & Pcre2SubstitutionOptions.SubstituteOverflowLength) != 0
                    ? outputLength.Value
                    : 0;
                status = OperationStatus.DestinationTooSmall;
                return true;
            }

            if (ledger.Count == 0)
            {
                if (!replacementOnly)
                {
                    Utf8InvocationOutput.Copy(input, destination, cursor.WorkingBufferControl);
                }

                bytesWritten = outputLength.Value;
                status = OperationStatus.Done;
                return true;
            }

            EmitLiteralDriverReplacement(input, plan, replacementOnly, destination, ledger.WrittenRanges, cursor.WorkingBufferControl);
            bytesWritten = outputLength.Value;
            status = OperationStatus.Done;
            return true;
        }
        finally
        {
            ledger.Dispose(cursor.WorkingBufferControl);
        }
    }

    private bool TryGetLiteralDriverReplacementPlan(
        string replacement,
        Pcre2SubstitutionOptions substitutionOptions,
        out Pcre2SimpleReplacementPlan plan,
        out bool replacementOnly)
    {
        if (_program.Operations.Replace is Pcre2BacktrackingDirectProgram backtrackingProgram &&
            backtrackingProgram.Program.SuppressesUnresetEmptyMatches)
        {
            plan = default;
            replacementOnly = false;
            return false;
        }

        if (_program.Operations.Replace is not (
            Pcre2LiteralDirectProgram or
            Pcre2CharacterDirectProgram or
            Pcre2SingleTokenRepeatDirectProgram or
            Pcre2BacktrackingDirectProgram))
        {
            plan = default;
            replacementOnly = false;
            return false;
        }

        replacementOnly = (substitutionOptions & Pcre2SubstitutionOptions.SubstituteReplacementOnly) != 0;
        if ((substitutionOptions & Pcre2SubstitutionOptions.SubstituteLiteral) != 0)
        {
            var unsupported = substitutionOptions &
                ~(Pcre2SubstitutionOptions.SubstituteLiteral |
                  Pcre2SubstitutionOptions.SubstituteOverflowLength |
                  Pcre2SubstitutionOptions.SubstituteReplacementOnly |
                  Pcre2SubstitutionOptions.SubstituteMatched);
            if (unsupported != Pcre2SubstitutionOptions.None)
            {
                plan = default;
                return false;
            }

            plan = Pcre2SimpleReplacementParser.Literal(replacement);
            return true;
        }

        var templateOptions = substitutionOptions &
            ~(Pcre2SubstitutionOptions.SubstituteOverflowLength |
              Pcre2SubstitutionOptions.SubstituteReplacementOnly |
              Pcre2SubstitutionOptions.SubstituteMatched);
        var parsed = GetSimpleReplacementPlan(replacement, templateOptions);
        if (parsed is not { } simplePlan)
        {
            plan = default;
            return false;
        }

        plan = simplePlan;
        return true;
    }

    private static Utf8ReplacementOutputLength BuildLiteralDriverReplacementRanges(
        int inputLength,
        Pcre2SimpleReplacementPlan plan,
        bool replacementOnly,
        ref Pcre2GlobalMatchCursor cursor,
        ref Utf8ReplacementRangeLedger ledger)
    {
        var outputLength = new Utf8ReplacementOutputLength(replacementOnly ? 0 : inputLength);
        while (cursor.MoveNext())
        {
            var match = cursor.Current;
            var matchLength = match.EndOffsetInBytes - match.StartOffsetInBytes;
            ledger.Add(new Utf8ReplacementRange(match.StartOffsetInBytes, matchLength), cursor.WorkingBufferControl);
            var emittedLength = GetLiteralDriverReplacementLength(plan, matchLength);
            outputLength.ReplaceRange(replacementOnly ? 0 : matchLength, emittedLength);
        }

        return outputLength;
    }

    private static int GetLiteralDriverReplacementLength(Pcre2SimpleReplacementPlan plan, int matchLength)
    {
        var length = 0;
        foreach (var segment in plan.Segments)
        {
            length = checked(length + (segment.IsWholeMatch ? matchLength : segment.LiteralUtf8.Length));
        }

        return length;
    }

    private static void EmitLiteralDriverReplacement(
        ReadOnlySpan<byte> input,
        Pcre2SimpleReplacementPlan plan,
        bool replacementOnly,
        Span<byte> destination,
        ReadOnlySpan<Utf8ReplacementRange> matches, scoped Pcre2CaptureProjectionBudget control)
    {
        var sourcePosition = 0;
        var sink = new Utf8ReplacementOutputSink(destination);
        foreach (var match in matches)
        {
            if (!replacementOnly)
            {
                sink.AppendSlice(input, sourcePosition, match.Start - sourcePosition, control);
            }

            foreach (var segment in plan.Segments)
            {
                if (segment.IsWholeMatch)
                {
                    sink.AppendSlice(input, match.Start, match.Length, control);
                }
                else
                {
                    sink.Append(segment.LiteralUtf8, control);
                }
            }

            sourcePosition = match.Start + match.Length;
        }

        if (!replacementOnly)
        {
            sink.Append(input[sourcePosition..], control);
        }
    }

    private byte[] ReplaceCore(
        ReadOnlySpan<byte> input,
        string replacement,
        Pcre2SubstitutionOptions substitutionOptions,
        int startOffsetInBytes,
        Pcre2MatchOptions matchOptions,
        CancellationToken cancellationToken)
    {
        using var preparationControl = Pcre2ReplacementPreparationControl.EnterOperation(_program.Request.MatchTimeout, cancellationToken);
        var result = ReplaceCoreWithPreparationControl(input, replacement, substitutionOptions, startOffsetInBytes, matchOptions, cancellationToken);
        Pcre2ReplacementPreparationControl.Check();
        return result;
    }

    private byte[] ReplaceCoreWithPreparationControl(ReadOnlySpan<byte> input, string replacement,
        Pcre2SubstitutionOptions substitutionOptions, int startOffsetInBytes,
        Pcre2MatchOptions matchOptions, CancellationToken cancellationToken)
    {
        if (RejectsReplacementIteration())
        {
            throw new NotSupportedException("SPEC-PCRE2 rejects replacement for lookaround-\\K iterative matches.");
        }

        if (TryReplaceUsingLiteralDriver(
                input,
                replacement,
                startOffsetInBytes,
                substitutionOptions,
                matchOptions,
                out var genericResult))
        {
            return genericResult;
        }

        if (_program.Operations.Replace is Pcre2BacktrackingDirectProgram backtrackingProgram)
        {
            return ReplaceBacktrackingDetailed(
                input,
                backtrackingProgram.Program,
                replacement,
                substitutionOptions,
                startOffsetInBytes,
                matchOptions, cancellationToken);
        }

        if (_program.Operations.Replace is Pcre2SliceDirectProgram sliceProgram)
        {
            if (matchOptions != Pcre2MatchOptions.None)
            {
                return ReplaceBacktrackingDetailed(
                    input,
                    sliceProgram.Fallback,
                    replacement,
                    substitutionOptions,
                    startOffsetInBytes,
                    matchOptions, cancellationToken);
            }

            return ReplaceSliceDetailed(
                input,
                sliceProgram,
                replacement,
                substitutionOptions,
                startOffsetInBytes,
                cancellationToken);
        }

        if (_program.Operations.Replace.Kind != Pcre2DirectProgramKind.None)
        {
            return ReplaceUsingDetailedIteration(input, replacement, substitutionOptions, startOffsetInBytes, cancellationToken);
        }

        throw CreateUnsupportedReplacementException();
    }

    private static NotSupportedException CreateUnsupportedProbeException()
        => new("SPEC-PCRE2 does not support Probe(...) for this pattern.");

    private static NotSupportedException CreateUnsupportedReplacementException(string? message)
        => new(message ?? "SPEC-PCRE2 does not support replacement for this pattern.");

    private static NotSupportedException CreateUnsupportedReplacementException()
        => CreateUnsupportedReplacementException(null);

    private Pcre2ReplacementTemplateWriter CreateTemplateWriter(ReadOnlySpan<byte> input, CancellationToken token)
        => new(input, NameEntries, _program.Request.DefaultLimits.HeapLimitInBytes, _program.Request.MatchTimeout, token);

    private byte[] ReplaceUsingDetailedIteration(ReadOnlySpan<byte> input, string replacement,
        Pcre2SubstitutionOptions options, int startOffsetInBytes, CancellationToken token)
    {
        var replacementOnly = (options & Pcre2SubstitutionOptions.SubstituteReplacementOnly) != 0;
        var templateOptions = options & ~Pcre2SubstitutionOptions.SubstituteReplacementOnly;
        var preparation = (options & Pcre2SubstitutionOptions.SubstituteLiteral) != 0
            ? new Pcre2ReplacementPreparation(Pcre2SimpleReplacementParser.Literal(replacement), null)
            : GetReplacementPreparation(replacement, templateOptions);
        var enumerator = EnumerateMatches(input, startOffsetInBytes, Pcre2MatchOptions.None, token);
        if (!enumerator.MoveNext()) return replacementOnly ? [] : input.ToArray();
        var writer = CreateTemplateWriter(input, token);
        try
        {
            var position = 0;
            do
            {
                var value = enumerator.Current;
                var match = MatchDetailed(input, value.StartOffsetInBytes, Pcre2MatchOptions.None, token);
                if (!match.Success) throw new InvalidOperationException("Translated enumeration produced a match that MatchDetailed(...) could not reproduce.");
                if (!replacementOnly) writer.AppendGap(position, value.StartOffsetInBytes - position);
                writer.AppendTemplate(preparation, templateOptions, CaptureGroups(match), value.StartOffsetInBytes,
                    value.EndOffsetInBytes - value.StartOffsetInBytes, match.Mark);
                position = value.EndOffsetInBytes;
            } while (enumerator.MoveNext());
            if (!replacementOnly) writer.AppendGap(position, input.Length - position);
            return writer.Complete();
        }
        finally { writer.Release(); }
    }

    private byte[] ReplaceBacktrackingDetailed(ReadOnlySpan<byte> input, Pcre2BacktrackingProgram program,
        string replacement, Pcre2SubstitutionOptions options, int startOffsetInBytes, Pcre2MatchOptions matchOptions, CancellationToken token)
    {
        var validated = ValidateSubjectAndStart(input, startOffsetInBytes, out var start);
        var cursor = new Pcre2BacktrackingDetailedGlobalMatchCursor(program, _program.CandidateSearchPlan,
            validated, start, matchOptions, _program.Request);
        if (!cursor.MoveNext()) return (options & Pcre2SubstitutionOptions.SubstituteReplacementOnly) != 0 ? [] : input.ToArray();
        var replacementOnly = (options & Pcre2SubstitutionOptions.SubstituteReplacementOnly) != 0;
        var templateOptions = options & ~Pcre2SubstitutionOptions.SubstituteReplacementOnly;
        var preparation = (options & Pcre2SubstitutionOptions.SubstituteLiteral) != 0
            ? new Pcre2ReplacementPreparation(Pcre2SimpleReplacementParser.Literal(replacement), null)
            : GetReplacementPreparation(replacement, templateOptions);
        var writer = CreateTemplateWriter(input, token);
        try
        {
            var position = 0;
            do
            {
                var groups = cursor.ProjectCurrentCaptures();
                var value = groups[0];
                if (!replacementOnly) writer.AppendGap(position, value.StartOffsetInBytes - position);
                writer.AppendTemplate(preparation, templateOptions, groups, value.StartOffsetInBytes,
                    value.EndOffsetInBytes - value.StartOffsetInBytes, cursor.Current.Mark);
                position = value.EndOffsetInBytes;
            } while (cursor.MoveNext());
            if (!replacementOnly) writer.AppendGap(position, input.Length - position);
            return writer.Complete();
        }
        finally { writer.Release(); }
    }

    private byte[] ReplaceSliceDetailed(ReadOnlySpan<byte> input, Pcre2SliceDirectProgram sliceProgram,
        string replacement, Pcre2SubstitutionOptions options, int startOffsetInBytes, CancellationToken token)
    {
        var validated = ValidateSubjectAndStart(input, startOffsetInBytes, out var start);
        var cursor = new Pcre2SliceGlobalMatchCursor(sliceProgram.SliceProgram, validated, start, _program.Request, token);
        if (!cursor.MoveNext()) return (options & Pcre2SubstitutionOptions.SubstituteReplacementOnly) != 0
            ? [] : Pcre2ReplacementTemplateWriter.CopyUnchanged(input, ref cursor.WorkingInvocation);
        var replacementOnly = (options & Pcre2SubstitutionOptions.SubstituteReplacementOnly) != 0;
        var templateOptions = options & ~Pcre2SubstitutionOptions.SubstituteReplacementOnly;
        var preparation = (options & Pcre2SubstitutionOptions.SubstituteLiteral) != 0
            ? new Pcre2ReplacementPreparation(Pcre2SimpleReplacementParser.Literal(replacement), null)
            : GetReplacementPreparation(replacement, templateOptions);
        var writer = new Pcre2ReplacementTemplateWriter(input, NameEntries, cursor.WorkingInvocation);
        try
        {
            var position = 0;
            do
            {
                var range = cursor.Current;
                var groups = cursor.CaptureGroupsAt(range.StartOffsetInBytes, range.EndOffsetInBytes > range.StartOffsetInBytes);
                var value = groups[0];
                if (!replacementOnly) writer.AppendGap(position, value.StartOffsetInBytes - position);
                writer.AppendTemplate(preparation, templateOptions, groups, value.StartOffsetInBytes,
                    value.EndOffsetInBytes - value.StartOffsetInBytes, null);
                position = value.EndOffsetInBytes;
            } while (cursor.MoveNext());
            if (!replacementOnly) writer.AppendGap(position, input.Length - position);
            return writer.Complete();
        }
        finally { writer.Release(); }
    }

    private bool RejectsReplacementIteration()
    {
        return GenericBacktrackingMayReportNonMonotoneMatchOffsets ||
            _program.Operations.Match is Pcre2BacktrackingDirectProgram backtracking &&
            HasDeferredLookaroundReset(backtracking.Program);
    }

    private static bool UsesSubroutines(Pcre2BacktrackingProgram program)
    {
        if (program.SubroutineTargets.Length != 0)
        {
            return true;
        }

        return program.AssertionPrograms.Any(UsesSubroutines);
    }

    private static bool HasDeferredLookaroundReset(Pcre2BacktrackingProgram program)
    {
        if (program.MayThrowDeferredLookaroundReset)
        {
            return true;
        }

        if (program.MayReportNonMonotoneMatchOffsets)
        {
            return false;
        }

        return program.AssertionPrograms.Any(static assertion =>
            assertion.UsesMatchBoundaryReset || HasDeferredLookaroundReset(assertion));
    }

    private bool GenericBacktrackingMayReportNonMonotoneMatchOffsets =>
        _program.Operations.Match is Pcre2BacktrackingDirectProgram
        {
            Program.MayReportNonMonotoneMatchOffsets: true,
        };

    private bool GenericProgramUsesCodeUnit => _program.Operations.Match switch
    {
        Pcre2CharacterDirectProgram { Program.UsesCodeUnit: true } => true,
        Pcre2SingleTokenRepeatDirectProgram { Program.UsesCodeUnit: true } => true,
        Pcre2BacktrackingDirectProgram { Program.UsesCodeUnit: true } => true,
        _ => false,
    };

    private void ThrowIfGenericIterationMayBeNonMonotone()
    {
        if (GenericBacktrackingMayReportNonMonotoneMatchOffsets)
        {
            throw new NotSupportedException("SPEC-PCRE2 rejects non-monotone iterative matches.");
        }
    }

    private Pcre2SimpleReplacementPlan? GetSimpleReplacementPlan(string replacement, Pcre2SubstitutionOptions options)
        => GetReplacementPreparation(replacement, options).Simple;

    private Pcre2ReplacementPreparation GetReplacementPreparation(string replacement, Pcre2SubstitutionOptions options)
    {
        using var control = Pcre2ReplacementPreparationControl.JoinOrEnter(_program.Request.MatchTimeout);
        var result = _replacementComponent.GetOrAdd(new Pcre2ReplacementCacheKey(replacement, options), static key =>
            TryParseSimpleReplacementPlan(key.Replacement, key.Options, out var simple)
                ? new(simple, null) : new(null, Pcre2PreparedReplacementParser.Parse(key.Replacement, key.Options)));
        Pcre2ReplacementPreparationControl.Check();
        return result;
    }

    private static bool TryParseSimpleReplacementPlan(string replacement, Pcre2SubstitutionOptions substitutionOptions, out Pcre2SimpleReplacementPlan plan)
        => Pcre2SimpleReplacementParser.TryParse(replacement, substitutionOptions, out plan);

    private readonly record struct Pcre2ReplacementCacheKey(string Replacement, Pcre2SubstitutionOptions Options);

    private sealed class Pcre2ReplacementComponent
    {
        private const int Capacity = 16;
        private readonly Utf8BoundedPreparationCache<Pcre2ReplacementCacheKey, Pcre2ReplacementPreparation> _plans;

        private Pcre2ReplacementComponent(Utf8BoundedPreparationCache<Pcre2ReplacementCacheKey, Pcre2ReplacementPreparation> plans)
            => _plans = plans;

        internal static Pcre2ReplacementComponent Create()
        {
            using var owner = Utf8ConstructionScope.Enter();
            var plans = Utf8BoundedPreparationCache<Pcre2ReplacementCacheKey, Pcre2ReplacementPreparation>.CreateRetained(Capacity, EqualityComparer<Pcre2ReplacementCacheKey>.Default);
            var result = owner.Budget.AllocateObject(plans, static value => new Pcre2ReplacementComponent(value));
            owner.Complete();
            return result;
        }

        internal int Count => _plans.Count;
        internal ulong PayloadBytes => _plans.PayloadBytes;

        internal Pcre2ReplacementPreparation GetOrAdd(
            Pcre2ReplacementCacheKey key,
            Func<Pcre2ReplacementCacheKey, Pcre2ReplacementPreparation> factory)
            => _plans.GetOrAdd(key, factory, static (item, prepare) => prepare(item), default(Pcre2ReplacementTextControl), default(Pcre2ReplacementRetainedStorage));
    }

    private readonly struct Pcre2ReplacementRetainedStorage : IUtf8RetainedStorage<Pcre2ReplacementCacheKey, Pcre2ReplacementPreparation>
    {
        public bool IsCertified => true;
        public Utf8RetainedStorageScope Scope => Utf8RetainedStorageScope.CompletePayload;
        public ulong GetKeyCharge(Pcre2ReplacementCacheKey key) => Utf8ConstructionBudget.StringBytes(key.Replacement.Length);
        public bool TryGetValueCharge<TControl>(Pcre2ReplacementPreparation value, ulong maximum, TControl control, out ulong charge)
            where TControl : struct, IUtf8ConstructionTextControl
        {
            var budget = Utf8ConstructionScope.CurrentBudget;
            ulong total = 0;
            charge = 0;
            control.Check(); budget.ChargeWork(1);
            if (value.Simple is { } simple)
            {
                if (!Add(Utf8ConstructionBudget.ArrayBytes<Pcre2SimpleReplacementSegment>(simple.Segments.Length))) return false;
                foreach (var segment in simple.Segments)
                    if (!Add(Utf8ConstructionBudget.ArrayBytes<byte>(segment.LiteralUtf8?.Length ?? 0))) return false;
            }
            if (value.Prepared is { } prepared)
            {
                if (!Add(Utf8ConstructionBudget.ArrayBytes<Pcre2ReplacementInstruction>(prepared.Instructions.Length)
                    + Utf8ConstructionBudget.ArrayBytes<Pcre2ReplacementFragment>(prepared.Fragments.Length))) return false;
                foreach (var instruction in prepared.Instructions)
                    if (!Add(Utf8ConstructionBudget.ArrayBytes<byte>(instruction.Payload?.Length ?? 0))) return false;
            }
            charge = total;
            return true;

            bool Add(ulong bytes)
            {
                control.Check(); budget.ChargeWork(1);
                if (bytes > maximum - total) return false;
                total += bytes; return true;
            }
        }
    }

}
