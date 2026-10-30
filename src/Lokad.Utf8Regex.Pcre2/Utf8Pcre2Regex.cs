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
    private readonly Utf8BoundedPreparationCache<Pcre2ReplacementCacheKey, Pcre2ReplacementPreparation> _replacementCache;

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
        // Preserve timeout-before-flags/settings ordering from string entries,
        // and reject invalid arguments before any byte-pattern conversion.
        var validatedTimeout = Utf8MatchTimeout.Validate(matchTimeout, nameof(matchTimeout));
        Pcre2CompileValidator.ValidateArguments(options, compileSettings);
        ArgumentNullException.ThrowIfNull(patternSource.Text, "pattern");
        using var construction = Utf8ConstructionScope.Enter();
        var pattern = patternSource.Text;
        if (patternSource.IsUtf8)
        {
            construction.Budget.ChargeWork((ulong)patternSource.Utf8.Length);
            ValidatePatternUtf8(patternSource.Utf8, construction.Budget);
            pattern = construction.Budget.DecodeUtf8(patternSource.Utf8, Encoding.UTF8);
        }
        var request = new Pcre2CompileRequest(
            pattern,
            options,
            compileSettings,
            defaultExecutionLimits,
            validatedTimeout);
        _program = Pcre2Compiler.Compile(request);
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
        _replacementCache = Utf8BoundedPreparationCache<Pcre2ReplacementCacheKey, Pcre2ReplacementPreparation>.CreateRetained(
            16, EqualityComparer<Pcre2ReplacementCacheKey>.Default);
        construction.Complete();
    }

    /// <summary>Maps a shared resource policy onto PCRE2 execution limits.</summary>
    /// <exception cref="ArgumentOutOfRangeException">A policy budget is zero or exceeds the 32-bit work/depth range.</exception>
    private static Utf8Pcre2ExecutionLimits ToExecutionLimits(Utf8ResourcePolicy resourcePolicy)
    {
        resourcePolicy.Validate(nameof(resourcePolicy));
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
            ResultLimitInBytes = resourcePolicy.MaxResultBytes ?? 0ul,
        };
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

    private bool IsMatchCore(ReadOnlySpan<byte> input, int startOffsetInBytes, Pcre2MatchOptions matchOptions,
        CancellationToken cancellationToken, IUtf8MemoryAllowance? workingMemoryAllowance = null)
    {
        var invocation = Pcre2SliceDriver.CreateInvocation(_program.Request, cancellationToken, workingMemoryAllowance);
        try
        {
            Pcre2SliceDriver.ThrowIfSliceBreached(Utf8ValidatedInput.TryCreate(input, ref invocation, out var subject));
            var start = subject.GetBytePosition(startOffsetInBytes, nameof(startOffsetInBytes));
            if (Pcre2Runner.TryIsMatch(_program, ref subject, start, matchOptions, ref invocation, out var result))
            {
                if (!invocation.IsUnmetered)
                {
                    invocation.Budget.ThrowIfCancellationRequested();
                    invocation.Budget.CheckDeadline();
                    invocation.Budget.ThrowIfCancellationRequested();
                }
                return result;
            }
            throw CreateUnsupportedPatternException();
        }
        catch (Utf8ExecutionDeadlineExpiredException)
        {
            throw new Pcre2MatchException("The PCRE2 match deadline expired.", Pcre2ErrorKind.Timeout);
        }
        finally
        {
            // This lexical bool result leaves no cursor or payload alias. The
            // separate funded invocation owns every remaining temporary loan.
            if (workingMemoryAllowance is not null) invocation.Ledger.Release(invocation.Ledger.ReservedBytes);
        }
    }

    /// <summary>Determines whether a match exists using an explicit start byte offset and per-operation options, observing cancellation during validation and matching.</summary>
    /// <param name="input">The well-formed UTF-8 subject to match against.</param>
    /// <param name="startOffsetInBytes">The zero-based UTF-8 byte offset where matching starts.</param>
    /// <param name="matchOptions">The per-operation match options.</param>
    /// <param name="cancellationToken">The token to observe before entry and during bounded validation and matching.</param>
    /// <exception cref="OperationCanceledException">Cancellation was requested.</exception>
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

    private int CountCore(ReadOnlySpan<byte> input, int startOffsetInBytes, Pcre2MatchOptions matchOptions,
        CancellationToken cancellationToken, IUtf8MemoryAllowance? workingMemoryAllowance = null)
    {
        ThrowIfGenericIterationMayBeNonMonotone();
        var invocation = Pcre2SliceDriver.CreateInvocation(_program.Request, cancellationToken, workingMemoryAllowance);
        try
        {
            Pcre2SliceDriver.ThrowIfSliceBreached(Utf8ValidatedInput.TryCreate(input, ref invocation, out var subject));
            var start = subject.GetBytePosition(startOffsetInBytes, nameof(startOffsetInBytes));
            if (Pcre2GlobalOperationDriver.TryCount(_program, ref subject, start, matchOptions, ref invocation, out var result))
            {
                Pcre2SliceDriver.CheckInvocation(ref invocation);
                return result;
            }
            throw CreateUnsupportedPatternException();
        }
        catch (Utf8ExecutionDeadlineExpiredException)
        {
            throw new Pcre2MatchException("The PCRE2 match deadline expired.", Pcre2ErrorKind.Timeout);
        }
        finally
        {
            if (workingMemoryAllowance is not null) invocation.Ledger.Release(invocation.Ledger.ReservedBytes);
        }
    }

    /// <summary>Counts non-overlapping matches using an explicit start byte offset and per-operation options, observing cancellation during validation and cumulative global matching.</summary>
    /// <param name="input">The well-formed UTF-8 subject to match against.</param>
    /// <param name="startOffsetInBytes">The zero-based UTF-8 byte offset where matching starts.</param>
    /// <param name="matchOptions">The per-operation match options.</param>
    /// <param name="cancellationToken">The token retained through validation and all matching work.</param>
    /// <exception cref="OperationCanceledException">Cancellation was requested.</exception>
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
        var invocation = Pcre2SliceDriver.CreateInvocation(_program.Request, cancellationToken);
        try
        {
            Pcre2SliceDriver.ThrowIfSliceBreached(Utf8ValidatedInput.TryCreate(input, ref invocation, out var subject));
            var start = subject.GetBytePosition(startOffsetInBytes, nameof(startOffsetInBytes));
            if (Pcre2Runner.TryMatch(_program, ref subject, start, matchOptions, ref invocation, out var directMatch))
            {
                if (!invocation.IsUnmetered)
                {
                    invocation.Budget.ThrowIfCancellationRequested();
                    invocation.Budget.CheckDeadline();
                    invocation.Budget.ThrowIfCancellationRequested();
                }
                return Utf8Pcre2ValueMatch.Create(input, directMatch);
            }
            throw CreateUnsupportedPatternException();
        }
        catch (Utf8ExecutionDeadlineExpiredException)
        {
            throw new Pcre2MatchException("The PCRE2 match deadline expired.", Pcre2ErrorKind.Timeout);
        }
    }

    /// <summary>Finds the first match using an explicit start byte offset and per-operation options, observing cancellation during validation, matching and required range projection.</summary>
    /// <param name="input">The well-formed UTF-8 subject to match against.</param>
    /// <param name="startOffsetInBytes">The zero-based UTF-8 byte offset where matching starts.</param>
    /// <param name="matchOptions">The per-operation match options.</param>
    /// <param name="cancellationToken">The token to observe before entry and during bounded validation and matching.</param>
    /// <exception cref="OperationCanceledException">Cancellation was requested.</exception>
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
        var invocation = Pcre2SliceDriver.CreateInvocation(_program.Request, cancellationToken);
        try
        {
            Pcre2SliceDriver.ThrowIfSliceBreached(Utf8ValidatedInput.TryCreate(input, ref invocation, out var subject));
            var start = subject.GetBytePosition(startOffsetInBytes, nameof(startOffsetInBytes));
            return MatchDetailedValidated(input, ref subject, start, matchOptions, ref invocation);
        }
        catch (Utf8ExecutionDeadlineExpiredException)
        {
            throw new Pcre2MatchException("The PCRE2 match deadline expired.", Pcre2ErrorKind.Timeout);
        }
    }

    // Probe's normal fallback reuses this already validated subject/authority.
    private Utf8Pcre2MatchContext MatchDetailedValidated(ReadOnlySpan<byte> input,
        scoped ref Utf8ValidatedInput subject, Utf8BytePosition start,
        Pcre2MatchOptions matchOptions, scoped ref Utf8SliceInvocation invocation)
    {
        if (Pcre2Runner.TryMatchDetailed(
                _program,
                ref subject,
                start,
                matchOptions,
                ref invocation,
                out var directGroups,
                out var directMark))
        {
            return directGroups.Length != 0 || directMark is not null
                ? Utf8Pcre2MatchContext.Create(input, directGroups,
                    NameEntries, directMark)
                : default;
        }

        throw CreateUnsupportedPatternException();
    }

    /// <summary>Finds the first detailed match using an explicit start byte offset and per-operation options, observing cancellation during validation, matching and required capture projection.</summary>
    /// <param name="input">The well-formed UTF-8 subject to match against.</param>
    /// <param name="startOffsetInBytes">The zero-based UTF-8 byte offset where matching starts.</param>
    /// <param name="matchOptions">The per-operation match options.</param>
    /// <param name="cancellationToken">The token to observe before entry and during bounded validation and matching.</param>
    /// <exception cref="OperationCanceledException">Cancellation was requested.</exception>
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
        var invocation = Pcre2SliceDriver.CreateInvocation(_program.Request, cancellationToken);
        try
        {
            Pcre2SliceDriver.ThrowIfSliceBreached(Utf8ValidatedInput.TryCreate(input, ref invocation, out var subject));
            var start = subject.GetBytePosition(startOffsetInBytes, nameof(startOffsetInBytes));
            return EnumerateMatchesValidated(input, subject, start, matchOptions, invocation);
        }
        catch (Utf8ExecutionDeadlineExpiredException)
        {
            throw new Pcre2MatchException("The PCRE2 match deadline expired.", Pcre2ErrorKind.Timeout);
        }
    }

    private Utf8Pcre2ValueMatchEnumerator EnumerateMatchesValidated(ReadOnlySpan<byte> input,
        Utf8ValidatedInput subject, Utf8BytePosition start, Pcre2MatchOptions matchOptions,
        Utf8SliceInvocation invocation)
    {
        var budget = new Pcre2ResourceBudget(_program.Request.DefaultLimits, invocation, false);
        if (Pcre2GlobalOperationDriver.TryCreateCursor(_program, subject, start, matchOptions, out var directCursor, budget))
            return new Utf8Pcre2ValueMatchEnumerator(input, directCursor);
        return EnumerateMatchesViaConfiguredBackend(subject, start, matchOptions, invocation);
    }

    private static NotSupportedException CreateUnsupportedPatternException() =>
        new("SPEC-PCRE2 does not support this pattern in the managed profile.");

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private Utf8Pcre2ValueMatchEnumerator EnumerateMatchesViaConfiguredBackend(
        Utf8ValidatedInput input,
        Utf8BytePosition start,
        Pcre2MatchOptions matchOptions,
        Utf8SliceInvocation invocation)
    {
        if (matchOptions == Pcre2MatchOptions.None &&
            _program.Operations.Enumerate is Pcre2SliceDirectProgram slice)
        {
            return new Utf8Pcre2ValueMatchEnumerator(
                input.Bytes,
                new Pcre2SliceGlobalMatchCursor(
                    slice.SliceProgram,
                    invocation,
                    input,
                    start,
                    _program.Request));
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
                new Pcre2ResourceBudget(_program.Request.DefaultLimits, invocation, false));
            return new Utf8Pcre2ValueMatchEnumerator(input.Bytes, fallbackCursor);
        }

        return default;
    }

    /// <summary>Creates a match cursor using an explicit start byte offset and per-operation options, retaining cancellation and cumulative limits through validation and every advance.</summary>
    /// <param name="input">The well-formed UTF-8 subject to match against.</param>
    /// <param name="startOffsetInBytes">The zero-based UTF-8 byte offset where matching starts.</param>
    /// <param name="matchOptions">The per-operation match options.</param>
    /// <param name="cancellationToken">The token retained during validation and cursor advances, including copies.</param>
    /// <exception cref="OperationCanceledException">Cancellation was requested.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Utf8Pcre2ValueMatchEnumerator EnumerateMatches(ReadOnlySpan<byte> input, int startOffsetInBytes, Pcre2MatchOptions matchOptions, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return EnumerateMatchesCore(input, startOffsetInBytes, matchOptions, cancellationToken);
    }

    /// <summary>Probes the UTF-8 subject for a full or permitted subject-end partial match.</summary>
    /// <remarks>Outside literal/curated partial drivers, only proven generic Soft full matches are supported. None uses ordinary detailed matching.</remarks>
    /// <exception cref="NotSupportedException">The selected partial result cannot be established for this pattern.</exception>
    public Utf8Pcre2ProbeResult Probe(ReadOnlySpan<byte> input, Pcre2PartialMode partialMode)
        => Probe(input, partialMode, 0, Pcre2MatchOptions.None);

    /// <summary>Probes for a full or partial match at or after a scalar-aligned UTF-8 byte offset.</summary>
    public Utf8Pcre2ProbeResult Probe(ReadOnlySpan<byte> input, Pcre2PartialMode partialMode, int startOffsetInBytes)
        => Probe(input, partialMode, startOffsetInBytes, Pcre2MatchOptions.None);

    /// <summary>Probes for a full or partial match using an explicit start byte offset and per-operation options.</summary>
    public Utf8Pcre2ProbeResult Probe(ReadOnlySpan<byte> input, Pcre2PartialMode partialMode, int startOffsetInBytes, Pcre2MatchOptions matchOptions)
        => ProbeCore(input, partialMode, startOffsetInBytes, matchOptions, default);

    private Utf8Pcre2ProbeResult ProbeCore(ReadOnlySpan<byte> input, Pcre2PartialMode partialMode,
        int startOffsetInBytes, Pcre2MatchOptions matchOptions, CancellationToken cancellationToken)
    {
        // PCRE2 10.47 rejects partial matching with compile- or match-time
        // ENDANCHORED before engine selection. Keep the same closed failure.
        if (partialMode is Pcre2PartialMode.Soft or Pcre2PartialMode.Hard &&
            ((matchOptions & Pcre2MatchOptions.EndAnchored) != 0 ||
             (_program.Request.Options & Pcre2CompileOptions.EndAnchored) != 0))
            throw new Pcre2MatchException("Partial matching cannot be combined with EndAnchored.", Pcre2ErrorKind.BadOption);
        var invocation = Pcre2SliceDriver.CreateInvocation(_program.Request, cancellationToken);
        try
        {
            Pcre2SliceDriver.ThrowIfSliceBreached(Utf8ValidatedInput.TryCreate(input, ref invocation, out var subject));
            var start = subject.GetBytePosition(startOffsetInBytes, nameof(startOffsetInBytes));
            if (_program.Operations.Match is Pcre2LiteralDirectProgram literalProgram)
            {
                var budget = new Pcre2ResourceBudget(_program.Request.DefaultLimits, invocation, false);
                return Pcre2LiteralProbeRunner.Probe(input, literalProgram.Program, subject,
                    start, partialMode, matchOptions, ref budget);
            }

            // Normal detailed matching handles explicit non-partial flags and
            // compiled end anchoring, including later candidates/alternatives.
            if (_program.PartialProbe.Kind != Pcre2PartialProbeProgramKind.None &&
                (partialMode != Pcre2PartialMode.None || matchOptions == Pcre2MatchOptions.None &&
                 (_program.Request.Options & Pcre2CompileOptions.EndAnchored) == 0))
            {
                var budget = new Pcre2ResourceBudget(_program.Request.DefaultLimits, invocation, false);
                var runner = new Pcre2PartialProbeRunner(subject, _program, NameEntries, matchOptions, budget);
                return runner.Probe(partialMode, startOffsetInBytes, _program, matchOptions);
            }

            if (partialMode == Pcre2PartialMode.None)
                return MatchDetailedValidated(input, ref subject, start, matchOptions, ref invocation).AsProbeResult();

            // Ordinary matching can prove a Soft full result, but cannot decide
            // Hard partial priority or infer absence of a partial from MARK.
            if (partialMode == Pcre2PartialMode.Soft &&
                _program.Operations.Match is Pcre2BacktrackingDirectProgram or Pcre2SliceDirectProgram &&
                Pcre2Runner.TryMatchDetailed(_program, ref subject, start, matchOptions,
                    ref invocation, out var directGroups, out var directMark))
            {
                if (directGroups.Length != 0)
                    return Utf8Pcre2ProbeResult.CreateFullMatch(input, directGroups, NameEntries, directMark);
            }

            throw CreateUnsupportedProbeException();
        }
        catch (Utf8ExecutionDeadlineExpiredException)
        {
            throw new Pcre2MatchException("The PCRE2 match deadline expired.", Pcre2ErrorKind.Timeout);
        }
    }

    /// <summary>Probes for a full or partial match using an explicit start byte offset and per-operation options, observing live cancellation during execution and required result shaping.</summary>
    /// <param name="input">The well-formed UTF-8 subject to match against.</param>
    /// <param name="partialMode">The permitted subject-end partial-match mode.</param>
    /// <param name="startOffsetInBytes">The zero-based UTF-8 byte offset where matching starts.</param>
    /// <param name="matchOptions">The per-operation match options.</param>
    /// <param name="cancellationToken">The token to observe before entry and during supported metered matching.</param>
    /// <exception cref="OperationCanceledException">Cancellation was requested.</exception>
    /// <exception cref="NotSupportedException">The selected partial result cannot be established for this pattern; only literal/curated partial results and generic Soft full matches are supported.</exception>
    public Utf8Pcre2ProbeResult Probe(ReadOnlySpan<byte> input, Pcre2PartialMode partialMode, int startOffsetInBytes, Pcre2MatchOptions matchOptions, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ProbeCore(input, partialMode, startOffsetInBytes, matchOptions, cancellationToken);
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
        return ReplaceCore(input, replacement, substitutionOptions, startOffsetInBytes, matchOptions, CancellationToken.None, false).Bytes;
    }

    /// <summary>Replaces matches using explicit start, substitution, and per-match options, observing cancellation during validation, matching and output shaping.</summary>
    /// <param name="input">The well-formed UTF-8 subject to match against.</param>
    /// <param name="replacement">The PCRE2 replacement template.</param>
    /// <param name="startOffsetInBytes">The zero-based UTF-8 byte offset where matching starts.</param>
    /// <param name="substitutionOptions">The substitution options.</param>
    /// <param name="matchOptions">The per-operation match options.</param>
    /// <param name="cancellationToken">The original token retained through validation, matching and output shaping.</param>
    /// <exception cref="OperationCanceledException">Cancellation was requested.</exception>
    public byte[] Replace(ReadOnlySpan<byte> input, string replacement, int startOffsetInBytes, Pcre2SubstitutionOptions substitutionOptions, Pcre2MatchOptions matchOptions, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ReplaceCore(input, replacement, substitutionOptions, startOffsetInBytes, matchOptions, cancellationToken, false).Bytes;
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
        return ReplaceUtf8TemplateCore(input, replacementPatternUtf8, substitutionOptions, startOffsetInBytes, matchOptions, default).Bytes;
    }

    /// <summary>Replaces matches using a UTF-8 template with explicit start, substitution, and per-match options, observing cancellation during validation, matching and output shaping.</summary>
    /// <param name="input">The well-formed UTF-8 subject to match against.</param>
    /// <param name="replacementPatternUtf8">The PCRE2 replacement template encoded as well-formed UTF-8.</param>
    /// <param name="startOffsetInBytes">The zero-based UTF-8 byte offset where matching starts.</param>
    /// <param name="substitutionOptions">The substitution options.</param>
    /// <param name="matchOptions">The per-operation match options.</param>
    /// <param name="cancellationToken">The original token retained through validation, matching and output shaping.</param>
    /// <exception cref="OperationCanceledException">Cancellation was requested.</exception>
    public byte[] Replace(ReadOnlySpan<byte> input, ReadOnlySpan<byte> replacementPatternUtf8, int startOffsetInBytes, Pcre2SubstitutionOptions substitutionOptions, Pcre2MatchOptions matchOptions, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ReplaceUtf8TemplateCore(input, replacementPatternUtf8, substitutionOptions, startOffsetInBytes, matchOptions, cancellationToken).Bytes;
    }

    /// <summary>Replaces matches through a stateful callback that writes UTF-8 replacement content.</summary>
    public byte[] Replace<TState>(ReadOnlySpan<byte> input, TState state, Pcre2MatchEvaluator<TState> evaluator)
        => Replace(input, state, evaluator, 0, Pcre2MatchOptions.None);

    /// <summary>Replaces matches through a UTF-8 callback using explicit start and per-match options.</summary>
    public byte[] Replace<TState>(ReadOnlySpan<byte> input, TState state, Pcre2MatchEvaluator<TState> evaluator, int startOffsetInBytes, Pcre2MatchOptions matchOptions)
        => ReplaceCallback(input, state, new Pcre2ByteCallback<TState>(evaluator), startOffsetInBytes, matchOptions,
            CancellationToken.None, false).Bytes;

    /// <summary>Replaces matches through a UTF-8 callback using explicit start and per-match options, observing cancellation during validation, matching and output shaping.</summary>
    /// <param name="input">The well-formed UTF-8 subject to match against.</param>
    /// <param name="state">Additional state passed to the evaluator.</param>
    /// <param name="evaluator">The callback invoked for each match.</param>
    /// <param name="startOffsetInBytes">The zero-based UTF-8 byte offset where matching starts.</param>
    /// <param name="matchOptions">The per-operation match options.</param>
    /// <typeparam name="TState">The type of additional state passed to the evaluator.</typeparam>
    /// <param name="cancellationToken">The original token retained through validation, matching and output shaping.</param>
    /// <exception cref="OperationCanceledException">Cancellation was requested.</exception>
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
        return ReplaceCore(input, replacement, substitutionOptions, startOffsetInBytes, matchOptions, CancellationToken.None, true).Text;
    }

    /// <summary>Replaces matches to UTF-16 text using explicit start, substitution, and per-match options, observing cancellation during validation, matching and output shaping.</summary>
    /// <param name="input">The well-formed UTF-8 subject to match against.</param>
    /// <param name="replacement">The PCRE2 replacement template.</param>
    /// <param name="startOffsetInBytes">The zero-based UTF-8 byte offset where matching starts.</param>
    /// <param name="substitutionOptions">The substitution options.</param>
    /// <param name="matchOptions">The per-operation match options.</param>
    /// <param name="cancellationToken">The original token retained through validation, matching and output shaping.</param>
    /// <exception cref="OperationCanceledException">Cancellation was requested.</exception>
    public string ReplaceToString(ReadOnlySpan<byte> input, string replacement, int startOffsetInBytes, Pcre2SubstitutionOptions substitutionOptions, Pcre2MatchOptions matchOptions, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ReplaceCore(input, replacement, substitutionOptions, startOffsetInBytes, matchOptions, cancellationToken, true).Text;
    }

    /// <summary>Replaces matches through a stateful callback that returns UTF-16 replacement text.</summary>
    public string ReplaceToString<TState>(ReadOnlySpan<byte> input, TState state, Pcre2Utf16MatchEvaluator<TState> evaluator)
        => ReplaceToString(input, state, evaluator, 0, Pcre2MatchOptions.None);

    /// <summary>Replaces matches through a UTF-16 callback using explicit start and per-match options.</summary>
    public string ReplaceToString<TState>(ReadOnlySpan<byte> input, TState state, Pcre2Utf16MatchEvaluator<TState> evaluator, int startOffsetInBytes, Pcre2MatchOptions matchOptions)
        => ReplaceCallback(input, state, new Pcre2StringCallback<TState>(evaluator), startOffsetInBytes, matchOptions,
            CancellationToken.None, true).Text;

    /// <summary>Replaces matches through a UTF-16 callback using explicit start and per-match options, observing cancellation during validation, matching and output shaping.</summary>
    /// <param name="input">The well-formed UTF-8 subject to match against.</param>
    /// <param name="state">Additional state passed to the evaluator.</param>
    /// <param name="evaluator">The callback invoked for each match.</param>
    /// <param name="startOffsetInBytes">The zero-based UTF-8 byte offset where matching starts.</param>
    /// <param name="matchOptions">The per-operation match options.</param>
    /// <typeparam name="TState">The type of additional state passed to the evaluator.</typeparam>
    /// <param name="cancellationToken">The original token retained through validation, matching and output shaping.</param>
    /// <exception cref="OperationCanceledException">Cancellation was requested.</exception>
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
    /// <remarks>Input and replacement may overlap the destination. Matched overlapping input uses admitted temporary output; a short destination is unchanged.</remarks>
    /// <returns><see cref="OperationStatus.Done"/> on success, or <see cref="OperationStatus.DestinationTooSmall"/> when the complete result does not fit.</returns>
    public OperationStatus TryReplace(ReadOnlySpan<byte> input, ReadOnlySpan<byte> replacementPatternUtf8, Span<byte> destination, out int bytesWritten, int startOffsetInBytes, Pcre2SubstitutionOptions substitutionOptions, Pcre2MatchOptions matchOptions)
        => TryReplaceCore(input, replacementPatternUtf8, destination, out bytesWritten, startOffsetInBytes, substitutionOptions, matchOptions, default);

    private OperationStatus TryReplaceCore(ReadOnlySpan<byte> input, ReadOnlySpan<byte> replacementPatternUtf8,
        Span<byte> destination, out int bytesWritten, int startOffsetInBytes, Pcre2SubstitutionOptions substitutionOptions,
        Pcre2MatchOptions matchOptions, CancellationToken token)
    {
        var invocation = Pcre2SliceDriver.CreateInvocation(_program.Request, token);
        using var preparationControl = Pcre2ReplacementPreparationControl.Borrow(invocation, token);
        ulong templateReservation = 0;
        try
        {
            Pcre2SliceDriver.ThrowIfSliceBreached(Utf8ValidatedInput.TryCreate(replacementPatternUtf8, ref invocation, out var template));
            Pcre2SliceDriver.ThrowIfSliceBreached(Utf8ValidatedInput.TryCreate(input, ref invocation, out var subject));
            var start = subject.GetBytePosition(startOffsetInBytes, nameof(startOffsetInBytes));
            var replacementText = DecodeReplacementTemplate(replacementPatternUtf8, template.Utf16Length,
                ref invocation, out templateReservation);
            Pcre2ReplacementPreparation? preparation = null;
            if (TryReplaceUsingLiteralDriver(input, subject, start, replacementText, substitutionOptions,
                    matchOptions, invocation, destination, ref preparation, out bytesWritten, out var status))
            {
                Pcre2SliceDriver.CheckInvocation(ref invocation);
                return status;
            }

            var replaced = ReplaceValidated(input, subject, start, replacementText, substitutionOptions,
                matchOptions, invocation, false, preparation).Bytes;
            if (replaced.Length > destination.Length)
            {
                bytesWritten = (substitutionOptions & Pcre2SubstitutionOptions.SubstituteOverflowLength) != 0
                    ? replaced.Length : 0;
                Pcre2SliceDriver.CheckInvocation(ref invocation);
                return OperationStatus.DestinationTooSmall;
            }
            Utf8SliceBufferWork.Copy(replaced, destination, ref invocation);
            bytesWritten = replaced.Length;
            Pcre2SliceDriver.CheckInvocation(ref invocation);
            return OperationStatus.Done;
        }
        catch (Utf8ExecutionDeadlineExpiredException)
        { throw new Pcre2MatchException("The PCRE2 match deadline expired.", Pcre2ErrorKind.Timeout); }
        catch (InvalidOperationException) when (invocation.Budget.LimitBreach != Utf8ExecutionLimitKind.None)
        { Pcre2SliceDriver.ThrowIfSliceBreached(invocation.Budget.LimitBreach); throw; }
        finally { invocation.Ledger.Release(templateReservation); }
    }

    /// <summary>Attempts to write a replacement result, observing cancellation during validation, preparation, discovery and copying.</summary>
    /// <remarks>Input and replacement may overlap the destination. Matched overlapping input uses admitted temporary output; a short destination is unchanged.</remarks>
    /// <param name="input">The well-formed UTF-8 subject to match against.</param>
    /// <param name="replacementPatternUtf8">The PCRE2 replacement template encoded as well-formed UTF-8.</param>
    /// <param name="destination">The destination buffer for the UTF-8 replacement result.</param>
    /// <param name="bytesWritten">When this method returns, the number of bytes written to the destination.</param>
    /// <param name="startOffsetInBytes">The zero-based UTF-8 byte offset where matching starts.</param>
    /// <param name="substitutionOptions">The substitution options.</param>
    /// <param name="matchOptions">The per-operation match options.</param>
    /// <param name="cancellationToken">The original token retained through the operation.</param>
    /// <exception cref="OperationCanceledException">Cancellation was requested; destination bytes copied before interruption may remain.</exception>
    public OperationStatus TryReplace(ReadOnlySpan<byte> input, ReadOnlySpan<byte> replacementPatternUtf8, Span<byte> destination, out int bytesWritten, int startOffsetInBytes, Pcre2SubstitutionOptions substitutionOptions, Pcre2MatchOptions matchOptions, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return TryReplaceCore(input, replacementPatternUtf8, destination, out bytesWritten, startOffsetInBytes, substitutionOptions, matchOptions, cancellationToken);
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
        var count = 0;
        foreach (var entry in NameEntries)
        {
            if (string.IsNullOrEmpty(entry.Name) || !string.Equals(entry.Name, name, StringComparison.Ordinal))
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

    /// <summary>Attempts to find the first participating named slot using explicit start and per-match options, observing cancellation during validation, matching and capture publication.</summary>
    /// <param name="input">The well-formed UTF-8 subject to match against.</param>
    /// <param name="name">The capture group name.</param>
    /// <param name="group">When this method returns, the first participating capture slot.</param>
    /// <param name="startOffsetInBytes">The zero-based UTF-8 byte offset where matching starts.</param>
    /// <param name="matchOptions">The per-operation match options.</param>
    /// <param name="cancellationToken">The original token retained through detailed matching and group publication.</param>
    /// <exception cref="OperationCanceledException">Cancellation was requested.</exception>
    public bool TryGetFirstSetGroup(ReadOnlySpan<byte> input, string name, out Utf8Pcre2GroupContext group, int startOffsetInBytes, Pcre2MatchOptions matchOptions, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var context = MatchDetailedCore(input, startOffsetInBytes, matchOptions, cancellationToken);
        var found = context.TryGetFirstSetGroup(name, out group);
        cancellationToken.ThrowIfCancellationRequested();
        return found;
    }

    /// <summary>Copies successive non-overlapping match snapshots into a caller-provided destination.</summary>
    /// <remarks>Input and destination storage must be disjoint. Overlap is unsupported and is not checked.</remarks>
    /// <returns>The number of matches copied; <paramref name="isMore"/> reports whether another match is available.</returns>
    public int MatchMany(ReadOnlySpan<byte> input, Span<Utf8Pcre2MatchData> destination, out bool isMore)
        => MatchMany(input, destination, out isMore, 0, Pcre2MatchOptions.None);

    /// <summary>Copies successive match snapshots using explicit start and per-match options.</summary>
    /// <remarks>Input and destination storage must be disjoint. Overlap is unsupported and is not checked.</remarks>
    /// <returns>The number of matches copied; <paramref name="isMore"/> reports whether another match is available.</returns>
    public int MatchMany(ReadOnlySpan<byte> input, Span<Utf8Pcre2MatchData> destination, out bool isMore, int startOffsetInBytes, Pcre2MatchOptions matchOptions)
        => MatchManyCore(input, destination, out isMore, startOffsetInBytes, matchOptions, default);

    private int MatchManyCore(ReadOnlySpan<byte> input, Span<Utf8Pcre2MatchData> destination, out bool isMore,
        int startOffsetInBytes, Pcre2MatchOptions matchOptions, CancellationToken token)
    {
        ThrowIfGenericIterationMayBeNonMonotone();
        var invocation = Pcre2SliceDriver.CreateInvocation(_program.Request, token);
        try
        {
            Pcre2SliceDriver.ThrowIfSliceBreached(Utf8ValidatedInput.TryCreate(input, ref invocation, out var subject));
            var start = subject.GetBytePosition(startOffsetInBytes, nameof(startOffsetInBytes));
            if (Pcre2GlobalOperationDriver.TryCreateCursor(
                    _program,
                    subject,
                    start,
                    matchOptions,
                    out var directCursor,
                    new Pcre2ResourceBudget(_program.Request.DefaultLimits, invocation, false)))
            {
                var written = 0;
                while (written < destination.Length && directCursor.MoveNext())
                {
                    Pcre2SliceDriver.CheckInvocation(ref invocation);
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
                    Pcre2SliceDriver.CheckInvocation(ref invocation);
                    return written;
                }

                isMore = directCursor.MoveNext();
                Pcre2SliceDriver.CheckInvocation(ref invocation);
                return written;
            }


            var enumerator = EnumerateMatchesValidated(input, subject, start, matchOptions, invocation);
            var fallbackWritten = MatchManyViaEnumerateMatches(enumerator, destination, out isMore, ref invocation);
            Pcre2SliceDriver.CheckInvocation(ref invocation);
            return fallbackWritten;
        }
        catch (Utf8ExecutionDeadlineExpiredException)
        {
            throw new Pcre2MatchException("The PCRE2 match deadline expired.", Pcre2ErrorKind.Timeout);
        }
    }

    private static int MatchManyViaEnumerateMatches(Utf8Pcre2ValueMatchEnumerator enumerator, Span<Utf8Pcre2MatchData> destination, out bool isMore, scoped ref Utf8SliceInvocation invocation)
    {
        if (destination.IsEmpty)
        {
            isMore = enumerator.MoveNext();
            return 0;
        }

        var written = 0;
        while (written < destination.Length && enumerator.MoveNext())
        {
            var current = enumerator.Current;
            Pcre2SliceDriver.CheckInvocation(ref invocation);
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

    /// <summary>Copies successive match snapshots using explicit start and per-match options, observing cancellation during validation, discovery and copying.</summary>
    /// <remarks>Input and destination storage must be disjoint. Overlap is unsupported and is not checked.</remarks>
    /// <param name="input">The well-formed UTF-8 subject to match against.</param>
    /// <param name="destination">The destination for match snapshots.</param>
    /// <param name="isMore">When this method returns, whether additional matches remain.</param>
    /// <param name="startOffsetInBytes">The zero-based UTF-8 byte offset where matching starts.</param>
    /// <param name="matchOptions">The per-operation match options.</param>
    /// <param name="cancellationToken">The original token retained through validation, discovery and destination writes.</param>
    /// <exception cref="OperationCanceledException">Cancellation was requested; an already-written destination prefix remains valid.</exception>
    public int MatchMany(ReadOnlySpan<byte> input, Span<Utf8Pcre2MatchData> destination, out bool isMore, int startOffsetInBytes, Pcre2MatchOptions matchOptions, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return MatchManyCore(input, destination, out isMore, startOffsetInBytes, matchOptions, cancellationToken);
    }

    /// <summary>Returns the compiled expression's execution and result-shaping characteristics.</summary>
    public Utf8Pcre2Analysis Analyze()
    {
        var backtracking = (_program.Operations.Match as Pcre2BacktrackingDirectProgram)?.Program;
        var literal = (_program.Operations.Match as Pcre2LiteralDirectProgram)?.Program;
        return new Utf8Pcre2Analysis
        {
            // R05: every selectable program kind is owned; no BCL-direct kind remains.
            IsFullyNative = true,
            IsExactLiteral = literal is not null,
            MinRequiredLengthInBytes = _program.Operations.Match switch
            {
                Pcre2LiteralDirectProgram program => program.Program.LiteralUtf8.Length,
                Pcre2CharacterDirectProgram program => program.Program.MinimumByteLength,
                Pcre2SingleTokenRepeatDirectProgram program => program.Program.Fallback.AnalyzedMinimumByteLength,
                Pcre2BacktrackingDirectProgram program => program.Program.AnalyzedMinimumByteLength,
                Pcre2SliceDirectProgram program => program.Fallback.AnalyzedMinimumByteLength,
                _ => 0,
            },
            HasDuplicateNames = NameEntries.GroupBy(static entry => entry.Name, StringComparer.Ordinal).Any(static group => group.Count() > 1),
            UsesBranchReset = _program.UsesBranchReset,
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

    private bool TryReplaceUsingLiteralDriver(
        ReadOnlySpan<byte> input,
        Utf8ValidatedInput subject,
        Utf8BytePosition start,
        string replacement,
        Pcre2SubstitutionOptions substitutionOptions,
        Pcre2MatchOptions matchOptions,
        Utf8SliceInvocation invocation,
        bool text,
        ref Pcre2ReplacementPreparation? preparation,
        out Pcre2ReplacementResult result)
    {
        if (!TryGetLiteralDriverReplacementPlan(
                replacement,
                substitutionOptions,
                ref preparation,
                out var plan,
                out var replacementOnly))
        {
            result = default;
            return false;
        }

        if (!Pcre2GlobalOperationDriver.TryCreateCursor(
                _program,
                subject,
                start,
                matchOptions,
                out var cursor,
                new Pcre2ResourceBudget(_program.Request.DefaultLimits,
                    invocation, collectDiagnostics: false)))
        {
            result = default;
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
                var bytes = Utf8InvocationOutput.AllocateBytes(replacementOnly ? 0 : input.Length, cursor.WorkingBufferControl, out outputReservation);
                if (!replacementOnly) Utf8InvocationOutput.Copy(input, bytes, cursor.WorkingBufferControl);
                result = text ? new(cursor.WorkingBufferControl.DecodeReplacementText(bytes), []) : new(string.Empty, bytes);
                return true;
            }

            var output = Utf8InvocationOutput.AllocateBytes(outputLength.Value, cursor.WorkingBufferControl, out outputReservation);
            EmitLiteralDriverReplacement(input, plan, replacementOnly, output, ledger.WrittenRanges, cursor.WorkingBufferControl);
            result = text ? new(cursor.WorkingBufferControl.DecodeReplacementText(output), []) : new(string.Empty, output);
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
        Utf8ValidatedInput subject,
        Utf8BytePosition start,
        string replacement,
        Pcre2SubstitutionOptions substitutionOptions,
        Pcre2MatchOptions matchOptions,
        Utf8SliceInvocation invocation,
        Span<byte> destination,
        ref Pcre2ReplacementPreparation? preparation,
        out int bytesWritten,
        out OperationStatus status)
    {
        if (!TryGetLiteralDriverReplacementPlan(
                replacement,
                substitutionOptions,
                ref preparation,
                out var plan,
                out var replacementOnly))
        {
            bytesWritten = 0;
            status = default;
            return false;
        }

        if (!Pcre2GlobalOperationDriver.TryCreateCursor(
                _program,
                subject,
                start,
                matchOptions,
                out var cursor,
                new Pcre2ResourceBudget(_program.Request.DefaultLimits, invocation, false)))
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

            if (input.Overlaps(destination))
            {
                // Memmove of each fragment cannot protect input read by later fragments.
                // Keep staged output admitted through the final bounded copy.
                ulong reservation = 0;
                try
                {
                    var output = Utf8InvocationOutput.AllocateBytes(outputLength.Value, cursor.WorkingBufferControl, out reservation);
                    EmitLiteralDriverReplacement(input, plan, replacementOnly, output, ledger.WrittenRanges, cursor.WorkingBufferControl);
                    Utf8InvocationOutput.Copy(output, destination, cursor.WorkingBufferControl);
                }
                finally { cursor.WorkingBufferControl.ReleaseWorking(reservation); }
            }
            else
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
        ref Pcre2ReplacementPreparation? preparation,
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

            preparation ??= Pcre2ReplacementPreparationControl.Prepare(replacement, substitutionOptions, true);
            plan = preparation.Value.Simple
                ?? throw new InvalidOperationException("Literal preparation did not produce a simple plan.");
            return true;
        }

        var templateOptions = substitutionOptions &
            ~(Pcre2SubstitutionOptions.SubstituteOverflowLength |
              Pcre2SubstitutionOptions.SubstituteReplacementOnly |
              Pcre2SubstitutionOptions.SubstituteMatched);
        // A declined simple optimization still produces a complete prepared plan.
        // Carry it into the executor even when this template cannot be cached.
        preparation ??= GetReplacementPreparation(replacement, templateOptions);
        // Execution-only flags still retain their established streaming route.
        // Sharing syntax preparation must not opt those calls into the range ledger.
        if (templateOptions != Pcre2SubstitutionOptions.None || preparation.Value.Simple is not { } simplePlan)
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

    private readonly record struct Pcre2ReplacementResult(string Text, byte[] Bytes);

    private Pcre2ReplacementResult ReplaceCore(
        ReadOnlySpan<byte> input,
        string replacement,
        Pcre2SubstitutionOptions substitutionOptions,
        int startOffsetInBytes,
        Pcre2MatchOptions matchOptions,
        CancellationToken cancellationToken, bool text)
    {
        var invocation = Pcre2SliceDriver.CreateInvocation(_program.Request, cancellationToken);
        using var preparationControl = Pcre2ReplacementPreparationControl.Borrow(invocation, cancellationToken);
        try
        {
            Pcre2SliceDriver.ThrowIfSliceBreached(Utf8ValidatedInput.TryCreate(input, ref invocation, out var subject));
            var start = subject.GetBytePosition(startOffsetInBytes, nameof(startOffsetInBytes));
            var result = ReplaceValidated(input, subject, start, replacement, substitutionOptions, matchOptions, invocation, text);
            Pcre2SliceDriver.CheckInvocation(ref invocation);
            return result;
        }
        catch (Utf8ExecutionDeadlineExpiredException)
        { throw new Pcre2MatchException("The PCRE2 match deadline expired.", Pcre2ErrorKind.Timeout); }
    }

    private Pcre2ReplacementResult ReplaceUtf8TemplateCore(ReadOnlySpan<byte> input, ReadOnlySpan<byte> replacement,
        Pcre2SubstitutionOptions options, int startOffsetInBytes, Pcre2MatchOptions matchOptions, CancellationToken token)
    {
        var invocation = Pcre2SliceDriver.CreateInvocation(_program.Request, token);
        using var preparationControl = Pcre2ReplacementPreparationControl.Borrow(invocation, token);
        ulong reservation = 0;
        try
        {
            Pcre2SliceDriver.ThrowIfSliceBreached(Utf8ValidatedInput.TryCreate(replacement, ref invocation, out var template));
            Pcre2SliceDriver.ThrowIfSliceBreached(Utf8ValidatedInput.TryCreate(input, ref invocation, out var subject));
            var start = subject.GetBytePosition(startOffsetInBytes, nameof(startOffsetInBytes));
            var decoded = DecodeReplacementTemplate(replacement, template.Utf16Length, ref invocation, out reservation);
            var result = ReplaceValidated(input, subject, start, decoded, options, matchOptions, invocation, false);
            Pcre2SliceDriver.CheckInvocation(ref invocation);
            return result;
        }
        catch (Utf8ExecutionDeadlineExpiredException)
        { throw new Pcre2MatchException("The PCRE2 match deadline expired.", Pcre2ErrorKind.Timeout); }
        catch (InvalidOperationException) when (invocation.Budget.LimitBreach != Utf8ExecutionLimitKind.None)
        { Pcre2SliceDriver.ThrowIfSliceBreached(invocation.Budget.LimitBreach); throw; }
        finally { invocation.Ledger.Release(reservation); }
    }

    private static string DecodeReplacementTemplate(ReadOnlySpan<byte> bytes, int charCount,
        scoped ref Utf8SliceInvocation invocation, out ulong reservation)
    {
        reservation = 0;
        // Preparation scratch stays admitted while discovery/output overlap;
        // it is not publication and does not consume result-byte credits.
        var capacity = Utf8ConstructionBudget.StringBytes(charCount);
        Pcre2SliceDriver.ThrowIfSliceBreached(invocation.TryReserveWorking(capacity));
        reservation = capacity;
        return invocation.IsUnmetered ? Encoding.UTF8.GetString(bytes)
            : Utf8SliceBufferWork.DecodeUtf8(bytes, charCount, ref invocation);
    }

    private Pcre2ReplacementResult ReplaceValidated(ReadOnlySpan<byte> input, Utf8ValidatedInput subject, Utf8BytePosition start, string replacement,
        Pcre2SubstitutionOptions substitutionOptions,
        Pcre2MatchOptions matchOptions, Utf8SliceInvocation invocation, bool text,
        Pcre2ReplacementPreparation? preparation = null)
    {
        if (RejectsReplacementIteration())
        {
            throw new NotSupportedException("SPEC-PCRE2 rejects replacement for lookaround-\\K iterative matches.");
        }

        if (TryReplaceUsingLiteralDriver(
                input,
                subject,
                start,
                replacement,
                substitutionOptions,
                matchOptions,
                invocation,
                text,
                ref preparation,
                out var genericResult))
        {
            return genericResult;
        }

        if (_program.Operations.Replace is Pcre2BacktrackingDirectProgram backtrackingProgram)
        {
            return ReplaceBacktrackingDetailed(
                input, subject, start,
                backtrackingProgram.Program,
                replacement,
                substitutionOptions,
                matchOptions, invocation, text, preparation);
        }

        if (_program.Operations.Replace is Pcre2SliceDirectProgram sliceProgram)
        {
            if (matchOptions != Pcre2MatchOptions.None)
            {
                return ReplaceBacktrackingDetailed(
                    input, subject, start,
                    sliceProgram.Fallback,
                    replacement,
                    substitutionOptions,
                    matchOptions, invocation, text, preparation);
            }

            return ReplaceSliceDetailed(
                input, subject, start,
                sliceProgram.SliceProgram,
                replacement,
                substitutionOptions,
                invocation, text, preparation);
        }

        return ReplaceNativeValueTemplate(input, subject, start, replacement, substitutionOptions,
            matchOptions, invocation, text, preparation);
    }

    // Extended templates on capture-free native routes keep the existing
    // global cursor/kernel. Reuse one admitted group-zero slot and lend the
    // discovery authority to the writer; no per-match core rematching.
    private Pcre2ReplacementResult ReplaceNativeValueTemplate(ReadOnlySpan<byte> input, Utf8ValidatedInput subject, Utf8BytePosition start, string replacement,
        Pcre2SubstitutionOptions options, Pcre2MatchOptions matchOptions,
        Utf8SliceInvocation invocation, bool text, Pcre2ReplacementPreparation? prepared)
    {
        if (!Pcre2GlobalOperationDriver.TryCreateCursor(_program, subject, start, matchOptions, out var cursor,
            new Pcre2ResourceBudget(_program.Request.DefaultLimits,
                invocation, collectDiagnostics: false)))
            throw CreateUnsupportedReplacementException();
        var control = cursor.WorkingBufferControl;
        var replacementOnly = (options & Pcre2SubstitutionOptions.SubstituteReplacementOnly) != 0;
        if (!cursor.MoveNext())
            return text ? new(Pcre2ReplacementTemplateWriter.DecodeUnchanged(replacementOnly ? [] : input, ref control.WorkingInvocation), [])
                : new(string.Empty, Pcre2ReplacementTemplateWriter.CopyUnchanged(replacementOnly ? [] : input, ref control.WorkingInvocation));
        var templateOptions = options & ~Pcre2SubstitutionOptions.SubstituteReplacementOnly;
        var preparation = prepared ?? ((options & Pcre2SubstitutionOptions.SubstituteLiteral) != 0
            ? Pcre2ReplacementPreparationControl.Prepare(replacement, options, true)
            : GetReplacementPreparation(replacement, templateOptions));
        control.WorkingInvocation.ShareStorageForCursor();
        var groupBytes = Utf8ConstructionBudget.ArrayBytes<Pcre2GroupData>(1);
        control.TryChargeResultBytes(groupBytes);
        control.ReserveWorking(groupBytes);
        var writer = default(Pcre2ReplacementTemplateWriter);
        try
        {
            var groups = new Pcre2GroupData[1];
            writer = new Pcre2ReplacementTemplateWriter(input, NameEntries, control.WorkingInvocation);
            var position = 0;
            do
            {
                groups[0] = cursor.Current;
                var value = groups[0];
                if (!replacementOnly) writer.AppendGap(position, value.StartOffsetInBytes - position);
                writer.AppendTemplate(preparation, templateOptions, groups, value.StartOffsetInBytes,
                    value.EndOffsetInBytes - value.StartOffsetInBytes, null);
                position = value.EndOffsetInBytes;
            } while (cursor.MoveNext());
            if (!replacementOnly) writer.AppendGap(position, input.Length - position);
            return text ? new(writer.CompleteToString(), []) : new(string.Empty, writer.Complete());
        }
        finally { writer.Release(); control.ReleaseWorking(groupBytes); }
    }

    private static NotSupportedException CreateUnsupportedProbeException()
        => new("SPEC-PCRE2 does not support Probe(...) for this pattern.");

    private static NotSupportedException CreateUnsupportedReplacementException(string? message)
        => new(message ?? "SPEC-PCRE2 does not support replacement for this pattern.");

    private static NotSupportedException CreateUnsupportedReplacementException()
        => CreateUnsupportedReplacementException(null);

    private Pcre2ReplacementResult ReplaceBacktrackingDetailed(ReadOnlySpan<byte> input, Utf8ValidatedInput validated, Utf8BytePosition start, Pcre2BacktrackingProgram program,
        string replacement, Pcre2SubstitutionOptions options, Pcre2MatchOptions matchOptions, Utf8SliceInvocation invocation, bool text,
        Pcre2ReplacementPreparation? prepared)
    {
        var cursor = new Pcre2BacktrackingDetailedGlobalMatchCursor(program, _program.CandidateSearchPlan,
            validated, start, matchOptions, _program.Request,
            new Pcre2ResourceBudget(_program.Request.DefaultLimits,
                invocation, collectDiagnostics: false));
        if (!cursor.MoveNext())
        {
            if ((options & Pcre2SubstitutionOptions.SubstituteReplacementOnly) != 0) return new(string.Empty, []);
            return text ? new(Pcre2ReplacementTemplateWriter.DecodeUnchanged(input, ref cursor.WorkingInvocation), [])
                : new(string.Empty, Pcre2ReplacementTemplateWriter.CopyUnchanged(input, ref cursor.WorkingInvocation));
        }
        var replacementOnly = (options & Pcre2SubstitutionOptions.SubstituteReplacementOnly) != 0;
        var templateOptions = options & ~Pcre2SubstitutionOptions.SubstituteReplacementOnly;
        var preparation = prepared ?? ((options & Pcre2SubstitutionOptions.SubstituteLiteral) != 0
            ? Pcre2ReplacementPreparationControl.Prepare(replacement, options, true)
            : GetReplacementPreparation(replacement, templateOptions));
        // Controlled calls already share this owner. Promote unlimited hits
        // before the writer copies it; no-match helpers borrow the inline owner.
        cursor.WorkingInvocation.ShareStorageForCursor();
        var writer = new Pcre2ReplacementTemplateWriter(input, NameEntries, cursor.WorkingInvocation);
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
            return text ? new(writer.CompleteToString(), []) : new(string.Empty, writer.Complete());
        }
        finally { writer.Release(); }
    }

    private Pcre2ReplacementResult ReplaceSliceDetailed(ReadOnlySpan<byte> input, Utf8ValidatedInput validated, Utf8BytePosition start, Utf8SliceProgram sliceProgram,
        string replacement, Pcre2SubstitutionOptions options, Utf8SliceInvocation invocation, bool text,
        Pcre2ReplacementPreparation? prepared)
    {
        var cursor = new Pcre2SliceGlobalMatchCursor(sliceProgram, invocation, validated, start, _program.Request);
        if (!cursor.MoveNext())
        {
            if ((options & Pcre2SubstitutionOptions.SubstituteReplacementOnly) != 0) return new(string.Empty, []);
            return text ? new(Pcre2ReplacementTemplateWriter.DecodeUnchanged(input, ref cursor.WorkingInvocation), [])
                : new(string.Empty, Pcre2ReplacementTemplateWriter.CopyUnchanged(input, ref cursor.WorkingInvocation));
        }
        var replacementOnly = (options & Pcre2SubstitutionOptions.SubstituteReplacementOnly) != 0;
        var templateOptions = options & ~Pcre2SubstitutionOptions.SubstituteReplacementOnly;
        var preparation = prepared ?? ((options & Pcre2SubstitutionOptions.SubstituteLiteral) != 0
            ? Pcre2ReplacementPreparationControl.Prepare(replacement, options, true)
            : GetReplacementPreparation(replacement, templateOptions));
        cursor.WorkingInvocation.ShareStorageForCursor();
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
            return text ? new(writer.CompleteToString(), []) : new(string.Empty, writer.Complete());
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

    private Pcre2ReplacementPreparation GetReplacementPreparation(string replacement, Pcre2SubstitutionOptions options)
    {
        // Only Extended changes template grammar. Execution flags remain with
        // the caller, including its separate literal-driver eligibility above.
        options &= Pcre2SubstitutionOptions.Extended;
        using var control = Pcre2ReplacementPreparationControl.JoinOrEnter(_program.Request.MatchTimeout);
        Pcre2ReplacementPreparationControl.CheckText(replacement.Length);
        Pcre2ReplacementPreparation result;
        try
        {
            result = _replacementCache.GetOrAdd(new Pcre2ReplacementCacheKey(replacement, options), 0,
                static (key, _) => Pcre2ReplacementPreparationControl.Prepare(key.Replacement, key.Options, false),
                default(Pcre2ReplacementTextControl), default(Pcre2ReplacementRetainedStorage));
        }
        catch (Exception failure) when (failure is OperationCanceledException ||
            failure is Pcre2MatchException { ErrorKind: Pcre2ErrorKind.Timeout or Pcre2ErrorKind.MatchLimit or
                Pcre2ErrorKind.DepthLimit or Pcre2ErrorKind.HeapLimit or Pcre2ErrorKind.ResultLimit })
        {
            // Pending entries share their preparer's failure. Our token, deadline
            // and sticky limits remain authoritative: Check rethrows our own
            // failure, otherwise prepare once independently under our allowance.
            // A failed independent preparation is never retried or published.
            Pcre2ReplacementPreparationControl.Check();
            result = Pcre2ReplacementPreparationControl.Prepare(replacement, options, false);
        }
        Pcre2ReplacementPreparationControl.Check();
        return result;
    }

    private readonly record struct Pcre2ReplacementCacheKey(string Replacement, Pcre2SubstitutionOptions Options);

    private readonly struct Pcre2ReplacementRetainedStorage : IUtf8RetainedStorage<Pcre2ReplacementCacheKey, Pcre2ReplacementPreparation>
    {
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
