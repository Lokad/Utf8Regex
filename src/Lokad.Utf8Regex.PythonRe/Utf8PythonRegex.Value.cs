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

    /// <summary>Finds the first match in the UTF-8 input.</summary>
    public Utf8PythonValueMatch Search(ReadOnlySpan<byte> input) => Search(input, 0);

    /// <summary>Finds the first match at or after a zero-based UTF-8 byte offset.</summary>
    public Utf8PythonValueMatch Search(ReadOnlySpan<byte> input, int startOffsetInBytes)
    {
        // PLAN.md C08: native value paths skip positive-lookbehind verification (core literal plans drop the assertion while detailed paths verify); keep lookbehind patterns on verified routes.
        if (_utf8Regex is not null && !_hasLookbehind)
        {
            ValidateStartOffset(input, startOffsetInBytes);
            var match = startOffsetInBytes == 0 && _canUseZeroOffsetUtf8SearchValueFastPath
                ? _utf8Regex.Match(input)
                : _utf8Regex.MatchFromUtf16Offset(
                    input,
                    GetUtf16OffsetOfBytePrefix(input, startOffsetInBytes));
            return Utf8PythonValueMatchFromUtf8Regex(input, match);
        }

        return SearchViaManagedRegex(input, startOffsetInBytes);
    }

    /// <summary>Matches at the beginning of the UTF-8 input.</summary>
    public Utf8PythonValueMatch Match(ReadOnlySpan<byte> input) => Match(input, 0);

    /// <summary>Matches exactly at a zero-based UTF-8 byte offset.</summary>
    public Utf8PythonValueMatch Match(ReadOnlySpan<byte> input, int startOffsetInBytes)
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
                ? _utf8Regex.Match(input)
                : _utf8Regex.MatchFromUtf16Offset(input, startOffsetInUtf16);
            if (!match.Success || match.IndexInUtf16 != startOffsetInUtf16)
            {
                return default;
            }

            return Utf8PythonValueMatchFromUtf8Regex(input, match);
        }

        var context = MatchDetailed(input, startOffsetInBytes);
        return context.Value;
    }

    /// <summary>Matches the complete UTF-8 input.</summary>
    public Utf8PythonValueMatch FullMatch(ReadOnlySpan<byte> input) => FullMatch(input, 0);

    /// <summary>Matches the complete UTF-8 suffix beginning at a zero-based byte offset.</summary>
    public Utf8PythonValueMatch FullMatch(ReadOnlySpan<byte> input, int startOffsetInBytes)
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
                GetUtf16OffsetOfBytePrefix(input, startOffsetInBytes));
            if (!match.Success)
            {
                return default;
            }

            return Utf8PythonValueMatchFromUtf8Regex(input, match);
        }

        return FullMatchViaManagedRegex(input, startOffsetInBytes);
    }

    /// <summary>Counts non-overlapping Python-style matches in the UTF-8 input.</summary>
    public int Count(ReadOnlySpan<byte> input) => Count(input, 0);

    /// <summary>Counts non-overlapping matches at or after a zero-based UTF-8 byte offset.</summary>
    public int Count(ReadOnlySpan<byte> input, int startOffsetInBytes)
    {
        ValidateStartOffset(input, startOffsetInBytes);
        if (_canCountAsciiWordBoundariesDirectly && MatchTimeout == Timeout.InfiniteTimeSpan)
        {
            var decoded = Decode(input);
            return CountAsciiWordBoundaries(decoded, GetUtf16OffsetOfBytePrefix(input, startOffsetInBytes));
        }

        if (_countBackend == PythonReDirectBackendKind.Utf8Regex && _utf8Regex is not null && !_hasLookbehind && startOffsetInBytes == 0)
        {
            return _utf8Regex.Count(input);
        }

        var subject = Decode(input);
        var startOffsetInUtf16 = GetUtf16OffsetOfBytePrefix(input, startOffsetInBytes);
        if (!_canMatchEmpty)
        {
            return _managedRegex.Value.Count(subject, startOffsetInUtf16);
        }

        return CountManagedMatchesPythonStyle(subject, startOffsetInUtf16);
    }
}
