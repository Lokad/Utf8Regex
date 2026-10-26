using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal;
using Lokad.Utf8Regex.Internal.FrontEnd;

namespace Lokad.Utf8Regex.Pcre2;

internal static class Pcre2FiniteLiteralLanguageAnalyzer
{
    private const int MaximumAlternatives = 64;
    private const int MaximumLiteralCharacters = 4096;

    private sealed record Pcre2FiniteLiteralCapture(string Name, string Value);

    private sealed record Pcre2FiniteLiteralState(
        string Value,
        int ReportedStartCharacterIndex,
        Dictionary<int, Pcre2FiniteLiteralCapture> Captures);

    internal static Pcre2FiniteLiteralLanguageCompilation? TryCompile(
        IPcre2BacktrackingNode root,
        Pcre2CompileRequest request)
    {
        if (request.Options != Pcre2CompileOptions.None)
        {
            return null;
        }

        using var construction = Utf8ConstructionScope.Enter();
        var budget = construction.Budget;
        // Coarse expansion capacity covers state/list overhead and sharing again.
        // Hold these conservative loans until eager construction completes.
        Admit(256, 1);
        var trackCaptures = NeedsCaptures(root);

        // Large or non-finite languages retain the original VM instead of
        // paying combinatorial construction cost for an auxiliary plan.
        var initialStates = new List<Pcre2FiniteLiteralState>
        {
            new(string.Empty, 0, []),
        };
        // An atomic group can be flattened only when it owns the root: without
        // a following continuation, no later failure can revisit its choice.
        var evaluationRoot = root is Pcre2AtomicBacktrackingNode rootAtomic
            ? rootAtomic.Body
            : root;
        if (!TryEvaluate(evaluationRoot, initialStates, out var states) ||
            states.Count == 0 ||
            states.Any(static state => state.Value.Length == 0))
        {
            return null;
        }

        Admit(256ul * ((ulong)states.Count + 1), (ulong)states.Count);
        var distinctValues = new HashSet<string>(StringComparer.Ordinal);
        var alternatives = new List<Pcre2FiniteLiteralState>(states.Count);
        foreach (var state in states)
        {
            budget.ChargeWork((ulong)state.Value.Length + 1);
            if (distinctValues.Add(state.Value))
            {
                alternatives.Add(state);
            }
        }

        var alternativeBytes = budget.AllocateArray<byte[]>(alternatives.Count, false);
        using var pattern = Utf8ConstructionText.Create();
        if (alternatives.Count > 1) pattern.Append("(?:");
        for (var index = 0; index < alternatives.Count; index++)
        {
            var value = alternatives[index].Value;
            alternativeBytes[index] = budget.EncodeUtf8(value);
            if (index != 0) pattern.Append('|');
            foreach (var character in value) pattern.AppendEscaped(character);
        }
        if (alternatives.Count > 1) pattern.Append(')');
        var regex = new Utf8Regex(pattern.ToText(), RegexOptions.CultureInvariant, request.MatchTimeout, request.CoreResourcePolicy);
        Pcre2FiniteLiteralBoundaryProjection? boundaryProjection = null;
        if (alternatives.Any(static state => state.ReportedStartCharacterIndex != 0))
        {
            var boundaries = budget.AllocateArray<Pcre2FiniteLiteralBoundaryAlternative>(alternatives.Count, false);
            for (var index = 0; index < alternatives.Count; index++)
            {
                var state = alternatives[index];
                budget.ChargeWork((ulong)state.ReportedStartCharacterIndex + 1);
                boundaries[index] = new(alternativeBytes[index],
                    Encoding.UTF8.GetByteCount(state.Value.AsSpan(0, state.ReportedStartCharacterIndex)));
            }
            boundaryProjection = budget.AllocateObject(boundaries, static values => new Pcre2FiniteLiteralBoundaryProjection(values));
        }
        var search = budget.AllocateObject(alternativeBytes, static values => new Pcre2FiniteLiteralBooleanSearch(values));
        var result = budget.AllocateObject((regex, search, boundaryProjection),
            static state => new Pcre2FiniteLiteralLanguageCompilation(state.regex, state.search, state.boundaryProjection));
        construction.Complete();
        return result;

        void Admit(ulong bytes, ulong work)
        {
            budget.CheckBytes(bytes);
            budget.ChargeWork(work);
            budget.ReserveBytes(bytes);
        }

        bool NeedsCaptures(IPcre2BacktrackingNode node)
        {
            var depth = Pcre2BacktrackingCompiler.EnterDepth();
            try
            {
                return node switch
                {
                    Pcre2BackreferenceBacktrackingNode => true,
                    Pcre2SequenceBacktrackingNode sequence => sequence.Children.Any(NeedsCaptures),
                    Pcre2AlternationBacktrackingNode alternation => alternation.Alternatives.Any(NeedsCaptures),
                    Pcre2RepeatBacktrackingNode repeat => NeedsCaptures(repeat.Body),
                    Pcre2CaptureBacktrackingNode capture => NeedsCaptures(capture.Body),
                    Pcre2AtomicBacktrackingNode atomic => NeedsCaptures(atomic.Body),
                    _ => false,
                };
            }
            finally { depth.LeaveDepth(); }
        }

        bool TryEvaluate(
            IPcre2BacktrackingNode node,
            List<Pcre2FiniteLiteralState> inputs,
            out List<Pcre2FiniteLiteralState> outputs)
        {
            var depth = Pcre2BacktrackingCompiler.EnterDepth();
            try
            {
                Admit(256ul * ((ulong)inputs.Count + 1), (ulong)inputs.Count + 1);
                outputs = [];
                switch (node)
                {
                    case Pcre2TokenBacktrackingNode token
                        when token.Token.Kind == Pcre2CharacterTokenKind.Literal &&
                             token.Token.Options == Pcre2CharacterOptions.None:
                        var literal = token.Token.Literal.ToString();
                        foreach (var input in inputs)
                        {
                            if (input.Value.Length > MaximumLiteralCharacters - literal.Length)
                            {
                                return false;
                            }

                            Admit(Utf8ConstructionBudget.StringBytes(input.Value.Length + literal.Length),
                                (ulong)input.Value.Length + (ulong)literal.Length);
                            outputs.Add(new Pcre2FiniteLiteralState(
                                input.Value + literal,
                                input.ReportedStartCharacterIndex,
                                input.Captures));
                        }
                        return true;

                    case Pcre2SequenceBacktrackingNode sequence:
                        outputs = inputs;
                        foreach (var child in sequence.Children)
                        {
                            var childOutputs = new List<Pcre2FiniteLiteralState>();
                            foreach (var input in outputs)
                            {
                                if (!TryEvaluate(child, [input], out var inputOutputs))
                                {
                                    return false;
                                }

                                childOutputs.AddRange(inputOutputs);
                                if (childOutputs.Count > MaximumAlternatives)
                                {
                                    return false;
                                }
                            }

                            outputs = childOutputs;
                        }
                        return true;

                    case Pcre2AlternationBacktrackingNode alternation:
                        foreach (var input in inputs)
                        {
                            foreach (var alternative in alternation.Alternatives)
                            {
                                if (!TryEvaluate(alternative, [input], out var alternativeOutputs))
                                {
                                    return false;
                                }

                                outputs.AddRange(alternativeOutputs);
                                if (outputs.Count > MaximumAlternatives)
                                {
                                    return false;
                                }
                            }
                        }
                        return true;

                    case Pcre2RepeatBacktrackingNode repeat
                        when repeat.Maximum != int.MaxValue &&
                             repeat.Maximum <= MaximumAlternatives &&
                             repeat.Preference != Pcre2RepeatPreference.Possessive:
                        foreach (var input in inputs)
                        {
                            var statesByCount = new List<List<Pcre2FiniteLiteralState>>(repeat.Maximum + 1)
                            {
                                new() { input },
                            };
                            HashSet<int>? bodyRelativeLengths = repeat.Preference == Pcre2RepeatPreference.Greedy &&
                                repeat.Minimum != repeat.Maximum
                                ? new HashSet<int>()
                                : null;
                            for (var count = 1; count <= repeat.Maximum; count++)
                            {
                                var repeatedStates = new List<Pcre2FiniteLiteralState>();
                                foreach (var state in statesByCount[count - 1])
                                {
                                    if (!TryEvaluate(repeat.Body, [state], out var bodyOutputs) ||
                                        bodyOutputs.Any(output => output.Value.Length == state.Value.Length))
                                    {
                                        return false;
                                    }

                                    repeatedStates.AddRange(bodyOutputs);
                                    if (count == 1 && bodyRelativeLengths is not null)
                                    {
                                        foreach (var bodyOutput in bodyOutputs)
                                        {
                                            bodyRelativeLengths.Add(bodyOutput.Value.Length - state.Value.Length);
                                        }
                                    }
                                    if (repeatedStates.Count > MaximumAlternatives)
                                    {
                                        return false;
                                    }
                                }

                                statesByCount.Add(repeatedStates);
                            }

                            // R05: greedy ranged repeats over mixed-length derivations would
                            // project longest spans where PCRE2 stops ordered repetition at first
                            // success (probed: (?:a|ab){2,3} enum aaba vs oracle aa). Decline those
                            // shapes to the backtracking program (the slice overlay converts where
                            // it serves); exact counts resolve it1-major like backtracking, uniform
                            // derivations cannot diverge, and lazy starts at minimum like the oracle.
                            if (bodyRelativeLengths is { Count: > 1 })
                            {
                                return false;
                            }

                            var firstCount = repeat.Preference == Pcre2RepeatPreference.Greedy
                                ? repeat.Maximum
                                : repeat.Minimum;
                            var lastCount = repeat.Preference == Pcre2RepeatPreference.Greedy
                                ? repeat.Minimum
                                : repeat.Maximum;
                            var step = repeat.Preference == Pcre2RepeatPreference.Greedy ? -1 : 1;
                            for (var count = firstCount; ; count += step)
                            {
                                outputs.AddRange(statesByCount[count]);
                                if (outputs.Count > MaximumAlternatives)
                                {
                                    return false;
                                }

                                if (count == lastCount)
                                {
                                    break;
                                }
                            }
                        }
                        return true;

                    case Pcre2CaptureBacktrackingNode capture:
                        foreach (var input in inputs)
                        {
                            if (!TryEvaluate(capture.Body, [input], out var captureOutputs))
                            {
                                return false;
                            }

                            if (!trackCaptures)
                            {
                                outputs.AddRange(captureOutputs);
                                continue;
                            }
                            foreach (var captureOutput in captureOutputs)
                            {
                                // Snapshot only reference state, with one coarse major
                                // map-capacity allowance including growth/copy overlap.
                                var capturedLength = captureOutput.Value.Length - input.Value.Length;
                                Admit(256ul * ((ulong)captureOutput.Captures.Count + 1) +
                                    Utf8ConstructionBudget.StringBytes(capturedLength),
                                    (ulong)captureOutput.Captures.Count + (ulong)capturedLength + 1);
                                var captures = new Dictionary<int, Pcre2FiniteLiteralCapture>(captureOutput.Captures)
                                {
                                    [capture.Slot] = new(
                                        capture.Name,
                                        captureOutput.Value[input.Value.Length..]),
                                };
                                outputs.Add(new Pcre2FiniteLiteralState(
                                    captureOutput.Value,
                                    captureOutput.ReportedStartCharacterIndex,
                                    captures));
                                if (outputs.Count > MaximumAlternatives)
                                {
                                    return false;
                                }
                            }
                        }
                        return true;

                    case Pcre2BackreferenceBacktrackingNode backreference
                        when backreference.Options == Pcre2CharacterOptions.None:
                        foreach (var input in inputs)
                        {
                        budget.ChargeWork((ulong)input.Captures.Count + 1);
                            var slot = backreference.Target.Kind switch
                            {
                                Pcre2BackreferenceTargetKind.Absolute => backreference.Target.Number,
                                Pcre2BackreferenceTargetKind.Relative when backreference.Target.Number > 0 =>
                                    backreference.Target.CaptureCountAtReference + backreference.Target.Number,
                                Pcre2BackreferenceTargetKind.Relative =>
                                    backreference.Target.CaptureCountAtReference + backreference.Target.Number + 1,
                                Pcre2BackreferenceTargetKind.Named => input.Captures
                                    .Where(pair => string.Equals(
                                        pair.Value.Name,
                                        backreference.Target.Name,
                                        StringComparison.Ordinal))
                                    .Select(static pair => pair.Key)
                                    .DefaultIfEmpty()
                                    .Min(),
                                _ => 0,
                            };
                            if (slot == 0 || !input.Captures.TryGetValue(slot, out var captured))
                            {
                                continue;
                            }

                            if (input.Value.Length > MaximumLiteralCharacters - captured.Value.Length)
                            {
                                return false;
                            }

                            Admit(Utf8ConstructionBudget.StringBytes(input.Value.Length + captured.Value.Length),
                                (ulong)input.Value.Length + (ulong)captured.Value.Length);
                            outputs.Add(new Pcre2FiniteLiteralState(
                                input.Value + captured.Value,
                                input.ReportedStartCharacterIndex,
                                input.Captures));
                        }
                        return true;

                    case Pcre2MatchBoundaryResetBacktrackingNode:
                        foreach (var input in inputs)
                        {
                            outputs.Add(input with { ReportedStartCharacterIndex = input.Value.Length });
                        }
                        return true;

                    default:
                        return false;
                }
            }
            finally { depth.LeaveDepth(); }
        }
    }
}
