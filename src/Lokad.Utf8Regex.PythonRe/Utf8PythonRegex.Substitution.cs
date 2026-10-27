using System.Buffers;
using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Caching;
using Lokad.Utf8Regex.Internal;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Input;
using Lokad.Utf8Regex.Internal.Planning;

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
    private PythonReReplacementPlan GetReplacementPlan(string replacement) =>
        _replacementPlanCache.GetOrAdd(
            replacement,
            this,
            static (template, self) =>
            {
                if (self.ResourcePolicy.MaxWorkingBytes is not { } maximumBytes)
                    return PythonReReplacementParser.Parse(template, self.CaptureGroupCount, self._namedGroups);
                // No token/deadline to forward on this lane. Restrict parser
                // bytes without installing an otherwise empty per-work hook.
                using var construction = Utf8ConstructionScope.RestrictAdditionalBytes(maximumBytes);
                var plan = PythonReReplacementParser.Parse(template, self.CaptureGroupCount, self._namedGroups);
                construction.Complete();
                return plan;
            },
            default(Utf8ConstructionTextNoControl), default(PythonReReplacementRetainedStorage));

    private PythonReReplacementPlan GetReplacementPlan(ReadOnlySpan<byte> input, string replacement,
        Utf8SliceInvocation invocation, CancellationToken cancellationToken)
    {
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
                    return GetReplacementPlan(replacement);
                return _replacementPlanCache.GetOrAdd(replacement, (Self: this, Control: control),
                    static (template, state) => state.Self.PrepareReplacementPlan(template, state.Control),
                    control, default(PythonReReplacementRetainedStorage));
            }
            catch (OperationCanceledException failure) when (failure.CancellationToken != cancellationToken)
            {
                // Pending entries share factory failures, but another caller's
                // interruption is not this call's authority. Prepare once with
                // our controls instead of entering another shared retry loop.
                control.Check();
                return PrepareReplacementPlan(replacement, control);
            }
            catch (Utf8ExecutionDeadlineExpiredException)
            {
                control.Check(); // Rethrow if our own deadline expired.
                return PrepareReplacementPlan(replacement, control);
            }
        }
        catch (Utf8ExecutionDeadlineExpiredException)
        {
            throw CreateMatchTimeoutException(input, Pattern);
        }
    }

    private PythonReReplacementPlan PrepareReplacementPlan(string replacement,
        PythonRePreparationInterruptionControl control)
    {
        // Borrow existing authority fields on a cache miss; a distinct parent
        // retains its original callback chain. Restore both fields and hook.
        // Working bytes also cap this phase's additional parser capacity;
        // construction work/depth and retained-cache admission stay separate.
        using var construction = ResourcePolicy.MaxWorkingBytes is { } maximumBytes
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
        return SubnTemplateCore(input, replacement, count, startOffsetInBytes, CancellationToken.None).ResultBytes;
    }

    /// <summary>Replaces matches after a byte offset and returns owned UTF-8 bytes, observing cancellation during validation, matching and required result processing.</summary>
    /// <param name="input">The well-formed UTF-8 input to match against.</param>
    /// <param name="replacement">The replacement text.</param>
    /// <param name="count">The maximum number of replacements to perform.</param>
    /// <param name="startOffsetInBytes">The zero-based UTF-8 byte offset where matching starts.</param>
    /// <param name="cancellationToken">The token to observe at entry and during bounded operation checkpoints.</param>
    /// <exception cref="OperationCanceledException">Cancellation was requested through the supplied token.</exception>
    public byte[] Replace(ReadOnlySpan<byte> input, string replacement, int count, int startOffsetInBytes, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return SubnTemplateCore(input, replacement, count, startOffsetInBytes, cancellationToken).ResultBytes;
    }

    /// <summary>Replaces all matches and returns a string.</summary>
    public string ReplaceToString(ReadOnlySpan<byte> input, string replacement) =>
        ReplaceToString(input, replacement, 0, 0);

    /// <summary>Replaces at most the requested number of matches and returns a string.</summary>
    public string ReplaceToString(ReadOnlySpan<byte> input, string replacement, int count) =>
        ReplaceToString(input, replacement, count, 0);

    /// <summary>Replaces matches after a byte offset and returns a string.</summary>
    public string ReplaceToString(ReadOnlySpan<byte> input, string replacement, int count, int startOffsetInBytes)
    {
        return SubnToStringTemplateCore(input, replacement, count, startOffsetInBytes, CancellationToken.None).ResultText;
    }

    /// <summary>Replaces matches after a byte offset and returns a string, observing cancellation during validation, matching and required result processing.</summary>
    /// <param name="input">The well-formed UTF-8 input to match against.</param>
    /// <param name="replacement">The replacement text.</param>
    /// <param name="count">The maximum number of replacements to perform.</param>
    /// <param name="startOffsetInBytes">The zero-based UTF-8 byte offset where matching starts.</param>
    /// <param name="cancellationToken">The token to observe at entry and during bounded operation checkpoints.</param>
    /// <exception cref="OperationCanceledException">Cancellation was requested through the supplied token.</exception>
    public string ReplaceToString(ReadOnlySpan<byte> input, string replacement, int count, int startOffsetInBytes, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return SubnToStringTemplateCore(input, replacement, count, startOffsetInBytes, cancellationToken).ResultText;
    }

    /// <summary>Replaces all matches and returns the string plus replacement count.</summary>

    public Utf8PythonSubnResult SubnToString(ReadOnlySpan<byte> input, string replacement) =>
        SubnToString(input, replacement, 0, 0);

    /// <summary>Replaces a bounded number of matches and returns the string plus count.</summary>
    public Utf8PythonSubnResult SubnToString(ReadOnlySpan<byte> input, string replacement, int count) =>
        SubnToString(input, replacement, count, 0);

    /// <summary>Replaces matches after a byte offset and returns the string plus count.</summary>
    public Utf8PythonSubnResult SubnToString(
        ReadOnlySpan<byte> input,
        string replacement,
        int count,
        int startOffsetInBytes) => SubnToStringTemplateCore(input, replacement, count, startOffsetInBytes, CancellationToken.None);

    private Utf8PythonSubnResult SubnToStringTemplateCore(
        ReadOnlySpan<byte> input,
        string replacement,
        int count,
        int startOffsetInBytes, CancellationToken cancellationToken)
    {
        ValidateStartOffset(input, startOffsetInBytes);
        var invocation = new Utf8SliceInvocation(InvocationLimits, MatchTimeout, cancellationToken);
        var scalarPlan = GetReplacementPlan(input, replacement, invocation, cancellationToken);
        if (!HasFiniteExecutionLimits && !cancellationToken.CanBeCanceled && MatchTimeout == Timeout.InfiniteTimeSpan &&
            TryReplaceViaUtf8Literal(input, scalarPlan, count, startOffsetInBytes, true, ref invocation, out _, out var literalText, out var literalCount))
            return new Utf8PythonSubnResult { ResultText = literalText, ReplacementCount = literalCount };
        SubstituteScalar(input, scalarPlan,
                static (ReadOnlySpan<byte> source, PythonReReplacementPlan plan, scoped ref PythonReScalarMatchCursor cursor, scoped ref Utf8SliceOutputWriter writer) =>
                    plan.AppendToUtf8(source, ref cursor, ref writer),
                count, startOffsetInBytes, invocation, true, false, out _, out var scalarResult);
        return scalarResult;
    }

    /// <summary>Replaces matches after a byte offset and returns the string plus count, observing cancellation during validation, matching and required result processing.</summary>
    /// <param name="input">The well-formed UTF-8 input to match against.</param>
    /// <param name="replacement">The replacement text.</param>
    /// <param name="count">The maximum number of replacements to perform.</param>
    /// <param name="startOffsetInBytes">The zero-based UTF-8 byte offset where matching starts.</param>
    /// <param name="cancellationToken">The token to observe at entry and during bounded operation checkpoints.</param>
    /// <exception cref="OperationCanceledException">Cancellation was requested through the supplied token.</exception>
    public Utf8PythonSubnResult SubnToString(ReadOnlySpan<byte> input, string replacement, int count, int startOffsetInBytes, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return SubnToStringTemplateCore(input, replacement, count, startOffsetInBytes, cancellationToken);
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
        int startOffsetInBytes) => SubnTemplateCore(input, replacement, count, startOffsetInBytes, CancellationToken.None);

    private Utf8PythonSubnUtf8Result SubnTemplateCore(
        ReadOnlySpan<byte> input,
        string replacement,
        int count,
        int startOffsetInBytes, CancellationToken cancellationToken)
    {
        ValidateStartOffset(input, startOffsetInBytes);
        var invocation = new Utf8SliceInvocation(InvocationLimits, MatchTimeout, cancellationToken);
        var scalarPlan = GetReplacementPlan(input, replacement, invocation, cancellationToken);
        if (!HasFiniteExecutionLimits && !cancellationToken.CanBeCanceled && MatchTimeout == Timeout.InfiniteTimeSpan &&
            TryReplaceViaUtf8Literal(input, scalarPlan, count, startOffsetInBytes, false, ref invocation, out var literalBytes, out _, out var literalCount))
            return new Utf8PythonSubnUtf8Result { ResultBytes = literalBytes, ReplacementCount = literalCount };
        SubstituteScalar(input, scalarPlan,
                static (ReadOnlySpan<byte> source, PythonReReplacementPlan plan, scoped ref PythonReScalarMatchCursor cursor, scoped ref Utf8SliceOutputWriter writer) =>
                    plan.AppendToUtf8(source, ref cursor, ref writer),
                count, startOffsetInBytes, invocation, false, false, out var scalarResult, out _);
        return scalarResult;
    }

    /// <summary>Replaces matches after a byte offset and returns UTF-8 bytes plus count, observing cancellation during validation, matching and required result processing.</summary>
    /// <param name="input">The well-formed UTF-8 input to match against.</param>
    /// <param name="replacement">The replacement text.</param>
    /// <param name="count">The maximum number of replacements to perform.</param>
    /// <param name="startOffsetInBytes">The zero-based UTF-8 byte offset where matching starts.</param>
    /// <param name="cancellationToken">The token to observe at entry and during bounded operation checkpoints.</param>
    /// <exception cref="OperationCanceledException">Cancellation was requested through the supplied token.</exception>
    public Utf8PythonSubnUtf8Result Subn(ReadOnlySpan<byte> input, string replacement, int count, int startOffsetInBytes, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return SubnTemplateCore(input, replacement, count, startOffsetInBytes, cancellationToken);
    }

    /// <summary>Replaces all matches with a stateful string evaluator and returns UTF-8 bytes.</summary>
    public byte[] Replace<TState>(
        ReadOnlySpan<byte> input,
        TState state,
        Utf8PythonMatchEvaluator<TState> evaluator) => Replace(input, state, evaluator, 0, 0);

    /// <summary>Replaces a bounded number of matches with a stateful string evaluator.</summary>
    public byte[] Replace<TState>(
        ReadOnlySpan<byte> input,
        TState state,
        Utf8PythonMatchEvaluator<TState> evaluator,
        int count) => Replace(input, state, evaluator, count, 0);

    /// <summary>Replaces matches after a byte offset with a stateful string evaluator.</summary>
    public byte[] Replace<TState>(
        ReadOnlySpan<byte> input,
        TState state,
        Utf8PythonMatchEvaluator<TState> evaluator,
        int count,
        int startOffsetInBytes)
    {
        return Subn(input, state, evaluator, count, startOffsetInBytes).ResultBytes;
    }

    /// <summary>Replaces matches after a byte offset with a stateful string evaluator, observing cancellation during validation, matching and required result processing.</summary>
    /// <param name="input">The well-formed UTF-8 input to match against.</param>
    /// <param name="state">Additional state passed to the evaluator.</param>
    /// <param name="evaluator">The callback invoked for each match.</param>
    /// <param name="count">The maximum number of replacements to perform.</param>
    /// <param name="startOffsetInBytes">The zero-based UTF-8 byte offset where matching starts.</param>
    /// <typeparam name="TState">The type of additional state passed to the evaluator.</typeparam>
    /// <param name="cancellationToken">The token to observe at entry and during bounded operation checkpoints.</param>
    /// <exception cref="OperationCanceledException">Cancellation was requested through the supplied token.</exception>
    public byte[] Replace<TState>(ReadOnlySpan<byte> input, TState state, Utf8PythonMatchEvaluator<TState> evaluator, int count, int startOffsetInBytes, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return SubnEvaluatorCore(input, state, evaluator, count, startOffsetInBytes, cancellationToken).ResultBytes;
    }

    /// <summary>Replaces all matches with a stateful evaluator and returns a string.</summary>
    public string ReplaceToString<TState>(
        ReadOnlySpan<byte> input,
        TState state,
        Utf8PythonMatchEvaluator<TState> evaluator) => ReplaceToString(input, state, evaluator, 0, 0);

    /// <summary>Replaces a bounded number of matches with a stateful evaluator.</summary>
    public string ReplaceToString<TState>(
        ReadOnlySpan<byte> input,
        TState state,
        Utf8PythonMatchEvaluator<TState> evaluator,
        int count) => ReplaceToString(input, state, evaluator, count, 0);

    /// <summary>Replaces matches after a byte offset with a stateful evaluator.</summary>
    public string ReplaceToString<TState>(
        ReadOnlySpan<byte> input,
        TState state,
        Utf8PythonMatchEvaluator<TState> evaluator,
        int count,
        int startOffsetInBytes)
    {
        return SubnToString(input, state, evaluator, count, startOffsetInBytes).ResultText;
    }

    /// <summary>Replaces matches after a byte offset with a stateful evaluator and returns a string, observing cancellation during validation, matching and required result processing.</summary>
    /// <param name="input">The well-formed UTF-8 input to match against.</param>
    /// <param name="state">Additional state passed to the evaluator.</param>
    /// <param name="evaluator">The callback invoked for each match.</param>
    /// <param name="count">The maximum number of replacements to perform.</param>
    /// <param name="startOffsetInBytes">The zero-based UTF-8 byte offset where matching starts.</param>
    /// <typeparam name="TState">The type of additional state passed to the evaluator.</typeparam>
    /// <param name="cancellationToken">The token to observe at entry and during bounded operation checkpoints.</param>
    /// <exception cref="OperationCanceledException">Cancellation was requested through the supplied token.</exception>
    public string ReplaceToString<TState>(ReadOnlySpan<byte> input, TState state, Utf8PythonMatchEvaluator<TState> evaluator, int count, int startOffsetInBytes, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return SubnToStringEvaluatorCore(input, state, evaluator, count, startOffsetInBytes, cancellationToken).ResultText;
    }

    /// <summary>Evaluates all replacements and returns the string plus replacement count.</summary>
    public Utf8PythonSubnResult SubnToString<TState>(
        ReadOnlySpan<byte> input,
        TState state,
        Utf8PythonMatchEvaluator<TState> evaluator) => SubnToString(input, state, evaluator, 0, 0);

    /// <summary>Evaluates a bounded number of replacements and returns the string plus count.</summary>
    public Utf8PythonSubnResult SubnToString<TState>(
        ReadOnlySpan<byte> input,
        TState state,
        Utf8PythonMatchEvaluator<TState> evaluator,
        int count) => SubnToString(input, state, evaluator, count, 0);

    /// <summary>Evaluates replacements after a byte offset and returns the string plus count.</summary>
    public Utf8PythonSubnResult SubnToString<TState>(
        ReadOnlySpan<byte> input,
        TState state,
        Utf8PythonMatchEvaluator<TState> evaluator,
        int count,
        int startOffsetInBytes)
    {
        return SubnToStringEvaluatorCore(input, state, evaluator, count, startOffsetInBytes, default);
    }

    private Utf8PythonSubnResult SubnToStringEvaluatorCore<TState>(
        ReadOnlySpan<byte> input,
        TState state,
        Utf8PythonMatchEvaluator<TState> evaluator,
        int count,
        int startOffsetInBytes,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(evaluator);
        SubstituteScalar(input, (Value: state, Evaluator: evaluator, Names: _nameEntries),
                static (ReadOnlySpan<byte> source, (TState Value, Utf8PythonMatchEvaluator<TState> Evaluator, PythonReNameEntry[] Names) s, scoped ref PythonReScalarMatchCursor cursor, scoped ref Utf8SliceOutputWriter writer) =>
                    cursor.AppendText(ref writer, s.Evaluator(s.Value, cursor.SnapshotDetailed(s.Names))),
                count, startOffsetInBytes, new Utf8SliceInvocation(InvocationLimits, MatchTimeout, cancellationToken),
                true, false, out _, out var scalarResult);
        return scalarResult;
    }

    /// <summary>Evaluates replacements after a byte offset and returns the string plus count, observing cancellation during validation, matching and required result processing.</summary>
    /// <param name="input">The well-formed UTF-8 input to match against.</param>
    /// <param name="state">Additional state passed to the evaluator.</param>
    /// <param name="evaluator">The callback invoked for each match.</param>
    /// <param name="count">The maximum number of replacements to perform.</param>
    /// <param name="startOffsetInBytes">The zero-based UTF-8 byte offset where matching starts.</param>
    /// <typeparam name="TState">The type of additional state passed to the evaluator.</typeparam>
    /// <param name="cancellationToken">The token to observe at entry and during bounded operation checkpoints.</param>
    /// <exception cref="OperationCanceledException">Cancellation was requested through the supplied token.</exception>
    public Utf8PythonSubnResult SubnToString<TState>(ReadOnlySpan<byte> input, TState state, Utf8PythonMatchEvaluator<TState> evaluator, int count, int startOffsetInBytes, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return SubnToStringEvaluatorCore(input, state, evaluator, count, startOffsetInBytes, cancellationToken);
    }

    /// <summary>Evaluates all string replacements and returns UTF-8 bytes plus count.</summary>
    public Utf8PythonSubnUtf8Result Subn<TState>(
        ReadOnlySpan<byte> input,
        TState state,
        Utf8PythonMatchEvaluator<TState> evaluator) => Subn(input, state, evaluator, 0, 0);

    /// <summary>Evaluates a bounded number of string replacements and returns UTF-8 bytes plus count.</summary>
    public Utf8PythonSubnUtf8Result Subn<TState>(
        ReadOnlySpan<byte> input,
        TState state,
        Utf8PythonMatchEvaluator<TState> evaluator,
        int count) => Subn(input, state, evaluator, count, 0);

    /// <summary>Evaluates string replacements after a byte offset and returns UTF-8 bytes plus count.</summary>
    public Utf8PythonSubnUtf8Result Subn<TState>(
        ReadOnlySpan<byte> input,
        TState state,
        Utf8PythonMatchEvaluator<TState> evaluator,
        int count,
        int startOffsetInBytes)
    {
        return SubnEvaluatorCore(input, state, evaluator, count, startOffsetInBytes, default);
    }

    private Utf8PythonSubnUtf8Result SubnEvaluatorCore<TState>(
        ReadOnlySpan<byte> input,
        TState state,
        Utf8PythonMatchEvaluator<TState> evaluator,
        int count,
        int startOffsetInBytes,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(evaluator);
        SubstituteScalar(input, (Value: state, Evaluator: evaluator, Names: _nameEntries),
                static (ReadOnlySpan<byte> source, (TState Value, Utf8PythonMatchEvaluator<TState> Evaluator, PythonReNameEntry[] Names) s, scoped ref PythonReScalarMatchCursor cursor, scoped ref Utf8SliceOutputWriter writer) =>
                    cursor.AppendText(ref writer, s.Evaluator(s.Value, cursor.SnapshotDetailed(s.Names))),
                count, startOffsetInBytes, new Utf8SliceInvocation(InvocationLimits, MatchTimeout, cancellationToken),
                false, false, out var scalarResult, out _);
        return scalarResult;
    }

    /// <summary>Evaluates string replacements after a byte offset and returns UTF-8 bytes plus count, observing cancellation during validation, matching and required result processing.</summary>
    /// <param name="input">The well-formed UTF-8 input to match against.</param>
    /// <param name="state">Additional state passed to the evaluator.</param>
    /// <param name="evaluator">The callback invoked for each match.</param>
    /// <param name="count">The maximum number of replacements to perform.</param>
    /// <param name="startOffsetInBytes">The zero-based UTF-8 byte offset where matching starts.</param>
    /// <typeparam name="TState">The type of additional state passed to the evaluator.</typeparam>
    /// <param name="cancellationToken">The token to observe at entry and during bounded operation checkpoints.</param>
    /// <exception cref="OperationCanceledException">Cancellation was requested through the supplied token.</exception>
    public Utf8PythonSubnUtf8Result Subn<TState>(ReadOnlySpan<byte> input, TState state, Utf8PythonMatchEvaluator<TState> evaluator, int count, int startOffsetInBytes, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return SubnEvaluatorCore(input, state, evaluator, count, startOffsetInBytes, cancellationToken);
    }

    /// <summary>Replaces all matches with a stateful UTF-8 evaluator.</summary>
    public byte[] Replace<TState>(
        ReadOnlySpan<byte> input,
        TState state,
        Utf8PythonUtf8MatchEvaluator<TState> evaluator) => Replace(input, state, evaluator, 0, 0);

    /// <summary>Replaces a bounded number of matches with a stateful UTF-8 evaluator.</summary>
    public byte[] Replace<TState>(
        ReadOnlySpan<byte> input,
        TState state,
        Utf8PythonUtf8MatchEvaluator<TState> evaluator,
        int count) => Replace(input, state, evaluator, count, 0);

    /// <summary>Replaces matches after a byte offset with a stateful UTF-8 evaluator.</summary>
    public byte[] Replace<TState>(
        ReadOnlySpan<byte> input,
        TState state,
        Utf8PythonUtf8MatchEvaluator<TState> evaluator,
        int count,
        int startOffsetInBytes)
    {
        return Subn(input, state, evaluator, count, startOffsetInBytes).ResultBytes;
    }

    /// <summary>Replaces matches after a byte offset with a stateful UTF-8 evaluator, observing cancellation during validation, matching and required result processing.</summary>
    /// <param name="input">The well-formed UTF-8 input to match against.</param>
    /// <param name="state">Additional state passed to the evaluator.</param>
    /// <param name="evaluator">The callback invoked for each match.</param>
    /// <param name="count">The maximum number of replacements to perform.</param>
    /// <param name="startOffsetInBytes">The zero-based UTF-8 byte offset where matching starts.</param>
    /// <typeparam name="TState">The type of additional state passed to the evaluator.</typeparam>
    /// <param name="cancellationToken">The token to observe at entry and during bounded operation checkpoints.</param>
    /// <exception cref="OperationCanceledException">Cancellation was requested through the supplied token.</exception>
    public byte[] Replace<TState>(ReadOnlySpan<byte> input, TState state, Utf8PythonUtf8MatchEvaluator<TState> evaluator, int count, int startOffsetInBytes, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return SubnUtf8EvaluatorCore(input, state, evaluator, count, startOffsetInBytes, cancellationToken).ResultBytes;
    }

    /// <summary>Evaluates all UTF-8 replacements and returns bytes plus replacement count.</summary>
    public Utf8PythonSubnUtf8Result Subn<TState>(
        ReadOnlySpan<byte> input,
        TState state,
        Utf8PythonUtf8MatchEvaluator<TState> evaluator) => Subn(input, state, evaluator, 0, 0);

    /// <summary>Evaluates a bounded number of UTF-8 replacements and returns bytes plus count.</summary>
    public Utf8PythonSubnUtf8Result Subn<TState>(
        ReadOnlySpan<byte> input,
        TState state,
        Utf8PythonUtf8MatchEvaluator<TState> evaluator,
        int count) => Subn(input, state, evaluator, count, 0);

    /// <summary>Evaluates UTF-8 replacements after a byte offset and returns bytes plus count.</summary>
    public Utf8PythonSubnUtf8Result Subn<TState>(
        ReadOnlySpan<byte> input,
        TState state,
        Utf8PythonUtf8MatchEvaluator<TState> evaluator,
        int count,
        int startOffsetInBytes)
    {
        return SubnUtf8EvaluatorCore(input, state, evaluator, count, startOffsetInBytes, default);
    }

    private Utf8PythonSubnUtf8Result SubnUtf8EvaluatorCore<TState>(
        ReadOnlySpan<byte> input,
        TState state,
        Utf8PythonUtf8MatchEvaluator<TState> evaluator,
        int count,
        int startOffsetInBytes,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(evaluator);
        SubstituteScalar(input, (Value: state, Evaluator: evaluator, Names: _nameEntries),
                static (ReadOnlySpan<byte> source, (TState Value, Utf8PythonUtf8MatchEvaluator<TState> Evaluator, PythonReNameEntry[] Names) s, scoped ref PythonReScalarMatchCursor cursor, scoped ref Utf8SliceOutputWriter writer) =>
                    cursor.AppendBytes(ref writer, s.Evaluator(s.Value, cursor.SnapshotDetailed(s.Names))),
                count, startOffsetInBytes, new Utf8SliceInvocation(InvocationLimits, MatchTimeout, cancellationToken),
                false, true, out var scalarResult, out _);
        return scalarResult;
    }

    /// <summary>Evaluates UTF-8 replacements after a byte offset and returns bytes plus count, observing cancellation during validation, matching and required result processing.</summary>
    /// <param name="input">The well-formed UTF-8 input to match against.</param>
    /// <param name="state">Additional state passed to the evaluator.</param>
    /// <param name="evaluator">The callback invoked for each match.</param>
    /// <param name="count">The maximum number of replacements to perform.</param>
    /// <param name="startOffsetInBytes">The zero-based UTF-8 byte offset where matching starts.</param>
    /// <typeparam name="TState">The type of additional state passed to the evaluator.</typeparam>
    /// <param name="cancellationToken">The token to observe at entry and during bounded operation checkpoints.</param>
    /// <exception cref="OperationCanceledException">Cancellation was requested through the supplied token.</exception>
    public Utf8PythonSubnUtf8Result Subn<TState>(ReadOnlySpan<byte> input, TState state, Utf8PythonUtf8MatchEvaluator<TState> evaluator, int count, int startOffsetInBytes, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return SubnUtf8EvaluatorCore(input, state, evaluator, count, startOffsetInBytes, cancellationToken);
    }

}
