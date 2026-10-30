using Lokad.Utf8Regex.Internal;
using System.Buffers;

namespace Lokad.Utf8Regex.Pcre2;

/// <summary>Owns caller-funded PCRE2 compilation, Boolean/count calls and persistent range cursors.</summary>
/// <remarks>Dispose closes new calls and refunds compilation after active calls finish. Temporary matching storage uses the program allowance unless a separate working account is supplied. Borrowed input and fixed invocation metadata are outside modeled buffer storage.</remarks>
public sealed class Utf8Pcre2RegexProgramOwner : IDisposable
{
    private Utf8Pcre2Regex? _regex;
    private readonly Utf8FundedStorage _storage;

    internal Utf8Pcre2RegexProgramOwner(Utf8Pcre2Regex regex, Utf8FundedStorage storage)
    { _regex = regex; _storage = storage; }

    /// <summary>Gets the number of defined capture slots, excluding whole-match group zero.</summary>
    /// <remarks>Rejects access after close. Branch-reset numbering and duplicate names retain the validated profile semantics.</remarks>
    public int CaptureGroupCount
    {
        get
        {
            var regex = Volatile.Read(ref _regex) ?? throw new ObjectDisposedException(nameof(Utf8Pcre2RegexProgramOwner));
            using var use = _storage.Enter();
            return regex.CaptureGroupCount;
        }
    }

    /// <inheritdoc cref="Utf8Pcre2Regex.TryCopyPatternUtf8" />
    /// <remarks>Holds program admission during the copy and rejects calls after close. The destination belongs to the caller and remains valid after owner disposal; no borrowed program-storage alias is exposed.</remarks>
    public OperationStatus TryCopyPatternUtf8(Span<byte> destination, out int bytesRequired,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var regex = Volatile.Read(ref _regex) ?? throw new ObjectDisposedException(nameof(Utf8Pcre2RegexProgramOwner));
        using var use = _storage.Enter();
        return regex.TryCopyPatternUtf8(destination, out bytesRequired, cancellationToken);
    }

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

    /// <summary>Creates a persistent range cursor retaining program admission until EOF, failure or disposal.</summary>
    /// <remarks>Null working allowance uses program funding. Closing this owner rejects new cursors; existing cursors remain valid under their original controls.</remarks>
    public Utf8Pcre2MatchCursor CreateMatchCursor(ReadOnlyMemory<byte> input, int startOffsetInBytes = 0,
        Pcre2MatchOptions matchOptions = Pcre2MatchOptions.None, CancellationToken cancellationToken = default,
        IUtf8MemoryAllowance? workingMemoryAllowance = null)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var regex = Volatile.Read(ref _regex) ?? throw new ObjectDisposedException(nameof(Utf8Pcre2RegexProgramOwner));
        var use = _storage.Enter();
        try
        {
            var cursor = regex.CreateMatchCursor(input, startOffsetInBytes, matchOptions, cancellationToken,
                workingMemoryAllowance ?? _storage.Allowance);
            cursor.AttachProgramUse(use); return cursor;
        }
        catch { use.Dispose(); throw; }
    }

    /// <summary>Rejects new calls and refunds compilation after the final active use.</summary>
    public void Dispose()
    {
        Interlocked.Exchange(ref _regex, null);
        _storage.Close();
    }
}
