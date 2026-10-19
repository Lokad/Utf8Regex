using Lokad.Utf8Regex.Internal;
using Lokad.Utf8Regex.Internal.Execution;
using System.Numerics;
using Lokad.Utf8Regex.Internal.Planning;
using Lokad.Utf8Regex.Internal.Search;

namespace Lokad.Utf8Regex.Benchmarks;

/// <summary>
/// Invocation-local stable merge of structural candidate sources. Each source
/// advances monotonically; two-source portfolios stay entirely inline. Larger
/// portfolios admit their complete exact vectors in the borrowed invocation.
/// This lexical workspace has one owner and must not be copied.
/// </summary>
internal ref struct Utf8CandidatePortfolioCursor
{
    private readonly Utf8StructuralSearchPlan[] _plans;
    private readonly ReadOnlySpan<byte> _input;
    private readonly ref Utf8SliceInvocation _invocation;
    private readonly bool _controlled;
    private Utf8StructuralSearchState _state0;
    private Utf8StructuralSearchState _state1;
    private Utf8StructuralCandidate _candidate0;
    private Utf8StructuralCandidate _candidate1;
    private byte _inlineAvailable;
    private Utf8StructuralSearchState[]? _states;
    private Utf8StructuralCandidate[]? _candidates;
    private bool[]? _available;
    private int _minimumStart;
    private ulong _reservedBytes;

    public Utf8CandidatePortfolioCursor(
        Utf8StructuralSearchPlan[] plans,
        ReadOnlySpan<byte> input,
        int startIndex,
        ref Utf8SliceInvocation invocation)
    {
        _plans = plans;
        _input = input;
        _invocation = ref invocation;
        _controlled = !invocation.IsUnmetered;
        _state0 = CreateCandidateState(startIndex);
        _state1 = CreateCandidateState(startIndex);
        _candidate0 = default;
        _candidate1 = default;
        _inlineAvailable = 0;
        _states = null;
        _candidates = null;
        _available = null;
        _minimumStart = startIndex;
        _reservedBytes = 0;
        SourceAdvanceCount = 0;

        try
        {
            if (plans.Length > 2)
            {
                // The shared exact pool accepts bounded power-of-two leaves.
                // Large portfolios acquire exactly their source count.
                var capacity = plans.Length <= 256
                    ? (int)BitOperations.RoundUpToPowerOf2((uint)Math.Max(16, plans.Length))
                    : plans.Length;
                var bytes = checked(Utf8ConstructionBudget.ArrayBytes<Utf8StructuralSearchState>(capacity) +
                    Utf8ConstructionBudget.ArrayBytes<Utf8StructuralCandidate>(capacity) +
                    Utf8ConstructionBudget.ArrayBytes<bool>(capacity));
                var admission = invocation.TryReserveWorking(bytes);
                if (admission != Utf8ExecutionLimitKind.None)
                    throw new InvalidOperationException($"Scalar candidate portfolio exceeded {admission}.");
                _reservedBytes = bytes;
                var retain = invocation.Ledger.IsUnlimited;
                Check();
                _states = Utf8ExactStatePool<Utf8StructuralSearchState>.Rent(capacity, retain);
                Check();
                _candidates = Utf8ExactStatePool<Utf8StructuralCandidate>.Rent(capacity, retain);
                Check();
                _available = Utf8ExactStatePool<bool>.Rent(capacity, retain);
                for (var i = 0; i < plans.Length; i++)
                {
                    Check();
                    _available[i] = false;
                    _states[i] = CreateCandidateState(startIndex);
                }
            }

            for (var i = 0; i < plans.Length; i++) AdvanceSource(i);
        }
        catch { Dispose(); throw; }
    }

    internal int SourceAdvanceCount { get; private set; }

    internal bool UsesPooledStorage => _states is not null;

    public bool TryGetNext(out Utf8StructuralCandidate candidate)
    {
        var bestIndex = FindEarliestCandidateIndex();
        if (bestIndex < 0)
        {
            candidate = default;
            return false;
        }

        candidate = GetCandidate(bestIndex);
        AdvanceSource(bestIndex);
        return true;
    }

    public bool TryGetNextScalarBoundary(out Utf8StructuralCandidate candidate)
    {
        while (TryGetNext(out candidate))
        {
            if (IsScalarBoundaryByteOffset(_input, candidate.StartIndex))
            {
                return true;
            }
        }

        candidate = default;
        return false;
    }

    public void AdvancePast(int minimumStart)
    {
        if (minimumStart <= _minimumStart)
        {
            return;
        }

        _minimumStart = minimumStart;
        for (var i = 0; i < _plans.Length; i++)
        {
            Check();
            while (IsAvailable(i) && GetCandidate(i).StartIndex < minimumStart)
            {
                AdvanceSource(i);
            }
        }
    }

    public void Dispose()
    {
        if (_states is { } states)
        {
            Utf8ExactStatePool<Utf8StructuralSearchState>.Return(states, _invocation.Ledger.IsUnlimited);
            _states = null;
        }

        if (_candidates is { } candidates)
        {
            Utf8ExactStatePool<Utf8StructuralCandidate>.Return(candidates, _invocation.Ledger.IsUnlimited);
            _candidates = null;
        }

        if (_available is { } available)
        {
            Utf8ExactStatePool<bool>.Return(available, _invocation.Ledger.IsUnlimited);
            _available = null;
        }
        _invocation.Ledger.Release(_reservedBytes);
        _reservedBytes = 0;
    }

    private void AdvanceSource(int index)
    {
        Check();
        SourceAdvanceCount++;
        if (_states is { } states && _candidates is { } candidates && _available is { } available)
        {
            available[index] = TryAdvanceCandidate(
                _input,
                _plans[index],
                ref states[index],
                _minimumStart,
                out candidates[index]);
            return;
        }

        if (index == 0)
        {
            SetInlineAvailable(
                index,
                TryAdvanceCandidate(_input, _plans[index], ref _state0, _minimumStart, out _candidate0));
            return;
        }

        SetInlineAvailable(
            index,
            TryAdvanceCandidate(_input, _plans[index], ref _state1, _minimumStart, out _candidate1));
    }

    private int FindEarliestCandidateIndex()
    {
        var bestIndex = -1;
        for (var i = 0; i < _plans.Length; i++)
        {
            Check();
            if (!IsAvailable(i))
            {
                continue;
            }

            if (bestIndex < 0 || CompareCandidates(GetCandidate(i), GetCandidate(bestIndex)) < 0)
            {
                bestIndex = i;
            }
        }

        return bestIndex;
    }

    private bool IsAvailable(int index)
        => _available is { } available
            ? available[index]
            : (_inlineAvailable & (1 << index)) != 0;

    private Utf8StructuralCandidate GetCandidate(int index)
        => _candidates is { } candidates
            ? candidates[index]
            : index == 0 ? _candidate0 : _candidate1;

    private void SetInlineAvailable(int index, bool value)
    {
        var bit = (byte)(1 << index);
        _inlineAvailable = value
            ? (byte)(_inlineAvailable | bit)
            : (byte)(_inlineAvailable & ~bit);
    }

    private static int CompareCandidates(Utf8StructuralCandidate left, Utf8StructuralCandidate right)
    {
        var startComparison = left.StartIndex.CompareTo(right.StartIndex);
        if (startComparison != 0)
        {
            return startComparison;
        }

        var endComparison = CompareCandidateEnds(left.EndIndex, right.EndIndex);
        return endComparison;
    }

    private static int CompareCandidateEnds(int leftEnd, int rightEnd)
    {
        if (leftEnd < 0)
        {
            return rightEnd < 0 ? 0 : 1;
        }

        return rightEnd < 0 ? -1 : leftEnd.CompareTo(rightEnd);
    }

    private static Utf8StructuralSearchState CreateCandidateState(int startIndex)
        => new(
            new PreparedSearchScanState(startIndex, default),
            PreparedWindowScanState.Create(startIndex));

    private static bool TryAdvanceCandidate(
        ReadOnlySpan<byte> input,
        Utf8StructuralSearchPlan plan,
        ref Utf8StructuralSearchState state,
        int minimumStart,
        out Utf8StructuralCandidate candidate)
    {
        while (plan.TryFindNextCandidate(input, ref state, out candidate))
        {
            if (candidate.StartIndex >= minimumStart)
            {
                return true;
            }
        }

        candidate = default;
        return false;
    }

    private static bool IsScalarBoundaryByteOffset(ReadOnlySpan<byte> input, int byteOffset)
        => (uint)byteOffset <= (uint)input.Length &&
           (byteOffset == 0 || byteOffset == input.Length || (input[byteOffset] & 0xC0) != 0x80);

    private void Check()
    {
        if (!_controlled && _invocation.CheckpointHook is null) return;
        var breach = _invocation.TryChargeInputStep();
        if (breach != Utf8ExecutionLimitKind.None)
            throw new InvalidOperationException($"Scalar candidate portfolio exceeded {breach}.");
    }
}
