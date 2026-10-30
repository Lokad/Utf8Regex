namespace Lokad.Utf8Regex;

/// <summary>Owns explicit admission for one immutable Core range snapshot.</summary>
/// <remarks>No finalizer. Dispose after retained users finish or take over admission for copies kept afterward. Already copied matches remain valid. Borrowed input is excluded; shared name/number storage is conservatively charged again.</remarks>
public sealed class Utf8RetainedRangeMatch : IDisposable
{
    private Utf8RangeMatch _match;
    private IUtf8MemoryAllowance? _allowance;
    private ulong _bytes;

    internal Utf8RetainedRangeMatch(Utf8RangeMatch match, IUtf8MemoryAllowance allowance)
    { _match = match; _allowance = allowance; }

    internal void SetTransferredBytes(ulong bytes) => _bytes = bytes;

    /// <summary>Gets the snapshot while its reservation is held. Finish property reads before disposal.</summary>
    public Utf8RangeMatch Match => Volatile.Read(ref _allowance) is not null
        ? _match : throw new ObjectDisposedException(nameof(Utf8RetainedRangeMatch));
    /// <summary>The original modeled charge, including after disposal.</summary>
    public ulong ChargedBytes => _bytes;

    /// <summary>Refunds once, including concurrent disposal through aliases. Already copied matches stay valid.</summary>
    public void Dispose()
    {
        var allowance = Interlocked.Exchange(ref _allowance, null);
        if (allowance is null) return;
        _match = default;
        if (_bytes != 0) allowance.Release(_bytes);
    }
}
