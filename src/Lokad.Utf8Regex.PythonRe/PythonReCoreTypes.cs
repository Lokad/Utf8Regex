namespace Lokad.Utf8Regex.PythonRe;

internal readonly struct PythonReGroupData
{
    public int Number { get; init; }

    public bool Success { get; init; }

    public int StartOffsetInBytes { get; init; }

    public int EndOffsetInBytes { get; init; }


}

/// <summary>Provides a stack-only match view over the original UTF-8 input.</summary>
/// <remarks>The view and returned spans must not outlive the input supplied to the matching operation.</remarks>
public readonly ref struct Utf8PythonValueMatch
{
    private readonly ReadOnlySpan<byte> _input;
    private readonly PythonReGroupData _data;

    internal Utf8PythonValueMatch(ReadOnlySpan<byte> input, PythonReGroupData data)
    {
        _input = input;
        _data = data;
    }

    /// <summary>Gets whether the operation found a match.</summary>
    public bool Success => _data.Success;

    /// <summary>Gets the match start as a zero-based byte offset, or zero after no match.</summary>
    public int StartOffsetInBytes => _data.StartOffsetInBytes;

    /// <summary>Gets the exclusive match end as a zero-based byte offset, or zero after no match.</summary>
    public int EndOffsetInBytes => _data.EndOffsetInBytes;

    /// <summary>Gets the match value from the original UTF-8 input.</summary>
    /// <returns>An empty span after no match.</returns>
    public ReadOnlySpan<byte> GetValueBytes()
    {
        if (!Success)
        {
            return ReadOnlySpan<byte>.Empty;
        }

        return _input[StartOffsetInBytes..EndOffsetInBytes];
    }

    internal static Utf8PythonValueMatch Create(ReadOnlySpan<byte> input, PythonReGroupData data) => new(input, data);
}

/// <summary>Contains the UTF-8 result and replacement count from a CPython <c>subn</c> operation.</summary>
public readonly struct Utf8PythonSubnUtf8Result
{
    /// <summary>Gets the replaced text as owned UTF-8 bytes.</summary>
    public byte[] ResultBytes { get; init; }

    /// <summary>Gets the number of substitutions performed.</summary>
    public int ReplacementCount { get; init; }
}

