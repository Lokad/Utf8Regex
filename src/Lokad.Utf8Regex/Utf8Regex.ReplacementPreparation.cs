using Lokad.Utf8Regex.Internal;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.FrontEnd;
using Lokad.Utf8Regex.Internal.Input;
using Lokad.Utf8Regex.Internal.Replacement;
using System.Text;

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

    private Utf8AnalyzedReplacement PrepareReplacement(string replacement, ReplacementPreparationControl control)
    {
        using var construction = Utf8ConstructionScope.Enter();
        var previous = construction.Budget.CheckpointHook;
        construction.Budget.CheckpointHook = steps => { previous?.Invoke(steps); control.Check(); };
        try
        {
            control.Check();
            var result = Utf8FrontEndReplacementAnalyzer.Analyze(replacement, _groupNumbers, _groupNames,
                (Options & System.Text.RegularExpressions.RegexOptions.ECMAScript) != 0);
            control.Check();
            construction.Complete();
            return result;
        }
        finally { construction.Budget.CheckpointHook = previous; }
    }

    private static void ValidateReplacementTemplateUtf8(ReadOnlySpan<byte> template, ReplacementPreparationControl control)
    {
        if (control.IsUncontrolled) { _ = Utf8Validation.Validate(template); return; }
        for (var offset = 0; offset < template.Length;)
        {
            control.Check();
            var count = Utf8SliceBufferWork.Utf8BlockLength(template[offset..]);
            if (!Utf8ValidationCore.TryValidate(template.Slice(offset, count), false, out _, out var error))
                throw Utf8Validation.CreateInvalidUtf8Exception(offset + error);
            offset += count;
        }
        control.Check();
    }

    private Utf8AnalyzedReplacement GetParsedReplacement(ReadOnlySpan<byte> validatedTemplate, ReplacementPreparationControl control)
    {
        using var construction = Utf8ConstructionScope.Enter();
        var previous = construction.Budget.CheckpointHook;
        construction.Budget.CheckpointHook = steps => { previous?.Invoke(steps); control.Check(); };
        try
        {
            control.Check();
            // The template was strictly validated before route selection. Its
            // single decoded key still needs admission before acquisition.
            var text = construction.Budget.DecodeUtf8(validatedTemplate, Encoding.UTF8);
            var result = GetParsedReplacement(text, control);
            control.Check();
            construction.Complete();
            return result;
        }
        finally { construction.Budget.CheckpointHook = previous; }
    }
}
