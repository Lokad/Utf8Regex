using System.Runtime.CompilerServices;
using Lokad.Utf8Regex.Internal.Input;

namespace Lokad.Utf8Regex.Internal.Execution;

using Lokad.Utf8Regex.Internal.Search;

internal ref struct Utf8PreparedValueMatchEnumerator
{
    private readonly ReadOnlySpan<byte> _input;
    private readonly PreparedSearcher _preparedSearcher;
    private int _cursor;
    private PreparedMultiLiteralScanState _multiLiteralScanState;
    private int _startOffsetInBytes;
    private int _endOffsetInBytes;

    public Utf8PreparedValueMatchEnumerator(
        ReadOnlySpan<byte> input,
        PreparedSearcher preparedSearcher,
        Utf8BytePosition start)
    {
        _input = input;
        _preparedSearcher = preparedSearcher;
        _cursor = start.Value;
        _multiLiteralScanState = new PreparedMultiLiteralScanState(start.Value, start.Value, 0);
        _startOffsetInBytes = 0;
        _endOffsetInBytes = 0;
    }

    public int StartOffsetInBytes
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _startOffsetInBytes;
    }

    public int EndOffsetInBytes
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _endOffsetInBytes;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool MoveNext()
    {
        int matchedLength;
        var found = _preparedSearcher.Kind == PreparedSearcherKind.MultiLiteral
            ? _preparedSearcher.TryFindNextNonOverlappingLength(
                _input,
                ref _multiLiteralScanState,
                out _startOffsetInBytes,
                out matchedLength)
            : _preparedSearcher.TryFindNextNonOverlappingLiteral(
                _input,
                ref _cursor,
                out _startOffsetInBytes,
                out matchedLength);
        if (!found)
        {
            _startOffsetInBytes = 0;
            _endOffsetInBytes = 0;
            return false;
        }

        _endOffsetInBytes = _startOffsetInBytes + matchedLength;
        return true;
    }
}
