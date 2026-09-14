using System.Buffers;
using System.Text;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Input;
using Lokad.Utf8Regex.Internal.Search;

namespace Lokad.Utf8Regex.Pcre2;

// PLAN.md D09: backtracking lowerer, split from the compiler collection along the lowering ownership boundary.
internal sealed class Pcre2BacktrackingLowerer
{
    private readonly List<Pcre2BacktrackingInstruction> _instructions = [];
    private readonly List<Pcre2BacktrackingProgram> _assertionPrograms = [];
    private readonly List<Pcre2BackreferenceSlotSet> _backreferenceSlotSets = [];
    private readonly List<Pcre2BacktrackingCondition> _conditions = [];
    private readonly List<string> _markNames = [];
    private readonly Dictionary<string, int> _markNameIds = new(StringComparer.Ordinal);
    private readonly int _captureCount;
    private readonly Pcre2NameEntry[] _nameEntries;
    private readonly IReadOnlyDictionary<int, Pcre2CaptureBacktrackingNode> _captureDefinitions;
    private readonly Dictionary<string, int[]> _nameSlots;
    private readonly Pcre2CompileRequest _request;
    private readonly string[] _groupNames;
    private int _repeatCount;
    private bool _requiresCaptureState;
    private bool _hasCaptureWrites;
    private bool _usesBacktrackingControlVerbs;
    private bool _usesMatchBoundaryReset;
    private bool _mayReportNonMonotoneMatchOffsets;
    private bool _mayThrowDeferredLookaroundReset;
    private bool _hasSubroutineCalls;
    private int _autoPossessiveRepeatCount;
    private List<int>? _thenPatches;

    internal Pcre2BacktrackingLowerer(
        int captureCount,
        Pcre2NameEntry[] nameEntries,
        IReadOnlyDictionary<int, Pcre2CaptureBacktrackingNode> captureDefinitions,
        Pcre2CompileRequest request,
        string[] groupNames)
    {
        _captureCount = captureCount;
        _nameEntries = nameEntries;
        _captureDefinitions = captureDefinitions;
        _nameSlots = nameEntries
            .GroupBy(static entry => entry.Name, StringComparer.Ordinal)
            .ToDictionary(
                static group => group.Key,
                static group => group.Select(static entry => entry.Number).ToArray(),
                StringComparer.Ordinal);
        _request = request;
        _groupNames = groupNames;
    }

    internal Pcre2BacktrackingProgram Lower(IPcre2BacktrackingNode root)
    {
        EmitNode(root, null);
        _instructions.Add(Pcre2BacktrackingInstruction.CreateAccept());
        var subroutineTargets = Array.Empty<int>();
        var subroutinePreservesUnsetCaptures = Array.Empty<bool>();
        if (_hasSubroutineCalls)
        {
            subroutineTargets = new int[_captureCount + 1];
            subroutinePreservesUnsetCaptures = new bool[_captureCount + 1];
            subroutineTargets.AsSpan().Fill(-1);
            subroutineTargets[0] = _instructions.Count;
            subroutinePreservesUnsetCaptures[0] = !ContainsDeferredControlVerb(root);
            EmitNode(root, null);
            _instructions.Add(Pcre2BacktrackingInstruction.CreateSubroutineReturn());
            foreach (var definition in _captureDefinitions.OrderBy(static pair => pair.Key))
            {
                subroutineTargets[definition.Key] = _instructions.Count;
                subroutinePreservesUnsetCaptures[definition.Key] = !ContainsDeferredControlVerb(definition.Value);
                EmitNode(definition.Value, null);
                _instructions.Add(Pcre2BacktrackingInstruction.CreateSubroutineReturn());
            }
        }

        return new Pcre2BacktrackingProgram(
            [.. _instructions],
            _request,
            _repeatCount,
            _captureCount + 1,
            _requiresCaptureState,
            _hasCaptureWrites,
            _usesBacktrackingControlVerbs,
            _usesMatchBoundaryReset,
            _mayReportNonMonotoneMatchOffsets,
            _mayThrowDeferredLookaroundReset,
            SuppressesUnresetEmptyMatches(root),
            Pcre2BacktrackingAnalysis.RestrictsSearchToInitialCandidate(root),
            _autoPossessiveRepeatCount,
            [.. _assertionPrograms],
            [.. _backreferenceSlotSets],
            [.. _conditions],
            subroutineTargets,
            subroutinePreservesUnsetCaptures,
            [.. _markNames],
            _groupNames,
            _nameEntries,
            Pcre2BacktrackingAnalysis.GetMinimumByteLength(root),
            Pcre2BacktrackingAnalysis.GetMinimumByteLength(root, _captureDefinitions, _nameSlots),
            Pcre2BacktrackingAnalysis.GetMinimumScalarLength(root),
            Pcre2BacktrackingAnalysis.GetMaximumScalarLength(root),
            Pcre2BacktrackingAnalysis.GetLeadingAsciiByte(root),
            Pcre2BacktrackingAnalysis.StartsWithExtendedGraphemeCluster(root),
            Pcre2BacktrackingAnalysis.ContainsCodeUnit(root));
    }

    private void EmitNode(
        IPcre2BacktrackingNode node,
        Pcre2CharacterToken? requiredFollowingLiteral)
    {
        bool CanAutoPossess(
            Pcre2RepeatBacktrackingNode repeat,
            Pcre2CharacterToken followingLiteral)
        {
            // The current lowering uses an atomic checkpoint. Keep metered plans on
            // their original instruction/depth accounting until commitment has a
            // dedicated instruction that preserves those observable limits.
            if (!Pcre2GlobalOperationDriver.HasUnmeteredExecution(_request) ||
                repeat.Maximum == 0 ||
                repeat.Body is not Pcre2TokenBacktrackingNode repeated ||
                followingLiteral.Kind != Pcre2CharacterTokenKind.Literal ||
                followingLiteral.Options != Pcre2CharacterOptions.None)
            {
                return false;
            }

            var following = followingLiteral.Literal;
            var caseless = (repeated.Token.Options & Pcre2CharacterOptions.Caseless) != 0;
            var ucp = (_request.Options & Pcre2CompileOptions.Ucp) != 0;
            return repeated.Token.Kind switch
            {
                Pcre2CharacterTokenKind.Literal =>
                    !Pcre2CharacterSemantics.Equals(repeated.Token.Literal, following, caseless),
                Pcre2CharacterTokenKind.CharacterClass =>
                    !repeated.Token.CharacterClass.Matches(following, ucp, caseless),
                _ => false,
            };
        }

        static bool TryGetRequiredLeadingLiteral(
            IPcre2BacktrackingNode candidate,
            out Pcre2CharacterToken literal)
        {
            switch (candidate)
            {
                case Pcre2TokenBacktrackingNode
                {
                    Token:
                    {
                        Kind: Pcre2CharacterTokenKind.Literal,
                        Options: Pcre2CharacterOptions.None,
                    } token,
                }:
                    literal = token;
                    return true;
                case Pcre2SequenceBacktrackingNode { Children.Length: > 0 } sequence:
                    return TryGetRequiredLeadingLiteral(sequence.Children[0], out literal);
                case Pcre2AlternationBacktrackingNode alternation:
                    Pcre2CharacterToken? common = null;
                    foreach (var alternative in alternation.Alternatives)
                    {
                        if (!TryGetRequiredLeadingLiteral(alternative, out var branchLiteral) ||
                            common is { } value && value.Literal != branchLiteral.Literal)
                        {
                            literal = default;
                            return false;
                        }

                        common = branchLiteral;
                    }

                    if (common is { } commonLiteral)
                    {
                        literal = commonLiteral;
                        return true;
                    }
                    break;
                case Pcre2RepeatBacktrackingNode { Minimum: > 0 } repeat:
                    return TryGetRequiredLeadingLiteral(repeat.Body, out literal);
                case Pcre2CaptureBacktrackingNode capture:
                    return TryGetRequiredLeadingLiteral(capture.Body, out literal);
                case Pcre2AtomicBacktrackingNode atomic:
                    return TryGetRequiredLeadingLiteral(atomic.Body, out literal);
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
                for (var index = 0; index < sequence.Children.Length; index++)
                {
                    Pcre2CharacterToken? childFollowingLiteral = null;
                    if (index + 1 < sequence.Children.Length)
                    {
                        if (TryGetRequiredLeadingLiteral(sequence.Children[index + 1], out var nextLiteral))
                        {
                            childFollowingLiteral = nextLiteral;
                        }
                    }
                    else
                    {
                        childFollowingLiteral = requiredFollowingLiteral;
                    }

                    EmitNode(sequence.Children[index], childFollowingLiteral);
                }
                return;
            case Pcre2AlternationBacktrackingNode alternation:
                EmitAlternation(alternation, requiredFollowingLiteral);
                return;
            case Pcre2RepeatBacktrackingNode repeat:
                var autoPossessive = repeat.Preference == Pcre2RepeatPreference.Greedy &&
                    requiredFollowingLiteral is { } followingLiteral &&
                    CanAutoPossess(repeat, followingLiteral);
                if (autoPossessive)
                {
                    _autoPossessiveRepeatCount++;
                    var repeatedToken = ((Pcre2TokenBacktrackingNode)repeat.Body).Token;
                    if (repeatedToken.Kind == Pcre2CharacterTokenKind.CharacterClass &&
                        (repeatedToken.Options & Pcre2CharacterOptions.Caseless) == 0)
                    {
                        repeatedToken.CharacterClass.PrepareAsciiSearchValues();
                    }

                    _instructions.Add(Pcre2BacktrackingInstruction.CreatePossessiveTokenRepeat(
                        repeatedToken,
                        repeat.Minimum,
                        repeat.Maximum));
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
                _instructions.Add(Pcre2BacktrackingInstruction.CreateCaptureStart(capture.Slot));
                EmitNode(capture.Body, requiredFollowingLiteral);
                _instructions.Add(Pcre2BacktrackingInstruction.CreateCaptureEnd(capture.Slot));
                return;
            case Pcre2BackreferenceBacktrackingNode backreference:
                _requiresCaptureState = true;
                EmitBackreference(backreference);
                return;
            case Pcre2AssertionBacktrackingNode assertion:
                var assertionLowerer = new Pcre2BacktrackingLowerer(
                    _captureCount,
                    _nameEntries,
                    _captureDefinitions,
                    _request,
                    _groupNames);
                var assertionProgramId = _assertionPrograms.Count;
                var assertionProgram = assertionLowerer.Lower(assertion.Body);
                _assertionPrograms.Add(assertionProgram);
                _requiresCaptureState |= assertionProgram.RequiresCaptureState;
                _hasCaptureWrites |= assertionProgram.HasCaptureWrites;
                _usesBacktrackingControlVerbs |= assertionProgram.UsesBacktrackingControlVerbs;
                _usesMatchBoundaryReset |= assertionProgram.UsesMatchBoundaryReset;
                _mayReportNonMonotoneMatchOffsets |= ContainsMatchBoundaryReset(assertion.Body);
                _mayThrowDeferredLookaroundReset |=
                    assertionProgram.UsesMatchBoundaryReset && !ContainsMatchBoundaryReset(assertion.Body);
                foreach (var markName in assertionProgram.MarkNames)
                {
                    _ = GetMarkNameId(markName);
                }
                _instructions.Add(Pcre2BacktrackingInstruction.CreateAssertion(
                    assertionProgramId,
                    assertion.AssertionKind));
                return;
            case Pcre2SubroutineCallBacktrackingNode subroutineCall:
                _hasSubroutineCalls = true;
                _instructions.Add(Pcre2BacktrackingInstruction.CreateSubroutineCall(
                    ResolveSubroutine(subroutineCall.Target)));
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

    private void EmitAlternation(
        Pcre2AlternationBacktrackingNode alternation,
        Pcre2CharacterToken? requiredFollowingLiteral)
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
            EmitNode(alternation.Alternatives[i], requiredFollowingLiteral);
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
        EmitNode(alternation.Alternatives[^1], requiredFollowingLiteral);
        var exitTarget = _instructions.Count;
        foreach (var jumpIndex in exitJumps)
        {
            _instructions[jumpIndex] = Pcre2BacktrackingInstruction.CreateJump(exitTarget);
        }

        _thenPatches = parentThenPatches;
    }

    private void EmitControlVerb(Pcre2ControlVerbBacktrackingNode controlVerb)
    {
        _usesBacktrackingControlVerbs = true;
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

    private Pcre2BacktrackingCondition CreateCondition(Pcre2ConditionalBacktrackingNode conditional)
    {
        if (conditional.ConditionKind == Pcre2BacktrackingConditionKind.Assertion)
        {
            var assertion = conditional.Assertion ??
                throw new InvalidOperationException("A PCRE2 assertion condition requires an assertion body.");
            var assertionLowerer = new Pcre2BacktrackingLowerer(
                _captureCount,
                _nameEntries,
                _captureDefinitions,
                _request,
                _groupNames);
            var assertionProgramId = _assertionPrograms.Count;
            var assertionProgram = assertionLowerer.Lower(assertion.Body);
            _assertionPrograms.Add(assertionProgram);
            _requiresCaptureState |= assertionProgram.RequiresCaptureState;
            _hasCaptureWrites |= assertionProgram.HasCaptureWrites;
            _usesBacktrackingControlVerbs |= assertionProgram.UsesBacktrackingControlVerbs;
            _usesMatchBoundaryReset |= assertionProgram.UsesMatchBoundaryReset;
            _mayReportNonMonotoneMatchOffsets |= ContainsMatchBoundaryReset(assertion.Body);
            _mayThrowDeferredLookaroundReset |=
                assertionProgram.UsesMatchBoundaryReset && !ContainsMatchBoundaryReset(assertion.Body);
            foreach (var markName in assertionProgram.MarkNames)
            {
                _ = GetMarkNameId(markName);
            }
            return new Pcre2BacktrackingCondition(
                conditional.ConditionKind,
                [],
                assertionProgramId,
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
                ResolveSubroutine(conditional.Target));
        }

        var captureSlots = conditional.Target.Kind == Pcre2BackreferenceTargetKind.Named
            ? ResolveNamedSlots(conditional.Target.Name)
            : [ResolveBackreference(conditional.Target)];
        _requiresCaptureState = true;
        return new Pcre2BacktrackingCondition(
            conditional.ConditionKind,
            captureSlots,
            0,
            default,
            0);
    }

    private int[] ResolveNamedSlots(string name)
    {
        if (!_nameSlots.TryGetValue(name, out var slots))
        {
            throw new Pcre2CompileException(
                "Reference to a non-existent capturing group.",
                Pcre2ErrorKind.UnrecognizedEscape);
        }

        return slots;
    }

    private int ResolveBackreference(Pcre2BackreferenceTarget target)
    {
        var slot = target.Kind switch
        {
            Pcre2BackreferenceTargetKind.Absolute => target.Number,
            Pcre2BackreferenceTargetKind.Relative when target.Number > 0 =>
                target.CaptureCountAtReference + target.Number,
            Pcre2BackreferenceTargetKind.Relative =>
                target.CaptureCountAtReference + target.Number + 1,
            _ => 0,
        };
        if (slot <= 0 || slot > _captureCount)
        {
            throw new Pcre2CompileException("Reference to a non-existent capturing group.", Pcre2ErrorKind.UnrecognizedEscape);
        }

        return slot;
    }

    private void EmitBackreference(Pcre2BackreferenceBacktrackingNode backreference)
    {
        if (backreference.Target.Kind != Pcre2BackreferenceTargetKind.Named)
        {
            _instructions.Add(Pcre2BacktrackingInstruction.CreateBackreference(
                ResolveBackreference(backreference.Target),
                backreference.Options));
            return;
        }

        if (!_nameSlots.TryGetValue(backreference.Target.Name, out var slots))
        {
            throw new Pcre2CompileException(
                "Reference to a non-existent capturing group.",
                Pcre2ErrorKind.UnrecognizedEscape);
        }

        if (slots.Length == 1)
        {
            _instructions.Add(Pcre2BacktrackingInstruction.CreateBackreference(slots[0], backreference.Options));
            return;
        }

        var slotSetId = _backreferenceSlotSets.Count;
        _backreferenceSlotSets.Add(new Pcre2BackreferenceSlotSet(slots));
        _instructions.Add(Pcre2BacktrackingInstruction.CreateBackreferenceSlotSet(
            slotSetId,
            backreference.Options));
    }

    private int ResolveSubroutine(Pcre2BackreferenceTarget target)
    {
        var slot = target.Kind switch
        {
            Pcre2BackreferenceTargetKind.Absolute => target.Number,
            Pcre2BackreferenceTargetKind.Relative when target.Number > 0 =>
                target.CaptureCountAtReference + target.Number,
            Pcre2BackreferenceTargetKind.Relative =>
                target.CaptureCountAtReference + target.Number + 1,
            Pcre2BackreferenceTargetKind.Named when _nameSlots.TryGetValue(target.Name, out var slots) => slots[0],
            _ => -1,
        };
        if (slot < 0 || slot > _captureCount || slot > 0 && !_captureDefinitions.ContainsKey(slot))
        {
            throw new Pcre2CompileException(
                "Reference to a non-existent subroutine.",
                Pcre2ErrorKind.InvalidAfterParensQuery);
        }

        return slot;
    }

    private static bool ContainsDeferredControlVerb(IPcre2BacktrackingNode node) => node switch
    {
        Pcre2ControlVerbBacktrackingNode control => control.VerbKind is
            Pcre2ControlVerbKind.Commit or
            Pcre2ControlVerbKind.Prune or
            Pcre2ControlVerbKind.Skip or
            Pcre2ControlVerbKind.Then,
        Pcre2SequenceBacktrackingNode sequence => sequence.Children.Any(ContainsDeferredControlVerb),
        Pcre2AlternationBacktrackingNode alternation => alternation.Alternatives.Any(ContainsDeferredControlVerb),
        Pcre2RepeatBacktrackingNode repeat => ContainsDeferredControlVerb(repeat.Body),
        Pcre2CaptureBacktrackingNode capture => ContainsDeferredControlVerb(capture.Body),
        Pcre2AssertionBacktrackingNode assertion => ContainsDeferredControlVerb(assertion.Body),
        Pcre2ConditionalBacktrackingNode conditional =>
            conditional.Assertion is not null && ContainsDeferredControlVerb(conditional.Assertion.Body) ||
            ContainsDeferredControlVerb(conditional.YesBranch) ||
            ContainsDeferredControlVerb(conditional.NoBranch),
        Pcre2AtomicBacktrackingNode atomic => ContainsDeferredControlVerb(atomic.Body),
        _ => false,
    };

    private static bool ContainsMatchBoundaryReset(IPcre2BacktrackingNode node) => node switch
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

    private static bool SuppressesUnresetEmptyMatches(IPcre2BacktrackingNode node) =>
        node is Pcre2RepeatBacktrackingNode
        {
            Minimum: 0,
        } repeat && ContainsMatchBoundaryReset(repeat.Body);
}
