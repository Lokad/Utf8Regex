using System.Buffers;
using Lokad.Utf8Regex.Internal;
using System.Text;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Input;
using Lokad.Utf8Regex.Internal.Search;

namespace Lokad.Utf8Regex.Pcre2;

// backtracking lowerer, split from the compiler collection along the lowering ownership boundary.
internal sealed class Pcre2BacktrackingLowerer
{
    private readonly Utf8ConstructionBuffer<Pcre2BacktrackingInstruction> _instructions = new(Utf8ConstructionScope.CurrentBudget, true);
    private readonly Utf8ConstructionBuffer<Pcre2BacktrackingProgram> _assertionPrograms = new(Utf8ConstructionScope.CurrentBudget, false);
    private readonly Utf8ConstructionBuffer<Pcre2BackreferenceSlotSet> _backreferenceSlotSets = new(Utf8ConstructionScope.CurrentBudget, false);
    private readonly Utf8ConstructionBuffer<Pcre2BacktrackingCondition> _conditions = new(Utf8ConstructionScope.CurrentBudget, false);
    private readonly Utf8ConstructionBuffer<string> _markNames = new(Utf8ConstructionScope.CurrentBudget, false);
    private readonly Dictionary<string, int> _markNameIds = new(StringComparer.Ordinal);
    private readonly int _captureCount;
    private readonly Pcre2NameEntry[] _nameEntries;
    private readonly IReadOnlyDictionary<int, Pcre2CaptureBacktrackingNode> _captureDefinitions;
    private readonly IReadOnlyDictionary<string, int[]> _nameSlots;
    private readonly Pcre2CompileRequest _request;
    private int _repeatCount;
    private int _captureReadSlot;
    private bool _hasCaptureWrites;
    private bool _usesMatchBoundaryReset;
    private bool _mayReportNonMonotoneMatchOffsets;
    private bool _mayThrowDeferredLookaroundReset;
    private bool _hasSubroutineCalls;
    private bool _hasControlVerbs;
    private int _autoPossessiveRepeatCount;
    private List<int>? _thenPatches;

    internal Pcre2BacktrackingLowerer(
        int captureCount,
        Pcre2NameEntry[] nameEntries,
        IReadOnlyDictionary<int, Pcre2CaptureBacktrackingNode> captureDefinitions,
        IReadOnlyDictionary<string, int[]> nameSlots,
        Pcre2CompileRequest request)
    {
        _captureCount = captureCount;
        _nameEntries = nameEntries;
        _captureDefinitions = captureDefinitions;
        _nameSlots = nameSlots;
        _request = request;
    }

    internal Pcre2BacktrackingProgram Lower(IPcre2BacktrackingNode root, Pcre2AssertionKind? assertionKind = null)
    {
        EmitNode(root, null, followedByMatchAccept: assertionKind is null);
        _instructions.Add(Pcre2BacktrackingInstruction.CreateAccept());
        if (_hasSubroutineCalls)
        {
            var budget = Utf8ConstructionScope.CurrentBudget;
            var firstCalls = budget.AllocateArray<int>(_captureCount + 1, false);
            budget.ChargeWork((ulong)firstCalls.Length);
            firstCalls.AsSpan().Fill(-1);
            // Appended bodies can introduce more calls. The first linked call
            // supplies the body entry and byte minimum to all later callers.
            for (var index = 0; index < _instructions.Count; index++)
            {
                budget.ChargeWork(1);
                var instruction = _instructions[index];
                if (instruction.Kind != Pcre2BacktrackingInstructionKind.SubroutineCall)
                    continue;
                var slot = instruction.SubroutineSlot;
                var firstCall = firstCalls[slot];
                if (firstCall >= 0)
                {
                    _instructions[index] = _instructions[firstCall];
                    continue;
                }
                var body = slot == 0 ? root : _captureDefinitions[slot];
                var target = _instructions.Count;
                EmitNode(body, null);
                _instructions.Add(Pcre2BacktrackingInstruction.CreateSubroutineReturn());
                var minimumBytes = Pcre2BacktrackingAnalysis.GetMinimumByteLength(body);
                _instructions[index] = Pcre2BacktrackingInstruction.CreateSubroutineCall(slot, target, minimumBytes);
                firstCalls[slot] = index;
            }
        }

        var lookbehindBounds = assertionKind is Pcre2AssertionKind.PositiveLookbehind or Pcre2AssertionKind.NegativeLookbehind
            ? (Pcre2BacktrackingAnalysis.GetMinimumScalarLength(root), Pcre2BacktrackingAnalysis.GetMaximumScalarLength(root))
            : default;
        return new Pcre2BacktrackingProgram(
            _instructions.Take(),
            _request,
            _repeatCount,
            _captureCount + 1,
            _captureReadSlot,
            _hasCaptureWrites,
            _usesMatchBoundaryReset,
            _mayReportNonMonotoneMatchOffsets,
            _mayThrowDeferredLookaroundReset,
            SuppressesUnresetEmptyMatches(root),
            Pcre2BacktrackingAnalysis.RestrictsSearchToInitialCandidate(root),
            _autoPossessiveRepeatCount,
            _assertionPrograms.Take(),
            _backreferenceSlotSets.Take(),
            _conditions.Take(),
            _hasSubroutineCalls,
            _hasControlVerbs,
            _markNames.Take(),
            _nameEntries,
            Pcre2BacktrackingAnalysis.GetMinimumByteLength(root),
            lookbehindBounds,
            Pcre2BacktrackingAnalysis.GetLeadingAsciiByte(root),
            Pcre2BacktrackingAnalysis.StartsWithExtendedGraphemeCluster(root),
            FindAnchoredFinalAscii(root));
    }

    private (Utf8SearchAsciiSet Allowed, bool AllowTrailingLf, bool CanMatchEmpty) FindAnchoredFinalAscii(IPcre2BacktrackingNode root)
    {
        if (root is not Pcre2SequenceBacktrackingNode { Children: { Length: >= 3 and <= 24 } children } ||
            children[0] is not Pcre2TokenBacktrackingNode { Token: var start } ||
            !(start.Kind == Pcre2CharacterTokenKind.BeginningOfSubject ||
              start.Kind == Pcre2CharacterTokenKind.BeginningOfLine && (start.Options & Pcre2CharacterOptions.Multiline) == 0) ||
            children[^1] is not Pcre2TokenBacktrackingNode { Token: var end }) return default;

        if (!TryGetTerminalEnd(end, out var allowTrailingLf)) return default;

        var allowed = default(Utf8SearchAsciiSet);
        var canMatchEmpty = true;
        for (var index = children.Length - 2; index > 0; index--)
        {
            Utf8ConstructionScope.ChargeWork(1);
            var node = children[index];
            var minimum = 1;
            if (node is Pcre2RepeatBacktrackingNode repeat)
            {
                if (repeat.Maximum == 0) continue;
                node = repeat.Body;
                minimum = repeat.Minimum;
            }
            // Prove every child, including the nonterminal prefix. Opaque
            // control effects or early acceptance must keep their normal VM.
            if (node is not Pcre2TokenBacktrackingNode { Token: var atom } ||
                atom.Kind is not (Pcre2CharacterTokenKind.Literal or Pcre2CharacterTokenKind.CharacterClass) ||
                (atom.Options & Pcre2CharacterOptions.Caseless) != 0) return default;
            if (!canMatchEmpty) continue;
            if (atom.Kind == Pcre2CharacterTokenKind.CharacterClass)
            {
                var mask = atom.CharacterClass.AsciiSet;
                allowed = Utf8SearchAsciiSet.FromMasks(allowed.Low | mask.ActualLowMask, allowed.High | mask.ActualHighMask);
            }
            else if (atom.Literal.IsAscii)
                allowed = allowed.With((byte)atom.Literal.Value);
            canMatchEmpty = minimum == 0;
        }
        return (allowed, allowTrailingLf, canMatchEmpty);
    }

    private bool TryGetTerminalEnd(Pcre2CharacterToken token, out bool allowTrailingLf)
    {
        allowTrailingLf = false;
        if (token.Kind == Pcre2CharacterTokenKind.EndOfSubject) return true;
        if (token.Kind == Pcre2CharacterTokenKind.EndOfLine)
        {
            if ((token.Options & Pcre2CharacterOptions.Multiline) != 0) return false;
            if ((_request.Options & Pcre2CompileOptions.DollarEndOnly) != 0) return true;
        }
        else if (token.Kind != Pcre2CharacterTokenKind.EndOfSubjectOrFinalNewline) return false;
        allowTrailingLf = true;
        return _request.Settings.Newline is Pcre2NewlineConvention.Default or Pcre2NewlineConvention.Lf;
    }

    private (int Earlier, int Final) FindTerminalRepeatsWithoutRewind(IPcre2BacktrackingNode[] children,
        Utf8ConstructionBudget budget)
    {
        if (children.Length < 2 || children[^1] is not Pcre2TokenBacktrackingNode { Token: var end } ||
            !TryGetTerminalEnd(end, out var allowTrailingLf)) return (-1, -1);
        budget.ChargeWork(1);
        var final = -1;
        // A shorter endpoint would lie before a scalar the repeat consumed.
        // Only a final LF could satisfy a lax end there.
        if (children[^2] is Pcre2RepeatBacktrackingNode { Maximum: > 0, Preference: Pcre2RepeatPreference.Greedy,
                Body: Pcre2TokenBacktrackingNode { Token: var finalAtom } } &&
            (finalAtom.Kind switch
            {
                Pcre2CharacterTokenKind.Literal => !allowTrailingLf ||
                    (finalAtom.Options & Pcre2CharacterOptions.Caseless) == 0 && finalAtom.Literal.Value != '\n',
                Pcre2CharacterTokenKind.CharacterClass => !allowTrailingLf ||
                    (finalAtom.Options & Pcre2CharacterOptions.Caseless) == 0 && !finalAtom.CharacterClass.AsciiSet.Contains((byte)'\n'),
                Pcre2CharacterTokenKind.Any or Pcre2CharacterTokenKind.AnyNotNewline => !allowTrailingLf,
                _ => false,
            }))
            final = children.Length - 2;

        if (children.Length < 4) return (-1, final);
        budget.ChargeWork(3);
        if (children[^4] is not Pcre2RepeatBacktrackingNode { Maximum: int.MaxValue, Preference: Pcre2RepeatPreference.Greedy,
                Body: Pcre2TokenBacktrackingNode { Token: var first } } ||
            children[^3] is not Pcre2RepeatBacktrackingNode { Minimum: 0, Maximum: 1,
                Body: Pcre2TokenBacktrackingNode { Token: { Kind: Pcre2CharacterTokenKind.Literal } separator } } ||
            children[^2] is not Pcre2RepeatBacktrackingNode { Minimum: 0, Maximum: int.MaxValue,
                Body: Pcre2TokenBacktrackingNode { Token: var last } } ||
            first.Kind != last.Kind || ((first.Options | last.Options | separator.Options) & Pcre2CharacterOptions.Caseless) != 0 ||
            !separator.Literal.IsAscii || separator.Literal.Value == '\n') return (-1, final);

        // The pure second repeat covers the same run on a retry. The optional
        // separator cannot participate until the first run's maximal endpoint.
        var separatorByte = (byte)separator.Literal.Value;
        if (first.Kind == Pcre2CharacterTokenKind.Literal)
            return (first.Literal == last.Literal && first.Literal.Value != separatorByte && first.Literal.Value != '\n'
                ? children.Length - 4 : -1, final);
        if (first.Kind != Pcre2CharacterTokenKind.CharacterClass) return (-1, final);
        var own = first.CharacterClass;
        var other = last.CharacterClass;
        if (own.Negated != other.Negated || own.Terms.Length > 16 || own.Terms.Length != other.Terms.Length ||
            own.AsciiSet.Contains(separatorByte) || own.AsciiSet.Contains((byte)'\n')) return (-1, final);
        budget.ChargeWork((ulong)own.Terms.Length);
        return (own.Terms.AsSpan().SequenceEqual(other.Terms) ? children.Length - 4 : -1, final);
    }

    private bool CanCommitTerminalOptional(IPcre2BacktrackingNode[] children, int index, Utf8ConstructionBudget budget)
    {
        if (children.Length - index is < 2 or > 8 ||
            children[index] is not Pcre2RepeatBacktrackingNode { Minimum: 0, Maximum: 1,
                Preference: Pcre2RepeatPreference.Greedy, Body: Pcre2TokenBacktrackingNode { Token: var optional } } ||
            (optional.Options & Pcre2CharacterOptions.Caseless) != 0 ||
            children[^1] is not Pcre2TokenBacktrackingNode { Token: var end } ||
            !TryGetTerminalEnd(end, out var allowTrailingLf)) return false;

        AsciiCharClass ascii;
        if (optional.Kind == Pcre2CharacterTokenKind.Literal && optional.Literal.IsAscii)
            ascii = AsciiCharClass.ForByte((byte)optional.Literal.Value);
        else if (optional.Kind == Pcre2CharacterTokenKind.CharacterClass &&
            optional.CharacterClass is { Negated: false, Terms.Length: <= 16 } characterClass)
        {
            foreach (var term in characterClass.Terms)
            {
                budget.ChargeWork(1);
                if (term is not { Kind: Pcre2CharacterClassTermKind.Range, Negated: false, Range.High: < 128 }) return false;
            }
            ascii = characterClass.AsciiSet;
        }
        else return false;

        // A skipped optional would leave its consumed byte at the start of
        // the suffix. Every possible first consumer must reject that byte.
        for (var cursor = index + 1; cursor < children.Length - 1; cursor++)
        {
            budget.ChargeWork(1);
            var child = children[cursor];
            var minimum = 1;
            if (child is Pcre2RepeatBacktrackingNode repeat)
            {
                if (repeat.Maximum == 0) continue;
                minimum = repeat.Minimum;
                child = repeat.Body;
            }
            if (child is not Pcre2TokenBacktrackingNode { Token: var next } ||
                (next.Options & Pcre2CharacterOptions.Caseless) != 0) return false;
            var disjoint = next.Kind switch
            {
                Pcre2CharacterTokenKind.Literal => !next.Literal.IsAscii || !ascii.Contains((byte)next.Literal.Value),
                Pcre2CharacterTokenKind.CharacterClass => ascii.IsDisjoint(next.CharacterClass.AsciiSet),
                _ => false,
            };
            if (!disjoint) return false;
            if (minimum > 0) return true;
        }

        return !allowTrailingLf || !ascii.Contains((byte)'\n');
    }

    private void EmitPossessiveTokenRepeat(Pcre2RepeatBacktrackingNode repeat)
    {
        _autoPossessiveRepeatCount++;
        var token = ((Pcre2TokenBacktrackingNode)repeat.Body).Token;
        if (token.Kind == Pcre2CharacterTokenKind.CharacterClass && (token.Options & Pcre2CharacterOptions.Caseless) == 0)
            token.CharacterClass.PrepareAsciiByteSet();
        _instructions.Add(Pcre2BacktrackingInstruction.CreatePossessiveTokenRepeat(token, repeat.Minimum, repeat.Maximum));
    }

    private void EmitNode(
        IPcre2BacktrackingNode node,
        Pcre2CharacterToken? requiredFollowingToken,
        bool followedByMatchAccept = false)
    {
        var budget = Pcre2BacktrackingCompiler.EnterDepth();
        try
        {
            bool CanAutoPossess(
                Pcre2RepeatBacktrackingNode repeat,
                Pcre2CharacterToken followingToken)
            {
                if (repeat.Maximum == 0 ||
                    repeat.Body is not Pcre2TokenBacktrackingNode repeated ||
                    followingToken.Options != Pcre2CharacterOptions.None)
                {
                    return false;
                }

                var following = followingToken.Literal;
                var ucp = (_request.Options & Pcre2CompileOptions.Ucp) != 0;
                if (followingToken.Kind == Pcre2CharacterTokenKind.WordBoundary)
                {
                    // Shortening a word-only run leaves two word scalars next
                    // to each other, so it cannot make this boundary succeed.
                    // With minimum zero, giving back the entire run can expose
                    // a boundary at its start; retain that alternative.
                    if (repeat.Minimum == 0 || repeated.Token.Options != Pcre2CharacterOptions.None) return false;
                    if (repeated.Token.Kind == Pcre2CharacterTokenKind.Literal)
                        return Pcre2CharacterSemantics.IsWord(repeated.Token.Literal, ucp);
                    if (repeated.Token.Kind != Pcre2CharacterTokenKind.CharacterClass ||
                        repeated.Token.CharacterClass.Negated) return false;
                    foreach (var term in repeated.Token.CharacterClass.Terms)
                    {
                        budget.ChargeWork(1);
                        if (term.Negated || (term.Kind switch
                        {
                            Pcre2CharacterClassTermKind.Digit or Pcre2CharacterClassTermKind.Word => false,
                            Pcre2CharacterClassTermKind.Range =>
                                !(term.Range.Low >= '0' && term.Range.High <= '9' ||
                                  term.Range.Low >= 'A' && term.Range.High <= 'Z' ||
                                  term.Range.Low >= 'a' && term.Range.High <= 'z' ||
                                  term.Range.Low == '_' && term.Range.High == '_'),
                            _ => true,
                        })) return false;
                    }
                    return true;
                }
                if (followingToken.Kind != Pcre2CharacterTokenKind.Literal) return false;
                // Every shorter choice would leave a scalar accepted by the
                // repeat where the required literal must begin.
                return repeated.Token.Kind switch
                {
                    Pcre2CharacterTokenKind.Literal =>
                        !Pcre2CharacterSemantics.Equals(repeated.Token.Literal, following, repeated.Token.Options),
                    Pcre2CharacterTokenKind.CharacterClass =>
                        !repeated.Token.CharacterClass.Matches(following, ucp, repeated.Token.Options),
                    _ => false,
                };
            }

            static bool TryGetRequiredLeadingToken(
                IPcre2BacktrackingNode candidate,
                out Pcre2CharacterToken literal)
            {
                Utf8ConstructionScope.ChargeWork(1);
                switch (candidate)
                {
                    case Pcre2TokenBacktrackingNode
                    {
                        Token:
                        {
                            Kind: Pcre2CharacterTokenKind.Literal or Pcre2CharacterTokenKind.WordBoundary,
                            Options: Pcre2CharacterOptions.None,
                        } token,
                    }:
                        literal = token;
                        return true;
                    case Pcre2SequenceBacktrackingNode { Children.Length: > 0 } sequence:
                        return TryGetRequiredLeadingToken(sequence.Children[0], out literal);
                    case Pcre2AlternationBacktrackingNode alternation:
                        Pcre2CharacterToken? common = null;
                        foreach (var alternative in alternation.Alternatives)
                        {
                            if (!TryGetRequiredLeadingToken(alternative, out var branchToken) ||
                                common is { } value && (value.Kind != branchToken.Kind || value.Literal != branchToken.Literal))
                            {
                                literal = default;
                                return false;
                            }

                            common = branchToken;
                        }

                        if (common is { } commonToken)
                        {
                            literal = commonToken;
                            return true;
                        }
                        break;
                    case Pcre2RepeatBacktrackingNode { Minimum: > 0 } repeat:
                        return TryGetRequiredLeadingToken(repeat.Body, out literal);
                    case Pcre2CaptureBacktrackingNode capture:
                        return TryGetRequiredLeadingToken(capture.Body, out literal);
                    case Pcre2AtomicBacktrackingNode atomic:
                        return TryGetRequiredLeadingToken(atomic.Body, out literal);
                }

                literal = default;
                return false;
            }

            switch (node)
            {
                case Pcre2EmptyBacktrackingNode:
                    return;
                case Pcre2TokenBacktrackingNode token:
                    _instructions.Add(Pcre2BacktrackingInstruction.CreateToken(token.Token));
                    return;
                case Pcre2SequenceBacktrackingNode sequence:
                    var (earlierRepeat, finalRepeat) = FindTerminalRepeatsWithoutRewind(sequence.Children, budget);
                    for (var index = 0; index < sequence.Children.Length; index++)
                    {
                        if (index == earlierRepeat || index == finalRepeat ||
                            CanCommitTerminalOptional(sequence.Children, index, budget))
                        {
                            EmitPossessiveTokenRepeat((Pcre2RepeatBacktrackingNode)sequence.Children[index]);
                            continue;
                        }
                        Pcre2CharacterToken? childFollowingToken = null;
                        if (index + 1 < sequence.Children.Length)
                        {
                            if (TryGetRequiredLeadingToken(sequence.Children[index + 1], out var nextToken))
                            {
                                childFollowingToken = nextToken;
                            }
                        }
                        else
                        {
                            childFollowingToken = requiredFollowingToken;
                        }

                        EmitNode(sequence.Children[index], childFollowingToken,
                            followedByMatchAccept && index == sequence.Children.Length - 1);
                    }
                    return;
                case Pcre2AlternationBacktrackingNode alternation:
                    EmitAlternation(alternation, requiredFollowingToken);
                    return;
                case Pcre2RepeatBacktrackingNode repeat:
                    // At the main Accept, shortening a positive run cannot
                    // reach the subject end or turn an empty match into a hit.
                    // Match-boundary resets can make the reported start
                    // differ from the consumed start, so decline them.
                    var finalTokenRun = followedByMatchAccept && !_usesMatchBoundaryReset && !_hasSubroutineCalls &&
                        repeat is
                        {
                            Minimum: > 0,
                            Preference: Pcre2RepeatPreference.Greedy,
                            Body: Pcre2TokenBacktrackingNode
                            {
                                Token.Kind: Pcre2CharacterTokenKind.Literal or Pcre2CharacterTokenKind.CharacterClass
                            }
                        };
                    var autoPossessive = repeat.Preference == Pcre2RepeatPreference.Greedy &&
                        requiredFollowingToken is { } followingToken &&
                        CanAutoPossess(repeat, followingToken);
                    if (finalTokenRun || autoPossessive || IsSmallFixedTokenRepeat(repeat))
                        EmitPossessiveTokenRepeat(repeat);
                    else if (TryEmitSmallFixedSequence(repeat, budget))
                        return;
                    else if (repeat is { Minimum: 0, Maximum: 1 } &&
                        repeat.Preference != Pcre2RepeatPreference.Possessive &&
                        CanLowerOptionalAsFork(repeat.Body, budget))
                    {
                        // The fork preserves the order of the one attempt and skip.
                        // The body keeps its own restoration checkpoints.
                        var fork = _instructions.Count;
                        _instructions.Add(default);
                        EmitNode(repeat.Body, null);
                        var exit = _instructions.Count;
                        _instructions[fork] = repeat.Preference == Pcre2RepeatPreference.Greedy
                            ? Pcre2BacktrackingInstruction.CreateSplit(fork + 1, exit)
                            : Pcre2BacktrackingInstruction.CreateSplit(exit, fork + 1);
                    }
                    else if (repeat.Preference == Pcre2RepeatPreference.Greedy &&
                        repeat.Body is Pcre2TokenBacktrackingNode { Token: { Kind: Pcre2CharacterTokenKind.CharacterClass } classToken })
                    {
                        if ((classToken.Options & Pcre2CharacterOptions.Caseless) == 0)
                        {
                            classToken.CharacterClass.PrepareAsciiByteSet();
                        }

                        _instructions.Add(Pcre2BacktrackingInstruction.CreateGreedyClassRepeat(
                            classToken, repeat.Minimum, repeat.Maximum));
                    }
                    else if (repeat.Preference == Pcre2RepeatPreference.Possessive)
                    {
                        _instructions.Add(Pcre2BacktrackingInstruction.CreateAtomicStart());
                        EmitRepeat(new Pcre2RepeatBacktrackingNode(
                            repeat.Body,
                            repeat.Minimum,
                            repeat.Maximum,
                            Pcre2RepeatPreference.Greedy));
                        _instructions.Add(Pcre2BacktrackingInstruction.CreateAtomicEnd());
                    }
                    else
                    {
                        EmitRepeat(repeat);
                    }
                    return;
                case Pcre2CaptureBacktrackingNode capture:
                    _hasCaptureWrites = true;
                    if (IsSingleScalarToken(capture.Body, out var capturedToken))
                    {
                        _instructions.Add(Pcre2BacktrackingInstruction.CreateCaptureToken(capture.Slot, capturedToken));
                        return;
                    }
                    _instructions.Add(Pcre2BacktrackingInstruction.CreateCaptureStart(capture.Slot));
                    EmitNode(capture.Body, requiredFollowingToken, followedByMatchAccept);
                    _instructions.Add(Pcre2BacktrackingInstruction.CreateCaptureEnd(capture.Slot));
                    return;
                case Pcre2BackreferenceBacktrackingNode backreference:
                    EmitBackreference(backreference);
                    return;
                case Pcre2AssertionBacktrackingNode assertion:
                    _instructions.Add(Pcre2BacktrackingInstruction.CreateAssertion(
                        LowerAssertion(assertion),
                        assertion.AssertionKind));
                    return;
                case Pcre2SubroutineCallBacktrackingNode subroutineCall:
                    _hasSubroutineCalls = true;
                    _instructions.Add(Pcre2BacktrackingInstruction.CreateSubroutineCall(
                        GetSubroutineSlot(subroutineCall.Target)));
                    return;
                case Pcre2ConditionalBacktrackingNode conditional:
                    EmitConditional(conditional);
                    return;
                case Pcre2AtomicBacktrackingNode atomic:
                    _instructions.Add(Pcre2BacktrackingInstruction.CreateAtomicStart());
                    EmitNode(atomic.Body, null);
                    _instructions.Add(Pcre2BacktrackingInstruction.CreateAtomicEnd());
                    return;
                case Pcre2ControlVerbBacktrackingNode controlVerb:
                    EmitControlVerb(controlVerb);
                    return;
                case Pcre2MatchBoundaryResetBacktrackingNode:
                    _usesMatchBoundaryReset = true;
                    _instructions.Add(Pcre2BacktrackingInstruction.CreateMatchBoundaryReset());
                    return;
                default:
                    throw new InvalidOperationException("The PCRE2 backtracking syntax node is not lowerable.");
            }

        }
        finally { budget.LeaveDepth(); }
    }

    private void EmitAlternation(
        Pcre2AlternationBacktrackingNode alternation,
        Pcre2CharacterToken? requiredFollowingToken)
    {
        var parentThenPatches = _thenPatches;
        var exitJumps = new List<int>(alternation.Alternatives.Length - 1);
        for (var i = 0; i < alternation.Alternatives.Length - 1; i++)
        {
            var branchThenPatches = new List<int>();
            _thenPatches = branchThenPatches;
            var splitIndex = _instructions.Count;
            _instructions.Add(default);
            var primaryTarget = _instructions.Count;
            EmitNode(alternation.Alternatives[i], requiredFollowingToken);
            var jumpIndex = _instructions.Count;
            _instructions.Add(default);
            exitJumps.Add(jumpIndex);
            var secondaryTarget = _instructions.Count;
            _instructions[splitIndex] = Pcre2BacktrackingInstruction.CreateSplit(primaryTarget, secondaryTarget);
            foreach (var thenPatch in branchThenPatches)
            {
                var instruction = _instructions[thenPatch];
                _instructions[thenPatch] = Pcre2BacktrackingInstruction.CreateControlDeferred(
                    Pcre2ControlVerbKind.Then,
                    instruction.ControlNameId,
                    secondaryTarget);
            }
        }

        _thenPatches = parentThenPatches;
        EmitNode(alternation.Alternatives[^1], requiredFollowingToken);
        var exitTarget = _instructions.Count;
        foreach (var jumpIndex in exitJumps)
        {
            _instructions[jumpIndex] = Pcre2BacktrackingInstruction.CreateJump(exitTarget);
        }

        _thenPatches = parentThenPatches;
    }

    private void EmitControlVerb(Pcre2ControlVerbBacktrackingNode controlVerb)
    {
        _hasControlVerbs = true;
        var nameId = GetMarkNameId(controlVerb.Name);
        switch (controlVerb.VerbKind)
        {
            case Pcre2ControlVerbKind.Accept:
                _instructions.Add(Pcre2BacktrackingInstruction.CreateControlAccept(nameId));
                return;
            case Pcre2ControlVerbKind.Fail:
                _instructions.Add(Pcre2BacktrackingInstruction.CreateControlFail(nameId));
                return;
            case Pcre2ControlVerbKind.Mark:
                _instructions.Add(Pcre2BacktrackingInstruction.CreateControlMark(nameId));
                return;
            case Pcre2ControlVerbKind.Then:
                var instructionIndex = _instructions.Count;
                _instructions.Add(Pcre2BacktrackingInstruction.CreateControlDeferred(
                    controlVerb.VerbKind,
                    nameId,
                    -1));
                _thenPatches?.Add(instructionIndex);
                return;
            default:
                _instructions.Add(Pcre2BacktrackingInstruction.CreateControlDeferred(
                    controlVerb.VerbKind,
                    nameId,
                    -1));
                return;
        }
    }

    private int GetMarkNameId(string name)
    {
        if (name.Length == 0)
        {
            return -1;
        }

        if (_markNameIds.TryGetValue(name, out var id))
        {
            return id;
        }

        id = _markNames.Count;
        _markNames.Add(name);
        _markNameIds.Add(name, id);
        return id;
    }

    private static bool IsSmallFixedTokenRepeat(Pcre2RepeatBacktrackingNode repeat)
        => repeat is
        {
            Minimum: >= 2 and <= 8,
            Preference: Pcre2RepeatPreference.Greedy,
            Body: Pcre2TokenBacktrackingNode
            {
                Token.Kind: Pcre2CharacterTokenKind.Literal or Pcre2CharacterTokenKind.CharacterClass
            }
        } && repeat.Minimum == repeat.Maximum;

    private static bool CanLowerOptionalAsFork(IPcre2BacktrackingNode body, Utf8ConstructionBudget budget)
    {
        if (body is Pcre2SubroutineCallBacktrackingNode || IsSingleScalarToken(body, out _)) return true;
        if (body is not Pcre2SequenceBacktrackingNode { Children: { Length: >= 2 and <= 16 } children }) return false;
        budget.ChargeWork((ulong)children.Length);
        foreach (var child in children)
        {
            var atom = child is Pcre2RepeatBacktrackingNode repeat ? repeat.Body : child;
            if (!IsSingleScalarToken(atom, out _)) return false;
        }
        return true;
    }

    private static bool IsSingleScalarToken(IPcre2BacktrackingNode node, out Pcre2CharacterToken token)
    {
        if (node is Pcre2TokenBacktrackingNode
        {
            Token: { Kind: Pcre2CharacterTokenKind.Literal or Pcre2CharacterTokenKind.CharacterClass or Pcre2CharacterTokenKind.Any } scalar,
        })
        {
            token = scalar;
            return true;
        }
        token = default;
        return false;
    }

    private bool TryEmitSmallFixedSequence(Pcre2RepeatBacktrackingNode repeat, Utf8ConstructionBudget budget)
    {
        if (repeat is not { Minimum: 2 or 3, Preference: Pcre2RepeatPreference.Greedy } ||
            repeat.Minimum != repeat.Maximum) return false;
        var capture = repeat.Body as Pcre2CaptureBacktrackingNode;
        if ((capture?.Body ?? repeat.Body) is not Pcre2SequenceBacktrackingNode
            { Children: { Length: >= 2 and <= 8 } children }) return false;

        budget.ChargeWork((ulong)children.Length);
        foreach (var child in children)
        {
            if (child is Pcre2TokenBacktrackingNode
                { Token.Kind: Pcre2CharacterTokenKind.Literal or Pcre2CharacterTokenKind.CharacterClass } ||
                child is Pcre2RepeatBacktrackingNode fixedToken && IsSmallFixedTokenRepeat(fixedToken))
                continue;
            return false;
        }

        // Every child consumes a fixed number of scalars without a retry.
        // Each copy writes the same capture slot, in the original order.
        // Admit bounded expansion work before the ordinary instruction writes.
        budget.ChargeWork((ulong)(repeat.Minimum * (children.Length + (capture is null ? 0 : 2))));
        for (var copy = 0; copy < repeat.Minimum; copy++)
            EmitNode(repeat.Body, null);
        return true;
    }

    private void EmitRepeat(Pcre2RepeatBacktrackingNode repeat)
    {
        var repeatId = _repeatCount++;
        var repeatIndex = _instructions.Count;
        _instructions.Add(default);
        var bodyTarget = _instructions.Count;
        EmitNode(repeat.Body, null);
        _instructions.Add(Pcre2BacktrackingInstruction.CreateRepeatEnd(repeatIndex));
        var exitTarget = _instructions.Count;
        _instructions.Add(Pcre2BacktrackingInstruction.CreateRepeatExit(repeatId));
        _instructions[repeatIndex] = Pcre2BacktrackingInstruction.CreateRepeat(
            bodyTarget,
            exitTarget,
            repeatId,
            repeat.Minimum,
            repeat.Maximum,
            repeat.Preference);
    }

    private void EmitConditional(Pcre2ConditionalBacktrackingNode conditional)
    {
        var conditionId = _conditions.Count;
        _conditions.Add(CreateCondition(conditional));
        var conditionIndex = _instructions.Count;
        _instructions.Add(default);
        var yesTarget = _instructions.Count;
        EmitNode(conditional.YesBranch, null);
        var exitJump = _instructions.Count;
        _instructions.Add(default);
        var noTarget = _instructions.Count;
        EmitNode(conditional.NoBranch, null);
        var exitTarget = _instructions.Count;
        _instructions[conditionIndex] = Pcre2BacktrackingInstruction.CreateConditional(
            conditionId,
            yesTarget,
            noTarget);
        _instructions[exitJump] = Pcre2BacktrackingInstruction.CreateJump(exitTarget);
    }

    private int LowerAssertion(Pcre2AssertionBacktrackingNode assertion)
    {
        var assertionLowerer = new Pcre2BacktrackingLowerer(
            _captureCount,
            _nameEntries,
            _captureDefinitions,
            _nameSlots,
            _request);
        var assertionProgramId = _assertionPrograms.Count;
        var assertionProgram = assertionLowerer.Lower(assertion.Body, assertion.AssertionKind);
        _assertionPrograms.Add(assertionProgram);
        if (assertionProgram.CaptureReadSlot != 0) _captureReadSlot = -1;
        _hasCaptureWrites |= assertionProgram.HasCaptureWrites;
        _usesMatchBoundaryReset |= assertionProgram.UsesMatchBoundaryReset;
        var containsReset = ContainsMatchBoundaryReset(assertion.Body);
        _mayReportNonMonotoneMatchOffsets |= containsReset;
        _mayThrowDeferredLookaroundReset |= assertionProgram.UsesMatchBoundaryReset && !containsReset;
        foreach (var markName in assertionProgram.MarkNames)
        {
            _ = GetMarkNameId(markName);
        }

        return assertionProgramId;
    }

    private Pcre2BacktrackingCondition CreateCondition(Pcre2ConditionalBacktrackingNode conditional)
    {
        if (conditional.ConditionKind == Pcre2BacktrackingConditionKind.Assertion)
        {
            var assertion = conditional.Assertion ??
                throw new InvalidOperationException("A PCRE2 assertion condition requires an assertion body.");
            return new Pcre2BacktrackingCondition(
                conditional.ConditionKind,
                [],
                LowerAssertion(assertion),
                assertion.AssertionKind,
                0);
        }

        if (conditional.ConditionKind == Pcre2BacktrackingConditionKind.RecursionAny)
        {
            return new Pcre2BacktrackingCondition(
                conditional.ConditionKind,
                [],
                0,
                default,
                0);
        }

        if (conditional.ConditionKind == Pcre2BacktrackingConditionKind.RecursionSlot)
        {
            return new Pcre2BacktrackingCondition(
                conditional.ConditionKind,
                [],
                0,
                default,
                GetSubroutineSlot(conditional.Target));
        }

        var captureSlots = conditional.Target.Kind == Pcre2BackreferenceTargetKind.Named
            ? _nameSlots[conditional.Target.Name]
            : [Pcre2BacktrackingAnalysis.ResolveReferenceSlot(conditional.Target)];
        if (captureSlots.Length == 1) NoteCaptureRead(captureSlots[0]);
        else _captureReadSlot = -1;
        return new Pcre2BacktrackingCondition(
            conditional.ConditionKind,
            captureSlots,
            0,
            default,
            0);
    }

    private void EmitBackreference(Pcre2BackreferenceBacktrackingNode backreference)
    {
        if (backreference.Target.Kind != Pcre2BackreferenceTargetKind.Named)
        {
            var slot = Pcre2BacktrackingAnalysis.ResolveReferenceSlot(backreference.Target);
            NoteCaptureRead(slot);
            _instructions.Add(Pcre2BacktrackingInstruction.CreateBackreference(slot, backreference.Options));
            return;
        }

        var slots = _nameSlots[backreference.Target.Name];
        if (slots.Length == 1)
        {
            NoteCaptureRead(slots[0]);
            _instructions.Add(Pcre2BacktrackingInstruction.CreateBackreference(slots[0], backreference.Options));
            return;
        }

        _captureReadSlot = -1;
        var slotSetId = _backreferenceSlotSets.Count;
        _backreferenceSlotSets.Add(new Pcre2BackreferenceSlotSet(slots));
        _instructions.Add(Pcre2BacktrackingInstruction.CreateBackreferenceSlotSet(
            slotSetId,
            backreference.Options));
    }

    private void NoteCaptureRead(int slot)
    {
        if (_captureReadSlot != slot)
            _captureReadSlot = _captureReadSlot == 0 ? slot : -1;
    }

    private int GetSubroutineSlot(Pcre2BackreferenceTarget target)
        => target.Kind == Pcre2BackreferenceTargetKind.Named
            ? _nameSlots[target.Name][0]
            : Pcre2BacktrackingAnalysis.ResolveReferenceSlot(target);

    private static bool ContainsMatchBoundaryReset(IPcre2BacktrackingNode node)
    {
        Utf8ConstructionScope.ChargeWork(1);
        return node switch
        {
            Pcre2MatchBoundaryResetBacktrackingNode => true,
            Pcre2SequenceBacktrackingNode sequence => sequence.Children.Any(ContainsMatchBoundaryReset),
            Pcre2AlternationBacktrackingNode alternation => alternation.Alternatives.Any(ContainsMatchBoundaryReset),
            Pcre2RepeatBacktrackingNode repeat => ContainsMatchBoundaryReset(repeat.Body),
            Pcre2CaptureBacktrackingNode capture => ContainsMatchBoundaryReset(capture.Body),
            Pcre2AssertionBacktrackingNode assertion => ContainsMatchBoundaryReset(assertion.Body),
            Pcre2ConditionalBacktrackingNode conditional =>
                conditional.Assertion is not null && ContainsMatchBoundaryReset(conditional.Assertion.Body) ||
                ContainsMatchBoundaryReset(conditional.YesBranch) ||
                ContainsMatchBoundaryReset(conditional.NoBranch),
            Pcre2AtomicBacktrackingNode atomic => ContainsMatchBoundaryReset(atomic.Body),
            _ => false,
        };
    }

    private static bool SuppressesUnresetEmptyMatches(IPcre2BacktrackingNode node) =>
        node is Pcre2RepeatBacktrackingNode
        {
            Minimum: 0,
        } repeat && ContainsMatchBoundaryReset(repeat.Body);
}
