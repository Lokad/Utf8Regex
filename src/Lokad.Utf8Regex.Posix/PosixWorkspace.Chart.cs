using System.Buffers;
using System.Text;
using Lokad.Utf8Regex.Internal;
using Lokad.Utf8Regex.Internal.Execution;
using ChartKind = Lokad.Utf8Regex.Posix.PosixProgram.ChartKind;
using ChartNode = Lokad.Utf8Regex.Posix.PosixProgram.ChartNode;

namespace Lokad.Utf8Regex.Posix;

internal partial struct PosixWorkspace
{
    // One cell selects a completed interval, never an unfinished history. Keys
    // cannot grow with the number of competing parses. An unbounded repetition
    // saturates its count after the required minimum; empty progress is bounded.
    private readonly record struct ChartKey(int Node, int Start, int End, int Count);
    private readonly record struct ChartFrame(ChartKey Key, int Phase = 0, int Middle = 0, bool Single = false);
    private void PushChartFrame(ChartFrame frame, ref Utf8SliceInvocation invocation)
    {
        Check(invocation.Budget.TryNoteDepth(checked((uint)_chartFrames.Count + 1)), ref invocation);
        Check(_chartFrames.TryPush(frame, ref invocation), ref invocation);
    }

    private void ReplaceChartFrame(ChartFrame frame, ref Utf8SliceInvocation invocation)
    {
        _chartFrames.Pop();
        PushChartFrame(frame, ref invocation);
    }

    private void StoreChart(ChartKey key, int decision, ref Utf8SliceInvocation invocation)
    {
        if (_chartMemo is null || _chartMemo.Count == _chartCapacity)
        {
            var capacity = (int)Math.Min(Array.MaxLength, _chartCapacity == 0 ? 16L : (long)_chartCapacity * 2);
            if (capacity <= _chartCapacity) throw new OutOfMemoryException("The POSIX interval table cannot grow further.");
            // Deliberately conservative: four key/value and index-array extents
            // cover runtime dictionary rounding/storage. This is major-capacity
            // admission, not internal hash-probe or helper-object accounting.
            var bytes = checked(4 * (Utf8ConstructionBudget.ArrayBytes<KeyValuePair<ChartKey, int>>(capacity) +
                Utf8ConstructionBudget.ArrayBytes<int>(capacity)));
            Check(invocation.TryReserveWorking(bytes), ref invocation);
            var adopted = false;
            try
            {
                Charge(ref invocation);
                _chartMemo ??= new();
                _chartCapacity = _chartMemo.EnsureCapacity(capacity);
                invocation.Ledger.Release(_chartBytes);
                _chartBytes = bytes;
                adopted = true;
            }
            finally { if (!adopted) invocation.Ledger.Release(bytes); }
        }
        _chartMemo.Add(key, decision);
    }

    private void CompleteChart(ChartFrame frame, bool result, int decision, ref Utf8SliceInvocation invocation)
    {
        _chartFrames.Pop();
        if (!IsChartAtom(_program.Chart[frame.Key.Node]))
            StoreChart(frame.Key, result ? decision : -1, ref invocation);
        _chartResult = result;
    }

    private static bool IsChartAtom(ChartNode node) => node.Kind is ChartKind.Empty or ChartKind.Literal or
        ChartKind.Dot or ChartKind.Anchor or ChartKind.Class;

    private static int PreviousChartBoundary(ReadOnlySpan<byte> input, int at)
    {
        if (at == 0) return -1;
        at--;
        // Valid UTF8 has at most three continuation bytes. Bound the operation
        // even for a caller violating AssumeValid; all indexing remains checked.
        for (var skipped = 0; skipped < 3 && at > 0 && (input[at] & 0xc0) == 0x80; skipped++) at--;
        return at;
    }

    private static int FloorChartBoundary(ReadOnlySpan<byte> input, int at, int floor)
    {
        for (var skipped = 0; skipped < 3 && at > floor && at < input.Length && (input[at] & 0xc0) == 0x80; skipped++) at--;
        return at;
    }

    private static int NextChartBoundary(ReadOnlySpan<byte> input, int at)
        => Rune.DecodeFromUtf8(input[at..], out _, out var width) == OperationStatus.Done ? at + width : -1;

    private bool MatchChartAtom(ChartNode node, ChartKey key, ReadOnlySpan<byte> input,
        ref Utf8SliceInvocation invocation)
    {
        if (node.Kind == ChartKind.Empty) return key.Start == key.End;
        if (node.Kind == ChartKind.Anchor)
            return key.Start == key.End && ((Utf8SliceAnchorKind)node.A switch
            {
                Utf8SliceAnchorKind.Beginning => key.Start == 0 || node.B != 0 && input[key.Start - 1] == '\n',
                Utf8SliceAnchorKind.VeryEnd => key.Start == input.Length,
                Utf8SliceAnchorKind.End => key.Start == input.Length || node.B != 0 && input[key.Start] == '\n',
                _ => throw new InvalidOperationException("Unsupported POSIX anchor."),
            });
        if (Rune.DecodeFromUtf8(input.Slice(key.Start, key.End - key.Start), out var scalar, out var width) != OperationStatus.Done ||
            width != key.End - key.Start) return false;
        if (node.Kind == ChartKind.Literal) return scalar.Value == node.A;
        if (node.Kind == ChartKind.Dot) return node.A != 0 || scalar.Value != '\n';
        var set = _program.ChartClasses[node.A];
        var hit = Utf8SliceScalarClasses.MatchesMask(scalar, set.Mask16, false, _program.Scalar.CaseBehavior);
        if (!hit)
        {
            hit = Utf8SliceScalarClasses.MatchesTables(scalar, set.Categories, false, _program.Scalar.CaseBehavior,
                ref invocation, out var breach);
            Check(breach, ref invocation);
        }
        return hit != set.Negated;
    }

    private void BeginChartSplit(ChartFrame frame, ChartNode node, ReadOnlySpan<byte> input,
        ref Utf8SliceInvocation invocation)
    {
        var key = frame.Key;
        var left = _program.Chart[node.Left]; var right = _program.Chart[node.Right];
        var middle = key.End; var single = false;
        if (IsChartAtom(left))
        { middle = left.Kind is ChartKind.Empty or ChartKind.Anchor ? key.Start : NextChartBoundary(input, key.Start); single = true; }
        else if (IsChartAtom(right))
        { middle = right.Kind is ChartKind.Empty or ChartKind.Anchor ? key.End : PreviousChartBoundary(input, key.End); single = true; }
        else if (left.MaximumBytes >= 0)
            middle = FloorChartBoundary(input, (int)Math.Min(key.End, (long)key.Start + left.MaximumBytes), key.Start);
        var floor = right.MaximumBytes < 0 ? key.Start : Math.Max(key.Start, key.End - right.MaximumBytes);
        if (middle < floor || middle > key.End) { CompleteChart(frame, false, 0, ref invocation); return; }
        frame = frame with { Phase = 4, Middle = middle, Single = single };
        ReplaceChartFrame(frame, ref invocation);
        PushChartFrame(new(IsChartAtom(right) ? new(node.Right, middle, key.End, 0) : new(node.Left, key.Start, middle, 0)), ref invocation);
    }

    private void NextChartSplit(ChartFrame frame, ChartNode node, ReadOnlySpan<byte> input,
        ref Utf8SliceInvocation invocation)
    {
        var middle = frame.Single ? -1 : PreviousChartBoundary(input, frame.Middle);
        var right = _program.Chart[node.Right];
        var floor = right.MaximumBytes < 0 ? frame.Key.Start : Math.Max(frame.Key.Start, frame.Key.End - right.MaximumBytes);
        if (middle < floor) { CompleteChart(frame, false, 0, ref invocation); return; }
        frame = frame with { Phase = 4, Middle = middle };
        ReplaceChartFrame(frame, ref invocation);
        PushChartFrame(new(IsChartAtom(right) ? new(node.Right, middle, frame.Key.End, 0) : new(node.Left, frame.Key.Start, middle, 0)), ref invocation);
    }

    private void BeginChartRepeat(ChartFrame frame, ChartNode node, int middle,
        ref Utf8SliceInvocation invocation)
    {
        if (middle == frame.Key.Start && frame.Key.Count >= node.A) middle = -1;
        if (middle < frame.Key.Start) { CompleteChart(frame, false, 0, ref invocation); return; }
        frame = frame with { Phase = 6, Middle = middle };
        ReplaceChartFrame(frame, ref invocation);
        PushChartFrame(new(new(node.Left, frame.Key.Start, middle, 0)), ref invocation);
    }

    private bool ChartMember(ChartKey key, ReadOnlySpan<byte> input, ref Utf8SliceInvocation invocation)
    {
        // Selection continuations can share this owner below the dependency
        // stack. Evaluation never visits them and no CLR recursion is involved.
        var baseDepth = _chartFrames.Count;
        PushChartFrame(new(key), ref invocation);
        while (_chartFrames.Count > baseDepth)
        {
            Charge(ref invocation);
            var frame = _chartFrames[_chartFrames.Count - 1];
            var node = _program.Chart[frame.Key.Node]; var k = frame.Key;
            if (frame.Phase == 0)
            {
                if (node.MaximumBytes >= 0 && k.End - k.Start > node.MaximumBytes)
                { _chartFrames.Pop(); _chartResult = false; continue; }
                if (IsChartAtom(node)) { CompleteChart(frame, MatchChartAtom(node, k, input, ref invocation), 0, ref invocation); continue; }
                if (_chartMemo is not null && _chartMemo.TryGetValue(k, out var cached))
                { _chartFrames.Pop(); _chartResult = cached >= 0; continue; }
                switch (node.Kind)
                {
                    case ChartKind.Group:
                        ReplaceChartFrame(frame with { Phase = 1 }, ref invocation);
                        PushChartFrame(new(new(node.Left, k.Start, k.End, 0)), ref invocation); break;
                    case ChartKind.Alternate:
                        ReplaceChartFrame(frame with { Phase = 2 }, ref invocation);
                        PushChartFrame(new(new(node.Left, k.Start, k.End, 0)), ref invocation); break;
                    case ChartKind.Concat: BeginChartSplit(frame, node, input, ref invocation); break;
                    case ChartKind.Repeat:
                        if (k.Start == k.End && k.Count >= node.A) CompleteChart(frame, true, k.End, ref invocation);
                        else if (node.B >= 0 && k.Count >= node.B) CompleteChart(frame, false, 0, ref invocation);
                        else
                        {
                            var maximum = _program.Chart[node.Left].MaximumBytes;
                            var middle = maximum < 0 ? k.End : FloorChartBoundary(input, (int)Math.Min(k.End, (long)k.Start + maximum), k.Start);
                            BeginChartRepeat(frame, node, middle, ref invocation);
                        }
                        break;
                }
                continue;
            }
            switch (frame.Phase)
            {
                case 1: CompleteChart(frame, _chartResult, 0, ref invocation); break;
                case 2:
                    if (_chartResult) CompleteChart(frame, true, 0, ref invocation);
                    else
                    {
                        ReplaceChartFrame(frame with { Phase = 3 }, ref invocation);
                        PushChartFrame(new(new(node.Right, k.Start, k.End, 0)), ref invocation);
                    }
                    break;
                case 3: CompleteChart(frame, _chartResult, 1, ref invocation); break;
                case 4:
                    if (!_chartResult) NextChartSplit(frame, node, input, ref invocation);
                    else
                    {
                        ReplaceChartFrame(frame with { Phase = 5 }, ref invocation);
                        PushChartFrame(new(IsChartAtom(_program.Chart[node.Right]) ? new(node.Left, k.Start, frame.Middle, 0) : new(node.Right, frame.Middle, k.End, 0)), ref invocation);
                    }
                    break;
                case 5:
                    if (_chartResult) CompleteChart(frame, true, frame.Middle, ref invocation);
                    else NextChartSplit(frame, node, input, ref invocation);
                    break;
                case 6:
                    if (!_chartResult) BeginChartRepeat(frame, node, PreviousChartBoundary(input, frame.Middle), ref invocation);
                    else
                    {
                        ReplaceChartFrame(frame with { Phase = 7 }, ref invocation);
                        PushChartFrame(new(new(k.Node, frame.Middle, k.End, node.B < 0 ? Math.Min(k.Count + 1, node.A + 1) : k.Count + 1)), ref invocation);
                    }
                    break;
                case 7:
                    if (_chartResult) CompleteChart(frame, true, frame.Middle, ref invocation);
                    else BeginChartRepeat(frame, node, PreviousChartBoundary(input, frame.Middle), ref invocation);
                    break;
                default: throw new InvalidOperationException("An invalid POSIX interval dependency was evaluated.");
            }
        }
        return _chartResult;
    }

    private void SelectChart(ReadOnlySpan<byte> input, scoped Span<int> state, int start, int end,
        ref Utf8SliceInvocation invocation)
    {
        var root = new ChartKey(0, start, end, 0);
        if (!ChartMember(root, input, ref invocation))
            throw new InvalidOperationException("The selected POSIX whole interval is absent from its capture language.");
        PushChartFrame(new(root, 8), ref invocation);
        while (_chartFrames.Count != 0)
        {
            Charge(ref invocation);
            var key = _chartFrames[_chartFrames.Count - 1].Key; _chartFrames.Pop();
            var node = _program.Chart[key.Node];
            switch (node.Kind)
            {
                case ChartKind.Group:
                    for (var slot = node.A; slot <= node.LastGroup; slot++)
                    {
                        if (((slot - node.A) & 63) == 0) Charge(ref invocation);
                        state[_openOffset + slot - 1] = -1; state[_lastOffset + slot - 1] = -1;
                    }
                    state[_openOffset + node.A - 1] = key.Start; state[_lastOffset + node.A - 1] = key.End;
                    PushChartFrame(new(new(node.Left, key.Start, key.End, 0), 8), ref invocation); break;
                case ChartKind.Concat:
                    var middle = _chartMemo![key];
                    PushChartFrame(new(new(node.Right, middle, key.End, 0), 8), ref invocation);
                    PushChartFrame(new(new(node.Left, key.Start, middle, 0), 8), ref invocation); break;
                case ChartKind.Alternate:
                    PushChartFrame(new(new(_chartMemo![key] == 0 ? node.Left : node.Right, key.Start, key.End, 0), 8), ref invocation); break;
                case ChartKind.Repeat:
                    if (key.Start == key.End && key.Count >= node.A)
                    {
                        if (key.Count == 0 && node.B != 0 && ChartMember(new(node.Left, key.Start, key.Start, 0), input, ref invocation))
                            PushChartFrame(new(new(node.Left, key.Start, key.Start, 0), 8), ref invocation);
                        break;
                    }
                    middle = _chartMemo![key];
                    PushChartFrame(new(new(key.Node, middle, key.End, node.B < 0 ? Math.Min(key.Count + 1, node.A + 1) : key.Count + 1), 8), ref invocation);
                    PushChartFrame(new(new(node.Left, key.Start, middle, 0), 8), ref invocation); break;
            }
        }
    }

    private void ResetChart(ref Utf8SliceInvocation invocation)
    {
        Clear(ref _chartFrames, ref invocation);
        if (_chartMemo is { Count: > 0 })
        {
            Charge(ref invocation);
            _chartMemo.Clear();
            Charge(ref invocation);
        }
    }

    private void ReleaseChart(ref Utf8SliceInvocation invocation)
    {
        _chartFrames.Release(ref invocation);
        invocation.Ledger.Release(_chartBytes);
        _chartMemo = null; _chartCapacity = 0; _chartBytes = 0;
    }
}
