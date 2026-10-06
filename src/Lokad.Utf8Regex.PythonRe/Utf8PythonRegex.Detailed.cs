using System.Buffers;
using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Caching;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Input;
using Lokad.Utf8Regex.Internal.Planning;

namespace Lokad.Utf8Regex.PythonRe;

// PLAN.md D09: detailed match entry points, split from the Python facade along the detailed-match ownership boundary.
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
        if (TryMatchScalar(input, startOffsetInBytes, 0, false, cancellationToken, out var scalarGroups, out _))
            return scalarGroups.Length == 0 ? default : new Utf8PythonMatchContext(input, scalarGroups, _nameEntries);

        if (_utf8Regex is not null)
        {
            ValidateStartOffset(input, startOffsetInBytes);
            var utf8StartOffsetInUtf16 = GetUtf16OffsetOfBytePrefix(input, startOffsetInBytes);
            var utf8Match = _utf8Regex.MatchDetailedFromUtf16Offset(input, utf8StartOffsetInUtf16);
            return CreateMatchContextFromUtf8(input, utf8Match);
        }

        ValidateStartOffset(input, startOffsetInBytes);
        var subject = Decode(input);
        var indexMap = PythonReUtf8IndexMap.Create(input, subject);
        var startOffsetInUtf16 = GetUtf16OffsetOfBytePrefix(input, startOffsetInBytes);
        var match = _managedRegex.Value.Match(subject, startOffsetInUtf16);
        return CreateMatchContext(input, match, indexMap);
    }

    /// <summary>Finds the first capture context at or after a zero-based UTF-8 byte offset, observing cancellation before matching begins.</summary>
    /// <param name="input">The well-formed UTF-8 input to match against.</param>
    /// <param name="startOffsetInBytes">The zero-based UTF-8 byte offset where matching starts.</param>
    /// <param name="cancellationToken">The token to observe before matching begins.</param>
    /// <exception cref="OperationCanceledException">The token was already canceled.</exception>
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
        if (TryMatchScalar(input, startOffsetInBytes, 1, false, cancellationToken, out var scalarGroups, out _))
            return scalarGroups.Length == 0 ? default : new Utf8PythonMatchContext(input, scalarGroups, _nameEntries);

        if (_utf8Regex is not null)
        {
            ValidateStartOffset(input, startOffsetInBytes);
            var utf8StartOffsetInUtf16 = GetUtf16OffsetOfBytePrefix(input, startOffsetInBytes);
            var utf8Match = _utf8Regex.MatchDetailedFromUtf16Offset(input, utf8StartOffsetInUtf16);
            if (!utf8Match.Success || utf8Match.IndexInUtf16 != utf8StartOffsetInUtf16)
            {
                return default;
            }

            return CreateMatchContextFromUtf8(input, utf8Match);
        }

        ValidateStartOffset(input, startOffsetInBytes);
        var subject = Decode(input);
        var startOffsetInUtf16 = GetUtf16OffsetOfBytePrefix(input, startOffsetInBytes);
        var match = _managedRegex.Value.Match(subject, startOffsetInUtf16);
        if (!match.Success || match.Index != startOffsetInUtf16)
        {
            return default;
        }

        var indexMap = PythonReUtf8IndexMap.Create(input, subject);
        return CreateMatchContext(input, match, indexMap);
    }

    /// <summary>Matches at a zero-based UTF-8 byte offset and exposes its capture context, observing cancellation before matching begins.</summary>
    /// <param name="input">The well-formed UTF-8 input to match against.</param>
    /// <param name="startOffsetInBytes">The zero-based UTF-8 byte offset where matching starts.</param>
    /// <param name="cancellationToken">The token to observe before matching begins.</param>
    /// <exception cref="OperationCanceledException">The token was already canceled.</exception>
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
        if (TryMatchScalar(input, startOffsetInBytes, 2, false, cancellationToken, out var scalarGroups, out _))
            return scalarGroups.Length == 0 ? default : new Utf8PythonMatchContext(input, scalarGroups, _nameEntries);

        ValidateStartOffset(input, startOffsetInBytes);
        var utf8FullRegex = GetUtf8FullRegex();
        if (utf8FullRegex is not null)
        {
            // Whole subject from the start offset: see FullMatch for the pos contract. Native detailed paths verify lookbehind, so they stay available here.
            var utf8Match = utf8FullRegex.MatchDetailedFromUtf16Offset(
                input,
                GetUtf16OffsetOfBytePrefix(input, startOffsetInBytes));
            if (!utf8Match.Success)
            {
                return default;
            }

            return CreateMatchContextFromUtf8(input, utf8Match);
        }

        ValidateStartOffset(input, startOffsetInBytes);
        var subject = Decode(input);
        var startOffsetInUtf16 = GetUtf16OffsetOfBytePrefix(input, startOffsetInBytes);
        var match = _lazyManagedFullRegex.Value.Match(subject, startOffsetInUtf16);
        if (!match.Success)
        {
            return default;
        }

        var indexMap = PythonReUtf8IndexMap.Create(input, subject);
        return CreateMatchContext(input, match, indexMap);
    }

    /// <summary>Matches the complete UTF-8 suffix and exposes its capture context, observing cancellation before matching begins.</summary>
    /// <param name="input">The well-formed UTF-8 input to match against.</param>
    /// <param name="startOffsetInBytes">The zero-based UTF-8 byte offset where matching starts.</param>
    /// <param name="cancellationToken">The token to observe before matching begins.</param>
    /// <exception cref="OperationCanceledException">The token was already canceled.</exception>
    public Utf8PythonMatchContext FullMatchDetailed(ReadOnlySpan<byte> input, int startOffsetInBytes, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return FullMatchDetailedCore(input, startOffsetInBytes, cancellationToken);
    }

    /// <summary>Returns the first matched value as a string, or <see langword="null"/>.</summary>
    public string? SearchToString(ReadOnlySpan<byte> input) => SearchToString(input, 0);

    /// <summary>Returns the first matched value after a UTF-8 byte offset, or <see langword="null"/>.</summary>
    public string? SearchToString(ReadOnlySpan<byte> input, int startOffsetInBytes)
    {
        var match = Search(input, startOffsetInBytes);
        return match.Success ? match.GetValueString() : null;
    }

    /// <summary>Returns the first matched value after a UTF-8 byte offset as a string, observing cancellation before matching begins.</summary>
    /// <param name="input">The well-formed UTF-8 input to match against.</param>
    /// <param name="startOffsetInBytes">The zero-based UTF-8 byte offset where matching starts.</param>
    /// <param name="cancellationToken">The token to observe before matching begins.</param>
    /// <exception cref="OperationCanceledException">The token was already canceled.</exception>
    public string? SearchToString(ReadOnlySpan<byte> input, int startOffsetInBytes, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var match = Search(input, startOffsetInBytes, cancellationToken);
        return match.Success ? match.GetValueString() : null;
    }

    /// <summary>Returns the prefix match as a string, or <see langword="null"/>.</summary>
    public string? MatchToString(ReadOnlySpan<byte> input) => MatchToString(input, 0);

    /// <summary>Returns the match at a UTF-8 byte offset as a string, or <see langword="null"/>.</summary>
    public string? MatchToString(ReadOnlySpan<byte> input, int startOffsetInBytes)
    {
        var match = Match(input, startOffsetInBytes);
        return match.Success ? match.GetValueString() : null;
    }

    /// <summary>Returns the match at a UTF-8 byte offset as a string, observing cancellation before matching begins.</summary>
    /// <param name="input">The well-formed UTF-8 input to match against.</param>
    /// <param name="startOffsetInBytes">The zero-based UTF-8 byte offset where matching starts.</param>
    /// <param name="cancellationToken">The token to observe before matching begins.</param>
    /// <exception cref="OperationCanceledException">The token was already canceled.</exception>
    public string? MatchToString(ReadOnlySpan<byte> input, int startOffsetInBytes, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var match = Match(input, startOffsetInBytes, cancellationToken);
        return match.Success ? match.GetValueString() : null;
    }

    /// <summary>Returns the complete-input match as a string, or <see langword="null"/>.</summary>
    public string? FullMatchToString(ReadOnlySpan<byte> input) => FullMatchToString(input, 0);

    /// <summary>Returns the complete-suffix match as a string, or <see langword="null"/>.</summary>
    public string? FullMatchToString(ReadOnlySpan<byte> input, int startOffsetInBytes)
    {
        var match = FullMatch(input, startOffsetInBytes);
        return match.Success ? match.GetValueString() : null;
    }

    /// <summary>Returns the complete-suffix match as a string, observing cancellation before matching begins.</summary>
    /// <param name="input">The well-formed UTF-8 input to match against.</param>
    /// <param name="startOffsetInBytes">The zero-based UTF-8 byte offset where matching starts.</param>
    /// <param name="cancellationToken">The token to observe before matching begins.</param>
    /// <exception cref="OperationCanceledException">The token was already canceled.</exception>
    public string? FullMatchToString(ReadOnlySpan<byte> input, int startOffsetInBytes, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var match = FullMatch(input, startOffsetInBytes, cancellationToken);
        return match.Success ? match.GetValueString() : null;
    }

    /// <summary>Finds and materializes the first match and all capture data.</summary>
    public Utf8PythonDetailedMatchData SearchDetailedData(ReadOnlySpan<byte> input) => SearchDetailedData(input, 0);

    /// <summary>Finds and materializes capture data after a zero-based UTF-8 byte offset.</summary>
    public Utf8PythonDetailedMatchData SearchDetailedData(ReadOnlySpan<byte> input, int startOffsetInBytes)
        => SearchDetailedDataCore(input, startOffsetInBytes, CancellationToken.None);

    private Utf8PythonDetailedMatchData SearchDetailedDataCore(ReadOnlySpan<byte> input, int startOffsetInBytes, CancellationToken cancellationToken)
    {
        if (TryMatchScalar(input, startOffsetInBytes, 0, true, cancellationToken, out _, out var scalarData))
            return scalarData;

        if (_utf8Regex is not null)
        {
            ValidateStartOffset(input, startOffsetInBytes);
            var utf8StartOffsetInUtf16 = GetUtf16OffsetOfBytePrefix(input, startOffsetInBytes);
            var utf8Match = _utf8Regex.MatchDetailedFromUtf16Offset(input, utf8StartOffsetInUtf16);
            return CreateDetailedMatchDataFromUtf8(input, utf8Match);
        }

        ValidateStartOffset(input, startOffsetInBytes);
        var subject = Decode(input);
        var startOffsetInUtf16 = GetUtf16OffsetOfBytePrefix(input, startOffsetInBytes);
        var match = _managedRegex.Value.Match(subject, startOffsetInUtf16);
        var indexMap = PythonReUtf8IndexMap.Create(input, subject);
        return CreateDetailedMatchData(input, match, indexMap);
    }

    /// <summary>Finds and materializes capture data after a zero-based UTF-8 byte offset, observing cancellation before matching begins.</summary>
    /// <param name="input">The well-formed UTF-8 input to match against.</param>
    /// <param name="startOffsetInBytes">The zero-based UTF-8 byte offset where matching starts.</param>
    /// <param name="cancellationToken">The token to observe before matching begins.</param>
    /// <exception cref="OperationCanceledException">The token was already canceled.</exception>
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
        if (TryMatchScalar(input, startOffsetInBytes, 1, true, cancellationToken, out _, out var scalarData))
            return scalarData;

        if (_utf8Regex is not null)
        {
            ValidateStartOffset(input, startOffsetInBytes);
            var utf8StartOffsetInUtf16 = GetUtf16OffsetOfBytePrefix(input, startOffsetInBytes);
            var utf8Match = _utf8Regex.MatchDetailedFromUtf16Offset(input, utf8StartOffsetInUtf16);
            if (!utf8Match.Success || utf8Match.IndexInUtf16 != utf8StartOffsetInUtf16)
            {
                return default;
            }

            return CreateDetailedMatchDataFromUtf8(input, utf8Match);
        }

        ValidateStartOffset(input, startOffsetInBytes);
        var subject = Decode(input);
        var startOffsetInUtf16 = GetUtf16OffsetOfBytePrefix(input, startOffsetInBytes);
        var match = _managedRegex.Value.Match(subject, startOffsetInUtf16);
        if (!match.Success || match.Index != startOffsetInUtf16)
        {
            return default;
        }

        var indexMap = PythonReUtf8IndexMap.Create(input, subject);
        return CreateDetailedMatchData(input, match, indexMap);
    }

    /// <summary>Matches at a zero-based UTF-8 byte offset and materializes all capture data, observing cancellation before matching begins.</summary>
    /// <param name="input">The well-formed UTF-8 input to match against.</param>
    /// <param name="startOffsetInBytes">The zero-based UTF-8 byte offset where matching starts.</param>
    /// <param name="cancellationToken">The token to observe before matching begins.</param>
    /// <exception cref="OperationCanceledException">The token was already canceled.</exception>
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
        if (TryMatchScalar(input, startOffsetInBytes, 2, true, cancellationToken, out _, out var scalarData))
            return scalarData;

        ValidateStartOffset(input, startOffsetInBytes);
        var utf8FullRegex = GetUtf8FullRegex();
        if (utf8FullRegex is not null)
        {
            // Whole subject from the start offset: see FullMatch for the pos contract. Native detailed paths verify lookbehind, so they stay available here.
            var utf8Match = utf8FullRegex.MatchDetailedFromUtf16Offset(
                input,
                GetUtf16OffsetOfBytePrefix(input, startOffsetInBytes));
            if (!utf8Match.Success)
            {
                return default;
            }

            return CreateDetailedMatchDataFromUtf8(input, utf8Match);
        }

        ValidateStartOffset(input, startOffsetInBytes);
        var subject = Decode(input);
        var startOffsetInUtf16 = GetUtf16OffsetOfBytePrefix(input, startOffsetInBytes);
        var match = _lazyManagedFullRegex.Value.Match(subject, startOffsetInUtf16);
        if (!match.Success)
        {
            return default;
        }

        var indexMap = PythonReUtf8IndexMap.Create(input, subject);
        return CreateDetailedMatchData(input, match, indexMap);
    }

    /// <summary>Matches the complete UTF-8 suffix and materializes all capture data, observing cancellation before matching begins.</summary>
    /// <param name="input">The well-formed UTF-8 input to match against.</param>
    /// <param name="startOffsetInBytes">The zero-based UTF-8 byte offset where matching starts.</param>
    /// <param name="cancellationToken">The token to observe before matching begins.</param>
    /// <exception cref="OperationCanceledException">The token was already canceled.</exception>
    public Utf8PythonDetailedMatchData FullMatchDetailedData(ReadOnlySpan<byte> input, int startOffsetInBytes, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return FullMatchDetailedDataCore(input, startOffsetInBytes, cancellationToken);
    }

}
