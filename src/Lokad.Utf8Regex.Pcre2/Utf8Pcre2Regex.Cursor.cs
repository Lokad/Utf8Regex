using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Pcre2;

public sealed partial class Utf8Pcre2Regex
{
    /// <summary>Creates a persistent range cursor over caller-valid, unchanged UTF-8 memory.</summary>
    /// <remarks>Starts are scalar-aligned bytes in the original subject, preserving assertion context. Each advance accepts its current-call token; the creation token, absolute deadline and cumulative numeric limits remain authoritative. Dispose abandoned cursors. Null working admission inherits program funding. Major scratch/result staging remains admitted; ordinary publication ends live result admission. An existing cursor retains program admission after expression disposal until EOF, failure or Dispose.</remarks>
    public Utf8Pcre2MatchCursor CreateMatchCursor(ReadOnlyMemory<byte> input, int startOffsetInBytes = 0,
        Pcre2MatchOptions matchOptions = Pcre2MatchOptions.None, CancellationToken cancellationToken = default,
        IUtf8MemoryAllowance? workingMemoryAllowance = null)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var use = _programMemory?.Enter();
        try
        {
            workingMemoryAllowance ??= _programMemory?.Allowance;
            ThrowIfGenericIterationMayBeNonMonotone();
            var request = _program.Request;
            var invocation = Utf8SliceInvocation.CreateExclusive(request.DefaultLimits.ToSliceLimits(),
                request.MatchTimeout, cancellationToken, workingMemoryAllowance);
            var cursor = new Utf8Pcre2MatchCursor(input, _program, startOffsetInBytes, matchOptions, invocation);
            if (use is not null) cursor.AttachProgramUse(use);
            return cursor;
        }
        catch { use?.Dispose(); throw; }
    }
}
