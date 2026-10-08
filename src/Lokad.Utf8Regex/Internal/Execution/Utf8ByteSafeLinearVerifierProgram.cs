using Lokad.Utf8Regex.Internal.FrontEnd;

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
    private readonly byte[]? _text;
    private readonly byte[][]? _alternatives;
    private readonly Utf8ByteSafeLinearVerifierStep[]? _program;
    private readonly AsciiCharClass _projectedAsciiCharClass;
    private readonly string? _set;

    private Utf8ByteSafeLinearVerifierStep(
        Utf8ByteSafeLinearVerifierStepKind kind,
        byte value,
        byte[]? text,
        byte[][]? alternatives,
        Utf8ByteSafeLinearVerifierStep[]? program,
        AsciiCharClass projectedAsciiCharClass,
        string? set,
        int min,
        int max,
        RegexOptions options)
    {
        Kind = kind;
        Value = value;
        _text = text;
        _alternatives = alternatives;
        _program = program;
        _projectedAsciiCharClass = projectedAsciiCharClass;
        _set = set;
        Min = min;
        Max = max;
        Options = options;
    }

    public Utf8ByteSafeLinearVerifierStepKind Kind { get; }

    public byte Value { get; }

    public byte[] Text => _text ?? [];

    public byte[][] Alternatives => _alternatives ?? [];

    public Utf8ByteSafeLinearVerifierStep[] Program => _program ?? [];

    public AsciiCharClass ProjectedAsciiCharClass => _projectedAsciiCharClass;

    public string Set => _set ?? string.Empty;

    public int Min { get; }

    public int Max { get; }

    public RegexOptions Options { get; }

    internal Utf8ExecutionNodeKind AnchorKind => Value != 0 ? (Utf8ExecutionNodeKind)Value
        : Kind == Utf8ByteSafeLinearVerifierStepKind.RequireBeginning ? Utf8ExecutionNodeKind.Bol : Utf8ExecutionNodeKind.Eol;

    public static Utf8ByteSafeLinearVerifierStep MatchByte(byte value) =>
        new(Utf8ByteSafeLinearVerifierStepKind.MatchByte, value, null, null, null, default, null, 0, 0, RegexOptions.None);

    public static Utf8ByteSafeLinearVerifierStep MatchText(byte[] text) =>
        new(Utf8ByteSafeLinearVerifierStepKind.MatchText, 0, text, null, null, default, null, 0, 0, RegexOptions.None);

    public static Utf8ByteSafeLinearVerifierStep MatchSet(string set) =>
        new(Utf8ByteSafeLinearVerifierStepKind.MatchSet, 0, null, null, null, default, set, 0, 0, RegexOptions.None);

    public static Utf8ByteSafeLinearVerifierStep MatchProjectedAsciiSet(string set, AsciiCharClass asciiCharClass) =>
        new(Utf8ByteSafeLinearVerifierStepKind.MatchProjectedAsciiSet, 0, null, null, null, asciiCharClass, set, 0, 0, RegexOptions.None);

    public static Utf8ByteSafeLinearVerifierStep LoopByte(byte value, int min, int max) =>
        new(Utf8ByteSafeLinearVerifierStepKind.LoopByte, value, null, null, null, default, null, min, max, RegexOptions.None);

    public static Utf8ByteSafeLinearVerifierStep LoopText(byte[] text, int min, int max) =>
        new(Utf8ByteSafeLinearVerifierStepKind.LoopText, 0, text, null, null, default, null, min, max, RegexOptions.None);

    public static Utf8ByteSafeLinearVerifierStep LoopSet(string set, int min, int max) =>
        new(Utf8ByteSafeLinearVerifierStepKind.LoopSet, 0, null, null, null, default, set, min, max, RegexOptions.None);

    public static Utf8ByteSafeLinearVerifierStep LoopProjectedAsciiSet(string set, AsciiCharClass asciiCharClass, int min, int max) =>
        new(Utf8ByteSafeLinearVerifierStepKind.LoopProjectedAsciiSet, 0, null, null, null, asciiCharClass, set, min, max, RegexOptions.None);

    public static Utf8ByteSafeLinearVerifierStep RequireBeginning(RegexOptions options) =>
        new(Utf8ByteSafeLinearVerifierStepKind.RequireBeginning, 0, null, null, null, default, null, 0, 0, options);

    public static Utf8ByteSafeLinearVerifierStep RequireEnd(RegexOptions options) =>
        new(Utf8ByteSafeLinearVerifierStepKind.RequireEnd, 0, null, null, null, default, null, 0, 0, options);

    internal static Utf8ByteSafeLinearVerifierStep RequireBeginning(Utf8ExecutionNodeKind kind, RegexOptions options) =>
        new(Utf8ByteSafeLinearVerifierStepKind.RequireBeginning, (byte)kind, null, null, null, default, null, 0, 0, options);

    internal static Utf8ByteSafeLinearVerifierStep RequireEnd(Utf8ExecutionNodeKind kind, RegexOptions options) =>
        new(Utf8ByteSafeLinearVerifierStepKind.RequireEnd, (byte)kind, null, null, null, default, null, 0, 0, options);

    public static Utf8ByteSafeLinearVerifierStep RequireBoundary() =>
        CreateMarker(Utf8ByteSafeLinearVerifierStepKind.RequireBoundary);

    public static Utf8ByteSafeLinearVerifierStep RequireNonBoundary() =>
        CreateMarker(Utf8ByteSafeLinearVerifierStepKind.RequireNonBoundary);

    public static Utf8ByteSafeLinearVerifierStep MatchAnyText(byte[][] alternatives) =>
        new(Utf8ByteSafeLinearVerifierStepKind.MatchAnyText, 0, null, alternatives, null, default, null, 0, 0, RegexOptions.None);

    public static Utf8ByteSafeLinearVerifierStep MatchAnyTextOptional(byte[][] alternatives) =>
        new(Utf8ByteSafeLinearVerifierStepKind.MatchAnyTextOptional, 0, null, alternatives, null, default, null, 0, 0, RegexOptions.None);

    public static Utf8ByteSafeLinearVerifierStep LoopAnyText(byte[][] alternatives, int min, int max) =>
        new(Utf8ByteSafeLinearVerifierStepKind.LoopAnyText, 0, null, alternatives, null, default, null, min, max, RegexOptions.None);

    public static Utf8ByteSafeLinearVerifierStep LoopProgram(Utf8ByteSafeLinearVerifierStep[] program, int min, int max) =>
        new(Utf8ByteSafeLinearVerifierStepKind.LoopProgram, 0, null, null, program, default, null, min, max, RegexOptions.None);

    public static Utf8ByteSafeLinearVerifierStep Accept() =>
        CreateMarker(Utf8ByteSafeLinearVerifierStepKind.Accept);

    private static Utf8ByteSafeLinearVerifierStep CreateMarker(Utf8ByteSafeLinearVerifierStepKind kind) =>
        new(kind, 0, null, null, null, default, null, 0, 0, RegexOptions.None);
}

internal readonly record struct Utf8ByteSafeLinearCompileOutcome(
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

    private struct Compiler(Utf8ConstructionMap<Utf8ByteSafeLinearVerifierStep[], int>? minimumLengths)
    {
        private Utf8ByteSafeLinearCompileFailureKind _failureKind;
        private readonly Utf8ConstructionMap<Utf8ByteSafeLinearVerifierStep[], int>? _minimumLengths = minimumLengths;
        private enum AppendKind : byte { Node, Loop, SubProgram, TerminalLoop, SafeLoop, TerminalAlternation }
        private enum AppendAction : byte { Complete, Resume, Child }

        // Value continuations borrow builder/text/program payloads. Those
        // separate acquisitions still need their own construction admission.
        private struct AppendFrame(Utf8ExecutionNode node, List<Utf8ByteSafeLinearVerifierStep>? steps)
        {
            internal Utf8ExecutionNode Node = node;
            internal List<Utf8ByteSafeLinearVerifierStep>? Steps = steps;
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
            if (tree?.Root is not { } root)
                return new(default, Utf8ByteSafeLinearCompileFailureKind.EmptyTree);
            using var construction = Utf8ConstructionScope.Enter();
            var budget = construction.Budget;
            using var frames = Utf8ConstructionFrames<AppendFrame>.Create(budget, new(root, null));
            using var lengths = Utf8ConstructionMap<Utf8ByteSafeLinearVerifierStep[], int>.Create(budget, ReferenceEqualityComparer.Instance);
            var compiler = new Compiler(lengths);
            var last = new AppendResult(false, [], 0);
            List<Utf8ByteSafeLinearVerifierStep>? rootSteps = null;
            while (frames.Count != 0)
            {
                budget.ChargeWork(1);
                var index = frames.Count - 1;
                var frame = frames[index];
                AppendAction action;
                AppendFrame child;
                try { action = compiler.Advance(ref frame, last, out child); }
                finally { frames[index] = frame; }
                if (index == 0) rootSteps = frame.Steps;
                if (action == AppendAction.Child) frames.Push(child);
                else if (action == AppendAction.Complete)
                {
                    last = new(frame.Succeeded, frame.Program, frame.MinimumConsumedLength);
                    frames.Pop();
                }
            }
            var program = last.Succeeded && rootSteps is { } steps
                ? new Utf8ByteSafeLinearVerifierProgram([.. steps, Utf8ByteSafeLinearVerifierStep.Accept()]) : default;
            if (!program.HasValue && compiler._failureKind == Utf8ByteSafeLinearCompileFailureKind.None)
                compiler._failureKind = Utf8ByteSafeLinearCompileFailureKind.UnsupportedNode;
            // All admitted storage here is temporary. Returned program/text
            // arrays remain separate payload admission work; no scratch is retained.
            return new(program, compiler._failureKind);
        }

        private AppendAction Advance(ref AppendFrame frame, AppendResult last, out AppendFrame child)
        {
            frame.Steps ??= new List<Utf8ByteSafeLinearVerifierStep>();
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
                if (node.Children.Count != 1) return AppendAction.Complete;
                frame.Node = Utf8DeterministicAnchorSearch.Unwrap(node.Children[0]);
                return AppendAction.Resume;
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
                    var child = Utf8DeterministicAnchorSearch.Unwrap(node.Children[frame.Index]);
                    if (TryAppendDeterministicOptionalAlternation(node.Children, frame.Index, steps) ||
                        TryAppendDeterministicAlternation(child, steps))
                    {
                        frame.Index++; frame.Stage = 0;
                        return AppendAction.Resume;
                    }
                    frame.Stage = 4;
                    pending = new(child, steps) { Kind = child.Kind is Utf8ExecutionNodeKind.Loop or Utf8ExecutionNodeKind.LazyLoop
                        ? AppendKind.Loop : AppendKind.Node };
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
            case Utf8ExecutionNodeKind.One when node.Ch <= 0x7F:
                steps.Add(Utf8ByteSafeLinearVerifierStep.MatchByte((byte)node.Ch));
                break;
            case Utf8ExecutionNodeKind.Multi when Utf8DeterministicAnchorSearch.TryGetAsciiBytes(node.Text, out var text):
                steps.Add(Utf8ByteSafeLinearVerifierStep.MatchText(text));
                break;
            case Utf8ExecutionNodeKind.Set when node.Text is { Length: > 0 } set && TryCreateByteSafeSetStep(set, false, 0, 0, out var setStep):
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
                frame.AllowVariableLength = true;
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

        if (node.Children.Count == 1 &&
            TryExtractAlternationTexts(Utf8DeterministicAnchorSearch.Unwrap(node.Children[0]), out var alternatives, out var optional) &&
            !optional &&
            AreAlternativesPrefixFree(alternatives) &&
            (node.Min == node.Max || allowVariableLength))
        {
            steps.Add(Utf8ByteSafeLinearVerifierStep.LoopAnyText(alternatives, node.Min, node.Max));
            frame.Succeeded = true;
            return AppendAction.Complete;
        }

        if (node.Ch is > (char)0 and <= (char)0x7F)
        {
            steps.Add(Utf8ByteSafeLinearVerifierStep.LoopByte((byte)node.Ch, node.Min, node.Max));
            frame.Succeeded = true;
            return AppendAction.Complete;
        }

        if (node.Text is { Length: > 0 } loopSet && TryCreateByteSafeSetStep(loopSet, loop: true, node.Min, node.Max, out var loopSetStep))
        {
            steps.Add(loopSetStep);
            frame.Succeeded = true;
            return AppendAction.Complete;
        }

        if (Utf8DeterministicAnchorSearch.TryGetAsciiBytes(node.Text, out var loopText))
        {
            steps.Add(Utf8ByteSafeLinearVerifierStep.LoopText(loopText, node.Min, node.Max));
            frame.Succeeded = true;
            return AppendAction.Complete;
        }

        if (node.Children.Count != 1)
        {
            return AppendAction.Complete;
        }

        var child = Utf8DeterministicAnchorSearch.Unwrap(node.Children[0]);
        switch (child.Kind)
        {
            case Utf8ExecutionNodeKind.One when child.Ch <= 0x7F:
                steps.Add(Utf8ByteSafeLinearVerifierStep.LoopByte((byte)child.Ch, node.Min, node.Max));
                break;
            case Utf8ExecutionNodeKind.Multi when Utf8DeterministicAnchorSearch.TryGetAsciiBytes(child.Text, out var text):
                steps.Add(Utf8ByteSafeLinearVerifierStep.LoopText(text, node.Min, node.Max));
                break;
            case Utf8ExecutionNodeKind.Set when child.Text is { Length: > 0 } set && TryCreateByteSafeSetStep(set, true, node.Min, node.Max, out var setStep):
                steps.Add(setStep);
                break;
            case Utf8ExecutionNodeKind.Concatenate:
            case Utf8ExecutionNodeKind.Capture:
            case Utf8ExecutionNodeKind.Group:
            case Utf8ExecutionNodeKind.Loop:
            case Utf8ExecutionNodeKind.LazyLoop:
                frame.Stage = 1;
                pending = new(child, null) { Kind = AppendKind.SubProgram };
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
            pending = new(frame.Node, frame.Steps);
            return AppendAction.Child;
        }
        if (!last.Succeeded)
        {
            if (_failureKind == Utf8ByteSafeLinearCompileFailureKind.None)
                _failureKind = Utf8ByteSafeLinearCompileFailureKind.NestedSubProgram;
            return AppendAction.Complete;
        }
        frame.Program = [.. frame.Steps ?? throw new InvalidOperationException()];
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
                Utf8ByteSafeLinearVerifierStep.LoopText(step.Text, combinedMin, combinedMax),
            Utf8ByteSafeLinearVerifierStepKind.LoopSet =>
                Utf8ByteSafeLinearVerifierStep.LoopSet(step.Set, combinedMin, combinedMax),
            Utf8ByteSafeLinearVerifierStepKind.LoopProjectedAsciiSet =>
                Utf8ByteSafeLinearVerifierStep.LoopProjectedAsciiSet(step.Set, step.ProjectedAsciiCharClass, combinedMin, combinedMax),
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
            var node = Utf8DeterministicAnchorSearch.Unwrap(children[frame.Index]);
            if (node.Kind is not (Utf8ExecutionNodeKind.Loop or Utf8ExecutionNodeKind.LazyLoop) ||
                !CanTreatRemainingAsZeroWidth(children, frame.Index + 1)) return AppendAction.Complete;
            frame.Stage = 1;
            pending = new(node, frame.Steps) { Kind = AppendKind.Loop, AllowVariableLength = true };
            return AppendAction.Child;
        }
        if (!last.Succeeded) return AppendAction.Complete;
        if (++frame.Index < children.Count)
        {
            pending = new(Utf8DeterministicAnchorSearch.Unwrap(children[frame.Index]), frame.Steps);
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
        var node = Utf8DeterministicAnchorSearch.Unwrap(children[frame.Index]);
        if (node.Kind != Utf8ExecutionNodeKind.Loop || node.Min == node.Max) return AppendAction.Complete;
        if (!TryGetLeadingAsciiByteMask(node, out var loopLow, out var loopHigh) ||
            !TryGetFollowingAsciiByteMask(children, frame.Index + 1, out var nextLow, out var nextHigh) ||
            !MasksAreDisjoint(loopLow, loopHigh, nextLow, nextHigh))
        {
            if (_failureKind == Utf8ByteSafeLinearCompileFailureKind.None)
                _failureKind = Utf8ByteSafeLinearCompileFailureKind.NonDisjointVariableLoop;
            return AppendAction.Complete;
        }
        frame.Stage = 1;
        pending = new(node, frame.Steps) { Kind = AppendKind.Loop, AllowVariableLength = true };
        return AppendAction.Child;
    }

    private bool TryCreateByteSafeSetStep(string runtimeSet, bool loop, int min, int max, out Utf8ByteSafeLinearVerifierStep step)
    {
        if (TryCreateProjectedAsciiCharClass(runtimeSet, out var asciiCharClass))
        {
            // This carrier executes the ASCII intersection. Non-ASCII input
            // declines to the dispatcher; surrogate-half membership cannot
            // determine scalar loop admission or its minimum.
            step = loop
                ? Utf8ByteSafeLinearVerifierStep.LoopProjectedAsciiSet(runtimeSet, asciiCharClass, min, max)
                : Utf8ByteSafeLinearVerifierStep.MatchProjectedAsciiSet(runtimeSet, asciiCharClass);
            return true;
        }

        if (Internal.FrontEnd.Utf8OwnedCharClass.IsAscii(runtimeSet))
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
        return FrontEnd.DotNetAsciiCharClassProjector.TryProjectAsciiIntersection(runtimeSet, out asciiCharClass);
    }

    private AppendAction AppendTerminalAlternation(ref AppendFrame frame, AppendResult last, out AppendFrame pending)
    {
        pending = default;
        var children = frame.Node.Children;
        if (frame.Stage == 0)
        {
            var node = Utf8DeterministicAnchorSearch.Unwrap(children[frame.Index]);
            if (!CanTreatRemainingAsZeroWidth(children, frame.Index + 1))
            {
                if (node.Kind == Utf8ExecutionNodeKind.Alternate && _failureKind == Utf8ByteSafeLinearCompileFailureKind.None)
                    _failureKind = Utf8ByteSafeLinearCompileFailureKind.UnsupportedAlternation;
                return AppendAction.Complete;
            }
            if (!TryExtractAlternationTexts(node, out var alternatives, out var optional)) return AppendAction.Complete;
            var steps = frame.Steps ?? throw new InvalidOperationException();
            steps.Add(optional ? Utf8ByteSafeLinearVerifierStep.MatchAnyTextOptional(alternatives)
                : Utf8ByteSafeLinearVerifierStep.MatchAnyText(alternatives));
            frame.Stage = 1;
        }
        else if (!last.Succeeded) return AppendAction.Complete;
        if (++frame.Index < children.Count)
        {
            pending = new(Utf8DeterministicAnchorSearch.Unwrap(children[frame.Index]), frame.Steps);
            return AppendAction.Child;
        }
        frame.Succeeded = true;
        return AppendAction.Complete;
    }

    private bool TryAppendDeterministicOptionalAlternation(
        IReadOnlyList<Utf8ExecutionNode> children,
        int index,
        List<Utf8ByteSafeLinearVerifierStep> steps)
    {
        var node = Utf8DeterministicAnchorSearch.Unwrap(children[index]);
        if (!TryExtractAlternationTexts(node, out var alternatives, out var optional) ||
            !optional ||
            !TryExtractFollowingAsciiPrefix(children, index + 1, out var followingPrefix) ||
            !IsSafeOptionalAlternation(alternatives, followingPrefix))
        {
            if (node.Kind == Utf8ExecutionNodeKind.Alternate &&
                _failureKind == Utf8ByteSafeLinearCompileFailureKind.None)
            {
                _failureKind = Utf8ByteSafeLinearCompileFailureKind.UnsupportedAlternation;
            }
            return false;
        }

        steps.Add(Utf8ByteSafeLinearVerifierStep.MatchAnyTextOptional(alternatives));
        return true;
    }

    private bool TryAppendDeterministicAlternation(Utf8ExecutionNode node, List<Utf8ByteSafeLinearVerifierStep> steps)
    {
        if (!TryExtractAlternationTexts(node, out var alternatives, out var optional) ||
            optional ||
            !AreAlternativesPrefixFree(alternatives))
        {
            if (node.Kind == Utf8ExecutionNodeKind.Alternate &&
                _failureKind == Utf8ByteSafeLinearCompileFailureKind.None)
            {
                _failureKind = Utf8ByteSafeLinearCompileFailureKind.UnsupportedAlternation;
            }
            return false;
        }

        steps.Add(Utf8ByteSafeLinearVerifierStep.MatchAnyText(alternatives));
        return true;
    }

    private bool CanTreatRemainingAsZeroWidth(IReadOnlyList<Utf8ExecutionNode> children, int startIndex)
    {
        for (var i = startIndex; i < children.Count; i++)
        {
            Utf8ConstructionScope.ChargeWork(1);
            if (!Utf8DeterministicAnchorSearch.IsZeroWidthNode(Utf8DeterministicAnchorSearch.Unwrap(children[i])))
            {
                return false;
            }
        }

        return true;
    }

    private struct TextFrame(Utf8ExecutionNode node)
    {
        internal Utf8ExecutionNode Node = node;
        internal int Index;
    }

    private bool TryExtractAlternationTexts(Utf8ExecutionNode node, out byte[][] alternatives, out bool optional)
    {
        alternatives = [];
        optional = false;
        using var construction = Utf8ConstructionScope.Enter();
        var budget = construction.Budget;
        using var frames = Utf8ConstructionFrames<TextFrame>.Create(budget, new(node));
        var sawOptional = false;
        while (node.Kind == Utf8ExecutionNodeKind.Loop && node.Min == 0 && node.Max == 1 && node.Children.Count == 1)
        {
            budget.ChargeWork(1);
            sawOptional = true;
            node = Utf8DeterministicAnchorSearch.Unwrap(node.Children[0]);
            frames[0] = new(node);
        }
        budget.ChargeWork(1);
        if (node.Kind != Utf8ExecutionNodeKind.Alternate) return false;
        // The builder and returned payloads retain separate admission work.
        // The borrowed branch cursor is already admitted before acquisition.
        var list = new List<byte[]>(node.Children.Count);
        while (frames[0].Index < node.Children.Count)
        {
            budget.ChargeWork(1);
            var frame = frames[0];
            var child = node.Children[frame.Index++];
            frames[0] = frame;
            if (!TryExtractFixedAsciiText(Utf8DeterministicAnchorSearch.Unwrap(child), out var value))
            {
                if (_failureKind == Utf8ByteSafeLinearCompileFailureKind.None)
                    _failureKind = Utf8ByteSafeLinearCompileFailureKind.UnsupportedFixedText;
                return false;
            }
            list.Add(value);
        }
        alternatives = [.. list];
        optional = sawOptional && alternatives.Length > 0;
        return alternatives.Length > 0;
    }

    private bool TryExtractFollowingAsciiPrefix(
        IReadOnlyList<Utf8ExecutionNode> children,
        int startIndex,
        out byte[] prefix)
    {
        prefix = [];
        for (var i = startIndex; i < children.Count; i++)
        {
            Utf8ConstructionScope.ChargeWork(1);
            var child = Utf8DeterministicAnchorSearch.Unwrap(children[i]);
            if (Utf8DeterministicAnchorSearch.IsZeroWidthNode(child))
            {
                continue;
            }

            if (!TryExtractFixedAsciiText(child, out prefix) || prefix.Length == 0)
            {
                if (_failureKind == Utf8ByteSafeLinearCompileFailureKind.None)
                {
                    _failureKind = Utf8ByteSafeLinearCompileFailureKind.UnsupportedFollowingPrefix;
                }
                return false;
            }

            return true;
        }

        return false;
    }

    private bool TryGetFollowingAsciiByteMask(
        IReadOnlyList<Utf8ExecutionNode> children,
        int startIndex,
        out ulong lowMask,
        out ulong highMask)
    {
        lowMask = 0;
        highMask = 0;

        for (var i = startIndex; i < children.Count; i++)
        {
            Utf8ConstructionScope.ChargeWork(1);
            var child = Utf8DeterministicAnchorSearch.Unwrap(children[i]);
            if (Utf8DeterministicAnchorSearch.IsZeroWidthNode(child))
            {
                continue;
            }

            return TryGetLeadingAsciiByteMask(child, out lowMask, out highMask);
        }

        if (_failureKind == Utf8ByteSafeLinearCompileFailureKind.None)
        {
            _failureKind = Utf8ByteSafeLinearCompileFailureKind.UnsupportedFollowingMask;
        }
        return false;
    }

    private bool TryExtractFixedAsciiText(Utf8ExecutionNode node, out byte[] text)
    {
        text = [];
        using var construction = Utf8ConstructionScope.Enter();
        var budget = construction.Budget;
        using var frames = Utf8ConstructionFrames<TextFrame>.Create(budget, new(node));
        var buffer = new List<byte>();
        while (frames.Count != 0)
        {
            budget.ChargeWork(1);
            var index = frames.Count - 1;
            var frame = frames[index];
            node = frame.Node;
            if (node.Kind == Utf8ExecutionNodeKind.Concatenate)
            {
                if (frame.Index == node.Children.Count) { frames.Pop(); continue; }
                var child = node.Children[frame.Index++];
                frames[index] = frame;
                frames.Push(new(Utf8DeterministicAnchorSearch.Unwrap(child)));
                continue;
            }
            switch (node.Kind)
            {
                case Utf8ExecutionNodeKind.Empty: break;
                case Utf8ExecutionNodeKind.One when node.Ch <= 0x7F:
                    buffer.Add((byte)node.Ch);
                    break;
                case Utf8ExecutionNodeKind.Multi when Utf8DeterministicAnchorSearch.TryGetAsciiBytes(node.Text, out var bytes):
                    buffer.AddRange(bytes);
                    break;
                default:
                    if (_failureKind == Utf8ByteSafeLinearCompileFailureKind.None)
                        _failureKind = Utf8ByteSafeLinearCompileFailureKind.UnsupportedFixedText;
                    return false;
            }
            frames.Pop();
        }
        text = [.. buffer];
        return true;
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

            if (alternative.Length <= followingPrefix.Length &&
                SamePrefix(followingPrefix, alternative))
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

    private struct MaskFrame(Utf8ExecutionNode node)
    {
        internal Utf8ExecutionNode Node = node;
        internal int Index;
        internal bool Waiting;
        internal ulong Low;
        internal ulong High;
    }

    private bool TryGetLeadingAsciiByteMask(Utf8ExecutionNode node, out ulong lowMask, out ulong highMask)
    {
        lowMask = 0;
        highMask = 0;
        using var construction = Utf8ConstructionScope.Enter();
        var budget = construction.Budget;
        using var frames = Utf8ConstructionFrames<MaskFrame>.Create(budget, new(node));
        var success = false;
        while (frames.Count != 0)
        {
            budget.ChargeWork(1);
            var index = frames.Count - 1;
            var frame = frames[index];
            node = frame.Node;
            if (node.Kind == Utf8ExecutionNodeKind.Alternate && node.Children.Count > 0)
            {
                if (frame.Waiting)
                {
                    // Discard failed child masks; retain completed siblings.
                    if (!success)
                    {
                        lowMask = frame.Low;
                        highMask = frame.High;
                        frames.Pop();
                        continue;
                    }
                    frame.Low |= lowMask;
                    frame.High |= highMask;
                    if (frame.Index == node.Children.Count)
                    {
                        lowMask = frame.Low;
                        highMask = frame.High;
                        success = (lowMask | highMask) != 0;
                        frames.Pop();
                        continue;
                    }
                }
                frame.Waiting = true;
                var child = node.Children[frame.Index++];
                frames[index] = frame;
                frames.Push(new(Utf8DeterministicAnchorSearch.Unwrap(child)));
                continue;
            }
            if (node.Kind == Utf8ExecutionNodeKind.Concatenate)
            {
                Utf8ExecutionNode? first = null;
                while (frame.Index < node.Children.Count)
                {
                    budget.ChargeWork(1);
                    var child = node.Children[frame.Index++];
                    frames[index] = frame;
                    child = Utf8DeterministicAnchorSearch.Unwrap(child);
                    if (Utf8DeterministicAnchorSearch.IsZeroWidthNode(child)) continue;
                    first = child;
                    break;
                }
                if (first is not null)
                {
                    frames[index] = new(first);
                    continue;
                }
            }
            if (node.Kind is Utf8ExecutionNodeKind.Loop or Utf8ExecutionNodeKind.LazyLoop && node.Children.Count == 1)
            {
                // No parent work follows this tail step; keep one live frame.
                frames[index] = new(Utf8DeterministicAnchorSearch.Unwrap(node.Children[0]));
                continue;
            }
            success = ReadScalar(node, out lowMask, out highMask);
            frames.Pop();
        }
        return success;

        static bool ReadScalar(Utf8ExecutionNode node, out ulong low, out ulong high)
        {
            low = 0;
            high = 0;
            switch (node.Kind)
            {
                case Utf8ExecutionNodeKind.One when node.Ch <= 0x7F:
                    AddByteToMask((byte)node.Ch, ref low, ref high);
                    return true;
                case Utf8ExecutionNodeKind.Multi when Utf8DeterministicAnchorSearch.TryGetAsciiBytes(node.Text, out var text) && text.Length > 0:
                    AddByteToMask(text[0], ref low, ref high);
                    return true;
                case Utf8ExecutionNodeKind.Set when node.Text is { Length: > 0 } set:
                    return TryBuildByteSafeLeadingMask(set, out low, out high);
                case Utf8ExecutionNodeKind.Loop:
                case Utf8ExecutionNodeKind.LazyLoop:
                    if (node.Ch is > (char)0 and <= (char)0x7F)
                    {
                        AddByteToMask((byte)node.Ch, ref low, ref high);
                        return true;
                    }
                    if (node.Text is { Length: > 0 } loopSet)
                        return TryBuildByteSafeLeadingMask(loopSet, out low, out high);
                    if (Utf8DeterministicAnchorSearch.TryGetAsciiBytes(node.Text, out var loopText) && loopText.Length > 0)
                    {
                        AddByteToMask(loopText[0], ref low, ref high);
                        return true;
                    }
                    return false;
                default: return false;
            }
        }
    }

    private static bool TryBuildAsciiSetMask(string set, out ulong lowMask, out ulong highMask)
    {
        lowMask = 0;
        highMask = 0;

        for (var i = 0; i < 128; i++)
        {
            Utf8ConstructionScope.ChargeWork(1);
            if (Internal.FrontEnd.Utf8OwnedCharClass.CharInClass((char)i, set))
            {
                AddByteToMask((byte)i, ref lowMask, ref highMask);
            }
        }

        return (lowMask | highMask) != 0;
    }

    private static bool TryBuildByteSafeLeadingMask(string set, out ulong lowMask, out ulong highMask)
    {
        if (Internal.FrontEnd.Utf8OwnedCharClass.IsAscii(set))
        {
            return TryBuildAsciiSetMask(set, out lowMask, out highMask);
        }

        if (TryCreateProjectedAsciiCharClass(set, out var asciiCharClass))
        {
            Utf8ConstructionScope.ChargeWork(1);
            lowMask = asciiCharClass.Negated ? 0 : asciiCharClass.PositiveLowMask;
            highMask = asciiCharClass.Negated ? 0 : asciiCharClass.PositiveHighMask;

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

    private bool MasksAreDisjoint(
        ulong firstLowMask,
        ulong firstHighMask,
        ulong secondLowMask,
        ulong secondHighMask)
    {
        return (firstLowMask & secondLowMask) == 0 &&
               (firstHighMask & secondHighMask) == 0;
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
                    Utf8ByteSafeLinearVerifierStepKind.MatchSet => 1,
                    Utf8ByteSafeLinearVerifierStepKind.MatchProjectedAsciiSet => 1,
                    Utf8ByteSafeLinearVerifierStepKind.LoopByte => step.Min,
                    Utf8ByteSafeLinearVerifierStepKind.LoopText => step.Min * step.Text.Length,
                    Utf8ByteSafeLinearVerifierStepKind.LoopSet => step.Min,
                    Utf8ByteSafeLinearVerifierStepKind.LoopProjectedAsciiSet => step.Min,
                    Utf8ByteSafeLinearVerifierStepKind.MatchAnyText => step.Alternatives.Min(static value => value.Length),
                    Utf8ByteSafeLinearVerifierStepKind.MatchAnyTextOptional => 0,
                    Utf8ByteSafeLinearVerifierStepKind.LoopAnyText => step.Min * step.Alternatives.Min(static value => value.Length),
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

    private static bool MatchesSet(byte value, string runtimeSet)
    {
        return value < 128 && Internal.FrontEnd.Utf8OwnedCharClass.CharInClass((char)value, runtimeSet);
    }

}
