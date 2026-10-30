namespace Lokad.Utf8Regex.Pcre2;

public sealed partial class Utf8Pcre2Regex
{
    /// <summary>Creates a persistent range cursor, validating unchanged subject memory once.</summary>
    /// <remarks>Starts are scalar-aligned bytes in the original subject, preserving assertion context. Each advance accepts its current-call token; the creation token, absolute deadline and cumulative numeric limits remain authoritative. Dispose abandoned cursors. Optional working admission covers major scratch/result staging; ordinary publication ends live result admission.</remarks>
    public Utf8Pcre2MatchCursor CreateMatchCursor(ReadOnlyMemory<byte> input, int startOffsetInBytes = 0,
        Pcre2MatchOptions matchOptions = Pcre2MatchOptions.None, CancellationToken cancellationToken = default,
        IUtf8MemoryAllowance? workingMemoryAllowance = null)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ThrowIfGenericIterationMayBeNonMonotone();
        var invocation = Pcre2SliceDriver.CreateInvocation(_program.Request, cancellationToken, workingMemoryAllowance);
        return new(input, _program, startOffsetInBytes, matchOptions, invocation);
    }
}
