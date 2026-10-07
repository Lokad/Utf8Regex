using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.FrontEnd;

namespace Lokad.Utf8Regex.PythonRe;

internal sealed class PythonReParser
{
    private const string PatternBadEscapeLetters = "ceghijklmopqyCEFGHIJKLMNOPQRTVXYz";
    private const string CharacterClassBadEscapeLetters = "ceghijklmopqyzABCEFGHIJKLMOPQRTVXYZ";
    private const string AsciiWordClass = "[A-Za-z0-9_]";
    private const string AsciiNonWordClass = "(?:(?![A-Za-z0-9_])" + ManagedUnicodeScalarPattern + ")";
    private const string AsciiDigitClass = "[0-9]";
    private const string AsciiNonDigitClass = "(?:(?![0-9])" + ManagedUnicodeScalarPattern + ")";
    private const string AsciiSpaceClass = @"[\t\n\r\f\v ]";
    private const string AsciiNonSpaceClass = @"(?:(?![\t\n\r\f\v ])" + ManagedUnicodeScalarPattern + ")";
    private const string AsciiWordBoundary = @"(?:(?<![A-Za-z0-9_])(?=[A-Za-z0-9_])|(?<=[A-Za-z0-9_])(?![A-Za-z0-9_]))";
    // PLAN.md C08: `\B` fails on an empty range in CPython (beginning == end rejects both boundary kinds) while .NET matches empty. The guard fails only when the subject itself is empty, which coincides with an empty range because this API has no endpos parameter.
    private const string AsciiNonWordBoundary = @"(?:(?!\A\z)(?:(?<=[A-Za-z0-9_])(?=[A-Za-z0-9_])|(?<![A-Za-z0-9_])(?![A-Za-z0-9_])))";
    // PLAN.md C04: Python Unicode classes in terms of BCL categories, verified by a
    // full-scalar census against CPython 3.13.2 (census-c04.py): `[\p{L}\p{N}_]`
    // agrees with Python `\w` and `[\s\x1c-\x1f]` agrees with Python `\s` on every
    // non-surrogate scalar. No Unicode property tables are embedded: the BCL
    // supplies the category data. `\d` keeps its .NET pass-through, verified
    // identical (Unicode decimal digits) on the same census.
    private const string UnicodeWordClass = @"[\p{L}\p{N}_]";
    private const string UnicodeNonWordClass = @"(?:(?![\p{L}\p{N}_])" + ManagedUnicodeScalarPattern + ")";
    private const string UnicodeSpaceClass = @"[\s\x1c-\x1f]";
    private const string UnicodeNonSpaceClass = @"(?:(?![\s\x1c-\x1f])" + ManagedUnicodeScalarPattern + ")";
    private const string UnicodeWordBoundary = @"(?:(?<![\p{L}\p{N}_])(?=[\p{L}\p{N}_])|(?<=[\p{L}\p{N}_])(?![\p{L}\p{N}_]))";
    private const string UnicodeNonDigitComplement = @"(?:(?!\d)" + ManagedUnicodeScalarPattern + ")";
    private const string UnicodeNonWordBoundary = @"(?:(?!\A\z)(?:(?<=[\p{L}\p{N}_])(?=[\p{L}\p{N}_])|(?<![\p{L}\p{N}_])(?![\p{L}\p{N}_])))";
    // Temporary managed-backend spelling retained for its live callers. The
    // owned renderer replaces this internal scalar atom before execution; UTF-16
    // pair classes cannot consume supplementary scalars in the core.
    internal const string ManagedUnicodeScalarPattern = @"(?:[\u0000-\uD7FF\uE000-\uFFFF]|[\uD800-\uDBFF][\uDC00-\uDFFF])";
    private const string UnicodeScalarDotAll = ManagedUnicodeScalarPattern;
    private const string UnicodeScalarDot = @"(?!\n)" + ManagedUnicodeScalarPattern;
    private const string OctalDigits = "01234567";
    private const string HexDigits = "0123456789abcdefABCDEF";
    private readonly string _pattern;
    private readonly Dictionary<string, int> _namedGroups = new(StringComparer.Ordinal);
    private readonly Dictionary<int, PythonReNode> _groupBodies = new();
    private bool _hasLookbehind;
    private readonly HashSet<string> _openNamedGroups = new(StringComparer.Ordinal);
    private readonly HashSet<int> _openGroupNumbers = [];
    // A suspended parent reads the completed child's node only after the
    // admitted preparation driver resumes it. The initial node is the empty
    // expression, also the result of an empty sequence.
    private PythonReNode _parsedNode = PythonReAstFactory.Empty;
    private int _position;
    private int _groupCount;
    private PythonReCompileOptions _leadingInlineOptions;

    public PythonReParser(string pattern)
    {
        _pattern = pattern;
    }

    public PythonReParseResult Parse(PythonReCompileOptions options)
    {
        using var construction = Utf8ConstructionScope.Enter();
        ConsumeLeadingInlineFlags(ref options);
        Utf8PreparationStep.TryRun(ParseAlternation(options), construction.Budget);
        var node = _parsedNode;
        if (!IsAtEnd)
        {
            if (Current == ')')
            {
                throw Error("unbalanced parenthesis");
            }

            throw Error("unexpected character");
        }

        var result = new PythonReParseResult(node, options, _groupCount, new Dictionary<string, int>(_namedGroups, StringComparer.Ordinal), _hasLookbehind, new Dictionary<int, PythonReNode>(_groupBodies));
        construction.Complete();
        return result;
    }

    private void ConsumeLeadingInlineFlags(ref PythonReCompileOptions options)
    {
        while (TryConsumeLeadingInlineFlagGroup(out var add))
        {
            Utf8ConstructionScope.ChargeWork(1);
            options |= add;
            _leadingInlineOptions |= add;
        }
    }

    private bool TryConsumeLeadingInlineFlagGroup(out PythonReCompileOptions add)
    {
        add = PythonReCompileOptions.None;
        if (!_pattern.AsSpan(_position).StartsWith("(?", StringComparison.Ordinal))
        {
            return false;
        }

        var probe = _position + 2;
        if (probe >= _pattern.Length || !IsInlineFlagChar(_pattern[probe]))
        {
            return false;
        }

        while (probe < _pattern.Length && IsInlineFlagChar(_pattern[probe]))
        {
            Utf8ConstructionScope.ChargeWork(1);
            probe++;
        }

        if (probe >= _pattern.Length || _pattern[probe] != ')')
        {
            return false;
        }

        _position += 2;
        add = ParseFlagSet(allowEmpty: false, FlagParseContext.Added);
        _position++;
        return true;
    }

    private IEnumerable<Utf8PreparationStep> ParseAlternation(PythonReCompileOptions options)
    {
        var branches = new List<PythonReNode>();
        yield return new Utf8PreparationStep(ParseSequence(options), false);
        branches.Add(_parsedNode);
        while (TryConsume('|'))
        {
            Utf8ConstructionScope.ChargeWork(1);
            yield return new Utf8PreparationStep(ParseSequence(options), false);
            branches.Add(_parsedNode);
        }

        _parsedNode = branches.Count == 1
            ? branches[0]
            : PythonReAstFactory.Alternation(branches);
    }

    private IEnumerable<Utf8PreparationStep> ParseSequence(PythonReCompileOptions options)
    {
        var elements = new List<PythonReNode>();
        while (true)
        {
            Utf8ConstructionScope.ChargeWork(1);
            SkipIgnoredPatternTrivia(options);
            if (IsAtEnd || Current == ')' || Current == '|')
            {
                break;
            }

            PythonReNode atom;
            if (TryConsume('('))
            {
                yield return new Utf8PreparationStep(ParseGroup(options), false);
                atom = _parsedNode;
            }
            else atom = ParseAtom(options);
            elements.Add(ParseQuantifiedAtom(atom));
        }

        _parsedNode = elements.Count switch
        {
            0 => PythonReAstFactory.Empty,
            1 => elements[0],
            _ => PythonReAstFactory.Sequence(elements),
        };
    }

    private PythonReNode ParseQuantifiedAtom(PythonReNode atom)
    {
        if (IsAtEnd)
        {
            return atom;
        }

        int min;
        int? max;
        if (TryConsume('*'))
        {
            min = 0;
            max = null;
        }
        else if (TryConsume('+'))
        {
            min = 1;
            max = null;
        }
        else if (TryConsume('?'))
        {
            min = 0;
            max = 1;
        }
        else if (TryConsume('{'))
        {
            min = ParseNumber();
            max = min;
            if (TryConsume(','))
            {
                max = TryPeekDigit() ? ParseNumber() : null;
            }

            if (!TryConsume('}'))
            {
                throw Error("missing }");
            }

            if (max.HasValue && max.Value < min)
            {
                throw Error("numbers out of order in quantifier");
            }
        }
        else
        {
            return atom;
        }

        var flavor = PythonReQuantifierFlavor.Greedy;
        if (TryConsume('?'))
        {
            flavor = PythonReQuantifierFlavor.Reluctant;
        }
        else if (TryConsume('+'))
        {
            flavor = PythonReQuantifierFlavor.Possessive;
        }

        if (!IsAtEnd && Current is '*' or '+' or '?' or '{')
        {
            throw new PythonRePatternException("multiple repeat", _position);
        }

        return PythonReAstFactory.Quantifier(atom, min, max, flavor);
    }

    private PythonReNode ParseAtom(PythonReCompileOptions options)
    {
        if (!IsAtEnd && Current is '*' or '+' or '?' or '{')
        {
            throw new PythonRePatternException("nothing to repeat", _position);
        }

        if (TryConsume('['))
        {
            return ParseCharacterClass(options);
        }

        if (TryConsume('\\'))
        {
            return ParseEscape(_position - 1, options);
        }

        if (TryConsume('.'))
        {
            // PLAN.md C06: `.` matches one scalar, not one UTF-16 unit. The text is fixed at parse time from the DotAll scope so shape analyses keep keying on the Dot kind; ASCII mode does not restrict dot.
            var dot = (options & PythonReCompileOptions.DotAll) != 0 ? UnicodeScalarDotAll : UnicodeScalarDot;
            return PythonReAstFactory.Raw(dot, PythonReRawKind.Dot);
        }

        if (TryConsume('^'))
        {
            return PythonReAstFactory.Raw("^", PythonReRawKind.StartAnchor);
        }

        if (TryConsume('$'))
        {
            return PythonReAstFactory.Raw("$", PythonReRawKind.EndAnchor);
        }

        var literal = Consume();
        if (char.IsHighSurrogate(literal) &&
            !IsAtEnd &&
            char.IsLowSurrogate(Current))
        {
            // PLAN.md C06: tokenize one scalar before quantification so a repeat binds the whole pair. Mirrors the astral escape path: explicit case partners when folding needs them, otherwise one escaped pair atom.
            var low = Consume();
            var scalar = char.ConvertToUtf32(literal, low);
            return ExpandIgnoreCaseScalar(scalar, options)
                ?? PythonReAstFactory.Raw(Regex.Escape(char.ConvertFromUtf32(scalar)), PythonReRawKind.AstralLiteral);
        }

        return ExpandIgnoreCaseScalar(literal, options)
            ?? PythonReAstFactory.Literal(literal);
    }

    private IEnumerable<Utf8PreparationStep> ParseGroup(PythonReCompileOptions options)
    {
        var groupStart = _position - 1;
        if (!TryConsume('?'))
        {
            var groupNumber = _groupCount + 1;
            _groupCount = groupNumber;
            _openGroupNumbers.Add(groupNumber);
            yield return new Utf8PreparationStep(ParseAlternation(options), false);
            var inner = _parsedNode;
            _openGroupNumbers.Remove(groupNumber);
            ExpectGroupEnd(groupStart, "missing ), unterminated subpattern");
            _groupBodies[groupNumber] = inner;
            _parsedNode = PythonReAstFactory.Group(PythonReGroupKind.Capturing, inner, null, PythonReCompileOptions.None, PythonReCompileOptions.None, groupNumber);
            yield break;
        }

        if (IsAtEnd)
        {
            throw new PythonRePatternException("unexpected end of pattern", _position);
        }

        if (TryConsume('#'))
        {
            while (!IsAtEnd && !TryConsume(')'))
            {
                Utf8ConstructionScope.ChargeWork(1);
                _position++;
            }

            if (IsAtEnd && (_position == 0 || _pattern[_position - 1] != ')'))
            {
                throw new PythonRePatternException("missing ), unterminated comment", groupStart);
            }

            _parsedNode = PythonReAstFactory.Empty;
            yield break;
        }

        if (TryConsume(':'))
        {
            yield return new Utf8PreparationStep(ParseAlternation(options), false);
            var inner = _parsedNode;
            ExpectGroupEnd(groupStart, "missing ), unterminated subpattern");
            _parsedNode = PythonReAstFactory.Group(PythonReGroupKind.NonCapturing, inner, null, PythonReCompileOptions.None, PythonReCompileOptions.None, 0);
            yield break;
        }

        if (TryConsume('>'))
        {
            yield return new Utf8PreparationStep(ParseAlternation(options), false);
            var inner = _parsedNode;
            ExpectGroupEnd(groupStart, "missing ), unterminated subpattern");
            _parsedNode = PythonReAstFactory.Group(PythonReGroupKind.Atomic, inner, null, PythonReCompileOptions.None, PythonReCompileOptions.None, 0);
            yield break;
        }

        if (TryConsume('='))
        {
            yield return new Utf8PreparationStep(ParseAlternation(options), false);
            var inner = _parsedNode;
            ExpectGroupEnd(groupStart, "missing ), unterminated subpattern");
            _parsedNode = PythonReAstFactory.Group(PythonReGroupKind.PositiveLookahead, inner, null, PythonReCompileOptions.None, PythonReCompileOptions.None, 0);
            yield break;
        }

        if (TryConsume('!'))
        {
            yield return new Utf8PreparationStep(ParseAlternation(options), false);
            var inner = _parsedNode;
            ExpectGroupEnd(groupStart, "missing ), unterminated subpattern");
            _parsedNode = PythonReAstFactory.Group(PythonReGroupKind.NegativeLookahead, inner, null, PythonReCompileOptions.None, PythonReCompileOptions.None, 0);
            yield break;
        }

        if (TryConsumeExact("<="))
        {
            yield return new Utf8PreparationStep(ParseAlternation(options), false);
            var inner = _parsedNode;
            ExpectGroupEnd(groupStart, "missing ), unterminated subpattern");
            _hasLookbehind = true;
            _parsedNode = PythonReAstFactory.Group(PythonReGroupKind.PositiveLookbehind, inner, null, PythonReCompileOptions.None, PythonReCompileOptions.None, 0);
            yield break;
        }

        if (TryConsumeExact("<!"))
        {
            yield return new Utf8PreparationStep(ParseAlternation(options), false);
            var inner = _parsedNode;
            ExpectGroupEnd(groupStart, "missing ), unterminated subpattern");
            _hasLookbehind = true;
            _parsedNode = PythonReAstFactory.Group(PythonReGroupKind.NegativeLookbehind, inner, null, PythonReCompileOptions.None, PythonReCompileOptions.None, 0);
            yield break;
        }

        if (TryConsumeExact("P<"))
        {
            var groupNumber = _groupCount + 1;
            _groupCount = groupNumber;
            var name = ParseGroupName('>', "missing >, unterminated name");
            if (!_namedGroups.TryAdd(name, groupNumber))
            {
                throw Error($"redefinition of group name '{name}'");
            }

            _openNamedGroups.Add(name);
            _openGroupNumbers.Add(groupNumber);
            yield return new Utf8PreparationStep(ParseAlternation(options), false);
            var inner = _parsedNode;
            _openNamedGroups.Remove(name);
            _openGroupNumbers.Remove(groupNumber);
            ExpectGroupEnd(groupStart, "missing ), unterminated subpattern");
            _groupBodies[groupNumber] = inner;
            _parsedNode = PythonReAstFactory.Group(PythonReGroupKind.NamedCapturing, inner, name, PythonReCompileOptions.None, PythonReCompileOptions.None, groupNumber);
            yield break;
        }

        if (TryConsumeExact("P="))
        {
            var referenceStart = _position;
            var name = ParseGroupName(')', "missing ), unterminated name");
            if (_openNamedGroups.Contains(name))
            {
                throw new PythonRePatternException("cannot refer to an open group", referenceStart);
            }

            _parsedNode = PythonReAstFactory.NamedReference(name, BackreferenceLower(options));
            yield break;
        }

        if (TryConsume('('))
        {
            var condition = ParseConditionalReference();
            ExpectGroupEnd(groupStart, "missing ), unterminated name");
            yield return new Utf8PreparationStep(ParseSequence(options), false);
            var yesBranch = _parsedNode;
            PythonReNode? noBranch = null;
            if (TryConsume('|'))
            {
                yield return new Utf8PreparationStep(ParseSequence(options), false);
                noBranch = _parsedNode;
                if (!IsAtEnd && Current == '|')
                {
                    throw new PythonRePatternException("conditional backref with more than two branches", _position);
                }
            }

            ExpectGroupEnd(groupStart, "missing ), unterminated subpattern");
            _parsedNode = PythonReAstFactory.Conditional(condition, yesBranch, noBranch);
            yield break;
        }

        if (TryConsume('-'))
        {
            var removedOnlyOptions = ParseFlagSet(allowEmpty: false, context: FlagParseContext.Removed);
            if (!IsAtEnd && char.IsAsciiLetter(Current) && Current != ':')
            {
                throw new PythonRePatternException("unknown flag", _position);
            }

            if (TryConsume(':'))
            {
                yield return new Utf8PreparationStep(ParseAlternation(ApplyScopedFlags(options, PythonReCompileOptions.None, removedOnlyOptions)), false);
                var inner = _parsedNode;
                ExpectGroupEnd(groupStart, "missing ), unterminated subpattern");
                _parsedNode = PythonReAstFactory.Group(PythonReGroupKind.ScopedFlags, inner, null, PythonReCompileOptions.None, removedOnlyOptions, 0);
                yield break;
            }

            throw new PythonRePatternException("missing :", _position);
        }

        if (IsAtEnd)
        {
            throw new PythonRePatternException("missing -, : or )", _position);
        }

        if (Current == 'P')
        {
            if (_position + 1 >= _pattern.Length)
            {
                throw new PythonRePatternException("unexpected end of pattern", _position + 1);
            }

            throw new PythonRePatternException(GetUnknownExtensionMessage(), _position - 1);
        }

        if (char.IsAsciiLetter(Current) && !IsInlineFlagChar(Current))
        {
            throw new PythonRePatternException("unknown extension ?" + Current, _position - 1);
        }

        if (Current == '<')
        {
            if (_position + 1 >= _pattern.Length)
            {
                throw new PythonRePatternException("unexpected end of pattern", _position + 1);
            }

            if (_pattern[_position + 1] == '>')
            {
                throw new PythonRePatternException("unknown extension ?<>", _position - 1);
            }

            // CPython rejects every other (?< opener (balancing shapes,
            // stray flags) as an unknown extension naming the next
            // character; the flag-set fallthrough below only served these
            // invalid spellings with a misleading missing-flag error.
            throw new PythonRePatternException("unknown extension ?<" + _pattern[_position + 1], _position - 1);
        }

        var addOptions = ParseFlagSet(allowEmpty: false, context: FlagParseContext.Added);
        if (!IsAtEnd && Current != ':' && Current != ')' && Current != '-' && !char.IsAsciiLetter(Current))
        {
            throw new PythonRePatternException("missing -, : or )", _position);
        }

        var removeOptions = PythonReCompileOptions.None;
        if (TryConsume('-'))
        {
            removeOptions = ParseFlagSet(allowEmpty: false, context: FlagParseContext.Removed);
            if (!IsAtEnd && char.IsAsciiLetter(Current) && Current != ':')
            {
                throw new PythonRePatternException("unknown flag", _position);
            }
        }

        if (TryConsume(':'))
        {
            yield return new Utf8PreparationStep(ParseAlternation(ApplyScopedFlags(options, addOptions, removeOptions)), false);
            var inner = _parsedNode;
            ExpectGroupEnd(groupStart, "missing ), unterminated subpattern");
            _parsedNode = PythonReAstFactory.Group(PythonReGroupKind.ScopedFlags, inner, null, addOptions, removeOptions, 0);
            yield break;
        }

        if (TryConsume(')'))
        {
            throw new PythonRePatternException("global flags not at the start of the expression", groupStart);
        }

        if (IsAtEnd)
        {
            throw new PythonRePatternException("missing -, : or )", _position);
        }

        if (char.IsAsciiLetter(Current))
        {
            throw new PythonRePatternException("unknown flag", _position);
        }

        throw Error("unknown group extension");
    }

    private PythonReConditionalReference ParseConditionalReference()
    {
        if (TryPeekDigit())
        {
            var referenceStart = _position;
            var number = ParseNumber();
            if (number <= 0)
            {
                throw new PythonRePatternException("bad group number", referenceStart);
            }

            if (IsAtEnd)
            {
                throw new PythonRePatternException("missing ), unterminated name", referenceStart);
            }

            if (Current != ')')
            {
                while (!IsAtEnd && Current != ')')
                {
                    Utf8ConstructionScope.ChargeWork(1);
                    _position++;
                }

                var invalidName = _pattern[referenceStart.._position];
                throw new PythonRePatternException($"bad character in group name '{invalidName}'", referenceStart);
            }

            return PythonReConditionalReference.ForGroupNumber(number);
        }

        var start = _position;
        while (!IsAtEnd && Current != ')')
        {
            Utf8ConstructionScope.ChargeWork(1);
            _position++;
        }

        if (_position == start)
        {
            throw Error("missing group name");
        }

        var name = _pattern[start.._position];
        if (!PythonReGroupNameValidator.IsValid(name))
        {
            throw new PythonRePatternException($"bad character in group name '{name}'", start);
        }

        return PythonReConditionalReference.ForGroupName(name);
    }

    private PythonReNode ParseCharacterClass(PythonReCompileOptions options)
    {
        var classStart = _position - 1;
        var isNegated = TryConsume('^');
        var items = new List<PythonReCharacterClassItem>();
        var allowLiteralClosingBracket = true;
        while (!IsAtEnd)
        {
            Utf8ConstructionScope.ChargeWork(1);
            if (Current == ']' && !allowLiteralClosingBracket)
            {
                _position++;
                return PythonReAstFactory.CharacterClass(isNegated, ExpandClassItems(items, options));
            }

            var parsedItem = ParseCharacterClassItem(options, allowLiteralClosingBracket);
            var item = parsedItem.Item;
            allowLiteralClosingBracket = false;

            if (item is PythonReCharacterClassLiteralItem literal &&
                !IsAtEnd &&
                Current == '-' &&
                _position + 1 < _pattern.Length &&
                _pattern[_position + 1] != ']')
            {
                var rangePosition = _position;
                _position++;
                var endParsedItem = ParseCharacterClassItem(options, allowLiteralClosingBracket: false);
                var endItem = endParsedItem.Item;
                if (endItem is not PythonReCharacterClassLiteralItem endLiteral)
                {
                    throw new PythonRePatternException(
                        $"bad character range {DescribeCharacterClassItem(literal)}-{DescribeCharacterClassItem(endItem)}",
                        parsedItem.Position);
                }

                if (literal.Scalar > endLiteral.Scalar)
                {
                    throw new PythonRePatternException(
                        $"bad character range {DescribeCharacterClassItem(literal)}-{DescribeCharacterClassItem(endLiteral)}",
                        parsedItem.Position);
                }

                items.Add(PythonReAstFactory.ClassRange(literal.Scalar, endLiteral.Scalar));
                continue;
            }

            if (item is not PythonReCharacterClassLiteralItem &&
                !IsAtEnd &&
                Current == '-' &&
                _position + 1 < _pattern.Length &&
                _pattern[_position + 1] != ']')
            {
                var rangePosition = _position;
                _position++;
                var endItem = ParseCharacterClassItem(options, allowLiteralClosingBracket: false).Item;
                throw new PythonRePatternException(
                    $"bad character range {DescribeCharacterClassItem(item)}-{DescribeCharacterClassItem(endItem)}",
                    parsedItem.Position);
            }

            items.Add(item);
        }

        throw new PythonRePatternException("unterminated character set", classStart);

        // Pinned scalar closures contain lowercase inverses and literal-only
        // extra partners. Recompress runs; negate the completed positive class.
        static List<PythonReCharacterClassItem> ExpandClassItems(List<PythonReCharacterClassItem> classItems, PythonReCompileOptions scopeOptions)
        {
            if ((scopeOptions & PythonReCompileOptions.IgnoreCase) == 0)
            {
                return classItems;
            }

            var bmp = new SortedSet<int>();
            var astral = new SortedSet<int>();
            var others = new List<PythonReCharacterClassItem>();
            foreach (var item in classItems)
            {
                Utf8ConstructionScope.ChargeWork(1);
                switch (item)
                {
                    case PythonReCharacterClassLiteralItem literal:
                        AddExpandedScalar(bmp, astral, literal.Scalar, scopeOptions);
                        break;
                    case PythonReCharacterClassRangeItem range:
                        for (var scalar = range.StartScalar; ; scalar++)
                        {
                            Utf8ConstructionScope.ChargeWork(1);
                            AddExpandedScalar(bmp, astral, scalar, scopeOptions);
                            if (scalar == range.EndScalar)
                            {
                                break;
                            }
                        }

                        break;
                    default:
                        others.Add(item);
                        break;
                }
            }

            var rebuilt = new List<PythonReCharacterClassItem>();
            AddScalarRuns(rebuilt, bmp);
            AddScalarRuns(rebuilt, astral);
            rebuilt.AddRange(others);
            return rebuilt.Count == 0 ? classItems : rebuilt;
        }

        static void AddExpandedScalar(SortedSet<int> bmp, SortedSet<int> astral, int scalar, PythonReCompileOptions scopeOptions)
        {
            if (IsAsciiMode(scopeOptions))
            {
                // ASCII folding: exact members plus ASCII case flips.
                bmp.Add(scalar);
                if (scalar is >= 'a' and <= 'z')
                {
                    bmp.Add(scalar - 32);
                }
                else if (scalar is >= 'A' and <= 'Z')
                {
                    bmp.Add(scalar + 32);
                }

                return;
            }

            var target = scalar > 0xFFFF ? astral : bmp;
            target.Add(scalar);
            foreach (var partner in PythonReScalarCaseData.LiteralPartners(scalar))
                {
                Utf8ConstructionScope.ChargeWork(1);
                (partner > 0xFFFF ? astral : bmp).Add(partner);
            }
        }

        static void AddScalarRuns(List<PythonReCharacterClassItem> rebuilt, SortedSet<int> scalars)
        {
            var runStart = -1;
            var runEnd = -1;
            foreach (var scalar in scalars)
            {
                Utf8ConstructionScope.ChargeWork(1);
                if (runStart < 0)
                {
                    runStart = scalar;
                    runEnd = scalar;
                }
                else if (scalar == runEnd + 1)
                {
                    runEnd = scalar;
                }
                else
                {
                    rebuilt.Add(runStart == runEnd
                        ? PythonReAstFactory.ClassLiteral(runStart)
                        : PythonReAstFactory.ClassRange(runStart, runEnd));
                    runStart = scalar;
                    runEnd = scalar;
                }
            }

            if (runStart >= 0)
            {
                rebuilt.Add(runStart == runEnd
                    ? PythonReAstFactory.ClassLiteral(runStart)
                    : PythonReAstFactory.ClassRange(runStart, runEnd));
            }
        }
    }

    private PythonReNode ParseEscape(int escapeStart, PythonReCompileOptions options)
    {
        if (IsAtEnd)
        {
            throw new PythonRePatternException("bad escape (end of pattern)", escapeStart);
        }

        var ch = Consume();
        return ch switch
        {
            'w' when IsAsciiMode(options) => PythonReAstFactory.Raw(AsciiWordClass, PythonReRawKind.Escape),
            'W' when IsAsciiMode(options) => PythonReAstFactory.Raw(AsciiNonWordClass, PythonReRawKind.Escape),
            'd' when IsAsciiMode(options) => PythonReAstFactory.Raw(AsciiDigitClass, PythonReRawKind.Escape),
            'D' when IsAsciiMode(options) => PythonReAstFactory.Raw(AsciiNonDigitClass, PythonReRawKind.Escape),
            's' when IsAsciiMode(options) => PythonReAstFactory.Raw(AsciiSpaceClass, PythonReRawKind.Escape),
            'S' when IsAsciiMode(options) => PythonReAstFactory.Raw(AsciiNonSpaceClass, PythonReRawKind.Escape),
            'b' when IsAsciiMode(options) => PythonReAstFactory.Raw(AsciiWordBoundary, PythonReRawKind.Escape),
            'B' when IsAsciiMode(options) => PythonReAstFactory.Raw(AsciiNonWordBoundary, PythonReRawKind.Escape),
            'w' or 'W' or 's' or 'S' or 'd' or 'D' when !IsAsciiMode(options) => PythonReAstFactory.Raw(GetUnicodePredefinedFragment(ch), PythonReRawKind.Escape),
            'b' when !IsAsciiMode(options) => PythonReAstFactory.Raw(UnicodeWordBoundary, PythonReRawKind.Escape),
            'B' when !IsAsciiMode(options) => PythonReAstFactory.Raw(UnicodeNonWordBoundary, PythonReRawKind.Escape),
            'Z' => PythonReAstFactory.Raw(@"\z", PythonReRawKind.Escape),
            'A' or 'b' or 'B' or 'd' or 'D' or 's' or 'S' or 'w' or 'W' or '\\' or '.' or '^' or '$' or '*' or '+' or '?' or '{' or '}' or '[' or ']' or '(' or ')' or '|' =>
                PythonReAstFactory.Raw("\\" + ch, PythonReRawKind.Escape),
            'x' => CreateEscapedLiteralNode(ParseHexEscapeValue(escapeStart, 2, 'x'), options),
            'u' => CreateEscapedLiteralNode(ParseHexEscapeValue(escapeStart, 4, 'u'), options),
            'U' => CreateEscapedLiteralNode(ParseUnicodeScalarEscapeValue(escapeStart), options),
            'N' => throw new PythonRePatternException(@"\N{...} named Unicode escapes are intentionally unsupported in PythonRe to avoid a large embedded Unicode-name table.", escapeStart),
            '0' => CreateEscapedLiteralNode(ParseOctalEscapeValue(escapeStart, ch), options),
            >= '1' and <= '9' => ParseNumericEscapeOrBackreference(ch, escapeStart, options),
            _ when PatternBadEscapeLetters.IndexOf(ch) >= 0 => throw new PythonRePatternException($@"bad escape \{ch}", escapeStart),
            _ => PythonReAstFactory.Raw("\\" + ch, PythonReRawKind.Escape),
        };
    }

    // Exact scalar classes encode all profile folding before core lowering.
    // A scoped ASCII class must stay exact against non-ASCII fold partners.
    private static PythonReNode? ExpandIgnoreCaseScalar(int scalar, PythonReCompileOptions options)
    {
        if ((options & PythonReCompileOptions.IgnoreCase) == 0) return null;
        if (IsAsciiMode(options))
        {
            if (scalar is >= 'a' and <= 'z') return ToAsciiPairClass(scalar, scalar - 32);
            if (scalar is >= 'A' and <= 'Z') return ToAsciiPairClass(scalar, scalar + 32);
            return null;
        }
        var partners = PythonReScalarCaseData.LiteralPartners(scalar);
        return partners.IsEmpty ? null : ToScalarClass(new SortedSet<int>(partners.ToArray()));
    }

    static PythonReCharacterClassNode ToScalarClass(SortedSet<int> members)
    {
        var items = new List<PythonReCharacterClassItem>(members.Count);
        foreach (var member in members)
        {
            Utf8ConstructionScope.ChargeWork(1);
            items.Add(PythonReAstFactory.ClassLiteral(member));
        }

        return PythonReAstFactory.CharacterClass(false, items);
    }

    static PythonReCharacterClassNode ToAsciiPairClass(int scalar, int partner)
    {
        return PythonReAstFactory.CharacterClass(false, [PythonReAstFactory.ClassLiteral(scalar), PythonReAstFactory.ClassLiteral(partner)]);
    }

    private static PythonReNode CreateEscapedLiteralNode(string value, PythonReCompileOptions options)
    {
        if (value.Length == 1 && char.IsSurrogate(value[0])) return PythonReAstFactory.Literal(value[0]);
        int scalar;
        if (value.Length == 1 && !char.IsSurrogate(value[0]))
        {
            scalar = value[0];
        }
        else if (value.Length == 2 && char.IsSurrogatePair(value[0], value[1]))
        {
            scalar = char.ConvertToUtf32(value, 0);
        }
        else
        {
            return PythonReAstFactory.Raw(Regex.Escape(value), PythonReRawKind.Escape);
        }

        return ExpandIgnoreCaseScalar(scalar, options)
            ?? PythonReAstFactory.Raw(Regex.Escape(value), PythonReRawKind.Escape);
    }

    private PythonReNode ParseNumericEscapeOrBackreference(char firstDigit, int escapeStart, PythonReCompileOptions options)
    {
        if (TryPeekDigit())
        {
            var secondDigit = Consume();
            if (IsOctalDigit(firstDigit) &&
                IsOctalDigit(secondDigit) &&
                !IsAtEnd &&
                IsOctalDigit(Current))
            {
                var thirdDigit = Consume();
                return CreateEscapedLiteralNode(ParseOctalEscapeValue(escapeStart, firstDigit, secondDigit, thirdDigit), options);
            }

            return ParseNumericBackreference(firstDigit, secondDigit, escapeStart, options);
        }

        return ParseNumericBackreference(firstDigit, null, escapeStart, options);
    }

    private PythonReNode ParseNumericBackreference(char firstDigit, char? secondDigit, int escapeStart, PythonReCompileOptions options)
    {
        var value = firstDigit - '0';
        if (secondDigit.HasValue)
        {
            value = checked(value * 10 + (secondDigit.Value - '0'));
        }

        if (_openGroupNumbers.Contains(value))
        {
            throw new PythonRePatternException("cannot refer to an open group", escapeStart);
        }

        if (value > _groupCount)
        {
            throw new PythonRePatternException($"invalid group reference {value}", escapeStart + 1);
        }

        return PythonReAstFactory.NumericReference(value, BackreferenceLower(options));
    }

    // References compare captured scalars using lexical simple lowercase,
    // without the literal-only extra partners.
    private static Func<int, int>? BackreferenceLower(PythonReCompileOptions options)
        => (options & PythonReCompileOptions.IgnoreCase) == 0 ? null
            : IsAsciiMode(options) ? PythonReScalarCaseData.LowerAscii : PythonReScalarCaseData.LowerUnicode;

    private PythonReParsedCharacterClassItem ParseCharacterClassItem(PythonReCompileOptions options, bool allowLiteralClosingBracket)
    {
        var escapeStart = _position;
        var ch = Consume();
        if (ch == '\\')
        {
            if (IsAtEnd)
            {
                throw new PythonRePatternException("bad escape (end of pattern)", escapeStart);
            }

            var escaped = Consume();
            if (CharacterClassBadEscapeLetters.IndexOf(escaped) >= 0)
            {
                throw new PythonRePatternException($@"bad escape \{escaped}", escapeStart);
            }

            if (escaped == 'N')
            {
                throw new PythonRePatternException(@"\N{...} named Unicode escapes are intentionally unsupported in PythonRe to avoid a large embedded Unicode-name table.", escapeStart);
            }

            var escapedLiteral = ParseEscapedCharacterClassLiteralScalar(escaped, escapeStart);
            if (escapedLiteral.HasValue)
            {
                return new PythonReParsedCharacterClassItem(PythonReAstFactory.ClassLiteral(escapedLiteral.Value), escapeStart);
            }

            if (TryGetAsciiCharacterClassEscapeFragment(escaped, options, out var asciiFragment))
            {
                return new PythonReParsedCharacterClassItem(PythonReAstFactory.ClassPredefined(asciiFragment, false), escapeStart);
            }

            if (escaped is 'w' or 'W' or 'd' or 'D' or 's' or 'S')
            {
                return new PythonReParsedCharacterClassItem(PythonReAstFactory.ClassPredefined(GetUnicodePredefinedFragment(escaped), true), escapeStart);
            }

            return new PythonReParsedCharacterClassItem(PythonReAstFactory.ClassLiteral(escaped), escapeStart);
        }

        if (ch == ']' && allowLiteralClosingBracket)
        {
            return new PythonReParsedCharacterClassItem(PythonReAstFactory.ClassLiteral(ch), escapeStart);
        }

        if (char.IsHighSurrogate(ch) &&
            !IsAtEnd &&
            char.IsLowSurrogate(Current))
        {
            var low = Consume();
            return new PythonReParsedCharacterClassItem(PythonReAstFactory.ClassLiteral(char.ConvertToUtf32(ch, low)), escapeStart);
        }

        return new PythonReParsedCharacterClassItem(PythonReAstFactory.ClassLiteral(ch), escapeStart);
    }

    private int? ParseEscapedCharacterClassLiteralScalar(char escaped, int escapeStart)
    {
        switch (escaped)
        {
            case 'x':
                return ParseHexEscapeScalarValue(escapeStart, 2, 'x');
            case 'u':
                return ParseHexEscapeScalarValue(escapeStart, 4, 'u');
            case 'U':
                return ParseUnicodeScalarEscapeIntValue(escapeStart);
            case >= '0' and <= '7':
                return ParseOctalEscapeIntValue(escapeStart, escaped);
            case '8' or '9':
                throw new PythonRePatternException($@"bad escape \{escaped}", escapeStart);
            default:
                return null;
        }
    }

    private string ParseHexEscapeValue(int escapeStart, int digits, char kind)
    {
        return ((char)ParseHexEscapeScalarValue(escapeStart, digits, kind)).ToString();
    }

    private int ParseHexEscapeScalarValue(int escapeStart, int digits, char kind)
    {
        var hex = ReadHexDigits(escapeStart, digits, kind);
        return Convert.ToInt32(hex, 16);
    }

    private string ParseUnicodeScalarEscapeValue(int escapeStart)
    {
        var value = ParseUnicodeScalarEscapeIntValue(escapeStart);
        return value <= 0xFFFF ? ((char)value).ToString() : char.ConvertFromUtf32(value);
    }

    private int ParseUnicodeScalarEscapeIntValue(int escapeStart)
    {
        var hex = ReadHexDigits(escapeStart, 8, 'U');
        var scalar = Convert.ToInt32(hex, 16);
        if ((uint)scalar > 0x10FFFF)
        {
            throw new PythonRePatternException($@"bad escape \U{hex}", escapeStart);
        }

        return scalar;
    }

    private string ReadHexDigits(int escapeStart, int digits, char kind)
    {
        var builder = new StringBuilder(digits);
        while (builder.Length < digits && !IsAtEnd && HexDigits.IndexOf(Current) >= 0)
        {
            Utf8ConstructionScope.ChargeWork(1);
            builder.Append(Consume());
        }

        if (builder.Length != digits)
        {
            throw new PythonRePatternException($@"incomplete escape \{kind}{builder}", escapeStart);
        }

        return builder.ToString();
    }

    private string ParseOctalEscapeValue(int escapeStart, params char[] digits)
    {
        return char.ConvertFromUtf32(ParseOctalEscapeIntValue(escapeStart, digits));
    }

    private int ParseOctalEscapeIntValue(int escapeStart, params char[] digits)
    {
        var builder = new StringBuilder(3);
        foreach (var digit in digits)
        {
            Utf8ConstructionScope.ChargeWork(1);
            builder.Append(digit);
        }

        while (builder.Length < 3 && !IsAtEnd && IsOctalDigit(Current))
        {
            Utf8ConstructionScope.ChargeWork(1);
            builder.Append(Consume());
        }

        var value = Convert.ToInt32(builder.ToString(), 8);
        if (value > 0xFF)
        {
            throw new PythonRePatternException($@"octal escape value \{builder} outside of range 0-0o377", escapeStart);
        }

        return value;
    }

    private static bool IsOctalDigit(char ch) => OctalDigits.IndexOf(ch) >= 0;

    private string ParseGroupName(char terminator, string unterminatedMessage)
    {
        if (IsAtEnd)
        {
            throw Error("missing group name");
        }

        var start = _position;
        while (!IsAtEnd && Current != terminator)
        {
            Utf8ConstructionScope.ChargeWork(1);
            if (terminator == '>' && Current == ')')
            {
                if (_position == start)
                {
                    throw new PythonRePatternException("missing group name", start);
                }

                throw new PythonRePatternException(unterminatedMessage, start);
            }

            _position++;
        }

        if (IsAtEnd)
        {
            if (_position == start)
            {
                throw Error("missing group name");
            }

            throw new PythonRePatternException(unterminatedMessage, start);
        }

        var name = _pattern[start.._position];
        _position++;
        if (name.Length == 0)
        {
            throw new PythonRePatternException("missing group name", start);
        }

        if (!PythonReGroupNameValidator.IsValid(name))
        {
            throw new PythonRePatternException($"bad character in group name '{name}'", start);
        }

        return name;
    }

    private PythonReCompileOptions ParseFlagSet(bool allowEmpty, FlagParseContext context)
    {
        var start = _position;
        var options = PythonReCompileOptions.None;
        while (!IsAtEnd)
        {
            Utf8ConstructionScope.ChargeWork(1);
            PythonReCompileOptions next = Current switch
            {
                'i' => PythonReCompileOptions.IgnoreCase,
                'm' => PythonReCompileOptions.Multiline,
                's' => PythonReCompileOptions.DotAll,
                'x' => PythonReCompileOptions.Verbose,
                'a' => PythonReCompileOptions.Ascii,
                'L' => throw CreateInlineFlagException('L', context),
                'u' => PythonReCompileOptions.Unicode,
                _ => PythonReCompileOptions.None,
            };

            if (next == PythonReCompileOptions.None)
            {
                break;
            }

            if (context == FlagParseContext.Removed &&
                next is PythonReCompileOptions.Ascii or PythonReCompileOptions.Unicode)
            {
                throw CreateInlineFlagException(Current, context);
            }

            if (next is PythonReCompileOptions.Ascii or PythonReCompileOptions.Unicode)
            {
                var hasAsciiOrUnicode = (options & (PythonReCompileOptions.Ascii | PythonReCompileOptions.Unicode)) != 0;
                if (hasAsciiOrUnicode && (options & next) == 0)
                {
                    throw new PythonRePatternException("bad inline flags: flags 'a' and 'u' are incompatible", _position + 1);
                }
            }

            options |= next;
            _position++;
        }

        if (!allowEmpty && _position == start)
        {
            if (!IsAtEnd && char.IsAsciiLetter(Current))
            {
                throw new PythonRePatternException("unknown flag", _position);
            }

            throw Error("missing flag");
        }

        return options;
    }

    private PythonRePatternException CreateInlineFlagException(char flag, FlagParseContext context)
    {
        return context switch
        {
            FlagParseContext.Removed => new PythonRePatternException("bad inline flags: cannot turn off flags 'a', 'u' and 'L'", _position + 1),
            _ => new PythonRePatternException("bad inline flags: flags 'a', 'u' and 'L' are incompatible", _position + 1),
        };
    }

    private string GetUnknownExtensionMessage()
    {
        if (_position >= _pattern.Length)
        {
            return "unexpected end of pattern";
        }

        if (_position < _pattern.Length && _pattern[_position] == 'P')
        {
            var suffix = _position + 1 < _pattern.Length ? _pattern[_position + 1].ToString() : string.Empty;
            return "unknown extension ?P" + suffix;
        }

        return "unknown extension ?" + _pattern[_position];
    }

    private static bool IsInlineFlagChar(char ch) => ch is 'i' or 'm' or 's' or 'x' or 'a' or 'L' or 'u';

    private static bool IsAsciiMode(PythonReCompileOptions options)
        => (options & PythonReCompileOptions.Ascii) != 0;

    private void SkipIgnoredPatternTrivia(PythonReCompileOptions options)
    {
        if ((options & PythonReCompileOptions.Verbose) == 0)
        {
            return;
        }

        while (!IsAtEnd)
        {
            Utf8ConstructionScope.ChargeWork(1);
            // CPython verbose skips exactly space, tab, LF, CR, VT, and FF (Lib/re/_parser.py WHITESPACE), unlike char.IsWhiteSpace, which also eats no-break spaces and Unicode spaces that CPython preserves literally.
            if (Current is ' ' or '\t' or '\n' or '\r' or '\v' or '\f')
            {
                _position++;
                continue;
            }

            if (Current == '#')
            {
                _position++;
                while (!IsAtEnd && Current != '\n')
                {
                    Utf8ConstructionScope.ChargeWork(1);
                    _position++;
                }

                continue;
            }

            break;
        }
    }

    private static PythonReCompileOptions ApplyScopedFlags(
        PythonReCompileOptions options,
        PythonReCompileOptions addOptions,
        PythonReCompileOptions removeOptions)
    {
        if ((addOptions & PythonReCompileOptions.Ascii) != 0)
        {
            options &= ~PythonReCompileOptions.Unicode;
        }

        if ((addOptions & PythonReCompileOptions.Unicode) != 0)
        {
            options &= ~PythonReCompileOptions.Ascii;
        }

        return (options | addOptions) & ~removeOptions;
    }

    private static string GetUnicodePredefinedFragment(char escaped) => escaped switch
    {
        'w' => UnicodeWordClass,
        'W' => UnicodeNonWordClass,
        's' => UnicodeSpaceClass,
        'S' => UnicodeNonSpaceClass,
        'd' => @"\d",
        'D' => UnicodeNonDigitComplement,
        _ => @"\" + escaped,
    };

    private static bool TryGetAsciiCharacterClassEscapeFragment(char escaped, PythonReCompileOptions options, out string fragment)
    {
        if (!IsAsciiMode(options))
        {
            fragment = string.Empty;
            return false;
        }

        switch (escaped)
        {
            case 'w':
                fragment = "A-Za-z0-9_";
                return true;
            case 'W':
                fragment = "^A-Za-z0-9_";
                return true;
            case 'd':
                fragment = "0-9";
                return true;
            case 'D':
                fragment = "^0-9";
                return true;
            case 's':
                fragment = @"\t\n\r\f\v ";
                return true;
            case 'S':
                fragment = @"^\t\n\r\f\v ";
                return true;
            default:
                fragment = string.Empty;
                return false;
        }
    }

    private int ParseNumber()
    {
        if (!TryPeekDigit())
        {
            throw Error("expected number");
        }

        var value = 0;
        while (TryPeekDigit())
        {
            Utf8ConstructionScope.ChargeWork(1);
            value = checked(value * 10 + (Consume() - '0'));
        }

        return value;
    }

    private bool TryPeekDigit() => !IsAtEnd && char.IsAsciiDigit(Current);

    private bool IsAtEnd => _position >= _pattern.Length;

    private char Current => _pattern[_position];

    private char Consume() => _pattern[_position++];

    private void Expect(char ch)
    {
        if (!TryConsume(ch))
        {
            throw Error($"missing {ch}");
        }
    }

    private void ExpectGroupEnd(int groupStart, string message)
    {
        if (!TryConsume(')'))
        {
            throw new PythonRePatternException(message, groupStart);
        }
    }

    private bool TryConsume(char ch)
    {
        if (!IsAtEnd && Current == ch)
        {
            _position++;
            return true;
        }

        return false;
    }

    private static string EscapeCharacterClassLiteral(string value)
    {
        var builder = new StringBuilder(value.Length + 4);
        foreach (var ch in value)
        {
            Utf8ConstructionScope.ChargeWork(1);
            if (ch is '\\' or '-' or ']' or '^')
            {
                builder.Append('\\');
            }

            builder.Append(ch);
        }

        return builder.ToString();
    }

    private static string DescribeCharacterClassItem(PythonReCharacterClassItem item)
    {
        return item switch
        {
            PythonReCharacterClassLiteralItem literal => char.ConvertFromUtf32(literal.Scalar),
            PythonReCharacterClassRangeItem range => char.ConvertFromUtf32(range.StartScalar) + "-" + char.ConvertFromUtf32(range.EndScalar),
            // Standalone Unicode fragments render back to their Python escape so
            // diagnostics keep naming the source construct (inline switch: a new
            // single-caller helper would count as source debt).
            PythonReCharacterClassPredefinedItem predefined => predefined.RegexClassFragment switch
            {
                _ when predefined.IsStandaloneAtom && predefined.RegexClassFragment == UnicodeWordClass => @"\w",
                _ when predefined.IsStandaloneAtom && predefined.RegexClassFragment == UnicodeNonWordClass => @"\W",
                _ when predefined.IsStandaloneAtom && predefined.RegexClassFragment == UnicodeSpaceClass => @"\s",
                _ when predefined.IsStandaloneAtom && predefined.RegexClassFragment == UnicodeNonSpaceClass => @"\S",
                _ when predefined.RegexClassFragment.StartsWith("^", StringComparison.Ordinal) => @"\" + char.ToUpperInvariant(predefined.RegexClassFragment[1]),
                _ when predefined.RegexClassFragment.StartsWith(@"\", StringComparison.Ordinal) => predefined.RegexClassFragment,
                _ => @"\" + InferAsciiEscapeName(predefined.RegexClassFragment),
            },
            _ => "?",
        };
    }

    private static char InferAsciiEscapeName(string fragment)
    {
        return fragment switch
        {
            "A-Za-z0-9_" => 'w',
            "^A-Za-z0-9_" => 'W',
            "0-9" => 'd',
            "^0-9" => 'D',
            @"\t\n\r\f\v " => 's',
            @"^\t\n\r\f\v " => 'S',
            _ => '?',
        };
    }

    private bool TryConsumeExact(string text)
    {
        if (_pattern.AsSpan(_position).StartsWith(text, StringComparison.Ordinal))
        {
            _position += text.Length;
            return true;
        }

        return false;
    }

    private PythonRePatternException Error(string message) => new(message, _position);
}

internal readonly record struct PythonReParseResult(
    PythonReNode Root,
    PythonReCompileOptions Options,
    int CaptureGroupCount,
    IReadOnlyDictionary<string, int> NamedGroups,
    bool HasLookbehind,
    IReadOnlyDictionary<int, PythonReNode> GroupBodies);

internal enum FlagParseContext
{
    Added,
    Removed,
}

internal readonly record struct PythonReParsedCharacterClassItem(PythonReCharacterClassItem Item, int Position);
