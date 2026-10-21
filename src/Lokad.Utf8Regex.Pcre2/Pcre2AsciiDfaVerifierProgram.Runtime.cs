using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal;
using Lokad.Utf8Regex.Internal.Execution;
using System.Threading;

namespace Lokad.Utf8Regex.Pcre2;

internal readonly partial struct Pcre2AsciiDfaVerifierProgram
{
    public bool TryMatch(ReadOnlySpan<byte> input, int startIndex, out int matchedLength)
        => TryMatch(input, startIndex, Utf8ExecutionDeadline.Infinite, out matchedLength);

    internal bool TryMatch(ReadOnlySpan<byte> input, int startIndex, Utf8ExecutionDeadline deadline, out int matchedLength)
    {
        var invocation = new Utf8SliceInvocation(Utf8ExecutionLimits.Unlimited, deadline, CancellationToken.None);
        var found = TryMatch(input, startIndex, ref invocation, out matchedLength, out var breach);
        if (breach != Utf8ExecutionLimitKind.None)
            throw new InvalidOperationException("An unlimited DFA invocation exceeded its representable state extent.");
        return found;
    }

    internal bool TryMatch(ReadOnlySpan<byte> input, int startIndex, ref Utf8SliceInvocation invocation,
        out int matchedLength, out Utf8ExecutionLimitKind breach)
    {
        matchedLength = 0;
        breach = invocation.TryChargeInputStep();
        if (breach != Utf8ExecutionLimitKind.None || !HasValue || (uint)startIndex > (uint)input.Length) return false;
        breach = invocation.Budget.TryNoteDepth(1);
        if (breach != Utf8ExecutionLimitKind.None) return false;
        if (RequiresBeginning && startIndex != 0) return false;

        var state = 0;
        var index = startIndex;
        var lastAcceptLength = _acceptingStates[state] && (!RequiresEnd ||
            IsAtEnd(input, index)) ? 0 : -1;
        while ((uint)index < (uint)input.Length)
        {
            if (((index - startIndex) & 63) == 0)
            {
                breach = invocation.TryChargeInputStep();
                if (breach != Utf8ExecutionLimitKind.None) return false;
            }
            var nextState = FindTransition(state, input[index], ref invocation, out breach);
            if (breach != Utf8ExecutionLimitKind.None) return false;
            if (nextState < 0) break;
            state = nextState;
            index++;
            // Preserve the last valid accepting position when a greedy suffix
            // consumes a newline and then fails beyond an earlier line end.
            if (_acceptingStates[state] && (!RequiresEnd ||
                IsAtEnd(input, index)))
                lastAcceptLength = index - startIndex;
        }
        breach = invocation.TryChargeInputStep();
        if (breach != Utf8ExecutionLimitKind.None || lastAcceptLength < 0) return false;
        matchedLength = lastAcceptLength;
        return true;
    }

    // The sole producer admits default, non-multiline anchors only.
    private static bool IsAtEnd(ReadOnlySpan<byte> input, int index) =>
        index == input.Length || index == input.Length - 1 && input[index] == (byte)'\n';

    private int FindTransition(int state, byte value, ref Utf8SliceInvocation invocation, out Utf8ExecutionLimitKind breach)
    {
        breach = Utf8ExecutionLimitKind.None;
        var offset = _transitionOffsets[state];
        var count = _transitionCounts[state];
        for (var i = 0; i < count; i++)
        {
            if (i != 0 && (i & 63) == 0)
            {
                breach = invocation.TryChargeInputStep();
                if (breach != Utf8ExecutionLimitKind.None) return -1;
            }
            var transitionIndex = offset + i;
            if (_transitionSets[transitionIndex].Contains(value)) return _transitionTargets[transitionIndex];
        }
        return -1;
    }
}
