namespace Lokad.Utf8Regex.Internal.Execution;

internal readonly partial struct Utf8ByteSafeLazyDfaVerifierProgram
{
    private sealed partial class Compiler
    {
        private enum NfaActionKind : byte { Program, Loop, FinishOpenLoop }
        private readonly record struct NfaAction(NfaActionKind Kind, Utf8ByteSafeLinearVerifierStep[] Program,
            int Pc, int Min, int Max, int Count, int Exit, int LoopStart, bool Top);

        private void ChargeCompilationStep()
        {
            _budget.ChargeWork(1);
            // Retain the former one-million-step optimizer ceiling as a
            // heuristic decline. It neither owns storage nor resets parent work.
            if (++_optimizationSteps > MaxOptimizationSteps) throw new CompilationBudgetExceededException();
        }

        private int AddDfaState(Utf8ConstructionBuffer<bool> accepting)
        {
            ChargeCompilationStep();
            if (accepting.Count >= MaxDfaStates) throw new CompilationBudgetExceededException();
            var index = accepting.Count;
            accepting.Add(false);
            return index;
        }

        private bool TryReadAnchors(Utf8ByteSafeLinearVerifierStep[] steps)
        {
            var consumed = false;
            var hasBeginning = false;
            for (var index = 0; index < steps.Length; index++)
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
                    if (index != steps.Length - 2 || steps[^1].Kind != Utf8ByteSafeLinearVerifierStepKind.Accept) return false;
                    _endAnchor = step;
                }
                else if (step.Kind != Utf8ByteSafeLinearVerifierStepKind.Accept) consumed = true;
            }
            return true;
        }

        private bool TryCompileNfa(Utf8ByteSafeLinearVerifierStep[] steps,
            out NfaGraph graph, out bool requiresBeginning, out bool requiresEnd)
        {
            requiresBeginning = false;
            requiresEnd = false;
            using var scope = Utf8ConstructionScope.Enter();
            using var actions = Buffer<NfaAction>(false);
            using var states = Buffer<NfaState>(false);
            var transferred = false;
            _budget.EnterDepth();
            try
            {
                var current = AddState(states);
                PushNfaAction(new(NfaActionKind.Program, steps, 0, 0, 0, 0, -1, -1, true), actions);
                while (actions.Count != 0)
                {
                    ChargeCompilationStep();
                    var action = actions.Pop();
                    _budget.LeaveDepth();
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
                            PushNfaAction(action with { Count = action.Count + 1 }, actions);
                            PushNfaAction(new(NfaActionKind.Program, action.Program, 0, 0, 0, 0, -1, -1, false), actions);
                            continue;
                        }
                        var exit = action.Exit >= 0 ? action.Exit : AddState(states);
                        if (IsOpenEndedLoop(action.Max))
                        {
                            states[current].EpsilonTransitions.Add(exit);
                            PushNfaAction(action with { Kind = NfaActionKind.FinishOpenLoop, Exit = exit, LoopStart = current }, actions);
                            PushNfaAction(new(NfaActionKind.Program, action.Program, 0, 0, 0, 0, -1, -1, false), actions);
                        }
                        else if (action.Count < action.Max)
                        {
                            states[current].EpsilonTransitions.Add(exit);
                            PushNfaAction(action with { Count = action.Count + 1, Exit = exit }, actions);
                            PushNfaAction(new(NfaActionKind.Program, action.Program, 0, 0, 0, 0, -1, -1, false), actions);
                        }
                        else
                        {
                            states[current].EpsilonTransitions.Add(exit);
                            current = exit;
                        }
                    }
                    else
                    {
                        for (var pc = action.Pc; pc < action.Program.Length; pc++)
                        {
                            ChargeCompilationStep();
                            var step = action.Program[pc];
                            if (step.Kind == Utf8ByteSafeLinearVerifierStepKind.LoopProgram)
                            {
                                PushNfaAction(action with { Pc = pc + 1 }, actions);
                                PushNfaAction(new(NfaActionKind.Loop, step.Program, 0, step.Min, step.Max, 0, -1, -1, false), actions);
                                break;
                            }
                            if (!TryAppendNfaStep(states, ref current, step, action.Top, ref requiresBeginning,
                                ref requiresEnd, pc == action.Program.Length - 2 && action.Program[^1].Kind == Utf8ByteSafeLinearVerifierStepKind.Accept))
                            {
                                if (_failureKind == Utf8ByteSafeLazyDfaCompileFailureKind.None)
                                    _failureKind = action.Top ? Utf8ByteSafeLazyDfaCompileFailureKind.UnsupportedStep : Utf8ByteSafeLazyDfaCompileFailureKind.UnsupportedSubProgram;
                                if (!action.Top) throw new InvalidOperationException($"Unsupported sub-program step {step.Kind} in lazy DFA compiler.");
                                graph = default;
                                return false;
                            }
                            if (step.Kind == Utf8ByteSafeLinearVerifierStepKind.Accept)
                            {
                                graph = new(states.Take(), current, _budget);
                                transferred = true;
                                scope.Complete();
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
            finally
            {
                while (actions.TryPop(out _)) _budget.LeaveDepth();
                if (!transferred) foreach (var state in states) state.Dispose();
                _budget.LeaveDepth();
            }
        }

        private void PushNfaAction(NfaAction action, Utf8ConstructionBuffer<NfaAction> actions)
        {
            _budget.EnterDepth();
            try { actions.Push(action); }
            catch { _budget.LeaveDepth(); throw; }
        }
    }
}
