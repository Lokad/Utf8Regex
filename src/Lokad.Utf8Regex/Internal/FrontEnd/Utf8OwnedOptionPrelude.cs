using System.Globalization;

namespace Lokad.Utf8Regex.Internal.FrontEnd;

// Owned construction prelude: resolves the effective construction culture and
// strips leading global option groups. Both mirror the fork observable behavior
// without referencing its sources, probed black-box: CultureInvariant selects
// the invariant culture and anything else the ambient current culture; leading
// (?flags) runs fold into the options and are consumed, stopping at the first
// colon group, unknown letter, bare (?) or unterminated group. Dashes only select
// clearing (no re-enable): a second dash is just another clearing marker.
internal static class Utf8OwnedOptionPrelude
{
    public static CultureInfo GetTargetCulture(RegexOptions options)
    {
        return (options & RegexOptions.CultureInvariant) != 0
            ? CultureInfo.InvariantCulture
            : CultureInfo.CurrentCulture;
    }

    public static string NormalizeLeadingGlobalOptions(string pattern, ref RegexOptions options)
    {
        using var owner = Utf8ConstructionScope.Enter();
        var stagedOptions = options;
        var index = 0;
        var consumed = 0;
        while (pattern.Length - index > 3 &&
               pattern[index] == '(' &&
               pattern[index + 1] == '?' &&
               TryConsumeLeadingGlobalOptionsGroup(owner.Budget, pattern, ref index, ref stagedOptions))
        {
            owner.Budget.ChargeWork(1);
            consumed = index;
        }

        owner.Budget.ChargeWork(1);
        var result = consumed == 0 ? pattern : owner.Budget.CopyText(pattern.AsSpan(consumed));
        options = stagedOptions;
        owner.Complete();
        return result;

        static bool TryConsumeLeadingGlobalOptionsGroup(Utf8ConstructionBudget budget, string pattern, ref int index, ref RegexOptions options)
        {
            var scan = index + 2;
            var enable = true;
            var changed = false;
            var staged = options;
            while (scan < pattern.Length)
            {
                budget.ChargeWork(1);
                var ch = pattern[scan];
                if (ch == '-')
                {
                    enable = false;
                    scan++;
                    continue;
                }

                if (ch == ')')
                {
                    index = scan + 1;
                    options = staged;
                    return changed;
                }

                if (ch == ':' || !TryMapInlineOption(ch, out var option))
                {
                    return false;
                }

                staged = enable ? staged | option : staged & ~option;
                changed = true;
                scan++;
            }

            return false;
        }

        static bool TryMapInlineOption(char ch, out RegexOptions option)
        {
            option = ch switch
            {
                'i' => RegexOptions.IgnoreCase,
                'm' => RegexOptions.Multiline,
                'n' => RegexOptions.ExplicitCapture,
                's' => RegexOptions.Singleline,
                'x' => RegexOptions.IgnorePatternWhitespace,
                _ => RegexOptions.None,
            };

            return option != RegexOptions.None;
        }
    }
}
