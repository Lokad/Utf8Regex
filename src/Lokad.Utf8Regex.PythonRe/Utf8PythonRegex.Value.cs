using System.Buffers;
using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Caching;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Input;
using Lokad.Utf8Regex.Internal.Planning;

namespace Lokad.Utf8Regex.PythonRe;

// PLAN.md D09: value entry points, split from the Python facade along the value-operation ownership boundary.
/// <summary>Value entry points for <see cref="Utf8PythonRegex"/>.</summary>
public sealed partial class Utf8PythonRegex
{
    /// <summary>Tests whether the UTF-8 input contains a match.</summary>
    public bool IsMatch(ReadOnlySpan<byte> input) => IsMatch(input, 0);

    /// <summary>Tests for a match at or after a zero-based UTF-8 byte offset.</summary>
    public bool IsMatch(ReadOnlySpan<byte> input, int startOffsetInBytes)
    {
        return Search(input, startOffsetInBytes).Success;
    }

    /// <summary>Tests for a match at or after a zero-based UTF-8 byte offset, observing cancellation before matching begins.</summary>
    /// <param name="input">The well-formed UTF-8 input to match against.</param>
    /// <param name="startOffsetInBytes">The zero-based UTF-8 byte offset where matching starts.</param>
    /// <param name="cancellationToken">The token to observe before matching begins.</param>
    /// <exception cref="OperationCanceledException">The token was already canceled.</exception>
    public bool IsMatch(ReadOnlySpan<byte> input, int startOffsetInBytes, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Search(input, startOffsetInBytes, cancellationToken).Success;
    }

    /// <summary>Finds the first match in the UTF-8 input.</summary>
    public Utf8PythonValueMatch Search(ReadOnlySpan<byte> input) => Search(input, 0);

    /// <summary>Finds the first match at or after a zero-based UTF-8 byte offset.</summary>
    public Utf8PythonValueMatch Search(ReadOnlySpan<byte> input, int startOffsetInBytes)
    {
        return SearchCore(input, startOffsetInBytes, CancellationToken.None);
    }

    private Utf8PythonValueMatch SearchCore(ReadOnlySpan<byte> input, int startOffsetInBytes, CancellationToken cancellationToken)
    {
        // PLAN.md C08: native value paths skip positive-lookbehind verification (core literal plans drop the assertion while detailed paths verify); keep lookbehind patterns on verified routes.
        if (_utf8Regex is not null && !_hasLookbehind)
        {
            ValidateStartOffset(input, startOffsetInBytes);
            var match = startOffsetInBytes == 0 && _canUseZeroOffsetUtf8SearchValueFastPath
                ? _utf8Regex.Match(input, cancellationToken)
                : _utf8Regex.MatchFromUtf16Offset(
                    input,
                    GetUtf16OffsetOfBytePrefix(input, startOffsetInBytes),
                    cancellationToken);
            return Utf8PythonValueMatchFromUtf8Regex(input, match);
        }

        return SearchViaManagedRegex(input, startOffsetInBytes);
    }

    /// <summary>Finds the first match at or after a zero-based UTF-8 byte offset, observing cancellation before matching begins.</summary>
    /// <param name="input">The well-formed UTF-8 input to match against.</param>
    /// <param name="startOffsetInBytes">The zero-based UTF-8 byte offset where matching starts.</param>
    /// <param name="cancellationToken">The token to observe before matching begins.</param>
    /// <exception cref="OperationCanceledException">The token was already canceled.</exception>
    public Utf8PythonValueMatch Search(ReadOnlySpan<byte> input, int startOffsetInBytes, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return SearchCore(input, startOffsetInBytes, cancellationToken);
    }

    /// <summary>Matches at the beginning of the UTF-8 input.</summary>
    public Utf8PythonValueMatch Match(ReadOnlySpan<byte> input) => Match(input, 0);

    /// <summary>Matches exactly at a zero-based UTF-8 byte offset.</summary>
    public Utf8PythonValueMatch Match(ReadOnlySpan<byte> input, int startOffsetInBytes)
    {
        return MatchCore(input, startOffsetInBytes, CancellationToken.None);
    }

    private Utf8PythonValueMatch MatchCore(ReadOnlySpan<byte> input, int startOffsetInBytes, CancellationToken cancellationToken)
    {
        if (startOffsetInBytes == 0 && _asciiLiteralPrefixDigitMatchPrefix is not null)
        {
            var end = PythonReAsciiPrefixDigitMatcher.MatchValidated(
                input,
                _asciiLiteralPrefixDigitMatchPrefix);
            return end == 0
                ? default
                : Utf8PythonValueMatch.Create(input, new PythonReGroupData
                {
                    Number = 0,
                    Success = true,
                    StartOffsetInBytes = 0,
                    EndOffsetInBytes = end,
                    StartOffsetInUtf16 = 0,
                    EndOffsetInUtf16 = end,
                });
        }

        if (_utf8Regex is not null && !_hasLookbehind)
        {
            ValidateStartOffset(input, startOffsetInBytes);
            var startOffsetInUtf16 = startOffsetInBytes == 0
                ? 0
                : GetUtf16OffsetOfBytePrefix(input, startOffsetInBytes);
            var match = startOffsetInBytes == 0 && _canUseZeroOffsetUtf8SearchValueFastPath
                ? _utf8Regex.Match(input, cancellationToken)
                : _utf8Regex.MatchFromUtf16Offset(input, startOffsetInUtf16, cancellationToken);
            if (!match.Success || match.IndexInUtf16 != startOffsetInUtf16)
            {
                return default;
            }

            return Utf8PythonValueMatchFromUtf8Regex(input, match);
        }

        var context = MatchDetailed(input, startOffsetInBytes);
        return context.Value;
    }

    /// <summary>Matches exactly at a zero-based UTF-8 byte offset, observing cancellation before matching begins.</summary>
    /// <param name="input">The well-formed UTF-8 input to match against.</param>
    /// <param name="startOffsetInBytes">The zero-based UTF-8 byte offset where matching starts.</param>
    /// <param name="cancellationToken">The token to observe before matching begins.</param>
    /// <exception cref="OperationCanceledException">The token was already canceled.</exception>
    public Utf8PythonValueMatch Match(ReadOnlySpan<byte> input, int startOffsetInBytes, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return MatchCore(input, startOffsetInBytes, cancellationToken);
    }

    /// <summary>Matches the complete UTF-8 input.</summary>
    public Utf8PythonValueMatch FullMatch(ReadOnlySpan<byte> input) => FullMatch(input, 0);

    /// <summary>Matches the complete UTF-8 suffix beginning at a zero-based byte offset.</summary>
    public Utf8PythonValueMatch FullMatch(ReadOnlySpan<byte> input, int startOffsetInBytes)
    {
        return FullMatchCore(input, startOffsetInBytes, CancellationToken.None);
    }

    private Utf8PythonValueMatch FullMatchCore(ReadOnlySpan<byte> input, int startOffsetInBytes, CancellationToken cancellationToken)
    {
        ValidateStartOffset(input, startOffsetInBytes);
        if (_asciiDotAllFullMatchPrefix is not null && _asciiDotAllFullMatchSuffix is not null)
        {
            var validation = Utf8InputAnalyzer.ValidateOnly(input);
            var tail = input[startOffsetInBytes..];
            if (tail.Length < _asciiDotAllFullMatchPrefix.Length ||
                tail.Length - _asciiDotAllFullMatchPrefix.Length < _asciiDotAllFullMatchSuffix.Length ||
                !tail.StartsWith(_asciiDotAllFullMatchPrefix) ||
                !tail.EndsWith(_asciiDotAllFullMatchSuffix))
            {
                return default;
            }

            var startOffsetInUtf16 = startOffsetInBytes == 0
                ? 0
                : GetUtf16OffsetOfBytePrefix(input, startOffsetInBytes);
            return Utf8PythonValueMatch.Create(input, new PythonReGroupData
            {
                Number = 0,
                Success = true,
                StartOffsetInBytes = startOffsetInBytes,
                EndOffsetInBytes = input.Length,
                StartOffsetInUtf16 = startOffsetInUtf16,
                EndOffsetInUtf16 = validation.Utf16Length,
            });
        }

        var utf8FullRegex = GetUtf8FullRegex();
        if (utf8FullRegex is not null && !_hasLookbehind)
        {
            // PLAN.md C08: match the whole subject from the start offset so `^` keeps the real start and lookbehind keeps its prefix; the `\G` wrapper pins the span while `\z` pins the end.
            var match = utf8FullRegex.MatchFromUtf16Offset(
                input,
                GetUtf16OffsetOfBytePrefix(input, startOffsetInBytes),
                cancellationToken);
            if (!match.Success)
            {
                return default;
            }

            return Utf8PythonValueMatchFromUtf8Regex(input, match);
        }

        return FullMatchViaManagedRegex(input, startOffsetInBytes);
    }

    /// <summary>Matches the complete UTF-8 suffix beginning at a zero-based byte offset, observing cancellation before matching begins.</summary>
    /// <param name="input">The well-formed UTF-8 input to match against.</param>
    /// <param name="startOffsetInBytes">The zero-based UTF-8 byte offset where matching starts.</param>
    /// <param name="cancellationToken">The token to observe before matching begins.</param>
    /// <exception cref="OperationCanceledException">The token was already canceled.</exception>
    public Utf8PythonValueMatch FullMatch(ReadOnlySpan<byte> input, int startOffsetInBytes, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return FullMatchCore(input, startOffsetInBytes, cancellationToken);
    }

    /// <summary>Counts non-overlapping Python-style matches in the UTF-8 input.</summary>
    public int Count(ReadOnlySpan<byte> input) => Count(input, 0);

    /// <summary>Counts non-overlapping matches at or after a zero-based UTF-8 byte offset.</summary>
    public int Count(ReadOnlySpan<byte> input, int startOffsetInBytes)
    {
        return CountCore(input, startOffsetInBytes, CancellationToken.None);
    }

    private int CountCore(ReadOnlySpan<byte> input, int startOffsetInBytes, CancellationToken cancellationToken)
    {
        ValidateStartOffset(input, startOffsetInBytes);
        if (_canCountAsciiWordBoundariesDirectly && MatchTimeout == Timeout.InfiniteTimeSpan)
        {
            var decoded = Decode(input);
            return CountAsciiWordBoundaries(decoded, GetUtf16OffsetOfBytePrefix(input, startOffsetInBytes));
        }

        if (_countBackend == PythonReDirectBackendKind.Utf8Regex && _utf8Regex is not null && !_hasLookbehind && startOffsetInBytes == 0 &&
            !_utf8Regex.Inspection.DebugUsesOwnedSliceProgram)
        {
            return _utf8Regex.Count(input, cancellationToken);
        }

        var subject = Decode(input);
        var startOffsetInUtf16 = GetUtf16OffsetOfBytePrefix(input, startOffsetInBytes);
        if (!_hasLookbehind && _utf8Regex is not null &&
            _utf8Regex.Inspection.DebugUsesOwnedSliceProgram &&
            _utf8Regex.Inspection.OwnedSliceProgram is { } ownedProgram)
        {
            try
            {
                return CountOwnedSlicePythonStyle(
                    input, startOffsetInUtf16, ownedProgram, cancellationToken);
            }
            catch (Utf8ExecutionDeadlineExpiredException)
            {
                throw new RegexMatchTimeoutException(subject, _translation.Pattern, MatchTimeout);
            }
        }

        if (!_canMatchEmpty)
        {
            if (!_hasLookbehind && _utf8Regex is not null && _utf8Regex.TryCreateOwnedSplitCursor(input, out var ownedCountCursor, out var ownedCountMap) && ownedCountMap is not null)
            {
                var ownedCount = 0;
                var ownedCountFirst = true;
                var ownedCountDeclined = false;
                try
                {
                    while (ownedCountCursor.MoveNext())
                    {
                        var ownedCountMatch = ownedCountCursor.CurrentValueMatch;
                        if (!ownedCountMatch.Success || ownedCountMatch.IndexInUtf16 < startOffsetInUtf16)
                        {
                            continue;
                        }

                        var ownedCountBranch = ownedCountCursor.LastLiteralFamilyBranchId;
                        if (ownedCountFirst)
                        {
                            ownedCountFirst = false;
                            if (ownedCountBranch < 0 || (uint)ownedCountBranch >= (uint)ownedCountMap.BranchGroups.Length)
                            {
                                ownedCountDeclined = true;
                                break;
                            }
                        }

                        ownedCount++;
                    }
                }
                catch (Utf8ExecutionDeadlineExpiredException)
                {
                    throw new RegexMatchTimeoutException(subject, _translation.Pattern, MatchTimeout);
                }

                if (!ownedCountDeclined)
                {
                    return ownedCount;
                }
            }

            return _managedRegex.Value.Count(subject, startOffsetInUtf16);
        }

        return CountManagedMatchesPythonStyle(subject, startOffsetInUtf16);
    }

    private int CountOwnedSlicePythonStyle(
        ReadOnlySpan<byte> input,
        int startOffsetInUtf16,
        Lokad.Utf8Regex.Internal.Execution.Utf8SliceProgram program,
        CancellationToken cancellationToken)
    {
        // Mirrors PythonReManagedMatchCursor: after an empty match the same
        // position is retried requiring a non-empty match before advancing
        // past the scalar, reproducing CPython finditer adjacency.
        var map = Lokad.Utf8Regex.Internal.Input.Utf8BoundaryMap.Create(input);
        var invocation = new Lokad.Utf8Regex.Internal.Execution.Utf8SliceInvocation(
            Lokad.Utf8Regex.Internal.Execution.Utf8ExecutionLimits.Unlimited,
            MatchTimeout,
            cancellationToken);
        var slots = Lokad.Utf8Regex.Internal.Execution.Utf8SliceRunner.RentSlots(program.GroupCount, ref invocation);
        try
        {
            var count = 0;
            var unit = startOffsetInUtf16;
            var retryPending = false;
            var retryUnit = 0;
            while (true)
            {
                Lokad.Utf8Regex.Internal.Execution.Utf8SliceUnitOutcome outcome;
                if (retryPending)
                {
                    retryPending = false;
                    if (retryUnit >= map.Utf16Length)
                    {
                        break;
                    }

                    outcome = Lokad.Utf8Regex.Internal.Execution.Utf8SliceRunner.TryMatchFromUnitNonEmpty(
                        program, input, map, retryUnit, slots.Starts, slots.Ends, ref invocation);
                    if (outcome.Breach != Lokad.Utf8Regex.Internal.Execution.Utf8ExecutionLimitKind.None)
                    {
                        throw new InvalidOperationException("Owned slice execution breached an unlimited invocation budget.");
                    }

                    if (outcome.Found)
                    {
                        count++;
                        unit = outcome.EndUnit;
                        continue;
                    }

                    if (!map.TryAdvanceScalar(retryUnit, out var advanced))
                    {
                        break;
                    }

                    unit = advanced.Utf16Offset;
                    continue;
                }

                if (unit > map.Utf16Length)
                {
                    break;
                }

                outcome = Lokad.Utf8Regex.Internal.Execution.Utf8SliceRunner.TryMatchFromUnit(
                    program, input, map, unit, slots.Starts, slots.Ends, ref invocation);
                if (outcome.Breach != Lokad.Utf8Regex.Internal.Execution.Utf8ExecutionLimitKind.None)
                {
                    throw new InvalidOperationException("Owned slice execution breached an unlimited invocation budget.");
                }

                if (!outcome.Found)
                {
                    unit++;
                    continue;
                }

                count++;
                if (outcome.EndUnit == outcome.StartUnit)
                {
                    retryPending = true;
                    retryUnit = outcome.StartUnit;
                    continue;
                }

                unit = outcome.EndUnit;
            }

            return count;
        }
        finally
        {
            Lokad.Utf8Regex.Internal.Execution.Utf8SliceRunner.ReleaseSlots(slots, ref invocation);
        }
    }

    /// <summary>Counts non-overlapping matches at or after a zero-based UTF-8 byte offset, observing cancellation before matching begins.</summary>
    /// <param name="input">The well-formed UTF-8 input to match against.</param>
    /// <param name="startOffsetInBytes">The zero-based UTF-8 byte offset where matching starts.</param>
    /// <param name="cancellationToken">The token to observe before matching begins.</param>
    /// <exception cref="OperationCanceledException">The token was already canceled.</exception>
    public int Count(ReadOnlySpan<byte> input, int startOffsetInBytes, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return CountCore(input, startOffsetInBytes, cancellationToken);
    }

}
