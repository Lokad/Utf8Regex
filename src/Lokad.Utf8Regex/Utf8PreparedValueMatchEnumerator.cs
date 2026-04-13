using System.Runtime.CompilerServices;
using Lokad.Utf8Regex.Internal.Planning;

namespace Lokad.Utf8Regex;

// PCRE2-INTEGRATION-POINT
internal ref struct Utf8PreparedValueMatchEnumerator
{
    private readonly ReadOnlySpan<byte> _input;
    private readonly PreparedSearcher _preparedSearcher;
    private int _cursor;
    private int _startOffsetInBytes;
    private int _endOffsetInBytes;

    public Utf8PreparedValueMatchEnumerator(ReadOnlySpan<byte> input, PreparedSearcher preparedSearcher, int startOffsetInBytes)
    {
        _input = input;
        _preparedSearcher = preparedSearcher;
        _cursor = startOffsetInBytes;
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
        if (!_preparedSearcher.TryFindNextNonOverlappingLiteral(_input, ref _cursor, out _startOffsetInBytes, out var matchedLength))
        {
            _startOffsetInBytes = 0;
            _endOffsetInBytes = 0;
            return false;
        }

        _endOffsetInBytes = _startOffsetInBytes + matchedLength;
        return true;
    }
}
