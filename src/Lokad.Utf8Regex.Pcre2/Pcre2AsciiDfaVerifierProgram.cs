using Lokad.Utf8Regex.Internal;
using Lokad.Utf8Regex.Internal.Execution;
namespace Lokad.Utf8Regex.Pcre2;

internal readonly partial record struct Pcre2AsciiDfaCompileOutcome(
    Pcre2AsciiDfaVerifierProgram Program,
    Pcre2AsciiDfaCompileFailureKind FailureKind)
{
    public bool Succeeded => Program.HasValue;
}

internal readonly partial struct Pcre2AsciiDfaVerifierProgram
{
    private const int MaxDfaStates = 256;
    private const int MaxDfaTransitions = 4096;
    private const int MaxNfaStates = 2048;

    private readonly int[] _transitionOffsets;
    private readonly int[] _transitionCounts;
    private readonly AsciiCharClass[] _transitionSets;
    private readonly int[] _transitionTargets;
    private readonly bool[] _acceptingStates;

    private Pcre2AsciiDfaVerifierProgram(
        int[] transitionOffsets,
        int[] transitionCounts,
        AsciiCharClass[] transitionSets,
        int[] transitionTargets,
        bool[] acceptingStates,
        bool requiresBeginning,
        bool requiresEnd)
    {
        _transitionOffsets = transitionOffsets;
        _transitionCounts = transitionCounts;
        _transitionSets = transitionSets;
        _transitionTargets = transitionTargets;
        _acceptingStates = acceptingStates;
        RequiresBeginning = requiresBeginning;
        RequiresEnd = requiresEnd;
    }

    public bool RequiresBeginning { get; }

    public bool RequiresEnd { get; }

    public bool HasValue => _acceptingStates is { Length: > 0 };

    public int StateCount => _acceptingStates?.Length ?? 0;

    // PCRE2-INTEGRATION-POINT: shared byte-safe graph construction.
    public static Pcre2AsciiDfaCompileOutcome Compile(Pcre2AsciiDfaInput linearProgram)
    {
        if (!linearProgram.HasValue) return new(default, Pcre2AsciiDfaCompileFailureKind.EmptyProgram);
        using var scope = Utf8ConstructionScope.Enter();
        var budget = scope.Budget;
        budget.EnterDepth();
        try
        {
            var compiler = budget.AllocateObject((owner: budget, failure: Pcre2AsciiDfaCompileFailureKind.None,
                optimizerSteps: 0UL),
                static state => new Pcre2DfaCompiler(state.owner));
            var outcome = compiler.Compile(linearProgram);
            if (outcome.Succeeded) scope.Complete();
            return outcome;
        }
        finally { budget.LeaveDepth(); }
    }

    private sealed partial class Pcre2DfaCompiler
    {
        private const ulong MaxOptimizationSteps = 1_048_576;
        private ulong _optimizationSteps;
        private Pcre2AsciiDfaCompileFailureKind _failureKind;
        private readonly Utf8ConstructionBudget _budget;

        public Pcre2DfaCompiler(Utf8ConstructionBudget budget) => _budget = budget;

        private Utf8ConstructionBuffer<T> Buffer<T>(bool instructions) =>
            Utf8ConstructionBuffer<T>.Create(_budget, instructions);

        public Pcre2AsciiDfaCompileOutcome Compile(Pcre2AsciiDfaInput linearProgram)
        {
            try
            {
                if (!linearProgram.HasValue)
                {
                    return new Pcre2AsciiDfaCompileOutcome(default, Pcre2AsciiDfaCompileFailureKind.EmptyProgram);
                }

                if (!TryReadAnchors(linearProgram.Steps))
                    return new(default, Pcre2AsciiDfaCompileFailureKind.UnsupportedAnchoring);

                if (TryCompileDeterministicSequence(linearProgram.Steps, out var deterministicProgram))
                {
                    return new Pcre2AsciiDfaCompileOutcome(deterministicProgram, Pcre2AsciiDfaCompileFailureKind.None);
                }

                if (!TryCompileNfa(linearProgram.Steps, out var nfa, out var requiresBeginning, out var requiresEnd))
                {
                    if (_failureKind == Pcre2AsciiDfaCompileFailureKind.None)
                    {
                        _failureKind = Pcre2AsciiDfaCompileFailureKind.UnsupportedStep;
                    }

                    return new Pcre2AsciiDfaCompileOutcome(default, _failureKind);
                }

                using var ownedNfa = nfa;
                var program = BuildDfa(nfa, requiresBeginning, requiresEnd);
                if (!program.HasValue && _failureKind == Pcre2AsciiDfaCompileFailureKind.None)
                {
                    _failureKind = Pcre2AsciiDfaCompileFailureKind.StateBudgetExceeded;
                }

                return new Pcre2AsciiDfaCompileOutcome(program, program.HasValue ? Pcre2AsciiDfaCompileFailureKind.None : _failureKind);
            }
            catch (Pcre2DfaCompilationBudgetExceededException)
            {
                return new(default, Pcre2AsciiDfaCompileFailureKind.StateBudgetExceeded);
            }
        }

        // Admission covers both the direct kernel and the metered graph path.
        // A supported optional body need not be safe to append deterministically.

        private bool TryCompileDeterministicSequence(
            Pcre2AsciiDfaStep[] steps,
            out Pcre2AsciiDfaVerifierProgram program)
        {
            using var attempt = Utf8ConstructionScope.Enter();
            _budget.EnterDepth();
            try
            {
                if (TryCompileDisjointLoopSequence(steps, out program))
                {
                    attempt.Complete();
                    return true;
                }

                var requiresBeginning = false;
                var requiresEnd = false;
                using var transitionOffsets = Buffer<int>(false);
                using var transitionCounts = Buffer<int>(false);
                using var transitionSets = Buffer<AsciiCharClass>(false);
                using var transitionTargets = Buffer<int>(true);
                using var accepting = Buffer<bool>(false);
                var stateIndex = 0;

                AddDfaState(accepting);

                for (var stepIndex = 0; stepIndex < steps.Length; stepIndex++)
                {
                    ChargeCompilationStep();
                    var step = steps[stepIndex];
                    switch (step.Kind)
                    {
                        case Pcre2AsciiDfaStepKind.RequireBeginning:
                            if (stateIndex != 0)
                            {
                                program = default;
                                return false;
                            }

                            requiresBeginning = true;
                            break;

                        case Pcre2AsciiDfaStepKind.RequireEnd:
                            requiresEnd = true;
                            break;

                        case Pcre2AsciiDfaStepKind.Accept:
                            accepting[stateIndex] = true;
                            while (transitionOffsets.Count < accepting.Count)
                            {
                                transitionOffsets.Add(0);
                                transitionCounts.Add(0);
                            }

                            program = new Pcre2AsciiDfaVerifierProgram(
                                transitionOffsets.Take(),
                                transitionCounts.Take(),
                                transitionSets.Take(),
                                transitionTargets.Take(),
                                accepting.Take(),
                                requiresBeginning,
                                requiresEnd);
                            attempt.Complete();
                            return true;

                        case Pcre2AsciiDfaStepKind.MatchAsciiClass:
                            if (!AsciiCharClass.TryUseNonEmpty(step.CharClass, out var projectedMatchSet))
                            {
                                _failureKind = Pcre2AsciiDfaCompileFailureKind.UnsupportedProjectedAsciiSet;
                                program = default;
                                return false;
                            }

                            stateIndex = AppendDeterministicTransition(
                                step,
                                projectedMatchSet,
                                stateIndex,
                                accepting,
                                transitionOffsets,
                                transitionCounts,
                                transitionSets,
                                transitionTargets);
                            break;

                        case Pcre2AsciiDfaStepKind.LoopAsciiClass when step.Min == step.Max:
                            if (!AsciiCharClass.TryUseNonEmpty(step.CharClass, out var projectedLoopSet))
                            {
                                _failureKind = Pcre2AsciiDfaCompileFailureKind.UnsupportedProjectedAsciiSet;
                                program = default;
                                return false;
                            }

                            for (var i = 0; i < step.Min; i++)
                            {
                                stateIndex = AppendDeterministicTransition(
                                    step,
                                    projectedLoopSet,
                                    stateIndex,
                                    accepting,
                                    transitionOffsets,
                                    transitionCounts,
                                    transitionSets,
                                    transitionTargets);
                            }
                            break;

                        case Pcre2AsciiDfaStepKind.LoopAsciiClass when
                            step.Min == 0 &&
                            IsOpenEndedLoop(step.Max) &&
                            AsciiCharClass.TryUseNonEmpty(step.CharClass, out var leadingProjectedLoopSet) &&
                            TryGetFirstDeterministicTransitionSet(steps, stepIndex + 1, out var followingSet) &&
                            leadingProjectedLoopSet.IsDisjoint(followingSet):
                            AddDeterministicTransition(
                                stateIndex,
                                leadingProjectedLoopSet,
                                stateIndex,
                                transitionOffsets,
                                transitionCounts,
                                transitionSets,
                                transitionTargets);
                            break;

                        default:
                            _failureKind = Pcre2AsciiDfaCompileFailureKind.UnsupportedDeterministicStep;
                            program = default;
                            return false;
                    }
                }

                program = default;
                return false;
            }
            finally { _budget.LeaveDepth(); }
        }

        private bool TryCompileDisjointLoopSequence(
            Pcre2AsciiDfaStep[] steps,
            out Pcre2AsciiDfaVerifierProgram program)
        {
            using var attempt = Utf8ConstructionScope.Enter();
            _budget.EnterDepth();
            try
            {
                program = default;
                if (steps.Length < 3 || steps[^1].Kind != Pcre2AsciiDfaStepKind.Accept)
                {
                    return false;
                }

                var requiresBeginning = false;
                var requiresEnd = false;
                var start = 0;
                var endExclusive = steps.Length - 1;

                if (steps[start].Kind == Pcre2AsciiDfaStepKind.RequireBeginning)
                {
                    requiresBeginning = true;
                    start++;
                }

                if (endExclusive > start && steps[endExclusive - 1].Kind == Pcre2AsciiDfaStepKind.RequireEnd)
                {
                    requiresEnd = true;
                    endExclusive--;
                }

                if (!requiresBeginning || !requiresEnd || endExclusive <= start)
                {
                    return false;
                }

                if (endExclusive - start >= MaxDfaStates) return false;
                var payloadSteps = steps.AsSpan(start, endExclusive - start);
                if (payloadSteps.Length == 0)
                {
                    return false;
                }

                var coreSteps = payloadSteps;

                if (coreSteps.Length >= MaxDfaStates) return false;
                var loopSets = _budget.AllocateArray<AsciiCharClass>(coreSteps.Length, false);
                try
                {
                    for (var i = 0; i < coreSteps.Length; i++)
                    {
                        ChargeCompilationStep();
                        if (!TryGetDisjointLoopSet(coreSteps[i], out loopSets[i]))
                        {
                            return false;
                        }

                        if (!IsOpenEndedLoop(coreSteps[i].Max))
                        {
                            return false;
                        }

                        if (coreSteps[i].Min != 1 && !(i == coreSteps.Length - 1 && coreSteps[i].Min == 0))
                        {
                            return false;
                        }

                        if (i > 0 && !loopSets[i - 1].IsDisjoint(loopSets[i]))
                        {
                            return false;
                        }
                    }

                    using var transitionOffsets = Buffer<int>(false);
                    transitionOffsets.Add(0);
                    using var transitionCounts = Buffer<int>(false);
                    transitionCounts.Add(0);
                    using var transitionSets = Buffer<AsciiCharClass>(false);
                    using var transitionTargets = Buffer<int>(true);
                    using var accepting = Buffer<bool>(false);
                    AddDfaState(accepting);

                    var previousStageState = 0;
                    for (var i = 0; i < coreSteps.Length; i++)
                    {
                        var stageState = AddDfaState(accepting);
                        transitionOffsets.Add(0);
                        transitionCounts.Add(0);

                        AddDeterministicTransition(previousStageState, loopSets[i], stageState, transitionOffsets, transitionCounts, transitionSets, transitionTargets);
                        AddDeterministicTransition(stageState, loopSets[i], stageState, transitionOffsets, transitionCounts, transitionSets, transitionTargets);

                        if (i == coreSteps.Length - 1)
                        {
                            accepting[stageState] = true;
                            if (coreSteps[i].Min == 0)
                            {
                                accepting[previousStageState] = true;
                            }
                        }

                        previousStageState = stageState;
                    }

                    program = new Pcre2AsciiDfaVerifierProgram(
                        transitionOffsets.Take(),
                        transitionCounts.Take(),
                        transitionSets.Take(),
                        transitionTargets.Take(),
                        accepting.Take(),
                        requiresBeginning,
                        requiresEnd);
                    attempt.Complete();
                    return true;
                }
                finally { _budget.ReleaseArray(loopSets, false); }
            }
            finally { _budget.LeaveDepth(); }
        }

        private bool TryGetDisjointLoopSet(Pcre2AsciiDfaStep step, out AsciiCharClass set)
        {
            if (step.Kind == Pcre2AsciiDfaStepKind.LoopAsciiClass)
                return AsciiCharClass.TryUseNonEmpty(step.CharClass, out set);
            set = AsciiCharClass.Empty;
            return false;
        }

        private bool TryGetFirstDeterministicTransitionSet(
            Pcre2AsciiDfaStep[] steps, int startIndex, out AsciiCharClass set)
        {
            for (var index = startIndex; index < steps.Length; index++)
            {
                ChargeCompilationStep();
                if (steps[index].Kind is Pcre2AsciiDfaStepKind.MatchAsciiClass or Pcre2AsciiDfaStepKind.LoopAsciiClass)
                    return AsciiCharClass.TryUseNonEmpty(steps[index].CharClass, out set);
            }
            set = AsciiCharClass.Empty;
            return false;
        }

        private bool IsOpenEndedLoop(int max) => max < 0 || max == int.MaxValue;

        private int AppendDeterministicTransition(
            Pcre2AsciiDfaStep step,
            AsciiCharClass transitionSet,
            int stateIndex,
            Utf8ConstructionBuffer<bool> accepting,
            Utf8ConstructionBuffer<int> transitionOffsets,
            Utf8ConstructionBuffer<int> transitionCounts,
            Utf8ConstructionBuffer<AsciiCharClass> transitionSets,
            Utf8ConstructionBuffer<int> transitionTargets)
        {
            ChargeCompilationStep();
            while (transitionOffsets.Count <= stateIndex)
            {
                transitionOffsets.Add(0);
                transitionCounts.Add(0);
            }

            var nextState = AddDfaState(accepting);
            if (transitionCounts[stateIndex] == 0)
            {
                transitionOffsets[stateIndex] = transitionSets.Count;
                transitionCounts[stateIndex] = 1;
                if (transitionSets.Count >= MaxDfaTransitions) throw new Pcre2DfaCompilationBudgetExceededException();
                transitionSets.Add(transitionSet);
                transitionTargets.Add(nextState);
                return nextState;
            }

            AddDeterministicTransition(
                stateIndex,
                transitionSet,
                nextState,
                transitionOffsets,
                transitionCounts,
                transitionSets,
                transitionTargets);
            return nextState;
        }

        private void AddDeterministicTransition(
            int stateIndex,
            AsciiCharClass transitionSet,
            int targetState,
            Utf8ConstructionBuffer<int> transitionOffsets,
            Utf8ConstructionBuffer<int> transitionCounts,
            Utf8ConstructionBuffer<AsciiCharClass> transitionSets,
            Utf8ConstructionBuffer<int> transitionTargets)
        {
            ChargeCompilationStep();
            while (transitionOffsets.Count <= stateIndex)
            {
                transitionOffsets.Add(0);
                transitionCounts.Add(0);
            }

            if (transitionCounts[stateIndex] == 0)
            {
                transitionOffsets[stateIndex] = transitionSets.Count;
            }

            var offset = transitionOffsets[stateIndex];
            var count = transitionCounts[stateIndex];
            for (var i = 0; i < count; i++)
            {
                ChargeCompilationStep();
                if (!transitionSets[offset + i].IsDisjoint(transitionSet))
                {
                    throw new InvalidOperationException("Deterministic lazy DFA compiler received overlapping transitions.");
                }
            }

            if (transitionSets.Count >= MaxDfaTransitions) throw new Pcre2DfaCompilationBudgetExceededException();
            transitionSets.Add(transitionSet);
            transitionTargets.Add(targetState);
            transitionCounts[stateIndex] = count + 1;
        }

        private bool TryAppendNfaStep(Utf8ConstructionBuffer<Pcre2DfaNfaState> states,
            ref int current, Pcre2AsciiDfaStep step, ref bool requiresBeginning,
            ref bool requiresEnd, bool isLastBeforeAccept)
        {
            ChargeCompilationStep();
            switch (step.Kind)
            {
                case Pcre2AsciiDfaStepKind.RequireBeginning:
                    if (current != 0) { _failureKind = Pcre2AsciiDfaCompileFailureKind.UnsupportedAnchoring; return false; }
                    requiresBeginning = true;
                    return true;
                case Pcre2AsciiDfaStepKind.RequireEnd:
                    if (!isLastBeforeAccept) { _failureKind = Pcre2AsciiDfaCompileFailureKind.UnsupportedAnchoring; return false; }
                    requiresEnd = true;
                    return true;
                case Pcre2AsciiDfaStepKind.Accept:
                    return true;
                case Pcre2AsciiDfaStepKind.MatchAsciiClass:
                case Pcre2AsciiDfaStepKind.LoopAsciiClass:
                    if (!AsciiCharClass.TryUseNonEmpty(step.CharClass, out var projected))
                    { _failureKind = Pcre2AsciiDfaCompileFailureKind.UnsupportedProjectedAsciiSet; return false; }
                    current = step.Kind == Pcre2AsciiDfaStepKind.MatchAsciiClass
                        ? AppendSet(states, current, projected)
                        : AppendLoop(states, current, projected, step.Min, step.Max);
                    return true;
                default:
                    return false;
            }
        }

        private int AppendLoop(Utf8ConstructionBuffer<Pcre2DfaNfaState> states,
            int current, AsciiCharClass set, int min, int max)
        {
            for (var index = 0; index < min; index++)
            {
                ChargeCompilationStep();
                current = AppendSet(states, current, set);
            }
            var exit = AddState(states);
            if (IsOpenEndedLoop(max))
            {
                states[current].EpsilonTransitions.Add(exit);
                var loopStart = current;
                current = AppendSet(states, current, set);
                states[current].EpsilonTransitions.Add(loopStart);
            }
            else
                for (var index = min; index < max; index++)
                {
                    ChargeCompilationStep();
                    states[current].EpsilonTransitions.Add(exit);
                    current = AppendSet(states, current, set);
                }
            states[current].EpsilonTransitions.Add(exit);
            return exit;
        }

        private int AppendSet(Utf8ConstructionBuffer<Pcre2DfaNfaState> states, int current, AsciiCharClass set)
        {
            var next = AddState(states);
            states[current].Transitions.Add(new Pcre2DfaNfaTransition(set, next));
            return next;
        }

        private int AddState(Utf8ConstructionBuffer<Pcre2DfaNfaState> states)
        {
            ChargeCompilationStep();
            if (states.Count >= MaxNfaStates)
            {
                throw new Pcre2DfaCompilationBudgetExceededException();
            }

            var index = states.Count;
            states.Add(Pcre2DfaNfaState.Create(_budget));
            return index;
        }

        private sealed class Pcre2DfaNfaState(Utf8ConstructionBuffer<int> epsilon, Utf8ConstructionBuffer<Pcre2DfaNfaTransition> transitions) : IDisposable
        {
            public Utf8ConstructionBuffer<int> EpsilonTransitions { get; } = epsilon;
            public Utf8ConstructionBuffer<Pcre2DfaNfaTransition> Transitions { get; } = transitions;
            internal static Pcre2DfaNfaState Create(Utf8ConstructionBudget budget)
            {
                budget.ChargeWork(1); // Pre-admit the mandatory cleanup walk.
                var epsilon = Utf8ConstructionBuffer<int>.Create(budget, true);
                var transitions = Utf8ConstructionBuffer<Pcre2DfaNfaTransition>.Create(budget, true);
                return budget.AllocateObject((epsilon, transitions), static state => new Pcre2DfaNfaState(state.epsilon, state.transitions));
            }
            public void Dispose() { EpsilonTransitions.Dispose(); Transitions.Dispose(); }
        }

        private readonly record struct Pcre2DfaNfaTransition(AsciiCharClass Set, int TargetState);

        private readonly record struct Pcre2DfaNfaStateSet(int[] States)
        {
            public bool Contains(int state)
            {
                for (var i = 0; i < States.Length; i++)
                {
                    Utf8ConstructionScope.ChargeWork(1);
                    if (States[i] == state)
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        private sealed class Pcre2DfaNfaStateSetComparer(Utf8ConstructionBudget budget) : IEqualityComparer<Pcre2DfaNfaStateSet>
        {

            public bool Equals(Pcre2DfaNfaStateSet x, Pcre2DfaNfaStateSet y)
            {
                budget.ChargeWork((ulong)Math.Min(x.States.Length, y.States.Length));
                return x.States.AsSpan().SequenceEqual(y.States);
            }

            public int GetHashCode(Pcre2DfaNfaStateSet obj)
            {
                budget.ChargeWork((ulong)obj.States.Length);
                var hash = new HashCode();
                foreach (var state in obj.States)
                {
                    hash.Add(state);
                }

                return hash.ToHashCode();
            }
        }

        private readonly record struct Pcre2DfaNfaGraph(Pcre2DfaNfaState[] States, int AcceptState, Utf8ConstructionBudget Budget) : IDisposable
        {
            public void Dispose()
            {
                foreach (var state in States) state.Dispose();
                Budget.ReleaseArray(States, false);
            }
        }

        private sealed class Pcre2DfaCompilationBudgetExceededException : Exception;
    }
}

internal enum Pcre2AsciiDfaCompileFailureKind : byte
{
    None = 0,
    EmptyProgram = 1,
    UnsupportedDeterministicStep = 5,
    UnsupportedStep = 6,
    UnsupportedProjectedAsciiSet = 9,
    UnsupportedAnchoring = 10,
    StateBudgetExceeded = 11,
}
