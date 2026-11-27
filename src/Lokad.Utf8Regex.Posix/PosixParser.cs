using System.Buffers;
using System.Text;
using Lokad.Utf8Regex.Internal;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Posix;

// ERE admission produces the existing scalar tree. Operator/group stacks are
// explicit so nested patterns consume admitted storage instead of the CLR stack.
internal sealed class PosixParser
{
    private readonly string _pattern;
    private readonly bool _newline;
    private readonly bool _ignoreCase;
    private readonly Dialect _dialect;
    private readonly Utf8ConstructionBudget _budget;
    private int _position;
    private int _groups;
    private int _closedReferenceSlots;
    private int _branchEntrySlots;
    private int _previousAlternativeSlots;
    private bool _hasBackreferences;
    private const int ReferenceSlotBits = 10;
    private const int ReferenceSlotMask = (1 << ReferenceSlotBits) - 1;

    private PosixParser(string pattern, PosixCompileOptions options, Dialect dialect, Utf8ConstructionBudget budget)
    {
        _pattern = pattern; _newline = (options & PosixCompileOptions.Newline) != 0;
        _ignoreCase = (options & PosixCompileOptions.IgnoreCase) != 0;
        _dialect = dialect; _budget = budget;
    }

    internal enum Dialect : byte { Extended, GnuBasic, GnuExtended }

    internal static (Utf8SliceNode Root, int GroupCount, bool HasBackreferences) Parse(string pattern, PosixCompileOptions options,
        Dialect dialect = Dialect.Extended)
    {
        ArgumentNullException.ThrowIfNull(pattern);
        if ((options & ~(PosixCompileOptions.Newline | PosixCompileOptions.IgnoreCase)) != 0) throw new ArgumentOutOfRangeException(nameof(options));
        using var construction = Utf8ConstructionScope.Enter();
        var parser = new PosixParser(pattern, options, dialect, construction.Budget);
        var root = parser.ParseExpression();
        construction.Complete();
        return (root, parser._groups, parser._hasBackreferences);
    }

    private enum Operator : byte { Open, Alternate, Concat }
    private readonly record struct Pending(Operator Kind, int Group, int ReferenceScope = 0);

    private Utf8SliceNode ParseExpression()
    {
        using var nodes = Utf8ConstructionBuffer<Utf8SliceNode>.Create(_budget);
        using var operators = Utf8ConstructionBuffer<Pending>.Create(_budget);
        var needsAtom = true;
        var canRepeat = false;
        var afterBeginningAnchor = false;
        var opened = 0;
        try
        {
            while (_position < _pattern.Length)
            {
                _budget.Checkpoint();
                var next = SyntaxAt(_position, out var tokenWidth);
                if (_dialect == Dialect.GnuBasic &&
                    (next == '^' && !needsAtom || next == '$' && _position + 1 < _pattern.Length &&
                        SyntaxAt(_position + 1, out _) is not ('|' or ')') ||
                     next == '*' && (needsAtom || afterBeginningAnchor)))
                    next = '\0';
                if (_dialect == Dialect.GnuExtended && next == ')' && opened == 0)
                    next = '\0';
                afterBeginningAnchor = next == '^';
                if (next == ')')
                {
                    _position += tokenWidth;
                    if (opened == 0) throw Error("Unmatched closing parenthesis");
                    if (needsAtom) nodes.Push(NewEmpty());
                    while (operators.Peek().Kind != Operator.Open) Reduce(nodes, operators.Pop());
                    var open = operators.Pop();
                    var group = open.Group;
                    if (_dialect != Dialect.Extended)
                    {
                        _closedReferenceSlots |= _previousAlternativeSlots;
                        if (group <= 9) _closedReferenceSlots |= 1 << group;
                        _branchEntrySlots = open.ReferenceScope & ReferenceSlotMask;
                        _previousAlternativeSlots = open.ReferenceScope >> ReferenceSlotBits;
                    }
                    var child = nodes.Pop();
                    nodes.Push(_budget.AllocateObject((group, child), static x => new Utf8SliceNode.CaptureNode(x.group, x.child)));
                    opened--; _budget.LeaveDepth();
                    needsAtom = false; canRepeat = true;
                    continue;
                }
                if (next == '|')
                {
                    _position += tokenWidth;
                    if (needsAtom) nodes.Push(NewEmpty());
                    PushOperator(nodes, operators, Operator.Alternate);
                    _previousAlternativeSlots |= _closedReferenceSlots;
                    _closedReferenceSlots = _branchEntrySlots;
                    needsAtom = true; canRepeat = false;
                    continue;
                }
                if (next is '*' or '+' or '?' or '{')
                {
                    if (needsAtom || !canRepeat) throw Error("Repetition has no repeatable atom");
                    var atom = nodes.Pop();
                    long minimum, maximum;
                    switch (next)
                    {
                        case '*': minimum = 0; maximum = long.MaxValue; _position += tokenWidth; break;
                        case '+': minimum = 1; maximum = long.MaxValue; _position += tokenWidth; break;
                        case '?': minimum = 0; maximum = 1; _position += tokenWidth; break;
                        default: (minimum, maximum) = ReadInterval(tokenWidth); break;
                    }
                    if (_position < _pattern.Length && SyntaxAt(_position, out _) is '?' or '+')
                        throw Unsupported("Lazy and possessive repetition are unsupported");
                    nodes.Push(_budget.AllocateObject((atom, minimum, maximum),
                        static x => new Utf8SliceNode.RepeatNode(x.atom, x.minimum, x.maximum, false)));
                    canRepeat = false;
                    continue;
                }
                if (!needsAtom) PushOperator(nodes, operators, Operator.Concat);
                if (next == '(')
                {
                    _position += tokenWidth;
                    if (_dialect != Dialect.GnuBasic && _position < _pattern.Length && _pattern[_position] == '?')
                        throw Unsupported("Extended groups are unsupported");
                    _budget.EnterDepth(); opened++;
                    operators.Push(new(Operator.Open, checked(++_groups),
                        _branchEntrySlots | (_previousAlternativeSlots << ReferenceSlotBits)));
                    // Sibling arms start with the group's incoming scope. A
                    // later concatenated term can refer to either closed arm.
                    _branchEntrySlots = _closedReferenceSlots;
                    _previousAlternativeSlots = 0;
                    needsAtom = true; canRepeat = false;
                    continue;
                }
                Utf8SliceNode node;
                switch (next)
                {
                    case '[': node = ReadBracket(); break;
                    case '.': _position++; node = _budget.AllocateObject(!_newline, static x => new Utf8SliceNode.DotNode(x)); break;
                    case '^':
                        _position++;
                        node = _budget.AllocateObject(_newline, static x => new Utf8SliceNode.AnchorNode(Utf8SliceAnchorKind.Beginning, x));
                        break;
                    case '$':
                        _position++;
                        node = _budget.AllocateObject(_newline, static x => new Utf8SliceNode.AnchorNode(x ? Utf8SliceAnchorKind.End : Utf8SliceAnchorKind.VeryEnd, x));
                        break;
                    case '}': throw Error("Unescaped closing interval brace");
                    case '\\':
                        _position++;
                        var escaped = ReadScalar();
                        if (_dialect != Dialect.Extended && escaped is >= '1' and <= '9')
                        {
                            var slot = escaped - '0';
                            if ((_closedReferenceSlots & (1 << slot)) == 0) throw Error("Invalid backreference");
                            _hasBackreferences = true;
                            node = _budget.AllocateObject((slot, Fold: _ignoreCase),
                                static x => new Utf8SliceNode.BackrefNode(x.slot, x.Fold, false));
                            break;
                        }
                        if (escaped > 127 || !".^$[]()|*+?{}\\".Contains((char)escaped)) throw Unsupported("Unsupported escape");
                        node = NewLiteral(escaped);
                        break;
                    default: node = NewLiteral(ReadScalar()); break;
                }
                nodes.Push(node);
                needsAtom = false; canRepeat = next is not ('^' or '$');
            }
            if (opened != 0) throw Error("Unclosed group");
            if (needsAtom) nodes.Push(NewEmpty());
            while (operators.Count != 0) Reduce(nodes, operators.Pop());
            if (nodes.Count != 1) throw new InvalidOperationException("ERE operator stack did not reduce to one expression.");
            return nodes.Pop();
        }
        finally { while (opened-- > 0) _budget.LeaveDepth(); }
    }

    // GNU BRE switches these operators to their escaped spellings. A zero
    // token leaves the original code unit to the normal literal reader.
    private char SyntaxAt(int position, out int width)
    {
        var current = _pattern[position];
        width = 1;
        if (_dialect != Dialect.GnuBasic) return current;
        if (current == '\\' && position + 1 < _pattern.Length && _pattern[position + 1] is '(' or ')' or '|' or '+' or '?' or '{' or '}')
        {
            width = 2;
            return _pattern[position + 1];
        }
        return current is '(' or ')' or '|' or '+' or '?' or '{' or '}' ? '\0' : current;
    }

    private void PushOperator(Utf8ConstructionBuffer<Utf8SliceNode> nodes,
        Utf8ConstructionBuffer<Pending> operators, Operator kind)
    {
        // Concatenation's semantic hierarchy is right-associated: in abc, a
        // takes priority over the remaining bc. Left-associated synthetic ab
        // would incorrectly outrank a in cases such as a?(ab)?b?.
        while (operators.Count > 0 && operators.Peek().Kind != Operator.Open &&
            (operators.Peek().Kind > kind || kind == Operator.Alternate && operators.Peek().Kind == kind))
            Reduce(nodes, operators.Pop());
        operators.Push(new(kind, 0));
    }

    private void Reduce(Utf8ConstructionBuffer<Utf8SliceNode> nodes, Pending operation)
    {
        _budget.Checkpoint();
        var right = nodes.Pop(); var left = nodes.Pop();
        if (operation.Kind == Operator.Concat)
        {
            var children = _budget.AllocateArray<Utf8SliceNode>(2, false);
            children[0] = left; children[1] = right;
            nodes.Push(_budget.AllocateObject(children, static x => new Utf8SliceNode.ConcatNode(x)));
        }
        else
        {
            var branches = _budget.AllocateArray<Utf8SliceNode>(2, false);
            branches[0] = left; branches[1] = right;
            nodes.Push(_budget.AllocateObject(branches, static x => new Utf8SliceNode.AlternationNode(x)));
        }
    }

    private (long Minimum, long Maximum) ReadInterval(int openingWidth)
    {
        _position += openingWidth;
        var minimum = ReadNumber();
        long maximum = minimum;
        if (Take(',')) maximum = _position < _pattern.Length && SyntaxAt(_position, out _) == '}' ? long.MaxValue : ReadNumber();
        if (_position == _pattern.Length || SyntaxAt(_position, out var closingWidth) != '}')
            throw Error("Invalid repetition interval");
        _position += closingWidth;
        if (minimum > maximum) throw Error("Invalid repetition interval");
        return (minimum, maximum);
    }

    private int ReadNumber()
    {
        if (_position == _pattern.Length || !char.IsAsciiDigit(_pattern[_position])) throw Error("Missing interval number");
        var value = 0;
        while (_position < _pattern.Length && char.IsAsciiDigit(_pattern[_position]))
        {
            _budget.Checkpoint();
            value = value * 10 + _pattern[_position++] - '0';
            if (value > 255) throw Error("An interval number exceeds 255");
        }
        return value;
    }

    private Utf8SliceNode ReadBracket()
    {
        _position++;
        var negated = Take('^');
        using var ranges = Utf8ConstructionBuffer<(int Start, int End)>.Create(_budget);
        var first = true;
        while (_position < _pattern.Length && (_pattern[_position] != ']' || first))
        {
            _budget.Checkpoint();
            var isFirst = first;
            first = false;
            if (_pattern[_position] == '-' && !isFirst &&
                _position + 1 < _pattern.Length && _pattern[_position + 1] != ']')
                throw Error("A literal hyphen must be first or last");
            if (_pattern[_position] == '[' && _position + 1 < _pattern.Length && _pattern[_position + 1] is ':' or '.' or '=')
            {
                if (_pattern[_position + 1] != ':') throw Unsupported("Locale collation is unsupported");
                ReadNamedClass(ranges);
                if (_position + 1 < _pattern.Length && _pattern[_position] == '-' && _pattern[_position + 1] != ']')
                    throw Error("A named class cannot be a range endpoint");
                continue;
            }
            var start = ReadScalar();
            var end = start;
            if (!(isFirst && start == '-') && _position + 1 < _pattern.Length &&
                _pattern[_position] == '-' && _pattern[_position + 1] != ']')
            {
                _position++;
                if (_pattern[_position] == '[' && _position + 1 < _pattern.Length)
                {
                    if (_pattern[_position + 1] is '.' or '=') throw Unsupported("Locale collation is unsupported");
                    if (_pattern[_position + 1] == ':') throw Error("A named class cannot be a range endpoint");
                }
                end = ReadScalar();
                if (end < start) throw Error("Descending scalar range");
            }
            ranges.Add((start, checked(end + 1)));
        }
        if (!Take(']')) throw Error("Unclosed bracket expression");
        // For REG_NEWLINE a negated bracket must reject LF even if its listed
        // positive set does not contain LF. Add LF before applying negation.
        if (negated && _newline) ranges.Add(('\n', '\n' + 1));
        ranges.Sort(static (left, right) => left.Start.CompareTo(right.Start));
        using var merged = Utf8ConstructionBuffer<int>.Create(_budget);
        foreach (var range in ranges.Span)
        {
            _budget.Checkpoint();
            if (merged.Count != 0 && range.Start <= merged[merged.Count - 1])
                merged[merged.Count - 1] = Math.Max(merged[merged.Count - 1], range.End);
            else { merged.Add(range.Start); merged.Add(range.End); }
        }
        var mask = _budget.AllocateArray<byte>(16, false);
        var retained = 0;
        var fold = false;
        for (var index = 0; index < merged.Count; index += 2)
        {
            _budget.Checkpoint();
            var start = merged[index]; var end = merged[index + 1];
            // Invariant folding changes only ASCII letters in an ASCII-only
            // bracket. Wider ranges retain their scalar case comparison.
            if (_ignoreCase && !fold)
                fold = end > 128 || start <= 'Z' && end > 'A' || start <= 'z' && end > 'a';
            for (var value = start; value < Math.Min(end, 128); value++)
            {
                _budget.Checkpoint();
                mask[value >> 3] |= (byte)(1 << (value & 7));
            }
            if (end > 128)
            {
                merged[retained++] = Math.Max(start, 128);
                merged[retained++] = end;
            }
        }
        merged.Truncate(retained);
        // Use the existing ASCII representation; keep only non-ASCII ranges in
        // the table. ASCII-only brackets need no category-table descriptor.
        Utf8SliceNode.CategorySetMember[] members = [];
        if (merged.Count != 0)
        {
            var table = Utf8SliceCategoryTable.ForScalarRanges(merged.Take());
            members = _budget.AllocateArray<Utf8SliceNode.CategorySetMember>(1, false);
            members[0] = new Utf8SliceNode.CategorySetMember(table, false);
        }
        return _budget.AllocateObject((mask, negated, members, fold),
            static x => new Utf8SliceNode.CategorySetNode(x.mask, x.negated, [], x.members, x.fold));
    }

    private void ReadNamedClass(Utf8ConstructionBuffer<(int Start, int End)> ranges)
    {
        _position += 2;
        var start = _position;
        while (_position < _pattern.Length && _pattern[_position] != ':') { _budget.Checkpoint(); _position++; }
        var name = _pattern.AsSpan(start, _position - start);
        if (!Take(':') || !Take(']')) throw Error("Unclosed named class");
        if (!(name is "alnum" or "alpha" or "blank" or "cntrl" or "digit" or "graph" or "lower" or
            "print" or "punct" or "space" or "upper" or "xdigit")) throw Error("Unknown named class");
        var run = -1;
        for (var value = 0; value <= 128; value++)
        {
            _budget.Checkpoint();
            var hit = value < 128 && NamedClassContains(name, value);
            if (hit && run < 0) run = value;
            if (!hit && run >= 0) { ranges.Add((run, value)); run = -1; }
        }
    }

    private static bool NamedClassContains(ReadOnlySpan<char> name, int value)
    {
        var upper = value is >= 'A' and <= 'Z'; var lower = value is >= 'a' and <= 'z';
        var digit = value is >= '0' and <= '9'; var graph = value is >= 0x21 and <= 0x7e;
        return name switch
        {
            "alnum" => upper || lower || digit, "alpha" => upper || lower,
            "blank" => value is '\t' or ' ', "cntrl" => value < 0x20 || value == 0x7f,
            "digit" => digit, "graph" => graph, "lower" => lower,
            "print" => value is >= 0x20 and <= 0x7e, "punct" => graph && !upper && !lower && !digit,
            "space" => value is >= 9 and <= 13 or ' ', "upper" => upper,
            "xdigit" => digit || value is >= 'A' and <= 'F' or >= 'a' and <= 'f',
            _ => false,
        };
    }

    private int ReadScalar()
    {
        _budget.Checkpoint();
        if (_position >= _pattern.Length || Rune.DecodeFromUtf16(_pattern.AsSpan(_position), out var scalar, out var width) != OperationStatus.Done)
            throw Error("Expected a well-formed Unicode scalar");
        _position += width;
        return scalar.Value;
    }

    private bool Take(char value)
    {
        if (_position == _pattern.Length || _pattern[_position] != value) return false;
        _position++; return true;
    }

    private Utf8SliceNode NewEmpty() => _budget.AllocateObject(0, static _ => new Utf8SliceNode.EmptyNode());
    private Utf8SliceNode NewLiteral(int value) => _budget.AllocateObject((value, _ignoreCase),
        static x => new Utf8SliceNode.LiteralNode(x.value, x._ignoreCase));
    private ArgumentException Error(string reason) => new(Diagnostic(reason), "pattern");
    private PosixUnsupportedSyntaxException Unsupported(string reason) => new(Diagnostic(reason));
    private string Diagnostic(string reason) => $"{reason} at UTF-16 pattern offset {_position}.";
}
