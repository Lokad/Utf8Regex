using Lokad.Utf8Regex.Internal;

namespace Lokad.Utf8Regex.Pcre2;

/// <summary>Owns caller-funded PCRE2 compilation and reusable Boolean/count operations.</summary>
/// <remarks>Dispose closes new calls and refunds compilation after active calls finish. Temporary matching storage uses the program allowance unless a separate working account is supplied. Borrowed input and fixed invocation metadata are outside modeled buffer storage.</remarks>
public sealed class Utf8Pcre2RegexProgramOwner : IDisposable
{
    private Utf8Pcre2Regex? _regex;
    private readonly Utf8FundedStorage _storage;

    internal Utf8Pcre2RegexProgramOwner(Utf8Pcre2Regex regex, Utf8FundedStorage storage)
    { _regex = regex; _storage = storage; }

    /// <summary>Tests for a match with caller-funded temporary storage and the program's numeric limits.</summary>
    /// <remarks>The start is a byte coordinate in the original subject. Null working allowance uses program funding. Refused acquisition is terminal.</remarks>
    public bool IsMatch(ReadOnlySpan<byte> input, int startOffsetInBytes = 0,
        Pcre2MatchOptions matchOptions = Pcre2MatchOptions.None, CancellationToken cancellationToken = default,
        IUtf8MemoryAllowance? workingMemoryAllowance = null)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var regex = Volatile.Read(ref _regex) ?? throw new ObjectDisposedException(nameof(Utf8Pcre2RegexProgramOwner));
        using var use = _storage.Enter();
        return regex.IsMatch(input, startOffsetInBytes, matchOptions, cancellationToken,
            workingMemoryAllowance ?? _storage.Allowance);
    }

    /// <summary>Counts matches using caller-funded temporary storage and existing PCRE2 global progression.</summary>
    /// <remarks>Temporary loans refund synchronously. Work/result traffic and depth/heap controls remain independently applicable.</remarks>
    public int Count(ReadOnlySpan<byte> input, int startOffsetInBytes = 0,
        Pcre2MatchOptions matchOptions = Pcre2MatchOptions.None, CancellationToken cancellationToken = default,
        IUtf8MemoryAllowance? workingMemoryAllowance = null)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var regex = Volatile.Read(ref _regex) ?? throw new ObjectDisposedException(nameof(Utf8Pcre2RegexProgramOwner));
        using var use = _storage.Enter();
        return regex.Count(input, startOffsetInBytes, matchOptions, cancellationToken,
            workingMemoryAllowance ?? _storage.Allowance);
    }

    /// <summary>Rejects new calls and refunds compilation after the final active use.</summary>
    public void Dispose()
    {
        Interlocked.Exchange(ref _regex, null);
        _storage.Close();
    }
}
