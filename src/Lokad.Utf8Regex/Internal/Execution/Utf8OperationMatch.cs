namespace Lokad.Utf8Regex.Internal.Execution;

internal readonly struct Utf8OperationMatch
{
    private readonly Utf8ValueMatch _valueMatch;

    public Utf8OperationMatch(Utf8ValueMatch valueMatch, int branchId, Utf8CaptureSlots? captureSlots)
    {
        _valueMatch = valueMatch;
        BranchId = branchId;
        CaptureSlots = captureSlots;
    }

    public bool Success => _valueMatch.Success;

    public bool IsByteAligned => _valueMatch.IsByteAligned;

    public int IndexInUtf16 => _valueMatch.IndexInUtf16;

    public int LengthInUtf16 => _valueMatch.LengthInUtf16;

    public int IndexInBytes => _valueMatch.IndexInBytes;

    public int LengthInBytes => _valueMatch.LengthInBytes;

    public int BranchId { get; }

    public Utf8CaptureSlots? CaptureSlots { get; }

    public Utf8ValueMatch ToValueMatch() => _valueMatch;

    public static Utf8OperationMatch NoMatch => new(Utf8ValueMatch.NoMatch, branchId: -1, captureSlots: null);

    public static implicit operator Utf8OperationMatch(Utf8ValueMatch valueMatch) =>
        new(valueMatch, branchId: 0, captureSlots: null);
}
