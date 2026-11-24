using Lokad.Utf8Regex.Internal.Diagnostics;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Planning;

namespace Lokad.Utf8Regex;

public sealed partial class Utf8Regex
{
    private bool ShouldUseKelvinSignFallback(ReadOnlySpan<byte> input)
        => _requiresKelvinSignFallback && ContainsKelvinSign(input);

    private const int MaximumFoldedSearchLiteralLength = 64;

    // One-shot calls without interruption or resource controls can look for
    // both literal starts and Kelvin aliases in the same byte scan.
    private bool TryResolveFoldedLiteralMatch(ReadOnlySpan<byte> input, CancellationToken token, out Utf8CaptureRange match)
    {
        match = default;
        if (token.CanBeCanceled || MatchTimeout != Regex.InfiniteMatchTimeout ||
            _hasCaptures || UsesRightToLeft() ||
            (Options & RegexOptions.CultureInvariant) == 0 ||
            Utf8SearchDiagnosticsSession.Current?.SliceCheckpointHook is not null)
            return false;

        ref readonly var plan = ref _preparedRegex.SearchPlan;
        if (plan is not { Kind: Utf8SearchKind.AsciiFoldedByteLiteral, HasBoundaryRequirements: false,
                HasTrailingLiteralRequirement: false, LiteralUtf8: { Length: > 0 and <= MaximumFoldedSearchLiteralLength } literal })
            return false;

        var kelvin = "K"u8;
        var kelvinLead = kelvin[0];
        var first = literal[0];
        var other = (uint)((first | 0x20) - 'a') <= 'z' - 'a' ? (byte)(first ^ 0x20) : first;
        var lastStart = input.Length - literal.Length;
        var found = -1;
        for (var floor = 0; floor < input.Length;)
        {
            var offset = input[floor..].IndexOfAny(first, other, kelvinLead);
            if (offset < 0) break;
            var start = floor + offset;
            if (input[start] == kelvinLead)
            {
                if (!input[start..].StartsWith(kelvin) || GetOwnedSliceProgram(ReadOnlySpan<byte>.Empty) is not null)
                    return false;
                // A failed ASCII candidate can still match across this first
                // Kelvin sign. All scalars before it in such a match are ASCII.
                var resume = Math.Max(0, start - literal.Length + 1);
                while ((input[resume] & 0xc0) == 0x80) resume++;
                match = OwnedSliceMatchFromByte(input, _ownedSliceProgram, resume, token);
                return true;
            }
            if (start <= lastStart && System.Text.Ascii.EqualsIgnoreCase(input.Slice(start, literal.Length), literal))
            {
                found = start;
                break;
            }
            floor = start + 1;
        }

        // An earlier match consumes the same number of scalars. If it
        // overlaps the confirmed ASCII hit, its Kelvin alias must precede it.
        if (GetOwnedSliceProgram(ReadOnlySpan<byte>.Empty) is not null) return false;
        if (found >= 0) match = new Utf8CaptureRange(found, found + literal.Length);
        return true;
    }

    private static bool ContainsKelvinSign(ReadOnlySpan<byte> input)
        => input.IndexOf("K"u8) >= 0;

    private static bool RequiresKelvinSignFallback(Utf8PreparedRegex preparedRegex)
    {

        if (preparedRegex.ExecutionKind == NativeExecutionKind.AsciiLiteralIgnoreCase)
        {
            return ContainsAsciiK(preparedRegex.LiteralUtf8);
        }

        if (preparedRegex.ExecutionKind != NativeExecutionKind.AsciiLiteralIgnoreCaseLiterals)
        {
            return false;
        }

        foreach (var literal in preparedRegex.SearchPlan.AlternateLiteralsUtf8 ?? [])
        {
            if (ContainsAsciiK(literal))
            {
                return true;
            }
        }

        return false;
    }

    private static bool ContainsAsciiK(byte[]? literal)
        => literal is not null && literal.AsSpan().IndexOfAny((byte)'K', (byte)'k') >= 0;
}
