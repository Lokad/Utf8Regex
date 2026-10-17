using Lokad.Utf8Regex.Internal.FrontEnd;

namespace Lokad.Utf8Regex.PythonRe;

internal sealed partial class PythonReParser
{
    private enum ParseFrameKind { Alternation, Sequence, Group }

    // Suspended state lives in an admitted array, with its actual element stride.
    // Referenced collections and AST/text payloads have independent loans.
    private struct ParseFrame(ParseFrameKind kind, PythonReCompileOptions options)
    {
        internal readonly ParseFrameKind Kind = kind;
        internal readonly PythonReCompileOptions Options = options;
        internal int Stage;
        internal Utf8ConstructionBuffer<PythonReNode>? Nodes;
        internal int GroupStart;
        internal int GroupNumber;
        internal int PreviousLookbehindGroupStart;
        internal PythonReGroupKind GroupKind;
        internal string? GroupName;
        internal PythonReCompileOptions AddOptions;
        internal PythonReCompileOptions RemoveOptions;
        internal PythonReConditionalReference Condition;
        internal PythonReNode? YesBranch;
    }

    private void ParseFrames(PythonReCompileOptions options, Utf8ConstructionBudget budget)
    {
        // Check depth before acquiring even the root stack descriptor.
        budget.EnterDepth();
        var rootDepthHeld = true;
        Utf8ConstructionBuffer<ParseFrame>? frames = null;
        try
        {
            frames = Utf8ConstructionBuffer<ParseFrame>.Create(budget);
            frames.Push(new(ParseFrameKind.Alternation, options));
            rootDepthHeld = false;
            while (frames.Count != 0)
            {
                budget.ChargeWork(1);
                var index = frames.Count - 1;
                var frame = frames[index];
                ParseFrame child;
                bool completed;
                try
                {
                    completed = frame.Kind switch
                    {
                        ParseFrameKind.Alternation => ResumeAlternation(ref frame, out child),
                        ParseFrameKind.Sequence => ResumeSequence(ref frame, out child),
                        _ => ResumeGroup(ref frame, out child),
                    };
                }
                finally { frames[index] = frame; } // Preserve scratch ownership even when a resume throws.
                if (completed)
                {
                    try { frames.Pop().Nodes?.Dispose(); }
                    finally { budget.LeaveDepth(); }
                }
                else
                {
                    budget.EnterDepth();
                    try { frames.Push(child); }
                    catch { budget.LeaveDepth(); throw; }
                }
            }
        }
        finally
        {
            if (rootDepthHeld) budget.LeaveDepth();
            if (frames is not null)
            {
                while (frames.Count != 0)
                {
                    try { frames.Pop().Nodes?.Dispose(); }
                    finally { budget.LeaveDepth(); }
                }
                frames.Dispose();
            }
        }
    }

    private bool ResumeAlternation(ref ParseFrame frame, out ParseFrame child)
    {
        if (frame.Stage == 0)
        {
            _storage ??= PythonReParserStorage.Create(Utf8ConstructionScope.CurrentBudget);
            frame.Nodes = Utf8ConstructionBuffer<PythonReNode>.Create(Storage.Arena.Budget);
            frame.Stage = 1;
            child = new(ParseFrameKind.Sequence, frame.Options);
            return false;
        }
        var branches = frame.Nodes ?? throw new InvalidOperationException("Alternation frame storage is missing.");
        branches.Add(_parsedNode);
        if (TryConsume('|'))
        {
            Utf8ConstructionScope.ChargeWork(1);
            child = new(ParseFrameKind.Sequence, frame.Options);
            return false;
        }
        _parsedNode = branches.Count == 1
            ? branches[0] : PythonReAstFactory.Alternation(Storage.Arena.Take(branches, true));
        child = default;
        return true;
    }

    private bool ResumeSequence(ref ParseFrame frame, out ParseFrame child)
    {
        frame.Nodes ??= Utf8ConstructionBuffer<PythonReNode>.Create(Storage.Arena.Budget);
        var elements = frame.Nodes;
        if (frame.Stage == 1)
        {
            elements.Add(ParseQuantifiedAtom(_parsedNode));
            frame.Stage = 0;
        }
        while (true)
        {
            Utf8ConstructionScope.ChargeWork(1);
            SkipIgnoredPatternTrivia(frame.Options);
            if (IsAtEnd || Current == ')' || Current == '|') break;
            if (TryConsume('('))
            {
                frame.Stage = 1;
                child = new(ParseFrameKind.Group, frame.Options);
                return false;
            }
            elements.Add(ParseQuantifiedAtom(ParseAtom(frame.Options)));
        }
        _parsedNode = elements.Count switch
        {
            0 => PythonReAstFactory.Empty,
            1 => elements[0],
            _ => PythonReAstFactory.Sequence(Storage.Arena.Take(elements, true)),
        };
        child = default;
        return true;
    }

    private bool ResumeGroup(ref ParseFrame frame, out ParseFrame child)
    {
        if (frame.Stage == 0) return StartGroup(ref frame, out child);
        child = default;
        if (frame.Stage == 1)
        {
            var inner = _parsedNode;
            ExpectGroupEnd(frame.GroupStart, "missing ), unterminated subpattern");
            if (frame.GroupNumber != 0) Storage.GroupBodies[frame.GroupNumber - 1] = inner;
            if (frame.GroupKind is PythonReGroupKind.PositiveLookbehind or PythonReGroupKind.NegativeLookbehind)
            {
                _lookbehindGroupStart = frame.PreviousLookbehindGroupStart;
                _hasLookbehind = true;
            }
            _parsedNode = PythonReAstFactory.Group(frame.GroupKind, inner, frame.GroupName,
                frame.AddOptions, frame.RemoveOptions, frame.GroupNumber);
            return true;
        }
        if (frame.Stage == 2)
        {
            frame.YesBranch = _parsedNode;
            if (TryConsume('|'))
            {
                frame.Stage = 3;
                child = new(ParseFrameKind.Sequence, frame.Options);
                return false;
            }
        }
        else if (!IsAtEnd && Current == '|')
            throw new PythonRePatternException("conditional backref with more than two branches", _position);
        ExpectGroupEnd(frame.GroupStart, "missing ), unterminated subpattern");
        _parsedNode = PythonReAstFactory.Conditional(frame.Condition,
            frame.YesBranch ?? throw new InvalidOperationException("Conditional frame branch is missing."),
            frame.Stage == 3 ? _parsedNode : null);
        return true;
    }

    private bool StartGroup(ref ParseFrame frame, out ParseFrame child)
    {
        frame.GroupStart = _position - 1;
        var bodyOptions = frame.Options;
        if (!TryConsume('?'))
        {
            frame.GroupNumber = ++_groupCount;
            Storage.GroupBodies.Add(null);
            frame.GroupKind = PythonReGroupKind.Capturing;
        }
        else
        {
            if (IsAtEnd) throw new PythonRePatternException("unexpected end of pattern", _position);
            if (TryConsume('#'))
            {
                while (!IsAtEnd && !TryConsume(')'))
                {
                    Utf8ConstructionScope.ChargeWork(1);
                    _position++;
                }
                if (IsAtEnd && (_position == 0 || _pattern[_position - 1] != ')'))
                    throw new PythonRePatternException("missing ), unterminated comment", frame.GroupStart);
                _parsedNode = PythonReAstFactory.Empty;
                child = default;
                return true;
            }
            if (TryConsume(':')) frame.GroupKind = PythonReGroupKind.NonCapturing;
            else if (TryConsume('>')) frame.GroupKind = PythonReGroupKind.Atomic;
            else if (TryConsume('=')) frame.GroupKind = PythonReGroupKind.PositiveLookahead;
            else if (TryConsume('!')) frame.GroupKind = PythonReGroupKind.NegativeLookahead;
            else if (TryConsumeExact("<=")) frame.GroupKind = PythonReGroupKind.PositiveLookbehind;
            else if (TryConsumeExact("<!")) frame.GroupKind = PythonReGroupKind.NegativeLookbehind;
            else if (TryConsumeExact("P<"))
            {
                frame.GroupNumber = ++_groupCount;
                frame.GroupName = ParseGroupName('>', "missing >, unterminated name");
                if (Storage.NamedGroups.ContainsKey(frame.GroupName))
                    throw Error($"redefinition of group name '{frame.GroupName}'");
                Storage.NamedGroups.Add(frame.GroupName, frame.GroupNumber);
                Storage.GroupBodies.Add(null);
                frame.GroupKind = PythonReGroupKind.NamedCapturing;
            }
            else if (TryConsumeExact("P="))
            {
                var referenceStart = _position;
                var name = ParseGroupName(')', "missing ), unterminated name");
                if (!Storage.NamedGroups.TryGetValue(name, out var number))
                    throw new PythonRePatternException($"unknown group name '{name}'", referenceStart);
                if (Storage.IsOpen(number))
                    throw new PythonRePatternException("cannot refer to an open group", referenceStart);
                CheckLookbehindReference(number, _position);
                _parsedNode = PythonReAstFactory.NamedReference(name, BackreferenceLower(frame.Options));
                child = default;
                return true;
            }
            else if (TryConsume('('))
            {
                frame.Condition = ParseConditionalReference();
                ExpectGroupEnd(frame.GroupStart, "missing ), unterminated name");
                if (_lookbehindGroupStart != 0)
                {
                    var number = frame.Condition.GroupName is string name
                        ? Storage.NamedGroups[name] : frame.Condition.GroupNumber;
                    CheckLookbehindReference(number, _position);
                }
                frame.Stage = 2;
                child = new(ParseFrameKind.Sequence, frame.Options);
                return false;
            }
            else
            {
                frame.GroupKind = PythonReGroupKind.ScopedFlags;
                if (TryConsume('-'))
                {
                    frame.RemoveOptions = ParseFlagSet(allowEmpty: false, context: FlagParseContext.Removed);
                    if (!IsAtEnd && char.IsAsciiLetter(Current) && Current != ':')
                        throw new PythonRePatternException("unknown flag", _position);
                    if (!TryConsume(':')) throw new PythonRePatternException("missing :", _position);
                }
                else
                {
                    if (IsAtEnd) throw new PythonRePatternException("missing -, : or )", _position);
                    if (Current == 'P')
                    {
                        if (_position + 1 >= _pattern.Length)
                            throw new PythonRePatternException("unexpected end of pattern", _position + 1);
                        throw new PythonRePatternException(GetUnknownExtensionMessage(), _position - 1);
                    }
                    if (char.IsAsciiLetter(Current) && !IsInlineFlagChar(Current))
                        throw new PythonRePatternException("unknown extension ?" + Current, _position - 1);
                    if (Current == '<')
                    {
                        if (_position + 1 >= _pattern.Length)
                            throw new PythonRePatternException("unexpected end of pattern", _position + 1);
                        if (_pattern[_position + 1] == '>')
                            throw new PythonRePatternException("unknown extension ?<>", _position - 1);
                        throw new PythonRePatternException("unknown extension ?<" + _pattern[_position + 1], _position - 1);
                    }
                    frame.AddOptions = ParseFlagSet(allowEmpty: false, context: FlagParseContext.Added);
                    if (!IsAtEnd && Current != ':' && Current != ')' && Current != '-' && !char.IsAsciiLetter(Current))
                        throw new PythonRePatternException("missing -, : or )", _position);
                    if (TryConsume('-'))
                    {
                        frame.RemoveOptions = ParseFlagSet(allowEmpty: false, context: FlagParseContext.Removed);
                        if (!IsAtEnd && char.IsAsciiLetter(Current) && Current != ':')
                            throw new PythonRePatternException("unknown flag", _position);
                    }
                    if (!TryConsume(':'))
                    {
                        if (TryConsume(')'))
                            throw new PythonRePatternException("global flags not at the start of the expression", frame.GroupStart);
                        if (IsAtEnd) throw new PythonRePatternException("missing -, : or )", _position);
                        if (char.IsAsciiLetter(Current)) throw new PythonRePatternException("unknown flag", _position);
                        throw Error("unknown group extension");
                    }
                }
                bodyOptions = ApplyScopedFlags(frame.Options, frame.AddOptions, frame.RemoveOptions);
            }
        }
        if (frame.GroupKind is PythonReGroupKind.PositiveLookbehind or PythonReGroupKind.NegativeLookbehind)
        {
            frame.PreviousLookbehindGroupStart = _lookbehindGroupStart;
            // Nested lookbehinds inherit the outer assertion's boundary.
            if (_lookbehindGroupStart == 0) _lookbehindGroupStart = _groupCount + 1;
        }
        frame.Stage = 1;
        child = new(ParseFrameKind.Alternation, bodyOptions);
        return false;
    }
}
