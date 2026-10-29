using Lokad.Utf8Regex.Internal;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.PythonRe;

public sealed partial class Utf8PythonRegex : IDisposable
{
    private Utf8FundedStorage? _programMemory;

    private Utf8FundedStorage.Use? EnterProgramUse() => _programMemory?.Enter();

    private Utf8SliceInvocation CreateInvocation(CancellationToken token)
        => _programMemory is { } memory
            ? new Utf8SliceInvocation(InvocationLimits, MatchTimeout, token, memory.Allowance)
            : new Utf8SliceInvocation(InvocationLimits, MatchTimeout, token);

    /// <summary>Closes caller-funded program/cache storage after its last active users finish.</summary>
    /// <remarks>New matching uses of a funded expression fail after disposal. In-flight uses retain their funding and complete under their original controls; disposal neither waits nor starts background work. Existing snapshots remain valid. Disposal is a no-op for expressions compiled without a caller-owned program allowance.</remarks>
    public void Dispose()
    {
        if (_programMemory is not { } memory) return;
        // Stop retention before clearing. Active substitutions can finish
        // uncached; entry uses keep evicted plans funded until done.
        _replacementPlanCache.Capacity = 0;
        _replacementPlanCache.Clear();
        memory.Close();
    }
}
