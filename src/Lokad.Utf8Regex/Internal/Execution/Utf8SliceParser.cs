using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.FrontEnd;

namespace Lokad.Utf8Regex.Internal.Execution;

/// <summary>Recognizes the owned R02a slice: UTF-16 patterns built from
/// literals, concatenation, ordered alternation, greedy/lazy repetition,
/// numbered and named groups (BCL unnamed-first numbering), balancing
/// captures, atomic groups, transparent non-capturing groups and inline comments,
/// (R03) dots
/// with Singleline-scoped newline matching, anchors with Multiline-scoped line
/// edges, UTF-16 character classes (literals, ranges, supported escapes, and
/// shorthand members), word boundaries, shorthand classes, backreferences,
/// conditional alternations (group tests with committed branches, expression
/// tests via lookahead desugar), and zero-width lookarounds (dots, negated classes,
/// and negated shorthands
/// decline astral inputs at routing). IgnoreCase folds per-scalar BCL-invariant
/// case (Turkic instances keep legacy routing through the construction gate).
/// Unicode property escapes (general categories/groups and named blocks as
/// bare atoms or single-category classes, including blocks under IgnoreCase
/// (matched through invariant case equivalents). Assertion bodies establish
/// their own direction and share the invocation's capture/balancing journal.
/// Unsupported grammar declines to <c>null</c> so existing routes preserve
/// its behavior, including construction failures. Group
/// nesting beyond <see cref="MaxNestingDepth"/> also declines. R02b construction
/// admission: patterns longer than <see cref="MaxPatternLength"/> decline before any
/// parse work so retained slice programs stay bounded; declined shapes keep
/// legacy routing.</summary>
internal static class Utf8SliceParser
{
    internal const int MaxNestingDepth = 64;

    // Defense cap, not a language limit: the longest surveyed corpus and test
    // patterns stay under a hundred characters, so 64 KiB leaves orders of
    // magnitude of headroom while bounding retained programs to single-digit
    // megabytes. Over-length shapes decline to legacy routing with identical
    // behavior; construction never fails on length.
    internal const int MaxPatternLength = 65536;

    private const RegexOptions RejectedOptions =
        RegexOptions.ECMAScript |
        RegexOptions.NonBacktracking;

    internal static Utf8SliceProgram? TryParse(string pattern, RegexOptions options)
    {
        if (pattern is null || pattern.Length > MaxPatternLength || (options & RejectedOptions) != 0)
            return null;
        return TryParse(pattern, options, Utf8OwnedTreeParser.TryParse(pattern, options));
    }

    internal static Utf8SliceProgram? TryParse(string pattern, RegexOptions options, Utf8OwnedRegexTree? tree)
    {
        if (pattern is null)
        {
            return null;
        }

        if (pattern.Length > MaxPatternLength)
        {
            return null;
        }

        if ((options & RejectedOptions) != 0)
        {
            return null;
        }

        if (tree is not null && Utf8OwnedTreeSliceLowerer.TryLower(tree, options) is { } ownedProgram)
            return ownedProgram;

        // Temporary compatibility parser for grammar the authoritative front
        // end still declines. It is removed as those remaining forms migrate.

        // IgnorePatternWhitespace strips insignificant pattern whitespace and
        // #-to-newline comments before parsing (escapes, classes, and (?#)
        // comments pass through verbatim). Only the ASCII insignificance set
        // matters: BCL skips
        // exactly space/tab/LF/FF/CR (vertical tab stays significant, probed).
        if ((options & RegexOptions.IgnorePatternWhitespace) != 0)
        {
            pattern = StripInsignificantPatternWhitespace(pattern);
        }

        var parser = new Parser(pattern, (options & RegexOptions.Singleline) != 0, (options & RegexOptions.Multiline) != 0, (options & RegexOptions.IgnoreCase) != 0, (options & RegexOptions.ExplicitCapture) != 0, (options & RegexOptions.IgnorePatternWhitespace) != 0);
        if (!parser.TryParseAlternation(0, out var root) || parser.Position != pattern.Length)
        {
            return null;
        }

        // .NET 10.0.12 compiled capture-writing finite lazy ranges can retain
        // abandoned progress and captures, producing phantom matches and even
        // non-progressing enumeration. Both owned lanes follow the stable
        // interpreted semantics: abandon every iteration and undo its captures.
        // Do not reproduce those diagnosed runtime defects through a fallback.

        // .NET 10.0.12 compiled greedy class loops ending at U+FFFF can
        // emit an invalid position. Preserve the interpreted pattern semantics
        // in our owned VM; deliberately do not reproduce those runtime faults.
        root ??= new Utf8SliceNode.EmptyNode();

        // Capture declarations use temporary opening-order slots. Shared numbering
        // folds duplicate names into one identity and binds references after parsing.
        static Utf8SliceNode ResolveNumeric(string digits, bool foldCase, Utf8SliceNode.RepeatNode? repeat,
            Utf8CaptureCatalog catalog, ref bool referencesValid)
        {
            var number = catalog.ReferenceNumber(digits);
            Utf8SliceNode atom;
            if (number > 0)
            {
                atom = new Utf8SliceNode.BackrefNode(catalog.OrdinalFromNumber(number) - 1, foldCase);
            }
            else
            {
                if (!Utf8NumericEscape.TryReadBareOctal(digits, out var value, out var consumed))
                {
                    referencesValid = false;
                    return new Utf8SliceNode.EmptyNode();
                }
                atom = new Utf8SliceNode.LiteralNode((ushort)value, foldCase);
                if (consumed < digits.Length)
                {
                    // Only the final replayed digit takes the escape's lexical
                    // quantifier. An outer group quantifier still covers the whole
                    // sequence because it reaches this binder through its body.
                    var children = new Utf8SliceNode[digits.Length - consumed + 1];
                    children[0] = atom;
                    for (var digit = consumed; digit < digits.Length; digit++)
                        children[digit - consumed + 1] = new Utf8SliceNode.LiteralNode(digits[digit], false);
                    if (repeat is not null)
                        children[^1] = new Utf8SliceNode.RepeatNode(children[^1], repeat.Minimum, repeat.Maximum, repeat.Lazy);
                    return new Utf8SliceNode.ConcatNode(children);
                }
            }
            return repeat is null ? atom : new Utf8SliceNode.RepeatNode(atom, repeat.Minimum, repeat.Maximum, repeat.Lazy);
        }

        static Utf8SliceNode RenumberCaptures(Utf8SliceNode node, int[] finals, Utf8CaptureCatalog catalog, ref bool referencesValid)
        {
            switch (node)
            {
                case Utf8SliceNode.UnresolvedNumericNode numeric:
                    return ResolveNumeric(numeric.Digits, numeric.FoldCase, null, catalog, ref referencesValid);
                case Utf8SliceNode.UnresolvedNumericRepeatNode numericRepeat:
                    return ResolveNumeric(numericRepeat.Digits, numericRepeat.FoldCase,
                        new Utf8SliceNode.RepeatNode(new Utf8SliceNode.EmptyNode(), numericRepeat.Minimum, numericRepeat.Maximum, numericRepeat.Lazy),
                        catalog, ref referencesValid);
                case Utf8SliceNode.UnresolvedBackrefNode reference:
                    var number = catalog.ReferenceNumber(reference.Name);
                    if (number < 0)
                    {
                        referencesValid = false;
                        return new Utf8SliceNode.EmptyNode();
                    }
                    // Group zero completes only after the pattern succeeds, so
                    // references to it always fail inside the pattern.
                    return number == 0 ? new Utf8SliceNode.FailNode()
                        : new Utf8SliceNode.BackrefNode(catalog.OrdinalFromNumber(number) - 1, reference.FoldCase);
                case Utf8SliceNode.UnresolvedBalanceNode balanceReference:
                    var popOrdinal = catalog.OrdinalFromNumber(catalog.ReferenceNumber(balanceReference.PopName));
                    if (popOrdinal < 0)
                    {
                        referencesValid = false;
                        return new Utf8SliceNode.EmptyNode();
                    }
                    var balanceBody = RenumberCaptures(balanceReference.Body, finals, catalog, ref referencesValid);
                    if (popOrdinal == 0) return new Utf8SliceNode.FailNode();
                    return new Utf8SliceNode.BalanceNode(popOrdinal - 1,
                        balanceReference.CaptureNumber > 0 ? catalog.OrdinalFromNumber(balanceReference.CaptureNumber) - 1
                            : balanceReference.CaptureSlot < 0 ? -1 : finals[balanceReference.CaptureSlot] - 1,
                        balanceBody);
                case Utf8SliceNode.UnresolvedConditionalNode conditionReference:
                    var testOrdinal = catalog.OrdinalFromNumber(catalog.ReferenceNumber(conditionReference.Number));
                    if (testOrdinal < 0) referencesValid = false;
                    return new Utf8SliceNode.ConditionalNode(testOrdinal - 1,
                        RenumberCaptures(conditionReference.YesBody, finals, catalog, ref referencesValid),
                        RenumberCaptures(conditionReference.NoBody, finals, catalog, ref referencesValid), null);
                case Utf8SliceNode.UnresolvedCaptureNode aliasCapture:
                    var captureOrdinal = catalog.OrdinalFromNumber(aliasCapture.Number);
                    if (captureOrdinal <= 0) referencesValid = false;
                    return new Utf8SliceNode.CaptureNode(captureOrdinal - 1,
                        RenumberCaptures(aliasCapture.Body, finals, catalog, ref referencesValid));
                case Utf8SliceNode.CaptureNode capture:
                    return new Utf8SliceNode.CaptureNode(
                        finals[capture.Slot] - 1,
                        RenumberCaptures(capture.Body, finals, catalog, ref referencesValid));
                case Utf8SliceNode.ConcatNode concat:
                    var children = new Utf8SliceNode[concat.Children.Length];
                    for (var child = 0; child < children.Length; child++)
                    {
                        children[child] = RenumberCaptures(concat.Children[child], finals, catalog, ref referencesValid);
                    }

                    return new Utf8SliceNode.ConcatNode(children);
                case Utf8SliceNode.AlternationNode alternation:
                    var branches = new Utf8SliceNode[alternation.Branches.Length][];
                    for (var branch = 0; branch < branches.Length; branch++)
                    {
                        var rewritten = new Utf8SliceNode[alternation.Branches[branch].Length];
                        for (var child = 0; child < rewritten.Length; child++)
                        {
                            rewritten[child] = RenumberCaptures(alternation.Branches[branch][child], finals, catalog, ref referencesValid);
                        }

                        branches[branch] = rewritten;
                    }

                    return new Utf8SliceNode.AlternationNode(branches);
                case Utf8SliceNode.RepeatNode repeat:
                    return new Utf8SliceNode.RepeatNode(
                        RenumberCaptures(repeat.Body, finals, catalog, ref referencesValid),
                        repeat.Minimum,
                        repeat.Maximum,
                        repeat.Lazy);
                case Utf8SliceNode.LookaheadNode lookahead:
                    return new Utf8SliceNode.LookaheadNode(
                        RenumberCaptures(lookahead.Body, finals, catalog, ref referencesValid),
                        lookahead.Positive);
                case Utf8SliceNode.LookbehindNode lookbehind:
                    return new Utf8SliceNode.LookbehindNode(
                        RenumberCaptures(lookbehind.Body, finals, catalog, ref referencesValid),
                        lookbehind.Positive);
                case Utf8SliceNode.AtomicNode atomic:
                    return new Utf8SliceNode.AtomicNode(
                        RenumberCaptures(atomic.Body, finals, catalog, ref referencesValid));
                case Utf8SliceNode.BalanceNode balance:
                    return new Utf8SliceNode.BalanceNode(
                        finals[balance.PopSlot] - 1,
                        balance.CaptureSlot < 0 ? -1 : finals[balance.CaptureSlot] - 1,
                        RenumberCaptures(balance.Body, finals, catalog, ref referencesValid));
                case Utf8SliceNode.ConditionalNode conditional:
                    // Named tests hold temporary declaration slots; numeric tests
                    // bind separately through the catalog. Group zero stays false.
                    var resolvedTest = conditional.TestSlot >= 0
                        ? finals[conditional.TestSlot] - 1
                        : -1;
                    return new Utf8SliceNode.ConditionalNode(
                        resolvedTest,
                        RenumberCaptures(conditional.YesBody, finals, catalog, ref referencesValid),
                        RenumberCaptures(conditional.NoBody, finals, catalog, ref referencesValid),
                        conditional.TestBody is null ? null : RenumberCaptures(conditional.TestBody, finals, catalog, ref referencesValid));
                default:
                    return node;
            }
        }

        // Every concatenation follows its containing execution direction.
        // Assertions establish their own direction even when nested: lookahead
        // runs forward and lookbehind runs backward in both main-stream modes.
        static Utf8SliceNode OrientConcatenations(Utf8SliceNode node, bool rightToLeft)
        {
            switch (node)
            {
                case Utf8SliceNode.ConcatNode concat:
                    var children = new Utf8SliceNode[concat.Children.Length];
                    for (var child = 0; child < children.Length; child++)
                    {
                        children[child] = OrientConcatenations(concat.Children[child], rightToLeft);
                    }

                    if (rightToLeft)
                    {
                        Array.Reverse(children);
                    }

                    return new Utf8SliceNode.ConcatNode(children);
                case Utf8SliceNode.AlternationNode alternation:
                    var branches = new Utf8SliceNode[alternation.Branches.Length][];
                    for (var branch = 0; branch < branches.Length; branch++)
                    {
                        var source = alternation.Branches[branch];
                        var rewritten = new Utf8SliceNode[source.Length];
                        for (var child = 0; child < rewritten.Length; child++)
                        {
                            rewritten[child] = OrientConcatenations(source[child], rightToLeft);
                        }

                        if (rightToLeft)
                        {
                            Array.Reverse(rewritten);
                        }

                        branches[branch] = rewritten;
                    }

                    return new Utf8SliceNode.AlternationNode(branches);
                case Utf8SliceNode.RepeatNode repeat:
                    return new Utf8SliceNode.RepeatNode(
                        OrientConcatenations(repeat.Body, rightToLeft),
                        repeat.Minimum,
                        repeat.Maximum,
                        repeat.Lazy);
                case Utf8SliceNode.CaptureNode capture:
                    return new Utf8SliceNode.CaptureNode(
                        capture.Slot,
                        OrientConcatenations(capture.Body, rightToLeft));
                case Utf8SliceNode.AtomicNode atomic:
                    return new Utf8SliceNode.AtomicNode(
                        OrientConcatenations(atomic.Body, rightToLeft));
                case Utf8SliceNode.ConditionalNode conditional:
                    return new Utf8SliceNode.ConditionalNode(
                        conditional.TestSlot,
                        OrientConcatenations(conditional.YesBody, rightToLeft),
                        OrientConcatenations(conditional.NoBody, rightToLeft),
                        conditional.TestBody is null ? null : OrientConcatenations(conditional.TestBody, false));
                case Utf8SliceNode.BalanceNode balance:
                    return new Utf8SliceNode.BalanceNode(
                        balance.PopSlot,
                        balance.CaptureSlot,
                        OrientConcatenations(balance.Body, rightToLeft));
                case Utf8SliceNode.LookaheadNode lookahead:
                    return new Utf8SliceNode.LookaheadNode(
                        OrientConcatenations(lookahead.Body, false),
                        lookahead.Positive);
                case Utf8SliceNode.LookbehindNode lookbehind:
                    return new Utf8SliceNode.LookbehindNode(
                        OrientConcatenations(lookbehind.Body, true),
                        lookbehind.Positive);
                default:
                    return node;
            }
        }

        var finals = new int[parser.GroupCount];
        var definitions = new Utf8CaptureDefinition[parser.GroupCount];
        var unnamed = 0;
        foreach (var declaration in parser.GroupDecls)
            definitions[declaration.Slot] = declaration.Name is null
                ? new Utf8CaptureDefinition(string.Empty, ++unnamed, false, declaration.Slot)
                : Utf8CaptureCatalog.TryParseNumber(declaration.Name, out var declared)
                    ? new Utf8CaptureDefinition(declaration.Name, declared, false, declaration.Slot)
                    : new Utf8CaptureDefinition(declaration.Name, -1, true, declaration.Slot);
        var catalog = new Utf8CaptureCatalog(0, definitions, parser.RequiredNumbers.ToArray());
        for (var declaration = 0; declaration < finals.Length; declaration++)
            finals[declaration] = catalog.OrdinalFromNumber(catalog.DefinitionNumbers[declaration]);
        foreach (var required in parser.RequiredNumbers)
            if (catalog.OrdinalFromNumber(required) < 0) return null;
        var referencesValid = true;
        root = RenumberCaptures(root, finals, catalog, ref referencesValid);
        if (!referencesValid) return null;

        var rightToLeft = (options & RegexOptions.RightToLeft) != 0;
        root = OrientConcatenations(root, rightToLeft);

        return Utf8SliceLowerer.Lower(root, catalog.GroupNames, parser.SawBalancing,
            parser.MatchIgnoreCase || parser.SawFold, rightToLeft, catalog,
            (options & RegexOptions.Compiled) != 0 && parser.SawCaptureWritingLazyRange,
            parser.SawLiteralClassBracketSyntax);
    }

    // Hex, unicode, and octal escapes produce one UTF-16 unit (exactly two
    // or four hex digits, or at most three octal digits masked to a byte).
    // Malformed escapes decline. Shared by literal and class positions (two
    // callers, so the single-caller debt ceiling does not apply).
    private static bool TryParseControlEscape(string pattern, int start, out int value, out int next)
    {
        value = 0;
        next = start;
        if (start + 1 >= pattern.Length) return false;
        var letter = pattern[start + 1];
        if (letter is >= 'a' and <= 'z') letter = (char)(letter - ('a' - 'A'));
        if (letter is < '@' or > '_') return false;
        value = letter - '@';
        next = start + 2;
        return true;
    }

    private static bool TryParseNumericEscape(string pattern, int start, bool bareOctal, out int value, out int next)
    {
        static int HexValue(char digit)
        {
            return digit <= '9' ? digit - '0' : (digit & ~32) - 'A' + 10;
        }

        value = 0;
        next = start;
        if (start >= pattern.Length)
        {
            return false;
        }

        var marker = pattern[start];
        if (marker == 'x')
        {
            if (start + 2 >= pattern.Length || !char.IsAsciiHexDigit(pattern[start + 1]) || !char.IsAsciiHexDigit(pattern[start + 2]))
            {
                return false;
            }

            value = HexValue(pattern[start + 1]) * 16 + HexValue(pattern[start + 2]);
            next = start + 3;
            return true;
        }

        if (marker == 'u')
        {
            if (start + 4 >= pattern.Length)
            {
                return false;
            }

            value = 0;
            for (var digit = 1; digit <= 4; digit++)
            {
                if (!char.IsAsciiHexDigit(pattern[start + digit]))
                {
                    return false;
                }

                value = value * 16 + HexValue(pattern[start + digit]);
            }

            next = start + 5;
            return true;
        }

        if (marker >= '0' && marker <= '7' && (bareOctal || marker == '0'))
        {
            var parsed = Utf8NumericEscape.TryReadOctal(pattern.AsSpan(start), out value, out var consumed);
            next = start + consumed;
            return parsed;
        }

        return false;
    }

    // Unicode property escapes (BCL ParseProperty shapes: braces required,
    // word-char/dash names, unknown names decline). General categories and
    // groups resolve to masks, Is*/_xml* blocks to half-open range strings;
    // blocks carry the fold bit under IgnoreCase and match through the invariant
    // case-equivalence table at run time (candidate-side lookup, no parse
    // expansion). Shared by bare atoms and single-category classes (two
    // callers, so the single-caller debt ceiling does not apply).
    private static bool TryParseCategoryAtom(string pattern, int backslashIndex, bool negated, bool foldCase, out Utf8SliceNode? node, out int nextIndex)
    {
        node = null;
        nextIndex = backslashIndex;
        if (backslashIndex + 3 >= pattern.Length || pattern[backslashIndex] != '\\' ||
            (pattern[backslashIndex + 1] != 'p' && pattern[backslashIndex + 1] != 'P') ||
            pattern[backslashIndex + 2] != '{')
        {
            return false;
        }

        var nameStart = backslashIndex + 3;
        var nameEnd = nameStart;
        while (nameEnd < pattern.Length && (char.IsAsciiLetterOrDigit(pattern[nameEnd]) || pattern[nameEnd] == '_' || pattern[nameEnd] == '-'))
        {
            nameEnd++;
        }

        if (nameEnd == nameStart || nameEnd >= pattern.Length || pattern[nameEnd] != '}' ||
            !Utf8SliceCategories.TryResolve(pattern.Substring(nameStart, nameEnd - nameStart), out var mask, out var ranges, out _))
        {
            return false;
        }

        node = new Utf8SliceNode.CategoryNode(mask, ranges, negated, foldCase);
        nextIndex = nameEnd + 1;
        return true;
    }


    static string StripInsignificantPatternWhitespace(string source)
    {
        var filtered = new StringBuilder(source.Length);
        var index = 0;
        while (index < source.Length)
        {
            var current = source[index];
            if (current == '\\' && index + 1 < source.Length)
            {
                if (source[index + 1] == 'k' && index + 2 < source.Length && source[index + 2] is '<' or '\'')
                {
                    var referenceEnd = source.IndexOf(source[index + 2] == '<' ? '>' : '\'', index + 3);
                    var spanEnd = referenceEnd < 0 ? source.Length : referenceEnd + 1;
                    filtered.Append(source, index, spanEnd - index);
                    index = spanEnd;
                    continue;
                }
                // Unicode property spans lex verbatim: BCL reads the property
                // name raw (a space like \p{Is Greek} throws), so stripping
                // inside the braces would invent an accepted name. Classes
                // already pass through verbatim below.
                if ((source[index + 1] == 'p' || source[index + 1] == 'P') &&
                    index + 2 < source.Length && source[index + 2] == '{')
                {
                    var propertyEnd = source.IndexOf('}', index + 3);
                    var spanEnd = propertyEnd < 0 ? source.Length : propertyEnd + 1;
                    filtered.Append(source, index, spanEnd - index);
                    index = spanEnd;
                }
                else
                {
                    filtered.Append(current).Append(source[index + 1]);
                    index += 2;
                }
            }
            else if (current == '(' && index + 3 < source.Length && source[index + 1] == '?' &&
                (source[index + 2] == '\'' || source[index + 2] == '<' && source[index + 3] is not '=' and not '!'))
            {
                // Keep names raw while the temporary compatibility parser is
                // still needed: removing spaces could invent a valid declaration.
                var nameEnd = source.IndexOf(source[index + 2] == '<' ? '>' : '\'', index + 3);
                var spanEnd = nameEnd < 0 ? source.Length : nameEnd + 1;
                filtered.Append(source, index, spanEnd - index);
                index = spanEnd;
            }
            else if (current == '(' && index + 2 < source.Length &&
                source[index + 1] == '?' && source[index + 2] == '#')
            {
                var commentEnd = source.IndexOf(')', index + 3);
                if (commentEnd < 0)
                {
                    filtered.Append(source, index, source.Length - index);
                    break;
                }

                filtered.Append(source, index, commentEnd - index + 1);
                index = commentEnd + 1;
            }
            else if (current == '[')
            {
                filtered.Append(current);
                index++;
                if (index < source.Length && source[index] == ']')
                {
                    filtered.Append(']');
                    index++;
                }

                while (index < source.Length && source[index] != ']')
                {
                    if (source[index] == '\\' && index + 1 < source.Length)
                    {
                        filtered.Append(source[index]).Append(source[index + 1]);
                        index += 2;
                    }
                    else
                    {
                        filtered.Append(source[index]);
                        index++;
                    }
                }

                if (index < source.Length)
                {
                    filtered.Append(']');
                    index++;
                }
            }
            else if (current == '#')
            {
                while (index < source.Length && source[index] != '\n')
                {
                    index++;
                }
            }
            else if (current == ' ' || current == '\t' || current == '\n' || current == '\f' || current == '\r')
            {
                index++;
            }
            else
            {
                filtered.Append(current);
                index++;
            }
        }

        return filtered.ToString();
    }

    private sealed class Parser
    {
        private readonly string _pattern;
        private bool _matchNewline;
        private bool _matchMultiline;
        private bool _matchIgnoreCase;
        private bool _explicitCapture;
        private bool _sawFold;
        private int _expressionBranchDepth = -1;
        private bool _whitespaceStripped;
        private int _position;
        private int _groups;
        private readonly List<(int Slot, string? Name)> _groupDecls = new();
        private readonly List<int> _requiredNumbers = new();
        private readonly Dictionary<string, int> _groupSlots = new(StringComparer.Ordinal);
        private bool _sawBalancing;
        private bool _sawCaptureWritingLazyRange;
        private bool _sawLiteralClassBracketSyntax;

        internal Parser(string pattern, bool matchNewline, bool matchMultiline, bool matchIgnoreCase, bool explicitCapture, bool whitespaceStripped)
        {
            _pattern = pattern;
            _matchNewline = matchNewline;
            _matchMultiline = matchMultiline;
            _matchIgnoreCase = matchIgnoreCase;
            _explicitCapture = explicitCapture;
            _whitespaceStripped = whitespaceStripped;
        }

        internal int Position => _position;
        internal int GroupCount => _groups;
        internal List<(int Slot, string? Name)> GroupDecls => _groupDecls;
        internal List<int> RequiredNumbers => _requiredNumbers;
        internal bool SawBalancing => _sawBalancing;
        internal bool MatchIgnoreCase => _matchIgnoreCase;
        internal bool SawFold => _sawFold;
        internal bool SawCaptureWritingLazyRange => _sawCaptureWritingLazyRange;
        internal bool SawLiteralClassBracketSyntax => _sawLiteralClassBracketSyntax;

        internal bool TryParseAlternation(int depth, out Utf8SliceNode? root)
        {
            var branches = new List<List<Utf8SliceNode>> { new() };
            while (true)
            {
                if (!TryParseConcatenation(depth, branches[^1]))
                {
                    root = null;
                    return false;
                }

                if (AtEnd || _pattern[_position] != '|')
                {
                    break;
                }

                _position++;
                branches.Add(new List<Utf8SliceNode>());
            }

            root = branches.Count == 1 && branches[0].Count == 1
                ? branches[0][0]
                : new Utf8SliceNode.AlternationNode(branches.Select(static branch => branch.ToArray()).ToArray());
            return true;
        }

        private bool TryParseConcatenation(int depth, List<Utf8SliceNode> children)
        {
            while (!AtEnd && _pattern[_position] != '|' && _pattern[_position] != ')')
            {
                if (!TryParseRepeat(depth, out var child) || child is null)
                {
                    return false;
                }

                children.Add(child);
            }

            return true;
        }

        // True when the lazy-range body can leak abandonment progress on the
        // BCL-compiled lane: only capture-writing subtrees (captures and
        // balancing groups, anywhere inside including nested loops) exhibit the
        // divergence or the enumeration oscillation. Reads (backreferences),
        // lookarounds, atomics, and conditionals pass through transparently;
        // nested captures inside them still require complete interpreter unwind.
        private static bool RepeatBodyWritesCaptures(Utf8SliceNode node)
        {
            switch (node)
            {
                case Utf8SliceNode.CaptureNode:
                case Utf8SliceNode.UnresolvedCaptureNode:
                case Utf8SliceNode.BalanceNode:
                case Utf8SliceNode.UnresolvedBalanceNode:
                    return true;
                case Utf8SliceNode.ConcatNode concat:
                    foreach (var child in concat.Children)
                    {
                        if (RepeatBodyWritesCaptures(child))
                        {
                            return true;
                        }
                    }

                    return false;
                case Utf8SliceNode.AlternationNode alternation:
                    foreach (var branch in alternation.Branches)
                    {
                        foreach (var member in branch)
                        {
                            if (RepeatBodyWritesCaptures(member))
                            {
                                return true;
                            }
                        }
                    }

                    return false;
                case Utf8SliceNode.RepeatNode repeat:
                    return RepeatBodyWritesCaptures(repeat.Body);
                case Utf8SliceNode.LookaheadNode lookahead:
                    return RepeatBodyWritesCaptures(lookahead.Body);
                case Utf8SliceNode.LookbehindNode lookbehind:
                    return RepeatBodyWritesCaptures(lookbehind.Body);
                case Utf8SliceNode.AtomicNode atomic:
                    return RepeatBodyWritesCaptures(atomic.Body);
                case Utf8SliceNode.UnresolvedConditionalNode conditionalReference:
                    return RepeatBodyWritesCaptures(conditionalReference.YesBody) || RepeatBodyWritesCaptures(conditionalReference.NoBody);
                case Utf8SliceNode.ConditionalNode conditional:
                    return RepeatBodyWritesCaptures(conditional.YesBody) ||
                        RepeatBodyWritesCaptures(conditional.NoBody) ||
                        (conditional.TestBody is not null && RepeatBodyWritesCaptures(conditional.TestBody));
                default:
                    return false;
            }
        }

        private bool TryParseRepeat(int depth, out Utf8SliceNode? node)
        {
            node = null;
            var atomStart = _position;
            if (!TryParseAtom(depth, out var atom) || atom is null)
            {
                return false;
            }

            if (AtEnd)
            {
                node = atom;
                return true;
            }

            var minimum = 1;
            var maximum = 1;
            var quantified = true;
            switch (_pattern[_position])
            {
                case '*':
                    minimum = 0;
                    maximum = int.MaxValue;
                    _position++;
                    break;
                case '+':
                    minimum = 1;
                    maximum = int.MaxValue;
                    _position++;
                    break;
                case '?':
                    minimum = 0;
                    maximum = 1;
                    _position++;
                    break;
                case '{':
                    if (!TryParseBraceBounds(out minimum, out maximum))
                    {
                        node = atom;
                        return true;
                    }

                    break;
                default:
                    quantified = false;
                    break;
            }

            if (!quantified)
            {
                node = atom;
                return true;
            }

            var lazy = false;
            if (!AtEnd && _pattern[_position] == '?')
            {
                lazy = true;
                _position++;
            }

            // Only true ranges (maximum above both one and the minimum) whose
            // body writes captures can exhibit the per-lane abandonment
            // divergence or the enumeration oscillation; exact counts, single
            // maxima, and capture-free ranges are probe-proven lane-identical.
            if (lazy && maximum != int.MaxValue && maximum > 1 && maximum > minimum && RepeatBodyWritesCaptures(atom))
            {
                _sawCaptureWritingLazyRange = true;
            }

            node = atom is Utf8SliceNode.UnresolvedNumericNode numeric && _pattern[atomStart] == '\\'
                ? new Utf8SliceNode.UnresolvedNumericRepeatNode(numeric.Digits, numeric.FoldCase, minimum, maximum, lazy)
                : new Utf8SliceNode.RepeatNode(atom, minimum, maximum, lazy);
            return true;
        }

        private bool TryParseBraceBounds(out int minimum, out int maximum)
        {
            minimum = 0;
            maximum = 0;
            var start = _position;
            _position++;
            if (!TryParseDigits(out minimum))
            {
                _position = start;
                return false;
            }

            maximum = minimum;
            if (!AtEnd && _pattern[_position] == ',')
            {
                _position++;
                if (!AtEnd && _pattern[_position] == '}')
                {
                    maximum = int.MaxValue;
                }
                else if (!TryParseDigits(out maximum))
                {
                    _position = start;
                    return false;
                }
            }

            if (AtEnd || _pattern[_position] != '}' || maximum < minimum)
            {
                _position = start;
                return false;
            }

            _position++;
            return true;
        }

        private bool TryParseDigits(out int value)
        {
            value = 0;
            var start = _position;
            while (!AtEnd && _pattern[_position] >= '0' && _pattern[_position] <= '9')
            {
                var next = (long)value * 10 + (_pattern[_position] - '0');
                if (next > int.MaxValue)
                {
                    _position = start;
                    value = 0;
                    return false;
                }

                value = (int)next;
                _position++;
            }

            return _position != start;
        }

        private bool TryParseAtom(int depth, out Utf8SliceNode? node)
        {
            node = null;
            if (AtEnd)
            {
                return false;
            }

            var character = _pattern[_position];
            switch (character)
            {
                case '(':
                    if (depth >= MaxNestingDepth)
                    {
                        return false;
                    }

                    // Zero-width lookarounds bind their bodies without
                    // consuming a group number; every other (? form keeps
                    // declining through the capture path below.
                    if (_position + 2 < _pattern.Length && _pattern[_position + 1] == '?')
                    {
                        var marker = _pattern[_position + 2];
                        if (marker == '=' || marker == '!')
                        {
                            var positive = marker == '=';
                            _position += 3;
                            var savedLookahead = (_matchNewline, _matchMultiline, _matchIgnoreCase, _explicitCapture);
                            if (!TryParseAlternation(depth + 1, out var lookahead) || AtEnd || _pattern[_position] != ')')
                            {
                                return false;
                            }

                            _position++;
                            (_matchNewline, _matchMultiline, _matchIgnoreCase, _explicitCapture) = savedLookahead;
                            node = new Utf8SliceNode.LookaheadNode(lookahead ?? new Utf8SliceNode.EmptyNode(), positive);
                            return true;
                        }

                        if (marker == '<' && _position + 3 < _pattern.Length &&
                            (_pattern[_position + 3] == '=' || _pattern[_position + 3] == '!'))
                        {
                            var behindPositive = _pattern[_position + 3] == '=';
                            _position += 4;
                            var savedBehind = (_matchNewline, _matchMultiline, _matchIgnoreCase, _explicitCapture);
                            var parsedBehind = TryParseAlternation(depth + 1, out var lookbehind);
                            if (!parsedBehind || AtEnd || _pattern[_position] != ')')
                            {
                                return false;
                            }

                            _position++;
                            (_matchNewline, _matchMultiline, _matchIgnoreCase, _explicitCapture) = savedBehind;
                            node = new Utf8SliceNode.LookbehindNode(lookbehind ?? new Utf8SliceNode.EmptyNode(), behindPositive);
                            return true;
                        }

                        if (marker == 'i' || marker == 'I' || marker == 'm' || marker == 'M' ||
                            marker == 'n' || marker == 'N' || marker == 's' || marker == 'S' ||
                            marker == 'x' || marker == 'X' || marker == '-' || marker == '+')
                        {
                            // Inline options (BCL ScanOptions): letters apply case-insensitively
                            // with -/+ prefixes; an unknown letter stops the scan and anything
                            // but ) or : declines. Option constructs directly inside
                            // expression-conditional branches decline (BCL throws there). x-forms
                            // decline unless they only switch whitespace off with global
                            // stripping already off (regional lexing lives in legacy).
                            if (_expressionBranchDepth == depth)
                            {
                                return false;
                            }

                            var scan = _position + 2;
                            var off = false;
                            var nextIgnoreCase = _matchIgnoreCase;
                            var nextMultiline = _matchMultiline;
                            var nextNewline = _matchNewline;
                            var nextExplicit = _explicitCapture;
                            var sawX = false;
                            var xValue = false;
                            while (scan < _pattern.Length)
                            {
                                var optionChar = _pattern[scan];
                                if (optionChar == '-')
                                {
                                    off = true;
                                    scan++;
                                }
                                else if (optionChar == '+')
                                {
                                    off = false;
                                    scan++;
                                }
                                else if ((optionChar | 0x20) == 'i')
                                {
                                    nextIgnoreCase = !off;
                                    scan++;
                                }
                                else if ((optionChar | 0x20) == 'm')
                                {
                                    nextMultiline = !off;
                                    scan++;
                                }
                                else if ((optionChar | 0x20) == 'n')
                                {
                                    nextExplicit = !off;
                                    scan++;
                                }
                                else if ((optionChar | 0x20) == 's')
                                {
                                    nextNewline = !off;
                                    scan++;
                                }
                                else if ((optionChar | 0x20) == 'x')
                                {
                                    sawX = true;
                                    xValue = !off;
                                    scan++;
                                }
                                else
                                {
                                    break;
                                }
                            }

                            if (scan >= _pattern.Length || (_pattern[scan] != ')' && _pattern[scan] != ':'))
                            {
                                return false;
                            }

                            if (sawX && (_whitespaceStripped || xValue))
                            {
                                return false;
                            }

                            if (_pattern[scan] == ')')
                            {
                                _position = scan + 1;
                                if (!AtEnd && (_pattern[_position] == '*' || _pattern[_position] == '+' || _pattern[_position] == '?' || _pattern[_position] == '{'))
                                {
                                    return false;
                                }

                                _matchIgnoreCase = nextIgnoreCase;
                                _matchMultiline = nextMultiline;
                                _matchNewline = nextNewline;
                                _explicitCapture = nextExplicit;
                                node = new Utf8SliceNode.EmptyNode();
                                return true;
                            }

                            var savedOptions = (_matchNewline, _matchMultiline, _matchIgnoreCase, _explicitCapture);
                            _position = scan + 1;
                            _matchIgnoreCase = nextIgnoreCase;
                            _matchMultiline = nextMultiline;
                            _matchNewline = nextNewline;
                            _explicitCapture = nextExplicit;
                            if (!TryParseAlternation(depth + 1, out var scopedBody) || AtEnd || _pattern[_position] != ')')
                            {
                                return false;
                            }

                            _position++;
                            (_matchNewline, _matchMultiline, _matchIgnoreCase, _explicitCapture) = savedOptions;
                            node = scopedBody ?? new Utf8SliceNode.EmptyNode();
                            return true;
                        }

                        if (marker == '>')
                        {
                            // Atomic (possessive) groups bind their bodies without
                            // consuming a group number.
                            _position += 3;
                            var savedAtomic = (_matchNewline, _matchMultiline, _matchIgnoreCase, _explicitCapture);
                            if (!TryParseAlternation(depth + 1, out var atomicBody) || AtEnd || _pattern[_position] != ')')
                            {
                                return false;
                            }

                            _position++;
                            (_matchNewline, _matchMultiline, _matchIgnoreCase, _explicitCapture) = savedAtomic;
                            node = new Utf8SliceNode.AtomicNode(atomicBody ?? new Utf8SliceNode.EmptyNode());
                            return true;
                        }

                        if (marker == ':')
                        {
                            // Non-capturing groups are transparent: the body
                            // lowers as-is with no slot and no group number.
                            _position += 3;
                            var savedTransparent = (_matchNewline, _matchMultiline, _matchIgnoreCase, _explicitCapture);
                            if (!TryParseAlternation(depth + 1, out var plainBody) || AtEnd || _pattern[_position] != ')')
                            {
                                return false;
                            }

                            _position++;
                            (_matchNewline, _matchMultiline, _matchIgnoreCase, _explicitCapture) = savedTransparent;
                            node = plainBody ?? new Utf8SliceNode.EmptyNode();
                            return true;
                        }

                        if (marker == '(')
                        {
                            // Conditional alternation (?(test)yes|no): number tests bind final
                            // group numbers (validated post-parse since forward references read
                            // groups defined anywhere), backward-defined names bind template slots,
                            // and every other test parses as an expression subpattern that desugars
                            // to a lookahead-guarded alternation. A second | declines (BCL throws
                            // AlternationHasTooManyConditions); a named capture or comment opening
                            // the test declines (BCL throws).
                            var savedConditional = (_matchNewline, _matchMultiline, _matchIgnoreCase, _explicitCapture);
                            var testStart = _position + 3;
                            var testChar = testStart < _pattern.Length ? _pattern[testStart] : '\0';
                            int testSlot = int.MinValue;
                            string? testNumber = null;
                            Utf8SliceNode? testBody = null;
                            if (testChar >= '0' && testChar <= '9')
                            {
                                var testEnd = testStart;
                                var rawNumber = 0L;
                                while (testEnd < _pattern.Length && _pattern[testEnd] >= '0' && _pattern[testEnd] <= '9')
                                {
                                    rawNumber = rawNumber * 10 + (_pattern[testEnd] - '0');
                                    if (rawNumber > int.MaxValue)
                                    {
                                        return false;
                                    }
                                    testEnd++;
                                }
                                if (testEnd >= _pattern.Length || _pattern[testEnd] != ')')
                                {
                                    return false;
                                }
                                testNumber = _pattern.Substring(testStart, testEnd - testStart);
                                _position = testEnd + 1;
                            }
                            else
                            {
                                var nameEnd = testStart;
                                while (nameEnd < _pattern.Length && (( _pattern[nameEnd] >= '0' && _pattern[nameEnd] <= '9') || (_pattern[nameEnd] >= 'A' && _pattern[nameEnd] <= 'Z') || _pattern[nameEnd] == '_' || (_pattern[nameEnd] >= 'a' && _pattern[nameEnd] <= 'z')))
                                {
                                    nameEnd++;
                                }
                                if (nameEnd > testStart && nameEnd < _pattern.Length && _pattern[nameEnd] == ')' && _groupSlots.TryGetValue(_pattern.Substring(testStart, nameEnd - testStart), out var testTemp))
                                {
                                    testSlot = testTemp;
                                    _position = nameEnd + 1;
                                }
                                else
                                {
                                    // The expression test starts at its own paren. A (?-form parses
                                    // as one atom (those forms never capture, so no phantom group
                                    // arises); a named capture or comment opener declines exactly
                                    // like the BCL construction throw. A plain group parses with its
                                    // parens stripped (BCL ignores the test paren while inner groups
                                    // bind normally).
                                    var testParen = _position + 2;
                                    if (testParen + 1 < _pattern.Length && _pattern[testParen + 1] == '?')
                                    {
                                        if (testParen + 2 < _pattern.Length)
                                        {
                                            var testMarker = _pattern[testParen + 2];
                                            if (testMarker == '#'
                                                || testMarker == '\''
                                                || (testMarker == '<' && testParen + 3 < _pattern.Length && _pattern[testParen + 3] != '=' && _pattern[testParen + 3] != '!'))
                                            {
                                                return false;
                                            }
                                        }

                                        _position = testParen;
                                        if (!TryParseAtom(depth + 1, out testBody) || testBody is null)
                                        {
                                            return false;
                                        }
                                    }
                                    else
                                    {
                                        var innerStart = testParen + 1;
                                        if (innerStart < _pattern.Length && _pattern[innerStart] == ')')
                                        {
                                            testBody = new Utf8SliceNode.EmptyNode();
                                            _position = innerStart + 1;
                                        }
                                        else
                                        {
                                            _position = innerStart;
                                            if (!TryParseAlternation(depth + 1, out testBody) || testBody is null || AtEnd || _pattern[_position] != ')')
                                            {
                                                return false;
                                            }

                                            _position++;
                                        }
                                    }
                                }
                            }
                            var savedExprDepth = _expressionBranchDepth;
                            if (testBody is not null)
                            {
                                _expressionBranchDepth = depth + 1;
                            }

                            var yesChildren = new List<Utf8SliceNode>();
                            if (!TryParseConcatenation(depth + 1, yesChildren))
                            {
                                return false;
                            }
                            var yesBranch = yesChildren.Count == 0 ? (Utf8SliceNode)new Utf8SliceNode.EmptyNode() : yesChildren.Count == 1 ? yesChildren[0] : new Utf8SliceNode.AlternationNode(new[] { yesChildren.ToArray() });
                            var noBranch = (Utf8SliceNode)new Utf8SliceNode.EmptyNode();
                            if (!AtEnd && _pattern[_position] == '|')
                            {
                                _position++;
                                var noChildren = new List<Utf8SliceNode>();
                                if (!TryParseConcatenation(depth + 1, noChildren))
                                {
                                    return false;
                                }
                                noBranch = noChildren.Count == 0 ? (Utf8SliceNode)new Utf8SliceNode.EmptyNode() : noChildren.Count == 1 ? noChildren[0] : new Utf8SliceNode.AlternationNode(new[] { noChildren.ToArray() });
                                if (!AtEnd && _pattern[_position] == '|')
                                {
                                    return false;
                                }
                            }
                            if (AtEnd || _pattern[_position] != ')')
                            {
                                return false;
                            }
                            _position++;
                            (_matchNewline, _matchMultiline, _matchIgnoreCase, _explicitCapture) = savedConditional;
                            _expressionBranchDepth = savedExprDepth;
                            if (testBody is not null)
                            {
                                // Expression tests stay on the node (desugared at lowering so
                                // consumers can distinguish assertion capture flow); group tests
                                // carry a null test body.
                                node = new Utf8SliceNode.ConditionalNode(-1, yesBranch, noBranch, testBody);
                            }
                            else
                            {
                                node = testNumber is null
                                    ? new Utf8SliceNode.ConditionalNode(testSlot, yesBranch, noBranch, null)
                                    : new Utf8SliceNode.UnresolvedConditionalNode(testNumber, yesBranch, noBranch);
                            }
                            return true;
                        }

                        if (marker == '#')
                        {
                            // Inline comments run to the next closing paren;
                            // an unterminated comment declines so legacy
                            // routing preserves the construction failure.
                            var commentEnd = _pattern.IndexOf(')', _position + 3);
                            if (commentEnd < 0)
                            {
                                return false;
                            }

                            _position = commentEnd + 1;
                            if (!AtEnd && (_pattern[_position] == '*' || _pattern[_position] == '+' || _pattern[_position] == '?' || _pattern[_position] == '{'))
                            {
                                return false;
                            }

                            node = new Utf8SliceNode.EmptyNode();
                            return true;
                        }

                        if (marker == '<' || marker == '\'')
                        {
                            // Numbered and named declarations share final capture identities.
                            // Zero-padded numbers alias an existing declaration; balancing
                            // operands bind after the complete pattern has been parsed.
                            var closer = marker == '<' ? '>' : '\'';
                            var scan = _position + 3;
                            var nameScanEnd = scan;
                            while (nameScanEnd < _pattern.Length)
                            {
                                var scanChar = _pattern[nameScanEnd];
                                if ((scanChar >= 'a' && scanChar <= 'z') ||
                                    (scanChar >= 'A' && scanChar <= 'Z') ||
                                    (scanChar >= '0' && scanChar <= '9') ||
                                    scanChar == '_')
                                {
                                    nameScanEnd++;
                                }
                                else
                                {
                                    break;
                                }
                            }

                            if ((nameScanEnd > scan || (scan < _pattern.Length && _pattern[scan] == '-')) &&
                                nameScanEnd < _pattern.Length &&
                                _pattern[nameScanEnd] == '-')
                            {
                                // Pop a final named or numeric identity, optionally capturing
                                // the interval into another identity. The body runs after pop.
                                string? captureName = nameScanEnd > scan
                                    ? _pattern.Substring(scan, nameScanEnd - scan)
                                    : null;
                                if (captureName is not null && char.IsAsciiDigit(captureName[0]) &&
                                    (!Utf8CaptureCatalog.TryParseNumber(captureName, out var captureNumber) || captureNumber == 0)) return false;

                                var popStart = nameScanEnd + 1;
                                var popEnd = popStart;
                                while (popEnd < _pattern.Length)
                                {
                                    var popChar = _pattern[popEnd];
                                    if ((popChar >= 'a' && popChar <= 'z') ||
                                        (popChar >= 'A' && popChar <= 'Z') ||
                                        (popChar >= '0' && popChar <= '9') ||
                                        popChar == '_')
                                    {
                                        popEnd++;
                                    }
                                    else
                                    {
                                        break;
                                    }
                                }

                                if (popEnd == popStart ||
                                    popEnd >= _pattern.Length ||
                                    _pattern[popEnd] != closer)
                                {
                                    return false;
                                }

                                var popName = _pattern.Substring(popStart, popEnd - popStart);
                                var captureTemp = -1;
                                var captureAlias = 0;
                                if (captureName is not null)
                                {
                                    if (captureName.Length > 1 && captureName[0] == '0' && Utf8CaptureCatalog.TryParseNumber(captureName, out captureAlias))
                                        _requiredNumbers.Add(captureAlias);
                                    else
                                    {
                                        captureTemp = _groups++;
                                        _groupSlots.TryAdd(captureName, captureTemp);
                                        _groupDecls.Add((captureTemp, captureName));
                                    }
                                }

                                _position = popEnd + 1;
                                var savedBalance = (_matchNewline, _matchMultiline, _matchIgnoreCase, _explicitCapture);
                                if (!TryParseAlternation(depth + 1, out var balanceBody) || AtEnd || _pattern[_position] != ')')
                                {
                                    return false;
                                }

                                _position++;
                                (_matchNewline, _matchMultiline, _matchIgnoreCase, _explicitCapture) = savedBalance;
                                node = new Utf8SliceNode.UnresolvedBalanceNode(popName, captureTemp, captureAlias, balanceBody ?? new Utf8SliceNode.EmptyNode());
                                _sawBalancing = true;
                                return true;
                            }

                            var nameStart = _position + 3;
                            var nameEnd = nameStart;
                            while (nameEnd < _pattern.Length)
                            {
                                var nameChar = _pattern[nameEnd];
                                if (Utf8OwnedCharClass.IsWordChar(nameChar))
                                {
                                    nameEnd++;
                                }
                                else
                                {
                                    break;
                                }
                            }

                            if (nameEnd == nameStart ||
                                nameEnd >= _pattern.Length ||
                                _pattern[nameEnd] != closer)
                            {
                                return false;
                            }

                            var name = _pattern.Substring(nameStart, nameEnd - nameStart);
                            if (char.IsAsciiDigit(name[0]))
                            {
                                if (!Utf8CaptureCatalog.TryParseNumber(name, out var declared) || declared == 0) return false;
                                if (name.Length > 1 && name[0] == '0')
                                {
                                    // This spelling captures into an existing number without
                                    // declaring it; bind the alias after all declarations.
                                    _requiredNumbers.Add(declared);
                                    _position = nameEnd + 1;
                                    var savedNumeric = (_matchNewline, _matchMultiline, _matchIgnoreCase, _explicitCapture);
                                    if (!TryParseAlternation(depth + 1, out var numericBody) || AtEnd || _pattern[_position] != ')') return false;
                                    _position++;
                                    (_matchNewline, _matchMultiline, _matchIgnoreCase, _explicitCapture) = savedNumeric;
                                    node = new Utf8SliceNode.UnresolvedCaptureNode(declared, numericBody ?? new Utf8SliceNode.EmptyNode());
                                    return true;
                                }
                            }
                            _position = nameEnd + 1;
                            var namedSlot = _groups++;
                            _groupSlots.TryAdd(name, namedSlot);

                            _groupDecls.Add((namedSlot, name));
                            var savedNamed = (_matchNewline, _matchMultiline, _matchIgnoreCase, _explicitCapture);
                            if (!TryParseAlternation(depth + 1, out var namedBody) || AtEnd || _pattern[_position] != ')')
                            {
                                return false;
                            }

                            _position++;
                            (_matchNewline, _matchMultiline, _matchIgnoreCase, _explicitCapture) = savedNamed;
                            node = new Utf8SliceNode.CaptureNode(namedSlot, namedBody ?? new Utf8SliceNode.EmptyNode());
                            return true;
                        }
                    }

                    _position++;
                    if (_explicitCapture)
                    {
                        // ExplicitCapture leaves unnamed groups unnumbered, exactly
                        // like non-capturing groups (backreference digits still denote
                        // final numbers, so unbound digits decline as usual).
                        var savedExplicitBody = (_matchNewline, _matchMultiline, _matchIgnoreCase, _explicitCapture);
                        if (!TryParseAlternation(depth + 1, out var plainBody) || AtEnd || _pattern[_position] != ')')
                        {
                            return false;
                        }

                        _position++;
                        (_matchNewline, _matchMultiline, _matchIgnoreCase, _explicitCapture) = savedExplicitBody;
                        node = plainBody ?? new Utf8SliceNode.EmptyNode();
                        return true;
                    }

                    var slot = _groups++;
                    _groupDecls.Add((slot, null));
                    var savedPlain = (_matchNewline, _matchMultiline, _matchIgnoreCase, _explicitCapture);
                    if (!TryParseAlternation(depth + 1, out var body) || AtEnd || _pattern[_position] != ')')
                    {
                        return false;
                    }

                    _position++;
                    (_matchNewline, _matchMultiline, _matchIgnoreCase, _explicitCapture) = savedPlain;
                    node = new Utf8SliceNode.CaptureNode(slot, body ?? new Utf8SliceNode.EmptyNode());
                    return true;
                case '\\':
                    if (_position + 1 >= _pattern.Length)
                    {
                        return false;
                    }

                    var escaped = _pattern[_position + 1];
                    if (escaped == 'b' || escaped == 'B')
                    {
                        _position += 2;
                        node = new Utf8SliceNode.BoundaryNode(escaped == 'b');
                        return true;
                    }

                    if (escaped == 'A')
                    {
                        _position += 2;
                        node = new Utf8SliceNode.AnchorNode(Utf8SliceAnchorKind.Beginning, false);
                        return true;
                    }

                    if (escaped == 'Z')
                    {
                        _position += 2;
                        node = new Utf8SliceNode.AnchorNode(Utf8SliceAnchorKind.End, false);
                        return true;
                    }

                    if (escaped == 'z')
                    {
                        _position += 2;
                        node = new Utf8SliceNode.AnchorNode(Utf8SliceAnchorKind.VeryEnd, false);
                        return true;
                    }

                    if (escaped == 'G')
                    {
                        // Contiguous matches pin to the search start (BCL startat).
                        _position += 2;
                        node = new Utf8SliceNode.AnchorNode(Utf8SliceAnchorKind.Contiguous, false);
                        return true;
                    }

                    Utf8SliceShorthand? shorthand = escaped switch
                    {
                        'd' => Utf8SliceShorthand.Digit,
                        'D' => Utf8SliceShorthand.NonDigit,
                        'w' => Utf8SliceShorthand.Word,
                        'W' => Utf8SliceShorthand.NonWord,
                        's' => Utf8SliceShorthand.Space,
                        'S' => Utf8SliceShorthand.NonSpace,
                        _ => null,
                    };
                    if (shorthand is not null)
                    {
                        _position += 2;
                        node = new Utf8SliceNode.ShorthandNode(shorthand.Value);
                        return true;
                    }

                    if (escaped == 'p' || escaped == 'P')
                    {
                        // Unicode property escapes lower to owned category
                        // atoms (BCL general categories/groups plus named
                        // blocks, including blocks under IgnoreCase through
                        // invariant case equivalents); malformed or unknown names
                        // decline so legacy construction throws BCL-exact errors.
                        if (!TryParseCategoryAtom(_pattern, _position, escaped == 'P', _matchIgnoreCase, out node, out var categoryNext))
                        {
                            return false;
                        }

                        _position = categoryNext;
                        _sawFold |= _matchIgnoreCase;
                        return true;
                    }

                    // Bind after all declarations: references use final identities and
                    // may precede their declaration (notably in reverse execution).
                    // The complete decimal run binds before octal fallback.
                    // Retain its lexical atom boundary for trailing quantifiers.
                    if (escaped >= '1' && escaped <= '9')
                    {
                        var digitStart = _position + 1;
                        var digitEnd = digitStart + 1;
                        while (digitEnd < _pattern.Length && char.IsAsciiDigit(_pattern[digitEnd])) digitEnd++;
                        _position = digitEnd;
                        _sawFold |= _matchIgnoreCase;
                        node = new Utf8SliceNode.UnresolvedNumericNode(_pattern.Substring(digitStart, digitEnd - digitStart), _matchIgnoreCase);
                        return true;
                    }
                    if (escaped == 'k' && _position + 2 < _pattern.Length && _pattern[_position + 2] is '<' or '\'')
                    {
                        var closer = _pattern[_position + 2] == '<' ? '>' : '\'';
                        var nameStart = _position + 3;
                        var nameEnd = nameStart;
                        while (nameEnd < _pattern.Length && Utf8OwnedCharClass.IsWordChar(_pattern[nameEnd])) nameEnd++;
                        if (nameEnd == nameStart || nameEnd >= _pattern.Length || _pattern[nameEnd] != closer) return false;
                        var name = _pattern.Substring(nameStart, nameEnd - nameStart);
                        _position = nameEnd + 1;
                        _sawFold |= _matchIgnoreCase;
                        node = new Utf8SliceNode.UnresolvedBackrefNode(name, _matchIgnoreCase);
                        return true;
                    }

                    // C control escapes lower to ASCII literals (single-char escapes
                    // with BCL-probed control-code values); control-letter escapes fold
                    // to upper case and mask to 0..31. Anything else stays legacy-exact.
                    if (escaped is 'a' or 'e' or 'f' or 'n' or 'r' or 't' or 'v')
                    {
                        _position += 2;
                        _sawFold |= _matchIgnoreCase;
                        node = new Utf8SliceNode.LiteralNode(escaped switch
                        {
                            'a' => 7,
                            'e' => 27,
                            'f' => 12,
                            'n' => 10,
                            'r' => 13,
                            't' => 9,
                            _ => 11, // 'v' by the outer check
                        }, _matchIgnoreCase);
                        return true;
                    }

                    if (escaped == 'c' && TryParseControlEscape(_pattern, _position + 1, out var control, out var controlNext))
                    {
                        _position = controlNext;
                        _sawFold |= _matchIgnoreCase;
                        node = new Utf8SliceNode.LiteralNode((ushort)control, _matchIgnoreCase);
                        return true;
                    }

                    // Hex, unicode, and NUL-led octal escapes produce one unit;
                    // malformed results decline to
                    // legacy-exact routing (BCL throws on the former).
                    if (escaped == 'x' || escaped == 'u' || escaped == '0')
                    {
                        if (!TryParseNumericEscape(_pattern, _position + 1, false, out var number, out var next))
                        {
                            return false;
                        }

                        _position = next;
                        _sawFold |= _matchIgnoreCase;
                        node = new Utf8SliceNode.LiteralNode((ushort)number, _matchIgnoreCase);
                        return true;
                    }

                    if (Utf8OwnedCharClass.IsWordChar(escaped))
                    {
                        return false;
                    }

                    _position += 2;
                    _sawFold |= _matchIgnoreCase;
                    node = new Utf8SliceNode.LiteralNode(escaped, _matchIgnoreCase);
                    return true;
                case ')':
                case '|':
                case '*':
                case '+':
                case '?':
                    return false;
                case '{':
                    // Only a syntactically complete quantifier is special here.
                    // It either belongs to the preceding atom or must be declined
                    // for the constructor's exact quantifier error (including
                    // overflow and reversed bounds). Other braces are literals.
                    var braceEnd = _position + 1;
                    var firstDigit = braceEnd;
                    while (braceEnd < _pattern.Length && char.IsAsciiDigit(_pattern[braceEnd])) braceEnd++;
                    if (braceEnd > firstDigit)
                    {
                        if (braceEnd < _pattern.Length && _pattern[braceEnd] == ',')
                        {
                            braceEnd++;
                            while (braceEnd < _pattern.Length && char.IsAsciiDigit(_pattern[braceEnd])) braceEnd++;
                        }
                        if (braceEnd < _pattern.Length && _pattern[braceEnd] == '}') return false;
                    }
                    _position++;
                    _sawFold |= _matchIgnoreCase;
                    node = new Utf8SliceNode.LiteralNode(character, _matchIgnoreCase);
                    return true;
                case '}':
                    _position++;
                    _sawFold |= _matchIgnoreCase;
                    node = new Utf8SliceNode.LiteralNode(character, _matchIgnoreCase);
                    return true;
                case '[':
                    return TryParseClass(0, out node);
                case '^':
                    _position++;
                    node = new Utf8SliceNode.AnchorNode(Utf8SliceAnchorKind.Beginning, _matchMultiline);
                    return true;
                case '$':
                    _position++;
                    node = new Utf8SliceNode.AnchorNode(Utf8SliceAnchorKind.End, _matchMultiline);
                    return true;
                case '.':
                    _position++;
                    node = new Utf8SliceNode.DotNode(_matchNewline);
                    return true;
                default:
                    _position++;
                    _sawFold |= _matchIgnoreCase;
                    node = new Utf8SliceNode.LiteralNode(character, _matchIgnoreCase);
                    return true;
            }
        }

        // UTF-16 character classes, qualified against the pinned runtime:
        // literals (with `]`-first and `-`-edge rules), ascending
        // ranges (escapes allowed as bounds), supported single-char
        // escapes, and shorthand members (always members, never bounds).
        // Subtraction recursively parses the final class operand and lowers
        // to an immutable, invocation-metered class chain. Reversed ranges and
        // shorthand upper bounds decline to null so legacy routing
        // preserves exact behavior including construction failures.
        // An unescaped `[` followed by `:`, `.`, or `=` is a literal member
        // under .NET semantics. Retain that syntax fact so sibling profiles
        // can keep their own POSIX, collating, or equivalence interpretation.
        private bool TryParseClass(int depth, out Utf8SliceNode? node)
        {
            node = null;
            if (depth > MaxNestingDepth) return false;
            _position++;

            // Single-shorthand classes fold to the shorthand atom they denote.
            var shorthandOffset = 0;
            if (_position + shorthandOffset < _pattern.Length && _pattern[_position + shorthandOffset] == '^')
            {
                shorthandOffset++;
            }

            if (_position + shorthandOffset + 2 < _pattern.Length &&
                _pattern[_position + shorthandOffset] == '\\' &&
                _pattern[_position + shorthandOffset + 2] == ']')
            {
                Utf8SliceShorthand? shorthand = _pattern[_position + shorthandOffset + 1] switch
                {
                    'd' => Utf8SliceShorthand.Digit,
                    'D' => Utf8SliceShorthand.NonDigit,
                    'w' => Utf8SliceShorthand.Word,
                    'W' => Utf8SliceShorthand.NonWord,
                    's' => Utf8SliceShorthand.Space,
                    'S' => Utf8SliceShorthand.NonSpace,
                    _ => (Utf8SliceShorthand?)null,
                };
                if (shorthand is not null)
                {
                    if (shorthandOffset == 1)
                    {
                        shorthand = (Utf8SliceShorthand)((int)shorthand.Value ^ 1);
                    }

                    _position += shorthandOffset + 3;
                    node = new Utf8SliceNode.ShorthandNode(shorthand.Value);
                    return true;
                }
            }

            // Single-category classes fold to the category atom they denote;
            // anything else naming a category stays legacy-exact (mixed
            // members would need hybrid category operands, and BCL reads a
            // category in bound position as a construction throw).
            var categoryOffset = 0;
            if (_position + categoryOffset < _pattern.Length && _pattern[_position + categoryOffset] == '^')
            {
                categoryOffset++;
            }

            if (_position + categoryOffset + 3 < _pattern.Length &&
                _pattern[_position + categoryOffset] == '\\' &&
                (_pattern[_position + categoryOffset + 1] == 'p' || _pattern[_position + categoryOffset + 1] == 'P') &&
                _pattern[_position + categoryOffset + 2] == '{' &&
                TryParseCategoryAtom(_pattern, _position + categoryOffset, (categoryOffset == 1) != (_pattern[_position + categoryOffset + 1] == 'P'), _matchIgnoreCase, out var categoryNode, out var categoryNext) &&
                categoryNext < _pattern.Length && _pattern[categoryNext] == ']')
            {
                // Class-negated blocks under IgnoreCase cannot fold to one atom:
                // BCL expands the positive ranges before the class negation
                // ([^\\p{IsBasicLatin}]+IC misses 'k'), while a member negation
                // bakes the complement first and overlaps the positive test
                // ([^\\P{IsBasicLatin}]+IC misses 'k' but \\P{IsBasicLatin}+IC
                // hits it). One atom bit cannot keep both apart, so every
                // class-negated block atom falls through to the general class
                // path, which keeps both negations; general categories
                // negate cleanly, so they still fold.
                if (!(categoryNode is Utf8SliceNode.CategoryNode foldedBlock &&
                    foldedBlock.Ranges.Length != 0 && foldedBlock.FoldCase && categoryOffset == 1))
                {
                    _position = categoryNext + 1;
                    node = categoryNode;
                    return true;
                }
            }

            var negated = false;
            if (!AtEnd && _pattern[_position] == '^')
            {
                negated = true;
                _position++;
            }

            var bits = new bool[128];
            var memberBits = 0;
            var categoryMembers = new List<Utf8SliceNode.CategorySetMember>();
            var unicodeRanges = new List<(int Start, int End)>();
            void AddRange(int start, int end)
            {
                for (var member = start; member <= Math.Min(end, 127); member++)
                {
                    bits[member] = true;
                }

                if (end >= 128)
                {
                    unicodeRanges.Add((Math.Max(start, 128), end));
                }
            }
            var lowerBound = -1;
            var inRange = false;
            var firstChar = true;
            var closed = false;
            Utf8SliceNode? subtraction = null;
            for (; !AtEnd; firstChar = false)
            {
                var translatedChar = false;
                var character = _pattern[_position];
                _position++;
                if (character == ']' && !firstChar)
                {
                    closed = true;
                    break;
                }

                int value;
                if (character == '\\' && !AtEnd)
                {
                    var escaped = _pattern[_position];
                    if (escaped is 'd' or 'D' or 's' or 'S' or 'w' or 'W')
                    {
                        // Mixed-class shorthand members (BCL uniform rule):
                        // a shorthand is always a member, never a bound; a
                        // following dash stays literal through the existing
                        // dash logic below. In upper-bound position (after
                        // `X-`) BCL throws, so decline to legacy-exact.
                        if (inRange)
                        {
                            return false;
                        }

                        Utf8SliceShorthand shorthand = escaped switch
                        {
                            'd' => Utf8SliceShorthand.Digit,
                            'D' => Utf8SliceShorthand.NonDigit,
                            'w' => Utf8SliceShorthand.Word,
                            'W' => Utf8SliceShorthand.NonWord,
                            's' => Utf8SliceShorthand.Space,
                            _ => Utf8SliceShorthand.NonSpace,
                        };
                        memberBits |= 1 << (int)shorthand;
                        switch (escaped)
                        {
                            case 'd':
                                for (var digit = '0'; digit <= '9'; digit++)
                                {
                                    bits[digit] = true;
                                }

                                break;
                            case 'D':
                                for (var nonDigit = 0; nonDigit < 128; nonDigit++)
                                {
                                    if (nonDigit < '0' || nonDigit > '9')
                                    {
                                        bits[nonDigit] = true;
                                    }
                                }

                                break;
                            case 'w':
                                for (var word = 0; word < 128; word++)
                                {
                                    if ((word >= '0' && word <= '9') || (word >= 'A' && word <= 'Z') || word == '_' || (word >= 'a' && word <= 'z'))
                                    {
                                        bits[word] = true;
                                    }
                                }

                                break;
                            case 'W':
                                for (var nonWord = 0; nonWord < 128; nonWord++)
                                {
                                    if ((nonWord < '0' || nonWord > '9') && (nonWord < 'A' || nonWord > 'Z') && nonWord != '_' && (nonWord < 'a' || nonWord > 'z'))
                                    {
                                        bits[nonWord] = true;
                                    }
                                }

                                break;
                            case 's':
                                for (var space = 0x09; space <= 0x0D; space++)
                                {
                                    bits[space] = true;
                                }

                                bits[0x20] = true;
                                break;
                            default:
                                for (var nonSpace = 0; nonSpace < 128; nonSpace++)
                                {
                                    if ((nonSpace < 0x09 || nonSpace > 0x0D) && nonSpace != 0x20)
                                    {
                                        bits[nonSpace] = true;
                                    }
                                }

                                break;
                        }

                        _position++;
                        continue;
                    }

                    if (escaped is 'p' or 'P')
                    {
                        // Categories are always members, never bounds (BCL
                        // uniform rule, mirroring shorthands); in
                        // upper-bound position BCL throws, so decline to
                        // legacy-exact. Block members carry the fold bit and
                        // match through invariant case equivalents.
                        if (inRange)
                        {
                            return false;
                        }

                        if (!TryParseCategoryAtom(_pattern, _position - 1, escaped == 'P', _matchIgnoreCase, out var categoryAtom, out var categoryMemberNext) ||
                            categoryAtom is not Utf8SliceNode.CategoryNode categoryMember)
                        {
                            return false;
                        }

                        categoryMembers.Add(new Utf8SliceNode.CategorySetMember(
                            new Utf8SliceCategoryTable(categoryMember.Mask, categoryMember.Ranges),
                            escaped == 'P'));
                        _position = categoryMemberNext;
                        continue;
                    }

                    if (escaped == '-')
                    {
                        _position++;
                        if (inRange)
                        {
                            if (lowerBound > '-')
                            {
                                return false;
                            }

                            AddRange(lowerBound, '-');

                            inRange = false;
                            lowerBound = -1;
                        }
                        else
                        {
                            bits['-'] = true;
                        }

                        continue;
                    }

                    switch (escaped)
                    {
                        case 'a':
                            value = 7;
                            break;
                        case 'b':
                            value = 8;
                            break;
                        case 'e':
                            value = 27;
                            break;
                        case 'f':
                            value = 12;
                            break;
                        case 'n':
                            value = 10;
                            break;
                        case 'r':
                            value = 13;
                            break;
                        case 't':
                            value = 9;
                            break;
                        case 'v':
                            value = 11;
                            break;
                        case 'c':
                            if (!TryParseControlEscape(_pattern, _position, out value, out var controlNext))
                                return false;
                            _position = controlNext - 1;
                            break;
                        case 'x':
                        case 'u':
                        case '0':
                        case '1':
                        case '2':
                        case '3':
                        case '4':
                        case '5':
                        case '6':
                        case '7':
                            // Hex, unicode, and octal escapes (bare octal included)
                            // resolve here; the value flows into member/range logic
                            // below. Anything unusable declines to legacy-exact.
                            if (!TryParseNumericEscape(_pattern, _position, true, out value, out var next))
                            {
                                return false;
                            }

                            // The shared tail advances past one escape char, so step
                            // back to land after multi-char escapes.
                            _position = next - 1;
                            break;
                        default:
                            if (Utf8OwnedCharClass.IsWordChar(escaped))
                            {
                                return false;
                            }

                            value = escaped;
                            break;
                    }

                    _position++;
                    translatedChar = true;
                }
                else
                {
                    value = character;
                }

                if (inRange)
                {
                    inRange = false;
                    if (value == '[' && !translatedChar && !firstChar)
                    {
                        return false;
                    }

                    if (lowerBound > value)
                    {
                        return false;
                    }

                    AddRange(lowerBound, value);

                    lowerBound = -1;
                }
                else if (_position + 1 < _pattern.Length && _pattern[_position] == '-' && _pattern[_position + 1] is not (']' or '['))
                {
                    lowerBound = value;
                    inRange = true;
                    _position++;
                }
                else if (value == '-' && !translatedChar && !AtEnd && _pattern[_position] == '[' && !firstChar)
                {
                    if (!TryParseClass(depth + 1, out subtraction) || subtraction is null ||
                        AtEnd || _pattern[_position] != ']') return false;
                    _position++;
                    closed = true;
                    break;
                }
                else
                {
                    if (value == '[' && !translatedChar && !AtEnd &&
                        (_pattern[_position] == ':' || _pattern[_position] == '.' || _pattern[_position] == '='))
                    {
                        _sawLiteralClassBracketSyntax = true;
                    }

                    AddRange(value, value);
                }
            }

            if (!closed)
            {
                return false;
            }

            // Keep the ASCII mask compact. Merge the remaining literal ranges
            // once and reuse the owned category-set membership/case tables.
            // A final lone endpoint represents a range ending at U+FFFF.
            if (unicodeRanges.Count != 0)
            {
                unicodeRanges.Sort(static (left, right) => left.Start.CompareTo(right.Start));
                var endpoints = new List<ushort>();
                var start = unicodeRanges[0].Start;
                var end = unicodeRanges[0].End;
                for (var range = 1; range <= unicodeRanges.Count; range++)
                {
                    if (range < unicodeRanges.Count && unicodeRanges[range].Start <= end + 1)
                    {
                        end = Math.Max(end, unicodeRanges[range].End);
                        continue;
                    }

                    endpoints.Add((ushort)start);
                    if (end != char.MaxValue) endpoints.Add((ushort)(end + 1));
                    if (range < unicodeRanges.Count)
                    {
                        start = unicodeRanges[range].Start;
                        end = unicodeRanges[range].End;
                    }
                }

                categoryMembers.Add(new Utf8SliceNode.CategorySetMember(
                    new Utf8SliceCategoryTable(0, endpoints.ToArray()), false));
            }

            // Case-insensitive classes carry both cases (input scalars fold
            // before the mask test, so folded members must be present too).
            if (_matchIgnoreCase)
            {
                _sawFold = true;
                for (var member = 0; member < 128; member++)
                {
                    if (bits[member] && ((member >= 65 && member <= 90) || (member >= 97 && member <= 122)))
                    {
                        bits[member ^ 32] = true;
                    }
                }
            }

            var mask = new byte[16];
            for (var member = 0; member < 128; member++)
            {
                if (bits[member])
                {
                    mask[member >> 3] |= (byte)(1 << (member & 7));
                }
            }

            Utf8SliceShorthand[] members;
            if (memberBits == 0)
            {
                members = [];
            }
            else
            {
                var collected = new Utf8SliceShorthand[6];
                var collectedCount = 0;
                for (var bit = 0; bit < 6; bit++)
                {
                    if ((memberBits & (1 << bit)) != 0)
                    {
                        collected[collectedCount++] = (Utf8SliceShorthand)bit;
                    }
                }

                members = collected[..collectedCount];
            }

            if (categoryMembers.Count == 0)
            {
                node = new Utf8SliceNode.SetNode(mask, negated, members, _matchIgnoreCase);
            }
            else
            {
                node = new Utf8SliceNode.CategorySetNode(mask, negated, members, categoryMembers.ToArray(), _matchIgnoreCase);
            }

            if (subtraction is not null) node = Utf8SliceClassProgram.Subtract(node, subtraction);

            return true;
        }

        private bool AtEnd => _position >= _pattern.Length;
    }
}
