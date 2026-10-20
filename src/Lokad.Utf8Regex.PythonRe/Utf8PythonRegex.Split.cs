namespace Lokad.Utf8Regex.PythonRe;

// PLAN.md D09: split entry points, split from the Python facade along the split ownership boundary.
/// <summary>Split entry points for <see cref="Utf8PythonRegex"/>.</summary>
public sealed partial class Utf8PythonRegex
{
    /// <summary>Splits the complete input with unlimited Python-style splits.</summary>
    public string?[] SplitToStrings(ReadOnlySpan<byte> input) => SplitToStrings(input, 0, 0);

    /// <summary>Splits the input at most the requested number of times.</summary>
    public string?[] SplitToStrings(ReadOnlySpan<byte> input, int maxSplit) =>
        SplitToStrings(input, maxSplit, 0);

    /// <summary>Splits after a zero-based UTF-8 byte offset and returns strings and captures.</summary>
    public string?[] SplitToStrings(ReadOnlySpan<byte> input, int maxSplit, int startOffsetInBytes) => SplitToStringsCore(input, maxSplit, startOffsetInBytes, CancellationToken.None);

    private string?[] SplitToStringsCore(ReadOnlySpan<byte> input, int maxSplit, int startOffsetInBytes, CancellationToken cancellationToken)
    {
        SplitScalar(input, maxSplit, startOffsetInBytes, cancellationToken, true, out _, out var values);
        return values;
    }

    /// <summary>Splits after a zero-based UTF-8 byte offset and returns strings and captures, observing cancellation during validation, matching and required result processing.</summary>
    /// <param name="input">The well-formed UTF-8 input to match against.</param>
    /// <param name="maxSplit">The maximum number of splits to perform.</param>
    /// <param name="startOffsetInBytes">The zero-based UTF-8 byte offset where matching starts.</param>
    /// <param name="cancellationToken">The token to observe at entry and during bounded operation checkpoints.</param>
    /// <exception cref="OperationCanceledException">Cancellation was requested through the supplied token.</exception>
    public string?[] SplitToStrings(ReadOnlySpan<byte> input, int maxSplit, int startOffsetInBytes, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return SplitToStringsCore(input, maxSplit, startOffsetInBytes, cancellationToken);
    }

    /// <summary>Splits the complete input and describes values and captured separators.</summary>
    public Utf8PythonSplitItem[] SplitDetailed(ReadOnlySpan<byte> input) => SplitDetailed(input, 0, 0);

    /// <summary>Performs a bounded detailed split.</summary>
    public Utf8PythonSplitItem[] SplitDetailed(ReadOnlySpan<byte> input, int maxSplit) =>
        SplitDetailed(input, maxSplit, 0);

    /// <summary>Performs a detailed split after a zero-based UTF-8 byte offset.</summary>
    public Utf8PythonSplitItem[] SplitDetailed(ReadOnlySpan<byte> input, int maxSplit, int startOffsetInBytes) => SplitDetailedCore(input, maxSplit, startOffsetInBytes, CancellationToken.None);

    private Utf8PythonSplitItem[] SplitDetailedCore(ReadOnlySpan<byte> input, int maxSplit, int startOffsetInBytes, CancellationToken cancellationToken)
    {
        SplitScalar(input, maxSplit, startOffsetInBytes, cancellationToken, false, out var items, out _);
        return items;
    }

    /// <summary>Performs a detailed split after a zero-based UTF-8 byte offset, observing cancellation during validation, matching and required result processing.</summary>
    /// <param name="input">The well-formed UTF-8 input to match against.</param>
    /// <param name="maxSplit">The maximum number of splits to perform.</param>
    /// <param name="startOffsetInBytes">The zero-based UTF-8 byte offset where matching starts.</param>
    /// <param name="cancellationToken">The token to observe at entry and during bounded operation checkpoints.</param>
    /// <exception cref="OperationCanceledException">Cancellation was requested through the supplied token.</exception>
    public Utf8PythonSplitItem[] SplitDetailed(ReadOnlySpan<byte> input, int maxSplit, int startOffsetInBytes, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return SplitDetailedCore(input, maxSplit, startOffsetInBytes, cancellationToken);
    }

}
