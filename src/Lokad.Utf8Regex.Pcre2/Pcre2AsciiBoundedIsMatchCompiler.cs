using Lokad.Utf8Regex.Internal;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Search;

namespace Lokad.Utf8Regex.Pcre2;

internal sealed class Pcre2AsciiBoundedIsMatchDirectProgram : IPcre2DirectProgram
{
    internal Pcre2AsciiBoundedIsMatchDirectProgram(
        AsciiCharClass[][] alternatives,
        bool beginningOfSubject,
        bool endOfSubjectOrFinalNewline,
        Pcre2BacktrackingProgram fallback)
    {
        Alternatives = alternatives;
        BeginningOfSubject = beginningOfSubject;
        EndOfSubjectOrFinalNewline = endOfSubjectOrFinalNewline;
        Fallback = fallback;
        using var scope = Utf8ConstructionScope.Enter();
        var budget = scope.Budget; budget.EnterDepth();
        try
        {
            budget.ChargeWork(2ul * (ulong)alternatives.Length);
            MinimumLength = alternatives.Min(static alternative => alternative.Length);
            ulong low = 0, high = 0;
            foreach (var alternative in alternatives)
            {
                low |= alternative[0].ActualLowMask;
                high |= alternative[0].ActualHighMask;
            }
            LeadingBytes = PreparedByteSet.CreateOwned(low, high, 0, 0);
            scope.Complete();
        }
        finally { budget.LeaveDepth(); }
    }

    public Pcre2DirectProgramKind Kind => Pcre2DirectProgramKind.Pcre2AsciiBoundedIsMatch;

    internal AsciiCharClass[][] Alternatives { get; }

    internal PreparedByteSet LeadingBytes { get; }

    internal int MinimumLength { get; }

    internal bool BeginningOfSubject { get; }

    internal bool EndOfSubjectOrFinalNewline { get; }

    internal Pcre2BacktrackingProgram Fallback { get; }
}

internal static class Pcre2AsciiBoundedIsMatchAnalyzer
{
    // Boolean execution needs only the accepted finite byte language: capture
    // state and greedy/lazy ordering cannot change whether any alternative
    // succeeds. The caps keep construction linear in a deliberately small
    // auxiliary plan instead of expanding arbitrary bounded expressions.
    private const int MaximumAlternatives = 64;
    private const int MaximumTokensPerAlternative = 128;

    internal static Pcre2AsciiBoundedIsMatchDirectProgram? TryCompile(
        IPcre2BacktrackingNode root,
        Pcre2CompileRequest request,
        Pcre2BacktrackingProgram fallback)
    {
        using var construction = Utf8ConstructionScope.Enter();
        if (request.Options != Pcre2CompileOptions.None ||
            request.Settings.Newline != Pcre2NewlineConvention.Default ||
            request.Settings.Bsr != Pcre2BsrConvention.Default ||
            request.Settings.AllowDuplicateNames ||
            request.Settings.BackslashC != Pcre2BackslashCPolicy.Forbid ||
            request.Settings.AllowLookaroundBackslashK ||
            !TryGetMatchBody(root, out var matchBody, out var beginningOfSubject, out var endOfSubjectOrFinalNewline) ||
            !TryExpand(matchBody, fallback, out var alternatives) ||
            alternatives.Length == 0 ||
            alternatives.Any(static alternative => alternative.Length == 0))
        {
            return null;
        }

        var result = construction.Budget.AllocateObject(
            (alternatives, beginningOfSubject, endOfSubjectOrFinalNewline, fallback),
            static state => new Pcre2AsciiBoundedIsMatchDirectProgram(state.alternatives,
                state.beginningOfSubject, state.endOfSubjectOrFinalNewline, state.fallback));
        construction.Complete();
        return result;
    }

    internal static bool TryGetMatchBody(
        IPcre2BacktrackingNode root,
        out IPcre2BacktrackingNode matchBody,
        out bool beginningOfSubject,
        out bool endOfSubjectOrFinalNewline)
    {
        beginningOfSubject = false;
        endOfSubjectOrFinalNewline = false;
        if (root is not Pcre2SequenceBacktrackingNode sequence)
        {
            matchBody = root;
            return true;
        }

        var first = 0;
        var last = sequence.Children.Length;
        if (first < last &&
            sequence.Children[first] is Pcre2TokenBacktrackingNode
            {
                Token:
                {
                    Kind: Pcre2CharacterTokenKind.BeginningOfLine,
                    Options: Pcre2CharacterOptions.None,
                },
            })
        {
            beginningOfSubject = true;
            first++;
        }

        if (first < last &&
            sequence.Children[last - 1] is Pcre2TokenBacktrackingNode
            {
                Token:
                {
                    Kind: Pcre2CharacterTokenKind.EndOfLine,
                    Options: Pcre2CharacterOptions.None,
                },
            })
        {
            endOfSubjectOrFinalNewline = true;
            last--;
        }

        if (first >= last)
        {
            matchBody = root;
            return false;
        }

        matchBody = root;
        if (first != 0 || last != sequence.Children.Length)
        {
            var budget = Utf8ConstructionScope.CurrentBudget;
            var children = budget.AllocateArray<IPcre2BacktrackingNode>(last - first, false);
            budget.ChargeWork((ulong)children.Length);
            sequence.Children.AsSpan(first, children.Length).CopyTo(children);
            matchBody = budget.AllocateObject(children, static nodes => new Pcre2SequenceBacktrackingNode(nodes));
        }
        return true;
    }

    private static bool TryExpand(IPcre2BacktrackingNode node, Pcre2BacktrackingProgram program, out AsciiCharClass[][] alternatives)
    {
        using var construction = Utf8ConstructionScope.Enter();
        var depth = Pcre2BacktrackingCompiler.EnterDepth();
        try
        {
            var result = TryExpandCore(node, program, construction.Budget, out alternatives);
            if (result) construction.Complete();
            return result;
        }
        finally { depth.LeaveDepth(); }
    }

    private static bool TryExpandCore(IPcre2BacktrackingNode node, Pcre2BacktrackingProgram program, Utf8ConstructionBudget budget,
        out AsciiCharClass[][] alternatives)
    {
        switch (node)
        {
            case Pcre2TokenBacktrackingNode token:
                if (TryGetAsciiClass(token.Token, out var asciiClass))
                {
                    alternatives = budget.AllocateArray<AsciiCharClass[]>(1, false);
                    alternatives[0] = budget.AllocateArray<AsciiCharClass>(1, false);
                    alternatives[0][0] = asciiClass;
                    return true;
                }

                break;
            case Pcre2SequenceBacktrackingNode { Children.Length: > 0 } sequence:
                alternatives = budget.AllocateArray<AsciiCharClass[]>(1, false);
                alternatives[0] = [];
                foreach (var child in sequence.Children)
                {
                    if (!TryExpand(child, program, out var childAlternatives) ||
                        !TryConcatenate(alternatives, childAlternatives, out alternatives))
                    {
                        alternatives = [];
                        return false;
                    }
                }

                return true;
            case Pcre2AlternationBacktrackingNode { Alternatives.Length: > 0 } alternation:
            {
                using var expanded = new Utf8ConstructionBuffer<AsciiCharClass[]>(budget);
                foreach (var branch in alternation.Alternatives)
                {
                    if (!TryExpand(branch, program, out var branchAlternatives) ||
                        expanded.Count + branchAlternatives.Length > MaximumAlternatives)
                    {
                        alternatives = [];
                        return false;
                    }

                    expanded.AddRange(branchAlternatives);
                }

                alternatives = expanded.Take();
                return true;
            }
            case Pcre2CaptureBacktrackingNode capture:
                return TryExpand(capture.Body, program, out alternatives);
            case Pcre2ConditionalBacktrackingNode
            {
                ConditionKind: Pcre2BacktrackingConditionKind.Assertion,
                Assertion: { AssertionKind: Pcre2AssertionKind.PositiveLookahead or Pcre2AssertionKind.NegativeLookahead } assertion,
            } conditional:
            {
                var body = assertion.Body;
                while (true)
                {
                    // Terminal ACCEPT determines the assertion condition's
                    // truth just like reaching its end. Result operations still
                    // use the VM to preserve the conditional capture flow.
                    var prefix = body switch
                    {
                        Pcre2CaptureBacktrackingNode guardCapture => guardCapture.Body,
                        Pcre2SequenceBacktrackingNode
                        {
                            Children: [var beforeAccept, Pcre2ControlVerbBacktrackingNode { VerbKind: Pcre2ControlVerbKind.Accept, Name.Length: 0 }],
                        } => beforeAccept,
                        _ => null,
                    };
                    if (prefix is null) break;
                    budget.ChargeWork(1);
                    body = prefix;
                }
                if (body is not Pcre2TokenBacktrackingNode guard ||
                    !TryGetAsciiClass(guard.Token, out var guardClass) ||
                    !TryExpand(conditional.YesBranch, program, out var yes) ||
                    !TryExpand(conditional.NoBranch, program, out var no) ||
                    yes.Length + no.Length > MaximumAlternatives)
                    break;

                // Both branches must consume an ASCII byte here. Its membership
                // decides this one-token lookahead, so intersect the first class
                // rather than retaining an assertion or conditional executor.
                // Empty branches and observable/control-dependent guards decline.
                using var expanded = new Utf8ConstructionBuffer<AsciiCharClass[]>(budget);
                for (var branch = 0; branch < 2; branch++)
                {
                    var requireHit = (branch == 0) == (assertion.AssertionKind == Pcre2AssertionKind.PositiveLookahead);
                    var low = requireHit ? guardClass.ActualLowMask : ~guardClass.ActualLowMask;
                    var high = requireHit ? guardClass.ActualHighMask : ~guardClass.ActualHighMask;
                    foreach (var alternative in branch == 0 ? yes : no)
                    {
                        budget.ChargeWork(1);
                        if (alternative.Length == 0)
                        {
                            alternatives = [];
                            return false;
                        }
                        var first = new AsciiCharClass(alternative[0].ActualLowMask & low, alternative[0].ActualHighMask & high);
                        if (first.IsEmpty) continue;
                        alternative[0] = first;
                        expanded.Add(alternative);
                    }
                }
                alternatives = expanded.Take();
                return true;
            }
            case Pcre2SubroutineCallBacktrackingNode call:
                if (TryGetAsciiSubroutineClass(call.Target, program, out var callClass))
                {
                    alternatives = budget.AllocateArray<AsciiCharClass[]>(1, false);
                    alternatives[0] = budget.AllocateArray<AsciiCharClass>(1, false);
                    alternatives[0][0] = callClass;
                    return true;
                }
                break;
            case Pcre2RepeatBacktrackingNode
            {
                Maximum: < int.MaxValue,
                Preference: not Pcre2RepeatPreference.Possessive,
            } repeat when repeat.Maximum <= MaximumTokensPerAlternative:
            {
                if (!TryExpand(repeat.Body, program, out var bodyAlternatives))
                {
                    break;
                }

                using var repeated = new Utf8ConstructionBuffer<AsciiCharClass[]>(budget);
                var firstCount = repeat.Preference == Pcre2RepeatPreference.Lazy
                    ? repeat.Minimum
                    : repeat.Maximum;
                var lastCount = repeat.Preference == Pcre2RepeatPreference.Lazy
                    ? repeat.Maximum
                    : repeat.Minimum;
                var increment = firstCount <= lastCount ? 1 : -1;
                for (var count = firstCount; ; count += increment)
                {
                    var states = budget.AllocateArray<AsciiCharClass[]>(1, false);
                    states[0] = [];
                    for (var i = 0; i < count; i++)
                    {
                        if (!TryConcatenate(states, bodyAlternatives, out states))
                        {
                            alternatives = [];
                            return false;
                        }
                    }

                    if (repeated.Count + states.Length > MaximumAlternatives)
                    {
                        alternatives = [];
                        return false;
                    }

                    repeated.AddRange(states);
                    if (count == lastCount)
                    {
                        break;
                    }
                }

                alternatives = repeated.Take();
                return true;
            }
        }

        alternatives = [];
        return false;
    }

    private static bool TryGetAsciiSubroutineClass(
        Pcre2BackreferenceTarget target,
        Pcre2BacktrackingProgram program,
        out AsciiCharClass asciiClass)
    {
        asciiClass = default;
        var slot = target.Kind == Pcre2BackreferenceTargetKind.Named
            ? -1 : Pcre2BacktrackingAnalysis.ResolveReferenceSlot(target);
        if (target.Kind == Pcre2BackreferenceTargetKind.Named)
        {
            foreach (var name in program.NameEntries)
            {
                Utf8ConstructionScope.ChargeWork(1);
                if (string.Equals(name.Name, target.Name, StringComparison.Ordinal))
                {
                    slot = name.Number;
                    break;
                }
            }
        }
        if (slot <= 0 || (uint)slot >= (uint)program.SubroutineTargets.Length)
            return false;
        var entry = program.SubroutineTargets[slot];
        if (entry < 0 || entry > program.Instructions.Length - 4)
            return false;

        // Inspect the actual lowered target: this cannot recursively expand a
        // call, change its options, or overlook capture-dependent instructions.
        var body = program.Instructions.AsSpan(entry, 4);
        return body[0].Kind == Pcre2BacktrackingInstructionKind.CaptureStart && body[0].CaptureSlot == slot &&
            body[1].Kind == Pcre2BacktrackingInstructionKind.Token &&
            body[2].Kind == Pcre2BacktrackingInstructionKind.CaptureEnd && body[2].CaptureSlot == slot &&
            body[3].Kind == Pcre2BacktrackingInstructionKind.SubroutineReturn &&
            TryGetAsciiClass(body[1].Token, out asciiClass);
    }

    internal static bool TryConcatenate(
        AsciiCharClass[][] left,
        AsciiCharClass[][] right,
        out AsciiCharClass[][] product)
    {
        if ((long)left.Length * right.Length > MaximumAlternatives)
        {
            product = [];
            return false;
        }

        var budget = Utf8ConstructionScope.CurrentBudget;
        product = budget.AllocateArray<AsciiCharClass[]>(left.Length * right.Length, false);
        var productIndex = 0;
        foreach (var leftAlternative in left)
        {
            foreach (var rightAlternative in right)
            {
                if (leftAlternative.Length + rightAlternative.Length > MaximumTokensPerAlternative)
                {
                    product = [];
                    return false;
                }

                var combined = budget.AllocateArray<AsciiCharClass>(leftAlternative.Length + rightAlternative.Length, false);
                budget.ChargeWork((ulong)combined.Length + 1);
                leftAlternative.CopyTo(combined, 0);
                rightAlternative.CopyTo(combined, leftAlternative.Length);
                product[productIndex++] = combined;
            }
        }

        return true;
    }

    internal static bool TryGetAsciiClass(
        Pcre2CharacterToken token,
        out AsciiCharClass asciiClass)
    {
        if (token.Options != Pcre2CharacterOptions.None)
        {
            asciiClass = default;
            return false;
        }

        if (token.Kind == Pcre2CharacterTokenKind.Literal && token.Literal.IsAscii)
        {
            asciiClass = AsciiCharClass.ForByte((byte)token.Literal.Value);
            return true;
        }

        if (token.Kind == Pcre2CharacterTokenKind.CharacterClass &&
            !token.CharacterClass.Negated &&
            !token.CharacterClass.AsciiSet.IsEmpty &&
            token.CharacterClass.Terms.All(static term =>
                !term.Negated &&
                term.Kind switch
                {
                    Pcre2CharacterClassTermKind.Range => term.Range.High < 128,
                    Pcre2CharacterClassTermKind.Digit or
                        Pcre2CharacterClassTermKind.Space or
                        Pcre2CharacterClassTermKind.Word => true,
                    _ => false,
                }))
        {
            asciiClass = token.CharacterClass.AsciiSet;
            return true;
        }

        asciiClass = default;
        return false;
    }
}

internal static class Pcre2AsciiBoundedIsMatchRunner
{
    internal static bool IsMatch(
        Pcre2AsciiBoundedIsMatchDirectProgram program,
        ReadOnlySpan<byte> input,
        int startOffsetInBytes)
    {
        if (program.BeginningOfSubject)
        {
            return startOffsetInBytes == 0 && MatchesAnyAlternative(program, input, 0);
        }

        var scanOffset = startOffsetInBytes;
        while (scanOffset <= input.Length - program.MinimumLength)
        {
            var relativeCandidate = program.LeadingBytes.IndexOf(input[scanOffset..]);
            if (relativeCandidate < 0)
            {
                return false;
            }

            var candidate = scanOffset + relativeCandidate;
            if (MatchesAnyAlternative(program, input, candidate))
            {
                return true;
            }

            scanOffset = candidate + 1;
        }

        return false;
    }

    internal static bool MatchesAnyAlternative(
        Pcre2AsciiBoundedIsMatchDirectProgram program,
        ReadOnlySpan<byte> input,
        int candidate)
    {
        foreach (var alternative in program.Alternatives)
        {
            var matchEnd = candidate + alternative.Length;
            // With the default LF convention, `$` also accepts immediately
            // before one final line feed.
            if (candidate <= input.Length - alternative.Length &&
                (!program.EndOfSubjectOrFinalNewline ||
                 matchEnd == input.Length ||
                 matchEnd == input.Length - 1 && input[^1] == (byte)'\n') &&
                MatchesAt(input, candidate, alternative))
            {
                return true;
            }
        }

        return false;
    }

    internal static bool MatchesAt(
        ReadOnlySpan<byte> input,
        int candidate,
        AsciiCharClass[] alternative)
    {
        for (var i = 0; i < alternative.Length; i++)
        {
            if (!alternative[i].Contains(input[candidate + i]))
            {
                return false;
            }
        }

        return true;
    }
}
