using Lokad.Utf8Regex.Internal.FrontEnd;

namespace Lokad.Utf8Regex.Internal.Execution;

internal enum Utf8ByteSafeLinearVerifierStepKind : byte
{
    MatchByte = 0,
    MatchText = 1,
    MatchAsciiClass = 3,
    LoopByte = 4,
    LoopText = 5,
    LoopAsciiClass = 7,
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

internal readonly partial struct Utf8ByteSafeLinearVerifierStep
{
    private readonly byte[]? _text;
    private readonly byte[][]? _alternatives;
    private readonly Utf8ByteSafeLinearVerifierStep[]? _program;
    private readonly AsciiCharClass _charClass;

    private Utf8ByteSafeLinearVerifierStep(
        Utf8ByteSafeLinearVerifierStepKind kind,
        byte value,
        byte[]? text,
        byte[][]? alternatives,
        Utf8ByteSafeLinearVerifierStep[]? program,
        AsciiCharClass charClass,
        int min,
        int max,
        RegexOptions options)
    {
        Kind = kind;
        Value = value;
        _text = text;
        _alternatives = alternatives;
        _program = program;
        _charClass = charClass;
        Min = min;
        Max = max;
        Options = options;
    }

    public Utf8ByteSafeLinearVerifierStepKind Kind { get; }

    public byte Value { get; }

    public byte[] Text => _text ?? [];

    public byte[][] Alternatives => _alternatives ?? [];

    public Utf8ByteSafeLinearVerifierStep[] Program => _program ?? [];

    public AsciiCharClass CharClass => _charClass;

    public int Min { get; }

    public int Max { get; }

    public RegexOptions Options { get; }

    internal Utf8ExecutionNodeKind AnchorKind => Value != 0 ? (Utf8ExecutionNodeKind)Value
        : Kind == Utf8ByteSafeLinearVerifierStepKind.RequireBeginning ? Utf8ExecutionNodeKind.Bol : Utf8ExecutionNodeKind.Eol;

    public static Utf8ByteSafeLinearVerifierStep MatchByte(byte value) =>
        new(Utf8ByteSafeLinearVerifierStepKind.MatchByte, value, null, null, null, default, 0, 0, RegexOptions.None);

    public static Utf8ByteSafeLinearVerifierStep MatchText(byte[] text) =>
        new(Utf8ByteSafeLinearVerifierStepKind.MatchText, 0, text, null, null, default, 0, 0, RegexOptions.None);

    public static Utf8ByteSafeLinearVerifierStep MatchAsciiClass(AsciiCharClass asciiCharClass) =>
        new(Utf8ByteSafeLinearVerifierStepKind.MatchAsciiClass, 0, null, null, null, asciiCharClass, 0, 0, RegexOptions.None);

    public static Utf8ByteSafeLinearVerifierStep LoopByte(byte value, int min, int max) =>
        new(Utf8ByteSafeLinearVerifierStepKind.LoopByte, value, null, null, null, default, min, max, RegexOptions.None);

    public static Utf8ByteSafeLinearVerifierStep LoopText(byte[] text, int min, int max) =>
        new(Utf8ByteSafeLinearVerifierStepKind.LoopText, 0, text, null, null, default, min, max, RegexOptions.None);

    public static Utf8ByteSafeLinearVerifierStep LoopAsciiClass(AsciiCharClass asciiCharClass, int min, int max) =>
        new(Utf8ByteSafeLinearVerifierStepKind.LoopAsciiClass, 0, null, null, null, asciiCharClass, min, max, RegexOptions.None);

    public static Utf8ByteSafeLinearVerifierStep RequireBeginning(RegexOptions options) =>
        new(Utf8ByteSafeLinearVerifierStepKind.RequireBeginning, 0, null, null, null, default, 0, 0, options);

    public static Utf8ByteSafeLinearVerifierStep RequireEnd(RegexOptions options) =>
        new(Utf8ByteSafeLinearVerifierStepKind.RequireEnd, 0, null, null, null, default, 0, 0, options);

    internal static Utf8ByteSafeLinearVerifierStep RequireBeginning(Utf8ExecutionNodeKind kind, RegexOptions options) =>
        new(Utf8ByteSafeLinearVerifierStepKind.RequireBeginning, (byte)kind, null, null, null, default, 0, 0, options);

    internal static Utf8ByteSafeLinearVerifierStep RequireEnd(Utf8ExecutionNodeKind kind, RegexOptions options) =>
        new(Utf8ByteSafeLinearVerifierStepKind.RequireEnd, (byte)kind, null, null, null, default, 0, 0, options);

    public static Utf8ByteSafeLinearVerifierStep RequireBoundary() =>
        CreateMarker(Utf8ByteSafeLinearVerifierStepKind.RequireBoundary);

    public static Utf8ByteSafeLinearVerifierStep RequireNonBoundary() =>
        CreateMarker(Utf8ByteSafeLinearVerifierStepKind.RequireNonBoundary);

    public static Utf8ByteSafeLinearVerifierStep MatchAnyText(byte[][] alternatives) =>
        new(Utf8ByteSafeLinearVerifierStepKind.MatchAnyText, 0, null, alternatives, null, default, 0, 0, RegexOptions.None);

    public static Utf8ByteSafeLinearVerifierStep MatchAnyTextOptional(byte[][] alternatives) =>
        new(Utf8ByteSafeLinearVerifierStepKind.MatchAnyTextOptional, 0, null, alternatives, null, default, 0, 0, RegexOptions.None);

    public static Utf8ByteSafeLinearVerifierStep LoopAnyText(byte[][] alternatives, int min, int max) =>
        new(Utf8ByteSafeLinearVerifierStepKind.LoopAnyText, 0, null, alternatives, null, default, min, max, RegexOptions.None);

    public static Utf8ByteSafeLinearVerifierStep LoopProgram(Utf8ByteSafeLinearVerifierStep[] program, int min, int max) =>
        new(Utf8ByteSafeLinearVerifierStepKind.LoopProgram, 0, null, null, program, default, min, max, RegexOptions.None);

    public static Utf8ByteSafeLinearVerifierStep Accept() =>
        CreateMarker(Utf8ByteSafeLinearVerifierStepKind.Accept);

    private static Utf8ByteSafeLinearVerifierStep CreateMarker(Utf8ByteSafeLinearVerifierStepKind kind) =>
        new(kind, 0, null, null, null, default, 0, 0, RegexOptions.None);
}

internal readonly partial record struct Utf8ByteSafeLinearCompileOutcome(
    Utf8ByteSafeLinearVerifierProgram Program,
    Utf8ByteSafeLinearCompileFailureKind FailureKind)
{
    public bool Succeeded => Program.HasValue;
}

internal readonly partial struct Utf8ByteSafeLinearVerifierProgram
{
    public Utf8ByteSafeLinearVerifierProgram(Utf8ByteSafeLinearVerifierStep[]? steps)
    {
        Steps = steps is { Length: > 0 } ? steps : [];
    }

    public Utf8ByteSafeLinearVerifierStep[] Steps { get; }

    public bool HasValue => Steps is { Length: > 0 };

    public static Utf8ByteSafeLinearCompileOutcome Compile(Utf8ExecutionTree? tree)
    {
        return Compiler.Compile(tree);
    }

    private struct Compiler
    {
        private Utf8ByteSafeLinearCompileFailureKind _failureKind;
        private readonly Utf8ConstructionMap<Utf8ByteSafeLinearVerifierStep[], int>? _minimumLengths;
        private readonly Utf8ConstructionBuffer<PayloadLoan>? _payloads;
        private readonly record struct PayloadLoan(Array Array, ulong Bytes);
        private Compiler(Utf8ConstructionMap<Utf8ByteSafeLinearVerifierStep[], int> minimumLengths, Utf8ConstructionBuffer<PayloadLoan> payloads)
        {
            _minimumLengths = minimumLengths; _payloads = payloads;
            _failureKind = Utf8ByteSafeLinearCompileFailureKind.None;
        }
        private enum AppendKind : byte { Node, Loop, SubProgram, TerminalLoop, SafeLoop, TerminalAlternation }
        private enum AppendAction : byte { Complete, Resume, Child }

        // Value continuations borrow admitted builders. Their final arrays and
        // text/alternative payloads transfer through the same construction owner.
        private struct AppendFrame(Utf8ExecutionNode node, Utf8ConstructionBuffer<Utf8ByteSafeLinearVerifierStep>? steps)
        {
            internal Utf8ExecutionNode Node = node;
            internal Utf8ConstructionBuffer<Utf8ByteSafeLinearVerifierStep>? Steps = steps;
            internal AppendKind Kind;
            internal int Index;
            internal int Stage;
            internal bool AllowVariableLength;
            internal bool Succeeded;
            internal Utf8ByteSafeLinearVerifierStep[] Program = [];
            internal int MinimumConsumedLength;
        }

        private readonly record struct AppendResult(bool Succeeded,
            Utf8ByteSafeLinearVerifierStep[] Program, int MinimumConsumedLength);

        public static Utf8ByteSafeLinearCompileOutcome Compile(Utf8ExecutionTree? tree)
        {
            if (tree?.Root is not { } root) return new(default, Utf8ByteSafeLinearCompileFailureKind.EmptyTree);
            using var construction = Utf8ConstructionScope.Enter(); var budget = construction.Budget;
            using var frames = Utf8ConstructionFrames<AppendFrame>.Create(budget, new(root, null) { AllowVariableLength = true });
            using var lengths = Utf8ConstructionMap<Utf8ByteSafeLinearVerifierStep[], int>.Create(budget, ReferenceEqualityComparer.Instance);
            using var builders = Utf8ConstructionBuffer<Utf8ConstructionBuffer<Utf8ByteSafeLinearVerifierStep>>.Create(budget);
            using var payloads = Utf8ConstructionBuffer<PayloadLoan>.Create(budget);
            var compiler = new Compiler(lengths, payloads);
            var last = new AppendResult(false, [], 0); Utf8ConstructionBuffer<Utf8ByteSafeLinearVerifierStep>? rootSteps = null;
            try
            {
                while (frames.Count != 0)
                {
                    budget.ChargeWork(1); var index = frames.Count - 1; var frame = frames[index];
                    AppendAction action; AppendFrame child;
                    try { action = compiler.Advance(ref frame, last, builders, out child); }
                    finally { frames[index] = frame; }
                    if (index == 0) rootSteps = frame.Steps;
                    if (action == AppendAction.Child) frames.Push(child);
                    else if (action == AppendAction.Complete)
                    { last = new(frame.Succeeded, frame.Program, frame.MinimumConsumedLength); frames.Pop(); }
                }
                if (!last.Succeeded || rootSteps is null)
                {
                    if (compiler._failureKind == Utf8ByteSafeLinearCompileFailureKind.None) compiler._failureKind = Utf8ByteSafeLinearCompileFailureKind.UnsupportedNode;
                    return new(default, compiler._failureKind);
                }
                rootSteps.Add(Utf8ByteSafeLinearVerifierStep.Accept());
                var steps = rootSteps.Take(); compiler.Track(steps);
                // Drop memo references before releasing collapsed or speculative
                // payloads. Only arrays reachable from the final graph retain loans.
                lengths.Dispose(); compiler.ReleaseUnretainedPayloads(steps);
                construction.Complete(); return new(new(steps), compiler._failureKind);
            }
            finally
            {
                foreach (var builder in builders) builder.Dispose();
            }
        }

        private AppendAction Advance(ref AppendFrame frame, AppendResult last, Utf8ConstructionBuffer<Utf8ConstructionBuffer<Utf8ByteSafeLinearVerifierStep>> builders, out AppendFrame child)
        {
            if (frame.Steps is null)
            {
                frame.Steps = Utf8ConstructionBuffer<Utf8ByteSafeLinearVerifierStep>.Create(Utf8ConstructionScope.CurrentBudget);
                builders.Add(frame.Steps);
            }
            child = default;
            return frame.Kind switch
            {
                AppendKind.Node => AppendNode(ref frame, last, out child),
                AppendKind.Loop => AppendLoop(ref frame, last, out child),
                AppendKind.SubProgram => AppendSubProgram(ref frame, last, out child),
                AppendKind.TerminalLoop => AppendTerminalLoop(ref frame, last, out child),
                AppendKind.SafeLoop => AppendSafeVariableLoop(ref frame, last, out child),
                _ => AppendTerminalAlternation(ref frame, last, out child),
            };
        }

    private AppendAction AppendNode(ref AppendFrame frame, AppendResult last, out AppendFrame pending)
    {
        pending = default;
        var node = frame.Node;
        var steps = frame.Steps ?? throw new InvalidOperationException();
        switch (node.Kind)
        {
            case Utf8ExecutionNodeKind.Empty:
                frame.Succeeded = true;
                return AppendAction.Complete;
            case Utf8ExecutionNodeKind.Capture:
            case Utf8ExecutionNodeKind.Group:
            case Utf8ExecutionNodeKind.Atomic:
                if (node.Children.Count != 1) return AppendAction.Complete;
                if (frame.Stage == 0)
                {
                    frame.Stage = 1;
                    pending = new(node.Children[0], steps) { AllowVariableLength = frame.AllowVariableLength };
                    return AppendAction.Child;
                }
                frame.Succeeded = last.Succeeded; return AppendAction.Complete;
            case Utf8ExecutionNodeKind.Concatenate:
                if (frame.Stage == 1)
                {
                    if (last.Succeeded) { frame.Succeeded = true; return AppendAction.Complete; }
                    frame.Stage = 2;
                    pending = new(node, steps) { Kind = AppendKind.SafeLoop, Index = frame.Index };
                    return AppendAction.Child;
                }
                if (frame.Stage == 2)
                {
                    if (last.Succeeded) { frame.Index++; frame.Stage = 0; return AppendAction.Resume; }
                    frame.Stage = 3;
                    pending = new(node, steps) { Kind = AppendKind.TerminalAlternation, Index = frame.Index };
                    return AppendAction.Child;
                }
                if (frame.Stage == 3)
                {
                    if (last.Succeeded) { frame.Succeeded = true; return AppendAction.Complete; }
                    var child = node.Children[frame.Index];
                    if (TryAppendDeterministicOptionalAlternation(node.Children, frame.Index, steps) ||
                        TryAppendDeterministicAlternation(child, steps))
                    {
                        frame.Index++; frame.Stage = 0;
                        return AppendAction.Resume;
                    }
                    frame.Stage = 4;
                    // Earlier phases prove terminal/disjoint variable loops.
                    // An ordinary child cannot silently relax that requirement.
                    pending = new(child, steps);
                    return AppendAction.Child;
                }
                if (frame.Stage == 4)
                {
                    if (!last.Succeeded) return AppendAction.Complete;
                    frame.Index++; frame.Stage = 0;
                    return AppendAction.Resume;
                }
                if (frame.Index == node.Children.Count)
                {
                    frame.Succeeded = true;
                    return AppendAction.Complete;
                }
                frame.Stage = 1;
                pending = new(node, steps) { Kind = AppendKind.TerminalLoop, Index = frame.Index };
                return AppendAction.Child;
            case Utf8ExecutionNodeKind.One when IsExactAsciiScalar(node.Ch, node):
                steps.Add(Utf8ByteSafeLinearVerifierStep.MatchByte((byte)node.Ch));
                break;
            case Utf8ExecutionNodeKind.Multi when TryGetExactAsciiBytes(node, out var text):
                Track(text); steps.Add(Utf8ByteSafeLinearVerifierStep.MatchText(text));
                break;
            case Utf8ExecutionNodeKind.Set when node.ScalarClass is { } set && TryCreateByteSafeSetStep(set, false, 0, 0, out var setStep):
                steps.Add(setStep);
                break;
            case Utf8ExecutionNodeKind.Bol:
            case Utf8ExecutionNodeKind.Beginning:
            case Utf8ExecutionNodeKind.Start:
                steps.Add(Utf8ByteSafeLinearVerifierStep.RequireBeginning(node.Kind, node.Options));
                break;
            case Utf8ExecutionNodeKind.Eol:
            case Utf8ExecutionNodeKind.EndZ:
            case Utf8ExecutionNodeKind.End:
                steps.Add(Utf8ByteSafeLinearVerifierStep.RequireEnd(node.Kind, node.Options));
                break;
            case Utf8ExecutionNodeKind.Boundary:
                steps.Add(Utf8ByteSafeLinearVerifierStep.RequireBoundary());
                break;
            case Utf8ExecutionNodeKind.NonBoundary:
                steps.Add(Utf8ByteSafeLinearVerifierStep.RequireNonBoundary());
                break;
            case Utf8ExecutionNodeKind.Loop:
            case Utf8ExecutionNodeKind.LazyLoop:
                // The node/loop delegation is an in-place phase, not an extra
                // suspended frame. Nested subprogram bodies still admit depth.
                frame.Kind = AppendKind.Loop;
                return AppendAction.Resume;
            default:
                if (_failureKind == Utf8ByteSafeLinearCompileFailureKind.None)
                    _failureKind = Utf8ByteSafeLinearCompileFailureKind.UnsupportedNode;
                return AppendAction.Complete;
        }
        frame.Succeeded = true;
        return AppendAction.Complete;
    }

    private AppendAction AppendLoop(ref AppendFrame frame, AppendResult last, out AppendFrame pending)
    {
        pending = default;
        if (frame.Stage == 1)
        {
            if (!last.Succeeded) return AppendAction.Complete;
            if (last.MinimumConsumedLength == 0)
            {
                if (_failureKind == Utf8ByteSafeLinearCompileFailureKind.None)
                    _failureKind = Utf8ByteSafeLinearCompileFailureKind.NestedSubProgram;
                return AppendAction.Complete;
            }
            var output = frame.Steps ?? throw new InvalidOperationException();
            output.Add(TryCombineLoopSubProgram(last.Program, frame.Node.Min, frame.Node.Max, out var combined)
                ? combined : Utf8ByteSafeLinearVerifierStep.LoopProgram(last.Program, frame.Node.Min, frame.Node.Max));
            frame.Succeeded = true;
            return AppendAction.Complete;
        }
        var node = frame.Node;
        var steps = frame.Steps ?? throw new InvalidOperationException();
        var allowVariableLength = frame.AllowVariableLength;
        if (node.Kind == Utf8ExecutionNodeKind.LazyLoop && node.Min != node.Max)
        {
            if (_failureKind == Utf8ByteSafeLinearCompileFailureKind.None)
            {
                _failureKind = Utf8ByteSafeLinearCompileFailureKind.UnsupportedLoop;
            }
            return AppendAction.Complete;
        }

        if (!allowVariableLength && node.Min != node.Max)
        {
            if (_failureKind == Utf8ByteSafeLinearCompileFailureKind.None)
            {
                _failureKind = Utf8ByteSafeLinearCompileFailureKind.UnsupportedLoop;
            }
            return AppendAction.Complete;
        }

        if (node.Children.Count == 1 && TryExtractAlternationTexts(node.Children[0], out var alternatives, out var optional))
        {
            TrackAlternatives(alternatives);
            if (!optional && AreAlternativesPrefixFree(alternatives) && (node.Min == node.Max || allowVariableLength))
            {
                steps.Add(Utf8ByteSafeLinearVerifierStep.LoopAnyText(alternatives, node.Min, node.Max));
                frame.Succeeded = true; return AppendAction.Complete;
            }
        }

        if (node.Ch != 0 && IsExactAsciiScalar(node.Ch, node))
        {
            steps.Add(Utf8ByteSafeLinearVerifierStep.LoopByte((byte)node.Ch, node.Min, node.Max));
            frame.Succeeded = true;
            return AppendAction.Complete;
        }

        if (node.ScalarClass is { } loopSet && TryCreateByteSafeSetStep(loopSet, loop: true, node.Min, node.Max, out var loopSetStep))
        {
            steps.Add(loopSetStep);
            frame.Succeeded = true;
            return AppendAction.Complete;
        }

        if (node.ScalarClass is null && TryGetExactAsciiBytes(node, out var loopText))
        {
            Track(loopText); steps.Add(Utf8ByteSafeLinearVerifierStep.LoopText(loopText, node.Min, node.Max));
            frame.Succeeded = true;
            return AppendAction.Complete;
        }

        if (node.Children.Count != 1)
        {
            return AppendAction.Complete;
        }

        var child = PeekContent(node.Children[0]);
        switch (child.Kind)
        {
            case Utf8ExecutionNodeKind.One when IsExactAsciiScalar(child.Ch, child):
                steps.Add(Utf8ByteSafeLinearVerifierStep.LoopByte((byte)child.Ch, node.Min, node.Max));
                break;
            case Utf8ExecutionNodeKind.Multi when TryGetExactAsciiBytes(child, out var text):
                Track(text); steps.Add(Utf8ByteSafeLinearVerifierStep.LoopText(text, node.Min, node.Max));
                break;
            case Utf8ExecutionNodeKind.Set when child.ScalarClass is { } set && TryCreateByteSafeSetStep(set, true, node.Min, node.Max, out var setStep):
                steps.Add(setStep);
                break;
            case Utf8ExecutionNodeKind.Concatenate:
            case Utf8ExecutionNodeKind.Capture:
            case Utf8ExecutionNodeKind.Group:
            case Utf8ExecutionNodeKind.Atomic:
            case Utf8ExecutionNodeKind.Loop:
            case Utf8ExecutionNodeKind.LazyLoop:
                frame.Stage = 1;
                pending = new(node.Children[0], null) { Kind = AppendKind.SubProgram };
                return AppendAction.Child;
            default:
                if (_failureKind == Utf8ByteSafeLinearCompileFailureKind.None)
                    _failureKind = Utf8ByteSafeLinearCompileFailureKind.UnsupportedLoop;
                return AppendAction.Complete;
        }
        frame.Succeeded = true;
        return AppendAction.Complete;
    }

    private AppendAction AppendSubProgram(ref AppendFrame frame, AppendResult last, out AppendFrame pending)
    {
        pending = default;
        if (frame.Stage == 0)
        {
            frame.Stage = 1;
            pending = new(frame.Node, frame.Steps) { AllowVariableLength = true };
            return AppendAction.Child;
        }
        if (!last.Succeeded)
        {
            if (_failureKind == Utf8ByteSafeLinearCompileFailureKind.None)
                _failureKind = Utf8ByteSafeLinearCompileFailureKind.NestedSubProgram;
            return AppendAction.Complete;
        }
        frame.Program = (frame.Steps ?? throw new InvalidOperationException()).Take();
        Track(frame.Program);
        frame.MinimumConsumedLength = GetMinimumConsumedLength(frame.Program);
        frame.Succeeded = frame.Program.Length > 0;
        return AppendAction.Complete;
    }

    private bool TryCombineLoopSubProgram(
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
            Utf8ByteSafeLinearVerifierStepKind.LoopAsciiClass => (step.Kind, step.Min, step.Max),
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
                Utf8ByteSafeLinearVerifierStep.LoopText(step.Text, combinedMin, combinedMax),
            Utf8ByteSafeLinearVerifierStepKind.LoopAsciiClass =>
                Utf8ByteSafeLinearVerifierStep.LoopAsciiClass(step.CharClass, combinedMin, combinedMax),
            Utf8ByteSafeLinearVerifierStepKind.LoopAnyText =>
                Utf8ByteSafeLinearVerifierStep.LoopAnyText(step.Alternatives, combinedMin, combinedMax),
            _ => default,
        };

        return combinedStep.Kind != Utf8ByteSafeLinearVerifierStepKind.MatchByte;
    }

    private int CombineLoopBounds(int outer, int inner)
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

    private AppendAction AppendTerminalLoop(ref AppendFrame frame, AppendResult last, out AppendFrame pending)
    {
        pending = default;
        var children = frame.Node.Children;
        if (frame.Stage == 0)
        {
            var node = PeekContent(children[frame.Index]);
            if (node.Kind is not (Utf8ExecutionNodeKind.Loop or Utf8ExecutionNodeKind.LazyLoop) ||
                !CanTreatRemainingAsZeroWidth(children, frame.Index + 1)) return AppendAction.Complete;
            if (node.Min != node.Max && !Utf8ByteSafeContinuationProof.AllowsGreedyChoice(children[frame.Index], children, frame.Index + 1, nullableSuffix: false))
                return AppendAction.Complete;
            frame.Stage = 1;
            pending = new(children[frame.Index], frame.Steps) { AllowVariableLength = true };
            return AppendAction.Child;
        }
        if (!last.Succeeded) return AppendAction.Complete;
        if (++frame.Index < children.Count)
        {
            pending = new(children[frame.Index], frame.Steps);
            return AppendAction.Child;
        }
        frame.Succeeded = true;
        return AppendAction.Complete;
    }

    private AppendAction AppendSafeVariableLoop(ref AppendFrame frame, AppendResult last, out AppendFrame pending)
    {
        pending = default;
        if (frame.Stage == 1)
        {
            frame.Succeeded = last.Succeeded;
            return AppendAction.Complete;
        }
        var children = frame.Node.Children;
        var node = PeekContent(children[frame.Index]);
        if (node.Kind != Utf8ExecutionNodeKind.Loop || node.Min == node.Max) return AppendAction.Complete;
        if (!TryGetLeadingAsciiByteMask(children[frame.Index], out var loopLow, out var loopHigh) ||
            !TryGetFollowingAsciiByteFacts(children, frame.Index + 1, out var nextLow, out var nextHigh, out var nullableSuffix) ||
            !MasksAreDisjoint(loopLow, loopHigh, nextLow, nextHigh))
        {
            if (_failureKind == Utf8ByteSafeLinearCompileFailureKind.None)
                _failureKind = Utf8ByteSafeLinearCompileFailureKind.NonDisjointVariableLoop;
            return AppendAction.Complete;
        }
        // A nullable suffix can succeed at an earlier loop endpoint without
        // consuming any of its disjoint starts. Its assertions need a separate
        // proof; first-set disjointness alone does not make them irrevocable.
        if (nullableSuffix && !Utf8ByteSafeContinuationProof.AllowsGreedyChoice(children[frame.Index], children, frame.Index + 1, nullableSuffix: true))
            return AppendAction.Complete;
        frame.Stage = 1;
        pending = new(children[frame.Index], frame.Steps) { AllowVariableLength = true };
        return AppendAction.Child;
    }

    private bool TryCreateByteSafeSetStep(Utf8ScalarClass scalarClass, bool loop, int min, int max, out Utf8ByteSafeLinearVerifierStep step)
    {
        if (Utf8ScalarAsciiProjector.TryProjectAsciiIntersection(scalarClass, out var mask))
        {
            // Only the proved ASCII intersection executes here. A non-ASCII
            // encounter requests the authoritative scalar dispatcher.
            step = loop ? Utf8ByteSafeLinearVerifierStep.LoopAsciiClass(mask, min, max)
                : Utf8ByteSafeLinearVerifierStep.MatchAsciiClass(mask);
            return true;
        }
        step = default;
        return false;
    }

    private AppendAction AppendTerminalAlternation(ref AppendFrame frame, AppendResult last, out AppendFrame pending)
    {
        pending = default;
        var children = frame.Node.Children;
        if (frame.Stage == 0)
        {
            var node = PeekContent(children[frame.Index]);
            if (!CanTreatRemainingAsZeroWidth(children, frame.Index + 1))
            {
                if (node.Kind == Utf8ExecutionNodeKind.Alternate && _failureKind == Utf8ByteSafeLinearCompileFailureKind.None)
                    _failureKind = Utf8ByteSafeLinearCompileFailureKind.UnsupportedAlternation;
                return AppendAction.Complete;
            }
            if (!TryExtractAlternationTexts(children[frame.Index], out var alternatives, out var optional)) return AppendAction.Complete;
            TrackAlternatives(alternatives);
            // A zero-width suffix can reject the first matching arm. The linear
            // kernel cannot retry a longer arm that shares that arm's prefix.
            if (!AreAlternativesPrefixFree(alternatives)) return AppendAction.Complete;
            if (optional && !Utf8ByteSafeContinuationProof.AllowsGreedyChoice(children[frame.Index], children, frame.Index + 1, nullableSuffix: false))
                return AppendAction.Complete;
            var steps = frame.Steps ?? throw new InvalidOperationException();
            steps.Add(optional ? Utf8ByteSafeLinearVerifierStep.MatchAnyTextOptional(alternatives)
                : Utf8ByteSafeLinearVerifierStep.MatchAnyText(alternatives));
            frame.Stage = 1;
        }
        else if (!last.Succeeded) return AppendAction.Complete;
        if (++frame.Index < children.Count)
        {
            pending = new(children[frame.Index], frame.Steps);
            return AppendAction.Child;
        }
        frame.Succeeded = true;
        return AppendAction.Complete;
    }

    private bool TryAppendDeterministicOptionalAlternation(IReadOnlyList<Utf8ExecutionNode> children, int index, Utf8ConstructionBuffer<Utf8ByteSafeLinearVerifierStep> steps)
    {
        var node = PeekContent(children[index]);
        if (TryExtractAlternationTexts(children[index], out var alternatives, out var optional))
        {
            TrackAlternatives(alternatives);
            if (optional && AreAlternativesPrefixFree(alternatives) && TryExtractFollowingAsciiPrefix(children, index + 1, out var followingPrefix))
            {
                Track(followingPrefix);
                if (IsSafeOptionalAlternation(alternatives, followingPrefix))
                { steps.Add(Utf8ByteSafeLinearVerifierStep.MatchAnyTextOptional(alternatives)); return true; }
            }
        }
        if (node.Kind == Utf8ExecutionNodeKind.Alternate && _failureKind == Utf8ByteSafeLinearCompileFailureKind.None)
            _failureKind = Utf8ByteSafeLinearCompileFailureKind.UnsupportedAlternation;
        return false;
    }

    private bool TryAppendDeterministicAlternation(Utf8ExecutionNode node, Utf8ConstructionBuffer<Utf8ByteSafeLinearVerifierStep> steps)
    {
        if (TryExtractAlternationTexts(node, out var alternatives, out var optional))
        {
            TrackAlternatives(alternatives);
            if (!optional && AreAlternativesPrefixFree(alternatives))
            { steps.Add(Utf8ByteSafeLinearVerifierStep.MatchAnyText(alternatives)); return true; }
        }
        if (node.Kind == Utf8ExecutionNodeKind.Alternate && _failureKind == Utf8ByteSafeLinearCompileFailureKind.None)
            _failureKind = Utf8ByteSafeLinearCompileFailureKind.UnsupportedAlternation;
        return false;
    }

    private readonly record struct FollowingFrame(IReadOnlyList<Utf8ExecutionNode> Children, int Index);
    private struct TextFrame(Utf8ExecutionNode node)
    {
        internal Utf8ExecutionNode Node = node;
        internal int Index;
    }

    private bool CanTreatRemainingAsZeroWidth(IReadOnlyList<Utf8ExecutionNode> children, int startIndex)
    {
        using var scope = Utf8ConstructionScope.Enter();
        using var cursor = Utf8ConstructionFrames<FollowingFrame>.Create(scope.Budget, new(children, startIndex));
        for (var index = startIndex; index < children.Count; index++)
        {
            scope.Budget.ChargeWork(1);
            cursor[0] = new(children, index);
            if (!IsFixedZeroWidth(children[index])) return false;
        }
        return true;
    }

    private static bool IsFixedZeroWidth(Utf8ExecutionNode root)
    {
        using var scope = Utf8ConstructionScope.Enter();
        using var frames = Utf8ConstructionFrames<TextFrame>.Create(scope.Budget, new(root));
        while (frames.Count != 0)
        {
            scope.Budget.ChargeWork(1);
            var index = frames.Count - 1; var frame = frames[index]; var node = frame.Node;
            if (Utf8ExecutionPrefixFacts.Transparent(node) && node.Children.Count == 1 || node.Kind == Utf8ExecutionNodeKind.Concatenate)
            {
                if (frame.Index == node.Children.Count) { frames.Pop(); continue; }
                frames[index] = new(node) { Index = frame.Index + 1 };
                frames.Push(new(node.Children[frame.Index])); continue;
            }
            // This compiler's supported zero-width vocabulary excludes assertions
            // it cannot emit; a width proof alone cannot erase their conditions.
            if (!Utf8DeterministicAnchorSearch.IsZeroWidthNode(node)) return false;
            frames.Pop();
        }
        return true;
    }

    private bool TryExtractAlternationTexts(Utf8ExecutionNode node, out byte[][] alternatives, out bool optional)
    {
        alternatives = []; optional = false;
        using var scope = Utf8ConstructionScope.Enter(); var budget = scope.Budget;
        using var frames = Utf8ConstructionFrames<TextFrame>.Create(budget, new(node));
        var sawOptional = false;
        while (Utf8ExecutionPrefixFacts.Transparent(node) && node.Children.Count == 1 ||
               node.Kind == Utf8ExecutionNodeKind.Loop && node.Min == 0 && node.Max == 1 && node.Children.Count == 1)
        {
            budget.ChargeWork(1); sawOptional |= node.Kind == Utf8ExecutionNodeKind.Loop;
            node = node.Children[0]; frames.Push(new(node));
        }
        budget.ChargeWork(1);
        if (node.Kind != Utf8ExecutionNodeKind.Alternate || node.Children.Count == 0) return false;
        using var values = Utf8ConstructionBuffer<byte[]>.Create(budget);
        var index = frames.Count - 1;
        while (frames[index].Index < node.Children.Count)
        {
            budget.ChargeWork(1); var frame = frames[index]; frames[index] = new(node) { Index = frame.Index + 1 };
            if (!TryExtractFixedAsciiText(node.Children[frame.Index], out var value))
            {
                if (_failureKind == Utf8ByteSafeLinearCompileFailureKind.None) _failureKind = Utf8ByteSafeLinearCompileFailureKind.UnsupportedFixedText;
                return false;
            }
            values.Add(value);
        }
        alternatives = values.Take(); optional = sawOptional;
        scope.Complete(); return true;
    }

    private bool TryExtractFollowingAsciiPrefix(IReadOnlyList<Utf8ExecutionNode> children, int startIndex, out byte[] prefix)
    {
        prefix = [];
        using var scope = Utf8ConstructionScope.Enter();
        using var cursor = Utf8ConstructionFrames<FollowingFrame>.Create(scope.Budget, new(children, startIndex));
        for (var index = startIndex; index < children.Count; index++)
        {
            scope.Budget.ChargeWork(1); cursor[0] = new(children, index);
            if (IsFixedZeroWidth(children[index])) continue;
            if (!TryExtractFixedAsciiText(children[index], out var value) || value.Length == 0)
            {
                if (_failureKind == Utf8ByteSafeLinearCompileFailureKind.None) _failureKind = Utf8ByteSafeLinearCompileFailureKind.UnsupportedFollowingPrefix;
                return false;
            }
            prefix = value; scope.Complete(); return true;
        }
        return false;
    }

    private bool TryGetFollowingAsciiByteMask(IReadOnlyList<Utf8ExecutionNode> children, int startIndex, out ulong lowMask, out ulong highMask)
        => TryGetFollowingAsciiByteFacts(children, startIndex, out lowMask, out highMask, out _);

    private bool TryGetFollowingAsciiByteFacts(IReadOnlyList<Utf8ExecutionNode> children, int startIndex, out ulong lowMask, out ulong highMask, out bool canBeEmpty)
    {
        lowMask = highMask = 0; canBeEmpty = false;
        using var scope = Utf8ConstructionScope.Enter();
        using var cursor = Utf8ConstructionFrames<FollowingFrame>.Create(scope.Budget, new(children, startIndex));
        var mask = default(AsciiCharClass);
        for (var index = startIndex; index < children.Count; index++)
        {
            scope.Budget.ChargeWork(1); cursor[0] = new(children, index);
            if (!Utf8ExecutionPrefixFacts.TryFirstMask(children[index], out var first, out var nullable, asciiOnly: true)) return false;
            mask = new(mask.ActualLowMask | first.ActualLowMask, mask.ActualHighMask | first.ActualHighMask);
            if (nullable) continue;
            if (mask.IsEmpty) return false;
            lowMask = mask.ActualLowMask; highMask = mask.ActualHighMask; return true;
        }
        // A wholly nullable suffix can finish without consuming another byte;
        // all its possible nonempty starts still participate in disjointness.
        if (!mask.IsEmpty)
        { lowMask = mask.ActualLowMask; highMask = mask.ActualHighMask; canBeEmpty = true; return true; }
        if (_failureKind == Utf8ByteSafeLinearCompileFailureKind.None) _failureKind = Utf8ByteSafeLinearCompileFailureKind.UnsupportedFollowingMask;
        return false;
    }

    private bool TryExtractFixedAsciiText(Utf8ExecutionNode root, out byte[] text)
    {
        text = [];
        using var scope = Utf8ConstructionScope.Enter(); var budget = scope.Budget;
        using var frames = Utf8ConstructionFrames<TextFrame>.Create(budget, new(root));
        using var bytes = Utf8ConstructionBuffer<byte>.Create(budget);
        while (frames.Count != 0)
        {
            budget.ChargeWork(1);
            var index = frames.Count - 1; var frame = frames[index]; var node = frame.Node;
            if (Utf8ExecutionPrefixFacts.Transparent(node) && node.Children.Count == 1 || node.Kind == Utf8ExecutionNodeKind.Concatenate)
            {
                if (frame.Index == node.Children.Count) { frames.Pop(); continue; }
                frames[index] = new(node) { Index = frame.Index + 1 };
                frames.Push(new(node.Children[frame.Index])); continue;
            }
            var proved = node.Kind == Utf8ExecutionNodeKind.Empty;
            if (node.Kind == Utf8ExecutionNodeKind.One) proved = Append(node.Ch, node);
            else if (node.Kind == Utf8ExecutionNodeKind.Multi)
            {
                budget.ChargeWork((ulong)node.Text.Length); proved = true;
                foreach (var value in node.Text) if (!Append(value, node)) { proved = false; break; }
            }
            if (!proved)
            {
                if (_failureKind == Utf8ByteSafeLinearCompileFailureKind.None) _failureKind = Utf8ByteSafeLinearCompileFailureKind.UnsupportedFixedText;
                return false;
            }
            frames.Pop();
        }
        text = bytes.Take(); scope.Complete(); return true;

        bool Append(char value, Utf8ExecutionNode node)
        {
            budget.ChargeWork(1);
            if (value > 127 || Utf8ExecutionPrefixFacts.Folded(node) &&
                (!Utf8ExecutionPrefixFacts.ScalarMask(value, node, out var mask) || mask.Count != 1 || !mask.Contains((byte)value))) return false;
            bytes.Add((byte)value); return true;
        }
    }

    private bool AreAlternativesPrefixFree(byte[][] alternatives)
    {
        for (var i = 0; i < alternatives.Length; i++)
        {
            Utf8ConstructionScope.ChargeWork(1);
            var current = alternatives[i];
            for (var j = 0; j < alternatives.Length; j++)
            {
                Utf8ConstructionScope.ChargeWork(1);
                if (i == j)
                {
                    continue;
                }

                var other = alternatives[j];
                if (current.Length <= other.Length &&
                    SamePrefix(other, current))
                {
                    return false;
                }
            }
        }

        return true;
    }

    private bool IsSafeOptionalAlternation(byte[][] alternatives, byte[] followingPrefix)
    {
        for (var i = 0; i < alternatives.Length; i++)
        {
            Utf8ConstructionScope.ChargeWork(1);
            var alternative = alternatives[i];
            if (alternative.Length == 0)
            {
                return false;
            }

            // Either prefix relation can require skipping the optional arm:
            // both "ab" before "abc" and "abc" before "ab" can mask the
            // shorter successful path when the greedy continuation fails.
            if (alternative.Length <= followingPrefix.Length ? SamePrefix(followingPrefix, alternative)
                : SamePrefix(alternative, followingPrefix))
            {
                return false;
            }
        }

        return true;
    }


    private static bool SamePrefix(byte[] value, byte[] prefix)
    {
        Utf8ConstructionScope.ChargeWork((ulong)prefix.Length);
        return value.AsSpan(0, prefix.Length).SequenceEqual(prefix);
    }

    private bool TryGetLeadingAsciiByteMask(Utf8ExecutionNode node, out ulong lowMask, out ulong highMask)
    {
        var proved = Utf8ExecutionPrefixFacts.TryFirstMask(node, out var mask, out _, asciiOnly: true);
        lowMask = proved ? mask.ActualLowMask : 0; highMask = proved ? mask.ActualHighMask : 0;
        return proved && !mask.IsEmpty;
    }

    private bool MasksAreDisjoint(
        ulong firstLowMask,
        ulong firstHighMask,
        ulong secondLowMask,
        ulong secondHighMask)
    {
        return (firstLowMask & secondLowMask) == 0 &&
               (firstHighMask & secondHighMask) == 0;
    }

    private static bool IsExactAsciiScalar(char value, Utf8ExecutionNode node) => value < 128 &&
        (!Utf8ExecutionPrefixFacts.Folded(node) || Utf8ExecutionPrefixFacts.ScalarMask(value, node, out var mask) && mask.Count == 1 && mask.Contains((byte)value));

    private bool TryGetExactAsciiBytes(Utf8ExecutionNode node, out byte[] bytes)
    {
        bytes = [];
        if (node.Text.Length == 0) return false;
        return Utf8ExecutionPrefixFacts.Folded(node) ? TryExtractFixedAsciiText(node, out bytes)
            : Utf8DeterministicAnchorSearch.TryGetAsciiBytes(node.Text, out bytes);
    }

    private static Utf8ExecutionNode PeekContent(Utf8ExecutionNode node)
    {
        using var scope = Utf8ConstructionScope.Enter();
        using var frames = Utf8ConstructionFrames<Utf8ExecutionNode>.Create(scope.Budget, node);
        while (Utf8ExecutionPrefixFacts.Transparent(node) && node.Children.Count == 1)
        { scope.Budget.ChargeWork(1); node = node.Children[0]; frames.Push(node); }
        return node;
    }

    // Track only after nested producers return. Mutating a borrowed registry
    // under a speculative child's rollback checkpoint would corrupt its loans.
    private void Track<T>(T[] array)
    {
        if (array.Length != 0 && _payloads is { } payloads)
            payloads.Add(new(array, Utf8ConstructionBudget.ArrayBytes<T>(array.Length)));
    }
    private void TrackAlternatives(byte[][] alternatives)
    {
        Track(alternatives);
        foreach (var value in alternatives) { Utf8ConstructionScope.ChargeWork(1); Track(value); }
    }

    private struct PayloadFrame(Array value)
    {
        internal readonly Array Value = value;
        internal int Index;
        internal int Stage;
    }
    private void ReleaseUnretainedPayloads(Utf8ByteSafeLinearVerifierStep[] root)
    {
        var budget = Utf8ConstructionScope.CurrentBudget;
        using var frames = Utf8ConstructionFrames<PayloadFrame>.Create(budget, new(root));
        using var live = Utf8ConstructionMap<Array, bool>.Create(budget, ReferenceEqualityComparer.Instance);
        while (frames.Count != 0)
        {
            budget.ChargeWork(1); var index = frames.Count - 1; var frame = frames[index];
            if (frame.Index == 0 && frame.Stage == 0)
            {
                if (live.ContainsKey(frame.Value)) { frames.Pop(); continue; }
                live[frame.Value] = true;
            }
            Array child;
            if (frame.Value is Utf8ByteSafeLinearVerifierStep[] steps)
            {
                if (frame.Index == steps.Length) { frames.Pop(); continue; }
                var step = steps[frame.Index];
                child = frame.Stage switch { 0 => step.Program, 1 => step.Text, _ => step.Alternatives };
                if (++frame.Stage == 3) { frame.Stage = 0; frame.Index++; }
            }
            else if (frame.Value is byte[][] alternatives)
            {
                if (frame.Index == alternatives.Length) { frames.Pop(); continue; }
                child = alternatives[frame.Index++];
            }
            else { frames.Pop(); continue; }
            frames[index] = frame;
            if (child.Length != 0) frames.Push(new(child));
        }
        if (_payloads is not { } payloads) throw new InvalidOperationException();
        for (var index = 0; index < payloads.Count; index++)
        {
            budget.ChargeWork(1); var loan = payloads[index]; payloads[index] = default;
            if (!live.ContainsKey(loan.Array)) budget.ReleaseBytes(loan.Bytes);
        }
        payloads.Clear();
    }

    private static int MinimumAlternativeByteLength(byte[][] alternatives)
    {
        if (alternatives.Length == 0) throw new InvalidOperationException("The alternative family is empty.");
        var minimum = int.MaxValue;
        foreach (var value in alternatives)
        { Utf8ConstructionScope.ChargeWork(1); minimum = Math.Min(minimum, value.Length); }
        return minimum;
    }

    private struct MinimumFrame(Utf8ByteSafeLinearVerifierStep[] steps)
    {
        internal readonly Utf8ByteSafeLinearVerifierStep[] Steps = steps;
        internal int Index;
        internal int Length;
    }

    private int GetMinimumConsumedLength(Utf8ByteSafeLinearVerifierStep[] steps)
    {
        // A borrowed memo belongs to Compile's transaction: don't roll back a
        // nested scope underneath its updated arrays. Standalone private probes
        // acquire and release an independent memo in their own transaction.
        using var local = _minimumLengths is null ? Utf8ConstructionScope.Enter() : null;
        var budget = Utf8ConstructionScope.CurrentBudget;
        using var frames = Utf8ConstructionFrames<MinimumFrame>.Create(budget, new(steps));
        using var owned = _minimumLengths is null
            ? Utf8ConstructionMap<Utf8ByteSafeLinearVerifierStep[], int>.Create(budget, ReferenceEqualityComparer.Instance) : null;
        var lengths = _minimumLengths ?? owned ?? throw new InvalidOperationException();
        while (frames.Count != 0)
        {
            budget.ChargeWork(1);
            var index = frames.Count - 1;
            var frame = frames[index];
            if (frame.Index == frame.Steps.Length)
            {
                lengths[frame.Steps] = frame.Length;
                frames.Pop();
                continue;
            }
            var step = frame.Steps[frame.Index];
            if (step.Kind == Utf8ByteSafeLinearVerifierStepKind.LoopProgram && !lengths.ContainsKey(step.Program))
            {
                frames.Push(new(step.Program));
                continue;
            }
            checked
            {
                frame.Length += step.Kind switch
                {
                    Utf8ByteSafeLinearVerifierStepKind.MatchByte => 1,
                    Utf8ByteSafeLinearVerifierStepKind.MatchText => step.Text.Length,
                    Utf8ByteSafeLinearVerifierStepKind.MatchAsciiClass => 1,
                    Utf8ByteSafeLinearVerifierStepKind.LoopByte => step.Min,
                    Utf8ByteSafeLinearVerifierStepKind.LoopText => step.Min * step.Text.Length,
                    Utf8ByteSafeLinearVerifierStepKind.LoopAsciiClass => step.Min,
                    Utf8ByteSafeLinearVerifierStepKind.MatchAnyText => MinimumAlternativeByteLength(step.Alternatives),
                    Utf8ByteSafeLinearVerifierStepKind.MatchAnyTextOptional => 0,
                    Utf8ByteSafeLinearVerifierStepKind.LoopAnyText => step.Min * MinimumAlternativeByteLength(step.Alternatives),
                    Utf8ByteSafeLinearVerifierStepKind.LoopProgram => step.Min * lengths[step.Program],
                    _ => 0,
                };
            }
            frame.Index++;
            frames[index] = frame;
        }
        return lengths[steps];
    }

    }

}
