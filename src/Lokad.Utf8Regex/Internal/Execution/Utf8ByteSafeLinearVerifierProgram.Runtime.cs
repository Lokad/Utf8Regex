using System.Threading;

namespace Lokad.Utf8Regex.Internal.Execution;

internal readonly partial struct Utf8ByteSafeLinearVerifierProgram
{
    private readonly record struct LoopFrame(Utf8ByteSafeLinearVerifierStep[] Parent, int Pc, int Index, int Count);

    public bool TryMatch(ReadOnlySpan<byte> input, int startIndex, out int matchedLength)
        => TryMatch(input, startIndex, out matchedLength, out _);

    public bool TryMatch(ReadOnlySpan<byte> input, int startIndex, out int matchedLength, out bool requiresCompatibilityFallback)
        => TryMatch(input, startIndex, Utf8ExecutionDeadline.Infinite, out matchedLength, out requiresCompatibilityFallback);

    internal bool TryMatch(ReadOnlySpan<byte> input, int startIndex, Utf8ExecutionDeadline deadline,
        out int matchedLength, out bool requiresCompatibilityFallback)
    {
        var invocation = new Utf8SliceInvocation(Utf8ExecutionLimits.Unlimited, deadline, CancellationToken.None);
        var found = TryMatch(input, startIndex, ref invocation, out matchedLength, out requiresCompatibilityFallback, out var breach);
        if (breach != Utf8ExecutionLimitKind.None)
            throw new InvalidOperationException("An unlimited linear invocation exceeded its representable state extent.");
        return found;
    }

    internal bool TryMatch(ReadOnlySpan<byte> input, int startIndex, ref Utf8SliceInvocation invocation,
        out int matchedLength, out bool requiresCompatibilityFallback, out Utf8ExecutionLimitKind breach)
    {
        matchedLength = 0;
        requiresCompatibilityFallback = false;
        breach = Utf8ExecutionLimitKind.None;
        var frames = new Utf8InvocationStateBuffer<LoopFrame>();
        try
        {
            breach = invocation.TryChargeInputStep();
            if (breach != Utf8ExecutionLimitKind.None || !HasValue || (uint)startIndex > (uint)input.Length) return false;
            var steps = Steps;
            var pc = 0;
            var index = startIndex;
            while (true)
            {
                breach = invocation.TryChargeInputStep();
                if (breach != Utf8ExecutionLimitKind.None) return false;
                breach = invocation.Budget.TryNoteDepth((uint)frames.Count + 1);
                if (breach != Utf8ExecutionLimitKind.None) return false;
                bool succeeded;
                if (pc == steps.Length)
                {
                    if (frames.Count == 0) return false; // Only the root's Accept publishes a match.
                    succeeded = true;
                }
                else
                {
                    var step = steps[pc];
                    if (step.Kind == Utf8ByteSafeLinearVerifierStepKind.Accept)
                    {
                        if (frames.Count == 0)
                        {
                            breach = invocation.TryChargeInputStep();
                            if (breach != Utf8ExecutionLimitKind.None) return false;
                            matchedLength = index - startIndex;
                            return true;
                        }
                        succeeded = false; // An embedded Accept is not a subprogram result.
                    }
                    else if (step.Kind == Utf8ByteSafeLinearVerifierStepKind.LoopProgram && step.Max != 0)
                    {
                        breach = invocation.Budget.TryNoteDepth((uint)frames.Count + 2);
                        if (breach != Utf8ExecutionLimitKind.None) return false;
                        breach = frames.TryPush(new(steps, pc, index, 0), ref invocation);
                        if (breach != Utf8ExecutionLimitKind.None) return false;
                        steps = step.Program;
                        pc = 0;
                        continue;
                    }
                    else
                    {
                        succeeded = step.Kind == Utf8ByteSafeLinearVerifierStepKind.LoopProgram
                            ? step.Min == 0
                            : TryExecuteAtom(step, input, ref index, ref invocation, out requiresCompatibilityFallback, out breach);
                        if (breach != Utf8ExecutionLimitKind.None) return false;
                        if (succeeded) { pc++; continue; }
                    }
                }

                // Unwind failed bodies without committing their partial consumption.
                // A successful body commits one iteration; a non-progressing body
                // rejects that loop even when its minimum was already satisfied.
                while (frames.Count != 0)
                {
                    breach = invocation.TryChargeInputStep();
                    if (breach != Utf8ExecutionLimitKind.None) return false;
                    var frame = frames[frames.Count - 1];
                    frames.Pop();
                    var loop = frame.Parent[frame.Pc];
                    var progress = succeeded && index != frame.Index;
                    var count = frame.Count + (progress ? 1 : 0);
                    if (progress && (loop.Max < 0 || count < loop.Max))
                    {
                        breach = frames.TryPush(frame with { Index = index, Count = count }, ref invocation);
                        if (breach != Utf8ExecutionLimitKind.None) return false;
                        steps = loop.Program;
                        pc = 0;
                        break;
                    }
                    if (!progress) index = frame.Index;
                    succeeded = !requiresCompatibilityFallback && (!succeeded || progress) && count >= loop.Min;
                    steps = frame.Parent;
                    pc = frame.Pc + 1;
                    if (succeeded) break;
                }
                if (!succeeded && frames.Count == 0) return false;
            }
        }
        finally { frames.Release(ref invocation); }
    }

    private static bool TryExecuteAtom(Utf8ByteSafeLinearVerifierStep step, ReadOnlySpan<byte> input,
        ref int index, ref Utf8SliceInvocation invocation, out bool fallback, out Utf8ExecutionLimitKind breach)
    {
        fallback = false;
        breach = Utf8ExecutionLimitKind.None;
        switch (step.Kind)
        {
            case Utf8ByteSafeLinearVerifierStepKind.MatchByte:
            case Utf8ByteSafeLinearVerifierStepKind.MatchSet:
            case Utf8ByteSafeLinearVerifierStepKind.MatchProjectedAsciiSet:
            case Utf8ByteSafeLinearVerifierStepKind.LoopByte:
            case Utf8ByteSafeLinearVerifierStepKind.LoopSet:
            case Utf8ByteSafeLinearVerifierStepKind.LoopProjectedAsciiSet:
                var looping = step.Kind is Utf8ByteSafeLinearVerifierStepKind.LoopByte or
                    Utf8ByteSafeLinearVerifierStepKind.LoopSet or Utf8ByteSafeLinearVerifierStepKind.LoopProjectedAsciiSet;
                var min = looping ? step.Min : 1;
                var max = looping ? step.Max : 1;
                var count = 0;
                while ((uint)index < (uint)input.Length && (max < 0 || count < max))
                {
                    if ((count & 2047) == 0)
                    {
                        breach = invocation.TryChargeInputStep();
                        if (breach != Utf8ExecutionLimitKind.None) return false;
                    }
                    var value = input[index];
                    bool contains;
                    if (step.Kind is Utf8ByteSafeLinearVerifierStepKind.MatchProjectedAsciiSet or Utf8ByteSafeLinearVerifierStepKind.LoopProjectedAsciiSet)
                    {
                        if (value >= 128) { fallback = true; return false; }
                        contains = step.ProjectedAsciiCharClass.Contains(value);
                    }
                    else contains = step.Kind is Utf8ByteSafeLinearVerifierStepKind.MatchByte or Utf8ByteSafeLinearVerifierStepKind.LoopByte
                        ? value == step.Value : MatchesSet(value, step.Set);
                    if (!contains) break;
                    index++;
                    count++;
                }
                return count >= min;
            case Utf8ByteSafeLinearVerifierStepKind.MatchText:
                return TryMatchText(input, ref index, step.Text, ref invocation, out breach);
            case Utf8ByteSafeLinearVerifierStepKind.MatchAnyText:
            case Utf8ByteSafeLinearVerifierStepKind.MatchAnyTextOptional:
                return TryMatchAnyText(input, ref index, step.Alternatives,
                    step.Kind == Utf8ByteSafeLinearVerifierStepKind.MatchAnyTextOptional, ref invocation, out breach);
            case Utf8ByteSafeLinearVerifierStepKind.LoopText:
            case Utf8ByteSafeLinearVerifierStepKind.LoopAnyText:
                var iterations = 0;
                while (step.Max < 0 || iterations < step.Max)
                {
                    breach = invocation.TryChargeInputStep();
                    if (breach != Utf8ExecutionLimitKind.None) return false;
                    var next = index;
                    var found = step.Kind == Utf8ByteSafeLinearVerifierStepKind.LoopText
                        ? TryMatchText(input, ref next, step.Text, ref invocation, out breach)
                        : TryMatchAnyText(input, ref next, step.Alternatives, false, ref invocation, out breach);
                    if (breach != Utf8ExecutionLimitKind.None) return false;
                    if (!found) break;
                    if (next == index) return false; // Admitted loop bodies always consume input.
                    index = next;
                    iterations++;
                }
                return iterations >= step.Min;
            case Utf8ByteSafeLinearVerifierStepKind.RequireBeginning:
                return IsBeginningOfLine(input, index, step.Options);
            case Utf8ByteSafeLinearVerifierStepKind.RequireEnd:
                return IsEndAnchorMatch(input, index, step.Options);
            case Utf8ByteSafeLinearVerifierStepKind.RequireBoundary:
                return DotNetUtf8WordBoundary.IsBoundary(input, index);
            case Utf8ByteSafeLinearVerifierStepKind.RequireNonBoundary:
                return !DotNetUtf8WordBoundary.IsBoundary(input, index);
            default:
                return false;
        }
    }

    private static bool TryMatchText(ReadOnlySpan<byte> input, ref int index, ReadOnlySpan<byte> text,
        ref Utf8SliceInvocation invocation, out Utf8ExecutionLimitKind breach)
    {
        breach = Utf8ExecutionLimitKind.None;
        if (input.Length - index < text.Length) return false;
        for (var offset = 0; offset < text.Length;)
        {
            breach = invocation.TryChargeInputStep();
            if (breach != Utf8ExecutionLimitKind.None) return false;
            var count = Math.Min(2048, text.Length - offset);
            if (!input.Slice(index + offset, count).SequenceEqual(text.Slice(offset, count))) return false;
            offset += count;
        }
        index += text.Length;
        return true;
    }

    private static bool TryMatchAnyText(ReadOnlySpan<byte> input, ref int index, byte[][] alternatives,
        bool optional, ref Utf8SliceInvocation invocation, out Utf8ExecutionLimitKind breach)
    {
        breach = Utf8ExecutionLimitKind.None;
        foreach (var candidate in alternatives)
        {
            breach = invocation.TryChargeInputStep();
            if (breach != Utf8ExecutionLimitKind.None) return false;
            if (TryMatchText(input, ref index, candidate, ref invocation, out breach)) return true;
            if (breach != Utf8ExecutionLimitKind.None) return false;
        }
        return optional;
    }
}
