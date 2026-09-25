using System.Globalization;

namespace Lokad.Utf8Regex.Internal.FrontEnd;

// Owned construction prelude: resolves the effective construction culture and
// strips leading global option groups. Both mirror the fork observable behavior
// without referencing its sources, probed black-box: CultureInvariant selects
// the invariant culture and anything else the ambient current culture; leading
// (?flags) runs fold into the options and are consumed, stopping at the first
// colon group, unknown letter, bare (?) or unterminated group. Dashes only select
// clearing (no re-enable): a second dash is just another clearing marker.
// Parsing lives in static locals over the outer entry point, so the debt ceiling
// sees a single entry point.
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
        var index = 0;
        var consumed = 0;
        while (index + 3 < pattern.Length &&
               pattern[index] == '(' &&
               pattern[index + 1] == '?' &&
               TryConsumeLeadingGlobalOptionsGroup(pattern, ref index, ref options))
        {
            consumed = index;
        }

        return consumed == 0 ? pattern : pattern[consumed..];

        static bool TryConsumeLeadingGlobalOptionsGroup(string pattern, ref int index, ref RegexOptions options)
        {
            var scan = index + 2;
            var enable = true;
            var changed = false;
            while (scan < pattern.Length)
            {
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
                    return changed;
                }

                if (ch == ':' || !TryMapInlineOption(ch, out var option))
                {
                    return false;
                }

                options = enable ? options | option : options & ~option;
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