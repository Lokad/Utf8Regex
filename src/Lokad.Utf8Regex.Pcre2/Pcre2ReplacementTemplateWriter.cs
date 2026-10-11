using System.Buffers;
using System.Text;
using Lokad.Utf8Regex.Internal;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Input;

namespace Lokad.Utf8Regex.Pcre2;

// Prepared templates expand directly into bytes. Continuations share the shaping
// ledger with output; matching and cache retention remain separate policy gates.
internal ref struct Pcre2ReplacementTemplateWriter
{
    private readonly ReadOnlySpan<byte> _input;
    private readonly Pcre2NameEntry[] _names;
    private Utf8SliceOutputWriter _writer;
    private Utf8SliceInvocation _invocation;
    private Pcre2GroupData[] _groups;
    private string? _mark;
    private Pcre2ReplacementRuntimeFrame[] _frames;
    private int _frameCount;
    private ulong _frameReservation;

    internal Pcre2ReplacementTemplateWriter(ReadOnlySpan<byte> input, Pcre2NameEntry[] names,
        ulong heapLimit, TimeSpan timeout, CancellationToken token)
    {
        _input = input; _names = names; _groups = []; _mark = null;
        _frames = []; _frameCount = 0; _frameReservation = 0;
        _invocation = new(new Utf8ExecutionLimits(0, 0, heapLimit), timeout, token);
        try
        {
            if (!Utf8SliceOutputWriter.Rent(input.Length, ref _invocation, out _writer)) ThrowHeapLimitExceeded();
        }
        catch (Utf8ExecutionDeadlineExpiredException) { throw DeadlineFailure(); }
    }

    internal int Length => _writer.Length;
    internal ulong ReservedWorkingBytes => _invocation.Ledger.ReservedBytes;
    internal void AppendGap(int start, int length) => AppendBytes(_input.Slice(start, length));

    internal void AppendTemplate(Pcre2ReplacementPreparation preparation, Pcre2SubstitutionOptions options,
        Pcre2GroupData[] groups, int matchStart, int matchLength, string? mark)
    {
        _groups = groups; _mark = mark; _frameCount = 0;
        if (preparation.Simple is { } simple)
        {
            foreach (var segment in simple.Segments)
                AppendBytes(segment.IsWholeMatch ? _input.Slice(matchStart, matchLength) : segment.LiteralUtf8);
            return;
        }
        if (preparation.Prepared is not { } plan) throw new InvalidOperationException("Missing prepared replacement program.");
        var state = default(Pcre2ReplacementCaseState);
        var syntaxOnly = false;
        var pc = plan.Fragments[0].Normal;
        while (true)
        {
            CheckWork();
            var instruction = plan.Instructions[pc];
            switch (instruction.Kind)
            {
                case Pcre2ReplacementOpcode.Return:
                    if (_frameCount == 0) return;
                    var frame = _frames[--_frameCount];
                    if (frame.RestoreState) state = frame.State;
                    syntaxOnly = frame.SyntaxOnly;
                    pc = frame.IsFragment ? Root(plan, frame.Normal, state.Quoted) : state.Quoted ? frame.Quoted : frame.Normal;
                    continue;
                case Pcre2ReplacementOpcode.Literal:
                    AppendReplacementBytes(instruction.Payload, ref state, syntaxOnly); break;
                case Pcre2ReplacementOpcode.Reference:
                    AppendResolvedReference(Resolve(instruction), options, ref state, syntaxOnly); break;
                case Pcre2ReplacementOpcode.Whole:
                    AppendReplacementBytes(_input.Slice(matchStart, matchLength), ref state, syntaxOnly); break;
                case Pcre2ReplacementOpcode.Prefix:
                    AppendReplacementBytes(_input[..matchStart], ref state, syntaxOnly); break;
                case Pcre2ReplacementOpcode.Suffix:
                    AppendReplacementBytes(_input[(matchStart + matchLength)..], ref state, syntaxOnly); break;
                case Pcre2ReplacementOpcode.Subject:
                    AppendReplacementBytes(_input, ref state, syntaxOnly); break;
                case Pcre2ReplacementOpcode.Plus:
                    var last = ResolveLastCapture();
                    if (last.Known || (options & Pcre2SubstitutionOptions.UnknownUnset) == 0)
                        AppendResolvedReference(last, options, ref state, syntaxOnly);
                    break;
                case Pcre2ReplacementOpcode.Quote: state.Quoted = true; break;
                // Ending quotation preserves case forcing; an unquoted \E resets it.
                case Pcre2ReplacementOpcode.EndQuote: state.Quoted = false; break;
                case Pcre2ReplacementOpcode.EndCase: state = default; break;
                case Pcre2ReplacementOpcode.Upper: state.Continuous = Pcre2ReplacementCaseTransformMode.Upper; break;
                case Pcre2ReplacementOpcode.Lower: state.Continuous = Pcre2ReplacementCaseTransformMode.Lower; break;
                case Pcre2ReplacementOpcode.UpperNext: state.Next = Pcre2ReplacementCaseTransformMode.Upper; state.Continuous = default; break;
                case Pcre2ReplacementOpcode.LowerNext: state.Next = Pcre2ReplacementCaseTransformMode.Lower; state.Continuous = default; break;
                case Pcre2ReplacementOpcode.AngleUnclosed:
                    if (!syntaxOnly) ThrowSubstitution(Pcre2ErrorKind.MalformedReplacementPattern);
                    break;
                case Pcre2ReplacementOpcode.Error: ThrowSubstitution((Pcre2ErrorKind)instruction.Argument); break;
                case Pcre2ReplacementOpcode.Conditional:
                case Pcre2ReplacementOpcode.Fallback:
                    var resolution = Resolve(instruction);
                    if (!resolution.Known && (options & Pcre2SubstitutionOptions.UnknownUnset) == 0)
                        ThrowSubstitution(Pcre2ErrorKind.UnknownReplacementGroup);
                    if (instruction.Kind == Pcre2ReplacementOpcode.Fallback && resolution.Known && resolution.Success)
                    { AppendResolvedValue(resolution, ref state, syntaxOnly); break; }
                    Push(new(instruction.NextNormal, instruction.NextQuoted, false, syntaxOnly, false, default));
                    if (instruction.Kind == Pcre2ReplacementOpcode.Fallback)
                    { pc = Root(plan, instruction.ElseFragment, state.Quoted); continue; }
                    var chosen = resolution.Known && resolution.Success ? instruction.ThenFragment : instruction.ElseFragment;
                    Push(new(chosen, 0, true, syntaxOnly, true, state));
                    if (instruction.ElseFragment >= 0) Push(new(instruction.ElseFragment, 0, true, true, true, state));
                    syntaxOnly = true;
                    pc = Root(plan, instruction.ThenFragment, state.Quoted);
                    continue;
                case Pcre2ReplacementOpcode.BackslashDigits:
                    var digits = ResolveNumber(instruction.Argument);
                    if (digits.Known) { AppendResolvedReference(digits, options, ref state, syntaxOnly); break; }
                    AppendReplacementBytes(instruction.Payload, ref state, syntaxOnly);
                    Push(new(instruction.NextNormal, instruction.NextQuoted, false, syntaxOnly, false, default));
                    pc = Root(plan, instruction.ElseFragment, state.Quoted);
                    continue;
            }
            pc = state.Quoted ? instruction.NextQuoted : instruction.NextNormal;
        }
    }

    private static int Root(Pcre2PreparedReplacementPlan plan, int fragment, bool quoted)
        => fragment < 0 ? 0 : quoted ? plan.Fragments[fragment].Quoted : plan.Fragments[fragment].Normal;

    internal byte[] Complete()
    {
        ReleaseFrames();
        try
        {
            if (!_writer.TryToArrayAndRelease(ref _invocation, out var result)) ThrowHeapLimitExceeded();
            return result;
        }
        catch (Utf8ExecutionDeadlineExpiredException) { throw DeadlineFailure(); }
    }
    internal void Release() { ReleaseFrames(); _writer.Release(ref _invocation); _groups = []; _mark = null; }

    private void Push(Pcre2ReplacementRuntimeFrame frame)
    {
        CheckWork();
        if (_frameCount == _frames.Length)
        {
            var capacity = checked(Math.Max(4, _frames.Length * 2));
            var bytes = Utf8ConstructionBudget.ArrayBytes<Pcre2ReplacementRuntimeFrame>(capacity);
            if (new Utf8InvocationBufferControl(ref _invocation).TryReserveWorking(bytes) != Utf8ExecutionLimitKind.None) ThrowHeapLimitExceeded();
            try
            {
                var grown = new Pcre2ReplacementRuntimeFrame[capacity];
                for (var index = 0; index < _frameCount; index++) { CheckWork(); grown[index] = _frames[index]; }
                _invocation.Ledger.Release(_frameReservation);
                _frameReservation = bytes; _frames = grown;
            }
            catch { _invocation.Ledger.Release(bytes); throw; }
        }
        _frames[_frameCount++] = frame;
    }

    private void ReleaseFrames()
    {
        _invocation.Ledger.Release(_frameReservation);
        _frames = []; _frameReservation = 0; _frameCount = 0;
    }

    private void AppendBytes(scoped ReadOnlySpan<byte> bytes)
    {
        CheckWork();
        try { if (!_writer.Append(bytes, ref _invocation)) ThrowHeapLimitExceeded(); }
        catch (Utf8ExecutionDeadlineExpiredException) { throw DeadlineFailure(); }
    }

    private void CheckWork()
    {
        Pcre2ReplacementPreparationControl.Check();
        try
        {
            if (_invocation.TryChargeInputStep() != Utf8ExecutionLimitKind.None)
                throw new Pcre2MatchException("The PCRE2 match limit was exceeded.", Pcre2ErrorKind.MatchLimit);
        }
        catch (Utf8ExecutionDeadlineExpiredException)
        { throw DeadlineFailure(); }
    }

    private static Pcre2MatchException DeadlineFailure()
        => new("The PCRE2 match deadline expired.", Pcre2ErrorKind.Timeout);

    private static void ThrowHeapLimitExceeded()
        => throw new Pcre2MatchException("The PCRE2 heap limit was exceeded.", Pcre2ErrorKind.HeapLimit);
    private static void ThrowSubstitution(Pcre2ErrorKind kind)
        => throw new Pcre2SubstitutionException("Invalid PCRE2 replacement template.", kind);

    private Pcre2ReplacementReferenceResolution Resolve(Pcre2ReplacementInstruction instruction) => instruction.Selector switch
    {
        Pcre2ReplacementSelectorKind.Number => ResolveNumber(instruction.Argument),
        Pcre2ReplacementSelectorKind.Name => ResolveName(instruction.Payload),
        _ => new(true, _mark is not null, 0, 0, true)
    };

    private Pcre2ReplacementReferenceResolution ResolveNumber(int number)
    {
        if ((uint)number >= (uint)_groups.Length) return default;
        var group = _groups[number];
        return new(true, group.Success, group.StartOffsetInBytes, group.EndOffsetInBytes - group.StartOffsetInBytes, false);
    }

    private Pcre2ReplacementReferenceResolution ResolveName(ReadOnlySpan<byte> name)
    {
        var known = false;
        foreach (var entry in _names)
        {
            var token = name; var candidate = entry.Name.AsSpan();
            while (!token.IsEmpty && !candidate.IsEmpty)
            {
                CheckWork();
                _ = Rune.DecodeFromUtf8(token, out var expected, out var bytes);
                _ = Rune.DecodeFromUtf16(candidate, out var actual, out var chars);
                if (expected != actual) break;
                token = token[bytes..]; candidate = candidate[chars..];
            }
            if (!token.IsEmpty || !candidate.IsEmpty) continue;
            known = true;
            if ((uint)entry.Number < (uint)_groups.Length && _groups[entry.Number].Success) return ResolveNumber(entry.Number);
        }
        return known ? new(true, false, 0, 0, false) : default;
    }

    private Pcre2ReplacementReferenceResolution ResolveLastCapture()
    {
        if (_groups.Length <= 1) return default;
        for (var index = _groups.Length - 1; index >= 1; index--)
        { CheckWork(); if (_groups[index].Success) return ResolveNumber(index); }
        return new(true, false, 0, 0, false);
    }

    private void AppendResolvedReference(Pcre2ReplacementReferenceResolution resolution, Pcre2SubstitutionOptions options,
        ref Pcre2ReplacementCaseState state, bool syntaxOnly)
    {
        if (!resolution.Known)
        {
            if (syntaxOnly) return;
            if ((options & Pcre2SubstitutionOptions.UnknownUnset) == 0) ThrowSubstitution(Pcre2ErrorKind.UnknownReplacementGroup);
            if ((options & Pcre2SubstitutionOptions.UnsetEmpty) == 0) ThrowSubstitution(Pcre2ErrorKind.UnsetReplacementGroup);
            return;
        }
        if (!resolution.Success)
        {
            if (!syntaxOnly && (options & Pcre2SubstitutionOptions.UnsetEmpty) == 0) ThrowSubstitution(Pcre2ErrorKind.UnsetReplacementGroup);
            return;
        }
        AppendResolvedValue(resolution, ref state, syntaxOnly);
    }

    private void AppendResolvedValue(Pcre2ReplacementReferenceResolution resolution, ref Pcre2ReplacementCaseState state, bool syntaxOnly)
    {
        if (!resolution.IsMark) { AppendReplacementBytes(_input.Slice(resolution.Start, resolution.Length), ref state, syntaxOnly); return; }
        var text = _mark.AsSpan();
        while (!text.IsEmpty)
        {
            CheckWork();
            var status = Rune.DecodeFromUtf16(text, out var scalar, out var consumed);
            AppendScalar(status == OperationStatus.Done ? scalar : Rune.ReplacementChar, ref state, syntaxOnly);
            text = text[Math.Max(1, consumed)..];
        }
    }

    private void AppendReplacementBytes(ReadOnlySpan<byte> bytes, ref Pcre2ReplacementCaseState state, bool syntaxOnly)
    {
        if (state.Next == Pcre2ReplacementCaseTransformMode.None && state.Continuous == Pcre2ReplacementCaseTransformMode.None)
        { if (!syntaxOnly) AppendBytes(bytes); return; }
        while (!bytes.IsEmpty)
        {
            CheckWork();
            if (Rune.DecodeFromUtf8(bytes, out var scalar, out var consumed) != OperationStatus.Done)
                _ = Utf8Validation.Validate(bytes);
            AppendScalar(scalar, ref state, syntaxOnly);
            bytes = bytes[consumed..];
        }
    }

    private void AppendScalar(Rune value, ref Pcre2ReplacementCaseState state, bool syntaxOnly)
    {
        var transform = state.Next != Pcre2ReplacementCaseTransformMode.None ? state.Next : state.Continuous;
        value = transform switch { Pcre2ReplacementCaseTransformMode.Upper => Rune.ToUpperInvariant(value),
            Pcre2ReplacementCaseTransformMode.Lower => Rune.ToLowerInvariant(value), _ => value };
        if (!syntaxOnly)
        {
            Span<byte> bytes = stackalloc byte[4];
            AppendBytes(bytes[..value.EncodeToUtf8(bytes)]);
        }
        state.Next = default;
    }

    private readonly record struct Pcre2ReplacementReferenceResolution(bool Known, bool Success, int Start, int Length, bool IsMark);
    private enum Pcre2ReplacementCaseTransformMode : byte { None, Upper, Lower }
    private struct Pcre2ReplacementCaseState
    {
        internal Pcre2ReplacementCaseTransformMode Next;
        internal Pcre2ReplacementCaseTransformMode Continuous;
        internal bool Quoted;
    }
    private readonly record struct Pcre2ReplacementRuntimeFrame(int Normal, int Quoted, bool IsFragment,
        bool SyntaxOnly, bool RestoreState, Pcre2ReplacementCaseState State);
}
