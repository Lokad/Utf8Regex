using System.Numerics;
using Lokad.Utf8Regex.Internal;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Pcre2;

// One lexical workspace admits all fixed VM vectors before any acquisition.
// Nested assertions borrow the same budget while their parent vectors stay live.
internal ref struct Pcre2BacktrackingWorkspace
{
    private readonly ref Pcre2ResourceBudget _budget;
    private readonly bool _retain;
    private ulong _reservedBytes;
    private int[]? _repeatState;
    private int[]? _captureState;
    private int[]? _captureRestoreGenerations;
    private int[]? _captureRestoreSlots;
    private Pcre2SubroutineCaptureSnapshot[]? _captureRestoreSnapshots;
    private int[]? _repeatRestoreGenerations;
    private int[]? _repeatRestoreIds;
    private Pcre2SubroutineRepeatSnapshot[]? _repeatRestoreSnapshots;

    internal Pcre2BacktrackingWorkspace(int repeatCount, int captureSlotCount, bool restoreSubroutines, ref Pcre2ResourceBudget budget)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(repeatCount);
        ArgumentOutOfRangeException.ThrowIfNegative(captureSlotCount);
        _budget = ref budget;
        _retain = budget.CanRetainWorkspace;
        _reservedBytes = 0;
        _repeatState = null;
        _captureState = null;
        _captureRestoreGenerations = null;
        _captureRestoreSlots = null;
        _captureRestoreSnapshots = null;
        _repeatRestoreGenerations = null;
        _repeatRestoreIds = null;
        _repeatRestoreSnapshots = null;
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
        try
        {
            _repeatState = Rent<int>(repeatStateCapacity);
            _captureState = Rent<int>(captureStateCapacity);
            _captureRestoreGenerations = Rent<int>(captureCapacity);
            _captureRestoreSlots = Rent<int>(captureCapacity);
            _captureRestoreSnapshots = Rent<Pcre2SubroutineCaptureSnapshot>(captureCapacity);
            _repeatRestoreGenerations = Rent<int>(repeatCapacity);
            _repeatRestoreIds = Rent<int>(repeatCapacity);
            _repeatRestoreSnapshots = Rent<Pcre2SubroutineRepeatSnapshot>(repeatCapacity);
        }
        catch { Dispose(); throw; }
    }

    internal readonly int[]? RepeatState => _repeatState;
    internal readonly int[]? CaptureState => _captureState;
    internal readonly int[]? CaptureRestoreGenerations => _captureRestoreGenerations;
    internal readonly int[]? CaptureRestoreSlots => _captureRestoreSlots;
    internal readonly Pcre2SubroutineCaptureSnapshot[]? CaptureRestoreSnapshots => _captureRestoreSnapshots;
    internal readonly int[]? RepeatRestoreGenerations => _repeatRestoreGenerations;
    internal readonly int[]? RepeatRestoreIds => _repeatRestoreIds;
    internal readonly Pcre2SubroutineRepeatSnapshot[]? RepeatRestoreSnapshots => _repeatRestoreSnapshots;

    internal void Dispose()
    {
        Return(ref _repeatState);
        Return(ref _captureState);
        Return(ref _captureRestoreGenerations);
        Return(ref _captureRestoreSlots);
        Return(ref _captureRestoreSnapshots);
        Return(ref _repeatRestoreGenerations);
        Return(ref _repeatRestoreIds);
        Return(ref _repeatRestoreSnapshots);
        _budget.ReleaseWorking(_reservedBytes);
        _reservedBytes = 0;
    }

    private T[]? Rent<T>(int capacity) where T : struct
    {
        if (capacity == 0) return null;
        _budget.ChargeWorkspaceStep();
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
