using System.Buffers;
using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Caching;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Input;
using Lokad.Utf8Regex.Internal.Planning;

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
        if (TrySplitScalar(input, maxSplit, startOffsetInBytes, cancellationToken, true, out _, out var scalarValues)) return scalarValues;
        ValidateStartOffset(input, startOffsetInBytes);
        var subject = Decode(input);
        if (TrySplitToOwnedItems(input, subject, maxSplit, GetUtf16OffsetOfBytePrefix(input, startOffsetInBytes), out var ownedItems))
        {
            var values = new string?[ownedItems.Length];
            for (var i = 0; i < values.Length; i++)
            {
                values[i] = ownedItems[i].ValueText;
            }

            return values;
        }

        // Regex.Split advances empty matches by UTF-16 code unit, whereas Python advances by scalar.
        if (_groupNumberingIsIdentity &&
            _canUseManagedSplitFastPath &&
            (!_canMatchEmpty || input.IndexOfAnyInRange((byte)0xF0, (byte)0xF4) < 0) &&
            startOffsetInBytes == 0 &&
            maxSplit >= 0)
        {
            return maxSplit is 0 or int.MaxValue
                ? _managedRegex.Value.Split(subject)
                : _managedRegex.Value.Split(subject, maxSplit + 1);
        }

        var startOffsetInUtf16 = GetUtf16OffsetOfBytePrefix(input, startOffsetInBytes);
        var parts = new List<string?>();
        var lastIndex = startOffsetInUtf16;
            var cursor = new PythonReManagedMatchCursor(subject, _managedRegex, _lazyManagedNonEmptyAtSamePositionRegex, startOffsetInUtf16);
            var splitCount = 0;
            while ((maxSplit == 0 || splitCount < maxSplit) && cursor.TryMoveNext(out var match))
            {
                AppendSplitMatch(parts, subject, match, ref lastIndex, 0);
                splitCount++;
            }

        parts.Add(subject[lastIndex..]);
        return parts.ToArray();
    }

    // R04: literal shapes (singular or family, grouped or not) split on the shared owned
    // cursor with branch-indexed group spans; captures follow Python slot order with null for
    // unmatched groups. The first hit validates the branch channel before any output, so an
    // unknown branch still declines cleanly to the managed routes.
    private bool TrySplitToOwnedItems(
        ReadOnlySpan<byte> input,
        string subject,
        int maxSplit,
        int startOffsetInUtf16,
        out Utf8PythonSplitItem[] items)
    {
        items = [];
        if (_utf8Regex is null || _canMatchEmpty)
        {
            return false;
        }

        if (!_utf8Regex.TryCreateOwnedSplitCursor(input, out var cursor, out var map) || map is null)
        {
            return false;
        }

        var parts = new List<Utf8PythonSplitItem>();
        var lastIndex = startOffsetInUtf16;
        var splitCount = 0;
        var first = true;
        try
        {
            while ((maxSplit == 0 || splitCount < maxSplit) && cursor.MoveNext())
            {
                var match = cursor.CurrentValueMatch;
                if (!match.Success || match.IndexInUtf16 < startOffsetInUtf16)
                {
                    continue;
                }

                var branch = cursor.LastLiteralFamilyBranchId;
                if (first)
                {
                    first = false;
                    if (branch < 0 || (uint)branch >= (uint)map.BranchGroups.Length)
                    {
                        return false;
                    }
                }

                parts.Add(new Utf8PythonSplitItem
                {
                    ValueText = subject[lastIndex..match.IndexInUtf16],
                    IsCapture = false,
                    CaptureGroupNumber = 0,
                });
                for (var pythonGroup = 1; pythonGroup < _managedGroupNumbersByPythonGroup.Length; pythonGroup++)
                {
                    var managedGroup = _managedGroupNumbersByPythonGroup[pythonGroup];
                    var matched = Array.IndexOf(map.WholeMatchGroups, managedGroup) >= 0 ||
                        Array.IndexOf(map.BranchGroups[branch], managedGroup) >= 0;
                    parts.Add(new Utf8PythonSplitItem
                    {
                        ValueText = matched ? subject[match.IndexInUtf16..(match.IndexInUtf16 + match.LengthInUtf16)] : null,
                        IsCapture = true,
                        CaptureGroupNumber = pythonGroup,
                    });
                }

                lastIndex = match.IndexInUtf16 + match.LengthInUtf16;
                splitCount++;
            }
        }
        catch (Utf8ExecutionDeadlineExpiredException)
        {
            throw new RegexMatchTimeoutException(subject, _translation.Pattern, MatchTimeout);
        }

        parts.Add(new Utf8PythonSplitItem
        {
            ValueText = subject[lastIndex..],
            IsCapture = false,
            CaptureGroupNumber = 0,
        });
        items = parts.ToArray();
        return true;
    }

    /// <summary>Splits after a zero-based UTF-8 byte offset and returns strings and captures, observing cancellation before matching begins.</summary>
    /// <param name="input">The well-formed UTF-8 input to match against.</param>
    /// <param name="maxSplit">The maximum number of splits to perform.</param>
    /// <param name="startOffsetInBytes">The zero-based UTF-8 byte offset where matching starts.</param>
    /// <param name="cancellationToken">The token to observe before matching begins.</param>
    /// <exception cref="OperationCanceledException">The token was already canceled.</exception>
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
        if (TrySplitScalar(input, maxSplit, startOffsetInBytes, cancellationToken, false, out var scalarItems, out _)) return scalarItems;
        ValidateStartOffset(input, startOffsetInBytes);
        var subject = Decode(input);
        if (TrySplitToOwnedItems(input, subject, maxSplit, GetUtf16OffsetOfBytePrefix(input, startOffsetInBytes), out var ownedItems))
        {
            return ownedItems;
        }

        var startOffsetInUtf16 = GetUtf16OffsetOfBytePrefix(input, startOffsetInBytes);
        var parts = new List<Utf8PythonSplitItem>();
        var lastIndex = startOffsetInUtf16;
            var cursor = new PythonReManagedMatchCursor(subject, _managedRegex, _lazyManagedNonEmptyAtSamePositionRegex, startOffsetInUtf16);
            var splitCount = 0;
            while ((maxSplit == 0 || splitCount < maxSplit) && cursor.TryMoveNext(out var match))
            {
                AppendSplitDetailedMatch(parts, subject, match, ref lastIndex, 0);
                splitCount++;
            }

        parts.Add(new Utf8PythonSplitItem
        {
            ValueText = subject[lastIndex..],
            IsCapture = false,
            CaptureGroupNumber = 0,
        });
        return parts.ToArray();
    }

    /// <summary>Performs a detailed split after a zero-based UTF-8 byte offset, observing cancellation before matching begins.</summary>
    /// <param name="input">The well-formed UTF-8 input to match against.</param>
    /// <param name="maxSplit">The maximum number of splits to perform.</param>
    /// <param name="startOffsetInBytes">The zero-based UTF-8 byte offset where matching starts.</param>
    /// <param name="cancellationToken">The token to observe before matching begins.</param>
    /// <exception cref="OperationCanceledException">The token was already canceled.</exception>
    public Utf8PythonSplitItem[] SplitDetailed(ReadOnlySpan<byte> input, int maxSplit, int startOffsetInBytes, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return SplitDetailedCore(input, maxSplit, startOffsetInBytes, cancellationToken);
    }

}
