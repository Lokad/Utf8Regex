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
    private readonly Utf8ConstructionBudget _budget;
    private int _position;
    private int _groups;

    private PosixParser(string pattern, PosixCompileOptions options, Utf8ConstructionBudget budget)
    { _pattern = pattern; _newline = (options & PosixCompileOptions.Newline) != 0; _budget = budget; }

    internal static Utf8SliceNode Parse(string pattern, PosixCompileOptions options, out int groupCount)
    {
        ArgumentNullException.ThrowIfNull(pattern);
        if ((options & ~PosixCompileOptions.Newline) != 0) throw new ArgumentOutOfRangeException(nameof(options));
        using var construction = Utf8ConstructionScope.Enter();
        var parser = new PosixParser(pattern, options, construction.Budget);
        var root = parser.ParseExpression();
        groupCount = parser._groups;
        construction.Complete();
        return root;
    }

    private enum Operator : byte { Open, Alternate, Concat }
    private readonly record struct Pending(Operator Kind, int Group);

    private Utf8SliceNode ParseExpression()
    {
        using var nodes = Utf8ConstructionBuffer<Utf8SliceNode>.Create(_budget);
        using var operators = Utf8ConstructionBuffer<Pending>.Create(_budget);
        var needsAtom = true;
        var canRepeat = false;
        var opened = 0;
        try
        {
            while (_position < _pattern.Length)
            {
                _budget.ChargeWork(1);
                var next = _pattern[_position];
                if (next == ')')
                {
                    _position++;
                    if (opened == 0) throw Error("Unmatched closing parenthesis");
                    if (needsAtom) nodes.Push(NewEmpty());
                    while (operators.Peek().Kind != Operator.Open) Reduce(nodes, operators.Pop());
                    var group = operators.Pop().Group;
                    var child = nodes.Pop();
                    nodes.Push(_budget.AllocateObject((group, child), static x => new Utf8SliceNode.CaptureNode(x.group, x.child)));
                    opened--; _budget.LeaveDepth();
                    needsAtom = false; canRepeat = true;
                    continue;
                }
                if (next == '|')
                {
                    _position++;
                    if (needsAtom) nodes.Push(NewEmpty());
                    PushOperator(nodes, operators, Operator.Alternate);
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
                        case '*': minimum = 0; maximum = long.MaxValue; _position++; break;
                        case '+': minimum = 1; maximum = long.MaxValue; _position++; break;
                        case '?': minimum = 0; maximum = 1; _position++; break;
                        default: (minimum, maximum) = ReadInterval(); break;
                    }
                    nodes.Push(_budget.AllocateObject((atom, minimum, maximum),
                        static x => new Utf8SliceNode.RepeatNode(x.atom, x.minimum, x.maximum, false)));
                    canRepeat = false;
                    continue;
                }
                if (!needsAtom) PushOperator(nodes, operators, Operator.Concat);
                if (next == '(')
                {
                    _position++;
                    if (_position < _pattern.Length && _pattern[_position] == '?') throw Error("Extended groups are unsupported");
                    _budget.EnterDepth(); opened++;
                    operators.Push(new(Operator.Open, checked(++_groups)));
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
                        if (escaped > 127 || !".^$[]()|*+?{}\\".Contains((char)escaped)) throw Error("Unsupported escape");
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
        _budget.ChargeWork(1);
        var right = nodes.Pop(); var left = nodes.Pop();
        if (operation.Kind == Operator.Concat)
        {
            var children = _budget.AllocateArray<Utf8SliceNode>(2, false);
            children[0] = left; children[1] = right;
            nodes.Push(_budget.AllocateObject(children, static x => new Utf8SliceNode.ConcatNode(x)));
        }
        else
        {
            var branches = _budget.AllocateArray<Utf8SliceNode[]>(2, false);
            branches[0] = _budget.AllocateArray<Utf8SliceNode>(1, false); branches[0][0] = left;
            branches[1] = _budget.AllocateArray<Utf8SliceNode>(1, false); branches[1][0] = right;
            nodes.Push(_budget.AllocateObject(branches, static x => new Utf8SliceNode.AlternationNode(x)));
        }
    }

    private (long Minimum, long Maximum) ReadInterval()
    {
        _position++;
        var minimum = ReadNumber();
        long maximum = minimum;
        if (Take(',')) maximum = _position < _pattern.Length && _pattern[_position] == '}' ? long.MaxValue : ReadNumber();
        if (!Take('}') || minimum > maximum) throw Error("Invalid repetition interval");
        return (minimum, maximum);
    }

    private int ReadNumber()
    {
        if (_position == _pattern.Length || !char.IsAsciiDigit(_pattern[_position])) throw Error("Missing interval number");
        var value = 0;
        while (_position < _pattern.Length && char.IsAsciiDigit(_pattern[_position]))
        {
            _budget.ChargeWork(1);
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
            _budget.ChargeWork(1);
            var isFirst = first;
            first = false;
            if (_pattern[_position] == '-' && !isFirst &&
                _position + 1 < _pattern.Length && _pattern[_position + 1] != ']')
                throw Error("A literal hyphen must be first or last");
            if (_pattern[_position] == '[' && _position + 1 < _pattern.Length && _pattern[_position + 1] is ':' or '.' or '=')
            {
                if (_pattern[_position + 1] != ':') throw Error("Locale collation is unsupported");
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
                if (_pattern[_position] == '[' && _position + 1 < _pattern.Length && _pattern[_position + 1] is ':' or '.' or '=')
                    throw Error("A named class cannot be a range endpoint");
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
            _budget.ChargeWork(1);
            if (merged.Count != 0 && range.Start <= merged[merged.Count - 1])
                merged[merged.Count - 1] = Math.Max(merged[merged.Count - 1], range.End);
            else { merged.Add(range.Start); merged.Add(range.End); }
        }
        var table = Utf8SliceCategoryTable.ForScalarRanges(merged.Take());
        var members = _budget.AllocateArray<Utf8SliceNode.CategorySetMember>(1, false);
        members[0] = Utf8SliceNode.CategorySetMember.Create(table, false);
        var mask = _budget.AllocateArray<byte>(16, false);
        return _budget.AllocateObject((mask, negated, members),
            static x => new Utf8SliceNode.CategorySetNode(x.mask, x.negated, [], x.members, false));
    }

    private void ReadNamedClass(Utf8ConstructionBuffer<(int Start, int End)> ranges)
    {
        _position += 2;
        var start = _position;
        while (_position < _pattern.Length && _pattern[_position] != ':') { _budget.ChargeWork(1); _position++; }
        var name = _pattern.AsSpan(start, _position - start);
        if (!Take(':') || !Take(']')) throw Error("Unclosed named class");
        if (!(name is "alnum" or "alpha" or "blank" or "cntrl" or "digit" or "graph" or "lower" or
            "print" or "punct" or "space" or "upper" or "xdigit")) throw Error("Unknown named class");
        var run = -1;
        for (var value = 0; value <= 128; value++)
        {
            _budget.ChargeWork(1);
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
        _budget.ChargeWork(1);
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
    private Utf8SliceNode NewLiteral(int value) => _budget.AllocateObject(value, static x => new Utf8SliceNode.LiteralNode(x, false));
    private ArgumentException Error(string reason) => new($"{reason} at UTF-16 pattern offset {_position}.", "pattern");
}
