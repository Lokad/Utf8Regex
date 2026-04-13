namespace Lokad.Utf8Regex.Internal.Execution;

internal enum Utf8EmittedTokenFamilyKind : byte
{
    None = 0,
    BoundedDate = 1,
    Uri = 2,
}

internal sealed class Utf8EmittedTokenFamilyMatcher
{
    private static readonly Utf8AsciiLiteralFinder s_uriDelimiterFinder = new("://"u8);
    private readonly Utf8FallbackDirectFamilyPlan _plan;

    private Utf8EmittedTokenFamilyMatcher(Utf8FallbackDirectFamilyPlan plan, Utf8EmittedTokenFamilyKind kind)
    {
        _plan = plan;
        Kind = kind;
    }

    public Utf8EmittedTokenFamilyKind Kind { get; }

    public static bool TryCreate(in Utf8FallbackDirectFamilyPlan plan, out Utf8EmittedTokenFamilyMatcher? matcher)
    {
        if (plan.Kind == Utf8FallbackDirectFamilyKind.AsciiBoundedDateToken)
        {
            matcher = new Utf8EmittedTokenFamilyMatcher(plan, Utf8EmittedTokenFamilyKind.BoundedDate);
            return true;
        }

        if (plan.Kind == Utf8FallbackDirectFamilyKind.AsciiUriToken)
        {
            matcher = new Utf8EmittedTokenFamilyMatcher(plan, Utf8EmittedTokenFamilyKind.Uri);
            return true;
        }

        matcher = null;
        return false;
    }

    public bool TryFindNext(ReadOnlySpan<byte> input, int startIndex, out int matchIndex, out int matchedLength)
    {
        return Kind switch
        {
            Utf8EmittedTokenFamilyKind.BoundedDate => TryFindNextBoundedDate(input, startIndex, out matchIndex, out matchedLength),
            Utf8EmittedTokenFamilyKind.Uri => TryFindNextUri(input, startIndex, out matchIndex, out matchedLength),
            _ => ReturnNoMatch(out matchIndex, out matchedLength),
        };
    }

    public int Count(ReadOnlySpan<byte> input)
    {
        var count = 0;
        var startIndex = 0;
        while (TryFindNext(input, startIndex, out var matchIndex, out var matchedLength))
        {
            count++;
            startIndex = matchIndex + Math.Max(matchedLength, 1);
        }

        return count;
    }

    private bool TryFindNextBoundedDate(ReadOnlySpan<byte> input, int startIndex, out int matchIndex, out int matchedLength)
    {
        matchIndex = -1;
        matchedLength = 0;
        if ((uint)startIndex >= (uint)input.Length)
        {
            return false;
        }

        var minLength = _plan.FirstFieldMinCount + 1 + _plan.SecondFieldMinCount + 1 + _plan.ThirdFieldMinCount;
        var searchFrom = Math.Max(startIndex + minLength - 1, 0);
        while ((uint)searchFrom < (uint)input.Length)
        {
            var relative = input[searchFrom..].IndexOf(_plan.SecondSeparatorByte);
            if (relative < 0)
            {
                return false;
            }

            var secondSeparatorIndex = searchFrom + relative;
            if (TryMatchAtSecondSeparator(input, secondSeparatorIndex, out matchIndex, out matchedLength))
            {
                return true;
            }

            searchFrom = secondSeparatorIndex + 1;
        }

        return false;
    }

    private static bool TryFindNextUri(ReadOnlySpan<byte> input, int startIndex, out int matchIndex, out int matchedLength)
    {
        matchIndex = -1;
        matchedLength = 0;
        if ((uint)startIndex > (uint)input.Length)
        {
            return false;
        }

        var searchFrom = startIndex;
        while (s_uriDelimiterFinder.TryFindNext(input, searchFrom, out var delimiterIndex))
        {
            searchFrom = delimiterIndex + 3;
            if (TryMatchUriAtDelimiter(input, startIndex, delimiterIndex, out matchIndex, out matchedLength))
            {
                return true;
            }
        }

        return false;
    }

    private bool TryMatchAtSecondSeparator(ReadOnlySpan<byte> input, int secondSeparatorIndex, out int matchIndex, out int matchedLength)
    {
        matchIndex = -1;
        matchedLength = 0;

        if (secondSeparatorIndex <= 0)
        {
            return false;
        }

        var thirdStart = secondSeparatorIndex + 1;
        if ((uint)thirdStart >= (uint)input.Length || !IsAsciiDigit(input[thirdStart]))
        {
            return false;
        }

        var thirdEnd = thirdStart;
        while ((uint)thirdEnd < (uint)input.Length &&
               thirdEnd - thirdStart < _plan.ThirdFieldMaxCount &&
               IsAsciiDigit(input[thirdEnd]))
        {
            thirdEnd++;
        }

        var thirdLength = thirdEnd - thirdStart;
        if (thirdLength < _plan.ThirdFieldMinCount)
        {
            return false;
        }

        if (_plan.RequireTrailingBoundary && !HasTrailingBoundary(input, thirdEnd))
        {
            return false;
        }

        var secondFieldEnd = secondSeparatorIndex;
        var secondFieldStart = secondFieldEnd;
        while (secondFieldStart > 0 &&
               secondFieldEnd - secondFieldStart < _plan.SecondFieldMaxCount &&
               IsAsciiDigit(input[secondFieldStart - 1]))
        {
            secondFieldStart--;
        }

        var secondFieldLength = secondFieldEnd - secondFieldStart;
        if (secondFieldLength < _plan.SecondFieldMinCount || secondFieldStart <= 0 || input[secondFieldStart - 1] != _plan.SeparatorByte)
        {
            return false;
        }

        var firstSeparatorIndex = secondFieldStart - 1;
        var firstFieldEnd = firstSeparatorIndex;
        var firstFieldStart = firstFieldEnd;
        while (firstFieldStart > 0 &&
               firstFieldEnd - firstFieldStart < _plan.FirstFieldMaxCount &&
               IsAsciiDigit(input[firstFieldStart - 1]))
        {
            firstFieldStart--;
        }

        var firstFieldLength = firstFieldEnd - firstFieldStart;
        if (firstFieldLength < _plan.FirstFieldMinCount)
        {
            return false;
        }

        if (_plan.RequireLeadingBoundary && !HasLeadingBoundary(input, firstFieldStart))
        {
            return false;
        }

        matchIndex = firstFieldStart;
        matchedLength = thirdEnd - firstFieldStart;
        return true;
    }

    private static bool TryMatchUriAtDelimiter(ReadOnlySpan<byte> input, int minStartIndex, int delimiterIndex, out int matchIndex, out int matchedLength)
    {
        matchIndex = -1;
        matchedLength = 0;

        var schemeStart = delimiterIndex;
        while (schemeStart > minStartIndex && IsAsciiWordByte(input[schemeStart - 1]))
        {
            schemeStart--;
        }

        if (schemeStart == delimiterIndex)
        {
            return false;
        }

        if (schemeStart > 0 && input[schemeStart - 1] >= 0x80)
        {
            return false;
        }

        var index = delimiterIndex + 3;
        if ((uint)(index + 1) >= (uint)input.Length)
        {
            return false;
        }

        if (!IsAsciiUriBodyStart(input[index]) || !IsAsciiUriBodyContinuation(input[index + 1]))
        {
            return false;
        }

        index += 2;
        while ((uint)index < (uint)input.Length && IsAsciiUriBodyContinuation(input[index]))
        {
            index++;
        }

        if ((uint)index < (uint)input.Length)
        {
            if (input[index] >= 0x80)
            {
                return false;
            }

            if (input[index] == (byte)'?')
            {
                index++;
                while ((uint)index < (uint)input.Length && IsAsciiUriQueryByte(input[index]))
                {
                    index++;
                }

                if ((uint)index < (uint)input.Length && input[index] >= 0x80)
                {
                    return false;
                }
            }

            if ((uint)index < (uint)input.Length && input[index] == (byte)'#')
            {
                index++;
                while ((uint)index < (uint)input.Length && IsAsciiUriFragmentByte(input[index]))
                {
                    index++;
                }

                if ((uint)index < (uint)input.Length && input[index] >= 0x80)
                {
                    return false;
                }
            }
        }

        matchIndex = schemeStart;
        matchedLength = index - schemeStart;
        return true;
    }

    private static bool ReturnNoMatch(out int matchIndex, out int matchedLength)
    {
        matchIndex = -1;
        matchedLength = 0;
        return false;
    }

    private static bool HasLeadingBoundary(ReadOnlySpan<byte> input, int index)
    {
        return index <= 0 || !IsAsciiWordByte(input[index - 1]);
    }

    private static bool HasTrailingBoundary(ReadOnlySpan<byte> input, int index)
    {
        return (uint)index >= (uint)input.Length || !IsAsciiWordByte(input[index]);
    }

    private static bool IsAsciiDigit(byte value) => value is >= (byte)'0' and <= (byte)'9';

    private static bool IsAsciiWordByte(byte value)
    {
        return value is >= (byte)'0' and <= (byte)'9' or
            >= (byte)'A' and <= (byte)'Z' or
            >= (byte)'a' and <= (byte)'z' or
            (byte)'_';
    }

    private static bool IsAsciiWhitespace(byte value)
    {
        return value is (byte)' ' or (byte)'\t' or (byte)'\r' or (byte)'\n' or (byte)'\f' or (byte)'\v';
    }

    private static bool IsAsciiUriBodyStart(byte value)
    {
        return value < 0x80 &&
            value != (byte)'/' &&
            value != (byte)'?' &&
            value != (byte)'#' &&
            !IsAsciiWhitespace(value);
    }

    private static bool IsAsciiUriBodyContinuation(byte value)
    {
        return value < 0x80 &&
            value != (byte)'?' &&
            value != (byte)'#' &&
            !IsAsciiWhitespace(value);
    }

    private static bool IsAsciiUriQueryByte(byte value)
    {
        return value < 0x80 &&
            value != (byte)'#' &&
            !IsAsciiWhitespace(value);
    }

    private static bool IsAsciiUriFragmentByte(byte value)
    {
        return value < 0x80 && !IsAsciiWhitespace(value);
    }
}
