using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex;

public sealed partial class Utf8Regex
{
    /// <summary>Creates a persistent cursor over caller-valid, unchanged UTF-8 memory.</summary>
    /// <remarks>Null start selects zero for LTR or the subject end for RTL. Explicit starts are scalar-aligned byte coordinates and preserve prefix/lookbehind/anchor context. Sequential advances may cross awaits; the original deadline includes waits. Dispose abandoned cursors. Optional shared working admission covers major scratch and result staging; ordinary snapshot publication ends that admission responsibility.</remarks>
    public Utf8RegexMatchCursor CreateMatchCursor(ReadOnlyMemory<byte> input, int? startOffsetInBytes = null,
        CancellationToken cancellationToken = default, IUtf8MemoryAllowance? workingMemoryAllowance = null)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var programUse = _programMemory?.Enter();
        try
        {
            workingMemoryAllowance ??= _programMemory?.Allowance;
            var invocation = Utf8SliceInvocation.CreateExclusive(InvocationLimits, MatchTimeout,
                cancellationToken, workingMemoryAllowance);
            var cursor = new Utf8RegexMatchCursor(input, this, _ownedSliceProgram, _program.CaptureCatalog,
                startOffsetInBytes ?? (_ownedSliceProgram.RightToLeft ? input.Length : 0), invocation);
            if (programUse is not null) cursor.AttachProgramUse(programUse);
            return cursor;
        }
        catch { programUse?.Dispose(); throw; }
    }
}
