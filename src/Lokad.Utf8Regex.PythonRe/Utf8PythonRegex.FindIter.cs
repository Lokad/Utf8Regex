namespace Lokad.Utf8Regex.PythonRe;

// PLAN.md D09: iteration entry points, split from the Python facade along the iteration ownership boundary.
/// <summary>Iteration entry points for <see cref="Utf8PythonRegex"/>.</summary>
public sealed partial class Utf8PythonRegex
{
    /// <summary>Materializes detailed data for every non-overlapping match.</summary>
    public Utf8PythonDetailedMatchData[] FindIterDetailed(ReadOnlySpan<byte> input) => FindIterDetailed(input, 0);

    /// <summary>Materializes detailed matches after a zero-based UTF-8 byte offset.</summary>
    public Utf8PythonDetailedMatchData[] FindIterDetailed(ReadOnlySpan<byte> input, int startOffsetInBytes) => FindIterDetailedCore(input, startOffsetInBytes, CancellationToken.None);

    private Utf8PythonDetailedMatchData[] FindIterDetailedCore(ReadOnlySpan<byte> input, int startOffsetInBytes, CancellationToken cancellationToken)
    {
        FindIterScalar(input, startOffsetInBytes, cancellationToken, out var scalarResult);
        return scalarResult;
    }

    /// <summary>Materializes detailed matches after a zero-based UTF-8 byte offset, observing cancellation before matching begins.</summary>
    /// <param name="input">The well-formed UTF-8 input to match against.</param>
    /// <param name="startOffsetInBytes">The zero-based UTF-8 byte offset where matching starts.</param>
    /// <param name="cancellationToken">The token to observe before matching begins.</param>
    /// <exception cref="OperationCanceledException">The token was already canceled.</exception>
    public Utf8PythonDetailedMatchData[] FindIterDetailed(ReadOnlySpan<byte> input, int startOffsetInBytes, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return FindIterDetailedCore(input, startOffsetInBytes, cancellationToken);
    }

}
