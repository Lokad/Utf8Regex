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
public sealed partial class Utf8Pcre2Regex : IDisposable
{

    /// <summary>Compiles an expression for caller-valid, unchanged UTF-8 subjects.</summary>
    /// <remarks>A null timeout uses DefaultMatchTimeout. Subject bytes remain caller-valid/unchanged; host text/collections are outside regex controls. Policy construction limits/deadline and optional program funding are independent of matching; Compile additionally accepts a construction token.</remarks>
    public Utf8Pcre2Regex(string pattern, Pcre2CompileOptions options = Pcre2CompileOptions.None, Utf8Pcre2CompileSettings compileSettings = default, Utf8RegexPolicy? policy = null)
        : this(new Utf8ConstructionPattern(pattern), options, compileSettings, policy, default, explicitCompile: false) { }

    /// <summary>Compiles a strict UTF-8 pattern for caller-valid, unchanged UTF-8 subjects.</summary>
    /// <remarks>A null timeout uses DefaultMatchTimeout. Subject bytes remain caller-valid/unchanged; host text/collections are outside regex controls. Policy construction limits/deadline and optional program funding are independent of matching; Compile additionally accepts a construction token.</remarks>
    public Utf8Pcre2Regex(ReadOnlySpan<byte> patternUtf8, Pcre2CompileOptions options = Pcre2CompileOptions.None, Utf8Pcre2CompileSettings compileSettings = default, Utf8RegexPolicy? policy = null)
        : this(new Utf8ConstructionPattern(patternUtf8), options, compileSettings, policy, default, explicitCompile: false) { }

    private readonly Utf8FundedStorage? _programMemory;

    /// <summary>Closes caller-funded program admission after admitted calls and persistent cursors finish.</summary>
    /// <remarks>No-op without funding. Funded span enumerators require an open expression at each advance; previously returned values remain valid. No finalizer is used.</remarks>
    public void Dispose() => _programMemory?.Close();

    private static TimeSpan s_defaultMatchTimeout = Timeout.InfiniteTimeSpan;
    private readonly Pcre2CompiledProgram _program;
    private readonly Pcre2FiniteLiteralLanguageDirectProgram? _unmeteredLiteralLanguage;
    private readonly Utf8BoundedPreparationCache<Pcre2ReplacementCacheKey, Pcre2ReplacementPreparation> _replacementCache;

    // One owner for default inherited construction or explicit policy/funding.
    private Utf8Pcre2Regex(Utf8ConstructionPattern patternSource, Pcre2CompileOptions options,
        Utf8Pcre2CompileSettings compileSettings, Utf8RegexPolicy? policy, CancellationToken token, bool explicitCompile)
    {
        token.ThrowIfCancellationRequested();
        policy ??= Utf8RegexPolicy.Default;
        var defaultExecutionLimits = ToExecutionLimits(policy.ResourcePolicy);
        var validatedTimeout = Utf8MatchTimeout.Validate(policy.MatchTimeout ?? DefaultMatchTimeout, nameof(policy));
        var constructionPolicy = policy.ConstructionPolicy;
        constructionPolicy.Validate(nameof(policy));
        var constructionTimeout = Utf8MatchTimeout.Validate(policy.ConstructionTimeout ?? Timeout.InfiniteTimeSpan, nameof(policy));
        Pcre2CompileValidator.ValidateArguments(options, compileSettings);
        ArgumentNullException.ThrowIfNull(patternSource.Text, "pattern");
        var account = policy.ProgramMemoryAllowance;
        var ownsConstruction = explicitCompile || account is not null || constructionPolicy != default ||
            constructionTimeout != Timeout.InfiniteTimeSpan;
        using var construction = !ownsConstruction ? Utf8ConstructionScope.Enter() : account is null
            ? Utf8ConstructionScope.Enter(constructionPolicy, constructionTimeout, token)
            : Utf8ConstructionScope.Enter(constructionPolicy, constructionTimeout, token, account);
        try
        {
            object pattern = patternSource.Text;
            if (patternSource.IsUtf8)
            {
                construction.Budget.ChargeWork((ulong)patternSource.Utf8.Length);
                ValidatePatternUtf8(patternSource.Utf8, construction.Budget);
                var bytes = construction.Budget.AllocateArray<byte>(patternSource.Length, false);
                construction.Budget.ChargeWork((ulong)bytes.Length);
                patternSource.Utf8.CopyTo(bytes);
                pattern = bytes;
            }
            var request = new Pcre2CompileRequest(
                pattern,
                options,
                compileSettings,
                defaultExecutionLimits,
                validatedTimeout);
            _program = Pcre2Compiler.Compile(request);
            if (Pcre2GlobalOperationDriver.HasUnmeteredExecution(request) &&
                _program.Operations.IsMatch is Pcre2FiniteLiteralLanguageDirectProgram literalLanguage)
            {
                _unmeteredLiteralLanguage = literalLanguage;
            }
            _replacementCache = Utf8BoundedPreparationCache<Pcre2ReplacementCacheKey, Pcre2ReplacementPreparation>.CreateRetained(
                16, EqualityComparer<Pcre2ReplacementCacheKey>.Default);
            if (account is not null)
            {
                // Cover bounded retained cache capacity before publication, with
                // no eager buffers, per-entry leases or later funding handoff.
                construction.Budget.ReserveBytes(_replacementCache.MaximumMetadataBytes + _replacementCache.MaximumPayloadBytes);
                var storage = construction.Budget.AllocateObject(account,
                    static allowance => new Utf8FundedStorage(allowance, nameof(Utf8Pcre2Regex)));
                construction.Budget.ChargeWork(0);
                storage.Retain(construction.Budget.TransferFundedBytes());
                _programMemory = storage;
            }
            else if (ownsConstruction) construction.Budget.ChargeWork(0);
            construction.Complete();
        }
        catch (Utf8ExecutionDeadlineExpiredException) when (ownsConstruction)
        { throw new RegexMatchTimeoutException(string.Empty, patternSource.IsUtf8 ? string.Empty : patternSource.Text, constructionTimeout); }
    }

    /// <summary>Maps a shared resource policy onto PCRE2 execution limits.</summary>
    /// <exception cref="ArgumentOutOfRangeException">A policy budget is zero or exceeds the 32-bit work/depth range.</exception>
    private static Utf8Pcre2ExecutionLimits ToExecutionLimits(Utf8ResourcePolicy resourcePolicy)
    {
        resourcePolicy.Validate("policy");
        uint workSteps = 0;
        if (resourcePolicy.MaxWorkSteps.HasValue && resourcePolicy.MaxWorkSteps.Value > uint.MaxValue)
        {
            throw new ArgumentOutOfRangeException("policy", "The work-steps budget exceeds the 32-bit PCRE2 range.");
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
            ResultLimitInBytes = resourcePolicy.MaxResultBytes ?? 0ul
        };
    }

    /// <summary>Gets or sets the timeout used by constructors and Compile policies that do not specify one.</summary>
    /// <remarks>The initial value is an infinite timeout.</remarks>
    public static TimeSpan DefaultMatchTimeout
    {
        get => s_defaultMatchTimeout;
        set => s_defaultMatchTimeout = Utf8MatchTimeout.Validate(value, nameof(value));
    }

    /// <summary>Gets pattern text as explicit interoperability; native source is decoded per request.</summary>
    public string Pattern => _program.Request.Pattern;

    /// <summary>Gets the compile-option flags.</summary>
    public Pcre2CompileOptions Options => _program.Request.Options;

    /// <summary>Gets the compile-time newline, group-name, and byte-oriented settings.</summary>
    public Utf8Pcre2CompileSettings CompileSettings => _program.Request.Settings;

    /// <summary>Gets the shared invocation work, depth, working and result policy copied at construction.</summary>
    public Utf8ResourcePolicy ResourcePolicy => _program.Request.CoreResourcePolicy;

    /// <summary>Gets the read-only PCRE2-unit projection of the shared invocation policy; zero denotes unlimited.</summary>
    public Utf8Pcre2ExecutionLimits DefaultExecutionLimits => _program.Request.DefaultLimits;

    /// <summary>Gets the maximum duration of an individual matching operation.</summary>
    public TimeSpan MatchTimeout => _program.Request.MatchTimeout;


    private Pcre2NameEntry[] NameEntries => _program.NameEntries;

    private bool IsMatchCore(ReadOnlySpan<byte> input, int startOffsetInBytes, Pcre2MatchOptions matchOptions,
        CancellationToken cancellationToken, IUtf8MemoryAllowance? workingMemoryAllowance = null)
    {
        var invocation = Pcre2SliceDriver.CreateInvocation(_program.Request, cancellationToken, workingMemoryAllowance);
        try
        {
            var subject = Pcre2Runner.PrepareInput(input, startOffsetInBytes, ref invocation);
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

    /// <summary>Determines whether a match exists using an explicit start byte offset and per-operation options, observing cancellation during matching.</summary>
    /// <param name="input">The well-formed UTF-8 subject to match against.</param>
    /// <param name="startOffsetInBytes">The zero-based UTF-8 byte offset where matching starts.</param>
    /// <param name="matchOptions">The per-operation match options.</param>
    /// <param name="cancellationToken">The token to observe before entry and during bounded matching.</param>
    /// <param name="workingMemoryAllowance">Optional shared working admission; null inherits program funding, or keeps numeric limits without funding. Refusal is terminal.</param>
    /// <exception cref="OperationCanceledException">Cancellation was requested.</exception>
    public bool IsMatch(ReadOnlySpan<byte> input, int startOffsetInBytes = 0, Pcre2MatchOptions matchOptions = Pcre2MatchOptions.None,
        CancellationToken cancellationToken = default, IUtf8MemoryAllowance? workingMemoryAllowance = null)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var programUse = _programMemory?.Enter();
        workingMemoryAllowance ??= _programMemory?.Allowance;
        if (workingMemoryAllowance is null && startOffsetInBytes == 0 && matchOptions == Pcre2MatchOptions.None &&
            !cancellationToken.CanBeCanceled && _unmeteredLiteralLanguage is { } literalLanguage)
            return literalLanguage.BooleanSearch.IsMatch(input, 0);

        return IsMatchCore(input, startOffsetInBytes, matchOptions, cancellationToken, workingMemoryAllowance);
    }

    private int CountCore(ReadOnlySpan<byte> input, int startOffsetInBytes, Pcre2MatchOptions matchOptions,
        CancellationToken cancellationToken, IUtf8MemoryAllowance? workingMemoryAllowance = null)
    {
        ThrowIfGenericIterationMayBeNonMonotone();
        var invocation = Pcre2SliceDriver.CreateInvocation(_program.Request, cancellationToken, workingMemoryAllowance);
        try
        {
            // Literal Count needs only byte-boundary admission and original
            // controls; it builds no interoperability metadata.
            if (matchOptions == Pcre2MatchOptions.None &&
                _program.Operations.Count is Pcre2LiteralDirectProgram literal &&
                (uint)startOffsetInBytes <= (uint)input.Length &&
                literal.Program.CanCountDirectly(input.Length - startOffsetInBytes))
            {
                Pcre2Runner.CheckInput(input, startOffsetInBytes, ref invocation);
                if (startOffsetInBytes < input.Length && (input[startOffsetInBytes] & 0xC0) == 0x80)
                    throw new ArgumentOutOfRangeException(nameof(startOffsetInBytes), startOffsetInBytes,
                        "The requested byte offset is not aligned to a UTF-8 scalar boundary.");
                int count;
                if (invocation.IsUnmetered && invocation.CheckpointHook is null)
                    count = literal.Program.CountNonOverlapping(input[startOffsetInBytes..]);
                else
                    Pcre2SliceDriver.ThrowIfSliceBreached(literal.Program.TryCountNonOverlapping(
                        input[startOffsetInBytes..], ref invocation, out count));
                Pcre2SliceDriver.CheckInvocation(ref invocation);
                return count;
            }
            var subject = Pcre2Runner.PrepareInput(input, startOffsetInBytes, ref invocation);
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

    /// <summary>Counts non-overlapping matches using an explicit start byte offset and per-operation options, observing cancellation during cumulative global matching.</summary>
    /// <param name="input">The well-formed UTF-8 subject to match against.</param>
    /// <param name="startOffsetInBytes">The zero-based UTF-8 byte offset where matching starts.</param>
    /// <param name="matchOptions">The per-operation match options.</param>
    /// <param name="cancellationToken">The token retained through all matching work.</param>
    /// <param name="workingMemoryAllowance">Optional shared working admission; null inherits program funding, or keeps numeric limits without funding. Covered temporary loans refund synchronously.</param>
    /// <exception cref="OperationCanceledException">Cancellation was requested.</exception>
    public int Count(ReadOnlySpan<byte> input, int startOffsetInBytes = 0, Pcre2MatchOptions matchOptions = Pcre2MatchOptions.None,
        CancellationToken cancellationToken = default, IUtf8MemoryAllowance? workingMemoryAllowance = null)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var programUse = _programMemory?.Enter();
        workingMemoryAllowance ??= _programMemory?.Allowance;
        return CountCore(input, startOffsetInBytes, matchOptions, cancellationToken, workingMemoryAllowance);
    }

    private Utf8Pcre2ValueMatch MatchCore(ReadOnlySpan<byte> input, int startOffsetInBytes, Pcre2MatchOptions matchOptions, CancellationToken cancellationToken,
        IUtf8MemoryAllowance? workingMemoryAllowance, scoped Span<Utf8Pcre2CaptureRange> output)
    {
        var invocation = Pcre2SliceDriver.CreateInvocation(_program.Request, cancellationToken, workingMemoryAllowance);
        try
        {
            var subject = Pcre2Runner.PrepareInput(input, startOffsetInBytes, ref invocation);
            var start = subject.GetBytePosition(startOffsetInBytes, nameof(startOffsetInBytes));
            if (Pcre2Runner.TryMatch(_program, ref subject, start, matchOptions, ref invocation, out var directMatch, out var mark))
            {
                if (!output.IsEmpty)
                    output[0] = directMatch.Success ? new(0, true, directMatch.StartOffsetInBytes,
                        directMatch.EndOffsetInBytes, directMatch.IsUtf8SliceWellFormed) : default;
                if (!invocation.IsUnmetered)
                {
                    invocation.Budget.ThrowIfCancellationRequested();
                    invocation.Budget.CheckDeadline();
                    invocation.Budget.ThrowIfCancellationRequested();
                }
                return Utf8Pcre2ValueMatch.Create(input, directMatch, mark);
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

    /// <summary>Finds the first match and optionally writes caller-owned byte capture slots zero onward.</summary>
    /// <remarks>Short output cannot change selection; excess slots stay untouched. No match or failure clears the supplied prefix, except entry cancellation leaves it unchanged. Caller-owned slots do not consume result-byte capacity. Null working admission inherits program funding; explicit admission overrides only working storage. MARK is returned on both hits and misses without capture materialization. Byte fragments and noncontiguous ranges keep their explicit validity status.</remarks>
    public Utf8Pcre2ValueMatch Match(ReadOnlySpan<byte> input, int startOffsetInBytes = 0,
        scoped Span<Utf8Pcre2CaptureRange> captures = default, Pcre2MatchOptions matchOptions = Pcre2MatchOptions.None,
        CancellationToken cancellationToken = default, IUtf8MemoryAllowance? workingMemoryAllowance = null)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var programUse = _programMemory?.Enter();
        workingMemoryAllowance ??= _programMemory?.Allowance;
        var output = captures[..Math.Min(captures.Length, _program.CaptureGroupCount + 1)];
        try
        {
            return output.Length <= 1
                ? MatchCore(input, startOffsetInBytes, matchOptions, cancellationToken, workingMemoryAllowance, output)
                : MatchCaptures(input, startOffsetInBytes, output, matchOptions, cancellationToken, workingMemoryAllowance);
        }
        catch { output.Clear(); throw; }
    }

    private Utf8Pcre2ValueMatch MatchCaptures(ReadOnlySpan<byte> input, int startOffsetInBytes,
        scoped Span<Utf8Pcre2CaptureRange> output, Pcre2MatchOptions matchOptions, CancellationToken token,
        IUtf8MemoryAllowance? workingMemoryAllowance)
    {
        var invocation = Pcre2SliceDriver.CreateInvocation(_program.Request, token, workingMemoryAllowance);
        var budget = new Pcre2ResourceBudget(_program.Request.DefaultLimits, invocation, false);
        try
        {
            var subject = Pcre2Runner.PrepareInput(input, startOffsetInBytes, ref budget.WorkingInvocation);
            var start = subject.GetBytePosition(startOffsetInBytes, nameof(startOffsetInBytes));
            var slots = new Utf8SliceSlots([], []);
            var workspace = new Utf8SliceRunner.AttemptWorkspace();
            var match = Pcre2Runner.MatchByteRanges(_program, ref subject, start, start, matchOptions,
                ref budget, ref slots, ref workspace, output);
            if (!match.Success) ClearCaptureOutput(output, ref budget);
            Pcre2SliceDriver.CheckInvocation(ref budget.WorkingInvocation);
            return Utf8Pcre2ValueMatch.Create(input, new Pcre2GroupData
            {
                Success = match.Success, StartOffsetInBytes = match.StartOffsetInBytes,
                EndOffsetInBytes = match.EndOffsetInBytes, Utf8SliceIsWellFormed = output[0].IsUtf8SliceWellFormed
            }, match.Mark);
        }
        catch (Utf8ExecutionDeadlineExpiredException)
        { throw new Pcre2MatchException("The PCRE2 match deadline expired.", Pcre2ErrorKind.Timeout); }
        finally { budget.WorkingInvocation.Ledger.Release(budget.WorkingInvocation.Ledger.ReservedBytes); }
    }

    private static void ClearCaptureOutput(Span<Utf8Pcre2CaptureRange> output, ref Pcre2ResourceBudget budget)
    {
        for (var offset = 0; offset < output.Length;)
        {
            budget.ChargeWorkspaceStep();
            var count = Math.Min(256, output.Length - offset);
            output.Slice(offset, count).Clear(); offset += count;
        }
    }

    private Utf8Pcre2MatchContext MatchDetailedCore(ReadOnlySpan<byte> input, int startOffsetInBytes, Pcre2MatchOptions matchOptions, CancellationToken cancellationToken)
    {
        var invocation = Pcre2SliceDriver.CreateInvocation(_program.Request, cancellationToken);
        try
        {
            var subject = Pcre2Runner.PrepareInput(input, startOffsetInBytes, ref invocation);
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

    /// <summary>Finds the first detailed match using an explicit start byte offset and per-operation options, observing cancellation during matching and required capture projection.</summary>
    /// <param name="input">The well-formed UTF-8 subject to match against.</param>
    /// <param name="startOffsetInBytes">The zero-based UTF-8 byte offset where matching starts.</param>
    /// <param name="matchOptions">The per-operation match options.</param>
    /// <param name="cancellationToken">The token to observe before entry and during bounded matching.</param>
    /// <exception cref="OperationCanceledException">Cancellation was requested.</exception>
    public Utf8Pcre2MatchContext MatchDetailed(ReadOnlySpan<byte> input, int startOffsetInBytes = 0, Pcre2MatchOptions matchOptions = Pcre2MatchOptions.None, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var programUse = _programMemory?.Enter();
        return MatchDetailedCore(input, startOffsetInBytes, matchOptions, cancellationToken);
    }

    private Utf8Pcre2ValueMatchEnumerator EnumerateMatchesCore(ReadOnlySpan<byte> input, int startOffsetInBytes, Pcre2MatchOptions matchOptions, CancellationToken cancellationToken)
    {
        ThrowIfGenericIterationMayBeNonMonotone();
        var invocation = Pcre2SliceDriver.CreateInvocation(_program.Request, cancellationToken);
        try
        {
            var subject = Pcre2Runner.PrepareInput(input, startOffsetInBytes, ref invocation);
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
                    _program.Request, null));
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

    /// <summary>Creates a match cursor using an explicit start byte offset and per-operation options, retaining cancellation and cumulative limits through every advance.</summary>
    /// <param name="input">The well-formed UTF-8 subject to match against.</param>
    /// <param name="startOffsetInBytes">The zero-based UTF-8 byte offset where matching starts.</param>
    /// <param name="matchOptions">The per-operation match options.</param>
    /// <remarks>Funded span enumeration borrows an open expression at creation and each advance. Copied positions and cumulative numeric limits remain independent/shared respectively; no host working loan remains between advances. Use a persistent cursor to survive program disposal.</remarks>
    /// <param name="cancellationToken">The token retained during cursor advances, including copies.</param>
    /// <exception cref="OperationCanceledException">Cancellation was requested.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Utf8Pcre2ValueMatchEnumerator EnumerateMatches(ReadOnlySpan<byte> input, int startOffsetInBytes = 0, Pcre2MatchOptions matchOptions = Pcre2MatchOptions.None, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var programUse = _programMemory?.Enter();
        return EnumerateMatchesCore(input, startOffsetInBytes, matchOptions, cancellationToken).WithProgramMemory(_programMemory);
    }

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
            var subject = Pcre2Runner.PrepareInput(input, startOffsetInBytes, ref invocation);
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
    public Utf8Pcre2ProbeResult Probe(ReadOnlySpan<byte> input, Pcre2PartialMode partialMode, int startOffsetInBytes = 0, Pcre2MatchOptions matchOptions = Pcre2MatchOptions.None, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var programUse = _programMemory?.Enter();
        return ProbeCore(input, partialMode, startOffsetInBytes, matchOptions, cancellationToken);
    }

    /// <summary>Replaces matches using explicit start, substitution, and per-match options, observing cancellation during matching and output shaping.</summary>
    /// <param name="input">The well-formed UTF-8 subject to match against.</param>
    /// <param name="replacement">The PCRE2 replacement template.</param>
    /// <param name="startOffsetInBytes">The zero-based UTF-8 byte offset where matching starts.</param>
    /// <param name="substitutionOptions">The substitution options.</param>
    /// <param name="matchOptions">The per-operation match options.</param>
    /// <param name="cancellationToken">The original token retained through validation, matching and output shaping.</param>
    /// <exception cref="OperationCanceledException">Cancellation was requested.</exception>
    public byte[] Replace(ReadOnlySpan<byte> input, string replacement, int startOffsetInBytes = 0, Pcre2SubstitutionOptions substitutionOptions = Pcre2SubstitutionOptions.None, Pcre2MatchOptions matchOptions = Pcre2MatchOptions.None, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var programUse = _programMemory?.Enter();
        return ReplaceCore(input, replacement, substitutionOptions, startOffsetInBytes, matchOptions, cancellationToken).Bytes;
    }

    /// <summary>Replaces matches using a UTF-8 template with explicit start, substitution, and per-match options, observing cancellation during matching and output shaping.</summary>
    /// <param name="input">The well-formed UTF-8 subject to match against.</param>
    /// <param name="replacementPatternUtf8">The PCRE2 replacement template encoded as well-formed UTF-8.</param>
    /// <param name="startOffsetInBytes">The zero-based UTF-8 byte offset where matching starts.</param>
    /// <param name="substitutionOptions">The substitution options.</param>
    /// <param name="matchOptions">The per-operation match options.</param>
    /// <param name="cancellationToken">The original token retained through validation, matching and output shaping.</param>
    /// <exception cref="OperationCanceledException">Cancellation was requested.</exception>
    public byte[] Replace(ReadOnlySpan<byte> input, ReadOnlySpan<byte> replacementPatternUtf8, int startOffsetInBytes = 0, Pcre2SubstitutionOptions substitutionOptions = Pcre2SubstitutionOptions.None, Pcre2MatchOptions matchOptions = Pcre2MatchOptions.None, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var programUse = _programMemory?.Enter();
        return ReplaceUtf8TemplateCore(input, replacementPatternUtf8, substitutionOptions, startOffsetInBytes, matchOptions, cancellationToken).Bytes;
    }

    private OperationStatus TryReplaceCore(ReadOnlySpan<byte> input, ReadOnlySpan<byte> replacementPatternUtf8,
        Span<byte> destination, out int bytesWritten, int startOffsetInBytes, Pcre2SubstitutionOptions substitutionOptions,
        Pcre2MatchOptions matchOptions, CancellationToken token, IUtf8MemoryAllowance? workingMemoryAllowance = null)
    {
        var invocation = Pcre2SliceDriver.CreateInvocation(_program.Request, token, workingMemoryAllowance);
        try
        {
            // Covered cold preparation and output borrow one working ledger.
            // This temporary construction owner refunds after final copying;
            // retained cache state is covered by the program's prepaid maximum.
            using var construction = workingMemoryAllowance is null ? null : Utf8ConstructionScope.Enter(
                new() { MaxBytes = Utf8ConstructionScope.DefaultBufferBytes }, Timeout.InfiniteTimeSpan,
                token, invocation.BorrowWorkingMemoryAllowance());
            if (construction is not null) construction.Budget.Deadline = invocation.Budget.Deadline;
            using var preparationControl = Pcre2ReplacementPreparationControl.Borrow(invocation, token);
            Pcre2SliceDriver.ThrowIfSliceBreached(Utf8Validation.TryValidateOnly(replacementPatternUtf8, ref invocation));
            var subject = Pcre2Runner.PrepareInput(input, startOffsetInBytes, ref invocation);
            var start = subject.GetBytePosition(startOffsetInBytes, nameof(startOffsetInBytes));
            var replacement = new Utf8ConstructionPattern(replacementPatternUtf8);
            Pcre2ReplacementPreparation? preparation = null;
            if (TryReplaceUsingLiteralDriver(input, subject, start, replacement, substitutionOptions,
                    matchOptions, invocation, destination, ref preparation, out bytesWritten, out var status))
            {
                Pcre2SliceDriver.CheckInvocation(ref invocation);
                return status;
            }

            var result = ReplaceValidated(input, subject, start, replacement, substitutionOptions,
                matchOptions, invocation, Pcre2ReplacementResultKind.Destination, destination, preparation);
            bytesWritten = result.BytesWritten;
            Pcre2SliceDriver.CheckInvocation(ref invocation);
            return result.Status;
        }
        catch (Utf8ExecutionDeadlineExpiredException)
        { throw new Pcre2MatchException("The PCRE2 match deadline expired.", Pcre2ErrorKind.Timeout); }
        catch (InvalidOperationException) when (invocation.Budget.LimitBreach != Utf8ExecutionLimitKind.None)
        { Pcre2SliceDriver.ThrowIfSliceBreached(invocation.Budget.LimitBreach); throw; }
        finally
        {
            if (workingMemoryAllowance is not null) invocation.Ledger.Release(invocation.Ledger.ReservedBytes);
        }
    }

    /// <summary>Attempts to write a replacement result, observing cancellation during preparation, discovery and copying.</summary>
    /// <remarks>Input and replacement may overlap the destination. Matched overlapping input uses admitted temporary output; a short destination is unchanged. Null working admission inherits program funding, or retains numeric controls without a shared account.</remarks>
    /// <param name="input">The well-formed UTF-8 subject to match against.</param>
    /// <param name="replacementPatternUtf8">The PCRE2 replacement template encoded as well-formed UTF-8.</param>
    /// <param name="destination">The destination buffer for the UTF-8 replacement result.</param>
    /// <param name="bytesWritten">When this method returns, the number of bytes written to the destination.</param>
    /// <param name="startOffsetInBytes">The zero-based UTF-8 byte offset where matching starts.</param>
    /// <param name="substitutionOptions">The substitution options.</param>
    /// <param name="matchOptions">The per-operation match options.</param>
    /// <param name="cancellationToken">The original token retained through the operation.</param>
    /// <param name="workingMemoryAllowance">Optional shared working admission; null inherits program funding.</param>
    /// <exception cref="OperationCanceledException">Cancellation was requested; destination bytes copied before interruption may remain.</exception>
    public OperationStatus TryReplace(ReadOnlySpan<byte> input, ReadOnlySpan<byte> replacementPatternUtf8, Span<byte> destination, out int bytesWritten, int startOffsetInBytes = 0, Pcre2SubstitutionOptions substitutionOptions = Pcre2SubstitutionOptions.None, Pcre2MatchOptions matchOptions = Pcre2MatchOptions.None, CancellationToken cancellationToken = default, IUtf8MemoryAllowance? workingMemoryAllowance = null)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var programUse = _programMemory?.Enter();
        workingMemoryAllowance ??= _programMemory?.Allowance;
        return TryReplaceCore(input, replacementPatternUtf8, destination, out bytesWritten, startOffsetInBytes, substitutionOptions, matchOptions, cancellationToken, workingMemoryAllowance);
    }

    /// <summary>Gets the number of name-to-slot entries, including duplicate names.</summary>
    public int NameEntryCount
    {
        get { using var use = _programMemory?.Enter(); return NameEntries.Length; }
    }

    /// <summary>Copies name-to-slot entries into a caller-provided destination.</summary>
    /// <returns>The number of entries copied; <paramref name="isMore"/> reports whether additional entries did not fit.</returns>
    public int CopyNameEntries(Span<Pcre2NameEntry> destination, out bool isMore)
    {
        using var programUse = _programMemory?.Enter();
        var written = Math.Min(destination.Length, NameEntries.Length);
        NameEntries.AsSpan(0, written).CopyTo(destination);
        isMore = written < NameEntries.Length;
        return written;
    }

    /// <summary>Copies every capture slot assigned to a possibly duplicated group name.</summary>
    /// <returns>The number of slot numbers copied; <paramref name="isMore"/> reports whether additional numbers did not fit.</returns>
    public int CopyNumbersForName(string name, Span<int> destination, out bool isMore)
    {
        using var programUse = _programMemory?.Enter();
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

    private int MatchManyCore(ReadOnlySpan<byte> input, Span<Utf8Pcre2MatchData> destination, out bool isMore,
        int startOffsetInBytes, Pcre2MatchOptions matchOptions, CancellationToken token)
    {
        ThrowIfGenericIterationMayBeNonMonotone();
        var invocation = Pcre2SliceDriver.CreateInvocation(_program.Request, token);
        try
        {
            var subject = Pcre2Runner.PrepareInput(input, startOffsetInBytes, ref invocation);
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
                IsUtf8SliceWellFormed = current.IsUtf8SliceWellFormed
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

    /// <summary>Copies successive match snapshots using explicit start and per-match options, observing cancellation during discovery and copying.</summary>
    /// <remarks>Input and destination storage must be disjoint. Overlap is unsupported and is not checked.</remarks>
    /// <param name="input">The well-formed UTF-8 subject to match against.</param>
    /// <param name="destination">The destination for match snapshots.</param>
    /// <param name="isMore">When this method returns, whether additional matches remain.</param>
    /// <param name="startOffsetInBytes">The zero-based UTF-8 byte offset where matching starts.</param>
    /// <param name="matchOptions">The per-operation match options.</param>
    /// <param name="cancellationToken">The original token retained through discovery and destination writes.</param>
    /// <exception cref="OperationCanceledException">Cancellation was requested; an already-written destination prefix remains valid.</exception>
    public int MatchMany(ReadOnlySpan<byte> input, Span<Utf8Pcre2MatchData> destination, out bool isMore, int startOffsetInBytes = 0, Pcre2MatchOptions matchOptions = Pcre2MatchOptions.None, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var programUse = _programMemory?.Enter();
        return MatchManyCore(input, destination, out isMore, startOffsetInBytes, matchOptions, cancellationToken);
    }

    /// <summary>Returns the compiled expression's execution and result-shaping characteristics.</summary>
    public Utf8Pcre2Analysis Analyze()
    {
        using var programUse = _programMemory?.Enter();
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
            MayFailIterativeExecutionAtRuntime = backtracking is not null && HasDeferredLookaroundReset(backtracking)
        };
    }

    private bool TryReplaceUsingLiteralDriver(
        ReadOnlySpan<byte> input,
        Utf8ValidatedInput subject,
        Utf8BytePosition start,
        Utf8ConstructionPattern replacement,
        Pcre2SubstitutionOptions substitutionOptions,
        Pcre2MatchOptions matchOptions,
        Utf8SliceInvocation invocation,
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
                result = new(bytes);
                return true;
            }

            var output = Utf8InvocationOutput.AllocateBytes(outputLength.Value, cursor.WorkingBufferControl, out outputReservation);
            EmitLiteralDriverReplacement(input, plan, replacementOnly, output, ledger.WrittenRanges, cursor.WorkingBufferControl);
            result = new(output);
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
        Utf8ConstructionPattern replacement,
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
        Utf8ConstructionPattern replacement,
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
        preparation ??= GetTemplatePreparation(replacement, templateOptions);
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
        ReadOnlySpan<Utf8ReplacementRange> matches, scoped Pcre2ResultControl control)
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

    private enum Pcre2ReplacementResultKind : byte { Bytes, Destination }
    private readonly record struct Pcre2ReplacementResult(byte[] Bytes,
        OperationStatus Status = OperationStatus.Done, int BytesWritten = 0);

    private static Pcre2ReplacementResult CompleteReplacementWriter(ref Pcre2ReplacementTemplateWriter writer,
        Pcre2ReplacementResultKind kind, Span<byte> destination, Pcre2SubstitutionOptions options)
    {
        if (kind == Pcre2ReplacementResultKind.Bytes) return new(writer.Complete());
        var status = writer.CompleteToDestination(destination,
            (options & Pcre2SubstitutionOptions.SubstituteOverflowLength) != 0, out var written);
        return new([], status, written);
    }

    private static Pcre2ReplacementResult CompleteUnchangedReplacement(ReadOnlySpan<byte> input,
        Pcre2ReplacementResultKind kind, Span<byte> destination, Pcre2SubstitutionOptions options,
        ref Utf8SliceInvocation invocation)
    {
        if (kind == Pcre2ReplacementResultKind.Bytes)
            return new(Pcre2ReplacementTemplateWriter.CopyUnchanged(input, ref invocation));
        Pcre2SliceDriver.CheckInvocation(ref invocation);
        if (input.Length > destination.Length)
            return new([], OperationStatus.DestinationTooSmall,
                (options & Pcre2SubstitutionOptions.SubstituteOverflowLength) != 0 ? input.Length : 0);
        Utf8SliceBufferWork.Copy(input, destination, ref invocation);
        return new([], OperationStatus.Done, input.Length);
    }

    private Pcre2ReplacementResult ReplaceCore(
        ReadOnlySpan<byte> input,
        string replacement,
        Pcre2SubstitutionOptions substitutionOptions,
        int startOffsetInBytes,
        Pcre2MatchOptions matchOptions,
        CancellationToken cancellationToken)
    {
        var invocation = Pcre2SliceDriver.CreateInvocation(_program.Request, cancellationToken);
        using var preparationControl = Pcre2ReplacementPreparationControl.Borrow(invocation, cancellationToken);
        try
        {
            var subject = Pcre2Runner.PrepareInput(input, startOffsetInBytes, ref invocation);
            var start = subject.GetBytePosition(startOffsetInBytes, nameof(startOffsetInBytes));
            var result = ReplaceValidated(input, subject, start, new Utf8ConstructionPattern(replacement), substitutionOptions,
                matchOptions, invocation, Pcre2ReplacementResultKind.Bytes, default);
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
        try
        {
            Pcre2SliceDriver.ThrowIfSliceBreached(Utf8Validation.TryValidateOnly(replacement, ref invocation));
            var subject = Pcre2Runner.PrepareInput(input, startOffsetInBytes, ref invocation);
            var start = subject.GetBytePosition(startOffsetInBytes, nameof(startOffsetInBytes));
            var result = ReplaceValidated(input, subject, start, new Utf8ConstructionPattern(replacement), options,
                matchOptions, invocation, Pcre2ReplacementResultKind.Bytes, default);
            Pcre2SliceDriver.CheckInvocation(ref invocation);
            return result;
        }
        catch (Utf8ExecutionDeadlineExpiredException)
        { throw new Pcre2MatchException("The PCRE2 match deadline expired.", Pcre2ErrorKind.Timeout); }
        catch (InvalidOperationException) when (invocation.Budget.LimitBreach != Utf8ExecutionLimitKind.None)
        { Pcre2SliceDriver.ThrowIfSliceBreached(invocation.Budget.LimitBreach); throw; }
    }

    private Pcre2ReplacementResult ReplaceValidated(ReadOnlySpan<byte> input, Utf8ValidatedInput subject, Utf8BytePosition start, Utf8ConstructionPattern replacement,
        Pcre2SubstitutionOptions substitutionOptions,
        Pcre2MatchOptions matchOptions, Utf8SliceInvocation invocation, Pcre2ReplacementResultKind kind, Span<byte> destination,
        Pcre2ReplacementPreparation? preparation = null)
    {
        if (RejectsReplacementIteration())
        {
            throw new NotSupportedException("SPEC-PCRE2 rejects replacement for lookaround-\\K iterative matches.");
        }

        if (kind != Pcre2ReplacementResultKind.Destination && TryReplaceUsingLiteralDriver(
                input,
                subject,
                start,
                replacement,
                substitutionOptions,
                matchOptions,
                invocation,
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
                matchOptions, invocation, kind, destination, preparation);
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
                    matchOptions, invocation, kind, destination, preparation);
            }

            return ReplaceSliceDetailed(
                input, subject, start,
                sliceProgram.SliceProgram,
                replacement,
                substitutionOptions,
                invocation, kind, destination, preparation);
        }

        return ReplaceNativeValueTemplate(input, subject, start, replacement, substitutionOptions,
            matchOptions, invocation, kind, destination, preparation);
    }

    // Extended templates on capture-free native routes keep the existing
    // global cursor/kernel. Reuse one admitted group-zero slot and lend the
    // discovery authority to the writer; no per-match core rematching.
    private Pcre2ReplacementResult ReplaceNativeValueTemplate(ReadOnlySpan<byte> input, Utf8ValidatedInput subject, Utf8BytePosition start, Utf8ConstructionPattern replacement,
        Pcre2SubstitutionOptions options, Pcre2MatchOptions matchOptions,
        Utf8SliceInvocation invocation, Pcre2ReplacementResultKind kind, Span<byte> destination, Pcre2ReplacementPreparation? prepared)
    {
        if (!Pcre2GlobalOperationDriver.TryCreateCursor(_program, subject, start, matchOptions, out var cursor,
            new Pcre2ResourceBudget(_program.Request.DefaultLimits,
                invocation, collectDiagnostics: false)))
            throw CreateUnsupportedReplacementException();
        var control = cursor.WorkingBufferControl;
        var replacementOnly = (options & Pcre2SubstitutionOptions.SubstituteReplacementOnly) != 0;
        if (!cursor.MoveNext())
            return CompleteUnchangedReplacement(replacementOnly ? [] : input, kind, destination,
                options, ref control.WorkingInvocation);
        var templateOptions = options & ~Pcre2SubstitutionOptions.SubstituteReplacementOnly;
        var preparation = prepared ?? ((options & Pcre2SubstitutionOptions.SubstituteLiteral) != 0
            ? Pcre2ReplacementPreparationControl.Prepare(replacement, options, true)
            : GetTemplatePreparation(replacement, templateOptions));
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
            return CompleteReplacementWriter(ref writer, kind, destination, options);
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
        Utf8ConstructionPattern replacement, Pcre2SubstitutionOptions options, Pcre2MatchOptions matchOptions, Utf8SliceInvocation invocation,
        Pcre2ReplacementResultKind kind, Span<byte> destination,
        Pcre2ReplacementPreparation? prepared)
    {
        var cursor = new Pcre2BacktrackingDetailedGlobalMatchCursor(program, _program.CandidateSearchPlan,
            validated, start, matchOptions, _program.Request,
            new Pcre2ResourceBudget(_program.Request.DefaultLimits,
                invocation, collectDiagnostics: false));
        if (!cursor.MoveNext())
        {
            if ((options & Pcre2SubstitutionOptions.SubstituteReplacementOnly) != 0) return new([]);
            return CompleteUnchangedReplacement(input, kind, destination, options, ref cursor.WorkingInvocation);
        }
        var replacementOnly = (options & Pcre2SubstitutionOptions.SubstituteReplacementOnly) != 0;
        var templateOptions = options & ~Pcre2SubstitutionOptions.SubstituteReplacementOnly;
        var preparation = prepared ?? ((options & Pcre2SubstitutionOptions.SubstituteLiteral) != 0
            ? Pcre2ReplacementPreparationControl.Prepare(replacement, options, true)
            : GetTemplatePreparation(replacement, templateOptions));
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
            return CompleteReplacementWriter(ref writer, kind, destination, options);
        }
        finally { writer.Release(); }
    }

    private Pcre2ReplacementResult ReplaceSliceDetailed(ReadOnlySpan<byte> input, Utf8ValidatedInput validated, Utf8BytePosition start, Utf8SliceProgram sliceProgram,
        Utf8ConstructionPattern replacement, Pcre2SubstitutionOptions options, Utf8SliceInvocation invocation,
        Pcre2ReplacementResultKind kind, Span<byte> destination,
        Pcre2ReplacementPreparation? prepared)
    {
        var cursor = new Pcre2SliceGlobalMatchCursor(sliceProgram, invocation, validated, start, _program.Request, null);
        if (!cursor.MoveNext())
        {
            if ((options & Pcre2SubstitutionOptions.SubstituteReplacementOnly) != 0) return new([]);
            return CompleteUnchangedReplacement(input, kind, destination, options, ref cursor.WorkingInvocation);
        }
        var replacementOnly = (options & Pcre2SubstitutionOptions.SubstituteReplacementOnly) != 0;
        var templateOptions = options & ~Pcre2SubstitutionOptions.SubstituteReplacementOnly;
        var preparation = prepared ?? ((options & Pcre2SubstitutionOptions.SubstituteLiteral) != 0
            ? Pcre2ReplacementPreparationControl.Prepare(replacement, options, true)
            : GetTemplatePreparation(replacement, templateOptions));
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
            return CompleteReplacementWriter(ref writer, kind, destination, options);
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
                static (key, _) => Pcre2ReplacementPreparationControl.Prepare(key.Source, key.Options, false),
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

    private Pcre2ReplacementPreparation GetTemplatePreparation(Utf8ConstructionPattern replacement, Pcre2SubstitutionOptions options)
    {
        if (!replacement.IsUtf8) return GetReplacementPreparation(replacement.Text, options);
        options &= Pcre2SubstitutionOptions.Extended;
        using var control = Pcre2ReplacementPreparationControl.JoinOrEnter(_program.Request.MatchTimeout);
        Pcre2ReplacementPreparationControl.CheckText(replacement.Length);
        try
        {
            if (_replacementCache.TryGet(replacement.Utf8, options,
                static (key, query, flags) => key.Options == flags && key.Replacement is byte[] owned && NativeTemplateEquals(owned, query),
                default(Pcre2ReplacementTextControl), out var plan, out _)) return plan;
            using var owner = Utf8ConstructionScope.Enter();
            using var storage = Utf8ConstructionSession.Create(owner.Budget);
            var bytes = Pcre2ReplacementPreparationControl.FreezeNativeKey(storage, replacement);
            var result = _replacementCache.GetOrAdd(new Pcre2ReplacementCacheKey(bytes, options), 0,
                static (key, _) => Pcre2ReplacementPreparationControl.Prepare(key.Source, key.Options, false),
                default(Pcre2ReplacementTextControl), default(Pcre2ReplacementRetainedStorage));
            storage.Complete(); owner.Complete();
            return result;
        }
        catch (Exception failure) when (failure is OperationCanceledException ||
            failure is Pcre2MatchException { ErrorKind: Pcre2ErrorKind.Timeout or Pcre2ErrorKind.MatchLimit or
                Pcre2ErrorKind.DepthLimit or Pcre2ErrorKind.HeapLimit or Pcre2ErrorKind.ResultLimit })
        {
            Pcre2ReplacementPreparationControl.Check();
            return Pcre2ReplacementPreparationControl.Prepare(replacement, options, false);
        }
    }

    private static bool NativeTemplateEquals(ReadOnlySpan<byte> owned, ReadOnlySpan<byte> query)
    {
        if (owned.Length != query.Length) return false;
        for (var offset = 0; offset < query.Length;)
        {
            Pcre2ReplacementPreparationControl.Check();
            var length = Math.Min(2048, query.Length - offset);
            if (!owned.Slice(offset, length).SequenceEqual(query.Slice(offset, length))) return false;
            offset += length;
        }
        Pcre2ReplacementPreparationControl.Check();
        return true;
    }

    private readonly record struct Pcre2ReplacementCacheKey(object Replacement, Pcre2SubstitutionOptions Options)
    {
        internal Utf8ConstructionPattern Source => Replacement is byte[] bytes
            ? new Utf8ConstructionPattern(bytes) : new Utf8ConstructionPattern((string)Replacement);
        public bool Equals(Pcre2ReplacementCacheKey other) => Options == other.Options &&
            (ReferenceEquals(Replacement, other.Replacement) ||
                (Replacement is string text && other.Replacement is string otherText && StringComparer.Ordinal.Equals(text, otherText)) ||
                (Replacement is byte[] bytes && other.Replacement is byte[] otherBytes && NativeTemplateEquals(bytes, otherBytes)));
        public override int GetHashCode()
        {
            if (Replacement is string text) return HashCode.Combine(StringComparer.Ordinal.GetHashCode(text), Options);
            var hash = new HashCode(); hash.Add(Options);
            var bytes = (byte[])Replacement;
            for (var offset = 0; offset < bytes.Length;)
            {
                Pcre2ReplacementPreparationControl.Check();
                var length = Math.Min(2048, bytes.Length - offset);
                hash.AddBytes(bytes.AsSpan(offset, length)); offset += length;
            }
            return hash.ToHashCode();
        }
    }

    private readonly struct Pcre2ReplacementRetainedStorage : IUtf8RetainedStorage<Pcre2ReplacementCacheKey, Pcre2ReplacementPreparation>
    {
        public ulong GetKeyCharge(Pcre2ReplacementCacheKey key) => key.Replacement is byte[] bytes
            ? Utf8ConstructionBudget.ArrayBytes<byte>(bytes.Length)
            : Utf8ConstructionBudget.StringBytes(((string)key.Replacement).Length);
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
