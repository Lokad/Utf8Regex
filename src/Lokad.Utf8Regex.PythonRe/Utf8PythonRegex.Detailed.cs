namespace Lokad.Utf8Regex.PythonRe;

// detailed match entry points, split from the Python facade along the detailed-match ownership boundary.
/// <summary>Detailed match entry points for <see cref="Utf8PythonRegex"/>.</summary>
public sealed partial class Utf8PythonRegex
{
    /// <summary>Finds the first match and exposes its capture context.</summary>
    public Utf8PythonMatchContext SearchDetailed(ReadOnlySpan<byte> input) => SearchDetailed(input, 0);

    /// <summary>Finds the first capture context at or after a zero-based UTF-8 byte offset.</summary>
    public Utf8PythonMatchContext SearchDetailed(ReadOnlySpan<byte> input, int startOffsetInBytes)
        => SearchDetailedCore(input, startOffsetInBytes, CancellationToken.None);

    private Utf8PythonMatchContext SearchDetailedCore(ReadOnlySpan<byte> input, int startOffsetInBytes, CancellationToken cancellationToken)
    {
        using var programUse = EnterProgramUse();
        return MatchContextScalar(input, startOffsetInBytes, 0, cancellationToken);
    }

    /// <summary>Finds the first capture context at or after a zero-based UTF-8 byte offset, observing cancellation during matching and required result processing.</summary>
    /// <param name="input">The well-formed UTF-8 input to match against.</param>
    /// <param name="startOffsetInBytes">The zero-based UTF-8 byte offset where matching starts.</param>
    /// <param name="cancellationToken">The token to observe at entry and during bounded operation checkpoints.</param>
    /// <exception cref="OperationCanceledException">Cancellation was requested through the supplied token.</exception>
    public Utf8PythonMatchContext SearchDetailed(ReadOnlySpan<byte> input, int startOffsetInBytes, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return SearchDetailedCore(input, startOffsetInBytes, cancellationToken);
    }

    /// <summary>Matches at the beginning of the input and exposes its capture context.</summary>
    public Utf8PythonMatchContext MatchDetailed(ReadOnlySpan<byte> input) => MatchDetailed(input, 0);

    /// <summary>Matches at a zero-based UTF-8 byte offset and exposes its capture context.</summary>
    public Utf8PythonMatchContext MatchDetailed(ReadOnlySpan<byte> input, int startOffsetInBytes)
        => MatchDetailedCore(input, startOffsetInBytes, CancellationToken.None);

    private Utf8PythonMatchContext MatchDetailedCore(ReadOnlySpan<byte> input, int startOffsetInBytes, CancellationToken cancellationToken)
    {
        using var programUse = EnterProgramUse();
        return MatchContextScalar(input, startOffsetInBytes, 1, cancellationToken);
    }

    /// <summary>Matches at a zero-based UTF-8 byte offset and exposes its capture context, observing cancellation during matching and required result processing.</summary>
    /// <param name="input">The well-formed UTF-8 input to match against.</param>
    /// <param name="startOffsetInBytes">The zero-based UTF-8 byte offset where matching starts.</param>
    /// <param name="cancellationToken">The token to observe at entry and during bounded operation checkpoints.</param>
    /// <exception cref="OperationCanceledException">Cancellation was requested through the supplied token.</exception>
    public Utf8PythonMatchContext MatchDetailed(ReadOnlySpan<byte> input, int startOffsetInBytes, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return MatchDetailedCore(input, startOffsetInBytes, cancellationToken);
    }

    /// <summary>Matches the complete input and exposes its capture context.</summary>
    public Utf8PythonMatchContext FullMatchDetailed(ReadOnlySpan<byte> input) => FullMatchDetailed(input, 0);

    /// <summary>Matches the complete UTF-8 suffix and exposes its capture context.</summary>
    public Utf8PythonMatchContext FullMatchDetailed(ReadOnlySpan<byte> input, int startOffsetInBytes)
        => FullMatchDetailedCore(input, startOffsetInBytes, CancellationToken.None);

    private Utf8PythonMatchContext FullMatchDetailedCore(ReadOnlySpan<byte> input, int startOffsetInBytes, CancellationToken cancellationToken)
    {
        using var programUse = EnterProgramUse();
        return MatchContextScalar(input, startOffsetInBytes, 2, cancellationToken);
    }

    /// <summary>Matches the complete UTF-8 suffix and exposes its capture context, observing cancellation during matching and required result processing.</summary>
    /// <param name="input">The well-formed UTF-8 input to match against.</param>
    /// <param name="startOffsetInBytes">The zero-based UTF-8 byte offset where matching starts.</param>
    /// <param name="cancellationToken">The token to observe at entry and during bounded operation checkpoints.</param>
    /// <exception cref="OperationCanceledException">Cancellation was requested through the supplied token.</exception>
    public Utf8PythonMatchContext FullMatchDetailed(ReadOnlySpan<byte> input, int startOffsetInBytes, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return FullMatchDetailedCore(input, startOffsetInBytes, cancellationToken);
    }

    /// <summary>Finds and materializes the first match and all capture data.</summary>
    public Utf8PythonDetailedMatchData SearchDetailedData(ReadOnlySpan<byte> input) => SearchDetailedData(input, 0);

    /// <summary>Finds and materializes capture data after a zero-based UTF-8 byte offset.</summary>
    public Utf8PythonDetailedMatchData SearchDetailedData(ReadOnlySpan<byte> input, int startOffsetInBytes)
        => SearchDetailedDataCore(input, startOffsetInBytes, CancellationToken.None);

    private Utf8PythonDetailedMatchData SearchDetailedDataCore(ReadOnlySpan<byte> input, int startOffsetInBytes, CancellationToken cancellationToken)
    {
        using var programUse = EnterProgramUse();
        return MatchDataScalar(input, startOffsetInBytes, 0, cancellationToken);
    }

    /// <summary>Finds and materializes capture data after a zero-based UTF-8 byte offset, observing cancellation during matching and required result processing.</summary>
    /// <param name="input">The well-formed UTF-8 input to match against.</param>
    /// <param name="startOffsetInBytes">The zero-based UTF-8 byte offset where matching starts.</param>
    /// <param name="cancellationToken">The token to observe at entry and during bounded operation checkpoints.</param>
    /// <exception cref="OperationCanceledException">Cancellation was requested through the supplied token.</exception>
    public Utf8PythonDetailedMatchData SearchDetailedData(ReadOnlySpan<byte> input, int startOffsetInBytes, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return SearchDetailedDataCore(input, startOffsetInBytes, cancellationToken);
    }

    /// <summary>Matches at the beginning and materializes all capture data.</summary>
    public Utf8PythonDetailedMatchData MatchDetailedData(ReadOnlySpan<byte> input) => MatchDetailedData(input, 0);

    /// <summary>Matches at a zero-based UTF-8 byte offset and materializes all capture data.</summary>
    public Utf8PythonDetailedMatchData MatchDetailedData(ReadOnlySpan<byte> input, int startOffsetInBytes)
        => MatchDetailedDataCore(input, startOffsetInBytes, CancellationToken.None);

    private Utf8PythonDetailedMatchData MatchDetailedDataCore(ReadOnlySpan<byte> input, int startOffsetInBytes, CancellationToken cancellationToken)
    {
        using var programUse = EnterProgramUse();
        return MatchDataScalar(input, startOffsetInBytes, 1, cancellationToken);
    }

    /// <summary>Matches at a zero-based UTF-8 byte offset and materializes all capture data, observing cancellation during matching and required result processing.</summary>
    /// <param name="input">The well-formed UTF-8 input to match against.</param>
    /// <param name="startOffsetInBytes">The zero-based UTF-8 byte offset where matching starts.</param>
    /// <param name="cancellationToken">The token to observe at entry and during bounded operation checkpoints.</param>
    /// <exception cref="OperationCanceledException">Cancellation was requested through the supplied token.</exception>
    public Utf8PythonDetailedMatchData MatchDetailedData(ReadOnlySpan<byte> input, int startOffsetInBytes, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return MatchDetailedDataCore(input, startOffsetInBytes, cancellationToken);
    }

    /// <summary>Matches the complete input and materializes all capture data.</summary>
    public Utf8PythonDetailedMatchData FullMatchDetailedData(ReadOnlySpan<byte> input) => FullMatchDetailedData(input, 0);

    /// <summary>Matches the complete UTF-8 suffix and materializes all capture data.</summary>
    public Utf8PythonDetailedMatchData FullMatchDetailedData(ReadOnlySpan<byte> input, int startOffsetInBytes)
        => FullMatchDetailedDataCore(input, startOffsetInBytes, CancellationToken.None);

    private Utf8PythonDetailedMatchData FullMatchDetailedDataCore(ReadOnlySpan<byte> input, int startOffsetInBytes, CancellationToken cancellationToken)
    {
        using var programUse = EnterProgramUse();
        return MatchDataScalar(input, startOffsetInBytes, 2, cancellationToken);
    }

    /// <summary>Matches the complete UTF-8 suffix and materializes all capture data, observing cancellation during matching and required result processing.</summary>
    /// <param name="input">The well-formed UTF-8 input to match against.</param>
    /// <param name="startOffsetInBytes">The zero-based UTF-8 byte offset where matching starts.</param>
    /// <param name="cancellationToken">The token to observe at entry and during bounded operation checkpoints.</param>
    /// <exception cref="OperationCanceledException">Cancellation was requested through the supplied token.</exception>
    public Utf8PythonDetailedMatchData FullMatchDetailedData(ReadOnlySpan<byte> input, int startOffsetInBytes, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return FullMatchDetailedDataCore(input, startOffsetInBytes, cancellationToken);
    }

}
