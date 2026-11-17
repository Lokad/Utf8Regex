using System.Buffers;
using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Caching;
using Lokad.Utf8Regex.Internal;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Input;
using Lokad.Utf8Regex.Internal.Planning;
using Lokad.Utf8Regex.Internal.FrontEnd;

namespace Lokad.Utf8Regex.PythonRe;

// substitution entry points, split from the Python facade along the substitution ownership boundary.
/// <summary>Substitution entry points for <see cref="Utf8PythonRegex"/>.</summary>
public sealed partial class Utf8PythonRegex
{
    /// <summary>Replaces all matches and returns owned UTF-8 bytes.</summary>
    public byte[] Replace(ReadOnlySpan<byte> input, string replacement) => Replace(input, replacement, 0, 0);

    /// <summary>Replaces at most the requested number of matches and returns owned UTF-8 bytes.</summary>
    public byte[] Replace(ReadOnlySpan<byte> input, string replacement, int count) =>
        Replace(input, replacement, count, 0);

    // replacement templates are parsed once per distinct template and shared across operations; failures are never retained.
    private PythonReReplacementPlan GetReplacementPlan(string replacement, out Utf8FundedStorage.Use? memoryUse) =>
        _replacementPlanCache.GetOrAdd(
            replacement,
            this,
            static (template, self) =>
            {
                if (self.ResourcePolicy.MaxWorkingBytes is not { } maximumBytes)
                    return PythonReReplacementParser.Parse((string)template, self.CaptureGroupCount, self._namedGroups);
                // No token/deadline to forward on this lane. Restrict parser
                // bytes without installing an otherwise empty per-work hook.
                using var construction = Utf8ConstructionScope.RestrictAdditionalBytes(maximumBytes);
                var plan = PythonReReplacementParser.Parse((string)template, self.CaptureGroupCount, self._namedGroups);
                construction.Complete();
                return plan;
            },
            default(Utf8ConstructionTextNoControl), default(PythonReReplacementRetainedStorage), out memoryUse);

    private PythonReReplacementPlan GetReplacementPlan(ReadOnlySpan<byte> input, string replacement,
        Utf8SliceInvocation invocation, CancellationToken cancellationToken, out Utf8FundedStorage.Use? memoryUse)
    {
        memoryUse = null;
        var deadline = invocation.Budget.Deadline;
        var control = new PythonRePreparationInterruptionControl(deadline, cancellationToken);
        try
        {
            // Admit length-dependent hashing/comparison and possible parsing
            // before entering the cache. Coarse text blocks share the original
            // invocation; fine-grained parser work keeps its construction backstop.
            if (!invocation.IsUnmetered)
            {
                var bufferControl = new Utf8InvocationBufferControl(ref invocation);
                for (var offset = 0; offset < replacement.Length;)
                {
                    var breach = bufferControl.TryChargeWorkspaceStep();
                    if (breach != Utf8ExecutionLimitKind.None)
                        throw invocation.Budget.CreateResourceFailure();
                    offset += Math.Min(2048, replacement.Length - offset);
                }
            }
            try
            {
                if (deadline.IsInfinite && !cancellationToken.CanBeCanceled)
                    return GetReplacementPlan(replacement, out memoryUse);
                return _replacementPlanCache.GetOrAdd(replacement, (Self: this, Control: control),
                    static (template, state) => state.Self.PrepareReplacementPlan(template, state.Control),
                    control, default(PythonReReplacementRetainedStorage), out memoryUse);
            }
            catch (OperationCanceledException failure) when (failure.CancellationToken != cancellationToken)
            {
                // Pending entries share factory failures, but another caller's
                // interruption is not this call's authority. Prepare once with
                // our controls instead of entering another shared retry loop.
                control.Check();
                return _replacementPlanCache.PrepareForUse(replacement, (Self: this, Control: control),
                    static (template, state) => state.Self.PrepareReplacementPlan(template, state.Control), control, out memoryUse);
            }
            catch (Utf8ExecutionDeadlineExpiredException)
            {
                control.Check(); // Rethrow if our own deadline expired.
                return _replacementPlanCache.PrepareForUse(replacement, (Self: this, Control: control),
                    static (template, state) => state.Self.PrepareReplacementPlan(template, state.Control), control, out memoryUse);
            }
        }
        catch (Utf8ExecutionDeadlineExpiredException)
        {
            throw CreateMatchTimeoutException(input, Pattern);
        }
    }

    private PythonReReplacementPlan PrepareReplacementPlan(object replacement,
        PythonRePreparationInterruptionControl control)
    {
        // Borrow existing authority fields on a cache miss; a distinct parent
        // retains its original callback chain. Restore both fields and hook.
        // Working bytes also cap this phase's additional parser capacity;
        // construction work/depth and retained-cache admission stay separate.
        var maximum = ResourcePolicy.MaxWorkingBytes;
        if (replacement is byte[] native)
        {
            // The frozen key still has its temporary invocation loan. Bound its
            // overlap with parser capacity; retained cache storage is separate.
            var ceiling = Math.Min(maximum ?? ulong.MaxValue, Utf8ConstructionScope.DefaultBufferBytes);
            var keyBytes = Utf8ConstructionBudget.ArrayBytes<byte>(native.Length);
            maximum = ceiling - Math.Min(ceiling, keyBytes);
        }
        using var construction = maximum is { } maximumBytes
            ? Utf8ConstructionScope.RestrictAdditionalBytes(maximumBytes)
            : Utf8ConstructionScope.Enter();
        var budget = construction.Budget;
        var previousToken = budget.CancellationToken;
        var previousDeadline = budget.Deadline;
        var previousCheckpoint = budget.CheckpointHook;
        if ((!previousToken.CanBeCanceled || previousToken == control.Token) &&
            (previousDeadline.IsInfinite || ReferenceEquals(previousDeadline, control.Deadline)))
        {
            budget.CancellationToken = control.Token;
            budget.Deadline = control.Deadline;
        }
        else
        {
            var parentHook = previousCheckpoint;
            var currentControl = control;
            budget.CheckpointHook = steps => { parentHook?.Invoke(steps); currentControl.Check(); };
        }
        try
        {
            var plan = replacement is byte[] utf8
                ? PythonReReplacementParser.Parse(utf8.AsSpan(), CaptureGroupCount, _namedGroups)
                : PythonReReplacementParser.Parse((string)replacement, CaptureGroupCount, _namedGroups);
            construction.Complete();
            return plan;
        }
        finally
        {
            budget.CheckpointHook = previousCheckpoint;
            budget.CancellationToken = previousToken;
            budget.Deadline = previousDeadline;
        }
    }

    private PythonReReplacementPlan GetReplacementPlan(ReadOnlySpan<byte> input,
        Utf8ConstructionPattern replacement, Utf8SliceInvocation invocation,
        CancellationToken cancellationToken, out Utf8FundedStorage.Use? memoryUse)
    {
        if (!replacement.IsUtf8)
            return GetReplacementPlan(input, replacement.Text, invocation, cancellationToken, out memoryUse);
        memoryUse = null;
        var control = new PythonRePreparationInterruptionControl(invocation.Budget.Deadline, cancellationToken);
        ulong keyCharge = 0;
        try
        {
            var query = replacement.Utf8;
            // Admit source-length work on warm hits too. The comparison borrows
            // the caller's span; only misses acquire an immutable key.
            var bufferControl = new Utf8InvocationBufferControl(ref invocation);
            for (var offset = 0; offset < query.Length; offset += Math.Min(2048, query.Length - offset))
                if (bufferControl.TryChargeWorkspaceStep() != Utf8ExecutionLimitKind.None)
                    throw invocation.Budget.CreateResourceFailure();
            try
            {
                if (_replacementPlanCache.TryGet(query, control,
                    static (key, bytes, current) => key is byte[] owned && NativeTemplateEquals(owned, bytes, current),
                    control, out var plan, out memoryUse)) return plan;
            }
            catch (OperationCanceledException failure) when (failure.CancellationToken != cancellationToken)
            { control.Check(); return PrepareNativeUncached(query, control, ref invocation, out memoryUse); }
            catch (Utf8ExecutionDeadlineExpiredException)
            { control.Check(); return PrepareNativeUncached(query, control, ref invocation, out memoryUse); }

            var key = FreezeNativeTemplate(query, control, ref invocation, out keyCharge);
            try
            {
                return _replacementPlanCache.GetOrAdd(key, (Self: this, Control: control),
                    static (template, state) => state.Self.PrepareReplacementPlan(template, state.Control),
                    control, default(PythonReReplacementRetainedStorage), out memoryUse);
            }
            catch (OperationCanceledException failure) when (failure.CancellationToken != cancellationToken)
            {
                control.Check();
                return _replacementPlanCache.PrepareForUse(key, (Self: this, Control: control),
                    static (template, state) => state.Self.PrepareReplacementPlan(template, state.Control), control, out memoryUse);
            }
            catch (Utf8ExecutionDeadlineExpiredException)
            {
                control.Check();
                return _replacementPlanCache.PrepareForUse(key, (Self: this, Control: control),
                    static (template, state) => state.Self.PrepareReplacementPlan(template, state.Control), control, out memoryUse);
            }
        }
        catch (Utf8ExecutionDeadlineExpiredException) { throw CreateMatchTimeoutException(input, Pattern); }
        finally { invocation.Ledger.Release(keyCharge); }
    }

    private PythonReReplacementPlan PrepareNativeUncached(ReadOnlySpan<byte> query,
        PythonRePreparationInterruptionControl control, ref Utf8SliceInvocation invocation,
        out Utf8FundedStorage.Use? memoryUse)
    {
        ulong charge = 0;
        try
        {
            var key = FreezeNativeTemplate(query, control, ref invocation, out charge);
            return _replacementPlanCache.PrepareForUse(key, (Self: this, Control: control),
                static (template, state) => state.Self.PrepareReplacementPlan(template, state.Control), control, out memoryUse);
        }
        finally { invocation.Ledger.Release(charge); }
    }

    private static byte[] FreezeNativeTemplate(ReadOnlySpan<byte> query,
        PythonRePreparationInterruptionControl control, ref Utf8SliceInvocation invocation, out ulong charge)
    {
        charge = 0;
        control.Check();
        var required = Utf8ConstructionBudget.ArrayBytes<byte>(query.Length);
        if (required > Utf8ConstructionScope.DefaultBufferBytes)
            throw new Utf8ResourceLimitExceededException(Utf8ResourceLimitKind.ConstructionBytes,
                Utf8ConstructionScope.DefaultBufferBytes, required);
        if (invocation.TryReserveWorking(required) != Utf8ExecutionLimitKind.None)
            throw invocation.Budget.CreateResourceFailure();
        charge = required;
        control.Check();
        var key = query.IsEmpty ? [] : new byte[query.Length];
        var bufferControl = new Utf8InvocationBufferControl(ref invocation);
        for (var offset = 0; offset < query.Length;)
        {
            if (bufferControl.TryChargeWorkspaceStep() != Utf8ExecutionLimitKind.None)
                throw invocation.Budget.CreateResourceFailure();
            var length = Math.Min(2048, query.Length - offset);
            query.Slice(offset, length).CopyTo(key.AsSpan(offset, length));
            offset += length;
        }
        control.Check();
        return key;
    }

    private static bool NativeTemplateEquals(ReadOnlySpan<byte> owned, ReadOnlySpan<byte> query,
        PythonRePreparationInterruptionControl control)
    {
        if (owned.Length != query.Length) return false;
        for (var offset = 0; offset < query.Length;)
        {
            control.Check();
            var length = Math.Min(2048, query.Length - offset);
            if (!owned.Slice(offset, length).SequenceEqual(query.Slice(offset, length))) return false;
            offset += length;
        }
        control.Check();
        return true;
    }

    // Existing cache control also polls pending-entry waits and publication.
    // It carries interruption only; preparation keeps its own numeric budgets.
    private readonly struct PythonRePreparationInterruptionControl(
        Utf8ExecutionDeadline deadline, CancellationToken token) : IUtf8ConstructionTextControl
    {
        internal Utf8ExecutionDeadline Deadline => deadline;
        internal CancellationToken Token => token;

        public void Check()
        {
            token.ThrowIfCancellationRequested();
            deadline.CheckNow();
        }
    }

    /// <summary>Replaces matches after a byte offset and returns owned UTF-8 bytes.</summary>
    public byte[] Replace(ReadOnlySpan<byte> input, string replacement, int count, int startOffsetInBytes)
    {
        return SubnTemplateCore(input, replacement, count, startOffsetInBytes, CancellationToken.None, out _).ResultBytes;
    }

    /// <summary>Replaces matches after a byte offset and returns owned UTF-8 bytes, observing cancellation during matching and required result processing.</summary>
    /// <param name="input">The well-formed UTF-8 input to match against.</param>
    /// <param name="replacement">The replacement text.</param>
    /// <param name="count">The maximum number of replacements to perform.</param>
    /// <param name="startOffsetInBytes">The zero-based UTF-8 byte offset where matching starts.</param>
    /// <param name="cancellationToken">The token to observe at entry and during bounded operation checkpoints.</param>
    /// <exception cref="OperationCanceledException">Cancellation was requested through the supplied token.</exception>
    public byte[] Replace(ReadOnlySpan<byte> input, string replacement, int count, int startOffsetInBytes, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return SubnTemplateCore(input, replacement, count, startOffsetInBytes, cancellationToken, out _).ResultBytes;
    }

    /// <summary>Replaces all matches and returns UTF-8 bytes plus replacement count.</summary>
    public Utf8PythonSubnUtf8Result Subn(ReadOnlySpan<byte> input, string replacement) =>
        Subn(input, replacement, 0, 0);

    /// <summary>Replaces a bounded number of matches and returns UTF-8 bytes plus count.</summary>
    public Utf8PythonSubnUtf8Result Subn(ReadOnlySpan<byte> input, string replacement, int count) =>
        Subn(input, replacement, count, 0);

    /// <summary>Replaces matches after a byte offset and returns UTF-8 bytes plus count.</summary>
    public Utf8PythonSubnUtf8Result Subn(
        ReadOnlySpan<byte> input,
        string replacement,
        int count,
        int startOffsetInBytes) => SubnTemplateCore(input, replacement, count, startOffsetInBytes, CancellationToken.None, out _);

    /// <summary>Substitutes into owned UTF-8 bytes whose charge remains reserved until the returned receipt is disposed.</summary>
    /// <remarks>Requires caller-funded Compile. Reuses template preparation and native/scalar execution. No output copy or Release/TryReserve gap is added at publication. Dispose after retained users finish, or take over admission for byte-array aliases kept afterward. Numeric limits and the original token/deadline remain authoritative.</remarks>
    /// <exception cref="InvalidOperationException">This expression has no caller-owned program allowance.</exception>
    public Utf8PythonRetainedSubnResult SubnRetained(ReadOnlySpan<byte> input, string replacement,
        int count = 0, int startOffsetInBytes = 0, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (_programMemory is null)
            throw new InvalidOperationException("Retained substitution requires caller-funded compilation.");
        _ = SubnTemplateCore(input, replacement, count, startOffsetInBytes, cancellationToken, out var retained, retain: true);
        return retained!;
    }

    private Utf8PythonSubnUtf8Result SubnTemplateCore(ReadOnlySpan<byte> input, string replacement,
        int count, int startOffsetInBytes, CancellationToken cancellationToken,
        out Utf8PythonRetainedSubnResult? retained, bool retain = false)
        => SubnTemplateCore(input, new Utf8ConstructionPattern(replacement), count,
            startOffsetInBytes, cancellationToken, out retained, retain);

    private Utf8PythonSubnUtf8Result SubnTemplateCore(
        ReadOnlySpan<byte> input,
        Utf8ConstructionPattern replacement,
        int count,
        int startOffsetInBytes, CancellationToken cancellationToken,
        out Utf8PythonRetainedSubnResult? retained, bool retain = false)
    {
        retained = null;
        using var programUse = EnterProgramUse();
        ValidateStartOffset(input, startOffsetInBytes);
        var invocation = CreateInvocation(cancellationToken);
        var scalarPlan = GetReplacementPlan(input, replacement, invocation, cancellationToken, out var memoryUse);
        using var planUse = memoryUse;
        if (!HasFiniteExecutionLimits && !cancellationToken.CanBeCanceled && MatchTimeout == Timeout.InfiniteTimeSpan &&
            TryReplaceViaUtf8Literal(input, scalarPlan, count, startOffsetInBytes, ref invocation, out var literalBytes, out var literalCount, out retained, retain))
            return new Utf8PythonSubnUtf8Result { ResultBytes = literalBytes, ReplacementCount = literalCount };
        SubstituteScalar(input, scalarPlan, count, startOffsetInBytes, invocation,
            out var scalarResult, out retained, retain);
        return scalarResult;
    }

    /// <summary>Replaces matches after a byte offset and returns UTF-8 bytes plus count, observing cancellation during matching and required result processing.</summary>
    /// <param name="input">The well-formed UTF-8 input to match against.</param>
    /// <param name="replacement">The replacement text.</param>
    /// <param name="count">The maximum number of replacements to perform.</param>
    /// <param name="startOffsetInBytes">The zero-based UTF-8 byte offset where matching starts.</param>
    /// <param name="cancellationToken">The token to observe at entry and during bounded operation checkpoints.</param>
    /// <exception cref="OperationCanceledException">Cancellation was requested through the supplied token.</exception>
    public Utf8PythonSubnUtf8Result Subn(ReadOnlySpan<byte> input, string replacement, int count, int startOffsetInBytes, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return SubnTemplateCore(input, replacement, count, startOffsetInBytes, cancellationToken, out _);
    }

}
