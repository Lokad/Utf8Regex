using System.Buffers;
using Lokad.Utf8Regex.Internal.FrontEnd;

namespace Lokad.Utf8Regex;

public sealed partial class Utf8Regex
{
    /// <summary>Gets the number of defined capture slots, excluding whole-match group zero.</summary>
    /// <remarks>Sparse numbers and repeated declarations of a shared slot do not add slots. Match participation does not affect this count.</remarks>
    public int CaptureGroupCount
    {
        get { using var use = _programMemory?.Enter(); return _program.CaptureCatalog.GroupNumbers.Length - 1; }
    }

    /// <summary>Gets the group number for a compact capture slot, including slot zero.</summary>
    /// <remarks>Byte-start Match writes slots in this order. Sparse or shared group numbers do not require sparse output storage. No metadata array is allocated.</remarks>
    public int GroupNumberFromCaptureSlot(int captureSlot)
    {
        using var use = _programMemory?.Enter();
        var numbers = _program.CaptureCatalog.GroupNumbers;
        if ((uint)captureSlot >= (uint)numbers.Length) throw new ArgumentOutOfRangeException(nameof(captureSlot));
        return numbers[captureSlot];
    }

    /// <summary>Copies the name of a compact capture slot as UTF-8, including slot zero.</summary>
    /// <remarks>Slots follow GroupNumberFromCaptureSlot; unnamed slots use their actual decimal number. Capacity, cancellation, funding and ownership follow TryCopyPatternUtf8.</remarks>
    public OperationStatus TryCopyGroupNameUtf8(int captureSlot, Span<byte> destination, out int bytesRequired,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var use = _programMemory?.Enter();
        var names = _program.CaptureCatalog.GroupNames;
        if ((uint)captureSlot >= (uint)names.Length) throw new ArgumentOutOfRangeException(nameof(captureSlot));
        return Utf8PatternMetadata.TryCopy(new Utf8ConstructionPattern(names[captureSlot]), destination,
            out bytesRequired, cancellationToken);
    }

    /// <summary>Finds a defined capture number from a strict UTF-8 group name, returning -1 when undefined.</summary>
    /// <remarks>Matches the existing ordinal name/numeric lookup, without decoding or allocating a key string. Validates the complete name. This explicit metadata lookup uses the supplied token independently of matching limits.</remarks>
    public int GroupNumberFromUtf8Name(ReadOnlySpan<byte> nameUtf8, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var use = _programMemory?.Enter();
        return _program.CaptureCatalog.GroupNumberFromUtf8Name(nameUtf8, cancellationToken);
    }

    /// <summary>Copies the original pattern as strict UTF-8 into caller-owned storage.</summary>
    /// <param name="destination">Receives the complete original source on success.</param>
    /// <param name="bytesRequired">The complete UTF-8 length on Done or DestinationTooSmall; zero on InvalidData.</param>
    /// <param name="cancellationToken">Observed synchronously at bounded copy/conversion checkpoints.</param>
    /// <returns>Done, DestinationTooSmall, or InvalidData for an unrepresentable legacy UTF-16 source.</returns>
    /// <remarks>No storage is allocated or retained. InvalidData and DestinationTooSmall leave the destination unchanged; cancellation may leave a partial copy. This explicit metadata operation uses the supplied token, independently of matching timeouts and numeric invocation budgets.</remarks>
    public OperationStatus TryCopyPatternUtf8(Span<byte> destination, out int bytesRequired,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var use = _programMemory?.Enter();
        return Utf8PatternMetadata.TryCopy(_patternText is not null ? new Utf8ConstructionPattern(_patternText)
            : new Utf8ConstructionPattern(_patternUtf8!), destination, out bytesRequired, cancellationToken);
    }
}
