using Lokad.Utf8Regex.Internal;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.FrontEnd;
using Lokad.Utf8Regex.Internal.Input;
using Lokad.Utf8Regex.Internal.Replacement;

namespace Lokad.Utf8Regex;

/// <inheritdoc />
public sealed partial class Utf8Regex
{
    private readonly record struct ReplacementPreparationControl(Utf8ExecutionDeadline Deadline, CancellationToken Token)
        : IUtf8ConstructionTextControl
    {
        public void Check() { Token.ThrowIfCancellationRequested(); Deadline.CheckNow(); }
        internal bool IsUncontrolled => Deadline.IsInfinite && !Token.CanBeCanceled;
    }

    private Utf8AnalyzedReplacement GetParsedReplacement(string replacement, ReplacementPreparationControl control)
    {
        // Reject the public null template before observing elapsed preparation
        // time (pre-cancellation is checked at the API).
        if (replacement is null) throw new ArgumentNullException(nameof(replacement));
        try
        {
            if (control.IsUncontrolled) return GetParsedReplacement(replacement);
            return _replacementCache.GetOrAdd(replacement, (Self: this, Control: control),
                static (text, state) => state.Self.PrepareReplacement(text, state.Control), control);
        }
        catch (OperationCanceledException failure) when (failure.CancellationToken != control.Token)
        {
            // A pending factory may belong to another caller. Observe our own
            // authority and prepare once, rather than inheriting its failure.
            control.Check();
            return PrepareReplacement(replacement, control);
        }
        catch (Utf8ExecutionDeadlineExpiredException)
        {
            control.Check();
            return PrepareReplacement(replacement, control);
        }
    }

    private Utf8AnalyzedReplacement PrepareReplacement(object replacement, ReplacementPreparationControl control)
        => PrepareBorrowedReplacement(replacement is byte[] bytes ? new Utf8ConstructionPattern(bytes)
            : new Utf8ConstructionPattern((string)replacement), control);

    private Utf8AnalyzedReplacement PrepareBorrowedReplacement(Utf8ConstructionPattern replacement, ReplacementPreparationControl control)
    {
        using var construction = ResourcePolicy.MaxWorkingBytes is { } maximumBytes
            ? Utf8ConstructionScope.RestrictAdditionalBytes(maximumBytes)
            : Utf8ConstructionScope.Enter();
        var budget = construction.Budget;
        if (control.IsUncontrolled)
        {
            var prepared = Utf8FrontEndReplacementAnalyzer.Analyze(replacement, _groupNumbers, _groupNames,
                (Options & System.Text.RegularExpressions.RegexOptions.ECMAScript) != 0);
            construction.Complete(); return prepared;
        }
        var previousToken = budget.CancellationToken;
        var previousDeadline = budget.Deadline;
        var previousHook = budget.CheckpointHook;
        if ((!previousToken.CanBeCanceled || previousToken == control.Token) &&
            (previousDeadline.IsInfinite || ReferenceEquals(previousDeadline, control.Deadline)))
        {
            // ChargeWork already checks these fields after the existing hook.
            // Borrow the same authority instead of adding another callback.
            budget.CancellationToken = control.Token;
            budget.Deadline = control.Deadline;
        }
        else
        {
            var parentHook = previousHook;
            var currentControl = control;
            budget.CheckpointHook = steps => { parentHook?.Invoke(steps); currentControl.Check(); };
        }
        try
        {
            control.Check();
            var result = Utf8FrontEndReplacementAnalyzer.Analyze(replacement, _groupNumbers, _groupNames,
                (Options & System.Text.RegularExpressions.RegexOptions.ECMAScript) != 0);
            control.Check();
            construction.Complete();
            return result;
        }
        finally
        {
            budget.CheckpointHook = previousHook;
            budget.CancellationToken = previousToken;
            budget.Deadline = previousDeadline;
        }
    }

    private static void ValidateReplacementTemplateUtf8(ReadOnlySpan<byte> template, ReplacementPreparationControl control)
    {
        control.Check();
        var construction = Utf8ConstructionScope.IsActive ? Utf8ConstructionScope.CurrentBudget : null;
        var work = (ulong)template.Length;
        if (construction is not null) construction.ChargeWork(work);
        else if (work > Utf8ConstructionScope.DefaultWorkSteps)
            throw new Utf8ResourceLimitExceededException(Utf8ResourceLimitKind.ConstructionWorkSteps,
                Utf8ConstructionScope.DefaultWorkSteps, work);

        // This scan acquires no payload and needs no new numeric owner. A live
        // parent shares its cumulative admission and interruption checkpoints.
        if (control.IsUncontrolled && (construction is null ||
            !construction.CancellationToken.CanBeCanceled && construction.Deadline.IsInfinite && construction.CheckpointHook is null))
        { _ = Utf8Validation.Validate(template); return; }
        for (var offset = 0; offset < template.Length;)
        {
            construction?.ChargeWork(0);
            control.Check();
            var count = Utf8SliceBufferWork.Utf8BlockLength(template[offset..]);
            if (!Utf8ValidationCore.TryValidate(template.Slice(offset, count), false, out _, out var error))
                throw Utf8Validation.CreateInvalidUtf8Exception(offset + error);
            offset += count;
        }
        construction?.ChargeWork(0);
        control.Check();
    }

    private Utf8AnalyzedReplacement GetParsedReplacement(ReadOnlySpan<byte> validatedTemplate, ReplacementPreparationControl control)
    {
        try
        {
            if (_replacementCache.TryGet(validatedTemplate, control, out var cached)) return cached;
            return PrepareNativeReplacement(validatedTemplate, control);
        }
        catch (OperationCanceledException failure) when (failure.CancellationToken != control.Token)
        {
            control.Check();
            return PrepareBorrowedReplacement(new Utf8ConstructionPattern(validatedTemplate), control);
        }
        catch (Utf8ExecutionDeadlineExpiredException)
        {
            control.Check();
            return PrepareBorrowedReplacement(new Utf8ConstructionPattern(validatedTemplate), control);
        }
    }

    private Utf8AnalyzedReplacement PrepareNativeReplacement(ReadOnlySpan<byte> validatedTemplate, ReplacementPreparationControl control)
    {
        using var construction = ResourcePolicy.MaxWorkingBytes is { } maximumBytes
            ? Utf8ConstructionScope.RestrictAdditionalBytes(maximumBytes)
            : Utf8ConstructionScope.Enter();
        if (control.IsUncontrolled)
        {
            var key = FreezeNativeReplacement(validatedTemplate, construction.Budget, control);
            var result = _replacementCache.GetOrAdd(key, (Self: this, Control: control),
                static (source, state) => state.Self.PrepareReplacement(source, state.Control), control);
            construction.Complete();
            return result;
        }

        // Keep captured locals in this block so uncontrolled key decoding does
        // not allocate an empty interruption closure before its early return.
        {
            var budget = construction.Budget;
            var previousToken = budget.CancellationToken;
            var previousDeadline = budget.Deadline;
            var previousHook = budget.CheckpointHook;
            if ((!previousToken.CanBeCanceled || previousToken == control.Token) &&
                (previousDeadline.IsInfinite || ReferenceEquals(previousDeadline, control.Deadline)))
            {
                budget.CancellationToken = control.Token;
                budget.Deadline = control.Deadline;
            }
            else
            {
                var parentHook = previousHook;
                var currentControl = control;
                budget.CheckpointHook = steps => { parentHook?.Invoke(steps); currentControl.Check(); };
            }
            try
            {
                control.Check();
                // Source was validated before route selection. The major key
                // array and nested parser overlap share this admission owner.
                var key = FreezeNativeReplacement(validatedTemplate, budget, control);
                var result = _replacementCache.GetOrAdd(key, (Self: this, Control: control),
                    static (source, state) => state.Self.PrepareReplacement(source, state.Control), control);
                control.Check();
                construction.Complete();
                return result;
            }
            finally
            {
                budget.CheckpointHook = previousHook;
                budget.CancellationToken = previousToken;
                budget.Deadline = previousDeadline;
            }
        }
    }

    private static byte[] FreezeNativeReplacement(ReadOnlySpan<byte> source, Utf8ConstructionBudget budget,
        ReplacementPreparationControl control)
    {
        control.Check();
        var result = budget.AllocateArray<byte>(source.Length, false);
        for (var offset = 0; offset < source.Length;)
        {
            control.Check();
            var count = Math.Min(2048, source.Length - offset);
            budget.ChargeWork((ulong)count);
            source.Slice(offset, count).CopyTo(result.AsSpan(offset, count)); offset += count;
        }
        control.Check(); return result;
    }
}
