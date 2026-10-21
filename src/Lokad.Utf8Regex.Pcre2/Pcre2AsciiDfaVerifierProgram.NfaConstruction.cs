using Lokad.Utf8Regex.Internal;
using Lokad.Utf8Regex.Internal.Execution;
namespace Lokad.Utf8Regex.Pcre2;

internal readonly partial struct Pcre2AsciiDfaVerifierProgram
{
    private sealed partial class Pcre2DfaCompiler
    {
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
                    if (consumed || hasBeginning)
                        return false;
                    hasBeginning = true;
                }
                else if (step.Kind == Pcre2AsciiDfaStepKind.RequireEnd)
                {
                    if (index != steps.Length - 2 || steps[^1].Kind != Pcre2AsciiDfaStepKind.Accept) return false;
                }
                else if (step.Kind != Pcre2AsciiDfaStepKind.Accept) consumed = true;
            }
            return true;
        }

        private bool TryCompileNfa(Pcre2AsciiDfaStep[] steps,
            out Pcre2DfaNfaGraph graph, out bool requiresBeginning, out bool requiresEnd)
        {
            graph = default;
            requiresBeginning = requiresEnd = false;
            using var scope = Utf8ConstructionScope.Enter();
            using var states = Buffer<Pcre2DfaNfaState>(false);
            var transferred = false;
            _budget.EnterDepth();
            try
            {
                var current = AddState(states);
                for (var index = 0; index < steps.Length; index++)
                {
                    ChargeCompilationStep();
                    var step = steps[index];
                    if (!TryAppendNfaStep(states, ref current, step, ref requiresBeginning,
                        ref requiresEnd, index == steps.Length - 2 && steps[^1].Kind == Pcre2AsciiDfaStepKind.Accept))
                    {
                        if (_failureKind == Pcre2AsciiDfaCompileFailureKind.None)
                            _failureKind = Pcre2AsciiDfaCompileFailureKind.UnsupportedStep;
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
                return false;
            }
            catch (Pcre2DfaCompilationBudgetExceededException)
            {
                _failureKind = Pcre2AsciiDfaCompileFailureKind.StateBudgetExceeded;
                requiresBeginning = requiresEnd = false;
                return false;
            }
            finally
            {
                if (!transferred) foreach (var state in states) state.Dispose();
                _budget.LeaveDepth();
            }
        }
    }
}
