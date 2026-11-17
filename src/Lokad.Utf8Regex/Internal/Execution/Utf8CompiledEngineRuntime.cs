using Lokad.Utf8Regex.Internal.Diagnostics;
using Lokad.Utf8Regex.Internal.Input;
using Lokad.Utf8Regex.Internal.Planning;
using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex.Internal.Execution;

internal readonly record struct Utf8CompiledRuntimeCapabilities(
    bool SupportsAsciiWellFormedOnlyMatch, bool SupportsWellFormedOnlyMatch, bool WellFormedOnlyMatchMissIsDefinitive, bool SupportsWellFormedOnlyCount, bool SkipRequiredPrefilterForMatch, bool SkipRequiredPrefilterForCount);

internal interface IUtf8AsciiWellFormedMatchRuntime
{
    /// <summary>
    /// Attempts a match after the caller has proved that the subject is ASCII
    /// and well formed. A false result is definitive only when the runtime's
    /// capability record says so.
    /// </summary>
    bool TryMatchAsciiWellFormed(ReadOnlySpan<byte> input, out Utf8CaptureRange match);
}

internal interface IUtf8WellFormedMatchRuntime
{
    /// <summary>
    /// Attempts a match over caller-valid UTF-8 text.
    /// A false result is definitive only when the capability record says so.
    /// </summary>
    bool TryMatchWellFormed(ReadOnlySpan<byte> input, out Utf8CaptureRange match);
}

internal interface IUtf8UnvalidatedMatchRuntime
{
    /// <summary>
    /// Attempts a byte-safe match without validation. The implementation must
    /// charge the supplied deadline and return false when it cannot safely
    /// decide the operation over caller-valid UTF-8.
    /// </summary>
    bool ExecuteMatchWithoutValidation(
        ReadOnlySpan<byte> input, Utf8ExecutionDeadline budget, out Utf8CaptureRange match);
}

internal abstract class Utf8CompiledEngineRuntime
{
    /// <summary>
    /// Describes optional whole-operation shortcuts implemented by this
    /// runtime. A false member means that the canonical matching operation
    /// path must be used; all paths require caller-valid text.
    /// </summary>
    public virtual Utf8CompiledRuntimeCapabilities Capabilities => default;

    public bool SupportsAsciiWellFormedOnlyMatch => Capabilities.SupportsAsciiWellFormedOnlyMatch;
    public bool SupportsWellFormedOnlyMatch => Capabilities.SupportsWellFormedOnlyMatch;
    public bool WellFormedOnlyMatchMissIsDefinitive => Capabilities.WellFormedOnlyMatchMissIsDefinitive;
    public bool SupportsWellFormedOnlyCount => Capabilities.SupportsWellFormedOnlyCount;
    public bool SkipRequiredPrefilterForMatch => Capabilities.SkipRequiredPrefilterForMatch;
    public bool SkipRequiredPrefilterForCount => Capabilities.SkipRequiredPrefilterForCount;

    public bool TryMatchAsciiWellFormedOnly(ReadOnlySpan<byte> input, out Utf8CaptureRange match)
    {
        if (this is IUtf8AsciiWellFormedMatchRuntime runtime)
        {
            return runtime.TryMatchAsciiWellFormed(input, out match);
        }

        match = default(Utf8CaptureRange);
        return false;
    }

    public bool TryMatchWellFormedOnly(ReadOnlySpan<byte> input, out Utf8CaptureRange match)
    {
        if (this is IUtf8WellFormedMatchRuntime runtime)
        {
            return runtime.TryMatchWellFormed(input, out match);
        }

        match = default(Utf8CaptureRange);
        return false;
    }

    /// <summary>
    /// Evaluates boolean match semantics for a complete subject. The caller
    /// owns validation and supplies either its result or the documented
    /// well-formed shortcut sentinel. Implementations must charge the deadline
    /// and a false result is definitive.
    /// </summary>
    public abstract bool IsMatch(ReadOnlySpan<byte> input, Utf8ValidationResult validation, Utf8ExecutionDeadline budget);

    /// <summary>
    /// Counts nonoverlapping matches, progressing through complete scalars.
    /// Validation is caller-owned, every scan charges the deadline, and the
    /// returned count is complete rather than a lower bound.
    /// </summary>
    public abstract int Count(ReadOnlySpan<byte> input, Utf8ValidationResult validation, Utf8ExecutionDeadline budget);

    /// <summary>
    /// Returns the first capture-zero match for the supplied validated subject,
    /// or a default range. Endpoints refer to the original bytes and all work
    /// charges the deadline.
    /// </summary>
    public abstract Utf8CaptureRange Match(ReadOnlySpan<byte> input, Utf8ValidationResult validation, Utf8ExecutionDeadline budget);

    public bool TryMatchWithoutValidation(ReadOnlySpan<byte> input, Utf8ExecutionDeadline budget, out Utf8CaptureRange match)
    {
        if (this is IUtf8UnvalidatedMatchRuntime runtime)
        {
            return runtime.ExecuteMatchWithoutValidation(input, budget, out match);
        }

        match = default(Utf8CaptureRange);
        return false;
    }

    public static Utf8CompiledEngineRuntime Create(Utf8CompiledEngine compiledEngine, Utf8PreparedRegex regexPlan, RegexOptions options)
        => Utf8CompiledRuntimeFactory.Create(compiledEngine, regexPlan, options);

}
