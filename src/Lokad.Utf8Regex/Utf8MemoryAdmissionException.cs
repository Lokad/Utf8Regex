namespace Lokad.Utf8Regex;

/// <summary>Reports refusal by a caller-owned shared memory allowance before storage acquisition.</summary>
public sealed class Utf8MemoryAdmissionException : Exception
{
    /// <summary>Creates an admission failure for an additional modeled byte reservation.</summary>
    public Utf8MemoryAdmissionException(ulong requestedBytes)
        : base($"The shared memory allowance refused a reservation of {requestedBytes} bytes.")
    {
        RequestedBytes = requestedBytes;
    }

    /// <summary>Gets the refused additional byte amount, not a total across other concurrent users.</summary>
    public ulong RequestedBytes { get; }
}
