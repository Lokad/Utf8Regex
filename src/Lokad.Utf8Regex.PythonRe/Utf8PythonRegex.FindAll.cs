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
    public Utf8PythonMatchData[] FindAll(ReadOnlySpan<byte> input, int startOffsetInBytes)
    {
        ValidateStartOffset(input, startOffsetInBytes);
        if (_findAllBackend == PythonReDirectBackendKind.Utf8Regex &&
            TryFindAllViaUtf8Regex(input, startOffsetInBytes, out var utf8Matches))
        {
            return utf8Matches;
        }

        var ownedFindAllSubject = Decode(input);
        var ownedFindAllStart = GetUtf16OffsetOfBytePrefix(input, startOffsetInBytes);
        if (TryFindIterDetailedOwned(input, ownedFindAllSubject, ownedFindAllStart, out var ownedDetailed))
        {
            var ownedFindAll = new Utf8PythonMatchData[ownedDetailed.Length];
            for (var ownedIndex = 0; ownedIndex < ownedFindAll.Length; ownedIndex++)
            {
                var ownedValue = ownedDetailed[ownedIndex].Value;
                ownedFindAll[ownedIndex] = new Utf8PythonMatchData
                {
                    Success = true,
                    StartOffsetInBytes = ownedValue.StartOffsetInBytes,
                    EndOffsetInBytes = ownedValue.EndOffsetInBytes,
                    StartOffsetInUtf16 = ownedValue.StartOffsetInUtf16,
                    EndOffsetInUtf16 = ownedValue.EndOffsetInUtf16,
                    ValueText = ownedValue.ValueText,
                };
            }

            return ownedFindAll;
        }

        var subject = Decode(input);
        var indexMap = default(PythonReUtf8IndexMap);
        var hasIndexMap = false;
        var startOffsetInUtf16 = GetUtf16OffsetOfBytePrefix(input, startOffsetInBytes);
        var matches = new List<Utf8PythonMatchData>();
        var cursor = new PythonReManagedMatchCursor(subject, _managedRegex, _lazyManagedNonEmptyAtSamePositionRegex, startOffsetInUtf16);
        while (cursor.TryMoveNext(out var match))
        {
            if (!hasIndexMap)
            {
                indexMap = PythonReUtf8IndexMap.Create(input, subject);
                hasIndexMap = true;
            }

            var snapshot = CreateMatchSnapshot(match, indexMap);
            var context = snapshot.ToContext(input, _nameEntries);
            var value = context.Value;
            matches.Add(new Utf8PythonMatchData
            {
                Success = true,
                StartOffsetInBytes = value.StartOffsetInBytes,
                EndOffsetInBytes = value.EndOffsetInBytes,
                StartOffsetInUtf16 = value.StartOffsetInUtf16,
                EndOffsetInUtf16 = value.EndOffsetInUtf16,
                ValueText = value.GetValueString(),
            });
        }

        return matches.ToArray();
    }

    /// <summary>Returns Python <c>findall</c> string values using capture-dependent shaping.</summary>
    public Utf8PythonFindAllResult FindAllToStrings(ReadOnlySpan<byte> input) => FindAllToStrings(input, 0);

    /// <summary>Returns shaped <c>findall</c> strings after a zero-based UTF-8 byte offset.</summary>
    public Utf8PythonFindAllResult FindAllToStrings(ReadOnlySpan<byte> input, int startOffsetInBytes)
    {
        ValidateStartOffset(input, startOffsetInBytes);
        if (_asciiGreedyStarFindAllByte is { } repeatedByte)
        {
            return PythonReAsciiGreedyStarFindAll.Execute(input, startOffsetInBytes, repeatedByte);
        }

        if (_translation.CaptureGroupCount == 0)
        {
            if (_repeatedExactFindAllString is not null &&
                _findAllBackend == PythonReDirectBackendKind.Utf8Regex &&
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

        var subject = Decode(input);
        var startOffsetInUtf16 = GetUtf16OffsetOfBytePrefix(input, startOffsetInBytes);
        if (_translation.CaptureGroupCount == 1)
        {
            if (TryFindIterDetailedOwned(input, subject, startOffsetInUtf16, out var ownedSingleDetailed))
            {
                var ownedSingleValues = new string[ownedSingleDetailed.Length];
                for (var ownedSingleIndex = 0; ownedSingleIndex < ownedSingleValues.Length; ownedSingleIndex++)
                {
                    ownedSingleValues[ownedSingleIndex] = ownedSingleDetailed[ownedSingleIndex].Groups[1].ValueText;
                }

                return new Utf8PythonFindAllResult
                {
                    Shape = Utf8PythonFindAllShape.SingleGroup,
                    ScalarValues = ownedSingleValues,
                    TupleValues = [],
                };
            }

            if (_optionalExactCaptureFindAllValue is not null)
            {
                List<string> directValues = [];
                foreach (var match in _managedRegex.Value.EnumerateMatches(subject, startOffsetInUtf16))
                {
                    directValues.Add(match.Length switch
                    {
                        var length when length == _optionalExactCapturePresentMatchLength =>
                            _optionalExactCaptureFindAllValue,
                        var length when length == _optionalExactCaptureAbsentMatchLength => string.Empty,
                        _ => throw new InvalidOperationException(
                            "The optional exact-capture route encountered an unexpected full-match length."),
                    });
                }

                return new Utf8PythonFindAllResult
                {
                    Shape = Utf8PythonFindAllShape.SingleGroup,
                    ScalarValues = directValues.ToArray(),
                    TupleValues = [],
                };
            }

            if (_singleTrailingCapturePrefixLength >= 0)
            {
                List<string> directValues = [];
                foreach (var match in _managedRegex.Value.EnumerateMatches(subject, startOffsetInUtf16))
                {
                    var captureStart = match.Index + _singleTrailingCapturePrefixLength;
                    directValues.Add(subject.Substring(
                        captureStart,
                        match.Length - _singleTrailingCapturePrefixLength));
                }

                return new Utf8PythonFindAllResult
                {
                    Shape = Utf8PythonFindAllShape.SingleGroup,
                    ScalarValues = directValues.ToArray(),
                    TupleValues = [],
                };
            }

            List<string> collected = [];
            var cursor = new PythonReManagedMatchCursor(subject, _managedRegex, _lazyManagedNonEmptyAtSamePositionRegex, startOffsetInUtf16);
            while (cursor.TryMoveNext(out var match))
            {
                collected.Add(GetFindAllGroupValue(match, 1));
            }

            return new Utf8PythonFindAllResult
            {
                Shape = Utf8PythonFindAllShape.SingleGroup,
                ScalarValues = collected.ToArray(),
                TupleValues = [],
            };
        }

        if (_separatedCaptureTupleSeparator is { } separator)
        {
            List<string[]> directTuples = [];
            foreach (var match in _managedRegex.Value.EnumerateMatches(subject, startOffsetInUtf16))
            {
                var matched = subject.AsSpan(match.Index, match.Length);
                var separatorIndex = matched.IndexOf(separator);
                directTuples.Add([
                    new string(matched[..separatorIndex]),
                    new string(matched[(separatorIndex + 1)..]),
                ]);
            }

            return new Utf8PythonFindAllResult
            {
                Shape = Utf8PythonFindAllShape.GroupTuple,
                ScalarValues = [],
                TupleValues = directTuples.ToArray(),
            };
        }

        if (TryFindIterDetailedOwned(input, subject, startOffsetInUtf16, out var ownedTupleDetailed))
        {
            var ownedTupleValues = new string[ownedTupleDetailed.Length][];
            for (var ownedTupleIndex = 0; ownedTupleIndex < ownedTupleValues.Length; ownedTupleIndex++)
            {
                var ownedTuple = new string[_translation.CaptureGroupCount];
                for (var ownedGroupIndex = 0; ownedGroupIndex < ownedTuple.Length; ownedGroupIndex++)
                {
                    ownedTuple[ownedGroupIndex] = ownedTupleDetailed[ownedTupleIndex].Groups[ownedGroupIndex + 1].ValueText;
                }

                ownedTupleValues[ownedTupleIndex] = ownedTuple;
            }

            return new Utf8PythonFindAllResult
            {
                Shape = Utf8PythonFindAllShape.GroupTuple,
                ScalarValues = [],
                TupleValues = ownedTupleValues,
            };
        }

        List<string[]> tuples = [];
        {
            var cursor = new PythonReManagedMatchCursor(subject, _managedRegex, _lazyManagedNonEmptyAtSamePositionRegex, startOffsetInUtf16);
            while (cursor.TryMoveNext(out var match))
            {
                var tuple = new string[_translation.CaptureGroupCount];
                for (var i = 0; i < tuple.Length; i++)
                {
                    tuple[i] = GetFindAllGroupValue(match, i + 1);
                }

                tuples.Add(tuple);
            }
        }

        return new Utf8PythonFindAllResult
        {
            Shape = Utf8PythonFindAllShape.GroupTuple,
            ScalarValues = [],
            TupleValues = tuples.ToArray(),
        };
    }

    /// <summary>Returns Python <c>findall</c> values as owned UTF-8 byte arrays.</summary>
    public Utf8PythonFindAllUtf8Result FindAllToUtf8(ReadOnlySpan<byte> input) => FindAllToUtf8(input, 0);

    /// <summary>Returns shaped UTF-8 <c>findall</c> values after a byte offset.</summary>
    public Utf8PythonFindAllUtf8Result FindAllToUtf8(ReadOnlySpan<byte> input, int startOffsetInBytes)
    {
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

        var subject = Decode(input);
        var indexMap = default(PythonReUtf8IndexMap);
        var hasIndexMap = false;
        var startOffsetInUtf16 = GetUtf16OffsetOfBytePrefix(input, startOffsetInBytes);
        if (_translation.CaptureGroupCount == 1)
        {
            if (TryFindIterDetailedOwned(input, subject, startOffsetInUtf16, out var ownedSingleByteDetailed))
            {
                var ownedSingleByteValues = new byte[ownedSingleByteDetailed.Length][];
                for (var ownedSingleByteIndex = 0; ownedSingleByteIndex < ownedSingleByteValues.Length; ownedSingleByteIndex++)
                {
                    var ownedSingleByteGroup = ownedSingleByteDetailed[ownedSingleByteIndex].Groups[1];
                    if (ownedSingleByteGroup.Success)
                    {
                        ownedSingleByteValues[ownedSingleByteIndex] = ownedSingleByteGroup.HasContiguousByteRange
                            ? input[ownedSingleByteGroup.StartOffsetInBytes..ownedSingleByteGroup.EndOffsetInBytes].ToArray()
                            : Encoding.UTF8.GetBytes(ownedSingleByteGroup.ValueText);
                    }
                    else
                    {
                        ownedSingleByteValues[ownedSingleByteIndex] = [];
                    }
                }

                return new Utf8PythonFindAllUtf8Result
                {
                    Shape = Utf8PythonFindAllShape.SingleGroup,
                    ScalarValues = ownedSingleByteValues,
                    TupleValues = [],
                };
            }

            List<byte[]> collected = [];
            var cursor = new PythonReManagedMatchCursor(subject, _managedRegex, _lazyManagedNonEmptyAtSamePositionRegex, startOffsetInUtf16);
            while (cursor.TryMoveNext(out var match))
            {
                if (!hasIndexMap)
                {
                    indexMap = PythonReUtf8IndexMap.Create(input, subject);
                    hasIndexMap = true;
                }

                collected.Add(GetFindAllGroupBytes(input, CreateMatchSnapshot(match, indexMap).Groups, 1));
            }

            return new Utf8PythonFindAllUtf8Result
            {
                Shape = Utf8PythonFindAllShape.SingleGroup,
                ScalarValues = collected.ToArray(),
                TupleValues = [],
            };
        }

        if (TryFindIterDetailedOwned(input, subject, startOffsetInUtf16, out var ownedByteTupleDetailed))
        {
            var ownedByteTupleValues = new byte[ownedByteTupleDetailed.Length][][];
            for (var ownedByteTupleIndex = 0; ownedByteTupleIndex < ownedByteTupleValues.Length; ownedByteTupleIndex++)
            {
                var ownedByteTuple = new byte[_translation.CaptureGroupCount][];
                for (var ownedByteGroupIndex = 0; ownedByteGroupIndex < ownedByteTuple.Length; ownedByteGroupIndex++)
                {
                    var ownedByteGroup = ownedByteTupleDetailed[ownedByteTupleIndex].Groups[ownedByteGroupIndex + 1];
                    if (ownedByteGroup.Success)
                    {
                        ownedByteTuple[ownedByteGroupIndex] = ownedByteGroup.HasContiguousByteRange
                            ? input[ownedByteGroup.StartOffsetInBytes..ownedByteGroup.EndOffsetInBytes].ToArray()
                            : Encoding.UTF8.GetBytes(ownedByteGroup.ValueText);
                    }
                    else
                    {
                        ownedByteTuple[ownedByteGroupIndex] = [];
                    }
                }

                ownedByteTupleValues[ownedByteTupleIndex] = ownedByteTuple;
            }

            return new Utf8PythonFindAllUtf8Result
            {
                Shape = Utf8PythonFindAllShape.GroupTuple,
                ScalarValues = [],
                TupleValues = ownedByteTupleValues,
            };
        }

        List<byte[][]> tuples = [];
        {
            var cursor = new PythonReManagedMatchCursor(subject, _managedRegex, _lazyManagedNonEmptyAtSamePositionRegex, startOffsetInUtf16);
            while (cursor.TryMoveNext(out var match))
            {
                if (!hasIndexMap)
                {
                    indexMap = PythonReUtf8IndexMap.Create(input, subject);
                    hasIndexMap = true;
                }

                var snapshot = CreateMatchSnapshot(match, indexMap);
                var tuple = new byte[_translation.CaptureGroupCount][];
                for (var i = 0; i < tuple.Length; i++)
                {
                    tuple[i] = GetFindAllGroupBytes(input, snapshot.Groups, i + 1);
                }

                tuples.Add(tuple);
            }
        }

        return new Utf8PythonFindAllUtf8Result
        {
            Shape = Utf8PythonFindAllShape.GroupTuple,
            ScalarValues = [],
            TupleValues = tuples.ToArray(),
        };
    }
}