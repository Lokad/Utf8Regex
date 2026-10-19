using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.FrontEnd;

namespace Lokad.Utf8Regex.Pcre2;

internal readonly partial struct Pcre2AsciiDfaVerifierProgram
{
    private sealed partial class Pcre2DfaCompiler
    {
        private readonly record struct Pcre2DfaTransitionBucket(Pcre2DfaTransitionKey Key, Utf8ConstructionBuffer<int> Targets, int Ordinal);

        private Pcre2AsciiDfaVerifierProgram BuildDfa(Pcre2DfaNfaGraph nfa, bool requiresBeginning, bool requiresEnd)
        {
            using var scope = Utf8ConstructionScope.Enter();
            _budget.EnterDepth();
            using var stateSets = Buffer<Pcre2DfaNfaStateSet>(false);
            using var accepting = Buffer<bool>(false);
            using var offsets = Buffer<int>(false);
            using var counts = Buffer<int>(false);
            using var sets = Buffer<AsciiCharClass>(false);
            using var targets = Buffer<int>(true);
            var comparer = _budget.AllocateObject(_budget, static owner => new Pcre2DfaNfaStateSetComparer(owner));
            using var stateMap = Utf8ConstructionMap<Pcre2DfaNfaStateSet, int>.Create(_budget, comparer);
            try
            {
                var zero = 0;
                var start = ComputeClosure(new ReadOnlySpan<int>(in zero), nfa);
                var adoptedStart = false;
                try
                {
                    stateSets.Add(start);
                    adoptedStart = true;
                    _budget.ChargeWork(1); // Pre-admit the closing state-set walk.
                    stateMap[start] = 0;
                    accepting.Add(start.Contains(nfa.AcceptState));
                }
                finally { if (!adoptedStart) _budget.ReleaseArray(start.States, false); }

                // The ordered state vector is also the breadth-first queue.
                for (var next = 0; next < stateSets.Count; next++)
                {
                    ChargeCompilationStep();
                    using var bucketMap = Utf8ConstructionMap<Pcre2DfaTransitionKey, int>.Create(_budget, Pcre2DfaTransitionKeyComparer.Instance);
                    using var buckets = Buffer<Pcre2DfaTransitionBucket>(false);
                    try
                    {
                        foreach (var stateId in stateSets[next].States)
                        {
                            ChargeCompilationStep();
                            foreach (var transition in nfa.States[stateId].Transitions)
                            {
                                ChargeCompilationStep();
                                if (!bucketMap.TryGetValue(transition.Key, out var bucketIndex))
                                {
                                    var bucketTargets = Buffer<int>(false);
                                    bucketIndex = buckets.Count;
                                    // Acquire only the descriptor here; the buffer is
                                    // already retained by the owner if publication fails.
                                    buckets.Add(new(transition.Key, bucketTargets, bucketIndex));
                                    _budget.ChargeWork(1); // Closing bucket walk.
                                    bucketMap.Add(transition.Key, bucketIndex);
                                }
                                buckets[bucketIndex].Targets.Add(transition.TargetState);
                            }
                        }
                        buckets.Sort(static (left, right) =>
                        {
                            var order = left.Key.SortKey.CompareTo(right.Key.SortKey);
                            return order != 0 ? order : left.Ordinal.CompareTo(right.Ordinal);
                        });
                        offsets.Add(sets.Count);
                        counts.Add(buckets.Count);
                        foreach (var bucket in buckets)
                        {
                            ChargeCompilationStep();
                            var closure = ComputeClosure(bucket.Targets.Span, nfa);
                            var adopted = false;
                            try
                            {
                                if (!stateMap.TryGetValue(closure, out var targetIndex))
                                {
                                    targetIndex = stateSets.Count;
                                    if (targetIndex >= MaxDfaStates) throw new Pcre2DfaCompilationBudgetExceededException();
                                    stateSets.Add(closure);
                                    adopted = true;
                                    _budget.ChargeWork(1); // Closing state-set walk.
                                    stateMap.Add(closure, targetIndex);
                                    accepting.Add(closure.Contains(nfa.AcceptState));
                                }
                                if (sets.Count >= MaxDfaTransitions) throw new Pcre2DfaCompilationBudgetExceededException();
                                sets.Add(bucket.Key.Set);
                                targets.Add(targetIndex);
                            }
                            finally { if (!adopted) _budget.ReleaseArray(closure.States, false); }
                        }
                    }
                    finally { foreach (var bucket in buckets) bucket.Targets.Dispose(); }
                }
                var program = new Pcre2AsciiDfaVerifierProgram(offsets.Take(), counts.Take(),
                    sets.Take(), targets.Take(), accepting.Take(), requiresBeginning, requiresEnd,
                    _beginningAnchor, _endAnchor);
                scope.Complete();
                return program;
            }
            finally
            {
                foreach (var state in stateSets) _budget.ReleaseArray(state.States, false);
                _budget.LeaveDepth();
            }
        }

        private Pcre2DfaNfaStateSet ComputeClosure(ReadOnlySpan<int> startStates, Pcre2DfaNfaGraph nfa)
        {
            using var scope = Utf8ConstructionScope.Enter();
            using var pending = Buffer<int>(false);
            var visited = _budget.AllocateArray<bool>(nfa.States.Length, false);
            try
            {
                foreach (var state in startStates) PushClosureState(state, pending);
                var count = 0;
                while (pending.TryPop(out var state))
                {
                    _budget.LeaveDepth();
                    ChargeCompilationStep();
                    if (visited[state]) continue;
                    visited[state] = true;
                    count++;
                    foreach (var target in nfa.States[state].EpsilonTransitions)
                    {
                        ChargeCompilationStep();
                        PushClosureState(target, pending);
                    }
                }
                var states = _budget.AllocateArray<int>(count, false);
                _budget.ChargeWork((ulong)visited.Length);
                var index = 0;
                // A scan produces the canonical sorted key without another
                // collection, temporary sort array or unmetered comparer.
                for (var state = 0; state < visited.Length; state++)
                    if (visited[state]) states[index++] = state;
                scope.Complete();
                return new(states);
            }
            finally
            {
                while (pending.TryPop(out _)) _budget.LeaveDepth();
                _budget.ReleaseArray(visited, false);
            }
        }

        private void PushClosureState(int state, Utf8ConstructionBuffer<int> pending)
        {
            _budget.EnterDepth();
            try { pending.Push(state); }
            catch { _budget.LeaveDepth(); throw; }
        }
    }
}
