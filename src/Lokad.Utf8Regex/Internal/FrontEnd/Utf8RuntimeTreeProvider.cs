namespace Lokad.Utf8Regex.Internal.FrontEnd;

using RuntimeFrontEnd = Lokad.Utf8Regex.Internal.FrontEnd.Runtime;

internal static class Utf8RuntimeTreeProvider
{
    // Strangler seam: owned-first with legacy fallback. The owned parser covers
    // the pinned subset and declines everything else, so uncovered shapes keep
    // exact legacy behavior including construction throws. Search analysis now
    // consumes owned find facts, which is what the reverted attempt was missing.
    // TryParseLegacy stays as the differential-battery oracle.
    //
    // No-vendor rule: the Runtime/* fork is never extended. The owned scan
    // below replicates the fork-independent BCL verdicts that used to be
    // patched into the fork, so declining still lands on BCL-exact behavior:
    // - nesting past MaxLegacyNestingDepth skips the fork, whose analyses
    //   recurse without bounds; the pattern still serves through BCL fallback.
    // - a shorthand or category escape in upper-bound class position (right
    //   after a single-char atom plus '-') is BCL-invalid; declining routes
    //   construction to the BCL fallback, which raises the BCL-exact error.
    // - unknown \p{name} leaks ArgumentException out of the fork; mapping it
    //   to a decline routes those to the same BCL-exact failure.
    private const int MaxLegacyNestingDepth = 256;

    public static RuntimeFrontEnd.RegexTree? TryParse(string pattern, RegexOptions options)
    {
        if (Utf8OwnedTreeParser.TryParse(pattern, options) is { } owned)
        {
            return owned;
        }

        ScanFallbackShape(pattern, out var nestingDepth, out var hasRejectedRange);
        if (nestingDepth > MaxLegacyNestingDepth || hasRejectedRange)
        {
            return null;
        }

        return TryParseLegacy(pattern, options);
    }

    public static RuntimeFrontEnd.RegexTree? TryParseLegacy(string pattern, RegexOptions options)
    {
        try
        {
            return RuntimeFrontEnd.RegexParser.Parse(
                pattern,
                options,
                RuntimeFrontEnd.RegexParser.GetTargetCulture(options));
        }
        catch (RuntimeFrontEnd.RegexParseException)
        {
            return null;
        }
        catch (ArgumentException ex) when (ex.ParamName == "categoryName")
        {
            return null;
        }
    }

    // Single iterative pass over the pattern text. Counts group nesting while
    // tracking class-range state, so neither the depth gate nor the
    // upper-bound-escape gate recurses. Escapes and character classes are
    // honored; (?#...) and (?x) comment parens may over-count depth, which
    // only diverts to the still-correct fallback route.
    private static void ScanFallbackShape(string pattern, out int maxNestingDepth, out bool hasRejectedRange)
    {
        maxNestingDepth = 0;
        hasRejectedRange = false;
        var depth = 0;
        var inClass = false;
        var classFirst = false;
        var hasAtom = false;
        var lowerIsSet = false;
        var pendingUpper = false;
        var i = 0;
        while (i < pattern.Length)
        {
            var c = pattern[i];
            if (inClass)
            {
                if (c == '\\' && i + 1 < pattern.Length)
                {
                    var e = pattern[i + 1];
                    if (e is 'p' or 'P')
                    {
                        if (pendingUpper && !lowerIsSet)
                        {
                            hasRejectedRange = true;
                            return;
                        }

                        i += 2;
                        if (i < pattern.Length && pattern[i] == '{')
                        {
                            var close = pattern.IndexOf('}', i + 1);
                            i = close < 0 ? pattern.Length : close + 1;
                        }

                        hasAtom = true;
                        lowerIsSet = true;
                        pendingUpper = false;
                        classFirst = false;
                        continue;
                    }

                    if (pendingUpper && !lowerIsSet && e is 'd' or 'D' or 's' or 'S' or 'w' or 'W')
                    {
                        hasRejectedRange = true;
                        return;
                    }

                    i += 2;
                    hasAtom = true;
                    lowerIsSet = false;
                    pendingUpper = false;
                    classFirst = false;
                    continue;
                }

                if (c == ']' && !classFirst)
                {
                    inClass = false;
                    hasAtom = false;
                    pendingUpper = false;
                    i++;
                    continue;
                }

                if (c == '-' && hasAtom && !classFirst)
                {
                    pendingUpper = true;
                    i++;
                    continue;
                }

                if (c == '^' && classFirst)
                {
                    i++;
                    continue;
                }

                hasAtom = true;
                lowerIsSet = false;
                pendingUpper = false;
                classFirst = false;
                i++;
                continue;
            }

            if (c == '\\' && i + 1 < pattern.Length)
            {
                i += 2;
                continue;
            }

            if (c == '[')
            {
                inClass = true;
                classFirst = true;
                hasAtom = false;
                lowerIsSet = false;
                pendingUpper = false;
                i++;
                continue;
            }

            if (c == '(')
            {
                depth++;
                if (depth > maxNestingDepth)
                {
                    maxNestingDepth = depth;
                }

                i++;
                continue;
            }

            if (c == ')' && depth > 0)
            {
                depth--;
                i++;
                continue;
            }

            i++;
        }
    }
}