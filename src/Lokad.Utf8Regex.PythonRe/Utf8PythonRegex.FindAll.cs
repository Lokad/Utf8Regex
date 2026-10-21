using System.Buffers;
using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Caching;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Input;
using Lokad.Utf8Regex.Internal.Planning;

namespace Lokad.Utf8Regex.PythonRe;

// PLAN.md D09: find-all entry points, split from the Python facade along the find-all ownership boundary.
/// <summary>Find-all entry points for <see cref="Utf8PythonRegex"/>.</summary>
public sealed partial class Utf8PythonRegex
{
    /// <summary>Materializes all non-overlapping matches.</summary>
    public Utf8PythonMatchData[] FindAll(ReadOnlySpan<byte> input) => FindAll(input, 0);

    /// <summary>Materializes all matches at or after a zero-based UTF-8 byte offset.</summary>
    public Utf8PythonMatchData[] FindAll(ReadOnlySpan<byte> input, int startOffsetInBytes) => FindAllCore(input, startOffsetInBytes, CancellationToken.None);

    private Utf8PythonMatchData[] FindAllCore(ReadOnlySpan<byte> input, int startOffsetInBytes, CancellationToken cancellationToken)
    {
        if (((HasFiniteExecutionLimits || cancellationToken.CanBeCanceled) && !HasPreparedExactLiteralFamily) ||
            !_canUseUtf8IterationFastPath)
        {
            FindAllScalar(input, startOffsetInBytes, cancellationToken, out var scalarResult);
            return scalarResult;
        }
        ValidateStartOffset(input, startOffsetInBytes);
        if (TryFindAllViaUtf8Regex(input, startOffsetInBytes, cancellationToken, out var utf8Matches))
        {
            return utf8Matches;
        }

        FindAllScalar(input, startOffsetInBytes, cancellationToken, out var remaining);
        return remaining;
    }

    /// <summary>Materializes all matches at or after a zero-based UTF-8 byte offset, observing cancellation during validation, matching and required result processing.</summary>
    /// <param name="input">The well-formed UTF-8 input to match against.</param>
    /// <param name="startOffsetInBytes">The zero-based UTF-8 byte offset where matching starts.</param>
    /// <param name="cancellationToken">The token to observe at entry and during bounded operation checkpoints.</param>
    /// <exception cref="OperationCanceledException">Cancellation was requested through the supplied token.</exception>
    public Utf8PythonMatchData[] FindAll(ReadOnlySpan<byte> input, int startOffsetInBytes, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return FindAllCore(input, startOffsetInBytes, cancellationToken);
    }

    /// <summary>Returns Python <c>findall</c> string values using capture-dependent shaping.</summary>
    public Utf8PythonFindAllResult FindAllToStrings(ReadOnlySpan<byte> input) => FindAllToStrings(input, 0);

    /// <summary>Returns shaped <c>findall</c> strings after a zero-based UTF-8 byte offset.</summary>
    public Utf8PythonFindAllResult FindAllToStrings(ReadOnlySpan<byte> input, int startOffsetInBytes) => FindAllToStringsCore(input, startOffsetInBytes, CancellationToken.None);

    private Utf8PythonFindAllResult FindAllToStringsCore(ReadOnlySpan<byte> input, int startOffsetInBytes, CancellationToken cancellationToken)
    {
        if (((HasFiniteExecutionLimits || cancellationToken.CanBeCanceled) && !HasPreparedExactLiteralFamily) ||
            (!_canUseUtf8IterationFastPath && !CanUseCountedExactLiteralFindAll))
        {
            FindAllScalarStrings(input, startOffsetInBytes, cancellationToken, out var scalarResult);
            return scalarResult;
        }
        ValidateStartOffset(input, startOffsetInBytes);
        if (TryFindAllCanonicalLiteral(input, startOffsetInBytes, out var canonicalResult)) return canonicalResult;
        if (TryCollectUtf8MatchRanges(input, startOffsetInBytes, cancellationToken, out var ranges))
        {
            try
            {
                var values = ranges.AllocateResultArray<string>(ranges.Count);
                for (var i = 0; i < values.Length; i++)
                {
                    if ((i & 255) == 0) ranges.Checkpoint();
                    values[i] = _repeatedExactFindAllString ?? ranges.GetText(input, ranges[i]);
                }
                ranges.Checkpoint();
                return new Utf8PythonFindAllResult
                {
                    Shape = Utf8PythonFindAllShape.FullMatch, ScalarValues = values, TupleValues = [],
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
        FindAllScalarStrings(input, startOffsetInBytes, cancellationToken, out var remaining);
        return remaining;
    }

    /// <summary>Returns shaped <c>findall</c> strings after a zero-based UTF-8 byte offset, observing cancellation during validation, matching and required result processing.</summary>
    /// <param name="input">The well-formed UTF-8 input to match against.</param>
    /// <param name="startOffsetInBytes">The zero-based UTF-8 byte offset where matching starts.</param>
    /// <param name="cancellationToken">The token to observe at entry and during bounded operation checkpoints.</param>
    /// <exception cref="OperationCanceledException">Cancellation was requested through the supplied token.</exception>
    public Utf8PythonFindAllResult FindAllToStrings(ReadOnlySpan<byte> input, int startOffsetInBytes, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return FindAllToStringsCore(input, startOffsetInBytes, cancellationToken);
    }

    /// <summary>Returns Python <c>findall</c> values as owned UTF-8 byte arrays.</summary>
    public Utf8PythonFindAllUtf8Result FindAllToUtf8(ReadOnlySpan<byte> input) => FindAllToUtf8(input, 0);

    /// <summary>Returns shaped UTF-8 <c>findall</c> values after a byte offset.</summary>
    public Utf8PythonFindAllUtf8Result FindAllToUtf8(ReadOnlySpan<byte> input, int startOffsetInBytes) => FindAllToUtf8Core(input, startOffsetInBytes, CancellationToken.None);

    private Utf8PythonFindAllUtf8Result FindAllToUtf8Core(ReadOnlySpan<byte> input, int startOffsetInBytes, CancellationToken cancellationToken)
    {
        if (((HasFiniteExecutionLimits || cancellationToken.CanBeCanceled) && !HasPreparedExactLiteralFamily) ||
            !_canUseUtf8IterationFastPath)
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
                    Shape = Utf8PythonFindAllShape.FullMatch, ScalarValues = values, TupleValues = [],
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

    /// <summary>Returns shaped UTF-8 <c>findall</c> values after a byte offset, observing cancellation during validation, matching and required result processing.</summary>
    /// <param name="input">The well-formed UTF-8 input to match against.</param>
    /// <param name="startOffsetInBytes">The zero-based UTF-8 byte offset where matching starts.</param>
    /// <param name="cancellationToken">The token to observe at entry and during bounded operation checkpoints.</param>
    /// <exception cref="OperationCanceledException">Cancellation was requested through the supplied token.</exception>
    public Utf8PythonFindAllUtf8Result FindAllToUtf8(ReadOnlySpan<byte> input, int startOffsetInBytes, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return FindAllToUtf8Core(input, startOffsetInBytes, cancellationToken);
    }

}
