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

    private Utf8AnalyzedReplacement AnalyzeReplacement(ReadOnlySpan<byte> replacement)
        => Utf8FrontEndReplacementAnalyzer.Analyze(replacement, _groupNumbers, _groupNames,
            (Options & System.Text.RegularExpressions.RegexOptions.ECMAScript) != 0, _wholeLiteralCaptureNumber);

    private Utf8AnalyzedReplacement PrepareBorrowedReplacement(ReadOnlySpan<byte> replacement, ReplacementPreparationControl control)
    {
        var maximumBytes = InvocationLimits.MaxWorkingBytes;
        using var construction = maximumBytes != 0
            ? Utf8ConstructionScope.RestrictAdditionalBytes(maximumBytes)
            : Utf8ConstructionScope.Enter();
        var budget = construction.Budget;
        if (control.IsUncontrolled)
        {
            var prepared = AnalyzeReplacement(replacement);
            construction.Complete(); return prepared;
        }
        var previousToken = budget.CancellationToken;
        var previousDeadline = budget.Deadline;
        var previousHook = budget.CheckpointHook;
        if ((!previousToken.CanBeCanceled || previousToken == control.Token) &&
            (previousDeadline.IsInfinite || ReferenceEquals(previousDeadline, control.Deadline)))
        {
            // The checkpoint checks the token and deadline after the existing hook.
            // Borrow the same authority instead of adding another callback.
            budget.CancellationToken = control.Token;
            budget.Deadline = control.Deadline;
        }
        else
        {
            var parentHook = previousHook;
            var currentControl = control;
            budget.CheckpointHook = () => { parentHook?.Invoke(); currentControl.Check(); };
        }
        try
        {
            control.Check();
            var result = AnalyzeReplacement(replacement);
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
        construction?.Checkpoint();

        // This scan acquires no payload and needs no new numeric owner. A live
        // parent shares its cumulative admission and interruption checkpoints.
        if (control.IsUncontrolled && (construction is null ||
            !construction.CancellationToken.CanBeCanceled && construction.Deadline.IsInfinite && construction.CheckpointHook is null))
        { Utf8Validation.ThrowIfInvalidOnly(template); return; }
        for (var offset = 0; offset < template.Length;)
        {
            construction?.Checkpoint();
            control.Check();
            var count = Utf8SliceBufferWork.Utf8BlockLength(template[offset..]);
            Utf8Validation.ThrowIfInvalidOnly(template.Slice(offset, count), offset);
            offset += count;
        }
        construction?.Checkpoint();
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
            return PrepareBorrowedReplacement(validatedTemplate, control);
        }
        catch (Utf8ExecutionDeadlineExpiredException)
        {
            control.Check();
            return PrepareBorrowedReplacement(validatedTemplate, control);
        }
    }

    private Utf8AnalyzedReplacement PrepareNativeReplacement(ReadOnlySpan<byte> validatedTemplate, ReplacementPreparationControl control)
    {
        // The synchronous cache factory already shares this byte limit and
        // interruption control. The analyzer owns the parser's rollback scope.
        var maximumBytes = InvocationLimits.MaxWorkingBytes;
        using var construction = maximumBytes != 0
            ? Utf8ConstructionScope.RestrictAdditionalBytes(maximumBytes)
            : Utf8ConstructionScope.Enter();
        if (control.IsUncontrolled)
        {
            var key = FreezeNativeReplacement(validatedTemplate, construction.Budget, control);
            var result = _replacementCache.GetOrAdd(key, this,
                static (source, regex) => regex.AnalyzeReplacement(source), control);
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
                budget.CheckpointHook = () => { parentHook?.Invoke(); currentControl.Check(); };
            }
            try
            {
                control.Check();
                // Source was validated before route selection. The major key
                // array and nested parser overlap share this admission owner.
                var key = FreezeNativeReplacement(validatedTemplate, budget, control);
                var result = _replacementCache.GetOrAdd(key, this,
                    static (source, regex) => regex.AnalyzeReplacement(source), control);
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
            budget.Checkpoint();
            source.Slice(offset, count).CopyTo(result.AsSpan(offset, count)); offset += count;
        }
        control.Check(); return result;
    }
}
