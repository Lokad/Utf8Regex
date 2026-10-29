namespace Lokad.Utf8Regex.PythonRe;

// iteration entry points, split from the Python facade along the iteration ownership boundary.
/// <summary>Iteration entry points for <see cref="Utf8PythonRegex"/>.</summary>
public sealed partial class Utf8PythonRegex
{
    /// <summary>Materializes detailed data for every non-overlapping match.</summary>
    public Utf8PythonDetailedMatchData[] FindIterDetailed(ReadOnlySpan<byte> input) => FindIterDetailed(input, 0);

    /// <summary>Materializes detailed matches after a zero-based UTF-8 byte offset.</summary>
    public Utf8PythonDetailedMatchData[] FindIterDetailed(ReadOnlySpan<byte> input, int startOffsetInBytes) => FindIterDetailedCore(input, startOffsetInBytes, CancellationToken.None);

    private Utf8PythonDetailedMatchData[] FindIterDetailedCore(ReadOnlySpan<byte> input, int startOffsetInBytes, CancellationToken cancellationToken)
    {
        using var programUse = EnterProgramUse();
        FindIterScalar(input, startOffsetInBytes, cancellationToken, out var scalarResult);
        return scalarResult;
    }

    /// <summary>Materializes detailed matches after a zero-based UTF-8 byte offset, observing cancellation during validation, matching and required result processing.</summary>
    /// <param name="input">The well-formed UTF-8 input to match against.</param>
    /// <param name="startOffsetInBytes">The zero-based UTF-8 byte offset where matching starts.</param>
    /// <param name="cancellationToken">The token to observe at entry and during bounded operation checkpoints.</param>
    /// <exception cref="OperationCanceledException">Cancellation was requested through the supplied token.</exception>
    public Utf8PythonDetailedMatchData[] FindIterDetailed(ReadOnlySpan<byte> input, int startOffsetInBytes, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return FindIterDetailedCore(input, startOffsetInBytes, cancellationToken);
    }

}
