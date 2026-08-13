using System.Buffers;
using Lokad.Utf8Regex.Internal.Planning;
using Lokad.Utf8Regex.Internal.Search;

namespace Lokad.Utf8Regex.Internal.Execution;

/// <summary>
/// Invocation-local stable merge of structural candidate sources. Each source
/// advances monotonically; two-source portfolios stay entirely inline.
/// </summary>
internal ref struct Utf8CandidatePortfolioCursor
{
    private readonly Utf8StructuralSearchPlan[] _plans;
    private readonly ReadOnlySpan<byte> _input;
    private Utf8StructuralSearchState _state0;
    private Utf8StructuralSearchState _state1;
    private Utf8StructuralCandidate _candidate0;
    private Utf8StructuralCandidate _candidate1;
    private byte _inlineAvailable;
    private Utf8StructuralSearchState[]? _states;
    private Utf8StructuralCandidate[]? _candidates;
    private bool[]? _available;
    private int _minimumStart;

    public Utf8CandidatePortfolioCursor(
        Utf8StructuralSearchPlan[] plans,
        ReadOnlySpan<byte> input,
        int startIndex)
    {
        _plans = plans;
        _input = input;
        _state0 = CreateCandidateState(startIndex);
        _state1 = CreateCandidateState(startIndex);
        _candidate0 = default;
        _candidate1 = default;
        _inlineAvailable = 0;
        _states = null;
        _candidates = null;
        _available = null;
        _minimumStart = startIndex;
        SourceAdvanceCount = 0;

        if (plans.Length > 2)
        {
            _states = ArrayPool<Utf8StructuralSearchState>.Shared.Rent(plans.Length);
            _candidates = ArrayPool<Utf8StructuralCandidate>.Shared.Rent(plans.Length);
            _available = ArrayPool<bool>.Shared.Rent(plans.Length);
            Array.Clear(_available, 0, plans.Length);
            for (var i = 0; i < plans.Length; i++)
            {
                _states[i] = CreateCandidateState(startIndex);
            }
        }

        for (var i = 0; i < plans.Length; i++)
        {
            AdvanceSource(i);
        }
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
            ArrayPool<Utf8StructuralSearchState>.Shared.Return(states, clearArray: true);
            _states = null;
        }

        if (_candidates is { } candidates)
        {
            ArrayPool<Utf8StructuralCandidate>.Shared.Return(candidates);
            _candidates = null;
        }

        if (_available is { } available)
        {
            ArrayPool<bool>.Shared.Return(available, clearArray: true);
            _available = null;
        }
    }

    private void AdvanceSource(int index)
    {
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
}
