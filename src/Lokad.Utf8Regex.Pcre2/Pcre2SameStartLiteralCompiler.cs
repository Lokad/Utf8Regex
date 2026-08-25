using Lokad.Utf8Regex.Internal.Input;

namespace Lokad.Utf8Regex.Pcre2;

internal sealed class Pcre2SameStartLiteralDirectProgram : IPcre2DirectProgram
{
    internal Pcre2SameStartLiteralDirectProgram(
        byte[] lookbehind,
        byte[][] nonEmptyAlternatives,
        Pcre2BacktrackingProgram fallback)
    {
        Lookbehind = lookbehind;
        NonEmptyAlternatives = nonEmptyAlternatives;
        Fallback = fallback;
    }

    public Pcre2DirectProgramKind Kind => Pcre2DirectProgramKind.Pcre2SameStartLiteral;

    internal byte[] Lookbehind { get; }

    internal byte[][] NonEmptyAlternatives { get; }

    internal Pcre2BacktrackingProgram Fallback { get; }
}

internal static class Pcre2SameStartLiteralAnalyzer
{
    internal static Pcre2SameStartLiteralDirectProgram? TryCompile(
        IPcre2BacktrackingNode root,
        Pcre2CompileRequest request,
        Pcre2BacktrackingProgram fallback)
    {
        if (request.Options != Pcre2CompileOptions.None ||
            root is not Pcre2SequenceBacktrackingNode { Children.Length: 2 } sequence ||
            sequence.Children[0] is not Pcre2AssertionBacktrackingNode
            {
                AssertionKind: Pcre2AssertionKind.PositiveLookbehind,
                Body: var lookbehindBody,
            } ||
            !Pcre2AsciiLiteralAlternationRepeatAnalyzer.IsNonEmptyAsciiLiteral(lookbehindBody))
        {
            return null;
        }

        var alternationBody = sequence.Children[1] is Pcre2CaptureBacktrackingNode capture
            ? capture.Body
            : sequence.Children[1];
        if (alternationBody is not Pcre2AlternationBacktrackingNode alternation ||
            alternation.Alternatives is not
            [
                Pcre2EmptyBacktrackingNode,
                .. var nonEmptyAlternatives,
            ] ||
            nonEmptyAlternatives.Length == 0 ||
            !nonEmptyAlternatives.All(Pcre2AsciiLiteralAlternationRepeatAnalyzer.IsNonEmptyAsciiLiteral))
        {
            return null;
        }

        return new Pcre2SameStartLiteralDirectProgram(
            Pcre2AsciiLiteralAlternationRepeatAnalyzer.ExtractAsciiLiteral(lookbehindBody),
            nonEmptyAlternatives
                .Select(Pcre2AsciiLiteralAlternationRepeatAnalyzer.ExtractAsciiLiteral)
                .ToArray(),
            fallback);
    }
}

internal static class Pcre2SameStartLiteralRunner
{
    internal static Pcre2DirectGlobalMatch Match(
        Pcre2SameStartLiteralDirectProgram program,
        ReadOnlySpan<byte> input,
        Utf8BytePosition start,
        Utf8BytePosition firstMatchingPosition,
        Pcre2MatchOptions options)
    {
        var candidate = -1;
        if ((options & Pcre2MatchOptions.Anchored) != 0)
        {
            var lookbehindStart = start.Value - program.Lookbehind.Length;
            if (lookbehindStart >= 0 && input[lookbehindStart..start.Value].SequenceEqual(program.Lookbehind))
            {
                candidate = start.Value;
            }
        }
        else
        {
            var searchFrom = Math.Max(0, start.Value - program.Lookbehind.Length);
            while (searchFrom <= input.Length - program.Lookbehind.Length)
            {
                var relative = input[searchFrom..].IndexOf(program.Lookbehind);
                if (relative < 0)
                {
                    break;
                }

                var lookbehindStart = searchFrom + relative;
                var lookbehindEnd = lookbehindStart + program.Lookbehind.Length;
                if (lookbehindEnd >= start.Value)
                {
                    candidate = lookbehindEnd;
                    break;
                }

                searchFrom = lookbehindStart + 1;
            }
        }

        if (candidate < 0)
        {
            return default;
        }

        var emptyDisallowed = (options & Pcre2MatchOptions.NotEmpty) != 0 ||
            ((options & Pcre2MatchOptions.NotEmptyAtStart) != 0 &&
             ((options & Pcre2MatchOptions.Anchored) != 0 || candidate == firstMatchingPosition.Value));
        if (!emptyDisallowed)
        {
            return new Pcre2DirectGlobalMatch(
                Success: true,
                StartOffsetInBytes: candidate,
                EndOffsetInBytes: candidate,
                ConsumedStartOffsetInBytes: candidate,
                ConsumedEndOffsetInBytes: candidate,
                MatchBoundaryWasReset: false);
        }

        foreach (var alternative in program.NonEmptyAlternatives)
        {
            if (input[candidate..].StartsWith(alternative))
            {
                return new Pcre2DirectGlobalMatch(
                    Success: true,
                    StartOffsetInBytes: candidate,
                    EndOffsetInBytes: candidate + alternative.Length,
                    ConsumedStartOffsetInBytes: candidate,
                    ConsumedEndOffsetInBytes: candidate + alternative.Length,
                    MatchBoundaryWasReset: false);
            }
        }

        return default;
    }
}
