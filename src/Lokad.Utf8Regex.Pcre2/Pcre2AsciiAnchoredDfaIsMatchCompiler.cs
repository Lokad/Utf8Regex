using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Pcre2;

internal sealed class Pcre2AsciiAnchoredDfaIsMatchDirectProgram : IPcre2DirectProgram
{
    internal Pcre2AsciiAnchoredDfaIsMatchDirectProgram(
        Utf8ByteSafeLazyDfaVerifierProgram dfa,
        Pcre2BacktrackingProgram fallback)
    {
        Dfa = dfa;
        Fallback = fallback;
    }

    public Pcre2DirectProgramKind Kind => Pcre2DirectProgramKind.Pcre2AsciiAnchoredDfaIsMatch;

    internal Utf8ByteSafeLazyDfaVerifierProgram Dfa { get; }

    internal Pcre2BacktrackingProgram Fallback { get; }
}

internal static class Pcre2AsciiAnchoredDfaIsMatchAnalyzer
{
    internal static Pcre2AsciiAnchoredDfaIsMatchDirectProgram? TryCompile(
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
            !Pcre2AsciiBoundedIsMatchAnalyzer.TryGetMatchBody(
                root,
                out var matchBody,
                out var beginningOfSubject,
                out var endOfSubjectOrFinalNewline) ||
            !beginningOfSubject ||
            !endOfSubjectOrFinalNewline)
        {
            return null;
        }

        var steps = new List<Utf8ByteSafeLinearVerifierStep>
        {
            Utf8ByteSafeLinearVerifierStep.RequireBeginning(RegexOptions.None),
        };
        var hasUnboundedRepeat = false;
        if (!TryAppend(matchBody, steps, ref hasUnboundedRepeat) || !hasUnboundedRepeat)
        {
            return null;
        }

        steps.Add(Utf8ByteSafeLinearVerifierStep.RequireEnd(RegexOptions.None));
        steps.Add(Utf8ByteSafeLinearVerifierStep.Accept());
        var linearProgram = new Utf8ByteSafeLinearVerifierProgram([.. steps]);
        var dfaOutcome = Utf8ByteSafeLazyDfaVerifierProgram.Compile(linearProgram);
        return dfaOutcome.Succeeded
            ? new Pcre2AsciiAnchoredDfaIsMatchDirectProgram(dfaOutcome.Program, fallback)
            : null;
    }

    internal static bool TryAppend(
        IPcre2BacktrackingNode node,
        List<Utf8ByteSafeLinearVerifierStep> steps,
        ref bool hasUnboundedRepeat)
    {
        switch (node)
        {
            case Pcre2EmptyBacktrackingNode:
                return true;
            case Pcre2TokenBacktrackingNode token:
                if (!Pcre2AsciiBoundedIsMatchAnalyzer.TryGetAsciiClass(
                        token.Token,
                        out var characterClass))
                {
                    return false;
                }

                steps.Add(Utf8ByteSafeLinearVerifierStep.MatchProjectedAsciiSet(
                    string.Empty,
                    characterClass));
                return true;
            case Pcre2SequenceBacktrackingNode sequence:
                foreach (var child in sequence.Children)
                {
                    if (!TryAppend(child, steps, ref hasUnboundedRepeat))
                    {
                        return false;
                    }
                }

                return true;
            case Pcre2CaptureBacktrackingNode capture:
                return TryAppend(capture.Body, steps, ref hasUnboundedRepeat);
            case Pcre2RepeatBacktrackingNode
            {
                Body: Pcre2TokenBacktrackingNode token,
                Preference: not Pcre2RepeatPreference.Possessive,
            } repeat:
                if (!Pcre2AsciiBoundedIsMatchAnalyzer.TryGetAsciiClass(
                        token.Token,
                        out var repeatedClass))
                {
                    return false;
                }

                hasUnboundedRepeat |= repeat.Maximum == int.MaxValue;
                steps.Add(Utf8ByteSafeLinearVerifierStep.LoopProjectedAsciiSet(
                    string.Empty,
                    repeatedClass,
                    repeat.Minimum,
                    repeat.Maximum == int.MaxValue ? -1 : repeat.Maximum));
                return true;
            default:
                return false;
        }
    }
}

internal static class Pcre2AsciiAnchoredDfaIsMatchRunner
{
    internal static bool IsMatch(
        Pcre2AsciiAnchoredDfaIsMatchDirectProgram program,
        ReadOnlySpan<byte> input,
        int startOffsetInBytes)
    {
        if (startOffsetInBytes != 0)
        {
            return false;
        }

        return IsAccepted(program, input) ||
            !input.IsEmpty &&
            input[^1] == (byte)'\n' &&
            IsAccepted(program, input[..^1]);
    }

    internal static bool IsAccepted(
        Pcre2AsciiAnchoredDfaIsMatchDirectProgram program,
        ReadOnlySpan<byte> input) =>
        program.Dfa.TryMatch(input, 0, out var matchedLength) &&
        matchedLength == input.Length;
}
