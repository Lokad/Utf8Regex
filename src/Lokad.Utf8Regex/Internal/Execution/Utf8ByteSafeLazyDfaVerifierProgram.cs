namespace Lokad.Utf8Regex.Internal.Execution;

internal readonly partial record struct Utf8ByteSafeLazyDfaCompileOutcome(
    Utf8ByteSafeLazyDfaVerifierProgram Program,
    Utf8ByteSafeLazyDfaCompileFailureKind FailureKind)
{
    public bool Succeeded => Program.HasValue;
}

internal readonly partial struct Utf8ByteSafeLazyDfaVerifierProgram
{
    private const int MaxDfaStates = 256;
    private const int MaxDfaTransitions = 4096;
    private const int MaxNfaStates = 2048;

    private readonly int[] _transitionOffsets;
    private readonly int[] _transitionCounts;
    private readonly AsciiCharClass[] _transitionSets;
    private readonly int[] _transitionTargets;
    private readonly bool[] _acceptingStates;
    private readonly Utf8ExecutionNodeKind _beginningKind;
    private readonly Utf8ExecutionNodeKind _endKind;
    private readonly RegexOptions _beginningOptions;
    private readonly RegexOptions _endOptions;

    private Utf8ByteSafeLazyDfaVerifierProgram(
        int[] transitionOffsets,
        int[] transitionCounts,
        AsciiCharClass[] transitionSets,
        int[] transitionTargets,
        bool[] acceptingStates,
        bool requiresBeginning,
        bool requiresEnd,
        Utf8ByteSafeLinearVerifierStep beginningAnchor,
        Utf8ByteSafeLinearVerifierStep endAnchor)
    {
        _beginningKind = beginningAnchor.AnchorKind;
        _endKind = endAnchor.AnchorKind;
        _beginningOptions = beginningAnchor.Options;
        _endOptions = endAnchor.Options;
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

    public int TransitionCount => _transitionTargets?.Length ?? 0;

    // PCRE2-INTEGRATION-POINT: shared byte-safe graph construction.
    public static Utf8ByteSafeLazyDfaCompileOutcome Compile(Utf8ByteSafeLinearVerifierProgram linearProgram)
    {
        if (!linearProgram.HasValue) return new(default, Utf8ByteSafeLazyDfaCompileFailureKind.EmptyProgram);
        using var scope = Utf8ConstructionScope.Enter();
        var budget = scope.Budget;
        budget.EnterDepth();
        try
        {
            var compiler = budget.AllocateObject((owner: budget, failure: Utf8ByteSafeLazyDfaCompileFailureKind.None,
                beginning: default(Utf8ByteSafeLinearVerifierStep), end: default(Utf8ByteSafeLinearVerifierStep), optimizerSteps: 0UL),
                static state => new Compiler(state.owner));
            var outcome = compiler.Compile(linearProgram);
            if (outcome.Succeeded) scope.Complete();
            return outcome;
        }
        finally { budget.LeaveDepth(); }
    }

    private sealed partial class Compiler
    {
        private const ulong MaxOptimizationSteps = 1_048_576;
        private ulong _optimizationSteps;
        private Utf8ByteSafeLazyDfaCompileFailureKind _failureKind;
        private Utf8ByteSafeLinearVerifierStep _beginningAnchor;
        private Utf8ByteSafeLinearVerifierStep _endAnchor;
        private readonly Utf8ConstructionBudget _budget;

        public Compiler(Utf8ConstructionBudget budget) => _budget = budget;

        private Utf8ConstructionBuffer<T> Buffer<T>(bool instructions) =>
            Utf8ConstructionBuffer<T>.Create(_budget, instructions);

        public Utf8ByteSafeLazyDfaCompileOutcome Compile(Utf8ByteSafeLinearVerifierProgram linearProgram)
        {
            try
            {
                if (!linearProgram.HasValue)
                {
                    return new Utf8ByteSafeLazyDfaCompileOutcome(default, Utf8ByteSafeLazyDfaCompileFailureKind.EmptyProgram);
                }

                if (IsCurrentlyTooComplex(linearProgram.Steps))
                {
                    if (_failureKind == Utf8ByteSafeLazyDfaCompileFailureKind.None)
                    {
                        _failureKind = Utf8ByteSafeLazyDfaCompileFailureKind.ComplexLoopShape;
                    }

                    return new Utf8ByteSafeLazyDfaCompileOutcome(default, _failureKind);
                }

                if (!TryReadAnchors(linearProgram.Steps))
                    return new(default, Utf8ByteSafeLazyDfaCompileFailureKind.UnsupportedAnchoring);

                if (TryCompileDeterministicSequence(linearProgram.Steps, out var deterministicProgram))
                {
                    return new Utf8ByteSafeLazyDfaCompileOutcome(deterministicProgram, Utf8ByteSafeLazyDfaCompileFailureKind.None);
                }

                if (!TryCompileNfa(linearProgram.Steps, out var nfa, out var requiresBeginning, out var requiresEnd))
                {
                    if (_failureKind == Utf8ByteSafeLazyDfaCompileFailureKind.None)
                    {
                        _failureKind = Utf8ByteSafeLazyDfaCompileFailureKind.UnsupportedStep;
                    }

                    return new Utf8ByteSafeLazyDfaCompileOutcome(default, _failureKind);
                }

                using var ownedNfa = nfa;
                var program = BuildDfa(nfa, requiresBeginning, requiresEnd);
                if (!program.HasValue && _failureKind == Utf8ByteSafeLazyDfaCompileFailureKind.None)
                {
                    _failureKind = Utf8ByteSafeLazyDfaCompileFailureKind.StateBudgetExceeded;
                }

                return new Utf8ByteSafeLazyDfaCompileOutcome(program, program.HasValue ? Utf8ByteSafeLazyDfaCompileFailureKind.None : _failureKind);
            }
            catch (CompilationBudgetExceededException)
            {
                return new(default, Utf8ByteSafeLazyDfaCompileFailureKind.StateBudgetExceeded);
            }
        }
        private bool IsCurrentlyTooComplex(Utf8ByteSafeLinearVerifierStep[] steps)
        {
            for (var i = 0; i < steps.Length; i++)
            {
                ChargeCompilationStep();
                var step = steps[i];
                switch (step.Kind)
                {
                    case Utf8ByteSafeLinearVerifierStepKind.LoopProgram:
                        if (IsSupportedDeterministicLoopProgram(step))
                        {
                            continue;
                        }

                        _failureKind = Utf8ByteSafeLazyDfaCompileFailureKind.LoopProgram;
                        return true;
                }
            }

            return false;
        }

        private bool IsSupportedDeterministicLoopProgram(Utf8ByteSafeLinearVerifierStep step)
        {
            if (IsSupportedDeterministicOptionalProgram(step))
            {
                return true;
            }

            if (step.Program is not { Length: > 0 } program ||
                step.Min != 1 ||
                !IsOpenEndedLoop(step.Max))
            {
                return false;
            }

            var previousSet = AsciiCharClass.Empty;
            var hasPrevious = false;
            for (var i = 0; i < program.Length; i++)
            {
                ChargeCompilationStep();
                if (!TryGetDisjointLoopSet(program[i], out var currentSet))
                {
                    return false;
                }

                if (!IsOpenEndedLoop(program[i].Max))
                {
                    return false;
                }

                if (program[i].Min != 1 && !(i == program.Length - 1 && program[i].Min == 0))
                {
                    return false;
                }

                if (hasPrevious && !previousSet.IsDisjoint(currentSet))
                {
                    return false;
                }

                previousSet = currentSet;
                hasPrevious = true;
            }

            return true;
        }

        private bool TryCompileDeterministicSequence(
            Utf8ByteSafeLinearVerifierStep[] steps,
            out Utf8ByteSafeLazyDfaVerifierProgram program)
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
                        case Utf8ByteSafeLinearVerifierStepKind.RequireBeginning:
                            if (stateIndex != 0)
                            {
                                program = default;
                                return false;
                            }

                            requiresBeginning = true;
                            break;

                        case Utf8ByteSafeLinearVerifierStepKind.RequireEnd:
                            requiresEnd = true;
                            break;

                        case Utf8ByteSafeLinearVerifierStepKind.Accept:
                            accepting[stateIndex] = true;
                            while (transitionOffsets.Count < accepting.Count)
                            {
                                transitionOffsets.Add(0);
                                transitionCounts.Add(0);
                            }

                            program = new Utf8ByteSafeLazyDfaVerifierProgram(
                                transitionOffsets.Take(),
                                transitionCounts.Take(),
                                transitionSets.Take(),
                                transitionTargets.Take(),
                                accepting.Take(),
                                requiresBeginning,
                                requiresEnd, _beginningAnchor, _endAnchor);
                            attempt.Complete();
                            return true;

                        case Utf8ByteSafeLinearVerifierStepKind.MatchByte:
                            stateIndex = AppendDeterministicTransition(
                                step,
                                AsciiCharClass.ForByte(step.Value),
                                stateIndex,
                                accepting,
                                transitionOffsets,
                                transitionCounts,
                                transitionSets,
                                transitionTargets);
                            break;

                        case Utf8ByteSafeLinearVerifierStepKind.MatchText:
                            foreach (var value in step.Text)
                            {
                                stateIndex = AppendDeterministicTransition(
                                    step,
                                    AsciiCharClass.ForByte(value),
                                    stateIndex,
                                    accepting,
                                    transitionOffsets,
                                    transitionCounts,
                                    transitionSets,
                                    transitionTargets);
                            }
                            break;

                        case Utf8ByteSafeLinearVerifierStepKind.MatchAsciiClass:
                            if (!AsciiCharClass.TryUseNonEmpty(step.CharClass, out var projectedMatchSet))
                            {
                                _failureKind = Utf8ByteSafeLazyDfaCompileFailureKind.UnsupportedProjectedAsciiSet;
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

                        case Utf8ByteSafeLinearVerifierStepKind.LoopByte when step.Min == step.Max:
                            for (var i = 0; i < step.Min; i++)
                            {
                                stateIndex = AppendDeterministicTransition(
                                    step,
                                    AsciiCharClass.ForByte(step.Value),
                                    stateIndex,
                                    accepting,
                                    transitionOffsets,
                                    transitionCounts,
                                    transitionSets,
                                    transitionTargets);
                            }
                            break;

                        case Utf8ByteSafeLinearVerifierStepKind.LoopText when step.Min == step.Max:
                            for (var repeat = 0; repeat < step.Min; repeat++)
                            {
                                foreach (var value in step.Text)
                                {
                                    stateIndex = AppendDeterministicTransition(
                                        step,
                                        AsciiCharClass.ForByte(value),
                                        stateIndex,
                                        accepting,
                                        transitionOffsets,
                                        transitionCounts,
                                        transitionSets,
                                        transitionTargets);
                                }
                            }
                            break;

                        case Utf8ByteSafeLinearVerifierStepKind.LoopAsciiClass when step.Min == step.Max:
                            if (!AsciiCharClass.TryUseNonEmpty(step.CharClass, out var projectedLoopSet))
                            {
                                _failureKind = Utf8ByteSafeLazyDfaCompileFailureKind.UnsupportedProjectedAsciiSet;
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

                        case Utf8ByteSafeLinearVerifierStepKind.LoopAsciiClass when
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

                        case Utf8ByteSafeLinearVerifierStepKind.LoopProgram when
                            step.Min == 0 &&
                            step.Max == 1 &&
                            step.Program is { Length: > 0 } optionalProgram &&
                            TryAppendDeterministicOptionalProgram(
                                optionalProgram,
                                ref stateIndex,
                                accepting,
                                transitionOffsets,
                                transitionCounts,
                                transitionSets,
                                transitionTargets):
                            break;

                        default:
                            _failureKind = Utf8ByteSafeLazyDfaCompileFailureKind.UnsupportedDeterministicStep;
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
            Utf8ByteSafeLinearVerifierStep[] steps,
            out Utf8ByteSafeLazyDfaVerifierProgram program)
        {
            using var attempt = Utf8ConstructionScope.Enter();
            _budget.EnterDepth();
            try
            {
                program = default;
                if (steps.Length < 3 || steps[^1].Kind != Utf8ByteSafeLinearVerifierStepKind.Accept)
                {
                    return false;
                }

                var requiresBeginning = false;
                var requiresEnd = false;
                var start = 0;
                var endExclusive = steps.Length - 1;

                if (steps[start].Kind == Utf8ByteSafeLinearVerifierStepKind.RequireBeginning)
                {
                    requiresBeginning = true;
                    start++;
                }

                if (endExclusive > start && steps[endExclusive - 1].Kind == Utf8ByteSafeLinearVerifierStepKind.RequireEnd)
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

                var repeatedSegments = false;
                ReadOnlySpan<Utf8ByteSafeLinearVerifierStep> coreSteps;
                if (payloadSteps.Length == 1 &&
                    payloadSteps[0].Kind == Utf8ByteSafeLinearVerifierStepKind.LoopProgram &&
                    payloadSteps[0].Program is { Length: > 0 } subProgram &&
                    payloadSteps[0].Min == 1 &&
                    IsOpenEndedLoop(payloadSteps[0].Max))
                {
                    repeatedSegments = true;
                    coreSteps = subProgram;
                }
                else
                {
                    coreSteps = payloadSteps;
                }

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

                    if (repeatedSegments)
                    {
                        var firstSet = loopSets[0];
                        for (var stateIndex = 0; stateIndex < accepting.Count; stateIndex++)
                        {
                            ChargeCompilationStep();
                            if (!accepting[stateIndex])
                            {
                                continue;
                            }

                            AddDeterministicTransition(stateIndex, firstSet, 1, transitionOffsets, transitionCounts, transitionSets, transitionTargets);
                        }
                    }

                    program = new Utf8ByteSafeLazyDfaVerifierProgram(
                        transitionOffsets.Take(),
                        transitionCounts.Take(),
                        transitionSets.Take(),
                        transitionTargets.Take(),
                        accepting.Take(),
                        requiresBeginning,
                        requiresEnd, _beginningAnchor, _endAnchor);
                    attempt.Complete();
                    return true;
                }
                finally { _budget.ReleaseArray(loopSets, false); }
            }
            finally { _budget.LeaveDepth(); }
        }

        private bool TryGetDisjointLoopSet(Utf8ByteSafeLinearVerifierStep step, out AsciiCharClass set)
        {
            switch (step.Kind)
            {
                case Utf8ByteSafeLinearVerifierStepKind.LoopByte:
                    set = AsciiCharClass.ForByte(step.Value);
                    return true;

                case Utf8ByteSafeLinearVerifierStepKind.LoopAsciiClass:
                    if (AsciiCharClass.TryUseNonEmpty(step.CharClass, out set))
                    {
                        return true;
                    }

                    break;
            }

            set = AsciiCharClass.Empty;
            return false;
        }

        private bool TryGetFirstDeterministicTransitionSet(
            Utf8ByteSafeLinearVerifierStep[] steps,
            int startIndex,
            out AsciiCharClass set)
        {
            while (true)
            {
                var descended = false;
                for (var i = startIndex; i < steps.Length; i++)
                {
                    ChargeCompilationStep();
                    var step = steps[i];
                    switch (step.Kind)
                    {
                        case Utf8ByteSafeLinearVerifierStepKind.RequireBeginning:
                        case Utf8ByteSafeLinearVerifierStepKind.RequireEnd:
                        case Utf8ByteSafeLinearVerifierStepKind.Accept:
                            continue;

                        case Utf8ByteSafeLinearVerifierStepKind.MatchByte:
                            set = AsciiCharClass.ForByte(step.Value);
                            return true;

                        case Utf8ByteSafeLinearVerifierStepKind.MatchText when step.Text is { Length: > 0 } text:
                            set = AsciiCharClass.ForByte(text[0]);
                            return true;

                        case Utf8ByteSafeLinearVerifierStepKind.MatchAsciiClass:
                            return AsciiCharClass.TryUseNonEmpty(step.CharClass, out set);

                        case Utf8ByteSafeLinearVerifierStepKind.LoopByte:
                            set = AsciiCharClass.ForByte(step.Value);
                            return true;

                        case Utf8ByteSafeLinearVerifierStepKind.LoopAsciiClass:
                            return AsciiCharClass.TryUseNonEmpty(step.CharClass, out set);

                        case Utf8ByteSafeLinearVerifierStepKind.LoopProgram when
                            step.Min == 0 &&
                            step.Max == 1 &&
                            step.Program is { Length: > 0 } optionalProgram:
                            steps = optionalProgram;
                            startIndex = 0;
                            descended = true;
                            break;

                        default:
                            set = AsciiCharClass.Empty;
                            return false;
                    }
                    if (descended) break;
                }
                if (descended) continue;

                set = AsciiCharClass.Empty;
                return false;
            }
        }

        private bool TryAppendDeterministicOptionalProgram(
            Utf8ByteSafeLinearVerifierStep[] steps,
            ref int stateIndex,
            Utf8ConstructionBuffer<bool> accepting,
            Utf8ConstructionBuffer<int> transitionOffsets,
            Utf8ConstructionBuffer<int> transitionCounts,
            Utf8ConstructionBuffer<AsciiCharClass> transitionSets,
            Utf8ConstructionBuffer<int> transitionTargets)
        {
            accepting[stateIndex] = true;
            return TryAppendDeterministicSubProgram(
                steps,
                ref stateIndex,
                accepting,
                transitionOffsets,
                transitionCounts,
                transitionSets,
                transitionTargets);
        }

        private bool TryAppendDeterministicSubProgram(
            Utf8ByteSafeLinearVerifierStep[] steps,
            ref int stateIndex,
            Utf8ConstructionBuffer<bool> accepting,
            Utf8ConstructionBuffer<int> transitionOffsets,
            Utf8ConstructionBuffer<int> transitionCounts,
            Utf8ConstructionBuffer<AsciiCharClass> transitionSets,
            Utf8ConstructionBuffer<int> transitionTargets)
        {
            for (var i = 0; i < steps.Length; i++)
            {
                ChargeCompilationStep();
                var step = steps[i];
                switch (step.Kind)
                {
                    case Utf8ByteSafeLinearVerifierStepKind.MatchByte:
                        stateIndex = AppendDeterministicTransition(
                            step,
                            AsciiCharClass.ForByte(step.Value),
                            stateIndex,
                            accepting,
                            transitionOffsets,
                            transitionCounts,
                            transitionSets,
                            transitionTargets);
                        break;

                    case Utf8ByteSafeLinearVerifierStepKind.MatchText:
                        foreach (var value in step.Text)
                        {
                            stateIndex = AppendDeterministicTransition(
                                step,
                                AsciiCharClass.ForByte(value),
                                stateIndex,
                                accepting,
                                transitionOffsets,
                                transitionCounts,
                                transitionSets,
                                transitionTargets);
                        }
                        break;

                    case Utf8ByteSafeLinearVerifierStepKind.MatchAsciiClass:
                        if (!AsciiCharClass.TryUseNonEmpty(step.CharClass, out var projectedMatchSet))
                        {
                            _failureKind = Utf8ByteSafeLazyDfaCompileFailureKind.UnsupportedProjectedAsciiSet;
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

                    case Utf8ByteSafeLinearVerifierStepKind.LoopAsciiClass:
                        if (!AsciiCharClass.TryUseNonEmpty(step.CharClass, out var projectedLoopSet))
                        {
                            _failureKind = Utf8ByteSafeLazyDfaCompileFailureKind.UnsupportedProjectedAsciiSet;
                            return false;
                        }

                        if (step.Min == 0 && step.Max == 1)
                        {
                            AddDeterministicTransition(
                                stateIndex,
                                projectedLoopSet,
                                stateIndex,
                                transitionOffsets,
                                transitionCounts,
                                transitionSets,
                                transitionTargets);
                            break;
                        }

                        if (!TryAppendRepeatedDisjointLoopSequence(
                                System.Runtime.InteropServices.MemoryMarshal.CreateReadOnlySpan(ref step, 1),
                                ref stateIndex,
                                accepting,
                                transitionOffsets,
                                transitionCounts,
                                transitionSets,
                                transitionTargets))
                        {
                            return false;
                        }

                        break;

                    case Utf8ByteSafeLinearVerifierStepKind.LoopProgram when
                        step.Min == 1 &&
                        IsOpenEndedLoop(step.Max) &&
                        step.Program is { Length: > 0 } repeatedProgram:
                        if (!TryAppendRepeatedDisjointLoopSequence(
                                repeatedProgram,
                                ref stateIndex,
                                accepting,
                                transitionOffsets,
                                transitionCounts,
                                transitionSets,
                                transitionTargets))
                        {
                            return false;
                        }

                        break;

                    default:
                        _failureKind = Utf8ByteSafeLazyDfaCompileFailureKind.UnsupportedDeterministicStep;
                        return false;
                }
            }

            return true;
        }

        private bool IsSupportedDeterministicOptionalProgram(Utf8ByteSafeLinearVerifierStep step)
        {
            if (step.Program is not { Length: > 0 } program ||
                step.Min != 0 ||
                step.Max != 1)
            {
                return false;
            }

            for (var i = 0; i < program.Length; i++)
            {
                ChargeCompilationStep();
                if (!IsSupportedDeterministicSubProgramStep(program[i]))
                {
                    return false;
                }
            }

            return true;
        }

        private bool IsSupportedDeterministicSubProgramStep(Utf8ByteSafeLinearVerifierStep step)
        {
            switch (step.Kind)
            {
                case Utf8ByteSafeLinearVerifierStepKind.MatchByte:
                case Utf8ByteSafeLinearVerifierStepKind.MatchText:
                    return true;

                case Utf8ByteSafeLinearVerifierStepKind.MatchAsciiClass:
                    return !step.CharClass.IsEmpty;

                case Utf8ByteSafeLinearVerifierStepKind.LoopAsciiClass:
                    return !step.CharClass.IsEmpty &&
                        ((step.Min == 0 && step.Max == 1) || IsOpenEndedLoop(step.Max));

                case Utf8ByteSafeLinearVerifierStepKind.LoopProgram:
                    return step.Program is { Length: > 0 } nestedProgram &&
                        step.Min == 1 &&
                        IsOpenEndedLoop(step.Max) &&
                        AreDisjointLoopSteps(nestedProgram);

                default:
                    return false;
            }
        }

        private bool AreDisjointLoopSteps(Utf8ByteSafeLinearVerifierStep[] steps)
        {
            var previousSet = AsciiCharClass.Empty;
            var hasPrevious = false;
            for (var i = 0; i < steps.Length; i++)
            {
                ChargeCompilationStep();
                if (!TryGetDisjointLoopSet(steps[i], out var currentSet))
                {
                    return false;
                }

                if (!IsOpenEndedLoop(steps[i].Max))
                {
                    return false;
                }

                if (steps[i].Min != 1 && !(i == steps.Length - 1 && steps[i].Min == 0))
                {
                    return false;
                }

                if (hasPrevious && !previousSet.IsDisjoint(currentSet))
                {
                    return false;
                }

                previousSet = currentSet;
                hasPrevious = true;
            }

            return hasPrevious;
        }

        private bool TryAppendRepeatedDisjointLoopSequence(
            ReadOnlySpan<Utf8ByteSafeLinearVerifierStep> steps,
            ref int stateIndex,
            Utf8ConstructionBuffer<bool> accepting,
            Utf8ConstructionBuffer<int> transitionOffsets,
            Utf8ConstructionBuffer<int> transitionCounts,
            Utf8ConstructionBuffer<AsciiCharClass> transitionSets,
            Utf8ConstructionBuffer<int> transitionTargets)
        {
            if (steps.Length == 0)
            {
                return false;
            }

            if (steps.Length >= MaxDfaStates) return false;
            var loopSets = _budget.AllocateArray<AsciiCharClass>(steps.Length, false);
            try
            {
                for (var i = 0; i < steps.Length; i++)
                {
                    ChargeCompilationStep();
                    if (!TryGetDisjointLoopSet(steps[i], out loopSets[i]))
                    {
                        return false;
                    }

                    if (!IsOpenEndedLoop(steps[i].Max))
                    {
                        return false;
                    }

                    if (steps[i].Min != 1 && !(i == steps.Length - 1 && steps[i].Min == 0))
                    {
                        return false;
                    }

                    if (i > 0 && !loopSets[i - 1].IsDisjoint(loopSets[i]))
                    {
                        return false;
                    }
                }

                var firstStageState = -1;
                var previousStageState = stateIndex;
                for (var i = 0; i < loopSets.Length; i++)
                {
                    var stageState = AddDfaState(accepting);
                    transitionOffsets.Add(0);
                    transitionCounts.Add(0);

                    AddDeterministicTransition(previousStageState, loopSets[i], stageState, transitionOffsets, transitionCounts, transitionSets, transitionTargets);
                    AddDeterministicTransition(stageState, loopSets[i], stageState, transitionOffsets, transitionCounts, transitionSets, transitionTargets);

                    if (i == 0)
                    {
                        firstStageState = stageState;
                    }

                    if (i == loopSets.Length - 1)
                    {
                        accepting[stageState] = true;
                        if (steps[i].Min == 0)
                        {
                            accepting[previousStageState] = true;
                        }
                    }

                    previousStageState = stageState;
                }

                for (var i = 0; i < accepting.Count; i++)
                {
                    ChargeCompilationStep();
                    if (accepting[i])
                    {
                        AddDeterministicTransition(i, loopSets[0], firstStageState, transitionOffsets, transitionCounts, transitionSets, transitionTargets);
                    }
                }

                stateIndex = previousStageState;
                return true;
            }
            finally { _budget.ReleaseArray(loopSets, false); }
        }

        private bool IsOpenEndedLoop(int max) => max < 0 || max == int.MaxValue;

        private int AppendDeterministicTransition(
            Utf8ByteSafeLinearVerifierStep step,
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
                if (transitionSets.Count >= MaxDfaTransitions) throw new CompilationBudgetExceededException();
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

            if (transitionSets.Count >= MaxDfaTransitions) throw new CompilationBudgetExceededException();
            transitionSets.Add(transitionSet);
            transitionTargets.Add(targetState);
            transitionCounts[stateIndex] = count + 1;
        }

        private bool TryAppendNfaStep(
            Utf8ConstructionBuffer<NfaState> states,
            ref int current,
            Utf8ByteSafeLinearVerifierStep step,
            bool isTopLevel,
            ref bool requiresBeginning,
            ref bool requiresEnd,
            bool isLastBeforeAccept)
        {
            ChargeCompilationStep();
            switch (step.Kind)
            {
                case Utf8ByteSafeLinearVerifierStepKind.RequireBeginning:
                    if (!isTopLevel || current != 0)
                    {
                        _failureKind = Utf8ByteSafeLazyDfaCompileFailureKind.UnsupportedAnchoring;
                        return false;
                    }

                    requiresBeginning = true;
                    return true;

                case Utf8ByteSafeLinearVerifierStepKind.RequireEnd:
                    if (!isTopLevel || !isLastBeforeAccept)
                    {
                        _failureKind = Utf8ByteSafeLazyDfaCompileFailureKind.UnsupportedAnchoring;
                        return false;
                    }

                    requiresEnd = true;
                    return true;

                case Utf8ByteSafeLinearVerifierStepKind.Accept:
                    return isTopLevel;

                case Utf8ByteSafeLinearVerifierStepKind.MatchByte:
                    current = AppendSet(states, current, AsciiCharClass.ForByte(step.Value));
                    return true;

                case Utf8ByteSafeLinearVerifierStepKind.MatchText:
                    foreach (var value in step.Text)
                    {
                        current = AppendSet(states, current, AsciiCharClass.ForByte(value));
                    }
                    return true;

                case Utf8ByteSafeLinearVerifierStepKind.MatchAsciiClass:
                    if (!AsciiCharClass.TryUseNonEmpty(step.CharClass, out var projectedSet))
                    {
                        _failureKind = Utf8ByteSafeLazyDfaCompileFailureKind.UnsupportedProjectedAsciiSet;
                        return false;
                    }

                    current = AppendSet(states, current, projectedSet);
                    return true;

                case Utf8ByteSafeLinearVerifierStepKind.LoopByte:
                    current = AppendLoop(states, current, AsciiCharClass.ForByte(step.Value), step.Min, step.Max);
                    return true;

                case Utf8ByteSafeLinearVerifierStepKind.LoopText:
                    if (step.Text.Length >= MaxNfaStates) throw new CompilationBudgetExceededException();
                    using (var units = Buffer<AsciiCharClass>(false))
                    {
                        foreach (var value in step.Text)
                        {
                            ChargeCompilationStep();
                            units.Add(AsciiCharClass.ForByte(value));
                        }
                        current = AppendLoop(states, current, units.Span, step.Min, step.Max);
                    }
                    return true;

                case Utf8ByteSafeLinearVerifierStepKind.LoopAsciiClass:
                    if (!AsciiCharClass.TryUseNonEmpty(step.CharClass, out var projectedLoopSet))
                    {
                        _failureKind = Utf8ByteSafeLazyDfaCompileFailureKind.UnsupportedProjectedAsciiSet;
                        return false;
                    }

                    current = AppendLoop(states, current, projectedLoopSet, step.Min, step.Max);
                    return true;

                case Utf8ByteSafeLinearVerifierStepKind.MatchAnyText:
                    current = AppendAlternatives(states, current, step.Alternatives, optional: false);
                    return true;

                case Utf8ByteSafeLinearVerifierStepKind.MatchAnyTextOptional:
                    current = AppendAlternatives(states, current, step.Alternatives, optional: true);
                    return true;

                case Utf8ByteSafeLinearVerifierStepKind.LoopAnyText:
                    current = AppendAlternativeLoop(states, current, step.Alternatives, step.Min, step.Max);
                    return true;

                default:
                    _failureKind = Utf8ByteSafeLazyDfaCompileFailureKind.UnsupportedStep;
                    return false;
            }
        }

        private int AppendLoop(Utf8ConstructionBuffer<NfaState> states, int current, AsciiCharClass set, int min, int max)
        {
            return AppendLoop(states, current, System.Runtime.InteropServices.MemoryMarshal.CreateReadOnlySpan(ref set, 1), min, max);
        }

        private int AppendLoop(Utf8ConstructionBuffer<NfaState> states, int current, ReadOnlySpan<AsciiCharClass> units, int min, int max)
        {
            for (var i = 0; i < min; i++)
            {
                ChargeCompilationStep();
                current = AppendSequence(states, current, units);
            }

            var exitState = AddState(states);
            if (max < 0)
            {
                states[current].EpsilonTransitions.Add(exitState);
                var loopStart = current;
                current = AppendSequence(states, current, units);
                states[current].EpsilonTransitions.Add(loopStart);
                states[current].EpsilonTransitions.Add(exitState);
                return exitState;
            }

            for (var i = min; i < max; i++)
            {
                ChargeCompilationStep();
                states[current].EpsilonTransitions.Add(exitState);
                current = AppendSequence(states, current, units);
            }

            states[current].EpsilonTransitions.Add(exitState);
            return exitState;
        }

        private int AppendSequence(Utf8ConstructionBuffer<NfaState> states, int current, ReadOnlySpan<AsciiCharClass> units)
        {
            for (var i = 0; i < units.Length; i++)
            {
                current = AppendSet(states, current, units[i]);
            }

            return current;
        }

        private int AppendAlternatives(Utf8ConstructionBuffer<NfaState> states, int current, byte[][] alternatives, bool optional)
        {
            var exitState = AddState(states);
            if (optional)
            {
                states[current].EpsilonTransitions.Add(exitState);
            }

            for (var i = 0; i < alternatives.Length; i++)
            {
                ChargeCompilationStep();
                var branchState = AddState(states);
                states[current].EpsilonTransitions.Add(branchState);
                var branchCurrent = branchState;
                foreach (var value in alternatives[i])
                {
                    branchCurrent = AppendSet(states, branchCurrent, AsciiCharClass.ForByte(value));
                }

                states[branchCurrent].EpsilonTransitions.Add(exitState);
            }

            return exitState;
        }

        private int AppendAlternativeLoop(Utf8ConstructionBuffer<NfaState> states, int current, byte[][] alternatives, int min, int max)
        {
            for (var i = 0; i < min; i++)
            {
                ChargeCompilationStep();
                current = AppendAlternatives(states, current, alternatives, optional: false);
            }

            var exitState = AddState(states);
            if (max < 0)
            {
                states[current].EpsilonTransitions.Add(exitState);
                var loopStart = current;
                current = AppendAlternatives(states, current, alternatives, optional: false);
                states[current].EpsilonTransitions.Add(loopStart);
                states[current].EpsilonTransitions.Add(exitState);
                return exitState;
            }

            for (var i = min; i < max; i++)
            {
                ChargeCompilationStep();
                states[current].EpsilonTransitions.Add(exitState);
                current = AppendAlternatives(states, current, alternatives, optional: false);
            }

            states[current].EpsilonTransitions.Add(exitState);
            return exitState;
        }

        private int AppendSet(Utf8ConstructionBuffer<NfaState> states, int current, AsciiCharClass set)
        {
            var next = AddState(states);
            states[current].Transitions.Add(new NfaTransition(new TransitionKey(set), next));
            return next;
        }

        private int AddState(Utf8ConstructionBuffer<NfaState> states)
        {
            ChargeCompilationStep();
            if (states.Count >= MaxNfaStates)
            {
                throw new CompilationBudgetExceededException();
            }

            var index = states.Count;
            states.Add(NfaState.Create(_budget));
            return index;
        }

        private sealed class NfaState(Utf8ConstructionBuffer<int> epsilon, Utf8ConstructionBuffer<NfaTransition> transitions) : IDisposable
        {
            public Utf8ConstructionBuffer<int> EpsilonTransitions { get; } = epsilon;
            public Utf8ConstructionBuffer<NfaTransition> Transitions { get; } = transitions;
            internal static NfaState Create(Utf8ConstructionBudget budget)
            {
                budget.ChargeWork(1); // Pre-admit the mandatory cleanup walk.
                var epsilon = Utf8ConstructionBuffer<int>.Create(budget, true);
                var transitions = Utf8ConstructionBuffer<NfaTransition>.Create(budget, true);
                return budget.AllocateObject((epsilon, transitions), static state => new NfaState(state.epsilon, state.transitions));
            }
            public void Dispose() { EpsilonTransitions.Dispose(); Transitions.Dispose(); }
        }

        private readonly record struct NfaTransition(TransitionKey Key, int TargetState);

        private readonly record struct TransitionKey(AsciiCharClass Set)
        {
            public ulong SortKey => Set.LowMask ^ (Set.HighMask * 31UL);
        }

        private readonly record struct NfaStateSet(int[] States)
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

        private sealed class NfaStateSetComparer(Utf8ConstructionBudget budget) : IEqualityComparer<NfaStateSet>
        {


            public bool Equals(NfaStateSet x, NfaStateSet y)
            {
                budget.ChargeWork((ulong)Math.Min(x.States.Length, y.States.Length));
                return x.States.AsSpan().SequenceEqual(y.States);
            }

            public int GetHashCode(NfaStateSet obj)
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

        private sealed class TransitionKeyComparer : IEqualityComparer<TransitionKey>
        {
            public static TransitionKeyComparer Instance { get; } = new();

            public bool Equals(TransitionKey x, TransitionKey y)
            {
                return x.Set.LowMask == y.Set.LowMask && x.Set.HighMask == y.Set.HighMask;
            }

            public int GetHashCode(TransitionKey obj)
            {
                return HashCode.Combine(obj.Set.LowMask, obj.Set.HighMask);
            }
        }

        private readonly record struct NfaGraph(NfaState[] States, int AcceptState, Utf8ConstructionBudget Budget) : IDisposable
        {
            public void Dispose()
            {
                foreach (var state in States) state.Dispose();
                Budget.ReleaseArray(States, false);
            }
        }

        private sealed class CompilationBudgetExceededException : Exception;
    }
}

internal enum Utf8ByteSafeLazyDfaCompileFailureKind : byte
{
    None = 0,
    EmptyProgram = 1,
    ComplexLoopShape = 2,
    LoopProgram = 3,
    VariableLoop = 4,
    UnsupportedDeterministicStep = 5,
    UnsupportedStep = 6,
    UnsupportedSubProgram = 7,
    UnsupportedProjectedAsciiSet = 9,
    UnsupportedAnchoring = 10,
    StateBudgetExceeded = 11,
}
