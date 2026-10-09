using System.Buffers;
using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Caching;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Input;
using Lokad.Utf8Regex.Internal.Planning;

namespace Lokad.Utf8Regex.PythonRe;

// PLAN.md D09: substitution entry points, split from the Python facade along the substitution ownership boundary.
/// <summary>Substitution entry points for <see cref="Utf8PythonRegex"/>.</summary>
public sealed partial class Utf8PythonRegex
{
    /// <summary>Replaces all matches and returns owned UTF-8 bytes.</summary>
    public byte[] Replace(ReadOnlySpan<byte> input, string replacement) => Replace(input, replacement, 0, 0);

    /// <summary>Replaces at most the requested number of matches and returns owned UTF-8 bytes.</summary>
    public byte[] Replace(ReadOnlySpan<byte> input, string replacement, int count) =>
        Replace(input, replacement, count, 0);

    // PLAN.md D03: replacement templates are parsed once per distinct template and shared across operations; failures are never retained.
    private PythonReReplacementPlan GetReplacementPlan(string replacement) =>
        _replacementPlanCache.GetOrAdd(
            replacement,
            this,
            static (template, self) => PythonReReplacementParser.Parse(template, self._translation.CaptureGroupCount, self._namedGroups));

    /// <summary>Replaces matches after a byte offset and returns owned UTF-8 bytes.</summary>
    public byte[] Replace(ReadOnlySpan<byte> input, string replacement, int count, int startOffsetInBytes)
    {
        return SubnTemplateCore(input, replacement, count, startOffsetInBytes, CancellationToken.None).ResultBytes;
    }

    /// <summary>Replaces matches after a byte offset and returns owned UTF-8 bytes, observing cancellation before matching begins.</summary>
    /// <param name="input">The well-formed UTF-8 input to match against.</param>
    /// <param name="replacement">The replacement text.</param>
    /// <param name="count">The maximum number of replacements to perform.</param>
    /// <param name="startOffsetInBytes">The zero-based UTF-8 byte offset where matching starts.</param>
    /// <param name="cancellationToken">The token to observe before matching begins.</param>
    /// <exception cref="OperationCanceledException">The token was already canceled.</exception>
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

    // R04: template substitution on literal shapes discovers through the shared owned cursor
    // and expands every replacement from owned group spans; the first hit validates the branch
    // channel before any output, so an unknown branch still declines cleanly to managed routes.

    /// <summary>Replaces matches after a byte offset and returns a string, observing cancellation before matching begins.</summary>
    /// <param name="input">The well-formed UTF-8 input to match against.</param>
    /// <param name="replacement">The replacement text.</param>
    /// <param name="count">The maximum number of replacements to perform.</param>
    /// <param name="startOffsetInBytes">The zero-based UTF-8 byte offset where matching starts.</param>
    /// <param name="cancellationToken">The token to observe before matching begins.</param>
    /// <exception cref="OperationCanceledException">The token was already canceled.</exception>
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
        var scalarPlan = GetReplacementPlan(replacement);
        if (!cancellationToken.CanBeCanceled && MatchTimeout == Timeout.InfiniteTimeSpan &&
            TryReplaceViaUtf8Literal(input, scalarPlan, count, startOffsetInBytes, out var literalBytes, out var literalCount))
            return new Utf8PythonSubnResult { ResultText = Encoding.UTF8.GetString(literalBytes), ReplacementCount = literalCount };
        SubstituteScalar(input, scalarPlan,
                static (ReadOnlySpan<byte> source, PythonReReplacementPlan plan, scoped ref PythonReScalarMatchCursor cursor) => plan.ExpandToUtf8(source, cursor.SnapshotGroups()),
                count, startOffsetInBytes, cancellationToken, out var scalarResult);
        return new Utf8PythonSubnResult { ResultText = Encoding.UTF8.GetString(scalarResult.ResultBytes), ReplacementCount = scalarResult.ReplacementCount };
    }

    /// <summary>Replaces matches after a byte offset and returns the string plus count, observing cancellation before matching begins.</summary>
    /// <param name="input">The well-formed UTF-8 input to match against.</param>
    /// <param name="replacement">The replacement text.</param>
    /// <param name="count">The maximum number of replacements to perform.</param>
    /// <param name="startOffsetInBytes">The zero-based UTF-8 byte offset where matching starts.</param>
    /// <param name="cancellationToken">The token to observe before matching begins.</param>
    /// <exception cref="OperationCanceledException">The token was already canceled.</exception>
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
        var scalarPlan = GetReplacementPlan(replacement);
        if (!cancellationToken.CanBeCanceled && MatchTimeout == Timeout.InfiniteTimeSpan &&
            TryReplaceViaUtf8Literal(input, scalarPlan, count, startOffsetInBytes, out var literalBytes, out var literalCount))
            return new Utf8PythonSubnUtf8Result { ResultBytes = literalBytes, ReplacementCount = literalCount };
        SubstituteScalar(input, scalarPlan,
                static (ReadOnlySpan<byte> source, PythonReReplacementPlan plan, scoped ref PythonReScalarMatchCursor cursor) => plan.ExpandToUtf8(source, cursor.SnapshotGroups()),
                count, startOffsetInBytes, cancellationToken, out var scalarResult);
        return scalarResult;
    }

    /// <summary>Replaces matches after a byte offset and returns UTF-8 bytes plus count, observing cancellation before matching begins.</summary>
    /// <param name="input">The well-formed UTF-8 input to match against.</param>
    /// <param name="replacement">The replacement text.</param>
    /// <param name="count">The maximum number of replacements to perform.</param>
    /// <param name="startOffsetInBytes">The zero-based UTF-8 byte offset where matching starts.</param>
    /// <param name="cancellationToken">The token to observe before matching begins.</param>
    /// <exception cref="OperationCanceledException">The token was already canceled.</exception>
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

    /// <summary>Replaces matches after a byte offset with a stateful string evaluator, observing cancellation before matching begins.</summary>
    /// <param name="input">The well-formed UTF-8 input to match against.</param>
    /// <param name="state">Additional state passed to the evaluator.</param>
    /// <param name="evaluator">The callback invoked for each match.</param>
    /// <param name="count">The maximum number of replacements to perform.</param>
    /// <param name="startOffsetInBytes">The zero-based UTF-8 byte offset where matching starts.</param>
    /// <typeparam name="TState">The type of additional state passed to the evaluator.</typeparam>
    /// <param name="cancellationToken">The token to observe before matching begins.</param>
    /// <exception cref="OperationCanceledException">The token was already canceled.</exception>
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

    // R04: evaluator substitution on literal shapes discovers through the shared owned cursor
    // and shapes every callback argument from owned spans; the first hit validates the branch
    // channel before any output, so an unknown branch still declines cleanly to managed routes.

    /// <summary>Replaces matches after a byte offset with a stateful evaluator and returns a string, observing cancellation before matching begins.</summary>
    /// <param name="input">The well-formed UTF-8 input to match against.</param>
    /// <param name="state">Additional state passed to the evaluator.</param>
    /// <param name="evaluator">The callback invoked for each match.</param>
    /// <param name="count">The maximum number of replacements to perform.</param>
    /// <param name="startOffsetInBytes">The zero-based UTF-8 byte offset where matching starts.</param>
    /// <typeparam name="TState">The type of additional state passed to the evaluator.</typeparam>
    /// <param name="cancellationToken">The token to observe before matching begins.</param>
    /// <exception cref="OperationCanceledException">The token was already canceled.</exception>
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
        SubstituteScalar(input, (Value: state, Evaluator: evaluator, Names: _nameEntries),
                static (ReadOnlySpan<byte> source, (TState Value, Utf8PythonMatchEvaluator<TState> Evaluator, PythonReNameEntry[] Names) s, scoped ref PythonReScalarMatchCursor cursor) => Encoding.UTF8.GetBytes(s.Evaluator(s.Value, cursor.SnapshotDetailed(s.Names))),
                count, startOffsetInBytes, cancellationToken, out var scalarResult);
        return new Utf8PythonSubnResult { ResultText = Encoding.UTF8.GetString(scalarResult.ResultBytes), ReplacementCount = scalarResult.ReplacementCount };
    }

    /// <summary>Evaluates replacements after a byte offset and returns the string plus count, observing cancellation before matching begins.</summary>
    /// <param name="input">The well-formed UTF-8 input to match against.</param>
    /// <param name="state">Additional state passed to the evaluator.</param>
    /// <param name="evaluator">The callback invoked for each match.</param>
    /// <param name="count">The maximum number of replacements to perform.</param>
    /// <param name="startOffsetInBytes">The zero-based UTF-8 byte offset where matching starts.</param>
    /// <typeparam name="TState">The type of additional state passed to the evaluator.</typeparam>
    /// <param name="cancellationToken">The token to observe before matching begins.</param>
    /// <exception cref="OperationCanceledException">The token was already canceled.</exception>
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
        SubstituteScalar(input, (Value: state, Evaluator: evaluator, Names: _nameEntries),
                static (ReadOnlySpan<byte> source, (TState Value, Utf8PythonMatchEvaluator<TState> Evaluator, PythonReNameEntry[] Names) s, scoped ref PythonReScalarMatchCursor cursor) => Encoding.UTF8.GetBytes(s.Evaluator(s.Value, cursor.SnapshotDetailed(s.Names))),
                count, startOffsetInBytes, cancellationToken, out var scalarResult);
        return scalarResult;
    }

    /// <summary>Evaluates string replacements after a byte offset and returns UTF-8 bytes plus count, observing cancellation before matching begins.</summary>
    /// <param name="input">The well-formed UTF-8 input to match against.</param>
    /// <param name="state">Additional state passed to the evaluator.</param>
    /// <param name="evaluator">The callback invoked for each match.</param>
    /// <param name="count">The maximum number of replacements to perform.</param>
    /// <param name="startOffsetInBytes">The zero-based UTF-8 byte offset where matching starts.</param>
    /// <typeparam name="TState">The type of additional state passed to the evaluator.</typeparam>
    /// <param name="cancellationToken">The token to observe before matching begins.</param>
    /// <exception cref="OperationCanceledException">The token was already canceled.</exception>
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

    /// <summary>Replaces matches after a byte offset with a stateful UTF-8 evaluator, observing cancellation before matching begins.</summary>
    /// <param name="input">The well-formed UTF-8 input to match against.</param>
    /// <param name="state">Additional state passed to the evaluator.</param>
    /// <param name="evaluator">The callback invoked for each match.</param>
    /// <param name="count">The maximum number of replacements to perform.</param>
    /// <param name="startOffsetInBytes">The zero-based UTF-8 byte offset where matching starts.</param>
    /// <typeparam name="TState">The type of additional state passed to the evaluator.</typeparam>
    /// <param name="cancellationToken">The token to observe before matching begins.</param>
    /// <exception cref="OperationCanceledException">The token was already canceled.</exception>
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
        SubstituteScalar(input, (Value: state, Evaluator: evaluator, Names: _nameEntries),
                static (ReadOnlySpan<byte> source, (TState Value, Utf8PythonUtf8MatchEvaluator<TState> Evaluator, PythonReNameEntry[] Names) s, scoped ref PythonReScalarMatchCursor cursor) => s.Evaluator(s.Value, cursor.SnapshotDetailed(s.Names)),
                count, startOffsetInBytes, cancellationToken, out var scalarResult);
        return scalarResult;
    }

    /// <summary>Evaluates UTF-8 replacements after a byte offset and returns bytes plus count, observing cancellation before matching begins.</summary>
    /// <param name="input">The well-formed UTF-8 input to match against.</param>
    /// <param name="state">Additional state passed to the evaluator.</param>
    /// <param name="evaluator">The callback invoked for each match.</param>
    /// <param name="count">The maximum number of replacements to perform.</param>
    /// <param name="startOffsetInBytes">The zero-based UTF-8 byte offset where matching starts.</param>
    /// <typeparam name="TState">The type of additional state passed to the evaluator.</typeparam>
    /// <param name="cancellationToken">The token to observe before matching begins.</param>
    /// <exception cref="OperationCanceledException">The token was already canceled.</exception>
    public Utf8PythonSubnUtf8Result Subn<TState>(ReadOnlySpan<byte> input, TState state, Utf8PythonUtf8MatchEvaluator<TState> evaluator, int count, int startOffsetInBytes, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return SubnUtf8EvaluatorCore(input, state, evaluator, count, startOffsetInBytes, cancellationToken);
    }

}
