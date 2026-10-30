using Lokad.Utf8Regex.Internal;

namespace Lokad.Utf8Regex;

/// <summary>Owns caller-funded Core compilation and reusable existence/count operations.</summary>
/// <remarks>Dispose closes new operations and refunds compilation after active calls finish. The underlying expression stays private. Temporary matching storage uses the program allowance unless a separate working allowance is supplied. Borrowed input and fixed invocation metadata remain outside modeled storage.</remarks>
public sealed class Utf8RegexProgramOwner : IDisposable
{
    private Utf8Regex? _regex;
    private readonly Utf8FundedStorage _storage;

    internal Utf8RegexProgramOwner(Utf8Regex regex, Utf8FundedStorage storage)
    { _regex = regex; _storage = storage; }

    /// <summary>Tests for a match with caller-funded temporary storage.</summary>
    /// <remarks>Null workingMemoryAllowance uses the program allowance; otherwise working storage uses the supplied separate owner. Numeric invocation limits still apply. A refused acquisition is terminal.</remarks>
    public bool IsMatch(ReadOnlySpan<byte> input, CancellationToken cancellationToken = default,
        IUtf8MemoryAllowance? workingMemoryAllowance = null)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var regex = Volatile.Read(ref _regex) ?? throw new ObjectDisposedException(nameof(Utf8RegexProgramOwner));
        using var use = _storage.Enter();
        return regex.IsMatch(input, cancellationToken, workingMemoryAllowance ?? _storage.Allowance);
    }

    /// <summary>Counts matches with caller-funded temporary storage.</summary>
    /// <remarks>Null workingMemoryAllowance uses the program allowance; otherwise working storage uses the supplied separate owner. Numeric invocation limits still apply. Covered temporary storage refunds synchronously on success, failure or interruption.</remarks>
    public int Count(ReadOnlySpan<byte> input, CancellationToken cancellationToken = default,
        IUtf8MemoryAllowance? workingMemoryAllowance = null)
    {
        cancellationToken.ThrowIfCancellationRequested();
        // Capture before Enter: a successfully entered use retains this local
        // root across Close; a concurrent closed gate rejects the call.
        var regex = Volatile.Read(ref _regex) ?? throw new ObjectDisposedException(nameof(Utf8RegexProgramOwner));
        using var use = _storage.Enter();
        return regex.Count(input, cancellationToken, workingMemoryAllowance ?? _storage.Allowance);
    }

    /// <summary>Closes new uses and releases compilation after its last active call finishes.</summary>
    public void Dispose()
    {
        // Drop the owner's payload before a synchronous final refund callback.
        // In-flight calls already hold their local root and storage-use lease.
        Interlocked.Exchange(ref _regex, null);
        _storage.Close();
    }
}
