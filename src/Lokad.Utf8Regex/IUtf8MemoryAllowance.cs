namespace Lokad.Utf8Regex;

/// <summary>Provides caller-owned, synchronous admission for modeled managed storage.</summary>
/// <remarks>Implementations must be thread-safe; methods must not reenter regex APIs. Keep both methods fast; arbitrary caller work cannot be preempted. A successful reservation remains funded until the producer releases that exact amount. Covered scopes and conservative byte charges are documented by each accepting API.</remarks>
public interface IUtf8MemoryAllowance
{
    /// <summary>Attempts to reserve additional bytes before storage acquisition.</summary>
    /// <remarks>Return false without changing the allowance on refusal. A throwing implementation must also leave it unchanged. There is no implicit retry or alternate allowance.</remarks>
    bool TryReserve(ulong bytes);

    /// <summary>Releases a previously admitted amount after its covered users finish.</summary>
    /// <remarks>This cleanup operation must not throw or observe cancellation.</remarks>
    void Release(ulong bytes);
}
