namespace Lokad.Utf8Regex.Internal.Execution;

internal enum Utf8EmittedTokenFamilyKind : byte
{
    None = 0,
    BoundedDate = 1,
    Uri = 2,
}

internal sealed class Utf8EmittedTokenFamilyMatcher
{
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

        if (plan.Kind == Utf8FallbackDirectFamilyKind.Utf8UriToken)
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
            Utf8EmittedTokenFamilyKind.Uri => Utf8AsciiUriTokenExecutor.TryFindAsciiUriToken(
                input,
                startIndex,
                out matchIndex,
                out matchedLength),
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

    private static bool IsAsciiDigit(byte value) => Utf8AsciiBytePredicates.IsDigit(value);

    private static bool IsAsciiWordByte(byte value)
    {
        return Utf8AsciiBytePredicates.IsWord(value);
    }

}
