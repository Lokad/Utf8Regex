namespace Lokad.Utf8Regex.Internal.Execution;

using RuntimeFrontEnd = Lokad.Utf8Regex.Internal.FrontEnd.Runtime;

internal static partial class Utf8AsciiSimplePatternLowerer
{
    private static bool TryLowerSet(string? set, out AsciiSimplePatternToken token)
    {
        if (set is null)
        {
            token = default;
            return false;
        }

        if (set == RuntimeFrontEnd.RegexCharClass.NotNewLineClass)
        {
            token = AsciiSimplePatternToken.Dot;
            return true;
        }

        if (!TryCreateAsciiCharClass(set, out var charClass))
        {
            token = default;
            return false;
        }

        token = new AsciiSimplePatternToken(charClass);
        return true;
    }

    private static bool TryCreateAsciiCharClass(string runtimeSet, out AsciiCharClass charClass)
    {
        if (TryCreateKnownAsciiPredefinedCharClass(runtimeSet, out charClass))
        {
            return true;
        }

        if (!CanProjectRuntimeSetToAscii(runtimeSet))
        {
            charClass = null!;
            return false;
        }

        var negated = RuntimeFrontEnd.RegexCharClass.IsNegated(runtimeSet);
        var matches = new bool[128];
        for (var i = 0; i < matches.Length; i++)
        {
            matches[i] = RuntimeFrontEnd.RegexCharClass.CharInClassBase((char)i, runtimeSet);
        }

        charClass = new AsciiCharClass(matches, negated);
        return true;
    }

    private static bool TryCreateKnownAsciiPredefinedCharClass(string runtimeSet, out AsciiCharClass charClass)
    {
        switch (runtimeSet)
        {
            case RuntimeFrontEnd.RegexCharClass.SpaceClass:
            case RuntimeFrontEnd.RegexCharClass.ECMASpaceClass:
                charClass = CreateAsciiCharClass(static ch => ch is ' ' or '\t' or '\r' or '\n' or '\f' or '\v', negated: false);
                return true;

            case RuntimeFrontEnd.RegexCharClass.NotSpaceClass:
            case RuntimeFrontEnd.RegexCharClass.NotECMASpaceClass:
                charClass = CreateAsciiCharClass(static ch => ch is ' ' or '\t' or '\r' or '\n' or '\f' or '\v', negated: true);
                return true;

            default:
                charClass = null!;
                return false;
        }
    }

    private static bool CanProjectRuntimeSetToAscii(string runtimeSet)
    {
        return RuntimeFrontEnd.RegexCharClass.IsAscii(runtimeSet);
    }

    private static string? GetCategoryPayload(string runtimeSet)
    {
        if (runtimeSet.Length < RuntimeFrontEnd.RegexCharClass.SetStartIndex)
        {
            return null;
        }

        var setLength = runtimeSet[RuntimeFrontEnd.RegexCharClass.SetLengthIndex];
        var categoryLength = runtimeSet[RuntimeFrontEnd.RegexCharClass.CategoryLengthIndex];
        if (categoryLength == 0)
        {
            return string.Empty;
        }

        var setEnd = RuntimeFrontEnd.RegexCharClass.SetStartIndex + setLength;
        if (runtimeSet.Length < setEnd + categoryLength)
        {
            return null;
        }

        for (var i = RuntimeFrontEnd.RegexCharClass.SetStartIndex; i < setEnd; i += 2)
        {
            if (runtimeSet[i + 1] > 0x80)
            {
                return null;
            }
        }

        return runtimeSet.Substring(setEnd, categoryLength);
    }

    private static bool TryExtractCharClassRunPlan(
        AsciiSimplePatternToken[][] branches,
        bool isStartAnchored,
        bool isEndAnchored,
        out AsciiSimplePatternRunPlan runPlan)
    {
        runPlan = default;

        if (isStartAnchored || isEndAnchored || branches.Length == 0)
        {
            return false;
        }

        if (branches[0].Length == 0 ||
            branches[0][0].Kind != AsciiSimplePatternTokenKind.CharClass ||
            branches[0][0].CharClass is not { Negated: false } firstClass)
        {
            return false;
        }

        var minLength = branches[0].Length;
        var maxLength = branches[0].Length;
        for (var i = 0; i < branches.Length; i++)
        {
            var branch = branches[i];
            if (branch.Length == 0)
            {
                return false;
            }

            if (branch.Length < minLength)
            {
                minLength = branch.Length;
            }

            if (branch.Length > maxLength)
            {
                maxLength = branch.Length;
            }

            for (var j = 0; j < branch.Length; j++)
            {
                if (branch[j].Kind != AsciiSimplePatternTokenKind.CharClass ||
                    branch[j].CharClass is not { } branchClass ||
                    !firstClass.HasSameDefinition(branchClass))
                {
                    return false;
                }
            }
        }

        runPlan = new AsciiSimplePatternRunPlan(firstClass, minLength, maxLength);
        return true;
    }

    private static AsciiCharClass CreateAsciiCharClass(Func<char, bool> predicate, bool negated)
    {
        var matches = new bool[128];
        for (var i = 0; i < matches.Length; i++)
        {
            if (predicate((char)i))
            {
                matches[i] = true;
            }
        }

        return new AsciiCharClass(matches, negated);
    }
}
