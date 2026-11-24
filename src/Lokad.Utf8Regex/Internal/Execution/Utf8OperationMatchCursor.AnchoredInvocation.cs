using Lokad.Utf8Regex.Internal.Input;
using Lokad.Utf8Regex.Internal.Planning;

namespace Lokad.Utf8Regex.Internal.Execution;

internal ref partial struct Utf8OperationMatchCursor
{
    // Both cursor edges pass an exact-length slice. The local length check
    // lets the JIT eliminate the input bounds check inside the loop.
    private static bool FoldedAnchoredLiteralEquals(ReadOnlySpan<byte> input, byte[] literal)
    {
        if (input.Length < literal.Length)
            return false;

        for (var index = 0; index < literal.Length; index++)
        {
            var candidate = input[index];
            var expected = literal[index];
            if (candidate == expected)
                continue;
            if (ToUpperAscii(candidate) != ToUpperAscii(expected))
                return false;
        }
        return true;
    }

    private static byte ToUpperAscii(byte value) =>
        value is >= (byte)'a' and <= (byte)'z' ? (byte)(value - 32) : value;

    internal Utf8OperationMatchCursor(ReadOnlySpan<byte> input, byte[] literal, Utf8AnchoredLiteralKind kind, Utf8SliceInvocation invocation)
        : this(input, literal, Utf8ExecutionDeadline.Infinite, kind)
    {
        // The borrowed invocation already owns the deadline and shared state.
        // Do not allocate a second finite invocation in the base constructor.
        _budget = invocation.Budget.Deadline;
        _sliceInvocation = invocation;
        _hasAnchoredLiteralInvocation = true;
    }

    // Full-subject anchors retain their original context at a nonzero floor.
    // Candidate verification and publication commit with the range only
    // after the final invocation checkpoint; denied moves retain all progress.
    internal Utf8ExecutionLimitKind TryMoveNextAnchoredLiteral(out bool found)
    {
        found = false;
        var breach = _sliceInvocation.TryChargeInputStep();
        if (breach != Utf8ExecutionLimitKind.None) return breach;
        _sliceInvocation.Budget.ThrowIfCancellationRequested();
        if (_anchoredExhausted) return Utf8ExecutionLimitKind.None;
        var literal = _literal ?? throw new InvalidOperationException("An anchored cursor needs its literal.");
        var prefix = _mode is EnumeratorMode.AnchoredAsciiLiteralPrefix or EnumeratorMode.AnchoredFoldedAsciiLiteralPrefix;
        var folded = _mode is EnumeratorMode.AnchoredFoldedAsciiLiteralPrefix or
            EnumeratorMode.AnchoredFoldedAsciiLiteralSuffix or EnumeratorMode.AnchoredFoldedAsciiLiteralSuffixStrictEnd;
        var end = prefix ? literal.Length : _input.Length;
        if (_mode is EnumeratorMode.AnchoredAsciiLiteralSuffix or EnumeratorMode.AnchoredFoldedAsciiLiteralSuffix &&
            end > literal.Length && _input[end - 1] == (byte)'\n') end--;
        var start = end - literal.Length;
        var equal = false;
        if (start >= _consumed && end <= _input.Length)
        {
            breach = Utf8SearchExecutor.TryCompareLiteral(_input.Slice(start, literal.Length), literal, folded,
                ref _sliceInvocation, out equal);
            if (breach != Utf8ExecutionLimitKind.None) return breach;
        }
        if (!equal)
        {
            breach = _sliceInvocation.TryChargeInputStep();
            if (breach != Utf8ExecutionLimitKind.None) return breach;
            _sliceInvocation.Budget.ThrowIfCancellationRequested();
            _anchoredExhausted = true;
            return Utf8ExecutionLimitKind.None;
        }
        var consumed = (start) + (literal.Length);
        breach = Utf8SliceBufferWork.TryCreateMatchRange(start, (start) + (literal.Length), ref _sliceInvocation, out var projected);
        if (breach != Utf8ExecutionLimitKind.None) return breach;
        breach = _sliceInvocation.TryChargeInputStep();
        if (breach != Utf8ExecutionLimitKind.None) return breach;
        _sliceInvocation.Budget.ThrowIfCancellationRequested();
        _current = projected;
        _consumed = consumed;
        _anchoredExhausted = true;
        found = true;
        return Utf8ExecutionLimitKind.None;
    }
}
