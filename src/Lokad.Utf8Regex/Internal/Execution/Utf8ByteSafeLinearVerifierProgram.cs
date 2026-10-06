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
        return new Compiler().Compile(tree);
    }

    private sealed class Compiler
    {
        private Utf8ByteSafeLinearCompileFailureKind _failureKind;
        private readonly Dictionary<Utf8ByteSafeLinearVerifierStep[], int> _minimumLengths = [];

        private enum AppendKind : byte { Node, Loop, SubProgram, TerminalLoop, SafeLoop, TerminalAlternation }

        private sealed class AppendRequest(Utf8ExecutionNode node, List<Utf8ByteSafeLinearVerifierStep> steps)
        {
            internal readonly Utf8ExecutionNode Node = node;
            internal readonly List<Utf8ByteSafeLinearVerifierStep> Steps = steps;
            internal AppendKind Kind;
            internal int Index;
            internal bool AllowVariableLength;
            internal bool Succeeded;
            internal Utf8ByteSafeLinearVerifierStep[] Program = [];
            internal int MinimumConsumedLength;
        }

        public Utf8ByteSafeLinearCompileOutcome Compile(Utf8ExecutionTree? tree)
        {
            if (tree?.Root is not { } root)
            {
                return new Utf8ByteSafeLinearCompileOutcome(default, Utf8ByteSafeLinearCompileFailureKind.EmptyTree);
            }

            var steps = new List<Utf8ByteSafeLinearVerifierStep>();
            var request = new AppendRequest(Utf8DeterministicAnchorSearch.Unwrap(root), steps);
            var frames = new Stack<IEnumerator<AppendRequest>>();
            frames.Push(AppendSteps(request).GetEnumerator());
            try
            {
                while (frames.Count > 0)
                {
                    var frame = frames.Peek();
                    if (frame.MoveNext()) frames.Push(AppendSteps(frame.Current).GetEnumerator());
                    else frames.Pop().Dispose();
                }
            }
            finally
            {
                while (frames.TryPop(out var frame)) frame.Dispose();
            }
            var program = request.Succeeded
                ? new Utf8ByteSafeLinearVerifierProgram([.. steps, Utf8ByteSafeLinearVerifierStep.Accept()])
                : default;
            if (!program.HasValue && _failureKind == Utf8ByteSafeLinearCompileFailureKind.None)
            {
                _failureKind = Utf8ByteSafeLinearCompileFailureKind.UnsupportedNode;
            }

            return new Utf8ByteSafeLinearCompileOutcome(program, _failureKind);

            IEnumerable<AppendRequest> AppendSteps(AppendRequest request) => request.Kind switch
            {
                AppendKind.Node => AppendNodeSteps(request),
                AppendKind.Loop => AppendLoopSteps(request),
                AppendKind.SubProgram => CompileSubProgramSteps(request),
                AppendKind.TerminalLoop => AppendTerminalLoopSteps(request),
                AppendKind.SafeLoop => AppendSafeVariableLoopSteps(request),
                _ => AppendTerminalAlternationSteps(request),
            };
        }

    private IEnumerable<AppendRequest> AppendNodeSteps(AppendRequest request)
    {
        var node = request.Node;
        var steps = request.Steps;
        switch (node.Kind)
        {
            case Utf8ExecutionNodeKind.Empty:
                request.Succeeded = true;
                yield break;

            case Utf8ExecutionNodeKind.Capture:
            case Utf8ExecutionNodeKind.Group:
                if (node.Children.Count != 1) yield break;
                var wrapped = new AppendRequest(Utf8DeterministicAnchorSearch.Unwrap(node.Children[0]), steps);
                yield return wrapped;
                request.Succeeded = wrapped.Succeeded;
                yield break;

            case Utf8ExecutionNodeKind.Concatenate:
                for (var i = 0; i < node.Children.Count; i++)
                {
                    var child = Utf8DeterministicAnchorSearch.Unwrap(node.Children[i]);
                    var terminalLoop = new AppendRequest(node, steps) { Kind = AppendKind.TerminalLoop, Index = i };
                    yield return terminalLoop;
                    if (terminalLoop.Succeeded)
                    {
                        request.Succeeded = true;
                        yield break;
                    }

                    var safeLoop = new AppendRequest(node, steps) { Kind = AppendKind.SafeLoop, Index = i };
                    yield return safeLoop;
                    if (safeLoop.Succeeded)
                    {
                        continue;
                    }

                    var terminalAlternation = new AppendRequest(node, steps) { Kind = AppendKind.TerminalAlternation, Index = i };
                    yield return terminalAlternation;
                    if (terminalAlternation.Succeeded)
                    {
                        request.Succeeded = true;
                        yield break;
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
                        var fixedLoop = new AppendRequest(child, steps) { Kind = AppendKind.Loop };
                        yield return fixedLoop;
                        if (!fixedLoop.Succeeded)
                        {
                            yield break;
                        }

                        continue;
                    }

                    var nested = new AppendRequest(Utf8DeterministicAnchorSearch.Unwrap(child), steps);
                    yield return nested;
                    if (!nested.Succeeded)
                    {
                        yield break;
                    }
                }

                request.Succeeded = true;
                yield break;

            case Utf8ExecutionNodeKind.One when node.Ch <= 0x7F:
                steps.Add(Utf8ByteSafeLinearVerifierStep.MatchByte((byte)node.Ch));
                request.Succeeded = true;
                yield break;

            case Utf8ExecutionNodeKind.Multi when Utf8DeterministicAnchorSearch.TryGetAsciiBytes(node.Text, out var text):
                steps.Add(Utf8ByteSafeLinearVerifierStep.MatchText(text));
                request.Succeeded = true;
                yield break;

            case Utf8ExecutionNodeKind.Set when node.Text is { Length: > 0 } set && TryCreateByteSafeSetStep(set, loop: false, min: 0, max: 0, out var setStep):
                steps.Add(setStep);
                request.Succeeded = true;
                yield break;

            case Utf8ExecutionNodeKind.Bol:
            case Utf8ExecutionNodeKind.Beginning:
            case Utf8ExecutionNodeKind.Start:
                steps.Add(Utf8ByteSafeLinearVerifierStep.RequireBeginning(node.Options));
                request.Succeeded = true;
                yield break;

            case Utf8ExecutionNodeKind.Eol:
            case Utf8ExecutionNodeKind.EndZ:
            case Utf8ExecutionNodeKind.End:
                steps.Add(Utf8ByteSafeLinearVerifierStep.RequireEnd(node.Options));
                request.Succeeded = true;
                yield break;

            case Utf8ExecutionNodeKind.Boundary:
                steps.Add(Utf8ByteSafeLinearVerifierStep.RequireBoundary());
                request.Succeeded = true;
                yield break;

            case Utf8ExecutionNodeKind.NonBoundary:
                steps.Add(Utf8ByteSafeLinearVerifierStep.RequireNonBoundary());
                request.Succeeded = true;
                yield break;

            case Utf8ExecutionNodeKind.Loop:
            case Utf8ExecutionNodeKind.LazyLoop:
                var loop = new AppendRequest(node, steps) { Kind = AppendKind.Loop, AllowVariableLength = true };
                yield return loop;
                request.Succeeded = loop.Succeeded;
                yield break;

            default:
                if (_failureKind == Utf8ByteSafeLinearCompileFailureKind.None)
                {
                    _failureKind = Utf8ByteSafeLinearCompileFailureKind.UnsupportedNode;
                }
                yield break;
        }
    }

    private IEnumerable<AppendRequest> AppendLoopSteps(AppendRequest request)
    {
        var node = request.Node;
        var steps = request.Steps;
        var allowVariableLength = request.AllowVariableLength;
        if (node.Kind == Utf8ExecutionNodeKind.LazyLoop && node.Min != node.Max)
        {
            if (_failureKind == Utf8ByteSafeLinearCompileFailureKind.None)
            {
                _failureKind = Utf8ByteSafeLinearCompileFailureKind.UnsupportedLoop;
            }
            yield break;
        }

        if (!allowVariableLength && node.Min != node.Max)
        {
            if (_failureKind == Utf8ByteSafeLinearCompileFailureKind.None)
            {
                _failureKind = Utf8ByteSafeLinearCompileFailureKind.UnsupportedLoop;
            }
            yield break;
        }

        if (node.Children.Count == 1 &&
            TryExtractAlternationTexts(Utf8DeterministicAnchorSearch.Unwrap(node.Children[0]), out var alternatives, out var optional) &&
            !optional &&
            AreAlternativesPrefixFree(alternatives) &&
            (node.Min == node.Max || allowVariableLength))
        {
            steps.Add(Utf8ByteSafeLinearVerifierStep.LoopAnyText(alternatives, node.Min, node.Max));
            request.Succeeded = true;
            yield break;
        }

        if (node.Ch is > (char)0 and <= (char)0x7F)
        {
            steps.Add(Utf8ByteSafeLinearVerifierStep.LoopByte((byte)node.Ch, node.Min, node.Max));
            request.Succeeded = true;
            yield break;
        }

        if (node.Text is { Length: > 0 } loopSet && TryCreateByteSafeSetStep(loopSet, loop: true, node.Min, node.Max, out var loopSetStep))
        {
            steps.Add(loopSetStep);
            request.Succeeded = true;
            yield break;
        }

        if (Utf8DeterministicAnchorSearch.TryGetAsciiBytes(node.Text, out var loopText))
        {
            steps.Add(Utf8ByteSafeLinearVerifierStep.LoopText(loopText, node.Min, node.Max));
            request.Succeeded = true;
            yield break;
        }

        if (node.Children.Count != 1)
        {
            yield break;
        }

        var child = Utf8DeterministicAnchorSearch.Unwrap(node.Children[0]);
        switch (child.Kind)
        {
            case Utf8ExecutionNodeKind.One when child.Ch <= 0x7F:
                steps.Add(Utf8ByteSafeLinearVerifierStep.LoopByte((byte)child.Ch, node.Min, node.Max));
                request.Succeeded = true;
                yield break;

            case Utf8ExecutionNodeKind.Multi when Utf8DeterministicAnchorSearch.TryGetAsciiBytes(child.Text, out var text):
                steps.Add(Utf8ByteSafeLinearVerifierStep.LoopText(text, node.Min, node.Max));
                request.Succeeded = true;
                yield break;

            case Utf8ExecutionNodeKind.Set when child.Text is { Length: > 0 } set && TryCreateByteSafeSetStep(set, loop: true, node.Min, node.Max, out var childLoopSetStep):
                steps.Add(childLoopSetStep);
                request.Succeeded = true;
                yield break;

            case Utf8ExecutionNodeKind.Concatenate:
            case Utf8ExecutionNodeKind.Capture:
            case Utf8ExecutionNodeKind.Group:
            case Utf8ExecutionNodeKind.Loop:
            case Utf8ExecutionNodeKind.LazyLoop:
                var subProgram = new AppendRequest(child, []) { Kind = AppendKind.SubProgram };
                yield return subProgram;
                if (subProgram.Succeeded)
                {
                    var programSteps = subProgram.Program;
                    if (subProgram.MinimumConsumedLength == 0)
                    {
                        if (_failureKind == Utf8ByteSafeLinearCompileFailureKind.None)
                        {
                            _failureKind = Utf8ByteSafeLinearCompileFailureKind.NestedSubProgram;
                        }
                        yield break;
                    }

                    if (TryCombineLoopSubProgram(programSteps, node.Min, node.Max, out var combinedLoopStep))
                    {
                        steps.Add(combinedLoopStep);
                        request.Succeeded = true;
                        yield break;
                    }

                    steps.Add(Utf8ByteSafeLinearVerifierStep.LoopProgram(programSteps, node.Min, node.Max));
                    request.Succeeded = true;
                    yield break;
                }

                yield break;

            default:
                if (_failureKind == Utf8ByteSafeLinearCompileFailureKind.None)
                {
                    _failureKind = Utf8ByteSafeLinearCompileFailureKind.UnsupportedLoop;
                }
                yield break;
        }
    }

    private IEnumerable<AppendRequest> CompileSubProgramSteps(AppendRequest request)
    {
        var child = new AppendRequest(request.Node, request.Steps);
        yield return child;
        if (!child.Succeeded)
        {
            if (_failureKind == Utf8ByteSafeLinearCompileFailureKind.None)
                _failureKind = Utf8ByteSafeLinearCompileFailureKind.NestedSubProgram;
            yield break;
        }
        request.Program = [.. request.Steps];
        request.MinimumConsumedLength = GetMinimumConsumedLength(request.Program);
        request.Succeeded = request.Program.Length > 0;
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

    private IEnumerable<AppendRequest> AppendTerminalLoopSteps(AppendRequest request)
    {
        var children = request.Node.Children;
        var index = request.Index;
        var node = Utf8DeterministicAnchorSearch.Unwrap(children[index]);
        if (node.Kind is not (Utf8ExecutionNodeKind.Loop or Utf8ExecutionNodeKind.LazyLoop) ||
            !CanTreatRemainingAsZeroWidth(children, index + 1)) yield break;
        var loop = new AppendRequest(node, request.Steps) { Kind = AppendKind.Loop, AllowVariableLength = true };
        yield return loop;
        if (!loop.Succeeded) yield break;
        for (var i = index + 1; i < children.Count; i++)
        {
            var child = new AppendRequest(Utf8DeterministicAnchorSearch.Unwrap(children[i]), request.Steps);
            yield return child;
            if (!child.Succeeded) yield break;
        }
        request.Succeeded = true;
    }

    private IEnumerable<AppendRequest> AppendSafeVariableLoopSteps(AppendRequest request)
    {
        var children = request.Node.Children;
        var index = request.Index;
        var steps = request.Steps;
        var node = Utf8DeterministicAnchorSearch.Unwrap(children[index]);
        if (node.Kind != Utf8ExecutionNodeKind.Loop || node.Min == node.Max)
        {
            yield break;
        }

        if (!TryGetLeadingAsciiByteMask(node, out var loopLowMask, out var loopHighMask) ||
            !TryGetFollowingAsciiByteMask(children, index + 1, out var nextLowMask, out var nextHighMask) ||
            !MasksAreDisjoint(loopLowMask, loopHighMask, nextLowMask, nextHighMask))
        {
            if (_failureKind == Utf8ByteSafeLinearCompileFailureKind.None)
            {
                _failureKind = Utf8ByteSafeLinearCompileFailureKind.NonDisjointVariableLoop;
            }
            yield break;
        }

        var loop = new AppendRequest(node, steps) { Kind = AppendKind.Loop, AllowVariableLength = true };
        yield return loop;
        request.Succeeded = loop.Succeeded;
    }

    private bool TryCreateByteSafeSetStep(string runtimeSet, bool loop, int min, int max, out Utf8ByteSafeLinearVerifierStep step)
    {
        if (TryCreateProjectedAsciiCharClass(runtimeSet, out var asciiCharClass))
        {
            // R05 units-vs-scalars fence: a projected loop counts scalars while BCL
            // counts astral units, so unbounded minima other than one diverge when the
            // set meets astral halves (probed false-miss: (?i)a\\W{2,}b on astral pairs
            // with no fallback constructed); min-one stays exact by the indifference
            // argument, bounded ranges route elsewhere, and astral-safe sets (positive
            // shorthands, ASCII-only sets) never meet halves, so only unbounded minima
            // other than one over halves-meeting sets divert to BCL-backed engines here.
            // Halves-meeting holds exactly when both surrogate halves are members
            // (probed across the shorthand table: negated word-group and
            // negated-shorthand sets meet both halves while positive shorthands and
            // ASCII-only sets meet neither).
            if (loop && min != 1 && max == int.MaxValue &&
                Internal.FrontEnd.Utf8OwnedCharClass.CharInClass((char)0xD800, runtimeSet) &&
                Internal.FrontEnd.Utf8OwnedCharClass.CharInClass((char)0xDC00, runtimeSet))
            {
                if (_failureKind == Utf8ByteSafeLinearCompileFailureKind.None)
                {
                    _failureKind = Utf8ByteSafeLinearCompileFailureKind.UnsupportedLoop;
                }

                step = default;
                return false;
            }

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

    private bool TryCreateProjectedAsciiCharClass(string runtimeSet, out AsciiCharClass asciiCharClass)
    {
        return FrontEnd.DotNetAsciiCharClassProjector.TryProjectAsciiIntersection(runtimeSet, out asciiCharClass);
    }

    private IEnumerable<AppendRequest> AppendTerminalAlternationSteps(AppendRequest request)
    {
        var children = request.Node.Children;
        var index = request.Index;
        var steps = request.Steps;
        var node = Utf8DeterministicAnchorSearch.Unwrap(children[index]);
        if (!CanTreatRemainingAsZeroWidth(children, index + 1))
        {
            if (node.Kind == Utf8ExecutionNodeKind.Alternate &&
                _failureKind == Utf8ByteSafeLinearCompileFailureKind.None)
            {
                _failureKind = Utf8ByteSafeLinearCompileFailureKind.UnsupportedAlternation;
            }
            yield break;
        }

        if (TryExtractAlternationTexts(node, out var alternatives, out var optional))
        {
            steps.Add(optional
                ? Utf8ByteSafeLinearVerifierStep.MatchAnyTextOptional(alternatives)
                : Utf8ByteSafeLinearVerifierStep.MatchAnyText(alternatives));

            for (var i = index + 1; i < children.Count; i++)
            {
                var child = new AppendRequest(Utf8DeterministicAnchorSearch.Unwrap(children[i]), steps);
                yield return child;
                if (!child.Succeeded)
                {
                    yield break;
                }
            }

            request.Succeeded = true;
            yield break;
        }

        yield break;
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
            if (!Utf8DeterministicAnchorSearch.IsZeroWidthNode(Utf8DeterministicAnchorSearch.Unwrap(children[i])))
            {
                return false;
            }
        }

        return true;
    }

    private bool TryExtractAlternationTexts(Utf8ExecutionNode node, out byte[][] alternatives, out bool optional)
    {
        alternatives = [];
        optional = false;

        var sawOptional = false;
        while (node.Kind == Utf8ExecutionNodeKind.Loop && node.Min == 0 && node.Max == 1 && node.Children.Count == 1)
        {
            sawOptional = true;
            node = Utf8DeterministicAnchorSearch.Unwrap(node.Children[0]);
        }
        // Compact childless loops are not alternations. Probing the same node
        // recursively used to revisit itself without making progress.

        if (node.Kind != Utf8ExecutionNodeKind.Alternate)
        {
            return false;
        }

        var list = new List<byte[]>(node.Children.Count);
        foreach (var child in node.Children)
        {
            if (!TryExtractFixedAsciiText(Utf8DeterministicAnchorSearch.Unwrap(child), out var text))
            {
                if (_failureKind == Utf8ByteSafeLinearCompileFailureKind.None)
                {
                    _failureKind = Utf8ByteSafeLinearCompileFailureKind.UnsupportedFixedText;
                }
                return false;
            }

            list.Add(text);
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
        var buffer = new List<byte>();
        var pending = new Stack<Utf8ExecutionNode>();
        pending.Push(node);
        while (pending.TryPop(out node))
        {
            switch (node.Kind)
            {
                case Utf8ExecutionNodeKind.Empty: break;
                case Utf8ExecutionNodeKind.One when node.Ch <= 0x7F:
                    buffer.Add((byte)node.Ch);
                    break;
                case Utf8ExecutionNodeKind.Multi when Utf8DeterministicAnchorSearch.TryGetAsciiBytes(node.Text, out var bytes):
                    buffer.AddRange(bytes);
                    break;
                case Utf8ExecutionNodeKind.Concatenate:
                    for (var i = node.Children.Count - 1; i >= 0; i--)
                        pending.Push(Utf8DeterministicAnchorSearch.Unwrap(node.Children[i]));
                    break;
                default:
                    if (_failureKind == Utf8ByteSafeLinearCompileFailureKind.None)
                        _failureKind = Utf8ByteSafeLinearCompileFailureKind.UnsupportedFixedText;
                    return false;
            }
        }
        text = [.. buffer];
        return true;
    }

    private bool AreAlternativesPrefixFree(byte[][] alternatives)
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

    private bool IsSafeOptionalAlternation(byte[][] alternatives, byte[] followingPrefix)
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

    private bool TryGetLeadingAsciiByteMask(Utf8ExecutionNode node, out ulong lowMask, out ulong highMask)
    {
        var frames = new Stack<(IReadOnlyList<Utf8ExecutionNode> Children, int Next, ulong Low, ulong High)>();
        while (true)
        {
            if (node.Kind == Utf8ExecutionNodeKind.Alternate && node.Children.Count > 0)
            {
                frames.Push((node.Children, 1, 0, 0));
                node = Utf8DeterministicAnchorSearch.Unwrap(node.Children[0]);
                continue;
            }
            if (node.Kind == Utf8ExecutionNodeKind.Concatenate)
            {
                var next = 0;
                while (next < node.Children.Count && Utf8DeterministicAnchorSearch.IsZeroWidthNode(
                    Utf8DeterministicAnchorSearch.Unwrap(node.Children[next]))) next++;
                if (next < node.Children.Count)
                {
                    node = Utf8DeterministicAnchorSearch.Unwrap(node.Children[next]);
                    continue;
                }
            }
            if (node.Kind is Utf8ExecutionNodeKind.Loop or Utf8ExecutionNodeKind.LazyLoop && node.Children.Count == 1)
            {
                node = Utf8DeterministicAnchorSearch.Unwrap(node.Children[0]);
                continue;
            }
            lowMask = 0;
            highMask = 0;
            var success = ReadScalar(node, out lowMask, out highMask);
            var resumed = false;
            while (frames.TryPop(out var frame))
            {
                // A failed branch contributes no partial mask to its parent.
                // Retain only the masks from the parent's successful siblings.
                if (!success)
                {
                    lowMask = frame.Low;
                    highMask = frame.High;
                    continue;
                }
                lowMask |= frame.Low;
                highMask |= frame.High;
                if (frame.Next < frame.Children.Count)
                {
                    frames.Push((frame.Children, frame.Next + 1, lowMask, highMask));
                    node = Utf8DeterministicAnchorSearch.Unwrap(frame.Children[frame.Next]);
                    resumed = true;
                    break;
                }
                success = (lowMask | highMask) != 0;
            }
            if (!resumed) return success;
        }

        bool ReadScalar(Utf8ExecutionNode node, out ulong low, out ulong high)
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

    private bool TryBuildAsciiSetMask(string set, out ulong lowMask, out ulong highMask)
    {
        lowMask = 0;
        highMask = 0;

        for (var i = 0; i < 128; i++)
        {
            if (Internal.FrontEnd.Utf8OwnedCharClass.CharInClass((char)i, set))
            {
                AddByteToMask((byte)i, ref lowMask, ref highMask);
            }
        }

        return (lowMask | highMask) != 0;
    }

    private bool TryBuildByteSafeLeadingMask(string set, out ulong lowMask, out ulong highMask)
    {
        if (Internal.FrontEnd.Utf8OwnedCharClass.IsAscii(set))
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

    private void AddByteToMask(byte value, ref ulong lowMask, ref ulong highMask)
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

    private int GetMinimumConsumedLength(Utf8ByteSafeLinearVerifierStep[] steps)
    {
        var frames = new Stack<(Utf8ByteSafeLinearVerifierStep[] Steps, int Index, int Length)>();
        frames.Push((steps, 0, 0));
        while (frames.TryPop(out var frame))
        {
            if (frame.Index == frame.Steps.Length)
            {
                _minimumLengths[frame.Steps] = frame.Length;
                continue;
            }
            var step = frame.Steps[frame.Index];
            if (step.Kind == Utf8ByteSafeLinearVerifierStepKind.LoopProgram && !_minimumLengths.ContainsKey(step.Program))
            {
                frames.Push(frame);
                frames.Push((step.Program, 0, 0));
                continue;
            }
            var length = frame.Length;
            checked
            {
                length += step.Kind switch
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
                    Utf8ByteSafeLinearVerifierStepKind.LoopProgram => step.Min * _minimumLengths[step.Program],
                    _ => 0,
                };
            }
            frames.Push((frame.Steps, frame.Index + 1, length));
        }
        return _minimumLengths[steps];
    }

    }

    private static bool MatchesSet(byte value, string runtimeSet)
    {
        return value < 128 && Internal.FrontEnd.Utf8OwnedCharClass.CharInClass((char)value, runtimeSet);
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

}
