using RuntimeFrontEnd = Lokad.Utf8Regex.Internal.FrontEnd.Runtime;

namespace Lokad.Utf8Regex.Internal.Execution;

internal enum Utf8ByteSafeLinearVerifierStepKind : byte
{
    MatchByte = 0,
    MatchText = 1,
    MatchSet = 2,
    MatchProjectedAsciiSet = 3,
    LoopByte = 4,
    LoopText = 5,
    LoopSet = 6,
    LoopProjectedAsciiSet = 7,
    RequireBeginning = 8,
    RequireEnd = 9,
    RequireBoundary = 10,
    RequireNonBoundary = 11,
    MatchAnyText = 12,
    MatchAnyTextOptional = 13,
    LoopAnyText = 14,
    LoopProgram = 15,
    Accept = 16,
}

internal enum Utf8ByteSafeLinearCompileFailureKind : byte
{
    None = 0,
    EmptyTree = 1,
    UnsupportedNode = 2,
    UnsupportedLoop = 3,
    UnsupportedAlternation = 4,
    UnsupportedFixedText = 5,
    UnsupportedFollowingPrefix = 6,
    UnsupportedFollowingMask = 7,
    NonDisjointVariableLoop = 8,
    NestedSubProgram = 9,
}

internal readonly struct Utf8ByteSafeLinearVerifierStep
{
    private Utf8ByteSafeLinearVerifierStep(
        Utf8ByteSafeLinearVerifierStepKind kind,
        byte value = 0,
        byte[]? text = null,
        byte[][]? alternatives = null,
        Utf8ByteSafeLinearVerifierStep[]? program = null,
        AsciiCharClass? projectedAsciiCharClass = null,
        string? set = null,
        int min = 0,
        int max = 0,
        RegexOptions options = RegexOptions.None)
    {
        Kind = kind;
        Value = value;
        Text = text;
        Alternatives = alternatives;
        Program = program;
        ProjectedAsciiCharClass = projectedAsciiCharClass;
        Set = set;
        Min = min;
        Max = max;
        Options = options;
    }

    public Utf8ByteSafeLinearVerifierStepKind Kind { get; }

    public byte Value { get; }

    public byte[]? Text { get; }

    public byte[][]? Alternatives { get; }

    public Utf8ByteSafeLinearVerifierStep[]? Program { get; }

    public AsciiCharClass? ProjectedAsciiCharClass { get; }

    public string? Set { get; }

    public int Min { get; }

    public int Max { get; }

    public RegexOptions Options { get; }

    public static Utf8ByteSafeLinearVerifierStep MatchByte(byte value) =>
        new(Utf8ByteSafeLinearVerifierStepKind.MatchByte, value: value);

    public static Utf8ByteSafeLinearVerifierStep MatchText(byte[] text) =>
        new(Utf8ByteSafeLinearVerifierStepKind.MatchText, text: text);

    public static Utf8ByteSafeLinearVerifierStep MatchSet(string set) =>
        new(Utf8ByteSafeLinearVerifierStepKind.MatchSet, set: set);

    public static Utf8ByteSafeLinearVerifierStep MatchProjectedAsciiSet(string set, AsciiCharClass asciiCharClass) =>
        new(Utf8ByteSafeLinearVerifierStepKind.MatchProjectedAsciiSet, set: set, projectedAsciiCharClass: asciiCharClass);

    public static Utf8ByteSafeLinearVerifierStep LoopByte(byte value, int min, int max) =>
        new(Utf8ByteSafeLinearVerifierStepKind.LoopByte, value: value, min: min, max: max);

    public static Utf8ByteSafeLinearVerifierStep LoopText(byte[] text, int min, int max) =>
        new(Utf8ByteSafeLinearVerifierStepKind.LoopText, text: text, min: min, max: max);

    public static Utf8ByteSafeLinearVerifierStep LoopSet(string set, int min, int max) =>
        new(Utf8ByteSafeLinearVerifierStepKind.LoopSet, set: set, min: min, max: max);

    public static Utf8ByteSafeLinearVerifierStep LoopProjectedAsciiSet(string set, AsciiCharClass asciiCharClass, int min, int max) =>
        new(Utf8ByteSafeLinearVerifierStepKind.LoopProjectedAsciiSet, set: set, projectedAsciiCharClass: asciiCharClass, min: min, max: max);

    public static Utf8ByteSafeLinearVerifierStep RequireBeginning(RegexOptions options) =>
        new(Utf8ByteSafeLinearVerifierStepKind.RequireBeginning, options: options);

    public static Utf8ByteSafeLinearVerifierStep RequireEnd(RegexOptions options) =>
        new(Utf8ByteSafeLinearVerifierStepKind.RequireEnd, options: options);

    public static Utf8ByteSafeLinearVerifierStep RequireBoundary() =>
        new(Utf8ByteSafeLinearVerifierStepKind.RequireBoundary);

    public static Utf8ByteSafeLinearVerifierStep RequireNonBoundary() =>
        new(Utf8ByteSafeLinearVerifierStepKind.RequireNonBoundary);

    public static Utf8ByteSafeLinearVerifierStep MatchAnyText(byte[][] alternatives) =>
        new(Utf8ByteSafeLinearVerifierStepKind.MatchAnyText, alternatives: alternatives);

    public static Utf8ByteSafeLinearVerifierStep MatchAnyTextOptional(byte[][] alternatives) =>
        new(Utf8ByteSafeLinearVerifierStepKind.MatchAnyTextOptional, alternatives: alternatives);

    public static Utf8ByteSafeLinearVerifierStep LoopAnyText(byte[][] alternatives, int min, int max) =>
        new(Utf8ByteSafeLinearVerifierStepKind.LoopAnyText, alternatives: alternatives, min: min, max: max);

    public static Utf8ByteSafeLinearVerifierStep LoopProgram(Utf8ByteSafeLinearVerifierStep[] program, int min, int max) =>
        new(Utf8ByteSafeLinearVerifierStepKind.LoopProgram, program: program, min: min, max: max);

    public static Utf8ByteSafeLinearVerifierStep Accept() =>
        new(Utf8ByteSafeLinearVerifierStepKind.Accept);
}

internal readonly struct Utf8ByteSafeLinearVerifierProgram
{
    [ThreadStatic]
    private static Utf8ByteSafeLinearCompileFailureKind s_lastFailureKind;

    public Utf8ByteSafeLinearVerifierProgram(Utf8ByteSafeLinearVerifierStep[]? steps)
    {
        Steps = steps is { Length: > 0 } ? steps : [];
    }

    public Utf8ByteSafeLinearVerifierStep[] Steps { get; }

    public bool HasValue => Steps is { Length: > 0 };

    public static Utf8ByteSafeLinearVerifierProgram Create(Utf8ExecutionTree? tree)
    {
        s_lastFailureKind = Utf8ByteSafeLinearCompileFailureKind.None;
        if (tree?.Root is not { } root)
        {
            return default;
        }

        var steps = new List<Utf8ByteSafeLinearVerifierStep>();
        return TryAppendNode(Unwrap(root), steps)
            ? new Utf8ByteSafeLinearVerifierProgram([.. steps, Utf8ByteSafeLinearVerifierStep.Accept()])
            : default;
    }

    public static Utf8ByteSafeLinearCompileFailureKind GetCompileFailureKind(Utf8ExecutionTree? tree)
    {
        s_lastFailureKind = Utf8ByteSafeLinearCompileFailureKind.None;
        if (tree?.Root is not { } root)
        {
            return Utf8ByteSafeLinearCompileFailureKind.EmptyTree;
        }

        _ = TryAppendNode(Unwrap(root), []);
        return s_lastFailureKind;
    }

    public bool TryMatch(ReadOnlySpan<byte> input, int startIndex, out int matchedLength)
        => TryMatch(input, startIndex, out matchedLength, out _);

    public bool TryMatch(ReadOnlySpan<byte> input, int startIndex, out int matchedLength, out bool requiresCompatibilityFallback)
    {
        matchedLength = 0;
        requiresCompatibilityFallback = false;
        if (!HasValue || (uint)startIndex > (uint)input.Length)
        {
            return false;
        }

        var index = startIndex;
        foreach (var step in Steps)
        {
            if (!TryExecuteStep(step, input, ref index, startIndex, out matchedLength, out requiresCompatibilityFallback))
            {
                return false;
            }

            if (step.Kind == Utf8ByteSafeLinearVerifierStepKind.Accept)
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryAppendNode(Utf8ExecutionNode node, List<Utf8ByteSafeLinearVerifierStep> steps)
    {
        switch (node.Kind)
        {
            case Utf8ExecutionNodeKind.Empty:
                return true;

            case Utf8ExecutionNodeKind.Capture:
            case Utf8ExecutionNodeKind.Group:
                return node.Children.Count == 1 && TryAppendNode(Unwrap(node.Children[0]), steps);

            case Utf8ExecutionNodeKind.Concatenate:
                for (var i = 0; i < node.Children.Count; i++)
                {
                    var child = Unwrap(node.Children[i]);
                    if (TryAppendTerminalLoop(node.Children, i, steps))
                    {
                        return true;
                    }

                    if (TryAppendSafeVariableLoop(node.Children, i, steps))
                    {
                        continue;
                    }

                    if (TryAppendTerminalAlternation(node.Children, i, steps))
                    {
                        return true;
                    }

                    if (TryAppendDeterministicOptionalAlternation(node.Children, i, steps))
                    {
                        continue;
                    }

                    if (TryAppendDeterministicAlternation(child, steps))
                    {
                        continue;
                    }

                    if (child.Kind is Utf8ExecutionNodeKind.Loop or Utf8ExecutionNodeKind.LazyLoop)
                    {
                        if (!TryAppendLoop(child, steps, allowVariableLength: false))
                        {
                            return false;
                        }

                        continue;
                    }

                    if (!TryAppendNode(Unwrap(child), steps))
                    {
                        return false;
                    }
                }

                return true;

            case Utf8ExecutionNodeKind.One when node.Ch <= 0x7F:
                steps.Add(Utf8ByteSafeLinearVerifierStep.MatchByte((byte)node.Ch));
                return true;

            case Utf8ExecutionNodeKind.Multi when TryGetAsciiBytes(node.Text, out var text):
                steps.Add(Utf8ByteSafeLinearVerifierStep.MatchText(text));
                return true;

            case Utf8ExecutionNodeKind.Set when node.Text is { Length: > 0 } set && TryCreateByteSafeSetStep(set, loop: false, min: 0, max: 0, out var setStep):
                steps.Add(setStep);
                return true;

            case Utf8ExecutionNodeKind.Bol:
            case Utf8ExecutionNodeKind.Beginning:
            case Utf8ExecutionNodeKind.Start:
                steps.Add(Utf8ByteSafeLinearVerifierStep.RequireBeginning(node.Options));
                return true;

            case Utf8ExecutionNodeKind.Eol:
            case Utf8ExecutionNodeKind.EndZ:
            case Utf8ExecutionNodeKind.End:
                steps.Add(Utf8ByteSafeLinearVerifierStep.RequireEnd(node.Options));
                return true;

            case Utf8ExecutionNodeKind.Boundary:
                steps.Add(Utf8ByteSafeLinearVerifierStep.RequireBoundary());
                return true;

            case Utf8ExecutionNodeKind.NonBoundary:
                steps.Add(Utf8ByteSafeLinearVerifierStep.RequireNonBoundary());
                return true;

            case Utf8ExecutionNodeKind.Loop:
            case Utf8ExecutionNodeKind.LazyLoop:
                return TryAppendLoop(node, steps, allowVariableLength: true);

            default:
                if (s_lastFailureKind == Utf8ByteSafeLinearCompileFailureKind.None)
                {
                    s_lastFailureKind = Utf8ByteSafeLinearCompileFailureKind.UnsupportedNode;
                }
                return false;
        }
    }

    private static bool TryAppendLoop(Utf8ExecutionNode node, List<Utf8ByteSafeLinearVerifierStep> steps, bool allowVariableLength)
    {
        if (node.Kind == Utf8ExecutionNodeKind.LazyLoop && node.Min != node.Max)
        {
            if (s_lastFailureKind == Utf8ByteSafeLinearCompileFailureKind.None)
            {
                s_lastFailureKind = Utf8ByteSafeLinearCompileFailureKind.UnsupportedLoop;
            }
            return false;
        }

        if (!allowVariableLength && node.Min != node.Max)
        {
            if (s_lastFailureKind == Utf8ByteSafeLinearCompileFailureKind.None)
            {
                s_lastFailureKind = Utf8ByteSafeLinearCompileFailureKind.UnsupportedLoop;
            }
            return false;
        }

        if (node.Children.Count == 1 &&
            TryExtractAlternationTexts(Unwrap(node.Children[0]), out var alternatives, out var optional) &&
            !optional &&
            AreAlternativesPrefixFree(alternatives) &&
            (node.Min == node.Max || allowVariableLength))
        {
            steps.Add(Utf8ByteSafeLinearVerifierStep.LoopAnyText(alternatives, node.Min, node.Max));
            return true;
        }

        if (node.Ch is > (char)0 and <= (char)0x7F)
        {
            steps.Add(Utf8ByteSafeLinearVerifierStep.LoopByte((byte)node.Ch, node.Min, node.Max));
            return true;
        }

        if (node.Text is { Length: > 0 } loopSet && TryCreateByteSafeSetStep(loopSet, loop: true, node.Min, node.Max, out var loopSetStep))
        {
            steps.Add(loopSetStep);
            return true;
        }

        if (TryGetAsciiBytes(node.Text, out var loopText))
        {
            steps.Add(Utf8ByteSafeLinearVerifierStep.LoopText(loopText, node.Min, node.Max));
            return true;
        }

        if (node.Children.Count != 1)
        {
            return false;
        }

        var child = Unwrap(node.Children[0]);
        switch (child.Kind)
        {
            case Utf8ExecutionNodeKind.One when child.Ch <= 0x7F:
                steps.Add(Utf8ByteSafeLinearVerifierStep.LoopByte((byte)child.Ch, node.Min, node.Max));
                return true;

            case Utf8ExecutionNodeKind.Multi when TryGetAsciiBytes(child.Text, out var text):
                steps.Add(Utf8ByteSafeLinearVerifierStep.LoopText(text, node.Min, node.Max));
                return true;

            case Utf8ExecutionNodeKind.Set when child.Text is { Length: > 0 } set && TryCreateByteSafeSetStep(set, loop: true, node.Min, node.Max, out var childLoopSetStep):
                steps.Add(childLoopSetStep);
                return true;

            case Utf8ExecutionNodeKind.Concatenate:
            case Utf8ExecutionNodeKind.Capture:
            case Utf8ExecutionNodeKind.Group:
            case Utf8ExecutionNodeKind.Loop:
            case Utf8ExecutionNodeKind.LazyLoop:
                if (TryCompileDeterministicSubProgram(child, out var programSteps, out var minConsumedLength))
                {
                    if (minConsumedLength == 0)
                    {
                        if (s_lastFailureKind == Utf8ByteSafeLinearCompileFailureKind.None)
                        {
                            s_lastFailureKind = Utf8ByteSafeLinearCompileFailureKind.NestedSubProgram;
                        }
                        return false;
                    }

                    if (TryCombineLoopSubProgram(programSteps, node.Min, node.Max, out var combinedLoopStep))
                    {
                        steps.Add(combinedLoopStep);
                        return true;
                    }

                    steps.Add(Utf8ByteSafeLinearVerifierStep.LoopProgram(programSteps, node.Min, node.Max));
                    return true;
                }

                return false;

            default:
                if (s_lastFailureKind == Utf8ByteSafeLinearCompileFailureKind.None)
                {
                    s_lastFailureKind = Utf8ByteSafeLinearCompileFailureKind.UnsupportedLoop;
                }
                return false;
        }
    }

    private static bool TryCompileDeterministicSubProgram(
        Utf8ExecutionNode node,
        out Utf8ByteSafeLinearVerifierStep[] steps,
        out int minConsumedLength)
    {
        var list = new List<Utf8ByteSafeLinearVerifierStep>();
        if (!TryAppendNode(node, list))
        {
            steps = [];
            minConsumedLength = 0;
            if (s_lastFailureKind == Utf8ByteSafeLinearCompileFailureKind.None)
            {
                s_lastFailureKind = Utf8ByteSafeLinearCompileFailureKind.NestedSubProgram;
            }
            return false;
        }

        steps = [.. list];
        minConsumedLength = GetMinimumConsumedLength(steps);
        return steps.Length > 0;
    }

    private static bool TryCombineLoopSubProgram(
        Utf8ByteSafeLinearVerifierStep[] programSteps,
        int outerMin,
        int outerMax,
        out Utf8ByteSafeLinearVerifierStep combinedStep)
    {
        combinedStep = default;
        if (programSteps.Length != 1)
        {
            return false;
        }

        var step = programSteps[0];
        var (kind, min, max) = step.Kind switch
        {
            Utf8ByteSafeLinearVerifierStepKind.LoopByte => (step.Kind, step.Min, step.Max),
            Utf8ByteSafeLinearVerifierStepKind.LoopText => (step.Kind, step.Min, step.Max),
            Utf8ByteSafeLinearVerifierStepKind.LoopSet => (step.Kind, step.Min, step.Max),
            Utf8ByteSafeLinearVerifierStepKind.LoopProjectedAsciiSet => (step.Kind, step.Min, step.Max),
            Utf8ByteSafeLinearVerifierStepKind.LoopAnyText => (step.Kind, step.Min, step.Max),
            _ => (Utf8ByteSafeLinearVerifierStepKind.Accept, 0, 0),
        };

        if (kind == Utf8ByteSafeLinearVerifierStepKind.Accept)
        {
            return false;
        }

        var combinedMin = CombineLoopBounds(outerMin, min);
        var combinedMax = CombineLoopBounds(outerMax, max);
        if (combinedMin < 0 || combinedMax == int.MinValue)
        {
            return false;
        }

        combinedStep = kind switch
        {
            Utf8ByteSafeLinearVerifierStepKind.LoopByte =>
                Utf8ByteSafeLinearVerifierStep.LoopByte(step.Value, combinedMin, combinedMax),
            Utf8ByteSafeLinearVerifierStepKind.LoopText =>
                Utf8ByteSafeLinearVerifierStep.LoopText(step.Text!, combinedMin, combinedMax),
            Utf8ByteSafeLinearVerifierStepKind.LoopSet =>
                Utf8ByteSafeLinearVerifierStep.LoopSet(step.Set!, combinedMin, combinedMax),
            Utf8ByteSafeLinearVerifierStepKind.LoopProjectedAsciiSet =>
                Utf8ByteSafeLinearVerifierStep.LoopProjectedAsciiSet(step.Set!, step.ProjectedAsciiCharClass!, combinedMin, combinedMax),
            Utf8ByteSafeLinearVerifierStepKind.LoopAnyText =>
                Utf8ByteSafeLinearVerifierStep.LoopAnyText(step.Alternatives!, combinedMin, combinedMax),
            _ => default,
        };

        return combinedStep.Kind != Utf8ByteSafeLinearVerifierStepKind.MatchByte;
    }

    private static int CombineLoopBounds(int outer, int inner)
    {
        if (outer == 0 || inner == 0)
        {
            return 0;
        }

        if (outer < 0 || inner < 0)
        {
            return -1;
        }

        var combined = (long)outer * inner;
        return combined > int.MaxValue ? int.MinValue : (int)combined;
    }

    private static bool TryAppendTerminalLoop(
        IReadOnlyList<Utf8ExecutionNode> children,
        int index,
        List<Utf8ByteSafeLinearVerifierStep> steps)
    {
        var node = Unwrap(children[index]);
        if (node.Kind is not (Utf8ExecutionNodeKind.Loop or Utf8ExecutionNodeKind.LazyLoop) ||
            !CanTreatRemainingAsZeroWidth(children, index + 1) ||
            !TryAppendLoop(node, steps, allowVariableLength: true))
        {
            return false;
        }

        for (var i = index + 1; i < children.Count; i++)
        {
            if (!TryAppendNode(Unwrap(children[i]), steps))
            {
                return false;
            }
        }

        return true;
    }

    private static bool TryAppendSafeVariableLoop(
        IReadOnlyList<Utf8ExecutionNode> children,
        int index,
        List<Utf8ByteSafeLinearVerifierStep> steps)
    {
        var node = Unwrap(children[index]);
        if (node.Kind != Utf8ExecutionNodeKind.Loop || node.Min == node.Max)
        {
            return false;
        }

        if (!TryGetLeadingAsciiByteMask(node, out var loopLowMask, out var loopHighMask) ||
            !TryGetFollowingAsciiByteMask(children, index + 1, out var nextLowMask, out var nextHighMask) ||
            !MasksAreDisjoint(loopLowMask, loopHighMask, nextLowMask, nextHighMask))
        {
            if (s_lastFailureKind == Utf8ByteSafeLinearCompileFailureKind.None)
            {
                s_lastFailureKind = Utf8ByteSafeLinearCompileFailureKind.NonDisjointVariableLoop;
            }
            return false;
        }

        return TryAppendLoop(node, steps, allowVariableLength: true);
    }

    private static Utf8ExecutionNode Unwrap(Utf8ExecutionNode node)
    {
        while (node.Kind is Utf8ExecutionNodeKind.Capture or Utf8ExecutionNodeKind.Group &&
               node.Children.Count == 1)
        {
            node = node.Children[0];
        }

        return node;
    }

    private static bool TryGetAsciiBytes(string? text, out byte[] bytes)
    {
        bytes = [];
        if (string.IsNullOrEmpty(text))
        {
            return false;
        }

        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] > 0x7F)
            {
                return false;
            }
        }

        bytes = text.Select(static ch => (byte)ch).ToArray();
        return true;
    }

    private static bool TryCreateByteSafeSetStep(string runtimeSet, bool loop, int min, int max, out Utf8ByteSafeLinearVerifierStep step)
    {
        if (TryCreateProjectedAsciiCharClass(runtimeSet, out var asciiCharClass))
        {
            step = loop
                ? Utf8ByteSafeLinearVerifierStep.LoopProjectedAsciiSet(runtimeSet, asciiCharClass, min, max)
                : Utf8ByteSafeLinearVerifierStep.MatchProjectedAsciiSet(runtimeSet, asciiCharClass);
            return true;
        }

        if (RuntimeFrontEnd.RegexCharClass.IsAscii(runtimeSet))
        {
            step = loop
                ? Utf8ByteSafeLinearVerifierStep.LoopSet(runtimeSet, min, max)
                : Utf8ByteSafeLinearVerifierStep.MatchSet(runtimeSet);
            return true;
        }

        step = default;
        return false;
    }

    private static bool TryCreateProjectedAsciiCharClass(string runtimeSet, out AsciiCharClass asciiCharClass)
    {
        if (TryCreateKnownProjectedAsciiCharClass(runtimeSet, out asciiCharClass))
        {
            return true;
        }

        if (!CanProjectRuntimeSetToAscii(runtimeSet))
        {
            asciiCharClass = null!;
            return false;
        }

        var matches = new bool[128];
        if (!TryPopulateProjectedAsciiMatches(runtimeSet, matches))
        {
            asciiCharClass = null!;
            return false;
        }

        var negated = RuntimeFrontEnd.RegexCharClass.IsNegated(runtimeSet);
        asciiCharClass = new AsciiCharClass(matches, negated);
        return true;
    }

    private static bool TryCreateKnownProjectedAsciiCharClass(string runtimeSet, out AsciiCharClass asciiCharClass)
    {
        switch (runtimeSet)
        {
            case RuntimeFrontEnd.RegexCharClass.SpaceClass:
            case RuntimeFrontEnd.RegexCharClass.ECMASpaceClass:
                asciiCharClass = CreateAsciiCharClass(static ch => ch is ' ' or '\t' or '\r' or '\n' or '\f' or '\v', negated: false);
                return true;

            case RuntimeFrontEnd.RegexCharClass.NotSpaceClass:
            case RuntimeFrontEnd.RegexCharClass.NotECMASpaceClass:
                asciiCharClass = CreateAsciiCharClass(static ch => ch is ' ' or '\t' or '\r' or '\n' or '\f' or '\v', negated: true);
                return true;

            default:
                asciiCharClass = null!;
                return false;
        }
    }

    private static bool CanProjectRuntimeSetToAscii(string runtimeSet)
    {
        if (RuntimeFrontEnd.RegexCharClass.IsAscii(runtimeSet))
        {
            return true;
        }

        var categoryPayload = GetCategoryPayload(runtimeSet);
        if (categoryPayload is null)
        {
            return false;
        }

        var digitPayload = GetCategoryPayload(RuntimeFrontEnd.RegexCharClass.DigitClass);
        var notDigitPayload = GetCategoryPayload(RuntimeFrontEnd.RegexCharClass.NotDigitClass);
        var spacePayload = GetCategoryPayload(RuntimeFrontEnd.RegexCharClass.SpaceClass);
        var notSpacePayload = GetCategoryPayload(RuntimeFrontEnd.RegexCharClass.NotSpaceClass);
        var wordPayload = GetCategoryPayload(RuntimeFrontEnd.RegexCharClass.WordClass);
        var notWordPayload = GetCategoryPayload(RuntimeFrontEnd.RegexCharClass.NotWordClass);

        return categoryPayload == digitPayload ||
            categoryPayload == notDigitPayload ||
            categoryPayload == spacePayload ||
            categoryPayload == notSpacePayload ||
            categoryPayload == wordPayload ||
            categoryPayload == notWordPayload;
    }

    private static bool TryPopulateProjectedAsciiMatches(string runtimeSet, bool[] matches)
    {
        if (RuntimeFrontEnd.RegexCharClass.IsAscii(runtimeSet))
        {
            for (var i = 0; i < matches.Length; i++)
            {
                matches[i] = RuntimeFrontEnd.RegexCharClass.CharInClassBase((char)i, runtimeSet);
            }

            return true;
        }

        var categoryPayload = GetCategoryPayload(runtimeSet);
        if (categoryPayload is null)
        {
            return false;
        }

        for (var i = 0; i < matches.Length; i++)
        {
            matches[i] = MatchesKnownProjectedAsciiCategory((char)i, categoryPayload);
        }

        var setLength = runtimeSet[RuntimeFrontEnd.RegexCharClass.SetLengthIndex];
        var setEnd = RuntimeFrontEnd.RegexCharClass.SetStartIndex + setLength;
        for (var i = RuntimeFrontEnd.RegexCharClass.SetStartIndex; i < setEnd; i += 2)
        {
            var start = runtimeSet[i];
            var endExclusive = runtimeSet[i + 1];
            if (start >= 0x80)
            {
                continue;
            }

            var max = Math.Min(endExclusive, (char)0x80);
            for (var ch = start; ch < max; ch++)
            {
                matches[ch] = true;
            }
        }

        return true;
    }

    private static string? GetCategoryPayload(string runtimeSet)
    {
        if (runtimeSet.Length < RuntimeFrontEnd.RegexCharClass.SetStartIndex)
        {
            return null;
        }

        var setLength = runtimeSet[RuntimeFrontEnd.RegexCharClass.SetLengthIndex];
        var categoryLength = runtimeSet[RuntimeFrontEnd.RegexCharClass.CategoryLengthIndex];
        if (categoryLength == 0)
        {
            return string.Empty;
        }

        var setEnd = RuntimeFrontEnd.RegexCharClass.SetStartIndex + setLength;
        if (runtimeSet.Length < setEnd + categoryLength)
        {
            return null;
        }

        for (var i = RuntimeFrontEnd.RegexCharClass.SetStartIndex; i < setEnd; i += 2)
        {
            if (runtimeSet[i + 1] > 0x80)
            {
                return null;
            }
        }

        return runtimeSet.Substring(setEnd, categoryLength);
    }

    private static bool MatchesKnownProjectedAsciiCategory(char ch, string categoryPayload)
    {
        var digitPayload = GetCategoryPayload(RuntimeFrontEnd.RegexCharClass.DigitClass);
        if (categoryPayload == digitPayload)
        {
            return ch is >= '0' and <= '9';
        }

        var notDigitPayload = GetCategoryPayload(RuntimeFrontEnd.RegexCharClass.NotDigitClass);
        if (categoryPayload == notDigitPayload)
        {
            return ch is < '0' or > '9';
        }

        var spacePayload = GetCategoryPayload(RuntimeFrontEnd.RegexCharClass.SpaceClass);
        if (categoryPayload == spacePayload)
        {
            return ch is ' ' or '\t' or '\r' or '\n' or '\f' or '\v';
        }

        var notSpacePayload = GetCategoryPayload(RuntimeFrontEnd.RegexCharClass.NotSpaceClass);
        if (categoryPayload == notSpacePayload)
        {
            return ch is not (' ' or '\t' or '\r' or '\n' or '\f' or '\v');
        }

        var wordPayload = GetCategoryPayload(RuntimeFrontEnd.RegexCharClass.WordClass);
        if (categoryPayload == wordPayload)
        {
            return RuntimeFrontEnd.RegexCharClass.IsBoundaryWordChar(ch);
        }

        var notWordPayload = GetCategoryPayload(RuntimeFrontEnd.RegexCharClass.NotWordClass);
        if (categoryPayload == notWordPayload)
        {
            return !RuntimeFrontEnd.RegexCharClass.IsBoundaryWordChar(ch);
        }

        return RuntimeFrontEnd.RegexCharClass.CharInClassBase(ch, categoryPayload);
    }

    private static AsciiCharClass CreateAsciiCharClass(Func<char, bool> predicate, bool negated)
    {
        var matches = new bool[128];
        for (var i = 0; i < matches.Length; i++)
        {
            matches[i] = predicate((char)i);
        }

        return new AsciiCharClass(matches, negated);
    }

    private static bool TryAppendTerminalAlternation(
        IReadOnlyList<Utf8ExecutionNode> children,
        int index,
        List<Utf8ByteSafeLinearVerifierStep> steps)
    {
        var node = Unwrap(children[index]);
        if (!CanTreatRemainingAsZeroWidth(children, index + 1))
        {
            if (node.Kind == Utf8ExecutionNodeKind.Alternate &&
                s_lastFailureKind == Utf8ByteSafeLinearCompileFailureKind.None)
            {
                s_lastFailureKind = Utf8ByteSafeLinearCompileFailureKind.UnsupportedAlternation;
            }
            return false;
        }

        if (TryExtractAlternationTexts(node, out var alternatives, out var optional))
        {
            steps.Add(optional
                ? Utf8ByteSafeLinearVerifierStep.MatchAnyTextOptional(alternatives)
                : Utf8ByteSafeLinearVerifierStep.MatchAnyText(alternatives));

            for (var i = index + 1; i < children.Count; i++)
            {
                if (!TryAppendNode(Unwrap(children[i]), steps))
                {
                    return false;
                }
            }

            return true;
        }

        return false;
    }

    private static bool TryAppendDeterministicOptionalAlternation(
        IReadOnlyList<Utf8ExecutionNode> children,
        int index,
        List<Utf8ByteSafeLinearVerifierStep> steps)
    {
        var node = Unwrap(children[index]);
        if (!TryExtractAlternationTexts(node, out var alternatives, out var optional) ||
            !optional ||
            !TryExtractFollowingAsciiPrefix(children, index + 1, out var followingPrefix) ||
            !IsSafeOptionalAlternation(alternatives, followingPrefix))
        {
            if (node.Kind == Utf8ExecutionNodeKind.Alternate &&
                s_lastFailureKind == Utf8ByteSafeLinearCompileFailureKind.None)
            {
                s_lastFailureKind = Utf8ByteSafeLinearCompileFailureKind.UnsupportedAlternation;
            }
            return false;
        }

        steps.Add(Utf8ByteSafeLinearVerifierStep.MatchAnyTextOptional(alternatives));
        return true;
    }

    private static bool TryAppendDeterministicAlternation(Utf8ExecutionNode node, List<Utf8ByteSafeLinearVerifierStep> steps)
    {
        if (!TryExtractAlternationTexts(node, out var alternatives, out var optional) ||
            optional ||
            !AreAlternativesPrefixFree(alternatives))
        {
            if (node.Kind == Utf8ExecutionNodeKind.Alternate &&
                s_lastFailureKind == Utf8ByteSafeLinearCompileFailureKind.None)
            {
                s_lastFailureKind = Utf8ByteSafeLinearCompileFailureKind.UnsupportedAlternation;
            }
            return false;
        }

        steps.Add(Utf8ByteSafeLinearVerifierStep.MatchAnyText(alternatives));
        return true;
    }

    private static bool CanTreatRemainingAsZeroWidth(IReadOnlyList<Utf8ExecutionNode> children, int startIndex)
    {
        for (var i = startIndex; i < children.Count; i++)
        {
            if (!IsZeroWidthNode(Unwrap(children[i])))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsZeroWidthNode(Utf8ExecutionNode node)
    {
        return node.Kind is Utf8ExecutionNodeKind.Empty
            or Utf8ExecutionNodeKind.Bol
            or Utf8ExecutionNodeKind.Beginning
            or Utf8ExecutionNodeKind.Start
            or Utf8ExecutionNodeKind.Eol
            or Utf8ExecutionNodeKind.EndZ
            or Utf8ExecutionNodeKind.End
            or Utf8ExecutionNodeKind.Boundary
            or Utf8ExecutionNodeKind.NonBoundary;
    }

    private static bool TryExtractAlternationTexts(Utf8ExecutionNode node, out byte[][] alternatives, out bool optional)
    {
        alternatives = [];
        optional = false;

        if (node.Kind == Utf8ExecutionNodeKind.Loop &&
            node.Min == 0 &&
            node.Max == 1)
        {
            var candidate = node.Children.Count == 1 ? Unwrap(node.Children[0]) : node;
            if (TryExtractAlternationTexts(candidate, out alternatives, out _))
            {
                optional = true;
                return true;
            }
        }

        if (node.Kind != Utf8ExecutionNodeKind.Alternate)
        {
            return false;
        }

        var list = new List<byte[]>(node.Children.Count);
        foreach (var child in node.Children)
        {
            if (!TryExtractFixedAsciiText(Unwrap(child), out var text))
            {
                if (s_lastFailureKind == Utf8ByteSafeLinearCompileFailureKind.None)
                {
                    s_lastFailureKind = Utf8ByteSafeLinearCompileFailureKind.UnsupportedFixedText;
                }
                return false;
            }

            list.Add(text);
        }

        alternatives = [.. list];
        return alternatives.Length > 0;
    }

    private static bool TryExtractFollowingAsciiPrefix(
        IReadOnlyList<Utf8ExecutionNode> children,
        int startIndex,
        out byte[] prefix)
    {
        prefix = [];
        for (var i = startIndex; i < children.Count; i++)
        {
            var child = Unwrap(children[i]);
            if (IsZeroWidthNode(child))
            {
                continue;
            }

            if (!TryExtractFixedAsciiText(child, out prefix) || prefix.Length == 0)
            {
                if (s_lastFailureKind == Utf8ByteSafeLinearCompileFailureKind.None)
                {
                    s_lastFailureKind = Utf8ByteSafeLinearCompileFailureKind.UnsupportedFollowingPrefix;
                }
                return false;
            }

            return true;
        }

        return false;
    }

    private static bool TryGetFollowingAsciiByteMask(
        IReadOnlyList<Utf8ExecutionNode> children,
        int startIndex,
        out ulong lowMask,
        out ulong highMask)
    {
        lowMask = 0;
        highMask = 0;

        for (var i = startIndex; i < children.Count; i++)
        {
            var child = Unwrap(children[i]);
            if (IsZeroWidthNode(child))
            {
                continue;
            }

            return TryGetLeadingAsciiByteMask(child, out lowMask, out highMask);
        }

        if (s_lastFailureKind == Utf8ByteSafeLinearCompileFailureKind.None)
        {
            s_lastFailureKind = Utf8ByteSafeLinearCompileFailureKind.UnsupportedFollowingMask;
        }
        return false;
    }

    private static bool TryExtractFixedAsciiText(Utf8ExecutionNode node, out byte[] text)
    {
        text = [];
        switch (node.Kind)
        {
            case Utf8ExecutionNodeKind.Empty:
                text = [];
                return true;

            case Utf8ExecutionNodeKind.One when node.Ch <= 0x7F:
                text = [(byte)node.Ch];
                return true;

            case Utf8ExecutionNodeKind.Multi when TryGetAsciiBytes(node.Text, out var bytes):
                text = bytes;
                return true;

            case Utf8ExecutionNodeKind.Concatenate:
                var buffer = new List<byte>();
                foreach (var child in node.Children)
                {
                    if (!TryExtractFixedAsciiText(Unwrap(child), out var childText))
                    {
                        return false;
                    }

                    buffer.AddRange(childText);
                }

                text = [.. buffer];
                return true;

            default:
                if (s_lastFailureKind == Utf8ByteSafeLinearCompileFailureKind.None)
                {
                    s_lastFailureKind = Utf8ByteSafeLinearCompileFailureKind.UnsupportedFixedText;
                }
                return false;
        }
    }

    private static bool AreAlternativesPrefixFree(byte[][] alternatives)
    {
        for (var i = 0; i < alternatives.Length; i++)
        {
            var current = alternatives[i];
            for (var j = 0; j < alternatives.Length; j++)
            {
                if (i == j)
                {
                    continue;
                }

                var other = alternatives[j];
                if (current.Length <= other.Length &&
                    other.AsSpan(0, current.Length).SequenceEqual(current))
                {
                    return false;
                }
            }
        }

        return true;
    }

    private static bool IsSafeOptionalAlternation(byte[][] alternatives, byte[] followingPrefix)
    {
        for (var i = 0; i < alternatives.Length; i++)
        {
            var alternative = alternatives[i];
            if (alternative.Length == 0)
            {
                return false;
            }

            if (alternative.Length <= followingPrefix.Length &&
                followingPrefix.AsSpan(0, alternative.Length).SequenceEqual(alternative))
            {
                return false;
            }
        }

        return true;
    }

    private static bool TryGetLeadingAsciiByteMask(Utf8ExecutionNode node, out ulong lowMask, out ulong highMask)
    {
        lowMask = 0;
        highMask = 0;

        switch (node.Kind)
        {
            case Utf8ExecutionNodeKind.One when node.Ch <= 0x7F:
                AddByteToMask((byte)node.Ch, ref lowMask, ref highMask);
                return true;

            case Utf8ExecutionNodeKind.Multi when TryGetAsciiBytes(node.Text, out var text) && text.Length > 0:
                AddByteToMask(text[0], ref lowMask, ref highMask);
                return true;

            case Utf8ExecutionNodeKind.Set when node.Text is { Length: > 0 } set:
                return TryBuildByteSafeLeadingMask(set, out lowMask, out highMask);

            case Utf8ExecutionNodeKind.Concatenate:
                foreach (var child in node.Children)
                {
                    var unwrapped = Unwrap(child);
                    if (IsZeroWidthNode(unwrapped))
                    {
                        continue;
                    }

                    return TryGetLeadingAsciiByteMask(unwrapped, out lowMask, out highMask);
                }

                return false;

            case Utf8ExecutionNodeKind.Alternate:
                foreach (var child in node.Children)
                {
                    if (!TryGetLeadingAsciiByteMask(Unwrap(child), out var childLowMask, out var childHighMask))
                    {
                        return false;
                    }

                    lowMask |= childLowMask;
                    highMask |= childHighMask;
                }

                return (lowMask | highMask) != 0;

            case Utf8ExecutionNodeKind.Loop:
            case Utf8ExecutionNodeKind.LazyLoop:
                if (node.Children.Count == 1)
                {
                    return TryGetLeadingAsciiByteMask(Unwrap(node.Children[0]), out lowMask, out highMask);
                }

                if (node.Ch is > (char)0 and <= (char)0x7F)
                {
                    AddByteToMask((byte)node.Ch, ref lowMask, ref highMask);
                    return true;
                }

                if (node.Text is { Length: > 0 } loopSet)
                {
                    return TryBuildByteSafeLeadingMask(loopSet, out lowMask, out highMask);
                }

                if (TryGetAsciiBytes(node.Text, out var loopText) && loopText.Length > 0)
                {
                    AddByteToMask(loopText[0], ref lowMask, ref highMask);
                    return true;
                }

                return false;

            default:
                return false;
        }
    }

    private static bool TryBuildAsciiSetMask(string set, out ulong lowMask, out ulong highMask)
    {
        lowMask = 0;
        highMask = 0;

        for (var i = 0; i < 128; i++)
        {
            if (RuntimeFrontEnd.RegexCharClass.CharInClass((char)i, set))
            {
                AddByteToMask((byte)i, ref lowMask, ref highMask);
            }
        }

        return (lowMask | highMask) != 0;
    }

    private static bool TryBuildByteSafeLeadingMask(string set, out ulong lowMask, out ulong highMask)
    {
        if (RuntimeFrontEnd.RegexCharClass.IsAscii(set))
        {
            return TryBuildAsciiSetMask(set, out lowMask, out highMask);
        }

        if (TryCreateProjectedAsciiCharClass(set, out var asciiCharClass))
        {
            lowMask = 0;
            highMask = 0;
            foreach (var value in asciiCharClass.GetPositiveMatchBytes())
            {
                AddByteToMask(value, ref lowMask, ref highMask);
            }

            return (lowMask | highMask) != 0;
        }

        lowMask = 0;
        highMask = 0;
        return false;
    }

    private static void AddByteToMask(byte value, ref ulong lowMask, ref ulong highMask)
    {
        if (value < 64)
        {
            lowMask |= 1UL << value;
        }
        else
        {
            highMask |= 1UL << (value - 64);
        }
    }

    private static bool MasksAreDisjoint(
        ulong firstLowMask,
        ulong firstHighMask,
        ulong secondLowMask,
        ulong secondHighMask)
    {
        return (firstLowMask & secondLowMask) == 0 &&
               (firstHighMask & secondHighMask) == 0;
    }

    private static bool TryConsumeByteLoop(ReadOnlySpan<byte> input, ref int index, byte value, int min, int max)
    {
        var count = 0;
        while ((uint)index < (uint)input.Length &&
               input[index] == value &&
               (max < 0 || count < max))
        {
            index++;
            count++;
        }

        return count >= min;
    }

    private static bool TryConsumeTextLoop(ReadOnlySpan<byte> input, ref int index, ReadOnlySpan<byte> text, int min, int max)
    {
        var count = 0;
        while (input.Length - index >= text.Length &&
               input.Slice(index, text.Length).SequenceEqual(text) &&
               (max < 0 || count < max))
        {
            index += text.Length;
            count++;
        }

        return count >= min;
    }

    private static bool TryConsumeSetLoop(ReadOnlySpan<byte> input, ref int index, string set, int min, int max)
    {
        var count = 0;
        while ((uint)index < (uint)input.Length &&
               MatchesSet(input[index], set) &&
               (max < 0 || count < max))
        {
            index++;
            count++;
        }

        return count >= min;
    }

    private static bool TryConsumeProjectedAsciiSetLoop(
        ReadOnlySpan<byte> input,
        ref int index,
        AsciiCharClass asciiCharClass,
        int min,
        int max,
        out bool requiresCompatibilityFallback)
    {
        requiresCompatibilityFallback = false;
        var count = 0;
        while ((uint)index < (uint)input.Length && (max < 0 || count < max))
        {
            var value = input[index];
            if (value >= 0x80)
            {
                requiresCompatibilityFallback = true;
                return false;
            }

            if (!asciiCharClass.Contains(value))
            {
                break;
            }

            index++;
            count++;
        }

        return count >= min;
    }

    private static bool TryMatchAnyText(ReadOnlySpan<byte> input, ref int index, byte[][] alternatives, bool optional)
    {
        for (var i = 0; i < alternatives.Length; i++)
        {
            var candidate = alternatives[i];
            if (input.Length - index >= candidate.Length &&
                input.Slice(index, candidate.Length).SequenceEqual(candidate))
            {
                index += candidate.Length;
                return true;
            }
        }

        return optional;
    }

    private static bool TryConsumeAnyTextLoop(ReadOnlySpan<byte> input, ref int index, byte[][] alternatives, int min, int max)
    {
        var count = 0;
        while (max < 0 || count < max)
        {
            var nextIndex = index;
            if (!TryMatchAnyText(input, ref nextIndex, alternatives, optional: false))
            {
                break;
            }

            index = nextIndex;
            count++;
        }

        return count >= min;
    }

    private static bool TryConsumeProgramLoop(
        ReadOnlySpan<byte> input,
        ref int index,
        Utf8ByteSafeLinearVerifierStep[] program,
        int min,
        int max,
        out bool requiresCompatibilityFallback)
    {
        requiresCompatibilityFallback = false;
        var count = 0;
        while (max < 0 || count < max)
        {
            var nextIndex = index;
            if (!TryExecuteProgram(program, input, ref nextIndex, out requiresCompatibilityFallback))
            {
                break;
            }

            if (nextIndex == index)
            {
                return false;
            }

            index = nextIndex;
            count++;
        }

        return !requiresCompatibilityFallback && count >= min;
    }

    private static bool TryExecuteProgram(
        Utf8ByteSafeLinearVerifierStep[] steps,
        ReadOnlySpan<byte> input,
        ref int index,
        out bool requiresCompatibilityFallback)
    {
        requiresCompatibilityFallback = false;
        var startIndex = index;
        foreach (var step in steps)
        {
            if (!TryExecuteStep(step, input, ref index, startIndex, out _, out requiresCompatibilityFallback))
            {
                return false;
            }

            if (step.Kind == Utf8ByteSafeLinearVerifierStepKind.Accept)
            {
                return false;
            }
        }

        return true;
    }

    private static bool TryExecuteStep(
        Utf8ByteSafeLinearVerifierStep step,
        ReadOnlySpan<byte> input,
        ref int index,
        int startIndex,
        out int matchedLength,
        out bool requiresCompatibilityFallback)
    {
        matchedLength = 0;
        requiresCompatibilityFallback = false;

        switch (step.Kind)
        {
            case Utf8ByteSafeLinearVerifierStepKind.MatchByte:
                if ((uint)index >= (uint)input.Length || input[index] != step.Value)
                {
                    return false;
                }

                index++;
                return true;

            case Utf8ByteSafeLinearVerifierStepKind.MatchText:
                var text = step.Text!;
                if (input.Length - index < text.Length || !input.Slice(index, text.Length).SequenceEqual(text))
                {
                    return false;
                }

                index += text.Length;
                return true;

            case Utf8ByteSafeLinearVerifierStepKind.MatchSet:
                if ((uint)index >= (uint)input.Length || !MatchesSet(input[index], step.Set!))
                {
                    return false;
                }

                index++;
                return true;

            case Utf8ByteSafeLinearVerifierStepKind.MatchProjectedAsciiSet:
                if ((uint)index >= (uint)input.Length)
                {
                    return false;
                }

                if (input[index] >= 0x80)
                {
                    requiresCompatibilityFallback = true;
                    return false;
                }

                if (step.ProjectedAsciiCharClass is null || !step.ProjectedAsciiCharClass.Contains(input[index]))
                {
                    return false;
                }

                index++;
                return true;

            case Utf8ByteSafeLinearVerifierStepKind.LoopByte:
                return TryConsumeByteLoop(input, ref index, step.Value, step.Min, step.Max);

            case Utf8ByteSafeLinearVerifierStepKind.LoopText:
                return TryConsumeTextLoop(input, ref index, step.Text!, step.Min, step.Max);

            case Utf8ByteSafeLinearVerifierStepKind.LoopSet:
                return TryConsumeSetLoop(input, ref index, step.Set!, step.Min, step.Max);

            case Utf8ByteSafeLinearVerifierStepKind.LoopProjectedAsciiSet:
                return TryConsumeProjectedAsciiSetLoop(input, ref index, step.ProjectedAsciiCharClass!, step.Min, step.Max, out requiresCompatibilityFallback);

            case Utf8ByteSafeLinearVerifierStepKind.RequireBeginning:
                return IsBeginningOfLine(input, index, step.Options);

            case Utf8ByteSafeLinearVerifierStepKind.RequireEnd:
                return IsEndAnchorMatch(input, index, step.Options);

            case Utf8ByteSafeLinearVerifierStepKind.RequireBoundary:
                return IsWordBoundary(input, index);

            case Utf8ByteSafeLinearVerifierStepKind.RequireNonBoundary:
                return !IsWordBoundary(input, index);

            case Utf8ByteSafeLinearVerifierStepKind.MatchAnyText:
                return TryMatchAnyText(input, ref index, step.Alternatives!, optional: false);

            case Utf8ByteSafeLinearVerifierStepKind.MatchAnyTextOptional:
                return TryMatchAnyText(input, ref index, step.Alternatives!, optional: true);

            case Utf8ByteSafeLinearVerifierStepKind.LoopAnyText:
                return TryConsumeAnyTextLoop(input, ref index, step.Alternatives!, step.Min, step.Max);

            case Utf8ByteSafeLinearVerifierStepKind.LoopProgram:
                return TryConsumeProgramLoop(input, ref index, step.Program!, step.Min, step.Max, out requiresCompatibilityFallback);

            case Utf8ByteSafeLinearVerifierStepKind.Accept:
                matchedLength = index - startIndex;
                return true;

            default:
                return false;
        }
    }

    private static int GetMinimumConsumedLength(Utf8ByteSafeLinearVerifierStep[] steps)
    {
        var length = 0;
        foreach (var step in steps)
        {
            checked
            {
                length += step.Kind switch
                {
                    Utf8ByteSafeLinearVerifierStepKind.MatchByte => 1,
                    Utf8ByteSafeLinearVerifierStepKind.MatchText => step.Text!.Length,
                    Utf8ByteSafeLinearVerifierStepKind.MatchSet => 1,
                    Utf8ByteSafeLinearVerifierStepKind.MatchProjectedAsciiSet => 1,
                    Utf8ByteSafeLinearVerifierStepKind.LoopByte => step.Min,
                    Utf8ByteSafeLinearVerifierStepKind.LoopText => step.Min * step.Text!.Length,
                    Utf8ByteSafeLinearVerifierStepKind.LoopSet => step.Min,
                    Utf8ByteSafeLinearVerifierStepKind.LoopProjectedAsciiSet => step.Min,
                    Utf8ByteSafeLinearVerifierStepKind.MatchAnyText => step.Alternatives!.Min(static value => value.Length),
                    Utf8ByteSafeLinearVerifierStepKind.MatchAnyTextOptional => 0,
                    Utf8ByteSafeLinearVerifierStepKind.LoopAnyText => step.Min * step.Alternatives!.Min(static value => value.Length),
                    Utf8ByteSafeLinearVerifierStepKind.LoopProgram => step.Min * GetMinimumConsumedLength(step.Program!),
                    _ => 0,
                };
            }
        }

        return length;
    }

    private static bool MatchesSet(byte value, string runtimeSet)
    {
        return value < 128 && RuntimeFrontEnd.RegexCharClass.CharInClass((char)value, runtimeSet);
    }

    private static bool IsBeginningOfLine(ReadOnlySpan<byte> input, int index, RegexOptions options)
    {
        return index == 0 || ((options & RegexOptions.Multiline) != 0 && input[index - 1] == (byte)'\n');
    }

    private static bool IsEndAnchorMatch(ReadOnlySpan<byte> input, int index, RegexOptions options)
    {
        if (index == input.Length)
        {
            return true;
        }

        return ((options & RegexOptions.Multiline) != 0 &&
                input[index] == (byte)'\n') ||
               (index == input.Length - 1 && input[index] == (byte)'\n');
    }

    private static bool IsWordBoundary(ReadOnlySpan<byte> input, int byteOffset)
    {
        var previousIsWord = byteOffset > 0 &&
            RuntimeFrontEnd.RegexCharClass.IsBoundaryWordChar((char)input[byteOffset - 1]);
        var nextIsWord = byteOffset < input.Length &&
            RuntimeFrontEnd.RegexCharClass.IsBoundaryWordChar((char)input[byteOffset]);
        return previousIsWord != nextIsWord;
    }
}
