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
    {
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
        var match = _managedRegex.Match(subject, startOffsetInUtf16);
        return CreateMatchContext(input, match, indexMap);
    }

    /// <summary>Matches at the beginning of the input and exposes its capture context.</summary>
    public Utf8PythonMatchContext MatchDetailed(ReadOnlySpan<byte> input) => MatchDetailed(input, 0);

    /// <summary>Matches at a zero-based UTF-8 byte offset and exposes its capture context.</summary>
    public Utf8PythonMatchContext MatchDetailed(ReadOnlySpan<byte> input, int startOffsetInBytes)
    {
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
        var match = _managedRegex.Match(subject, startOffsetInUtf16);
        if (!match.Success || match.Index != startOffsetInUtf16)
        {
            return default;
        }

        var indexMap = PythonReUtf8IndexMap.Create(input, subject);
        return CreateMatchContext(input, match, indexMap);
    }

    /// <summary>Matches the complete input and exposes its capture context.</summary>
    public Utf8PythonMatchContext FullMatchDetailed(ReadOnlySpan<byte> input) => FullMatchDetailed(input, 0);

    /// <summary>Matches the complete UTF-8 suffix and exposes its capture context.</summary>
    public Utf8PythonMatchContext FullMatchDetailed(ReadOnlySpan<byte> input, int startOffsetInBytes)
    {
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

    /// <summary>Returns the first matched value as a string, or <see langword="null"/>.</summary>
    public string? SearchToString(ReadOnlySpan<byte> input) => SearchToString(input, 0);

    /// <summary>Returns the first matched value after a UTF-8 byte offset, or <see langword="null"/>.</summary>
    public string? SearchToString(ReadOnlySpan<byte> input, int startOffsetInBytes)
    {
        var match = Search(input, startOffsetInBytes);
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

    /// <summary>Returns the complete-input match as a string, or <see langword="null"/>.</summary>
    public string? FullMatchToString(ReadOnlySpan<byte> input) => FullMatchToString(input, 0);

    /// <summary>Returns the complete-suffix match as a string, or <see langword="null"/>.</summary>
    public string? FullMatchToString(ReadOnlySpan<byte> input, int startOffsetInBytes)
    {
        var match = FullMatch(input, startOffsetInBytes);
        return match.Success ? match.GetValueString() : null;
    }

    /// <summary>Finds and materializes the first match and all capture data.</summary>
    public Utf8PythonDetailedMatchData SearchDetailedData(ReadOnlySpan<byte> input) => SearchDetailedData(input, 0);

    /// <summary>Finds and materializes capture data after a zero-based UTF-8 byte offset.</summary>
    public Utf8PythonDetailedMatchData SearchDetailedData(ReadOnlySpan<byte> input, int startOffsetInBytes)
    {
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
        var match = _managedRegex.Match(subject, startOffsetInUtf16);
        var indexMap = PythonReUtf8IndexMap.Create(input, subject);
        return CreateDetailedMatchData(input, match, indexMap);
    }

    /// <summary>Matches at the beginning and materializes all capture data.</summary>
    public Utf8PythonDetailedMatchData MatchDetailedData(ReadOnlySpan<byte> input) => MatchDetailedData(input, 0);

    /// <summary>Matches at a zero-based UTF-8 byte offset and materializes all capture data.</summary>
    public Utf8PythonDetailedMatchData MatchDetailedData(ReadOnlySpan<byte> input, int startOffsetInBytes)
    {
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
        var match = _managedRegex.Match(subject, startOffsetInUtf16);
        if (!match.Success || match.Index != startOffsetInUtf16)
        {
            return default;
        }

        var indexMap = PythonReUtf8IndexMap.Create(input, subject);
        return CreateDetailedMatchData(input, match, indexMap);
    }

    /// <summary>Matches the complete input and materializes all capture data.</summary>
    public Utf8PythonDetailedMatchData FullMatchDetailedData(ReadOnlySpan<byte> input) => FullMatchDetailedData(input, 0);

    /// <summary>Matches the complete UTF-8 suffix and materializes all capture data.</summary>
    public Utf8PythonDetailedMatchData FullMatchDetailedData(ReadOnlySpan<byte> input, int startOffsetInBytes)
    {
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
}
