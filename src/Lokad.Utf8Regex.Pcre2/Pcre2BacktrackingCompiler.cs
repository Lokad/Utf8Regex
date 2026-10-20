using System.Buffers;
using Lokad.Utf8Regex.Internal;
using System.Text;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Input;
using Lokad.Utf8Regex.Internal.Search;

namespace Lokad.Utf8Regex.Pcre2;

internal sealed class Pcre2CompiledBacktrackingOutcome
{
    internal Pcre2CompiledBacktrackingOutcome(
        Pcre2BacktrackingSyntaxTree syntaxTree,
        Pcre2BacktrackingProgram program,
        bool requiresProfileOptions)
    {
        SyntaxTree = syntaxTree;
        Program = program;
        RequiresProfileOptions = requiresProfileOptions;
    }

    internal Pcre2BacktrackingSyntaxTree SyntaxTree { get; }

    internal Pcre2BacktrackingProgram Program { get; }

    internal bool RequiresProfileOptions { get; }
}

internal static class Pcre2BacktrackingCompiler
{
    // This compiler uses CLR recursion; the iterative Core parser has a different
    // fixed depth backstop. Check before entering the next recursive frame.
    internal const ulong MaximumRecursiveDepth = 256;

    internal static Utf8ConstructionBudget EnterDepth()
    {
        var budget = Utf8ConstructionScope.CurrentBudget;
        budget.ChargeWork(1);
        if (budget.Depth >= MaximumRecursiveDepth)
            throw new Utf8ResourceLimitExceededException(Utf8ResourceLimitKind.ConstructionDepth,
                MaximumRecursiveDepth, budget.Depth + 1);
        budget.EnterDepth();
        return budget;
    }

    internal static Pcre2CompiledBacktrackingOutcome? Compile(Pcre2CompileRequest request)
    {
        using var construction = Utf8ConstructionScope.Enter();
        // Coarse admission for transient AST vectors, capture/name maps and
        // metadata copies. Shared storage can be charged twice; no helper graph
        // or per-object ledger is retained. Instructions have separate exact
        // array/slot admission in the lowerer.
        construction.Budget.ChargeWork((ulong)request.Pattern.Length);
        construction.Budget.ReserveBytes(checked((ulong)request.Pattern.Length * 256));
        var parser = new Pcre2BacktrackingParser(request);
        if (!parser.TryParse(out var root))
        {
            return null;
        }

        var nameEntries = parser.NameEntries;
        var groupNames = parser.GroupNames;
        var lowerer = new Pcre2BacktrackingLowerer(
            parser.CaptureCount,
            nameEntries,
            parser.CaptureDefinitions,
            request,
            groupNames);
        var program = lowerer.Lower(root);
        construction.Complete();
        return new Pcre2CompiledBacktrackingOutcome(
            new Pcre2BacktrackingSyntaxTree(root),
            program,
            parser.RequiresProfileOptions);
    }
}

internal static class Pcre2BacktrackingAnalysis
{
    // This is deliberately narrower than general anchoring analysis. It proves
    // only that every successful path reaches a subject-start or first-match
    // position assertion before any input can be consumed. A failed attempt at
    // the caller's initial candidate therefore cannot be rescued by bumpalong.
    internal static bool RestrictsSearchToInitialCandidate(IPcre2BacktrackingNode node)
    {
        Utf8ConstructionScope.ChargeWork(1);
        return node switch
        {
            Pcre2TokenBacktrackingNode { Token: var token } => token.Kind switch
            {
                Pcre2CharacterTokenKind.BeginningOfSubject or
                Pcre2CharacterTokenKind.FirstMatchingPosition => true,
                Pcre2CharacterTokenKind.BeginningOfLine =>
                    (token.Options & Pcre2CharacterOptions.Multiline) == 0,
                _ => false,
            },
            Pcre2SequenceBacktrackingNode sequence => SequenceRestrictsSearch(sequence),
            Pcre2AlternationBacktrackingNode alternation =>
                alternation.Alternatives.All(RestrictsSearchToInitialCandidate),
            Pcre2RepeatBacktrackingNode { Minimum: > 0 } repeat =>
                RestrictsSearchToInitialCandidate(repeat.Body),
            Pcre2CaptureBacktrackingNode capture => RestrictsSearchToInitialCandidate(capture.Body),
            Pcre2AtomicBacktrackingNode atomic => RestrictsSearchToInitialCandidate(atomic.Body),
            _ => false,
        };
    }

    internal static int GetMinimumByteLength(
        IPcre2BacktrackingNode node,
        IReadOnlyDictionary<int, Pcre2CaptureBacktrackingNode> captureDefinitions,
        IReadOnlyDictionary<string, int[]> nameSlots) =>
        GetMinimumByteLength(node, captureDefinitions, nameSlots, []);

    internal static int GetMinimumByteLength(IPcre2BacktrackingNode node)
    {
        Utf8ConstructionScope.ChargeWork(1);
        return node switch
        {
            Pcre2EmptyBacktrackingNode => 0,
            Pcre2TokenBacktrackingNode token => ConsumesInput(token.Token.Kind) ? 1 : 0,
            Pcre2SequenceBacktrackingNode sequence => GetSequenceMinimum(sequence),
            Pcre2AlternationBacktrackingNode alternation => alternation.Alternatives.Min(GetMinimumByteLength),
            Pcre2RepeatBacktrackingNode repeat => SaturatingMultiply(GetMinimumByteLength(repeat.Body), repeat.Minimum),
            Pcre2CaptureBacktrackingNode capture => GetMinimumByteLength(capture.Body),
            Pcre2BackreferenceBacktrackingNode => 0,
            Pcre2AssertionBacktrackingNode => 0,
            Pcre2SubroutineCallBacktrackingNode => 0,
            Pcre2ConditionalBacktrackingNode conditional => Math.Min(
                GetMinimumByteLength(conditional.YesBranch),
                GetMinimumByteLength(conditional.NoBranch)),
            Pcre2AtomicBacktrackingNode atomic => GetMinimumByteLength(atomic.Body),
            Pcre2ControlVerbBacktrackingNode => 0,
            Pcre2MatchBoundaryResetBacktrackingNode => 0,
            _ => 0,
        };
    }

    private static int GetMinimumByteLength(
        IPcre2BacktrackingNode node,
        IReadOnlyDictionary<int, Pcre2CaptureBacktrackingNode> captureDefinitions,
        IReadOnlyDictionary<string, int[]> nameSlots,
        HashSet<int> activeCaptures)
    {
        var budget = Pcre2BacktrackingCompiler.EnterDepth();
        try
        {
            switch (node)
            {
                case Pcre2EmptyBacktrackingNode:
                    return 0;
                case Pcre2TokenBacktrackingNode token:
                    return ConsumesInput(token.Token.Kind) ? 1 : 0;
                case Pcre2SequenceBacktrackingNode sequence:
                    var sequenceMinimum = 0;
                    foreach (var child in sequence.Children)
                    {
                        sequenceMinimum = SaturatingAdd(
                            sequenceMinimum,
                            GetMinimumByteLength(child, captureDefinitions, nameSlots, activeCaptures));
                    }

                    return sequenceMinimum;
                case Pcre2AlternationBacktrackingNode alternation:
                    return alternation.Alternatives.Min(
                        child => GetMinimumByteLength(child, captureDefinitions, nameSlots, activeCaptures));
                case Pcre2RepeatBacktrackingNode repeat:
                    return SaturatingMultiply(
                        GetMinimumByteLength(repeat.Body, captureDefinitions, nameSlots, activeCaptures),
                        repeat.Minimum);
                case Pcre2CaptureBacktrackingNode capture:
                    return GetMinimumByteLength(capture.Body, captureDefinitions, nameSlots, activeCaptures);
                case Pcre2BackreferenceBacktrackingNode backreference:
                    return GetBackreferenceMinimum(backreference.Target, captureDefinitions, nameSlots, activeCaptures);
                case Pcre2AssertionBacktrackingNode:
                    return 0;
                case Pcre2SubroutineCallBacktrackingNode subroutine:
                    var subroutineSlot = ResolveReferenceSlot(subroutine.Target);
                    return subroutineSlot > 0
                        ? GetCaptureMinimum(subroutineSlot, captureDefinitions, nameSlots, activeCaptures)
                        : 0;
                case Pcre2ConditionalBacktrackingNode conditional:
                    return Math.Min(
                        GetMinimumByteLength(conditional.YesBranch, captureDefinitions, nameSlots, activeCaptures),
                        GetMinimumByteLength(conditional.NoBranch, captureDefinitions, nameSlots, activeCaptures));
                case Pcre2AtomicBacktrackingNode atomic:
                    return GetMinimumByteLength(atomic.Body, captureDefinitions, nameSlots, activeCaptures);
                default:
                    return 0;
            }

        }
        finally { budget.LeaveDepth(); }
    }

    private static int GetBackreferenceMinimum(
        Pcre2BackreferenceTarget target,
        IReadOnlyDictionary<int, Pcre2CaptureBacktrackingNode> captureDefinitions,
        IReadOnlyDictionary<string, int[]> nameSlots,
        HashSet<int> activeCaptures)
    {
        if (target.Kind != Pcre2BackreferenceTargetKind.Named)
        {
            return GetCaptureMinimum(
                ResolveReferenceSlot(target),
                captureDefinitions,
                nameSlots,
                activeCaptures);
        }

        if (!nameSlots.TryGetValue(target.Name, out var slots) || slots.Length == 0)
        {
            return 0;
        }

        var minimum = int.MaxValue;
        foreach (var slot in slots)
        {
            minimum = Math.Min(
                minimum,
                GetCaptureMinimum(slot, captureDefinitions, nameSlots, activeCaptures));
        }

        return minimum == int.MaxValue ? 0 : minimum;
    }

    private static int GetCaptureMinimum(
        int slot,
        IReadOnlyDictionary<int, Pcre2CaptureBacktrackingNode> captureDefinitions,
        IReadOnlyDictionary<string, int[]> nameSlots,
        HashSet<int> activeCaptures)
    {
        if (slot <= 0 ||
            !captureDefinitions.TryGetValue(slot, out var capture) ||
            !activeCaptures.Add(slot))
        {
            return 0;
        }

        var minimum = GetMinimumByteLength(capture.Body, captureDefinitions, nameSlots, activeCaptures);
        _ = activeCaptures.Remove(slot);
        return minimum;
    }

    private static int ResolveReferenceSlot(Pcre2BackreferenceTarget target) => target.Kind switch
    {
        Pcre2BackreferenceTargetKind.Absolute => target.Number,
        Pcre2BackreferenceTargetKind.Relative when target.Number > 0 =>
            target.CaptureCountAtReference + target.Number,
        Pcre2BackreferenceTargetKind.Relative =>
            target.CaptureCountAtReference + target.Number + 1,
        _ => 0,
    };

    internal static int GetMinimumScalarLength(IPcre2BacktrackingNode node)
    {
        Utf8ConstructionScope.ChargeWork(1);
        return node switch
        {
            Pcre2EmptyBacktrackingNode => 0,
            Pcre2TokenBacktrackingNode token => ConsumesScalar(token.Token.Kind) ? 1 : 0,
            Pcre2SequenceBacktrackingNode sequence => sequence.Children.Aggregate(
                0,
                static (length, child) => SaturatingAdd(length, GetMinimumScalarLength(child))),
            Pcre2AlternationBacktrackingNode alternation => alternation.Alternatives.Min(GetMinimumScalarLength),
            Pcre2RepeatBacktrackingNode repeat => SaturatingMultiply(GetMinimumScalarLength(repeat.Body), repeat.Minimum),
            Pcre2CaptureBacktrackingNode capture => GetMinimumScalarLength(capture.Body),
            Pcre2BackreferenceBacktrackingNode => 0,
            Pcre2AssertionBacktrackingNode => 0,
            Pcre2SubroutineCallBacktrackingNode => 0,
            Pcre2ConditionalBacktrackingNode conditional => Math.Min(
                GetMinimumScalarLength(conditional.YesBranch),
                GetMinimumScalarLength(conditional.NoBranch)),
            Pcre2AtomicBacktrackingNode atomic => GetMinimumScalarLength(atomic.Body),
            Pcre2ControlVerbBacktrackingNode => 0,
            Pcre2MatchBoundaryResetBacktrackingNode => 0,
            _ => 0,
        };
    }

    internal static int GetMaximumScalarLength(IPcre2BacktrackingNode node)
    {
        Utf8ConstructionScope.ChargeWork(1);
        return node switch
        {
            Pcre2EmptyBacktrackingNode => 0,
            Pcre2TokenBacktrackingNode token => token.Token.Kind switch
            {
                Pcre2CharacterTokenKind.NewlineSequence => 2,
                Pcre2CharacterTokenKind.ExtendedGraphemeCluster => int.MaxValue,
                _ => ConsumesScalar(token.Token.Kind) ? 1 : 0,
            },
            Pcre2SequenceBacktrackingNode sequence => sequence.Children.Aggregate(
                0,
                static (length, child) => SaturatingAdd(length, GetMaximumScalarLength(child))),
            Pcre2AlternationBacktrackingNode alternation => alternation.Alternatives.Max(GetMaximumScalarLength),
            Pcre2RepeatBacktrackingNode repeat => SaturatingMultiply(GetMaximumScalarLength(repeat.Body), repeat.Maximum),
            Pcre2CaptureBacktrackingNode capture => GetMaximumScalarLength(capture.Body),
            Pcre2BackreferenceBacktrackingNode => int.MaxValue,
            Pcre2AssertionBacktrackingNode => 0,
            Pcre2SubroutineCallBacktrackingNode => int.MaxValue,
            Pcre2ConditionalBacktrackingNode conditional => Math.Max(
                GetMaximumScalarLength(conditional.YesBranch),
                GetMaximumScalarLength(conditional.NoBranch)),
            Pcre2AtomicBacktrackingNode atomic => GetMaximumScalarLength(atomic.Body),
            Pcre2ControlVerbBacktrackingNode => 0,
            Pcre2MatchBoundaryResetBacktrackingNode => 0,
            _ => int.MaxValue,
        };
    }

    internal static byte? GetLeadingAsciiByte(IPcre2BacktrackingNode node)
    {
        return TryGetLeadingAsciiByte(node, out var leading) ? leading : null;
    }

    internal static bool StartsWithExtendedGraphemeCluster(IPcre2BacktrackingNode node)
    {
        Utf8ConstructionScope.ChargeWork(1);
        return node switch
        {
            Pcre2TokenBacktrackingNode token =>
                token.Token.Kind == Pcre2CharacterTokenKind.ExtendedGraphemeCluster,
            Pcre2SequenceBacktrackingNode { Children.Length: > 0 } sequence =>
                StartsWithExtendedGraphemeCluster(sequence.Children[0]),
            Pcre2AlternationBacktrackingNode alternation =>
                alternation.Alternatives.All(StartsWithExtendedGraphemeCluster),
            Pcre2RepeatBacktrackingNode { Minimum: > 0 } repeat =>
                StartsWithExtendedGraphemeCluster(repeat.Body),
            Pcre2CaptureBacktrackingNode capture => StartsWithExtendedGraphemeCluster(capture.Body),
            Pcre2AtomicBacktrackingNode atomic => StartsWithExtendedGraphemeCluster(atomic.Body),
            _ => false,
        };
    }

    internal static bool ContainsCodeUnit(IPcre2BacktrackingNode node)
    {
        Utf8ConstructionScope.ChargeWork(1);
        return node switch
        {
            Pcre2TokenBacktrackingNode token => token.Token.Kind == Pcre2CharacterTokenKind.CodeUnit,
            Pcre2SequenceBacktrackingNode sequence => sequence.Children.Any(ContainsCodeUnit),
            Pcre2AlternationBacktrackingNode alternation => alternation.Alternatives.Any(ContainsCodeUnit),
            Pcre2RepeatBacktrackingNode repeat => ContainsCodeUnit(repeat.Body),
            Pcre2CaptureBacktrackingNode capture => ContainsCodeUnit(capture.Body),
            Pcre2AssertionBacktrackingNode assertion => ContainsCodeUnit(assertion.Body),
            Pcre2ConditionalBacktrackingNode conditional =>
                conditional.Assertion is not null && ContainsCodeUnit(conditional.Assertion.Body) ||
                ContainsCodeUnit(conditional.YesBranch) ||
                ContainsCodeUnit(conditional.NoBranch),
            Pcre2AtomicBacktrackingNode atomic => ContainsCodeUnit(atomic.Body),
            _ => false,
        };
    }

    private static int GetSequenceMinimum(Pcre2SequenceBacktrackingNode sequence)
    {
        var result = 0;
        foreach (var child in sequence.Children)
        {
            result = SaturatingAdd(result, GetMinimumByteLength(child));
        }

        return result;
    }

    private static bool SequenceRestrictsSearch(Pcre2SequenceBacktrackingNode sequence)
    {
        foreach (var child in sequence.Children)
        {
            if (RestrictsSearchToInitialCandidate(child))
            {
                return true;
            }

            if (CanConsume(child))
            {
                return false;
            }
        }

        return false;
    }

    private static bool TryGetLeadingAsciiByte(IPcre2BacktrackingNode node, out byte leading)
    {
        Utf8ConstructionScope.ChargeWork(1);
        switch (node)
        {
            case Pcre2TokenBacktrackingNode token when
                token.Token.Kind == Pcre2CharacterTokenKind.Literal &&
                token.Token.Literal.IsAscii &&
                (token.Token.Options & Pcre2CharacterOptions.Caseless) == 0:
                leading = (byte)token.Token.Literal.Value;
                return true;
            case Pcre2SequenceBacktrackingNode sequence:
                foreach (var child in sequence.Children)
                {
                    if (TryGetLeadingAsciiByte(child, out leading))
                    {
                        return true;
                    }

                    if (CanConsume(child))
                    {
                        break;
                    }
                }
                break;
            case Pcre2AlternationBacktrackingNode alternation:
                byte? common = null;
                foreach (var alternative in alternation.Alternatives)
                {
                    if (!TryGetLeadingAsciiByte(alternative, out var branchLeading))
                    {
                        leading = 0;
                        return false;
                    }

                    if (common is { } value && value != branchLeading)
                    {
                        leading = 0;
                        return false;
                    }

                    common = branchLeading;
                }

                if (common is { } commonValue)
                {
                    leading = commonValue;
                    return true;
                }
                break;
            case Pcre2RepeatBacktrackingNode repeat when repeat.Minimum > 0:
                return TryGetLeadingAsciiByte(repeat.Body, out leading);
            case Pcre2CaptureBacktrackingNode capture:
                return TryGetLeadingAsciiByte(capture.Body, out leading);
            case Pcre2AssertionBacktrackingNode:
                break;
            case Pcre2ConditionalBacktrackingNode conditional:
                if (TryGetLeadingAsciiByte(conditional.YesBranch, out var yesLeading) &&
                    TryGetLeadingAsciiByte(conditional.NoBranch, out var noLeading) &&
                    yesLeading == noLeading)
                {
                    leading = yesLeading;
                    return true;
                }
                break;
            case Pcre2AtomicBacktrackingNode atomic:
                return TryGetLeadingAsciiByte(atomic.Body, out leading);
        }

        leading = 0;
        return false;
    }

    private static bool ConsumesScalar(Pcre2CharacterTokenKind kind) => kind is
        Pcre2CharacterTokenKind.Literal or
        Pcre2CharacterTokenKind.CharacterClass or
        Pcre2CharacterTokenKind.Any or
        Pcre2CharacterTokenKind.AnyNotNewline or
        Pcre2CharacterTokenKind.NewlineSequence or
        Pcre2CharacterTokenKind.ExtendedGraphemeCluster;

    private static bool ConsumesInput(Pcre2CharacterTokenKind kind) =>
        ConsumesScalar(kind) || kind == Pcre2CharacterTokenKind.CodeUnit;

    private static bool CanConsume(IPcre2BacktrackingNode node)
    {
        Utf8ConstructionScope.ChargeWork(1);
        return node switch
        {
            Pcre2EmptyBacktrackingNode => false,
            Pcre2TokenBacktrackingNode token => ConsumesInput(token.Token.Kind),
            Pcre2SequenceBacktrackingNode sequence => sequence.Children.Any(CanConsume),
            Pcre2AlternationBacktrackingNode alternation => alternation.Alternatives.Any(CanConsume),
            Pcre2RepeatBacktrackingNode repeat => repeat.Maximum > 0 && CanConsume(repeat.Body),
            Pcre2CaptureBacktrackingNode capture => CanConsume(capture.Body),
            Pcre2BackreferenceBacktrackingNode => true,
            Pcre2AssertionBacktrackingNode => false,
            Pcre2SubroutineCallBacktrackingNode => true,
            Pcre2ConditionalBacktrackingNode conditional =>
                CanConsume(conditional.YesBranch) || CanConsume(conditional.NoBranch),
            Pcre2AtomicBacktrackingNode atomic => CanConsume(atomic.Body),
            Pcre2ControlVerbBacktrackingNode => false,
            Pcre2MatchBoundaryResetBacktrackingNode => false,
            _ => true,
        };
    }

    private static int SaturatingAdd(int left, int right) =>
        left > int.MaxValue - right ? int.MaxValue : left + right;

    private static int SaturatingMultiply(int value, int multiplier) =>
        value == 0 || multiplier == 0
            ? 0
            : value > int.MaxValue / multiplier ? int.MaxValue : value * multiplier;
}
