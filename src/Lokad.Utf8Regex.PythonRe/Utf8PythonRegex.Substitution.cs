using Lokad.Utf8Regex.Internal;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.PythonRe;

/// <summary>Substitution entry points for <see cref="Utf8PythonRegex"/>.</summary>
public sealed partial class Utf8PythonRegex
{
    private PythonReReplacementPlan PrepareReplacementPlan(byte[] replacement,
        PythonRePreparationInterruptionControl control)
    {
        // Borrow existing authority fields on a cache miss; a distinct parent
        // retains its original callback chain. Restore both fields and hook.
        // Working bytes also cap this phase's additional parser capacity;
        // construction work/depth and retained-cache admission stay separate.
        // The frozen key still has its temporary invocation loan. Bound its
        // overlap with parser capacity; retained cache storage is separate.
        var ceiling = Math.Min(ResourcePolicy.MaxWorkingBytes ?? ulong.MaxValue, Utf8ConstructionScope.DefaultBufferBytes);
        var keyBytes = Utf8ConstructionBudget.ArrayBytes<byte>(replacement.Length);
        using var construction = Utf8ConstructionScope.RestrictAdditionalBytes(ceiling - Math.Min(ceiling, keyBytes));
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
            var plan = PythonReReplacementParser.Parse(replacement, CaptureGroupCount, _namedGroups);
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
        ReadOnlySpan<byte> query, ref Utf8SliceInvocation invocation,
        CancellationToken cancellationToken, out Utf8FundedStorage.Use? memoryUse)
    {
        memoryUse = null;
        var control = new PythonRePreparationInterruptionControl(invocation.Budget.Deadline, cancellationToken);
        ulong keyCharge = 0;
        try
        {
            // Admit source-length work on warm hits too. The comparison borrows
            // the caller's span; only misses acquire an immutable key.
            var bufferControl = new Utf8InvocationBufferControl(ref invocation);
            for (var offset = 0; offset < query.Length; offset += Math.Min(2048, query.Length - offset))
                if (bufferControl.TryChargeWorkspaceStep() != Utf8ExecutionLimitKind.None)
                    throw invocation.Budget.CreateResourceFailure();
            try
            {
                if (_replacementPlanCache.TryGet(query, control,
                    static (key, bytes, current) => TemplateEquals(key, bytes, current),
                    control, out var plan, out memoryUse)) return plan;
            }
            catch (OperationCanceledException failure) when (failure.CancellationToken != cancellationToken)
            { control.Check(); return PrepareUncachedTemplate(query, control, ref invocation, out memoryUse); }
            catch (Utf8ExecutionDeadlineExpiredException)
            { control.Check(); return PrepareUncachedTemplate(query, control, ref invocation, out memoryUse); }

            var key = FreezeTemplate(query, control, ref invocation, out keyCharge);
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

    private PythonReReplacementPlan PrepareUncachedTemplate(ReadOnlySpan<byte> query,
        PythonRePreparationInterruptionControl control, ref Utf8SliceInvocation invocation,
        out Utf8FundedStorage.Use? memoryUse)
    {
        ulong charge = 0;
        try
        {
            var key = FreezeTemplate(query, control, ref invocation, out charge);
            return _replacementPlanCache.PrepareForUse(key, (Self: this, Control: control),
                static (template, state) => state.Self.PrepareReplacementPlan(template, state.Control), control, out memoryUse);
        }
        finally { invocation.Ledger.Release(charge); }
    }

    private static byte[] FreezeTemplate(ReadOnlySpan<byte> query,
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

    private static bool TemplateEquals(ReadOnlySpan<byte> owned, ReadOnlySpan<byte> query,
        PythonRePreparationInterruptionControl control)
    {
        if (owned.Length != query.Length) return false;
        // The cache checks interruption before comparison and again before
        // returning the plan. One short comparison fits between those checks.
        if (query.Length <= 2048) return owned.SequenceEqual(query);
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

    /// <summary>Replaces matches with a strict UTF-8 Python template and returns owned UTF-8 bytes.</summary>
    /// <remarks>Zero count replaces all matches. The byte start preserves prefix assertion context. Template validation precedes matching, including on no match. The UTF-8 template uses Python text semantics.</remarks>
    public byte[] Replace(ReadOnlySpan<byte> input, ReadOnlySpan<byte> replacementUtf8,
        int count = 0, int startOffsetInBytes = 0, CancellationToken cancellationToken = default)
        => Subn(input, replacementUtf8, count, startOffsetInBytes, cancellationToken).ResultBytes;

    /// <summary>Substitutes a strict UTF-8 Python template and returns owned bytes and the replacement count.</summary>
    /// <remarks>Retains scalar semantics, capture expansion, synchronous cancellation and the original deadline/resource policy. Template errors retain their UTF-16 Position; malformed UTF-8 is rejected before template grammar. A cold template copies an admitted immutable key; warm lookups borrow the supplied span.</remarks>
    public Utf8PythonSubnUtf8Result Subn(ReadOnlySpan<byte> input, ReadOnlySpan<byte> replacementUtf8,
        int count = 0, int startOffsetInBytes = 0, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return SubnTemplateCore(input, replacementUtf8, count,
            startOffsetInBytes, cancellationToken, out _);
    }

    /// <summary>Substitutes a strict UTF-8 template into owned bytes with caller-funded retained admission.</summary>
    /// <remarks>Requires caller-funded compilation. Output remains valid after expression disposal; dispose the receipt when retained users finish, or take over admission for aliases kept afterward. Cached preparation stays funded through eviction or disposal and its last active use.</remarks>
    /// <exception cref="InvalidOperationException">This expression has no caller-owned program allowance.</exception>
    public Utf8PythonRetainedSubnResult SubnRetained(ReadOnlySpan<byte> input, ReadOnlySpan<byte> replacementUtf8,
        int count = 0, int startOffsetInBytes = 0, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (_programMemory is null)
            throw new InvalidOperationException("Retained substitution requires caller-funded compilation.");
        _ = SubnTemplateCore(input, replacementUtf8, count,
            startOffsetInBytes, cancellationToken, out var retained, retain: true);
        return retained!;
    }

    private Utf8PythonSubnUtf8Result SubnTemplateCore(
        ReadOnlySpan<byte> input,
        ReadOnlySpan<byte> replacement,
        int count,
        int startOffsetInBytes, CancellationToken cancellationToken,
        out Utf8PythonRetainedSubnResult? retained, bool retain = false)
    {
        retained = null;
        using var programUse = EnterProgramUse();
        ValidateStartOffset(input, startOffsetInBytes);
        var invocation = CreateInvocation(cancellationToken);
        var scalarPlan = GetReplacementPlan(input, replacement, ref invocation, cancellationToken, out var memoryUse);
        using var planUse = memoryUse;
        if (TryReplaceViaUtf8Literal(input, scalarPlan, count, startOffsetInBytes, ref invocation,
            out var literalBytes, out var literalCount, out retained, retain))
            return new Utf8PythonSubnUtf8Result { ResultBytes = literalBytes, ReplacementCount = literalCount };
        SubstituteScalar(input, scalarPlan, count, startOffsetInBytes, invocation,
            out var scalarResult, out retained, retain);
        return scalarResult;
    }

}
