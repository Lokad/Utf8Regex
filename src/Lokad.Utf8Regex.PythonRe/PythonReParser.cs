using Lokad.Utf8Regex.Internal.FrontEnd;

namespace Lokad.Utf8Regex.PythonRe;

internal sealed partial class PythonReParser
{
    private const string PatternBadEscapeLetters = "ceghijklmopqyCEFGHIJKLMNOPQRTVXYz";
    private const string CharacterClassBadEscapeLetters = "ceghijklmopqyzABCEFGHIJKLMOPQRTVXYZ";
    private const string AsciiWordClass = "[A-Za-z0-9_]";
    private const string AsciiNonWordClass = "(?:(?![A-Za-z0-9_])" + ManagedUnicodeScalarPattern + ")";
    private const string AsciiDigitClass = "[0-9]";
    private const string AsciiNonDigitClass = "(?:(?![0-9])" + ManagedUnicodeScalarPattern + ")";
    private const string AsciiSpaceClass = @"[\t\n\r\f\v ]";
    private const string AsciiNonSpaceClass = @"(?:(?![\t\n\r\f\v ])" + ManagedUnicodeScalarPattern + ")";
    internal const string AsciiWordBoundary = @"(?:(?<![A-Za-z0-9_])(?=[A-Za-z0-9_])|(?<=[A-Za-z0-9_])(?![A-Za-z0-9_]))";
    // PLAN.md C08: `\B` fails on an empty range in CPython (beginning == end rejects both boundary kinds) while .NET matches empty. The guard fails only when the subject itself is empty, which coincides with an empty range because this API has no endpos parameter.
    internal const string AsciiNonWordBoundary = @"(?:(?!\A\z)(?:(?<=[A-Za-z0-9_])(?=[A-Za-z0-9_])|(?<![A-Za-z0-9_])(?![A-Za-z0-9_])))";
    // Semantic fragments retained for comparator rendering and native planning.
    // The scalar compiler lowers them directly with pinned profile predicates;
    // host-category native routes are ineligible for these atoms/boundaries.
    // C04's historical category census alone did not qualify new host versions.
    internal const string UnicodeWordClass = @"[\p{L}\p{N}_]";
    internal const string UnicodeNonWordClass = @"(?:(?![\p{L}\p{N}_])" + ManagedUnicodeScalarPattern + ")";
    internal const string UnicodeSpaceClass = @"[\s\x1c-\x1f]";
    internal const string UnicodeNonSpaceClass = @"(?:(?![\s\x1c-\x1f])" + ManagedUnicodeScalarPattern + ")";
    internal const string UnicodeWordBoundary = @"(?:(?<![\p{L}\p{N}_])(?=[\p{L}\p{N}_])|(?<=[\p{L}\p{N}_])(?![\p{L}\p{N}_]))";
    internal const string UnicodeNonDigitComplement = @"(?:(?!\d)" + ManagedUnicodeScalarPattern + ")";
    internal const string UnicodeNonWordBoundary = @"(?:(?!\A\z)(?:(?<=[\p{L}\p{N}_])(?=[\p{L}\p{N}_])|(?<![\p{L}\p{N}_])(?![\p{L}\p{N}_])))";

    internal static bool IsUnicodePredefinedFragment(string text) => text is UnicodeWordClass or UnicodeNonWordClass
        or UnicodeSpaceClass or UnicodeNonSpaceClass or UnicodeWordBoundary or UnicodeNonWordBoundary
        or UnicodeNonDigitComplement or @"\d";
    // Temporary managed-backend spelling retained for its live callers. The
    // owned renderer replaces this internal scalar atom before execution; UTF-16
    // pair classes cannot consume supplementary scalars in the core.
    internal const string ManagedUnicodeScalarPattern = @"(?:[\u0000-\uD7FF\uE000-\uFFFF]|[\uD800-\uDBFF][\uDC00-\uDFFF])";
    private const string UnicodeScalarDotAll = ManagedUnicodeScalarPattern;
    private const string UnicodeScalarDot = @"(?!\n)" + ManagedUnicodeScalarPattern;
    private const string OctalDigits = "01234567";
    private const string HexDigits = "0123456789abcdefABCDEF";
    private readonly string _pattern;
    private PythonReParserStorage? _storage;
    private PythonReParserStorage Storage => _storage ?? throw new InvalidOperationException("Parser storage is not initialized.");
    private bool _hasLookbehind;
    // A suspended parent reads the completed child's node only after the
    // admitted preparation driver resumes it. The initial node is the empty
    // expression, also the result of an empty sequence.
    private PythonReNode _parsedNode = PythonReAstFactory.Empty;
    private int _position;
    private int _groupCount;
    private int _lookbehindGroupStart;
    private PythonReCompileOptions _leadingInlineOptions;

    internal static PythonReParser Create(string pattern) =>
        Utf8ConstructionScope.CurrentBudget.AllocateObject(
            (pattern, (PythonReParserStorage?)null, false, PythonReAstFactory.Empty, 0, 0, 0, PythonReCompileOptions.None),
            static state => new PythonReParser(state.pattern));

    public PythonReParser(string pattern)
    {
        _pattern = pattern;
    }

    public PythonReParseResult Parse(PythonReCompileOptions options)
    {
        using var construction = Utf8ConstructionScope.Enter();
        PythonReParseResult result;
        try
        {
            ConsumeLeadingInlineFlags(ref options);
            ParseFrames(options, construction.Budget);
            var node = _parsedNode;
            if (!IsAtEnd)
            {
                if (Current == ')') throw Error("unbalanced parenthesis");
                throw Error("unexpected character");
            }
            result = new PythonReParseResult(node, options, _groupCount,
                Storage.NamedGroups.Freeze(Storage.Arena), _hasLookbehind, Storage.Arena.Take(Storage.GroupBodies, true));
            Storage.Arena.Complete();
        }
        catch
        {
            _parsedNode = PythonReAstFactory.Empty;
            throw;
        }
        finally
        {
            _storage?.Dispose();
            _storage = null;
        }
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

    private PythonReNode ParseQuantifiedAtom(PythonReNode atom, PythonReCompileOptions options)
    {
        SkipIgnoredPatternTrivia(options);
        if (IsAtEnd)
        {
            return atom;
        }

        uint min;
        uint? max;
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
        else if (IsBraceQuantifier())
        {
            _position++;
            var boundsStart = _position;
            min = TryPeekDigit() ? ParseNumber() : 0;
            max = min;
            if (TryConsume(','))
            {
                max = TryPeekDigit() ? ParseNumber() : null;
            }

            _position++; // The lexical check established the closing brace.

            if (max.HasValue && max.Value < min)
            {
                throw new PythonRePatternException("min repeat greater than max repeat", boundsStart);
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

        // Trivia may separate repeats, but never joins a lazy/possessive suffix.
        SkipIgnoredPatternTrivia(options);
        if (!IsAtEnd && (Current is '*' or '+' or '?' || IsBraceQuantifier()))
        {
            throw new PythonRePatternException("multiple repeat", _position);
        }

        return PythonReAstFactory.Quantifier(atom, min, max, flavor);
    }

    private PythonReNode ParseAtom(PythonReCompileOptions options)
    {
        if (!IsAtEnd && (Current is '*' or '+' or '?' || IsBraceQuantifier()))
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
                ?? PythonReAstFactory.Raw(Utf8ConstructionText.EscapeScalar(scalar), PythonReRawKind.AstralLiteral);
        }

        return ExpandIgnoreCaseScalar(literal, options)
            ?? PythonReAstFactory.Literal(literal);
    }

    private PythonReConditionalReference ParseConditionalReference()
    {
        if (TryPeekDigit())
        {
            var referenceStart = _position;
            var number = 0;
            var tooLarge = false;
            // Finish the header before classifying it, including after the
            // numeric value exceeds our group index. Keep every digit metered.
            while (TryPeekDigit())
            {
                Utf8ConstructionScope.ChargeWork(1);
                var digit = Consume() - '0';
                if (number > (int.MaxValue - digit) / 10) tooLarge = true;
                else if (!tooLarge) number = number * 10 + digit;
            }
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

                var invalidName = Storage.Arena.Budget.CopyText(_pattern.AsSpan(referenceStart, _position - referenceStart));
                throw new PythonRePatternException($"bad character in group name '{invalidName}'", referenceStart);
            }

            if (tooLarge)
            {
                using var message = Utf8ConstructionText.Create();
                throw new PythonRePatternException(message.Append("invalid group reference ")
                    .Append(_pattern.AsSpan(referenceStart, _position - referenceStart).TrimStart('0')).ToText(), referenceStart);
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

        var name = Storage.Arena.Budget.CopyText(_pattern.AsSpan(start, _position - start));
        if (!PythonReGroupNameValidator.IsValid(name))
        {
            throw new PythonRePatternException($"bad character in group name '{name}'", start);
        }

        // Names resolve where the conditional is parsed. Unlike numeric
        // references, a later group definition cannot make this name valid.
        if (!IsAtEnd && !Storage.NamedGroups.ContainsKey(name))
        {
            throw new PythonRePatternException($"unknown group name '{name}'", start);
        }

        return PythonReConditionalReference.ForGroupName(name);
    }

    private PythonReNode ParseCharacterClass(PythonReCompileOptions options)
    {
        var classStart = _position - 1;
        var isNegated = TryConsume('^');
        using var items = Utf8ConstructionBuffer<PythonReCharacterClassItem>.Create(Storage.Arena.Budget);
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
        PythonReCharacterClassItem[] ExpandClassItems(Utf8ConstructionBuffer<PythonReCharacterClassItem> classItems, PythonReCompileOptions scopeOptions)
        {
            if ((scopeOptions & PythonReCompileOptions.IgnoreCase) == 0)
                return Storage.Arena.Take(classItems, true);
            using var scalars = PythonReScalarSet.Create(Storage.Arena.Budget);
            using var others = Utf8ConstructionBuffer<PythonReCharacterClassItem>.Create(Storage.Arena.Budget);
            for (var index = 0; index < classItems.Count; index++)
            {
                Utf8ConstructionScope.ChargeWork(1);
                switch (classItems[index])
                {
                    case PythonReCharacterClassLiteralItem literal:
                        AddExpandedScalar(literal.Scalar);
                        break;
                    case PythonReCharacterClassRangeItem range:
                        for (var scalar = range.StartScalar; ; scalar++)
                        {
                            Utf8ConstructionScope.ChargeWork(1);
                            AddExpandedScalar(scalar);
                            if (scalar == range.EndScalar) break;
                        }
                        break;
                    default:
                        others.Add(classItems[index]);
                        break;
                }
            }
            using var rebuilt = Utf8ConstructionBuffer<PythonReCharacterClassItem>.Create(Storage.Arena.Budget);
            var runStart = -1;
            var runEnd = -1;
            foreach (var scalar in scalars)
            {
                Utf8ConstructionScope.ChargeWork(1);
                if (runStart < 0) runStart = runEnd = scalar;
                else if (scalar == runEnd + 1 && (IsAsciiMode(scopeOptions) || scalar != 0x10000)) runEnd = scalar;
                else
                {
                    rebuilt.Add(runStart == runEnd ? PythonReAstFactory.ClassLiteral(runStart) : PythonReAstFactory.ClassRange(runStart, runEnd));
                    runStart = runEnd = scalar;
                }
            }
            if (runStart >= 0)
                rebuilt.Add(runStart == runEnd ? PythonReAstFactory.ClassLiteral(runStart) : PythonReAstFactory.ClassRange(runStart, runEnd));
            for (var index = 0; index < others.Count; index++)
            {
                Utf8ConstructionScope.ChargeWork(1);
                rebuilt.Add(others[index]);
            }
            return Storage.Arena.Take(rebuilt.Count == 0 ? classItems : rebuilt, true);

            void AddExpandedScalar(int scalar)
            {
                scalars.Add(scalar);
                if (IsAsciiMode(scopeOptions))
                {
                    if (scalar is >= 'a' and <= 'z') scalars.Add(scalar - 32);
                    else if (scalar is >= 'A' and <= 'Z') scalars.Add(scalar + 32);
                }
                else
                    foreach (var partner in PythonReScalarCaseData.LiteralPartners(scalar))
                    {
                        Utf8ConstructionScope.ChargeWork(1);
                        scalars.Add(partner);
                    }
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
                PythonReAstFactory.Raw(Storage.Arena.Budget.CopyText(_pattern.AsSpan(escapeStart, 2)), PythonReRawKind.Escape),
            'x' => CreateEscapedLiteralNode(ParseHexEscapeScalarValue(escapeStart, 2, 'x'), options),
            'u' => CreateEscapedLiteralNode(ParseHexEscapeScalarValue(escapeStart, 4, 'u'), options),
            'U' => CreateEscapedLiteralNode(ParseUnicodeScalarEscapeIntValue(escapeStart), options),
            'N' => throw new PythonRePatternException(@"\N{...} named Unicode escapes are intentionally unsupported in PythonRe to avoid a large embedded Unicode-name table.", escapeStart),
            '0' => CreateEscapedLiteralNode(ParseOctalEscapeIntValue(escapeStart, ch), options),
            >= '1' and <= '9' => ParseNumericEscapeOrBackreference(ch, escapeStart, options),
            _ when PatternBadEscapeLetters.IndexOf(ch) >= 0 => throw new PythonRePatternException($@"bad escape \{ch}", escapeStart),
            _ => PythonReAstFactory.Raw(Storage.Arena.Budget.CopyText(_pattern.AsSpan(escapeStart, 2)), PythonReRawKind.Escape),
        };
    }

    // Exact scalar classes encode all profile folding before core lowering.
    // A scoped ASCII class must stay exact against non-ASCII fold partners.
    private PythonReNode? ExpandIgnoreCaseScalar(int scalar, PythonReCompileOptions options)
    {
        if ((options & PythonReCompileOptions.IgnoreCase) == 0) return null;
        if (IsAsciiMode(options))
        {
            if (scalar is not (>= 'a' and <= 'z') and not (>= 'A' and <= 'Z')) return null;
            var pair = Storage.Arena.Allocate<PythonReCharacterClassItem>(2, true);
            pair[0] = PythonReAstFactory.ClassLiteral(scalar);
            pair[1] = PythonReAstFactory.ClassLiteral(scalar is >= 'a' and <= 'z' ? scalar - 32 : scalar + 32);
            return PythonReAstFactory.CharacterClass(false, pair);
        }
        var partners = PythonReScalarCaseData.LiteralPartners(scalar);
        if (partners.IsEmpty) return null;
        using var members = PythonReScalarSet.Create(Storage.Arena.Budget);
        foreach (var partner in partners)
        {
            Utf8ConstructionScope.ChargeWork(1);
            members.Add(partner);
        }
        using var items = Utf8ConstructionBuffer<PythonReCharacterClassItem>.Create(Storage.Arena.Budget);
        foreach (var member in members)
        {
            Utf8ConstructionScope.ChargeWork(1);
            items.Add(PythonReAstFactory.ClassLiteral(member));
        }
        return PythonReAstFactory.CharacterClass(false, Storage.Arena.Take(items, true));
    }

    private PythonReNode CreateEscapedLiteralNode(int scalar, PythonReCompileOptions options)
    {
        if (scalar is >= 0xd800 and <= 0xdfff) return PythonReAstFactory.Literal((char)scalar);
        return ExpandIgnoreCaseScalar(scalar, options)
            ?? PythonReAstFactory.Raw(Utf8ConstructionText.EscapeScalar(scalar), PythonReRawKind.Escape);
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
                return CreateEscapedLiteralNode(ParseOctalEscapeIntValue(escapeStart, firstDigit, secondDigit, thirdDigit), options);
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

        if (Storage.IsOpen(value))
        {
            throw new PythonRePatternException("cannot refer to an open group", escapeStart);
        }

        if (value > _groupCount)
        {
            throw new PythonRePatternException($"invalid group reference {value}", escapeStart + 1);
        }

        CheckLookbehindReference(value, _position);
        return PythonReAstFactory.NumericReference(value, BackreferenceLower(options));
    }

    private void CheckLookbehindReference(int number, int position)
    {
        if (_lookbehindGroupStart == 0) return;
        if (number > _groupCount || Storage.IsOpen(number))
            throw new PythonRePatternException("cannot refer to an open group", position);
        if (number >= _lookbehindGroupStart)
            throw new PythonRePatternException("cannot refer to group defined in the same lookbehind subpattern", position);
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

    private int ParseHexEscapeScalarValue(int escapeStart, int digits, char kind)
    {
        var hex = ReadHexDigits(escapeStart, digits, kind);
        Utf8ConstructionScope.ChargeWork((ulong)hex.Length);
        return int.Parse(hex, System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture);
    }

    private int ParseUnicodeScalarEscapeIntValue(int escapeStart)
    {
        var hex = ReadHexDigits(escapeStart, 8, 'U');
        Utf8ConstructionScope.ChargeWork((ulong)hex.Length);
        var scalar = int.Parse(hex, System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture);
        if ((uint)scalar > 0x10FFFF)
            throw new PythonRePatternException($@"bad escape \U{Storage.Arena.Budget.CopyText(hex)}", escapeStart);
        return scalar;
    }

    private ReadOnlySpan<char> ReadHexDigits(int escapeStart, int digits, char kind)
    {
        var start = _position;
        while (_position - start < digits && !IsAtEnd && HexDigits.IndexOf(Current) >= 0)
        {
            Utf8ConstructionScope.ChargeWork(1);
            _position++;
        }
        var hex = _pattern.AsSpan(start, _position - start);
        if (hex.Length != digits)
            throw new PythonRePatternException($@"incomplete escape \{kind}{Storage.Arena.Budget.CopyText(hex)}", escapeStart);
        return hex;
    }

    private int ParseOctalEscapeIntValue(int escapeStart, char firstDigit)
    {
        Span<char> octal = stackalloc char[3];
        octal[0] = firstDigit;
        Utf8ConstructionScope.ChargeWork(1);
        var count = 1;
        var value = firstDigit - '0';
        while (count < 3 && !IsAtEnd && IsOctalDigit(Current))
        {
            Utf8ConstructionScope.ChargeWork(1);
            var digit = Consume();
            octal[count++] = digit;
            value = value * 8 + digit - '0';
        }
        if (value > 0xFF)
            throw new PythonRePatternException($@"octal escape value \{Storage.Arena.Budget.CopyText(octal[..count])} outside of range 0-0o377", escapeStart);
        return value;
    }

    private int ParseOctalEscapeIntValue(int escapeStart, char firstDigit, char secondDigit, char thirdDigit)
    {
        Utf8ConstructionScope.ChargeWork(3);
        var value = ((firstDigit - '0') * 8 + secondDigit - '0') * 8 + thirdDigit - '0';
        if (value > 0xFF)
            throw new PythonRePatternException($@"octal escape value \{Storage.Arena.Budget.CopyText(_pattern.AsSpan(_position - 3, 3))} outside of range 0-0o377", escapeStart);
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

        var name = Storage.Arena.Budget.CopyText(_pattern.AsSpan(start, _position - start));
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

    private bool IsBraceQuantifier()
    {
        if (IsAtEnd || Current != '{') return false;
        // Only complete repeat syntax reserves a brace. Peek without allocating
        // or rewinding work; malformed/unfinished text remains literal Python syntax.
        Utf8ConstructionScope.ChargeWork(1);
        var index = _position + 1;
        var minimumStart = index;
        while (index < _pattern.Length && char.IsAsciiDigit(_pattern[index]))
        {
            Utf8ConstructionScope.ChargeWork(1);
            index++;
        }
        if (index < _pattern.Length && _pattern[index] == ',')
        {
            index++;
            while (index < _pattern.Length && char.IsAsciiDigit(_pattern[index]))
            {
                Utf8ConstructionScope.ChargeWork(1);
                index++;
            }
        }
        else if (index == minimumStart) return false;
        return index < _pattern.Length && _pattern[index] == '}';
    }

    private uint ParseNumber()
    {
        if (!TryPeekDigit())
        {
            throw Error("expected number");
        }

        uint value = 0;
        while (TryPeekDigit())
        {
            Utf8ConstructionScope.ChargeWork(1);
            var digit = (uint)(Current - '0');
            // Python reserves UINT_MAX for infinity; a finite bound cannot use it.
            if (value > (uint.MaxValue - 1 - digit) / 10)
                throw Error("the repetition number is too large");
            value = value * 10 + digit;
            Consume();
        }

        return value;
    }

    private bool TryPeekDigit() => !IsAtEnd && char.IsAsciiDigit(Current);

    private bool IsAtEnd => _position >= _pattern.Length;

    private char Current => _pattern[_position];

    private char Consume() => _pattern[_position++];

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
    PythonReNode?[] GroupBodies)
{
    internal bool TryGetGroupBody(int number, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out PythonReNode? body)
    {
        Utf8ConstructionScope.ChargeWork(1);
        body = number > 0 && number <= GroupBodies.Length ? GroupBodies[number - 1] : null;
        return body is not null;
    }
}

internal enum FlagParseContext
{
    Added,
    Removed,
}

internal readonly record struct PythonReParsedCharacterClassItem(PythonReCharacterClassItem Item, int Position);
