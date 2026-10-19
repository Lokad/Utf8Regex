using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal;
using Lokad.Utf8Regex.Internal.Execution;
namespace Lokad.Utf8Regex.Pcre2;

internal readonly partial struct Pcre2AsciiDfaVerifierProgram
{
    private sealed partial class Pcre2DfaCompiler
    {
        private enum Pcre2DfaNfaActionKind : byte { Program, Loop, FinishOpenLoop }
        private readonly record struct Pcre2DfaNfaAction(Pcre2DfaNfaActionKind Kind, Pcre2AsciiDfaStep[] Program,
            int Pc, int Min, int Max, int Count, int Exit, int LoopStart, bool Top);

        private void ChargeCompilationStep()
        {
            _budget.ChargeWork(1);
            // Retain the former one-million-step optimizer ceiling as a
            // heuristic decline. It neither owns storage nor resets parent work.
            if (++_optimizationSteps > MaxOptimizationSteps) throw new Pcre2DfaCompilationBudgetExceededException();
        }

        private int AddDfaState(Utf8ConstructionBuffer<bool> accepting)
        {
            ChargeCompilationStep();
            if (accepting.Count >= MaxDfaStates) throw new Pcre2DfaCompilationBudgetExceededException();
            var index = accepting.Count;
            accepting.Add(false);
            return index;
        }

        private bool TryReadAnchors(Pcre2AsciiDfaStep[] steps)
        {
            var consumed = false;
            var hasBeginning = false;
            for (var index = 0; index < steps.Length; index++)
            {
                ChargeCompilationStep();
                var step = steps[index];
                if (step.Kind == Pcre2AsciiDfaStepKind.RequireBeginning)
                {
                    if (consumed || hasBeginning && (_beginningAnchor.AnchorKind != step.AnchorKind || _beginningAnchor.Options != step.Options))
                        return false;
                    _beginningAnchor = step;
                    hasBeginning = true;
                }
                else if (step.Kind == Pcre2AsciiDfaStepKind.RequireEnd)
                {
                    if (index != steps.Length - 2 || steps[^1].Kind != Pcre2AsciiDfaStepKind.Accept) return false;
                    _endAnchor = step;
                }
                else if (step.Kind != Pcre2AsciiDfaStepKind.Accept) consumed = true;
            }
            return true;
        }

        private bool TryCompileNfa(Pcre2AsciiDfaStep[] steps,
            out Pcre2DfaNfaGraph graph, out bool requiresBeginning, out bool requiresEnd)
        {
            requiresBeginning = false;
            requiresEnd = false;
            using var scope = Utf8ConstructionScope.Enter();
            using var actions = Buffer<Pcre2DfaNfaAction>(false);
            using var states = Buffer<Pcre2DfaNfaState>(false);
            var transferred = false;
            _budget.EnterDepth();
            try
            {
                var current = AddState(states);
                PushNfaAction(new(Pcre2DfaNfaActionKind.Program, steps, 0, 0, 0, 0, -1, -1, true), actions);
                while (actions.Count != 0)
                {
                    ChargeCompilationStep();
                    var action = actions.Pop();
                    _budget.LeaveDepth();
                    if (action.Kind == Pcre2DfaNfaActionKind.FinishOpenLoop)
                    {
                        states[current].EpsilonTransitions.Add(action.LoopStart);
                        states[current].EpsilonTransitions.Add(action.Exit);
                        current = action.Exit;
                    }
                    else if (action.Kind == Pcre2DfaNfaActionKind.Loop)
                    {
                        if (action.Count < action.Min)
                        {
                            PushNfaAction(action with { Count = action.Count + 1 }, actions);
                            PushNfaAction(new(Pcre2DfaNfaActionKind.Program, action.Program, 0, 0, 0, 0, -1, -1, false), actions);
                            continue;
                        }
                        var exit = action.Exit >= 0 ? action.Exit : AddState(states);
                        if (IsOpenEndedLoop(action.Max))
                        {
                            states[current].EpsilonTransitions.Add(exit);
                            PushNfaAction(action with { Kind = Pcre2DfaNfaActionKind.FinishOpenLoop, Exit = exit, LoopStart = current }, actions);
                            PushNfaAction(new(Pcre2DfaNfaActionKind.Program, action.Program, 0, 0, 0, 0, -1, -1, false), actions);
                        }
                        else if (action.Count < action.Max)
                        {
                            states[current].EpsilonTransitions.Add(exit);
                            PushNfaAction(action with { Count = action.Count + 1, Exit = exit }, actions);
                            PushNfaAction(new(Pcre2DfaNfaActionKind.Program, action.Program, 0, 0, 0, 0, -1, -1, false), actions);
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
                            if (step.Kind == Pcre2AsciiDfaStepKind.LoopProgram)
                            {
                                PushNfaAction(action with { Pc = pc + 1 }, actions);
                                PushNfaAction(new(Pcre2DfaNfaActionKind.Loop, step.Program, 0, step.Min, step.Max, 0, -1, -1, false), actions);
                                break;
                            }
                            if (!TryAppendNfaStep(states, ref current, step, action.Top, ref requiresBeginning,
                                ref requiresEnd, pc == action.Program.Length - 2 && action.Program[^1].Kind == Pcre2AsciiDfaStepKind.Accept))
                            {
                                if (_failureKind == Pcre2AsciiDfaCompileFailureKind.None)
                                    _failureKind = action.Top ? Pcre2AsciiDfaCompileFailureKind.UnsupportedStep : Pcre2AsciiDfaCompileFailureKind.UnsupportedSubProgram;
                                if (!action.Top) throw new InvalidOperationException($"Unsupported sub-program step {step.Kind} in lazy DFA compiler.");
                                graph = default;
                                return false;
                            }
                            if (step.Kind == Pcre2AsciiDfaStepKind.Accept)
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
            catch (Pcre2DfaCompilationBudgetExceededException)
            {
                _failureKind = Pcre2AsciiDfaCompileFailureKind.StateBudgetExceeded;
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

        private void PushNfaAction(Pcre2DfaNfaAction action, Utf8ConstructionBuffer<Pcre2DfaNfaAction> actions)
        {
            _budget.EnterDepth();
            try { actions.Push(action); }
            catch { _budget.LeaveDepth(); throw; }
        }
    }
}
