using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Pcre2;

/// <summary>Shapes PCRE2 evaluator replacements over original UTF-8 byte ranges.</summary>
public sealed partial class Utf8Pcre2Regex
{
    private interface IPcre2CallbackExpansion<TState>
    {
        void Append(in Utf8Pcre2MatchContext match, ref Utf8ReplacementWriter writer, ref TState state);
    }

    private readonly struct Pcre2ByteCallback<TState>(Pcre2MatchEvaluator<TState> evaluator) : IPcre2CallbackExpansion<TState>
    {
        public void Append(in Utf8Pcre2MatchContext match, ref Utf8ReplacementWriter writer, ref TState state)
            => evaluator(in match, ref writer, ref state);
    }

    private readonly struct Pcre2StringCallback<TState>(Pcre2Utf16MatchEvaluator<TState> evaluator) : IPcre2CallbackExpansion<TState>
    {
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
        ValidateStartOffset(input, startOffsetInBytes);
        Utf8SliceByteOutputScope? scope = null;
        try
        {
            if (_program.Operations.Replace is Pcre2SliceDirectProgram slice && matchOptions == Pcre2MatchOptions.None)
                return ReplaceSliceCallback(input, slice, startOffsetInBytes, token, state, expansion, text);

            // Matching still owns its own work/heap policy. This scope provides
            // one shaping deadline/token and admitted buffer identities, growth
            // overlap and terminal copies. A common finite operation-wide heap
            // authority remains a separate migration gate.
            scope = Utf8SliceByteOutputScope.Enter(Utf8ExecutionLimits.Unlimited, _program.Request.MatchTimeout, token);
            var output = new Pcre2CallbackReplacementOutput<TState, TExpansion>(input, scope, expansion, state);
            if (!TryReplaceCallbackViaLiteralDriver(input, startOffsetInBytes, matchOptions, ref output))
            {
                if (_program.Operations.Replace is Pcre2BacktrackingDirectProgram backtracking)
                    ReplaceBacktrackingCallback(input, backtracking.Program, startOffsetInBytes, matchOptions, ref output);
                else
                    ReplaceIterationCallback(input, startOffsetInBytes, matchOptions, token, ref output);
            }
            return output.Complete(text);
        }
        catch (Utf8ExecutionDeadlineExpiredException)
        {
            throw new Pcre2MatchException("The PCRE2 match deadline expired.", Pcre2ErrorKind.Timeout);
        }
        finally { scope?.Release(); }
    }

    private bool TryReplaceCallbackViaLiteralDriver<TState, TExpansion>(ReadOnlySpan<byte> input,
        int startOffsetInBytes, Pcre2MatchOptions matchOptions, ref Pcre2CallbackReplacementOutput<TState, TExpansion> output)
        where TExpansion : struct, IPcre2CallbackExpansion<TState>
    {
        if (_program.Operations.Replace is Pcre2SliceDirectProgram slice && slice.SliceProgram.GroupCount > 0 ||
            _program.Operations.Replace is Pcre2BacktrackingDirectProgram backtracking &&
            (backtracking.Program.CaptureSlotCount > 1 || backtracking.Program.SuppressesUnresetEmptyMatches))
            return false;

        var subject = ValidateSubjectAndStart(input, startOffsetInBytes, out var start);
        if (!Pcre2GlobalOperationDriver.TryCreateCursor(_program, subject, start, matchOptions, out var cursor))
            return false;

        var groups = new Pcre2GroupData[1];
        while (true)
        {
            output.Checkpoint();
            if (!cursor.MoveNext()) break;
            groups[0] = cursor.Current;
            var context = Utf8Pcre2MatchContext.Create(input, groups, NameEntries);
            output.Append(in context);
        }
        return true;
    }

    private void ReplaceIterationCallback<TState, TExpansion>(ReadOnlySpan<byte> input,
        int startOffsetInBytes, Pcre2MatchOptions matchOptions, CancellationToken token,
        ref Pcre2CallbackReplacementOutput<TState, TExpansion> output)
        where TExpansion : struct, IPcre2CallbackExpansion<TState>
    {
        var cursor = EnumerateMatches(input, startOffsetInBytes, matchOptions, token);
        while (true)
        {
            output.Checkpoint();
            if (!cursor.MoveNext()) break;
            var context = MatchDetailed(input, cursor.Current.StartOffsetInBytes, matchOptions, token);
            output.Append(in context);
        }
    }

    private Pcre2ReplacementResult ReplaceSliceCallback<TState, TExpansion>(ReadOnlySpan<byte> input,
        Pcre2SliceDirectProgram program, int startOffsetInBytes, CancellationToken token,
        TState state, TExpansion expansion, bool text)
        where TExpansion : struct, IPcre2CallbackExpansion<TState>
    {
        // Enter once before subject validation. Discovery, capture replay,
        // callback buffers and final copying share this original deadline,
        // token, cumulative work and overlapping capacity allowance.
        var invocation = Pcre2SliceDriver.CreateInvocation(_program.Request, token);
        invocation.ShareStorageForCursor();
        Utf8SliceByteOutputScope? scope = null;
        try
        {
            scope = Utf8SliceByteOutputScope.Enter(invocation);
            var output = new Pcre2CallbackReplacementOutput<TState, TExpansion>(input, scope, expansion, state);
            var subject = ValidateSubjectAndStart(input, startOffsetInBytes, out var start);
            var cursor = new Pcre2SliceGlobalMatchCursor(program.SliceProgram, scope.Invocation, subject, start, _program.Request);
            while (true)
            {
                output.Checkpoint();
                if (!cursor.MoveNext()) break;
                var range = cursor.Current;
                // Reproduce the precise global yield, including the non-empty
                // retry after an empty match at the same start. A plain query
                // would project the preceding empty match's captures instead.
                var groups = cursor.CaptureGroupsAt(range.StartOffsetInBytes, range.EndOffsetInBytes > range.StartOffsetInBytes);
                var context = Utf8Pcre2MatchContext.Create(input, groups, NameEntries, null);
                output.Append(in context);
            }
            return output.Complete(text);
        }
        catch (InvalidOperationException) when (invocation.Budget.LimitBreach != Utf8ExecutionLimitKind.None)
        {
            Pcre2SliceDriver.ThrowIfSliceBreached(invocation.Budget.LimitBreach);
            throw;
        }
        finally { scope?.Release(); }
    }

    private void ReplaceBacktrackingCallback<TState, TExpansion>(ReadOnlySpan<byte> input, Pcre2BacktrackingProgram program,
        int startOffsetInBytes, Pcre2MatchOptions matchOptions, ref Pcre2CallbackReplacementOutput<TState, TExpansion> output)
        where TExpansion : struct, IPcre2CallbackExpansion<TState>
    {
        var subject = ValidateSubjectAndStart(input, startOffsetInBytes, out var start);
        var cursor = new Pcre2BacktrackingDetailedGlobalMatchCursor(program, _program.CandidateSearchPlan,
            subject, start, matchOptions, _program.Request);
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
