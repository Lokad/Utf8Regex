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
        if (_canUseZeroOffsetUtf8SearchValueFastPath && startOffsetInBytes == 0 &&
            !cancellationToken.CanBeCanceled && MatchTimeout == Timeout.InfiniteTimeSpan && _utf8Regex is not null)
        {
            var match = _utf8Regex.Match(input, cancellationToken);
            return Utf8PythonValueMatchFromUtf8Regex(input, match);
        }
        MatchScalar(input, startOffsetInBytes, 0, false, cancellationToken, out var groups, out _);
        return groups.Length == 0 ? default : Utf8PythonValueMatch.Create(input, groups[0]);
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
        if (startOffsetInBytes == 0 && _asciiLiteralPrefixDigitMatchPrefix is not null &&
            !cancellationToken.CanBeCanceled && MatchTimeout == Timeout.InfiniteTimeSpan)
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
        if (_canUseZeroOffsetUtf8SearchValueFastPath && startOffsetInBytes == 0 &&
            !cancellationToken.CanBeCanceled && MatchTimeout == Timeout.InfiniteTimeSpan && _utf8Regex is not null)
        {
            var match = _utf8Regex.Match(input, cancellationToken);
            if (!match.Success || match.IndexInUtf16 != 0) return default;
            return Utf8PythonValueMatchFromUtf8Regex(input, match);
        }
        MatchScalar(input, startOffsetInBytes, 1, false, cancellationToken, out var groups, out _);
        return groups.Length == 0 ? default : Utf8PythonValueMatch.Create(input, groups[0]);
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
        if (_asciiDotAllFullMatchPrefix is not null && _asciiDotAllFullMatchSuffix is not null &&
            !cancellationToken.CanBeCanceled)
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
        MatchScalar(input, startOffsetInBytes, 2, false, cancellationToken, out var scalarGroups, out _);
        return scalarGroups.Length == 0 ? default : Utf8PythonValueMatch.Create(input, scalarGroups[0]);
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
        // The native rendering intentionally has exact references. Only the
        // proven exact-literal control can bypass lexical profile comparison.
        if (_canUseZeroOffsetUtf8SearchValueFastPath && startOffsetInBytes == 0 &&
            !cancellationToken.CanBeCanceled && MatchTimeout == Timeout.InfiniteTimeSpan && _utf8Regex is not null)
            return _utf8Regex.Count(input, cancellationToken);
        return CountOwnedSlicePythonStyle(input, startOffsetInBytes, ScalarProgram, cancellationToken);
    }

    private int CountOwnedSlicePythonStyle(ReadOnlySpan<byte> input, int startOffsetInBytes,
        Utf8SliceProgram program, CancellationToken cancellationToken)
    {
        var cursor = new PythonReScalarMatchCursor(input, program, _coreGroupNumbersByPythonGroup,
            startOffsetInBytes, MatchTimeout, _translation.Pattern, cancellationToken);
        try
        {
            var count = 0;
            while (cursor.MoveNext()) count++;
            return count;
        }
        finally { cursor.Dispose(); }
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
