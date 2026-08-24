using Lokad.Utf8Regex.Internal.Execution;
namespace Lokad.Utf8Regex.Internal.FrontEnd;

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

        if (DotNetAsciiCharClassProjector.TryProjectWholeClass(set, out var charClass))
        {
            token = new AsciiSimplePatternToken(
                charClass,
                DotNetAsciiCharClassProjector.RequiresAsciiInput(set),
                set == RuntimeFrontEnd.RegexCharClass.SpaceClass
                    ? Utf8SimplePatternScalarClassKind.UnicodeWhitespace
                    : Utf8SimplePatternScalarClassKind.None);
            return true;
        }

        if (!DotNetAsciiCharClassProjector.TryProjectAsciiIntersection(set, out charClass))
        {
            token = default;
            return false;
        }

        token = new AsciiSimplePatternToken(charClass, requiresAsciiInput: true);
        return true;
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
                    branch[j].CharClass.IsEmpty ||
                    !firstClass.HasSameDefinition(branch[j].CharClass))
                {
                    return false;
                }
            }
        }

        runPlan = new AsciiSimplePatternRunPlan(firstClass, minLength, maxLength);
        return true;
    }

}
