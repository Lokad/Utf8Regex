using System.Buffers;
using System.Text;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Internal.Input;

internal readonly partial struct Utf8BoundaryMap
{
    private Utf8BoundaryMap(Utf8ValidationResult validation, int[] offsets, uint[] boundaries)
    {
        ByteLength = validation.ByteLength;
        Utf16Length = validation.Utf16Length;
        _isAscii = validation.IsAscii;
        _byteOffsets = offsets;
        _scalarBoundaryBits = boundaries;
    }

    internal static Utf8BoundaryMap Create(ReadOnlySpan<byte> input, Utf8ValidationResult validation,
        ref Utf8SliceInvocation invocation, out ulong reservation)
    {
        var breach = TryCreate(input, validation, ref invocation, out var map, out reservation);
        if (breach != Utf8ExecutionLimitKind.None)
            throw new InvalidOperationException($"UTF-8 boundary map exceeded the invocation {breach} budget.");
        return map;
    }

    // Admit complete modeled array extents alongside all caller-owned storage before
    // allocation. A successful caller must transfer this reservation with the
    // map; failure leaves its previous aggregate ledger intact. Ownership metadata
    // and public copied lifetimes remain separate resource-policy debt.
    internal static Utf8ExecutionLimitKind TryCreate(ReadOnlySpan<byte> input, Utf8ValidationResult validation,
        ref Utf8SliceInvocation invocation, out Utf8BoundaryMap map, out ulong reservation)
    {
        map = default;
        reservation = 0;
        var breach = invocation.TryChargeInputStep();
        if (breach != Utf8ExecutionLimitKind.None) return breach;
        invocation.Budget.ThrowIfCancellationRequested();
        if (validation.IsAscii)
        {
            map = new Utf8BoundaryMap(validation, [], []);
            return Utf8ExecutionLimitKind.None;
        }
        var offsetCount = (long)validation.Utf16Length + 1;
        var bitCount = (offsetCount + 31) / 32;
        if (offsetCount > Array.MaxLength || bitCount > Array.MaxLength)
            return Utf8ExecutionLimitKind.WorkingBytes;
        var bytes = checked(Utf8ConstructionBudget.ArrayBytes<int>((int)offsetCount)
            + Utf8ConstructionBudget.ArrayBytes<uint>((int)bitCount));
        breach = invocation.Budget.TryChargeResultBytes(bytes);
        if (breach != Utf8ExecutionLimitKind.None) return breach;
        if (!invocation.Ledger.TryReserve(bytes)) return Utf8ExecutionLimitKind.WorkingBytes;
        var transferred = false;
        try
        {
            breach = invocation.Budget.TryNoteWorkingBytes(invocation.Ledger.ReservedBytes);
            if (breach != Utf8ExecutionLimitKind.None) return breach;
            breach = invocation.TryChargeInputStep();
            if (breach != Utf8ExecutionLimitKind.None) return breach;
            var offsets = GC.AllocateUninitializedArray<int>((int)offsetCount);
            var boundaries = GC.AllocateUninitializedArray<uint>((int)bitCount);
            for (var index = 0; index < boundaries.Length; index += 512)
            {
                breach = invocation.TryChargeInputStep();
                if (breach != Utf8ExecutionLimitKind.None) return breach;
                invocation.Budget.ThrowIfCancellationRequested();
                boundaries.AsSpan(index, Math.Min(512, boundaries.Length - index)).Clear();
            }
            var candidate = new Utf8BoundaryMap(validation, offsets, boundaries);
            offsets[0] = 0;
            candidate.SetScalarBoundary(0);
            var unit = 0;
            var nextCheckpoint = 0;
            for (var index = 0; index < input.Length;)
            {
                if (index >= nextCheckpoint)
                {
                    breach = invocation.TryChargeInputStep();
                    if (breach != Utf8ExecutionLimitKind.None) return breach;
                    invocation.Budget.ThrowIfCancellationRequested();
                    nextCheckpoint = index > int.MaxValue - 2048 ? int.MaxValue : index + 2048;
                }
                if (Rune.DecodeFromUtf8(input[index..], out var rune, out var width) != OperationStatus.Done)
                    throw Utf8Validation.CreateInvalidUtf8Exception(index);
                if (!rune.IsBmp) offsets[++unit] = index;
                index += width;
                offsets[++unit] = index;
                candidate.SetScalarBoundary(unit);
            }
            breach = invocation.TryChargeInputStep();
            if (breach != Utf8ExecutionLimitKind.None) return breach;
            invocation.Budget.ThrowIfCancellationRequested();
            map = candidate;
            reservation = bytes;
            transferred = true;
            return Utf8ExecutionLimitKind.None;
        }
        finally
        {
            if (!transferred) invocation.Ledger.Release(bytes);
        }
    }
}
