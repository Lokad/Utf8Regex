using System.Numerics;
using Lokad.Utf8Regex.Internal;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Pcre2;

// One lexical owner acquires fixed VM vectors on the first candidate, then
// lends them to later attempts. Nested assertions keep separate vectors while
// the parent stays live. Acquisition and release use the owner's actual budget.
internal ref struct Pcre2BacktrackingWorkspace
{
    private bool _retain;
    private ulong _reservedBytes;
    private int[]? _repeatState;
    private int[]? _captureState;
    private int[]? _captureRestoreGenerations;
    private int[]? _captureRestoreSlots;
    private Pcre2SubroutineCaptureSnapshot[]? _captureRestoreSnapshots;
    private int[]? _repeatRestoreGenerations;
    private int[]? _repeatRestoreIds;
    private Pcre2SubroutineRepeatSnapshot[]? _repeatRestoreSnapshots;

    internal bool EnsureInitialized(int repeatCount, int captureSlotCount, bool restoreSubroutines, scoped ref Pcre2ResourceBudget budget)
    {
        if (_reservedBytes != 0)
        {
            // Reuse still reaches the original synchronous token/deadline and
            // sticky-denial checkpoint, without charging another acquisition.
            budget.ReserveWorking(0);
            return false;
        }
        ArgumentOutOfRangeException.ThrowIfNegative(repeatCount);
        ArgumentOutOfRangeException.ThrowIfNegative(captureSlotCount);
        _retain = budget.CanRetainWorkspace;
        var repeatCapacity = restoreSubroutines ? Capacity(repeatCount) : 0;
        var captureCapacity = restoreSubroutines ? Capacity(captureSlotCount) : 0;
        var repeatStateCapacity = Capacity(checked(repeatCount * 2));
        var captureStateCapacity = Capacity(checked(captureSlotCount * 5));
        var bytes = checked(Utf8ConstructionBudget.ArrayBytes<int>(repeatStateCapacity) +
            Utf8ConstructionBudget.ArrayBytes<int>(captureStateCapacity) +
            2 * Utf8ConstructionBudget.ArrayBytes<int>(captureCapacity) +
            Utf8ConstructionBudget.ArrayBytes<Pcre2SubroutineCaptureSnapshot>(captureCapacity) +
            2 * Utf8ConstructionBudget.ArrayBytes<int>(repeatCapacity) +
            Utf8ConstructionBudget.ArrayBytes<Pcre2SubroutineRepeatSnapshot>(repeatCapacity));
        budget.ReserveWorking(bytes);
        _reservedBytes = bytes;
        // The lexical owner's finally also covers partial acquisition.
        _repeatState = Rent<int>(repeatStateCapacity, ref budget);
        _captureState = Rent<int>(captureStateCapacity, ref budget);
        _captureRestoreGenerations = Rent<int>(captureCapacity, ref budget);
        _captureRestoreSlots = Rent<int>(captureCapacity, ref budget);
        _captureRestoreSnapshots = Rent<Pcre2SubroutineCaptureSnapshot>(captureCapacity, ref budget);
        _repeatRestoreGenerations = Rent<int>(repeatCapacity, ref budget);
        _repeatRestoreIds = Rent<int>(repeatCapacity, ref budget);
        _repeatRestoreSnapshots = Rent<Pcre2SubroutineRepeatSnapshot>(repeatCapacity, ref budget);
        return true;
    }

    internal readonly int[]? RepeatState => _repeatState;
    internal readonly int[]? CaptureState => _captureState;
    internal readonly int[]? CaptureRestoreGenerations => _captureRestoreGenerations;
    internal readonly int[]? CaptureRestoreSlots => _captureRestoreSlots;
    internal readonly Pcre2SubroutineCaptureSnapshot[]? CaptureRestoreSnapshots => _captureRestoreSnapshots;
    internal readonly int[]? RepeatRestoreGenerations => _repeatRestoreGenerations;
    internal readonly int[]? RepeatRestoreIds => _repeatRestoreIds;
    internal readonly Pcre2SubroutineRepeatSnapshot[]? RepeatRestoreSnapshots => _repeatRestoreSnapshots;

    internal void Dispose(scoped ref Pcre2ResourceBudget budget)
    {
        Return(ref _repeatState);
        Return(ref _captureState);
        Return(ref _captureRestoreGenerations);
        Return(ref _captureRestoreSlots);
        Return(ref _captureRestoreSnapshots);
        Return(ref _repeatRestoreGenerations);
        Return(ref _repeatRestoreIds);
        Return(ref _repeatRestoreSnapshots);
        budget.ReleaseWorking(_reservedBytes);
        _reservedBytes = 0;
    }

    private T[]? Rent<T>(int capacity, scoped ref Pcre2ResourceBudget budget) where T : struct
    {
        if (capacity == 0) return null;
        budget.ChargeWorkspaceStep();
        return Utf8ExactStatePool<T>.Rent(capacity, _retain);
    }

    private void Return<T>(ref T[]? items) where T : struct
    {
        if (items is not null) Utf8ExactStatePool<T>.Return(items, _retain);
        items = null;
    }

    private static int Capacity(int count) => count == 0 ? 0 : count <= 256
        ? (int)BitOperations.RoundUpToPowerOf2((uint)Math.Max(16, count)) : count;
}
