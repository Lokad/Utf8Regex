using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.PythonRe;

// find-all entry points, split from the Python facade along the find-all ownership boundary.
/// <summary>Find-all entry points for <see cref="Utf8PythonRegex"/>.</summary>
public sealed partial class Utf8PythonRegex
{

    private Utf8PythonFindAllUtf8Result FindAllToUtf8Core(ReadOnlySpan<byte> input, int startOffsetInBytes, CancellationToken cancellationToken)
    {
        using var programUse = EnterProgramUse();
        if (((HasFiniteExecutionLimits || cancellationToken.CanBeCanceled) && !HasPreparedExactLiteralFamily) ||
            _canMatchEmpty || CaptureGroupCount > 0 || !_canUseUtf8IterationFastPath)
        {
            FindAllScalarUtf8(input, startOffsetInBytes, cancellationToken, out var scalarResult);
            return scalarResult;
        }
        ValidateStartOffset(input, startOffsetInBytes);
        if (TryCollectUtf8MatchRanges(input, startOffsetInBytes, cancellationToken, out var ranges))
        {
            try
            {
                var values = ranges.AllocateResultArray<byte[]>(ranges.Count);
                for (var i = 0; i < values.Length; i++) values[i] = ranges.GetBytes(input, ranges[i]);
                ranges.Checkpoint();
                return new Utf8PythonFindAllUtf8Result
                {
                    Shape = Utf8PythonFindAllShape.FullMatch,
                    ScalarValues = values,
                    TupleValues = []
                };
            }
            catch (InvalidOperationException error) when (error is not Utf8ResourceLimitExceededException && ranges.HasResourceFailure)
            {
                throw ranges.ResourceFailure();
            }
            catch (Utf8ExecutionDeadlineExpiredException)
            {
                throw CreateMatchTimeoutException(input, Pattern);
            }
            finally { ranges.Dispose(); }
        }
        FindAllScalarUtf8(input, startOffsetInBytes, cancellationToken, out var remaining);
        return remaining;
    }

    /// <summary>Returns shaped UTF-8 <c>findall</c> values after a byte offset, observing cancellation during matching and required result processing.</summary>
    /// <param name="input">The well-formed UTF-8 input to match against.</param>
    /// <param name="startOffsetInBytes">The zero-based UTF-8 byte offset where matching starts.</param>
    /// <param name="cancellationToken">The token to observe at entry and during bounded operation checkpoints.</param>
    /// <exception cref="OperationCanceledException">Cancellation was requested through the supplied token.</exception>
    public Utf8PythonFindAllUtf8Result FindAllToUtf8(ReadOnlySpan<byte> input, int startOffsetInBytes = 0, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return FindAllToUtf8Core(input, startOffsetInBytes, cancellationToken);
    }

}
