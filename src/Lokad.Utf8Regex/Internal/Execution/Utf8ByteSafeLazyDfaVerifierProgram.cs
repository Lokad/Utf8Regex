using RuntimeFrontEnd = Lokad.Utf8Regex.Internal.FrontEnd.Runtime;

namespace Lokad.Utf8Regex.Internal.Execution;

internal readonly struct Utf8ByteSafeLazyDfaVerifierProgram
{
    private const int MaxDfaStates = 256;
    private const int MaxDfaTransitions = 4096;
    private const int MaxNfaStates = 2048;

    [ThreadStatic]
    private static Utf8ByteSafeLazyDfaCompileFailureKind s_lastFailureKind;

    private readonly int[] _transitionOffsets;
    private readonly int[] _transitionCounts;
    private readonly Utf8AsciiByteSet[] _transitionSets;
    private readonly int[] _transitionTargets;
    private readonly bool[] _acceptingStates;

    private Utf8ByteSafeLazyDfaVerifierProgram(
        int[] transitionOffsets,
        int[] transitionCounts,
        Utf8AsciiByteSet[] transitionSets,
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

    public int TransitionCount => _transitionTargets?.Length ?? 0;

    public static Utf8ByteSafeLazyDfaVerifierProgram Create(Utf8ByteSafeLinearVerifierProgram linearProgram)
    {
        s_lastFailureKind = Utf8ByteSafeLazyDfaCompileFailureKind.None;
        if (!linearProgram.HasValue)
        {
            s_lastFailureKind = Utf8ByteSafeLazyDfaCompileFailureKind.EmptyProgram;
            return default;
        }

        if (IsCurrentlyTooComplex(linearProgram.Steps))
        {
            if (s_lastFailureKind == Utf8ByteSafeLazyDfaCompileFailureKind.None)
            {
                s_lastFailureKind = Utf8ByteSafeLazyDfaCompileFailureKind.ComplexLoopShape;
            }

            return default;
        }

        if (TryCompileDeterministicSequence(linearProgram.Steps, out var deterministicProgram))
        {
            return deterministicProgram;
        }

        if (!TryCompileNfa(linearProgram.Steps, out var nfa, out var requiresBeginning, out var requiresEnd))
        {
            if (s_lastFailureKind == Utf8ByteSafeLazyDfaCompileFailureKind.None)
            {
                s_lastFailureKind = Utf8ByteSafeLazyDfaCompileFailureKind.UnsupportedStep;
            }

            return default;
        }

        var program = BuildDfa(nfa, requiresBeginning, requiresEnd);
        if (!program.HasValue && s_lastFailureKind == Utf8ByteSafeLazyDfaCompileFailureKind.None)
        {
            s_lastFailureKind = Utf8ByteSafeLazyDfaCompileFailureKind.StateBudgetExceeded;
        }

        return program;
    }

    public static Utf8ByteSafeLazyDfaCompileFailureKind GetCompileFailureKind(Utf8ByteSafeLinearVerifierProgram linearProgram)
    {
        _ = Create(linearProgram);
        return s_lastFailureKind;
    }

    private static bool IsCurrentlyTooComplex(IReadOnlyList<Utf8ByteSafeLinearVerifierStep> steps)
    {
        for (var i = 0; i < steps.Count; i++)
        {
            var step = steps[i];
            switch (step.Kind)
            {
                case Utf8ByteSafeLinearVerifierStepKind.LoopProgram:
                    if (IsSupportedDeterministicLoopProgram(step))
                    {
                        continue;
                    }

                    s_lastFailureKind = Utf8ByteSafeLazyDfaCompileFailureKind.LoopProgram;
                    return true;
            }
        }

        return false;
    }

    private static bool IsSupportedDeterministicLoopProgram(Utf8ByteSafeLinearVerifierStep step)
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

        var previousSet = default(Utf8AsciiByteSet);
        var hasPrevious = false;
        for (var i = 0; i < program.Length; i++)
        {
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

    public bool TryMatch(ReadOnlySpan<byte> input, int startIndex, out int matchedLength)
    {
        matchedLength = 0;
        if (!HasValue || (uint)startIndex > (uint)input.Length)
        {
            return false;
        }

        if (RequiresBeginning && startIndex != 0)
        {
            return false;
        }

        var state = 0;
        var lastAcceptLength = _acceptingStates[state] ? 0 : -1;
        var index = startIndex;

        while ((uint)index < (uint)input.Length)
        {
            var nextState = FindTransition(state, input[index]);
            if (nextState < 0)
            {
                break;
            }

            state = nextState;
            index++;
            if (_acceptingStates[state])
            {
                lastAcceptLength = index - startIndex;
            }
        }

        if (RequiresEnd)
        {
            if (index != input.Length || !_acceptingStates[state])
            {
                return false;
            }

            matchedLength = index - startIndex;
            return true;
        }

        if (lastAcceptLength < 0)
        {
            return false;
        }

        matchedLength = lastAcceptLength;
        return true;
    }

    private int FindTransition(int state, byte value)
    {
        var offset = _transitionOffsets[state];
        var count = _transitionCounts[state];
        for (var i = 0; i < count; i++)
        {
            var transitionIndex = offset + i;
            if (_transitionSets[transitionIndex].Contains(value))
            {
                return _transitionTargets[transitionIndex];
            }
        }

        return -1;
    }

    private static Utf8ByteSafeLazyDfaVerifierProgram BuildDfa(NfaGraph nfa, bool requiresBeginning, bool requiresEnd)
    {
        var stateMap = new Dictionary<NfaStateSet, int>(NfaStateSetComparer.Instance);
        var queue = new Queue<NfaStateSet>();
        var dfaStateSets = new List<NfaStateSet>();
        var accepting = new List<bool>();
        var transitionOffsets = new List<int>();
        var transitionCounts = new List<int>();
        var transitionSets = new List<Utf8AsciiByteSet>();
        var transitionTargets = new List<int>();

        var startSet = ComputeClosure([0], nfa);
        stateMap[startSet] = 0;
        dfaStateSets.Add(startSet);
        accepting.Add(startSet.Contains(nfa.AcceptState));
        queue.Enqueue(startSet);

        while (queue.Count > 0)
        {
            if (dfaStateSets.Count > MaxDfaStates || transitionSets.Count > MaxDfaTransitions)
            {
                s_lastFailureKind = Utf8ByteSafeLazyDfaCompileFailureKind.StateBudgetExceeded;
                return default;
            }

            var current = queue.Dequeue();
            var buckets = new Dictionary<TransitionKey, HashSet<int>>(TransitionKeyComparer.Instance);

            foreach (var stateId in current.States)
            {
                foreach (var transition in nfa.States[stateId].Transitions)
                {
                    if (!buckets.TryGetValue(transition.Key, out var targets))
                    {
                        targets = [];
                        buckets[transition.Key] = targets;
                    }

                    targets.Add(transition.TargetState);
                }
            }

            var orderedBuckets = buckets
                .Select(static kvp => (Key: kvp.Key, Targets: kvp.Value))
                .OrderBy(static bucket => bucket.Key.SortKey)
                .ToArray();

            transitionOffsets.Add(transitionSets.Count);
            transitionCounts.Add(orderedBuckets.Length);

            for (var i = 0; i < orderedBuckets.Length; i++)
            {
                var closure = ComputeClosure([.. orderedBuckets[i].Targets], nfa);
                if (!stateMap.TryGetValue(closure, out var targetIndex))
                {
                    targetIndex = dfaStateSets.Count;
                    if (targetIndex > MaxDfaStates)
                    {
                        s_lastFailureKind = Utf8ByteSafeLazyDfaCompileFailureKind.StateBudgetExceeded;
                        return default;
                    }

                    stateMap[closure] = targetIndex;
                    dfaStateSets.Add(closure);
                    accepting.Add(closure.Contains(nfa.AcceptState));
                    queue.Enqueue(closure);
                }

                transitionSets.Add(orderedBuckets[i].Key.Set);
                transitionTargets.Add(targetIndex);
                if (transitionSets.Count > MaxDfaTransitions)
                {
                    s_lastFailureKind = Utf8ByteSafeLazyDfaCompileFailureKind.StateBudgetExceeded;
                    return default;
                }
            }
        }

        return new Utf8ByteSafeLazyDfaVerifierProgram(
            [.. transitionOffsets],
            [.. transitionCounts],
            [.. transitionSets],
            [.. transitionTargets],
            [.. accepting],
            requiresBeginning,
            requiresEnd);
    }

    private static NfaStateSet ComputeClosure(int[] startStates, NfaGraph nfa)
    {
        var visited = new HashSet<int>();
        var stack = new Stack<int>(startStates);
        while (stack.Count > 0)
        {
            var state = stack.Pop();
            if (!visited.Add(state))
            {
                continue;
            }

            foreach (var target in nfa.States[state].EpsilonTransitions)
            {
                stack.Push(target);
            }
        }

        var ordered = visited.OrderBy(static value => value).ToArray();
        return new NfaStateSet(ordered);
    }

    private static bool TryCompileDeterministicSequence(
        IReadOnlyList<Utf8ByteSafeLinearVerifierStep> steps,
        out Utf8ByteSafeLazyDfaVerifierProgram program)
    {
        if (TryCompileDisjointLoopSequence(steps, out program))
        {
            return true;
        }

        var requiresBeginning = false;
        var requiresEnd = false;
        var transitionOffsets = new List<int>();
        var transitionCounts = new List<int>();
        var transitionSets = new List<Utf8AsciiByteSet>();
        var transitionTargets = new List<int>();
        var accepting = new List<bool>();
        var stateIndex = 0;

        accepting.Add(false);

        for (var stepIndex = 0; stepIndex < steps.Count; stepIndex++)
        {
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
                        [.. transitionOffsets],
                        [.. transitionCounts],
                        [.. transitionSets],
                        [.. transitionTargets],
                        [.. accepting],
                        requiresBeginning,
                        requiresEnd);
                    return true;

                case Utf8ByteSafeLinearVerifierStepKind.MatchByte:
                    stateIndex = AppendDeterministicTransition(
                        step,
                        Utf8AsciiByteSet.ForByte(step.Value),
                        stateIndex,
                        accepting,
                        transitionOffsets,
                        transitionCounts,
                        transitionSets,
                        transitionTargets);
                    break;

                case Utf8ByteSafeLinearVerifierStepKind.MatchText:
                    foreach (var value in step.Text!)
                    {
                        stateIndex = AppendDeterministicTransition(
                            step,
                            Utf8AsciiByteSet.ForByte(value),
                            stateIndex,
                            accepting,
                            transitionOffsets,
                            transitionCounts,
                            transitionSets,
                            transitionTargets);
                    }
                    break;

                case Utf8ByteSafeLinearVerifierStepKind.MatchSet:
                    if (!Utf8AsciiByteSet.TryCreateFromRuntimeSet(step.Set!, out var matchSet))
                    {
                        s_lastFailureKind = Utf8ByteSafeLazyDfaCompileFailureKind.UnsupportedRuntimeSet;
                        program = default;
                        return false;
                    }

                    stateIndex = AppendDeterministicTransition(
                        step,
                        matchSet,
                        stateIndex,
                        accepting,
                        transitionOffsets,
                        transitionCounts,
                        transitionSets,
                            transitionTargets);
                    break;

                case Utf8ByteSafeLinearVerifierStepKind.MatchProjectedAsciiSet:
                    if (step.ProjectedAsciiCharClass is null ||
                        !Utf8AsciiByteSet.TryCreateFromAsciiCharClass(step.ProjectedAsciiCharClass, out var projectedMatchSet))
                    {
                        s_lastFailureKind = Utf8ByteSafeLazyDfaCompileFailureKind.UnsupportedProjectedAsciiSet;
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
                            Utf8AsciiByteSet.ForByte(step.Value),
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
                        foreach (var value in step.Text!)
                        {
                            stateIndex = AppendDeterministicTransition(
                                step,
                                Utf8AsciiByteSet.ForByte(value),
                                stateIndex,
                                accepting,
                                transitionOffsets,
                                transitionCounts,
                                transitionSets,
                                transitionTargets);
                        }
                    }
                    break;

                case Utf8ByteSafeLinearVerifierStepKind.LoopSet when step.Min == step.Max:
                    if (!Utf8AsciiByteSet.TryCreateFromRuntimeSet(step.Set!, out var loopSet))
                    {
                        s_lastFailureKind = Utf8ByteSafeLazyDfaCompileFailureKind.UnsupportedRuntimeSet;
                        program = default;
                        return false;
                    }

                    for (var i = 0; i < step.Min; i++)
                    {
                        stateIndex = AppendDeterministicTransition(
                            step,
                            loopSet,
                            stateIndex,
                            accepting,
                            transitionOffsets,
                            transitionCounts,
                            transitionSets,
                            transitionTargets);
                    }
                    break;

                case Utf8ByteSafeLinearVerifierStepKind.LoopProjectedAsciiSet when step.Min == step.Max:
                    if (step.ProjectedAsciiCharClass is null ||
                        !Utf8AsciiByteSet.TryCreateFromAsciiCharClass(step.ProjectedAsciiCharClass, out var projectedLoopSet))
                    {
                        s_lastFailureKind = Utf8ByteSafeLazyDfaCompileFailureKind.UnsupportedProjectedAsciiSet;
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

                case Utf8ByteSafeLinearVerifierStepKind.LoopProjectedAsciiSet when
                    step.Min == 0 &&
                    IsOpenEndedLoop(step.Max) &&
                    step.ProjectedAsciiCharClass is not null &&
                    Utf8AsciiByteSet.TryCreateFromAsciiCharClass(step.ProjectedAsciiCharClass, out var leadingProjectedLoopSet) &&
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
                    s_lastFailureKind = Utf8ByteSafeLazyDfaCompileFailureKind.UnsupportedDeterministicStep;
                    program = default;
                    return false;
            }
        }

        program = default;
        return false;
    }

    private static bool TryCompileDisjointLoopSequence(
        IReadOnlyList<Utf8ByteSafeLinearVerifierStep> steps,
        out Utf8ByteSafeLazyDfaVerifierProgram program)
    {
        program = default;
        if (steps.Count < 3 || steps[^1].Kind != Utf8ByteSafeLinearVerifierStepKind.Accept)
        {
            return false;
        }

        var requiresBeginning = false;
        var requiresEnd = false;
        var start = 0;
        var endExclusive = steps.Count - 1;

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

        var payloadSteps = steps.Skip(start).Take(endExclusive - start).ToArray();
        if (payloadSteps.Length == 0)
        {
            return false;
        }

        var repeatedSegments = false;
        Utf8ByteSafeLinearVerifierStep[] coreSteps;
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

        var loopSets = new Utf8AsciiByteSet[coreSteps.Length];
        for (var i = 0; i < coreSteps.Length; i++)
        {
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

        var transitionOffsets = new List<int> { 0 };
        var transitionCounts = new List<int> { 0 };
        var transitionSets = new List<Utf8AsciiByteSet>();
        var transitionTargets = new List<int>();
        var accepting = new List<bool> { false };

        var previousStageState = 0;
        for (var i = 0; i < coreSteps.Length; i++)
        {
            var stageState = accepting.Count;
            transitionOffsets.Add(0);
            transitionCounts.Add(0);
            accepting.Add(false);

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
                if (!accepting[stateIndex])
                {
                    continue;
                }

                AddDeterministicTransition(stateIndex, firstSet, 1, transitionOffsets, transitionCounts, transitionSets, transitionTargets);
            }
        }

        program = new Utf8ByteSafeLazyDfaVerifierProgram(
            [.. transitionOffsets],
            [.. transitionCounts],
            [.. transitionSets],
            [.. transitionTargets],
            [.. accepting],
            requiresBeginning,
            requiresEnd);
        return true;
    }

    private static bool TryGetDisjointLoopSet(Utf8ByteSafeLinearVerifierStep step, out Utf8AsciiByteSet set)
    {
        switch (step.Kind)
        {
            case Utf8ByteSafeLinearVerifierStepKind.LoopByte:
                set = Utf8AsciiByteSet.ForByte(step.Value);
                return true;

            case Utf8ByteSafeLinearVerifierStepKind.LoopSet:
                return Utf8AsciiByteSet.TryCreateFromRuntimeSet(step.Set!, out set);

            case Utf8ByteSafeLinearVerifierStepKind.LoopProjectedAsciiSet:
                if (step.ProjectedAsciiCharClass is not null &&
                    Utf8AsciiByteSet.TryCreateFromAsciiCharClass(step.ProjectedAsciiCharClass, out set))
                {
                    return true;
                }

                break;
        }

        set = default;
        return false;
    }

    private static bool TryGetFirstDeterministicTransitionSet(
        IReadOnlyList<Utf8ByteSafeLinearVerifierStep> steps,
        int startIndex,
        out Utf8AsciiByteSet set)
    {
        for (var i = startIndex; i < steps.Count; i++)
        {
            var step = steps[i];
            switch (step.Kind)
            {
                case Utf8ByteSafeLinearVerifierStepKind.RequireBeginning:
                case Utf8ByteSafeLinearVerifierStepKind.RequireEnd:
                case Utf8ByteSafeLinearVerifierStepKind.Accept:
                    continue;

                case Utf8ByteSafeLinearVerifierStepKind.MatchByte:
                    set = Utf8AsciiByteSet.ForByte(step.Value);
                    return true;

                case Utf8ByteSafeLinearVerifierStepKind.MatchText when step.Text is { Length: > 0 } text:
                    set = Utf8AsciiByteSet.ForByte(text[0]);
                    return true;

                case Utf8ByteSafeLinearVerifierStepKind.MatchSet when step.Set is not null:
                    return Utf8AsciiByteSet.TryCreateFromRuntimeSet(step.Set, out set);

                case Utf8ByteSafeLinearVerifierStepKind.MatchProjectedAsciiSet when step.ProjectedAsciiCharClass is not null:
                    return Utf8AsciiByteSet.TryCreateFromAsciiCharClass(step.ProjectedAsciiCharClass, out set);

                case Utf8ByteSafeLinearVerifierStepKind.LoopByte:
                    set = Utf8AsciiByteSet.ForByte(step.Value);
                    return true;

                case Utf8ByteSafeLinearVerifierStepKind.LoopSet when step.Set is not null:
                    return Utf8AsciiByteSet.TryCreateFromRuntimeSet(step.Set, out set);

                case Utf8ByteSafeLinearVerifierStepKind.LoopProjectedAsciiSet when step.ProjectedAsciiCharClass is not null:
                    return Utf8AsciiByteSet.TryCreateFromAsciiCharClass(step.ProjectedAsciiCharClass, out set);

                case Utf8ByteSafeLinearVerifierStepKind.LoopProgram when
                    step.Min == 0 &&
                    step.Max == 1 &&
                    step.Program is { Length: > 0 } optionalProgram:
                    return TryGetFirstDeterministicTransitionSet(optionalProgram, 0, out set);

                default:
                    set = default;
                    return false;
            }
        }

        set = default;
        return false;
    }

    private static bool TryAppendDeterministicOptionalProgram(
        IReadOnlyList<Utf8ByteSafeLinearVerifierStep> steps,
        ref int stateIndex,
        List<bool> accepting,
        List<int> transitionOffsets,
        List<int> transitionCounts,
        List<Utf8AsciiByteSet> transitionSets,
        List<int> transitionTargets)
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

    private static bool TryAppendDeterministicSubProgram(
        IReadOnlyList<Utf8ByteSafeLinearVerifierStep> steps,
        ref int stateIndex,
        List<bool> accepting,
        List<int> transitionOffsets,
        List<int> transitionCounts,
        List<Utf8AsciiByteSet> transitionSets,
        List<int> transitionTargets)
    {
        for (var i = 0; i < steps.Count; i++)
        {
            var step = steps[i];
            switch (step.Kind)
            {
                case Utf8ByteSafeLinearVerifierStepKind.MatchByte:
                    stateIndex = AppendDeterministicTransition(
                        step,
                        Utf8AsciiByteSet.ForByte(step.Value),
                        stateIndex,
                        accepting,
                        transitionOffsets,
                        transitionCounts,
                        transitionSets,
                        transitionTargets);
                    break;

                case Utf8ByteSafeLinearVerifierStepKind.MatchText:
                    foreach (var value in step.Text!)
                    {
                        stateIndex = AppendDeterministicTransition(
                            step,
                            Utf8AsciiByteSet.ForByte(value),
                            stateIndex,
                            accepting,
                            transitionOffsets,
                            transitionCounts,
                            transitionSets,
                            transitionTargets);
                    }
                    break;

                case Utf8ByteSafeLinearVerifierStepKind.MatchProjectedAsciiSet:
                    if (step.ProjectedAsciiCharClass is null ||
                        !Utf8AsciiByteSet.TryCreateFromAsciiCharClass(step.ProjectedAsciiCharClass, out var projectedMatchSet))
                    {
                        s_lastFailureKind = Utf8ByteSafeLazyDfaCompileFailureKind.UnsupportedProjectedAsciiSet;
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

                case Utf8ByteSafeLinearVerifierStepKind.LoopProjectedAsciiSet:
                    if (step.ProjectedAsciiCharClass is null ||
                        !Utf8AsciiByteSet.TryCreateFromAsciiCharClass(step.ProjectedAsciiCharClass, out var projectedLoopSet))
                    {
                        s_lastFailureKind = Utf8ByteSafeLazyDfaCompileFailureKind.UnsupportedProjectedAsciiSet;
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
                            [step],
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
                    s_lastFailureKind = Utf8ByteSafeLazyDfaCompileFailureKind.UnsupportedDeterministicStep;
                    return false;
            }
        }

        return true;
    }

    private static bool IsSupportedDeterministicOptionalProgram(Utf8ByteSafeLinearVerifierStep step)
    {
        if (step.Program is not { Length: > 0 } program ||
            step.Min != 0 ||
            step.Max != 1)
        {
            return false;
        }

        for (var i = 0; i < program.Length; i++)
        {
            if (!IsSupportedDeterministicSubProgramStep(program[i]))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsSupportedDeterministicSubProgramStep(Utf8ByteSafeLinearVerifierStep step)
    {
        switch (step.Kind)
        {
            case Utf8ByteSafeLinearVerifierStepKind.MatchByte:
            case Utf8ByteSafeLinearVerifierStepKind.MatchText:
                return true;

            case Utf8ByteSafeLinearVerifierStepKind.MatchProjectedAsciiSet:
                return step.ProjectedAsciiCharClass is not null;

            case Utf8ByteSafeLinearVerifierStepKind.LoopProjectedAsciiSet:
                return step.ProjectedAsciiCharClass is not null &&
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

    private static bool AreDisjointLoopSteps(IReadOnlyList<Utf8ByteSafeLinearVerifierStep> steps)
    {
        var previousSet = default(Utf8AsciiByteSet);
        var hasPrevious = false;
        for (var i = 0; i < steps.Count; i++)
        {
            if (!TryGetDisjointLoopSet(steps[i], out var currentSet))
            {
                return false;
            }

            if (!IsOpenEndedLoop(steps[i].Max))
            {
                return false;
            }

            if (steps[i].Min != 1 && !(i == steps.Count - 1 && steps[i].Min == 0))
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

    private static bool TryAppendRepeatedDisjointLoopSequence(
        IReadOnlyList<Utf8ByteSafeLinearVerifierStep> steps,
        ref int stateIndex,
        List<bool> accepting,
        List<int> transitionOffsets,
        List<int> transitionCounts,
        List<Utf8AsciiByteSet> transitionSets,
        List<int> transitionTargets)
    {
        if (steps.Count == 0)
        {
            return false;
        }

        var loopSets = new Utf8AsciiByteSet[steps.Count];
        for (var i = 0; i < steps.Count; i++)
        {
            if (!TryGetDisjointLoopSet(steps[i], out loopSets[i]))
            {
                return false;
            }

            if (!IsOpenEndedLoop(steps[i].Max))
            {
                return false;
            }

            if (steps[i].Min != 1 && !(i == steps.Count - 1 && steps[i].Min == 0))
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
            var stageState = accepting.Count;
            transitionOffsets.Add(0);
            transitionCounts.Add(0);
            accepting.Add(false);

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
            if (accepting[i])
            {
                AddDeterministicTransition(i, loopSets[0], firstStageState, transitionOffsets, transitionCounts, transitionSets, transitionTargets);
            }
        }

        stateIndex = previousStageState;
        return true;
    }

    private static bool IsOpenEndedLoop(int max) => max < 0 || max == int.MaxValue;

    private static int AppendDeterministicTransition(
        Utf8ByteSafeLinearVerifierStep step,
        Utf8AsciiByteSet transitionSet,
        int stateIndex,
        List<bool> accepting,
        List<int> transitionOffsets,
        List<int> transitionCounts,
        List<Utf8AsciiByteSet> transitionSets,
        List<int> transitionTargets)
    {
        while (transitionOffsets.Count <= stateIndex)
        {
            transitionOffsets.Add(0);
            transitionCounts.Add(0);
        }

        var nextState = accepting.Count;
        accepting.Add(false);
        if (transitionCounts[stateIndex] == 0)
        {
            transitionOffsets[stateIndex] = transitionSets.Count;
            transitionCounts[stateIndex] = 1;
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

    private static void AddDeterministicTransition(
        int stateIndex,
        Utf8AsciiByteSet transitionSet,
        int targetState,
        List<int> transitionOffsets,
        List<int> transitionCounts,
        List<Utf8AsciiByteSet> transitionSets,
        List<int> transitionTargets)
    {
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
            if (!transitionSets[offset + i].IsDisjoint(transitionSet))
            {
                throw new InvalidOperationException("Deterministic lazy DFA compiler received overlapping transitions.");
            }
        }

        transitionSets.Add(transitionSet);
        transitionTargets.Add(targetState);
        transitionCounts[stateIndex] = count + 1;
    }

    private static bool TryCompileNfa(
        IReadOnlyList<Utf8ByteSafeLinearVerifierStep> steps,
        out NfaGraph graph,
        out bool requiresBeginning,
        out bool requiresEnd)
    {
        try
        {
            requiresBeginning = false;
            requiresEnd = false;
            var states = new List<NfaState>
            {
                new(),
            };

            var current = 0;
            for (var i = 0; i < steps.Count; i++)
            {
                var step = steps[i];
                if (!TryAppendNfaStep(states, ref current, step, isTopLevel: true, ref requiresBeginning, ref requiresEnd, isLastBeforeAccept: i == steps.Count - 2 && steps[^1].Kind == Utf8ByteSafeLinearVerifierStepKind.Accept))
                {
                    graph = default;
                    if (s_lastFailureKind == Utf8ByteSafeLazyDfaCompileFailureKind.None)
                    {
                        s_lastFailureKind = Utf8ByteSafeLazyDfaCompileFailureKind.UnsupportedStep;
                    }
                    return false;
                }

                if (step.Kind == Utf8ByteSafeLinearVerifierStepKind.Accept)
                {
                    graph = new NfaGraph([.. states], current);
                    return true;
                }
            }

            graph = default;
            return false;
        }
        catch (NfaBudgetExceededException)
        {
            s_lastFailureKind = Utf8ByteSafeLazyDfaCompileFailureKind.StateBudgetExceeded;
            graph = default;
            requiresBeginning = false;
            requiresEnd = false;
            return false;
        }
    }

    private static bool TryAppendNfaStep(
        List<NfaState> states,
        ref int current,
        Utf8ByteSafeLinearVerifierStep step,
        bool isTopLevel,
        ref bool requiresBeginning,
        ref bool requiresEnd,
        bool isLastBeforeAccept)
    {
        switch (step.Kind)
        {
            case Utf8ByteSafeLinearVerifierStepKind.RequireBeginning:
                if (!isTopLevel || current != 0)
                {
                    s_lastFailureKind = Utf8ByteSafeLazyDfaCompileFailureKind.UnsupportedAnchoring;
                    return false;
                }

                requiresBeginning = true;
                return true;

            case Utf8ByteSafeLinearVerifierStepKind.RequireEnd:
                if (!isTopLevel || !isLastBeforeAccept)
                {
                    s_lastFailureKind = Utf8ByteSafeLazyDfaCompileFailureKind.UnsupportedAnchoring;
                    return false;
                }

                requiresEnd = true;
                return true;

            case Utf8ByteSafeLinearVerifierStepKind.Accept:
                return isTopLevel;

            case Utf8ByteSafeLinearVerifierStepKind.MatchByte:
                current = AppendSet(states, current, Utf8AsciiByteSet.ForByte(step.Value));
                return true;

            case Utf8ByteSafeLinearVerifierStepKind.MatchText:
                foreach (var value in step.Text!)
                {
                    current = AppendSet(states, current, Utf8AsciiByteSet.ForByte(value));
                }
                return true;

            case Utf8ByteSafeLinearVerifierStepKind.MatchSet:
                if (!Utf8AsciiByteSet.TryCreateFromRuntimeSet(step.Set!, out var matchSet))
                {
                    s_lastFailureKind = Utf8ByteSafeLazyDfaCompileFailureKind.UnsupportedRuntimeSet;
                    return false;
                }

                current = AppendSet(states, current, matchSet);
                return true;

            case Utf8ByteSafeLinearVerifierStepKind.MatchProjectedAsciiSet:
                if (step.ProjectedAsciiCharClass is null ||
                    !Utf8AsciiByteSet.TryCreateFromAsciiCharClass(step.ProjectedAsciiCharClass, out var projectedSet))
                {
                    s_lastFailureKind = Utf8ByteSafeLazyDfaCompileFailureKind.UnsupportedProjectedAsciiSet;
                    return false;
                }

                current = AppendSet(states, current, projectedSet);
                return true;

            case Utf8ByteSafeLinearVerifierStepKind.LoopByte:
                current = AppendLoop(states, current, Utf8AsciiByteSet.ForByte(step.Value), 1, step.Min, step.Max);
                return true;

            case Utf8ByteSafeLinearVerifierStepKind.LoopText:
                current = AppendLoop(states, current, step.Text!.Select(static value => Utf8AsciiByteSet.ForByte(value)).ToArray(), step.Min, step.Max);
                return true;

            case Utf8ByteSafeLinearVerifierStepKind.LoopSet:
                if (!Utf8AsciiByteSet.TryCreateFromRuntimeSet(step.Set!, out var loopSet))
                {
                    s_lastFailureKind = Utf8ByteSafeLazyDfaCompileFailureKind.UnsupportedRuntimeSet;
                    return false;
                }

                current = AppendLoop(states, current, loopSet, 1, step.Min, step.Max);
                return true;

            case Utf8ByteSafeLinearVerifierStepKind.LoopProjectedAsciiSet:
                if (step.ProjectedAsciiCharClass is null ||
                    !Utf8AsciiByteSet.TryCreateFromAsciiCharClass(step.ProjectedAsciiCharClass, out var projectedLoopSet))
                {
                    s_lastFailureKind = Utf8ByteSafeLazyDfaCompileFailureKind.UnsupportedProjectedAsciiSet;
                    return false;
                }

                current = AppendLoop(states, current, projectedLoopSet, 1, step.Min, step.Max);
                return true;

            case Utf8ByteSafeLinearVerifierStepKind.MatchAnyText:
                current = AppendAlternatives(states, current, step.Alternatives!, optional: false);
                return true;

            case Utf8ByteSafeLinearVerifierStepKind.MatchAnyTextOptional:
                current = AppendAlternatives(states, current, step.Alternatives!, optional: true);
                return true;

            case Utf8ByteSafeLinearVerifierStepKind.LoopAnyText:
                current = AppendAlternativeLoop(states, current, step.Alternatives!, step.Min, step.Max);
                return true;

            case Utf8ByteSafeLinearVerifierStepKind.LoopProgram:
                current = AppendProgramLoop(states, current, step.Program!, step.Min, step.Max);
                return true;

            default:
                s_lastFailureKind = Utf8ByteSafeLazyDfaCompileFailureKind.UnsupportedStep;
                return false;
        }
    }

    private static int AppendLoop(List<NfaState> states, int current, Utf8AsciiByteSet set, int width, int min, int max)
    {
        var units = new Utf8AsciiByteSet[width];
        for (var i = 0; i < width; i++)
        {
            units[i] = set;
        }

        return AppendLoop(states, current, units, min, max);
    }

    private static int AppendLoop(List<NfaState> states, int current, Utf8AsciiByteSet[] units, int min, int max)
    {
        for (var i = 0; i < min; i++)
        {
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
            states[current].EpsilonTransitions.Add(exitState);
            current = AppendSequence(states, current, units);
        }

        states[current].EpsilonTransitions.Add(exitState);
        return exitState;
    }

    private static int AppendSequence(List<NfaState> states, int current, Utf8AsciiByteSet[] units)
    {
        for (var i = 0; i < units.Length; i++)
        {
            current = AppendSet(states, current, units[i]);
        }

        return current;
    }

    private static int AppendAlternatives(List<NfaState> states, int current, byte[][] alternatives, bool optional)
    {
        var exitState = AddState(states);
        if (optional)
        {
            states[current].EpsilonTransitions.Add(exitState);
        }

        for (var i = 0; i < alternatives.Length; i++)
        {
            var branchState = AddState(states);
            states[current].EpsilonTransitions.Add(branchState);
            var branchCurrent = branchState;
            foreach (var value in alternatives[i])
            {
                branchCurrent = AppendSet(states, branchCurrent, Utf8AsciiByteSet.ForByte(value));
            }

            states[branchCurrent].EpsilonTransitions.Add(exitState);
        }

        return exitState;
    }

    private static int AppendAlternativeLoop(List<NfaState> states, int current, byte[][] alternatives, int min, int max)
    {
        for (var i = 0; i < min; i++)
        {
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
            states[current].EpsilonTransitions.Add(exitState);
            current = AppendAlternatives(states, current, alternatives, optional: false);
        }

        states[current].EpsilonTransitions.Add(exitState);
        return exitState;
    }

    private static int AppendProgramLoop(List<NfaState> states, int current, Utf8ByteSafeLinearVerifierStep[] program, int min, int max)
    {
        for (var i = 0; i < min; i++)
        {
            current = AppendProgram(states, current, program);
        }

        var exitState = AddState(states);
        if (max < 0)
        {
            states[current].EpsilonTransitions.Add(exitState);
            var loopStart = current;
            current = AppendProgram(states, current, program);
            states[current].EpsilonTransitions.Add(loopStart);
            states[current].EpsilonTransitions.Add(exitState);
            return exitState;
        }

        for (var i = min; i < max; i++)
        {
            states[current].EpsilonTransitions.Add(exitState);
            current = AppendProgram(states, current, program);
        }

        states[current].EpsilonTransitions.Add(exitState);
        return exitState;
    }

    private static int AppendProgram(List<NfaState> states, int current, Utf8ByteSafeLinearVerifierStep[] program)
    {
        var requiresBeginning = false;
        var requiresEnd = false;
        for (var i = 0; i < program.Length; i++)
        {
            if (!TryAppendNfaStep(states, ref current, program[i], isTopLevel: false, ref requiresBeginning, ref requiresEnd, isLastBeforeAccept: false))
            {
                if (s_lastFailureKind == Utf8ByteSafeLazyDfaCompileFailureKind.None)
                {
                    s_lastFailureKind = Utf8ByteSafeLazyDfaCompileFailureKind.UnsupportedSubProgram;
                }
                throw new InvalidOperationException($"Unsupported sub-program step {program[i].Kind} in lazy DFA compiler.");
            }
        }

        return current;
    }

    private static int AppendSet(List<NfaState> states, int current, Utf8AsciiByteSet set)
    {
        var next = AddState(states);
        states[current].Transitions.Add(new NfaTransition(new TransitionKey(set), next));
        return next;
    }

    private static int AddState(List<NfaState> states)
    {
        if (states.Count >= MaxNfaStates)
        {
            throw new NfaBudgetExceededException();
        }

        var index = states.Count;
        states.Add(new NfaState());
        return index;
    }

    private sealed class NfaState
    {
        public List<int> EpsilonTransitions { get; } = [];

        public List<NfaTransition> Transitions { get; } = [];
    }

    private readonly record struct NfaTransition(TransitionKey Key, int TargetState);

    private readonly record struct TransitionKey(Utf8AsciiByteSet Set)
    {
        public ulong SortKey => Set.LowMask ^ (Set.HighMask * 31UL);
    }

    private readonly record struct NfaStateSet(int[] States)
    {
        public bool Contains(int state)
        {
            for (var i = 0; i < States.Length; i++)
            {
                if (States[i] == state)
                {
                    return true;
                }
            }

            return false;
        }
    }

    private sealed class NfaStateSetComparer : IEqualityComparer<NfaStateSet>
    {
        public static NfaStateSetComparer Instance { get; } = new();

        public bool Equals(NfaStateSet x, NfaStateSet y)
        {
            return x.States.AsSpan().SequenceEqual(y.States);
        }

        public int GetHashCode(NfaStateSet obj)
        {
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

    private readonly record struct NfaGraph(NfaState[] States, int AcceptState);

    private sealed class NfaBudgetExceededException : Exception;
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
    UnsupportedRuntimeSet = 8,
    UnsupportedProjectedAsciiSet = 9,
    UnsupportedAnchoring = 10,
    StateBudgetExceeded = 11,
}

internal readonly struct Utf8AsciiByteSet
{
    public Utf8AsciiByteSet(ulong lowMask, ulong highMask)
    {
        LowMask = lowMask;
        HighMask = highMask;
    }

    public ulong LowMask { get; }

    public ulong HighMask { get; }

    public bool Contains(byte value)
    {
        if (value >= 128)
        {
            return false;
        }

        return value < 64
            ? (LowMask & (1UL << value)) != 0
            : (HighMask & (1UL << (value - 64))) != 0;
    }

    public bool IsDisjoint(Utf8AsciiByteSet other) =>
        (LowMask & other.LowMask) == 0 &&
        (HighMask & other.HighMask) == 0;

    public static Utf8AsciiByteSet ForByte(byte value)
    {
        return value < 64
            ? new Utf8AsciiByteSet(1UL << value, 0)
            : new Utf8AsciiByteSet(0, 1UL << (value - 64));
    }

    public static bool TryCreateFromRuntimeSet(string runtimeSet, out Utf8AsciiByteSet set)
    {
        if (!RuntimeFrontEnd.RegexCharClass.IsAscii(runtimeSet))
        {
            set = default;
            return false;
        }

        ulong lowMask = 0;
        ulong highMask = 0;
        for (var i = 0; i < 128; i++)
        {
            if (!RuntimeFrontEnd.RegexCharClass.CharInClass((char)i, runtimeSet))
            {
                continue;
            }

            if (i < 64)
            {
                lowMask |= 1UL << i;
            }
            else
            {
                highMask |= 1UL << (i - 64);
            }
        }

        if ((lowMask | highMask) == 0)
        {
            set = default;
            return false;
        }

        set = new Utf8AsciiByteSet(lowMask, highMask);
        return true;
    }

    public static bool TryCreateFromAsciiCharClass(AsciiCharClass asciiCharClass, out Utf8AsciiByteSet set)
    {
        ulong lowMask = 0;
        ulong highMask = 0;
        for (var i = 0; i < 128; i++)
        {
            if (!asciiCharClass.Contains((byte)i))
            {
                continue;
            }

            if (i < 64)
            {
                lowMask |= 1UL << i;
            }
            else
            {
                highMask |= 1UL << (i - 64);
            }
        }

        if ((lowMask | highMask) == 0)
        {
            set = default;
            return false;
        }

        set = new Utf8AsciiByteSet(lowMask, highMask);
        return true;
    }
}
