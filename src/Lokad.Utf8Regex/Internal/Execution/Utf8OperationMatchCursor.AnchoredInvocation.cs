using Lokad.Utf8Regex.Internal.Input;
using Lokad.Utf8Regex.Internal.Planning;

namespace Lokad.Utf8Regex.Internal.Execution;

internal ref partial struct Utf8OperationMatchCursor
{
    private bool _hasAnchoredLiteralInvocation;

    internal Utf8OperationMatchCursor(ReadOnlySpan<byte> input, byte[] literal,
        Utf8FallbackDirectFamilyKind kind, Utf8ValidationResult validation, Utf8SliceInvocation invocation)
        : this(input, literal, Utf8ExecutionDeadline.Infinite, kind)
    {
        // The borrowed invocation already owns the deadline and shared state.
        // Do not allocate a second finite invocation in the base constructor.
        _budget = invocation.Budget.Deadline;
        _sliceInvocation = invocation;
        _asciiInput = validation.IsAscii;
        _hasAnchoredLiteralInvocation = true;
        if (kind == Utf8FallbackDirectFamilyKind.AnchoredAsciiLiteralPrefix)
            _mode = EnumeratorMode.AnchoredAsciiLiteralPrefix;
    }

    // Full-subject anchors retain their original context at a nonzero floor.
    // Candidate verification and UTF-16 projection commit with the range only
    // after the final invocation checkpoint; denied moves retain all progress.
    internal Utf8ExecutionLimitKind TryMoveNextAnchoredLiteral(out bool found)
    {
        found = false;
        var breach = _sliceInvocation.TryChargeInputStep();
        if (breach != Utf8ExecutionLimitKind.None) return breach;
        _sliceInvocation.Budget.ThrowIfCancellationRequested();
        if (_nativeStructuralExhausted) return Utf8ExecutionLimitKind.None;
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
            _nativeStructuralExhausted = true;
            return Utf8ExecutionLimitKind.None;
        }
        breach = Utf8ProjectionExecutor.TryProjectMatch(new Utf8ProjectionPlan(_asciiInput
                ? Utf8ProjectionKind.ByteOnly : Utf8ProjectionKind.Utf16Incremental), _input,
            _consumed, _consumedUtf16, start, literal.Length, literal.Length, ref _sliceInvocation,
            out var projected, out var consumed, out var consumedUtf16);
        if (breach != Utf8ExecutionLimitKind.None) return breach;
        breach = _sliceInvocation.TryChargeInputStep();
        if (breach != Utf8ExecutionLimitKind.None) return breach;
        _sliceInvocation.Budget.ThrowIfCancellationRequested();
        _current = projected;
        _consumed = consumed;
        _consumedUtf16 = consumedUtf16;
        _nativeStructuralExhausted = true;
        found = true;
        return Utf8ExecutionLimitKind.None;
    }
}
