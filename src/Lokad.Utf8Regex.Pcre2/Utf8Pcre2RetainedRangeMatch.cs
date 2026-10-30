namespace Lokad.Utf8Regex.Pcre2;

/// <summary>Owns explicit admission for one immutable PCRE2 byte-range result.</summary>
/// <remarks>No finalizer. Dispose after retained users finish or take over admission for copies kept afterward. Already copied matches remain valid. Borrowed input is excluded; shared names and mark text are conservatively charged again.</remarks>
public sealed class Utf8Pcre2RetainedRangeMatch : IDisposable
{
    private Utf8Pcre2RangeMatch _match;
    private IUtf8MemoryAllowance? _allowance;
    private ulong _bytes;
    internal Utf8Pcre2RetainedRangeMatch(Utf8Pcre2RangeMatch match, IUtf8MemoryAllowance allowance)
    { _match = match; _allowance = allowance; }
    internal void SetTransferredBytes(ulong bytes) => _bytes = bytes;
    /// <summary>Gets the snapshot while its reservation is held. Finish property reads before disposing.</summary>
    public Utf8Pcre2RangeMatch Match => Volatile.Read(ref _allowance) is not null
        ? _match : throw new ObjectDisposedException(nameof(Utf8Pcre2RetainedRangeMatch));
    /// <summary>The original modeled charge, including after disposal.</summary>
    public ulong ChargedBytes => _bytes;
    /// <summary>Refunds once, including disposal through concurrent aliases. Already copied results stay valid.</summary>
    public void Dispose()
    {
        var allowance = Interlocked.Exchange(ref _allowance, null);
        if (allowance is null) return;
        _match = default;
        if (_bytes != 0) allowance.Release(_bytes);
    }
}
