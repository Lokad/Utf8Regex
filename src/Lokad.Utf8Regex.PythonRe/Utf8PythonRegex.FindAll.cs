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
        if (cancellationToken.CanBeCanceled || _canMatchEmpty || _translation.CaptureGroupCount > 0 || !_canUseUtf8IterationFastPath)
        {
            FindAllScalar(input, startOffsetInBytes, cancellationToken, out var scalarResult);
            return scalarResult;
        }
        ValidateStartOffset(input, startOffsetInBytes);
        if (TryFindAllViaUtf8Regex(input, startOffsetInBytes, out var utf8Matches))
        {
            return utf8Matches;
        }

        FindAllScalar(input, startOffsetInBytes, cancellationToken, out var remaining);
        return remaining;
    }

    /// <summary>Materializes all matches at or after a zero-based UTF-8 byte offset, observing cancellation before matching begins.</summary>
    /// <param name="input">The well-formed UTF-8 input to match against.</param>
    /// <param name="startOffsetInBytes">The zero-based UTF-8 byte offset where matching starts.</param>
    /// <param name="cancellationToken">The token to observe before matching begins.</param>
    /// <exception cref="OperationCanceledException">The token was already canceled.</exception>
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
        if (cancellationToken.CanBeCanceled || _canMatchEmpty || _translation.CaptureGroupCount > 0 || !_canUseUtf8IterationFastPath)
        {
            FindAllScalarStrings(input, startOffsetInBytes, cancellationToken, out var scalarResult);
            return scalarResult;
        }
        ValidateStartOffset(input, startOffsetInBytes);
        if (_translation.CaptureGroupCount == 0)
        {
            if (_repeatedExactFindAllString is not null &&
                _utf8Regex is not null)
            {
                var validatedInput = Utf8ValidatedInput.Create(input);
                var start = validatedInput.GetBytePosition(startOffsetInBytes, nameof(startOffsetInBytes));
                var values = new string[_utf8Regex.ByteOffsetExecution.CountPrepared(validatedInput, start)];
                Array.Fill(values, _repeatedExactFindAllString);
                return new Utf8PythonFindAllResult
                {
                    Shape = Utf8PythonFindAllShape.FullMatch,
                    ScalarValues = values,
                    TupleValues = [],
                };
            }

            if (TryCollectUtf8MatchRanges(input, startOffsetInBytes, out var ranges))
            {
                try
                {
                    var values = new string[ranges.Count];
                    if (_repeatedExactFindAllString is not null)
                    {
                        Array.Fill(values, _repeatedExactFindAllString);
                    }
                    else
                    {
                        for (var i = 0; i < values.Length; i++)
                        {
                            var range = ranges[i];
                            values[i] = Encoding.UTF8.GetString(input.Slice(range.IndexInBytes, range.LengthInBytes));
                        }
                    }

                    return new Utf8PythonFindAllResult
                    {
                        Shape = Utf8PythonFindAllShape.FullMatch,
                        ScalarValues = values,
                        TupleValues = [],
                    };
                }
                finally
                {
                    ranges.Dispose();
                }
            }

            var matches = FindAll(input, startOffsetInBytes);
            return new Utf8PythonFindAllResult
            {
                Shape = Utf8PythonFindAllShape.FullMatch,
                ScalarValues = matches.Select(x => x.ValueText).ToArray(),
                TupleValues = [],
            };
        }
        FindAllScalarStrings(input, startOffsetInBytes, cancellationToken, out var remaining);
        return remaining;
    }

    /// <summary>Returns shaped <c>findall</c> strings after a zero-based UTF-8 byte offset, observing cancellation before matching begins.</summary>
    /// <param name="input">The well-formed UTF-8 input to match against.</param>
    /// <param name="startOffsetInBytes">The zero-based UTF-8 byte offset where matching starts.</param>
    /// <param name="cancellationToken">The token to observe before matching begins.</param>
    /// <exception cref="OperationCanceledException">The token was already canceled.</exception>
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
        if (cancellationToken.CanBeCanceled || _canMatchEmpty || _translation.CaptureGroupCount > 0 || !_canUseUtf8IterationFastPath)
        {
            FindAllScalarUtf8(input, startOffsetInBytes, cancellationToken, out var scalarResult);
            return scalarResult;
        }
        ValidateStartOffset(input, startOffsetInBytes);
        if (_translation.CaptureGroupCount == 0)
        {
            if (TryCollectUtf8MatchRanges(input, startOffsetInBytes, out var ranges))
            {
                try
                {
                    var directValues = new byte[ranges.Count][];
                    for (var i = 0; i < directValues.Length; i++)
                    {
                        var range = ranges[i];
                        directValues[i] = input.Slice(range.IndexInBytes, range.LengthInBytes).ToArray();
                    }

                    return new Utf8PythonFindAllUtf8Result
                    {
                        Shape = Utf8PythonFindAllShape.FullMatch,
                        ScalarValues = directValues,
                        TupleValues = [],
                    };
                }
                finally
                {
                    ranges.Dispose();
                }
            }

            var matches = FindAll(input, startOffsetInBytes);
            var values = new byte[matches.Length][];
            for (var i = 0; i < matches.Length; i++)
            {
                values[i] = input[matches[i].StartOffsetInBytes..matches[i].EndOffsetInBytes].ToArray();
            }

            return new Utf8PythonFindAllUtf8Result
            {
                Shape = Utf8PythonFindAllShape.FullMatch,
                ScalarValues = values,
                TupleValues = [],
            };
        }
        FindAllScalarUtf8(input, startOffsetInBytes, cancellationToken, out var remaining);
        return remaining;
    }

    /// <summary>Returns shaped UTF-8 <c>findall</c> values after a byte offset, observing cancellation before matching begins.</summary>
    /// <param name="input">The well-formed UTF-8 input to match against.</param>
    /// <param name="startOffsetInBytes">The zero-based UTF-8 byte offset where matching starts.</param>
    /// <param name="cancellationToken">The token to observe before matching begins.</param>
    /// <exception cref="OperationCanceledException">The token was already canceled.</exception>
    public Utf8PythonFindAllUtf8Result FindAllToUtf8(ReadOnlySpan<byte> input, int startOffsetInBytes, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return FindAllToUtf8Core(input, startOffsetInBytes, cancellationToken);
    }

}
