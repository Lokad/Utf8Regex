using System.Buffers;
using Lokad.Utf8Regex.Internal.Execution;

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
        MinimumLength = alternatives.Min(static alternative => alternative.Length);
        LeadingBytes = SearchValues.Create(
            alternatives
                .SelectMany(static alternative => alternative[0].GetMatchBytes())
                .Distinct()
                .ToArray());
    }

    public Pcre2DirectProgramKind Kind => Pcre2DirectProgramKind.Pcre2AsciiBoundedIsMatch;

    internal AsciiCharClass[][] Alternatives { get; }

    internal SearchValues<byte> LeadingBytes { get; }

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
        if (request.Options != Pcre2CompileOptions.None ||
            request.Settings.Newline != Pcre2NewlineConvention.Default ||
            request.Settings.Bsr != Pcre2BsrConvention.Default ||
            request.Settings.AllowDuplicateNames ||
            request.Settings.BackslashC != Pcre2BackslashCPolicy.Forbid ||
            request.Settings.AllowLookaroundBackslashK ||
            !TryGetMatchBody(root, out var matchBody, out var beginningOfSubject, out var endOfSubjectOrFinalNewline) ||
            !TryExpand(matchBody, out var alternatives) ||
            alternatives.Length == 0 ||
            alternatives.Any(static alternative => alternative.Length == 0))
        {
            return null;
        }

        return new Pcre2AsciiBoundedIsMatchDirectProgram(
            alternatives,
            beginningOfSubject,
            endOfSubjectOrFinalNewline,
            fallback);
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

        matchBody = first == 0 && last == sequence.Children.Length
            ? root
            : new Pcre2SequenceBacktrackingNode(sequence.Children[first..last]);
        return true;
    }

    internal static bool TryExpand(
        IPcre2BacktrackingNode node,
        out AsciiCharClass[][] alternatives)
    {
        switch (node)
        {
            case Pcre2TokenBacktrackingNode token:
                if (TryGetAsciiClass(token.Token, out var asciiClass))
                {
                    alternatives = [[asciiClass]];
                    return true;
                }

                break;
            case Pcre2SequenceBacktrackingNode { Children.Length: > 0 } sequence:
                alternatives = [[]];
                foreach (var child in sequence.Children)
                {
                    if (!TryExpand(child, out var childAlternatives) ||
                        !TryConcatenate(alternatives, childAlternatives, out alternatives))
                    {
                        alternatives = [];
                        return false;
                    }
                }

                return true;
            case Pcre2AlternationBacktrackingNode { Alternatives.Length: > 0 } alternation:
            {
                var expanded = new List<AsciiCharClass[]>();
                foreach (var branch in alternation.Alternatives)
                {
                    if (!TryExpand(branch, out var branchAlternatives) ||
                        expanded.Count + branchAlternatives.Length > MaximumAlternatives)
                    {
                        alternatives = [];
                        return false;
                    }

                    expanded.AddRange(branchAlternatives);
                }

                alternatives = [.. expanded];
                return true;
            }
            case Pcre2CaptureBacktrackingNode capture:
                return TryExpand(capture.Body, out alternatives);
            case Pcre2RepeatBacktrackingNode
            {
                Maximum: < int.MaxValue,
                Preference: not Pcre2RepeatPreference.Possessive,
            } repeat when repeat.Maximum <= MaximumTokensPerAlternative:
                if (!TryExpand(repeat.Body, out var bodyAlternatives))
                {
                    break;
                }

                var repeated = new List<AsciiCharClass[]>();
                var firstCount = repeat.Preference == Pcre2RepeatPreference.Lazy
                    ? repeat.Minimum
                    : repeat.Maximum;
                var lastCount = repeat.Preference == Pcre2RepeatPreference.Lazy
                    ? repeat.Maximum
                    : repeat.Minimum;
                var increment = firstCount <= lastCount ? 1 : -1;
                for (var count = firstCount; ; count += increment)
                {
                    AsciiCharClass[][] states = [[]];
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

                alternatives = [.. repeated];
                return true;
        }

        alternatives = [];
        return false;
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

        product = new AsciiCharClass[left.Length * right.Length][];
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

                var combined = new AsciiCharClass[leftAlternative.Length + rightAlternative.Length];
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
            var relativeCandidate = input[scanOffset..].IndexOfAny(program.LeadingBytes);
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
