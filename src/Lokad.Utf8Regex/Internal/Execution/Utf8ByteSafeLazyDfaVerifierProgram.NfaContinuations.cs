namespace Lokad.Utf8Regex.Internal.Execution;

internal readonly partial struct Utf8ByteSafeLazyDfaVerifierProgram
{
    private sealed partial class Compiler
    {
        private enum NfaActionKind : byte { Program, Loop, FinishOpenLoop }
        private readonly record struct NfaAction(NfaActionKind Kind, IReadOnlyList<Utf8ByteSafeLinearVerifierStep> Program,
            int Pc, int Min, int Max, int Count, int Exit, int LoopStart, bool Top, uint Depth);

        private void ChargeCompilationStep()
        {
            if (_invocation.TryChargeInputStep() != Utf8ExecutionLimitKind.None)
                throw new CompilationBudgetExceededException();
        }

        private int AddDfaState(List<bool> accepting)
        {
            ChargeCompilationStep();
            if (accepting.Count >= MaxDfaStates) throw new CompilationBudgetExceededException();
            var index = accepting.Count;
            accepting.Add(false);
            return index;
        }

        private bool TryReadAnchors(IReadOnlyList<Utf8ByteSafeLinearVerifierStep> steps)
        {
            var consumed = false;
            var hasBeginning = false;
            for (var index = 0; index < steps.Count; index++)
            {
                ChargeCompilationStep();
                var step = steps[index];
                if (step.Kind == Utf8ByteSafeLinearVerifierStepKind.RequireBeginning)
                {
                    if (consumed || hasBeginning && (_beginningAnchor.AnchorKind != step.AnchorKind || _beginningAnchor.Options != step.Options))
                        return false;
                    _beginningAnchor = step;
                    hasBeginning = true;
                }
                else if (step.Kind == Utf8ByteSafeLinearVerifierStepKind.RequireEnd)
                {
                    if (index != steps.Count - 2 || steps[^1].Kind != Utf8ByteSafeLinearVerifierStepKind.Accept) return false;
                    _endAnchor = step;
                }
                else if (step.Kind != Utf8ByteSafeLinearVerifierStepKind.Accept) consumed = true;
            }
            return true;
        }

        private bool TryCompileNfa(IReadOnlyList<Utf8ByteSafeLinearVerifierStep> steps,
            out NfaGraph graph, out bool requiresBeginning, out bool requiresEnd)
        {
            requiresBeginning = false;
            requiresEnd = false;
            var actions = new Utf8InvocationStateBuffer<NfaAction>();
            try
            {
                var states = new List<NfaState>();
                var current = AddState(states);
                PushNfaAction(new(NfaActionKind.Program, steps, 0, 0, 0, 0, -1, -1, true, 1), ref actions);
                while (actions.Count != 0)
                {
                    ChargeCompilationStep();
                    var action = actions[actions.Count - 1];
                    actions.Pop();
                    if (action.Kind == NfaActionKind.FinishOpenLoop)
                    {
                        states[current].EpsilonTransitions.Add(action.LoopStart);
                        states[current].EpsilonTransitions.Add(action.Exit);
                        current = action.Exit;
                    }
                    else if (action.Kind == NfaActionKind.Loop)
                    {
                        if (action.Count < action.Min)
                        {
                            PushNfaAction(action with { Count = action.Count + 1 }, ref actions);
                            PushNfaAction(new(NfaActionKind.Program, action.Program, 0, 0, 0, 0, -1, -1, false, action.Depth), ref actions);
                            continue;
                        }
                        var exit = action.Exit >= 0 ? action.Exit : AddState(states);
                        if (action.Max < 0)
                        {
                            states[current].EpsilonTransitions.Add(exit);
                            PushNfaAction(action with { Kind = NfaActionKind.FinishOpenLoop, Exit = exit, LoopStart = current }, ref actions);
                            PushNfaAction(new(NfaActionKind.Program, action.Program, 0, 0, 0, 0, -1, -1, false, action.Depth), ref actions);
                        }
                        else if (action.Count < action.Max)
                        {
                            states[current].EpsilonTransitions.Add(exit);
                            PushNfaAction(action with { Count = action.Count + 1, Exit = exit }, ref actions);
                            PushNfaAction(new(NfaActionKind.Program, action.Program, 0, 0, 0, 0, -1, -1, false, action.Depth), ref actions);
                        }
                        else
                        {
                            states[current].EpsilonTransitions.Add(exit);
                            current = exit;
                        }
                    }
                    else
                    {
                        for (var pc = action.Pc; pc < action.Program.Count; pc++)
                        {
                            ChargeCompilationStep();
                            var step = action.Program[pc];
                            if (step.Kind == Utf8ByteSafeLinearVerifierStepKind.LoopProgram)
                            {
                                PushNfaAction(action with { Pc = pc + 1 }, ref actions);
                                PushNfaAction(new(NfaActionKind.Loop, step.Program, 0, step.Min, step.Max, 0, -1, -1, false, action.Depth + 1), ref actions);
                                break;
                            }
                            if (!TryAppendNfaStep(states, ref current, step, action.Top, ref requiresBeginning,
                                ref requiresEnd, pc == action.Program.Count - 2 && action.Program[^1].Kind == Utf8ByteSafeLinearVerifierStepKind.Accept))
                            {
                                if (_failureKind == Utf8ByteSafeLazyDfaCompileFailureKind.None)
                                    _failureKind = action.Top ? Utf8ByteSafeLazyDfaCompileFailureKind.UnsupportedStep : Utf8ByteSafeLazyDfaCompileFailureKind.UnsupportedSubProgram;
                                if (!action.Top) throw new InvalidOperationException($"Unsupported sub-program step {step.Kind} in lazy DFA compiler.");
                                graph = default;
                                return false;
                            }
                            if (step.Kind == Utf8ByteSafeLinearVerifierStepKind.Accept)
                            {
                                graph = new([.. states], current);
                                return true;
                            }
                        }
                    }
                }
                graph = default;
                return false;
            }
            catch (CompilationBudgetExceededException)
            {
                _failureKind = Utf8ByteSafeLazyDfaCompileFailureKind.StateBudgetExceeded;
                graph = default;
                requiresBeginning = false;
                requiresEnd = false;
                return false;
            }
            finally { actions.Release(ref _invocation); }
        }

        private void PushNfaAction(NfaAction action, ref Utf8InvocationStateBuffer<NfaAction> actions)
        {
            if (_invocation.Budget.TryNoteDepth(action.Depth) != Utf8ExecutionLimitKind.None ||
                actions.TryPush(action, ref _invocation) != Utf8ExecutionLimitKind.None)
                throw new CompilationBudgetExceededException();
        }
    }
}
