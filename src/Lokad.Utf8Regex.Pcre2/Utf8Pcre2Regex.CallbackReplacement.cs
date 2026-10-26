using Lokad.Utf8Regex.Internal;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Input;

namespace Lokad.Utf8Regex.Pcre2;

/// <summary>Shapes PCRE2 evaluator replacements over original UTF-8 byte ranges.</summary>
public sealed partial class Utf8Pcre2Regex
{
    private interface IPcre2CallbackExpansion<TState>
    {
        void Validate();
        void Append(in Utf8Pcre2MatchContext match, ref Utf8ReplacementWriter writer, ref TState state);
    }

    private readonly struct Pcre2ByteCallback<TState>(Pcre2MatchEvaluator<TState> evaluator) : IPcre2CallbackExpansion<TState>
    {
        public void Validate() => ArgumentNullException.ThrowIfNull(evaluator);

        public void Append(in Utf8Pcre2MatchContext match, ref Utf8ReplacementWriter writer, ref TState state)
            => evaluator(in match, ref writer, ref state);
    }

    private readonly struct Pcre2StringCallback<TState>(Pcre2Utf16MatchEvaluator<TState> evaluator) : IPcre2CallbackExpansion<TState>
    {
        public void Validate() => ArgumentNullException.ThrowIfNull(evaluator);

        public void Append(in Utf8Pcre2MatchContext match, ref Utf8ReplacementWriter writer, ref TState state)
            => writer.Append(evaluator(in match, ref state).AsSpan());
    }

    // Every route borrows the same output scope. Default writers and writer
    // copies created in a callback join it; discarded identities are released
    // after the callback and nested replacements restore their caller's scope.
    private ref struct Pcre2CallbackReplacementOutput<TState, TExpansion>
        where TExpansion : struct, IPcre2CallbackExpansion<TState>
    {
        private readonly ReadOnlySpan<byte> _input;
        private readonly Utf8SliceByteOutputScope _scope;
        private Utf8ReplacementWriter _writer;
        private TExpansion _expansion;
        private TState _state;
        private int _position;

        internal Pcre2CallbackReplacementOutput(ReadOnlySpan<byte> input, Utf8SliceByteOutputScope scope,
            TExpansion expansion, TState state)
        {
            _input = input;
            _scope = scope;
            _writer = new(scope.CreateBuffer());
            _expansion = expansion;
            _state = state;
            _position = 0;
        }

        internal void Checkpoint() => Utf8SliceBufferWork.Checkpoint(ref _scope.Invocation);

        internal void Append(in Utf8Pcre2MatchContext context)
        {
            var match = context.Value;
            _writer.Append(_input.Slice(_position, match.StartOffsetInBytes - _position));
            Checkpoint();
            _expansion.Append(in context, ref _writer, ref _state);
            Checkpoint();
            _scope.Retain(_writer.OwnedOutput);
            _position = match.EndOffsetInBytes;
        }

        internal Pcre2ReplacementResult Complete(bool text)
        {
            _writer.Append(_input[_position..]);
            var output = _writer.GetOwnedOutput(_scope);
            return text ? new(output.ToValidatedString(), []) : new(string.Empty, output.ToArray());
        }
    }

    private Pcre2ReplacementResult ReplaceCallback<TState, TExpansion>(ReadOnlySpan<byte> input,
        TState state, TExpansion expansion, int startOffsetInBytes, Pcre2MatchOptions matchOptions,
        CancellationToken token, bool text)
        where TExpansion : struct, IPcre2CallbackExpansion<TState>
    {
        token.ThrowIfCancellationRequested();
        expansion.Validate();
        var invocation = Pcre2SliceDriver.CreateInvocation(_program.Request, token);
        invocation.ShareStorageForCursor();
        Utf8SliceByteOutputScope? scope = null;
        try
        {
            Pcre2SliceDriver.ThrowIfSliceBreached(Utf8ValidatedInput.TryCreate(input, ref invocation, out var subject));
            var start = subject.GetBytePosition(startOffsetInBytes, nameof(startOffsetInBytes));
            scope = Utf8SliceByteOutputScope.Enter(invocation);
            var output = new Pcre2CallbackReplacementOutput<TState, TExpansion>(input, scope, expansion, state);
            if (_program.Operations.Replace is Pcre2SliceDirectProgram slice)
                ReplaceCapturedCallback(input, subject, start, slice, matchOptions, scope.Invocation, ref output);
            else if (_program.Operations.Replace is Pcre2BacktrackingDirectProgram captured &&
                (captured.Program.CaptureSlotCount > 1 || captured.Program.SuppressesUnresetEmptyMatches))
                ReplaceCapturedCallback(input, subject, start, captured, matchOptions, scope.Invocation, ref output);
            else if (!TryReplaceCallbackViaLiteralDriver(input, subject, start, matchOptions, scope.Invocation, ref output))
            {
                if (_program.Operations.Replace is Pcre2BacktrackingDirectProgram backtracking)
                    ReplaceBacktrackingCallback(input, subject, start, backtracking.Program, matchOptions,
                        new Pcre2ResourceBudget(_program.Request.DefaultLimits, scope.Invocation,
                            collectDiagnostics: false), ref output);
                else throw CreateUnsupportedPatternException();
            }
            var result = output.Complete(text);
            Pcre2SliceDriver.CheckInvocation(ref scope.Invocation);
            return result;
        }
        catch (Utf8ExecutionDeadlineExpiredException)
        {
            throw new Pcre2MatchException("The PCRE2 match deadline expired.", Pcre2ErrorKind.Timeout);
        }
        catch (InvalidOperationException) when (invocation.Budget.LimitBreach != Utf8ExecutionLimitKind.None)
        {
            Pcre2SliceDriver.ThrowIfSliceBreached(invocation.Budget.LimitBreach);
            throw;
        }
        finally { scope?.Release(); }
    }

    private bool TryReplaceCallbackViaLiteralDriver<TState, TExpansion>(ReadOnlySpan<byte> input,
        Utf8ValidatedInput subject, Utf8BytePosition start, Pcre2MatchOptions matchOptions, Utf8SliceInvocation invocation,
        ref Pcre2CallbackReplacementOutput<TState, TExpansion> output)
        where TExpansion : struct, IPcre2CallbackExpansion<TState>
    {
        var budget = new Pcre2ResourceBudget(_program.Request.DefaultLimits, invocation, collectDiagnostics: false);
        if (!Pcre2GlobalOperationDriver.TryCreateCursor(_program, subject, start, matchOptions, out var cursor, budget))
            return false;

        var bytes = Utf8ConstructionBudget.ArrayBytes<Pcre2GroupData>(1);
        Pcre2SliceDriver.ThrowIfSliceBreached(budget.WorkingInvocation.Budget.TryChargeResultBytes(bytes));
        budget.ReserveWorking(bytes);
        try
        {
            var groups = new Pcre2GroupData[1];
            while (true)
            {
                output.Checkpoint();
                if (!cursor.MoveNext()) break;
                groups[0] = cursor.Current;
                var context = Utf8Pcre2MatchContext.Create(input, groups, NameEntries);
                output.Append(in context);
            }
        }
        finally { budget.ReleaseWorking(bytes); }
        return true;
    }

    private void ReplaceCapturedCallback<TState, TExpansion>(ReadOnlySpan<byte> input,
        Utf8ValidatedInput subject, Utf8BytePosition start, IPcre2DirectProgram program, Pcre2MatchOptions matchOptions,
        Utf8SliceInvocation invocation, ref Pcre2CallbackReplacementOutput<TState, TExpansion> output)
        where TExpansion : struct, IPcre2CallbackExpansion<TState>
    {
        // Explicit options use the slice shell's existing native program.
        // Its detailed cursor owns the actual global yield: querying again
        // would lose the empty-retry and original-start dispositions.
        var backtracking = program is Pcre2BacktrackingDirectProgram native ? native.Program
            : matchOptions != Pcre2MatchOptions.None && program is Pcre2SliceDirectProgram sliceFallback ? sliceFallback.Fallback : null;
        if (backtracking is not null)
        {
            var budget = new Pcre2ResourceBudget(_program.Request.DefaultLimits, invocation, collectDiagnostics: false);
            ReplaceBacktrackingCallback(input, subject, start, backtracking, matchOptions, budget, ref output);
            return;
        }

        var scalarProgram = ((Pcre2SliceDirectProgram)program).SliceProgram;
        var cursor = new Pcre2SliceGlobalMatchCursor(scalarProgram, invocation, subject, start, _program.Request);
        while (true)
        {
            output.Checkpoint();
            if (!cursor.MoveNext()) break;
            var range = cursor.Current;
            // Preserve the precise yield, including the nonempty same-start retry.
            var groups = cursor.CaptureGroupsAt(range.StartOffsetInBytes, range.EndOffsetInBytes > range.StartOffsetInBytes);
            var context = Utf8Pcre2MatchContext.Create(input, groups,
                NameEntries, null);
            output.Append(in context);
        }
    }

    private void ReplaceBacktrackingCallback<TState, TExpansion>(ReadOnlySpan<byte> input, Utf8ValidatedInput subject, Utf8BytePosition start, Pcre2BacktrackingProgram program,
        Pcre2MatchOptions matchOptions, Pcre2ResourceBudget budget,
        ref Pcre2CallbackReplacementOutput<TState, TExpansion> output)
        where TExpansion : struct, IPcre2CallbackExpansion<TState>
    {
        var cursor = new Pcre2BacktrackingDetailedGlobalMatchCursor(program, _program.CandidateSearchPlan,
            subject, start, matchOptions, _program.Request, budget);
        while (true)
        {
            output.Checkpoint();
            if (!cursor.MoveNext()) break;
            var groups = cursor.ProjectCurrentCaptures();
            var context = Utf8Pcre2MatchContext.Create(input, groups, NameEntries, cursor.Current.Mark);
            output.Append(in context);
        }
    }
}
