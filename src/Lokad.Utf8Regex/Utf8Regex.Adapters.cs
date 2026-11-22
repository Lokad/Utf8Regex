using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Input;
using Lokad.Utf8Regex.Internal.Planning;

namespace Lokad.Utf8Regex;

// byte-offset adapters and direct probes, split from the matching facade along the operation-adapter ownership boundary.
/// <summary>Byte-offset operation adapters and direct probes for <see cref="Utf8Regex"/>.</summary>
public sealed partial class Utf8Regex
{
    internal bool TryMatchDirectWithoutValidation(ReadOnlySpan<byte> input, out Utf8CaptureRange match)
    {
        if (GetOwnedSliceProgram(input) is null)
            return TryMatchSelectedDirect(input, out match);
        match = default;
        return false;
    }

    // Selection already accounts for boundary, folding and timeout requirements.
    private bool TryMatchSelectedDirect(ReadOnlySpan<byte> input, out Utf8CaptureRange match)
    {
        if (TryMatchAnchoredLiteral(input, out match))
        {
            return true;
        }

        if (_program.CompiledEngineRuntime is { } runtime)
            return runtime.TryMatchDirect(input, out match);

        return false;
    }

    private Utf8PreparedValueMatchEnumerator EnumeratePreparedMatchesAtByteOffset(
        Utf8ValidatedInput input, Utf8BytePosition start)
    {
        return new Utf8PreparedValueMatchEnumerator(input.Bytes, _preparedRegex.SearchPlan.PreparedSearcher, start);
    }

    internal readonly struct Utf8ByteOffsetExecution
    {
        private readonly Utf8Regex _owner;

        public Utf8ByteOffsetExecution(Utf8Regex owner)
        {
            _owner = owner;
        }

        // PCRE2-INTEGRATION-POINT: the adapter admits an exact literal family
        // and supplies a scalar-aligned suffix of an unmetered invocation.
        public int CountLiteralFamily(ReadOnlySpan<byte> remaining) =>
            _owner._compiledEngineRuntime.Count(remaining, Utf8ExecutionDeadline.Infinite);

        // PCRE2-INTEGRATION-POINT: flavor-neutral prepared byte-range iteration.
        public Utf8PreparedValueMatchEnumerator EnumeratePreparedMatches(
            Utf8ValidatedInput input, Utf8BytePosition start)
            => _owner.EnumeratePreparedMatchesAtByteOffset(input, start);

        public Utf8SearchPortfolioKind SearchPortfolioKind =>
            _owner._preparedRegex.SearchPlan.PortfolioKind;
    }
}
